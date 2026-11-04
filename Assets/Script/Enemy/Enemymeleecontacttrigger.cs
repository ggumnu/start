using System;
using System.Collections.Generic;
using UnityEngine;

// 적 몸통(공격 방식 컴포넌트가 있는 부모)과는 별개의 자식 오브젝트/콜라이더로 붙는
// "플레이어 접촉 피해 판정" 전용 트리거입니다. 근접 적/원거리 적 등 공격 방식과 무관하게
// "몸에 닿으면 데미지"가 필요한 모든 적 프리팹에 공용으로 재사용됩니다.
//
// [왜 콜라이더를 두 개로 나누는가]
// 적끼리는 서로 밀어내고 벽에도 끼지 않아야 하지만, 플레이어는 적을 그냥 통과할 수
// 있어야 합니다(구석에 몰려 일방적으로 맞는 상황 방지). 대신 통과하는 동안 플레이어
// 체력이 깎여야 하므로, "막는 몸통 콜라이더"(Enemy 물리 레이어, Player와 물리 충돌은
// 꺼둠)와 "피해만 주는 트리거 콜라이더"(Player와 트리거 감지는 계속 필요하므로 별도
// 오브젝트/레이어)를 분리했습니다.
//
// [피해 빈도 조절]
// 이 트리거는 자체적으로 피해 빈도를 제한하지 않습니다 - 닿아있는 동안(OnTriggerStay
// 포함) 계속 TakeDamage를 호출하며, 실제 피해 간격은 전적으로 PlayerHealth의
// invincibilityDuration(무적 시간)이 담당합니다.
//
// [변경 이력 참고]
// 예전 버전은 "돌진 중에만 무장(Arm)되고 돌진 1회당 딱 1타만" 방식이었는데, 그러면
// 평소(추적/대기 중)에 몸이 닿아도 데미지가 안 들어가는 의도치 않은 결과가 있었습니다.
// 항상 접촉 시 데미지가 들어가도록 단순화했고, 다단히트 방지는 오직 플레이어의 무적
// 시간만으로 처리합니다.
//
// [v1.19 변경 - 돌진 중 데미지 증가 지원]
// EnemyMeleeAttack이 있는 적(근접 적)의 경우, "지금 돌진(Dashing) 중인지"를 물어볼 수
// 있는 참조를 함께 받아둡니다. 접촉 판정 시점에 그 참조가 있고 실제로 돌진 중이면
// EnemyStats.attackPower 대신 돌진 전용 데미지 값을 사용해 더 센 데미지를 줍니다. 이
// 참조가 없는 경우(원거리 적처럼 돌진 개념이 아예 없는 프리팹)에는 지금까지와 동일하게
// 항상 attackPower만 사용합니다.
//
// [변경 - IDashDamageSource 인터페이스로 일반화]
// 처음엔 이 트리거가 EnemyMeleeAttack이라는 구체 클래스를 직접 참조해서 IsDashing/
// dashDamage를 읽었습니다. 그런데 연속 돌진(EnemyChainDashAttack)처럼 "돌진"이라는 같은
// 개념을 가진 다른 컴포넌트가 추가되면서, 이 트리거가 그 새 클래스는 아예 인식하지 못하는
// 문제가 생겼습니다(C#은 멤버 이름이 같다고 같은 타입으로 취급하지 않습니다). 그래서 구체
// 클래스 대신 IDashDamageSource라는 작은 인터페이스만 참조하도록 바꿨습니다 - 이제
// EnemyMeleeAttack이든 EnemyChainDashAttack이든, 앞으로 추가될 어떤 돌진형 공격
// 컴포넌트든 이 인터페이스만 구현하면 자동으로 돌진 중 추가 데미지가 적용됩니다. 기존
// EnemyMeleeAttack의 동작(값/타이밍)은 100% 그대로 유지됩니다 - 참조하는 타입만
// 구체 클래스에서 인터페이스로 바뀐 것뿐입니다.
public class EnemyMeleeContactTrigger : MonoBehaviour
{
    // [VFX 연동] 근접(돌진 포함) 공격이 플레이어에게 실제로 맞았을 때를 게임 전체에 알리는
    // 정적 이벤트입니다(충돌 지점, 공격자->플레이어 방향). EnemyHealth.OnAnyEnemyDied와
    // 같은 패턴입니다. OnTriggerStay로 매 프레임 반복 판정되는 구조라, 무적 시간 중이라
    // 실제로 피해가 무시될 때는 이 이벤트도 발생하지 않습니다(HandleContact 참고) - 그렇지
    // 않으면 붙어있는 동안 이펙트가 매 프레임 반복 재생됩니다.
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

    // dashSource는 선택 사항입니다 - 이 적이 돌진형 공격(IDashDamageSource를 구현하는
    // 컴포넌트, 예: EnemyMeleeAttack, EnemyChainDashAttack)을 갖고 있을 때만 넘겨주세요
    // (각 공격 컴포넌트의 Initialize()가 자동으로 넘겨줍니다). 원거리 적처럼
    // EnemyRangedAIController가 직접 초기화하는 경우에는 생략하면 됩니다.
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
        opponent.TakeDamage(damage);
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