using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// 2레벨 보스 컨트롤러입니다. BossAIController를 상속해 보스 판정(체력바, 매료/오한/넉백/87번 제외 등)과 이동/시야/후퇴 부품을 그대로 받습니다.
// 평상시 패턴: 5연사(EnemyRangedAttack - 연사 수/간격은 그 컴포넌트, 주기 = 1 / EnemyStats.rangedAttackSpeed) + 후퇴 백스텝 샷건
// (후퇴가 시작되면 진행 중이던 연사를 끊고 74번처럼 잔상과 함께 뒤로 짧게 빠진 뒤, 백스텝이 끝나는 순간 샷건 1회 + 기본 연사 주기를 처음부터
// 다시 셈. 붙어 있는 동안 Shotgun Interval마다 반복). 이동(접근/제자리/후퇴)은 1레벨 보스와 같습니다.
// 보호막(BossShield): 방에 처음 들어오면 충전(그동안 제자리에서 목표만 바라봄, 무적/제어 면역은 BossShield가 처리) -> 보호막이 있는 동안은 수면 등에
// 멈추지 않음(해킹은 표시만). 보호막이 깨지면 그로기(이동/공격 정지 + 모델 기울기 방향만 빙글 도는 어지러움, 제어와 무관하게 진행) ->
// 깨진 순간부터 Recharge Delay(그로기 포함) 뒤 다시 충전. 그로기 동안 몸은 노란색(IEnemyColorOverride - 피격 깜빡임 뒤에도 노란색으로 돌아오고,
// 해킹 등 제어 색보다 우선). 그로기가 끝나면 주황 물결(신호 연출만) + 방 네 모서리에 적 소환(물결 앞이 그 모서리에 닿는 순간 나타남,
// 소환된 적까지 다 죽어야 방 클리어 - EnemyKillObjective.TryAddAlongside). 소환된 적은 흰 빛에서 서서히 나타나며 그동안 무해(SummonMaterialize),
// 보스는 물결 뒤 Summon Attack Delay 동안 이동만 하고 쏘지 않음.
// 포격: 보호막이 생기는 순간 멈춰서 모델 전면을 하늘로 들고 돌며 수류탄 모델을 쏘아 올리고(연출), 이후 보호막이 있는 동안 포격(BossArtilleryStrike)을
// 하나씩 반복(조준 표시 뒤 폭발, 폭발 뒤 간격, N번째마다 플레이어 위치).
// 벽 돌진: 발동 모션이 끝나면 첫 돌진 충전 이펙트(Wall Dash Charge Vfx)와 함께 보호막이 깨질 때까지 돌진 반복 - 조준(첫 1초, 이후 0.3초) 뒤 벽/장애물(NavMesh 경계)에 닿을 때까지
// 직진, 조준/돌진 중 몸이 1레벨 보스처럼 물들어 발광, 돌진 중 주황 잔상 + 돌진 피해(IDashDamageSource - 접촉 트리거가 읽음). 목표를 놓치면(112번 투명 등) 멈췄다가 다시 보이면 처음부터
// (충전 이펙트 + 첫 조준). 기획은 Docs/설계노트.md [2레벨 보스].
public class BossLv2AIController : BossAIController, IEnemyColorOverride, IDashDamageSource, ICancellableAttack
{
    [Header("2레벨 보스 - 후퇴 샷건")]
    [Tooltip("후퇴하는 동안 이 간격(초)마다 백스텝 + 샷건(붙어 있는 동안 계속). 간격은 샷건을 쏜 순간부터 셈.")]
    [Min(0.1f)] public float shotgunInterval = 2f;
    [Tooltip("샷건 발 수.")]
    [Min(1)] public int shotgunPelletCount = 5;
    [Tooltip("샷건 전체 퍼짐 각도(도) - 가장 왼쪽 탄과 오른쪽 탄 사이.")]
    public float shotgunSpreadAngle = 90f;
    [Tooltip("샷건 한 발 데미지(연사 데미지는 EnemyStats의 Ranged Damage).")]
    public float shotgunDamage = 1f;
    [Tooltip("샷건 탄환 크기(연사 탄환 크기는 EnemyStats의 Ranged Projectile Size).")]
    [Min(0.01f)] public float shotgunProjectileSize = 0.05f;
    [Tooltip("샷건 탄속(연사 탄속은 EnemyStats의 Ranged Projectile Speed). 피할 여지를 주려고 느리게.")]
    [Min(0.01f)] public float shotgunProjectileSpeed = 1.5f;

    [Header("2레벨 보스 - 샷건 전 백스텝 (74번 돌진처럼)")]
    [Tooltip("목표 반대쪽으로 빠지는 거리. 벽/장애물 쪽이면 옆으로 비껴 빠지고, 빠질 곳이 없으면 제자리에서 같은 시간만큼 멈췄다가 쏨.")]
    [Min(0f)] public float backstepDistance = 1f;
    [Tooltip("백스텝에 걸리는 시간(초). 끝나는 순간 샷건을 쏨.")]
    [Min(0.05f)] public float backstepDuration = 0.25f;
    [Tooltip("백스텝 잔상(74번과 같은 방식 - 보스 모델의 그 순간 모습을 반투명 단색으로 남김).")]
    public DashAfterimageSettings backstepAfterimage = new DashAfterimageSettings { color = new Color(1f, 0.55f, 0.15f, 0.45f) };

    [Header("2레벨 보스 - 그로기 / 보호막 주기")]
    [Tooltip("보호막이 깨진 뒤 그로기 시간(초).")]
    [Min(0f)] public float groggyDuration = 5f;
    [Tooltip("보호막이 깨진 순간부터 다시 충전할 때까지(초, 그로기 포함).")]
    [Min(0f)] public float rechargeDelay = 20f;
    [Tooltip("그로기 때 기울일 모델(보스 루트의 자식). 비우면 렌더러가 있는 첫 자식을 자동으로 씀.")]
    public Transform groggyTiltTarget;
    [Tooltip("그로기 기울기 각도(도).")]
    [Range(0f, 60f)] public float groggyTiltAngle = 15f;
    [Tooltip("기울어진 방향이 한 바퀴 도는 횟수(초당).")]
    public float groggyWobbleSpeed = 1f;
    [Tooltip("그로기 시작/끝에 기울기가 서서히 들어가고 빠지는 시간(초).")]
    [Min(0.01f)] public float groggyBlendTime = 0.3f;

    [Tooltip("그로기 동안의 몸 색.")]
    public Color groggyColor = new Color(1f, 0.85f, 0.1f, 1f);
    [Tooltip("그로기 동안 발광 세기(그로기 색 x 이 값, 0이면 발광 없음).")]
    [Min(0f)] public float groggyEmissionBoost = 0.6f;

    [Header("2레벨 보스 - 그로기 뒤 소환")]
    [Tooltip("그로기가 끝날 때 소환할 적(일반 적 Enemy Definition). 방 네 모서리를 차례로 돌며 하나씩 놓습니다 - 0번과 2번, 1번과 3번이 서로 " +
             "대각선 맞은편이고, 다섯 번째부터는 다시 첫 모서리부터. 수치/공격 방식/AI는 일반 방에 스폰될 때와 똑같습니다.")]
    public List<EnemyDefinition> summonDefinitions = new List<EnemyDefinition>();
    [Tooltip("모서리에서 방 안쪽으로 들어온 거리(m). 그 자리가 막혀 있으면 방 중심 쪽으로 조금씩 옮겨 찾습니다.")]
    [Min(0f)] public float summonCornerInset = 1.5f;
    [Tooltip("소환 신호 물결 - 90번과 같은 그림만 퍼집니다(피해/적 총알 제거/공격 중단/발광/공격 금지 없음). Wave Material에 90번 아이템과 " +
             "같은 RoomWave 재질을 넣어 주세요. 이 안의 '물결 진행'(Expand Duration, Fade Start Progress)과 '물결 모양' 칸만 쓰고 나머지 칸은 무시합니다.")]
    public RoomWaveClearEffect summonWave = new RoomWaveClearEffect { waveColor = new Color(1.6f, 0.75f, 0.2f, 1f) };
    [Tooltip("방을 찾지 못할 때(방이 없는 테스트 씬) 보스를 중심으로 쓸 사각형의 반 폭(m).")]
    [Min(0.5f)] public float summonFallbackHalfSize = 5f;
    [Tooltip("소환 물결을 퍼뜨린 순간부터 보스가 사격을 다시 시작하기까지(초). 그동안 이동(접근/후퇴)은 그대로 하고, 연사와 백스텝 샷건은 하지 않습니다.")]
    [Min(0f)] public float summonAttackDelay = 1.5f;
    [Tooltip("소환된 적이 흰 빛에서 원래 색으로 서서히 나타나는 시간(초). 그동안은 움직이지 않고, 공격도 접촉 피해도 없습니다(피격은 받음).")]
    [Min(0f)] public float summonMaterializeDuration = 1.5f;

    [Header("2레벨 보스 - 포격 발동 모션 (보호막이 생기는 순간)")]
    [Tooltip("모델 전면을 하늘로 드는(그리고 끝나고 내리는) 시간(초).")]
    [Min(0.01f)] public float launchRaiseTime = 0.2f;
    [Tooltip("전면을 하늘로 드는 각도(도). 90 = 정확히 하늘.")]
    [Range(0f, 90f)] public float launchPitchAngle = 90f;
    [Tooltip("하늘로 수류탄을 쏘아 올리는 시간(초) - 이 동안 Launch Shell Count발을 고르게 나눠 쏘고, 모델이 돎.")]
    [Min(0f)] public float launchFireDuration = 0.5f;
    [Tooltip("쏘아 올리는 수류탄 수(연출만, 피해 없음).")]
    [Min(0)] public int launchShellCount = 6;
    [Tooltip("쏘는 동안 모델이 도는 속도(도/초).")]
    public float launchSpinSpeed = 720f;
    [Tooltip("쏘아 올린 수류탄 속도(m/초).")]
    [Min(0.1f)] public float launchShellSpeed = 18f;
    [Tooltip("쏘아 올리는 방향이 수직에서 벗어나는 최대 각도(도, 무작위).")]
    [Range(0f, 45f)] public float launchShellSpread = 12f;
    [Tooltip("쏜 높이보다 이만큼 올라가면 수류탄을 지움(m).")]
    [Min(0.5f)] public float launchShellDespawnHeight = 12f;
    [Tooltip("쏘아 올리는 수류탄 크기 = 수류탄 모델 크기 x 이 값.")]
    [Min(0.01f)] public float launchShellScale = 1f;

    [Header("2레벨 보스 - 포격 (보호막이 있는 동안 반복)")]
    [Tooltip("포격 모델. 비워두면 플레이어 수류탄 모델(PlayerGrenadeThrower의 Grenade Prefab).")]
    public GameObject artilleryShellPrefab;
    [Tooltip("포격 반경(m).")]
    [Min(0.1f)] public float artilleryRadius = 1.5f;
    [Tooltip("포격 피해.")]
    [Min(0f)] public float artilleryDamage = 2f;
    [Tooltip("포격이 터진 뒤 다음 조준 표시가 뜨기까지(초).")]
    [Min(0f)] public float artilleryInterval = 1f;
    [Tooltip("이 횟수마다 한 번은 그 순간 플레이어 위치를 노림(3 = 3, 6, 9번째). 플레이어가 투명(112번)이면 무작위. 0이면 항상 무작위.")]
    [Min(0)] public int artilleryAimEvery = 3;
    [Tooltip("무작위 위치는 방 벽에서 (반경 + 이 값)만큼 안쪽에서 고름(m).")]
    [Min(0f)] public float artilleryWallMargin = 0.3f;
    [Tooltip("조준 표시/레이저/포탄/폭발 설정(기본 주황, 조준 1초 - 그중 마지막 0.5초에 포탄이 떨어짐).")]
    public BossArtilleryStrike.Settings artillery = new BossArtilleryStrike.Settings();

    [Header("2레벨 보스 - 벽 돌진 (포격 발동 모션 뒤, 보호막이 있는 동안 반복)")]
    [Tooltip("돌진 반복을 처음 시작할 때(그리고 놓쳤던 목표를 다시 찾아 재개할 때) 조준 시간(초).")]
    [Min(0f)] public float wallDashFirstAimTime = 1f;
    [Tooltip("돌진이 벽에 닿은 뒤 다음 돌진까지 조준 시간(초).")]
    [Min(0f)] public float wallDashAimTime = 0.3f;
    [Tooltip("돌진 속도(m/초) - 1레벨 보스와 같은 5.")]
    [Min(0.1f)] public float wallDashSpeed = 5f;
    [Tooltip("돌진 중 접촉 피해 - 1레벨 보스와 같은 2. 평소 접촉 피해는 EnemyStats의 Attack Power.")]
    [Min(0f)] public float wallDashDamage = 2f;
    [Tooltip("조준하는 동안 돌진 방향으로 도는 속도(1레벨 보스 연속 돌진과 같은 12).")]
    public float wallDashTurnSpeed = 12f;
    [Tooltip("벽을 못 만나도 돌진 하나가 이 시간(초)을 넘으면 끝냄(안전장치).")]
    [Min(0.5f)] public float wallDashMaxDuration = 4f;
    [Tooltip("돌진 조준/돌진 중 몸 색(1레벨 보스 연속 돌진과 같은 값). 첫 조준 동안 원래 색에서 서서히 물들고, 반복이 끝나면 바로 원래 색.")]
    public Color wallDashTelegraphColor = new Color(1f, 0.549f, 0f, 1f);
    [Tooltip("돌진 조준/돌진 중 발광 세기(색 x 이 값 - 1레벨 보스와 같은 2).")]
    [Min(0f)] public float wallDashEmissionBoost = 2f;
    [Tooltip("돌진 잔상(74번과 같은 방식).")]
    public DashAfterimageSettings wallDashAfterimage = new DashAfterimageSettings { color = new Color(1f, 0.55f, 0.15f, 0.45f) };
    [Tooltip("돌진 반복을 시작할 때(첫 조준) 재생할 충전 이펙트 - 보호막 충전 이펙트와 따로. 비워두면 이펙트 없음.")]
    public DashChargeVfxSettings wallDashChargeVfx = new DashChargeVfxSettings();
    private GameObject wallDashChargeVfxInstance;

    private float nextShotgunTime;

    private enum WallDashPhase { Idle, Aiming, Dashing }
    private bool wallDashArmed;       // 보호막이 있는 동안 돌진 반복을 해야 함(발동 모션이 끝나면 켜짐)
    private WallDashPhase wallDashPhase = WallDashPhase.Idle;
    private float wallDashTimer;
    private Vector3 wallDashDirection;
    private Vector3 wallDashAimPoint;
    private float wallDashElapsed;
    private float wallDashStuckTimer;
    private float wallDashNextAfterimage;

    // IDashDamageSource - 접촉 트리거(MeleeHitbox)가 돌진 중 여부/피해를 읽습니다.
    public bool IsDashing => wallDashPhase == WallDashPhase.Dashing;
    public float DashDamage => wallDashDamage;

    private enum LaunchState { None, Raising, Firing, Lowering }
    private LaunchState launchState = LaunchState.None;
    private float launchTimer;
    private int launchShellsFired;
    private float launchWeight;      // 0~1 전면을 하늘로 든 정도
    private float launchSpin;        // 도는 각도(도)
    private float launchSpinAtLower; // 내리기 시작할 때의 각도(내리면서 0으로 돌아감)

    private bool artilleryRunning;
    private bool artilleryStrikePending;
    private BossArtilleryStrike currentStrike;
    private float nextStrikeTime;
    private int strikeCount;

    private struct PendingSummon
    {
        public EnemyDefinition definition;
        public Vector3 position;
        public float spawnTime;
    }
    private readonly List<PendingSummon> pendingSummons = new List<PendingSummon>();
    private RealEnemySpawner summonSpawner;
    private LevelBuilder levelBuilder;
    private bool warnedNoSpawner;
    private float attackResumeTime; // 소환 물결 뒤 사격 재개 시각(그 전엔 이동만)

    private BossShield shield;
    private bool firstShieldDone; // 방에 처음 들어왔을 때의 보호막 충전을 마쳤는지
    private float rechargeAt = float.MaxValue;

    private bool groggy;
    private float groggyEndTime;
    private float tiltWeight;     // 0~1 (그로기가 끝난 뒤에도 0이 될 때까지 서서히 빠짐)
    private float wobblePhase;
    private Quaternion tiltBaseRotation;
    private bool tiltBaseSaved;

    private EnemyHealth health;
    private float colorWeight;    // 0~1 그로기 색 섞임(시작/끝은 Groggy Blend Time에 걸쳐 서서히)
    private float dashColorWeight; // 0~1 벽 돌진 색 섞임(첫 조준 동안 서서히, 반복이 끝나면 바로 0)

    // IEnemyColorOverride - EnemyHealth 피격 깜빡임이 돌아갈 색, EnemyControlState는 이 동안 칠하지 않고 양보합니다.
    // 그로기(보호막 없음)와 벽 돌진(보호막 있음)은 동시에 일어나지 않지만, 넘어가는 순간을 위해 원래 색 -> 그로기 색 -> 돌진 색 순서로 섞습니다.
    public bool HasColorOverride => (colorWeight > 0f || dashColorWeight > 0f) && health != null && health.RendSupportsColor;
    public Color OverrideColor
    {
        get
        {
            Color baseColor = health != null ? health.OriginalColor : groggyColor;
            return Color.Lerp(Color.Lerp(baseColor, groggyColor, colorWeight), wallDashTelegraphColor, dashColorWeight);
        }
    }
    public Color OverrideEmission
    {
        get
        {
            Color baseEmission = health != null ? health.OriginalEmission : groggyColor * groggyEmissionBoost;
            Color e = Color.Lerp(baseEmission, groggyColor * groggyEmissionBoost, colorWeight);
            return Color.Lerp(e, wallDashTelegraphColor * wallDashEmissionBoost, dashColorWeight);
        }
    }

    private bool backstepping;
    private Vector3 backstepVelocity;
    private float backstepRemaining;
    private float backstepElapsed;
    private float nextAfterimageAt;

    // 1레벨 보스의 전방위/연속 돌진 컴포넌트는 쓰지 않으므로 연사만 준비합니다(없다는 경고도 띄우지 않음).
    protected override void Awake()
    {
        CacheCoreComponents();
        // 다른 적처럼 위치는 NavMeshAgent만 움직이게 물리 영향(중력/충돌 밀림/회전)을 끕니다. 1레벨 보스는 연속 돌진 부품이 이 일을
        // 했는데 2레벨 보스에는 그 부품이 없어, 꺼 두지 않으면 판정 위치가 어긋나 수류탄 범위 검사/처치 폭발 위치가 빗나갑니다.
        var body = GetComponent<Rigidbody>();
        if (body != null)
        {
            body.isKinematic = true;
            body.useGravity = false;
        }
        health = GetComponent<EnemyHealth>();
        shield = GetComponent<BossShield>();
        if (shield != null)
        {
            shield.OnShieldGained += () => firstShieldDone = true;
            shield.OnShieldGained += BeginLaunch;
            shield.OnShieldBroken += HandleShieldBroken;
        }
        if (groggyTiltTarget == null) groggyTiltTarget = FindModelChild();

        // 접촉 트리거(MeleeHitbox)에 능력치와 돌진 피해원(이 컨트롤러)을 연결합니다(1레벨 보스는 연속 돌진 부품이 하던 일).
        var contact = GetComponentInChildren<EnemyMeleeContactTrigger>(true);
        if (contact != null) contact.Initialize(stats, this);

        if (rangedAttack == null)
        {
            Debug.LogWarning($"BossLv2AIController ({gameObject.name}): EnemyRangedAttack이 없어 사격을 하지 않습니다.");
        }
        else
        {
            rangedAttack.Initialize(stats);
        }

        TryFindPlayer();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        nextShotgunTime = 0f;
        backstepping = false;
        pendingSummons.Clear();
        attackResumeTime = 0f;
        launchState = LaunchState.None;
        artilleryRunning = false;
        artilleryStrikePending = false;
        currentStrike = null;
        wallDashArmed = false;
        wallDashPhase = WallDashPhase.Idle;
    }

    void OnDisable()
    {
        pendingSummons.Clear();
        launchState = LaunchState.None;
        artilleryRunning = false;
        artilleryStrikePending = false;
        wallDashArmed = false;
        wallDashPhase = WallDashPhase.Idle;
        tiltWeight = 0f;
        launchWeight = 0f;
        launchSpin = 0f;
        ApplyTilt();
        if (colorWeight > 0f || dashColorWeight > 0f)
        {
            colorWeight = 0f;
            dashColorWeight = 0f;
            PaintGroggyColor();
        }
    }

    protected override void Update()
    {
        UpdateTilt();
        UpdateGroggyColor();
        UpdatePendingSummons();
        UpdateArtillery();

        // 그로기 중에는 제어/목표와 무관하게 멈춰서 어지러워합니다. 끝나는 순간 소환 물결.
        if (groggy)
        {
            agent.isStopped = true;
            if (Time.time < groggyEndTime) return;
            groggy = false;
            BeginSummon();
        }

        // 보호막 충전: 방에 처음 들어왔을 때 + 깨진 뒤 Recharge Delay가 지났을 때(충전 중 무적/제어 면역 - BossShield).
        bool wantsCharge = !firstShieldDone || Time.time >= rechargeAt;
        if (shield != null && wantsCharge && !shield.IsCharging && !shield.IsActive && shield.BeginCharge())
        {
            rechargeAt = float.MaxValue;
            backstepping = false;
            if (rangedAttack != null) rangedAttack.CancelBurst();
        }

        if (shield != null && shield.IsCharging)
        {
            UpdateCharging();
            return;
        }

        // 포격 발동 모션 중에는 제자리에서 모션만(보호막이 있어 제어 면역).
        if (launchState != LaunchState.None)
        {
            UpdateLaunch();
            return;
        }

        // 강력 제어(수면/해킹) 중에는 판단을 전부 멈춥니다(1레벨 보스와 같음). 보호막이 있는 동안은 해킹이 표시만 걸리므로 멈추지 않습니다.
        // 백스텝 중이었으면 샷건 없이 끝냅니다.
        bool controlImmune = shield != null && shield.IsControlImmune;
        if (!controlImmune && EnemyControlState.IsStrongControlledFor(gameObject))
        {
            if (backstepping)
            {
                backstepping = false;
                nextShotgunTime = Time.time; // 막아 뒀던 간격을 풀어 제어가 끝나면 다시 시도할 수 있게
            }
            agent.isStopped = true;
            return;
        }

        if (player == null)
        {
            TryFindPlayer();
            if (player == null) return;
        }

        // [112번 / 87번] 노릴 대상이 없으면(투명한 플레이어뿐) 멈춥니다. 이미 시작한 연사는 EnemyRangedAttack이 끝까지 쏩니다.
        target = CombatTargeting.Resolve(gameObject, false, player, out _);

        // 벽 돌진 반복(보호막이 있는 동안) - 평소 사격/샷건 대신.
        if (wallDashArmed)
        {
            if (shield != null && shield.IsActive && (health == null || !health.IsDead))
            {
                UpdateWallDash();
                return;
            }
            StopWallDash();
        }

        if (backstepping)
        {
            UpdateBackstep();
            return;
        }

        if (target == null)
        {
            agent.isStopped = true;
            return;
        }

        UpdateNormalPhase();
    }

    // ---------- 그로기 뒤 소환 ----------

    // 보스 자리에서 주황 물결을 퍼뜨리고, 소환할 적마다 모서리 자리와 "물결 앞이 그 자리에 닿는 시각"을 정해 둡니다(실제 생성은 UpdatePendingSummons).
    private void BeginSummon()
    {
        if (summonDefinitions == null || summonDefinitions.Count == 0) return;
        if (health != null && health.IsDead) return;

        Vector3 center = transform.position;
        if (!TryGetRoomRect(out Rect rect))
        {
            float h = Mathf.Max(summonFallbackHalfSize, 0.5f);
            rect = new Rect(center.x - h, center.z - h, h * 2f, h * 2f);
        }

        float dx = Mathf.Max(Mathf.Abs(center.x - rect.xMin), Mathf.Abs(center.x - rect.xMax));
        float dz = Mathf.Max(Mathf.Abs(center.z - rect.yMin), Mathf.Abs(center.z - rect.yMax));
        float maxRadius = Mathf.Max(Mathf.Sqrt(dx * dx + dz * dz), 0.1f);

        // 물결을 퍼뜨린 뒤 잠시 사격하지 않습니다(플레이어가 소환된 적에 대응할 틈).
        attackResumeTime = Time.time + summonAttackDelay;
        if (rangedAttack != null) rangedAttack.CancelBurst();

        // 물결은 연출이라 오류가 나도 소환은 그대로 진행합니다.
        if (summonWave != null)
        {
            try
            {
                RoomWavePulse.SpawnVisualOnly(summonWave, center, maxRadius, rect);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"BossLv2AIController: 소환 물결 생성 중 오류가 발생해 물결 없이 소환합니다: {e}");
            }
        }

        float inset = Mathf.Min(summonCornerInset, rect.width * 0.45f, rect.height * 0.45f);
        Vector2[] corners =
        {
            new Vector2(rect.xMax - inset, rect.yMax - inset),
            new Vector2(rect.xMin + inset, rect.yMax - inset),
            new Vector2(rect.xMin + inset, rect.yMin + inset),
            new Vector2(rect.xMax - inset, rect.yMin + inset),
        };
        float expand = Mathf.Max(summonWave != null ? summonWave.expandDuration : 0.6f, 0.05f);

        for (int i = 0; i < summonDefinitions.Count; i++)
        {
            EnemyDefinition definition = summonDefinitions[i];
            if (definition == null) continue;

            Vector2 c = corners[i % corners.Length];
            Vector3 corner = new Vector3(c.x, center.y, c.y);
            if (!TryFindSummonPosition(corner, center, out Vector3 position))
            {
                Debug.LogWarning($"BossLv2AIController: 모서리 근처에서 소환할 자리를 찾지 못해 '{definition.variantId}' 소환을 건너뜁니다.");
                continue;
            }

            // 물결 반경 = 최대 반경 x (1 - (1 - t)^2) (RoomWavePulse와 같은 퍼짐) -> 그 자리에 닿는 진행도 t.
            float distance = new Vector2(position.x - center.x, position.z - center.z).magnitude;
            float reach01 = Mathf.Clamp01(distance / maxRadius);
            float t = 1f - Mathf.Sqrt(1f - reach01);

            pendingSummons.Add(new PendingSummon { definition = definition, position = position, spawnTime = Time.time + t * expand });
        }
    }

    // 정해 둔 시각이 된 소환을 실제로 만듭니다. 보스가 죽었으면(방 클리어 판정이 끝났을 수 있음) 남은 소환은 취소합니다.
    private void UpdatePendingSummons()
    {
        if (pendingSummons.Count == 0) return;
        if (health != null && health.IsDead)
        {
            pendingSummons.Clear();
            return;
        }

        for (int i = pendingSummons.Count - 1; i >= 0; i--)
        {
            if (Time.time < pendingSummons[i].spawnTime) continue;
            PendingSummon summon = pendingSummons[i];
            pendingSummons.RemoveAt(i);
            SpawnSummon(summon);
        }
    }

    // 일반 적 스폰과 같은 방식으로 만들고(RealEnemySpawner), 보스를 세고 있는 방 클리어 조건에 더합니다 - 소환된 적까지 죽어야 클리어.
    // 보스와 같은 부모(방 콘텐츠) 밑에 두므로, 클리어 전에 방을 벗어났다 돌아오면 방 초기화 때 함께 지워집니다.
    private void SpawnSummon(PendingSummon summon)
    {
        if (summonSpawner == null) summonSpawner = FindFirstObjectByType<RealEnemySpawner>();
        if (summonSpawner == null)
        {
            if (!warnedNoSpawner)
            {
                warnedNoSpawner = true;
                Debug.LogWarning("BossLv2AIController: 씬에 RealEnemySpawner가 없어 소환하지 못했습니다.");
            }
            return;
        }

        try
        {
            EnemyHealth spawned = summonSpawner.SpawnSummonedEnemy(summon.definition, summon.position, transform.parent);
            // 흰 빛에서 서서히 나타나는 동안은 움직이지도, 공격하지도, 접촉 피해를 주지도 않습니다(SummonMaterialize).
            if (spawned != null && summonMaterializeDuration > 0f) SummonMaterialize.Begin(spawned.gameObject, summonMaterializeDuration);
            if (spawned != null && health != null && !EnemyKillObjective.TryAddAlongside(health, spawned))
            {
                Debug.LogWarning($"BossLv2AIController: 소환한 '{summon.definition.variantId}'를 방 클리어 조건에 더하지 못했습니다(보스를 세는 방이 없음 - 방이 없는 테스트 씬이면 정상).");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"BossLv2AIController: '{summon.definition.variantId}' 소환 중 오류가 발생해 건너뜁니다: {e}");
        }
    }

    // 모서리에서 시작해 보스 쪽으로 조금씩 옮겨 가며, NavMesh 위이고 보스가 걸어서 갈 수 있는(=방 바닥과 이어진) 자리를 찾습니다.
    private bool TryFindSummonPosition(Vector3 corner, Vector3 center, out Vector3 result)
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

    // 보스가 들어 있는 방(방 콘텐츠의 자식)이 차지한 칸들을 덮는 바닥 사각형(x/z). 칸 중심 = 칸 좌표 x cellSize(LevelBuilder.CellToWorld, 90번과 같은 규칙).
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

    // ---------- 보호막 파괴 -> 그로기 ----------
    private void HandleShieldBroken()
    {
        groggy = true;
        groggyEndTime = Time.time + groggyDuration;
        rechargeAt = Time.time + rechargeDelay;

        if (backstepping)
        {
            backstepping = false;
            nextShotgunTime = Time.time;
        }
        if (rangedAttack != null) rangedAttack.CancelBurst();
        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }

        // 발동 모션 도중 깨졌으면 모델을 내리고(그로기 기울기와 섞여 서서히), 포격 반복은 멈춤(이미 뜬 포격은 그대로 터짐).
        if (launchState == LaunchState.Raising || launchState == LaunchState.Firing) StartLowering();
        artilleryRunning = false;
        StopWallDash();
    }

    // ---------- 벽 돌진 반복 ----------

    private void UpdateWallDash()
    {
        switch (wallDashPhase)
        {
            case WallDashPhase.Idle:
                agent.isStopped = true;
                if (target == null) return; // 목표를 놓친 동안(112번 투명 등)은 제자리에서 기다림 - 다시 보이면 처음부터
                StartWallDashSeries();
                break;

            case WallDashPhase.Aiming:
                agent.isStopped = true;
                if (target == null)
                {
                    DashChargeVfx.Stop(ref wallDashChargeVfxInstance);
                    wallDashPhase = WallDashPhase.Idle;
                    return;
                }
                wallDashAimPoint = ComputeWallDashAimPoint();
                RotateTowards(wallDashAimPoint, wallDashTurnSpeed);
                wallDashTimer -= Time.deltaTime; // [90번] 보스는 물결의 공격 금지를 받지 않음
                if (wallDashTimer <= 0f) StartWallDash();
                break;

            case WallDashPhase.Dashing:
                UpdateWallDashMove();
                break;
        }
    }

    // 반복을 처음부터: 임플로전 이펙트 + 첫 조준.
    private void StartWallDashSeries()
    {
        DashChargeVfx.Stop(ref wallDashChargeVfxInstance);
        wallDashChargeVfxInstance = DashChargeVfx.Play(wallDashChargeVfx, transform);
        if (rangedAttack != null) rangedAttack.CancelBurst();
        backstepping = false;
        if (agent.isActiveAndEnabled && agent.isOnNavMesh) agent.ResetPath();

        wallDashPhase = WallDashPhase.Aiming;
        wallDashTimer = wallDashFirstAimTime;
    }

    // 1레벨 보스 연속 돌진과 같은 조준: 목표까지 NavMesh 경로의 첫 꺾이는 지점(벽이 가로막으면 돌아가는 방향), 못 구하면 목표 직선.
    private Vector3 ComputeWallDashAimPoint()
    {
        if (target == null) return transform.position + transform.forward;
        if (agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            var path = new NavMeshPath();
            if (agent.CalculatePath(target.position, path) && path.corners.Length >= 2 &&
                (path.status == NavMeshPathStatus.PathComplete || path.status == NavMeshPathStatus.PathPartial))
            {
                return path.corners[1];
            }
        }
        return target.position;
    }

    private void RotateTowards(Vector3 point, float speed)
    {
        Vector3 look = point - transform.position;
        look.y = 0f;
        if (look.sqrMagnitude < 0.0001f) return;
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(look), speed * Time.deltaTime);
    }

    private void StartWallDash()
    {
        Vector3 dir = wallDashAimPoint - transform.position;
        dir.y = 0f;
        wallDashDirection = dir.sqrMagnitude > 0.0001f ? dir.normalized : transform.forward;
        transform.rotation = Quaternion.LookRotation(wallDashDirection);

        wallDashPhase = WallDashPhase.Dashing;
        wallDashElapsed = 0f;
        wallDashStuckTimer = 0f;
        SpawnWallDashAfterimage();
        wallDashNextAfterimage = Mathf.Max(wallDashAfterimage != null ? wallDashAfterimage.spawnInterval : 0.04f, 0.01f);
    }

    // 벽/장애물(NavMesh 경계)에 닿을 때까지 직진. 닿으면 그 자리까지 가서 끝내고 다음 조준으로. 끼여서 못 나가도 끝냄.
    private void UpdateWallDashMove()
    {
        float dt = Time.deltaTime;
        float step = wallDashSpeed * dt;
        Vector3 from = transform.position;
        bool hitWall = false;

        if (step > 0f)
        {
            Vector3 to = from + wallDashDirection * step;
            if (NavMesh.Raycast(from, to, out NavMeshHit hit, NavMesh.AllAreas))
            {
                Vector3 move = hit.position - from;
                move.y = 0f;
                if (move.sqrMagnitude > 0f) agent.Move(move);
                hitWall = true;
            }
            else
            {
                agent.Move(wallDashDirection * step);
            }
        }

        transform.rotation = Quaternion.LookRotation(wallDashDirection);
        wallDashElapsed += dt;

        if (!hitWall && step > 0f)
        {
            float moved = new Vector2(transform.position.x - from.x, transform.position.z - from.z).magnitude;
            if (moved < step * 0.3f) wallDashStuckTimer += dt;
            else wallDashStuckTimer = 0f;
        }

        if (!hitWall && wallDashElapsed >= wallDashNextAfterimage)
        {
            SpawnWallDashAfterimage();
            wallDashNextAfterimage = wallDashElapsed + Mathf.Max(wallDashAfterimage != null ? wallDashAfterimage.spawnInterval : 0.04f, 0.01f);
        }

        if (hitWall || wallDashStuckTimer >= 0.15f || wallDashElapsed >= wallDashMaxDuration) EndWallDash();
    }

    // 돌진 하나가 끝남: 목표가 보이면 짧은 조준 뒤 다음 돌진, 놓쳤으면 멈춰 기다렸다가 다시 보이면 처음부터.
    private void EndWallDash()
    {
        if (target == null)
        {
            wallDashPhase = WallDashPhase.Idle;
            return;
        }
        wallDashPhase = WallDashPhase.Aiming;
        wallDashTimer = wallDashAimTime;
    }

    private void StopWallDash()
    {
        if (wallDashPhase == WallDashPhase.Aiming) DashChargeVfx.Stop(ref wallDashChargeVfxInstance);
        wallDashArmed = false;
        wallDashPhase = WallDashPhase.Idle;
    }

    // ICancellableAttack - 진행 중인 공격을 끊는 효과가 오면 조준/돌진을 멈추고 처음부터 다시(보호막이 남아 있으면). 90번 물결은 보스를 끊지 않음.
    public void CancelAttack()
    {
        if (wallDashPhase == WallDashPhase.Aiming) DashChargeVfx.Stop(ref wallDashChargeVfxInstance);
        if (wallDashPhase == WallDashPhase.Aiming || wallDashPhase == WallDashPhase.Dashing) wallDashPhase = WallDashPhase.Idle;
    }

    private void SpawnWallDashAfterimage()
    {
        if (wallDashAfterimage == null || !wallDashAfterimage.show) return;
        try
        {
            DashAfterimage.Spawn(gameObject, wallDashAfterimage);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"BossLv2AIController: 돌진 잔상 생성 중 오류가 발생해 건너뜁니다: {e}");
        }
    }

    // ---------- 포격 발동 모션 ----------

    // 보호막이 생기는 순간(첫 충전/재충전 모두): 멈춰서 전면을 하늘로 들기 시작합니다.
    private void BeginLaunch()
    {
        if (health != null && health.IsDead) return;
        launchState = LaunchState.Raising;
        launchTimer = 0f;
        launchShellsFired = 0;
        artilleryRunning = false;
        backstepping = false;
        if (rangedAttack != null) rangedAttack.CancelBurst();
        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }
    }

    private void UpdateLaunch()
    {
        agent.isStopped = true;

        switch (launchState)
        {
            case LaunchState.Raising:
                if (launchWeight >= 1f)
                {
                    launchState = LaunchState.Firing;
                    launchTimer = 0f;
                }
                break;

            case LaunchState.Firing:
                launchTimer += Time.deltaTime;
                launchSpin += launchSpinSpeed * Time.deltaTime;

                int count = Mathf.Max(launchShellCount, 0);
                float duration = Mathf.Max(launchFireDuration, 0f);
                int due = count;
                if (count > 1 && duration > 0f) due = Mathf.Min(count, 1 + Mathf.FloorToInt(launchTimer / (duration / (count - 1))));
                while (launchShellsFired < due)
                {
                    SpawnLaunchShell();
                    launchShellsFired++;
                }

                if (launchTimer >= duration && launchShellsFired >= count)
                {
                    StartLowering();
                    StartArtillery();
                }
                break;

            case LaunchState.Lowering:
                if (launchWeight <= 0f)
                {
                    launchState = LaunchState.None;
                    // 모션이 끝났고 보호막이 남아 있으면 벽 돌진 반복 시작(다음 Update에서 임플로전 + 첫 조준).
                    if (shield != null && shield.IsActive)
                    {
                        wallDashArmed = true;
                        wallDashPhase = WallDashPhase.Idle;
                    }
                }
                break;
        }
    }

    private void StartLowering()
    {
        launchState = LaunchState.Lowering;
        launchSpinAtLower = Mathf.Repeat(launchSpin, 360f);
    }

    // 모델 꼭대기(지금 자세 기준 - 하늘을 향하면 앞쪽 끝)에서 거의 수직으로 쏘아 올립니다. 연출이라 오류가 나도 모션은 계속.
    private void SpawnLaunchShell()
    {
        try
        {
            Vector3 origin = transform.position + Vector3.up;
            Transform model = groggyTiltTarget != null ? groggyTiltTarget : transform;
            bool any = false;
            Bounds b = default;
            foreach (var r in model.GetComponentsInChildren<Renderer>())
            {
                if (r == null || !r.enabled) continue;
                if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;
                if (any) b.Encapsulate(r.bounds);
                else { b = r.bounds; any = true; }
            }
            if (any) origin = b.center + Vector3.up * b.extents.y;

            Vector3 dir = Quaternion.AngleAxis(Random.Range(0f, 360f), Vector3.up)
                        * Quaternion.AngleAxis(Random.Range(0f, launchShellSpread), Vector3.right) * Vector3.up;
            BossArtilleryLaunchShell.Launch(artilleryShellPrefab, launchShellScale, origin, dir, launchShellSpeed, launchShellDespawnHeight);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"BossLv2AIController: 발사 연출 수류탄 생성 중 오류가 발생해 건너뜁니다: {e}");
        }
    }

    // ---------- 포격 반복 (보호막이 있는 동안) ----------

    private void StartArtillery()
    {
        artilleryRunning = true;
        artilleryStrikePending = false;
        currentStrike = null;
        strikeCount = 0;
        nextStrikeTime = Time.time; // 모션이 끝나는 즉시 첫 조준
    }

    // 한 번에 하나: 조준 표시 -> 폭발(BossArtilleryStrike가 스스로 지워짐) -> Artillery Interval 뒤 다음 조준. 보호막이 없어지면 새로 띄우지 않음.
    private void UpdateArtillery()
    {
        if (artilleryStrikePending)
        {
            if (currentStrike != null) return;
            artilleryStrikePending = false;
            nextStrikeTime = Time.time + artilleryInterval;
        }

        if (!artilleryRunning) return;
        if (health != null && health.IsDead) { artilleryRunning = false; return; }
        if (shield == null || !shield.IsActive) { artilleryRunning = false; return; }
        if (Time.time < nextStrikeTime) return;

        strikeCount++;
        bool aimAtPlayer = artilleryAimEvery > 0 && strikeCount % artilleryAimEvery == 0 && !PlayerInvisibility.IsActive;
        if (!TryPickStrikePoint(aimAtPlayer, out Vector3 point))
        {
            nextStrikeTime = Time.time + 0.2f; // 자리를 못 찾았으면 잠시 뒤 다시
            return;
        }

        try
        {
            currentStrike = BossArtilleryStrike.Create(artillery, health, point, artilleryRadius, artilleryDamage, artilleryShellPrefab);
            artilleryStrikePending = currentStrike != null;
            if (!artilleryStrikePending) nextStrikeTime = Time.time + artilleryInterval;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"BossLv2AIController: 포격 생성 중 오류가 발생해 건너뜁니다: {e}");
            nextStrikeTime = Time.time + artilleryInterval;
        }
    }

    // 플레이어 조준이면 지금 플레이어 발밑, 아니면 방 안 무작위(벽에서 반경 + 여유만큼 안쪽) - 둘 다 NavMesh 바닥 높이에 맞춤.
    private bool TryPickStrikePoint(bool aimAtPlayer, out Vector3 point)
    {
        if (aimAtPlayer)
        {
            if (player == null) TryFindPlayer();
            if (player != null)
            {
                point = player.position;
                if (NavMesh.SamplePosition(point, out NavMeshHit hitP, 2f, NavMesh.AllAreas)) point = hitP.position;
                return true;
            }
        }

        Vector3 center = transform.position;
        if (!TryGetRoomRect(out Rect rect))
        {
            float h = Mathf.Max(summonFallbackHalfSize, 0.5f);
            rect = new Rect(center.x - h, center.z - h, h * 2f, h * 2f);
        }
        float inset = Mathf.Min(artilleryRadius + artilleryWallMargin, rect.width * 0.45f, rect.height * 0.45f);

        for (int i = 0; i < 12; i++)
        {
            Vector3 candidate = new Vector3(Random.Range(rect.xMin + inset, rect.xMax - inset), center.y,
                                            Random.Range(rect.yMin + inset, rect.yMax - inset));
            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 1f, NavMesh.AllAreas))
            {
                point = hit.position;
                return true;
            }
        }

        point = Vector3.zero;
        return false;
    }

    // 그로기 기울기: 모델을 일정 각도로 기울인 채 기울어진 방향(수평 축)만 빙글 돌림 - 보스 자체(루트)는 돌지 않음. 시작/끝은 서서히.
    private void UpdateTilt()
    {
        // 모델이 없어도 비율은 진행합니다(포격 발동 모션이 비율로 다음 단계로 넘어가므로). 실제로 돌리는 건 ApplyTilt(모델이 있을 때만).
        float target01 = groggy ? 1f : 0f;
        bool launching = launchState == LaunchState.Raising || launchState == LaunchState.Firing;
        float launchTarget = launching ? 1f : 0f;
        if (tiltWeight == target01 && tiltWeight == 0f && launchWeight == launchTarget && launchWeight == 0f) return;

        tiltWeight = Mathf.MoveTowards(tiltWeight, target01, Time.deltaTime / Mathf.Max(groggyBlendTime, 0.01f));
        if (tiltWeight > 0f) wobblePhase += groggyWobbleSpeed * 360f * Time.deltaTime;

        // [포격 발동 모션] 전면을 하늘로 들고(Raising), 쏘는 동안 돌다가(Firing), 내리면서 도는 각도도 0으로 돌아옴.
        launchWeight = Mathf.MoveTowards(launchWeight, launchTarget, Time.deltaTime / Mathf.Max(launchRaiseTime, 0.01f));
        if (!launching) launchSpin = launchWeight > 0f ? Mathf.LerpAngle(launchSpinAtLower, 0f, 1f - launchWeight) : 0f;
        ApplyTilt();
    }

    private void ApplyTilt()
    {
        if (groggyTiltTarget == null) return;
        bool posed = tiltWeight > 0f || launchWeight > 0f;
        if (!tiltBaseSaved)
        {
            if (!posed) return;
            tiltBaseRotation = groggyTiltTarget.localRotation;
            tiltBaseSaved = true;
        }

        // 축은 부모(보스 루트) 기준 수평 방향 - 이 축으로 기울이면 기울어진 쪽이 wobblePhase를 따라 돎.
        Vector3 axis = Quaternion.Euler(0f, wobblePhase, 0f) * Vector3.right;
        // [포격 발동 모션] 루트의 오른쪽 축으로 -각도(앞 -> 위) 들고, 루트의 위쪽 축으로 돎.
        Quaternion launchPose = Quaternion.AngleAxis(launchSpin, Vector3.up) * Quaternion.AngleAxis(-launchPitchAngle * launchWeight, Vector3.right);
        groggyTiltTarget.localRotation = Quaternion.AngleAxis(groggyTiltAngle * tiltWeight, axis) * launchPose * tiltBaseRotation;

        if (!posed)
        {
            groggyTiltTarget.localRotation = tiltBaseRotation;
            tiltBaseSaved = false;
        }
    }

    // 그로기 색/벽 돌진 색은 섞임 비율이 바뀌는 동안만 직접 칠하고, 그 사이에는 피격 깜빡임이 OverrideColor로 돌아오게 둡니다.
    private void UpdateGroggyColor()
    {
        if (health == null || health.IsDead) return;
        float target01 = groggy ? 1f : 0f;
        bool dashing = wallDashArmed && (wallDashPhase == WallDashPhase.Aiming || wallDashPhase == WallDashPhase.Dashing);
        float dashTarget = dashing ? 1f : 0f;
        if (colorWeight == target01 && dashColorWeight == dashTarget) return;

        colorWeight = Mathf.MoveTowards(colorWeight, target01, Time.deltaTime / Mathf.Max(groggyBlendTime, 0.01f));
        // 1레벨 보스 연속 돌진처럼: 첫 조준 동안 서서히 물들고(이후 조준/돌진은 그대로 유지), 반복이 끝나면 바로 원래 색.
        dashColorWeight = dashing ? Mathf.MoveTowards(dashColorWeight, 1f, Time.deltaTime / Mathf.Max(wallDashFirstAimTime, 0.01f)) : 0f;
        PaintGroggyColor();
    }

    private void PaintGroggyColor()
    {
        if (health == null || !health.RendSupportsColor) return;
        Renderer r = health.TargetRenderer;
        if (r == null) return;

        Color c = OverrideColor;
        if (health.UsesBaseColor) r.material.SetColor("_BaseColor", c);
        if (health.UsesColor) r.material.SetColor("_Color", c);
        if (health.UsesEmission) r.material.SetColor("_EmissionColor", OverrideEmission);
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

    // ---------- 보호막 충전: 제자리에서 목표만 바라봄 ----------
    private void UpdateCharging()
    {
        agent.isStopped = true;
        if (player == null) TryFindPlayer();
        if (player == null) return;
        target = CombatTargeting.Resolve(gameObject, false, player, out _);
        if (target != null) UpdateFacing();
    }

    // ---------- 평상시: 이동 + 후퇴 백스텝 샷건 + 5연사 ----------
    private void UpdateNormalPhase()
    {
        // [90번] 보스는 물결의 공격 금지(EnemyAttackLock)를 받지 않습니다(진행 중인 연사만 끊기고 총알만 지워짐).
        bool entryDelayElapsed = Time.time >= attackUnlockTime;

        float distance = Vector3.Distance(transform.position, target.position);
        bool hasLineOfSight = CheckLineOfSight();

        UpdateMovement(distance, hasLineOfSight);
        UpdateFacing();

        if (!entryDelayElapsed || rangedAttack == null || !hasLineOfSight) return;
        if (Time.time < attackResumeTime) return; // 소환 물결 직후 - 이동/후퇴만, 연사/백스텝 샷건 없음

        if (TryStartBackstep()) return;
        UpdateBasicFiring(distance);
    }

    // 후퇴 중(시야가 있을 때만 실제로 후퇴함)이고 간격이 지났으면 진행 중인 연사를 끊고 백스텝을 시작합니다. 샷건은 백스텝이 끝나는 순간.
    private bool TryStartBackstep()
    {
        if (!IsRetreatCommitted) return false;
        if (Time.time < nextShotgunTime) return false;

        rangedAttack.CancelBurst();

        agent.isStopped = true;
        agent.ResetPath();

        Vector3 dir = FindBackstepDirection();
        float duration = Mathf.Max(backstepDuration, 0.05f);
        backstepVelocity = dir * (backstepDistance / duration);
        backstepRemaining = duration;
        backstepElapsed = 0f;
        backstepping = true;

        // 백스텝이 끝나 샷건을 쏠 때 간격을 다시 정하므로, 그 사이 다시 시작되지 않게만 막아 둡니다.
        nextShotgunTime = float.MaxValue;

        if (dir.sqrMagnitude > 0f) SpawnAfterimage();
        nextAfterimageAt = Mathf.Max(backstepAfterimage != null ? backstepAfterimage.spawnInterval : 0.04f, 0.01f);
        return true;
    }

    private void UpdateBackstep()
    {
        float dt = Time.deltaTime;
        float step = Mathf.Min(dt, backstepRemaining);
        // NavMeshAgent.Move는 NavMesh 안에서만 움직여(벽에서 미끄러짐) 장애물을 뚫지 않습니다. 경로 이동이 멈춰 있어도 동작합니다.
        if (step > 0f && backstepVelocity.sqrMagnitude > 0f) agent.Move(backstepVelocity * step);

        backstepRemaining -= dt;
        backstepElapsed += dt;

        if (target != null) UpdateFacing(); // 목표를 바라본 채 뒤로 빠짐

        if (backstepVelocity.sqrMagnitude > 0f && backstepRemaining > 0f && backstepElapsed >= nextAfterimageAt)
        {
            SpawnAfterimage();
            nextAfterimageAt = backstepElapsed + Mathf.Max(backstepAfterimage != null ? backstepAfterimage.spawnInterval : 0.04f, 0.01f);
        }

        if (backstepRemaining > 0f) return;

        backstepping = false;
        FireShotgun();
    }

    // 목표 반대 방향이 막혀 있으면 45/90도 비낀 방향 중 충분히 빠질 수 있는 첫 방향. 전부 막혀 있으면 제자리(0).
    private Vector3 FindBackstepDirection()
    {
        if (backstepDistance <= 0f) return Vector3.zero;

        Vector3 away = transform.position - target.position;
        away.y = 0f;
        if (away.sqrMagnitude < 0.0001f) away = -transform.forward;
        away.Normalize();

        float[] angles = { 0f, 45f, -45f, 90f, -90f };
        foreach (float angle in angles)
        {
            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * away;
            Vector3 end = transform.position + dir * backstepDistance;
            if (!NavMesh.Raycast(transform.position, end, out NavMeshHit hit, NavMesh.AllAreas)) return dir;
            if (hit.distance >= backstepDistance * 0.5f) return dir;
        }
        return Vector3.zero;
    }

    // 백스텝 도중 목표를 놓쳤으면(투명 등) 쏘지 않고, 곧바로 다시 시도할 수 있게 둡니다.
    private void FireShotgun()
    {
        if (target == null || rangedAttack == null)
        {
            nextShotgunTime = Time.time;
            return;
        }

        rangedAttack.FireSpreadNow(AimDirection(), shotgunPelletCount, shotgunSpreadAngle, shotgunDamage, shotgunProjectileSize,
                                   shotgunProjectileSpeed);
        nextShotgunTime = Time.time + shotgunInterval;
    }

    // 잔상은 순수 연출이라 오류가 나도 백스텝은 그대로 진행합니다.
    private void SpawnAfterimage()
    {
        if (backstepAfterimage == null || !backstepAfterimage.show) return;
        try
        {
            DashAfterimage.Spawn(gameObject, backstepAfterimage);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"BossLv2AIController: 백스텝 잔상 생성 중 오류가 발생해 건너뜁니다: {e}");
        }
    }

    private void UpdateBasicFiring(float distance)
    {
        if (distance > stats.engageRange) return; // 탄환 거리(rangedAttackRange)가 아니라 사격 개시 거리
        if (!rangedAttack.IsReady) return;        // 연사 중이거나 주기가 아직 안 참

        rangedAttack.TryFire(AimDirection());
    }

    private Vector3 AimDirection()
    {
        Vector3 dir = target.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;
        return dir.normalized;
    }
}
