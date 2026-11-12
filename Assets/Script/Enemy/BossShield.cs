using System;
using UnityEngine;

// 2레벨 보스 보호막입니다(보스 프리팹 루트에 붙임). 피해를 양만큼 흡수하는 일반 게임식 보호막으로, 깨뜨린 한 방의 남은 피해는 체력으로 넘기지 않습니다.
// - 충전(BeginCharge): Charge Duration 동안 무적 + 모든 제어 면역, 임플로전 이펙트, 막이 커지며 체력바 위 보호막 바가 차오름 -> 끝나면 가득 찬 보호막.
// - 있는 동안: 주황 깎은 정이십면체 막이 항상 보이고(플레이어 보호막 연출과 같은 셰이더/메시), 맞으면 흰색으로 깜빡임. 보호막 크기만큼 판정이 커짐 -
//   보스 루트에 트리거 구체(플레이어 총알/수류탄/해킹 등 모든 범위 판정) + 접촉 판정(MeleeHitbox)에 같은 크기 트리거. 트리거라 이동/장애물 충돌과
//   플레이어 통과에는 영향 없음(보스는 본체 콜라이더대로 움직임). 수면 등 모든 강력 제어 면역, 해킹만 표시용으로 걸림(41번 피해 증가 적용, 멈추지는 않음).
// - 깨질 때: 플레이어 보호막 파괴 연출처럼 막이 순간 커지며 발광했다가 줄어들며 사라짐(Break 설정), OnShieldBroken -> 보스 컨트롤러가 그로기/재충전.
// - 피해 연결: EnemyHealth.TakeDamage가 IsInvulnerable/IsActive를 보고 이 컴포넌트로 넘김. 해킹툴 추가 피해는 PlayerHackTool이 ApplyHackToolBonus 호출.
// 기획은 Docs/설계노트.md [2레벨 보스].
[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyHealth))]
public class BossShield : MonoBehaviour, IEnemyControlFilter
{
    private enum Phase { None, Charging, Active }

    [Header("보호막 양")]
    [Min(1f)] public float maxShield = 300f;

    [Header("충전")]
    [Tooltip("충전 시간(초). 이 동안 무적 + 모든 제어 면역.")]
    [Min(0.05f)] public float chargeDuration = 1f;
    [Tooltip("충전이 시작될 때 보스 위치에 생성할 이펙트(VFX_Implosion_Custom 3). 비우면 이펙트 없음.")]
    public GameObject chargeVfxPrefab;
    [Tooltip("충전 이펙트 위치(보스 몸 중심 기준).")]
    public Vector3 chargeVfxOffset = Vector3.zero;
    [Min(0.01f)] public float chargeVfxScale = 1f;
    [Tooltip("충전 이펙트를 지울 때까지의 시간(초).")]
    [Min(0.1f)] public float chargeVfxLifetime = 2f;

    [Header("해킹툴")]
    [Tooltip("해킹툴을 직접 써서 보호막에 맞혔을 때 원래 피해에 더해 주는 추가 피해 = 최대 보호막 x 이 비율.")]
    [Range(0f, 1f)] public float hackToolBonusRatio = 0.25f;

    [Header("크기 (판정 + 외형)")]
    [Tooltip("보호막 반지름 = 보스 모델 범위(가장 긴 반지름) x 이 값.")]
    [Min(0.1f)] public float radiusMultiplier = 1.3f;
    [Tooltip("보호막 중심 위치 보정(월드 단위, 처음 충전할 때의 보스 방향 기준으로 한 번 반영).")]
    public Vector3 centerOffset = Vector3.zero;

    [Header("외형 (플레이어 보호막 연출과 같은 셰이더 - 주황)")]
    [Tooltip("비우면 MobArchive/ShieldBreakIcosa를 이름으로 찾음(빌드에서 빠지면 직접 넣을 것).")]
    public Shader shader;
    [ColorUsage(true, true)] public Color flickerWhite = new Color(1f, 0.95f, 0.85f, 0.7f);
    [ColorUsage(true, true)] public Color flickerColor = new Color(1f, 0.5f, 0.1f, 0.4f);
    [Tooltip("면이 바뀌는 횟수(초당). 항상 보이는 막이라 플레이어 연출보다 느리게.")]
    [Min(0f)] public float flickerSpeed = 3f;
    [Min(0.05f)] public float flickerSharpness = 1.3f;
    [Min(0f)] public float flickerGlow = 0.4f;
    [Range(0f, 0.9f)] public float rimFade = 0.3f;
    [Range(0f, 1f)] public float backFaceAlpha = 0.4f;
    [Tooltip("막 전체 불투명도.")]
    [Range(0f, 1f)] public float alpha = 0.8f;
    [Tooltip("초당 회전 각도.")]
    public float rotationSpeed = 30f;

    [Header("깜빡임 (피격 / 획득)")]
    [ColorUsage(true, true)] public Color hitFlashColor = Color.white;
    [Min(0f)] public float hitFlashIntensity = 1.5f;
    [Tooltip("피격 깜빡임이 사라지는 시간(초).")]
    [Min(0.01f)] public float hitFlashTime = 0.15f;
    [Range(0f, 1f)] public float hitFlashFaceAlpha = 0.6f;
    [Min(0f)] public float gainFlashIntensity = 2f;
    [Min(0.01f)] public float gainFlashTime = 0.35f;

    [Header("파괴 연출 (플레이어 보호막 파괴 연출과 같은 움직임)")]
    [Min(0.05f)] public float breakDuration = 0.55f;
    [Tooltip("파괴 순간 빠르게 커지는 시간(초).")]
    [Min(0.01f)] public float breakExpandTime = 0.12f;
    [Min(0.01f)] public float breakPeakScale = 1.25f;
    [Min(0.01f)] public float breakEndScale = 0.9f;
    [Min(0f)] public float breakGlowIntensity = 1.5f;
    [Min(0.01f)] public float breakGlowTime = 0.2f;
    [Tooltip("파괴 연출 동안의 면 반짝임 속도(평소보다 빠르게).")]
    [Min(0f)] public float breakFlickerSpeed = 7f;

    public event Action OnShieldGained;
    public event Action OnShieldBroken;

    private Phase phase = Phase.None;
    private float current;
    private float chargeTimer;

    private EnemyHealth health;
    private SphereCollider hitTrigger;     // 루트 - 플레이어 총알/범위 피해 판정
    private SphereCollider contactTrigger; // MeleeHitbox - 플레이어에게 주는 접촉/돌진 피해 판정
    private Vector3 localCenter;           // 보스 루트 기준 보호막 중심
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
    private float breakElapsed = -1f; // 0 이상이면 파괴 연출 중

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
    public bool IsActive => phase == Phase.Active && current > 0f;
    public bool IsInvulnerable => IsCharging;
    public bool IsControlImmune => IsCharging || IsActive;
    public float Current => IsActive ? current : 0f;
    public float Max => maxShield;
    // 체력바용: 충전 중에는 차오르는 비율, 있는 동안은 남은 비율.
    public float DisplayRatio => IsCharging ? 1f - Mathf.Clamp01(chargeTimer / Mathf.Max(chargeDuration, 0.01f))
                                           : (maxShield > 0f ? Mathf.Clamp01(current / maxShield) : 0f);
    public bool ShowsBar => IsCharging || IsActive;
    public bool IsBreaking => breakElapsed >= 0f;

    void Awake()
    {
        health = GetComponent<EnemyHealth>();
    }

    void OnDisable()
    {
        // 충전 도중 꺼지면(레벨을 만드는 순간 방이 잠깐 켜졌다 꺼지는 경우 등) 충전을 없던 일로 - 다음에 켜질 때 컨트롤러가 다시 시작합니다.
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

    // 보호막 충전을 시작합니다(이미 충전 중이거나 보호막이 있으면 무시). 성공하면 true.
    public bool BeginCharge()
    {
        if (phase != Phase.None || (health != null && health.IsDead)) return false;

        MeasureSize();
        breakElapsed = -1f;
        phase = Phase.Charging;
        chargeTimer = Mathf.Max(chargeDuration, 0.05f);
        SetTriggers(false);

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

    // 보호막이 피해를 받습니다(EnemyHealth.TakeDamage가 부름). 깨지면 남은 피해는 버립니다.
    public void AbsorbDamage(float amount)
    {
        if (!IsActive || amount <= 0f) return;

        current -= amount;
        Flash(hitFlashIntensity, hitFlashTime);

        if (current <= 0f) Break();
        else GameAudio.Play(SfxId.BossShieldHit);
    }

    // [해킹툴 직접 사용] 보호막이 남아 있으면 최대 보호막 x 비율만큼 보호막에 추가 피해(받는 피해 배율 등은 TakeDamage를 그대로 거침).
    public static void ApplyHackToolBonus(EnemyHealth enemy)
    {
        if (enemy == null || enemy.IsDead) return;
        var shield = enemy.GetComponent<BossShield>();
        if (shield == null || !shield.IsActive || shield.hackToolBonusRatio <= 0f) return;
        enemy.TakeDamage(shield.maxShield * shield.hackToolBonusRatio);
    }

    // IEnemyControlFilter - 충전 중에는 모든 강력 제어를 막고, 보호막이 있는 동안 해킹은 표시만(이펙트/지속시간/41번), 나머지(수면 등)는 막음.
    public EnemyControlFilterResult FilterStrong(EnemyControlType type)
    {
        if (IsCharging) return EnemyControlFilterResult.Block;
        if (IsActive) return type == EnemyControlType.Hack ? EnemyControlFilterResult.MarkOnly : EnemyControlFilterResult.Block;
        return EnemyControlFilterResult.Allow;
    }

    void Update()
    {
        if (phase != Phase.Charging) return;

        chargeTimer -= Time.deltaTime;
        if (chargeTimer > 0f) return;

        current = maxShield;
        phase = Phase.Active;
        SetTriggers(true);
        Flash(gainFlashIntensity, gainFlashTime);
        OnShieldGained?.Invoke();
    }

    private void Break()
    {
        current = 0f;
        phase = Phase.None;
        SetTriggers(false);
        breakElapsed = visual != null ? 0f : -1f; // 파괴 연출(판정은 이미 꺼짐)
        GameAudio.Play(SfxId.BossShieldBreak);
        OnShieldBroken?.Invoke();
    }

    private void Flash(float intensity, float time)
    {
        flashIntensity = intensity;
        flashDuration = Mathf.Max(time, 0.01f);
        flashTimer = flashDuration;
    }

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
        if (IsCharging)
        {
            float k = DisplayRatio;
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
            // 파괴: 빠르게 커지며 발광(끝에서 느려짐) -> 남은 시간 동안 줄어들며 투명해짐
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
        block.SetColor(GlowColorId, hitFlashColor);
        block.SetFloat(GlowId, glow);
        block.SetFloat(GlowFaceAlphaId, hitFlashFaceAlpha);
        visualRenderer.SetPropertyBlock(block);
    }

    // 보스 모델(메시 렌더러) 범위로 보호막 중심/반지름을 한 번 잽니다(파티클/선/트레일 렌더러는 제외).
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

        localCenter = transform.InverseTransformPoint(b.center + centerOffset); // 보정은 처음 잴 때 한 번 반영(보스와 함께 회전)
        worldRadius = Mathf.Max(0.1f, Mathf.Max(b.extents.x, Mathf.Max(b.extents.y, b.extents.z)) * radiusMultiplier);
        sizeMeasured = true;
        BuildTriggers();
    }

    // 보호막 크기 트리거 2개를 만듭니다(꺼진 채로). 트리거라 이동/물리 충돌에는 끼어들지 않습니다.
    private void BuildTriggers()
    {
        Vector3 worldCenter = transform.TransformPoint(localCenter);

        if (hitTrigger == null)
        {
            hitTrigger = gameObject.AddComponent<SphereCollider>();
            hitTrigger.isTrigger = true;
            hitTrigger.enabled = false;
        }
        hitTrigger.center = transform.InverseTransformPoint(worldCenter);
        hitTrigger.radius = worldRadius / MaxAbsScale(transform);

        var contact = GetComponentInChildren<EnemyMeleeContactTrigger>(true);
        if (contact == null)
        {
            Debug.LogWarning($"BossShield ({gameObject.name}): 자식에 EnemyMeleeContactTrigger(MeleeHitbox)가 없어 보호막 접촉 피해 판정을 만들지 못했습니다.");
            return;
        }
        if (contactTrigger == null)
        {
            contactTrigger = contact.gameObject.AddComponent<SphereCollider>();
            contactTrigger.isTrigger = true;
            contactTrigger.enabled = false;
        }
        contactTrigger.center = contact.transform.InverseTransformPoint(worldCenter);
        contactTrigger.radius = worldRadius / MaxAbsScale(contact.transform);
    }

    private void SetTriggers(bool on)
    {
        if (hitTrigger != null) hitTrigger.enabled = on;
        if (contactTrigger != null) contactTrigger.enabled = on;
    }

    private static float MaxAbsScale(Transform t)
    {
        Vector3 s = t.lossyScale;
        return Mathf.Max(0.0001f, Mathf.Max(Mathf.Abs(s.x), Mathf.Max(Mathf.Abs(s.y), Mathf.Abs(s.z))));
    }

    // 막 오브젝트는 보스 자식이 아니라 따로 만들고 위치만 따라갑니다(EnemyHealth 피격 색/잔상 등이 보스 렌더러를 모을 때 섞이지 않게).
    private bool EnsureVisual()
    {
        if (visual != null) return true;

        Shader s = shader != null ? shader : Shader.Find("MobArchive/ShieldBreakIcosa");
        if (s == null)
        {
            Debug.LogWarning("BossShield: 셰이더 MobArchive/ShieldBreakIcosa를 찾지 못해 보호막 외형을 만들지 못했습니다(판정은 그대로 동작).");
            return false;
        }

        visualMaterial = new Material(s);
        block = new MaterialPropertyBlock();

        visual = new GameObject("BossShieldVisual");
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
