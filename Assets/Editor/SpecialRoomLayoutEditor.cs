using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// 특수방 레이아웃 에셋(SpecialRoomLayout) Inspector 맨 아래에 "이 레이아웃에서 나올 수 있는 아이템"을 읽기 전용으로
// 보여주는 에디터 전용 스크립트입니다(형빈 확정 A안 - 등록은 아이템 에셋의 Appearance Contexts 한 곳에서만 하고,
// 레이아웃에서는 결과를 확인만 합니다).
//
// [보여주는 것]
// - 이 레이아웃 전용: Appearance Contexts에 Special + 이 레이아웃 ID가 있는 아이템
// - 특수방 공용: Appearance Contexts에 Special + (카테고리 전체)가 있는 아이템. 이 레이아웃의 Include Shared
//   Special Items가 꺼져 있으면 "등장하지 않음"으로 흐리게 표시합니다.
// 판정 규칙은 실제 게임(LevelBuilder.SpawnSpecialItemPedestal → ItemDatabase.GetItemsForRoom)과 같은
// ItemDatabase.MatchesRoom을 그대로 씁니다. 그래서 여기 보이는 목록 = 실제 후보 목록입니다. 단 게임 중에는 여기에
// 더해 "이미 가진 아이템/장착 중인 아이템/이 레벨에서 이미 나온 아이템"이 빠집니다.
//
// [진입 조건 확률]
// 맨 위에는 Entry Condition Pool(진입 조건 후보)이 실제로 몇 % 확률로 뽑히는지도 보여줍니다.
//
// [Item Database 선택]
// 실제 게임에서 쓰는 아이템 목록은 LevelBuilder의 Item Database 칸에 연결된 에셋입니다. 프로젝트에 Item Database가
// 여러 개(예: 테스트용)일 수 있어 미리보기 기준을 위쪽 드롭다운에서 고를 수 있고, 고른 값은 이 컴퓨터에 기억됩니다.
// 처음에는 이름에 "Test"가 없는 것을 먼저 고릅니다.
//
// [설정]
// Assets/Editor 폴더에 두기만 하면 됩니다. 씬이나 에셋에 따로 붙일 필요가 없습니다.
[CustomEditor(typeof(SpecialRoomLayout))]
public class SpecialRoomLayoutEditor : Editor
{
    private const string DatabasePrefKey = "MobArchive.SpecialRoomLayoutEditor.ItemDatabaseGuid";

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var layout = (SpecialRoomLayout)target;

        DrawEntryConditionChances(layout);

        EditorGUILayout.Space(14f);
        EditorGUILayout.LabelField("아이템 미리보기 (읽기 전용)", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("등록은 각 아이템 에셋의 Appearance Contexts에서 합니다.", EditorStyles.miniLabel);

        DrawLayoutIdWarnings(layout);

        if (!layout.spawnItemPedestal)
        {
            EditorGUILayout.HelpBox("내용물의 Spawn Item Pedestal이 꺼져 있어, 지금은 이 방에 실제로 아이템이 나오지 않습니다. " +
                                    "아래 목록은 등장 규칙 확인용입니다.", MessageType.Info);
        }

        var database = DrawDatabasePicker();
        if (database == null) return;

        // 전용: Special + 이 레이아웃 ID (공용은 빼고 확인)
        var exclusive = string.IsNullOrEmpty(layout.layoutId)
            ? new List<ItemDefinition>()
            : FindItems(database, ctx => ItemDatabase.MatchesRoom(ctx, RoomCategory.Special, layout.layoutId, false));

        // 공용: Special + (카테고리 전체). variantId 없이 확인하면 비어 있는 컨텍스트만 맞습니다.
        var shared = FindItems(database, ctx => ItemDatabase.MatchesRoom(ctx, RoomCategory.Special, null, true));

        // 전용과 공용 양쪽에 등록된 아이템은 전용 쪽에만 보여줍니다(실제 후보에서도 한 번만 들어감).
        shared = shared.Where(item => !exclusive.Contains(item)).ToList();

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField($"이 레이아웃 전용 ({exclusive.Count})", EditorStyles.boldLabel);
        if (string.IsNullOrEmpty(layout.layoutId))
        {
            EditorGUILayout.LabelField("  레이아웃 ID가 비어 있어 전용 아이템을 찾을 수 없습니다.", EditorStyles.miniLabel);
        }
        else
        {
            DrawItemList(exclusive, true);
        }

        EditorGUILayout.Space(6f);
        string sharedTitle = layout.includeSharedSpecialItems
            ? $"특수방 공용 ({shared.Count})"
            : $"특수방 공용 ({shared.Count}) - 이 레이아웃에서는 등장하지 않음";
        EditorGUILayout.LabelField(sharedTitle, EditorStyles.boldLabel);
        DrawItemList(shared, layout.includeSharedSpecialItems);

        int total = exclusive.Count + (layout.includeSharedSpecialItems ? shared.Count : 0);
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField($"실제 후보 합계: {total}개", EditorStyles.miniBoldLabel);
        if (total == 0)
        {
            EditorGUILayout.HelpBox("후보가 0개입니다. 아이템 받침대를 열면 기본 아이템으로 대체됩니다.", MessageType.Warning);
        }
    }

    // 진입 조건이 실제로 어떤 확률로 뽑히는지 보여줍니다(SpecialRoomLayout.PickEntryCondition과 같은 규칙).
    // 같은 조건이 여러 줄에 있으면 합쳐서 보여줍니다.
    private static void DrawEntryConditionChances(SpecialRoomLayout layout)
    {
        EditorGUILayout.Space(14f);
        EditorGUILayout.LabelField("진입 조건 확률 (읽기 전용)", EditorStyles.boldLabel);

        float total = layout.GetEntryConditionPoolTotalWeight();
        if (total <= 0f)
        {
            string reason = (layout.entryConditionPool == null || layout.entryConditionPool.Count == 0)
                ? "후보 목록이 비어 있어"
                : "후보의 가중치가 전부 0이라";
            EditorGUILayout.LabelField($"  {reason} 항상 {layout.entryCondition} (100%)", EditorStyles.miniLabel);
            return;
        }

        var sums = new Dictionary<SpecialRoomEntryCondition, float>();
        foreach (var option in layout.entryConditionPool)
        {
            if (option == null || option.weight <= 0f) continue;
            sums.TryGetValue(option.condition, out float current);
            sums[option.condition] = current + option.weight;
        }

        foreach (var pair in sums.OrderBy(p => (int)p.Key))
        {
            EditorGUILayout.LabelField($"  {pair.Key}: {pair.Value / total * 100f:0.#}%", EditorStyles.miniLabel);
        }
        EditorGUILayout.LabelField("  위 Entry Condition(고정 조건)은 후보 목록이 있는 동안 쓰이지 않습니다.", EditorStyles.miniLabel);
    }

    // 레이아웃 ID가 비었거나 다른 레이아웃과 겹치면 경고합니다(겹치면 두 레이아웃이 같은 전용 아이템/미니맵 아이콘을 공유하게 됨).
    private static void DrawLayoutIdWarnings(SpecialRoomLayout layout)
    {
        if (string.IsNullOrEmpty(layout.layoutId))
        {
            EditorGUILayout.HelpBox("Layout Id가 비어 있습니다. 미니맵 아이콘/전용 아이템/전용 벽 세트와 연결하려면 고유한 ID를 넣어주세요.",
                                    MessageType.Warning);
            return;
        }

        var duplicates = new List<string>();
        foreach (var guid in AssetDatabase.FindAssets($"t:{nameof(SpecialRoomLayout)}"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var other = AssetDatabase.LoadAssetAtPath<SpecialRoomLayout>(path);
            if (other == null || other == layout) continue;
            if (string.Equals(other.layoutId, layout.layoutId, System.StringComparison.OrdinalIgnoreCase))
            {
                duplicates.Add(other.name);
            }
        }

        if (duplicates.Count > 0)
        {
            EditorGUILayout.HelpBox($"Layout Id '{layout.layoutId}'가 다른 레이아웃({string.Join(", ", duplicates)})과 겹칩니다. " +
                                    "서로 다른 ID를 써주세요.", MessageType.Error);
        }
    }

    // 미리보기 기준 Item Database를 고르는 드롭다운입니다. 고른 에셋을 돌려주고, 하나도 없으면 null.
    private static ItemDatabase DrawDatabasePicker()
    {
        var guids = AssetDatabase.FindAssets($"t:{nameof(ItemDatabase)}").ToList();
        if (guids.Count == 0)
        {
            EditorGUILayout.HelpBox("프로젝트에 Item Database 에셋이 없어 미리보기를 할 수 없습니다.", MessageType.Warning);
            return null;
        }

        var names = guids.Select(g => System.IO.Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(g))).ToArray();

        string savedGuid = EditorPrefs.GetString(DatabasePrefKey, string.Empty);
        int index = guids.IndexOf(savedGuid);
        if (index < 0)
        {
            index = System.Array.FindIndex(names, n => n.IndexOf("Test", System.StringComparison.OrdinalIgnoreCase) < 0);
            if (index < 0) index = 0;
        }

        int newIndex = EditorGUILayout.Popup("미리보기 기준 Item Database", index, names);
        if (newIndex != index || savedGuid != guids[newIndex])
        {
            EditorPrefs.SetString(DatabasePrefKey, guids[newIndex]);
        }

        return AssetDatabase.LoadAssetAtPath<ItemDatabase>(AssetDatabase.GUIDToAssetPath(guids[newIndex]));
    }

    private static List<ItemDefinition> FindItems(ItemDatabase database, System.Func<AppearanceContext, bool> predicate)
    {
        if (database.allItems == null) return new List<ItemDefinition>();

        return database.allItems
            .Where(item => item != null && item.appearanceContexts != null && item.appearanceContexts.Any(predicate))
            .Distinct()
            .OrderBy(item => item.category)
            .ThenBy(item => item.itemId)
            .ToList();
    }

    private static void DrawItemList(List<ItemDefinition> items, bool active)
    {
        if (items.Count == 0)
        {
            EditorGUILayout.LabelField("  (없음)", EditorStyles.miniLabel);
            return;
        }

        using (new EditorGUI.DisabledScope(!active))
        {
            foreach (var item in items)
            {
                EditorGUILayout.BeginHorizontal();
                // 아이템 에셋 칸입니다. 클릭하면 Project 창에서 그 에셋 위치를 보여줍니다. 다른 에셋을 끌어다 놓아도
                // 아무 곳에도 저장되지 않으므로 바로 원래대로 돌아옵니다(읽기 전용).
                EditorGUILayout.ObjectField(item, typeof(ItemDefinition), false, GUILayout.Width(170f));
                EditorGUILayout.LabelField($"#{item.itemId}  {item.displayName}  ·  {CategoryLabel(item.category)}  ·  {item.rarity}",
                                           EditorStyles.miniLabel);
                EditorGUILayout.EndHorizontal();
            }
        }
    }

    private static string CategoryLabel(ItemDefinition.ItemCategory category)
    {
        switch (category)
        {
            case ItemDefinition.ItemCategory.Passive: return "패시브";
            case ItemDefinition.ItemCategory.Active: return "액티브";
            case ItemDefinition.ItemCategory.Trinket: return "장신구";
            case ItemDefinition.ItemCategory.Disposable: return "일회용";
            case ItemDefinition.ItemCategory.Food: return "음식";
            default: return category.ToString();
        }
    }
}
