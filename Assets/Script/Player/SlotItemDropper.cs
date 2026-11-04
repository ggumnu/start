using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// [슬롯 아이템 버리기 - 실제 처리 (형빈 확정)]
// 버리기 창(SlotItemDropUI)이 쓰는 "판정/자리 찾기/버리기" 기능을 모아둔 곳입니다. 창은 화면만 담당하고 실제 일은 여기서 합니다.
//
// [전투가 없는 방에서만]
// 지금 플레이어가 있는 방이 "클리어 전 전투방"(일반방/보스방에서 적이 남은 상태)이면 버릴 수 없습니다. 동아리방/상점/시작방/
// 특수방/선택방처럼 전투가 없는 방, 이미 클리어한 전투방에서는 버릴 수 있습니다. 아이템 테스트 씬처럼 LevelBuilder가 없는
// 곳에서는 막을 기준이 없으므로 항상 허용합니다(테스트용).
//
// [버릴 자리 - 보물상자가 나타날 수 있는 자리와 같은 기준]
// 플레이어 주변부터 바깥쪽으로 원을 그리며 후보를 확인하고, 아래를 모두 만족하는 자리에 놓습니다.
// - 바닥(NavMesh) 위이고, 벽/장애물과 겹치지 않음(일반방 보물상자 스폰과 같은 NavMesh 허용 오차/겹침 반경/겹침 레이어 - 그
//   스포너의 값을 그대로 씀. 바닥 자체나 플레이어 몸과 겹치는 것은 막힌 것으로 치지 않음)
// - 지금 방 안(방이 차지한 칸 안)이고, 플레이어가 걸어서 갈 수 있음
// - 플레이어/다른 버린 아이템/방에 있는 상자·좌대·상인·포탈과 일정 거리 이상 떨어짐
// 고른 아이템 수만큼 자리를 못 찾으면 하나도 버리지 않습니다(창이 "현재 방에서 버릴 수 없습니다"를 띄움).
//
// [버린 아이템 - 보물상자가 아니라 임시 좌대]
// 버린 아이템은 상점의 임시 좌대와 같은 HoldingPedestal에 올라갑니다. 보물상자 취급이 아니라서 9번(상자 복제)/48번 등의 대상이
// 아니고, F로 다시 가져가거나 교체할 수 있습니다. 그 방 오브젝트 아래에 놓여 방을 나갔다 와도 남아 있고, 미니맵에도 "아이템"
// 아이콘이 남습니다(HoldingPedestal이 IMinimapMarker). 다 가져가서 비면 좌대가 스스로 사라집니다(DroppedItemPedestal).
public static class SlotItemDropper
{
    // 버리기 창의 칸 순서와 같습니다(화면 왼쪽부터 장신구 -> 일회용 -> 음식).
    public enum SlotKind { Trinket = 0, Disposable = 1, Food = 2 }

    public const int SlotKindCount = 3;

    // 종류마다 최대 칸 수(슬롯 HUD와 같은 2칸 - 장신구는 55번, 일회용은 61번, 음식은 79번으로 2칸까지 씀)
    public const int MaxSlotsPerKind = 2;

    // 플레이어 루트(PlayerHealth가 붙은 오브젝트)를 찾습니다. 없거나 사망했으면 false.
    public static bool TryGetLivePlayer(out GameObject player)
    {
        player = null;
        var health = Object.FindFirstObjectByType<PlayerHealth>();
        if (health == null || health.IsDead) return false;
        player = health.gameObject;
        return true;
    }

    // 그 종류의 index번째 칸에 지금 장착 중인 아이템(없으면 null). 장신구는 55번, 일회용은 61번, 음식은 79번으로 2칸일 수
    // 있습니다(먼저 얻은 것이 0번).
    public static ItemDefinition GetEquipped(GameObject player, SlotKind kind, int index)
    {
        if (player == null) return null;
        if (kind == SlotKind.Trinket)
        {
            var trinket = player.GetComponent<PlayerTrinket>();
            return trinket != null ? trinket.GetEquippedAt(index) : null;
        }
        if (kind == SlotKind.Disposable)
        {
            var disposable = player.GetComponent<PlayerDisposableItem>();
            return disposable != null ? disposable.GetEquippedAt(index) : null;
        }
        if (kind == SlotKind.Food)
        {
            var food = player.GetComponent<PlayerFoodItem>();
            return food != null ? food.GetEquippedAt(index) : null;
        }
        return null;
    }

    public static bool HasAnySlotItem(GameObject player)
    {
        for (int i = 0; i < SlotKindCount; i++)
        {
            for (int k = 0; k < MaxSlotsPerKind; k++)
            {
                if (GetEquipped(player, (SlotKind)i, k) != null) return true;
            }
        }
        return false;
    }

    // 지금 방에서 버리기 창을 열 수 있는지(전투가 없는 방인지)
    public static bool IsInCombatFreeRoom()
    {
        return TryGetCurrentRoom(out _, out _, out bool combatFree, out _) && combatFree;
    }

    // 지금 플레이어가 있는 방 정보입니다. LevelBuilder가 없으면(테스트 씬) room/controller는 null이고 combatFree는 true.
    // LevelBuilder는 있는데 플레이어가 어느 방에 있는지 모르면 false.
    private static bool TryGetCurrentRoom(out RoomInstance room, out RoomController controller, out bool combatFree,
                                          out LevelBuilder levelBuilder)
    {
        room = null;
        controller = null;
        combatFree = true;

        levelBuilder = Object.FindFirstObjectByType<LevelBuilder>();
        if (levelBuilder == null) return true;
        if (!levelBuilder.HasPlayerCell) return false;

        Vector2Int cell = levelBuilder.CurrentPlayerCell;
        foreach (var r in levelBuilder.GeneratedRooms)
        {
            if (r != null && r.occupiedCells != null && r.occupiedCells.Contains(cell))
            {
                room = r;
                break;
            }
        }
        if (room == null) return false;

        controller = levelBuilder.GetRoomController(room.id);
        combatFree = controller == null || !controller.RequiresClear || room.isCleared;
        return true;
    }

    // ------------------------------------------------------------------
    // 자리 찾기
    // ------------------------------------------------------------------

    // count개의 버릴 자리를 찾습니다. 다 찾지 못하면 false(하나도 버리면 안 됨). parent = 좌대를 넣을 방 오브젝트(없으면 null).
    public static bool TryFindDropPositions(GameObject player, int count, out List<Vector3> positions, out Transform parent)
    {
        return FindDropPositions(player, count, true, out positions, out parent);
    }

    // requireCombatFree = false면 전투 중인 방에서도 찾습니다(55번을 잃어 넘친 장신구 떨어뜨리기용). 이때 플레이어가 어느 방에
    // 있는지 모르면 방 칸 확인 없이 찾습니다.
    private static bool FindDropPositions(GameObject player, int count, bool requireCombatFree,
                                          out List<Vector3> positions, out Transform parent)
    {
        positions = new List<Vector3>();
        parent = null;
        if (player == null || count <= 0) return false;

        bool roomKnown = TryGetCurrentRoom(out RoomInstance room, out RoomController controller, out bool combatFree,
                                           out LevelBuilder levelBuilder);
        if (requireCombatFree)
        {
            if (!roomKnown || !combatFree) return false;
        }
        else if (!roomKnown)
        {
            room = null;
            controller = null;
        }

        if (controller != null && controller.ContentRoot != null) parent = controller.ContentRoot.transform;

        var settings = SlotDropSettings.Get();
        float sampleDistance = settings.navMeshSampleDistance;
        float overlapRadius = settings.overlapCheckRadius;
        LayerMask overlapMask = settings.overlapCheckMask;
        if (settings.useTreasureSpawnerValues && levelBuilder != null && levelBuilder.treasureSpawner != null)
        {
            sampleDistance = levelBuilder.treasureSpawner.navMeshSampleDistance;
            overlapRadius = levelBuilder.treasureSpawner.overlapCheckRadius;
            overlapMask = levelBuilder.treasureSpawner.overlapCheckMask;
        }
        float cellSize = levelBuilder != null ? levelBuilder.cellSize : 0f;

        Vector3 playerPos = player.transform.position;
        bool hasNavOrigin = NavMesh.SamplePosition(playerPos, out NavMeshHit originHit, Mathf.Max(sampleDistance, 1f) * 2f,
                                                   NavMesh.AllAreas);
        var path = new NavMeshPath();

        var blockers = CollectBlockerPositions();
        float minSpacingSqr = settings.minSpacing * settings.minSpacing;
        float minPlayerSqr = settings.minDistanceFromPlayer * settings.minDistanceFromPlayer;

        float step = Mathf.Max(0.1f, settings.searchStep);
        float startRadius = Mathf.Max(step, settings.minDistanceFromPlayer);

        for (float radius = startRadius; radius <= settings.searchMaxRadius + 0.001f && positions.Count < count; radius += step)
        {
            int samples = Mathf.Max(8, Mathf.CeilToInt(2f * Mathf.PI * radius / step));
            float startAngle = Random.Range(0f, 360f);

            for (int i = 0; i < samples && positions.Count < count; i++)
            {
                float angle = (startAngle + 360f * i / samples) * Mathf.Deg2Rad;
                Vector3 candidate = playerPos + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;

                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, sampleDistance, NavMesh.AllAreas)) continue;
                Vector3 p = hit.position;

                // 지금 방 안인지(방이 차지한 칸 안) - LevelBuilder와 같은 칸 계산
                if (room != null && cellSize > 0f)
                {
                    var cell = new Vector2Int(Mathf.RoundToInt(p.x / cellSize), Mathf.RoundToInt(p.z / cellSize));
                    if (!room.occupiedCells.Contains(cell)) continue;
                }

                if (FlatDistanceSqr(p, playerPos) < minPlayerSqr) continue;
                if (TooClose(p, blockers, minSpacingSqr) || TooClose(p, positions, minSpacingSqr)) continue;

                // 벽/장애물과 겹치는지 - 일반방 보물상자 스폰과 같은 반경/레이어로 검사합니다.
                if (OverlapsObstacle(p, overlapRadius, overlapMask, player.transform)) continue;

                // 플레이어가 걸어서 갈 수 있는 자리인지
                if (hasNavOrigin)
                {
                    if (!NavMesh.CalculatePath(originHit.position, p, NavMesh.AllAreas, path)) continue;
                    if (path.status != NavMeshPathStatus.PathComplete) continue;
                }

                positions.Add(p);
            }
        }

        return positions.Count >= count;
    }

    // 구 안에 벽/장애물 콜라이더가 있는지. 겹침 레이어에 바닥이 포함돼 있어도 바닥 때문에 항상 실패하지 않도록 구를 바닥에서
    // 반경만큼 살짝 띄워 검사하고, 플레이어 자신의 콜라이더는 무시합니다(플레이어 바로 옆 자리가 막히지 않도록).
    private static readonly Collider[] overlapBuffer = new Collider[32];

    private static bool OverlapsObstacle(Vector3 floorPoint, float radius, LayerMask mask, Transform playerRoot)
    {
        Vector3 center = floorPoint + Vector3.up * (radius + 0.05f);
        int count = Physics.OverlapSphereNonAlloc(center, radius, overlapBuffer, mask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            var col = overlapBuffer[i];
            if (col == null) continue;
            if (playerRoot != null && col.transform.IsChildOf(playerRoot)) continue;
            return true;
        }
        return false;
    }

    // 버린 아이템이 너무 붙지 않게 피할 기존 물건들의 위치
    private static List<Vector3> CollectBlockerPositions()
    {
        var list = new List<Vector3>();
        foreach (var c in Object.FindObjectsByType<TreasureChest>(FindObjectsSortMode.None)) list.Add(c.transform.position);
        foreach (var c in Object.FindObjectsByType<ItemPedestal>(FindObjectsSortMode.None)) list.Add(c.transform.position);
        foreach (var c in Object.FindObjectsByType<HoldingPedestal>(FindObjectsSortMode.None)) list.Add(c.transform.position);
        foreach (var c in Object.FindObjectsByType<PresetBasicItemChest>(FindObjectsSortMode.None)) list.Add(c.transform.position);
        foreach (var c in Object.FindObjectsByType<ShopNPC>(FindObjectsSortMode.None)) list.Add(c.transform.position);
        foreach (var c in Object.FindObjectsByType<LevelPortal>(FindObjectsSortMode.None)) list.Add(c.transform.position);
        return list;
    }

    private static bool TooClose(Vector3 p, List<Vector3> others, float minSqr)
    {
        for (int i = 0; i < others.Count; i++)
        {
            if (FlatDistanceSqr(p, others[i]) < minSqr) return true;
        }
        return false;
    }

    private static float FlatDistanceSqr(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return dx * dx + dz * dz;
    }

    // ------------------------------------------------------------------
    // 버리기
    // ------------------------------------------------------------------

    // 그 종류의 아이템 item을 장착 해제합니다(장신구는 효과 해제, 일회용/음식은 지연 소모 대기 중이면 대기 취소).
    // 장신구/일회용/음식은 칸이 여러 개일 수 있어(55번/61번/79번) 어떤 아이템인지 item으로 고릅니다.
    public static bool TryUnequip(GameObject player, SlotKind kind, ItemDefinition item, out ItemDefinition removed)
    {
        removed = null;
        if (player == null || item == null) return false;
        switch (kind)
        {
            case SlotKind.Trinket:
                var trinket = player.GetComponent<PlayerTrinket>();
                return trinket != null && trinket.TryUnequip(item, out removed);
            case SlotKind.Disposable:
                var disposable = player.GetComponent<PlayerDisposableItem>();
                return disposable != null && disposable.TryUnequip(item, out removed);
            case SlotKind.Food:
                var food = player.GetComponent<PlayerFoodItem>();
                return food != null && food.TryUnequip(item, out removed);
        }
        return false;
    }

    // [55번 연동] 장신구 칸이 줄어 넘친 장신구처럼, 버리기 창을 거치지 않고 아이템 하나를 지금 방 바닥에 좌대로 떨어뜨립니다.
    // 버리기와 같은 기준으로 자리를 찾되 전투 중인 방에서도 찾고, 자리가 없으면 플레이어 발밑에 놓습니다(아이템이 사라지지 않도록).
    public static HoldingPedestal DropNearPlayer(GameObject player, ItemDefinition item)
    {
        if (player == null || item == null) return null;

        Vector3 position;
        if (FindDropPositions(player, 1, false, out List<Vector3> positions, out Transform parent) && positions.Count > 0)
        {
            position = positions[0];
        }
        else
        {
            position = player.transform.position;
            if (NavMesh.SamplePosition(position, out NavMeshHit hit, 2f, NavMesh.AllAreas)) position = hit.position;
            Debug.LogWarning($"SlotItemDropper: '{item.displayName}'을(를) 놓을 빈자리를 찾지 못해 플레이어 발밑에 놓습니다.");
        }
        return SpawnDroppedPedestal(item, position, parent, true);
    }

    // 버린 아이템을 올린 좌대를 그 자리에 만듭니다. appearEffect = true면 일반방 보물상자가 생길 때와 같이 "투명 -> 흰색으로
    // 빛남 -> 원래 색"으로 나타납니다(AppearFromWhite - 55번을 잃어 넘친 장신구가 떨어질 때 사용, 형빈 확정).
    public static HoldingPedestal SpawnDroppedPedestal(ItemDefinition item, Vector3 position, Transform parent,
                                                       bool appearEffect = false)
    {
        if (item == null) return null;
        var settings = SlotDropSettings.Get();

        GameObject go;
        if (settings.droppedPedestalPrefab != null)
        {
            // 프리팹 에셋에 저장된 회전값을 그대로 씁니다(일반방 보물상자 스폰과 같은 이유 - 원하는 방향은 프리팹에서 조절).
            go = Object.Instantiate(settings.droppedPedestalPrefab, position, settings.droppedPedestalPrefab.transform.rotation, parent);
            StripChestComponents(go);

            if (settings.makeCollidersTrigger)
            {
                foreach (var col in go.GetComponentsInChildren<Collider>(true)) col.isTrigger = true;
            }

            // 아이템 아이콘을 올리기 전에 시작합니다(아이콘은 보물상자와 같이 이 연출 대상이 아님).
            if (appearEffect) AppearFromWhite.Begin(go);
        }
        else
        {
            go = new GameObject("DroppedItemPedestal");
            go.transform.SetParent(parent, false);
            go.transform.position = position;
        }

        var pedestal = go.GetComponent<HoldingPedestal>();
        if (pedestal == null) pedestal = go.AddComponent<HoldingPedestal>();
        if (go.GetComponent<DroppedItemPedestal>() == null) go.AddComponent<DroppedItemPedestal>();

        pedestal.TryPlace(item);
        return pedestal;
    }

    // 프리팹에 보물상자 컴포넌트가 붙어 있으면 떼어냅니다 - 보물상자로 취급되면 9번 복제 등의 대상이 됩니다(형빈 확정).
    private static void StripChestComponents(GameObject go)
    {
        int removed = 0;
        foreach (var c in go.GetComponentsInChildren<TreasureChest>(true)) { Object.DestroyImmediate(c); removed++; }
        foreach (var c in go.GetComponentsInChildren<ItemPedestal>(true)) { Object.DestroyImmediate(c); removed++; }
        foreach (var c in go.GetComponentsInChildren<PresetBasicItemChest>(true)) { Object.DestroyImmediate(c); removed++; }
        if (removed > 0)
        {
            Debug.LogWarning($"SlotItemDropper: 버린 아이템 좌대 프리팹에 보물상자 컴포넌트가 {removed}개 있어 떼어냈습니다. " +
                              "SlotDropSettings의 Dropped Pedestal Prefab에는 보물상자 컴포넌트가 없는 프리팹을 넣는 것을 권장합니다.");
        }
    }
}
