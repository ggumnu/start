using System.Collections.Generic;
using UnityEngine;

// [적 공격 금지 - 정적 등록소]
// 일정 시간 동안 모든 적이 "새 공격"(사격/돌진/충전/전방위 공격)을 시작하지 못하게 막습니다. 이동과 접촉 피해는 그대로입니다.
// 지금 쓰는 곳: 90번 액티브(물결) - 물결이 방 전체로 다 퍼질 때까지 적의 사격/충전/돌진을 모두 막습니다(형빈 확정).
//
// - 이미 진행 중인 공격을 끊는 것은 이 등록소의 일이 아닙니다(90번은 물결이 닿는 순간 각 적의 공격을 직접 끊음 -
//   ICancellableAttack.CancelAttack / EnemyRangedAttack.CancelBurst). 이 등록소는 "새로 시작하는 것"만 막습니다.
// - 확인하는 곳: 근접 AI(EnemyAIController - 공격 시작), 원거리 AI(EnemyRangedAIController - 사격 시작), 보스 AI(BossAIController -
//   돌진/전방위/사격 시작). 세 곳 모두 "방 입장 공격 유예"와 같은 자리에서 함께 확인합니다.
// - 여러 출처가 동시에 걸면 가장 늦게 끝나는 시간까지 막습니다. 게임이 멈춘 동안(Time.time 기준)은 시간이 흐르지 않습니다.
// - 플레이 시작 때마다 비워서, 에디터에서 Domain Reload를 꺼 둔 경우에도 이전 플레이의 금지가 남지 않습니다.
public static class EnemyAttackLock
{
    private static readonly Dictionary<string, float> locks = new Dictionary<string, float>();

    // sourceId 출처로 지금부터 duration초 동안 적의 새 공격을 막습니다(같은 출처로 다시 부르면 끝나는 시간을 새로 정함).
    public static void Lock(string sourceId, float duration)
    {
        if (string.IsNullOrEmpty(sourceId)) return;
        locks[sourceId] = Time.time + Mathf.Max(duration, 0f);
    }

    // sourceId 출처의 금지를 바로 풉니다.
    public static void Unlock(string sourceId)
    {
        if (string.IsNullOrEmpty(sourceId)) return;
        locks.Remove(sourceId);
    }

    // 지금 적의 새 공격이 막혀 있는지입니다.
    public static bool IsLocked
    {
        get
        {
            if (locks.Count == 0) return false;
            float now = Time.time;
            bool locked = false;
            List<string> expired = null;
            foreach (var pair in locks)
            {
                if (pair.Value > now) locked = true;
                else (expired ??= new List<string>()).Add(pair.Key);
            }
            if (expired != null) foreach (var key in expired) locks.Remove(key);
            return locked;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlayStart()
    {
        locks.Clear();
    }
}
