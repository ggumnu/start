using System.Collections.Generic;
using UnityEngine;

// [벽 관통 - 정적 등록소 + 방 경계 도우미]
// 120번 패시브 "총알이 벽을 관통한다"를 위한 등록소입니다(WallBounceOverride/35번과 같은 패턴). 등록이 하나라도 살아 있으면 IsActive = true.
// - Item120effect가 생성되는 총알마다 Projectile.SetWallPiercing(true)를 걸고, 조준선(AimTrajectoryIndicator)은 IsActive를 보고 벽에서 끊지 않습니다.
// - 방 경계 도우미(RoomOf / DistanceInsideRoom): 벽을 관통하는 총알(120번, 110번 충전 관통탄)은 방 테두리를 넘는 순간 사라지므로, 총알과
//   조준선이 같은 기준(LevelBuilder의 칸 - 방이 차지한 칸들)으로 "방 안인지"를 판정합니다. LevelBuilder가 없는 씬(아이템 테스트 아레나)에서는
//   방이 모두 null이라 경계가 없는 것으로 칩니다.
// - 플레이 시작 때마다 비워서, 에디터에서 Domain Reload를 꺼 둔 경우에도 이전 플레이의 등록이 남지 않습니다.
public static class WallPierceOverride
{
    private static readonly Dictionary<string, Object> entries = new Dictionary<string, Object>();
    private static LevelBuilder cachedLevelBuilder;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlayStart()
    {
        entries.Clear();
        cachedLevelBuilder = null;
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

    public static bool IsActive
    {
        get
        {
            if (entries.Count == 0) return false;
            List<string> dead = null;
            bool any = false;
            foreach (var kvp in entries)
            {
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

    // ---- 방 경계 ----

    private static LevelBuilder GetLevelBuilder()
    {
        if (cachedLevelBuilder == null) cachedLevelBuilder = Object.FindFirstObjectByType<LevelBuilder>();
        return cachedLevelBuilder;
    }

    // 월드 위치가 속한 방입니다(LevelBuilder가 없거나 어느 방에도 속하지 않으면 null). ClaymoreOverride.RoomOf와 같은 칸 계산입니다.
    public static RoomInstance RoomOf(Vector3 position)
    {
        var levelBuilder = GetLevelBuilder();
        if (levelBuilder == null || levelBuilder.cellSize <= 0f) return null;
        var cell = new Vector2Int(Mathf.RoundToInt(position.x / levelBuilder.cellSize),
                                  Mathf.RoundToInt(position.z / levelBuilder.cellSize));
        foreach (var r in levelBuilder.GeneratedRooms)
        {
            if (r != null && r.occupiedCells != null && r.occupiedCells.Contains(cell)) return r;
        }
        return null;
    }

    // origin에서 direction으로 maxDistance까지 가는 동안 origin이 있던 방을 벗어나기 전까지의 거리입니다(조준선용).
    // origin이 어느 방에도 없으면(테스트 씬 등) maxDistance 그대로입니다.
    public static float DistanceInsideRoom(Vector3 origin, Vector3 direction, float maxDistance)
    {
        if (maxDistance <= 0f) return 0f;
        RoomInstance room = RoomOf(origin);
        if (room == null) return maxDistance;

        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) return maxDistance;
        direction.Normalize();

        const float step = 0.25f;
        float inside = 0f;
        for (float d = step; ; d += step)
        {
            float probe = Mathf.Min(d, maxDistance);
            if (!ReferenceEquals(RoomOf(origin + direction * probe), room))
            {
                // inside ~ probe 사이에서 경계를 좁혀 찾습니다.
                float lo = inside, hi = probe;
                for (int i = 0; i < 6; i++)
                {
                    float mid = (lo + hi) * 0.5f;
                    if (ReferenceEquals(RoomOf(origin + direction * mid), room)) lo = mid;
                    else hi = mid;
                }
                return lo;
            }
            inside = probe;
            if (probe >= maxDistance) return maxDistance;
        }
    }
}
