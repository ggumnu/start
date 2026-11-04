// [110번 관통 강화 총알 - 피격 굴절]
// 맞은 적 몸통 앞에 카메라를 향하는 판(ChargedHitRipple.cs가 만듦)을 띄우고, 판 가운데에서 바깥으로 퍼지는 고리 + 잔물결 모양으로
// "그 뒤의 화면"을 비틀어 다시 그립니다. 그래서 적 몸이 출렁 굴절됐다가 원래대로 돌아오는 것처럼 보입니다(90번 물결 굴절과 같은 원리).
// - _Progress(0 → 1): 퍼진 정도. 고리 반경 = _Progress x 0.5(판 가장자리). 코드가 매 프레임 넣습니다.
// - _Strength: 굴절 세기(화면 비율). 코드가 충전 수에 맞춰 넣습니다.
// - 판 가장자리(원 밖)는 그리지 않아 네모가 보이지 않습니다.
// URP 설정 에셋의 Rendering > Opaque Texture가 켜져 있어야 합니다(90번 굴절과 같음 - 꺼져 있으면 굴절 자리가 검게/회색으로 보임).
//
// [재질 만드는 법] 메뉴 MobArchive/VFX/110번 피격 굴절 재질 만들기 (ChargedHitRippleBuilder.cs)가 자동으로 만들고 110번 에셋에 넣어 줍니다.
// 직접 만들려면 이 셰이더를 우클릭 > Create > Material.
Shader "MobArchive/HitRipple"
{
    Properties
    {
        // 아래 값들은 110번 Item Definition 설정으로 코드가 넣습니다(재질 인스펙터에서는 숨김).
        [HideInInspector] _Progress ("Progress", Float) = 0
        [HideInInspector] _Strength ("Strength", Float) = 0.03
        [HideInInspector] _Fade ("Fade", Float) = 1
        [HideInInspector] _RingWidth ("Ring Width", Float) = 0.12
        [HideInInspector] _RippleCount ("Ripple Count", Float) = 2
        [HideInInspector] _RippleSpacing ("Ripple Spacing", Float) = 0.12
        [HideInInspector] _RippleFalloff ("Ripple Falloff", Float) = 0.55
        [HideInInspector] [HDR] _Color ("Tint", Color) = (0.8, 0.6, 1.4, 0.15)
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "HitRipple"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Progress;
                float _Strength;
                float _Fade;
                float _RingWidth;
                float _RippleCount;
                float _RippleSpacing;
                float _RippleFalloff;
                half4 _Color;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.uv = input.uv;
                o.screenPos = ComputeScreenPos(p.positionCS);
                return o;
            }

            // 반경 r에 있는 고리 하나의 세기(0~1). 바깥쪽은 날카롭고 안쪽으로 부드럽게 꼬리가 남습니다(90번 물결과 같은 모양).
            float RingProfile(float d, float r, float w)
            {
                float x = d - r;
                float outside = exp(-(x * x) / max(w * w * 0.12, 1e-5));
                float inside = saturate(1.0 + x / max(w * 3.0, 1e-4));
                inside *= inside;
                return x > 0.0 ? outside : inside;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 local = i.uv - 0.5;          // 판 중심 기준(-0.5 ~ 0.5)
                float d = length(local);
                float r = _Progress * 0.5;

                float wave = RingProfile(d, r, _RingWidth);
                float amp = 1.0;
                [unroll]
                for (int k = 1; k <= 3; k++)
                {
                    amp *= _RippleFalloff;
                    float rk = r - k * _RippleSpacing;
                    float use = step((float)k, _RippleCount + 0.5) * step(0.0, rk);
                    wave += RingProfile(d, max(rk, 0.0), _RingWidth * 0.7) * amp * use;
                }
                wave = saturate(wave);

                // 판 가장자리(원 밖)는 지웁니다.
                float edge = saturate((0.5 - d) / 0.08);
                float a = wave * edge * saturate(_Fade);

                // 판은 카메라를 향하므로 판의 가로/세로가 화면의 가로/세로와 같습니다 - 중심에서 바깥 방향으로 화면을 밀어 다시 그립니다.
                float2 dir = d > 1e-4 ? local / d : float2(0.0, 0.0);
                float2 uv = i.screenPos.xy / i.screenPos.w;
                float2 distUV = uv + dir * (_Strength * a);
                half3 scene = SampleSceneColor(distUV);
                half3 col = scene + _Color.rgb * _Color.a * a;
                return half4(col, saturate(a * 3.0));
            }
            ENDHLSL
        }
    }
    FallBack Off
}
