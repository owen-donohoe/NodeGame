using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace NodeWar.View.Outline
{
    /// <summary>
    /// The group-silhouette outline system's entry point into URP.
    ///
    /// Two passes: a mask that draws the registered groups as IDs into an
    /// offscreen target, and a full-screen dilate that turns ID boundaries into
    /// outline colour. Only the mask exists so far.
    ///
    /// This is the project's first custom renderer feature, so it has to be
    /// added to both Assets/Settings/Mobile_Renderer.asset and
    /// Assets/Settings/PC_Renderer.asset -- Mobile has no feature list content
    /// at all today, and PC has only the built-in SSAO.
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

        private Material maskMaterial;
        private OutlineMaskPass maskPass;

        public override void Create()
        {
            if (maskPass == null) maskPass = new OutlineMaskPass();
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

            if (!TryPrepareMaterial()) return;

            maskPass.Setup(settings, maskMaterial);
            renderer.EnqueuePass(maskPass);
        }

        private bool TryPrepareMaterial()
        {
            if (maskMaterial != null) return true;
            if (maskShader == null) return false;
            if (!maskShader.isSupported) return false;

            maskMaterial = CoreUtils.CreateEngineMaterial(maskShader);
            return maskMaterial != null;
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(maskMaterial);
            maskMaterial = null;
            maskPass = null;
        }
    }
}
