// [SD 캐릭터용 툰 셰이더 - 새 SD 캐릭터(알바디 + 교체 파츠) 전용]
// 기존 "MobArchive/Toon_Character"를 복사해서 만든 셰이더입니다. 명암/림라이트/외곽선/그림자 동작은 원본과
// 완전히 같고, 아래 두 가지만 추가했습니다. 원본 셰이더와 레이사 등 기존 캐릭터 재질은 전혀 건드리지 않습니다.
//
// 1) 색 영역 바꾸기 (RGBA 마스크)
//    - 기본 텍스처(Base Map)에서 색이 바뀔 부분은 흰색으로, 주름선/봉제선 같은 고정 디테일은 회색/검정으로 그립니다.
//    - 마스크 텍스처(Mask Map)의 R/G/B/A 채널이 각각 하나의 색 영역입니다. 칠해진 곳만 "영역 R~A 색"이 곱해지고,
//      아무 채널에도 칠해지지 않은 곳(흰 토글 단추 같은 고정색)은 기본 텍스처 색 그대로 나옵니다.
//    - 마스크 텍스처는 반드시 가져오기 설정에서 sRGB를 꺼야 합니다(SD 재질 자동 설정 메뉴가 처리합니다).
//    - 마스크를 비워두면 영역 색은 무시되고 기본 텍스처 색 그대로 나옵니다.
//
// 2) 피부색 연동
//    - "전체 피부"를 켜면(머리, 맨손) 메시 전체가 피부색이 됩니다. 마스크는 필요 없습니다.
//    - 일부만 피부인 파츠(예: 팔찌 찬 손)는 피부 영역을 마스크의 한 채널에 칠하고 "X 영역 = 피부색 사용"을 켭니다.
//    - 마스크 채널 4개를 다 쓴 파츠(예: 토르소의 목 부분)는 "기본 텍스처의 알파"로 피부를 표시합니다.
//      기본 텍스처에서 알파가 0인 곳은 자동으로 피부색이 됩니다(알파 잘라내기를 끈 재질만). 알파가 없는
//      텍스처(전부 불투명)는 아무 영향이 없습니다.
//    - 피부색 값은 캐릭터마다 SDCharacterSkin 컴포넌트가 한 번에 덮어씁니다. 재질의 "피부색"은 그 컴포넌트가
//      없을 때의 기본값입니다.
//
// 기존 스크립트 호환: 원본과 같이 _BaseColor(material.color)로 피격 색 변경, 렌더러 껐다 켜기 점멸을 그대로 지원합니다.
Shader "MobArchive/Toon_Character_SD"
{
    Properties
    {
        [Header(Base)]
        [MainTexture] _BaseMap ("기본 텍스처 (Base Map)", 2D) = "white" {}
        [MainColor] _BaseColor ("기본 색 (Base Color)", Color) = (1, 1, 1, 1)
        [Normal][NoScaleOffset] _BumpMap ("노멀 맵 (Normal Map)", 2D) = "bump" {}
        _BumpScale ("노멀 맵 세기", Range(0, 2)) = 1

        [Header(SD Color Regions)]
        [NoScaleOffset] _MaskMap ("색 영역 마스크 (R/G/B/A 채널, sRGB 끔)", 2D) = "black" {}
        _RegionColorR ("영역 R 색", Color) = (1, 1, 1, 1)
        _RegionColorG ("영역 G 색", Color) = (1, 1, 1, 1)
        _RegionColorB ("영역 B 색", Color) = (1, 1, 1, 1)
        _RegionColorA ("영역 A 색", Color) = (1, 1, 1, 1)

        [Header(Skin)]
        _SkinColor ("피부색 (SDCharacterSkin이 캐릭터마다 덮어씀)", Color) = (0.984, 0.929, 0.91, 1)
        [ToggleUI] _WholeSkin ("전체 피부 (머리, 맨손)", Float) = 0
        [ToggleUI] _SkinLinkR ("R 영역 = 피부색 사용", Float) = 0
        [ToggleUI] _SkinLinkG ("G 영역 = 피부색 사용", Float) = 0
        [ToggleUI] _SkinLinkB ("B 영역 = 피부색 사용", Float) = 0
        [ToggleUI] _SkinLinkA ("A 영역 = 피부색 사용", Float) = 0

        [Header(Toon Shading)]
        _ShadowColor ("그림자 색", Color) = (0.82, 0.74, 0.84, 1)
        _ShadowThreshold ("명암 경계 위치 (클수록 그림자 넓어짐)", Range(-1, 1)) = 0
        _ShadowSmoothness ("명암 경계 부드러움 (낮을수록 또렷)", Range(0.001, 1)) = 0.02
        _ShadeAmount ("명암 적용 정도 (얼굴은 0 추천)", Range(0, 1)) = 1
        _ReceiveShadowAmount ("다른 물체 그림자 받기", Range(0, 1)) = 1
        _Brightness ("밝기", Range(0, 2)) = 1

        [Header(Rim Light)]
        _RimColor ("가장자리 빛 색", Color) = (1, 1, 1, 1)
        _RimStrength ("가장자리 빛 세기 (0이면 끔)", Range(0, 1)) = 0.25
        _RimWidth ("가장자리 빛 두께", Range(0, 1)) = 0.3

        [Header(Highlight)]
        [NoScaleOffset] _MetallicGlossMap ("반사 맵 (MetallicSmoothness, A채널 사용)", 2D) = "white" {}
        _Smoothness ("매끄러움", Range(0, 1)) = 0.5
        _SpecularStrength ("하이라이트 세기 (0이면 끔)", Range(0, 1)) = 0

        [Header(Emission)]
        [HDR] _EmissionColor ("자체발광 색", Color) = (0, 0, 0, 1)
        [NoScaleOffset] _EmissionMap ("자체발광 텍스처", 2D) = "white" {}

        [Header(Outline)]
        [ToggleUI] _OutlineEnabled ("외곽선 사용", Float) = 0
        _OutlineColor ("외곽선 색", Color) = (0.22, 0.18, 0.24, 1)
        _OutlineWidth ("외곽선 두께 (1080p 기준 대략 픽셀)", Range(0, 10)) = 2
        _OutlineBaseColorBlend ("외곽선에 텍스처 색 섞기", Range(0, 1)) = 0.5

        [Header(Alpha)]
        [ToggleUI] _AlphaClip ("알파 잘라내기 사용", Float) = 0
        _Cutoff ("알파 기준값", Range(0, 1)) = 0.5

        [Header(Render)]
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("면 그리기 (Cull)", Float) = 2

        // 아래는 기존 스크립트(투명 전환 등)와 URP/Lit 호환용입니다. 인스펙터에는 보이지 않습니다.
        [HideInInspector] _Surface ("__surface", Float) = 0
        [HideInInspector] _Blend ("__blend", Float) = 0
        [HideInInspector] _SrcBlend ("__src", Float) = 1
        [HideInInspector] _DstBlend ("__dst", Float) = 0
        [HideInInspector] _ZWrite ("__zw", Float) = 1
        [HideInInspector] _MainTex ("BaseMap (호환용)", 2D) = "white" {}
        [HideInInspector] _Color ("Base Color (호환용)", Color) = (1, 1, 1, 1)

        // [112번 투명] 디더 반투명 정도(1 = 평소). 코드(PlayerInvisibility)가 넣으므로 인스펙터에는 보이지 않습니다.
        [HideInInspector] _DitherFade ("Dither Fade (코드 전용)", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        // 모든 Pass가 같이 쓰는 재질 값입니다.
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _BumpScale;
            half4 _ShadowColor;
            half _ShadowThreshold;
            half _ShadowSmoothness;
            half _ShadeAmount;
            half _ReceiveShadowAmount;
            half _Brightness;
            half4 _RimColor;
            half _RimStrength;
            half _RimWidth;
            half _Smoothness;
            half _SpecularStrength;
            half4 _EmissionColor;
            half _OutlineEnabled;
            half4 _OutlineColor;
            half _OutlineWidth;
            half _OutlineBaseColorBlend;
            half _AlphaClip;
            half _Cutoff;
            half4 _RegionColorR;
            half4 _RegionColorG;
            half4 _RegionColorB;
            half4 _RegionColorA;
            half4 _SkinColor;
            half _WholeSkin;
            half _SkinLinkR;
            half _SkinLinkG;
            half _SkinLinkB;
            half _SkinLinkA;
            half _DitherFade;
        CBUFFER_END

        // [실시간 초상화 조명 - PlayerPortraitCamera가 초상화를 그리는 동안에만 켜는 전역 값, 평소엔 0]
        // _SDPortraitLightAmount(0~1)만큼 태양 방향 대신 _SDPortraitLightDir(카메라 쪽에서 얼굴로 비추는 빛)으로 명암을 나누고,
        // 다른 물체/머리카락이 드리우는 그림자도 그만큼 없앱니다. 카툰 명암(입체감)은 그대로 남습니다.
        float _SDPortraitLightAmount;
        float4 _SDPortraitLightDir;

        TEXTURE2D(_BaseMap);          SAMPLER(sampler_BaseMap);
        TEXTURE2D(_BumpMap);          SAMPLER(sampler_BumpMap);
        TEXTURE2D(_MetallicGlossMap); SAMPLER(sampler_MetallicGlossMap);
        TEXTURE2D(_EmissionMap);      SAMPLER(sampler_EmissionMap);
        TEXTURE2D(_MaskMap);          SAMPLER(sampler_MaskMap);

        // 색 영역 마스크와 피부색 설정으로 "기본 텍스처에 곱할 색"을 계산합니다.
        // 아무 채널에도 칠해지지 않은 곳은 1(흰색)이 곱해져서 기본 텍스처 색이 그대로 나옵니다.
        // baseAlpha: 기본 텍스처의 알파. 알파 잘라내기를 끈 재질에서는 알파가 0인 곳을 피부로 봅니다.
        half3 SDRegionTint(float2 uv, half baseAlpha)
        {
            if (_WholeSkin > 0.5h) return _SkinColor.rgb;

            half4 m = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, uv);
            half3 colR = lerp(_RegionColorR.rgb, _SkinColor.rgb, _SkinLinkR);
            half3 colG = lerp(_RegionColorG.rgb, _SkinColor.rgb, _SkinLinkG);
            half3 colB = lerp(_RegionColorB.rgb, _SkinColor.rgb, _SkinLinkB);
            half3 colA = lerp(_RegionColorA.rgb, _SkinColor.rgb, _SkinLinkA);

            half covered = saturate(m.r + m.g + m.b + m.a);
            half3 tint = (1.0h - covered) + m.r * colR + m.g * colG + m.b * colB + m.a * colA;

            half skinFromAlpha = (_AlphaClip > 0.5h) ? 0.0h : (1.0h - baseAlpha);
            return lerp(tint, _SkinColor.rgb, skinFromAlpha);
        }

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

        // 알파 잘라내기를 켠 재질만 투명 부분을 버립니다.
        void ToonCharAlphaClip(float2 uv)
        {
            if (_AlphaClip > 0.5h)
            {
                half alpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).a * _BaseColor.a;
                clip(alpha - _Cutoff);
            }
        }
        ENDHLSL

        // ---------------- 1. 실제 색을 그리는 Pass ----------------
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
                float4 tangentWS  : TEXCOORD3;
                half   fogFactor  : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.normalWS = normalInputs.normalWS;
                output.tangentWS = float4(normalInputs.tangentWS, input.tangentOS.w * GetOddNegativeScale());
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogFactor = ComputeFogFactor(posInputs.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                SDDitherClip(input.positionCS); // [112번 투명]

                // 기본 색 (피격 시 스크립트가 _BaseColor를 바꾸면 여기에 그대로 반영됩니다)
                half4 baseTex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half4 albedo = baseTex * _BaseColor;
                if (_AlphaClip > 0.5h) clip(albedo.a - _Cutoff);

                // SD: 색 영역 / 피부색 적용. 텍스처 알파는 피부 표시용이므로 투명도로는 쓰지 않습니다.
                albedo.rgb *= SDRegionTint(input.uv, baseTex.a);
                if (_AlphaClip < 0.5h) albedo.a = _BaseColor.a;

                // 노멀 맵을 반영한 표면 방향
                half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv), _BumpScale);
                float3 bitangentWS = input.tangentWS.w * cross(input.normalWS, input.tangentWS.xyz);
                half3 normalWS = NormalizeNormalPerPixel(
                    TransformTangentToWorld(normalTS, half3x3(input.tangentWS.xyz, bitangentWS, input.normalWS)));
                half3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);

                // 태양 빛 + 그림자
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                // 빛 색에서 "세기"를 빼고 "색"만 남깁니다.
                half lightMax = max(max(mainLight.color.r, mainLight.color.g), mainLight.color.b);
                half3 lightHue = lightMax > 0.0001h ? mainLight.color / lightMax : half3(1, 1, 1);

                // [실시간 초상화] 초상화를 그리는 동안에는 카메라 쪽 빛으로 바꿉니다(평소엔 _SDPortraitLightAmount = 0이라 태양 그대로).
                half portraitAmount = saturate(_SDPortraitLightAmount);
                half3 lightDirWS = mainLight.direction;
                if (portraitAmount > 0.0h)
                    lightDirWS = SafeNormalize(lerp(float3(mainLight.direction), _SDPortraitLightDir.xyz, portraitAmount));

                // 셀 셰이딩: 빛 받는 면(1) / 등진 면(0)을 또렷하게 나눕니다.
                half NdotL = dot(normalWS, lightDirWS);
                half ramp = smoothstep(_ShadowThreshold - _ShadowSmoothness, _ShadowThreshold + _ShadowSmoothness, NdotL);
                ramp = lerp(1.0h, ramp, _ShadeAmount);

                // 건물 등 다른 물체가 드리운 그림자
                half castShadow = lerp(1.0h, mainLight.shadowAttenuation, _ReceiveShadowAmount * (1.0h - portraitAmount));
                half lit = ramp * castShadow;

                half3 shade = lerp(_ShadowColor.rgb, lightHue, lit);
                half3 color = albedo.rgb * shade * _Brightness;

                // 가장자리 빛(림라이트): 윤곽 쪽을 살짝 밝혀 배경에서 캐릭터가 또렷하게 떠 보이게 합니다.
                if (_RimStrength > 0.0h)
                {
                    half NdotV = saturate(dot(normalWS, viewDirWS));
                    half rimEdge = 1.0h - _RimWidth;
                    half rim = smoothstep(rimEdge, rimEdge + 0.08h, 1.0h - NdotV);
                    color += _RimColor.rgb * lightHue * rim * _RimStrength * (0.5h + 0.5h * lit);
                }

                // 선택: 툰 스타일 하이라이트(드론의 금속 부분 등)
                if (_SpecularStrength > 0.0h)
                {
                    half smooth = SAMPLE_TEXTURE2D(_MetallicGlossMap, sampler_MetallicGlossMap, input.uv).a * _Smoothness;
                    half3 halfDir = SafeNormalize(float3(lightDirWS) + float3(viewDirWS));
                    half NdotH = saturate(dot(normalWS, halfDir));
                    half spec = pow(NdotH, exp2(smooth * 10.0h + 1.0h));
                    spec = smoothstep(0.45h, 0.55h, spec) * smooth;
                    color += lightHue * spec * _SpecularStrength * lit;
                }

                // 자체발광(적 피격 플래시 / 돌진 예고 색 등 기존 스크립트가 쓰는 값)
                color += SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, input.uv).rgb * _EmissionColor.rgb;

                color = MixFog(color, input.fogFactor);
                return half4(color, albedo.a);
            }
            ENDHLSL
        }

        // ---------------- 2. 외곽선 Pass ----------------
        // 모델을 표면 방향으로 살짝 부풀린 뒤 "뒷면"만 그려서, 캐릭터 둘레에 선이 보이게 합니다.
        // 두께는 화면 기준이라 카메라와의 거리와 관계없이 일정하게 보입니다.
        // "외곽선 사용"이 꺼진 재질은 아무것도 그리지 않습니다.
        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull Front

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex OutlineVert
            #pragma fragment OutlineFrag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                half   fogFactor  : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings OutlineVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);

                // 외곽선이 꺼져 있으면 모든 꼭짓점을 한 점에 모아 아무것도 그려지지 않게 합니다.
                if (_OutlineEnabled < 0.5h || _OutlineWidth <= 0.0h)
                {
                    output.positionCS = float4(0, 0, 0, 1);
                    return output;
                }

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float4 positionCS = TransformWorldToHClip(positionWS);

                // 화면 위에서의 표면 방향으로 밀어냅니다(화면 비율 보정 포함).
                float2 dirCS = TransformWorldToHClipDir(normalWS).xy;
                float dirLen = max(length(dirCS), 0.00001);
                dirCS /= dirLen;
                dirCS.x *= _ScreenParams.y / _ScreenParams.x;

                // 화면 높이 1080 기준 대략 1 = 1픽셀 (클립 공간 높이 2 = 1080픽셀 → 1픽셀 ≈ 1/540)
                positionCS.xy += dirCS * (_OutlineWidth / 540.0) * positionCS.w;

                output.positionCS = positionCS;
                output.fogFactor = ComputeFogFactor(positionCS.z);
                return output;
            }

            half4 OutlineFrag(Varyings input) : SV_Target
            {
                SDDitherClip(input.positionCS); // [112번 투명]
                half4 baseTex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half4 albedo = baseTex * _BaseColor;
                if (_AlphaClip > 0.5h) clip(albedo.a - _Cutoff);

                // SD: 외곽선에 섞는 색도 바뀐 영역 색을 따라갑니다.
                albedo.rgb *= SDRegionTint(input.uv, baseTex.a);
                if (_AlphaClip < 0.5h) albedo.a = _BaseColor.a;

                // 외곽선 색에 텍스처 색을 섞어서, 머리카락은 진한 머리색 선 / 옷은 진한 옷색 선처럼 보이게 합니다.
                half3 color = _OutlineColor.rgb * lerp(half3(1, 1, 1), albedo.rgb, _OutlineBaseColorBlend);

                // 피격 플래시처럼 자체발광이 켜지면 외곽선도 같이 밝아집니다.
                color += _EmissionColor.rgb * 0.5h;

                color = MixFog(color, input.fogFactor);
                return half4(color, albedo.a * _OutlineColor.a);
            }
            ENDHLSL
        }

        // ---------------- 3. 그림자를 드리우는 Pass ----------------
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            Varyings ShadowVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif

                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
            #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif

                output.positionCS = positionCS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 ShadowFrag(Varyings input) : SV_Target
            {
                SDDitherClip(input.positionCS); // [112번 투명] 그림자도 같이 옅어집니다.
                ToonCharAlphaClip(input.uv);
                return 0;
            }
            ENDHLSL
        }

        // ---------------- 4. 깊이 정보 Pass ----------------
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            Varyings DepthVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            float DepthFrag(Varyings input) : SV_Target
            {
                SDDitherClip(input.positionCS); // [112번 투명]
                ToonCharAlphaClip(input.uv);
                return input.positionCS.z;
            }
            ENDHLSL
        }

        // ---------------- 5. 깊이 + 표면 방향 Pass (데칼 / 화면 효과용) ----------------
        // 배경 셰이더와 같은 이유로 URP/Lit의 공식 Pass를 그대로 가져와 씁니다.
        UsePass "Universal Render Pipeline/Lit/DEPTHNORMALS"
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
