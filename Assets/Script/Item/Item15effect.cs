using UnityEngine;

// 패시브 아이템 15번 - "남은 탄환이 탄창의 50% 미만일 때 장전하면 장전 시간이 50% 감소한다.
// 단, 탄창이 완전히 빈 상태(0)에서의 장전에는 효과가 없다."
//
// [효과]
// 예를 들어 탄창 10 / 남은 탄환 4 / 장전 시간 1초라면(40% < 50%) 장전 시간이 0.5초로 줄어들고,
// 남은 탄환 7이라면(70% >= 50%) 그대로 1초입니다. 남은 탄환이 0이라면 수동/자동 재장전 모두
// 보너스 없이 원래 장전 시간이 적용됩니다. 줄어든 장전 시간은 플레이어의 장전 시간 하한선
// (PlayerStats.statFloors의 ReloadTime 설정) 밑으로는 내려가지 않습니다.
//
// [이 파일의 역할]
// 인스펙터 설정값(reloadTimeReductionRatio)만 들고 있고, 아이템을 획득하면 Apply()가 한 번
// 호출되어 ReloadSpeedBonusOverride에 감소율을 등록합니다. "남은 탄환 비율이 50% 미만인가"
// 판정과 "하한선 클램프"는 전부 실제 장전 시간이 결정되는 시점(PlayerShooting.
// ComputeReloadDuration)에서 이루어집니다 - 이 효과 클래스는 그 판정 로직을 전혀 몰라도
// 됩니다(6번 아이템과 동일한 구조).
//
// [여러 개의 "장전 시간 감소" 아이템이 함께 있을 때 - 형빈 확정]
// 등록된 감소율은 전부 곱연산으로 합쳐집니다(ReloadSpeedBonusOverride.cs 참고). 지금은 15번
// 하나뿐이라 실질적으로 항상 50% 감소이지만, 나중에 같은 종류의 아이템이 하나 더 생기면
// 자동으로 두 보너스가 곱연산으로 누적됩니다.
//
// [에디터 설정 방법]
// ItemDefinition 에셋 - Special Effects 리스트에 "+"를 눌러 LowAmmoFastReloadEffect를 고르면
// Reload Time Reduction Ratio 칸이 나타납니다.
[System.Serializable]
public class LowAmmoFastReloadEffect : IPassiveItemEffect, IRemovablePassiveEffect
{
    [Tooltip("발동 조건을 만족했을 때 장전 시간에서 줄어드는 비율입니다(0.5 = 50% 감소).")]
    public float reloadTimeReductionRatio = 0.5f;

    public void Apply(PassiveEffectContext context, string sourceItemId)
    {
        if (context == null || context.PlayerObject == null) return;

        ReloadSpeedBonusOverride.SetBonus("reload_bonus_" + sourceItemId, context.PlayerObject, () => reloadTimeReductionRatio);
    }

    // [패시브 잃기 - 48번 등] 소지 중에만 유지되던 효과를 되돌립니다(IRemovablePassiveEffect.cs 참고).
    public void Remove(PassiveEffectContext context, string sourceItemId)
    {
        ReloadSpeedBonusOverride.RemoveBonus("reload_bonus_" + sourceItemId);
    }
}
