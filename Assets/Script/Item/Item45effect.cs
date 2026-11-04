using System;
using UnityEngine;

// 장신구 45번 - "장착 시 최초 한정으로 코스트 3을 충전한다. 장착하고 있는 동안 모든 액티브 아이템의 코스트 소모량이
// 1 감소한다(최소 1)."
//
// [기획 - 형빈 확정]
// - 장착하는 순간 코스트를 costOnFirstEquip(기본 3)만큼 충전합니다. "최초 한정"은 다른 장신구(20/32/39번)와 같은
//   레벨당 1회입니다(GameFlowManager.TryConsumeOneTimeGrant) - 같은 레벨에서 뺐다 다시 끼면 안 주고, 다음 레벨에서
//   다시 끼면 또 줍니다. 충전은 최대 코스트(PlayerResources.maxCost)를 넘지 않습니다(넘치는 만큼은 버려짐).
// - 장착 중에는 모든 액티브 아이템의 소모량이 costReduction(기본 1)만큼 줄어듭니다(3 -> 2, 5 -> 4). 1 밑으로는
//   내려가지 않습니다(ActiveCostModifier 참고). 좌상단 소모 코스트 숫자(PlayerCostDisplay)도 줄어든 값으로 보입니다.
// - 다른 장신구와 교체되거나 이 아이템을 잃으면(Remove) 감소가 즉시 해제되어 원래 소모량/표시로 돌아옵니다.
[Serializable]
public class ActiveCostReductionTrinketEffect : ITrinketEffect
{
    [Tooltip("장착 시 충전하는 코스트입니다(레벨당 1회).")]
    public int costOnFirstEquip = 3;
    [Tooltip("장착 중 모든 액티브 아이템의 코스트 소모량 감소량입니다(결과는 최소 1).")]
    public int costReduction = 1;

    public void Apply(TrinketEffectContext context, string sourceItemId)
    {
        if (context == null) return;

        // 재장착 시 이전 등록이 남아있으면 먼저 정리합니다(다른 장신구들과 같은 안전장치).
        Remove(context, sourceItemId);

        bool grantedNow = context.GameFlow != null &&
                          context.GameFlow.TryConsumeOneTimeGrant($"{sourceItemId}_cost");
        if (grantedNow && costOnFirstEquip > 0 && context.Resources != null)
        {
            context.Resources.Add(BasicItemType.Cost, costOnFirstEquip);
        }

        ActiveCostModifier.SetReduction(RegistryKey(sourceItemId), context.PlayerObject, costReduction);
    }

    public void Remove(TrinketEffectContext context, string sourceItemId)
    {
        ActiveCostModifier.RemoveReduction(RegistryKey(sourceItemId));
    }

    private static string RegistryKey(string sourceItemId) => "active_cost_reduction_" + sourceItemId;
}
