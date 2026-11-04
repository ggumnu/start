using System.Collections.Generic;
using UnityEngine;

// [즉시 처치(섬멸) - 정적 등록소]
// 98번 패시브 "적에게 피해를 줄 때 그 적의 체력이 25% 이하라면 즉시 처치한다"를 위한 등록소입니다.
// GrenadeSplitOverride(92번)/ChargeShotOverride(110번)와 같은 구조입니다.
//
// - 효과를 가진 아이템이 Register(이름, 소유자, 설정)로 등록하면, 등록이 하나라도 남아 있는 동안 TryGetActive가 true입니다.
//   여러 개가 등록되어 있으면 가장 나중에 등록한 설정을 씁니다.
// - 판정은 EnemyHealth.TakeDamage가 "피해를 빼기 직전"에 ShouldExecute로 합니다(그 순간 체력이 기준 이하일 때만 - 이번 피해로 기준
//   이하가 된 경우는 처치하지 않음). 처치된 적은 EnemyHealth.WasExecuted가 true가 되고, 그 순간의 설정(ExecuteSettings)을 들고 있어서
//   "섬멸" 글자(DamagePopupDisplay)/빨간 사라짐(DissolveAndVanish)/처치 이펙트(98번)가 모두 그 설정을 씁니다.
// - 소유자(보통 플레이어 오브젝트)가 사라지면 등록도 자동으로 정리됩니다.
// - 플레이 시작 때마다 비워서, 에디터에서 Domain Reload를 꺼 둔 경우에도 이전 플레이의 등록이 남지 않습니다.
public static class ExecuteOverride
{
    // 98번 설정입니다. 98번 Item Definition의 인스펙터에서 조절합니다.
    [System.Serializable]
    public class Settings
    {
        [Header("즉시 처치 기준")]
        [Tooltip("피해를 받는 순간 체력이 최대 체력의 이 비율 이하이면 즉시 처치합니다(기본 0.25 = 25%). 보스 포함.")]
        [Range(0f, 1f)] public float healthThreshold = 0.25f;

        [Header("\"섬멸\" 글자 (피해 숫자 대신, 처치 모션이 끝날 때까지 유지)")]
        [Tooltip("피해 숫자 대신 띄울 글자입니다.")]
        public string popupText = "섬멸";

        [Tooltip("글자 색입니다.")]
        public Color popupTextColor = Color.black;

        [Tooltip("글자 테두리 색입니다.")]
        public Color popupOutlineColor = new Color(0.9f, 0.05f, 0.05f, 1f);

        [Tooltip("테두리 두께(캔버스 픽셀)입니다. 너무 키우면 글자 뒤에 옅은 사각형이 보일 수 있습니다(약 2~3 권장).")]
        [Min(0f)] public float popupOutlinePixels = 2.5f;

        [Tooltip("글자 크기입니다(피해 숫자 기본값 40).")]
        [Min(1)] public int popupFontSize = 44;

        [Tooltip("적이 완전히 사라진(처치 모션이 끝난) 뒤 글자가 옅어지며 사라지는 시간(초)입니다.")]
        [Min(0f)] public float popupFadeOutTime = 0.15f;

        [Header("사라짐 색 (흰색 대신)")]
        [Tooltip("즉시 처치된 적이 사라질 때 물드는 색입니다(평소 처치는 흰색).")]
        public Color dissolveColor = new Color(1f, 0.08f, 0.08f, 1f);

        [Header("처치 이펙트 (평소 처치 폭발 대신)")]
        [Tooltip("즉시 처치된 적 위치에 터뜨릴 이펙트입니다(VFX_ImpactCritical_1.1.0_Pink). 비워두면 처치 폭발 없이 사라집니다(평소 폭발도 나오지 않음).")]
        public GameObject executeVfxPrefab;

        [Tooltip("적 위치(발밑)에서 이펙트를 옮기는 값입니다(월드 기준, m). Y를 몸 중심 높이에 맞추세요.")]
        public Vector3 executeVfxOffset = new Vector3(0f, 0.5f, 0f);

        [Tooltip("이펙트 크기 배율입니다(파티클 크기/속도에 곱함).")]
        [Min(0.01f)] public float executeVfxScale = 1f;

        [Tooltip("보스를 처치할 때 위 배율에 추가로 곱하는 값입니다(보스가 커서 이펙트가 작아 보이면 올리세요).")]
        [Min(0.01f)] public float bossVfxScaleMultiplier = 1f;

        [Tooltip("생성한 이펙트를 이 시간(초) 뒤에 지웁니다.")]
        [Min(0.1f)] public float executeVfxLifetime = 2f;
    }

    private class Entry
    {
        public Object owner;
        public Settings settings;
        public int order;
    }

    private static readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>();
    private static int registerCounter;

    public static void Register(string sourceId, Object owner, Settings settings)
    {
        if (string.IsNullOrEmpty(sourceId) || settings == null) return;
        entries[sourceId] = new Entry { owner = owner, settings = settings, order = ++registerCounter };
    }

    public static void Unregister(string sourceId)
    {
        if (string.IsNullOrEmpty(sourceId)) return;
        entries.Remove(sourceId);
    }

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

    // 피해를 받기 "직전"의 체력으로 즉시 처치 여부를 판정합니다. 처치해야 하면 true와 그 순간의 설정을 돌려줍니다.
    public static bool ShouldExecute(float currentHealth, float maxHealth, out Settings settings)
    {
        settings = null;
        if (maxHealth <= 0f || currentHealth <= 0f) return false;
        if (!TryGetActive(out var active)) return false;
        if (currentHealth > maxHealth * active.healthThreshold + 0.0001f) return false;

        settings = active;
        return true;
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
