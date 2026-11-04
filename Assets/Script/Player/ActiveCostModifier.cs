using System.Collections.Generic;
using UnityEngine;

// "액티브 아이템 코스트 소모량 감소" 부류 효과를 위한 정적 등록소입니다(CreditGainBonusOverride 등과 같은 패턴).
// 아이템이 감소량을 등록해두면, 실제로 코스트를 지불하는 곳(PlayerActiveItem.TryUse)과 좌상단 소모 코스트 표시
// (PlayerCostDisplay)가 모두 Resolve()로 "실제 소모 코스트"를 구해 씁니다 - 지불과 표시가 항상 같습니다.
//
// [규칙 - 형빈 확정]
// - 등록된 감소량은 모두 더해서 뺍니다(나중에 감소 아이템이 더 생기면 감소량끼리 합연산).
// - 결과는 1 밑으로 내려가지 않습니다(예: 소모량 1인 아이템은 감소가 있어도 1). 원래 소모량이 이미 1 이하인
//   아이템은 그대로 둡니다(0을 1로 올리지 않음).
// - 등록된 것이 없으면 원래 소모량 그대로 - 기존 동작과 100% 동일합니다.
// - 첫 사용처: 45번 장신구(장착 중 소모량 -1, Item45effect.cs). 장신구가 교체되면 Remove에서 등록을 해제하므로
//   그 즉시 원래 소모량으로 돌아갑니다.
public static class ActiveCostModifier
{
    private class Entry
    {
        public Object owner;
        public int reduction;
    }

    private static readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>();

    // sourceId 하나당 하나만 등록됩니다(같은 sourceId로 다시 호출하면 덮어씀). owner가 파괴되면(씬 재시작 등) 자동으로 빠집니다.
    public static void SetReduction(string sourceId, Object owner, int reduction)
    {
        if (string.IsNullOrEmpty(sourceId)) return;
        entries[sourceId] = new Entry { owner = owner, reduction = reduction };
    }

    public static void RemoveReduction(string sourceId)
    {
        if (string.IsNullOrEmpty(sourceId)) return;
        entries.Remove(sourceId);
    }

    // 원래 소모량(baseCost)에 감소를 반영한 실제 소모량입니다.
    public static int Resolve(int baseCost)
    {
        if (entries.Count == 0 || baseCost <= 1) return baseCost;

        PruneDead();
        int totalReduction = 0;
        foreach (var entry in entries.Values) totalReduction += entry.reduction;

        return Mathf.Max(1, baseCost - totalReduction);
    }

    private static void PruneDead()
    {
        List<string> deadKeys = null;
        foreach (var kvp in entries)
        {
            if (kvp.Value.owner == null)
            {
                if (deadKeys == null) deadKeys = new List<string>();
                deadKeys.Add(kvp.Key);
            }
        }
        if (deadKeys != null)
        {
            foreach (var key in deadKeys) entries.Remove(key);
        }
    }

    // 플레이 시작 시 이전 플레이의 등록 정보가 남아있지 않도록 초기화합니다(기존 등록소들과 같은 관례).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlayStart()
    {
        entries.Clear();
    }
}
