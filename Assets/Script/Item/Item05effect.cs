using UnityEngine;

// 패시브 아이템 5번 - "이 아이템을 소지하고 있을 때, 플레이어의 탄환이 적이나 벽에 닿으면
// 탄환이 8갈래의 작은 탄환으로 나뉘어 퍼진다."
//
// [기획 - 형빈]
// - 적/벽에 닿는 순간 그 지점에서 수평 8갈래(45도 간격)로 분열합니다. 사거리를 다 써서 저절로
//   사라질 때는 분열하지 않습니다.
// - 분열탄은 그 지점에서 "새로 발사된" 탄환으로 취급합니다. 이동 거리는 0부터, 사거리도 분열
//   지점부터 계산합니다(11번 거리 비례 피해도 분열 지점부터 잽니다).
// - 분열탄의 피해/사거리는 부모의 50%입니다. 기준은 부모가 발사될 때의 기본 값(BaseDamage,
//   MaxRange)이라, 11번의 거리 보너스가 자식에게 이월되지 않습니다.
//   예) 기본 피해 10 / 사거리 10 -> 1세대 5 / 5 -> 2세대 2.5 / 2.5
// - 아이템 효과도 부모의 50%입니다(Projectile.EffectScale = 0.5의 세대 제곱). 4번 폭발은 탄환
//   피해 50% x 효과 50% = 원본의 25%, 폭발 범위/이펙트 크기도 50%. 11번 거리 보너스/페널티도
//   50%입니다. 이 배율은 4번/11번 스크립트가 각자 EffectScale을 곱해서 적용합니다.
// - 분열탄에도 4번(폭발)/35번(튕김)/11번(거리 피해)/38번(매료)/5번 자신(재분열)이 그대로
//   적용됩니다. 분열탄도 원본과 똑같이 PlayerShooting.OnProjectileSpawned를 거쳐서 생성되기
//   때문입니다(PlayerShooting.SpawnSplitProjectile 참고). 38번은 전역 명중 이벤트를 쓰므로
//   따로 수정 없이 원본과 동일하게 매료를 겁니다.
// - 분열의 원인이 된 부모가 맞힌 적은 그 분열탄에 맞지 않습니다(통과). 같은 분열에서 나온
//   형제 탄들끼리는 같은 적을 각자 맞힐 수 있습니다(형빈 확정).
// - 분열은 최대 maxGeneration세대까지입니다(기본 2: 원본 -> 작은 탄 -> 더 작은 탄). 마지막
//   세대의 탄환은 더 분열하지 않습니다. 추가 안전장치로 살아있는 분열탄이
//   maxLiveSplitProjectiles개를 넘으면 더 분열하지 않습니다.
//
// [벽에서 분열할 때 - 형빈 확정: 360도 8갈래, 벽 쪽 탄은 제거]
// 분열 위치는 벽 표면에서 법선 방향으로 wallSpawnOffset만큼 띄운 곳입니다(벽 콜라이더에 파묻혀
// 태어나 즉시 다시 벽에 닿는 것을 방지). 8갈래는 튕겨나가는 방향(반사 방향)을 기준으로 45도씩
// 돌려 만들고, 벽 쪽을 향하거나 벽과 나란한 방향(벽 법선과의 내적이 minWallDot 이하)은 만들지
// 않습니다. 또 아주 가까운 거리(wallProbeDistance)에 벽이 있는 방향도 만들지 않습니다.
// 그래서 벽에서는 실제로 3~5발 정도만 나갑니다.
//
// [적에게서 분열할 때]
// 진행 방향을 기준으로 45도씩 돌려 8갈래를 만듭니다. 한 갈래는 진행 방향 그대로 나가지만,
// 부모가 맞힌 적은 통과하므로 그 적에게 다시 맞지는 않습니다. 근처 벽이 바로 앞을 막고 있는
// 방향은 위와 같은 이유로 만들지 않습니다.
//
// [35번(튕김)과 함께 있을 때]
// 원본이 벽에 튕기면 튕길 때마다 분열합니다(4번 폭발과 똑같이 각 아이템이 독립적으로
// 반응하는 기존 규칙). 아이템 획득 순서와 상관없이 충돌 당시의 방향(IncomingDirection)을
// 기준으로 하므로 결과가 같습니다.
//
// [120번(벽 관통)과 함께 있을 때 - 형빈 확정]
// 부모가 120번 총알(벽 관통)이면 분열탄도 벽을 관통하므로, 벽에서 분열할 때 벽 쪽을 포함한 8갈래가 모두 나오고 "바로 앞에 벽이 있는 방향"도
// 빼지 않습니다(적에게서 분열할 때도). 대신 벽에서 생긴 분열탄은 자기를 만든 그 벽 콜라이더에는 반응하지 않습니다(그 벽에서 다시 4번 폭발/
// 5번 분열 X - 벽 쪽으로 나간 분열탄이 그 자리에서 마구 증식하지 않도록). 부모 총알은 벽을 관통해 계속 날아갑니다(Projectile.cs 참고).
//
// [110번(충전 사격)과 함께 있을 때]
// 강화 총알이 분열하면 분열탄에도 강화 외형(ChargedBulletVisual)이 붙습니다(크기는 분열탄 크기에 맞춰 작아짐, 번개는 줄여서).
//
// [에디터 설정 방법]
// ItemDefinition 에셋 - Item Id = 5, Category = Passive, Special Effects 리스트에 "+"를 눌러
// SplitOnImpactEffect를 고르면 아래 필드들이 나타납니다.
[System.Serializable]
public class SplitOnImpactEffect : IPassiveItemEffect, IRemovablePassiveEffect
{
    [Header("분열 개수 / 세대")]
    [Tooltip("한 번 분열할 때 나뉘는 탄환 수입니다(수평 360도를 이 수로 등분). 벽에서는 벽 쪽을 향하는 탄이 빠져서 실제로는 더 적게 나갑니다.")]
    [Min(1)] public int splitCount = 8;
    [Tooltip("분열이 일어나는 최대 세대입니다. 2면 원본 -> 작은 탄 -> 더 작은 탄까지 만들어지고, 더 작은 탄은 분열하지 않습니다. 1이면 재분열이 없습니다.")]
    [Min(1)] public int maxGeneration = 2;
    [Tooltip("살아있는 분열탄이 이 수를 넘으면 더 분열하지 않습니다(연쇄 분열로 탄환이 폭증하는 것을 막는 안전장치).")]
    [Min(1)] public int maxLiveSplitProjectiles = 200;

    [Header("분열탄 배율 (부모 탄환 대비)")]
    [Tooltip("분열탄 피해 = 부모의 발사 당시 기본 피해 x 이 값입니다.")]
    public float damageRatio = 0.5f;
    [Tooltip("분열탄 사거리 = 부모의 최대 사거리 x 이 값입니다. 분열 지점부터 계산합니다.")]
    public float rangeRatio = 0.5f;
    [Tooltip("분열탄 크기(눈에 보이는 크기와 충돌 범위) = 부모 크기 x 이 값입니다.")]
    public float sizeRatio = 0.5f;
    [Tooltip("분열탄이 가지는 아이템 효과 배율 = 부모의 효과 배율 x 이 값입니다. 4번 폭발 피해/범위/이펙트, 11번 거리 보너스에 적용됩니다.")]
    public float effectRatio = 0.5f;

    [Header("생성 위치 / 벽 처리")]
    [Tooltip("벽에서 분열할 때 분열탄을 벽 표면에서 이만큼(월드 단위) 띄워서 만듭니다.")]
    public float wallSpawnOffset = 0.15f;
    [Tooltip("분열 지점에서 이 거리 안에 벽이 있는 방향으로는 분열탄을 만들지 않습니다.")]
    public float wallProbeDistance = 0.25f;
    [Tooltip("벽에서 분열할 때, 벽 법선과 이 값(내적) 이하로만 벌어진 방향은 만들지 않습니다. 0이면 벽과 정확히 나란한 방향부터 제외되고, 값이 클수록 벽과 얕은 각도의 방향도 더 제외합니다.")]
    [Range(0f, 0.5f)] public float minWallDot = 0.05f;

    // Apply에서 받아두는 참조입니다(분열탄 생성용). 직렬화되지 않습니다.
    [System.NonSerialized] private PlayerShooting shooting;

    public void Apply(PassiveEffectContext context, string sourceItemId)
    {
        if (context == null || context.Shooting == null) return;

        shooting = context.Shooting;
        shooting.OnProjectileSpawned -= HandleProjectileSpawned; // 혹시 남아 있을 이전 구독 정리(중복 방지)
        shooting.OnProjectileSpawned += HandleProjectileSpawned;
    }

    // [패시브 잃기 - 48번 등] 소지 중에만 유지되던 효과를 되돌립니다(IRemovablePassiveEffect.cs 참고). 잃은 뒤로는 이미
    // 날아가고 있던 총알도 더 이상 분열하지 않습니다(분열탄을 만드는 shooting 참조를 비우므로).
    public void Remove(PassiveEffectContext context, string sourceItemId)
    {
        if (shooting != null) shooting.OnProjectileSpawned -= HandleProjectileSpawned;
        if (context?.Shooting != null) context.Shooting.OnProjectileSpawned -= HandleProjectileSpawned;
        shooting = null;
    }

    private void HandleProjectileSpawned(Projectile projectile)
    {
        if (projectile == null) return;

        // 원본 탄환뿐 아니라 분열탄도 이 이벤트로 들어오므로, 분열탄에도 똑같이 구독이 걸려
        // 재분열(세대 상한까지)이 자연스럽게 성립합니다.
        projectile.OnDespawning += (despawnEvent) => HandleDespawning(projectile, despawnEvent);
    }

    private void HandleDespawning(Projectile projectile, ProjectileDespawnEvent despawnEvent)
    {
        if (projectile == null || shooting == null) return;

        // 기획: 벽/적 적중일 때만 분열. 사거리 소진일 때는 아무것도 하지 않습니다.
        bool hitWall = despawnEvent.Reason == ProjectileDespawnReason.HitWall;
        bool hitEnemy = despawnEvent.Reason == ProjectileDespawnReason.HitEnemy;
        if (!hitWall && !hitEnemy) return;

        // [63번 미사일] 처음 쏜 미사일만 한 번 분열합니다(분열 미사일은 다시 분열하지 않음 - 사거리 제한이 없어서, 형빈 확정).
        if (projectile.IsMissile && projectile.Generation >= 1) return;

        if (projectile.Generation >= maxGeneration) return;
        if (Projectile.LiveSplitCount + splitCount > maxLiveSplitProjectiles) return;

        // 충돌 당시의 진행 방향(수평)입니다. 35번이 먼저 처리되어 탄환 방향이 이미 바뀌었어도
        // 이 값은 그대로입니다.
        Vector3 incoming = despawnEvent.IncomingDirection;
        incoming.y = 0f;
        if (incoming.sqrMagnitude < 0.0001f)
        {
            incoming = projectile.Direction;
            incoming.y = 0f;
        }
        if (incoming.sqrMagnitude < 0.0001f) return;
        incoming.Normalize();

        // [120번] 부모가 벽 관통 총알이면 분열탄도 벽을 관통합니다(같은 120번 효과가 붙음) - 벽 방향을 빼지 않습니다.
        bool childrenPierceWalls = projectile.IsWallPiercing;

        Vector3 origin = despawnEvent.Position;
        Vector3 baseDirection = incoming;
        Vector3 wallNormal = Vector3.zero;
        EnemyHealth enemyToIgnore = null;

        if (hitWall)
        {
            wallNormal = despawnEvent.SurfaceNormal;
            wallNormal.y = 0f;
            if (wallNormal.sqrMagnitude < 0.0001f) wallNormal = -incoming;
            wallNormal.Normalize();

            // 법선은 벽 바깥(탄환이 온 쪽)을 향해야 합니다. 반대로 나왔으면 뒤집습니다.
            if (Vector3.Dot(wallNormal, incoming) > 0f) wallNormal = -wallNormal;

            origin += wallNormal * wallSpawnOffset;
            baseDirection = Vector3.Reflect(incoming, wallNormal);
        }
        else if (despawnEvent.HitObject != null)
        {
            // 부모가 맞힌 적은 분열탄이 통과합니다.
            enemyToIgnore = despawnEvent.HitObject.GetComponent<EnemyHealth>();
        }

        int count = Mathf.Max(1, splitCount);
        float angleStep = 360f / count;

        float childDamage = projectile.BaseDamage * damageRatio;
        float childRange = projectile.MaxRange * rangeRatio;
        float childSize = projectile.BulletSize * sizeRatio;
        float childEffectScale = projectile.EffectScale * effectRatio;
        int childGeneration = projectile.Generation + 1;

        // [63번 미사일] 분열 미사일 8갈래 - 8방향으로 잠깐 직진한 뒤 각자 유도합니다(벽은 무시하므로 벽 방향도 뺄 필요 없음).
        // 피해/효과 배율/크기는 일반 분열탄과 같은 규칙(부모의 50%)이고, 실제 생성은 HomingMissile이 합니다.
        if (projectile.IsMissile)
        {
            HomingMissile.SpawnSplitMissiles(projectile, shooting, origin, baseDirection, count, childDamage, childSize,
                                            childGeneration, childEffectScale, enemyToIgnore, maxLiveSplitProjectiles);
            return;
        }

        for (int i = 0; i < count; i++)
        {
            if (Projectile.LiveSplitCount >= maxLiveSplitProjectiles) break;

            Vector3 direction = Quaternion.AngleAxis(angleStep * i, Vector3.up) * baseDirection;

            // 벽 쪽을 향하거나 벽과 나란한 방향은 만들지 않습니다. [120번] 분열탄이 벽을 관통하면 만듭니다.
            if (!childrenPierceWalls && hitWall && Vector3.Dot(direction, wallNormal) <= minWallDot) continue;

            // 바로 앞에 벽이 있는 방향도 만들지 않습니다(벽에서든 적에게서든 공통). [120번] 분열탄이 벽을 관통하면 만듭니다.
            if (!childrenPierceWalls && IsBlockedByWall(origin, direction)) continue;

            Projectile child = shooting.SpawnSplitProjectile(origin, direction, childDamage, childRange, projectile.Speed,
                                                             childSize, childGeneration, childEffectScale, enemyToIgnore);

            // [110번 연동] 부모가 강화 총알이면 분열탄에도 강화 외형을 붙입니다(형빈 확정 - 분열해도 둥근 총알로 바뀌지 않게).
            ChargedBulletVisual.CopyToSplit(projectile, child);

            // [120번] 벽에서 생긴 분열탄은 자기를 만든 벽에 반응하지 않습니다(그 벽에서 다시 폭발/분열 X).
            if (hitWall && child != null && child.PiercesWalls && despawnEvent.HitObject != null)
            {
                child.AddIgnoredWall(despawnEvent.HitObject);
            }
        }
    }

    // origin에서 direction 방향으로 wallProbeDistance 안에 "Wall" 태그 콜라이더가 있는지
    // 검사합니다(트리거 콜라이더는 무시).
    private bool IsBlockedByWall(Vector3 origin, Vector3 direction)
    {
        if (wallProbeDistance <= 0f) return false;

        if (Physics.Raycast(origin, direction, out RaycastHit hit, wallProbeDistance,
                            Physics.AllLayers, QueryTriggerInteraction.Ignore))
        {
            return hit.collider != null && hit.collider.CompareTag("Wall");
        }

        return false;
    }
}
