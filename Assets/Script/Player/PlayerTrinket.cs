using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 플레이어가 장착 중인 "장신구"를 관리합니다. PlayerInventory(패시브, 무제한 보유)/
// PlayerActiveItem(액티브, 단일 슬롯 + 사용)과 완전히 별도의 저장소입니다.
//
// [장신구 = 패시브 + 액티브를 섞은 성격]
// - 액티브처럼: 장착할 수 있는 칸 수가 정해져 있고(기본 1칸), 칸이 꽉 찬 상태에서 다른 장신구를 획득하면
//   기존 장신구와 "교체"됩니다(획득 X, 교체 O) - PlayerActiveItem.TryAcquireItem과 동일한 계약입니다.
// - 패시브처럼: 장착하고 있는 동안 효과가 계속 발휘됩니다(사용 개념이 없습니다 - 스페이스
//   바로 "쓰는" 게 아니라 갖고만 있으면 됩니다). 여러 개를 끼면 모두의 효과를 받습니다.
//
// [교체 시 이전 효과를 반드시 해제]
// 패시브 아이템은 한 번 걸리면 영구적이라 해제할 필요가 없었지만, 장신구는 교체되므로
// ITrinketEffect.Remove()를 반드시 호출해 이전 장신구가 걸어둔 효과(스탯 수정자 등)를
// 되돌립니다 - 안 그러면 장신구를 바꿔도 이전 효과가 영구히 남습니다.
//
// [장신구 칸 늘리기 - 55번 (형빈 확정)]
// 칸 수 = 기본 1칸 + AddExtraSlots로 등록된 추가 칸(55번이 +1). 장착 순서는 "먼저 얻은 것이 앞"입니다.
// - 빈칸이 있으면 새 장신구를 맨 뒤에 추가합니다(밀려나는 장신구 없음).
// - 칸이 꽉 차 있으면 맨 앞(가장 먼저 얻은) 장신구의 효과를 해제하고 빼낸 뒤, 나머지를 앞으로 당기고 새 장신구를 맨 뒤에
//   넣습니다. 예) [A, B]에서 C 획득 -> A가 밀려나고 [B, C]. 다시 A 획득 -> B가 밀려나고 [C, A].
// - 교체 확인창은 "지금 얻으면 밀려날 장신구"(ReplacedOnAcquire - 빈칸이 있으면 없음) 기준으로 뜹니다.
// - 칸이 줄어들어(55번을 잃음) 장착 수가 칸 수보다 많아지면, 그 프레임 끝(LateUpdate)에 나중에 얻은 장신구부터 효과를 해제하고
//   지금 방 바닥에 버린 아이템처럼 좌대로 떨어뜨립니다(SlotItemDropper.DropNearPlayer). 프레임 끝에 판정하므로 48번이 55번을
//   다시 55번으로 바꾸는 경우(잃었다가 같은 프레임에 다시 얻음)에는 아무것도 떨어지지 않습니다.
[RequireComponent(typeof(PlayerStats))]
public class PlayerTrinket : MonoBehaviour
{
    // 기본 장신구 칸 수
    public const int BaseSlotCount = 1;

    private PlayerStats stats;
    private PlayerHealth health;
    private PlayerShooting shooting;
    private PlayerResources resources;
    private GameFlowManager gameFlow;
    private TrinketEffectContext effectContext;

    // 장착 중인 장신구(먼저 얻은 것이 앞)
    private readonly List<ItemDefinition> equippedItems = new List<ItemDefinition>();

    // 추가 칸 등록(출처 이름 -> 칸 수). 55번 등
    private readonly Dictionary<string, int> extraSlots = new Dictionary<string, int>();

    // 장착 중인 장신구 전체(먼저 얻은 것이 앞). 읽기 전용.
    public IReadOnlyList<ItemDefinition> EquippedItems => equippedItems;

    // 장착 중인 장신구 수
    public int EquippedCount => equippedItems.Count;

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

    // index번째 칸의 장신구(없으면 null). 0 = 가장 먼저 얻은 장신구.
    public ItemDefinition GetEquippedAt(int index)
    {
        return index >= 0 && index < equippedItems.Count ? equippedItems[index] : null;
    }

    // 지금 새 장신구를 얻으면 밀려날 장신구입니다. 빈칸이 있으면 null(바로 추가됨), 꽉 찼으면 가장 먼저 얻은 장신구.
    // 교체 확인창/상점의 보관 좌대 필요 여부 판단에 씁니다.
    public ItemDefinition ReplacedOnAcquire =>
        equippedItems.Count >= SlotCount && equippedItems.Count > 0 ? equippedItems[0] : null;

    // 이 장신구를 장착 중인지(itemId 기준)
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
        shooting = GetComponent<PlayerShooting>();
        resources = GetComponent<PlayerResources>();
        gameFlow = Object.FindFirstObjectByType<GameFlowManager>();

        if (health == null)
        {
            Debug.LogWarning("PlayerTrinket: 같은 오브젝트에서 PlayerHealth를 찾지 못했습니다. " +
                              "체력 관련 장신구 효과는 정상 동작하지 않을 수 있습니다.");
        }
        if (resources == null)
        {
            Debug.LogWarning("PlayerTrinket: 같은 오브젝트에서 PlayerResources를 찾지 못했습니다. " +
                              "재화 지급 관련 장신구 효과는 정상 동작하지 않을 수 있습니다.");
        }
        if (gameFlow == null)
        {
            Debug.LogWarning("PlayerTrinket: GameFlowManager를 찾지 못했습니다. " +
                              "레벨당 1회 획득값 중복 방지가 정상 동작하지 않을 수 있습니다.");
        }

        effectContext = new TrinketEffectContext(stats, health, shooting, resources, gameFlow, gameObject);

        // 사망 처리로 컴포넌트가 꺼져 있어도 받도록 Awake/OnDestroy에서 구독.
        LoadingScreen.OnBeforeSceneChange += ReleaseEffectsForSceneChange;
    }

    void OnDestroy()
    {
        LoadingScreen.OnBeforeSceneChange -= ReleaseEffectsForSceneChange;
    }

    // [판 정리] 씬이 바뀌기 직전에 장착 중인 장신구 효과를 전부 Remove()로 되돌립니다(PlayerInventory와 같은 이유).
    private void ReleaseEffectsForSceneChange()
    {
        foreach (var item in equippedItems)
        {
            if (item == null || item.trinketEffect == null) continue;
            try { item.trinketEffect.Remove(effectContext, item.itemId); }
            catch (System.Exception e)
            {
                Debug.LogError($"PlayerTrinket: '{item.displayName}' 효과를 판 정리 중 되돌리다 오류가 나 건너뜁니다: {e}");
            }
        }
    }

    // [칸이 줄었을 때] 장착 수가 칸 수보다 많으면 나중에 얻은 장신구부터 효과를 해제하고 바닥에 떨어뜨립니다(형빈 확정).
    // 프레임 끝에 판정해서, 같은 프레임 안에 칸이 줄었다가 다시 늘어난 경우(48번이 55번을 55번으로 바꿈)는 무시됩니다.
    void LateUpdate()
    {
        int slotCount = SlotCount;
        while (equippedItems.Count > slotCount && equippedItems.Count > 0)
        {
            int last = equippedItems.Count - 1;
            ItemDefinition overflow = equippedItems[last];
            equippedItems.RemoveAt(last);
            if (overflow == null) continue;

            overflow.trinketEffect?.Remove(effectContext, overflow.itemId);
            Debug.Log($"PlayerTrinket: 장신구 칸이 줄어 '{overflow.displayName}'을(를) 바닥에 떨어뜨립니다.");
            SlotItemDropper.DropNearPlayer(gameObject, overflow);
        }
    }

    // 장신구 칸을 amount만큼 늘립니다(sourceId별로 하나만 - 같은 이름으로 다시 부르면 덮어씀).
    public void AddExtraSlots(string sourceId, int amount)
    {
        if (string.IsNullOrEmpty(sourceId)) return;
        extraSlots[sourceId] = amount;
    }

    // AddExtraSlots로 늘린 칸을 되돌립니다. 넘치는 장신구는 그 프레임 끝(LateUpdate)에 떨어집니다.
    public void RemoveExtraSlots(string sourceId)
    {
        if (string.IsNullOrEmpty(sourceId)) return;
        extraSlots.Remove(sourceId);
    }

    // newItem 장착을 시도합니다. 빈칸이 있으면 맨 뒤에 추가하고 displacedItem = null, 칸이 꽉 차 있으면 가장 먼저 얻은
    // 장신구의 효과를 해제(Remove)해 빼낸 뒤 displacedItem으로 돌려주고, 새 장신구를 맨 뒤에 넣습니다. 이미 같은 장신구를
    // 장착 중이면(정상적으로는 일어나지 않음 - 상자/상점이 장착 중인 장신구를 내용물에서 뺌) 아무것도 하지 않고 false.
    public bool TryAcquireItem(ItemDefinition newItem, out ItemDefinition displacedItem)
    {
        displacedItem = null;
        if (newItem == null) return false;

        if (IsEquipped(newItem))
        {
            Debug.LogWarning($"PlayerTrinket: '{newItem.displayName}'은(는) 이미 장착 중이라 장착하지 않았습니다.");
            return false;
        }

        if (equippedItems.Count >= SlotCount && equippedItems.Count > 0)
        {
            displacedItem = equippedItems[0];
            equippedItems.RemoveAt(0);
            displacedItem?.trinketEffect?.Remove(effectContext, displacedItem.itemId);
        }

        equippedItems.Add(newItem);
        ItemCodexRecord.MarkAcquired(newItem); // [도감] 처음 인벤토리에 들어온 아이템이면 도감 기록에 남깁니다(테스트 씬 제외 - ItemCodexRecord.cs).
        newItem.trinketEffect?.Apply(effectContext, newItem.itemId);

        Debug.Log(displacedItem == null
            ? $"PlayerTrinket: '{newItem.displayName}' 장착."
            : $"PlayerTrinket: '{newItem.displayName}' 장착 (기존 '{displacedItem.displayName}'은 방출됨).");

        return true;
    }

    // [슬롯 아이템 버리기 연동] 장착 중인 장신구 item을 빼냅니다(SlotItemDropper가 호출). 교체와 같이 Remove()로 효과를 먼저
    // 해제한 뒤 칸에서 뺍니다(뒤의 장신구는 앞으로 당겨짐). 장착 중이 아니면 아무 일도 없이 false.
    public bool TryUnequip(ItemDefinition item, out ItemDefinition removedItem)
    {
        removedItem = null;
        if (item == null) return false;

        int index = equippedItems.IndexOf(item);
        if (index < 0) return false;

        removedItem = item;
        equippedItems.RemoveAt(index);
        item.trinketEffect?.Remove(effectContext, item.itemId);

        Debug.Log($"PlayerTrinket: '{item.displayName}' 장착 해제(버림).");
        return true;
    }

    // 이미 장착 중인 장신구(itemId 기준, 모든 칸)를 풀에서 제외한 목록을 반환합니다. ItemPedestal/
    // TreasureChest가 뽑기 직전에 PlayerInventory.FilterUnowned()/PlayerActiveItem.
    // FilterOutEquipped()와 함께 호출해야 하는 유틸입니다.
    public List<ItemDefinition> FilterOutEquipped(List<ItemDefinition> pool)
    {
        if (pool == null) return new List<ItemDefinition>();
        if (equippedItems.Count == 0) return pool;

        return pool
            .Where(item => item != null && !IsEquipped(item))
            .ToList();
    }
}
