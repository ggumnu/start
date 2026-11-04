using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// [셰이더 일괄 교체 도구]
// 메뉴: MobArchive > 셰이더 일괄 교체
//
// 프로젝트(Assets 폴더) 안에서 아직 "Universal Render Pipeline/Lit"을 쓰는 재질을 모두 찾아 목록으로
// 보여주고, 체크한 재질만 버튼 한 번으로 MobArchive/Toon_Environment(배경용) 또는
// MobArchive/Toon_Character(캐릭터용)로 바꿉니다.
//
// - 두 툰 셰이더는 URP/Lit과 프로퍼티 이름이 같아서, 셰이더만 바꿔도 텍스처/색/노멀 맵이 그대로 유지됩니다.
// - 교체 후 Ctrl+Z로 되돌릴 수 있고, 나중에라도 재질의 Shader를 다시 URP/Lit으로 고르면 원래대로 돌아갑니다.
// - URP/Lit이 아닌 재질(아이템 아이콘, 이펙트, UI 등)은 목록에 아예 나오지 않으므로 건드리지 않습니다.
// - 이미 Toon_Environment(배경용)으로 바꾼 재질 전체의 외곽선을 한 번에 켜고 끄는 버튼도 있습니다.
// - FBX 안에 들어있어서 직접 바꿀 수 없는 재질은 아래쪽에 따로 표시합니다. 그 FBX는 Inspector의
//   Materials 탭에서 "Extract Materials..."로 재질을 꺼낸 뒤 [다시 검색]하면 위 목록에 나타납니다.
public class ToonShaderBatchConverter : EditorWindow
{
    private const string LitShaderName = "Universal Render Pipeline/Lit";
    private const string EnvShaderName = "MobArchive/Toon_Environment";
    private const string CharShaderName = "MobArchive/Toon_Character";

    private static readonly string[] TargetNames =
    {
        "배경용 (Toon_Environment)",
        "캐릭터용 (Toon_Character)",
    };

    private const int TargetEnvironment = 0;
    private const int TargetCharacter = 1;

    // 처음 검색할 때 기본으로 체크를 꺼둘 폴더입니다(유니티 예제 / 이펙트 / UI / 설정). 필요하면 직접 체크하면 됩니다.
    private static readonly string[] UncheckedByDefaultFolders =
    {
        "Assets/TutorialInfo/",
        "/VFX/",
        "/UI_Sprite/",
        "Assets/Settings/",
    };

    private class Entry
    {
        public Material material;
        public string path;
        public bool selected;
        public int target;
    }

    private class EmbeddedEntry
    {
        public string modelPath;
        public List<string> materialNames;
    }

    private readonly List<Entry> entries = new List<Entry>();
    private readonly List<EmbeddedEntry> embeddedEntries = new List<EmbeddedEntry>();

    private bool setEnvNormalStrength = true;
    private bool enableCharOutline = true;
    private bool showEmbedded = true;
    private Vector2 scroll;

    [MenuItem("MobArchive/셰이더 일괄 교체")]
    private static void Open()
    {
        var window = GetWindow<ToonShaderBatchConverter>("셰이더 일괄 교체");
        window.minSize = new Vector2(640, 420);
        window.Scan();
    }

    private void OnEnable()
    {
        Scan();
    }

    // ---------------- 검색 ----------------
    private void Scan()
    {
        entries.Clear();
        embeddedEntries.Clear();

        // 1) 따로 저장된 재질(.mat 파일)
        foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith(".mat", StringComparison.OrdinalIgnoreCase)) continue;

            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null || mat.shader == null) continue;
            if (mat.shader.name != LitShaderName) continue;

            entries.Add(new Entry
            {
                material = mat,
                path = path,
                selected = !IsUncheckedByDefault(path),
                target = GuessTarget(path),
            });
        }
        entries.Sort((a, b) => string.Compare(a.path, b.path, StringComparison.OrdinalIgnoreCase));

        // 2) FBX 안에 들어있는 재질(직접 바꿀 수 없음 - 표시만)
        foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) continue;
            if (importer.materialImportMode == ModelImporterMaterialImportMode.None) continue;

            // Materials 탭에서 다른 재질로 연결(Remap)해둔 이름은 실제로 쓰이지 않으므로 제외합니다.
            var remapped = new HashSet<string>();
            foreach (var pair in importer.GetExternalObjectMap())
            {
                if (pair.Key.type == typeof(Material) && pair.Value != null) remapped.Add(pair.Key.name);
            }

            var names = new List<string>();
            foreach (var obj in AssetDatabase.LoadAllAssetRepresentationsAtPath(path))
            {
                var mat = obj as Material;
                if (mat == null || mat.shader == null) continue;
                if (mat.shader.name != LitShaderName) continue;
                if (remapped.Contains(mat.name)) continue;
                names.Add(mat.name);
            }

            if (names.Count > 0)
            {
                embeddedEntries.Add(new EmbeddedEntry { modelPath = path, materialNames = names });
            }
        }
        embeddedEntries.Sort((a, b) => string.Compare(a.modelPath, b.modelPath, StringComparison.OrdinalIgnoreCase));

        Repaint();
    }

    private static bool IsUncheckedByDefault(string path)
    {
        foreach (string folder in UncheckedByDefaultFolders)
        {
            if (path.IndexOf(folder, StringComparison.OrdinalIgnoreCase) >= 0) return true;
        }
        return false;
    }

    // 캐릭터 폴더(Assets/Player/Character/) 안의 재질은 캐릭터용, 나머지는 배경용으로 먼저 골라둡니다.
    private static int GuessTarget(string path)
    {
        return path.IndexOf("/Player/Character/", StringComparison.OrdinalIgnoreCase) >= 0
            ? TargetCharacter
            : TargetEnvironment;
    }

    // ---------------- 화면 ----------------
    private void OnGUI()
    {
        Shader envShader = Shader.Find(EnvShaderName);
        Shader charShader = Shader.Find(CharShaderName);

        EditorGUILayout.HelpBox(
            "아직 URP/Lit을 쓰는 재질 목록입니다. 체크한 재질만 오른쪽에서 고른 툰 셰이더로 바뀝니다.\n" +
            "텍스처와 색은 그대로 유지되며, 교체 직후에는 Ctrl+Z로 되돌릴 수 있습니다.",
            MessageType.Info);

        if (envShader == null || charShader == null)
        {
            EditorGUILayout.HelpBox(
                "Toon_Environment 또는 Toon_Character 셰이더를 찾지 못했습니다. Assets/Shaders 폴더에 두 셰이더 파일이 " +
                "있는지, Console에 셰이더 에러가 없는지 확인해주세요.",
                MessageType.Error);
        }

        // 교체할 때 같이 맞춰줄 값(지금까지 손으로 맞춘 값과 동일)
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("교체할 때 함께 적용", EditorStyles.boldLabel);
        setEnvNormalStrength = EditorGUILayout.ToggleLeft("배경용: 노멀 맵 세기를 0.4로 맞추기", setEnvNormalStrength);
        enableCharOutline = EditorGUILayout.ToggleLeft("캐릭터용: 외곽선 사용 켜기", enableCharOutline);

        // 이미 배경용 툰 셰이더로 바꾼 재질 전체의 외곽선 켜기/끄기
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("배경용 재질 외곽선 (이미 Toon_Environment인 재질 전체)", EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(envShader == null))
            {
                if (GUILayout.Button("전체 외곽선 켜기", GUILayout.Width(140))) SetEnvironmentOutline(envShader, true);
                if (GUILayout.Button("전체 외곽선 끄기", GUILayout.Width(140))) SetEnvironmentOutline(envShader, false);
            }
        }

        EditorGUILayout.Space(4);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("다시 검색", GUILayout.Width(90))) Scan();
            if (GUILayout.Button("전체 선택", GUILayout.Width(90))) SetAllSelected(true);
            if (GUILayout.Button("전체 해제", GUILayout.Width(90))) SetAllSelected(false);
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField($"URP/Lit 재질 {entries.Count}개", GUILayout.Width(140));
        }

        scroll = EditorGUILayout.BeginScrollView(scroll);

        if (entries.Count == 0)
        {
            EditorGUILayout.HelpBox("바꿀 URP/Lit 재질이 없습니다.", MessageType.None);
        }

        foreach (var entry in entries)
        {
            if (entry.material == null) continue;

            using (new EditorGUILayout.HorizontalScope())
            {
                entry.selected = EditorGUILayout.Toggle(entry.selected, GUILayout.Width(18));

                // 이름을 누르면 Project 창에서 해당 재질을 찾아 보여줍니다.
                if (GUILayout.Button(entry.material.name, EditorStyles.linkLabel, GUILayout.Width(180)))
                {
                    EditorGUIUtility.PingObject(entry.material);
                    Selection.activeObject = entry.material;
                }

                EditorGUILayout.LabelField(GetFolder(entry.path), EditorStyles.miniLabel, GUILayout.MinWidth(120));

                entry.target = EditorGUILayout.Popup(entry.target, TargetNames, GUILayout.Width(190));
            }
        }

        // FBX 안에 들어있는 재질
        if (embeddedEntries.Count > 0)
        {
            EditorGUILayout.Space(10);
            showEmbedded = EditorGUILayout.Foldout(showEmbedded,
                $"FBX 안에 들어있어 바로 바꿀 수 없는 재질 ({embeddedEntries.Count}개 FBX)", true);

            if (showEmbedded)
            {
                EditorGUILayout.HelpBox(
                    "아래 FBX는 재질이 파일 안에 들어있어 이 도구로 바꿀 수 없습니다. 게임에 쓰는 모델이라면 " +
                    "[선택]을 누른 뒤 Inspector의 Materials 탭에서 \"Extract Materials...\"로 재질을 꺼내고 " +
                    "[다시 검색]을 누르세요. 애니메이션 전용 FBX처럼 화면에 보이지 않는 모델은 무시해도 됩니다.",
                    MessageType.Warning);

                foreach (var embedded in embeddedEntries)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("선택", GUILayout.Width(50)))
                        {
                            var asset = AssetDatabase.LoadMainAssetAtPath(embedded.modelPath);
                            Selection.activeObject = asset;
                            EditorGUIUtility.PingObject(asset);
                        }

                        EditorGUILayout.LabelField(embedded.modelPath, GUILayout.MinWidth(200));
                    }

                    EditorGUILayout.LabelField("    재질: " + string.Join(", ", embedded.materialNames), EditorStyles.miniLabel);
                }
            }
        }

        EditorGUILayout.EndScrollView();

        // 교체 버튼
        EditorGUILayout.Space(6);
        int selectedCount = entries.Count(e => e.selected && e.material != null);
        using (new EditorGUI.DisabledScope(selectedCount == 0 || envShader == null || charShader == null))
        {
            if (GUILayout.Button($"체크한 재질 {selectedCount}개 교체", GUILayout.Height(30)))
            {
                bool ok = EditorUtility.DisplayDialog(
                    "셰이더 일괄 교체",
                    $"체크한 재질 {selectedCount}개의 셰이더를 바꿉니다.\n텍스처와 색은 유지되며, 바로 Ctrl+Z로 되돌릴 수 있습니다.",
                    "교체", "취소");

                if (ok) ConvertSelected(envShader, charShader);
            }
        }
        EditorGUILayout.Space(4);
    }

    private void SetAllSelected(bool value)
    {
        foreach (var entry in entries) entry.selected = value;
    }

    private static string GetFolder(string path)
    {
        int slash = path.LastIndexOf('/');
        return slash > 0 ? path.Substring(0, slash) : path;
    }

    // ---------------- 배경용 재질 외곽선 일괄 설정 ----------------
    private static void SetEnvironmentOutline(Shader envShader, bool enabled)
    {
        var targets = new List<Material>();
        foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith(".mat", StringComparison.OrdinalIgnoreCase)) continue;

            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null || mat.shader != envShader) continue;
            if (!mat.HasProperty("_OutlineEnabled")) continue;
            targets.Add(mat);
        }

        if (targets.Count == 0)
        {
            EditorUtility.DisplayDialog("배경용 재질 외곽선", "Toon_Environment를 쓰는 재질이 없습니다.", "확인");
            return;
        }

        string label = enabled ? "켭니다" : "끕니다";
        if (!EditorUtility.DisplayDialog("배경용 재질 외곽선",
                $"Toon_Environment를 쓰는 재질 {targets.Count}개의 외곽선을 {label}.\n바로 Ctrl+Z로 되돌릴 수 있습니다.",
                "적용", "취소"))
        {
            return;
        }

        Undo.RecordObjects(targets.Select(m => (UnityEngine.Object)m).ToArray(), "배경용 재질 외곽선 설정");
        foreach (var mat in targets)
        {
            mat.SetFloat("_OutlineEnabled", enabled ? 1f : 0f);
            EditorUtility.SetDirty(mat);
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[셰이더 일괄 교체] 배경용 재질 {targets.Count}개의 외곽선을 {label}.");
    }

    // ---------------- 교체 ----------------
    private void ConvertSelected(Shader envShader, Shader charShader)
    {
        var targets = entries.Where(e => e.selected && e.material != null).ToList();
        if (targets.Count == 0) return;

        Undo.RecordObjects(targets.Select(e => (UnityEngine.Object)e.material).ToArray(), "Toon 셰이더 일괄 교체");

        int envCount = 0;
        int charCount = 0;

        foreach (var entry in targets)
        {
            Material mat = entry.material;

            if (entry.target == TargetCharacter)
            {
                mat.shader = charShader;
                if (enableCharOutline && mat.HasProperty("_OutlineEnabled")) mat.SetFloat("_OutlineEnabled", 1f);
                charCount++;
            }
            else
            {
                mat.shader = envShader;
                if (setEnvNormalStrength && mat.HasProperty("_BumpScale")) mat.SetFloat("_BumpScale", 0.4f);
                envCount++;
            }

            EditorUtility.SetDirty(mat);
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[셰이더 일괄 교체] 배경용 {envCount}개, 캐릭터용 {charCount}개 재질을 교체했습니다.");

        Scan();
    }
}
