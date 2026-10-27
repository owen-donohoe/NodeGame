Shader "NodeWar/WaterFloor"
{
    Properties
    {
        _OceanDeepColor("Ocean deep", Color) = (0.180392,0.164706,0.419608,1)
        _LakeDeepColor("Lake deep", Color) = (0.243137,0.560784,0.819608,1)
        _DeepLakeDeepColor("Deep lake", Color) = (0.243137,0.560784,0.819608,1)
        _BodyStepA("Body blend step A", Range(0,1)) = 0.25
        _BodyStepB("Body blend step B", Range(0,1)) = 0.6
        _ShoreTex("Shore", 2D) = "black" {}
        _ShorePrevTex("Previous shore", 2D) = "black" {}
        _ShoreBlend("Blend", Float) = 1
        _ShoreST("Shore mapping", Vector) = (0,0,1,1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            ZWrite On
            HLSLPROGRAM
            #pragma vertex FloorVertex
            #pragma fragment FloorFragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_ShoreTex); SAMPLER(sampler_ShoreTex);
            TEXTURE2D(_ShorePrevTex); SAMPLER(sampler_ShorePrevTex);
            CBUFFER_START(UnityPerMaterial)
            float4 _OceanDeepColor, _LakeDeepColor, _DeepLakeDeepColor, _ShoreST;
            float _ShoreBlend, _BodyStepA, _BodyStepB;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float2 shoreUV : TEXCOORD1; };
            float4 Shore(float2 uv)
            {
                if (_ShoreBlend >= 1) return SAMPLE_TEXTURE2D(_ShoreTex, sampler_ShoreTex, uv);
                return lerp(SAMPLE_TEXTURE2D(_ShorePrevTex, sampler_ShorePrevTex, uv), SAMPLE_TEXTURE2D(_ShoreTex, sampler_ShoreTex, uv), _ShoreBlend);
            }
            Varyings FloorVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.shoreUV = (output.positionWS.xz - _ShoreST.xy) * _ShoreST.zw;
                return output;
            }
            half4 FloorFragment(Varyings input) : SV_Target
            {
                float4 shore = Shore(input.shoreUV);
                float body = shore.b;
                float bodyMix = shore.a < 0.166667 ? 0 : shore.a < 0.5 ? _BodyStepA : shore.a < 0.833333 ? _BodyStepB : 1;
                float lake = step(0.25, body);
                float deepLake = lake * (1 - step(0.75, body));
                return half4(lerp(_OceanDeepColor.rgb, lerp(_LakeDeepColor.rgb, _DeepLakeDeepColor.rgb, deepLake), bodyMix) * 0.85, 1);
            }
            ENDHLSL
        }
    }
}
