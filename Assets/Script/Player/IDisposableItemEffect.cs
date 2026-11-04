// 일회용 아이템(Disposable)이 Q로 "사용"됐을 때 실제로 어떤 효과를 일으키는지를 표현하는
// 인터페이스입니다. IActiveItemEffect와 짝을 이루는 구조지만, 일회용 아이템은 사용 후
// 곧바로 소멸한다는 점이 다르고, 그중에서도 두 가지 하위 방식으로 나뉩니다.
//
// [즉시형 (IsImmediate = true)]
// Q를 누르는 즉시 ActivateImmediate()가 호출되어 효과가 바로 일어나고, 그 즉시 아이템도
// 소멸합니다(PlayerDisposableItem.TryUse() 참고). BeginPending()/CancelPending()은 호출되지
// 않으므로 즉시형 구현체는 두 메서드를 빈 본문으로 둬도 됩니다.
//
// [지연형 (IsImmediate = false)]
// Q를 누르면 BeginPending()만 호출됩니다 - 여기서 "실제 발동 트리거"를 구독해두기만 하고,
// 아이템은 아직 소멸하지 않고 인벤토리에 남습니다("사용은 했지만 대기 중"인 상태). 트리거가
// 실제로 발생하면, 그 구현체가 직접 context.CompletePendingUse(sourceItemId)를 호출해
// PlayerDisposableItem에게 "지금 효과를 발동하고 소멸시켜도 된다"고 알려야 합니다. 대기
// 중에 다른 일회용 아이템으로 교체되면 CompletePendingUse가 호출되기 전에 CancelPending()이
// 먼저 호출됩니다 - BeginPending()에서 건 구독을 여기서 반드시 해지해야 합니다(안 그러면
// 이미 교체된 뒤에도 몰래 트리거가 발동해 버립니다).
//
// [22/46번 연동 - CanUse 게이트]
// IFoodEffect.CanUse()와 완전히 동일한 취지로 추가했습니다. 22번("무작위 동아리방으로
// 텔레포트")/46번("보스방으로 텔레포트")처럼 "이동할 대상 방 자체가 레벨에 없으면 애초에
// 사용할 수 없어야 하는" 효과를 위한 게이트입니다. false를 반환하면
// PlayerDisposableItem.TryUse()가 ActivateImmediate/BeginPending을 아예 호출하지 않고,
// 아이템도 소모되지 않습니다(대기 상태로도 들어가지 않습니다). 대부분의 일회용 아이템은
// 항상 사용 가능하므로 true만 반환하면 됩니다.
//
// [새 구현체를 만들 때 지켜야 할 것 - IActiveItemEffect/ITrinketEffect와 동일한 규칙]
// - 클래스 선언 위에 [System.Serializable]을 붙일 것.
// - 매개변수 없는 생성자를 가질 것(필드 + 기본값만으로 구성) - DisposableItemEffectDrawer.cs가
//   인스펙터 드롭다운에 그 타입을 보여주려면 필요합니다.
public interface IDisposableItemEffect
{
    // true면 즉시형(ActivateImmediate만 쓰임), false면 지연형(BeginPending/CancelPending이 쓰임).
    bool IsImmediate { get; }

    // 이 일회용 아이템을 지금 사용할 수 있는지 미리 확인합니다. false를 반환하면
    // PlayerDisposableItem.TryUse()가 ActivateImmediate/BeginPending을 아예 호출하지 않고,
    // 아이템도 소모되지 않습니다(대기 상태로도 들어가지 않습니다). 대부분의 일회용
    // 아이템은 항상 사용 가능하므로 true만 반환하면 됩니다.
    bool CanUse(DisposableEffectContext context);

    // 즉시형 전용. Q 입력 즉시 정확히 1번 호출됩니다. 효과를 바로 일으키면 됩니다 - 반환값이
    // 따로 없는 이유는 호출한 쪽(PlayerDisposableItem)이 IsImmediate가 true인 걸 이미 알고
    // 있어서, 이 호출 직후 항상 곧바로 소멸 처리를 진행하기 때문입니다.
    void ActivateImmediate(DisposableEffectContext context, string sourceItemId);

    // 지연형 전용. Q 입력 즉시 호출됩니다. 여기서는 실제 발동 트리거만 구독해두고, 트리거가
    // 오면 그 콜백 안에서 context.CompletePendingUse(sourceItemId)를 호출하세요.
    void BeginPending(DisposableEffectContext context, string sourceItemId);

    // 지연형 전용. 대기 중(BeginPending 이후, 아직 트리거가 발생하기 전)에 다른 일회용
    // 아이템으로 교체되어 대기가 취소될 때 호출됩니다. BeginPending()에서 건 구독을 반드시
    // 해지하세요 - 효과는 발동시키지 않습니다.
    void CancelPending(DisposableEffectContext context, string sourceItemId);
}
