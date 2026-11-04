using System;
using System.Collections.Generic;
using UnityEngine;

// "적이 받는 피해 % 증가" 부류 효과를 위한 정적 등록소입니다(CreditGainBonusOverride/EnemySpeedModifier와 같은 패턴).
// 아이템이 "이 적에게 지금 몇 배를 적용할지"를 돌려주는 함수를 등록해두면, EnemyHealth.TakeDamage()가 피해를 적용하기
// 직전에 등록된 모든 배율을 곱해서(Resolve) 최종 피해량을 만듭니다.
//
// [규칙 - 형빈 확정]
// - 곱연산: 여러 효과가 동시에 걸리면 배율끼리 곱합니다(예: 1.5배 + 1.2배 = 1.8배).
// - 최종 피해 기준: 총알의 덧셈형 피해 보너스(11번/35번 등 Projectile 쪽)까지 다 더해진 값에 곱해지므로, 그런 보너스와도
//   결과적으로 곱연산이 됩니다. 폭발/수류탄/해킹/액티브 추가 피해 등 피해 종류와 무관하게 모두 적용됩니다
//   (적이 피해를 받는 통로가 EnemyHealth.TakeDamage 하나뿐이기 때문).
// - 등록된 것이 없으면 배율 1 - 원래 동작과 100% 동일합니다.
// - 첫 사용처: 41번 패시브(해킹된 적이 받는 피해 x1.5, Item41effect.cs).
public static class EnemyDamageTakenModifier
{
    private class Entry
    {
        public UnityEngine.Object owner;
        public Func<EnemyHealth, float> getMultiplier;
    }

    private static readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>();

    // sourceId 하나당 하나만 등록됩니다(같은 sourceId로 다시 호출하면 덮어씀). owner가 파괴되면(씬 재시작 등) 자동으로 빠집니다.
    // getMultiplier는 피해를 받는 적을 받아 그 순간 적용할 배율을 돌려줍니다(해당 없으면 1).
    public static void SetMultiplier(string sourceId, UnityEngine.Object owner, Func<EnemyHealth, float> getMultiplier)
    {
        if (string.IsNullOrEmpty(sourceId) || getMultiplier == null) return;
        entries[sourceId] = new Entry { owner = owner, getMultiplier = getMultiplier };
    }

    public static void RemoveMultiplier(string sourceId)
    {
        if (string.IsNullOrEmpty(sourceId)) return;
        entries.Remove(sourceId);
    }

    // 이 적이 지금 받는 피해에 곱할 최종 배율(등록된 배율들의 곱, 없으면 1).
    public static float Resolve(EnemyHealth target)
    {
        if (entries.Count == 0 || target == null) return 1f;

        PruneDead();
        float total = 1f;
        foreach (var entry in entries.Values)
        {
            float m = entry.getMultiplier(target);
            if (m < 0f) m = 0f;
            total *= m;
        }
        return total;
    }

    private static void PruneDead()
    {
        List<string> deadKeys = null;
        foreach (var kvp in entries)
        {
            if (kvp.Value.owner == null)
            {
                (deadKeys ??= new List<string>()).Add(kvp.Key);
            }
        }
        if (deadKeys != null)
        {
            foreach (var key in deadKeys) entries.Remove(key);
        }
    }

    // 플레이 시작 시 이전 플레이의 등록 정보가 남아있지 않도록 초기화합니다(기존 등록소들과 같은 관례).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlayStart()
    {
        entries.Clear();
    }
}
