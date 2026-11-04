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
//    - 이름이 "_Mask"로 끝나는 텍스처: sRGB 끔, 압축 없음(색 영역 경계가 번지지 않게), 알파 채널 그대로 사용
//    - 이름이 "_Base"로 끝나는 텍스처: sRGB 켬, 고품질 압축, 알파 그대로 사용(알파 0 = 피부 영역)
// 2) 재질 만들기 (SD/Materials 폴더)
//    - T_SD_{부위}_Base 텍스처마다 M_SD_{부위} 재질을 "MobArchive/Toon_Character_SD" 셰이더로 만듭니다.
//    - 처음 만들 때만: 명암/림라이트/외곽선 값을 레이사 재질(Player_Body)에서 복사하고, 영역 기본 색과
//      "전체 피부"(Head, Hand)를 넣습니다. 이미 있는 재질은 직접 조정한 값을 덮어쓰지 않고, 비어 있는 텍스처 칸만 채웁니다.
// 3) FBX 재질 연결 (SD 폴더 안의 모든 FBX)
//    - 3ds Max에서 지정한 재질 이름(M_SD_Head 등)과 같은 이름의 재질을 FBX에 연결(Remap)합니다.
//      그래서 앞으로 파츠 FBX를 같은 재질 이름으로 내보내면 이 메뉴 한 번으로 연결됩니다.
//
// [새 파츠를 추가할 때]
// 텍스처를 T_SD_{부위}_Base / T_SD_{부위}_Mask 이름으로 SD/Textures에 넣고, 3ds Max에서 재질 이름을
// M_SD_{부위}로 지정해 내보낸 뒤 이 메뉴를 다시 실행하면 됩니다. 영역 기본 색은 만든 뒤 재질에서 직접 고칩니다.
public class SDCharacterMaterialSetup : AssetPostprocessor
{
    private const string SdRoot = "Assets/Player/Character/Player/SD";
    private const string TextureFolder = SdRoot + "/Textures";
    private const string MaterialFolder = SdRoot + "/Materials";
    private const string ShaderName = "MobArchive/Toon_Character_SD";
    private const string ReferenceMaterialPath = "Assets/Player/Character/Player/Player_Mat/Player_Body.mat";

    // 처음 재질을 만들 때 넣을 영역 기본 색(R, G, B, A). 만든 뒤에는 재질 인스펙터에서 직접 바꾸면 됩니다.
    private static readonly Dictionary<string, string[]> DefaultRegionColors = new Dictionary<string, string[]>
    {
        // 외투 / 세일러 옷깃 / 상의·치마 / 리본·단추·선
        { "Torso", new[] { "#434E6B", "#F3F3F3", "#CBD2DA", "#30374B" } },
        // 신발 본체 / 밑창·토캡·끈·입구 테두리 / 파란 포인트 / 양말
        { "Foot", new[] { "#FBD5F8", "#F6F6F8", "#4F96C8", "#F8F8FA" } },
        // 하늘 머리색 / 분홍 가닥 / 별 머리핀 / (사용 안 함). 머리끈은 텍스처에 고정색으로 그려져 있음
        { "Hair_Reisa01", new[] { "#CFDDF6", "#F3C8EB", "#B4A5EE", "#FFFFFF" } },
    };

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
        var maskPaths = new Dictionary<string, string>();
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
            else if (file.EndsWith("_Mask")) maskPaths[part] = path;
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
            if (maskPaths.TryGetValue(part, out string maskPath) && (created || mat.GetTexture("_MaskMap") == null))
            {
                mat.SetTexture("_MaskMap", AssetDatabase.LoadAssetAtPath<Texture2D>(maskPath));
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
        bool isMask = file.EndsWith("_Mask");
        bool isBase = file.EndsWith("_Base");
        if (!isMask && !isBase) return false;

        bool changed = false;
        if (importer.textureType != TextureImporterType.Default) { importer.textureType = TextureImporterType.Default; changed = true; }

        bool wantSrgb = !isMask;
        if (importer.sRGBTexture != wantSrgb) { importer.sRGBTexture = wantSrgb; changed = true; }

        // 마스크의 A 채널은 네 번째 색 영역, 기본 텍스처의 A 채널은 "피부 영역" 표시라서 둘 다 "투명도"가
        // 아닙니다. Alpha Is Transparency가 켜지면 Unity가 알파 0인 곳의 색을 바꿔버리므로 반드시 끕니다.
        if (importer.alphaSource != TextureImporterAlphaSource.FromInput) { importer.alphaSource = TextureImporterAlphaSource.FromInput; changed = true; }
        if (importer.alphaIsTransparency) { importer.alphaIsTransparency = false; changed = true; }

        if (isMask)
        {
            if (importer.textureCompression != TextureImporterCompression.Uncompressed) { importer.textureCompression = TextureImporterCompression.Uncompressed; changed = true; }
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

        if (DefaultRegionColors.TryGetValue(part, out string[] colors))
        {
            string[] props = { "_RegionColorR", "_RegionColorG", "_RegionColorB", "_RegionColorA" };
            for (int i = 0; i < props.Length && i < colors.Length; i++)
            {
                if (ColorUtility.TryParseHtmlString(colors[i], out Color c)) mat.SetColor(props[i], c);
            }
        }
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
    }
}
