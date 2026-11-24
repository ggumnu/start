// [3레벨 보스 그로기 비콘 - 종류별 색 테두리] 87번 아군 테두리(AllyOutline)와 같은 방식(모델을 화면 기준 두께만큼 부풀려 뒷면만 그림)이지만,
// 부풀리는 방향으로 "부드럽게 맞춘 법선"(UV3 - GroggyBeacon이 메시마다 계산해 넣음)을 써서 각진 모서리에서 테두리가 갈라지지 않습니다.
// UV3가 비어 있으면(메시를 읽을 수 없는 경우) 원래 법선을 씁니다. 색/두께는 GroggyBeacon이 넣습니다.
Shader "MobArchive/BeaconOutline"
{
    Properties
    {
        [HDR] _OutlineColor ("Outline Color", Color) = (1, 1, 1, 1)
        _OutlineWidth ("Outline Width (1080p 기준 대략 픽셀)", Range(0, 10)) = 2.5
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry+10" }

        Pass
        {
            Name "BeaconOutline"
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
                float3 smoothOS   : TEXCOORD3; // 부드럽게 맞춘 법선(없으면 0)
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
                float3 n = dot(input.smoothOS, input.smoothOS) > 0.01 ? input.smoothOS : input.normalOS;
                float3 normalWS = TransformObjectToWorldNormal(n);
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
