using UnityEngine;

// 적 종류 하나를 정의하는 데이터입니다. RoomVariant/WeightedPrefab과 동일한 패턴 -
// RealEnemySpawner가 이 정의들의 리스트를 가중치 기반으로 랜덤 선택해 스폰합니다.
//
// [prefab에 무엇이 들어가야 하는가]
// 지금은 3D 모델/Capsule Collider가 이미 형빈이 직접 만든 프리팹에 붙어있고, 여기에는
// "공격 방식처럼 적 종류마다 고정으로 달라지는 컴포넌트"만 있으면 됩니다.
// EnemyStats/EnemyHealth/NavMeshAgent/AI 컨트롤러처럼 모든 적이 공통으로 쓰는 컴포넌트는
// 스폰 시점에 RealEnemySpawner가 코드로 확인 후 없으면 붙여줍니다.
//
// [일반 적 vs 정예 적처럼 같은 공격 방식/프리팹을 재사용하되 수치만 다르게 하고 싶을 때]
// 같은 prefab을 참조하는 EnemyDefinition을 하나 더 만들고 maxHealth/attackPower 등의
// 값만 올려서 등록하면 됩니다.
//
// [v1.18 변경 - 공격 방식별 스탯을 attackConfig로 분리]
// 예전에는 원거리 전용 스탯(rangedDamage 등)이 이 클래스에 평평한 필드로 나열되어 있어서,
// 근접 적을 만들 때도 원거리 필드가 인스펙터에 그대로 보이는 문제가 있었습니다.
// [SerializeReference] IEnemyAttackConfig attackConfig 필드 하나로 바꿔서, 근접(Dash)/
// 원거리(Shoot) 중 선택한 타입의 필드만 인스펙터에 나타나도록 했습니다. 나중에 새 공격
// 방식(특수 공격, 하이브리드 등)이 추가돼도 IEnemyAttackConfig를 구현하는 클래스만 새로
// 만들면 되고, 이 클래스와 RealEnemySpawner는 손댈 필요가 없습니다(IEnemyAttackConfig.cs,
// DashAttackConfig.cs, ShootAttackConfig.cs 참고).
[CreateAssetMenu(fileName = "EnemyDefinition", menuName = "MobArchive/Enemy Definition")]
public class EnemyDefinition : ScriptableObject
{
    [Header("식별 및 가중치")]
    [Tooltip("이 적을 구분하는 이름입니다 (디버깅용).")]
    public string variantId;
    [Tooltip("같은 풀 안에서 뽑힐 가중치. 값이 클수록 더 자주 등장합니다.")]
    public float weight = 1f;

    [Header("프리팹")]
    public GameObject prefab;

    [Header("스탯 (스폰 시점에 이 프리팹의 EnemyStats에 그대로 주입됩니다 - 모든 공격 방식 공통)")]
    public float maxHealth = 10f;
    public float moveSpeed = 3.5f;
    [Tooltip("NavMeshAgent.acceleration에 적용됩니다. 돌진처럼 의도된 경우가 아니면 모든 " +
             "이동이 moveSpeed에 맞춰 일정해야 하므로, 가속/감속 구간이 사실상 없도록 기본값을 " +
             "크게 잡아뒀습니다. 의도적으로 가속감을 주고 싶을 때만 낮추세요.")]
    public float moveAcceleration = 999f;
    public float detectRange = 8f;
    public float attackPower = 1f;

    [Header("공격 방식 (하나만 선택 - 선택한 타입의 필드만 아래에 표시됩니다)")]
    [Tooltip("근접(Dash)/원거리(Shoot) 중 이 적의 공격 방식을 선택하세요. 필드를 클릭하면 " +
             "타입을 고를 수 있습니다. 여기서 고른 타입이 실제로 요구하는 컴포넌트(예: Dash는 " +
             "EnemyMeleeAttack)가 위 prefab에 없으면 저장 시 콘솔에 에러가 뜹니다.")]
    [SerializeReference]
    public IEnemyAttackConfig attackConfig;

    // 애셋을 에디터에서 수정/저장할 때마다 "선택한 attackConfig 타입이 실제로 요구하는
    // 컴포넌트가 prefab에 붙어있는지"를 확인합니다. 어긋나면(예: Shoot을 골랐는데 prefab에
    // EnemyRangedAttack이 없음) 콘솔에 명확한 에러를 띄워, 플레이해보기 전에 실수를 잡을 수
    // 있게 합니다.
    private void OnValidate()
    {
        if (prefab == null || attackConfig == null) return;

        var expected = attackConfig.ExpectedComponentType;
        if (expected == null) return;

        if (prefab.GetComponent(expected) == null)
        {
            Debug.LogError($"EnemyDefinition '{name}': attackConfig가 {attackConfig.GetType().Name}인데, " +
                            $"연결된 prefab '{prefab.name}'에는 필요한 컴포넌트({expected.Name})가 없습니다. " +
                            "prefab을 다시 확인하거나 attackConfig 타입을 다시 선택해주세요.");
        }
    }
}