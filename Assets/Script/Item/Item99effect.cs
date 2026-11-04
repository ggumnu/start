using UnityEngine;

// 패시브 99번 - "총알이 적을 관통한다."
//
// [기획 - 형빈 확정]
// - 보유하는 동안 쏘는 모든 총알(19번/137번 갈래 총알, 5번 분열탄, 110번 강화 총알과 그 분열탄 포함)이 적을 관통합니다.
//   적에게 닿아도 사라지지 않고 사거리 끝까지 날아갑니다. 벽에는 평소처럼 사라지고, 35번이 있으면 벽에서 튕깁니다(35번과 함께 사용 가능).
// - 관통할 때마다 줄지 않은 같은 피해를 주고, 적에게 닿았을 때의 효과(4번 폭발/5번 분열/38번 매료/68번 넉백/91번 오한 등)도 닿을 때마다
//   그대로 일어납니다.
// - 총알 하나는 같은 적을 한 번만 맞힙니다(적 몸 안을 지나가는 동안 계속 피해를 주지 않음). 단 벽에 튕기면 그 기록이 비워져서, 튕겨 돌아온
//   총알은 아까 뚫은 적을 다시 맞힐 수 있습니다(형빈 확정). 실제 처리는 Projectile.SetEnemyPiercing 참고.
// - 110번 강화 총알 자체의 관통(4발 이상 충전 - 적과 벽 모두 관통, 같은 적 한 번만)은 별개 규칙이라 그대로입니다.
// - 69번(적 튕김)과는 같이 가질 수 없고 서로 등장하지도 않습니다 - 99번 에셋의 Incompatible Item Ids 칸에 69를 적어 둡니다
//   (ItemCompatibility.cs 참고).
// - 48번 등으로 잃으면 그 뒤로 쏘는 총알부터 관통하지 않습니다(날아가는 중인 총알은 그대로 관통).
//
// [아이템 에셋 설정]
// ItemDefinition: Item Id = 99, Category = Passive, Special Effects에 Item99effect 하나, Incompatible Item Ids에 69.
[System.Serializable]
public class Item99effect : IPassiveItemEffect, IRemovablePassiveEffect
{
    public void Apply(PassiveEffectContext context, string sourceItemId)
    {
        if (context == null || context.Shooting == null) return;

        context.Shooting.OnProjectileSpawned -= HandleProjectileSpawned; // 혹시 남아 있을 이전 구독 정리(중복 방지)
        context.Shooting.OnProjectileSpawned += HandleProjectileSpawned;
    }

    // [패시브 잃기 - 48번 등] 그 뒤로 쏘는 총알부터 관통하지 않습니다.
    public void Remove(PassiveEffectContext context, string sourceItemId)
    {
        if (context?.Shooting != null) context.Shooting.OnProjectileSpawned -= HandleProjectileSpawned;
    }

    // 원본 총알과 5번 분열탄 모두 이 알림을 거치므로 분열탄도 관통합니다.
    private void HandleProjectileSpawned(Projectile projectile)
    {
        if (projectile == null) return;
        projectile.SetEnemyPiercing(true);
    }
}
