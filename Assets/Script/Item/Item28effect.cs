using System.Linq;

// 일회용 아이템 28번 - "사용하면 이번 레벨의 상점으로 즉시 텔레포트하고 그 즉시 소멸".
//
// [기획 - 형빈 확정]
// - 즉시형 일회용 아이템입니다 - Q를 누르면 그 즉시 텔레포트가 일어나고 아이템은 바로 소멸합니다(대기 상태 없음).
// - 텔레포트 대상은 이번 레벨의 RoomCategory.Store(상점) 방입니다. 지금은 레벨당 상점이 하나뿐이지만, 나중에 2~3개로
//   늘어나면 22번(동아리방 텔레포트)과 똑같이 아직 한 번도 들어가지 않은 상점을 우선으로 고르고, 전부 다녀왔을 때만
//   전체 상점 중에서 무작위로 고릅니다("다녀왔다" = RoomInstance.visited - 미니맵 방문 표시와 같은 기준).
// - 도착 위치/게이트 처리 등은 걸어서 들어갈 때와 완전히 같습니다(LevelBuilder.TryTeleportPlayerToRandomRoom이 도착
//   즉시 RoomController.EnterRoom()을 호출). 클리어 전 전투방에서 사용하면 그 방은 22번/46번과 똑같이 다음에 다시
//   들어올 때 처음 상태로 초기화됩니다(RoomController의 전투 중 이탈 규칙).
// - [CanUse 게이트] n.5 선택방 레벨처럼 상점이 없는 레벨에서는 사용할 수 없습니다(Q를 눌러도 아무 일도 일어나지 않고
//   아이템도 소모되지 않음 - PlayerDisposableItem.TryUse()의 CanUse 검사). 22번/46번과 같은 기준입니다.
//
// [구현 - LevelBuilder.TryTeleportPlayerToRandomRoom / GeneratedRooms]
// Item22effect(TeleportToClubEffect)와 완전히 같은 구조이고 카테고리만 RoomCategory.Store로 바꿨습니다. 상점은 카테고리
// 하나(Store) 안에 편의점/카페 등 variant로 나뉘므로, variant와 상관없이 Store 방이면 전부 후보입니다.
//
// [아이템 에셋 설정]
// ItemDefinition: Item Id = 28, Category = Disposable, Disposable Effect = TeleportToShopEffect.
[System.Serializable]
public class TeleportToShopEffect : IDisposableItemEffect
{
    public bool IsImmediate => true;

    // [CanUse 게이트] 이번 레벨에 상점이 하나도 없으면 사용할 수 없습니다. 이미 그 상점에 가봤는지/물건을 샀는지는
    // 상관없습니다 - 방 자체의 존재 여부만 봅니다(GeneratedRooms는 이번 레벨에 생성된 방 전체를 담고 있습니다).
    public bool CanUse(DisposableEffectContext context)
    {
        var rooms = context?.LevelBuilder?.GeneratedRooms;
        return rooms != null && rooms.Any(r => r != null && r.category == RoomCategory.Store);
    }

    public void ActivateImmediate(DisposableEffectContext context, string sourceItemId)
    {
        if (context?.LevelBuilder == null)
        {
            UnityEngine.Debug.LogWarning("Item28effect: LevelBuilder를 찾지 못해 텔레포트하지 못했습니다.");
            return;
        }

        // 안 가본 상점 우선(전부 다녀왔으면 전체 중에서)
        bool teleported = context.LevelBuilder.TryTeleportPlayerToRandomRoom(true, RoomCategory.Store);
        if (!teleported)
        {
            UnityEngine.Debug.LogWarning("Item28effect: 이 레벨에 상점이 하나도 없어 텔레포트하지 못했습니다.");
        }
    }

    // 즉시형이라 호출되지 않습니다.
    public void BeginPending(DisposableEffectContext context, string sourceItemId) { }

    // 즉시형이라 호출되지 않습니다.
    public void CancelPending(DisposableEffectContext context, string sourceItemId) { }
}
