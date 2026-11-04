using UnityEngine;

// 패시브 55번 - "획득 시 수류탄/해킹툴/청휘석을 1개씩 얻는다(레벨당 최초 1회). 보유하고 있으면 장신구를 최대 두 개 장착할 수 있다."
//
// [기획 - 형빈 확정]
// 1) 지급: 획득할 때 수류탄/해킹툴/청휘석을 각각 grenadeAmount/hackingToolAmount/pyroxenesAmount(기본 1)개 줍니다. 세 가지를 한
//    묶음으로 "하나의 레벨" 기준 최초 1회입니다(10번/57번과 같은 규칙 - GameFlowManager.TryConsumeOneTimeGrant). 같은 레벨에서
//    잃었다가 다시 얻으면 칸 효과는 다시 생기지만 지급은 하지 않고, 다음 레벨에서 다시 얻으면 다시 줍니다. 48번으로 얻으면 제한
//    없이 줍니다.
// 2) 장신구 칸 +1: 보유하는 동안 장신구를 2개까지 장착하고 두 장신구의 효과를 모두 받습니다. 칸이 꽉 찬 상태에서 장신구를 얻으면
//    가장 먼저 얻은 장신구가 교체되고(교체 시 확인 대상이면 확인창), 남은 장신구가 앞칸으로, 새 장신구가 뒷칸으로 갑니다
//    (예: [A, B]에서 C -> [B, C], 다시 A -> [C, A]). 실제 처리는 PlayerTrinket이 합니다.
//
// [패시브를 잃을 때 - 48번 등]
// 칸 +1이 사라집니다. 장신구를 2개 끼고 있었다면 나중에 얻은 장신구(뒷칸)를 지금 방 바닥에 버린 아이템처럼 좌대로 떨어뜨립니다
// (형빈 확정 - PlayerTrinket.LateUpdate). 1회성으로 받은 수류탄/해킹툴/청휘석은 빼앗지 않습니다.
//
// [아이템 에셋 설정]
// ItemDefinition: Item Id = 55, Category = Passive, Special Effects에 TrinketSlotExpansionEffect 하나.
[System.Serializable]
public class TrinketSlotExpansionEffect : IPassiveItemEffect, IRemovablePassiveEffect
{
    [Tooltip("보유하는 동안 늘어나는 장신구 칸 수입니다(기본 1칸 + 이 값).")]
    [Min(0)] public int extraTrinketSlots = 1;

    [Header("획득 시 지급 (세 가지 묶음으로 레벨당 최초 1회, 48번으로 얻으면 제한 없음)")]
    [Min(0)] public int grenadeAmount = 1;
    [Min(0)] public int hackingToolAmount = 1;
    [Min(0)] public int pyroxenesAmount = 1;

    // 장신구 칸 등록에 쓰는 이름입니다.
    public static string SlotSourceId(string itemId) => "trinket_slot_expansion_" + itemId;

    public void Apply(PassiveEffectContext context, string sourceItemId)
    {
        if (context == null || context.PlayerObject == null)
        {
            Debug.LogWarning("TrinketSlotExpansionEffect: 플레이어를 찾을 수 없어 효과를 적용하지 못했습니다.");
            return;
        }

        GrantOncePerLevel(context, sourceItemId);

        var trinket = context.PlayerObject.GetComponent<PlayerTrinket>();
        if (trinket == null)
        {
            Debug.LogWarning("TrinketSlotExpansionEffect: PlayerTrinket을 찾지 못해 장신구 칸을 늘리지 못했습니다.");
            return;
        }
        trinket.AddExtraSlots(SlotSourceId(sourceItemId), extraTrinketSlots);
    }

    // [패시브 잃기 - 48번 등] 늘어난 칸을 되돌립니다. 넘치는 장신구(나중에 얻은 쪽)는 그 프레임 끝에 바닥으로 떨어집니다.
    // 1회성으로 받은 수류탄/해킹툴/청휘석은 빼앗지 않습니다(형빈 확정).
    public void Remove(PassiveEffectContext context, string sourceItemId)
    {
        if (context == null || context.PlayerObject == null) return;
        var trinket = context.PlayerObject.GetComponent<PlayerTrinket>();
        if (trinket != null) trinket.RemoveExtraSlots(SlotSourceId(sourceItemId));
    }

    private void GrantOncePerLevel(PassiveEffectContext context, string sourceItemId)
    {
        if (grenadeAmount <= 0 && hackingToolAmount <= 0 && pyroxenesAmount <= 0) return;

        if (context.Resources == null)
        {
            Debug.LogWarning("TrinketSlotExpansionEffect: PlayerResources를 찾을 수 없어 수류탄/해킹툴/청휘석을 지급하지 못했습니다.");
            return;
        }

        // 레벨당 1회 게이트입니다(세 가지 묶음). 아이템 테스트 씬처럼 GameFlowManager가 없는 곳에서는 경고만 남기고 지급합니다.
        // [48번 연동] 48번으로 얻을 때는(PassiveEffectContext.IgnoreOneTimeGrantLimit) 레벨당 1회 제한 없이 지급합니다.
        if (!context.IgnoreOneTimeGrantLimit)
        {
            var gameFlow = Object.FindFirstObjectByType<GameFlowManager>();
            if (gameFlow != null)
            {
                if (!gameFlow.TryConsumeOneTimeGrant(sourceItemId + "_bundle_grant")) return;
            }
            else
            {
                Debug.LogWarning("TrinketSlotExpansionEffect: GameFlowManager를 찾지 못해 레벨당 1회 제한 없이 지급합니다.");
            }
        }

        if (grenadeAmount > 0) context.Resources.Add(BasicItemType.Grenade, grenadeAmount);
        if (hackingToolAmount > 0) context.Resources.Add(BasicItemType.HackingTool, hackingToolAmount);
        if (pyroxenesAmount > 0) context.Resources.Add(BasicItemType.Pyroxenes, pyroxenesAmount);
    }
}
