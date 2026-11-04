using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

// 상점(편의점/카페 등) 하나의 판매 설정입니다. 다른 설정들과 동일하게 ScriptableObject 에셋으로 만들고
// (Project 창 우클릭 > Create > MobArchive > Shop Config), 상점 바닥 프리팹 안의 상인 NPC(ShopNPC)의
// Shop Config 칸에 연결해서 씁니다. 상점마다 다른 구성을 원하면 에셋을 하나 더 만들어 다른 NPC에 연결하면 됩니다.
//
// [진열 구성]
// - 소모성 재화(basicItems): 수류탄/해킹툴/현재 체력/청휘석처럼 항상 같은 칸에 진열되는 상품입니다. 재고는 무제한
//   (형빈 확정)이고, 구매 팝업에서 수량을 고를 수 있습니다. 한 번 살 때 받는 양은 amountPerPurchase(예: x5 묶음).
// - 플레이어 아이템(최소~최대 개수 사이에서 무작위, 기본 1~3개): itemDatabase에서 playerItemContexts에 맞는 아이템(패시브/액티브/장신구/
//   일회용/음식 전부) 중, 이미 가진 패시브와 지금 장착 중인 슬롯 아이템을 빼고 가중치(weight)로 뽑습니다.
//   1개만 살 수 있고, 사면 그 칸에 어두운 덮개가 씌워집니다. 가격은 등급(rarity)별 가격 범위(최소~최대) 안에서 진열될 때마다
//   무작위로 정해집니다(가중치 없음 - 형빈 확정).
// - 진열 칸은 최대 8칸(한 줄 4칸 x 2줄)입니다. 소모성 재화가 먼저, 플레이어 아이템이 그 뒤에 놓입니다.
//
// [보물상자 시스템과 분리]
// 상점 아이템 추첨은 보물상자 쪽 추첨(ItemPedestal/NormalRoomTreasureSpawner)과 코드를 공유하지 않습니다 - 상자 내용물을
// 바꾸는 아이템(48번 가중치 섞기, 27번 최상급 보장 등)이 상점 진열에 끼어들지 않도록 일부러 따로 둡니다.
[CreateAssetMenu(fileName = "ShopConfig", menuName = "MobArchive/Shop Config")]
public class ShopConfig : ScriptableObject
{
    // 상점 창의 진열 칸 수(한 줄 4칸 x 2줄). 스크롤은 아직 계획에 없어서 이 이상은 진열하지 않습니다.
    public const int MaxSlots = 8;

    [Header("소모성 재화 (항상 진열, 재고 무제한)")]
    [Tooltip("위에서부터 순서대로 진열됩니다. 표시 이름을 비워두면 기본 이름(수류탄/해킹툴/현재 체력/청휘석 등)을 씁니다.")]
    public List<ShopBasicItemEntry> basicItems = new List<ShopBasicItemEntry>
    {
        new ShopBasicItemEntry { type = BasicItemType.Grenade, currency = ShopCurrency.Credit, price = 5, amountPerPurchase = 1 },
        new ShopBasicItemEntry { type = BasicItemType.HackingTool, currency = ShopCurrency.Credit, price = 5, amountPerPurchase = 1 },
        new ShopBasicItemEntry { type = BasicItemType.Health, currency = ShopCurrency.Credit, price = 3, amountPerPurchase = 1 },
        new ShopBasicItemEntry { type = BasicItemType.Pyroxenes, currency = ShopCurrency.Credit, price = 10, amountPerPurchase = 1 },
    };

    [Header("플레이어 아이템")]
    [Tooltip("플레이어 아이템을 뽑아올 아이템 데이터베이스입니다(LevelBuilder에 연결된 것과 같은 에셋).")]
    public ItemDatabase itemDatabase;
    [Tooltip("이 상점에 진열될 수 있는 아이템의 등장 위치입니다. 아이템 에셋의 '등장 위치'(appearanceContexts)와 같은 규칙 - " +
             "variantId를 비워두면 그 카테고리 전체(예: Store = 모든 편의점/카페), 채우면 그 방에 등장하도록 설정된 아이템만 진열됩니다. " +
             "여러 개를 넣으면 그중 하나라도 맞는 아이템이 후보가 됩니다.")]
    public List<AppearanceContext> playerItemContexts = new List<AppearanceContext>
    {
        new AppearanceContext { category = RoomCategory.Store, variantId = "" },
    };
    [Tooltip("진열할 플레이어 아이템 개수의 최소값입니다. 상점을 처음 열 때와 갱신할 때마다 최소~최대 사이에서 무작위로(가중치 없이) " +
             "정해집니다. 후보가 부족하면 있는 만큼만 진열되고, 소모성 재화 칸을 뺀 남은 칸 수를 넘지 않습니다.")]
    [Min(0)] public int minPlayerItemCount = 1;
    [Tooltip("진열할 플레이어 아이템 개수의 최대값입니다(최소값보다 작으면 최소값으로 고정).")]
    [Min(0)] public int maxPlayerItemCount = 3;

    [Tooltip("등급별 플레이어 아이템 가격 범위입니다(Tier0~Tier3 순서). 진열될 때마다 최소~최대 사이에서 무작위로(가중치 없이) 정해집니다.")]
    public ShopRarityPrice[] rarityPrices = new ShopRarityPrice[]
    {
        new ShopRarityPrice { rarity = ItemDefinition.ItemRarity.Tier0, currency = ShopCurrency.Credit, minPrice = 5, maxPrice = 15 },
        new ShopRarityPrice { rarity = ItemDefinition.ItemRarity.Tier1, currency = ShopCurrency.Credit, minPrice = 15, maxPrice = 30 },
        new ShopRarityPrice { rarity = ItemDefinition.ItemRarity.Tier2, currency = ShopCurrency.Credit, minPrice = 25, maxPrice = 40 },
        new ShopRarityPrice { rarity = ItemDefinition.ItemRarity.Tier3, currency = ShopCurrency.Credit, minPrice = 30, maxPrice = 50 },
    };

    [Header("갱신하기 (플레이어 아이템만 다시 뽑음)")]
    public ShopCurrency refreshCurrency = ShopCurrency.Credit;
    [Min(0)] public int refreshPrice = 1;
    [Tooltip("이 상점에서 갱신할 수 있는 최대 횟수입니다. 다 쓰면 갱신하기 버튼이 어둡게 비활성화됩니다.")]
    [Min(0)] public int maxRefreshCount = 2;

    // 이 상점에 진열될 수 있는 플레이어 아이템 후보 전체(분류 무관, 중복 제거)입니다. 보유 여부 필터링은 ShopNPC가 합니다.
    public List<ItemDefinition> GetPlayerItemPool()
    {
        var result = new List<ItemDefinition>();
        if (itemDatabase == null || itemDatabase.allItems == null || playerItemContexts == null) return result;

        foreach (var item in itemDatabase.allItems)
        {
            if (item == null || item.appearanceContexts == null || result.Contains(item)) continue;
            if (MatchesAnyContext(item)) result.Add(item);
        }
        return result;
    }

    private bool MatchesAnyContext(ItemDefinition item)
    {
        foreach (var shopCtx in playerItemContexts)
        {
            if (shopCtx == null) continue;
            string key = shopCtx.ToContextKey();
            foreach (var itemCtx in item.appearanceContexts)
            {
                if (itemCtx != null && string.Equals(itemCtx.ToContextKey(), key, System.StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        return false;
    }

    // 등급에 맞는 가격표 줄(없으면 null)
    // 이번에 진열할 플레이어 아이템 개수(최소~최대 사이 무작위, 가중치 없음)
    public int RollPlayerItemCount()
    {
        int min = Mathf.Max(0, minPlayerItemCount);
        int max = Mathf.Max(min, maxPlayerItemCount);
        return Random.Range(min, max + 1);
    }

    public ShopRarityPrice GetRarityPrice(ItemDefinition.ItemRarity rarity)
    {
        if (rarityPrices == null) return null;
        foreach (var entry in rarityPrices)
        {
            if (entry != null && entry.rarity == rarity) return entry;
        }
        return null;
    }

    // 소모성 재화의 기본 표시 이름입니다(ShopBasicItemEntry.displayName을 비워뒀을 때 사용).
    public static string GetDefaultBasicName(BasicItemType type)
    {
        switch (type)
        {
            case BasicItemType.Health: return "현재 체력";
            case BasicItemType.Grenade: return "수류탄";
            case BasicItemType.HackingTool: return "해킹툴";
            case BasicItemType.Credit: return "크레딧";
            case BasicItemType.Pyroxenes: return "청휘석";
            case BasicItemType.Cost: return "코스트";
            default: return type.ToString();
        }
    }

    public static string GetCurrencyName(ShopCurrency currency)
    {
        return currency == ShopCurrency.Pyroxenes ? "청휘석" : "크레딧";
    }

    // 재화 종류를 PlayerResources가 쓰는 자원 종류로 바꿉니다(크레딧/청휘석 모두 PlayerResources가 보유량을 관리).
    public static BasicItemType ToResourceType(ShopCurrency currency)
    {
        return currency == ShopCurrency.Pyroxenes ? BasicItemType.Pyroxenes : BasicItemType.Credit;
    }
}

// 상점에서 값을 치르는 재화 종류입니다.
public enum ShopCurrency
{
    Credit,     // 크레딧
    Pyroxenes,  // 청휘석
}

// 소모성 재화 진열 칸 하나의 설정입니다.
[System.Serializable]
public class ShopBasicItemEntry
{
    public BasicItemType type = BasicItemType.Grenade;
    [Tooltip("비워두면 기본 이름(수류탄/해킹툴/현재 체력/청휘석 등)을 씁니다.")]
    public string displayName;
    [Tooltip("카드에 표시할 아이콘입니다(비워두면 상점 창에서 빈 아이콘으로 표시).")]
    public Sprite icon;
    public ShopCurrency currency = ShopCurrency.Credit;
    [Tooltip("1회 구매 가격입니다. 구매 팝업에서 수량을 늘리면 이 값 x 수량이 됩니다.")]
    [Min(1)] public int price = 5;
    [Tooltip("1회 구매 시 받는 양입니다(예: 5면 x5 묶음). 현재 체력은 회복량입니다.")]
    [Min(1)] public int amountPerPurchase = 1;
}

// 등급별 플레이어 아이템 가격 범위 한 줄입니다.
[System.Serializable]
public class ShopRarityPrice
{
    public ItemDefinition.ItemRarity rarity;
    public ShopCurrency currency = ShopCurrency.Credit;
    [Tooltip("가격 범위의 최소값입니다(이전의 고정 가격 값이 여기로 옮겨짐).")]
    [FormerlySerializedAs("price")]
    [Min(0)] public int minPrice = 30;
    [Tooltip("가격 범위의 최대값입니다. 최소값보다 작으면(0 포함) 최소값으로 고정됩니다.")]
    [Min(0)] public int maxPrice = 0;

    // 최소~최대 사이 무작위 가격(가중치 없음, 양 끝 포함)
    public int RollPrice()
    {
        int min = Mathf.Max(0, minPrice);
        int max = Mathf.Max(min, maxPrice);
        return Random.Range(min, max + 1);
    }
}

// 상점 진열 칸 하나의 실제 내용(런타임 전용 - 인스펙터에 노출되지 않음)입니다. ShopNPC가 상점을 처음 열 때 만들고,
// 상점 창(ShopUI)은 이 목록을 읽어서 카드를 그립니다.
public class ShopListing
{
    public bool isBasic;                 // true = 소모성 재화, false = 플레이어 아이템
    public BasicItemType basicType;      // isBasic일 때만
    public ItemDefinition item;          // 플레이어 아이템일 때만
    public string displayName;
    public Sprite icon;
    public ShopCurrency currency;
    public int unitPrice;                // 1회 구매 가격
    public int amountPerPurchase = 1;    // 1회 구매 시 받는 양(플레이어 아이템은 항상 1)
    public bool soldOut;                 // 플레이어 아이템을 샀으면 true - 카드에 어두운 덮개를 씌우고 누를 수 없게 함
}
