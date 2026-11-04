using UnityEngine;
using UnityEngine.AI;

// 보스 최상위 컨트롤러입니다. 1레벨 보스: 접근/제자리/후퇴하며 연사(EnemyRangedAttack), 연사 N번마다 전방위(EnemyRadialAttack),
// 이와 별개로 쿨타임마다 연속 돌진(EnemyChainDashAttack). 세 공격은 서로를 끊지 않습니다(진행 중인 공격이 끝나야 다음 공격).
// 다른 스크립트는 "GetComponent<BossAIController>() != null"로 보스를 판별합니다(체력바, 매료/오한/넉백/87번 제외 등) - 다른 보스 패턴은
// 이 클래스를 상속해 만들어야 같은 보스 규칙을 받습니다(BossLv2AIController). 이동/시야/후퇴 부품은 protected로 물려줍니다.
// 주의: 보스 수치는 EnemyDefinition을 거치지 않으므로 각 컴포넌트 인스펙터에 직접 입력. EnemyStats의 rangedAttackRange(탄환 거리) /
// engageRange(사격 개시) / chaseRange(접근 멈춤) / retreatRange(후퇴 시작)는 서로 다른 값 - 예) 5/4/3이면 5 이내 사격, 4까지 접근, 3 이하 후퇴.
// 주의: 프리팹에 EnemyAIController/EnemyRangedAIController를 같이 붙이지 말 것(같은 NavMeshAgent를 서로 움직임).
// 설계 배경은 Docs/설계노트.md [보스 컨트롤러].
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(EnemyStats))]
public class BossAIController : MonoBehaviour
{
    [Header("시야(LOS) 판정")]
    [Tooltip("이 레이어에 걸리면 시야가 막힌 것으로 판정. 벽 레이어만 지정.")]
    public LayerMask wallLayerMask;
    [Tooltip("시야 판정 기준점(보통 firePoint와 같은 오브젝트). 비우면 이 오브젝트 위치 + Los Height Offset.")]
    public Transform losOrigin;
    public float losHeightOffset = 1f;

    [Header("후퇴 위치 탐색 (평소 상태에서만 - 공격 위임 중엔 적용 안 됨)")]
    public int retreatCandidateDirections = 8;
    public float retreatSearchRadius = 2f;
    public float retreatSearchInterval = 0.4f;
    public float navMeshSampleDistance = 1f;
    [Tooltip("후퇴가 시작되면 최소 이 시간(초) 동안 거리와 무관하게 후퇴를 계속(사격 판단은 별개).")]
    public float retreatDuration = 0.5f;

    [Header("회전 설정 (평소 접근/후퇴 중 목표를 바라보는 속도)")]
    public float facingRotationSpeed = 12f;

    [Header("입장 유예 (전투 시작 직후 즉시 공격하지 않도록)")]
    [Tooltip("이 시간 동안은 공격을 시작하지 않음(이동은 함).")]
    public float entryAttackDelay = 0.5f;

    [Header("전방위 공격 패턴 (1레벨 보스 전용)")]
    [Tooltip("연사를 이 횟수만큼 할 때마다 다음 공격이 전방위(3 = 연사-연사-연사-전방위). 전방위 수치는 EnemyRadialAttack에서.")]
    public int burstsBeforeRadial = 3;

    protected NavMeshAgent agent;
    protected EnemyStats stats;
    protected EnemyRangedAttack rangedAttack;
    private EnemyChainDashAttack dashAttack;
    private EnemyRadialAttack radialAttack;
    protected Transform player;
    protected Transform target; // [87번] 이번 프레임의 목표(플레이어 또는 아군 - CombatTargeting, 보스는 항상 적 편)

    private int burstsSinceRadial = 0;

    private float retreatSearchTimer = 0f;
    protected float attackUnlockTime;

    // 0보다 크면 강제 후퇴 지속 중(retreatRange 안으로 들어온 순간 retreatDuration으로 채워짐). 공격 위임 중에는 멈춰 있다가 이어서 흐름.
    private float retreatCommitTimer = 0f;

    // 지금 강제 후퇴 지속 시간 중인지(BossLv2AIController의 후퇴 샷건 판단용).
    protected bool IsRetreatCommitted => retreatCommitTimer > 0f;

    // 방 입장(활성화) 기준으로 공격 유예 시각을 다시 계산합니다.
    protected virtual void OnEnable()
    {
        attackUnlockTime = Time.time + entryAttackDelay;
    }

    protected virtual void Awake()
    {
        CacheCoreComponents();
        dashAttack = GetComponent<EnemyChainDashAttack>();
        radialAttack = GetComponent<EnemyRadialAttack>();

        if (rangedAttack == null)
        {
            Debug.LogWarning($"BossAIController ({gameObject.name}): EnemyRangedAttack이 없어 사격을 하지 않습니다.");
        }
        if (dashAttack == null)
        {
            Debug.LogWarning($"BossAIController ({gameObject.name}): EnemyChainDashAttack이 없어 연속 돌진을 하지 않습니다.");
        }
        if (radialAttack == null)
        {
            Debug.LogWarning($"BossAIController ({gameObject.name}): EnemyRadialAttack이 없어 전방위 공격을 하지 않습니다.");
        }

        if (rangedAttack != null) rangedAttack.Initialize(stats);
        // dashAttack.Initialize()가 자식의 EnemyMeleeContactTrigger를 찾아 돌진 피해원으로 연결합니다.
        if (dashAttack != null) dashAttack.Initialize(stats, transform);
        if (radialAttack != null) radialAttack.Initialize(stats, transform);

        TryFindPlayer();
    }

    // 모든 보스 공통: 이동/스탯/연사 컴포넌트를 찾고, 회전은 이동 방향이 아니라 컨트롤러/공격 연출이 직접 맡도록 자동 회전을 끕니다.
    protected void CacheCoreComponents()
    {
        agent = GetComponent<NavMeshAgent>();
        stats = GetComponent<EnemyStats>();
        rangedAttack = GetComponent<EnemyRangedAttack>();
        agent.updateRotation = false;
    }

    protected virtual void Start()
    {
        agent.speed = stats.moveSpeed;
        agent.acceleration = stats.moveAcceleration;
        // 다른 AI와 같은 이유로 NavMeshAgent 회피를 끕니다(일정한 속도 우선).
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;

        retreatSearchTimer = Random.Range(0f, retreatSearchInterval);
    }

    protected virtual void Update()
    {
        // 강력 제어(수면/해킹) 중에는 판단을 전부 멈춥니다(진행 중 공격은 제어가 걸리는 순간 CancelAttack으로 이미 끊김).
        if (EnemyControlState.IsStrongControlledFor(gameObject))
        {
            agent.isStopped = true;
            return;
        }

        if (player == null)
        {
            TryFindPlayer();
            if (player == null) return;
        }

        // 돌진/전방위(충전·스핀) 진행 중에는 그 컴포넌트에 전권을 맡깁니다.
        if (dashAttack != null && dashAttack.IsAttacking) return;
        if (radialAttack != null && radialAttack.IsAttacking) return;

        // [112번 액티브 연동] 투명 중에는 보스도 플레이어를 인식하지 못해 이동/사격/돌진/전방위 공격을 새로 시작하지 않고 멈춥니다.
        // 진행 중이던 돌진은 그 돌진까지만 끝내고(다음 연속 돌진은 EnemyChainDashAttack이 취소), 충전은 각 공격 컴포넌트가 취소하며,
        // 이미 시작한 연사는 EnemyRangedAttack이 끝까지 쏩니다.
        // [87번 포섭] 보스의 목표 = 플레이어와 아군 중 가장 가까운 쪽(플레이어가 투명하면 아군만). 노릴 대상이 없으면(투명한 플레이어뿐)
        // 위 112번 규칙대로 멈춥니다. 아군이 없으면 예전과 같이 목표 = 플레이어.
        target = CombatTargeting.Resolve(gameObject, false, player, out _);
        if (target == null)
        {
            agent.isStopped = true;
            return;
        }

        UpdateNormalRanged();
    }

    // ---------- 평소 상태 (접근/제자리/후퇴 + 연사 판단 + 전방위/돌진 시작 판단) ----------
    private void UpdateNormalRanged()
    {
        // [90번] 보스는 물결의 공격 금지(EnemyAttackLock)를 받지 않습니다(형빈 확정 - 보스는 진행 중인 연사만 끊기고 총알만 지워짐).
        bool entryDelayElapsed = Time.time >= attackUnlockTime;

        // 돌진은 사거리/시야와 무관하게 쿨타임만 보고, 연사 중이 아닐 때 시작합니다(형빈 확인).
        bool rangedBusy = rangedAttack != null && rangedAttack.IsBursting;
        bool dashReady = dashAttack != null && entryDelayElapsed && !rangedBusy && dashAttack.CanBeginAttack;

        if (dashReady)
        {
            agent.isStopped = true;
            if (dashAttack.BeginAttack(target.position))
            {
                return; // 이번 프레임은 돌진에 전권 위임 - 다음 프레임부터 위 Update()의 IsAttacking 체크가 전권을 가져갑니다.
            }
            agent.isStopped = false; // 이론상 거의 발생하지 않지만, 시작 실패 시 평소대로 복구
        }

        float distance = Vector3.Distance(transform.position, target.position);
        bool hasLineOfSight = CheckLineOfSight();

        UpdateMovement(distance, hasLineOfSight);
        UpdateFacing();
        UpdateFiring(distance, hasLineOfSight, entryDelayElapsed);
    }

    protected void UpdateMovement(float distance, bool hasLineOfSight)
    {
        // 후퇴 지속 타이머는 시야와 무관하게 흘러갑니다.
        if (retreatCommitTimer > 0f) retreatCommitTimer -= Time.deltaTime;

        if (!hasLineOfSight)
        {
            // 시야가 막히면 거리와 무관하게 접근(시야 확보가 거리 유지보다 우선).
            ApproachPlayer();
            return;
        }

        if (distance <= stats.retreatRange && retreatCommitTimer <= 0f)
        {
            retreatCommitTimer = retreatDuration;
        }

        if (retreatCommitTimer > 0f)
        {
            // 지속 시간 동안은 거리와 무관하게 후퇴(형빈 요청). 사격 판단은 별개.
            TryRetreat();
        }
        else if (distance <= stats.chaseRange)
        {
            agent.isStopped = true;
        }
        else
        {
            ApproachPlayer();
        }
    }

    protected void ApproachPlayer()
    {
        agent.isStopped = false;
        agent.SetDestination(target.position);
    }

    // 단순 반대 방향이 아니라 "목표로부터 가장 멀어지는, 실제로 갈 수 있는 방향"을 주기적으로 재탐색합니다(EnemyRangedAIController와 같음).
    protected void TryRetreat()
    {
        retreatSearchTimer -= Time.deltaTime;
        if (retreatSearchTimer > 0f) return;

        retreatSearchTimer = retreatSearchInterval;

        float currentDistance = Vector3.Distance(transform.position, target.position);
        Vector3 bestCandidate = transform.position;
        float bestDistance = currentDistance;
        bool found = false;

        for (int i = 0; i < retreatCandidateDirections; i++)
        {
            float angle = (360f / retreatCandidateDirections) * i;
            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            Vector3 candidate = transform.position + dir * retreatSearchRadius;

            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, navMeshSampleDistance, NavMesh.AllAreas))
                continue;

            NavMeshPath path = new NavMeshPath();
            if (!agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete)
                continue;

            float candidateDistance = Vector3.Distance(hit.position, target.position);
            if (candidateDistance > bestDistance)
            {
                bestDistance = candidateDistance;
                bestCandidate = hit.position;
                found = true;
            }
        }

        if (found)
        {
            agent.isStopped = false;
            agent.SetDestination(bestCandidate);
        }
        else
        {
            // 더 멀어질 곳이 없으면 목표 쪽으로 파고들지 않고 멈춥니다.
            agent.isStopped = true;
        }
    }

    protected void UpdateFacing()
    {
        Vector3 lookDir = target.position - transform.position;
        lookDir.y = 0f;
        if (lookDir.sqrMagnitude < 0.0001f) return;

        Quaternion targetRot = Quaternion.LookRotation(lookDir);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, facingRotationSpeed * Time.deltaTime);
    }

    protected bool CheckLineOfSight()
    {
        Vector3 from = losOrigin != null ? losOrigin.position : transform.position + Vector3.up * losHeightOffset;
        float heightOffset = from.y - transform.position.y;
        Vector3 to = target.position + Vector3.up * heightOffset;
        return !Physics.Linecast(from, to, wallLayerMask, QueryTriggerInteraction.Ignore);
    }

    // 연사 N번마다 전방위 차례. 카운터는 연사를 "시작"할 때 오르고, 돌진이 끼어들어도 초기화되지 않습니다(형빈 확인).
    private void UpdateFiring(float distance, bool hasLineOfSight, bool entryDelayElapsed)
    {
        if (!hasLineOfSight) return;
        if (distance > stats.engageRange) return; // 탄환이 날아가는 거리(rangedAttackRange)가 아니라 사격 개시 거리를 봅니다.
        if (rangedAttack == null) return;
        if (!entryDelayElapsed) return;
        if (!rangedAttack.IsReady) return; // 연사 중이거나 공격속도 쿨다운 중이면 대기

        if (burstsSinceRadial >= burstsBeforeRadial)
        {
            if (radialAttack != null && radialAttack.BeginAttack(target.position))
            {
                burstsSinceRadial = 0; // 전방위 공격 시작 - 다음 사이클은 다시 연사부터 시작합니다.
            }
            return;
        }

        Vector3 dir = target.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;

        if (rangedAttack.TryFire(dir.normalized))
        {
            burstsSinceRadial++;
        }
    }

    protected void TryFindPlayer()
    {
        var playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) player = playerObj.transform;
    }
}