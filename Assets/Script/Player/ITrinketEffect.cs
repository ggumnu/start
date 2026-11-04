// 장신구가 플레이어에게 실제로 어떤 효과를 가하는지를 표현하는 인터페이스입니다.
// IPassiveItemEffect/IActiveItemEffect와 짝을 이루는 구조이지만, 장신구는 "패시브처럼
// 소유하는 동안 효과가 유지되지만, 액티브처럼 한 개만 장착 가능하고 교체될 수 있다"는
// 특성이 있어 둘과는 다른 생애주기를 가집니다.
//
// - Apply()는 이 장신구를 장착하는 순간(최초 획득 또는 다른 장신구와 교체로 새로 들어올
//   때) 호출됩니다.
// - Remove()는 이 장신구가 장착 해제되는 순간(다른 장신구로 교체되어 빠질 때) 호출됩니다.
//   Apply()에서 건 효과(스탯 수정자, 훅 등)를 여기서 반드시 되돌려야 합니다 - 그렇지 않으면
//   장신구를 바꿔도 이전 효과가 영구히 남습니다.
//
// [새 구현체를 만들 때 지켜야 할 것 - IPassiveItemEffect와 동일한 규칙]
// - 클래스 선언 위에 [System.Serializable]을 붙일 것.
// - 매개변수 없는 생성자를 가질 것(필드 + 기본값만으로 구성) - TrinketEffectDrawer.cs가
//   인스펙터 드롭다운에 그 타입을 보여주려면 필요합니다.
//
// [획득 시 1회성 지급(스탯/재화 등)이 있는 경우]
// 장신구는 교체가 가능하므로, Apply()가 매번 뭔가를 지급하면 같은 레벨에서 교체를 반복해
// 부정하게 여러 번 받을 수 있습니다. 이런 지급은 context.GameFlow.TryConsumeOneTimeGrant()로
// 먼저 게이트를 건 뒤에만 실제로 지급하세요(GameFlowManager.cs 참고 - 레벨이 바뀔 때만
// 초기화되므로 같은 레벨 안에서는 중복 지급을 막고, 다른 레벨에서는 다시 받을 수 있습니다).
public interface ITrinketEffect
{
    void Apply(TrinketEffectContext context, string sourceItemId);
    void Remove(TrinketEffectContext context, string sourceItemId);
}
