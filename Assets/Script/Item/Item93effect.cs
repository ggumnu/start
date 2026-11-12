using System;
using UnityEngine;

// 93번 액티브 아이템 효과 - 사용 시 "직전에 실제로 소모한 음식"의 효과를 한 번 더 재현합니다.
// (코스트는 ItemDefinition의 activeCost로 지정합니다 - 93번은 5.)
//
// [번호 정리 - 형빈 확정]
// 이 효과는 원래 13번 아이템이었고 93번으로 옮겼습니다(13번 번호는 새 음식 아이템이 씀 - Item13effect.cs).
// 효과 이름(ReplayLastFoodEffect)과 동작은 그대로라, 예전 13번 에셋의 이름/Item Id만 93으로 바꾸면 효과 연결이
// 그대로 유지됩니다.
//
// [재현 규칙]
// - 즉시형 음식이면 스페이스를 누르는 즉시 그 음식의 즉시 효과가 발동합니다.
// - 지연형 음식이면 이 액티브 아이템이 "대기 상태"로 들어가 HUD의 액티브 아이콘이 반투명해지고,
//   그 음식의 발동 조건이 충족되는 순간 효과가 발동한 뒤 대기가 풀립니다. 대기 중에는 이
//   액티브를 다시 사용할 수 없고, 다른 액티브 아이템으로 교체하면 대기가 취소됩니다(코스트는
//   돌려받지 않습니다).
// - 직전에 먹은 음식이 없으면 코스트를 소모하지 않고 사용도 되지 않습니다(CanActivate가 false).
// - 새 음식을 먹기 전까지는 코스트만 내면 몇 번이든 같은 음식 효과를 재현할 수 있습니다.
//
// [직전 음식의 기준]
// PlayerFoodItem.LastUsedFood - 음식이 "실제로 소모된 시점"에 기록됩니다(즉시형은 E를 누른
// 순간, 지연형은 효과가 발동해 소멸한 순간). 대기 중에 교체되어 취소된 음식은 기록되지 않습니다.
//
// [재현하는 것과 하지 않는 것]
// 음식의 "사용" 효과(즉시형이면 ActivateImmediate, 지연형이면 BeginPending)만 재현합니다.
// 획득 시 1회성 효과(OnAcquired)는 재현하지 않습니다.
//
// [사용 연출 - 형빈 확정]
// 재현한 음식의 아이콘을 머리 위에 띄웁니다(85번과 같음, ItemUsePopup.cs). 즉시형은 Space를 누른 순간, 지연형은 효과가 발동하는 순간.
// 대기 중 교체로 취소되면 띄우지 않습니다.
//
// [지연형 재현이 복제본으로 동작하는 이유]
// 지연형 음식 효과는 대기 상태(구독 핸들러 등)를 효과 객체 안에 들고 있습니다. 원본 객체를
// 그대로 쓰면 음식 슬롯의 대기 상태와 뒤섞일 수 있어, JsonUtility로 복제한 별도 객체로 재현합니다.
// (JsonUtility는 public 직렬화 필드만 복사하므로, 대기 상태 같은 private 필드는 새 객체에서
// 깨끗한 초기값으로 시작합니다.)
//
// [아이템 에셋 설정]
// ItemDefinition: Item Id = 93, Category = Active, Active Cost = 5, Active Effect = ReplayLastFoodEffect.
[Serializable]
public class ReplayLastFoodEffect : IDelayedActiveItemEffect, IHasOwnUseSound
{
    // 지금 대기 중인 재현 상태입니다(인스펙터 설정이 아니라 런타임 상태 - 직렬화되지 않습니다).
    // pendingEffect가 null이 아니면 "지연형 재현이 진행 중"이라는 뜻입니다.
    [NonSerialized] private IFoodEffect pendingEffect;
    [NonSerialized] private FoodEffectContext pendingFoodContext;
    [NonSerialized] private string pendingSourceId;
    [NonSerialized] private ItemDefinition pendingItem;   // 사용 연출용 - 재현 중인 음식
    [NonSerialized] private GameObject pendingPlayer;     // 사용 연출용 - 아이콘을 띄울 플레이어

    // 코스트를 내기 전에 호출됩니다. 재현할 음식이 있고, 지연형이라면 복제도 가능할 때만 true입니다.
    public bool CanActivate(ActiveEffectContext context)
    {
        PlayerFoodItem foodItem = GetFoodItem(context);
        if (foodItem == null) return false;

        ItemDefinition food = foodItem.LastUsedFood;
        if (food == null || food.foodEffect == null) return false;

        // 지연형은 복제에 실패하면 코스트만 나가고 아무 일도 없게 되므로, 미리 막습니다.
        if (!food.foodEffect.IsImmediate && CloneEffect(food.foodEffect) == null) return false;

        return true;
    }

    public void Activate(ActiveEffectContext context)
    {
        GameAudio.Play(SfxId.Active93); // [사운드] 전용 사용음(공용 발동음 대신)
        PlayerFoodItem foodItem = GetFoodItem(context);
        if (foodItem == null) return;

        ItemDefinition food = foodItem.LastUsedFood;
        if (food == null || food.foodEffect == null) return;

        IFoodEffect source = food.foodEffect;
        string sourceId = food.itemId;

        // 즉시형: 완료 신호가 필요 없으므로 아무 일도 하지 않는 콜백을 연결하고 바로 발동합니다.
        if (source.IsImmediate)
        {
            source.ActivateImmediate(foodItem.CreateReplayContext(_ => { }), sourceId);
            ItemUsePopup.Show(foodItem.gameObject, food); // [사용 연출] 재현한 음식 아이콘
            return;
        }

        // 지연형: 복제본으로 대기에 들어갑니다.
        IFoodEffect clone = CloneEffect(source);
        if (clone == null) return;

        pendingEffect = clone;
        pendingSourceId = sourceId;
        pendingItem = food;
        pendingPlayer = foodItem.gameObject;
        pendingFoodContext = foodItem.CreateReplayContext(_ => OnReplayCompleted(context, clone));

        // 대기 진입을 먼저 알립니다 - BeginPending 안에서 곧바로 완료 신호가 오더라도
        // (대기 해제가 대기 진입보다 먼저 일어나 아이콘이 반투명인 채로 남는 일이 없도록) 순서를 지킵니다.
        context.EnterPending();
        clone.BeginPending(pendingFoodContext, sourceId);
    }

    // 대기 중에 다른 액티브 아이템으로 교체될 때 호출됩니다. 효과는 발동시키지 않습니다.
    public void CancelPending(ActiveEffectContext context)
    {
        IFoodEffect effect = pendingEffect;
        FoodEffectContext foodContext = pendingFoodContext;
        string sourceId = pendingSourceId;
        if (effect == null) return;

        // 상태부터 비웁니다 - 취소 도중 완료 신호가 와도 무시되도록.
        ClearPending();
        effect.CancelPending(foodContext, sourceId);
    }

    // 복제된 지연형 효과가 발동을 마쳤을 때(완료 신호) 호출됩니다.
    private void OnReplayCompleted(ActiveEffectContext context, IFoodEffect completed)
    {
        // 이미 취소되었거나 다른 재현으로 바뀐 뒤에 온 신호는 무시합니다.
        if (!ReferenceEquals(pendingEffect, completed)) return;

        // [사용 연출] 지연형 음식이 발동한 순간 그 아이콘을 띄웁니다(ClearPending 전에 값을 받아 둠).
        ItemDefinition replayedItem = pendingItem;
        GameObject player = pendingPlayer;

        ClearPending();
        context.CompletePendingUse();
        ItemUsePopup.Show(player, replayedItem);
    }

    private void ClearPending()
    {
        pendingEffect = null;
        pendingFoodContext = null;
        pendingSourceId = null;
        pendingItem = null;
        pendingPlayer = null;
    }

    private static PlayerFoodItem GetFoodItem(ActiveEffectContext context)
    {
        if (context == null || context.PlayerObject == null) return null;
        return context.PlayerObject.GetComponent<PlayerFoodItem>();
    }

    // 음식 효과 객체를 JsonUtility로 복제합니다. 실패하면 null을 돌려줍니다.
    private static IFoodEffect CloneEffect(IFoodEffect source)
    {
        if (source == null) return null;

        try
        {
            IFoodEffect clone = Activator.CreateInstance(source.GetType()) as IFoodEffect;
            if (clone == null) return null;

            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(source), clone);
            return clone;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
