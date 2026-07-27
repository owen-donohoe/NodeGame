using UnityEngine;
using UnityEngine.InputSystem;
using NodeWar.Simulation;

namespace NodeWar.Input
{
    public class SelectionSystem : MonoBehaviour
    {
        private SimulationState simState;
        private int localPlayerID = 0;

        public int SelectedVillagerID { get; private set; } = -1;

        private Camera mainCam;

        public void Initialize(SimulationState state, int playerID)
        {
            simState = state;
            localPlayerID = playerID;
            mainCam = Camera.main;
        }

        private void Update()
        {
            if (simState == null) return;

            Mouse mouse = Mouse.current;
            if (mouse == null) return;

            if (mouse.leftButton.wasPressedThisFrame)
            {
                TrySelectVillager();
            }
        }

        private void TrySelectVillager()
        {
            Vector2 screenPos = Mouse.current.position.ReadValue();
            Ray ray = mainCam.ScreenPointToRay(screenPos);

            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, 100f))
            {
                NodeWar.View.VillagerView villagerView = hit.collider.GetComponentInParent<NodeWar.View.VillagerView>();
                if (villagerView != null)
                {
                    int id = villagerView.GetVillagerID();

                    if (simState.villagers[id].ownerID == localPlayerID)
                    {
                        SelectedVillagerID = id;
                        return;
                    }
                }
            }
        }

        public void ClearSelection()
        {
            SelectedVillagerID = -1;
        }
    }
}