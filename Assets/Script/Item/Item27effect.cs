using System;

// 음식 27번 - "사용하면(지연 소모) 다음에 개봉하는 동아리방 보물상자에서 반드시 최상급
// (Tier 3) 아이템 중 하나가 등장한다".
//
// [확장 - 보스방 보물상자도 대상 (형빈 확정)]
// 대상이 "다음 동아리방 보물상자"에서 "다음에 여는 동아리방 또는 보스방 보물상자 중 먼저 여는 상자 하나"로
// 넓어졌습니다. 둘 중 먼저 열리는 쪽에서 최상급이 나오고 27번은 그때 소모됩니다(1회성 그대로). 일반방 보물상자는
// 여전히 반응하지 않습니다. 보스방 쪽 신호는 BossChestOpenEvents(NormalRoomTreasureSpawner.
// ResolveBossRewardContent가 뽑기 직전에 울림 - 9번으로 복제된 보스방 상자 포함)이고, 무장 플래그는 동아리방과
// 같은 NextClubPedestalLegendaryOverride를 함께 씁니다(이름은 동아리방용이지만 보스방 뽑기도 이 값을 확인).
// 아래 [기획]의 "동아리방만 반응" 설명은 이 확장 이전의 내용입니다.
//
// [기획]
// - 지연형 음식입니다 - 사용해도 그 즉시 효과가 나타나지 않습니다. 사용 후에도 음식 전용
//   인벤토리 한 칸을 계속 차지한 채 "대기" 상태로 남고("먹은 뒤 행동을 해야 소모"), 실제
//   효과와 소멸은 플레이어가 다음으로 "동아리방 보물상자"를 여는 바로 그 순간(F로 처음 열 때)
//   함께 일어납니다.
// - 대기 중에 다른 음식으로 교체되면(PlayerFoodItem.TryAcquireItem()이 CancelPending()을
//   호출) 대기가 취소되어 "사용 자체를 하지 않은" 판정으로 되돌아갑니다 - 다시 획득하면
//   처음부터 다시 사용해야 합니다.
// - 대기 중엔 동아리방이 아닌 다른 방의 보물상자(일반방/보스방 TreasureChest)를 아무리
//   열어도 소모되지 않습니다 - 오직 "동아리방 보물상자"가 열릴 때만 반응합니다
//   (ClubChestOpenEvents 참고 - ContainerOpenEvents.OnAnyContainerOpened와 달리 동아리방
//   전용 신호입니다).
// - 최상급 등급으로 등록된 아이템이 그 보물상자의 후보 풀에 하나도 없으면(아직 등록되지 않았거나
//   전부 이미 보유/배정된 경우) 보장을 지킬 수 없으므로, ItemPedestal이 경고를 남기고 보장
//   없이 평소대로 진행합니다(ItemPedestal.PickWeighted() 참고).
//
// [구현 - ClubChestOpenEvents/NextClubPedestalLegendaryOverride]
// Item48effect(48번 일회용 - 다음 상자 가중치 뒤섞기)와 완전히 동일한 구조입니다. 다만
// ContainerOpenEvents(아무 상자나) 대신 ClubChestOpenEvents(동아리방 보물상자만)를 구독하고,
// NextItemPickWeightOverride 대신 NextClubPedestalLegendaryOverride를 무장시킨다는 점만
// 다릅니다. 사용하는 순간 ClubChestOpenEvents.OnClubChestOpened를 한 번 구독해두고, 처음
// 발생하는 순간(=다음 동아리방 보물상자가 열리는 순간) NextClubPedestalLegendaryOverride를
// 무장시켜(그 보물상자의 실제 뽑기 로직이 바로 뒤이어 실행되므로, 여기서 무장하면 정확히 그
// 보물상자에만 반영됩니다) 스스로 구독을 해지하고, CompletePendingUse()를 호출해 실제 소모를
// 요청합니다. 무장 자체를 해제하는 일은 이 클래스가 하지 않습니다 - ItemPedestal.Open()이
// 뽑기가 끝난 뒤 항상 NextClubPedestalLegendaryOverride.ClearAfterThisOpen()을 호출해 스스로
// 정리합니다.
[Serializable]
public class GuaranteedLegendaryFoodEffect : IFoodEffect
{
    // 대기 중 열림 신호를 기다리기 위해 구독해둔 핸들러입니다. null이 아니면 "지금 대기
    // 중이고 구독도 이미 되어있다"는 뜻입니다.
    private Action pendingHandler;
    [NonSerialized] private Action sceneChangeHandler;

    public bool IsImmediate => false;

    // 획득 시 1회성 효과가 없는 음식이라 빈 본문입니다.
    public void OnAcquired(FoodEffectContext context, string sourceItemId)
    {
    }

    // [9번 음식 연동] 이 음식은 항상 사용할 수 있습니다.
    public bool CanUse(FoodEffectContext context) => true;

    // 지연형이라 실제로는 호출되지 않지만, 인터페이스 계약상 빈 본문을 남겨둡니다.
    public void ActivateImmediate(FoodEffectContext context, string sourceItemId)
    {
    }

    public void BeginPending(FoodEffectContext context, string sourceItemId)
    {
        // 이미 대기 중이면(정상적으로는 PlayerFoodItem이 IsPending일 때 사용 입력을 아예
        // 막아주므로 발생하지 않지만, 안전하게) 중복 구독하지 않습니다.
        if (pendingHandler != null) return;

        // 동아리방/보스방 보물상자 중 먼저 열리는 쪽 하나에만 반응하고, 그 순간 두 신호 모두 구독을 해지합니다.
        pendingHandler = () =>
        {
            NextClubPedestalLegendaryOverride.ArmForThisOpen();
            ClubChestOpenEvents.OnClubChestOpened -= pendingHandler;
            BossChestOpenEvents.OnBossChestOpened -= pendingHandler;
            pendingHandler = null;
            ClearSceneChangeHandler();
            context?.CompletePendingUse(sourceItemId);
        };
        ClubChestOpenEvents.OnClubChestOpened += pendingHandler;
        BossChestOpenEvents.OnBossChestOpened += pendingHandler;

        // 대기 중에 판이 끝나면(사망 후 다시하기 등) 다음 판의 상자에 반응하지 않게 대기를 취소.
        sceneChangeHandler = () => CancelPending(context, sourceItemId);
        LoadingScreen.OnBeforeSceneChange += sceneChangeHandler;
    }

    // 대기 중에 다른 음식으로 교체되어 대기가 취소될 때 호출됩니다. 구독만 해지하고, 무장은
    // 걸지 않습니다 - 효과가 아예 없었던 것으로 되돌립니다.
    public void CancelPending(FoodEffectContext context, string sourceItemId)
    {
        ClearSceneChangeHandler();
        if (pendingHandler == null) return;
        ClubChestOpenEvents.OnClubChestOpened -= pendingHandler;
        BossChestOpenEvents.OnBossChestOpened -= pendingHandler;
        pendingHandler = null;
    }

    private void ClearSceneChangeHandler()
    {
        if (sceneChangeHandler != null) LoadingScreen.OnBeforeSceneChange -= sceneChangeHandler;
        sceneChangeHandler = null;
    }
}
