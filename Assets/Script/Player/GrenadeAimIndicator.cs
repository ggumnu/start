using UnityEngine;

// 수류탄 모드(2키)에서 우클릭으로 조준하는 동안 바닥에 조준 표시를 그립니다.
//   1. 최대 사거리: 플레이어 발밑을 중심으로 한 흰 테두리 원(리그 오브 레전드 사거리 표시처럼)
//   2. 폭발 반경: 착지점을 중심으로 한 반투명 흰색으로 꽉 찬 원
//   3. 곡사 조준선: 손에서 착지점까지 실제 수류탄과 똑같은 포물선(Grenade.EvaluateArc)
// 이미지나 머티리얼 파일은 필요 없습니다 - 전부 코드에서 실행 중에 만듭니다.
//
// [사용 방법]
// Player 오브젝트(PlayerGrenadeThrower가 붙은 오브젝트)에 Add Component만 하면 됩니다.
// 색/두께/투명도는 아래 인스펙터 값으로 조절하고, 플레이 중에 바꿔도 바로 반영됩니다.
//
// [PlayerHitFlash와의 관계]
// 표시용 오브젝트들을 Awake가 아니라 Start에서 만듭니다. PlayerHitFlash는 Awake에서 플레이어의
// 렌더러 목록을 한 번만 모으기 때문에, 그 뒤에 생기는 이 표시들은 무적 점멸 대상에 들어가지
// 않습니다(PlayerHitFlash 수정 없이 해결).
[RequireComponent(typeof(PlayerGrenadeThrower))]
public class GrenadeAimIndicator : MonoBehaviour
{
    [Header("참조 (비워두면 자동으로 찾습니다)")]
    public PlayerGrenadeThrower thrower;

    [Header("사거리 원 (흰 테두리)")]
    public Color rangeCircleColor = new Color(1f, 1f, 1f, 0.8f);
    [Min(0.001f)] public float rangeCircleWidth = 0.05f;

    [Header("폭발 반경 원 (꽉 찬 반투명)")]
    public Color radiusFillColor = new Color(1f, 1f, 1f, 0.25f);

    [Header("곡사 조준선")]
    public Color arcLineColor = new Color(1f, 1f, 1f, 0.7f);
    [Min(0.001f)] public float arcLineWidth = 0.06f;
    [Tooltip("곡선을 몇 개의 점으로 그릴지입니다. 클수록 매끄럽습니다.")]
    [Range(4, 64)] public int arcPointCount = 30;

    [Header("공통")]
    [Tooltip("바닥과 겹쳐 깜빡이는 현상을 막기 위해 원을 바닥에서 띄우는 높이(m)입니다.")]
    public float groundOffset = 0.03f;
    [Tooltip("원을 몇 개의 조각으로 그릴지입니다. 클수록 동그랗습니다.")]
    [Range(16, 128)] public int circleSegments = 64;

    private LineRenderer rangeLine;
    private LineRenderer arcLine;
    private Transform radiusDisc;
    private MeshRenderer radiusDiscRenderer;
    private Mesh discMesh;
    private Material rangeMaterial;
    private Material arcMaterial;
    private Material discMaterial;
    private bool built;

    void Awake()
    {
        if (thrower == null) thrower = GetComponent<PlayerGrenadeThrower>();
    }

    void Start()
    {
        Build();
    }

    void Build()
    {
        rangeMaterial = BuildMaterial("GrenadeRangeCircle_Material", rangeCircleColor);
        arcMaterial = BuildMaterial("GrenadeArcLine_Material", arcLineColor);
        discMaterial = BuildMaterial("GrenadeRadiusDisc_Material", radiusFillColor);

        rangeLine = CreateLine("GrenadeRangeCircle", rangeMaterial);
        rangeLine.loop = true;

        arcLine = CreateLine("GrenadeArcLine", arcMaterial);
        arcLine.loop = false;

        discMesh = BuildUnitDiscMesh(circleSegments);
        GameObject discObj = new GameObject("GrenadeRadiusDisc");
        radiusDisc = discObj.transform;
        radiusDisc.SetParent(transform, worldPositionStays: false);
        discObj.AddComponent<MeshFilter>().sharedMesh = discMesh;
        radiusDiscRenderer = discObj.AddComponent<MeshRenderer>();
        ConfigureRenderer(radiusDiscRenderer, discMaterial);

        built = true;
        SetVisible(false);
    }

    LineRenderer CreateLine(string objName, Material mat)
    {
        GameObject obj = new GameObject(objName);
        obj.transform.SetParent(transform, worldPositionStays: false);
        LineRenderer line = obj.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.alignment = LineAlignment.View;
        line.numCornerVertices = 2;
        line.numCapVertices = 2;
        ConfigureRenderer(line, mat);
        return line;
    }

    static void ConfigureRenderer(Renderer rend, Material mat)
    {
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rend.receiveShadows = false;
        rend.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        rend.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        rend.sharedMaterial = mat;
        rend.enabled = false;
    }

    void LateUpdate()
    {
        if (!built) return;

        bool show = thrower != null && thrower.IsAimingGrenade;
        if (!show)
        {
            SetVisible(false);
            return;
        }

        SyncColors();
        UpdateRangeCircle();
        UpdateRadiusDisc();
        UpdateArcLine();
        SetVisible(true);
    }

    void SetVisible(bool visible)
    {
        if (rangeLine.enabled != visible) rangeLine.enabled = visible;
        if (arcLine.enabled != visible) arcLine.enabled = visible;
        if (radiusDiscRenderer.enabled != visible) radiusDiscRenderer.enabled = visible;
    }

    void SyncColors()
    {
        if (rangeMaterial.color != rangeCircleColor) rangeMaterial.color = rangeCircleColor;
        if (arcMaterial.color != arcLineColor) arcMaterial.color = arcLineColor;
        if (discMaterial.color != radiusFillColor) discMaterial.color = radiusFillColor;
    }

    void UpdateRangeCircle()
    {
        Vector3 center = transform.position;
        center.y += groundOffset;
        float r = thrower.CurrentRange;

        rangeLine.startWidth = rangeCircleWidth;
        rangeLine.endWidth = rangeCircleWidth;
        if (rangeLine.positionCount != circleSegments) rangeLine.positionCount = circleSegments;

        for (int i = 0; i < circleSegments; i++)
        {
            float angle = (float)i / circleSegments * Mathf.PI * 2f;
            rangeLine.SetPosition(i, center + new Vector3(Mathf.Cos(angle) * r, 0f, Mathf.Sin(angle) * r));
        }
    }

    void UpdateRadiusDisc()
    {
        Vector3 target = thrower.AimTarget;
        float r = thrower.CurrentRadius;

        radiusDisc.position = new Vector3(target.x, target.y + groundOffset, target.z);
        radiusDisc.rotation = Quaternion.identity;

        // 부모(Player) 스케일이 1이 아니어도 실제 월드 반경이 r이 되도록 보정합니다
        // (AimTrajectoryIndicator와 동일한 방식).
        Vector3 parentScale = transform.lossyScale;
        radiusDisc.localScale = new Vector3(
            SafeDivide(r, parentScale.x),
            SafeDivide(1f, parentScale.y),
            SafeDivide(r, parentScale.z));
    }

    void UpdateArcLine()
    {
        Vector3 start = thrower.ThrowOrigin;
        Vector3 end = thrower.AimTarget;
        end.y += groundOffset;

        arcLine.startWidth = arcLineWidth;
        arcLine.endWidth = arcLineWidth;
        if (arcLine.positionCount != arcPointCount) arcLine.positionCount = arcPointCount;

        for (int i = 0; i < arcPointCount; i++)
        {
            float u = (float)i / (arcPointCount - 1);
            arcLine.SetPosition(i, Grenade.EvaluateArc(start, end, thrower.arcHeight, u));
        }
    }

    static float SafeDivide(float value, float divisor)
    {
        return Mathf.Approximately(divisor, 0f) ? value : value / divisor;
    }

    // 반지름 1짜리 원판(바닥에 누운 모양) 메시입니다. 실제 반경은 localScale로 조절합니다.
    // Grenade.cs의 착탄 지점 표시도 같은 모양을 쓰도록 public으로 열어뒀습니다.
    public static Mesh BuildUnitDiscMesh(int segments)
    {
        Mesh mesh = new Mesh { name = "GrenadeRadiusDisc" };

        var vertices = new Vector3[segments + 1];
        var triangles = new int[segments * 3];
        vertices[0] = Vector3.zero;

        for (int i = 0; i < segments; i++)
        {
            float angle = (float)i / segments * Mathf.PI * 2f;
            vertices[i + 1] = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
        }

        for (int i = 0; i < segments; i++)
        {
            int next = (i + 1) % segments;
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = next + 1;
            triangles[i * 3 + 2] = i + 1;
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    // AimTrajectoryIndicator와 동일한 반투명 머티리얼 생성 방식입니다(URP Unlit 우선).
    public static Material BuildMaterial(string matName, Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
            ?? Shader.Find("Unlit/Transparent")
            ?? Shader.Find("Sprites/Default")
            ?? Shader.Find("Standard");

        Material mat = new Material(shader) { name = matName };

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
        if (mat.HasProperty("_Cull"))
        {
            mat.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
        }
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        mat.color = color;
        return mat;
    }

    void OnDestroy()
    {
        if (rangeMaterial != null) Destroy(rangeMaterial);
        if (arcMaterial != null) Destroy(arcMaterial);
        if (discMaterial != null) Destroy(discMaterial);
        if (discMesh != null) Destroy(discMesh);
    }
}
