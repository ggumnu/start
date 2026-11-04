using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 일회용 62번 - "사용하면 가진 패시브 아이템 하나를 골라, 한 등급 높은 랜덤한 패시브 아이템으로 교체한다. 3등급을 고르면 등급과 상관없이
// 랜덤한 패시브 아이템 두 개로 교체한다. 전투가 없는 방에서만 사용할 수 있다." (56번의 반대 방향)
//
// [기획 - 형빈 확정]
// - 사용(Q)하면 56번과 같은 교체 창(PassiveRerollUI)이 열립니다. 아이템을 하나 골라 [교체하기] -> 확인 창 [교체하기]를 누르면 교체되고 62번이
//   소모됩니다. [취소하기]/Esc(또는 창이 떠 있는 동안 피해)로 닫으면 아무 일도 없고 62번은 그대로 남습니다. 창의 문구도 56번과 같습니다.
// - 0/1/2등급을 고르면: 한 등급 높은 패시브 중 1개.
// - 3등급을 고르면: 모든 패시브(0~3등급) 중에서 같은 확률로 2개 - 아이템 수가 많은 등급이 더 자주 나오고, 운이 좋으면 3등급 2개도 가능합니다.
//   두 개는 서로 겹치지 않습니다. 후보가 1개뿐이면 1개만 줍니다.
// - 후보 공통(56번과 같음): Item Database의 모든 패시브 중, 이미 가진 것과 고른 아이템은 빼고, 이 레벨에서 이미 나온 아이템(보물상자/상점에 뜬 것)도
//   뺍니다. 교체 뒤에도 남는 보유 아이템과 같이 가질 수 없는 아이템(상극 - ItemCompatibility), 같이 받는 것끼리 상극인 조합도 나오지 않습니다.
//   등장 장소/가중치는 따지지 않는 완전 랜덤입니다.
// - 후보가 하나도 없는 아이템은 창에서 어둡게 보이고 고를 수 없습니다(예: 2등급을 골랐는데 3등급 후보가 다 떨어짐 - 다른 등급으로 대체하지 않음).
//   고를 수 있는 아이템이 아예 없으면 "교체할 수 있는 아이템이 없습니다" 창을 띄우고 소모되지 않습니다.
// - 고른 아이템은 56번/48번과 같은 규칙으로 잃고, 새 아이템은 "레벨당 최초 1회" 지급 제한 없이 줍니다. 새로 얻은 아이템마다 획득 연출을 순서대로 보여줍니다.
// - 전투 중인 방에서는 Q를 눌러도 아무 일도 일어나지 않습니다(창도 뜨지 않고 소모되지도 않음).
//
// [구현] 56번(PassiveDowngradeRerollEffect)과 같은 지연형 일회용 구조입니다(56번 파일은 수정하지 않음).
//
// [아이템 에셋 설정]
// ItemDefinition: Item Id = 62, Category = Disposable, Disposable Effect = PassiveUpgradeRerollEffect.
// Item Database 칸을 비워두면 LevelBuilder에 연결된 것을 씁니다(아이템 테스트 씬처럼 LevelBuilder가 없으면 채워주세요).
[System.Serializable]
public class PassiveUpgradeRerollEffect : IDisposableItemEffect
{
    [Tooltip("후보를 뽑을 아이템 목록입니다. 비워두면 LevelBuilder의 Item Database를 씁니다(테스트 씬에서는 채워주세요).")]
    public ItemDatabase itemDatabase;

    [Tooltip("0~2등급 아이템을 골랐을 때 얻는 한 등급 높은 아이템 수입니다.")]
    [Min(1)] public int rewardCount = 1;
    [Tooltip("3등급 아이템을 골랐을 때 얻는 랜덤 등급 아이템 수입니다.")]
    [Min(1)] public int highestTierRewardCount = 2;

    // 창을 띄우고 확정/취소를 기다리므로 지연형입니다.
    public bool IsImmediate => false;

    // 전투 중인 방이거나 창이 이미 떠 있으면 사용하지 않습니다(아무 창도 띄우지 않고 소모되지도 않음).
    public bool CanUse(DisposableEffectContext context)
    {
        return context != null && context.PlayerObject != null && !PassiveRerollUI.IsOpen &&
               SlotItemDropper.IsInCombatFreeRoom();
    }

    // 즉시형이 아니라 호출되지 않습니다.
    public void ActivateImmediate(DisposableEffectContext context, string sourceItemId) { }

    public void BeginPending(DisposableEffectContext context, string sourceItemId)
    {
        var settings = PassiveRerollSettings.Get();
        GameObject player = context.PlayerObject;

        var inventory = player.GetComponent<PlayerInventory>();
        var database = ResolveDatabase(context);
        var gameFlow = context.GameFlow;
        if (database == null)
        {
            Debug.LogWarning("PassiveUpgradeRerollEffect: Item Database를 찾지 못했습니다. 62번 아이템의 Disposable Effect에 " +
                             "Item Database를 넣어주세요(테스트 씬).");
        }

        // 보여줄 목록: 인벤토리 HUD와 같은 순서(최근 것이 앞). 고를 수 있는 것 = 후보가 하나라도 있는 것.
        var displayItems = new List<ItemDefinition>();
        var selectable = new HashSet<ItemDefinition>();
        if (inventory != null)
        {
            var owned = inventory.OwnedItems;
            for (int i = owned.Count - 1; i >= 0; i--)
            {
                var item = owned[i];
                if (item == null) continue;
                displayItems.Add(item);
                if (database != null && GetCandidates(item, inventory, database, gameFlow).Count > 0) selectable.Add(item);
            }
        }

        if (selectable.Count == 0)
        {
            PassiveRerollUI.ShowMessage(player, settings.noItemMessage);
            context.CancelPendingUse(sourceItemId);
            return;
        }

        bool opened = PassiveRerollUI.OpenSelection(player, displayItems, selectable,
            chosen =>
            {
                Execute(chosen, inventory, database, gameFlow);
                context.CompletePendingUse(sourceItemId);
            },
            () => context.CancelPendingUse(sourceItemId));

        if (!opened) context.CancelPendingUse(sourceItemId);
    }

    // 대기 중에 다른 이유로 취소됐을 때(교체/버리기 등): 창만 닫습니다(콜백 없음).
    public void CancelPending(DisposableEffectContext context, string sourceItemId)
    {
        PassiveRerollUI.ForceClose();
    }

    // 고른 아이템을 잃고, 보상을 뽑아 순서대로 얻습니다.
    private void Execute(ItemDefinition chosen, PlayerInventory inventory, ItemDatabase database, GameFlowManager gameFlow)
    {
        if (chosen == null || inventory == null || database == null) return;
        if (!inventory.OwnedItems.Contains(chosen)) return; // 그 사이 이미 잃었으면 아무것도 하지 않음

        var candidates = GetCandidates(chosen, inventory, database, gameFlow);
        int count = chosen.rarity == ItemDefinition.ItemRarity.Tier3 ? highestTierRewardCount : rewardCount;

        // 서로 겹치지 않게 같은 확률로 뽑기(섞은 뒤 앞에서부터)
        for (int i = candidates.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            var tmp = candidates[i];
            candidates[i] = candidates[j];
            candidates[j] = tmp;
        }

        // [상극 아이템] 앞에서부터 고르되, 이미 고른 보상과 같이 가질 수 없는 아이템은 건너뜁니다.
        var rewards = new List<ItemDefinition>();
        foreach (var candidate in candidates)
        {
            if (rewards.Count >= count) break;
            if (ItemCompatibility.ConflictsWithAny(candidate, rewards)) continue;
            rewards.Add(candidate);
        }
        if (rewards.Count == 0) return;

        inventory.RemoveItem(chosen);

        var acquired = new List<ItemDefinition>();
        foreach (var reward in rewards)
        {
            // 56번/48번과 같이 "레벨당 최초 1회" 지급 제한 없이 줍니다.
            if (inventory.TryAcquireItem(reward, true)) acquired.Add(reward);
        }

        Debug.Log($"PassiveUpgradeRerollEffect: '{chosen.displayName}' -> " +
                  string.Join(", ", acquired.Select(item => item.displayName)));

        PassiveRerollUI.RaisePickupsInSequence(acquired);
    }

    // 고른 아이템의 교체 후보: 0~2등급이면 한 등급 높은 패시브, 3등급이면 모든 등급의 패시브. 가지지 않은 것(고른 아이템 제외), 남는 보유 아이템과
    // 상극이 아닌 것, 이 레벨에서 아직 나오지 않은 것(상자/상점), 겹침 없이.
    private static List<ItemDefinition> GetCandidates(ItemDefinition chosen, PlayerInventory inventory, ItemDatabase database,
                                                      GameFlowManager gameFlow)
    {
        var result = new List<ItemDefinition>();
        if (chosen == null || database == null || database.allItems == null) return result;

        bool anyTier = chosen.rarity == ItemDefinition.ItemRarity.Tier3;
        ItemDefinition.ItemRarity targetTier = anyTier
            ? ItemDefinition.ItemRarity.Tier3
            : (ItemDefinition.ItemRarity)((int)chosen.rarity + 1);

        var remaining = ItemCompatibility.CollectOwned(inventory != null ? inventory.gameObject : null);
        remaining.Remove(chosen);
        var seen = new HashSet<string>();
        foreach (var item in database.allItems)
        {
            if (item == null || item == chosen) continue;
            if (item.category != ItemDefinition.ItemCategory.Passive) continue;
            if (!anyTier && item.rarity != targetTier) continue;
            if (inventory != null && inventory.HasItem(item.itemId)) continue;
            if (!string.IsNullOrEmpty(chosen.itemId) && item.itemId == chosen.itemId) continue;
            // [상극 아이템] 교체 뒤에도 남는 아이템(고른 아이템 제외)과 같이 가질 수 없으면 제외합니다.
            if (ItemCompatibility.ConflictsWithAny(item, remaining)) continue;

            string key = string.IsNullOrEmpty(item.itemId) ? item.name : item.itemId;
            if (!seen.Add(key)) continue;
            result.Add(item);
        }

        // 이 레벨에서 이미 나온 아이템(보물상자를 열어 뜬 아이템, 상점에 진열됐던 아이템)은 뺍니다.
        if (gameFlow != null) result = gameFlow.FilterOutAlreadyAssignedThisLevel(result);
        return result;
    }

    private ItemDatabase ResolveDatabase(DisposableEffectContext context)
    {
        if (itemDatabase != null) return itemDatabase;
        return context != null && context.LevelBuilder != null ? context.LevelBuilder.itemDatabase : null;
    }
}
