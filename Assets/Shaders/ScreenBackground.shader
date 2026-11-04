// [화면 고정 배경 셰이더 - 스카이박스용]
// 블루아카이브 카페 화면처럼 "화면에 딱 붙어있는 2D 배경 이미지"를 맵 뒤 빈 공간에 그립니다.
//
// - Lighting 창의 Skybox Material 칸에 이 셰이더를 쓴 재질을 넣어서 사용합니다.
//   (카메라 Background Type은 Skybox여야 합니다.)
// - 방향이 아니라 "화면 좌표" 기준으로 이미지를 그리기 때문에, 카메라가 이동해도 배경은 움직이지 않고
//   이미지가 왜곡되지 않습니다. 맵(건물/바닥 등)이 그려지지 않은 빈 공간만 채웁니다.
// - 배경 이미지는 화면 비율에 맞춰 늘어나지 않고 꽉 채웁니다(남는 가장자리는 잘림).
// - 패턴 텍스처(작은 타일)를 화면 전체에 반복해서 겹치고, 원하면 천천히 흘러가게 할 수 있습니다.
// - 조명에는 영향을 주지 않습니다(환경광이 Color 모드이므로).
// - [레벨별 배경] "색으로 그라데이션"을 켜면 배경 이미지 대신 위 색 -> 아래 색 세로 그라데이션을 계산해서 그립니다(레벨마다 재질만 복사해
//   색을 바꾸면 됨 - LevelBackground). 끄면 배경 이미지 그대로(1레벨 BG_ScreenSky). 초상화 칸은 그리기에는 안 쓰고, 이 재질이 그 레벨
//   배경일 때 HUD 초상화 하늘 색으로 쓰입니다.
Shader "MobArchive/ScreenBackground"
{
    Properties
    {
        [Header(Background)]
        [NoScaleOffset] _BackgroundTex ("배경 이미지", 2D) = "white" {}
        _Tint ("배경 색 보정 (곱하기)", Color) = (1, 1, 1, 1)
        _Exposure ("배경 밝기", Range(0, 2)) = 1

        [Header(Color Gradient)]
        [ToggleUI] _UseColorGradient ("색으로 그라데이션 (끄면 배경 이미지)", Float) = 0
        _TopColor ("위 색", Color) = (0.08, 0.44, 0.87, 1)
        _BottomColor ("아래 색", Color) = (0.46, 0.78, 0.96, 1)
        _GradientCurve ("그라데이션 곡선 (1 = 고르게, 작을수록 위 색이 넓게)", Range(0.2, 3)) = 0.7

        [Header(HUD Portrait Sky)]
        _PortraitTopColor ("초상화 하늘 위 색", Color) = (0.40, 0.70, 0.94, 1)
        _PortraitBottomColor ("초상화 하늘 아래 색", Color) = (0.80, 0.92, 0.99, 1)

        [Header(Pattern)]
        [NoScaleOffset] _PatternTex ("패턴 타일 (투명 배경)", 2D) = "black" {}
        _PatternColor ("패턴 색", Color) = (1, 1, 1, 1)
        _PatternOpacity ("패턴 진하기 (0이면 끔)", Range(0, 1)) = 0.35
        _PatternTiles ("화면 높이당 패턴 반복 수", Range(1, 60)) = 14
        _PatternScroll ("패턴 흐르는 속도 (X, Y / 0이면 정지)", Vector) = (0, 0, 0, 0)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Background"
            "RenderType" = "Background"
            "PreviewType" = "Skybox"
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BackgroundTex);
            TEXTURE2D(_PatternTex);
            // 배경은 가장자리가 반대편과 섞이지 않게 Clamp, 패턴은 반복되게 Repeat로 읽습니다.
            SAMPLER(sampler_BgLinearClamp);
            SAMPLER(sampler_PatternLinearRepeat);

            CBUFFER_START(UnityPerMaterial)
                float4 _BackgroundTex_TexelSize; // (1/가로, 1/세로, 가로, 세로) - 유니티가 자동으로 채움
                half4 _Tint;
                half _Exposure;
                half _UseColorGradient;
                half4 _TopColor;
                half4 _BottomColor;
                half _GradientCurve;
                half4 _PortraitTopColor;
                half4 _PortraitBottomColor;
                half4 _PatternColor;
                half _PatternOpacity;
                float _PatternTiles;
                float4 _PatternScroll;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // 화면 좌표 (왼쪽 아래 0,0 ~ 오른쪽 위 1,1)
                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);

                float screenAspect = _ScaledScreenParams.x / _ScaledScreenParams.y;
                float texAspect = _BackgroundTex_TexelSize.z / max(_BackgroundTex_TexelSize.w, 1.0);

                // 이미지를 늘리지 않고 화면을 꽉 채우도록(넘치는 쪽은 잘라냄) 가운데 기준으로 맞춥니다.
                float2 bgUV = screenUV - 0.5;
                if (screenAspect > texAspect)
                {
                    bgUV.y *= texAspect / screenAspect;   // 화면이 더 넓음 → 위아래를 잘라냄
                }
                else
                {
                    bgUV.x *= screenAspect / texAspect;   // 화면이 더 좁음 → 좌우를 잘라냄
                }
                bgUV += 0.5;

                half3 color;
                if (_UseColorGradient > 0.5h)
                {
                    // 화면 아래(0) -> 위(1). 원래 배경 이미지처럼 아래에서 위로 갈수록 빠르게 진해지는 곡선(smoothstep 뒤 곡선 지수).
                    float t = pow(saturate(smoothstep(0.0, 1.0, screenUV.y)), max(_GradientCurve, 0.01));
                    color = lerp(_BottomColor.rgb, _TopColor.rgb, t) * _Tint.rgb * _Exposure;
                }
                else
                {
                    color = SAMPLE_TEXTURE2D(_BackgroundTex, sampler_BgLinearClamp, bgUV).rgb * _Tint.rgb * _Exposure;
                }

                // 패턴: 화면 높이 기준으로 일정 횟수 반복(화면 비율이 달라도 마름모가 찌그러지지 않음)
                if (_PatternOpacity > 0.0h)
                {
                    float2 patternUV = float2(screenUV.x * screenAspect, screenUV.y) * _PatternTiles;
                    patternUV += _PatternScroll.xy * _Time.y;
                    half patternAlpha = SAMPLE_TEXTURE2D(_PatternTex, sampler_PatternLinearRepeat, patternUV).a;
                    color = lerp(color, _PatternColor.rgb, patternAlpha * _PatternOpacity * _PatternColor.a);
                }

                return half4(color, 1.0h);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
