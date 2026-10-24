Shader "NodeWar/Water"
{
    Properties
    {
        _OceanDeep("Ocean deep", Color) = (0.180392,0.164706,0.419608,1)
        _OceanMid("Ocean mid", Color) = (0.239216,0.247059,0.560784,1)
        _OceanShallow("Ocean shallow", Color) = (0.352941,0.419608,0.768627,1)
        _LakeDeep("Lake deep", Color) = (0.243137,0.560784,0.819608,1)
        _LakeMid("Lake mid", Color) = (0.403922,0.701961,0.901961,1)
        _LakeShallow("Lake shallow", Color) = (0.603922,0.839216,0.949020,1)
        _InkColor("Ink", Color) = (0.117647,0.101961,0.180392,1)
        _FoamColor("Foam", Color) = (0.909804,0.956863,1,1)
        _HorizonColor("Horizon", Color) = (0.549020,0.560784,0.850980,1)
        _BandThresholds("Band thresholds", Vector) = (0.15,0.45,0,0)
        _BandSoftness("Band softness", Float) = 0.02
        _InkWidth("Ink width (cells)", Float) = 0.08
        _FoamTex("Packed foam", 2D) = "black" {}
        _FoamTiling("Foam tiling", Float) = 0.06
        _FoamSpeed("Foam speed", Float) = 0.03
        _FoamCutoff("Foam cutoff", Range(0,1)) = 0.97
        _ShoreFoamWidth("Shore foam width (cells)", Float) = 0.2
        _ShoreTex("Shore field", 2D) = "white" {}
        _ShorePrevTex("Previous shore field", 2D) = "white" {}
        _ShoreBlend("Shore blend", Range(0,1)) = 1
        _ShoreST("Shore origin and inverse extent", Vector) = (0,0,1,1)
        _WaveA("Wave A", Vector) = (1,0.2,0.08,6)
        _WaveB("Wave B", Vector) = (0.3,1,0.06,4)
        _WaveC("Wave C", Vector) = (-0.7,0.4,0.035,2)
        _WaveD("Wave D", Vector) = (0.2,-1,0.025,1)
        _NormalTex("Normal", 2D) = "bump" {}
        _NormalStrength("Normal strength", Float) = 0.2
        _SpecHardness("Specular threshold", Range(0,1)) = 0.97
        _SpecColor("Specular", Color) = (0.909804,0.956863,1,1)
        _FresnelPower("Fresnel power", Float) = 4
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        ZWrite On
        Cull Back
        HLSLINCLUDE
        #include "WaterCore.hlsl"
        ENDHLSL
        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex WaterVertex
            #pragma fragment WaterFragment
            #pragma multi_compile_local _ _WATER_MEDIUM _WATER_HIGH _WATER_ULTRA
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ColorMask R
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex WaterVertex
            #pragma fragment WaterDepth
            #pragma multi_compile_local _ _WATER_MEDIUM _WATER_HIGH _WATER_ULTRA
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex WaterVertex
            #pragma fragment WaterDepthNormals
            #pragma multi_compile_local _ _WATER_MEDIUM _WATER_HIGH _WATER_ULTRA
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            ENDHLSL
        }
    }
}
