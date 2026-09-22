using NodeWar.Input;
using NodeWar.Simulation;
using NodeWar.UI;
using NodeWar.View.Outline;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NodeWar.View
{
    /// <summary>
    /// Turns gameplay state into outline intents. The only thing in the project
    /// that calls <see cref="OutlineGroup.SetIntent"/>.
    ///
    /// The outline system shipped with no callers at all -- every outline on
    /// screen came from the debugStyle field in the Inspector. This is what
    /// connects it to the game: hover follows the pointer over the local
    /// player's own villagers, and Selected follows villager selection and the
    /// inspected node.
    ///
    /// Read-only against the simulation, and it issues no commands. It reads
    /// SimulationState and SelectionSystem and writes neither, which is what
    /// keeps it on the correct side of the view boundary -- an outline is
    /// presentation, and none of this may diverge between two peers.
    /// </summary>
    public sealed class OutlineDriver : MonoBehaviour
    {
        private SimulationState state;
        private SelectionSystem selection;
        private NodePanelManager panel;
        private Camera cam;

        private OutlineGroup[] villagerGroups;
        private OutlineGroup[] nodeGroups;

        private int hoveredVillager = -1;
        private int inspectedNode = -1;

        private LayerMask villagerLayer;

        public void Initialize(SimulationState simulationState, SelectionSystem selectionSystem,
                               Camera camera)
        {
            state = simulationState;
            selection = selectionSystem;
            cam = camera;

            // The same mask SelectionSystem raycasts against, so hover and the
            // tap that follows it can never disagree about what is under the
            // pointer.
            villagerLayer = LayerMask.GetMask("Villagers");
        }

        public void SetVillagerGroups(OutlineGroup[] groups) => villagerGroups = groups;

        public void SetNodeGroups(OutlineGroup[] groups) => nodeGroups = groups;

        /// <summary>
        /// Subscribes to node inspection.
        ///
        /// Deliberately not NodeOpened/NodeClosed: those track whether a sheet
        /// is showing, and GameplayHUDController still drives NodeSheet from
        /// them. Inspection is a separate, wider question -- it fires for
        /// every node a tap lands on, sheet or no sheet -- so the outline
        /// follows the tap even on a node with nothing to open.
        /// </summary>
        public void BindPanel(NodePanelManager panelManager)
        {
            if (panel != null)
            {
                panel.NodeInspected -= OnNodeInspected;
            }

            panel = panelManager;
            if (panel == null) return;

            panel.NodeInspected += OnNodeInspected;
        }

        /// <summary>
        /// Called when the debug switch changes sides.
        ///
        /// Only hover needs clearing by hand. SelectionSystem.SetPlayerID
        /// already clears the selection, so Selected intents drain through the
        /// ordinary sync below -- but the hovered villager belonged to the
        /// player who just stopped being local, and nothing would take its
        /// outline away until the pointer happened to move.
        /// </summary>
        public void OnPlayerSideChanged(int playerID)
        {
            SetHovered(-1);
        }

        private void OnNodeInspected(int nodeID) => inspectedNode = nodeID;

        private void OnDestroy()
        {
            if (panel == null) return;

            panel.NodeInspected -= OnNodeInspected;
        }

        // After the movement and slot code has placed things for the frame, so
        // the raycast hits villagers where they are being drawn rather than
        // where they were last frame.
        private void LateUpdate()
        {
            if (state == null || selection == null) return;

            UpdateHover();
            SyncVillagerSelection();
            SyncNodeSelection();
        }

        /// <summary>
        /// Points hover at whatever owned villager is under the cursor.
        ///
        /// Mouse only, and deliberately so: hover is a state a finger cannot
        /// express, so on a touch device there is nothing to show and this
        /// leaves the intent clear rather than inventing one from the last tap.
        /// </summary>
        private void UpdateHover()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || cam == null)
            {
                SetHovered(-1);
                return;
            }

            Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());

            if (!Physics.Raycast(ray, out RaycastHit hit, 100f, villagerLayer))
            {
                SetHovered(-1);
                return;
            }

            VillagerView view = hit.collider.GetComponentInParent<VillagerView>();
            if (view == null)
            {
                SetHovered(-1);
                return;
            }

            int id = view.GetVillagerID();

            // Both the ownership rule and the switch-awareness come from here.
            // IsSelectable reads SelectionSystem's current localPlayerID and
            // already means "ours, alive, not consumed", so Tab moves hover to
            // the other side's villagers with no further bookkeeping, and an
            // opponent's villager never lights up.
            SetHovered(selection.IsSelectable(id) ? id : -1);
        }

        private void SetHovered(int villagerID)
        {
            if (hoveredVillager == villagerID) return;

            ApplyIntent(villagerGroups, hoveredVillager, OutlineStyle.Hover, false);
            hoveredVillager = villagerID;
            ApplyIntent(villagerGroups, hoveredVillager, OutlineStyle.Hover, true);
        }

        /// <summary>
        /// Mirrors the selection onto the villager groups.
        ///
        /// Walks every villager rather than diffing against the previous
        /// selection. SetIntent returns immediately when the resolved mask does
        /// not change, so the steady-state cost is a comparison per villager,
        /// and the bookkeeping a diff would need is not worth owning.
        /// </summary>
        private void SyncVillagerSelection()
        {
            if (villagerGroups == null) return;

            int count = Mathf.Min(villagerGroups.Length, state.villagers.Length);

            for (int i = 0; i < count; i++)
            {
                OutlineGroup group = villagerGroups[i];
                if (group == null) continue;

                group.SetIntent(OutlineStyle.Selected, selection.IsSelected(i) && IsAlive(i));
            }
        }

        private void SyncNodeSelection()
        {
            if (nodeGroups == null) return;

            for (int i = 0; i < nodeGroups.Length; i++)
            {
                OutlineGroup group = nodeGroups[i];
                if (group == null) continue;

                group.SetIntent(OutlineStyle.Selected, i == inspectedNode);
            }
        }

        /// <summary>
        /// A dead or consumed villager can still be in SelectionSystem's list,
        /// but must not keep its outline: VillagerView disables its renderers,
        /// so the group would hold an ID while contributing nothing -- one of
        /// the 255 the allocator has, spent on an invisible villager.
        /// </summary>
        private bool IsAlive(int villagerID)
        {
            if (villagerID < 0 || villagerID >= state.villagers.Length) return false;

            VillagerData v = state.villagers[villagerID];
            return v.state != VillagerState.Dead && !v.isConsumed;
        }

        private static void ApplyIntent(OutlineGroup[] groups, int index,
                                        OutlineStyle style, bool active)
        {
            if (groups == null) return;
            if (index < 0 || index >= groups.Length) return;
            if (groups[index] == null) return;

            groups[index].SetIntent(style, active);
        }
    }
}
