// [캐릭터용 툰 셰이더 - 플레이어 / 적 / 캐릭터 소품]
// 블루아카이브 인게임 캐릭터처럼 "또렷한 2단 명암(셀 셰이딩) + 색이 있는 그림자 + 가장자리 빛(림라이트)
// + 외곽선"을 만듭니다.
//
// - 기존 URP/Lit 재질과 프로퍼티 이름(_BaseMap, _BaseColor, _BumpMap, _MetallicGlossMap, _Smoothness,
//   _EmissionColor, _Cull 등)이 같아서, 재질의 Shader만 바꿔도 텍스처와 색이 그대로 유지됩니다.
//   반대로 Shader를 다시 "Universal Render Pipeline/Lit"로 돌리면 원래대로 돌아갑니다.
// - 외곽선은 재질마다 켜고 끌 수 있습니다(기본값: 끔). 지금은 플레이어와 플레이어 소품 재질만 켭니다.
// - 얼굴처럼 명암이 생기면 어색한 부위는 "명암 적용 정도"를 0으로 두면 항상 밝게 보입니다.
// - 태양 빛(Directional Light)은 "색"만 반영하고 "세기(Intensity)"는 반영하지 않습니다(배경 셰이더와 동일).
// - 스크립트 호환: 피격 색 변경(_BaseColor / material.color), 자체발광(_EmissionColor),
//   투명 전환(_Surface/_SrcBlend/_DstBlend/_ZWrite), 렌더러 껐다 켜기 점멸을 그대로 지원하므로
//   EnemyHealth / EnemyMeleeAttack / DissolveAndVanish / PlayerHitFlash를 수정하지 않아도 됩니다.
Shader "MobArchive/Toon_Character"
{
    Properties
    {
        [Header(Base)]
        [MainTexture] _BaseMap ("기본 텍스처 (Base Map)", 2D) = "white" {}
        [MainColor] _BaseColor ("기본 색 (Base Color)", Color) = (1, 1, 1, 1)
        [Normal][NoScaleOffset] _BumpMap ("노멀 맵 (Normal Map)", 2D) = "bump" {}
        _BumpScale ("노멀 맵 세기", Range(0, 2)) = 1

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
        CBUFFER_END

        // [레벨 조명 - LevelLighting.cs] 레벨마다 그림자 색/전체 밝기를 바꾸는 전역 값입니다(재질 값 아님). 값이 0이면(기본, 1레벨/n.5레벨/
        // 테스트 씬) 아무 변화가 없도록 "빼는 양"으로 받습니다: 그림자 색 x (1 - ShadowSub), 최종 색 x (1 - Darken).
        half4 _MobLvCharShadowSub;
        half _MobLvCharDarken;

        TEXTURE2D(_BaseMap);          SAMPLER(sampler_BaseMap);
        TEXTURE2D(_BumpMap);          SAMPLER(sampler_BumpMap);
        TEXTURE2D(_MetallicGlossMap); SAMPLER(sampler_MetallicGlossMap);
        TEXTURE2D(_EmissionMap);      SAMPLER(sampler_EmissionMap);

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

                // 기본 색 (피격 시 스크립트가 _BaseColor를 바꾸면 여기에 그대로 반영됩니다)
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                if (_AlphaClip > 0.5h) clip(albedo.a - _Cutoff);

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

                // 셀 셰이딩: 빛 받는 면(1) / 등진 면(0)을 또렷하게 나눕니다.
                half NdotL = dot(normalWS, mainLight.direction);
                half ramp = smoothstep(_ShadowThreshold - _ShadowSmoothness, _ShadowThreshold + _ShadowSmoothness, NdotL);
                ramp = lerp(1.0h, ramp, _ShadeAmount);

                // 건물 등 다른 물체가 드리운 그림자
                half castShadow = lerp(1.0h, mainLight.shadowAttenuation, _ReceiveShadowAmount);
                half lit = ramp * castShadow;

                // [레벨 조명] 그림자 색과 전체 밝기에 레벨 값을 곱합니다(기본 0 = 변화 없음).
                half3 levelShadow = _ShadowColor.rgb * saturate(1.0h - _MobLvCharShadowSub.rgb);
                half3 shade = lerp(levelShadow, lightHue, lit);
                half3 color = albedo.rgb * shade * _Brightness * saturate(1.0h - _MobLvCharDarken);

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
                    half3 halfDir = SafeNormalize(float3(mainLight.direction) + float3(viewDirWS));
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
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                if (_AlphaClip > 0.5h) clip(albedo.a - _Cutoff);

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
