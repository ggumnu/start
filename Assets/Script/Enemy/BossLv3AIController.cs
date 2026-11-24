using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// 3레벨 보스 컨트롤러입니다. BossAIController를 상속해 보스 판정(체력바, 매료/오한/넉백/87번 제외 등)을 그대로 받습니다.
// 방 중앙에서 시작해 이동하지 않고, 제자리에서 몸(과 레이저)만 돌립니다. 페이즈 단위로 진행하며 지금은 1페이즈만 있습니다.
// [1페이즈 흐름] 피해 감소막 충전(BossDamageReduction - 무적, 맞으면 MISS) -> 감소막(85% 감소, 무한)
// -> 지금 정면 기준 레이저(BossLaserBeam) 조준선 Telegraph Time -> 조사 -> Rotation Start Delay 뒤부터 반시계로 점점 빨라지며 회전(Max Rotation Speed까지)
// -> 조사 Beacon Spawn Delay 뒤 방 모서리 세 곳에 그로기 비콘(수류탄/해킹/총알, GroggyBeacon)이 내려옴(남은 한 모서리는 지원 드론 자리)
// -> 비콘과 함께 남은 모서리에 지원 드론이 보급 상자(수류탄 1 + 해킹툴 1)를 내려놓음(BeaconSupplyDrone). 수류탄/해킹 비콘이 남아 있는데
//    그 재화가 0개이고 열지 않은 보급 상자/안 가져간 그 재화가 없으면 Resupply Delay 뒤 그 재화만 든 상자를 비콘이 없는 모서리에 다시 보급
// -> 비콘 셋을 모두 해제(남은 보급 상자와 안의 것은 바로 흰 빛으로 사라짐 - 재화 무한 수급 방지). 마지막 웨이브가 아니면(형빈 확정 - 기본 2웨이브)
//    레이저가 Reverse Decel Time 동안 서서히 멈췄다가 반대 방향으로 Reverse Accel Time 동안 원래 속도까지 가속하고, 가속 시작 Next Wave Delay 뒤
//    비콘 셋 + 지원 드론이 다시 내려옴. 비콘 하나를 해제할 때마다 그로기 스택이 쌓이고(보스 체력바 오른쪽 게이지 - IBossGroggyGauge)
// -> 마지막 웨이브의 비콘까지 모두 해제 = 기믹 해제 -> 레이저가 Break Fade Time에 걸쳐 사그라듦(그동안 회전도 서서히 멈춤) -> 보스에 연쇄 폭파 + 최대 체력 x Break Damage Ratio(감소 없이, 피해 숫자 Weak)
// -> 감소막이 사라짐 -> 그로기(Groggy Duration, 2레벨처럼 모델이 갸우뚱 + 노란 몸, 게이지 가득) -> 게이지 비움 -> 체력이 남아 있으면 다시 충전부터 반복.
// 그로기/해제는 제어/목표와 무관하게 진행됩니다(1페이즈의 행동은 전부 기믹이라 수면/해킹으로 멈추지 않음 - 감소막이 있는 동안은 어차피 면역).
// 방을 나가면 기존 규칙대로 보스가 처음 상태로 다시 만들어집니다. 기획은 Docs/설계노트.md [3레벨 보스].
public class BossLv3AIController : BossAIController, IEnemyColorOverride, IBossGroggyGauge
{
    [Header("3레벨 보스 - 1페이즈 레이저 (판정/모양은 BossLaserBeam)")]
    [Tooltip("감소막이 생긴 뒤 조준선만 보여주는 시간(초) - 이 동안 피할 수 있음.")]
    [Min(0f)] public float telegraphTime = 1f;
    [Tooltip("조준선 시간 중 앞부분: 차지 이펙트가 나오고 보스가 흰 발광으로 서서히 뒤덮이는 시간(초). 남은 조준선 시간은 흰 발광만 유지.")]
    [Min(0.01f)] public float chargeTime = 1f;
    [Tooltip("차지 동안 보스를 뒤덮는 색(발광색은 이 색 x Charge Glow Emission Boost).")]
    public Color chargeGlowColor = Color.white;
    [Tooltip("차지 발광 세기(0이면 몸 색만 바뀌고 발광 없음).")]
    [Min(0f)] public float chargeGlowEmissionBoost = 1.5f;
    [Tooltip("레이저 발사 뒤 본래 색으로 돌아오는 시간(초).")]
    [Min(0.01f)] public float chargeGlowReturnTime = 0.15f;
    [Tooltip("발사 직후 굵기 유지(BossLaserBeam의 Burst Hold Time)가 끝나 줄어들기 시작한 뒤 회전을 시작하기까지(초).")]
    [Min(0f)] public float rotationDelayAfterBurst = 0f;
    [Tooltip("회전 속도가 0에서 최대까지 오르는 시간(초).")]
    [Min(0f)] public float rotationRampTime = 3f;
    [Tooltip("최대 회전 속도(도/초).")]
    [Min(0f)] public float maxRotationSpeed = 25f;
    [Tooltip("켜면 위에서 봤을 때 반시계 방향으로 돕니다.")]
    public bool rotateCounterClockwise = true;
    [Tooltip("레이저 발사 순간 보스 중심에서 한 번 퍼지는 신호 물결 - 2레벨 보스 소환 물결과 같은 그림만(피해/효과 없음). Wave Material에 " +
             "RoomWave_Mat(90번 아이템과 같은 재질)을 넣어 주세요. '물결 진행'과 '물결 모양' 칸만 쓰고 나머지는 무시합니다. 재질이 비면 물결 없음.")]
    public RoomWaveClearEffect fireWave = new RoomWaveClearEffect { waveColor = new Color(2.2f, 0.25f, 0.2f, 1f) };

    [Header("3레벨 보스 - 1페이즈 비콘 웨이브 (레이저 역회전)")]
    [Tooltip("그로기까지 내려오는 비콘 웨이브 수(한 웨이브 = 비콘 셋). 2면 비콘 6개를 해제해야 그로기(형빈 확정). 그로기 게이지는 비콘 하나당 1 / (3 x 이 값)씩 찹니다.")]
    [Min(1)] public int beaconWavesBeforeGroggy = 2;
    [Tooltip("웨이브의 비콘을 모두 해제한 뒤(마지막 웨이브 제외) 레이저 회전이 지금 속도에서 0까지 서서히 줄어드는 시간(초).")]
    [Min(0f)] public float reverseDecelTime = 1.5f;
    [Tooltip("멈춘 뒤 반대 방향으로 0에서 최대 회전 속도까지 가속하는 시간(초).")]
    [Min(0f)] public float reverseAccelTime = 1.5f;
    [Tooltip("반대 방향으로 가속하기 시작한 뒤 다음 웨이브 비콘 + 지원 드론이 내려오기 시작하기까지(초).")]
    [Min(0f)] public float nextWaveDelay = 3f;

    [Header("3레벨 보스 - 1페이즈 기믹 해제")]
    [Tooltip("기믹을 모두 해제한 뒤 레이저가 사그라드는 시간(초). 이 동안 회전도 그때 속도에서 서서히 멈추고, 끝나는 순간 보스에 연쇄 폭파 + 피해.")]
    [Min(0f)] public float breakFadeTime = 0.5f;
    [Tooltip("기믹 해제 피해 = 최대 체력 x 이 값(감소막 감소 없이 들어가고, 피해 숫자는 Weak).")]
    [Range(0f, 1f)] public float breakDamageRatio = 0.2f;
    [Tooltip("기믹 해제 때 보스에 터지는 연쇄 폭파 크기 = 일반 적 처치 폭발(CombatVfxSpawner) x 이 값(흩어지는 범위와 크기 모두).")]
    [Min(0.1f)] public float breakExplosionScale = 2f;

    [Header("3레벨 보스 - 그로기")]
    [Tooltip("감소막이 사라진 뒤 그로기 시간(초). 끝나면 체력이 남아 있으면 다시 충전부터.")]
    [Min(0f)] public float groggyDuration = 7f;
    [Tooltip("그로기 때 기울일 모델(보스 루트의 자식). 비우면 렌더러가 있는 첫 자식을 자동으로 씀.")]
    public Transform groggyTiltTarget;
    [Tooltip("그로기 기울기 각도(도).")]
    [Range(0f, 60f)] public float groggyTiltAngle = 15f;
    [Tooltip("기울어진 방향이 한 바퀴 도는 횟수(초당).")]
    public float groggyWobbleSpeed = 1f;
    [Tooltip("그로기 시작/끝에 기울기와 색이 서서히 들어가고 빠지는 시간(초).")]
    [Min(0.01f)] public float groggyBlendTime = 0.3f;
    [Tooltip("그로기 동안의 몸 색(2레벨 보스와 같은 노란색).")]
    public Color groggyColor = new Color(1f, 0.85f, 0.1f, 1f);
    [Tooltip("그로기 동안 발광 세기(그로기 색 x 이 값, 0이면 발광 없음).")]
    [Min(0f)] public float groggyEmissionBoost = 0.6f;

    [Header("3레벨 보스 - 1페이즈 그로기 비콘 (GroggyBeacon)")]
    [Tooltip("비콘 모델 프리팹(Boss3_Beacon). 비우면 임시 상자 모양으로 내려옵니다.")]
    public GameObject beaconModelPrefab;
    [Tooltip("발사 직후 굵어진 레이저가 기존 폭으로 줄어든 뒤 비콘이 내려오기 시작하기까지(초).")]
    [Min(0f)] public float beaconDelayAfterBurst = 3f;
    [Tooltip("방 모서리에서 안쪽으로 들어온 거리(월드 단위) - 2레벨 보스 소환 자리와 같은 규칙.")]
    [Min(0f)] public float beaconCornerInset = 0.8f;
    [Tooltip("비콘이 보스를 바라볼 때 모델 정면 보정 각도(도, Y축). 모델 정면이 보스를 안 보면 90 / 180 / -90 중 하나를 넣어 보세요.")]
    public float beaconFacingOffset = 0f;
    [Tooltip("방 크기를 못 읽었을 때(방이 없는 테스트 씬 등) 보스 중심 기준 모서리까지 반 너비(월드 단위).")]
    [Min(0.5f)] public float beaconFallbackHalfSize = 5f;
    [Tooltip("MobArchive/BossLaserSmoke (Assets/Shaders/BossLaserSmoke.shader) - 비콘 하강 연기. 비우면 이름으로 찾고, 못 찾으면 연기 없이 내려옵니다.")]
    public Shader beaconSmokeShader;
    [Tooltip("비콘 하강 부스터 외형 원본 - 1레벨 보스 프리팹을 끌어다 놓으면 그 Enemy Radial Attack의 강화 탄환 외형/크기/꼬리 값을 그대로 씁니다" +
             "(나선 고리만 끔). 비우면 부스터 없음.")]
    public EnemyRadialAttack beaconBoosterSource;
    public GroggyBeaconSettings beaconSettings = new GroggyBeaconSettings();

    [Header("3레벨 보스 - 1페이즈 지원 드론 보급 (BeaconSupplyDrone)")]
    public BeaconSupplySettings supplySettings = new BeaconSupplySettings();

    private enum State { Entering, Charging, Gimmick, Breaking, Groggy }
    private State state = State.Entering;
    private float stateTimer;

    private BossLaserBeam laser;
    private bool laserFiring;      // 기믹 중 조준선이 끝나고 조사 중
    private float firingElapsed;   // 조사 시작 후 지난 시간(회전/비콘 타이밍 기준)
    private float breakStartRotationSpeed; // 기믹 해제 순간의 회전 속도(부호 포함 - 사그라드는 동안 여기서 0까지 줄임)

    // 비콘 웨이브 / 역회전 / 그로기 스택
    private int waveIndex;              // 이번 기믹에서 몇 번째 웨이브인지(0부터)
    private bool reversing;             // 웨이브 사이 - 감속/반대 방향 가속 중이고 다음 웨이브 비콘을 기다리는 중
    private float reverseElapsed;       // 마지막 역회전 시작 후 지난 시간(둘째 웨이브부터 회전 속도 계산 기준 - 비콘이 내려온 뒤에도 계속 셈)
    private float reverseStartVelocity; // 역회전 시작 순간 회전 속도(부호 포함)
    private float waveDirection = 1f;   // 이번 웨이브의 회전 방향(1 = Rotate Counter Clockwise 설정 방향, -1 = 반대)
    private int brokenBeaconCount;      // 이번 기믹에서 해제한 비콘 수(그로기 스택)
    private bool gaugeFull;             // 기믹 해제 ~ 그로기 끝까지 게이지를 가득으로

    private readonly List<GroggyBeacon> beacons = new List<GroggyBeacon>();
    private bool beaconsSpawned;   // 이번 기믹에서 비콘을 이미 내려보냈는지
    private bool hasFreeCorner;    // 비콘이 없는 남은 모서리(지원 드론 자리)
    private Vector3 freeCornerPosition;
    private readonly Dictionary<GroggyBeacon, Vector3> beaconCorners = new Dictionary<GroggyBeacon, Vector3>();
    private readonly List<BeaconSupplyDrone> deliveries = new List<BeaconSupplyDrone>();
    private float grenadeResupplyTimer;
    private float hackResupplyTimer;
    private PlayerResources playerResources;
    private LevelBuilder levelBuilder;
    private Material beaconSmokeMaterial;
    private bool warnedNoBeaconModel;
    private BeaconBoosterVisual beaconBooster;

    // 1레벨 보스 전방위 탄환 외형을 비콘 부스터용으로 옮겨 담습니다(고리만 끈 복사본, 처음 한 번 - 보스가 살아 있는 동안 재사용).
    private BeaconBoosterVisual GetBeaconBooster()
    {
        if (beaconSettings == null || !beaconSettings.boosterEnabled || beaconBoosterSource == null) return null;
        if (beaconBooster != null) return beaconBooster;

        var source = beaconBoosterSource;
        var visual = source.chargedVisual != null ? source.chargedVisual.Clone() : ChargedBulletVisual.Settings.CreateCyan();
        visual.ringCount = 0; // 검은 나선 고리는 안 보이게(형빈 확정)

        float bulletScale = Mathf.Max(0.01f, source.projectileScale);
        float hostScale = bulletScale * Mathf.Max(0.01f, beaconSettings.boosterScale);
        // 꼬리 길이(월드) = 1레벨 탄환 꼬리 시간 x 탄환 속도(칸에 값이 있으면 그 값). 외형은 지름 단위라 크기로 나눔.
        float tailWorld = beaconSettings.boosterTailLength;
        if (tailWorld <= 0f)
        {
            var sourceStats = source.GetComponent<EnemyStats>();
            float speed = sourceStats != null ? sourceStats.rangedProjectileSpeed : 14f;
            tailWorld = visual.trailTime * speed * Mathf.Max(0.01f, beaconSettings.boosterScale);
        }

        beaconBooster = new BeaconBoosterVisual
        {
            settings = visual,
            level = Mathf.Max(1, source.chargedVisualLevel),
            hostScale = hostScale,
            tailLength = tailWorld / hostScale,
        };
        return beaconBooster;
    }

    // 이번 기믹에서 비콘이 내려가지 않은 모서리(지원 드론이 나타날 자리)입니다.
    public bool TryGetFreeCorner(out Vector3 position)
    {
        position = freeCornerPosition;
        return hasFreeCorner;
    }

    private EnemyHealth health;
    private BossDamageReduction reduction;
    private bool warnedNoReduction;

    private float tiltWeight;     // 0~1 (그로기가 끝난 뒤에도 0이 될 때까지 서서히 빠짐)
    private float wobblePhase;
    private Quaternion tiltBaseRotation;
    private bool tiltBaseSaved;
    private float colorWeight;    // 0~1 그로기 색 섞임
    private float chargeGlowWeight; // 0~1 차지 흰 발광 섞임(그로기와 겹치지 않음)

    // IEnemyColorOverride - EnemyHealth 피격 깜빡임이 돌아갈 색(그로기 동안 노란색), EnemyControlState는 이 동안 칠하지 않고 양보합니다.
    // 레이저 차지 동안에는 흰 발광(chargeGlowColor)도 같은 방식으로 돌아갈 색이 됩니다.
    public bool HasColorOverride => (colorWeight > 0f || chargeGlowWeight > 0f) && health != null && health.RendSupportsColor;
    public Color OverrideColor
    {
        get
        {
            Color baseColor = health != null ? health.OriginalColor : groggyColor;
            Color c = Color.Lerp(baseColor, groggyColor, colorWeight);
            return Color.Lerp(c, chargeGlowColor, chargeGlowWeight);
        }
    }
    public Color OverrideEmission
    {
        get
        {
            Color baseEmission = health != null ? health.OriginalEmission : Color.black;
            Color e = Color.Lerp(baseEmission, groggyColor * groggyEmissionBoost, colorWeight);
            return Color.Lerp(e, chargeGlowColor * chargeGlowEmissionBoost, chargeGlowWeight);
        }
    }

    // 1레벨 보스의 연사/돌진/전방위 부품은 쓰지 않으므로 그 경고 없이 필요한 것만 준비합니다.
    protected override void Awake()
    {
        CacheCoreComponents();
        // 다른 적처럼 위치는 NavMeshAgent만 움직이게 물리 영향을 끕니다(2레벨 보스와 같은 이유 - 판정 위치 어긋남 방지).
        var body = GetComponent<Rigidbody>();
        if (body != null)
        {
            body.isKinematic = true;
            body.useGravity = false;
        }
        health = GetComponent<EnemyHealth>();
        reduction = GetComponent<BossDamageReduction>();
        laser = GetComponent<BossLaserBeam>();
        if (laser == null) Debug.LogWarning($"BossLv3AIController ({gameObject.name}): BossLaserBeam이 없어 레이저 없이 진행합니다.");
        if (groggyTiltTarget == null) groggyTiltTarget = FindModelChild();

        // 2레벨 보스 프리팹을 복제해 만들었을 때 남은 부품 확인(보호막 두 개가 동시에 피해/제어를 거르면 규칙이 꼬임).
        if (GetComponent<BossShield>() != null)
            Debug.LogWarning($"BossLv3AIController ({gameObject.name}): BossShield가 붙어 있습니다 - 3레벨 보스는 BossDamageReduction만 씁니다. BossShield를 떼 주세요.");
        if (GetComponent<BossLv2AIController>() != null)
            Debug.LogWarning($"BossLv3AIController ({gameObject.name}): BossLv2AIController가 함께 붙어 있습니다 - 떼 주세요.");

        // 접촉 트리거(MeleeHitbox)에 능력치를 연결합니다(평소 접촉 피해 = Attack Power).
        var contact = GetComponentInChildren<EnemyMeleeContactTrigger>(true);
        if (contact != null) contact.Initialize(stats);

        TryFindPlayer();
    }

    protected override void Start()
    {
        base.Start();
        // 몸(과 레이저)은 이 컨트롤러가 직접 돌립니다(NavMeshAgent가 방향을 되돌리지 않게).
        if (agent != null) agent.updateRotation = false;
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        // 충전 도중 꺼졌다 켜지면 감소막이 충전을 없던 일로 하므로 처음부터.
        if (state == State.Charging) state = State.Entering;
    }

    void OnDestroy()
    {
        if (beaconSmokeMaterial != null) Destroy(beaconSmokeMaterial);
    }

    void OnDisable()
    {
        if (state == State.Charging) state = State.Entering;
        // 방이 꺼지면 레이저도 꺼짐(BossLaserBeam.OnDisable) - 기믹 중이었다면 켜질 때 조준선부터 다시.
        if (state == State.Gimmick) state = State.Entering;
        laserFiring = false;
        reversing = false;
        if (state == State.Entering) ResetGroggyStacks(); // 기믹 처음부터 다시 = 스택도 처음부터
        ClearBeacons(false); // 방이 꺼지면 비콘도 지움(다시 켜지면 기믹 처음부터 - 새 비콘)
        ClearSupply(false);
        tiltWeight = 0f;
        ApplyTilt();
        if (colorWeight > 0f || chargeGlowWeight > 0f)
        {
            colorWeight = 0f;
            chargeGlowWeight = 0f;
            PaintGroggyColor();
        }
    }

    protected override void Update()
    {
        UpdateTilt();
        UpdateGroggyColor();

        // 제자리 고정(이동하지 않음).
        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh) agent.isStopped = true;

        if (health != null && health.IsDead)
        {
            if (reduction != null && (reduction.IsActive || reduction.IsCharging)) reduction.Remove(true);
            if (laser != null && laser.IsActive) laser.StopImmediate();
            if (beacons.Count > 0) ClearBeacons(true);
            if (deliveries.Count > 0) ClearSupply(true);
            return;
        }

        if (player == null) TryFindPlayer();

        switch (state)
        {
            case State.Entering: UpdateEntering(); break;
            case State.Charging: UpdateChargingState(); break;
            case State.Gimmick: UpdateGimmick(); break;
            case State.Breaking: UpdateBreaking(); break;
            case State.Groggy: UpdateGroggy(); break;
        }
    }

    // ---------- 충전 ----------

    private void UpdateEntering()
    {
        if (reduction == null)
        {
            if (!warnedNoReduction)
            {
                warnedNoReduction = true;
                Debug.LogWarning($"BossLv3AIController ({gameObject.name}): BossDamageReduction이 없어 감소막 없이 기믹을 진행합니다.");
            }
            state = State.Gimmick;
            return;
        }
        if (reduction.BeginCharge()) state = State.Charging;
    }

    private void UpdateChargingState()
    {
        if (reduction == null || reduction.IsActive) { EnterGimmick(); return; }
        // 충전이 어떤 이유로 끊겼으면(방이 잠깐 꺼짐 등) 다시 시작.
        if (!reduction.IsCharging) state = State.Entering;
    }

    // ---------- 기믹 (레이저 + 그로기 비콘) ----------

    private void EnterGimmick()
    {
        state = State.Gimmick;
        stateTimer = 0f;
        laserFiring = false;
        firingElapsed = 0f;
        beaconsSpawned = false;
        waveIndex = 0;
        reversing = false;
        waveDirection = 1f;
        ResetGroggyStacks();
        ClearBeacons(false);
        if (laser != null) laser.ShowTelegraph(telegraphTime, chargeTime);
    }

    private void UpdateGimmick()
    {
        stateTimer += Time.deltaTime;

        if (!laserFiring)
        {
            if (stateTimer >= telegraphTime)
            {
                laserFiring = true;
                firingElapsed = 0f;
                if (laser != null) laser.StartFiring();
                SpawnFireWave();
            }
        }
        else
        {
            firingElapsed += Time.deltaTime;
            if (waveIndex > 0) reverseElapsed += Time.deltaTime;
            RotateBody(CurrentRotationVelocity());

            if (reversing)
            {
                // 웨이브 사이: 감속 -> 반대 방향 가속(가속이 덜 끝났어도 비콘이 내려온 뒤 이어서 가속), 가속 시작 Next Wave Delay 뒤 다음 웨이브.
                if (reverseElapsed >= reverseDecelTime + nextWaveDelay)
                {
                    reversing = false;
                    SpawnBeacons();
                }
                return;
            }

            if (!beaconsSpawned && waveIndex == 0 && firingElapsed >= LaserBurstDuration + beaconDelayAfterBurst) SpawnBeacons();
            if (beaconsSpawned) UpdateResupply();
        }
    }

    // 웨이브의 비콘을 모두 해제했는데 마지막 웨이브가 아님 -> 지금 속도에서 감속해 멈췄다가 반대 방향으로 가속(형빈 확정).
    private void BeginReverse()
    {
        reverseStartVelocity = CurrentRotationVelocity(); // 역회전 상태로 바꾸기 전 속도
        reversing = true;
        reverseElapsed = 0f;
        beaconsSpawned = false;
        waveIndex++;
        waveDirection = -waveDirection;
    }

    // 모든 비콘을 해제한 순간(HandleBeaconBroken). 레이저가 사그라드는 시간 뒤 해제 피해.
    public void BeginBreak()
    {
        if (state != State.Gimmick) return;
        breakStartRotationSpeed = laserFiring ? CurrentRotationVelocity() : 0f;
        reversing = false;
        gaugeFull = true;
        state = State.Breaking;
        stateTimer = breakFadeTime;
        laserFiring = false;
        if (laser != null) laser.BeginFade(breakFadeTime); // 판정은 바로 꺼지고 외형만 사그라듦(회전은 UpdateBreaking에서 서서히 멈춤)
    }

    // 발사 직후 굵은 레이저가 기존 폭으로 줄어드는 데 걸리는 시간(레이저가 없으면 0).
    private float LaserBurstDuration => laser != null ? laser.BurstDuration : 0f;

    // 발사 직후 굵기 유지가 끝나는 시간(줄어들기 시작 - 레이저가 없으면 0).
    private float LaserBurstHoldDuration => laser != null ? laser.BurstHoldDuration : 0f;

    // 첫 웨이브: 레이저가 줄어들기 시작한 뒤 Rotation Delay After Burst가 지나면 0에서 Rotation Ramp Time에 걸쳐 최대 속도까지 오르고, 그 뒤 고정.
    private float CurrentRotationSpeed()
    {
        float t = firingElapsed - LaserBurstHoldDuration - rotationDelayAfterBurst;
        if (t <= 0f) return 0f;
        if (rotationRampTime <= 0f) return maxRotationSpeed;
        return maxRotationSpeed * Mathf.Clamp01(t / rotationRampTime);
    }

    // 지금 회전 속도(부호 포함: 양수 = Rotate Counter Clockwise 설정 방향, 음수 = 반대).
    // 둘째 웨이브부터(역회전 뒤): 시작 속도에서 Reverse Decel Time 동안 부드럽게 0까지 -> 반대 방향으로 Reverse Accel Time 동안
    // 최대 속도까지(부드럽게) -> 그 방향 최대 속도로 고정.
    private float CurrentRotationVelocity()
    {
        if (waveIndex > 0)
        {
            if (reverseElapsed < reverseDecelTime)
            {
                float k = Mathf.SmoothStep(1f, 0f, reverseElapsed / Mathf.Max(reverseDecelTime, 0.0001f));
                return reverseStartVelocity * k;
            }
            float a = reverseAccelTime > 0f ? Mathf.Clamp01((reverseElapsed - reverseDecelTime) / reverseAccelTime) : 1f;
            return waveDirection * maxRotationSpeed * Mathf.SmoothStep(0f, 1f, a);
        }
        return waveDirection * CurrentRotationSpeed();
    }

    // 루트(몸 + 레이저 기준 방향)를 Y축으로 돌립니다. 유니티에서 +Y 회전은 위에서 봤을 때 시계 방향이라 반시계면 음수.
    // velocity는 부호 포함(양수 = Rotate Counter Clockwise 설정 방향).
    private void RotateBody(float velocity)
    {
        if (velocity == 0f) return;
        float sign = rotateCounterClockwise ? -1f : 1f;
        transform.Rotate(0f, sign * velocity * Time.deltaTime, 0f, Space.World);
    }

    // ---------- 그로기 스택 (IBossGroggyGauge - 보스 체력바 오른쪽 게이지) ----------

    public bool ShowsGroggyGauge => health == null || !health.IsDead;

    // 비콘 하나당 1 / (3 x 웨이브 수)씩. 기믹 해제 ~ 그로기 끝까지는 가득.
    public float GroggyGaugeRatio
    {
        get
        {
            if (gaugeFull || state == State.Breaking || state == State.Groggy) return 1f;
            int required = 3 * Mathf.Max(1, beaconWavesBeforeGroggy);
            return Mathf.Clamp01(brokenBeaconCount / (float)required);
        }
    }

    private void ResetGroggyStacks()
    {
        brokenBeaconCount = 0;
        gaugeFull = false;
    }

    private void UpdateBreaking()
    {
        // 사그라드는 동안 회전을 그때 속도에서 0까지 일정하게 줄입니다(브레이크를 밟듯 - 레이저 외형도 같이 돎).
        if (breakFadeTime > 0f && breakStartRotationSpeed != 0f)
        {
            float k = Mathf.Clamp01(stateTimer / breakFadeTime); // 1 -> 0
            RotateBody(breakStartRotationSpeed * k);
        }

        stateTimer -= Time.deltaTime;
        if (stateTimer > 0f) return;

        // 연쇄 폭파 + 최대 체력 x 비율(감소 없이, Weak) -> 감소막이 사라짐 -> 그로기.
        CombatVfxSpawner.PlayDeathExplosionAt(transform.position, breakExplosionScale);
        float damage = health != null ? health.MaxHealth * breakDamageRatio : 0f;
        if (reduction != null) reduction.ApplyBreakDamage(health, damage);
        else if (health != null && damage > 0f) health.TakeWeakDamage(damage);
        if (reduction != null) reduction.Remove(true);

        if (health != null && health.IsDead) return;

        GameAudio.Play(SfxId.BossGroggy);
        state = State.Groggy;
        stateTimer = groggyDuration;
    }

    // 레이저 발사 신호 물결(그림만). 연출이라 오류가 나도 기믹은 그대로 진행합니다.
    private void SpawnFireWave()
    {
        if (fireWave == null || fireWave.waveMaterial == null) return;
        try
        {
            Vector3 center = transform.position;
            if (!TryGetRoomRect(out Rect rect))
            {
                float h = Mathf.Max(beaconFallbackHalfSize, 0.5f);
                rect = new Rect(center.x - h, center.z - h, h * 2f, h * 2f);
            }
            float dx = Mathf.Max(Mathf.Abs(center.x - rect.xMin), Mathf.Abs(center.x - rect.xMax));
            float dz = Mathf.Max(Mathf.Abs(center.z - rect.yMin), Mathf.Abs(center.z - rect.yMax));
            float maxRadius = Mathf.Max(Mathf.Sqrt(dx * dx + dz * dz), 0.1f);
            RoomWavePulse.SpawnVisualOnly(fireWave, center, maxRadius, rect);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"BossLv3AIController ({gameObject.name}): 레이저 신호 물결 생성 중 오류가 발생해 물결 없이 진행합니다: {e}");
        }
    }

    // ---------- 그로기 비콘 ----------

    // 방 모서리 네 곳 중 무작위 세 곳에 수류탄/해킹/총알 비콘을 하나씩(종류 순서도 무작위), 남은 한 곳은 지원 드론 자리로 둡니다.
    private void SpawnBeacons()
    {
        beaconsSpawned = true;
        ClearBeacons(false);

        Vector3 center = transform.position;
        if (!TryGetRoomRect(out Rect rect))
        {
            float h = Mathf.Max(beaconFallbackHalfSize, 0.5f);
            rect = new Rect(center.x - h, center.z - h, h * 2f, h * 2f);
        }

        float inset = Mathf.Min(beaconCornerInset, rect.width * 0.45f, rect.height * 0.45f);
        Vector2[] corners =
        {
            new Vector2(rect.xMax - inset, rect.yMax - inset),
            new Vector2(rect.xMin + inset, rect.yMax - inset),
            new Vector2(rect.xMin + inset, rect.yMin + inset),
            new Vector2(rect.xMax - inset, rect.yMin + inset),
        };

        int[] order = { 0, 1, 2, 3 };
        Shuffle(order);
        GroggyBeacon.BeaconType[] types = { GroggyBeacon.BeaconType.Grenade, GroggyBeacon.BeaconType.Hack, GroggyBeacon.BeaconType.Bullet };
        Shuffle(types);

        for (int i = 0; i < types.Length; i++)
        {
            Vector3 position = ResolveCornerPosition(corners[order[i]], center);
            GroggyBeacon beacon = CreateBeacon(types[i], position);
            if (beacon != null)
            {
                beacons.Add(beacon);
                beaconCorners[beacon] = position;
            }
        }

        freeCornerPosition = ResolveCornerPosition(corners[order[3]], center);
        hasFreeCorner = true;

        // 첫 보급: 비콘과 함께 남은 모서리에 수류탄 1 + 해킹툴 1.
        ClearSupply(false);
        grenadeResupplyTimer = 0f;
        hackResupplyTimer = 0f;
        StartDelivery(freeCornerPosition, new[] { BasicItemType.Grenade, BasicItemType.HackingTool });
    }

    // ---------- 지원 드론 보급 ----------

    private void StartDelivery(Vector3 floor, IList<BasicItemType> items)
    {
        BeaconSupplyDrone drone = BeaconSupplyDrone.Spawn(supplySettings, floor, transform.position, transform.position, items);
        if (drone != null) deliveries.Add(drone);
    }

    // 재보급: 수류탄(해킹) 비콘이 남아 있고 + 플레이어 수류탄(해킹툴) 0개 + 열지 않은 보급 상자가 없고 + 안 가져간 수류탄(해킹툴)이 없는
    // 상태가 Resupply Delay 동안 이어지면 그 재화만 든 상자를 비콘이 없는 모서리에 보급합니다(둘 다 그렇게 되면 한 상자에 둘 다).
    private void UpdateResupply()
    {
        deliveries.RemoveAll(d => d == null);
        if (playerResources == null) playerResources = FindFirstObjectByType<PlayerResources>();
        if (playerResources == null) return;

        bool anyUnopened = false;
        for (int i = 0; i < deliveries.Count; i++)
        {
            if (deliveries[i].HasUnopenedBox) { anyUnopened = true; break; }
        }

        bool needGrenade = !anyUnopened && NeedsResupply(GroggyBeacon.BeaconType.Grenade, BasicItemType.Grenade);
        bool needHack = !anyUnopened && NeedsResupply(GroggyBeacon.BeaconType.Hack, BasicItemType.HackingTool);
        grenadeResupplyTimer = needGrenade ? grenadeResupplyTimer + Time.deltaTime : 0f;
        hackResupplyTimer = needHack ? hackResupplyTimer + Time.deltaTime : 0f;

        float delay = supplySettings != null ? supplySettings.resupplyDelay : 3f;
        bool grenadeReady = needGrenade && grenadeResupplyTimer >= delay;
        bool hackReady = needHack && hackResupplyTimer >= delay;
        if (!grenadeReady && !hackReady) return;

        var items = new List<BasicItemType>();
        if (grenadeReady) items.Add(BasicItemType.Grenade);
        if (hackReady) items.Add(BasicItemType.HackingTool);
        grenadeResupplyTimer = 0f;
        hackResupplyTimer = 0f;
        StartDelivery(PickResupplyCorner(), items);
    }

    private bool NeedsResupply(GroggyBeacon.BeaconType beaconType, BasicItemType resource)
    {
        bool beaconLeft = false;
        for (int i = 0; i < beacons.Count; i++)
        {
            if (beacons[i] != null && !beacons[i].IsBroken && beacons[i].Type == beaconType) { beaconLeft = true; break; }
        }
        if (!beaconLeft || playerResources.GetAmount(resource) > 0) return false;
        for (int i = 0; i < deliveries.Count; i++)
        {
            if (deliveries[i].HasUnpicked(resource)) return false;
        }
        return true;
    }

    // 비콘이 없는 모서리(처음 남겨 둔 모서리 + 비콘이 해제된 모서리) 중 놓인 보급 상자가 없는 곳. 없으면 남겨 둔 모서리.
    private Vector3 PickResupplyCorner()
    {
        var candidates = new List<Vector3>();
        if (hasFreeCorner) candidates.Add(freeCornerPosition);
        foreach (var pair in beaconCorners)
        {
            GroggyBeacon b = pair.Key;
            if (b == null || b.IsBroken || !beacons.Contains(b)) candidates.Add(pair.Value);
        }
        foreach (var candidate in candidates)
        {
            bool occupied = false;
            for (int i = 0; i < deliveries.Count; i++)
            {
                if (!deliveries[i].HasLiveBox) continue;
                Vector3 d = deliveries[i].FloorPoint - candidate;
                d.y = 0f;
                if (d.sqrMagnitude < 0.8f * 0.8f) { occupied = true; break; }
            }
            if (!occupied) return candidate;
        }
        return candidates.Count > 0 ? candidates[0] : freeCornerPosition;
    }

    // 보급 드론/상자를 모두 정리합니다(fade = 흰 빛으로 사라짐 - 기믹 해제/보스 사망, 아니면 바로 - 방이 꺼짐/새 기믹).
    private void ClearSupply(bool fade)
    {
        for (int i = 0; i < deliveries.Count; i++)
        {
            if (deliveries[i] != null) deliveries[i].Vanish(fade);
        }
        deliveries.Clear();
        grenadeResupplyTimer = 0f;
        hackResupplyTimer = 0f;
    }

    // 모서리 근처 NavMesh 위 자리(2레벨 보스 소환과 같은 규칙). 못 찾으면 모서리 그대로(보스 높이).
    private Vector3 ResolveCornerPosition(Vector2 corner2D, Vector3 center)
    {
        Vector3 corner = new Vector3(corner2D.x, center.y, corner2D.y);
        if (TryFindCornerPosition(corner, center, out Vector3 position)) return position;
        Debug.LogWarning($"BossLv3AIController ({gameObject.name}): 모서리 근처 NavMesh 자리를 찾지 못해 모서리 위치에 그대로 둡니다.");
        return corner;
    }

    private GroggyBeacon CreateBeacon(GroggyBeacon.BeaconType type, Vector3 position)
    {
        try
        {
            var root = new GameObject($"GroggyBeacon_{type}");
            // 보스(회전함)가 아니라 방 콘텐츠 밑에 둡니다 - 방 초기화 때 함께 정리되고, 보스가 돌아도 제자리.
            root.transform.SetParent(transform.parent, false);
            root.transform.SetPositionAndRotation(position, Quaternion.identity);

            GameObject model;
            if (beaconModelPrefab != null)
            {
                model = Instantiate(beaconModelPrefab, root.transform);
                model.transform.localPosition = Vector3.zero;
            }
            else
            {
                if (!warnedNoBeaconModel)
                {
                    warnedNoBeaconModel = true;
                    Debug.LogWarning($"BossLv3AIController ({gameObject.name}): Beacon Model Prefab이 비어 임시 상자로 비콘을 만듭니다.");
                }
                model = GameObject.CreatePrimitive(PrimitiveType.Cube);
                model.transform.SetParent(root.transform, false);
                model.transform.localPosition = Vector3.up * 0.5f;
            }
            model.name = "Model";

            if (beaconSmokeMaterial == null)
            {
                Shader shader = beaconSmokeShader != null ? beaconSmokeShader : Shader.Find("MobArchive/BossLaserSmoke");
                if (shader != null) beaconSmokeMaterial = new Material(shader) { name = "GroggyBeaconSmoke_Material" };
            }

            var beacon = root.AddComponent<GroggyBeacon>();
            // 보스 쪽을 바라보게(바닥 평면 기준) + 모델 정면 보정.
            Vector3 toBoss = transform.position - position;
            toBoss.y = 0f;
            Quaternion facing = toBoss.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(toBoss.normalized, Vector3.up) : Quaternion.identity;
            facing *= Quaternion.Euler(0f, beaconFacingOffset, 0f);
            beacon.Setup(type, position, facing, beaconSettings, beaconSmokeMaterial, HandleBeaconBroken, GetBeaconBooster());
            return beacon;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"BossLv3AIController ({gameObject.name}): {type} 비콘 생성 중 오류: {e}");
            return null;
        }
    }

    // 비콘 하나가 해제될 때(GroggyBeacon.Break). 그로기 스택 +1. 이번 웨이브의 비콘이 모두 해제되면 마지막 웨이브면 기믹 해제,
    // 아니면 레이저 역회전 후 다음 웨이브.
    private void HandleBeaconBroken(GroggyBeacon broken)
    {
        if (state == State.Gimmick) brokenBeaconCount++;
        beacons.Remove(broken);
        for (int i = 0; i < beacons.Count; i++)
        {
            if (beacons[i] != null && !beacons[i].IsBroken) return;
        }
        beacons.Clear();
        beaconCorners.Clear();
        hasFreeCorner = false;
        ClearSupply(true); // 남은 보급 상자와 안의 것은 바로 사라짐(재화 무한 수급 방지 - 형빈 확정)
        if (state != State.Gimmick) return;
        if (waveIndex + 1 < Mathf.Max(1, beaconWavesBeforeGroggy) && laserFiring) BeginReverse();
        else BeginBreak();
    }

    // 남은 비콘을 지웁니다(fadeOut = 흰 빛으로 사라짐 - 보스 사망). 해제로 치지 않습니다.
    private void ClearBeacons(bool fadeOut)
    {
        for (int i = 0; i < beacons.Count; i++)
        {
            if (beacons[i] != null) beacons[i].Discard(fadeOut);
        }
        beacons.Clear();
        beaconCorners.Clear();
        hasFreeCorner = false;
    }

    private static void Shuffle<T>(T[] array)
    {
        for (int i = array.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (array[i], array[j]) = (array[j], array[i]);
        }
    }

    // 모서리에서 시작해 보스 쪽으로 조금씩 옮겨 가며, NavMesh 위이고 보스 자리와 이어진(=방 바닥) 자리를 찾습니다(2레벨 보스와 같은 규칙).
    private bool TryFindCornerPosition(Vector3 corner, Vector3 center, out Vector3 result)
    {
        const int steps = 6;
        for (int i = 0; i <= steps; i++)
        {
            Vector3 candidate = Vector3.Lerp(corner, center, i / (float)(steps + 2));
            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 1.5f, NavMesh.AllAreas)) continue;

            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(center, hit.position, NavMesh.AllAreas, path)) continue;
            if (path.status != NavMeshPathStatus.PathComplete) continue;

            result = hit.position;
            return true;
        }

        result = Vector3.zero;
        return false;
    }

    // 보스가 들어 있는 방(방 콘텐츠의 자식)이 차지한 칸들을 덮는 바닥 사각형(x/z) - 2레벨 보스와 같은 계산.
    private bool TryGetRoomRect(out Rect rect)
    {
        rect = default;
        if (levelBuilder == null) levelBuilder = FindFirstObjectByType<LevelBuilder>();
        if (levelBuilder == null || levelBuilder.cellSize <= 0f) return false;

        float cell = levelBuilder.cellSize;
        foreach (var room in levelBuilder.GeneratedRooms)
        {
            if (room == null || room.occupiedCells == null || room.occupiedCells.Count == 0) continue;
            var controller = levelBuilder.GetRoomController(room.id);
            if (controller == null || controller.ContentRoot == null) continue;
            if (!transform.IsChildOf(controller.ContentRoot.transform)) continue;

            float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;
            foreach (var c in room.occupiedCells)
            {
                minX = Mathf.Min(minX, c.x * cell - cell * 0.5f);
                maxX = Mathf.Max(maxX, c.x * cell + cell * 0.5f);
                minZ = Mathf.Min(minZ, c.y * cell - cell * 0.5f);
                maxZ = Mathf.Max(maxZ, c.y * cell + cell * 0.5f);
            }
            rect = Rect.MinMaxRect(minX, minZ, maxX, maxZ);
            return true;
        }
        return false;
    }

    // ---------- 그로기 ----------

    private void UpdateGroggy()
    {
        stateTimer -= Time.deltaTime;
        if (stateTimer > 0f) return;
        state = State.Entering; // 체력이 남아 있으므로 다시 충전부터
        ResetGroggyStacks();    // 그로기가 끝나면 게이지를 비움(다음 기믹에서 다시 6개)
    }

    private void UpdateTilt()
    {
        float target01 = state == State.Groggy ? 1f : 0f;
        if (tiltWeight == target01 && tiltWeight == 0f) return;

        tiltWeight = Mathf.MoveTowards(tiltWeight, target01, Time.deltaTime / Mathf.Max(groggyBlendTime, 0.01f));
        if (tiltWeight > 0f) wobblePhase += groggyWobbleSpeed * 360f * Time.deltaTime;
        ApplyTilt();
    }

    // 2레벨 보스와 같은 어지러움: 루트 기준 수평 축으로 기울이고, 그 축이 wobblePhase를 따라 돎(모델 자식만 - 루트는 그대로).
    private void ApplyTilt()
    {
        if (groggyTiltTarget == null) return;
        bool posed = tiltWeight > 0f;
        if (!tiltBaseSaved)
        {
            if (!posed) return;
            tiltBaseRotation = groggyTiltTarget.localRotation;
            tiltBaseSaved = true;
        }

        Vector3 axis = Quaternion.Euler(0f, wobblePhase, 0f) * Vector3.right;
        groggyTiltTarget.localRotation = Quaternion.AngleAxis(groggyTiltAngle * tiltWeight, axis) * tiltBaseRotation;

        if (!posed)
        {
            groggyTiltTarget.localRotation = tiltBaseRotation;
            tiltBaseSaved = false;
        }
    }

    // 그로기 색은 섞임 비율이 바뀌는 동안만 직접 칠하고, 그 사이에는 피격 깜빡임이 OverrideColor로 돌아오게 둡니다.
    // 레이저 차지 흰 발광도 여기서 함께 칠합니다: 조준선 앞부분(Charge Time) 동안 0 -> 1로 서서히 뒤덮고, 남은 조준선 동안 1 유지,
    // 발사(또는 기믹이 끊김) 뒤 Charge Glow Return Time 동안 본래 색으로 빠르게 돌아옴.
    private void UpdateGroggyColor()
    {
        if (health == null || health.IsDead) return;
        bool changed = false;

        float target01 = state == State.Groggy ? 1f : 0f;
        if (colorWeight != target01)
        {
            colorWeight = Mathf.MoveTowards(colorWeight, target01, Time.deltaTime / Mathf.Max(groggyBlendTime, 0.01f));
            changed = true;
        }

        float glow;
        if (state == State.Gimmick && !laserFiring)
        {
            float t = Mathf.Clamp01(stateTimer / Mathf.Max(chargeTime, 0.01f));
            glow = Mathf.Max(chargeGlowWeight, t * t * (3f - 2f * t)); // 부드럽게 차오름(되돌아가지 않음)
        }
        else
        {
            glow = Mathf.MoveTowards(chargeGlowWeight, 0f, Time.deltaTime / Mathf.Max(chargeGlowReturnTime, 0.01f));
        }
        if (glow != chargeGlowWeight)
        {
            chargeGlowWeight = glow;
            changed = true;
        }

        if (changed) PaintGroggyColor();
    }

    private void PaintGroggyColor()
    {
        if (health == null || !health.RendSupportsColor) return;
        Renderer r = health.TargetRenderer;
        if (r == null) return;

        bool overridden = colorWeight > 0f || chargeGlowWeight > 0f;
        Color c = overridden ? OverrideColor : health.OriginalColor;
        Color e = overridden ? OverrideEmission : health.OriginalEmission;
        if (health.UsesBaseColor) r.material.SetColor("_BaseColor", c);
        if (health.UsesColor) r.material.SetColor("_Color", c);
        if (health.UsesEmission) r.material.SetColor("_EmissionColor", e);
    }

    // 렌더러가 있는 첫 직계 자식(모델). 접촉 판정(MeleeHitbox) 등 렌더러 없는 자식은 건너뜁니다.
    private Transform FindModelChild()
    {
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child.GetComponentInChildren<Renderer>(true) != null) return child;
        }
        return null;
    }
}
