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
float4 _HorizonColor, _SurfaceFoamColor, _IntersectionFoamColor, _InkColor, _SpecularColor;
float4 _WaveA, _WaveB, _WaveC, _WaveD, _ShoreST;
float4 _SurfaceFoamTex_ST, _IntersectionFoamTex_ST, _NormalsTex_ST;
float _DepthRange, _ColorSteps, _HorizonDistance;
float _SurfaceFoamTiling, _SurfaceFoamDirection, _SurfaceFoamSpeed, _SurfaceFoamCutoff, _SurfaceFoamDistortion;
float _IntersectionFoamDepth, _IntersectionFoamCutoff, _IntersectionFoamFade, _IntersectionFoamTiling, _IntersectionFoamSpeed;
float _InkWidth, _NormalsScale, _NormalsSpeed, _NormalsStrength;
float _Smoothness, _Hardness, _WaveSpeed, _WaveSteepnessScale, _ReflectionStrength, _ShoreBlend;
CBUFFER_END
struct Attributes { float4 positionOS : POSITION; };
struct Varyings
{
    float4 positionCS : SV_POSITION;
    float3 positionWS : TEXCOORD0;
    float3 normalWS : TEXCOORD1;
    float2 shoreUV : TEXCOORD2;
};

float4 Shore(float2 uv)
{
    if (_ShoreBlend >= 1.0) return SAMPLE_TEXTURE2D(_ShoreTex, sampler_ShoreTex, uv);
    return lerp(SAMPLE_TEXTURE2D(_ShorePrevTex, sampler_ShorePrevTex, uv),
        SAMPLE_TEXTURE2D(_ShoreTex, sampler_ShoreTex, uv), _ShoreBlend);
}

void Gerstner(float4 wave, float2 xz, float damping, inout float3 position,
    inout float3 tangent, inout float3 binormal)
{
    float2 direction = wave.xy / max(length(wave.xy), 0.001);
    float k = 6.2831853 / max(wave.w, 0.01);
    float phase = k * (dot(direction, xz) - sqrt(9.8 / k) * _WaveSpeed * _Time.y);
    float steepness = wave.z * _WaveSteepnessScale * damping;
    float amplitude = steepness / k;
    float s = sin(phase), c = cos(phase);
    position += float3(direction.x * amplitude * c, amplitude * s, direction.y * amplitude * c);
    tangent += float3(-direction.x * direction.x * steepness * s, direction.x * steepness * c,
        -direction.x * direction.y * steepness * s);
    binormal += float3(-direction.x * direction.y * steepness * s, direction.y * steepness * c,
        -direction.y * direction.y * steepness * s);
}

Varyings WaterVertex(Attributes input)
{
    Varyings output;
    float3 position = TransformObjectToWorld(input.positionOS.xyz);
    float2 xz = position.xz;
    output.shoreUV = (xz - _ShoreST.xy) * _ShoreST.zw;
    float3 tangent = float3(1, 0, 0), binormal = float3(0, 0, 1);
#if defined(_WATER_MEDIUM) || defined(_WATER_HIGH) || defined(_WATER_ULTRA)
    float damping = lerp(SAMPLE_TEXTURE2D_LOD(_ShorePrevTex, sampler_ShorePrevTex, output.shoreUV, 0).g,
        SAMPLE_TEXTURE2D_LOD(_ShoreTex, sampler_ShoreTex, output.shoreUV, 0).g, _ShoreBlend);
    Gerstner(_WaveA, xz, damping, position, tangent, binormal);
    Gerstner(_WaveB, xz, damping, position, tangent, binormal);
#if defined(_WATER_HIGH) || defined(_WATER_ULTRA)
    Gerstner(_WaveC, xz, damping, position, tangent, binormal);
    Gerstner(_WaveD, xz, damping, position, tangent, binormal);
#endif
#endif
    output.positionWS = position;
    output.normalWS = normalize(cross(binormal, tangent));
    output.positionCS = TransformWorldToHClip(position);
    return output;
}

float3 WaterNormal(Varyings input)
{
    float3 normal = normalize(input.normalWS);
#if defined(_WATER_HIGH) || defined(_WATER_ULTRA)
    float2 uv = input.positionWS.xz * _NormalsScale;
    float3 detail = UnpackNormal(SAMPLE_TEXTURE2D(_NormalsTex, sampler_NormalsTex,
        (uv + _Time.y * _NormalsSpeed * float2(1, 0.63)) * _NormalsTex_ST.xy + _NormalsTex_ST.zw));
#if defined(_WATER_ULTRA)
    float3 second = UnpackNormal(SAMPLE_TEXTURE2D(_NormalsTex, sampler_NormalsTex,
        (uv * 1.71 + _Time.y * _NormalsSpeed * float2(-0.7, 1.2)) * _NormalsTex_ST.xy + _NormalsTex_ST.zw));
    detail = normalize(float3(detail.xy + second.xy, detail.z * second.z));
#endif
    detail = normalize(float3(detail.xy * _NormalsStrength, detail.z));
    float3 tangent = normalize(float3(normal.y, -normal.x, 0));
    float3 binormal = cross(tangent, normal);
    normal = normalize(tangent * detail.x + binormal * detail.y + normal * detail.z);
#endif
    return normal;
}

float3 WaterRGBToHSV(float3 rgb)
{
    float4 k = float4(0, -1.0 / 3.0, 2.0 / 3.0, -1);
    float4 p = lerp(float4(rgb.bg, k.wz), float4(rgb.gb, k.xy), step(rgb.b, rgb.g));
    float4 q = lerp(float4(p.xyw, rgb.r), float4(rgb.r, p.yzx), step(p.x, rgb.r));
    float d = q.x - min(q.w, q.y);
    return float3(abs(q.z + (q.w - q.y) / (6 * d + 1e-6)), d / (q.x + 1e-6), q.x);
}

float3 WaterHSVToRGB(float3 hsv)
{
    float3 p = abs(frac(hsv.xxx + float3(1, 2.0 / 3.0, 1.0 / 3.0)) * 6 - 3);
    return hsv.z * lerp(float3(1, 1, 1), saturate(p - 1), hsv.y);
}

float3 WaterColor(float3 shallow, float3 deep, float depth)
{
#if defined(_WATER_MEDIUM) || defined(_WATER_HIGH) || defined(_WATER_ULTRA)
    float3 a = WaterRGBToHSV(shallow), b = WaterRGBToHSV(deep);
    float hueDelta = frac(b.x - a.x + 0.5) - 0.5;
    return WaterHSVToRGB(float3(frac(a.x + hueDelta * depth), lerp(a.yz, b.yz, depth)));
#else
    return lerp(shallow, deep, depth);
#endif
}

float3 FoamOverlay(float3 baseColor, float4 foamColor, float coverage)
{
    float3 overlay = lerp(2 * baseColor * foamColor.rgb,
        1 - 2 * (1 - baseColor) * (1 - foamColor.rgb), step(0.5, baseColor));
    return lerp(baseColor, overlay, saturate(coverage * foamColor.a));
}

float2 DistortFoam(float2 uv)
{
    float t = _Time.y, amount = _SurfaceFoamDistortion;
    uv.y += amount * 0.01 * (sin(uv.x * 3.5 + t * 0.35) + sin(uv.x * 4.8 + t * 1.05) + sin(uv.x * 7.3 + t * 0.45)) / 3;
    uv.x += amount * 0.12 * (sin(uv.y * 4 + t * 0.50) + sin(uv.y * 6.8 + t * 0.75) + sin(uv.y * 11.3 + t * 0.2)) / 3;
    uv.y += amount * 0.12 * (sin(uv.x * 4.2 + t * 0.64) + sin(uv.x * 6.3 + t * 1.65) + sin(uv.x * 8.2 + t * 0.45)) / 3;
    return uv;
}

half4 WaterFragment(Varyings input) : SV_Target
{
    float4 shore = Shore(input.shoreUV);
    float lake = step(0.25, shore.b);
    float deepLake = lake * (1 - step(0.75, shore.b));
    float3 shallow = lerp(lerp(_OceanShallowColor.rgb, _LakeShallowColor.rgb, lake), _DeepLakeShallowColor.rgb, deepLake);
    float3 deep = lerp(lerp(_OceanDeepColor.rgb, _LakeDeepColor.rgb, lake), _DeepLakeDeepColor.rgb, deepLake);
    float distance = shore.r * 3;
    float depth = saturate(distance / max(_DepthRange, 0.001));
    if (_ColorSteps >= 2) depth = floor(depth * (_ColorSteps - 1) + 0.5) / (_ColorSteps - 1);
    float3 color = WaterColor(shallow, deep, depth);
#if defined(_WATER_MEDIUM) || defined(_WATER_HIGH) || defined(_WATER_ULTRA)
    float3 normal = WaterNormal(input);
    float3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
    float fresnel = pow(1 - saturate(dot(normal, view)), max(_HorizonDistance, 0.001));
    color = lerp(color, _HorizonColor.rgb, fresnel);
#endif
    float angle = _SurfaceFoamDirection * 6.2831853;
    float2 foamUV = input.positionWS.xz * _SurfaceFoamTiling
        + _Time.y * _SurfaceFoamSpeed * float2(cos(angle), sin(angle));
#if defined(_WATER_MEDIUM) || defined(_WATER_HIGH) || defined(_WATER_ULTRA)
    foamUV = DistortFoam(foamUV);
#endif
    float surface = SAMPLE_TEXTURE2D(_SurfaceFoamTex, sampler_SurfaceFoamTex,
        foamUV * _SurfaceFoamTex_ST.xy + _SurfaceFoamTex_ST.zw).r;
    color = FoamOverlay(color, _SurfaceFoamColor, step(_SurfaceFoamCutoff, surface));
    float fade = saturate(distance / max(_IntersectionFoamDepth, 0.001));
    float2 intersectionUV = input.positionWS.xz * _IntersectionFoamTiling
        + _Time.y * _IntersectionFoamSpeed * float2(-0.63, 1);
    float intersection = SAMPLE_TEXTURE2D(_IntersectionFoamTex, sampler_IntersectionFoamTex,
        intersectionUV * _IntersectionFoamTex_ST.xy + _IntersectionFoamTex_ST.zw).g;
    float coverage = step(_IntersectionFoamCutoff * fade, intersection)
        * pow(1 - fade, max(_IntersectionFoamFade, 0.001));
    color = FoamOverlay(color, _IntersectionFoamColor, coverage);
    float aa = max(fwidth(distance), 0.0001);
    float ink = (1 - smoothstep(_InkWidth - aa, _InkWidth + aa, distance)) * step(0.00001, _InkWidth);
    color = lerp(color, _InkColor.rgb, ink * _InkColor.a);
#if defined(_WATER_MEDIUM) || defined(_WATER_HIGH) || defined(_WATER_ULTRA)
    Light mainLight = GetMainLight();
    float shadow = 1;
#if defined(_WATER_HIGH) || defined(_WATER_ULTRA)
    shadow = MainLightRealtimeShadow(TransformWorldToShadowCoord(input.positionWS));
    color *= lerp(float3(0.7, 0.73, 0.85), float3(1, 1, 1), shadow);
#endif
    float3 halfDirection = SafeNormalize(mainLight.direction + view);
    float softSpec = pow(saturate(dot(normal, halfDirection)), exp2(10 * _Smoothness + 1));
    float hardSpec = step(0.5, smoothstep(0.005, 0.01, softSpec));
    float spec = lerp(softSpec, hardSpec, _Hardness);
    color += spec * _SpecularColor.rgb * mainLight.color * shadow;
#if defined(_WATER_ULTRA)
    half3 sky = GlossyEnvironmentReflection(reflect(-view, normal), 0.15h, 1.0h);
    color = lerp(color, sky, fresnel * _ReflectionStrength);
#endif
#endif
    return half4(color, 1);
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
