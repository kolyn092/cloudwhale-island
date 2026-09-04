using System;

namespace CloudWhale.Game
{
    public enum HouseStage
    {
        Unbuilt = 0,
        Foundation = 1,
        Framing = 2,
        Complete = 3,
    }

    public enum GameReason
    {
        None = 0,
        InsufficientResources,
        HouseAlreadyHasFoundation,
        HouseAlreadyComplete,
        RecoveredInvalidSave,
        ClockMovedBackward,
        StorageUnavailable,
    }

    [Serializable]
    public sealed class GameStateData
    {
        public const int CurrentVersion = 1;

        public int version;
        public long savedAtUnixSeconds;
        public int driftwood;
        public int cloudCotton;
        public int dew;
        public int stardust;
        public int houseStage;

        // Extension fields are kept in every save now so later units can add their rules without a format reset.
        public int starlightParts;
        public FacilityExtensionState[] facilities;
        public string[] unlockedStoryIds;
        public DecorationPlacement[] decorations;

        public static GameStateData Fresh(DateTime utcNow)
        {
            return new GameStateData
            {
                version = CurrentVersion,
                savedAtUnixSeconds = ToUnixSeconds(utcNow),
                facilities = Array.Empty<FacilityExtensionState>(),
                unlockedStoryIds = Array.Empty<string>(),
                decorations = Array.Empty<DecorationPlacement>(),
            };
        }

        public void NormalizeExtensions()
        {
            facilities = facilities ?? Array.Empty<FacilityExtensionState>();
            unlockedStoryIds = unlockedStoryIds ?? Array.Empty<string>();
            decorations = decorations ?? Array.Empty<DecorationPlacement>();
        }

        public static long ToUnixSeconds(DateTime utcNow)
        {
            return new DateTimeOffset(utcNow.ToUniversalTime()).ToUnixTimeSeconds();
        }
    }

    [Serializable]
    public sealed class FacilityExtensionState
    {
        public string id;
        public int stage;
    }

    [Serializable]
    public sealed class DecorationPlacement
    {
        public string slotId;
        public string decorationId;
    }

    public readonly struct ResourceAmounts : IEquatable<ResourceAmounts>
    {
        public static readonly ResourceAmounts Zero = new ResourceAmounts(0, 0, 0, 0);

        public ResourceAmounts(int driftwood, int cloudCotton, int dew, int stardust)
        {
            if (driftwood < 0 || cloudCotton < 0 || dew < 0 || stardust < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(driftwood), "Resources cannot be negative.");
            }

            Driftwood = driftwood;
            CloudCotton = cloudCotton;
            Dew = dew;
            Stardust = stardust;
        }

        public int Driftwood { get; }
        public int CloudCotton { get; }
        public int Dew { get; }
        public int Stardust { get; }

        public bool Equals(ResourceAmounts other)
        {
            return Driftwood == other.Driftwood && CloudCotton == other.CloudCotton && Dew == other.Dew && Stardust == other.Stardust;
        }

        public override bool Equals(object obj) => obj is ResourceAmounts other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Driftwood, CloudCotton, Dew, Stardust);
        public static bool operator ==(ResourceAmounts left, ResourceAmounts right) => left.Equals(right);
        public static bool operator !=(ResourceAmounts left, ResourceAmounts right) => !left.Equals(right);
    }

    public readonly struct HouseFoundationCost
    {
        public static readonly HouseFoundationCost Zero = new HouseFoundationCost(0, 0, 0, 0);
        public HouseFoundationCost(int driftwood, int cloudCotton, int dew, int stardust)
        {
            Resources = new ResourceAmounts(driftwood, cloudCotton, dew, stardust);
        }

        public ResourceAmounts Resources { get; }
    }

    public readonly struct GameStateSnapshot
    {
        public GameStateSnapshot(ResourceAmounts resources, HouseStage houseStage, int starlightParts)
        {
            Resources = resources;
            HouseStage = houseStage;
            StarlightParts = starlightParts;
        }

        public ResourceAmounts Resources { get; }
        public HouseStage HouseStage { get; }
        public int StarlightParts { get; }
    }
}
