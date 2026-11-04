using System;

// 보물상자에서 플레이어 아이템(패시브/액티브/장신구/일회용/음식)을 실제로 획득한 순간을 알리는 정적 신호입니다.
//
// [왜 따로 만들었나]
// 아이템은 보물상자 말고도 여러 경로(테스트 프로필, 아이템 효과 등)로 플레이어에게 들어올 수 있지만,
// "아이템을 들어 올려 자랑하는" 획득 연출(PlayerItemPickupPresenter)은 보물상자에서 직접 주웠을 때만
// 나와야 합니다. 그래서 각 보유 컴포넌트(PlayerInventory/PlayerActiveItem 등)의 획득 이벤트를 듣는 대신,
// 보물상자(TreasureChest/ItemPedestal)가 획득에 성공한 지점에서 이 신호를 직접 보냅니다.
// 기본 아이템(수류탄/해킹툴/크레딧/청휘석/체력/코스트) 묶음은 이 신호를 보내지 않습니다.
//
// 구독자가 없어도(?.Invoke) 안전합니다.
public static class ItemPickupEvents
{
    // 획득한 아이템(교체로 상자에 남는 기존 아이템이 아니라, 방금 플레이어가 가져간 아이템)을 실어 보냅니다.
    public static event Action<ItemDefinition> OnPlayerItemPickedUp;

    public static void Raise(ItemDefinition item)
    {
        if (item == null) return;
        OnPlayerItemPickedUp?.Invoke(item);
    }
}
