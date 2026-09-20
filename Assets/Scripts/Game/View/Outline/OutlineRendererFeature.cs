using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace NodeWar.View.Outline
{
    /// <summary>
    /// The group-silhouette outline system's entry point into URP.
    ///
    /// Two passes, both live: a mask that draws the registered groups as IDs
    /// into an offscreen target, and a full-screen dilate that turns ID
    /// boundaries into outline colour.
    ///
    /// This is the project's first custom renderer feature, and it is wired
    /// into both Assets/Settings/Mobile_Renderer.asset and
    /// Assets/Settings/PC_Renderer.asset -- the outline is Mobile's only
    /// feature, and sits alongside the built-in SSAO on PC. Both entries are
    /// needed: a renderer asset that does not list it renders no outlines at
    /// all, silently, and which asset is in play is a quality-setting value.
    /// Any renderer added later needs the same entry.
    /// </summary>
    [DisallowMultipleRendererFeature("NodeWar Outline")]
    public sealed class OutlineRendererFeature : ScriptableRendererFeature
    {
        [Tooltip("Palette, thickness, mask resolution, clip threshold and " +
                 "occlusion. Without it the feature does nothing.")]
        [SerializeField] private OutlineSettings settings;

        [Tooltip("NodeWar/Outline Mask. Serialized rather than found by name so " +
                 "it survives a build -- Shader.Find only sees shaders something " +
                 "already references.")]
        [SerializeField] private Shader maskShader;

        [Tooltip("NodeWar/Outline Composite.")]
        [SerializeField] private Shader compositeShader;

        private Material maskMaterial;
        private Material compositeMaterial;
        private OutlineMaskPass maskPass;
        private OutlineCompositePass compositePass;

        public override void Create()
        {
            if (maskPass == null) maskPass = new OutlineMaskPass();
            if (compositePass == null) compositePass = new OutlineCompositePass();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (settings == null) return;

            ref CameraData cameraData = ref renderingData.cameraData;

            // Game and Scene View only. Preview cameras (material and prefab
            // thumbnails) and reflection probes must not run this -- they render
            // objects that are in no group, at sizes the thickness maths was
            // never meant for. Scene View is allowed on purpose, so outlines are
            // visible while authoring, and must not throw there.
            if (cameraData.cameraType != CameraType.Game &&
                cameraData.cameraType != CameraType.SceneView)
            {
                return;
            }

            // Base cameras only. No camera stacking is used today -- both
            // Gameplay.unity and Lobby.unity have a single Base camera with an
            // empty stack -- but if an Overlay camera is ever added, letting it
            // run this would composite the outline a second time onto the shared
            // target.
            if (cameraData.renderType != CameraRenderType.Base) return;

            // The cheap early out, and the common case in normal play: nothing
            // is outlined, so no pass is enqueued, no render target is
            // allocated, and no draw call is added.
            if (!OutlineRegistry.Instance.HasWork) return;

            if (!TryPrepareMaterials()) return;

            maskPass.Setup(settings, maskMaterial);
            renderer.EnqueuePass(maskPass);

            // Enqueued unconditionally alongside the mask. It finds no mask in
            // frame data and skips itself if the mask pass decided there was
            // nothing to draw, which keeps the "is there work?" decision in one
            // place instead of two that can disagree.
            compositePass.Setup(settings, compositeMaterial);
            renderer.EnqueuePass(compositePass);

            Trace(cameraData.camera, renderer);
        }

        // Capped so a diagnostic run is readable. Reports what was enqueued and
        // onto which camera and renderer, which is the half of the chain the
        // pass itself cannot see.
        private int traced;

        private void Trace(Camera camera, ScriptableRenderer renderer)
        {
            if (settings.DebugView == OutlineDebugView.Off) return;
            if (traced >= 4) return;

            traced++;
            Debug.LogWarning(
                $"[Outline] enqueued both passes on camera '{camera.name}' " +
                $"({camera.cameraType}), renderer {renderer.GetType().Name}, " +
                $"groups={OutlineRegistry.Instance.ActiveCount}, " +
                $"injection={settings.InjectionPoint}");
        }

        private bool TryPrepareMaterials()
        {
            maskMaterial = EnsureMaterial(maskMaterial, maskShader);
            compositeMaterial = EnsureMaterial(compositeMaterial, compositeShader);

            return maskMaterial != null && compositeMaterial != null;
        }

        private static Material EnsureMaterial(Material existing, Shader shader)
        {
            if (existing != null && existing.shader == shader) return existing;

            // The serialized shader can change while the feature is alive.
            // Release the old material before replacing it or clearing the slot.
            CoreUtils.Destroy(existing);
            if (shader == null || !shader.isSupported) return null;

            return CoreUtils.CreateEngineMaterial(shader);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(maskMaterial);
            CoreUtils.Destroy(compositeMaterial);
            maskMaterial = null;
            compositeMaterial = null;
            maskPass = null;
            compositePass = null;
        }
    }
}
