using System;

// PlayerStats의 모든 스탯을 아이템 시스템에서 참조하기 위한 식별자입니다.
// PlayerStats에 새 스탯 필드를 추가하면 이 enum에도 같이 추가하고,
// PlayerStats.CacheBaseAndMinValues() / Compute() 쪽 매핑도 함께 갱신해주세요.
public enum StatType
{
    MaxHealth,
    MoveSpeed,
    AttackSpeed,
    ReloadTime,
    Luck,
    CostRecovery,
    Damage,
    Range,
    MagazineSize,
    BulletSize,
    BulletSpeed,
    Multishot,
    MultishotInterval,
    GematriaChance,
    SchaleChance,

    // [수류탄 스탯] 반드시 enum 맨 끝에 추가합니다 - enum은 숫자로 저장되므로 중간에 끼워넣으면
    // 이미 만들어둔 아이템 에셋들의 스탯 종류가 한 칸씩 밀려버립니다.
    GrenadeDamage,
    GrenadeSelfDamage,
    GrenadeRadius,
    GrenadeRange,

    // [해킹툴 스탯] 수류탄과 마찬가지로 반드시 enum 맨 끝에 추가합니다.
    HackDamage,
    HackRadius,
    HackDuration,
}

// 스탯 수정 방식입니다.
// Additive: 여러 아이템의 값을 전부 더한 뒤 base 값에 합산 (예: +1, +0.5)
// Multiplicative: 여러 아이템의 배율을 전부 곱함. value 자체가 배율입니다
//                 (예: 1.2 = 20% 증가, 0.8 = 20% 감소. 0이나 음수는 넣지 마세요)
public enum ModifierType
{
    Additive,
    Multiplicative,
}

// 아이템 하나가 스탯 하나에 가하는 효과 한 조각입니다.
// ItemDefinition.statModifiers 리스트의 원소로 인스펙터에서 직접 등록합니다.
// 계산 순서는 "이 스탯에 걸린 모든 Additive를 먼저 다 더하고, 그 다음 모든
// Multiplicative를 곱하는" 방식입니다 (아이템 획득 순서에 따라 결과가 달라지지 않도록).
[Serializable]
public class StatModifier
{
    public StatType statType;
    public ModifierType modifierType = ModifierType.Additive;

    [UnityEngine.Tooltip("Additive면 더해질 값(예: 1 = +1), Multiplicative면 배율 자체입니다(예: 1.2 = x1.2).")]
    public float value;
}