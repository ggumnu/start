using System.Collections.Generic;

// IPassiveItemEffect의 첫 구현체이자 "즉시 적용형"의 예시입니다. ItemDefinition.statModifiers
// 리스트를 그대로 받아서, 아이템 획득 시 각 StatModifier를 PlayerStats에 등록합니다.
// "공격력 +1" 같은 단일 효과 아이템도, "공격력 +1 & 이동속도 -0.5" 같은 복합/트레이드오프
// 아이템도 전부 이 클래스 하나로 처리됩니다 - 새 스탯형 아이템을 추가할 때 스크립트를
// 새로 만들 필요가 없습니다.
public class StatModifierEffect : IPassiveItemEffect
{
    private readonly List<StatModifier> modifiers;

    public StatModifierEffect(List<StatModifier> modifiers)
    {
        this.modifiers = modifiers;
    }

    public void Apply(PassiveEffectContext context, string sourceItemId)
    {
        if (modifiers == null || context == null || context.Stats == null) return;

        foreach (var mod in modifiers)
        {
            context.Stats.AddModifier(mod.statType, mod.modifierType, mod.value, sourceItemId);
        }
    }
}