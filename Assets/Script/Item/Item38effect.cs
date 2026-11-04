using UnityEngine;

// 패시브 38번 - "이 아이템을 소지한 상태에서 적이 플레이어의 총알에 맞으면 1.5초 동안
// 매료된다(이동속도 -30%, 보스가 아니면 스킬/사격 개시 금지 + 원거리 적은 후퇴 무시하고
// 접근, 진행 중이던 공격은 즉시 강제 중단). 매료가 끝나면 3초 동안 재매료되지 않고,
// 그 3초 동안은 감속도 적용되지 않는다. 보스는 이동속도 감소만 적용된다."
//
// [구현]
// 실제 매료 로직은 전부 EnemyCharmState(적 하나당 자동으로 붙는 컴포넌트)가 담당합니다 -
// 이 효과는 Projectile.OnPlayerBulletHitEnemy(플레이어 총알이 적에게 명중하는 모든
// 순간 발생하는 전역 이벤트)를 영구 구독해두고, 맞을 때마다 그 적에게 TryApplyCharm()을
// 호출하기만 합니다. 패시브라 Remove()가 없으므로(다른 패시브 아이템들과 동일한 관례)
// 구독도 해제하지 않습니다.
//
// [매료 지속 이펙트 - 하트 (형빈 확정)]
// 31번 수면 방울과 같은 방식입니다. 매료가 실제로 새로 걸린 적에게만(TryApplyCharm()이 true)
// EnemyStatusVfx.Attach()로 하트 이펙트를 붙입니다. 매료 중에는 적을 따라다니며 하트가 올라오고,
// 매료가 끝나면(시간 종료 / 수면·해킹으로 교체 / 사망 / 사라지는 연출) 떠 있던 하트까지 전부
// Fade Out Time 동안 함께 투명해지며 사라집니다(Fade Out Remaining Particles 체크). 하트의 모양/
// 움직임은 VFX_Sleep_Status를 복제한 하트 프리팹에서 직접 수정하고, 좌우로 움직이는 방향에 맞춰
// 하트가 기울어지는 연출은 그 프리팹에 붙인 ParticleSwayTilt가 담당합니다.
// 보스에게는 하트를 띄우지 않습니다(보스는 감속만 받아 실질적으로 매료되지 않으므로 - 형빈 확정).
//
// [4번 폭발 연동 - 형빈 확정]
// 4번 폭발에 휘말린 적도 총알에 맞은 것과 똑같이 매료를 시도합니다(ExplodeOnImpactEffect.OnExplosionHitEnemy 구독, 규칙은 총알과 동일).
[System.Serializable]
public class BulletHitCharmEffect : IPassiveItemEffect, IRemovablePassiveEffect
{
    [Tooltip("매료 지속시간(초)입니다.")]
    public float charmDuration = 1.5f;
    [Tooltip("매료가 끝난 뒤 재매료되지 않는 면역시간(초)입니다.")]
    public float immunityDuration = 3f;
    [Tooltip("매료 중 이동속도 배율입니다. 1 = 감속 없음, 0.7 = 30% 감소(기본값).")]
    [Range(0f, 1f)]
    public float speedMultiplier = 0.7f;
    [Tooltip("매료 중 표시할 색상입니다. 발광(Emission) 효과 없이 오브젝트 색(_BaseColor/" +
             "_Color)만 이 색으로 바뀝니다 - 돌진/충전 공격의 발광 텔레그래프와 다른, " +
             "예전 방식의 단순 색 변화입니다.")]
    public Color charmColor = new Color(0.6f, 0.2f, 0.85f);
    [Tooltip("매료가 자연스럽게 끝난 뒤에도 사격/스킬 개시를 조금 더 금지하는 유예시간(초)" +
             "입니다. 방 입장 시 공격 유예시간(entryAttackDelay)과 같은 역할로, 매료 때문에 " +
             "플레이어에게 바짝 붙은 원거리 적이 매료가 풀리자마자 영거리에서 바로 사격하는 " +
             "것을 막기 위한 값입니다. 이동(후퇴 대신 접근)에는 영향을 주지 않습니다 - 접근은 " +
             "매료가 끝나는 즉시 멈춥니다.")]
    public float postCharmAttackDelay = 0.5f;

    [Header("매료 지속 이펙트 (매료 중인 동안 적에게 계속 붙어 있음)")]
    [Tooltip("매료가 걸려 있는 동안 적에게 붙여둘 하트 이펙트입니다. 이펙트의 색/크기/움직임은 프리팹을 " +
             "복제해서 그 프리팹에서 직접 수정하세요. 매료가 끝나면 떠 있던 하트까지 전부 Fade Out Time " +
             "동안 투명해지며 사라집니다(Fade Out Remaining Particles 체크).")]
    public EnemyStatusVfxSettings charmStatusVfx = new EnemyStatusVfxSettings
    {
        fadeOutTime = 0.25f,
        fadeOutRemainingParticles = true,
    };

    public void Apply(PassiveEffectContext context, string sourceItemId)
    {
        Projectile.OnPlayerBulletHitEnemy -= OnBulletHitEnemy; // 혹시 남아 있을 이전 구독 정리(중복 방지)
        Projectile.OnPlayerBulletHitEnemy += OnBulletHitEnemy;
        // [4번 폭발 연동 - 형빈 확정] 4번 폭발에 휘말린 적도 총알에 맞은 것과 똑같이 매료됩니다.
        ExplodeOnImpactEffect.OnExplosionHitEnemy -= OnBulletHitEnemy;
        ExplodeOnImpactEffect.OnExplosionHitEnemy += OnBulletHitEnemy;
    }

    // [패시브 잃기 - 48번 등] 소지 중에만 유지되던 효과를 되돌립니다(IRemovablePassiveEffect.cs 참고).
    public void Remove(PassiveEffectContext context, string sourceItemId)
    {
        Projectile.OnPlayerBulletHitEnemy -= OnBulletHitEnemy;
        ExplodeOnImpactEffect.OnExplosionHitEnemy -= OnBulletHitEnemy;
    }

    private void OnBulletHitEnemy(EnemyHealth enemy)
    {
        if (enemy == null) return;

        var charm = EnemyCharmState.GetOrAdd(enemy.gameObject);
        if (charm == null) return;

        // 매료가 실제로 새로 걸렸을 때만 하트 지속 이펙트를 붙입니다(이미 매료 중/면역 중/수면·해킹
        // 중이면 false라 붙이지 않음).
        if (charm.TryApplyCharm(charmDuration, immunityDuration, speedMultiplier, charmColor, postCharmAttackDelay))
        {
            // [보스 - 형빈 확정] 보스는 실질적으로 매료에 당하지 않으므로(감속만 적용) 하트 이펙트를 띄우지 않습니다.
            if (enemy.GetComponent<BossAIController>() == null)
            {
                EnemyStatusVfx.Attach(enemy.gameObject, EnemyControlType.Charm, charmStatusVfx);
            }
        }
    }
}
