// 장신구 21번 - "획득 시 최초 한정으로 체력 2 회복 + 소유 중엔 받는 모든 피해량이 0.5로 고정".
//
// [기획]
// - 획득(장착)하면 그 즉시 현재 체력을 2 회복합니다 - 단, 같은 레벨 안에서는 딱 한 번만
//   (교체해서 버렸다가 같은 레벨에서 다시 주워도 두 번째부터는 회복되지 않습니다. 다음
//   레벨로 넘어가면 다시 받을 수 있습니다 - GameFlowManager.TryConsumeOneTimeGrant 참고).
// - 소유하고 있는 동안은 받는 모든 피해량이 실제 공격력과 무관하게 항상 fixedDamageTaken
//   (기본 0.5)으로 고정됩니다.
// - 다른 장신구로 교체되어 빠지면(Remove) 피해량 고정 효과만 해제됩니다 - 이미 받은 체력
//   회복은 되돌리지 않습니다(1회성 지급은 "받았다"는 사실 자체가 남아야 하므로).
[System.Serializable]
public class FixedDamageTrinketEffect : ITrinketEffect
{
    [UnityEngine.Tooltip("장착 시 최초 한 번(레벨당)만 회복되는 체력입니다.")]
    public float healOnFirstEquip = 2f;

    [UnityEngine.Tooltip("소유 중일 때 받는 모든 피해량을 이 값으로 고정합니다.")]
    public float fixedDamageTaken = 0.5f;

    public void Apply(TrinketEffectContext context, string sourceItemId)
    {
        if (context?.Health == null) return;

        // 같은 레벨 안에서 이미 한 번 받았으면 다시 지급하지 않습니다 - 교체를 반복해도
        // 두 번째부터는 아무 일도 일어나지 않습니다.
        bool grantedNow = context.GameFlow != null &&
                           context.GameFlow.TryConsumeOneTimeGrant($"{sourceItemId}_heal");
        if (grantedNow)
        {
            context.Health.Heal(healOnFirstEquip);
        }

        context.Health.SetFixedIncomingDamage(fixedDamageTaken);
    }

    public void Remove(TrinketEffectContext context, string sourceItemId)
    {
        context?.Health?.SetFixedIncomingDamage(null);
    }
}
