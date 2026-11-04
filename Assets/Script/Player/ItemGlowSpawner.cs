using UnityEngine;

// [보물상자 위 플레이어 아이템 아이콘 뒤 빛 이펙트]
// 동아리방 보물상자(ItemPedestal)든 일반방/보스방 보물상자(TreasureChest)든, 상자 위에 플레이어
// 아이템(패시브/액티브/장신구/일회용/음식) 아이콘이 떠 있는 동안 그 아이콘 뒤에서 빛나는 이펙트를
// 붙입니다. 기본 아이템(수류탄/해킹툴/크레딧/청휘석/체력/코스트) 묶음은 아이콘이 없으므로 빛나지
// 않습니다.
//
// [에디터 설정]
// 씬에 빈 오브젝트를 하나 만들고 이 컴포넌트를 붙인 뒤 Glow Prefab에 이펙트 프리팹(예:
// VFX/FreeQuickEffectsVol1/Prefabs/vfx_LootDrop_02)을 연결하세요. CombatVfxSpawner처럼 씬에
// 하나만 두면 됩니다(실제 게임 LevelBuilder 씬 + 테스트 ItemTestArena 씬 각각). 씬에 이
// 컴포넌트가 없으면 빛 이펙트 없이 기존과 100% 동일하게 동작합니다.
//
// [동작 방식 - 상자 쪽에서 매 프레임 Sync() 한 줄만 호출]
// TreasureChest/ItemPedestal은 아이템이 정해지거나(열기, 테스트 전시대), 교체로 다른 아이템이
// 다시 들어오거나(액티브/장신구/일회용/음식), 습득으로 비워지는 등 아이콘이 바뀌는 지점이 여러
// 곳이라, 그 지점마다 호출을 끼워 넣는 대신 각 상자의 LateUpdate에서 "지금 아이콘 + 지금 아이템"을
// Sync()에 넘기기만 합니다. Sync()는 이전 프레임과 같으면 아무것도 하지 않고, 아이템이 바뀌었을
// 때만 이펙트를 새로 만들거나(색 갱신) 지웁니다.
// - 이펙트는 아이콘 오브젝트의 자식으로 붙습니다. 아이템을 주워 상자가 아이콘을 지우면 빛도
//   자동으로 함께 사라집니다.
// - 인스펙터 값을 Play 중에 바꾸면 OnValidate가 버전을 올려, 다음 프레임에 떠 있는 빛들이 새
//   값으로 다시 만들어집니다(HUD들과 같은 라이브 트윅).
//
// [크기 - 월드 기준 배율]
// 아이콘은 아주 작은 스케일(기본 0.05)이라 그 자식으로 붙이면 이펙트도 같이 작아집니다. 그래서
// 부모(아이콘/상자) 스케일과 무관하게 "월드 기준으로 glowScale배"가 되도록 로컬 스케일을 역산해서
// 넣습니다. vfx_LootDrop 계열은 파티클 Scaling Mode가 Hierarchy라 Transform 스케일이 크기/
// 퍼지는 범위/속도에 그대로 반영됩니다. Scaling Mode가 Local/Shape인 다른 프리팹을 쓰는데 크기가
// 안 바뀌면 particleSizeMultiplier로 파티클 크기를 직접 곱하세요(CombatVfxSpawner와 같은 방식).
//
// [아이콘 "뒤"에 보이게 하는 방법 두 가지]
// 1) 위치: 아이콘은 카메라를 정면으로 보도록 회전되어 있어서 아이콘의 앞(forward) 방향 = 카메라에서
//    멀어지는 방향입니다. 그쪽으로 pushBehindDistance만큼 밀어 둡니다.
// 2) 그리기 순서: 이펙트 렌더러의 sortingOrder를 아이콘보다 낮게 맞춰, 거리와 무관하게 아이콘이
//    항상 이펙트 위에 그려지게 합니다(sortingOrderBelowIcon).
//
// [색 - 아이템 등급(Tier 0~3)별]
// vfx_LootDrop 계열은 각 파티클의 수명별 색(Color over Lifetime)이 전부 흰색이고 투명도만 바뀌어서,
// 실제 색은 Start Color 하나로 정해집니다. 그래서 각 파티클의 Start Color를 등급 색으로 바꿔 넣으면
// 이펙트 전체 색이 바뀝니다. keepOriginalAlpha가 켜져 있으면 파티클마다 원래 정해진 투명도(예:
// 퍼지는 원은 반투명)는 그대로 둡니다. 원본 프리팹 에셋은 건드리지 않습니다(생성된 복사본만 바꿈).
//
// [추가 이펙트 레이어 - 흩날리는 입자 등을 겹치기]
// 위의 기본 이펙트(Glow Prefab) 외에 Extra Layers 목록에 이펙트를 더 넣으면 같은 아이콘 뒤에 함께
// 겹쳐서 띄웁니다(예: vfx_Projectile_01의 흩날리는 입자). 레이어마다 크기/위치/회전/등급 색 적용
// 여부와, 입자 수명/속도/생성량/떠오르는 힘(음수 중력) 배율을 따로 조절할 수 있습니다. 또
// "Disabled Child Names"에 프리팹 안 부품(자식 오브젝트) 이름을 적으면 그 부품만 꺼서 쓰지 않습니다
// (예: vfx_Projectile_01에서 "Particle System"(둥근 빛 덩어리)과 "Trail"(날아갈 때만 보이는 꼬리)을
// 끄고 "Particle System (1)"(흩날리는 입자)만 남김 - 이름은 정확히 같아야 합니다).
// Trail/Line Renderer가 남아 있으면 그 색도 등급 색으로 바꿉니다.
// 목록에 새 칸을 추가하면 기본값(배율 1, 크기 0.12 등)이 자동으로 채워집니다.
[DisallowMultipleComponent]
public class ItemGlowSpawner : MonoBehaviour
{
    [Header("켜기/끄기")]
    [Tooltip("끄면 보물상자 아이템 아이콘 뒤 빛 이펙트를 전부 표시하지 않습니다.")]
    public bool enableGlow = true;

    [Header("이펙트 프리팹")]
    [Tooltip("아이콘 뒤에 띄울 이펙트 프리팹입니다(예: vfx_LootDrop_02). 비워두면 빛 이펙트를 표시하지 않습니다.")]
    public GameObject glowPrefab;

    [Header("크기 / 위치 / 회전")]
    [Tooltip("이펙트의 월드 기준 크기 배율입니다. 1이면 프리팹 원래 크기(vfx_LootDrop은 수 m 단위라 매우 큼)입니다. " +
             "아이콘 크기에 맞게 줄여서 쓰세요.")]
    [Min(0.001f)] public float glowScale = 0.12f;

    [Tooltip("Scaling Mode가 Hierarchy가 아니라서 위 glowScale로 크기가 안 바뀌는 프리팹을 쓸 때만 1이 아닌 값으로 " +
             "바꾸세요. 각 파티클의 시작 크기/속도에 직접 곱합니다.")]
    [Min(0.01f)] public float particleSizeMultiplier = 1f;

    [Tooltip("아이콘 중심 기준으로 이펙트 위치를 옮기는 값입니다(월드 기준, m). 예: (0, -0.2, 0)이면 아이콘보다 0.2m 아래.")]
    public Vector3 worldOffset = Vector3.zero;

    [Tooltip("이펙트를 아이콘 뒤(카메라에서 멀어지는 쪽)로 미는 거리(m)입니다.")]
    [Min(0f)] public float pushBehindDistance = 0.05f;

    [Tooltip("이펙트의 월드 기준 회전값입니다. (0,0,0)이면 프리팹이 만들어진 그대로 똑바로 섭니다(빛줄기가 위로, " +
             "퍼지는 원은 바닥과 평행).")]
    public Vector3 worldEulerAngles = Vector3.zero;

    [Header("그리기 순서")]
    [Tooltip("켜두면 이펙트 렌더러들의 Sorting Layer/Order를 아이콘과 같은 레이어, 아이콘보다 낮은 순서로 맞춰서 " +
             "아이콘이 항상 이펙트 위에 그려지게 합니다.")]
    public bool sortingOrderBelowIcon = true;

    [Header("등급(Tier 0~3)별 빛 색")]
    [Tooltip("켜면 아이템 등급에 따라 아래 색으로 바꿉니다. 끄면 프리팹 원래 색 그대로 씁니다.")]
    public bool useTierColors = true;
    // 기본값은 아이템 설명칸 이름 색(PlayerItemInfoDisplay - 씬에 설정된 값)과 같은 등급 색입니다:
    // Tier 0 흰색 / Tier 1 파랑 / Tier 2 노랑 / Tier 3 보라.
    [Tooltip("Tier 0 등급 빛 색입니다(기본 흰색).")]
    public Color tier0Color = new Color(1f, 1f, 1f, 1f);
    [Tooltip("Tier 1 등급 빛 색입니다(기본 파랑).")]
    public Color tier1Color = new Color(0f, 1f, 1f, 1f);
    [Tooltip("Tier 2 등급 빛 색입니다(기본 노랑).")]
    public Color tier2Color = new Color(1f, 0.92156863f, 0.015686275f, 1f);
    [Tooltip("Tier 3 등급 빛 색입니다(기본 보라).")]
    public Color tier3Color = new Color(1f, 0f, 1f, 1f);

    [Tooltip("켜두면 파티클마다 원래 정해진 투명도(알파)는 유지하고 색(RGB)만 바꿉니다. 끄면 위 색의 알파값을 그대로 씁니다.")]
    public bool keepOriginalAlpha = true;

    [Header("추가 이펙트 레이어 (기본 이펙트와 함께 겹쳐서 표시)")]
    [Tooltip("기본 이펙트(Glow Prefab) 위에 함께 겹칠 이펙트들입니다(예: 흩날리는 입자 vfx_Projectile_01). " +
             "칸마다 따로 조절할 수 있고, 등급 색은 위 Tier 0~3 색을 함께 씁니다.")]
    public System.Collections.Generic.List<GlowLayer> extraLayers = new System.Collections.Generic.List<GlowLayer>();

    [System.Serializable]
    public class GlowLayer
    {
        [Tooltip("끄면 이 레이어만 표시하지 않습니다.")]
        public bool enabled = true;
        [Tooltip("겹쳐서 띄울 이펙트 프리팹입니다. 비워두면 이 레이어는 무시됩니다.")]
        public GameObject prefab;

        [Header("크기 / 위치 / 회전")]
        [Tooltip("월드 기준 크기 배율입니다(기본 이펙트의 Glow Scale과 같은 의미).")]
        [Min(0.001f)] public float scale = 0.12f;
        [Tooltip("아이콘 중심 기준 위치 보정입니다(월드 기준, m).")]
        public Vector3 worldOffset = Vector3.zero;
        [Tooltip("아이콘 뒤(카메라에서 멀어지는 쪽)로 미는 거리(m)입니다.")]
        [Min(0f)] public float pushBehindDistance = 0.05f;
        [Tooltip("월드 기준 회전값입니다.")]
        public Vector3 worldEulerAngles = Vector3.zero;

        [Header("색")]
        [Tooltip("켜면 위 Tier 0~3 색을 이 레이어에도 적용합니다. 끄면 이 프리팹 원래 색 그대로 씁니다.")]
        public bool useTierColors = true;
        [Tooltip("켜두면 파티클마다 원래 투명도는 유지하고 색(RGB)만 바꿉니다.")]
        public bool keepOriginalAlpha = true;

        [Header("입자 움직임 (1이면 원본 그대로)")]
        [Tooltip("각 입자가 살아있는 시간 배율입니다. 키우면 더 멀리/오래 흩날립니다.")]
        [Min(0.01f)] public float lifetimeMultiplier = 1f;
        [Tooltip("입자가 튀어나가는 속도 배율입니다.")]
        [Min(0f)] public float speedMultiplier = 1f;
        [Tooltip("초당 생성되는 입자 수 배율입니다.")]
        [Min(0f)] public float emissionRateMultiplier = 1f;
        [Tooltip("입자 크기 배율입니다(Transform 크기와 별개로 파티클 시작 크기에 직접 곱함).")]
        [Min(0.01f)] public float particleSizeMultiplier = 1f;
        [Tooltip("0이면 원본 그대로입니다. 음수로 하면 입자가 위로 떠오르고(불티처럼), 양수로 하면 아래로 떨어집니다. " +
                 "원본 중력값을 이 값으로 덮어씁니다.")]
        public float gravityOverride = 0f;

        [Header("끌 부품")]
        [Tooltip("프리팹 안에서 끌 자식 오브젝트 이름 목록입니다(정확히 같은 이름만). 예: vfx_Projectile_01이면 " +
                 "\"Particle System\"(둥근 빛 덩어리), \"Trail\"(꼬리).")]
        public System.Collections.Generic.List<string> disabledChildNames = new System.Collections.Generic.List<string>();

        // 목록에 새 칸이 추가될 때(유니티는 첫 칸을 0/빈 값으로 만듦) 기본값을 채우기 위한 표시입니다.
        [SerializeField, HideInInspector] internal bool initialized = true;

        internal void ApplyDefaults()
        {
            enabled = true;
            scale = 0.12f;
            worldOffset = Vector3.zero;
            pushBehindDistance = 0.05f;
            worldEulerAngles = Vector3.zero;
            useTierColors = true;
            keepOriginalAlpha = true;
            lifetimeMultiplier = 1f;
            speedMultiplier = 1f;
            emissionRateMultiplier = 1f;
            particleSizeMultiplier = 1f;
            gravityOverride = 0f;
            if (disabledChildNames == null) disabledChildNames = new System.Collections.Generic.List<string>();
            initialized = true;
        }
    }

    // ---- 정적 접근 ----
    private static ItemGlowSpawner instance;
    private static bool searched;
    private static int settingsVersion;

    void Awake()
    {
        if (instance == null) instance = this;
    }

    void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
            searched = false;
        }
    }

    void OnValidate()
    {
        // 목록에 새로 추가된 칸(아직 기본값이 채워지지 않은 칸)에 기본값을 넣습니다.
        if (extraLayers != null)
        {
            foreach (var layer in extraLayers)
            {
                if (layer != null && !layer.initialized) layer.ApplyDefaults();
            }
        }

        // Play 중 인스펙터 값을 바꾸면 떠 있는 빛들을 새 값으로 다시 만들도록 버전을 올립니다.
        settingsVersion++;
    }

    static ItemGlowSpawner Instance
    {
        get
        {
            if (instance == null && !searched)
            {
                instance = FindFirstObjectByType<ItemGlowSpawner>();
                searched = true;
            }
            return instance;
        }
    }

    // 씬을 다시 불러올 때(게임 오버 후 재시작 등) 이전 씬 기준으로 "찾아봤는데 없었다"는 기록이
    // 남지 않도록 초기화합니다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        instance = null;
        searched = false;
        settingsVersion = 0;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void HookSceneLoaded()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        if (instance == null) searched = false;
    }

    // TreasureChest/ItemPedestal이 LateUpdate에서 매 프레임 호출합니다. icon은 상자 위 아이템
    // 아이콘(없으면 null), item은 지금 상자에 들어있는 플레이어 아이템(없으면 null)입니다.
    public static void Sync(SpriteRenderer icon, ItemDefinition item)
    {
        if (icon == null) return; // 아이콘이 없으면 빛도 없음(아이콘과 함께 이미 사라짐)

        var existing = icon.GetComponentInChildren<ItemGlowMarker>(true);
        var spawner = Instance;

        bool shouldShow = spawner != null && spawner.isActiveAndEnabled && spawner.enableGlow &&
                          item != null && spawner.HasAnyLayer();

        if (!shouldShow)
        {
            if (existing != null) Destroy(existing.gameObject);
            return;
        }

        if (existing != null && existing.item == item && existing.version == settingsVersion) return;

        if (existing != null) Destroy(existing.gameObject);
        spawner.SpawnGlow(icon, item);
    }

    bool HasAnyLayer()
    {
        if (glowPrefab != null) return true;
        if (extraLayers == null) return false;
        foreach (var layer in extraLayers)
        {
            if (layer != null && layer.enabled && layer.prefab != null) return true;
        }
        return false;
    }

    // 아이콘 아래에 빈 묶음 오브젝트(표식 포함)를 하나 만들고, 기본 이펙트 + 추가 레이어들을 그 안에
    // 생성합니다. 묶음 오브젝트는 부모(아이콘/상자) 스케일과 무관하게 월드 기준 크기 1이 되도록 맞춰서,
    // 각 이펙트의 크기 값이 그대로 월드 기준 배율이 됩니다.
    void SpawnGlow(SpriteRenderer icon, ItemDefinition item)
    {
        Transform iconT = icon.transform;

        var root = new GameObject("Item Glow");
        root.transform.SetParent(iconT, worldPositionStays: false);
        root.transform.localPosition = Vector3.zero;
        root.transform.rotation = Quaternion.identity;
        Vector3 parentScale = iconT.lossyScale;
        root.transform.localScale = new Vector3(
            1f / SafeScale(parentScale.x),
            1f / SafeScale(parentScale.y),
            1f / SafeScale(parentScale.z));

        var marker = root.AddComponent<ItemGlowMarker>();
        marker.item = item;
        marker.version = settingsVersion;

        Color tierColor = GetTierColor(item);

        // 기본 이펙트(기존 설정 그대로)
        if (glowPrefab != null)
        {
            GameObject glow = SpawnLayerObject(root.transform, iconT, glowPrefab, glowScale, worldOffset,
                                               pushBehindDistance, worldEulerAngles, null);
            ApplyParticleSettings(glow, useTierColors, tierColor, keepOriginalAlpha,
                                  particleSizeMultiplier, 1f, 1f, 1f, 0f);
            ApplySorting(glow, icon);
        }

        // 추가 레이어들
        if (extraLayers != null)
        {
            foreach (var layer in extraLayers)
            {
                if (layer == null || !layer.enabled || layer.prefab == null) continue;

                GameObject obj = SpawnLayerObject(root.transform, iconT, layer.prefab, layer.scale, layer.worldOffset,
                                                  layer.pushBehindDistance, layer.worldEulerAngles,
                                                  layer.disabledChildNames);
                ApplyParticleSettings(obj, layer.useTierColors, tierColor, layer.keepOriginalAlpha,
                                      layer.particleSizeMultiplier, layer.lifetimeMultiplier,
                                      layer.speedMultiplier, layer.emissionRateMultiplier, layer.gravityOverride);
                ApplySorting(obj, icon);
            }
        }
    }

    GameObject SpawnLayerObject(Transform root, Transform iconT, GameObject prefab, float scale, Vector3 offset,
                                float pushBehind, Vector3 eulerAngles,
                                System.Collections.Generic.List<string> disabledChildNames)
    {
        Vector3 position = iconT.position + offset + iconT.forward * pushBehind;
        GameObject obj = Instantiate(prefab, position, Quaternion.Euler(eulerAngles));
        obj.name = prefab.name + " (Item Glow)";
        obj.transform.SetParent(root, worldPositionStays: true);
        obj.transform.localScale = Vector3.one * scale; // 묶음이 월드 크기 1이라 그대로 월드 배율

        // 쓰지 않을 부품(자식 오브젝트)을 끕니다. 프리팹 맨 위(자기 자신)는 대상이 아닙니다.
        if (disabledChildNames != null && disabledChildNames.Count > 0)
        {
            foreach (var t in obj.GetComponentsInChildren<Transform>(true))
            {
                if (t == obj.transform) continue;
                if (disabledChildNames.Contains(t.name)) t.gameObject.SetActive(false);
            }
        }
        return obj;
    }

    static void ApplyParticleSettings(GameObject obj, bool recolor, Color tierColor, bool keepAlpha,
                                      float sizeMul, float lifetimeMul, float speedMul, float rateMul,
                                      float gravityOverride)
    {
        var systems = obj.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < systems.Length; i++)
        {
            var ps = systems[i];
            var main = ps.main;

            if (!Mathf.Approximately(sizeMul, 1f))
            {
                main.startSizeMultiplier *= sizeMul;
                if (main.startSize3D)
                {
                    main.startSizeXMultiplier *= sizeMul;
                    main.startSizeYMultiplier *= sizeMul;
                    main.startSizeZMultiplier *= sizeMul;
                }
            }
            if (!Mathf.Approximately(lifetimeMul, 1f)) main.startLifetimeMultiplier *= lifetimeMul;
            if (!Mathf.Approximately(speedMul, 1f)) main.startSpeedMultiplier *= speedMul;
            if (!Mathf.Approximately(gravityOverride, 0f)) main.gravityModifierMultiplier = gravityOverride;
            if (!Mathf.Approximately(rateMul, 1f))
            {
                var emission = ps.emission;
                emission.rateOverTimeMultiplier *= rateMul;
            }

            if (recolor)
            {
                Color original = main.startColor.mode == ParticleSystemGradientMode.Color
                    ? main.startColor.color
                    : main.startColor.colorMax;
                Color c = tierColor;
                if (keepAlpha) c.a = original.a;
                main.startColor = new ParticleSystem.MinMaxGradient(c);
            }

            // Prewarm된 파티클이 바뀐 색/크기로 처음부터 다시 나오도록 재시작합니다(꺼진 부품은 제외).
            if (ps.gameObject.activeInHierarchy)
            {
                ps.Clear(false);
                ps.Play(false);
            }
        }

        if (!recolor) return;

        // 꼬리(Trail)/선(Line) 렌더러가 남아 있으면 그 색도 등급 색으로 바꿉니다(투명도 변화는 유지).
        foreach (var trail in obj.GetComponentsInChildren<TrailRenderer>(true))
        {
            trail.colorGradient = TintGradient(trail.colorGradient, tierColor);
        }
        foreach (var line in obj.GetComponentsInChildren<LineRenderer>(true))
        {
            line.colorGradient = TintGradient(line.colorGradient, tierColor);
        }
    }

    static Gradient TintGradient(Gradient source, Color tint)
    {
        var g = new Gradient();
        var alphaKeys = source != null ? source.alphaKeys : new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) };
        var colorKeys = new[] { new GradientColorKey(tint, 0f), new GradientColorKey(tint, 1f) };
        g.SetKeys(colorKeys, alphaKeys);
        return g;
    }

    void ApplySorting(GameObject obj, SpriteRenderer icon)
    {
        if (!sortingOrderBelowIcon) return;
        foreach (var r in obj.GetComponentsInChildren<Renderer>(true))
        {
            if (r is SpriteRenderer) continue;
            r.sortingLayerID = icon.sortingLayerID;
            r.sortingOrder = icon.sortingOrder - 1;
        }
    }

    Color GetTierColor(ItemDefinition item)
    {
        switch (item.rarity)
        {
            case ItemDefinition.ItemRarity.Tier0: return tier0Color;
            case ItemDefinition.ItemRarity.Tier2: return tier2Color;
            case ItemDefinition.ItemRarity.Tier3: return tier3Color;
            default: return tier1Color;
        }
    }

    static float SafeScale(float s)
    {
        return Mathf.Abs(s) < 0.0001f ? 0.0001f : s;
    }
}
