// [시작 화면 로고 - 흰 빛 연출]
// TitleScreenUI가 로고에 쓰는 셰이더입니다. 한 셰이더로 두 가지를 그립니다(_GlowMode).
// - 로고 본체(_GlowMode = 0): 스프라이트 원래 색을 _Flash만큼 흰색(_FlashColor)으로 덮습니다. 0 = 원래 색, 1 = 로고 모양 그대로 흰색.
// - 후광(_GlowMode = 1): 로고 둘레에 부드럽게 번지는 빛을 그립니다. 로고보다 넓은 칸(RawImage)에 로고를 작게 비춰서, 주변 여러 점의
//   투명도를 모아(흐림) 빛 번짐을 만듭니다. 스프라이트 범위(_UVBounds) 밖은 비어 있는 것으로 봅니다.
// 전체 투명도 = UI 색(Vertex Color)의 알파입니다.
Shader "MobArchive/UIFlash"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Flash ("Flash (0 = 원래 색, 1 = 흰색)", Range(0, 1)) = 0
        _FlashColor ("Flash Color", Color) = (1, 1, 1, 1)
        _GlowMode ("Glow Mode (0 = 로고 본체, 1 = 후광)", Float) = 0
        _GlowColor ("Glow Color", Color) = (1, 1, 1, 1)
        _GlowRadius ("Glow Radius (xy = 번짐 반경 UV)", Vector) = (0.02, 0.05, 0, 0)
        _GlowStrength ("Glow Strength", Float) = 2.5
        _UVBounds ("Sprite UV Bounds (xy = 최소, zw = 최대)", Vector) = (0, 0, 1, 1)

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
            #pragma target 3.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            sampler2D _MainTex;
            float _Flash;
            float4 _FlashColor;
            float _GlowMode;
            float4 _GlowColor;
            float4 _GlowRadius;
            float _GlowStrength;
            float4 _UVBounds;
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

            // 스프라이트 범위 밖은 투명으로 봅니다(텍스처 반복/가장자리 늘어남 방지).
            float SampleAlpha(float2 uv)
            {
                float inside = step(_UVBounds.x, uv.x) * step(uv.x, _UVBounds.z) * step(_UVBounds.y, uv.y) * step(uv.y, _UVBounds.w);
                return tex2Dlod(_MainTex, float4(uv, 0, 0)).a * inside;
            }

            float4 frag (v2f i) : SV_Target
            {
                float4 outColor;
                if (_GlowMode < 0.5)
                {
                    float4 tex = tex2D(_MainTex, i.uv);
                    float3 rgb = lerp(tex.rgb * i.color.rgb, _FlashColor.rgb, saturate(_Flash));
                    outColor = float4(rgb, tex.a * i.color.a);
                }
                else
                {
                    // 가운데 + 반경 1/3, 2/3, 1 고리마다 12방향을 모아 부드럽게 번진 투명도를 만듭니다(바깥 고리일수록 약하게).
                    float sum = SampleAlpha(i.uv);
                    float total = 1.0;
                    [unroll]
                    for (int r = 1; r <= 3; r++)
                    {
                        float radius = r / 3.0;
                        float w = 1.0 - (r - 1) * 0.3;
                        [unroll]
                        for (int k = 0; k < 12; k++)
                        {
                            float ang = (k + (r % 2) * 0.5) * (6.2831853 / 12.0);
                            float2 off = float2(cos(ang), sin(ang)) * _GlowRadius.xy * radius;
                            sum += SampleAlpha(i.uv + off) * w;
                            total += w;
                        }
                    }
                    float a = saturate(sum / total * _GlowStrength) * _GlowColor.a * i.color.a;
                    outColor = float4(_GlowColor.rgb, a);
                }

                #ifdef UNITY_UI_CLIP_RECT
                outColor.a *= UnityGet2DClipping(i.worldPos.xy, _ClipRect);
                #endif

                return outColor;
            }
            ENDCG
        }
    }
}
