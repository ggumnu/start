using UnityEngine;

// 패시브 30번 - "획득 시 크레딧 30을 즉시 얻는다. 추가로, 이 아이템을 소지하고 있는 동안
// 크레딧을 획득할 때마다(출처 무관) 1을 추가로 더 얻는다. 이 아이템 자신이 주는 30 크레딧
// 획득에도 적용되어 실질적으로는 31을 얻는다."
//
// [기획 - 두 개의 효과로 분리]
// 1) CreditGrantEffect - 획득 시 크레딧 creditAmount(기본 30)를 즉시 지급.
// 2) CreditGainBonusEffect - 소지 중에는 "크레딧을 얻을 때마다" 항상 bonusPerGain(기본 1)을
//    추가로 더 받도록 CreditGainBonusOverride(정적 등록소)에 등록. 실제 가산은
//    PlayerResources.Add()에서 Credit 타입일 때 이 레지스트리의 CurrentBonus를 더하는
//    방식으로 처리되므로(별도 수정 필요 - PlayerResources.cs 참고), 39번의 방 클리어 보상,
//    20번의 최초 지급, 보물상자 드랍 등 크레딧을 주는 모든 경로에 자동으로 적용됩니다.
//
// [중요 - Special Effects 리스트 순서]
// 이 아이템 자신이 주는 30 크레딧에도 +1이 적용되려면(총 31), CreditGainBonusEffect가
// CreditGrantEffect보다 먼저 등록되어 있어야 합니다. PlayerInventory.TryAcquireItem()은
// GetEffects()가 반환하는 순서 그대로 각 효과의 Apply()를 순차 호출하므로, ItemDefinition
// 인스펙터의 Special Effects 리스트에서 CreditGainBonusEffect를 CreditGrantEffect보다
// 위쪽(먼저)에 놓아야 합니다. 순서가 반대면 이 아이템 자신의 30 크레딧에는 보너스가 붙지
// 않고(30 그대로), 이후에 얻는 크레딧부터만 +1이 적용됩니다.
[System.Serializable]
public class CreditGrantEffect : IPassiveItemEffect, IRemovablePassiveEffect
{
    public int creditAmount = 30;

    public void Apply(PassiveEffectContext context, string sourceItemId)
    {
        if (context == null || context.Resources == null)
        {
            Debug.LogWarning("CreditGrantEffect: PlayerResources를 찾을 수 없어 효과를 적용하지 못했습니다.");
            return;
        }

        context.Resources.Add(BasicItemType.Credit, creditAmount);
    }

    // [패시브 잃기 - 48번 등] 1회성으로 받은 크레딧은 잃어도 빼앗지 않으므로 되돌릴 것이 없습니다(형빈 확정).
    public void Remove(PassiveEffectContext context, string sourceItemId) { }
}

[System.Serializable]
public class CreditGainBonusEffect : IPassiveItemEffect, IRemovablePassiveEffect
{
    public int bonusPerGain = 1;

    public void Apply(PassiveEffectContext context, string sourceItemId)
    {
        if (context == null || context.PlayerObject == null) return;

        CreditGainBonusOverride.SetBonus("credit_gain_bonus_" + sourceItemId, context.PlayerObject, () => bonusPerGain);
    }

    // [패시브 잃기 - 48번 등] 소지 중에만 유지되던 효과를 되돌립니다(IRemovablePassiveEffect.cs 참고).
    public void Remove(PassiveEffectContext context, string sourceItemId)
    {
        CreditGainBonusOverride.RemoveBonus("credit_gain_bonus_" + sourceItemId);
    }
}
