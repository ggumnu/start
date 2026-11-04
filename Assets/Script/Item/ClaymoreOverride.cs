using System.Collections.Generic;
using UnityEngine;

// [크레모어 - 정적 등록소 + 설치된 크레모어 목록]
// 115번 패시브 "수류탄이 크레모어로 바뀐다"를 위한 등록소입니다. GrenadeSplitOverride(92번)/ExecuteOverride(98번)와 같은 구조입니다.
//
// - 115번 효과가 Register(이름, 소유자, 설정)로 등록하면, 등록이 남아 있는 동안 던지는 수류탄은 크레모어가 됩니다(던지는 순간 기준).
// - 던진 크레모어(날아가는 중 + 설치된 것)는 Track/Untrack으로 이 목록에 들어갑니다. 각 크레모어는 "던질 때 플레이어가 있던 방"을
//   기억합니다(92번 분열 크레모어는 원래 것의 방을 물려받음).
// - DetonateInRoom(방): 그 방의 크레모어만 모두 터뜨립니다(수류탄 모드 R - 다른 방에 설치한 것은 터지지 않음, 형빈 확정).
// - DetonateAll(): 모든 방의 크레모어를 터뜨립니다(115번을 잃을 때 - 형빈 확정).
// - 설치된(착지한) 크레모어만 터집니다. 아직 날아가는 크레모어는 R로 터지지 않고(형빈 확정 - 공중 폭파 없음), DetonateAll 때는 일반
//   수류탄으로 되돌려 착지하면 평소처럼 터지게 합니다.
// - 방 판정은 LevelBuilder 기준입니다. LevelBuilder가 없는 씬(아이템 테스트 아레나)에서는 방이 모두 null이라 전부 같은 방으로 취급합니다.
// - 플레이 시작 때마다 비워서, 에디터에서 Domain Reload를 꺼 둔 경우에도 이전 플레이의 등록이 남지 않습니다.
public static class ClaymoreOverride
{
    // 115번 설정입니다. 115번 Item Definition의 인스펙터에서 조절합니다.
    [System.Serializable]
    public class Settings
    {
        [Header("설치 중 빨간 점멸 (81번 소이탄 발광보다 크게)")]
        [Tooltip("빛 그림 색입니다(알파 = 가장 밝을 때의 진하기).")]
        public Color glowColor = new Color(1f, 0.05f, 0.05f, 0.9f);

        [Tooltip("빛 그림 지름(m)입니다. 81번 소이탄 발광 기본값은 0.6입니다.")]
        [Min(0.01f)] public float glowSize = 1.2f;

        [Tooltip("가장 어두울 때의 밝기 비율입니다(0 = 완전히 꺼짐, 1 = 점멸 없음).")]
        [Range(0f, 1f)] public float glowMinBrightness = 0.15f;

        [Tooltip("밝음 → 서서히 어두움 → 서서히 밝음 한 번에 걸리는 시간(초)입니다.")]
        [Min(0.1f)] public float pulsePeriod = 1.6f;

        [Tooltip("가장 어두울 때 빛 그림 크기 비율입니다(밝을수록 1에 가까워짐). 1이면 크기는 변하지 않습니다.")]
        [Range(0.1f, 1f)] public float glowMinScale = 0.85f;

        [Tooltip("켜면 빨간 점광원(주변 바닥을 비추는 빛)도 같이 점멸합니다.")]
        public bool enableGlowLight = true;

        [Tooltip("점광원 색입니다.")]
        public Color glowLightColor = new Color(1f, 0.1f, 0.05f, 1f);

        [Tooltip("가장 밝을 때 점광원 세기입니다(81번 기본 2).")]
        [Min(0f)] public float glowLightIntensity = 4f;

        [Tooltip("점광원이 비추는 거리(m)입니다(81번 기본 1.5).")]
        [Min(0.01f)] public float glowLightRange = 3f;

        [Tooltip("크레모어 중심에서 빛을 옮기는 값(월드, m)입니다. 바닥에 묻혀 보이면 Y를 올리세요.")]
        public Vector3 glowOffset = new Vector3(0f, 0.1f, 0f);
    }

    private class Entry
    {
        public Object owner;
        public Settings settings;
        public int order;
    }

    private static readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>();
    private static int registerCounter;

    private static readonly List<Grenade> tracked = new List<Grenade>();

    // [118번 포격 연동] 수류탄이 아닌 "크레모어처럼 기다리는 것"(118번 크레모어 포격)도 같은 R/115번 잃기에 반응하도록 등록하는 곳입니다.
    // 함수는 (방, 모든 방인지)를 받아 그 순간 터뜨리기(포격 시작)를 시작한 개수를 돌려줍니다. 이름 하나당 하나만 등록됩니다.
    private static readonly Dictionary<string, System.Func<RoomInstance, bool, int>> externalDetonators =
        new Dictionary<string, System.Func<RoomInstance, bool, int>>();

    public static void SetExternalDetonator(string sourceId, System.Func<RoomInstance, bool, int> detonator)
    {
        if (string.IsNullOrEmpty(sourceId) || detonator == null) return;
        externalDetonators[sourceId] = detonator;
    }
    private static readonly List<Grenade> detonateBuffer = new List<Grenade>();

    // 이번 프레임의 "플레이어가 있는 방" 캐시(크레모어마다 매 프레임 묻기 때문)
    private static int cachedRoomFrame = -1;
    private static RoomInstance cachedRoom;
    private static LevelBuilder cachedLevelBuilder;

    public static void Register(string sourceId, Object owner, Settings settings)
    {
        if (string.IsNullOrEmpty(sourceId) || settings == null) return;
        entries[sourceId] = new Entry { owner = owner, settings = settings, order = ++registerCounter };
    }

    public static void Unregister(string sourceId)
    {
        if (string.IsNullOrEmpty(sourceId)) return;
        entries.Remove(sourceId);
    }

    public static bool TryGetActive(out Settings settings)
    {
        PruneDead();

        settings = null;
        int bestOrder = int.MinValue;
        foreach (var entry in entries.Values)
        {
            if (entry.order > bestOrder)
            {
                bestOrder = entry.order;
                settings = entry.settings;
            }
        }
        return settings != null;
    }

    // ---- 크레모어 목록 ----

    public static void Track(Grenade claymore)
    {
        if (claymore != null && !tracked.Contains(claymore)) tracked.Add(claymore);
    }

    public static void Untrack(Grenade claymore)
    {
        tracked.Remove(claymore);
    }

    // room에 속한 크레모어(날아가는 중 포함)를 모두 터뜨리고, 터뜨린 개수를 돌려줍니다.
    public static int DetonateInRoom(RoomInstance room)
    {
        return Detonate(room, allRooms: false);
    }

    // 모든 방의 크레모어를 터뜨리고, 터뜨린 개수를 돌려줍니다.
    public static int DetonateAll()
    {
        return Detonate(null, allRooms: true);
    }

    private static int Detonate(RoomInstance room, bool allRooms)
    {
        // 터지면서 목록에서 빠지므로 복사본으로 돕니다.
        detonateBuffer.Clear();
        for (int i = 0; i < tracked.Count; i++)
        {
            Grenade g = tracked[i];
            if (g == null) continue;
            if (!allRooms && !ReferenceEquals(g.ClaymoreRoom, room)) continue;
            detonateBuffer.Add(g);
        }

        int count = 0;
        for (int i = 0; i < detonateBuffer.Count; i++)
        {
            Grenade g = detonateBuffer[i];
            if (g == null) continue;
            try
            {
                if (g.DetonateClaymore()) count++;
                else if (allRooms) g.RevertToNormalGrenade(); // 115번을 잃을 때 날아가던 크레모어 → 일반 수류탄
            }
            catch (System.Exception e)
            {
                Debug.LogError($"ClaymoreOverride: 크레모어 폭발 중 오류가 발생해 건너뜁니다: {e}");
            }
        }
        detonateBuffer.Clear();
        tracked.RemoveAll(g => g == null);

        // [118번 포격 연동] 등록된 다른 "크레모어처럼 기다리는 것"도 같은 규칙으로 터뜨립니다.
        if (externalDetonators.Count > 0)
        {
            foreach (var detonator in new List<System.Func<RoomInstance, bool, int>>(externalDetonators.Values))
            {
                try
                {
                    count += Mathf.Max(0, detonator(room, allRooms));
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"ClaymoreOverride: 등록된 크레모어 처리(118번 포격 등) 중 오류가 발생해 건너뜁니다: {e}");
                }
            }
        }
        return count;
    }

    // ---- 방 판정 ----

    private static LevelBuilder GetLevelBuilder()
    {
        if (cachedLevelBuilder == null) cachedLevelBuilder = Object.FindFirstObjectByType<LevelBuilder>();
        return cachedLevelBuilder;
    }

    // 지금 플레이어가 있는 방입니다(LevelBuilder가 없거나 모르면 null).
    public static RoomInstance CurrentPlayerRoom()
    {
        if (cachedRoomFrame == Time.frameCount) return cachedRoom;
        cachedRoomFrame = Time.frameCount;
        cachedRoom = null;

        var levelBuilder = GetLevelBuilder();
        if (levelBuilder == null || !levelBuilder.HasPlayerCell) return null;
        cachedRoom = FindRoomByCell(levelBuilder, levelBuilder.CurrentPlayerCell);
        return cachedRoom;
    }

    // 월드 위치 position이 속한 방입니다(LevelBuilder가 없거나 어느 방에도 속하지 않으면 null).
    public static RoomInstance RoomOf(Vector3 position)
    {
        var levelBuilder = GetLevelBuilder();
        if (levelBuilder == null || levelBuilder.cellSize <= 0f) return null;
        var cell = new Vector2Int(Mathf.RoundToInt(position.x / levelBuilder.cellSize),
                                  Mathf.RoundToInt(position.z / levelBuilder.cellSize));
        return FindRoomByCell(levelBuilder, cell);
    }

    // room이 지금 레벨에 더 이상 없는지입니다(다음 레벨로 넘어가 방이 새로 만들어진 경우). LevelBuilder가 없거나 room이 null이면 false.
    public static bool IsRoomGone(RoomInstance room)
    {
        if (room == null) return false;
        var levelBuilder = GetLevelBuilder();
        if (levelBuilder == null) return false;
        foreach (var r in levelBuilder.GeneratedRooms)
        {
            if (ReferenceEquals(r, room)) return false;
        }
        return true;
    }

    private static RoomInstance FindRoomByCell(LevelBuilder levelBuilder, Vector2Int cell)
    {
        foreach (var r in levelBuilder.GeneratedRooms)
        {
            if (r != null && r.occupiedCells != null && r.occupiedCells.Contains(cell)) return r;
        }
        return null;
    }

    private static void PruneDead()
    {
        List<string> deadKeys = null;
        foreach (var kvp in entries)
        {
            if (kvp.Value.owner != null) continue;
            if (deadKeys == null) deadKeys = new List<string>();
            deadKeys.Add(kvp.Key);
        }
        if (deadKeys == null) return;
        foreach (var key in deadKeys) entries.Remove(key);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlayStart()
    {
        entries.Clear();
        registerCounter = 0;
        tracked.Clear();
        detonateBuffer.Clear();
        externalDetonators.Clear();
        cachedRoomFrame = -1;
        cachedRoom = null;
        cachedLevelBuilder = null;
    }
}
