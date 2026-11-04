using System;

// 보스방 보상 보물상자가 열릴 때만 울리는 신호입니다. ClubChestOpenEvents(동아리방 전용)의 보스방 버전입니다.
// ContainerOpenEvents.OnAnyContainerOpened(아무 상자나)와 달리 일반방 보물상자가 열릴 때는 울리지 않습니다.
//
// [왜 필요한가 - 27번 음식 확장, 형빈 확정]
// 27번 음식("사용하면 다음에 여는 상자에서 반드시 최상급(Tier 3) 아이템이 나온다")의 대상이 "다음 동아리방
// 보물상자"에서 "다음 동아리방 또는 보스방 보물상자 중 먼저 여는 것 하나"로 넓어졌습니다. 보스방 상자는
// 일반방과 같은 TreasureChest라 상자 쪽에서는 구분할 수 없으므로, 보스방 상자의 내용물을 정하는
// NormalRoomTreasureSpawner.ResolveBossRewardContent()가 실제 뽑기 직전에 이 신호를 울립니다(9번 음식으로
// 복제된 보스방 상자도 같은 함수를 쓰므로 똑같이 울립니다). 27번이 여기서 NextClubPedestalLegendaryOverride를
// 무장시키면 그 직후의 뽑기에 반영됩니다.
public static class BossChestOpenEvents
{
    public static event Action OnBossChestOpened;

    public static void RaiseOpened()
    {
        OnBossChestOpened?.Invoke();
    }
}
