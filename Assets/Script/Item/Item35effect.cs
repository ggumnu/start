using UnityEngine;

// 특수 효과 아이템 예시(1호) - "총알이 벽에 닿으면 튕긴다".
//
// [기획]
// - 어떤 벽이든 닿으면 튕긴다.
// - 적이 적중하면(그대로) 소멸한다 - 이 효과는 관여하지 않음.
// - 튕기는 순간에도 "벽에 닿아 소멸할 때"와 동일한 효과(예: 벽에 닿으면 폭발하는 다른
//   아이템)가 함께 발동해야 한다. 이 효과는 "사거리 소진"이나 "적 적중"에는 반응하지 않는다.
//
// [구현 - Projectile.OnDespawning 훅 재사용]
// PlayerShooting.OnProjectileSpawned를 구독해 새로 생성되는 탄환마다, 그 탄환의
// OnDespawning에 다시 구독을 겁니다(IPassiveItemEffect.cs 상단 주석의 "훅 등록형" 패턴).
// OnDespawning은 사유(Reason)를 함께 넘겨주므로, HitWall일 때만 반응하고 HitEnemy/
// RangeExceeded일 때는 아무것도 하지 않아 기획을 그대로 만족합니다.
//
// [사거리 재계산 방식 - v1.45 변경, 형빈 확정]
// v1.41에서는 튕길 때마다 사거리를 원래 maxRange로 "완전히" 다시 채웠습니다(예: 사거리
// 5가 3만큼 날아가 튕기면 그 지점부터 다시 5). 그런데 이 방식은 벽 두 개가 서로 가깝게
// 마주보는 배치에서 총알이 누가 맞기 전까지 사실상 무한히 튕기는 문제가 있었습니다(매번
// 사거리가 완전히 꽉 채워지니 절대 소진되지 않음). v1.45부터는 튕길 때마다 그 시점의
// maxRange에 RangeMultiplierPerBounce(75%)를 곱해서 줄입니다 - 사거리가 10이면 튕긴
// 뒤에는 7.5, 그다음 또 튕기면 5.625... 식으로 튕길 때마다 계속 작아지다가 결국 0에
// 가까워져 더 이상 튕기지 못하고 소멸합니다. 무한 튕김 없이도 "한동안은 튕기며 오래
// 살아남는" 원래 기획 의도는 유지됩니다.
//
// [조준선 미리보기 연동 - WallBounceOverride]
// AimTrajectoryIndicator.cs가 "지금 조준 중인 방향으로 쏘면 어디서 튕겨서 어디로 갈지"
// 미리보기를 그리려면 이 아이템을 보유 중인지 알아야 합니다. Apply()에서
// WallBounceOverride.SetActive(sourceItemId, ...)를 호출해두면, 조준선 쪽은
// WallBounceOverride.IsActive만 조회해서 이 아이템을 몰라도 미리보기 여부를 판단할 수
// 있습니다(19번의 SplitShotOverride와 동일한 패턴).
//
// [사거리가 튕길 때마다 줄어드는 이유 - 위 v1.45 변경 참고]
// Projectile.Redirect()의 resetDistanceTraveled를 true로 넘기면 내부 누적 이동 거리
// (distanceTraveled)가 0으로 리셋되고, 동시에 rangeMultiplierOnReset을 곱해서 maxRange
// 자체를 줄입니다. 그래서 튕길 때마다 그 지점을 새 발사 지점 삼아 "그 시점 기준으로
// 줄어든" 사거리만큼만 다시 이동할 수 있습니다.
//
// [튕길 때마다 피해량 증가 - v1.46 추가 / 69번 때 곱연산으로 변경, 형빈 확정]
// 벽에 튕길 때마다 이 탄환의 최종 피해가 DamageMultiplierPerBounce(x1.25)씩 곱연산으로 늘어납니다 - 기본 피해가
// 100이면 1번 튕기면 125, 2번 튕기면 156.25입니다(예전 v1.46은 기본 피해의 25%를 더하는 합연산 100 -> 125 -> 150이었음).
// Projectile.AddDamageMultiplierProvider로 등록하고 Projectile.WallBounceCount(벽에 튕긴 횟수)를 읽어서 계산하므로,
// 항상 최신 튕김 횟수를 반영합니다. 곱셈 순서는 (기본 + 11번 거리 보너스 등 덧셈 보너스) x 배율이고, 23번 추가 피해는
// 곱하지 않습니다(Projectile.Damage 주석 참고).
//
// [69번(적에게 맞으면 가장 가까운 적에게 튕김)과 함께 있을 때 - 형빈 확정]
// 벽 튕김은 x1.25, 적 튕김은 x1(피해 변화 없음)입니다. 69번 단독의 적 튕김 감소(x0.25)는 적용되지 않습니다. 이를 위해
// 탄환 생성 때 Projectile.MarkWallBounceEffect()로 "35번이 붙은 총알"이라고 표시해두고, 69번이 그 표시를 보고 감소를
// 건너뜁니다(Item69effect.cs 참고). 사거리는 벽 튕김(35번) x0.75, 적 튕김(69번) x0.75로 각자 줄어듭니다.
//
// [다른 벽-반응 효과와 자동으로 공존하는 이유]
// OnDespawning은 이 효과 혼자만 구독하는 이벤트가 아니라, "벽에 닿으면 폭발" 같은 다른
// 특수효과 아이템도 동시에 구독할 수 있는 공용 훅입니다. 이 효과가 CancelDespawn을
// true로 바꿔 소멸을 취소하는 것과 무관하게, 다른 구독자는 각자 원하는 반응을 그대로
// 실행하므로 "튕길 때마다 다른 벽 충돌 효과도 함께 발동"이 서로 몰라도 자동으로
// 성립합니다. 여러 번 튕기면 그때마다 다시 발동합니다.
[System.Serializable]
public class WallBounceEffect : IPassiveItemEffect, IRemovablePassiveEffect
{
    // [v1.45 추가] 튕길 때마다 사거리(maxRange)에 곱하는 배율입니다. AimTrajectoryIndicator.cs
    // 조준선 미리보기도 이 값을 그대로 참조해서, 튕긴 뒤 조각의 사거리 표시를 실제 총알과
    // 똑같이 줄어들게 맞춥니다.
    public const float RangeMultiplierPerBounce = 0.75f;

    // [69번 때 곱연산으로 변경] 벽에 튕길 때마다 최종 피해에 이 배율을 곱합니다 - 기본 피해 100: 1번 125, 2번 156.25.
    public const float DamageMultiplierPerBounce = 1.25f;

    public void Apply(PassiveEffectContext context, string sourceItemId)
    {
        if (context == null || context.Shooting == null) return;

        WallBounceOverride.SetActive(sourceItemId, context.PlayerObject);
        context.Shooting.OnProjectileSpawned -= HandleProjectileSpawned; // 혹시 남아 있을 이전 구독 정리(중복 방지)
        context.Shooting.OnProjectileSpawned += HandleProjectileSpawned;
    }

    // [패시브 잃기 - 48번 등] 소지 중에만 유지되던 효과를 되돌립니다(IRemovablePassiveEffect.cs 참고). 이미 날아가고 있는
    // 총알에 걸린 효과는 그 총알이 사라질 때까지 남고, 그 뒤로 쏘는 총알부터 적용되지 않습니다.
    public void Remove(PassiveEffectContext context, string sourceItemId)
    {
        WallBounceOverride.RemoveActive(sourceItemId);
        if (context?.Shooting != null) context.Shooting.OnProjectileSpawned -= HandleProjectileSpawned;
    }

    private void HandleProjectileSpawned(Projectile projectile)
    {
        if (projectile == null) return;

        // 람다에서 projectile을 그대로 캡처해뒀다가, 이 탄환이 사라지려는 순간마다(여러 번
        // 튕기면 여러 번) 반사 방향을 다시 계산합니다.
        projectile.OnDespawning += (despawnEvent) => HandleDespawning(projectile, despawnEvent);

        // [v1.46 추가] 튕길 때마다 피해량이 늘어나는 보너스를 등록합니다 - Damage를 읽을
        // 때마다 GetBounceDamageBonus가 호출되어 그 시점의 BounceCount를 기준으로 다시
        // 계산되므로, 나중에 더 튕겨도 자동으로 반영됩니다.
        // [69번 때 곱연산으로 변경] 위 [튕길 때마다 피해량 증가] 주석 참고.
        projectile.AddDamageMultiplierProvider(GetBounceDamageMultiplier);

        // [69번 연동] 이 총알에는 35번이 붙어 있다고 표시합니다 - 69번이 이걸 보고 적 튕김 피해 감소를 건너뜁니다.
        projectile.MarkWallBounceEffect();
    }

    // 위 [튕길 때마다 피해량 증가] 주석 참고 - 상태를 갖지 않는 순수 계산이라 static입니다.
    private static float GetBounceDamageMultiplier(Projectile projectile)
    {
        return Mathf.Pow(DamageMultiplierPerBounce, projectile.WallBounceCount);
    }

    private void HandleDespawning(Projectile projectile, ProjectileDespawnEvent despawnEvent)
    {
        // 기획: 벽에 닿았을 때만 반응. 적 적중/사거리 소진은 건드리지 않고 그대로 둡니다.
        if (despawnEvent.Reason != ProjectileDespawnReason.HitWall) return;

        // [110번 관통 총알] 벽을 관통하는 총알은 튕기지 않습니다(형빈 확정).
        if (despawnEvent.IsPierceHit || projectile.IsPiercing) return;

        despawnEvent.CancelDespawn = true;

        // [v1.43 수정 - 튕긴 방향이 Y축으로 비스듬히 꺾이는 버그, 형빈 확인 요청]
        // despawnEvent.SurfaceNormal이 항상 완전히 수평(Y=0)이라는 보장이 없습니다(모서리를
        // 스치듯 맞았거나 벽 콜라이더 형태에 따라 Y 성분이 섞여 나올 수 있음 - Item05effect.cs가
        // 같은 이유로 이미 이렇게 처리하고 있었는데, 여기는 빠져 있었습니다). 이 게임은 탄환이
        // 항상 수평(Y 고정)으로만 움직이므로, 법선의 Y 성분을 지우고 다시 정규화한 뒤에
        // 반사시켜야 반사 방향도 수평을 유지합니다. 이걸 안 하면 반사 방향에 Y 성분이 섞여
        // 들어가면서 수평 성분(실제 날아가는 방향)이 원래 의도한 반사각과 달라져 버립니다
        // (심하면 플레이어 쪽으로 튀는 것처럼 보일 만큼 어긋남 - 조준선에서 발견된 Y축 꺾임과
        // 같은 원인입니다).
        Vector3 incoming = projectile.Direction;
        incoming.y = 0f;
        if (incoming.sqrMagnitude < 0.0001f) incoming = projectile.Direction;
        else incoming.Normalize();

        Vector3 wallNormal = despawnEvent.SurfaceNormal;
        wallNormal.y = 0f;
        if (wallNormal.sqrMagnitude < 0.0001f) wallNormal = -incoming;
        else wallNormal.Normalize();

        Vector3 reflected = Vector3.Reflect(incoming, wallNormal);
        // [v1.45 변경] 튕긴 지점부터 사거리를 다시 계산하되, RangeMultiplierPerBounce(75%)만큼
        // 줄여서 - 위 [사거리 재계산 방식] 주석 참고.
        projectile.Redirect(reflected, resetDistanceTraveled: true, rangeMultiplierOnReset: RangeMultiplierPerBounce);
    }
}
