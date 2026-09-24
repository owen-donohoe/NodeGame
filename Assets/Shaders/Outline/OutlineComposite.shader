// Turns group-ID boundaries in the mask into outline colour.
//
// For each pixel: decode the group ID underneath it, then measure the distance
// to the nearest pixel belonging to a different group by walking a tap table
// spread over a disc. That distance drives the alpha, so the edge is
// antialiased rather than stair-stepped.
//
// The distance is an estimate, and its error is a fraction of the line's width
// -- worst at an acute corner, where fewest taps land on the shape. So the
// radius is approximately, not exactly, constant around a silhouette, and sharp
// points are where that shows. Fixing it properly means a real distance field
// rather than a denser tap table: more taps shrink the error but never remove
// it, because it is a property of gathering rather than of the count.
//
// The line always sits *outside* the group it belongs to, so it never covers
// that group's own art and a sprite never loses its outermost pixels to its own
// outline. Where two groups meet, only the one nearer the camera may draw onto
// the other's pixels.
//
// That depth test is the whole point. Without it both groups draw at a shared
// seam, each painting its line into the other's pixels, and since the mask is
// depth-tested the pixels a line lands on usually belong to the object in
// *front* -- so the nearer object wears the further object's outline and looks
// like it has none of its own. Letting the nearer group win instead gives one
// line, on the correct side, reading as the front object overlapping the back
// one.
//
// Nearness comes packed into the mask's blue and alpha channels, so the depth
// comparison is free: it reads two channels of a texel the dilation loop has
// already fetched.
//
// Two groups closer together than the line is thick both have a claim on the
// pixels between them, and something has to settle it the same way every frame
// rather than flickering as the camera moves a fraction of a pixel.
//
// *The styles are layers, not competitors.* Every style that reaches a pixel
// keeps its own coverage there, and they are painted in OutlineStyle order --
// Hover, then Contested, then Selected, then CommandAck -- so a higher style
// lands in front of a lower one rather than in place of it. A Hover ring stays
// continuous underneath a Selected band; it is simply covered where the Selected
// line is opaque, and shows through where it is not.
//
// Picking a single winner per pixel instead is wrong in a way that is easy to
// miss: the faded outer edge of a Selected band would claim pixels it then
// barely paints, chewing a near-invisible bite out of the Hover ring it crossed.
//
// Depth still decides whether a group may draw on another group's pixels at
// all, which is the check above, and it answers a different question from
// layering. A hovered villager standing on a selected node is in front, so it
// earns the node's interior and its ring shows there; the node's own line is
// painted after it and so runs unbroken across the top.
//
// The walk still usually stops early. The tap table is sorted closest-first, so
// the first eligible tap of a style is the nearest one of that style, and
// _OutlineStylesPresent says which styles are on screen at all -- account for
// them and nothing later can add a layer. With one style showing, that is the
// first boundary found. Only a pixel with no boundary within reach pays for all
// sixteen taps, and OutlineCompositePass scissors most of those away before this
// shader ever runs on them.
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

            // The quality knob, and the worst-case cost knob. Every tap is a
            // texture sample, but the walk stops once every style on screen has
            // been accounted for, so this is the bill for a pixel with *no*
            // boundary in reach rather than the bill for an outlined one -- and
            // with a single style showing, that is the first boundary found.
            // Sixteen is the budget the design settled on for a 1-3px line;
            // raise it if the fade looks stepped, which is the first artefact to
            // appear, because the distance estimate can only take as many values
            // as there are taps.
            #define OUTLINE_TAP_COUNT 16
            #define OUTLINE_STYLE_COUNT 5

            // The four drawable styles map one-to-one onto the lanes of a float4
            // in the resolve below, which is what keeps every lane a constant
            // index. Add a member to OutlineStyle and that mapping silently runs
            // out of lanes, so fail the compile instead.
            #if OUTLINE_STYLE_COUNT != 5
                #error OutlineStyle changed size -- the float4 lanes in OutlineFragment no longer cover every drawable style.
            #endif

            float4 _OutlinePalette[OUTLINE_STYLE_COUNT];

            // One texel per mask ID: that group's own line colour, blended over
            // its style's palette colour by alpha. A texture rather than a
            // uniform array because 256 float4s is past what GLES 3.0 promises a
            // fragment shader. Texel 0 is never written, so a lane with no group
            // reads clear and keeps the palette.
            TEXTURE2D(_OutlineGroupTint);

            // .x is the style's line width as a fraction of the tap radius, so
            // Selected can read as a thin line while still winning every pixel
            // it contests.
            float4 _OutlineStyleRadius[OUTLINE_STYLE_COUNT];

            // Bitmask of the styles actually drawn this frame, 1u << styleValue,
            // matching OutlineStyleMask.Bit. The tap walk stops as soon as it has
            // accounted for all of them, because nothing later can add a layer.
            float _OutlineStylesPresent;

            float2 _OutlineTexelSize;
            float _OutlineTapRadius;

            // Fraction of the line's width spent fading out. Zero gives the
            // old hard, aliased edge.
            float _OutlineSoftness;

            // 0 normal, 1 fill the silhouettes, 2 paint a band. See
            // OutlineDebugView.
            float _OutlineDebugMode;

            // A Vogel disc: radius sqrt((i + 0.5) / N), angle i * the golden
            // angle. Two things follow from that, and both matter.
            //
            // The points are spread evenly by *area* across the disc rather
            // than sitting on rings, so the reach is very nearly the same in
            // every direction. The two-ring table this replaces reached the
            // full radius in only eight directions and half of it in the eight
            // between, dilating every silhouette into an eight-pointed star --
            // that, not the tap count, is what made corners look faceted.
            //
            // Every point also has a different radius, so sixteen taps give
            // sixteen distinct distance readings. That is what there is to
            // antialias with; a ring layout would have offered two.
            //
            // The entries are *sorted by radius*, ascending, and the loop
            // depends on it: that is what makes the first hit the nearest hit
            // and lets the walk break instead of finishing. Reordering this
            // table does not fail a compile or look obviously wrong -- it
            // quietly starts picking whichever boundary happens to come first
            // in the list. Keep it sorted.
            //
            // xy is the offset in units of the tap radius, z is its length,
            // precomputed so the loop needs no square root.
            static const float3 kOutlineTaps[OUTLINE_TAP_COUNT] =
            {
                float3( 0.176777,  0.000000, 0.176777),
                float3(-0.225772,  0.206826, 0.306186),
                float3( 0.034558, -0.393771, 0.395285),
                float3( 0.284571,  0.371173, 0.467707),
                float3(-0.522223, -0.092374, 0.530330),
                float3( 0.494695, -0.314685, 0.586302),
                float3(-0.165466,  0.615525, 0.637377),
                float3(-0.315561, -0.607594, 0.684653),
                float3( 0.684642,  0.250030, 0.728869),
                float3(-0.712256,  0.294009, 0.770552),
                float3( 0.343354, -0.733729, 0.810093),
                float3( 0.253730,  0.808932, 0.847791),
                float3(-0.764746, -0.443186, 0.883883),
                float3( 0.897134, -0.197232, 0.918559),
                float3(-0.547507,  0.778772, 0.951972),
                float3(-0.126487, -0.976090, 0.984251),
            };

            // An explicit round, never a raw float compare. The ID is an
            // identifier stored in an 8-bit UNorm channel, so 5 arrives as
            // 5/255 and only becomes 5 again by rounding. Comparing the floats
            // directly would work until it did not.
            uint DecodeId(float channel)
            {
                return (uint)round(channel * 255.0);
            }

            // The inverse of the mask's EncodeNearness. The high byte survives
            // the UNorm round trip exactly, so rounding it recovers the integer
            // and the low channel supplies the remainder.
            float DecodeNearness(float2 bits)
            {
                return (round(bits.x * 255.0) + bits.y) / 255.0;
            }

            /// Composites one style's line over what is already accumulated.
            ///
            /// A function rather than a macro on purpose: this file is stored
            /// with CRLF endings, and a multi-line macro would put a carriage
            /// return between the continuation backslash and the newline, which
            /// not every shader preprocessor forgives. The callers pass the
            /// palette and radius entries directly, so the array indices stay
            /// compile-time constants either way.
            ///
            /// <param name="accumulated">Premultiplied. Converted back to
            /// straight alpha by the caller, once.</param>
            void OutlineLayerOver(float4 colour, float radius, float distance,
                                  float fadeFraction, inout float4 accumulated)
            {
                // Past this style's own edge, so it contributes no coverage here
                // and whatever else reached the pixel keeps it.
                if (distance > radius) return;

                float r = max(radius, 1.0e-4);
                colour.a *= 1.0 - smoothstep(r - fadeFraction * r, r, distance);

                accumulated.rgb = colour.rgb * colour.a + accumulated.rgb * (1.0 - colour.a);
                accumulated.a   = colour.a             + accumulated.a   * (1.0 - colour.a);
            }

            /// The colour a line is drawn in: its style's palette entry, under
            /// the tint of the group the line belongs to. Only the colour moves.
            /// The palette's alpha is the style's opacity and is kept, so a
            /// tinted Selected line is exactly as solid as an untinted one.
            float4 TintedColour(float4 paletteColour, uint groupId)
            {
                float4 tint = LOAD_TEXTURE2D(_OutlineGroupTint, uint2(groupId, 0u));
                paletteColour.rgb = lerp(paletteColour.rgb, tint.rgb, tint.a);
                return paletteColour;
            }

            float4 OutlineFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord;

                // Paints regardless of the mask, so a blank screen here means
                // the pass never reached the colour target and the problem is
                // upstream of any dilation logic.
                //
                // Opaque, and only part of the screen. Alpha 1 removes the
                // blend unit from the set of things that could be swallowing
                // it, and stopping at 40% of the width leaves a hard vertical
                // edge with the scene beside it -- a full-screen fill is
                // indistinguishable from a camera clear colour, which is a
                // different bug.
                if (_OutlineDebugMode > 1.5)
                {
                    if (uv.x > 0.4) discard;
                    return float4(1.0, 0.0, 1.0, 1.0);
                }

                // sampler_PointClamp, from Blit.hlsl, is doing two jobs here.
                // Point, because bilinear would interpolate two IDs into a
                // third that belongs to no group and grow an outline around a
                // phantom. Clamp, because taps near the screen border would
                // otherwise wrap and read the far edge of the mask.
                float4 centre = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv);
                uint centreId = DecodeId(centre.r);

                if (_OutlineDebugMode > 0.5)
                {
                    if (centreId == 0u) discard;

                    // Group ID in red, style in green, both scaled to something
                    // the eye can actually distinguish.
                    return float4(saturate(centreId / 8.0),
                                  saturate(DecodeId(centre.g) / 4.0),
                                  0.0, 1.0);
                }

                float centreNearness = DecodeNearness(centre.ba);

                float2 step = _OutlineTapRadius * _OutlineTexelSize;

                // Distance to the nearest eligible boundary *per style*, in units
                // of the tap radius, starting past the furthest tap so that
                // "found nothing" stays distinct from "found one at the very
                // edge".
                //
                // One lane each, rather than a single winner, because the styles
                // are layers and not competitors: a pixel can carry a Hover line
                // with a Selected one over it, and collapsing that to whichever
                // ranked highest is what turned the faint outer edge of a
                // Selected band into a bite taken out of the Hover ring.
                //
                // A float4 rather than an array so no lane is ever addressed
                // dynamically -- an indexable temp is the one thing in this
                // shader that would be genuinely slow on a tile-based GPU.
                float4 styleDist = 1.0e6;

                // The group each lane's nearest boundary belongs to, for its
                // tint. Same lanes, same reason for not using an array.
                uint4 styleId = 0u;

                uint stylesPresent = (uint)_OutlineStylesPresent;
                uint stylesFound = 0u;

                // Deliberately not UNITY_UNROLL. The taps are sorted, so the
                // loop exits at the first hit, and an unrolled body turns that
                // exit into predication -- sixteen texture fetches issued
                // whatever the branches say, which is precisely the cost this
                // is trying not to pay on a tile-based GPU. A real loop with a
                // real break cannot be read that way.
                for (int i = 0; i < OUTLINE_TAP_COUNT; i++)
                {
                    float3 tap = kOutlineTaps[i];

                    float2 tapUv = uv + tap.xy * step;
                    float4 tapTexel =
                        SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, tapUv);

                    uint tapId = DecodeId(tapTexel.r);

                    // Same group, or empty space on both sides. Either way
                    // there is no boundary here. Dropping the same-group case
                    // is the whole point of group IDs: the seam where a node's
                    // quad meets its building sprites reaches this line and is
                    // discarded, so the union reads as one shape.
                    if (tapId == centreId) continue;

                    // Inside a group, looking at empty space. No line here: this
                    // edge is drawn by the empty pixels on the other side, which
                    // is what keeps every line outside the group it belongs to.
                    if (tapId == 0u) continue;

                    // Inside a group, looking at a different one. Only the
                    // nearer group draws, and the line lands on the further
                    // one's pixels.
                    if (centreId != 0u)
                    {
                        float tapNearness = DecodeNearness(tapTexel.ba);

                        if (tapNearness < centreNearness) continue;

                        // Equal depth, which two objects genuinely can reach.
                        // Broken by group ID so exactly one side of the seam
                        // draws: without a tiebreaker neither would, and the
                        // outline would vanish along that edge.
                        if (tapNearness == centreNearness && tapId > centreId) continue;
                    }

                    // Clamped at the decode, not at each use. This is an integer
                    // recovered from an 8-bit channel, so nothing but the writer's
                    // discipline keeps it in range, and an out-of-range value
                    // would shift a bit off the end of the mask below.
                    uint tapStyle = min(DecodeId(tapTexel.g), OUTLINE_STYLE_COUNT - 1);

                    // None is not a layer. A group holding this style never
                    // reaches the mask, so this is unreachable in practice -- but
                    // the lane chain below ends in an else, and a zero arriving
                    // there would silently land in CommandAck's lane.
                    if (tapStyle == 0u) continue;

                    uint tapBit = 1u << tapStyle;

                    // Already have this style's nearest. The table runs closest
                    // to furthest, so the first eligible tap of a style is the
                    // nearest one of that style and every later one is further.
                    // That holds for the radius rejection below too, which is
                    // why the style is marked found either way.
                    if ((stylesFound & tapBit) != 0u) continue;

                    stylesFound |= tapBit;

                    // Inside this style's own line width. A thinner style stops
                    // short of the tap radius, and a pixel past its edge simply
                    // carries no line of that style -- whatever else reaches the
                    // pixel still draws there.
                    if (tap.z <= _OutlineStyleRadius[tapStyle].x)
                    {
                        if (tapStyle == 1u)      { styleDist.x = tap.z; styleId.x = tapId; }
                        else if (tapStyle == 2u) { styleDist.y = tap.z; styleId.y = tapId; }
                        else if (tapStyle == 3u) { styleDist.z = tap.z; styleId.z = tapId; }
                        else                     { styleDist.w = tap.z; styleId.w = tapId; }
                    }

                    // Every style on screen has been accounted for, so no later
                    // tap can add anything. With a single style showing -- the
                    // common case -- this fires on the first boundary found and
                    // the walk costs what it did before layering existed.
                    if (stylesFound == stylesPresent) break;
                }

                float fadeFrac = max(_OutlineSoftness, 1.0e-4);

                // Accumulated premultiplied, converted back to straight alpha on
                // the way out, because the pass blends SrcAlpha/OneMinusSrcAlpha.
                // Compositing straight-alpha layers directly is the classic way
                // to get a colour that is subtly wrong wherever two of them
                // overlap at partial coverage -- which is exactly where these
                // layers meet.
                float4 accum = 0.0;

                // Painted lowest style to highest, each one over what is already
                // there, so Selected lands in front of Contested lands in front
                // of Hover. "In front of", not "instead of": a lower style still
                // shows wherever the one above it is absent or only partly
                // covering, which is what keeps a Hover ring continuous where a
                // Selected band's faded outer edge crosses it.
                //
                // Coverage is measured against each style's own radius, so a
                // thinner style fades out over its own edge rather than over a
                // boundary it never reaches.
                OutlineLayerOver(TintedColour(_OutlinePalette[1], styleId.x), _OutlineStyleRadius[1].x,
                                 styleDist.x, fadeFrac, accum);
                OutlineLayerOver(TintedColour(_OutlinePalette[2], styleId.y), _OutlineStyleRadius[2].x,
                                 styleDist.y, fadeFrac, accum);
                OutlineLayerOver(TintedColour(_OutlinePalette[3], styleId.z), _OutlineStyleRadius[3].x,
                                 styleDist.z, fadeFrac, accum);
                OutlineLayerOver(TintedColour(_OutlinePalette[4], styleId.w), _OutlineStyleRadius[4].x,
                                 styleDist.w, fadeFrac, accum);

                // No layer covered this pixel. Discard rather than returning a
                // transparent one, so the blend unit does no work for the vast
                // majority of the pixels inside the scissor.
                if (accum.a <= 1.0e-4) discard;

                return float4(accum.rgb / accum.a, accum.a);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
