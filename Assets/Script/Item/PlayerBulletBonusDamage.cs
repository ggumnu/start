using System;
using System.Collections.Generic;
using UnityEngine;

// "플레이어 총알이 적에게 명중할 때 더하는 추가 피해" 부류 효과를 위한 정적 등록소입니다(EnemyDamageTakenModifier와 같은 패턴 - 그쪽은 곱하기,
// 이쪽은 더하기).
//
// [규칙 - 형빈 확정 (113번 슈가 중첩)]
// - 총알이 적에게 명중하는 순간에만 더해집니다(Projectile.OnTriggerEnter). 4번 폭발/수류탄/해킹/94번 후광/102번 중독 등 다른 피해에는 붙지 않습니다.
// - 총알의 피해(공격력 배율, 11번 거리 보정, 35/69번 튕김 배율, 23번 랜덤 추가 피해까지 모두 반영된 Projectile.Damage)에 그대로 더합니다 -
//   그래서 공격력 배율/튕김 배율로 커지지 않습니다. 더한 값이 EnemyHealth.TakeDamage로 들어가므로 41번 같은 "적이 받는 피해 % 증가"는
//   이 추가 피해까지 포함한 맨 마지막에 곱해집니다.
//   최종 = (총알 피해 + 추가 피해) x 적이 받는 피해 배율
// - 4번 폭발은 Projectile.Damage를 읽으므로 이 추가 피해가 들어가지 않습니다.
// - 여러 효과가 등록되면 모두 더합니다. 등록된 것이 없으면 0 - 원래 동작과 100% 같습니다.
// - 소유자(보통 플레이어)가 사라지면 자동으로 빠집니다. 플레이 시작 때마다 비웁니다.
public static class PlayerBulletBonusDamage
{
    private class Entry
    {
        public UnityEngine.Object owner;
        public Func<EnemyHealth, float> getBonus;
    }

    private static readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        entries.Clear();
    }

    // sourceId 하나당 하나만 등록됩니다(같은 sourceId로 다시 부르면 덮어씀). getBonus는 맞는 적을 받아 그 순간 더할 피해를 돌려줍니다(없으면 0).
    public static void SetBonus(string sourceId, UnityEngine.Object owner, Func<EnemyHealth, float> getBonus)
    {
        if (string.IsNullOrEmpty(sourceId) || getBonus == null) return;
        entries[sourceId] = new Entry { owner = owner, getBonus = getBonus };
    }

    public static void RemoveBonus(string sourceId)
    {
        if (string.IsNullOrEmpty(sourceId)) return;
        entries.Remove(sourceId);
    }

    // 이 적에게 지금 총알이 명중하면 더할 추가 피해의 합입니다(0 이상).
    public static float Resolve(EnemyHealth target)
    {
        if (entries.Count == 0 || target == null) return 0f;

        PruneDead();
        float total = 0f;
        foreach (var entry in entries.Values)
        {
            float b = 0f;
            try
            {
                b = entry.getBonus(target);
            }
            catch (Exception e)
            {
                Debug.LogError($"PlayerBulletBonusDamage: 추가 피해 계산 중 오류가 발생해 이 항목은 0으로 칩니다: {e}");
            }
            if (b > 0f) total += b;
        }
        return total;
    }

    private static void PruneDead()
    {
        List<string> deadKeys = null;
        foreach (var kvp in entries)
        {
            // owner를 넣었는데 파괴된 경우만 지웁니다(owner 없이 등록한 것은 유지).
            if (!ReferenceEquals(kvp.Value.owner, null) && kvp.Value.owner == null)
            {
                if (deadKeys == null) deadKeys = new List<string>();
                deadKeys.Add(kvp.Key);
            }
        }
        if (deadKeys == null) return;
        foreach (var key in deadKeys) entries.Remove(key);
    }
}
