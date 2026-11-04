using UnityEngine;
using UnityEngine.AI;

// 적끼리 서로 완전히 겹쳐버리는 것을 막는 보조 컴포넌트입니다. 근접(EnemyAIController +
// EnemyMeleeAttack)이든 원거리(EnemyRangedAIController)든 상관없이, NavMeshAgent가 붙어있는
// 적 프리팹이라면 이 컴포넌트만 추가로 붙이면 됩니다.
//
// [왜 필요한가]
// 이 프로젝트는 EnemyAIController/EnemyRangedAIController의 Start()에서
// agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance로 NavMeshAgent의
// 내장 회피 시스템을 의도적으로 꺼뒀습니다("예측 가능하고 일정한 속도로 움직이는" 결정론적
// 이동을 위해서입니다). 그 대가로 여러 적이 같은 목표(플레이어)로 동시에 접근/돌진하면
// 서로를 피하지 않고 그대로 겹쳐버릴 수 있습니다. EnemyMeleeAttack의 돌진(Dash)은 자체적으로
// 다른 적과 부딪히면 그 자리에서 멈추도록 처리했지만(SphereCast + OverlapSphere), 그건
// "돌진 중"에만 적용되는 처리라 평소 추적/후퇴 이동에는 적용되지 않습니다.
//
// [동작 방식 - 물리 엔진 대신 NavMeshAgent.Move()]
// Rigidbody 충돌 해결(비-Kinematic + 물리 엔진)에 맡기지 않는 이유는, 이 프로젝트의 적들은
// 전부 rb.isKinematic = true로 되어 있고(NavMeshAgent가 위치를 직접 관리하는 구조와 충돌을
// 피하기 위함) NavMeshAgent와 비-Kinematic Rigidbody를 함께 쓰면 위치 제어권을 두고 서로
// 다퉈 불안정해지기 때문입니다. 대신 이 컴포넌트는 경로 계산(추적/후퇴/돌진)에는 전혀
// 관여하지 않고, LateUpdate에서 그 프레임의 모든 이동이 끝난 뒤 "지금 다른 적과 겹친 만큼만"
// NavMeshAgent.Move()로 살짝 밀어내는 후처리만 담당합니다 - 이 프로젝트의 다른 모든 이동과
// 동일하게 NavMesh 경계 안에서만 밀리므로 벽을 뚫고 나가는 일도 없습니다.
//
// [에디터 설정 필요사항]
// - NavMeshAgent가 붙은 적 프리팹(근접/원거리 모두)에 이 컴포넌트를 추가하세요.
// - enemyLayerMask에 "적이 서 있는 물리 레이어"를 지정하세요. 벽/플레이어 레이어는 넣지
//   마세요 - 이 컴포넌트는 오직 적끼리만 밀어내는 용도입니다.
// - separationRadius는 보통 NavMeshAgent.Radius의 2배(=몸통이 서로 딱 맞닿는 거리) 정도로
//   맞추면 자연스럽습니다.
[RequireComponent(typeof(NavMeshAgent))]
public class EnemySeparation : MonoBehaviour
{
    [Tooltip("이 반지름 안에 다른 적의 콜라이더가 있으면 밀어냅니다. 보통 " +
             "NavMeshAgent.Radius의 2배(=몸통이 서로 딱 맞닿는 거리) 정도로 맞추세요.")]
    public float separationRadius = 0.8f;

    [Tooltip("겹침을 감지할 레이어입니다. 반드시 '적이 서 있는 물리 레이어'만 지정하세요 - " +
             "벽/플레이어 레이어가 섞이면 그쪽으로도 밀려나는 원치 않는 동작이 생깁니다.")]
    public LayerMask enemyLayerMask;

    [Tooltip("한 프레임에 밀어낼 수 있는 최대 속도입니다. 너무 크면 돌진/추적 이동과 겹쳐 " +
             "떨림이 생길 수 있고, 너무 작으면 겹침 해소가 느려 보일 수 있습니다.")]
    public float separationSpeed = 6f;

    [Tooltip("적의 '몸통' 콜라이더가 Is Trigger 상태라면 Collide로 바꿔주세요. 기본값(Ignore)은 " +
             "트리거가 아닌 일반 콜라이더만 감지합니다 - EnemyMeleeContactTrigger처럼 Is " +
             "Trigger가 켜진 콜라이더만 Enemy 레이어에 있고 막는 용도의 일반 콜라이더가 " +
             "따로 없다면, Ignore 상태에서는 아무것도 감지되지 않아 전혀 밀려나지 않습니다.")]
    public QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

    [Tooltip("켜두면 매 프레임 감지된 겹침 개수를 콘솔에 출력합니다 - 0이 계속 찍히면 " +
             "감지 자체가 안 되고 있다는 뜻이니 레이어/트리거 설정을 다시 확인하세요. " +
             "확인이 끝나면 꺼주세요(로그가 매 프레임 쌓입니다).")]
    public bool debugLogOverlaps = false;

    private NavMeshAgent agent;
    private static readonly Collider[] overlapBuffer = new Collider[8];

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    void LateUpdate()
    {
        // 이 프레임에 다른 스크립트(EnemyAIController, EnemyMeleeAttack, EnemyRangedAIController
        // 등)가 이미 이동을 다 처리한 뒤에, 그 결과 겹친 부분만 마지막에 보정합니다.
        if (agent == null || !agent.enabled) return;

        Vector3 push = ComputeSeparationPush();
        if (push.sqrMagnitude < 0.0001f) return;

        float maxStep = separationSpeed * Time.deltaTime;
        if (push.magnitude > maxStep)
        {
            push = push.normalized * maxStep;
        }

        agent.Move(push);
    }

    // 겹쳐있는 다른 적들로부터 "밀려나야 할 방향 × 겹친 깊이"를 전부 더한 벡터를 계산합니다.
    // 여러 적과 동시에 겹쳐있으면 그만큼 더 강하게 밀려납니다.
    private Vector3 ComputeSeparationPush()
    {
        int count = Physics.OverlapSphereNonAlloc(
            transform.position, separationRadius, overlapBuffer, enemyLayerMask, triggerInteraction);

        if (debugLogOverlaps)
        {
            Debug.Log($"EnemySeparation ({gameObject.name}): OverlapSphere 감지 수 = {count}" +
                      (count == 0 ? " (0이 계속 찍히면 레이어/트리거 설정을 확인하세요)" : ""));
        }

        Vector3 push = Vector3.zero;

        for (int i = 0; i < count; i++)
        {
            var col = overlapBuffer[i];
            // [주의] transform.root가 아니라 IsChildOf(transform)을 씁니다. 이 프로젝트는 같은
            // 레벨의 모든 적/벽/바닥이 결국 LevelBuilder 밑의 같은 최상위 조상(contentRoot ->
            // Room_X -> LevelRoot -> LevelBuilder)을 공유하는 구조라, transform.root끼리
            // 비교하면 "다른 적"조차 항상 "나 자신"으로 잘못 판정됩니다(모델이 같아서가
            // 아니라, 씬 계층 구조상 모든 오브젝트의 root가 우연히 똑같기 때문입니다).
            // IsChildOf(transform)은 "내 오브젝트 자신이거나 내 자식 계층 안에 있는지"만
            // 보므로 씬 전체의 공유 조상과 무관하게 정확히 "내 콜라이더"만 걸러냅니다.
            if (col.transform.IsChildOf(transform)) continue; // 자기 자신 제외

            Vector3 away = transform.position - col.transform.position;
            away.y = 0f;
            float dist = away.magnitude;

            if (dist < 0.0001f)
            {
                // 정확히 같은 위치에 겹친 경우 - 0으로 나누기를 피하려고 임의의 수평 방향을 씁니다.
                away = new Vector3(Random.value - 0.5f, 0f, Random.value - 0.5f);
                dist = away.magnitude;
                if (dist < 0.0001f) { away = Vector3.forward; dist = 1f; }
            }

            float overlapAmount = Mathf.Max(0f, separationRadius - dist);
            push += (away / dist) * overlapAmount;
        }

        if (debugLogOverlaps && count > 0 && push.sqrMagnitude < 0.0001f)
        {
            Debug.Log($"EnemySeparation ({gameObject.name}): 콜라이더는 {count}개 감지됐지만 전부 " +
                      "자기 자신(내 자식 계층)으로 걸러졌습니다 - Enemy Layer Mask에 자기 자신이 아닌 " +
                      "'다른' 적의 콜라이더가 실제로 포함되어 있는지 확인하세요.");
        }

        return push;
    }
}