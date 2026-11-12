using System;
using System.Collections.Generic;
using UnityEngine;

// 해킹툴 전개(3키 모드 + 좌클릭, 1개 소모): deployDelay 뒤 그 순간 플레이어 위치 기준 원기둥 범위의 적에게 해킹 데미지 + 해킹(강력 제어 -
// 규칙은 EnemyControlState) + 범위 안 적 총알 제거 + 이펙트. 사용 동작 중에는 연타/모드 전환이 미뤄짐(효과 발동 뒤엔 1/2번으로 끊기 가능).
// 공용 처리 ApplyHackArea(66번 수류탄 해킹도 사용), 단일 대상 ApplyHackToEnemy(81번+66번), 아이템 훅 OnHackDeployed(직접 사용 때만).
// [2레벨 보스] 해킹툴을 직접 써서 보호막에 맞히면 보호막에 최대 보호막의 일정 비율 추가 피해(BossShield.ApplyHackToolBonus).
// 설계 배경은 Docs/설계노트.md [해킹툴].
[RequireComponent(typeof(PlayerStats))]
public class PlayerHackTool : MonoBehaviour
{
    [Header("해킹 색")]
    [Tooltip("해킹된 적이 해킹되어 있는 동안 표시할 색입니다(발광 없이 색만 바뀜 - 매료/수면과 같은 방식). " +
             "매료/수면 색보다 항상 우선해서 보입니다.")]
    public Color hackColor = new Color(0.2f, 0.9f, 1f);

    [Header("판정")]
    [Tooltip("피해/해킹을 줄 적 콜라이더의 태그입니다(총알/수류탄과 동일).")]
    public string enemyTag = "Enemy";

    [Tooltip("범위 판정 원기둥의 높이(m)입니다. 바닥 원과 같은 반경의 원기둥 안에 몸이 걸친 적이 대상입니다.")]
    [Min(0.1f)] public float effectHeight = 3f;

    [Header("전개 이펙트 (플레이어 위치에 1회)")]
    [Tooltip("해킹툴을 전개할 때 플레이어 위치에 생성할 이펙트 프리팹입니다. 비워두면 이펙트 없이 효과만 적용됩니다.")]
    public GameObject deployVfxPrefab;
    [Tooltip("전개 이펙트를 플레이어 발 위치에서 얼마나 옮겨서 생성할지입니다(월드 기준, m).")]
    public Vector3 deployVfxOffset = Vector3.zero;
    [Tooltip("기본 해킹 범위일 때의 전개 이펙트 크기 배율입니다(범위가 커지면 그 비율만큼 자동으로 같이 커짐). 1이면 프리팹 원래 크기입니다(파티클 크기/속도 배율을 직접 곱하는 방식).")]
    [Min(0.01f)] public float deployVfxScale = 1f;
    [Tooltip("전개 이펙트를 이 시간(초) 뒤에 파괴합니다. 0 이하면 파괴하지 않습니다.")]
    public float deployVfxLifetime = 2f;
    [Tooltip("켜두면 전개 이펙트가 생성된 뒤에도 플레이어를 따라다닙니다.")]
    public bool deployVfxFollowPlayer = false;

    [Header("적별 해킹 이펙트 (해킹이 걸린 적마다)")]
    [Tooltip("해킹이 걸린 적 각각의 위치에 생성할 이펙트 프리팹입니다. 비워두면 이펙트 없이 색 변화만 적용됩니다.")]
    public GameObject enemyVfxPrefab;
    [Tooltip("적별 이펙트를 적 위치에서 얼마나 옮겨서 생성할지입니다(월드 기준, m).")]
    public Vector3 enemyVfxOffset = Vector3.zero;
    [Tooltip("적별 이펙트 크기 배율입니다. 1이면 프리팹 원래 크기입니다.")]
    [Min(0.01f)] public float enemyVfxScale = 1f;
    [Tooltip("적별 이펙트를 이 시간(초) 뒤에 파괴합니다. 0 이하면 파괴하지 않습니다.")]
    public float enemyVfxLifetime = 1.5f;
    [Tooltip("켜두면 적별 이펙트가 그 적을 따라다닙니다(적이 죽어 사라지면 함께 사라짐).")]
    public bool enemyVfxFollowEnemy = true;

    [Header("적 총알 제거 (해킹 효과 발동 순간, 같은 범위)")]
    [Tooltip("켜면 해킹 효과가 발동하는 순간, 적을 해킹하는 것과 같은 범위(반경 HackRadius, 높이 effectHeight) 안의 " +
             "적 총알(보스 전방위 탄환 포함)을 모두 없앱니다. 범위 안에 적이 없어도 동작합니다.")]
    public bool clearEnemyBullets = true;
    [Tooltip("없어지는 총알마다 그 위치에 생성할 이펙트 프리팹입니다. 비워두면 이펙트 없이 그냥 사라집니다.")]
    public GameObject bulletClearVfxPrefab;
    [Tooltip("총알 제거 이펙트를 총알 위치에서 얼마나 옮겨서 생성할지입니다(월드 기준, m).")]
    public Vector3 bulletClearVfxOffset = Vector3.zero;
    [Tooltip("총알 제거 이펙트 크기 배율입니다. 1이면 프리팹 원래 크기입니다.")]
    [Min(0.01f)] public float bulletClearVfxScale = 1f;
    [Tooltip("총알 제거 이펙트를 이 시간(초) 뒤에 파괴합니다. 0 이하면 파괴하지 않습니다.")]
    public float bulletClearVfxLifetime = 1f;

    [Header("해킹 지속 이펙트 (해킹되어 있는 동안 적에게 계속 붙어 있음)")]
    [Tooltip("해킹이 걸려 있는 동안 적에게 붙여둘 이펙트입니다. 이펙트의 색/크기/분사율 등은 프리팹을 복제해서 " +
             "그 프리팹에서 직접 수정하세요. 해킹이 끝나거나 수면으로 바뀌거나 적이 죽으면 자동으로 꺼집니다. " +
             "위의 \"적별 해킹 이펙트\"(걸리는 순간 한 번)와는 별개라 함께 써도 됩니다.")]
    public EnemyStatusVfxSettings hackStatusVfx = new EnemyStatusVfxSettings();

    [Header("사용 동작 타이밍 (애니메이션에 맞춰 조절)")]
    [Tooltip("좌클릭 후 피해/해킹 효과가 실제로 발동되기까지의 시간(초)입니다. 사용 애니메이션(Hacking 클립)에서 " +
             "해킹이 발동되는 순간에 맞추세요. 0이면 클릭 즉시 발동합니다. 전개 이펙트도 이 순간에 생성됩니다.")]
    [Min(0f)] public float deployDelay = 0.5f;

    [Tooltip("사용 동작 전체 시간(초)입니다. 클릭부터 이 시간이 지날 때까지는 다음 사용과 무기 전환이 막힙니다" +
             "(효과 발동 뒤에는 1번/2번으로 끊고 전환 가능). 사용 애니메이션(Hacking 클립) 길이에 맞추세요. " +
             "deployDelay보다 짧으면 deployDelay까지 기다립니다.")]
    [Min(0f)] public float deployDuration = 1.67f;

    [Header("애니메이션 (선택)")]
    [Tooltip("전개할 때 재생할 Animator 트리거 이름입니다. Animator에 이 이름의 Trigger 파라미터가 없으면 " +
             "아무것도 하지 않습니다(오류 없음).")]
    public string deployAnimatorTrigger = "Hack";

    [Header("참조 (비워두면 자동으로 찾습니다)")]
    public PlayerStats stats;
    public PlayerResources resources;
    public PlayerHealth playerHealth;
    public Animator animator;

    // ---- 아이템용 훅 ----
    public event Action<Vector3, float, IReadOnlyList<EnemyHealth>> OnHackDeployed;
    // [58번 때 추가] 클릭해서 해킹툴 1개가 실제로 소모된 순간(효과 발동 전) 울립니다. 58번은 이 순간 보호막을 줍니다
    // (형빈 확정 - 급할 때 보호막을 바로 얻도록 발동이 아니라 소모 순간 기준). 테스트용 "자원 무한"이어도 사용에 성공하면 울립니다.
    public event Action OnHackToolSpent;

    // ---- 다른 스크립트가 읽는 상태 ----
    public bool IsHackMode => weaponMode != null && weaponMode.IsHack;
    // 아이템 획득 연출 중(PlayerItemPickupPresenter)에는 범위 표시도 띄우지 않습니다(전개 불가).
    public bool IsAimingHack => IsHackMode && Input.GetMouseButton(1) && !PlayerItemPickupPresenter.BlocksActions;
    public float CurrentRadius => stats != null ? Mathf.Max(stats.HackRadius, 0f) : 0f;
    public int HackToolCount => resources != null ? resources.GetAmount(BasicItemType.HackingTool) : 0;

    // 사용 동작 중(클릭 ~ deployDuration)이면 true입니다. PlayerWeaponMode가 이 동안 모드 전환을 미룹니다.
    public bool IsDeploying => isDeploying;

    // 효과가 이미 발동됐고 사용 동작만 남은 상태면 true입니다. 이때만 PlayerWeaponMode가 1번/2번 입력으로
    // 사용 동작을 끊을 수 있습니다(발동 전에는 끊을 수 없음 - 수류탄 투척 캔슬과 동일, 형빈 확정).
    public bool CanInterruptDeploy => isDeploying && !effectPending;

    // 남은 사용 동작을 즉시 끝냅니다(사용 캔슬). 효과가 아직 발동되지 않았으면 아무것도 하지 않습니다.
    // 사용 대기시간도 함께 사라지므로, 다시 해킹툴 모드로 돌아오면 바로 쓸 수 있습니다.
    public void InterruptDeploy()
    {
        if (!CanInterruptDeploy) return;

        isDeploying = false;
        ResetDeployTrigger();
    }

    private PlayerWeaponMode weaponMode;
    private bool animatorHasDeployTrigger;

    // 사용 동작 진행 상태
    private bool isDeploying;
    private bool effectPending;
    private float deployTimer;

    void Awake()
    {
        if (stats == null) stats = GetComponent<PlayerStats>();
        if (resources == null) resources = GetComponent<PlayerResources>();
        if (resources == null) resources = FindFirstObjectByType<PlayerResources>();
        if (playerHealth == null) playerHealth = GetComponent<PlayerHealth>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        weaponMode = PlayerWeaponMode.GetOrAdd(gameObject);

        animatorHasDeployTrigger = HasTriggerParameter(animator, deployAnimatorTrigger);
    }

    // 사망 처리(PlayerDeathHandler) 등으로 이 컴포넌트가 꺼지면 진행 중이던 사용 동작을 정리합니다
    // ("사용 중" 상태가 남아 무기 전환이 계속 막히는 일이 없도록).
    void OnDisable()
    {
        isDeploying = false;
        effectPending = false;
    }

    void Update()
    {
        // 진행 중인 사용 동작(효과 발동 시점/동작 종료)은 모드와 무관하게 끝까지 처리합니다.
        UpdateDeploySequence();

        // 모드 전환(1/2/3키, 해킹툴 0개 시 총 복귀)은 PlayerWeaponMode가 처리합니다.
        if (!IsHackMode) return;

        // 조준(우클릭) 여부와 무관하게 좌클릭으로 사용합니다(기획 확정). 사용 동작 중에는 연타 금지.
        // HUD 접기 버튼 위에서 누른 클릭은 버튼 조작용이라 전개하지 않습니다(HUDClickBlocker).
        if (!isDeploying && Input.GetMouseButtonDown(0) && !HUDClickBlocker.BlocksLeftClick)
        {
            if (!TryDeploy() && (resources == null || resources.GetAmount(BasicItemType.HackingTool) <= 0))
            {
                GameAudio.Play(SfxId.ActionFail); // [사운드] 해킹툴 0개
            }
        }
    }

    // 클릭 순간: 해킹툴 소모 + 사용 애니메이션 시작. 실제 효과는 deployDelay 뒤에 발동됩니다.
    bool TryDeploy()
    {
        if (isDeploying) return false;
        if (PlayerItemPickupPresenter.BlocksActions) return false; // 아이템 획득 연출 중에는 전개 불가
        if (playerHealth != null && playerHealth.IsDead) return false;
        if (resources == null || !resources.TrySpend(BasicItemType.HackingTool, 1)) return false;

        OnHackToolSpent?.Invoke();

        isDeploying = true;
        effectPending = true;
        deployTimer = 0f;

        if (animatorHasDeployTrigger && animator != null)
        {
            animator.SetTrigger(deployAnimatorTrigger);
        }

        if (deployDelay <= 0f)
        {
            ApplyHackEffect();
        }

        return true;
    }

    // 사용 동작 타이머를 진행합니다: deployDelay가 되면 효과를 발동하고, deployDuration이 되면 동작을 끝냅니다.
    void UpdateDeploySequence()
    {
        if (!isDeploying) return;

        deployTimer += Time.deltaTime;

        if (effectPending && deployTimer >= deployDelay)
        {
            ApplyHackEffect();
        }

        if (!effectPending && deployTimer >= Mathf.Max(deployDuration, deployDelay))
        {
            FinishDeploy();
        }
    }

    // 효과 발동 순간: 그 시점 플레이어 위치 기준 범위 안의 적에게 피해 + 해킹을 걸고 이펙트를 생성합니다.
    void ApplyHackEffect()
    {
        effectPending = false;

        Vector3 center = transform.position;
        float radius = CurrentRadius;
        float damage = stats != null ? stats.HackDamage : 0f;

        var hacked = ApplyHackArea(center, radius, Mathf.Max(effectHeight, 0.1f), damage,
                                   deployVfxFollowPlayer ? transform : null, true);

        OnHackDeployed?.Invoke(center, radius, hacked);
    }

    // [공용 해킹 처리] 원기둥 안의 적에게 damage 피해(0이면 없음) -> 살아남은 적에게 해킹(지속시간 HackDuration) -> 같은 범위 적 총알 제거 ->
    // 전개 이펙트. 해킹이 걸린 적 목록을 돌려줍니다. OnHackDeployed는 보내지 않습니다. hackToolShieldBonus = 해킹툴 직접 사용(2레벨 보스 보호막 추가 피해).
    public List<EnemyHealth> ApplyHackArea(Vector3 center, float radius, float height, float damage, Transform deployVfxFollow,
                                           bool hackToolShieldBonus = false)
    {
        float duration = stats != null ? stats.HackDuration : 0f;

        var hacked = new List<EnemyHealth>();
        foreach (var enemy in CollectEnemiesInRange(center, radius, height))
        {
            if (damage > 0f) enemy.TakeWeakDamage(damage); // 피해 숫자 항상 Weak(형빈 확정)
            if (hackToolShieldBonus) BossShield.ApplyHackToolBonus(enemy);

            // 이 피해로 죽은 적에게는 해킹을 걸지 않습니다.
            if (enemy.IsDead) continue;

            var control = EnemyControlState.GetOrAdd(enemy.gameObject);
            if (control != null && control.TryApplyStrong(EnemyControlType.Hack, duration, hackColor))
            {
                hacked.Add(enemy);
                SpawnVfx(enemyVfxPrefab, enemy.transform.position + enemyVfxOffset, enemyVfxScale,
                         enemyVfxLifetime, enemyVfxFollowEnemy ? enemy.transform : null);
                EnemyStatusVfx.Attach(enemy.gameObject, EnemyControlType.Hack, hackStatusVfx);
            }
        }

        // 같은 원기둥 안의 적 총알을 없앱니다(적 해킹과 같은 순간/같은 범위).
        if (clearEnemyBullets)
        {
            var clearedPositions = bulletClearVfxPrefab != null ? new List<Vector3>() : null;
            EnemyProjectile.ClearInRadius(center, radius, Mathf.Max(height, 0.1f), clearedPositions);
            if (clearedPositions != null)
            {
                foreach (var pos in clearedPositions)
                    SpawnVfx(bulletClearVfxPrefab, pos + bulletClearVfxOffset, bulletClearVfxScale,
                             bulletClearVfxLifetime, null);
            }
        }

        SpawnVfx(deployVfxPrefab, center + deployVfxOffset, deployVfxScale * GetRadiusVfxFactor(radius), deployVfxLifetime,
                 deployVfxFollow);

        return hacked;
    }

    // [81번 소이탄 + 66번 연동] 적 한 명에게만 해킹을 겁니다(피해 없음, 해킹툴 전개 이펙트/적 총알 제거 없음). 해킹이 걸리는 순간의
    // 적 이펙트는 부를 때마다 생성하고(불 구역 틱마다 반복 - 형빈 확정), 해킹 지속 이펙트는 EnemyStatusVfx가 이미 붙어 있으면 새로
    // 만들지 않고 이어갑니다. 이미 해킹된 적이면 해킹 시간이 다시 채워집니다. 해킹이 걸렸으면 true입니다. OnHackDeployed는 보내지 않습니다.
    public bool ApplyHackToEnemy(EnemyHealth enemy)
    {
        if (enemy == null || enemy.IsDead) return false;

        float duration = stats != null ? stats.HackDuration : 0f;
        var control = EnemyControlState.GetOrAdd(enemy.gameObject);
        if (control == null || !control.TryApplyStrong(EnemyControlType.Hack, duration, hackColor)) return false;

        SpawnVfx(enemyVfxPrefab, enemy.transform.position + enemyVfxOffset, enemyVfxScale,
                 enemyVfxLifetime, enemyVfxFollowEnemy ? enemy.transform : null);
        EnemyStatusVfx.Attach(enemy.gameObject, EnemyControlType.Hack, hackStatusVfx);
        return true;
    }

    // 범위 radius가 기본 해킹 범위의 몇 배인지(전개 이펙트 크기 비율). 기본 범위 그대로면 1이라 예전과 같습니다.
    public float GetRadiusVfxFactor(float radius)
    {
        float baseRadius = stats != null ? stats.hackRadius : 0f;
        if (baseRadius <= 0.0001f || radius <= 0f) return 1f;
        return radius / baseRadius;
    }

    // 사용 동작 종료: 다음 사용/무기 전환을 다시 허용하고, 마지막 해킹툴이었다면 총 모드로 돌아갑니다.
    void FinishDeploy()
    {
        isDeploying = false;

        // 혹시 소모되지 않고 남은 Hack 트리거가 나중에 엉뚱하게 재생되지 않도록 지웁니다.
        ResetDeployTrigger();

        // 마지막 해킹툴을 썼으면 자동으로 기본 총 모드로 돌아갑니다(같은 클릭으로 총이 나가지 않게
        // 하는 처리는 PlayerWeaponMode가 함께 합니다).
        if (HackToolCount <= 0 && IsHackMode)
        {
            weaponMode?.ReturnToGunAfterUse();
        }
    }

    void ResetDeployTrigger()
    {
        if (animatorHasDeployTrigger && animator != null)
        {
            animator.ResetTrigger(deployAnimatorTrigger);
        }
    }

    // center 중심 원기둥 안의 살아있는 적을 EnemyHealth 단위로 중복 없이 모읍니다
    // (Grenade.DamageEnemies와 같은 기준).
    List<EnemyHealth> CollectEnemiesInRange(Vector3 center, float radius, float height)
    {
        var result = new List<EnemyHealth>();
        if (radius <= 0f) return result;

        Vector3 top = center + Vector3.up * Mathf.Max(height, 0.1f);
        Collider[] hits = Physics.OverlapCapsule(center, top, radius);

        var seen = new HashSet<EnemyHealth>();
        foreach (var hit in hits)
        {
            if (hit == null) continue;

            EnemyHealth enemyHealth = hit.GetComponent<EnemyHealth>();
            if (enemyHealth == null) enemyHealth = hit.GetComponentInParent<EnemyHealth>();
            if (enemyHealth == null) continue;

            bool isEnemy = string.IsNullOrEmpty(enemyTag)
                           || hit.CompareTag(enemyTag)
                           || enemyHealth.CompareTag(enemyTag);
            if (!isEnemy) continue;
            if (enemyHealth.IsDead) continue;
            if (!seen.Add(enemyHealth)) continue;

            result.Add(enemyHealth);
        }
        return result;
    }

    static void SpawnVfx(GameObject prefab, Vector3 position, float scale, float lifetime, Transform follow)
    {
        if (prefab == null) return;

        GameObject vfx = Instantiate(prefab, position, Quaternion.identity);
        if (follow != null)
        {
            vfx.transform.SetParent(follow, worldPositionStays: true);
        }
        else
        {
            // [방 전환] 따라다니는 대상이 없는(제자리) 이펙트는 플레이어가 다른 방에 들어가면 바로 지워지도록 등록합니다(RoomScope).
            RoomScope.DestroyWhenPlayerLeaves(vfx);
        }
        ApplyVfxScale(vfx, scale);
        if (lifetime > 0f) Destroy(vfx, lifetime);
    }

    // 수류탄/31번 수면 이펙트와 같은 방식입니다 - localScale이 아니라 파티클의 시작 크기/속도 배율을
    // 직접 곱해서, Scaling Mode 때문에 크기가 안 바뀌는 이펙트에도 적용되게 합니다.
    static void ApplyVfxScale(GameObject instance, float multiplier)
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

    static bool HasTriggerParameter(Animator anim, string paramName)
    {
        if (anim == null || string.IsNullOrEmpty(paramName)) return false;
        foreach (var p in anim.parameters)
        {
            if (p.type == AnimatorControllerParameterType.Trigger && p.name == paramName) return true;
        }
        return false;
    }
}
