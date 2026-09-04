using System;
using System.Collections.Generic;

namespace CloudWhale.Game.Presentation
{
    /// <summary>
    /// Read-only presentation boundary around GameSession. Only GameSession changes persisted state.
    /// </summary>
    public sealed class IslandPresentationController
    {
        private readonly GameSession session;
        private readonly HouseFoundationCost stageCost;

        public IslandPresentationController(GameSession session, HouseFoundationCost stageCost)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.stageCost = stageCost;
            Refresh();
        }

        public IslandPresentationView View { get; private set; }

        public void Refresh()
        {
            var state = session.State;
            View = CreateView(state, StatusMessage(session.LastReason, state.Resources));
        }

        public void BuildNextHouseStage()
        {
            session.TryBuildNextHouseStage();
            var state = session.State;
            View = CreateView(state, StatusMessage(session.LastReason, state.Resources));
        }

        public void BuildNextGardenStage()
        {
            session.TryBuildNextGardenStage();
            var state = session.State;
            View = CreateView(state, StatusMessage(session.LastReason, state.Resources));
        }

        private IslandPresentationView CreateView(GameStateSnapshot state, string statusMessage)
        {
            return new IslandPresentationView(
                state.Resources,
                state.HouseStage,
                AppearanceFor(state.HouseStage),
                state.HouseStage != HouseStage.Complete,
                NextAction(state.HouseStage),
                state.GardenStage,
                GardenAppearanceFor(state.GardenStage),
                state.HouseStage == HouseStage.Complete && state.GardenStage != GardenStage.Complete,
                GardenNextAction(state),
                statusMessage);
        }

        private static IslandHouseAppearance AppearanceFor(HouseStage stage)
        {
            switch (stage)
            {
                case HouseStage.Foundation: return IslandHouseAppearance.Foundation;
                case HouseStage.Framing: return IslandHouseAppearance.Framing;
                case HouseStage.Complete: return IslandHouseAppearance.Complete;
                default: return IslandHouseAppearance.Unbuilt;
            }
        }

        private string NextAction(HouseStage stage)
        {
            switch (stage)
            {
                case HouseStage.Foundation: return "Next: build the house framing (" + CostText() + ").";
                case HouseStage.Framing: return "Next: complete the house (" + CostText() + ").";
                case HouseStage.Complete: return "House complete — enjoy your calm island.";
                default: return "Next: build the house foundation (" + CostText() + ").";
            }
        }

        private static IslandGardenAppearance GardenAppearanceFor(GardenStage stage)
        {
            switch (stage)
            {
                case GardenStage.Foundation: return IslandGardenAppearance.Foundation;
                case GardenStage.Framing: return IslandGardenAppearance.Framing;
                case GardenStage.Complete: return IslandGardenAppearance.Complete;
                default: return IslandGardenAppearance.Locked;
            }
        }

        private string GardenNextAction(GameStateSnapshot state)
        {
            if (state.HouseStage != HouseStage.Complete)
            {
                return "Garden locked — complete the house first.";
            }

            switch (state.GardenStage)
            {
                case GardenStage.Foundation: return "Next: build the garden framing (" + GardenCostText() + ").";
                case GardenStage.Framing: return "Next: complete the garden (" + GardenCostText() + ").";
                case GardenStage.Complete: return "Garden complete — Cloud Cotton and Dew now grow faster.";
                default: return "Next: build the garden foundation (" + GardenCostText() + ").";
            }
        }

        private string CostText()
        {
            var cost = stageCost.Resources;
            return $"Driftwood {cost.Driftwood}, Cloud Cotton {cost.CloudCotton}, Dew {cost.Dew}, Stardust {cost.Stardust}";
        }

        private string GardenCostText()
        {
            var cost = session.GardenStageCost.Resources;
            return $"Driftwood {cost.Driftwood}, Cloud Cotton {cost.CloudCotton}, Dew {cost.Dew}, Stardust {cost.Stardust}";
        }

        private string FailureMessage(GameReason reason, ResourceAmounts resources)
        {
            if (reason == GameReason.InsufficientResources)
            {
                var missing = MissingResources(resources);
                return "Not enough resources yet. Needed: " + string.Join(", ", missing) + ". Nothing was spent — there is no penalty.";
            }

            if (reason == GameReason.HouseAlreadyComplete) return "The house is already complete and resting safely on the island.";
            if (reason == GameReason.HouseMustBeComplete) return "Garden locked — complete the house first.";
            if (reason == GameReason.GardenAlreadyComplete) return "The garden is already complete and blooming safely on the island.";
            return SaveMessage(reason);
        }

        private string StatusMessage(GameReason reason, ResourceAmounts resources)
        {
            if (reason == GameReason.InsufficientResources || reason == GameReason.HouseAlreadyComplete || reason == GameReason.HouseMustBeComplete || reason == GameReason.GardenAlreadyComplete)
            {
                return FailureMessage(reason, resources);
            }

            return SaveMessage(reason);
        }

        private IEnumerable<string> MissingResources(ResourceAmounts resources)
        {
            var cost = stageCost.Resources;
            if (resources.Driftwood < cost.Driftwood) yield return "Driftwood " + (cost.Driftwood - resources.Driftwood);
            if (resources.CloudCotton < cost.CloudCotton) yield return "Cloud Cotton " + (cost.CloudCotton - resources.CloudCotton);
            if (resources.Dew < cost.Dew) yield return "Dew " + (cost.Dew - resources.Dew);
            if (resources.Stardust < cost.Stardust) yield return "Stardust " + (cost.Stardust - resources.Stardust);
        }

        private static string SaveMessage(GameReason reason)
        {
            switch (reason)
            {
                case GameReason.StorageUnavailable: return "Playing safely in this tab, but the browser could not save progress.";
                case GameReason.RecoveredInvalidSave: return "A damaged save was safely replaced with a fresh island.";
                case GameReason.ClockMovedBackward: return "The clock changed; progress was kept safe without extra rewards.";
                default: return "Progress saved in this browser.";
            }
        }
    }

    public enum IslandHouseAppearance
    {
        Unbuilt,
        Foundation,
        Framing,
        Complete,
    }

    public enum IslandGardenAppearance
    {
        Locked,
        Foundation,
        Framing,
        Complete,
    }

    public readonly struct IslandPresentationView
    {
        public IslandPresentationView(ResourceAmounts resources, HouseStage houseStage, IslandHouseAppearance houseAppearance, bool canBuildNextHouseStage, string nextAction, GardenStage gardenStage, IslandGardenAppearance gardenAppearance, bool canBuildNextGardenStage, string gardenNextAction, string statusMessage)
        {
            Resources = resources;
            HouseStage = houseStage;
            HouseAppearance = houseAppearance;
            CanBuildNextHouseStage = canBuildNextHouseStage;
            NextAction = nextAction;
            GardenStage = gardenStage;
            GardenAppearance = gardenAppearance;
            CanBuildNextGardenStage = canBuildNextGardenStage;
            GardenNextAction = gardenNextAction;
            StatusMessage = statusMessage;
        }

        public ResourceAmounts Resources { get; }
        public HouseStage HouseStage { get; }
        public IslandHouseAppearance HouseAppearance { get; }
        public bool CanBuildNextHouseStage { get; }
        public string NextAction { get; }
        public GardenStage GardenStage { get; }
        public IslandGardenAppearance GardenAppearance { get; }
        public bool CanBuildNextGardenStage { get; }
        public string GardenNextAction { get; }
        public string StatusMessage { get; }
    }
}
