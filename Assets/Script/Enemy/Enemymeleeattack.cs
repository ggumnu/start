using System;
using UnityEngine;
using UnityEngine.AI;

// 일반 돌진 적의 공격 방식: 포착 -> 충전(telegraph, 원래 색에서 telegraphColor로 서서히) -> 바라보는 방향으로 dashDuration 동안 돌진(벽은 NavMesh로
// 미끄러짐, 끼이면 종료) -> 쿨다운. 시작 전 시야(LOS) 체크. 돌진 중 접촉 피해는 dashDamage(IDashDamageSource). 충전 시작 때 첫 돌진 충전 이펙트 재생.
// 수치는 EnemyDefinition의 DashAttackConfig가 스폰 때 덮어씀. 프리팹 설정(Rigidbody, 자식 EnemyMeleeContactTrigger, wallLayerMask, losOrigin)과
// 설계 배경은 Docs/설계노트.md [돌진 적].
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Rigidbody))]
public class EnemyMeleeAttack : MonoBehaviour, IEnemyAttackBehavior, IDashDamageSource, IEnemyColorOverride, ICancellableAttack
{
    private enum AttackPhase { None, Telegraph, Dashing, Cooldown }

    [Header("사거리 (이 거리 이내로 플레이어가 들어오면 AIController가 공격을 시작시킵니다)")]
    public float attackRange = 5f;

    [Header("충전(텔레그래프) 시간 - 이 순간의 플레이어 위치가 돌진 방향으로 고정됩니다")]
    public float telegraphDuration = 0.6f;

    [Header("첫 돌진 충전 이펙트 (EnemyDefinition의 Attack Config 칸이 채워져 있으면 그쪽이 우선)")]
    public DashChargeVfxSettings firstDashChargeVfx = new DashChargeVfxSettings();
    private GameObject chargeVfx;

    [Header("충전 중 돌진 방향을 바라보는 회전 속도")]
    public float telegraphFacingRotationSpeed = 12f;

    [Header("돌진 속도 (고정값)")]
    public float dashSpeed = 8f;

    [Header("돌진 지속 시간 - 이 시간 동안 정해진 방향으로 계속 돌진합니다")]
    [Tooltip("예전처럼 '플레이어 위치까지'가 아니라, 이 시간 동안 dashSpeed로 정해진 " +
             "방향을 계속 밀고 나갑니다. 플레이 테스트하면서 값을 조정하세요(2초보다 " +
             "훨씬 짧은 값이 될 가능성이 높습니다).")]
    public float dashDuration = 0.5f;

    [Header("돌진 종료 후 다음 공격까지 대기시간")]
    public float postDashCooldown = 1f;

    [Header("돌진 중(Dashing 상태) 접촉 데미지 - 평소에는 대신 EnemyStats.attackPower가 적용됩니다")]
    public float dashDamage = 2f;

    [Header("돌진 시작 전 시야(LOS) 체크")]
    [Tooltip("이 레이어에 막혀있으면 사거리 안이라도 돌진을 시작하지 않습니다. 벽 " +
             "레이어만 지정하세요(적 레이어는 넣지 마세요 - 이제 적끼리는 서로 막지 " +
             "않습니다).")]
    public LayerMask wallLayerMask = ~0;
    [Tooltip("시야 체크 기준점입니다. 모델의 눈/머리 높이 정도에 자식 오브젝트로 만들어 " +
             "연결해주세요. 비워두면 이 오브젝트 위치 + losHeightOffset(Y축)을 대신 " +
             "사용합니다.")]
    public Transform losOrigin;
    [Tooltip("losOrigin이 비어있을 때만 사용되는 기본 높이 오프셋입니다. losOrigin을 " +
             "연결하면 이 값은 쓰이지 않습니다.")]
    public float losHeightOffset = 1f;

    [Header("돌진 중 끼임 감지")]
    [Tooltip("돌진 도중 실제로 거의 이동하지 못하는 상태가 이 시간 이상 지속되면 돌진을 " +
             "강제 종료합니다.")]
    public float dashStuckAbortTime = 0.3f;
    [Tooltip("한 프레임에 이 거리보다 적게 움직이면 '못 움직이는 중'으로 취급합니다.")]
    public float dashStuckMinProgress = 0.02f;

    [Header("충전 중 -> 돌진 중 유지되는 색상 피드백 (임시 - 실제 모델/애니메이션 도입 전까지)")]
    [Tooltip("꺼두면 충전/돌진 내내 색이 바뀌지 않습니다.")]
    public bool flashOnTelegraph = true;
    [Tooltip("충전 시작 시점의 원래 색에서 이 색으로 telegraphDuration에 걸쳐 서서히 " +
             "바뀌고, 돌진이 끝날 때까지 이 색을 유지합니다.")]
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

    // IEnemyColorOverride되는 - 지금 텔레그래프/돌진 중에 실제로 적용 중인 색입니다
    // (EnemyHealth가 피격 페이드의 목표 색을 계산할 때 이 값을 참고합니다).
    private Color currentOverrideColor;
    private Color currentOverrideEmission;

    private EnemyStats stats;
    private AttackPhase phase = AttackPhase.None;

    // 충전 시작 시점(BeginAttack 호출 시)의 플레이어 위치 스냅샷입니다. 더 이상 "돌진해서
    // 도달할 지점"이 아니라 "바라볼/돌진할 방향을 계산하기 위한 기준점"으로만 쓰입니다.
    private Vector3 dashAimPosition;
    private Vector3 dashDirection;
    private float phaseTimer;
    private Vector3 dashLastPosition;
    private float dashStuckTimer;

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
            // 3ds Max에서 넘어온 모델처럼 Shader Graph 기반 커스텀 셰이더는 표준 "_Color"
            // 프로퍼티가 없는 경우가 많습니다. 미리 확인해서 지원하지 않으면 충전 시 색상
            // 피드백만 조용히 건너뜁니다 (돌진/공격 로직에는 영향 없음).
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
                Debug.LogWarning($"EnemyMeleeAttack ({gameObject.name}): '{mat.shader.name}' 셰이더에 " +
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
            Debug.LogWarning($"EnemyMeleeAttack ({gameObject.name}): 자식에서 EnemyMeleeContactTrigger를 찾지 못했습니다. " +
                              "접촉 시 플레이어 피해가 발생하지 않습니다.");
        }
    }

    public void Initialize(EnemyStats ownerStats, Transform self)
    {
        stats = ownerStats;
        // this를 넘겨줘서, 트리거가 접촉 판정 시점에 "지금 돌진 중인지(IsDashing)"를 직접
        // 물어보고 그에 맞춰 attackPower/dashDamage 중 하나를 고를 수 있게 합니다.
        // (EnemyMeleeContactTrigger는 이제 EnemyMeleeAttack이 아니라 IDashDamageSource
        // 인터페이스로 이 참조를 받습니다 - this는 두 인터페이스를 모두 구현하므로
        // 여기서는 아무것도 바뀌지 않습니다.)
        // [트리거 연결 보강] 프리팹에서 AI 컨트롤러가 이 컴포넌트보다 위에 붙어 있으면, AI의 Awake가 이 컴포넌트의 Awake보다 먼저
        // 실행되어 아직 트리거를 못 찾은 상태로 Initialize가 불립니다. 그러면 접촉 트리거가 능력치를 받지 못해 접촉/돌진 피해가
        // 예비값 1로 고정됩니다. 여기서 한 번 더 찾아서 컴포넌트 순서와 상관없이 항상 연결되게 합니다.
        if (contactTrigger == null) contactTrigger = GetComponentInChildren<EnemyMeleeContactTrigger>();
        if (contactTrigger != null) contactTrigger.Initialize(stats, this);
    }

    // AIController가 공격 시작 조건(사거리 안 + CanBeginAttack)을 만족했을 때 호출합니다.
    // 목표(플레이어)와 자신 사이에 벽이 있어 시야가 막혀있으면 공격을 시작하지 않고 false를
    // 반환합니다 - 호출부(EnemyAIController)는 이 경우 즉시 평소 접근 행동으로 돌아갑니다.
    public bool BeginAttack(Vector3 targetPosition)
    {
        if (phase != AttackPhase.None) return false; // 이미 공격 시퀀스 진행 중이면 무시

        if (!HasLineOfSightTo(targetPosition))
        {
            return false;
        }

        dashAimPosition = targetPosition;
        phase = AttackPhase.Telegraph;
        phaseTimer = telegraphDuration;

        if (agent != null && agent.enabled) agent.isStopped = true;

        DashChargeVfx.Stop(ref chargeVfx);
        chargeVfx = DashChargeVfx.Play(firstDashChargeVfx, transform);

        // 색상은 여기서 즉시 바꾸지 않습니다 - UpdateTelegraphColor()가 매 프레임 원래 색에서
        // telegraphColor로 서서히 보간합니다 (첫 프레임엔 사실상 원래 색 그대로).
        return true;
    }

    // 자신과 targetPos 사이에 wallLayerMask에 해당하는 콜라이더가 있으면 시야가 막힌
    // 것으로 판단합니다. EnemyRangedAIController.CheckLineOfSight()와 동일한 방식입니다.
    // losOrigin이 설정되어 있으면 그 위치를, 아니면 transform.position + losHeightOffset을
    // 기준점으로 쓰고, 목표(targetPos) 쪽도 같은 높이만큼 올려서 비교합니다.
    private bool HasLineOfSightTo(Vector3 targetPos)
    {
        Vector3 from = losOrigin != null ? losOrigin.position : transform.position + Vector3.up * losHeightOffset;
        float heightOffset = from.y - transform.position.y;
        Vector3 to = targetPos + Vector3.up * heightOffset;
        return !Physics.Linecast(from, to, wallLayerMask, QueryTriggerInteraction.Ignore);
    }

    void Update()
    {
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
        // [112번 투명] 돌진 준비(충전) 중에 플레이어가 투명해지면 준비를 취소합니다(이미 시작된 돌진은 끝까지 진행 - 형빈 확정).
        // [87번] 공격 목표가 아군이면 플레이어가 투명해도 취소하지 않습니다(목표가 투명한 플레이어일 때만).
        if (CombatTargeting.TargetIsInvisiblePlayer(gameObject))
        {
            EndDash();
            return;
        }

        RotateTowardsDashAim();

        phaseTimer -= Time.deltaTime;
        UpdateTelegraphColor();

        if (phaseTimer > 0f) return;

        StartDash();
    }

    // 충전 진행률(0 = 막 시작, 1 = 충전 완료 직전)에 맞춰 원래 색 -> telegraphColor로
    // 서서히 보간합니다. phaseTimer가 telegraphDuration에서 0까지 줄어드는 것을 그대로
    // 진행률로 사용합니다.
    private void UpdateTelegraphColor()
    {
        if (!flashOnTelegraph || !rendSupportsColor) return;

        float elapsed = telegraphDuration - Mathf.Max(phaseTimer, 0f);
        float t = telegraphDuration > 0f ? Mathf.Clamp01(elapsed / telegraphDuration) : 1f;

        Color c = Color.Lerp(originalColor, telegraphColor, t);
        if (usesBaseColor) rend.material.SetColor("_BaseColor", c);
        if (usesColor) rend.material.SetColor("_Color", c);
        currentOverrideColor = c;

        if (telegraphUsesEmission && usesEmission)
        {
            Color e = Color.Lerp(originalEmission, telegraphColor * telegraphEmissionBoost, t);
            rend.material.SetColor("_EmissionColor", e);
            currentOverrideEmission = e;
        }
    }

    // 충전 중엔 살아있는 플레이어 위치가 아니라, BeginAttack 시점에 스냅샷된
    // dashAimPosition(=돌진해서 향할 방향의 기준점)을 고정해서 바라봅니다. 그래야
    // 텔레그래프 도중 플레이어가 움직여도 "바라보는 방향"과 "돌진해서 실제로 가는 방향"이
    // 항상 일치합니다.
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

        // 텔레그래프 내내 이미 같은 방향을 바라보고 있었으므로 이 시점의 회전은 사실상
        // dashDirection과 거의 일치하지만, 안전하게 한 번 더 정확히 맞춰줍니다. 돌진이
        // 끝나 쿨다운에 들어가면 EnemyAIController가 다시 플레이어를 바라보는 회전을
        // 이어받습니다.
        transform.rotation = Quaternion.LookRotation(dashDirection);

        // 충전 색은 여기서 원래 색으로 되돌리지 않습니다 - 돌진 중에도 telegraphColor를 그대로
        // 유지합니다. (부동소수점 오차로 완전히 telegraphColor에 도달하지 못했을 가능성에 대비해
        // 한 번 더 정확히 맞춰줍니다.)
        if (flashOnTelegraph && rendSupportsColor)
        {
            if (usesBaseColor) rend.material.SetColor("_BaseColor", telegraphColor);
            if (usesColor) rend.material.SetColor("_Color", telegraphColor);
            currentOverrideColor = telegraphColor;

            if (telegraphUsesEmission && usesEmission)
            {
                rend.material.SetColor("_EmissionColor", telegraphColor * telegraphEmissionBoost);
                currentOverrideEmission = telegraphColor * telegraphEmissionBoost;
            }
        }

        dashLastPosition = transform.position;
        dashStuckTimer = 0f;
        phaseTimer = dashDuration;

        phase = AttackPhase.Dashing;
    }

    private void UpdateDash()
    {
        float step = dashSpeed * Time.deltaTime;

        // 벽/다른 적에 막혀도 더 이상 여기서 멈추지 않습니다 - 그냥 같은 방향으로 계속
        // 밀어붙입니다. NavMeshAgent.Move()가 알아서 위치를 NavMesh 경계 안쪽으로
        // 재투영해주기 때문에, 벽에서는 자연스럽게 그 경계를 따라 미끄러지는 것처럼
        // 보입니다(파일 상단 "벽 슬라이딩" 설명 참고).
        agent.Move(dashDirection * step);

        // 그래도 실제로 거의 못 움직이는 상태(코너에 끼임 등)가 계속되면 강제로 멈춥니다.
        // 고정된 "최대 돌진 시간"이 아니라 "실제 이동 거리"를 감시하는 것이라, 정상적으로
        // 슬라이딩 중인 돌진은 전혀 방해하지 않습니다.
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

        // 정상적으로 진행 중이면 dashDuration이 다 지났을 때만 돌진을 종료합니다(더 이상
        // "목표 지점에 도달했는지"는 확인하지 않습니다).
        phaseTimer -= Time.deltaTime;
        if (phaseTimer <= 0f)
        {
            EndDash();
        }
    }

    private void EndDash()
    {
        // 돌진이 끝나는 순간(=공격 해제) 색은 서서히가 아니라 즉시 원래 색으로 복귀합니다 -
        // "공격이 끝났다"는 것을 명확하게 읽히게 하기 위함입니다.
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

    private void UpdateCooldown()
    {
        // 쿨다운 중엔 IsAttacking이 false이므로 EnemyAIController가 매 프레임 다시 이동(접근)
        // 및 플레이어를 바라보는 회전 판단을 이어받습니다. 여기서는 순수하게 타이머만 관리합니다.
        // [91번 오한 등 - 공격 속도 보정] 공격 사이 대기(쿨다운)가 줄어드는 속도에 공격 속도 배율을 곱합니다(보정이 없으면 1 - 기존과 같음).
        phaseTimer -= Time.deltaTime * EnemyAttackSpeedModifier.GetMultiplier(gameObject);
        if (phaseTimer > 0f) return;

        phase = AttackPhase.None;
        OnAttackFinished?.Invoke();
    }

    // [38번 패시브 연동] 매료(EnemyCharmState)가 걸리는 즉시 텔레그래프/돌진 중이던 공격을
    // 강제로 중단시킵니다. EndDash()가 이미 "색 즉시 원상복귀 + 쿨다운 전환"을 정확히
    // 처리하므로 그대로 재사용합니다(돌진 중이든 텔레그래프 중이든 동일하게 안전합니다).
    public void CancelAttack()
    {
        if (phase != AttackPhase.Telegraph && phase != AttackPhase.Dashing) return;
        if (phase == AttackPhase.Telegraph) DashChargeVfx.Stop(ref chargeVfx); // 충전이 끊기면 충전 이펙트도 바로 지움
        EndDash();
    }
}