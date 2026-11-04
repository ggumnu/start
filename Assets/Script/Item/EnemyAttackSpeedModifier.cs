using System.Collections.Generic;
using UnityEngine;

// 적의 "공격 속도 보정"을 한곳에서 모아 처리하는 범용 부품입니다. 이동 속도 보정(EnemySpeedModifier)과 같은 구조로, 91번 오한
// 뿐 아니라 나중에 만들 다른 공격 둔화/가속 효과도 전부 이 부품에 "보정"만 등록하면 됩니다.
//
// [무엇이 느려지나 - 형빈 확정]
// "공격과 공격 사이의 대기 시간"만 늘어납니다. 각 공격 스크립트의 대기 타이머가 줄어드는 속도에 Multiplier를 곱합니다.
// - 근접 돌진(EnemyMeleeAttack)/연속 돌진(EnemyChainDashAttack)/전방위 공격(EnemyRadialAttack): 공격이 끝난 뒤의 쿨다운
// - 원거리 사격(EnemyRangedAttack): 다음 사격까지의 대기(공격 주기)
// 배율 0.5면 대기가 두 배로 길어집니다. 돌진 충전(텔레그래프) 시간/돌진 속도/연사 한 번 안의 탄 간격은 그대로입니다.
// 접촉만으로 피해를 주는 적(EnemyContactOnlyAttack)은 대기 개념이 없어 영향이 없습니다.
//
// [계산 방식]
// 최종 배율 = 등록된 모든 보정 배율의 곱. 보정은 출처 이름(sourceId)별로 하나씩만 들어갑니다(같은 출처는 중첩되지 않음).
// 이 부품이 없는(=보정을 한 번도 받지 않은) 적은 항상 1이라 기존과 100% 같습니다.
//
// [사용 방법 - 코드에서]
// EnemyAttackSpeedModifier.GetOrAdd(enemyGameObject).SetMultiplier("내_출처_이름", 0.5f); // 공격 속도 50% 감소
// EnemyAttackSpeedModifier.GetOrAdd(enemyGameObject).RemoveMultiplier("내_출처_이름");
// 공격 스크립트 쪽: timer -= Time.deltaTime * EnemyAttackSpeedModifier.GetMultiplier(gameObject);
[DisallowMultipleComponent]
public class EnemyAttackSpeedModifier : MonoBehaviour
{
    private readonly Dictionary<string, float> multipliers = new Dictionary<string, float>();

    // 등록된 모든 보정 배율의 곱입니다(보정이 없으면 1). 0 밑으로는 내려가지 않습니다.
    public float CurrentMultiplier
    {
        get
        {
            float result = 1f;
            foreach (var pair in multipliers) result *= pair.Value;
            return Mathf.Max(0f, result);
        }
    }

    public static EnemyAttackSpeedModifier GetOrAdd(GameObject enemy)
    {
        if (enemy == null) return null;
        var existing = enemy.GetComponent<EnemyAttackSpeedModifier>();
        return existing != null ? existing : enemy.AddComponent<EnemyAttackSpeedModifier>();
    }

    // 공격 스크립트가 대기 타이머를 줄일 때 곱하는 값입니다. 부품이 없으면 1입니다.
    public static float GetMultiplier(GameObject enemy)
    {
        if (enemy == null) return 1f;
        var mod = enemy.GetComponent<EnemyAttackSpeedModifier>();
        return mod != null ? mod.CurrentMultiplier : 1f;
    }

    public void SetMultiplier(string sourceId, float multiplier)
    {
        if (string.IsNullOrEmpty(sourceId)) return;
        multipliers[sourceId] = Mathf.Max(0f, multiplier);
    }

    public void RemoveMultiplier(string sourceId)
    {
        if (string.IsNullOrEmpty(sourceId)) return;
        multipliers.Remove(sourceId);
    }
}
