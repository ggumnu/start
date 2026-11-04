using System.Collections.Generic;
using UnityEngine;

// [수류탄 착지 후 유도 - 정적 등록소]
// 77번 패시브 "수류탄이 착지 직전 폭발을 보류하고, 바닥에 붙어 가까운 적에게 유도되다가 잠시 뒤 터진다"를 위한 등록소입니다.
// GrenadeSelfDamageOverride(57번)/WallBounceOverride(35번)와 같은 구조입니다.
//
// - 효과를 가진 아이템이 Register(이름, 소유자, 설정)로 등록하면, 등록이 하나라도 남아 있는 동안 TryGetActive가 true입니다.
//   여러 개가 등록되어 있으면 가장 나중에 등록한 설정을 씁니다.
// - Grenade가 착지하는 순간 TryGetActive로 확인해서, 등록이 있으면 바로 터지지 않고 유도 단계로 넘어갑니다
//   (날아가는 도중 77번을 잃으면 착지 순간 등록이 없으므로 평소처럼 바로 터집니다).
// - 소유자(보통 플레이어 오브젝트)가 사라지면 등록도 자동으로 정리됩니다.
// - 플레이 시작 때마다 비워서, 에디터에서 Domain Reload를 꺼 둔 경우에도 이전 플레이의 등록이 남지 않습니다.
public static class GrenadeHomingOverride
{
    // 유도 단계 설정입니다. 값은 등록한 아이템(77번 Item Definition 인스펙터)에서 옵니다.
    public class Settings
    {
        public float homingDuration = 1f;      // 착지 후 터지기까지 유도하는 시간(초)
        public float speedMultiplier = 1f;     // 유도 속도 = 플레이어 현재 이동속도 x 이 값
        public float groundOffset = 0.05f;     // 바닥에서 수류탄 중심을 띄우는 높이(m)
        public float collisionRadius = 0.1f;   // 벽에 막히는 판정/굴러가는 회전에 쓰는 수류탄 반지름(m)
    }

    private class Entry
    {
        public Object owner;
        public Settings settings;
        public int order;
    }

    private static readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>();
    private static int registerCounter;

    // 등록합니다. 같은 이름으로 다시 불러도 안전합니다(값을 덮어씀).
    public static void Register(string sourceId, Object owner, Settings settings)
    {
        if (string.IsNullOrEmpty(sourceId) || settings == null) return;
        entries[sourceId] = new Entry { owner = owner, settings = settings, order = ++registerCounter };
    }

    // 등록을 해제합니다. 등록된 적이 없는 이름이어도 안전합니다.
    public static void Unregister(string sourceId)
    {
        if (string.IsNullOrEmpty(sourceId)) return;
        entries.Remove(sourceId);
    }

    // 살아 있는 등록이 있으면 true와 함께 (가장 나중에 등록한) 설정을 돌려줍니다.
    public static bool TryGetActive(out Settings settings)
    {
        PruneDead();

        settings = null;
        int bestOrder = int.MinValue;
        foreach (var entry in entries.Values)
        {
            if (entry.order > bestOrder)
            {
                bestOrder = entry.order;
                settings = entry.settings;
            }
        }
        return settings != null;
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
        registerCounter = 0;
    }
}
