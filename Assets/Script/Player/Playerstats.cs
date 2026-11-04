using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

// 플레이어의 모든 능력치를 한 곳에서 관리하는 스크립트입니다.
// 아래 [Header 1~13] 필드들은 이제 "최종값"이 아니라 이번 런 시작 시점의 "기본값(base)"
// 입니다. 아이템(패시브)들은 이 기본값을 직접 수정하지 않고, AddModifier()로 가감/배율
// 효과를 등록합니다. 다른 스크립트(이동, 발사, 체력 등)는 지금까지와 완전히 동일하게
// 이 스크립트의 프로퍼티(MoveSpeed, Damage 등)로만 값을 읽으면 되고, 내부적으로
// "기본값 + 모든 아이템 효과가 반영된 최종값"이 자동으로 계산되어 반환됩니다.
//
// [계산 방식]
// 스탯 하나당: (base + 모든 Additive 모디파이어의 합) x (모든 Multiplicative 모디파이어의 곱)
// 아이템 획득 순서와 무관하게 항상 같은 결과가 나오도록, Additive를 전부 더한 뒤에
// Multiplicative를 곱하는 순서로 고정했습니다.
//
// [최소값 하한선 - 트레이드오프 아이템으로 스탯이 0/음수가 되는 것 방지]
// 예전에는 전 스탯이 하나의 minStatRatio(예: 0.5 = 50%)를 공유했지만, 지금은 statFloors
// 리스트에서 스탯별로 완전히 다른 하한선을 걸 수 있습니다. 각 항목은 두 가지 모드 중
// 하나를 고를 수 있습니다.
//   - Ratio: "이번 런 시작 시점의 기본값 x value" 가 하한선입니다.
//            예: 장전 시간 기본값이 1.2이고 value=0.5면, 아무리 감소 아이템을 먹어도
//            장전 시간은 0.6 밑으로 안 내려갑니다.
//   - Absolute: value 그 자체가 고정 하한선입니다. 기본값이 얼마든 상관없이 항상
//            이 값 이상입니다. 예: 공격력에 Absolute/1을 걸면 공격력은 항상 최소 1입니다.
// Ratio 모드의 계산 기준값은 Awake() 시점(런 시작 시점)에 한 번만 캐싱되므로, 이후
// 아이템으로 base 필드 자체가 바뀌어도(예: 체력방으로 maxHealth 증가) 하한선은 "이번 런
// 시작 시점" 기본값 기준 그대로 고정됩니다. (statFloors 자체를 스탯 추가/삭제하는 게
// 아니라 각 항목의 mode/value만 바꾸는 용도이므로, 인스펙터에는 스탯 15종이 항상 전부
// 보이도록 기본값을 채워뒀습니다.)
//
// [확률형 스탯(GematriaChance/SchaleChance)과 정수형 스탯(MagazineSize/Multishot)]
// 이 스탯들은 statFloors와 무관하게 Compute() 이후 별도의 안전장치가 하나 더 적용됩니다
// (확률은 0~1 clamp, 정수형은 최소 1 보장). statFloors 설정은 그 위에 추가로 걸리는
// 하한선이고, 기존 안전장치를 대체하지 않습니다.
//
// [MaxHealth와 PlayerHealth의 연동]
// MaxHealth 스탯은 다른 스탯과 달리 PlayerHealth.currentHealth라는 "실시간 값"과
// 짝을 이루고 있어서, 최종 MaxHealth가 바뀔 때마다 currentHealth도 같은 양만큼
// 움직여줘야 합니다(예: 9/10 상태에서 MaxHealth +1 아이템을 먹으면 10/11이 되어야지,
// 9/11이 되면 안 됩니다). 이를 위해 AddModifier / RemoveModifiersFromSource에서
// MaxHealth의 "적용 전 최종값"과 "적용 후 최종값" 차이(delta)를 계산해 OnMaxHealthChanged
// 이벤트로 흘려보냅니다. PlayerHealth는 이 이벤트를 구독해서 currentHealth를 같은
// delta만큼 조정합니다.
//
// [다음 전투 레벨 진입 시 최대 체력 정리 - 형빈 확정]
// n.5 선택방 레벨에서 다음 전투 레벨로 넘어갈 때(GameFlowManager), 채워지지 않은 최대 체력 칸을 1칸 단위로 지웁니다
// (ApplyLevelEndMaxHealthLoss). 예) 3/5 -> 3/3, 3.5/5 -> 3.5/4. 전투 레벨 -> n.5 레벨로 갈 때는 지우지 않습니다(n.5 레벨이
// 마지막 회복 기회). 최소 1칸은 항상 남습니다. 지운 양은 아이템이 아닌 전용 출처(LevelEndLossSource)의 모디파이어로 기록하므로
// 아이템을 얻거나 잃는 규칙과 섞이지 않고, 이후 최대 체력 증가 아이템은 평소처럼 더해집니다.
public class PlayerStats : MonoBehaviour
{
    [Header("1. 체력 (최대 체력)")]
    [Tooltip("최대 체력. 실제 '현재 체력'은 PlayerHealth가 관리합니다.")]
    public float maxHealth = 6f;

    // [최대 체력 상한 - 체력 UI 두 줄 개편 때 추가, 형빈 확정]
    // 최대 체력 최종값은 이 값을 넘지 않습니다(기본 10 = 체력 UI 두 줄 x 5칸). 상한을 넘는 최대 체력 증가분은 기억하지 않습니다
    // (형빈 확정 - 예전에는 숨은 초과분으로 기억했지만 없앰): 10/10에서 +1 아이템을 먹으면 최대 체력 스탯은 10 그대로이고, 막힌 만큼
    // 현재 체력만 회복합니다(OnMaxHealthIncreaseCapped -> PlayerHealth). 넘친 양은 전용 출처(CapTrimSource)의 음수 모디파이어로 그때그때
    // 깎아 둡니다(TrimMaxHealthToCap).
    [Tooltip("최대 체력 상한입니다(기본 10). 상한을 넘는 최대 체력 증가분은 버리고, 막힌 만큼 현재 체력을 회복합니다.")]
    [Min(0.5f)] public float maxHealthCap = 10f;

    // [테스트 전용 - PlayerTestProfile의 "체력 상한 해제"] 켜져 있으면 최대 체력 상한(maxHealthCap)을 적용하지 않습니다.
    // 테스트 씬에서 PlayerTestProfile이 켤 때만 true가 되고, 인스펙터에 표시되거나 프리팹에 저장되지 않는 실행 전용 값입니다.
    [System.NonSerialized] public bool testNoMaxHealthCap = false;

    [Header("2. 이동 속도")]
    public float moveSpeed = 6f;

    [Header("3. 공격 속도 (초당 발사 가능 횟수)")]
    [Tooltip("값이 높을수록 다음 공격까지의 대기시간이 짧아집니다. 예: 2 = 0.5초마다 공격 가능")]
    public float attackSpeed = 2f;

    [Header("4. 장전 시간 (초)")]
    [Tooltip("값이 낮을수록 장전이 빨리 끝납니다.")]
    public float reloadTime = 1.2f;

    [Header("5. 행운 (보류 - 아이템 확률 시스템에서 사용 예정)")]
    public float luck = 0f;

    [Header("6. 코스트 회복 (보류 - 방 클리어 게이지 시스템에서 사용 예정)")]
    public float costRecovery = 1f;

    [Header("7. 공격력 (탄환 1발당 데미지)")]
    public float damage = 3.5f;

    [Header("8. 사거리 (탄환이 소멸하기까지 날아가는 거리)")]
    public float range = 12f;

    [Header("9. 탄창 크기 (장전당 가능한 '공격 클릭' 횟수)")]
    [Tooltip("실제 발사되는 탄환 수가 아니라, 장전 전까지 좌클릭할 수 있는 횟수입니다.")]
    public int magazineSize = 6;

    [Header("10. 총알 크기 (탄환 오브젝트 스케일 배율)")]
    public float bulletSize = 1f;

    [Header("11. 총알 속도")]
    public float bulletSpeed = 20f;

    [Header("12. 연사 (클릭 1회당 연속 발사되는 탄환 수)")]
    public int multishot = 1;

    [Tooltip("연사 시 탄환 사이의 발사 간격 (초). '따다당' 느낌을 위한 값입니다.")]
    public float multishotInterval = 0.08f;

    [Header("13. 선택방 확률 (n.5 레벨 - 아이템으로 조절 예정)")]
    [Tooltip("n.5 레벨 시작방 서쪽에 '게마트리아' 방이 등장할 확률 (0~1). n레벨 클리어 시점의 이 값으로 판정됩니다.")]
    [Range(0f, 1f)] [UnityEngine.Serialization.FormerlySerializedAs("blackSuitChance")] public float gematriaChance = 0f;

    [Tooltip("n.5 레벨 시작방 동쪽에 '샬레' 방이 등장할 확률 (0~1). n레벨 클리어 시점의 이 값으로 판정됩니다.")]
    [FormerlySerializedAs("chaleChance")]
    [Range(0f, 1f)] public float schaleChance = 0f;

    [Header("14. 수류탄 (2키 수류탄 모드 - PlayerGrenadeThrower/Grenade가 사용)")]
    [Tooltip("수류탄 폭발 시 반경 안의 적에게 주는 피해입니다. 총 공격력(Damage)과는 무관한 독립 고정값입니다.")]
    public float grenadeDamage = 10f;

    [Tooltip("수류탄 폭발 반경 안에 플레이어 자신이 있을 때 플레이어가 받는 피해입니다(적 피해와 별개). " +
             "하한선은 기본적으로 고정값 0이라, 아이템으로 0까지(자폭 면역) 내릴 수 있습니다.")]
    public float grenadeSelfDamage = 1f;

    [Tooltip("수류탄 폭발 반경(m)입니다.")]
    public float grenadeRadius = 2.5f;

    [Tooltip("수류탄을 던질 수 있는 최대 거리(m)입니다. 조준 시 흰 테두리 원으로 표시됩니다.")]
    public float grenadeRange = 8f;

    [Header("15. 해킹툴 (3키 해킹툴 모드 - PlayerHackTool이 사용)")]
    [Tooltip("해킹툴 전개 시 범위 안의 적에게 주는 피해입니다. 총 공격력(Damage)과는 무관한 독립 고정값입니다. " +
             "플레이어 자신은 피해를 받지 않습니다.")]
    public float hackDamage = 5f;

    [Tooltip("해킹툴 전개 범위(플레이어 중심 반경, m)입니다. 우클릭 시 흰 테두리 원으로 표시됩니다.")]
    public float hackRadius = 3f;

    [Tooltip("해킹(강력 제어) 기본 지속시간(초)입니다. 실제 적용값은 6번 패시브(제어 지속시간 보너스)가 " +
             "합산된 값입니다.")]
    public float hackDuration = 2f;

    // 하한선 모드입니다.
    // Ratio: "이번 런 시작 시점 기본값 x value"가 하한선 (기존 minStatRatio와 동일한 방식).
    // Absolute: value 그 자체가 고정 하한선 (기본값과 무관하게 항상 이 값 이상).
    public enum FloorMode
    {
        Ratio,
        Absolute,
    }

    // 스탯 하나에 대한 하한선 설정 한 줄입니다. statFloors 리스트의 원소로 인스펙터에
    // 노출됩니다.
    [Serializable]
    public class StatFloorConfig
    {
        public StatType statType;
        public FloorMode mode = FloorMode.Ratio;

        [Tooltip("Ratio 모드면 이번 런 시작 시점 기본값에 곱할 비율(예: 0.5 = 50%), " +
                 "Absolute 모드면 최종값이 내려갈 수 있는 고정 하한선 그 자체입니다(예: 1 = 항상 최소 1).")]
        public float value = 0.5f;
    }

    [Header("아이템 시스템 - 스탯별 하한선")]
    [Tooltip("트레이드오프 아이템으로 스탯이 내려가도 각 줄에서 정한 하한선 밑으로는 절대 " +
             "내려가지 않습니다. 스탯마다 Ratio(기본값의 %)와 Absolute(고정 숫자) 중 원하는 " +
             "모드를 골라 쓸 수 있습니다.")]
    public List<StatFloorConfig> statFloors = new List<StatFloorConfig>
    {
        new StatFloorConfig { statType = StatType.MaxHealth,         mode = FloorMode.Ratio, value = 0.5f },
        new StatFloorConfig { statType = StatType.MoveSpeed,         mode = FloorMode.Ratio, value = 0.5f },
        new StatFloorConfig { statType = StatType.AttackSpeed,       mode = FloorMode.Ratio, value = 0.5f },
        new StatFloorConfig { statType = StatType.ReloadTime,        mode = FloorMode.Ratio, value = 0.5f },
        new StatFloorConfig { statType = StatType.Luck,              mode = FloorMode.Ratio, value = 0.5f },
        new StatFloorConfig { statType = StatType.CostRecovery,      mode = FloorMode.Ratio, value = 0.5f },
        new StatFloorConfig { statType = StatType.Damage,            mode = FloorMode.Ratio, value = 0.5f },
        new StatFloorConfig { statType = StatType.Range,             mode = FloorMode.Ratio, value = 0.5f },
        new StatFloorConfig { statType = StatType.MagazineSize,      mode = FloorMode.Ratio, value = 0.5f },
        new StatFloorConfig { statType = StatType.BulletSize,        mode = FloorMode.Ratio, value = 0.5f },
        new StatFloorConfig { statType = StatType.BulletSpeed,       mode = FloorMode.Ratio, value = 0.5f },
        new StatFloorConfig { statType = StatType.Multishot,         mode = FloorMode.Ratio, value = 0.5f },
        new StatFloorConfig { statType = StatType.MultishotInterval, mode = FloorMode.Ratio, value = 0.5f },
        new StatFloorConfig { statType = StatType.GematriaChance,    mode = FloorMode.Ratio, value = 0.5f },
        new StatFloorConfig { statType = StatType.SchaleChance,       mode = FloorMode.Ratio, value = 0.5f },
        new StatFloorConfig { statType = StatType.GrenadeDamage,     mode = FloorMode.Ratio, value = 0.5f },
        new StatFloorConfig { statType = StatType.GrenadeSelfDamage, mode = FloorMode.Absolute, value = 0f },
        new StatFloorConfig { statType = StatType.GrenadeRadius,     mode = FloorMode.Ratio, value = 0.5f },
        new StatFloorConfig { statType = StatType.GrenadeRange,      mode = FloorMode.Ratio, value = 0.5f },
        new StatFloorConfig { statType = StatType.HackDamage,        mode = FloorMode.Ratio, value = 0.5f },
        new StatFloorConfig { statType = StatType.HackRadius,        mode = FloorMode.Ratio, value = 0.5f },
        new StatFloorConfig { statType = StatType.HackDuration,      mode = FloorMode.Ratio, value = 0.5f },
    };

    // MaxHealth의 "최종값"이 바뀔 때마다 그 변화량(delta)을 전달합니다.
    // PlayerHealth가 이 이벤트를 구독해서 currentHealth를 같은 폭만큼 조정합니다.
    public event Action<float> OnMaxHealthChanged;

    // [최대 체력 상한] 아이템으로 최대 체력이 올라야 했는데 상한(maxHealthCap)에 막혀 오르지 못한 양을 전달합니다.
    // PlayerHealth가 구독해서 그만큼 현재 체력을 회복합니다(형빈 확정 - 상한에서 최대 체력 아이템 = 회복).
    public event Action<float> OnMaxHealthIncreaseCapped;

    // ---- 내부 상태: 아이템이 건 모디파이어들과, 런 시작 시점 기본값 기반 하한선 ----
    private class AppliedModifier
    {
        public ModifierType modifierType;
        public float value;
        public string sourceItemId;
    }

    private readonly Dictionary<StatType, List<AppliedModifier>> activeModifiers = new Dictionary<StatType, List<AppliedModifier>>();

    // [최대 체력 상한] 상한을 넘은 최대 체력을 깎아 두는 모디파이어의 출처입니다(아이템 itemId와 겹치지 않는 이름).
    private const string CapTrimSource = "__maxhp_cap_trim";
    // [다음 전투 레벨 진입 시 최대 체력 정리] 채워지지 않아 지운 최대 체력을 기록하는 모디파이어의 출처입니다.
    private const string LevelEndLossSource = "__maxhp_level_end_loss";
    // 최대 체력 정리 후에도 항상 남기는 최소 최대 체력입니다(다음 레벨에 들어가자마자 죽지 않도록 - 형빈 확정 1칸).
    private const float MinMaxHealthAfterLevelLoss = 1f;
    private readonly Dictionary<StatType, float> minValues = new Dictionary<StatType, float>();

    void Awake()
    {
        CacheMinValues();
    }

    // statFloors를 순회하며 스탯별 하한선을 미리 계산해둡니다. Ratio 모드는 런 시작
    // 시점(Awake)의 기본값을 기준으로 고정되고, Absolute 모드는 기본값과 무관하게
    // value를 그대로 씁니다. 이후 아이템으로 base 필드 자체가 바뀌어도(예: 체력방으로
    // maxHealth 증가) 하한선은 "이번 런 시작 시점" 기준 그대로 유지됩니다.
    private void CacheMinValues()
    {
        minValues.Clear();
        EnsureAllStatFloors();
        if (statFloors == null) return;

        foreach (var cfg in statFloors)
        {
            if (cfg == null) continue;

            float min = cfg.mode == FloorMode.Ratio
                ? GetBaseFieldValue(cfg.statType) * cfg.value
                : cfg.value;

            minValues[cfg.statType] = min;
        }
    }

    // [수류탄 스탯 추가 시 함께 추가됨]
    // Player 프리팹/씬에 이미 저장되어 있던 statFloors 리스트는 위 코드 기본값(초기화 목록)을
    // 다시 읽지 않기 때문에, 나중에 StatType에 새 스탯이 추가되어도 인스펙터 목록에 그 줄이
    // 저절로 생기지 않습니다(= 새 스탯에 하한선이 아예 안 걸림). 그래서 목록에 없는 스탯이
    // 있으면 기본 하한선 줄을 자동으로 채워 넣습니다. 이미 있는 줄(형빈이 바꿔둔 값 포함)은
    // 절대 건드리지 않고, 빠진 스탯만 맨 뒤에 추가합니다. 에디터에서는 OnValidate()로 인스펙터
    // 목록에도 바로 보이게 되고, 실행 중에는 CacheMinValues()에서 한 번 더 보장합니다.
    private void EnsureAllStatFloors()
    {
        if (statFloors == null) statFloors = new List<StatFloorConfig>();

        foreach (StatType type in Enum.GetValues(typeof(StatType)))
        {
            bool exists = false;
            for (int i = 0; i < statFloors.Count; i++)
            {
                if (statFloors[i] != null && statFloors[i].statType == type)
                {
                    exists = true;
                    break;
                }
            }
            if (exists) continue;

            statFloors.Add(CreateDefaultFloor(type));
        }
    }

    // 스탯별 기본 하한선입니다. 수류탄 자해 데미지만 "고정값 0"(아이템으로 자폭 면역까지 내릴 수
    // 있게)이고, 나머지는 기존 스탯들과 동일하게 "런 시작 기본값의 50%"입니다.
    private static StatFloorConfig CreateDefaultFloor(StatType type)
    {
        if (type == StatType.GrenadeSelfDamage)
        {
            return new StatFloorConfig { statType = type, mode = FloorMode.Absolute, value = 0f };
        }
        return new StatFloorConfig { statType = type, mode = FloorMode.Ratio, value = 0.5f };
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        EnsureAllStatFloors();
    }
#endif

    // statFloors의 Ratio 모드 계산에 쓰이는 "런 시작 시점 기본값"을 스탯 종류별로
    // 반환합니다. PlayerStats에 새 스탯 필드를 추가할 때는 StatModifier.cs의 StatType과
    // 아래 Compute() 호출부, 그리고 이 매핑에도 함께 추가해주세요.
    private float GetBaseFieldValue(StatType type)
    {
        switch (type)
        {
            case StatType.MaxHealth: return maxHealth;
            case StatType.MoveSpeed: return moveSpeed;
            case StatType.AttackSpeed: return attackSpeed;
            case StatType.ReloadTime: return reloadTime;
            case StatType.Luck: return luck;
            case StatType.CostRecovery: return costRecovery;
            case StatType.Damage: return damage;
            case StatType.Range: return range;
            case StatType.MagazineSize: return magazineSize;
            case StatType.BulletSize: return bulletSize;
            case StatType.BulletSpeed: return bulletSpeed;
            case StatType.Multishot: return multishot;
            case StatType.MultishotInterval: return multishotInterval;
            case StatType.GematriaChance: return gematriaChance;
            case StatType.SchaleChance: return schaleChance;
            case StatType.GrenadeDamage: return grenadeDamage;
            case StatType.GrenadeSelfDamage: return grenadeSelfDamage;
            case StatType.GrenadeRadius: return grenadeRadius;
            case StatType.GrenadeRange: return grenadeRange;
            case StatType.HackDamage: return hackDamage;
            case StatType.HackRadius: return hackRadius;
            case StatType.HackDuration: return hackDuration;
            default: return 0f;
        }
    }

    // [테스트 프로필 연동] GetBaseFieldValue()의 짝입니다 - 스탯 종류별 기본값(base) 필드에 값을
    // 씁니다. 테스트 씬의 PlayerTestProfile이 "플레이어가 켜지기 전(Awake 전)"에 호출해서, 하한선
    // 계산(CacheMinValues)/시작 체력/탄창 등이 전부 덮어쓴 값 기준으로 시작되게 합니다. 정수
    // 스탯(MagazineSize/Multishot)은 반올림합니다. 새 스탯을 추가할 때는 위 GetBaseFieldValue와
    // 함께 여기에도 추가해주세요.
    public void SetBaseFieldValue(StatType type, float value)
    {
        switch (type)
        {
            case StatType.MaxHealth: maxHealth = value; break;
            case StatType.MoveSpeed: moveSpeed = value; break;
            case StatType.AttackSpeed: attackSpeed = value; break;
            case StatType.ReloadTime: reloadTime = value; break;
            case StatType.Luck: luck = value; break;
            case StatType.CostRecovery: costRecovery = value; break;
            case StatType.Damage: damage = value; break;
            case StatType.Range: range = value; break;
            case StatType.MagazineSize: magazineSize = Mathf.RoundToInt(value); break;
            case StatType.BulletSize: bulletSize = value; break;
            case StatType.BulletSpeed: bulletSpeed = value; break;
            case StatType.Multishot: multishot = Mathf.RoundToInt(value); break;
            case StatType.MultishotInterval: multishotInterval = value; break;
            case StatType.GematriaChance: gematriaChance = Mathf.Clamp01(value); break;
            case StatType.SchaleChance: schaleChance = Mathf.Clamp01(value); break;
            case StatType.GrenadeDamage: grenadeDamage = value; break;
            case StatType.GrenadeSelfDamage: grenadeSelfDamage = value; break;
            case StatType.GrenadeRadius: grenadeRadius = value; break;
            case StatType.GrenadeRange: grenadeRange = value; break;
            case StatType.HackDamage: hackDamage = value; break;
            case StatType.HackRadius: hackRadius = value; break;
            case StatType.HackDuration: hackDuration = value; break;
            default:
                Debug.LogWarning($"PlayerStats: SetBaseFieldValue()에 {type} 매핑이 없습니다. 새 스탯이면 이 메서드에 추가해주세요.");
                break;
        }
    }

    // 아이템 획득 시 StatModifierEffect(또는 향후 다른 IPassiveItemEffect 구현체)가 호출합니다.
    // MaxHealth 모디파이어가 추가되는 경우, 적용 전/후 최종 MaxHealth 차이를
    // OnMaxHealthChanged로 흘려보내 PlayerHealth의 currentHealth가 같이 움직이도록 합니다.
    public void AddModifier(StatType type, ModifierType modifierType, float value, string sourceItemId)
    {
        float previousMaxHealth = type == StatType.MaxHealth ? MaxHealth : 0f;
        // [최대 체력 상한] 상한을 적용하기 전 값 - 상한에 막혀 못 오른 양을 계산하는 데 씁니다.
        float previousRawMaxHealth = type == StatType.MaxHealth ? RawMaxHealth : 0f;

        if (!activeModifiers.TryGetValue(type, out var list))
        {
            list = new List<AppliedModifier>();
            activeModifiers[type] = list;
        }

        list.Add(new AppliedModifier
        {
            modifierType = modifierType,
            value = value,
            sourceItemId = sourceItemId,
        });

        if (type == StatType.MaxHealth)
        {
            // [최대 체력 상한] 올라야 했던 양(상한 전)을 먼저 구한 뒤, 상한을 넘은 만큼은 기억하지 않도록 깎습니다.
            float rawGain = RawMaxHealth - previousRawMaxHealth;
            TrimMaxHealthToCap();

            float delta = MaxHealth - previousMaxHealth;
            if (!Mathf.Approximately(delta, 0f))
            {
                OnMaxHealthChanged?.Invoke(delta);
            }

            // [최대 체력 상한] 올라야 했던 양 중 상한에 막혀 실제로 오르지 못한 양만큼 회복을 요청합니다.
            if (rawGain > 0f)
            {
                float blocked = rawGain - Mathf.Max(delta, 0f);
                if (blocked > 0.001f)
                {
                    OnMaxHealthIncreaseCapped?.Invoke(blocked);
                }
            }
        }
    }

    // 지금 당장 쓰이지는 않지만, 나중에 "저주받은 아이템 해제"나 "임시 효과 종료"처럼
    // 특정 아이템이 건 효과만 제거해야 하는 상황을 위해 미리 열어둔 훅입니다.
    // MaxHealth 모디파이어가 제거되어 최종값이 줄어드는 경우에도 AddModifier와 동일하게
    // OnMaxHealthChanged로 delta(음수)를 흘려보내 currentHealth가 같이 줄어들도록 합니다.
    public void RemoveModifiersFromSource(string sourceItemId)
    {
        float previousMaxHealth = MaxHealth;

        foreach (var list in activeModifiers.Values)
        {
            list.RemoveAll(m => m.sourceItemId == sourceItemId);
        }

        float delta = MaxHealth - previousMaxHealth;
        if (!Mathf.Approximately(delta, 0f))
        {
            OnMaxHealthChanged?.Invoke(delta);
        }
    }

    // [패시브 잃기 - 48번 등] 특정 아이템이 건 모디파이어 중 keepType 스탯만 남기고 나머지를 제거합니다.
    // 패시브를 잃을 때 "기본 스탯은 사라지지만 최대 체력은 남긴다"(형빈 확정)를 위해 PlayerInventory가
    // keepType = MaxHealth로 호출합니다. 남기는 스탯이 MaxHealth라 최대 체력은 바뀌지 않지만, 다른 스탯을 남기도록
    // 호출할 때를 위해 MaxHealth 변화가 있으면 RemoveModifiersFromSource와 똑같이 알립니다.
    public void RemoveModifiersFromSourceExcept(string sourceItemId, StatType keepType)
    {
        float previousMaxHealth = MaxHealth;

        foreach (var pair in activeModifiers)
        {
            if (pair.Key == keepType) continue;
            pair.Value.RemoveAll(m => m.sourceItemId == sourceItemId);
        }

        float delta = MaxHealth - previousMaxHealth;
        if (!Mathf.Approximately(delta, 0f))
        {
            OnMaxHealthChanged?.Invoke(delta);
        }
    }

    // (base + 모든 Additive 합) x (모든 Multiplicative 곱), 하한선 clamp까지 적용한
    // 최종값을 계산합니다. 확률 스탯(GematriaChance/SchaleChance)은 추가로 0~1 범위로도
    // clamp합니다 (확률은 항상 유효 범위 안에 있어야 하므로).
    private float Compute(StatType type, float baseValue)
    {
        float result = ComputeUncapped(type, baseValue);

        // [최대 체력 상한] 최종값을 상한에서 자릅니다. 상한을 넘는 증가분은 AddModifier에서 곧바로 깎아 두므로(TrimMaxHealthToCap)
        // 평소에는 상한 전 값과 같고, 이 자르기는 안전장치입니다.
        if (type == StatType.MaxHealth && maxHealthCap > 0f && !testNoMaxHealthCap)
        {
            result = Mathf.Min(result, maxHealthCap);
        }

        return result;
    }

    // 상한(maxHealthCap)을 적용하기 전 최대 체력입니다. 상한에 막힌 양을 계산할 때만 씁니다.
    private float RawMaxHealth => ComputeUncapped(StatType.MaxHealth, maxHealth);

    // 최대 체력에 걸린 Multiplicative 모디파이어의 곱입니다(없으면 1). 최대 체력을 정해진 양만큼 깎는 Additive 값을 계산할 때 씁니다.
    private float MaxHealthMultiplier()
    {
        float product = 1f;
        if (activeModifiers.TryGetValue(StatType.MaxHealth, out var list))
        {
            foreach (var mod in list)
            {
                if (mod.modifierType == ModifierType.Multiplicative) product *= mod.value;
            }
        }
        return product;
    }

    // 최대 체력 최종값이 amount만큼 줄도록 전용 출처의 음수 Additive 모디파이어를 더합니다(이벤트는 부르는 쪽이 보냄).
    private void AddMaxHealthReduction(float amount, string source)
    {
        if (amount <= 0f) return;
        float product = MaxHealthMultiplier();
        if (product <= 0f) return;

        if (!activeModifiers.TryGetValue(StatType.MaxHealth, out var list))
        {
            list = new List<AppliedModifier>();
            activeModifiers[StatType.MaxHealth] = list;
        }
        list.Add(new AppliedModifier
        {
            modifierType = ModifierType.Additive,
            value = -amount / product,
            sourceItemId = source,
        });
    }

    // [최대 체력 상한] 상한 전 최대 체력이 상한을 넘었으면 넘은 만큼 깎아서, 초과분을 기억하지 않게 합니다. 테스트용 상한 해제
    // (testNoMaxHealthCap)가 켜져 있거나 상한이 없으면 아무것도 하지 않습니다.
    private void TrimMaxHealthToCap()
    {
        if (maxHealthCap <= 0f || testNoMaxHealthCap) return;
        float excess = RawMaxHealth - maxHealthCap;
        if (excess > 0.0001f) AddMaxHealthReduction(excess, CapTrimSource);
    }

    // [다음 전투 레벨 진입 시 최대 체력 정리 - 형빈 확정] 채워지지 않은 최대 체력 칸을 1칸 단위로 지웁니다. currentHealth는 지금
    // 현재 체력입니다. 예) 3/5 -> 3/3, 3.5/5 -> 3.5/4. 최소 1칸은 남깁니다. 지운 양(0이면 지우지 않음)을 돌려줍니다.
    // 현재 체력은 바꾸지 않아야 하므로 PlayerHealth.ApplyLevelEndMaxHealthLoss를 거쳐 부릅니다(GameFlowManager).
    public float ApplyLevelEndMaxHealthLoss(float currentHealth)
    {
        float previousMaxHealth = MaxHealth;
        float missing = previousMaxHealth - Mathf.Max(currentHealth, 0f);
        float loss = Mathf.Floor(missing + 0.001f); // 빈 칸 중 온전한 1칸 단위만(0.5칸 빈 것은 남김)
        if (loss <= 0f) return 0f;

        float target = Mathf.Max(MinMaxHealthAfterLevelLoss, previousMaxHealth - loss);
        AddMaxHealthReduction(previousMaxHealth - target, LevelEndLossSource);

        float delta = MaxHealth - previousMaxHealth;
        if (!Mathf.Approximately(delta, 0f))
        {
            OnMaxHealthChanged?.Invoke(delta);
        }
        return -delta;
    }

    // 원래의 Compute 계산(아이템 효과 + 하한선 + 확률 0~1)입니다. 최대 체력 상한만 빠져 있습니다.
    private float ComputeUncapped(StatType type, float baseValue)
    {
        float additiveSum = 0f;
        float multiplicativeProduct = 1f;

        if (activeModifiers.TryGetValue(type, out var list))
        {
            foreach (var mod in list)
            {
                if (mod.modifierType == ModifierType.Additive) additiveSum += mod.value;
                else multiplicativeProduct *= mod.value;
            }
        }

        float result = (baseValue + additiveSum) * multiplicativeProduct;

        if (minValues.TryGetValue(type, out float min))
        {
            result = Mathf.Max(result, min);
        }

        if (type == StatType.GematriaChance || type == StatType.SchaleChance)
        {
            result = Mathf.Clamp01(result);
        }

        return result;
    }

    // [87번 아군 공격력 연동] 플레이어 대신 baseValue를 기본값으로 삼아 이 스탯의 아이템 효과만 적용한 값을 돌려줍니다:
    // (baseValue + 모든 Additive 합) x (모든 Multiplicative 곱). 하한선(statFloors)은 적용하지 않습니다 - 하한선은 부르는 쪽이
    // 자기 기준으로 겁니다(AllyFaction은 고정 1). 플레이어 자신의 스탯 계산(Compute)에는 아무 영향이 없습니다.
    public float ApplyModifiersTo(StatType type, float baseValue)
    {
        float additiveSum = 0f;
        float multiplicativeProduct = 1f;

        if (activeModifiers.TryGetValue(type, out var list))
        {
            foreach (var mod in list)
            {
                if (mod.modifierType == ModifierType.Additive) additiveSum += mod.value;
                else multiplicativeProduct *= mod.value;
            }
        }

        return (baseValue + additiveSum) * multiplicativeProduct;
    }

    // 이 스탯의 하한선 값을 돌려줍니다(statFloors 설정 기준 - Ratio는 런 시작 시점 기본값 x 비율,
    // Absolute는 고정값). 하한선이 설정되지 않은 스탯이면 false를 돌려줍니다. 다른 스크립트가
    // "스탯 하한선 밑으로 내려가면 안 되는" 계산(예: 11번 아이템의 피해 감소)을 할 때 씁니다.
    public bool TryGetStatFloor(StatType type, out float floor)
    {
        return minValues.TryGetValue(type, out floor);
    }

    // ---- 다른 스크립트는 아래 프로퍼티를 통해 값을 읽습니다 ----
    // 시그니처(이름/타입)는 전부 기존과 동일하므로, 이 프로퍼티들을 쓰던 다른 스크립트는
    // 수정할 필요가 없습니다. 내부적으로만 base 값이 아니라 아이템 효과까지 반영된
    // 최종값을 계산해서 반환하도록 바뀌었습니다.
    public float MaxHealth => Compute(StatType.MaxHealth, maxHealth);
    public float MoveSpeed => Compute(StatType.MoveSpeed, moveSpeed);
    public float AttackSpeed => Compute(StatType.AttackSpeed, attackSpeed);
    public float ReloadTime => Compute(StatType.ReloadTime, reloadTime);
    public float Luck => Compute(StatType.Luck, luck);
    public float CostRecovery => Compute(StatType.CostRecovery, costRecovery);
    public float Damage => Compute(StatType.Damage, damage);
    public float Range => Compute(StatType.Range, range);
    public int MagazineSize => Mathf.Max(1, Mathf.RoundToInt(Compute(StatType.MagazineSize, magazineSize)));
    public float BulletSize => Compute(StatType.BulletSize, bulletSize);
    public float BulletSpeed => Compute(StatType.BulletSpeed, bulletSpeed);
    public int Multishot => Mathf.Max(1, Mathf.RoundToInt(Compute(StatType.Multishot, multishot)));
    public float MultishotInterval => Compute(StatType.MultishotInterval, multishotInterval);
    public float GematriaChance => Compute(StatType.GematriaChance, gematriaChance);
    public float SchaleChance => Compute(StatType.SchaleChance, schaleChance);
    public float GrenadeDamage => Compute(StatType.GrenadeDamage, grenadeDamage);
    public float GrenadeSelfDamage => Compute(StatType.GrenadeSelfDamage, grenadeSelfDamage);
    public float GrenadeRadius => Compute(StatType.GrenadeRadius, grenadeRadius);
    public float GrenadeRange => Compute(StatType.GrenadeRange, grenadeRange);
    public float HackDamage => Compute(StatType.HackDamage, hackDamage);
    public float HackRadius => Compute(StatType.HackRadius, hackRadius);
    public float HackDuration => Compute(StatType.HackDuration, hackDuration);
}
