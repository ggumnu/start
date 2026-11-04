using UnityEngine;

// 패시브 24번 - "획득 시 최대 코스트가 2 증가하고, 증가 직후 현재 코스트를 새 최대치로
// 가득 채운다. 나중에 최대 코스트를 늘리는 다른 아이템이 나와도, 그 아이템 역시 같은
// 방식으로 구현하면 이미 늘어나 있던 최대 코스트 위에 추가로 증가하더라도 항상 현재
// 코스트가 가득 찬 상태가 된다."
//
// [기획]
// - 획득 시 PlayerResources.maxCost를 maxCostIncrease(기본 2)만큼 늘립니다 (10 → 12).
// - 늘어난 직후, 현재 코스트를 새로운 최대치로 즉시 채웁니다.
// - 코스트 UI(PlayerCostDisplay.cs)는 이미 매 프레임 playerResources.maxCost 값을 보고
//   슬롯 개수를 맞추도록 구현되어 있어서, 이 아이템을 손대지 않아도 UI가 자동으로
//   12칸으로 늘어납니다.
//
// [구현 - 항상 가득 채우는 방식]
// PlayerResources.Add()는 Cost 타입에 한해 결과값을 [0, maxCost]로 자동 클램프합니다.
// 그래서 maxCost를 먼저 늘린 뒤 그 늘어난 maxCost 값만큼(또는 그 이상) Add를 호출하면,
// 그 시점의 현재 코스트가 얼마였든 상관없이 결과는 항상 "새로운 maxCost"로 가득 찬
// 상태가 됩니다. 예: 이미 다른 아이템으로 maxCost가 15까지 늘어나 있는 상태에서 이
// 아이템을 획득하면 maxCost가 17이 되고, Add(Cost, 17)을 호출하므로 현재 코스트도
// 정확히 17로 가득 찹니다 - 기획에서 요구한 스택 상황도 이 방식 그대로 만족합니다.
[System.Serializable]
public class MaxCostIncreaseEffect : IPassiveItemEffect, IRemovablePassiveEffect
{
    public int maxCostIncrease = 2;

    public void Apply(PassiveEffectContext context, string sourceItemId)
    {
        if (context == null || context.Resources == null)
        {
            Debug.LogWarning("MaxCostIncreaseEffect: PlayerResources를 찾을 수 없어 효과를 적용하지 못했습니다.");
            return;
        }

        var resources = context.Resources;
        resources.maxCost += maxCostIncrease;

        // Cost 타입은 Add() 내부에서 [0, maxCost]로 자동 클램프되므로, 새 maxCost만큼
        // 더하면 현재 보유량과 무관하게 정확히 새 최대치로 가득 채워집니다.
        resources.Add(BasicItemType.Cost, resources.maxCost);
    }

    // [패시브 잃기 - 48번 등, 형빈 확정] 코스트는 플레이어 스탯이 아니라 PlayerResources가 관리하는 값이라 예외적으로
    // 되돌립니다. 늘렸던 최대 코스트를 다시 줄이고, 현재 코스트가 새 최대치를 넘으면 새 최대치까지 깎습니다
    // (예: 12/12 -> 10/10, 7/12 -> 7/10). 다시 얻으면 Apply가 또 늘리고 가득 채우므로 두 번 겹치지 않습니다.
    public void Remove(PassiveEffectContext context, string sourceItemId)
    {
        if (context == null || context.Resources == null) return;

        var resources = context.Resources;
        resources.maxCost = Mathf.Max(1, resources.maxCost - maxCostIncrease);

        int over = resources.GetAmount(BasicItemType.Cost) - resources.maxCost;
        if (over > 0) resources.Add(BasicItemType.Cost, -over);
    }
}
