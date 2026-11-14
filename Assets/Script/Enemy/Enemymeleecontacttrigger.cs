using System;
using System.Collections.Generic;
using UnityEngine;

// 적의 접촉 피해 판정 전용 자식 트리거입니다(몸에 닿으면 피해 - 근접/원거리 등 공격 방식과 무관하게 모든 적 프리팹 공용).
// 막는 몸통 콜라이더와 따로 둬서 플레이어가 적을 통과할 수 있게 함(구석에 몰려 일방적으로 맞는 상황 방지).
// 피해 간격은 이 트리거가 제한하지 않음 - 플레이어 무적 시간이 담당. 돌진 중이면 IDashDamageSource의 돌진 피해를 씀.
// 설계 경위(콜라이더 분리, 항상 접촉 피해로 단순화, 인터페이스 일반화)는 Docs/설계노트.md [적 접촉 판정].
public class EnemyMeleeContactTrigger : MonoBehaviour
{
    // [VFX 연동] 근접(돌진 포함) 공격이 플레이어에게 실제로 맞았을 때만 알림(충돌 지점, 공격자->플레이어 방향).
    // OnTriggerStay로 매 프레임 판정하므로, 무적 중이라 피해가 무시될 때는 울리지 않게 함 - 아니면 이펙트가 매 프레임 반복됨.
    public static event Action<Vector3, Vector3> OnAnyMeleeHitPlayer;

    private EnemyStats stats;
    private IDashDamageSource dashSource;

    // [87번 포섭 - 아군 ↔ 적 접촉 피해]
    // 아군과 적은 서로 몸이 닿으면 접촉 피해를 줍니다(돌진 중이면 돌진 피해). 같은 상대에게는 FactionContactInterval(1초)마다 한 번입니다.
    // - 아군이 주는 피해는 이미 x30 환산된 값(AllyFaction), 적이 아군에게 주는 피해는 원래 값(아군의 환산 체력에서 깎임 + 1초 무적).
    // - 아군이 돌진하는 동안은 적의 접촉 피해를 받지 않습니다(형빈 확정).
    // - 물리 레이어 설정(적끼리 트리거가 감지되는지)과 상관없이 동작하도록, 아군이 하나라도 있을 때만 이 트리거와 상대 몸통 콜라이더의
    //   겹침을 직접 검사합니다(FixedUpdate). 아군이 없으면 아무 일도 하지 않아 예전과 같습니다.
    private const float FactionContactInterval = 1f;
    private readonly Dictionary<EnemyHealth, float> nextFactionHitTime = new Dictionary<EnemyHealth, float>();
    private static readonly List<Collider> opponentColliders = new List<Collider>();
    private Collider ownTrigger;
    private EnemyHealth ownHealth;

    // dashSource는 돌진형 공격(EnemyMeleeAttack/EnemyChainDashAttack 등)이 있을 때만 각 공격 컴포넌트의 Initialize()가 넘김.
    // 원거리 적처럼 돌진이 없으면 생략.
    public void Initialize(EnemyStats ownerStats, IDashDamageSource dashDamageSource = null)
    {
        stats = ownerStats;
        dashSource = dashDamageSource;
    }

    void OnTriggerEnter(Collider other)
    {
        HandleContact(other);
    }

    void OnTriggerStay(Collider other)
    {
        HandleContact(other);
    }

    void FixedUpdate()
    {
        if (AllyFaction.All.Count == 0) return;

        if (ownTrigger == null) ownTrigger = GetComponent<Collider>();
        if (ownHealth == null) ownHealth = GetComponentInParent<EnemyHealth>();
        if (ownTrigger == null || !ownTrigger.enabled || ownHealth == null || ownHealth.IsDead) return;

        bool selfAlly = AllyFaction.IsAlly(ownHealth.gameObject);
        if (selfAlly)
        {
            var enemies = EnemyHealth.ActiveEnemies;
            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                var e = enemies[i];
                if (CombatTargeting.IsHostileAlive(e)) TryHitOpponent(e, true);
            }
        }
        else
        {
            var allies = AllyFaction.All;
            for (int i = allies.Count - 1; i >= 0; i--)
            {
                var a = allies[i];
                if (CombatTargeting.IsAllyAlive(a)) TryHitOpponent(a.Health, false);
            }
        }
    }

    // [87번] 상대(아군이면 적, 적이면 아군)와 닿아 있으면 접촉 피해를 줍니다(같은 상대에게 1초 간격).
    private void TryHitOpponent(EnemyHealth opponent, bool selfAlly)
    {
        if (opponent == null || opponent == ownHealth || opponent.IsDead) return;
        if (!OverlapsBody(opponent)) return;

        // 아군이 돌진하는 동안은 적의 접촉 피해를 받지 않습니다.
        if (!selfAlly)
        {
            var opponentDash = opponent.GetComponentInChildren<IDashDamageSource>();
            if (opponentDash != null && opponentDash.IsDashing) return;
        }

        if (nextFactionHitTime.TryGetValue(opponent, out float next) && Time.time < next) return;
        nextFactionHitTime[opponent] = Time.time + FactionContactInterval;

        bool isDashing = dashSource != null && dashSource.IsDashing;
        float damage = isDashing ? dashSource.DashDamage : (stats != null ? stats.attackPower : 1f);
        // 아군이 적에게 주는 피해는 피해 숫자 항상 Weak(아이템으로 생긴 피해 - 형빈 확정). 적이 아군에게 주는 피해는 그대로.
        if (selfAlly) opponent.TakeWeakDamage(damage);
        else opponent.TakeDamage(damage);
    }

    // 이 트리거가 상대의 몸통 콜라이더(트리거가 아닌 것)와 실제로 겹쳐 있는지입니다.
    private bool OverlapsBody(EnemyHealth opponent)
    {
        Bounds own = ownTrigger.bounds;
        opponent.GetComponentsInChildren(false, opponentColliders);
        for (int i = 0; i < opponentColliders.Count; i++)
        {
            var c = opponentColliders[i];
            if (c == null || !c.enabled || c.isTrigger) continue;
            if (!own.Intersects(c.bounds)) continue;
            if (Physics.ComputePenetration(ownTrigger, ownTrigger.transform.position, ownTrigger.transform.rotation,
                                           c, c.transform.position, c.transform.rotation, out _, out _))
            {
                opponentColliders.Clear();
                return true;
            }
        }
        opponentColliders.Clear();
        return false;
    }

    private void HandleContact(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        // [87번 포섭] 포섭된 아군은 플레이어에게 접촉 피해를 주지 않습니다(적과의 접촉은 아래 FixedUpdate).
        if (AllyFaction.IsAlly(gameObject)) return;

        var playerHealth = other.GetComponent<PlayerHealth>();
        if (playerHealth == null) return;

        // [74번 돌진 회피] 플레이어가 돌진하는 동안은 접촉 피해를 받지 않습니다(피격 이펙트도 없음). 돌진이 끝난 뒤에도 닿아 있으면
        // OnTriggerStay가 계속 불리므로 그때부터 평소처럼 피해를 받습니다(형빈 확정).
        if (PlayerDash.IsDodging) return;

        // [112번 투명] 투명 상태 동안은 접촉 피해(돌진 포함)를 받지 않습니다(피격 이펙트도 없음). 적 탄환과 수류탄 자폭 피해는 그대로입니다.
        if (PlayerInvisibility.IsActive) return;

        bool isDashing = dashSource != null && dashSource.IsDashing;
        float damage = isDashing ? dashSource.DashDamage : (stats != null ? stats.attackPower : 1f);

        // [VFX 연동] PlayerHealth.TakeDamage()가 실제로 피해를 적용하는 조건(사망X, 무적X)과
        // 똑같은 조건을 미리 확인해서, 무적 시간 중 반복 판정으로 이펙트가 스팸되지 않게 합니다.
        // [보호막] 보호막이 막을 공격이면 피격 충격 이펙트도 띄우지 않습니다(보호막 파괴 연출만 나옴 - 형빈 확정).
        if (!playerHealth.IsDead && !playerHealth.IsInvincible && !playerHealth.WillShieldBlockNextHit)
        {
            Vector3 hitPoint = other.ClosestPoint(transform.position);
            Vector3 direction = other.transform.position - transform.position;
            OnAnyMeleeHitPlayer?.Invoke(hitPoint, direction);
        }

        playerHealth.TakeDamage(damage);
    }
}