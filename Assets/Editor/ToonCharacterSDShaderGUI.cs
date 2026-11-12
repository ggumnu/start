using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// "MobArchive/Toon_Character_SD" 재질 인스펙터(에디터 전용). 셰이더 맨 아래 CustomEditor 줄이 이 클래스를 가리킵니다.
// ID 영역 색은 "인스펙터에 보일 영역 수"만큼만 펼칩니다.
// 보여주기만 바꿀 뿐 재질 값이나 그리는 결과에는 영향이 없습니다(숨긴 칸의 값도 그대로 저장되어 있음).
public class ToonCharacterSDShaderGUI : ShaderGUI
{
    private const string IdColorPrefix = "_IdColor";

    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
    {
        MaterialProperty count = FindProperty("_IdRegionCount", properties, false);
        int shownCount = count != null ? Mathf.Clamp(Mathf.RoundToInt(count.floatValue), 1, 32) : 32;

        materialEditor.SetDefaultGUIWidths();

        foreach (MaterialProperty prop in properties)
        {
            if ((prop.propertyFlags & ShaderPropertyFlags.HideInInspector) != 0) continue;

            string name = prop.name;
            if (name.StartsWith(IdColorPrefix) && GetIdColorNumber(name) > shownCount) continue;

            materialEditor.ShaderProperty(prop, prop.displayName);

            if (name == "_IdMap") DrawIdMapWarning(prop);
        }

        EditorGUILayout.Space();
        materialEditor.RenderQueueField();
        materialEditor.EnableInstancingField();
        materialEditor.DoubleSidedGIField();
    }

    // "_IdColor07" -> 7 (읽지 못하면 0 = 항상 보임)
    private static int GetIdColorNumber(string name)
    {
        int.TryParse(name.Substring(IdColorPrefix.Length), out int number);
        return number;
    }

    // ID 맵은 번호 그림이라 가져오기 설정이 틀리면 경계에 엉뚱한 색이 나옵니다. 틀린 경우만 경고를 띄웁니다.
    private static void DrawIdMapWarning(MaterialProperty idMapProp)
    {
        var tex = idMapProp.textureValue as Texture2D;
        if (tex == null) return;

        var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(tex)) as TextureImporter;
        if (importer == null) return;

        bool ok = !importer.sRGBTexture
                  && importer.filterMode == FilterMode.Point
                  && !importer.mipmapEnabled
                  && importer.textureCompression == TextureImporterCompression.Uncompressed;
        if (ok) return;

        EditorGUILayout.HelpBox(
            "ID 맵 가져오기 설정이 맞지 않습니다(sRGB 끔 / Filter Point / 밉맵 끔 / 압축 없음 필요).\n" +
            "메뉴 MobArchive > SD 캐릭터 재질 자동 설정을 다시 실행하거나 텍스처 설정을 직접 맞춰주세요.",
            MessageType.Warning);
    }
}
