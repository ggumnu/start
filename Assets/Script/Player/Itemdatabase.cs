using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 프로젝트의 모든 ItemDefinition을 한곳에 모아두는 데이터베이스입니다. "이 아이템이
// 어디서 등장하는가"는 LevelBuilder가 아니라 각 ItemDefinition.appearanceContexts가
// 결정하므로, LevelBuilder는 이 데이터베이스 하나만 참조하면 되고 방 종류가 늘어나도
// LevelBuilder 인스펙터에 새 리스트 필드를 추가할 필요가 없습니다.
//
// [컨텍스트 키 규칙]
// 방 하나의 "컨텍스트 키"는 GetContextKey()가 결정합니다: 그 방에 variantId가 있으면
// variantId 문자열 그대로(예: 카페='cafe', 편의점='convenience_store'), 없으면
// RoomCategory 이름 그대로(예: 동아리방='Club')를 사용합니다. RoomVariant.variantId와
// 완전히 같은 문자열 규칙을 재사용하므로, 새 특수방(RoomVariant)을 추가할 때 이
// 스크립트나 별도 enum을 건드릴 필요 없이 그 variantId 문자열을 아이템의
// appearanceContexts에 적기만 하면 그 방에서 등장하게 됩니다.
//
// [분류(Category) 필터 - v1.23 추가]
// GetItemsForContext()에 category 매개변수를 추가했습니다. 기본값이 Passive라, 지금까지와
// 동일하게 호출하면(ItemPedestal/NormalRoomTreasureSpawner가 그렇게 호출합니다) 패시브
// 아이템만 반환됩니다. 아직 액티브/장신구/일회용/음식 아이템은 슬롯 제한/교체/소모/전용 UI가
// 구현되지 않았으므로, 이 필터가 그런 아이템이 실수로 보물상자의 패시브 풀에 섞여
// 등장하는 것을 막아줍니다. 나중에 각 분류를 실제로 구현할 때, 그 분류 전용 풀을 뽑는
// 코드에서 이 메서드를 해당 category 값으로 호출하면 됩니다.
[CreateAssetMenu(fileName = "ItemDatabase", menuName = "MobArchive/Item Database")]
public class ItemDatabase : ScriptableObject
{
    [Tooltip("프로젝트에 존재하는 모든 ItemDefinition을 여기에 등록하세요. 실제로 어디서 " +
             "등장할지는 각 ItemDefinition.appearanceContexts가 결정합니다.")]
    public List<ItemDefinition> allItems = new List<ItemDefinition>();

    // room의 컨텍스트 키를 계산합니다. variantId가 있으면 그대로, 없으면 category 이름을 사용합니다.
    // LevelBuilder가 방을 지을 때, 그리고 ItemDefinition.appearanceContexts를 채울 때 항상
    // 이 규칙(또는 이와 동일한 문자열)을 기준으로 삼아야 서로 매칭됩니다.
    public static string GetContextKey(RoomInstance room)
    {
        if (room == null) return null;
        return string.IsNullOrEmpty(room.variantId) ? room.category.ToString() : room.variantId;
    }

    // [특수방 통합 - 방 기준으로 후보 고르기]
    // GetItemsForContext()는 키 하나(variantId가 있으면 variantId, 없으면 카테고리 이름)만 비교해서, variantId가
    // 있는 방에서는 "카테고리 전체"로 지정한 아이템이 절대 매칭되지 않습니다. 특수방은 항상 레이아웃 ID(variantId)를
    // 가지므로, 이 메서드는 방 하나를 두 가지 기준으로 봅니다 - 아이템의 컨텍스트 중 하나라도
    //   1) category가 같고 variantId가 이 방의 variantId와 같거나(이 레이아웃/variant 전용 아이템)
    //   2) includeCategoryWide가 켜져 있고, category가 같고 variantId가 비어 있으면(카테고리 공용 아이템)
    // 후보가 됩니다. variantId가 없는 방(동아리방/일반방/보스방)은 1)이 성립할 수 없으므로 2)만 보고, 그 결과는
    // 기존 GetItemsForContext(카테고리 이름)와 같습니다(includeCategoryWide가 켜져 있을 때).
    //
    // 지금은 특수방 아이템 받침대(LevelBuilder.SpawnSpecialItemPedestal)와 레이아웃 미리보기(SpecialRoomLayoutEditor)만
    // 이 메서드를 씁니다. 기존 동아리방/일반방/보스방/상점 코드는 그대로 GetItemsForContext를 씁니다.
    public List<ItemDefinition> GetItemsForRoom(RoomInstance room, bool includeCategoryWide, ItemDefinition.ItemCategory category = ItemDefinition.ItemCategory.Passive)
    {
        if (room == null) return new List<ItemDefinition>();
        return GetItemsFor(room.category, room.variantId, includeCategoryWide, category);
    }

    // GetItemsForRoom과 같은 규칙을 방(RoomInstance) 없이 카테고리 + variantId로 바로 확인합니다
    // (에디터 미리보기처럼 실제 방이 아직 없을 때 사용).
    public List<ItemDefinition> GetItemsFor(RoomCategory roomCategory, string roomVariantId, bool includeCategoryWide, ItemDefinition.ItemCategory category = ItemDefinition.ItemCategory.Passive)
    {
        if (allItems == null) return new List<ItemDefinition>();

        return allItems
            .Where(item => item != null && item.category == category &&
                           item.appearanceContexts != null &&
                           item.appearanceContexts.Any(ctx => MatchesRoom(ctx, roomCategory, roomVariantId, includeCategoryWide)))
            .ToList();
    }

    // 방에 놓이는 보물상자(동아리방/특수방 받침대)가 쓰는 "모든 분류를 합친" 풀입니다. 순서와 구성은
    // LevelBuilder.SpawnClubItemPedestal과 같습니다(패시브 → 액티브 → 장신구 → 일회용 → 음식).
    public List<ItemDefinition> GetAllCategoryItemsForRoom(RoomInstance room, bool includeCategoryWide)
    {
        var result = new List<ItemDefinition>();
        foreach (var itemCategory in PedestalPoolCategories)
        {
            result.AddRange(GetItemsForRoom(room, includeCategoryWide, itemCategory));
        }
        return result;
    }

    // 동아리방/특수방 받침대 풀에 들어가는 분류 순서입니다.
    public static readonly ItemDefinition.ItemCategory[] PedestalPoolCategories =
    {
        ItemDefinition.ItemCategory.Passive,
        ItemDefinition.ItemCategory.Active,
        ItemDefinition.ItemCategory.Trinket,
        ItemDefinition.ItemCategory.Disposable,
        ItemDefinition.ItemCategory.Food,
    };

    // 아이템 컨텍스트 하나가 이 방(카테고리 + variantId)에 해당하는지 확인합니다. 규칙은 GetItemsForRoom 주석 참고.
    public static bool MatchesRoom(AppearanceContext ctx, RoomCategory roomCategory, string roomVariantId, bool includeCategoryWide)
    {
        if (ctx == null || ctx.category != roomCategory) return false;

        if (string.IsNullOrEmpty(ctx.variantId))
        {
            return includeCategoryWide;
        }

        return !string.IsNullOrEmpty(roomVariantId) &&
               string.Equals(ctx.variantId, roomVariantId, StringComparison.OrdinalIgnoreCase);
    }

    // contextKey와 일치하는 컨텍스트를 하나라도 가진 아이템 중, category와 일치하는 것만
    // 걸러서 반환합니다(대소문자 무시). category 기본값은 Passive입니다 - 지금까지 이
    // 메서드를 호출하던 모든 곳(ItemPedestal, NormalRoomTreasureSpawner)이 패시브 아이템만
    // 뽑던 것과 동일하게 계속 동작하며, 액티브/장신구/일회용/음식 아이템은 그 분류를 실제로
    // 구현해 이 메서드를 그 category로 호출하기 전까지는 여기서 걸러지지 않습니다(=아무
    // 풀에도 등장하지 않습니다).
    public List<ItemDefinition> GetItemsForContext(string contextKey, ItemDefinition.ItemCategory category = ItemDefinition.ItemCategory.Passive)
    {
        if (string.IsNullOrEmpty(contextKey) || allItems == null) return new List<ItemDefinition>();

        return allItems
            .Where(item => item != null && item.category == category &&
                           item.appearanceContexts != null &&
                           item.appearanceContexts.Any(ctx => ctx != null &&
                               string.Equals(ctx.ToContextKey(), contextKey, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }
}
