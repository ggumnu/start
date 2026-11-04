using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// [87번 포섭 - 3단계: 방 이동 / 층 이동]
// 포섭된 아군이 아이작의 패밀리어처럼 플레이어를 따라 방을 옮기게 합니다(AllyFaction이 자동으로 씀 - 씬/프리팹에 넣을 필요 없음).
//
// [형빈 확정]
// - 플레이어가 다른 방에 들어가면 아군은 플레이어 곁으로 순간이동합니다. 그 방의 적과 겹치지 않는 곳에 나타납니다.
// - 다음 층(선택 층 포함)으로 넘어가면 아군은 사라집니다.
//
// [구현]
// - 포섭하는 순간 아군을 방 오브젝트(Content) 밑에서 빼서, 층 전체 묶음(LevelRoot) 밑에 만든 "Allies (87)" 오브젝트 밑으로 옮깁니다.
//   방 오브젝트는 플레이어가 없으면 꺼지는데(RoomVisibilityManager), 그 밑에 있으면 아군도 같이 꺼지기 때문입니다.
//   층을 넘어갈 때 LevelBuilder가 LevelRoot를 통째로 지우므로 아군도 함께 지워집니다(따로 정리할 것 없음).
// - 방에 들어가는 순간(RoomController.OnAnyRoomEntered) 아군을 잠깐 꺼 둡니다. 이 시점엔 아직 이전 방의 NavMesh가 켜져 있고
//   새 방의 NavMesh는 꺼져 있어서, 바로 옮기면 NavMeshAgent가 길을 못 찾습니다. 다음 프레임(새 방이 켜지고 적까지 다시 생긴 뒤)에
//   자리를 골라 옮긴 다음 다시 켭니다. 꺼져 있는 한 프레임 동안은 보이지 않을 뿐 체력/상태는 그대로입니다.
// - 자리 고르기: 플레이어 주변 teleportMinDistance~teleportMaxDistance(m) 고리 안에서 무작위로 골라, NavMesh 위이고, 플레이어에서
//   벽에 막히지 않고 곧장 닿는 곳이며, 적과 enemyClearance(m) 이상, 다른 아군과 0.9m 이상 떨어진 곳을 씁니다. 못 찾으면 적 거리 조건을
//   빼고 다시 찾고, 그래도 없으면 플레이어 발밑에 둡니다(아군은 플레이어와 부딪혀도 피해가 없음).
// - 같은 방에 다시 "들어가는" 신호가 와도(문턱을 오가는 경우 등) 방이 바뀌지 않았으면 아무것도 하지 않습니다.
public class AllyRoomTransfer : MonoBehaviour
{
    private const string HolderName = "Allies (87)";
    private const float AllySpacing = 0.9f;
    private const int SearchAttempts = 32;
    private const int RelaxedAttemptsFrom = 20; // 이 횟수부터는 적과의 거리 조건을 빼고 찾습니다.

    private static AllyRoomTransfer instance;
    private static RoomInstance lastRoom;

    private readonly List<AllyFaction> pending = new List<AllyFaction>();
    private readonly List<Vector3> placedPositions = new List<Vector3>();
    private Coroutine placeRoutine;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlayStart()
    {
        instance = null;
        lastRoom = null;
        RoomController.OnAnyRoomEntered -= HandleRoomEntered;
        RoomController.OnAnyRoomEntered += HandleRoomEntered;
    }

    // 포섭 직후 AllyFaction이 부릅니다: 아군을 방 오브젝트 밑에서 빼서 아군 묶음 밑으로 옮깁니다(월드 위치 그대로).
    public static void Adopt(AllyFaction ally)
    {
        if (ally == null) return;
        var holder = GetOrCreateHolder(ally.transform);
        if (holder == null) return;
        ally.transform.SetParent(holder.transform, true);
    }

    private static AllyRoomTransfer GetOrCreateHolder(Transform from)
    {
        if (instance != null) return instance; // 이전 층의 묶음은 LevelRoot와 함께 지워져 여기서 null이 됩니다.

        Transform levelRoot = FindLevelRoot(from);
        var go = new GameObject(HolderName);
        if (levelRoot != null) go.transform.SetParent(levelRoot, false);
        instance = go.AddComponent<AllyRoomTransfer>();
        return instance;
    }

    // from의 부모들 중 LevelBuilder가 만든 "LevelRoot"를 찾습니다(테스트 아레나처럼 없으면 null - 그때는 씬 최상위에 묶음을 둠).
    private static Transform FindLevelRoot(Transform from)
    {
        for (Transform t = from; t != null; t = t.parent)
        {
            if (t.name == "LevelRoot") return t;
        }
        return null;
    }

    private static void HandleRoomEntered(RoomInstance room)
    {
        if (room == lastRoom) return;
        lastRoom = room;

        if (instance == null || AllyFaction.All.Count == 0) return;
        instance.BeginTransfer();
    }

    private void BeginTransfer()
    {
        var allies = AllyFaction.All;
        for (int i = 0; i < allies.Count; i++)
        {
            var ally = allies[i];
            if (ally == null || ally.Health == null || ally.Health.IsDead) continue;
            if (pending.Contains(ally)) continue;

            ally.PrepareForRoomTransfer();
            ally.gameObject.SetActive(false);
            pending.Add(ally);
        }

        if (pending.Count == 0) return;
        if (placeRoutine != null) StopCoroutine(placeRoutine);
        placeRoutine = StartCoroutine(PlaceNextFrame());
    }

    private IEnumerator PlaceNextFrame()
    {
        yield return null; // 새 방 켜짐(NavMesh 포함) + 적 다시 생성이 끝난 뒤

        placeRoutine = null;

        var playerGO = GameObject.FindGameObjectWithTag("Player");
        if (playerGO == null) yield break; // 플레이어가 없으면 다음 방 이동 때 다시 시도합니다.
        Transform player = playerGO.transform;

        placedPositions.Clear();
        for (int i = 0; i < pending.Count; )
        {
            var ally = pending[i];
            if (ally == null || ally.Health == null || ally.Health.IsDead)
            {
                pending.RemoveAt(i);
                continue;
            }

            if (TryFindSpot(ally, player, out Vector3 spot))
            {
                Place(ally, spot, player);
                placedPositions.Add(spot);
                pending.RemoveAt(i);
            }
            else
            {
                i++; // 이 방에 NavMesh가 없는 등 자리를 못 찾음 - 꺼진 채로 두고 다음 방 이동 때 다시 시도합니다.
            }
        }
    }

    private void Place(AllyFaction ally, Vector3 spot, Transform player)
    {
        ally.transform.position = spot; // 켜기 전에 옮겨야 NavMeshAgent가 새 방 NavMesh 위에서 시작합니다.

        Vector3 facing = player.forward;
        facing.y = 0f;
        if (facing.sqrMagnitude > 0.0001f) ally.transform.rotation = Quaternion.LookRotation(facing);

        ally.gameObject.SetActive(true);

        var agent = ally.GetComponent<NavMeshAgent>();
        if (agent != null && agent.isActiveAndEnabled)
        {
            agent.Warp(spot);
            if (agent.isOnNavMesh) agent.ResetPath();
        }
    }

    private bool TryFindSpot(AllyFaction ally, Transform player, out Vector3 spot)
    {
        var s = ally.ActiveSettings ?? new AllyFaction.Settings();
        var agent = ally.GetComponent<NavMeshAgent>();
        var filter = new NavMeshQueryFilter
        {
            agentTypeID = agent != null ? agent.agentTypeID : 0,
            areaMask = NavMesh.AllAreas
        };

        // 플레이어 발밑의 NavMesh 지점(기준점)
        if (!NavMesh.SamplePosition(player.position, out NavMeshHit originHit, 3f, filter))
        {
            spot = default;
            return false;
        }
        Vector3 origin = originHit.position;

        float minR = Mathf.Max(0f, s.teleportMinDistance);
        float maxR = Mathf.Max(minR + 0.1f, s.teleportMaxDistance);

        for (int attempt = 0; attempt < SearchAttempts; attempt++)
        {
            bool relaxed = attempt >= RelaxedAttemptsFrom;

            Vector2 dir = Random.insideUnitCircle;
            if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;
            dir.Normalize();
            float radius = Random.Range(minR, maxR);
            Vector3 candidate = origin + new Vector3(dir.x, 0f, dir.y) * radius;

            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 1f, filter)) continue;
            if (NavMesh.Raycast(origin, hit.position, out _, filter)) continue; // 벽/장애물에 막힘
            if (!relaxed && IsNearHostile(hit.position, s.enemyClearance)) continue;
            if (IsNearPlaced(hit.position)) continue;

            spot = hit.position;
            return true;
        }

        spot = origin;
        return true;
    }

    private static bool IsNearHostile(Vector3 pos, float clearance)
    {
        if (clearance <= 0f) return false;
        float sqr = clearance * clearance;
        var enemies = EnemyHealth.ActiveEnemies;
        for (int i = 0; i < enemies.Count; i++)
        {
            var e = enemies[i];
            if (!CombatTargeting.IsHostileAlive(e)) continue;
            Vector3 d = e.transform.position - pos;
            d.y = 0f;
            if (d.sqrMagnitude < sqr) return true;
        }
        return false;
    }

    private bool IsNearPlaced(Vector3 pos)
    {
        float sqr = AllySpacing * AllySpacing;
        for (int i = 0; i < placedPositions.Count; i++)
        {
            Vector3 d = placedPositions[i] - pos;
            d.y = 0f;
            if (d.sqrMagnitude < sqr) return true;
        }
        return false;
    }
}
