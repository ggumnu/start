// [아이템 도감 배경 - 모서리 삼각형 무늬]
// 아이템 도감(ItemCodexUI)의 흰 막 위에 그리는 삼각형 무늬입니다. 텍스처 없이 셰이더가 직접 정삼각형 격자를 그립니다.
// - 삼각형마다 다른 랜덤 진하기(흰색 ~ _Tint)로 칠합니다(UI_AmmoBox/UI_PlayerInventory 오른쪽 무늬와 같은 느낌).
// - 화면 왼쪽 위 / 오른쪽 아래 모서리에서 가장 진하고, 대각선 안쪽으로 _Spread(px)만큼 가면 완전히 사라집니다.
// - 반짝임: 삼각형마다 다른 박자로 아주 가끔 _GlowColor 쪽으로 진해졌다가 돌아옵니다(_GlowAmount = 0이면 반짝이지 않음).
// - 크기/거리는 모두 1920x1080 기준 px입니다(_RectSize = 이 그림이 덮는 화면 크기, 스크립트가 매 프레임 넣음).
// - 전체 투명도는 UI 색(Vertex Color)의 알파를 곱합니다 - 흰 막과 함께 나타나고 사라지도록 스크립트가 넣습니다.
Shader "MobArchive/UICodexCornerPattern"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _RectSize ("Rect Size (px)", Vector) = (1920, 1080, 0, 0)
        _TriSide ("Triangle Side (px)", Float) = 145
        _Spread ("Spread (px)", Float) = 820
        _MaxAlpha ("Max Alpha", Range(0, 1)) = 0.75
        _Tint ("Tint", Color) = (0.784, 0.894, 0.973, 1)
        _GlowColor ("Glow Color", Color) = (0.588, 0.804, 0.98, 1)
        _GlowAmount ("Glow Amount", Range(0, 1)) = 0.35
        _GlowSpeed ("Glow Speed", Float) = 0.15
        _GlowSharpness ("Glow Sharpness", Float) = 6
        _PatternTime ("Pattern Time", Float) = 0
        _Seed ("Seed", Float) = 0

        // UI 기본 속성(마스크 호환용)
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" "PreviewType" = "Plane" "CanUseSpriteAtlas" = "True" }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _RectSize;
            float _TriSide;
            float _Spread;
            float _MaxAlpha;
            float4 _Tint;
            float4 _GlowColor;
            float _GlowAmount;
            float _GlowSpeed;
            float _GlowSharpness;
            float _PatternTime;
            float _Seed;

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

            // 0~1 랜덤 값(같은 입력이면 항상 같은 값)
            float Hash2(float a, float b)
            {
                return frac(sin(a * 127.1 + b * 311.7 + _Seed * 74.7) * 43758.5453);
            }

            // 삼각형 하나의 진하기(0 = 흰색, 1 = _Tint). 원래 무늬처럼 대부분 옅고 가끔 진한 삼각형이 섞입니다.
            float TriValue(float row, float idx)
            {
                float h0 = Hash2(row, idx);
                float h1 = Hash2(row + 17.0, idx + 3.0);
                return h0 < 0.07 ? 0.95 : h1 * 0.62;
            }

            // 삼각형 하나의 지금 반짝임(0~1). 삼각형마다 박자/속도가 다르고, _GlowSharpness가 클수록 짧게 반짝입니다.
            float TriGlow(float row, float idx)
            {
                float phase = Hash2(row + 5.0, idx + 11.0);
                float speed = 0.6 + 0.8 * Hash2(row + 9.0, idx + 2.0);
                float w = sin((_PatternTime * _GlowSpeed * speed + phase) * 6.2831853) * 0.5 + 0.5;
                return pow(saturate(w), max(_GlowSharpness, 0.01));
            }

            float CornerFade(float d)
            {
                float a = saturate(1.0 - d);
                return a * a * (3.0 - 2.0 * a);
            }

            float4 frag (v2f i) : SV_Target
            {
                float2 p = i.uv * _RectSize.xy; // 왼쪽 아래 = (0, 0), px
                float side = max(_TriSide, 1.0);
                float h = side * 0.8660254;

                // 정삼각형 격자: 줄마다 반 칸씩 어긋남
                float row = floor(p.y / h);
                float fy = frac(p.y / h);
                float xs = p.x / side + (fmod(abs(row), 2.0) >= 1.0 ? 0.5 : 0.0);
                float col = floor(xs);
                float fx = frac(xs);

                // g > 0 이면 위로 뾰족한 삼각형 안, 아니면 옆의 아래로 뾰족한 삼각형
                float g = (1.0 - abs(2.0 * fx - 1.0)) - fy;
                float upIdx = 2.0 * col;
                float downIdx = fx < 0.5 ? 2.0 * col - 1.0 : 2.0 * col + 1.0;

                // 비스듬한 경계는 1px 정도 부드럽게(계단 현상 방지). g는 줄 경계에서 값이 끊기므로 fwidth(g) 대신
                // 화면 1px이 몇 px(1920 기준)인지(fwidth(p.x))로 직접 계산합니다(줄 경계에 가는 선이 생기지 않게).
                float aa = max((2.0 / side + 1.0 / h) * fwidth(p.x), 1e-5);
                float w = saturate(g / aa + 0.5);

                float value = lerp(TriValue(row, downIdx), TriValue(row, upIdx), w);
                float glow = lerp(TriGlow(row, downIdx), TriGlow(row, upIdx), w) * _GlowAmount;

                // 왼쪽 위 / 오른쪽 아래 모서리에서 대각선 안쪽으로 옅어짐
                float dTopLeft = (p.x + (_RectSize.y - p.y)) / max(_Spread, 1.0);
                float dBottomRight = ((_RectSize.x - p.x) + p.y) / max(_Spread, 1.0);
                float fade = max(CornerFade(dTopLeft), CornerFade(dBottomRight));

                float k = saturate(value + glow);
                float3 rgb = lerp(_Tint.rgb, _GlowColor.rgb, saturate(glow));
                float alpha = k * fade * _MaxAlpha * i.color.a;
                return float4(rgb * i.color.rgb, alpha);
            }
            ENDCG
        }
    }
}
