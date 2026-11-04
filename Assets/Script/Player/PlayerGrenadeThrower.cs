using System;
using UnityEngine;

// [수류탄 투척 - 플레이어 추가 행동]
// 아이작 리펜던스의 폭탄처럼 여러 경로(보물상자, 시작 지급 등 - PlayerResources의 Grenade)로
// 얻는 소모성 자원이지만, 제자리에 놓는 대신 브롤스타즈 다이너마이크처럼 곡사로 던집니다.
//
// [조작]
// - 2키: 수류탄 모드로 전환합니다. 수류탄이 0개면 전환되지 않습니다.
// - 1키: 기본 총 모드로 돌아옵니다.
// - [해킹툴 추가 시 변경] 1/2/3키 전환과 "다 쓰면 총으로 복귀"는 이제 PlayerWeaponMode가 전부
//   맡습니다. 이 컴포넌트는 "지금 수류탄 모드인가?"만 물어보고, 마지막 수류탄을 던지면
//   PlayerWeaponMode.ReturnToGunAfterUse()로 총 복귀만 요청합니다.
// - 수류탄 모드에서도 이동/우클릭 조준 회전은 총 모드와 완전히 동일합니다(PlayerAimAndAttack).
// - 수류탄 모드 + 우클릭을 누르고 있는 동안: 조준 표시(GrenadeAimIndicator)가 나타나고, 이때
//   좌클릭하면 수류탄 1개를 소모해서 던집니다. 우클릭 조준 없이 좌클릭만 하면 아무 일도 없습니다.
// - 마지막 수류탄을 던지면 자동으로 1번(기본 총) 모드로 돌아옵니다.
// - 수류탄 모드 중에는 총을 쏠 수 없고(PlayerAimAndAttack), R키 재장전도 할 수 없습니다
//   (PlayerShooting). 두 스크립트는 PlayerWeaponMode의 "총 모드인가?"만 확인합니다.
//
// [곡사 궤적 - 조준선과 실제 비행이 항상 일치하는 이유]
// 수류탄은 물리 엔진(Rigidbody 중력)으로 날리지 않고, "출발점 -> 착지점"을 잇는 포물선 공식
// (Grenade.EvaluateArc)으로 매 프레임 위치를 직접 계산합니다. 조준선(GrenadeAimIndicator)도 같은
// 함수로 점을 찍어 그리므로, 화면에 보이는 궤적 = 실제로 날아가는 궤적입니다. 비행 시간은
// 거리와 무관하게 flightTime(기본 0.6초) 고정이고, 착지하는 순간(=바닥에 닿는 순간) 바로 폭발합니다.
// 벽/장애물은 넘어서 날아갑니다(기획 확정).
//
// [착지점 계산]
// 마우스가 가리키는 바닥 지점이 착지점입니다. 사거리(PlayerStats.GrenadeRange)보다 멀면 사거리 원의
// 가장자리로 당깁니다. clampToRoomFloor가 켜져 있으면, 착지점을 "플레이어가 있는 방의 사각형" 안으로
// 잘라냅니다(방 벽 위/방 밖을 찍으면 방 안쪽 가장자리에 착지). 방 안의 장애물은 이 사각형 안에 있으므로
// 태그와 무관하게 찍은 자리(바닥 높이)에 그대로 착지합니다.
//
// [방 범위를 콜라이더가 아니라 좌표로 계산하는 이유]
// 처음에는 착지점 아래로 광선을 쏴서 "무엇 위인지"로 방 안팎을 가렸지만, 바닥 프리팹 안에 바닥 지면
// 메시/투명 이동 제한 벽/Wall 태그 장애물이 섞여 있고 벽 프리팹에도 소품이 섞여 있어서, 무엇이 먼저
// 맞느냐에 따라 방 밖이 "착지 가능"으로 잘못 판정되는 문제가 있었습니다. LevelBuilder의 방은 항상
// 한 변이 cellSize인 정사각형 칸 하나이고 방 중심은 (칸 좌표 x cellSize)이므로, 플레이어 위치로 지금
// 방의 중심을 계산해 "중심 +- (cellSize/2 - roomInnerMargin)" 사각형으로 자르면 프리팹 구성과 무관하게
// 항상 같은 결과가 나옵니다. 씬에 LevelBuilder가 없으면(아이템 테스트 아레나 등) 보정하지 않습니다.
//
// [스탯]
// 데미지/자해 데미지/폭발 반경/사거리는 PlayerStats의 "14. 수류탄" 스탯을 씁니다(아이템 모디파이어가
// 그대로 적용됨). 던지는 순간의 값을 수류탄에 저장하므로, 날아가는 도중 스탯이 바뀌어도 조준 때
// 보여준 반경 그대로 터집니다.
//
// [투척 동작 타이밍 - 수류탄 애니메이션 적용 시 추가]
// 우클릭 조준 중 좌클릭하면 그 순간 수류탄 1개를 소모하고, 착지점을 그 순간의 조준점으로 고정한 뒤
// Animator에 Throw 트리거를 보냅니다. 수류탄 오브젝트는 바로 나가지 않고 releaseDelay초 뒤(투척
// 애니메이션에서 손을 떼는 순간)에 생성되어 날아갑니다. 손을 떼는 순간 플레이어가 그 사이 이동했을 수
// 있으므로, 고정해둔 착지점을 그 시점의 위치 기준으로 사거리/방 범위 안에 다시 한 번 맞춥니다.
// 클릭부터 throwDuration초(투척 애니메이션 전체 길이)가 지날 때까지는 IsThrowing이 true라서
// 다음 투척이 막히고(연타 금지), PlayerWeaponMode도 1/2/3 전환과 "자원 0이면 총 복귀"를 미룹니다(투척
// 애니메이션이 중간에 끊기지 않도록). 마지막 수류탄이었다면 투척 동작이 끝난 뒤에 총 모드로 돌아갑니다.
//
// [아이템용 훅]
// OnGrenadeThrown(던진 직후), OnGrenadeExploded(폭발 위치, 폭발 반경) 이벤트를 열어두었습니다.
// PlayerShooting.OnProjectileSpawned와 같은 방식이라, 나중에 수류탄 관련 아이템(분열 수류탄,
// 화염 장판 등)을 이 스크립트 수정 없이 구독만으로 붙일 수 있습니다.
//
// [수류탄 외형 - 57번 등]
// 던질 때 수류탄 모델 색, 터질 때 폭발 이펙트(대체 프리팹/색 덧씌우기)를 GrenadeVisualOverride 등록소에서 읽습니다.
// 외형 아이템이 없으면 기존과 완전히 같습니다.
//
// [분열 수류탄 - 92번]
// Grenade가 터질 때 분열탄 4개를 요청하면 SpawnSplitGrenade가 만듭니다. 수류탄 모델(프리팹/임시 구체)과 57번 외형 색은 일반 수류탄과
// 같고, 크기만 배율(기본 0.6)로 줄입니다. 분열탄은 "던진" 수류탄이 아니므로 OnGrenadeThrown은 보내지 않고, 터질 때는 일반 수류탄처럼
// NotifyExploded(폭발 이펙트 + OnGrenadeExploded)를 거칩니다. 착지점의 방 범위 보정은 ClampToRoomBounds(터진 위치 기준)를 씁니다.
[RequireComponent(typeof(PlayerStats))]
public class PlayerGrenadeThrower : MonoBehaviour
{
    [Header("투척 설정")]
    [Tooltip("던진 뒤 착지(=폭발)까지 걸리는 시간(초)입니다. 거리와 무관하게 항상 이 시간만큼 날아갑니다.")]
    [Min(0.05f)] public float flightTime = 0.6f;

    [Tooltip("포물선의 최고점 높이(m)입니다. 출발점과 착지점을 잇는 직선의 한가운데에서 이만큼 위로 " +
             "솟습니다. 연출용 값이라 스탯이 아니라 여기서 조절합니다.")]
    [Min(0f)] public float arcHeight = 2f;

    [Tooltip("수류탄이 출발하는 위치(손 등)입니다. 비워두면 PlayerShooting의 총구(firePoint)를, " +
             "그것도 없으면 플레이어 위치에서 throwHeightFallback만큼 위를 씁니다.")]
    public Transform throwPoint;

    [Tooltip("throwPoint와 총구가 모두 없을 때 쓰는 출발 높이(m)입니다.")]
    public float throwHeightFallback = 1f;

    [Header("수류탄 모델")]
    [Tooltip("날아가는 수류탄 모델 프리팹입니다. 비워두면 임시로 작은 구체를 씁니다. 프리팹에 콜라이더/" +
             "Rigidbody가 있어도 비행에는 영향을 주지 않도록 자동으로 꺼집니다.")]
    public GameObject grenadePrefab;

    [Tooltip("grenadePrefab이 비어 있을 때 쓰는 임시 구체의 지름(m)입니다.")]
    public float fallbackSphereSize = 0.3f;

    [Tooltip("grenadePrefab이 비어 있을 때 쓰는 임시 구체의 색입니다(기본: 어두운 초록).")]
    public Color fallbackSphereColor = new Color(0.12f, 0.3f, 0.12f, 1f);

    // 임시 구체 전용 재질(처음 던질 때 한 번만 만들고 모든 임시 구체가 같이 씁니다 - 던질 때마다 재질이 쌓이지 않게).
    // [빌드 핑크 방지] 실행 중에 CreatePrimitive로 만든 구체는 옛 렌더링 방식(Built-in)의 기본 재질을 쓰는데, 에디터에서는
    // URP가 대신 그려 주지만 빌드에서는 해석하지 못해 핑크(셰이더 오류색)로 보였습니다. 그래서 적 탄환
    // (EnemyRangedAttack.SpawnProjectile)과 같은 방식으로 URP 셰이더 재질을 직접 입힙니다.
    private Material fallbackSphereMaterial;

    [Tooltip("날아가는 동안 수류탄이 초당 회전하는 각도입니다. 0이면 회전하지 않습니다.")]
    public float spinDegreesPerSecond = 540f;

    [Header("폭발")]
    [Tooltip("피해를 줄 적 콜라이더의 태그입니다(Projectile/4번 폭발과 동일).")]
    public string enemyTag = "Enemy";

    [Tooltip("폭발 판정 기둥의 높이(m)입니다. 바닥에 그려지는 원과 같은 반경의 원기둥 안에 몸이 " +
             "걸친 적이 피해를 받습니다.")]
    [Min(0.1f)] public float explosionHeight = 3f;

    [Tooltip("폭발 위치에 생성할 이펙트 프리팹입니다. 비워두면 이펙트 없이 피해만 적용됩니다.")]
    public GameObject explosionVfxPrefab;

    [Tooltip("생성한 폭발 이펙트를 이 시간(초) 뒤에 파괴합니다. 0 이하면 파괴하지 않습니다.")]
    public float explosionVfxLifetime = 2f;

    [Tooltip("폭발 이펙트를 폭발 위치(착지점)에서 얼마나 옮겨서 생성할지입니다(월드 기준, m). " +
             "예: (0, 0.5, 0)이면 바닥에서 0.5m 위에 생성됩니다. 피해 판정 위치는 바뀌지 않습니다.")]
    public Vector3 explosionVfxOffset = Vector3.zero;

    [Tooltip("기본 폭발 반경일 때의 폭발 이펙트 크기 배율입니다(폭발 반경이 커지면 그 비율만큼 자동으로 같이 커짐). 1이면 프리팹 원래 크기입니다. CombatVfxSpawner와 같은 방식" +
             "(파티클 크기/속도 배율을 직접 곱함)이라, Scaling Mode 때문에 크기가 안 바뀌는 이펙트에도 " +
             "적용됩니다. 피해 반경은 바뀌지 않습니다.")]
    [Min(0.01f)] public float explosionVfxScale = 1f;

    [Header("착탄 지점 표시 (던진 뒤 ~ 폭발 직전)")]
    [Tooltip("켜두면 수류탄을 던진 순간부터 터지기 직전까지 착탄 지점에 폭발 반경 원을 표시합니다.")]
    public bool showLandingMarker = true;

    [Tooltip("착탄 지점 원의 안쪽(꽉 찬 부분) 색상/투명도입니다. 알파를 0으로 하면 안쪽은 숨겨집니다.")]
    public Color landingMarkerFillColor = new Color(1f, 1f, 1f, 0.25f);

    [Tooltip("착탄 지점 원의 테두리 색상/투명도입니다. 알파를 0으로 하면 테두리는 숨겨집니다.")]
    public Color landingMarkerOutlineColor = new Color(1f, 1f, 1f, 0.8f);

    [Tooltip("착탄 지점 원의 테두리 두께(m)입니다.")]
    [Min(0.001f)] public float landingMarkerOutlineWidth = 0.05f;

    [Tooltip("바닥과 겹쳐 깜빡이는 현상을 막기 위해 착탄 지점 원을 바닥에서 띄우는 높이(m)입니다.")]
    public float landingMarkerGroundOffset = 0.03f;

    [Header("착지점 보정 (방 범위)")]
    [Tooltip("켜두면 착지점을 지금 방의 사각형 범위 안으로 잘라냅니다. 방 벽 위나 방 밖을 찍으면 방 안쪽 " +
             "가장자리에 떨어지고, 방 안의 장애물 위는 그대로 착지합니다. 씬에 LevelBuilder가 없으면 " +
             "(아이템 테스트 아레나 등) 보정하지 않습니다.")]
    public bool clampToRoomFloor = true;

    [Tooltip("방 가장자리(칸 경계)에서 안쪽으로 이만큼(m) 들어온 선까지만 착지할 수 있습니다. 벽 두께와 " +
             "벽 앞 공간을 감안한 값으로, 착지 가능 영역이 너무 넓어 벽 위에 떨어지면 늘리고, 너무 좁으면 " +
             "줄이세요. 예: cellSize 10, 여백 1이면 방 중심에서 가로세로 +-4m(8x8) 안에 착지합니다.")]
    [Min(0f)] public float roomInnerMargin = 1f;

    [Tooltip("방 크기(cellSize)를 읽어올 LevelBuilder입니다. 비워두면 씬에서 자동으로 찾습니다.")]
    public LevelBuilder levelBuilder;

    [Header("투척 동작 타이밍 (애니메이션에 맞춰 조절)")]
    [Tooltip("좌클릭 후 수류탄이 실제로 손을 떠나 날아가기까지의 시간(초)입니다. 투척 애니메이션에서 손을 " +
             "떼는 순간에 맞추세요. 0이면 클릭 즉시 날아갑니다.")]
    [Min(0f)] public float releaseDelay = 0.3f;

    [Tooltip("투척 동작 전체 시간(초)입니다. 클릭부터 이 시간이 지날 때까지는 다음 투척과 무기 전환이 " +
             "막힙니다. 투척 애니메이션(Grenade_Throw 클립) 길이에 맞추세요. releaseDelay보다 짧으면 " +
             "releaseDelay까지 기다립니다.")]
    [Min(0f)] public float throwDuration = 1.1f;

    [Header("애니메이션 (선택)")]
    [Tooltip("던질 때 재생할 Animator 트리거 이름입니다. Animator에 이 이름의 Trigger 파라미터가 " +
             "없으면 아무것도 하지 않습니다(오류 없음).")]
    public string throwAnimatorTrigger = "Throw";

    [Header("참조 (비워두면 자동으로 찾습니다)")]
    public PlayerStats stats;
    public PlayerResources resources;
    public PlayerHealth playerHealth;
    public Camera mainCamera;
    public Animator animator;

    // ---- 아이템용 훅 ----
    public event Action<Grenade> OnGrenadeThrown;
    public event Action<Vector3, float> OnGrenadeExploded;

    // [방 전환] 지금 OnGrenadeExploded를 보내고 있는 폭발이 속한 방입니다(이벤트를 받는 동안에만 값이 있고, 그 밖에는 null).
    // 특수방 잠금(SpecialRoomLock)이 폭발 위치가 아닌 "수류탄을 던진 방" 기준으로 판정할 때 읽습니다. 수류탄은 벽을 넘어 옆 방 칸에
    // 떨어질 수도 있어서 위치로는 방을 정할 수 없습니다. null이면(포격 등 방을 넘기지 않는 폭발) 받는 쪽이 예전 기준으로 판정합니다.
    public RoomInstance ExplodingRoom { get; private set; }

    // ---- 다른 스크립트가 읽는 상태 ----
    public bool IsGrenadeMode => weaponMode != null && weaponMode.IsGrenade;

    // GrenadeAimIndicator가 매 프레임 읽어서 조준 표시를 그립니다.
    // 아이템 획득 연출 중(PlayerItemPickupPresenter)에는 조준 표시도 띄우지 않습니다(투척 불가).
    public bool IsAimingGrenade => IsGrenadeMode && Input.GetMouseButton(1) && !PlayerItemPickupPresenter.BlocksActions;
    public Vector3 AimTarget => aimTarget;
    public Vector3 ThrowOrigin => GetThrowOrigin();
    public float CurrentRange => stats != null ? Mathf.Max(stats.GrenadeRange, 0f) : 0f;
    public float CurrentRadius => stats != null ? Mathf.Max(stats.GrenadeRadius, 0f) : 0f;
    public int GrenadeCount => resources != null ? resources.GetAmount(BasicItemType.Grenade) : 0;

    // 투척 동작 중(클릭 ~ throwDuration)이면 true입니다. PlayerWeaponMode가 이 동안 모드 전환을 미룹니다.
    public bool IsThrowing => isThrowing;

    // 수류탄이 이미 손을 떠났고 투척 동작만 남은 상태면 true입니다. 이때만 PlayerWeaponMode가 1번/3번
    // 입력으로 투척 동작을 끊을 수 있습니다(손을 떠나기 전에는 끊을 수 없음 - 형빈 확정).
    public bool CanInterruptThrow => isThrowing && !releasePending;

    // 남은 투척 동작을 즉시 끝냅니다(투척 캔슬). 수류탄이 아직 손을 떠나지 않았으면 아무것도 하지 않습니다.
    // 투척 대기시간도 함께 사라지므로, 다시 수류탄 모드로 돌아오면 바로 던질 수 있습니다.
    public void InterruptThrow()
    {
        if (!CanInterruptThrow) return;

        isThrowing = false;
        if (animatorHasThrowTrigger && animator != null)
        {
            animator.ResetTrigger(throwAnimatorTrigger);
        }
    }

    private PlayerWeaponMode weaponMode;
    private Vector3 aimTarget;
    private PlayerShooting shooting;
    private bool animatorHasThrowTrigger;

    // 투척 동작 진행 상태
    private bool isThrowing;
    private bool releasePending;
    private float throwTimer;
    private Vector3 pendingTarget;


    void Awake()
    {
        if (stats == null) stats = GetComponent<PlayerStats>();
        if (resources == null) resources = GetComponent<PlayerResources>();
        if (resources == null) resources = FindFirstObjectByType<PlayerResources>();
        if (playerHealth == null) playerHealth = GetComponent<PlayerHealth>();
        if (mainCamera == null) mainCamera = Camera.main;
        if (animator == null) animator = GetComponentInChildren<Animator>();
        shooting = GetComponent<PlayerShooting>();
        if (levelBuilder == null) levelBuilder = FindFirstObjectByType<LevelBuilder>();
        weaponMode = PlayerWeaponMode.GetOrAdd(gameObject);

        animatorHasThrowTrigger = HasTriggerParameter(animator, throwAnimatorTrigger);
        aimTarget = transform.position;
    }

    void Update()
    {
        // 진행 중인 투척 동작(손 떼는 시점/동작 종료)은 모드와 무관하게 끝까지 처리합니다.
        UpdateThrowSequence();

        // 모드 전환(1/2/3키, 수류탄 0개 시 총 복귀)은 PlayerWeaponMode가 처리합니다.
        if (!IsGrenadeMode) return;

        if (mainCamera == null) mainCamera = Camera.main;
        aimTarget = ComputeAimTarget();

        // HUD 접기 버튼 위에서 누른 클릭은 버튼 조작용이라 던지지 않습니다(HUDClickBlocker).
        if (!isThrowing && IsAimingGrenade && Input.GetMouseButtonDown(0) && !HUDClickBlocker.BlocksLeftClick)
        {
            TryThrow();
        }
    }

    // 클릭 순간: 수류탄 소모 + 착지점 고정 + 투척 애니메이션 시작. 실제 수류탄은 releaseDelay 뒤에 나갑니다.
    bool TryThrow()
    {
        if (isThrowing) return false;
        if (PlayerItemPickupPresenter.BlocksActions) return false; // 아이템 획득 연출 중에는 투척 불가
        if (playerHealth != null && playerHealth.IsDead) return false;
        if (resources == null || !resources.TrySpend(BasicItemType.Grenade, 1)) return false;

        pendingTarget = aimTarget;
        isThrowing = true;
        releasePending = true;
        throwTimer = 0f;

        if (animatorHasThrowTrigger && animator != null)
        {
            animator.SetTrigger(throwAnimatorTrigger);
        }

        if (releaseDelay <= 0f)
        {
            ReleaseGrenade();
        }

        return true;
    }

    // 투척 동작 타이머를 진행합니다: releaseDelay가 되면 수류탄을 놓고, throwDuration이 되면 동작을 끝냅니다.
    void UpdateThrowSequence()
    {
        if (!isThrowing) return;

        throwTimer += Time.deltaTime;

        if (releasePending && throwTimer >= releaseDelay)
        {
            ReleaseGrenade();
        }

        if (!releasePending && throwTimer >= Mathf.Max(throwDuration, releaseDelay))
        {
            FinishThrow();
        }
    }

    // 손을 떼는 순간: 수류탄을 생성해 날립니다.
    void ReleaseGrenade()
    {
        releasePending = false;

        Vector3 origin = GetThrowOrigin();
        Vector3 target = ClampTarget(transform.position, pendingTarget);

        Grenade grenade = SpawnGrenadeObject(origin);
        grenade.Launch(this, origin, target, flightTime, arcHeight,
                       stats.GrenadeDamage, stats.GrenadeSelfDamage, CurrentRadius,
                       explosionHeight, enemyTag, spinDegreesPerSecond);

        // [수류탄 외형 - 57번 등] 외형 아이템을 보유 중이면 이 수류탄 모델에 색을 입힙니다(GrenadeVisualOverride.cs 참고).
        GrenadeVisualOverride.ApplyToGrenade(grenade.gameObject);

        OnGrenadeThrown?.Invoke(grenade);
    }

    // 투척 동작 종료: 다음 투척/무기 전환을 다시 허용하고, 마지막 수류탄이었다면 총 모드로 돌아갑니다.
    void FinishThrow()
    {
        isThrowing = false;

        // 혹시 소모되지 않고 남은 Throw 트리거가 나중에 엉뚱하게 재생되지 않도록 지웁니다.
        if (animatorHasThrowTrigger && animator != null)
        {
            animator.ResetTrigger(throwAnimatorTrigger);
        }

        // 마지막 수류탄을 던졌으면 자동으로 기본 총 모드로 돌아갑니다(기획 확정).
        if (GrenadeCount <= 0 && IsGrenadeMode)
        {
            weaponMode?.ReturnToGunAfterUse();
        }
    }

    // 임시 구체용 URP 재질을 돌려줍니다(색은 인스펙터 값을 매번 반영). 셰이더를 못 찾으면 null(기본 재질 유지).
    Material GetFallbackSphereMaterial()
    {
        if (fallbackSphereMaterial == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Sprites/Default");
            if (shader == null) return null;
            fallbackSphereMaterial = new Material(shader) { name = "GrenadeFallback_Material" };
        }
        fallbackSphereMaterial.color = fallbackSphereColor;
        return fallbackSphereMaterial;
    }

    void OnDestroy()
    {
        if (fallbackSphereMaterial != null) Destroy(fallbackSphereMaterial);
        fallbackSphereMaterial = null;
    }

    Grenade SpawnGrenadeObject(Vector3 origin)
    {
        GameObject obj;
        if (grenadePrefab != null)
        {
            obj = Instantiate(grenadePrefab, origin, Quaternion.identity);
        }
        else
        {
            obj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            obj.name = "Grenade (임시 구체)";
            obj.transform.position = origin;
            obj.transform.localScale = Vector3.one * Mathf.Max(fallbackSphereSize, 0.01f);

            var rend = obj.GetComponent<Renderer>();
            Material mat = GetFallbackSphereMaterial();
            if (rend != null && mat != null) rend.sharedMaterial = mat;
        }

        // 스크립트로 궤적을 직접 움직이므로, 프리팹에 물리 요소가 있어도 비행/충돌에 끼어들지
        // 않도록 모두 끕니다(적이나 플레이어를 밀어내는 일도 방지).
        foreach (var col in obj.GetComponentsInChildren<Collider>(true)) col.enabled = false;
        foreach (var rb in obj.GetComponentsInChildren<Rigidbody>(true))
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        Grenade grenade = obj.GetComponent<Grenade>();
        if (grenade == null) grenade = obj.AddComponent<Grenade>();
        return grenade;
    }

    // [92번 분열탄] 분열탄 하나를 만들어 origin에서 target으로 날립니다. 수류탄 모델/57번 외형은 일반 수류탄과 같고 크기만
    // modelScale배입니다. inheritedIncendiary는 원래 수류탄의 소이탄 설정(null이면 일반)입니다. 분열탄은 다시 분열하지 않습니다.
    public Grenade SpawnSplitGrenade(Vector3 origin, Vector3 target, float childFlightTime, float childArcHeight,
                                     float childEnemyDamage, float childSelfDamage, float childRadius,
                                     float childExplosionHeight, string childEnemyTag, float modelScale,
                                     IncendiaryGrenadeOverride.Settings inheritedIncendiary,
                                     ClaymoreOverride.Settings inheritedClaymore = null, RoomInstance claymoreRoom = null)
    {
        Grenade grenade = SpawnGrenadeObject(origin);
        grenade.transform.localScale *= Mathf.Max(modelScale, 0.01f);

        grenade.PrepareAsSplitChild(inheritedIncendiary);
        // [115번] 원래 수류탄이 크레모어면 분열탄도 크레모어(같은 방)입니다. null이면 일반 분열탄.
        if (inheritedClaymore != null) grenade.PrepareAsClaymoreChild(inheritedClaymore, claymoreRoom);
        grenade.Launch(this, origin, target, childFlightTime, childArcHeight,
                       childEnemyDamage, childSelfDamage, childRadius,
                       childExplosionHeight, childEnemyTag, spinDegreesPerSecond);

        // [수류탄 외형 - 57번 등] 일반 수류탄과 같은 색을 입힙니다.
        GrenadeVisualOverride.ApplyToGrenade(grenade.gameObject);
        return grenade;
    }

    // [92번 분열탄] target을 reference 위치가 있는 방의 사각형 안으로 잘라냅니다(clampToRoomFloor가 꺼져 있거나 LevelBuilder가 없으면
    // 그대로). 사거리 제한은 하지 않습니다.
    public Vector3 ClampToRoomBounds(Vector3 reference, Vector3 target)
    {
        if (!clampToRoomFloor) return target;
        return ClampToCurrentRoom(reference, target);
    }

    // Grenade가 폭발한 직후 호출합니다. 이펙트 생성 + 아이템용 이벤트 전달을 담당합니다.
    public void NotifyExploded(Vector3 position, float radius)
    {
        NotifyExploded(position, radius, position);
    }

    // [115번 크레모어] 폭발 이펙트 위치(vfxPosition)를 따로 받는 버전입니다. 공중에서 터진 크레모어는 이펙트만 공중에, 아이템용 이벤트
    // (OnGrenadeExploded - 66번/57번/특수방)는 바닥 착탄 원 기준(position)으로 보냅니다.
    public void NotifyExploded(Vector3 position, float radius, Vector3 vfxPosition)
    {
        NotifyExploded(position, radius, vfxPosition, null);
    }

    // [방 전환] 폭발이 속한 방(room - 수류탄을 던진 방)을 함께 넘기는 버전입니다. 이벤트를 보내는 동안 ExplodingRoom에 담아 둡니다.
    public void NotifyExploded(Vector3 position, float radius, Vector3 vfxPosition, RoomInstance room)
    {
        RoomInstance previousRoom = ExplodingRoom;
        ExplodingRoom = room;
        try
        {
            NotifyExplodedCore(position, radius, vfxPosition);
        }
        finally
        {
            ExplodingRoom = previousRoom;
        }
    }

    private void NotifyExplodedCore(Vector3 position, float radius, Vector3 vfxPosition)
    {
        // [수류탄 외형 - 57번 등] 외형 아이템에 대체 폭발 프리팹이 있으면 그것으로 바꾸고, 없으면 원래 폭발 이펙트에 색을
        // 덧씌웁니다(GrenadeVisualOverride.cs 참고). 크기/위치/소멸 시간은 아래 원래 설정을 그대로 씁니다.
        GameObject vfxPrefab = GrenadeVisualOverride.ResolveExplosionPrefab(explosionVfxPrefab, out bool usedReplacement);
        if (vfxPrefab != null)
        {
            GameObject vfx = Instantiate(vfxPrefab, vfxPosition + explosionVfxOffset, Quaternion.identity);
            // [방 전환] 플레이어가 다른 방에 들어가면 바로 지워지도록 등록합니다(RoomScope - 다른 방 위 허공에 남지 않게).
            RoomScope.DestroyWhenPlayerLeaves(vfx);
            ApplyVfxScale(vfx, explosionVfxScale * GetRadiusVfxFactor(radius));
            if (!usedReplacement) GrenadeVisualOverride.ApplyToExplosion(vfx);
            if (explosionVfxLifetime > 0f) Destroy(vfx, explosionVfxLifetime);
        }

        OnGrenadeExploded?.Invoke(position, radius);
    }

    // 이펙트 크기를 배율만큼 조절합니다. CombatVfxSpawner/4번 폭발(ExplodeOnImpactEffect)과 같은
    // 방식으로, localScale이 아니라 파티클의 시작 크기/속도 배율을 직접 곱합니다(일부 이펙트는
    // Scaling Mode 설정 때문에 localScale을 바꿔도 크기가 변하지 않기 때문). 파티클이 없는
    // 이펙트는 localScale을 곱합니다. 배율이 1이면 아무것도 하지 않습니다.
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

    // [폭발 이펙트 크기 - 폭발 반경에 맞춰 커짐/작아짐]
    // explosionVfxScale은 "기본 폭발 반경(PlayerStats의 수류탄 폭발 반경 기본값)"일 때의 크기입니다. 아이템으로 폭발 반경이 바뀌면
    // 이펙트도 (실제 반경 / 기본 반경) 비율만큼 같이 커지거나 작아집니다. 기본 반경 그대로면 비율 1이라 예전과 같습니다.
    public float GetRadiusVfxFactor(float radius)
    {
        float baseRadius = stats != null ? stats.grenadeRadius : 0f;
        if (baseRadius <= 0.0001f || radius <= 0f) return 1f;
        return radius / baseRadius;
    }

    // Grenade가 폭발 반경 안에 플레이어가 있는지 판정할 때 씁니다.
    public PlayerHealth OwnerHealth => playerHealth;

    Vector3 GetThrowOrigin()
    {
        if (throwPoint != null) return throwPoint.position;
        if (shooting != null && shooting.firePoint != null) return shooting.firePoint.position;
        return transform.position + Vector3.up * throwHeightFallback;
    }

    // 마우스가 가리키는 바닥(플레이어 발 높이) 지점을 사거리 안으로 당기고, 필요하면 방 바닥 안쪽으로
    // 보정한 착지점을 돌려줍니다.
    Vector3 ComputeAimTarget()
    {
        Vector3 feet = transform.position;
        Vector3 target = feet;

        if (mainCamera != null)
        {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            Plane groundPlane = new Plane(Vector3.up, new Vector3(0f, feet.y, 0f));
            if (groundPlane.Raycast(ray, out float distance))
            {
                target = ray.GetPoint(distance);
            }
        }
        return ClampTarget(feet, target);
    }

    // 착지점을 feet(플레이어 발 위치) 기준 사거리 안으로 당기고, 필요하면 방 바닥 안쪽으로 보정합니다.
    // 조준 중(ComputeAimTarget)과 손을 떼는 순간(ReleaseGrenade - 그 사이 이동했을 수 있음) 둘 다 씁니다.
    Vector3 ClampTarget(Vector3 feet, Vector3 target)
    {
        target.y = feet.y;

        Vector3 offset = target - feet;
        float range = CurrentRange;
        if (offset.magnitude > range)
        {
            target = feet + offset.normalized * range;
        }

        if (clampToRoomFloor)
        {
            target = ClampToCurrentRoom(feet, target);
        }

        return target;
    }

    // 착지점을 플레이어가 있는 방(칸)의 사각형 안으로 잘라냅니다. LevelBuilder.CellToWorld()와 같은
    // 규칙(칸 중심 = 칸 좌표 x cellSize)으로, 플레이어 위치에서 가장 가까운 칸 중심을 지금 방의 중심으로
    // 봅니다. 콜라이더/태그는 전혀 보지 않습니다.
    Vector3 ClampToCurrentRoom(Vector3 feet, Vector3 target)
    {
        if (levelBuilder == null) return target;

        float cell = levelBuilder.cellSize;
        if (cell <= 0f) return target;

        float centerX = Mathf.Round(feet.x / cell) * cell;
        float centerZ = Mathf.Round(feet.z / cell) * cell;
        float half = Mathf.Max(cell * 0.5f - roomInnerMargin, 0f);

        target.x = Mathf.Clamp(target.x, centerX - half, centerX + half);
        target.z = Mathf.Clamp(target.z, centerZ - half, centerZ + half);
        return target;
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
