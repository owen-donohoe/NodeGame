Shader "NodeWar/Water"
{
    Properties
    {
        _OceanShallowColor("Ocean shallow", Color) = (0.352941,0.419608,0.768627,1)
        _OceanDeepColor("Ocean deep", Color) = (0.180392,0.164706,0.419608,1)
        _LakeShallowColor("Lake shallow", Color) = (0.603922,0.839216,0.949020,1)
        _LakeDeepColor("Lake deep", Color) = (0.243137,0.560784,0.819608,1)
        _DeepLakeShallowColor("Deep lake shallow", Color) = (0.243137,0.560784,0.819608,1)
        _DeepLakeDeepColor("Deep lake deep", Color) = (0.243137,0.560784,0.819608,1)
        _DepthRange("Depth range (cells)", Float) = 2
        _ColorSteps("Colour steps (0 = smooth)", Range(0,16)) = 0
        _HorizonColor("Horizon", Color) = (0.549020,0.560784,0.850980,1)
        _HorizonDistance("Horizon exponent", Float) = 4
        _SurfaceFoamTex("Surface foam", 2D) = "black" {}
        _SurfaceFoamTiling("Surface foam tiling", Float) = 0.2
        _SurfaceFoamDirection("Surface foam direction (turns)", Range(0,1)) = 0.09
        _SurfaceFoamSpeed("Surface foam speed", Float) = 0.03
        _SurfaceFoamWidth("Surface foam width", Range(0,0.3)) = 0.06
        _SurfaceFoamDistortion("Surface foam distortion", Range(0,10)) = 1
        _SurfaceFoamColor("Surface foam (alpha = amount)", Color) = (0.909804,0.956863,1,0.6)
        _IntersectionFoamTex("Intersection foam", 2D) = "black" {}
        _IntersectionFoamDepth("Intersection foam depth (cells)", Float) = 0.3
        _IntersectionFoamCutoff("Intersection foam cutoff", Range(0,1)) = 0.7
        _IntersectionFoamFade("Intersection foam fade exponent", Float) = 1
        _IntersectionFoamTiling("Intersection foam tiling", Float) = 0.6
        _IntersectionFoamSpeed("Intersection foam speed", Float) = 0.05
        _IntersectionFoamColor("Intersection foam (alpha = amount)", Color) = (0.909804,0.956863,1,1)
        _InkWidth("Ink width (cells, 0 = off)", Range(0,0.3)) = 0.025
        _InkColor("Ink", Color) = (0.117647,0.101961,0.180392,1)
        _NormalsTex("Normals", 2D) = "bump" {}
        _NormalsScale("Normals scale", Float) = 0.4
        _NormalsSpeed("Normals speed", Float) = 0.04
        _NormalsStrength("Normals strength", Range(0,2)) = 0.6
        _Smoothness("Smoothness", Range(0,1)) = 0.6
        _Hardness("Specular hardness", Range(0,1)) = 0.5
        _SpecularColor("Specular", Color) = (0.909804,0.956863,1,1)
        _CellSize("Cell size", Float) = 6
        _BoardRect("Board bounds", Vector) = (-3,-3,3,3)
        _WaveFadeStart("Wave fade start (cells)", Float) = 2
        _WaveFadeEnd("Wave fade end (cells)", Float) = 4
        _DetailFadeEnd("Detail fade end (cells)", Float) = 10
        _WaveA("Wave A", Vector) = (1,0.2,0.28,18)
        _WaveB("Wave B", Vector) = (0.3,1,0.22,11)
        _WaveC("Wave C", Vector) = (-0.7,0.4,0.14,6)
        _WaveD("Wave D", Vector) = (0.2,-1,0.1,3.5)
        _WaveSpeed("Wave speed", Float) = 1
        _WaveSteepnessScale("Wave steepness scale", Range(0,2)) = 0.125
        _MaxWaveAmplitude("Maximum summed wave amplitude", Float) = 0.37
        _CrestColor("Crest colour", Color) = (0.514706,0.564706,0.826470,1)
        _CrestStrength("Crest strength", Range(0,1)) = 0.5
        _TroughStrength("Trough strength", Range(0,1)) = 0.25
        _CrestFoamThreshold("Crest foam threshold", Range(0,1)) = 0.7
        _ReflectionStrength("Reflection strength", Range(0,1)) = 0.6
        _ReflectionMin("Reflection floor", Range(0,1)) = 0.25
        _ReflectionSteps("Reflection steps (0 = smooth)", Range(0,16)) = 0
        _MacroNormalsScale("Macro normals scale", Float) = 0.04
        _MacroNormalsStrength("Macro normals strength", Range(0,2)) = 0.8
        [NoScaleOffset] _ShoreTex("Shore field", 2D) = "white" {}
        [NoScaleOffset] _ShorePrevTex("Previous shore field", 2D) = "white" {}
        _ShoreBlend("Shore blend", Range(0,1)) = 1
        _ShoreST("Shore origin and inverse extent", Vector) = (0,0,1,1)
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
            #pragma multi_compile _ _WATER_MEDIUM _WATER_HIGH _WATER_ULTRA
            #pragma multi_compile _ _REFLECTION_PROBE_BOX_PROJECTION
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
            #pragma multi_compile _ _WATER_MEDIUM _WATER_HIGH _WATER_ULTRA
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
            #pragma multi_compile _ _WATER_MEDIUM _WATER_HIGH _WATER_ULTRA
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            ENDHLSL
        }
    }
}
