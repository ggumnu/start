using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// [에디터 전용 - 손으로 붙인 NightLight 한 번에 지우기]
// 메뉴: MobArchive > Night Light > 손으로 붙인 NightLight 모두 지우기
// 프로젝트의 모든 프리팹과 지금 열려 있는 씬에서 NightLight를 지웁니다.
// Night Light Auto Rules에 견본으로 등록된 프리팹은 건너뜁니다(견본은 지우면 안 되므로).
// NightLight 말고 다른 것이 없는 빈 오브젝트는 오브젝트째 지우고, 다른 컴포넌트/자식이 있으면 NightLight 컴포넌트만 지웁니다.
public static class NightLightCleanupTool
{
    [MenuItem("MobArchive/Night Light/손으로 붙인 NightLight 모두 지우기")]
    private static void Run()
    {
        if (PrefabStageUtility.GetCurrentPrefabStage() != null)
        {
            EditorUtility.DisplayDialog("NightLight 지우기", "프리팹 편집 화면이 열려 있습니다.\nHierarchy 왼쪽 위의 < 화살표로 프리팹 편집 화면을 닫은 뒤 다시 실행해 주세요.", "확인");
            return;
        }

        HashSet<string> templatePaths = CollectTemplatePaths();

        var prefabPaths = new List<string>();
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (templatePaths.Contains(path))
                continue;

            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset != null && asset.GetComponentsInChildren<NightLight>(true).Length > 0)
                prefabPaths.Add(path);
        }

        var sceneLights = new List<NightLight>();
        for (int s = 0; s < EditorSceneManager.sceneCount; s++)
        {
            var scene = EditorSceneManager.GetSceneAt(s);
            if (!scene.isLoaded)
                continue;

            foreach (GameObject root in scene.GetRootGameObjects())
                sceneLights.AddRange(root.GetComponentsInChildren<NightLight>(true));
        }

        if (prefabPaths.Count == 0 && sceneLights.Count == 0)
        {
            EditorUtility.DisplayDialog("NightLight 지우기", "지울 NightLight가 없습니다.", "확인");
            return;
        }

        string message =
            $"프리팹 {prefabPaths.Count}개, 열린 씬의 NightLight {sceneLights.Count}개를 지웁니다.\n" +
            $"견본으로 등록된 프리팹 {templatePaths.Count}개는 건너뜁니다.\n\n" +
            "프리팹에서 지운 것은 Ctrl+Z로 되돌릴 수 없습니다. 계속할까요?";

        if (!EditorUtility.DisplayDialog("NightLight 지우기", message, "지우기", "취소"))
            return;

        int removed = 0;
        int skipped = 0;

        for (int i = 0; i < prefabPaths.Count; i++)
        {
            string path = prefabPaths[i];
            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                int before = removed;
                RemoveAll(contents, false, ref removed, ref skipped);
                if (removed > before)
                    PrefabUtility.SaveAsPrefabAsset(contents, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        if (sceneLights.Count > 0)
        {
            var touchedScenes = new HashSet<UnityEngine.SceneManagement.Scene>();
            for (int i = 0; i < sceneLights.Count; i++)
            {
                NightLight nl = sceneLights[i];
                if (nl == null)
                    continue;
                touchedScenes.Add(nl.gameObject.scene);
                RemoveOne(nl, true, ref removed, ref skipped);
            }

            foreach (var scene in touchedScenes)
                EditorSceneManager.MarkSceneDirty(scene);
        }

        AssetDatabase.SaveAssets();

        string done = $"NightLight {removed}개를 지웠습니다.";
        if (skipped > 0)
            done += $"\n중첩 프리팹 안에 있어서 건너뛴 것 {skipped}개(원본 프리팹 쪽에서 함께 지워졌습니다).";
        if (sceneLights.Count > 0)
            done += "\n씬을 저장(Ctrl+S)해 주세요.";

        Debug.Log("[NightLight] " + done.Replace("\n", " "));
        EditorUtility.DisplayDialog("NightLight 지우기", done, "확인");
    }

    private static HashSet<string> CollectTemplatePaths()
    {
        var paths = new HashSet<string>();
        string[] guids = AssetDatabase.FindAssets("t:NightLightAutoRules");
        for (int i = 0; i < guids.Length; i++)
        {
            var rules = AssetDatabase.LoadAssetAtPath<NightLightAutoRules>(AssetDatabase.GUIDToAssetPath(guids[i]));
            if (rules == null || rules.rules == null)
                continue;

            foreach (var rule in rules.rules)
            {
                if (rule != null && rule.template != null)
                    paths.Add(AssetDatabase.GetAssetPath(rule.template));
            }
        }
        return paths;
    }

    private static void RemoveAll(GameObject root, bool useUndo, ref int removed, ref int skipped)
    {
        NightLight[] lights = root.GetComponentsInChildren<NightLight>(true);
        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i] != null)
                RemoveOne(lights[i], useUndo, ref removed, ref skipped);
        }
    }

    private static void RemoveOne(NightLight nl, bool useUndo, ref int removed, ref int skipped)
    {
        GameObject go = nl.gameObject;

        bool goRemovable = !PrefabUtility.IsPartOfPrefabInstance(go) || PrefabUtility.IsAddedGameObjectOverride(go);
        bool onlyLight = go.transform.childCount == 0 && go.GetComponents<Component>().Length == 2;

        if (onlyLight && goRemovable)
        {
            if (useUndo) Undo.DestroyObjectImmediate(go);
            else Object.DestroyImmediate(go);
            removed++;
            return;
        }

        bool compRemovable = !PrefabUtility.IsPartOfPrefabInstance(nl) || PrefabUtility.IsAddedComponentOverride(nl);
        if (compRemovable)
        {
            if (useUndo) Undo.DestroyObjectImmediate(nl);
            else Object.DestroyImmediate(nl, true);
            removed++;
            return;
        }

        skipped++;
    }
}
