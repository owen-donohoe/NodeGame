using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace NodeWar.View.Outline
{
    /// <summary>
    /// Carries the finished mask from the mask pass to the composite pass
    /// within one frame. A field on the feature would work until the day two
    /// cameras render in the same frame and the second one reads the first
    /// one's texture; frame data is per-camera and cannot do that.
    /// </summary>
    public sealed class OutlineMaskData : ContextItem
    {
        public TextureHandle mask;

        /// <summary>
        /// The mask's actual dimensions. Carried rather than recomputed by the
        /// composite, so the two passes cannot disagree about the size -- a
        /// disagreement would put the tap radius, and therefore the outline
        /// thickness, quietly out by the mask resolution factor.
        /// </summary>
        public int width;
        public int height;

        /// <summary>
        /// The screen rectangle the outlined groups cover, in camera-target
        /// pixels, before the outline's own width is added to it. The composite
        /// scissors to this rather than rasterising the whole screen.
        ///
        /// Carried for the same reason as the dimensions above: the mask pass
        /// already walks every renderer to build its draw list, so it can
        /// accumulate this for nothing, and a composite that recomputed it
        /// could disagree about which renderers counted.
        ///
        /// Already resolved to the full target when the projection could not be
        /// trusted, so the composite has one case to handle rather than two.
        /// </summary>
        public Rect groupBounds;

        /// <summary>
        /// The highest <see cref="OutlineStyle"/> value actually drawn this
        /// frame.
        ///
        /// The composite resolves a contested pixel by style priority, which in
        /// principle means scanning every tap rather than stopping at the first
        /// boundary. This is what gives the loop its exit back: once it has
        /// found the highest style present on screen, nothing further can
        /// outrank it. In the common case one style is showing, so the first
        /// hit is immediately unbeatable and the walk stops exactly where it
        /// used to.
        /// </summary>
        public int maxStyle;

        public override void Reset()
        {
            mask = TextureHandle.nullHandle;
            width = 0;
            height = 0;
            groupBounds = OutlineScreenBounds.Empty;
            maxStyle = 0;
        }
    }

    /// <summary>
    /// Draws the registered outline groups into an offscreen mask, writing a
    /// group ID and a style index instead of colour.
    ///
    /// This is the pass that makes the union silhouette possible. Because every
    /// renderer in a group writes one shared ID, the dilate pass downstream
    /// finds no edge along the interior seams, and the outline comes out as a
    /// single continuous line around the whole group.
    ///
    /// Renderers are selected individually rather than by layer mask. That is
    /// deliberate and it also avoids a trap: Assets/Shaders/SpriteShader.shader
    /// is a legacy built-in surface shader with no UniversalForward LightMode
    /// tag, so a shader-tag-filtered pass would have silently skipped anything
    /// using it.
    /// </summary>
    internal sealed class OutlineMaskPass : ScriptableRenderPass
    {
        private static readonly int ClipThresholdId = Shader.PropertyToID("_ClipThreshold");
        private static readonly int GroupIdId = Shader.PropertyToID("_OutlineGroupId");
        private static readonly int StyleIndexId = Shader.PropertyToID("_OutlineStyleIndex");

        private const int DepthTestedShaderPass = 0;
        private const int DrawThroughShaderPass = 1;

        /// <summary>
        /// One group's worth of drawing. Struct, and held in a list that is
        /// cleared rather than reallocated, because this is rebuilt every frame
        /// and the budget allows no per-frame allocation.
        /// </summary>
        private struct GroupDraw
        {
            public float id01;
            public float style01;
            public int shaderPass;
            public Renderer[] renderers;
        }

        private sealed class PassData
        {
            public Material maskMaterial;
            public List<GroupDraw> draws;
        }

        // Cached so the method-group conversion does not allocate a delegate
        // every frame. Unity's C# version does not cache static method groups.
        private static readonly BaseRenderFunc<PassData, RasterGraphContext> ExecuteFunc = Execute;

        private readonly List<GroupDraw> draws = new List<GroupDraw>(32);

        // Accumulated alongside the draw list, in camera-target pixels.
        private Rect boundsUnion;

        // Highest style value among the groups actually added to the draw list.
        private int maxStyleDrawn;

        // Latched when a projection could not be trusted -- a group straddling
        // the near plane. Once set, the union is abandoned and the composite
        // gets the whole screen, because a partly-wrong rect is worse than no
        // rect: it would clip an outline rather than merely fail to tighten it.
        private bool boundsUnreliable;

        private OutlineSettings settings;
        private Material maskMaterial;

        public OutlineMaskPass()
        {
            // The mask has its own depth and does not read the scene's, so this
            // only has to land before the composite. After transparents keeps it
            // next to the geometry it mirrors in the frame debugger.
            renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
        }

        public void Setup(OutlineSettings outlineSettings, Material material)
        {
            settings = outlineSettings;
            maskMaterial = material;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (settings == null || maskMaterial == null) return;

            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

            RenderTextureDescriptor colorDesc = cameraData.cameraTargetDescriptor;

            // The plain projection, not GetGPUProjectionMatrix: this is a CPU
            // projection into bottom-left pixel coordinates, which is what the
            // scissor wants, and the GPU variant carries a platform-specific
            // y flip and depth range that would put the rect upside down on
            // half the platforms and nowhere on the other half.
            Matrix4x4 viewProjection =
                cameraData.GetProjectionMatrix() * cameraData.GetViewMatrix();

            BuildDrawList(viewProjection, colorDesc.width, colorDesc.height);

            // Nothing outlined, or everything outlined has an empty renderer
            // list. Add no pass at all: no render target is allocated and no
            // draw is recorded. This is the common case in normal play and it
            // has to cost nothing.
            if (draws.Count == 0) return;

            // Every group is off screen. Previously this still allocated two
            // full-screen attachments and drew into them, then composited
            // nothing -- the draw list was non-empty, which was the only
            // question asked.
            if (!boundsUnreliable && OutlineScreenBounds.IsEmpty(boundsUnion)) return;

            GetMaskSize(colorDesc.width, colorDesc.height, out int width, out int height);

            colorDesc.width = width;
            colorDesc.height = height;
            colorDesc.depthBufferBits = 0;
            colorDesc.useMipMap = false;
            colorDesc.autoGenerateMips = false;
            colorDesc.bindMS = false;

            // No MSAA: resolving would average two IDs into a third that belongs
            // to neither group, and the dilate pass would draw an outline around
            // the phantom.
            colorDesc.msaaSamples = 1;

            // UNorm, and explicitly not the sRGB variant. This project renders in
            // Linear colour space, and an sRGB target would apply a transfer
            // curve to values that are identifiers rather than colours -- the
            // round-trip through round(r * 255) would then land on the wrong ID.
            colorDesc.graphicsFormat = GraphicsFormat.R8G8B8A8_UNorm;

            // Chosen over an integer target for mobile: Android is configured for
            // Vulkan *and* OpenGLES3, and GLES3 is a real shipping path here
            // rather than a fallback.
            RenderTextureDescriptor depthDesc = colorDesc;
            depthDesc.graphicsFormat = GraphicsFormat.None;
            depthDesc.depthStencilFormat = GraphicsFormat.D24_UNorm_S8_UInt;

            // Its own depth attachment, at exactly the mask's dimensions. The
            // camera depth texture cannot be reused: it is the wrong size
            // whenever the mask is not full resolution, and it would also make
            // unoutlined scenery occlude outlines, which is a different
            // behaviour from the one chosen.
            TextureHandle maskColor = UniversalRenderer.CreateRenderGraphTexture(
                renderGraph, colorDesc, "_OutlineMask", true);
            TextureHandle maskDepth = UniversalRenderer.CreateRenderGraphTexture(
                renderGraph, depthDesc, "_OutlineMaskDepth", true);

            OutlineMaskData maskData = frameData.GetOrCreate<OutlineMaskData>();
            maskData.mask = maskColor;
            maskData.width = width;
            maskData.height = height;
            maskData.groupBounds = boundsUnreliable
                ? OutlineScreenBounds.FullTarget(colorDesc.width, colorDesc.height)
                : boundsUnion;
            maskData.maxStyle = maxStyleDrawn;

            maskMaterial.SetFloat(ClipThresholdId, settings.AlphaClipThreshold);

            using (IRasterRenderGraphBuilder builder =
                   renderGraph.AddRasterRenderPass<PassData>("NodeWar Outline Mask", out PassData passData))
            {
                passData.maskMaterial = maskMaterial;
                passData.draws = draws;

                builder.SetRenderAttachment(maskColor, 0, AccessFlags.Write);
                builder.SetRenderAttachmentDepth(maskDepth, AccessFlags.ReadWrite);

                // Required for the per-group SetGlobalFloat calls. It also turns
                // off pass culling, which matters while nothing downstream
                // consumes the mask yet -- otherwise the graph would drop the
                // pass and the frame debugger would show nothing.
                builder.AllowGlobalStateModification(true);

                builder.SetRenderFunc(ExecuteFunc);
            }
        }

        /// <summary>
        /// Snapshots the registry into a flat draw list, depth-tested groups
        /// first and draw-through groups after.
        ///
        /// The ordering is load-bearing, not cosmetic. A draw-through group
        /// renders with ZTest Always, so it overwrites IDs already in the mask;
        /// interleaved with depth-tested groups it would punch holes in the ones
        /// in front of it.
        /// </summary>
        private void BuildDrawList(Matrix4x4 viewProjection, int targetWidth, int targetHeight)
        {
            draws.Clear();
            boundsUnion = OutlineScreenBounds.Empty;
            boundsUnreliable = false;
            maxStyleDrawn = 0;

            OutlineRegistry registry = OutlineRegistry.Instance;
            int count = registry.ActiveCount;

            for (int i = 0; i < count; i++)
            {
                IOutlineGroup group = registry.GetActive(i);
                if (group == null) continue;
                if (settings.GetStyle(group.Style).drawThrough) continue;

                AddGroup(group, DepthTestedShaderPass, viewProjection, targetWidth, targetHeight);
            }

            for (int i = 0; i < count; i++)
            {
                IOutlineGroup group = registry.GetActive(i);
                if (group == null) continue;
                if (!settings.GetStyle(group.Style).drawThrough) continue;

                AddGroup(group, DrawThroughShaderPass, viewProjection, targetWidth, targetHeight);
            }
        }

        /// <summary>
        /// Whether a renderer will actually produce pixels this frame.
        ///
        /// Shared between the bounds accumulation here and the draw loop in
        /// <see cref="Execute"/> on purpose. The two must agree: a rect built
        /// from a set larger than the one drawn is merely loose, but a rect
        /// built from a smaller one clips a real outline, and that is the sort
        /// of divergence that only shows up on the one frame a villager dies.
        /// </summary>
        private static bool IsDrawable(Renderer renderer)
        {
            // Nothing in this project destroys nodes or villagers. A dead
            // villager is a live GameObject whose SpriteRenderers were disabled
            // by VillagerView.ApplyVisualState, so this is the check that stops
            // it keeping its outline for the rest of the match.
            if (renderer == null) return false;
            if (!renderer.enabled) return false;
            if (!renderer.gameObject.activeInHierarchy) return false;

            return true;
        }

        private void AddGroup(IOutlineGroup group, int shaderPass,
                              Matrix4x4 viewProjection, int targetWidth, int targetHeight)
        {
            Renderer[] groupRenderers = group.Renderers;
            if (groupRenderers == null || groupRenderers.Length == 0) return;

            // Accumulated before the group is added, so a group whose renderers
            // are all disabled adds no draw at all rather than an empty one.
            int drawable = 0;

            for (int i = 0; i < groupRenderers.Length; i++)
            {
                Renderer renderer = groupRenderers[i];
                if (!IsDrawable(renderer)) continue;

                drawable++;

                // Already given up. Keep counting drawables -- whether the group
                // draws at all is a separate question from whether its rect can
                // be trusted -- but stop paying for projections nobody will read.
                if (boundsUnreliable) continue;

                if (!OutlineScreenBounds.TryProject(viewProjection, renderer.bounds,
                                                    targetWidth, targetHeight, ref boundsUnion))
                {
                    boundsUnreliable = true;
                }
            }

            if (drawable == 0) return;

            if ((int)group.Style > maxStyleDrawn) maxStyleDrawn = (int)group.Style;

            draws.Add(new GroupDraw
            {
                // The mask stores both in one 8-bit UNorm channel each, decoded
                // with an explicit round on the far side.
                id01 = group.OutlineId / 255f,
                style01 = (int)group.Style / 255f,
                shaderPass = shaderPass,
                renderers = groupRenderers,
            });
        }

        private void GetMaskSize(int cameraWidth, int cameraHeight, out int width, out int height)
        {
            if (settings.MaskResolution == OutlineMaskResolution.Half)
            {
                width = Mathf.Max(1, cameraWidth / 2);
                height = Mathf.Max(1, cameraHeight / 2);
                return;
            }

            width = Mathf.Max(1, cameraWidth);
            height = Mathf.Max(1, cameraHeight);
        }

        private static void Execute(PassData data, RasterGraphContext context)
        {
            RasterCommandBuffer cmd = context.cmd;
            List<GroupDraw> groupDraws = data.draws;

            for (int i = 0; i < groupDraws.Count; i++)
            {
                GroupDraw draw = groupDraws[i];

                // Set once per group, not per renderer: this is what makes every
                // renderer in the group share an ID and the interior seams
                // disappear.
                cmd.SetGlobalFloat(GroupIdId, draw.id01);
                cmd.SetGlobalFloat(StyleIndexId, draw.style01);

                Renderer[] groupRenderers = draw.renderers;
                for (int r = 0; r < groupRenderers.Length; r++)
                {
                    Renderer renderer = groupRenderers[r];

                    // Re-checked rather than trusted from record time. The
                    // record-time pass over the same filter is what sized the
                    // scissor; this one is what keeps a renderer disabled in
                    // between out of the mask.
                    if (!IsDrawable(renderer)) continue;

                    cmd.DrawRenderer(renderer, data.maskMaterial, 0, draw.shaderPass);
                }
            }
        }
    }
}
