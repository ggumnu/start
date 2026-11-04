#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// [보물상자 뚜껑 분리 도구]
// 메뉴: MobArchive > 보물상자 뚜껑 분리
//
// 뚜껑과 몸통이 하나의 메시로 합쳐진 채 export된 보물상자 FBX를, 3ds Max 없이 유니티 안에서
// "몸통 메시"와 "뚜껑 메시" 두 개로 나누고, 프리팹을 뚜껑이 열릴 수 있는 구조로 자동으로 고쳐줍니다.
// 반드시 폴더 이름이 "Editor"인 곳에 둬야 합니다(빌드에서 자동 제외). #if UNITY_EDITOR로도 한 번 더 감쌌습니다.
//
// [사용 방법]
// 1. Project 창에서 보물상자 프리팹(예: DroneBox 1_Box / 2_Box / 3_Box)을 선택합니다(여러 개 가능).
// 2. 이 창의 [선택한 프리팹에 적용] 버튼을 누릅니다.
//
// [프리팹마다 하는 일]
// 1. 프리팹 안의 모델 메시를 "월드 위쪽 기준 높이"로 나눕니다. 분리 높이는 메시 전체 높이의 splitRatio
//    지점(기본 0.5 = 정확히 절반)입니다. 삼각형 하나하나를 판정하며, 분리 높이 위면 뚜껑, 아래면 몸통입니다.
//    분리 높이에 딱 붙어있는 납작한 면(몸통 윗면 테두리, 뚜껑 아랫면 테두리 등)은 면이 바라보는 방향으로
//    구분합니다 - 위를 보면 몸통의 윗면, 아래를 보면 뚜껑의 아랫면입니다.
// 2. 분리 높이를 "가로지르는" 삼각형이 하나라도 있으면(=뚜껑과 몸통이 실제로 이어져 있으면) 그 프리팹은
//    아무것도 바꾸지 않고 오류만 알립니다. 깔끔하게 나눌 수 없는 모델을 억지로 자르지 않기 위함입니다.
// 3. 나눈 두 메시를 원본 FBX 옆 "LidSplit" 폴더에 "<FBX 이름>_Body.asset" / "_Lid.asset"으로 저장합니다.
//    (원본 FBX 파일 자체는 전혀 수정하지 않습니다.)
// 4. 프리팹의 모델 메시를 몸통 메시로 바꾸고, 그 자식으로 "Lid" 오브젝트(뚜껑 메시 + 원래와 같은 재질)를
//    만듭니다. 뚜껑은 움직여야 하므로 Static 설정은 끄고, 충돌체(Collider)는 넣지 않습니다.
// 5. 프리팹 루트에 ChestLidOpener를 추가합니다(이미 있으면 그대로 둡니다).
//
// 이미 "Lid"가 있는 프리팹은 건너뜁니다 - 실수로 여러 번 눌러도 안전합니다.
// 프리팹 파일을 직접 고치므로 Ctrl+Z로는 되돌릴 수 없습니다. 실행 전에 git에 저장해두세요.
public class ChestLidSplitTool : EditorWindow
{
    private const string LidObjectName = "Lid";
    private const string OutputFolderName = "LidSplit";

    private float splitRatio = 0.5f;

    [MenuItem("MobArchive/보물상자 뚜껑 분리")]
    private static void Open()
    {
        var window = GetWindow<ChestLidSplitTool>("보물상자 뚜껑 분리");
        window.minSize = new Vector2(420, 260);
    }

    private void OnSelectionChange()
    {
        Repaint();
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "Project 창에서 보물상자 프리팹을 선택한 뒤 아래 버튼을 누르세요. 여러 개를 한꺼번에 선택해도 됩니다.\n" +
            "프리팹 파일을 직접 고치므로 Ctrl+Z로 되돌릴 수 없습니다. 실행 전에 git에 저장해두세요.",
            MessageType.Info);

        splitRatio = EditorGUILayout.Slider(
            new GUIContent("분리 높이 비율", "메시 전체 높이 중 어디서 나눌지 정합니다. 0.5 = 정확히 절반(DroneBox 기준값)."),
            splitRatio, 0.05f, 0.95f);

        var prefabPaths = GetSelectedPrefabPaths();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField($"선택된 프리팹: {prefabPaths.Count}개", EditorStyles.boldLabel);
        foreach (var p in prefabPaths)
        {
            EditorGUILayout.LabelField("  " + p);
        }

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(prefabPaths.Count == 0))
        {
            if (GUILayout.Button("선택한 프리팹에 적용", GUILayout.Height(32)))
            {
                Apply(prefabPaths);
            }
        }
    }

    private static List<string> GetSelectedPrefabPaths()
    {
        var result = new List<string>();
        foreach (var obj in Selection.GetFiltered<GameObject>(SelectionMode.Assets))
        {
            string path = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(path)) continue;
            if (!path.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase)) continue;
            if (!result.Contains(path)) result.Add(path);
        }
        return result;
    }

    private void Apply(List<string> prefabPaths)
    {
        int success = 0;
        foreach (var path in prefabPaths)
        {
            if (ProcessPrefab(path)) success++;
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[보물상자 뚜껑 분리] 완료: {prefabPaths.Count}개 중 {success}개 적용(건너뛴 것 포함 시 Console의 다른 메시지 참고).");
    }

    // 프리팹 하나를 처리합니다. 실제로 수정했으면 true.
    private bool ProcessPrefab(string prefabPath)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            // 이미 적용된 프리팹은 건너뜁니다.
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == LidObjectName)
                {
                    Debug.Log($"[보물상자 뚜껑 분리] '{prefabPath}': 이미 \"{LidObjectName}\"가 있어서 건너뜁니다.");
                    return false;
                }
            }

            // 대상 메시는 프리팹 안에 정확히 하나여야 합니다(DroneBox는 모델 메시 하나).
            var filters = root.GetComponentsInChildren<MeshFilter>(true);
            var candidates = new List<MeshFilter>();
            foreach (var mf in filters)
            {
                if (mf.sharedMesh != null && mf.GetComponent<MeshRenderer>() != null) candidates.Add(mf);
            }
            if (candidates.Count != 1)
            {
                Debug.LogError($"[보물상자 뚜껑 분리] '{prefabPath}': 메시가 {candidates.Count}개 있습니다. " +
                               "메시가 정확히 하나인 프리팹만 처리할 수 있어 건너뜁니다.");
                return false;
            }

            MeshFilter target = candidates[0];
            MeshRenderer targetRenderer = target.GetComponent<MeshRenderer>();
            Mesh source = target.sharedMesh;

            // 프리팹 안에서의 "월드 위쪽"을 메시 기준 방향으로 바꿔서 높이를 잽니다
            // (프리팹 루트가 -90도 눕혀져 있는 등 회전이 있어도 정확하도록).
            Vector3 upLocal = target.transform.InverseTransformDirection(Vector3.up).normalized;

            if (!SplitMesh(source, upLocal, splitRatio, out Mesh bodyMesh, out Mesh lidMesh, out string error))
            {
                Debug.LogError($"[보물상자 뚜껑 분리] '{prefabPath}': {error} 이 프리팹은 바꾸지 않았습니다.");
                return false;
            }

            // 메시 저장 (원본 메시가 들어있는 FBX 옆 LidSplit 폴더)
            string sourcePath = AssetDatabase.GetAssetPath(source);
            string folder = EnsureOutputFolder(Path.GetDirectoryName(sourcePath).Replace('\\', '/'));
            string baseName = Path.GetFileNameWithoutExtension(sourcePath);
            Mesh savedBody = SaveMeshAsset(bodyMesh, $"{folder}/{baseName}_Body.asset");
            Mesh savedLid = SaveMeshAsset(lidMesh, $"{folder}/{baseName}_Lid.asset");

            // 모델 메시 -> 몸통 메시
            target.sharedMesh = savedBody;

            // 뚜껑 오브젝트 생성 (모델과 같은 좌표계 - 위치/회전/크기 그대로)
            var lidGO = new GameObject(LidObjectName);
            lidGO.layer = target.gameObject.layer;
            lidGO.transform.SetParent(target.transform, false);
            lidGO.transform.localPosition = Vector3.zero;
            lidGO.transform.localRotation = Quaternion.identity;
            lidGO.transform.localScale = Vector3.one;
            GameObjectUtility.SetStaticEditorFlags(lidGO, 0); // 움직이는 오브젝트라 Static 금지

            var lidFilter = lidGO.AddComponent<MeshFilter>();
            lidFilter.sharedMesh = savedLid;
            var lidRenderer = lidGO.AddComponent<MeshRenderer>();
            lidRenderer.sharedMaterials = targetRenderer.sharedMaterials;
            lidRenderer.shadowCastingMode = targetRenderer.shadowCastingMode;
            lidRenderer.receiveShadows = targetRenderer.receiveShadows;
            lidRenderer.lightProbeUsage = targetRenderer.lightProbeUsage;
            lidRenderer.reflectionProbeUsage = targetRenderer.reflectionProbeUsage;
            lidRenderer.renderingLayerMask = targetRenderer.renderingLayerMask;

            // 뚜껑 연출 컴포넌트
            var opener = root.GetComponent<ChestLidOpener>();
            if (opener == null) opener = root.AddComponent<ChestLidOpener>();
            opener.lid = lidGO.transform;

            bool hasContainer = false;
            foreach (var mb in root.GetComponents<MonoBehaviour>())
            {
                if (mb is IOpenableContainer) { hasContainer = true; break; }
            }
            if (!hasContainer)
            {
                Debug.LogWarning($"[보물상자 뚜껑 분리] '{prefabPath}': 루트에 IOpenableContainer를 구현한 상자 스크립트" +
                                 "(TreasureChest/ItemPedestal)가 없습니다. 뚜껑은 분리했지만 열림 연출은 동작하지 않습니다.");
            }

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Debug.Log($"[보물상자 뚜껑 분리] '{prefabPath}': 적용 완료 (몸통 삼각형 {CountTriangles(savedBody)}개 / " +
                      $"뚜껑 삼각형 {CountTriangles(savedLid)}개).");
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ---------------- 메시 분리 ----------------

    private static bool SplitMesh(Mesh source, Vector3 up, float ratio, out Mesh body, out Mesh lid, out string error)
    {
        body = null;
        lid = null;
        error = null;

        Vector3[] verts = source.vertices;
        if (verts.Length == 0)
        {
            error = "메시에 정점이 없습니다.";
            return false;
        }

        float minH = float.MaxValue, maxH = float.MinValue;
        var heights = new float[verts.Length];
        for (int i = 0; i < verts.Length; i++)
        {
            heights[i] = Vector3.Dot(verts[i], up);
            if (heights[i] < minH) minH = heights[i];
            if (heights[i] > maxH) maxH = heights[i];
        }

        float split = Mathf.Lerp(minH, maxH, ratio);
        float eps = (maxH - minH) * 0.002f; // 분리 높이에 "딱 붙어있다"고 볼 허용 오차

        int subCount = source.subMeshCount;
        var bodyTris = new List<int>[subCount];
        var lidTris = new List<int>[subCount];
        int straddling = 0;
        int bodyTotal = 0, lidTotal = 0;

        for (int s = 0; s < subCount; s++)
        {
            bodyTris[s] = new List<int>();
            lidTris[s] = new List<int>();
            if (source.GetTopology(s) != MeshTopology.Triangles)
            {
                error = "삼각형이 아닌 형태의 서브메시가 있습니다.";
                return false;
            }

            int[] tris = source.GetTriangles(s);
            for (int i = 0; i < tris.Length; i += 3)
            {
                int a = tris[i], b = tris[i + 1], c = tris[i + 2];
                float ha = heights[a], hb = heights[b], hc = heights[c];

                bool anyAbove = ha > split + eps || hb > split + eps || hc > split + eps;
                bool anyBelow = ha < split - eps || hb < split - eps || hc < split - eps;

                bool isLid;
                if (anyAbove && anyBelow)
                {
                    straddling++;
                    continue;
                }
                else if (anyAbove)
                {
                    isLid = true;
                }
                else if (anyBelow)
                {
                    isLid = false;
                }
                else
                {
                    // 분리 높이에 딱 붙어있는 납작한 면: 아래를 보면 뚜껑의 아랫면, 위를 보면 몸통의 윗면
                    Vector3 n = Vector3.Cross(verts[b] - verts[a], verts[c] - verts[a]);
                    isLid = Vector3.Dot(n, up) < 0f;
                }

                var list = isLid ? lidTris[s] : bodyTris[s];
                list.Add(a); list.Add(b); list.Add(c);
                if (isLid) lidTotal++; else bodyTotal++;
            }
        }

        if (straddling > 0)
        {
            error = $"분리 높이를 가로지르는 삼각형이 {straddling}개 있습니다(뚜껑과 몸통이 실제로 이어져 있음). " +
                    "분리 높이 비율을 바꿔보거나, 3ds Max에서 뚜껑을 분리해야 합니다.";
            return false;
        }
        if (bodyTotal == 0 || lidTotal == 0)
        {
            error = "분리 결과 한쪽이 비었습니다(분리 높이 비율을 확인하세요).";
            return false;
        }

        body = BuildSubsetMesh(source, bodyTris, source.name + "_Body");
        lid = BuildSubsetMesh(source, lidTris, source.name + "_Lid");
        return true;
    }

    // 원본 메시에서 지정한 삼각형들만 골라 새 메시를 만듭니다. 정점 속성(노멀/탄젠트/UV/색)을 전부 그대로
    // 복사하고, 서브메시 개수도 원본과 똑같이 유지해 재질 순서가 어긋나지 않게 합니다.
    private static Mesh BuildSubsetMesh(Mesh src, List<int>[] trisPerSub, string meshName)
    {
        var remap = new Dictionary<int, int>();
        var used = new List<int>();
        foreach (var list in trisPerSub)
        {
            foreach (int idx in list)
            {
                if (!remap.ContainsKey(idx))
                {
                    remap[idx] = used.Count;
                    used.Add(idx);
                }
            }
        }

        var mesh = new Mesh { name = meshName };
        if (used.Count > 65535) mesh.indexFormat = IndexFormat.UInt32;

        Vector3[] v = src.vertices;
        mesh.vertices = Pick(v, used);

        Vector3[] n = src.normals;
        if (n != null && n.Length == v.Length) mesh.normals = Pick(n, used);

        Vector4[] tan = src.tangents;
        if (tan != null && tan.Length == v.Length) mesh.tangents = Pick(tan, used);

        Color[] col = src.colors;
        if (col != null && col.Length == v.Length) mesh.colors = Pick(col, used);

        var uvBuffer = new List<Vector4>();
        for (int ch = 0; ch < 8; ch++)
        {
            if (!src.HasVertexAttribute((VertexAttribute)((int)VertexAttribute.TexCoord0 + ch))) continue;
            uvBuffer.Clear();
            src.GetUVs(ch, uvBuffer);
            if (uvBuffer.Count != v.Length) continue;
            var picked = new List<Vector4>(used.Count);
            foreach (int idx in used) picked.Add(uvBuffer[idx]);
            mesh.SetUVs(ch, picked);
        }

        mesh.subMeshCount = trisPerSub.Length;
        for (int s = 0; s < trisPerSub.Length; s++)
        {
            var newTris = new int[trisPerSub[s].Count];
            for (int i = 0; i < newTris.Length; i++) newTris[i] = remap[trisPerSub[s][i]];
            mesh.SetTriangles(newTris, s, false);
        }

        mesh.RecalculateBounds();
        return mesh;
    }

    private static T[] Pick<T>(T[] src, List<int> used)
    {
        var result = new T[used.Count];
        for (int i = 0; i < used.Count; i++) result[i] = src[used[i]];
        return result;
    }

    private static int CountTriangles(Mesh m)
    {
        int count = 0;
        for (int s = 0; s < m.subMeshCount; s++) count += (int)m.GetIndexCount(s) / 3;
        return count;
    }

    // ---------------- 에셋 저장 ----------------

    private static string EnsureOutputFolder(string parentFolder)
    {
        string folder = $"{parentFolder}/{OutputFolderName}";
        if (!AssetDatabase.IsValidFolder(folder))
        {
            AssetDatabase.CreateFolder(parentFolder, OutputFolderName);
        }
        return folder;
    }

    // 같은 경로에 이미 메시 파일이 있으면 내용만 덮어써서(파일 ID 유지) 기존 연결이 끊어지지 않게 합니다.
    private static Mesh SaveMeshAsset(Mesh mesh, string assetPath)
    {
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
        if (existing != null)
        {
            EditorUtility.CopySerialized(mesh, existing);
            existing.name = Path.GetFileNameWithoutExtension(assetPath);
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(mesh);
            return existing;
        }

        mesh.name = Path.GetFileNameWithoutExtension(assetPath);
        AssetDatabase.CreateAsset(mesh, assetPath);
        return mesh;
    }
}
#endif
