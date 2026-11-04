using System.Linq;

// 일회용 아이템 22번 - "사용하면 레벨 내의 무작위 동아리방 근처로 즉시 텔레포트하고 그
// 즉시 소멸".
//
// [기획]
// - 즉시형 일회용 아이템입니다 - Q를 누르면 그 즉시 텔레포트가 일어나고 아이템은 바로
//   소멸합니다(대기 상태 없음).
// - 텔레포트 대상은 "이번 레벨에 있는 RoomCategory.Club(동아리방) 방들을 전부 모은 뒤
//   그중 하나를 무작위로 고른다"입니다 - 동아리방이 여러 개면 그중 하나를 균등하게
//   뽑습니다. (v1.32 변경 - 원래는 RoomCategory.Store(편의점)도 후보에 포함했지만,
//   동아리방으로만 한정하기로 했습니다.)
// - 도착 위치는 그 방의 "앵커(다른 방과 이어지는 연결 지점)" 중 하나를 무작위로 골라, 그
//   앵커가 가리키는 문 방향에서 방 안쪽으로 살짝 들어온 지점입니다(문 경계에 딱 걸치지
//   않도록). 앵커가 없는 방(이론상 거의 없음)은 그 방의 첫 칸 중심으로 대체합니다.
//
// [안 가본 동아리방 우선 - 형빈 확정]
// 위 [기획]의 "균등하게 뽑는다"를 바꿨습니다: 아직 한 번도 들어가지 않은 동아리방이 있으면 그중에서만 무작위로
// 고르고(예: 3개 중 2개를 다녀왔으면 남은 1개로, 1개만 다녀왔으면 나머지 2개 중 하나로), 전부 다녀왔을 때만 전체
// 동아리방 중에서 고릅니다. "다녀왔다" = 그 방에 한 번이라도 들어갔다(RoomInstance.visited - 미니맵의 방문 표시와
// 같은 기준). LevelBuilder.TryTeleportPlayerToRandomRoom(preferUnvisited: true, ...)로 처리합니다.
//
// [구현 - LevelBuilder.TryTeleportPlayerToRandomRoom]
// 실제 방 목록 조회/위치 계산/CharacterController를 안전하게 끄고 옮기는 처리는 모두
// LevelBuilder 쪽에 새로 추가한 공용 메서드에 맡깁니다(레벨 전환 시 플레이어를 옮기는
// RelocatePlayer()를 그대로 재사용) - 이 클래스는 "어떤 카테고리로 보낼지"만 알면 됩니다.
// TryTeleportPlayerToRandomRoom(params RoomCategory[])이 여러 카테고리를 받을 수 있는
// 구조라, 나중에 다시 다른 카테고리를 후보에 추가하고 싶어지면 이 클래스만 고치면 됩니다.
//
// [클래스 이름 - v1.33에서 TeleportToShopOrClubEffect -> TeleportToClubEffect로 변경]
// 상점이 후보였던 예전 기획의 흔적인 이름을 동아리방 전용으로 정리했습니다. 이 타입은
// ItemDefinition 에셋의 [SerializeReference] 필드에 타입 이름 그대로 직렬화되어 있어서,
// 그냥 이름만 바꾸면 기존 22번 아이템 에셋이 이 효과를 잃어버립니다. 그래서 아래
// [MovedFrom] 특성으로 "이 타입은 예전에 TeleportToShopOrClubEffect라는 이름이었다"를
// 알려줬습니다 - Unity가 이걸 보고 기존에 저장된 참조를 새 이름으로 자동 연결해줍니다.
// 유니티 에디터에서 다시 컴파일한 뒤, 22번 아이템 에셋의 Disposable Effect 드롭다운이
// 여전히 이 효과로 정상 표시되는지 한 번 확인해 주세요(혹시 비어 보이면 Reimport).
[System.Serializable]
[UnityEngine.Scripting.APIUpdating.MovedFrom(false, null, null, "TeleportToShopOrClubEffect")]
public class TeleportToClubEffect : IDisposableItemEffect
{
    public bool IsImmediate => true;

    // [CanUse 게이트] 이번 레벨에 동아리방이 하나도 없으면 사용할 수 없습니다. 이미 그
    // 동아리방 보물상자를 획득했는지(=열었는지)는 상관없습니다 - 방 자체의 존재 여부만
    // 봅니다(GeneratedRooms는 isCleared/보물상자 획득 여부와 무관하게 이번 레벨에 생성된
    // 방 전체를 담고 있습니다).
    public bool CanUse(DisposableEffectContext context)
    {
        var rooms = context?.LevelBuilder?.GeneratedRooms;
        return rooms != null && rooms.Any(r => r != null && r.category == RoomCategory.Club);
    }

    public void ActivateImmediate(DisposableEffectContext context, string sourceItemId)
    {
        if (context?.LevelBuilder == null)
        {
            UnityEngine.Debug.LogWarning("Item22effect: LevelBuilder를 찾지 못해 텔레포트하지 못했습니다.");
            return;
        }

        // 안 가본 동아리방 우선(전부 다녀왔으면 전체 중에서)
        bool teleported = context.LevelBuilder.TryTeleportPlayerToRandomRoom(true, RoomCategory.Club);
        if (!teleported)
        {
            UnityEngine.Debug.LogWarning("Item22effect: 이 레벨에 동아리방이 하나도 없어 텔레포트하지 못했습니다.");
        }
    }

    // 즉시형이라 호출되지 않습니다.
    public void BeginPending(DisposableEffectContext context, string sourceItemId) { }

    // 즉시형이라 호출되지 않습니다.
    public void CancelPending(DisposableEffectContext context, string sourceItemId) { }
}
