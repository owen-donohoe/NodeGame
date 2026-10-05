using UnityEngine;
using NodeWar.Simulation;
using NodeWar.View;

namespace NodeWar.UI
{
    /// <summary>
    /// The breach bar over a Core: how far the attackers on it have got toward
    /// spending one of themselves. Counterpart to NodeClaimBar, and like it a
    /// camera-facing world-space indicator that reads SimulationState and
    /// nothing else -- but built in code and added to the Core node at runtime,
    /// so the node prefab needs no edit.
    ///
    /// Visible to both players. Fill is PlayerData.breachBar / breachBarMax for
    /// the defender whose Core this is; one pip sits under the bar for each
    /// Breaching attacker on it, in the attacker's colour. Hidden while nothing
    /// is banked and nobody is channelling, fading in and out rather than
    /// popping.
    /// </summary>
    public class CoreBreachBar : MonoBehaviour
    {
        private const int PipCapacity = BreachTempoMath.MaxPips;

        private static Sprite whiteSprite;
        private static Sprite whiteSpriteLeftPivot;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSharedSpritesOnEnterPlayMode()
        {
            whiteSprite = null;
            whiteSpriteLeftPivot = null;
        }

        private SimulationState simState;
        private int defenderID;
        private BreachCueSettings cues;
        private Camera viewCamera;
        private Transform root;

        private SpriteRenderer background;
        private SpriteRenderer fill;
        private SpriteRenderer[] pips;

        private float barWidth;
        private float barHeight;
        private float heightOffset;
        private float alpha;
        private bool initialized;

        /// <param name="defender">The player whose Core this is; their breachBar fills it.</param>
        /// <param name="nodeScale">World size of one grid step, so the bar scales with the board.</param>
        public void Initialize(SimulationState state, int defender, BreachCueSettings settings, float nodeScale)
        {
            simState = state;
            defenderID = defender;
            cues = settings;
            viewCamera = Camera.main;

            barWidth = nodeScale * 0.7f;
            barHeight = barWidth * 0.13f;
            heightOffset = nodeScale * 0.62f;

            Build();
            initialized = true;
            ApplyAlpha(0f);
        }

        private static Sprite GetSprite(bool leftPivot)
        {
            if (whiteSprite == null)
            {
                Texture2D tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
                Color32[] px = new Color32[16];
                for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
                tex.SetPixels32(px);
                tex.Apply();
                tex.filterMode = FilterMode.Point;

                // 4px at 4 PPU is one world unit, so a scale is a size.
                whiteSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
                whiteSpriteLeftPivot = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0f, 0.5f), 4f);
            }

            return leftPivot ? whiteSpriteLeftPivot : whiteSprite;
        }

        private SpriteRenderer MakeQuad(string name, bool leftPivot, int order)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(root, false);

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = GetSprite(leftPivot);
            // Core GFX uses the Villagers layer at group order 0. The bar is
            // outside that group: orders 400/401 put it above the art, below UI.
            sr.sortingLayerID = SortingLayer.NameToID("Villagers");
            sr.sortingOrder = order;
            return sr;
        }

        private void Build()
        {
            root = new GameObject("BreachBar_P" + defenderID).transform;
            root.SetParent(transform, false);

            // Sizes below are world units. Cancel whatever scale the node prefab
            // carries so they come out that way.
            Vector3 parentScale = transform.lossyScale;
            root.localScale = new Vector3(
                parentScale.x != 0f ? 1f / parentScale.x : 1f,
                parentScale.y != 0f ? 1f / parentScale.y : 1f,
                parentScale.z != 0f ? 1f / parentScale.z : 1f);

            // Above the node, kept clear of the claim bar: a Core's claim bar
            // sits at +/-claimThreshold and never shows.
            background = MakeQuad("Back", false, 400);
            background.color = new Color(0.05f, 0.05f, 0.08f, 0.8f);
            background.transform.localScale = new Vector3(barWidth + barHeight * 0.5f, barHeight * 1.5f, 1f);

            fill = MakeQuad("Fill", true, 401);
            fill.transform.localPosition = new Vector3(-barWidth * 0.5f, 0f, 0f);
            fill.transform.localScale = new Vector3(0f, barHeight, 1f);

            pips = new SpriteRenderer[PipCapacity];
            float pip = barHeight * 1.1f;
            for (int i = 0; i < PipCapacity; i++)
            {
                SpriteRenderer p = MakeQuad("Pip" + i, false, 401);
                p.transform.localScale = new Vector3(pip, pip, 1f);
                p.enabled = false;
                pips[i] = p;
            }
        }

        private int CountBreachers()
        {
            int core = simState.players[defenderID].coreNodeID;
            int attacker = 1 - defenderID;
            int count = 0;

            for (int i = 0; i < simState.villagers.Length; i++)
            {
                VillagerData v = simState.villagers[i];
                if (v.isConsumed || v.ownerID != attacker) continue;
                if (v.state != VillagerState.Breaching || v.currentNodeID != core) continue;
                count++;
            }

            return count;
        }

        private void Update()
        {
            if (!initialized || simState == null || cues == null) return;

            PlayerData defender = simState.players[defenderID];
            int breachers = CountBreachers();
            float fraction = BreachTempoMath.BarFill(defender.breachBar, cues.breachBarMax);
            bool visible = cues.breachBarMax > 0 && BreachTempoMath.BarVisible(defender.breachBar, breachers);

            alpha = Mathf.MoveTowards(alpha, visible ? 1f : 0f, 4f * Time.deltaTime);
            ApplyAlpha(alpha);
            if (alpha <= 0f) return;

            fill.transform.localScale = new Vector3(fraction * barWidth, barHeight, 1f);

            // Brightens toward full so a bar about to spend someone reads as
            // urgent without a second channel; reduced motion keeps it steady.
            Color c = cues.breacherColor;
            float urgency = fraction > 0.75f && !cues.reducedMotion
                ? 0.5f + 0.5f * Mathf.Sin(Time.time * cues.barPulseHz * Mathf.PI * 2f)
                : 0f;
            fill.color = Color.Lerp(c, Color.white, urgency * 0.55f) * new Color(1f, 1f, 1f, alpha);

            LayoutPips(BreachTempoMath.PipsToShow(breachers));
        }

        private void LayoutPips(int shown)
        {
            Color pipColor = defenderID == 0 ? cues.p1PipColor : cues.p0PipColor; // the attacker's
            pipColor.a = alpha;

            float pip = barHeight * 1.1f;
            float gap = pip * 0.45f;
            float total = shown * pip + (shown - 1) * gap;
            float x = -total * 0.5f + pip * 0.5f;
            float y = -(barHeight * 0.75f + pip * 0.85f);

            for (int i = 0; i < pips.Length; i++)
            {
                bool on = i < shown;
                pips[i].enabled = on;
                if (!on) continue;

                pips[i].transform.localPosition = new Vector3(x + i * (pip + gap), y, 0f);
                pips[i].color = pipColor;
            }
        }

        private void ApplyAlpha(float a)
        {
            if (background == null) return;

            Color b = background.color;
            b.a = 0.8f * a;
            background.color = b;

            if (a <= 0f)
            {
                fill.enabled = false;
                for (int i = 0; i < pips.Length; i++) pips[i].enabled = false;
            }
            else
            {
                fill.enabled = true;
            }

            background.enabled = a > 0f;
        }

        private void LateUpdate()
        {
            if (!initialized || root == null) return;

            if (viewCamera == null) viewCamera = Camera.main;
            if (viewCamera == null) return;

            // Billboard, lifted along the camera's own up so it sits above the
            // node however the POV is turned.
            root.rotation = viewCamera.transform.rotation;
            root.position = transform.position + viewCamera.transform.up * heightOffset;
        }
    }
}
