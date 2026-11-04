using System.Collections.Generic;
using UnityEngine;

// [방 단위 판정 공용 도구]
// "이 위치는 어느 방인가", "플레이어가 지금 어느 방에 있는가", "그 방에 플레이어가 있는가"를 한곳에서 판단합니다.
// 원칙: 그 방에서 생긴 일은 그 방에서만 보이고, 그 방에만 영향을 줍니다.
//
// [판정 규칙]
// - 방 = LevelBuilder가 만든 RoomInstance. 칸 좌표는 LevelBuilder와 같은 규칙(월드 X/Z ÷ cellSize 반올림)입니다.
// - 플레이어의 방은 LevelBuilder.CurrentPlayerCell 기준입니다. 이 칸은 경계에서 떨리지 않도록 경계보다 조금 더 걸어 들어가야
//   바뀌므로(히스테리시스), 문 근처 좁은 구간에서는 "위치로 계산한 방"과 다를 수 있습니다. 그래서 무언가의 소속 방을 정할 때는
//   "만들어진 순간 플레이어가 있던 방"(CurrentPlayerRoom)을 쓰고, 위치 기준(RoomOf)은 "방 밖으로 나갔는가" 판정에만 씁니다.
// - 판단할 수 없으면(테스트 씬처럼 LevelBuilder가 없거나, 방 정보가 아직 없을 때) 항상 "플레이어가 그 방에 있다"로 봅니다.
//   판정이 실패했을 때 물건이 사라지는 쪽이 아니라 지금까지처럼 동작하는 쪽으로 가게 하기 위함입니다.
// - 같은 프레임 안에서도 방이 바뀔 수 있으므로(LevelBuilder.Update가 칸을 바꾸고 곧바로 EnterRoom 호출) 결과를 저장해 두지 않고
//   매번 새로 계산합니다. 방 개수가 적어 부담은 없습니다.
//
// [방이 바뀌면 지워지는 오브젝트 - RoomScopedObject]
// 폭발 이펙트처럼 씬 최상위에 만들어지는 짧은 오브젝트는 RoomScope.DestroyWhenPlayerLeaves(오브젝트)로 등록합니다. 플레이어가
// 다른 방에 들어가는 순간(RoomController.OnAnyRoomEntered) 소속 방이 아닌 등록 오브젝트를 전부 지웁니다. 레벨이 넘어가 새 시작방에
// 들어갈 때도 같은 이벤트가 오므로 이전 레벨에서 남은 것도 함께 지워집니다. 방 전환 이벤트는 이 도구 하나만 받고(오브젝트마다
// 구독하지 않음), 등록 명단 하나로 관리합니다. 지울 때는 꼬리(BulletTrail)도 마무리 연출 없이 바로 지웁니다(다른 방 위 허공에
// 꼬리가 남지 않도록).
//
// 이 파일은 읽기 전용 판정 도구라서, 다른 스크립트가 직접 쓰기 전에는 아무 동작도 바꾸지 않습니다.
public static class RoomScope
{
    // LevelBuilder가 없을 때(테스트 씬) 매 호출마다 씬 전체를 뒤지지 않도록, 못 찾았으면 이 간격(초)이 지나야 다시 찾습니다.
    private const float BuilderRetryInterval = 1f;

    private static LevelBuilder cachedBuilder;
    private static float nextBuilderSearchTime;

    private static readonly List<RoomScopedObject> scopedObjects = new List<RoomScopedObject>();
    private static bool subscribed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        cachedBuilder = null;
        nextBuilderSearchTime = 0f;
        scopedObjects.Clear();
        RoomController.OnAnyRoomEntered -= HandleRoomEntered;
        subscribed = false;
    }

    // ------------------------------------------------------------------
    // 방 판정
    // ------------------------------------------------------------------

    private static LevelBuilder Builder
    {
        get
        {
            // Unity 오브젝트는 파괴되면 == null이 되므로, 씬이 바뀐 경우에도 다시 찾습니다.
            if (cachedBuilder == null && Time.unscaledTime >= nextBuilderSearchTime)
            {
                cachedBuilder = Object.FindFirstObjectByType<LevelBuilder>();
                if (cachedBuilder == null) nextBuilderSearchTime = Time.unscaledTime + BuilderRetryInterval;
            }
            return cachedBuilder;
        }
    }

    // 지금 플레이어가 있는 방입니다(판단할 수 없으면 null).
    public static RoomInstance CurrentPlayerRoom()
    {
        var builder = Builder;
        if (builder == null || !builder.HasPlayerCell) return null;
        return RoomOfCell(builder, builder.CurrentPlayerCell);
    }

    // 월드 위치가 속한 방입니다(LevelBuilder가 없거나 어느 방에도 속하지 않으면 null).
    public static RoomInstance RoomOf(Vector3 position)
    {
        var builder = Builder;
        if (builder == null || builder.cellSize <= 0f) return null;
        var cell = new Vector2Int(Mathf.RoundToInt(position.x / builder.cellSize),
                                  Mathf.RoundToInt(position.z / builder.cellSize));
        return RoomOfCell(builder, cell);
    }

    private static RoomInstance RoomOfCell(LevelBuilder builder, Vector2Int cell)
    {
        var rooms = builder.GeneratedRooms;
        for (int i = 0; i < rooms.Count; i++)
        {
            var room = rooms[i];
            if (room != null && room.occupiedCells != null && room.occupiedCells.Contains(cell)) return room;
        }
        return null;
    }

    // room이 지금 레벨에 더 이상 없는지입니다(다음 레벨로 넘어가 방이 새로 만들어진 경우). 판단할 수 없거나 room이 null이면 false.
    public static bool IsRoomGone(RoomInstance room)
    {
        if (room == null) return false;
        var builder = Builder;
        if (builder == null) return false;
        var rooms = builder.GeneratedRooms;
        for (int i = 0; i < rooms.Count; i++)
        {
            if (ReferenceEquals(rooms[i], room)) return false;
        }
        return true;
    }

    // 플레이어가 room에 있는지입니다. room이 null이거나 플레이어의 방을 판단할 수 없으면 true(지금까지처럼 동작),
    // room이 이미 사라진 방이면 false입니다.
    public static bool IsPlayerInRoom(RoomInstance room)
    {
        if (room == null) return true;
        if (IsRoomGone(room)) return false;
        var playerRoom = CurrentPlayerRoom();
        if (playerRoom == null) return true;
        return ReferenceEquals(playerRoom, room);
    }

    // ------------------------------------------------------------------
    // 방이 바뀌면 지워지는 오브젝트
    // ------------------------------------------------------------------

    // go를 "지금 플레이어가 있는 방" 소속으로 등록합니다. 플레이어가 다른 방에 들어가는 순간 지워집니다.
    // 플레이어의 방을 판단할 수 없으면(테스트 씬 등) 등록하지 않습니다(원래대로 스스로 사라질 때까지 남음).
    public static void DestroyWhenPlayerLeaves(GameObject go)
    {
        DestroyWhenPlayerLeaves(go, CurrentPlayerRoom());
    }

    // 소속 방을 직접 정해 등록합니다(room이 null이면 등록하지 않음).
    public static void DestroyWhenPlayerLeaves(GameObject go, RoomInstance room)
    {
        if (go == null || room == null) return;

        var scoped = go.GetComponent<RoomScopedObject>();
        if (scoped == null) scoped = go.AddComponent<RoomScopedObject>();
        scoped.OwnerRoom = room;

        if (!scopedObjects.Contains(scoped)) scopedObjects.Add(scoped);
        EnsureSubscribed();
    }

    internal static void Unregister(RoomScopedObject scoped)
    {
        scopedObjects.Remove(scoped);
    }

    private static void EnsureSubscribed()
    {
        if (subscribed) return;
        // 중복 구독을 막기 위해 한 번 빼고 다시 넣습니다.
        RoomController.OnAnyRoomEntered -= HandleRoomEntered;
        RoomController.OnAnyRoomEntered += HandleRoomEntered;
        subscribed = true;
    }

    private static void HandleRoomEntered(RoomInstance enteredRoom)
    {
        if (scopedObjects.Count == 0) return;

        // Destroy는 프레임 끝에 처리되고 OnDestroy가 명단에서 빼므로, 복사본을 돌며 지웁니다.
        var snapshot = scopedObjects.ToArray();
        for (int i = 0; i < snapshot.Length; i++)
        {
            var scoped = snapshot[i];
            if (scoped == null) { scopedObjects.Remove(scoped); continue; }
            if (ReferenceEquals(scoped.OwnerRoom, enteredRoom)) continue;

            scopedObjects.Remove(scoped);
            BulletTrail.RemoveNow(scoped.gameObject);
            // 이번 프레임 안에 한 번이라도 더 보이거나 동작하지 않도록 먼저 끄고 지웁니다.
            scoped.gameObject.SetActive(false);
            Object.Destroy(scoped.gameObject);
        }
    }
}

// RoomScope.DestroyWhenPlayerLeaves가 자동으로 붙이는 표시용 컴포넌트입니다(직접 붙일 필요 없음).
// 소속 방만 기억하고, 지워질 때 등록 명단에서 자신을 뺍니다.
[DisallowMultipleComponent]
public class RoomScopedObject : MonoBehaviour
{
    public RoomInstance OwnerRoom { get; internal set; }

    void OnDestroy()
    {
        RoomScope.Unregister(this);
    }
}
