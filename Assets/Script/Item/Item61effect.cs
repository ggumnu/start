using UnityEngine;

// 패시브 61번 - "보유하고 있으면 일회용 아이템을 최대 두 개 보유할 수 있다."
//
// [기획 - 형빈 확정, 55번(장신구 칸 +1)과 같은 규칙]
// - 보유하는 동안 일회용 칸이 2칸이 됩니다. 칸이 꽉 찬 상태에서 일회용을 얻으면 가장 먼저 얻은 아이템이 교체되고(교체 시 확인 대상이면
//   확인창), 남은 아이템이 앞칸으로, 새 아이템이 뒷칸으로 갑니다.
// - Q는 항상 앞칸(먼저 얻은) 아이템을 씁니다. 즉시형이면 바로 소모되고 뒷칸 아이템이 앞으로 당겨집니다(예: [A, B]에서 Q -> A 소모,
//   [B]). 지연형이면 앞칸 아이템이 대기 상태가 되고, 대기가 끝나 소모될 때까지 Q를 눌러도 앞칸/뒷칸 모두 쓰이지 않습니다.
// - Ctrl을 꾹 눌러 여는 버리기 창에서 두 칸을 각각 버릴 수 있습니다.
// 실제 처리는 PlayerDisposableItem이 합니다.
//
// [패시브를 잃을 때 - 48번 등]
// 칸 +1이 사라집니다. 일회용을 2개 가지고 있었다면 나중에 얻은 아이템(뒷칸)을 지금 방 바닥에 좌대로 떨어뜨립니다(55번과 같음 -
// PlayerDisposableItem.LateUpdate). 대기 중인 아이템은 항상 앞칸이라 떨어지지 않습니다.
//
// [아이템 에셋 설정]
// ItemDefinition: Item Id = 61, Category = Passive, Special Effects에 DisposableSlotExpansionEffect 하나.
[System.Serializable]
public class DisposableSlotExpansionEffect : IPassiveItemEffect, IRemovablePassiveEffect
{
    [Tooltip("보유하는 동안 늘어나는 일회용 칸 수입니다(기본 1칸 + 이 값).")]
    [Min(0)] public int extraDisposableSlots = 1;

    // 일회용 칸 등록에 쓰는 이름입니다.
    public static string SlotSourceId(string itemId) => "disposable_slot_expansion_" + itemId;

    public void Apply(PassiveEffectContext context, string sourceItemId)
    {
        if (context == null || context.PlayerObject == null)
        {
            Debug.LogWarning("DisposableSlotExpansionEffect: 플레이어를 찾을 수 없어 효과를 적용하지 못했습니다.");
            return;
        }

        var disposable = context.PlayerObject.GetComponent<PlayerDisposableItem>();
        if (disposable == null)
        {
            Debug.LogWarning("DisposableSlotExpansionEffect: PlayerDisposableItem을 찾지 못해 일회용 칸을 늘리지 못했습니다.");
            return;
        }
        disposable.AddExtraSlots(SlotSourceId(sourceItemId), extraDisposableSlots);
    }

    // [패시브 잃기 - 48번 등] 늘어난 칸을 되돌립니다. 넘치는 일회용(나중에 얻은 쪽)은 그 프레임 끝에 바닥으로 떨어집니다.
    public void Remove(PassiveEffectContext context, string sourceItemId)
    {
        if (context == null || context.PlayerObject == null) return;
        var disposable = context.PlayerObject.GetComponent<PlayerDisposableItem>();
        if (disposable != null) disposable.RemoveExtraSlots(SlotSourceId(sourceItemId));
    }
}
