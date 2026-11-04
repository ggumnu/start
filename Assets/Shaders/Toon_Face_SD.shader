// [SD 캐릭터 얼굴(눈/입) 전용 셰이더]
// 머리 앞에 붙은 얼굴 평면(Body_Eye, Body_Mouth)에 표정 시트의 한 칸만 골라서 그립니다.
//
// 1) 표정 시트에서 칸 고르기
//    - 표정 시트는 3 x 3 칸(1800 x 1500, 칸 하나 = 머리 정면 600 x 500)이고, 칸 번호는 왼쪽 위부터 1~9입니다.
//    - "표시할 칸"(_Cell)에 번호를 넣으면 그 칸이 평면 전체에 그려집니다.
//      게임 중에는 SDFaceController가 캐릭터마다 이 값을 바꿉니다(재질 자체는 바꾸지 않음).
//
// 2) 색 바꾸기 (눈 시트 기준)
//    - 마스크 텍스처의 R = 어두운 눈동자, G = 밝은 눈동자, B = 눈썹, A(알파 채널) = 속눈썹입니다.
//      A 채널까지 쓰므로 마스크는 알파 채널이 든 TGA(32비트)로 저장합니다.
//    - 칠해진 곳만 "영역 색"이 곱해지므로, 기본 텍스처에서 색이 바뀔 부분은 흰색(명암이 있으면 회색조)으로 그립니다.
//    - 마스크를 비워두면(입 시트처럼) 그린 색 그대로 나옵니다.
//    - (외형 설정 UI 계획) 어두운 눈동자 색을 고르면 밝은 눈동자 색이 자동으로 정해지고, 밝은 색도 따로 고를 수 있게 합니다.
//
// 3) 투명 처리와 겹침
//    - 그림의 투명한 부분은 부드럽게 비쳐 보입니다(반투명 섞기). 깊이 값을 쓰지 않으므로 앞머리 같은 불투명한
//      물체 뒤에서는 자연스럽게 가려집니다.
//    - 머리 표면과 거의 붙어 있어도 깜빡이지 않도록 깊이를 아주 살짝 앞으로 당겨 그립니다(Offset).
//    - 외곽선과 그림자 드리우기는 없습니다(얼굴 그림에 외곽선이 생기거나 머리에 그림자가 지지 않게).
//
// 4) 명암
//    - 머리와 똑같은 셀 셰이딩 계산을 합니다. 명암 값(그림자 색 등)은 SDFaceController가 머리 재질에서
//      자동으로 복사해 오므로 보통은 직접 맞출 필요가 없습니다.
//
// 기존 스크립트 호환: _BaseColor(material.color)로 피격 색 변경, 렌더러 껐다 켜기 점멸(PlayerHitFlash)을 그대로 지원합니다.
Shader "MobArchive/Toon_Face_SD"
{
    Properties
    {
        [Header(Sheet)]
        [MainTexture][NoScaleOffset] _BaseMap ("표정 시트 (3 x 3)", 2D) = "white" {}
        [MainColor] _BaseColor ("기본 색 (Base Color)", Color) = (1, 1, 1, 1)
        [IntRange] _Cell ("표시할 칸 (1~9, 왼쪽 위부터)", Range(1, 9)) = 1

        [Header(Color Regions)]
        [NoScaleOffset] _MaskMap ("색 영역 마스크 (R 어두운 눈동자 / G 밝은 눈동자 / B 눈썹 / A 속눈썹, sRGB 끔)", 2D) = "black" {}
        _RegionColorR ("R 영역 색 (어두운 눈동자)", Color) = (1, 1, 1, 1)
        _RegionColorG ("G 영역 색 (밝은 눈동자)", Color) = (1, 1, 1, 1)
        _RegionColorB ("B 영역 색 (눈썹)", Color) = (1, 1, 1, 1)
        _RegionColorA ("A 영역 색 (속눈썹)", Color) = (1, 1, 1, 1)

        [Header(Toon Shading)]
        _ShadowColor ("그림자 색", Color) = (0.82, 0.74, 0.84, 1)
        _ShadowThreshold ("명암 경계 위치 (클수록 그림자 넓어짐)", Range(-1, 1)) = 0
        _ShadowSmoothness ("명암 경계 부드러움 (낮을수록 또렷)", Range(0.001, 1)) = 0.02
        _ShadeAmount ("명암 적용 정도", Range(0, 1)) = 1
        _ReceiveShadowAmount ("다른 물체 그림자 받기", Range(0, 1)) = 1
        _Brightness ("밝기", Range(0, 2)) = 1

        // PlayerHitFlash 등 기존 스크립트가 "_Color가 있는지"로 색 변경 가능 여부를 확인하기 때문에 남겨둡니다.
        [HideInInspector] _Color ("Base Color (호환용)", Color) = (1, 1, 1, 1)

        // [112번 투명] 디더 반투명 정도(1 = 평소). 코드(PlayerInvisibility)가 넣으므로 인스펙터에는 보이지 않습니다.
        [HideInInspector] _DitherFade ("Dither Fade (코드 전용)", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back
            Offset -1, -1

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Cell;
                half4 _RegionColorR;
                half4 _RegionColorG;
                half4 _RegionColorB;
                half4 _RegionColorA;
                half4 _ShadowColor;
                half _ShadowThreshold;
                half _ShadowSmoothness;
                half _ShadeAmount;
                half _ReceiveShadowAmount;
                half _Brightness;
                half4 _Color;
                half _DitherFade;
            CBUFFER_END

            // [실시간 초상화 조명 - Toon_Character_SD와 같은 전역 값, 평소엔 0]
            float _SDPortraitLightAmount;
            float4 _SDPortraitLightDir;

            // [112번 투명 - 디더 반투명] _DitherFade(1 = 평소, 0 = 완전히 안 보임)만큼의 픽셀만 남기고 나머지를 4x4 점 무늬로 버립니다.
            // 진짜 반투명과 달리 그리는 순서/겹침 문제가 없어 머리카락·몸·외곽선이 서로 비치지 않습니다. 평소(1)에는 아무것도 버리지 않습니다.
            // 값은 코드(PlayerInvisibility)가 MaterialPropertyBlock으로 넣습니다.
            static const float SDDitherBayer4x4[16] =
            {
                 0.0,  8.0,  2.0, 10.0,
                12.0,  4.0, 14.0,  6.0,
                 3.0, 11.0,  1.0,  9.0,
                15.0,  7.0, 13.0,  5.0
            };

            void SDDitherClip(float4 positionCS)
            {
                if (_DitherFade >= 0.999h) return;
                uint2 p = (uint2)fmod(floor(positionCS.xy), 4.0);
                float threshold = (SDDitherBayer4x4[p.y * 4 + p.x] + 0.5) / 16.0;
                clip(_DitherFade - threshold);
            }

            TEXTURE2D(_BaseMap);  SAMPLER(sampler_BaseMap);
            TEXTURE2D(_MaskMap);  SAMPLER(sampler_MaskMap);

            static const float SheetColumns = 3.0;
            static const float SheetRows = 3.0;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
                half   fogFactor  : TEXCOORD3;
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                output.fogFactor = ComputeFogFactor(posInputs.positionCS.z);
                return output;
            }

            // 평면의 UV(0~1)를 시트 안의 "고른 칸" 위치로 바꿉니다. 1번 칸 = 왼쪽 위.
            float2 CellUV(float2 uv)
            {
                float index = clamp(round(_Cell), 1.0, SheetColumns * SheetRows) - 1.0;
                float column = fmod(index, SheetColumns);
                float rowFromTop = floor(index / SheetColumns);
                float rowFromBottom = SheetRows - 1.0 - rowFromTop;   // 텍스처 UV는 아래쪽이 0
                float2 inCell = saturate(uv);                          // 옆 칸이 비치지 않게 칸 안으로 제한
                return float2((inCell.x + column) / SheetColumns, (inCell.y + rowFromBottom) / SheetRows);
            }

            half4 frag(Varyings input) : SV_Target
            {
                SDDitherClip(input.positionCS); // [112번 투명] 머리/몸과 같은 점 무늬로 옅어집니다.
                float2 uv = CellUV(input.uv);

                half4 baseTex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);
                half4 m = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, uv);

                // 색 영역: 칠해진 만큼 영역 색을 곱하고, 아무 채널에도 없는 곳은 그린 색 그대로
                half covered = saturate(m.r + m.g + m.b + m.a);
                half3 tint = (1.0h - covered) + m.r * _RegionColorR.rgb + m.g * _RegionColorG.rgb
                           + m.b * _RegionColorB.rgb + m.a * _RegionColorA.rgb;

                half3 albedo = baseTex.rgb * tint * _BaseColor.rgb;
                half alpha = baseTex.a * _BaseColor.a;
                clip(alpha - 0.002h);   // 완전히 투명한 부분은 아예 그리지 않음

                // 머리와 같은 셀 셰이딩 (태양 빛 + 다른 물체 그림자)
                half3 normalWS = normalize(input.normalWS);
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                half lightMax = max(max(mainLight.color.r, mainLight.color.g), mainLight.color.b);
                half3 lightHue = lightMax > 0.0001h ? mainLight.color / lightMax : half3(1, 1, 1);

                // [실시간 초상화] 초상화를 그리는 동안에는 카메라 쪽 빛으로 바꾸고 다른 물체 그림자를 없앱니다(평소엔 태양 그대로).
                half portraitAmount = saturate(_SDPortraitLightAmount);
                half3 lightDirWS = mainLight.direction;
                if (portraitAmount > 0.0h)
                    lightDirWS = SafeNormalize(lerp(float3(mainLight.direction), _SDPortraitLightDir.xyz, portraitAmount));

                half NdotL = dot(normalWS, lightDirWS);
                half ramp = smoothstep(_ShadowThreshold - _ShadowSmoothness, _ShadowThreshold + _ShadowSmoothness, NdotL);
                ramp = lerp(1.0h, ramp, _ShadeAmount);
                half castShadow = lerp(1.0h, mainLight.shadowAttenuation, _ReceiveShadowAmount * (1.0h - portraitAmount));
                half lit = ramp * castShadow;

                half3 shade = lerp(_ShadowColor.rgb, lightHue, lit);
                half3 color = albedo * shade * _Brightness;

                color = MixFog(color, input.fogFactor);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
