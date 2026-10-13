Shader "NodeWar/UI/PaintCoverage"
{
    Properties { _MainTex ("Coverage", 2D) = "white" {} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        CGINCLUDE
        #include "UnityCG.cginc"
        sampler2D _MainTex;
        float4 _Segment, _CoverageRect, _Tint, _OutlineStep;
        float _Radius;
        ENDCG
        // Opaque disc/capsule stamp. Discard leaves previous union pixels intact.
        Pass
        {
            Blend Off
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment stamp
            fixed4 stamp(v2f_img i) : SV_Target
            {
                float2 p = _CoverageRect.xy + i.uv * _CoverageRect.zw;
                float2 a = _Segment.xy, delta = _Segment.zw - a;
                float lengthSq = dot(delta, delta);
                float t = lengthSq > 0 ? saturate(dot(p - a, delta) / lengthSq) : 0;
                float2 offset = p - a - t * delta;
                clip(_Radius * _Radius - dot(offset, offset));
                return 1;
            }
            ENDCG
        }
        // Composite once. The outline follows only the outside of the union.
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment composite
            float coverage(float2 uv)
            {
                if (any(uv < 0) || any(uv > 1)) return 0;
                return tex2D(_MainTex, uv).r;
            }
            fixed4 composite(v2f_img i) : SV_Target
            {
                float inside = coverage(i.uv);
                float outside = 0;
                for (int y = -1; y <= 1; y++)
                    for (int x = -1; x <= 1; x++)
                        outside = max(outside, coverage(i.uv + float2(x, y) * _OutlineStep.xy));
                float outline = max(0, outside - inside);
                return fixed4(_Tint.rgb, _Tint.a * (inside * 0.22 + outline));
            }
            ENDCG
        }
    }
    Fallback Off
}
