using System;
using System.Collections.Generic;
using UnityEngine;

// 적의 "플레이어 탐지 거리(EnemyStats.detectRange)"에 상한을 거는 범용 보정 창구입니다.
// 7번 아이템(탐지 거리 감소)뿐 아니라, 나중에 탐지 거리를 늘리거나 줄이는 다른 효과도
// 전부 여기에 "출처 이름 + 값"만 등록하면 됩니다.
//
// [왜 EnemyStats 값을 직접 바꾸지 않고 이 창구를 거치나요]
// 적은 스폰될 때(방 재입장으로 새로 만들어질 때 포함) detectRange가 원래 값(예: 8)으로
// 채워지고, 근접/원거리 AI는 플레이어가 그 거리 안에 들어온 "그 순간" 영구히 깨어납니다
// (한번 깨어나면 다시 잠들지 않음). 그래서 아이템 쪽에서 주기적으로 스탯을 덮어쓰는
// 방식은 새로 만들어진 적이 원래 값으로 한 프레임이라도 판정하면 놓치는 위험이 있습니다.
// 대신 AI가 "깨어날지 판단하는 바로 그 줄"에서 Resolve()를 거치게 해서, 값이 언제
// 채워지든 항상 상한이 적용되도록 합니다(EnemyAIController, EnemyRangedAIController).
//
// [적용 규칙]
// - 등록된 상한이 여러 개면 가장 작은 값이 적용됩니다.
// - 상한이므로 원래 탐지 거리가 상한보다 작은 적은 그대로 둡니다(늘어나지 않음).
// - 등록된 게 없으면 원래 값을 그대로 돌려줍니다.
// - 보스(BossAIController)는 탐지 거리를 아예 쓰지 않으므로 영향이 없습니다.
//
// [등록자(플레이어)가 사라졌을 때]
// 등록할 때 주인 오브젝트(보통 플레이어)를 함께 넘기고, 주인이 파괴되면(런 재시작 등)
// 그 상한은 자동으로 무시/정리됩니다. 그래서 씬을 다시 불러도 이전 판의 상한이 남지
// 않습니다.
//
// [사용 방법 - 코드에서]
// EnemyDetectRangeOverride.SetCap("내_출처_이름", 주인GameObject, () => 3f);
// EnemyDetectRangeOverride.RemoveCap("내_출처_이름");
// float range = EnemyDetectRangeOverride.Resolve(stats.detectRange);
public static class EnemyDetectRangeOverride
{
    private class Entry
    {
        public UnityEngine.Object owner;
        public Func<float> getCap;
    }

    private static readonly Dictionary<string, Entry> caps = new Dictionary<string, Entry>();
    private static readonly List<string> deadKeys = new List<string>();

    // 에디터에서 도메인 리로드를 끈 상태로 Play를 다시 눌러도 이전 Play의 상한이 남지
    // 않도록, Play 시작 때마다 비웁니다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlayStart()
    {
        caps.Clear();
    }

    // 상한을 등록합니다. 같은 sourceId로 다시 부르면 값만 갱신됩니다(중첩되지 않음).
    // getCap은 값을 매번 읽어가는 함수라서, Play 중에 인스펙터 값을 바꾸면 곧바로 반영됩니다.
    public static void SetCap(string sourceId, UnityEngine.Object owner, Func<float> getCap)
    {
        if (string.IsNullOrEmpty(sourceId) || getCap == null) return;

        PruneDead();
        caps[sourceId] = new Entry { owner = owner, getCap = getCap };
    }

    // 상한을 해제합니다. 등록된 적이 없는 sourceId여도 안전합니다.
    public static void RemoveCap(string sourceId)
    {
        if (string.IsNullOrEmpty(sourceId)) return;

        caps.Remove(sourceId);
    }

    // AI가 "플레이어를 탐지할 거리"를 정할 때 stats.detectRange 대신 이 값을 씁니다.
    public static float Resolve(float baseRange)
    {
        if (caps.Count == 0) return baseRange;

        float result = baseRange;

        foreach (var pair in caps)
        {
            Entry entry = pair.Value;

            // 주인이 파괴된 상한은 무시합니다(Unity 오브젝트의 파괴 여부는 == null로 확인).
            if (entry.owner == null) continue;

            float cap = Mathf.Max(0f, entry.getCap());
            if (cap < result) result = cap;
        }

        return result;
    }

    // 주인이 이미 파괴된 상한을 사전에서 치웁니다(새로 등록할 때만 한 번씩 정리).
    private static void PruneDead()
    {
        deadKeys.Clear();

        foreach (var pair in caps)
        {
            if (pair.Value.owner == null) deadKeys.Add(pair.Key);
        }

        for (int i = 0; i < deadKeys.Count; i++)
        {
            caps.Remove(deadKeys[i]);
        }

        deadKeys.Clear();
    }
}
