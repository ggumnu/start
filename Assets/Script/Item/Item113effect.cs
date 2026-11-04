using UnityEngine;

// 패시브 113번 - "적에게 총알을 적중시킬 때마다 슈가 중첩을 쌓는다. 총알이 적중할 때 중첩 하나당 5의 추가 피해를 입힌다."
//
// [기획 - 형빈 확정]
// - 플레이어 총알이 적에게 명중할 때마다 그 적에게 슈가 중첩 +1(적이 죽을 때까지 무한히). 5번 분열탄, 99번 관통, 69번 튕김, 110번 충전탄
//   (관통 포함), 19/137번 여러 발 모두 명중할 때마다 각각 +1입니다. 4번 폭발/수류탄/해킹툴 등 총알이 아닌 피해로는 쌓이지 않습니다.
// - 추가 피해도 총알 명중에만 붙습니다: "지금까지 쌓인 중첩 x 5"를 더한 뒤 그 명중으로 중첩 +1.
//   예) 공격력 10: 첫 명중 10(중첩 0 -> 1), 두 번째 15(1 -> 2), 세 번째 20(2 -> 3).
//   폭발/수류탄/해킹/94번 후광/102번 중독 등 다른 피해에는 추가 피해가 없습니다.
// - 추가 피해는 총알 피해(공격력 배율, 11번 거리 보정, 35/69번 튕김 배율, 23번 랜덤 추가 피해까지 반영된 값)에 그대로 더하는 고정값이라
//   그런 배율로 커지지 않습니다. 41번처럼 "적이 받는 피해 % 증가"는 맨 마지막에 이 합계 전체에 곱해집니다.
//   최종 = (총알 피해 + 중첩 x 5) x 적이 받는 피해 배율. 피해 숫자는 이 합계 하나로 뜹니다.
// - 그 총알로 적이 죽으면 중첩은 쌓지 않습니다(의미 없음). 보스에게도 쌓입니다. 방을 탈출해 적이 다시 만들어지면 중첩도 0부터입니다.
// - 113번을 잃으면(48번 등) 모든 적의 중첩이 사라지고 추가 피해/표시도 멈춥니다. 다시 얻으면 0부터 다시 쌓습니다.
// - 표시: 중첩이 1 이상인 동안 항상, 적 몸통 오른쪽 아래에 113번 아이콘 + 중첩 수(검은 글자 + 흰 테두리 - 110번 충전 표시와 같은 모양).
//
// [구현]
// - 추가 피해: PlayerBulletBonusDamage에 "이 적의 중첩 x 5"를 등록 -> Projectile이 명중 피해에 더함(피해를 준 "뒤" 중첩이 쌓이므로 쌓이기 전 기준).
// - 중첩: Projectile.OnPlayerBulletHitEnemy(피해를 준 직후 알림)에서 EnemySugarStack에 +1.
// - 표시: SugarStackDisplay(EnemySugarStack.cs).
//
// [아이템 에셋 설정]
// ItemDefinition: Item Id = 113, Category = Passive, Special Effects에 SugarStackEffect 하나. Stat Modifiers는 비워둡니다.
[System.Serializable]
public class SugarStackEffect : IPassiveItemEffect, IRemovablePassiveEffect
{
    [Tooltip("중첩 하나당 총알 명중 추가 피해입니다(기본 5).")]
    [Min(0f)] public float bonusDamagePerStack = 5f;

    [Tooltip("총알 한 번 명중에 쌓이는 중첩 수입니다(기본 1).")]
    [Min(1)] public int stacksPerHit = 1;

    [Header("중첩 표시 (적 몸통 오른쪽 아래)")]
    [Tooltip("표시할 아이콘입니다. 비워두면 113번 아이템 아이콘을 씁니다.")]
    public Sprite iconOverride;

    public SugarStackDisplay.Settings display = new SugarStackDisplay.Settings();

    [System.NonSerialized] private bool subscribed;
    [System.NonSerialized] private string sourceId;
    [System.NonSerialized] private PlayerInventory inventory;
    [System.NonSerialized] private GameObject owner;
    [System.NonSerialized] private Sprite cachedIcon;

    private string BonusKey => "sugar_stack_" + sourceId;

    public void Apply(PassiveEffectContext context, string sourceItemId)
    {
        if (context == null || context.PlayerObject == null || string.IsNullOrEmpty(sourceItemId))
        {
            Debug.LogWarning("SugarStackEffect: 플레이어/아이템 ID를 찾을 수 없어 113번 효과를 적용하지 못했습니다.");
            return;
        }

        Unsubscribe();

        sourceId = sourceItemId;
        owner = context.PlayerObject;
        inventory = context.Inventory != null ? context.Inventory : owner.GetComponent<PlayerInventory>();
        cachedIcon = null;

        // 새로 얻으면 0부터(혹시 남아 있던 중첩이 있으면 지움).
        EnemySugarStack.ClearAll();

        float perStack = bonusDamagePerStack;
        PlayerBulletBonusDamage.SetBonus(BonusKey, owner, enemy => EnemySugarStack.GetStacks(enemy) * perStack);

        Projectile.OnPlayerBulletHitEnemy += OnBulletHitEnemy;
        subscribed = true;

        SugarStackDisplay.Configure(display, ResolveIcon);
    }

    // [패시브 잃기 - 48번 등] 모든 적의 중첩을 지우고 추가 피해/표시를 멈춥니다.
    public void Remove(PassiveEffectContext context, string sourceItemId)
    {
        Unsubscribe();
        if (!string.IsNullOrEmpty(sourceId)) PlayerBulletBonusDamage.RemoveBonus(BonusKey);
        else if (!string.IsNullOrEmpty(sourceItemId)) PlayerBulletBonusDamage.RemoveBonus("sugar_stack_" + sourceItemId);
        EnemySugarStack.ClearAll();
        SugarStackDisplay.Disable();
        owner = null;
        inventory = null;
        cachedIcon = null;
    }

    private void Unsubscribe()
    {
        Projectile.OnPlayerBulletHitEnemy -= OnBulletHitEnemy;
        subscribed = false;
    }

    // 총알이 적에게 피해를 준 직후에 불립니다(Projectile). 이 명중으로 중첩을 쌓습니다.
    private void OnBulletHitEnemy(EnemyHealth enemy)
    {
        if (!subscribed) return;
        // 플레이어가 사라졌는데(이전 플레이 등) 구독이 남아 있으면 정리만 합니다.
        if (owner == null) { Unsubscribe(); return; }
        if (enemy == null || enemy.IsDead) return;

        try
        {
            var stack = EnemySugarStack.GetOrAdd(enemy.gameObject);
            if (stack != null) stack.AddStack(stacksPerHit);
        }
        catch (System.Exception e)
        {
            // 명중 알림 안에서 실행되므로, 오류가 나도 총알 처리가 막히지 않게 이 적만 건너뜁니다.
            Debug.LogError($"SugarStackEffect: 슈가 중첩을 쌓는 중 오류가 발생해 이 적은 건너뜁니다: {e}");
        }
    }

    // 표시할 아이콘: 지정한 아이콘 -> 보유 중인 113번 아이템 아이콘(획득 순간엔 아직 목록에 없을 수 있어 그때그때 찾음).
    private Sprite ResolveIcon()
    {
        if (iconOverride != null) return iconOverride;
        if (cachedIcon != null) return cachedIcon;
        if (inventory == null) return null;

        foreach (var item in inventory.OwnedItems)
        {
            if (item != null && item.itemId == sourceId)
            {
                cachedIcon = item.icon;
                break;
            }
        }
        return cachedIcon;
    }
}
