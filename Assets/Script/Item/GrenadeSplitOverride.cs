using System.Collections.Generic;
using UnityEngine;

// [분열 수류탄 - 정적 등록소]
// 92번 패시브 "수류탄이 폭발할 때 던진 방향 기준 X자 4방향으로 분열 수류탄이 나와 한 번 더 폭발한다"를 위한 등록소입니다.
// GrenadeHomingOverride(77번)/IncendiaryGrenadeOverride(81번)와 같은 구조입니다.
//
// - 효과를 가진 아이템이 Register(이름, 소유자, 설정)로 등록하면, 등록이 하나라도 남아 있는 동안 TryGetActive가 true입니다.
//   여러 개가 등록되어 있으면 가장 나중에 등록한 설정을 씁니다.
// - Grenade는 "던지는 순간" TryGetActive로 확인해서 분열 여부를 정합니다(81번 소이탄과 같은 시점). 날아가는 도중 92번을 잃어도
//   그 수류탄은 분열하고, 그 뒤로 던지는 수류탄부터 분열하지 않습니다.
// - 분열 수류탄 자신은 다시 분열하지 않습니다(Grenade.cs가 분열탄에는 확인 자체를 하지 않음).
// - 소유자(보통 플레이어 오브젝트)가 사라지면 등록도 자동으로 정리됩니다.
// - 플레이 시작 때마다 비워서, 에디터에서 Domain Reload를 꺼 둔 경우에도 이전 플레이의 등록이 남지 않습니다.
public static class GrenadeSplitOverride
{
    // 분열 설정입니다. 92번 Item Definition의 인스펙터에서 조절합니다(등록은 이 묶음을 그대로 가리키므로 Play 중에 값을 바꾸면
    // 다음 수류탄부터 바로 반영됩니다).
    [System.Serializable]
    public class Settings
    {
        [Header("분열탄 위력 (원래 수류탄 대비 비율)")]
        [Tooltip("분열탄이 적에게 주는 피해 = 원래 수류탄 피해(던진 순간 값) x 이 값입니다(기본 0.5 = 50%). 81번 소이탄이면 그 위에 소이탄 폭발 비율이 한 번 더 곱해집니다.")]
        [Min(0f)] public float damageRatio = 0.5f;

        [Tooltip("분열탄의 폭발 반경 = 원래 수류탄 폭발 반경 x 이 값입니다(기본 0.5 = 50%). 폭발 이펙트/착탄 원/81번 불 구역/66번 해킹 범위도 이 반경을 따릅니다.")]
        [Min(0f)] public float radiusRatio = 0.5f;

        [Tooltip("분열탄 자해 피해 = 원래 수류탄 자해 피해 x 이 값을 0.5 단위로 올림한 값입니다(기본 0.5 → 자해 1이면 0.5). 원래 자해 피해가 0이면 0입니다. 57번이 있으면 받지 않습니다.")]
        [Min(0f)] public float selfDamageRatio = 0.5f;

        [Header("분열 방향/거리")]
        [Tooltip("던진 방향 기준 분열 각도(도)입니다. 기본 45 → 앞쪽 ±45도, 뒤쪽 ±135도의 X자 4방향(서로 90도 간격).")]
        [Range(0f, 90f)] public float splitAngle = 45f;

        [Tooltip("분열탄이 날아가는 거리 = 원래 수류탄 폭발 반경 x 이 값입니다(기본 1 = 원래 폭발 원의 가장자리). 폭발 반경이 커지면 거리도 같이 늘어납니다.")]
        [Min(0f)] public float distanceRatio = 1f;

        [Header("분열탄 비행/외형")]
        // 비행 시간(착지 = 폭발 또는 77번 유도 시작까지)은 따로 두지 않고 원래 수류탄과 같습니다(PlayerGrenadeThrower의 Flight Time).
        [Tooltip("분열탄 포물선의 최고점 높이(m)입니다.")]
        [Min(0f)] public float arcHeight = 1f;

        [Tooltip("분열탄이 출발하는 높이(m)입니다. 폭발 지점 바닥에서 이만큼 위에서 튀어나옵니다.")]
        [Min(0f)] public float spawnHeight = 0.3f;

        [Tooltip("분열탄 모델 크기 배율입니다(기본 0.6 = 원래 수류탄의 0.6배).")]
        [Min(0.01f)] public float modelScale = 0.6f;
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
