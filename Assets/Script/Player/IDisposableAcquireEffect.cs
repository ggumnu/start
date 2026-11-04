// [일회용 아이템 "획득 시" 효과 - 선택 약속]
// IDisposableItemEffect는 "사용(Q)"만 다루고, 음식(IFoodEffect.OnAcquired)처럼 슬롯에 새로 들어오는 순간 호출되는 자리가
// 없습니다. 획득만 해도 무언가 주는 일회용(17번 - 획득 시 레벨 최초 한정 최대 체력 +1)은 이 인터페이스를 함께 구현하면,
// PlayerDisposableItem.TryAcquireItem()이 새 아이템을 슬롯에 넣는 바로 그 순간(최초 장착이든 교체로 들어오든) OnAcquired()를
// 한 번 호출합니다. 구현하지 않은 기존 일회용 효과(22/25/28/46/48번 등)는 아무 영향이 없습니다.
//
// [레벨당 1회 - 교체 반복 방지]
// 일회용도 보물상자에서 교체를 반복하면 같은 아이템을 여러 번 장착할 수 있으므로, 획득 시 무언가 지급한다면
// context.GameFlow.TryConsumeOneTimeGrant()로 먼저 게이트를 건 뒤에만 지급하세요(IFoodEffect.cs와 같은 규칙 - 같은
// 레벨에서는 한 번만, 다음 레벨로 넘어가면 다시 받을 수 있음).
public interface IDisposableAcquireEffect
{
    // 이 일회용 아이템이 슬롯에 새로 장착되는 순간 정확히 1번 호출됩니다(사용(Q)과는 별개의 시점).
    void OnAcquired(DisposableEffectContext context, string sourceItemId);
}
