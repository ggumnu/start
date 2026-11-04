using UnityEngine;

// 패시브 아이템 11번 - "탄환이 날아간 거리에 따라 피해가 달라진다. 기준 거리보다 가까이서
// 맞히면 피해가 줄고, 더 멀리서 맞히면 피해가 는다."
//
// [효과]
// 탄환이 "실제로 움직인 거리"(Projectile.DistanceTraveled)를 기준으로 피해가 바뀝니다.
// 적과 플레이어 사이의 직선 거리가 아닙니다. Projectile은 매 물리 프레임 실제로 이동한 거리를
// 계속 누적하고 있어서(35번 벽 튕김 아이템 때부터), 벽에 튕겨서 돌아 맞는 경우처럼 가까운
// 적이라도 튕긴 거리만큼 이동한 것으로 계산됩니다. 발사 위치(총구)에서 0부터 시작합니다.
//
// [계산 - 기준 거리(referenceDistance, 기본 1)를 중심으로]
// - 기준 거리보다 멀리 날아간 경우: 초과한 거리 distanceStep(0.1)마다 damagePerStep(0.1)만큼
//   피해가 늘어납니다. 추가 피해 = (이동 거리 - 기준 거리) / distanceStep x damagePerStep.
// - 기준 거리보다 적게 날아간 경우: 못 날아간 거리 shortfallStep(0.1)마다
//   damageLossPerShortfallStep(0.2)만큼 피해가 줄어듭니다.
//   감소 피해 = (기준 거리 - 이동 거리) / shortfallStep x damageLossPerShortfallStep.
// - 기준 거리와 정확히 같으면 기본 피해 그대로입니다. 모두 부드럽게 비례합니다(단계로 끊지 않음).
// 예) 기본 피해 10, 기본 설정일 때: 이동 1.0 -> 10, 0.9 -> 9.8, 0.1 -> 8.2, 1.5 -> 10.5.
//
// [피해 하한선]
// 피해 감소가 있어도 탄환 피해는 플레이어 스탯의 "데미지 하한선"(PlayerStats.statFloors에서
// StatType.Damage에 설정된 값) 밑으로 내려가지 않습니다. 하한선 설정이 없는 경우에만
// fallbackMinDamage(기본 1)를 대신 씁니다. 하한선은 탄환이 생성되는 순간 Projectile에
// 설정되고, Projectile.Damage가 항상 그 값 이상을 돌려줍니다.
//
// [폭발 피해에도 적용됩니다 - 형빈 확정]
// 이 효과는 탄환의 "피해 보너스 제공자"로 등록됩니다(Projectile.AddDamageBonusProvider).
// Projectile.Damage를 읽을 때마다 그 시점의 이동 거리로 계산된 값이 나오는데, 4번
// 아이템(폭발)이 폭발 피해를 정할 때 이 Projectile.Damage를 읽으므로 4번 스크립트를
// 수정하지 않아도 폭발에 자동으로 반영됩니다(감소도 마찬가지). 폭발 피해는
// (보정된 피해 x 폭발 배율)로 계산됩니다.
//
// [이 파일의 역할]
// 인스펙터 설정값만 들고 있고, 아이템을 획득하면 Apply()가 한 번 호출되어 이후 발사되는 모든
// 탄환에 보너스 제공자와 최소 피해를 등록합니다. 값은 매번 읽으므로 Play 중에 인스펙터 값을
// 바꾸면 곧바로 반영됩니다(이미 날아가고 있는 탄환에도 적용. 단, 피해 하한선은 탄환이
// 생성될 때 정해진 값 그대로입니다).
//
// [에디터 설정 방법]
// ItemDefinition 에셋 - Special Effects 리스트에 "+"를 눌러 DistanceDamageEffect를 고르면
// 아래 필드들이 나타납니다.
[System.Serializable]
public class DistanceDamageEffect : IPassiveItemEffect, IRemovablePassiveEffect
{
    [Header("기준 거리")]
    [Tooltip("피해가 늘고 주는 기준이 되는 이동 거리(월드 단위)입니다. 이 거리만큼 날아가서 " +
             "맞히면 기본 피해 그대로입니다.")]
    public float referenceDistance = 1f;

    [Header("기준보다 멀리 날아갔을 때 (피해 증가)")]
    [Tooltip("기준 거리를 넘어 이 거리(월드 단위)를 더 날아갈 때마다 아래 '단위당 추가 피해'만큼 " +
             "피해가 늘어납니다. 부드럽게 비례하므로 이 값보다 짧은 거리도 비례해서 반영됩니다.")]
    public float distanceStep = 0.1f;
    [Tooltip("거리 단위(Distance Step)만큼 더 날아갈 때마다 늘어나는 피해량입니다.")]
    public float damagePerStep = 0.1f;

    [Header("기준보다 적게 날아갔을 때 (피해 감소)")]
    [Tooltip("기준 거리에 이 거리(월드 단위)만큼 못 미칠 때마다 아래 '단위당 감소 피해'만큼 " +
             "피해가 줄어듭니다. 부드럽게 비례합니다.")]
    public float shortfallStep = 0.1f;
    [Tooltip("부족한 거리 단위(Shortfall Step)마다 줄어드는 피해량입니다.")]
    public float damageLossPerShortfallStep = 0.2f;

    [Header("피해 하한선")]
    [Tooltip("플레이어 스탯(PlayerStats.statFloors)에 데미지 하한선이 설정돼 있지 않을 때만 " +
             "대신 쓰는 최소 피해입니다. 하한선이 설정돼 있으면 그 값이 우선합니다.")]
    public float fallbackMinDamage = 1f;

    // Apply에서 받아두는 플레이어 스탯입니다(피해 하한선을 읽는 용도). 직렬화되지 않습니다.
    [System.NonSerialized] private PlayerStats stats;

    public void Apply(PassiveEffectContext context, string sourceItemId)
    {
        if (context == null || context.Shooting == null) return;

        stats = context.Stats;
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

        // 피해 감소가 있어도 스탯의 데미지 하한선 밑으로는 내려가지 않게 합니다.
        float minDamage = fallbackMinDamage;
        if (stats != null && stats.TryGetStatFloor(StatType.Damage, out float floor))
        {
            minDamage = floor;
        }
        // [5번 패시브 연동] 분열탄은 피해 자체가 원본의 일부(50% 등)라서, 하한선도 같은 효과
        // 배율만큼 줄이지 않으면 하한선이 분열탄 피해를 끌어올려 "50%" 규칙이 깨집니다. 원본
        // 탄환은 EffectScale이 1이라 기존과 완전히 동일합니다.
        projectile.SetMinDamage(minDamage * projectile.EffectScale);

        projectile.AddDamageBonusProvider(GetBonusDamage);
    }

    // Projectile.Damage를 읽을 때마다 호출됩니다. 그 순간까지 실제로 이동한 거리가 기준입니다.
    // 기준 거리 이상이면 양수(증가), 미만이면 음수(감소)를 돌려줍니다.
    private float GetBonusDamage(Projectile projectile)
    {
        if (projectile == null) return 0f;

        float distance = projectile.DistanceTraveled;

        // [5번 패시브 연동] 분열탄은 증가/감소 피해 모두 효과 배율(EffectScale)만큼만
        // 받습니다. 원본 탄환은 1이라 기존과 완전히 동일합니다. 거리는 분열탄이 태어난
        // 지점부터 새로 누적되므로(DistanceTraveled가 0부터) 별도 처리가 필요 없습니다.
        float scale = projectile.EffectScale;

        if (distance >= referenceDistance)
        {
            float step = Mathf.Max(0.0001f, distanceStep);
            return (distance - referenceDistance) / step * damagePerStep * scale;
        }

        float shortStep = Mathf.Max(0.0001f, shortfallStep);
        return -((referenceDistance - distance) / shortStep * damageLossPerShortfallStep) * scale;
    }
}
