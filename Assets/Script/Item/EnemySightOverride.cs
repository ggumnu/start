using System;
using System.Collections.Generic;
using UnityEngine;

// "적이 플레이어를 볼 수 있는가"에 대한 규칙을 등록해두는 범용 창구입니다. 8번 아이템(벽 뒤로
// 숨으면 추적 중단)이 등록해두고, 근접/원거리 AI가 매 프레임 이 창구에 물어봅니다. 나중에
// 시야를 다르게 다루는 다른 효과도 여기에 규칙만 등록하면 됩니다.
//
// [규칙이 하나도 등록되지 않았을 때]
// 모든 함수가 "시야 규칙 없음"으로 동작합니다(CanSee는 항상 true, ShouldLoseTarget은 항상
// false) - 아이템이 없는 평소에는 Physics 검사를 전혀 하지 않아 비용이 거의 없고, 적
// 동작도 예전과 완전히 같습니다.
//
// [시야 판정 방식]
// 적의 발밑 좌표에서 eyeHeight만큼 올린 점과, 플레이어 발밑에서 같은 높이만큼 올린 점 사이에
// wallMask 레이어의 콜라이더가 하나라도 있으면 시야가 막힌 것으로 봅니다(트리거 콜라이더는
// 무시). 기존 EnemyRangedAIController.CheckLineOfSight()와 같은 방식입니다. 벽 레이어는
// 프리팹마다 값이 제각각이라 각 적의 설정을 쓰지 않고, 규칙을 등록하는 쪽(아이템)이 한 곳에서
// 지정한 값을 모든 적에게 똑같이 적용합니다.
//
// [등록자(플레이어)가 사라졌을 때]
// 등록할 때 주인 오브젝트(보통 플레이어)를 함께 넘기고, 주인이 파괴되면 그 규칙은 자동으로
// 무시/정리됩니다(EnemyDetectRangeOverride와 같은 방식). 런 재시작으로 씬을 다시 불러도 이전
// 판의 규칙이 남지 않습니다.
//
// [규칙이 여러 개일 때]
// 벽 레이어는 합치고(OR), 눈 높이와 유예 시간은 더 큰 값을 씁니다.
//
// [사용 방법 - 코드에서]
// EnemySightOverride.SetRule("내_출처_이름", 주인GameObject, () => new EnemySightRule { ... });
// EnemySightOverride.RemoveRule("내_출처_이름");
// bool visible = EnemySightOverride.CanSee(적위치, 플레이어위치);
// if (EnemySightOverride.ShouldLoseTarget(ref 적이_들고있는_타이머, 적위치, 플레이어위치)) { ... }
public struct EnemySightRule
{
    // 이 레이어의 콜라이더가 적과 플레이어 사이에 있으면 시야가 막힌 것으로 봅니다.
    public LayerMask wallMask;
    // 시야 판정 점을 발밑에서 얼마나 올릴지입니다(월드 단위).
    public float eyeHeight;
    // 시야가 막힌 뒤 이 시간(초) 동안 계속 막혀 있어야 "놓쳤다"고 판정합니다.
    public float lossGraceTime;
}

public static class EnemySightOverride
{
    private class Entry
    {
        public UnityEngine.Object owner;
        public Func<EnemySightRule> getRule;
    }

    private static readonly Dictionary<string, Entry> rules = new Dictionary<string, Entry>();
    private static readonly List<string> deadKeys = new List<string>();

    // 에디터에서 도메인 리로드를 끈 상태로 Play를 다시 눌러도 이전 Play의 규칙이 남지 않도록,
    // Play 시작 때마다 비웁니다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlayStart()
    {
        rules.Clear();
    }

    // 규칙을 등록합니다. 같은 sourceId로 다시 부르면 값만 갱신됩니다(중첩되지 않음).
    // getRule은 값을 매번 읽어가는 함수라서, Play 중에 인스펙터 값을 바꾸면 곧바로 반영됩니다.
    public static void SetRule(string sourceId, UnityEngine.Object owner, Func<EnemySightRule> getRule)
    {
        if (string.IsNullOrEmpty(sourceId) || getRule == null) return;

        PruneDead();
        rules[sourceId] = new Entry { owner = owner, getRule = getRule };
    }

    // 규칙을 해제합니다. 등록된 적이 없는 sourceId여도 안전합니다.
    public static void RemoveRule(string sourceId)
    {
        if (string.IsNullOrEmpty(sourceId)) return;

        rules.Remove(sourceId);
    }

    // 지금 유효한 시야 규칙이 하나라도 있으면 true입니다.
    public static bool IsActive
    {
        get { return TryGetCombinedRule(out _); }
    }

    // 적이 플레이어를 볼 수 있으면 true입니다. 시야 규칙이 없으면 항상 true입니다.
    public static bool CanSee(Vector3 enemyPosition, Vector3 playerPosition)
    {
        if (!TryGetCombinedRule(out EnemySightRule rule)) return true;

        return HasLineOfSight(rule, enemyPosition, playerPosition);
    }

    // 이미 깨어나 추적 중인 적이 "플레이어를 놓쳤는지" 판정합니다. 시야가 막혀 있는 시간을
    // lostTimer(적마다 자기 것을 하나씩 들고 있어야 함)에 쌓아서, 유예 시간을 넘기면 true를
    // 돌려주고 타이머를 0으로 되돌립니다. 시야가 열려 있거나 규칙이 없으면 타이머를 0으로
    // 되돌리고 false를 돌려줍니다.
    public static bool ShouldLoseTarget(ref float lostTimer, Vector3 enemyPosition, Vector3 playerPosition)
    {
        if (!TryGetCombinedRule(out EnemySightRule rule))
        {
            lostTimer = 0f;
            return false;
        }

        if (HasLineOfSight(rule, enemyPosition, playerPosition))
        {
            lostTimer = 0f;
            return false;
        }

        lostTimer += Time.deltaTime;
        if (lostTimer >= Mathf.Max(0f, rule.lossGraceTime))
        {
            lostTimer = 0f;
            return true;
        }

        return false;
    }

    private static bool HasLineOfSight(EnemySightRule rule, Vector3 enemyPosition, Vector3 playerPosition)
    {
        Vector3 up = Vector3.up * Mathf.Max(0f, rule.eyeHeight);
        return !Physics.Linecast(enemyPosition + up, playerPosition + up, rule.wallMask, QueryTriggerInteraction.Ignore);
    }

    // 등록된 유효한 규칙들을 하나로 합칩니다. 유효한 규칙이 없으면 false입니다.
    private static bool TryGetCombinedRule(out EnemySightRule combined)
    {
        combined = default(EnemySightRule);

        if (rules.Count == 0) return false;

        bool any = false;

        foreach (var pair in rules)
        {
            Entry entry = pair.Value;

            // 주인이 파괴된 규칙은 무시합니다(Unity 오브젝트의 파괴 여부는 == null로 확인).
            if (entry.owner == null) continue;

            EnemySightRule rule = entry.getRule();

            if (!any)
            {
                combined = rule;
                any = true;
            }
            else
            {
                combined.wallMask = combined.wallMask.value | rule.wallMask.value;
                combined.eyeHeight = Mathf.Max(combined.eyeHeight, rule.eyeHeight);
                combined.lossGraceTime = Mathf.Max(combined.lossGraceTime, rule.lossGraceTime);
            }
        }

        return any;
    }

    // 주인이 이미 파괴된 규칙을 사전에서 치웁니다(새로 등록할 때만 한 번씩 정리).
    private static void PruneDead()
    {
        deadKeys.Clear();

        foreach (var pair in rules)
        {
            if (pair.Value.owner == null) deadKeys.Add(pair.Key);
        }

        for (int i = 0; i < deadKeys.Count; i++)
        {
            rules.Remove(deadKeys[i]);
        }

        deadKeys.Clear();
    }
}
