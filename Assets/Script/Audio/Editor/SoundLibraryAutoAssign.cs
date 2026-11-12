using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// 효과음 클립 자동 배치(에디터 전용): Assets/Audio/SFX 의 "SFX_<소리 종류 이름>"(여러 개면 _1, _2 ...) 클립을 SoundLibrary의 같은 종류 칸에 넣습니다.
// 이미 클립이 하나라도 들어 있는 칸은 건드리지 않습니다(직접 바꾼 칸 보호) - 다시 채우고 싶으면 그 칸의 Clips를 비운 뒤 메뉴를 다시 누르면 됩니다.
// 소리 종류 이름 = SfxId의 코드 이름(예: SFX_GrenadeExplode, SFX_Footstep_1). 메뉴 MobArchive/사운드/효과음 클립 자동 배치 (빈 칸만).
public static class SoundLibraryAutoAssign
{
    private const string ClipFolder = "Assets/Audio/SFX";
    private const string LibraryPath = "Assets/Resources/" + SoundLibrary.ResourcePath + ".asset";

    [MenuItem("MobArchive/사운드/효과음 클립 자동 배치 (빈 칸만)")]
    private static void Run()
    {
        var library = AssetDatabase.LoadAssetAtPath<SoundLibrary>(LibraryPath);
        if (library == null)
        {
            EditorUtility.DisplayDialog("효과음 클립 자동 배치",
                "SoundLibrary 에셋이 없습니다.\n먼저 메뉴 MobArchive/사운드/사운드 라이브러리 만들기를 눌러 주세요.", "확인");
            return;
        }
        if (!AssetDatabase.IsValidFolder(ClipFolder))
        {
            EditorUtility.DisplayDialog("효과음 클립 자동 배치", ClipFolder + " 폴더가 없습니다.", "확인");
            return;
        }

        Undo.RecordObject(library, "효과음 클립 자동 배치");
        library.EnsureEntries();

        var groups = new Dictionary<SfxId, List<KeyValuePair<int, AudioClip>>>();
        var unknownNames = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { ClipFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string name = Path.GetFileNameWithoutExtension(path);
            if (!name.StartsWith("SFX_", StringComparison.Ordinal)) continue;

            string idName = name.Substring(4);
            int order = 0;
            int underscore = idName.LastIndexOf('_');
            if (underscore > 0 && int.TryParse(idName.Substring(underscore + 1), out int number))
            {
                order = number;
                idName = idName.Substring(0, underscore);
            }

            if (!Enum.TryParse(idName, false, out SfxId id) || id == SfxId.None)
            {
                unknownNames.Add(name);
                continue;
            }

            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null) continue;
            if (!groups.TryGetValue(id, out var list)) groups[id] = list = new List<KeyValuePair<int, AudioClip>>();
            list.Add(new KeyValuePair<int, AudioClip>(order, clip));
        }

        int filled = 0, kept = 0;
        foreach (var entry in library.entries)
        {
            if (entry == null || !groups.TryGetValue(entry.id, out var list)) continue;
            bool hasClip = entry.clips != null && entry.clips.Any(c => c != null);
            if (hasClip)
            {
                kept++;
                continue;
            }
            entry.clips = list.OrderBy(p => p.Key).Select(p => p.Value).ToArray();
            filled++;
        }

        EditorUtility.SetDirty(library);
        AssetDatabase.SaveAssets();
        Selection.activeObject = library;
        EditorGUIUtility.PingObject(library);

        string message = $"빈 칸 {filled}개에 클립을 넣었습니다.\n이미 클립이 있어 그대로 둔 칸: {kept}개";
        if (unknownNames.Count > 0) message += $"\n이름이 소리 종류와 맞지 않아 건너뛴 파일: {string.Join(", ", unknownNames)}";
        Debug.Log("SoundLibraryAutoAssign: " + message.Replace("\n", " / "));
        EditorUtility.DisplayDialog("효과음 클립 자동 배치", message, "확인");
    }
}
