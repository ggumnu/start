using System;
using System.Collections.Generic;
using UnityEngine;

// 테스트 씬(ItemTestArena) 전용 "플레이어 덮어쓰기 값" 에셋입니다.
//
// [왜 만들었나 - 형빈 확정]
// 예전에는 테스트 씬에서 진짜 플레이어 프리팹을 복제한 레플리카(Reisa_Player_Test)에 테스트용
// 스탯/자원(공격력 100, 수류탄 100개 등)을 넣어 썼는데, 진짜 프리팹에 새로 연결한 인스펙터 값
// (장전 클립, 총 모델 등)이 레플리카에는 빠져 있어서 테스트 결과가 실제와 달라지는 문제가 있었습니다.
// 이제는 테스트 씬도 진짜 플레이어 프리팹을 그대로 쓰고, 이 에셋에 적어둔 값만 그 위에 덮어씁니다.
// 덮어쓰지 않은 모든 값은 진짜 프리팹을 그대로 따라갑니다.
//
// [만드는 방법]
// Project 창 빈 곳 우클릭 > Create > MobArchive > Player Test Profile
// 만든 에셋을 테스트 씬의 ItemTestArenaBuilder > Test Profile 칸에 연결하면 됩니다. 실제 게임 씬
// (LevelBuilder)에는 이 칸이 없으므로 절대 적용되지 않습니다.
//
// [적용 시점]
// ItemTestArenaBuilder가 플레이어를 "꺼진 상태"로 생성한 뒤 ApplyTo()를 호출하고, 그 다음에 켭니다.
// 그래서 각 컴포넌트의 시작 처리(Awake - 시작 체력 = 최대 체력, 탄창 가득, 시작 자원 지급, 스탯
// 하한선 계산)가 전부 덮어쓴 값 기준으로 한 번만 실행됩니다. 스탯 하한선(Ratio 모드)도 덮어쓴 값
// 기준입니다(예: 공격력 100이면 하한선 50).
//
// [Play 중 변경]
// 스탯/자원 값은 플레이어가 생성되는 순간에만 적용됩니다(Play 중에 바꾸면 다음 Play부터 반영).
// 무적 모드/자원 무한 체크박스는 ItemTestArenaBuilder가 매 프레임 다시 반영하므로 Play 중에도 바로
// 켜고 끌 수 있습니다. 에셋이라 Play 중에 바꾼 값은 Play를 꺼도 그대로 남습니다.
[CreateAssetMenu(fileName = "PlayerTestProfile", menuName = "MobArchive/Player Test Profile")]
public class PlayerTestProfile : ScriptableObject
{
    [Serializable]
    public class StatOverride
    {
        [Tooltip("덮어쓸 스탯 종류입니다.")]
        public StatType stat;
        [Tooltip("이 스탯의 기본값(base)을 이 값으로 바꿉니다. 탄창/연사처럼 정수 스탯은 반올림됩니다.")]
        public float value;
    }

    [Serializable]
    public class IntOverride
    {
        [Tooltip("체크한 항목만 덮어씁니다. 해제하면 진짜 프리팹 값을 그대로 씁니다.")]
        public bool enabled;
        [Min(0)] public int value;
    }

    [Header("스탯 덮어쓰기 (목록에 넣은 스탯만 바뀝니다)")]
    [Tooltip("+ 버튼으로 줄을 추가하고 스탯 종류와 값을 고르세요. 같은 스탯을 두 번 넣으면 아래쪽 값이 적용됩니다.")]
    public List<StatOverride> statOverrides = new List<StatOverride>();

    [Header("자원 덮어쓰기 (체크한 항목만 바뀝니다)")]
    public IntOverride startingGrenade = new IntOverride();
    public IntOverride startingHackingTool = new IntOverride();
    public IntOverride startingCredit = new IntOverride();
    public IntOverride startingPyroxenes = new IntOverride();
    public IntOverride startingCost = new IntOverride();
    [Tooltip("코스트 상한선입니다(최소 1). 시작 코스트를 크게 줄 때는 이 값도 함께 올려야 상한선에 잘리지 않습니다.")]
    public IntOverride maxCost = new IntOverride();

    [Header("보호막 (테스트용 - 체력 UI 개편 때 추가, 나중에 지워도 됨)")]
    [Tooltip("체크하면 시작 보호막 개수를 이 값으로 덮어씁니다(PlayerHealth.startingShield, 최대 보호막 5개까지).")]
    public IntOverride startingShield = new IntOverride();
    [Tooltip("Play 중에 체크하면 보호막을 위 '시작 보호막' 값(체크 안 했으면 최대치)으로 바로 채우고, 체크는 자동으로 풀립니다. " +
             "보호막 UI/파괴 연출을 반복해서 확인할 때 쓰세요.")]
    public bool refillShieldNow = false;

    [Header("상한 해제 (테스트 씬 전용 - Play 중에도 바로 켜고 끌 수 있음)")]
    [Tooltip("켜면 최대 체력 상한(10)이 없어집니다. 체력 칸은 그래도 10칸까지만 보입니다.")]
    public bool noMaxHealthCap = true;
    [Tooltip("켜면 보호막 최대 개수(5) 제한이 없어집니다. 시작 보호막/보호막 채우기를 99처럼 크게 넣어도 그대로 들어갑니다. " +
             "보호막 바는 그래도 5칸까지만 나눠 보입니다.")]
    public bool noShieldCap = true;

    [Header("테스트 편의 (Play 중에도 바로 켜고 끌 수 있음)")]
    [Tooltip("켜면 플레이어가 어떤 피해도 받지 않습니다(적 공격/접촉/수류탄 자해 모두). 체력이 깎이지 않으므로 " +
             "피격 점멸과 '피격 시' 아이템 효과도 발생하지 않습니다. 피격 이펙트(CombatVfxSpawner)는 그대로 뜹니다.")]
    public bool invincible = false;
    [Tooltip("켜면 수류탄/해킹툴/코스트를 써도 줄지 않습니다(0개인 자원은 여전히 쓸 수 없음).")]
    public bool infiniteResources = false;

    // 플레이어가 켜지기 전(Awake 전)에 호출합니다. 스탯/자원 값 + 테스트 스위치를 적용합니다.
    public void ApplyTo(GameObject player)
    {
        if (player == null) return;

        var stats = player.GetComponent<PlayerStats>();
        if (stats != null && statOverrides != null)
        {
            foreach (var o in statOverrides)
            {
                if (o == null) continue;
                stats.SetBaseFieldValue(o.stat, o.value);
            }
        }

        var resources = player.GetComponent<PlayerResources>();
        if (resources != null)
        {
            if (startingGrenade.enabled) resources.startingGrenade = startingGrenade.value;
            if (startingHackingTool.enabled) resources.startingHackingTool = startingHackingTool.value;
            if (startingCredit.enabled) resources.startingCredit = startingCredit.value;
            if (startingPyroxenes.enabled) resources.startingPyroxenes = startingPyroxenes.value;
            if (startingCost.enabled) resources.startingCost = startingCost.value;
            if (maxCost.enabled) resources.maxCost = Mathf.Max(1, maxCost.value);
        }

        // [상한 해제] Awake 전에 켜 둬야 시작 체력/시작 보호막이 상한에 잘리지 않습니다.
        ApplyCapToggles(player);

        // [보호막 테스트] Awake 전이라 시작 보호막 값만 바꿔 두면 PlayerHealth가 시작할 때 그만큼 가지고 시작합니다.
        var shieldHealth = player.GetComponent<PlayerHealth>();
        if (shieldHealth != null && startingShield.enabled) shieldHealth.startingShield = startingShield.value;

        ApplyToggles(player);
    }

    // 무적 모드/자원 무한 스위치만 반영합니다. ItemTestArenaBuilder가 매 프레임 호출합니다.
    public void ApplyToggles(GameObject player)
    {
        if (player == null) return;

        var health = player.GetComponent<PlayerHealth>();
        if (health != null) health.testInvincible = invincible;

        ApplyCapToggles(player);

        // [보호막 테스트] '보호막 채우기'를 체크하면 한 번 채우고 체크를 풉니다.
        if (refillShieldNow)
        {
            refillShieldNow = false;
            if (health != null) health.SetShield(startingShield.enabled ? startingShield.value : health.maxShield);
        }

        var resources = player.GetComponent<PlayerResources>();
        if (resources != null) resources.testInfiniteResources = infiniteResources;
    }

    // 체력/보호막 상한 해제 스위치를 플레이어에 반영합니다(ApplyTo와 매 프레임 ApplyToggles에서 호출).
    private void ApplyCapToggles(GameObject player)
    {
        var stats = player.GetComponent<PlayerStats>();
        if (stats != null) stats.testNoMaxHealthCap = noMaxHealthCap;

        var health = player.GetComponent<PlayerHealth>();
        if (health != null) health.testNoShieldCap = noShieldCap;
    }
}
