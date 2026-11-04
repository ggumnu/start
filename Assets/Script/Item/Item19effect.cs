using UnityEngine;

// 패시브 19번 - "총알이 정중앙이 아니라 좌우로 60도(각 30도씩) 벌어진 두 갈래로 나간다".
//
// [기획 - 형빈 확정]
// - 장착하고 있는 동안, PlayerShooting이 쏘는 모든 총알이 정중앙 한 발이 아니라 좌우로
//   SplitShotOverride.HalfAngle(기본 30)도씩 벌어진 두 발로 나갑니다(실제 각도 계산/발사
//   로직은 PlayerShooting.FireBurst 참고 - 이 클래스는 "지금 벌어져야 한다"는 신호만
//   켜둡니다).
// - 연사(Multishot)와는 완전히 독립된 축입니다 - 탄창의 탄환 하나당 두 발이 나가는 것이지,
//   두 발을 쏘는 데 탄약 2개가 드는 게 아닙니다(PlayerShooting.cs의 currentAmmo -= 1이
//   그대로인 것 참고).
// - 두 발 각각은 기본 발사와 완전히 동일한 총알이라, 폭발/사거리 피해/튕기기 등 다른 패시브
//   효과가 각자 개별로 계산됩니다(둘 다 SpawnProjectile -> Launch -> OnProjectileSpawned를
//   그대로 타므로 별도 처리가 필요 없습니다). 적이 초근접에 있으면 두 발을 한꺼번에 맞을 수도
//   있습니다(형빈 확인, 의도된 동작).
// - 조준 중일 때 조준선도 이 벌어진 두 방향을 그대로 보여줍니다(AimTrajectoryIndicator.cs가
//   같은 SplitShotOverride를 참조해서 자동으로 맞춰집니다).
//
// [구현 - SplitShotOverride 등록]
// 패시브 아이템은 영구 적용이라 별도의 해제 시점이 없습니다(다른 영구 패시브 효과와 동일).
// Apply()에서 SplitShotOverride.SetActive(sourceItemId, context.PlayerObject)만 호출하면
// 되고, 실제 각도 계산/두 발 발사/조준선 갈림은 전부 PlayerShooting.cs/
// AimTrajectoryIndicator.cs 쪽에서 SplitShotOverride를 조회해 처리합니다.
//
// [아이템 에셋 설정]
// ItemDefinition: Item Id = 19, Category = Passive, Special Effects = Item19effect.
[System.Serializable]
public class Item19effect : IPassiveItemEffect, IRemovablePassiveEffect
{
    public void Apply(PassiveEffectContext context, string sourceItemId)
    {
        if (context == null) return;

        SplitShotOverride.SetActive(sourceItemId, context.PlayerObject);
    }

    // [패시브 잃기 - 48번 등] 소지 중에만 유지되던 효과를 되돌립니다(IRemovablePassiveEffect.cs 참고).
    public void Remove(PassiveEffectContext context, string sourceItemId)
    {
        SplitShotOverride.RemoveActive(sourceItemId);
    }
}
