using UnityEngine;
using DG.Tweening;

namespace NodeWar.View
{
    /// <summary>
    /// Manages sprite orientation and entrance/exit animations for node prefabs.
    /// Finds SpriteRenderers under gfxRoot and applies a uniform world rotation,
    /// then provides tweened startup (spring up) and breakdown (collapse) animations.
    /// 
    /// Also rotates the WorkPoints root on camera flip so slot positions mirror correctly.
    /// 
    /// Per-sprite rotation offsets are supported via SpriteOrientationOffset components
    /// on individual sprite GameObjects.
    /// 
    /// [ExecuteAlways] allows orientation preview in the Editor without entering play mode.
    /// </summary>
    [ExecuteAlways]
    public class NodePresentation : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Parent containing all sprite children. Auto-discovered if null.")]
        [SerializeField] private Transform gfxRoot;
        [Tooltip("Parent containing work slot transforms. Auto-discovered if null.")]
        [SerializeField] private Transform workPointsRoot;

        [Header("Orientation")]
        [Tooltip("Base euler rotation applied to all sprites. Should match camera viewing angle.")]
        [SerializeField] private Vector3 spriteRotation = new Vector3(50f, 0f, 0f);

        [Header("Startup Animation")]
        [Tooltip("Duration of the Y-scale spring from 0 to 1.")]
        [SerializeField] private float startupDuration = 0.5f;
        [Tooltip("Overshoot amount for the spring. Higher = more bounce.")]
        [SerializeField] private float startupOvershoot = 1.6f;
        [SerializeField] private Ease startupEase = Ease.OutBack;

        [Header("Breakdown Animation")]
        [Tooltip("Duration of the Y-scale collapse from 1 to 0.")]
        [SerializeField] private float breakdownDuration = 0.35f;
        [SerializeField] private Ease breakdownEase = Ease.InCubic;

        private Transform[] sprites;
        private Vector3[] baseScales;
        private bool isPresented;
        private Sequence activeSequence;

        private void OnEnable()
        {
            DiscoverTargets();
            ApplyOrientation();
        }

        // ===== ORIENTATION =====

        [ContextMenu("Apply Orientation")]
        private void ApplyOrientation()
        {
            if (sprites == null) return;

            Quaternion baseRotation = Quaternion.Euler(spriteRotation);

            for (int i = 0; i < sprites.Length; i++)
            {
                if (sprites[i] == null) continue;

                SpriteOrientationOffset perSpriteOffset = sprites[i].GetComponent<SpriteOrientationOffset>();
                if (perSpriteOffset != null)
                {
                    sprites[i].rotation = Quaternion.Euler(spriteRotation + perSpriteOffset.offset);
                }
                else
                {
                    sprites[i].rotation = baseRotation;
                }
            }
        }

        // ===== STARTUP =====

        /// <summary>
        /// Sprites spring up from ground (Y scale 0 to 1 with overshoot).
        /// Staggered slightly per sprite for a layered feel.
        /// </summary>
        public void PlayStartup(float delay = 0f)
        {
            if (sprites == null) DiscoverTargets();

            activeSequence?.Kill();

            for (int i = 0; i < sprites.Length; i++)
            {
                if (sprites[i] == null) continue;
                Vector3 scale = baseScales[i];
                scale.y = 0f;
                sprites[i].localScale = scale;
            }

            Sequence seq = DOTween.Sequence();

            for (int i = 0; i < sprites.Length; i++)
            {
                if (sprites[i] == null) continue;

                Transform t = sprites[i];
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
        /// Sprites collapse into ground (Y scale 1 to 0, slow-to-fast).
        /// </summary>
        public void PlayBreakdown(float delay = 0f)
        {
            if (sprites == null) return;

            activeSequence?.Kill();

            Sequence seq = DOTween.Sequence();

            for (int i = 0; i < sprites.Length; i++)
            {
                if (sprites[i] == null) continue;

                Transform t = sprites[i];
                float spriteDelay = delay + (i * 0.02f);

                seq.Insert(spriteDelay,
                    t.DOScaleY(0f, breakdownDuration)
                     .SetEase(breakdownEase));
            }

            seq.OnComplete(() => isPresented = false);
            activeSequence = seq;
        }

        public void SetPresented()
        {
            if (sprites == null) DiscoverTargets();

            for (int i = 0; i < sprites.Length; i++)
            {
                if (sprites[i] == null) continue;
                sprites[i].localScale = baseScales[i];
            }
            isPresented = true;
        }

        public void SetHidden()
        {
            if (sprites == null) DiscoverTargets();

            for (int i = 0; i < sprites.Length; i++)
            {
                if (sprites[i] == null) continue;
                Vector3 scale = baseScales[i];
                scale.y = 0f;
                sprites[i].localScale = scale;
            }
            isPresented = false;
        }

        /// <summary>
        /// Sets the Y rotation of the GFX root and WorkPoints root.
        /// The existing local X and Z rotations are preserved.
        /// </summary>
        public void RotateNode(float yRotation)
        {
            if (gfxRoot != null)
            {
                Vector3 rotation = gfxRoot.localEulerAngles;
                rotation.y = yRotation;
                gfxRoot.localEulerAngles = rotation;
            }

            if (workPointsRoot != null)
            {
                Vector3 rotation = workPointsRoot.localEulerAngles;
                rotation.y = yRotation;
                workPointsRoot.localEulerAngles = rotation;
            }
        }

        /// <summary>
        /// Sets the base sprite rotation using only the X and Z values provided.
        /// The Y value is intentionally ignored because Y is controlled separately
        /// by Flip().
        /// </summary>
        public void SetBaseSpriteRotation(Vector3 rotation)
        {
            spriteRotation = new Vector3(
                rotation.x,
                0f,
                rotation.z
            );

            ApplyOrientation();
        }
        
        // ===== TARGET DISCOVERY =====

        private void DiscoverTargets()
        {
            // Discover gfxRoot
            if (gfxRoot == null)
            {
                gfxRoot = transform.Find("GFX")
                    ?? transform.Find("gfx")
                    ?? transform.Find("Gfx");

                if (gfxRoot == null && transform.childCount > 0)
                    gfxRoot = transform.GetChild(0);
            }

            // Discover workPointsRoot
            if (workPointsRoot == null)
            {
                workPointsRoot = transform.Find("WorkPoints")
                    ?? transform.Find("Work Points")
                    ?? transform.Find("WORKPOINTS");
            }

            if (gfxRoot == null)
            {
                sprites = new Transform[0];
                baseScales = new Vector3[0];
                return;
            }

            Renderer[] renderers = gfxRoot.GetComponentsInChildren<Renderer>(true);

            var spriteList = new System.Collections.Generic.List<Transform>();

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] is SpriteRenderer)
                    spriteList.Add(renderers[i].transform);
            }

            sprites = spriteList.ToArray();
            baseScales = new Vector3[sprites.Length];
            for (int i = 0; i < sprites.Length; i++)
                baseScales[i] = sprites[i].localScale;
        }

        // ===== EDITOR =====

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (sprites == null || sprites.Length == 0)
                DiscoverTargets();
            ApplyOrientation();
        }

        private void OnTransformChildrenChanged()
        {
            DiscoverTargets();
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