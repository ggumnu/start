using UnityEngine;

// 배경 모델 외곽선을 "크기에 따라" 얇게 하는 설정입니다(Resources/OutlineWidthSettings, 에디터가 없으면 자동 생성).
// 기준 크기(기본 = Car_Cyan 프리팹의 가로·세로·높이 평균)보다 작은 모델은 크기에 비례해 얇아지고, 큰 모델(건물/바닥/차)은 재질 두께 그대로.
// 값은 셰이더 전역값(_OutlineRefSize/_OutlineMinWidth)으로 넘어가서, Inspector에서 바꾸면 바로 반영됩니다(다시 굽기 불필요).
// 모델 크기는 OutlineSmoothNormalBaker가 임포트 때 UV4.w에 저장합니다.
[CreateAssetMenu(fileName = "OutlineWidthSettings", menuName = "MobArchive/Outline Width Settings")]
public class OutlineWidthSettings : ScriptableObject
{
    public const string ResourcePath = "OutlineWidthSettings";

    private static readonly int RefSizeId = Shader.PropertyToID("_OutlineRefSize");
    private static readonly int MinWidthId = Shader.PropertyToID("_OutlineMinWidth");

    [Header("크기별 외곽선 두께 (배경 모델)")]
    [Tooltip("끄면 모든 배경 모델이 재질의 외곽선 두께 그대로 그려집니다.")]
    public bool scaleBySize = true;

    [Tooltip("기준 모델 프리팹입니다. ⋮ 메뉴 > \"기준 모델에서 크기 읽기\"로 아래 Reference Size를 채웁니다.")]
    public GameObject referencePrefab;

    [Tooltip("이 크기(가로·세로·높이 평균, m)보다 작은 모델부터 비례해서 얇아집니다. 0이면 기준 모델에서 자동으로 잽니다.")]
    [Min(0f)] public float referenceSize = 0f;

    [Tooltip("가장 얇아질 때의 비율입니다(0.4 = 재질 두께의 40%까지만 얇아짐).")]
    [Range(0.05f, 1f)] public float minWidthFactor = 0.4f;

    public float EffectiveReferenceSize
    {
        get
        {
            if (referenceSize > 0f) return referenceSize;
            return referencePrefab != null ? MeasureAverageSize(referencePrefab, referencePrefab.GetComponentsInChildren<MeshFilter>(true), false) : 0f;
        }
    }

    public void Apply()
    {
        Shader.SetGlobalFloat(RefSizeId, scaleBySize ? EffectiveReferenceSize : 0f);
        Shader.SetGlobalFloat(MinWidthId, minWidthFactor);
    }

    public static void ApplyFromResources()
    {
        var settings = Resources.Load<OutlineWidthSettings>(ResourcePath);
        if (settings != null) settings.Apply();
        else Shader.SetGlobalFloat(RefSizeId, 0f); // 설정이 없으면 크기 보정 없음
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RuntimeInit()
    {
        ApplyFromResources();
    }

    private void OnEnable() { Apply(); }
    private void OnValidate() { Apply(); }

    [ContextMenu("기준 모델에서 크기 읽기")]
    public void ReadReferenceSize()
    {
        if (referencePrefab == null) return;
        referenceSize = MeasureAverageSize(referencePrefab, referencePrefab.GetComponentsInChildren<MeshFilter>(true), false);
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
        Apply();
    }

    // 메시들을 합친 상자의 가로·세로·높이 평균입니다. relativeToRoot = true면 루트 자신의 위치/회전/크기는 빼고 잽니다
    // (모델 임포트용 - 실제 크기는 셰이더가 오브젝트 스케일을 곱해 계산). false면 루트 크기까지 포함(기준 프리팹용).
    public static float MeasureAverageSize(GameObject root, MeshFilter[] filters, bool relativeToRoot)
    {
        if (root == null || filters == null) return 0f;

        bool any = false;
        Vector3 min = Vector3.zero, max = Vector3.zero;
        Matrix4x4 toRoot = relativeToRoot ? root.transform.worldToLocalMatrix : Matrix4x4.identity;

        foreach (MeshFilter mf in filters)
        {
            if (mf == null || mf.sharedMesh == null) continue;
            Bounds b = mf.sharedMesh.bounds;
            Matrix4x4 m = toRoot * mf.transform.localToWorldMatrix;

            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 w = m.MultiplyPoint3x4(corner);
                if (!any) { min = max = w; any = true; }
                else { min = Vector3.Min(min, w); max = Vector3.Max(max, w); }
            }
        }

        if (!any) return 0f;
        Vector3 size = max - min;
        return (size.x + size.y + size.z) / 3f;
    }
}
