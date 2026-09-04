using System;
using CloudWhale.Game;
using CloudWhale.Game.Presentation;
using NUnit.Framework;

namespace CloudWhale.Tests
{
    public sealed class IslandPresentationControllerTests
    {
        [Test]
        public void BuildNextStage_UsesGameSessionAndShowsTheNextStageImmediately_WhenResourcesAreEnough()
        {
            var session = CreateSession(new HouseFoundationCost(2, 3, 4, 5));
            session.AddResources(new ResourceAmounts(2, 3, 4, 5));
            var presentation = new IslandPresentationController(session, new HouseFoundationCost(2, 3, 4, 5));

            presentation.BuildNextHouseStage();

            Assert.That(presentation.View.HouseStage, Is.EqualTo(HouseStage.Foundation));
            Assert.That(presentation.View.HouseAppearance, Is.EqualTo(IslandHouseAppearance.Foundation));
            Assert.That(presentation.View.CanBuildNextHouseStage, Is.True);
            Assert.That(presentation.View.StatusMessage, Does.Contain("saved"));
            Assert.That(session.State.Resources, Is.EqualTo(ResourceAmounts.Zero));
        }

        [Test]
        public void Build_RefusesWithoutMutatingAndKeepsEveryMissingResourceMessageAfterRefresh_WhenResourcesAreShort()
        {
            var cost = new HouseFoundationCost(2, 3, 4, 5);
            var session = CreateSession(cost);
            session.AddResources(new ResourceAmounts(1, 3, 0, 4));
            var presentation = new IslandPresentationController(session, cost);
            var before = session.State;

            presentation.BuildNextHouseStage();
            presentation.Refresh();

            Assert.That(presentation.View.HouseStage, Is.EqualTo(HouseStage.Unbuilt));
            Assert.That(session.State.Resources, Is.EqualTo(before.Resources));
            Assert.That(presentation.View.StatusMessage, Does.Contain("Driftwood 1"));
            Assert.That(presentation.View.StatusMessage, Does.Contain("Dew 4"));
            Assert.That(presentation.View.StatusMessage, Does.Contain("Stardust 1"));
        }

        [Test]
        public void SessionStageCost_IsTheSameCostShownForEveryIncompleteStage()
        {
            var configuredCost = new HouseFoundationCost(2, 3, 4, 5);
            var session = CreateSession(configuredCost);
            var presentation = new IslandPresentationController(session, session.HouseFoundationCost);

            AssertBuildActionShows(configuredCost, presentation, "foundation");

            session.AddResources(configuredCost.Resources);
            presentation.BuildNextHouseStage();
            AssertBuildActionShows(configuredCost, presentation, "framing");

            session.AddResources(configuredCost.Resources);
            presentation.BuildNextHouseStage();
            AssertBuildActionShows(configuredCost, presentation, "complete the house");
        }

        [Test]
        public void BuildNextStage_ChangesEveryStageAppearanceAndHidesTheActionAfterCompletion()
        {
            var cost = new HouseFoundationCost(5, 5, 5, 5);
            var session = CreateSession(cost);
            session.AddResources(new ResourceAmounts(15, 15, 15, 15));
            var presentation = new IslandPresentationController(session, cost);

            Assert.That(presentation.View.HouseAppearance, Is.EqualTo(IslandHouseAppearance.Unbuilt));
            presentation.BuildNextHouseStage();
            Assert.That(presentation.View.HouseAppearance, Is.EqualTo(IslandHouseAppearance.Foundation));
            presentation.BuildNextHouseStage();
            Assert.That(presentation.View.HouseAppearance, Is.EqualTo(IslandHouseAppearance.Framing));
            presentation.BuildNextHouseStage();

            Assert.That(presentation.View.HouseStage, Is.EqualTo(HouseStage.Complete));
            Assert.That(presentation.View.HouseAppearance, Is.EqualTo(IslandHouseAppearance.Complete));
            Assert.That(presentation.View.CanBuildNextHouseStage, Is.False);
            Assert.That(presentation.View.NextAction, Does.Contain("complete"));
        }

        [Test]
        public void BuildNextStage_KeepsCompletedHouseRejectionMessageAfterRefresh_WithoutChangingItsAppearance()
        {
            var cost = new HouseFoundationCost(5, 5, 5, 5);
            var session = CreateSession(cost);
            session.AddResources(new ResourceAmounts(15, 15, 15, 15));
            var presentation = new IslandPresentationController(session, cost);
            presentation.BuildNextHouseStage();
            presentation.BuildNextHouseStage();
            presentation.BuildNextHouseStage();

            presentation.BuildNextHouseStage();
            presentation.Refresh();

            Assert.That(presentation.View.HouseAppearance, Is.EqualTo(IslandHouseAppearance.Complete));
            Assert.That(presentation.View.StatusMessage, Does.Contain("complete"));
        }

        [Test]
        public void Garden_RemainsLockedWithVacantLotGuidance_UntilTheHouseIsComplete()
        {
            var session = CreateSession(new HouseFoundationCost(5, 5, 5, 5));
            var presentation = new IslandPresentationController(session, session.HouseFoundationCost);

            Assert.That(presentation.View.GardenAppearance, Is.EqualTo(IslandGardenAppearance.Locked));
            Assert.That(presentation.View.CanBuildNextGardenStage, Is.False);
            Assert.That(presentation.View.GardenNextAction, Does.Contain("complete the house"));

            presentation.BuildNextGardenStage();
            presentation.Refresh();

            Assert.That(presentation.View.GardenAppearance, Is.EqualTo(IslandGardenAppearance.Locked));
            Assert.That(presentation.View.StatusMessage, Does.Contain("complete the house"));
        }

        [Test]
        public void Garden_ShowsConfiguredCostAndMissingResourcesWithoutChangingTheGarden()
        {
            var cost = new HouseFoundationCost(5, 5, 5, 5);
            var session = CreateSession(cost);
            session.AddResources(new ResourceAmounts(15, 15, 15, 15));
            var presentation = new IslandPresentationController(session, cost);
            presentation.BuildNextHouseStage();
            presentation.BuildNextHouseStage();
            presentation.BuildNextHouseStage();

            Assert.That(presentation.View.CanBuildNextGardenStage, Is.True);
            Assert.That(presentation.View.GardenNextAction, Does.Contain("garden foundation"));
            Assert.That(presentation.View.GardenNextAction, Does.Contain("Driftwood 5"));
            Assert.That(presentation.View.GardenNextAction, Does.Contain("Cloud Cotton 5"));
            Assert.That(presentation.View.GardenNextAction, Does.Contain("Dew 5"));
            Assert.That(presentation.View.GardenNextAction, Does.Contain("Stardust 5"));

            presentation.BuildNextGardenStage();
            presentation.Refresh();

            Assert.That(presentation.View.GardenStage, Is.EqualTo(GardenStage.Locked));
            Assert.That(presentation.View.StatusMessage, Does.Contain("Not enough resources"));
        }

        [Test]
        public void BuildNextGardenStage_ImmediatelyChangesAllFourAppearancesAndHidesActionAfterCompletion()
        {
            var cost = new HouseFoundationCost(5, 5, 5, 5);
            var session = CreateSession(cost);
            session.AddResources(new ResourceAmounts(30, 30, 30, 30));
            var presentation = new IslandPresentationController(session, cost);
            presentation.BuildNextHouseStage();
            presentation.BuildNextHouseStage();
            presentation.BuildNextHouseStage();

            Assert.That(presentation.View.GardenAppearance, Is.EqualTo(IslandGardenAppearance.Locked));
            presentation.BuildNextGardenStage();
            Assert.That(presentation.View.GardenAppearance, Is.EqualTo(IslandGardenAppearance.Foundation));
            presentation.BuildNextGardenStage();
            Assert.That(presentation.View.GardenAppearance, Is.EqualTo(IslandGardenAppearance.Framing));
            presentation.BuildNextGardenStage();

            Assert.That(presentation.View.GardenStage, Is.EqualTo(GardenStage.Complete));
            Assert.That(presentation.View.GardenAppearance, Is.EqualTo(IslandGardenAppearance.Complete));
            Assert.That(presentation.View.CanBuildNextGardenStage, Is.False);
            Assert.That(presentation.View.GardenNextAction, Does.Contain("complete"));

            presentation.BuildNextGardenStage();
            presentation.Refresh();
            Assert.That(presentation.View.StatusMessage, Does.Contain("garden is already complete"));
        }

        private static void AssertBuildActionShows(HouseFoundationCost cost, IslandPresentationController presentation, string expectedStage)
        {
            Assert.That(presentation.View.CanBuildNextHouseStage, Is.True);
            Assert.That(presentation.View.NextAction, Does.Contain(expectedStage));
            Assert.That(presentation.View.NextAction, Does.Contain("Driftwood " + cost.Resources.Driftwood));
            Assert.That(presentation.View.NextAction, Does.Contain("Cloud Cotton " + cost.Resources.CloudCotton));
            Assert.That(presentation.View.NextAction, Does.Contain("Dew " + cost.Resources.Dew));
            Assert.That(presentation.View.NextAction, Does.Contain("Stardust " + cost.Resources.Stardust));
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
