using System.Collections.Generic;
using UnityEngine;

// [제어 지속 이펙트 - 끝날 때 남은 입자를 투명하게 지우는 부품]
// EnemyStatusVfx가 제어(수면 등)가 끝난 이펙트를 놓아줄 때, 설정에서 "끝날 때 남은 입자를 투명하게
// 사라지게"가 켜져 있으면 그 이펙트 오브젝트에 이 컴포넌트를 붙입니다. 프리팹에 미리 붙여둘 필요는
// 없습니다(자동으로 붙음).
//
// [하는 일]
// 새 입자 생성은 EnemyStatusVfx가 이미 멈춘 상태에서 시작합니다. 이 컴포넌트는 지정된 시간(duration)
// 동안 이미 떠 있는 입자 전부의 투명도를 동시에 0까지 낮춘 뒤, 이펙트 오브젝트를 지웁니다. 입자의
// 원래 색(Start Color)의 알파만 낮추므로 Color over Lifetime 등 프리팹에 설정한 색 변화는 그대로
// 유지된 채 전체가 함께 옅어집니다. 재질/셰이더 종류와 상관없이 동작합니다.
//
// [EnemyStatusVfx 안에서 처리하지 않는 이유]
// 적이 사라지는 연출(DissolveAndVanish)이 시작되면 EnemyStatusVfx 컴포넌트 자체가 꺼지므로, 그 안에서
// 투명도를 낮추면 도중에 멈춰버립니다. 그래서 이펙트 오브젝트 쪽에 따로 붙어서 스스로 끝까지 처리합니다.
[DisallowMultipleComponent]
public class StatusVfxParticleFader : MonoBehaviour
{
    private ParticleSystem[] systems;
    private float duration;
    private float elapsed;
    private bool started;

    private ParticleSystem.Particle[] buffer = new ParticleSystem.Particle[64];

    // 입자마다 "투명해지기 시작할 때의 원래 알파"를 기억합니다. 입자는 죽을 때마다 배열 안 순서가
    // 바뀔 수 있어서, 순서 대신 입자마다 고유한 randomSeed 값으로 구분합니다.
    private readonly List<Dictionary<uint, byte>> originalAlpha = new List<Dictionary<uint, byte>>();

    // 이펙트 오브젝트에 붙여서 투명하게 지우기 시작합니다. fadeDuration이 0 이하이면 즉시 지웁니다.
    public static void Begin(GameObject instance, float fadeDuration)
    {
        if (instance == null) return;

        if (fadeDuration <= 0f)
        {
            Destroy(instance);
            return;
        }

        var fader = instance.GetComponent<StatusVfxParticleFader>();
        if (fader == null) fader = instance.AddComponent<StatusVfxParticleFader>();
        fader.StartFade(fadeDuration);
    }

    private void StartFade(float fadeDuration)
    {
        systems = GetComponentsInChildren<ParticleSystem>(true);
        duration = fadeDuration;
        elapsed = 0f;
        started = true;

        originalAlpha.Clear();
        for (int i = 0; i < systems.Length; i++)
        {
            originalAlpha.Add(new Dictionary<uint, byte>());
        }

        if (systems.Length == 0)
        {
            Destroy(gameObject);
        }
    }

    void LateUpdate()
    {
        if (!started) return;

        elapsed += Time.deltaTime;
        float remain = 1f - Mathf.Clamp01(elapsed / duration);

        int aliveTotal = 0;
        for (int s = 0; s < systems.Length; s++)
        {
            ParticleSystem ps = systems[s];
            if (ps == null) continue;

            int count = ps.particleCount;
            if (count == 0) continue;

            if (buffer.Length < count)
            {
                buffer = new ParticleSystem.Particle[Mathf.NextPowerOfTwo(count)];
            }

            count = ps.GetParticles(buffer, count);
            Dictionary<uint, byte> alphas = originalAlpha[s];

            for (int i = 0; i < count; i++)
            {
                uint key = buffer[i].randomSeed;
                Color32 c = buffer[i].startColor;

                if (!alphas.TryGetValue(key, out byte baseAlpha))
                {
                    baseAlpha = c.a;
                    alphas[key] = baseAlpha;
                }

                c.a = (byte)Mathf.RoundToInt(baseAlpha * remain);
                buffer[i].startColor = c;
            }

            ps.SetParticles(buffer, count);
            aliveTotal += count;
        }

        // 시간이 다 됐거나, 남은 입자가 하나도 없으면 이펙트를 지웁니다.
        if (remain <= 0f || aliveTotal == 0)
        {
            started = false;
            Destroy(gameObject);
        }
    }
}
