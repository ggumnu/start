using UnityEngine;

// 음식 13번 - "획득 시 최초 한정(레벨당)으로 공격 속도 +0.3. 사용하면 현재 공격 속도 0.1당 공격력 0.5를 영구히 얻는다."
//
// [번호 정리 - 형빈 확정]
// 예전 13번(액티브 - 직전에 소모한 음식의 효과를 재현, ReplayLastFoodEffect)은 93번으로 옮겨 Item93effect.cs에
// 그대로 있습니다. 이 파일은 이제 새 13번(음식) 효과만 담습니다.
//
// [기획]
// - 즉시형 음식입니다 - E를 누르면 그 즉시 효과가 일어나고 음식은 바로 소멸합니다(대기 상태 없음).
// - 획득(장착)하면 공격 속도를 attackSpeedGainOnAcquire(기본 0.3)만큼 올립니다. 단 같은 레벨 안에서는 딱 한
//   번만입니다(교체로 버렸다가 같은 레벨에서 다시 주워도 다시 오르지 않고, 다음 레벨에서 다시 얻으면 다시 오름 -
//   34번과 같은 GameFlowManager.TryConsumeOneTimeGrant 패턴). 올라간 공격 속도는 음식을 먹거나 버려도 남습니다.
// - 사용(E)하면 "그 순간의" 공격 속도(다른 아이템 효과까지 반영된 최종 값)를 attackSpeedStep(기본 0.1) 단위로
//   센 뒤, 한 단위당 attackPerStep(기본 0.5)만큼 공격력을 영구히 올립니다. 0.1 미만 끝자리는 버립니다(형빈 확정).
//   예) 기본 공격 속도 2 + 획득 0.3 = 2.3 -> 23단위 -> 공격력 +11.5 / 공격 속도 2.37 -> 23단위(0.07 버림) -> +11.5
// - 34번(MaxHealthAttackFoodEffect)과 같은 구조이고, 비례 기준만 최대 체력 대신 공격 속도입니다. 93번 액티브로 이
//   음식을 재현하면 그때의 공격 속도 기준으로 공격력이 한 번 더 붙습니다.
//
// [끝자리 버림 오차 보정]
// 컴퓨터는 2.3을 2.2999999처럼 아주 조금 작게 저장할 수 있어서, 그대로 0.1로 나눠 버리면 23이 아니라 22로 세어
// 공격력이 0.5 덜 오르는 버그가 생깁니다. 그래서 나눈 값에 아주 작은 값(StepEpsilon)을 더한 뒤 버립니다. 이 값은
// 실제 0.1 단위 차이(1)보다 훨씬 작아서 2.29 같은 진짜 값은 그대로 22로 셉니다.
//
// [아이템 에셋 설정]
// ItemDefinition: Item Id = 13, Category = Food, Food Effect = AttackSpeedAttackFoodEffect.
[System.Serializable]
public class AttackSpeedAttackFoodEffect : IFoodEffect
{
    [Tooltip("획득 시 최초 한 번(레벨당)만 오르는 공격 속도입니다.")]
    public float attackSpeedGainOnAcquire = 0.3f;

    [Tooltip("사용 시 공격 속도를 이 단위로 셉니다(기본 0.1). 이 단위보다 작은 끝자리는 버립니다.")]
    [Min(0.0001f)] public float attackSpeedStep = 0.1f;

    [Tooltip("사용 시, 공격 속도 한 단위(위 Attack Speed Step)당 올릴 공격력입니다.")]
    public float attackPerStep = 0.5f;

    // 끝자리 버림 전에 더하는 아주 작은 값입니다(위 [끝자리 버림 오차 보정] 참고).
    private const float StepEpsilon = 0.0001f;

    public bool IsImmediate => true;

    public void OnAcquired(FoodEffectContext context, string sourceItemId)
    {
        if (context?.Stats == null || Mathf.Approximately(attackSpeedGainOnAcquire, 0f)) return;

        // 같은 레벨 안에서 이미 한 번 받았으면 다시 올리지 않습니다. 아이템 테스트 씬처럼 GameFlowManager가 없는
        // 곳에서는 막을 기준이 없으므로 경고만 남기고 올립니다(10번/26번/37번과 같은 처리).
        if (context.GameFlow != null)
        {
            if (!context.GameFlow.TryConsumeOneTimeGrant($"{sourceItemId}_attackspeed")) return;
        }
        else
        {
            Debug.LogWarning("AttackSpeedAttackFoodEffect: GameFlowManager를 찾지 못해 레벨당 1회 제한 없이 공격 속도를 올립니다.");
        }

        context.Stats.AddModifier(StatType.AttackSpeed, ModifierType.Additive, attackSpeedGainOnAcquire, sourceItemId);
    }

    // 이 음식은 항상 사용할 수 있습니다.
    public bool CanUse(FoodEffectContext context) => true;

    public void ActivateImmediate(FoodEffectContext context, string sourceItemId)
    {
        if (context?.Stats == null || attackSpeedStep <= 0f) return;

        int steps = Mathf.FloorToInt(context.Stats.AttackSpeed / attackSpeedStep + StepEpsilon);
        if (steps <= 0) return;

        float bonus = steps * attackPerStep;
        context.Stats.AddModifier(StatType.Damage, ModifierType.Additive, bonus, sourceItemId);
    }

    // 즉시형이라 호출되지 않습니다.
    public void BeginPending(FoodEffectContext context, string sourceItemId) { }

    // 즉시형이라 호출되지 않습니다.
    public void CancelPending(FoodEffectContext context, string sourceItemId) { }
}
