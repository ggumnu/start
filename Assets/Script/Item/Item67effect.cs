using UnityEngine;

// 패시브 67번 - "음식이 더 자주 등장하고, 상점의 음식이 25% 할인된다."
//
// [기획 - 형빈 확정]
// - 보유하고 있는 동안 음식(Category = Food) 아이템의 등장 가중치가 +foodWeightBonus(기본 50) 오릅니다(더하기).
//   보물상자/클럽·특수방 좌대/상점 등 음식이 원래 나올 수 있는 곳에서만 적용됩니다(상점 전용 음식이 특수방에 나오지는 않음).
//   가중치가 0 이하로 꺼 둔 음식은 그대로 나오지 않습니다.
// - 보유하고 있는 동안 상점의 음식 가격이 foodDiscountPercent(기본 25)% 할인됩니다. 소수점은 버리고, 최소 1입니다.
//   상점 카드/구매 팝업에 원래 가격(회색 + 빨간 줄)과 할인된 가격이 같이 표시되고, 얻거나 잃는 즉시 가격이 바뀝니다.
// - 같은 효과를 가진 다른 아이템과는 스탯처럼 더하기를 먼저 모두 더한 뒤 곱하기를 적용합니다.
// - 잃으면(48번 교체 등) 두 효과 모두 바로 사라집니다.
//
// [구현]
// 공용 등록소 ItemWeightModifier(등장 가중치)와 ShopPriceModifier(상점 가격)에 음식 분류로 등록/해제만 합니다.
//
// [아이템 에셋 설정]
// ItemDefinition: Item Id = 67, Category = Passive, Special Effects에 FoodFavorEffect 하나. Stat Modifiers는 비워둡니다.
[System.Serializable]
public class FoodFavorEffect : IPassiveItemEffect, IRemovablePassiveEffect
{
    [Tooltip("음식 아이템의 등장 가중치에 더하는 값입니다(기본 50). 예) 가중치 60인 음식 -> 110.")]
    public float foodWeightBonus = 50f;

    [Tooltip("상점 음식 가격 할인율(%)입니다(기본 25). 할인된 가격은 소수점 버림, 최소 1.")]
    [Range(0f, 100f)] public float foodDiscountPercent = 25f;

    public void Apply(PassiveEffectContext context, string sourceItemId)
    {
        if (context == null || context.PlayerObject == null)
        {
            Debug.LogWarning("FoodFavorEffect: 플레이어를 찾을 수 없어 67번 효과를 적용하지 못했습니다.");
            return;
        }

        var owner = context.PlayerObject;
        ItemWeightModifier.Register(sourceItemId, ItemDefinition.ItemCategory.Food, foodWeightBonus, 1f, owner);
        ShopPriceModifier.Register(sourceItemId, ItemDefinition.ItemCategory.Food, 0f, 1f - foodDiscountPercent / 100f, owner);
    }

    // [패시브 잃기 - 48번 등] 두 효과를 바로 해제합니다.
    public void Remove(PassiveEffectContext context, string sourceItemId)
    {
        ItemWeightModifier.Unregister(sourceItemId);
        ShopPriceModifier.Unregister(sourceItemId);
    }
}
