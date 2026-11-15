using System;

// 음식 40번 - "사용하면(지연 소모) 다음에 클리어하는 일반방에 반드시 일반맵 보물상자가
// 등장한다".
//
// [기획]
// - 지연형 음식입니다 - 사용해도 그 즉시 효과가 나타나지 않습니다. 사용 후에도 음식 전용
//   인벤토리 한 칸을 계속 차지한 채 "대기" 상태로 남고, 실제 효과와 소멸은 플레이어가
//   다음으로 "일반방"을 클리어하는(=그 방의 적을 전멸시키는) 바로 그 순간 함께 일어납니다.
// - 대기 중에 보스방 등 일반방이 아닌 전투방을 클리어해도 아무 반응이 없습니다 - 오직
//   "일반방"을 클리어할 때만 반응합니다(RoomController.OnAnyRoomCleared는 일반방/보스방
//   둘 다에서 발생하므로, 여기서 RoomCategory.Normal인지 직접 걸러냅니다). 보스방이 먼저
//   클리어돼도 대기는 취소되지 않고 계속 다음 일반방을 기다립니다.
// - 반응한 그 일반방은 원래의 등장 확률 굴림(NormalRoomTreasureSpawner.TrySpawnTreasure()의
//   chancePerEnemy 기반 확률)을 건너뛰고 무조건 보물상자가 하나 나옵니다 - 이미 확률에
//   당첨돼 있었어도(즉 어차피 나왔을 상황이어도) 상자가 두 개 나오지는 않고, 이 음식은
//   그 클리어 순간에 정확히 소모됩니다.
// - 대기 중에 다른 음식으로 교체되면(PlayerFoodItem.TryAcquireItem()이 CancelPending()을
//   호출) 대기가 취소되어 "사용 자체를 하지 않은" 판정으로 되돌아갑니다 - 다시 획득하면
//   처음부터 다시 사용해야 합니다.
//
// [구현 - RoomController.OnAnyRoomCleared + NextNormalRoomTreasureGuaranteeOverride]
// Item27effect(GuaranteedLegendaryFoodEffect)/Item39effect(CreditBonusTrinketEffect)와
// 동일한 구조로 RoomController.OnAnyRoomCleared를 구독합니다. 다만 그 둘과 달리 방
// 카테고리를 직접 걸러야 하므로(이 이벤트는 일반방/보스방 모두에서 발생), 핸들러 안에서
// RoomCategory.Normal이 아니면 그냥 무시하고 계속 대기합니다. 일반방일 때만
// NextNormalRoomTreasureGuaranteeOverride.ArmForThisRoom()을 호출해 무장하고(그 직후
// RoomController.HandleCleared()가 NormalRoomTreasureSpawner.TrySpawnTreasure()를 호출
// 하므로 이 무장이 정확히 그 상자에만 반영됩니다), 구독을 해지한 뒤 CompletePendingUse()로
// 실제 소모를 요청합니다. 무장 자체를 해제하는 일은 이 클래스가 하지 않습니다 -
// NormalRoomTreasureSpawner.TrySpawnTreasure()가 시작하자마자 항상
// NextNormalRoomTreasureGuaranteeOverride.ClearAfterThisRoom()을 호출해 스스로 정리합니다.
//
// [아이템 에셋 설정]
// ItemDefinition: Item Id = 40, Category = Food, Food Effect =
// GuaranteedNormalRoomTreasureFoodEffect.
[Serializable]
public class GuaranteedNormalRoomTreasureFoodEffect : IFoodEffect
{
    // 대기 중 방 클리어 신호를 기다리기 위해 구독해둔 핸들러입니다. null이 아니면 "지금
    // 대기 중이고 구독도 이미 되어있다"는 뜻입니다.
    private Action<RoomInstance> pendingHandler;
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

        pendingHandler = room =>
        {
            // 이 이벤트는 일반방/보스방 클리어 모두에서 발생합니다 - 일반방이 아니면
            // 무시하고 계속 다음 방을 기다립니다(구독을 유지합니다).
            if (room == null || room.category != RoomCategory.Normal) return;

            NextNormalRoomTreasureGuaranteeOverride.ArmForThisRoom();
            RoomController.OnAnyRoomCleared -= pendingHandler;
            pendingHandler = null;
            ClearSceneChangeHandler();
            context?.CompletePendingUse(sourceItemId);
        };
        RoomController.OnAnyRoomCleared += pendingHandler;

        // 대기 중에 판이 끝나면(사망 후 다시하기 등) 다음 판의 방 클리어에 반응하지 않게 대기를 취소.
        sceneChangeHandler = () => CancelPending(context, sourceItemId);
        LoadingScreen.OnBeforeSceneChange += sceneChangeHandler;
    }

    // 대기 중에 다른 음식으로 교체되어 대기가 취소될 때 호출됩니다. 구독만 해지하고,
    // 무장은 걸지 않습니다 - 효과가 아예 없었던 것으로 되돌립니다.
    public void CancelPending(FoodEffectContext context, string sourceItemId)
    {
        ClearSceneChangeHandler();
        if (pendingHandler != null)
        {
            RoomController.OnAnyRoomCleared -= pendingHandler;
            pendingHandler = null;
        }
    }

    private void ClearSceneChangeHandler()
    {
        if (sceneChangeHandler != null) LoadingScreen.OnBeforeSceneChange -= sceneChangeHandler;
        sceneChangeHandler = null;
    }
}
