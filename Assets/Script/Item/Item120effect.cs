using UnityEngine;

// 패시브 120번 - "총알이 벽을 관통한다."
//
// [기획 - 형빈 확정]
// - 보유하는 동안 쏘는 모든 총알(19번/137번 갈래, 5번 분열탄, 110번 강화 총알 포함)이 방 안의 벽/장애물을 관통합니다. 99번(적 관통)의 벽 버전입니다.
// - 벽 한 덩어리를 지날 때 한 번만 반응합니다(벽 프리팹이 여러 조각 콜라이더여도 - 겹쳐 붙은 조각은 같은 덩어리로 봄).
//   - 4번: 벽에 닿는 순간 폭발하고 총알은 계속 날아갑니다.
//   - 5번: 벽에 닿는 순간 분열하고 부모 총알도 계속 날아갑니다. 분열탄도 벽을 관통하므로 8갈래 모두 나오고(벽 쪽 포함), 분열을 일으킨 그 벽
//     콜라이더에는 반응하지 않습니다(그 벽에서 다시 폭발/분열 X - 무한 증식 방지).
//   - 벽에 닿을 때의 작은 피격 이펙트는 지나가는 벽마다 나옵니다.
// - 방 테두리를 넘는 순간 사라집니다(방 밖으로 날아가지 않음). 테두리 벽에 닿을 때의 4번 폭발/5번 분열은 일어나고, 그 뒤 테두리를 넘으면 사라집니다.
//   110번 충전 관통탄도 같은 규칙입니다(Projectile.cs 참고).
// - 35번(벽 튕김)과는 같이 가질 수 없고 서로 등장하지도 않습니다 - 120번 에셋의 Incompatible Item Ids 칸에 35를 적어 둡니다(ItemCompatibility.cs).
// - 48번 등으로 잃으면 그 뒤로 쏘는 총알부터 관통하지 않습니다(날아가는 중인 총알은 그대로 관통).
//
// [아이템 에셋 설정]
// ItemDefinition: Item Id = 120, Category = Passive, Special Effects에 Item120effect 하나, Incompatible Item Ids에 35.
[System.Serializable]
public class Item120effect : IPassiveItemEffect, IRemovablePassiveEffect
{
    private static string SourceKey(string sourceItemId) => "wall_pierce_" + sourceItemId;

    public void Apply(PassiveEffectContext context, string sourceItemId)
    {
        if (context == null || context.Shooting == null) return;

        context.Shooting.OnProjectileSpawned -= HandleProjectileSpawned; // 혹시 남아 있을 이전 구독 정리(중복 방지)
        context.Shooting.OnProjectileSpawned += HandleProjectileSpawned;
        WallPierceOverride.Register(SourceKey(sourceItemId), context.PlayerObject);
    }

    // [패시브 잃기 - 48번 등] 그 뒤로 쏘는 총알부터 관통하지 않습니다.
    public void Remove(PassiveEffectContext context, string sourceItemId)
    {
        if (context?.Shooting != null) context.Shooting.OnProjectileSpawned -= HandleProjectileSpawned;
        WallPierceOverride.Unregister(SourceKey(sourceItemId));
    }

    // 원본 총알과 5번 분열탄 모두 이 알림을 거치므로 분열탄도 관통합니다.
    private void HandleProjectileSpawned(Projectile projectile)
    {
        if (projectile == null) return;
        projectile.SetWallPiercing(true);
    }
}
