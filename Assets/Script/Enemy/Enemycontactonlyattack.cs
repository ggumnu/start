using System;
using UnityEngine;
using UnityEngine.AI;

// "아주 기본적인" 근접 적의 공격 방식입니다. EnemyMeleeAttack처럼 실제로 이동하는 돌진은
// 없고, 플레이어를 향해 맹목적으로 계속 다가가다가 몸이 닿을 정도로 가까워지면(=아래
// attackRange 이내) 그 자리에 멈추지 않고 계속 플레이어를 향해 이동하면서 spinDuration초
// 동안 spinRotations바퀴 회전하는 "공격 시퀀스"를 재생합니다. 실제 접촉 데미지는 이
// 시퀀스와 무관하게 항상 EnemyMeleeContactTrigger가 처리하고 있으므로(닿아있는 동안
// 계속), 이 회전은 순수하게 "공격하는 것처럼 보이는" 연출과 OnAttackFinished 이벤트를
// 발생시키는 역할만 합니다.
//
// [v2 변경 - 공격 중에도 이동 계속]
// 처음 버전은 EnemyMeleeAttack과 동일하게 IsAttacking=true인 동안 EnemyAIController가
// 이동/회전에서 완전히 손을 떼고(그 결과 컨트롤러가 미리 걸어둔 agent.isStopped=true로
// 제자리에 멈춤), 이 컴포넌트는 회전만 담당했습니다. 이제는 "회전 중에도 계속 플레이어를
// 쫓아가야 한다"는 요청에 따라, IsAttacking은 여전히 true로 유지해 회전(=transform 회전)의
// 전권은 이 컴포넌트가 갖되, 이동만큼은 이 컴포넌트가 직접 매 프레임 agent.isStopped=false +
// agent.SetDestination(player.position)으로 밀어 넣어 계속 진행시킵니다. NavMeshAgent의
// updateRotation은 EnemyAIController.Start()에서 이미 꺼져 있으므로(agent.updateRotation =
// false), 에이전트가 이동 방향으로 자동 회전시키는 것과 이 컴포넌트의 회전 연출이 서로
// 충돌하지 않습니다 - 위치는 에이전트가, 회전은 이 컴포넌트가 각각 독립적으로 담당합니다.
// 플레이어 위치는 EnemyAIController와 마찬가지로 "Player" 태그로 직접 찾아 캐시해둡니다
// (BeginAttack의 targetPosition은 호출 시점 스냅샷이라 계속 갱신되는 실시간 위치가
// 아니므로, 추적을 위해서는 이 컴포넌트가 스스로 참조를 들고 있어야 합니다).
//
// [v3 변경 - 수치를 EnemyDefinition에서 관리하도록 ContactOnlyAttackConfig로 분리]
// attackRange/spinDuration/spinRotations/postAttackCooldown은 더 이상 이 컴포넌트에서
// "최종값"으로 취급되지 않습니다 - 프리팹에 붙은 기본값은 안전장치용 폴백일 뿐이고, 실제
// 값은 스폰 시점에 EnemyDefinition.attackConfig(ContactOnlyAttackConfig)가 덮어씁니다.
// DashAttackConfig가 EnemyMeleeAttack에 값을 직접 대입하는 것과 동일한 패턴입니다
// (ContactOnlyAttackConfig.cs 참고).
//
// [상태 흐름]
// None(평소 접근 - EnemyAIController가 전담) -> Attacking(spinDuration초 동안
// spinRotations바퀴 회전 + 계속 플레이어를 향해 이동 - 회전은 이 컴포넌트가, 이동 목적지도
// 이 컴포넌트가 매 프레임 갱신) -> Cooldown(postAttackCooldown초 대기 - 이 구간엔 다시
// EnemyAIController가 평소 접근/회전을 이어받습니다. 계속 붙어있는 상태라면 몸이 계속
// 닿아있으니 데미지는 이 사이에도 끊기지 않습니다) -> None(다시 공격 가능,
// OnAttackFinished 이벤트 발생).
//
// [데미지]
// 이 컴포넌트는 데미지 값을 직접 갖지 않습니다. 접촉 판정과 데미지는 지금까지와 동일하게
// 자식 오브젝트의 EnemyMeleeContactTrigger가 전담하며(EnemyStats.attackPower 고정 사용 -
// 돌진 전용 데미지 같은 예외 없음), 공격 시퀀스(회전+이동) 중이든 쿨다운 중이든 몸이
// 닿아있는 한 계속 동일하게 적용됩니다.
//
// [에디터 설정 필요사항 - EnemyMeleeAttack과 동일]
// - 이 컴포넌트가 붙는 프리팹 루트에는 Rigidbody와 NavMeshAgent가 필요합니다(아래
//   RequireComponent로 자동으로 같이 붙습니다). Rigidbody는 콜라이더 트리거 감지에
//   필요하며 이동에는 사용하지 않습니다.
// - 자식 오브젝트로 EnemyMeleeContactTrigger(+ Is Trigger 콜라이더)를 반드시 만들어
//   연결해야 접촉 시 플레이어 피해가 발생합니다.
// - EnemyStats/EnemyHealth/EnemyAIController는 프리팹에 미리 붙여둘 필요가 없습니다 -
//   RealEnemySpawner가 스폰 시점에 자동으로 확인 후 붙여줍니다.
// - EnemyDefinition의 Attack Config를 ContactOnlyAttackConfig로 선택하고, 그 안에서
//   attackRange/spinDuration/spinRotations/postAttackCooldown 값을 설정하세요. 아래
//   필드들의 기본값은 그 설정이 없을 때(또는 아직 스폰되지 않은 프리팹을 에디터에서 볼 때)의
//   폴백일 뿐입니다.
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(NavMeshAgent))]
public class EnemyContactOnlyAttack : MonoBehaviour, IEnemyAttackBehavior
{
    private enum AttackPhase { None, Attacking, Cooldown }

    [Header("사거리 (이 거리 이내로 플레이어가 들어오면 공격 시퀀스를 시작합니다 - 사실상 접촉 거리)")]
    [Tooltip("실제 값은 EnemyDefinition.attackConfig(ContactOnlyAttackConfig)가 스폰 시점에 " +
             "덮어씁니다. 이동형 공격이 아니라 근접 회전이므로, 프리팹 콜라이더 반지름 + " +
             "플레이어 콜라이더 반지름 정도의 작은 값을 권장합니다.")]
    public float attackRange = 1.4f;

    [Header("공격 시퀀스 - 회전하며 계속 접근")]
    [Tooltip("회전에 걸리는 시간(초)입니다. 이 구간 동안에도 플레이어를 향해 계속 이동합니다.")]
    public float spinDuration = 0.4f;
    [Tooltip("spinDuration 동안 몇 바퀴 회전할지입니다. 1이면 360도, 2면 720도.")]
    public float spinRotations = 1f;

    [Header("공격 시퀀스 종료 후 다음 공격까지 대기시간")]
    public float postAttackCooldown = 0.6f;

    private NavMeshAgent agent;
    private Rigidbody rb;
    private EnemyMeleeContactTrigger contactTrigger;
    private Transform player;

    private AttackPhase phase = AttackPhase.None;
    private float phaseTimer;

    public float AttackRange => attackRange;
    public bool IsAttacking => phase == AttackPhase.Attacking;
    public bool CanBeginAttack => phase == AttackPhase.None;

    public event Action OnAttackFinished;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();

        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        contactTrigger = GetComponentInChildren<EnemyMeleeContactTrigger>();
        if (contactTrigger == null)
        {
            Debug.LogWarning($"EnemyContactOnlyAttack ({gameObject.name}): 자식에서 EnemyMeleeContactTrigger를 찾지 못했습니다. " +
                              "접촉 시 플레이어 피해가 발생하지 않습니다.");
        }

        TryFindPlayer();
    }

    public void Initialize(EnemyStats ownerStats, Transform self)
    {
        // ownerMeleeAttack 인자를 넘기지 않습니다 - 이 적은 돌진 전용 데미지가 없으므로,
        // 트리거가 항상 EnemyStats.attackPower를 접촉 데미지로 사용하게 됩니다.
        // [트리거 연결 보강] 프리팹에서 AI 컨트롤러가 이 컴포넌트보다 위에 붙어 있으면, AI의 Awake가 이 컴포넌트의 Awake보다 먼저
        // 실행되어 아직 트리거를 못 찾은 상태로 Initialize가 불립니다. 그러면 접촉 트리거가 능력치를 받지 못해 접촉/돌진 피해가
        // 예비값 1로 고정됩니다. 여기서 한 번 더 찾아서 컴포넌트 순서와 상관없이 항상 연결되게 합니다.
        if (contactTrigger == null) contactTrigger = GetComponentInChildren<EnemyMeleeContactTrigger>();
        if (contactTrigger != null) contactTrigger.Initialize(ownerStats);
    }

    // AIController가 사거리 안 + CanBeginAttack을 모두 만족했을 때 호출합니다. 이 적은
    // 도달 불가능한 목표 같은 것을 따질 필요가 없어 항상 시작에 성공합니다.
    public bool BeginAttack(Vector3 targetPosition)
    {
        if (phase != AttackPhase.None) return false;

        phase = AttackPhase.Attacking;
        phaseTimer = spinDuration;

        if (agent != null && agent.enabled) agent.isStopped = false; // 회전 중에도 계속 이동
        return true;
    }

    void Update()
    {
        switch (phase)
        {
            case AttackPhase.Attacking:
                UpdateAttacking();
                break;
            case AttackPhase.Cooldown:
                UpdateCooldown();
                break;
        }
    }

    // 회전 연출과 "계속 플레이어를 향해 이동"을 동시에 처리합니다. 위치(NavMeshAgent)와
    // 회전(transform.Rotate)은 서로 다른 경로라 간섭하지 않습니다 - agent.updateRotation은
    // EnemyAIController.Start()에서 이미 꺼져 있습니다.
    private void UpdateAttacking()
    {
        if (player == null) TryFindPlayer();

        // [87번 포섭] 지금의 목표(플레이어 또는 아군/적)를 쫓아가며 돕니다. 아군이 없으면 예전과 같이 플레이어.
        // [112번 투명] 목표가 없으면(투명한 플레이어뿐 / 아군인데 싸울 적이 없음) 멈추고 공격을 끝냅니다(쿨다운으로).
        Transform chaseTarget = CombatTargeting.Resolve(gameObject, AllyFaction.IsAlly(gameObject), player, out bool followPlayer);
        if (chaseTarget == null || followPlayer)
        {
            if (agent != null && agent.enabled) agent.isStopped = true;
            EndAttack();
            return;
        }

        // [68번 패시브 연동] 넉백으로 밀리는 동안(ForcedPush)에는 플레이어 쪽 이동을 쉽니다(회전 연출/타이머는 그대로 진행).
        // 안 쉬면 매 프레임 플레이어 쪽으로 다시 걸어와 밀림이 상쇄됩니다.
        if (ForcedPush.IsBeingPushed(gameObject))
        {
            if (agent != null && agent.enabled) agent.isStopped = true;
        }
        else if (agent != null && agent.enabled)
        {
            agent.isStopped = false;
            agent.SetDestination(chaseTarget.position);
        }

        if (spinDuration <= 0f)
        {
            EndAttack();
            return;
        }

        float degreesPerSecond = (spinRotations * 360f) / spinDuration;
        transform.Rotate(Vector3.up, degreesPerSecond * Time.deltaTime, Space.World);

        phaseTimer -= Time.deltaTime;
        if (phaseTimer <= 0f)
        {
            EndAttack();
        }
    }

    private void EndAttack()
    {
        phase = AttackPhase.Cooldown;
        phaseTimer = postAttackCooldown;
    }

    private void UpdateCooldown()
    {
        // 쿨다운 중엔 IsAttacking이 false이므로 EnemyAIController가 매 프레임 다시 이동(접근)
        // 및 플레이어를 바라보는 회전 판단을 이어받습니다. 여기서는 순수하게 타이머만 관리합니다.
        phaseTimer -= Time.deltaTime;
        if (phaseTimer > 0f) return;

        phase = AttackPhase.None;
        OnAttackFinished?.Invoke();
    }

    // EnemyAIController.TryFindPlayer()와 동일한 방식입니다 - 스폰 순서상 이 컴포넌트가
    // Awake될 때 플레이어가 아직 없을 수 있어, 찾을 때까지 조용히 재시도합니다.
    private void TryFindPlayer()
    {
        var playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) player = playerObj.transform;
    }
}