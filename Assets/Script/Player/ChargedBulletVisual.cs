using System.Collections.Generic;
using UnityEngine;

// [110번 장신구 - 강화 총알 외형] 충전된 플레이어 총알(+5번 분열탄)과 강화 외형을 켠 적 탄환(EnemyRadialAttack)에 붙는 코드 생성 외형입니다.
// 중심부 빛 / 외곽 빛 / 나선 고리 / 번개 / 꼬리 다섯 요소, 크기 단위는 "총알 지름"(총알 자식이라 총알 크기를 따라감). HDR 색이라 URP Bloom 필요.
// 주의: 빌드에서 분홍색(셰이더 누락)이면 설정의 Additive/Ring Material 칸에 URP Particles/Unlit 재질을 넣을 것.
// 설계 배경(충전 수에 따른 세기, 23번 무지개 색 규칙)은 Docs/설계노트.md [110번 강화 총알 외형].
public class ChargedBulletVisual : MonoBehaviour
{
    [System.Serializable]
    public class Settings
    {
        [Header("직접 만든 이펙트 (선택)")]
        [Tooltip("채우면 코드로 만드는 외형 대신 이 프리팹을 총알에 붙입니다(진행 방향 = 프리팹 +Z, 총알 크기에 맞춰 커짐).")]
        public GameObject overridePrefab;

        [Tooltip("비워두면 실행 중에 만듭니다. 빌드에서 분홍색으로 보이면 URP Particles/Unlit(Transparent, Additive) 재질을 직접 넣으세요.")]
        public Material additiveMaterial;

        [Tooltip("비워두면 실행 중에 만듭니다. 빌드에서 분홍색으로 보이면 URP Particles/Unlit(Transparent, Alpha) 재질을 직접 넣으세요(고리 그림은 코드가 넣음).")]
        public Material ringMaterial;

        [Header("세기 (충전 수에 따라)")]
        [Tooltip("1발 더 충전될 때마다 밝기에 더하는 배율입니다(1발 충전 = 1배).")]
        [Min(0f)] public float intensityPerCharge = 0.15f;

        [Tooltip("밝기 배율의 최대값입니다.")]
        [Min(1f)] public float maxIntensityMultiplier = 2f;

        [Header("1) 중심부 (단위: 총알 지름)")]
        [Tooltip("중심부 색입니다(총알 색 아이템이 있으면 그 색을 대신 씀).")]
        public Color coreColor = new Color(1f, 0.55f, 0.95f, 1f);
        [Min(0f)] public float coreIntensity = 3f;
        [Min(0.01f)] public float coreLength = 3f;
        [Min(0.01f)] public float coreWidth = 1.4f;
        [Tooltip("중심부 한가운데의 하얗게 타는 부분 밝기입니다.")]
        [Min(0f)] public float hotCoreIntensity = 4f;

        [Header("2) 외곽 빛 (단위: 총알 지름)")]
        public Color haloColor = new Color(0.45f, 0.4f, 1f, 0.7f);
        [Min(0f)] public float haloIntensity = 1.6f;
        [Min(0.01f)] public float haloLength = 5f;
        [Min(0.01f)] public float haloWidth = 3f;

        [Header("3) 나선 고리 (단위: 총알 지름)")]
        [Tooltip("고리 안쪽 어두운 띠 색입니다(투명도 = 진하기).")]
        public Color ringColor = new Color(0.08f, 0.05f, 0.2f, 0.85f);
        [Tooltip("고리 바깥 테두리의 밝은 색입니다.")]
        public Color ringRimColor = new Color(0.6f, 0.8f, 1f, 1f);
        [Min(0)] public int ringCount = 3;
        [Min(0.01f)] public float ringSize = 2.2f;
        [Tooltip("고리 사이 간격입니다(총알 뒤쪽으로 이 간격마다 하나씩).")]
        [Min(0f)] public float ringSpacing = 1.1f;
        [Tooltip("고리가 진행 방향에서 기울어진 각도(도)입니다.")]
        [Range(0f, 80f)] public float ringTilt = 35f;
        [Tooltip("기울어진 방향이 총알 둘레를 도는 속도(도/초)입니다.")]
        public float ringSpinSpeed = 720f;

        [Header("4) 번개 (단위: 총알 지름)")]
        public Color lightningColor = new Color(0.5f, 0.85f, 1f, 1f);
        [Min(0f)] public float lightningIntensity = 3f;
        [Tooltip("1발 충전일 때의 번개 줄기 수입니다(2발 충전마다 하나씩 늘어남).")]
        [Min(0)] public int lightningCount = 2;
        [Min(0)] public int lightningCountMax = 4;
        [Tooltip("분열탄(5번)의 번개 줄기 수입니다(분열탄이 많아 줄여 둠).")]
        [Min(0)] public int splitLightningCount = 1;
        [Min(0.01f)] public float lightningLength = 4f;
        [Tooltip("번개가 옆으로 꺾이는 정도입니다.")]
        [Min(0f)] public float lightningJitter = 0.9f;
        [Tooltip("번개 굵기입니다.")]
        [Min(0.001f)] public float lightningWidth = 0.12f;
        [Tooltip("번개 모양을 다시 그리는 간격(초)입니다.")]
        [Min(0.01f)] public float lightningRefreshInterval = 0.05f;

        [Header("23번 무지개 색 (23번이 굴린 색이 있는 강화 총알)")]
        [Tooltip("중심부/외곽 빛/번개를 23번 색으로 바꾸는 정도입니다(0 = 원래 색, 1 = 23번 색으로 완전히 교체).")]
        [Range(0f, 1f)] public float rainbowTintStrength = 1f;

        [Tooltip("세 부분의 색상을 23번 색에서 얼마나 벌릴지입니다(색상환 비율, 0.06 ≈ 22도). 외곽 빛은 +, 번개는 - 방향으로 돌아갑니다. 0이면 셋 다 같은 색.")]
        [Range(0f, 0.25f)] public float rainbowHueSpread = 0.06f;

        [Tooltip("23번 색의 채도 최소값입니다. 색이 옅어 보이면 올리세요.")]
        [Range(0f, 1f)] public float rainbowMinSaturation = 0.75f;

        [Tooltip("23번 총알의 중심부 밝기 배율입니다. 너무 밝으면 빛 번짐 때문에 하얗게 바래 색이 안 보이므로 낮춥니다(1 = 원래 밝기).")]
        [Range(0.05f, 1f)] public float rainbowCoreIntensityScale = 0.5f;

        [Tooltip("중심부 가운데 흰 부분을 23번 색으로 물들이는 정도입니다(0 = 흰색 유지, 1 = 23번 색).")]
        [Range(0f, 1f)] public float rainbowHotCoreTint = 0.6f;

        [Tooltip("번개의 채도 배율입니다(번개는 조금 더 밝고 옅게 - 1이면 중심부와 같은 채도).")]
        [Range(0f, 1f)] public float rainbowLightningSaturation = 0.6f;

        [Header("5) 꼬리 (단위: 총알 지름)")]
        public Color trailHeadColor = new Color(1f, 0.45f, 0.9f, 1f);
        public Color trailTailColor = new Color(0.3f, 0.45f, 1f, 1f);
        [Min(0f)] public float trailIntensity = 2f;
        [Min(0.01f)] public float trailTime = 0.18f;
        [Min(0.01f)] public float trailWidth = 1.8f;

        // 1레벨 보스 전방위 탄환(하늘색)에 맞춘 기본값 - 색만 하늘색 계열이고 크기/세기는 110번 기본값과 같습니다.
        public static Settings CreateCyan()
        {
            return new Settings
            {
                coreColor = new Color(0.55f, 1f, 1f, 1f),
                haloColor = new Color(0.1f, 0.6f, 1f, 0.7f),
                ringColor = new Color(0.02f, 0.1f, 0.2f, 0.85f),
                ringRimColor = new Color(0.6f, 1f, 1f, 1f),
                lightningColor = new Color(0.6f, 1f, 1f, 1f),
                trailHeadColor = new Color(0.45f, 1f, 1f, 1f),
                trailTailColor = new Color(0.1f, 0.45f, 1f, 1f),
            };
        }
    }

    // ---- 공용 재질/메시/그림 캐시 ----
    private static Material sharedAdditiveMaterial;
    private static Material sharedSolidAdditiveMaterial;
    private static readonly Dictionary<Settings, Material> ringMaterials = new Dictionary<Settings, Material>();
    private static Texture2D glowTexture;
    private static Mesh quadMesh;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlayStart()
    {
        ringMaterials.Clear();
    }

    // ---- 이 총알의 상태 ----
    private Projectile projectile;
    private Settings settings;
    private int chargeCount;
    private bool isSplit;
    private float intensity;
    private Camera cam;

    private Transform visualRoot;
    private Transform haloQuad;
    private Transform coreQuad;
    private Transform hotQuad;
    private readonly List<Transform> rings = new List<Transform>();
    private readonly List<LineRenderer> bolts = new List<LineRenderer>();
    private float boltTimer;
    private float spinAngle;

    public int ChargeCount => chargeCount;

    // 총알에 강화 외형을 붙입니다. 이미 붙어 있으면 아무 일도 하지 않습니다.
    public static ChargedBulletVisual Attach(Projectile target, int charges, Settings visualSettings, bool split)
    {
        if (target == null || visualSettings == null || charges <= 0) return null;
        if (target.GetComponent<ChargedBulletVisual>() != null) return null;

        var visual = target.gameObject.AddComponent<ChargedBulletVisual>();
        visual.projectile = target;
        visual.settings = visualSettings;
        visual.chargeCount = charges;
        visual.isSplit = split;
        try
        {
            visual.Build();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"ChargedBulletVisual: 강화 외형 생성 중 오류가 발생해 외형 없이 진행합니다(총알 동작에는 영향 없음): {e}");
        }
        return visual;
    }

    // 플레이어 총알(Projectile)이 아닌 탄환(적 탄환 등)에 강화 외형을 붙입니다. 진행 방향 = 탄환의 +Z, 23번 무지개 색/총알 색 아이템은 적용 안 됨.
    // 외형 생성에 실패하면 붙인 것을 지우고 null을 돌려줍니다(호출 쪽이 원래 탄환 그림을 그대로 보여 주도록).
    public static ChargedBulletVisual AttachToBullet(GameObject bullet, int charges, Settings visualSettings)
    {
        if (bullet == null || visualSettings == null || charges <= 0) return null;
        if (bullet.GetComponent<ChargedBulletVisual>() != null) return null;

        var visual = bullet.AddComponent<ChargedBulletVisual>();
        visual.settings = visualSettings;
        visual.chargeCount = charges;
        visual.isSplit = false;
        try
        {
            visual.Build();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"ChargedBulletVisual: 탄환 강화 외형 생성 중 오류가 발생해 외형 없이 진행합니다: {e}");
            if (visual.visualRoot != null) Destroy(visual.visualRoot.gameObject);
            Destroy(visual);
            return null;
        }
        return visual;
    }

    // [5번 분열 연동] parent가 강화 총알이면 child(분열탄)에도 같은 강화 외형을 붙입니다(형빈 확정).
    public static void CopyToSplit(Projectile parent, Projectile child)
    {
        if (parent == null || child == null) return;
        var parentVisual = parent.GetComponent<ChargedBulletVisual>();
        if (parentVisual == null) return;
        Attach(child, parentVisual.chargeCount, parentVisual.settings, true);
    }

    void Build()
    {
        var s = settings;
        intensity = Mathf.Min(1f + s.intensityPerCharge * (chargeCount - 1), Mathf.Max(1f, s.maxIntensityMultiplier));

        visualRoot = new GameObject("ChargedVisual").transform;
        visualRoot.SetParent(transform, false);
        AlignRoot();

        // 꼬리는 총알이 사라져도 자연스럽게 옅어지도록 따로 만듭니다(BulletTrail과 같은 이유).
        ChargedTrailFollower.Spawn(transform, s, intensity);

        if (s.overridePrefab != null)
        {
            var custom = Instantiate(s.overridePrefab, visualRoot);
            custom.transform.localPosition = Vector3.zero;
            custom.transform.localRotation = Quaternion.identity;
            return;
        }

        Material additive = GetAdditiveMaterial(s);

        // [23번] 굴린 색이 있으면 중심부/외곽 빛/번개 색과 밝기를 그 색 기준으로 바꿉니다(위 [23번과 함께] 참고).
        Color haloColor = s.haloColor;
        Color coreBase = projectile != null ? ResolveCoreColor(s.coreColor) : s.coreColor; // 총알 색 아이템은 플레이어 총알만
        Color hotColor = Color.white;
        Color lightningColor = s.lightningColor;
        float coreIntensityScale = 1f;
        if (projectile != null && projectile.HasRandomDamageRoll && s.rainbowTintStrength > 0f)
        {
            Color.RGBToHSV(projectile.RandomDamageColor, out float hue, out float sat, out _);
            sat = Mathf.Max(sat, s.rainbowMinSaturation);
            float k = s.rainbowTintStrength;

            Color coreRainbow = Color.HSVToRGB(hue, sat, 1f);
            Color haloRainbow = Color.HSVToRGB(Mathf.Repeat(hue + s.rainbowHueSpread, 1f), sat, 1f);
            Color boltRainbow = Color.HSVToRGB(Mathf.Repeat(hue - s.rainbowHueSpread, 1f), sat * s.rainbowLightningSaturation, 1f);

            coreBase = Color.Lerp(s.coreColor, coreRainbow, k);
            haloColor = Color.Lerp(s.haloColor, haloRainbow, k);
            haloColor.a = s.haloColor.a;
            lightningColor = Color.Lerp(s.lightningColor, boltRainbow, k);
            hotColor = Color.Lerp(Color.white, coreRainbow, s.rainbowHotCoreTint * k);
            coreIntensityScale = Mathf.Lerp(1f, s.rainbowCoreIntensityScale, k);
        }

        // 2) 외곽 빛 - 큰 빛 그림(뒤쪽으로 길게)
        haloQuad = CreateQuad("Halo", additive, haloColor * (s.haloIntensity * intensity), haloColor.a);
        haloQuad.localScale = new Vector3(s.haloWidth, s.haloLength, 1f);
        haloQuad.localPosition = new Vector3(0f, 0f, 0.5f - s.haloLength * 0.45f);

        // 1) 중심부 - 총알 색 아이템이 있으면 그 색
        coreQuad = CreateQuad("Core", additive, coreBase * (s.coreIntensity * intensity * coreIntensityScale), 1f);
        coreQuad.localScale = new Vector3(s.coreWidth, s.coreLength, 1f);
        coreQuad.localPosition = new Vector3(0f, 0f, 0.5f - s.coreLength * 0.45f);

        hotQuad = CreateQuad("HotCore", additive, hotColor * (s.hotCoreIntensity * intensity * coreIntensityScale), 1f);
        hotQuad.localScale = new Vector3(s.coreWidth * 0.45f, s.coreLength * 0.75f, 1f);
        hotQuad.localPosition = new Vector3(0f, 0f, 0.5f - s.coreLength * 0.4f);

        // 3) 나선 고리
        Material ringMat = GetRingMaterial(s);
        for (int i = 0; i < s.ringCount; i++)
        {
            Transform ring = CreateMeshChild("Ring" + i, ringMat);
            ring.localScale = new Vector3(s.ringSize, s.ringSize, 1f);
            ring.localPosition = new Vector3(0f, 0f, 0.2f - i * s.ringSpacing);
            rings.Add(ring);
        }

        // 4) 번개
        int boltCount = isSplit ? s.splitLightningCount
                                : Mathf.Min(s.lightningCount + (chargeCount - 1) / 2, Mathf.Max(s.lightningCount, s.lightningCountMax));
        Material solid = GetSolidAdditiveMaterial(s);
        for (int i = 0; i < boltCount; i++)
        {
            bolts.Add(CreateBolt("Bolt" + i, solid, lightningColor * (s.lightningIntensity * intensity)));
        }
        RefreshBolts();

        UpdateBillboards();
        UpdateRings(0f);
    }

    void LateUpdate()
    {
        if (visualRoot == null || settings == null) return;

        AlignRoot();
        UpdateBillboards();
        UpdateRings(Time.deltaTime);

        boltTimer += Time.deltaTime;
        if (boltTimer >= settings.lightningRefreshInterval)
        {
            boltTimer = 0f;
            RefreshBolts();
        }
    }

    // 외형 기준(visualRoot)의 +Z를 총알 진행 방향에 맞춥니다(벽/적 튕김으로 방향이 바뀌어도 따라감).
    void AlignRoot()
    {
        Vector3 dir = projectile != null ? projectile.Direction : transform.forward;
        if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;
        if (dir.sqrMagnitude < 0.0001f) return;
        visualRoot.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
    }

    // 빛 그림들은 진행 방향을 축으로 카메라를 바라보게 돌립니다(길쭉한 빛이 어느 각도에서도 납작해 보이지 않게).
    void UpdateBillboards()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        Vector3 axis = visualRoot.forward;
        Vector3 view = cam.transform.forward;
        Vector3 facing = view - Vector3.Dot(view, axis) * axis;
        if (facing.sqrMagnitude < 0.0001f) facing = Vector3.Cross(axis, Vector3.right);
        // 그림의 위쪽(Y) = 진행 방향, 그림 면 = 카메라 쪽
        Quaternion rot = Quaternion.LookRotation(facing.normalized, axis);

        if (haloQuad != null) haloQuad.rotation = rot;
        if (coreQuad != null) coreQuad.rotation = rot;
        if (hotQuad != null) hotQuad.rotation = rot;
    }

    // 고리는 진행 방향에 수직인 면에서 ringTilt만큼 기울어지고, 기울어진 방향이 진행 방향을 축으로 돕니다.
    void UpdateRings(float deltaTime)
    {
        if (rings.Count == 0) return;
        spinAngle += settings.ringSpinSpeed * deltaTime;
        float phaseStep = 360f / Mathf.Max(1, rings.Count);
        for (int i = 0; i < rings.Count; i++)
        {
            if (rings[i] == null) continue;
            rings[i].localRotation = Quaternion.AngleAxis(spinAngle + phaseStep * i, Vector3.forward)
                                     * Quaternion.AngleAxis(settings.ringTilt, Vector3.right);
        }
    }

    // 번개 줄기 모양을 새로 뽑습니다(총알 앞쪽에서 뒤쪽으로 지그재그, 가운데가 가장 많이 꺾임).
    void RefreshBolts()
    {
        if (bolts.Count == 0) return;
        var s = settings;
        float worldScale = Mathf.Max(transform.lossyScale.x, 0.0001f);

        foreach (var bolt in bolts)
        {
            if (bolt == null) continue;
            int count = bolt.positionCount;
            float startZ = 0.4f;
            float endZ = 0.4f - s.lightningLength * Random.Range(0.6f, 1f);
            float spin = Random.Range(0f, 360f);
            for (int k = 0; k < count; k++)
            {
                float t = count > 1 ? (float)k / (count - 1) : 0f;
                float taper = Mathf.Sin(t * Mathf.PI); // 양 끝은 중심에 붙고 가운데가 가장 멀리
                Vector2 offset = Random.insideUnitCircle * (s.lightningJitter * (0.25f + taper));
                Vector3 local = new Vector3(offset.x, offset.y, Mathf.Lerp(startZ, endZ, t));
                bolt.SetPosition(k, Quaternion.AngleAxis(spin, Vector3.forward) * local);
            }
            bolt.widthMultiplier = s.lightningWidth * worldScale;
        }
    }

    Color ResolveCoreColor(Color fallback)
    {
        Renderer rend = GetComponent<Renderer>();
        if (rend == null)
        {
            foreach (var r in GetComponentsInChildren<Renderer>())
            {
                if (r.transform.IsChildOf(visualRoot)) continue;
                rend = r;
                break;
            }
        }
        if (rend != null && ProjectileColorOverride.TryGetRendererOverrideColor(rend, out Color c))
        {
            c.a = 1f;
            return c;
        }
        return fallback;
    }

    // ---- 조각 만들기 ----

    Transform CreateQuad(string name, Material mat, Color hdrColor, float alpha)
    {
        Transform t = CreateMeshChild(name, mat);
        hdrColor.a = alpha;
        SetColor(t.GetComponent<Renderer>(), hdrColor);
        return t;
    }

    Transform CreateMeshChild(string name, Material mat)
    {
        var go = new GameObject(name);
        go.transform.SetParent(visualRoot, false);
        go.AddComponent<MeshFilter>().sharedMesh = GetQuadMesh();
        var mr = go.AddComponent<MeshRenderer>();
        ConfigureRenderer(mr, mat);
        return go.transform;
    }

    LineRenderer CreateBolt(string name, Material mat, Color hdrColor)
    {
        var go = new GameObject(name);
        go.transform.SetParent(visualRoot, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.positionCount = 7;
        lr.alignment = LineAlignment.View;
        lr.numCornerVertices = 0;
        lr.numCapVertices = 0;
        lr.startColor = Color.white;
        lr.endColor = new Color(1f, 1f, 1f, 0.6f);
        ConfigureRenderer(lr, mat);
        SetColor(lr, hdrColor);
        return lr;
    }

    static void ConfigureRenderer(Renderer r, Material mat)
    {
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        r.sharedMaterial = mat;
    }

    // 재질은 공용이고, 조각마다 색(HDR)만 따로 넣습니다.
    internal static void SetColor(Renderer r, Color color)
    {
        if (r == null) return;
        var block = new MaterialPropertyBlock();
        r.GetPropertyBlock(block);
        block.SetColor(BaseColorId, color);
        block.SetColor(ColorId, color);
        r.SetPropertyBlock(block);
    }

    // ---- 공용 재질/메시/그림 ----

    // 부드러운 빛 그림(가운데가 밝고 바깥으로 옅어지는 원) + 더하기 섞기
    internal static Material GetAdditiveMaterial(Settings s)
    {
        if (s != null && s.additiveMaterial != null) return s.additiveMaterial;
        if (sharedAdditiveMaterial == null)
        {
            sharedAdditiveMaterial = CreateParticleMaterial("ChargedBullet_Additive", true, GetGlowTexture());
        }
        return sharedAdditiveMaterial;
    }

    // 꽉 찬 흰 그림 + 더하기 섞기(번개/꼬리)
    internal static Material GetSolidAdditiveMaterial(Settings s)
    {
        if (s != null && s.additiveMaterial != null) return s.additiveMaterial;
        if (sharedSolidAdditiveMaterial == null)
        {
            sharedSolidAdditiveMaterial = CreateParticleMaterial("ChargedBullet_SolidAdditive", true, Texture2D.whiteTexture);
        }
        return sharedSolidAdditiveMaterial;
    }

    static Material GetRingMaterial(Settings s)
    {
        if (ringMaterials.TryGetValue(s, out var cached) && cached != null) return cached;

        Texture2D ringTex = BuildRingTexture(s.ringColor, s.ringRimColor);
        Material mat;
        if (s.ringMaterial != null)
        {
            mat = new Material(s.ringMaterial) { name = "ChargedBullet_Ring (copy)" };
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", ringTex);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", ringTex);
        }
        else
        {
            mat = CreateParticleMaterial("ChargedBullet_Ring", false, ringTex);
        }
        ringMaterials[s] = mat;
        return mat;
    }

    // URP Particles/Unlit로 투명 재질을 만듭니다. additive = 더하기(빛), 아니면 일반 투명(고리).
    static Material CreateParticleMaterial(string name, bool additive, Texture texture)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                        ?? Shader.Find("Universal Render Pipeline/Unlit")
                        ?? Shader.Find("Sprites/Default");
        var mat = new Material(shader) { name = name };

        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", texture);
        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", texture);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);

        // 투명 설정(URP 재질 화면에서 Surface Type = Transparent, Blending Mode = Additive/Alpha로 고른 것과 같은 값)
        if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
        if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", additive ? 2f : 0f);
        if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 0f); // 양면
        if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);

        var src = UnityEngine.Rendering.BlendMode.SrcAlpha;
        var dst = additive ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha;
        if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)src);
        if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)dst);
        if (mat.HasProperty("_SrcBlendAlpha")) mat.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
        if (mat.HasProperty("_DstBlendAlpha")) mat.SetFloat("_DstBlendAlpha", (float)dst);

        mat.SetOverrideTag("RenderType", "Transparent");
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        return mat;
    }

    static Texture2D GetGlowTexture()
    {
        if (glowTexture != null) return glowTexture;

        const int size = 64;
        glowTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "ChargedBullet_Glow",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f;
                float dy = (y + 0.5f) / size * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1f - r);
                a = a * a * (3f - 2f * a); // 부드럽게
                pixels[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        }
        glowTexture.SetPixels(pixels);
        glowTexture.Apply();
        return glowTexture;
    }

    // 고리 그림: 어두운 띠 + 바깥 테두리에 밝은 선
    static Texture2D BuildRingTexture(Color band, Color rim)
    {
        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "ChargedBullet_Ring",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };
        const float inner = 0.62f, outer = 0.96f, rimWidth = 0.1f, soft = 0.04f;
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f;
                float dy = (y + 0.5f) / size * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);

                float bandMask = Mathf.Clamp01((r - inner) / soft) * Mathf.Clamp01((outer - r) / soft);
                float rimMask = Mathf.Clamp01(1f - Mathf.Abs(r - (outer - rimWidth * 0.5f)) / (rimWidth * 0.5f));

                Color c = Color.Lerp(band, rim, rimMask);
                c.a = Mathf.Max(band.a * bandMask, rim.a * rimMask * bandMask);
                pixels[y * size + x] = c;
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    // 1x1 크기의 사각형(가운데 기준, 면 = Z축). 정점 색을 흰색으로 넣어 둡니다(파티클 셰이더가 정점 색을 곱하므로).
    static Mesh GetQuadMesh()
    {
        if (quadMesh != null) return quadMesh;

        quadMesh = new Mesh { name = "ChargedBullet_Quad" };
        quadMesh.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
            new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f),
        };
        quadMesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
        quadMesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
        quadMesh.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
        quadMesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        quadMesh.RecalculateBounds();
        return quadMesh;
    }
}

// 강화 총알의 분홍 → 파랑 꼬리입니다. 총알의 자식이 아니라 따로 따라다니다가, 총알이 사라지면 새 꼬리를 멈추고 남은 꼬리가 옅어진 뒤
// 스스로 지워집니다(BulletTrail과 같은 이유 - 총알을 지우는 코드가 여러 곳에 있어 자식이면 꼬리가 뚝 끊김).
public class ChargedTrailFollower : MonoBehaviour
{
    private Transform target;
    private TrailRenderer trail;
    private bool released;

    public static void Spawn(Transform target, ChargedBulletVisual.Settings s, float intensity)
    {
        if (target == null || s == null) return;

        var go = new GameObject("ChargedBulletTrail");
        go.transform.position = target.position;

        var tr = go.AddComponent<TrailRenderer>();
        tr.time = s.trailTime;
        tr.minVertexDistance = 0.01f;
        tr.alignment = LineAlignment.View;
        tr.numCapVertices = 2;
        tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        tr.receiveShadows = false;
        tr.widthMultiplier = s.trailWidth * Mathf.Max(target.lossyScale.x, 0.0001f);
        tr.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.15f));

        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(s.trailHeadColor, 0f), new GradientColorKey(s.trailTailColor, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.7f, 0.4f), new GradientAlphaKey(0f, 1f) });
        tr.colorGradient = gradient;
        tr.sharedMaterial = ChargedBulletVisual.GetSolidAdditiveMaterial(s);
        ChargedBulletVisual.SetColor(tr, Color.white * (s.trailIntensity * intensity));
        tr.Clear();

        var follower = go.AddComponent<ChargedTrailFollower>();
        follower.target = target;
        follower.trail = tr;
    }

    void LateUpdate()
    {
        if (released) return;

        if (target == null || !target.gameObject.activeInHierarchy)
        {
            released = true;
            if (trail != null) trail.emitting = false;
            Destroy(gameObject, trail != null ? trail.time + 0.1f : 0f);
            return;
        }

        transform.position = target.position;
    }
}
