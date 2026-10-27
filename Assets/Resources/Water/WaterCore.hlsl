#ifndef NODE_WAR_WATER_CORE
#define NODE_WAR_WATER_CORE
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

TEXTURE2D(_ShoreTex); SAMPLER(sampler_ShoreTex);
TEXTURE2D(_ShorePrevTex); SAMPLER(sampler_ShorePrevTex);
TEXTURE2D(_SurfaceFoamTex); SAMPLER(sampler_SurfaceFoamTex);
TEXTURE2D(_IntersectionFoamTex); SAMPLER(sampler_IntersectionFoamTex);
TEXTURE2D(_NormalsTex); SAMPLER(sampler_NormalsTex);
CBUFFER_START(UnityPerMaterial)
float4 _OceanShallowColor, _OceanDeepColor, _LakeShallowColor, _LakeDeepColor;
float4 _DeepLakeShallowColor, _DeepLakeDeepColor;
float4 _HorizonColor, _SurfaceFoamColor, _IntersectionFoamColor, _SpecularColor;
float4 _WaveA, _WaveB, _WaveC, _WaveD, _ShoreST;
float4 _SurfaceFoamTex_ST, _IntersectionFoamTex_ST, _NormalsTex_ST;
float4 _BoardRect, _CrestColor, _LapColor;
float _CrestSharpness, _CrestFoamStrength;
float _CrestStrength, _TroughStrength, _CrestFoamThreshold, _WaveScale;
float _ShoreWaveDamping, _ShoreMaxAmplitude, _ShoreCalmDistance;
float _WaveGroupScale, _WaveGroupSpeed, _WaveGroupStrength;
float _ReflectionMin, _ReflectionSteps, _MacroNormalsScale, _MacroNormalsStrength;
float _ShallowAlpha, _ClearDepth, _FloorDepth, _ShoreNoiseAmount, _ShoreNoiseScale, _ShoreNoiseSpeed;
float _DetailNormalSamples;
float _LapFrequency, _LapSpeed, _LapRange, _LapWidth, _LapOpacity;
float _CellSize, _WaveFadeStart, _WaveFadeEnd, _DetailFadeEnd;
float _BodyStepA, _BodyStepB;
float _DepthRange, _ColorSteps, _HorizonDistance;
float _SurfaceFoamTiling, _SurfaceFoamDirection, _SurfaceFoamSpeed, _SurfaceFoamWidth, _SurfaceFoamDistortion;
float _IntersectionFoamSolid, _IntersectionFoamBreakup, _IntersectionFoamTiling, _IntersectionFoamSpeed;
float _NormalsScale, _NormalsSpeed, _NormalsStrength;
float _Smoothness, _Hardness, _WaveSpeed, _WaveSteepnessScale, _ReflectionStrength, _ShoreBlend;
CBUFFER_END
struct Attributes { float4 positionOS : POSITION; };
struct Varyings
{
    float4 positionCS : SV_POSITION;
    float3 positionWS : TEXCOORD0;
    float3 normalWS : TEXCOORD1;
    float2 shoreUV : TEXCOORD2;
    float crest : TEXCOORD3;
    float fold : TEXCOORD4;
};

float4 Shore(float2 uv)
{
    if (_ShoreBlend >= 1.0) return SAMPLE_TEXTURE2D(_ShoreTex, sampler_ShoreTex, uv);
    return lerp(SAMPLE_TEXTURE2D(_ShorePrevTex, sampler_ShorePrevTex, uv),
        SAMPLE_TEXTURE2D(_ShoreTex, sampler_ShoreTex, uv), _ShoreBlend);
}

void Gerstner(float4 wave, float2 xz, float damping, inout float3 position,
    inout float3 tangent, inout float3 binormal, inout float fold)
{
    float2 direction = wave.xy / max(length(wave.xy), 0.001);
    float k = 6.2831853 / max(wave.w * _WaveScale, 0.01);
    float phase = k * (dot(direction, xz) - sqrt(9.8 / k) * _WaveSpeed * _Time.y);
    float steepness = wave.z * damping;
    float amplitude = steepness / k;
    float s = sin(phase), c = cos(phase);
    fold += steepness * s;
    position += float3(direction.x * amplitude * c, amplitude * s, direction.y * amplitude * c);
    tangent += float3(-direction.x * direction.x * steepness * s, direction.x * steepness * c,
        -direction.x * direction.y * steepness * s);
    binormal += float3(-direction.x * direction.y * steepness * s, direction.y * steepness * c,
        -direction.y * direction.y * steepness * s);
}

float OutsideBoard(float2 xz)
{
    return length(max(max(_BoardRect.xy - xz, xz - _BoardRect.zw), 0));
}
float WaveFade(float2 xz)
{
    return 1 - smoothstep(_WaveFadeStart * _CellSize, max(_WaveFadeEnd, _WaveFadeStart + 0.01) * _CellSize, OutsideBoard(xz));
}
float DetailFade(float2 xz)
{
    return 1 - smoothstep(_WaveFadeStart * _CellSize, max(_DetailFadeEnd, _WaveFadeStart + 0.01) * _CellSize, OutsideBoard(xz));
}

Varyings WaterVertex(Attributes input)
{
    Varyings output;
    float3 position = TransformObjectToWorld(input.positionOS.xyz);
    float2 xz = position.xz;
    output.shoreUV = (xz - _ShoreST.xy) * _ShoreST.zw;
    float3 tangent = float3(1, 0, 0), binormal = float3(0, 0, 1);
    output.crest = 0;
    output.fold = 0;
#if defined(_WATER_WAVES_2) || defined(_WATER_WAVES_4)
    float4 shoreVertex = lerp(SAMPLE_TEXTURE2D_LOD(_ShorePrevTex, sampler_ShorePrevTex, output.shoreUV, 0),
        SAMPLE_TEXTURE2D_LOD(_ShoreTex, sampler_ShoreTex, output.shoreUV, 0), _ShoreBlend);
    float damping = _ShoreWaveDamping <= 0 ? 1 : pow(saturate(shoreVertex.g), _ShoreWaveDamping);
    damping *= WaveFade(xz);
    float amplitudeSum = (_WaveA.z * _WaveA.w + _WaveB.z * _WaveB.w) * _WaveScale / 6.2831853;
    float steepnessSum = _WaveA.z + _WaveB.z;
#if defined(_WATER_WAVES_4)
    amplitudeSum += (_WaveC.z * _WaveC.w + _WaveD.z * _WaveD.w) * _WaveScale / 6.2831853;
    steepnessSum += _WaveC.z + _WaveD.z;
#endif
    float groupNoise = SAMPLE_TEXTURE2D_LOD(_SurfaceFoamTex, sampler_SurfaceFoamTex,
        xz * _WaveGroupScale + _Time.y * _WaveGroupSpeed * float2(0.6, 0.8), 0).b;
    float groupAmount = lerp(1 - _WaveGroupStrength, 1 + _WaveGroupStrength, groupNoise);
    float waveScale = min(_WaveSteepnessScale * groupAmount, 0.95 / max(steepnessSum, 0.0001));
    float shoreLimit = lerp(_ShoreMaxAmplitude, 1e4, smoothstep(0, max(_ShoreCalmDistance, 0.001), shoreVertex.r * 3));
    waveScale *= min(1, max(shoreLimit, 0) / max(amplitudeSum * waveScale * damping, 0.0001));
    damping *= waveScale;
    Gerstner(_WaveA, xz, damping, position, tangent, binormal, output.fold);
    Gerstner(_WaveB, xz, damping, position, tangent, binormal, output.fold);
#if defined(_WATER_WAVES_4)
    Gerstner(_WaveC, xz, damping, position, tangent, binormal, output.fold);
    Gerstner(_WaveD, xz, damping, position, tangent, binormal, output.fold);
#endif
    output.fold /= max(steepnessSum, 0.0001); // Threshold is a fraction of authored summed steepness.
    output.crest = (position.y - TransformObjectToWorld(input.positionOS.xyz).y) / max(amplitudeSum * damping, 0.0001);
#endif
    output.positionWS = position;
    output.normalWS = normalize(cross(binormal, tangent));
    output.positionCS = TransformWorldToHClip(position);
    return output;
}

float3 WaterNormal(Varyings input)
{
    float3 normal = normalize(input.normalWS);
    float fade = DetailFade(input.positionWS.xz);
    if (_WATER_MACRO_NORMALS)
    {
        float2 uv = input.positionWS.xz * _MacroNormalsScale + _Time.y * _NormalsSpeed * 0.2 * float2(0.6, -0.4);
        float3 macro = UnpackNormal(SAMPLE_TEXTURE2D(_NormalsTex, sampler_NormalsTex, uv * _NormalsTex_ST.xy + _NormalsTex_ST.zw));
        normal = normalize(normal + float3(macro.x, 0, macro.y) * _MacroNormalsStrength * fade);
    }
    if (_WATER_DETAIL_NORMALS)
    {
        float2 uv = input.positionWS.xz * _NormalsScale;
        float3 detail = UnpackNormal(SAMPLE_TEXTURE2D(_NormalsTex, sampler_NormalsTex,
            (uv + _Time.y * _NormalsSpeed * float2(1, 0.63)) * _NormalsTex_ST.xy + _NormalsTex_ST.zw));
        if (_DetailNormalSamples > 1)
        {
            float3 second = UnpackNormal(SAMPLE_TEXTURE2D(_NormalsTex, sampler_NormalsTex,
                (uv * 1.71 + _Time.y * _NormalsSpeed * float2(-0.7, 1.2)) * _NormalsTex_ST.xy + _NormalsTex_ST.zw));
            detail = normalize(float3(detail.xy + second.xy, detail.z * second.z));
        }
        detail = normalize(float3(detail.xy * _NormalsStrength * fade, detail.z));
        float3 tangent = normalize(float3(normal.y, -normal.x, 0));
        float3 binormal = cross(tangent, normal);
        normal = normalize(tangent * detail.x + binormal * detail.y + normal * detail.z);
    }
    return normal;
}

float3 FoamOverlay(float3 baseColor, float4 foamColor, float coverage)
{
    float3 overlay = lerp(2 * baseColor * foamColor.rgb,
        1 - 2 * (1 - baseColor) * (1 - foamColor.rgb), step(0.5, baseColor));
    return lerp(baseColor, overlay, saturate(coverage * foamColor.a));
}

float2 DistortFoam(float2 uv)
{
    if (!_WATER_SURFACE_FOAM_DISTORTION) return uv;
    float t = _Time.y, amount = _SurfaceFoamDistortion;
    uv.y += amount * 0.01 * (sin(uv.x * 3.5 + t * 0.35) + sin(uv.x * 4.8 + t * 1.05) + sin(uv.x * 7.3 + t * 0.45)) / 3;
    uv.x += amount * 0.12 * (sin(uv.y * 4 + t * 0.50) + sin(uv.y * 6.8 + t * 0.75) + sin(uv.y * 11.3 + t * 0.2)) / 3;
    uv.y += amount * 0.12 * (sin(uv.x * 4.2 + t * 0.64) + sin(uv.x * 6.3 + t * 1.65) + sin(uv.x * 8.2 + t * 0.45)) / 3;
    return uv;
}

float FoamLine(float edge, float width)
{
    float aa = max(fwidth(edge), 0.0001);
    return (1 - smoothstep(width - aa, width + aa, edge)) * step(0.00001, width);
}

half4 WaterFragment(Varyings input) : SV_Target
{
    float4 shore = Shore(input.shoreUV);
    float lake = step(0.25, shore.b);
    float deepLake = lake * (1 - step(0.75, shore.b));
    float bodyMix = shore.a < 0.166667 ? 0 : shore.a < 0.5 ? _BodyStepA : shore.a < 0.833333 ? _BodyStepB : 1;
    float3 shallow = lerp(_OceanShallowColor.rgb, lerp(_LakeShallowColor.rgb, _DeepLakeShallowColor.rgb, deepLake), bodyMix);
    float3 deep = lerp(_OceanDeepColor.rgb, lerp(_LakeDeepColor.rgb, _DeepLakeDeepColor.rgb, deepLake), bodyMix);
    float rawDistance = shore.r * 3;
    float distance = rawDistance, noise = 0.5;
    float detailFade = DetailFade(input.positionWS.xz);
    if (_WATER_SHORE_NOISE || _WATER_LAPPING_LINES)
    {
        float2 uv = input.positionWS.xz * _ShoreNoiseScale;
        noise = (SAMPLE_TEXTURE2D(_SurfaceFoamTex, sampler_SurfaceFoamTex, uv + _Time.y * _ShoreNoiseSpeed * float2(1, 0.7)).b
            + SAMPLE_TEXTURE2D(_SurfaceFoamTex, sampler_SurfaceFoamTex, uv * 1.13 + _Time.y * _ShoreNoiseSpeed * float2(-0.6, 1)).b) * 0.5;
        if (_WATER_SHORE_NOISE) distance = max(0, distance + (noise - 0.5) * 2 * _ShoreNoiseAmount);
    }
    float alpha = 1;
    if (_WATER_TRANSPARENCY) alpha = lerp(_ShallowAlpha, 1, smoothstep(0, max(_ClearDepth, 0.001), distance));
    float depth = saturate(distance / max(_DepthRange, 0.001));
    if (_ColorSteps >= 2) depth = floor(depth * (_ColorSteps - 1) + 0.5) / (_ColorSteps - 1);
    float3 color = lerp(shallow, deep, depth);
    if (_WATER_CREST_COLOUR)
    {
        float crestHighlight = pow(saturate(input.crest), max(_CrestSharpness, 0.001));
        if (_ColorSteps >= 2) crestHighlight = floor(crestHighlight * (_ColorSteps - 1) + 0.5) / (_ColorSteps - 1);
        color = lerp(color, saturate(shallow * _CrestColor.rgb), crestHighlight * _CrestStrength * WaveFade(input.positionWS.xz));
        color = lerp(color, deep, saturate(-input.crest) * _TroughStrength * WaveFade(input.positionWS.xz));
    }
    float3 normal = WaterNormal(input);
    float3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
    if (_WATER_HORIZON_FRESNEL || _WATER_REFLECTIONS)
    {
        float fresnel = pow(1 - saturate(dot(normal, view)), max(_HorizonDistance, 0.001));
        float weight = lerp(_ReflectionMin, 1, fresnel) * _ReflectionStrength;
        float3 reflection = _HorizonColor.rgb;
        if (_WATER_REFLECTIONS)
        {
            reflection = GlossyEnvironmentReflection(reflect(-view, normal), input.positionWS, 0.15h, 1.0h);
            if (_ReflectionSteps >= 2)
            {
                float luminance = max(dot(reflection, float3(0.2126, 0.7152, 0.0722)), 0.0001);
                reflection *= (floor(luminance * _ReflectionSteps) + 0.5) / (_ReflectionSteps * luminance);
            }
            reflection = lerp(_HorizonColor.rgb, reflection, detailFade);
        }
        // Horizon and probe are independently selectable in Custom.
        if (!_WATER_HORIZON_FRESNEL) weight *= detailFade;
        color = lerp(color, reflection, weight);
    }
    // Converge on exactly the flat far-ocean palette as wave and detail fades complete.
    float farWeight = _WATER_HORIZON_FRESNEL
        ? lerp(_ReflectionMin, 1, pow(1 - saturate(view.y), max(_HorizonDistance, 0.001))) * _ReflectionStrength : 0;
    float3 farOcean = lerp(_OceanDeepColor.rgb, _HorizonColor.rgb, farWeight);
    color = lerp(farOcean, color, detailFade);
    float angle = _SurfaceFoamDirection * 6.2831853;
    float2 foamUV = input.positionWS.xz * _SurfaceFoamTiling
        + _Time.y * _SurfaceFoamSpeed * float2(cos(angle), sin(angle));
    float surface = SAMPLE_TEXTURE2D(_SurfaceFoamTex, sampler_SurfaceFoamTex,
        DistortFoam(foamUV) * _SurfaceFoamTex_ST.xy + _SurfaceFoamTex_ST.zw).r;
    float surfaceCoverage = FoamLine(surface, _SurfaceFoamWidth);
    if (_WATER_SECOND_FOAM_LAYER)
    {
        float2 uv = input.positionWS.xz * _SurfaceFoamTiling * 1.37 + _Time.y * _SurfaceFoamSpeed * 0.6 * float2(-0.71, 0.7);
        float edge = SAMPLE_TEXTURE2D(_SurfaceFoamTex, sampler_SurfaceFoamTex,
            DistortFoam(uv) * _SurfaceFoamTex_ST.xy + _SurfaceFoamTex_ST.zw).r;
        surfaceCoverage = max(surfaceCoverage, FoamLine(edge, _SurfaceFoamWidth));
    }
    surfaceCoverage *= lerp(0.025, 1, detailFade);
    color = FoamOverlay(color, _SurfaceFoamColor, surfaceCoverage);
    alpha = max(alpha, surfaceCoverage * _SurfaceFoamColor.a);
    if (_WATER_CREST_FOAM)
    {
        float crestFoam = smoothstep(_CrestFoamThreshold, _CrestFoamThreshold + 0.15, input.fold)
            * FoamLine(surface, 0.35) * _CrestFoamStrength * detailFade;
        color = lerp(color, float3(1, 1, 1), saturate(crestFoam));
        alpha = max(alpha, saturate(crestFoam));
    }
    float2 intersectionUV = input.positionWS.xz * _IntersectionFoamTiling + _Time.y * _IntersectionFoamSpeed * float2(-0.63, 1);
    float intersection = SAMPLE_TEXTURE2D(_IntersectionFoamTex, sampler_IntersectionFoamTex,
        intersectionUV * _IntersectionFoamTex_ST.xy + _IntersectionFoamTex_ST.zw).g;
    float breakup = saturate((distance - _IntersectionFoamSolid) / max(_IntersectionFoamBreakup, 0.001));
    float coverage = (distance <= _IntersectionFoamSolid ? 1 : FoamLine(intersection, 0.35 * (1 - breakup))) * detailFade;
    color = lerp(color, _IntersectionFoamColor.rgb, saturate(coverage * _IntersectionFoamColor.a));
    alpha = max(alpha, coverage * _IntersectionFoamColor.a);
    if (_WATER_LAPPING_LINES)
    {
        float lapPhase = abs(frac(distance * _LapFrequency - _Time.y * _LapSpeed) - 0.5);
        float lap = FoamLine(lapPhase, _LapWidth) * (1 - smoothstep(0, max(_LapRange, 0.001), distance))
            * step(0.45, noise) * detailFade * _LapOpacity;
        color = lerp(color, _LapColor.rgb, saturate(lap * _LapColor.a));
        alpha = max(alpha, lap * _LapColor.a);
    }
    float shadow = 1;
    if (_WATER_SHADOWS)
    {
        shadow = MainLightRealtimeShadow(TransformWorldToShadowCoord(input.positionWS));
        color *= lerp(float3(0.7, 0.73, 0.85), float3(1, 1, 1), shadow);
    }
    if (_WATER_SPECULAR)
    {
        Light mainLight = GetMainLight();
        float3 halfDirection = SafeNormalize(mainLight.direction + view);
        float softSpec = pow(saturate(dot(normal, halfDirection)), exp2(10 * _Smoothness + 1));
        float hardSpec = step(0.5, smoothstep(0.005, 0.01, softSpec));
        float spec = lerp(softSpec, hardSpec, _Hardness);
        color += spec * _SpecularColor.rgb * mainLight.color * shadow;
        alpha = max(alpha, spec * shadow);
    }
    if (_WATER_DITHER)
    {
        float dither = frac(52.9829189 * frac(dot(input.positionCS.xy, float2(0.06711056, 0.00583715))));
        color += (dither - 0.5) / 255.0;
    }
    return half4(color, alpha);
}
half4 WaterDepth(Varyings input) : SV_Target
{
    return 0;
}

half4 WaterDepthNormals(Varyings input) : SV_Target
{
    float3 normal = WaterNormal(input);
#if defined(_GBUFFER_NORMALS_OCT)
    float2 oct = PackNormalOctQuadEncode(normal);
    return half4(PackFloat2To888(saturate(oct * 0.5 + 0.5)), 0);
#else
    return half4(normal, 0);
#endif
}
#endif
