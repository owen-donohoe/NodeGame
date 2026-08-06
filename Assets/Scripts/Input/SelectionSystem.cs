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
            Debug.Log("[SEL] Initialized. Layer mask value: " + villagerLayer.value + " PlayerID: " + localPlayerID);
        }

        public void SetPlayerID(int id)
        {
            localPlayerID = id;
            ClearSelection();
        }

        public void SetVillagerTransforms(Transform[] transforms)
        {
            villagerTransforms = transforms;
            Debug.Log("[SEL] All Villager transforms set. Count: " + (transforms != null ? transforms.Length : 0));
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
            Debug.Log("[SEL] TryClickSelect at screen pos: " + screenPos);

            Ray ray = mainCam.ScreenPointToRay(screenPos);
            //Debug.Log("[SEL] Ray origin: " + ray.origin + " direction: " + ray.direction);

            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, 100f, villagerLayer))
            {
                Debug.Log("[SEL] Raycast HIT: " + hit.collider.gameObject.name + " on layer: " + hit.collider.gameObject.layer);

                NodeWar.View.VillagerView villagerView = hit.collider.GetComponentInParent<NodeWar.View.VillagerView>();
                if (villagerView != null)
                {
                    int id = villagerView.GetVillagerID();
                    Debug.Log("[SEL] Found VillagerView. ID: " + id + " Owner: " + simState.villagers[id].ownerID + " State: " + simState.villagers[id].state + " Consumed: " + simState.villagers[id].isConsumed);

                    if (simState.villagers[id].ownerID == localPlayerID &&
                        simState.villagers[id].state != VillagerState.Dead &&
                        !simState.villagers[id].isConsumed)
                    {
                        selectedVillagerIDs.Clear();
                        selectedVillagerIDs.Add(id);
                        Debug.Log("[SEL] SELECTED villager " + id);
                        return;
                    }
                    else
                    {
                        Debug.Log("[SEL] Villager failed ownership/state check. LocalPlayer: " + localPlayerID);
                    }
                }
                else
                {
                    Debug.Log("[SEL] Hit object on Villiger layer, has no VillagerView in parents: " + hit.collider.gameObject.name);
                }
            }
            else
            {
                Debug.Log("[SEL] Raycast MISSED. No collider on villager layer mask: " + villagerLayer.value);

                // Debug: try raycast without layer mask to see what we ARE hitting
                RaycastHit debugHit;
                if (Physics.Raycast(ray, out debugHit, 100f))
                {
                    Debug.Log("[SEL] (debug no-mask raycast hit: " + debugHit.collider.gameObject.name + " layer: " + debugHit.collider.gameObject.layer + ")");
                }
                else
                {
                    Debug.Log("[SEL] (debug no-mask raycast also missed - nothing in scene)");
                }
            }

            Debug.Log("[SEL] Clearing selection (nothing valid clicked)");
            ClearSelection();
        }

        private void CircleSelect(Vector2 center, float radius)
        {
            Debug.Log("[SEL] CircleSelect. Center: " + center + " Radius: " + radius);
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
                    selectedVillagerIDs.Add(i);
                    Debug.Log("[SEL] Circle captured villager " + i + " at screen dist: " + dist);
                }
            }

            Debug.Log("[SEL] CircleSelect done. Total selected: " + selectedVillagerIDs.Count);
        }

        public void ClearSelection()
        {
            if (selectedVillagerIDs.Count > 0)
                Debug.Log("[SEL] ClearSelection called. Was: " + selectedVillagerIDs.Count);
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