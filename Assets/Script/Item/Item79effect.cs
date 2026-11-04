using UnityEngine;

// 패시브 79번 - "보유하고 있으면 음식을 최대 두 개 보유할 수 있다."
//
// [기획 - 형빈 확정, 61번(일회용 칸 +1)과 같은 규칙]
// - 보유하는 동안 음식 칸이 2칸이 됩니다. 칸이 꽉 찬 상태에서 음식을 얻으면 가장 먼저 얻은 음식이 교체되고(교체 시 확인 대상이면
//   확인창), 남은 음식이 앞칸으로, 새 음식이 뒷칸으로 갑니다.
// - E는 항상 앞칸(먼저 얻은) 음식을 먹습니다. 즉시형이면 바로 소모되고 뒷칸 음식이 앞으로 당겨집니다. 지연형이면 앞칸 음식이 대기
//   상태가 되고, 대기가 끝나 소모될 때까지 E를 눌러도 앞칸/뒷칸 모두 먹지 않습니다.
// - Ctrl을 꾹 눌러 여는 버리기 창에서 두 칸을 각각 버릴 수 있습니다.
// 실제 처리는 PlayerFoodItem이 합니다.
//
// [패시브를 잃을 때 - 48번 등]
// 칸 +1이 사라집니다. 음식을 2개 가지고 있었다면 나중에 얻은 음식(뒷칸)을 지금 방 바닥에 좌대로 떨어뜨립니다(55번/61번과 같음 -
// PlayerFoodItem.LateUpdate). 대기 중인 음식은 항상 앞칸이라 떨어지지 않습니다.
//
// [아이템 에셋 설정]
// ItemDefinition: Item Id = 79, Category = Passive, Special Effects에 FoodSlotExpansionEffect 하나.
[System.Serializable]
public class FoodSlotExpansionEffect : IPassiveItemEffect, IRemovablePassiveEffect
{
    [Tooltip("보유하는 동안 늘어나는 음식 칸 수입니다(기본 1칸 + 이 값).")]
    [Min(0)] public int extraFoodSlots = 1;

    // 음식 칸 등록에 쓰는 이름입니다.
    public static string SlotSourceId(string itemId) => "food_slot_expansion_" + itemId;

    public void Apply(PassiveEffectContext context, string sourceItemId)
    {
        if (context == null || context.PlayerObject == null)
        {
            Debug.LogWarning("FoodSlotExpansionEffect: 플레이어를 찾을 수 없어 효과를 적용하지 못했습니다.");
            return;
        }

        var food = context.PlayerObject.GetComponent<PlayerFoodItem>();
        if (food == null)
        {
            Debug.LogWarning("FoodSlotExpansionEffect: PlayerFoodItem을 찾지 못해 음식 칸을 늘리지 못했습니다.");
            return;
        }
        food.AddExtraSlots(SlotSourceId(sourceItemId), extraFoodSlots);
    }

    // [패시브 잃기 - 48번 등] 늘어난 칸을 되돌립니다. 넘치는 음식(나중에 얻은 쪽)은 그 프레임 끝에 바닥으로 떨어집니다.
    public void Remove(PassiveEffectContext context, string sourceItemId)
    {
        if (context == null || context.PlayerObject == null) return;
        var food = context.PlayerObject.GetComponent<PlayerFoodItem>();
        if (food != null) food.RemoveExtraSlots(SlotSourceId(sourceItemId));
    }
}
