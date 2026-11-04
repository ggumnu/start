using System.Collections.Generic;
using UnityEngine;

// [소이탄 - 정적 등록소]
// 81번 패시브 "수류탄이 소이탄이 된다"를 위한 등록소입니다. GrenadeHomingOverride(77번)/GrenadeSelfDamageOverride(57번)와 같은 구조입니다.
//
// - 효과를 가진 아이템이 Register(이름, 소유자, 설정)로 등록하면, 등록이 하나라도 남아 있는 동안 TryGetActive가 true입니다.
//   여러 개가 등록되어 있으면 가장 나중에 등록한 설정을 씁니다.
// - Grenade는 "던지는 순간" TryGetActive로 확인해서 소이탄 여부를 정합니다(빨간 발광/폭발 피해 50%/불타는 구역이 한 세트로
//   일치하도록). 날아가는 도중 81번을 잃어도 그 수류탄은 소이탄으로 터지고, 그 뒤로 던지는 수류탄부터 일반 수류탄입니다.
// - 소유자(보통 플레이어 오브젝트)가 사라지면 등록도 자동으로 정리됩니다.
// - 플레이 시작 때마다 비워서, 에디터에서 Domain Reload를 꺼 둔 경우에도 이전 플레이의 등록이 남지 않습니다.
public static class IncendiaryGrenadeOverride
{
    // 소이탄 설정입니다. 81번 Item Definition의 인스펙터에서 조절합니다(등록은 이 묶음을 그대로 가리키므로 Play 중에 값을 바꾸면
    // 다음 수류탄부터 바로 반영됩니다).
    [System.Serializable]
    public class Settings
    {
        [Header("폭발")]
        [Tooltip("폭발 순간 적에게 주는 피해 = 수류탄 피해 x 이 값(기본 0.5 = 50%).")]
        [Min(0f)] public float explosionDamageRatio = 0.5f;

        [Tooltip("폭발 순간 플레이어가 받는 자해 피해입니다(고정값, 기본 0.5). 수류탄 자해 피해 스탯이 0이면 받지 않습니다. 57번이 있으면 받지 않습니다.")]
        [Min(0f)] public float explosionSelfDamage = 0.5f;

        [Header("불타는 구역 (범위 = 수류탄 폭발 반경)")]
        [Tooltip("불타는 시간(초, 기본 3).")]
        [Min(0.01f)] public float burnDuration = 3f;

        [Tooltip("피해 간격(초, 기본 0.5). 터진 직후에는 주지 않고 이 간격이 지날 때마다 줍니다(기본 0.5/1.0/.../3.0초에 6번).")]
        [Min(0.05f)] public float tickInterval = 0.5f;

        [Tooltip("틱마다 적에게 주는 피해 = 수류탄 피해 x 이 값(기본 0.25 = 25%).")]
        [Min(0f)] public float tickDamageRatio = 0.25f;

        [Tooltip("틱마다 구역 안의 플레이어가 받는 피해입니다(고정값, 기본 0.5). 57번이 있으면 받지 않습니다.")]
        [Min(0f)] public float playerTickDamage = 0.5f;

        [Header("불타는 구역 표시 (착탄 지점 원과 같은 방식)")]
        [Tooltip("구역 안쪽(꽉 찬 원) 색입니다. 알파로 진하기 조절, 0이면 안 그림.")]
        public Color zoneFillColor = new Color(1f, 0.15f, 0.05f, 0.3f);

        [Tooltip("구역 테두리 색입니다. 알파 0이면 안 그림. 두께/바닥 띄움 높이는 PlayerGrenadeThrower의 착탄 지점 표시 값을 그대로 씁니다.")]
        public Color zoneOutlineColor = new Color(1f, 0.2f, 0.1f, 0.9f);

        [Header("불꽃 이펙트 (구역 안 랜덤 위치)")]
        [Tooltip("구역 안 랜덤 위치에 띄울 불꽃 프리팹입니다(vfx_Flames_01 등). 비워두면 불꽃 없이 원만 표시됩니다.")]
        public GameObject flamePrefab;

        [Tooltip("불꽃을 하나씩 띄우는 간격(초)입니다. 작을수록 불꽃이 많아집니다.")]
        [Min(0.02f)] public float flameSpawnInterval = 0.15f;

        [Tooltip("불꽃 하나가 새 불길을 뿜는 시간(초)입니다. 이 시간이 지나면 새 불길을 멈추고 남은 불길만 사라질 때까지 재생됩니다.")]
        [Min(0.05f)] public float flameEmitTime = 0.6f;

        [Tooltip("불꽃이 새 불길을 멈춘 뒤 오브젝트를 지우기까지 기다리는 시간(초)입니다(남은 불길이 자연스럽게 사라지는 시간).")]
        [Min(0f)] public float flameLingerTime = 1f;

        [Tooltip("불꽃 크기 배율입니다. 수류탄 폭발 반경이 커지면 그 비율만큼 자동으로 더 커집니다.")]
        [Min(0.01f)] public float flameScale = 1f;

        [Tooltip("랜덤 위치에서 불꽃을 옮기는 값입니다(월드 기준, m).")]
        public Vector3 flameOffset = Vector3.zero;

        [Header("소이탄 발광 (던진 순간부터 터질 때까지 수류탄 주변)")]
        [Tooltip("끄면 수류탄 주변 빨간 빛 그림을 띄우지 않습니다.")]
        public bool enableGlowSprite = true;

        [Tooltip("빛 그림 색입니다(알파로 진하기 조절).")]
        public Color glowColor = new Color(1f, 0.2f, 0.05f, 0.85f);

        [Tooltip("빛 그림 지름(월드 m)입니다.")]
        [Min(0.01f)] public float glowSize = 0.6f;

        [Tooltip("빛 그림이 맥박치듯 커졌다 작아지는 정도입니다(0.15 = ±15%). 0이면 맥박 없음.")]
        [Min(0f)] public float glowPulseAmount = 0.15f;

        [Tooltip("맥박 속도입니다(클수록 빠름).")]
        [Min(0f)] public float glowPulseSpeed = 10f;

        [Tooltip("켜면 수류탄에 빨간 점광원을 달아 주변 바닥/벽을 빨갛게 비춥니다.")]
        public bool enableGlowLight = true;

        [Tooltip("점광원 색입니다.")]
        public Color glowLightColor = new Color(1f, 0.25f, 0.1f, 1f);

        [Tooltip("점광원 밝기입니다.")]
        [Min(0f)] public float glowLightIntensity = 2f;

        [Tooltip("점광원이 비추는 거리(월드 m)입니다.")]
        [Min(0.01f)] public float glowLightRange = 1.5f;
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
