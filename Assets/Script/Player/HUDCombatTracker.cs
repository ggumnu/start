using UnityEngine;

// [HUD 전투 상태 공용 부품] 스탯 창(PlayerStatDisplay)/슬롯 인벤토리(PlayerSlotItemDisplay)/패시브 인벤토리(PlayerInventoryDisplay)가
// 각자 하나씩 가지고 "지금 클리어 전 전투 방인지" + "전투 전환 진행값"을 얻습니다(설정 창의 전투 중 자동 반투명용).
// - 전투 방 = 일반방/보스방(RoomController.RequiresClear와 같은 기준) 중 아직 클리어 전. 지금 방은 RoomController.OnAnyRoomEntered로 기억합니다.
// - HUD 오브젝트가 씬마다 새로 만들어지므로 정적 값이 아니라 인스턴스로 둡니다(지난 씬의 방이 남지 않게).
// - 각 HUD의 OnEnable/OnDisable에서 Subscribe/Unsubscribe를 부르고, 매 프레임 UpdateBlend로 진행값을 받습니다.
public class HUDCombatTracker
{
    private RoomInstance currentRoom;
    private bool subscribed;
    private float blend;
    private bool blendInitialized;

    // 0 = 평소, 1 = 전투(그 사이 = 전환 중)
    public float Blend => blend;

    public void Subscribe()
    {
        if (subscribed) return;
        RoomController.OnAnyRoomEntered += HandleRoomEntered;
        subscribed = true;
    }

    public void Unsubscribe()
    {
        if (!subscribed) return;
        RoomController.OnAnyRoomEntered -= HandleRoomEntered;
        subscribed = false;
    }

    private void HandleRoomEntered(RoomInstance room)
    {
        currentRoom = room;
    }

    public bool InCombatRoom
    {
        get
        {
            if (currentRoom == null || currentRoom.isCleared) return false;
            return currentRoom.category == RoomCategory.Normal || currentRoom.category == RoomCategory.Boss;
        }
    }

    // 진행값을 duration초 동안 전투 상태 쪽으로 옮기고 돌려줍니다(실제 시간 기준 - 처음 한 번은 바로 맞춤).
    public float UpdateBlend(float duration)
    {
        float target = InCombatRoom ? 1f : 0f;
        if (!blendInitialized)
        {
            blend = target;
            blendInitialized = true;
            return blend;
        }
        float step = duration > 0f ? Time.unscaledDeltaTime / duration : 1f;
        blend = Mathf.MoveTowards(blend, target, step);
        return blend;
    }
}
