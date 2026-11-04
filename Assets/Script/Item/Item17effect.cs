using UnityEngine;

// 일회용 17번 (2026-10 변경 - 패시브에서 일회용으로, 형빈 확정) - "획득 시 레벨 최초 한정으로 최대 체력 +1. 사용하면 그 순간
// 깎여 있던 체력 1당 최대 체력을 1 얻는다."
//
// [변경 이유]
// 패시브일 때는 획득하는 순간에만 효과가 있고 그 뒤로는 아무 의미가 없는 아이템이 되어서, 효과는 그대로 두고 일회용으로
// 바꿨습니다. 예전 패시브 효과(MissingHealthMaxHealthBonusEffect)는 삭제했습니다.
//
// [기획]
// - 획득(일회용 슬롯에 장착)하면 최대 체력을 maxHealthGainOnAcquire(기본 1)만큼 올립니다. 같은 레벨 안에서는 딱 한 번만
//   입니다(교체로 버렸다가 같은 레벨에서 다시 주워도 다시 오르지 않고, 다음 레벨에서 다시 얻으면 다시 오름 - 34번 음식과 같은
//   GameFlowManager.TryConsumeOneTimeGrant 패턴). 일회용은 아이템 에셋의 Stat Modifiers를 읽지 않으므로, 예전 기본 스탯이던
//   이 +1을 효과 안으로 옮겼습니다.
// - 사용(Q)하면 그 순간의 (최대 체력 - 현재 체력)을 계산해 정수 부분만큼 최대 체력을 올립니다(소수점 버림 - 예전 패시브와 같은
//   계산). 예) 최대 7 / 현재 5 -> 깎인 체력 2 -> 최대 체력 +2(7 -> 9). 깎인 체력이 1.5면 +1.
// - 깎인 체력이 1 미만이면(체력이 가득 찼거나 0.5만 깎였을 때) 얻을 게 없으므로 사용할 수 없습니다 - 소모되지 않습니다(형빈
//   확정, 9번/48번과 같은 방식).
// - 얻은 최대 체력은 둘 다 영구적입니다.
//
// [계산 시점]
// PlayerStats.AddModifier(MaxHealth, ...)를 호출하면 그 즉시 PlayerHealth가 현재 체력도 같은 폭만큼 올려주므로, 깎인 체력은
// 반드시 AddModifier 호출 *전에* 읽어둡니다.
//
// [아이템 에셋 설정]
// ItemDefinition: Item Id = 17, Category = Disposable, Disposable Effect = MissingHealthMaxHealthDisposableEffect.
// 예전 Stat Modifiers(최대 체력 +1)와 Special Effects 줄은 지워주세요(일회용은 둘 다 읽지 않음).
[System.Serializable]
public class MissingHealthMaxHealthDisposableEffect : IDisposableItemEffect, IDisposableAcquireEffect
{
    [Tooltip("획득 시 최초 한 번(레벨당)만 오르는 최대 체력입니다.")]
    public float maxHealthGainOnAcquire = 1f;

    public bool IsImmediate => true;

    // 획득 시: 레벨당 1회만 최대 체력 +1
    public void OnAcquired(DisposableEffectContext context, string sourceItemId)
    {
        if (context?.Stats == null || Mathf.Approximately(maxHealthGainOnAcquire, 0f)) return;

        // 같은 레벨 안에서 이미 한 번 받았으면 다시 올리지 않습니다. 아이템 테스트 씬처럼 GameFlowManager가 없는 곳에서는
        // 막을 기준이 없으므로 경고만 남기고 올립니다(13번/14번과 같은 처리).
        if (context.GameFlow != null)
        {
            if (!context.GameFlow.TryConsumeOneTimeGrant($"{sourceItemId}_maxhealth")) return;
        }
        else
        {
            Debug.LogWarning("MissingHealthMaxHealthDisposableEffect: GameFlowManager를 찾지 못해 레벨당 1회 제한 없이 최대 체력을 올립니다.");
        }

        context.Stats.AddModifier(StatType.MaxHealth, ModifierType.Additive, maxHealthGainOnAcquire, sourceItemId);
    }

    // 깎인 체력이 1 이상일 때만 사용할 수 있습니다(1 미만이면 얻을 게 없어 소모되지 않음).
    public bool CanUse(DisposableEffectContext context)
    {
        return GetBonus(context) >= 1;
    }

    public void ActivateImmediate(DisposableEffectContext context, string sourceItemId)
    {
        int bonus = GetBonus(context);
        if (bonus < 1) return;

        context.Stats.AddModifier(StatType.MaxHealth, ModifierType.Additive, bonus, sourceItemId);
    }

    // 즉시형이라 호출되지 않습니다.
    public void BeginPending(DisposableEffectContext context, string sourceItemId) { }

    // 즉시형이라 호출되지 않습니다.
    public void CancelPending(DisposableEffectContext context, string sourceItemId) { }

    // 지금 깎인 체력의 정수 부분(= 사용하면 얻을 최대 체력)입니다. 소수점은 버립니다.
    private static int GetBonus(DisposableEffectContext context)
    {
        if (context == null || context.Stats == null || context.Health == null) return 0;

        float missingHealth = context.Stats.MaxHealth - context.Health.CurrentHealth;
        return Mathf.FloorToInt(missingHealth + 0.0001f); // 2를 1.9999로 저장하는 오차로 1로 세는 것 방지
    }
}
