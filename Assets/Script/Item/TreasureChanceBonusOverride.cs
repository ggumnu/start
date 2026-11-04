using System;
using System.Collections.Generic;
using UnityEngine;

// 일반맵 보물상자 "등장 확률"에 곱연산 보너스를 거는 범용 등록 창구입니다. 18번 아이템(보물상자
// 등장 확률 50% 증가)뿐 아니라, 나중에 같은 종류의 보너스를 주는 다른 패시브/액티브 효과가
// 추가돼도 전부 여기에 "출처 이름 + 보너스율"만 등록하면 됩니다.
//
// [계산 방식 - 곱연산]
// 최종 배수 = 등록된 모든 보너스에 대해 (1 + 보너스율)을 전부 곱한 값입니다. EnemySpeedModifier
// (이동 속도 보정)와 동일한 관례입니다 - 예를 들어 +50%짜리 두 개가 동시에 등록되면
// 1.5 x 1.5 = 2.25배가 됩니다(합연산이 아닙니다). 지금은 등록하는 아이템이 18번 하나뿐이라
// 결과가 드러나지 않지만, 나중에 비슷한 아이템이 추가될 때를 대비해 미리 이 방식으로
// 만들어둡니다.
//
// [왜 NormalRoomTreasureSpawner.chancePerEnemy를 직접 바꾸지 않고 이 창구를 거치나요]
// chancePerEnemy는 "적 한 마리당 확률"이라는 중간 계산 상수일 뿐이고, 이 아이템이 실제로
// 늘리려는 대상은 그 계산이 끝난 뒤의 최종 등장 확률입니다. EnemyDetectRangeOverride/
// EnemySpeedModifier와 같은 이유로, 최종 계산값에 개입하는 창구를 별도로 두면 나중에
// chancePerEnemy를 쓰는 계산식 자체가 바뀌어도(예: 방 크기별 가중치가 추가되는 등) 이
// 아이템은 아무 영향을 받지 않습니다.
//
// [등록자(플레이어)가 사라졌을 때]
// 등록할 때 주인 오브젝트(보통 플레이어)를 함께 넘기고, 주인이 파괴되면(런 재시작 등) 그
// 보너스는 자동으로 무시/정리됩니다 - EnemyDetectRangeOverride와 동일한 패턴입니다. 그래서
// 씬을 다시 불러도 이전 판의 보너스가 남지 않습니다.
//
// [사용 방법 - 코드에서]
// TreasureChanceBonusOverride.SetBonus("내_출처_이름", 주인GameObject, () => 0.5f); // +50%
// TreasureChanceBonusOverride.RemoveBonus("내_출처_이름");
// float chance = baseChance * TreasureChanceBonusOverride.CurrentMultiplier;
public static class TreasureChanceBonusOverride
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

    // 등록된 모든 보너스를 곱연산으로 합친 최종 배수입니다(보너스가 없으면 1).
    public static float CurrentMultiplier
    {
        get
        {
            if (bonuses.Count == 0) return 1f;

            float result = 1f;

            foreach (var pair in bonuses)
            {
                Entry entry = pair.Value;

                // 주인이 이미 파괴된 보너스는 무시합니다(Unity 오브젝트의 파괴 여부는 == null로 확인).
                if (entry.owner == null) continue;

                float bonus = entry.getBonus();
                result *= Mathf.Max(0f, 1f + bonus);
            }

            return result;
        }
    }

    // 주인이 이미 파괴된 보너스를 사전에서 치웁니다(새로 등록할 때만 한 번씩 정리).
    private static void PruneDead()
    {
        deadKeys.Clear();

        foreach (var pair in bonuses)
        {
            if (pair.Value.owner == null) deadKeys.Add(pair.Key);
        }

        foreach (var key in deadKeys)
        {
            bonuses.Remove(key);
        }
    }
}
