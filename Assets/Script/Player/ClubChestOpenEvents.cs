using System;

// 동아리방(Club) 보물상자가 열릴 때만 울리는 신호입니다. ContainerOpenEvents.OnAnyContainerOpened와
// 달리, 일반방/보스방 보물상자(TreasureChest)가 열릴 때는 울리지 않습니다 - 27번 음식
// ("사용하면 다음에 여는 동아리방 상자에서 반드시 최상급 아이템 중 하나가 나온다")처럼,
// "아무 상자나 다음 것"이 아니라 "다음 동아리방 상자"를 정확히 특정해야 하는 효과를 위한
// 전용 신호입니다(ContainerOpenEvents.cs 상단 주석의 48번 아이템과 같은 이유로 별도 static
// 클래스로 뽑아냈습니다 - TreasureChest/ItemPedestal이 서로 다른 클래스라 한쪽에 두면
// 자연스럽지 않습니다).
//
// [지금은 ItemPedestal = 동아리방 전용]
// 현재 ItemPedestal은 동아리방에만 스폰됩니다(LevelBuilder.SpawnClubItemPedestal이 유일한
// 스폰 지점). 그래서 이 이벤트는 ItemPedestal.Open()에서만 울리며, 지금 시점엔 "이 이벤트가
// 울렸다 = 동아리방 보물상자가 열렸다"가 항상 성립합니다. 나중에 이 보물상자가 다른 방(보스방/상점 등)
// 에도 재사용되면, 그때는 이 이벤트를 "그 보물상자가 실제로 동아리방 소속일 때만" 울리도록
// room 정보를 함께 확인하는 형태로 다시 손봐야 합니다.
//
// ItemPedestal.Open()이 실제 내용물을 확정하기(후보를 거르거나 뽑기) 전, ContainerOpenEvents.
// RaiseOpened()와 같은 시점에 함께 호출합니다 - 27번 음식이 여기서
// NextClubPedestalLegendaryOverride를 무장시켜야, 그 직후 일어나는 실제 뽑기(PickWeighted)에
// 반영됩니다.
public static class ClubChestOpenEvents
{
    public static event Action OnClubChestOpened;

    public static void RaiseOpened()
    {
        OnClubChestOpened?.Invoke();
    }
}
