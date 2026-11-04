using UnityEngine;

// 패시브 65번 - "보유하고 있는 동안 음식을 소모할 때마다 공격력 +10(영구 귀속)."
//
// [기획 - 형빈 확정]
// - 음식이 완전히 소모되는 순간마다 공격력 +damagePerFood(기본 10)를 더하기로 얻습니다.
//   - 즉시형: E로 먹는 즉시 / 지연형: 대기가 끝나 효과가 발동하고 소모되는 순간(대기 상태에서는 오르지 않음).
//   - 25번으로 음식 대신 25번이 소모된 경우도 음식 소모로 취급해 오릅니다.
//   - 93번(직전 음식 효과 재발동)은 음식을 소모하지 않으므로 오르지 않습니다.
//   - 대기 중 교체/버리기로 취소된 음식, 칸이 줄어 바닥에 떨어진 음식은 먹은 게 아니므로 오르지 않습니다.
// - 판정 기준은 "소모되는 순간 65번을 들고 있는지"입니다. 65번을 얻기 전에 대기를 시작한 음식도 얻은 뒤에 소모되면 오르고,
//   대기 중에 65번을 잃으면 그 음식이 소모돼도 오르지 않습니다.
// - 얻은 공격력은 영구 귀속입니다. 65번을 잃어도(48번 교체 포함) 지금까지 얻은 공격력은 남고, 그 뒤 먹는 음식부터 오르지 않습니다.
//
// [구현]
// PlayerFoodItem.OnFoodConsumed(음식이 소모되는 단 한 지점에서 발생)를 구독합니다. 공격력은 특수 효과 능력치용 출처 이름
// (아이템 ID + "_effect")으로 걸어서, 패시브를 잃을 때 기본 스탯(출처 = 아이템 ID)만 지우는 규칙에 휩쓸리지 않고 남습니다.
//
// [아이템 에셋 설정]
// ItemDefinition: Item Id = 65, Category = Passive, Special Effects에 FoodConsumedDamageEffect 하나. Stat Modifiers는 비워둡니다.
[System.Serializable]
public class FoodConsumedDamageEffect : IPassiveItemEffect, IRemovablePassiveEffect
{
    [Tooltip("음식을 하나 소모할 때마다 영구히 얻는 공격력입니다(기본 10).")]
    public float damagePerFood = 10f;

    // 구독을 풀 때 같은 대상을 쓰기 위해 기억해 둡니다.
    [System.NonSerialized] private PlayerFoodItem subscribedFood;
    [System.NonSerialized] private PlayerStats stats;
    [System.NonSerialized] private string modifierSourceId;

    public void Apply(PassiveEffectContext context, string sourceItemId)
    {
        if (context == null || context.PlayerObject == null || context.Stats == null)
        {
            Debug.LogWarning("FoodConsumedDamageEffect: 플레이어/PlayerStats를 찾을 수 없어 65번 효과를 적용하지 못했습니다.");
            return;
        }

        var food = context.PlayerObject.GetComponent<PlayerFoodItem>();
        if (food == null)
        {
            Debug.LogWarning("FoodConsumedDamageEffect: PlayerFoodItem을 찾을 수 없어 65번 효과를 적용하지 못했습니다.");
            return;
        }

        Unsubscribe(); // 혹시 남아 있을 이전 구독 정리(중복 방지)

        stats = context.Stats;
        modifierSourceId = sourceItemId + "_effect";
        subscribedFood = food;
        subscribedFood.OnFoodConsumed += HandleFoodConsumed;
    }

    // [패시브 잃기 - 48번 등] 구독만 풉니다. 이미 얻은 공격력은 귀속이라 남깁니다(형빈 확정).
    public void Remove(PassiveEffectContext context, string sourceItemId)
    {
        Unsubscribe();
    }

    private void Unsubscribe()
    {
        if (subscribedFood != null) subscribedFood.OnFoodConsumed -= HandleFoodConsumed;
        subscribedFood = null;
    }

    private void HandleFoodConsumed(ItemDefinition food, bool redirected)
    {
        if (stats == null || Mathf.Approximately(damagePerFood, 0f)) return;
        stats.AddModifier(StatType.Damage, ModifierType.Additive, damagePerFood, modifierSourceId);
    }
}
