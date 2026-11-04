using System.Collections.Generic;
using UnityEngine;

// 체력 소모 특수방(진입 조건 = Health)의 출입 피해입니다(아이작 리펜던스의 가시방과 같은 방식 - 형빈 확정).
//
// [규칙]
// - 체력 소모 특수방과 그 부모 방(일반방/시작방) 사이를 지날 때마다(들어갈 때 1번, 나올 때 1번) 플레이어가 피격됩니다.
// - 피해량은 Special Room Entry Settings의 Health Damage Per Pass(기본 0.5)입니다.
// - 일반 피격(PlayerHealth.TakeDamage)으로 처리하므로 무적 시간 중에는 피해가 없고(무적 시간을 이용한 통과 테크닉),
//   21번 장신구(받는 피해 고정)도 그대로 적용되며, 체력이 피해량 이하이면 사망합니다(가시방과 동일).
// - 잠금 바리케이드는 없습니다. 부모 방 전투 중에는 일반 열린 벽처럼 전투 바리케이드가 막고, 전투가 끝나면 드나들 수
//   있습니다(그때마다 피해).
//
// [판정 방식]
// LevelBuilder는 플레이어가 칸 경계를 넘어 다른 방에 들어갈 때마다 RoomController.OnAnyRoomEntered를 울립니다.
// 직전 방과 새 방 중 하나가 체력 소모 특수방이고, 둘이 서로 바로 이어진 방(앵커로 연결)일 때만 피해를 줍니다 -
// 특수방은 항상 막다른 방이라 이어진 방은 부모 방 하나뿐입니다. 순간이동(22번/46번 등)이나 레벨 전환처럼 이어지지 않은
// 방으로 옮겨진 경우에는 피해가 없습니다.
//
// [나중에 만들 "출입 피해 막기" 아이템]
// 아이템 효과가 SpecialRoomHealthDamageOverride.Block(아이템 ID, 소유자)을 호출해 두면 피해를 주지 않습니다(장착 해제 시
// Unblock). 이 스크립트나 PlayerHealth를 고칠 필요가 없습니다(크레딧/보물상자 보너스 Override들과 같은 방식).
//
// [설정]
// 씬에 배치할 필요가 없습니다. LevelBuilder가 시작할 때 Configure()로 설정 에셋을 넘겨 줍니다.
public static class SpecialRoomHealthHazard
{
    private static SpecialRoomEntrySettings settings;
    private static bool subscribed;
    private static RoomInstance lastEnteredRoom;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        if (subscribed) RoomController.OnAnyRoomEntered -= HandleRoomEntered;
        subscribed = false;
        settings = null;
        lastEnteredRoom = null;
    }

    // LevelBuilder.Start()에서 호출합니다. 설정 에셋이 없으면(null) 기본 피해량 0.5로 동작합니다.
    public static void Configure(SpecialRoomEntrySettings entrySettings)
    {
        settings = entrySettings;
        if (!subscribed)
        {
            RoomController.OnAnyRoomEntered += HandleRoomEntered;
            subscribed = true;
        }
    }

    private static void HandleRoomEntered(RoomInstance entered)
    {
        var previous = lastEnteredRoom;
        lastEnteredRoom = entered;

        if (previous == null || entered == null || previous == entered) return;

        RoomInstance special = IsHealthSpecial(entered) ? entered : (IsHealthSpecial(previous) ? previous : null);
        if (special == null) return;

        RoomInstance other = special == entered ? previous : entered;
        if (!AreDirectlyConnected(special, other)) return;

        if (SpecialRoomHealthDamageOverride.IsBlocked) return;

        float damage = settings != null ? settings.healthDamagePerPass : SpecialRoomEntrySettings.DefaultHealthDamagePerPass;
        if (damage <= 0f) return;

        var player = GameObject.FindGameObjectWithTag("Player");
        var health = player != null ? player.GetComponent<PlayerHealth>() : null;
        if (health == null) return;

        health.TakeDamage(damage);
    }

    private static bool IsHealthSpecial(RoomInstance room)
    {
        // 레이아웃이 아니라 그 방에 뽑힌 진입 조건을 봅니다(레이아웃의 진입 조건이 랜덤일 수 있음).
        return room != null && room.category == RoomCategory.Special &&
               room.specialLayout != null && room.specialEntryCondition == SpecialRoomEntryCondition.Health;
    }

    private static bool AreDirectlyConnected(RoomInstance a, RoomInstance b)
    {
        if (a == null || b == null || a.anchors == null) return false;
        foreach (var link in a.anchors)
        {
            if (link == null) continue;
            if ((link.roomA == a && link.roomB == b) || (link.roomA == b && link.roomB == a)) return true;
        }
        return false;
    }
}

// 체력 소모 특수방 출입 피해를 막는 효과를 등록하는 곳입니다(나중에 만들 아이템용). 하나라도 등록되어 있으면 피해가 없습니다.
// sourceId(예: 아이템 ID) 하나당 하나만 등록되며, owner가 파괴되면 자동으로 빠집니다(CreditGainBonusOverride와 같은 방식).
public static class SpecialRoomHealthDamageOverride
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
