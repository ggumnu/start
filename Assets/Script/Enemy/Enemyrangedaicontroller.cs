using UnityEngine;
using UnityEngine.AI;

// 원거리 적("스켈레톤"형) 전용 AI 컨트롤러입니다. EnemyAIController(근접용)와는 완전히
// 분리된 독립 컴포넌트입니다 - 근접 쪽은 "공격 시퀀스 동안 이동을 멈추고 전권 위임"
// 모델(IEnemyAttackBehavior)이라 원거리처럼 "사격 중에도 계속 움직이고, 사거리 안에서도
// 접근/후퇴 판단을 매 프레임 이어가야 하는" 요구사항과 맞지 않기 때문입니다.
//
// [행동 규칙 - 마인크래프트 스켈레톤 참고]
// 1. Idle: 플레이어가 detectRange 밖이면 아무것도 하지 않습니다.
// 2. 활성화되면 매 프레임 다음을 판단합니다.
//    - 시야가 막혀있으면(벽에 가려짐): 거리와 무관하게 무조건 접근합니다. 시야 확보가
//      거리 유지보다 우선순위가 높습니다.
//    - 시야가 뚫려있고 retreatRange보다 가까우면: 후퇴를 시작하고, 그 순간부터
//      retreatDuration(초) 동안은 거리가 다시 벌어져도 무조건 후퇴를 이어갑니다(형빈
//      요청 - "붙었다 멈췄다" 하는 딱딱한 느낌을 없애기 위함).
//    - (후퇴 지속 중이 아니면서) 시야가 뚫려있고 retreatRange보다는 멀지만 chaseRange
//      이내면: 제자리에 멈춥니다(추격을 멈추는 거리 - 사격 여부는 engageRange가 별도로
//      판단합니다).
//    - 그 외(chaseRange 밖): 접근합니다. engageRange 안쪽이라면 접근하는 동안에도
//      사격은 계속됩니다.
// 3. 발사는 이동 상태와 완전히 독립적으로 판단합니다 - 시야가 뚫려있고 engageRange 안이면
//    접근 중이든 제자리 정지 중이든 후퇴 중이든 상관없이 쿨다운만 차면 그냥 쏩니다.
// 4. 활성 상태인 동안은 이동 방향과 무관하게 항상 플레이어 쪽을 바라봅니다 (발사 방향이
//    이동 방향과 다를 수 있으므로 NavMeshAgent의 자동 회전은 꺼두고 직접 회전시킵니다).
//
// [변경 - 이동 판단을 "사격 개시 거리" 하나가 아니라 세 단계로 세분화 (형빈 요청)]
// 처음엔 stats.rangedAttackRange(탄환이 날아가는 거리)와 stats.engageRange(사격을
// 시작하는 거리)만 분리했었는데, 그러면 engageRange 안에 들어오는 즉시 제자리에 멈춰버려서
// "좀 더 파고들어와서 쏘는" 느낌을 낼 수 없었습니다. 그래서 "사격 여부 판단"과 "이동 여부
// 판단"을 완전히 분리했습니다 - engageRange는 오직 사격 여부만 결정하고(UpdateFiring),
// stats.chaseRange가 새로 추가되어 "계속 접근할지 멈출지"를 결정합니다(UpdateMovement).
// 자세한 예시는 EnemyStats.cs 상단 주석 참고. chaseRange를 engageRange와 같게 두면
// 예전과 동일한 동작(사격 개시 거리에 도달하는 즉시 멈춤)이 됩니다.
//
// [후퇴 위치 탐색 - "플레이어의 반대 방향"이 아니라 "플레이어로부터 가장 멀어지는 방향"]
// 자기 주변 여러 방향으로 후보 지점을 뽑아 NavMesh 위로 스냅한 뒤, 그 중 플레이어와의
// 거리가 가장 먼 지점으로 이동합니다. retreatSearchInterval 주기로만 재탐색합니다.
// 더 멀어질 수 있는 후보가 하나도 없으면 그 자리에 멈춥니다.
//
// [시야(LOS) 판정 기준점 - v1.13 변경]
// 이전에는 losHeightOffset이라는 float 값 하나로 "바닥에서 얼마나 위에서 선을 그을지"를
// 정했는데, 이 값은 사실 발사 위치(EnemyRangedAttack.firePoint)의 Y좌표를 형빈이 수동으로
// 재서 float 필드에 대입해 맞춘 결과였습니다. 즉 firePoint가 조금이라도 옮겨지면 이 값도
// 같이 수동으로 다시 맞춰줘야 하는 구조였습니다. 그래서 firePoint처럼 아예 Transform을
// 직접 연결하는 방식(losOrigin)으로 바꿨습니다 - losOrigin을 연결하면 그 Transform의
// 실제 위치를 그대로 시야 판정 기준점으로 쓰고, 비워두면 예전처럼 transform.position +
// losHeightOffset(폴백 값)을 사용합니다. losOrigin에 firePoint와 같은 오브젝트를
// 연결해도 되고, 시야 판정용으로 별도 오브젝트를 만들어 연결해도 됩니다.
//
// [v1.14 변경 - 방 입장 직후 사격 유예]
// EnemyAIController(근접용)와 동일한 이유로, 방에 들어오자마자 사거리 안이면 곧바로
// 사격을 시작해 플레이어가 반응할 시간이 부족하다는 문제가 있었습니다. 이 오브젝트도
// RoomVisibilityManager가 방을 보여줄 때 SetActive(true)로 활성화되므로, OnEnable()
// 시점이 곧 "플레이어가 이 방에 입장한 시점"입니다. OnEnable()에서 "사격을 시작해도 되는
// 시각"을 기록해두고, UpdateFiring()에서 이 시각이 지나기 전까지는 쿨다운/사거리/시야
// 조건을 다 만족해도 발사하지 않도록 했습니다. 이동(접근/후퇴) 판단(UpdateMovement)은
// 전혀 영향받지 않습니다.
//
// [v1.17 변경 - 원거리 적의 몸통 접촉 데미지 (선택적)]
// 기본적으로 원거리 적은 근접 적과 달리 몸에 닿아도 플레이어가 피해를 입지 않습니다
// (EnemyMeleeAttack이 없어 그 자식의 EnemyMeleeContactTrigger가 만들어지지 않기 때문).
// 지금 당장은 접촉 데미지를 안 쓰더라도, 나중에 "몸빵형 원거리 적"처럼 접촉 피해가 필요한
// 변형이 생길 수 있어 확장 지점을 미리 열어뒀습니다: 이 프리팹의 자식 오브젝트로
// EnemyMeleeContactTrigger(+ Is Trigger 콜라이더)를 직접 만들어 붙이면, Awake()가 그걸
// 찾아서 자동으로 Initialize(stats)를 호출해 연결합니다. 안 붙이면(자식에 없으면) 지금까지와
// 동일하게 접촉 데미지가 전혀 발생하지 않습니다 - 즉 이 기능은 완전히 선택 사항입니다.
//
// [에디터 설정 필요사항]
// - 이 컴포넌트가 붙는 프리팹 루트에는 NavMeshAgent와 EnemyStats(RequireComponent로
//   자동 요구됨)가 필요하고, EnemyRangedAttack도 같은 오브젝트에 붙어있어야 사격이
//   동작합니다.
// - wallLayerMask 인스펙터 필드에 시야를 차단할 벽 레이어를 지정해주세요 (다른 적은
//   일부러 시야를 막지 않도록 제외했습니다).
// - losOrigin에 시야 판정 기준점(보통 firePoint와 동일한 위치)을 연결해주세요. 비워두면
//   losHeightOffset을 폴백으로 사용합니다.
// - NavMeshAgent의 Radius/Height는 실제 모델 스케일(Capsule Collider 값)과 반드시
//   일치시켜주세요. 두 값이 어긋나면 NavMeshAgent 내부 계산이 불안정해질 수 있습니다.
// - (선택) 몸통 접촉 데미지가 필요하면 자식 오브젝트로 EnemyMeleeContactTrigger(+ Is
//   Trigger 콜라이더)를 만들어 연결하세요.
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(EnemyStats))]
public class EnemyRangedAIController : MonoBehaviour
{
    [Header("시야(LOS) 판정")]
    [Tooltip("이 레이어에 뭔가 걸리면 시야가 막힌 것으로 판정합니다. 벽 레이어만 지정하세요.")]
    public LayerMask wallLayerMask;
    [Tooltip("시야 판정 기준점입니다. 보통 firePoint와 같은 오브젝트를 연결하면 발사 위치와 " +
             "동일한 기준으로 시야를 판정합니다. 비워두면 이 오브젝트 위치 + losHeightOffset을 " +
             "대신 사용합니다.")]
    public Transform losOrigin;
    [Tooltip("losOrigin이 비어있을 때만 사용되는 기본 높이 오프셋입니다(바닥 높이 기준으로 " +
             "얼마나 위에서 선을 그을지). losOrigin을 연결하면 이 값은 쓰이지 않습니다.")]
    public float losHeightOffset = 1f;

    [Header("후퇴 위치 탐색")]
    [Tooltip("후퇴 후보 지점을 몇 방향으로 탐색할지입니다.")]
    public int retreatCandidateDirections = 8;
    [Tooltip("후퇴 후보 지점까지의 거리입니다. 방 크기보다 충분히 작아야 합니다.")]
    public float retreatSearchRadius = 2f;
    [Tooltip("후퇴 위치를 재탐색하는 주기(초)입니다.")]
    public float retreatSearchInterval = 0.4f;
    [Tooltip("NavMesh.SamplePosition 허용 오차 거리입니다.")]
    public float navMeshSampleDistance = 1f;
    [Tooltip("후퇴가 시작된 직후부터 최소 이 시간(초) 동안은 플레이어와의 거리와 무관하게 " +
             "무조건 후퇴를 계속합니다. 거리가 retreatRange를 벗어나는 즉시 멈추던 기존 " +
             "동작보다 좀 더 유연하게(형빈 표현) 움직이게 하기 위한 값입니다. 이 시간 " +
             "동안에도 사격은 engageRange 기준으로 독립적으로 계속됩니다(UpdateFiring 참고).")]
    public float retreatDuration = 0.5f;

    [Header("회전 설정")]
    [Tooltip("활성 상태일 때 플레이어를 바라보는 회전 속도입니다.")]
    public float facingRotationSpeed = 12f;

    [Header("입장 유예 (방에 막 들어왔을 때 즉시 사격하지 않도록)")]
    [Tooltip("이 오브젝트가 활성화된(=플레이어가 이 방에 들어와 화면에 나타난) 직후 이 시간 " +
             "동안은 사거리 안이라도 사격하지 않습니다. 접근/후퇴 이동에는 영향을 주지 " +
             "않습니다.")]
    public float entryAttackDelay = 0.5f;

    private NavMeshAgent agent;
    private EnemyStats stats;
    private EnemyRangedAttack rangedAttack;
    private EnemyHealth health;
    private Transform player;
    private Transform target;   // [87번] 이번 프레임의 목표(플레이어 또는 아군/적 - CombatTargeting)
    private bool isAllyNow;     // [87번] 이 적이 포섭된 아군인지(이번 프레임)

    private bool isActive = false;
    private float retreatSearchTimer = 0f;
    private float attackUnlockTime;

    // [8번 아이템] 플레이어를 못 보고 있는 시간을 쌓는 타이머입니다(EnemySightOverride 참고).
    private float sightLostTimer = 0f;

    // [후퇴 지속 타이머] 0보다 크면 "지금은 강제 후퇴 지속 시간 중"이라는 뜻입니다.
    // retreatRange 안으로 들어오는 순간 retreatDuration으로 채워지고, 매 프레임
    // Time.deltaTime만큼 줄어듭니다. 이 값이 0보다 큰 동안은 거리가 얼마든 무조건
    // TryRetreat()을 호출합니다(UpdateMovement 참고).
    private float retreatCommitTimer = 0f;

    void OnEnable()
    {
        // 방 입장(=이 오브젝트가 활성화되는 순간) 기준으로 사격 유예 시각을 다시 계산합니다.
        attackUnlockTime = Time.time + entryAttackDelay;
        sightLostTimer = 0f;
    }

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        stats = GetComponent<EnemyStats>();
        rangedAttack = GetComponent<EnemyRangedAttack>();
        health = GetComponent<EnemyHealth>();

        // 회전은 이동 방향이 아니라 항상 플레이어를 바라보도록 이 스크립트가 직접
        // 처리합니다 (후퇴 중에도 플레이어를 바라보며 쏴야 하므로).
        agent.updateRotation = false;

        if (rangedAttack != null)
        {
            rangedAttack.Initialize(stats);
        }
        else
        {
            Debug.LogWarning($"EnemyRangedAIController ({gameObject.name}): EnemyRangedAttack 컴포넌트가 " +
                              "없어 사격을 하지 않습니다. 이동(추적/후퇴)만 동작합니다.");
        }

        // 몸통 접촉 데미지는 완전히 선택 사항입니다 - 자식에 EnemyMeleeContactTrigger를
        // 직접 만들어 붙인 프리팹에서만 찾아서 연결하고, 없으면 조용히 건너뜁니다
        // (파일 상단 "v1.17 변경" 참고).
        var contactTrigger = GetComponentInChildren<EnemyMeleeContactTrigger>();
        if (contactTrigger != null)
        {
            contactTrigger.Initialize(stats);
        }
    }

    void Start()
    {
        agent.speed = stats.moveSpeed;
        agent.acceleration = stats.moveAcceleration;
        // EnemyAIController.Start()와 동일한 이유로 회피 시스템을 끕니다 - "항상 일정한
        // 속도로, 매 순간 판단해서 움직이는" 결정론적 이동을 원하기 때문입니다.
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
        TryFindPlayer();

        // 개체마다 재탐색 타이머 시작점을 살짝 흩어서, 같은 방의 원거리 적들이 전부 같은
        // 프레임에 몰려 NavMesh 질의를 쏟아내는 상황을 줄입니다.
        retreatSearchTimer = Random.Range(0f, retreatSearchInterval);
    }

    void Update()
    {
        // [38번 패시브 연동] Die()가 호출된 뒤에도 Destroy(gameObject)/디졸브 연출이 끝날
        // 때까지 이 컴포넌트는 계속 살아있어 Update()가 계속 돌아갑니다. 죽은 즉시 이동/사격
        // 판단에서 완전히 손을 떼지 않으면(특히 매료처럼 매 프레임 강제로 NavMeshAgent를
        // 움직이려는 효과가 있으면) 이미 명령을 받을 수 없는 에이전트에 접근하려다 오류가
        // 나거나("Resume"/"SetDestination" 관련 콘솔 오류), 죽은 직후에도 사격이 몇 번 더
        // 나가는 문제가 있어 추가했습니다.
        if (health != null && health.IsDead) return;

        // [제어 2단계 개편] 강력 제어(기절/해킹) 중에는 이동/사격 판단을 전부 멈춥니다
        // (EnemyControlState 참고). 접촉 데미지는 이 Update()와 무관하게 물리 트리거로
        // 처리되므로 그대로 유지됩니다.
        if (EnemyControlState.IsStrongControlledFor(gameObject))
        {
            agent.isStopped = true;
            return;
        }

        // [68번 패시브 연동] 넉백으로 밀리는 동안(ForcedPush)에는 이동/사격 판단을 쉽니다. 안 쉬면 접근/후퇴 이동이 밀림을
        // 상쇄합니다. 밀리는 시간은 아주 짧습니다(68번 Push Duration).
        if (ForcedPush.IsBeingPushed(gameObject))
        {
            agent.isStopped = true;
            return;
        }

        // 레벨 빌드 시 모든 방의 적이 먼저 스폰되고 플레이어는 맨 마지막에 스폰되므로,
        // 이 컴포넌트가 Awake/Start되는 시점엔 아직 플레이어가 존재하지 않을 수 있습니다.
        if (player == null)
        {
            TryFindPlayer();
            if (player == null) return;
        }

        // [87번 포섭] 이번 프레임의 목표를 정합니다(CombatTargeting). 아군 = 같은 방의 가장 가까운 적(없으면 플레이어를 따라다님),
        // 적 = 플레이어와 아군 중 가장 가까운 쪽(플레이어가 112번으로 투명하면 아군만). 아군이 없으면 예전과 같이 목표 = 플레이어.
        bool isAlly = AllyFaction.IsAlly(gameObject);
        isAllyNow = isAlly;
        target = CombatTargeting.Resolve(gameObject, isAlly, player, out bool followPlayer);
        if (isAlly && followPlayer)
        {
            isActive = false;
            retreatCommitTimer = 0f;
            var allySettings = GetComponent<AllyFaction>()?.ActiveSettings;
            CombatTargeting.FollowPlayer(agent, transform, player, allySettings, facingRotationSpeed);
            return;
        }

        // [112번 액티브 연동] 투명 중에는 플레이어를 인식하지 못합니다 - 추적 중이었어도 놓친 것으로 보고 대기로 돌아가 멈추며,
        // 새로 발견하지도 않습니다. 이미 시작한 연사는 EnemyRangedAttack이 끝까지 쏩니다(조준은 투명해진 순간의 방향 유지).
        // [87번] 투명한 플레이어는 목표 후보에서 빠지므로, 노릴 아군도 없으면 목표가 없어 여기서 대기합니다.
        if (target == null)
        {
            isActive = false;
            retreatCommitTimer = 0f;
            agent.isStopped = true;
            return;
        }

        float distance = Vector3.Distance(transform.position, target.position);

        if (isAlly) isActive = true; // [87번] 아군은 탐지 범위와 상관없이 바로 싸웁니다.

        if (!isActive)
        {
            if (distance <= EnemyDetectRangeOverride.Resolve(stats.detectRange) && EnemySightOverride.CanSee(transform.position, target.position))
            {
                isActive = true;
            }
            else
            {
                return; // 아직 감지 범위 밖 - 대기
            }
        }

        // [8번 아이템] 시야가 유예 시간 넘게 계속 막혀 있으면 플레이어를 놓친 것으로 보고
        // Idle로 돌아가 완전히 멈춥니다(이동/회전/사격 판단 전부 건너뜀). 아이템이 없으면
        // 항상 false라 아무 일도 없습니다.
        if (!isAlly && EnemySightOverride.ShouldLoseTarget(ref sightLostTimer, transform.position, target.position))
        {
            isActive = false;
            retreatCommitTimer = 0f;
            agent.isStopped = true;
            return;
        }

        bool hasLineOfSight = CheckLineOfSight();

        UpdateMovement(distance, hasLineOfSight);
        UpdateFacing();
        UpdateFiring(distance, hasLineOfSight);
    }

    // ---------- 이동 판단 ----------
    private void UpdateMovement(float distance, bool hasLineOfSight)
    {
        // [38번 패시브 연동] 매료로 강제 접근 중인 동안은 후퇴 판정 자체를 건너뛰고
        // 무조건 접근합니다(원거리 적이라도 매료 중엔 후퇴하지 않고 다가가야 함 - 형빈
        // 기획). SuppressesMovementFor는 매료가 끝나면(=충전 종료 즉시) 유예 없이 바로
        // false가 됩니다 - 매료 종료 후의 유예시간은 아래 UpdateFiring()의 사격/스킬 개시
        // 금지(Suppresses)에만 적용되고, 접근 자체는 매료가 끝나는 즉시 멈춰야 하기
        // 때문입니다(형빈 요청 - "매료가 끝나고 일정 시간 동안은 사격, 스킬의 유예기간").
        if (EnemyCharmState.SuppressesMovementFor(gameObject))
        {
            ApproachPlayer();
            return;
        }

        // 후퇴 지속 타이머는 시야 여부와 무관하게 "명령이 내려진 시점부터" 흘러갑니다 -
        // 아래에서 시야가 막혀 이번 프레임엔 실제로 후퇴하지 못하더라도, 남은 지속
        // 시간 자체는 계속 줄어듭니다(형빈 표현 그대로 "후퇴 명령이 내려진 직후부터
        // n초 동안").
        if (retreatCommitTimer > 0f) retreatCommitTimer -= Time.deltaTime;

        if (!hasLineOfSight)
        {
            // 시야가 막혀 있으면 애초에 쏠 수 없으므로, 거리가 아무리 가까워도 접근을
            // 우선합니다 (거리 유지보다 시야 확보가 우선순위가 높음).
            ApproachPlayer();
            return;
        }

        // 아직 강제 후퇴 지속 중이 아닌데 새로 후퇴 범위 안으로 들어왔다면, 지금이
        // "후퇴 명령이 내려지는 순간"입니다 - 지속 타이머를 채웁니다.
        if (distance <= stats.retreatRange && retreatCommitTimer <= 0f)
        {
            retreatCommitTimer = retreatDuration;
        }

        if (retreatCommitTimer > 0f)
        {
            // 후퇴 지속 시간 동안은 플레이어와의 거리와 무관하게 무조건 후퇴를
            // 이어갑니다(형빈 요청 - 거리 유지 때문에 끊겼다 붙었다 하는 딱딱한 느낌을
            // 없애기 위함). 사격은 engageRange 기준으로 완전히 독립적으로 계속됩니다.
            TryRetreat();
        }
        else if (distance <= stats.chaseRange)
        {
            // 추격을 멈추는 거리 안 + 후퇴 거리 밖 - 제자리에서 사격만 합니다. 사격
            // 여부 자체는 engageRange를 보는 UpdateFiring()이 별도로 판단합니다.
            agent.isStopped = true;
        }
        else
        {
            // chaseRange 밖 - 계속 접근합니다. engageRange 안이라면 접근하는 동안에도
            // UpdateFiring()이 계속 사격을 진행합니다.
            ApproachPlayer();
        }
    }

    private void ApproachPlayer()
    {
        // 방어적 가드입니다 - 정상적인 경우엔 위 Update()의 IsDead 체크가 먼저 걸러주지만,
        // 그 외의 이유로 에이전트가 비활성화되거나 NavMesh 위에 있지 않게 되는 경우에도
        // "Resume"/"SetDestination" 콘솔 오류 없이 조용히 아무 일도 하지 않도록 합니다.
        if (agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh) return;

        agent.isStopped = false;
        agent.SetDestination(target.position);
    }

    // "플레이어의 반대 방향"이 아니라 "플레이어로부터 가장 멀어지는, 실제로 갈 수 있는
    // 방향"을 주기적으로 재탐색합니다.
    private void TryRetreat()
    {
        retreatSearchTimer -= Time.deltaTime;
        if (retreatSearchTimer > 0f) return; // 재탐색 주기 전에는 기존 목적지를 그대로 유지

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
            // 더 멀어질 수 있는 방향이 없습니다. 억지로 플레이어 쪽으로 파고들지 않고
            // 제자리에 멈춥니다.
            agent.isStopped = true;
        }
    }

    // ---------- 회전 ----------
    private void UpdateFacing()
    {
        Vector3 lookDir = target.position - transform.position;
        lookDir.y = 0f;
        if (lookDir.sqrMagnitude < 0.0001f) return;

        Quaternion targetRot = Quaternion.LookRotation(lookDir);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, facingRotationSpeed * Time.deltaTime);
    }

    // ---------- 발사 판단 (이동 상태와 무관하게 독립적으로 동작) ----------
    private void UpdateFiring(float distance, bool hasLineOfSight)
    {
        // [38번 패시브 연동] 매료 중이거나, 매료가 막 끝난 뒤의 사격/스킬 유예시간
        // (postCharmAttackDelay) 안이면 새 사격을 시작하지 않습니다 - 매료로 플레이어에게
        // 바짝 붙은 채로 매료가 풀리자마자 영거리에서 바로 쏘는 것을 막기 위한 유예입니다
        // (방 입장 유예 시간과 같은 역할, 형빈 요청). 이미 진행 중이던 연사는 매료가 걸리는
        // 순간 EnemyCharmState가 EnemyRangedAttack.CancelBurst()를 직접 호출해 즉시
        // 중단시킵니다.
        if (EnemyCharmState.Suppresses(gameObject)) return;

        if (!hasLineOfSight) return;
        if (distance > stats.engageRange) return;
        if (rangedAttack == null) return;
        if (Time.time < attackUnlockTime) return; // 방 입장 유예 시간 - 이동 판단에는 영향 없음
        if (EnemyAttackLock.IsLocked && !isAllyNow) return;      // [87번] 아군은 90번 물결 영향 없음 / [90번 액티브 연동] 물결이 다 퍼질 때까지 새 사격 금지 - 이동 판단에는 영향 없음

        Vector3 dir = target.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;

        rangedAttack.TryFire(dir.normalized);
    }

    // ---------- 시야 판정 ----------
    // losOrigin이 설정되어 있으면 그 위치를 그대로 기준점으로 쓰고, 플레이어 쪽도 같은
    // 높이만큼 올려서 비교합니다(즉 losOrigin의 실제 월드 높이가 곧 "시야 판정 높이"가
    // 됩니다). losOrigin이 비어있으면 기존처럼 transform.position + losHeightOffset을
    // 기준점으로 씁니다.
    private bool CheckLineOfSight()
    {
        Vector3 from = losOrigin != null ? losOrigin.position : transform.position + Vector3.up * losHeightOffset;
        float heightOffset = from.y - transform.position.y;
        Vector3 to = target.position + Vector3.up * heightOffset;
        return !Physics.Linecast(from, to, wallLayerMask, QueryTriggerInteraction.Ignore);
    }

    private void TryFindPlayer()
    {
        var playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) player = playerObj.transform;
    }
}