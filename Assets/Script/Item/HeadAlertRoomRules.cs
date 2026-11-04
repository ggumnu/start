using UnityEngine;

// 피격 없이 방 클리어하면 보상을 주는 장신구(39/111번)가 같이 쓰는 머리 위 알림 도우미입니다.
//
// [성공 기준이 걸린 방 - 형빈 확정]
// "피격 없이 클리어"가 아직 걸려 있는 방 = 아직 클리어하지 않은 일반방/보스방입니다. 이런 방에서 처음 맞았을 때만 실패 알림을 띄웁니다.
// 이미 클리어한 방, 특수방/동아리방/상점/시작방처럼 클리어가 없는 방에서 맞으면 알림이 없습니다. 방을 모를 때(LevelBuilder가 없는 아이템
// 테스트 씬)는 성공 기준이 걸린 것으로 칩니다(알림 확인용).
//
// [알림 색 - 형빈 확정] 성공 = 하늘색(106/111/39번 공통), 실패 = 빨강(103번과 같음).
public static class HeadAlertRoomRules
{
    public static readonly Color SuccessGlow = new Color(0.45f, 0.85f, 1f, 0.9f);
    public static readonly Color SuccessSparkle = new Color(0.6f, 0.92f, 1f, 1f);
    public static readonly Color FailureGlow = new Color(1f, 0.12f, 0.12f, 0.9f);
    public static readonly Color FailureSparkle = new Color(1f, 0.3f, 0.25f, 1f);

    // room(모르면 지금 방)이 성공 기준이 걸린 방인지입니다.
    public static bool IsSuccessCriteriaActive(RoomInstance room)
    {
        if (room == null) room = FindCurrentRoom();
        if (room == null) return true; // 방을 모름(테스트 씬)
        if (room.isCleared) return false;
        return room.category == RoomCategory.Normal || room.category == RoomCategory.Boss;
    }

    // 지금 플레이어가 있는 방입니다(LevelBuilder가 없거나 모르면 null - 103번과 같은 방식).
    public static RoomInstance FindCurrentRoom()
    {
        var levelBuilder = Object.FindFirstObjectByType<LevelBuilder>();
        if (levelBuilder == null || !levelBuilder.HasPlayerCell) return null;

        Vector2Int cell = levelBuilder.CurrentPlayerCell;
        foreach (var r in levelBuilder.GeneratedRooms)
        {
            if (r != null && r.occupiedCells != null && r.occupiedCells.Contains(cell)) return r;
        }
        return null;
    }

    // 기존 아이템 에셋에 연출 칸이 새로 생겨 값이 비어 있는 경우(크기 0 등)를 대비해, 비어 있으면 기본 설정(성공 = 하늘색, 실패 = 빨강)으로
    // 대신합니다. 제대로 채워져 있으면 그대로 돌려줍니다.
    public static CreditPopupSettings Sanitize(CreditPopupSettings settings, bool success)
    {
        if (settings != null && settings.iconSize > 0f && settings.appearDuration > 0f && settings.vanishDuration > 0f) return settings;

        var fallback = new CreditPopupSettings();
        if (settings != null) fallback.icon = settings.icon;
        fallback.glowColor = success ? SuccessGlow : FailureGlow;
        fallback.sparkleColor = success ? SuccessSparkle : FailureSparkle;
        return fallback;
    }
}
