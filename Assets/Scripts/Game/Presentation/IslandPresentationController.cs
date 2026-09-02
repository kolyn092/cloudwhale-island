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
        private readonly HouseFoundationCost foundationCost;

        public IslandPresentationController(GameSession session, HouseFoundationCost foundationCost)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.foundationCost = foundationCost;
            Refresh();
        }

        public IslandPresentationView View { get; private set; }

        public void Refresh()
        {
            var state = session.State;
            View = new IslandPresentationView(state.Resources, state.HouseStage, NextAction(state.HouseStage), SaveMessage(session.LastReason));
        }

        public void BuildFoundation()
        {
            // This is deliberately the only build call: cost checking, persistence, and stage mutation remain in T2.
            var built = session.TryBuildHouseFoundation();
            var state = session.State;
            var message = built ? SaveMessage(session.LastReason) : FailureMessage(session.LastReason, state.Resources);
            View = new IslandPresentationView(state.Resources, state.HouseStage, NextAction(state.HouseStage), message);
        }

        private string NextAction(HouseStage stage) => stage == HouseStage.Foundation
            ? "House foundation complete — enjoy your calm island."
            : "Next: build the house foundation (" + CostText() + ").";

        private string CostText()
        {
            var cost = foundationCost.Resources;
            return $"Driftwood {cost.Driftwood}, Cloud Cotton {cost.CloudCotton}, Dew {cost.Dew}, Stardust {cost.Stardust}";
        }

        private string FailureMessage(GameReason reason, ResourceAmounts resources)
        {
            if (reason == GameReason.InsufficientResources)
            {
                var missing = MissingResources(resources);
                return "Not enough resources yet. Needed: " + string.Join(", ", missing) + ". Nothing was spent — there is no penalty.";
            }

            if (reason == GameReason.HouseAlreadyHasFoundation) return "The house foundation is already resting safely on the island.";
            return SaveMessage(reason);
        }

        private IEnumerable<string> MissingResources(ResourceAmounts resources)
        {
            var cost = foundationCost.Resources;
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

    public readonly struct IslandPresentationView
    {
        public IslandPresentationView(ResourceAmounts resources, HouseStage houseStage, string nextAction, string statusMessage)
        {
            Resources = resources;
            HouseStage = houseStage;
            NextAction = nextAction;
            StatusMessage = statusMessage;
        }

        public ResourceAmounts Resources { get; }
        public HouseStage HouseStage { get; }
        public string NextAction { get; }
        public string StatusMessage { get; }
    }
}
