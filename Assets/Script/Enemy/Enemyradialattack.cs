using System;
using UnityEngine;
using UnityEngine.AI;

// 충전 -> 360도 전방위 탄환 동시 발사 -> 회전 연출 -> 쿨다운을 하는 독립 공격 컴포넌트입니다(지금은 보스 전용, BossAIController가 BeginAttack 호출).
// 탄환 데미지/크기/색은 이 컴포넌트 값(연사 탄환 EnemyRangedAttack과 별개), 사거리/속도는 EnemyStats 공용 값을 씁니다.
// 강화 총알 외형을 켜면 탄환 구슬을 숨기고 110번 강화 총알 외형(ChargedBulletVisual)으로 그리며, 플레이어가 맞으면 피격 굴절(ChargedHitRipple)이 나옵니다.
// 충전 시작 때 충전 이펙트(chargeVfx, DashChargeVfx와 같은 방식) 재생. 설계 배경은 Docs/설계노트.md [보스 전방위 공격].
[RequireComponent(typeof(NavMeshAgent))]
public class EnemyRadialAttack : MonoBehaviour, IEnemyAttackBehavior, IEnemyColorOverride, ICancellableAttack
{
    private enum AttackPhase { None, Charging, Spinning, Cooldown }

    [Header("사거리 (지금은 미사용 - 일반 적 파이프라인 연결 대비)")]
    public float attackRange = 6f;

    [Header("발사 위치 (비워두면 이 오브젝트 위치)")]
    public Transform firePoint;

    [Header("충전")]
    [Tooltip("발사 전 충전 시간. 이동은 멈추고 목표를 바라보며, 시야를 잃어도 취소되지 않음.")]
    public float chargeDuration = 0.8f;
    [Tooltip("충전 중 목표를 바라보는 회전 속도.")]
    public float facingRotationSpeed = 12f;
    [Tooltip("충전이 시작될 때 재생할 이펙트(돌진 적의 첫 돌진 충전 이펙트와 같은 방식 - 적을 따라다니고 수명 뒤 사라짐, 충전이 끊기면 바로 지움). 비워두면 없음.")]
    public DashChargeVfxSettings chargeVfx = new DashChargeVfxSettings();
    private GameObject chargeVfxInstance;

    [Header("전방위 탄환 (연사 탄환과 별개 수치)")]
    [Tooltip("발사 순간 목표 방향을 0도로 360도를 이 개수만큼 균등 분할해 동시 발사(4발 = 0/90/180/270도).")]
    public int pelletCount = 12;
    [Tooltip("한 발당 데미지(EnemyStats.rangedDamage와 별개).")]
    public float damage = 2f;
    [Tooltip("탄환 크기(EnemyStats.rangedProjectileSize와 별개). 강화 외형도 이 크기에 맞춰 커짐.")]
    public float projectileScale = 0.4f;
    [Tooltip("탄환 색(꼬리도 이 색). 강화 외형 색은 아래 Charged Visual에서 따로 정함.")]
    public Color projectileColor = new Color(1f, 0.2f, 0.8f);

    [Header("강화 총알 외형 (110번 강화 총알 이펙트)")]
    [Tooltip("켜면 탄환 구슬을 숨기고 110번 강화 총알 외형으로 그림 + 플레이어 피격 굴절. 연사 탄환에는 영향 없음.")]
    public bool useChargedBulletVisual = false;
    [Tooltip("외형 세기(110번 충전 수와 같은 의미 - 1 = 기본 밝기, 높을수록 밝고 번개가 늘어남).")]
    [Min(1)] public int chargedVisualLevel = 1;
    [Tooltip("강화 외형 설정(110번과 같은 항목). 기본값은 1레벨 보스 탄환에 맞춘 하늘색.")]
    public ChargedBulletVisual.Settings chargedVisual = ChargedBulletVisual.Settings.CreateCyan();
    [Tooltip("플레이어가 이 탄환에 실제로 피해를 입을 때(무적/보호막/돌진 회피 제외) 플레이어 몸에 나오는 굴절. Material은 110번과 같은 재질을 넣으면 됨.")]
    public ChargedHitRipple.Settings playerHitRipple = ChargedHitRipple.Settings.CreateCyan();

    [Header("발사 후 회전 연출")]
    [Tooltip("이 동안 이동은 멈추고 회전은 이 연출이 전담.")]
    public float spinDuration = 0.5f;
    [Tooltip("spinDuration 동안 회전 바퀴 수(1 = 360도).")]
    public float spinRotations = 1f;

    [Header("공격 종료 후 다음 전방위 공격까지 최소 대기시간")]
    public float postAttackCooldown = 1f;

    [Header("충전 중 색상 피드백")]
    public bool flashOnCharge = true;
    public Color telegraphColor = new Color(1f, 0.2f, 0.8f);
    [Tooltip("자체발광도 함께 올림(흰색에 가까운 색은 _BaseColor 곱만으로는 변화가 안 보여서).")]
    public bool chargeUsesEmission = true;
    [Tooltip("충전 중 자체발광 세기.")]
    public float chargeEmissionBoost = 2f;

    private NavMeshAgent agent;
    private Renderer rend;
    private Color originalColor;
    private bool rendSupportsColor;
    private bool usesBaseColor;
    private bool usesColor;
    private bool usesEmission;
    private Color originalEmission;
    private Transform player;
    private EnemyStats stats;

    // IEnemyColorOverride - EnemyHealth가 피격 페이드 목표 색을 계산할 때 참고합니다.
    private Color currentOverrideColor;
    private Color currentOverrideEmission;

    private AttackPhase phase = AttackPhase.None;
    private float phaseTimer;

    public float AttackRange => attackRange;
    // 충전과 스핀 둘 다 공격 중 - BossAIController가 이 동안 이동/사격/돌진 판단을 쉼(공격끼리 서로 끊지 않음).
    public bool IsAttacking => phase == AttackPhase.Charging || phase == AttackPhase.Spinning;
    public bool CanBeginAttack => phase == AttackPhase.None;
    public event Action OnAttackFinished;

    // 발사 순간 색이 원래대로 돌아가므로 충전 중에만 덮어씁니다.
    public bool HasColorOverride => flashOnCharge && rendSupportsColor && phase == AttackPhase.Charging;
    public Color OverrideColor => currentOverrideColor;
    public Color OverrideEmission => currentOverrideEmission;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();

        rend = GetComponentInChildren<Renderer>();
        if (rend != null)
        {
            var mat = rend.material;
            usesBaseColor = mat.HasProperty("_BaseColor");
            usesColor = mat.HasProperty("_Color");
            rendSupportsColor = usesBaseColor || usesColor;

            if (rendSupportsColor)
            {
                originalColor = usesBaseColor ? mat.GetColor("_BaseColor") : mat.GetColor("_Color");
            }
            else if (flashOnCharge)
            {
                Debug.LogWarning($"EnemyRadialAttack ({gameObject.name}): '{mat.shader.name}' 셰이더에 " +
                                  "_Color/_BaseColor 프로퍼티가 없어 충전 색상 피드백을 표시할 수 없습니다. " +
                                  "flashOnCharge를 꺼서 이 경고를 없앨 수 있습니다.");
            }

            usesEmission = mat.HasProperty("_EmissionColor");
            if (usesEmission)
            {
                originalEmission = mat.GetColor("_EmissionColor");
                mat.EnableKeyword("_EMISSION");
            }
        }

        TryFindPlayer();
    }

    public void Initialize(EnemyStats ownerStats, Transform self)
    {
        stats = ownerStats;
    }

    // targetPosition은 인터페이스 형식용 - 충전 내내 실시간 목표를 다시 보므로 실제 조준에는 쓰지 않습니다.
    public bool BeginAttack(Vector3 targetPosition)
    {
        if (phase != AttackPhase.None) return false;

        phase = AttackPhase.Charging;
        phaseTimer = chargeDuration;

        ForceStayInPlace();

        DashChargeVfx.Stop(ref chargeVfxInstance);
        chargeVfxInstance = DashChargeVfx.Play(chargeVfx, transform);

        return true;
    }

    // 충전/스핀 내내 매 프레임 이동을 멈춥니다.
    // 주의: NavMeshAgent를 끄는(enabled=false) 방식은 쓰지 말 것 - 껐다 켜면 SetDestination 이동(접근/후퇴)만 조용히 먹통이 됨.
    // 주의: 보스 프리팹에 EnemyAIController/EnemyRangedAIController가 같이 붙어 있으면 충전 중에도 움직임 - 붙이지 말 것.
    private void ForceStayInPlace()
    {
        if (agent == null || !agent.enabled) return;

        agent.isStopped = true;
        agent.ResetPath();
        agent.velocity = Vector3.zero;
        agent.updateRotation = false;
    }

    void Update()
    {
        if (player == null) TryFindPlayer();

        switch (phase)
        {
            case AttackPhase.Charging:
                UpdateCharging();
                break;
            case AttackPhase.Spinning:
                UpdateSpinning();
                break;
            case AttackPhase.Cooldown:
                UpdateCooldown();
                break;
        }
    }

    private void UpdateCharging()
    {
        // [112번 투명] 목표가 투명한 플레이어가 되면 충전을 취소합니다(아군을 노리는 중이면 그대로 - 87번).
        if (CombatTargeting.TargetIsInvisiblePlayer(gameObject))
        {
            CancelAttack();
            return;
        }

        // 발사 순간의 조준 방향이 0도 기준이라 회전은 계속 목표를 따라갑니다.
        ForceStayInPlace();
        UpdateFacing();
        UpdateChargeColor();

        phaseTimer -= Time.deltaTime;
        if (phaseTimer > 0f) return;

        FireRadialAndBeginSpin();
    }

    private void UpdateFacing()
    {
        Transform aimTarget = CombatTargeting.CurrentTarget(gameObject, player); // [87번] 플레이어 또는 아군
        if (aimTarget == null) return;

        Vector3 lookDir = aimTarget.position - transform.position;
        lookDir.y = 0f;
        if (lookDir.sqrMagnitude < 0.0001f) return;

        Quaternion targetRot = Quaternion.LookRotation(lookDir);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, facingRotationSpeed * Time.deltaTime);
    }

    private void UpdateChargeColor()
    {
        if (!flashOnCharge || !rendSupportsColor) return;

        float elapsed = chargeDuration - Mathf.Max(phaseTimer, 0f);
        float t = chargeDuration > 0f ? Mathf.Clamp01(elapsed / chargeDuration) : 1f;

        Color c = Color.Lerp(originalColor, telegraphColor, t);
        if (usesBaseColor) rend.material.SetColor("_BaseColor", c);
        if (usesColor) rend.material.SetColor("_Color", c);
        currentOverrideColor = c;

        if (chargeUsesEmission && usesEmission)
        {
            Color e = Color.Lerp(originalEmission, telegraphColor * chargeEmissionBoost, t);
            rend.material.SetColor("_EmissionColor", e);
            currentOverrideEmission = e;
        }
    }

    private void FireRadialAndBeginSpin()
    {
        Transform aimTarget = CombatTargeting.CurrentTarget(gameObject, player); // [87번] 지금의 목표 기준
        Vector3 dir = aimTarget != null ? aimTarget.position - transform.position : transform.forward;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;

        SpawnRadialBurst(dir.normalized);

        if (flashOnCharge && rendSupportsColor)
        {
            if (usesBaseColor) rend.material.SetColor("_BaseColor", originalColor);
            if (usesColor) rend.material.SetColor("_Color", originalColor);

            if (chargeUsesEmission && usesEmission)
            {
                rend.material.SetColor("_EmissionColor", originalEmission);
            }
        }

        phase = AttackPhase.Spinning;
        phaseTimer = spinDuration;
    }

    // 0도부터 등분해야(가운데 기준 부채꼴이 아니라) 원이 이음매 없이 채워집니다.
    private void SpawnRadialBurst(Vector3 centerDirection)
    {
        int pellets = Mathf.Max(pelletCount, 1);
        float step = 360f / pellets;

        for (int i = 0; i < pellets; i++)
        {
            float angle = step * i;
            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * centerDirection;
            SpawnProjectile(dir);
        }
    }

    // EnemyRangedAttack.SpawnProjectile()과 생성 방식은 같지만 이 컴포넌트 값을 씁니다(공용 파일을 안 건드리려고 일부러 복제).
    private void SpawnProjectile(Vector3 direction)
    {
        if (stats == null) return;

        Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position;

        GameObject bulletObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        bulletObj.name = "EnemyRadialProjectile";
        bulletObj.transform.position = spawnPos;
        bulletObj.transform.rotation = Quaternion.LookRotation(direction);
        bulletObj.transform.localScale = Vector3.one * projectileScale;

        var col = bulletObj.GetComponent<Collider>();
        if (col != null) col.isTrigger = true;

        var bulletRend = bulletObj.GetComponent<Renderer>();
        if (bulletRend != null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Standard")
                ?? Shader.Find("Sprites/Default");

            if (shader != null)
            {
                // [87번 포섭] 아군이 쏘면 아군 탄환 색(기본 하늘색). 꼬리는 Launch에서 이 색을 따라갑니다.
                bulletRend.material = new Material(shader) { color = AllyFaction.ProjectileColorFor(gameObject, projectileColor) };
            }
        }

        // 주의: Launch가 localScale을 bulletSize로 다시 덮어쓰므로 projectileScale을 그대로 넘겨야 합니다.
        var projectile = bulletObj.AddComponent<EnemyProjectile>();
        projectile.Launch(direction, damage, stats.rangedAttackRange, stats.rangedProjectileSpeed, projectileScale);
        // [87번 포섭] 아군이 쏜 방사 탄환은 적에게만 맞고 플레이어/아군은 통과합니다.
        if (AllyFaction.IsAlly(gameObject)) projectile.SetFromAlly(true);

        if (useChargedBulletVisual) ApplyChargedVisual(bulletObj, bulletRend, projectile);
    }

    // 구슬은 충돌 판정(Sphere Collider)에 필요해 오브젝트는 남기고 그림만 끕니다. 외형 생성이 실패하면 구슬을 다시 보여 줍니다.
    private void ApplyChargedVisual(GameObject bulletObj, Renderer bulletRend, EnemyProjectile projectile)
    {
        try
        {
            var visual = ChargedBulletVisual.AttachToBullet(bulletObj, chargedVisualLevel, chargedVisual);
            if (visual != null && bulletRend != null) bulletRend.enabled = false;
        }
        catch (Exception e)
        {
            if (bulletRend != null) bulletRend.enabled = true;
            Debug.LogError($"EnemyRadialAttack ({gameObject.name}): 강화 총알 외형 적용 중 오류가 발생해 기본 구슬로 쏩니다: {e}");
        }

        if (playerHitRipple != null && playerHitRipple.enabled)
        {
            var rippleSettings = playerHitRipple;
            projectile.OnHitPlayerDamaging += hitPlayer => ChargedHitRipple.SpawnOnPlayer(hitPlayer, rippleSettings);
        }
    }

    // ---------- 발사 직후 회전 연출 ----------
    private void UpdateSpinning()
    {
        ForceStayInPlace();

        if (spinDuration > 0f)
        {
            float degreesPerSecond = (spinRotations * 360f) / spinDuration;
            transform.Rotate(Vector3.up, degreesPerSecond * Time.deltaTime, Space.World);
        }

        phaseTimer -= Time.deltaTime;
        if (phaseTimer > 0f) return;

        phase = AttackPhase.Cooldown;
        phaseTimer = postAttackCooldown;
        if (agent != null && agent.enabled) agent.isStopped = false; // 스핀이 끝나야 이동을 재개합니다.
    }

    private void UpdateCooldown()
    {
        // [91번 오한 등] 쿨다운이 줄어드는 속도에 공격 속도 배율을 곱합니다(보정이 없으면 1).
        phaseTimer -= Time.deltaTime * EnemyAttackSpeedModifier.GetMultiplier(gameObject);
        if (phaseTimer > 0f) return;

        phase = AttackPhase.None;
        OnAttackFinished?.Invoke();
    }

    // [31번 수면] 수면이 걸리면 충전/스핀 중이던 공격을 즉시 끊고 쿨다운으로(이동 판단은 수면 게이트가 막아 줌).
    public void CancelAttack()
    {
        if (phase != AttackPhase.Charging && phase != AttackPhase.Spinning) return;

        if (phase == AttackPhase.Charging) DashChargeVfx.Stop(ref chargeVfxInstance); // 충전이 끊기면 충전 이펙트도 바로 지움

        if (flashOnCharge && rendSupportsColor)
        {
            if (usesBaseColor) rend.material.SetColor("_BaseColor", originalColor);
            if (usesColor) rend.material.SetColor("_Color", originalColor);

            if (chargeUsesEmission && usesEmission)
            {
                rend.material.SetColor("_EmissionColor", originalEmission);
            }
        }

        phase = AttackPhase.Cooldown;
        phaseTimer = postAttackCooldown;
    }

    // [촬영 스튜디오] 막 시작한 충전을 "이미 elapsed초 충전한 상태"로 바로 맞춥니다(앞 장면의 충전 모습에 이어 붙이기용).
    // 남은 충전 시간, 충전 색, 충전 이펙트(파티클을 elapsed초만큼 미리 돌림)를 함께 맞춥니다. 게임 플레이 코드는 부르지 않습니다.
    public void SkipChargeTime(float elapsed)
    {
        if (phase != AttackPhase.Charging || elapsed <= 0f) return;

        elapsed = Mathf.Min(elapsed, chargeDuration);
        phaseTimer = chargeDuration - elapsed;
        UpdateChargeColor();

        if (chargeVfxInstance == null) return;
        foreach (var ps in chargeVfxInstance.GetComponentsInChildren<ParticleSystem>(true))
        {
            ps.Simulate(elapsed, false, true);
            ps.Play(false);
        }
    }

    private void TryFindPlayer()
    {
        var playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) player = playerObj.transform;
    }
}
