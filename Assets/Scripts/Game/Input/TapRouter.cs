using UnityEngine;
using NodeWar.UI;

namespace NodeWar.Input
{
    /// <summary>
    /// Decides what a tap means, once, in one place.
    ///
    /// Before this, SelectionSystem, CommandSystem and NodePanelManager each
    /// raycast the same press and inferred the others' intent from the state
    /// they could see -- NodePanelManager raycast the villager layer purely to
    /// work out whether SelectionSystem was about to claim the click. The order
    /// those three ran in was the real arbitration, and it was implicit.
    ///
    /// The ladder below is that arbitration made explicit and evaluated once.
    /// It is deliberately short: every rung is a whole behaviour, and a tap can
    /// only ever match one.
    /// </summary>
    public class TapRouter : MonoBehaviour
    {
        [SerializeField] private bool verboseLogging = false;

        private PointerGestureSource source;
        private SelectionSystem selection;
        private CommandSystem commands;
        private NodePanelManager panel;

        public void Initialize(PointerGestureSource gestureSource,
                               SelectionSystem selectionSystem,
                               CommandSystem commandSystem,
                               NodePanelManager panelManager)
        {
            Unsubscribe();

            source = gestureSource;
            selection = selectionSystem;
            commands = commandSystem;
            panel = panelManager;

            Subscribe();
        }

        private void OnEnable() => Subscribe();
        private void OnDisable() => Unsubscribe();
        private void OnDestroy() => Unsubscribe();

        private bool subscribed;

        private void Subscribe()
        {
            if (source == null || subscribed) return;
            source.OnTap += HandleTap;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (source == null || !subscribed) return;
            source.OnTap -= HandleTap;
            subscribed = false;
        }

        /// <summary>
        /// The ladder. A tap always replaces the current selection -- there is
        /// no path here that adds to it, which is what keeps tapping a second
        /// villager from silently growing a group.
        /// </summary>
        private void HandleTap(GestureTarget target)
        {
            switch (target.kind)
            {
                // 1. Villager wins over node when both are under the finger.
                //    Priority was decided when the target was resolved, so there
                //    is no re-deciding it here.
                case GestureTargetKind.Villager:
                {
                    // SelectSingle owns validity: not ours, dead or consumed all
                    // return false. An unselectable villager is treated as empty
                    // ground rather than swallowing the tap, so tapping an enemy
                    // still clears the way it would anywhere else.
                    if (selection != null && selection.SelectSingle(target.id))
                    {
                        if (panel != null) panel.ClosePanel();
                        Log("villager " + target.id + " selected");
                        return;
                    }

                    Log("villager " + target.id + " not selectable -> treated as empty");
                    ClearEverything();
                    return;
                }

                // 2. A node tapped while villagers are selected is a move order.
                //    This is why the rung order matters: the same tap means
                //    "move here" or "inspect this" depending only on whether a
                //    selection exists.
                case GestureTargetKind.Node:
                {
                    bool hasSelection = selection != null &&
                                        selection.SelectedVillagerIDs.Count > 0;

                    if (hasSelection)
                    {
                        if (commands != null) commands.IssueMoveTo(target.id);
                        Log("move order to node " + target.id);
                        return;
                    }

                    // 3. No selection: the tap is an inspection.
                    if (panel != null) panel.OpenForNode(target.id);
                    Log("panel for node " + target.id);
                    return;
                }

                // 4. Empty ground dismisses everything. Both halves fire
                //    together and in a defined order, which the two independent
                //    clear-on-miss paths in the old code did not guarantee.
                default:
                    Log("empty tap -> clear");
                    ClearEverything();
                    return;
            }
        }

        private void ClearEverything()
        {
            if (selection != null) selection.ClearSelection();
            if (panel != null) panel.ClosePanel();
        }

        private void Log(string message)
        {
            if (verboseLogging) Debug.Log("[TAP] " + message);
        }
    }
}
