using System;
using System.Collections.Generic;
using UnityEngine;

// "크레딧을 획득할 때마다 추가로 N을 더 받는다" 부류의 패시브 아이템(30번 등)을 위한 정적
// 등록소입니다. EnemyDetectRangeOverride/EnemySpeedModifier/TreasureChanceBonusOverride와
// 동일한 패턴입니다 - 아이템이 등록해둔 값들을 여기서 합산해두고, 실제로 값을 쓰는 지점
// (PlayerResources.Add)에서 이 합산값을 참조합니다.
//
// 여러 개의 "크레딧 획득 시 +N" 아이템이 동시에 존재하게 되더라도(예: 30번 + 나중에 나올
// 비슷한 아이템), 각자 서로 다른 sourceId로 등록되므로 값이 합연산으로 자연스럽게 누적됩니다.
public static class CreditGainBonusOverride
{
    private class Entry
    {
        public UnityEngine.Object owner;
        public Func<int> getBonus;
    }

    private static readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>();

    // sourceId 하나당 하나의 보너스만 등록됩니다 - 같은 sourceId로 다시 호출하면 이전 값을
    // 덮어씁니다.
    public static void SetBonus(string sourceId, UnityEngine.Object owner, Func<int> getBonus)
    {
        if (string.IsNullOrEmpty(sourceId) || getBonus == null) return;
        entries[sourceId] = new Entry { owner = owner, getBonus = getBonus };
    }

    public static void RemoveBonus(string sourceId)
    {
        if (string.IsNullOrEmpty(sourceId)) return;
        entries.Remove(sourceId);
    }

    // 등록된 모든 보너스의 합입니다. 패시브 아이템은 영구 적용이라 현재는 owner가 사라질
    // 일이 거의 없지만, 씬 전환 등 예외 상황을 대비해 죽은 항목은 걸러냅니다.
    public static int CurrentBonus
    {
        get
        {
            PruneDead();
            int total = 0;
            foreach (var entry in entries.Values)
            {
                total += entry.getBonus();
            }
            return total;
        }
    }

    private static void PruneDead()
    {
        List<string> deadKeys = null;
        foreach (var kvp in entries)
        {
            if (kvp.Value.owner == null)
            {
                (deadKeys ??= new List<string>()).Add(kvp.Key);
            }
        }
        if (deadKeys != null)
        {
            foreach (var key in deadKeys) entries.Remove(key);
        }
    }

    // 플레이 시작 시 이전 플레이의 등록 정보가 남아있지 않도록 초기화합니다
    // (EnemyDetectRangeOverride 등 기존 레지스트리들과 동일한 관례).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlayStart()
    {
        entries.Clear();
    }
}
