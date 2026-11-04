using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

// 아이템 테스트 전용 씬을 Play 모드 진입 시 코드로 통째로 지어주는 컴포넌트입니다.
// LevelBuilder와 동일한 철학 - 아무것도 없는 빈 씬에 이 컴포넌트 하나만 있는 GameObject를
// 두고, Start()에서 바닥/벽/플레이어/아이템 지급/더미 적/아이템 설명칸 전시대를 전부
// 런타임에 생성합니다.
// 실제 레벨 생성 파이프라인(LevelGraphGenerator/RoomController/RealEnemySpawner)은 전혀
// 거치지 않는, 완전히 독립된 테스트 전용 스크립트입니다 - 기존 스크립트는 하나도 건드리지
// 않았습니다(ItemPedestal.cs의 AssignForDisplayOnly()는 예외 - 기존 로직을 전혀 바꾸지
// 않고 새 메서드 하나만 추가했습니다).
//
// [왜 만들었는가 - 형빈 확인]
// 지금까지는 아이템 테스트를 위해 보물상자의 등장 가중치를 10000으로 올려 특정 아이템이
// 무조건 뜨게 하는 방식을 썼는데, 여러 아이템을 한꺼번에 테스트할 때 시간이 오래 걸리고
// 가중치 10000을 뚫고 엉뚱한 아이템이 나오면 처음부터 다시 해야 하는 번거로움이 있었습니다.
// 이 컴포넌트는 그 대신 "테스트하고 싶은 아이템을 인스펙터에 넣고 Play만 누르면 즉시
// 플레이어에게 귀속되는" 전용 테스트장을 만듭니다.
//
// [바닥/벽 - 큐브 플레이스홀더, cellSize 10 기준 5x5칸]
// 실제 게임의 바닥/벽 프리팹을 전혀 쓰지 않고, LevelBuilder.BuildFloor()/BuildWallSegment()의
// "프리팹이 없으면 기본 Cube로 자동 생성" 폴백 경로와 동일한 방식으로 큐브 플레이스홀더만
// 만듭니다. 여러 방으로 나뉘는 실제 레벨과 달리 테스트장은 하나로 트인 사각형 아레나라,
// 칸(Room) 단위가 아니라 아레나 전체를 감싸는 바닥 하나 + 벽 4개로 단순화했습니다.
//
// [장애물 - 형빈이 직접 배치]
// 장애물 콜라이더는 형빈이 이미 갖고 있는 박스 콜라이더 모델링을 씬에 직접 가져다 놓을
// 예정이라, 이 스크립트는 장애물을 전혀 다루지 않습니다.
//
// [아이템 지급 - ItemPedestal.TryPickup()과 동일한 분류별 분기를 그대로 재사용]
// 액티브/장신구/일회용/음식은 각각 전용 슬롯 컴포넌트의 TryAcquireItem()을 그대로 호출하고
// (기존 슬롯이 있으면 교체되며, 교체되어 밀려난 아이템은 테스트 목적상 특별히 다루지
// 않고 버립니다 - 실제 플레이처럼 바닥에 떨어뜨리지 않습니다), 패시브는
// PlayerInventory.TryAcquireItem()을 호출합니다. F키로 여는 절차나 보물상자 자체는 전혀
// 없고, Start() 시점에 리스트에 넣어둔 아이템이 전부 즉시 적용됩니다.
//
// [더미 적 - 실제 드론 프리팹에서 AI만 제거]
// 기존 DummyEnemySpawner(캡슐 프리미티브 + EnemyHealth만 붙은 더미)를 쓰지 않고, 실제
// 드론 EnemyDefinition을 인스펙터에서 골라 그 프리팹을 그대로 스폰합니다. 스탯/체력
// 초기화는 RealEnemySpawner.SpawnOne()과 완전히 동일한 순서(EnemyStats.ApplyFrom ->
// attackConfig.ApplyTo -> EnemyHealth.Initialize)를 그대로 따라가 체력이 정상으로
// 잡히게 하되, 그 다음 아래 컴포넌트들을 제거/비활성화해서 "가만히 서서 맞기만 하는"
// 상태로 만듭니다:
//   - EnemyAIController / EnemyRangedAIController (이동·사격 판단) : Destroy
//   - IEnemyAttackBehavior를 구현한 실제 공격 컴포넌트(EnemyMeleeAttack/EnemyContactOnlyAttack/
//     EnemyChainDashAttack 등) + EnemyRangedAttack : Destroy (인터페이스 기준으로 찾으므로
//     드론 종류가 바뀌어도 새 공격 컴포넌트가 추가되는 것만으로 자동 대응됩니다)
//   - EnemySeparation (NavMeshAgent에 의존하는 적끼리 밀어내기 로직) : Destroy
//   - NavMeshAgent : Destroy하지 않고 enabled = false로만 비활성화합니다. 이동 판단
//     컴포넌트를 다 지웠으니 굳이 켜둘 필요가 없고, 꺼두면 더미가 서있는 위치가 NavMesh
//     경계에 살짝 걸쳐도(예: 벽 바로 앞) 밀려나지 않고 지정한 자리에 정확히 고정됩니다.
// 드론 프리팹을 직접 열어 확인한 결과(형빈 확인 필요 없이 프리팹 파일에서 직접 확인함),
// 이 컴포넌트들은 RealEnemySpawner가 런타임에 붙이는 게 아니라 프리팹 자체에 이미
// 에디터에서 붙어있으므로, 반드시 위와 같이 명시적으로 제거해야 합니다.
//
// [EnemyMeleeContactTrigger는 그대로 유지 - 형빈 확정]
// 몸으로 부딪히면 플레이어에게 접촉 피해를 주는 EnemyMeleeContactTrigger는 AI 컨트롤러와
// 별개의 컴포넌트이고, IEnemyAttackBehavior도 구현하지 않아 위 제거 대상에 자동으로
// 포함되지 않습니다 - 더미에게 몸으로 부딪히면 접촉 피해는 그대로 받도록 남겨두기로
// 형빈과 확정했습니다. 이 트리거가 참조하는 EnemyStats는 Destroy되지 않고 그대로 남아있는
// 컴포넌트이므로, 스탯 값도 정상적으로 반영됩니다.
//
// [NavMesh 런타임 베이크 - v2 추가]
// 바닥/벽이 전부 런타임(Play 시점)에 큐브로 생성되기 때문에 에디터에서 미리 구워둘 방법이
// 없습니다(LevelBuilder가 방마다 런타임 베이크를 쓰는 것과 완전히 같은 이유). 그래서
// BuildArena() 직후, 더미 적을 스폰하기 전에 LevelBuilder.BakeRoomNavMesh()와 동일한
// 방식(NavMeshSurface + PhysicsColliders)으로 아레나 전체 NavMesh를 한 번 굽습니다. 이
// 단계가 없으면 더미 적 프리팹에 이미 붙어있는 NavMeshAgent가 Instantiate 직후
// OnEnable()에서 "Failed to create agent because there is no valid NavMesh" 오류를
// 냅니다(이후 StripAiForDummy()가 그 NavMeshAgent를 비활성화하긴 하지만, 비활성화되기
// 전 그 짧은 순간에 이미 오류가 발생해버립니다).
//
// [아이템 설명칸 전시대 - v2 추가]
// 컴포넌트로 즉시 귀속시키는 기존 방식은 그대로 두고, 추가로 각 아이템마다 "설명칸만
// 보이는" 보물상자를 하나씩 더 세웁니다. 열기/획득 기능은 없고 다가가기만 하면 설명칸이
// 뜨는 게 목적이라(형빈 확정), Open()의 F키 절차를 아예 거치지 않고
// ItemPedestal.AssignForDisplayOnly()로 assignedItem을 즉시 확정시킵니다.
// PlayerItemInfoDisplay.FindNearestAvailableItem()이 IsOpened는 보지 않고
// PlayerInRange && AssignedItem != null만 확인하므로 이것만으로 충분합니다(UI 코드는
// 전혀 건드리지 않았습니다). itemsToGrant 리스트를 그대로 재사용하므로 인스펙터에 새로
// 설정할 건 없고, 플레이어 스폰 지점 기준 displayStandRowOffset만큼 떨어진 곳에
// displayStandSpacing 간격으로 한 줄로 자동 배치됩니다.
//
// [테스트 프로필 - 진짜 플레이어 프리팹 + 테스트용 덮어쓰기]
// playerPrefab에는 실제 게임과 같은 진짜 플레이어 프리팹을 연결하고, 테스트용 스탯/자원/무적/자원
// 무한은 testProfile(PlayerTestProfile 에셋)에 적습니다. testProfile이 있으면 플레이어를 꺼진
// 부모 아래에 생성해(=아직 Awake 전) 덮어쓰기를 적용한 뒤 꺼낸 순간 켜지게 해서, 시작 체력/탄창/
// 시작 자원/스탯 하한선이 전부 덮어쓴 값 기준으로 시작되게 합니다. 무적/자원 무한은 Update()에서
// 매 프레임 다시 반영하므로 Play 중에도 에셋에서 바로 켜고 끌 수 있습니다.
//
// [테스트 보물상자 - 획득/교체 테스트용, 즉시 귀속과 별개]
// testChestItems 목록의 아이템마다 진짜 TreasureChest(일반방 보물상자)를 하나씩 세우고, 내용물을 그 아이템으로
// 고정합니다(TreasureChest.SetContentResolver로 "열릴 때 이 아이템을 넣어라"만 맡김 - TreasureChest 등 기존
// 스크립트는 무수정). 그래서 F를 누르면 실제 게임과 완전히 같은 경로로 획득/교체되고(교체 확인창, 획득 연출,
// 아이콘 빛, F 안내, 설명칸 포함), 교체되면 원래 장착하던 아이템이 상자에 남아 왔다 갔다 바꿔볼 수 있습니다.
// - testChestsStartOpened(기본 켬): 처음부터 열린 상태 - 아이콘이 보이고 첫 F부터 바로 획득/교체. 끄면 실제처럼
//   첫 F는 열기입니다.
// - respawnEmptiedTestChests(기본 켬): 내용물을 다 가져가 상자가 사라지면 testChestRespawnDelay초 뒤 같은 자리에
//   원래 아이템을 담은 새 상자가 다시 생깁니다(반복 테스트용).
// - 배치: 플레이어 스폰 지점 기준 testChestRowOffset(기본 뒤쪽 8m - 설명칸 전시대 줄의 반대편)에
//   testChestSpacing 간격으로 한 줄 자동 배치. testChestPrefab을 비우면 금색 큐브 자리표시를 씁니다
//   (NormalRoomTreasureSpawner.SpawnChestObject와 같은 방식: 콜라이더 전부 트리거 + 바닥에 맞춰 높이 보정).
public class ItemTestArenaBuilder : MonoBehaviour
{
    [Header("아레나 크기 (cellSize 10 기준 5 x 5칸 = 50m x 50m)")]
    [Tooltip("한 칸의 월드 유닛 크기입니다. LevelBuilder.cellSize와 같은 의미입니다.")]
    public float cellSize = 10f;
    [Tooltip("아레나의 가로(X) 칸 수입니다.")]
    public int arenaCellsX = 5;
    [Tooltip("아레나의 세로(Z) 칸 수입니다.")]
    public int arenaCellsZ = 5;
    public float wallHeight = 4f;
    public float wallThickness = 0.5f;

    [Header("NavMesh (더미 적 스폰용 - 아레나 전체를 한 번만 런타임 베이크)")]
    [Tooltip("꺼두면 NavMesh를 굽지 않습니다 - 더미 적이 하나도 없다면 꺼도 무방합니다.")]
    public bool bakeNavMesh = true;
    [Tooltip("NavMesh를 구울 때 사용할 에이전트 타입 ID입니다 (Window > AI > Navigation > " +
             "Agents 탭 프리셋 목록 순서와 대응). LevelBuilder.navMeshAgentTypeID와 같은 값으로 " +
             "맞춰주세요(기본값 0은 보통 Humanoid 프리셋).")]
    public int navMeshAgentTypeID = 0;

    [Header("플레이어")]
    [Tooltip("Tag가 'Player'로 설정되어 있어야 합니다. LevelBuilder.playerPrefab과 동일한 프리팹을 연결하세요.")]
    public GameObject playerPrefab;

    [Header("테스트 프로필 (진짜 플레이어 프리팹 위에 테스트용 값 덮어쓰기)")]
    [Tooltip("PlayerTestProfile 에셋을 연결하세요. 비워두면 playerPrefab을 그대로(덮어쓰기 없이) 씁니다.")]
    public PlayerTestProfile testProfile;

    [Header("테스트할 아이템 (Play 시작과 동시에 전부 즉시 지급됩니다)")]
    [Tooltip("여기 넣은 아이템은 F키/보물상자 절차 없이 Start() 시점에 바로 플레이어에게 적용됩니다. " +
             "분류(Passive/Active/Trinket/Disposable/Food)는 각 아이템의 ItemDefinition.category를 그대로 따릅니다. " +
             "spawnItemDisplayStands가 켜져 있으면 이 목록과 동일한 아이템으로 설명칸 전시대도 함께 세워집니다.")]
    public List<ItemDefinition> itemsToGrant = new List<ItemDefinition>();

    [Header("더미 적 (실제 적 프리팹 기반 - AI/공격 컴포넌트만 제거, 접촉 피해는 유지)")]
    [Tooltip("각 항목마다 스폰할 적 종류(EnemyDefinition)와 위치를 지정하세요. 드론뿐 아니라 " +
             "attackConfig가 붙어있는 어떤 EnemyDefinition이든 사용할 수 있습니다.")]
    public List<DummyEnemySpawnPoint> dummyEnemies = new List<DummyEnemySpawnPoint>();

    [Tooltip("적 몸통 물리 레이어 이름입니다. RealEnemySpawner.enemyBodyLayerName과 동일한 값으로 맞춰주세요.")]
    public string enemyBodyLayerName = "Enemy";

    [Header("아이템 설명칸 전시대 (itemsToGrant와 동일한 목록 - F 없이 다가가기만 해도 설명칸이 뜹니다)")]
    [Tooltip("켜두면 itemsToGrant의 각 아이템마다 설명칸 전용 전시대를 하나씩 자동으로 세웁니다. " +
             "열기/획득 기능은 없고(F를 눌러도 무해하게 정상 지급은 됩니다), 다가가기만 하면 실제 " +
             "게임과 동일한 설명 패널이 뜹니다.")]
    public bool spawnItemDisplayStands = true;
    [Tooltip("전시대끼리의 가로 간격(m)입니다.")]
    public float displayStandSpacing = 3f;
    [Tooltip("플레이어 스폰 지점 기준 전시대 줄이 놓일 오프셋입니다. 기본값은 스폰 지점에서 앞으로 8m입니다.")]
    public Vector3 displayStandRowOffset = new Vector3(0f, 0f, -8f);
    [Tooltip("전시대 아이콘 스케일입니다. ItemPedestal.iconScale과 같은 의미입니다.")]
    public Vector3 displayStandIconScale = new Vector3(0.5f, 0.5f, 0.5f);
    [Tooltip("전시대 아이콘이 카메라 정면을 보도록 하는 고정 회전값입니다. CameraFollow의 " +
             "Fixed Euler Angles와 맞춰주세요.")]
    public Vector3 displayStandIconFixedEulerAngles = new Vector3(55f, 45f, 0f);

    [Header("테스트 보물상자 (목록의 아이템이 무조건 나옴 - F로 실제와 똑같이 획득/교체, 즉시 귀속과 별개)")]
    [Tooltip("여기 넣은 아이템마다 진짜 보물상자를 하나씩 세웁니다. 상자 안에는 항상 이 아이템이 들어있고, F로 " +
             "획득하거나 장착 중인 아이템과 교체할 수 있습니다(교체되면 원래 아이템이 상자에 남음). itemsToGrant와는 별개입니다.")]
    public List<ItemDefinition> testChestItems = new List<ItemDefinition>();
    [Tooltip("플레이어 스폰 지점 기준 테스트 보물상자 줄이 놓일 오프셋입니다. 기본값은 뒤쪽 8m(설명칸 전시대 줄의 반대편)입니다.")]
    public Vector3 testChestRowOffset = new Vector3(0f, 0f, 8f);
    [Tooltip("테스트 보물상자끼리의 가로 간격(m)입니다.")]
    public float testChestSpacing = 3f;
    [Tooltip("상자 모양으로 쓸 프리팹입니다(실제 게임의 보물상자 프리팹을 넣어도 됩니다). 비워두면 금색 큐브 자리표시를 씁니다.")]
    public GameObject testChestPrefab;
    [Tooltip("체크하면 처음부터 열린 상태로 시작합니다(아이콘이 보이고 첫 F부터 바로 획득/교체). 끄면 실제처럼 첫 F는 열기입니다.")]
    public bool testChestsStartOpened = true;
    [Tooltip("체크하면 내용물을 다 가져가 상자가 사라졌을 때, 잠시 뒤 같은 자리에 원래 아이템을 담은 새 상자가 다시 생깁니다.")]
    public bool respawnEmptiedTestChests = true;
    [Tooltip("상자가 사라진 뒤 다시 생길 때까지의 시간(초)입니다.")]
    [Min(0f)] public float testChestRespawnDelay = 1.5f;
    [Tooltip("상자 위 아이템 아이콘 크기입니다(TreasureChest.iconScale과 같은 의미, 실제 게임 확정값 0.05).")]
    public Vector3 testChestIconScale = new Vector3(0.05f, 0.05f, 0.05f);
    [Tooltip("상자 위 아이템 아이콘이 카메라 정면을 보도록 하는 고정 회전값입니다(TreasureChest.iconFixedEulerAngles와 같은 의미).")]
    public Vector3 testChestIconFixedEulerAngles = new Vector3(55f, -45f, 0f);
    [Tooltip("상자 기준 아이콘이 떠 있을 높이입니다(TreasureChest.iconHeight와 같은 의미).")]
    public float testChestIconHeight = 1f;

    // 테스트 보물상자 한 자리(어떤 아이템을 어디에 - 상자가 사라지면 다시 만들기 위해 기억)
    private class TestChestSlot
    {
        public ItemDefinition item;
        public Vector3 position;
        public TreasureChest chest;
        public float respawnTimer;
    }
    private readonly List<TestChestSlot> testChestSlots = new List<TestChestSlot>();

    [System.Serializable]
    public class DummyEnemySpawnPoint
    {
        public EnemyDefinition definition;
        public Vector3 position;
    }

    private GameObject spawnedPlayer;

    // 테스트 프로필의 무적/자원 무한 스위치를 Play 중에도 바로 반영합니다.
    void Update()
    {
        if (testProfile != null && spawnedPlayer != null)
        {
            testProfile.ApplyToggles(spawnedPlayer);
        }

        UpdateTestChestRespawn();
    }

    void Start()
    {
        BuildArena();
        if (bakeNavMesh) BakeArenaNavMesh();
        SpawnPlayerAndGrantItems();
        SpawnDummyEnemies();
        if (spawnItemDisplayStands) SpawnItemDisplayStands();
        SpawnTestChests();
    }

    // ---------- 바닥 + 벽 4개 (큐브 플레이스홀더) ----------
    private void BuildArena()
    {
        var arenaRoot = new GameObject("TestArena").transform;
        arenaRoot.SetParent(transform);

        float sizeX = cellSize * arenaCellsX;
        float sizeZ = cellSize * arenaCellsZ;

        // 바닥 - LevelBuilder.BuildFloor()의 플레이스홀더 경로와 동일하게 얇은 Cube 하나로 아레나
        // 전체를 덮습니다(칸마다 나누지 않고 한 장으로 처리 - 여러 방으로 갈라지는 실제 레벨과
        // 달리 테스트장은 애초에 하나로 트인 공간이라 나눌 필요가 없습니다).
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Floor_Placeholder";
        floor.transform.SetParent(arenaRoot);
        floor.transform.position = new Vector3(0f, -0.1f, 0f);
        floor.transform.localScale = new Vector3(sizeX, 0.2f, sizeZ);

        // 벽 4개 - LevelBuilder.BuildWallSegment()의 닫힌 벽 플레이스홀더와 동일한 형태(Cube,
        // 높이 wallHeight, 두께 wallThickness)로 아레나 바깥 테두리를 완전히 막습니다.
        BuildWall(arenaRoot, "Wall_North", new Vector3(0f, wallHeight / 2f, sizeZ / 2f), new Vector3(sizeX, wallHeight, wallThickness));
        BuildWall(arenaRoot, "Wall_South", new Vector3(0f, wallHeight / 2f, -sizeZ / 2f), new Vector3(sizeX, wallHeight, wallThickness));
        BuildWall(arenaRoot, "Wall_East", new Vector3(sizeX / 2f, wallHeight / 2f, 0f), new Vector3(wallThickness, wallHeight, sizeZ));
        BuildWall(arenaRoot, "Wall_West", new Vector3(-sizeX / 2f, wallHeight / 2f, 0f), new Vector3(wallThickness, wallHeight, sizeZ));
    }

    private void BuildWall(Transform parent, string name, Vector3 position, Vector3 scale)
    {
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = name;
        wall.transform.SetParent(parent);
        wall.transform.position = position;
        wall.transform.localScale = scale;
    }

    // ---------- NavMesh 베이크 (더미 적의 NavMeshAgent가 "NavMesh 없음" 오류를 내지 않도록) ----------
    // LevelBuilder.BakeRoomNavMesh()와 동일한 방식입니다 - 바닥/벽이 런타임에 생성되므로
    // 에디터에서 미리 구워둘 수 없어, Physics 콜라이더 정보를 직접 읽어 런타임에 굽습니다.
    // 반드시 BuildArena() 다음, SpawnDummyEnemies() 이전에 호출해야 합니다.
    private void BakeArenaNavMesh()
    {
        Physics.SyncTransforms();

        var surface = gameObject.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.Children;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.agentTypeID = navMeshAgentTypeID;
        surface.BuildNavMesh();
    }

    // ---------- 플레이어 스폰 + 아이템 즉시 지급 ----------
    private void SpawnPlayerAndGrantItems()
    {
        if (playerPrefab == null)
        {
            Debug.LogWarning("ItemTestArenaBuilder: playerPrefab이 비어있어 플레이어를 스폰하지 않았습니다.");
            return;
        }

        Vector3 spawnPos = Vector3.up * 1f;

        if (testProfile != null)
        {
            // 꺼진 부모 아래에 생성하면 플레이어의 Awake/OnEnable이 아직 실행되지 않습니다. 그 사이에
            // 테스트 값을 덮어쓰고, 부모에서 꺼내는 순간 켜지면서 시작 처리가 덮어쓴 값으로 실행됩니다.
            var holder = new GameObject("TestPlayerSpawnHolder (inactive)");
            holder.SetActive(false);
            spawnedPlayer = Instantiate(playerPrefab, spawnPos, Quaternion.identity, holder.transform);
            spawnedPlayer.name = playerPrefab.name;
            testProfile.ApplyTo(spawnedPlayer);
            spawnedPlayer.transform.SetParent(null, true);
            Destroy(holder);
        }
        else
        {
            spawnedPlayer = Instantiate(playerPrefab, spawnPos, Quaternion.identity);
        }

        // CameraFollow는 Awake() 시점에 "Player" 태그를 자동으로 찾지만, 이 컴포넌트가
        // 플레이어보다 먼저 Awake될 수도 있으므로 LevelBuilder.SpawnPlayer()와 동일하게
        // 안전하게 재연결해줍니다. 이 경고가 뜬다면 씬의 Main Camera에 CameraFollow
        // 컴포넌트 자체가 없다는 뜻입니다(실제 게임 씬처럼 Main Camera에 직접 Add
        // Component로 붙여줘야 합니다 - 이 스크립트는 카메라를 만들어주지 않습니다).
        var cameraFollow = Object.FindFirstObjectByType<CameraFollow>();
        if (cameraFollow != null)
        {
            cameraFollow.target = spawnedPlayer.transform;
        }
        else
        {
            Debug.LogWarning("ItemTestArenaBuilder: 씬에서 CameraFollow 컴포넌트를 찾지 못했습니다.");
        }

        // ItemPedestal.TryPickup()과 동일한 분류별 분기입니다 - 스왑형(액티브/장신구/일회용/
        // 음식)은 각자의 슬롯 컴포넌트가 처리하고, 나머지(패시브)는 PlayerInventory가
        // 처리합니다. 테스트 목적상 교체되어 밀려난 아이템(out displacedItem)은 특별히
        // 다루지 않고 버립니다 - 실제 플레이처럼 바닥에 다시 드랍하지 않습니다.
        var playerInventory = spawnedPlayer.GetComponent<PlayerInventory>();
        var playerActiveItem = spawnedPlayer.GetComponent<PlayerActiveItem>();
        var playerTrinket = spawnedPlayer.GetComponent<PlayerTrinket>();
        var playerDisposableItem = spawnedPlayer.GetComponent<PlayerDisposableItem>();
        var playerFoodItem = spawnedPlayer.GetComponent<PlayerFoodItem>();

        foreach (var item in itemsToGrant)
        {
            if (item == null) continue;

            switch (item.category)
            {
                case ItemDefinition.ItemCategory.Active:
                    if (playerActiveItem != null) playerActiveItem.TryAcquireItem(item, out _);
                    break;
                case ItemDefinition.ItemCategory.Trinket:
                    if (playerTrinket != null) playerTrinket.TryAcquireItem(item, out _);
                    break;
                case ItemDefinition.ItemCategory.Disposable:
                    if (playerDisposableItem != null) playerDisposableItem.TryAcquireItem(item, out _);
                    break;
                case ItemDefinition.ItemCategory.Food:
                    if (playerFoodItem != null) playerFoodItem.TryAcquireItem(item, out _);
                    break;
                default: // Passive
                    if (playerInventory != null) playerInventory.TryAcquireItem(item);
                    break;
            }
        }
    }

    // ---------- 더미 적 스폰 ----------
    private void SpawnDummyEnemies()
    {
        foreach (var spawnPoint in dummyEnemies)
        {
            if (spawnPoint == null || spawnPoint.definition == null || spawnPoint.definition.prefab == null)
            {
                Debug.LogWarning("ItemTestArenaBuilder: EnemyDefinition 또는 prefab이 비어있는 더미 적 슬롯을 건너뛰었습니다.");
                continue;
            }

            SpawnDummyEnemy(spawnPoint.definition, spawnPoint.position);
        }
    }

    private void SpawnDummyEnemy(EnemyDefinition definition, Vector3 position)
    {
        GameObject enemyGO = Instantiate(definition.prefab, position, Quaternion.identity, transform);
        enemyGO.name = $"Dummy_{definition.variantId}";

        // RealEnemySpawner.ApplyEnemyTagAndLayer()와 동일합니다 - 플레이어 총알이 Enemy 태그로
        // 이 더미를 인식해야 정상적으로 맞습니다.
        enemyGO.tag = "Enemy";
        int layerIndex = LayerMask.NameToLayer(enemyBodyLayerName);
        if (layerIndex >= 0)
        {
            enemyGO.layer = layerIndex;
        }
        else
        {
            Debug.LogWarning($"ItemTestArenaBuilder: '{enemyBodyLayerName}' 레이어가 프로젝트에 없습니다.");
        }

        // RealEnemySpawner.SpawnOne()과 완전히 동일한 순서입니다 - EnemyHealth.Initialize()는
        // 반드시 stats.ApplyFrom() 이후(값이 전부 확정된 뒤)에 호출해야 체력이 정확히 잡힙니다
        // (EnemyHealth.cs 상단 주석 참고 - 순서가 틀리면 currentHealth가 0으로 남아 스폰 즉시
        // 죽어버립니다).
        var stats = enemyGO.GetComponent<EnemyStats>();
        if (stats == null) stats = enemyGO.AddComponent<EnemyStats>();
        stats.ApplyFrom(definition);
        definition.attackConfig?.ApplyTo(stats, enemyGO);

        var health = enemyGO.GetComponent<EnemyHealth>();
        if (health == null) health = enemyGO.AddComponent<EnemyHealth>();
        health.Initialize(stats);

        StripAiForDummy(enemyGO);
    }

    // 이동/사격/돌진 판단을 담당하는 컴포넌트를 전부 제거해서 "가만히 서서 맞기만 하는" 상태로
    // 만듭니다. EnemyMeleeContactTrigger(접촉 피해)는 이 목록에 포함되지 않으므로 그대로
    // 남습니다(형빈 확정 - 클래스 상단 주석 참고).
    private void StripAiForDummy(GameObject enemyGO)
    {
        var rangedAIController = enemyGO.GetComponent<EnemyRangedAIController>();
        if (rangedAIController != null) Destroy(rangedAIController);

        var aiController = enemyGO.GetComponent<EnemyAIController>();
        if (aiController != null) Destroy(aiController);

        var rangedAttack = enemyGO.GetComponent<EnemyRangedAttack>();
        if (rangedAttack != null) Destroy(rangedAttack);

        // 근접/돌진/접촉 전용 공격 등 IEnemyAttackBehavior를 구현하는 컴포넌트는 전부
        // 제거합니다 - 인터페이스 기준이라 새 공격 방식이 추가돼도 이 코드는 손댈 필요가
        // 없습니다.
        var attackBehaviors = enemyGO.GetComponents<IEnemyAttackBehavior>();
        foreach (var behavior in attackBehaviors)
        {
            if (behavior is Component component) Destroy(component);
        }

        var separation = enemyGO.GetComponent<EnemySeparation>();
        if (separation != null) Destroy(separation);

        // NavMeshAgent는 Destroy하지 않고 비활성화만 합니다 - 이동 판단 컴포넌트를 이미 다
        // 지웠으므로 계속 켜져 있을 이유가 없고, 꺼두면 벽 바로 앞 등 NavMesh 경계에 걸친
        // 위치에서도 밀려나지 않고 지정한 자리에 정확히 고정됩니다.
        var agent = enemyGO.GetComponent<NavMeshAgent>();
        if (agent != null) agent.enabled = false;
    }

    // ---------- 아이템 설명칸 전시대 (F 없이 다가가기만 해도 설명 패널이 뜹니다) ----------
    private void SpawnItemDisplayStands()
    {
        var validItems = new List<ItemDefinition>();
        foreach (var item in itemsToGrant)
        {
            if (item != null) validItems.Add(item);
        }
        if (validItems.Count == 0) return;

        float startX = -(validItems.Count - 1) * displayStandSpacing / 2f;
        for (int i = 0; i < validItems.Count; i++)
        {
            Vector3 position = displayStandRowOffset + new Vector3(startX + i * displayStandSpacing, 0f, 0f);
            SpawnItemDisplayStand(validItems[i], position);
        }
    }

    // LevelBuilder.SpawnClubItemPedestal()의 플레이스홀더 생성 패턴과 동일합니다 - Cube +
    // 트리거 콜라이더 + ItemPedestal. Open()/TryPickup() 절차 없이
    // ItemPedestal.AssignForDisplayOnly()로 아이템을 즉시 확정합니다.
    //
    // [v3 수정 - Icon Scale이 인스펙터에서 바뀌지 않던 문제]
    // ItemPedestal.Awake()가 iconScale/iconFixedEulerAngles 값을 읽어 아이콘 스프라이트를
    // "딱 한 번만" 만듭니다(LevelBuilder.cs의 동일한 주석 참고). 그런데 CreatePrimitive로
    // 만든 standGO는 이미 활성(active) 상태라, AddComponent<ItemPedestal>()을 호출하는 그
    // 즉시 Awake()가 실행되어 버립니다 - 그 시점엔 아직 pedestal.iconScale에 우리가 원하는
    // displayStandIconScale 값을 넣기 전이라, 항상 ItemPedestal 클래스 기본값(0.5,0.5,0.5)
    // 그대로 아이콘이 만들어지고, 그 다음에 필드를 바꿔봐야 이미 만들어진 아이콘 크기에는
    // 반영되지 않았던 것입니다. LevelBuilder.SpawnClubItemPedestal()은 정확히 이 문제를
    // 피하려고 "오브젝트를 비활성 상태로 만들어 Awake를 지연시킨 뒤 필드값을 먼저 채우고
    // 마지막에 활성화"하는 순서를 씁니다 - 여기서도 동일한 순서로 고쳤습니다.
    private void SpawnItemDisplayStand(ItemDefinition item, Vector3 position)
    {
        var standGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
        standGO.name = $"ItemDisplayStand_{item.itemId}";
        standGO.transform.SetParent(transform);
        standGO.transform.position = position + Vector3.up * 0.4f;
        standGO.transform.localScale = new Vector3(0.8f, 0.8f, 0.8f);

        var col = standGO.GetComponent<Collider>();
        if (col != null) col.isTrigger = true;

        // Awake()가 지금 당장 실행되지 않도록 잠깐 비활성화한 상태로 컴포넌트를 붙이고
        // 아이콘 관련 필드를 먼저 채운 뒤, 마지막에 다시 활성화합니다.
        standGO.SetActive(false);
        var pedestal = standGO.AddComponent<ItemPedestal>();
        pedestal.iconScale = displayStandIconScale;
        pedestal.iconFixedEulerAngles = displayStandIconFixedEulerAngles;
        standGO.SetActive(true);

        pedestal.AssignForDisplayOnly(item);
    }

    // ---------- 테스트 보물상자 (지정 아이템 고정 - F로 실제와 똑같이 획득/교체) ----------
    private void SpawnTestChests()
    {
        testChestSlots.Clear();

        var validItems = new List<ItemDefinition>();
        foreach (var item in testChestItems)
        {
            if (item != null) validItems.Add(item);
        }
        if (validItems.Count == 0) return;

        float startX = -(validItems.Count - 1) * testChestSpacing / 2f;
        for (int i = 0; i < validItems.Count; i++)
        {
            var slot = new TestChestSlot
            {
                item = validItems[i],
                position = testChestRowOffset + new Vector3(startX + i * testChestSpacing, 0f, 0f),
            };
            slot.chest = SpawnTestChest(slot.item, slot.position, false);
            testChestSlots.Add(slot);
        }
    }

    // 내용물을 다 가져가 상자가 사라졌으면(TreasureChest가 스스로 소멸) 잠시 뒤 같은 자리에 다시 세웁니다.
    private void UpdateTestChestRespawn()
    {
        if (!respawnEmptiedTestChests) return;

        foreach (var slot in testChestSlots)
        {
            if (slot.chest != null) continue; // 아직 살아있음(교체로 다른 아이템이 들어있는 경우 포함)

            slot.respawnTimer += Time.deltaTime;
            if (slot.respawnTimer < testChestRespawnDelay) continue;

            slot.respawnTimer = 0f;
            slot.chest = SpawnTestChest(slot.item, slot.position, true);
        }
    }

    // NormalRoomTreasureSpawner.SpawnChestObject()와 같은 방식으로 상자 오브젝트를 만들고(프리팹 또는 금색 큐브,
    // 콜라이더 전부 트리거, 바닥에 맞춰 높이 보정), 내용물을 item으로 고정합니다.
    private TreasureChest SpawnTestChest(ItemDefinition item, Vector3 position, bool appearEffect)
    {
        GameObject chestGO;
        if (testChestPrefab != null)
        {
            chestGO = Instantiate(testChestPrefab, position, testChestPrefab.transform.rotation, transform);
        }
        else
        {
            chestGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
            chestGO.transform.SetParent(transform);
            chestGO.transform.position = position;
            chestGO.transform.localScale = new Vector3(0.8f, 0.8f, 0.8f);
            var rend = chestGO.GetComponent<Renderer>();
            if (rend != null) rend.material.color = new Color(1f, 0.85f, 0.2f); // 임시 식별용 금색
        }
        chestGO.name = $"TestTreasureChest_{item.itemId}";

        var chest = chestGO.GetComponent<TreasureChest>();
        if (chest == null) chest = chestGO.AddComponent<TreasureChest>();

        // TreasureChest가 붙은 뒤에(자동 추가된 콜라이더 포함) 전부 트리거로 - 플레이어가 통과할 수 있게.
        foreach (var col in chestGO.GetComponentsInChildren<Collider>())
        {
            if (col is MeshCollider meshCollider && !meshCollider.convex) meshCollider.convex = true;
            col.isTrigger = true;
        }

        SnapTestChestToGround(chestGO, position.y);

        // 아이콘은 내용물이 정해질 때(Open) 만들어지므로 그 전에 표시값을 넣어둡니다.
        chest.iconScale = testChestIconScale;
        chest.iconFixedEulerAngles = testChestIconFixedEulerAngles;
        chest.iconHeight = testChestIconHeight;

        // 열릴 때 이 아이템을 넣도록 맡겨둡니다(무조건 이 아이템).
        chest.SetContentResolver(() => chest.SetPassiveItemContent(item));
        if (testChestsStartOpened) chest.Open();

        if (appearEffect) AppearFromWhite.Begin(chestGO);
        return chest;
    }

    // 렌더러 경계의 아래쪽 끝이 floorY에 맞닿도록 높이를 보정합니다(NormalRoomTreasureSpawner.SnapToGround와 동일).
    private static void SnapTestChestToGround(GameObject go, float floorY)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers == null || renderers.Length == 0) return;

        Bounds combined = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) combined.Encapsulate(renderers[i].bounds);

        float offset = floorY - combined.min.y;
        if (Mathf.Abs(offset) > 0.0001f) go.transform.position += new Vector3(0f, offset, 0f);
    }
}
