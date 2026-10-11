Shader "NodeWar/World/NodeOwnershipFloor"
{
    Properties
    {
        _OwnerColor ("Owner colour", Color) = (1,1,1,1)
        _OwnerShape ("Solid (0) or corner brackets (1)", Float) = 0
        _EdgeAlpha ("Edge alpha", Range(0,1)) = 0.1
        _DitherAlpha ("Dither alpha (reserved)", Range(0,1)) = 0
        _HalfExtent ("Territory half extent", Float) = 0
        _EdgeWidth ("Territory width", Float) = 0
        _CornerRadius ("Corner radius", Float) = 0
        _BracketFraction ("Bracket arm fraction", Range(0,0.5)) = 0.3
        _MinimumWidthPixels ("Minimum stroke pixels", Float) = 1.5
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent" }
        Pass
        {
            Name "OwnershipFloor"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            ZWrite Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _OwnerColor;
                float _OwnerShape, _EdgeAlpha, _DitherAlpha;
                float _HalfExtent, _EdgeWidth, _CornerRadius;
                float _BracketFraction, _MinimumWidthPixels;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = input.uv;
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                // UVs are board-local cell coordinates, independent of seat
                // and sprite tilt. The rounded box is the stroke centreline.
                float2 p = abs(input.uv - 0.5);
                float radius = clamp(_CornerRadius, 0.0, _HalfExtent);
                float2 q = p - (_HalfExtent - radius);
                float distance = length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - radius;
                float pixel = max(fwidth(distance), 1.0e-6);
                float width = max(_EdgeWidth, max(_MinimumWidthPixels, 0.0) * pixel);
                float coverage = saturate((width * 0.5 - abs(distance)) / pixel + 0.5);

                // Four Ls: each arm occupies its side's corner-most 30%,
                // leaving the middle 40% open. Rounded corners stay connected.
                float tangent = min(p.x, p.y);
                float cutoff = _HalfExtent * (1.0 - 2.0 * saturate(_BracketFraction));
                float tangentPixel = max(fwidth(tangent), 1.0e-6);
                float brackets = saturate((tangent - cutoff) / tangentPixel + 0.5);
                coverage *= lerp(1.0, brackets, step(0.5, _OwnerShape));
                float alpha = coverage * saturate(_EdgeAlpha) * _OwnerColor.a;
                clip(alpha - 1.0e-4);
                // _DitherAlpha is deliberately unused until P4.3.
                return float4(_OwnerColor.rgb, alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
