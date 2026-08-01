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

        private Camera mainCam;

        private static readonly Color p0HighlightColor = new Color(0.4f, 0.7f, 1f, 0.9f);
        private static readonly Color p1HighlightColor = new Color(1f, 0.4f, 0.5f, 0.9f);

        public void Initialize(SimulationState state, InputBuffer buffer, SelectionSystem selection, int playerID)
        {
            simState = state;
            inputBuffer = buffer;
            selectionSystem = selection;
            localPlayerID = playerID;
            mainCam = Camera.main;
        }

        public void SetPlayerID(int id)
        {
            localPlayerID = id;
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
            if (selectionSystem.SelectedVillagerIDs.Count == 0) return;

            Vector2 screenPos = Mouse.current.position.ReadValue();
            Ray ray = mainCam.ScreenPointToRay(screenPos);

            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, 100f))
            {
                NodeWar.View.NodeView nodeView = hit.collider.GetComponentInParent<NodeWar.View.NodeView>();
                if (nodeView != null)
                {
                    int targetNode = nodeView.GetNodeID();

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

                    Color highlightColor = (localPlayerID == 0) ? p0HighlightColor : p1HighlightColor;
                    nodeView.TriggerHighlight(highlightColor);

                    selectionSystem.ClearSelection();
                }
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