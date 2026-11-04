// 액티브 아이템 효과가 "쿨타임 / 이번 사용의 코스트 / 지금 사용 가능 여부"를 PlayerActiveItem에 알려주는 선택형 인터페이스입니다.
// IActiveItemEffect를 확장하므로, 이 인터페이스를 구현한 효과도 평소처럼 Activate()를 가집니다. 기존 액티브 효과들은 이
// 인터페이스를 구현하지 않으므로 PlayerActiveItem의 동작이 예전과 완전히 같습니다(IDelayedActiveItemEffect와 같은 방식 -
// PlayerActiveItem이 "이 효과가 이 인터페이스를 구현했는지"를 확인해서 구현한 경우에만 아래 멤버를 씁니다).
//
// [사용 방식]
// - CanActivateNow: 스페이스 입력 시 코스트를 내기 "전에" 호출됩니다. false면 코스트도 소모하지 않고 사용도 일어나지 않습니다
//   (예: 74번 - 쿨타임 중, 사망, 로딩 화면 중 등). Activate()에서 실패할 수 있는 조건은 전부 여기서 먼저 걸러 주세요
//   (Activate()가 불릴 때는 이미 코스트를 낸 뒤입니다).
// - ResolveUseCost: 이번에 실제로 낼 코스트입니다. effectiveCost는 45번 등 감소가 반영된 값(PlayerActiveItem.GetEffectiveCost)이고,
//   그대로 돌려주면 평소와 같습니다. 0 이하를 돌려주면 코스트를 내지 않고 사용합니다(예: 74번 - 전투 중이 아닐 때 무료).
//   좌상단 소모 코스트 숫자(PlayerCostDisplay)도 이 값을 그대로 보여줍니다(매 프레임 호출되므로 가볍게 만들 것).
// - CooldownRemaining01: 남은 쿨타임 비율(1 = 막 사용함, 0 = 사용 가능)입니다. 좌상단 액티브 아이콘의 쿨타임 연출에 씁니다.
//   쿨타임이 없는 효과는 항상 0을 돌려주면 됩니다.
//
// [새 구현체를 만들 때 지켜야 할 것 - IActiveItemEffect와 동일한 규칙]
// - 클래스 선언 위에 [System.Serializable]을 붙일 것.
// - 매개변수 없는 생성자를 가질 것(필드 + 기본값만으로 구성).
public interface IActiveItemUseRules : IActiveItemEffect
{
    bool CanActivateNow(ActiveEffectContext context);

    int ResolveUseCost(ActiveEffectContext context, int effectiveCost);

    float CooldownRemaining01 { get; }
}
