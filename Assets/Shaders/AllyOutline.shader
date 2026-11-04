// [87번 아군 - 하늘색 테두리]
// 포섭된 아군의 모델에 재질로 덧붙여, 캐릭터 셰이더의 검은 외곽선처럼 모델 둘레에 하늘색 테두리를 그립니다(살짝 발광).
// 모델을 표면 방향으로 화면 기준 두께만큼 부풀린 뒤 "뒷면"만 그려서, 원래 모델 바깥으로 삐져나온 부분만 테두리로 보입니다.
// 적이 쓰는 셰이더와 상관없이 덧붙일 수 있습니다(AllyFaction이 렌더러의 재질 목록 맨 뒤에 추가).
//
// [재질 만드는 법]
// Project 창에서 이 셰이더를 우클릭 > Create > Material. 만든 재질을 87번 Item Definition의 Outline Material 칸에 넣습니다.
// 색/두께는 Item Definition의 값이 덮어씁니다.
Shader "MobArchive/AllyOutline"
{
    Properties
    {
        [HDR] _OutlineColor ("Outline Color (Item Definition 값이 덮어씀)", Color) = (0.45, 0.9, 1.25, 1)
        _OutlineWidth ("Outline Width (1080p 기준 대략 픽셀, Item Definition 값이 덮어씀)", Range(0, 10)) = 2.5
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry+10" }

        Pass
        {
            Name "AllyOutline"
            Tags { "LightMode" = "UniversalForward" }

            Cull Front
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _OutlineColor;
                float _OutlineWidth;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
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

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float4 positionCS = TransformWorldToHClip(positionWS);

                // 화면 위에서의 표면 방향으로 밀어냅니다(화면 비율 보정 포함) - Toon_Character_SD 외곽선과 같은 방식.
                float2 dirCS = TransformWorldToHClipDir(normalWS).xy;
                float dirLen = max(length(dirCS), 0.00001);
                dirCS /= dirLen;
                dirCS.x *= _ScreenParams.y / _ScreenParams.x;
                positionCS.xy += dirCS * (_OutlineWidth / 540.0) * positionCS.w;

                output.positionCS = positionCS;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
