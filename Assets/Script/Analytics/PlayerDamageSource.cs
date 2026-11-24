using UnityEngine;

// [플레이 통계 - 사망 원인 (형빈 확정)]
// 플레이어에게 피해를 준 원인입니다. PlayerHealth.TakeDamage(피해량, 원인)으로 넘기면 체력이 실제로 깎일 때
// PlayerHealth.LastDamageSource에 남고, 플레이어가 죽는 순간 플레이 통계(RunAnalytics)가 이 값을 사망 원인으로 보냅니다.
//
// - attacker(누가): melee(근접 적) / ranged(원거리 적) / boss_lv1 / boss_lv2 / self(내 수류탄·포격 자해) / special_room(체력 소모 특수방) / unknown(기타)
// - attack(어떻게): contact(몸 접촉) / dash(돌진) / bullet(탄환) / artillery(보스 포격) / grenade(수류탄) / item_artillery(아이템 포격) / health_room / unknown
// - enemyName: 적 프리팹 이름("(Clone)" 제거). 적이 아니면 빈 문자열.
//
// 원인을 넘기지 않는 예전 방식 TakeDamage(피해량)은 "unknown"(기타)으로 기록됩니다. 플레이어에게 피해를 주는 코드를 새로 만들면
// 아래 미리 만든 값(GrenadeSelf 등)이나 FromEnemy()로 원인을 같이 넘겨 주세요 - 안 넘겨도 오류는 없고 통계에서 "기타"로 보입니다.
public readonly struct PlayerDamageSource
{
    public const string AttackerMelee = "melee";
    public const string AttackerRanged = "ranged";
    public const string AttackerBossLv1 = "boss_lv1";
    public const string AttackerBossLv2 = "boss_lv2";
    public const string AttackerBossLv3 = "boss_lv3";
    public const string AttackerSelf = "self";
    public const string AttackerSpecialRoom = "special_room";
    public const string AttackerUnknown = "unknown";

    public const string AttackContact = "contact";
    public const string AttackDash = "dash";
    public const string AttackBullet = "bullet";
    public const string AttackArtillery = "artillery";
    public const string AttackGrenade = "grenade";
    public const string AttackItemArtillery = "item_artillery";
    public const string AttackHealthRoom = "health_room";
    public const string AttackLaser = "laser"; // [3레벨 보스] 레이저
    public const string AttackUnknown = "unknown";

    public readonly string attacker;
    public readonly string attack;
    public readonly string enemyName;

    public PlayerDamageSource(string attacker, string attack, string enemyName)
    {
        this.attacker = string.IsNullOrEmpty(attacker) ? AttackerUnknown : attacker;
        this.attack = string.IsNullOrEmpty(attack) ? AttackUnknown : attack;
        this.enemyName = enemyName ?? "";
    }

    // 값이 채워져 있는지입니다(아무것도 안 넣은 기본값이면 false).
    public bool IsSet => !string.IsNullOrEmpty(attacker);

    public static PlayerDamageSource Unknown => new PlayerDamageSource(AttackerUnknown, AttackUnknown, "");
    public static PlayerDamageSource GrenadeSelf => new PlayerDamageSource(AttackerSelf, AttackGrenade, "");
    public static PlayerDamageSource ItemArtillerySelf => new PlayerDamageSource(AttackerSelf, AttackItemArtillery, "");
    public static PlayerDamageSource HealthSpecialRoom => new PlayerDamageSource(AttackerSpecialRoom, AttackHealthRoom, "");

    // 적의 아무 부품(공격 컴포넌트, 접촉 트리거, EnemyHealth 등)을 넘기면 그 적이 어떤 종류인지 판별해 원인을 만듭니다.
    // 판별: 보스 컨트롤러가 있으면 보스(2레벨 보스 전용 컨트롤러면 boss_lv2, 3레벨이면 boss_lv3, 아니면 boss_lv1) -> 원거리 공격(EnemyRangedAttack)이 있으면 ranged -> 나머지는 melee.
    // 어떤 경우에도 예외를 밖으로 내보내지 않습니다(피해 처리를 절대 막지 않도록).
    public static PlayerDamageSource FromEnemy(Component anyPartOfEnemy, string attack)
    {
        try
        {
            if (anyPartOfEnemy == null) return new PlayerDamageSource(AttackerUnknown, attack, "");

            var health = anyPartOfEnemy.GetComponentInParent<EnemyHealth>();
            GameObject root = health != null ? health.gameObject : anyPartOfEnemy.gameObject;
            return new PlayerDamageSource(ClassifyEnemy(root), attack, CleanName(root.name));
        }
        catch (System.Exception)
        {
            return new PlayerDamageSource(AttackerUnknown, attack, "");
        }
    }

    private static string ClassifyEnemy(GameObject root)
    {
        if (root == null) return AttackerUnknown;

        var boss = root.GetComponentInChildren<BossAIController>(true);
        if (boss != null)
        {
            if (boss is BossLv2AIController) return AttackerBossLv2;
            if (boss is BossLv3AIController) return AttackerBossLv3;
            return AttackerBossLv1;
        }

        if (root.GetComponentInChildren<EnemyRangedAttack>(true) != null) return AttackerRanged;
        return AttackerMelee;
    }

    private static string CleanName(string name)
    {
        if (string.IsNullOrEmpty(name)) return "";
        return name.Replace("(Clone)", "").Trim();
    }
}
