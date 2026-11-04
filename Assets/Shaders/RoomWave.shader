// [90번 액티브 - 방 전체로 퍼지는 물결]
// 바닥에 깐 사각형 하나 위에, 플레이어 위치(_Center)에서 퍼지는 물결을 계산해서 그립니다(RoomWavePulse.cs가 매 프레임 값을 넣음).
//   - 앞 고리: 바깥쪽 끝은 날카롭고 안쪽으로 부드럽게 꼬리가 남는 밝은 고리(_Radius 위치, 두께 _Width)
//   - 잔물결: 앞 고리 뒤로 _RippleSpacing 간격마다 _RippleCount개(뒤로 갈수록 _RippleFalloff만큼 옅어짐)
//   - 방 바닥 사각형(_RoomRect = 최소X, 최소Z, 최대X, 최대Z) 밖은 그리지 않습니다(가장자리 _EdgeSoftness만큼 부드럽게).
//   - 전체 진하기 _Fade(다 퍼진 뒤 0으로 줄어듦)
//
// [굴절 - Refraction Strength]
// 0보다 크게 올리면 물결이 지나가는 자리의 바닥이 실제 물처럼 일렁입니다(화면을 물결 방향으로 살짝 밀어서 다시 그림). 0이면 꺼짐(기본).
// URP 설정 에셋(PC_RPAsset 등)의 Rendering > Opaque Texture가 켜져 있어야 합니다(꺼져 있으면 일렁이는 자리가 검게/회색으로 보임).
// (처음에는 체크 칸(키워드 방식)으로 만들었는데 재질 인스펙터에 나타나지 않는 문제가 있어, 키워드 없이 값 하나로 켜고 끄는 방식으로 바꿨습니다.)
//
// [재질 만드는 법]
// Project 창에서 이 셰이더를 우클릭 > Create > Material. 만든 재질을 90번 Item Definition의 Wave Material 칸에 넣습니다.
// 색/고리 두께 등은 Item Definition에서 정하고 코드가 매 프레임 넣으므로 재질 인스펙터에서는 숨겨 두었습니다 - 재질에서는
// Refraction Strength만 조절하면 됩니다.
Shader "MobArchive/RoomWave"
{
    Properties
    {
        // 아래 값들은 90번 Item Definition 설정으로 코드가 매 프레임 넣습니다(재질 인스펙터에서는 숨김).
        [HideInInspector] [HDR] _Color ("Color", Color) = (0.55, 0.9, 1.6, 1)
        [HideInInspector] _Center ("Center", Vector) = (0, 0, 0, 0)
        [HideInInspector] _Radius ("Radius", Float) = 0
        [HideInInspector] _Width ("Ring Width", Float) = 0.5
        [HideInInspector] _RippleCount ("Ripple Count", Float) = 2
        [HideInInspector] _RippleSpacing ("Ripple Spacing", Float) = 1
        [HideInInspector] _RippleFalloff ("Ripple Falloff", Float) = 0.55
        [HideInInspector] _RoomRect ("Room Rect", Vector) = (-1000, -1000, 1000, 1000)
        [HideInInspector] _EdgeSoftness ("Edge Softness", Float) = 0.3
        [HideInInspector] _Fade ("Fade", Float) = 1

        // 재질에서 직접 조절하는 값: 0 = 굴절 끔, 올릴수록 바닥이 크게 일렁임(URP Opaque Texture 필요)
        _RefractionStrength ("Refraction Strength", Range(0, 0.1)) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "RoomWave"
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
                half4 _Color;
                float4 _Center;
                float _Radius;
                float _Width;
                float _RippleCount;
                float _RippleSpacing;
                float _RippleFalloff;
                float4 _RoomRect;
                float _EdgeSoftness;
                float _Fade;
                float _RefractionStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float4 screenPos  : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.screenPos = ComputeScreenPos(p.positionCS);
                return o;
            }

            // 반경 r에 있는 고리 하나의 밝기(0~1). 바깥쪽(d > r)은 짧고 날카롭게, 안쪽은 두께의 3배 길이로 부드럽게 꼬리가 남습니다.
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
                float2 p = i.positionWS.xz;
                float2 c = _Center.xz;
                float d = distance(p, c);
                float r = _Radius;

                // 앞 고리 + 잔물결(최대 3개)
                float wave = RingProfile(d, r, _Width);
                float amp = 1.0;
                [unroll]
                for (int k = 1; k <= 3; k++)
                {
                    amp *= _RippleFalloff;
                    float rk = r - k * _RippleSpacing;
                    float use = step((float)k, _RippleCount + 0.5) * step(0.0, rk);
                    wave += RingProfile(d, max(rk, 0.0), _Width * 0.7) * amp * use;
                }
                wave = saturate(wave);

                // 방 바닥 사각형 밖은 그리지 않음(가장자리 부드럽게)
                float2 toMin = p - _RoomRect.xy;
                float2 toMax = _RoomRect.zw - p;
                float inner = min(min(toMin.x, toMin.y), min(toMax.x, toMax.y));
                float roomMask = saturate(inner / max(_EdgeSoftness, 1e-4));

                float a = wave * roomMask * saturate(_Fade);

                // [굴절] Refraction Strength가 0보다 크면 켜짐(키워드 없이 값으로 판단).
                if (_RefractionStrength > 1e-5)
                {
                // 물결이 퍼지는 방향(중심 → 이 점)을 화면 방향으로 바꿔 그쪽으로 화면을 살짝 밀어 다시 그립니다.
                float2 uv = i.screenPos.xy / i.screenPos.w;
                float2 dirWS = d > 1e-4 ? (p - c) / d : float2(1.0, 0.0);
                float4 offCS = TransformWorldToHClip(i.positionWS + float3(dirWS.x, 0.0, dirWS.y) * 0.25);
                float4 offSP = ComputeScreenPos(offCS);
                float2 duv = offSP.xy / offSP.w - uv;
                float len = length(duv);
                duv = len > 1e-5 ? duv / len : float2(0.0, 0.0);

                float2 distUV = uv + duv * (_RefractionStrength * a);
                half3 scene = SampleSceneColor(distUV);
                half3 col = scene + _Color.rgb * a;
                return half4(col, saturate(a * 2.0) * _Color.a);
                }

                return half4(_Color.rgb, a * _Color.a);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
