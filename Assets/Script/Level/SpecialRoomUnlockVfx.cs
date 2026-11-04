using System.Collections;
using UnityEngine;

// 특수방 잠금 바리케이드가 풀릴 때의 해제 이펙트(수류탄 = 연쇄 폭파, 해킹툴 = 전용 이펙트)를 재생하는 일회용 실행기입니다.
// SpecialRoomLock.Unlock()이 Play()로 호출하며, 씬에 따로 배치할 필요가 없습니다.
//
// [왜 바리케이드가 아니라 별도 오브젝트에서 재생하나]
// 바리케이드는 풀리는 순간 DissolveAndVanish로 사라지기 시작하고, 다 사라지면 꺼집니다(꺼진 오브젝트의 코루틴은 멈춤).
// 연쇄 폭파는 여러 발을 시간차로 터뜨려야 해서, 바리케이드와 무관한 빈 오브젝트를 하나 만들어 거기서 재생한 뒤 스스로
// 지웁니다. 이펙트 하나하나도 방 콘텐츠가 아니라 씬 최상위에 생성됩니다(플레이어가 곧바로 방을 옮겨도 끊기지 않음).
//
// 크기 배율/자동 정리 방식은 적 처치 연쇄 폭파(CombatVfxSpawner 8번)와 같습니다.
public class SpecialRoomUnlockVfx : MonoBehaviour
{
    private SpecialRoomLockSettings settings;
    private Bounds area;

    // settings의 이펙트를 area(바리케이드 모양의 월드 경계 상자) 기준으로 재생합니다. 프리팹이 없으면 아무것도 하지 않습니다.
    public static void Play(SpecialRoomLockSettings settings, Bounds area)
    {
        if (settings == null || settings.unlockVfxPrefab == null) return;

        var runner = new GameObject("SpecialRoomUnlockVfx");
        var vfx = runner.AddComponent<SpecialRoomUnlockVfx>();
        // [방 전환] 플레이어가 다른 방에 들어가면 연쇄 폭파 진행과 이미 나온 이펙트를 바로 지웁니다(RoomScope).
        RoomScope.DestroyWhenPlayerLeaves(runner);
        vfx.settings = settings;
        vfx.area = area;
        vfx.StartCoroutine(vfx.PlayRoutine());
    }

    private IEnumerator PlayRoutine()
    {
        int count = Mathf.Max(1, settings.burstCount);

        for (int i = 0; i < count; i++)
        {
            Vector3 position = settings.spreadAcrossBarricade ? RandomPointIn(area) : area.center;
            position += settings.positionOffset;

            Quaternion rotation = settings.randomizeRotation
                ? Quaternion.Euler(0f, Random.Range(0f, 360f), 0f)
                : Quaternion.identity;

            GameObject instance = Instantiate(settings.unlockVfxPrefab, position, rotation);
            RoomScope.DestroyWhenPlayerLeaves(instance);
            ApplyScale(instance, settings.scaleMultiplier);
            Destroy(instance, ComputeEffectDuration(instance, settings.maxLifetime));

            if (i < count - 1)
            {
                float delay = Random.Range(settings.burstDelayRange.x, settings.burstDelayRange.y);
                if (delay > 0f) yield return new WaitForSeconds(delay);
            }
        }

        Destroy(gameObject);
    }

    private static Vector3 RandomPointIn(Bounds b)
    {
        return new Vector3(
            Random.Range(b.min.x, b.max.x),
            Random.Range(b.min.y, b.max.y),
            Random.Range(b.min.z, b.max.z));
    }

    // 파티클 크기와 퍼지는 속도를 함께 배율만큼 조절합니다(CombatVfxSpawner.ApplyScale과 같은 방식). 파티클이 없으면 오브젝트 크기를 바꿉니다.
    private static void ApplyScale(GameObject instance, float multiplier)
    {
        if (Mathf.Approximately(multiplier, 1f)) return;

        var systems = instance.GetComponentsInChildren<ParticleSystem>(true);
        if (systems.Length == 0)
        {
            instance.transform.localScale *= multiplier;
            return;
        }

        for (int i = 0; i < systems.Length; i++)
        {
            var main = systems[i].main;
            main.startSizeMultiplier *= multiplier;
            if (main.startSize3D)
            {
                main.startSizeXMultiplier *= multiplier;
                main.startSizeYMultiplier *= multiplier;
                main.startSizeZMultiplier *= multiplier;
            }
            main.startSpeedMultiplier *= multiplier;
        }
    }

    // 파티클 재생이 끝나는 대략적인 시간입니다(CombatVfxSpawner.ComputeEffectDuration과 같은 방식). 반복 재생이거나
    // 계산이 애매하면 maxLifetime을 씁니다 - "언젠가는 확실히 지운다"가 목적입니다.
    private static float ComputeEffectDuration(GameObject instance, float maxLifetime)
    {
        var systems = instance.GetComponentsInChildren<ParticleSystem>(true);
        if (systems.Length == 0) return maxLifetime;

        float longest = 0f;
        for (int i = 0; i < systems.Length; i++)
        {
            var main = systems[i].main;
            if (main.loop) return maxLifetime;

            float lifetime;
            switch (main.startLifetime.mode)
            {
                case ParticleSystemCurveMode.Constant:
                    lifetime = main.startLifetime.constant;
                    break;
                case ParticleSystemCurveMode.TwoConstants:
                    lifetime = main.startLifetime.constantMax;
                    break;
                default:
                    lifetime = maxLifetime;
                    break;
            }

            float duration = main.duration + lifetime;
            if (duration > longest) longest = duration;
        }

        return Mathf.Clamp(longest, 0.05f, maxLifetime);
    }
}
