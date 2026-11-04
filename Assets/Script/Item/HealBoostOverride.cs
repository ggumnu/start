using System.Collections.Generic;
using UnityEngine;

// [회복량 배율 - 정적 등록소]
// 101번 패시브 "회복량 두 배"를 위한 등록소입니다. ExecuteOverride(98번)/GrenadeSplitOverride(92번)와 같은 구조입니다.
//
// - 효과를 가진 아이템이 Register(이름, 소유자, 배율)로 등록하면, 등록이 하나라도 남아 있는 동안 GetMultiplier가 그 배율을 돌려줍니다.
//   여러 개가 등록되어 있으면 가장 큰 배율 하나만 씁니다(겹쳐서 곱하지 않음). 등록이 없으면 1입니다.
// - PlayerHealth가 체력을 올리는 두 곳에서 이 배율을 씁니다.
//   1) Heal(양): 양 x 배율만큼 회복(최대 체력을 넘는 만큼은 잘림). 보물상자/상점 "현재 체력+1", 21/32/36/72/88번 등 모든 회복.
//   2) 최대 체력 증가(PlayerStats.OnMaxHealthChanged의 delta가 양수일 때): 현재 체력을 delta x 배율만큼 올림(새 최대 체력을 넘지 않게).
//      최대 체력이 줄어들 때(delta가 음수)는 배율을 쓰지 않습니다.
// - 소유자(보통 플레이어 오브젝트)가 사라지면 등록도 자동으로 정리됩니다.
// - 플레이 시작 때마다 비워서, 에디터에서 Domain Reload를 꺼 둔 경우에도 이전 플레이의 등록이 남지 않습니다.
public static class HealBoostOverride
{
    private class Entry
    {
        public Object owner;
        public float multiplier;
    }

    private static readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>();

    public static void Register(string sourceId, Object owner, float multiplier)
    {
        if (string.IsNullOrEmpty(sourceId)) return;
        entries[sourceId] = new Entry { owner = owner, multiplier = Mathf.Max(0f, multiplier) };
    }

    public static void Unregister(string sourceId)
    {
        if (string.IsNullOrEmpty(sourceId)) return;
        entries.Remove(sourceId);
    }

    // 지금 적용할 회복 배율입니다(등록이 없으면 1).
    public static float GetMultiplier()
    {
        PruneDead();

        bool any = false;
        float best = 1f;
        foreach (var entry in entries.Values)
        {
            if (!any || entry.multiplier > best)
            {
                best = entry.multiplier;
                any = true;
            }
        }
        return any ? best : 1f;
    }

    private static void PruneDead()
    {
        List<string> deadKeys = null;
        foreach (var kvp in entries)
        {
            if (kvp.Value.owner != null) continue;
            if (deadKeys == null) deadKeys = new List<string>();
            deadKeys.Add(kvp.Key);
        }
        if (deadKeys == null) return;
        foreach (var key in deadKeys) entries.Remove(key);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlayStart()
    {
        entries.Clear();
    }
}
