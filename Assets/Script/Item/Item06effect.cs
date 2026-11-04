using UnityEngine;

// 패시브 아이템 6번 - "적을 제어하는 스킬(매료 등)의 지속시간이 1초 증가한다."
//
// [효과]
// 예를 들어 38번 아이템의 매료는 적중 시 2초 동안 적을 매료시키는데, 이 아이템을
// 소지하고 있으면 지속시간이 1초 늘어나 총 3초가 됩니다.
//
// [이 파일의 역할]
// 인스펙터 설정값(durationBonusSeconds)만 들고 있고, 아이템을 획득하면 Apply()가 한 번
// 호출되어 EnemyControlDurationBonus에 보너스를 등록합니다. 실제 적용은 각 제어 스킬
// (지금은 38번 매료 - EnemyCharmState)이 자기 지속시간을 확정하는 시점에
// EnemyControlDurationBonus.Resolve()를 거쳐서 이루어집니다 - 이 효과 클래스는 어떤
// 제어 스킬이 있는지 전혀 몰라도 됩니다(EnemyDetectRangeOverride/7번과 동일한 구조).
// 값은 매번 읽으므로 Play 중에 인스펙터 값을 바꾸면 곧바로 반영됩니다.
//
// [앞으로 새 제어 스킬을 추가할 때]
// 이 아이템의 보너스는 자동으로 모든 디버프에 적용되지 않습니다. 새 제어 스킬(빙결
// 등)을 만들 때, 그 스킬이 자기 지속시간을 확정하는 바로 그 줄에서
// EnemyControlDurationBonus.Resolve()를 직접 호출해줘야만 6번의 혜택을 받습니다 -
// EnemyControlDurationBonus.cs 상단 주석 참고.
//
// [여러 개의 "제어 지속시간 증가" 아이템이 함께 있을 때 - 형빈 확정]
// 등록된 보너스는 전부 더합니다(합연산). 지금은 6번 하나뿐이라 실질적으로 항상 +1초
// 이지만, 나중에 같은 종류의 아이템이 하나 더 생기면 자동으로 두 보너스가 합산됩니다.
//
// [에디터 설정 방법]
// ItemDefinition 에셋 - Special Effects 리스트에 "+"를 눌러 ControlDurationBonusEffect를
// 고르면 Duration Bonus Seconds 칸이 나타납니다.
[System.Serializable]
public class ControlDurationBonusEffect : IPassiveItemEffect, IRemovablePassiveEffect
{
    [Tooltip("제어 스킬(매료 등)의 지속시간에 더해지는 보너스(초)입니다.")]
    public float durationBonusSeconds = 1f;

    public void Apply(PassiveEffectContext context, string sourceItemId)
    {
        if (context == null || context.PlayerObject == null) return;

        EnemyControlDurationBonus.SetBonus("control_bonus_" + sourceItemId, context.PlayerObject, () => durationBonusSeconds);
    }

    // [패시브 잃기 - 48번 등] 소지 중에만 유지되던 효과를 되돌립니다(IRemovablePassiveEffect.cs 참고).
    public void Remove(PassiveEffectContext context, string sourceItemId)
    {
        EnemyControlDurationBonus.RemoveBonus("control_bonus_" + sourceItemId);
    }
}
