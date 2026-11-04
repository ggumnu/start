using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 레벨 하나의 방 배치 그래프를 생성하는 순수 로직입니다.
// 이 스크립트는 씬에 아무것도 만들지 않고, RoomInstance 리스트(데이터)만 반환합니다.
// 실제 프리팹을 씬에 배치하는 작업은 이후 별도의 "빌더" 스크립트에서 담당합니다.
//
// [v2 - 카테고리별 최소/최대 개수 방식으로 전면 개편]
// 예전에는 config.totalRoomBudget(고정 전체 개수)을 먼저 정해두고, 그 예산 안에 소비방/
// 거래방/신비방 등의 등장 여부를 확률(boolean)로 끼워 넣는 방식이었습니다. 이제는 반대로
// 순서가 바뀝니다:
// 1. RollAllCounts()가 config.roomCountRules의 카테고리마다 "이번 레벨에 몇 개 등장하는지"를
//    먼저 굴립니다 (RoomGenerationConfig.cs의 RoomCountRule 참고).
// 2. 특수방(Special)을 뺀 나머지 개수들의 합이 그리드에 채울 칸 수가 되고, 막다른 방(리프)에만
//    배정되는 카테고리(Boss/Club/Store)의 개수 합이 "필요한 리프 개수"가 됩니다.
// 3. 그리드를 채우고 카테고리를 배정합니다(가지 뻗기 -> 리프에 보스/동아리/편의점 배정 ->
//    남은 칸은 자동으로 Normal).
// 4. [특수방 통합] 마지막으로 AttachSpecialRooms()가 특수방을 굴려진 개수만큼 "붙입니다".
//    특수방은 리프 자리를 미리 차지하지 않고, 다 만든 맵에서 config.specialRoomParentCategories
//    (기본: 시작방/일반방)에 해당하는 방의 빈 옆칸 중 다른 방과 맞닿지 않는 칸에 새로
//    생깁니다 - 그래서 특수방은 항상 막다른 방이고, 특수방 너머로 다른 방이 이어지는 일이 없습니다
//    (진입 조건이 있는 특수방 때문에 길이 막히지 않도록). 붙일 자리가 모자라면 그 시도는 실패로
//    보고 맵 전체를 다시 만듭니다.
//
// [생성 방식 - 그리드 채우기 자체는 기존과 동일]
// 1. 이번 레벨에 필요한 "리프(앵커 1개짜리 막다른 방)" 개수를 먼저 계산합니다.
// 2. 매 칸을 추가할 때 "새 가지 만들기"를 우선 시도하고, 그게 불가능하면(예: 시작방이
//    4방향 모두 꽉 찬 경우) "기존 가지를 한 칸 더 늘리기"로 자연스럽게 전환합니다.
//    가지를 늘리면 그 끝이 새로운 갈림길(접점)이 되므로, 다음 시도에서 그 자리를 통해
//    또 다른 가지를 뻗을 수 있게 됩니다.
// 3. 칸을 붙일 때는 항상 "의도한 부모 칸 외 다른 칸과 절대 맞닿지 않도록" 검증하므로,
//    서로 다른 가지가 우연히 붙어서 리프가 사라지는 문제도 함께 방지됩니다.
public class LevelGraphGenerator
{
    // 그리드를 채울 때 막다른 방(리프) 자리를 미리 확보해야 하는 카테고리입니다. Normal은 남는 칸이
    // 되고, Special(특수방)은 맵을 다 만든 뒤 AttachSpecialRooms()가 따로 붙이므로 여기 포함되지 않습니다.
    private static readonly RoomCategory[] LeafRequiringCategories =
    {
        RoomCategory.Boss, RoomCategory.Club, RoomCategory.Store
    };

    // config.specialRoomParentCategories에 들어 있어도 특수방의 부모로 쓰지 않는 카테고리입니다.
    // 보스방 옆에는 붙이지 않고, 특수방 너머로 또 특수방이 이어지지 않게 하며, 선택방은 이 생성기와 무관합니다.
    private static readonly RoomCategory[] NeverSpecialParentCategories =
    {
        RoomCategory.Boss, RoomCategory.Special, RoomCategory.Choice
    };

    private RoomGenerationConfig config;
    private Dictionary<Vector2Int, RoomInstance> grid;
    private List<RoomInstance> rooms;
    private System.Random rng;

    public LevelGraphGenerator(RoomGenerationConfig config)
    {
        this.config = config;
        rng = new System.Random();
    }

    public List<RoomInstance> Generate()
    {
        var rolledCounts = RollAllCounts();

        int requiredLeaves = LeafRequiringCategories.Sum(cat => GetCount(rolledCounts, cat));
        int specialCount = GetCount(rolledCounts, RoomCategory.Special);
        // 특수방은 그리드를 다 채운 뒤 따로 붙이므로, 그리드에 채울 칸 수에서는 뺍니다.
        int totalBudget = rolledCounts.Values.Sum() - specialCount;

        int bestLeafCountSeen = -1;
        int bestOccupiedCountSeen = -1;
        int specialAttachFailures = 0;

        for (int attempt = 0; attempt < config.maxGenerationAttempts; attempt++)
        {
            if (TryBuildGrid(requiredLeaves, totalBudget, out var leafCoords, out var occupiedCount))
            {
                if (leafCoords.Count > bestLeafCountSeen) bestLeafCountSeen = leafCoords.Count;

                if (leafCoords.Count >= requiredLeaves)
                {
                    var result = AssignCategories(leafCoords, rolledCounts);

                    // 특수방을 붙일 자리가 모자라면 이번 시도는 실패로 보고 맵 전체를 다시 만듭니다.
                    // (grid/rooms는 다음 TryBuildGrid가 처음부터 새로 만들므로 따로 되돌릴 필요가 없습니다.)
                    if (AttachSpecialRooms(specialCount))
                    {
                        return result;
                    }
                    specialAttachFailures++;
                }
            }
            else
            {
                if (occupiedCount > bestOccupiedCountSeen) bestOccupiedCountSeen = occupiedCount;
            }
        }

        Debug.LogError(
            $"레벨 생성 실패: 시도 횟수를 초과했습니다. " +
            $"필요 리프 개수={requiredLeaves}, 확보했던 최대 리프 개수={bestLeafCountSeen}, " +
            $"칸 채우기가 끝까지 성공한 적={(bestLeafCountSeen >= 0 ? "있음" : "없음")}, " +
            $"칸 채우기 실패 시 도달했던 최대 칸 수={bestOccupiedCountSeen}/{totalBudget}, " +
            $"특수방 {specialCount}개를 붙일 자리가 모자라 실패한 횟수={specialAttachFailures}. " +
            $"roomCountRules(특히 Normal의 min/max)를 늘리거나 boundingBoxLimit 값을 늘려보세요.");
        return null;
    }

    // ---------- 0단계: 카테고리별 개수 굴리기 ----------

    // 카테고리별로 규칙에 맞춰 이번 레벨의 실제 개수를 굴립니다. Start는 그리드 구조상
    // 항상 원점 1칸뿐이라, roomCountRules에 어떤 값이 들어있든 여기서 1로 덮어씁니다.
    private Dictionary<RoomCategory, int> RollAllCounts()
    {
        var result = new Dictionary<RoomCategory, int>();

        if (config.roomCountRules != null)
        {
            foreach (var rule in config.roomCountRules)
            {
                result[rule.category] = RollCount(rule);
            }
        }

        result[RoomCategory.Start] = 1;
        return result;
    }

    // minCount~maxCount 사이의 각 개수마다 매겨진 가중치(countWeights)로 하나를 뽑습니다 -
    // PickDistinctWeighted 등에서 쓰는 것과 동일한 총 가중치 방식입니다(그 개수가 나올 확률은
    // (그 개수의 가중치) / (전체 가중치 합)이며, 각 개수의 확률을 서로 완전히 독립적으로
    // 조절할 수 있습니다 - 한 단계라도 실패하면 그 뒤는 굴리지 않던 예전의 순차 누적 방식과는
    // 다릅니다). countWeights가 비어있거나 원소 개수가 (maxCount - minCount + 1)과 다르면
    // (아직 값을 안 채운 규칙 등) 안전하게 minCount로 고정합니다.
    private int RollCount(RoomCountRule rule)
    {
        int optionCount = rule.maxCount - rule.minCount + 1;
        if (optionCount <= 1) return rule.minCount;

        var weights = rule.countWeights;
        if (weights == null || weights.Count != optionCount) return rule.minCount;

        float totalWeight = 0f;
        for (int i = 0; i < optionCount; i++) totalWeight += Mathf.Max(0f, weights[i]);
        if (totalWeight <= 0f) return rule.minCount;

        float roll = (float)(rng.NextDouble() * totalWeight);
        float cumulative = 0f;
        for (int i = 0; i < optionCount; i++)
        {
            cumulative += Mathf.Max(0f, weights[i]);
            if (roll <= cumulative) return rule.minCount + i;
        }
        return rule.maxCount;
    }

    private static int GetCount(Dictionary<RoomCategory, int> rolledCounts, RoomCategory category)
    {
        return rolledCounts.TryGetValue(category, out int count) ? count : 0;
    }

    // ---------- 1단계: 가지 뻗기로 그리드 채우기 ----------
    private bool TryBuildGrid(int requiredLeaves, int budget, out List<Vector2Int> leafCoords, out int occupiedCount)
    {
        grid = new Dictionary<Vector2Int, RoomInstance>();
        var start = Vector2Int.zero;
        var occupied = new HashSet<Vector2Int> { start };
        var degree = new Dictionary<Vector2Int, int> { [start] = 0 };
        var depth = new Dictionary<Vector2Int, int> { [start] = 0 };

        // leaves: 앵커 1개짜리 막다른 칸 (start 제외). junctions: 새 가지가 뻗어나올 수 있는 칸 (start 포함).
        var leaves = new List<Vector2Int>();
        var junctions = new List<Vector2Int> { start };

        int effectiveMin = Mathf.Max(config.minBranches, requiredLeaves);
        int effectiveMax = Mathf.Max(config.maxBranches, effectiveMin);
        int targetBranches = Mathf.Min(rng.Next(effectiveMin, effectiveMax + 1), budget - 1);

        int guard = 0;
        while (occupied.Count < budget && guard++ < 4000)
        {
            bool placed = false;
            bool wantNewBranch = leaves.Count < targetBranches;

            // 1순위: 아직 리프가 부족하면, 접점(junction) 중 아무 곳에서나 새 가지를 뻗어본다.
            if (wantNewBranch)
            {
                foreach (var j in junctions.OrderBy(_ => rng.Next()))
                {
                    if (TryPlaceCell(j, occupied, degree, depth, leaves, junctions))
                    {
                        placed = true;
                        break;
                    }
                }
            }

            // 2순위: 새 가지를 못 뻗었거나(예: 접점이 전부 4방향 포화) 이미 목표 리프를 채웠다면,
            // 기존 리프를 한 칸 늘린다. 이러면 그 리프가 접점으로 바뀌어 다음 시도에서
            // 새로운 가지를 뻗을 자리가 다시 생긴다.
            if (!placed)
            {
                var candidates = leaves
                    .Where(l => !(depth.TryGetValue(l, out var d) && d >= config.maxBranchDepth))
                    .OrderBy(_ => rng.Next());

                foreach (var l in candidates)
                {
                    if (TryPlaceCell(l, occupied, degree, depth, leaves, junctions))
                    {
                        placed = true;
                        break;
                    }
                }
            }

            // 어느 쪽으로도 더 이상 놓을 곳이 없으면 이번 시도는 여기서 끝냅니다.
            if (!placed) break;
        }

        occupiedCount = occupied.Count;

        if (occupied.Count != budget)
        {
            leafCoords = null;
            return false; // 예산을 못 맞췄으면 이번 시도는 실패, 재시도
        }

        // RoomInstance 껍데기 생성 (카테고리는 아직 미배정 = Normal)
        rooms = new List<RoomInstance>();
        int id = 0;
        foreach (var coord in occupied)
        {
            var room = new RoomInstance { id = id++, category = RoomCategory.Normal };
            room.occupiedCells.Add(coord);
            grid[coord] = room;
            rooms.Add(room);
        }

        // 인접한 셀은 전부 무조건 연결합니다 (예외 없음).
        // 성장 과정에서 "의도한 부모 외 다른 칸과 맞닿지 않도록" 검증했기 때문에,
        // 여기서 만들어지는 연결은 성장 과정에서 의도한 트리 구조와 정확히 일치합니다.
        foreach (var coord in occupied)
        {
            foreach (var dir in DirectionUtil.All)
            {
                var neighbor = coord + DirectionUtil.ToOffset(dir);
                if (!occupied.Contains(neighbor)) continue;
                if (coord.x > neighbor.x || (coord.x == neighbor.x && coord.y > neighbor.y)) continue; // 중복 방지

                var link = new AnchorLink
                {
                    roomA = grid[coord],
                    roomB = grid[neighbor],
                    cellA = coord,
                    cellB = neighbor,
                    directionFromA = dir,
                    isOpen = false
                };
                grid[coord].anchors.Add(link);
                grid[neighbor].anchors.Add(link);
            }
        }

        leafCoords = occupied.Where(c => grid[c].anchors.Count == 1 && c != start).ToList();
        return true;
    }

    // parent에서 사방으로 랜덤 순서로 시도해서, "다른 칸과 접하지 않는" 유효한 칸을 하나 붙입니다.
    private bool TryPlaceCell(
        Vector2Int parent,
        HashSet<Vector2Int> occupied,
        Dictionary<Vector2Int, int> degree,
        Dictionary<Vector2Int, int> depth,
        List<Vector2Int> leaves,
        List<Vector2Int> junctions)
    {
        var dirs = DirectionUtil.All.OrderBy(_ => rng.Next()).ToArray();
        foreach (var dir in dirs)
        {
            var next = parent + DirectionUtil.ToOffset(dir);
            if (occupied.Contains(next)) continue;
            if (Mathf.Abs(next.x) > config.boundingBoxLimit.x || Mathf.Abs(next.y) > config.boundingBoxLimit.y) continue;
            if (HasExtraNeighbor(next, parent, occupied)) continue;

            occupied.Add(next);

            int parentDegreeBefore = degree.TryGetValue(parent, out var pd) ? pd : 0;
            degree[parent] = parentDegreeBefore + 1;
            depth[next] = (depth.TryGetValue(parent, out var pdep) ? pdep : 0) + 1;
            degree[next] = 1;

            // parent가 리프였다가(1) 이번에 갈래가 하나 더 생겨서 접점(2 이상)이 되면 목록을 옮깁니다.
            if (parentDegreeBefore == 1 && leaves.Contains(parent))
            {
                leaves.Remove(parent);
                junctions.Add(parent);
            }

            leaves.Add(next);
            return true;
        }
        return false;
    }

    // candidate가 parent 외에 이미 점유된 다른 칸과 맞닿아 있는지 확인합니다.
    // (서로 다른 가지가 우연히 붙어서 의도치 않은 연결이 생기는 것을 방지)
    private bool HasExtraNeighbor(Vector2Int candidate, Vector2Int parent, HashSet<Vector2Int> occupied)
    {
        foreach (var dir in DirectionUtil.All)
        {
            var neighbor = candidate + DirectionUtil.ToOffset(dir);
            if (neighbor == parent) continue;
            if (occupied.Contains(neighbor)) return true;
        }
        return false;
    }

    // ---------- 2단계: 카테고리 배정 ----------
    private List<RoomInstance> AssignCategories(List<Vector2Int> leafCoords, Dictionary<RoomCategory, int> rolledCounts)
    {
        grid[Vector2Int.zero].category = RoomCategory.Start;
        var startVariant = PickDistinctWeighted(config.startVariants, 1);
        if (startVariant.Count > 0)
        {
            grid[Vector2Int.zero].variantId = startVariant[0].variantId;
        }

        var remainingLeaves = new List<Vector2Int>(leafCoords);

        // 보스: 가장 먼 리프부터 굴려진 개수만큼. 다른 특수방보다 먼저 배정해서
        // "가장 먼 곳"을 항상 확실히 차지하도록 합니다(기존과 동일한 순서).
        int bossCount = GetCount(rolledCounts, RoomCategory.Boss);
        if (bossCount > 0)
        {
            var distances = BfsDistances(Vector2Int.zero);
            var bossCoords = remainingLeaves.OrderByDescending(c => distances[c]).Take(bossCount).ToList();
            var bossVariantsPicked = PickDistinctWeighted(config.bossVariants, bossCount);

            for (int i = 0; i < bossCoords.Count; i++)
            {
                grid[bossCoords[i]].category = RoomCategory.Boss;
                if (bossVariantsPicked.Count > 0)
                {
                    grid[bossCoords[i]].variantId = bossVariantsPicked[i % bossVariantsPicked.Count].variantId;
                }
                remainingLeaves.Remove(bossCoords[i]);
            }
        }

        AssignLeafCategory(RoomCategory.Club, config.clubVariants, rolledCounts, remainingLeaves);
        AssignLeafCategory(RoomCategory.Store, config.storeVariants, rolledCounts, remainingLeaves);

        // 일반방(Normal)은 별도 배정 로직이 필요 없습니다 - 그리드 칸 수가 이미 Generate()에서
        // (특수방을 뺀) 카테고리 개수의 합으로 계산되었기 때문에, 위에서 아무 카테고리로도 배정되지
        // 않고 남은 칸은 정확히 Normal 굴림 개수만큼 남아 자동으로 Normal로 유지됩니다.
        // 특수방은 이 다음에 AttachSpecialRooms()가 따로 붙입니다(Generate() 참고).

        return rooms;
    }

    // ---------- 3단계: 특수방 붙이기 ----------

    // 카테고리 배정이 끝난 맵에 특수방을 count개 붙입니다. 전부 붙이면 true, 붙일 자리가 모자라면 false
    // (Generate()가 맵 전체를 다시 만듭니다). rooms 리스트는 AssignCategories()가 돌려준 것과 같은
    // 인스턴스라, 여기서 추가한 방이 그대로 결과에 포함됩니다.
    //
    // [붙일 수 있는 칸 - LevelBuilder.FindExtraRoomSlots(16번 아이템)와 같은 규칙]
    // - 부모 방: config.specialRoomParentCategories에 있는 종류(기본: 시작방/일반방). 단 보스방/
    //   특수방/선택방은 목록에 있어도 제외합니다(NeverSpecialParentCategories).
    // - 칸: 부모 방 바로 옆의 빈 칸 중, boundingBoxLimit 안이고 부모 방 외 다른 방과 맞닿지 않는 칸
    //   (= 새 방이 반드시 막다른 방이 되는 칸). 그리드 채우기(TryPlaceCell)와 같은 검사입니다.
    // 하나를 붙일 때마다 후보를 다시 계산하므로, 두 특수방이 서로 맞닿아 생기는 일도 없습니다.
    private bool AttachSpecialRooms(int count)
    {
        if (count <= 0) return true;

        var occupied = new HashSet<Vector2Int>(grid.Keys);
        var pickedLayouts = PickDistinctWeightedLayouts(config.specialLayouts, count);
        int nextId = rooms.Count == 0 ? 0 : rooms.Max(r => r.id) + 1;

        for (int i = 0; i < count; i++)
        {
            var slots = FindSpecialRoomSlots(occupied);
            if (slots.Count == 0) return false;

            // 칸 하나하나가 아니라 "부모 방"을 먼저 고르게 뽑습니다. 칸을 그대로 뽑으면 빈 옆칸이 많은
            // 방에 특수방이 몰리기 때문입니다.
            var parentCoords = slots.Select(s => s.parentCoord).Distinct().ToList();
            var chosenParent = parentCoords[rng.Next(parentCoords.Count)];
            var parentSlots = slots.Where(s => s.parentCoord == chosenParent).ToList();
            var (parentCoord, dir) = parentSlots[rng.Next(parentSlots.Count)];
            var cell = parentCoord + DirectionUtil.ToOffset(dir);
            var parent = grid[parentCoord];

            var special = new RoomInstance { id = nextId++, category = RoomCategory.Special };
            special.occupiedCells.Add(cell);
            if (pickedLayouts.Count > 0)
            {
                // 레이아웃 수보다 특수방이 많으면 순환해서 다시 씁니다(다른 카테고리의 variant 배정과 같은 방식).
                var layout = pickedLayouts[i % pickedLayouts.Count];
                special.specialLayout = layout;
                special.variantId = string.IsNullOrEmpty(layout.layoutId) ? null : layout.layoutId;
                // 진입 조건은 방마다 따로 뽑습니다(레이아웃의 Entry Condition Pool, 비어 있으면 고정 조건).
                special.specialEntryCondition = layout.PickEntryCondition(rng);
            }

            var link = new AnchorLink
            {
                roomA = parent,
                roomB = special,
                cellA = parentCoord,
                cellB = cell,
                directionFromA = dir,
                isOpen = false
            };
            parent.anchors.Add(link);
            special.anchors.Add(link);

            grid[cell] = special;
            rooms.Add(special);
            occupied.Add(cell);
        }
        return true;
    }

    // 특수방 레이아웃 목록에서 가중치 기반으로 서로 다른 레이아웃을 최대 count개 뽑습니다(PickDistinctWeighted와
    // 같은 방식). 레이아웃 칸이 비었거나 가중치가 0 이하인 줄은 건너뜁니다. 쓸 수 있는 줄이 하나도 없으면 빈 목록을
    // 돌려주고, 그러면 특수방은 레이아웃 없이 생깁니다(경고 로그는 한 번만).
    private List<SpecialRoomLayout> PickDistinctWeightedLayouts(List<SpecialLayoutEntry> entries, int count)
    {
        var picked = new List<SpecialRoomLayout>();
        var remaining = entries == null
            ? new List<SpecialLayoutEntry>()
            : entries.Where(e => e != null && e.layout != null && e.weight > 0f).ToList();

        if (remaining.Count == 0)
        {
            if (count > 0)
            {
                Debug.LogWarning("LevelGraphGenerator: Level Config의 Special Layouts에 쓸 수 있는 레이아웃이 없어 " +
                                 "특수방이 레이아웃 없이(내용물/진입 조건 없음) 생성됩니다.");
            }
            return picked;
        }

        for (int i = 0; i < count && remaining.Count > 0; i++)
        {
            float totalWeight = remaining.Sum(e => e.weight);
            float roll = (float)(rng.NextDouble() * totalWeight);
            float cumulative = 0f;
            SpecialLayoutEntry chosen = remaining[remaining.Count - 1];
            foreach (var e in remaining)
            {
                cumulative += e.weight;
                if (roll <= cumulative) { chosen = e; break; }
            }
            picked.Add(chosen.layout);

            // 같은 레이아웃 에셋이 목록에 두 줄 이상 들어 있어도 한 레벨에서 겹치지 않도록 같은 에셋을 전부 뺍니다.
            remaining.RemoveAll(e => e.layout == chosen.layout);
        }
        return picked;
    }

    // 지금 맵에서 특수방을 붙일 수 있는 (부모 칸, 방향) 목록입니다. 규칙은 AttachSpecialRooms 주석 참고.
    private List<(Vector2Int parentCoord, Direction dir)> FindSpecialRoomSlots(HashSet<Vector2Int> occupied)
    {
        var slots = new List<(Vector2Int, Direction)>();
        var allowedParents = config.specialRoomParentCategories;
        if (allowedParents == null || allowedParents.Count == 0) return slots;

        foreach (var room in rooms)
        {
            if (room == null || room.occupiedCells.Count == 0) continue;
            if (!allowedParents.Contains(room.category)) continue;
            if (NeverSpecialParentCategories.Contains(room.category)) continue;

            var parentCoord = room.occupiedCells[0];
            foreach (var dir in DirectionUtil.All)
            {
                var next = parentCoord + DirectionUtil.ToOffset(dir);
                if (occupied.Contains(next)) continue;
                if (Mathf.Abs(next.x) > config.boundingBoxLimit.x || Mathf.Abs(next.y) > config.boundingBoxLimit.y) continue;
                if (HasExtraNeighbor(next, parentCoord, occupied)) continue;

                slots.Add((parentCoord, dir));
            }
        }
        return slots;
    }

    // Boss를 제외한, "막다른 방(리프)에만 배정되는" 카테고리들의 공통 처리입니다.
    // rolledCounts에서 이 카테고리의 개수를 읽어 remainingLeaves 중 무작위로 그만큼 뽑아
    // 배정하고, variantPool에서 후보를 순환 배정합니다(풀이 필요 개수보다 적어도 안전합니다).
    private void AssignLeafCategory(
        RoomCategory category,
        List<RoomVariant> variantPool,
        Dictionary<RoomCategory, int> rolledCounts,
        List<Vector2Int> remainingLeaves)
    {
        int count = GetCount(rolledCounts, category);
        if (count <= 0) return;

        var pickedVariants = PickDistinctWeighted(variantPool, count);

        for (int i = 0; i < count && remainingLeaves.Count > 0; i++)
        {
            var coord = PickRandom(remainingLeaves);
            grid[coord].category = category;

            if (pickedVariants.Count > 0)
            {
                grid[coord].variantId = pickedVariants[i % pickedVariants.Count].variantId;
            }

            remainingLeaves.Remove(coord);
        }
    }

    // ---------- 유틸 ----------
    private Dictionary<Vector2Int, int> BfsDistances(Vector2Int from)
    {
        var dist = new Dictionary<Vector2Int, int> { [from] = 0 };
        var queue = new Queue<Vector2Int>();
        queue.Enqueue(from);
        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();
            foreach (var link in grid[cur].anchors)
            {
                var neighborCoord = link.cellA == cur ? link.cellB : link.cellA;
                if (dist.ContainsKey(neighborCoord)) continue;
                dist[neighborCoord] = dist[cur] + 1;
                queue.Enqueue(neighborCoord);
            }
        }
        return dist;
    }

    private Vector2Int PickRandom(List<Vector2Int> list) => list[rng.Next(list.Count)];

    private List<RoomVariant> PickDistinctWeighted(List<RoomVariant> pool, int count)
    {
        // 호출부에서 null 체크 없이 바로 호출하는 경우가 있어(예: AssignLeafCategory),
        // 여기서도 방어합니다 - 이 메서드 자체가 항상 null-safe해야 합니다.
        if (pool == null || pool.Count == 0) return new List<RoomVariant>();

        var remaining = new List<RoomVariant>(pool);
        var picked = new List<RoomVariant>();
        for (int i = 0; i < count && remaining.Count > 0; i++)
        {
            float totalWeight = remaining.Sum(v => v.weight);
            float roll = (float)(rng.NextDouble() * totalWeight);
            float cumulative = 0f;
            RoomVariant chosen = remaining[0];
            foreach (var v in remaining)
            {
                cumulative += v.weight;
                if (roll <= cumulative) { chosen = v; break; }
            }
            picked.Add(chosen);
            remaining.Remove(chosen);
        }
        return picked;
    }
}