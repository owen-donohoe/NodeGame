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

        public void Initialize(SimulationState state, InputBuffer buffer, SelectionSystem selection, int playerID)
        {
            simState = state;
            inputBuffer = buffer;
            selectionSystem = selection;
            localPlayerID = playerID;
            mainCam = Camera.main;
        }

        private void Update()
        {
            if (simState == null) return;

            Mouse mouse = Mouse.current;
            if (mouse == null) return;

            if (mouse.rightButton.wasPressedThisFrame)
            {
                TryIssueMoveCommand();
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

                    // One command per selected villager
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

                    selectionSystem.ClearSelection();
                }
            }
        }
    }
}