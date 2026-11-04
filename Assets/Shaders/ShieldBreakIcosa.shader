﻿// [보호막 파괴 연출 - 깎은 정이십면체용 셰이더]
// PlayerShieldBreakVfx가 코드로 만든 깎은 정이십면체 메시를 반투명하게 그립니다. 조명/그림자 없이 색만 칠하는 Unlit 셰이더입니다.
//
// [면 반짝임 - 블루 아카이브 유우카 EX 스킬 보호막 느낌]
// - 외곽선 없음: 면 가장자리(uv.x가 1에 가까운 곳)를 _RimFade 폭만큼 투명하게 흐려서, 면 사이에 선 대신 틈이 보입니다.
// - 면마다 다른 랜덤 값(uv.y)과 _Seed로, 시간(_FlickerTime x _FlickerSpeed)마다 면별 랜덤 밝기를 뽑아 부드럽게 이어 갑니다.
//   밝기 0 = 투명, 0.5 = _FlickerBlue, 1 = _FlickerWhite. _FlickerSharpness가 1보다 크면 투명한 면이 많아집니다.
//   밝을수록 _FlickerGlow만큼 더 밝게(흰빛) 빛납니다.
// - _Alpha: 연출 페이드용 전체 투명도(스크립트가 매 프레임 넣음).
// - _Glow / _GlowColor / _GlowFaceAlpha: 전개 순간의 전체 발광. 모든 면에 흰빛을 더하고, 그동안 면을 _GlowFaceAlpha까지 진하게 합니다.
// - 뒷면 -> 앞면 순서로 두 번 그리고, 뒷면(안쪽)은 _BackFaceAlpha만큼 흐리게 해서 앞쪽 면이 또렷하게 보입니다.
Shader "MobArchive/ShieldBreakIcosa"
{
    Properties
    {
        [HDR] _FlickerWhite ("Flicker White", Color) = (1, 1, 1, 0.85)
        [HDR] _FlickerBlue ("Flicker Blue", Color) = (0.35, 0.72, 1, 0.45)
        _FlickerSpeed ("Flicker Speed", Float) = 7
        _FlickerSharpness ("Flicker Sharpness", Float) = 1.3
        _FlickerGlow ("Flicker Glow", Float) = 0.6
        _RimFade ("Rim Fade", Range(0, 0.9)) = 0.3
        _BackFaceAlpha ("Back Face Alpha", Range(0, 1)) = 0.5
        _FlickerTime ("Flicker Time", Float) = 0
        _Seed ("Seed", Float) = 0
        _Alpha ("Alpha", Range(0, 1)) = 1
        [HDR] _GlowColor ("Glow Color", Color) = (1, 1, 1, 1)
        _Glow ("Glow", Float) = 0
        _GlowFaceAlpha ("Glow Face Alpha", Range(0, 1)) = 0.6
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }

        CGINCLUDE
        #include "UnityCG.cginc"

        float4 _FlickerWhite;
        float4 _FlickerBlue;
        float _FlickerSpeed;
        float _FlickerSharpness;
        float _FlickerGlow;
        float _RimFade;
        float _BackFaceAlpha;
        float _FlickerTime;
        float _Seed;
        float _Alpha;
        float4 _GlowColor;
        float _Glow;
        float _GlowFaceAlpha;

        struct appdata
        {
            float4 vertex : POSITION;
            float2 uv : TEXCOORD0;
        };

        struct v2f
        {
            float4 pos : SV_POSITION;
            float2 uv : TEXCOORD0; // x = 면 중심 0 ~ 테두리 1, y = 면별 랜덤 값
        };

        v2f vert (appdata v)
        {
            v2f o;
            o.pos = UnityObjectToClipPos(v.vertex);
            o.uv = v.uv;
            return o;
        }

        // 0~1 랜덤 값(같은 입력이면 항상 같은 값)
        float Hash(float n)
        {
            return frac(sin(n * 127.1 + 311.7) * 43758.5453);
        }

        float4 Shade (v2f i, float alphaScale)
        {
            // 면 하나는 모든 픽셀의 uv.y가 같으므로, 면 전체가 같은 밝기로 반짝입니다.
            float faceId = i.uv.y * 97.0 + _Seed;
            float t = _FlickerTime * _FlickerSpeed + Hash(faceId) * 10.0; // 면마다 시작 타이밍도 다르게
            float step0 = floor(t);
            float k = frac(t);
            k = k * k * (3.0 - 2.0 * k);
            float level = lerp(Hash(faceId + step0 * 13.37), Hash(faceId + (step0 + 1.0) * 13.37), k);
            level = pow(saturate(level), max(_FlickerSharpness, 0.05));

            // 밝기 0 -> 투명, 0.5 -> 파랑, 1 -> 흰색
            float4 clearBlue = float4(_FlickerBlue.rgb, 0.0);
            float4 col = level < 0.5
                ? lerp(clearBlue, _FlickerBlue, level * 2.0)
                : lerp(_FlickerBlue, _FlickerWhite, (level - 0.5) * 2.0);
            col.rgb *= 1.0 + _FlickerGlow * level;

            // 외곽선 대신 가장자리를 투명하게 흐림
            if (_RimFade > 0.0)
            {
                float aa = max(fwidth(i.uv.x), 1e-4);
                col.a *= 1.0 - smoothstep(1.0 - _RimFade - aa, 1.0 - aa, i.uv.x);
            }

            // 전개 순간의 전체 발광
            float g = saturate(_Glow);
            col.a = lerp(col.a, max(col.a, _GlowFaceAlpha), g);
            col.rgb += _GlowColor.rgb * _Glow;

            col.a *= _Alpha * alphaScale;
            return col;
        }

        float4 fragBack (v2f i) : SV_Target { return Shade(i, _BackFaceAlpha); }
        float4 fragFront (v2f i) : SV_Target { return Shade(i, 1.0); }
        ENDCG

        // 1) 뒷면(안쪽) - 흐리게
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Front
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragBack
            ENDCG
        }

        // 2) 앞면(바깥쪽)
        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragFront
            ENDCG
        }
    }
}
