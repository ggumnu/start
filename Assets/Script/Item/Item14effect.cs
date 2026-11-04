using UnityEngine;

// 음식 14번 - "획득 시 최초 한정(레벨당)으로 이동 속도 +0.3. 사용하면 현재 이동 속도 0.1당 공격력 0.5를 영구히 얻는다."
//
// [기획]
// - 즉시형 음식입니다 - E를 누르면 그 즉시 효과가 일어나고 음식은 바로 소멸합니다(대기 상태 없음).
// - 획득(장착)하면 이동 속도를 moveSpeedGainOnAcquire(기본 0.3)만큼 올립니다. 단 같은 레벨 안에서는 딱 한
//   번만입니다(교체로 버렸다가 같은 레벨에서 다시 주워도 다시 오르지 않고, 다음 레벨에서 다시 얻으면 다시 오름 -
//   13번/34번과 같은 GameFlowManager.TryConsumeOneTimeGrant 패턴). 올라간 이동 속도는 음식을 먹거나 버려도 남습니다.
// - 사용(E)하면 "그 순간의" 이동 속도(다른 아이템 효과까지 반영된 최종 값)를 moveSpeedStep(기본 0.1) 단위로
//   센 뒤, 한 단위당 attackPerStep(기본 0.5)만큼 공격력을 영구히 올립니다. 0.1 미만 끝자리는 버립니다(13번과 동일).
//   예) 기본 이동 속도 2 + 획득 0.3 = 2.3 -> 23단위 -> 공격력 +11.5 / 이동 속도 2.37 -> 23단위(0.07 버림) -> +11.5
// - 13번(AttackSpeedAttackFoodEffect)과 완전히 같은 구조이고, 비례 기준만 공격 속도 대신 이동 속도입니다. 93번
//   액티브로 이 음식을 재현하면 그때의 이동 속도 기준으로 공격력이 한 번 더 붙습니다.
//
// [끝자리 버림 오차 보정]
// 컴퓨터는 2.3을 2.2999999처럼 아주 조금 작게 저장할 수 있어서, 그대로 0.1로 나눠 버리면 23이 아니라 22로 세어
// 공격력이 0.5 덜 오르는 버그가 생깁니다. 그래서 나눈 값에 아주 작은 값(StepEpsilon)을 더한 뒤 버립니다. 이 값은
// 실제 0.1 단위 차이(1)보다 훨씬 작아서 2.29 같은 진짜 값은 그대로 22로 셉니다.
//
// [아이템 에셋 설정]
// ItemDefinition: Item Id = 14, Category = Food, Food Effect = MoveSpeedAttackFoodEffect.
[System.Serializable]
public class MoveSpeedAttackFoodEffect : IFoodEffect
{
    [Tooltip("획득 시 최초 한 번(레벨당)만 오르는 이동 속도입니다.")]
    public float moveSpeedGainOnAcquire = 0.3f;

    [Tooltip("사용 시 이동 속도를 이 단위로 셉니다(기본 0.1). 이 단위보다 작은 끝자리는 버립니다.")]
    [Min(0.0001f)] public float moveSpeedStep = 0.1f;

    [Tooltip("사용 시, 이동 속도 한 단위(위 Move Speed Step)당 올릴 공격력입니다.")]
    public float attackPerStep = 0.5f;

    // 끝자리 버림 전에 더하는 아주 작은 값입니다(위 [끝자리 버림 오차 보정] 참고).
    private const float StepEpsilon = 0.0001f;

    public bool IsImmediate => true;

    public void OnAcquired(FoodEffectContext context, string sourceItemId)
    {
        if (context?.Stats == null || Mathf.Approximately(moveSpeedGainOnAcquire, 0f)) return;

        // 같은 레벨 안에서 이미 한 번 받았으면 다시 올리지 않습니다. 아이템 테스트 씬처럼 GameFlowManager가 없는
        // 곳에서는 막을 기준이 없으므로 경고만 남기고 올립니다(13번과 같은 처리).
        if (context.GameFlow != null)
        {
            if (!context.GameFlow.TryConsumeOneTimeGrant($"{sourceItemId}_movespeed")) return;
        }
        else
        {
            Debug.LogWarning("MoveSpeedAttackFoodEffect: GameFlowManager를 찾지 못해 레벨당 1회 제한 없이 이동 속도를 올립니다.");
        }

        context.Stats.AddModifier(StatType.MoveSpeed, ModifierType.Additive, moveSpeedGainOnAcquire, sourceItemId);
    }

    // 이 음식은 항상 사용할 수 있습니다.
    public bool CanUse(FoodEffectContext context) => true;

    public void ActivateImmediate(FoodEffectContext context, string sourceItemId)
    {
        if (context?.Stats == null || moveSpeedStep <= 0f) return;

        int steps = Mathf.FloorToInt(context.Stats.MoveSpeed / moveSpeedStep + StepEpsilon);
        if (steps <= 0) return;

        float bonus = steps * attackPerStep;
        context.Stats.AddModifier(StatType.Damage, ModifierType.Additive, bonus, sourceItemId);
    }

    // 즉시형이라 호출되지 않습니다.
    public void BeginPending(FoodEffectContext context, string sourceItemId) { }

    // 즉시형이라 호출되지 않습니다.
    public void CancelPending(FoodEffectContext context, string sourceItemId) { }
}
