using System;

// TreasureChest와 ItemPedestal은 "이름에 상관없이 게임 안에서는 모두 보물상자로
// 취급한다"(v1.30)는 원칙에 따라 열기(Open) 개념을 공유하지만, 서로 다른
// 클래스(상호작용/충돌 판정 방식 자체가 다름)라 한쪽 클래스에 static 이벤트를 두면
// 다른 쪽에서 자연스럽지 않습니다. 그래서 "어떤 보물상자든 방금 새로 열렸다"는 신호를
// 두 클래스 모두가 발행할 수 있는 별도의 static 클래스로 뽑아냈습니다
// (RoomController.OnAnyRoomEntered와 동일한 패턴).
//
// [48번 아이템(지연형 일회용 - 다음 상자 가중치 뒤섞기)이 이 이벤트를 쓰는 이유]
// 48번을 사용(Q)하면 BeginPending()이 이 이벤트를 구독해두고, 정확히 "다음으로 열리는
// 보물상자 아무거나 하나"에서 NextItemPickWeightOverride를 무장시킨 뒤 스스로 구독을
// 해지합니다. TreasureChest.Open()/ItemPedestal.Open()은 실제 내용물을 확정하기 전에
// (=콘텐츠 리졸버를 실행하거나 PickWeighted를 호출하기 전에) 반드시 RaiseOpened()를
// 먼저 호출해야, 48번의 핸들러가 무장을 마친 뒤에 뽑기가 일어나는 순서가 보장됩니다.
public static class ContainerOpenEvents
{
    public static event Action OnAnyContainerOpened;

    public static void RaiseOpened()
    {
        OnAnyContainerOpened?.Invoke();
    }
}
