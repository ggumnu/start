// [3레벨 보스 그로기 비콘 - 아이콘 테두리] 스프라이트 모양 그대로 _OutlineColor(HDR - 빛 번짐) 단색으로 칠합니다(투명도는 그림의 투명도).
// GroggyBeacon이 아이콘/역삼각형 뒤에 이 재질로 같은 그림을 둘레 여러 방향으로 조금씩 밀어 깔아서 테두리를 만듭니다.
Shader "MobArchive/SpriteGlowOutline"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [HDR] _OutlineColor ("Outline Color", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" "PreviewType" = "Plane" "CanUseSpriteAtlas" = "True" }

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _OutlineColor;

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv = v.uv;
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                float a = tex2D(_MainTex, i.uv).a * i.color.a * _OutlineColor.a;
                return float4(_OutlineColor.rgb, saturate(a));
            }
            ENDCG
        }
    }
}
