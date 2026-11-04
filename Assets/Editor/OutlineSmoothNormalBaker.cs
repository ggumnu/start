using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// 배경 모델 외곽선 보정 - 모델을 임포트할 때 외곽선 전용 데이터를 UV4(채널 3)에 저장합니다. 게임 실행 중 추가 비용 없음.
//   xyz = 외곽선 전용 노멀: "볼록한" 모서리만 부드럽게 이어 붙임(하드 엣지 모서리 끊김 방지). 오목한 곳(창문 음각 등)은
//         원래 노멀 그대로 - 평균을 내면 외곽선이 앞쪽 면 위로 튀어나와 그려지기 때문.
//   w   = 모델 크기(가로·세로·높이 평균, 모델 기준). 셰이더가 오브젝트 스케일을 곱해 실제 크기로 바꾸고,
//         OutlineWidthSettings의 기준 크기보다 작으면 외곽선을 비례해서 얇게 그림.
// 화면 음영은 원래 노멀을 그대로 써서 바뀌지 않습니다.
//
// - 대상: TargetFolders 안의 모델만. 새로 넣은 모델은 자동 적용, 이미 있던 모델/폴더 추가/계산 변경 후엔 메뉴로 다시 굽기.
// - 스킨 메시(뼈로 움직이는 모델)는 대상 아님(MeshFilter만 처리).
// - UV4를 이미 쓰는 메시는 덮어쓰지 않고 건너뜁니다(콘솔 경고).
// - 주의: 이 모델들을 정적 배칭(Static Batching)으로 합치면 노멀 방향/크기가 틀어집니다. 지금은 방을 런타임에 생성해 배칭 없음.
// - GetVersion은 일부러 두지 않음(두면 계산을 바꿀 때마다 프로젝트의 모든 모델이 다시 임포트됨).
public class OutlineSmoothNormalBaker : AssetPostprocessor
{
    public static readonly string[] TargetFolders = { "Assets/Object" };
    public const int UvChannel = 3; // UV4 (셰이더의 TEXCOORD3)

    private const string SettingsAssetPath = "Assets/Resources/OutlineWidthSettings.asset";
    private const string DefaultReferencePrefabPath = "Assets/Object_Prefab/Prop/Car_Cyan_Wall.prefab";

    private static bool IsTarget(string path)
    {
        if (string.IsNullOrEmpty(path)) return false;
        path = path.Replace('\\', '/');
        foreach (string folder in TargetFolders)
        {
            string f = folder.TrimEnd('/') + "/";
            if (path.StartsWith(f, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    private void OnPostprocessModel(GameObject root)
    {
        if (!IsTarget(assetPath)) return;

        MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);

        // 모델 전체 크기(모델 루트 기준) - 모든 메시가 같은 값을 받음(창문 틀처럼 작은 부품만 얇아지지 않도록).
        float modelSize = OutlineWidthSettings.MeasureAverageSize(root, filters, relativeToRoot: true);

        var done = new HashSet<Mesh>();
        foreach (MeshFilter mf in filters)
        {
            if (mf.sharedMesh == null || !done.Add(mf.sharedMesh)) continue;
            Bake(mf.sharedMesh, assetPath, modelSize);
        }
    }

    private struct FaceRef
    {
        public Vector3 normal;
        public Vector3 centroid;
    }

    private static Vector3Int Key(Vector3 v)
    {
        return new Vector3Int(Mathf.RoundToInt(v.x * 10000f), Mathf.RoundToInt(v.y * 10000f), Mathf.RoundToInt(v.z * 10000f));
    }

    private static void Bake(Mesh mesh, string path, float modelSize)
    {
        Vector3[] vertices = mesh.vertices;
        Vector3[] normals = mesh.normals;
        if (vertices.Length == 0 || normals.Length != vertices.Length)
        {
            Debug.LogWarning($"[외곽선 노멀] 노멀이 없어 건너뜀: {path} / {mesh.name}");
            return;
        }

        var existing = new List<Vector4>();
        mesh.GetUVs(UvChannel, existing);
        if (existing.Count > 0)
        {
            Debug.LogWarning($"[외곽선 노멀] UV4를 이미 쓰는 메시라 건너뜀: {path} / {mesh.name}");
            return;
        }

        // 같은 위치(0.1mm 단위)의 꼭짓점끼리 묶고, 그 자리에 모이는 "서로 다른 면 방향"과 그 면의 중심점을 하나씩 기록합니다.
        var keys = new Vector3Int[vertices.Length];
        for (int i = 0; i < vertices.Length; i++) keys[i] = Key(vertices[i]);

        var groups = new Dictionary<Vector3Int, List<FaceRef>>();
        int[] tris = mesh.triangles;
        for (int t = 0; t + 2 < tris.Length; t += 3)
        {
            Vector3 centroid = (vertices[tris[t]] + vertices[tris[t + 1]] + vertices[tris[t + 2]]) / 3f;
            for (int k = 0; k < 3; k++)
            {
                int vi = tris[t + k];
                Vector3 n = normals[vi].normalized;
                if (!groups.TryGetValue(keys[vi], out List<FaceRef> list))
                {
                    list = new List<FaceRef>(4);
                    groups.Add(keys[vi], list);
                }

                bool duplicate = false;
                for (int j = 0; j < list.Count; j++)
                {
                    if (Vector3.Dot(list[j].normal, n) > 0.9999f) { duplicate = true; break; }
                }
                if (!duplicate) list.Add(new FaceRef { normal = n, centroid = centroid });
            }
        }

        float eps = Mathf.Max(mesh.bounds.size.magnitude * 1e-5f, 1e-7f);
        var result = new List<Vector4>(vertices.Length);
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 p = vertices[i];
            Vector3 ni = normals[i].normalized;
            Vector3 sum = ni;

            if (groups.TryGetValue(keys[i], out List<FaceRef> list))
            {
                // 이 꼭짓점이 속한 면의 중심점
                Vector3 ci = p;
                for (int j = 0; j < list.Count; j++)
                {
                    if (Vector3.Dot(list[j].normal, ni) > 0.9999f) { ci = list[j].centroid; break; }
                }

                for (int j = 0; j < list.Count; j++)
                {
                    FaceRef other = list[j];
                    if (Vector3.Dot(other.normal, ni) > 0.9999f) continue;

                    // 볼록 판정: 서로 상대 면의 중심이 자기 면 "뒤쪽"에 있으면 볼록한 모서리(바깥 모서리)
                    float side = Vector3.Dot(ni, other.centroid - p) + Vector3.Dot(other.normal, ci - p);
                    if (side < -eps) sum += other.normal;
                }
            }

            Vector3 smooth = sum.sqrMagnitude > 1e-8f ? sum.normalized : ni;
            result.Add(new Vector4(smooth.x, smooth.y, smooth.z, modelSize));
        }

        mesh.SetUVs(UvChannel, result);
    }

    [MenuItem("MobArchive/외곽선/배경 모델 외곽선 노멀 다시 굽기")]
    private static void RebakeAll()
    {
        var folders = new List<string>();
        foreach (string f in TargetFolders)
        {
            string folder = f.TrimEnd('/');
            if (AssetDatabase.IsValidFolder(folder)) folders.Add(folder);
        }
        if (folders.Count == 0)
        {
            Debug.LogWarning("[외곽선 노멀] 대상 폴더가 없습니다: " + string.Join(", ", TargetFolders));
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:Model", folders.ToArray());
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (string guid in guids)
            {
                AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }
        Debug.Log($"[외곽선 노멀] 모델 {guids.Length}개를 다시 임포트했습니다.");
    }

    // 에디터를 켤 때 크기별 두께 설정을 셰이더에 적용하고, 설정 에셋이 없으면 자동으로 만듭니다(기준 = Car_Cyan 프리팹).
    [InitializeOnLoadMethod]
    private static void InitOutlineWidthSettings()
    {
        EditorApplication.delayCall += () =>
        {
            if (AssetDatabase.LoadAssetAtPath<OutlineWidthSettings>(SettingsAssetPath) == null)
            {
                if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");

                var settings = ScriptableObject.CreateInstance<OutlineWidthSettings>();
                settings.referencePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultReferencePrefabPath);
                settings.ReadReferenceSize();
                AssetDatabase.CreateAsset(settings, SettingsAssetPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"[외곽선 두께] 설정 에셋을 만들었습니다: {SettingsAssetPath} (기준 크기 {settings.referenceSize:0.###}m)");
            }
            OutlineWidthSettings.ApplyFromResources();
        };
    }
}
