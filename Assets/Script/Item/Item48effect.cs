using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 일회용 48번 (2026-10 변경, 형빈 확정) - "사용하면 보유하고 있는 모든 패시브 아이템을 랜덤한 패시브 아이템으로 교체한다."
// (예전 효과 "보물상자 가중치 뒤섞기"는 보류 - Item48WeightScramble.cs에 그대로 보관)
//
// [기획 - 형빈 확정]
// - 보유 패시브가 3개면 랜덤한 3개로 교체됩니다. 결과끼리는 겹치지 않지만(A,A,F 불가), 원래 가졌던 아이템이 다시 나오는
//   것은 괜찮습니다(A,B,C -> A,E,F 또는 B,C,A 가능 - 다른 자리로 나와도 됨).
// - 각 자리의 새 아이템은 Item Database에 있는 모든 패시브 중에서 모두 같은 확률로 뽑습니다(2026-10 변경, 형빈 확정 -
//   등장 장소(동아리방/샬레/특수방 레이아웃 등)는 전혀 따지지 않는 완전 랜덤, 아이템 Weight도 무시). 그래서 평소에는 특정
//   방에서만 나오는 아이템도 나올 수 있습니다.
// - 48번으로 나온 아이템은 원래 가졌던 아이템이어도 전부 "새로 얻은 것"으로 취급합니다 - 원래 아이템을 잃고 새로 얻습니다.
//   특수 효과는 한 번만 걸리고(겹치지 않음), 1회성 지급은 다시 받으며, 레벨당 1회 제한도 풉니다.
//   잃을 때/얻을 때의 자세한 규칙은 IRemovablePassiveEffect.cs / PlayerInventory.ReplaceItems 참고.
// - 이 레벨에서 이미 나온 아이템(보물상자를 열어 뜬 아이템, 상점에 진열됐던 아이템 - 줍거나 사지 않았어도)은 후보에서 뺍니다
//   (형빈 확정 - 보물상자/상점과 같은 GameFlowManager 기록. 그 아이템을 나중에 또 얻는 일이 없도록). 단, 지금 가지고 있는
//   패시브는 위 규칙대로 다시 나올 수 있습니다. 이 기록은 다음 레벨로 넘어가면 초기화됩니다.
// - 보유 패시브가 하나도 없으면 사용할 수 없습니다(소모되지 않음). 더 이상 굴릴 후보가 없는 자리는 원래 아이템을 그대로
//   둡니다(그 자리는 잃지도 다시 얻지도 않음).
//
// [후보 고르기 - 결과 중복 방지]
// 자리 순서를 섞은 뒤 한 자리씩 "아직 안 뽑힌 후보" 중에서 같은 확률로 뽑습니다. 드물게 앞자리들이 후보를 다 가져가 뒷자리가
// 비는 경우가 있으면 처음부터 다시 굴리고(최대 RerollAttempts번), 그래도 안 되면 "다른 보유 아이템은 자기 자리에서만
// 다시 나올 수 있다"는 조건으로 한 번 더 굴립니다 - 이 경우 어떤 자리도 비지 않습니다.
//
// [아이템 목록]
// 인스펙터의 Item Database 칸을 비워두면 LevelBuilder에 연결된 것을 씁니다. 아이템 테스트 씬처럼 LevelBuilder가 없는
// 곳에서는 이 칸을 채워야 동작합니다(Item Database가 없으면 사용 불가로 처리).
//
// [아이템 에셋 설정]
// ItemDefinition: Item Id = 48, Category = Disposable, Disposable Effect = RerollAllPassivesEffect.
// 예전 Special Effects 줄(WeightScramblePassiveEffect)은 지워주세요(일회용은 Special Effects를 읽지 않지만 헷갈림 방지).
[Serializable]
public class RerollAllPassivesEffect : IDisposableItemEffect
{
    [Tooltip("후보를 뽑을 아이템 목록입니다. 비워두면 LevelBuilder의 Item Database를 씁니다(테스트 씬에서는 채워주세요).")]
    public ItemDatabase itemDatabase;

    // 결과가 겹쳐서 다시 굴리는 최대 횟수입니다(위 [후보 고르기] 참고).
    private const int RerollAttempts = 30;

    public bool IsImmediate => true;

    // 보유 패시브가 하나도 없거나 아이템 목록을 찾지 못하면 사용할 수 없습니다(소모되지 않음).
    public bool CanUse(DisposableEffectContext context)
    {
        var inventory = GetInventory(context);
        if (inventory == null || inventory.OwnedItems.Count == 0) return false;

        if (ResolveDatabase(context) == null)
        {
            Debug.LogWarning("RerollAllPassivesEffect: Item Database를 찾지 못해 48번을 사용할 수 없습니다. " +
                              "48번 에셋의 Item Database 칸을 채워주세요.");
            return false;
        }
        return true;
    }

    public void ActivateImmediate(DisposableEffectContext context, string sourceItemId)
    {
        var inventory = GetInventory(context);
        var database = ResolveDatabase(context);
        if (inventory == null || database == null) return;

        var owned = inventory.OwnedItems.ToList();
        if (owned.Count == 0) return;

        // 후보 = Item Database의 모든 패시브(중복 제거). 등장 장소는 따지지 않습니다(형빈 확정 - 완전 랜덤).
        var allPassives = database.allItems
            .Where(item => item != null && item.category == ItemDefinition.ItemCategory.Passive)
            .Distinct()
            .ToList();

        // 이 레벨에서 이미 나온 아이템(상자/상점)은 뺍니다. 지금 가진 패시브는 다시 나올 수 있어야 하므로 남겨둡니다.
        if (context.GameFlow != null)
        {
            var notYetAppeared = new HashSet<ItemDefinition>(context.GameFlow.FilterOutAlreadyAssignedThisLevel(allPassives));
            allPassives = allPassives.Where(item => notYetAppeared.Contains(item) || owned.Contains(item)).ToList();
        }

        // 자리마다 후보 목록 - 모든 자리가 같은 전체 목록을 씁니다. 목록(Item Database)에 없는 보유 아이템은 자기 자리에서만
        // 다시 나올 수 있게 그 자리 목록에 자신을 더합니다.
        var pools = new List<List<ItemDefinition>>();
        foreach (var original in owned)
        {
            var pool = new List<ItemDefinition>(allPassives);
            if (!pool.Contains(original)) pool.Add(original);
            pools.Add(pool);
        }

        var replacements = PickReplacements(owned, pools);
        if (replacements == null) return;
        // (상극 아이템 조합은 PickReplacements 안에서 걸러지고, 남더라도 ReplaceItems가 교체를 거절합니다.)

        inventory.ReplaceItems(replacements, ignoreOneTimeGrantLimit: true);
    }

    // 즉시형이라 호출되지 않습니다.
    public void BeginPending(DisposableEffectContext context, string sourceItemId) { }

    // 즉시형이라 호출되지 않습니다.
    public void CancelPending(DisposableEffectContext context, string sourceItemId) { }

    // ---------------- 후보 고르기 ----------------

    // 자리마다 새 아이템을 고릅니다. 결과[i]가 null이면 그 자리는 그대로 둡니다(굴릴 후보 없음).
    private static List<ItemDefinition> PickReplacements(List<ItemDefinition> owned, List<List<ItemDefinition>> pools)
    {
        int count = owned.Count;

        // 처음부터 후보가 하나도 없는 자리는 그대로 두고, 그 아이템은 다른 자리에서도 못 뽑게 합니다(지금은 Item Database가
        // 비어 있지 않은 한 생기지 않지만 안전장치로 남겨둡니다).
        var kept = new HashSet<ItemDefinition>();
        for (int i = 0; i < count; i++)
        {
            if (pools[i].Count == 0) kept.Add(owned[i]);
        }

        for (int attempt = 0; attempt < RerollAttempts; attempt++)
        {
            var result = TryPick(owned, pools, kept, restrictOtherOwned: false);
            if (result != null) return result;
        }

        // 마지막 수단: 다른 보유 아이템은 자기 자리에서만 다시 나올 수 있게 해서 어떤 자리도 비지 않게 합니다.
        return TryPick(owned, pools, kept, restrictOtherOwned: true);
    }

    // 한 번 굴립니다. 어떤 자리가 비면 null(실패)을 돌려줍니다 - restrictOtherOwned가 true면 실패하지 않습니다.
    private static List<ItemDefinition> TryPick(List<ItemDefinition> owned, List<List<ItemDefinition>> pools,
                                                HashSet<ItemDefinition> kept, bool restrictOtherOwned)
    {
        int count = owned.Count;
        var result = new ItemDefinition[count];
        var used = new HashSet<ItemDefinition>(kept);

        var order = Enumerable.Range(0, count).ToList();
        Shuffle(order);

        foreach (int i in order)
        {
            if (pools[i].Count == 0) continue; // 그대로 두는 자리(result[i] = null)

            // [상극 아이템] 이미 정해진 결과(뽑힌 것 + 그대로 두는 자리)와 같이 가질 수 없는 아이템은 후보에서 뺍니다(예: 69번과 99번).
            var candidates = pools[i].Where(c => !used.Contains(c) &&
                                                 (!restrictOtherOwned || c == owned[i] || !owned.Contains(c)) &&
                                                 !ItemCompatibility.ConflictsWithAny(c, used))
                                     .ToList();
            if (candidates.Count == 0)
            {
                if (!restrictOtherOwned) return null;

                // 여기까지 오는 경우는 자기 자신도 후보에 없을 때뿐입니다 - 그대로 둡니다.
                used.Add(owned[i]);
                continue;
            }

            var pick = candidates[UnityEngine.Random.Range(0, candidates.Count)]; // 모든 후보 같은 확률(형빈 확정)
            result[i] = pick;
            used.Add(pick);
        }

        // [상극 아이템] 최종 결과(그대로 두는 자리 포함)에 같이 가질 수 없는 조합이 남으면 이번 굴림은 실패로 보고 다시 굴립니다.
        // 마지막 수단 굴림에서도 남으면(극히 드묾) 상극을 일으키는 자리는 교체하지 않고 원래 아이템으로 둡니다 - 그래도 상극이면
        // PlayerInventory.ReplaceItems가 교체 자체를 거절합니다.
        var final = new List<ItemDefinition>();
        for (int i = 0; i < count; i++) final.Add(result[i] != null ? result[i] : owned[i]);
        for (int i = 0; i < count; i++)
        {
            for (int j = i + 1; j < count; j++)
            {
                if (!ItemCompatibility.AreIncompatible(final[i], final[j])) continue;
                if (!restrictOtherOwned) return null;
                if (result[j] != null) { result[j] = null; final[j] = owned[j]; }
                else if (result[i] != null) { result[i] = null; final[i] = owned[i]; }
            }
        }

        return result.ToList();
    }

    private static void Shuffle(List<int> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    // ---------------- 참조 찾기 ----------------

    private static PlayerInventory GetInventory(DisposableEffectContext context)
    {
        return context != null && context.PlayerObject != null
            ? context.PlayerObject.GetComponent<PlayerInventory>()
            : null;
    }

    private ItemDatabase ResolveDatabase(DisposableEffectContext context)
    {
        if (itemDatabase != null) return itemDatabase;
        return context != null && context.LevelBuilder != null ? context.LevelBuilder.itemDatabase : null;
    }
}
