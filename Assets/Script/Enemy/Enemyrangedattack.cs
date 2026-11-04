using System;
using System.Collections;
using UnityEngine;

// 원거리 적의 사격 조각입니다: 공격 속도 쿨다운 + 연사(burstCount틱을 burstInterval 간격) + 틱마다 부채꼴 산탄(pelletCount발을 totalSpreadAngle 안에 등분).
// "언제 쏠지"(사거리/시야)는 AI 컨트롤러(EnemyRangedAIController/BossAIController)가 판단하고 TryFire만 부릅니다.
// 연사는 시작하면 시야가 막혀도 끝까지 쏘며, 두 번째 틱부터 그 순간의 목표 위치로 다시 조준합니다(목표가 투명한 플레이어면 직전 방향 유지 - 112번).
// 탄환은 구체 프리미티브 + 빨간 계열 색(플레이어 총알과 구분)으로 직접 만듭니다. 설계 배경은 Docs/설계노트.md [적 원거리 사격].
public class EnemyRangedAttack : MonoBehaviour
{
    // [VFX 연동] 발사 순간(총구 Transform, 방향) 알림 - 총구 이펙트가 총구를 따라가도록 Transform을 넘깁니다(CombatVfxSpawner가 구독).
    public static event Action<Transform, Vector3> OnAnyEnemyProjectileSpawned;

    [Header("발사 위치 (비워두면 이 오브젝트 위치를 그대로 사용)")]
    [Tooltip("모델의 무기 위치에 둔 자식 오브젝트(PlayerShooting의 firePoint와 같은 개념).")]
    public Transform firePoint;

    [Header("탄환 시각 설정")]
    [Tooltip("플레이어 총알과 구분되도록 기본값은 빨간색.")]
    public Color projectileColor = new Color(1f, 0.15f, 0.15f);
    [Tooltip("탄환 구체 기본 지름(최종 크기는 EnemyStats.rangedProjectileSize가 정함).")]
    public float baseProjectileScale = 0.4f;

    [Header("연사 (일반 적은 EnemyDefinition의 ShootAttackConfig가 스폰 때 덮어씀)")]
    [Tooltip("한 번 공격에 쏘는 틱 수(1 = 한 번).")]
    public int burstCount = 1;
    [Tooltip("연사 틱 사이 간격(초).")]
    public float burstInterval = 0.08f;

    [Header("샷건 (일반 적은 EnemyDefinition의 ShootAttackConfig가 스폰 때 덮어씀)")]
    [Tooltip("틱마다 부채꼴로 동시에 쏘는 발 수(1 = 중앙 한 발).")]
    public int pelletCount = 1;
    [Tooltip("가장 왼쪽 탄과 오른쪽 탄 사이 전체 각도(도).")]
    public float totalSpreadAngle = 0f;

    private EnemyStats stats;
    private float fireCooldownTimer = 0f;
    private bool isBursting = false;
    private Transform player;

    // 쿨다운뿐 아니라 연사 진행 중인지도 봅니다(연사 도중 새 연사 금지).
    public bool IsReady => !isBursting && fireCooldownTimer <= 0f;
    public bool IsBursting => isBursting;

    void Awake()
    {
        TryFindPlayer();
    }

    // AI 컨트롤러가 Awake에서 호출합니다.
    public void Initialize(EnemyStats ownerStats)
    {
        stats = ownerStats;
    }

    void Update()
    {
        // [91번 오한 등] 쿨다운이 줄어드는 속도에 공격 속도 배율을 곱합니다(보정이 없으면 1).
        if (fireCooldownTimer > 0f) fireCooldownTimer -= Time.deltaTime * EnemyAttackSpeedModifier.GetMultiplier(gameObject);
    }

    // 쿨다운 중이거나 연사 중이면 무시하고 false. 재진입 방지는 이 컴포넌트 책임(컨트롤러는 매 프레임 반환값을 안 보고 부를 수 있음).
    public bool TryFire(Vector3 direction)
    {
        if (stats == null) return false;
        if (isBursting) return false;
        if (fireCooldownTimer > 0f) return false;

        RestartCooldown();
        StartCoroutine(FireBurst(direction));
        return true;
    }

    // 공격 주기를 처음부터 다시 셉니다(1 / rangedAttackSpeed초 뒤에 다음 연사 가능).
    public void RestartCooldown()
    {
        if (stats == null) return;
        fireCooldownTimer = 1f / Mathf.Max(stats.rangedAttackSpeed, 0.01f);
    }

    // [38번 매료 등] 진행 중인 연사를 즉시 멈춥니다(이 컴포넌트의 코루틴은 FireBurst뿐이라 StopAllCoroutines로 안전).
    public void CancelBurst()
    {
        if (!isBursting) return;
        StopAllCoroutines();
        isBursting = false;
    }

    // [2레벨 보스 후퇴 샷건] 진행 중인 연사를 끊고, 지정한 발 수/각도/데미지/탄환 크기/탄속으로 부채꼴을 한 번 즉시 쏜 뒤 공격 주기를 처음부터 다시 셉니다.
    // 쿨다운과 무관하게 바로 쏩니다(쏠지 말지는 호출하는 컨트롤러가 정함). 탄환 색/사거리는 평소 연사와 같습니다.
    public bool FireSpreadNow(Vector3 direction, int pellets, float spreadAngle, float damage, float projectileSize, float projectileSpeed)
    {
        if (stats == null) return false;

        CancelBurst();
        SpawnPelletSpread(direction, Mathf.Max(pellets, 1), spreadAngle, damage, projectileSize, projectileSpeed);
        RestartCooldown();
        return true;
    }

    private IEnumerator FireBurst(Vector3 initialDirection)
    {
        isBursting = true;
        int ticksToFire = Mathf.Max(burstCount, 1);

        Vector3 aimDirection = initialDirection;
        for (int i = 0; i < ticksToFire; i++)
        {
            // [112번 투명 / 87번] 목표가 투명한 플레이어면 다시 조준하지 않고 직전 방향 그대로(아군을 노리는 중이면 계속 조준).
            if (i > 0 && !CombatTargeting.TargetIsInvisiblePlayer(gameObject)) aimDirection = GetCurrentAimDirection(aimDirection);
            SpawnPelletSpread(aimDirection, Mathf.Max(pelletCount, 1), totalSpreadAngle, stats.rangedDamage, stats.rangedProjectileSize,
                              stats.rangedProjectileSpeed);

            if (i < ticksToFire - 1)
            {
                yield return new WaitForSeconds(burstInterval);
            }
        }

        isBursting = false;
    }

    // -총합/2 ~ +총합/2를 (N-1)등분해 동시 발사(예: 60도·5발 = -30/-15/0/+15/+30도). 수평(Y축)으로만 벌립니다.
    private void SpawnPelletSpread(Vector3 centerDirection, int pellets, float spreadAngle, float damage, float projectileSize, float projectileSpeed)
    {
        if (pellets <= 1)
        {
            SpawnProjectile(centerDirection, damage, projectileSize, projectileSpeed);
            return;
        }

        float step = spreadAngle / (pellets - 1);
        float startAngle = -spreadAngle / 2f;

        for (int i = 0; i < pellets; i++)
        {
            float angle = startAngle + step * i;
            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * centerDirection;
            SpawnProjectile(dir, damage, projectileSize, projectileSpeed);
        }
    }

    // 그 순간의 목표 위치로 방향을 다시 계산합니다(못 찾으면 fallback). 시야는 보지 않습니다 - 벽이 막으면 탄환이 벽에서 멈춤.
    private Vector3 GetCurrentAimDirection(Vector3 fallback)
    {
        if (player == null) TryFindPlayer();
        Transform aimTarget = CombatTargeting.CurrentTarget(gameObject, player); // [87번] 플레이어 또는 아군/적
        if (aimTarget == null) return fallback;

        Vector3 origin = firePoint != null ? firePoint.position : transform.position;
        Vector3 dir = aimTarget.position - origin;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return fallback;

        return dir.normalized;
    }

    private void SpawnProjectile(Vector3 direction, float damage, float projectileSize, float projectileSpeed)
    {
        Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position;

        GameObject bulletObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        bulletObj.name = "EnemyProjectile";
        bulletObj.transform.position = spawnPos;
        bulletObj.transform.rotation = Quaternion.LookRotation(direction);
        bulletObj.transform.localScale = Vector3.one * baseProjectileScale;

        var col = bulletObj.GetComponent<Collider>();
        if (col != null) col.isTrigger = true;

        var rend = bulletObj.GetComponent<Renderer>();
        if (rend != null)
        {
            // 주의: URP에서 Standard 셰이더는 분홍색이 되므로 URP 셰이더부터 찾습니다.
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Standard")
                ?? Shader.Find("Sprites/Default");

            if (shader != null)
            {
                // [87번 포섭] 아군이 쏘면 아군 탄환 색(기본 하늘색). 꼬리는 Launch에서 이 색을 따라갑니다.
                rend.material = new Material(shader) { color = AllyFaction.ProjectileColorFor(gameObject, projectileColor) };
            }
        }

        var projectile = bulletObj.AddComponent<EnemyProjectile>();
        projectile.Launch(direction, damage, stats.rangedAttackRange, projectileSpeed, projectileSize);
        // [87번 포섭] 아군이 쏜 탄환은 적에게만 맞고 플레이어/다른 아군은 통과합니다(피해는 이미 x30 환산된 rangedDamage).
        if (AllyFaction.IsAlly(gameObject)) projectile.SetFromAlly(true);

        // firePoint가 비어 있어도 이펙트가 최소한 이 적을 따라가도록 몸통 Transform을 넘깁니다.
        OnAnyEnemyProjectileSpawned?.Invoke(firePoint != null ? firePoint : transform, direction);
    }

    // 스폰 순서상 Awake 때 플레이어가 아직 없을 수 있어, 찾을 때까지 조용히 재시도합니다.
    private void TryFindPlayer()
    {
        var playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) player = playerObj.transform;
    }
}
