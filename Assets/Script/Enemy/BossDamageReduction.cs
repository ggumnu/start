using System;
using UnityEngine;

// 3레벨 보스 1페이즈의 "피해 감소막"입니다(보스 프리팹 루트에 붙임). 겉모습만 플레이어 보호막과 같은 하늘색 막이고, 실제로는 보호막이 아닙니다
// - 피해를 흡수하지 않고, 깨지지 않으며, 체력바에도 보호막 줄이 없습니다(헷갈리지 않게 - 형빈 확정).
// - 충전(BeginCharge): Charge Duration 동안 무적 + 모든 강력 제어 면역, 막이 커지며 밝아짐. 이 동안 맞으면 피해 숫자 대신 "MISS"(EnemyHealth.OnAnyEnemyMissed).
// - 있는 동안: 받는 피해 x Damage Taken Multiplier(0.15 = 85% 감소, 숫자는 항상 Resist - 형빈 확정, 기본). 총알 추가 효과는 그대로 발동하되
//   113번 슈가 중첩만 쌓이지 않음(BlocksSugarStacks - 충전 중 포함, 형빈 확정).
//   Block All Damage를 켜면 충전 때처럼 피해 0 + "MISS", 총알은 맞은 것처럼 보이되 추가 효과 없음(Projectile - 4/5/69/118번 등 모두 X).
//   어느 쪽이든 98번 섬멸 안 걸림.
//   수면 등 강력 제어는 막고 해킹은 표시만(2레벨 보호막과 같은 거르기 - 41번 피해 증가가 동작).
// - 기간 제한 없음: 보스 컨트롤러(BossLv3AIController)가 기믹 해제 때 Remove로 끕니다(플레이어 보호막 파괴 연출처럼 커졌다 사라짐).
// - 피해 연결: EnemyHealth.TakeDamage가 IsInvulnerable/IsReducing/BlocksExecute를 읽음. 기믹 해제 피해(최대 체력 20%)는 ApplyBreakDamage로 감소 없이 넣음.
// 기획은 Docs/설계노트.md [3레벨 보스].
[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyHealth))]
public class BossDamageReduction : MonoBehaviour, IEnemyControlFilter
{
    private enum Phase { None, Charging, Active }

    [Header("피해 감소")]
    [Tooltip("켜면 막이 있는 동안 피해를 전혀 받지 않고 'MISS'만 뜹니다(총알 추가 효과도 없음). 끄면(기본) 아래 배율만큼만 피해를 받습니다. " +
             "기믹 해제 피해는 어느 쪽이든 그대로 들어감.")]
    public bool blockAllDamage = false;
    [Tooltip("Block All Damage를 끈 경우에만: 막이 있는 동안 받는 피해 배율입니다(0.15 = 85% 감소, 0이면 피해 숫자만 뜨고 체력은 안 깎임).")]
    [Range(0f, 1f)] public float damageTakenMultiplier = 0.15f;

    [Header("충전 (생성 연출 - 이 동안 무적, 맞으면 MISS)")]
    [Min(0.05f)] public float chargeDuration = 1f;
    [Tooltip("충전 시작 때 막 중심에 재생할 이펙트(2레벨 보스는 VFX_Implosion_Custom 3). 비우면 없음.")]
    public GameObject chargeVfxPrefab;
    public Vector3 chargeVfxOffset = Vector3.zero;
    [Min(0.01f)] public float chargeVfxScale = 1f;
    [Tooltip("충전 이펙트를 지우기까지(초).")]
    [Min(0.1f)] public float chargeVfxLifetime = 2f;

    [Header("막 크기")]
    [Tooltip("막 반지름 = 보스 모델 크기 x 이 값.")]
    [Min(0.1f)] public float radiusMultiplier = 1.3f;
    [Tooltip("막 중심 보정(처음 잴 때 한 번 반영).")]
    public Vector3 centerOffset = Vector3.zero;

    [Header("막 외형 (플레이어 보호막과 같은 셰이더/메시 - 기본값은 플레이어 보호막 하늘색)")]
    [Tooltip("비우면 MobArchive/ShieldBreakIcosa를 이름으로 찾습니다.")]
    public Shader shader;
    [ColorUsage(true, true)] public Color flickerWhite = new Color(1f, 1f, 1f, 0.85f);
    [ColorUsage(true, true)] public Color flickerColor = new Color(0.35f, 0.72f, 1f, 0.45f);
    [Min(0f)] public float flickerSpeed = 3f;
    [Min(0.05f)] public float flickerSharpness = 1.3f;
    [Min(0f)] public float flickerGlow = 0.6f;
    [Range(0f, 0.9f)] public float rimFade = 0.3f;
    [Range(0f, 1f)] public float backFaceAlpha = 0.5f;
    [Range(0f, 1f)] public float alpha = 0.8f;
    [Tooltip("막이 도는 속도(도/초).")]
    public float rotationSpeed = 30f;
    [ColorUsage(true, true)] public Color glowColor = Color.white;
    [Range(0f, 1f)] public float glowFaceAlpha = 0.6f;
    [Tooltip("충전이 끝나 막이 완성되는 순간의 발광 세기/시간.")]
    [Min(0f)] public float gainFlashIntensity = 2f;
    [Min(0.01f)] public float gainFlashTime = 0.35f;

    [Header("막이 사라질 때 (플레이어 보호막 파괴 연출과 같은 움직임)")]
    [Min(0.05f)] public float breakDuration = 0.55f;
    [Min(0.01f)] public float breakExpandTime = 0.12f;
    [Min(0.01f)] public float breakPeakScale = 1.25f;
    [Min(0.01f)] public float breakEndScale = 0.9f;
    [Min(0f)] public float breakGlowIntensity = 1.5f;
    [Min(0.01f)] public float breakGlowTime = 0.2f;
    [Min(0f)] public float breakFlickerSpeed = 7f;

    // 충전이 끝나 감소막이 생긴 순간.
    public event Action OnActivated;

    private Phase phase = Phase.None;
    private float chargeTimer;
    private bool bypassReduction; // ApplyBreakDamage 중(기믹 해제 피해는 줄이지 않음)

    private Vector3 localCenter;
    private float worldRadius = 1f;
    private bool sizeMeasured;

    private GameObject visual;
    private Transform visualTransform;
    private MeshRenderer visualRenderer;
    private Material visualMaterial;
    private MaterialPropertyBlock block;
    private Vector3 spinAxis = Vector3.up;
    private float seed;
    private float flickerTime;
    private float flashTimer;
    private float flashDuration = 0.15f;
    private float flashIntensity;
    private GameObject chargeVfx;
    private float breakElapsed = -1f; // 0 이상이면 사라지는 연출 중

    private static readonly int WhiteId = Shader.PropertyToID("_FlickerWhite");
    private static readonly int BlueId = Shader.PropertyToID("_FlickerBlue");
    private static readonly int SpeedId = Shader.PropertyToID("_FlickerSpeed");
    private static readonly int SharpnessId = Shader.PropertyToID("_FlickerSharpness");
    private static readonly int FlickerGlowId = Shader.PropertyToID("_FlickerGlow");
    private static readonly int RimFadeId = Shader.PropertyToID("_RimFade");
    private static readonly int BackAlphaId = Shader.PropertyToID("_BackFaceAlpha");
    private static readonly int FlickerTimeId = Shader.PropertyToID("_FlickerTime");
    private static readonly int SeedId = Shader.PropertyToID("_Seed");
    private static readonly int AlphaId = Shader.PropertyToID("_Alpha");
    private static readonly int GlowColorId = Shader.PropertyToID("_GlowColor");
    private static readonly int GlowId = Shader.PropertyToID("_Glow");
    private static readonly int GlowFaceAlphaId = Shader.PropertyToID("_GlowFaceAlpha");

    public bool IsCharging => phase == Phase.Charging;
    public bool IsActive => phase == Phase.Active;
    // 충전 중, 또는 막이 있는 동안(Block All Damage) = 무적(피해 대신 MISS, 총알 추가 효과 없음). 기믹 해제 피해(ApplyBreakDamage)는 제외.
    public bool IsInvulnerable => phase == Phase.Charging || (phase == Phase.Active && blockAllDamage && !bypassReduction);
    // 지금 들어오는 피해를 줄여야 하는지(기믹 해제 피해는 제외).
    public bool IsReducing => phase == Phase.Active && !blockAllDamage && !bypassReduction;
    // 막이 있는 동안(충전 포함)은 98번 섬멸이 걸리지 않습니다(형빈 확정).
    public bool BlocksExecute => phase != Phase.None;
    // 막이 있는 동안(충전 포함)은 113번 슈가 중첩이 쌓이지 않습니다(형빈 확정 - 피해를 받는 모드여도). 기믹 해제 뒤 그로기 동안에는 쌓임.
    public bool BlocksSugarStacks => phase != Phase.None;
    public bool IsControlImmune => phase != Phase.None;
    public float DamageTakenMultiplier => Mathf.Clamp01(damageTakenMultiplier);

    void OnDisable()
    {
        // 충전 도중 꺼지면(레벨을 만드는 순간 방이 잠깐 켜졌다 꺼지는 경우 등) 없던 일로 - 다음에 켜질 때 컨트롤러가 다시 시작합니다.
        if (phase == Phase.Charging) phase = Phase.None;
        breakElapsed = -1f;
        DestroyChargeVfx();
        if (visual != null) visual.SetActive(false);
    }

    void OnDestroy()
    {
        DestroyChargeVfx();
        if (visual != null) Destroy(visual);
        if (visualMaterial != null) Destroy(visualMaterial);
    }

    // 생성 연출(충전)을 시작합니다(이미 충전 중이거나 막이 있으면 무시). 성공하면 true.
    public bool BeginCharge()
    {
        if (phase != Phase.None) return false;
        var health = GetComponent<EnemyHealth>();
        if (health != null && health.IsDead) return false;

        MeasureSize();
        breakElapsed = -1f;
        phase = Phase.Charging;
        chargeTimer = Mathf.Max(chargeDuration, 0.05f);

        DestroyChargeVfx();
        if (chargeVfxPrefab != null)
        {
            chargeVfx = Instantiate(chargeVfxPrefab, transform.TransformPoint(localCenter) + chargeVfxOffset, Quaternion.identity, transform);
            chargeVfx.transform.localScale = chargeVfx.transform.localScale * chargeVfxScale;
            Destroy(chargeVfx, chargeVfxLifetime);
        }

        if (EnsureVisual())
        {
            seed = UnityEngine.Random.Range(0f, 100f);
            spinAxis = UnityEngine.Random.onUnitSphere;
            visualTransform.rotation = UnityEngine.Random.rotationUniform;
            visual.SetActive(true);
        }
        return true;
    }

    // 감소막을 끕니다(기믹 해제). playBreakEffect면 플레이어 보호막 파괴처럼 커졌다 사라지고 파괴음.
    public void Remove(bool playBreakEffect)
    {
        if (phase == Phase.None) return;
        phase = Phase.None;
        DestroyChargeVfx();
        if (playBreakEffect)
        {
            breakElapsed = visual != null ? 0f : -1f;
            GameAudio.Play(SfxId.BossShieldBreak);
        }
        else
        {
            breakElapsed = -1f;
            if (visual != null) visual.SetActive(false);
        }
    }

    // 감소 없이 "항상 Weak" 피해를 넣습니다(기믹 해제 = 최대 체력 x 20%). 98번 섬멸은 막이 있는 동안이라 걸리지 않습니다.
    public void ApplyBreakDamage(EnemyHealth health, float amount)
    {
        if (health == null || health.IsDead || amount <= 0f) return;
        bypassReduction = true;
        try { health.TakeWeakDamage(amount); }
        finally { bypassReduction = false; }
    }

    // IEnemyControlFilter - 2레벨 보호막과 같음: 충전 중엔 강력 제어 전부 막고, 막이 있는 동안 해킹은 표시만(이펙트/지속시간/41번), 나머지는 막음.
    public EnemyControlFilterResult FilterStrong(EnemyControlType type)
    {
        if (phase == Phase.Charging) return EnemyControlFilterResult.Block;
        if (phase == Phase.Active) return type == EnemyControlType.Hack ? EnemyControlFilterResult.MarkOnly : EnemyControlFilterResult.Block;
        return EnemyControlFilterResult.Allow;
    }

    void Update()
    {
        if (phase != Phase.Charging) return;

        chargeTimer -= Time.deltaTime;
        if (chargeTimer > 0f) return;

        phase = Phase.Active;
        Flash(gainFlashIntensity, gainFlashTime);
        OnActivated?.Invoke();
    }

    private void Flash(float intensity, float time)
    {
        flashIntensity = intensity;
        flashDuration = Mathf.Max(time, 0.01f);
        flashTimer = flashDuration;
    }

    // 충전 진행도(0 -> 1).
    private float ChargeProgress => 1f - Mathf.Clamp01(chargeTimer / Mathf.Max(chargeDuration, 0.01f));

    void LateUpdate()
    {
        if (visual == null) return;

        float dt = Time.deltaTime;
        if (breakElapsed >= 0f)
        {
            breakElapsed += dt;
            if (breakElapsed >= breakDuration) breakElapsed = -1f;
        }

        bool show = phase != Phase.None || breakElapsed >= 0f;
        if (visual.activeSelf != show) visual.SetActive(show); // 방이 꺼졌다 켜져도 다시 보이게
        if (!show) return;

        flickerTime += dt;
        if (flashTimer > 0f) flashTimer = Mathf.Max(0f, flashTimer - dt);

        // 충전 중에는 작게 시작해 커지며(끝으로 갈수록 느려짐) 점점 밝아짐
        float grow = 1f;
        float glow = 0f;
        float shownAlpha = alpha;
        float shownFlickerSpeed = flickerSpeed;
        if (phase == Phase.Charging)
        {
            float k = ChargeProgress;
            grow = Mathf.Lerp(0.2f, 1f, 1f - (1f - k) * (1f - k) * (1f - k));
            glow = k * 0.5f;
        }
        if (flashTimer > 0f)
        {
            float f = flashTimer / flashDuration;
            glow = Mathf.Max(glow, flashIntensity * f * f);
        }
        if (breakElapsed >= 0f)
        {
            // 사라짐: 빠르게 커지며 발광(끝에서 느려짐) -> 남은 시간 동안 줄어들며 투명해짐
            float total = Mathf.Max(0.05f, breakDuration);
            float expand = Mathf.Min(Mathf.Max(0.01f, breakExpandTime), total);
            if (breakElapsed < expand)
            {
                float k = breakElapsed / expand;
                grow = Mathf.LerpUnclamped(1f, breakPeakScale, 1f - (1f - k) * (1f - k) * (1f - k));
            }
            else
            {
                float k = Mathf.Clamp01((breakElapsed - expand) / Mathf.Max(0.0001f, total - expand));
                float smooth = k * k * (3f - 2f * k);
                grow = Mathf.LerpUnclamped(breakPeakScale, breakEndScale, smooth);
                shownAlpha = alpha * (1f - smooth);
            }
            float g = Mathf.Clamp01(1f - breakElapsed / Mathf.Max(0.01f, breakGlowTime));
            glow = breakGlowIntensity * g * g;
            shownFlickerSpeed = breakFlickerSpeed;
        }

        visualTransform.position = transform.TransformPoint(localCenter);
        visualTransform.localScale = Vector3.one * (worldRadius * grow);
        if (rotationSpeed != 0f) visualTransform.Rotate(spinAxis, rotationSpeed * dt, Space.World);

        block.SetColor(WhiteId, flickerWhite);
        block.SetColor(BlueId, flickerColor);
        block.SetFloat(SpeedId, shownFlickerSpeed);
        block.SetFloat(SharpnessId, flickerSharpness);
        block.SetFloat(FlickerGlowId, flickerGlow);
        block.SetFloat(RimFadeId, rimFade);
        block.SetFloat(BackAlphaId, backFaceAlpha);
        block.SetFloat(FlickerTimeId, flickerTime);
        block.SetFloat(SeedId, seed);
        block.SetFloat(AlphaId, shownAlpha);
        block.SetColor(GlowColorId, glowColor);
        block.SetFloat(GlowId, glow);
        block.SetFloat(GlowFaceAlphaId, glowFaceAlpha);
        visualRenderer.SetPropertyBlock(block);
    }

    // 보스 모델(메시 렌더러) 범위로 막 중심/반지름을 한 번 잽니다(파티클/선/트레일 렌더러는 제외). 판정은 넓히지 않습니다(보호막이 아니므로).
    private void MeasureSize()
    {
        if (sizeMeasured) return;

        bool any = false;
        Bounds b = new Bounds(transform.position, Vector3.zero);
        foreach (var r in GetComponentsInChildren<Renderer>())
        {
            if (r == null || !r.enabled) continue;
            if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;
            if (any) b.Encapsulate(r.bounds);
            else { b = r.bounds; any = true; }
        }
        if (!any) b = new Bounds(transform.position + Vector3.up * 0.5f, Vector3.one);

        localCenter = transform.InverseTransformPoint(b.center + centerOffset);
        worldRadius = Mathf.Max(0.1f, Mathf.Max(b.extents.x, Mathf.Max(b.extents.y, b.extents.z)) * radiusMultiplier);
        sizeMeasured = true;
    }

    // 막 오브젝트는 보스 자식이 아니라 따로 만들고 위치만 따라갑니다(EnemyHealth 피격 색/잔상 등이 보스 렌더러를 모을 때 섞이지 않게).
    private bool EnsureVisual()
    {
        if (visual != null) return true;

        Shader s = shader != null ? shader : Shader.Find("MobArchive/ShieldBreakIcosa");
        if (s == null)
        {
            Debug.LogWarning("BossDamageReduction: 셰이더 MobArchive/ShieldBreakIcosa를 찾지 못해 막 외형을 만들지 못했습니다(피해 감소는 그대로 동작).");
            return false;
        }

        visualMaterial = new Material(s);
        block = new MaterialPropertyBlock();

        visual = new GameObject("BossDamageReductionVisual");
        visualTransform = visual.transform;
        visual.AddComponent<MeshFilter>().sharedMesh = PlayerShieldBreakVfx.GetSharedMesh();
        visualRenderer = visual.AddComponent<MeshRenderer>();
        visualRenderer.sharedMaterial = visualMaterial;
        visualRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        visualRenderer.receiveShadows = false;
        visual.SetActive(false);
        return true;
    }

    private void DestroyChargeVfx()
    {
        if (chargeVfx != null) Destroy(chargeVfx);
        chargeVfx = null;
    }
}
