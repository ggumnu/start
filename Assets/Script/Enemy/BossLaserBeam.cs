using System.Collections.Generic;
using UnityEngine;

// 3레벨 보스 1페이즈 레이저(보스 프리팹 루트에 붙임). 보스 정면 기준 같은 간격(6갈래 = 60도)으로 뻗고, 보스 몸이 돌면 같이 돕니다(회전은 BossLv3AIController).
// - 조준선(ShowTelegraph): 레이저가 지나갈 자리에 빨간 띠만(피해 없음) - 폭 0에서 조준선 시간 동안 서서히 넓어져 발사 직전 눈에 보이는 레이저 폭이 됨.
//   조준선 앞부분(차지 시간) 동안 보스 정중앙 한 곳에 빨간 빛 덩어리가 커지고 빛 입자가 빨려 들어감(차지 - 형빈 확정). 차지 시간이 끝나면
//   빛 덩어리는 빠르게 사그라들고 입자는 새로 나오지 않음(남은 조준선 시간 동안은 보스 흰 발광만 - BossLv3AIController).
//   조사(StartFiring): 판정 켜짐 - 발사 직후 Burst Hold Time 동안 기존 폭 x Burst Width Scale로 굵게, 이어서 Burst Shrink Time 동안 기존 폭으로
//   줄어듦(형빈 확정, 판정 폭도 같이 - Burst Affects Hitbox). 보스 회전은 줄어들기 시작할 때부터(BurstHoldDuration), 비콘은 다 줄어든 뒤부터
//   (BurstDuration - BossLv3AIController). 조준선은 발사 직후 굵기(기존 폭 x Burst Width Scale)까지 넓어짐.
//   조준선은 보스 중앙의 원과 이어진 한 덩어리(겹치는 곳 없이 한 메시 - 반투명이 겹쳐 진해지지 않게)로 그립니다(형빈 확정).
//   사그라듦(BeginFade): 판정은 즉시 꺼지고 외형만 줄어듦.
// - 길이: 보스에서 Wall 태그 콜라이더(방 벽 + 방 안 장애물)에 처음 닿는 곳까지(장애물 = 엄폐물 - 형빈 확정). 낮은 장애물은 Obstacle Check Height보다
//   낮으면 넘어감. 비콘은 Wall 태그가 아니라서 통과합니다(엄폐물 X - 형빈 확정).
// - 판정은 위에서 내려다본 평면(XZ) 기준 = 높이와 상관없는 벽:
//   플레이어가 닿으면 Damage 피해(74번 돌진 회피 가능, 보호막/무적은 PlayerHealth 규칙 그대로, 90번/112번으로 끊기지 않음).
//   플레이어 총알(63번 미사일 포함)은 닿는 즉시 아이템 효과 없이 소멸(Projectile), 수류탄은 종류/상태와 상관없이 닿은 자리에서 폭발(Grenade)
//   - 둘 다 BossLaserBlockers에 물어봄. 118번 포격은 하늘에서 떨어져 막지 않음.
// - 외형(참고 이미지 기준 - 형빈 확정): 카메라를 향해 돌아가는 띠(MobArchive/BossLaser - 흰 중심/자홍 몸통/보라 번짐/흐르는 결/지지직 번개 줄기),
//   발사구와 벽에 닿는 끝의 섬광(MobArchive/BossLaserFlare), 레이저를 따라 피어오르는 연기 + 보스 발밑에서 사방으로 퍼지는 먼지(파티클, MobArchive/BossLaserSmoke).
//   보이는 폭 = 판정 반폭 x Visual Width Scale(흰 중심 ≈ 판정 폭). 색/두께/흐름/번개/연기 값은 전부 아래 칸 - Play 중에도 바로 반영됩니다.
//   셰이더 3개는 아래 칸에 넣어 두면 빌드에도 포함됩니다(비우면 이름으로 찾음 - 빌드에선 Always Included가 필요).
// - 외형 오브젝트는 보스 자식이 아니라 따로 만들고(피격 색/사망 디졸브에 섞이지 않게) 보스가 사라지면 같이 지웁니다.
// 기획은 Docs/설계노트.md [3레벨 보스].
[DisallowMultipleComponent]
public class BossLaserBeam : MonoBehaviour
{
    [Header("레이저 모양")]
    [Tooltip("레이저 갈래 수(같은 간격).")]
    [Min(1)] public int beamCount = 6;
    [Tooltip("레이저가 시작되는 거리(보스 중심에서). 몸 안쪽에서 시작하게 두면 됩니다.")]
    [Min(0f)] public float muzzleDistance = 0.4f;
    [Tooltip("레이저 높이(보스 발밑 기준). 외형용입니다 - 판정은 높이와 상관없음.")]
    public float beamHeight = 0.45f;
    [Tooltip("최대 길이(벽에 닿지 않을 때).")]
    [Min(0.5f)] public float maxLength = 12f;
    [Tooltip("장애물 검사 높이(보스 발밑 기준). 이보다 낮은 장애물은 레이저가 넘어갑니다.")]
    public float obstacleCheckHeight = 0.25f;

    [Header("판정")]
    [Tooltip("판정 반폭(레이저 중심선에서 이 거리 안이면 닿음).")]
    [Min(0.01f)] public float hitHalfWidth = 0.2f;
    [Header("발사 직후 굵기")]
    [Tooltip("발사 직후 굵기 = 기존 폭 x 이 값.")]
    [Min(1f)] public float burstWidthScale = 1.5f;
    [Tooltip("발사 직후 굵은 채로 유지하는 시간(초).")]
    [Min(0f)] public float burstHoldTime = 1f;
    [Tooltip("굵은 폭에서 기존 폭으로 줄어드는 시간(초).")]
    [Min(0f)] public float burstShrinkTime = 1f;
    [Tooltip("켜면 굵어진 만큼 피해 판정 폭도 넓어집니다(보이는 대로 맞음).")]
    public bool burstAffectsHitbox = true;

    [Header("판정 (계속)")]
    [Tooltip("플레이어 몸 반지름(판정 반폭에 더함).")]
    [Min(0f)] public float playerRadius = 0.2f;
    [Tooltip("플레이어가 닿았을 때 피해(0.5 = 반 칸).")]
    [Min(0f)] public float damage = 2f;

    [Header("조준선 (조사 전 예고 - 피해 없음)")]
    public Color telegraphColor = new Color(1f, 0.1f, 0.1f, 0.6f);
    [Tooltip("다 넓어졌을 때 폭 = 발사 직후 레이저 폭(판정 반폭 x Visual Width Scale x 2 x Burst Width Scale) x 이 값.")]
    [Min(0f)] public float telegraphWidthScale = 1f;
    [Tooltip("조준선 시간(0~1) 동안 폭이 넓어지는 모양(세로 0 = 안 보임, 1 = 다 넓어짐).")]
    public AnimationCurve telegraphGrowCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [Tooltip("조준선 높이(보스 발밑 기준, 바닥에 깔림).")]
    public float telegraphHeight = 0.03f;
    [Tooltip("보스 중앙 원의 반지름(다 넓어졌을 때, m). 조준선과 같이 넓어지고, 조준선 반폭보다 작아지지 않습니다.")]
    [Min(0f)] public float telegraphCenterRadius = 0.6f;

    [Header("차지 (조준선 동안 보스 정중앙에 모이는 빛)")]
    public bool chargeEnabled = true;
    [Tooltip("차지 빛 높이(보스 발밑 기준).")]
    public float chargeHeight = 0.45f;
    [Tooltip("차지 빛을 카메라 쪽으로 당기는 거리(보스 몸에 가려지지 않게 - 몸이 크면 늘려 주세요).")]
    [Min(0f)] public float chargeCameraBias = 0.8f;
    [Tooltip("MobArchive/BossLaserSpark (Assets/Shaders/BossLaserSpark.shader) - 빨려 들어가는 빛 입자. 빛 덩어리는 섬광 셰이더(Flare Shader)를 씀.")]
    public Shader sparkShader;
    [Tooltip("빛 덩어리가 다 커졌을 때 크기(m).")]
    [Min(0f)] public float chargeOrbSize = 0.6f;
    [Tooltip("조준선 시간(0~1) 동안 빛 덩어리가 커지고 밝아지는 모양.")]
    public AnimationCurve chargeOrbCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [Tooltip("차지 시간이 끝난 뒤 빛 덩어리가 사그라드는 시간(초).")]
    [Min(0.01f)] public float chargeOrbEndFadeTime = 0.15f;
    [ColorUsage(false, true)] public Color chargeOrbColor = new Color(2.6f, 0.45f, 0.35f, 1f);
    [ColorUsage(false, true)] public Color chargeOrbGlowColor = new Color(0.75f, 0.05f, 0.04f, 1f);
    [Tooltip("초당 빛 입자 수(다 모였을 때 - 처음엔 0에서 늘어남).")]
    [Min(0f)] public float chargeSparkRate = 45f;
    [Tooltip("빛 입자가 생기는 거리(차지 중심에서, m).")]
    [Min(0.05f)] public float chargeSparkRadius = 0.9f;
    [Tooltip("빛 입자가 차지 중심까지 빨려 들어가는 시간(초).")]
    [Min(0.05f)] public float chargeSparkTime = 0.35f;
    public Vector2 chargeSparkSize = new Vector2(0.06f, 0.13f);
    [Tooltip("빛 입자 색(파티클 색은 1을 넘지 못해 밝기는 아래 Intensity로 올림).")]
    public Color chargeSparkColor = new Color(1f, 0.25f, 0.18f, 1f);
    [Min(0f)] public float chargeSparkIntensity = 2.5f;

    [Header("레이저 이펙트 - 셰이더 (프리팹에 넣어 두면 빌드에도 포함)")]
    [Tooltip("MobArchive/BossLaser (Assets/Shaders/BossLaser.shader)")]
    public Shader beamShader;
    [Tooltip("MobArchive/BossLaserFlare (Assets/Shaders/BossLaserFlare.shader)")]
    public Shader flareShader;
    [Tooltip("MobArchive/BossLaserSmoke (Assets/Shaders/BossLaserSmoke.shader)")]
    public Shader smokeShader;

    [Header("레이저 이펙트 - 띠")]
    [Tooltip("보이는 반폭 = 판정 반폭 x 이 값(흰 중심이 판정 폭과 비슷해지는 값이 기본).")]
    [Min(0.1f)] public float visualWidthScale = 2.6f;
    [Tooltip("레이저 시작부를 둥글게(0 = 각진 끝, 1 = 반원, 1보다 크면 길쭉한 둥근 끝). 둥근 부분만큼 띠를 보스 쪽으로 더 늘여 그립니다.")]
    [Range(0f, 2f)] public float startCapRoundness = 1f;
    [Tooltip("번개 줄기가 몸통 밖으로 튀어나갈 여유(띠를 이만큼 더 넓게 그림).")]
    [Min(1f)] public float crackleOverscan = 1.2f;
    [ColorUsage(false, true)] public Color coreColor = new Color(2.2f, 2.0f, 2.2f, 1f);
    [Tooltip("흰 중심 폭(보이는 반폭 대비).")]
    [Range(0.02f, 1f)] public float coreWidth = 0.14f;
    [ColorUsage(false, true)] public Color bodyColor = new Color(1.25f, 0.15f, 1.0f, 1f);
    [ColorUsage(false, true)] public Color glowColor = new Color(0.28f, 0.03f, 0.55f, 1f);
    [Tooltip("가장자리 일렁임 세기.")]
    [Range(0f, 0.6f)] public float edgeWobble = 0.22f;
    [Tooltip("에너지 결이 흐르는 속도.")]
    public float flowSpeed = 9f;
    [Tooltip("에너지 결 크기(클수록 잘게).")]
    public float flowScale = 1.6f;

    [Header("레이저 이펙트 - 번개 줄기 (지지직)")]
    [ColorUsage(false, true)] public Color crackleColor = new Color(0.10f, 0f, 0.16f, 1f);
    [Range(0f, 1f)] public float crackleOpacity = 0.85f;
    [Tooltip("초당 모양이 바뀌는 횟수.")]
    [Min(0f)] public float crackleRate = 24f;
    [Tooltip("다음 모양으로 넘어가는 방식(0 = 순간 교체, 1 = 이번 모양에서 다음 모양으로 계속 부드럽게 이어짐).")]
    [Range(0f, 1f)] public float crackleSmoothness = 1f;
    [Tooltip("이어져 보이는 비율(작을수록 많이 끊김).")]
    [Range(0f, 1f)] public float crackleDensity = 0.55f;
    [Tooltip("줄기 두께(보이는 반폭 대비).")]
    [Range(0.005f, 0.5f)] public float crackleWidth = 0.09f;
    [Tooltip("줄기가 위아래로 꺾이는 폭(보이는 반폭 대비, 1을 넘으면 몸통 밖으로 튐).")]
    [Range(0f, 1.2f)] public float crackleAmplitude = 0.6f;
    [Tooltip("1m 당 꺾이는 횟수.")]
    [Min(0.1f)] public float crackleFrequency = 5f;

    [Header("레이저 이펙트 - 섬광 (발사구 / 벽에 닿는 끝)")]
    [Tooltip("발사구 섬광 크기(m).")]
    [Min(0f)] public float muzzleFlareSize = 0.55f;
    [Tooltip("끝 섬광 크기(m).")]
    [Min(0f)] public float endFlareSize = 0.65f;
    [ColorUsage(false, true)] public Color flareColor = new Color(2.2f, 1.3f, 2.0f, 1f);
    [ColorUsage(false, true)] public Color flareGlowColor = new Color(0.6f, 0.07f, 0.48f, 1f);
    [Tooltip("섬광을 카메라 쪽으로 당기는 거리(보스 몸에 묻히지 않게).")]
    [Min(0f)] public float flareCameraBias = 0.35f;

    [Header("레이저 이펙트 - 연기 (레이저를 따라 피어오름)")]
    public bool beamSmokeEnabled = true;
    [Tooltip("레이저 1m 당 초당 연기 수.")]
    [Min(0f)] public float smokeRatePerMeter = 5f;
    public Vector2 smokeLifetime = new Vector2(1f, 1.6f);
    public Vector2 smokeSize = new Vector2(0.25f, 0.5f);
    [Tooltip("위로 피어오르는 속도.")]
    public Vector2 smokeRiseSpeed = new Vector2(0.1f, 0.35f);
    public Color smokeColor = new Color(0.20f, 0.18f, 0.22f, 0.5f);
    [Tooltip("연기가 생기는 높이(보스 발밑 기준).")]
    public float smokeHeight = 0.1f;

    [Header("레이저 이펙트 - 먼지 (보스 발밑에서 사방으로)")]
    public bool baseDustEnabled = true;
    [Tooltip("초당 먼지 수.")]
    [Min(0f)] public float dustRate = 30f;
    [Tooltip("먼지가 생기는 원 반지름(보스 중심 기준).")]
    [Min(0f)] public float dustSpawnRadius = 0.35f;
    public Vector2 dustSpeed = new Vector2(1.2f, 2.2f);
    public Vector2 dustLifetime = new Vector2(0.7f, 1.1f);
    public Vector2 dustSize = new Vector2(0.3f, 0.6f);
    public Color dustColor = new Color(0.30f, 0.26f, 0.23f, 0.55f);
    [Tooltip("퍼지다가 느려지는 정도.")]
    [Min(0f)] public float dustDrag = 2f;

    private enum Mode { Off, Telegraph, Firing, Fading }
    private Mode mode = Mode.Off;
    private float fadeTimer;
    private float fadeDuration = 0.5f;
    private float telegraphElapsed;
    private float telegraphDuration = 1f;
    private float chargeDuration = 1f;
    private float firingElapsed;

    // 이번 프레임 레이저 선분(XZ 판정용, y는 보스 발밑 높이).
    private Vector3[] starts = new Vector3[0];
    private Vector3[] ends = new Vector3[0];

    private PlayerHealth playerHealth;
    private Transform playerTransform;

    private Mesh quadMesh;   // 바닥과 나란한 띠(조준선) / 카메라를 향해 세운 띠(레이저) - X -0.5~0.5, Z 0~1
    private Mesh flareMesh;  // 카메라를 향한 사각형(섬광) - XY -0.5~0.5
    private Material telegraphMaterial;
    private Material beamMaterial;
    private Material flareMaterial;
    private Material smokeMaterial;
    private Material chargeOrbMaterial;
    private Material sparkMaterial;
    private MaterialPropertyBlock block;
    private Camera cachedCamera;
    private GameObject telegraphObject;   // 중앙 원 + 조준선 띠를 한 메시로(매 프레임 다시 만듦)
    private Mesh telegraphMesh;
    private readonly List<Vector3> telegraphVerts = new List<Vector3>();
    private readonly List<int> telegraphTris = new List<int>();
    private readonly List<GameObject> beamObjects = new List<GameObject>();
    private readonly List<GameObject> muzzleFlares = new List<GameObject>();
    private readonly List<GameObject> endFlares = new List<GameObject>();
    private readonly List<ParticleSystem> smokeSystems = new List<ParticleSystem>();
    private ParticleSystem dustSystem;
    private readonly List<GameObject> chargeOrbs = new List<GameObject>();
    private readonly List<ParticleSystem> chargeSparkSystems = new List<ParticleSystem>();
    private bool chargeSparksRunning;
    private bool loggedMissingShader;

    private static readonly int SeedId = Shader.PropertyToID("_Seed");
    private static readonly int FadeId = Shader.PropertyToID("_Fade");
    private static readonly int LengthId = Shader.PropertyToID("_BeamLength");
    private static readonly int OverscanId = Shader.PropertyToID("_Overscan");
    private static readonly int CapLengthId = Shader.PropertyToID("_CapLength");

    // 판정이 켜져 있는지(조사 중). 조준선/사그라듦 중에는 false.
    public bool IsFiring => mode == Mode.Firing;
    public bool IsActive => mode != Mode.Off;
    public float HitHalfWidth => hitHalfWidth * (burstAffectsHitbox ? CurrentBurstScale() : 1f);
    // 발사 직후 굵어졌다가 기존 폭으로 줄어들기까지 걸리는 시간(초) - 보스 회전/비콘 타이밍 기준.
    public float BurstDuration => (burstWidthScale > 1f ? burstHoldTime + burstShrinkTime : 0f);
    // 발사 직후 굵은 채로 유지하는 시간(초) - 이 뒤 줄어들기 시작(보스 회전 시작 기준).
    public float BurstHoldDuration => (burstWidthScale > 1f ? burstHoldTime : 0f);

    // 지금 폭 배율(발사 직후 Burst Width Scale -> 기존 1). 조사 중이 아니면 1.
    private float CurrentBurstScale()
    {
        if (mode != Mode.Firing || burstWidthScale <= 1f) return 1f;
        if (firingElapsed <= burstHoldTime) return burstWidthScale;
        float t = burstShrinkTime > 0f ? Mathf.Clamp01((firingElapsed - burstHoldTime) / burstShrinkTime) : 1f;
        t = t * t * (3f - 2f * t); // 부드럽게 줄어듦
        return Mathf.Lerp(burstWidthScale, 1f, t);
    }

    void OnDisable()
    {
        // 방이 꺼지면(플레이어가 나감) 판정을 끕니다 - 방을 나가면 보스는 처음 상태로 다시 만들어집니다.
        StopImmediate();
    }

    void OnDestroy()
    {
        BossLaserBlockers.Unregister(this);
        if (telegraphObject != null) Destroy(telegraphObject);
        if (telegraphMesh != null) Destroy(telegraphMesh);
        DestroyAll(beamObjects);
        DestroyAll(muzzleFlares);
        DestroyAll(endFlares);
        DestroyAll(chargeOrbs);
        foreach (var ps in smokeSystems) if (ps != null) Destroy(ps.gameObject);
        smokeSystems.Clear();
        foreach (var ps in chargeSparkSystems) if (ps != null) Destroy(ps.gameObject);
        chargeSparkSystems.Clear();
        if (dustSystem != null) Destroy(dustSystem.gameObject);
        if (quadMesh != null) Destroy(quadMesh);
        if (flareMesh != null) Destroy(flareMesh);
        if (telegraphMaterial != null) Destroy(telegraphMaterial);
        if (beamMaterial != null) Destroy(beamMaterial);
        if (flareMaterial != null) Destroy(flareMaterial);
        if (smokeMaterial != null) Destroy(smokeMaterial);
        if (chargeOrbMaterial != null) Destroy(chargeOrbMaterial);
        if (sparkMaterial != null) Destroy(sparkMaterial);
    }

    private static void DestroyAll(List<GameObject> list)
    {
        foreach (var go in list) if (go != null) Destroy(go);
        list.Clear();
    }

    // 조준선만 보여줍니다(피해 없음). duration = 조준선 시간(이 동안 폭이 넓어짐), chargeTime = 그 앞부분 차지 빛이 모이는 시간
    // (0 이하 또는 duration보다 길면 조준선 시간 전체).
    public void ShowTelegraph(float duration = 1f, float chargeTime = -1f)
    {
        mode = Mode.Telegraph;
        telegraphElapsed = 0f;
        telegraphDuration = Mathf.Max(duration, 0.01f);
        chargeDuration = chargeTime > 0f ? Mathf.Min(chargeTime, telegraphDuration) : telegraphDuration;
        BossLaserBlockers.Unregister(this);
        RefreshNow();
    }

    // 조사 시작 - 판정이 켜집니다.
    public void StartFiring()
    {
        mode = Mode.Firing;
        firingElapsed = 0f;
        BossLaserBlockers.Register(this);
        RefreshNow();
    }

    // 사그라듦 - 판정은 바로 꺼지고 외형만 duration 동안 가늘어지며 사라집니다.
    public void BeginFade(float duration)
    {
        if (mode == Mode.Off) return;
        BossLaserBlockers.Unregister(this);
        if (duration <= 0f) { StopImmediate(); return; }
        mode = Mode.Fading;
        fadeDuration = duration;
        fadeTimer = duration;
    }

    // 즉시 끔(보스 사망/방 꺼짐).
    public void StopImmediate()
    {
        mode = Mode.Off;
        BossLaserBlockers.Unregister(this);
        if (telegraphObject != null && telegraphObject.activeSelf) telegraphObject.SetActive(false);
        SetObjectsActive(beamObjects, false);
        SetObjectsActive(muzzleFlares, false);
        SetObjectsActive(endFlares, false);
        SetObjectsActive(chargeOrbs, false);
        StopChargeSparks();
        foreach (var ps in smokeSystems) if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (dustSystem != null) dustSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    void LateUpdate()
    {
        if (mode == Mode.Off) return;

        if (mode == Mode.Fading)
        {
            fadeTimer -= Time.deltaTime;
            if (fadeTimer <= 0f) { StopImmediate(); return; }
        }
        if (mode == Mode.Telegraph) telegraphElapsed += Time.deltaTime;
        if (mode == Mode.Firing) firingElapsed += Time.deltaTime;

        // 보스 회전(Update)이 끝난 뒤의 방향으로 선분/외형/플레이어 판정을 맞춥니다.
        RefreshNow();
        if (mode == Mode.Firing) DamagePlayerIfTouching();
    }

    private void RefreshNow()
    {
        ComputeSegments();
        UpdateVisuals();
    }

    // ---------- 선분 계산 ----------

    private void ComputeSegments()
    {
        int count = Mathf.Max(1, beamCount);
        if (starts.Length != count)
        {
            starts = new Vector3[count];
            ends = new Vector3[count];
        }

        Vector3 root = transform.position;
        Vector3 forward = transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
        forward.Normalize();

        float step = 360f / count;
        for (int i = 0; i < count; i++)
        {
            Vector3 dir = Quaternion.AngleAxis(step * i, Vector3.up) * forward;
            Vector3 start = root + dir * muzzleDistance;
            float length = MeasureLength(root, dir);
            starts[i] = start;
            ends[i] = root + dir * Mathf.Max(muzzleDistance, length);
        }
    }

    // 보스 중심에서 dir로 Wall 태그 콜라이더에 처음 닿는 거리(없으면 최대 길이).
    private float MeasureLength(Vector3 root, Vector3 dir)
    {
        Vector3 origin = root + Vector3.up * obstacleCheckHeight;
        float best = maxLength;
        RaycastHit[] hits = Physics.RaycastAll(origin, dir, maxLength, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider c = hits[i].collider;
            if (c == null || !c.CompareTag("Wall")) continue;
            if (c.transform.IsChildOf(transform)) continue;
            if (hits[i].distance < best) best = hits[i].distance;
        }
        return best;
    }

    // ---------- 플레이어 피해 ----------

    private void DamagePlayerIfTouching()
    {
        if (playerHealth == null)
        {
            playerHealth = FindFirstObjectByType<PlayerHealth>();
            playerTransform = playerHealth != null ? playerHealth.transform : null;
        }
        if (playerHealth == null || playerTransform == null || playerHealth.IsDead) return;
        if (!playerHealth.isActiveAndEnabled) return;
        if (playerHealth.IsInvincible) return;   // 무적 시간 중엔 부르지 않음(PlayerHealth도 막지만 매 프레임 부르지 않게)
        if (PlayerDash.IsDodging) return;        // 74번 돌진 회피

        if (!BossLaserBlockers.IsTouching(this, playerTransform.position, playerRadius)) return;

        playerHealth.TakeDamage(damage, PlayerDamageSource.FromEnemy(this, PlayerDamageSource.AttackLaser));
    }

    // 점 p(XZ)가 이 레이저 중 하나에 닿는지(반지름 radius 포함).
    public bool ContainsPoint(Vector3 p, float radius)
    {
        if (mode != Mode.Firing) return false;
        float limit = HitHalfWidth + Mathf.Max(0f, radius);
        float limitSq = limit * limit;
        for (int i = 0; i < starts.Length; i++)
        {
            if (DistanceSqXZ(p, starts[i], ends[i]) <= limitSq) return true;
        }
        return false;
    }

    private static float DistanceSqXZ(Vector3 p, Vector3 a, Vector3 b)
    {
        float abx = b.x - a.x, abz = b.z - a.z;
        float apx = p.x - a.x, apz = p.z - a.z;
        float lenSq = abx * abx + abz * abz;
        float t = lenSq > 0.000001f ? Mathf.Clamp01((apx * abx + apz * abz) / lenSq) : 0f;
        float dx = apx - abx * t, dz = apz - abz * t;
        return dx * dx + dz * dz;
    }

    // ---------- 외형 ----------

    private void UpdateVisuals()
    {
        EnsureVisualObjects();

        bool showTelegraph = mode == Mode.Telegraph;
        bool showBeam = mode == Mode.Firing || mode == Mode.Fading;
        float fade01 = mode == Mode.Fading ? Mathf.Clamp01(fadeTimer / Mathf.Max(fadeDuration, 0.0001f)) : 1f;
        float groundY = transform.position.y;
        Vector3 camPos = CameraPosition();
        float visualHalf = hitHalfWidth * visualWidthScale;   // 기존 폭(조준선 기준)
        float beamHalf = visualHalf * CurrentBurstScale();      // 지금 레이저 폭(발사 직후 굵음)

        ApplyBeamMaterialValues();

        // 조준선: 폭 0에서 조준선 시간 동안 발사 직후 레이저 폭(굵은 폭)까지 넓어짐.
        float telegraph01 = Mathf.Clamp01(telegraphElapsed / Mathf.Max(telegraphDuration, 0.01f));
        float telegraphWidthNow = visualHalf * Mathf.Max(1f, burstWidthScale) * 2f * telegraphWidthScale * Mathf.Max(0f, EvaluateCurve(telegraphGrowCurve, telegraph01));
        float telegraphGrow01 = Mathf.Max(0f, EvaluateCurve(telegraphGrowCurve, telegraph01));
        bool telegraphOn = showTelegraph && telegraphWidthNow > 0.0001f && telegraphObject != null;
        if (telegraphObject != null && telegraphObject.activeSelf != telegraphOn) telegraphObject.SetActive(telegraphOn);
        if (telegraphOn) BuildTelegraphMesh(groundY + telegraphHeight, telegraphWidthNow * 0.5f, telegraphCenterRadius * telegraphGrow01);

        UpdateCharge(showTelegraph && chargeEnabled, telegraphElapsed, groundY, camPos);

        for (int i = 0; i < beamObjects.Count; i++)
        {
            bool on = showBeam && i < starts.Length && beamMaterial != null;
            GameObject beam = beamObjects[i];
            if (beam.activeSelf != on) beam.SetActive(on);
            if (on)
            {
                Vector3 a = new Vector3(starts[i].x, groundY + beamHeight, starts[i].z);
                Vector3 b = new Vector3(ends[i].x, groundY + beamHeight, ends[i].z);
                // 사그라들 때는 가늘어지면서(폭) 흐려짐(_Fade)
                float halfNow = beamHalf * Mathf.Lerp(0.3f, 1f, fade01);
                // 둥근 시작부: 지금 반폭 x Start Cap Roundness 길이만큼 보스 쪽으로 늘이고, 그 구간을 셰이더가 둥글게 깎습니다.
                float capLength = halfNow * startCapRoundness;
                Vector3 axis = b - a;
                if (capLength > 0f && axis.sqrMagnitude > 0.000001f) a -= axis.normalized * capLength;
                float length = PlaceCameraRibbon(beam.transform, a, b, halfNow * 2f * crackleOverscan, camPos);
                var r = beam.GetComponent<MeshRenderer>();
                r.GetPropertyBlock(block);
                block.SetFloat(SeedId, i * 1.731f + 0.37f);
                block.SetFloat(FadeId, fade01);
                block.SetFloat(LengthId, length);
                block.SetFloat(OverscanId, crackleOverscan);
                block.SetFloat(CapLengthId, capLength);
                r.SetPropertyBlock(block);
            }

            bool flareOn = on && flareMaterial != null;
            int k = Mathf.Min(i, starts.Length - 1);
            Vector3 muzzle = new Vector3(starts[k].x, groundY + beamHeight, starts[k].z);
            Vector3 tip = new Vector3(ends[k].x, groundY + beamHeight, ends[k].z);
            UpdateFlare(muzzleFlares[i], flareOn, muzzle, muzzleFlareSize, fade01, i * 2.1f, camPos);
            UpdateFlare(endFlares[i], flareOn, tip, endFlareSize, fade01, i * 3.7f + 1f, camPos);
        }

        UpdateSmoke(groundY);
        UpdateDust(groundY);
    }

    private static float EvaluateCurve(AnimationCurve curve, float t)
    {
        return curve != null && curve.length > 0 ? curve.Evaluate(t) : t;
    }

    // 차지: 보스 정중앙 한 곳에 빛 덩어리가 커지고 밝아지며, 주변에서 빛 입자가 그 점으로 빨려 들어갑니다.
    // 빛 덩어리는 보스 몸 안에 있으면 가려지므로 카메라 쪽으로 Charge Camera Bias만큼 당겨 그립니다(입자 중심도 같은 자리).
    // elapsed = 조준선 시작부터 지난 시간. 차지 시간 안 = 모이는 중, 지나면 빛 덩어리는 Charge Orb End Fade Time 동안 사그라들고 입자는 멈춤.
    private void UpdateCharge(bool show, float elapsed, float groundY, Vector3 camPos)
    {
        float chargeTime = Mathf.Max(chargeDuration, 0.01f);
        float t01 = Mathf.Clamp01(elapsed / chargeTime);
        bool gathering = elapsed < chargeTime;
        float endFade = gathering ? 1f : Mathf.Clamp01(1f - (elapsed - chargeTime) / Mathf.Max(chargeOrbEndFadeTime, 0.01f));

        Vector3 center = new Vector3(transform.position.x, groundY + chargeHeight, transform.position.z);
        Vector3 toCam = camPos - center;
        float camDist = toCam.magnitude;
        Vector3 biased = camDist > 0.0001f ? center + toCam / camDist * Mathf.Min(chargeCameraBias, camDist * 0.5f) : center;

        float orb01 = Mathf.Clamp01(EvaluateCurve(chargeOrbCurve, t01)) * endFade;
        for (int i = 0; i < chargeOrbs.Count; i++)
        {
            bool on = show && i == 0 && chargeOrbMaterial != null && orb01 > 0.001f;
            UpdateFlare(chargeOrbs[i], on, biased, chargeOrbSize * orb01, orb01, 0.5f, camPos, 0f);
        }

        bool sparks = show && sparkMaterial != null;
        if (!sparks)
        {
            StopChargeSparks();
            return;
        }

        float radius = Mathf.Max(0.05f, chargeSparkRadius);
        float life = Mathf.Max(0.05f, chargeSparkTime);
        for (int i = 0; i < chargeSparkSystems.Count; i++)
        {
            ParticleSystem ps = chargeSparkSystems[i];
            if (ps == null) continue;
            var emission = ps.emission;
            if (i != 0 || !gathering) { emission.rateOverTime = 0f; continue; } // 차지가 끝나면 새 입자 없음(날아가던 입자는 마저 모임)

            ps.transform.position = biased;
            var main = ps.main;
            main.startLifetime = life;
            main.startSpeed = -radius / life; // 구 표면에서 중심으로 - 수명이 끝날 때 딱 도착
            main.startSize = new ParticleSystem.MinMaxCurve(Mathf.Min(chargeSparkSize.x, chargeSparkSize.y), Mathf.Max(chargeSparkSize.x, chargeSparkSize.y));
            main.startColor = chargeSparkColor;
            var shape = ps.shape;
            shape.radius = radius;
            emission.rateOverTime = chargeSparkRate * t01;
            if (!ps.isPlaying) ps.Play();
        }
        chargeSparksRunning = true;
    }

    private void StopChargeSparks()
    {
        if (!chargeSparksRunning) return;
        chargeSparksRunning = false;
        foreach (var ps in chargeSparkSystems) if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void ApplyBeamMaterialValues()
    {
        if (beamMaterial != null)
        {
            beamMaterial.SetColor("_CoreColor", coreColor);
            beamMaterial.SetFloat("_CoreWidth", coreWidth);
            beamMaterial.SetColor("_BodyColor", bodyColor);
            beamMaterial.SetColor("_GlowColor", glowColor);
            beamMaterial.SetFloat("_EdgeWobble", edgeWobble);
            beamMaterial.SetFloat("_FlowSpeed", flowSpeed);
            beamMaterial.SetFloat("_FlowScale", flowScale);
            beamMaterial.SetColor("_CrackleColor", crackleColor);
            beamMaterial.SetFloat("_CrackleOpacity", crackleOpacity);
            beamMaterial.SetFloat("_CrackleRate", crackleRate);
            beamMaterial.SetFloat("_CrackleSmoothness", crackleSmoothness);
            beamMaterial.SetFloat("_CrackleDensity", crackleDensity);
            beamMaterial.SetFloat("_CrackleWidth", crackleWidth);
            beamMaterial.SetFloat("_CrackleAmplitude", crackleAmplitude);
            beamMaterial.SetFloat("_CrackleFrequency", crackleFrequency);
        }
        if (flareMaterial != null)
        {
            flareMaterial.SetColor("_Color", flareColor);
            flareMaterial.SetColor("_GlowColor", flareGlowColor);
        }
        if (sparkMaterial != null) sparkMaterial.SetFloat("_Intensity", chargeSparkIntensity);
        if (chargeOrbMaterial != null)
        {
            chargeOrbMaterial.SetColor("_Color", chargeOrbColor);
            chargeOrbMaterial.SetColor("_GlowColor", chargeOrbGlowColor);
        }
    }

    // cameraBias < 0 = 기본(Flare Camera Bias), 0 이상 = 그 거리만큼 카메라 쪽으로 당김(이미 당긴 자리를 넘기면 0).
    private void UpdateFlare(GameObject flare, bool on, Vector3 a, float size, float fade01, float seed, Vector3 camPos, float cameraBias = -1f)
    {
        on = on && size > 0f;
        if (flare.activeSelf != on) flare.SetActive(on);
        if (!on) return;

        Vector3 toCam = camPos - a;
        float dist = toCam.magnitude;
        Vector3 dirToCam = dist > 0.0001f ? toCam / dist : Vector3.up;
        Transform t = flare.transform;
        float bias = cameraBias < 0f ? flareCameraBias : cameraBias;
        t.position = a + dirToCam * Mathf.Min(bias, dist * 0.5f);
        t.rotation = Quaternion.LookRotation(-dirToCam, Vector3.up);
        t.localScale = Vector3.one * size * Mathf.Lerp(0.5f, 1f, fade01);

        var r = flare.GetComponent<MeshRenderer>();
        r.GetPropertyBlock(block);
        block.SetFloat(SeedId, seed);
        block.SetFloat(FadeId, fade01);
        r.SetPropertyBlock(block);
    }

    // 레이저를 따라 피어오르는 연기: 레이저마다 선(Edge) 모양 방출기를 레이저 위에 깔고, 길이에 비례해 내보냅니다. 조사 중에만 새로 나옴.
    private void UpdateSmoke(float groundY)
    {
        bool emit = mode == Mode.Firing && beamSmokeEnabled && smokeMaterial != null;
        for (int i = 0; i < smokeSystems.Count; i++)
        {
            ParticleSystem ps = smokeSystems[i];
            if (ps == null) continue;
            bool beamExists = i < starts.Length;
            var emission = ps.emission;
            if (!emit || !beamExists)
            {
                emission.rateOverTime = 0f;
                continue;
            }

            Vector3 a = starts[i];
            Vector3 b = ends[i];
            Vector3 d = b - a;
            d.y = 0f;
            float length = d.magnitude;
            if (length < 0.05f) { emission.rateOverTime = 0f; continue; }

            Transform t = ps.transform;
            Vector3 mid = (a + b) * 0.5f;
            t.position = new Vector3(mid.x, groundY + smokeHeight, mid.z);
            t.rotation = Quaternion.FromToRotation(Vector3.right, d / length);
            var shape = ps.shape;
            shape.radius = length * 0.5f;

            ApplySmokeMain(ps, smokeLifetime, smokeRiseSpeed, smokeSize, smokeColor);
            emission.rateOverTime = smokeRatePerMeter * length;
            if (!ps.isPlaying) ps.Play();
        }
    }

    // 보스 발밑에서 사방으로 퍼지는 먼지. 조사 중에만 새로 나옴.
    private void UpdateDust(float groundY)
    {
        if (dustSystem == null) return;
        var emission = dustSystem.emission;
        bool emit = mode == Mode.Firing && baseDustEnabled && smokeMaterial != null;
        if (!emit)
        {
            emission.rateOverTime = 0f;
            return;
        }

        Transform t = dustSystem.transform;
        t.position = new Vector3(transform.position.x, groundY + 0.08f, transform.position.z);
        t.rotation = Quaternion.Euler(-90f, 0f, 0f); // 원(Circle) 방출 면을 바닥과 나란하게
        var shape = dustSystem.shape;
        shape.radius = Mathf.Max(0.01f, dustSpawnRadius);
        var limit = dustSystem.limitVelocityOverLifetime;
        limit.drag = dustDrag;

        ApplySmokeMain(dustSystem, dustLifetime, dustSpeed, dustSize, dustColor);
        emission.rateOverTime = dustRate;
        if (!dustSystem.isPlaying) dustSystem.Play();
    }

    private static void ApplySmokeMain(ParticleSystem ps, Vector2 lifetime, Vector2 speed, Vector2 size, Color color)
    {
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(Mathf.Min(lifetime.x, lifetime.y), Mathf.Max(lifetime.x, lifetime.y));
        main.startSpeed = new ParticleSystem.MinMaxCurve(Mathf.Min(speed.x, speed.y), Mathf.Max(speed.x, speed.y));
        main.startSize = new ParticleSystem.MinMaxCurve(Mathf.Min(size.x, size.y), Mathf.Max(size.x, size.y));
        main.startColor = color;
    }

    private Vector3 CameraPosition()
    {
        if (cachedCamera == null || !cachedCamera.isActiveAndEnabled) cachedCamera = Camera.main;
        return cachedCamera != null ? cachedCamera.transform.position : transform.position + new Vector3(2.25f, 3.75f, -2.25f);
    }

    // 조준선(바닥, 월드 좌표): 보스 중앙 원(반지름 radius) + 원 바깥으로 뻗는 띠들(반폭 half). 띠는 원 둘레(호)에서 시작해
    // 원과 겹치지 않는 부분만 그리므로 반투명이 겹쳐 진해지는 곳이 없습니다.
    private void BuildTelegraphMesh(float y, float half, float radius)
    {
        const int circleSegments = 48;
        const int arcSegments = 8;
        Vector3 c = new Vector3(transform.position.x, y, transform.position.z);
        float r = Mathf.Max(radius, half * 1.02f, 0.01f);

        telegraphVerts.Clear();
        telegraphTris.Clear();

        // 원(가운데 점에서 부채꼴)
        telegraphVerts.Add(c);
        for (int k = 0; k < circleSegments; k++)
        {
            float a = k * Mathf.PI * 2f / circleSegments;
            telegraphVerts.Add(c + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r));
        }
        for (int k = 0; k < circleSegments; k++)
        {
            telegraphTris.Add(0);
            telegraphTris.Add(1 + k);
            telegraphTris.Add(1 + (k + 1) % circleSegments);
        }

        // 띠: 원 둘레의 호(양 옆 모서리가 원과 만나는 곳 사이) -> 끝. 끝 가운데 점에서 부채꼴.
        float halfAngle = Mathf.Asin(Mathf.Clamp(half / r, 0f, 0.999f));
        for (int i = 0; i < ends.Length; i++)
        {
            Vector3 d = ends[i] - c;
            d.y = 0f;
            float length = d.magnitude;
            if (length <= r + 0.001f) continue; // 벽이 원 안쪽이면 띠 없음
            d /= length;
            Vector3 side = new Vector3(d.z, 0f, -d.x);
            Vector3 end = c + d * length;

            int baseIndex = telegraphVerts.Count;
            telegraphVerts.Add(end);
            telegraphVerts.Add(end + side * half);
            for (int k = 0; k <= arcSegments; k++)
            {
                float a = Mathf.Lerp(halfAngle, -halfAngle, k / (float)arcSegments);
                telegraphVerts.Add(c + (d * Mathf.Cos(a) + side * Mathf.Sin(a)) * r);
            }
            telegraphVerts.Add(end - side * half);
            int last = telegraphVerts.Count - 1;
            for (int v = baseIndex + 1; v < last; v++)
            {
                telegraphTris.Add(baseIndex);
                telegraphTris.Add(v);
                telegraphTris.Add(v + 1);
            }
        }

        telegraphMesh.Clear();
        telegraphMesh.SetVertices(telegraphVerts);
        telegraphMesh.SetTriangles(telegraphTris, 0);
        telegraphMesh.RecalculateBounds();
    }

    // a -> b 띠를 레이저 축은 그대로 두고 카메라 쪽으로 돌려 세웁니다(어느 각도에서도 두께가 같게). 길이(m)를 돌려줍니다.
    private static float PlaceCameraRibbon(Transform t, Vector3 a, Vector3 b, float width, Vector3 camPos)
    {
        Vector3 d = b - a;
        float length = d.magnitude;
        if (length < 0.0001f) { t.localScale = Vector3.zero; return 0f; }
        d /= length;
        Vector3 toCam = camPos - (a + b) * 0.5f;
        Vector3 side = Vector3.Cross(d, toCam);
        if (side.sqrMagnitude < 0.000001f) side = Vector3.Cross(d, Vector3.up);
        if (side.sqrMagnitude < 0.000001f) side = Vector3.right;
        side.Normalize();
        // LookRotation의 오른쪽(X) = Cross(up, forward) -> up = Cross(d, side)로 주면 X가 side가 됩니다.
        t.position = a;
        t.rotation = Quaternion.LookRotation(d, Vector3.Cross(d, side));
        t.localScale = new Vector3(Mathf.Max(width, 0f), 1f, length);
        return length;
    }

    private void EnsureVisualObjects()
    {
        int count = Mathf.Max(1, beamCount);
        if (block == null) block = new MaterialPropertyBlock();
        if (quadMesh == null) quadMesh = BuildQuadMesh();
        if (flareMesh == null) flareMesh = BuildFlareMesh();
        if (telegraphMaterial == null) telegraphMaterial = BuildMaterial(telegraphColor, "BossLaserTelegraph_Material");
        if (beamMaterial == null) beamMaterial = BuildEffectMaterial(beamShader, "MobArchive/BossLaser", "BossLaserBeam_Material");
        if (flareMaterial == null) flareMaterial = BuildEffectMaterial(flareShader, "MobArchive/BossLaserFlare", "BossLaserFlare_Material");
        if (smokeMaterial == null) smokeMaterial = BuildEffectMaterial(smokeShader, "MobArchive/BossLaserSmoke", "BossLaserSmoke_Material");
        if (chargeOrbMaterial == null) chargeOrbMaterial = BuildEffectMaterial(flareShader, "MobArchive/BossLaserFlare", "BossLaserChargeOrb_Material");
        if (sparkMaterial == null) sparkMaterial = BuildEffectMaterial(sparkShader, "MobArchive/BossLaserSpark", "BossLaserSpark_Material");

        if (telegraphMesh == null)
        {
            telegraphMesh = new Mesh { name = "BossLaserTelegraphMesh" };
            telegraphMesh.MarkDynamic();
        }
        if (telegraphObject == null) telegraphObject = CreateMeshObject("BossLaserTelegraph", telegraphMesh, telegraphMaterial);
        while (beamObjects.Count < count) beamObjects.Add(CreateMeshObject("BossLaserBeam_" + beamObjects.Count, quadMesh, beamMaterial));
        while (muzzleFlares.Count < count) muzzleFlares.Add(CreateMeshObject("BossLaserMuzzleFlare_" + muzzleFlares.Count, flareMesh, flareMaterial));
        while (endFlares.Count < count) endFlares.Add(CreateMeshObject("BossLaserEndFlare_" + endFlares.Count, flareMesh, flareMaterial));
        if (chargeOrbs.Count == 0) chargeOrbs.Add(CreateMeshObject("BossLaserChargeOrb", flareMesh, chargeOrbMaterial));
        if (sparkMaterial != null && chargeSparkSystems.Count == 0) chargeSparkSystems.Add(CreateChargeSparkSystem("BossLaserChargeSparks"));
        if (smokeMaterial != null)
        {
            while (smokeSystems.Count < count) smokeSystems.Add(CreateSmokeSystem("BossLaserSmoke_" + smokeSystems.Count, false));
            if (dustSystem == null) dustSystem = CreateSmokeSystem("BossLaserBaseDust", true);
        }
    }

    // 셰이더 칸이 비었으면 이름으로 찾습니다(빌드에서 이름으로만 찾으려면 Always Included 필요 - 칸에 넣어 두는 쪽을 권장).
    private Material BuildEffectMaterial(Shader assigned, string shaderName, string materialName)
    {
        Shader shader = assigned != null ? assigned : Shader.Find(shaderName);
        if (shader == null)
        {
            if (!loggedMissingShader)
            {
                loggedMissingShader = true;
                Debug.LogWarning($"BossLaserBeam ({gameObject.name}): 셰이더 {shaderName}를 찾지 못해 그 부분 이펙트를 그리지 않습니다(판정은 그대로). 프리팹의 셰이더 칸을 채워 주세요.");
            }
            return null;
        }
        return new Material(shader) { name = materialName };
    }

    private GameObject CreateMeshObject(string objectName, Mesh mesh, Material material)
    {
        var go = new GameObject(objectName);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = material;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        go.SetActive(false);
        return go;
    }

    // 연기/먼지 파티클을 코드로 만듭니다(월드 공간 - 이미 나온 연기는 레이저가 돌아도 제자리에서 흩어짐).
    private ParticleSystem CreateSmokeSystem(string objectName, bool isDust)
    {
        var go = new GameObject(objectName);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.playOnAwake = false;
        main.loop = true;
        main.duration = 1f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = isDust ? 160 : 120;
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = isDust ? 0f : -0.02f;

        var emission = ps.emission;
        emission.rateOverTime = 0f;

        var shape = ps.shape;
        shape.enabled = true;
        if (isDust)
        {
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radiusThickness = 0f; // 원 둘레에서 바깥으로
            shape.arc = 360f;
            var limit = ps.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.drag = dustDrag;
        }
        else
        {
            shape.shapeType = ParticleSystemShapeType.SingleSidedEdge; // 선 위에서 위(+Y)로
        }

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(grad);

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, isDust ? 1.8f : 1.5f));

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.sharedMaterial = smokeMaterial;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        // 셰이더가 파티클마다 고정 난수(TEXCOORD0.z)로 뭉치 모양을 정합니다.
        r.SetActiveVertexStreams(new List<ParticleSystemVertexStream>
        {
            ParticleSystemVertexStream.Position,
            ParticleSystemVertexStream.Color,
            ParticleSystemVertexStream.UV,
            ParticleSystemVertexStream.StableRandomX,
        });
        return ps;
    }

    // 차지 빛 입자: 차지 중심을 둘러싼 구 표면에서 생겨 중심으로 빨려 들어갑니다(로컬 공간 - 중심을 따라감).
    private ParticleSystem CreateChargeSparkSystem(string objectName)
    {
        var go = new GameObject(objectName);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.playOnAwake = false;
        main.loop = true;
        main.duration = 1f;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = 120;
        main.gravityModifier = 0f;

        var emission = ps.emission;
        emission.rateOverTime = 0f;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radiusThickness = 0f; // 구 표면에서만

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(1f, 0.85f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(grad);

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.35f));

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Stretch; // 날아가는 방향으로 살짝 늘어난 빛줄기
        r.lengthScale = 2.2f;
        r.velocityScale = 0.05f;
        r.sharedMaterial = sparkMaterial;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return ps;
    }

    private static void SetObjectsActive(List<GameObject> list, bool on)
    {
        foreach (var go in list) if (go != null && go.activeSelf != on) go.SetActive(on);
    }

    private static Mesh BuildFlareMesh()
    {
        var mesh = new Mesh { name = "BossLaserFlareQuad" };
        mesh.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
            new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f),
        };
        mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
        mesh.triangles = new[] { 0, 2, 1, 1, 2, 3 };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Mesh BuildQuadMesh()
    {
        var mesh = new Mesh { name = "BossLaserQuad" };
        mesh.vertices = new[]
        {
            new Vector3(-0.5f, 0f, 0f), new Vector3(0.5f, 0f, 0f),
            new Vector3(-0.5f, 0f, 1f), new Vector3(0.5f, 0f, 1f),
        };
        mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
        mesh.triangles = new[] { 0, 2, 1, 1, 2, 3 };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    // 플레이어 조준선과 같은 방식의 반투명 Unlit 재질(AimTrajectoryIndicator.BuildMaterial과 같은 셰이더 순서).
    private static Material BuildMaterial(Color color, string materialName)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
            ?? Shader.Find("Unlit/Transparent")
            ?? Shader.Find("Sprites/Default");
        var mat = new Material(shader) { name = materialName };
        if (mat.HasProperty("_Surface"))
        {
            mat.SetFloat("_Surface", 1f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        }
        if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        mat.color = color;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        return mat;
    }
}

// 조사 중인 보스 레이저 목록입니다. 플레이어 총알(Projectile)과 수류탄(Grenade)이 "이번 프레임 이동 경로가 레이저에 걸리나"를 여기에 묻습니다.
// 판정은 위에서 내려다본 평면(XZ) 기준(높이와 상관없는 벽). 조사 중인 레이저가 없으면 AnyActive가 false라 다른 코드는 아무 일도 하지 않습니다.
public static class BossLaserBlockers
{
    private static readonly List<BossLaserBeam> active = new List<BossLaserBeam>();

    // 에디터에서 도메인 리로드를 끄고 Play해도 지난 판의 목록이 남지 않게 비웁니다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => active.Clear();

    public static bool AnyActive
    {
        get
        {
            for (int i = active.Count - 1; i >= 0; i--)
            {
                if (active[i] == null) active.RemoveAt(i);
            }
            return active.Count > 0;
        }
    }

    public static void Register(BossLaserBeam beam)
    {
        if (beam != null && !active.Contains(beam)) active.Add(beam);
    }

    public static void Unregister(BossLaserBeam beam)
    {
        active.Remove(beam);
    }

    public static bool IsTouching(BossLaserBeam beam, Vector3 point, float radius)
    {
        return beam != null && beam.ContainsPoint(point, radius);
    }

    // from -> to 이동 경로(XZ)가 조사 중인 레이저에 처음 닿는 지점. 닿지 않으면 false.
    // 경로를 레이저 반폭의 절반 간격으로 나눠 앞에서부터 확인합니다(총알/수류탄은 한 프레임 이동이 짧아 몇 번이면 끝남).
    public static bool TryGetFirstHit(Vector3 from, Vector3 to, float radius, out Vector3 hitPoint)
    {
        hitPoint = to;
        if (!AnyActive) return false;

        float minHalf = float.MaxValue;
        foreach (var beam in active) minHalf = Mathf.Min(minHalf, beam.HitHalfWidth);
        float spacing = Mathf.Max(0.02f, minHalf * 0.5f);

        Vector3 delta = to - from;
        delta.y = 0f;
        int steps = Mathf.Clamp(Mathf.CeilToInt(delta.magnitude / spacing), 1, 64);
        for (int s = 0; s <= steps; s++)
        {
            Vector3 p = Vector3.Lerp(from, to, (float)s / steps);
            foreach (var beam in active)
            {
                if (beam.ContainsPoint(p, radius))
                {
                    hitPoint = p;
                    return true;
                }
            }
        }
        return false;
    }
}
