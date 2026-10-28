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
        _ColorSteps("Colour steps (0 = smooth)", Range(0,16)) = 6
        _BodyStepA("Body blend step A", Range(0,1)) = 0.30
        _BodyStepB("Body blend step B", Range(0,1)) = 0.65
        _HorizonColor("Horizon", Color) = (0.549020,0.560784,0.850980,1)
        _HorizonDistance("Horizon exponent", Float) = 4
        _FoamTex("Surface foam", 2D) = "black" {}
        _SurfaceFoamTiling("Surface foam tiling", Float) = 0.2
        _SurfaceFoamDirection("Surface foam direction (turns)", Range(0,1)) = 0.09
        _SurfaceFoamSpeed("Surface foam speed", Float) = 0.03
        _SurfaceFoamWidth("Surface foam width", Range(0,0.3)) = 0.06
        _SurfaceFoamDistortion("Surface foam distortion", Range(0,10)) = 1
        _SurfaceFoamColor("Surface foam (alpha = amount)", Color) = (0.909804,0.956863,1,0.6)
        [HideInInspector] _DetailNormalSamples("Normal samples", Float) = 1
        _SwellTex("Swell height and derivatives", 2D) = "black" {}
        _NormalsScale("Normals scale", Float) = 0.4
        _NormalsSpeed("Normals speed", Float) = 0.04
        _NormalsStrength("Normals strength", Range(0,2)) = 0.6
        _Smoothness("Smoothness", Range(0,1)) = 0.6
        _Hardness("Specular hardness", Range(0,1)) = 0.5
        _SpecularColor("Specular", Color) = (0.909804,0.956863,1,1)
        [HideInInspector] _SrcBlend("Source blend", Float) = 1
        [HideInInspector] _DstBlend("Destination blend", Float) = 0
        [HideInInspector] _ZWrite("Depth write", Float) = 1
        _ShallowAlpha("Shallow alpha", Range(0,1)) = 0.35
        _ClearDepth("Clear depth (cells)", Float) = 0.8
        _FloorDepth("Floor depth (world units)", Float) = 1.2
        _BodyNoiseAmount("Body noise amount", Float) = 0.25
        _ShoreNoiseAmount("Shore noise amount (cells)", Float) = 0.25
        _LapFrequency("Lap frequency", Float) = 1.5
        _LapSpeed("Lap speed", Float) = 0.25
        _LapRange("Lap range (cells)", Float) = 1.2
        _LapWidth("Lap width", Float) = 0.08
        _LapOpacity("Lap opacity", Range(0,1)) = 0.85
        _LapColor("Lap colour", Color) = (0.909804,0.956863,1,1)
        _CellSize("Cell size", Float) = 6
        _BoardRect("Board bounds", Vector) = (-3,-3,3,3)
        _WaveFadeStart("Wave fade start (cells)", Float) = 2
        _WaveFadeEnd("Wave fade end (cells)", Float) = 10
        _DetailFadeEnd("Detail fade end (cells)", Float) = 16
        _WaveA("Wave A", Vector) = (1,0,0.28,18)
        _WaveB("Wave B", Vector) = (0.422618,0.906308,0.22,11.124)
        _WaveC("Wave C", Vector) = (-0.766044,0.642788,0.14,6.876)
        _WaveD("Wave D", Vector) = (-0.422618,-0.906308,0.1,4.248)
        _NoiseWaveHeight("Noise wave height (world)", Float) = 0.25
        _NoiseWaveScale("Noise wave scale", Float) = 0.025
        _NoiseWaveSpeed("Noise wave speed", Float) = 0.02
        _WaveSpeed("Wave speed", Float) = 1
        _WaveSteepnessScale("Wave steepness scale", Range(0,1.5)) = 1
        _WaveScale("Wave scale", Range(0.25,4)) = 1.0
        _ShoreWaveDamping("Shore wave damping", Range(0,3)) = 1
        _ShoreMaxAmplitude("Shore maximum amplitude", Float) = 0.35
        _ShoreCalmDistance("Shore calm distance (cells)", Float) = 1.5
        _WaveGroupScale("Wave group scale", Float) = 0.012
        _WaveGroupSpeed("Wave group speed", Float) = 0.01
        _WaveGroupStrength("Wave group strength", Range(0,1)) = 0.5
        _CrestColor("Crest tint (shallow multiplier)", Color) = (1.15,1.15,1.15,1)
        _CrestStrength("Crest strength", Range(0,1)) = 0.45
        _CrestSharpness("Crest sharpness", Float) = 2.5
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
        #define _REFLECTION_PROBE_BOX_PROJECTION 1
        #include "WaterCore.hlsl"
        ENDHLSL
        Pass
        {
            Name "UniversalForward"
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex WaterVertex
            #pragma fragment WaterFragment
            #pragma multi_compile _ _WATER_WAVES_NOISE _WATER_WAVES_2 _WATER_WAVES_4
            #pragma dynamic_branch _WATER_TRANSPARENCY
            #pragma dynamic_branch _WATER_SURFACE_FOAM_DISTORTION
            #pragma dynamic_branch _WATER_HORIZON_FRESNEL
            #pragma dynamic_branch _WATER_SPECULAR
            #pragma dynamic_branch _WATER_SHADOWS
            #pragma dynamic_branch _WATER_DETAIL_NORMALS
            #pragma dynamic_branch _WATER_MACRO_NORMALS
            #pragma dynamic_branch _WATER_REFLECTIONS
            #pragma dynamic_branch _WATER_CREST_COLOUR
            #pragma dynamic_branch _WATER_LAPPING_LINES
            #pragma dynamic_branch _WATER_SECOND_FOAM_LAYER
            #pragma dynamic_branch _WATER_DITHER
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
            #pragma multi_compile _ _WATER_WAVES_NOISE _WATER_WAVES_2 _WATER_WAVES_4
            #pragma dynamic_branch _WATER_TRANSPARENCY
            #pragma dynamic_branch _WATER_SURFACE_FOAM_DISTORTION
            #pragma dynamic_branch _WATER_HORIZON_FRESNEL
            #pragma dynamic_branch _WATER_SPECULAR
            #pragma dynamic_branch _WATER_SHADOWS
            #pragma dynamic_branch _WATER_DETAIL_NORMALS
            #pragma dynamic_branch _WATER_MACRO_NORMALS
            #pragma dynamic_branch _WATER_REFLECTIONS
            #pragma dynamic_branch _WATER_CREST_COLOUR
            #pragma dynamic_branch _WATER_LAPPING_LINES
            #pragma dynamic_branch _WATER_SECOND_FOAM_LAYER
            #pragma dynamic_branch _WATER_DITHER
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
            #pragma multi_compile _ _WATER_WAVES_NOISE _WATER_WAVES_2 _WATER_WAVES_4
            #pragma dynamic_branch _WATER_TRANSPARENCY
            #pragma dynamic_branch _WATER_SURFACE_FOAM_DISTORTION
            #pragma dynamic_branch _WATER_HORIZON_FRESNEL
            #pragma dynamic_branch _WATER_SPECULAR
            #pragma dynamic_branch _WATER_SHADOWS
            #pragma dynamic_branch _WATER_DETAIL_NORMALS
            #pragma dynamic_branch _WATER_MACRO_NORMALS
            #pragma dynamic_branch _WATER_REFLECTIONS
            #pragma dynamic_branch _WATER_CREST_COLOUR
            #pragma dynamic_branch _WATER_LAPPING_LINES
            #pragma dynamic_branch _WATER_SECOND_FOAM_LAYER
            #pragma dynamic_branch _WATER_DITHER
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            ENDHLSL
        }
    }
}
