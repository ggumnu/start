using System.Collections.Generic;
using UnityEngine;

// [수류탄 해킹 보유 여부 - 정적 등록소]
// 66번 패시브 "수류탄 폭발에 휘말린 적이 해킹된다"를 들고 있는지를 다른 효과가 알 수 있게 하는 등록소입니다
// (GrenadeSelfDamageOverride(57번)와 같은 구조). 66번 자체의 폭발 해킹은 여전히 66번이 OnGrenadeExploded로 직접 처리하고,
// 이 등록소는 "보유 중인가"만 알려줍니다.
//
// - 지금 쓰는 곳: 81번 소이탄의 불타는 구역 - 66번 보유 중이면 불 구역 피해를 받은 적도 틱마다 해킹됩니다(Item81effect.cs).
// - 소유자(보통 플레이어 오브젝트)가 사라지면 등록도 자동으로 정리됩니다.
// - 플레이 시작 때마다 비워서, 에디터에서 Domain Reload를 꺼 둔 경우에도 이전 플레이의 등록이 남지 않습니다.
public static class GrenadeHackOverride
{
    private static readonly Dictionary<string, Object> owners = new Dictionary<string, Object>();

    // 등록합니다. 같은 이름으로 다시 불러도 안전합니다.
    public static void Register(string sourceId, Object owner)
    {
        if (string.IsNullOrEmpty(sourceId)) return;
        owners[sourceId] = owner;
    }

    // 등록을 해제합니다. 등록된 적이 없는 이름이어도 안전합니다.
    public static void Unregister(string sourceId)
    {
        if (string.IsNullOrEmpty(sourceId)) return;
        owners.Remove(sourceId);
    }

    // 살아 있는 등록이 하나라도 있으면 true입니다.
    public static bool IsActive
    {
        get
        {
            PruneDead();
            return owners.Count > 0;
        }
    }

    private static void PruneDead()
    {
        List<string> deadKeys = null;
        foreach (var kvp in owners)
        {
            if (kvp.Value != null) continue;
            if (deadKeys == null) deadKeys = new List<string>();
            deadKeys.Add(kvp.Key);
        }
        if (deadKeys == null) return;
        foreach (var key in deadKeys) owners.Remove(key);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlayStart()
    {
        owners.Clear();
    }
}
