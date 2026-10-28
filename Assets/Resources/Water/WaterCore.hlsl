#ifndef NODE_WAR_WATER_CORE
#define NODE_WAR_WATER_CORE
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

TEXTURE2D(_ShoreTex); SAMPLER(sampler_ShoreTex);
TEXTURE2D(_ShorePrevTex); SAMPLER(sampler_ShorePrevTex);
TEXTURE2D(_FoamTex); SAMPLER(sampler_FoamTex);
TEXTURE2D(_SwellTex); SAMPLER(sampler_SwellTex);
CBUFFER_START(UnityPerMaterial)
half4 _OceanShallowColor, _OceanDeepColor, _LakeShallowColor, _LakeDeepColor;
half4 _DeepLakeShallowColor, _DeepLakeDeepColor;
half4 _HorizonColor, _SurfaceFoamColor, _SpecularColor, _ShoreFoamColor;
float _ShoreFoamWidth;
float4 _WaveA, _WaveB, _WaveC, _WaveD, _ShoreST;
float4 _FoamTex_ST, _SwellTex_ST;
float4 _ShoreTex_TexelSize;
float4 _BoardRect, _CrestColor, _SwashColor;
float _CrestSharpness;
float _CrestStrength, _WaveScale;
float _NoiseWaveHeight, _NoiseWaveScale, _NoiseWaveSpeed;
float _ShoreWaveDamping, _ShoreMaxAmplitude, _ShoreCalmDistance;
float _WaveGroupScale, _WaveGroupSpeed, _WaveGroupStrength;
float _ReflectionMin, _ReflectionSteps, _MacroNormalsScale, _MacroNormalsStrength;
float _ShallowAlpha, _ClearDepth, _FloorDepth, _ShoreNoiseAmount, _BodyNoiseAmount;
float _DetailNormalSamples, _FoamDistortionQuality;
float _SwashDesync, _SwashWetStrength, _SwashSpeed, _SwashRange, _SwashWidth, _SwashOpacity;
float _CellSize, _WaveFadeStart, _WaveFadeEnd, _DetailFadeEnd;
float _BodyStepA, _BodyStepB;
float _DepthRange, _ColorSteps, _HorizonDistance;
float _SurfaceFoamTiling, _SurfaceFoamDirection, _SurfaceFoamSpeed, _SurfaceFoamWidth, _SurfaceFoamDistortion;
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
    half crest : TEXCOORD3;
    half3 farOcean : TEXCOORD4;
    half horizonWeight : TEXCOORD5;
};

// Rotation 33.7 degrees, golden scale; wrap scrolls before adding world coordinates.
float2 RotateNoise(float2 uv) { return float2(0.831954 * uv.x - 0.554844 * uv.y, 0.554844 * uv.x + 0.831954 * uv.y); }
float2 UnrotateNoise(float2 uv) { return float2(0.831954 * uv.x + 0.554844 * uv.y, -0.554844 * uv.x + 0.831954 * uv.y); }
float2 Scroll(float speed, float2 direction) { return frac(_Time.y * speed * direction); }
half4 Shore(float2 uv)
{
    // Keep categorical body ownership exact while filtering distance, phase and mix.
    float2 bodyUV = (floor(uv * _ShoreTex_TexelSize.zw) + 0.5) * _ShoreTex_TexelSize.xy;
    half4 currentShore = SAMPLE_TEXTURE2D(_ShoreTex, sampler_ShoreTex, uv);
    currentShore.b = SAMPLE_TEXTURE2D(_ShoreTex, sampler_ShoreTex, bodyUV).b;
    if (_ShoreBlend >= 1.0) return currentShore;
    half4 previousShore = SAMPLE_TEXTURE2D(_ShorePrevTex, sampler_ShorePrevTex, uv);
    previousShore.b = SAMPLE_TEXTURE2D(_ShorePrevTex, sampler_ShorePrevTex, bodyUV).b;
    return lerp(previousShore, currentShore, _ShoreBlend);
}
void Gerstner(float4 wave, float2 xz, float damping, inout float3 position,
    inout float3 tangent, inout float3 binormal)
{
    float2 direction = wave.xy / max(length(wave.xy), 0.001);
    float k = 6.2831853 / max(wave.w * _WaveScale, 0.01);
    float phase = k * (dot(direction, xz) - sqrt(9.8 / k) * _WaveSpeed * _Time.y);
    float steepness = wave.z * damping;
    float amplitude = steepness / k;
    float s = sin(phase), c = cos(phase);
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
#if !defined(_WATER_LITE) && (defined(_WATER_WAVES_NOISE) || defined(_WATER_WAVES_2) || defined(_WATER_WAVES_4))
    float4 shoreVertex = SAMPLE_TEXTURE2D_LOD(_ShoreTex, sampler_ShoreTex, output.shoreUV, 0);
    if (_ShoreBlend < 1) shoreVertex = lerp(SAMPLE_TEXTURE2D_LOD(_ShorePrevTex, sampler_ShorePrevTex, output.shoreUV, 0), shoreVertex, _ShoreBlend);
    float damping = _ShoreWaveDamping <= 0 ? 1 : pow(saturate(shoreVertex.r * 2), _ShoreWaveDamping);
    damping *= WaveFade(xz);
    float shoreLimit = lerp(_ShoreMaxAmplitude, 1e4, smoothstep(0, max(_ShoreCalmDistance, 0.001), shoreVertex.r * 3));
#if defined(_WATER_WAVES_NOISE)
    float2 waveUV = xz * _NoiseWaveScale;
    float4 swellA = SAMPLE_TEXTURE2D_LOD(_SwellTex, sampler_SwellTex, (waveUV + Scroll(_NoiseWaveSpeed, float2(0.6, 0.8))) * _SwellTex_ST.xy + _SwellTex_ST.zw, 0);
    float4 swellB = SAMPLE_TEXTURE2D_LOD(_SwellTex, sampler_SwellTex, (RotateNoise(waveUV) * 1.618034 + Scroll(_NoiseWaveSpeed, float2(-0.8, 0.6))) * _SwellTex_ST.xy + _SwellTex_ST.zw, 0);
    float heightScale = min(max(_NoiseWaveHeight, 0) * damping, max(shoreLimit, 0));
    float height = swellA.r + swellB.r - 1;
    position.y += height * heightScale;
    float2 gradient = ((swellA.gb - 0.5) * _SwellTex_ST.xy + UnrotateNoise((swellB.gb - 0.5) * _SwellTex_ST.xy) * 1.618034) * 32 * _NoiseWaveScale * heightScale;
    tangent.y = gradient.x;
    binormal.y = gradient.y;
    output.crest = height;
#else
    float amplitudeSum = (_WaveA.z * _WaveA.w + _WaveB.z * _WaveB.w) * _WaveScale / 6.2831853;
    float steepnessSum = _WaveA.z + _WaveB.z;
#if defined(_WATER_WAVES_4)
    amplitudeSum += (_WaveC.z * _WaveC.w + _WaveD.z * _WaveD.w) * _WaveScale / 6.2831853;
    steepnessSum += _WaveC.z + _WaveD.z;
#endif
    float2 groupUV = xz * _WaveGroupScale;
    float groupNoise = (SAMPLE_TEXTURE2D_LOD(_SwellTex, sampler_SwellTex, (groupUV + Scroll(_WaveGroupSpeed, float2(0.6, 0.8))) * _SwellTex_ST.xy + _SwellTex_ST.zw, 0).r
        + SAMPLE_TEXTURE2D_LOD(_SwellTex, sampler_SwellTex, (RotateNoise(groupUV) * 1.618034 + Scroll(_WaveGroupSpeed, float2(-0.8, 0.6))) * _SwellTex_ST.xy + _SwellTex_ST.zw, 0).r) * 0.5;
    float groupAmount = lerp(1 - _WaveGroupStrength, 1 + _WaveGroupStrength, groupNoise);
    float waveScale = min(_WaveSteepnessScale * groupAmount, 0.95 / max(steepnessSum, 0.0001));
    waveScale *= min(1, max(shoreLimit, 0) / max(amplitudeSum * waveScale * damping, 0.0001));
    damping *= waveScale;
    Gerstner(_WaveA, xz, damping, position, tangent, binormal);
    Gerstner(_WaveB, xz, damping, position, tangent, binormal);
#if defined(_WATER_WAVES_4)
    Gerstner(_WaveC, xz, damping, position, tangent, binormal);
    Gerstner(_WaveD, xz, damping, position, tangent, binormal);
#endif
    output.crest = (position.y - TransformObjectToWorld(input.positionOS.xyz).y) / max(amplitudeSum * damping, 0.0001);
#endif
#endif
    // Same seam formula as WaterFar: geometric-normal vertex fresnel, then palette lerp.
    half3 viewDir = GetWorldSpaceNormalizeViewDir(position);
    half3 geometricNormal = TransformObjectToWorldNormal(float3(0, 1, 0));
    half farWeight = (_WATER_HORIZON_FRESNEL ? 1 : 0) * lerp(_ReflectionMin, 1, pow(1 - saturate(dot(geometricNormal, viewDir)), max(_HorizonDistance, 0.001h))) * _ReflectionStrength;
    output.farOcean = lerp(_OceanDeepColor.rgb, _HorizonColor.rgb, farWeight);
    output.horizonWeight = farWeight;
    output.positionWS = position;
    output.normalWS = normalize(cross(binormal, tangent));
    output.positionCS = TransformWorldToHClip(position);
    return output;
}

#if !defined(_WATER_LITE)
half3 WaterNormal(Varyings input)
{
    half3 normal = normalize(input.normalWS);
    float fade = DetailFade(input.positionWS.xz);
    if (_WATER_MACRO_NORMALS)
    {
        float2 uv = RotateNoise(input.positionWS.xz * _MacroNormalsScale) + Scroll(_NormalsSpeed * 0.2, float2(0.6, -0.4));
        half3 macro = float3((SAMPLE_TEXTURE2D(_SwellTex, sampler_SwellTex, uv * _SwellTex_ST.xy + _SwellTex_ST.zw).gb - 0.5) * -4.8, 1);
        normal = normalize(normal + float3(UnrotateNoise(macro.xy).x, 0, UnrotateNoise(macro.xy).y) * _MacroNormalsStrength * fade);
    }
    if (_WATER_DETAIL_NORMALS)
    {
        float2 uv = RotateNoise(input.positionWS.xz * _NormalsScale);
        half3 detail = float3((SAMPLE_TEXTURE2D(_SwellTex, sampler_SwellTex,
            (uv + Scroll(_NormalsSpeed, float2(1, 0.63))) * _SwellTex_ST.xy + _SwellTex_ST.zw).gb - 0.5) * -4.8, 1);
        if (_DetailNormalSamples > 1)
        {
            half3 second = float3((SAMPLE_TEXTURE2D(_SwellTex, sampler_SwellTex,
                (RotateNoise(uv) * 1.618034 + Scroll(_NormalsSpeed, float2(-0.7, 1.2))) * _SwellTex_ST.xy + _SwellTex_ST.zw).gb - 0.5) * -4.8, 1);
            detail = normalize(float3(detail.xy + UnrotateNoise(second.xy), detail.z * second.z));
        }
        detail.xy = UnrotateNoise(detail.xy);
        detail = normalize(float3(detail.xy * _NormalsStrength * fade, detail.z));
        float3 tangent = normalize(float3(normal.y, -normal.x, 0));
        float3 binormal = cross(tangent, normal);
        normal = normalize(tangent * detail.x + binormal * detail.y + normal * detail.z);
    }
    return normal;
}

#endif

half3 FoamOverlay(half3 baseColor, half4 foamColor, half coverage)
{
    half3 overlay = lerp(2 * baseColor * foamColor.rgb,
        1 - 2 * (1 - baseColor) * (1 - foamColor.rgb), step(0.5, baseColor));
    return lerp(baseColor, overlay, saturate(coverage * foamColor.a));
}

float2 DistortFoam(float2 uv)
{
#if defined(_WATER_LITE)
    return uv;
#else
    if (!_WATER_SURFACE_FOAM_DISTORTION) return uv;
    float t = _Time.y, amount = _SurfaceFoamDistortion;
    if (_FoamDistortionQuality <= 2)
    {
        uv += amount * 0.08 * float2(sin(uv.y * 4 + t * 0.5), sin(uv.x * 4.2 + t * 0.64));
        return uv;
    }
    uv.y += amount * 0.01 * (sin(uv.x * 3.5 + t * 0.35) + sin(uv.x * 4.8 + t * 1.05) + sin(uv.x * 7.3 + t * 0.45)) / 3;
    uv.x += amount * 0.12 * (sin(uv.y * 4 + t * 0.50) + sin(uv.y * 6.8 + t * 0.75) + sin(uv.y * 11.3 + t * 0.2)) / 3;
    uv.y += amount * 0.12 * (sin(uv.x * 4.2 + t * 0.64) + sin(uv.x * 6.3 + t * 1.65) + sin(uv.x * 8.2 + t * 0.45)) / 3;
    return uv;
#endif
}

half FoamLine(half edge, half width)
{
    float aa = max(fwidth(edge), 0.0001);
    return (1 - smoothstep(width - aa, width + aa, edge)) * step(0.00001, width);
}

half4 WaterFragment(Varyings input) : SV_Target
{
    half4 shore = Shore(input.shoreUV);
    float angle = _SurfaceFoamDirection * 6.2831853;
    float2 foamUV = input.positionWS.xz * _SurfaceFoamTiling
        + Scroll(_SurfaceFoamSpeed, float2(cos(angle), sin(angle)));
    half4 foam = SAMPLE_TEXTURE2D(_FoamTex, sampler_FoamTex,
        DistortFoam(foamUV) * _FoamTex_ST.xy + _FoamTex_ST.zw);
    half surfaceCoverage = FoamLine(foam.r, _SurfaceFoamWidth);
#if !defined(_WATER_LITE)
    if (_WATER_SECOND_FOAM_LAYER)
    {
        float2 uv = RotateNoise(input.positionWS.xz * _SurfaceFoamTiling) * 1.618034 + Scroll(_SurfaceFoamSpeed * 0.6, float2(-0.71, 0.7));
        half4 secondFoam = SAMPLE_TEXTURE2D(_FoamTex, sampler_FoamTex,
            DistortFoam(uv) * _FoamTex_ST.xy + _FoamTex_ST.zw);
        foam.g = (foam.g + secondFoam.g) * 0.5;
        surfaceCoverage = max(surfaceCoverage, FoamLine(secondFoam.r, _SurfaceFoamWidth));
    }
#endif
    half noise = foam.g;
    half displacedMix = shore.b >= 0.25 ? saturate(shore.a + (noise - 0.5) * 2 * _BodyNoiseAmount) : 0;
    half lake = step(0.25, shore.b);
    half deepLake = lake * (1 - step(0.75, shore.b));
    half bodyMix = displacedMix < 0.166667 ? 0 : displacedMix < 0.5 ? _BodyStepA : displacedMix < 0.833333 ? _BodyStepB : 1;
    half3 shallow = lerp(_OceanShallowColor.rgb, lerp(_LakeShallowColor.rgb, _DeepLakeShallowColor.rgb, deepLake), bodyMix);
    half3 deep = lerp(_OceanDeepColor.rgb, lerp(_LakeDeepColor.rgb, _DeepLakeDeepColor.rgb, deepLake), bodyMix);
    half distance = max(0, shore.r * 3 + (noise - 0.5) * 2 * _ShoreNoiseAmount);
    half detailFade = DetailFade(input.positionWS.xz);
    half alpha = 1;
#if !defined(_WATER_LITE)
    if (_WATER_TRANSPARENCY) alpha = lerp(_ShallowAlpha, 1, smoothstep(0, max(_ClearDepth, 0.001), distance));
#endif
    half depth = saturate(distance / max(_DepthRange, 0.001));
    if (_ColorSteps >= 2) depth = floor(depth * (_ColorSteps - 1) + 0.5) / (_ColorSteps - 1);
    half3 color = lerp(shallow, deep, depth);
#if !defined(_WATER_LITE)
    if (_WATER_CREST_COLOUR)
    {
        half crestHighlight = pow(saturate(input.crest), max(_CrestSharpness, 0.001));
        if (_ColorSteps >= 2) crestHighlight = floor(crestHighlight * (_ColorSteps - 1) + 0.5) / (_ColorSteps - 1);
        color = lerp(color, max(color, saturate(shallow * _CrestColor.rgb)), crestHighlight * _CrestStrength * WaveFade(input.positionWS.xz));
    }
    half3 normal = WaterNormal(input);
    half3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
    if (_WATER_HORIZON_FRESNEL || _WATER_REFLECTIONS)
    {
        half fresnel = pow(1 - saturate(dot(normal, view)), max(_HorizonDistance, 0.001));
        half weight = lerp(_ReflectionMin, 1, fresnel) * _ReflectionStrength;
        half3 reflection = _HorizonColor.rgb;
        if (_WATER_REFLECTIONS)
        {
            reflection = GlossyEnvironmentReflection(reflect(-view, normal), input.positionWS, 0.15h, 1.0h);
            if (_ReflectionSteps >= 2)
            {
                half luminance = max(dot(reflection, half3(0.2126, 0.7152, 0.0722)), 0.0001);
                reflection *= (floor(luminance * _ReflectionSteps) + 0.5) / (_ReflectionSteps * luminance);
            }
            reflection = lerp(_HorizonColor.rgb, reflection, detailFade);
        }
        // Horizon and probe are independently selectable in Custom.
        if (!_WATER_HORIZON_FRESNEL) weight *= detailFade;
        color = lerp(color, reflection, weight);
    }
#else
    color = lerp(color, _HorizonColor.rgb, input.horizonWeight);
#endif
    surfaceCoverage *= detailFade;
    color = FoamOverlay(color, _SurfaceFoamColor, surfaceCoverage);
    alpha = max(alpha, surfaceCoverage * _SurfaceFoamColor.a);
    half shoreAA = max(fwidth(distance), 0.0001h);
    half shoreCoverage = (1 - smoothstep(_ShoreFoamWidth - shoreAA, _ShoreFoamWidth + shoreAA, distance)) * detailFade;
    color = lerp(color, _ShoreFoamColor.rgb, saturate(shoreCoverage * _ShoreFoamColor.a));
    alpha = max(alpha, shoreCoverage * _ShoreFoamColor.a);
    if (_WATER_LAPPING_LINES)
    {
        float cycle = frac(_Time.y * _SwashSpeed + shore.g * _SwashDesync);
        half reach = _SwashRange * sin(3.14159265 * saturate(cycle / 0.6));
        half swashOpacity = _SwashOpacity * smoothstep(0, 0.15, reach) * (1 - smoothstep(0.6, 1, cycle));
        half swash = FoamLine(abs(distance - reach), _SwashWidth) * swashOpacity * detailFade;
        half wet = (1 - smoothstep(reach, reach + 0.02, distance)) * smoothstep(0, 0.15, reach) * detailFade;
        color *= 1 - wet * _SwashWetStrength;
        color = lerp(color, _SwashColor.rgb, saturate(swash * _SwashColor.a));
        alpha = max(alpha, swash * _SwashColor.a);
    }
#if !defined(_WATER_LITE)
    half shadow = 1;
    if (_WATER_SHADOWS)
    {
        shadow = MainLightRealtimeShadow(TransformWorldToShadowCoord(input.positionWS));
        color *= lerp(half3(0.7, 0.73, 0.85), half3(1, 1, 1), shadow);
    }
    if (_WATER_SPECULAR)
    {
        Light mainLight = GetMainLight();
        half3 halfDirection = SafeNormalize(mainLight.direction + view);
        half softSpec = pow(saturate(dot(normal, halfDirection)), exp2(10 * _Smoothness + 1));
        half hardSpec = step(0.5, smoothstep(0.005, 0.01, softSpec));
        half spec = lerp(softSpec, hardSpec, _Hardness);
        color += spec * _SpecularColor.rgb * mainLight.color * shadow;
        alpha = max(alpha, spec * shadow);
    }
    if (_WATER_DITHER)
    {
        float dither = frac(52.9829189 * frac(dot(input.positionCS.xy, float2(0.06711056, 0.00583715))));
        color += (dither - 0.5) / 255.0;
    }
#endif
    // Apply convergence last so specular, foam, shadows and dither all vanish at the shared seam.
    color = lerp(input.farOcean, color, detailFade);
    alpha = lerp(1, alpha, detailFade);
    return half4(color, alpha);
}
half4 WaterDepth(Varyings input) : SV_Target
{
    return 0;
}

half4 WaterDepthNormals(Varyings input) : SV_Target
{
    half3 normal = normalize(input.normalWS); // SSAO needs geometry, not texture-detail noise.
#if defined(_GBUFFER_NORMALS_OCT)
    float2 oct = PackNormalOctQuadEncode(normal);
    return half4(PackFloat2To888(saturate(oct * 0.5 + 0.5)), 0);
#else
    return half4(normal, 0);
#endif
}
#endif
