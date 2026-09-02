using System;

namespace CloudWhale.Game
{
    public interface IClock
    {
        DateTime UtcNow { get; }
    }

    public sealed class SystemClock : IClock
    {
        public DateTime UtcNow => DateTime.UtcNow;
    }

    public readonly struct ProductionSettings
    {
        // Runtime tuning defaults. Keep the foundation cost nonzero so starting a new game
        // still requires a production loop before a house can be built.
        public const int DefaultIntervalSeconds = 60;
        public const int DefaultAmountPerInterval = 1;
        public static readonly HouseFoundationCost DefaultHouseFoundationCost = new HouseFoundationCost(5, 5, 5, 5);
        public static readonly ProductionSettings Default = new ProductionSettings(
            DefaultIntervalSeconds,
            DefaultAmountPerInterval,
            DefaultHouseFoundationCost);

        public ProductionSettings(int intervalSeconds, int amountPerInterval, HouseFoundationCost houseFoundationCost)
        {
            if (intervalSeconds <= 0) throw new ArgumentOutOfRangeException(nameof(intervalSeconds));
            if (amountPerInterval <= 0) throw new ArgumentOutOfRangeException(nameof(amountPerInterval));
            IntervalSeconds = intervalSeconds;
            AmountPerInterval = amountPerInterval;
            HouseFoundationCost = houseFoundationCost;
        }

        public int IntervalSeconds { get; }
        public int AmountPerInterval { get; }
        public HouseFoundationCost HouseFoundationCost { get; }
    }

    public sealed class GameSession
    {
        private const long MaxOfflineSeconds = 24 * 60 * 60;
        private readonly IStateStorage storage;
        private readonly IClock clock;
        private readonly ProductionSettings production;
        private GameStateData data;

        public GameSession(IStateStorage storage, IClock clock, ProductionSettings production)
        {
            this.storage = storage ?? throw new ArgumentNullException(nameof(storage));
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            this.production = production;
        }

        public GameReason LastReason { get; private set; }
        public GameStateSnapshot State => ToSnapshot(data ?? GameStateData.Fresh(clock.UtcNow));
        public int ProductionIntervalSeconds => production.IntervalSeconds;

        public void Load()
        {
            var now = clock.UtcNow;
            if (!storage.TryRead(out var serialized, out _))
            {
                data = GameStateData.Fresh(now);
                LastReason = GameReason.StorageUnavailable;
                return;
            }

            if (string.IsNullOrWhiteSpace(serialized))
            {
                data = GameStateData.Fresh(now);
                LastReason = GameReason.None;
                Persist(now);
                return;
            }

            if (!GameStateSerializer.TryDeserialize(serialized, out data) || !IsValid(data))
            {
                data = GameStateData.Fresh(now);
                LastReason = GameReason.RecoveredInvalidSave;
                Persist(now, preserveReason: true);
                return;
            }

            var savedAt = FromUnixSeconds(data.savedAtUnixSeconds);
            if (savedAt > now)
            {
                LastReason = GameReason.ClockMovedBackward;
                Persist(now, preserveReason: true);
                return;
            }

            var elapsedSeconds = Math.Min((long)(now - savedAt).TotalSeconds, MaxOfflineSeconds);
            GrantCompletedIntervals(elapsedSeconds);
            LastReason = GameReason.None;
            Persist(now);
        }

        // Call this from the open-game timer. Remainders are intentionally discarded after each calculation.
        public void AdvanceWhileOpen()
        {
            EnsureLoaded();
            var now = clock.UtcNow;
            var savedAt = FromUnixSeconds(data.savedAtUnixSeconds);
            if (savedAt > now)
            {
                LastReason = GameReason.ClockMovedBackward;
                Persist(now, preserveReason: true);
                return;
            }

            var intervals = GrantCompletedIntervals((long)(now - savedAt).TotalSeconds);
            if (intervals > 0)
            {
                LastReason = GameReason.None;
                Persist(now);
            }
        }

        public void AddResources(ResourceAmounts amount)
        {
            EnsureLoaded();
            data.driftwood = SaturatingAdd(data.driftwood, amount.Driftwood);
            data.cloudCotton = SaturatingAdd(data.cloudCotton, amount.CloudCotton);
            data.dew = SaturatingAdd(data.dew, amount.Dew);
            data.stardust = SaturatingAdd(data.stardust, amount.Stardust);
            LastReason = GameReason.None;
            Persist(clock.UtcNow);
        }

        public bool TryBuildHouseFoundation()
        {
            EnsureLoaded();
            if ((HouseStage)data.houseStage != HouseStage.Unbuilt)
            {
                LastReason = GameReason.HouseAlreadyHasFoundation;
                return false;
            }

            var cost = production.HouseFoundationCost.Resources;
            if (data.driftwood < cost.Driftwood || data.cloudCotton < cost.CloudCotton || data.dew < cost.Dew || data.stardust < cost.Stardust)
            {
                LastReason = GameReason.InsufficientResources;
                return false;
            }

            data.driftwood -= cost.Driftwood;
            data.cloudCotton -= cost.CloudCotton;
            data.dew -= cost.Dew;
            data.stardust -= cost.Stardust;
            data.houseStage = (int)HouseStage.Foundation;
            LastReason = GameReason.None;
            Persist(clock.UtcNow);
            return true;
        }

        private int GrantCompletedIntervals(long elapsedSeconds)
        {
            var intervals = elapsedSeconds / production.IntervalSeconds;
            if (intervals <= 0) return 0;
            var grant = intervals > int.MaxValue / production.AmountPerInterval ? int.MaxValue : (int)intervals * production.AmountPerInterval;
            data.driftwood = SaturatingAdd(data.driftwood, grant);
            data.cloudCotton = SaturatingAdd(data.cloudCotton, grant);
            data.dew = SaturatingAdd(data.dew, grant);
            data.stardust = SaturatingAdd(data.stardust, grant);
            return (int)Math.Min(intervals, int.MaxValue);
        }

        private void Persist(DateTime now, bool preserveReason = false)
        {
            data.savedAtUnixSeconds = GameStateData.ToUnixSeconds(now);
            if (!storage.TryWrite(GameStateSerializer.Serialize(data), out _))
            {
                LastReason = GameReason.StorageUnavailable;
            }
            else if (!preserveReason)
            {
                LastReason = GameReason.None;
            }
        }

        private void EnsureLoaded()
        {
            if (data == null) throw new InvalidOperationException("Load must be called before changing game state.");
        }

        private static int SaturatingAdd(int current, int amount) => amount > int.MaxValue - current ? int.MaxValue : current + amount;

        private static bool IsValid(GameStateData state)
        {
            if (state == null || !HasValidTimestamp(state.savedAtUnixSeconds)) return false;
            return state.version == GameStateData.CurrentVersion
                && state.driftwood >= 0 && state.cloudCotton >= 0 && state.dew >= 0 && state.stardust >= 0
                && state.starlightParts >= 0
                && state.houseStage >= (int)HouseStage.Unbuilt && state.houseStage <= (int)HouseStage.Foundation;
        }

        private static DateTime FromUnixSeconds(long seconds)
        {
            try { return DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime; }
            catch (ArgumentOutOfRangeException) { return DateTime.UnixEpoch; }
        }

        private static bool HasValidTimestamp(long seconds)
        {
            try
            {
                DateTimeOffset.FromUnixTimeSeconds(seconds);
                return true;
            }
            catch (ArgumentOutOfRangeException)
            {
                return false;
            }
        }

        private static GameStateSnapshot ToSnapshot(GameStateData state)
        {
            return new GameStateSnapshot(new ResourceAmounts(state.driftwood, state.cloudCotton, state.dew, state.stardust), (HouseStage)state.houseStage, state.starlightParts);
        }
    }

    /// <summary>Testable timer boundary for an open game. A MonoBehaviour can call Tick from Update.</summary>
    public sealed class OpenGameProductionController
    {
        private readonly GameSession session;
        private readonly IClock clock;
        private DateTime nextProductionAt;

        public OpenGameProductionController(GameSession session, IClock clock)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            nextProductionAt = clock.UtcNow.AddSeconds(session.ProductionIntervalSeconds);
        }

        public bool Tick()
        {
            if (clock.UtcNow < nextProductionAt) return false;
            session.AdvanceWhileOpen();
            nextProductionAt = clock.UtcNow.AddSeconds(session.ProductionIntervalSeconds);
            return true;
        }
    }
}
