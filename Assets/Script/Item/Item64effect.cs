using UnityEngine;

// 패시브 64번 - "깎인 체력 0.5당 공격력 +5(1당 +10). 체력에 따라 계속 변한다."
//
// [기획 - 형빈 확정]
// - 보너스 공격력 = (최대 체력 - 현재 체력) x damagePerMissingHealth(기본 10). 체력은 0.5 단위라 0.5 깎이면 +5, 1.5 깎이면 +15.
//   예) 5/5 -> +0, 5/4 -> +10, 5/1 -> +40, 5/1에서 1 회복해 5/2 -> +30, 다 회복 -> +0.
// - "더하기" 공격력이라, 공격력 곱하기 아이템이 있으면 이 보너스도 같이 커집니다(다른 덧셈 스탯과 같은 규칙 - 형빈 확정).
// - 이 아이템을 잃으면 이 보너스는 전부 사라집니다(특수 효과 능력치 영구 귀속 규칙의 예외 - "잃으면 능력치도 잃는다").
// - 체력이 0이 되는 순간 깎인 체력이 최대치가 되면서 공격력이 잠깐 크게 표시될 수 있지만, 게임 오버라 그대로 둡니다(형빈 확정).
//
// [구현 - 기존 스크립트는 고치지 않았습니다]
// 획득하면 플레이어에 감시 컴포넌트(MissingHealthDamageBonus)를 붙입니다. 이 컴포넌트가 매 프레임 깎인 체력을 계산하고, 값이
// 바뀌었을 때만 64번 전용 출처 이름의 공격력 모디파이어를 빼고 새 값으로 다시 넣습니다. 체력이 바뀌는 길(피격/회복/최대 체력
// 변화 등)이 여러 곳이고 PlayerHealth에 "체력이 바뀌었다" 알림이 없어서, 어느 경로로 바뀌든 놓치지 않도록 직접 확인하는
// 방식을 택했습니다(계산은 뺄셈 한 번이라 부담 없음). 최대 체력 아이템은 현재 체력도 같이 올려주므로 깎인 체력은 그대로입니다.
//
// [아이템 에셋 설정]
// ItemDefinition: Item Id = 64, Category = Passive, Special Effects에 MissingHealthDamageEffect 하나. Stat Modifiers는 비워둡니다.
[System.Serializable]
public class MissingHealthDamageEffect : IPassiveItemEffect, IRemovablePassiveEffect
{
    [Tooltip("깎인 체력 1당 얻는 공격력입니다(기본 10 - 체력 0.5당 5). Play 중에 바꾸면 바로 반영됩니다.")]
    public float damagePerMissingHealth = 10f;

    // 64번 보너스 모디파이어의 출처 이름입니다. 아이템 기본 스탯(출처 = 아이템 ID)과 섞이지 않도록 따로 둡니다.
    public static string ModifierSourceId(string itemId) => "missing_health_damage_" + itemId;

    public void Apply(PassiveEffectContext context, string sourceItemId)
    {
        if (context == null || context.PlayerObject == null || context.Stats == null || context.Health == null)
        {
            Debug.LogWarning("MissingHealthDamageEffect: 플레이어/PlayerStats/PlayerHealth를 찾을 수 없어 64번 효과를 적용하지 못했습니다.");
            return;
        }

        var monitor = context.PlayerObject.GetComponent<MissingHealthDamageBonus>();
        if (monitor == null) monitor = context.PlayerObject.AddComponent<MissingHealthDamageBonus>();
        monitor.Setup(this, context.Stats, context.Health, ModifierSourceId(sourceItemId));
    }

    // [패시브 잃기 - 48번 등] 보너스 공격력을 즉시 전부 없애고 감시 컴포넌트를 뗍니다.
    public void Remove(PassiveEffectContext context, string sourceItemId)
    {
        if (context?.PlayerObject != null)
        {
            var monitor = context.PlayerObject.GetComponent<MissingHealthDamageBonus>();
            if (monitor != null)
            {
                monitor.Clear();
                Object.Destroy(monitor);
            }
        }

        // 컴포넌트가 없더라도 남은 보너스가 없도록 한 번 더 지웁니다.
        if (context?.Stats != null) context.Stats.RemoveModifiersFromSource(ModifierSourceId(sourceItemId));
    }
}

// 64번을 보유하는 동안 플레이어에 붙어 있는 감시 컴포넌트입니다(64번이 자동으로 붙이고 떼므로 에디터에서 붙일 필요 없음).
public class MissingHealthDamageBonus : MonoBehaviour
{
    private MissingHealthDamageEffect effect;
    private PlayerStats stats;
    private PlayerHealth health;
    private string sourceId;

    // 지금 걸려 있는 보너스의 기준이 된 "깎인 체력"입니다(-1 = 아직 아무것도 걸지 않음).
    private float appliedMissing = -1f;
    private float appliedPerHealth;
    private bool cleared;

    public void Setup(MissingHealthDamageEffect effect, PlayerStats stats, PlayerHealth health, string sourceId)
    {
        this.effect = effect;
        this.stats = stats;
        this.health = health;
        this.sourceId = sourceId;
        cleared = false;
        appliedMissing = -1f;
        Refresh(); // 얻는 순간 바로 반영
    }

    void Update()
    {
        Refresh();
    }

    void Refresh()
    {
        if (cleared || effect == null || stats == null || health == null) return;

        // 체력은 0.5 단위이므로 계산 오차를 없애기 위해 0.5 단위로 맞춥니다.
        float missing = Mathf.Max(0f, stats.MaxHealth - health.CurrentHealth);
        missing = Mathf.Round(missing * 2f) / 2f;

        float perHealth = effect.damagePerMissingHealth;
        if (Mathf.Approximately(missing, appliedMissing) && Mathf.Approximately(perHealth, appliedPerHealth)) return;

        stats.RemoveModifiersFromSource(sourceId);
        float bonus = missing * perHealth;
        if (!Mathf.Approximately(bonus, 0f))
        {
            stats.AddModifier(StatType.Damage, ModifierType.Additive, bonus, sourceId);
        }

        appliedMissing = missing;
        appliedPerHealth = perHealth;
    }

    // 보너스를 즉시 없애고 더 이상 갱신하지 않습니다.
    public void Clear()
    {
        if (stats != null && !string.IsNullOrEmpty(sourceId)) stats.RemoveModifiersFromSource(sourceId);
        cleared = true;
        appliedMissing = -1f;
    }

    void OnDestroy()
    {
        Clear();
    }
}
