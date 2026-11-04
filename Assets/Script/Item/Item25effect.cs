using System;
using UnityEngine;

// 일회용 25번 - "사용하면 지연 소모 상태가 되고, 그 상태에서 다음에 사용하는 음식은 효과는
// 그대로 발동하되 소모되지 않고 그대로 남아 다시 쓸 수 있다. 그 대신 25번 자신이 소모된다."
//
// [기획 - 즉시형/지연형 음식 공통]
// - 즉시형 음식(예: 34번)이 사용되면: 효과는 평소처럼 정확히 한 번 그대로 발동하고, 음식은
//   슬롯에서 사라지지 않고 그대로 남아 바로 다시 사용할 수 있습니다. 그 대신 25번이 소모됩니다.
// - 지연형 음식(예: 27번)이 사용되면: 음식은 평소처럼 정상적으로 자기 발동 조건을 기다립니다
//   (25번은 이 과정에 전혀 관여하지 않습니다). 그 조건이 실제로 충족되는 순간 효과는 평소처럼
//   정확히 발동하지만, 음식은 소모되지 않고 "대기 전(미사용)" 상태로 복구되어 슬롯에 남습니다
//   (다시 E를 눌러야 재사용됨). 그 대신 그 순간 25번이 소모됩니다.
//
// [구현 - PlayerFoodItem.ConsumptionRedirect]
// 즉시형/지연형 모두 실제 소멸은 PlayerFoodItem.ConsumeEquippedItem() 한 곳에서만 일어나므로
// (즉시형은 ActivateImmediate 직후, 지연형은 CompletePendingUse 안에서), 이 지점에 25번이
// 자기 콜백을 걸어두는 것만으로 즉시형/지연형을 구분할 필요 없이 동일하게 처리됩니다. 사용(Q)
// 즉시 이 콜백을 걸어두고, 다음번 음식 소모 시도 때 그 콜백이 호출되면 25번 자신을
// CompletePendingUse로 소모시키고 true를 반환해 음식의 소모를 막습니다.
//
// [대기 중 다른 일회용으로 교체]
// 아직 콜백이 호출되지 않은 상태(음식이 아직 사용되지 않았거나, 지연형 음식이 아직 발동
// 전)에서 다른 일회용 아이템으로 교체되면 CancelPending()에서 걸어둔 콜백을 해제합니다 -
// 이후에 음식이 소모되어도 25번과는 더 이상 무관하게 평소대로 소모됩니다.
[Serializable]
public class DelayFoodPreserveEffect : IDisposableItemEffect
{
    private PlayerFoodItem foodItem;
    private DisposableEffectContext pendingContext;
    private string pendingSourceId;

    // 25번 자신은 지연형입니다 - 사용(Q) 즉시 소모되지 않고, 다음 음식 사용을 기다립니다.
    public bool IsImmediate => false;

    // [22/46번 연동으로 추가된 CanUse 게이트] 25번은 항상 사용할 수 있습니다.
    public bool CanUse(DisposableEffectContext context) => true;

    // 지연형이라 실제로는 호출되지 않지만, 인터페이스 계약상 빈 본문을 남겨둡니다.
    public void ActivateImmediate(DisposableEffectContext context, string sourceItemId)
    {
    }

    public void BeginPending(DisposableEffectContext context, string sourceItemId)
    {
        var food = context?.PlayerObject != null ? context.PlayerObject.GetComponent<PlayerFoodItem>() : null;
        if (food == null)
        {
            Debug.LogWarning("DelayFoodPreserveEffect: PlayerFoodItem을 찾을 수 없어 효과를 적용하지 못했습니다.");
            context?.CompletePendingUse(sourceItemId);
            return;
        }

        foodItem = food;
        pendingContext = context;
        pendingSourceId = sourceItemId;

        foodItem.ConsumptionRedirect = OnNextFoodConsumptionAttempt;
    }

    // 다음 음식이 실제로 소모되려는 바로 그 시점(즉시형은 사용 직후, 지연형은 자기 발동 조건이
    // 충족된 순간)에 호출됩니다. 25번 자신을 대신 소모시키고, 음식은 소모하지 말라고 알립니다.
    private bool OnNextFoodConsumptionAttempt()
    {
        pendingContext?.CompletePendingUse(pendingSourceId);
        ClearPending();
        return true; // 음식은 소모하지 않습니다.
    }

    // 대기 중(콜백이 아직 호출되지 않은 상태)에 다른 일회용 아이템으로 교체되어 대기가
    // 취소될 때 호출됩니다. 걸어둔 콜백이 여전히 우리 것일 때만 해제합니다.
    public void CancelPending(DisposableEffectContext context, string sourceItemId)
    {
        if (foodItem != null && foodItem.ConsumptionRedirect == (Func<bool>)OnNextFoodConsumptionAttempt)
        {
            foodItem.ConsumptionRedirect = null;
        }
        ClearPending();
    }

    private void ClearPending()
    {
        foodItem = null;
        pendingContext = null;
        pendingSourceId = null;
    }
}
