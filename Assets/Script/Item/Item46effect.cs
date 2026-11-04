using System.Linq;

// 일회용 아이템 46번 - "사용하면 이번 레벨의 보스방으로 즉시 텔레포트하고 그 즉시 소멸".
//
// [기획]
// - 즉시형 일회용 아이템입니다 - Q를 누르면 그 즉시 텔레포트가 일어나고 아이템은 바로
//   소멸합니다(대기 상태 없음).
// - 텔레포트 대상은 이번 레벨의 RoomCategory.Boss(보스방)입니다 - 레벨당 보스방은 보통
//   하나뿐이지만, 혹시 여러 개인 레벨이 있어도 22번(동아리방 텔레포트)과 동일하게 그중
//   하나를 무작위로 고르도록 구현했습니다(TryTeleportPlayerToRandomRoom이 원래 여러 개를
//   받을 수 있는 구조라 카테고리별 개수를 이 클래스가 신경 쓸 필요가 없습니다).
// - 도착 위치/게이트 잠금 등은 걸어서 들어갈 때와 완전히 동일하게 처리됩니다
//   (LevelBuilder.TryTeleportPlayerToRandomRoom이 도착 즉시 RoomController.EnterRoom()을
//   호출합니다) - 즉, 그 보스방이 아직 클리어 전이면 평소처럼 즉시 게이트가 잠기고, 이미
//   클리어된 보스방이면 잠기지 않고 그냥 들어갑니다(클리어 여부는 CanUse 판정과 무관합니다
//   - 이미 클리어한 보스방이어도 사용할 수 있습니다).
// - [v1.38 추가 - CanUse 게이트] n.5 선택방 레벨처럼 보스방 자체가 없는 레벨에서는 아예
//   사용할 수 없습니다(Q를 눌러도 아무 일도 일어나지 않고 아이템도 소모되지 않습니다 -
//   PlayerDisposableItem.TryUse()의 CanUse 검사 참고). 22번과 동일한 기준입니다.
//
// [구현 - LevelBuilder.TryTeleportPlayerToRandomRoom / GeneratedRooms]
// Item22effect(TeleportToClubEffect)와 완전히 동일한 구조입니다 - 카테고리만
// RoomCategory.Club 대신 RoomCategory.Boss로 바꿨습니다. 실제 방 목록 조회/위치 계산/
// CharacterController를 안전하게 끄고 옮기는 처리는 전부 LevelBuilder 쪽 공용 메서드가
// 담당합니다.
//
// [아이템 에셋 설정]
// ItemDefinition: Item Id = 46, Category = Disposable, Disposable Effect =
// TeleportToBossEffect.
[System.Serializable]
public class TeleportToBossEffect : IDisposableItemEffect
{
    public bool IsImmediate => true;

    // [CanUse 게이트] 이번 레벨에 보스방이 하나도 없으면 사용할 수 없습니다. 이미 그
    // 보스방을 클리어했는지는 상관없습니다 - 방 자체의 존재 여부만 봅니다(GeneratedRooms는
    // isCleared와 무관하게 이번 레벨에 생성된 방 전체를 담고 있습니다).
    public bool CanUse(DisposableEffectContext context)
    {
        var rooms = context?.LevelBuilder?.GeneratedRooms;
        return rooms != null && rooms.Any(r => r != null && r.category == RoomCategory.Boss);
    }

    public void ActivateImmediate(DisposableEffectContext context, string sourceItemId)
    {
        if (context?.LevelBuilder == null)
        {
            UnityEngine.Debug.LogWarning("Item46effect: LevelBuilder를 찾지 못해 텔레포트하지 못했습니다.");
            return;
        }

        bool teleported = context.LevelBuilder.TryTeleportPlayerToRandomRoom(RoomCategory.Boss);
        if (!teleported)
        {
            UnityEngine.Debug.LogWarning("Item46effect: 이 레벨에 보스방이 없어 텔레포트하지 못했습니다.");
        }
    }

    // 즉시형이라 호출되지 않습니다.
    public void BeginPending(DisposableEffectContext context, string sourceItemId) { }

    // 즉시형이라 호출되지 않습니다.
    public void CancelPending(DisposableEffectContext context, string sourceItemId) { }
}
