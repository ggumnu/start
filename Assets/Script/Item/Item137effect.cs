using UnityEngine;

// 패시브 137번 - "기본 공격 총알이 정중앙과 좌우 15도, 세 갈래로 퍼져 나간다(확산 총)."
//
// [기획 - 형빈 확정]
// - 보유하는 동안 PlayerShooting이 쏘는 모든 총알이 한 발 대신 정중앙 + 좌우 SpreadShotOverride.SpreadAngle(15)도의 세 발로
//   나갑니다(샷건형 엘리트 적의 부채꼴 발사와 같은 모양). 실제 각도 계산/발사는 PlayerShooting.FireBurst와
//   SpreadShotOverride.FillShotAngles 참고 - 이 클래스는 "지금 퍼져야 한다"는 신호만 켜 둡니다.
// - 19번과 같이 탄약은 한 발만 소모합니다(연사 Multishot과는 별개 - 연사 한 번마다 세 발씩).
// - 19번과 함께 보유하면 19번이 우선입니다: 19번의 좌우 30도 총구 각각에서 세 발씩 퍼져 총 6발(-45/-30/-15/+15/+30/+45도),
//   정중앙으로는 나가지 않습니다.
// - 각 총알은 기본 발사와 완전히 같은 총알이라 피해량과 총알 효과(폭발/분열/튕김/넉백/색 등)가 총알마다 따로 붙습니다
//   (모두 SpawnProjectile -> Launch -> OnProjectileSpawned를 그대로 탐). 영거리에서는 여러 발이 한꺼번에 맞을 수 있습니다
//   (형빈 확정 - 의도된 동작). 5번 분열도 총알마다 따로 일어납니다.
// - 우클릭 조준 시 조준선도 실제로 나가는 방향 전부(3줄 / 19번과 함께면 6줄)를 보여줍니다(AimTrajectoryIndicator).
// - 48번 등으로 잃으면 그 즉시 원래대로 돌아갑니다(IRemovablePassiveEffect).
//
// [아이템 에셋 설정]
// ItemDefinition: Item Id = 137, Category = Passive, Special Effects에 Item137effect 하나. Stat Modifiers는 비워둡니다.
[System.Serializable]
public class Item137effect : IPassiveItemEffect, IRemovablePassiveEffect
{
    public void Apply(PassiveEffectContext context, string sourceItemId)
    {
        if (context == null) return;

        SpreadShotOverride.SetActive(sourceItemId, context.PlayerObject);
    }

    // [패시브 잃기 - 48번 등] 소지 중에만 유지되던 효과를 되돌립니다(IRemovablePassiveEffect.cs 참고).
    public void Remove(PassiveEffectContext context, string sourceItemId)
    {
        SpreadShotOverride.RemoveActive(sourceItemId);
    }
}
