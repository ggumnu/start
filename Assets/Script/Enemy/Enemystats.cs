using UnityEngine;

// 적 개체 하나의 공용 능력치를 담는 컴포넌트입니다. PlayerStats와 동일한 역할 -
// 이동/AI/공격 스크립트는 절대 자기만의 수치를 갖지 않고 항상 이 컴포넌트를 통해서만
// 값을 읽습니다.
//
// 인스펙터 기본값은 안전장치용 폴백이고, 실제 값은 스폰 시점에 RealEnemySpawner가
// EnemyDefinition(공통 스탯)과 EnemyDefinition.attackConfig(공격 방식별 스탯)의 값으로
// 덮어씁니다.
//
// [v1.18 변경 - 원거리 전용 필드를 채워주는 주체가 바뀜]
// rangedDamage 등 원거리 전용 필드 자체는 그대로 여기 남아있습니다 - EnemyRangedAttack과
// EnemyRangedAIController가 둘 다 읽는 공용 값이라 계속 이 컴포넌트가 버스 역할을 합니다.
// 다만 예전엔 ApplyFrom(EnemyDefinition)이 이 필드들까지 전부 채웠는데, 이제 EnemyDefinition
// 에서 원거리 전용 필드가 사라지고 attackConfig(ShootAttackConfig)로 옮겨갔기 때문에,
// ApplyFrom()은 "모든 적 공통" 필드만 채우고, 원거리 전용 필드는 RealEnemySpawner가
// definition.attackConfig?.ApplyTo(this, enemyGO)를 별도로 호출해서 채웁니다
// (RealEnemySpawner.SpawnOne(), ShootAttackConfig.cs 참고).
//
// [변경 - 이동 판단을 "사격 개시 거리" 하나가 아니라 세 단계로 세분화 (형빈 요청)]
// 처음엔 rangedAttackRange(탄환이 날아가는 거리)와 engageRange(사격을 시작하는 거리)만
// 분리했었는데, 그러면 engageRange 안에 들어오는 즉시 그 자리에 멈춰버려서 "좀 더
// 파고들어와서 쏘는" 느낌을 낼 수 없었습니다. 그래서 "사격 여부 판단"과 "이동 여부
// 판단"을 완전히 분리했습니다:
// - engageRange: 오직 "사격을 할지"만 결정합니다. 이동과는 무관합니다(각 AI 컨트롤러의
//   UpdateFiring() 참고 - 접근 중이든 제자리든 후퇴 중이든 이 거리 안이면 쿨다운만 차면
//   그냥 쏩니다).
// - chaseRange: "계속 접근할지, 멈출지"를 결정합니다. 거리가 이 값보다 크면(그리고
//   당연히 시야가 확보돼 있다면) 계속 접근하고, 이 값 이하로 들어오면(retreatRange보다는
//   먼 경우) 제자리에 멈춥니다.
// - retreatRange: 거리가 이 값보다 가까워지면 후퇴를 시도합니다.
// 예) engageRange=5, chaseRange=4, retreatRange=3이면: 거리 5 이내부터 사격을 시작하고,
// 거리가 4가 될 때까지는(사격을 계속하면서도) 접근을 이어가다가, 4 이하로 들어오면
// 제자리에 멈춰서 사격만 하고, 3 이하로 더 가까워지면 후퇴합니다. chaseRange를
// engageRange와 같게 설정하면 예전과 동일한 동작(사격 개시 거리에 도달하는 즉시 멈춤)이
// 됩니다.
public class EnemyStats : MonoBehaviour
{
    [Header("체력")]
    public float maxHealth = 10f;

    [Header("이동 속도 (NavMeshAgent.speed에 적용)")]
    public float moveSpeed = 3.5f;
    [Tooltip("NavMeshAgent.acceleration에 적용됩니다. 이 게임은 돌진(대시)처럼 명시적으로 " +
             "설계된 경우가 아니면 모든 이동이 moveSpeed에 맞춰 '일정한 속도'여야 하므로, " +
             "가속/감속 구간이 사실상 없게(즉시 목표 속도로 전환) 기본값을 매우 크게 " +
             "잡아뒀습니다. NavMeshAgent 구조상 가속도를 0이나 무한대로 만들 수는 없지만, " +
             "충분히 큰 값이면 체감상 동일한 효과를 냅니다. 의도적으로 가속감을 주고 싶은 " +
             "특수한 적을 만들 때만 이 값을 낮추세요.")]
    public float moveAcceleration = 999f;

    [Header("플레이어 감지 거리 (Idle -> Chase 전환 거리)")]
    public float detectRange = 8f;

    [Header("공격력 (근접 접촉 데미지 등 - 모든 공격 방식이 공용으로 참조)")]
    public float attackPower = 1f;

    [Header("원거리 공격 스탯 (원거리 공격 방식을 가진 적만 사용 - ShootAttackConfig가 채움)")]
    [Tooltip("EnemyProjectile 한 발당 데미지입니다.")]
    public float rangedDamage = 2f;
    [Tooltip("EnemyProjectile 오브젝트의 스케일 배율입니다.")]
    public float rangedProjectileSize = 1f;
    [Tooltip("EnemyProjectile의 이동 속도입니다.")]
    public float rangedProjectileSpeed = 14f;
    [Tooltip("탄환이 소멸하기까지 날아가는 거리입니다. AI의 이동/사격 판단과는 무관하고 " +
             "순수하게 탄환 자체의 사거리입니다 - 이동/사격 판단은 아래 engageRange/" +
             "chaseRange/retreatRange가 따로 담당합니다(이 파일 상단 주석 참고).")]
    public float rangedAttackRange = 8f;
    [Tooltip("AI가 '이 거리 안에 들어오면 사격을 시작한다'고 판단하는 거리입니다. 오직 " +
             "사격 여부만 결정하고 이동에는 영향을 주지 않습니다(이동 중이든 정지 중이든 " +
             "이 거리 안이면 쏩니다). rangedAttackRange(탄환이 실제로 날아가는 거리)보다 " +
             "작거나 같게 설정하세요 - 더 크게 설정하면 탄환이 도달하지 못하는 거리에서 " +
             "쏘려고 시도하게 됩니다.")]
    public float engageRange = 6f;
    [Tooltip("AI가 '이 거리보다 가까워지면 더 이상 접근하지 않고 제자리에 멈춘다'고 " +
             "판단하는 거리입니다(추격을 멈추는 거리 - 사격 여부는 위 engageRange가 " +
             "따로 담당). engageRange보다 작거나 같게 설정하세요 - engageRange 밖에서는 " +
             "애초에 사격을 하지 않으므로, chaseRange가 engageRange보다 크면 '사격도 안 " +
             "하면서 계속 다가가기만 하는' 구간이 생깁니다. retreatRange보다는 충분히 커야 " +
             "'접근 -> 제자리+사격 -> 후퇴' 세 구간이 자연스럽게 나뉩니다.")]
    public float chaseRange = 5f;
    [Tooltip("플레이어와의 거리가 이 값보다 가까워지면 후퇴를 시도합니다. chaseRange보다 " +
             "충분히 작게 설정해야 '접근 -> 제자리+사격 -> 후퇴' 간격이 자연스럽습니다.")]
    public float retreatRange = 4f;
    [Tooltip("초당 발사 가능 횟수입니다 (값이 높을수록 발사 간격이 짧아집니다).")]
    public float rangedAttackSpeed = 1f;

    // EnemyDefinition의 "모든 적 공통" 스탯만 채웁니다. 공격 방식별 스탯(원거리 등)은
    // RealEnemySpawner가 definition.attackConfig?.ApplyTo(this, enemyGO)로 별도로 채웁니다.
    public void ApplyFrom(EnemyDefinition def)
    {
        if (def == null) return;
        maxHealth = def.maxHealth;
        moveSpeed = def.moveSpeed;
        moveAcceleration = def.moveAcceleration;
        detectRange = def.detectRange;
        attackPower = def.attackPower;
    }
}