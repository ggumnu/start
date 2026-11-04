using UnityEngine;

// 일회용 86번 - "사용하면 이번 레벨 상점의 모든 음식 아이템 가격이 0이 된다."
//
// [기획 - 형빈 확정]
// - 사용 즉시 소모되고, 사용한 레벨(전투 레벨 n)에서만 상점의 음식 아이템 가격이 0 크레딧이 됩니다. 이미 진열된 음식과, 같은
//   레벨에서 상점 갱신으로 새로 뜬 음식 모두 0입니다. n.5 선택방 레벨과 다음 레벨부터는 원래 가격입니다(n.5는 같은 레벨로 보지 않음).
// - 67번(음식 25% 할인)과 같이 있어도 0입니다. 상점 창은 원래 가격(회색 줄) + 0으로 보여주고, 가격 옆 크레딧 아이콘 자리에 86번
//   아이콘을 보여줘서 86번으로 할인됐다는 것을 알려줍니다.
// - 사용 제한:
//   - 전투 중인 방에서는 Q를 눌러도 아무 반응이 없습니다(창도 뜨지 않고 소모되지도 않음 - 80번과 같음).
//   - 전투 중이 아닐 때, 이번 레벨에서 이미 사용했으면 alreadyUsedMessage, 이번 레벨에 상점이 없으면(n.5 선택방 레벨 포함)
//     noShopMessage 창을 띄우고 소모하지 않습니다.
// - 상점 판정: 이번 레벨에 만들어진 방 중 편의점 분류(Store - 편의점/카페 등 variant 포함) 방이 하나라도 있으면 상점이 있는
//   것으로 봅니다(아직 가보지 않았어도). 레벨 생성기가 없는 씬(아이템 테스트 씬)에서는 씬에 상점 NPC가 있는지로 봅니다.
// - 사용 연출은 없습니다.
//
// [구현 - 지연형 일회용 구조를 활용 (80번과 같은 방식)]
// 사용할 수 없을 때 "소모하지 않고 창만 띄우기"를 위해 지연형(IsImmediate = false)으로 만들었습니다. Q를 누르면 BeginPending에서
// 바로 판정해서, 적용했으면 CompletePendingUse(소모), 막혔으면 창을 띄우고 CancelPendingUse(그대로 남음)를 부릅니다. 대기 상태는
// 같은 프레임 안에 끝나므로 화면에 보이지 않습니다. 무료 처리는 ShopPriceModifier.RegisterLevelFree(레벨 한정 무료 등록)가 합니다.
//
// [아이템 에셋 설정]
// ItemDefinition: Item Id = 86, Category = Disposable, Disposable Effect = LevelFoodFreeShopEffect.
// 가격 옆 아이콘은 비워두면 86번 아이템 에셋의 Icon을 그대로 씁니다.
[System.Serializable]
public class LevelFoodFreeShopEffect : IDisposableItemEffect
{
    [Tooltip("이번 레벨에서 이미 사용했을 때 띄우는 창의 문구입니다.")]
    public string alreadyUsedMessage = "이번 레벨에서 이미 사용했습니다";

    [Tooltip("이번 레벨에 상점이 없을 때(n.5 선택방 레벨 포함) 띄우는 창의 문구입니다.")]
    public string noShopMessage = "이번 레벨에는 상점이 없습니다";

    [Tooltip("상점 가격 옆 크레딧 아이콘 대신 보여줄 아이콘입니다. 비워두면 86번 아이템 에셋의 Icon을 씁니다.")]
    public Sprite priceIconOverride;

    // 무료 등록에 쓰는 이름입니다(같은 이름으로 다시 등록하면 새 레벨 기준으로 덮어씀).
    public static string FreeSourceId(string itemId) => "level_food_free_" + itemId;

    public bool IsImmediate => false;

    // 전투 중인 방이거나 창이 이미 떠 있으면 사용하지 않습니다(아무 창도 띄우지 않고 소모되지도 않음 - 80번과 같음).
    public bool CanUse(DisposableEffectContext context)
    {
        return context != null && context.PlayerObject != null && !PassiveRerollUI.IsOpen &&
               SlotItemDropper.IsInCombatFreeRoom();
    }

    // 즉시형이 아니라 호출되지 않습니다.
    public void ActivateImmediate(DisposableEffectContext context, string sourceItemId) { }

    public void BeginPending(DisposableEffectContext context, string sourceItemId)
    {
        GameObject player = context.PlayerObject;

        // 이미 이번 레벨에서 음식이 무료면(86번을 이미 사용) 막습니다.
        if (ShopPriceModifier.IsLevelFree(ItemDefinition.ItemCategory.Food))
        {
            PassiveRerollUI.ShowMessage(player, alreadyUsedMessage);
            context.CancelPendingUse(sourceItemId);
            return;
        }

        if (!CurrentLevelHasShop(context))
        {
            PassiveRerollUI.ShowMessage(player, noShopMessage);
            context.CancelPendingUse(sourceItemId);
            return;
        }

        ShopPriceModifier.RegisterLevelFree(FreeSourceId(sourceItemId), ItemDefinition.ItemCategory.Food, player,
                                            ResolveIcon(context, sourceItemId));
        Debug.Log("LevelFoodFreeShopEffect: 이번 레벨 상점의 음식 가격이 0이 됩니다.");
        context.CompletePendingUse(sourceItemId);
    }

    // 지연형이지만 BeginPending 안에서 바로 끝나므로 정리할 것이 없습니다.
    public void CancelPending(DisposableEffectContext context, string sourceItemId) { }

    // 이번 레벨(전투 레벨)에 상점(편의점 분류 방)이 있는지입니다. n.5 선택방 레벨은 항상 false입니다.
    private static bool CurrentLevelHasShop(DisposableEffectContext context)
    {
        var gameFlow = context.GameFlow != null ? context.GameFlow : Object.FindFirstObjectByType<GameFlowManager>();
        if (gameFlow != null && gameFlow.CurrentPhase != GameFlowManager.LevelPhase.Combat) return false;

        var levelBuilder = context.LevelBuilder != null ? context.LevelBuilder : Object.FindFirstObjectByType<LevelBuilder>();
        if (levelBuilder != null && levelBuilder.GeneratedRooms != null)
        {
            foreach (var room in levelBuilder.GeneratedRooms)
            {
                if (room != null && room.category == RoomCategory.Store) return true;
            }
            return false;
        }

        // 레벨 생성기가 없는 씬(아이템 테스트 씬): 씬에 상점 NPC가 있는지로 봅니다(꺼져 있는 것도 포함).
        return Object.FindObjectsByType<ShopNPC>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length > 0;
    }

    // 가격 옆 아이콘: 지정한 아이콘 -> 지금 쓰고 있는 86번 아이템(일회용 칸)의 Icon -> 아이템 목록에서 찾은 Icon 순서.
    private Sprite ResolveIcon(DisposableEffectContext context, string sourceItemId)
    {
        if (priceIconOverride != null) return priceIconOverride;

        var slot = context.PlayerObject != null ? context.PlayerObject.GetComponent<PlayerDisposableItem>() : null;
        if (slot != null)
        {
            foreach (var item in slot.EquippedItems)
            {
                if (item != null && item.itemId == sourceItemId && item.icon != null) return item.icon;
            }
        }

        var levelBuilder = context.LevelBuilder != null ? context.LevelBuilder : Object.FindFirstObjectByType<LevelBuilder>();
        if (levelBuilder != null && levelBuilder.itemDatabase != null)
        {
            foreach (var item in levelBuilder.itemDatabase.allItems)
            {
                if (item != null && item.itemId == sourceItemId && item.icon != null) return item.icon;
            }
        }

        Debug.LogWarning("LevelFoodFreeShopEffect: 86번 아이콘을 찾지 못해 가격 옆에는 기존 크레딧 아이콘이 보입니다. Price Icon Override를 채워주세요.");
        return null;
    }
}
