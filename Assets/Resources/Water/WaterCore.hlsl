#ifndef NODE_WAR_WATER_CORE
#define NODE_WAR_WATER_CORE
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

TEXTURE2D(_ShoreTex); SAMPLER(sampler_ShoreTex);
TEXTURE2D(_ShorePrevTex); SAMPLER(sampler_ShorePrevTex);
TEXTURE2D(_FoamTex); SAMPLER(sampler_FoamTex);
TEXTURE2D(_NormalTex); SAMPLER(sampler_NormalTex);
CBUFFER_START(UnityPerMaterial)
float4 _OceanDeep, _OceanMid, _OceanShallow;
float4 _LakeDeep, _LakeMid, _LakeShallow;
float4 _InkColor, _FoamColor, _HorizonColor, _SpecColor;
float4 _BandThresholds, _ShoreST;
float4 _WaveA, _WaveB, _WaveC, _WaveD;
float4 _FoamTex_ST, _NormalTex_ST;
float _BandSoftness, _InkWidth, _FoamTiling, _FoamSpeed, _FoamCutoff, _ShoreFoamWidth;
float _ShoreBlend, _NormalStrength, _SpecHardness, _FresnelPower;
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
    return lerp(SAMPLE_TEXTURE2D(_ShorePrevTex, sampler_ShorePrevTex, uv),
        SAMPLE_TEXTURE2D(_ShoreTex, sampler_ShoreTex, uv), _ShoreBlend);
}

void Gerstner(float4 wave, float2 xz, float damping, inout float3 position,
    inout float3 tangent, inout float3 binormal)
{
    float2 direction = wave.xy / max(length(wave.xy), 0.001);
    float k = 6.2831853 / max(wave.w, 0.01);
    float phase = k * (dot(direction, xz) - sqrt(9.8 / k) * _Time.y);
    float steepness = wave.z * damping;
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
    float2 uv = input.positionWS.xz * _NormalTex_ST.xy + _NormalTex_ST.zw;
    float3 detail = UnpackNormal(SAMPLE_TEXTURE2D(_NormalTex, sampler_NormalTex, uv + _Time.y * float2(0.013, 0.009)));
#if defined(_WATER_ULTRA)
    detail.xy += UnpackNormal(SAMPLE_TEXTURE2D(_NormalTex, sampler_NormalTex,
        uv * 1.71 + _Time.y * float2(-0.009, 0.017))).xy;
#endif
    normal = normalize(normal + float3(detail.x, 0, detail.y) * _NormalStrength);
#endif
    return normal;
}

half4 WaterFragment(Varyings input) : SV_Target
{
    float4 shore = Shore(input.shoreUV);
    float lake = step(0.25, shore.b);
    float deepLake = lake * (1 - step(0.75, shore.b));
    float3 deep = lerp(_OceanDeep.rgb, _LakeDeep.rgb, lake);
    float3 mid = lerp(_OceanMid.rgb, _LakeMid.rgb, lake);
    float3 shallow = lerp(_OceanShallow.rgb, _LakeShallow.rgb, lake);
    float midBand, deepBand;
#if defined(_WATER_MEDIUM) || defined(_WATER_HIGH) || defined(_WATER_ULTRA)
    midBand = smoothstep(_BandThresholds.x - _BandSoftness, _BandThresholds.x + _BandSoftness, shore.r);
    deepBand = smoothstep(_BandThresholds.y - _BandSoftness, _BandThresholds.y + _BandSoftness, shore.r);
#else
    midBand = step(_BandThresholds.x, shore.r);
    deepBand = step(_BandThresholds.y, shore.r);
#endif
    float3 color = lerp(lerp(shallow, mid, midBand), deep, deepBand);
    color = lerp(color, deep, deepLake);
    float distance = shore.r * 3;
    float aa = max(fwidth(distance), 0.0001);
    float ink = (1 - smoothstep(_InkWidth - aa, _InkWidth + aa, distance)) * step(0.00001, distance);
    color = lerp(color, _InkColor.rgb, ink);
    float2 foamUV = input.positionWS.xz * _FoamTiling + _Time.y * _FoamSpeed * float2(1, 0.63);
#if defined(_WATER_MEDIUM) || defined(_WATER_HIGH) || defined(_WATER_ULTRA)
    foamUV += sin(foamUV.yx * 2.3 + _Time.y * 0.4) * 0.08;
#endif
    float3 foam = SAMPLE_TEXTURE2D(_FoamTex, sampler_FoamTex, foamUV).rgb;
    float coverage = step(_FoamCutoff, foam.r);
#if defined(_WATER_MEDIUM) || defined(_WATER_HIGH) || defined(_WATER_ULTRA)
    coverage = max(coverage, (1 - smoothstep(_ShoreFoamWidth - aa, _ShoreFoamWidth + aa, distance))
        * step(0.35, foam.g + (foam.b - 0.5) * 0.2));
    float3 normal = WaterNormal(input);
    float3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
    Light mainLight = GetMainLight();
    float3 halfDirection = SafeNormalize(mainLight.direction + view);
    float spec = step(_SpecHardness, pow(saturate(dot(normal, halfDirection)), 32.0));
    color += spec * _SpecColor.rgb * mainLight.color;
    float fresnel = pow(1 - saturate(dot(normal, view)), _FresnelPower);
    color = lerp(color, _HorizonColor.rgb, fresnel);
#if defined(_WATER_HIGH) || defined(_WATER_ULTRA)
    float shadow = MainLightRealtimeShadow(TransformWorldToShadowCoord(input.positionWS));
    color *= lerp(float3(0.7, 0.73, 0.85), float3(1, 1, 1), shadow);
#endif
#if defined(_WATER_ULTRA)
    half3 sky = GlossyEnvironmentReflection(reflect(-view, normal), 0.15h, 1.0h);
    color = lerp(color, sky, fresnel * 0.35);
#endif
#endif
    color = lerp(color, _FoamColor.rgb, coverage * (1 - deepLake));
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
