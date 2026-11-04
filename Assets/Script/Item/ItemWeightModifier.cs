using UnityEngine;

// [아이템 등장 가중치 보정 - 정적 등록소]
// 67번 패시브("음식 등장 가중치 +50") 같은 "특정 분류 아이템이 더 잘/덜 나오게" 하는 효과를 위한 공용 등록소입니다.
// 앞으로 비슷한 아이템(예: 장신구 가중치 x2)도 Register 한 줄로 붙일 수 있습니다.
//
// - 계산: (아이템 에셋의 weight + 더하기 합계) x 곱하기 전부 곱한 값(스탯과 같은 규칙). 0 아래로는 내려가지 않습니다.
// - 에셋의 weight가 0 이하인 아이템(등장하지 않게 꺼 둔 아이템)은 보정을 받지 않고 그대로 나오지 않습니다.
// - 보정은 "뽑을 때의 가중치"에만 쓰입니다. 어디에 나올 수 있는지(등장 위치 appearanceContexts)는 바꾸지 않으므로,
//   상점 전용 음식은 여전히 상점에서만 나옵니다.
// - 적용되는 곳: 일반 방 보물상자(NormalRoomTreasureSpawner), 아이템 좌대(클럽/특수방 - ItemPedestal), 상점(ShopNPC).
//   새로 아이템을 가중치로 뽑는 곳을 만들 때는 item.weight 대신 ItemWeightModifier.GetWeight(item)를 써 주세요.
// - 플레이 시작 때마다 비워서, 에디터에서 Domain Reload를 꺼 둔 경우에도 이전 플레이의 등록이 남지 않습니다.
public static class ItemWeightModifier
{
    private static readonly CategoryModifierSet modifiers = new CategoryModifierSet();

    // additive = 더할 가중치(없으면 0), multiplier = 곱할 배율(없으면 1). 같은 sourceId로 다시 부르면 덮어씁니다.
    public static void Register(string sourceId, ItemDefinition.ItemCategory category, float additive, float multiplier, Object owner)
    {
        modifiers.Register(sourceId, category, additive, multiplier, owner);
    }

    public static void Unregister(string sourceId)
    {
        modifiers.Unregister(sourceId);
    }

    // 보정이 적용된 실제 등장 가중치입니다.
    public static float GetWeight(ItemDefinition item)
    {
        if (item == null) return 0f;
        float baseWeight = item.weight;
        if (baseWeight <= 0f) return baseWeight; // 꺼 둔 아이템은 보정하지 않음
        return Mathf.Max(0f, modifiers.Apply(item.category, baseWeight));
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlayStart()
    {
        modifiers.Clear();
    }
}
