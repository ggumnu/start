// [헬기 퇴각 연출용 - 조명 없는 단순 그림]
// 헬기 그림자(바닥에 깔린 반투명 판)와 밧줄 사다리(카메라를 향하는 긴 띠)를 그립니다. HeliExtraction.cs가 그림/색을 매 프레임 넣습니다.
//   - _BaseMap x _BaseColor 색, 그림의 알파로 투명도.
//   - _Cutoff: 알파가 이 값보다 낮은 곳은 아예 그리지 않음(사다리처럼 테두리가 또렷한 그림용, 0이면 끔).
//   - _ZWrite: 1이면 깊이를 써서 캐릭터와 앞뒤가 정확히 가려짐(사다리), 0이면 반투명 겹침(그림자).
//
// [재질] M_HeliShadow(그림자: Cutoff 0, ZWrite 0) / M_HeliLadder(사다리: Cutoff 0.5, ZWrite 1)를 같은 폴더에 만들어 두었습니다.
Shader "MobArchive/HeliUnlit"
{
    Properties
    {
        _BaseMap ("Texture (코드가 넣음)", 2D) = "white" {}
        _BaseColor ("Color", Color) = (1, 1, 1, 1)
        _Cutoff ("Alpha Cutoff (0 = 끔)", Range(0, 1)) = 0
        [Enum(Off, 0, On, 1)] _ZWrite ("ZWrite", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "HeliUnlit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite [_ZWrite]
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _Cutoff;
                half _ZWrite;
            CBUFFER_END

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
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                if (_Cutoff > 0) clip(c.a - _Cutoff);
                return c;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
