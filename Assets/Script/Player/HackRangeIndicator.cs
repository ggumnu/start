using UnityEngine;

// 해킹툴 모드(3키)에서 우클릭을 누르고 있는 동안, 플레이어 발밑에 해킹 범위를 흰 테두리 원으로
// 보여줍니다(수류탄 조준 시 사거리 원과 같은 모양). 범위는 PlayerStats.HackRadius를 매 프레임 다시
// 읽으므로 아이템으로 범위가 바뀌면 원도 즉시 바뀝니다.
//
// [사용 방법]
// Player 오브젝트(PlayerHackTool이 붙은 오브젝트)에 Add Component만 하면 됩니다. 색/두께는
// 인스펙터에서 조절하고, 플레이 중에 바꿔도 바로 반영됩니다.
//
// [PlayerHitFlash와의 관계]
// GrenadeAimIndicator와 같은 이유로 선 오브젝트를 Awake가 아니라 Start에서 만들어서, 플레이어 무적
// 점멸 대상에 포함되지 않게 합니다.
[RequireComponent(typeof(PlayerHackTool))]
public class HackRangeIndicator : MonoBehaviour
{
    [Header("참조 (비워두면 자동으로 찾습니다)")]
    public PlayerHackTool hackTool;

    [Header("범위 원 (흰 테두리)")]
    public Color circleColor = new Color(1f, 1f, 1f, 0.8f);
    [Min(0.001f)] public float circleWidth = 0.05f;
    [Tooltip("바닥과 겹쳐 깜빡이는 현상을 막기 위해 원을 바닥에서 띄우는 높이(m)입니다.")]
    public float groundOffset = 0.03f;
    [Tooltip("원을 몇 개의 조각으로 그릴지입니다. 클수록 동그랗습니다.")]
    [Range(16, 128)] public int circleSegments = 64;

    private LineRenderer line;
    private Material lineMaterial;
    private bool built;

    void Awake()
    {
        if (hackTool == null) hackTool = GetComponent<PlayerHackTool>();
    }

    void Start()
    {
        lineMaterial = GrenadeAimIndicator.BuildMaterial("HackRangeCircle_Material", circleColor);

        GameObject obj = new GameObject("HackRangeCircle");
        obj.transform.SetParent(transform, worldPositionStays: false);
        line = obj.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.loop = true;
        line.alignment = LineAlignment.View;
        line.numCornerVertices = 2;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        line.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        line.sharedMaterial = lineMaterial;
        line.enabled = false;

        built = true;
    }

    void LateUpdate()
    {
        if (!built) return;

        bool show = hackTool != null && hackTool.IsAimingHack;
        if (!show)
        {
            if (line.enabled) line.enabled = false;
            return;
        }

        if (lineMaterial.color != circleColor) lineMaterial.color = circleColor;
        line.startWidth = circleWidth;
        line.endWidth = circleWidth;
        if (line.positionCount != circleSegments) line.positionCount = circleSegments;

        Vector3 center = transform.position;
        center.y += groundOffset;
        float r = hackTool.CurrentRadius;

        for (int i = 0; i < circleSegments; i++)
        {
            float angle = (float)i / circleSegments * Mathf.PI * 2f;
            line.SetPosition(i, center + new Vector3(Mathf.Cos(angle) * r, 0f, Mathf.Sin(angle) * r));
        }

        if (!line.enabled) line.enabled = true;
    }

    void OnDestroy()
    {
        if (lineMaterial != null) Destroy(lineMaterial);
    }
}
