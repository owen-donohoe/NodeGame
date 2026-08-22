using UnityEngine;
using DG.Tweening;

namespace NodeWar.View
{
    /// <summary>
    /// Manages sprite orientation and entrance/exit animations for node prefabs.
    /// Finds all SpriteRenderers and MeshRenderers under gfxRoot, sets them to
    /// a uniform rotation (with per-instance offset), and provides tweened
    /// startup/breakdown animations.
    ///
    /// Startup: Scale Y 0 --> 1 with overshoot (spring up from bottom pivot).
    /// Breakdown: Scale Y 1 --> 0, slow-to-fast (collapse into ground).
    ///
    /// [ExecuteAlways] for editor orientation preview.
    /// </summary>
    [ExecuteAlways]
    public class NodePresentation : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Parent transform containing all sprite children. Auto-finds if null.")]
        [SerializeField] private Transform gfxRoot;

        [Header("Orientation")]
        [Tooltip("Base euler rotation applied to all sprites. Set to match camera angle.")]
        [SerializeField] private Vector3 spriteRotation = new Vector3(50f, 0f, 0f);
        [Tooltip("Per-instance offset added to base rotation.")]
        [SerializeField] private Vector3 rotationOffset;

        [Header("Startup Animation")]
        [SerializeField] private float startupDuration = 0.5f;
        [SerializeField] private float startupOvershoot = 1.6f;
        [SerializeField] private Ease startupEase = Ease.OutBack;

        [Header("Breakdown Animation")]
        [SerializeField] private float breakdownDuration = 0.35f;
        [SerializeField] private Ease breakdownEase = Ease.InCubic;

        // Discovered targets
        private Transform[] targets;
        private Transform[] meshTargets; // excluded from rotation, always reset to identity
        private Vector3[] baseScales;
        private bool isPresented;
        private Sequence activeSequence;

        private void OnEnable()
        {
            DiscoverTargets();
            ApplyOrientation();
        }

        // ===== ORIENTATION =====

        private void ApplyOrientation()
        {
            if (targets == null) return;

            Quaternion rotation = Quaternion.Euler(spriteRotation + rotationOffset);

            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i] == null) continue;
                targets[i].rotation = rotation;
            }

            if (meshTargets == null) return;
            for (int i = 0; i < meshTargets.Length; i++)
            {
                if (meshTargets[i] == null) continue;
                meshTargets[i].rotation = Quaternion.Euler(90f, 0f, 0f);
            }
        }

        // ===== STARTUP =====

        /// <summary>
        /// Sprites spring up from ground (Y scale 0 --> 1 with overshoot).
        /// </summary>
        public void PlayStartup(float delay = 0f)
        {
            if (targets == null) DiscoverTargets();

            activeSequence?.Kill();

            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i] == null) continue;
                Vector3 scale = baseScales[i];
                scale.y = 0f;
                targets[i].localScale = scale;
            }

            Sequence seq = DOTween.Sequence();

            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i] == null) continue;

                Transform t = targets[i];
                Vector3 targetScale = baseScales[i];

                float spriteDelay = delay + (i * 0.03f);

                seq.Insert(spriteDelay,
                    t.DOScaleY(targetScale.y, startupDuration)
                     .SetEase(startupEase, startupOvershoot));
            }

            seq.OnComplete(() => isPresented = true);
            activeSequence = seq;
        }

        // ===== BREAKDOWN =====

        /// <summary>
        /// Sprites collapse into ground (Y scale 1 --> 0, slow-to-fast).
        /// </summary>
        public void PlayBreakdown(float delay = 0f)
        {
            if (targets == null) return;

            activeSequence?.Kill();

            Sequence seq = DOTween.Sequence();

            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i] == null) continue;

                Transform t = targets[i];
                float spriteDelay = delay + (i * 0.02f);

                seq.Insert(spriteDelay,
                    t.DOScaleY(0f, breakdownDuration)
                     .SetEase(breakdownEase));
            }

            seq.OnComplete(() => isPresented = false);
            activeSequence = seq;
        }

        /// <summary>
        /// Immediately visible, no animation.
        /// </summary>
        public void SetPresented()
        {
            if (targets == null) DiscoverTargets();

            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i] == null) continue;
                targets[i].localScale = baseScales[i];
            }
            isPresented = true;
        }

        /// <summary>
        /// Immediately hidden, no animation.
        /// </summary>
        public void SetHidden()
        {
            if (targets == null) DiscoverTargets();

            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i] == null) continue;
                Vector3 scale = baseScales[i];
                scale.y = 0f;
                targets[i].localScale = scale;
            }
            isPresented = false;
        }

        public bool IsPresented => isPresented;

        // ===== TARGET DISCOVERY =====

        private void DiscoverTargets()
        {
            if (gfxRoot == null)
            {
                Transform found = transform.Find("GFX");
                if (found == null) found = transform.Find("gfx");
                if (found == null) found = transform.Find("Gfx");
                if (found == null && transform.childCount > 0)
                    found = transform.GetChild(0);
                gfxRoot = found;
            }

            if (gfxRoot == null)
            {
                targets = new Transform[0];
                baseScales = new Vector3[0];
                meshTargets = new Transform[0];
                return;
            }

            Renderer[] renderers = gfxRoot.GetComponentsInChildren<Renderer>(true);
            var validTargets = new System.Collections.Generic.List<Transform>();
            var meshList = new System.Collections.Generic.List<Transform>();

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] is SpriteRenderer)
                    validTargets.Add(renderers[i].transform);
                else if (renderers[i] is MeshRenderer)
                    meshList.Add(renderers[i].transform);
            }

            targets = validTargets.ToArray();
            baseScales = new Vector3[targets.Length];
            for (int i = 0; i < targets.Length; i++)
                baseScales[i] = targets[i].localScale;

            meshTargets = meshList.ToArray();
        }

        /// <summary>
        /// Update sprite facing AND rotate GFX root to flip spatial arrangement.
        /// Sprites face camera via world rotation; depth ordering flips via parent Y rotation.
        /// </summary>
        public void SetBaseRotation(Vector3 rotation)
        {
            spriteRotation = rotation;

            // Rotate gfxRoot on Y to flip spatial arrangement of children
            if (gfxRoot != null)
                gfxRoot.localRotation = Quaternion.Euler(0f, rotation.y, 0f);

            ApplyOrientation();
        }

        // ===== EDITOR SUPPORT =====

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Re-apply orientation when inspector values change
            if (targets == null || targets.Length == 0)
                DiscoverTargets();
            ApplyOrientation();
        }

        private void OnTransformChildrenChanged()
        {
            DiscoverTargets();
            ApplyOrientation();
        }

        // In editor (not playing), keep orientation applied in case
        // scene camera movement deselects and re-serializes transforms
        private void Update()
        {
            if (!Application.isPlaying)
                ApplyOrientation();
        }
#endif

        private void OnDisable()
        {
            activeSequence?.Kill();
        }

        private void OnDestroy()
        {
            activeSequence?.Kill();
        }
    }
}