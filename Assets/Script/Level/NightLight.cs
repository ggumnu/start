using UnityEngine;
using UnityEngine.Rendering;

// [밤 전용 빛 - 벽/바닥 프리팹 안의 빈 오브젝트에 붙입니다]
// 밤 레벨(LevelLightingProfile.isNight가 켜진 레벨)에서만 보이고, 그 외 레벨(n.5 포함)에서는 숨겨집니다.
// 메시는 실행 중에 직접 만들어 자식 오브젝트로 붙입니다(텍스처/모델 불필요). 그림자/충돌 없음.
//   창문:   사각 판 1장(로컬 +Z 방향을 바라봄). 인스턴스마다 '켜질 확률'로 한 번 굴려 켜진 창/꺼진 창이 섞입니다.
//   자판기: 스크린 사각 판 + 앞쪽 바닥으로 번지는 사각 판(로컬 +Z 쪽으로 뻗고 멀어질수록 옅어짐).
//   가로등: 이 오브젝트 위치(전구 위치)에서 아래로 뻗는 빛 원뿔 + 바닥 원형 빛 웅덩이 + 카메라를 바라보는 전구 빛.
// 위치/크기는 씬 뷰에서 이 오브젝트를 선택하면 노란 선으로 미리 보입니다.
// 오른쪽 클릭 메뉴(컴포넌트 ⋮)의 "기본값: 창문/자판기/가로등"으로 종류와 색을 한 번에 채울 수 있습니다.
[DisallowMultipleComponent]
public class NightLight : MonoBehaviour
{
    public enum Kind
    {
        Window,
        VendingScreen,
        StreetLight
    }

    [Header("공통")]
    [Tooltip("빛 종류")]
    public Kind kind = Kind.Window;

    [Tooltip("밤 레벨에서 이 빛이 켜질 확률(0~1). 방이 만들어질 때 한 번만 굴립니다. 창문은 0.6 정도, 자판기/가로등은 1 권장.")]
    [Range(0f, 1f)]
    public float lightChance = 1f;

    [Tooltip("빛 색(HDR). 1보다 밝게 하면 Bloom으로 번집니다.")]
    [ColorUsage(false, true)]
    public Color color = new Color(1f, 0.8f, 0.5f, 1f);

    [Tooltip("MobArchive/NightGlow 셰이더를 쓴 머티리얼. 비우면 셰이더를 직접 찾아 씁니다(빌드에서는 꼭 지정 권장).")]
    public Material glowMaterial;

    [Header("사각 판 (창문 유리 / 자판기 스크린)")]
    [Tooltip("판 가로 크기(m)")]
    public float panelWidth = 1f;
    [Tooltip("판 세로 크기(m)")]
    public float panelHeight = 1f;
    [Tooltip("판 중심 위치(이 오브젝트 기준 로컬 좌표). 판은 로컬 +Z 방향을 바라봅니다. 벽에 묻히지 않게 Z를 살짝(0.01~0.02) 앞으로.")]
    public Vector3 panelOffset = new Vector3(0f, 0f, 0.02f);
    [Tooltip("판 밝기")]
    public float panelIntensity = 1.5f;
    [Tooltip("판 가장자리 부드러움(0.001~0.5)")]
    [Range(0.001f, 0.5f)]
    public float panelSoftness = 0.04f;

    [Header("바닥 번짐 (자판기 전용)")]
    [Tooltip("번짐 가로 크기(m)")]
    public float spillWidth = 1.4f;
    [Tooltip("번짐이 앞(로컬 +Z)으로 뻗는 길이(m)")]
    public float spillLength = 1.5f;
    [Tooltip("번짐 시작 위치(로컬). 보통 자판기 앞면 바로 아래 바닥(Y는 바닥보다 0.02 정도 위).")]
    public Vector3 spillOffset = new Vector3(0f, 0.02f, 0f);
    [Tooltip("번짐 밝기")]
    public float spillIntensity = 0.5f;
    [Tooltip("번짐 가장자리 부드러움")]
    [Range(0.001f, 0.5f)]
    public float spillSoftness = 0.35f;
    [Tooltip("멀어질수록 옅어지는 정도(0 = 고르게, 1 = 끝에서 0)")]
    [Range(0f, 1f)]
    public float spillFade = 1f;

    [Header("가로등 (이 오브젝트 위치 = 전구 위치)")]
    [Tooltip("빛 원뿔 높이(m) - 전구에서 바닥까지의 거리로 맞추면 됩니다.")]
    public float coneHeight = 3f;
    [Tooltip("원뿔 위쪽(전구 쪽) 반지름(m)")]
    public float coneTopRadius = 0.15f;
    [Tooltip("원뿔 아래쪽(바닥 쪽) 반지름(m)")]
    public float coneBottomRadius = 1.4f;
    [Tooltip("원뿔 둘레 분할 수")]
    [Range(8, 48)]
    public int coneSegments = 24;
    [Tooltip("원뿔 밝기")]
    public float coneIntensity = 0.35f;
    [Tooltip("원뿔이 아래로 옅어지는 빠르기")]
    [Range(0.2f, 6f)]
    public float coneFalloff = 1.3f;
    [Tooltip("원뿔 옆 가장자리가 옅어지는 정도")]
    [Range(0.1f, 6f)]
    public float coneEdgePower = 1.5f;
    [Tooltip("바닥 빛 웅덩이 반지름(m)")]
    public float poolRadius = 1.8f;
    [Tooltip("빛 웅덩이를 원뿔 바닥보다 얼마나 위에 띄울지(m). 바닥에 깜빡이며 묻히면 늘리세요.")]
    public float poolLift = 0.02f;
    [Tooltip("빛 웅덩이 밝기")]
    public float poolIntensity = 0.6f;
    [Tooltip("빛 웅덩이가 바깥으로 옅어지는 빠르기")]
    [Range(0.2f, 6f)]
    public float poolFalloff = 1.5f;
    [Tooltip("전구 빛 크기(m, 지름). 0이면 만들지 않습니다.")]
    public float bulbSize = 0.6f;
    [Tooltip("전구 빛 밝기")]
    public float bulbIntensity = 2f;
    [Tooltip("전구 빛이 바깥으로 옅어지는 빠르기")]
    [Range(0.2f, 6f)]
    public float bulbFalloff = 2f;

    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
    private static readonly int ShapeId = Shader.PropertyToID("_Shape");
    private static readonly int SoftnessId = Shader.PropertyToID("_Softness");
    private static readonly int FadeVId = Shader.PropertyToID("_FadeV");
    private static readonly int FalloffId = Shader.PropertyToID("_Falloff");
    private static readonly int EdgePowerId = Shader.PropertyToID("_EdgePower");

    private const float ShapeRect = 0f;
    private const float ShapeRadial = 1f;
    private const float ShapeCone = 2f;

    private static Material fallbackMaterial;
    private static bool warnedMissingShader;

    private bool rolled;
    private bool rolledOn;
    private bool built;
    private GameObject root;
    private Transform bulb;
    private Mesh[] ownedMeshes = new Mesh[5];
    private int ownedMeshCount;
    private MaterialPropertyBlock block;
    private Camera cachedCamera;
    private bool pendingRebuild;

    private void Awake()
    {
        RollIfNeeded();
    }

    private void OnEnable()
    {
        LevelLighting.OnChanged += RefreshVisible;
        RefreshVisible();
    }

    private void OnDisable()
    {
        LevelLighting.OnChanged -= RefreshVisible;
    }

    private void OnDestroy()
    {
        DestroyBuilt();
    }

    private void LateUpdate()
    {
        if (pendingRebuild)
        {
            pendingRebuild = false;
            bool wasVisible = root != null && root.activeSelf;
            Build();
            if (root != null)
                root.SetActive(wasVisible);
        }

        if (bulb == null || root == null || !root.activeSelf)
            return;

        if (cachedCamera == null || !cachedCamera.isActiveAndEnabled)
            cachedCamera = Camera.main;
        if (cachedCamera == null)
            return;

        // 전구 빛은 항상 카메라를 바라봅니다.
        bulb.rotation = Quaternion.LookRotation(bulb.position - cachedCamera.transform.position, cachedCamera.transform.up);
    }

    private void RollIfNeeded()
    {
        if (rolled)
            return;
        rolled = true;
        rolledOn = lightChance >= 1f || Random.value < lightChance;
    }

    private void RefreshVisible()
    {
        RollIfNeeded();
        bool visible = LevelLighting.IsNight && rolledOn;

        if (visible && !built)
            Build();

        if (root != null && root.activeSelf != visible)
            root.SetActive(visible);
    }

    // ---------------- 만들기 ----------------

    private void Build()
    {
        DestroyBuilt();
        built = true;

        Material material = ResolveMaterial();
        if (material == null)
            return;

        if (block == null)
            block = new MaterialPropertyBlock();

        root = new GameObject("NightLight_Glow");
        root.transform.SetParent(transform, false);
        root.layer = gameObject.layer;

        switch (kind)
        {
            case Kind.Window:
                BuildPanel(material);
                break;

            case Kind.VendingScreen:
                BuildPanel(material);
                BuildSpill(material);
                break;

            case Kind.StreetLight:
                BuildCone(material);
                BuildPool(material);
                BuildBulb(material);
                break;
        }
    }

    private void BuildPanel(Material material)
    {
        Mesh mesh = MakeQuadXY(panelWidth, panelHeight);
        MeshRenderer r = MakePart("Panel", mesh, material, panelOffset, Quaternion.identity);
        ApplyBlock(r, ShapeRect, panelIntensity, panelSoftness, 0f, 1f, 1f);
    }

    private void BuildSpill(Material material)
    {
        // 로컬 +Z 쪽으로 뻗는 바닥 판. v = 0이 자판기 쪽(가장 밝음).
        Mesh mesh = MakeQuadXZForward(spillWidth, spillLength);
        MeshRenderer r = MakePart("FloorSpill", mesh, material, spillOffset, Quaternion.identity);
        ApplyBlock(r, ShapeRect, spillIntensity, spillSoftness, spillFade, 1f, 1f);
    }

    private void BuildCone(Material material)
    {
        Mesh mesh = MakeCone(coneHeight, coneTopRadius, coneBottomRadius, coneSegments);
        MeshRenderer r = MakePart("Cone", mesh, material, Vector3.zero, Quaternion.identity);
        ApplyBlock(r, ShapeCone, coneIntensity, 0.05f, 0f, coneFalloff, coneEdgePower);
    }

    private void BuildPool(Material material)
    {
        Mesh mesh = MakeQuadXZCentered(poolRadius * 2f, poolRadius * 2f);
        Vector3 pos = new Vector3(0f, -coneHeight + poolLift, 0f);
        MeshRenderer r = MakePart("GroundPool", mesh, material, pos, Quaternion.identity);
        ApplyBlock(r, ShapeRadial, poolIntensity, 0.05f, 0f, poolFalloff, 1f);
    }

    private void BuildBulb(Material material)
    {
        if (bulbSize <= 0f)
            return;

        Mesh mesh = MakeQuadXY(bulbSize, bulbSize);
        MeshRenderer r = MakePart("Bulb", mesh, material, Vector3.zero, Quaternion.identity);
        ApplyBlock(r, ShapeRadial, bulbIntensity, 0.05f, 0f, bulbFalloff, 1f);
        bulb = r.transform;
    }

    private MeshRenderer MakePart(string partName, Mesh mesh, Material material, Vector3 localPos, Quaternion localRot)
    {
        GameObject go = new GameObject(partName);
        go.layer = root.layer;
        go.transform.SetParent(root.transform, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = localRot;

        MeshFilter mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;

        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = material;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.lightProbeUsage = LightProbeUsage.Off;
        mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
        mr.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;

        RememberMesh(mesh);
        return mr;
    }

    private void ApplyBlock(MeshRenderer r, float shape, float intensity, float softness, float fadeV, float falloff, float edgePower)
    {
        block.Clear();
        block.SetColor(ColorId, color);
        block.SetFloat(IntensityId, Mathf.Max(0f, intensity));
        block.SetFloat(ShapeId, shape);
        block.SetFloat(SoftnessId, Mathf.Clamp(softness, 0.001f, 0.5f));
        block.SetFloat(FadeVId, Mathf.Clamp01(fadeV));
        block.SetFloat(FalloffId, Mathf.Max(0.01f, falloff));
        block.SetFloat(EdgePowerId, Mathf.Max(0.01f, edgePower));
        r.SetPropertyBlock(block);
    }

    private Material ResolveMaterial()
    {
        if (glowMaterial != null)
            return glowMaterial;

        if (fallbackMaterial == null)
        {
            Shader shader = Shader.Find("MobArchive/NightGlow");
            if (shader == null)
            {
                if (!warnedMissingShader)
                {
                    warnedMissingShader = true;
                    Debug.LogWarning("[NightLight] MobArchive/NightGlow 셰이더를 찾지 못했습니다. NightLight의 Glow Material에 NightGlow 머티리얼을 지정해 주세요.");
                }
                return null;
            }

            fallbackMaterial = new Material(shader) { name = "NightGlow_Fallback" };
            if (!warnedMissingShader)
            {
                warnedMissingShader = true;
                Debug.LogWarning("[NightLight] Glow Material이 비어 있어 셰이더로 임시 머티리얼을 만들었습니다. 빌드에서 빠지지 않도록 NightGlow 머티리얼을 지정해 주세요.", this);
            }
        }

        return fallbackMaterial;
    }

    private void RememberMesh(Mesh mesh)
    {
        if (ownedMeshCount >= ownedMeshes.Length)
            System.Array.Resize(ref ownedMeshes, ownedMeshes.Length * 2);
        ownedMeshes[ownedMeshCount++] = mesh;
    }

    private void DestroyBuilt()
    {
        built = false;
        bulb = null;

        if (root != null)
        {
            if (Application.isPlaying)
                Destroy(root);
            else
                DestroyImmediate(root);
            root = null;
        }

        for (int i = 0; i < ownedMeshCount; i++)
        {
            if (ownedMeshes[i] == null)
                continue;
            if (Application.isPlaying)
                Destroy(ownedMeshes[i]);
            else
                DestroyImmediate(ownedMeshes[i]);
            ownedMeshes[i] = null;
        }
        ownedMeshCount = 0;
    }

    // ---------------- 메시 ----------------

    // 로컬 XY 평면, 중심 기준, +Z를 바라봄.
    private static Mesh MakeQuadXY(float width, float height)
    {
        float hw = Mathf.Max(0.001f, width) * 0.5f;
        float hh = Mathf.Max(0.001f, height) * 0.5f;
        Mesh m = new Mesh { name = "NightLight_QuadXY" };
        m.vertices = new[]
        {
            new Vector3(-hw, -hh, 0f), new Vector3(hw, -hh, 0f),
            new Vector3(-hw, hh, 0f), new Vector3(hw, hh, 0f)
        };
        m.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
        m.normals = new[] { Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward };
        m.triangles = new[] { 0, 2, 1, 1, 2, 3 };
        m.RecalculateBounds();
        return m;
    }

    // 로컬 XZ 평면, 중심 기준(위를 바라봄).
    private static Mesh MakeQuadXZCentered(float width, float depth)
    {
        float hw = Mathf.Max(0.001f, width) * 0.5f;
        float hd = Mathf.Max(0.001f, depth) * 0.5f;
        Mesh m = new Mesh { name = "NightLight_QuadXZ" };
        m.vertices = new[]
        {
            new Vector3(-hw, 0f, -hd), new Vector3(hw, 0f, -hd),
            new Vector3(-hw, 0f, hd), new Vector3(hw, 0f, hd)
        };
        m.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
        m.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
        m.triangles = new[] { 0, 2, 1, 1, 2, 3 };
        m.RecalculateBounds();
        return m;
    }

    // 로컬 XZ 평면, z = 0에서 +Z로 length만큼 뻗음. v = 0이 z = 0 쪽.
    private static Mesh MakeQuadXZForward(float width, float length)
    {
        float hw = Mathf.Max(0.001f, width) * 0.5f;
        float l = Mathf.Max(0.001f, length);
        Mesh m = new Mesh { name = "NightLight_Spill" };
        m.vertices = new[]
        {
            new Vector3(-hw, 0f, 0f), new Vector3(hw, 0f, 0f),
            new Vector3(-hw, 0f, l), new Vector3(hw, 0f, l)
        };
        m.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
        m.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
        m.triangles = new[] { 0, 2, 1, 1, 2, 3 };
        m.RecalculateBounds();
        return m;
    }

    // 꼭대기(y = 0, v = 0)에서 아래(y = -height, v = 1)로 넓어지는 뚜껑 없는 원뿔(옆면만).
    private static Mesh MakeCone(float height, float topRadius, float bottomRadius, int segments)
    {
        segments = Mathf.Clamp(segments, 8, 48);
        float h = Mathf.Max(0.01f, height);
        float rt = Mathf.Max(0f, topRadius);
        float rb = Mathf.Max(0.001f, bottomRadius);

        int ring = segments + 1;
        Vector3[] verts = new Vector3[ring * 2];
        Vector3[] normals = new Vector3[ring * 2];
        Vector2[] uvs = new Vector2[ring * 2];
        int[] tris = new int[segments * 6];

        // 옆면 바깥 방향 법선의 기울기(아래로 넓어지는 만큼 아래쪽을 향함).
        float slope = (rb - rt) / h;

        for (int i = 0; i < ring; i++)
        {
            float t = (float)i / segments;
            float ang = t * Mathf.PI * 2f;
            float cx = Mathf.Cos(ang);
            float cz = Mathf.Sin(ang);
            Vector3 n = new Vector3(cx, -slope, cz).normalized;

            verts[i] = new Vector3(cx * rt, 0f, cz * rt);
            verts[ring + i] = new Vector3(cx * rb, -h, cz * rb);
            normals[i] = n;
            normals[ring + i] = n;
            uvs[i] = new Vector2(t, 0f);
            uvs[ring + i] = new Vector2(t, 1f);
        }

        for (int i = 0; i < segments; i++)
        {
            int a = i;
            int b = i + 1;
            int c = ring + i;
            int d = ring + i + 1;
            int k = i * 6;
            tris[k] = a;
            tris[k + 1] = b;
            tris[k + 2] = c;
            tris[k + 3] = b;
            tris[k + 4] = d;
            tris[k + 5] = c;
        }

        Mesh m = new Mesh { name = "NightLight_Cone" };
        m.vertices = verts;
        m.normals = normals;
        m.uv = uvs;
        m.triangles = tris;
        m.RecalculateBounds();
        return m;
    }

    // ---------------- 에디터 ----------------

    private void OnValidate()
    {
        // 플레이 중 값을 바꾸면 다음 프레임에 다시 만듭니다(OnValidate 안에서는 오브젝트를 만들 수 없음).
        if (!Application.isPlaying || !built || !isActiveAndEnabled)
            return;

        pendingRebuild = true;
    }

    [ContextMenu("기본값: 창문")]
    private void PresetWindow()
    {
        kind = Kind.Window;
        lightChance = 0.6f;
        color = new Color(1f, 0.78f, 0.45f, 1f);
        panelIntensity = 1.5f;
        panelSoftness = 0.04f;
        MarkDirtyInEditor();
    }

    [ContextMenu("기본값: 자판기")]
    private void PresetVending()
    {
        kind = Kind.VendingScreen;
        lightChance = 1f;
        color = new Color(0.75f, 0.9f, 1f, 1f);
        panelIntensity = 2f;
        panelSoftness = 0.03f;
        spillIntensity = 0.5f;
        spillSoftness = 0.35f;
        spillFade = 1f;
        MarkDirtyInEditor();
    }

    [ContextMenu("기본값: 가로등")]
    private void PresetStreetLight()
    {
        kind = Kind.StreetLight;
        lightChance = 1f;
        color = new Color(1f, 0.8f, 0.5f, 1f);
        coneIntensity = 0.35f;
        poolIntensity = 0.6f;
        bulbIntensity = 2f;
        MarkDirtyInEditor();
    }

    private void MarkDirtyInEditor()
    {
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.9f);
        Gizmos.matrix = transform.localToWorldMatrix;

        switch (kind)
        {
            case Kind.Window:
                DrawPanelGizmo();
                break;

            case Kind.VendingScreen:
                DrawPanelGizmo();
                Vector3 c = spillOffset + new Vector3(0f, 0f, spillLength * 0.5f);
                Gizmos.DrawWireCube(c, new Vector3(spillWidth, 0f, spillLength));
                break;

            case Kind.StreetLight:
                DrawConeGizmo();
                break;
        }

        Gizmos.matrix = Matrix4x4.identity;
    }

    private void DrawPanelGizmo()
    {
        Gizmos.DrawWireCube(panelOffset, new Vector3(panelWidth, panelHeight, 0f));
        // 바라보는 방향(+Z) 표시.
        Gizmos.DrawLine(panelOffset, panelOffset + Vector3.forward * 0.3f);
    }

    private void DrawConeGizmo()
    {
        const int n = 16;
        Vector3 prevTop = Vector3.zero;
        Vector3 prevBottom = Vector3.zero;
        Vector3 prevPool = Vector3.zero;
        float poolY = -coneHeight + poolLift;

        for (int i = 0; i <= n; i++)
        {
            float ang = (float)i / n * Mathf.PI * 2f;
            Vector3 dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
            Vector3 top = dir * coneTopRadius;
            Vector3 bottom = dir * coneBottomRadius + Vector3.down * coneHeight;
            Vector3 pool = dir * poolRadius + Vector3.up * poolY;

            if (i > 0)
            {
                Gizmos.DrawLine(prevTop, top);
                Gizmos.DrawLine(prevBottom, bottom);
                Gizmos.DrawLine(prevPool, pool);
            }
            if (i % 4 == 0)
                Gizmos.DrawLine(top, bottom);

            prevTop = top;
            prevBottom = bottom;
            prevPool = pool;
        }

        Gizmos.DrawWireSphere(Vector3.zero, Mathf.Max(0.05f, bulbSize * 0.25f));
    }
}
