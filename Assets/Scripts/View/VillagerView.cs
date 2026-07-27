using UnityEngine;
using NodeWar.Simulation;
using NodeWar.Input;

namespace NodeWar.View
{
    public class VillagerView : MonoBehaviour
    {
        private SimulationState simState;
        private int villagerID;
        private bool initialized = false;

        private SpriteRenderer[] spriteRenderers;
        private Transform gfxTransform; // The GFX child

        // Reference to TickRunner for interpolation alpha
        private NodeWar.Core.TickRunner tickRunner;

        // Colors — brighter/more saturated than nodes
        private static readonly Color player0Color = new Color(0.3f, 0.6f, 1f);
        private static readonly Color player1Color = new Color(1f, 0.35f, 0.5f);
        private static readonly Color selectedTint = new Color(1f, 1f, 0.4f);

        // Offset for stacking
        private Vector3 stackOffset;

        // Selection highlight
        private SelectionSystem selectionSystem;

        public void Initialize(SimulationState state, int id)
        {
            simState = state;
            villagerID = id;
            initialized = true;

            // Find GFX child (if it exists)
            gfxTransform = transform.Find("GFX");

            // Get all sprite renderers (in GFX children)
            if (gfxTransform != null) 
            { 
                spriteRenderers = gfxTransform.GetComponentsInChildren<SpriteRenderer>();
            }
            else 
            {
                spriteRenderers = GetComponentsInChildren<SpriteRenderer>();
            }

            // Stack offset
            int localIndex = id % 10;
            float angle = localIndex * 36f * Mathf.Deg2Rad;
            float radius = 0.3f;
            stackOffset = new Vector3(Mathf.Cos(angle) * radius, 0.1f, Mathf.Sin(angle) * radius);

            UpdateVisuals();
        }

        public void SetTickRunner(NodeWar.Core.TickRunner runner)
        {
            tickRunner = runner;
        }

        public void SetSelectionSystem(SelectionSystem system)
        {
            selectionSystem = system;
        }

        private void Update()
        {
            if (!initialized) return;
            UpdateVisuals();
        }

        private void UpdateVisuals()
        {
            VillagerData villager = simState.villagers[villagerID];

            // Hide dead villagers
            if (villager.state == VillagerState.Dead)
            {
                SetRenderersEnabled(false);
                return;
            }
            else
            {
                SetRenderersEnabled(true);
            }

            //if (selectionSystem == null)
            //    Debug.Log("VilView: no selection sys");
            //else if (selectionSystem.SelectedVillagerID == villagerID)
            //    Debug.Log("Selected Vil_"+villagerID);
            
            //if(villager.movePath.Length <= 1)
            //    Debug.Log("VilView: length fail"); 
            //else if (tickRunner == null)
            //    Debug.Log("VilView: no tick runner");
            //else
            //    Debug.Log("VilView: Accepted, lerping");
            


            // Position with interpolation
            Vector3 targetPos;
            if (villager.state == VillagerState.Moving && villager.movePath.Length > 1 && tickRunner != null)
            {
                // Interpolate between previous position and next node in path
                Vector3 fromPos = simState.nodes[villager.movePath[villager.movePathIndex]].worldPosition;
                Vector3 toPos;

                if (villager.movePathIndex + 1 < villager.movePath.Length)
                    toPos = simState.nodes[villager.movePath[villager.movePathIndex + 1]].worldPosition;
                else
                    toPos = fromPos;

                // Progress within current edge + tick alpha for sub-tick smoothing
                float edgeProgress = (float)villager.moveProgress / (float)villager.moveSpeedTicks;
                float subTickAlpha = tickRunner.TickAlpha / (float)villager.moveSpeedTicks;
                float totalAlpha = Mathf.Clamp01(edgeProgress + subTickAlpha);

                targetPos = Vector3.Lerp(fromPos, toPos, totalAlpha);
            }
            else
            {
                targetPos = simState.nodes[villager.currentNodeID].worldPosition;
            }

            transform.position = targetPos + stackOffset;

            // Color
            bool isActive = (villager.state == VillagerState.Moving);
            Color baseColor = (villager.ownerID == 0) ? player0Color : player1Color;
            Color finalColor = isActive ? selectedTint : baseColor;

            SetRenderersColor(finalColor);
        }

        private void SetRenderersEnabled(bool enabled)
        {
            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                spriteRenderers[i].enabled = enabled;
            }
        }

        private void SetRenderersColor(Color color)
        {
            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                spriteRenderers[i].color = color;
            }
        }

        public int GetVillagerID()
        {
            return villagerID;
        }
    }
}