using UnityEngine;
using UnityEngine.AI;

// 31번 액티브 아이템(기절) 전용 - "지금 이 적이 기절 상태인지"를 전담하는 컴포넌트입니다.
// EnemyCharmState(38번 매료)와 동일하게, 프리팹에 미리 붙여둘 필요 없이 필요할 때(=이 적이
// 처음으로 기절당할 때) GetOrAdd()로 자동으로 붙습니다.
//
// [매료와의 핵심 차이 - 형빈 기획]
// - 재적용 시 지속시간을 "새로 덮어쓰지" 않고 "그 순간 남은 시간에 더합니다"(누적). 매료는
//   이미 매료 중이면 재적용을 무시하지만(TryApplyCharm의 immunityTimer/IsCharmed 가드),
//   기절은 그런 가드가 없습니다 - 기절 중인 적을 다시 기절시키면 남은 시간이 늘어납니다
//   (형빈 확인 - 예: 2초 기본 지속시간, 1초 경과 시점에 재사용하면 그 순간 남은 1초에 새
//   지속시간 2초가 더해져 남은 시간이 3초가 됩니다).
// - 보스도 완전히 동일하게 적용됩니다 - 매료는 보스에게 이동속도 감소만 걸고 스킬/사격은
//   억제하지 않지만, 기절은 예외 없이 이동/사격/스킬을 전부 막습니다.
// - 재기절 면역 같은 개념이 없습니다(매료의 immunityTimer에 해당하는 것 없음) - 기절 중이
//   아니라면 조건 없이 다시 걸 수 있습니다.
// - 접촉 데미지는 기절과 무관합니다 - 이 컴포넌트도, 아래에서 게이트를 추가한 AI
//   컨트롤러들의 Update()도 EnemyMeleeContactTrigger(물리 트리거로 별도 동작)를 전혀
//   건드리지 않으므로, 기절 중에도 몸에 닿으면 평소와 동일하게 피해를 입습니다.
//
// [6번 패시브 연동 - 제어 지속시간 증가]
// 매료와 동일하게 EnemyControlDurationBonus.Resolve()를 거쳐 실제 지속시간을 정합니다
// (등록된 보너스가 없으면 원래 값 그대로).
//
// [38번 매료와의 상호작용 - 기절이 우선]
// - 기절이 걸리는 순간, 지금 매료 중이었다면 EnemyCharmState.ForceEndForStun()을 호출해
//   매료를 즉시 끝냅니다. 이때 매료가 "자연 종료"될 때와 동일한 후처리(3초 재매료 면역 +
//   사격/스킬 개시 유예)가 그대로 적용됩니다(형빈 확인).
// - 반대로 기절 중에는 새로 매료가 걸리지 않습니다 - EnemyCharmState.TryApplyCharm()이
//   맨 앞에서 IsStunnedFor()를 확인해 무시합니다(EnemyCharmState.cs 참고).
// - 매료 컴포넌트 참조는 Awake()에서 캐싱하지 않고 그때그때 GetComponent로 조회합니다 -
//   매료와 기절 둘 다 나중에 동적으로(GetOrAdd) 붙는 컴포넌트라서, 이 컴포넌트가 먼저
//   붙었을 때 아직 존재하지 않는 EnemyCharmState를 캐싱해버리면 이후 매료가 실제로
//   붙어도 영영 그 참조를 얻지 못하는 문제가 생기기 때문입니다.
//
// [진행 중이던 공격을 즉시 강제 중단 - 보스 포함]
// 매료는 보스의 공격은 건드리지 않지만(스킬/사격 억제 자체가 없으므로), 기절은 보스를
// 포함한 모든 적의 진행 중인 공격을 즉시 중단시켜야 합니다. 이 오브젝트에 붙어있는 모든
// ICancellableAttack 구현체(근접 - EnemyMeleeAttack, 보스 - EnemyChainDashAttack +
// EnemyRadialAttack)를 GetComponents로 한 번에 모아 전부 CancelAttack()을 호출합니다 -
// 매료처럼 "보스는 예외"로 분기할 필요 없이, 보스에게 여러 공격 컴포넌트가 동시에 붙어있어도
// 이 방식이면 자동으로 전부 취소됩니다. 연사(EnemyRangedAttack)는 ICancellableAttack을
// 구현하지 않으므로 CancelBurst()를 별도로 호출합니다.
[DisallowMultipleComponent]
public class EnemyStunState : MonoBehaviour
{
    private float stunTimer;

    private EnemyHealth health;
    private NavMeshAgent agent;
    private ICancellableAttack[] cancellableAttacks;
    private EnemyRangedAttack rangedAttack;

    public bool IsStunned => stunTimer > 0f;

    // EnemyAIController/EnemyRangedAIController/BossAIController가 매 프레임 호출하는
    // 정적 유틸리티입니다(EnemyCharmState.SuppressesMovementFor 등과 동일한 호출 방식).
    // 아직 한 번도 기절한 적 없는(=이 컴포넌트가 붙어있지 않은) 적은 항상 false입니다.
    public static bool IsStunnedFor(GameObject enemy)
    {
        if (enemy == null) return false;
        var state = enemy.GetComponent<EnemyStunState>();
        return state != null && state.IsStunned;
    }

    // 이 적에게 붙어있는 부품을 돌려주고, 없으면 새로 붙여서 돌려줍니다
    // (EnemyCharmState.GetOrAdd와 동일한 패턴).
    public static EnemyStunState GetOrAdd(GameObject enemy)
    {
        if (enemy == null) return null;

        var existing = enemy.GetComponent<EnemyStunState>();
        if (existing != null) return existing;

        return enemy.AddComponent<EnemyStunState>();
    }

    void Awake()
    {
        health = GetComponent<EnemyHealth>();
        agent = GetComponent<NavMeshAgent>();
        cancellableAttacks = GetComponents<ICancellableAttack>();
        rangedAttack = GetComponent<EnemyRangedAttack>();
    }

    // Item31effect(RoomWideStunEffect)가 방 안의 적마다 호출합니다. duration은 아이템이
    // 정한 기본 지속시간(예: 2초)이며, 실제로 적용되는 값은 EnemyControlDurationBonus.
    // Resolve()를 거친 뒤의 값입니다. 이미 기절 중이어도 무시하지 않고 남은 시간에 그대로
    // 더합니다(파일 상단 주석 참고) - 재적용을 막는 별도 가드가 없습니다.
    public void TryApplyStun(float duration)
    {
        if (health != null && health.IsDead) return;

        // 매료 중이었다면 기절이 우선하므로 즉시 끝냅니다(매료의 정상 종료 후처리 포함).
        // 캐싱하지 않고 그때그때 조회하는 이유는 파일 상단 주석 참고.
        var charm = GetComponent<EnemyCharmState>();
        charm?.ForceEndForStun();

        stunTimer += EnemyControlDurationBonus.Resolve(duration);

        if (cancellableAttacks != null)
        {
            for (int i = 0; i < cancellableAttacks.Length; i++)
            {
                cancellableAttacks[i]?.CancelAttack();
            }
        }
        rangedAttack?.CancelBurst();

        // AI 컨트롤러의 다음 Update()를 기다리지 않고, 이 프레임에 바로 확실히 멈추도록
        // 여기서도 직접 정지시킵니다("사용하면 그 즉시" - 형빈 기획).
        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
        }
    }

    void Update()
    {
        // 기절 도중 적이 죽으면(다른 피해원 등으로) 더 끌 이유가 없으니 즉시 정리합니다.
        // 죽은 뒤에도 Destroy(gameObject)가 처리될 때까지 이 컴포넌트의 Update()는 계속
        // 돌므로, EnemyCharmState와 동일한 이유로 여기서 스스로 정리합니다.
        if (health != null && health.IsDead)
        {
            stunTimer = 0f;
            return;
        }

        if (stunTimer > 0f)
        {
            stunTimer -= Time.deltaTime;
            if (stunTimer < 0f) stunTimer = 0f;
        }
    }
}
