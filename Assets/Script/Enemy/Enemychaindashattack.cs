using System;
using UnityEngine;
using UnityEngine.AI;

// 연속 돌진 적(1레벨 보스 포함)의 공격 방식: 포착 -> 최초 충전 -> 돌진 -> (남은 횟수만큼) 짧은 재충전 -> 돌진 ... -> 쿨다운. 최초 충전 때만 첫 돌진 충전
// 이펙트 재생. 연속 돌진은 시야를 보지 않고 NavMesh 경로의 첫 꺾이는 지점으로 조준, 충전 색은 시퀀스 내내 유지, 끼이면 그 돌진만 종료하고 이어감.
// 수치는 EnemyDefinition의 ChainDashAttackConfig가 스폰 때 덮어씀(보스는 프리팹 값). 프리팹 설정과 설계 배경은 Docs/설계노트.md [연속 돌진 적].
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Rigidbody))]
public class EnemyChainDashAttack : MonoBehaviour, IEnemyAttackBehavior, IDashDamageSource, IEnemyColorOverride, ICancellableAttack
{
    private enum AttackPhase { None, Telegraph, Dashing, Cooldown }

    [Header("사거리 (이 거리 이내로 플레이어가 들어오면 AIController가 공격을 시작시킵니다)")]
    public float attackRange = 5f;

    [Header("최초 돌진 충전(텔레그래프) 시간")]
    [Tooltip("이 순간의 플레이어 위치가 첫 번째 돌진 방향으로 고정됩니다.")]
    public float initialTelegraphDuration = 0.6f;

    [Header("첫 돌진 충전 이펙트 (최초 충전 때만 - EnemyDefinition의 Attack Config 칸이 채워져 있으면 그쪽이 우선)")]
    public DashChargeVfxSettings firstDashChargeVfx = new DashChargeVfxSettings();
    private GameObject chargeVfx;

    [Header("연속 돌진")]
    [Tooltip("최초 돌진 이후 추가로 몇 번 더 돌진할지입니다. 0이면 한 번만 돌진합니다.")]
    public int chainDashCount = 1;
    [Tooltip("두 번째 이상의 돌진에서 쓰는 충전 시간입니다. initialTelegraphDuration과 " +
             "별개로 설정할 수 있습니다.")]
    public float chainTelegraphDuration = 0.25f;

    [Header("충전 중 돌진 방향을 바라보는 회전 속도")]
    public float telegraphFacingRotationSpeed = 12f;

    [Header("돌진 속도 (고정값 - 모든 회차 공통)")]
    public float dashSpeed = 8f;

    [Header("돌진 지속 시간 (고정값 - 모든 회차 공통)")]
    public float dashDuration = 0.5f;

    [Header("연속 돌진 전체가 끝난 뒤 다음 공격까지 대기시간")]
    public float postDashCooldown = 1f;

    [Header("돌진 중(Dashing 상태) 접촉 데미지 - 모든 회차 공통, 평소에는 EnemyStats.attackPower 적용")]
    public float dashDamage = 2f;

    [Header("최초 돌진 시작 전 시야(LOS) 체크")]
    [Tooltip("꺼두면 최초 돌진도 시야 체크 없이 무조건 시작합니다 - 보스처럼 '쿨타임만 " +
             "차면 벽 뒤에 있어도 무조건 추적 돌진'해야 하는 특수한 경우에만 끄세요. " +
             "기본값(켜짐)은 기존 동작과 동일합니다.")]
    public bool requireLineOfSightForFirstDash = true;
    [Tooltip("이 레이어에 막혀있으면 사거리 안이라도 최초 돌진을 시작하지 않습니다. 두 " +
             "번째 이상의 연속 돌진은 이 체크를 하지 않습니다 - 벽 뒤에 숨어도 계속 " +
             "쫓아가야 하기 때문입니다(대신 NavMesh 경로로 방향을 다시 잡습니다).")]
    public LayerMask wallLayerMask = ~0;
    [Tooltip("시야 체크 기준점입니다. 비워두면 이 오브젝트 위치 + losHeightOffset(Y축)을 " +
             "대신 사용합니다.")]
    public Transform losOrigin;
    [Tooltip("losOrigin이 비어있을 때만 사용되는 기본 높이 오프셋입니다.")]
    public float losHeightOffset = 1f;

    [Header("돌진 중 끼임 감지")]
    [Tooltip("돌진 도중 실제로 거의 이동하지 못하는 상태가 이 시간 이상 지속되면 그 돌진을 " +
             "강제 종료합니다(체인은 계속 이어집니다).")]
    public float dashStuckAbortTime = 0.3f;
    [Tooltip("한 프레임에 이 거리보다 적게 움직이면 '못 움직이는 중'으로 취급합니다.")]
    public float dashStuckMinProgress = 0.02f;

    [Header("충전 중 -> 돌진 중 유지되는 색상 피드백 (임시 - 실제 모델/애니메이션 도입 전까지)")]
    [Tooltip("꺼두면 충전/돌진 내내 색이 바뀌지 않습니다.")]
    public bool flashOnTelegraph = true;
    [Tooltip("최초 충전 시작 시점의 원래 색에서 이 색으로 서서히 바뀌고, 연속 돌진 " +
             "시퀀스가 전부 끝날 때까지 이 색을 유지합니다.")]
    public Color telegraphColor = new Color(1f, 0.55f, 0.2f);
    [Tooltip("충전 중 자체발광(Emission)도 함께 끌어올릴지 정합니다. telegraphColor를 " +
             "흰색에 가깝게 줘도 변화가 잘 안 보인다면 켜주세요 - _Color/_BaseColor는 원래 " +
             "색에 곱해지는 값이라 흰색을 곱하면 사실상 변화가 없는 반면, 자체발광은 " +
             "조명이나 원래 색과 무관하게 그대로 더해지는 값이라 확실하게 보입니다.")]
    public bool telegraphUsesEmission = true;
    [Tooltip("충전 중 자체발광을 얼마나 끌어올릴지 정합니다.")]
    public float telegraphEmissionBoost = 2f;

    private NavMeshAgent agent;
    private Rigidbody rb;
    private Renderer rend;
    private Color originalColor;
    private bool rendSupportsColor;
    private bool usesBaseColor;
    private bool usesColor;
    private bool usesEmission;
    private Color originalEmission;
    private EnemyMeleeContactTrigger contactTrigger;
    private Transform player;

    // IEnemyColorOverride용 - 지금 텔레그래프/돌진 중에 실제로 적용 중인 색입니다
    // (EnemyHealth가 피격 페이드의 목표 색을 계산할 때 이 값을 참고합니다).
    private Color currentOverrideColor;
    private Color currentOverrideEmission;

    private EnemyStats stats;
    private AttackPhase phase = AttackPhase.None;

    // 지금 바라보고/돌진할 방향을 계산하기 위한 기준점입니다. 최초 돌진은 BeginAttack이
    // 넘겨준 위치로, 연속 돌진은 ComputeChainRedirectAim()이 계산한 위치로 매번 새로
    // 고정됩니다.
    private Vector3 dashAimPosition;
    private Vector3 dashDirection;
    private float phaseTimer;
    private Vector3 dashLastPosition;
    private float dashStuckTimer;

    // 이번 공격 시퀀스에서 앞으로 더 해야 할 "추가" 돌진 횟수입니다. BeginAttack 시점에
    // chainDashCount로 초기화되고, 돌진이 끝날 때마다 하나씩 줄어듭니다.
    private int dashesRemaining;
    // 이번 시퀀스에서 이미 한 번 이상 돌진했는지 여부입니다 - 충전 색상을 처음부터
    // 보간할지(false), 이미 물든 색을 그대로 유지할지(true) 구분하는 데만 씁니다.
    private bool hasDashedAtLeastOnce;

    public float AttackRange => attackRange;
    public bool IsAttacking => phase == AttackPhase.Telegraph || phase == AttackPhase.Dashing;
    public bool CanBeginAttack => phase == AttackPhase.None;
    public bool IsDashing => phase == AttackPhase.Dashing;
    public float DashDamage => dashDamage;
    public event Action OnAttackFinished;

    // IEnemyColorOverride 구현 - 충전(Telegraph)/돌진(Dashing) 중에는 계속
    // 텔레그래프 색을 유지해야 하므로, IsAttacking과 동일한 조건을 그대로 씁니다.
    public bool HasColorOverride => flashOnTelegraph && rendSupportsColor && IsAttacking;
    public Color OverrideColor => currentOverrideColor;
    public Color OverrideEmission => currentOverrideEmission;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        rend = GetComponentInChildren<Renderer>();
        if (rend != null)
        {
            var mat = rend.material; // 최초 접근 시 자동으로 인스턴스화됨
            usesBaseColor = mat.HasProperty("_BaseColor");
            usesColor = mat.HasProperty("_Color");
            rendSupportsColor = usesBaseColor || usesColor;

            if (rendSupportsColor)
            {
                originalColor = usesBaseColor ? mat.GetColor("_BaseColor") : mat.GetColor("_Color");
            }
            else if (flashOnTelegraph)
            {
                Debug.LogWarning($"EnemyChainDashAttack ({gameObject.name}): '{mat.shader.name}' 셰이더에 " +
                                  "_Color/_BaseColor 프로퍼티가 없어 충전 시 색상 피드백을 표시할 수 없습니다. " +
                                  "flashOnTelegraph를 꺼서 이 경고를 없앨 수 있습니다.");
            }

            usesEmission = mat.HasProperty("_EmissionColor");
            if (usesEmission)
            {
                originalEmission = mat.GetColor("_EmissionColor");
                mat.EnableKeyword("_EMISSION");
            }
        }

        contactTrigger = GetComponentInChildren<EnemyMeleeContactTrigger>();
        if (contactTrigger == null)
        {
            Debug.LogWarning($"EnemyChainDashAttack ({gameObject.name}): 자식에서 EnemyMeleeContactTrigger를 찾지 못했습니다. " +
                              "접촉 시 플레이어 피해가 발생하지 않습니다.");
        }

        TryFindPlayer();
    }

    public void Initialize(EnemyStats ownerStats, Transform self)
    {
        stats = ownerStats;
        // IDashDamageSource로서 this를 넘깁니다 - EnemyMeleeAttack과 동일한 방식이지만,
        // EnemyMeleeContactTrigger는 구체 타입이 아니라 이 인터페이스만 보고 동작합니다.
        // [트리거 연결 보강] 프리팹에서 AI 컨트롤러가 이 컴포넌트보다 위에 붙어 있으면, AI의 Awake가 이 컴포넌트의 Awake보다 먼저
        // 실행되어 아직 트리거를 못 찾은 상태로 Initialize가 불립니다. 그러면 접촉 트리거가 능력치를 받지 못해 접촉/돌진 피해가
        // 예비값 1로 고정됩니다. 여기서 한 번 더 찾아서 컴포넌트 순서와 상관없이 항상 연결되게 합니다.
        if (contactTrigger == null) contactTrigger = GetComponentInChildren<EnemyMeleeContactTrigger>();
        if (contactTrigger != null) contactTrigger.Initialize(stats, this);
    }

    // AIController가 공격 시작 조건(사거리 안 + CanBeginAttack)을 만족했을 때 호출합니다.
    // 목표(플레이어)와 자신 사이에 벽이 있어 시야가 막혀있으면 최초 돌진 자체를 시작하지
    // 않고 false를 반환합니다 - 이 판단은 최초 돌진에만 적용되고, 이후 연속 돌진에는
    // 적용되지 않습니다(BeginChainTelegraph 참고).
    public bool BeginAttack(Vector3 targetPosition)
    {
        if (phase != AttackPhase.None) return false;

        if (requireLineOfSightForFirstDash && !HasLineOfSightTo(targetPosition))
        {
            return false;
        }

        dashesRemaining = chainDashCount;
        hasDashedAtLeastOnce = false;

        dashAimPosition = targetPosition;
        phase = AttackPhase.Telegraph;
        phaseTimer = initialTelegraphDuration;

        if (agent != null && agent.enabled) agent.isStopped = true;

        DashChargeVfx.Stop(ref chargeVfx);
        chargeVfx = DashChargeVfx.Play(firstDashChargeVfx, transform);

        return true;
    }

    private bool HasLineOfSightTo(Vector3 targetPos)
    {
        Vector3 from = losOrigin != null ? losOrigin.position : transform.position + Vector3.up * losHeightOffset;
        float heightOffset = from.y - transform.position.y;
        Vector3 to = targetPos + Vector3.up * heightOffset;
        return !Physics.Linecast(from, to, wallLayerMask, QueryTriggerInteraction.Ignore);
    }

    void Update()
    {
        if (player == null) TryFindPlayer();

        switch (phase)
        {
            case AttackPhase.Telegraph:
                UpdateTelegraph();
                break;
            case AttackPhase.Dashing:
                UpdateDash();
                break;
            case AttackPhase.Cooldown:
                UpdateCooldown();
                break;
        }
    }

    private void UpdateTelegraph()
    {
        // [112번 투명] 돌진 준비(최초 충전/연속 돌진 사이 재충전) 중에 플레이어가 투명해지면 연속 돌진 전체를 취소합니다.
        // [87번] 목표가 투명한 플레이어일 때만 취소합니다(아군을 노리는 중이면 그대로).
        if (CombatTargeting.TargetIsInvisiblePlayer(gameObject))
        {
            CancelAttack();
            return;
        }

        RotateTowardsDashAim();

        phaseTimer -= Time.deltaTime;
        UpdateTelegraphColor();

        if (phaseTimer > 0f) return;

        StartDash();
    }

    // 최초 충전 중에만 원래 색 -> telegraphColor로 서서히 보간합니다. 이미 한 번 이상
    // 돌진한 뒤의 재충전(hasDashedAtLeastOnce == true)에서는 telegraphColor를 그대로
    // 유지합니다 - 매번 원래 색으로 돌아갔다가 다시 물드는 깜빡임을 피하기 위함입니다.
    // _Color/_BaseColor(곱셈 틴트)와 필요하면 자체발광(가산)까지 한 번에 적용하는 헬퍼입니다.
    // UpdateTelegraphColor()와 BeginChainTelegraph() 양쪽에서 공유해서 씁니다.
    private void ApplyTelegraphColor(Color color)
    {
        if (usesBaseColor) rend.material.SetColor("_BaseColor", color);
        if (usesColor) rend.material.SetColor("_Color", color);
        currentOverrideColor = color;

        if (telegraphUsesEmission && usesEmission)
        {
            Color emission = color * telegraphEmissionBoost;
            rend.material.SetColor("_EmissionColor", emission);
            currentOverrideEmission = emission;
        }
    }

    private void UpdateTelegraphColor()
    {
        if (!flashOnTelegraph || !rendSupportsColor) return;

        if (hasDashedAtLeastOnce)
        {
            ApplyTelegraphColor(telegraphColor);
            return;
        }

        float elapsed = initialTelegraphDuration - Mathf.Max(phaseTimer, 0f);
        float t = initialTelegraphDuration > 0f ? Mathf.Clamp01(elapsed / initialTelegraphDuration) : 1f;
        ApplyTelegraphColor(Color.Lerp(originalColor, telegraphColor, t));
    }

    // 매 프레임 그 시점의 dashAimPosition을 향해 서서히 회전합니다. 충전이 새로 시작될 때
    // 회전을 즉시 스냅하지 않으므로, 직전 돌진이 끝난 방향에서 자연스럽게 새 목표 방향으로
    // 돌아갑니다(연속 돌진 사이의 부자연스러운 스냅 방지 - 형빈 확인).
    private void RotateTowardsDashAim()
    {
        Vector3 lookDir = dashAimPosition - transform.position;
        lookDir.y = 0f;
        if (lookDir.sqrMagnitude < 0.0001f) return;

        Quaternion targetRot = Quaternion.LookRotation(lookDir);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, telegraphFacingRotationSpeed * Time.deltaTime);
    }

    private void StartDash()
    {
        Vector3 flatDir = dashAimPosition - transform.position;
        flatDir.y = 0f;
        dashDirection = flatDir.sqrMagnitude > 0.0001f ? flatDir.normalized : transform.forward;

        transform.rotation = Quaternion.LookRotation(dashDirection);

        if (flashOnTelegraph && rendSupportsColor)
        {
            ApplyTelegraphColor(telegraphColor);
        }

        dashLastPosition = transform.position;
        dashStuckTimer = 0f;
        phaseTimer = dashDuration;

        phase = AttackPhase.Dashing;
    }

    private void UpdateDash()
    {
        float step = dashSpeed * Time.deltaTime;

        // EnemyMeleeAttack과 동일하게, 벽에 막혀도 여기서 멈추지 않고 같은 방향으로 계속
        // 밀어붙입니다. NavMeshAgent.Move()가 위치를 NavMesh 경계 안쪽으로 재투영해주므로
        // 벽에서는 자연스럽게 그 경계를 따라 미끄러지는 것처럼 보입니다.
        agent.Move(dashDirection * step);

        float movedThisFrame = Vector3.Distance(transform.position, dashLastPosition);
        if (movedThisFrame < dashStuckMinProgress)
        {
            dashStuckTimer += Time.deltaTime;
            if (dashStuckTimer >= dashStuckAbortTime)
            {
                EndDash();
                return;
            }
        }
        else
        {
            dashStuckTimer = 0f;
        }
        dashLastPosition = transform.position;

        phaseTimer -= Time.deltaTime;
        if (phaseTimer <= 0f)
        {
            EndDash();
        }
    }

    // 한 번의 돌진이 끝났을 때(정상 종료든 끼임으로 인한 강제 종료든 동일하게 취급) 호출됩니다.
    // 남은 연속 돌진 횟수가 있으면 다음 돌진을 위한 재충전으로, 없으면 쿨다운으로 넘어갑니다.
    private void EndDash()
    {
        hasDashedAtLeastOnce = true;

        // [112번 투명] 투명 중에는 진행 중이던 돌진까지만 끝내고 다음 연속 돌진은 하지 않습니다(형빈 확정 - 2번째 돌진 중에 투명해지면
        // 2번째를 끝내고 3번째를 취소). 아래 "마지막 돌진 종료" 처리로 넘어갑니다.
        if (CombatTargeting.TargetIsInvisiblePlayer(gameObject)) dashesRemaining = 0; // [87번] 목표가 투명한 플레이어일 때만

        if (dashesRemaining > 0)
        {
            dashesRemaining--;
            BeginChainTelegraph();
            return;
        }

        if (flashOnTelegraph && rendSupportsColor)
        {
            if (usesBaseColor) rend.material.SetColor("_BaseColor", originalColor);
            if (usesColor) rend.material.SetColor("_Color", originalColor);

            if (telegraphUsesEmission && usesEmission)
            {
                rend.material.SetColor("_EmissionColor", originalEmission);
            }
        }

        phase = AttackPhase.Cooldown;
        phaseTimer = postDashCooldown;
    }

    // 두 번째 이상의 돌진을 위한 재충전을 시작합니다. LOS 체크는 하지 않습니다 - 플레이어가
    // 벽 뒤에 숨어도 계속 쫓아가야 하는 게 이 공격 방식의 핵심이기 때문입니다. 대신
    // ComputeChainRedirectAim()이 NavMesh 경로를 이용해 벽을 돌아 다가갈 수 있는 방향을
    // 구합니다. agent.isStopped은 BeginAttack 이후 계속 true로 유지되고 있으므로 여기서
    // 따로 건드릴 필요가 없습니다.
    private void BeginChainTelegraph()
    {
        ComputeChainRedirectAim();

        phase = AttackPhase.Telegraph;
        phaseTimer = chainTelegraphDuration;
    }

    // 다음 돌진이 향할 방향을 정합니다. 에이전트의 현재 위치에서 플레이어의 "현재" 위치까지
    // NavMesh 경로를 계산해서, 경로의 첫 번째 꺾이는 지점(코너) 방향을 목표로 삼습니다 -
    // 벽이 가로막고 있으면 경로 자체가 그 벽을 돌아가는 형태로 나오므로, 플레이어 직선
    // 방향과 달리 실제로 다가갈 수 있는 방향이 자연스럽게 나옵니다. 경로를 구하지
    // 못하면(완전히 고립된 경우 등) 기존처럼 플레이어 직선 방향으로 폴백합니다.
    private void ComputeChainRedirectAim()
    {
        if (player == null) TryFindPlayer();

        // [87번 포섭] 다음 돌진은 "지금의 목표"(플레이어와 아군 중 가장 가까운 쪽) 쪽으로 향합니다. 아군이 없으면 예전과 같이 플레이어.
        Transform aimTarget = CombatTargeting.ResolveNow(gameObject, AllyFaction.IsAlly(gameObject), player, out _);

        if (aimTarget == null || agent == null || !agent.enabled)
        {
            dashAimPosition = transform.position + transform.forward; // 최후의 안전장치
            return;
        }

        NavMeshPath path = new NavMeshPath();
        bool hasPath = agent.CalculatePath(aimTarget.position, path);

        if (hasPath && path.corners.Length >= 2 &&
            (path.status == NavMeshPathStatus.PathComplete || path.status == NavMeshPathStatus.PathPartial))
        {
            dashAimPosition = path.corners[1];
        }
        else
        {
            dashAimPosition = aimTarget.position;
        }
    }

    private void UpdateCooldown()
    {
        // [91번 오한 등 - 공격 속도 보정] 공격 사이 대기(쿨다운)가 줄어드는 속도에 공격 속도 배율을 곱합니다(보정이 없으면 1 - 기존과 같음).
        phaseTimer -= Time.deltaTime * EnemyAttackSpeedModifier.GetMultiplier(gameObject);
        if (phaseTimer > 0f) return;

        phase = AttackPhase.None;
        OnAttackFinished?.Invoke();
    }

    // [38번 패시브 연동] EndDash()를 그대로 쓰면 남은 연속 돌진 횟수(dashesRemaining)가
    // 있을 때 재충전(BeginChainTelegraph)으로 이어져버리므로, 매료로 인한 강제 중단은
    // 남은 횟수를 0으로 만들어 그 자리에서 완전히 종료시키는 전용 로직을 따로 둡니다
    // (EndDash()의 "마지막 돌진 종료" 분기와 동일한 처리 - 색 즉시 원상복귀 + 쿨다운 전환).
    public void CancelAttack()
    {
        if (phase != AttackPhase.Telegraph && phase != AttackPhase.Dashing) return;

        // 최초 충전이 끊기면 충전 이펙트도 바로 지움(이미 돌진했으면 수명대로 사라지게 둠).
        if (phase == AttackPhase.Telegraph && !hasDashedAtLeastOnce) DashChargeVfx.Stop(ref chargeVfx);
        dashesRemaining = 0;

        if (flashOnTelegraph && rendSupportsColor)
        {
            if (usesBaseColor) rend.material.SetColor("_BaseColor", originalColor);
            if (usesColor) rend.material.SetColor("_Color", originalColor);

            if (telegraphUsesEmission && usesEmission)
            {
                rend.material.SetColor("_EmissionColor", originalEmission);
            }
        }

        phase = AttackPhase.Cooldown;
        phaseTimer = postDashCooldown;
    }

    // EnemyAIController.TryFindPlayer()와 동일한 방식입니다 - 스폰 순서상 이 컴포넌트가
    // Awake될 때 플레이어가 아직 없을 수 있어, 찾을 때까지 조용히 재시도합니다. 연속 돌진의
    // 방향 재계산(ComputeChainRedirectAim)에 살아있는 플레이어 위치가 필요해서 이 참조를
    // 직접 들고 있습니다(BeginAttack의 targetPosition은 최초 1회 스냅샷일 뿐입니다).
    private void TryFindPlayer()
    {
        var playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) player = playerObj.transform;
    }
}