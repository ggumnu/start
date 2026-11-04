#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// 선택한 오브젝트들(장애물 프리팹/FBX 모델/씬 오브젝트)에 콜라이더 + Tag + Layer를
// 한 번에 세팅해주는 에디터 전용 도구입니다.
// (RoomAssemblyGuide.cs / InvisibleColliderGizmo.cs와 같은 계열의 "조립 보조 도구".)
//
// [배경]
// Projectile.cs / EnemyProjectile.cs는 Tag == "Wall"인지만 보고 총알을 소멸시키고,
// EnemyRangedAIController.wallLayerMask는 별도의 물리 레이어(예: "Wall" 레이어)로 시야
// 차단을 판정합니다. 장애물 하나를 제대로 만들려면 매번 (1) 콜라이더 크기 맞추기
// (2) Tag = Wall (3) Layer = Wall(물리 레이어), 이 세 가지를 반복해야 하는데, 오브젝트
// 개수가 많아지면 제일 번거로운 부분이라 이 도구로 한 번에 처리합니다.
//
// [v3 변경 - 두 가지 동작 모드]
// - "씬에 바로 적용": v2와 동일하게, 선택한 씬 오브젝트(또는 프리팹 애셋) 밑에
//   콜라이더 전용 자식 오브젝트를 추가합니다. 딱 한 번만 쓰이는 세트피스에 적합합니다.
//   다만 그 오브젝트가 여러 방에서 재사용되는 "프리팹 풀" 소속이라면, 이 모드는 지금
//   선택한 그 인스턴스(또는 그 프리팹 애셋 자체를 선택한 경우엔 애셋)에만 적용됩니다.
// - "콜라이더 포함된 새 프리팹 만들기": 원본 FBX/프리팹을 감싸는 새로운 "래퍼" 프리팹을
//   만들어서 .prefab 파일로 저장합니다. 원본 FBX는 전혀 건드리지 않고(리임포트해도
//   안전), 그 안에 원본을 "중첩 프리팹 인스턴스"로 시각적으로만 넣고, 콜라이더/Tag/
//   Layer는 새 래퍼 오브젝트 쪽에 답니다. 결과로 나온 .prefab을 LevelBuilder의
//   floorPool/wallPools 등 프리팹 풀에 넣으면, 이후 스폰되는 모든 인스턴스에 자동으로
//   콜라이더가 딸려옵니다 - 반복 사용되는 장애물/벽 프리팹에는 이 모드를 쓰세요.
//   (FBX 원본에 직접 컴포넌트를 못 붙이는 이유와 동일한 원리로, 새로 만든 래퍼
//   오브젝트는 "생성된 프리팹"이 아니라 일반 오브젝트라 자유롭게 컴포넌트를 붙일 수
//   있습니다. 작업은 화면에 안 보이는 임시 프리뷰 씬에서 처리되어 현재 열려있는 씬을
//   전혀 건드리지 않습니다.)
//
// [콜라이더 종류]
// - Box / Capsule / 자동 판단: 바운딩 박스 기반 근사치. 가볍고 대부분의 경우 충분합니다.
// - 정확한 모양(Mesh Collider): 오브젝트 안의 모든 서브메쉬(MeshFilter)마다 그 메쉬를
//   그대로 참조하는 Mesh Collider를 하나씩 만듭니다. Convex를 켜지 않아 오목한 형태도
//   완전히 정확하게 막습니다 - 이 프로젝트의 장애물들은 Rigidbody가 없는 정적
//   오브젝트라 "Convex 콜라이더는 Rigidbody에만 필요하다"는 Unity 제약에 걸리지
//   않습니다. 다만 서브메쉬 개수만큼 콜라이더가 생기므로, 파츠가 아주 많은 복잡한
//   오브젝트(조립형 건물 등)는 Box/Capsule 근사보다 계산 비용이 커질 수 있습니다 -
//   단순한 형태는 Box/Capsule, 형태가 중요한 소수의 오브젝트는 정확한 모양을 추천합니다.
//
// [사용 방법]
// 1. Assets/Editor 폴더 아래에 넣습니다 (없으면 새로 만들기 - 이름이 정확히 "Editor").
// 2. Tools > 블아이작 > 장애물 콜라이더 일괄 설정.
// 3. 모드를 고르고, 프리팹 풀 소속 오브젝트라면 Project 창에서 원본 FBX/프리팹 애셋을
//    선택(씬 인스턴스가 아니라 애셋 자체를!), 세트피스라면 씬에서 오브젝트를 선택합니다.
// 4. 옵션 확인 후 버튼 클릭.
public class ObstacleColliderBatchTool : EditorWindow
{
    private enum ToolMode { ApplyToSelection, GenerateWrappedPrefabs }
    private enum ColliderShape { Box, Capsule, AutoDecide, ExactMesh }

    private const string ChildNameSuffix = "_Collider";
    private const string MeshChildSuffix = "_MeshCollider";

    private ToolMode toolMode = ToolMode.ApplyToSelection;
    private ColliderShape shape = ColliderShape.AutoDecide;
    private bool replaceExisting = false;
    private float padding = 0f;

    private bool applyTag = true;
    private string tagName = "Wall";
    private bool applyLayer = true;
    private string layerName = "Wall";
    private bool addGizmo = true;

    private string outputFolder = "Assets/Prefabs/Generated";
    private string prefabSuffix = "_Wall";
    private bool overwriteExistingPrefab = true;

    [MenuItem("Tools/블아이작/장애물 콜라이더 일괄 설정")]
    private static void Open()
    {
        GetWindow<ObstacleColliderBatchTool>("장애물 콜라이더 일괄 설정");
    }

    private void OnGUI()
    {
        toolMode = (ToolMode)EditorGUILayout.EnumPopup("모드", toolMode);

        if (toolMode == ToolMode.ApplyToSelection)
        {
            EditorGUILayout.HelpBox(
                "선택한 씬 오브젝트(또는 선택한 프리팹 애셋 자체)에 바로 콜라이더 자식을 추가합니다.\n" +
                "여러 방에서 재사용되는 프리팹 풀 소속 오브젝트라면 '새 프리팹 만들기' 모드를 쓰세요.",
                MessageType.Info);
        }
        else
        {
            EditorGUILayout.HelpBox(
                "Project 창에서 원본 FBX/프리팹 애셋을 선택하세요(씬 인스턴스 아님). " +
                "원본은 건드리지 않고, 콜라이더가 달린 새 프리팹(.prefab)을 별도로 생성합니다.",
                MessageType.Info);
            outputFolder = EditorGUILayout.TextField("출력 폴더", outputFolder);
            prefabSuffix = EditorGUILayout.TextField("새 프리팹 이름 접미사", prefabSuffix);
            overwriteExistingPrefab = EditorGUILayout.Toggle("동일 경로 프리팹 있으면 덮어쓰기", overwriteExistingPrefab);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("콜라이더", EditorStyles.boldLabel);
        shape = (ColliderShape)EditorGUILayout.EnumPopup("종류", shape);
        if (shape == ColliderShape.AutoDecide)
        {
            EditorGUILayout.HelpBox(
                "가로/세로 단면이 거의 정사각형이면서 세로로 긴 오브젝트는 Capsule, 나머지는 Box.",
                MessageType.None);
        }
        else if (shape == ColliderShape.ExactMesh)
        {
            EditorGUILayout.HelpBox(
                "서브메쉬마다 Mesh Collider(Convex 꺼짐)를 붙여 형태를 그대로 재현합니다. " +
                "Rigidbody가 없는 정적 오브젝트 전용입니다.", MessageType.None);
        }

        using (new EditorGUI.DisabledScope(shape == ColliderShape.ExactMesh))
        {
            padding = EditorGUILayout.FloatField(new GUIContent("여유값(패딩)", "Box/Capsule 전용. 실제 메쉬보다 이만큼(월드 유닛) 더 크게 잡습니다."), padding);
        }
        replaceExisting = EditorGUILayout.Toggle("기존에 만들어둔 콜라이더 자식 있으면 교체", replaceExisting);
        addGizmo = EditorGUILayout.Toggle(new GUIContent("InvisibleColliderGizmo 추가", "씬 뷰에서 반투명 박스로 보이게 합니다."), addGizmo);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Tag / Layer", EditorStyles.boldLabel);
        applyTag = EditorGUILayout.Toggle("Tag 지정", applyTag);
        using (new EditorGUI.DisabledScope(!applyTag)) tagName = EditorGUILayout.TextField("Tag 이름", tagName);
        applyLayer = EditorGUILayout.Toggle("Layer 지정", applyLayer);
        using (new EditorGUI.DisabledScope(!applyLayer)) layerName = EditorGUILayout.TextField("Layer 이름", layerName);

        EditorGUILayout.Space();
        int count = Selection.gameObjects.Length;
        using (new EditorGUI.DisabledScope(count == 0))
        {
            string buttonLabel = toolMode == ToolMode.ApplyToSelection
                ? $"선택한 {count}개에 적용"
                : $"선택한 {count}개로 새 프리팹 {count}개 생성";
            if (GUILayout.Button(buttonLabel, GUILayout.Height(32)))
            {
                if (toolMode == ToolMode.ApplyToSelection) ApplyToSelection(Selection.gameObjects);
                else GenerateWrappedPrefabs(Selection.gameObjects);
            }
        }
    }

    // ---------- 모드 A: 선택한 대상에 바로 적용 ----------
    private void ApplyToSelection(GameObject[] targets)
    {
        int layerIndex = ResolveLayer();

        Undo.SetCurrentGroupName("장애물 콜라이더 일괄 설정");
        int group = Undo.GetCurrentGroup();

        int applied = 0, skippedNoRenderer = 0;
        foreach (var go in targets)
        {
            int n = shape == ColliderShape.ExactMesh
                ? ApplyExactMesh(go, layerIndex)
                : ApplyBoxOrCapsule(go, layerIndex);

            if (n == 0) skippedNoRenderer++;
            else applied += n;
        }

        Undo.CollapseUndoOperations(group);
        AssetDatabase.SaveAssets();
        Debug.Log($"ObstacleColliderBatchTool: 콜라이더 {applied}개 적용 완료 (렌더러 없어서 건너뜀 {skippedNoRenderer}개).");
    }

    // ---------- 모드 B: 콜라이더 포함된 새 프리팹 생성 ----------
    private void GenerateWrappedPrefabs(GameObject[] sources)
    {
        int layerIndex = ResolveLayer();
        EnsureFolder(outputFolder);

        var previewScene = EditorSceneManager.NewPreviewScene();
        var createdAssets = new List<Object>();
        int created = 0, skippedNoRenderer = 0;

        try
        {
            foreach (var source in sources)
            {
                // 씬 인스턴스를 선택했다면 원본 애셋을 역추적합니다.
                var sourceAsset = PrefabUtility.GetCorrespondingObjectFromOriginalSource(source);
                if (sourceAsset == null) sourceAsset = source; // 이미 애셋 자체를 선택한 경우

                GameObject wrapperRoot = new GameObject(source.name + prefabSuffix);
                SceneManager.MoveGameObjectToScene(wrapperRoot, previewScene);

                GameObject visual = PrefabUtility.InstantiatePrefab(sourceAsset, previewScene) as GameObject;
                if (visual == null) visual = Object.Instantiate(sourceAsset) as GameObject; // 프리팹이 아닌 경우의 폴백

                visual.transform.SetParent(wrapperRoot.transform, false);
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;
                visual.transform.localScale = Vector3.one;

                int n = shape == ColliderShape.ExactMesh
                    ? ApplyExactMeshNoUndo(visual, layerIndex)
                    : ApplyBoxOrCapsuleNoUndo(wrapperRoot, layerIndex);

                if (n == 0)
                {
                    skippedNoRenderer++;
                    Object.DestroyImmediate(wrapperRoot);
                    continue;
                }

                string path = AssetDatabase.GenerateUniqueAssetPath($"{outputFolder}/{wrapperRoot.name}.prefab");
                if (overwriteExistingPrefab)
                {
                    string directPath = $"{outputFolder}/{wrapperRoot.name}.prefab";
                    if (AssetDatabase.LoadAssetAtPath<GameObject>(directPath) != null) path = directPath;
                }

                var savedPrefab = PrefabUtility.SaveAsPrefabAsset(wrapperRoot, path, out bool success);
                Object.DestroyImmediate(wrapperRoot);

                if (success)
                {
                    created++;
                    createdAssets.Add(savedPrefab);
                }
                else
                {
                    Debug.LogWarning($"ObstacleColliderBatchTool: '{source.name}' 프리팹 저장에 실패했습니다 ({path}).");
                }
            }
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(previewScene);
        }

        AssetDatabase.SaveAssets();
        if (createdAssets.Count > 0) Selection.objects = createdAssets.ToArray();

        Debug.Log($"ObstacleColliderBatchTool: 새 프리팹 {created}개 생성 완료 (렌더러 없어서 건너뜀 {skippedNoRenderer}개). " +
                   $"생성 위치: {outputFolder}");
    }

    // ---------- 공용 로직 ----------

    private int ResolveLayer()
    {
        if (!applyLayer) return -1;
        int layerIndex = LayerMask.NameToLayer(layerName);
        if (layerIndex < 0)
        {
            Debug.LogWarning($"ObstacleColliderBatchTool: '{layerName}' 레이어가 프로젝트에 없습니다. " +
                              "Edit > Project Settings > Tags and Layers에서 먼저 만들어주세요. Layer 지정을 건너뜁니다.");
        }
        return layerIndex;
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        var parts = folder.Split('/');
        string current = parts[0]; // "Assets"
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    // Box/Capsule/Auto 콜라이더를 parent 밑에 "_Collider" 자식으로 추가합니다 (Undo 기록 O).
    private int ApplyBoxOrCapsule(GameObject parent, int layerIndex)
    {
        var localBounds = ComputeLocalBounds(parent);
        if (localBounds == null) return 0;

        string childName = parent.name + ChildNameSuffix;
        var existing = parent.transform.Find(childName);
        if (existing != null)
        {
            if (!replaceExisting) return 0;
            Undo.DestroyObjectImmediate(existing.gameObject);
        }

        var colliderGO = new GameObject(childName);
        Undo.RegisterCreatedObjectUndo(colliderGO, "Create Obstacle Collider");
        BuildBoxOrCapsuleCollider(colliderGO, parent.transform, localBounds.Value, layerIndex, true);
        return 1;
    }

    // 위와 동일하지만 임시 프리뷰 씬에서 작업하므로 Undo를 기록하지 않는 버전입니다.
    private int ApplyBoxOrCapsuleNoUndo(GameObject parent, int layerIndex)
    {
        var localBounds = ComputeLocalBounds(parent);
        if (localBounds == null) return 0;

        var colliderGO = new GameObject(parent.name + ChildNameSuffix);
        BuildBoxOrCapsuleCollider(colliderGO, parent.transform, localBounds.Value, layerIndex, false);
        return 1;
    }

    private void BuildBoxOrCapsuleCollider(GameObject colliderGO, Transform parent, Bounds b, int layerIndex, bool withUndo)
    {
        colliderGO.transform.SetParent(parent, false);
        colliderGO.transform.localPosition = b.center;
        colliderGO.transform.localRotation = Quaternion.identity;
        colliderGO.transform.localScale = Vector3.one;

        Vector3 size = b.size + Vector3.one * padding;
        ColliderShape actual = shape;
        if (actual == ColliderShape.AutoDecide || actual == ColliderShape.ExactMesh)
        {
            float footprintDiff = Mathf.Abs(size.x - size.z) / Mathf.Max(size.x, size.z, 0.0001f);
            float avgFootprint = (size.x + size.z) * 0.5f;
            bool nearSquare = footprintDiff < 0.3f;
            bool taller = size.y > avgFootprint * 1.2f;
            actual = (nearSquare && taller) ? ColliderShape.Capsule : ColliderShape.Box;
        }

        if (actual == ColliderShape.Box)
        {
            var box = withUndo ? Undo.AddComponent<BoxCollider>(colliderGO) : colliderGO.AddComponent<BoxCollider>();
            box.center = Vector3.zero;
            box.size = size;
        }
        else
        {
            var cap = withUndo ? Undo.AddComponent<CapsuleCollider>(colliderGO) : colliderGO.AddComponent<CapsuleCollider>();
            cap.center = Vector3.zero;
            cap.direction = 1;
            float radius = Mathf.Max(size.x, size.z) * 0.5f;
            cap.radius = radius;
            cap.height = Mathf.Max(size.y, radius * 2f);
        }

        FinishColliderObject(colliderGO, layerIndex, withUndo);
    }

    // 서브메쉬마다 Mesh Collider를 하나씩 만듭니다. 각 메쉬 노드 밑에 자식으로 붙여
    // (그 노드가 FBX 임포트로 생성된 것이든 아니든 새 자식 추가는 항상 허용됩니다)
    // 로컬 좌표를 identity로 두면 원본 메쉬 형태와 정확히 겹칩니다.
    private int ApplyExactMesh(GameObject root, int layerIndex) => ApplyExactMeshInternal(root, layerIndex, true);
    private int ApplyExactMeshNoUndo(GameObject root, int layerIndex) => ApplyExactMeshInternal(root, layerIndex, false);

    private int ApplyExactMeshInternal(GameObject root, int layerIndex, bool withUndo)
    {
        var meshFilters = root.GetComponentsInChildren<MeshFilter>();
        int applied = 0;

        foreach (var mf in meshFilters)
        {
            if (mf.sharedMesh == null) continue;

            string childName = mf.gameObject.name + MeshChildSuffix;
            var existing = mf.transform.Find(childName);
            if (existing != null)
            {
                if (!replaceExisting) continue;
                if (withUndo) Undo.DestroyObjectImmediate(existing.gameObject);
                else Object.DestroyImmediate(existing.gameObject);
            }

            var colliderGO = new GameObject(childName);
            if (withUndo) Undo.RegisterCreatedObjectUndo(colliderGO, "Create Mesh Collider");
            colliderGO.transform.SetParent(mf.transform, false);
            colliderGO.transform.localPosition = Vector3.zero;
            colliderGO.transform.localRotation = Quaternion.identity;
            colliderGO.transform.localScale = Vector3.one;

            var mc = withUndo ? Undo.AddComponent<MeshCollider>(colliderGO) : colliderGO.AddComponent<MeshCollider>();
            mc.sharedMesh = mf.sharedMesh;
            mc.convex = false; // Rigidbody가 없는 정적 오브젝트라 오목한 형태 그대로 사용 가능

            FinishColliderObject(colliderGO, layerIndex, withUndo);
            applied++;
        }

        return applied;
    }

    private void FinishColliderObject(GameObject colliderGO, int layerIndex, bool withUndo)
    {
        if (addGizmo)
        {
            var gizmo = withUndo ? Undo.AddComponent<InvisibleColliderGizmo>(colliderGO) : colliderGO.AddComponent<InvisibleColliderGizmo>();
            gizmo.gizmoColor = new Color(1f, 0.55f, 0.1f, 0.5f);
        }

        if (applyTag)
        {
            try { colliderGO.tag = tagName; }
            catch (UnityException)
            {
                Debug.LogWarning($"ObstacleColliderBatchTool: '{colliderGO.name}'에 '{tagName}' 태그를 지정하지 못했습니다. " +
                                  "Edit > Project Settings > Tags and Layers에서 먼저 태그를 추가해주세요.");
            }
        }

        if (applyLayer && layerIndex >= 0) colliderGO.layer = layerIndex;

        EditorUtility.SetDirty(colliderGO);
    }

    // 대상 오브젝트 자신 + 모든 자식의 Renderer들을 모아서, 대상 오브젝트의 로컬 좌표계
    // 기준 바운딩 박스를 계산합니다. 렌더러가 하나도 없으면 null을 반환합니다.
    private static Bounds? ComputeLocalBounds(GameObject root)
    {
        var renderers = root.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return null;

        bool has = false;
        Bounds localBounds = new Bounds();

        foreach (var rend in renderers)
        {
            Bounds wb = rend.bounds;
            Vector3 c = wb.center;
            Vector3 e = wb.extents;

            for (int xi = -1; xi <= 1; xi += 2)
                for (int yi = -1; yi <= 1; yi += 2)
                    for (int zi = -1; zi <= 1; zi += 2)
                    {
                        Vector3 worldCorner = c + new Vector3(e.x * xi, e.y * yi, e.z * zi);
                        Vector3 localCorner = root.transform.InverseTransformPoint(worldCorner);

                        if (!has) { localBounds = new Bounds(localCorner, Vector3.zero); has = true; }
                        else localBounds.Encapsulate(localCorner);
                    }
        }

        return has ? (Bounds?)localBounds : null;
    }
}
#endif