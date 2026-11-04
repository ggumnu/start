using System.Collections.Generic;
using UnityEngine;

// [수류탄 자해 무효 - 정적 등록소]
// 57번 패시브 등 "수류탄에 의한 자해 데미지를 입지 않는다" 효과를 위한 등록소입니다. 37번이 쓰는
// SpecialRoomHealthDamageOverride와 같은 구조입니다.
//
// - 효과를 가진 아이템이 Block(이름, 소유자)로 등록하면, 등록이 하나라도 남아 있는 동안 IsBlocked가 true입니다.
// - Grenade.DamageOwner()가 IsBlocked를 확인해서 true면 PlayerHealth.TakeDamage를 아예 호출하지 않습니다.
//   (피격 점멸/무적 시간/21번 장신구 판정도 일어나지 않음 - 형빈 확정)
// - 소유자(보통 플레이어 오브젝트)가 사라지면 등록도 자동으로 정리됩니다(새 게임에서 플레이어가 새로 만들어질 때).
// - 플레이 시작 때마다 비워서, 에디터에서 Domain Reload를 꺼 둔 경우에도 이전 플레이의 등록이 남지 않습니다.
//
// [나중에 생길 수류탄 계열 폭발 - 분열 수류탄 등]
// 플레이어에게 자해 피해를 주는 수류탄 계열 폭발을 새로 만들 때는, 그 자해 판정에도 반드시
// "if (GrenadeSelfDamageOverride.IsBlocked) return;"을 넣어 이 등록소를 확인하게 해주세요.
public static class GrenadeSelfDamageOverride
{
    private static readonly Dictionary<string, Object> blockers = new Dictionary<string, Object>();

    public static void Block(string sourceId, Object owner)
    {
        if (string.IsNullOrEmpty(sourceId)) return;
        blockers[sourceId] = owner;
    }

    public static void Unblock(string sourceId)
    {
        if (string.IsNullOrEmpty(sourceId)) return;
        blockers.Remove(sourceId);
    }

    public static bool IsBlocked
    {
        get
        {
            PruneDead();
            return blockers.Count > 0;
        }
    }

    private static void PruneDead()
    {
        List<string> deadKeys = null;
        foreach (var kvp in blockers)
        {
            if (kvp.Value != null) continue;
            if (deadKeys == null) deadKeys = new List<string>();
            deadKeys.Add(kvp.Key);
        }
        if (deadKeys == null) return;
        foreach (var key in deadKeys) blockers.Remove(key);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlayStart()
    {
        blockers.Clear();
    }
}
