// Turns group-ID boundaries in the mask into outline colour.
//
// For each pixel: decode the group ID underneath it, then walk outwards through
// a tap table. Where a tap lands on a *different*, non-zero group, that group's
// style picks the colour out of the palette and the loop returns immediately.
//
// Returning on the first hit is what makes the result stable. Two groups closer
// together than the outline is thick both have a claim on the pixel between
// them, and because the taps are ordered nearest-first the nearer one always
// wins -- deterministically, the same way every frame, instead of flickering
// between them as the camera moves a fraction of a pixel.
//
// A tap of ID 0 is skipped, so no line is drawn on the inside of a silhouette
// facing empty space. The outline therefore sits entirely outside each group,
// and there are no inner outlines.
Shader "NodeWar/Outline Composite"
{
    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Overlay"
        }

        Pass
        {
            Name "OutlineComposite"

            Cull Off
            ZWrite Off
            ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment OutlineFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // Runtime/Utilities, not ShaderLibrary. There is no Blit.hlsl under
            // the core package's ShaderLibrary folder, and getting this wrong
            // is close to undetectable: the include failure kills the vertex
            // program, but the shader still reports isSupported, a material is
            // still created, the pass still records and still executes, and the
            // draw simply produces no pixels. Every URP shader that uses Blit
            // includes it from this path -- copy it rather than guessing.
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            #define OUTLINE_TAP_COUNT 16
            #define OUTLINE_STYLE_COUNT 5

            float4 _OutlinePalette[OUTLINE_STYLE_COUNT];
            float2 _OutlineTexelSize;
            float _OutlineTapRadius;

            // 0 normal, 1 fill the silhouettes, 2 paint everything. See
            // OutlineDebugView.
            float _OutlineDebugMode;

            // Two rings of eight, inner ring first, so the table is already
            // sorted nearest-first and the loop can simply return on its first
            // hit. Sixteen taps is the budget the design settled on for a 1-3px
            // line: enough to fill the band without the cost of a proper
            // jump-flood, which only earns its keep for thick outlines.
            //
            // Offsets are in units of the tap radius and are multiplied by the
            // per-axis texel size, so a non-square mask stays circular rather
            // than stretching along one axis.
            static const float2 kOutlineTaps[OUTLINE_TAP_COUNT] =
            {
                float2( 0.50000,  0.00000),
                float2( 0.35355,  0.35355),
                float2( 0.00000,  0.50000),
                float2(-0.35355,  0.35355),
                float2(-0.50000,  0.00000),
                float2(-0.35355, -0.35355),
                float2( 0.00000, -0.50000),
                float2( 0.35355, -0.35355),

                float2( 0.92388,  0.38268),
                float2( 0.38268,  0.92388),
                float2(-0.38268,  0.92388),
                float2(-0.92388,  0.38268),
                float2(-0.92388, -0.38268),
                float2(-0.38268, -0.92388),
                float2( 0.38268, -0.92388),
                float2( 0.92388, -0.38268),
            };

            // An explicit round, never a raw float compare. The ID is an
            // identifier stored in an 8-bit UNorm channel, so 5 arrives as
            // 5/255 and only becomes 5 again by rounding. Comparing the floats
            // directly would work until it did not.
            uint DecodeId(float channel)
            {
                return (uint)round(channel * 255.0);
            }

            float4 OutlineFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord;

                // Paints regardless of the mask, so a blank screen here means
                // the pass never reached the colour target and the problem is
                // upstream of any dilation logic.
                //
                // Opaque, and only part of the screen. Alpha 1 removes the blend
                // unit from the set of things that could be swallowing it, and
                // stopping at 40% of the width leaves a hard vertical edge with
                // the scene beside it -- a full-screen fill is indistinguishable
                // from a camera clear colour, which is a different bug.
                if (_OutlineDebugMode > 1.5)
                {
                    if (uv.x > 0.4) discard;
                    return float4(1.0, 0.0, 1.0, 1.0);
                }

                if (_OutlineDebugMode > 0.5)
                {
                    float4 probe = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv);
                    if (DecodeId(probe.r) == 0u) discard;

                    // Group ID in red, style in green, both scaled to something
                    // the eye can actually distinguish.
                    return float4(saturate(DecodeId(probe.r) / 8.0),
                                  saturate(DecodeId(probe.g) / 4.0),
                                  0.0, 1.0);
                }

                // sampler_PointClamp, from Blit.hlsl, is doing two jobs here.
                // Point, because bilinear would interpolate two IDs into a third
                // that belongs to no group and grow an outline around a phantom.
                // Clamp, because taps near the screen border would otherwise
                // wrap and read the far edge of the mask.
                uint centreId = DecodeId(
                    SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv).r);

                float2 step = _OutlineTapRadius * _OutlineTexelSize;

                UNITY_UNROLL
                for (int i = 0; i < OUTLINE_TAP_COUNT; i++)
                {
                    float2 tapUv = uv + kOutlineTaps[i] * step;
                    float4 tap = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, tapUv);

                    uint tapId = DecodeId(tap.r);

                    // Empty space. Skipping it is what keeps the outline on the
                    // outside of a silhouette instead of also lining its inside.
                    if (tapId == 0u) continue;

                    // Same group. This is the whole point of group IDs: the seam
                    // where a node's quad meets its building sprites reaches this
                    // line and is discarded, so the union reads as one shape.
                    if (tapId == centreId) continue;

                    uint style = DecodeId(tap.g);
                    style = min(style, OUTLINE_STYLE_COUNT - 1);

                    return _OutlinePalette[style];
                }

                // No boundary within reach. Discard rather than returning a
                // transparent pixel, so the blend unit does no work for the vast
                // majority of the screen.
                discard;
                return float4(0.0, 0.0, 0.0, 0.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
