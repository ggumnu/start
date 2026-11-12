// [공용 UI 상자 - 흰 창 / 평행사변형 버튼]
// UIPatternBox 컴포넌트가 쓰는 셰이더입니다. 텍스처 없이 계산으로 둥근 사각형(창) 또는 둥근 평행사변형(버튼)을 그리고,
// 그 안의 왼쪽 위 / 오른쪽 아래 모서리에 삼각형 무늬(아이템 도감 배경 UICodexCornerPattern과 같은 계산)를 깔고, 아래에 옅은 그림자를 그립니다.
// - 좌표는 상자 중심 기준 px(1920x1080 기준)입니다. 메시는 그림자가 들어갈 만큼 넓게 그려지고(UIPatternBox가 넣음), 모양 밖은 투명합니다.
// - 바탕색/전체 투명도 = UI 색(Vertex Color). 무늬/그림자 값은 상자마다 머티리얼 값으로 들어옵니다.
// - 그라데이션을 켜면 바탕색이 왼쪽 위(UI 색) → 오른쪽 아래(_GradientColor)로 대각선으로 바뀝니다(상점 등급 칸/카드).
// - 무늬는 상자 모양 안에서만 보입니다(둥근 모서리 밖으로 삐져나가지 않음).
Shader "MobArchive/UIPatternBox"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Size ("Size (px)", Vector) = (200, 60, 0, 0)
        _Shape ("Shape (x = 모서리 둥글기 px, y = 기울기 tan)", Vector) = (10, 0, 0, 0)
        _Pattern ("Pattern (x = 삼각형 변 px, y = 퍼짐 px, z = 최대 불투명도, w = 켜기)", Vector) = (24, 80, 0.7, 1)
        _PatternCorners ("Pattern Corners (xy = 왼쪽 위 기준점 이동, zw = 오른쪽 아래 기준점 이동 px)", Vector) = (0, 0, 0, 0)
        _PatternSpreads ("Pattern Spreads (x = 왼쪽 위 퍼짐 px, y = 오른쪽 아래 퍼짐 px)", Vector) = (80, 80, 0, 0)
        _PatternTint ("Pattern Tint", Color) = (1, 1, 1, 1)
        _GradientColor ("Gradient End Color (오른쪽 아래)", Color) = (1, 1, 1, 1)
        _Gradient ("Gradient (x = 켜기)", Vector) = (0, 0, 0, 0)
        _GlowColor ("Glow Color", Color) = (0.588, 0.804, 0.98, 1)
        _Glow ("Glow (x = 세기, y = 속도, z = 날카로움, w = 시간)", Vector) = (0, 0.15, 6, 0)
        _Seed ("Seed", Float) = 0
        _ShadowColor ("Shadow Color", Color) = (0.106, 0.165, 0.251, 1)
        _Shadow ("Shadow (xy = 오프셋 px, z = 번짐 px, w = 불투명도)", Vector) = (0, -4, 8, 0.3)

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
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            float4 _Size;
            float4 _Shape;
            float4 _Pattern;
            float4 _PatternCorners;
            float4 _PatternSpreads;
            float4 _PatternTint;
            float4 _GradientColor;
            float4 _Gradient;
            float4 _GlowColor;
            float4 _Glow;
            float _Seed;
            float4 _ShadowColor;
            float4 _Shadow;
            float4 _ClipRect;

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
                float4 worldPos : TEXCOORD1;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.worldPos = v.vertex;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv = v.uv;
                return o;
            }

            // 둥근 사각형까지의 거리(안쪽 음수). 평행사변형은 기울기만큼 x를 되돌린 좌표로 넣습니다.
            float SdRoundBox(float2 p, float2 halfSize, float r)
            {
                r = min(r, min(halfSize.x, halfSize.y));
                float2 q = abs(p) - (halfSize - r);
                return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r;
            }

            float ShapeDistance(float2 p)
            {
                float t = _Shape.y;
                float hh = _Size.y * 0.5;
                float bw = max((_Size.x - _Size.y * abs(t)) * 0.5, 1.0); // 평행사변형 전체 폭이 _Size.x에 들어가도록
                return SdRoundBox(float2(p.x - p.y * t, p.y), float2(bw, hh), _Shape.x);
            }

            // ---- 삼각형 무늬 (UICodexCornerPattern과 같은 계산) ----

            float Hash2(float a, float b)
            {
                return frac(sin(a * 127.1 + b * 311.7 + _Seed * 74.7) * 43758.5453);
            }

            float TriValue(float row, float idx)
            {
                float h0 = Hash2(row, idx);
                float h1 = Hash2(row + 17.0, idx + 3.0);
                return h0 < 0.07 ? 0.95 : h1 * 0.62;
            }

            float TriGlow(float row, float idx)
            {
                float phase = Hash2(row + 5.0, idx + 11.0);
                float speed = 0.6 + 0.8 * Hash2(row + 9.0, idx + 2.0);
                float w = sin((_Glow.w * _Glow.y * speed + phase) * 6.2831853) * 0.5 + 0.5;
                return pow(saturate(w), max(_Glow.z, 0.01));
            }

            float CornerFade(float d)
            {
                float a = saturate(1.0 - d);
                return a * a * (3.0 - 2.0 * a);
            }

            // 무늬 진하기(k)와 반짝임(glow)을 돌려줍니다. p = 상자 왼쪽 아래 기준 px.
            float2 Pattern(float2 p)
            {
                float side = max(_Pattern.x, 1.0);
                float h = side * 0.8660254;

                float row = floor(p.y / h);
                float fy = frac(p.y / h);
                float xs = p.x / side + (fmod(abs(row), 2.0) >= 1.0 ? 0.5 : 0.0);
                float col = floor(xs);
                float fx = frac(xs);

                float g = (1.0 - abs(2.0 * fx - 1.0)) - fy;
                float upIdx = 2.0 * col;
                float downIdx = fx < 0.5 ? 2.0 * col - 1.0 : 2.0 * col + 1.0;

                float aa = max((2.0 / side + 1.0 / h) * fwidth(p.x), 1e-5);
                float w = saturate(g / aa + 0.5);

                float value = lerp(TriValue(row, downIdx), TriValue(row, upIdx), w);
                float glow = lerp(TriGlow(row, downIdx), TriGlow(row, upIdx), w) * _Glow.x;

                // 모서리 기준점(_PatternCorners로 상자마다 옮길 수 있음)에서 대각선 방향으로 멀어질수록 옅어짐
                float dTopLeft = ((p.x - _PatternCorners.x) + (_Size.y + _PatternCorners.y - p.y)) / max(_PatternSpreads.x, 0.01);
                float dBottomRight = ((_Size.x + _PatternCorners.z - p.x) + (p.y - _PatternCorners.w)) / max(_PatternSpreads.y, 0.01);
                float fade = max(CornerFade(dTopLeft), CornerFade(dBottomRight));

                return float2(saturate(value + glow) * fade * _Pattern.z, saturate(glow));
            }

            float4 frag (v2f i) : SV_Target
            {
                float2 p = i.uv; // 상자 중심 기준 px

                // 상자
                float d = ShapeDistance(p);
                float aa = max(fwidth(d), 1e-4);
                float cover = saturate(0.5 - d / aa);

                float3 rgb = i.color.rgb;
                if (_Gradient.x > 0.5)
                {
                    // 왼쪽 위(0) → 오른쪽 아래(1) 대각선
                    float2 q = (p + _Size.xy * 0.5) / max(_Size.xy, 1.0);
                    float g = saturate((q.x + (1.0 - q.y)) * 0.5);
                    rgb = lerp(rgb, _GradientColor.rgb, g);
                }
                if (_Pattern.w > 0.5)
                {
                    float2 pk = Pattern(p + _Size.xy * 0.5);
                    float3 tint = lerp(_PatternTint.rgb, _GlowColor.rgb, pk.y);
                    rgb = lerp(rgb, tint, pk.x);
                }
                float boxA = cover * i.color.a;

                // 그림자(상자 아래에만 보임)
                float shadowA = 0.0;
                if (_Shadow.w > 0.0)
                {
                    float ds = ShapeDistance(p - _Shadow.xy);
                    shadowA = pow(saturate(1.0 - (ds + 1.0) / max(_Shadow.z, 0.01)), 1.5) * _Shadow.w * i.color.a;
                }

                float outA = boxA + shadowA * (1.0 - boxA);
                float3 outRgb = (rgb * boxA + _ShadowColor.rgb * shadowA * (1.0 - boxA)) / max(outA, 1e-4);

                #ifdef UNITY_UI_CLIP_RECT
                outA *= UnityGet2DClipping(i.worldPos.xy, _ClipRect);
                #endif

                return float4(outRgb, outA);
            }
            ENDCG
        }
    }
}
