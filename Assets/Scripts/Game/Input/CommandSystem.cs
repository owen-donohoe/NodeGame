using UnityEngine;
using UnityEngine.InputSystem;
using NodeWar.Simulation;

namespace NodeWar.Input
{
    public class CommandSystem : MonoBehaviour
    {
        private SimulationState simState;
        private InputBuffer inputBuffer;
        private SelectionSystem selectionSystem;
        private int localPlayerID = 0;

        private PointerGestureSource gestureSource;
        private bool gestureRouted;

        private static readonly Color p0HighlightColor = new Color(0.4f, 0.7f, 1f, 0.9f);
        private static readonly Color p1HighlightColor = new Color(1f, 0.4f, 0.5f, 0.9f);

        public void Initialize(SimulationState state, InputBuffer buffer, SelectionSystem selection, int playerID)
        {
            simState = state;
            inputBuffer = buffer;
            selectionSystem = selection;
            localPlayerID = playerID;
        }

        /// <summary>Enables resolved right-click orders; keyboard shortcuts remain independent.</summary>
        public void SetGestureRouted(bool routed)
        {
            gestureRouted = routed;
        }

        public void SetGestureSource(PointerGestureSource source)
        {
            if (gestureSource != null)
                gestureSource.OnSecondaryClick -= HandleSecondaryClick;

            gestureSource = source;

            if (gestureSource != null)
                gestureSource.OnSecondaryClick += HandleSecondaryClick;
        }

        private void HandleSecondaryClick(GestureTarget target)
        {
            if (!gestureRouted || !isActiveAndEnabled) return;
            if (target.kind == GestureTargetKind.Node) IssueMoveTo(target.id);
        }

        private void OnDestroy()
        {
            SetGestureSource(null);
        }

        public void SetPlayerID(int id)
        {
            localPlayerID = id;
        }

        /// <summary>
        /// Node views indexed by node ID, so a move issued by ID can still
        /// trigger the destination highlight. Matches the existing
        /// SetNodeSlotManagers pattern on SelectionSystem.
        /// </summary>
        public void SetNodeViews(NodeWar.View.NodeView[] views)
        {
            nodeViews = views;
        }

        private NodeWar.View.NodeView[] nodeViews;

        /// <summary>
        /// Orders every selected villager to a node, highlights the
        /// destination, and clears the selection.
        ///
        /// Extracted from the right-click path so a tap can reach the same
        /// behaviour without a second raycast. This is the only place a Move
        /// command is built; both the desktop right-click and the touch tap
        /// funnel through here.
        /// </summary>
        public void IssueMoveTo(int targetNode)
        {
            if (simState == null || inputBuffer == null) return;
            if (selectionSystem == null || selectionSystem.SelectedVillagerIDs.Count == 0) return;
            if (targetNode < 0 || targetNode >= simState.nodes.Length) return;

            for (int i = 0; i < selectionSystem.SelectedVillagerIDs.Count; i++)
            {
                int villagerID = selectionSystem.SelectedVillagerIDs[i];

                GameCommand cmd = new GameCommand
                {
                    type = CommandType.Move,
                    playerID = localPlayerID,
                    villagerID = villagerID,
                    targetNodeID = targetNode,
                    issuedOnTick = simState.tickCount
                };

                inputBuffer.EnqueueCommand(cmd);
            }

            if (nodeViews != null && targetNode < nodeViews.Length && nodeViews[targetNode] != null)
            {
                Color highlightColor = (localPlayerID == 0) ? p0HighlightColor : p1HighlightColor;
                nodeViews[targetNode].TriggerHighlight(highlightColor);
            }

            selectionSystem.ClearSelection();
        }

        private void Update()
        {
            if (simState == null) return;

            Keyboard keyboard = Keyboard.current;

            // E key: equip selected villagers as Soldiers
            if (keyboard != null && keyboard.eKey.wasPressedThisFrame)
            {
                TryIssueEquipCommand();
            }

            // R key: respawn first dead villager
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
            {
                TryIssueRespawnCommand();
            }
        }

        private void TryIssueEquipCommand()
        {
            if (selectionSystem.SelectedVillagerIDs.Count == 0) return;

            for (int i = 0; i < selectionSystem.SelectedVillagerIDs.Count; i++)
            {
                int villagerID = selectionSystem.SelectedVillagerIDs[i];

                GameCommand cmd = new GameCommand
                {
                    type = CommandType.Equip,
                    playerID = localPlayerID,
                    villagerID = villagerID,
                    value = (int)SuitType.Warrior,  // TODO: suit picker UI
                    issuedOnTick = simState.tickCount
                };

                inputBuffer.EnqueueCommand(cmd);
            }
        }

        private void TryIssueRespawnCommand()
        {
            // Find the first dead non-consumed villager owned by this player
            for (int i = 0; i < simState.villagers.Length; i++)
            {
                VillagerData v = simState.villagers[i];
                if (v.ownerID != localPlayerID) continue;
                if (v.state != VillagerState.Dead) continue;
                if (v.isConsumed) continue;

                GameCommand cmd = new GameCommand
                {
                    type = CommandType.Respawn,
                    playerID = localPlayerID,
                    villagerID = i,
                    issuedOnTick = simState.tickCount
                };

                inputBuffer.EnqueueCommand(cmd);
                return; // Only respawn one per press
            }
        }
    }
}