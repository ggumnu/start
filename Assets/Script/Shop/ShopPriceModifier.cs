using UnityEngine;

// [상점 가격 보정 - 정적 등록소]
// 67번 패시브("음식 25% 할인") 같은 "특정 분류 아이템의 상점 가격을 바꾸는" 효과를 위한 공용 등록소입니다.
// 앞으로 비슷한 아이템(예: 장신구 10% 할인, 패시브 가격 +5)도 Register 한 줄로 붙일 수 있습니다.
//
// - 계산: (진열될 때 정해진 원래 가격 + 더하기 합계) x 곱하기 전부 곱한 값 -> 소수점 버림 -> 최소 1.
//   예) 원가 20, 25% 할인(x0.75) -> 15 / 원가 15 -> 11.25 -> 11 / 원가 1 -> 0.75 -> 0 -> 최소 1
// - 플레이어 아이템 칸에만 적용됩니다. 소모성 재화 칸(체력/코스트/열쇠 등)은 보정하지 않습니다.
// - 원래 가격은 진열 칸(ShopListing.unitPrice)에 그대로 남고, 보정된 가격은 볼 때마다 새로 계산합니다(실시간). 그래서 상점에
//   있는 동안 67번을 얻거나 잃으면 가격 표시가 바로 바뀝니다.
// - [86번] 위 계산과 별개로 "레벨 한정 무료"(RegisterLevelFree)가 있습니다 - 아래 [86번] 주석 참고.
// - 플레이 시작 때마다 비워서, 에디터에서 Domain Reload를 꺼 둔 경우에도 이전 플레이의 등록이 남지 않습니다.
public static class ShopPriceModifier
{
    private static readonly CategoryModifierSet modifiers = new CategoryModifierSet();

    // additive = 더할 가격(없으면 0), multiplier = 곱할 배율(없으면 1 - 25% 할인이면 0.75). 같은 sourceId로 다시 부르면 덮어씁니다.
    public static void Register(string sourceId, ItemDefinition.ItemCategory category, float additive, float multiplier, Object owner)
    {
        modifiers.Register(sourceId, category, additive, multiplier, owner);
    }

    public static void Unregister(string sourceId)
    {
        modifiers.Unregister(sourceId);
    }

    // 보정이 적용된 실제 가격입니다. 원래 가격이 0 이하(무료)면 그대로 둡니다.
    // [86번] 이 분류가 "이번 레벨 무료"로 등록돼 있으면 다른 보정(67번 할인 등)과 상관없이 0입니다(최소 1 규칙도 적용 안 함).
    public static int GetPrice(ItemDefinition item, int basePrice)
    {
        if (item == null || basePrice <= 0) return basePrice;
        if (IsLevelFree(item.category)) return 0;
        if (!modifiers.HasAny(item.category)) return basePrice;
        float value = modifiers.Apply(item.category, basePrice);
        // 0.75 같은 소수 곱셈의 아주 작은 오차(14.9999…)로 1이 덜 나오지 않도록 아주 작은 값을 더한 뒤 버림합니다.
        return Mathf.Max(1, Mathf.FloorToInt(value + 0.0001f));
    }

    // ------------------------------------------------------------------
    // [86번 - 레벨 한정 무료] 특정 분류 아이템의 상점 가격을 "등록한 그 레벨(전투 레벨 n)에서만" 0으로 만듭니다.
    // - 등록 순간의 레벨 번호를 기억하고, 지금이 같은 번호의 전투 레벨일 때만 무료입니다. n.5 선택방 레벨과 다음 레벨부터는 따로
    //   지우지 않아도 자동으로 원래 가격으로 돌아갑니다(형빈 확정 - n.5는 같은 레벨로 보지 않음).
    // - 67번 할인과 같이 있어도 무료가 이깁니다(0). 상점 창은 원래 가격(회색 줄) + 0으로 보여주고, 가격 옆 재화 아이콘 자리에 등록한
    //   아이콘(86번 아이템 아이콘)을 보여줍니다(ShopUI 참고).
    // - GameFlowManager가 없는 씬(아이템 테스트 씬)에서는 레벨을 구분할 수 없어 그 판이 끝날 때까지 무료입니다.
    // - 소유자(보통 플레이어 오브젝트)가 사라지면(새 게임) 등록도 자동으로 정리됩니다.
    // ------------------------------------------------------------------
    private class LevelFreeEntry
    {
        public ItemDefinition.ItemCategory category;
        public int levelNumber;   // -1이면 레벨 구분 없음(GameFlowManager 없는 씬)
        public Object owner;
        public Sprite icon;
    }

    private static readonly System.Collections.Generic.Dictionary<string, LevelFreeEntry> levelFreeEntries =
        new System.Collections.Generic.Dictionary<string, LevelFreeEntry>();
    private static GameFlowManager cachedGameFlow;

    // 지금 레벨(전투 레벨)에서 category 아이템을 무료로 등록합니다. 같은 sourceId로 다시 부르면 덮어씁니다(새 레벨 기준으로 갱신).
    public static void RegisterLevelFree(string sourceId, ItemDefinition.ItemCategory category, Object owner, Sprite icon)
    {
        if (string.IsNullOrEmpty(sourceId)) return;
        GameFlowManager gameFlow = GetGameFlow();
        levelFreeEntries[sourceId] = new LevelFreeEntry
        {
            category = category,
            levelNumber = gameFlow != null ? gameFlow.CurrentLevelNumber : -1,
            owner = owner,
            icon = icon,
        };
    }

    public static void UnregisterLevelFree(string sourceId)
    {
        if (string.IsNullOrEmpty(sourceId)) return;
        levelFreeEntries.Remove(sourceId);
    }

    // 지금 레벨에서 category가 무료인지입니다.
    public static bool IsLevelFree(ItemDefinition.ItemCategory category)
    {
        return TryGetLevelFree(category, out _);
    }

    // 이 아이템이 지금 레벨 무료로 할인 중이면 등록한 아이콘을, 아니면 null을 돌려줍니다(상점 창 가격 옆 아이콘 교체용).
    public static Sprite GetLevelFreeIcon(ItemDefinition item)
    {
        if (item == null) return null;
        return TryGetLevelFree(item.category, out LevelFreeEntry entry) ? entry.icon : null;
    }

    private static bool TryGetLevelFree(ItemDefinition.ItemCategory category, out LevelFreeEntry found)
    {
        found = null;
        if (levelFreeEntries.Count == 0) return false;

        PruneDeadLevelFree();

        GameFlowManager gameFlow = GetGameFlow();
        foreach (var entry in levelFreeEntries.Values)
        {
            if (entry.category != category) continue;

            bool sameLevel;
            if (entry.levelNumber < 0 || gameFlow == null)
            {
                sameLevel = true; // 레벨을 구분할 수 없는 씬
            }
            else
            {
                sameLevel = gameFlow.CurrentLevelNumber == entry.levelNumber &&
                            gameFlow.CurrentPhase == GameFlowManager.LevelPhase.Combat;
            }

            if (sameLevel)
            {
                found = entry;
                return true;
            }
        }
        return false;
    }

    private static void PruneDeadLevelFree()
    {
        System.Collections.Generic.List<string> deadKeys = null;
        foreach (var kvp in levelFreeEntries)
        {
            if (kvp.Value.owner != null) continue;
            if (deadKeys == null) deadKeys = new System.Collections.Generic.List<string>();
            deadKeys.Add(kvp.Key);
        }
        if (deadKeys == null) return;
        foreach (var key in deadKeys) levelFreeEntries.Remove(key);
    }

    private static GameFlowManager GetGameFlow()
    {
        if (cachedGameFlow == null) cachedGameFlow = Object.FindFirstObjectByType<GameFlowManager>();
        return cachedGameFlow;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlayStart()
    {
        modifiers.Clear();
        levelFreeEntries.Clear();
        cachedGameFlow = null;
    }
}
