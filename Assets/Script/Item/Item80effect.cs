using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 일회용 80번 - "사용하면 랜덤 등급의 패시브 아이템 1~3개를 얻는다. 전투가 없는 방에서만 사용할 수 있다."
//
// [기획 - 형빈 확정]
// - 개수: 1개/2개/3개를 countWeights(기본 50/30/20) 가중치로 정합니다. 즉 1개 50%, 2개 30%, 3개 20%.
//   (나중에 행운 스탯으로 이 확률을 바꿀 예정 - 지금은 보류)
// - 후보: Item Database의 패시브 중
//   1) 등장 위치(appearanceContexts)에 일반방/보스방/상점(편의점·카페 등 variant 포함)/동아리방 중 하나라도 있는 것
//      (allowedRoomCategories - 특수방 전용/선택방 전용 아이템은 나오지 않음),
//   2) 이미 가진 것이 아닌 것,
//   3) 이 레벨에서 이미 나온 것이 아닌 것(보물상자를 열어 뜬 아이템, 상점에 진열됐던 아이템 - 줍거나 사지 않았어도. 보물상자/
//      상점/48번/56번과 같은 GameFlowManager 기록),
//   4) 가중치가 0보다 큰 것.
// - 뽑기: 등급을 따로 굴리지 않고, 보물상자와 같은 아이템별 등장 가중치(ItemWeightModifier.GetWeight - 에셋 weight + 보정)로
//   한 개씩 뽑습니다. 뽑힌 것은 빼고 다시 뽑으므로 서로 겹치지 않습니다. 후보가 모자라면 있는 만큼만 줍니다.
// - 지급: 보물상자에서 새로 얻은 것과 같은 경로(GameFlowManager.AcquireItemCopy - 새 사본)로 줍니다. 그래서 57번/77번 같은
//   "획득 시 지급"도 정상적으로 받습니다. 준 아이템은 이 레벨에서 나온 아이템으로 기록해 이후 상자/상점에 다시 나오지 않습니다.
//   받은 아이템마다 획득 연출을 순서대로 보여줍니다(56번과 같음).
// - 전투 중인 방에서는 Q를 눌러도 아무 일도 일어나지 않습니다(창도 뜨지 않고 소모되지도 않음 - 56번과 같음).
// - 후보가 하나도 없으면 이유를 알리는 창(noItemMessage)을 띄우고 소모하지 않습니다.
//
// [구현 - 지연형 일회용 구조를 활용]
// 후보가 없을 때 "소모하지 않고 창만 띄우기"를 위해 56번과 같이 지연형(IsImmediate = false)으로 만들었습니다. Q를 누르면
// BeginPending에서 바로 판정해서, 지급했으면 CompletePendingUse(소모), 후보가 없으면 CancelPendingUse(그대로 남음)를 부릅니다.
// 대기 상태는 같은 프레임 안에 끝나므로 화면에 보이지 않습니다.
//
// [아이템 에셋 설정]
// ItemDefinition: Item Id = 80, Category = Disposable, Disposable Effect = RandomPassiveGrantEffect.
// Item Database 칸을 비워두면 LevelBuilder에 연결된 것을 씁니다(아이템 테스트 씬처럼 LevelBuilder가 없으면 채워주세요).
[System.Serializable]
public class RandomPassiveGrantEffect : IDisposableItemEffect
{
    [Tooltip("후보를 뽑을 아이템 목록입니다. 비워두면 LevelBuilder의 Item Database를 씁니다(테스트 씬에서는 채워주세요).")]
    public ItemDatabase itemDatabase;

    [Tooltip("얻는 개수의 가중치입니다. 첫 칸 = 1개, 둘째 칸 = 2개, 셋째 칸 = 3개 ... (기본 50/30/20). 칸을 늘리면 4개 이상도 가능합니다.")]
    public List<float> countWeights = new List<float> { 50f, 30f, 20f };

    [Tooltip("후보로 인정하는 등장 위치입니다. 아이템의 등장 위치 중 하나라도 이 목록의 방 종류(variant 무관)면 후보가 됩니다. " +
             "기본: 일반방/보스방/상점/동아리방 - 특수방 전용/선택방 전용 아이템은 나오지 않습니다.")]
    public List<RoomCategory> allowedRoomCategories = new List<RoomCategory>
    {
        RoomCategory.Normal, RoomCategory.Boss, RoomCategory.Store, RoomCategory.Club,
    };

    [Tooltip("후보가 하나도 없을 때 띄우는 창의 문구입니다(이때 80번은 소모되지 않습니다).")]
    public string noItemMessage = "얻을 수 있는 아이템이 없습니다";

    // 후보가 없을 때 소모하지 않으려고 지연형으로 만들었습니다(위 [구현] 참고).
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
        GameObject player = context.PlayerObject;
        var inventory = player != null ? player.GetComponent<PlayerInventory>() : null;
        var database = ResolveDatabase(context);
        var gameFlow = context.GameFlow != null ? context.GameFlow : Object.FindFirstObjectByType<GameFlowManager>();

        if (inventory == null || database == null)
        {
            Debug.LogWarning("RandomPassiveGrantEffect: PlayerInventory 또는 Item Database를 찾지 못했습니다. 80번 아이템의 Disposable Effect에 " +
                             "Item Database를 넣어주세요(테스트 씬).");
            context.CancelPendingUse(sourceItemId);
            return;
        }

        var candidates = GetCandidates(inventory, database, gameFlow);
        if (candidates.Count == 0)
        {
            PassiveRerollUI.ShowMessage(player, noItemMessage);
            context.CancelPendingUse(sourceItemId);
            return;
        }

        int count = RollCount();
        var rewards = PickWeighted(candidates, count);

        var acquired = new List<ItemDefinition>();
        foreach (var reward in rewards)
        {
            // 이 레벨에서 나온 아이템으로 기록합니다(이후 보물상자/상점/48번/56번 후보에서 빠짐).
            if (gameFlow != null) gameFlow.MarkItemAssignedThisLevel(reward.itemId);

            // 보물상자에서 새로 얻은 사본과 같은 경로라 "획득 시 지급"(57번/77번 등)도 정상적으로 받습니다.
            ItemDefinition item = reward;
            if (GameFlowManager.AcquireItemCopy(item, true, () => inventory.TryAcquireItem(item)))
            {
                acquired.Add(item);
            }
        }

        Debug.Log($"RandomPassiveGrantEffect: {count}개 굴림 -> " + string.Join(", ", acquired.Select(i => i.displayName)));

        if (acquired.Count == 0)
        {
            // 정상적으로는 일어나지 않음(후보가 있었는데 하나도 못 얻음) - 소모하지 않습니다.
            context.CancelPendingUse(sourceItemId);
            return;
        }

        context.CompletePendingUse(sourceItemId);
        PassiveRerollUI.RaisePickupsInSequence(acquired);
    }

    // 지연형이지만 BeginPending 안에서 바로 끝나므로 정리할 것이 없습니다.
    public void CancelPending(DisposableEffectContext context, string sourceItemId) { }

    // countWeights 가중치로 개수를 정합니다(첫 칸 = 1개). 가중치가 전부 0 이하면 1개.
    private int RollCount()
    {
        if (countWeights == null || countWeights.Count == 0) return 1;

        float total = 0f;
        foreach (float w in countWeights) total += Mathf.Max(0f, w);
        if (total <= 0f) return 1;

        float roll = Random.Range(0f, total);
        for (int i = 0; i < countWeights.Count; i++)
        {
            float w = Mathf.Max(0f, countWeights[i]);
            if (roll < w) return i + 1;
            roll -= w;
        }
        return countWeights.Count;
    }

    // 아이템 등장 가중치로 count개를 겹치지 않게 뽑습니다(후보가 모자라면 있는 만큼).
    private static List<ItemDefinition> PickWeighted(List<ItemDefinition> candidates, int count)
    {
        var pool = new List<ItemDefinition>(candidates);
        var result = new List<ItemDefinition>();

        while (result.Count < count && pool.Count > 0)
        {
            float total = 0f;
            foreach (var item in pool) total += ItemWeightModifier.GetWeight(item);
            if (total <= 0f) break;

            float roll = Random.Range(0f, total);
            int picked = pool.Count - 1;
            for (int i = 0; i < pool.Count; i++)
            {
                float w = ItemWeightModifier.GetWeight(pool[i]);
                if (roll < w) { picked = i; break; }
                roll -= w;
            }

            ItemDefinition chosen = pool[picked];
            result.Add(chosen);
            pool.RemoveAt(picked);
            // [상극 아이템] 같이 받는 아이템끼리 같이 가질 수 없는 조합이 나오지 않게 뺍니다(예: 69번과 99번).
            pool.RemoveAll(other => ItemCompatibility.AreIncompatible(other, chosen));
        }
        return result;
    }

    // 후보: 허용 등장 위치가 하나라도 있는 패시브 중, 가지지 않았고, 이 레벨에서 아직 나오지 않았고, 가중치가 0보다 큰 것.
    private List<ItemDefinition> GetCandidates(PlayerInventory inventory, ItemDatabase database, GameFlowManager gameFlow)
    {
        var result = new List<ItemDefinition>();
        if (database == null || database.allItems == null) return result;

        var owned = ItemCompatibility.CollectOwned(inventory != null ? inventory.gameObject : null);
        var seen = new HashSet<string>();
        foreach (var item in database.allItems)
        {
            if (item == null || item.category != ItemDefinition.ItemCategory.Passive) continue;
            if (!HasAllowedAppearance(item)) continue;
            if (inventory != null && inventory.HasItem(item.itemId)) continue;
            if (ItemCompatibility.ConflictsWithAny(item, owned)) continue; // [상극 아이템] 가진 아이템과 같이 가질 수 없으면 제외
            if (ItemWeightModifier.GetWeight(item) <= 0f) continue;

            string key = string.IsNullOrEmpty(item.itemId) ? item.name : item.itemId;
            if (!seen.Add(key)) continue;
            result.Add(item);
        }

        // 이 레벨에서 이미 나온 아이템(보물상자를 열어 뜬 아이템, 상점에 진열됐던 아이템)은 뺍니다.
        if (gameFlow != null) result = gameFlow.FilterOutAlreadyAssignedThisLevel(result);
        return result;
    }

    private bool HasAllowedAppearance(ItemDefinition item)
    {
        if (item.appearanceContexts == null || allowedRoomCategories == null) return false;
        foreach (var ctx in item.appearanceContexts)
        {
            if (ctx != null && allowedRoomCategories.Contains(ctx.category)) return true;
        }
        return false;
    }

    private ItemDatabase ResolveDatabase(DisposableEffectContext context)
    {
        if (itemDatabase != null) return itemDatabase;
        return context != null && context.LevelBuilder != null ? context.LevelBuilder.itemDatabase : null;
    }
}
