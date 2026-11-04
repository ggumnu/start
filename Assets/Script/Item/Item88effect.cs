using UnityEngine;

// 일회용 88번 - "사용하면 즉시 소모되고, 현재 깎인 체력을 모두 회복한다."
//
// [기획 - 형빈 확정]
// - 사용(Q)하면 즉시 소모되고 체력을 최대 체력까지 채웁니다(깎인 체력 전부 회복).
// - 체력이 이미 가득 차 있으면 회복할 것이 없으므로 사용할 수 없습니다 - 소모되지 않습니다(17번과 같은 방식, 창은 띄우지 않음).
//   0.5만 깎여 있어도 사용할 수 있습니다.
// - 최대 체력은 바뀌지 않습니다.
// - 85번(마지막으로 소모한 일회용 재현)으로 재현해도 같은 효과입니다(85번은 이 아이템의 사용 조건을 따르므로, 체력이 가득이면 사용되지 않음).
//
// [구현]
// PlayerHealth.Heal(깎인 체력)을 부릅니다. Heal은 최대 체력을 넘지 않게 막고, 사망 중이면 아무 일도 하지 않습니다.
//
// [아이템 에셋 설정]
// ItemDefinition: Item Id = 88, Category = Disposable, Disposable Effect = FullHealDisposableEffect.
[System.Serializable]
public class FullHealDisposableEffect : IDisposableItemEffect
{
    public bool IsImmediate => true;

    // 깎인 체력이 조금이라도 있어야 사용할 수 있습니다(가득 차 있으면 소모되지 않음).
    public bool CanUse(DisposableEffectContext context)
    {
        if (context?.Health == null || context.Health.IsDead) return false;
        return GetMissingHealth(context) > 0.0001f;
    }

    public void ActivateImmediate(DisposableEffectContext context, string sourceItemId)
    {
        if (context?.Health == null) return;

        float missing = GetMissingHealth(context);
        if (missing <= 0.0001f) return;

        context.Health.Heal(missing);
    }

    // 즉시형이라 호출되지 않습니다.
    public void BeginPending(DisposableEffectContext context, string sourceItemId) { }

    // 즉시형이라 호출되지 않습니다.
    public void CancelPending(DisposableEffectContext context, string sourceItemId) { }

    // 지금 깎여 있는 체력(최대 체력 - 현재 체력)입니다.
    private static float GetMissingHealth(DisposableEffectContext context)
    {
        if (context == null || context.Stats == null || context.Health == null) return 0f;
        return Mathf.Max(0f, context.Stats.MaxHealth - context.Health.CurrentHealth);
    }
}
