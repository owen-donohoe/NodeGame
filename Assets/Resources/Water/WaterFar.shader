Shader "NodeWar/WaterFar"
{
    Properties
    {
        _OceanDeepColor("Ocean deep", Color) = (0.180392,0.164706,0.419608,1)
        _HorizonColor("Horizon", Color) = (0.549020,0.560784,0.850980,1)
        _HorizonDistance("Horizon exponent", Float) = 4
        _ReflectionMin("Reflection floor", Float) = 0.25
        _ReflectionStrength("Reflection strength", Float) = 0.6
        _HorizonEnabled("Horizon enabled", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            ZWrite On
            Cull Back
            HLSLPROGRAM
            #pragma vertex FarVertex
            #pragma fragment FarFragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _OceanDeepColor, _HorizonColor;
            float _HorizonDistance, _ReflectionMin, _ReflectionStrength, _HorizonEnabled;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; half3 farOcean : TEXCOORD0; };
            Varyings FarVertex(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                // Same seam formula as WaterVertex: geometric-normal vertex fresnel, then palette lerp.
                half3 viewDir = GetWorldSpaceNormalizeViewDir(positionWS);
                half3 geometricNormal = TransformObjectToWorldNormal(float3(0, 1, 0));
                half farWeight = _HorizonEnabled * lerp(_ReflectionMin, 1, pow(1 - saturate(dot(geometricNormal, viewDir)), max(_HorizonDistance, 0.001h))) * _ReflectionStrength;
                output.farOcean = lerp(_OceanDeepColor.rgb, _HorizonColor.rgb, farWeight);
                return output;
            }
            half4 FarFragment(Varyings input) : SV_Target { return half4(input.farOcean, 1); }
            ENDHLSL
        }
    }
}
