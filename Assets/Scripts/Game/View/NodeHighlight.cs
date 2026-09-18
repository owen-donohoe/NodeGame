using UnityEngine;

namespace NodeWar.View
{
    /// <summary>
    /// Brief pulse ring that appears on a node when it's targeted by a move command.
    /// Scales up and fades out over a short duration.
    /// Attached to each NodeView, triggered externally.
    /// </summary>
    public class NodeHighlight : MonoBehaviour
    {
        [Header("Highlight Settings")]
        [SerializeField] private Color highlightColor = new Color(1f, 1f, 1f, 0.8f);
        [SerializeField] private float pulseDuration = 0.5f;
        [SerializeField] private float startScale = 0.5f;
        [SerializeField] private float endScale = 1.8f;
        [SerializeField] private float ringWidth = 0.06f;

        private LineRenderer ringRenderer;
        private float pulseTimer;
        private bool isPulsing;
        private int segments = 24;

        // One material shared by every node's ring rather than one per node:
        // nodes are instantiated once by GameManager.SpawnNodeViews and never
        // destroyed, so a per-instance Material would both pay Shader.Find
        // once per node at startup and leak for the scene's lifetime, since
        // nothing would ever be left to destroy it. Colour is set per-instance
        // on the LineRenderer itself, not the material, so sharing is safe.
        private static Material sharedRingMaterial;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSharedMaterialOnEnterPlayMode()
        {
            sharedRingMaterial = null;
        }

        private static Material GetSharedRingMaterial()
        {
            if (sharedRingMaterial == null)
                sharedRingMaterial = new Material(Shader.Find("Sprites/Default"));
            return sharedRingMaterial;
        }

        private void Awake()
        {
            CreateRingRenderer();
        }

        private void CreateRingRenderer()
        {
            GameObject ringGO = new GameObject("HighlightRing");
            ringGO.transform.SetParent(transform);
            ringGO.transform.localPosition = new Vector3(0f, 0.03f, 0f);

            ringRenderer = ringGO.AddComponent<LineRenderer>();
            ringRenderer.loop = true;
            ringRenderer.positionCount = segments;
            ringRenderer.startWidth = ringWidth;
            ringRenderer.endWidth = ringWidth;
            ringRenderer.useWorldSpace = false;

            // Material
            ringRenderer.material = GetSharedRingMaterial();
            ringRenderer.startColor = highlightColor;
            ringRenderer.endColor = highlightColor;
            ringRenderer.enabled = false;

            // Generate unit circle points (will be scaled)
            for (int i = 0; i < segments; i++)
            {
                float angle = (float)i / (float)segments * Mathf.PI * 2f;
                float x = Mathf.Cos(angle);
                float z = Mathf.Sin(angle);
                ringRenderer.SetPosition(i, new Vector3(x, 0f, z));
            }
        }

        /// <summary>
        /// Overrides the pulse geometry and timing for this instance.
        ///
        /// The serialized defaults are tuned to sit on a node for a move order.
        /// A caller that pulses this ring somewhere else -- under a finger, at
        /// a different camera distance -- needs different numbers, and setting
        /// them here keeps the node defaults untouched rather than making every
        /// user of NodeHighlight agree on one size.
        /// </summary>
        public void Configure(float start, float end, float duration)
        {
            startScale = start;
            endScale = end;
            pulseDuration = duration;
        }

        /// <summary>
        /// Trigger the highlight pulse. Call this when a move command targets this node.
        /// </summary>
        public void Pulse()
        {
            pulseTimer = pulseDuration;
            isPulsing = true;
            ringRenderer.enabled = true;
        }

        /// <summary>
        /// Pulse with a specific color (for player-specific highlights).
        /// </summary>
        public void Pulse(Color color)
        {
            highlightColor = color;
            ringRenderer.startColor = color;
            ringRenderer.endColor = color;
            Pulse();
        }

        private void Update()
        {
            if (!isPulsing) return;

            pulseTimer -= Time.deltaTime;

            if (pulseTimer <= 0f)
            {
                isPulsing = false;
                ringRenderer.enabled = false;
                return;
            }

            // Progress 0 (start) to 1 (end)
            float t = 1f - (pulseTimer / pulseDuration);

            // Scale up
            float currentScale = Mathf.Lerp(startScale, endScale, t);
            ringRenderer.transform.localScale = Vector3.one * currentScale;

            // Fade out
            float alpha = Mathf.Lerp(highlightColor.a, 0f, t);
            Color c = highlightColor;
            c.a = alpha;
            ringRenderer.startColor = c;
            ringRenderer.endColor = c;
        }
    }
}