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
// *Style priority settles it*, not distance. Selected outranks Contested
// outranks Hover -- the same OutlineStyle order that resolves several intents
// on one group. Distance only breaks ties within one style.
//
// Depth still decides whether a group may draw on another group's pixels at
// all, which is the check above. The two rules answer different questions, and
// that separation is the point: a hovered villager standing on a selected node
// is in front, so it earns the node's interior and its ring shows there, while
// the node keeps every empty pixel along its own edge and its line runs
// unbroken instead of changing colour wherever the villager's ring crosses it.
// Priority alone would erase the villager; depth alone breaks the node's line.
//
// The walk still usually stops early. The tap table is sorted closest-first, so
// the first tap of a style is the nearest one of that style, and _OutlineMaxStyle
// says what the best possible answer on screen is -- reach it and nothing later
// can win. With one style showing, that is the first boundary found. Only a
// pixel with no boundary within reach pays for all sixteen taps, and
// OutlineCompositePass scissors most of those away before this shader runs.
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
            // texture sample, but the loop breaks at the first hit, so this is
            // the bill for a pixel with *no* boundary in reach rather than the
            // bill for an outlined one -- a pixel on the line itself typically
            // exits within a few taps. Sixteen is the budget the design settled
            // on for a 1-3px line; raise it if the fade looks stepped, which is
            // the first artefact to appear, because the distance estimate can
            // only take as many values as there are taps.
            #define OUTLINE_TAP_COUNT 16
            #define OUTLINE_STYLE_COUNT 5

            float4 _OutlinePalette[OUTLINE_STYLE_COUNT];

            // .x is the style's line width as a fraction of the tap radius, so
            // Selected can read as a thin line while still winning every pixel
            // it contests.
            float4 _OutlineStyleRadius[OUTLINE_STYLE_COUNT];

            // The highest style on screen this frame. The loop stops as soon as
            // it matches this, because nothing left can outrank it.
            float _OutlineMaxStyle;

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

                // Distance to the nearest differing pixel, in units of the tap
                // radius. Starts past the furthest tap so that "found nothing"
                // and "found one at the very edge" stay distinguishable.
                // Distance to the winning boundary, its style, and that style's
                // radius. Style leads the comparison, distance only breaks ties
                // within a style -- see the loop.
                float nearest = 1.0e6;
                uint nearestStyle = 0u;
                float nearestRadius = 1.0;

                uint maxStyle = (uint)_OutlineMaxStyle;

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

                    uint tapStyle = DecodeId(tapTexel.g);
                    float tapRadius =
                        _OutlineStyleRadius[min(tapStyle, OUTLINE_STYLE_COUNT - 1)].x;

                    // Outside this style's own line width. A thinner style stops
                    // short rather than reaching the full tap radius, and the
                    // pixel stays available to whatever else can claim it.
                    if (tap.z > tapRadius) continue;

                    // Style decides, not distance. This is what keeps a Selected
                    // line unbroken where a Hover line crosses it: both groups
                    // have a claim on the empty pixels between them, and handing
                    // those to whichever happens to be nearer is what made the
                    // higher-priority line change colour mid-run.
                    //
                    // Strictly greater, never equal: the table runs closest to
                    // furthest, so the first tap of any given style is already
                    // the nearest one of that style, and a later tap of the same
                    // style can only be further away.
                    if (tapStyle <= nearestStyle) continue;

                    nearest = tap.z;
                    nearestStyle = tapStyle;
                    nearestRadius = tapRadius;

                    // Nothing on screen outranks this, so no later tap can
                    // change the answer. With a single style showing -- the
                    // common case -- this fires on the first boundary found and
                    // the walk costs exactly what it did before priority
                    // existed.
                    if (nearestStyle >= maxStyle) break;
                }

                // No boundary within reach. Tested on the style rather than the
                // distance now, because a style's radius can be well under 1 and
                // "found nothing" has to stay distinct from "found a thin style
                // at its outer edge". Discard rather than returning a transparent
                // pixel, so the blend unit does no work for the vast majority of
                // the screen.
                if (nearestStyle == 0u) discard;

                float4 colour = _OutlinePalette[min(nearestStyle, OUTLINE_STYLE_COUNT - 1)];

                // Full strength close to the boundary, fading out over the
                // outermost _OutlineSoftness of the line's width. This is what
                // rounds the corners: coverage falls off with true radial
                // distance now, instead of stopping wherever the tap pattern
                // happened to reach.
                // Measured against the winning style's own radius, not the tap
                // radius, so a thinner style fades over its own outer edge
                // rather than over a boundary it never reaches.
                float radius = max(nearestRadius, 1.0e-4);
                float fade = max(_OutlineSoftness, 1.0e-4) * radius;
                colour.a *= 1.0 - smoothstep(radius - fade, radius, nearest);

                return colour;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
