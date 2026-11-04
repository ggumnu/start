using UnityEngine;
using UnityEngine.AI;

// 적 하나의 Idle -> Chase 상태 전환과 "지금 공격을 시작해도 되는지" 판단을 담당합니다.
// 실제로 "어떻게 공격하는지"는 IEnemyAttackBehavior 구현체(EnemyMeleeAttack 등, 어떤
// 컴포넌트가 붙어있든 무방)에게 위임하므로, 이 컨트롤러는 공격 방식이 무엇이든 전혀 수정할
// 필요가 없습니다. 나중에 새 근접형 공격 방식을 추가할 때도 이 파일은 그대로 둡니다.
//
// [v1.9 변경 - 쿨다운 중에도 플레이어에게 접근 + 항상 플레이어를 바라보도록]
// 예전엔 Idle/Chase/Attack 3단계 로컬 상태머신을 따로 두고, attackBehavior.IsAttacking이
// "텔레그래프+대시+쿨다운"을 전부 묶어 true를 반환했습니다. 그래서 쿨다운 동안 이
// 컨트롤러가 완전히 손을 떼 적이 그 자리에 가만히 서있었고, 회전도 NavMeshAgent 자동
// 회전(updateRotation=true)에 맡겨져 있어서 이동을 멈추면 스폰 당시 바라보던 방향 그대로
// 굳어있었습니다.
// 지금은 IsAttacking이 "컨트롤러가 반드시 손을 떼야 하는 실행 구간(텔레그래프/대시)"에서만
// true이고, 쿨다운 중에는 false로 바뀌어 이 컨트롤러가 매 프레임 다시 Chase 판단(현재
// 플레이어 위치로 새로 SetDestination)을 이어받습니다. 아직 쿨다운이라 새 공격을 시작하면
// 안 되는 상태는 attackBehavior.CanBeginAttack으로 따로 걸러내므로, "사거리 안인데 아직
// 공격 못 함" = "그냥 계속 접근"으로 자연스럽게 처리됩니다. 로컬 Attack 상태가 더 이상
// 필요 없어져 Idle/Chase 2단계로 단순화했습니다.
// 회전은 EnemyRangedAIController와 동일하게 agent.updateRotation을 끄고 이 컨트롤러가
// 직접 Slerp로 처리합니다 (텔레그래프/돌진 실행 중의 회전만 EnemyMeleeAttack이 별도로
// 담당합니다).
//
// [v1.10 변경 - 공격 시작 실패(도달 불가능한 목표) 처리]
// attackBehavior.BeginAttack()이 이제 bool을 반환합니다 (당시엔 골목처럼 NavMesh가 없는
// 위치로 플레이어가 이동했을 때, 공격 구현체가 스스로 "이 목표는 도달 불가능하다"고 판단해
// 공격을 시작하지 않을 수 있게 하기 위함이었습니다). 이 경우 이 컨트롤러가 미리 걸어둔
// agent.isStopped = true를 그대로 방치하면 적이 그 자리에 얼어붙으므로, false를 받으면
// 즉시 isStopped를 풀고 평소 접근 행동으로 되돌립니다.
//
// [v1.13 참고 - BeginAttack이 false를 반환하는 이유는 공격 방식마다 다를 수 있음]
// bool 반환 자체의 의미(= "공격 시작 실패, 평소 접근으로 복귀")는 v1.10과 동일하지만,
// 실제로 false가 나오는 사유는 공격 구현체가 무엇이냐에 따라 달라집니다. 예를 들어
// EnemyMeleeAttack은 v1.13에서 돌진 목표 도달 가능성(NavMesh) 검증을 제거하고 대신
// "돌진 시작 전 시야(LOS)가 벽에 막혀있는지"를 확인하도록 바뀌었습니다 - 자세한 내용은
// EnemyMeleeAttack.cs 참고. 이 컨트롤러 입장에서는 사유와 무관하게 항상 같은 방식
// (평소 접근으로 복귀)으로 대응하므로 이 파일 자체는 수정할 필요가 없습니다.
//
// [v1.14 변경 - 방 입장 직후 공격 유예]
// 지금까지는 방에 들어오자마자 사거리 안이면 곧바로 돌진을 시작해서, 플레이어가 반응할
// 시간이 부족하다는 문제가 있었습니다. 이 오브젝트(적)는 RoomVisibilityManager가 그
// 방을 보여줄 때(=플레이어가 그 방에 들어온 순간) SetActive(true)로 활성화되는 구조라,
// OnEnable()이 호출되는 시점이 사실상 "플레이어가 이 방에 입장한 시점"과 정확히
// 일치합니다(레벨 빌드 중 적 스폰 직후 잠깐 활성화됐다 다시 꺼지는 순간에도 OnEnable이
// 한 번 호출되지만, 실제 입장 시점에 다시 호출되면서 아래 값이 그때 기준으로 새로
// 갱신되므로 문제없습니다). 그래서 OnEnable()에서 "공격을 시작해도 되는 시각"을 기록해두고,
// 공격 시작 조건(사거리+쿨다운)을 만족해도 이 시각이 지나기 전까지는 돌진을 시작하지
// 않도록 했습니다. 추적(접근) 이동 자체는 전혀 영향받지 않습니다 - Chase 상태의 else
// 분기(평소 접근)가 그대로 매 프레임 실행됩니다.
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(EnemyStats))]
public class EnemyAIController : MonoBehaviour
{
    private enum State { Idle, Chase }

    [Header("회전 설정")]
    [Tooltip("Chase 상태(대기/추적/쿨다운 중 접근)일 때 플레이어를 바라보는 회전 속도입니다. " +
             "텔레그래프/돌진 실행 중의 회전은 EnemyMeleeAttack이 별도로 처리합니다.")]
    public float facingRotationSpeed = 12f;

    [Header("입장 유예 (방에 막 들어왔을 때 즉시 공격하지 않도록)")]
    [Tooltip("이 오브젝트가 활성화된(=플레이어가 이 방에 들어와 화면에 나타난) 직후 이 시간 " +
             "동안은 사거리 안이라도 돌진을 시작하지 않습니다. 추적(접근) 이동에는 영향을 " +
             "주지 않습니다.")]
    public float entryAttackDelay = 0.5f;

    private NavMeshAgent agent;
    private EnemyStats stats;
    private IEnemyAttackBehavior attackBehavior;
    private EnemyHealth health;
    private Transform player;
    private Transform target; // [87번] 이번 프레임의 목표(플레이어 또는 아군/적 - CombatTargeting)

    private State state = State.Idle;
    private float attackUnlockTime;

    // [8번 아이템] 플레이어를 못 보고 있는 시간을 쌓는 타이머입니다(EnemySightOverride 참고).
    private float sightLostTimer = 0f;

    void OnEnable()
    {
        // 방 입장(=이 오브젝트가 활성화되는 순간) 기준으로 공격 유예 시각을 다시 계산합니다.
        attackUnlockTime = Time.time + entryAttackDelay;
        sightLostTimer = 0f;
    }

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        stats = GetComponent<EnemyStats>();
        health = GetComponent<EnemyHealth>();
        attackBehavior = GetComponent<IEnemyAttackBehavior>(); // 근접/원거리 등 무엇이 붙어있든 동일하게 처리

        if (attackBehavior != null)
        {
            attackBehavior.Initialize(stats, transform);
        }
        else
        {
            Debug.LogWarning($"EnemyAIController ({gameObject.name}): IEnemyAttackBehavior 구현체가 없습니다. " +
                              "플레이어를 계속 쫓아가기만 하고 공격하지 않습니다.");
        }
    }

    void Start()
    {
        agent.speed = stats.moveSpeed;
        agent.acceleration = stats.moveAcceleration;
        // "물리적으로 부드럽게 서로 피해 다니는" 움직임이 아니라 "매 순간 판단해서 정해진
        // 속도로 이동/정지하는" 결정론적인 움직임(마인크래프트 스켈레톤 스타일)을 원하므로,
        // NavMeshAgent의 회피(avoidance) 시스템을 끕니다. 적끼리 서로 살짝 겹치고 지나갈 수
        // 있지만, 항상 예측 가능하고 일정한 속도로 움직이는 것을 우선합니다.
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
        // 회전은 이동 방향이 아니라 항상 플레이어를 바라보도록 이 컨트롤러(및 텔레그래프/돌진
        // 중에는 EnemyMeleeAttack)가 직접 처리합니다 (EnemyRangedAIController와 동일한 이유 -
        // 나중에 이동 방향과 바라보는 방향이 달라질 여지가 생겨도 안전합니다).
        agent.updateRotation = false;
        TryFindPlayer();
    }

    void Update()
    {
        // [38번 패시브 연동] Die()가 호출된 뒤에도 Destroy(gameObject)/디졸브 연출이 끝날
        // 때까지 이 컴포넌트는 계속 살아있어 Update()가 계속 돌아갑니다. 죽은 즉시 이동/공격
        // 판단에서 완전히 손을 떼지 않으면(특히 매료처럼 매 프레임 강제로 NavMeshAgent를
        // 움직이려는 효과가 있으면) 이미 명령을 받을 수 없는 에이전트에 접근하려다 오류가
        // 나거나 죽은 채로 계속 행동하는 문제가 있어 추가했습니다.
        if (health != null && health.IsDead) return;

        // [제어 2단계 개편] 강력 제어(기절/해킹) 중에는 이동/전투 판단을 전부 멈춥니다
        // (EnemyControlState 참고). 접촉 데미지는 이 Update()와 무관하게
        // EnemyMeleeContactTrigger가 물리 트리거로 처리하므로 그대로 유지됩니다.
        if (EnemyControlState.IsStrongControlledFor(gameObject))
        {
            agent.isStopped = true;
            return;
        }

        // [68번 패시브 연동] 넉백으로 밀리는 동안(ForcedPush)에는 이동/공격 판단을 쉽니다. 안 쉬면 매 프레임 플레이어 쪽으로
        // 다시 걸어와 밀림이 상쇄됩니다. 이미 진행 중인 충전(텔레그래프)은 공격 컴포넌트가 그대로 진행합니다.
        if (ForcedPush.IsBeingPushed(gameObject))
        {
            agent.isStopped = true;
            return;
        }

        // 레벨 빌드 시 모든 방의 적이 먼저 스폰되고 플레이어는 맨 마지막에 스폰되므로,
        // 이 컴포넌트가 Awake/Start되는 시점엔 아직 플레이어가 존재하지 않을 수 있습니다.
        // 찾을 때까지 조용히 매 프레임 재시도합니다 (경고 로그를 반복하지 않습니다).
        if (player == null)
        {
            TryFindPlayer();
            if (player == null) return;
        }

        if (attackBehavior != null && attackBehavior.IsAttacking) return; // 텔레그래프/대시 실행 중엔 이동+회전 전권을 위임

        // [87번 포섭] 이번 프레임의 목표를 정합니다(CombatTargeting). 아군 = 같은 방의 가장 가까운 적(없으면 플레이어를 따라다님),
        // 적 = 플레이어와 아군 중 가장 가까운 쪽(플레이어가 112번으로 투명하면 아군만). 아군이 없으면 예전과 같이 목표 = 플레이어.
        bool isAlly = AllyFaction.IsAlly(gameObject);
        target = CombatTargeting.Resolve(gameObject, isAlly, player, out bool followPlayer);
        if (isAlly && followPlayer)
        {
            state = State.Idle;
            var allySettings = GetComponent<AllyFaction>()?.ActiveSettings;
            CombatTargeting.FollowPlayer(agent, transform, player, allySettings, facingRotationSpeed);
            return;
        }

        // [112번 액티브 연동] 투명 중에는 플레이어를 인식하지 못합니다 - 추적 중이었어도 놓친 것으로 보고 대기로 돌아가 멈추며,
        // 새로 발견하지도 않습니다. 이미 진행 중인 돌진은 위에서 공격 컴포넌트에 맡겼으므로 끝까지 가고(돌진 준비는 공격 컴포넌트가
        // 스스로 취소), 투명이 끝나면 평소 규칙(탐지 범위 + 시야)으로 다시 찾습니다.
        // [87번] 투명한 플레이어는 목표 후보에서 빠지므로(CombatTargeting), 노릴 아군도 없으면 목표가 없어 여기서 대기합니다.
        if (target == null)
        {
            state = State.Idle;
            agent.isStopped = true;
            return;
        }

        float distance = Vector3.Distance(transform.position, target.position);

        if (isAlly) state = State.Chase; // [87번] 아군은 탐지 범위와 상관없이 바로 싸웁니다.

        switch (state)
        {
            case State.Idle:
                if (distance <= EnemyDetectRangeOverride.Resolve(stats.detectRange) && EnemySightOverride.CanSee(transform.position, target.position)) state = State.Chase;
                break;

            case State.Chase:
                // [8번 아이템] 시야가 유예 시간 넘게 계속 막혀 있으면 플레이어를 놓친 것으로 보고
                // Idle로 돌아가 완전히 멈춥니다. 아이템이 없으면 항상 false라 아무 일도 없습니다.
                if (!isAlly && EnemySightOverride.ShouldLoseTarget(ref sightLostTimer, transform.position, target.position))
                {
                    state = State.Idle;
                    agent.isStopped = true;
                    break;
                }

                UpdateFacing();

                bool inAttackRange = attackBehavior != null && distance <= attackBehavior.AttackRange;
                // [38번 패시브 연동] 매료(EnemyCharmState)로 억제되는 동안은 새 공격을
                // 시작하지 않습니다. 이미 진행 중이던 공격은 여기가 아니라 매료가 걸리는
                // 순간 EnemyCharmState가 ICancellableAttack.CancelAttack()으로 직접 중단시킵니다.
                bool canAttack = attackBehavior != null && attackBehavior.CanBeginAttack && !EnemyCharmState.Suppresses(gameObject);
                // [90번 액티브 연동] 물결이 다 퍼질 때까지(EnemyAttackLock)는 방 입장 유예처럼 새 공격(돌진/충전)을 시작하지 않습니다.
                bool entryDelayElapsed = Time.time >= attackUnlockTime && (isAlly || !EnemyAttackLock.IsLocked); // [87번] 90번 물결은 아군에 영향 없음

                if (inAttackRange && canAttack && entryDelayElapsed)
                {
                    agent.isStopped = true;
                    bool started = attackBehavior.BeginAttack(target.position);
                    if (!started)
                    {
                        // 공격 시작 조건(사거리+쿨다운)은 만족했지만, 공격 구현체가 스스로
                        // 판단해 공격을 시작하지 않았을 수 있습니다(예: EnemyMeleeAttack의
                        // 돌진 시작 전 시야(LOS) 체크에 걸려 벽 너머 플레이어를 향해 돌진을
                        // 시작하지 않은 경우 - 자세한 사유는 각 공격 구현체 참고). isStopped를
                        // 걸어둔 채로 방치하면 그대로 얼어붙어버리므로, 즉시 평소 접근 행동으로
                        // 되돌립니다.
                        agent.isStopped = false;
                        agent.SetDestination(target.position);
                    }
                }
                else
                {
                    // 사거리 밖이든, 아직 쿨다운이든, 방에 막 들어와 공격 유예 시간이 아직
                    // 안 지났든 - 기본 행동은 항상 플레이어에게 다가가는 것입니다.
                    agent.isStopped = false;
                    agent.SetDestination(target.position);
                }
                break;
        }
    }

    private void UpdateFacing()
    {
        Vector3 lookDir = target.position - transform.position;
        lookDir.y = 0f;
        if (lookDir.sqrMagnitude < 0.0001f) return;

        Quaternion targetRot = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir), facingRotationSpeed * Time.deltaTime);
        transform.rotation = targetRot;
    }

    private void TryFindPlayer()
    {
        var playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) player = playerObj.transform;
    }
}