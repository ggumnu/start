// [배경용 툰 셰이더 - 벽 / 바닥 / 소품]
// 블루아카이브 인게임 배경처럼 "밝고 매트한 질감 + 부드러운 2단 명암 + 색이 있는 그림자"를 만듭니다.
//
// - 기존 URP/Lit 재질과 프로퍼티 이름(_BaseMap, _BaseColor, _BumpMap, _BumpScale, _MetallicGlossMap,
//   _Smoothness, _EmissionColor, _Cull 등)이 같아서, 재질의 Shader만 바꿔도 텍스처와 색이 그대로 유지됩니다.
//   반대로 Shader를 다시 "Universal Render Pipeline/Lit"로 돌리면 원래대로 돌아갑니다.
// - 그림자 쪽 색은 Lighting 창의 환경광(Ambient)이 아니라 재질의 "그림자 색"이 정합니다.
// - 태양 빛(Directional Light)은 "색"만 반영하고 "세기(Intensity)"는 반영하지 않습니다.
//   전체 밝기는 재질의 "밝기" 값으로 조절합니다. 그림자 진하기는 Directional Light의 Shadow Strength가
//   그대로 반영됩니다.
// - 점 조명(Point/Spot) 같은 추가 조명은 반영하지 않습니다(블루아카 배경처럼 태양 빛 하나 기준).
// - 바닥 위 데칼(URP Decal Projector, DBuffer 방식)을 받아서 데칼 색에도 같은 명암이 적용됩니다.
// - 외곽선은 캐릭터 셰이더(Toon_Character)와 같은 방식이며 재질마다 켜고 끌 수 있습니다(기본값: 끔).
//   상자처럼 모서리가 각진 모델은 모서리에서 선이 살짝 벌어질 수 있습니다.
// - 스크립트 호환: 투명 전환(_Surface/_SrcBlend/_DstBlend/_ZWrite)과 자체발광(_EmissionColor)을
//   그대로 지원하므로, 기존 사라짐/색 변경 연출 스크립트를 수정하지 않아도 됩니다.
Shader "MobArchive/Toon_Environment"
{
    Properties
    {
        [Header(Base)]
        [MainTexture] _BaseMap ("기본 텍스처 (Base Map)", 2D) = "white" {}
        [MainColor] _BaseColor ("기본 색 (Base Color)", Color) = (1, 1, 1, 1)
        [Normal][NoScaleOffset] _BumpMap ("노멀 맵 (Normal Map)", 2D) = "bump" {}
        _BumpScale ("노멀 맵 세기 (낮을수록 매끈)", Range(0, 2)) = 1

        [Header(Toon Shading)]
        _ShadowColor ("그림자 색", Color) = (0.72, 0.78, 0.92, 1)
        _ShadowThreshold ("명암 경계 위치 (클수록 그림자 넓어짐)", Range(-1, 1)) = 0
        _ShadowSmoothness ("명암 경계 부드러움", Range(0.001, 1)) = 0.1
        _Brightness ("밝기", Range(0, 2)) = 1

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

        // 모든 Pass가 같이 쓰는 재질 값입니다(SRP Batcher 호환을 위해 한 곳에서 선언).
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _BumpScale;
            half4 _ShadowColor;
            half _ShadowThreshold;
            half _ShadowSmoothness;
            half _Brightness;
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
        half4 _MobLvEnvShadowSub;
        half _MobLvEnvDarken;

        TEXTURE2D(_BaseMap);          SAMPLER(sampler_BaseMap);
        TEXTURE2D(_BumpMap);          SAMPLER(sampler_BumpMap);
        TEXTURE2D(_MetallicGlossMap); SAMPLER(sampler_MetallicGlossMap);
        TEXTURE2D(_EmissionMap);      SAMPLER(sampler_EmissionMap);

        // 알파 잘라내기를 켠 재질만 투명 부분을 버립니다.
        void ToonEnvAlphaClip(float2 uv)
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

            // 태양 빛 그림자(일반/캐스케이드)와 부드러운 그림자 품질 단계
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            // 데칼(DBuffer 방식) 받기
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"

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

                // 기본 색
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                if (_AlphaClip > 0.5h) clip(albedo.a - _Cutoff);

                // 바닥 위에 설치한 데칼 색을 기본 색에 섞습니다(그 다음에 명암이 적용됩니다).
            #if defined(_DBUFFER)
                half3 decalBase = albedo.rgb;
                ApplyDecalToBaseColor(input.positionCS, decalBase);
                albedo.rgb = decalBase;
            #endif

                // 노멀 맵을 반영한 표면 방향
                half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv), _BumpScale);
                float3 bitangentWS = input.tangentWS.w * cross(input.normalWS, input.tangentWS.xyz);
                half3 normalWS = NormalizeNormalPerPixel(
                    TransformTangentToWorld(normalTS, half3x3(input.tangentWS.xyz, bitangentWS, input.normalWS)));

                // 태양 빛 + 그림자
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                // 빛 색에서 "세기"를 빼고 "색"만 남깁니다(가장 밝은 채널을 1로 맞춤).
                half lightMax = max(max(mainLight.color.r, mainLight.color.g), mainLight.color.b);
                half3 lightHue = lightMax > 0.0001h ? mainLight.color / lightMax : half3(1, 1, 1);

                // 2단 명암: 빛을 받는 면(1)과 등진 면(0)을 경계에서 부드럽게 나눕니다.
                half NdotL = dot(normalWS, mainLight.direction);
                half ramp = smoothstep(_ShadowThreshold - _ShadowSmoothness, _ShadowThreshold + _ShadowSmoothness, NdotL);

                // 다른 물체가 드리운 그림자도 같은 "그림자 색"으로 칠합니다.
                half lit = ramp * mainLight.shadowAttenuation;

                // [레벨 조명] 그림자 색과 전체 밝기에 레벨 값을 곱합니다(기본 0 = 변화 없음).
                half3 levelShadow = _ShadowColor.rgb * saturate(1.0h - _MobLvEnvShadowSub.rgb);
                half3 shade = lerp(levelShadow, lightHue, lit);
                half3 color = albedo.rgb * shade * _Brightness * saturate(1.0h - _MobLvEnvDarken);

                // 선택: 툰 스타일 하이라이트(하이라이트 세기가 0이면 계산하지 않음)
                if (_SpecularStrength > 0.0h)
                {
                    half smooth = SAMPLE_TEXTURE2D(_MetallicGlossMap, sampler_MetallicGlossMap, input.uv).a * _Smoothness;
                    half3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                    half3 halfDir = SafeNormalize(float3(mainLight.direction) + float3(viewDirWS));
                    half NdotH = saturate(dot(normalWS, halfDir));
                    half spec = pow(NdotH, exp2(smooth * 10.0h + 1.0h));
                    spec = smoothstep(0.45h, 0.55h, spec) * smooth;
                    color += lightHue * spec * _SpecularStrength * lit;
                }

                // 자체발광(피격 플래시 등 기존 스크립트가 쓰는 값)
                color += SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, input.uv).rgb * _EmissionColor.rgb;

                color = MixFog(color, input.fogFactor);
                return half4(color, albedo.a);
            }
            ENDHLSL
        }

        // ---------------- 2. 외곽선 Pass ----------------
        // 캐릭터 셰이더와 같은 방식: 모델을 화면 기준으로 살짝 부풀린 뒤 "뒷면"만 그려 둘레에 선을 만듭니다.
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

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                float4 outlineData : TEXCOORD3; // UV4: xyz = 외곽선 전용 노멀, w = 모델 크기 - OutlineSmoothNormalBaker가 임포트 때 구움
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                half   fogFactor  : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // 크기별 외곽선 두께(OutlineWidthSettings가 넣는 전역값). _OutlineRefSize가 0이면 보정 없음.
            float _OutlineRefSize;
            float _OutlineMinWidth;

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
                // 하드 엣지 모서리에서 선이 끊기지 않도록, 구운 외곽선 노멀이 있으면 그 방향으로 밀어냅니다
                // (없는 모델은 UV4가 0이라 원래 노멀을 그대로 씀).
                float3 outlineNormalOS = input.normalOS;
                if (dot(input.outlineData.xyz, input.outlineData.xyz) > 0.01) outlineNormalOS = input.outlineData.xyz;

                // 작은 모델은 외곽선을 얇게: 실제 크기(구운 모델 크기 x 오브젝트 스케일) / 기준 크기, 최소 _OutlineMinWidth.
                float widthScale = 1.0;
                if (_OutlineRefSize > 0.0 && input.outlineData.w > 0.0)
                {
                    float4x4 m = GetObjectToWorldMatrix();
                    float objScale = (length(m._m00_m10_m20) + length(m._m01_m11_m21) + length(m._m02_m12_m22)) / 3.0;
                    widthScale = clamp(input.outlineData.w * objScale / _OutlineRefSize, _OutlineMinWidth, 1.0);
                }
                float3 normalWS = TransformObjectToWorldNormal(outlineNormalOS);
                float4 positionCS = TransformWorldToHClip(positionWS);

                // 화면 위에서의 표면 방향으로 밀어냅니다(화면 비율 보정 포함).
                float2 dirCS = TransformWorldToHClipDir(normalWS).xy;
                float dirLen = max(length(dirCS), 0.00001);
                dirCS /= dirLen;
                dirCS.x *= _ScreenParams.y / _ScreenParams.x;

                // 화면 높이 1080 기준 대략 1 = 1픽셀
                positionCS.xy += dirCS * (_OutlineWidth * widthScale / 540.0) * positionCS.w;

                output.positionCS = positionCS;
                output.fogFactor = ComputeFogFactor(positionCS.z);
                return output;
            }

            half4 OutlineFrag(Varyings input) : SV_Target
            {
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                if (_AlphaClip > 0.5h) clip(albedo.a - _Cutoff);

                // 외곽선 색에 텍스처 색을 섞어서, 붉은 벽돌은 진한 붉은 선 / 회색 바닥은 진한 회색 선처럼 보이게 합니다.
                half3 color = _OutlineColor.rgb * lerp(half3(1, 1, 1), albedo.rgb, _OutlineBaseColorBlend);
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

            // URP가 그림자를 그릴 때 채워주는 값입니다.
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
                ToonEnvAlphaClip(input.uv);
                return 0;
            }
            ENDHLSL
        }

        // ---------------- 4. 깊이 정보 Pass (화면 효과용) ----------------
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
                ToonEnvAlphaClip(input.uv);
                return input.positionCS.z;
            }
            ENDHLSL
        }

        // ---------------- 5. 깊이 + 표면 방향 Pass (데칼 / SSAO용) ----------------
        // 데칼은 이 Pass가 기록한 "렌더링 레이어"와 "표면 방향"을 보고 어디에 찍힐지 정합니다.
        // 유니티 버전마다 세부 규칙이 달라 직접 만들지 않고, URP/Lit에 들어있는 공식 Pass를 그대로 가져와 씁니다.
        // (프로퍼티 이름이 URP/Lit과 같아서 그대로 동작합니다. 대신 이 셰이더는 SRP Batcher 묶음 처리에서
        //  빠지는데, 배경 재질 수가 적어 성능 차이는 거의 없습니다.)
        UsePass "Universal Render Pipeline/Lit/DEPTHNORMALS"
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
