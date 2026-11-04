using System.Collections.Generic;
using UnityEngine;

// 상점의 상인 NPC입니다. 플레이어가 가까이 오면 F 안내("거래하기")가 뜨고, F를 누르면 상점 창을 엽니다.
//
// [배치 - 형빈 확정]
// 상점(편의점/카페) 바닥 프리팹 안, 상인이 설 자리에 빈 오브젝트를 두고 이 컴포넌트를 붙인 뒤 Shop Config 칸에 상점 설정
// 에셋을 연결합니다(열린 벽 프리팹의 바리케이드처럼 프리팹 안에 미리 넣어두는 방식). 상인 모델(지금은 임시 캡슐)은 그
// 오브젝트의 자식으로 둡니다. F 판정 기준점은 이 오브젝트의 위치(바닥)입니다.
//
// [범위 판정]
// 보물상자(TreasureChest)와 같은 이유로 트리거 대신 매 프레임 거리 계산(바닥 평면 거리, interactRadius)으로 판정합니다 -
// 콜라이더 구성에 의존하지 않으므로, 상인 모델에 어떤 콜라이더가 있든(또는 없든) 상관없습니다.
//
// [F 대상]
// F 대상은 항상 "범위 안에서 가장 가까운 하나"입니다(InteractionTargetSelector) - 상인 근처에 보관 좌대/상자가 있어도 F 한 번에
// 하나만 반응합니다.
//
// [진열 목록]
// 처음 상점을 열 때 한 번만 진열 목록을 만들고(그 시점의 보유 아이템 기준으로 플레이어 아이템을 뽑음), 이후에는 같은 목록을
// 유지합니다 - 방을 나갔다 와도 바뀌지 않습니다(갱신하기로 플레이어 아이템 칸만 다시 뽑을 수 있음).
//
// [구매 - 3단계]
// 구매 판정(CheckPurchase/GetMaxQuantity/CanAfford)과 실제 거래(TryPurchase)는 여기서 합니다. 소모성 재화는 PlayerResources.Add
// (현재 체력은 PlayerHealth.Heal), 패시브는 PlayerInventory.TryAcquireItem + 획득 연출(ItemPickupEvents)로 받습니다. 슬롯 아이템
// (액티브/장신구/일회용/음식)은 그 분류를 장착 중이 아니면 바로 장착되고, 장착 중이면 원래 아이템이 이 상점의 빈 보관 좌대
// (HoldingPedestal)에 올라갑니다 - 빈 좌대가 없으면 구매할 수 없습니다(4단계).
//
// [보관 좌대 찾기]
// Holding Pedestals 리스트에 넣은 좌대를 순서대로 씁니다. 비워두면 같은 방(RoomController 아래, 없으면 같은 부모 아래)의
// 보관 좌대를 자동으로 찾습니다.
//
// [이 레벨에서 이미 나온 아이템 - 보물상자와 같은 규칙]
// 진열할 플레이어 아이템을 뽑을 때 이 레벨의 다른 보물상자/상점에서 이미 뽑힌 아이템은 빼고(GameFlowManager.
// FilterOutAlreadyAssignedThisLevel), 진열한 아이템은 바로 기록합니다(MarkItemAssignedThisLevel) - 상점과 상자에 같은 아이템이
// 동시에 나오지 않습니다.
//
// [갱신하기 - 5단계]
// Shop Config의 갱신 비용(기본 1 크레딧)을 내고 플레이어 아이템 칸만 전부 다시 뽑습니다(팔린 칸도 다시 채워짐, 소모성 재화 칸은
// 그대로). 이 상점에 한 번이라도 진열됐던 아이템은 이후 갱신에서 다시 나오지 않습니다(형빈 확정 - 갱신했는데 같은 아이템이
// 걸리는 불합리 방지). 사지 않은 채 갱신으로 사라진 아이템은 "이 레벨에서 나온 아이템" 기록에서 지워서 보물상자/48번/56번에서는
// 다시 나올 수 있게 합니다(형빈 확정). 이 상점의 진열 이력은 상점(상인)마다 따로이고, 레벨이 바뀌어 상점이 새로 만들어지면
// 처음부터 다시 시작합니다(1레벨 상점에서 넘긴 아이템도 2레벨 상점에는 나올 수 있음). 최대 횟수(기본 2회)를 다 쓰면 상점 창의 [갱신하기] 버튼이 어둡게 비활성화됩니다.
// 새로 진열할 후보가 하나도 없으면 재화를 쓰지 않도록 갱신을 막습니다.
//
// [상점 창 연결]
// F를 누르면 상점 창(ShopUI.Show)을 엽니다. 상점 창은 처음 열 때 스스로 만들어지므로 씬에 따로 놓을 필요가 없습니다.
// 창이 열리지 못한 경우(이미 열려 있음/플레이어 사망)에는 아무것도 하지 않습니다.
public class ShopNPC : MonoBehaviour
{
    [Header("상점 설정")]
    [Tooltip("이 상인이 파는 상품 구성입니다(Create > MobArchive > Shop Config로 만든 에셋).")]
    public ShopConfig shopConfig;

    [Header("보관 좌대 (슬롯 아이템 교체 시 원래 아이템을 올려둘 자리)")]
    [Tooltip("위에서부터 순서대로 빈 좌대를 씁니다. 비워두면 같은 방 안의 보관 좌대(HoldingPedestal)를 자동으로 찾습니다.")]
    public List<HoldingPedestal> holdingPedestals = new List<HoldingPedestal>();

    [Header("상호작용")]
    public KeyCode interactKey = KeyCode.F;
    [Tooltip("이 거리(바닥 평면 기준) 안에 플레이어가 들어오면 F 안내가 뜨고 F로 상점을 열 수 있습니다.")]
    [Min(0.1f)] public float interactRadius = 1.5f;

    private bool playerInRange = false;
    private Transform playerTransform;
    private PlayerHealth playerHealth;

    private readonly List<ShopListing> listings = new List<ShopListing>();
    private bool stocked = false;
    private int refreshesUsed = 0;
    // 이 상점에 한 번이라도 진열됐던 플레이어 아이템(갱신 때 다시 뽑지 않음 - 위 [갱신하기] 참고)
    private readonly HashSet<ItemDefinition> everDisplayed = new HashSet<ItemDefinition>();

    // F 안내/F 대상 판정이 읽는 값입니다(읽기 전용).
    public bool PlayerInRange => playerInRange;

    // 지금 진열 목록(소모성 재화 → 플레이어 아이템 순서, 최대 8칸). 상점 창이 읽습니다.
    public IReadOnlyList<ShopListing> Listings => listings;

    // 남은 갱신 횟수(0이 되면 상점 창의 [갱신하기] 버튼이 어둡게 비활성화됨)
    public int RefreshesUsed => refreshesUsed;
    public int RemainingRefreshes => shopConfig != null ? Mathf.Max(0, shopConfig.maxRefreshCount - refreshesUsed) : 0;

    void Update()
    {
        UpdatePlayerInRange();

        if (!playerInRange) return;
        if (SlotReplaceConfirmUI.BlocksInteract) return; // 슬롯 교체 확인창이 떠 있거나 방금 닫힌 프레임
        if (ShopUI.BlocksInteract) return;               // 상점 창이 떠 있거나 방금 닫힌 프레임
        if (!InteractionTargetSelector.IsCurrentTarget(this)) return;
        if (Input.GetKeyDown(interactKey))
        {
            RequestOpen();
        }
    }

    private void UpdatePlayerInRange()
    {
        if (playerTransform == null) FindPlayerRoot();

        // 플레이어가 없거나 사망했으면(게임 오버 중) 범위 밖으로 취급합니다 - 게임 오버 화면에서 F로 상점이 열리지 않도록.
        if (playerTransform == null || (playerHealth != null && playerHealth.IsDead))
        {
            playerInRange = false;
            return;
        }

        Vector3 a = transform.position;
        Vector3 b = playerTransform.position;
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        playerInRange = dx * dx + dz * dz <= interactRadius * interactRadius;
    }

    private void RequestOpen()
    {
        if (shopConfig == null)
        {
            Debug.LogWarning($"ShopNPC({name}): Shop Config 칸이 비어 있어 상점을 열 수 없습니다.");
            return;
        }

        EnsureStocked();
        ShopUI.Show(this);
    }

    // 처음 열 때 한 번만 진열 목록을 만듭니다.
    public void EnsureStocked()
    {
        if (stocked || shopConfig == null) return;
        stocked = true;

        listings.Clear();

        if (shopConfig.basicItems != null)
        {
            foreach (var entry in shopConfig.basicItems)
            {
                if (entry == null) continue;
                if (listings.Count >= ShopConfig.MaxSlots) break;
                listings.Add(new ShopListing
                {
                    isBasic = true,
                    basicType = entry.type,
                    displayName = string.IsNullOrEmpty(entry.displayName) ? ShopConfig.GetDefaultBasicName(entry.type) : entry.displayName,
                    icon = entry.icon,
                    currency = entry.currency,
                    unitPrice = Mathf.Max(1, entry.price),
                    amountPerPurchase = Mathf.Max(1, entry.amountPerPurchase),
                });
            }
        }

        foreach (var item in PickPlayerItems(RollWantedPlayerItemCount(), null))
        {
            listings.Add(CreateItemListing(item));
        }
    }

    // 이번에 진열할 플레이어 아이템 개수: Shop Config의 최소~최대 사이 무작위(가중치 없음)를 남은 칸 수로 자릅니다.
    // 처음 열 때와 갱신할 때마다 새로 정합니다(처음 2개였어도 갱신하면 1~3개 중 아무거나).
    private int RollWantedPlayerItemCount()
    {
        int freeSlots = ShopConfig.MaxSlots - listings.Count;
        int rolled = shopConfig.RollPlayerItemCount();
        if (rolled > freeSlots)
        {
            Debug.LogWarning($"ShopNPC({name}): 진열 칸(최대 {ShopConfig.MaxSlots}칸)이 부족해 플레이어 아이템을 {freeSlots}개만 진열합니다.");
        }
        return Mathf.Clamp(rolled, 0, Mathf.Max(0, freeSlots));
    }

    // 플레이어 아이템 후보에서 count개를 가중치로 뽑습니다(중복 없음). 이미 가진 패시브, 지금 장착 중인 슬롯 아이템,
    // exclude에 든 아이템은 제외합니다. 후보가 부족하면 있는 만큼만 돌려줍니다.
    private List<ItemDefinition> PickPlayerItems(int count, ICollection<ItemDefinition> exclude)
    {
        var picked = new List<ItemDefinition>();
        if (count <= 0 || shopConfig == null) return picked;

        var candidates = BuildPlayerItemCandidates(exclude, out GameFlowManager gameFlow);

        if (candidates.Count == 0)
        {
            Debug.LogWarning($"ShopNPC({name}): 진열할 수 있는 플레이어 아이템 후보가 없습니다. Shop Config의 아이템 데이터베이스/" +
                             "등장 위치와 아이템 에셋의 '등장 위치'(appearanceContexts)를 확인해 주세요.");
            return picked;
        }

        while (picked.Count < count && candidates.Count > 0)
        {
            // 67번 등의 등장 가중치 보정이 적용된 값으로 뽑습니다(ItemWeightModifier).
            float total = 0f;
            foreach (var c in candidates) total += ItemWeightModifier.GetWeight(c);

            float roll = UnityEngine.Random.value * total;
            int index = candidates.Count - 1;
            for (int i = 0; i < candidates.Count; i++)
            {
                roll -= ItemWeightModifier.GetWeight(candidates[i]);
                if (roll <= 0f)
                {
                    index = i;
                    break;
                }
            }

            picked.Add(candidates[index]);
            everDisplayed.Add(candidates[index]);
            if (gameFlow != null) gameFlow.MarkItemAssignedThisLevel(candidates[index].itemId); // 진열 즉시 기록
            ItemDefinition justPicked = candidates[index];
            candidates.RemoveAt(index);
            // [상극 아이템] 같이 진열되는 칸에 방금 뽑은 아이템과 같이 가질 수 없는 아이템이 나오지 않게 뺍니다(예: 69번과 99번).
            candidates.RemoveAll(other => ItemCompatibility.AreIncompatible(other, justPicked));
        }

        if (picked.Count < count)
        {
            Debug.LogWarning($"ShopNPC({name}): 플레이어 아이템 후보가 부족해 {count}개 중 {picked.Count}개만 진열합니다.");
        }
        return picked;
    }

    // 지금 진열할 수 있는 플레이어 아이템 후보 목록입니다(뽑지는 않음 - 갱신 가능 여부 확인에도 씀).
    // 이미 가진 패시브, 장착 중인 슬롯 아이템, 이 상점 보관 좌대의 아이템, exclude, 이 레벨에서 이미 나온 아이템은 뺍니다.
    private List<ItemDefinition> BuildPlayerItemCandidates(ICollection<ItemDefinition> exclude, out GameFlowManager gameFlow)
    {
        gameFlow = null;
        if (shopConfig == null) return new List<ItemDefinition>();

        var candidates = shopConfig.GetPlayerItemPool();
        EnsurePlayerRefs();
        var player = playerTransform != null ? playerTransform.gameObject : null;

        var inventory = player != null ? player.GetComponent<PlayerInventory>() : null;
        if (inventory != null) candidates = inventory.FilterUnowned(candidates);

        var equipped = GetEquippedSlotItems(player);
        var onPedestals = new HashSet<ItemDefinition>();
        foreach (var pedestal in GetHoldingPedestals())
        {
            if (pedestal != null && pedestal.HeldItem != null) onPedestals.Add(pedestal.HeldItem);
        }
        candidates.RemoveAll(item => item == null || item.weight <= 0f || equipped.Contains(item) || onPedestals.Contains(item) ||
                                     (exclude != null && exclude.Contains(item)));

        // 이 레벨의 다른 보물상자/상점에서 이미 뽑힌 아이템은 뺍니다(보물상자와 같은 규칙).
        gameFlow = UnityEngine.Object.FindFirstObjectByType<GameFlowManager>();
        if (gameFlow != null) candidates = gameFlow.FilterOutAlreadyAssignedThisLevel(candidates);
        return candidates;
    }

    // ------------------------------------------------------------------
    // 갱신하기 (5단계) - 플레이어 아이템 칸만 다시 뽑습니다(소모성 재화 칸은 그대로).
    // ------------------------------------------------------------------

    // 갱신할 수 없는 이유입니다(None = 갱신 가능).
    public enum RefreshBlock
    {
        None,
        NoRefreshesLeft,    // 남은 갱신 횟수 없음
        NoCandidates,       // 새로 진열할 아이템 후보가 하나도 없음(재화를 쓰지 않도록 막음)
        MissingComponent,   // PlayerResources를 못 찾음(설정 문제)
        NotEnoughCurrency,  // 재화 부족
    }

    public int RefreshPrice => shopConfig != null ? Mathf.Max(0, shopConfig.refreshPrice) : 0;
    public ShopCurrency RefreshCurrency => shopConfig != null ? shopConfig.refreshCurrency : ShopCurrency.Credit;

    public RefreshBlock CheckRefresh()
    {
        if (shopConfig == null || RemainingRefreshes <= 0) return RefreshBlock.NoRefreshesLeft;
        EnsurePlayerRefs();

        if (BuildPlayerItemCandidates(everDisplayed, out _).Count == 0) return RefreshBlock.NoCandidates;

        if (RefreshPrice > 0)
        {
            if (playerResources == null) return RefreshBlock.MissingComponent;
            if (playerResources.GetAmount(ShopConfig.ToResourceType(RefreshCurrency)) < RefreshPrice) return RefreshBlock.NotEnoughCurrency;
        }
        return RefreshBlock.None;
    }

    // 갱신: 재화를 내고, 플레이어 아이템 칸을 전부(팔린 칸 포함) 새로 뽑습니다. 이 상점에 진열됐던 아이템은 다시 나오지 않습니다.
    // 사지 않은 채 사라지는 아이템은 "이 레벨에서 나온 아이템" 기록에서 지워 보물상자/48번/56번 후보로 돌려보냅니다.
    // 후보가 모자라면 있는 만큼만 진열하고 나머지 칸은 빈 공간이 됩니다. 성공하면 true.
    public bool TryRefresh()
    {
        var block = CheckRefresh();
        if (block != RefreshBlock.None)
        {
            Debug.Log($"ShopNPC({name}): 갱신 불가 - {block}");
            return false;
        }

        if (RefreshPrice > 0 && !playerResources.TrySpend(ShopConfig.ToResourceType(RefreshCurrency), RefreshPrice)) return false;
        refreshesUsed++;

        // 사지 않은 채 사라지는 아이템은 기록에서 지웁니다(산 아이템은 그대로 기록 유지).
        var unsold = GetDisplayedPlayerItems();
        var gameFlow = UnityEngine.Object.FindFirstObjectByType<GameFlowManager>();
        if (gameFlow != null)
        {
            foreach (var item in unsold) gameFlow.UnmarkItemAssignedThisLevel(item.itemId);
        }
        listings.RemoveAll(l => !l.isBasic);

        foreach (var item in PickPlayerItems(RollWantedPlayerItemCount(), everDisplayed))
        {
            listings.Add(CreateItemListing(item));
        }

        Debug.Log($"ShopNPC({name}): 갱신 ({RefreshPrice} {ShopConfig.GetCurrencyName(RefreshCurrency)}, 남은 갱신 {RemainingRefreshes}회)");
        return true;
    }

    // 지금 진열 중인(아직 안 팔린) 플레이어 아이템
    private List<ItemDefinition> GetDisplayedPlayerItems()
    {
        var result = new List<ItemDefinition>();
        foreach (var l in listings)
        {
            if (l != null && !l.isBasic && !l.soldOut && l.item != null) result.Add(l.item);
        }
        return result;
    }

    // ------------------------------------------------------------------
    // 구매 (3단계) - 상점 창(ShopUI)의 구매 팝업이 이 함수들로 판정/처리합니다.
    // ------------------------------------------------------------------

    // 구매할 수 없는 이유입니다(None = 구매 가능).
    public enum PurchaseBlock
    {
        None,
        SoldOut,            // 이미 팔린/없는 상품
        AlreadyOwned,       // 이미 가진 패시브
        NoHoldingSpace,     // 슬롯 아이템 교체 시 원래 아이템을 올려둘 빈 보관 좌대가 없음
        HealthFull,         // 현재 체력이 가득 참
        InvalidQuantity,    // 수량이 범위를 벗어남
        MissingComponent,   // 플레이어 쪽 컴포넌트를 못 찾음(설정 문제)
        NotEnoughCurrency,  // 재화 부족 (가격 숫자만 빨갛게 표시)
    }

    // 플레이어 아이템과 일반 상한 - 소모성 재화는 재고 무제한이지만 팝업 수량은 이 값까지만 올라갑니다.
    public const int MaxBasicQuantity = 99;

    private PlayerResources playerResources;
    private PlayerStats playerStats;
    private PlayerInventory playerInventory;

    // 플레이어 루트(PlayerHealth가 붙은 오브젝트)를 찾습니다. "Player" 태그는 플레이어 프리팹의 자식(FirePoint/모델 등)에도
    // 붙어 있어서, 태그로 찾으면 자식이 잡혀 PlayerResources 등을 못 찾을 수 있습니다 - 그래서 PlayerHealth로 찾습니다.
    private void FindPlayerRoot()
    {
        var health = UnityEngine.Object.FindFirstObjectByType<PlayerHealth>();
        if (health == null) return;
        playerHealth = health;
        playerTransform = health.transform;
    }

    private void EnsurePlayerRefs()
    {
        if (playerTransform == null) FindPlayerRoot();
        if (playerTransform == null) return;

        GameObject p = playerTransform.gameObject;
        if (playerHealth == null) playerHealth = p.GetComponent<PlayerHealth>();
        if (playerResources == null) playerResources = p.GetComponent<PlayerResources>();
        if (playerStats == null) playerStats = p.GetComponent<PlayerStats>();
        if (playerInventory == null) playerInventory = p.GetComponent<PlayerInventory>();
    }

    // 지금 깎여 있는 체력(없으면 0)
    private float GetMissingHealth()
    {
        if (playerHealth == null || playerStats == null) return 0f;
        return Mathf.Max(0f, playerStats.MaxHealth - playerHealth.CurrentHealth);
    }

    // 구매 팝업에서 고를 수 있는 최대 수량입니다.
    // - 플레이어 아이템: 항상 1개(일회용/음식이어도 - 형빈 확정)
    // - 현재 체력: 깎인 체력만큼(올림 - 0.5가 깎였으면 1개까지, 실제 회복은 0.5만 됨)
    //   [101번] 회복 배율(HealBoostOverride)을 반영해서, 1개당 실제 회복량(1회 회복량 x 배율) 기준으로 셉니다(2칸 깎였고 2배면 1개까지).
    // - 코스트: 최대치까지 남은 만큼(올림)
    // - 그 외: MaxBasicQuantity
    public int GetMaxQuantity(ShopListing listing)
    {
        if (listing == null || !listing.isBasic) return 1;
        EnsurePlayerRefs();

        int per = Mathf.Max(1, listing.amountPerPurchase);
        switch (listing.basicType)
        {
            case BasicItemType.Health:
                float healPerUnit = per * Mathf.Max(1f, HealBoostOverride.GetMultiplier());
                return Mathf.Max(1, Mathf.CeilToInt(GetMissingHealth() / healPerUnit - 0.0001f));
            case BasicItemType.Cost:
                if (playerResources == null) return 1;
                int room = playerResources.maxCost - playerResources.GetAmount(BasicItemType.Cost);
                return Mathf.Clamp(Mathf.CeilToInt((float)room / per), 1, MaxBasicQuantity);
            default:
                return MaxBasicQuantity;
        }
    }

    // 지금 실제로 받는 1회 가격입니다. listing.unitPrice는 진열될 때 정해진 원래 가격이고, 플레이어 아이템 칸에는 67번 할인 같은
    // 상점 가격 보정(ShopPriceModifier)을 볼 때마다 새로 적용합니다(실시간 - 상점에 있는 동안 보정이 생기거나 사라지면 바로 바뀜).
    // 소모성 재화 칸은 보정하지 않습니다.
    public static int GetUnitPrice(ShopListing listing)
    {
        if (listing == null) return 0;
        if (listing.isBasic || listing.item == null) return listing.unitPrice;
        return ShopPriceModifier.GetPrice(listing.item, listing.unitPrice);
    }

    // 원래 가격과 실제 가격이 다른지(상점 창에서 원래 가격을 회색 + 빨간 줄로 같이 보여줄지 판단할 때 씀)
    public static bool IsPriceModified(ShopListing listing)
    {
        return listing != null && GetUnitPrice(listing) != listing.unitPrice;
    }

    // 원래 가격 기준 합계(할인 전 가격 표시용)
    public static int GetOriginalTotalPrice(ShopListing listing, int quantity)
    {
        return listing == null ? 0 : listing.unitPrice * Mathf.Max(0, quantity);
    }

    // 실제로 내는 합계입니다(보정된 1회 가격 x 수량).
    public static int GetTotalPrice(ShopListing listing, int quantity)
    {
        return listing == null ? 0 : GetUnitPrice(listing) * Mathf.Max(0, quantity);
    }

    // 재화가 충분한지(가격 숫자를 빨갛게 할지 판단할 때도 씀)
    public bool CanAfford(ShopListing listing, int quantity)
    {
        EnsurePlayerRefs();
        int total = GetTotalPrice(listing, quantity);
        if (total <= 0) return true;
        return playerResources != null && playerResources.GetAmount(ShopConfig.ToResourceType(listing.currency)) >= total;
    }

    // 구매할 수 없는 이유를 돌려줍니다. 재화 부족은 다른 이유가 하나도 없을 때만 돌려줍니다.
    public PurchaseBlock CheckPurchase(ShopListing listing, int quantity)
    {
        if (listing == null || listing.soldOut) return PurchaseBlock.SoldOut;
        EnsurePlayerRefs();

        if (!listing.isBasic)
        {
            if (listing.item == null) return PurchaseBlock.SoldOut;
            if (HoldingPedestal.IsSlotCategory(listing.item.category))
            {
                GameObject player = playerTransform != null ? playerTransform.gameObject : null;
                if (!HoldingPedestal.HasSlotComponent(player, listing.item.category)) return PurchaseBlock.MissingComponent;
                if (HoldingPedestal.IsSlotItemEquipped(player, listing.item)) return PurchaseBlock.AlreadyOwned;
                // 사면 밀려날 아이템이 있을 때만 그것을 올려둘 빈 보관 좌대가 필요합니다(55번/61번/79번으로 장신구/일회용/음식 빈칸이 있으면 필요 없음).
                ItemDefinition equipped = HoldingPedestal.GetEquippedSlotItem(player, listing.item.category);
                if (equipped != null && FindEmptyHoldingPedestal() == null) return PurchaseBlock.NoHoldingSpace;
            }
            else
            {
                if (playerInventory == null) return PurchaseBlock.MissingComponent;
                if (playerInventory.HasItem(listing.item.itemId)) return PurchaseBlock.AlreadyOwned;
            }
        }
        else if (listing.basicType == BasicItemType.Health)
        {
            if (playerHealth == null || playerStats == null) return PurchaseBlock.MissingComponent;
            if (GetMissingHealth() <= 0f) return PurchaseBlock.HealthFull;
        }

        if (quantity < 1 || quantity > GetMaxQuantity(listing)) return PurchaseBlock.InvalidQuantity;
        if (playerResources == null) return PurchaseBlock.MissingComponent;
        if (!CanAfford(listing, quantity)) return PurchaseBlock.NotEnoughCurrency;
        return PurchaseBlock.None;
    }

    // 실제 구매: 재화를 내고 상품을 받습니다. 받을 수 있는지는 재화를 내기 전에 전부 확인하므로(CheckPurchase), 재화만 빠지고
    // 상품을 못 받는 일은 없습니다. 성공하면 true.
    public bool TryPurchase(ShopListing listing, int quantity)
    {
        var block = CheckPurchase(listing, quantity);
        if (block != PurchaseBlock.None)
        {
            Debug.Log($"ShopNPC({name}): '{listing?.displayName}' 구매 불가 - {block}");
            return false;
        }

        int total = GetTotalPrice(listing, quantity);
        if (total > 0 && !playerResources.TrySpend(ShopConfig.ToResourceType(listing.currency), total)) return false;

        if (listing.isBasic)
        {
            int amount = quantity * Mathf.Max(1, listing.amountPerPurchase);
            if (listing.basicType == BasicItemType.Health)
            {
                playerHealth.Heal(amount); // 최대 체력을 넘는 만큼은 Heal이 잘라냄(0.5가 깎였으면 0.5만 회복)
            }
            else
            {
                playerResources.Add(listing.basicType, amount);
            }
        }
        else if (HoldingPedestal.IsSlotCategory(listing.item.category))
        {
            // 슬롯 아이템: 장착하고, 원래 장착하던 아이템은 빈 보관 좌대에 올립니다(빈 좌대는 CheckPurchase에서 확인함).
            HoldingPedestal emptyPedestal = FindEmptyHoldingPedestal();
            // [1회성 지급 - 교체 복제 방지] 상점 구매는 새 사본이라 획득 시 1회성 지급을 받습니다(GameFlowManager.AcquireItemCopy).
            // 밀려난 아이템은 보관 좌대에 올라가며, 거기서 다시 주워도 지급되지 않습니다.
            ItemDefinition displaced = null;
            GameObject buyer = playerTransform.gameObject;
            ItemDefinition bought = listing.item;
            if (!GameFlowManager.AcquireItemCopy(bought, true, () =>
                {
                    bool ok = HoldingPedestal.TryEquipSlotItem(buyer, bought, out ItemDefinition d);
                    displaced = d;
                    return ok;
                }))
            {
                Debug.LogError($"ShopNPC({name}): '{listing.displayName}' 장착에 실패했습니다(재화 {total} 사용됨). 확인이 필요합니다.");
                return false;
            }
            if (displaced != null && (emptyPedestal == null || !emptyPedestal.TryPlace(displaced)))
            {
                Debug.LogError($"ShopNPC({name}): 교체된 '{displaced.displayName}'을(를) 보관 좌대에 올리지 못했습니다. 확인이 필요합니다.");
            }
            listing.soldOut = true;
            ItemPickupEvents.Raise(listing.item);
        }
        else
        {
            ItemDefinition boughtPassive = listing.item;
            if (!GameFlowManager.AcquireItemCopy(boughtPassive, true, () => playerInventory.TryAcquireItem(boughtPassive)))
            {
                Debug.LogError($"ShopNPC({name}): '{listing.displayName}' 지급에 실패했습니다(재화 {total} 사용됨). 확인이 필요합니다.");
                return false;
            }
            listing.soldOut = true;                // 그 칸은 카드 위에 어두운 덮개가 씌워집니다
            ItemPickupEvents.Raise(listing.item);  // 보물상자에서 주울 때와 같은 획득 연출
        }

        Debug.Log($"ShopNPC({name}): '{listing.displayName}' x{quantity} 구매 ({total} {ShopConfig.GetCurrencyName(listing.currency)})");
        return true;
    }

    // 이 상품(슬롯 아이템)을 사면 교체될, 지금 장착 중인 아이템(없거나 슬롯 아이템이 아니면 null) - 교체 확인창용
    public ItemDefinition GetEquippedItemReplacedBy(ShopListing listing)
    {
        if (listing == null || listing.isBasic || listing.item == null) return null;
        if (!HoldingPedestal.IsSlotCategory(listing.item.category)) return null;
        EnsurePlayerRefs();
        return HoldingPedestal.GetEquippedSlotItem(playerTransform != null ? playerTransform.gameObject : null, listing.item.category);
    }

    // 이 상점의 보관 좌대 목록: 인스펙터 리스트 → (비었으면) 같은 방 안 자동 탐색
    private readonly List<HoldingPedestal> autoFoundPedestals = new List<HoldingPedestal>();
    private bool autoSearched = false;

    private List<HoldingPedestal> GetHoldingPedestals()
    {
        if (holdingPedestals != null && holdingPedestals.Count > 0) return holdingPedestals;

        if (!autoSearched)
        {
            autoSearched = true;
            var room = GetComponentInParent<RoomController>();
            Transform searchRoot = room != null ? room.transform : transform.parent;
            if (searchRoot != null) autoFoundPedestals.AddRange(searchRoot.GetComponentsInChildren<HoldingPedestal>(true));
        }
        return autoFoundPedestals;
    }

    // 비어 있는 첫 보관 좌대(없으면 null)
    public HoldingPedestal FindEmptyHoldingPedestal()
    {
        foreach (var pedestal in GetHoldingPedestals())
        {
            if (pedestal != null && pedestal.IsEmpty) return pedestal;
        }
        return null;
    }

    private static HashSet<ItemDefinition> GetEquippedSlotItems(GameObject player)
    {
        var result = new HashSet<ItemDefinition>();
        if (player == null) return result;

        var active = player.GetComponent<PlayerActiveItem>();
        if (active != null && active.EquippedItem != null) result.Add(active.EquippedItem);
        var trinket = player.GetComponent<PlayerTrinket>();
        if (trinket != null)
        {
            foreach (var item in trinket.EquippedItems)
            {
                if (item != null) result.Add(item); // 55번으로 2개일 수 있음
            }
        }
        var disposable = player.GetComponent<PlayerDisposableItem>();
        if (disposable != null)
        {
            foreach (var item in disposable.EquippedItems)
            {
                if (item != null) result.Add(item); // 61번으로 2개일 수 있음
            }
        }
        var food = player.GetComponent<PlayerFoodItem>();
        if (food != null)
        {
            foreach (var item in food.EquippedItems)
            {
                if (item != null) result.Add(item); // 79번으로 2개일 수 있음
            }
        }
        return result;
    }

    private ShopListing CreateItemListing(ItemDefinition item)
    {
        var priceRow = shopConfig.GetRarityPrice(item.rarity);
        if (priceRow == null)
        {
            Debug.LogWarning($"ShopNPC({name}): Shop Config에 '{item.rarity}' 등급 가격이 없어 '{item.displayName}' 가격을 0으로 표시합니다.");
        }

        return new ShopListing
        {
            isBasic = false,
            item = item,
            displayName = string.IsNullOrEmpty(item.displayName) ? item.name : item.displayName,
            icon = item.icon,
            currency = priceRow != null ? priceRow.currency : ShopCurrency.Credit,
            unitPrice = priceRow != null ? priceRow.RollPrice() : 0, // 등급별 가격 범위 안에서 무작위(진열될 때마다)
            amountPerPurchase = 1,
            soldOut = false,
        };
    }
}
