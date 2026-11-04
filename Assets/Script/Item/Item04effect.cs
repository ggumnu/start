using UnityEngine;

// 특수 효과 아이템 예시(2호) - "총알이 벽이나 적에 닿으면 폭발한다".
//
// [기획]
// - 벽에 닿거나 적에게 적중하면 그 자리에서 폭발한다.
// - 사거리를 다 써서 저절로 사라질 때는 반응하지 않는다.
//
// [WallBounceEffect와 함께 있을 때]
// 튕기는 아이템과 이 아이템을 같이 보유하면, 튕길 때마다(=벽에 부딪힐 때마다) 이 효과도
// 매번 다시 반응해서 그때마다 폭발합니다 - 이 클래스가 WallBounceEffect를 알아서가 아니라,
// 둘 다 같은 Projectile.OnDespawning을 각자 구독하고 있어서 자동으로 성립하는
// 동작입니다(WallBounceEffect.cs 상단 주석 참고).
//
// [폭발 피해 판정 - Physics.OverlapSphere]
// 폭발 위치를 중심으로 explosionRadius 반경 안의 "Enemy" 태그 콜라이더를 전부 모아 데미지를
// 입힙니다. 탄환에 직접 맞아 이미 데미지를 받은 적이라도 반경 안에 있으면 폭발 피해를
// 한 번 더 받습니다(따로 제외하지 않았습니다 - 필요하면 나중에 조정 가능).
//
// [데미지 값 - 고정값이 아니라 탄환 데미지에 배율]
// Projectile.Damage(그 탄환이 실제로 가진 데미지 - 공격력 스탯/멀티샷 등이 이미 반영된
// 값)에 explosionDamageMultiplier를 곱해서 사용합니다. 그래서 공격력을 올리는 아이템을
// 같이 먹으면 폭발 피해도 자동으로 함께 오릅니다.
[System.Serializable]
public class ExplodeOnImpactEffect : IPassiveItemEffect, IRemovablePassiveEffect
{
    [Header("폭발 범위 / 피해")]
    public float explosionRadius = 2.5f;
    [Tooltip("탄환 자체 데미지에 곱해지는 배율입니다. 1이면 탄환 데미지와 동일한 피해를 폭발로 " +
             "한 번 더 입힙니다.")]
    public float explosionDamageMultiplier = 1f;

    [Header("연출 (선택 - 비워두면 이펙트 없이 피해만 적용)")]
    [Tooltip("폭발 위치에 생성할 이펙트 프리팹입니다. 비워두면 시각 효과 없이 피해만 적용됩니다.")]
    public GameObject explosionVfxPrefab;
    [Tooltip("생성한 이펙트를 이 시간(초) 뒤에 자동으로 파괴합니다. 0 이하로 두면 파괴하지 않고 " +
             "그대로 둡니다(프리팹 자체에 Stop Action 등 자동 소멸 설정이 있는 경우).")]
    public float explosionVfxLifetime = 2f;

    // [38번 매료 / 91번 오한 연동] 이 폭발에 휘말린 적(피해를 받은 뒤, 아직 살아 있는 적)을 폭발 하나당 적마다 한 번씩 알립니다.
    // 총알 명중(Projectile.OnPlayerBulletHitEnemy)처럼 "폭발에 맞으면 상태 이상이 걸리는" 효과가 구독합니다.
    public static event System.Action<EnemyHealth> OnExplosionHitEnemy;

    public void Apply(PassiveEffectContext context, string sourceItemId)
    {
        if (context == null || context.Shooting == null) return;

        context.Shooting.OnProjectileSpawned -= HandleProjectileSpawned; // 혹시 남아 있을 이전 구독 정리(중복 방지)
        context.Shooting.OnProjectileSpawned += HandleProjectileSpawned;
    }

    // [패시브 잃기 - 48번 등] 소지 중에만 유지되던 효과를 되돌립니다(IRemovablePassiveEffect.cs 참고). 이미 날아가고 있는
    // 총알에 걸린 효과는 그 총알이 사라질 때까지 남고, 그 뒤로 쏘는 총알부터 적용되지 않습니다.
    public void Remove(PassiveEffectContext context, string sourceItemId)
    {
        if (context?.Shooting != null) context.Shooting.OnProjectileSpawned -= HandleProjectileSpawned;
    }

    private void HandleProjectileSpawned(Projectile projectile)
    {
        if (projectile == null) return;

        // 람다에서 projectile을 캡처해뒀다가, 이 탄환이 사라지려는 순간(여러 번 튕기면
        // 여러 번) 그때그때의 Damage 값을 기준으로 폭발시킵니다.
        projectile.OnDespawning += (despawnEvent) => HandleDespawning(projectile, despawnEvent);
    }

    private void HandleDespawning(Projectile projectile, ProjectileDespawnEvent despawnEvent)
    {
        // 기획: 벽/적 적중일 때만 반응. 사거리 소진일 때는 아무것도 하지 않습니다.
        if (despawnEvent.Reason != ProjectileDespawnReason.HitWall &&
            despawnEvent.Reason != ProjectileDespawnReason.HitEnemy)
        {
            return;
        }

        // [5번 패시브 연동] 분열탄은 효과 배율(EffectScale)만큼 폭발 피해/범위/이펙트 크기가
        // 줄어듭니다. 원본 탄환은 EffectScale이 1이라 기존과 완전히 동일합니다. 예) 1세대
        // 분열탄: 탄 피해 50% x 폭발 배율 50% = 원본 피해의 25%, 폭발 범위/이펙트 50%.
        // [23번 30색 물감 세트 연동] 피해는 projectile.Damage에 23번 추가 피해가 이미 들어 있습니다. 불꽃 색은 그
        // 총알에 굴려진 23번 색이 있을 때만 바꿉니다(없으면 원래 이펙트 색 그대로).
        bool hasTint = projectile.HasRandomDamageRoll;
        Explode(despawnEvent.Position, projectile.Damage, projectile.EffectScale, hasTint, projectile.RandomDamageColor);
    }

    private void Explode(Vector3 position, float projectileDamage, float effectScale, bool hasTint, Color tint)
    {
        float damage = projectileDamage * explosionDamageMultiplier * effectScale;
        float radius = explosionRadius * effectScale;

        Collider[] hits = Physics.OverlapSphere(position, radius);
        var hitEnemies = new System.Collections.Generic.List<EnemyHealth>();
        foreach (var hit in hits)
        {
            if (!hit.CompareTag("Enemy")) continue;

            EnemyHealth enemyHealth = hit.GetComponent<EnemyHealth>();
            if (enemyHealth != null)
            {
                enemyHealth.TakeDamage(damage);
                if (!hitEnemies.Contains(enemyHealth)) hitEnemies.Add(enemyHealth);
            }
        }

        // [38번/91번 연동] 피해 처리가 끝난 뒤, 살아 있는 적에게만 "폭발에 맞음"을 알립니다(폭발로 죽은 적은 제외).
        if (OnExplosionHitEnemy != null)
        {
            for (int i = 0; i < hitEnemies.Count; i++)
            {
                EnemyHealth e = hitEnemies[i];
                if (e == null || e.IsDead) continue;
                try
                {
                    OnExplosionHitEnemy.Invoke(e);
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"ExplodeOnImpactEffect: 폭발 명중 알림 처리 중 오류가 발생해 이 적은 건너뜁니다: {ex}");
                }
            }
        }

        if (explosionVfxPrefab != null)
        {
            GameObject vfx = Object.Instantiate(explosionVfxPrefab, position, Quaternion.identity);
            // [방 전환] 플레이어가 다른 방에 들어가면 바로 지워지도록 등록합니다(RoomScope - 다른 방 위 허공에 남지 않게).
            RoomScope.DestroyWhenPlayerLeaves(vfx);
            ApplyVfxScale(vfx, effectScale);
            if (hasTint) ApplyVfxTint(vfx, tint);
            if (explosionVfxLifetime > 0f)
            {
                Object.Destroy(vfx, explosionVfxLifetime);
            }
        }
    }

    // 이펙트 크기를 배율만큼 줄입니다. CombatVfxSpawner와 같은 방식으로, 오브젝트의
    // localScale이 아니라 파티클의 시작 크기/속도에 직접 곱합니다(일부 이펙트는 Scaling Mode
    // 때문에 localScale을 바꿔도 반응하지 않기 때문). 파티클이 없는 이펙트만 localScale을
    // 씁니다. 배율이 1이면 아무것도 하지 않습니다.
    private static void ApplyVfxScale(GameObject instance, float multiplier)
    {
        if (instance == null || Mathf.Approximately(multiplier, 1f)) return;

        var systems = instance.GetComponentsInChildren<ParticleSystem>(true);
        if (systems.Length == 0)
        {
            instance.transform.localScale *= multiplier;
            return;
        }

        for (int i = 0; i < systems.Length; i++)
        {
            var main = systems[i].main;

            main.startSizeMultiplier *= multiplier;
            if (main.startSize3D)
            {
                main.startSizeXMultiplier *= multiplier;
                main.startSizeYMultiplier *= multiplier;
                main.startSizeZMultiplier *= multiplier;
            }
            main.startSpeedMultiplier *= multiplier;
        }
    }

    // [23번 30색 물감 세트 연동 - 형빈 확정] 폭발 이펙트의 "색이 있는" 파티클만 23번 색으로 바꿉니다. 흰색 섬광이나
    // 검은 그을음처럼 무채색(채도가 낮거나 아주 어두운) 파티클은 그대로 둡니다. 그래서 이펙트마다 따로 설정할 필요가
    // 없습니다. 생성된 이 폭발 하나의 시작 색(Start Color)만 바꾸고 원본 프리팹은 건드리지 않습니다. 투명도는 원래
    // 값을 유지합니다.
    private const float TintMinSaturation = 0.2f; // 이 채도 미만은 무채색으로 보고 그대로 둠
    private const float TintMinValue = 0.1f;      // 이 밝기 미만(거의 검정)도 그대로 둠

    private static void ApplyVfxTint(GameObject instance, Color tint)
    {
        if (instance == null) return;

        var systems = instance.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < systems.Length; i++)
        {
            var main = systems[i].main;
            main.startColor = TintMinMaxGradient(main.startColor, tint);
        }
    }

    private static ParticleSystem.MinMaxGradient TintMinMaxGradient(ParticleSystem.MinMaxGradient source, Color tint)
    {
        switch (source.mode)
        {
            case ParticleSystemGradientMode.Color:
                return new ParticleSystem.MinMaxGradient(TintColor(source.color, tint));

            case ParticleSystemGradientMode.TwoColors:
                return new ParticleSystem.MinMaxGradient(TintColor(source.colorMin, tint), TintColor(source.colorMax, tint));

            case ParticleSystemGradientMode.Gradient:
                return new ParticleSystem.MinMaxGradient(TintGradient(source.gradient, tint));

            case ParticleSystemGradientMode.TwoGradients:
                return new ParticleSystem.MinMaxGradient(TintGradient(source.gradientMin, tint), TintGradient(source.gradientMax, tint));

            case ParticleSystemGradientMode.RandomColor:
                var random = new ParticleSystem.MinMaxGradient(TintGradient(source.gradient, tint));
                random.mode = ParticleSystemGradientMode.RandomColor;
                return random;

            default:
                return source;
        }
    }

    private static Gradient TintGradient(Gradient source, Color tint)
    {
        if (source == null) return null;

        GradientColorKey[] colorKeys = source.colorKeys;
        for (int i = 0; i < colorKeys.Length; i++)
        {
            colorKeys[i].color = TintColor(colorKeys[i].color, tint);
        }

        var result = new Gradient();
        result.mode = source.mode;
        result.SetKeys(colorKeys, source.alphaKeys);
        return result;
    }

    // 색이 있는 색이면 23번 색으로 바꾸고(투명도 유지), 무채색이면 그대로 돌려줍니다.
    private static Color TintColor(Color source, Color tint)
    {
        Color.RGBToHSV(source, out _, out float saturation, out float value);
        if (saturation < TintMinSaturation || value < TintMinValue) return source;

        return new Color(tint.r, tint.g, tint.b, source.a);
    }
}