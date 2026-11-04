using System;
using System.Collections.Generic;
using UnityEngine;

// "장전 시간을 % 만큼 줄여주는" 패시브 효과를 위한 정적 등록 창구입니다(15번 아이템).
// EnemyControlDurationBonus/TreasureChanceBonusOverride와 동일한 패턴입니다 - 아이템이 자기
// 감소율만 여기에 등록해두면, 실제 사용 지점(PlayerShooting.ComputeReloadDuration)은 어떤
// 아이템이 등록했는지 전혀 몰라도 됩니다.
//
// [계산 방식 - 곱연산, 형빈 확정]
// 여러 개의 "장전 시간 감소" 보너스가 동시에 등록되면, 각 보너스의 "남는 비율"(1 - 감소율)을
// 전부 곱합니다. 예: 50% 감소 두 개가 동시에 있으면 0.5 x 0.5 = 0.25, 즉 원래 시간의 25%만
// 남습니다(총 75% 감소). 감소율을 그대로 더하는 합연산은 여러 개가 겹치면 100%를 넘어 장전
// 시간이 0 이하가 될 수 있어 위험하므로 쓰지 않습니다.
//
// [하한선 클램프]
// Resolve()에 넘긴 floor보다 더 줄어들지 않도록 마지막에 clamp합니다. 호출부(PlayerShooting)가
// PlayerStats.TryGetStatFloor(StatType.ReloadTime, ...)로 구한 "현재 장전 시간 하한선"을
// 그대로 넘겨주므로, 하한선 설정이 나중에 바뀌어도 이 코드는 손댈 필요가 없습니다.
//
// [등록자(플레이어)가 사라졌을 때]
// 등록할 때 주인 오브젝트(보통 플레이어)를 함께 넘기고, 주인이 파괴되면(런 재시작 등) 그
// 보너스는 자동으로 무시/정리됩니다 - 다른 정적 등록소들과 동일한 패턴입니다.
//
// [사용 방법 - 코드에서]
// ReloadSpeedBonusOverride.SetBonus("내_출처_이름", 주인GameObject, () => 0.5f); // 50% 감소
// ReloadSpeedBonusOverride.RemoveBonus("내_출처_이름");
// float reloadTime = ReloadSpeedBonusOverride.Resolve(baseReloadTime, floor);
public static class ReloadSpeedBonusOverride
{
    private class Entry
    {
        public UnityEngine.Object owner;
        public Func<float> getReductionRatio;
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
    // getReductionRatio는 값을 매번 읽어가는 함수라서, Play 중에 인스펙터 값을 바꾸면 곧바로
    // 반영됩니다.
    public static void SetBonus(string sourceId, UnityEngine.Object owner, Func<float> getReductionRatio)
    {
        if (string.IsNullOrEmpty(sourceId) || getReductionRatio == null) return;

        PruneDead();
        bonuses[sourceId] = new Entry { owner = owner, getReductionRatio = getReductionRatio };
    }

    // 보너스를 해제합니다. 등록된 적이 없는 sourceId여도 안전합니다.
    public static void RemoveBonus(string sourceId)
    {
        if (string.IsNullOrEmpty(sourceId)) return;

        bonuses.Remove(sourceId);
    }

    // baseReloadTime에 등록된 모든 보너스를 곱연산으로 적용한 뒤, floor 밑으로는 내려가지
    // 않도록 clamp한 값을 돌려줍니다. 등록된 보너스가 하나도 없으면 baseReloadTime을 그대로
    // (floor로만 clamp해서) 돌려줍니다.
    public static float Resolve(float baseReloadTime, float floor)
    {
        float remainingMultiplier = 1f;

        foreach (var pair in bonuses)
        {
            Entry entry = pair.Value;

            // 주인이 이미 파괴된 보너스는 무시합니다(Unity 오브젝트의 파괴 여부는 == null로 확인).
            if (entry.owner == null) continue;

            float reduction = Mathf.Clamp01(entry.getReductionRatio());
            remainingMultiplier *= (1f - reduction);
        }

        float result = baseReloadTime * remainingMultiplier;
        return Mathf.Max(result, floor);
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
