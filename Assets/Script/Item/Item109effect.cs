using UnityEngine;

// 패시브 109번 - 혼자서는 효과가 없습니다. 102번과 함께 가지고 있으면 공격력 +40(시너지).
//
// [기획 - 형빈 확정]
// - 102번과 109번을 둘 다 가지고 있는 동안만 공격력 +40입니다(더하기 - 공격력 배율 아이템이 있으면 함께 커짐).
// - 둘 중 하나라도 잃으면(48번 교체 등 포함) +40을 바로 잃습니다. 특수 효과 능력치는 원래 영구 귀속이지만 이 효과는 예외입니다(형빈 확정).
// - 102번 쪽 시너지(총알 적중 시 중독)는 Item102effect.cs 참고. 둘은 하나의 시너지로 봐도 됩니다.
//
// [구현] SynergyWatcher로 "102, 109 모두 보유"를 감시하고, 켜지면 출처 이름 "109_synergy"로 공격력 모디파이어를 걸고 꺼지면 지웁니다
// (76번과 같은 방식 - 아이템 기본 스탯/영구 귀속 효과와 출처 이름이 달라 서로 간섭하지 않음).
//
// [아이템 에셋 설정]
// ItemDefinition: Item Id = 109, Category = Passive, Special Effects에 Item109effect 하나. Stat Modifiers는 비워둡니다
// (Stat Modifiers에 넣으면 혼자 있을 때도 적용되므로 넣지 마세요).
[System.Serializable]
public class Item109effect : IPassiveItemEffect, IRemovablePassiveEffect
{
    private const string SourceSuffix = "_synergy";

    [Tooltip("함께 가지고 있어야 하는 짝 아이템의 Item Id입니다(기본 102).")]
    public string partnerItemId = "102";

    [Tooltip("시너지가 켜져 있는 동안 더하는 공격력입니다(기본 40).")]
    public float synergyDamageBonus = 40f;

    [System.NonSerialized] private SynergyWatcher watcher;
    [System.NonSerialized] private PlayerStats stats;
    [System.NonSerialized] private string sourceKey;

    public void Apply(PassiveEffectContext context, string sourceItemId)
    {
        if (context == null || context.Stats == null || context.Inventory == null || string.IsNullOrEmpty(sourceItemId))
        {
            Debug.LogWarning("Item109effect: PlayerStats/PlayerInventory/아이템 ID를 찾을 수 없어 109번 효과를 적용하지 못했습니다.");
            return;
        }

        stats = context.Stats;
        sourceKey = sourceItemId + SourceSuffix;

        if (watcher == null) watcher = new SynergyWatcher();
        watcher.Start(context.Inventory, new[] { sourceItemId, partnerItemId }, HandleSynergyChanged);
    }

    // [패시브 잃기 - 48번 등] 감시를 멈추고 +40을 지웁니다.
    public void Remove(PassiveEffectContext context, string sourceItemId)
    {
        watcher?.Stop();
        ClearBonus();
    }

    private void HandleSynergyChanged(bool active)
    {
        ClearBonus();
        if (active && stats != null && !string.IsNullOrEmpty(sourceKey))
        {
            stats.AddModifier(StatType.Damage, ModifierType.Additive, synergyDamageBonus, sourceKey);
        }
    }

    private void ClearBonus()
    {
        if (stats != null && !string.IsNullOrEmpty(sourceKey)) stats.RemoveModifiersFromSource(sourceKey);
    }
}
