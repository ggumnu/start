using UnityEditor;
using UnityEngine;

// 설정 창 HUD 탭 견본 연결 자동 채우기(에디터 전용): Resources/SettingsUISettings 에셋의 Player HUD Prefab 칸에
// Assets/Config/Level/PlayerHUD, 전투/비전투 견본 배경 칸에 Assets/UI_Sprite/UI/UI_SettingsPreviewBG 를 넣습니다.
// 인벤토리 견본 예시 아이콘(Preview Slot Icons / Preview Passive Icons)은 ItemDatabase에서 장신구/일회용/음식 첫 아이템과 패시브
// 앞쪽 7개(아이콘 있는 것)를 골라 넣습니다 - 실행 중에는 계산하지 않고 이 에셋에 고정으로 저장됩니다(형빈 확정).
// 이미 채워진 칸은 건드리지 않습니다(직접 바꾼 칸 보호) - 다시 채우고 싶으면 칸을 비운 뒤 메뉴를 다시 누르면 됩니다.
// 메뉴 MobArchive/설정 창/HUD 견본 연결 자동 채우기.
public static class SettingsUIAutoAssign
{
    private const string SettingsPath = "Assets/Resources/" + SettingsUISettings.ResourcePath + ".asset";
    private const string PlayerHUDPath = "Assets/Config/Level/PlayerHUD.prefab";
    private const string PreviewBackgroundPath = "Assets/UI_Sprite/UI/UI_SettingsPreviewBG.png";

    [MenuItem("MobArchive/설정 창/HUD 견본 연결 자동 채우기")]
    private static void Run()
    {
        var settings = AssetDatabase.LoadAssetAtPath<SettingsUISettings>(SettingsPath);
        if (settings == null)
        {
            EditorUtility.DisplayDialog("HUD 견본 연결", SettingsPath + " 에셋이 없습니다.", "확인");
            return;
        }

        Undo.RecordObject(settings, "HUD 견본 연결 자동 채우기");
        var report = new System.Text.StringBuilder();

        if (settings.playerHUDPrefab == null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerHUDPath);
            if (prefab != null)
            {
                settings.playerHUDPrefab = prefab;
                report.AppendLine("Player HUD Prefab: 연결함");
            }
            else report.AppendLine("Player HUD Prefab: " + PlayerHUDPath + " 를 찾지 못함");
        }
        else report.AppendLine("Player HUD Prefab: 이미 채워져 있어 그대로 둠");

        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PreviewBackgroundPath);
        if (sprite == null)
        {
            report.AppendLine("견본 배경: " + PreviewBackgroundPath + " 를 Sprite로 읽지 못함(Texture Type = Sprite (2D and UI) 확인)");
        }
        else
        {
            if (settings.normalPreviewBackground == null) { settings.normalPreviewBackground = sprite; report.AppendLine("비전투 견본 배경: 연결함"); }
            else report.AppendLine("비전투 견본 배경: 이미 채워져 있어 그대로 둠");
            if (settings.combatPreviewBackground == null) { settings.combatPreviewBackground = sprite; report.AppendLine("전투 견본 배경: 연결함"); }
            else report.AppendLine("전투 견본 배경: 이미 채워져 있어 그대로 둠");
        }

        FillPreviewIcons(settings, report);

        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("HUD 견본 연결", report.ToString(), "확인");
    }

    private const int PassivePreviewCount = 7;

    private static void FillPreviewIcons(SettingsUISettings settings, System.Text.StringBuilder report)
    {
        ItemDatabase database = null;
        foreach (string guid in AssetDatabase.FindAssets("t:ItemDatabase"))
        {
            database = AssetDatabase.LoadAssetAtPath<ItemDatabase>(AssetDatabase.GUIDToAssetPath(guid));
            if (database != null) break;
        }
        if (database == null || database.allItems == null)
        {
            report.AppendLine("견본 아이콘: ItemDatabase 에셋을 찾지 못함");
            return;
        }

        // 슬롯 견본: 0 = 장신구, 1 = 일회용, 2 = 음식 (비어 있는 칸만)
        if (settings.previewSlotIcons == null || settings.previewSlotIcons.Length < 3)
        {
            var resized = new Sprite[3];
            if (settings.previewSlotIcons != null)
                for (int i = 0; i < settings.previewSlotIcons.Length && i < 3; i++) resized[i] = settings.previewSlotIcons[i];
            settings.previewSlotIcons = resized;
        }
        var slotCategories = new[]
        {
            ItemDefinition.ItemCategory.Trinket, ItemDefinition.ItemCategory.Disposable, ItemDefinition.ItemCategory.Food,
        };
        int slotFilled = 0;
        for (int i = 0; i < slotCategories.Length; i++)
        {
            if (settings.previewSlotIcons[i] != null) continue;
            foreach (var item in database.allItems)
            {
                if (item == null || item.icon == null || item.category != slotCategories[i]) continue;
                settings.previewSlotIcons[i] = item.icon;
                slotFilled++;
                break;
            }
        }
        report.AppendLine("슬롯 견본 아이콘: " + slotFilled + "칸 채움");

        // 패시브 견본: 비어 있을 때만 앞쪽 7개
        bool passiveEmpty = settings.previewPassiveIcons == null || settings.previewPassiveIcons.Length == 0;
        if (!passiveEmpty)
        {
            report.AppendLine("패시브 견본 아이콘: 이미 채워져 있어 그대로 둠");
            return;
        }
        var list = new System.Collections.Generic.List<Sprite>();
        foreach (var item in database.allItems)
        {
            if (item == null || item.icon == null || item.category != ItemDefinition.ItemCategory.Passive) continue;
            list.Add(item.icon);
            if (list.Count >= PassivePreviewCount) break;
        }
        settings.previewPassiveIcons = list.ToArray();
        report.AppendLine("패시브 견본 아이콘: " + list.Count + "개 채움");
    }
}
