// 지연 소모(대기) 상태를 가질 수 있는 액티브 아이템 효과를 위한 선택형 인터페이스입니다.
// IActiveItemEffect를 확장하므로, 이 인터페이스를 구현한 효과도 평소처럼 Activate()를 가집니다.
// 기존 액티브 효과들은 이 인터페이스를 구현하지 않아도 되고, 구현하지 않으면 PlayerActiveItem의
// 동작은 예전과 완전히 같습니다(PlayerActiveItem이 "이 효과가 이 인터페이스를 구현했는지"를
// 확인해서 구현한 경우에만 아래 두 메서드를 호출합니다).
//
// [사용 방식]
// - CanActivate: 스페이스 입력 시 코스트를 내기 "전에" 호출됩니다. false를 돌려주면 코스트도
//   소모하지 않고 사용도 일어나지 않습니다(예: 93번 - 직전에 먹은 음식이 없을 때).
// - Activate(IActiveItemEffect에서 물려받음): 코스트 지불에 성공한 뒤 호출됩니다. 지연 소모
//   상태로 들어가야 하는 효과라면 이 안에서 context.EnterPending()을 부르세요. 그러면
//   PlayerActiveItem이 그 상태를 기억하고, 그 동안 스페이스 입력을 받지 않으며 HUD의 액티브
//   아이콘이 반투명해집니다. 실제로 끝나는 순간에는 context.CompletePendingUse()를 부르세요.
// - CancelPending: 대기 중에 다른 액티브 아이템으로 교체되어 대기가 취소될 때 호출됩니다.
//   Activate에서 걸어둔 구독 등을 반드시 해제하세요 - 효과는 발동시키지 않습니다. 이미 낸
//   코스트는 돌려받지 않습니다.
//
// [새 구현체를 만들 때 지켜야 할 것 - IActiveItemEffect와 동일한 규칙]
// - 클래스 선언 위에 [System.Serializable]을 붙일 것.
// - 매개변수 없는 생성자를 가질 것(필드 + 기본값만으로 구성).
public interface IDelayedActiveItemEffect : IActiveItemEffect
{
    bool CanActivate(ActiveEffectContext context);

    void CancelPending(ActiveEffectContext context);
}
