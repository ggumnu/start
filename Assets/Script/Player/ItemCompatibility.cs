using System.Collections.Generic;
using UnityEngine;

// [같이 가질 수 없는 아이템(상극) 규칙 - 99번 때 추가, 형빈 확정]
// 서로 모순되는 아이템(예: 99번 적 관통 ↔ 69번 적 튕김)은 함께 가질 수 없고, 등장도 하지 않습니다.
// ItemDefinition의 Incompatible Item Ids 칸에 상대 아이템 ID를 적으면 됩니다. 한쪽에만 적어도 양쪽 방향으로 똑같이 적용됩니다
// (99번에 "69"만 적으면 69번 → 99번도 막힘).
//
// [언제 막히나]
// 1. 보유: 가지고 있는 아이템(패시브 + 액티브/장신구/일회용/음식 칸)과 상극인 아이템은 보물상자/동아리방 좌대/상점 후보에서 빠집니다
//    (PlayerInventory.FilterUnowned). 혹시 상극 아이템을 주우려 하면 획득도 거절됩니다(PlayerInventory.TryAcquireItem - 아이템은 그대로 남음).
// 2. 이 레벨에 등장: 이번 레벨에 한 번이라도 뽑힌 아이템(보물상자/좌대/상점 - 줍지 않았어도)과 상극인 아이템도 그 레벨의 다른 곳에서
//    나오지 않습니다(GameFlowManager.FilterOutAlreadyAssignedThisLevel). 다음 레벨로 넘어가면 초기화됩니다.
// 3. 한 번에 여러 개를 뽑는 곳(상점 진열 2칸, 48번/56번/80번)은 같이 뽑힌 것끼리도 상극이 나오지 않게 합니다.
//
// 이 파일은 판정만 하고 상태를 갖지 않습니다.
public static class ItemCompatibility
{
    // 두 아이템이 서로 상극이면 true입니다(어느 한쪽 목록에만 적혀 있어도).
    public static bool AreIncompatible(ItemDefinition a, ItemDefinition b)
    {
        if (a == null || b == null || a == b) return false;
        if (!string.IsNullOrEmpty(a.itemId) && a.itemId == b.itemId) return false;
        return Lists(a, b.itemId) || Lists(b, a.itemId);
    }

    // item이 others 중 하나라도 상극이면 true입니다.
    public static bool ConflictsWithAny(ItemDefinition item, IEnumerable<ItemDefinition> others)
    {
        if (item == null || others == null) return false;
        foreach (var other in others)
        {
            if (AreIncompatible(item, other)) return true;
        }
        return false;
    }

    // item이 ids(아이템 ID 목록) 중 하나라도 상극이면 true입니다. resolve는 ID로 아이템을 찾는 함수입니다(상대 쪽 목록 확인용 -
    // 없으면 item 쪽 목록만 봅니다).
    public static bool ConflictsWithIds(ItemDefinition item, IEnumerable<string> ids, System.Func<string, ItemDefinition> resolve)
    {
        if (item == null || ids == null) return false;
        foreach (var id in ids)
        {
            if (string.IsNullOrEmpty(id) || id == item.itemId) continue;
            if (Lists(item, id)) return true;
            var other = resolve != null ? resolve(id) : null;
            if (other != null && Lists(other, item.itemId)) return true;
        }
        return false;
    }

    // 지금 플레이어가 가진 모든 아이템입니다(패시브 + 액티브/장신구/일회용/음식 칸). player는 플레이어 오브젝트입니다.
    public static List<ItemDefinition> CollectOwned(GameObject player)
    {
        var result = new List<ItemDefinition>();
        if (player == null) return result;

        var inventory = player.GetComponent<PlayerInventory>();
        if (inventory != null)
        {
            foreach (var item in inventory.OwnedItems) if (item != null) result.Add(item);
        }

        var active = player.GetComponent<PlayerActiveItem>();
        if (active != null && active.EquippedItem != null) result.Add(active.EquippedItem);

        var trinket = player.GetComponent<PlayerTrinket>();
        if (trinket != null)
        {
            for (int k = 0; k < trinket.SlotCount; k++) AddIfNotNull(result, trinket.GetEquippedAt(k));
        }

        var disposable = player.GetComponent<PlayerDisposableItem>();
        if (disposable != null)
        {
            for (int k = 0; k < disposable.SlotCount; k++) AddIfNotNull(result, disposable.GetEquippedAt(k));
        }

        var food = player.GetComponent<PlayerFoodItem>();
        if (food != null)
        {
            for (int k = 0; k < food.SlotCount; k++) AddIfNotNull(result, food.GetEquippedAt(k));
        }

        return result;
    }

    // item의 상극 목록에 otherId가 있는지입니다(앞뒤 공백 무시).
    private static bool Lists(ItemDefinition item, string otherId)
    {
        if (item == null || item.incompatibleItemIds == null || string.IsNullOrEmpty(otherId)) return false;
        string target = otherId.Trim();
        foreach (var id in item.incompatibleItemIds)
        {
            if (!string.IsNullOrEmpty(id) && id.Trim() == target) return true;
        }
        return false;
    }

    private static void AddIfNotNull(List<ItemDefinition> list, ItemDefinition item)
    {
        if (item != null && !list.Contains(item)) list.Add(item);
    }
}
