using System.Collections.Generic;
using UnityEngine;

// [충전 사격 - 정적 등록소]
// 110번 장신구 "좌클릭을 꾹 눌러 총알을 충전하고, 떼면 충전한 만큼 강한 총알 한 발을 쏜다"를 위한 등록소입니다.
// GrenadeSplitOverride(92번)/GrenadeHomingOverride(77번)와 같은 구조입니다.
//
// - 효과를 가진 장신구가 Register(이름, 소유자, 설정, 아이콘)로 등록하면, 등록이 하나라도 남아 있는 동안 TryGetActive가 true입니다.
//   여러 개가 등록되어 있으면 가장 나중에 등록한 설정을 씁니다.
// - PlayerChargeShot이 좌클릭을 누르는 순간 TryGetActive로 확인해서 충전을 시작할지 정합니다. 충전 도중 등록이 사라지면(장신구 교체/
//   버리기) 충전은 취소됩니다(충전에 쓴 탄약은 돌려주지 않음 - 장전과 같은 규칙).
// - 소유자(보통 플레이어 오브젝트)가 사라지면 등록도 자동으로 정리됩니다.
// - 플레이 시작 때마다 비워서, 에디터에서 Domain Reload를 꺼 둔 경우에도 이전 플레이의 등록이 남지 않습니다.
public static class ChargeShotOverride
{
    // 110번 설정입니다. 110번 Item Definition의 인스펙터(Trinket Effect)에서 조절합니다(등록은 이 묶음을 그대로 가리키므로 Play 중에
    // 값을 바꾸면 바로 반영됩니다).
    [System.Serializable]
    public class Settings
    {
        [Header("충전")]
        [Tooltip("좌클릭을 누르고 있는 동안 이 시간(초)마다 탄약 1발을 소모해 1발씩 충전합니다(기본 0.5). 누르는 순간에는 발사될 총알 자체의 1발을 소모합니다.")]
        [Min(0.05f)] public float chargeInterval = 0.5f;

        [Header("충전 1발당 가산 (실제 스탯 값 기준)")]
        [Tooltip("충전 1발당 피해 가산 = 현재 공격력 x 이 값입니다(기본 0.5 = +50%). 예) 1발 충전 = 공격력의 150%.")]
        [Min(0f)] public float damageRatioPerCharge = 0.5f;

        [Tooltip("충전 1발당 사거리 가산입니다(현재 사거리 스탯에 더함, 기본 0.1).")]
        public float rangePerCharge = 0.1f;

        [Tooltip("충전 1발당 총알 크기 가산입니다(실제 값 - 현재 총알 크기 스탯에 더함, 기본 0.01 = 화면 표시 +1).")]
        public float bulletSizePerCharge = 0.01f;

        [Tooltip("충전 1발당 총알 속도 가산입니다(현재 총알 속도 스탯에 더함, 기본 0.1).")]
        public float bulletSpeedPerCharge = 0.1f;

        [Header("관통")]
        [Tooltip("충전 수가 이 값 이상이면 강화 총알이 적과 벽을 관통합니다(기본 4발 = 머리 위 숫자 5). 관통 총알은 35번/69번으로 튕기지 않습니다. 0이면 관통하지 않습니다.")]
        [Min(0)] public int pierceChargeThreshold = 4;

        [Header("충전 이펙트 (1발 충전될 때마다 플레이어 중심에서)")]
        [Tooltip("1발 충전될 때마다 한 번씩 생성할 이펙트입니다(vfx_Implosion_01). 비워두면 이펙트 없이 충전됩니다.")]
        public GameObject implosionPrefab;

        [Tooltip("관통 충전 이펙트입니다. 충전 수가 관통 기준(Pierce Charge Threshold, 기본 4발 = 머리 위 숫자 5) 이상이 된 충전부터 위 이펙트 대신 " +
                 "이 이펙트를 터뜨립니다(예: 보라색으로 바꾼 vfx_Implosion_01 복제본). 비워두면 위 이펙트를 그대로 씁니다. 위치/크기/소멸 시간/" +
                 "따라다니기 설정은 아래 값을 같이 씁니다.")]
        public GameObject piercingImplosionPrefab;

        [Tooltip("플레이어 위치(발밑)에서 이펙트를 생성할 위치까지의 거리입니다(월드 기준, m). Y를 몸 중심 높이에 맞추세요.")]
        public Vector3 implosionOffset = new Vector3(0f, 0.8f, 0f);

        [Tooltip("이펙트 크기 배율입니다(파티클 크기/속도에 곱함). 1이면 프리팹 원래 크기입니다.")]
        [Min(0.01f)] public float implosionScale = 1f;

        [Tooltip("생성한 이펙트를 이 시간(초) 뒤에 지웁니다.")]
        [Min(0.1f)] public float implosionLifetime = 2f;

        [Tooltip("켜두면 이펙트가 플레이어를 따라 움직입니다(충전하며 이동해도 몸 중심에서 모이는 것처럼 보임).")]
        public bool implosionFollowPlayer = true;

        [Header("충전 수 표시 (장전 R 표시 자리 위쪽)")]
        [Tooltip("110번 아이콘의 화면상 크기입니다(1920x1080 기준 픽셀).")]
        [Min(1f)] public float indicatorIconSize = 48f;

        [Tooltip("장전 R 표시와 110번 아이콘 사이의 세로 간격입니다(1920x1080 기준 픽셀). R 표시 자리 바로 위에 아이콘이 뜹니다.")]
        public float indicatorGap = 6f;

        [Tooltip("아이콘 위치를 추가로 옮기는 값입니다(1920x1080 기준 픽셀, x 오른쪽 / y 위쪽).")]
        public Vector2 indicatorExtraOffset = Vector2.zero;

        [Tooltip("숫자 글자 크기입니다.")]
        [Min(1f)] public float numberFontSize = 26f;

        [Tooltip("숫자의 흰 테두리 두께(픽셀)입니다. 흰 글자 8개를 이 거리만큼 사방으로 밀어 깔아 만듭니다.")]
        [Min(0f)] public float numberOutlinePixels = 2f;

        [Tooltip("숫자를 아이콘 오른쪽 아래 모서리에서 옮기는 값입니다(픽셀, x 오른쪽 / y 위쪽).")]
        public Vector2 numberOffset = Vector2.zero;

        [Header("강화 총알 외형 (1발 이상 충전된 총알)")]
        public ChargedBulletVisual.Settings bulletVisual = new ChargedBulletVisual.Settings();

        [Header("피격 굴절 (관통 강화 총알에 맞은 적이 물결처럼 출렁임 - 관통 기준 이상)")]
        [Tooltip("관통 강화 총알이 적에게 맞을 때 적 몸이 굴절되는 연출입니다(ChargedHitRipple.cs). 재질은 메뉴 MobArchive/VFX/110번 피격 굴절 " +
                 "재질 만들기로 자동 생성/연결됩니다.")]
        public ChargedHitRipple.Settings hitRipple = new ChargedHitRipple.Settings();
    }

    private class Entry
    {
        public Object owner;
        public Settings settings;
        public Sprite icon;
        public int order;
    }

    private static readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>();
    private static int registerCounter;

    // 등록합니다. 같은 이름으로 다시 불러도 안전합니다(값을 덮어씀). icon은 충전 수 표시에 쓰는 아이템 아이콘입니다.
    public static void Register(string sourceId, Object owner, Settings settings, Sprite icon)
    {
        if (string.IsNullOrEmpty(sourceId) || settings == null) return;
        entries[sourceId] = new Entry { owner = owner, settings = settings, icon = icon, order = ++registerCounter };
    }

    // 등록을 해제합니다. 등록된 적이 없는 이름이어도 안전합니다.
    public static void Unregister(string sourceId)
    {
        if (string.IsNullOrEmpty(sourceId)) return;
        entries.Remove(sourceId);
    }

    // 살아 있는 등록이 있으면 true와 함께 (가장 나중에 등록한) 설정/아이콘을 돌려줍니다.
    public static bool TryGetActive(out Settings settings, out Sprite icon)
    {
        PruneDead();

        settings = null;
        icon = null;
        int bestOrder = int.MinValue;
        foreach (var entry in entries.Values)
        {
            if (entry.order > bestOrder)
            {
                bestOrder = entry.order;
                settings = entry.settings;
                icon = entry.icon;
            }
        }
        return settings != null;
    }

    public static bool TryGetActive(out Settings settings)
    {
        return TryGetActive(out settings, out _);
    }

    public static bool IsActive => TryGetActive(out _, out _);

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
