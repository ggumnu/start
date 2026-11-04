// 보물상자처럼 "열림" 상태를 갖는 오브젝트가 구현하는 최소 읽기 전용 인터페이스입니다.
//
// [왜 필요한가]
// 보물상자 종류마다 클래스가 다릅니다 - 동아리방은 ItemPedestal, 일반방/보스방은 TreasureChest.
// 뚜껑 열림 연출(ChestLidOpener)은 상자 종류와 상관없이 "지금 열렸는가"만 알면 되므로, 그 정보
// 하나만 이 인터페이스로 뽑아냈습니다(IBasicItemContainer와 같은 방식). 앞으로 새 상자 클래스를
// 추가할 때도 이 인터페이스만 구현하면 ChestLidOpener를 그대로 붙여 쓸 수 있습니다.
//
// IsOpened는 "열렸다"로 한 번 바뀌면 다시 닫히지 않는다고 가정합니다(지금의 두 상자 모두 그렇습니다).
public interface IOpenableContainer
{
    bool IsOpened { get; }
}
