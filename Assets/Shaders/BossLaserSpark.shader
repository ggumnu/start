// [3레벨 보스 레이저 차지 빛 입자] BossLaserBeam이 조준선 동안 발사 지점으로 빨려 들어가는 빛 입자(파티클)에 씁니다. 빛을 더하기만 합니다(Blend One One).
// 파티클 색(정점 색 - 색/투명도)으로 밝기를 정하고, 가운데가 밝은 둥근 빛으로 그립니다. 텍스처 없이 계산합니다.
Shader "MobArchive/BossLaserSpark"
{
    Properties
    {
        _Intensity ("Intensity", Float) = 1
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }

        Pass
        {
            Blend One One
            ZWrite Off
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float _Intensity;

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv = v.uv;
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                float2 p = i.uv * 2.0 - 1.0;
                float d = length(p);
                float core = exp(-pow(d / 0.35, 2.0));
                float halo = exp(-pow(d / 0.8, 2.0)) * 0.35;
                float edge = saturate((1.0 - d) / 0.2);
                float3 c = i.color.rgb * (core + halo) * edge * i.color.a * _Intensity;
                return float4(c, 0);
            }
            ENDCG
        }
    }
}
