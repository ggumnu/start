using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// n.5 레벨(선택방 레벨) 전용 생성기입니다. LevelGraphGenerator처럼 복잡한 트리 생성이
// 필요 없고, 시작방 + 서쪽 슬롯(확률 판정) + 동쪽 슬롯(확률 판정)만 배치합니다.
//
// n레벨을 클리어하면 n.5 레벨 자체는 무조건 생성됩니다. 다만 그 안의 서쪽/동쪽 방이
// 등장할지는 플레이어가 그 시점에 갖고 있는 확률 스탯(PlayerStats.GematriaChance /
// ChaleChance)에 따라 각각 독립적으로 결정됩니다. 확률 굴림에 실패한 슬롯은 칸 자체가
// 존재하지 않는 것으로 취급되어, 그 방향은 앵커가 없으므로 LevelBuilder가 자동으로
// 막힌 벽을 세웁니다 (별도 처리 불필요, 기존 BuildWallSegment 로직 그대로 재사용됨).
//
// 이 생성기는 순수 데이터 로직이라 PlayerStats(MonoBehaviour)에 직접 의존하지 않습니다.
// 확률 값은 호출부(LevelBuilder / 이후 GameFlowManager)가 PlayerStats에서 읽어 전달합니다.
//
// [확장 방법]
// - 같은 슬롯에 다른 후보를 추가하려면: ChoiceLevelConfig 에셋의 west/eastVariants 리스트에
//   RoomVariant를 추가하면 끝입니다 (가중치 기반으로 자동 처리).
// - 위/아래 슬롯을 추가하려면: 이 클래스에 north/southVariants + north/southChance 파라미터를
//   같은 패턴(TryAddSlot 호출 한 줄)으로 추가하면 됩니다.
public class ChoiceLevelGenerator
{
    private readonly ChoiceLevelConfig config;
    private readonly System.Random rng;

    public ChoiceLevelGenerator(ChoiceLevelConfig config)
    {
        this.config = config;
        rng = new System.Random();
    }

    // westChance/eastChance는 호출부에서 PlayerStats.GematriaChance / ChaleChance를 전달합니다.
    public List<RoomInstance> Generate(float westChance, float eastChance)
    {
        var rooms = new List<RoomInstance>();
        int nextId = 0;

        var start = new RoomInstance { id = nextId++, category = RoomCategory.Start };
        start.occupiedCells.Add(Vector2Int.zero);
        rooms.Add(start);

        TryAddSlot(rooms, ref nextId, start, westChance, Direction.West,
            config != null ? config.westVariants : null);
        TryAddSlot(rooms, ref nextId, start, eastChance, Direction.East,
            config != null ? config.eastVariants : null);

        return rooms;
    }

    // 확률 판정에 성공한 경우에만 슬롯 방을 만들고 시작방과 연결합니다.
    // 실패하면 아무것도 하지 않습니다 = 그 방향은 칸이 아예 없는 상태로 남습니다.
    private void TryAddSlot(
        List<RoomInstance> rooms,
        ref int nextId,
        RoomInstance start,
        float chance,
        Direction dir,
        List<RoomVariant> variantPool)
    {
        if (!Roll(chance)) return;

        var slotRoom = new RoomInstance { id = nextId++, category = RoomCategory.Choice };
        slotRoom.occupiedCells.Add(Vector2Int.zero + DirectionUtil.ToOffset(dir));

        var picked = PickWeighted(variantPool);
        if (picked != null) slotRoom.variantId = picked.variantId;

        var link = new AnchorLink
        {
            roomA = start,
            roomB = slotRoom,
            cellA = Vector2Int.zero,
            cellB = slotRoom.occupiedCells[0],
            directionFromA = dir,
            isOpen = false
        };
        start.anchors.Add(link);
        slotRoom.anchors.Add(link);

        rooms.Add(slotRoom);
    }

    private bool Roll(float chance) => rng.NextDouble() < chance;

    private RoomVariant PickWeighted(List<RoomVariant> pool)
    {
        if (pool == null || pool.Count == 0) return null;
        if (pool.Count == 1) return pool[0];

        float totalWeight = pool.Sum(v => v.weight);
        if (totalWeight <= 0f) return pool[0];

        float roll = (float)(rng.NextDouble() * totalWeight);
        float cumulative = 0f;
        foreach (var v in pool)
        {
            cumulative += v.weight;
            if (roll <= cumulative) return v;
        }
        return pool[pool.Count - 1];
    }
}