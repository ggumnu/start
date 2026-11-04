using System.Collections.Generic;
using UnityEngine;

// 총알 발사/피격/벽 충돌 순간에 에셋 스토어에서 가져온 파티클 이펙트를 재생하는 관리자
// 스크립트입니다. 씬에 딱 한 번만 배치하면 됩니다(피해량 숫자, 적 체력바를 붙인 오브젝트에
// 같이 붙여도 되고, 별도 빈 오브젝트에 붙여도 됩니다).
//
// [지원하는 7가지 상황]
// 1. 플레이어가 총을 발사할 때 (총구)
// 2. 적이 총을 발사할 때 (총구)
// 3. 플레이어가 근접 공격(일반 근접/돌진/접촉형 전부 포함)에 맞았을 때
// 4. 플레이어가 원거리 공격(적 총알)에 맞았을 때
// 5. 적이 플레이어의 총알에 맞았을 때
// 6. 플레이어의 총알이 벽에 닿았을 때
// 7. 적의 총알이 벽에 닿았을 때
//
// [연결 방식 - 기존 코드의 알림(이벤트)을 그대로 활용]
// - 1, 5, 6번은 PlayerShooting.OnProjectileSpawned / Projectile.OnDespawning을 그대로
//   구독합니다(플레이어 발사 스크립트는 원래 있던 이벤트만 쓰고 손대지 않았습니다).
// - 2, 4, 7번은 EnemyRangedAttack.cs / EnemyProjectile.cs에 EnemyHealth.OnAnyEnemyDied와
//   같은 방식의 정적(전역) 이벤트를 새로 추가해서 씁니다.
// - 3번은 EnemyMeleeContactTrigger.cs에 같은 방식의 정적 이벤트를 추가해서 씁니다.
//
// [무적 시간 처리]
// 4번(원거리 피격), 3번(근접 피격)은 실제로 체력이 깎일 때만 이펙트가 뜹니다. 무적 시간
// 중에는 맞아도 이펙트가 뜨지 않습니다 - 해당 이벤트를 발생시키는 쪽(EnemyProjectile.cs/
// EnemyMeleeContactTrigger.cs)에서 PlayerHealth.TakeDamage()와 같은 조건을 미리 확인하고
// 있어서, 이 스크립트는 이벤트가 오면 그냥 재생하기만 하면 됩니다.
//
// [이펙트가 엉뚱한 방향을 볼 때]
// 에셋마다 파티클이 기본적으로 어느 축을 바라보게 만들어졌는지가 달라서, "방향에 맞추기"를
// 켠 항목이 처음엔 눕거나 반대를 볼 수 있습니다. 그럴 땐 그 항목의 "회전 오프셋" 값으로
// 보정하면 됩니다(예: X축 90도를 더하면 90도 눕힙니다).
//
// [총구 이펙트가 총을 따라가는 이유]
// 1번(플레이어 발사), 2번(적 발사) 이펙트는 생성된 뒤 총구(firePoint) Transform의 자식으로
// 붙습니다. 그래서 총이 움직이거나 돌아가는 동안에도 이펙트가 사라질 때까지 총구 위치에 계속
// 붙어서 따라갑니다. 나머지 5가지(피격/벽 충돌)는 그 순간의 위치에 고정된 채로 재생됩니다.
//
// [자동 정리]
// 이펙트는 재생이 끝나면 자동으로 사라집니다. 파티클 재생 길이를 스스로 계산해서 그 시간
// 뒤에 지우고, 계산이 안 되거나(파티클이 없거나 무한 반복이거나) 너무 길게 나오면
// maxEffectLifetime 값으로 상한을 둡니다.
public class CombatVfxSpawner : MonoBehaviour
{
    // 이펙트 하나에 대한 설정 묶음입니다. 7가지 상황 각각이 이 타입의 필드를 하나씩 가집니다.
    [System.Serializable]
    public class VfxSlot
    {
        [Tooltip("재생할 이펙트 프리팹입니다. 비워두면 이 상황에서는 아무것도 재생하지 않습니다.")]
        public GameObject prefab;

        [Tooltip("생성 위치에 더할 오프셋입니다(월드 기준, 미터 단위).")]
        public Vector3 positionOffset = Vector3.zero;

        [Tooltip("자동으로 계산된 방향에 추가로 더할 회전(오일러 각, 도)입니다. 이펙트가 " +
                 "엉뚱한 방향을 보면 여기서 보정하세요.")]
        public Vector3 rotationOffsetEuler = Vector3.zero;

        [Tooltip("이펙트 크기 배율입니다. 1이 원본 크기입니다.")]
        public float scaleMultiplier = 1f;

        [Tooltip("켜면 총알 진행 방향/벽 표면 방향 등 그 상황에서 알 수 있는 방향에 맞춰 " +
                 "회전합니다. 끄면 위 회전 오프셋 값만 그대로 씁니다.")]
        public bool alignToDirection = true;
    }

    [Header("1. 플레이어 발사 - 총구")]
    public VfxSlot playerMuzzleFlash = new VfxSlot();

    [Header("2. 적 발사 - 총구")]
    public VfxSlot enemyMuzzleFlash = new VfxSlot();

    [Header("3. 플레이어가 근접 공격에 맞았을 때")]
    public VfxSlot playerHitByMelee = new VfxSlot();

    [Header("4. 플레이어가 원거리 공격에 맞았을 때")]
    public VfxSlot playerHitByRanged = new VfxSlot();

    [Header("5. 적이 플레이어 총알에 맞았을 때")]
    public VfxSlot enemyHitByPlayerBullet = new VfxSlot();

    [Header("6. 플레이어 총알이 벽에 닿았을 때")]
    public VfxSlot playerBulletHitWall = new VfxSlot();

    [Header("7. 적 총알이 벽에 닿았을 때")]
    public VfxSlot enemyBulletHitWall = new VfxSlot();

    [Header("공통 - 자동 정리")]
    [Tooltip("이펙트 하나가 화면에 남아있을 수 있는 최대 시간(초)입니다. 파티클 재생 길이를 " +
             "계산하지 못했거나(파티클이 없음) 무한 반복으로 설정된 이펙트에도 이 시간이 " +
             "지나면 강제로 사라집니다.")]
    [Min(0.1f)] public float maxEffectLifetime = 5f;

    // 씬 시작 순서에 따라 플레이어가 이 스크립트보다 늦게 생길 수 있어, 못 찾았을 때 계속
    // 재시도합니다(ItemTooltipDisplay.cs 등 기존 HUD 스크립트와 같은 패턴).
    private const float RefSearchInterval = 0.5f;
    private float refSearchTimer = 0f;
    private PlayerShooting playerShooting;

    void OnEnable()
    {
        EnemyRangedAttack.OnAnyEnemyProjectileSpawned += HandleEnemyProjectileSpawned;
        EnemyProjectile.OnAnyEnemyBulletHitPlayer += HandleEnemyBulletHitPlayer;
        EnemyProjectile.OnAnyEnemyBulletHitWall += HandleEnemyBulletHitWall;
        EnemyMeleeContactTrigger.OnAnyMeleeHitPlayer += HandleMeleeHitPlayer;
    }

    void OnDisable()
    {
        EnemyRangedAttack.OnAnyEnemyProjectileSpawned -= HandleEnemyProjectileSpawned;
        EnemyProjectile.OnAnyEnemyBulletHitPlayer -= HandleEnemyBulletHitPlayer;
        EnemyProjectile.OnAnyEnemyBulletHitWall -= HandleEnemyBulletHitWall;
        EnemyMeleeContactTrigger.OnAnyMeleeHitPlayer -= HandleMeleeHitPlayer;

        if (playerShooting != null)
        {
            playerShooting.OnProjectileSpawned -= HandlePlayerProjectileSpawned;
            playerShooting = null;
        }
    }

    void Update()
    {
        if (playerShooting == null)
        {
            refSearchTimer -= Time.deltaTime;
            if (refSearchTimer <= 0f)
            {
                refSearchTimer = RefSearchInterval;
                playerShooting = Object.FindFirstObjectByType<PlayerShooting>();
                if (playerShooting != null)
                {
                    playerShooting.OnProjectileSpawned += HandlePlayerProjectileSpawned;
                }
            }
        }
    }

    // ------------------------------------------------------------------
    // 1, 5, 6번 - 플레이어 발사/피해/벽 충돌 (Projectile.cs의 기존 이벤트 사용)
    // ------------------------------------------------------------------

    private void HandlePlayerProjectileSpawned(Projectile projectile)
    {
        if (projectile == null) return;

        // 1. 총구 이펙트 - 총구(firePoint)가 있으면 그 위치에 붙여서, 이펙트가 사라질 때까지
        // 총구를 따라 움직이고 회전하도록 합니다.
        Transform muzzleFollow = (playerShooting != null) ? playerShooting.firePoint : null;
        SpawnEffect(playerMuzzleFlash, projectile.transform.position, projectile.Direction, muzzleFollow);

        // 이 총알 하나에서 나중에 일어날 벽 충돌/적중을 구독합니다. 총알이 사라지면(Destroy)
        // 이 구독도 함께 사라지므로 따로 해제할 필요가 없습니다.
        projectile.OnDespawning += HandlePlayerProjectileDespawning;
    }

    private void HandlePlayerProjectileDespawning(ProjectileDespawnEvent e)
    {
        if (e.Reason == ProjectileDespawnReason.HitWall)
        {
            // 6. 플레이어 총알이 벽에 닿았을 때
            SpawnEffect(playerBulletHitWall, e.Position, e.SurfaceNormal);
        }
        else if (e.Reason == ProjectileDespawnReason.HitEnemy)
        {
            // 5. 적이 플레이어 총알에 맞았을 때
            SpawnEffect(enemyHitByPlayerBullet, e.Position, e.Position - (e.HitObject != null ? e.HitObject.transform.position : e.Position));
        }
    }

    // ------------------------------------------------------------------
    // 2, 4, 7번 - 적 발사/피해/벽 충돌 (새로 추가한 정적 이벤트 사용)
    // ------------------------------------------------------------------

    private void HandleEnemyProjectileSpawned(Transform anchor, Vector3 direction)
    {
        // 2. 총구 이펙트 - anchor(적의 총구, 없으면 몸통)에 붙여서 이펙트가 사라질 때까지
        // 그 위치를 따라가도록 합니다.
        Vector3 position = (anchor != null) ? anchor.position : Vector3.zero;
        SpawnEffect(enemyMuzzleFlash, position, direction, anchor);
    }

    private void HandleEnemyBulletHitPlayer(Vector3 position, Vector3 direction)
    {
        // 4. 플레이어가 원거리 공격에 맞았을 때 (무적 시간 중이면 이 이벤트 자체가 오지 않습니다)
        SpawnEffect(playerHitByRanged, position, direction);
    }

    private void HandleEnemyBulletHitWall(Vector3 position, Vector3 normal)
    {
        // 7. 적 총알이 벽에 닿았을 때
        SpawnEffect(enemyBulletHitWall, position, normal);
    }

    // ------------------------------------------------------------------
    // 3번 - 플레이어가 근접 공격에 맞았을 때 (새로 추가한 정적 이벤트 사용)
    // ------------------------------------------------------------------

    private void HandleMeleeHitPlayer(Vector3 position, Vector3 direction)
    {
        // 무적 시간 중이면 이 이벤트 자체가 오지 않습니다.
        SpawnEffect(playerHitByMelee, position, direction);
    }

    // ------------------------------------------------------------------
    // 공통 - 이펙트 생성/자동 정리
    // ------------------------------------------------------------------

    // followTarget을 넘기면(현재는 1, 2번 총구 이펙트만 해당), 생성한 이펙트를 그 Transform의
    // 자식으로 붙여서 이펙트가 사라질 때까지 총구 위치/회전을 계속 따라가도록 합니다.
    // worldPositionStays: true로 붙이므로, 붙는 순간의 월드 위치/회전(=아래서 계산한 position,
    // rotation)은 그대로 유지된 채 이후부터만 부모를 따라갑니다.
    private void SpawnEffect(VfxSlot slot, Vector3 position, Vector3 direction, Transform followTarget = null)
    {
        if (slot == null || slot.prefab == null) return;

        Quaternion rotation = (slot.alignToDirection && direction.sqrMagnitude > 0.0001f)
            ? Quaternion.LookRotation(direction.normalized)
            : Quaternion.identity;
        rotation *= Quaternion.Euler(slot.rotationOffsetEuler);

        GameObject instance = Instantiate(slot.prefab, position + slot.positionOffset, rotation);
        ApplyScale(instance, slot.scaleMultiplier);

        if (followTarget != null)
        {
            instance.transform.SetParent(followTarget, true);
        }

        Destroy(instance, ComputeEffectDuration(instance));
    }

    // Scale Multiplier를 오브젝트 크기(Transform.localScale)가 아니라 파티클 자체의 "시작
    // 크기"에 직접 곱합니다. 에셋에 따라 파티클 시스템의 Scaling Mode가 "Shape"거나, 크기가
    // 부모(루트) 크기를 따라가지 않는 "Local" 모드의 자식으로 만들어져 있는 경우가 있는데,
    // 그런 이펙트는 루트 오브젝트의 Transform.localScale을 바꿔도 전혀 반응하지 않습니다.
    // 파티클의 시작 크기/속도를 직접 곱하는 이 방식은 Scaling Mode/Simulation Space 설정과
    // 무관하게 항상 적용됩니다. 파티클 시스템이 하나도 없는(순수 3D 모델 등) 이펙트에서만
    // 예외적으로 기존 방식(Transform.localScale)을 씁니다.
    private void ApplyScale(GameObject instance, float multiplier)
    {
        if (Mathf.Approximately(multiplier, 1f)) return;

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

            // 입자 하나하나의 크기만 줄이면 "작아진 입자가 그대로 멀리 퍼지는" 부자연스러운
            // 모습이 될 수 있어, 퍼지는 속도도 함께 줄여 이펙트 전체 크기가 줄어든 것처럼
            // 보이게 합니다.
            main.startSpeedMultiplier *= multiplier;
        }
    }

    // 이펙트 프리팹 안의 파티클 시스템들을 훑어서 "재생이 끝나는 데 걸리는 대략적인 시간"을
    // 계산합니다. 무한 반복(loop)이거나 계산이 애매한 경우(커브 기반 수명)에는 안전하게
    // maxEffectLifetime을 그대로 씁니다 - 정확한 값이 아니라 "언젠가는 확실히 지운다"가
    // 목적인 안전장치입니다.
    private float ComputeEffectDuration(GameObject instance)
    {
        var systems = instance.GetComponentsInChildren<ParticleSystem>(true);
        if (systems.Length == 0) return maxEffectLifetime;

        float longest = 0f;
        for (int i = 0; i < systems.Length; i++)
        {
            var main = systems[i].main;

            if (main.loop)
            {
                longest = maxEffectLifetime;
                break;
            }

            float lifetime;
            switch (main.startLifetime.mode)
            {
                case ParticleSystemCurveMode.Constant:
                    lifetime = main.startLifetime.constant;
                    break;
                case ParticleSystemCurveMode.TwoConstants:
                    lifetime = main.startLifetime.constantMax;
                    break;
                default:
                    // 커브 기반 수명은 정확히 계산하기 번거로워 안전하게 상한값을 씁니다.
                    lifetime = maxEffectLifetime;
                    break;
            }

            float duration = main.duration + lifetime;
            if (duration > longest) longest = duration;
        }

        return Mathf.Clamp(longest, 0.05f, maxEffectLifetime);
    }
}
