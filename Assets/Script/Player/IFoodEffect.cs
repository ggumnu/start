// 음식(Food)이 실제로 어떤 효과를 일으키는지를 표현하는 인터페이스입니다. IDisposableItemEffect와
// 거의 동일한 구조입니다 - 음식은 일회용과 "사용 방식"이 완전히 같기 때문입니다(즉시형/지연형
// 구분, 대기 중 교체 시 취소 등). 다른 점은 딱 하나, 획득(장착) 시점에 호출되는 OnAcquired()가
// 추가로 있다는 것뿐입니다.
//
// [사용 방식 - IsImmediate/ActivateImmediate/BeginPending/CancelPending]
// IDisposableItemEffect와 완전히 동일한 계약입니다. 자세한 설명은 IDisposableItemEffect.cs 상단
// 주석을 참고하세요 - PlayerFoodItem이 PlayerDisposableItem과 동일한 방식으로 이 세 메서드를
// 호출합니다(사용 키만 Q 대신 E).
//
// [획득 시 1회성 효과 - OnAcquired]
// 일회용은 "사용(E)"해야만 효과가 일어나지만, 음식 중에는 "얻기만 해도" 발동하는 효과(예:
// 34번 - 획득 시 최초 한정으로 최대 체력 증가)가 있습니다. 이런 효과는 PlayerFoodItem.
// TryAcquireItem()이 이 음식을 슬롯에 새로 장착하는 바로 그 순간(최초 장착이든, 다른 음식과
// 교체로 새로 들어오든) 호출합니다 - ITrinketEffect.Apply()와 동일한 시점/성격입니다.
//
// [OnAcquired와 "최초 한정" - 레벨당 1회]
// 음식도 일회용/장신구와 마찬가지로 보물상자에서 "교체"를 반복하면 같은 음식을 여러 번
// 장착할 수 있습니다. OnAcquired()가 매번 뭔가를 지급하면 부정하게 여러 번 받을 수 있으므로,
// ITrinketEffect.cs와 동일하게 context.GameFlow.TryConsumeOneTimeGrant()로 먼저 게이트를 건
// 뒤에만 실제로 지급하세요(레벨이 바뀔 때만 초기화되므로, 같은 레벨 안에서는 중복 지급을
// 막고 다른 레벨에서는 다시 받을 수 있습니다).
//
// [OnAcquired에 대응하는 "해제"가 없는 이유]
// 장신구(ITrinketEffect)는 소유하는 동안 유지되는 효과라 교체 시 Remove()로 반드시 되돌려야
// 하지만, 음식의 OnAcquired 1회성 지급은 "받았다"는 사실 자체가 남아야 하는 성격이라(예:
// FixedDamageTrinketEffect의 최초 회복과 동일한 성격) 되돌릴 필요가 없습니다 - 그래서 Remove
// 대응 메서드를 따로 두지 않았습니다.
//
// [새 구현체를 만들 때 지켜야 할 것 - IDisposableItemEffect와 동일한 규칙]
// - 클래스 선언 위에 [System.Serializable]을 붙일 것.
// - 매개변수 없는 생성자를 가질 것(필드 + 기본값만으로 구성) - FoodEffectDrawer.cs가
//   인스펙터 드롭다운에 그 타입을 보여주려면 필요합니다.
public interface IFoodEffect
{
    // 이 음식이 슬롯에 새로 장착되는 순간(최초 장착 또는 다른 음식과 교체로 새로 들어올 때)
    // 정확히 1번 호출됩니다. 즉시형/지연형 여부와 무관하게 항상 호출됩니다 - "사용(E)"과는
    // 완전히 별개의 시점입니다.
    void OnAcquired(FoodEffectContext context, string sourceItemId);

    // [9번 음식 연동 - CanUse 게이트] 이 음식을 지금 사용할 수 있는지 미리 확인합니다. false를
    // 반환하면 PlayerFoodItem.TryUse()가 ActivateImmediate/BeginPending을 아예 호출하지 않고,
    // 음식도 소모되지 않습니다(대기 상태로도 들어가지 않습니다). 예를 들어 9번("사용하면 지금
    // 방의 보물상자가 복제된다")은 그 방에 복제할 보물상자가 아예 없으면 이 메서드가 false를
    // 반환해 "사용 불가"를 표현합니다. 대부분의 음식은 항상 사용 가능하므로 true만 반환하면
    // 됩니다.
    bool CanUse(FoodEffectContext context);

    // true면 즉시형(ActivateImmediate만 쓰임), false면 지연형(BeginPending/CancelPending이 쓰임).
    bool IsImmediate { get; }

    // 즉시형 전용. E 입력 즉시 정확히 1번 호출됩니다. 효과를 바로 일으키면 됩니다 - 반환값이
    // 따로 없는 이유는 호출한 쪽(PlayerFoodItem)이 IsImmediate가 true인 걸 이미 알고 있어서,
    // 이 호출 직후 항상 곧바로 소멸 처리를 진행하기 때문입니다.
    void ActivateImmediate(FoodEffectContext context, string sourceItemId);

    // 지연형 전용. E 입력 즉시 호출됩니다. 여기서는 실제 발동 트리거만 구독해두고, 트리거가
    // 오면 그 콜백 안에서 context.CompletePendingUse(sourceItemId)를 호출하세요.
    void BeginPending(FoodEffectContext context, string sourceItemId);

    // 지연형 전용. 대기 중(BeginPending 이후, 아직 트리거가 발생하기 전)에 다른 음식으로
    // 교체되어 대기가 취소될 때 호출됩니다. BeginPending()에서 건 구독을 반드시 해지하세요 -
    // 효과는 발동시키지 않습니다.
    void CancelPending(FoodEffectContext context, string sourceItemId);
}
