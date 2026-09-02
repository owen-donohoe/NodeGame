using UnityEngine;
using UnityEngine.InputSystem;
using NodeWar.Simulation;
using UnityEngine.EventSystems;

namespace NodeWar.Input
{
    public class CommandSystem : MonoBehaviour
    {
        private SimulationState simState;
        private InputBuffer inputBuffer;
        private SelectionSystem selectionSystem;
        private int localPlayerID = 0;

        private Camera mainCam;

        private static readonly Color p0HighlightColor = new Color(0.4f, 0.7f, 1f, 0.9f);
        private static readonly Color p1HighlightColor = new Color(1f, 0.4f, 0.5f, 0.9f);

        private LayerMask nodeLayer;

        public void Initialize(SimulationState state, InputBuffer buffer, SelectionSystem selection, int playerID)
        {
            simState = state;
            inputBuffer = buffer;
            selectionSystem = selection;
            localPlayerID = playerID;
            mainCam = Camera.main;
            nodeLayer = LayerMask.GetMask("Nodes");
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

            Mouse mouse = Mouse.current;
            Keyboard keyboard = Keyboard.current;

            // Right-click: move command
            if (mouse != null && mouse.rightButton.wasPressedThisFrame)
            {
                TryIssueMoveCommand();
            }

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

        private void TryIssueMoveCommand()
        {
            //if (UnityEngine.EventSystems.EventSystem.current != null &&
            //    UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            //    return;

            if (selectionSystem.SelectedVillagerIDs.Count == 0)
            {
                return;
            }

            Vector2 screenPos = Mouse.current.position.ReadValue();
            Ray ray = mainCam.ScreenPointToRay(screenPos);

            // Draw the ray for 20 seconds in the Scene view
            //Debug.DrawRay(ray.origin, ray.direction * 100f, Color.yellow, 20f);

            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, 100f, nodeLayer))
            {
                Debug.DrawLine(ray.origin, hit.point, Color.green, 20f);

                NodeWar.View.NodeView nodeView = hit.collider.GetComponentInParent<NodeWar.View.NodeView>();
                if (nodeView != null)
                {
                    IssueMoveTo(nodeView.GetNodeID());
                }
                else
                {
                    Debug.Log("[CMD] Hit object has no NodeView in parents: " + hit.collider.gameObject.name);
                }
            }
            else
            {
                // Red = miss
                Debug.DrawRay(ray.origin, ray.direction * 100f, Color.red, 20f);
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