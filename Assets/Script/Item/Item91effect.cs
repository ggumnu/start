using UnityEngine;

// 패시브 91번 - "총알이 적에게 적중하면 3초 동안 오한 상태로 만든다. 오한 상태의 적은 공격 속도와 이동 속도가 50% 감소한다."
//
// [기획 - 형빈 확정]
// - 플레이어 총알이 적에게 명중할 때마다(Projectile.OnPlayerBulletHitEnemy - 19번/137번 갈래 총알, 5번 분열탄, 69번 튕김 포함) 그 적에게
//   오한을 겁니다. 4번 폭발에 휘말린 적에게도 겁니다(ExplodeOnImpactEffect.OnExplosionHitEnemy). 94번 후광 피해와 수류탄에는 걸리지 않습니다.
// - 지속시간 3초 + 6번 제어 지속시간 보너스. 다시 맞으면 지속시간이 처음부터 갱신됩니다.
// - 이동 속도 x0.5(매료 감속 등과 곱해짐), 공격 속도 x0.5(공격 사이 대기만 두 배 - EnemyAttackSpeedModifier). 접촉형 적은 이동만 느려집니다.
// - 오한 중 색은 검파랑(Chill Color). 수면/해킹/매료 중에는 그 색(과 하트 등 이펙트)이 우선이고, 끝나면 오한 색이 보입니다.
// - 보스는 오한에 걸리지 않습니다(색도 그대로).
// - 실제 오한 로직은 EnemyChillState가 담당합니다.
//
// [아이템 에셋 설정]
// ItemDefinition: Item Id = 91, Category = Passive, Special Effects에 Item91effect 하나. Stat Modifiers는 비워둡니다.
// Chill Status Vfx의 Prefab 칸에 물방울 이펙트(vfx_Rain_02를 복제해 수정한 프리팹)를 넣습니다. 비워두면 색만 바뀝니다.
[System.Serializable]
public class Item91effect : IPassiveItemEffect, IRemovablePassiveEffect
{
    [Tooltip("오한 지속시간(초)입니다. 6번 제어 지속시간 보너스가 더해집니다. 다시 맞으면 처음부터 갱신됩니다.")]
    [Min(0f)] public float chillDuration = 3f;

    [Tooltip("오한 중 이동 속도 배율입니다(0.5 = 50% 감소). 매료 감속 등 다른 감속과 곱해집니다.")]
    [Range(0f, 1f)] public float moveSpeedMultiplier = 0.5f;

    [Tooltip("오한 중 공격 속도 배율입니다(0.5 = 공격과 공격 사이 대기가 두 배).")]
    [Range(0.05f, 1f)] public float attackSpeedMultiplier = 0.5f;

    [Tooltip("오한 중 적의 색입니다(기본 검파랑 1E2F6B). 발광 없이 몸 색만 바뀝니다.")]
    public Color chillColor = new Color(0.118f, 0.184f, 0.420f, 1f); // 1E2F6B

    [Header("오한 지속 이펙트 (몸에서 물기가 떨어지는 이펙트)")]
    [Tooltip("오한 중 적에게 붙여둘 이펙트입니다. 수면/해킹/매료 중에는 숨겨지고(이미 떨어진 물방울은 끝까지 재생) 끝나면 다시 나옵니다.")]
    public EnemyStatusVfxSettings chillStatusVfx = new EnemyStatusVfxSettings
    {
        anchor = EnemyStatusVfxSettings.AnchorPoint.BodyCenter,
        fadeOutTime = 0.6f,
        fadeOutRemainingParticles = false,
    };

    public void Apply(PassiveEffectContext context, string sourceItemId)
    {
        Projectile.OnPlayerBulletHitEnemy -= OnEnemyHit; // 혹시 남아 있을 이전 구독 정리(중복 방지)
        Projectile.OnPlayerBulletHitEnemy += OnEnemyHit;
        ExplodeOnImpactEffect.OnExplosionHitEnemy -= OnEnemyHit;
        ExplodeOnImpactEffect.OnExplosionHitEnemy += OnEnemyHit;
    }

    // [패시브 잃기 - 48번 등] 그 뒤부터 오한을 걸지 않습니다. 이미 걸린 오한은 남은 시간 동안 유지됩니다.
    public void Remove(PassiveEffectContext context, string sourceItemId)
    {
        Projectile.OnPlayerBulletHitEnemy -= OnEnemyHit;
        ExplodeOnImpactEffect.OnExplosionHitEnemy -= OnEnemyHit;
    }

    private void OnEnemyHit(EnemyHealth enemy)
    {
        if (enemy == null || enemy.IsDead) return;
        if (enemy.GetComponent<BossAIController>() != null) return; // 보스는 오한에 걸리지 않음

        try
        {
            var chill = EnemyChillState.GetOrAdd(enemy.gameObject);
            chill?.TryApply(chillDuration, moveSpeedMultiplier, attackSpeedMultiplier, chillColor, chillStatusVfx);
        }
        catch (System.Exception e)
        {
            // 명중 알림 안에서 실행되므로, 오류가 나도 총알/폭발 처리가 막히지 않게 이 적만 건너뜁니다.
            Debug.LogError($"Item91effect: 오한을 거는 중 오류가 발생해 이 적은 건너뜁니다: {e}");
        }
    }
}
