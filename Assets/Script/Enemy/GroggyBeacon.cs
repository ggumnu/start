using System;
using System.Collections.Generic;
using UnityEngine;

// [3레벨 보스 1페이즈] 그로기 비콘입니다. BossLv3AIController가 레이저 조사 뒤 방 모서리 세 곳에 하나씩 내려보내고(종류별 하나씩),
// 셋을 모두 해제하면 보스 기믹이 풀립니다. 프리팹에 미리 붙이는 컴포넌트가 아니라 보스가 비콘 모델로 만들 때 붙입니다.
// [종류별 해제 조건 - 형빈 확정]
//   수류탄 비콘 = 플레이어 수류탄 폭발(PlayerGrenadeThrower.OnGrenadeExploded - 118번 포격, 108번, 115번, 92번 등 수류탄 취급 폭발 포함)
//                 범위에 걸리면 해제(특수방 잠금과 같은 바닥 평면 판정)
//   해킹 비콘   = 해킹 범위가 펼쳐질 때(PlayerHackTool.OnAnyHackAreaApplied - 직접 사용, 59번 전개, 66번 수류탄 해킹) 범위에 걸리면 해제
//   총알 비콘   = 플레이어 총알 N발(피해량 무관, 한 발 = 한 번 - 관통/튕김/분열탄이 같은 비콘을 다시 맞혀도 안 셈)
// 수류탄/해킹 비콘에 닿은 총알은 막히고(소멸) "MISS"가 뜹니다. 어느 비콘이든 총알이 닿으면 "적에 닿았을 때" 아이템 효과(4번 폭발,
// 5번 분열, 69번 튕김 등)는 일어나지만 적이 아니므로 피해/명중 알림은 없습니다(Projectile.HitGroggyBeacon). 63번 미사일은 적만 노려 통과합니다.
// [모양] 위에서 로켓 착지처럼 감속하며 바닥 바로 위(Hover Height)까지 내려와(아래로 뿜는 연기 + 바닥에서 퍼지는 먼지) 잠깐 흔들리며 체공하다가,
// 연기를 끄고 무거운 물체처럼 "텅!" 떨어집니다(착지음 + 바닥 둘레로 먼지가 확 퍼짐 - 형빈 확정). 보스를 바라보고 내려옵니다. 떨어진 뒤부터 몸이 단단해집니다
// (플레이어가 통과할 수 없음, 레이저는 통과 - 엄폐물 아님). 위에 종류 아이콘이 뜨고, 총알 비콘은 남은 발 수만큼 총알 아이콘이 줄어듭니다.
// [발광/아이콘 - 형빈 확정] 모델의 발광 부품(Glow Part Name, 기본 Object7867)만 내려오는 동안 꺼진 듯 어두운 회색이고, 착지 후 종류별 색
// (수류탄 = 초록, 해킹 = 파랑, 총알 = 빨강)으로 서서히 켜진 뒤 은은하게 맥동합니다. 기믹 아이콘과 그 아래 위아래로 움직이는 역삼각형(발광색)은
// 맥동이 시작될 때 나타납니다. 총알이 닿으면 어느 비콘이든 흰색으로 잠깐 깜빡입니다(적과 같은 피격 깜빡임).
// [테두리 - 형빈 확정] 87번 아군 하늘색 테두리처럼 비콘 몸 둘레(MobArchive/BeaconOutline)와 아이콘/역삼각형 둘레(MobArchive/SpriteGlowOutline)에
// 종류별 색 테두리가 빛납니다. 몸 테두리는 각진 모서리에서 끊기지 않게 "부드럽게 맞춘 법선"으로 부풀립니다(메시마다 한 번 계산 - FBX Read/Write 필요).
// 아이콘 테두리는 같은 그림을 둘레 여러 방향으로 조금씩 밀어 뒤에 깔아 그립니다(스프라이트 여백과 상관없이 잘리지 않음).
// 몸 테두리는 발광이 켜질 때 함께 나타나 같이 맥동하고, 해제될 때 흰 빛 사라짐 전에 떼어 냅니다.
// [해제] 적 처치와 같은 연쇄 폭파 + 흰 빛으로 물들며 사라짐(DissolveAndVanish). 설정은 보스 Inspector의 Groggy Beacon Settings.
// [자동 조준 아이템의 목표 - 형빈 확정] 비콘은 적이 아니지만, 해제할 수 있는 아이템은 비콘도 노립니다(Active 목록으로 찾음):
//   108번 폭죽 포대 = 수류탄 비콘이 남아 있으면 보스보다 먼저 쏨 / 77번 유도 수류탄 = 보스와 수류탄 비콘 중 가까운 쪽으로 굴러감
//   63번 유도 미사일 = 보스와 함께 총알 비콘도 목표(한 발 = 한 번) / 69번 튕김 = 가장 가까운 적 또는 총알 비콘으로 튕김
//   118번 포격 = 비콘에 맞은 총알도 그 자리에 포격(Projectile.OnPlayerBulletHitBeacon)
public class GroggyBeacon : MonoBehaviour
{
    public enum BeaconType { Grenade, Hack, Bullet }

    private enum Phase { Descending, Hovering, Slamming, Landed, Broken }

    public BeaconType Type { get; private set; }
    public bool IsBroken => phase == Phase.Broken;
    // 내려앉은 뒤 해제 전까지만 총알을 받습니다(내려오는 중에는 콜라이더도 꺼져 있음).
    public bool AcceptsBullets => phase == Phase.Landed;
    public int RemainingHits => remainingHits;
    // 내려앉아 있고 해제 전이라 아이템이 노릴 수 있는 상태입니다.
    public bool IsTargetable => phase == Phase.Landed && isActiveAndEnabled;
    // 노릴 때 겨누는 몸 중심(월드)입니다.
    public Vector3 AimPoint => transform.TransformPoint(localBounds.center);

    // 지금 켜져 있는 비콘들입니다 - 내려오는 중/해제된 것도 들어 있으니 IsTargetable로 거르세요.
    private static readonly List<GroggyBeacon> active = new List<GroggyBeacon>();
    public static IReadOnlyList<GroggyBeacon> Active => active;

    private Phase phase = Phase.Descending;
    private GroggyBeaconSettings settings;
    private Action<GroggyBeacon> onBroken;
    private Material smokeMaterial;

    private Vector3 landPosition;
    private float descendElapsed;
    private int remainingHits;

    private readonly List<Collider> bodyColliders = new List<Collider>();
    private Bounds localBounds;   // 루트 기준 몸 크기(아이콘 높이, 연기 위치)

    private Transform iconRoot;
    private readonly List<SpriteRenderer> icons = new List<SpriteRenderer>();
    private Camera cachedCamera;

    private ParticleSystem thrusterSmoke;
    private ParticleSystem groundDust;
    private ParticleSystem impactDust;
    private float phaseTimer;
    private GameObject booster;   // 하강/체공 중 비콘 밑 로켓 불꽃(1레벨 보스 전방위 탄환 외형)

    // 피격 깜빡임(적이 총알에 맞을 때처럼 흰색 -> 원래 색). 처음 맞을 때 렌더러 재질을 복사해 씁니다.
    private class TintTarget
    {
        public Material material;
        public bool hasBase, hasColor, hasEmission;
        public Color baseColor, color, emission;
        public bool isGlowPart; // 발광 부품(Object7867) - 원래 색 대신 발광 상태 색이 기준
    }
    private readonly List<TintTarget> tintTargets = new List<TintTarget>();
    private bool tintTargetsReady;
    private float flashTimer;
    private bool flashWasActive;

    // 발광: 착지 후 지난 시간(꺼진 색 -> 종류 색, 다 켜지면 맥동 + 아이콘 등장)
    private float landedTime;
    private bool iconsShown;
    private float iconShowTimer;
    private Transform triangle;
    private float triangleBaseY;
    private static Sprite triangleSprite;

    // 테두리: 몸(렌더러 재질 목록 끝에 덧붙인 AllyOutline 복사본) / 아이콘(이 비콘 전용 SpriteGlowOutline 재질)
    private Material modelOutline;
    private readonly List<Renderer> outlinedRenderers = new List<Renderer>();
    private Material iconOutlineMaterial;
    private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
    private static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");
    // 원본 메시 -> 부드러운 법선(UV3)을 넣은 복사본(비콘끼리 같이 씀). 각진 모서리에서 테두리가 갈라지지 않게 합니다.
    private static readonly Dictionary<Mesh, Mesh> smoothOutlineMeshes = new Dictionary<Mesh, Mesh>();
    private static bool warnedUnreadableMesh;

    private PlayerGrenadeThrower boundThrower;

    private static Sprite placeholderSprite;

    // 보스가 비콘 모델을 만든 직후 부릅니다. root = 비어 있는 루트(이 컴포넌트가 붙은 오브젝트), 모델은 그 자식으로 이미 들어 있어야 합니다.
    // facing = 착지 후 바라볼 방향(보스 쪽, 모델 정면 보정 포함). 몸 크기는 회전 없이 잰 뒤 이 방향으로 돌립니다.
    // booster = 비콘 밑 로켓 불꽃 외형(없으면 불꽃 없음 - BossLv3AIController가 1레벨 보스 전방위 탄환 값으로 만들어 줌).
    public void Setup(BeaconType type, Vector3 landingPosition, Quaternion facing, GroggyBeaconSettings beaconSettings, Material sharedSmokeMaterial,
                      Action<GroggyBeacon> brokenCallback, BeaconBoosterVisual boosterVisual = null)
    {
        Type = type;
        settings = beaconSettings ?? new GroggyBeaconSettings();
        smokeMaterial = sharedSmokeMaterial;
        onBroken = brokenCallback;
        landPosition = landingPosition;
        remainingHits = Mathf.Max(1, settings.bulletHitsRequired);

        // 몸 크기는 루트가 회전 없이 착지 자리에 있을 때 잽니다(월드 경계 - 루트 위치 = 루트 기준 경계).
        transform.SetPositionAndRotation(landPosition, Quaternion.identity);
        CollectBodyColliders();
        localBounds = MeasureLocalBounds();
        SetBodyCollidersEnabled(false);

        // 루트 기준 경계(localBounds)는 회전과 무관하게 루트 로컬 좌표라 그대로 쓰고, 월드가 필요할 때 TransformPoint로 바꿉니다.
        transform.rotation = facing;

        BuildIcons();
        BuildSmoke();
        if (settings.descendTime > 0f && settings.dropHeight > 0f) BuildBooster(boosterVisual);
        EnsureTintTargets();   // 발광 부품을 처음부터 꺼진 색으로
        ApplyModelOutline();   // 피격/발광용 재질 복사가 끝난 뒤 테두리를 덧붙임
        UpdateBodyColors();

        descendElapsed = 0f;
        phaseTimer = 0f;
        phase = Phase.Descending;
        transform.position = landPosition + Vector3.up * Mathf.Max(0f, settings.dropHeight);
        if (settings.descendTime <= 0f || settings.dropHeight <= 0f) Land();
    }

    void OnEnable()
    {
        if (!active.Contains(this)) active.Add(this);
        PlayerHackTool.OnAnyHackAreaApplied += HandleHackArea;
        BindGrenadeThrower();
    }

    void OnDisable()
    {
        active.Remove(this);
        PlayerHackTool.OnAnyHackAreaApplied -= HandleHackArea;
        if (boundThrower != null) boundThrower.OnGrenadeExploded -= HandleGrenadeExploded;
        boundThrower = null;
    }

    void Update()
    {
        UpdateHitFlash();
        UpdateGlowAndIcons();
        UpdateBodyColors();
        if (boundThrower == null) BindGrenadeThrower();
        if (phase == Phase.Descending || phase == Phase.Hovering || phase == Phase.Slamming) UpdateDescent();
    }

    void LateUpdate()
    {
        // 아이콘은 늘 카메라를 정면으로 봅니다(역삼각형은 화면 기준 위아래로 움직임).
        if (iconRoot == null) return;
        if (cachedCamera == null || !cachedCamera.isActiveAndEnabled) cachedCamera = Camera.main;
        if (cachedCamera != null) iconRoot.rotation = cachedCamera.transform.rotation;
    }

    // ---------- 발광 / 아이콘 등장 ----------

    private float GlowRise01 => phase == Phase.Landed || phase == Phase.Broken
        ? Mathf.Clamp01(landedTime / Mathf.Max(settings.glowRiseTime, 0.01f)) : 0f;

    private void UpdateGlowAndIcons()
    {
        if (phase != Phase.Landed) return;
        landedTime += Time.deltaTime;

        // 다 켜지면(맥동 시작) 아이콘 + 역삼각형 등장 - 살짝 커지며 나타남.
        if (!iconsShown && landedTime >= settings.glowRiseTime) ShowIcons();
        if (iconsShown && iconRoot != null)
        {
            iconShowTimer += Time.deltaTime;
            float k = Mathf.Clamp01(iconShowTimer / Mathf.Max(settings.iconAppearTime, 0.01f));
            float pop = k < 1f ? Mathf.Sin(k * Mathf.PI * 0.5f) * (1f + 0.15f * Mathf.Sin(k * Mathf.PI)) : 1f;
            iconRoot.localScale = Vector3.one * pop;
        }
        if (triangle != null)
        {
            float bob = Mathf.Sin(landedTime * settings.triangleBobSpeed * Mathf.PI * 2f) * settings.triangleBobAmplitude;
            var lp = triangle.localPosition;
            triangle.localPosition = new Vector3(lp.x, triangleBaseY + bob, lp.z);
        }
    }

    private void ShowIcons()
    {
        iconsShown = true;
        iconShowTimer = 0f;
        if (iconRoot == null) return;
        iconRoot.localScale = Vector3.zero;
        iconRoot.gameObject.SetActive(true);
    }

    // 종류별 발광색(HDR 아님 - 세기는 Glow Intensity로).
    private Color TypeGlowColor()
    {
        switch (Type)
        {
            case BeaconType.Grenade: return settings.grenadeGlowColor;
            case BeaconType.Hack: return settings.hackGlowColor;
            default: return settings.bulletGlowColor;
        }
    }

    // 발광 부품의 지금 기준 색(꺼진 색 -> 켜진 색, 켜진 뒤 발광 세기가 맥동).
    // 맥동 밝기 배율(다 켜지기 전 1, 켜진 뒤 1 -> 1 - Pulse Depth -> 1).
    private float CurrentPulse()
    {
        if (GlowRise01 < 1f || settings.pulseDepth <= 0f) return 1f;
        float t = landedTime - settings.glowRiseTime;
        float wave = 0.5f - 0.5f * Mathf.Cos(t * settings.pulseSpeed * Mathf.PI * 2f); // 0 -> 1 -> 0
        return 1f - Mathf.Clamp01(settings.pulseDepth) * wave;
    }

    private void CurrentGlowColors(out Color baseColor, out Color emission)
    {
        float rise = GlowRise01;
        baseColor = Color.Lerp(settings.inactiveColor, settings.activeBaseColor, rise);
        float pulse = CurrentPulse();
        Color glow = TypeGlowColor() * (settings.glowIntensity * pulse);
        glow.a = 1f;
        emission = Color.Lerp(Color.black, glow, rise);
    }

    // ---------- 하강 ----------

    // 1) 하강: Drop Height -> Hover Height를 로켓 착지처럼 감속(3제곱 이징)  2) 체공: Hover Time 동안 살짝 흔들림
    // 3) 낙하: 연기를 끄고 Slam Time 동안 가속하며(제곱) 바닥까지 -> 착지.
    private void UpdateDescent()
    {
        float drop = Mathf.Max(0f, settings.dropHeight);
        float hover = Mathf.Clamp(settings.hoverHeight, 0f, drop);
        float height;

        if (phase == Phase.Descending)
        {
            descendElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(descendElapsed / Mathf.Max(settings.descendTime, 0.01f));
            float remain = 1f - t;
            height = hover + (drop - hover) * remain * remain * remain;
            if (t >= 1f)
            {
                phase = Phase.Hovering;
                phaseTimer = 0f;
            }
        }
        else if (phase == Phase.Hovering)
        {
            phaseTimer += Time.deltaTime;
            // 체공 중 위아래로 살짝 흔들림(처음과 끝은 0이라 하강/낙하와 이어짐).
            float bob = Mathf.Sin(phaseTimer * settings.hoverBobSpeed * Mathf.PI * 2f) * settings.hoverBobAmplitude;
            float envelope = Mathf.Sin(Mathf.Clamp01(phaseTimer / Mathf.Max(settings.hoverTime, 0.01f)) * Mathf.PI);
            height = hover + bob * envelope;
            if (phaseTimer >= settings.hoverTime)
            {
                BeginSlam();
                if (phase == Phase.Landed) return;
            }
        }
        else
        {
            phaseTimer += Time.deltaTime;
            float t = Mathf.Clamp01(phaseTimer / Mathf.Max(settings.slamTime, 0.01f));
            height = hover * (1f - t * t); // 가속하며 떨어짐
            if (t >= 1f)
            {
                Land();
                return;
            }
        }

        transform.position = landPosition + Vector3.up * height;
        UpdateDescentSmoke(height);
    }

    // 체공 끝 - 아래로 뿜던 연기를 끄고(엔진이 꺼진 듯) 떨어지기 시작합니다.
    private void BeginSlam()
    {
        phase = Phase.Slamming;
        phaseTimer = 0f;
        if (settings.slamTime <= 0f || settings.hoverHeight <= 0f) { Land(); return; }
        if (thrusterSmoke != null) thrusterSmoke.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        RemoveBooster();
    }

    // 비콘 밑에 위(+Z)를 향한 강화 탄환 외형을 붙여, 불꽃과 꼬리가 아래로 뿜어지게 합니다(고리는 BossLv3AIController가 꺼서 줌).
    private void BuildBooster(BeaconBoosterVisual visual)
    {
        if (visual == null || visual.settings == null) return;
        try
        {
            booster = new GameObject("BeaconBooster");
            booster.transform.SetParent(transform, false);
            booster.transform.localPosition = new Vector3(localBounds.center.x, localBounds.min.y - settings.boosterOffset, localBounds.center.z);
            booster.transform.rotation = Quaternion.LookRotation(Vector3.up, transform.forward);
            booster.transform.localScale = Vector3.one * Mathf.Max(0.01f, visual.hostScale);
            if (ChargedBulletVisual.AttachBooster(booster, Mathf.Max(1, visual.level), visual.settings, visual.tailLength) == null)
            {
                Destroy(booster);
                booster = null;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"GroggyBeacon: 부스터 생성 중 오류가 발생해 부스터 없이 내려옵니다: {e}");
            if (booster != null) Destroy(booster);
            booster = null;
        }
    }

    private void RemoveBooster()
    {
        if (booster != null) DetachAndDestroy(booster);
        booster = null;
    }

    private void Land()
    {
        transform.position = landPosition;
        phase = Phase.Landed;
        SetBodyCollidersEnabled(true);
        PushPlayerOut();

        RemoveBooster();
        // "텅!" - 착지음 + 바닥 둘레로 먼지가 확 퍼짐(빠르게 퍼지다 금방 느려짐). 뿜던 연기/바닥 먼지는 멈춥니다(이미 나온 것은 흩어짐).
        if (settings.playLandingSound) GameAudio.Play(settings.landingSound);
        if (thrusterSmoke != null) thrusterSmoke.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        if (groundDust != null)
        {
            var emission = groundDust.emission;
            emission.rateOverTime = 0f;
            groundDust.Emit(Mathf.Max(0, settings.landingDustBurst));
            groundDust.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
        if (impactDust != null)
        {
            impactDust.transform.position = new Vector3(landPosition.x, landPosition.y + 0.08f, landPosition.z);
            impactDust.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            impactDust.Play();
            impactDust.Emit(Mathf.Max(0, settings.impactDustBurst));
            impactDust.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    // 착지 자리에 플레이어가 서 있으면 몸에 끼지 않게 바깥으로 밀어냅니다.
    private void PushPlayerOut()
    {
        var health = FindFirstObjectByType<PlayerHealth>();
        if (health == null) return;
        var controller = health.GetComponent<CharacterController>();
        if (controller == null) return;

        Transform player = health.transform;
        for (int i = 0; i < bodyColliders.Count; i++)
        {
            Collider body = bodyColliders[i];
            if (body == null || !body.enabled) continue;
            if (!Physics.ComputePenetration(controller, player.position, player.rotation,
                                            body, body.transform.position, body.transform.rotation,
                                            out Vector3 direction, out float distance)) continue;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) continue;
            // CharacterController는 켜진 채로 위치를 바꾸면 되돌릴 수 있어 잠깐 끄고 옮깁니다.
            controller.enabled = false;
            player.position += direction.normalized * (distance + 0.02f);
            controller.enabled = true;
        }
    }

    // ---------- 아이템 목표 ----------

    // from에서 바닥 평면으로 가장 가까운 몸(콜라이더 경계 상자) 위의 점입니다(높이는 from). 폭발 반경이 작아도 반드시 걸리는 착탄점.
    public Vector3 ClosestFlatPoint(Vector3 from)
    {
        Vector3 center = AimPoint;
        Vector3 best = new Vector3(center.x, from.y, center.z);
        float bestSqr = float.MaxValue;
        for (int i = 0; i < bodyColliders.Count; i++)
        {
            Collider c = bodyColliders[i];
            if (c == null || !c.enabled) continue;
            Bounds b = c.bounds;
            var p = new Vector3(Mathf.Clamp(from.x, b.min.x, b.max.x), from.y, Mathf.Clamp(from.z, b.min.z, b.max.z));
            float sqr = (p - from).sqrMagnitude;
            if (sqr < bestSqr) { bestSqr = sqr; best = p; }
        }
        return best;
    }

    // 지금 플레이어가 있는 방의 비콘인지(방 정보를 못 읽으면 true). 다른 방 비콘은 꺼져 있어 Active에 없지만 안전장치로 한 번 더 봅니다.
    public bool IsInPlayerRoom()
    {
        var playerRoom = RoomScope.CurrentPlayerRoom();
        if (playerRoom == null) return true;
        var room = RoomScope.RoomOf(transform.position);
        return room == null || ReferenceEquals(room, playerRoom);
    }

    // from에서 수평 거리가 가장 가까운, 노릴 수 있는 type 비콘입니다(플레이어가 있는 방만, filter로 더 거를 수 있음). 없으면 null.
    public static GroggyBeacon FindNearest(Vector3 from, BeaconType type, Func<GroggyBeacon, bool> filter = null)
    {
        GroggyBeacon best = null;
        float bestSqr = float.MaxValue;
        for (int i = 0; i < active.Count; i++)
        {
            GroggyBeacon b = active[i];
            if (b == null || b.Type != type || !b.IsTargetable || !b.IsInPlayerRoom()) continue;
            if (filter != null && !filter(b)) continue;
            Vector3 d = b.AimPoint - from;
            d.y = 0f;
            float sqr = d.sqrMagnitude;
            if (sqr < bestSqr) { bestSqr = sqr; best = b; }
        }
        return best;
    }

    // ---------- 해제 조건 ----------

    // 총알이 닿았을 때(Projectile.HitGroggyBeacon - 한 총알당 한 번만 불림). 총알 비콘은 남은 횟수가 줄고, 나머지는 MISS.
    public void HandleBulletHit()
    {
        if (phase != Phase.Landed) return;

        GameAudio.Play(SfxId.EnemyHit);
        BeginHitFlash(); // 어느 비콘이든(피해가 없는 수류탄/해킹 비콘도) 맞으면 흰색으로 잠깐 발광 - 형빈 확정
        if (Type != BeaconType.Bullet)
        {
            DamagePopupDisplay.ShowMissOn(transform);
            return;
        }

        remainingHits = Mathf.Max(0, remainingHits - 1);
        RefreshBulletIcons();
        if (remainingHits <= 0) Break();
    }

    // ---------- 피격 깜빡임 ----------

    private void BeginHitFlash()
    {
        if (settings == null || !settings.flashOnHit || settings.flashFadeDuration <= 0f) return;
        EnsureTintTargets();
        flashTimer = settings.flashFadeDuration;
        UpdateBodyColors();
    }

    private void UpdateHitFlash()
    {
        if (flashTimer > 0f) flashTimer -= Time.deltaTime;
    }

    // 몸 색을 칠합니다: 기준 색(보통 부품 = 원래 색, 발광 부품 = 발광 상태 색)에 피격 깜빡임(흰색 -> 기준 색)을 겹칩니다.
    // 보통 부품은 깜빡이는 동안(과 끝나는 순간 한 번)만, 발광 부품은 매 프레임 칠합니다.
    private void UpdateBodyColors()
    {
        if (!tintTargetsReady || settings == null || phase == Phase.Broken) return;

        UpdateModelOutline();

        bool flashing = flashTimer > 0f;
        bool paintAll = flashing || flashWasActive;
        flashWasActive = flashing;
        float t = flashing ? 1f - Mathf.Clamp01(flashTimer / Mathf.Max(settings.flashFadeDuration, 0.01f)) : 1f; // 0 = 방금 맞음
        Color hit = settings.flashColor;
        Color hitEmission = settings.flashColor * settings.flashEmissionBoost;
        CurrentGlowColors(out Color glowBase, out Color glowEmission);

        for (int i = 0; i < tintTargets.Count; i++)
        {
            TintTarget target = tintTargets[i];
            if (target.material == null || (!target.isGlowPart && !paintAll)) continue;

            Color baseColor = target.isGlowPart ? glowBase : target.baseColor;
            Color color = target.isGlowPart ? glowBase : target.color;
            Color emission = target.isGlowPart ? glowEmission : target.emission;
            if (target.hasBase) target.material.SetColor("_BaseColor", Color.Lerp(hit, baseColor, t));
            if (target.hasColor) target.material.SetColor("_Color", Color.Lerp(hit, color, t));
            if (target.hasEmission)
            {
                Color e = settings.flashUsesEmission ? Color.Lerp(hitEmission, emission, t) : emission;
                target.material.SetColor("_EmissionColor", e);
            }
        }
    }

    // 몸 렌더러(아이콘/연기/부스터 제외)의 재질을 이 비콘 전용으로 복사하고 원래 색을 기억합니다.
    private void EnsureTintTargets()
    {
        if (tintTargetsReady) return;
        tintTargetsReady = true;
        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            if (r == null || r is SpriteRenderer || r is ParticleSystemRenderer || r is LineRenderer || r is TrailRenderer) continue;
            if (booster != null && r.transform.IsChildOf(booster.transform)) continue;
            if (iconRoot != null && r.transform.IsChildOf(iconRoot)) continue;
            bool glowPart = IsGlowPart(r.transform);
            Material[] mats = r.materials; // 이 렌더러 전용 복사본
            foreach (var m in mats)
            {
                if (m == null) continue;
                var target = new TintTarget { material = m, isGlowPart = glowPart };
                target.hasBase = m.HasProperty("_BaseColor");
                target.hasColor = m.HasProperty("_Color");
                target.hasEmission = m.HasProperty("_EmissionColor");
                if (target.hasBase) target.baseColor = m.GetColor("_BaseColor");
                if (target.hasColor) target.color = m.GetColor("_Color");
                if (target.hasEmission)
                {
                    target.emission = m.GetColor("_EmissionColor");
                    m.EnableKeyword("_EMISSION");
                }
                tintTargets.Add(target);
            }
        }
    }

    // ---------- 테두리 ----------

    // 몸 렌더러(Mesh/Skinned)의 재질 목록 끝에 AllyOutline 복사본을 덧붙입니다(87번 아군 테두리와 같은 방식).
    private void ApplyModelOutline()
    {
        if (!settings.modelOutlineEnabled || modelOutline != null) return;
        Shader shader = settings.modelOutlineShader != null ? settings.modelOutlineShader : Shader.Find("MobArchive/BeaconOutline");
        if (shader == null) return;
        modelOutline = new Material(shader) { name = "BeaconOutline (Instance)" };

        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;
            if (booster != null && r.transform.IsChildOf(booster.transform)) continue;
            if (iconRoot != null && r.transform.IsChildOf(iconRoot)) continue;
            UseSmoothOutlineMesh(r);
            var mats = r.materials; // 이미 복사본이면 같은 것을 돌려줌(피격/발광용 재질 유지)
            var extended = new Material[mats.Length + 1];
            for (int i = 0; i < mats.Length; i++) extended[i] = mats[i];
            extended[mats.Length] = modelOutline;
            r.materials = extended;
            outlinedRenderers.Add(r);
        }
        UpdateModelOutline();
    }

    // 메시를 "부드러운 법선(UV3)"이 들어간 복사본으로 바꿉니다. 같은 자리에 있는 정점들(각진 모서리에서 법선만 다르게 쪼개진 정점)의
    // 법선을 평균 내서, 테두리가 모서리에서 벌어지지 않고 한 덩어리로 부풀게 합니다. 메시를 읽을 수 없으면(FBX Read/Write 꺼짐) 그대로 둡니다.
    private void UseSmoothOutlineMesh(Renderer r)
    {
        try
        {
            Mesh source = null;
            MeshFilter filter = null;
            var skinned = r as SkinnedMeshRenderer;
            if (skinned != null) source = skinned.sharedMesh;
            else
            {
                filter = r.GetComponent<MeshFilter>();
                if (filter != null) source = filter.sharedMesh;
            }
            if (source == null) return;
            if (!source.isReadable)
            {
                if (!warnedUnreadableMesh)
                {
                    warnedUnreadableMesh = true;
                    Debug.LogWarning($"GroggyBeacon: 비콘 모델 메시({source.name})를 읽을 수 없어 테두리가 각진 모서리에서 끊겨 보일 수 있습니다. " +
                                     "FBX의 Model 탭에서 Read/Write를 켜 주세요.");
                }
                return;
            }

            if (!smoothOutlineMeshes.TryGetValue(source, out Mesh smooth) || smooth == null)
            {
                smooth = BuildSmoothOutlineMesh(source);
                smoothOutlineMeshes[source] = smooth;
            }
            if (skinned != null) skinned.sharedMesh = smooth;
            else filter.sharedMesh = smooth;
        }
        catch (Exception e)
        {
            Debug.LogError($"GroggyBeacon: 테두리용 부드러운 법선 계산 중 오류가 발생해 원래 메시로 그립니다: {e}");
        }
    }

    private static Mesh BuildSmoothOutlineMesh(Mesh source)
    {
        Mesh copy = Instantiate(source);
        copy.name = source.name + "_BeaconOutline";
        Vector3[] vertices = copy.vertices;
        Vector3[] normals = copy.normals;
        if (normals == null || normals.Length != vertices.Length) return copy;

        // 같은 자리(0.1mm 단위로 맞춤)의 법선을 더해 평균
        var sums = new Dictionary<Vector3Int, Vector3>();
        var keys = new Vector3Int[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 v = vertices[i] * 10000f;
            var key = new Vector3Int(Mathf.RoundToInt(v.x), Mathf.RoundToInt(v.y), Mathf.RoundToInt(v.z));
            keys[i] = key;
            sums.TryGetValue(key, out Vector3 sum);
            sums[key] = sum + normals[i];
        }
        var smooth = new List<Vector3>(vertices.Length);
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 n = sums[keys[i]];
            smooth.Add(n.sqrMagnitude > 0.000001f ? n.normalized : normals[i]);
        }
        copy.SetUVs(3, smooth);
        return copy;
    }

    // 몸 테두리: 발광이 켜지는 만큼 두꺼워지고(내려오는 동안 0 = 안 보임), 색은 발광색 x 세기 x 맥동.
    private void UpdateModelOutline()
    {
        if (modelOutline == null) return;
        float rise = GlowRise01;
        Color c = TypeGlowColor() * (settings.modelOutlineIntensity * CurrentPulse());
        c.a = 1f;
        modelOutline.SetColor(OutlineColorId, c);
        modelOutline.SetFloat(OutlineWidthId, settings.modelOutlineWidth * rise);
    }

    // 해제/정리 때 몸 테두리를 뗍니다(흰 빛 사라짐에 테두리만 남지 않게 - 87번 아군과 같은 이유). 부드러운 법선 메시는 그대로 둬도 모양은 같습니다.
    private void RemoveModelOutline()
    {
        if (modelOutline == null) return;
        for (int i = 0; i < outlinedRenderers.Count; i++)
        {
            Renderer r = outlinedRenderers[i];
            if (r == null) continue;
            var mats = r.sharedMaterials;
            var kept = new List<Material>(mats.Length);
            foreach (var m in mats) if (m != modelOutline) kept.Add(m);
            r.sharedMaterials = kept.ToArray();
        }
        outlinedRenderers.Clear();
        Destroy(modelOutline);
        modelOutline = null;
    }

    // 아이콘/역삼각형 테두리 재질(이 비콘 전용 - 종류 색). 셰이더가 없으면 테두리 없이 기본 스프라이트 재질.
    private Material GetIconOutlineMaterial()
    {
        if (!settings.iconOutlineEnabled) return null;
        if (iconOutlineMaterial != null) return iconOutlineMaterial;
        Shader shader = settings.iconOutlineShader != null ? settings.iconOutlineShader : Shader.Find("MobArchive/SpriteGlowOutline");
        if (shader == null) return null;
        iconOutlineMaterial = new Material(shader) { name = "BeaconIconOutline (Instance)" };
        Color c = TypeGlowColor() * settings.iconOutlineIntensity;
        c.a = 1f;
        iconOutlineMaterial.SetColor(OutlineColorId, c);
        return iconOutlineMaterial;
    }

    // 스프라이트 테두리: 같은 그림을 둘레 Icon Outline Samples 방향으로 Icon Outline Width(월드)만큼 밀어 단색으로 뒤에 깝니다.
    // (그림 자체의 사각형 밖으로도 그려지므로 여백이 없는 스프라이트도 잘리지 않음.) worldScale = 그림 오브젝트의 크기 배율.
    private void AddSpriteOutline(GameObject owner, Sprite sprite, float worldScale)
    {
        Material material = GetIconOutlineMaterial();
        if (material == null || sprite == null || settings.iconOutlineWidth <= 0f) return;
        int samples = Mathf.Clamp(settings.iconOutlineSamples, 4, 32);
        float offset = settings.iconOutlineWidth / Mathf.Max(worldScale, 0.0001f);
        for (int k = 0; k < samples; k++)
        {
            float ang = k * Mathf.PI * 2f / samples;
            var go = new GameObject("Outline_" + k);
            go.transform.SetParent(owner.transform, false);
            go.transform.localPosition = new Vector3(Mathf.Cos(ang) * offset, Mathf.Sin(ang) * offset, 0.001f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sharedMaterial = material;
            sr.sortingOrder = settings.iconSortingOrder - 1;
        }
    }

    // 발광 부품: 이름이 Glow Part Name인 오브젝트(또는 그 자식)의 렌더러. FBX에서 메시 이름 뒤에 숫자가 붙는 경우도 있어 앞부분이 같으면 같은 부품으로 봅니다.
    private bool IsGlowPart(Transform t)
    {
        string partName = settings != null ? settings.glowPartName : null;
        if (string.IsNullOrEmpty(partName)) return false;
        for (Transform x = t; x != null && x != transform; x = x.parent)
        {
            if (x.name.StartsWith(partName, StringComparison.Ordinal)) return true;
        }
        return false;
    }

    void OnDestroy()
    {
        // 피격 깜빡임/발광/테두리용으로 복사한 재질을 정리합니다.
        if (modelOutline != null) Destroy(modelOutline);
        if (iconOutlineMaterial != null) Destroy(iconOutlineMaterial);
        for (int i = 0; i < tintTargets.Count; i++)
        {
            if (tintTargets[i].material != null) Destroy(tintTargets[i].material);
        }
        tintTargets.Clear();
    }

    private void HandleGrenadeExploded(Vector3 center, float radius)
    {
        if (Type != BeaconType.Grenade || phase != Phase.Landed) return;
        if (IsWithinFlatRange(center, radius)) Break();
    }

    private void HandleHackArea(Vector3 center, float radius)
    {
        if (Type != BeaconType.Hack || phase != Phase.Landed) return;
        if (IsWithinFlatRange(center, radius)) Break();
    }

    // 특수방 잠금과 같은 판정: 바닥 평면(XZ)에서 중심부터 몸 콜라이더 경계 상자의 가장 가까운 점까지 거리가 반경 이하.
    private bool IsWithinFlatRange(Vector3 center, float radius)
    {
        if (radius <= 0f) return false;
        var flat = new Vector2(center.x, center.z);
        bool any = false;
        for (int i = 0; i < bodyColliders.Count; i++)
        {
            Collider c = bodyColliders[i];
            if (c == null || !c.enabled) continue;
            any = true;
            if (FlatDistanceToBounds(c.bounds, flat) <= radius) return true;
        }
        if (any) return false;

        var worldBounds = new Bounds(AimPoint, localBounds.size);
        return FlatDistanceToBounds(worldBounds, flat) <= radius;
    }

    private static float FlatDistanceToBounds(Bounds b, Vector2 point)
    {
        float x = Mathf.Clamp(point.x, b.min.x, b.max.x);
        float z = Mathf.Clamp(point.y, b.min.z, b.max.z);
        return Vector2.Distance(point, new Vector2(x, z));
    }

    private void BindGrenadeThrower()
    {
        var health = FindFirstObjectByType<PlayerHealth>();
        if (health == null) return;
        var thrower = health.GetComponent<PlayerGrenadeThrower>();
        if (thrower == null || thrower == boundThrower) return;
        if (boundThrower != null) boundThrower.OnGrenadeExploded -= HandleGrenadeExploded;
        boundThrower = thrower;
        boundThrower.OnGrenadeExploded += HandleGrenadeExploded;
    }

    // ---------- 해제 ----------

    // 해제: 적 처치와 같은 연쇄 폭파 + 흰 빛으로 사라짐. 보스에게 알립니다.
    public void Break()
    {
        if (phase == Phase.Broken) return;
        phase = Phase.Broken;

        Vector3 center = AimPoint;
        CombatVfxSpawner.PlayDeathExplosionAt(center, Mathf.Max(0.1f, settings.breakExplosionScale));
        GameAudio.Play(SfxId.EnemyKill);

        RemoveIconsAndSmoke();
        DissolveAndVanish.Begin(gameObject, DissolveAndVanish.EndAction.Destroy);

        var callback = onBroken;
        onBroken = null;
        callback?.Invoke(this);
    }

    // 보스가 죽거나 방이 꺼질 때 보스가 부릅니다(해제로 치지 않음 - 보스에게 알리지 않음).
    // fadeOut = 흰 빛으로 사라짐(보스 사망), 아니면 바로 지움(방이 꺼짐). 이미 해제되어 사라지는 중이면 fadeOut일 때는 그대로 둡니다.
    public void Discard(bool fadeOut)
    {
        bool wasBroken = phase == Phase.Broken;
        phase = Phase.Broken;
        onBroken = null;
        RemoveIconsAndSmoke();
        if (!fadeOut || !gameObject.activeInHierarchy) Destroy(gameObject);
        else if (!wasBroken) DissolveAndVanish.Begin(gameObject, DissolveAndVanish.EndAction.Destroy);
    }

    // 아이콘/연기는 흰 빛 사라짐 대상이 아니라 바로 지웁니다. Destroy는 프레임 끝에 처리되므로 먼저 떼어내서
    // 같은 프레임에 시작하는 DissolveAndVanish가 이 렌더러들을 잡지 않게 합니다.
    private void RemoveIconsAndSmoke()
    {
        RemoveModelOutline();
        if (iconRoot != null) DetachAndDestroy(iconRoot.gameObject);
        if (thrusterSmoke != null) DetachAndDestroy(thrusterSmoke.gameObject);
        if (groundDust != null) DetachAndDestroy(groundDust.gameObject);
        if (impactDust != null) DetachAndDestroy(impactDust.gameObject);
        impactDust = null;
        RemoveBooster();
        iconRoot = null;
        triangle = null;
        icons.Clear();
        thrusterSmoke = null;
        groundDust = null;
    }

    private static void DetachAndDestroy(GameObject go)
    {
        go.SetActive(false);
        go.transform.SetParent(null, false);
        Destroy(go);
    }

    // ---------- 몸 ----------

    // 모델에 막는 콜라이더(트리거 아님)가 있으면 그것을 쓰고, 없으면 루트에 모델 크기만 한 상자 콜라이더를 붙입니다.
    private void CollectBodyColliders()
    {
        bodyColliders.Clear();
        foreach (var c in GetComponentsInChildren<Collider>(true))
        {
            if (c != null && !c.isTrigger) bodyColliders.Add(c);
        }
        if (bodyColliders.Count > 0) return;

        Bounds b = MeasureRendererLocalBounds(out bool found);
        if (!found) b = new Bounds(Vector3.up * 0.5f, Vector3.one);
        var box = gameObject.AddComponent<BoxCollider>();
        box.center = b.center;
        box.size = b.size;
        bodyColliders.Add(box);
    }

    private void SetBodyCollidersEnabled(bool enabled)
    {
        for (int i = 0; i < bodyColliders.Count; i++)
        {
            if (bodyColliders[i] != null) bodyColliders[i].enabled = enabled;
        }
    }

    private Bounds MeasureLocalBounds()
    {
        Bounds b = MeasureRendererLocalBounds(out bool found);
        if (found) return b;
        for (int i = 0; i < bodyColliders.Count; i++)
        {
            if (bodyColliders[i] == null) continue;
            Bounds wb = bodyColliders[i].bounds;
            var lb = new Bounds(wb.center - transform.position, wb.size);
            if (!found) { b = lb; found = true; }
            else b.Encapsulate(lb);
        }
        return found ? b : new Bounds(Vector3.up * 0.5f, Vector3.one);
    }

    // 렌더러들의 월드 경계를 루트 위치 기준으로 합칩니다(루트가 회전 없이 착지 자리에 있을 때 부름).
    private Bounds MeasureRendererLocalBounds(out bool found)
    {
        found = false;
        var b = new Bounds(Vector3.zero, Vector3.zero);
        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            if (r == null || r is ParticleSystemRenderer || r is SpriteRenderer) continue;
            var lb = new Bounds(r.bounds.center - transform.position, r.bounds.size);
            if (!found) { b = lb; found = true; }
            else b.Encapsulate(lb);
        }
        return b;
    }

    // ---------- 아이콘 ----------

    private void BuildIcons()
    {
        var go = new GameObject("BeaconIcons");
        iconRoot = go.transform;
        iconRoot.SetParent(transform, false);
        // 기준점 = 역삼각형 자리(비콘 꼭대기 + Icon Height). 아이콘은 그 위에 놓입니다.
        iconRoot.localPosition = new Vector3(localBounds.center.x, localBounds.max.y + settings.iconHeight, localBounds.center.z);

        Sprite sprite = GetIconSprite(out Color tint);
        int count = Type == BeaconType.Bullet ? Mathf.Max(1, settings.bulletHitsRequired) : 1;
        float size = Type == BeaconType.Bullet ? settings.bulletIconSize : settings.iconSize;
        float spacing = settings.bulletIconSpacing;
        float triangleSize = settings.triangleEnabled ? settings.triangleSize : 0f;
        float iconY = triangleSize * 0.5f + settings.iconGapAboveTriangle + size * 0.5f;

        if (settings.triangleEnabled)
        {
            var triGo = new GameObject("GimmickArrow");
            triangle = triGo.transform;
            triangle.SetParent(iconRoot, false);
            triangleBaseY = 0f;
            triangle.localPosition = Vector3.zero;
            var tsr = triGo.AddComponent<SpriteRenderer>();
            tsr.sprite = GetTriangleSprite();
            Color tc = TypeGlowColor();
            tc.a = 1f;
            tsr.color = tc;
            tsr.sortingOrder = settings.iconSortingOrder;
            float tLargest = Mathf.Max(tsr.sprite.bounds.size.x, tsr.sprite.bounds.size.y);
            float tScale = tLargest > 0.0001f ? triangleSize / tLargest : triangleSize;
            triangle.localScale = new Vector3(tScale, tScale, tScale);
            AddSpriteOutline(triGo, tsr.sprite, tScale);
        }

        for (int i = 0; i < count; i++)
        {
            var iconGo = new GameObject(count > 1 ? $"Icon_{i}" : "Icon");
            iconGo.transform.SetParent(iconRoot, false);
            iconGo.transform.localPosition = new Vector3((i - (count - 1) * 0.5f) * spacing, iconY, 0f);
            var sr = iconGo.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = tint;
            sr.sortingOrder = settings.iconSortingOrder;
            float largest = Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);
            float scale = largest > 0.0001f ? size / largest : size;
            iconGo.transform.localScale = new Vector3(scale, scale, scale);
            AddSpriteOutline(iconGo, sprite, scale);
            icons.Add(sr);
        }
        RefreshBulletIcons();
        go.SetActive(false); // 착지 후 발광이 다 켜지면(맥동 시작) 나타남
    }

    // 위가 평평하고 아래로 뾰족한 역삼각형(흰색 - SpriteRenderer.color로 물들임), 가장자리는 부드럽게.
    private static Sprite GetTriangleSprite()
    {
        if (triangleSprite != null) return triangleSprite;
        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "GroggyBeaconTriangle", wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color32[size * size];
        const float top = 0.84f, bottom = 0.16f, halfTop = 0.38f; // 테두리가 잘리지 않게 둘레에 여백
        float aa = 1.5f / size;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size - 0.5f;
                float v = (y + 0.5f) / size;
                float half = halfTop * Mathf.Clamp01((v - bottom) / (top - bottom));
                float dSide = half - Mathf.Abs(u);       // 양옆 빗변 안쪽 거리(근사)
                float dTop = top - v;                     // 윗변 아래쪽 거리
                float dBottom = v - bottom;               // 뾰족한 끝 아래(그 밑으로 가는 선이 남지 않게)
                float d = Mathf.Min(Mathf.Min(dSide * 0.87f, dTop), dBottom);
                byte a = (byte)(Mathf.Clamp01(d / aa + 0.5f) * 255f);
                pixels[y * size + x] = new Color32(255, 255, 255, a);
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply();
        triangleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        triangleSprite.name = "GroggyBeaconTriangle";
        return triangleSprite;
    }

    // 총알 비콘: 남은 발 수만큼 왼쪽부터 보이고, 맞을 때마다 오른쪽부터 사라집니다.
    private void RefreshBulletIcons()
    {
        if (Type != BeaconType.Bullet) return;
        for (int i = 0; i < icons.Count; i++)
        {
            if (icons[i] != null) icons[i].gameObject.SetActive(i < remainingHits); // 테두리(자식)도 같이
        }
    }

    // 칸에 스프라이트가 없으면 종류별 색의 둥근 임시 아이콘을 씁니다(나중에 스프라이트를 넣으면 흰색 = 원래 색 그대로).
    private Sprite GetIconSprite(out Color tint)
    {
        Sprite assigned = null;
        Color placeholderColor = Color.white;
        switch (Type)
        {
            case BeaconType.Grenade: assigned = settings.grenadeIcon; placeholderColor = settings.grenadePlaceholderColor; break;
            case BeaconType.Hack: assigned = settings.hackIcon; placeholderColor = settings.hackPlaceholderColor; break;
            case BeaconType.Bullet: assigned = settings.bulletIcon; placeholderColor = settings.bulletPlaceholderColor; break;
        }
        if (assigned != null)
        {
            tint = Color.white;
            return assigned;
        }
        tint = placeholderColor;
        return GetPlaceholderSprite();
    }

    private static Sprite GetPlaceholderSprite()
    {
        if (placeholderSprite != null) return placeholderSprite;
        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "GroggyBeaconPlaceholderIcon", wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color32[size * size];
        float r = size * 0.4f; // 테두리가 잘리지 않게 둘레에 여백
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - size * 0.5f, dy = y + 0.5f - size * 0.5f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                // 흰 원 + 어두운 테두리(색은 SpriteRenderer.color로 입힘 - 테두리도 같이 물들지만 어둡게 남음).
                byte a = (byte)(Mathf.Clamp01(r - d + 0.5f) * 255f);
                byte v = d > r - 5f ? (byte)60 : (byte)255;
                pixels[y * size + x] = new Color32(v, v, v, a);
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply();
        placeholderSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        placeholderSprite.name = "GroggyBeaconPlaceholderIcon";
        return placeholderSprite;
    }

    // ---------- 연기 ----------

    // 하강 중 아래로 뿜는 연기(비콘 밑면을 따라 내려옴) + 바닥에서 사방으로 퍼지는 먼지(가까워질수록 많아짐).
    // 3레벨 보스 레이저 연기와 같은 셰이더(MobArchive/BossLaserSmoke)를 씁니다. 머티리얼이 없으면 연기 없이 내려옵니다.
    private void BuildSmoke()
    {
        if (smokeMaterial == null || settings.descendTime <= 0f || settings.dropHeight <= 0f) return;

        thrusterSmoke = CreateSmokeSystem("BeaconThrusterSmoke", false);
        thrusterSmoke.transform.SetParent(transform, false);
        thrusterSmoke.transform.localPosition = new Vector3(localBounds.center.x, localBounds.min.y, localBounds.center.z);
        thrusterSmoke.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // 원뿔이 아래(-Y)를 향하게
        var tShape = thrusterSmoke.shape;
        tShape.radius = Mathf.Max(0.02f, Mathf.Min(localBounds.extents.x, localBounds.extents.z) * 0.5f);
        ApplySmokeMain(thrusterSmoke, settings.thrusterLifetime, settings.thrusterSpeed, settings.thrusterSize, settings.thrusterColor);
        var tEmission = thrusterSmoke.emission;
        tEmission.rateOverTime = settings.thrusterRate;
        thrusterSmoke.Play();

        groundDust = CreateSmokeSystem("BeaconLandingDust", true);
        groundDust.transform.SetParent(transform, false);
        groundDust.transform.rotation = Quaternion.Euler(-90f, 0f, 0f); // 원(Circle) 방출 면을 바닥과 나란하게
        var dShape = groundDust.shape;
        dShape.radius = Mathf.Max(0.05f, Mathf.Max(localBounds.extents.x, localBounds.extents.z));
        ApplySmokeMain(groundDust, settings.dustLifetime, settings.dustSpeed, settings.dustSize, settings.dustColor);
        var dEmission = groundDust.emission;
        dEmission.rateOverTime = 0f;
        groundDust.Play();

        // 착지 충격 먼지: 같은 셰이더, 바닥 둘레에서 더 많이/빠르게 퍼지고 강하게 느려짐(착지 순간 한 번만).
        impactDust = CreateSmokeSystem("BeaconImpactDust", true);
        impactDust.transform.SetParent(transform, false);
        var iShape = impactDust.shape;
        iShape.radius = Mathf.Max(0.05f, Mathf.Max(localBounds.extents.x, localBounds.extents.z));
        var iMain = impactDust.main;
        iMain.maxParticles = Mathf.Max(iMain.maxParticles, settings.impactDustBurst + 10);
        ApplySmokeMain(impactDust, settings.impactDustLifetime, settings.impactDustSpeed, settings.impactDustSize, settings.impactDustColor);
        var iLimit = impactDust.limitVelocityOverLifetime;
        iLimit.drag = settings.impactDustDrag;

        UpdateDescentSmoke(settings.dropHeight);
    }

    private void UpdateDescentSmoke(float height)
    {
        if (groundDust == null) return;
        // 먼지는 늘 착지 바닥 높이에 둡니다(루트는 내려오는 중).
        groundDust.transform.position = new Vector3(landPosition.x, landPosition.y + 0.08f, landPosition.z);
        groundDust.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);

        float start = Mathf.Max(settings.dustStartHeight, 0.01f);
        float closeness = Mathf.Clamp01(1f - height / start);
        var emission = groundDust.emission;
        emission.rateOverTime = settings.dustRate * closeness;
    }

    private static void ApplySmokeMain(ParticleSystem ps, Vector2 lifetime, Vector2 speed, Vector2 size, Color color)
    {
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(Mathf.Min(lifetime.x, lifetime.y), Mathf.Max(lifetime.x, lifetime.y));
        main.startSpeed = new ParticleSystem.MinMaxCurve(Mathf.Min(speed.x, speed.y), Mathf.Max(speed.x, speed.y));
        main.startSize = new ParticleSystem.MinMaxCurve(Mathf.Min(size.x, size.y), Mathf.Max(size.x, size.y));
        main.startColor = color;
    }

    // 보스 레이저 연기(BossLaserBeam.CreateSmokeSystem)와 같은 구성입니다(월드 공간 - 이미 나온 연기는 그 자리에서 흩어짐).
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
        main.maxParticles = isDust ? 120 : 80;
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = isDust ? 0f : -0.05f;

        var emission = ps.emission;
        emission.rateOverTime = 0f;

        var shape = ps.shape;
        shape.enabled = true;
        if (isDust)
        {
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radiusThickness = 0f; // 원 둘레에서 바깥으로
            shape.arc = 360f;
        }
        else
        {
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = settings.thrusterConeAngle;
        }
        var limit = ps.limitVelocityOverLifetime;
        limit.enabled = true;
        limit.drag = isDust ? settings.dustDrag : settings.thrusterDrag;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(grad);

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, isDust ? 1.8f : 2f));

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
}

// 비콘 밑 로켓 불꽃 외형 - 1레벨 보스 전방위 탄환(EnemyRadialAttack 강화 외형) 값을 BossLv3AIController가 옮겨 담아 줍니다.
public class BeaconBoosterVisual
{
    public ChargedBulletVisual.Settings settings; // 고리를 끈 복사본
    public int level = 1;                         // 외형 세기(EnemyRadialAttack.chargedVisualLevel)
    public float hostScale = 0.4f;                // 외형 크기 = 탄환 크기(projectileScale) x Booster Scale
    public float tailLength = 3f;                 // 고정 꼬리 길이(총알 지름 단위)
}

// 보스 Inspector에서 고치는 그로기 비콘 설정입니다(BossLv3AIController.beaconSettings).
[Serializable]
public class GroggyBeaconSettings
{
    [Header("하강")]
    [Tooltip("비콘이 내려오기 시작하는 높이(착지 자리 위, 월드 단위).")]
    [Min(0f)] public float dropHeight = 8f;
    [Tooltip("Drop Height에서 Hover Height까지 내려오는 데 걸리는 시간(초). 가까워질수록 천천히 감속합니다.")]
    [Min(0f)] public float descendTime = 1.5f;
    [Tooltip("바닥 위 체공 높이(월드 단위). 여기서 잠깐 멈췄다가 떨어집니다.")]
    [Min(0f)] public float hoverHeight = 0.6f;
    [Tooltip("체공 시간(초).")]
    [Min(0f)] public float hoverTime = 0.35f;
    [Tooltip("체공 중 위아래 흔들림 폭(월드 단위).")]
    [Min(0f)] public float hoverBobAmplitude = 0.04f;
    [Tooltip("체공 중 흔들림 횟수(초당).")]
    [Min(0f)] public float hoverBobSpeed = 6f;
    [Tooltip("체공 높이에서 바닥까지 떨어지는 시간(초) - 짧을수록 무겁게 '텅!'.")]
    [Min(0f)] public float slamTime = 0.12f;
    public bool playLandingSound = true;
    [Tooltip("착지음(기본: 2레벨 보스 포격 폭발).")]
    public SfxId landingSound = SfxId.BossArtilleryExplode;

    [Header("총알 비콘")]
    [Tooltip("총알 비콘을 해제하는 데 필요한 플레이어 총알 수(피해량 무관).")]
    [Min(1)] public int bulletHitsRequired = 5;

    [Header("피격 깜빡임 (총알이 닿으면 적처럼 잠깐 발광 - 어느 비콘이든)")]
    public bool flashOnHit = true;
    public Color flashColor = Color.white;
    [Tooltip("피격 색에서 원래 색으로 돌아오는 시간(초).")]
    [Min(0f)] public float flashFadeDuration = 0.4f;
    public bool flashUsesEmission = true;
    [Min(0f)] public float flashEmissionBoost = 2f;

    [Header("해제")]
    [Tooltip("해제 때 연쇄 폭파 크기 = 일반 적 처치 폭발 x 이 값.")]
    [Min(0.1f)] public float breakExplosionScale = 1f;

    [Header("발광 (모델의 발광 부품만 - 내려올 때 꺼진 색, 착지 후 종류별 색으로 켜진 뒤 맥동)")]
    [Tooltip("발광시킬 부품(모델 안 오브젝트 이름 - 이 이름으로 시작하는 오브젝트와 그 자식).")]
    public string glowPartName = "Object7867";
    [Tooltip("내려오는 동안(꺼진 상태) 부품 색 - 텍스처 색에 곱해짐.")]
    public Color inactiveColor = new Color(0.22f, 0.22f, 0.24f, 1f);
    [Tooltip("켜진 뒤 부품 몸 색(텍스처 색에 곱해짐). 발광은 아래 색으로 더해짐.")]
    public Color activeBaseColor = new Color(0.45f, 0.45f, 0.45f, 1f);
    public Color grenadeGlowColor = new Color(0.25f, 1f, 0.3f, 1f);
    public Color hackGlowColor = new Color(0.2f, 0.55f, 1f, 1f);
    public Color bulletGlowColor = new Color(1f, 0.15f, 0.1f, 1f);
    [Tooltip("발광 세기(발광색 x 이 값).")]
    [Min(0f)] public float glowIntensity = 2.5f;
    [Tooltip("착지 후 꺼진 색에서 다 켜지기까지(초). 다 켜지면 맥동 + 아이콘 등장.")]
    [Min(0f)] public float glowRiseTime = 0.6f;
    [Tooltip("맥동 횟수(초당).")]
    [Min(0f)] public float pulseSpeed = 1.2f;
    [Tooltip("맥동 깊이(0 = 맥동 없음, 0.35 = 가장 어두울 때 65% 밝기).")]
    [Range(0f, 1f)] public float pulseDepth = 0.35f;

    [Header("테두리 (87번 아군 테두리처럼 종류별 색으로 빛남)")]
    public bool modelOutlineEnabled = true;
    [Tooltip("MobArchive/BeaconOutline (Assets/Shaders/BeaconOutline.shader). 비우면 이름으로 찾음(빌드에선 칸을 채워 두세요). " +
             "각진 모서리에서 끊기지 않으려면 비콘 FBX의 Read/Write를 켜 주세요.")]
    public Shader modelOutlineShader;
    [Tooltip("몸 테두리 두께(1080p 기준 대략 픽셀 - 87번 기본 2.5).")]
    [Range(0f, 10f)] public float modelOutlineWidth = 2.5f;
    [Tooltip("몸 테두리 밝기(발광색 x 이 값, 1보다 크면 빛 번짐).")]
    [Min(0f)] public float modelOutlineIntensity = 1.3f;
    public bool iconOutlineEnabled = true;
    [Tooltip("MobArchive/SpriteGlowOutline (Assets/Shaders/SpriteGlowOutline.shader). 비우면 이름으로 찾음(빌드에선 칸을 채워 두세요).")]
    public Shader iconOutlineShader;
    [Tooltip("아이콘/역삼각형 테두리 두께(월드 단위).")]
    [Min(0f)] public float iconOutlineWidth = 0.02f;
    [Tooltip("테두리를 그리는 방향 수(많을수록 둥글고 고르게).")]
    [Range(4, 32)] public int iconOutlineSamples = 16;
    [Tooltip("아이콘/역삼각형 테두리 밝기(발광색 x 이 값).")]
    [Min(0f)] public float iconOutlineIntensity = 1.6f;

    [Header("아이콘 (비우면 종류별 색의 둥근 임시 아이콘)")]
    public Sprite grenadeIcon;
    public Sprite hackIcon;
    [Tooltip("총알 비콘에 남은 발 수만큼 나란히 뜨는 총알 아이콘.")]
    public Sprite bulletIcon;
    [Tooltip("비콘 몸 꼭대기에서 역삼각형 중심까지 높이(월드 단위). 아이콘은 역삼각형 위에 놓입니다.")]
    public float iconHeight = 0.3f;
    [Tooltip("수류탄/해킹 아이콘 크기(월드 단위, 긴 쪽 기준).")]
    [Min(0.01f)] public float iconSize = 0.45f;
    [Tooltip("총알 아이콘 하나 크기(월드 단위, 긴 쪽 기준).")]
    [Min(0.01f)] public float bulletIconSize = 0.22f;
    [Tooltip("총알 아이콘 사이 간격(중심끼리, 월드 단위).")]
    [Min(0f)] public float bulletIconSpacing = 0.28f;
    [Tooltip("아이콘 그리는 순서(클수록 위).")]
    public int iconSortingOrder = 10;
    [Tooltip("아이콘 + 역삼각형이 나타나며 커지는 시간(초).")]
    [Min(0f)] public float iconAppearTime = 0.2f;
    [Tooltip("아이콘 아래 역삼각형(위가 평평하고 아래로 뾰족 - 발광색, 위아래로 움직임).")]
    public bool triangleEnabled = true;
    [Min(0.01f)] public float triangleSize = 0.2f;
    [Tooltip("역삼각형 위 끝과 아이콘 아래 끝 사이 간격(월드 단위).")]
    [Min(0f)] public float iconGapAboveTriangle = 0.06f;
    [Tooltip("역삼각형 위아래 움직임 폭(월드 단위).")]
    [Min(0f)] public float triangleBobAmplitude = 0.06f;
    [Tooltip("역삼각형 위아래 움직임 횟수(초당).")]
    [Min(0f)] public float triangleBobSpeed = 1.5f;
    public Color grenadePlaceholderColor = new Color(1f, 0.55f, 0.15f, 1f);
    public Color hackPlaceholderColor = new Color(0.3f, 0.9f, 1f, 1f);
    public Color bulletPlaceholderColor = new Color(1f, 0.9f, 0.3f, 1f);

    [Header("하강 연기 (셰이더는 보스의 Beacon Smoke Shader 칸)")]
    [Tooltip("비콘 밑에서 아래로 뿜는 연기 양(초당).")]
    [Min(0f)] public float thrusterRate = 40f;
    [Range(0f, 60f)] public float thrusterConeAngle = 15f;
    public Vector2 thrusterLifetime = new Vector2(0.4f, 0.7f);
    public Vector2 thrusterSpeed = new Vector2(2f, 3.5f);
    public Vector2 thrusterSize = new Vector2(0.25f, 0.45f);
    [Min(0f)] public float thrusterDrag = 2f;
    public Color thrusterColor = new Color(0.75f, 0.75f, 0.78f, 0.6f);

    [Tooltip("비콘이 이 높이보다 낮아지면 바닥 먼지가 나오기 시작합니다(가까울수록 많아짐).")]
    [Min(0.01f)] public float dustStartHeight = 3f;
    [Tooltip("바닥 먼지 최대 양(초당).")]
    [Min(0f)] public float dustRate = 60f;
    [Tooltip("착지 순간 한 번에 퍼지는 먼지 수.")]
    [Min(0)] public int landingDustBurst = 25;
    public Vector2 dustLifetime = new Vector2(0.8f, 1.4f);
    public Vector2 dustSpeed = new Vector2(1.5f, 3f);
    public Vector2 dustSize = new Vector2(0.3f, 0.6f);
    [Min(0f)] public float dustDrag = 3f;
    public Color dustColor = new Color(0.65f, 0.62f, 0.6f, 0.55f);

    [Header("하강 부스터 (1레벨 보스 전방위 탄환 외형 - 원본은 보스의 Beacon Booster Source 칸)")]
    public bool boosterEnabled = true;
    [Tooltip("부스터 크기 = 1레벨 보스 탄환 크기 x 이 값.")]
    [Min(0.01f)] public float boosterScale = 1f;
    [Tooltip("비콘 밑면에서 부스터 머리까지 내려간 거리(월드 단위, 음수면 몸 안쪽으로).")]
    public float boosterOffset = 0.05f;
    [Tooltip("고정 꼬리 길이(월드 단위). 0이면 1레벨 보스 탄환 꼬리 길이(꼬리 시간 x 탄환 속도)를 그대로 씀.")]
    [Min(0f)] public float boosterTailLength = 0f;

    [Header("착지 충격 먼지 (떨어지는 순간 바닥 둘레로 확 퍼짐)")]
    [Min(0)] public int impactDustBurst = 45;
    public Vector2 impactDustLifetime = new Vector2(0.5f, 0.9f);
    public Vector2 impactDustSpeed = new Vector2(4f, 7f);
    public Vector2 impactDustSize = new Vector2(0.35f, 0.7f);
    [Tooltip("퍼지다가 느려지는 정도(클수록 빨리 멈춤).")]
    [Min(0f)] public float impactDustDrag = 7f;
    public Color impactDustColor = new Color(0.68f, 0.65f, 0.62f, 0.65f);
}
