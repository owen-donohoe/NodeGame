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
        private Transform gfxTransform;

        private NodeWar.Core.TickRunner tickRunner;
        private SelectionSystem selectionSystem;

        // Colors
        private static readonly Color player0Color = new Color(0.3f, 0.6f, 1f);
        private static readonly Color player1Color = new Color(1f, 0.35f, 0.5f);
        private static readonly Color selectedTint = new Color(1f, 1f, 0.4f);
        private static readonly Color movingTint = new Color(0.8f, 0.9f, 0.4f);
        private static readonly Color claimingTint = new Color(0.4f, 1f, 0.6f);

        // Stack offset
        private Vector3 stackOffset;

        public void Initialize(SimulationState state, int id)
        {
            simState = state;
            villagerID = id;
            initialized = true;

            gfxTransform = transform.Find("GFX");

            if (gfxTransform != null)
            {
                spriteRenderers = gfxTransform.GetComponentsInChildren<SpriteRenderer>();
            }
            else
            {
                spriteRenderers = GetComponentsInChildren<SpriteRenderer>();
            }

            // Stack offset based on ID
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

            // Bounds check: villagerID might be out of range if array was resized
            // (shouldn't happen since views are spawned per-villager, but safety)
            if (villagerID >= simState.villagers.Length) return;

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

            // === Position ===
            Vector3 targetPos;
            if (villager.state == VillagerState.Moving && villager.movePath.Length > 1 && tickRunner != null)
            {
                Vector3 fromPos = simState.nodes[villager.movePath[villager.movePathIndex]].worldPosition;
                Vector3 toPos;

                if (villager.movePathIndex + 1 < villager.movePath.Length)
                    toPos = simState.nodes[villager.movePath[villager.movePathIndex + 1]].worldPosition;
                else
                    toPos = fromPos;

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

            // === Color ===
            Color baseColor = (villager.ownerID == 0) ? player0Color : player1Color;
            Color finalColor = baseColor;

            // State tinting
            switch (villager.state)
            {
                case VillagerState.Moving:
                    finalColor = movingTint;
                    break;
                case VillagerState.Claiming:
                    finalColor = Color.Lerp(baseColor, claimingTint, 0.5f);
                    break;
            }

            // Selection override (highest priority visual)
            if (selectionSystem != null && selectionSystem.IsSelected(villagerID))
            {
                finalColor = selectedTint;
            }

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