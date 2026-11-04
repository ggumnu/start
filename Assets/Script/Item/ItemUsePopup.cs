using UnityEngine;

// [일회용/음식 사용 연출 - 머리 위 아이콘]
// 일회용/음식 아이템이 실제로 "소모되는 순간" 그 아이템의 아이콘을 플레이어 머리 위에 잠깐 띄웁니다. 54번(크레딧)/72번(체력·
// 코스트)과 같은 아이콘 연출(CreditPopupDisplay - 아이콘 + 발광 + 빛 입자, 플레이어를 따라다님)이고, 발광/입자 색은 그 아이템의
// 등급 색(보물상자 아이템 빛 - ItemGlowSpawner의 Tier 색)입니다.
//
// [언제 뜨나 - 형빈 확정]
// - 즉시형/지연형 구분 없이 실제로 소모되는 순간에 뜹니다(즉시형은 Q를 누른 순간, 지연형은 효과가 발동해 소멸하는 순간 - 80/86번처럼
//   그 자리에서 끝나는 것은 바로, 56번은 창에서 고른 순간 등). 사용이 막혀 소모되지 않으면 뜨지 않습니다.
// - 85번(직전 일회용 재현)/93번(직전 음식 재현)으로 재현할 때도 재현한 아이템의 아이콘이 뜹니다(즉시형은 바로, 지연형은 발동하는 순간).
// - 부르는 곳: PlayerDisposableItem.ConsumeFrontItem, PlayerFoodItem.ConsumeEquippedItem, Item85effect/Item93effect(재현). 앞으로 만드는
//   일회용/음식은 따로 넣지 않아도 자동으로 적용됩니다.
//
// [설정]
// Resources 폴더에 ItemUsePopupSettings 에셋(Create > MobArchive > Item Use Popup Settings)이 있으면 그 값을 쓰고, 없으면 54번과 같은
// 기본값을 씁니다. 에셋의 발광/입자 "색"은 무시되고 등급 색으로 칠해지며, 알파(진하기)만 Glow Alpha/Sparkle Alpha 값을 씁니다.
// 연출은 순수 장식이라 여기서 문제가 생겨도 아이템 사용/소모 자체에는 영향을 주지 않도록 전부 try/catch로 감쌉니다.
public static class ItemUsePopup
{
    private static ItemUsePopupSettings cachedSettings;
    private static bool settingsLoaded;
    private static ItemGlowSpawner cachedGlowSpawner;

    // 등급 색을 찾지 못할 때 쓰는 기본값입니다(ItemGlowSpawner의 기본 Tier 색과 같음).
    private static readonly Color DefaultTier0 = new Color(1f, 1f, 1f, 1f);
    private static readonly Color DefaultTier1 = new Color(0f, 1f, 1f, 1f);
    private static readonly Color DefaultTier2 = new Color(1f, 0.92156863f, 0.015686275f, 1f);
    private static readonly Color DefaultTier3 = new Color(1f, 0f, 1f, 1f);

    // player 머리 위에 item 아이콘 연출을 띄웁니다.
    public static void Show(GameObject player, ItemDefinition item)
    {
        try
        {
            if (player == null || item == null || item.icon == null) return;

            ItemUsePopupSettings asset = GetSettingsAsset();
            CreditPopupSettings template = asset != null ? asset.popup : null;

            // 원본 설정을 건드리지 않도록 복사본에 등급 색을 칠합니다.
            var settings = new CreditPopupSettings();
            if (template != null) JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(template), settings);
            settings.icon = null; // 아이콘은 항상 그 아이템의 아이콘

            Color tier = GetTierColor(item.rarity);
            float glowAlpha = asset != null ? asset.glowAlpha : 0.85f;
            float sparkleAlpha = asset != null ? asset.sparkleAlpha : 1f;
            settings.glowColor = new Color(tier.r, tier.g, tier.b, glowAlpha);
            settings.sparkleColor = new Color(tier.r, tier.g, tier.b, sparkleAlpha);

            CreditPopupDisplay.Show(player, player.transform.position, settings, item.icon, followTarget: true);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"ItemUsePopup: 사용 연출 중 오류가 발생해 건너뜁니다(아이템 사용에는 영향 없음): {e}");
        }
    }

    // 보물상자 아이템 빛(ItemGlowSpawner)의 등급 색입니다. 씬에 없으면 기본값.
    private static Color GetTierColor(ItemDefinition.ItemRarity rarity)
    {
        if (cachedGlowSpawner == null) cachedGlowSpawner = Object.FindFirstObjectByType<ItemGlowSpawner>();
        var g = cachedGlowSpawner;

        switch (rarity)
        {
            case ItemDefinition.ItemRarity.Tier0: return g != null ? g.tier0Color : DefaultTier0;
            case ItemDefinition.ItemRarity.Tier2: return g != null ? g.tier2Color : DefaultTier2;
            case ItemDefinition.ItemRarity.Tier3: return g != null ? g.tier3Color : DefaultTier3;
            default: return g != null ? g.tier1Color : DefaultTier1;
        }
    }

    private static ItemUsePopupSettings GetSettingsAsset()
    {
        if (!settingsLoaded)
        {
            cachedSettings = Resources.Load<ItemUsePopupSettings>("ItemUsePopupSettings");
            settingsLoaded = true;
        }
        return cachedSettings;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlayStart()
    {
        cachedSettings = null;
        settingsLoaded = false;
        cachedGlowSpawner = null;
    }
}
