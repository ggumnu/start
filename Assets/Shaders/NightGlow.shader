// [밤 전용 빛 - NightLight.cs가 만드는 판/원뿔에 씁니다]
// 조명을 받지 않고 스스로 빛나는 반투명 "더하기" 셰이더입니다(겹칠수록 밝아짐, HDR 색이라 URP Bloom으로 은은하게 번짐).
// 레벨 조명(밤 어둡게)의 영향을 받지 않습니다. 모양은 텍스처 없이 UV로 계산합니다(_Shape - 판마다 MaterialPropertyBlock으로 정함).
//   0 = 사각 판: 가장자리가 _Softness만큼 부드럽게 옅어짐(창문 유리/자판기 스크린은 작게, 바닥 번짐은 크게). _FadeV만큼 v 방향으로 옅어짐.
//   1 = 원형 판: 가운데가 가장 밝고 바깥으로 옅어짐(가로등 바닥 빛 웅덩이, 전구 빛).
//   2 = 원뿔: 위(v=0)가 가장 밝고 아래로 옅어지며, 옆면이 화면과 비스듬할수록 옅어져 가장자리가 부드러움(가로등 빛줄기).
Shader "MobArchive/NightGlow"
{
    Properties
    {
        [HDR] _Color ("빛 색", Color) = (1, 0.85, 0.55, 1)
        _Intensity ("세기", Range(0, 8)) = 1
        _Shape ("모양 (0 사각 / 1 원형 / 2 원뿔)", Float) = 0
        _Softness ("사각 판 가장자리 부드러움", Range(0.001, 0.5)) = 0.05
        _FadeV ("사각 판 v 방향 옅어짐", Range(0, 1)) = 0
        _Falloff ("원형/원뿔 옅어지는 빠르기", Range(0.2, 6)) = 1.5
        _EdgePower ("원뿔 옆 가장자리 옅어짐", Range(0.1, 6)) = 1.5
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "NightGlow"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Intensity;
                half _Shape;
                half _Softness;
                half _FadeV;
                half _Falloff;
                half _EdgePower;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
            };

            Varyings vert(Attributes input)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.uv = input.uv;
                return o;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                half a = 1.0h;

                if (_Shape < 0.5h)
                {
                    // 사각 판: 가장 가까운 가장자리까지의 거리로 부드럽게.
                    half e = min(min(uv.x, 1.0h - uv.x), min(uv.y, 1.0h - uv.y));
                    a = smoothstep(0.0h, _Softness, e);
                    a *= lerp(1.0h, 1.0h - uv.y, _FadeV);
                }
                else if (_Shape < 1.5h)
                {
                    // 원형 판: 가운데에서 바깥으로.
                    half d = length(uv * 2.0h - 1.0h);
                    a = pow(saturate(1.0h - d), _Falloff);
                }
                else
                {
                    // 원뿔: 위에서 아래로 + 옆 가장자리.
                    half3 viewDir = normalize(GetWorldSpaceViewDir(input.positionWS));
                    half facing = abs(dot(normalize(input.normalWS), viewDir));
                    a = pow(saturate(1.0h - uv.y), _Falloff) * pow(facing, _EdgePower);
                }

                half3 col = _Color.rgb * _Intensity * a;
                return half4(col, 1.0h);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
