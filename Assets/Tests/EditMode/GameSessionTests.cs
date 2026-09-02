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

        private sealed class ManualClock : IClock
        {
            public ManualClock(DateTime utcNow) { UtcNow = utcNow; }
            public DateTime UtcNow { get; set; }
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
