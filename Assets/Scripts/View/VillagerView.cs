using UnityEngine;
using NodeWar.Simulation;
using NodeWar.Input;

namespace NodeWar.View
{
    public class VillagerView : MonoBehaviour
    {
        [Header("Player 0 (Blue) Colors")]
        [SerializeField] private Color p0BaseColor = new Color(0.3f, 0.6f, 1f);
        [SerializeField] private Color p0MovingColor = new Color(0.5f, 0.75f, 1f);
        [SerializeField] private Color p0ClaimingColor = new Color(0.3f, 0.8f, 0.9f);
        [SerializeField] private Color p0FightingColor = new Color(0.6f, 0.3f, 1f);
        [SerializeField] private Color p0IdleColor = new Color(0.3f, 0.6f, 1f);

        [Header("Player 1 (Red) Colors")]
        [SerializeField] private Color p1BaseColor = new Color(1f, 0.35f, 0.5f);
        [SerializeField] private Color p1MovingColor = new Color(1f, 0.6f, 0.4f);
        [SerializeField] private Color p1ClaimingColor = new Color(1f, 0.5f, 0.7f);
        [SerializeField] private Color p1FightingColor = new Color(1f, 0.15f, 0.15f);
        [SerializeField] private Color p1IdleColor = new Color(1f, 0.35f, 0.5f);

        [Header("Selection Outline")]
        [SerializeField] private Color outlineColor = new Color(1f, 1f, 1f, 1f);
        [SerializeField] private float outlineThickness = 1.5f;

        private SimulationState simState;
        private int villagerID;
        private bool initialized = false;

        private SpriteRenderer[] spriteRenderers;
        private MaterialPropertyBlock[] propBlocks;
        private Transform gfxTransform;

        private NodeWar.Core.TickRunner tickRunner;
        private SelectionSystem selectionSystem;

        private bool lastOutlineState = false;

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

            propBlocks = new MaterialPropertyBlock[spriteRenderers.Length];
            for (int i = 0; i < propBlocks.Length; i++)
            {
                propBlocks[i] = new MaterialPropertyBlock();
            }

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
            if (villagerID >= simState.villagers.Length) return;

            UpdateVisuals();
        }

        private void UpdateVisuals()
        {
            VillagerData villager = simState.villagers[villagerID];

            if (villager.state == VillagerState.Dead || villager.isConsumed)
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

            // === Color via SpriteRenderer.color (vertex color channel, always works) ===
            Color stateColor = GetStateColor(villager);
            SetRenderersColor(stateColor);

            // === Outline via PropertyBlock (only outline properties, no color conflict) ===
            bool isSelected = (selectionSystem != null && selectionSystem.IsSelected(villagerID));
            Debug.Log(isSelected);

            if (isSelected != lastOutlineState)
            {
                SetOutline(isSelected);
                lastOutlineState = isSelected;
            }
        }

        private void SetOutline(bool enabled)
        {
            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                spriteRenderers[i].GetPropertyBlock(propBlocks[i]);
                propBlocks[i].SetFloat("_OutlineEnabled", enabled ? 1f : 0f);
                propBlocks[i].SetColor("_OutlineColor", outlineColor);
                propBlocks[i].SetFloat("_OutlineThickness", outlineThickness);
                spriteRenderers[i].SetPropertyBlock(propBlocks[i]);
            }
        }

        private Color GetStateColor(VillagerData villager)
        {
            if (villager.ownerID == 0)
            {
                switch (villager.state)
                {
                    case VillagerState.Moving: return p0MovingColor;
                    case VillagerState.Claiming: return p0ClaimingColor;
                    case VillagerState.Fighting: return p0FightingColor;
                    case VillagerState.Idle: return p0IdleColor;
                    case VillagerState.Working: return p0BaseColor;
                    default: return p0BaseColor;
                }
            }
            else
            {
                switch (villager.state)
                {
                    case VillagerState.Moving: return p1MovingColor;
                    case VillagerState.Claiming: return p1ClaimingColor;
                    case VillagerState.Fighting: return p1FightingColor;
                    case VillagerState.Idle: return p1IdleColor;
                    case VillagerState.Working: return p1BaseColor;
                    default: return p1BaseColor;
                }
            }
        }

        private void SetRenderersColor(Color color)
        {
            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                spriteRenderers[i].color = color;
            }
        }

        private void SetRenderersEnabled(bool enabled)
        {
            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                spriteRenderers[i].enabled = enabled;
            }
        }

        public int GetVillagerID()
        {
            return villagerID;
        }
    }
}