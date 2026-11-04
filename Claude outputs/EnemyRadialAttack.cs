using System;
using UnityEngine;
using UnityEngine.AI;

// 360도 전방위 탄환 공격을 담당하는 독립 컴포넌트입니다. 처음에는 이 로직을 공용 컴포넌트인
// EnemyRangedAttack.cs 안에 FireRadialBurst()로 얹어서 구현했었지만, 그러면 전방위 공격을
// 전혀 하지 않는 다른 모든 원거리 적(일반/연사/샷건)도 이 필드/메서드를 인스펙터에서 그대로
// 봐야 하고, 나중에 "전방위만 하는 적"을 EnemyRangedAttack 없이 독립적으로 만들기도
// 어려워지는 문제가 있었습니다(형빈 지적). 그래서 EnemyChainDashAttack이 EnemyMeleeAttack과
// 완전히 분리된 새 컴포넌트로 추가됐던 것과 동일한 방식으로, 이 공격도 IEnemyAttackBehavior를
// 직접 구현하는 별도 컴포넌트로 분리했습니다 - EnemyRangedAttack.cs는 이 작업 이전 상태로
// 완전히 되돌려져 있습니다(전방위 관련 코드 없음).
//
// [지금 당장은 보스 전용으로만 쓰이지만, 구조 자체는 일반 원거리 적과 동일한 파이프라인을 탐]
// IEnemyAttackBehavior를 구현하므로, 이론적으로는 EnemyDefinition에 이 컴포넌트를 요구하는
// IEnemyAttackConfig(예: 나중에 만들 RadialAttackConfig)를 새로 추가하기만 하면
// RealEnemySpawner/EnemyAIController를 전혀 건드리지 않고 "전방위 공격만 하는 일반 적"도
// 만들 수 있습니다. 다만 그 Config/EnemyDefinition 연동은 이번 작업 범위가 아니라서(형빈
// 확인 - "만들긴 할 건데 지금은 아니야") 아직 만들지 않았고, 지금은 BossAIController가
// EnemyDefinition 없이 인스펙터 값을 직접 읽어 BeginAttack()을 호출하는 방식으로만 씁니다.
//
// [탄환 관련 수치를 EnemyRangedAttack과 완전히 분리 - 이번 작업의 핵심 요청]
// damage/projectileScale/projectileColor는 이 컴포넌트 자신의 필드입니다. EnemyRangedAttack의
// 연사 탄환(stats.rangedDamage/rangedProjectileSize/projectileColor)과 전혀 공유하지
// 않으므로, 보스의 연사 탄환과 전방위 탄환의 크기/데미지/색을 완전히 다르게 설정할 수
// 있습니다. 탄환의 사거리(rangedAttackRange)와 속도(rangedProjectileSpeed)만큼은 이번
// 요청에 포함되지 않아(형빈이 "총알 크기, 데미지, 총알 색깔 등"으로 콕 집었던 세 가지만
// 분리 대상이었습니다) EnemyStats(공용 버스)에서 그대로 읽습니다 - "이 적의 탄환이 얼마나
// 멀리/빠르게 날아가는지"는 공격 방식과 무관하게 개체 하나의 공통 특성으로 보는 편이
// 자연스럽다고 판단했습니다. 나중에 이 부분도 분리하고 싶으시면 말씀해주세요.
//
// [탄환 생성 코드가 EnemyRangedAttack.SpawnProjectile()과 상당 부분 중복됨 - 의도적 트레이드오프]
// 원래는 EnemyProjectile 생성 로직(Sphere 프리미티브+셰이더+Launch 호출)을 공용 팩토리로
// 뽑아 EnemyRangedAttack과 이 컴포넌트가 같이 쓰게 하려 했으나, 그러려면 EnemyProjectile.cs
// 또는 EnemyRangedAttack.cs를 손대야 해서 "보스 관련 파일만 수정"이라는 이번 작업 범위를
// 벗어납니다(형빈 확인). 그래서 지금은 이 컴포넌트가 자기 몫의 탄환 생성 코드를 그대로
// 하나 더 들고 있습니다 - 약간의 코드 중복은 있지만 공용 파일은 전혀 건드리지 않으므로 다른
// 모든 원거리 적에는 이 작업이 어떤 영향도 주지 않습니다. 공용 팩토리로 합치는 건 나중에
// 따로 요청해주시면 진행하겠습니다.
//
// [상태 흐름 - EnemyContactOnlyAttack의 회전 연출 + EnemyChainDashAttack의 충전 패턴을 결합]
// None(평소 - BossAIController가 이동/사격 판단 전담) -> Charging(충전 - 이동은 멈추고
// 플레이어를 계속 바라만 봄. 색상 피드백. 시야/사거리를 잃어도 취소하지 않고 끝까지
// 진행 - 보스가 "쿨타임만 차면 무조건 쏜다"는 요구사항과 동일한 이유) -> (충전 종료 순간의
// 플레이어 방향을 0도 기준으로 pelletCount발을 360도 균등 분할해 전부 동시 발사) ->
// Spinning(발사 직후 회전 연출 - EnemyContactOnlyAttack의 spin과 동일한 방식으로
// spinRotations바퀴를 spinDuration 동안 재생. 이동은 계속 멈춰있고, 회전은 플레이어를
// 따라가지 않고 이 연출이 전담) -> Cooldown(postAttackCooldown 대기 - 이 구간부터는
// IsAttacking이 다시 false가 되어 BossAIController가 평소 이동/사격 판단을 이어받습니다.
// 다음 전방위 공격까지 최소 대기시간을 주는 역할이며, 실제로는 burstsSinceRadial 카운터가
// 0으로 리셋된 직후라 연사 3세트를 다시 채우기 전까지는 어차피 재발동 조건 자체가 안
// 맞습니다) -> None(다시 BeginAttack 가능, OnAttackFinished 발생).
//
// [Charging/Spinning 둘 다 IsAttacking=true인 이유 - "세 공격 행동은 서로를 절대 끊지 않는다"]
// BossAIController.Update()는 이 컴포넌트의 IsAttacking이 true인 동안 맨 위에서 조기
// 리턴해서 이동/회전/사격 판단을 전부 손을 뗍니다(dashAttack.IsAttacking과 정확히 동일한
// 패턴). 충전 중뿐 아니라 발사 직후 스핀 연출 중에도 컨트롤러가 다시 끼어들어 이동을
// 재개하거나 돌진을 시작해버리면 안 되므로, 두 단계 모두 IsAttacking에 포함시켰습니다.
//
// [31번 액티브 연동 - ICancellableAttack 추가]
// 기절(EnemyStunState)이 걸리는 순간 충전/스핀 중이던 전방위 공격을 즉시 강제로 중단시킬
// 수 있어야 합니다(형빈 확인 - 기절은 보스도 예외 없이 완전히 막습니다). 매료(38번)는
// 보스의 공격을 건드리지 않으므로 지금까지는 이 인터페이스가 필요 없었지만, 기절은
// 보스에게도 완전히 적용되므로 EnemyChainDashAttack과 동일하게 ICancellableAttack을
// 구현했습니다. 아래 CancelAttack() 참고.
//
// [에디터 설정 필요사항]
// - 보스 프리팹 루트에 이 컴포넌트를 추가하고, damage/projectileScale/projectileColor/
//   pelletCount/chargeDuration/spinDuration/spinRotations/postAttackCooldown 값을 직접
//   입력하세요(EnemyDefinition을 거치지 않고 이 인스펙터 값이 곧 최종값입니다 - 보스는
//   RealEnemySpawner를 거치지 않는 유일한 개체라는 점은 BossAIController.cs 상단 주석과
//   동일합니다).
// - firePoint를 비워두면 이 오브젝트 위치에서 발사됩니다. EnemyRangedAttack의 firePoint와
//   동일한 위치를 공유해도 되고, 따로 둬도 됩니다.
// - NavMeshAgent가 프리팹 루트에 있어야 합니다(충전/스핀 중 이동을 멈추는 데 필요 -
//   BossAIController가 이미 요구하므로 별도로 추가할 필요는 없습니다).
[RequireComponent(typeof(NavMeshAgent))]
public class EnemyRadialAttack : MonoBehaviour, IEnemyAttackBehavior, IEnemyColorOverride, ICancellableAttack
{
    private enum AttackPhase { None, Charging, Spinning, Cooldown }

    [Header("사거리 (지금은 BossAIController가 직접 BeginAttack을 호출해 참조하지 않습니다 - " +
             "나중에 EnemyAIController/RealEnemySpawner 파이프라인으로 연결할 독립형 전방위 " +
             "적을 만들 때를 대비한 필드입니다)")]
    public float attackRange = 6f;

    [Header("발사 위치 (비워두면 이 오브젝트 위치를 그대로 사용)")]
    public Transform firePoint;

    [Header("충전")]
    [Tooltip("발사 전 충전 시간입니다. 이 동안 이동은 멈추고 플레이어를 계속 바라봅니다. " +
             "충전이 시작되면 도중에 시야를 잃거나 멀어져도 취소되지 않고 끝까지 " +
             "진행됩니다(형빈 확인 - 플레이어가 숨기만 해도 영원히 봉인되는 것을 방지).")]
    public float chargeDuration = 0.8f;
    [Tooltip("충전 중 플레이어를 바라보는 회전 속도입니다.")]
    public float facingRotationSpeed = 12f;

    [Header("전방위 탄환 (EnemyRangedAttack의 연사 탄환과 완전히 분리된 별도 수치)")]
    [Tooltip("발사 순간 플레이어를 바라보는 방향을 0도로 삼아 360도를 이 개수만큼 " +
             "균등하게(예: 4발이면 0/90/180/270도, 36발이면 10도 간격) 나눠 동시에 " +
             "발사합니다.")]
    public int pelletCount = 12;
    [Tooltip("전방위 탄환 한 발당 데미지입니다. EnemyStats.rangedDamage(연사 탄환용)와는 " +
             "별개입니다.")]
    public float damage = 2f;
    [Tooltip("전방위 탄환 구체의 스케일 배율입니다. EnemyStats.rangedProjectileSize(연사 " +
             "탄환용)와는 별개입니다.")]
    public float projectileScale = 0.4f;
    [Tooltip("전방위 탄환의 색입니다. EnemyRangedAttack.projectileColor(연사 탄환용)와는 " +
             "별개입니다.")]
    public Color projectileColor = new Color(1f, 0.2f, 0.8f);

    [Header("발사 후 회전 연출 (EnemyContactOnlyAttack의 spin과 동일한 방식)")]
    [Tooltip("이 동안에는 플레이어를 바라보는 회전 대신 이 회전이 전담합니다. 이동은 이 " +
             "동안에도 계속 멈춰 있습니다.")]
    public float spinDuration = 0.5f;
    [Tooltip("spinDuration 동안 몇 바퀴 회전할지입니다. 1이면 360도, 2면 720도.")]
    public float spinRotations = 1f;

    [Header("공격 종료 후 다음 전방위 공격까지 최소 대기시간")]
    public float postAttackCooldown = 1f;

    [Header("충전 중 색상 피드백")]
    public bool flashOnCharge = true;
    public Color telegraphColor = new Color(1f, 0.2f, 0.8f);
    [Tooltip("충전 중 자체발광(Emission)도 함께 끌어올릴지 정합니다. telegraphColor를 " +
             "흰색에 가깝게 줘도 변화가 잘 안 보인다면 켜주세요 - _Color/_BaseColor는 원래 " +
             "색에 곱해지는 값이라 흰색을 곱하면 사실상 변화가 없는 반면, 자체발광은 " +
             "조명이나 원래 색과 무관하게 그대로 더해지는 값이라 확실하게 보입니다.")]
    public bool chargeUsesEmission = true;
    [Tooltip("충전 중 자체발광을 얼마나 끌어올릴지 정합니다.")]
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

    // IEnemyColorOverride용 - 지금 충전 중에 실제로 적용 중인 색입니다
    // (EnemyHealth가 피격 페이드의 목표 색을 계산할 때 이 값을 참고합니다).
    private Color currentOverrideColor;
    private Color currentOverrideEmission;

    private AttackPhase phase = AttackPhase.None;
    private float phaseTimer;

    public float AttackRange => attackRange;
    public bool IsAttacking => phase == AttackPhase.Charging || phase == AttackPhase.Spinning;
    public bool CanBeginAttack => phase == AttackPhase.None;
    public event Action OnAttackFinished;

    // IEnemyColorOverride 구현 - Charging 중에만 색이 원래색이 아닌 상태로 유지됩니다.
    // Spinning으로 넘어가는 순간(FireRadialAndBeginSpin) 색이 이미 원래색으로
    // 되돌아가므로(발사 연출 자체가 원래 색으로 회전하는 것), IsAttacking(=Charging도
    // Spinning도 포함)이 아니라 Charging 여부만 봅니다.
    public bool HasColorOverride => flashOnCharge && rendSupportsColor && phase == AttackPhase.Charging;
    public Color OverrideColor => currentOverrideColor;
    public Color OverrideEmission => currentOverrideEmission;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();

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

    // BossAIController가 전방위 공격 차례(burstsSinceRadial >= burstsBeforeRadial)라고
    // 판단했을 때 직접 호출합니다. targetPosition은 인터페이스 시그니처를 맞추기 위해 받지만,
    // 충전 내내 실시간 플레이어 위치를 다시 추적해야 하므로(EnemyChainDashAttack과 동일한
    // 이유) 실제 조준에는 이 컴포넌트가 직접 찾은 player를 사용합니다.
    public bool BeginAttack(Vector3 targetPosition)
    {
        if (phase != AttackPhase.None) return false;

        phase = AttackPhase.Charging;
        phaseTimer = chargeDuration;

        ForceStayInPlace(); // 충전 시작 즉시 한 번 강제로 멈춥니다 - 매 프레임 재적용은 아래 참고.

        return true;
    }

    // 충전/스핀 내내 Update()에서 매 프레임 호출됩니다.
    //
    // [최종 원인 - 이 컴포넌트 문제가 아니라 보스 프리팹에 잘못 붙어있던 다른 컴포넌트]
    // "충전 중에도 움직인다"/"스핀 회전이 반대로 끌려간다" 두 증상 모두, 알고 보니 보스
    // 프리팹에 실수로 같이 붙어있던 근접 적 전용 EnemyAIController가 원인이었습니다 -
    // 이 컴포넌트가 BossAIController/EnemyRadialAttack과 전혀 무관하게 매 프레임 자기
    // 나름대로 "플레이어에게 접근 + 플레이어를 바라보게 회전"을 같은 NavMeshAgent에
    // 명령하고 있었습니다. 보스 프리팹에서 EnemyAIController(및 EnemyRangedAIController가
    // 붙어있었다면 그것도)를 제거하는 것으로 해결됨을 확인했습니다.
    //
    // [한때 agent.enabled를 꺼서 원천 차단하려 했다가 되돌린 이유]
    // 원인을 못 찾았을 때 NavMeshAgent 자체를 완전히 꺼버리는 방식으로 바꿨었는데, 이
    // 방식은 NavMeshAgent를 껐다 켜는 과정에서 isOnNavMesh 상태가 깨질 수 있어
    // agent.Move() 기반 이동(돌진)은 멀쩡한데 SetDestination() 기반 이동(접근/후퇴)만
    // 조용히 먹통이 되는 새로운 버그를 만들었습니다(형빈이 확인해준 "돌진은 되는데
    // 접근/후퇴가 전혀 안 된다" 증상). 진짜 원인(EnemyAIController)이 사라졌으니, 굳이
    // 위험한 컴포넌트 비활성화 방식을 쓸 필요가 없어 원래의 플래그 방식으로 되돌렸습니다.
    private void ForceStayInPlace()
    {
        if (agent == null || !agent.enabled) return;

        agent.isStopped = true;
        agent.ResetPath();
        agent.velocity = Vector3.zero; // 가속도 설정과 무관하게 그 즉시 멈추도록 감속을 기다리지 않습니다.
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
        // 이동은 멈춰있지만 회전은 계속 플레이어를 따라갑니다 - 발사 순간의 조준 방향이
        // 전방위 탄환의 0도 기준이 되기 때문입니다. 시야를 잃거나 거리가 멀어져도 충전을
        // 취소하지 않고 그대로 진행됩니다.
        ForceStayInPlace();
        UpdateFacing();
        UpdateChargeColor();

        phaseTimer -= Time.deltaTime;
        if (phaseTimer > 0f) return;

        FireRadialAndBeginSpin();
    }

    private void UpdateFacing()
    {
        if (player == null) return;

        Vector3 lookDir = player.position - transform.position;
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
        Vector3 dir = player != null ? player.position - transform.position : transform.forward;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;

        SpawnRadialBurst(dir.normalized);

        if (flashOnCharge && rendSupportsColor)
        {
            // 발사 순간 즉시 원래 색으로 복귀
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

    // centerDirection(발사 순간의 플레이어 방향)을 0도로 삼아 pelletCount발을 360도 안에
    // 균등 분할해 동시에 발사합니다. EnemyRangedAttack.SpawnPelletSpread()의 부채꼴 산탄과
    // 달리 "가운데 -총합/2 ~ +총합/2"가 아니라 0도부터 시작해 등분하는 방식입니다 - 그래야
    // 4발일 때 0/90/180/270도처럼 이음매 없이 원을 완전히 채웁니다(형빈이 예시로 든 그대로).
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

    // EnemyRangedAttack.SpawnProjectile()과 탄환 생성 로직 자체는 동일하지만(3D Sphere
    // 프리미티브 직접 생성 + URP/Standard 셰이더 폴백 체인 + EnemyProjectile.Launch), 이
    // 컴포넌트 자신의 damage/projectileScale/projectileColor를 쓰고 사거리/속도만 공용
    // EnemyStats에서 읽습니다(파일 상단 주석 참고). 공용 팩토리로 합치지 않고 그대로
    // 중복시킨 이유도 상단 주석 참고.
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
                bulletRend.material = new Material(shader) { color = projectileColor };
            }
        }

        // EnemyProjectile.Launch()가 내부에서 transform.localScale을 bulletSize로 다시
        // 덮어쓰므로(EnemyRangedAttack.baseProjectileScale이 겪던 것과 동일한 문제 -
        // 파일 상단 주석 및 세션 기록 참고), 위에서 미리 설정한 localScale이 무의미해지지
        // 않도록 여기서 projectileScale을 그대로 bulletSize 인자로 넘깁니다.
        var projectile = bulletObj.AddComponent<EnemyProjectile>();
        projectile.Launch(direction, damage, stats.rangedAttackRange, stats.rangedProjectileSpeed, projectileScale);
    }

    // ---------- 발사 직후 회전 연출 ----------
    private void UpdateSpinning()
    {
        // 충전 중과 달리 여기서는 플레이어를 바라보는 회전을 하지 않습니다 - 이 스핀 연출이
        // 회전을 전담합니다(EnemyContactOnlyAttack의 spin과 동일한 방식). 이동은 계속
        // 멈춰 있습니다(agent.isStopped는 BeginAttack에서 true로 설정된 이후 계속
        // 유지됩니다).
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
        if (agent != null && agent.enabled) agent.isStopped = false; // 스핀이 끝나야 비로소 이동을 재개합니다.
    }

    private void UpdateCooldown()
    {
        phaseTimer -= Time.deltaTime;
        if (phaseTimer > 0f) return;

        phase = AttackPhase.None;
        OnAttackFinished?.Invoke();
    }

    // [31번 액티브 연동] 기절이 걸리는 즉시 충전/스핀 중이던 전방위 공격을 강제로
    // 중단시킵니다. EnemyChainDashAttack.CancelAttack()과 동일한 패턴 - 색을 즉시 원래
    // 색으로 되돌리고 쿨다운으로 전환합니다. 이동 정지 상태(agent.isStopped)는 이미
    // ForceStayInPlace()로 켜져 있고, 기절 게이트가 BossAIController의 이동 판단 자체를
    // 막아주므로 여기서 따로 풀어주지 않아도 안전합니다.
    public void CancelAttack()
    {
        if (phase != AttackPhase.Charging && phase != AttackPhase.Spinning) return;

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

    private void TryFindPlayer()
    {
        var playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) player = playerObj.transform;
    }
}
