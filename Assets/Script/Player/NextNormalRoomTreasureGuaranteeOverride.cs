// 40번 음식("사용하면 지연 소모 상태가 되고, 다음 일반방을 클리어하면 그 방에 반드시
// 일반맵 보물상자가 등장한 뒤 소모된다") 전용 플래그입니다. NextItemPickWeightOverride
// (48번)/NextClubPedestalLegendaryOverride(27번)와 완전히 동일한 구조입니다.
//
// [스코프 - "다음에 클리어되는 일반방 하나"의 TrySpawnTreasure() 호출 한 번 동안만 유효]
// ArmForThisRoom()으로 무장하면, RoomController.HandleCleared()가 그 직후(같은 호출
// 안에서) 실행하는 NormalRoomTreasureSpawner.TrySpawnTreasure()가 등장 확률 굴림
// (Random.value > chance)을 건너뛰고 반드시 보물상자를 스폰합니다. 그 TrySpawnTreasure()
// 호출이 시작되는 순간 곧바로 ClearAfterThisRoom()이 호출되어 무장이 풀립니다 - 그래야
// 그 다음 일반방에는 영향이 새지 않습니다("이번 일반방 클리어 1회"라는 무장의 유효
// 범위는 TrySpawnTreasure() 호출이 시작된 시점에 이미 소모된 것으로 처리합니다).
//
// [무장 시점]
// Item40effect(GuaranteedNormalRoomTreasureFoodEffect).BeginPending()이 구독한
// RoomController.OnAnyRoomCleared 핸들러가 발동하는 시점(= 그 방의 HandleCleared() 안에서
// TrySpawnTreasure()가 호출되기 "직전")에 동기적으로 ArmForThisRoom()을 호출합니다.
// HandleCleared()가 OnAnyRoomCleared를 TrySpawnTreasure()보다 먼저 호출하도록 되어 있어
// 순서가 항상 보장됩니다. 핸들러는 RoomCategory.Normal인 방에서만 반응합니다 - 이
// 이벤트는 보스방 클리어에서도 발생하므로, 보스방이 먼저 클리어되어도 무장되지 않고
// 계속 다음 일반방을 기다립니다.
public static class NextNormalRoomTreasureGuaranteeOverride
{
    public static bool IsArmed { get; private set; }

    public static void ArmForThisRoom()
    {
        IsArmed = true;
    }

    public static void ClearAfterThisRoom()
    {
        IsArmed = false;
    }
}
