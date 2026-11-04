using UnityEngine;

// 패시브 69번 - "총알이 적에게 맞으면, 가장 가까운 다른 적에게 튕겨 날아간다."
//
// [기획 - 형빈 확정]
// - 적에게 맞았을 때만 튕깁니다(벽/사거리 소진에는 관여하지 않음 - 벽 튕김은 35번).
// - 튕기는 방향은 맞은 각도와 무관하게 "맞은 지점에서 가장 가까운 살아있는 적" 쪽입니다(방금 맞은 적은 제외).
//   수면/해킹/매료 중인 적, 보스도 포함합니다. 다른 적이 없으면(적이 하나뿐이면) 튕기지 않고 그대로 소멸합니다.
// - 사거리가 닿지 않는 적이라도 그 방향으로 날아갑니다(가다가 사거리가 다 되면 사라짐).
// - 가장 가까운 적이 벽 너머에 있어도 그 방향으로 날아갑니다. 벽에 닿으면 35번이 있으면 튕기고, 없으면 사라집니다.
// - 사거리: 적에게 튕길 때마다 그 시점의 사거리에 rangeMultiplierPerBounce(x0.75)를 곱합니다(35번 벽 튕김과 같은 방식).
//   35번과 같이 있으면 벽 튕김 x0.75(35번), 적 튕김 x0.75(69번)로 각자 줄어듭니다.
// - 피해: 적에게 튕길 때마다 최종 피해에 damageMultiplierPerBounce(x0.25)를 곱합니다(곱연산) - 100 -> 25 -> 6.25 -> 1.5625.
//   튕긴 뒤의 피해가 minDamageToBounce(1) 미만이 되면 튕기지 않고 그 적에게 맞은 자리에서 소멸합니다(위 예시는 4번째 적까지
//   맞히고 소멸 - 다음 피해 0.39). 판정은 최소 피해 하한(11번 등)과 23번 추가 피해를 적용하기 전 값으로 합니다
//   (Projectile.UnclampedDamage - 하한 때문에 1 아래로 안 내려가 영원히 튕기는 일을 막기 위함).
// - 35번과 같이 있으면: 적 튕김의 피해 감소가 적용되지 않습니다(x1). 벽 튕김은 35번대로 x1.25입니다. 이 판단은 "지금 35번을
//   들고 있는지"가 아니라 총알마다(Projectile.HasWallBounceEffect) 합니다 - 날아가는 도중 35번을 잃어도 쏠 때 규칙 유지.
//
// [구현 - 35번과 같은 "훅 등록형"]
// PlayerShooting.OnProjectileSpawned로 새 총알마다 OnDespawning을 구독하고, HitEnemy일 때 CancelDespawn = true로 소멸을
// 취소한 뒤 Projectile.RedirectFromEnemy로 방향을 바꿉니다. 피해 감소는 Projectile.AddDamageMultiplierProvider로 등록합니다.
// - 방금 맞은 적은 다른 적을 맞히거나 벽에 튕기기 전까지 통과합니다(콜라이더를 빠져나가는 도중 다시 맞는 것 방지 -
//   Projectile.RedirectFromEnemy 참고). 적 두 마리 사이를 오가며 튕기는 것(A -> B -> A)은 가능합니다.
// - 4번 폭발/5번 분열/38번 매료/68번 넉백은 각자 같은 순간을 구독하므로, 적에게 튕길 때마다 그대로 함께 발동합니다.
//   4번 폭발 피해는 튕기기 전(맞은 순간)의 피해 기준입니다(Projectile.RequestDespawn의 [피해 고정] 참고).
// - 5번 분열탄도 이 총알 생성 이벤트를 거치므로 69번이 그대로 붙습니다.
// - 조준선 미리보기는 없습니다(튕길 방향이 맞는 순간의 적 위치로 정해져서 미리 알 수 없음).
// - 다른 방(꺼져 있는 방)의 적은 비활성 상태라 찾지 않습니다 - 사실상 지금 방의 적만 대상입니다.
// - 다른 효과가 이미 이 적중의 소멸을 취소했다면(관통 등 - 현재는 없음) 69번은 끼어들지 않습니다.
// - 잃으면(48번 교체 등) 그 뒤로 쏘는 총알부터 적용되지 않습니다(이미 날아가는 총알은 그대로 - 35번 등과 같은 관례).
//
// [아이템 에셋 설정]
// ItemDefinition: Item Id = 69, Category = Passive, Special Effects에 EnemyChainBounceEffect 하나. Stat Modifiers는 비워둡니다.
[System.Serializable]
public class EnemyChainBounceEffect : IPassiveItemEffect, IRemovablePassiveEffect
{
    [Tooltip("적에게 튕길 때마다 그 시점의 사거리에 곱하는 배율입니다(기본 0.75 = 25% 감소, 35번 벽 튕김과 동일).")]
    [Range(0.01f, 1f)] public float rangeMultiplierPerBounce = 0.75f;

    [Tooltip("적에게 튕길 때마다 최종 피해에 곱하는 배율입니다(기본 0.25 - 100 -> 25 -> 6.25). 35번이 붙은 총알에는 적용되지 않습니다.")]
    [Range(0f, 1f)] public float damageMultiplierPerBounce = 0.25f;

    [Tooltip("튕긴 뒤의 피해가 이 값 미만이 되면 튕기지 않고 소멸합니다(기본 1).")]
    [Min(0f)] public float minDamageToBounce = 1f;

    public void Apply(PassiveEffectContext context, string sourceItemId)
    {
        if (context == null || context.Shooting == null) return;

        context.Shooting.OnProjectileSpawned -= HandleProjectileSpawned; // 혹시 남아 있을 이전 구독 정리(중복 방지)
        context.Shooting.OnProjectileSpawned += HandleProjectileSpawned;
    }

    // [패시브 잃기 - 48번 등] 이미 날아가는 총알에 걸린 효과는 그 총알이 사라질 때까지 남고, 그 뒤로 쏘는 총알부터 적용되지 않습니다.
    public void Remove(PassiveEffectContext context, string sourceItemId)
    {
        if (context?.Shooting != null) context.Shooting.OnProjectileSpawned -= HandleProjectileSpawned;
    }

    private void HandleProjectileSpawned(Projectile projectile)
    {
        if (projectile == null) return;

        // 총알마다 생성 시점의 설정값을 담아둡니다(나중에 인스펙터 값이 바뀌어도 이미 쏜 총알은 그대로).
        float damageMultiplier = damageMultiplierPerBounce;
        float rangeMultiplier = rangeMultiplierPerBounce;
        float minDamage = minDamageToBounce;

        projectile.AddDamageMultiplierProvider(p => GetDamageMultiplier(p, damageMultiplier));
        projectile.OnDespawning += (despawnEvent) => HandleDespawning(projectile, despawnEvent, damageMultiplier, rangeMultiplier, minDamage);
    }

    // 35번이 붙은 총알이면 x1(감소 없음), 아니면 적에게 튕긴 횟수만큼 곱합니다.
    private static float GetDamageMultiplier(Projectile projectile, float damageMultiplier)
    {
        if (projectile.HasWallBounceEffect) return 1f;
        return Mathf.Pow(damageMultiplier, projectile.EnemyBounceCount);
    }

    private static void HandleDespawning(Projectile projectile, ProjectileDespawnEvent despawnEvent,
                                         float damageMultiplier, float rangeMultiplier, float minDamage)
    {
        if (projectile == null) return;

        // 기획: 적에게 맞았을 때만 반응합니다.
        if (despawnEvent.Reason != ProjectileDespawnReason.HitEnemy) return;

        // [110번 관통 총알] 적을 관통하는 총알은 튕기지 않습니다(형빈 확정).
        if (despawnEvent.IsPierceHit || projectile.IsPiercing) return;

        // 다른 효과가 이미 소멸을 취소했다면 끼어들지 않습니다(위 [구현] 주석 참고).
        if (despawnEvent.CancelDespawn) return;

        EnemyHealth hitEnemy = despawnEvent.HitObject != null ? despawnEvent.HitObject.GetComponent<EnemyHealth>() : null;

        // 튕긴 뒤의 피해가 하한 미만이면 튕기지 않고 소멸합니다. 35번이 붙은 총알은 적 튕김으로 피해가 줄지 않으므로 검사하지 않습니다.
        if (!projectile.HasWallBounceEffect)
        {
            float nextDamage = projectile.UnclampedDamage * damageMultiplier;
            if (nextDamage < minDamage) return;
        }

        Vector3 origin = despawnEvent.Position;
        EnemyHealth target = FindNearestEnemy(origin, hitEnemy);
        if (target == null) return; // 다른 적이 없으면 그대로 소멸

        Vector3 toTarget = target.transform.position - origin;
        toTarget.y = 0f; // 총알은 항상 수평으로만 움직입니다(35번과 동일).
        if (toTarget.sqrMagnitude < 0.0001f)
        {
            // 목표 적이 맞은 지점과 거의 같은 위치(겹쳐 있음)면 진행 방향 그대로 날아가게 합니다.
            toTarget = despawnEvent.IncomingDirection;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude < 0.0001f) return;
        }

        despawnEvent.CancelDespawn = true;
        projectile.RedirectFromEnemy(toTarget.normalized, rangeMultiplier, hitEnemy);
    }

    // origin에서 가장 가까운 살아있는 적을 찾습니다(excluded 제외, 수평 거리 기준). 꺼져 있는 방의 적은 비활성이라 제외됩니다.
    private static EnemyHealth FindNearestEnemy(Vector3 origin, EnemyHealth excluded)
    {
        EnemyHealth[] enemies = Object.FindObjectsByType<EnemyHealth>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        EnemyHealth nearest = null;
        float nearestSqr = float.MaxValue;

        for (int i = 0; i < enemies.Length; i++)
        {
            EnemyHealth enemy = enemies[i];
            if (enemy == null || enemy == excluded) continue;
            if (enemy.IsDead || !enemy.isActiveAndEnabled) continue;
            if (AllyFaction.IsAlly(enemy.gameObject)) continue; // [87번] 포섭된 아군에게는 튕기지 않습니다.

            Vector3 offset = enemy.transform.position - origin;
            offset.y = 0f;
            float sqr = offset.sqrMagnitude;
            if (sqr < nearestSqr)
            {
                nearestSqr = sqr;
                nearest = enemy;
            }
        }

        return nearest;
    }
}
