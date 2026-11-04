using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 플레이어가 장착 중인 "음식"을 관리합니다. PlayerDisposableItem과 거의 완전히 동일한
// 슬롯 + 교체 구조입니다 - 음식은 일회용과 "사용 방식"이 완전히 같기 때문입니다(즉시형/
// 지연형, 대기 중 교체 시 취소 등). 다른 점은 딱 셋입니다.
//   1. 별도의 전용 슬롯(가방 칸)을 씁니다 - 일회용(Q)과는 완전히 독립적인 저장소입니다.
//   2. 사용 키가 Q가 아니라 E입니다.
//   3. 슬롯에 새로 장착되는 순간 IFoodEffect.OnAcquired()가 추가로 호출됩니다(획득 시 1회성
//      효과 - 예: 34번의 "획득 시 최초 한정 최대 체력 증가").
//
// [교체(swap) 계약 - TryAcquireItem]
// PlayerDisposableItem과 동일합니다 - 새 음식을 얻으려는 쪽(ItemPedestal/TreasureChest)이 이
// 메서드를 호출합니다. 칸이 꽉 차 있으면 가장 먼저 얻은 음식을 displacedItem으로 돌려줘 보물상자에 다시
// 채워지도록 합니다. 새로 장착되는 아이템에 대해서만 OnAcquired()를 호출합니다 - 방출되는
// 쪽에는 아무 일도 일어나지 않습니다(장신구의 Remove()에 대응하는 개념이 음식에는 없습니다 -
// IFoodEffect.cs 상단 주석 참고).
//
// [음식 칸 늘리기 - 79번 (형빈 확정, 61번 일회용과 같은 규칙)]
// 칸 수 = 기본 1칸 + AddExtraSlots로 등록된 추가 칸(79번이 +1). 장착 순서는 "먼저 얻은 것이 앞"입니다.
// - 빈칸이 있으면 새 음식을 맨 뒤에 추가합니다(밀려나는 음식 없음).
// - 칸이 꽉 차 있으면 맨 앞(가장 먼저 얻은) 음식이 밀려나고 나머지가 앞으로 당겨진 뒤 새 음식이 맨 뒤에 들어갑니다.
//   교체 확인창은 "지금 얻으면 밀려날 음식"(ReplacedOnAcquire - 빈칸이 있으면 없음) 기준으로 뜹니다.
// - E는 항상 맨 앞 음식을 먹습니다. 즉시형이면 바로 소모되고 뒤의 음식이 앞으로 당겨집니다. 지연형이면 맨 앞 음식이 대기
//   상태가 되고, 대기가 끝나(소모되어) 사라질 때까지 E를 눌러도 아무것도(뒤의 음식도) 먹지 않습니다.
// - 칸이 줄어들어(79번을 잃음) 장착 수가 칸 수보다 많아지면, 그 프레임 끝(LateUpdate)에 나중에 얻은 음식부터 지금 방 바닥에
//   좌대로 떨어뜨립니다(55번/61번과 같음). 대기 중인 음식은 항상 맨 앞이라 떨어지지 않습니다.
//
// [지연형 사용 중 교체 - 대기 취소]
// 대기 중인 음식(맨 앞)이 교체로 밀려나거나 버려지면, 효과를 발동시키지 않고 CancelPending()만 호출한 뒤 진행합니다 -
// 대기 중이던 아이템도 정상적으로 displacedItem에 담겨 보물상자로 돌아갑니다.
//
// [사용 - E]
// 장착 중인 음식이 있고 대기 중이 아닐 때만 E 입력을 받습니다. 즉시형이면 바로 효과를
// 일으키고 소멸시키고, 지연형이면 BeginPending()만 호출해 대기 상태로 들어갑니다(아직
// 소멸하지 않음). 이미 대기 중일 때 E를 다시 눌러도 아무 일도 일어나지 않습니다.
//
// [직전에 사용한 음식 - LastUsedFood]
// 음식이 실제로 소모되어 사라지는 순간(즉시형은 E를 누른 즉시, 지연형은 효과가 실제로 발동해
// 소멸하는 순간)의 아이템을 기억합니다. 대기 중에 교체되어 취소된 지연형은 소모된 적이 없으므로
// 기록되지 않습니다. 93번 액티브 아이템이 이 값을 읽어 직전 음식의 효과를 재현합니다. 아래
// ConsumptionRedirect로 소모 자체가 대체된 경우에도(음식이 사라지지 않았으므로) 기록되지
// 않습니다 - 자연스럽게 조기 반환되기 때문입니다.
//
// [ConsumptionRedirect - 25번 일회용 아이템 연동, "다음 음식은 소모되지 않는다"]
// 즉시형/지연형 모두 실제 소멸은 항상 ConsumeEquippedItem() 한 곳에서만 일어납니다(즉시형은
// ActivateImmediate 직후, 지연형은 CompletePendingUse 안에서). 25번처럼 "다음에 사용하는
// 음식은 효과는 그대로 발동하되 소모되지 않고, 대신 다른 무언가(25번 자신)가 소모된다"는
// 효과는 이 한 지점만 가로채면 즉시형/지연형 구분 없이 자동으로 처리됩니다. null이 아니면
// 다음 소모 시도 때 이 델리게이트를 대신 호출하고, true를 반환하면 음식을 소모하지 않습니다
// (그 대신 델리게이트 안에서 다른 소모 처리를 이미 마쳤다는 뜻 - 음식은 맨 앞 칸에 그대로 남음). 정확히 한 번만 쓰이고
// 스스로 비워집니다 - 여러 번 걸쳐 있고 싶다면 등록한 쪽이 다시 걸어야 합니다.
//
// [OnFoodConsumed - 65번 등 "음식을 소모할 때마다" 아이템용 알림]
// 음식 사용이 완전히 끝나 소모되는 순간(즉시형은 E 직후, 지연형은 대기가 끝나 효과가 발동한 직후) 한 번 발생합니다.
// 25번으로 소모가 대신 처리된 경우(음식은 남음)도 "소모"로 취급해 발생합니다(형빈 확정, redirected = true).
// 지연형 대기 중/대기 취소(교체·버리기)/칸 감소로 떨어짐/93번 재발동(이 슬롯을 거치지 않음)에서는 발생하지 않습니다.
[RequireComponent(typeof(PlayerStats))]
public class PlayerFoodItem : MonoBehaviour
{
    // 기본 음식 칸 수
    public const int BaseSlotCount = 1;

    [Header("사용 입력")]
    public KeyCode useKey = KeyCode.E;

    // [25번 일회용 아이템 연동] 위 클래스 주석 참고.
    public System.Func<bool> ConsumptionRedirect;

    // [65번 등] 위 클래스 주석 참고. (소모된 음식, 25번으로 대신 처리됐는지)
    public event System.Action<ItemDefinition, bool> OnFoodConsumed;

    private PlayerStats stats;
    private PlayerHealth health;
    private PlayerResources resources;
    private GameFlowManager gameFlow;
    private LevelBuilder levelBuilder;
    private FoodEffectContext effectContext;

    // 장착 중인 음식(먼저 얻은 것이 앞). 대기(지연 소모)는 항상 맨 앞 음식만 가능합니다.
    private readonly List<ItemDefinition> equippedItems = new List<ItemDefinition>();
    private bool isPending;
    private ItemDefinition lastUsedFood;

    // 추가 칸 등록(출처 이름 -> 칸 수). 79번 등
    private readonly Dictionary<string, int> extraSlots = new Dictionary<string, int>();

    // 맨 앞(다음에 E로 먹을) 음식. 없으면 null.
    public ItemDefinition EquippedItem => equippedItems.Count > 0 ? equippedItems[0] : null;

    // 맨 앞 음식이 지연 소모 대기 중인지
    public bool IsPending => isPending;

    // 가장 최근에 실제로 소모된(=사용이 끝난) 음식입니다. 아직 아무것도 먹지 않았으면 null입니다.
    public ItemDefinition LastUsedFood => lastUsedFood;

    // 장착 중인 음식 전체(먼저 얻은 것이 앞). 읽기 전용.
    public IReadOnlyList<ItemDefinition> EquippedItems => equippedItems;

    // 지금 장착할 수 있는 칸 수(기본 1 + 추가 칸)
    public int SlotCount
    {
        get
        {
            int count = BaseSlotCount;
            foreach (var pair in extraSlots) count += pair.Value;
            return Mathf.Max(0, count);
        }
    }

    // index번째 칸의 음식(없으면 null). 0 = 가장 먼저 얻은(다음에 E로 먹을) 음식.
    public ItemDefinition GetEquippedAt(int index)
    {
        return index >= 0 && index < equippedItems.Count ? equippedItems[index] : null;
    }

    // index번째 칸이 지연 소모 대기 중인지(대기는 맨 앞 칸만 가능)
    public bool IsPendingAt(int index) => index == 0 && isPending && equippedItems.Count > 0;

    // 지금 새 음식을 얻으면 밀려날 음식입니다. 빈칸이 있으면 null, 꽉 찼으면 가장 먼저 얻은 음식.
    // 교체 확인창/상점의 보관 좌대 필요 여부 판단에 씁니다.
    public ItemDefinition ReplacedOnAcquire =>
        equippedItems.Count >= SlotCount && equippedItems.Count > 0 ? equippedItems[0] : null;

    // 이 음식을 장착 중인지(itemId 기준)
    public bool IsEquipped(ItemDefinition item)
    {
        if (item == null || string.IsNullOrEmpty(item.itemId)) return false;
        foreach (var equipped in equippedItems)
        {
            if (equipped != null && equipped.itemId == item.itemId) return true;
        }
        return false;
    }

    void Awake()
    {
        stats = GetComponent<PlayerStats>();
        health = GetComponent<PlayerHealth>();
        resources = GetComponent<PlayerResources>();
        gameFlow = Object.FindFirstObjectByType<GameFlowManager>();
        levelBuilder = Object.FindFirstObjectByType<LevelBuilder>();

        if (levelBuilder == null)
        {
            Debug.LogWarning("PlayerFoodItem: LevelBuilder를 찾지 못했습니다. " +
                              "레벨 구조가 필요한 음식 효과는 정상 동작하지 않을 수 있습니다.");
        }

        effectContext = new FoodEffectContext(stats, health, resources, gameFlow, levelBuilder, gameObject, this);
    }

    void Update()
    {
        if (equippedItems.Count == 0 || isPending) return;
        // 아이템 획득 연출(PlayerItemPickupPresenter) 중에는 음식(E) 사용을 막습니다(형빈 확정).
        if (PlayerItemPickupPresenter.BlocksActions) return;
        if (Input.GetKeyDown(useKey))
        {
            TryUse();
        }
    }

    // [칸이 줄었을 때] 장착 수가 칸 수보다 많으면 나중에 얻은 음식부터 바닥에 떨어뜨립니다(55번/61번과 같은 규칙, 형빈 확정).
    // 프레임 끝에 판정해서, 같은 프레임 안에 칸이 줄었다가 다시 늘어난 경우(48번이 79번을 79번으로 바꿈)는 무시됩니다.
    // 대기 중인 음식은 맨 앞이라 칸이 1칸 이상 남아 있는 한 떨어지지 않습니다.
    void LateUpdate()
    {
        int slotCount = SlotCount;
        while (equippedItems.Count > slotCount && equippedItems.Count > 0)
        {
            int last = equippedItems.Count - 1;
            ItemDefinition overflow = equippedItems[last];
            if (last == 0 && isPending)
            {
                overflow?.foodEffect?.CancelPending(effectContext, overflow.itemId);
                isPending = false;
            }
            equippedItems.RemoveAt(last);
            if (overflow == null) continue;

            Debug.Log($"PlayerFoodItem: 음식 칸이 줄어 '{overflow.displayName}'을(를) 바닥에 떨어뜨립니다.");
            SlotItemDropper.DropNearPlayer(gameObject, overflow);
        }
    }

    // 음식 칸을 amount만큼 늘립니다(sourceId별로 하나만 - 같은 이름으로 다시 부르면 덮어씀).
    public void AddExtraSlots(string sourceId, int amount)
    {
        if (string.IsNullOrEmpty(sourceId)) return;
        extraSlots[sourceId] = amount;
    }

    // AddExtraSlots로 늘린 칸을 되돌립니다. 넘치는 음식은 그 프레임 끝(LateUpdate)에 떨어집니다.
    public void RemoveExtraSlots(string sourceId)
    {
        if (string.IsNullOrEmpty(sourceId)) return;
        extraSlots.Remove(sourceId);
    }

    private void TryUse()
    {
        ItemDefinition item = EquippedItem;
        if (item == null) return;

        var effect = item.foodEffect;
        if (effect == null) return;

        string sourceId = item.itemId;

        // [9번 음식 연동 - CanUse 게이트] 지금 사용할 수 없는 음식이면(예: 9번인데 복제할
        // 보물상자가 방에 없음) 아무 일도 일어나지 않아야 합니다 - 효과도 발동하지 않고,
        // 음식도 소모되지 않습니다(대기 상태로도 들어가지 않음). 뒤 칸의 음식이 대신 먹히지도 않습니다.
        if (!effect.CanUse(effectContext))
        {
            Debug.Log($"PlayerFoodItem: '{item.displayName}' 지금은 사용할 수 없어 소모되지 않았습니다.");
            return;
        }

        if (effect.IsImmediate)
        {
            effect.ActivateImmediate(effectContext, sourceId);
            ConsumeEquippedItem(item);
        }
        else
        {
            isPending = true;
            effect.BeginPending(effectContext, sourceId);
        }
    }

    // 지연형 효과가 실제 트리거 발생 시 FoodEffectContext.CompletePendingUse를 거쳐 호출합니다.
    // sourceItemId가 지금 대기 중인(맨 앞) 음식과 일치할 때만 실제로 소멸시킵니다 - 이미
    // 교체(CancelPending)돼서 대기가 끝난 뒤에 뒤늦게 트리거가 와도 무시됩니다.
    public void CompletePendingUse(string sourceItemId)
    {
        ItemDefinition front = EquippedItem;
        if (!isPending || front == null || front.itemId != sourceItemId) return;
        isPending = false;
        ConsumeEquippedItem(front);
    }

    // newItem 장착을 시도합니다. 빈칸이 있으면 맨 뒤에 추가하고 displacedItem = null, 칸이 꽉 차 있으면 가장 먼저 얻은
    // 음식을 빼서(대기 중이었다면 먼저 CancelPending으로 대기 취소) displacedItem으로 돌려주고, 새 음식을 맨 뒤에 넣은 뒤
    // OnAcquired()를 호출합니다(획득 시 1회성 효과). 이미 같은 음식을 장착 중이면(정상적으로는 일어나지 않음) false.
    public bool TryAcquireItem(ItemDefinition newItem, out ItemDefinition displacedItem)
    {
        displacedItem = null;
        if (newItem == null) return false;

        if (IsEquipped(newItem))
        {
            Debug.LogWarning($"PlayerFoodItem: '{newItem.displayName}'은(는) 이미 장착 중이라 장착하지 않았습니다.");
            return false;
        }

        if (equippedItems.Count >= SlotCount && equippedItems.Count > 0)
        {
            displacedItem = equippedItems[0];
            if (isPending)
            {
                displacedItem?.foodEffect?.CancelPending(effectContext, displacedItem.itemId);
                isPending = false;
            }
            equippedItems.RemoveAt(0);
        }

        equippedItems.Add(newItem);
        ItemCodexRecord.MarkAcquired(newItem); // [도감] 처음 인벤토리에 들어온 아이템이면 도감 기록에 남깁니다(테스트 씬 제외 - ItemCodexRecord.cs).
        newItem.foodEffect?.OnAcquired(effectContext, newItem.itemId);

        Debug.Log(displacedItem == null
            ? $"PlayerFoodItem: '{newItem.displayName}' 장착."
            : $"PlayerFoodItem: '{newItem.displayName}' 장착 (기존 '{displacedItem.displayName}'은 방출됨).");

        return true;
    }

    // [슬롯 아이템 버리기 연동] 장착 중인 음식 item을 빼냅니다(SlotItemDropper가 호출). 그 음식이 지연 소모 대기 중이었으면
    // 교체와 같이 CancelPending()으로 대기를 먼저 취소합니다(효과는 발동하지 않고, 먹은 적이 없으므로 LastUsedFood도 그대로).
    // 뒤의 음식은 앞으로 당겨집니다. 장착 중이 아니면 아무 일도 없이 false.
    public bool TryUnequip(ItemDefinition item, out ItemDefinition removedItem)
    {
        removedItem = null;
        if (item == null) return false;

        int index = equippedItems.IndexOf(item);
        if (index < 0) return false;

        if (index == 0 && isPending)
        {
            item.foodEffect?.CancelPending(effectContext, item.itemId);
            isPending = false;
        }
        equippedItems.RemoveAt(index);
        removedItem = item;

        Debug.Log($"PlayerFoodItem: '{item.displayName}' 장착 해제(버림).");
        return true;
    }

    // 맨 앞 음식(item)을 소모합니다. 뒤의 음식은 앞으로 당겨집니다.
    private void ConsumeEquippedItem(ItemDefinition item)
    {
        if (equippedItems.Count == 0 || equippedItems[0] != item) return;

        // [25번 일회용 아이템 연동] 이번 소모를 다른 쪽이 대신 처리하고 싶다고 걸어둔 훅이
        // 있으면 먼저 물어봅니다. true가 나오면(대신 처리 완료) 음식은 그대로 남기고
        // 여기서 끝냅니다 - lastUsedFood도 갱신하지 않습니다(실제로 소모된 적이 없으므로).
        if (ConsumptionRedirect != null)
        {
            var redirect = ConsumptionRedirect;
            ConsumptionRedirect = null; // 1회성 - 스스로 비웁니다.

            if (redirect())
            {
                Debug.Log($"PlayerFoodItem: '{item.displayName}' 사용됐지만 다른 아이템이 대신 소모되어 소멸하지 않음.");
                OnFoodConsumed?.Invoke(item, true); // 25번 대신 소모도 음식 소모로 취급(형빈 확정)
                return;
            }
        }

        Debug.Log($"PlayerFoodItem: '{item.displayName}' 사용 후 소멸.");
        lastUsedFood = item;
        equippedItems.Remove(item);
        OnFoodConsumed?.Invoke(item, false);

        // [사용 연출] 실제로 소모되는 순간 머리 위에 이 음식 아이콘을 띄웁니다(등급 색 발광 - ItemUsePopup.cs 참고). 25번이 대신
        // 소모된 경우(위 redirect)에는 음식이 소모되지 않았으므로 띄우지 않습니다(대신 소모된 쪽이 소모될 때 그쪽 아이콘이 뜸).
        ItemUsePopup.Show(gameObject, item);
    }

    // 음식 슬롯과 무관하게 음식 효과를 재현할 때(93번 액티브) 쓸 컨텍스트를 만듭니다. 이 컨텍스트로
    // 실행한 지연형 효과가 끝났다고 알리면 음식 슬롯이 아니라 onPendingCompleted로 신호가 갑니다.
    public FoodEffectContext CreateReplayContext(System.Action<string> onPendingCompleted)
    {
        return new FoodEffectContext(stats, health, resources, gameFlow, levelBuilder, gameObject, onPendingCompleted);
    }

    // 이미 장착 중인 음식(itemId 기준, 모든 칸)을 풀에서 제외한 목록을 반환합니다. ItemPedestal/
    // TreasureChest가 뽑기 직전에 다른 FilterOutEquipped들과 함께 호출해야 하는 유틸입니다.
    public List<ItemDefinition> FilterOutEquipped(List<ItemDefinition> pool)
    {
        if (pool == null) return new List<ItemDefinition>();
        if (equippedItems.Count == 0) return pool;

        return pool
            .Where(item => item != null && !IsEquipped(item))
            .ToList();
    }
}
