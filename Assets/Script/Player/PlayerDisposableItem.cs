using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 플레이어가 장착 중인 "일회용 아이템"을 관리합니다. PlayerActiveItem과 거의 동일한
// 슬롯 + 교체 구조지만, 사용 키가 스페이스가 아니라 Q이고, 코스트 개념이 없으며, 사용
// 방식이 "즉시형"과 "지연형" 두 가지로 나뉜다는 점이 다릅니다.
//
// [교체(swap) 계약 - TryAcquireItem]
// PlayerActiveItem/PlayerTrinket과 동일합니다 - 새 일회용 아이템을 얻으려는 쪽(ItemPedestal/
// TreasureChest)이 이 메서드를 호출합니다. 칸이 꽉 차 있으면 가장 먼저 얻은 아이템을 displacedItem으로
// 돌려줘 보물상자에 다시 채워지도록 합니다.
//
// [일회용 칸 늘리기 - 61번 (형빈 확정, 55번 장신구와 같은 규칙)]
// 칸 수 = 기본 1칸 + AddExtraSlots로 등록된 추가 칸(61번이 +1). 장착 순서는 "먼저 얻은 것이 앞"입니다.
// - 빈칸이 있으면 새 아이템을 맨 뒤에 추가합니다(밀려나는 아이템 없음).
// - 칸이 꽉 차 있으면 맨 앞(가장 먼저 얻은) 아이템이 밀려나고 나머지가 앞으로 당겨진 뒤 새 아이템이 맨 뒤에 들어갑니다.
//   교체 확인창은 "지금 얻으면 밀려날 아이템"(ReplacedOnAcquire - 빈칸이 있으면 없음) 기준으로 뜹니다.
// - Q는 항상 맨 앞 아이템을 씁니다. 즉시형이면 바로 소모되고 뒤의 아이템이 앞으로 당겨집니다. 지연형이면 맨 앞 아이템이 대기
//   상태가 되고, 대기가 끝나(소모되어) 사라질 때까지 Q를 눌러도 아무것도(뒤의 아이템도) 쓰이지 않습니다.
// - 칸이 줄어들어(61번을 잃음) 장착 수가 칸 수보다 많아지면, 그 프레임 끝(LateUpdate)에 나중에 얻은 아이템부터 지금 방 바닥에
//   좌대로 떨어뜨립니다(55번과 같음). 대기 중인 아이템은 항상 맨 앞이라 떨어지지 않습니다.
//
// [지연형 사용 중 교체 - 대기 취소]
// 대기 중인 아이템(맨 앞)이 교체로 밀려나거나 버려지면, 효과를 발동시키지 않고 CancelPending()만 호출한 뒤 진행합니다 -
// 대기 중이던 아이템도 정상적으로 displacedItem에 담겨 보물상자로 돌아갑니다(효과만 취소될 뿐).
//
// [획득 시 효과 - 17번 연동]
// 효과가 IDisposableAcquireEffect를 함께 구현하면, 새로 장착되는 순간 OnAcquired()를 한 번 호출합니다(음식의 OnAcquired와
// 같은 시점). 구현하지 않은 일회용은 기존과 완전히 같습니다.
//
// [사용 - Q]
// 장착 중인 아이템이 있고 대기 중이 아닐 때만 Q 입력을 받습니다. 즉시형이면 바로 효과를
// 일으키고 소멸시키고, 지연형이면 BeginPending()만 호출해 대기 상태로 들어갑니다(아직
// 소멸하지 않음). 이미 대기 중일 때 Q를 다시 눌러도 아무 일도 일어나지 않습니다(재사용 불가 -
// 대기를 취소하려면 다른 일회용 아이템으로 교체해야 합니다).
[RequireComponent(typeof(PlayerStats))]
public class PlayerDisposableItem : MonoBehaviour
{
    // 기본 일회용 칸 수
    public const int BaseSlotCount = 1;

    [Header("사용 입력")]
    public KeyCode useKey = KeyCode.Q;

    private PlayerStats stats;
    private PlayerHealth health;
    private PlayerResources resources;
    private GameFlowManager gameFlow;
    private LevelBuilder levelBuilder;
    private DisposableEffectContext effectContext;

    // 장착 중인 일회용 아이템(먼저 얻은 것이 앞). 대기(지연 소모)는 항상 맨 앞 아이템만 가능합니다.
    private readonly List<ItemDefinition> equippedItems = new List<ItemDefinition>();
    private bool isPending;

    // 추가 칸 등록(출처 이름 -> 칸 수). 61번 등
    private readonly Dictionary<string, int> extraSlots = new Dictionary<string, int>();

    // 맨 앞(다음에 Q로 쓸) 아이템. 없으면 null.
    public ItemDefinition EquippedItem => equippedItems.Count > 0 ? equippedItems[0] : null;

    // 맨 앞 아이템이 지연 소모 대기 중인지
    public bool IsPending => isPending;

    // [85번 연동 - 직전에 소모한 일회용] 일회용이 실제로 소모되어 사라지는 순간(즉시형은 Q를 누른 즉시, 지연형은 효과가 발동해
    // 소멸하는 순간)의 아이템입니다. 대기 중 교체/버리기/스스로 취소(56번 [취소하기] 등)로 소모되지 않은 것은 기록하지 않고,
    // 85번 액티브로 재현한 것도 이 슬롯을 거치지 않으므로 기록이 바뀌지 않습니다. 85번이 이 값을 읽어 효과를 재현하고,
    // 좌상단 보조 아이콘(PlayerCostDisplay)이 이 아이템의 아이콘을 보여줍니다. PlayerFoodItem.LastUsedFood(93번)와 같은 방식입니다.
    private ItemDefinition lastUsedDisposable;
    public ItemDefinition LastUsedDisposable => lastUsedDisposable;

    // 장착 중인 일회용 아이템 전체(먼저 얻은 것이 앞). 읽기 전용.
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

    // index번째 칸의 아이템(없으면 null). 0 = 가장 먼저 얻은(다음에 Q로 쓸) 아이템.
    public ItemDefinition GetEquippedAt(int index)
    {
        return index >= 0 && index < equippedItems.Count ? equippedItems[index] : null;
    }

    // index번째 칸이 지연 소모 대기 중인지(대기는 맨 앞 칸만 가능)
    public bool IsPendingAt(int index) => index == 0 && isPending && equippedItems.Count > 0;

    // 지금 새 일회용을 얻으면 밀려날 아이템입니다. 빈칸이 있으면 null, 꽉 찼으면 가장 먼저 얻은 아이템.
    // 교체 확인창/상점의 보관 좌대 필요 여부 판단에 씁니다.
    public ItemDefinition ReplacedOnAcquire =>
        equippedItems.Count >= SlotCount && equippedItems.Count > 0 ? equippedItems[0] : null;

    // 이 일회용을 장착 중인지(itemId 기준)
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
            Debug.LogWarning("PlayerDisposableItem: LevelBuilder를 찾지 못했습니다. " +
                              "텔레포트 등 레벨 구조가 필요한 일회용 아이템 효과는 정상 동작하지 않을 수 있습니다.");
        }

        effectContext = new DisposableEffectContext(stats, health, resources, gameFlow, levelBuilder, gameObject, this);
    }

    void Update()
    {
        if (equippedItems.Count == 0 || isPending) return;
        // 아이템 획득 연출(PlayerItemPickupPresenter) 중에는 일회용(Q) 사용을 막습니다(형빈 확정).
        if (PlayerItemPickupPresenter.BlocksActions) return;
        if (Input.GetKeyDown(useKey))
        {
            TryUse();
        }
    }

    // [칸이 줄었을 때] 장착 수가 칸 수보다 많으면 나중에 얻은 아이템부터 바닥에 떨어뜨립니다(55번 장신구와 같은 규칙, 형빈 확정).
    // 프레임 끝에 판정해서, 같은 프레임 안에 칸이 줄었다가 다시 늘어난 경우(48번이 61번을 61번으로 바꿈)는 무시됩니다.
    // 대기 중인 아이템은 맨 앞이라 칸이 1칸 이상 남아 있는 한 떨어지지 않습니다.
    void LateUpdate()
    {
        int slotCount = SlotCount;
        while (equippedItems.Count > slotCount && equippedItems.Count > 0)
        {
            int last = equippedItems.Count - 1;
            ItemDefinition overflow = equippedItems[last];
            if (last == 0 && isPending)
            {
                overflow?.disposableEffect?.CancelPending(effectContext, overflow.itemId);
                isPending = false;
            }
            equippedItems.RemoveAt(last);
            if (overflow == null) continue;

            Debug.Log($"PlayerDisposableItem: 일회용 칸이 줄어 '{overflow.displayName}'을(를) 바닥에 떨어뜨립니다.");
            SlotItemDropper.DropNearPlayer(gameObject, overflow);
        }
    }

    // 일회용 칸을 amount만큼 늘립니다(sourceId별로 하나만 - 같은 이름으로 다시 부르면 덮어씀).
    public void AddExtraSlots(string sourceId, int amount)
    {
        if (string.IsNullOrEmpty(sourceId)) return;
        extraSlots[sourceId] = amount;
    }

    // AddExtraSlots로 늘린 칸을 되돌립니다. 넘치는 아이템은 그 프레임 끝(LateUpdate)에 떨어집니다.
    public void RemoveExtraSlots(string sourceId)
    {
        if (string.IsNullOrEmpty(sourceId)) return;
        extraSlots.Remove(sourceId);
    }

    private void TryUse()
    {
        ItemDefinition item = EquippedItem;
        if (item == null) return;

        var effect = item.disposableEffect;
        if (effect == null) return;

        string sourceId = item.itemId;

        // [22/46번 연동 - CanUse 게이트] 지금 사용할 수 없는 아이템이면(예: 텔레포트할
        // 대상 방이 이 레벨에 하나도 없음) 아무 일도 일어나지 않아야 합니다 - 효과도
        // 발동하지 않고, 아이템도 소모되지 않습니다(대기 상태로도 들어가지 않음). 뒤 칸의 아이템이 대신 쓰이지도 않습니다.
        if (!effect.CanUse(effectContext))
        {
            Debug.Log($"PlayerDisposableItem: '{item.displayName}' 지금은 사용할 수 없어 소모되지 않았습니다.");
            return;
        }

        if (effect.IsImmediate)
        {
            effect.ActivateImmediate(effectContext, sourceId);
            ConsumeFrontItem(item);
        }
        else
        {
            isPending = true;
            effect.BeginPending(effectContext, sourceId);
        }
    }

    // 지연형 효과가 실제 트리거 발생 시 DisposableEffectContext.CompletePendingUse를 거쳐
    // 호출합니다. sourceItemId가 지금 대기 중인(맨 앞) 아이템과 일치할 때만 실제로 소멸시킵니다 -
    // 이미 교체(CancelPending)돼서 대기가 끝난 뒤에 뒤늦게 트리거가 와도 무시됩니다.
    public void CompletePendingUse(string sourceItemId)
    {
        ItemDefinition front = EquippedItem;
        if (!isPending || front == null || front.itemId != sourceItemId) return;
        isPending = false;
        ConsumeFrontItem(front);
    }

    // [56번 연동] 지연형 효과가 스스로 사용을 취소할 때 DisposableEffectContext.CancelPendingUse를 거쳐 호출합니다(예: 56번 창에서
    // [취소하기]). 대기만 풀고 아이템은 소모하지 않습니다(맨 앞 칸에 그대로 - 다시 Q로 쓸 수 있음). 효과 쪽이 이미 스스로 정리하고
    // 부르는 것이라 CancelPending()은 부르지 않습니다. sourceItemId가 지금 대기 중인(맨 앞) 아이템과 다르면 무시합니다.
    public void CancelPendingUse(string sourceItemId)
    {
        ItemDefinition front = EquippedItem;
        if (!isPending || front == null || front.itemId != sourceItemId) return;
        isPending = false;
        Debug.Log($"PlayerDisposableItem: '{front.displayName}' 사용이 취소되어 소모되지 않았습니다.");
    }

    // newItem 장착을 시도합니다. 빈칸이 있으면 맨 뒤에 추가하고 displacedItem = null, 칸이 꽉 차 있으면 가장 먼저 얻은
    // 아이템을 빼서(대기 중이었다면 먼저 CancelPending으로 대기 취소) displacedItem으로 돌려주고, 새 아이템을 맨 뒤에 넣습니다.
    // 이미 같은 아이템을 장착 중이면(정상적으로는 일어나지 않음 - 상자/상점이 장착 중인 아이템을 내용물에서 뺌) false.
    public bool TryAcquireItem(ItemDefinition newItem, out ItemDefinition displacedItem)
    {
        displacedItem = null;
        if (newItem == null) return false;

        if (IsEquipped(newItem))
        {
            Debug.LogWarning($"PlayerDisposableItem: '{newItem.displayName}'은(는) 이미 장착 중이라 장착하지 않았습니다.");
            return false;
        }

        if (equippedItems.Count >= SlotCount && equippedItems.Count > 0)
        {
            displacedItem = equippedItems[0];
            if (isPending)
            {
                displacedItem?.disposableEffect?.CancelPending(effectContext, displacedItem.itemId);
                isPending = false;
            }
            equippedItems.RemoveAt(0);
        }

        equippedItems.Add(newItem);
        ItemCodexRecord.MarkAcquired(newItem); // [도감] 처음 인벤토리에 들어온 아이템이면 도감 기록에 남깁니다(테스트 씬 제외 - ItemCodexRecord.cs).

        // [17번 연동 - 획득 시 효과] 획득만 해도 무언가 주는 일회용(IDisposableAcquireEffect를 함께 구현한 효과)이면
        // 슬롯에 새로 들어온 지금 한 번 호출합니다. 구현하지 않은 일회용은 아무 일도 일어나지 않습니다.
        if (newItem.disposableEffect is IDisposableAcquireEffect acquireEffect)
        {
            acquireEffect.OnAcquired(effectContext, newItem.itemId);
        }

        Debug.Log(displacedItem == null
            ? $"PlayerDisposableItem: '{newItem.displayName}' 장착."
            : $"PlayerDisposableItem: '{newItem.displayName}' 장착 (기존 '{displacedItem.displayName}'은 방출됨).");

        return true;
    }

    // [슬롯 아이템 버리기 연동] 장착 중인 일회용 item을 빼냅니다(SlotItemDropper가 호출). 그 아이템이 지연 소모 대기
    // 중이었으면 교체와 같이 CancelPending()으로 대기를 먼저 취소합니다(효과는 발동하지 않음). 뒤의 아이템은 앞으로 당겨집니다.
    // 장착 중이 아니면 아무 일도 없이 false.
    public bool TryUnequip(ItemDefinition item, out ItemDefinition removedItem)
    {
        removedItem = null;
        if (item == null) return false;

        int index = equippedItems.IndexOf(item);
        if (index < 0) return false;

        if (index == 0 && isPending)
        {
            item.disposableEffect?.CancelPending(effectContext, item.itemId);
            isPending = false;
        }
        equippedItems.RemoveAt(index);
        removedItem = item;

        Debug.Log($"PlayerDisposableItem: '{item.displayName}' 장착 해제(버림).");
        return true;
    }

    // [v1.32에서 추가했다가 v1.33에서 제거] 한때는 여기서 "이 레벨에서 사용 완료됨"을
    // 기록했지만, 그 기준(실제로 사용/소멸된 시점)으로는 "상자에 뽑혔지만 줍지 않은
    // 아이템"이 다른 상자에서 또 나오는 문제를 막지 못했습니다. 이제는 그보다 훨씬 이른
    // 시점 - 보물상자가 이 아이템을 내용물로 "뽑는 그 순간"(ItemPedestal.Open()/
    // NormalRoomTreasureSpawner.ResolvePassiveItemCandidate())에 GameFlowManager.
    // MarkItemAssignedThisLevel()로 기록하므로, 여기서는 더 이상 손댈 필요가 없습니다.
    // 맨 앞 아이템이 소모되어 사라지고, 뒤의 아이템이 앞으로 당겨집니다.
    private void ConsumeFrontItem(ItemDefinition item)
    {
        if (equippedItems.Count == 0 || equippedItems[0] != item) return;
        Debug.Log($"PlayerDisposableItem: '{item.displayName}' 사용 후 소멸.");
        lastUsedDisposable = item; // [85번 연동]
        equippedItems.RemoveAt(0);

        // [사용 연출] 실제로 소모되는 순간 머리 위에 이 아이템 아이콘을 띄웁니다(등급 색 발광 - ItemUsePopup.cs 참고).
        ItemUsePopup.Show(gameObject, item);
    }

    // [85번 연동] 일회용 슬롯과 무관하게 일회용 효과를 재현할 때 쓸 컨텍스트를 만듭니다. 이 컨텍스트로 실행한 지연형 효과가
    // 끝났다(CompletePendingUse)/취소됐다(CancelPendingUse)고 알리면 이 슬롯이 아니라 넘겨준 콜백으로 신호가 갑니다.
    public DisposableEffectContext CreateReplayContext(System.Action<string> onPendingCompleted, System.Action<string> onPendingCancelled)
    {
        return new DisposableEffectContext(stats, health, resources, gameFlow, levelBuilder, gameObject,
                                           onPendingCompleted, onPendingCancelled);
    }

    // 이미 장착 중인 일회용 아이템(itemId 기준, 모든 칸)을 풀에서 제외한 목록을 반환합니다.
    // ItemPedestal/TreasureChest가 뽑기 직전에 다른 FilterOutEquipped들과 함께 호출해야
    // 하는 유틸입니다.
    public List<ItemDefinition> FilterOutEquipped(List<ItemDefinition> pool)
    {
        if (pool == null) return new List<ItemDefinition>();
        if (equippedItems.Count == 0) return pool;

        return pool
            .Where(item => item != null && !IsEquipped(item))
            .ToList();
    }
}
