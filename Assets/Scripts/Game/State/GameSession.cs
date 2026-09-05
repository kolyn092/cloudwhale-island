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
        // Runtime tuning defaults. All house stages share this one adjustable cost.
        public const int DefaultIntervalSeconds = 60;
        public const int DefaultAmountPerInterval = 1;
        public static readonly HouseFoundationCost DefaultHouseStageCost = new HouseFoundationCost(5, 5, 5, 5);
        // Kept for callers that display the first stage while the presentation catches up with all stages.
        public static readonly HouseFoundationCost DefaultHouseFoundationCost = DefaultHouseStageCost;
        public static readonly ProductionSettings Default = new ProductionSettings(
            DefaultIntervalSeconds,
            DefaultAmountPerInterval,
            DefaultHouseStageCost);

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
        public HouseFoundationCost HouseStageCost => HouseFoundationCost;
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
        // Presentation reads costs only; the public build actions remain the mutation boundary.
        public HouseFoundationCost HouseFoundationCost => production.HouseFoundationCost;
        public HouseFoundationCost HouseStageCost => production.HouseFoundationCost;
        public HouseFoundationCost GardenStageCost => production.HouseStageCost;

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

            RestoreGarden(data);

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
                LastReason = (HouseStage)data.houseStage == HouseStage.Complete
                    ? GameReason.HouseAlreadyComplete
                    : GameReason.HouseAlreadyHasFoundation;
                return false;
            }

            return TryBuildNextHouseStage();
        }

        // The presentation invokes this action to build only the next allowed house stage.
        public bool TryBuildNextHouseStage()
        {
            EnsureLoaded();
            var currentStage = (HouseStage)data.houseStage;
            if (currentStage == HouseStage.Complete)
            {
                LastReason = GameReason.HouseAlreadyComplete;
                return false;
            }

            var cost = production.HouseStageCost.Resources;
            if (data.driftwood < cost.Driftwood || data.cloudCotton < cost.CloudCotton || data.dew < cost.Dew || data.stardust < cost.Stardust)
            {
                LastReason = GameReason.InsufficientResources;
                return false;
            }

            data.driftwood -= cost.Driftwood;
            data.cloudCotton -= cost.CloudCotton;
            data.dew -= cost.Dew;
            data.stardust -= cost.Stardust;
            data.houseStage = (int)(currentStage + 1);
            LastReason = GameReason.None;
            Persist(clock.UtcNow);
            return true;
        }

        // The garden is the sole facility in the common facility list. It can start only after the house is complete.
        public bool TryBuildNextGardenStage()
        {
            EnsureLoaded();
            if ((HouseStage)data.houseStage != HouseStage.Complete)
            {
                LastReason = GameReason.HouseMustBeComplete;
                return false;
            }

            var currentStage = GetGardenStage(data);
            if (currentStage == GardenStage.Complete)
            {
                LastReason = GameReason.GardenAlreadyComplete;
                return false;
            }

            var cost = production.HouseStageCost.Resources;
            if (data.driftwood < cost.Driftwood || data.cloudCotton < cost.CloudCotton || data.dew < cost.Dew || data.stardust < cost.Stardust)
            {
                LastReason = GameReason.InsufficientResources;
                return false;
            }

            data.driftwood -= cost.Driftwood;
            data.cloudCotton -= cost.CloudCotton;
            data.dew -= cost.Dew;
            data.stardust -= cost.Stardust;
            var nextStage = (GardenStage)((int)currentStage + 1);
            data.facilities[0].stage = (int)nextStage;
            if (nextStage == GardenStage.Complete)
            {
                data.gardenCompletedAtUnixSeconds = GameStateData.ToUnixSeconds(clock.UtcNow);
            }

            LastReason = GameReason.None;
            Persist(clock.UtcNow);
            return true;
        }

        // Called by the runtime when the game becomes inactive or is closing. This retries persistence
        // without changing resources, house progress, or any other gameplay state.
        public void SaveCurrentProgress()
        {
            EnsureLoaded();
            Persist(clock.UtcNow);
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

            if (GetGardenStage(data) == GardenStage.Complete)
            {
                var completedAt = data.gardenCompletedAtUnixSeconds;
                var savedAt = data.savedAtUnixSeconds;
                var elapsedUntilComplete = Math.Max(0, completedAt - savedAt);
                // A cycle completing exactly when the garden finishes was produced before completion.
                // The bonus starts at the next completed cycle, not at this boundary.
                var firstBonusInterval = elapsedUntilComplete / production.IntervalSeconds + 1;
                var bonusIntervals = Math.Max(0, intervals - firstBonusInterval + 1);
                var bonus = bonusIntervals > int.MaxValue / production.AmountPerInterval ? int.MaxValue : (int)bonusIntervals * production.AmountPerInterval;
                data.cloudCotton = SaturatingAdd(data.cloudCotton, bonus);
                data.dew = SaturatingAdd(data.dew, bonus);
            }
            return (int)Math.Min(intervals, int.MaxValue);
        }

        private void Persist(DateTime now, bool preserveReason = false)
        {
            var lastSuccessfulSavedAt = data.savedAtUnixSeconds;
            data.savedAtUnixSeconds = GameStateData.ToUnixSeconds(now);
            if (!storage.TryWrite(GameStateSerializer.Serialize(data), out _))
            {
                data.savedAtUnixSeconds = lastSuccessfulSavedAt;
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
                && state.houseStage >= (int)HouseStage.Unbuilt && state.houseStage <= (int)HouseStage.Complete
                && HasValidFacilities(state);
        }

        private static bool HasValidFacilities(GameStateData state)
        {
            if (state.facilities == null || state.facilities.Length == 0) return state.gardenCompletedAtUnixSeconds == 0;
            if (state.facilities.Length != 1) return false;
            var garden = state.facilities[0];
            if (garden == null || garden.id != FacilityExtensionState.GardenId) return false;
            if (garden.stage < (int)GardenStage.Locked || garden.stage > (int)GardenStage.Complete) return false;

            var gardenStage = (GardenStage)garden.stage;
            if (state.houseStage != (int)HouseStage.Complete && gardenStage != GardenStage.Locked) return false;
            if (gardenStage != GardenStage.Complete) return state.gardenCompletedAtUnixSeconds == 0;

            return state.gardenCompletedAtUnixSeconds != 0
                && HasValidTimestamp(state.gardenCompletedAtUnixSeconds)
                && state.gardenCompletedAtUnixSeconds <= state.savedAtUnixSeconds;
        }

        private static void RestoreGarden(GameStateData state)
        {
            if (state.facilities == null || state.facilities.Length == 0)
            {
                state.facilities = new[] { new FacilityExtensionState { id = FacilityExtensionState.GardenId, stage = (int)GardenStage.Locked } };
                return;
            }
        }

        private static GardenStage GetGardenStage(GameStateData state) => (GardenStage)state.facilities[0].stage;

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
            return new GameStateSnapshot(new ResourceAmounts(state.driftwood, state.cloudCotton, state.dew, state.stardust), (HouseStage)state.houseStage, GetGardenStage(state), state.starlightParts);
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

        /// <summary>Returns the whole seconds remaining until the next open-game production cycle.</summary>
        public int SecondsUntilNextProduction
        {
            get
            {
                var remaining = nextProductionAt - clock.UtcNow;
                return remaining <= TimeSpan.Zero ? 0 : (int)Math.Ceiling(remaining.TotalSeconds);
            }
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
