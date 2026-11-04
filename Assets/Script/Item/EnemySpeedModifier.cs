using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// 적의 "이동 속도 보정"을 한곳에서 모아 처리하는 범용 부품입니다. 발자국 위에서 느려지는
// 아이템 2번뿐 아니라, 나중에 만들 빙결/둔화/가속 같은 효과도 전부 이 부품에 "보정"만
// 등록하면 됩니다 - 서로 값을 덮어쓰는 충돌이 없습니다.
//
// [계산 방식]
// 최종 이동 속도 = EnemyStats.moveSpeed(원래 속도) x 등록된 모든 보정 배율의 곱.
// 보정은 출처 이름(sourceId)별로 하나씩만 들어갑니다 - 같은 출처가 여러 번 등록해도
// 마지막 값 하나만 유지되므로 중첩되지 않습니다(예: 발자국이 여러 개 겹쳐도 배율은 한 번만).
//
// [기존 스크립트를 수정하지 않아도 되는 이유]
// EnemyAIController/EnemyRangedAIController/BossAIController/RealEnemySpawner는
// NavMeshAgent.speed를 스폰/Start 때 한 번만 EnemyStats.moveSpeed로 넣고 이후에는 건드리지
// 않습니다. 이 부품이 그 뒤에 매 프레임(LateUpdate) 값을 덮어쓰므로 충돌이 없습니다.
// 보정이 하나도 없는 동안에는 agent.speed를 전혀 건드리지 않고, 보정이 모두 사라지는
// 순간에만 원래 속도(EnemyStats.moveSpeed)로 한 번 되돌립니다 - 그래서 나중에 다른
// 스크립트가 agent.speed를 직접 바꾸더라도 보정이 없을 때는 간섭하지 않습니다.
//
// [적용되지 않는 것]
// 근접 적의 돌진(대시)은 agent.speed를 쓰지 않고 NavMeshAgent.Move()로 직접 밀어서
// 이동하므로 이 부품의 영향을 받지 않습니다(형빈 확정 - 걷기만 감속).
//
// [사용 방법 - 코드에서]
// var mod = EnemySpeedModifier.GetOrAdd(enemyGameObject); // 없으면 자동으로 붙임
// mod.SetMultiplier("내_출처_이름", 0.5f);                // 50% 감속 등록
// mod.RemoveMultiplier("내_출처_이름");                   // 해제
// 프리팹에 미리 붙여둘 필요는 없습니다.
[DisallowMultipleComponent]
public class EnemySpeedModifier : MonoBehaviour
{
    private EnemyStats stats;
    private NavMeshAgent agent;

    // 출처 이름 -> 배율
    private readonly Dictionary<string, float> multipliers = new Dictionary<string, float>();

    // 이 부품이 agent.speed를 원래 값이 아닌 값으로 바꿔둔 상태인지 여부입니다.
    private bool speedOverridden = false;

    // 등록된 모든 보정 배율의 곱입니다(보정이 없으면 1).
    public float CurrentMultiplier
    {
        get
        {
            float result = 1f;
            foreach (var pair in multipliers) result *= pair.Value;
            return result;
        }
    }

    // 이 적에게 붙어있는 부품을 돌려주고, 없으면 새로 붙여서 돌려줍니다.
    public static EnemySpeedModifier GetOrAdd(GameObject enemy)
    {
        if (enemy == null) return null;

        var existing = enemy.GetComponent<EnemySpeedModifier>();
        if (existing != null) return existing;

        return enemy.AddComponent<EnemySpeedModifier>();
    }

    void Awake()
    {
        stats = GetComponent<EnemyStats>();
        agent = GetComponent<NavMeshAgent>();
    }

    // 보정을 등록합니다. 같은 sourceId로 다시 부르면 값만 갱신됩니다. 0 미만은 0으로 취급합니다.
    public void SetMultiplier(string sourceId, float multiplier)
    {
        if (string.IsNullOrEmpty(sourceId)) return;

        multipliers[sourceId] = Mathf.Max(0f, multiplier);
        ApplySpeed();
    }

    // 보정을 해제합니다. 등록된 적이 없는 sourceId여도 안전합니다.
    public void RemoveMultiplier(string sourceId)
    {
        if (string.IsNullOrEmpty(sourceId)) return;

        if (multipliers.Remove(sourceId))
        {
            ApplySpeed();
        }
    }

    void LateUpdate()
    {
        // 다른 스크립트가 Start()에서 agent.speed를 원래 값으로 다시 써버렸을 수 있으므로,
        // 보정이 걸려있는 동안에는 매 프레임 다시 덮어씁니다.
        ApplySpeed();
    }

    // 등록/해제 직후에도 즉시 호출됩니다 - 적이 꺼져 있는 방(비활성)에 있어서 LateUpdate가
    // 돌지 않는 상태에서 보정이 해제돼도 속도가 느린 채로 남지 않도록 하기 위함입니다.
    private void ApplySpeed()
    {
        if (stats == null) stats = GetComponent<EnemyStats>();
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (stats == null || agent == null) return;

        float multiplier = CurrentMultiplier;

        if (multiplier != 1f)
        {
            agent.speed = stats.moveSpeed * multiplier;
            speedOverridden = true;
        }
        else if (speedOverridden)
        {
            agent.speed = stats.moveSpeed;
            speedOverridden = false;
        }
    }
}
