using System;
using System.Reflection;
using UnityEngine;

// 85번 액티브 아이템 효과 - 사용 시 "직전에 실제로 소모한 일회용 아이템"의 효과를 한 번 더 재현합니다.
// (코스트는 ItemDefinition의 activeCost로 지정합니다 - 85번은 10.)
// 93번(ReplayLastFoodEffect - 직전 음식 재현)과 완전히 같은 구조이고, 음식 대신 일회용을 기록/재현합니다.
//
// [재현 규칙 - 형빈 확정]
// - 즉시형 일회용이면 스페이스를 누르는 즉시 그 일회용의 즉시 효과가 발동합니다.
// - 지연형 일회용이면 이 액티브 아이템이 "대기 상태"로 들어가 HUD의 액티브 아이콘이 반투명해지고,
//   그 일회용의 발동 조건이 충족되는 순간 효과가 발동한 뒤 대기가 풀립니다. 대기 중에는 이 액티브를 다시 사용할 수
//   없고, 다른 액티브 아이템으로 교체하면 대기가 취소됩니다(코스트는 돌려받지 않습니다 - 93번과 같음).
// - 재현한 지연형 효과가 스스로 사용을 취소하면(56번 창에서 [취소하기], 80번 "얻을 수 있는 아이템이 없습니다" 등 - 원래
//   일회용이었다면 소모되지 않는 상황) 대기가 풀리고 낸 코스트를 돌려받습니다(형빈 확정 - 아무 효과도 없었으므로).
// - 직전에 소모한 일회용이 없으면 코스트를 소모하지 않고 사용도 되지 않습니다(CanActivate가 false).
// - 그 일회용의 사용 조건(CanUse - 56번/80번은 전투 없는 방에서만, 22번/46번은 이동할 방이 있을 때만 등)도 그대로 따릅니다.
//   조건이 맞지 않으면 코스트를 내지 않고 아무 일도 일어나지 않습니다.
// - 새 일회용을 소모하기 전까지는 코스트만 내면 몇 번이든 같은 일회용 효과를 재현할 수 있습니다. 재현 불가 일회용은 따로
//   두지 않습니다(형빈 확정 - 85번 자체를 얻기 어렵게 만들 예정).
//
// [직전 일회용의 기준]
// PlayerDisposableItem.LastUsedDisposable - 일회용이 "실제로 소모된 시점"에 기록됩니다(즉시형은 Q를 누른 순간, 지연형은 효과가
// 발동해 소멸한 순간). 대기 중에 교체/버리기/스스로 취소되어 소모되지 않은 일회용은 기록되지 않고, 85번으로 재현한 것도
// 기록을 바꾸지 않습니다.
//
// [재현하는 것과 하지 않는 것]
// 일회용의 "사용" 효과(즉시형이면 ActivateImmediate, 지연형이면 BeginPending)만 재현합니다.
// 획득 시 효과(IDisposableAcquireEffect.OnAcquired - 17번 등)는 재현하지 않습니다.
//
// [지연형 재현이 복제본으로 동작하는 이유 - 93번과 같음]
// 지연형 효과는 대기 상태(구독 핸들러 등)를 효과 객체 안에 들고 있습니다. 원본 객체를 그대로 쓰면 일회용 슬롯의 대기
// 상태와 뒤섞일 수 있어, 복제한 별도 객체로 재현합니다. JsonUtility로 public 직렬화 필드를 복사하고(대기 상태 같은 private
// 필드는 새 객체에서 깨끗한 초기값으로 시작), 에셋 참조 필드(Item Database 등 UnityEngine.Object)는 원본과 같은 참조를 그대로
// 옮겨 담습니다.
//
// [아이템 에셋 설정]
// ItemDefinition: Item Id = 85, Category = Active, Active Cost = 10, Active Effect = ReplayLastDisposableEffect.
[Serializable]
public class ReplayLastDisposableEffect : IDelayedActiveItemEffect, IHasOwnUseSound
{
    // 지금 대기 중인 재현 상태입니다(인스펙터 설정이 아니라 런타임 상태 - 직렬화되지 않습니다).
    // pendingEffect가 null이 아니면 "지연형 재현이 진행 중"이라는 뜻입니다.
    [NonSerialized] private IDisposableItemEffect pendingEffect;
    [NonSerialized] private DisposableEffectContext pendingDisposableContext;
    [NonSerialized] private string pendingSourceId;
    [NonSerialized] private int pendingPaidCost;
    [NonSerialized] private ItemDefinition pendingItem;   // [사용 연출] 재현 중인 일회용(발동 순간 아이콘을 띄울 때 씀)
    [NonSerialized] private GameObject pendingPlayer;

    // 코스트를 내기 전에 호출됩니다. 재현할 일회용이 있고, 그 일회용의 사용 조건을 만족하고, 지연형이라면 복제도 가능할 때만 true.
    public bool CanActivate(ActiveEffectContext context)
    {
        PlayerDisposableItem slot = GetDisposableSlot(context);
        if (slot == null) return false;

        ItemDefinition item = slot.LastUsedDisposable;
        if (item == null || item.disposableEffect == null) return false;

        IDisposableItemEffect source = item.disposableEffect;

        // 일회용의 사용 조건(전투 없는 방에서만 등)을 그대로 따릅니다.
        if (!source.CanUse(slot.CreateReplayContext(null, null))) return false;

        // 지연형은 복제에 실패하면 코스트만 나가고 아무 일도 없게 되므로, 미리 막습니다.
        if (!source.IsImmediate && CloneEffect(source) == null) return false;

        return true;
    }

    public void Activate(ActiveEffectContext context)
    {
        GameAudio.Play(SfxId.Active85); // [사운드] 전용 사용음(공용 발동음 대신)
        PlayerDisposableItem slot = GetDisposableSlot(context);
        if (slot == null) return;

        ItemDefinition item = slot.LastUsedDisposable;
        if (item == null || item.disposableEffect == null) return;

        IDisposableItemEffect source = item.disposableEffect;
        string sourceId = item.itemId;

        // 즉시형: 완료/취소 신호가 필요 없으므로 아무 일도 하지 않는 콜백을 연결하고 바로 발동합니다.
        if (source.IsImmediate)
        {
            source.ActivateImmediate(slot.CreateReplayContext(null, null), sourceId);
            ItemUsePopup.Show(slot.gameObject, item); // [사용 연출] 재현한 일회용 아이콘을 머리 위에 띄움(ItemUsePopup.cs 참고)
            return;
        }

        // 지연형: 복제본으로 대기에 들어갑니다.
        IDisposableItemEffect clone = CloneEffect(source);
        if (clone == null) return;

        pendingEffect = clone;
        pendingSourceId = sourceId;
        pendingItem = item;
        pendingPlayer = slot.gameObject;
        pendingPaidCost = GetPaidCost(context);
        pendingDisposableContext = slot.CreateReplayContext(
            _ => OnReplayCompleted(context, clone),
            _ => OnReplayCancelled(context, clone));

        // 대기 진입을 먼저 알립니다 - BeginPending 안에서 곧바로 완료/취소 신호가 오더라도(80번처럼 그 자리에서 끝나는 효과)
        // 대기 해제가 대기 진입보다 먼저 일어나 아이콘이 반투명인 채로 남는 일이 없도록 순서를 지킵니다(93번과 같음).
        context.EnterPending();
        clone.BeginPending(pendingDisposableContext, sourceId);
    }

    // 대기 중에 다른 액티브 아이템으로 교체될 때 호출됩니다. 효과는 발동시키지 않고, 코스트도 돌려주지 않습니다(93번과 같음).
    public void CancelPending(ActiveEffectContext context)
    {
        IDisposableItemEffect effect = pendingEffect;
        DisposableEffectContext disposableContext = pendingDisposableContext;
        string sourceId = pendingSourceId;
        if (effect == null) return;

        // 상태부터 비웁니다 - 취소 도중 완료/취소 신호가 와도 무시되도록.
        ClearPending();
        effect.CancelPending(disposableContext, sourceId);
    }

    // 복제된 지연형 효과가 발동을 마쳤을 때(완료 신호) 호출됩니다.
    private void OnReplayCompleted(ActiveEffectContext context, IDisposableItemEffect completed)
    {
        // 이미 취소되었거나 다른 재현으로 바뀐 뒤에 온 신호는 무시합니다.
        if (!ReferenceEquals(pendingEffect, completed)) return;

        // [사용 연출] 재현한 지연형 일회용이 발동한 순간 그 아이콘을 머리 위에 띄웁니다(ItemUsePopup.cs 참고).
        ItemDefinition replayedItem = pendingItem;
        GameObject player = pendingPlayer;

        ClearPending();
        context.CompletePendingUse();
        ItemUsePopup.Show(player, replayedItem);
    }

    // 복제된 지연형 효과가 스스로 사용을 취소했을 때(56번 [취소하기], 80번 후보 없음 등) 호출됩니다. 대기를 풀고 코스트를
    // 돌려줍니다(형빈 확정).
    private void OnReplayCancelled(ActiveEffectContext context, IDisposableItemEffect cancelled)
    {
        if (!ReferenceEquals(pendingEffect, cancelled)) return;

        int refund = pendingPaidCost;
        ClearPending();
        context.CompletePendingUse(); // 액티브의 대기 상태만 풀어 다시 사용할 수 있게 합니다.

        if (refund > 0 && context.Resources != null)
        {
            context.Resources.Add(BasicItemType.Cost, refund);
        }
    }

    private void ClearPending()
    {
        pendingEffect = null;
        pendingDisposableContext = null;
        pendingSourceId = null;
        pendingPaidCost = 0;
        pendingItem = null;
        pendingPlayer = null;
    }

    private static PlayerDisposableItem GetDisposableSlot(ActiveEffectContext context)
    {
        if (context == null || context.PlayerObject == null) return null;
        return context.PlayerObject.GetComponent<PlayerDisposableItem>();
    }

    // 방금 이 액티브를 쓰며 낸 코스트입니다(PlayerActiveItem.TryUse가 지불한 것과 같은 계산 - 45번 등 감소 반영).
    private static int GetPaidCost(ActiveEffectContext context)
    {
        if (context == null || context.PlayerObject == null) return 0;
        var activeSlot = context.PlayerObject.GetComponent<PlayerActiveItem>();
        if (activeSlot == null || activeSlot.EquippedItem == null) return 0;
        return PlayerActiveItem.GetEffectiveCost(activeSlot.EquippedItem);
    }

    // 일회용 효과 객체를 복제합니다. 실패하면 null을 돌려줍니다(위 [지연형 재현이 복제본으로 동작하는 이유] 참고).
    private static IDisposableItemEffect CloneEffect(IDisposableItemEffect source)
    {
        if (source == null) return null;

        try
        {
            Type type = source.GetType();
            IDisposableItemEffect clone = Activator.CreateInstance(type) as IDisposableItemEffect;
            if (clone == null) return null;

            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(source), clone);
            CopyAssetReferences(source, clone, type);
            return clone;
        }
        catch (Exception)
        {
            return null;
        }
    }

    // 에셋 참조 필드(UnityEngine.Object 계열 - Item Database, 프리팹 등)는 원본과 같은 참조를 그대로 옮겨 담습니다.
    // public 필드와 [SerializeField] 필드만 대상입니다(대기 상태 같은 런타임 필드는 건드리지 않음).
    private static void CopyAssetReferences(object source, object clone, Type type)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        for (Type t = type; t != null && t != typeof(object); t = t.BaseType)
        {
            foreach (FieldInfo field in t.GetFields(flags | BindingFlags.DeclaredOnly))
            {
                if (!typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType)) continue;
                bool serialized = field.IsPublic || field.IsDefined(typeof(SerializeField), true);
                if (!serialized || field.IsDefined(typeof(NonSerializedAttribute), true)) continue;
                field.SetValue(clone, field.GetValue(source));
            }
        }
    }
}
