// Writes group IDs, not colour.
//
// Every renderer in one outline group is drawn with the same _OutlineGroupId,
// so the dilate pass finds no edge along the seams inside a group -- no line
// where a node's quad meets its building sprites, none where a villager's
// costume overlay meets its base. Different groups get different IDs, which is
// what stops a villager standing on a node from merging into one blob with it.
//
// The ID and style are globals rather than material properties because the mask
// pass draws with a single shared override material and sets them between
// DrawRenderer calls. A MaterialPropertyBlock would have been the obvious
// alternative and is the wrong one here: NodeView.UpdateVisuals rewrites the
// ground quad's property block every frame to tint it by claim progress, and
// would wipe the ID straight back out.
Shader "NodeWar/Outline Mask"
{
    Properties
    {
        [HideInInspector] _MainTex ("Sprite Texture", 2D) = "white" {}
        _ClipThreshold ("Alpha Clip Threshold", Range(0, 1)) = 0.5
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);

        CBUFFER_START(UnityPerMaterial)
            float _ClipThreshold;
        CBUFFER_END

        // Deliberately outside the material CBUFFER: these are set per group with
        // SetGlobalFloat between draws, and a value inside UnityPerMaterial would
        // be sourced from the material instead and never change.
        float _OutlineGroupId;
        float _OutlineStyleIndex;

        struct Attributes
        {
            float4 positionOS : POSITION;
            float2 uv         : TEXCOORD0;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 uv         : TEXCOORD0;
        };

        Varyings MaskVertex(Attributes input)
        {
            Varyings output;

            // A plain object-to-clip transform is correct and sufficient. Facing
            // is already baked into the Transform by the time this runs:
            // Billboard sets transform.rotation in LateUpdate on the CPU, and
            // SpriteOrientationOffset's offset is applied to sprite rotations by
            // NodePresentation.ApplyOrientation. There is no vertex-shader
            // billboarding anywhere in this project to replicate.
            output.positionCS = TransformObjectToHClip(input.positionOS.xyz);

            // Raw UVs, no TRANSFORM_TEX. A SpriteRenderer already supplies
            // atlas-space UVs, and applying an _ST from the override material
            // would be a second, wrong transform on top.
            output.uv = input.uv;

            return output;
        }

        float4 MaskFragment(Varyings input) : SV_Target
        {
            // Texture alpha only -- vertex colour is deliberately not folded in.
            // The ground quad is Unity's stock primitive mesh, which carries no
            // colour channel, so reading one there would sample whatever
            // happened to be in the register.
            //
            // An untextured renderer samples the "white" default, alpha 1, and
            // survives the clip as a solid shape. That is exactly what the node
            // ground quad needs: GroundMat has no texture at all, and its whole
            // footprint is meant to be part of the silhouette.
            float alpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a;
            clip(alpha - _ClipThreshold);

            // R is the group ID, G the style index, both as 0..1 UNorm and
            // decoded on the far side with an explicit round. B and A are spare;
            // B is reserved for a future owner tint so outline colour can be
            // redundantly tinted by player without ownership ever depending on it.
            return float4(_OutlineGroupId, _OutlineStyleIndex, 0.0, 1.0);
        }
        ENDHLSL

        // Pass 0. The default. Depth-tested and depth-writing against the mask's
        // own depth buffer, so a villager punches a hole in the node's mask and
        // the node's outline does not draw over the villager standing on it.
        // Where a group is occluded it writes no ID and grows no outline.
        Pass
        {
            Name "OutlineMaskDepthTested"

            ZWrite On
            ZTest LEqual
            Cull Off
            ColorMask RGBA

            HLSLPROGRAM
            #pragma vertex MaskVertex
            #pragma fragment MaskFragment
            ENDHLSL
        }

        // Pass 1. Draw-through, for styles that must stay findable behind
        // something -- a selected villager walking behind a node. Recorded after
        // every depth-tested group, because ZTest Always overwrites whatever IDs
        // are already there and would otherwise punch holes in groups in front.
        // ZWrite is off so it cannot disturb the depth other groups tested against.
        Pass
        {
            Name "OutlineMaskDrawThrough"

            ZWrite Off
            ZTest Always
            Cull Off
            ColorMask RGBA

            HLSLPROGRAM
            #pragma vertex MaskVertex
            #pragma fragment MaskFragment
            ENDHLSL
        }
    }

    Fallback Off
}
