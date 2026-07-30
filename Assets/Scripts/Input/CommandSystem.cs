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

        // Player highlight colors for node target pulse
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

                    // Trigger node highlight pulse
                    Color highlightColor = (localPlayerID == 0) ? p0HighlightColor : p1HighlightColor;
                    nodeView.TriggerHighlight(highlightColor);

                    selectionSystem.ClearSelection();
                }
            }
        }
    }
}