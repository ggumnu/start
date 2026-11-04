using UnityEngine;

// 패시브 98번 - "적에게 피해를 줄 때 그 적의 체력이 25% 이하라면 즉시 처치한다(보스 포함)."
//
// [기획 - 형빈 확정]
// - 피해를 입히는 "그 순간"(피해를 빼기 직전) 적의 체력이 최대 체력의 25% 이하일 때만 즉시 처치합니다. 이번 피해로 25% 이하가 된
//   경우는 처치하지 않고, 그 다음 피해부터 처치합니다. 보스도 똑같이 적용됩니다(보스 체력바도 바로 0).
// - 피해를 주는 모든 수단(총알, 수류탄, 해킹 피해, 81번 불 구역, 4번 폭발, 68번 충돌, 29번 등)에 똑같이 적용됩니다 - 모든 피해가
//   지나가는 EnemyHealth.TakeDamage에서 판정하기 때문입니다.
// - 즉시 처치된 적은 평소 처치와 표시가 다릅니다.
//   - 피해 숫자 대신 "섬멸"(검은 글자 + 빨간 테두리)이 뜨고, 처치 모션(사라짐)이 끝날 때까지 유지됩니다(DamagePopupDisplay).
//   - 흰색 대신 빨간색으로 물들며 사라집니다(DissolveAndVanish).
//   - 평소 처치 폭발(CombatVfxSpawner 8번) 대신 executeVfxPrefab(VFX_ImpactCritical_1.1.0_Pink)이 터집니다.
// - 처치 자체는 평소 처치와 같아서, 방 클리어/보물상자 위치/32번/54번 등 처치에 반응하는 것들은 그대로 동작합니다.
//
// [구현]
// ExecuteOverride 등록소에 설정을 등록/해제합니다. 판정과 빨간 사라짐은 EnemyHealth, "섬멸" 글자는 DamagePopupDisplay가 하고,
// 처치 이펙트는 이 효과가 EnemyHealth.OnAnyEnemyKilled를 구독해서 터뜨립니다(CombatVfxSpawner는 즉시 처치된 적의 평소 폭발을 건너뜀).
//
// [아이템 에셋 설정]
// ItemDefinition: Item Id = 98, Category = Passive, Special Effects에 ExecuteLowHealthEffect 하나. Stat Modifiers는 비워둡니다.
// Settings > Execute Vfx Prefab 칸에 VFX_ImpactCritical_1.1.0_Pink를 넣습니다.
[System.Serializable]
public class ExecuteLowHealthEffect : IPassiveItemEffect, IRemovablePassiveEffect
{
    [Tooltip("98번 즉시 처치 설정입니다(기준 비율/섬멸 글자/사라짐 색/처치 이펙트).")]
    public ExecuteOverride.Settings settings = new ExecuteOverride.Settings();

    [System.NonSerialized] private bool subscribed;

    public void Apply(PassiveEffectContext context, string sourceItemId)
    {
        if (context == null || context.PlayerObject == null)
        {
            Debug.LogWarning("ExecuteLowHealthEffect: 플레이어를 찾을 수 없어 98번 효과를 적용하지 못했습니다.");
            return;
        }

        if (settings == null) settings = new ExecuteOverride.Settings();
        ExecuteOverride.Register(SourceKey(sourceItemId), context.PlayerObject, settings);

        if (!subscribed)
        {
            EnemyHealth.OnAnyEnemyKilled += HandleEnemyKilled;
            subscribed = true;
        }
    }

    // [패시브 잃기 - 48번 등] 그 뒤로는 즉시 처치하지 않습니다. 이미 사라지는 중인 적의 연출은 그대로 끝까지 진행됩니다.
    public void Remove(PassiveEffectContext context, string sourceItemId)
    {
        ExecuteOverride.Unregister(SourceKey(sourceItemId));
        if (subscribed)
        {
            EnemyHealth.OnAnyEnemyKilled -= HandleEnemyKilled;
            subscribed = false;
        }
    }

    private static string SourceKey(string sourceItemId) => "execute_" + sourceItemId;

    // 즉시 처치된 적이 죽는 순간 처치 이펙트를 터뜨립니다(그 적이 처치될 때의 설정 기준). 예외가 나도 적의 사라짐 연출이 막히지
    // 않도록 감쌉니다(EnemyHealth.OnAnyEnemyKilled 주의사항).
    private static void HandleEnemyKilled(EnemyHealth enemy, Vector3 position)
    {
        try
        {
            if (enemy == null || !enemy.WasExecuted) return;
            var s = enemy.ExecuteSettings;
            if (s == null || s.executeVfxPrefab == null) return;

            float scale = s.executeVfxScale;
            if (enemy.GetComponent<BossAIController>() != null) scale *= s.bossVfxScaleMultiplier;

            GameObject vfx = Object.Instantiate(s.executeVfxPrefab, position + s.executeVfxOffset, Quaternion.identity);
            ApplyVfxScale(vfx, scale);
            Object.Destroy(vfx, Mathf.Max(s.executeVfxLifetime, 0.1f));
        }
        catch (System.Exception e)
        {
            Debug.LogError($"ExecuteLowHealthEffect: 처치 이펙트 생성 중 오류가 발생해 건너뜁니다: {e}");
        }
    }

    // 파티클 시작 크기/속도에 직접 곱합니다(다른 이펙트 배율과 같은 방식).
    private static void ApplyVfxScale(GameObject instance, float multiplier)
    {
        if (instance == null || Mathf.Approximately(multiplier, 1f)) return;

        var systems = instance.GetComponentsInChildren<ParticleSystem>(true);
        if (systems.Length == 0)
        {
            instance.transform.localScale *= multiplier;
            return;
        }

        foreach (var ps in systems)
        {
            var main = ps.main;
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
}
