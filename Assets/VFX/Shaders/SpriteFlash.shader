// [아이템 아이콘 흰색 플래시용 스프라이트 셰이더]
// 유니티 기본 스프라이트 셰이더는 색을 "곱하는" 방식이라 원래 색보다 하얗게 만들 수 없습니다.
// 이 셰이더는 _FlashAmount(0~1)만큼 스프라이트 색을 _FlashColor(기본 흰색)로 덮어서, 투명도와 별개로
// "흰색으로 물드는" 정도를 조절할 수 있게 합니다. 투명도는 SpriteRenderer.color의 알파로 조절합니다.
// 지금은 PlayerItemPickupPresenter(획득 연출의 머리 위 아이콘)가 사용합니다. 조명 영향을 받지 않는
// Unlit 셰이더이며, URP에서도 그대로 동작합니다.
Shader "MobArchive/SpriteFlash"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _FlashColor ("Flash Color", Color) = (1,1,1,1)
        _FlashAmount ("Flash Amount", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _FlashColor;
            float _FlashAmount;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv);
                fixed4 c = tex * i.color;
                c.rgb = lerp(c.rgb, _FlashColor.rgb, saturate(_FlashAmount));
                return c;
            }
            ENDCG
        }
    }
}
