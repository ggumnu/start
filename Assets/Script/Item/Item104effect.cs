using System;
using UnityEngine;

// 패시브 104번 - "탄환을 하나 소모할 때마다 공격력이 3 증가한다. 장전하면 초기화된다."
//
// [기획 - 형빈 확정]
// - 탄약이 실제로 줄어들 때마다 1발당 공격력 +3(더하기 수정치 - 다른 공격력 증가 아이템과 같은 규칙. 103번 x1.3 같은 곱하기
//   배율은 그 뒤에 곱해짐). 공격력 10, 탄창 5발이면 5발을 다 쏜 뒤 25.
// - 일반 사격은 총알을 만들기 "직전"에 오르므로, 그 총알부터 오른 공격력이 들어갑니다(1발째 13, ..., 5발째 25).
// - 연사(Multishot)는 한 발 한 발마다 +3. 19번 양갈래/137번 확산은 총알이 여러 개여도 탄약 1발이라 +3 한 번.
// - 110번 충전 사격도 탄약을 "소모"하므로 같이 오릅니다(누르는 순간 1발, 충전 1회마다 1발). 충전을 취소해 탄약을 돌려받지 못해도
//   소모한 것은 그대로 셉니다. 충전 0발에서 떼서 미리 뺀 1발을 돌려받으면 그만큼(-3) 되돌립니다(바로 이어지는 일반 사격이 다시 +3).
// - 장전을 "시작"하는 순간(R 수동/탄약 0에서 클릭한 자동 장전 모두) 초기화됩니다. 장전이 도중에 취소돼도 이미 초기화된 상태입니다.
// - 탄창 크기 증가로 빈 칸이 늘어나는 것은 소모가 아니므로 오르지 않습니다.
// - 이미 날아가고 있는 총알은 쏜 순간의 공격력으로 고정되어 있어 이후의 증가/초기화에 영향받지 않습니다.
// - 잃으면(48번 등) 증가분이 사라지고 그 뒤로는 오르지 않습니다. 다시 얻으면 0부터 셉니다.
// - 공격력 하한선(PlayerStats의 Stat Floors)은 그대로 지켜집니다. 따로 상한은 없습니다(탄창 크기만큼 오를 수 있음).
// - 연출 없음(형빈 확정 - 지금은). 화면의 스탯 표시는 자동으로 바뀝니다.
//
// [구현]
// PlayerShooting의 OnAmmoConsumed / OnAmmoRefunded / OnReloadStarted 알림을 받아 "이번 탄창에서 소모한 발 수"를 세고, PlayerStats에
// Damage 더하기 수정치(소모 발 수 x 3) 하나만 걸어 둡니다(값이 바뀔 때마다 지우고 다시 검).
//
// [아이템 에셋 설정]
// ItemDefinition: Item Id = 104, Category = Passive, Special Effects에 AmmoSpentDamageEffect 하나. Stat Modifiers는 비워둡니다.
[Serializable]
public class AmmoSpentDamageEffect : IPassiveItemEffect, IRemovablePassiveEffect
{
    [Tooltip("탄약 1발을 소모할 때마다 오르는 공격력입니다(기본 3, 더하기).")]
    public float damagePerAmmo = 3f;

    // ---- 실행 중 상태(저장되지 않음) ----
    [NonSerialized] private PlayerStats stats;
    [NonSerialized] private PlayerShooting shooting;
    [NonSerialized] private string modifierSourceId;
    [NonSerialized] private int spentCount;
    [NonSerialized] private Action<int> consumedHandler;
    [NonSerialized] private Action<int> refundedHandler;
    [NonSerialized] private Action reloadHandler;

    public void Apply(PassiveEffectContext context, string sourceItemId)
    {
        if (context == null || context.Stats == null || context.Shooting == null)
        {
            Debug.LogWarning("AmmoSpentDamageEffect: 플레이어 스탯/사격 컴포넌트를 찾을 수 없어 104번 효과를 적용하지 못했습니다.");
            return;
        }

        Unsubscribe(); // 혹시 Remove 없이 다시 불려도 중복 구독되지 않도록

        stats = context.Stats;
        shooting = context.Shooting;
        modifierSourceId = sourceItemId + "_ammo_spent_damage";
        spentCount = 0;
        stats.RemoveModifiersFromSource(modifierSourceId);

        consumedHandler = HandleAmmoConsumed;
        refundedHandler = HandleAmmoRefunded;
        reloadHandler = HandleReloadStarted;
        shooting.OnAmmoConsumed += consumedHandler;
        shooting.OnAmmoRefunded += refundedHandler;
        shooting.OnReloadStarted += reloadHandler;
    }

    // [패시브 잃기 - 48번 등] 증가분을 지우고 더 이상 세지 않습니다.
    public void Remove(PassiveEffectContext context, string sourceItemId)
    {
        Unsubscribe();

        var s = context?.Stats != null ? context.Stats : stats;
        string key = modifierSourceId ?? (sourceItemId + "_ammo_spent_damage");
        if (s != null) s.RemoveModifiersFromSource(key);

        stats = null;
        shooting = null;
        spentCount = 0;
    }

    private void Unsubscribe()
    {
        if (shooting != null)
        {
            if (consumedHandler != null) shooting.OnAmmoConsumed -= consumedHandler;
            if (refundedHandler != null) shooting.OnAmmoRefunded -= refundedHandler;
            if (reloadHandler != null) shooting.OnReloadStarted -= reloadHandler;
        }
        consumedHandler = null;
        refundedHandler = null;
        reloadHandler = null;
    }

    private void HandleAmmoConsumed(int amount)
    {
        if (amount <= 0) return;
        spentCount += amount;
        RefreshModifier();
    }

    private void HandleAmmoRefunded(int amount)
    {
        if (amount <= 0) return;
        spentCount = Mathf.Max(0, spentCount - amount);
        RefreshModifier();
    }

    private void HandleReloadStarted()
    {
        if (spentCount == 0) return;
        spentCount = 0;
        RefreshModifier();
    }

    // 지금 소모한 발 수에 맞춰 Damage 더하기 수정치를 다시 겁니다(0발이면 수정치 없음).
    private void RefreshModifier()
    {
        if (stats == null || string.IsNullOrEmpty(modifierSourceId)) return;
        stats.RemoveModifiersFromSource(modifierSourceId);
        if (spentCount > 0)
        {
            stats.AddModifier(StatType.Damage, ModifierType.Additive, spentCount * damagePerAmmo, modifierSourceId);
        }
    }
}
