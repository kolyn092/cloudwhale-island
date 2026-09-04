using System;
using NUnit.Framework;
using CloudWhale.Game;
using UnityEngine;

namespace CloudWhale.Tests
{
    public sealed class GameSessionTests
    {
        [Test]
        public void Load_GrantsOnlyCompletedIntervals_AndCapsOfflineTimeAt24Hours()
        {
            var storage = new InMemoryStorage();
            var clock = new ManualClock(new DateTime(2030, 1, 2, 12, 0, 0, DateTimeKind.Utc));
            storage.Value = GameStateSerializer.Serialize(GameStateData.Fresh(clock.UtcNow.AddDays(-3)));

            var session = new GameSession(storage, clock, new ProductionSettings(60, 2, HouseFoundationCost.Zero));
            session.Load();

            Assert.That(session.State.Resources.Driftwood, Is.EqualTo(2880));
            Assert.That(session.State.Resources.CloudCotton, Is.EqualTo(2880));
            Assert.That(session.State.Resources.Dew, Is.EqualTo(2880));
            Assert.That(session.State.Resources.Stardust, Is.EqualTo(2880));
        }

        [Test]
        public void BuildHouseFoundation_DeductsOnce_AndRejectsRepeat()
        {
            var storage = new InMemoryStorage();
            var clock = new ManualClock(new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            var cost = new HouseFoundationCost(3, 4, 5, 6);
            var session = new GameSession(storage, clock, new ProductionSettings(60, 1, cost));
            session.Load();
            session.AddResources(new ResourceAmounts(3, 4, 5, 6));

            Assert.That(session.TryBuildHouseFoundation(), Is.True);
            Assert.That(session.State.HouseStage, Is.EqualTo(HouseStage.Foundation));
            Assert.That(session.State.Resources, Is.EqualTo(ResourceAmounts.Zero));
            Assert.That(session.TryBuildHouseFoundation(), Is.False);
            Assert.That(session.State.Resources, Is.EqualTo(ResourceAmounts.Zero));
            Assert.That(session.LastReason, Is.EqualTo(GameReason.HouseAlreadyHasFoundation));
        }

        [Test]
        public void BuildHouseFoundation_LeavesStateUnchanged_WhenResourcesAreInsufficient()
        {
            var storage = new InMemoryStorage();
            var clock = new ManualClock(DateTime.UtcNow);
            var session = new GameSession(storage, clock, new ProductionSettings(60, 1, new HouseFoundationCost(1, 1, 1, 1)));
            session.Load();

            Assert.That(session.TryBuildHouseFoundation(), Is.False);
            Assert.That(session.State.HouseStage, Is.EqualTo(HouseStage.Unbuilt));
            Assert.That(session.State.Resources, Is.EqualTo(ResourceAmounts.Zero));
            Assert.That(session.LastReason, Is.EqualTo(GameReason.InsufficientResources));
        }

        [Test]
        public void DefaultFoundationCost_RequiresEveryResource_AndIsDeductedOnlyOnce()
        {
            var storage = new InMemoryStorage();
            var clock = new ManualClock(new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            var cost = ProductionSettings.Default.HouseFoundationCost.Resources;
            var session = new GameSession(storage, clock, ProductionSettings.Default);
            session.Load();

            Assert.That(cost.Driftwood, Is.GreaterThan(0));
            Assert.That(cost.CloudCotton, Is.GreaterThan(0));
            Assert.That(cost.Dew, Is.GreaterThan(0));
            Assert.That(cost.Stardust, Is.GreaterThan(0));
            Assert.That(session.TryBuildHouseFoundation(), Is.False);
            Assert.That(session.LastReason, Is.EqualTo(GameReason.InsufficientResources));

            session.AddResources(new ResourceAmounts(
                cost.Driftwood + 1,
                cost.CloudCotton + 1,
                cost.Dew + 1,
                cost.Stardust + 1));

            Assert.That(session.TryBuildHouseFoundation(), Is.True);
            Assert.That(session.State.HouseStage, Is.EqualTo(HouseStage.Foundation));
            Assert.That(session.State.Resources, Is.EqualTo(new ResourceAmounts(1, 1, 1, 1)));
            Assert.That(session.TryBuildHouseFoundation(), Is.False);
            Assert.That(session.State.Resources, Is.EqualTo(new ResourceAmounts(1, 1, 1, 1)));
        }

        [Test]
        public void BuildNextHouseStage_AdvancesFoundationFramingAndCompletion_WithFiveOfEveryResourcePerStage()
        {
            var storage = new InMemoryStorage();
            var clock = new ManualClock(new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            var session = new GameSession(storage, clock, ProductionSettings.Default);
            session.Load();
            session.AddResources(new ResourceAmounts(15, 15, 15, 15));
            var writesAfterResources = storage.WriteCount;

            clock.UtcNow = clock.UtcNow.AddSeconds(1);
            Assert.That(session.TryBuildNextHouseStage(), Is.True);
            Assert.That(session.State.HouseStage, Is.EqualTo(HouseStage.Foundation));
            Assert.That(session.State.Resources, Is.EqualTo(new ResourceAmounts(10, 10, 10, 10)));
            Assert.That(SavedTimestamp(storage), Is.EqualTo(GameStateData.ToUnixSeconds(clock.UtcNow)));

            clock.UtcNow = clock.UtcNow.AddSeconds(1);
            Assert.That(session.TryBuildNextHouseStage(), Is.True);
            Assert.That(session.State.HouseStage, Is.EqualTo(HouseStage.Framing));
            Assert.That(session.State.Resources, Is.EqualTo(new ResourceAmounts(5, 5, 5, 5)));
            Assert.That(SavedTimestamp(storage), Is.EqualTo(GameStateData.ToUnixSeconds(clock.UtcNow)));

            clock.UtcNow = clock.UtcNow.AddSeconds(1);
            Assert.That(session.TryBuildNextHouseStage(), Is.True);
            Assert.That(session.State.HouseStage, Is.EqualTo(HouseStage.Complete));
            Assert.That(session.State.Resources, Is.EqualTo(ResourceAmounts.Zero));
            Assert.That(storage.WriteCount, Is.EqualTo(writesAfterResources + 3));
            Assert.That(GameStateSerializer.TryDeserialize(storage.Value, out var saved), Is.True);
            Assert.That(saved.houseStage, Is.EqualTo((int)HouseStage.Complete));
            Assert.That(saved.savedAtUnixSeconds, Is.EqualTo(GameStateData.ToUnixSeconds(clock.UtcNow)));
        }

        [Test]
        public void BuildNextHouseStage_LeavesResourcesStageAndSaveUnchanged_WhenAnyMaterialIsInsufficient()
        {
            var storage = new InMemoryStorage();
            var clock = new ManualClock(new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            var session = new GameSession(storage, clock, ProductionSettings.Default);
            session.Load();
            session.AddResources(new ResourceAmounts(5, 5, 5, 5));
            Assert.That(session.TryBuildNextHouseStage(), Is.True);
            session.AddResources(new ResourceAmounts(5, 5, 4, 5));
            var savedBeforeRejectedBuild = storage.Value;
            var writesBeforeRejectedBuild = storage.WriteCount;

            Assert.That(session.TryBuildNextHouseStage(), Is.False);
            Assert.That(session.State.HouseStage, Is.EqualTo(HouseStage.Foundation));
            Assert.That(session.State.Resources, Is.EqualTo(new ResourceAmounts(5, 5, 4, 5)));
            Assert.That(storage.Value, Is.EqualTo(savedBeforeRejectedBuild));
            Assert.That(storage.WriteCount, Is.EqualTo(writesBeforeRejectedBuild));
            Assert.That(session.LastReason, Is.EqualTo(GameReason.InsufficientResources));
        }

        [Test]
        public void BuildNextHouseStage_RejectsCompletedHouseWithoutChangingStateOrSave()
        {
            var storage = new InMemoryStorage();
            var clock = new ManualClock(new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            var session = new GameSession(storage, clock, ProductionSettings.Default);
            session.Load();
            session.AddResources(new ResourceAmounts(15, 15, 15, 15));
            Assert.That(session.TryBuildNextHouseStage(), Is.True);
            Assert.That(session.TryBuildNextHouseStage(), Is.True);
            Assert.That(session.TryBuildNextHouseStage(), Is.True);
            var completedState = session.State;
            var savedBeforeRetry = storage.Value;
            var writesBeforeRetry = storage.WriteCount;

            Assert.That(session.TryBuildNextHouseStage(), Is.False);
            Assert.That(session.State.HouseStage, Is.EqualTo(HouseStage.Complete));
            Assert.That(session.State.Resources, Is.EqualTo(completedState.Resources));
            Assert.That(storage.Value, Is.EqualTo(savedBeforeRetry));
            Assert.That(storage.WriteCount, Is.EqualTo(writesBeforeRetry));
            Assert.That(session.LastReason, Is.EqualTo(GameReason.HouseAlreadyComplete));
        }

        [Test]
        public void BuildGarden_RequiresCompletedHouse_AndLeavesStateAndSaveUnchanged()
        {
            var storage = new InMemoryStorage();
            var session = new GameSession(storage, new ManualClock(DateTime.UtcNow), ProductionSettings.Default);
            session.Load();
            var savedBeforeAttempt = storage.Value;
            var writesBeforeAttempt = storage.WriteCount;

            Assert.That(session.TryBuildNextGardenStage(), Is.False);
            Assert.That(session.State.GardenStage, Is.EqualTo(GardenStage.Locked));
            Assert.That(session.State.Resources, Is.EqualTo(ResourceAmounts.Zero));
            Assert.That(storage.Value, Is.EqualTo(savedBeforeAttempt));
            Assert.That(storage.WriteCount, Is.EqualTo(writesBeforeAttempt));
            Assert.That(session.LastReason, Is.EqualTo(GameReason.HouseMustBeComplete));
        }

        [Test]
        public void BuildGarden_UsesFiveOfEachResourcePerStage_PersistsSingleGarden_AndRejectsCompletionRetry()
        {
            var storage = new InMemoryStorage();
            var clock = new ManualClock(new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            var session = new GameSession(storage, clock, ProductionSettings.Default);
            session.Load();
            session.AddResources(new ResourceAmounts(30, 30, 30, 30));
            Assert.That(session.TryBuildNextHouseStage(), Is.True);
            Assert.That(session.TryBuildNextHouseStage(), Is.True);
            Assert.That(session.TryBuildNextHouseStage(), Is.True);
            var writesBeforeGarden = storage.WriteCount;

            Assert.That(session.TryBuildNextGardenStage(), Is.True);
            Assert.That(session.State.GardenStage, Is.EqualTo(GardenStage.Foundation));
            Assert.That(session.State.Resources, Is.EqualTo(new ResourceAmounts(10, 10, 10, 10)));
            Assert.That(session.TryBuildNextGardenStage(), Is.True);
            Assert.That(session.State.GardenStage, Is.EqualTo(GardenStage.Framing));
            Assert.That(session.State.Resources, Is.EqualTo(new ResourceAmounts(5, 5, 5, 5)));
            Assert.That(session.TryBuildNextGardenStage(), Is.True);
            Assert.That(session.State.GardenStage, Is.EqualTo(GardenStage.Complete));
            Assert.That(session.State.Resources, Is.EqualTo(ResourceAmounts.Zero));
            Assert.That(storage.WriteCount, Is.EqualTo(writesBeforeGarden + 3));
            Assert.That(GameStateSerializer.TryDeserialize(storage.Value, out var saved), Is.True);
            Assert.That(saved.facilities, Has.Length.EqualTo(1));
            Assert.That(saved.facilities[0].id, Is.EqualTo("garden"));
            Assert.That(saved.facilities[0].stage, Is.EqualTo((int)GardenStage.Complete));

            var savedBeforeRetry = storage.Value;
            Assert.That(session.TryBuildNextGardenStage(), Is.False);
            Assert.That(storage.Value, Is.EqualTo(savedBeforeRetry));
            Assert.That(session.LastReason, Is.EqualTo(GameReason.GardenAlreadyComplete));
        }

        [Test]
        public void BuildGarden_RejectsInsufficientResourcesWithoutChangingStateOrSave()
        {
            var storage = new InMemoryStorage();
            var session = new GameSession(storage, new ManualClock(DateTime.UtcNow), ProductionSettings.Default);
            session.Load();
            session.AddResources(new ResourceAmounts(15, 15, 15, 15));
            Assert.That(session.TryBuildNextHouseStage(), Is.True);
            Assert.That(session.TryBuildNextHouseStage(), Is.True);
            Assert.That(session.TryBuildNextHouseStage(), Is.True);
            session.AddResources(new ResourceAmounts(5, 5, 4, 5));
            var stateBeforeAttempt = session.State;
            var savedBeforeAttempt = storage.Value;

            Assert.That(session.TryBuildNextGardenStage(), Is.False);
            Assert.That(session.State.Resources, Is.EqualTo(stateBeforeAttempt.Resources));
            Assert.That(session.State.GardenStage, Is.EqualTo(GardenStage.Locked));
            Assert.That(storage.Value, Is.EqualTo(savedBeforeAttempt));
            Assert.That(session.LastReason, Is.EqualTo(GameReason.InsufficientResources));
        }

        [Test]
        public void Load_EmptyFacilityList_RestoresLockedGardenWithoutChangingSaveVersion()
        {
            var clock = new ManualClock(new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            var legacy = GameStateData.Fresh(clock.UtcNow);
            legacy.facilities = Array.Empty<FacilityExtensionState>();
            var storage = new InMemoryStorage { Value = GameStateSerializer.Serialize(legacy) };
            var session = new GameSession(storage, clock, ProductionSettings.Default);

            session.Load();

            Assert.That(session.State.GardenStage, Is.EqualTo(GardenStage.Locked));
            Assert.That(GameStateSerializer.TryDeserialize(storage.Value, out var saved), Is.True);
            Assert.That(saved.version, Is.EqualTo(GameStateData.CurrentVersion));
            Assert.That(saved.facilities, Has.Length.EqualTo(1));
            Assert.That(saved.facilities[0].id, Is.EqualTo("garden"));
        }

        [TestCase("other", 0)]
        [TestCase("garden", 99)]
        public void Load_InvalidFacility_RecoversFreshState(string id, int stage)
        {
            var invalid = GameStateData.Fresh(DateTime.UtcNow);
            invalid.facilities = new[] { new FacilityExtensionState { id = id, stage = stage } };
            var storage = new InMemoryStorage { Value = GameStateSerializer.Serialize(invalid) };
            var session = new GameSession(storage, new ManualClock(DateTime.UtcNow), ProductionSettings.Default);

            session.Load();

            Assert.That(session.LastReason, Is.EqualTo(GameReason.RecoveredInvalidSave));
            Assert.That(session.State.HouseStage, Is.EqualTo(HouseStage.Unbuilt));
            Assert.That(session.State.GardenStage, Is.EqualTo(GardenStage.Locked));
        }

        [Test]
        public void Load_DuplicateGarden_RecoversFreshState()
        {
            var invalid = GameStateData.Fresh(DateTime.UtcNow);
            invalid.facilities = new[]
            {
                new FacilityExtensionState { id = "garden", stage = 0 },
                new FacilityExtensionState { id = "garden", stage = 0 },
            };
            var storage = new InMemoryStorage { Value = GameStateSerializer.Serialize(invalid) };
            var session = new GameSession(storage, new ManualClock(DateTime.UtcNow), ProductionSettings.Default);

            session.Load();

            Assert.That(session.LastReason, Is.EqualTo(GameReason.RecoveredInvalidSave));
            Assert.That(session.State.GardenStage, Is.EqualTo(GardenStage.Locked));
        }

        [TestCase(HouseStage.Unbuilt, GardenStage.Foundation)]
        [TestCase(HouseStage.Unbuilt, GardenStage.Framing)]
        [TestCase(HouseStage.Unbuilt, GardenStage.Complete)]
        [TestCase(HouseStage.Foundation, GardenStage.Foundation)]
        [TestCase(HouseStage.Foundation, GardenStage.Framing)]
        [TestCase(HouseStage.Foundation, GardenStage.Complete)]
        [TestCase(HouseStage.Framing, GardenStage.Foundation)]
        [TestCase(HouseStage.Framing, GardenStage.Framing)]
        [TestCase(HouseStage.Framing, GardenStage.Complete)]
        public void Load_GardenProgressBeforeHouseCompletion_RecoversFreshState(HouseStage houseStage, GardenStage gardenStage)
        {
            var invalid = GameStateData.Fresh(DateTime.UtcNow);
            invalid.houseStage = (int)houseStage;
            invalid.facilities[0].stage = (int)gardenStage;
            if (gardenStage == GardenStage.Complete)
            {
                invalid.gardenCompletedAtUnixSeconds = invalid.savedAtUnixSeconds;
            }

            var session = new GameSession(
                new InMemoryStorage { Value = GameStateSerializer.Serialize(invalid) },
                new ManualClock(DateTime.UtcNow),
                ProductionSettings.Default);

            session.Load();

            Assert.That(session.LastReason, Is.EqualTo(GameReason.RecoveredInvalidSave));
            Assert.That(session.State.HouseStage, Is.EqualTo(HouseStage.Unbuilt));
            Assert.That(session.State.GardenStage, Is.EqualTo(GardenStage.Locked));
        }

        [TestCase(GardenStage.Locked)]
        [TestCase(GardenStage.Foundation)]
        [TestCase(GardenStage.Framing)]
        public void Load_GardenCompletionTimeBeforeGardenCompletion_RecoversFreshState(GardenStage gardenStage)
        {
            var invalid = GameStateData.Fresh(DateTime.UtcNow);
            invalid.houseStage = (int)HouseStage.Complete;
            invalid.facilities[0].stage = (int)gardenStage;
            invalid.gardenCompletedAtUnixSeconds = invalid.savedAtUnixSeconds;

            var session = new GameSession(
                new InMemoryStorage { Value = GameStateSerializer.Serialize(invalid) },
                new ManualClock(DateTime.UtcNow),
                ProductionSettings.Default);

            session.Load();

            Assert.That(session.LastReason, Is.EqualTo(GameReason.RecoveredInvalidSave));
            Assert.That(session.State.GardenStage, Is.EqualTo(GardenStage.Locked));
        }

        [TestCase(0L)]
        [TestCase(long.MinValue)]
        public void Load_CompletedGardenWithMissingOrInvalidCompletionTime_RecoversFreshState(long completedAtUnixSeconds)
        {
            var invalid = CompletedGardenSave(DateTime.UtcNow);
            invalid.gardenCompletedAtUnixSeconds = completedAtUnixSeconds;

            var session = new GameSession(
                new InMemoryStorage { Value = GameStateSerializer.Serialize(invalid) },
                new ManualClock(DateTime.UtcNow),
                ProductionSettings.Default);

            session.Load();

            Assert.That(session.LastReason, Is.EqualTo(GameReason.RecoveredInvalidSave));
            Assert.That(session.State.GardenStage, Is.EqualTo(GardenStage.Locked));
        }

        [Test]
        public void Load_CompletedGardenWithFutureCompletionTime_RecoversFreshState()
        {
            var invalid = CompletedGardenSave(DateTime.UtcNow);
            invalid.gardenCompletedAtUnixSeconds = invalid.savedAtUnixSeconds + 1;

            var session = new GameSession(
                new InMemoryStorage { Value = GameStateSerializer.Serialize(invalid) },
                new ManualClock(DateTime.UtcNow),
                ProductionSettings.Default);

            session.Load();

            Assert.That(session.LastReason, Is.EqualTo(GameReason.RecoveredInvalidSave));
            Assert.That(session.State.GardenStage, Is.EqualTo(GardenStage.Locked));
        }

        [Test]
        public void Production_DoublesOnlyCloudCottonAndDewAfterGardenCompletion_WithoutRetroactiveBonusAfterFailedSave()
        {
            var storage = new InMemoryStorage();
            var clock = new ManualClock(new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            var session = new GameSession(storage, clock, ProductionSettings.Default);
            session.Load();
            session.AddResources(new ResourceAmounts(30, 30, 30, 30));
            Assert.That(session.TryBuildNextHouseStage(), Is.True);
            Assert.That(session.TryBuildNextHouseStage(), Is.True);
            Assert.That(session.TryBuildNextHouseStage(), Is.True);
            clock.UtcNow = clock.UtcNow.AddSeconds(61);
            storage.FailWrites = true;
            Assert.That(session.TryBuildNextGardenStage(), Is.True);
            Assert.That(session.TryBuildNextGardenStage(), Is.True);
            Assert.That(session.TryBuildNextGardenStage(), Is.True);
            Assert.That(session.LastReason, Is.EqualTo(GameReason.StorageUnavailable));

            clock.UtcNow = clock.UtcNow.AddSeconds(59);
            storage.FailWrites = false;
            session.AdvanceWhileOpen();

            // The completed cycle ending before garden completion is normal (1); the later cycle is boosted (2).
            Assert.That(session.State.Resources.Driftwood, Is.EqualTo(2));
            Assert.That(session.State.Resources.CloudCotton, Is.EqualTo(3));
            Assert.That(session.State.Resources.Dew, Is.EqualTo(3));
            Assert.That(session.State.Resources.Stardust, Is.EqualTo(2));
            Assert.That(session.LastReason, Is.EqualTo(GameReason.None));
        }

        [Test]
        public void Production_GardenCompletedOnProductionBoundary_AppliesBonusFromTheFollowingCompletedInterval()
        {
            var storage = new InMemoryStorage();
            var clock = new ManualClock(new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            var session = new GameSession(storage, clock, ProductionSettings.Default);
            session.Load();
            session.AddResources(new ResourceAmounts(30, 30, 30, 30));
            Assert.That(session.TryBuildNextHouseStage(), Is.True);
            Assert.That(session.TryBuildNextHouseStage(), Is.True);
            Assert.That(session.TryBuildNextHouseStage(), Is.True);

            clock.UtcNow = clock.UtcNow.AddSeconds(60);
            storage.FailWrites = true;
            Assert.That(session.TryBuildNextGardenStage(), Is.True);
            Assert.That(session.TryBuildNextGardenStage(), Is.True);
            Assert.That(session.TryBuildNextGardenStage(), Is.True);
            storage.FailWrites = false;
            session.AdvanceWhileOpen();

            Assert.That(session.State.Resources.Driftwood, Is.EqualTo(1));
            Assert.That(session.State.Resources.CloudCotton, Is.EqualTo(1));
            Assert.That(session.State.Resources.Dew, Is.EqualTo(1));
            Assert.That(session.State.Resources.Stardust, Is.EqualTo(1));

            clock.UtcNow = clock.UtcNow.AddSeconds(60);
            session.AdvanceWhileOpen();

            Assert.That(session.State.Resources.Driftwood, Is.EqualTo(2));
            Assert.That(session.State.Resources.CloudCotton, Is.EqualTo(3));
            Assert.That(session.State.Resources.Dew, Is.EqualTo(3));
            Assert.That(session.State.Resources.Stardust, Is.EqualTo(2));
        }

        [Test]
        public void Load_CompletedGarden_DoublesOnlyCloudCottonAndDewForOfflineCompletedIntervals()
        {
            var clock = new ManualClock(new DateTime(2030, 1, 1, 0, 2, 0, DateTimeKind.Utc));
            var saved = GameStateData.Fresh(clock.UtcNow.AddMinutes(-2));
            saved.houseStage = (int)HouseStage.Complete;
            saved.facilities[0].stage = (int)GardenStage.Complete;
            saved.gardenCompletedAtUnixSeconds = saved.savedAtUnixSeconds;
            var storage = new InMemoryStorage { Value = GameStateSerializer.Serialize(saved) };
            var session = new GameSession(storage, clock, ProductionSettings.Default);

            session.Load();

            Assert.That(session.State.Resources.Driftwood, Is.EqualTo(2));
            Assert.That(session.State.Resources.CloudCotton, Is.EqualTo(4));
            Assert.That(session.State.Resources.Dew, Is.EqualTo(4));
            Assert.That(session.State.Resources.Stardust, Is.EqualTo(2));
        }

        [Test]
        public void Load_UnfinishedGarden_DoesNotChangeOfflineProduction()
        {
            var clock = new ManualClock(new DateTime(2030, 1, 1, 0, 2, 0, DateTimeKind.Utc));
            var saved = GameStateData.Fresh(clock.UtcNow.AddMinutes(-2));
            saved.houseStage = (int)HouseStage.Complete;
            saved.facilities[0].stage = (int)GardenStage.Framing;
            var storage = new InMemoryStorage { Value = GameStateSerializer.Serialize(saved) };
            var session = new GameSession(storage, clock, ProductionSettings.Default);

            session.Load();

            Assert.That(session.State.Resources, Is.EqualTo(new ResourceAmounts(2, 2, 2, 2)));
        }

        [Test]
        public void Load_LegacyFoundationSave_RemainsValidAndCanAdvanceToFraming()
        {
            var clock = new ManualClock(new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            var legacyFoundationSave = GameStateData.Fresh(clock.UtcNow);
            legacyFoundationSave.houseStage = (int)HouseStage.Foundation;
            legacyFoundationSave.driftwood = 5;
            legacyFoundationSave.cloudCotton = 5;
            legacyFoundationSave.dew = 5;
            legacyFoundationSave.stardust = 5;
            var storage = new InMemoryStorage { Value = GameStateSerializer.Serialize(legacyFoundationSave) };
            var session = new GameSession(storage, clock, ProductionSettings.Default);

            session.Load();

            Assert.That(session.LastReason, Is.EqualTo(GameReason.None));
            Assert.That(session.State.HouseStage, Is.EqualTo(HouseStage.Foundation));
            Assert.That(session.TryBuildNextHouseStage(), Is.True);
            Assert.That(session.State.HouseStage, Is.EqualTo(HouseStage.Framing));
            Assert.That(session.State.Resources, Is.EqualTo(ResourceAmounts.Zero));
        }

        [TestCase("{not-json}")]
        [TestCase("{\"version\":1,\"savedAtUnixSeconds\":1,\"driftwood\":-1,\"cloudCotton\":0,\"dew\":0,\"stardust\":0,\"houseStage\":0}")]
        [TestCase("{\"version\":1,\"savedAtUnixSeconds\":1,\"driftwood\":0,\"cloudCotton\":0,\"dew\":0,\"stardust\":0,\"houseStage\":99}")]
        public void Load_ResetsInvalidSavedData_WithRecoveryReason(string invalidData)
        {
            var storage = new InMemoryStorage { Value = invalidData };
            var clock = new ManualClock(DateTime.UtcNow);
            var session = new GameSession(storage, clock, ProductionSettings.Default);

            session.Load();

            Assert.That(session.State.Resources, Is.EqualTo(ResourceAmounts.Zero));
            Assert.That(session.State.HouseStage, Is.EqualTo(HouseStage.Unbuilt));
            Assert.That(session.LastReason, Is.EqualTo(GameReason.RecoveredInvalidSave));
        }

        [TestCase("driftwood")]
        [TestCase("cloudCotton")]
        [TestCase("dew")]
        [TestCase("stardust")]
        public void Load_MissingRequiredResourceField_RecoversInvalidSave(string missingField)
        {
            var storage = new InMemoryStorage
            {
                Value = "{\"version\":1,\"savedAtUnixSeconds\":1,"
                    + ResourceFieldsExcept(missingField)
                    + "\"houseStage\":0}"
            };
            var session = new GameSession(storage, new ManualClock(DateTime.UtcNow), ProductionSettings.Default);

            session.Load();

            Assert.That(session.State.Resources, Is.EqualTo(ResourceAmounts.Zero));
            Assert.That(session.State.HouseStage, Is.EqualTo(HouseStage.Unbuilt));
            Assert.That(session.LastReason, Is.EqualTo(GameReason.RecoveredInvalidSave));
        }

        [Test]
        public void Load_NonNumericRequiredResourceField_RecoversInvalidSave()
        {
            var storage = new InMemoryStorage
            {
                Value = "{\"version\":1,\"savedAtUnixSeconds\":1,\"driftwood\":\"0\",\"cloudCotton\":0,\"dew\":0,\"stardust\":0,\"houseStage\":0}"
            };
            var session = new GameSession(storage, new ManualClock(DateTime.UtcNow), ProductionSettings.Default);

            session.Load();

            Assert.That(session.LastReason, Is.EqualTo(GameReason.RecoveredInvalidSave));
        }

        [Test]
        public void StorageWriteFailure_DoesNotStopPlay_AndExposesNonSavingReason()
        {
            var storage = new InMemoryStorage { FailWrites = true };
            var clock = new ManualClock(DateTime.UtcNow);
            var session = new GameSession(storage, clock, ProductionSettings.Default);
            session.Load();

            session.AddResources(new ResourceAmounts(1, 1, 1, 1));

            Assert.That(session.State.Resources.Driftwood, Is.EqualTo(1));
            Assert.That(session.LastReason, Is.EqualTo(GameReason.StorageUnavailable));
        }

        [Test]
        public void SaveCurrentProgress_UpdatesTimestampOnlyAfterASuccessfulWrite()
        {
            var storage = new InMemoryStorage();
            var clock = new ManualClock(new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            var session = new GameSession(storage, clock, ProductionSettings.Default);
            session.Load();
            var lastSuccessfulTimestamp = SavedTimestamp(storage);

            clock.UtcNow = clock.UtcNow.AddMinutes(5);
            storage.FailWrites = true;
            session.SaveCurrentProgress();

            Assert.That(SavedTimestamp(storage), Is.EqualTo(lastSuccessfulTimestamp));
            Assert.That(session.LastReason, Is.EqualTo(GameReason.StorageUnavailable));

            storage.FailWrites = false;
            session.SaveCurrentProgress();

            Assert.That(SavedTimestamp(storage), Is.EqualTo(GameStateData.ToUnixSeconds(clock.UtcNow)));
            Assert.That(session.LastReason, Is.EqualTo(GameReason.None));
        }

        [Test]
        public void Load_BackwardClock_GrantsNothing_AndMovesSavedTimestampToNow()
        {
            var storage = new InMemoryStorage();
            var clock = new ManualClock(new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            storage.Value = GameStateSerializer.Serialize(GameStateData.Fresh(clock.UtcNow.AddMinutes(10)));
            var session = new GameSession(storage, clock, ProductionSettings.Default);

            session.Load();

            Assert.That(session.State.Resources, Is.EqualTo(ResourceAmounts.Zero));
            Assert.That(session.LastReason, Is.EqualTo(GameReason.ClockMovedBackward));
            Assert.That(GameStateSerializer.TryDeserialize(storage.Value, out var saved), Is.True);
            Assert.That(saved.savedAtUnixSeconds, Is.EqualTo(GameStateData.ToUnixSeconds(clock.UtcNow)));
        }

        [Test]
        public void Load_DiscardsPartialInterval_InsteadOfCarryingItForward()
        {
            var storage = new InMemoryStorage();
            var clock = new ManualClock(new DateTime(2030, 1, 1, 0, 1, 59, DateTimeKind.Utc));
            storage.Value = GameStateSerializer.Serialize(GameStateData.Fresh(clock.UtcNow.AddSeconds(-119)));
            var session = new GameSession(storage, clock, new ProductionSettings(60, 1, HouseFoundationCost.Zero));

            session.Load();
            clock.UtcNow = clock.UtcNow.AddSeconds(1);
            session.AdvanceWhileOpen();

            Assert.That(session.State.Resources.Driftwood, Is.EqualTo(1));
        }

        [Test]
        public void OpenGameProductionController_AdvancesAtConfiguredInterval_AndPersists()
        {
            var storage = new InMemoryStorage();
            var clock = new ManualClock(new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            var session = new GameSession(storage, clock, new ProductionSettings(60, 3, HouseFoundationCost.Zero));
            session.Load();
            var controller = new OpenGameProductionController(session, clock);
            var writesAfterLoad = storage.WriteCount;

            clock.UtcNow = clock.UtcNow.AddSeconds(59);
            Assert.That(controller.Tick(), Is.False);
            clock.UtcNow = clock.UtcNow.AddSeconds(1);

            Assert.That(controller.Tick(), Is.True);
            Assert.That(session.State.Resources.Driftwood, Is.EqualTo(3));
            Assert.That(storage.WriteCount, Is.EqualTo(writesAfterLoad + 1));
        }

        [Test]
        public void OpenGameProductionController_ReportsWholeSecondsUntilTheNextCycle()
        {
            var storage = new InMemoryStorage();
            var clock = new ManualClock(new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            var session = new GameSession(storage, clock, ProductionSettings.Default);
            session.Load();
            var controller = new OpenGameProductionController(session, clock);

            Assert.That(controller.SecondsUntilNextProduction, Is.EqualTo(60));

            clock.UtcNow = clock.UtcNow.AddMilliseconds(200);
            Assert.That(controller.SecondsUntilNextProduction, Is.EqualTo(60));

            clock.UtcNow = clock.UtcNow.AddSeconds(59);
            Assert.That(controller.SecondsUntilNextProduction, Is.EqualTo(1));

            clock.UtcNow = clock.UtcNow.AddSeconds(1);
            Assert.That(controller.SecondsUntilNextProduction, Is.EqualTo(0));
        }

        [Test]
        public void BrowserStorage_ReadFailure_IsNotTreatedAsAnAbsentKey()
        {
            var browserStorage = new BrowserLocalStorage("state", new FailingBrowserBridge());
            var clock = new ManualClock(DateTime.UtcNow);
            var session = new GameSession(browserStorage, clock, ProductionSettings.Default);

            session.Load();

            Assert.That(session.LastReason, Is.EqualTo(GameReason.StorageUnavailable));
        }

        [Test]
        public void RuntimeProductionBehaviour_TicksConfiguredProduction_AndPersists()
        {
            var storage = new InMemoryStorage();
            var clock = new ManualClock(new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            var gameObject = new GameObject("production-runtime-test");
            try
            {
                var runtime = gameObject.AddComponent<OpenGameProductionRuntime>();
                runtime.Initialize(storage, clock, new ProductionSettings(30, 2, HouseFoundationCost.Zero));
                var writesAfterLoad = storage.WriteCount;
                clock.UtcNow = clock.UtcNow.AddSeconds(30);

                Assert.That(runtime.TickNow(), Is.True);
                Assert.That(runtime.Session.State.Resources.Driftwood, Is.EqualTo(2));
                Assert.That(storage.WriteCount, Is.EqualTo(writesAfterLoad + 1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void RuntimeLifecycle_InactiveAndQuitRetrySaving_WithoutChangingProgress()
        {
            var storage = new InMemoryStorage();
            var clock = new ManualClock(new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            var gameObject = new GameObject("production-runtime-lifecycle-test");
            try
            {
                var runtime = gameObject.AddComponent<OpenGameProductionRuntime>();
                runtime.Initialize(storage, clock, new ProductionSettings(60, 1, new HouseFoundationCost(1, 1, 1, 1)));
                runtime.Session.AddResources(new ResourceAmounts(1, 1, 1, 1));
                Assert.That(runtime.Session.TryBuildHouseFoundation(), Is.True);
                var stateBeforeSaving = runtime.Session.State;
                var writesBeforeSaving = storage.WriteCount;

                clock.UtcNow = clock.UtcNow.AddMinutes(1);
                InvokeRuntimeSignal(runtime, "OnApplicationPause", true);
                Assert.That(SavedTimestamp(storage), Is.EqualTo(GameStateData.ToUnixSeconds(clock.UtcNow)));

                InvokeRuntimeSignal(runtime, "OnApplicationFocus", true);
                Assert.That(storage.WriteCount, Is.EqualTo(writesBeforeSaving + 1));

                clock.UtcNow = clock.UtcNow.AddMinutes(1);
                InvokeRuntimeSignal(runtime, "OnApplicationFocus", false);
                clock.UtcNow = clock.UtcNow.AddMinutes(1);
                InvokeRuntimeSignal(runtime, "OnApplicationQuit");

                Assert.That(storage.WriteCount, Is.EqualTo(writesBeforeSaving + 3));
                Assert.That(runtime.Session.State.Resources, Is.EqualTo(stateBeforeSaving.Resources));
                Assert.That(runtime.Session.State.HouseStage, Is.EqualTo(stateBeforeSaving.HouseStage));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        private static long SavedTimestamp(InMemoryStorage storage)
        {
            Assert.That(GameStateSerializer.TryDeserialize(storage.Value, out var saved), Is.True);
            return saved.savedAtUnixSeconds;
        }

        private static GameStateData CompletedGardenSave(DateTime savedAt)
        {
            var state = GameStateData.Fresh(savedAt);
            state.houseStage = (int)HouseStage.Complete;
            state.facilities[0].stage = (int)GardenStage.Complete;
            return state;
        }

        private static void InvokeRuntimeSignal(OpenGameProductionRuntime runtime, string methodName, params object[] arguments)
        {
            var method = typeof(OpenGameProductionRuntime).GetMethod(
                methodName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(runtime, arguments);
        }

        private sealed class ManualClock : IClock
        {
            public ManualClock(DateTime utcNow) { UtcNow = utcNow; }
            public DateTime UtcNow { get; set; }
        }

        private static string ResourceFieldsExcept(string missingField)
        {
            var fields = new[] { "driftwood", "cloudCotton", "dew", "stardust" };
            var result = string.Empty;
            foreach (var field in fields)
            {
                if (field != missingField) result += string.Format("\"{0}\":0,", field);
            }

            return result;
        }

        private sealed class InMemoryStorage : IStateStorage
        {
            public string Value;
            public bool FailWrites;
            public int WriteCount;
            public bool TryRead(out string value, out string reason) { value = Value; reason = null; return true; }
            public bool TryWrite(string value, out string reason)
            {
                if (FailWrites) { reason = "write failed"; return false; }
                Value = value; WriteCount++; reason = null; return true;
            }
        }

        private sealed class FailingBrowserBridge : IBrowserLocalStorageBridge
        {
            public bool TryRead(string key, out string value, out string reason)
            {
                value = null;
                reason = "browser privacy mode blocked local storage";
                return false;
            }

            public bool TryWrite(string key, string value, out string reason)
            {
                reason = "browser privacy mode blocked local storage";
                return false;
            }
        }
    }
}
