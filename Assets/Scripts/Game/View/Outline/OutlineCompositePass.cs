using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace NodeWar.View.Outline
{
    /// <summary>
    /// Dilates the group-ID mask into visible outlines and blends them over the
    /// camera colour.
    ///
    /// One full-screen pass for every outlined group on screen, not one per
    /// group. That is the property that makes the whole approach affordable on a
    /// phone, and it is why thickness lands in screen pixels rather than world
    /// units -- the line stays readable at maximum zoom-out, which a
    /// world-space outline on a dolly-zoom camera does not.
    /// </summary>
    internal sealed class OutlineCompositePass : ScriptableRenderPass
    {
        private static readonly int PaletteId = Shader.PropertyToID("_OutlinePalette");
        private static readonly int TexelSizeId = Shader.PropertyToID("_OutlineTexelSize");
        private static readonly int TapRadiusId = Shader.PropertyToID("_OutlineTapRadius");
        private static readonly int SoftnessId = Shader.PropertyToID("_OutlineSoftness");
        private static readonly int DebugModeId = Shader.PropertyToID("_OutlineDebugMode");

        private sealed class PassData
        {
            public Material material;
            public TextureHandle mask;

            /// <summary>
            /// Whether this frame's execution should report itself. Carried on
            /// the pass data because the render function is static -- it cannot
            /// see the settings asset, and capturing it would allocate.
            /// </summary>
            public bool trace;
        }

        private static readonly BaseRenderFunc<PassData, RasterGraphContext> ExecuteFunc = Execute;

        // Rebuilt each frame from the settings asset, but never reallocated.
        private readonly Vector4[] palette = new Vector4[OutlineStyleMask.StyleCount];

        private OutlineSettings settings;
        private Material compositeMaterial;

        public void Setup(OutlineSettings outlineSettings, Material material)
        {
            settings = outlineSettings;
            compositeMaterial = material;

            // The pass blends into the colour target rather than replacing it,
            // so it needs a real intermediate texture to blend *with*. Without
            // it URP is free to send post-processing straight to the backbuffer,
            // and an after-post pass then has nothing well-defined to
            // read-modify-write.
            requiresIntermediateTexture = true;

            renderPassEvent = settings.InjectionPoint == OutlineInjectionPoint.AfterPostProcessing
                // The default, and it matters more here than it might elsewhere:
                // Gameplay.unity's Global Volume runs Bloom at 1.54 and Bokeh
                // depth of field, so an outline composited before post would be
                // both bloomed and depth-blurred.
                ? RenderPassEvent.AfterRenderingPostProcessing
                : RenderPassEvent.BeforeRenderingPostProcessing;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            ResetTracingIfDebugViewChanged();

            if (enterTraced < TraceBudget)
            {
                enterTraced++;
                Trace($"composite RecordRenderGraph called, renderPassEvent={renderPassEvent} " +
                      $"({(int)renderPassEvent})");
            }

            if (settings == null || compositeMaterial == null)
            {
                Bail("no settings or composite material");
                return;
            }

            // No mask means the mask pass skipped itself because nothing is
            // outlined. Skip in step -- this is the common case in normal play.
            if (!frameData.Contains<OutlineMaskData>())
            {
                Bail("frame data has no OutlineMaskData -- the mask pass did not record");
                return;
            }

            OutlineMaskData maskData = frameData.Get<OutlineMaskData>();
            if (!maskData.mask.IsValid())
            {
                Bail("mask texture handle is invalid");
                return;
            }

            if (maskData.height <= 0)
            {
                Bail("mask height is zero");
                return;
            }

            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            if (!resourceData.activeColorTexture.IsValid())
            {
                Bail("activeColorTexture is invalid at this injection point");
                return;
            }

            UploadPalette();

            compositeMaterial.SetVector(TexelSizeId, new Vector4(
                1f / maskData.width, 1f / maskData.height, maskData.width, maskData.height));

            // Thickness is authored in pixels at a reference height and scaled by
            // the mask's actual height. That holds a constant fraction of screen
            // height, so the line neither thins when Mobile_RPAsset's 0.8 render
            // scale kicks in, nor shrinks on a denser screen, nor changes when
            // the mask is dropped to half resolution.
            float tapRadius = settings.ThicknessReferencePixels *
                              (maskData.height / settings.ReferenceHeight);
            compositeMaterial.SetFloat(TapRadiusId, Mathf.Max(0.5f, tapRadius));
            compositeMaterial.SetFloat(SoftnessId, settings.EdgeSoftness);
            compositeMaterial.SetFloat(DebugModeId, (float)settings.DebugView);

            using (IRasterRenderGraphBuilder builder =
                   renderGraph.AddRasterRenderPass<PassData>("NodeWar Outline Composite", out PassData passData))
            {
                passData.material = compositeMaterial;
                passData.mask = maskData.mask;
                passData.trace = settings.DebugView != OutlineDebugView.Off && executeTraced < TraceBudget;

                // Reported at record time as well as inside Execute, because the
                // failure worth catching is a pass that records and then never
                // executes. That is only visible as a "recorded" line with no
                // "EXECUTING" line following it -- neither message says it on
                // its own.
                if (passData.trace)
                {
                    Trace($"composite recorded. isActiveTargetBackBuffer=" +
                          $"{resourceData.isActiveTargetBackBuffer}, " +
                          $"debugMode={(int)settings.DebugView}, " +
                          $"mask={maskData.width}x{maskData.height}.");
                }

                builder.UseTexture(maskData.mask, AccessFlags.Read);

                // ReadWrite, not Write: the outline is alpha-blended over what
                // is already there, so the attachment has to be loaded rather
                // than discarded.
                builder.SetRenderAttachment(resourceData.activeColorTexture, 0, AccessFlags.ReadWrite);

                // The pass writes the camera colour, which the final blit reads,
                // so the graph should keep it on those grounds alone. Said
                // explicitly anyway: a culled pass and a pass that draws nothing
                // look identical from the outside, and ruling one out is worth
                // more than the optimisation it gives up.
                builder.AllowPassCulling(false);

                builder.SetRenderFunc(ExecuteFunc);
            }
        }

        /// <summary>
        /// Copies the palette into the reusable array, converting to linear.
        ///
        /// The conversion is not optional. This project renders in Linear colour
        /// space, and SetVectorArray -- unlike SetColor -- performs no gamma
        /// conversion of its own, so an sRGB-authored colour would arrive in the
        /// shader too bright. Alpha is a coverage value and is never converted.
        /// </summary>
        private void UploadPalette()
        {
            bool linear = QualitySettings.activeColorSpace == ColorSpace.Linear;

            for (int i = 0; i < palette.Length; i++)
            {
                Color color = settings.GetStyle((OutlineStyle)i).color;
                if (linear)
                {
                    Color converted = color.linear;
                    converted.a = color.a;
                    color = converted;
                }

                palette[i] = color;
            }

            compositeMaterial.SetVectorArray(PaletteId, palette);
        }

        /// <summary>
        /// Reports why the composite declined to record, but only while a debug
        /// view is on. A pass that silently does nothing and a pass that is
        /// never recorded look the same on screen, and this is the cheapest way
        /// to tell them apart without reading the render graph.
        /// </summary>
        private void Bail(string reason)
        {
            if (bailTraced >= TraceBudget) return;

            bailTraced++;
            Trace($"composite skipped: {reason}");
        }

        // Capped rather than throttled by time, so a diagnostic run produces a
        // handful of lines to read instead of a console that has to be cleared.
        private const int TraceBudget = 4;

        // Three budgets, one per question, rather than one shared between them.
        // The shared counter this replaces was spent by whichever message fired
        // most often, and that was always the "recording" line, which runs every
        // frame -- so within four frames the budget was gone and the execute
        // line, the only one that proves a draw was actually issued, could never
        // print. A diagnostic that silences its own most important message is
        // worse than no diagnostic, because it reads as evidence of nothing
        // happening.
        private int enterTraced;
        private int bailTraced;

        // Static because Execute has to be static to avoid allocating a
        // delegate per frame.
        private static int executeTraced;

        private OutlineDebugView lastDebugView = OutlineDebugView.Off;

        /// <summary>
        /// Rearms the budgets when the debug view is changed. Without this, a
        /// setting flipped in the Inspector during play gives silence rather
        /// than lines, because the budget was already spent under the previous
        /// setting.
        /// </summary>
        private void ResetTracingIfDebugViewChanged()
        {
            if (settings == null || settings.DebugView == lastDebugView) return;

            lastDebugView = settings.DebugView;
            enterTraced = 0;
            bailTraced = 0;
            executeTraced = 0;
        }

        private void Trace(string message)
        {
            if (settings == null || settings.DebugView == OutlineDebugView.Off) return;

            Debug.LogWarning($"[Outline] {message}");
        }

        private static readonly MaterialPropertyBlock SharedProperties = new MaterialPropertyBlock();
        private static readonly int BlitTextureId = Shader.PropertyToID("_BlitTexture");
        private static readonly int BlitScaleBiasId = Shader.PropertyToID("_BlitScaleBias");

        private static void Execute(PassData data, RasterGraphContext context)
        {
            // Draws the fullscreen triangle directly rather than going through
            // Blitter.BlitTexture. This mirrors URP's own
            // FullScreenPassRendererFeature, and the reason it does matters: a
            // TextureHandle resolves to an RTHandle that can be null, and
            // Blitter given a null source binds nothing and silently draws
            // nothing -- a pass that records, is not culled, and produces no
            // pixels. URP null-checks it for the same reason.
            //
            // The camera colour is never sampled. The outline reaches it through
            // the blend unit, so there is no copy-colour step.
            RTHandle mask = data.mask;

            if (data.trace)
            {
                executeTraced++;
                Debug.LogWarning(
                    $"[Outline] composite EXECUTING. mask null? {mask == null}. " +
                    $"material '{(data.material == null ? "null" : data.material.shader.name)}'.");
            }

            SharedProperties.Clear();
            if (mask != null) SharedProperties.SetTexture(BlitTextureId, mask);
            SharedProperties.SetVector(BlitScaleBiasId, new Vector4(1f, 1f, 0f, 0f));

            context.cmd.DrawProcedural(
                Matrix4x4.identity, data.material, 0, MeshTopology.Triangles, 3, 1, SharedProperties);
        }
    }
}
