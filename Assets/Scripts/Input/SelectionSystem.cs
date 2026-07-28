using UnityEngine;
using UnityEngine.InputSystem;
using NodeWar.Simulation;
using System.Collections.Generic;

namespace NodeWar.Input
{
    public class SelectionSystem : MonoBehaviour
    {
        private SimulationState simState;
        private int localPlayerID = 0;

        private List<int> selectedVillagerIDs = new List<int>();
        public IReadOnlyList<int> SelectedVillagerIDs => selectedVillagerIDs;

        // Backward compat convenience
        public int SelectedVillagerID => selectedVillagerIDs.Count > 0 ? selectedVillagerIDs[0] : -1;

        // Circle drag state
        private bool isDragging = false;
        private Vector2 dragStartScreenPos;
        private const float DRAG_THRESHOLD = 10f; // pixels

        private Camera mainCam;

        public void Initialize(SimulationState state, int playerID)
        {
            simState = state;
            localPlayerID = playerID;
            mainCam = Camera.main;
        }

        /// <summary>
        /// Sets the local player ID this selection system responds to.
        /// Used by DebugPlayerSwitch to toggle control between players.
        /// </summary>
        public void SetPlayerID(int id)
        {
            localPlayerID = id;
            ClearSelection();
        }

        private void Update()
        {
            if (simState == null) return;

            Mouse mouse = Mouse.current;
            if (mouse == null) return;

            if (mouse.leftButton.wasPressedThisFrame)
            {
                isDragging = true;
                dragStartScreenPos = mouse.position.ReadValue();
            }

            if (mouse.leftButton.wasReleasedThisFrame && isDragging)
            {
                isDragging = false;
                Vector2 releasePos = mouse.position.ReadValue();
                float dragDistance = Vector2.Distance(dragStartScreenPos, releasePos);

                if (dragDistance < DRAG_THRESHOLD)
                {
                    TryClickSelect(releasePos);
                }
                else
                {
                    CircleSelect(dragStartScreenPos, dragDistance);
                }
            }
        }

        private void TryClickSelect(Vector2 screenPos)
        {
            Ray ray = mainCam.ScreenPointToRay(screenPos);

            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, 100f))
            {
                NodeWar.View.VillagerView villagerView = hit.collider.GetComponentInParent<NodeWar.View.VillagerView>();
                if (villagerView != null)
                {
                    int id = villagerView.GetVillagerID();
                    if (simState.villagers[id].ownerID == localPlayerID &&
                        simState.villagers[id].state != VillagerState.Dead &&
                        !simState.villagers[id].isConsumed)
                    {
                        selectedVillagerIDs.Clear();
                        selectedVillagerIDs.Add(id);
                        return;
                    }
                }
            }

            // Clicked nothing valid � deselect
            ClearSelection();
        }

        private void CircleSelect(Vector2 center, float radius)
        {
            selectedVillagerIDs.Clear();

            for (int i = 0; i < simState.villagers.Length; i++)
            {
                VillagerData v = simState.villagers[i];
                if (v.ownerID != localPlayerID) continue;
                if (v.state == VillagerState.Dead) continue;
                if (v.isConsumed) continue;

                // Get world position (use currentNodeID for stationary, path position for moving)
                Vector3 worldPos;
                if (v.state == VillagerState.Moving && v.movePath.Length > 1)
                {
                    worldPos = simState.nodes[v.movePath[v.movePathIndex]].worldPosition;
                }
                else
                {
                    worldPos = simState.nodes[v.currentNodeID].worldPosition;
                }

                Vector3 screenPos = mainCam.WorldToScreenPoint(worldPos);
                if (screenPos.z < 0) continue;

                float dist = Vector2.Distance(center, new Vector2(screenPos.x, screenPos.y));
                if (dist <= radius)
                {
                    selectedVillagerIDs.Add(i);
                }
            }
        }

        public void ClearSelection()
        {
            selectedVillagerIDs.Clear();
        }

        public bool IsSelected(int villagerID)
        {
            for (int i = 0; i < selectedVillagerIDs.Count; i++)
            {
                if (selectedVillagerIDs[i] == villagerID) return true;
            }
            return false;
        }

        public bool IsDragging => isDragging;
        public Vector2 DragStart => dragStartScreenPos;
        public float CurrentDragRadius
        {
            get
            {
                if (!isDragging) return 0f;
                Mouse mouse = Mouse.current;
                if (mouse == null) return 0f;
                return Vector2.Distance(dragStartScreenPos, mouse.position.ReadValue());
            }
        }
    }
}