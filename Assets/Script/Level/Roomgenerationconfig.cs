using System.Collections.Generic;
using UnityEngine;

// 레벨 하나의 생성 규칙(방 개수/가지/특수방/variant + 일반 적 마릿수·종류)을 담는 설정 에셋입니다.
// 레벨마다 하나씩(Level1Config, Level2Config...) 만들어 GameFlowManager.combatLevelConfigs에 순서대로 등록합니다.
// 맵 생성은 LevelGraphGenerator, 일반 적 설정은 RealEnemySpawner, 배경 재질은 GameFlowManager(LevelBackground)가 읽습니다.
[CreateAssetMenu(fileName = "LevelConfig", menuName = "MobArchive/Level Generation Config")]
public class RoomGenerationConfig : ScriptableObject
{
    [Header("레벨 식별용 (표시/로그 전용)")]
    public int levelNumber = 1;

    [Header("가지 뻗기 제한 (맵이 너무 늘어지지 않도록 조절)")]
    public int minBranches = 3;
    public int maxBranches = 5;
    public int maxBranchDepth = 4;
    public Vector2Int boundingBoxLimit = new Vector2Int(6, 6);

    [Header("카테고리별 등장 개수 규칙 (최소/최대 + 개수별 가중치)")]
    [Tooltip("Count Weights 칸 수 = 최대 - 최소 + 1(최소부터 순서대로), 비거나 안 맞으면 최소 개수로 고정. Boss/Club/Store는 막다른 방에만 들어가므로 늘리면 Normal도 넉넉히.")]
    public List<RoomCountRule> roomCountRules = new List<RoomCountRule>
    {
        new RoomCountRule { category = RoomCategory.Start,       minCount = 1, maxCount = 1 },
        new RoomCountRule { category = RoomCategory.Boss,        minCount = 1, maxCount = 1 },
        new RoomCountRule { category = RoomCategory.Club,        minCount = 1, maxCount = 1 },
        new RoomCountRule { category = RoomCategory.Store,       minCount = 1, maxCount = 1 },
        new RoomCountRule { category = RoomCategory.Special,     minCount = 1, maxCount = 1 },
        new RoomCountRule { category = RoomCategory.Normal,      minCount = 3, maxCount = 5, countWeights = new List<float> { 0.5f, 0.25f, 0.25f } },
    };

    [Header("종류별 프리팹 풀 (가중치 균등 초기값 1)")]
    public List<RoomVariant> storeVariants;

    [Header("특수방 레이아웃 (SpecialRoomLayout 에셋 + 가중치)")]
    [Tooltip("특수방마다 가중치로 레이아웃 하나를 뽑음(같은 레벨에선 되도록 안 겹침). 비우면 내용물/진입 조건 없는 특수방.")]
    public List<SpecialLayoutEntry> specialLayouts = new List<SpecialLayoutEntry>();

    [Tooltip("특수방이 붙을 수 있는 방 종류(기본: 시작방/일반방). 보스방/선택방/특수방은 넣어도 무시됨.")]
    public List<RoomCategory> specialRoomParentCategories = new List<RoomCategory>
    {
        RoomCategory.Start, RoomCategory.Normal
    };

    [Header("시작방/보스방/동아리방 variant 풀 (비우면 카테고리로만 구분)")]
    [Tooltip("레벨마다 가중치로 하나를 뽑아 variantId로 배정. 벽 세트(LevelBuilder Room Wall Sets)와 같은 variantId로 매칭됨.")]
    public List<RoomVariant> startVariants;
    [Tooltip("레벨마다 가중치로 하나를 뽑아 variantId로 배정. RealEnemySpawner Boss Pool의 같은 variantId 항목이 이 보스방 보스가 됨.")]
    public List<RoomVariant> bossVariants;
    [Tooltip("레벨마다 가중치로 하나를 뽑아 variantId로 배정. 벽 세트(LevelBuilder Room Wall Sets)와 같은 variantId로 매칭됨.")]
    public List<RoomVariant> clubVariants;

    [Header("일반 적 (일반방) - 이 레벨 전용")]
    [Tooltip("켜면 이 레벨 일반방의 적 마릿수를 아래 값으로 정함. 끄면 EnemySpawner의 Min/Max Count를 씀.")]
    public bool overrideNormalEnemyCount = false;
    [Tooltip("방 하나의 최소 마릿수.")]
    [Min(0)] public int normalEnemyMinCount = 3;
    [Tooltip("방 하나의 최대 마릿수.")]
    [Min(0)] public int normalEnemyMaxCount = 4;
    [Tooltip("최소~최대 각 마릿수의 가중치. 칸 수 = 최대 - 최소 + 1(최소부터 순서대로). 비우면 균등, 칸 수가 안 맞으면 경고 후 균등.")]
    public List<float> normalEnemyCountWeights = new List<float>();
    [Tooltip("이 레벨 일반 적 후보. 비우면 EnemySpawner의 Enemy Pool을 씀. 등장 비율은 각 적 에셋의 Weight.")]
    public List<EnemyDefinition> normalEnemyPool = new List<EnemyDefinition>();

    [Header("배경 (비워두면 씬의 원래 배경 - 1레벨 파란 하늘)")]
    [Tooltip("이 레벨의 화면 배경 재질(ScreenBackground 셰이더, BG_ScreenSky를 복사해 색만 바꾼 것). 재질의 초상화 칸 색으로 HUD 초상화 하늘도 바뀝니다.")]
    public Material backgroundMaterial;

    [Header("조명 (비워두면 원래 조명 - 1레벨 밝은 낮)")]
    [Tooltip("이 레벨의 조명 분위기(LevelLightingProfile - 해 색온도, 그늘 색, 밝기, 밤 여부). 2레벨 노을/3레벨 밤 등. n.5 선택방 레벨은 항상 원래 조명입니다.")]
    public LevelLightingProfile lightingProfile;

    [Header("확장 방 크기 허용 (배치 기능 미구현 - 꺼 둘 것)")]
    [Tooltip("1x2/2x1/2x2 방용 예약 칸. 실제 배치 로직이 아직 없음.")]
    public bool allowMultiCellRooms = false;

    [Header("생성 재시도 한도")]
    public int maxGenerationAttempts = 100;
}

// 카테고리 하나가 이번 레벨에 몇 개 나오는지의 규칙입니다.
// 예: min 1, max 3, countWeights [2, 1, 1] -> 1개 50% / 2개 25% / 3개 25%.
[System.Serializable]
public class RoomCountRule
{
    public RoomCategory category;

    [Tooltip("이 카테고리의 최소 등장 개수.")]
    public int minCount = 1;

    [Tooltip("이 카테고리의 최대 등장 개수. 최소와 같으면 항상 그 개수.")]
    public int maxCount = 1;

    [Tooltip("최소~최대 각 개수의 가중치. 칸 수 = 최대 - 최소 + 1(최소부터 순서대로). 비거나 안 맞으면 최소 개수로 고정.")]
    public List<float> countWeights = new List<float>();
}
