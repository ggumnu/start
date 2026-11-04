using System.Collections.Generic;
using UnityEngine;

// "일반방 보물상자에서 항상 플레이어 아이템이 나온다"(116번 장신구)를 위한 정적 등록소입니다(TreasureChanceBonusOverride와 같은 패턴).
//
// [규칙 - 형빈 확정]
// - 등록이 하나라도 살아 있는 동안 IsActive = true. NormalRoomTreasureSpawner.ResolveContent(일반방 상자 + 9번/106번으로 복제한 일반방 상자)가
//   "재화냐 플레이어 아이템이냐" 확률을 건너뛰고 바로 플레이어 아이템을 뽑습니다. 뽑을 아이템이 없으면 기존처럼 재화로 대신합니다.
// - 판정 시점은 상자를 처음 "열 때"(내용물이 정해지는 순간)입니다. 이미 내용물이 정해진 상자는 등록이 사라져도 그대로입니다.
// - 상자가 나타날 확률, 보스방 보상 상자, 동아리방 좌대, 특수방 크레딧 상자에는 영향이 없습니다.
// - 소유자(보통 플레이어)가 사라지면 자동으로 무시됩니다. 플레이 시작 때마다 비웁니다.
//
// [사용 방법 - 코드에서]
// NormalChestItemGuaranteeOverride.Register("출처_이름", 플레이어GameObject);
// NormalChestItemGuaranteeOverride.Unregister("출처_이름");
public static class NormalChestItemGuaranteeOverride
{
    private static readonly Dictionary<string, Object> entries = new Dictionary<string, Object>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlayStart()
    {
        entries.Clear();
    }

    public static void Register(string sourceId, Object owner)
    {
        if (string.IsNullOrEmpty(sourceId)) return;
        entries[sourceId] = owner;
    }

    public static void Unregister(string sourceId)
    {
        if (string.IsNullOrEmpty(sourceId)) return;
        entries.Remove(sourceId);
    }

    // 살아 있는 등록이 하나라도 있으면 true입니다(소유자가 파괴된 등록은 지우고 셈에서 뺍니다).
    public static bool IsActive
    {
        get
        {
            if (entries.Count == 0) return false;

            List<string> dead = null;
            bool any = false;
            foreach (var kvp in entries)
            {
                // 소유자 없이 등록한 것은 살아 있는 것으로 칩니다. 소유자를 넣었는데 파괴됐으면 지웁니다.
                if (!ReferenceEquals(kvp.Value, null) && kvp.Value == null)
                {
                    if (dead == null) dead = new List<string>();
                    dead.Add(kvp.Key);
                    continue;
                }
                any = true;
            }
            if (dead != null) foreach (var key in dead) entries.Remove(key);
            return any;
        }
    }
}
