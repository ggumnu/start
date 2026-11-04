using System.Collections.Generic;
using UnityEngine;

// [플레이어 디버프 면역 - 정적 등록소]
// 94번 패시브 "플레이어는 디버프에 걸리지 않는다"를 위한 등록소입니다. GrenadeSelfDamageOverride(57번)와 같은 구조입니다.
//
// [디버프의 범위 - 형빈 확정]
// 적 등이 플레이어에게 거는 상태 이상(앞으로 생길 감속, 독, 기절 등)만 디버프입니다. 아이템 효과로 능력치가 깎이는 것
// (트레이드오프 아이템)은 디버프가 아니므로 이 등록소와 무관합니다.
//
// [지금 상태]
// 플레이어에게 디버프를 거는 적이 아직 없어서, 지금은 등록만 되고 실제로 막히는 것은 없습니다.
//
// [나중에 플레이어 디버프를 만들 때 - 꼭 지킬 것]
// 플레이어에게 상태 이상을 거는 코드는 거는 순간 반드시 "if (PlayerDebuffImmunity.IsImmune) return;"으로 이 등록소를 먼저
// 확인해 주세요(거는 것 자체를 하지 않음 - 색/이펙트/지속시간도 시작하지 않음).
//
// - 효과를 가진 아이템이 Block(이름, 소유자)로 등록하면, 등록이 하나라도 남아 있는 동안 IsImmune이 true입니다.
// - 소유자(보통 플레이어 오브젝트)가 사라지면 등록도 자동으로 정리됩니다. Play 시작 때마다 비웁니다.
public static class PlayerDebuffImmunity
{
    private static readonly Dictionary<string, Object> blockers = new Dictionary<string, Object>();

    public static void Block(string sourceId, Object owner)
    {
        if (string.IsNullOrEmpty(sourceId)) return;
        blockers[sourceId] = owner;
    }

    public static void Unblock(string sourceId)
    {
        if (string.IsNullOrEmpty(sourceId)) return;
        blockers.Remove(sourceId);
    }

    public static bool IsImmune
    {
        get
        {
            PruneDead();
            return blockers.Count > 0;
        }
    }

    private static void PruneDead()
    {
        List<string> deadKeys = null;
        foreach (var kvp in blockers)
        {
            if (kvp.Value != null) continue;
            if (deadKeys == null) deadKeys = new List<string>();
            deadKeys.Add(kvp.Key);
        }
        if (deadKeys == null) return;
        foreach (var key in deadKeys) blockers.Remove(key);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlayStart()
    {
        blockers.Clear();
    }
}
