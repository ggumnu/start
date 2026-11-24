// [3레벨 보스 레이저 띠] BossLaserBeam이 레이저마다 만든 띠(카메라를 향해 돌아감)에 그립니다. 조명 없이 색만 칠하는 Unlit, 텍스처 없이 노이즈를 계산합니다.
// uv.x = 띠 폭(0~1), uv.y = 띠 길이(0~1). _BeamLength(미터)로 길이 방향 무늬 크기가 레이저 길이와 상관없이 같게 됩니다.
// - 흰 중심(_CoreColor) + 자홍 몸통(_BodyColor, 흐르는 결) + 보라 빛 번짐(_GlowColor): 빛은 더해짐(Bloom이 약해도 번져 보이게 직접 그림).
// - 번개 줄기(_Crackle...): 초당 _CrackleRate번 모양이 바뀌는 꺾인 선 2가닥이 구간별로 끊겨 지지직 튐(형빈 확정). 어둡게 덮어 그려짐.
//   _CrackleSmoothness: 다음 모양으로 넘어가는 방식(0 = 순간 교체, 1 = 이번 모양 -> 다음 모양으로 계속 부드럽게 이어짐 - 꺾인 위치가 옮겨 가고 끊긴 구간이 서서히 나타나고 사라짐).
// - 섞는 방식 = 미리 곱한 알파(Blend One OneMinusSrcAlpha): 알파 0인 곳은 빛을 더하고, 번개 줄기만 뒤를 가립니다.
// - _Fade(사그라듦), _Seed(레이저마다 다른 무늬), _BeamLength, _Overscan(번개가 몸통 밖으로 튀어나갈 여유)은 스크립트가 레이저마다 넣습니다.
// - _CapLength(미터, 0이면 각진 시작): 시작부 이 길이 구간에서 폭을 타원(반폭 = _CapLength면 반원)으로 줄여 시작부를 둥글게 깎습니다(형빈 확정).
Shader "MobArchive/BossLaser"
{
    Properties
    {
        [HDR] _CoreColor ("Core Color", Color) = (2.2, 2.0, 2.2, 1)
        _CoreWidth ("Core Width", Range(0.02, 1)) = 0.14
        [HDR] _BodyColor ("Body Color", Color) = (1.25, 0.15, 1.0, 1)
        [HDR] _GlowColor ("Glow Color", Color) = (0.28, 0.03, 0.55, 1)
        _EdgeWobble ("Edge Wobble", Range(0, 0.6)) = 0.22
        _FlowSpeed ("Flow Speed", Float) = 9
        _FlowScale ("Flow Scale", Float) = 1.6
        [HDR] _CrackleColor ("Crackle Color", Color) = (0.10, 0, 0.16, 1)
        _CrackleOpacity ("Crackle Opacity", Range(0, 1)) = 0.85
        _CrackleRate ("Crackle Rate", Float) = 24
        _CrackleSmoothness ("Crackle Smoothness", Range(0, 1)) = 1
        _CrackleDensity ("Crackle Density", Range(0, 1)) = 0.55
        _CrackleWidth ("Crackle Width", Range(0.005, 0.5)) = 0.09
        _CrackleAmplitude ("Crackle Amplitude", Range(0, 1.2)) = 0.6
        _CrackleFrequency ("Crackle Frequency", Float) = 5
        _EndFade ("End Fade (m)", Float) = 0.12
        _BeamLength ("Beam Length", Float) = 1
        _Seed ("Seed", Float) = 0
        _Fade ("Fade", Range(0, 1)) = 1
        _Overscan ("Overscan", Float) = 1.2
        _CapLength ("Start Cap Length (m)", Float) = 0
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }

        Pass
        {
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _CoreColor;
            float _CoreWidth;
            float4 _BodyColor;
            float4 _GlowColor;
            float _EdgeWobble;
            float _FlowSpeed;
            float _FlowScale;
            float4 _CrackleColor;
            float _CrackleOpacity;
            float _CrackleRate;
            float _CrackleSmoothness;
            float _CrackleDensity;
            float _CrackleWidth;
            float _CrackleAmplitude;
            float _CrackleFrequency;
            float _EndFade;
            float _BeamLength;
            float _Seed;
            float _Fade;
            float _Overscan;
            float _CapLength;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float Hash1(float n)
            {
                return frac(sin(n * 127.1 + 311.7) * 43758.5453);
            }

            float Hash2(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            // 부드러운 값 노이즈(0~1)
            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = Hash2(i);
                float b = Hash2(i + float2(1, 0));
                float c = Hash2(i + float2(0, 1));
                float d = Hash2(i + float2(1, 1));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float Fbm(float2 p)
            {
                float s = 0.0;
                float a = 0.5;
                float total = 0.0;
                for (int k = 0; k < 4; k++)
                {
                    s += a * ValueNoise(p);
                    total += a;
                    p = p * 2.03 + float2(17.1, 5.3);
                    a *= 0.5;
                }
                return s / total;
            }

            // 직선으로 이은 노이즈(0~1) - 꺾인 지그재그 선이 됩니다.
            float JaggedNoise(float x, float seed)
            {
                float i = floor(x);
                float f = frac(x);
                return lerp(Hash1(i + seed), Hash1(i + 1.0 + seed), f);
            }

            // 번개 줄기 한 가닥의 중심 위치(center)와 이 위치가 보이는지(seg 0/1)를 시간 칸 tq 기준으로 계산합니다.
            void CrackleShape(float u, float tq, float strand, out float center, out float seg)
            {
                float sd = tq * 1.37 + strand * 37.0 + _Seed * 11.0;
                float j1 = JaggedNoise(u * _CrackleFrequency, sd);
                float j2 = JaggedNoise(u * _CrackleFrequency * 2.6, sd * 1.7);
                center = ((j1 * 2.0 - 1.0) * 0.75 + (j2 * 2.0 - 1.0) * 0.25) * _CrackleAmplitude;
                seg = step(1.0 - _CrackleDensity, Hash1(floor(u * 3.0 + Hash1(sd) * 5.0) + sd * 2.3));
            }

            float4 frag (v2f i) : SV_Target
            {
                float t = _Time.y;
                float v = (i.uv.x * 2.0 - 1.0) * _Overscan;   // -1~1 = 보이는 반폭(바깥은 번개 여유)
                float u = i.uv.y * _BeamLength;               // 미터

                // 가장자리 일렁임 + 흐르는 결
                float wob = 1.0 + _EdgeWobble * (Fbm(float2(u * 2.2 - t * 7.0 + _Seed * 13.0, _Seed * 3.1)) * 2.0 - 1.0);
                // 둥근 시작부: 시작에서 _CapLength까지 폭 배율이 0 -> 1로 타원을 그리며 커짐(시작점에서 접선이 수직 = 둥근 끝).
                float capF = 1.0;
                if (_CapLength > 0.0001)
                {
                    float cx = saturate((_CapLength - u) / _CapLength);
                    capF = sqrt(saturate(1.0 - cx * cx));
                }
                float av = abs(v) / max(wob * capF, 0.02);
                float flow = Fbm(float2(u * _FlowScale - t * _FlowSpeed, v * 1.8 + _Seed * 7.0));
                float streak = Fbm(float2(u * 0.7 - t * 6.0, v * 5.0 + _Seed * 3.0));

                float core = exp(-pow(av / _CoreWidth, 2.0));
                float body = pow(saturate((1.0 - av) / 0.55), 1.3);
                float glow = exp(-pow(av / 0.75, 2.0));

                float3 e = core * (0.8 + 0.4 * flow) * _CoreColor.rgb
                         + body * (0.35 + 1.1 * flow * streak) * _BodyColor.rgb
                         + glow * _GlowColor.rgb;

                // 번개 줄기 2가닥: 시간 칸(tq)의 모양에서 다음 칸(tq + 1)의 모양으로 넘어갑니다. 넘어가는 비율 w는
                // _CrackleSmoothness가 1이면 칸 전체에 걸쳐 부드럽게(0 -> 1), 0이면 칸 끝에서 한 번에(순간 교체).
                float tt = t * _CrackleRate;
                float tq = floor(tt);
                float fr = tt - tq;
                float smoothSpan = max(_CrackleSmoothness, 0.0001);
                float w = saturate((fr - (1.0 - _CrackleSmoothness)) / smoothSpan);
                w = w * w * (3.0 - 2.0 * w);
                float crackle = 0.0;
                for (int s = 0; s < 2; s++)
                {
                    float c0, s0, c1, s1;
                    CrackleShape(u, tq, s, c0, s0);
                    CrackleShape(u, tq + 1.0, s, c1, s1);
                    float center = lerp(c0, c1, w);
                    float seg = lerp(s0, s1, w);
                    float dist = abs(v - center * capF);
                    crackle = max(crackle, seg * saturate((_CrackleWidth - dist) / (_CrackleWidth * 0.5)));
                }
                crackle *= saturate((1.15 - av) / 0.2);

                // 양 끝을 부드럽게 + 사그라듦
                float startFade = _CapLength > 0.0001 ? 1.0 : saturate(u / 0.08); // 둥근 시작부가 있으면 따로 흐리지 않음
                float ends = saturate((_BeamLength - u) / max(_EndFade, 0.001)) * startFade;
                float k = ends * _Fade;

                // 띠 가장자리(오버스캔 끝)에서 빛이 잘려 보이지 않게 서서히 0으로
                e *= saturate((_Overscan - abs(v)) / 0.2);
                e *= (1.0 - 0.85 * crackle) * k;

                float alpha = crackle * _CrackleOpacity * k;
                float3 rgb = e + _CrackleColor.rgb * alpha;
                return float4(rgb, alpha);
            }
            ENDCG
        }
    }
}
