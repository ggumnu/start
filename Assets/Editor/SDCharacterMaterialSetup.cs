using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// SD 캐릭터 재질 자동 설정 도구 (에디터 전용)
//
// [메뉴]  MobArchive > SD 캐릭터 재질 자동 설정
//
// [하는 일] 아래 순서로 처리하며, 여러 번 실행해도 안전합니다(이미 된 것은 건너뜀).
// 1) 텍스처 가져오기 설정 (SD/Textures 폴더)
//    - 이름이 "_Base"로 끝나는 텍스처: sRGB 켬, 고품질 압축, 알파 그대로 사용(알파 0 = 피부 영역)
//    - 이름이 "_ID"로 끝나는 텍스처(색 영역 ID 맵, 번호 그림): sRGB 끔, 압축 없음, Filter Point, 밉맵 끔
// 2) 재질 만들기 (SD/Materials 폴더)
//    - T_SD_{부위}_Base 텍스처마다 M_SD_{부위} 재질을 "MobArchive/Toon_Character_SD" 셰이더로 만듭니다.
//    - 처음 만들 때만: 명암/림라이트/외곽선 값을 레이사 재질(Player_Body)에서 복사하고,
//      "전체 피부"(Head, Hand)를 켭니다. 이미 있는 재질은 직접 조정한 값을 덮어쓰지 않고, 비어 있는 텍스처 칸만 채웁니다.
//    - T_SD_{부위}_ID가 있으면 ID 맵 칸에 넣습니다(ID 맵 칸이 비어 있을 때만). 이때 같은 폴더에
//      T_SD_{부위}_IDColors.txt(영역 색표)가 있으면 영역 색/피부 번호/보일 영역 수도 함께 넣습니다.
//      색표 형식: 한 줄에 "번호 #RRGGBB" (예: 03 #F3C8EB), 피부 번호는 "skin 번호", "//"로 시작하는 줄은 메모.
// 3) FBX 재질 연결 (SD 폴더 안의 모든 FBX)
//    - 3ds Max에서 지정한 재질 이름(M_SD_Head 등)과 같은 이름의 재질을 FBX에 연결(Remap)합니다.
//      그래서 앞으로 파츠 FBX를 같은 재질 이름으로 내보내면 이 메뉴 한 번으로 연결됩니다.
//
// [새 파츠를 추가할 때]
// 텍스처를 T_SD_{부위}_Base / T_SD_{부위}_ID / T_SD_{부위}_IDColors.txt 이름으로 SD/Textures에 넣고, 3ds Max에서 재질 이름을
// M_SD_{부위}로 지정해 내보낸 뒤 이 메뉴를 다시 실행하면 됩니다. 영역 색은 만든 뒤 재질에서 직접 고쳐도 됩니다.
public class SDCharacterMaterialSetup : AssetPostprocessor
{
    private const string SdRoot = "Assets/Player/Character/Player/SD";
    private const string TextureFolder = SdRoot + "/Textures";
    private const string MaterialFolder = SdRoot + "/Materials";
    private const string ShaderName = "MobArchive/Toon_Character_SD";
    private const string ReferenceMaterialPath = "Assets/Player/Character/Player/Player_Mat/Player_Body.mat";

    // 처음 만들 때 "전체 피부"를 켤 부위
    private static readonly HashSet<string> WholeSkinParts = new HashSet<string> { "Head", "Hand" };

    // 레이사 재질에서 복사할 툰 표현 값
    private static readonly string[] CopyFloats =
    {
        "_ShadowThreshold", "_ShadowSmoothness", "_ShadeAmount", "_ReceiveShadowAmount", "_Brightness",
        "_RimStrength", "_RimWidth", "_OutlineEnabled", "_OutlineWidth", "_OutlineBaseColorBlend",
    };
    private static readonly string[] CopyColors = { "_ShadowColor", "_RimColor", "_OutlineColor" };

    // ---------------------------------------------------------------- 새 텍스처가 처음 들어올 때
    // SD/Textures에 처음 들어오는 텍스처는 가져오기 설정을 자동으로 맞춥니다.
    // (이미 들어온 텍스처의 설정을 인스펙터에서 직접 바꾼 경우에는 덮어쓰지 않습니다.)
    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(TextureFolder + "/")) return;
        if (!assetImporter.importSettingsMissing) return;

        var importer = assetImporter as TextureImporter;
        if (importer != null) ApplyTextureSettings(importer, assetPath);
    }

    // ---------------------------------------------------------------- 메뉴
    [MenuItem("MobArchive/SD 캐릭터 재질 자동 설정")]
    private static void Run()
    {
        Shader shader = Shader.Find(ShaderName);
        if (shader == null)
        {
            EditorUtility.DisplayDialog("SD 캐릭터 재질 자동 설정",
                "셰이더 '" + ShaderName + "'를 찾지 못했습니다.\nAssets/Shaders/Toon_Character_SD.shader가 있는지, 콘솔에 셰이더 오류가 없는지 확인해주세요.",
                "확인");
            return;
        }

        if (!AssetDatabase.IsValidFolder(TextureFolder))
        {
            EditorUtility.DisplayDialog("SD 캐릭터 재질 자동 설정",
                "텍스처 폴더를 찾지 못했습니다:\n" + TextureFolder, "확인");
            return;
        }

        var log = new List<string>();

        // 1) 텍스처 가져오기 설정
        var basePaths = new Dictionary<string, string>();
        var idPaths = new Dictionary<string, string>();
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { TextureFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string file = Path.GetFileNameWithoutExtension(path);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) continue;

            if (ApplyTextureSettings(importer, path))
            {
                importer.SaveAndReimport();
                log.Add("텍스처 설정: " + file);
            }

            string part = GetPartName(file);
            if (part == null) continue;
            if (file.EndsWith("_Base")) basePaths[part] = path;
            else if (file.EndsWith("_ID")) idPaths[part] = path;
        }

        // 2) 재질 만들기 / 텍스처 채우기
        EnsureFolder(MaterialFolder);
        Material reference = AssetDatabase.LoadAssetAtPath<Material>(ReferenceMaterialPath);
        var materials = new List<Material>();

        foreach (var pair in basePaths)
        {
            string part = pair.Key;
            string matPath = MaterialFolder + "/M_SD_" + part + ".mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            bool created = false;

            if (mat == null)
            {
                mat = new Material(shader);
                CopyToonSettings(reference, mat);
                ApplyDefaults(part, mat);
                AssetDatabase.CreateAsset(mat, matPath);
                created = true;
                log.Add("재질 생성: M_SD_" + part);
            }
            else if (mat.shader != shader)
            {
                mat.shader = shader;
                log.Add("셰이더 교체: M_SD_" + part);
            }

            // 비어 있는 텍스처 칸만 채웁니다(직접 다른 텍스처를 넣은 경우 유지).
            if (created || mat.GetTexture("_BaseMap") == null)
            {
                mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(pair.Value));
            }
            if (idPaths.TryGetValue(part, out string idPath) && mat.HasProperty("_IdMap") && mat.GetTexture("_IdMap") == null)
            {
                mat.SetTexture("_IdMap", AssetDatabase.LoadAssetAtPath<Texture2D>(idPath));
                log.Add("ID 맵 연결: M_SD_" + part + ApplyIdColorTable(part, mat));
            }

            EditorUtility.SetDirty(mat);
            materials.Add(mat);
        }

        // M_SD_로 시작하는 기존 재질(텍스처 없이 직접 만든 것 포함)도 연결 대상에 넣습니다.
        foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { MaterialFolder }))
        {
            Material m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            if (m != null && m.name.StartsWith("M_SD_") && !materials.Contains(m)) materials.Add(m);
        }
        AssetDatabase.SaveAssets();

        // 3) FBX 재질 연결
        foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { SdRoot }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) continue;

            var current = importer.GetExternalObjectMap();
            bool changed = false;
            foreach (Material mat in materials)
            {
                var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), mat.name);
                if (current.TryGetValue(id, out Object existing) && existing == mat) continue;
                importer.AddRemap(id, mat);
                changed = true;
            }

            if (changed)
            {
                importer.SaveAndReimport();
                log.Add("FBX 재질 연결: " + Path.GetFileName(path));
            }
        }

        string message = log.Count == 0 ? "바뀐 것이 없습니다. (이미 모두 설정되어 있습니다)" : string.Join("\n", log);
        EditorUtility.DisplayDialog("SD 캐릭터 재질 자동 설정", message, "확인");
    }

    // ---------------------------------------------------------------- 도우미
    // 텍스처 설정을 맞춥니다. 바꾼 것이 있으면 true를 돌려줍니다.
    private static bool ApplyTextureSettings(TextureImporter importer, string path)
    {
        string file = Path.GetFileNameWithoutExtension(path);
        bool isBase = file.EndsWith("_Base");
        bool isId = file.EndsWith("_ID");
        if (!isBase && !isId) return false;

        bool changed = false;
        if (importer.textureType != TextureImporterType.Default) { importer.textureType = TextureImporterType.Default; changed = true; }

        bool wantSrgb = isBase;
        if (importer.sRGBTexture != wantSrgb) { importer.sRGBTexture = wantSrgb; changed = true; }

        // 기본 텍스처의 A 채널은 "피부 영역" 표시라서 "투명도"가 아닙니다. Alpha Is Transparency가 켜지면 Unity가 알파 0인 곳의 색을 바꿔버리므로 반드시 끕니다.
        if (importer.alphaSource != TextureImporterAlphaSource.FromInput) { importer.alphaSource = TextureImporterAlphaSource.FromInput; changed = true; }
        if (importer.alphaIsTransparency) { importer.alphaIsTransparency = false; changed = true; }

        if (isId)
        {
            // ID 맵은 픽셀 값이 "영역 번호"라서 압축/필터/밉맵이 값을 섞으면 엉뚱한 영역 색이 나옵니다.
            if (importer.textureCompression != TextureImporterCompression.Uncompressed) { importer.textureCompression = TextureImporterCompression.Uncompressed; changed = true; }
            if (importer.filterMode != FilterMode.Point) { importer.filterMode = FilterMode.Point; changed = true; }
            if (importer.mipmapEnabled) { importer.mipmapEnabled = false; changed = true; }
        }
        else
        {
            if (importer.textureCompression != TextureImporterCompression.CompressedHQ) { importer.textureCompression = TextureImporterCompression.CompressedHQ; changed = true; }
        }
        return changed;
    }

    // "T_SD_Torso_Base" -> "Torso"
    private static string GetPartName(string file)
    {
        const string prefix = "T_SD_";
        if (!file.StartsWith(prefix)) return null;
        int last = file.LastIndexOf('_');
        if (last <= prefix.Length) return null;
        return file.Substring(prefix.Length, last - prefix.Length);
    }

    private static void CopyToonSettings(Material reference, Material target)
    {
        if (reference == null)
        {
            // 레이사 재질이 없으면 플레이어 재질과 같은 외곽선 설정만 켭니다.
            target.SetFloat("_OutlineEnabled", 1f);
            target.SetFloat("_OutlineWidth", 1f);
            target.SetFloat("_OutlineBaseColorBlend", 1f);
            return;
        }

        foreach (string name in CopyFloats)
        {
            if (reference.HasProperty(name) && target.HasProperty(name)) target.SetFloat(name, reference.GetFloat(name));
        }
        foreach (string name in CopyColors)
        {
            if (reference.HasProperty(name) && target.HasProperty(name)) target.SetColor(name, reference.GetColor(name));
        }
    }

    private static void ApplyDefaults(string part, Material mat)
    {
        if (WholeSkinParts.Contains(part)) mat.SetFloat("_WholeSkin", 1f);
    }

    // T_SD_{부위}_IDColors.txt를 읽어 영역 색/피부 번호/보일 영역 수를 넣습니다. 로그에 붙일 문구를 돌려줍니다.
    private static string ApplyIdColorTable(string part, Material mat)
    {
        string tablePath = TextureFolder + "/T_SD_" + part + "_IDColors.txt";
        var table = AssetDatabase.LoadAssetAtPath<TextAsset>(tablePath);
        if (table == null) return " (색표 없음 - 영역 색은 재질에서 직접 지정)";

        int applied = 0;
        int maxId = 0;
        string[] lines = table.text.Split('\n');
        foreach (string raw in lines)
        {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("//")) continue;

            string[] tokens = line.Split(new[] { ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length < 2) continue;

            if (tokens[0].ToLowerInvariant() == "skin")
            {
                if (int.TryParse(tokens[1], out int skinId) && skinId >= 0 && skinId <= 32) mat.SetFloat("_IdSkinRegion", skinId);
                continue;
            }

            if (!int.TryParse(tokens[0], out int id) || id < 1 || id > 32) continue;
            if (!ColorUtility.TryParseHtmlString(tokens[1], out Color c)) continue;

            mat.SetColor("_IdColor" + id.ToString("00"), c);
            applied++;
            if (id > maxId) maxId = id;
        }

        if (maxId > 0) mat.SetFloat("_IdRegionCount", maxId);
        return " (색표 " + applied + "개 적용)";
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
    }
}
