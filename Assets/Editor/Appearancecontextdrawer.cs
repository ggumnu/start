#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// AppearanceContext의 인스펙터 표시를 담당하는 에디터 전용 드로어입니다.
// 반드시 폴더 이름이 "Editor"인 곳에 둬야 합니다 (예: Assets/Editor/AppearanceContextDrawer.cs) -
// 그래야 Unity가 빌드에서 자동으로 제외합니다. #if UNITY_EDITOR로도 한 번 더 감싸서,
// 혹시 다른 폴더에 놓이더라도 빌드 에러가 나지 않도록 안전장치를 뒀습니다.
//
// [variantId 드롭다운의 원리]
// variantId는 RoomCategory처럼 고정된 enum이 아니라, 레벨마다 따로 만드는
// RoomGenerationConfig/ChoiceLevelConfig 에셋 안의 RoomVariant 리스트(startVariants,
// storeVariants, westVariants 등)에 자유 문자열로 흩어져 있습니다. 그래서 이 드로어는
// 그릴 때마다 프로젝트에 있는 모든 RoomGenerationConfig/ChoiceLevelConfig 에셋을
// 스캔해서, 지금 선택된 category와 일치하는 RoomVariant.variantId들만 모아 드롭다운을
// 만듭니다 - 새 특수방(카페/편의점 등)을 레벨 설정에 추가하면 이 드로어를 전혀 손대지
// 않아도 다음에 인스펙터를 열 때 자동으로 드롭다운에 나타납니다.
//
// 목록 맨 위 "(카테고리 전체)" 항목을 고르면 variantId가 빈 문자열로 저장되어, 그
// category 전체에서 등장하는 것으로 처리됩니다(AppearanceContext.ToContextKey() 참고).
//
// [스캔 결과 캐시 - 인스펙터 렉 수정 (형빈 확정)]
// 예전에는 OnGUI가 불릴 때마다 프로젝트 전체 검색(AssetDatabase.FindAssets x3 + 에셋 로드)을 했습니다. 인스펙터는
// 클릭/글자 입력/마우스 이동마다 여러 번 다시 그려지므로, 등장 위치 줄이 많은 아이템(특히 기존 에셋을 복제해서 만든
// 아이템 - 등장 위치가 미리 채워져 있음)은 설명 칸에 글자를 칠 때마다 수십 번 검색해 크게 버벅였습니다(메뉴로 갓 만든
// 에셋은 등장 위치가 비어 있어 이 검색이 돌지 않아 빨랐던 것). 에셋 파일 자체에는 문제가 없었습니다.
// 이제는 category별 스캔 결과를 기억해 두고 재사용합니다. 대신 "방금 추가한 variant가 안 보이는" 일이 없도록
// - 프로젝트의 에셋이 바뀌면(저장/추가/삭제 - EditorApplication.projectChanged) 기억을 지우고,
// - 마지막 스캔 후 CacheLifetimeSeconds(1초)가 지나면 다시 스캔합니다(저장 전 인스펙터 수정도 1초 안에 반영).
// 그래서 검색은 많아야 category마다 1초에 한 번이고, 드롭다운은 예전처럼 항상 최신 목록을 보여줍니다.
[CustomPropertyDrawer(typeof(AppearanceContext))]
public class AppearanceContextDrawer : PropertyDrawer
{
    private const string WholeCategoryLabel = "(카테고리 전체)";

    // 스캔 결과를 다시 쓰는 최대 시간(초)입니다. 이 시간이 지나면 다음에 그릴 때 다시 스캔합니다.
    private const double CacheLifetimeSeconds = 1.0;

    private struct CacheEntry
    {
        public List<string> variantIds;
        public double scannedAt; // EditorApplication.timeSinceStartup 기준
    }

    // category -> 마지막 스캔 결과입니다. 스크립트가 다시 컴파일되면(도메인 리로드) 자동으로 비워집니다.
    private static readonly Dictionary<RoomCategory, CacheEntry> variantCache = new Dictionary<RoomCategory, CacheEntry>();

    // 프로젝트의 에셋이 바뀌면(레벨 설정/레이아웃 에셋 저장, 추가, 삭제 등) 기억해 둔 결과를 버립니다.
    [InitializeOnLoadMethod]
    private static void RegisterCacheInvalidation()
    {
        EditorApplication.projectChanged -= ClearVariantCache;
        EditorApplication.projectChanged += ClearVariantCache;
    }

    private static void ClearVariantCache()
    {
        variantCache.Clear();
    }

    // 기억해 둔 결과가 있고 아직 CacheLifetimeSeconds가 안 지났으면 그대로, 아니면 새로 스캔해서 돌려줍니다.
    // 돌려준 목록은 읽기 전용으로만 써야 합니다(여러 줄이 같은 목록을 함께 씁니다).
    private static List<string> GetVariantIds(RoomCategory category)
    {
        double now = EditorApplication.timeSinceStartup;
        if (variantCache.TryGetValue(category, out var entry) && entry.variantIds != null &&
            now - entry.scannedAt <= CacheLifetimeSeconds)
        {
            return entry.variantIds;
        }

        var ids = CollectVariantIds(category);
        variantCache[category] = new CacheEntry { variantIds = ids, scannedAt = now };
        return ids;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        var categoryProp = property.FindPropertyRelative("category");
        var variantIdProp = property.FindPropertyRelative("variantId");

        float spacing = 6f;
        float half = (position.width - spacing) / 2f;
        Rect categoryRect = new Rect(position.x, position.y, half, position.height);
        Rect variantRect = new Rect(position.x + half + spacing, position.y, half, position.height);

        EditorGUI.PropertyField(categoryRect, categoryProp, GUIContent.none);

        // enumValueIndex(목록에서 몇 번째인지)가 아니라 intValue(실제 enum 번호)로 읽습니다 - RoomCategory는
        // 5/6번이 비어 있어서(특수방 통합) 순서와 번호가 다릅니다(일반방 = 6번째 항목이지만 번호는 7).
        var category = (RoomCategory)categoryProp.intValue;
        var variantIds = GetVariantIds(category); // 매번 스캔하지 않고 기억해 둔 결과를 씁니다(파일 상단 [스캔 결과 캐시] 참고).

        var displayOptions = new List<string> { WholeCategoryLabel };
        displayOptions.AddRange(variantIds);

        int currentIndex = string.IsNullOrEmpty(variantIdProp.stringValue)
            ? 0
            : displayOptions.IndexOf(variantIdProp.stringValue);

        // 저장된 variantId가 지금 스캔 결과에 없는 경우(예: 그 사이 레벨 설정에서 삭제됨,
        // 또는 아직 씬/에셋이 임포트되지 않음)에는 실제 값을 지우지 않고 목록 맨 뒤에
        // 그대로 보여줘서 데이터 손실 없이 원인을 파악할 수 있게 합니다.
        if (currentIndex < 0)
        {
            displayOptions.Add($"{variantIdProp.stringValue} (현재 스캔 결과에 없음)");
            currentIndex = displayOptions.Count - 1;
        }

        int newIndex = EditorGUI.Popup(variantRect, currentIndex, displayOptions.ToArray());
        if (newIndex != currentIndex)
        {
            if (newIndex == 0)
            {
                variantIdProp.stringValue = string.Empty;
            }
            else if (newIndex <= variantIds.Count)
            {
                variantIdProp.stringValue = variantIds[newIndex - 1];
            }
            // newIndex가 그 이상(= "현재 스캔 결과에 없음" 항목)이면 값을 건드리지 않습니다.
        }

        EditorGUI.EndProperty();
    }

    // category와 일치하는 variantId들을 프로젝트 전체 RoomGenerationConfig/ChoiceLevelConfig
    // 에셋에서 모아 정렬된 목록으로 반환합니다. 실제 스캔은 GetVariantIds가 필요할 때만(에셋 변경 후,
    // 또는 1초가 지났을 때) 이 함수를 부릅니다.
    private static List<string> CollectVariantIds(RoomCategory category)
    {
        var results = new HashSet<string>();

        foreach (var guid in AssetDatabase.FindAssets($"t:{nameof(RoomGenerationConfig)}"))
        {
            var config = AssetDatabase.LoadAssetAtPath<RoomGenerationConfig>(AssetDatabase.GUIDToAssetPath(guid));
            if (config == null) continue;

            CollectFromList(config.startVariants, category, results);
            CollectFromList(config.bossVariants, category, results);
            CollectFromList(config.clubVariants, category, results);
            CollectFromList(config.storeVariants, category, results);
        }

        // 특수방은 RoomVariant 목록이 아니라 레이아웃 에셋(SpecialRoomLayout)의 레이아웃 ID가 variantId가 됩니다.
        // 레벨 설정에 아직 등록하지 않은 레이아웃도 고를 수 있도록, 프로젝트의 레이아웃 에셋을 전부 스캔합니다.
        if (category == RoomCategory.Special)
        {
            foreach (var guid in AssetDatabase.FindAssets($"t:{nameof(SpecialRoomLayout)}"))
            {
                var layout = AssetDatabase.LoadAssetAtPath<SpecialRoomLayout>(AssetDatabase.GUIDToAssetPath(guid));
                if (layout != null && !string.IsNullOrEmpty(layout.layoutId))
                {
                    results.Add(layout.layoutId);
                }
            }
        }

        foreach (var guid in AssetDatabase.FindAssets($"t:{nameof(ChoiceLevelConfig)}"))
        {
            var config = AssetDatabase.LoadAssetAtPath<ChoiceLevelConfig>(AssetDatabase.GUIDToAssetPath(guid));
            if (config == null) continue;

            CollectFromList(config.westVariants, category, results);
            CollectFromList(config.eastVariants, category, results);
        }

        var sorted = results.ToList();
        sorted.Sort();
        return sorted;
    }

    private static void CollectFromList(List<RoomVariant> variants, RoomCategory category, HashSet<string> results)
    {
        if (variants == null) return;

        foreach (var variant in variants)
        {
            if (variant != null && variant.category == category && !string.IsNullOrEmpty(variant.variantId))
            {
                results.Add(variant.variantId);
            }
        }
    }
}
#endif