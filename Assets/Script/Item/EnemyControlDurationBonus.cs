using System;
using System.Collections.Generic;
using UnityEngine;

// "적을 제어하는 스킬의 지속시간"에 보너스(초)를 더하는 범용 보정 창구입니다.
// EnemyDetectRangeOverride/EnemySightOverride와 완전히 동일한 정적 등록소 패턴입니다 -
// 6번 아이템(제어 스킬 지속시간 +1초)뿐 아니라, 나중에 같은 종류의 다른 효과가 생겨도
// 전부 여기에 "출처 이름 + 값"만 등록하면 됩니다.
//
// [무엇이 "제어 스킬"인가 - opt-in 구조]
// 이 등록소는 모든 디버프에 자동으로 적용되지 않습니다. "적의 행동(이동/공격 개시 등)을
// 일정 시간 억제하는" 제어 스킬을 새로 만들 때마다, 그 스킬이 자기 지속시간을 확정하는
// 바로 그 줄에서 Resolve()를 직접 호출해야 합니다(38번 매료 - EnemyCharmState.
// TryApplyCharm()의 charmTimer = Resolve(duration) 참고). 예를 들어 2번 아이템(발자국)의
// 감속처럼 "정해진 지속시간 없이 범위 안에 있는 동안만 적용되는" 효과는 애초에 지속시간
// 개념이 없어 이 창구와 무관합니다 - 그런 효과에까지 억지로 끼워 넣을 필요는 없습니다.
//
// [합산 규칙 - 형빈 확정]
// 등록된 보너스가 여러 개면 전부 더합니다(합연산). 등록된 게 하나도 없으면 원래
// duration을 그대로 돌려줍니다(EnemyDetectRangeOverride 등과 동일한 "무등록 시 원래
// 동작과 100% 동일" 원칙).
//
// [등록자(플레이어)가 사라졌을 때]
// 등록할 때 주인 오브젝트(보통 플레이어)를 함께 넘기고, 주인이 파괴되면(런 재시작 등)
// 그 보너스는 자동으로 무시/정리됩니다.
//
// [사용 방법 - 코드에서]
// EnemyControlDurationBonus.SetBonus("내_출처_이름", 주인GameObject, () => 1f);
// EnemyControlDurationBonus.RemoveBonus("내_출처_이름");
// float duration = EnemyControlDurationBonus.Resolve(baseDuration);
public static class EnemyControlDurationBonus
{
    private class Entry
    {
        public UnityEngine.Object owner;
        public Func<float> getBonus;
    }

    private static readonly Dictionary<string, Entry> bonuses = new Dictionary<string, Entry>();
    private static readonly List<string> deadKeys = new List<string>();

    // 에디터에서 도메인 리로드를 끈 상태로 Play를 다시 눌러도 이전 Play의 보너스가 남지
    // 않도록, Play 시작 때마다 비웁니다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlayStart()
    {
        bonuses.Clear();
    }

    // 보너스를 등록합니다. 같은 sourceId로 다시 부르면 값만 갱신됩니다(중첩되지 않음).
    // getBonus는 값을 매번 읽어가는 함수라서, Play 중에 인스펙터 값을 바꾸면 곧바로 반영됩니다.
    public static void SetBonus(string sourceId, UnityEngine.Object owner, Func<float> getBonus)
    {
        if (string.IsNullOrEmpty(sourceId) || getBonus == null) return;

        PruneDead();
        bonuses[sourceId] = new Entry { owner = owner, getBonus = getBonus };
    }

    // 보너스를 해제합니다. 등록된 적이 없는 sourceId여도 안전합니다.
    public static void RemoveBonus(string sourceId)
    {
        if (string.IsNullOrEmpty(sourceId)) return;

        bonuses.Remove(sourceId);
    }

    // 제어 스킬이 "실제로 몇 초 유지할지"를 정하는 시점에 baseDuration 대신 이 값을 씁니다.
    public static float Resolve(float baseDuration)
    {
        if (bonuses.Count == 0) return baseDuration;

        float result = baseDuration;

        foreach (var pair in bonuses)
        {
            Entry entry = pair.Value;

            // 주인이 파괴된 보너스는 무시합니다(Unity 오브젝트의 파괴 여부는 == null로 확인).
            if (entry.owner == null) continue;

            result += entry.getBonus();
        }

        return result;
    }

    // 주인이 이미 파괴된 보너스를 사전에서 치웁니다(새로 등록할 때만 한 번씩 정리).
    private static void PruneDead()
    {
        deadKeys.Clear();

        foreach (var pair in bonuses)
        {
            if (pair.Value.owner == null) deadKeys.Add(pair.Key);
        }

        for (int i = 0; i < deadKeys.Count; i++)
        {
            bonuses.Remove(deadKeys[i]);
        }

        deadKeys.Clear();
    }
}
