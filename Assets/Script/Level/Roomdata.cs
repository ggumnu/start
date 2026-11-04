using System.Collections.Generic;
using UnityEngine;

// 방의 "기능적 역할" - 생성 규칙이 이 값에 따라 갈립니다.
//
// [번호를 직접 적어둔 이유 - 절대 바꾸지 마세요]
// Unity는 enum 값을 이름이 아니라 "번호"로 에셋/씬/프리팹에 저장합니다. 예전의 소비방(4)/거래방(5)/
// 신비방(6)을 특수방 하나로 합치면서, 소비방 자리(4)를 특수방으로 이름만 바꾸고 5/6은 비워두었습니다.
// 번호를 당겨 쓰면 일반방(7)/선택방(8)이 5/6으로 밀려, 아이템 등장 위치(Normal = 7)나 Level Config,
// 선택방 설정(Choice = 8)에 저장된 값이 전부 다른 방을 가리키게 됩니다. 새 방 종류를 추가할 때는
// 비어 있는 5/6을 쓰거나 9 이상을 쓰세요.
public enum RoomCategory
{
    Start = 0,      // 시작방
    Boss = 1,       // 보스방
    Club = 2,       // 동아리방
    Store = 3,      // 편의점
    Special = 4,    // 특수방 (수류탄 폐허/해킹툴 폐허/뒷골목/은행/블랙마켓/보건실/학원교류회/데카그라마톤 자판기/슬럼피아 등 - 전부 레이아웃)
    // 5, 6은 비어 있음 (예전 거래방/신비방 - 특수방으로 통합됨)
    Normal = 7,     // 일반방
    Choice = 8      // 선택방 (n.5 레벨 전용: 게마트리아/샬레 등)
}

public enum Direction { North, East, South, West }

public static class DirectionUtil
{
    public static readonly Direction[] All = { Direction.North, Direction.East, Direction.South, Direction.West };

    public static Vector2Int ToOffset(Direction d)
    {
        switch (d)
        {
            case Direction.North: return new Vector2Int(0, 1);
            case Direction.South: return new Vector2Int(0, -1);
            case Direction.East: return new Vector2Int(1, 0);
            case Direction.West: return new Vector2Int(-1, 0);
            default: return Vector2Int.zero;
        }
    }

    public static Direction Opposite(Direction d)
    {
        switch (d)
        {
            case Direction.North: return Direction.South;
            case Direction.South: return Direction.North;
            case Direction.East: return Direction.West;
            case Direction.West: return Direction.East;
            default: return d;
        }
    }
}

// 실제 어떤 프리팹/컨셉을 쓸지 - Category 안에서 더 세부적으로 나뉩니다.
// 문자열 id로 관리 (나중에 얼마든지 추가 가능하도록)
[System.Serializable]
public class RoomVariant
{
    [Tooltip("예: grenade_ruins, black_market 등 이 방을 구분하는 고유 이름")]
    public string variantId;

    public RoomCategory category;

    [Tooltip("같은 카테고리 내에서 뽑힐 가중치. 값이 클수록 더 자주 등장합니다.")]
    public float weight = 1f;

    [Tooltip("이 방이 차지하는 셀들의 상대 좌표. 1x1 방이면 (0,0) 하나만 넣으면 됩니다.")]
    public Vector2Int[] footprint = new Vector2Int[] { Vector2Int.zero };
}

// 프리팹 후보 하나 + 가중치. 벽/바닥 등 여러 풀에서 공통으로 사용하는 범용 구조입니다.
[System.Serializable]
public class WeightedPrefab
{
    public GameObject prefab;
    [Tooltip("같은 풀 안에서 뽑힐 가중치. 값이 클수록 더 자주 등장합니다.")]
    public float weight = 1f;
}

// 방향 하나에 대한 "닫힌 벽 후보 목록"과 "열린 벽(골목) 후보 목록"을 담습니다.
// LevelBuilder.wallPools 리스트에 방향마다 하나씩 등록해서 사용합니다.
[System.Serializable]
public class DirectionalWallPool
{
    public Direction direction;

    [Tooltip("이 방향이 다른 방과 연결되지 않은(앵커 없는) 경우 사용할 완전히 막힌 벽 후보들입니다.")]
    public List<WeightedPrefab> closedWalls = new List<WeightedPrefab>();

    [Tooltip("이 방향이 다른 방과 연결된 경우 사용할 골목형 벽 후보들입니다. " +
             "각 프리팹 루트에는 WallSidePrefab 컴포넌트로 바리케이드가 연결되어 있어야 합니다.")]
    public List<WeightedPrefab> openWalls = new List<WeightedPrefab>();
}

// 카테고리(또는 variantId) 하나에 대한 특수방 전용 벽/바닥 세트입니다.
// LevelBuilder.roomWallSets 리스트에 등록해서 사용합니다.
//
// [매칭 우선순위]
// 1. variantId가 채워져 있으면, 그 variantId를 가진 방(편의점/특수방/선택방 등
//    구체적인 방)이 category와 무관하게 최우선으로 매칭됩니다 (예: "grenade_ruins" 전용 세트).
// 2. variantId가 비어있으면, category만으로 매칭됩니다 (예: Boss 카테고리 전체에 적용).
// 3. 매칭되는 세트가 없으면 LevelBuilder의 기본 wallPools/floorPool(일반방 폴백)을 사용합니다.
[System.Serializable]
public class RoomWallSet
{
    [Tooltip("variantId가 비어있을 때만 사용되는 매칭 기준입니다.")]
    public RoomCategory category;

    [Tooltip("채워져 있으면 category보다 우선 매칭됩니다. 예: 'grenade_ruins', 'black_market' 등 " +
             "RoomVariant.variantId와 동일한 문자열을 넣어주세요. 카테고리 전체에 적용하려면 비워두세요.")]
    public string variantId;

    public List<WeightedPrefab> floorPool = new List<WeightedPrefab>();
    public List<DirectionalWallPool> wallPools = new List<DirectionalWallPool>();
}

// "일반방이 특수방과 맞닿아 있을 때, 그 방향만 특수방을 향한 파사드로 보여주기" 위한
// 세트입니다. LevelBuilder.neighborFacingWallSets에 등록해서 사용합니다.
//
// [적용 조건]
// 이 세트는 "지금 짓는 방 자체가 이미 자기만의 특수방 정체성(RoomWallSet)을 갖고
// 있지 않은 경우"에만 적용됩니다 (즉 사실상 일반방일 때). 짓는 방 자체가 이미 특수방으로
// 매칭되면, 그 방향 벽은 이 세트가 아니라 그 방 자신의 RoomWallSet(또는 기본 폴백)을
// 그대로 씁니다.
[System.Serializable]
public class NeighborFacingWallSet
{
    [Tooltip("이웃 방의 category만으로 매칭할 때 사용합니다. neighborVariantId가 비어있을 때만 적용됩니다.")]
    public RoomCategory neighborCategory;

    [Tooltip("채워져 있으면 neighborCategory보다 우선 매칭됩니다. 이웃 방의 RoomInstance.variantId와 " +
             "동일한 문자열을 넣어주세요 (예: 'grenade_ruins').")]
    public string neighborVariantId;

    [Tooltip("이웃 방향(앵커로 연결된, 즉 열린 방향)에 적용할 벽 후보들입니다.")]
    public List<DirectionalWallPool> wallPools = new List<DirectionalWallPool>();
}

// 그리드 상의 셀 하나
public class CellNode
{
    public Vector2Int coord;
    public RoomInstance occupyingRoom; // 비어있으면 null
}

// 실제 배치된 방 하나 (여러 셀을 차지할 수 있음)
//
// [v1.16 변경 - 미니맵을 위한 visited 플래그 추가]
// 화면 우측 상단 미니맵(MinimapDisplay)이 "플레이어가 지금 있는 방 / 지나간 적이 있는 방 /
// 아직 못 가본 방" 세 가지 아이콘을 구분해서 그리려면, 각 방이 "한 번이라도 들어와 본
// 적이 있는지"를 알아야 합니다. isCleared는 "전투를 이겼는지"라 동아리방/상점처럼 전투가
// 없는 방에는 절대 true가 되지 않으므로 재사용할 수 없어서, 별도 플래그로 추가했습니다.
// RoomController.EnterRoom()이 호출될 때 true로 설정됩니다 - 그 외 로직은 전혀 건드리지
// 않았습니다.
public class RoomInstance
{
    public int id;
    public RoomCategory category;
    public string variantId;
    public List<Vector2Int> occupiedCells = new List<Vector2Int>();

    // 특수방(Special)일 때만 채워지는, 이 방에 배정된 레이아웃입니다(진입 조건/내용물 설정). 다른 방 종류는
    // 항상 null입니다. LevelGraphGenerator.AttachSpecialRooms가 채우며, 이때 variantId도 레이아웃 ID로 맞춰집니다.
    public SpecialRoomLayout specialLayout;

    // 특수방일 때 이 방에 뽑힌 진입 조건입니다(레이아웃의 Entry Condition Pool에서 랜덤, 비어 있으면 레이아웃의 고정
    // 조건). 같은 레이아웃이라도 방마다 다를 수 있으므로 잠금 바리케이드/체력 피해는 레이아웃이 아니라 이 값을 봅니다.
    // 특수방이 아니거나 레이아웃이 없으면 Free입니다. LevelGraphGenerator.AttachSpecialRooms가 specialLayout과 함께 채웁니다.
    public SpecialRoomEntryCondition specialEntryCondition = SpecialRoomEntryCondition.Free;

    public bool isCleared = false;
    public bool visited = false;

    // 이 방에서 다른 방으로 이어지는 앵커(연결) 목록
    public List<AnchorLink> anchors = new List<AnchorLink>();
}

// 두 방 사이의 연결 하나 (앵커 = 연결 지점)
public class AnchorLink
{
    public RoomInstance roomA;
    public RoomInstance roomB;
    public Vector2Int cellA;   // roomA 쪽에서 연결되는 정확한 셀
    public Vector2Int cellB;   // roomB 쪽에서 연결되는 정확한 셀
    public Direction directionFromA;
    public bool isOpen = false; // 클리어 전엔 닫혀 있음
}