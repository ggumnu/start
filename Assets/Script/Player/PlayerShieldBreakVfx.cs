using System.Collections.Generic;
using UnityEngine;

// 플레이어 보호막 연출입니다: 공격을 막아 1개 깎일 때(OnShieldBroken)와 얻을 때(OnShieldGained, 짧게) 몸을 감싸는 반투명 하늘색 "깎은 정이십면체"가
// 빠르게 커지며 발광했다가 줄어들며 사라짐. 면마다 투명/파랑/흰색을 랜덤하게 오가며 반짝임(셰이더 MobArchive/ShieldBreakIcosa). 메시는 코드로 한 번 만들어
// 공유하며, 2레벨 보스 보호막(BossShield)도 같은 메시/셰이더를 씁니다(GetSharedMesh). PlayerHealth가 없으면 자동으로 붙임.
// 주의: 연출 오브젝트는 플레이어 자식이 아님(피격 점멸 등 렌더러 모으기에 섞이지 않게). 설계 배경은 Docs/설계노트.md [보호막 연출].
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerHealth))]
public class PlayerShieldBreakVfx : MonoBehaviour
{
    [Header("셰이더")]
    [Tooltip("연출에 쓸 셰이더입니다. 비워두면 \"MobArchive/ShieldBreakIcosa\"를 이름으로 찾습니다.")]
    public Shader shader;

    [Header("면 반짝임 (유우카 EX 보호막 느낌)")]
    [Tooltip("가장 밝을 때 면 색입니다(알파 = 그때 불투명도). HDR이라 세기를 올리면 Bloom이 있는 카메라에서 번집니다.")]
    [ColorUsage(true, true)] public Color flickerWhite = new Color(1f, 1f, 1f, 0.85f);
    [Tooltip("중간 밝기일 때 면 색입니다(알파 = 그때 불투명도). 밝기가 0이면 완전히 투명해집니다.")]
    [ColorUsage(true, true)] public Color flickerBlue = new Color(0.35f, 0.72f, 1f, 0.45f);
    [Tooltip("면 밝기가 1초에 몇 번 새로 바뀌는지입니다(클수록 빠르게 반짝임).")]
    [Min(0f)] public float flickerSpeed = 7f;
    [Tooltip("면들이 얼마나 자주 보이는지입니다. 1 = 보통, 1보다 크면 투명한 면이 많아지고, 1보다 작으면 밝은 면이 많아집니다.")]
    [Min(0.05f)] public float flickerSharpness = 1.3f;
    [Tooltip("밝은 면이 추가로 빛나는 세기입니다(0 = 추가 발광 없음).")]
    [Min(0f)] public float flickerGlow = 0.6f;
    [Tooltip("면 가장자리가 투명하게 흐려지는 폭입니다(면 크기 대비 비율). 클수록 면 사이 틈이 넓고 부드러워집니다. 0이면 면이 딱 붙어 보임.")]
    [Range(0f, 0.9f)] public float rimFade = 0.3f;
    [Tooltip("안쪽(뒷면) 면의 불투명도 배율입니다. 낮을수록 앞쪽 면이 더 또렷해 보입니다.")]
    [Range(0f, 1f)] public float backFaceAlpha = 0.5f;

    [Header("크기 / 위치")]
    [Tooltip("플레이어 몸 범위(CharacterController)의 가장 긴 반지름에 곱할 배율입니다. 1이면 몸에 딱 맞고, 클수록 크게 감쌉니다.")]
    [Min(0.01f)] public float radiusMultiplier = 1.3f;
    [Tooltip("몸 중심에서 연출 중심을 옮기는 값입니다(월드 기준).")]
    public Vector3 positionOffset = Vector3.zero;

    [Header("전개 (확장 + 발광)")]
    [Tooltip("나타나는 순간의 크기 배율입니다(Radius Multiplier 기준 크기 x 이 값).")]
    [Min(0.01f)] public float startScale = 0.6f;
    [Tooltip("가장 크게 확장됐을 때의 크기 배율입니다.")]
    [Min(0.01f)] public float peakScale = 1.25f;
    [Tooltip("startScale에서 peakScale까지 확장하는 시간(초)입니다. 끝으로 갈수록 느려집니다.")]
    [Min(0.01f)] public float expandTime = 0.12f;
    [Tooltip("발광 색입니다(HDR - 세기를 1보다 키우면 Bloom이 켜진 카메라에서 빛이 번집니다).")]
    [ColorUsage(true, true)] public Color glowColor = Color.white;
    [Tooltip("발광 세기입니다(0 = 발광 없음). 나타나는 순간 이 세기로 시작해 glowTime 동안 0으로 줄어듭니다.")]
    [Min(0f)] public float glowIntensity = 1f;
    [Tooltip("발광이 사라지는 데 걸리는 시간(초)입니다.")]
    [Min(0.01f)] public float glowTime = 0.2f;
    [Tooltip("발광하는 동안 면을 이 불투명도까지 진하게 만듭니다(면이 반투명이라 발광이 잘 보이도록). 0이면 면 투명도는 그대로.")]
    [Range(0f, 1f)] public float glowFaceAlpha = 0.6f;

    [Header("소멸 (축소 + 투명)")]
    [Tooltip("연출 전체 시간(초)입니다. 확장이 끝난 뒤 남은 시간 동안 줄어들며 투명해집니다.")]
    [Min(0.05f)] public float duration = 0.55f;
    [Tooltip("사라질 때 마지막 크기 배율입니다(peakScale에서 이 값까지 줄어듦).")]
    [Min(0.01f)] public float endScale = 0.9f;
    [Tooltip("보호막을 얻었을 때 같은 연출을 재생하는 전체 시간(초)입니다(파괴 연출보다 짧게). 0이면 획득 연출을 재생하지 않습니다.")]
    [Min(0f)] public float acquireDuration = 0.5f;
    [Tooltip("초당 회전 각도입니다(천천히 돌아감). 0이면 회전하지 않습니다.")]
    public float rotationSpeed = 90f;

    private static Mesh sharedMesh;

    // 깎은 정이십면체 메시(반지름 1, 게임 시작 후 처음 부를 때 한 번 만들어 공유)입니다. 2레벨 보스 보호막(BossShield)도 씁니다.
    public static Mesh GetSharedMesh()
    {
        if (sharedMesh == null) sharedMesh = BuildTruncatedIcosahedron();
        return sharedMesh;
    }

    private PlayerHealth health;
    private CharacterController body;
    private GameObject instance;
    private Transform instanceTransform;
    private MeshRenderer instanceRenderer;
    private Material material;
    private MaterialPropertyBlock block;
    private float startTime = -1f;
    private Vector3 spinAxis = Vector3.up;
    private float seed;
    private float playDuration = 0.55f; // 지금 재생 중인 연출의 전체 시간(파괴 = duration, 획득 = acquireDuration)

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

    void Awake()
    {
        health = GetComponent<PlayerHealth>();
        body = GetComponent<CharacterController>();
    }

    void OnEnable()
    {
        if (health == null) health = GetComponent<PlayerHealth>();
        if (health != null)
        {
            health.OnShieldBroken += Play;
            health.OnShieldGained += HandleShieldGained;
        }
    }

    void OnDisable()
    {
        if (health != null)
        {
            health.OnShieldBroken -= Play;
            health.OnShieldGained -= HandleShieldGained;
        }
        Hide();
    }

    void OnDestroy()
    {
        if (instance != null) Destroy(instance);
        if (material != null) Destroy(material);
    }

    // 보호막이 깎인 순간 처음부터 재생합니다.
    public void Play()
    {
        PlayFor(duration);
    }

    // 보호막을 얻은 순간 짧게 재생합니다(바로 재생하라는 획득일 때만 - PlayerHealth.AddShield의 playGainVfx).
    private void HandleShieldGained(int gained, bool playNow)
    {
        if (playNow) PlayAcquire();
    }

    // 보호막 획득 연출을 짧게(acquireDuration) 재생합니다. 58번처럼 보호막을 먼저 주고 연출은 나중에 따로 띄울 때 직접 부릅니다.
    public void PlayAcquire()
    {
        if (acquireDuration > 0f) PlayFor(acquireDuration);
    }

    private void PlayFor(float totalDuration)
    {
        if (!EnsureInstance()) return;
        playDuration = Mathf.Max(0.05f, totalDuration);
        startTime = Time.time;
        seed = Random.Range(0f, 100f); // 재생할 때마다 다른 면들이 반짝이도록
        spinAxis = Random.onUnitSphere;
        instanceTransform.rotation = Random.rotationUniform;
        instance.SetActive(true);
        UpdateInstance();
    }

    void LateUpdate()
    {
        if (instance == null || !instance.activeSelf) return;
        UpdateInstance();
    }

    private void UpdateInstance()
    {
        float elapsed = Time.time - startTime;
        float total = Mathf.Max(0.05f, playDuration);
        if (startTime < 0f || elapsed >= total)
        {
            Hide();
            return;
        }

        float expand = Mathf.Min(Mathf.Max(0.01f, expandTime), total);

        // 크기: 확장(빠르게 커지다 끝에서 느려짐) -> 축소(남은 시간 동안 부드럽게)
        float scale;
        float alpha;
        if (elapsed < expand)
        {
            float k = elapsed / expand;
            float easeOut = 1f - (1f - k) * (1f - k) * (1f - k);
            scale = Mathf.LerpUnclamped(startScale, peakScale, easeOut);
            alpha = 1f;
        }
        else
        {
            float remain = Mathf.Max(0.0001f, total - expand);
            float k = Mathf.Clamp01((elapsed - expand) / remain);
            float smooth = k * k * (3f - 2f * k);
            scale = Mathf.LerpUnclamped(peakScale, endScale, smooth);
            alpha = 1f - smooth;
        }

        // 발광: 나타나는 순간 최대, glowTime 동안 줄어듦(빠르게 꺼지다 끝에서 느려짐)
        float g = Mathf.Clamp01(1f - elapsed / Mathf.Max(0.01f, glowTime));
        float glow = glowIntensity * g * g;

        // 위치/크기 - 플레이어 몸 범위 기준
        Bounds bounds = GetBodyBounds();
        float radius = Mathf.Max(bounds.extents.x, Mathf.Max(bounds.extents.y, bounds.extents.z)) * radiusMultiplier;
        instanceTransform.position = bounds.center + positionOffset;
        instanceTransform.localScale = Vector3.one * (radius * Mathf.Max(0.001f, scale));

        if (rotationSpeed != 0f) instanceTransform.Rotate(spinAxis, rotationSpeed * Time.deltaTime, Space.World);

        block.SetColor(WhiteId, flickerWhite);
        block.SetColor(BlueId, flickerBlue);
        block.SetFloat(SpeedId, flickerSpeed);
        block.SetFloat(SharpnessId, flickerSharpness);
        block.SetFloat(FlickerGlowId, flickerGlow);
        block.SetFloat(RimFadeId, rimFade);
        block.SetFloat(BackAlphaId, backFaceAlpha);
        block.SetFloat(FlickerTimeId, elapsed);
        block.SetFloat(SeedId, seed);
        block.SetFloat(AlphaId, alpha);
        block.SetColor(GlowColorId, glowColor);
        block.SetFloat(GlowId, glow);
        block.SetFloat(GlowFaceAlphaId, glowFaceAlpha);
        instanceRenderer.SetPropertyBlock(block);
    }

    private Bounds GetBodyBounds()
    {
        if (body == null) body = GetComponent<CharacterController>();
        if (body != null && body.enabled) return body.bounds;
        return new Bounds(transform.position + Vector3.up * 0.5f, Vector3.one);
    }

    private void Hide()
    {
        startTime = -1f;
        if (instance != null && instance.activeSelf) instance.SetActive(false);
    }

    private bool EnsureInstance()
    {
        if (instance != null) return true;

        Shader s = shader != null ? shader : Shader.Find("MobArchive/ShieldBreakIcosa");
        if (s == null)
        {
            Debug.LogWarning("PlayerShieldBreakVfx: 셰이더 MobArchive/ShieldBreakIcosa를 찾지 못해 보호막 파괴 연출을 건너뜁니다. " +
                             "Assets/Shaders/ShieldBreakIcosa.shader가 있는지 확인해주세요.");
            return false;
        }

        GetSharedMesh();

        material = new Material(s);
        block = new MaterialPropertyBlock();

        instance = new GameObject("ShieldBreakVfx");
        instanceTransform = instance.transform;
        instance.AddComponent<MeshFilter>().sharedMesh = sharedMesh;
        instanceRenderer = instance.AddComponent<MeshRenderer>();
        instanceRenderer.sharedMaterial = material;
        instanceRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        instanceRenderer.receiveShadows = false;
        instance.SetActive(false);
        return true;
    }

    // ---------------- 깎은 정이십면체 메시 만들기 ----------------

    // 반지름 1(중심에서 꼭짓점까지)인 깎은 정이십면체를 만듭니다. 면마다 꼭짓점을 따로 써서(평면 셰이딩) 면 경계가 또렷합니다.
    // uv.x = 면 중심 0 ~ 테두리 1 (가장자리 흐림용), uv.y = 면마다 다른 랜덤 값(면별 반짝임용)
    private static Mesh BuildTruncatedIcosahedron()
    {
        float phi = (1f + Mathf.Sqrt(5f)) * 0.5f;

        // 정이십면체 꼭짓점 12개(모서리 길이 2)
        var ico = new List<Vector3>();
        for (int a = -1; a <= 1; a += 2)
        {
            for (int b = -1; b <= 1; b += 2)
            {
                ico.Add(new Vector3(a, b * phi, 0f));
                ico.Add(new Vector3(0f, a, b * phi));
                ico.Add(new Vector3(b * phi, 0f, a));
            }
        }

        bool IsEdge(int i, int j) => Mathf.Abs((ico[i] - ico[j]).magnitude - 2f) < 1e-3f;
        Vector3 Near(int i, int j) => ico[i] + (ico[j] - ico[i]) / 3f; // i 쪽 1/3 지점

        var polygons = new List<Vector3[]>();

        // 육각형 20개 - 정이십면체 삼각형 면마다
        int n = ico.Count;
        for (int a = 0; a < n; a++)
            for (int b = a + 1; b < n; b++)
                for (int c = b + 1; c < n; c++)
                {
                    if (!IsEdge(a, b) || !IsEdge(b, c) || !IsEdge(a, c)) continue;
                    polygons.Add(new[] { Near(a, b), Near(b, a), Near(b, c), Near(c, b), Near(c, a), Near(a, c) });
                }

        // 오각형 12개 - 정이십면체 꼭짓점마다(이웃 5개를 둘레 순서로 정렬)
        for (int i = 0; i < n; i++)
        {
            Vector3 normal = ico[i].normalized;
            Vector3 t1 = Vector3.Cross(normal, Mathf.Abs(normal.z) < 0.9f ? Vector3.forward : Vector3.right).normalized;
            Vector3 t2 = Vector3.Cross(normal, t1);

            var neighbors = new List<int>();
            for (int j = 0; j < n; j++) if (j != i && IsEdge(i, j)) neighbors.Add(j);
            neighbors.Sort((x, y) =>
            {
                Vector3 dx = ico[x] - ico[i], dy = ico[y] - ico[i];
                return Mathf.Atan2(Vector3.Dot(dx, t2), Vector3.Dot(dx, t1))
                    .CompareTo(Mathf.Atan2(Vector3.Dot(dy, t2), Vector3.Dot(dy, t1)));
            });

            var pentagon = new Vector3[neighbors.Count];
            for (int k = 0; k < neighbors.Count; k++) pentagon[k] = Near(i, neighbors[k]);
            polygons.Add(pentagon);
        }

        // 반지름 1로 맞추기
        float maxLength = 0f;
        foreach (var poly in polygons) foreach (var v in poly) maxLength = Mathf.Max(maxLength, v.magnitude);
        float inv = maxLength > 0f ? 1f / maxLength : 1f;

        var vertices = new List<Vector3>();
        var uvs = new List<Vector2>();
        var normals = new List<Vector3>();
        var triangles = new List<int>();

        var faceRandom = new System.Random(20261029); // 면마다 고정된 랜덤 값(uv.y) - 반짝임 타이밍을 면별로 다르게
        foreach (var poly in polygons)
        {
            float faceId = (float)faceRandom.NextDouble();
            Vector3 center = Vector3.zero;
            foreach (var v in poly) center += v;
            center /= poly.Length;
            Vector3 faceNormal = center.normalized;

            int centerIndex = vertices.Count;
            vertices.Add(center * inv); uvs.Add(new Vector2(0f, faceId)); normals.Add(faceNormal);
            for (int k = 0; k < poly.Length; k++)
            {
                vertices.Add(poly[k] * inv); uvs.Add(new Vector2(1f, faceId)); normals.Add(faceNormal);
            }

            // 부채꼴 삼각형 - 바깥에서 봤을 때 시계방향이 되도록 감는 방향을 맞춥니다(셰이더는 앞/뒷면 둘 다 그림).
            for (int k = 0; k < poly.Length; k++)
            {
                int i0 = centerIndex + 1 + k;
                int i1 = centerIndex + 1 + (k + 1) % poly.Length;
                Vector3 cross = Vector3.Cross(poly[(k + 1) % poly.Length] - center, poly[k] - center);
                if (Vector3.Dot(cross, faceNormal) >= 0f)
                {
                    triangles.Add(centerIndex); triangles.Add(i1); triangles.Add(i0);
                }
                else
                {
                    triangles.Add(centerIndex); triangles.Add(i0); triangles.Add(i1);
                }
            }
        }

        var mesh = new Mesh { name = "TruncatedIcosahedron" };
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetNormals(normals);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        return mesh;
    }
}
