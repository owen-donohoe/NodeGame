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

        /// Splits a 0..1 value across two 8-bit UNorm channels, giving roughly
        /// 16 bits. Eight would not do: the composite compares the depths of two
        /// *different* objects at adjacent pixels, and 256 levels across a
        /// perspective depth range lets a villager and the node it stands on
        /// land on the same level. A tie reads as "neither is in front", which
        /// is a missing outline rather than a wrong one, so it would be easy to
        /// mistake for the boundary simply not being detected.
        float2 EncodeNearness(float value)
        {
            value = saturate(value) * 255.0;
            float high = floor(value);
            return float2(high / 255.0, value - high);
        }

        /// <param name="forceNearest">
        /// Draw-through groups claim maximum nearness rather than their real
        /// depth. Their whole purpose is to stay visible behind something, and
        /// the composite only draws the nearer group's line at a seam -- so a
        /// selected villager walking behind a node would lose the outline that
        /// makes it findable, which is the one case the style exists for.
        /// </param>
        float4 MaskCommon(Varyings input, bool forceNearest)
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

            // Nearness rather than depth: bigger always means closer to the
            // camera, on every platform. Doing the reversed-Z fold here means
            // the composite compares two numbers and needs to know nothing
            // about the platform's depth convention.
            #if UNITY_REVERSED_Z
                float nearness = input.positionCS.z;
            #else
                float nearness = 1.0 - input.positionCS.z;
            #endif

            if (forceNearest) nearness = 1.0;

            // R is the group ID, G the style index, both as 0..1 UNorm and
            // decoded on the far side with an explicit round. B and A carry
            // nearness, 16 bits across the pair.
            //
            // That spends both spare channels, including the one previously
            // reserved for an owner tint. Depth is the better use: without it
            // the composite cannot tell which of two overlapping groups is in
            // front, and an owner tint is recoverable from the group ID via the
            // palette, which is where every other outline colour decision
            // already lives.
            //
            // It also means alpha is no longer 1 wherever a group was drawn, so
            // the mask's alpha channel is no longer a silhouette preview in the
            // frame debugger. Use the red channel, or the Mask debug view.
            float2 nearnessBits = EncodeNearness(nearness);

            return float4(_OutlineGroupId, _OutlineStyleIndex,
                          nearnessBits.x, nearnessBits.y);
        }

        float4 MaskFragmentDepthTested(Varyings input) : SV_Target
        {
            return MaskCommon(input, false);
        }

        float4 MaskFragmentDrawThrough(Varyings input) : SV_Target
        {
            return MaskCommon(input, true);
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
            #pragma fragment MaskFragmentDepthTested
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
            #pragma fragment MaskFragmentDrawThrough
            ENDHLSL
        }
    }

    Fallback Off
}
