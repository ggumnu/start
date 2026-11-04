// 음식 34번 - "획득 시 최초 한정으로 최대 체력 1 증가. 사용하면 현재 최대 체력 1당 공격력을
// 0.5 증가시킨다".
//
// [기획]
// - 즉시형 음식입니다 - E를 누르면 그 즉시 효과가 일어나고 음식은 바로 소멸합니다(대기 상태
//   없음).
// - 획득(장착)하면 그 즉시 최대 체력을 maxHealthGainOnAcquire(기본 1)만큼 증가시킵니다 - 단,
//   같은 레벨 안에서는 딱 한 번만(교체해서 버렸다가 같은 레벨에서 다시 주워도 두 번째부터는
//   증가하지 않습니다. 다음 레벨로 넘어가면 다시 받을 수 있습니다 - ITrinketEffect.cs /
//   FixedDamageTrinketEffect와 동일한 GameFlowManager.TryConsumeOneTimeGrant 패턴).
// - 사용(E)하면 "그 순간의" 최대 체력에 attackPerMaxHealth(기본 0.5)를 곱한 만큼 공격력을
//   영구히 증가시킵니다(예: 최대 체력 5 -> 공격력 +2.5, 최대 체력 10 -> 공격력 +5). 현재
//   체력이 아니라 최대 체력 기준이며, 상한 없이 여러 번 사용할 때마다 그때그때의 최대 체력
//   기준으로 다시 계산되어 누적됩니다(같은 음식을 여러 레벨에 걸쳐 다시 얻어 사용해도 매번
//   별도의 sourceItemId 인스턴스 사용 시점 기준으로 PlayerStats.AddModifier가 그대로 누적).
[System.Serializable]
public class MaxHealthAttackFoodEffect : IFoodEffect
{
    [UnityEngine.Tooltip("획득 시 최초 한 번(레벨당)만 증가하는 최대 체력입니다.")]
    public float maxHealthGainOnAcquire = 1f;

    [UnityEngine.Tooltip("사용 시, 그 순간의 최대 체력 1당 증가시킬 공격력입니다.")]
    public float attackPerMaxHealth = 0.5f;

    public bool IsImmediate => true;

    public void OnAcquired(FoodEffectContext context, string sourceItemId)
    {
        if (context?.Stats == null) return;

        // 같은 레벨 안에서 이미 한 번 받았으면 다시 지급하지 않습니다 - 교체를 반복해도
        // 두 번째부터는 아무 일도 일어나지 않습니다.
        bool grantedNow = context.GameFlow != null &&
                           context.GameFlow.TryConsumeOneTimeGrant($"{sourceItemId}_maxhealth");
        if (!grantedNow) return;

        context.Stats.AddModifier(StatType.MaxHealth, ModifierType.Additive, maxHealthGainOnAcquire, sourceItemId);
    }

    // [9번 음식 연동] 이 음식은 항상 사용할 수 있습니다.
    public bool CanUse(FoodEffectContext context) => true;

    public void ActivateImmediate(FoodEffectContext context, string sourceItemId)
    {
        if (context?.Stats == null) return;

        float bonus = context.Stats.MaxHealth * attackPerMaxHealth;
        context.Stats.AddModifier(StatType.Damage, ModifierType.Additive, bonus, sourceItemId);
    }

    // 즉시형이라 호출되지 않습니다.
    public void BeginPending(FoodEffectContext context, string sourceItemId) { }

    // 즉시형이라 호출되지 않습니다.
    public void CancelPending(FoodEffectContext context, string sourceItemId) { }
}
