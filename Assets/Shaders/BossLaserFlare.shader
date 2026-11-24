// [3레벨 보스 레이저 섬광] BossLaserBeam이 레이저 발사구/벽에 닿는 끝에 놓는 카메라를 향한 사각형에 그립니다. 빛을 더하기만 합니다(Blend One One).
// 가운데 밝은 점 + 회전하며 반짝이는 빛살 7개 + 넓은 빛 번짐. 텍스처 없이 계산합니다. _Fade/_Seed/_Intensity는 스크립트가 섬광마다 넣습니다.
Shader "MobArchive/BossLaserFlare"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (2.2, 1.3, 2.0, 1)
        [HDR] _GlowColor ("Glow Color", Color) = (0.6, 0.07, 0.48, 1)
        _RayCount ("Ray Count", Float) = 7
        _RaySpeed ("Ray Speed", Float) = 20
        _Intensity ("Intensity", Float) = 1
        _Seed ("Seed", Float) = 0
        _Fade ("Fade", Range(0, 1)) = 1
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

            float4 _Color;
            float4 _GlowColor;
            float _RayCount;
            float _RaySpeed;
            float _Intensity;
            float _Seed;
            float _Fade;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                float2 p = i.uv * 2.0 - 1.0;          // -1~1
                float d = length(p);
                float ang = atan2(p.y, p.x);
                float rays = 0.6 + 0.4 * pow(abs(cos(ang * _RayCount * 0.5 + _Time.y * _RaySpeed + _Seed)), 8.0);
                float edge = saturate((1.0 - d) / 0.25); // 사각형 끝에서 잘려 보이지 않게
                float3 c = exp(-pow(d / 0.45, 2.0)) * rays * _Color.rgb
                         + exp(-pow(d / 0.9, 2.0)) * _GlowColor.rgb;
                return float4(c * edge * _Intensity * _Fade, 0);
            }
            ENDCG
        }
    }
}
