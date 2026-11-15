using System.Collections.Generic;
using UnityEngine;

// [2레벨 보스 포격 - 포격 하나] BossLv2AIController가 보호막이 있는 동안 하나씩 만듭니다(직접 배치할 필요 없음).
// 바닥에 조준 표시(주황 반투명 원 + 테두리 + 안쪽 원 + 십자선)와 하늘로 점멸하는 레이저를 띄우고, Telegraph Time 뒤에 터집니다.
// 마지막 Fall Time 동안 하늘에서 포탄(플레이어 수류탄 모델)이 떨어집니다. 폭발은 플레이어만 맞습니다(원 안이면 Damage) -
// 74번 돌진 중이면 피함, 플레이어 보호막이 막음(PlayerHealth.TakeDamage), 57번(자기 수류탄 피해 무효)으로는 못 막음. 적/보스는 맞지 않습니다.
// 폭발 이펙트는 플레이어 수류탄 폭발 이펙트(57번 외형 X). 플레이어가 다른 방에 들어가거나 보스가 죽거나 90번 물결이 닿으면(ClearInRadius) 터지지 않고 사라집니다.
// 118번 포격(ArtilleryStrike)과 같은 모양이지만 플레이어 수류탄/아이템과 엮이지 않도록 따로 만든 적 전용입니다. 설계 배경은 Docs/설계노트.md [2레벨 보스].
public class BossArtilleryStrike : MonoBehaviour
{
    [System.Serializable]
    public class Settings
    {
        [Header("시간")]
        [Tooltip("조준 표시가 뜬 뒤 터지기까지의 시간(초).")]
        [Min(0.1f)] public float telegraphTime = 1f;
        [Tooltip("포탄이 하늘에서 떨어지는 시간(초) - 조준 시간의 마지막 부분.")]
        [Min(0.05f)] public float fallTime = 0.5f;

        [Header("포탄 (수류탄 모델)")]
        [Tooltip("포탄이 떨어지기 시작하는 높이(바닥 기준, m).")]
        [Min(0.5f)] public float dropHeight = 12f;
        [Tooltip("포탄 크기 = 수류탄 모델 크기 x 이 값.")]
        [Min(0.01f)] public float shellScale = 1.2f;

        [Header("조준 표시 (바닥)")]
        public Color fillColor = new Color(1f, 0.45f, 0.05f, 0.22f);
        public Color lineColor = new Color(1f, 0.5f, 0.08f, 0.95f);
        [Min(0.001f)] public float outlineWidth = 0.06f;
        [Range(0.05f, 0.95f)] public float innerRingRatio = 0.5f;
        [Min(0.001f)] public float innerLineWidth = 0.04f;
        [Range(0.05f, 1f)] public float crossRatio = 0.3f;
        [Tooltip("바닥에서 띄우는 높이(m).")]
        public float groundOffset = 0.035f;

        [Header("레이저 (원 중심 → 하늘, 빠르게 점멸)")]
        public Color laserColor = new Color(1f, 0.5f, 0.08f, 0.85f);
        [Min(0.001f)] public float laserWidth = 0.05f;
        [Min(0.5f)] public float laserHeight = 20f;
        [Min(0.01f)] public float laserBlinkInterval = 0.06f;
        [Range(0f, 1f)] public float laserMinBrightness = 0.1f;

        [Header("폭발 이펙트")]
        [Tooltip("비워두면 플레이어 수류탄 폭발 이펙트(PlayerGrenadeThrower의 Explosion Vfx Prefab)를 그 크기/소멸 시간 그대로 씁니다.")]
        public GameObject explosionVfxOverride;
        [Tooltip("위 칸을 채웠을 때만 쓰는 크기 배율/소멸 시간(초).")]
        [Min(0.01f)] public float overrideVfxScale = 1f;
        [Min(0.1f)] public float overrideVfxLifetime = 2f;
    }

    private const int CircleSegments = 64;
    private static Mesh sharedDiscMesh;
    private static readonly List<BossArtilleryStrike> active = new List<BossArtilleryStrike>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        sharedDiscMesh = null;
        active.Clear();
    }

    // 지금 떠 있는(아직 터지지 않은) 포격 수 - 90번 사용 조건.
    public static int ActiveCount
    {
        get
        {
            active.RemoveAll(a => a == null || a.exploded);
            return active.Count;
        }
    }

    // [90번 물결] 중심에서 수평 거리 radius 안에 조준 중심이 있는 포격을 터뜨리지 않고 없앱니다(떨어지던 포탄 포함). 지운 자리를 positions에 담음.
    public static int ClearInRadius(Vector3 center, float radius, List<Vector3> positions)
    {
        int count = 0;
        float sqr = radius * radius;
        for (int i = active.Count - 1; i >= 0; i--)
        {
            var strike = active[i];
            if (strike == null || strike.exploded)
            {
                active.RemoveAt(i);
                continue;
            }
            float dx = strike.center.x - center.x;
            float dz = strike.center.z - center.z;
            if (dx * dx + dz * dz > sqr) continue;

            active.RemoveAt(i);
            strike.exploded = true; // 이번 프레임에 더 진행/폭발하지 않게
            positions?.Add(strike.center);
            Destroy(strike.gameObject);
            count++;
        }
        return count;
    }

    private Settings settings;
    private EnemyHealth owner;
    private Vector3 center;
    private float radius;
    private float damage;
    private GameObject shellPrefab;
    private float timer;
    private bool exploded;

    private GameObject markerRoot;
    private Material fillMaterial, lineMaterial, laserMaterial, shellMaterial;
    private GameObject shell;
    private float blinkTimer;
    private bool laserBright = true;

    // 포격 하나를 시작합니다. owner(보스)가 죽으면 터지지 않고 사라집니다. shellPrefab이 비면 플레이어 수류탄 모델(그것도 없으면 구체).
    public static BossArtilleryStrike Create(Settings settings, EnemyHealth owner, Vector3 groundPoint, float radius, float damage, GameObject shellPrefab)
    {
        if (settings == null || radius <= 0f) return null;

        var go = new GameObject("BossArtilleryStrike");
        go.transform.position = groundPoint;
        var strike = go.AddComponent<BossArtilleryStrike>();
        strike.settings = settings;
        strike.owner = owner;
        strike.center = groundPoint;
        strike.radius = radius;
        strike.damage = damage;
        strike.shellPrefab = shellPrefab;
        active.Add(strike);

        // [방 전환] 플레이어가 다른 방에 들어가면 바로 지워집니다(RoomScope).
        RoomScope.DestroyWhenPlayerLeaves(go);

        try
        {
            strike.BuildMarker();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"BossArtilleryStrike: 조준 표시 생성 중 오류가 발생해 표시 없이 진행합니다: {e}");
        }
        GameAudio.Play(SfxId.EnemyAttackWarn); // [사운드] 포격 조준 예고
        return strike;
    }

    void Update()
    {
        if (exploded) return;
        if (owner == null || owner.IsDead)
        {
            Destroy(gameObject);
            return;
        }

        timer += Time.deltaTime;
        float telegraph = Mathf.Max(settings.telegraphTime, 0.1f);
        float fall = Mathf.Clamp(settings.fallTime, 0.05f, telegraph);

        if (shell == null && timer >= telegraph - fall) BeginShell();
        UpdateShell(Mathf.InverseLerp(telegraph - fall, telegraph, timer));
        UpdateLaser();

        if (timer >= telegraph) Explode();
    }

    void OnDestroy()
    {
        active.Remove(this);
        if (fillMaterial != null) Destroy(fillMaterial);
        if (lineMaterial != null) Destroy(lineMaterial);
        if (laserMaterial != null) Destroy(laserMaterial);
        if (shellMaterial != null) Destroy(shellMaterial);
    }

    // ---------- 폭발 ----------

    private void Explode()
    {
        exploded = true;
        GameAudio.Play(SfxId.BossArtilleryExplode);
        try
        {
            DamagePlayer();
            SpawnExplosionVfx();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"BossArtilleryStrike: 포격 폭발 처리 중 오류가 발생했습니다: {e}");
        }
        Destroy(gameObject);
    }

    // 원(수평 거리) 안의 플레이어만 맞습니다. 74번 돌진 중이면 피함. 보호막/무적은 PlayerHealth.TakeDamage가 처리(57번은 상관없음).
    private void DamagePlayer()
    {
        if (damage <= 0f) return;
        if (PlayerDash.IsDodging) return;

        // 태그로 찾으면 "Player" 태그가 붙은 자식(FirePoint 등)이 잡혀 PlayerHealth를 못 찾을 수 있음(빌드에서 피해가 빠짐) - 루트를 직접 찾음.
        PlayerHealth health = Object.FindFirstObjectByType<PlayerHealth>();
        if (health == null || health.IsDead) return;
        GameObject playerObj = health.gameObject;

        Vector3 p = playerObj.transform.position;
        Vector2 flat = new Vector2(p.x - center.x, p.z - center.z);
        if (flat.magnitude <= radius) health.TakeDamage(damage);
    }

    private void SpawnExplosionVfx()
    {
        GameObject prefab = settings.explosionVfxOverride;
        float scale = settings.overrideVfxScale;
        float lifetime = settings.overrideVfxLifetime;
        Vector3 offset = Vector3.zero;

        if (prefab == null)
        {
            var thrower = FindFirstObjectByType<PlayerGrenadeThrower>();
            if (thrower == null || thrower.explosionVfxPrefab == null) return;
            prefab = thrower.explosionVfxPrefab;
            scale = thrower.explosionVfxScale * thrower.GetRadiusVfxFactor(radius);
            lifetime = thrower.explosionVfxLifetime;
            offset = thrower.explosionVfxOffset;
        }

        GameObject vfx = Instantiate(prefab, center + offset, Quaternion.identity);
        RoomScope.DestroyWhenPlayerLeaves(vfx);
        ScaleVfx(vfx, scale);
        if (lifetime > 0f) Destroy(vfx, lifetime);
    }

    // PlayerGrenadeThrower와 같은 방식(파티클 시작 크기/속도 배율, 파티클이 없으면 localScale).
    private static void ScaleVfx(GameObject instance, float multiplier)
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

    // ---------- 표시 ----------

    private void BuildMarker()
    {
        markerRoot = new GameObject("BossArtilleryLockOn");
        markerRoot.transform.SetParent(transform, false);
        markerRoot.transform.position = center + Vector3.up * settings.groundOffset;

        if (sharedDiscMesh == null) sharedDiscMesh = GrenadeAimIndicator.BuildUnitDiscMesh(CircleSegments);

        fillMaterial = GrenadeAimIndicator.BuildMaterial("BossArtilleryFill_Material", settings.fillColor);
        var fill = new GameObject("Fill");
        fill.transform.SetParent(markerRoot.transform, false);
        fill.transform.localScale = new Vector3(radius, 1f, radius);
        fill.AddComponent<MeshFilter>().sharedMesh = sharedDiscMesh;
        ConfigureRenderer(fill.AddComponent<MeshRenderer>(), fillMaterial);

        lineMaterial = GrenadeAimIndicator.BuildMaterial("BossArtilleryLine_Material", settings.lineColor);
        AddCircle("Outline", radius, settings.outlineWidth);
        AddCircle("InnerRing", radius * settings.innerRingRatio, settings.innerLineWidth);

        float half = radius * settings.crossRatio;
        AddLine("CrossX", new Vector3(-half, 0.002f, 0f), new Vector3(half, 0.002f, 0f), settings.innerLineWidth);
        AddLine("CrossZ", new Vector3(0f, 0.002f, -half), new Vector3(0f, 0.002f, half), settings.innerLineWidth);

        laserMaterial = GrenadeAimIndicator.BuildMaterial("BossArtilleryLaser_Material", settings.laserColor);
        var laserGO = new GameObject("Laser");
        laserGO.transform.SetParent(markerRoot.transform, false);
        var laser = laserGO.AddComponent<LineRenderer>();
        laser.useWorldSpace = false;
        laser.alignment = LineAlignment.View;
        laser.startWidth = settings.laserWidth;
        laser.endWidth = settings.laserWidth;
        laser.positionCount = 2;
        laser.SetPosition(0, Vector3.zero);
        laser.SetPosition(1, Vector3.up * settings.laserHeight);
        ConfigureRenderer(laser, laserMaterial);
    }

    private void AddCircle(string name, float r, float width)
    {
        var go = new GameObject(name);
        go.transform.SetParent(markerRoot.transform, false);
        var line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = true;
        line.alignment = LineAlignment.View;
        line.numCornerVertices = 2;
        line.startWidth = width;
        line.endWidth = width;
        line.positionCount = CircleSegments;
        for (int i = 0; i < CircleSegments; i++)
        {
            float a = (float)i / CircleSegments * Mathf.PI * 2f;
            line.SetPosition(i, new Vector3(Mathf.Cos(a) * r, 0.002f, Mathf.Sin(a) * r));
        }
        ConfigureRenderer(line, lineMaterial);
    }

    private void AddLine(string name, Vector3 a, Vector3 b, float width)
    {
        var go = new GameObject(name);
        go.transform.SetParent(markerRoot.transform, false);
        var line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.alignment = LineAlignment.View;
        line.startWidth = width;
        line.endWidth = width;
        line.positionCount = 2;
        line.SetPosition(0, a);
        line.SetPosition(1, b);
        ConfigureRenderer(line, lineMaterial);
    }

    private static void ConfigureRenderer(Renderer rend, Material mat)
    {
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rend.receiveShadows = false;
        rend.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        rend.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        rend.sharedMaterial = mat;
    }

    private void UpdateLaser()
    {
        if (laserMaterial == null) return;
        blinkTimer += Time.deltaTime;
        if (blinkTimer < settings.laserBlinkInterval) return;
        blinkTimer = 0f;
        laserBright = !laserBright;
        Color col = settings.laserColor;
        col.a *= laserBright ? 1f : settings.laserMinBrightness;
        laserMaterial.color = col;
    }

    // 포탄을 하늘(중심 위 dropHeight)에 만듭니다. 연출이라 오류가 나도 폭발은 그대로 진행합니다.
    private void BeginShell()
    {
        if (shell != null) return;
        try
        {
            shell = BossArtilleryShellModel.Create(shellPrefab, settings.shellScale, out shellMaterial);
            if (shell == null) return;
            shell.name = "BossArtilleryShell";
            shell.transform.SetParent(transform, true);
            UpdateShell(0f);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"BossArtilleryStrike: 포탄 생성 중 오류가 발생해 포탄 없이 진행합니다(폭발에는 영향 없음): {e}");
        }
    }

    // u = 0(하늘) ~ 1(바닥). 점점 빨라지게 떨어집니다.
    private void UpdateShell(float u)
    {
        if (shell == null) return;
        u = Mathf.Clamp01(u);
        float height = settings.dropHeight * (1f - u * u);
        shell.transform.position = new Vector3(center.x, center.y + height, center.z);
    }
}

// [2레벨 보스 포격] 포탄/발사 연출용 수류탄 모델을 만듭니다. prefab이 비면 플레이어 수류탄 모델(PlayerGrenadeThrower.grenadePrefab), 그것도 없으면
// 그 임시 구체. 스스로 날아가거나 무언가에 부딪히지 않도록 Grenade/콜라이더/물리를 끕니다. 57번 등 플레이어 외형은 입히지 않습니다.
public static class BossArtilleryShellModel
{
    public static GameObject Create(GameObject prefab, float scale, out Material createdMaterial)
    {
        createdMaterial = null;
        var thrower = Object.FindFirstObjectByType<PlayerGrenadeThrower>();
        if (prefab == null && thrower != null) prefab = thrower.grenadePrefab;

        GameObject model;
        if (prefab != null)
        {
            model = Object.Instantiate(prefab);
            model.transform.localScale = prefab.transform.localScale * scale;
        }
        else
        {
            model = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            float size = thrower != null ? Mathf.Max(thrower.fallbackSphereSize, 0.01f) : 0.3f;
            model.transform.localScale = Vector3.one * size * scale;
            var rend = model.GetComponent<Renderer>();
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Sprites/Default");
            if (rend != null && shader != null)
            {
                createdMaterial = new Material(shader) { name = "BossArtilleryShell_Material",
                                                         color = thrower != null ? thrower.fallbackSphereColor : new Color(0.12f, 0.3f, 0.12f, 1f) };
                rend.sharedMaterial = createdMaterial;
            }
        }

        foreach (var g in model.GetComponentsInChildren<Grenade>(true)) g.enabled = false;
        foreach (var col in model.GetComponentsInChildren<Collider>(true)) col.enabled = false;
        foreach (var rb in model.GetComponentsInChildren<Rigidbody>(true)) { rb.isKinematic = true; rb.useGravity = false; }
        return model;
    }
}

// [2레벨 보스 포격 발사 연출] 보스가 하늘로 쏘아 올리는 수류탄 하나 - 곧게 위로 날아가다 시작 높이보다 Despawn Height만큼 올라가면 지워집니다
// (피해 없음, 연출만). 플레이어가 다른 방에 들어가면 바로 지워집니다.
public class BossArtilleryLaunchShell : MonoBehaviour
{
    private Vector3 velocity;
    private float despawnY;
    private float lifeLeft;
    private Material createdMaterial;

    public static void Launch(GameObject prefab, float scale, Vector3 origin, Vector3 direction, float speed, float despawnHeight)
    {
        GameObject model = BossArtilleryShellModel.Create(prefab, scale, out Material mat);
        if (model == null) return;
        model.name = "BossArtilleryLaunchShell";
        model.transform.position = origin;
        if (direction.sqrMagnitude > 0.0001f) model.transform.rotation = Quaternion.LookRotation(direction.normalized);

        var shell = model.AddComponent<BossArtilleryLaunchShell>();
        shell.velocity = direction.normalized * Mathf.Max(speed, 0.1f);
        shell.despawnY = origin.y + Mathf.Max(despawnHeight, 0.5f);
        shell.lifeLeft = 5f; // 혹시 위로 못 가는 방향이어도 남지 않게
        shell.createdMaterial = mat;
        RoomScope.DestroyWhenPlayerLeaves(model);
    }

    void Update()
    {
        transform.position += velocity * Time.deltaTime;
        lifeLeft -= Time.deltaTime;
        if (transform.position.y >= despawnY || lifeLeft <= 0f) Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (createdMaterial != null) Destroy(createdMaterial);
    }
}
