using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 플레이어가 보유한 패시브 아이템 목록을 관리합니다.
// - 중복 획득 방지: 이미 가진 itemId는 다시 획득할 수 없습니다.
// - 아이템 획득 시 ItemDefinition.GetEffects()를 통해 각 효과에 PassiveEffectContext를
//   전달해 적용시킵니다 (스탯형은 즉시 적용, 특수효과형은 필요한 이벤트에 구독을 겁니다 -
//   PassiveEffectContext.cs/IPassiveItemEffect.cs 참고).
// - FilterUnowned()는 동아리방/보스방/상점 등 "어디서 아이템이 등장하든" 공통으로 쓸
//   수 있는 유틸입니다. 아이템 풀에서 뽑기 전에 항상 이 메서드로 먼저 걸러내면
//   보유 중인 아이템이 다시 뽑히는 일이 없습니다.
[RequireComponent(typeof(PlayerStats))]
public class PlayerInventory : MonoBehaviour
{
    private PlayerStats stats;
    private PassiveEffectContext effectContext;
    private readonly HashSet<string> ownedItemIds = new HashSet<string>();
    private readonly List<ItemDefinition> ownedItems = new List<ItemDefinition>();

    public IReadOnlyList<ItemDefinition> OwnedItems => ownedItems;

    // [76번 연동] 지금 보유한 패시브 아이템 개수입니다(액티브/장신구/음식/일회용은 각자 다른 슬롯 컴포넌트라 포함되지 않음).
    public int PassiveCount => ownedItems.Count;

    // [76번 연동] 보유 패시브 목록이 바뀐 "직후"에 한 번 알립니다 - 획득(TryAcquireItem), 잃기(RemoveItem), 교체(ReplaceItems).
    // 48번 교체는 "전부 잃기 -> 전부 얻기" 중간에 개수가 출렁이므로, 교체가 모두 끝난 뒤 딱 한 번만 알립니다.
    // 보유 패시브 개수에 따라 값이 바뀌는 효과(76번 등)가 구독해서 다시 계산합니다. 나중에 생길 "패시브 잃기" 효과도
    // RemoveItem/ReplaceItems를 쓰면 자동으로 알림이 갑니다.
    public event System.Action OnPassiveItemsChanged;

    void Awake()
    {
        stats = GetComponent<PlayerStats>();

        // PlayerShooting은 발사/탄환 관련 훅(OnProjectileSpawned 등)을 쓰는 특수효과
        // 아이템에게만 필요하지만, 없어도 컨텍스트 생성 자체는 막지 않습니다(스탯형 아이템만
        // 쓰는 테스트 플레이어 등을 배려) - 대신 나중에 그런 아이템을 얻었을 때 조용히
        // 아무 일도 안 일어나면 원인 파악이 어려우니 경고만 남깁니다.
        var shooting = GetComponent<PlayerShooting>();
        if (shooting == null)
        {
            Debug.LogWarning("PlayerInventory: 같은 오브젝트에서 PlayerShooting을 찾지 못했습니다. " +
                              "발사/탄환 관련 훅을 쓰는 특수효과 아이템은 정상 동작하지 않을 수 있습니다.");
        }

        // PlayerResources는 최대 코스트(maxCost) 등 재화를 직접 건드리는 특수효과 아이템
        // (24번 등)에게만 필요하지만, 없어도 컨텍스트 생성 자체는 막지 않습니다 - 대신
        // 나중에 그런 아이템을 얻었을 때 원인 파악이 어려우니 경고만 남깁니다.
        var resources = GetComponent<PlayerResources>();
        if (resources == null)
        {
            Debug.LogWarning("PlayerInventory: 같은 오브젝트에서 PlayerResources를 찾지 못했습니다. " +
                              "코스트/재화 관련 특수효과 아이템은 정상 동작하지 않을 수 있습니다.");
        }

        // PlayerHealth는 현재 체력(CurrentHealth)을 읽어야 하는 특수효과 아이템(17번 등)에게만
        // 필요하지만, 없어도 컨텍스트 생성 자체는 막지 않습니다 - 위 shooting/resources와 동일한
        // 이유로 경고만 남깁니다.
        var health = GetComponent<PlayerHealth>();
        if (health == null)
        {
            Debug.LogWarning("PlayerInventory: 같은 오브젝트에서 PlayerHealth를 찾지 못했습니다. " +
                              "현재 체력을 참조하는 특수효과 아이템은 정상 동작하지 않을 수 있습니다.");
        }

        effectContext = new PassiveEffectContext(stats, shooting, this, resources, health, gameObject);
    }

    public bool HasItem(string itemId)
    {
        return !string.IsNullOrEmpty(itemId) && ownedItemIds.Contains(itemId);
    }

    // 아이템 획득을 시도합니다. 이미 보유 중이면 아무 효과도 적용하지 않고 false를 반환합니다
    // (치트/명령어로 우회하는 경우를 제외하면, 정상 플레이에서는 이 체크로 중복 획득이
    // 원천 차단됩니다).
    public bool TryAcquireItem(ItemDefinition item)
    {
        return TryAcquireItem(item, false);
    }

    // ignoreOneTimeGrantLimit: true면 이 아이템의 "레벨당 1회" 1회성 지급 제한을 풉니다(56번 - 48번과 같은 규칙).
    public bool TryAcquireItem(ItemDefinition item, bool ignoreOneTimeGrantLimit)
    {
        if (item == null) return false;

        if (string.IsNullOrEmpty(item.itemId))
        {
            Debug.LogWarning($"PlayerInventory: '{item.name}' 아이템의 itemId가 비어있습니다. " +
                              "중복 획득 방지가 정상 동작하지 않으니 인스펙터에서 itemId를 채워주세요.");
        }
        else if (ownedItemIds.Contains(item.itemId))
        {
            Debug.LogWarning($"PlayerInventory: 이미 '{item.itemId}' 아이템을 보유하고 있어 다시 획득하지 않았습니다.");
            return false;
        }

        // [상극 아이템 - 99번 때 추가] 가진 아이템과 같이 가질 수 없는 아이템이면 얻지 않습니다(아이템은 그 자리에 그대로 남음).
        // 정상적으로는 등장 단계에서 이미 빠지므로 일어나지 않는 안전장치입니다(ItemCompatibility.cs 참고).
        if (ItemCompatibility.ConflictsWithAny(item, ItemCompatibility.CollectOwned(gameObject)))
        {
            Debug.LogWarning($"PlayerInventory: '{item.displayName}'은(는) 가지고 있는 아이템과 같이 가질 수 없어 획득하지 않았습니다.");
            return false;
        }

        effectContext.IgnoreOneTimeGrantLimit = ignoreOneTimeGrantLimit;
        try
        {
            foreach (var effect in item.GetEffects())
            {
                effect.Apply(effectContext, item.itemId);
            }
        }
        finally
        {
            effectContext.IgnoreOneTimeGrantLimit = false;
        }

        ownedItems.Add(item);
        if (!string.IsNullOrEmpty(item.itemId)) ownedItemIds.Add(item.itemId);
        ItemCodexRecord.MarkAcquired(item); // [도감] 처음 인벤토리에 들어온 아이템이면 도감 기록에 남깁니다(테스트 씬 제외 - ItemCodexRecord.cs).

        Debug.Log($"PlayerInventory: '{item.displayName}' 획득.");
        OnPassiveItemsChanged?.Invoke(); // [76번 연동]
        return true;
    }

    // ---------------- [패시브 잃기 / 교체 - 48번 등] ----------------
    // 패시브를 잃을 때의 규칙(형빈 확정)은 IRemovablePassiveEffect.cs 상단 주석에 정리돼 있습니다. 요약:
    // - 기본 스탯(Stat Modifiers)은 최대 체력만 남기고 사라집니다.
    // - 특수 효과는 각 효과의 Remove()로 되돌립니다(특수 효과로 얻은 능력치/1회성 재화는 남음).

    // 보유 중인 패시브 하나를 잃습니다. 보유하지 않은 아이템이면 false를 반환합니다. 나중에 생길 다른 "아이템 잃기"
    // 효과도 이 메서드를 쓰면 됩니다.
    public bool RemoveItem(ItemDefinition item)
    {
        if (item == null) return false;

        int index = ownedItems.IndexOf(item);
        if (index < 0) return false;

        RemoveEffectsOf(item);
        ownedItems.RemoveAt(index);
        RebuildOwnedIds();

        Debug.Log($"PlayerInventory: '{item.displayName}' 잃음.");
        OnPassiveItemsChanged?.Invoke(); // [76번 연동]
        return true;
    }

    // [48번] 보유 패시브를 한꺼번에 교체합니다. replacements[i]는 OwnedItems[i] 자리에 들어갈 새 아이템이고, null이면
    // 그 자리는 손대지 않고 그대로 둡니다(굴릴 후보가 없을 때). 개수가 OwnedItems와 다르면 아무것도 하지 않습니다.
    //
    // [처리 순서 - 잃기 전부 → 얻기 전부]
    // 교체되는 아이템을 먼저 전부 잃은 뒤 새 아이템을 얻습니다. 그래야 A,B,C → B,C,A처럼 보유 중이던 아이템이 다른
    // 자리로 다시 나와도 "이미 보유 중"에 걸리지 않고, 특수 효과도 한 번만 걸립니다(형빈 확정 - 48번으로 나온 아이템은
    // 원래 가졌던 아이템이어도 전부 새로 얻은 것으로 취급). 새 아이템은 원래 아이템이 있던 칸에 그대로 들어갑니다.
    //
    // ignoreOneTimeGrantLimit: true면 새 아이템의 "레벨당 1회" 1회성 지급 제한을 풉니다(48번은 true).
    public bool ReplaceItems(IReadOnlyList<ItemDefinition> replacements, bool ignoreOneTimeGrantLimit)
    {
        if (replacements == null || replacements.Count != ownedItems.Count)
        {
            Debug.LogWarning("PlayerInventory: 교체할 아이템 개수가 보유 패시브 개수와 달라 교체하지 않았습니다.");
            return false;
        }

        // [상극 아이템] 교체 결과 안에 같이 가질 수 없는 아이템끼리 있으면(호출한 쪽의 실수) 교체하지 않습니다.
        var resultItems = new List<ItemDefinition>();
        for (int i = 0; i < ownedItems.Count; i++) resultItems.Add(replacements[i] != null ? replacements[i] : ownedItems[i]);
        for (int i = 0; i < resultItems.Count; i++)
        {
            for (int j = i + 1; j < resultItems.Count; j++)
            {
                if (ItemCompatibility.AreIncompatible(resultItems[i], resultItems[j]))
                {
                    Debug.LogWarning($"PlayerInventory: 교체 결과에 같이 가질 수 없는 '{resultItems[i].displayName}'/'{resultItems[j].displayName}'가 " +
                                     "함께 있어 교체하지 않았습니다.");
                    return false;
                }
            }
        }

        // 결과 안에 같은 아이템이 두 번 들어있으면(호출한 쪽의 실수) 교체하지 않습니다.
        var resultIds = new HashSet<string>();
        for (int i = 0; i < ownedItems.Count; i++)
        {
            ItemDefinition result = replacements[i] != null ? replacements[i] : ownedItems[i];
            string key = string.IsNullOrEmpty(result.itemId) ? result.name : result.itemId;
            if (!resultIds.Add(key))
            {
                Debug.LogWarning($"PlayerInventory: 교체 결과에 '{key}'가 중복되어 교체하지 않았습니다.");
                return false;
            }
        }

        // 1) 교체되는 자리의 원래 아이템을 전부 잃습니다.
        for (int i = 0; i < ownedItems.Count; i++)
        {
            if (replacements[i] == null) continue;
            RemoveEffectsOf(ownedItems[i]);
        }

        // 2) 새 아이템을 같은 자리에 넣고 효과를 적용합니다.
        effectContext.IgnoreOneTimeGrantLimit = ignoreOneTimeGrantLimit;
        try
        {
            for (int i = 0; i < ownedItems.Count; i++)
            {
                ItemDefinition newItem = replacements[i];
                if (newItem == null) continue;

                ItemDefinition oldItem = ownedItems[i];
                ownedItems[i] = newItem;
                ItemCodexRecord.MarkAcquired(newItem); // [도감] 처음 인벤토리에 들어온 아이템이면 도감 기록에 남깁니다(테스트 씬 제외 - ItemCodexRecord.cs).
                RebuildOwnedIds();

                foreach (var effect in newItem.GetEffects())
                {
                    effect.Apply(effectContext, newItem.itemId);
                }

                Debug.Log($"PlayerInventory: '{oldItem.displayName}' -> '{newItem.displayName}' 교체.");
            }
        }
        finally
        {
            effectContext.IgnoreOneTimeGrantLimit = false;
            RebuildOwnedIds();
        }

        OnPassiveItemsChanged?.Invoke(); // [76번 연동] 교체가 전부 끝난 뒤 한 번만
        return true;
    }

    // 아이템 하나의 효과를 되돌립니다(목록에서 빼는 건 호출한 쪽이 합니다).
    private void RemoveEffectsOf(ItemDefinition item)
    {
        if (item == null) return;

        // 기본 스탯: 최대 체력만 남기고 제거합니다. 특수 효과로 얻은 능력치는 출처 이름이 달라서(아이템 ID + "_effect")
        // 여기에 휩쓸리지 않습니다.
        if (stats != null && !string.IsNullOrEmpty(item.itemId))
        {
            stats.RemoveModifiersFromSourceExcept(item.itemId, StatType.MaxHealth);
        }

        foreach (var effect in item.GetEffects())
        {
            if (effect == null || effect is StatModifierEffect) continue;

            if (effect is IRemovablePassiveEffect removable)
            {
                removable.Remove(effectContext, item.itemId);
            }
            else
            {
                Debug.LogWarning($"PlayerInventory: '{item.displayName}'의 특수 효과 {effect.GetType().Name}에 제거 기능" +
                                  "(IRemovablePassiveEffect)이 없어, 아이템을 잃어도 이 효과가 남을 수 있습니다.");
            }
        }
    }

    private void RebuildOwnedIds()
    {
        ownedItemIds.Clear();
        foreach (var owned in ownedItems)
        {
            if (owned != null && !string.IsNullOrEmpty(owned.itemId)) ownedItemIds.Add(owned.itemId);
        }
    }

    // 전달받은 풀에서 이미 보유한 아이템(itemId 기준)을 제외한 목록을 반환합니다.
    // ItemPedestal/상점/보스방 드랍 등 아이템이 등장하는 모든 곳이 뽑기 직전에
    // 공통으로 호출해야 하는 유틸입니다.
    // [상극 아이템 - 99번 때 추가] 가지고 있는 아이템(패시브 + 액티브/장신구/일회용/음식 칸)과 같이 가질 수 없는 아이템도 뺍니다.
    public List<ItemDefinition> FilterUnowned(List<ItemDefinition> pool)
    {
        if (pool == null) return new List<ItemDefinition>();

        var owned = ItemCompatibility.CollectOwned(gameObject);
        return pool
            .Where(item => item != null && !HasItem(item.itemId) && !ItemCompatibility.ConflictsWithAny(item, owned))
            .ToList();
    }
}
