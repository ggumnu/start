using UnityEditor;
using UnityEngine;

// [110번 관통 강화 총알 - 피격 굴절 재질 자동 생성 도구]
// 메뉴 "MobArchive/VFX/110번 피격 굴절 재질 만들기"를 누르면:
// 1) MobArchive/HitRipple 셰이더로 재질 Assets/Shaders/HitRipple_Mat.mat을 만듭니다(이미 있으면 그대로 씀).
// 2) 프로젝트의 모든 Item Definition 중 110번 효과(ChargeShotTrinketEffect)를 가진 것을 찾아 Settings > Hit Ripple > Material 칸에 넣습니다
//    (이미 다른 재질이 들어 있으면 건드리지 않음). Hit Ripple 묶음이 비어 있던 예전 에셋이면 기본값으로 채웁니다.
// 굴절 세기/크기/시간 등은 110번 Item Definition의 Hit Ripple 묶음에서 조절합니다(재질 자체에는 조절할 값이 없음).
public static class ChargedHitRippleBuilder
{
    private const string ShaderName = "MobArchive/HitRipple";
    private const string TargetFolder = "Assets/Shaders";
    private const string TargetPath = "Assets/Shaders/HitRipple_Mat.mat";

    [MenuItem("MobArchive/VFX/110번 피격 굴절 재질 만들기")]
    public static void Build()
    {
        Shader shader = Shader.Find(ShaderName);
        if (shader == null)
        {
            EditorUtility.DisplayDialog("110번 피격 굴절", $"셰이더 {ShaderName}를 찾지 못했습니다. HitRipple.shader가 Assets 안에 있는지 확인하세요.", "확인");
            return;
        }

        EnsureFolder(TargetFolder);
        var mat = AssetDatabase.LoadAssetAtPath<Material>(TargetPath);
        if (mat == null)
        {
            mat = new Material(shader) { name = "HitRipple_Mat" };
            AssetDatabase.CreateAsset(mat, TargetPath);
        }
        else if (mat.shader != shader)
        {
            mat.shader = shader;
            EditorUtility.SetDirty(mat);
        }

        int assigned = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:ItemDefinition"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var def = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
            if (def == null || !(def.trinketEffect is ChargeShotTrinketEffect effect)) continue;

            if (effect.settings == null) effect.settings = new ChargeShotOverride.Settings();
            if (effect.settings.hitRipple == null) effect.settings.hitRipple = new ChargedHitRipple.Settings();
            if (effect.settings.hitRipple.material != null) continue;

            effect.settings.hitRipple.material = mat;
            EditorUtility.SetDirty(def);
            assigned++;
        }

        AssetDatabase.SaveAssets();
        Selection.activeObject = mat;
        EditorGUIUtility.PingObject(mat);
        EditorUtility.DisplayDialog("110번 피격 굴절",
            $"재질을 만들었습니다: {TargetPath}\n110번 Item Definition {assigned}개에 연결했습니다(이미 재질이 있던 것은 그대로).", "확인");
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        string parent = System.IO.Path.GetDirectoryName(folder).Replace('\\', '/');
        string name = System.IO.Path.GetFileName(folder);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }
}
