using UnityEngine;
using UnityEngine.InputSystem;
using NodeWar.Simulation;
using System.Collections.Generic;
using UnityEngine.EventSystems;

namespace NodeWar.Input
{
    public class SelectionSystem : MonoBehaviour
    {
        private SimulationState simState;
        private int localPlayerID = 0;

        private List<int> selectedVillagerIDs = new List<int>();
        public IReadOnlyList<int> SelectedVillagerIDs => selectedVillagerIDs;

        public int SelectedVillagerID => selectedVillagerIDs.Count > 0 ? selectedVillagerIDs[0] : -1;

        private bool isDragging = false;
        private Vector2 dragStartScreenPos;
        private const float DRAG_THRESHOLD = 10f;

        private Camera mainCam;

        private LayerMask villagerLayer;
        private Transform[] villagerTransforms;

        public void Initialize(SimulationState state, int playerID)
        {
            simState = state;
            localPlayerID = playerID;
            mainCam = Camera.main;
            villagerLayer = LayerMask.GetMask("Villagers");
        }

        public void SetPlayerID(int id)
        {
            localPlayerID = id;
            ClearSelection();
        }

        public void SetVillagerTransforms(Transform[] transforms)
        {
            villagerTransforms = transforms;
        }

        private void Update()
        {
            if (simState == null) return;

            Mouse mouse = Mouse.current;
            if (mouse == null) return;

            if (mouse.leftButton.wasPressedThisFrame)
            {
                if (UnityEngine.EventSystems.EventSystem.current != null &&
                    UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
                    return;

                isDragging = true;
                dragStartScreenPos = mouse.position.ReadValue();
            }

            if (mouse.leftButton.wasReleasedThisFrame && isDragging)
            {
                isDragging = false;
                Vector2 releasePos = mouse.position.ReadValue();
                float dragDistance = Vector2.Distance(dragStartScreenPos, releasePos);

                Debug.Log("[SEL] Left released. Drag distance: " + dragDistance);

                if (dragDistance < DRAG_THRESHOLD)
                {
                    TryClickSelect(releasePos);
                }
                else
                {
                    CircleSelect(dragStartScreenPos, dragDistance);
                }

                Debug.Log("[SEL] After select action. Selected count: " + selectedVillagerIDs.Count);
                for (int i = 0; i < selectedVillagerIDs.Count; i++)
                    Debug.Log("[SEL]   Selected villager ID: " + selectedVillagerIDs[i]);
            }
        }

        private void TryClickSelect(Vector2 screenPos)
        {
            bool shiftHeld = Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;

            Ray ray = mainCam.ScreenPointToRay(screenPos);

            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, 100f, villagerLayer))
            {
                NodeWar.View.VillagerView villagerView = hit.collider.GetComponentInParent<NodeWar.View.VillagerView>();
                if (villagerView != null)
                {
                    int id = villagerView.GetVillagerID();

                    if (simState.villagers[id].ownerID == localPlayerID &&
                        simState.villagers[id].state != VillagerState.Dead &&
                        !simState.villagers[id].isConsumed)
                    {
                        if (shiftHeld)
                        {
                            // Toggle: remove if already selected, add if not
                            if (selectedVillagerIDs.Contains(id))
                                selectedVillagerIDs.Remove(id);
                            else
                                selectedVillagerIDs.Add(id);
                        }
                        else
                        {
                            // Normal: clear and select single
                            selectedVillagerIDs.Clear();
                            selectedVillagerIDs.Add(id);
                        }
                        return;
                    }
                }
            }

            // Clicked nothing valid
            if (!shiftHeld)
                ClearSelection();
        }

        private void CircleSelect(Vector2 center, float radius)
        {
            bool shiftHeld = Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;

            if (!shiftHeld)
                selectedVillagerIDs.Clear();

            for (int i = 0; i < simState.villagers.Length; i++)
            {
                VillagerData v = simState.villagers[i];
                if (v.ownerID != localPlayerID) continue;
                if (v.state == VillagerState.Dead) continue;
                if (v.isConsumed) continue;

                Vector3 worldPos;
                if (villagerTransforms != null && i < villagerTransforms.Length && villagerTransforms[i] != null)
                {
                    worldPos = villagerTransforms[i].position;
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
                    // Prevent duplicates when shift-adding
                    if (!selectedVillagerIDs.Contains(i))
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