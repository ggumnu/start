// [실시간 초상화 - 하늘 그라데이션 배경]
// PlayerPortraitCamera의 배경 카메라가 하늘(Skybox)로 그리는 셰이더입니다. 위쪽은 _TopColor, 아래쪽은 _BottomColor인 세로 그라데이션입니다.
// 세상 기준 위/아래로 칠하므로, 카메라가 얼굴을 따라 고개를 숙이거나 들면 그라데이션이 화면 안에서 살짝 위아래로 움직입니다
// (좌우로 돌 때는 변하지 않음 - 배경 건물 없이 은은한 움직임만 남김).
// - _Scale: 그라데이션의 촘촘함입니다. 초상화 화각이 좁아서(기본 30도) 1보다 꽤 커야 화면 안에서 색 차이가 보입니다.
//   클수록 같은 고개 움직임에도 그라데이션이 크게 움직입니다.
// - _Offset: 그라데이션 중심을 위(+)/아래(-)로 옮깁니다.
Shader "MobArchive/PortraitSkyGradient"
{
    Properties
    {
        _TopColor ("Top Color", Color) = (0.40, 0.70, 0.94, 1)
        _BottomColor ("Bottom Color", Color) = (0.80, 0.92, 0.99, 1)
        _Scale ("Gradient Scale", Float) = 2.5
        _Offset ("Gradient Offset", Float) = 0
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _TopColor;
            float4 _BottomColor;
            float _Scale;
            float _Offset;

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 dir : TEXCOORD0; // 카메라에서 이 픽셀로 향하는 세상 방향
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz; // 하늘 메시는 카메라 중심이라 정점 위치가 곧 방향
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                float up = normalize(i.dir).y;
                float t = saturate(up * _Scale * 0.5 + 0.5 + _Offset);
                t = t * t * (3.0 - 2.0 * t);
                return float4(lerp(_BottomColor.rgb, _TopColor.rgb, t), 1.0);
            }
            ENDCG
        }
    }
}
