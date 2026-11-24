// [3레벨 보스 레이저 연기/먼지] BossLaserBeam이 만드는 파티클(레이저를 따라 피어오르는 연기, 보스 발밑에서 퍼지는 먼지)에 씁니다.
// 텍스처 없이 가장자리가 울퉁불퉁한 둥근 뭉치를 그리고, 색/투명도는 파티클 색(정점 색)을 그대로 씁니다. 조명 없음, 반투명(알파 블렌드).
// 뭉치 모양은 파티클마다 고정된 난수(StableRandom.x - 스크립트가 정점 스트림으로 TEXCOORD0.z에 넣음)로 정해져서 움직여도 깜빡이지 않습니다.
Shader "MobArchive/BossLaserSmoke"
{
    Properties
    {
        _Softness ("Softness", Range(0.05, 1)) = 0.7
        _Lumpiness ("Lumpiness", Range(0, 1)) = 0.35
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float _Softness;
            float _Lumpiness;

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float3 uv : TEXCOORD0; // xy = 사각형 uv, z = 파티클 고정 난수
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float seed : TEXCOORD1;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv = v.uv.xy;
                o.seed = v.uv.z * 97.0;
                return o;
            }

            float Hash2(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash2(i), Hash2(i + float2(1, 0)), u.x),
                            lerp(Hash2(i + float2(0, 1)), Hash2(i + float2(1, 1)), u.x), u.y);
            }

            float4 frag (v2f i) : SV_Target
            {
                float2 p = i.uv * 2.0 - 1.0;
                float n = ValueNoise(p * 2.5 + i.seed * 7.0) * 0.6 + ValueNoise(p * 5.0 + i.seed * 3.0) * 0.4;
                float d = length(p) + (n - 0.5) * _Lumpiness;
                float a = saturate((1.0 - d) / max(_Softness, 0.01));
                return float4(i.color.rgb, i.color.a * a);
            }
            ENDCG
        }
    }
}
