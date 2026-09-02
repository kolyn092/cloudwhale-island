using System;
using CloudWhale.Game;
using CloudWhale.Game.Presentation;
using NUnit.Framework;

namespace CloudWhale.Tests
{
    public sealed class IslandPresentationControllerTests
    {
        [Test]
        public void Build_UsesGameSessionAndShowsFoundationImmediately_WhenResourcesAreEnough()
        {
            var session = CreateSession(new HouseFoundationCost(2, 3, 4, 5));
            session.AddResources(new ResourceAmounts(2, 3, 4, 5));
            var presentation = new IslandPresentationController(session, new HouseFoundationCost(2, 3, 4, 5));

            presentation.BuildFoundation();

            Assert.That(presentation.View.HouseStage, Is.EqualTo(HouseStage.Foundation));
            Assert.That(presentation.View.StatusMessage, Does.Contain("saved"));
            Assert.That(session.State.Resources, Is.EqualTo(ResourceAmounts.Zero));
        }

        [Test]
        public void Build_RefusesWithoutMutatingAndNamesEveryMissingResource_WhenResourcesAreShort()
        {
            var cost = new HouseFoundationCost(2, 3, 4, 5);
            var session = CreateSession(cost);
            session.AddResources(new ResourceAmounts(1, 3, 0, 4));
            var presentation = new IslandPresentationController(session, cost);
            var before = session.State;

            presentation.BuildFoundation();

            Assert.That(presentation.View.HouseStage, Is.EqualTo(HouseStage.Unbuilt));
            Assert.That(session.State.Resources, Is.EqualTo(before.Resources));
            Assert.That(presentation.View.StatusMessage, Does.Contain("Driftwood 1"));
            Assert.That(presentation.View.StatusMessage, Does.Contain("Dew 4"));
            Assert.That(presentation.View.StatusMessage, Does.Contain("Stardust 1"));
        }

        private static GameSession CreateSession(HouseFoundationCost cost)
        {
            var session = new GameSession(new MemoryStorage(), new FixedClock(), new ProductionSettings(60, 1, cost));
            session.Load();
            return session;
        }

        private sealed class FixedClock : IClock { public DateTime UtcNow => new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc); }
        private sealed class MemoryStorage : IStateStorage
        {
            public bool TryRead(out string value, out string reason) { value = null; reason = null; return true; }
            public bool TryWrite(string value, out string reason) { reason = null; return true; }
        }
    }
}
