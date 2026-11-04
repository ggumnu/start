using UnityEngine;

// 패시브 76번 - "보유한 패시브 아이템 하나당 공격력 +2 (76번 자신 포함)."
//
// [기획 - 형빈 확정]
// - 공격력 보너스 = damagePerPassive(기본 2) x 지금 보유한 패시브 아이템 개수(76번 자신 포함).
//   예) 76번 포함 패시브 3개 -> +6. 그 뒤 보물상자 등에서 패시브를 하나 더 얻으면 -> +8.
// - 패시브 아이템만 셉니다. 액티브/장신구/음식/일회용은 세지 않습니다(각자 다른 슬롯이라 PlayerInventory에 없음).
// - 다른 패시브를 잃으면(나중에 생길 "패시브 잃기" 효과 등) 그만큼 2씩 줄어듭니다 - 항상 "2 x 현재 개수"로 유지됩니다.
// - 76번 자신을 잃으면(48번 교체 등) 76번으로 얻은 공격력을 전부 잃습니다. 48번으로 76번을 다시 얻으면 그 순간의 패시브
//   개수로 새로 계산해서 얻습니다.
// - 더하기(Additive)로 붙으므로 공격력 배율(Multiplicative) 아이템이 있으면 이 보너스도 함께 커집니다.
//
// [구현]
// - 보너스는 출처 이름 "76_passiveCount"(아이템 ID + PassiveCountSourceSuffix)로 PlayerStats에 공격력 더하기 모디파이어
//   하나로 겁니다. 다시 계산할 때마다 그 출처의 모디파이어를 지우고 새 값으로 하나만 다시 걸어서, 값이 겹쳐 쌓이지 않습니다.
//   아이템 기본 스탯(출처 = 아이템 ID)이나 영구 귀속 특수 효과(출처 = 아이템 ID + "_effect")와 출처 이름이 달라서 서로
//   간섭하지 않습니다.
// - PlayerInventory.OnPassiveItemsChanged(획득/잃기/교체가 끝난 직후 알림)를 구독해서 개수가 바뀔 때마다 다시 계산합니다.
// - 획득 순간(Apply)에는 76번이 아직 보유 목록에 들어가기 전이라 자기 자신을 +1로 세고, 목록에 들어간 직후 오는 알림에서
//   실제 개수로 다시 계산합니다(결과는 같음). 48번 교체처럼 이미 목록에 들어간 뒤 Apply되면 +1을 하지 않습니다.
// - 잃으면(Remove) 구독을 풀고 보너스를 지웁니다.
//
// [아이템 에셋 설정]
// ItemDefinition: Item Id = 76, Category = Passive, Special Effects에 PassiveCountDamageEffect 하나. Stat Modifiers는 비워둡니다.
[System.Serializable]
public class PassiveCountDamageEffect : IPassiveItemEffect, IRemovablePassiveEffect
{
    // 이 효과가 PlayerStats에 거는 모디파이어의 출처 이름 꼬리입니다(아이템 ID 뒤에 붙음).
    private const string PassiveCountSourceSuffix = "_passiveCount";

    [Tooltip("보유 패시브 아이템 하나당 더하는 공격력입니다(기본 2). 76번 자신도 한 개로 셉니다.")]
    public float damagePerPassive = 2f;

    [System.NonSerialized] private PlayerStats stats;
    [System.NonSerialized] private PlayerInventory inventory;
    [System.NonSerialized] private string itemId;

    private string SourceKey => itemId + PassiveCountSourceSuffix;

    public void Apply(PassiveEffectContext context, string sourceItemId)
    {
        if (context == null || context.Stats == null || context.Inventory == null)
        {
            Debug.LogWarning("PassiveCountDamageEffect: PlayerStats/PlayerInventory를 찾을 수 없어 76번 효과를 적용하지 못했습니다.");
            return;
        }

        if (string.IsNullOrEmpty(sourceItemId))
        {
            Debug.LogWarning("PassiveCountDamageEffect: 아이템 ID가 비어 있어 76번 효과를 적용하지 못했습니다. ItemDefinition의 Item Id를 채워주세요.");
            return;
        }

        // 혹시 남아 있을 이전 구독 정리(중복 방지)
        if (inventory != null) inventory.OnPassiveItemsChanged -= HandlePassiveItemsChanged;

        stats = context.Stats;
        inventory = context.Inventory;
        itemId = sourceItemId;

        inventory.OnPassiveItemsChanged -= HandlePassiveItemsChanged;
        inventory.OnPassiveItemsChanged += HandlePassiveItemsChanged;

        Recalculate();
    }

    // [패시브 잃기 - 48번 등] 76번으로 얻은 공격력을 전부 잃습니다.
    public void Remove(PassiveEffectContext context, string sourceItemId)
    {
        if (inventory != null) inventory.OnPassiveItemsChanged -= HandlePassiveItemsChanged;

        PlayerStats targetStats = stats != null ? stats : context?.Stats;
        string key = (string.IsNullOrEmpty(itemId) ? sourceItemId : itemId) + PassiveCountSourceSuffix;
        if (targetStats != null) targetStats.RemoveModifiersFromSource(key);

        inventory = null;
    }

    private void HandlePassiveItemsChanged()
    {
        // 76번을 이미 잃은 상태면(정상이라면 Remove에서 구독이 풀려 여기 오지 않음) 아무것도 하지 않습니다.
        if (inventory == null || !inventory.HasItem(itemId)) return;
        Recalculate();
    }

    // 보너스를 "damagePerPassive x 현재 패시브 개수"로 다시 겁니다(이전 값은 지우고 하나만).
    private void Recalculate()
    {
        if (stats == null || inventory == null) return;

        int count = inventory.PassiveCount;
        if (!inventory.HasItem(itemId)) count += 1; // 획득 순간: 76번이 아직 목록에 들어가기 전이라 자기 자신을 셈

        stats.RemoveModifiersFromSource(SourceKey);

        float bonus = damagePerPassive * count;
        if (!Mathf.Approximately(bonus, 0f))
        {
            stats.AddModifier(StatType.Damage, ModifierType.Additive, bonus, SourceKey);
        }
    }
}
