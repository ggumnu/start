using System.Collections.Generic;
using System.Linq;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

// LevelGraphGenerator(또는 ChoiceLevelGenerator)가 만든 순수 데이터(RoomInstance 리스트)를
// 받아서 실제로 씬에 바닥/벽/게이트/적을 배치하고, 방마다 RoomController를 붙여
// "한 방만 보이기 + 클리어 전까지 잠금" 시스템까지 연결하는 스크립트입니다.
//
// [구조]
// "방 데이터를 어떻게 생성할지"(BuildLevel = 전투 레벨, BuildChoiceLevel = n.5 레벨)와
// "생성된 방 데이터를 어떻게 씬에 짓는지"(BuildRooms 이하 공통 로직)를 분리했습니다.
//
// [최초 스폰 vs 레벨 전환]
// BuildLevel()/BuildChoiceLevel()는 이제 GameObject existingPlayer를 받는 오버로드가
// 있습니다. null이면(기존과 동일) 새 플레이어를 프리팹으로 생성하고, 값이 있으면 그 플레이어
// 오브젝트를 파괴하지 않고 새 시작방 위치로 "이동"만 시킵니다. 이러면 체력/스탯/아이템이
// 전부 그대로 유지됩니다. 파라미터 없는 버전(ContextMenu, buildOnStart용)은 내부적으로
// null을 넘겨 기존과 동일하게 동작하므로 하위 호환됩니다.
// 실제 사용처는 이후 GameFlowManager가 될 예정이며, 그게 생기기 전까지는 아래 TEST용
// ContextMenu 2개로 전환 동작을 직접 검증할 수 있습니다.
//
// [중요 - 반드시 Play 모드에서만 빌드하세요]
// RoomController의 게이트 잠금이나 LevelBuilder.Update()의 칸 감지 등 이 시스템의
// 런타임 로직 대부분은 Play 모드에서만 정상 동작합니다.
//
// [방향별 프리팹 회전 재사용 - 북/서, 남/동 쌍]
// 마름모 시야 기준 화면 위쪽(카메라에서 먼, 항상 보이는 두 변)은 북/서, 화면 아래쪽
// (카메라에 가까워 숨겨지는 두 변)은 남/동입니다. 같은 그룹끼리는 시각적으로 비슷한
// 역할이라, 북쪽용 프리팹 하나만 만들어도 서쪽에서 그 프리팹을 90도 회전시켜 자동으로
// 재사용하고(남/동도 동일), 리소스를 두 배로 만들 필요가 없습니다. 단, 특정 방향에만
// 다른 디자인을 주고 싶으면 그 방향에 개별 프리팹을 등록하면 되고, 그러면 회전 없이
// 그 방향 전용 프리팹이 우선 사용됩니다 (아래 GetPoolPrefabWithRotation 참고).
//
// [NavMesh 런타임 베이크]
// 일반방의 바닥 프리팹이 매번 랜덤으로 선택되고, 그 안의 장애물 배치도 프리팹마다
// 다르기 때문에 에디터에서 미리 구워둔 정적 NavMesh를 쓸 수 없습니다. 그래서 방 하나를
// 다 짓고(바닥+벽 배치 완료) 적을 스폰하기 직전에, 그 방의 Content 루트만 대상으로
// NavMeshSurface를 이용해 런타임 베이크합니다 (BakeRoomNavMesh 참고). Unity 6.3에는
// AI Navigation 패키지가 기본 포함되어 있어 별도 설치는 필요 없습니다.
//
// [방 전환 시 바리케이드 겹침 방지 - 입장 위치 전진 밀어넣기]
// 방 전환은 트리거가 아니라 Update()에서 플레이어 좌표를 칸(cell) 단위로 반올림해서
// 판단합니다. 이 반올림 경계선은 정확히 골목(열린 벽) 프리팹이 세워지는 위치와
// 같습니다. 예전에는 이 경계선을 넘는 "바로 그 순간" 새 방의 바리케이드를 잠갔는데,
// 그러면 막 경계를 넘어온 플레이어와 바리케이드가 곧바로 겹쳐 다음 방에 아예 들어갈
// 수 없는 문제가 있었고, 겹침을 피하려고 유예 시간을 두면 이번엔 그 사이에 바로
// 후진해서 클리어 없이 이전 방으로 탈출하는 구멍이 생겼습니다.
//
// 그래서 Update()가 칸 전환을 감지하면, RoomController.EnterRoom()을 호출해 그 방의
// 바리케이드를 "즉시" 잠그기 직전에, 먼저 PushPlayerForwardIntoRoom()으로 플레이어를
// 진입 방향으로 roomEntryPushDistance만큼 먼저 밀어넣습니다. 이러면 바리케이드가 잠기는
// 시점엔 플레이어가 이미 바리케이드 구간보다 확실히 안쪽에 서 있으므로 겹칠 일이
// 없고, 유예 시간 자체가 없으니 후진으로 빠져나갈 틈도 없습니다.
//
// [칸 전환 판정 지점 자체를 옮기고 싶을 때 - cellTransitionMargin]
// 위 push는 "전환은 정중앙에서 일어나되, 그 직후 플레이어를 밀어넣는" 방식이라 밀어내는
// 거리가 크면 화면이 순간 밀리는 느낌이 들 수 있습니다. 그 대신 "전환이 일어나는 지점
// 자체"를 골목 안쪽으로 옮기고 싶다면 cellTransitionMargin을 쓰세요. 이전 칸을 기준으로
// (cellSize/2 + cellTransitionMargin)만큼 벗어나야 옆 칸으로 인정되는 히스테리시스
// 판정이라, 이 값을 키우면 실제 걸어야 하는 거리 자체가 늘어나 자연스럽게 골목 더
// 안쪽에서 전환됩니다(ComputeAxisCellWithHysteresis 참고). margin을 충분히 키워 전환
// 시점에 이미 바리케이드 안쪽까지 걸어 들어와 있게 만들면, roomEntryPushDistance는
// 0에 가깝게 줄여도 됩니다 - 즉 두 값을 함께 조절해 "순간 밀림 없이 자연스럽게 지나가되
// 클리어 전엔 못 빠져나가는" 지점을 인스펙터에서 직접 튜닝하면 됩니다.
//
// [골목이 근접 적의 "안전지대"가 되지 않도록 - NavMesh 커버리지]
// 골목 바닥에 실제로 걸을 수 있는 콜라이더가 있더라도, 통로 폭이 navMeshAgentTypeID가
// 가리키는 에이전트 타입의 반지름보다 좁으면 그 구간은 아예 워커블 NavMesh로 구워지지
// 않습니다 - 플레이어는 CharacterController라 자유롭게 지나다니지만, NavMeshAgent인 적은
// 물리적으로 그 자리에 존재할 수 없는 상태가 되어버립니다(그래서 EnemyMeleeAttack의 돌진
// 목표 도달 가능성 검사에 항상 걸려 접근을 포기합니다 - 그 자체는 정상 동작이지만, 결과적
// 으로 골목이 완전한 안전지대가 되어버립니다). 근접 적도 골목 안까지(바리케이드 앞까지)
// 실제로 쫓아오게 하려면: (1) 골목 프리팹 자체에 통로 폭 전체를 덮는 바닥 콜라이더(트리거
// 아님)가 있는지, (2) 그 통로 폭이 navMeshAgentTypeID 프리셋의 반지름×2보다 충분히 넓은지
// 확인하세요. 필요하면 Window > AI > Navigation > Agents에서 반지름이 더 작은 전용 에이전트
// 타입을 만들어 navMeshAgentTypeID에 연결하세요.
//
// [아이템 - 동아리방 무료 보물상자]
// 동아리방(Club)은 첫 아이템을 무료로 얻는 방입니다. BuildRoom()이 방을 다 지은 뒤
// 카테고리가 Club이면, itemDatabase에서 이 방의 컨텍스트 키(ItemDatabase.GetContextKey -
// variantId가 있으면 그 문자열, 없으면 category 이름)와 일치하는 아이템만 걸러서
// ItemPedestal을 하나 스폰합니다. "어떤 방에서 어떤 아이템이 나오는가"는 LevelBuilder가
// 아니라 각 ItemDefinition.appearanceContexts가 결정하므로, 나중에 보스방 100%/일반방
// 저확률/상점(카페/편의점 등 variantId별로 다른 풀) 등장이 추가되어도 LevelBuilder에
// 새 리스트 필드를 추가할 필요 없이 SpawnClubItemPedestal과 동일한 패턴(그 방의 컨텍스트
// 키로 itemDatabase.GetItemsForContext() 호출)만 그 방 카테고리에도 적용하면 됩니다.
// 실제 어떤 아이템이 뽑힐지는 ItemPedestal 자신이 플레이어(PlayerInventory)를 찾은
// 시점에 결정합니다(중복 보유 아이템은 자동 제외).
//
// [v1.13 변경 - 실제 보물상자 프리팹 적용 중 발견된 스폰 위치/회전 버그 수정]
// itemPedestalPrefab을 실제 3D 프리팹("드론 상자"류)으로 교체해 테스트하는 과정에서,
// SpawnClubItemPedestal()이 Instantiate(prefab, position, rotation, parent) 형태의
// 오버로드를 쓰고 있어 프리팹 자체에 미리 잡아둔 위치/회전(에디터에서 눈으로 보고 앉혀둔
// 값)이 전부 무시되고 항상 contentRoot 위치 + 회전 없음(Quaternion.identity)으로
// 강제되는 문제가 있었습니다(그 결과 상자가 땅에 박히고 전면부가 하늘을 보는 등 프리팹
// 원본과 다르게 나타남). Instantiate(prefab, parent) 형태로 바꿔서 프리팹이 원래 갖고
// 있던 로컬 위치/회전을 그대로 보존하도록 수정했습니다(자세한 내용은 아래
// SpawnClubItemPedestal() 주석 참고).
//
// [v1.16 변경 - 미니맵을 위한 읽기 전용 접근자 추가]
// 화면 우측 상단 미니맵(MinimapDisplay)이 "지금까지 생성된 방 목록"과 "플레이어가 지금
// 어느 칸에 있는지"를 읽어야 해서, 기존 private 필드(generatedRooms/currentPlayerCell/
// hasPlayerCell)는 그대로 두고 읽기 전용 프로퍼티만 추가했습니다. 또한 레벨 전환(n.5 선택방
// 등)이 일어날 때마다 generatedRooms 자체가 새 리스트로 통째로 교체되므로, 미니맵이 "방
// 목록이 바뀐 시점"을 감지할 수 있도록 BuildRooms()가 호출될 때마다 1씩 증가하는
// levelBuildVersion 카운터도 함께 추가했습니다. 기존 로직은 전혀 바뀌지 않았습니다.
//
// [일반맵 보물상자 연동]
// treasureSpawner(NormalRoomTreasureSpawner)를 새 필드로 추가하고, BuildRoom()이
// RoomController.Setup()을 호출할 때 그대로 넘겨줍니다. 실제 등장 확률/위치/내용물 결정은
// 전부 NormalRoomTreasureSpawner와 RoomController.HandleCleared()가 담당하고, 이
// 스크립트는 그 참조를 전달만 합니다.
//
// [런타임 방 추가 - 16번 액티브 아이템 등]
// 레벨이 다 지어진 뒤에도 TryAddExtraRoom()으로 방을 하나 더 붙일 수 있습니다. Level Config(
// RoomGenerationConfig)의 방 개수 규칙(roomCountRules)과는 무관하게 독자적으로 동작하며, 새 방이
// 들어갈 자리는 원래 레벨 생성 규칙과 똑같이 "부모방의 빈 옆칸 중 부모방 외 다른 방과 맞닿지
// 않는 칸"입니다(그래야 새 방이 막다른 방이 되고 미니맵 연결선도 하나만 생깁니다). 맵 크기
// 제한(boundingBoxLimit)도 적용하지 않습니다. 부모방은 적이 없는 방(전투가 없는 방, 또는 이미
// 클리어한 일반방)이어야 하고 보스방은 제외합니다 - 적 스폰 기록/바리케이드/NavMesh 재베이크가
// 전투 중인 방과 꼬이는 것을 막기 위해서입니다. n.5 선택방 레벨에서는 쓸 수 없습니다.
// 짓는 과정은 (1) 새 방 데이터 + 부모방과의 연결(AnchorLink) 생성, (2) 기존 BuildRoom으로 새
// 방 짓기, (3) 부모방의 닫힌 벽을 골목(열린 벽)으로 교체, (4) 부모방 NavMesh 다시 굽기,
// (5) LevelBuildVersion 증가(미니맵이 자동으로 다시 그림) 순서입니다.
public class LevelBuilder : MonoBehaviour
{
    public enum LevelBuildMode { Combat, ChoiceLevel }

    [Header("사용할 레벨 설정 (전투 레벨)")]
    public RoomGenerationConfig config;

    [Header("n.5 선택방 레벨 설정")]
    public ChoiceLevelConfig choiceLevelConfig;
    [Tooltip("독립 테스트(플레이어 미존재 상태)로 n.5 레벨을 빌드할 때만 사용되는 확률 소스입니다. " +
             "실제 레벨 전환 테스트(TEST: Transition 메뉴) 시에는 전환되는 플레이어 본인의 PlayerStats가 우선 사용됩니다.")]
    public PlayerStats debugPlayerStatsForChoiceLevel;

    [Header("칸 크기 (씬 상의 월드 유닛)")]
    public float cellSize = 10f;

    [Header("벽 설정")]
    public float wallHeight = 4f;
    public float wallThickness = 0.5f;
    [Tooltip("방향별 프리팹 회전 재사용(북<->서, 남<->동)의 회전 방향이 실제로 반대로 보이면 " +
             "이 옵션을 켜서 부호를 뒤집어보세요 (카메라 각도를 잡을 때와 같은 성격의 문제입니다).")]
    public bool invertWallRotationDirection = false;

    [Header("프리팹 - 일반방 기본/폴백 풀 (비워두면 기본 Cube로 자동 생성)")]
    [Tooltip("아래 roomWallSets에 매칭되는 세트가 없는 방(주로 일반방)에 사용할 바닥 프리팹 " +
             "후보 풀입니다. 여러 개를 넣으면 가중치 기반으로 랜덤 선택되고, 비워두면 얇은 " +
             "Cube를 바닥으로 생성합니다.")]
    public List<WeightedPrefab> floorPool = new List<WeightedPrefab>();
    [Tooltip("아래 roomWallSets에 매칭되는 세트가 없는 방(주로 일반방)에 사용할 방향별 벽 " +
             "프리팹 후보 풀입니다. 방향(North/East/South/West)마다 하나씩 항목을 추가하고, " +
             "각 항목 안의 닫힌 벽/열린 벽(골목) 리스트에 후보 프리팹들을 넣으면 됩니다. " +
             "같은 방향에 후보가 여러 개면 가중치 기반으로 랜덤 선택되고, 해당 방향에 등록된 " +
             "풀이 없거나 후보가 비어있으면 짝 방향(북<->서, 남<->동)의 프리팹을 회전시켜 " +
             "자동으로 재사용합니다. 그마저도 없으면 기본 Cube 플레이스홀더로 자동 대체됩니다. " +
             "방향을 전부 채울 필요는 없습니다 - 한쪽만 만들면 짝 방향은 자동으로 해결되고, " +
             "방향마다 다른 디자인을 주고 싶을 때만 그 방향에 따로 등록하면 됩니다.")]
    public List<DirectionalWallPool> wallPools = new List<DirectionalWallPool>();

    [Header("프리팹 - 특수방 전용 풀 (카테고리 또는 variantId 매칭, 있으면 위 기본 풀보다 우선)")]
    [Tooltip("보스방/편의점/특수방/선택방 등 특정 카테고리나 variantId 전용 " +
             "벽/바닥 세트입니다. 매칭되는 세트가 있으면 그 세트를 쓰고, 없으면 위의 기본 " +
             "floorPool/wallPools(일반방 폴백)을 사용합니다. 이 세트 안의 wallPools도 위와 " +
             "동일하게 북<->서, 남<->동 회전 재사용 규칙을 따릅니다. " +
             "특수방 레이아웃에 외형 세트(SpecialRoomLayout > Visual Set)가 연결되어 있으면 그 세트가 여기보다 우선합니다(빈 칸만 여기로 폴백).")]
    public List<RoomWallSet> roomWallSets = new List<RoomWallSet>();

    [Header("이웃 특수방 파사드 (일반방이 특수방과 맞닿은 방향만 특수하게 보이기)")]
    [Tooltip("지금 짓는 방 자체가 위 roomWallSets에 매칭되지 않는(=사실상 일반방인) 경우에만 " +
             "적용됩니다. 이 방의 특정 방향 너머에 있는 이웃 방이 여기 등록된 category/variantId와 " +
             "일치하면, 그 방향의 벽만 여기서 지정한 파사드 풀로 대체됩니다. 예: 일반방 북쪽이 " +
             "편의점이면, 북쪽 벽만 '편의점 정면부처럼 보이는' 프리팹을 사용.\n" +
             "예외 - 특수방 입구: 이웃이 특수방이면 지금 짓는 방(부모 방)이 자기 세트를 갖고 있어도(시작방 등) 적용하며, " +
             "그 레이아웃 외형 세트의 '부모 방 쪽 입구 벽'이 채워져 있으면 그게 여기보다 우선합니다.")]
    public List<NeighborFacingWallSet> neighborFacingWallSets = new List<NeighborFacingWallSet>();

    [Header("특수방 진입 조건")]
    [Tooltip("Create > MobArchive > Special Room Entry Settings로 만든 공용 설정 에셋입니다(잠금 바리케이드 임시 큐브 색, 해제 이펙트, 체력 소모 방 피해량). " +
             "비워두면 기본값으로 동작합니다.")]
    public SpecialRoomEntrySettings specialRoomEntrySettings;

    [Tooltip("보스방 클리어 시 그 자리에 스폰할 사다리(포탈) 프리팹. LevelPortal 컴포넌트가 붙어있어야 합니다. " +
             "비워두면 기본 Capsule 플레이스홀더로 자동 생성됩니다.")]
    public GameObject levelPortalPrefab;

    [Header("플레이어")]
    [Tooltip("최초 스폰(existingPlayer가 없을 때)에 사용할 플레이어 프리팹. Tag가 'Player'로 설정되어 있어야 합니다.")]
    public GameObject playerPrefab;

    [Header("방 클리어 로직")]
    [Tooltip("IEnemySpawner를 구현한 컴포넌트를 연결하세요 (예: RealEnemySpawner). 나중에 스폰 로직을 바꾸려면 이 필드에 새 컴포넌트를 갈아 끼우기만 하면 됩니다.")]
    public MonoBehaviour enemySpawnerComponent;
    private IEnemySpawner enemySpawner;

    [Header("아이템")]
    [Tooltip("동아리방에 스폰할 보물상자 프리팹입니다. ItemPedestal 컴포넌트가 붙어있어야 합니다 " +
             "(없으면 자동으로 추가됩니다). 비워두면 기본 Cube 플레이스홀더로 자동 생성됩니다. " +
             "실제 프리팹을 연결할 때는 프리팹 자체에 미리 잡아둔 위치/회전이 그대로 유지되니 " +
             "(v1.13 변경 참고) 프리팹 원점을 기준으로 원하는 모양대로 미리 앉혀두면 됩니다.")]
    public GameObject itemPedestalPrefab;
    [Tooltip("프로젝트의 모든 ItemDefinition을 모아둔 데이터베이스입니다. 어떤 방에서 어떤 " +
             "아이템이 등장하는지는 이 필드가 아니라 각 ItemDefinition.appearanceContexts에서 " +
             "관리하므로, 새 특수방(카페/편의점 등)이 추가돼도 이 필드는 건드릴 필요가 없습니다.")]
    public ItemDatabase itemDatabase;
    [Tooltip("itemPedestalPrefab이 비어있어 플레이스홀더 보물상자를 자동 생성할 때 적용할 아이콘 " +
             "스케일입니다. 스프라이트 원본 해상도가 크면 칸 크기(cellSize)보다 훨씬 커 보일 " +
             "수 있으니 여기서 줄여주세요. 실제 itemPedestalPrefab을 만들어 연결하면 이 값 " +
             "대신 그 프리팹(ItemPedestal 컴포넌트)에 직접 설정한 값이 사용됩니다.")]
    public Vector3 itemIconScale = new Vector3(0.5f, 0.5f, 0.5f);
    [Tooltip("itemPedestalPrefab이 비어있을 때 적용할, 아이콘이 카메라 정면을 보도록 하는 " +
             "고정 회전값입니다. CameraFollow의 Fixed Euler Angles와 반드시 맞춰야 합니다 - " +
             "값이 다르면 아이콘이 비스듬하게 보입니다.")]
    public Vector3 itemIconFixedEulerAngles = new Vector3(55f, 45f, 0f);

    [Tooltip("[9번 음식 연동] 동아리방 보물상자를 복제할 때, 원본 상자 위치에서 이 거리만큼 " +
             "떨어뜨려 스폰합니다(방향은 매번 무작위). 너무 가까우면 두 상자가 겹쳐 보이거나, " +
             "상호작용 범위가 겹쳐 F 한 번에 두 상자가 동시에 반응할 수 있습니다 - 그런 " +
             "경우 이 값을 더 키워주세요.")]
    public float duplicatePedestalOffsetDistance = 1f;

    [Header("16번 방 추가 (런타임)")]
    [Tooltip("16번 아이템으로 동아리방이 생길 때 붙을 수 있는 부모 방 종류입니다(형빈 확정 - 시작방/일반방/동아리방). " +
             "비워 두면 이 기본값을 씁니다. 특수방은 여기가 아니라 Level Config의 Special Room Parent Categories를 따르고, " +
             "편의점은 따로 제한이 없습니다(보스방/특수방/전투 중인 방에는 어떤 방도 붙지 않음).")]
    public List<RoomCategory> extraClubParentCategories = new List<RoomCategory>
    {
        RoomCategory.Start, RoomCategory.Normal, RoomCategory.Club
    };
    [Tooltip("16번으로 방이 붙는 순간 플레이어가 그 부모 방의 새 벽(골목/파사드/잠금 바리케이드) 바로 앞에 서 있으면, 벽과 " +
             "겹치지 않을 만큼 방 안쪽으로 밀어냅니다. 그때 벽에서 더 띄울 여유 거리입니다(m).")]
    [Min(0f)] public float extraRoomWallPushMargin = 0.3f;
    [Tooltip("위 밀어내기에 걸리는 시간(초)입니다. 처음엔 빠르고 끝으로 갈수록 느려지는 짧은 넉백입니다(ForcedPush).")]
    [Min(0.01f)] public float extraRoomWallPushDuration = 0.18f;

    [Header("보물상자 (일반맵)")]
    [Tooltip("일반방 클리어(마지막 적 처치) 시 보물상자 스폰을 담당하는 컴포넌트입니다. " +
             "NormalRoomTreasureSpawner를 연결하세요. 비워두면 일반맵에 보물상자가 등장하지 " +
             "않습니다.")]
    public NormalRoomTreasureSpawner treasureSpawner;

    [Header("NavMesh (적 AI 이동용 - 방마다 런타임 베이크)")]
    [Tooltip("방을 다 지은 직후 그 방의 콘텐츠만 대상으로 NavMesh를 굽습니다. 바닥/벽/장애물 " +
             "프리팹이 매번 다르게 배치되므로 에디터에서 미리 구워둘 수 없어 런타임 베이크가 " +
             "필요합니다. 적 스폰 로직을 아직 쓰지 않는다면 꺼도 무방합니다.")]
    public bool bakeNavMeshPerRoom = true;
    [Tooltip("NavMesh를 구울 때 사용할 에이전트 타입 ID입니다 (Window > AI > Navigation > " +
             "Agents 탭에서 만든 프리셋 목록의 순서와 대응됩니다). 이 프리셋의 반지름이 " +
             "실제로 통행 가능한 폭을 결정합니다 - 골목처럼 좁은 통로에서 근접 적이 못 " +
             "들어오는 문제가 있다면, NavMeshAgent 개별 컴포넌트의 Radius가 아니라 바로 이 " +
             "베이크용 에이전트 타입의 반지름이 원인일 가능성이 높습니다. 골목 통로 폭보다 " +
             "충분히 작은 반지름을 가진 에이전트 타입을 새로 만들어 여기 연결해주세요 " +
             "(기본값 0은 보통 Humanoid 프리셋, 반지름 0.5입니다).")]
    public int navMeshAgentTypeID = 0;

    [Header("경계 안정성 (플레이어가 방 경계에서 떨어지는 것 방지)")]
    [Tooltip("바닥을 칸 크기보다 이만큼 더 크게 만들어 옆 방과 살짝 겹치게 합니다.")]
    public float floorOverlapMargin = 1f;
    [Tooltip("방 전환 판정 지점(=골목 정중앙, 두 방이 딱 절반씩 나뉘는 자리)에 여유를 " +
             "둡니다. 0이면 예전처럼 정중앙을 넘는 즉시 칸이 바뀌고, 값을 키우면 그만큼 " +
             "더 걸어 들어가야 칸이 바뀝니다(양쪽 방향 모두 대칭 적용). 예를 들어 골목을 " +
             "2/3쯤 걸어야 다음 방으로 넘어가게 하고 싶으면, Play 모드에서 이 값을 조금씩 " +
             "올려보면서 원하는 지점에서 맞춰주세요 - 별도 계산 없이 인스펙터에서 바로 " +
             "튜닝하면 됩니다. 부수 효과로 경계선 바로 위에서 칸이 파르르 떨리는 현상도 " +
             "함께 없어집니다.")]
    public float cellTransitionMargin = 0f;
    [Tooltip("방을 넘어갈 때(칸 전환 감지 시) 플레이어를 진입 방향으로 이만큼 미리 밀어넣은 " +
             "뒤에 그 방의 바리케이드를 잠급니다. 골목/바리케이드 프리팹의 두께보다 살짝 " +
             "크게 잡아서, 잠기는 시점에 플레이어가 확실히 바리케이드 안쪽에 서 있도록 " +
             "하세요. 너무 작으면 여전히 겹칠 수 있고, 너무 크면 화면이 순간 밀리는 " +
             "느낌이 들 수 있습니다. cellTransitionMargin을 충분히 키워서 전환 시점에 " +
             "이미 바리케이드 안쪽 깊숙이 들어와 있다면, 이 값을 0에 가깝게 줄이거나 " +
             "아예 0으로 둬도 됩니다.")]
    public float roomEntryPushDistance = 0.1f;

    [Header("동작 설정")]
    [Tooltip("Play 모드 진입 시 자동으로 레벨을 빌드할지 여부입니다. (항상 새 플레이어로 최초 스폰합니다)")]
    public bool buildOnStart = true;
    [Tooltip("buildOnStart가 켜져 있을 때 어떤 레벨을 자동으로 빌드할지 선택합니다.")]
    public LevelBuildMode buildModeOnStart = LevelBuildMode.Combat;

    private Transform levelRoot;
    private GameObject spawnedPlayer;
    private CharacterController spawnedPlayerController;
    private List<RoomInstance> generatedRooms;
    private RoomVisibilityManager visibilityManager;
    private Dictionary<int, RoomController> roomControllers = new Dictionary<int, RoomController>();
    private Dictionary<Vector2Int, RoomController> cellToController = new Dictionary<Vector2Int, RoomController>();
    private Vector2Int currentPlayerCell;
    private bool hasPlayerCell = false;
    private int levelBuildVersion = 0;

    // 런타임 방 추가용 상태입니다. 방마다/방향마다 "지금 서 있는 벽 오브젝트(닫힌 벽 또는
    // 골목 루트)"를 기억해둬야, 나중에 닫힌 벽을 골목으로 갈아 끼울 수 있습니다.
    private readonly Dictionary<int, Dictionary<Direction, GameObject>> wallRoots =
        new Dictionary<int, Dictionary<Direction, GameObject>>();
    // 지금 지어진 레벨이 전투 레벨(BuildLevel)인지 여부입니다. n.5 선택방 레벨에서는 false입니다.
    private bool currentLevelIsCombat = false;
    // 이번 빌드에서 이미 경고 확인을 마친 특수방 외형 세트입니다(같은 세트를 방마다 반복 확인하지 않도록).
    private readonly HashSet<RoomVisualSet> validatedVisualSets = new HashSet<RoomVisualSet>();

    // GameFlowManager가 레벨 전환 시 "유지해야 할 플레이어"를 가져갈 때 사용합니다.
    public GameObject CurrentPlayer => spawnedPlayer;

    // 미니맵(MinimapDisplay)이 방 목록/현재 칸을 읽을 때 사용하는 읽기 전용 접근자입니다.
    // generatedRooms가 아직 없으면(레벨 빌드 전) 빈 리스트를 반환해 null 체크를 줄여줍니다.
    public IReadOnlyList<RoomInstance> GeneratedRooms => (IReadOnlyList<RoomInstance>)generatedRooms ?? System.Array.Empty<RoomInstance>();
    public Vector2Int CurrentPlayerCell => currentPlayerCell;
    public bool HasPlayerCell => hasPlayerCell;

    // BuildRooms()가 호출될 때마다(레벨 최초 빌드 + 이후 모든 전환 시) 1씩 증가합니다.
    // 미니맵은 이 값이 바뀐 걸 감지하면 방 아이콘들을 통째로 다시 만듭니다.
    public int LevelBuildVersion => levelBuildVersion;

    // [v1.17 변경 - 미니맵 오버레이 아이콘용 접근자]
    // 미니맵이 "이 방의 콘텐츠 루트 밑에 지금 어떤 아이템/보상이 남아있는지" 스캔하려면
    // room.id로 그 방의 RoomController(그리고 그 안의 ContentRoot)를 찾을 수 있어야 합니다.
    // 기존 private 딕셔너리(roomControllers)는 그대로 두고 조회 전용 메서드만 추가했습니다.
    public RoomController GetRoomController(int roomId)
    {
        roomControllers.TryGetValue(roomId, out var controller);
        return controller;
    }

    void Start()
    {
        // 체력 소모 특수방 출입 피해(SpecialRoomHealthHazard)에 공용 설정을 넘깁니다(빌드 여부와 무관하게 먼저).
        SpecialRoomHealthHazard.Configure(specialRoomEntrySettings);

        if (!buildOnStart) return;

        switch (buildModeOnStart)
        {
            case LevelBuildMode.Combat:
                BuildLevel();
                break;
            case LevelBuildMode.ChoiceLevel:
                BuildChoiceLevel();
                break;
        }
    }

    void Update()
    {
        // 물리 트리거 대신, 매 프레임 플레이어의 월드 좌표를 칸 좌표로 환산해서
        // "지금 어느 방에 있는지"를 직접 계산합니다.
        if (spawnedPlayer == null) return;

        var pos = spawnedPlayer.transform.position;

        Vector2Int cell;
        if (!hasPlayerCell)
        {
            // 기준이 될 이전 칸이 아직 없는 최초 프레임에만 단순 반올림으로 초기화합니다.
            // (일반적으로는 SpawnPlayer()가 시작방 칸을 직접 미리 채워두므로 이 분기는
            // 거의 타지 않습니다.)
            cell = new Vector2Int(Mathf.RoundToInt(pos.x / cellSize), Mathf.RoundToInt(pos.z / cellSize));
        }
        else
        {
            // 히스테리시스 판정: 이전 칸을 기준으로, 그 칸 중심에서
            // (cellSize/2 + cellTransitionMargin)만큼 벗어나야만 옆 칸으로 넘어간 것으로
            // 인정합니다. margin이 0이면 예전과 동일하게 정중앙(골목 한가운데)에서 즉시
            // 전환되고, margin을 키우면 그만큼 더 걸어 들어가야 전환됩니다. 이전 칸을
            // 기준으로 판정하므로 자연스럽게 히스테리시스가 생겨 경계선 위에서 칸이
            // 파르르 떨리는 현상도 함께 방지됩니다.
            cell = new Vector2Int(
                ComputeAxisCellWithHysteresis(pos.x, currentPlayerCell.x),
                ComputeAxisCellWithHysteresis(pos.z, currentPlayerCell.y));
        }

        if (hasPlayerCell && cell == currentPlayerCell) return;

        Vector2Int previousCell = currentPlayerCell;
        bool hadPreviousCell = hasPlayerCell;

        currentPlayerCell = cell;
        hasPlayerCell = true;

        if (cellToController.TryGetValue(cell, out var controller))
        {
            // EnterRoom()이 바리케이드를 유예 없이 즉시 잠그므로, 그 전에 플레이어를
            // 진입 방향으로 먼저 밀어넣어 바리케이드 구간보다 안쪽에 세워둡니다.
            // (최초 스폰 직후의 시작방 진입은 hadPreviousCell이 false라 여기를 타지
            // 않고 SpawnPlayer()가 별도로 EnterRoom()을 호출합니다 - 그 경우는
            // 경계를 "넘은" 게 아니라 애초에 방 안에서 스폰되므로 밀어넣을 필요가
            // 없습니다.)
            if (hadPreviousCell)
            {
                PushPlayerForwardIntoRoom(previousCell, cell);
            }

            controller.EnterRoom();
        }
    }

    // fromCellIndex(이전에 있던 것으로 확정된 칸)를 기준으로, worldPos가 그 칸 중심에서
    // (cellSize/2 + cellTransitionMargin)보다 더 멀어졌을 때만 이웃 칸으로 넘어간 것으로
    // 판정합니다. 아직 그만큼 벗어나지 않았으면 fromCellIndex를 그대로 유지합니다(=아직
    // 이전 방에 있는 것으로 취급). 한 프레임에 두 칸 이상 건너뛰는 극단적인 상황(순간이동,
    // 매우 큰 프레임 드랍)은 고려하지 않습니다 - 기존 반올림 방식도 동일한 전제였습니다.
    private int ComputeAxisCellWithHysteresis(float worldPos, int fromCellIndex)
    {
        float distFromCurrentCenter = worldPos - (fromCellIndex * cellSize);
        float switchDistance = (cellSize / 2f) + cellTransitionMargin;

        if (distFromCurrentCenter > switchDistance) return fromCellIndex + 1;
        if (distFromCurrentCenter < -switchDistance) return fromCellIndex - 1;
        return fromCellIndex;
    }

    // 플레이어를 previousCell -> cell로 이동한 방향(진입 방향)으로
    // roomEntryPushDistance만큼 더 밀어냅니다. CharacterController.Move()를 쓰는
    // 이유는 transform.position 직접 텔레포트가 CharacterController의 다음 프레임
    // 충돌 처리와 어긋나 모서리 근처에서 튕기거나 파고드는 문제를 일으키기 쉽기
    // 때문입니다(PlayerMovement.ClampMaxHeight()와 같은 이유).
    private void PushPlayerForwardIntoRoom(Vector2Int previousCell, Vector2Int cell)
    {
        Vector2Int delta = cell - previousCell;
        if (delta == Vector2Int.zero) return; // 같은 칸 안에서는 밀어넣을 필요 없음

        Vector3 dir = new Vector3(delta.x, 0f, delta.y).normalized;
        Vector3 push = dir * roomEntryPushDistance;

        if (spawnedPlayerController != null && spawnedPlayerController.enabled)
        {
            spawnedPlayerController.Move(push);
        }
        else
        {
            spawnedPlayer.transform.position += push;
        }
    }

    // ---------- 진입점 1: 전투 레벨 ----------
    [ContextMenu("Build Level")]
    public void BuildLevel() => BuildLevel(null);

    // existingPlayer가 있으면 그 플레이어를 파괴하지 않고 새 시작방으로 이동시킵니다 (레벨 전환용).
    // null이면 playerPrefab으로 새로 스폰합니다 (최초 진입용).
    public void BuildLevel(GameObject existingPlayer)
    {
        if (!EnsurePlayMode()) return;

        if (config == null)
        {
            Debug.LogWarning("LevelBuilder: config가 비어있습니다. RoomGenerationConfig 에셋을 연결해주세요.");
            return;
        }

        var generator = new LevelGraphGenerator(config);
        var rooms = generator.Generate();

        if (rooms == null)
        {
            Debug.LogWarning("LevelBuilder: 레벨 생성에 실패했습니다.");
            return;
        }

        BuildRooms(rooms, $"레벨 {config.levelNumber}", existingPlayer, true);
    }

    // ---------- 진입점 2: n.5 선택방 레벨 ----------
    // GameFlowManager가 레벨 전환 시 호출하는 정식 진입점입니다 (기존 "ForTest" 이름에서 정리).
    [ContextMenu("Build Choice Level (n.5)")]
    public void BuildChoiceLevel() => BuildChoiceLevel(null);

    // existingPlayer가 있으면 그 플레이어에 붙은 PlayerStats의 확률을 사용합니다 (실제 전환과 동일한 방식).
    // existingPlayer가 없으면(독립 테스트) debugPlayerStatsForChoiceLevel의 확률을 사용합니다.
    public void BuildChoiceLevel(GameObject existingPlayer)
    {
        if (!EnsurePlayMode()) return;

        if (choiceLevelConfig == null)
        {
            Debug.LogWarning("LevelBuilder: choiceLevelConfig가 비어있습니다. ChoiceLevelConfig 에셋을 연결해주세요.");
            return;
        }

        PlayerStats statsSource = existingPlayer != null ? existingPlayer.GetComponent<PlayerStats>() : null;
        if (statsSource == null) statsSource = debugPlayerStatsForChoiceLevel;

        float westChance = 0f;
        float eastChance = 0f;
        if (statsSource != null)
        {
            westChance = statsSource.GematriaChance;
            eastChance = statsSource.SchaleChance;
        }
        else
        {
            Debug.LogWarning("LevelBuilder: 확률을 읽어올 PlayerStats가 없어 0으로 취급합니다. " +
                              "전환 중인 플레이어에 PlayerStats가 있는지, 혹은 debugPlayerStatsForChoiceLevel이 연결되어 있는지 확인해주세요.");
        }

        var generator = new ChoiceLevelGenerator(choiceLevelConfig);
        var rooms = generator.Generate(westChance, eastChance);

        BuildRooms(rooms, $"n.5 선택방 레벨 (서쪽 {westChance:P0} / 동쪽 {eastChance:P0})", existingPlayer, false);

        // n.5 레벨은 적/보스가 없어 "클리어" 개념이 없으므로, 서쪽/동쪽 방 등장 여부와
        // 무관하게 시작방에 항상 즉시 포탈을 심어서 플레이어가 바로 다음 레벨로 갈 수 있게 합니다.
        var startRoom = generatedRooms.Find(r => r.category == RoomCategory.Start);
        if (startRoom != null && roomControllers.TryGetValue(startRoom.id, out var startController))
        {
            startController.ForceSpawnPortalNow();
        }
        else
        {
            Debug.LogWarning("LevelBuilder: n.5 레벨 시작방을 찾지 못해 포탈을 심지 못했습니다.");
        }
    }

    // ---------- TEST 전용: GameFlowManager가 생기기 전, 전환 동작을 직접 검증하기 위한 메뉴 ----------
    [ContextMenu("TEST: Transition To Combat Level (Preserve Player)")]
    public void TestTransitionToCombatLevel()
    {
        if (!EnsurePlayMode()) return;
        if (spawnedPlayer == null)
        {
            Debug.LogWarning("LevelBuilder: 유지할 플레이어가 없습니다. 먼저 레벨을 하나 빌드해 플레이어를 스폰해주세요.");
            return;
        }
        BuildLevel(spawnedPlayer);
    }

    [ContextMenu("TEST: Transition To Choice Level (Preserve Player)")]
    public void TestTransitionToChoiceLevel()
    {
        if (!EnsurePlayMode()) return;
        if (spawnedPlayer == null)
        {
            Debug.LogWarning("LevelBuilder: 유지할 플레이어가 없습니다. 먼저 레벨을 하나 빌드해 플레이어를 스폰해주세요.");
            return;
        }
        BuildChoiceLevel(spawnedPlayer);
    }

    private bool EnsurePlayMode()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("LevelBuilder: 레벨 빌드는 Play 모드에서만 가능합니다. Play를 먼저 눌러주세요.");
            return false;
        }
        return true;
    }

    // ---------- 공통 빌드 로직 (어떤 생성기의 결과물이든 동일하게 처리) ----------
    private void BuildRooms(List<RoomInstance> rooms, string levelLabel, GameObject existingPlayer, bool isCombatLevel)
    {
        if (rooms == null || rooms.Count == 0)
        {
            Debug.LogWarning("LevelBuilder: 빌드할 방 데이터가 없습니다.");
            return;
        }

        ClearPreviousLevel(existingPlayer);

        ValidateWallPools();
        validatedVisualSets.Clear();

        enemySpawner = enemySpawnerComponent as IEnemySpawner;
        if (enemySpawnerComponent != null && enemySpawner == null)
        {
            Debug.LogWarning("LevelBuilder: enemySpawnerComponent가 IEnemySpawner를 구현하지 않았습니다.");
        }

        generatedRooms = rooms;
        currentLevelIsCombat = isCombatLevel;
        levelBuildVersion++;

        levelRoot = new GameObject("LevelRoot").transform;
        levelRoot.SetParent(transform);

        var visibilityGO = new GameObject("RoomVisibilityManager");
        visibilityGO.transform.SetParent(levelRoot);
        visibilityManager = visibilityGO.AddComponent<RoomVisibilityManager>();

        roomControllers.Clear();
        cellToController.Clear();
        wallRoots.Clear();
        foreach (var room in generatedRooms)
        {
            BuildRoom(room);
        }

        SpawnPlayer(existingPlayer);

        Debug.Log($"LevelBuilder: {levelLabel} 빌드 완료 (방 {generatedRooms.Count}개)");
    }

    // wallPools(기본 및 특수방 세트별)에 같은 방향이 두 번 이상 등록되어 있는지, 그리고
    // roomWallSets에 같은 매칭 기준(카테고리 또는 variantId)이 중복 등록되어 있는지 확인합니다.
    // GetPoolPrefabWithRotation()/FindMatchingWallSet()은 먼저 찾은 항목만 사용하므로, 중복은
    // 조용히 무시되어 실수를 알아채기 어렵습니다. 빌드할 때마다 확인해서 경고로 미리 알려줍니다.
    private void ValidateWallPools()
    {
        ValidateDirectionalPools(wallPools, "기본(일반방 폴백)");

        var seenSetKeys = new HashSet<string>();
        foreach (var set in roomWallSets)
        {
            string label = string.IsNullOrEmpty(set.variantId) ? $"카테고리 {set.category}" : $"variantId '{set.variantId}'";
            ValidateDirectionalPools(set.wallPools, label);

            string key = string.IsNullOrEmpty(set.variantId) ? $"category:{set.category}" : $"variant:{set.variantId}";
            if (!seenSetKeys.Add(key))
            {
                Debug.LogWarning($"LevelBuilder: roomWallSets에 동일한 매칭 기준({label})이 중복 등록되어 있습니다. " +
                                  "FindMatchingWallSet()은 먼저 찾은 항목만 사용하므로, 중복된 나머지 세트는 무시됩니다.");
            }
        }

        var seenFacingKeys = new HashSet<string>();
        foreach (var facing in neighborFacingWallSets)
        {
            string label = string.IsNullOrEmpty(facing.neighborVariantId)
                ? $"이웃 카테고리 {facing.neighborCategory}"
                : $"이웃 variantId '{facing.neighborVariantId}'";
            ValidateDirectionalPools(facing.wallPools, label);

            string key = string.IsNullOrEmpty(facing.neighborVariantId) ? $"category:{facing.neighborCategory}" : $"variant:{facing.neighborVariantId}";
            if (!seenFacingKeys.Add(key))
            {
                Debug.LogWarning($"LevelBuilder: neighborFacingWallSets에 동일한 매칭 기준({label})이 중복 등록되어 있습니다. " +
                                  "FindNeighborFacingWallSet()은 먼저 찾은 항목만 사용하므로, 중복된 나머지 세트는 무시됩니다.");
            }
        }
    }

    private void ValidateDirectionalPools(List<DirectionalWallPool> pools, string label)
    {
        var seen = new HashSet<Direction>();
        var duplicates = new HashSet<Direction>();

        foreach (var pool in pools)
        {
            if (!seen.Add(pool.direction))
            {
                duplicates.Add(pool.direction);
            }
        }

        if (duplicates.Count > 0)
        {
            Debug.LogWarning($"LevelBuilder: [{label}] wallPools에 같은 방향이 중복 등록되어 있습니다 ({string.Join(", ", duplicates)}). " +
                              "GetPoolPrefabWithRotation()은 먼저 찾은 항목만 사용하므로, 중복된 나머지 항목에 넣은 프리팹들은 무시됩니다. " +
                              "인스펙터에서 중복된 방향 항목을 하나로 합쳐주세요.");
        }
    }

    // ---------- 방 하나 배치 ----------
    private void BuildRoom(RoomInstance room)
    {
        var coord = room.occupiedCells[0];
        var worldPos = CellToWorld(coord);

        // 바닥/벽 풀은 칸마다 따로 아래 순서로 찾습니다(먼저 비어 있지 않은 것을 씀).
        // 1) 특수방 레이아웃의 외형 세트(SpecialRoomLayout > Visual Set)
        // 2) 이 방의 카테고리/variantId에 맞는 Room Wall Sets 항목
        // 3) 일반방 기본 폴백 풀(floorPool/wallPools)
        var visualSet = GetVisualSet(room);
        var matchedSet = FindMatchingWallSet(room);
        ValidateVisualSetOnce(visualSet, room);
        var floorPoolToUse = (visualSet != null && visualSet.floorPool.Count > 0) ? visualSet.floorPool
                           : (matchedSet != null && matchedSet.floorPool.Count > 0) ? matchedSet.floorPool
                           : floorPool;
        var wallPoolsToUse = GetOwnWallPools(visualSet, matchedSet);
        // 외형 세트나 Room Wall Sets 항목이 있는 방은 "자기 외형이 있는 방"이라 이웃 파사드로 바꾸지 않습니다(아래 함수 참고).
        bool hasOwnLook = visualSet != null || matchedSet != null;

        // variantId(특수방이면 레이아웃 ID)가 있으면 이름 끝에 붙여 Hierarchy에서 바로 알아볼 수 있게 합니다.
        // 특수방은 이 방에 뽑힌 진입 조건도 붙입니다(예: Room_5_Special_credit_pile_Grenade - 랜덤 조건 확인용).
        string roomObjectName = string.IsNullOrEmpty(room.variantId)
            ? $"Room_{room.id}_{room.category}"
            : $"Room_{room.id}_{room.category}_{room.variantId}";
        if (room.category == RoomCategory.Special && room.specialLayout != null)
        {
            roomObjectName += $"_{room.specialEntryCondition}";
        }
        var roomRootGO = new GameObject(roomObjectName);
        var roomRoot = roomRootGO.transform;
        roomRoot.SetParent(levelRoot);
        roomRoot.position = worldPos;

        var contentRootGO = new GameObject("Content");
        contentRootGO.transform.SetParent(roomRoot);
        contentRootGO.transform.localPosition = Vector3.zero;
        var contentRoot = contentRootGO.transform;

        BuildFloor(contentRoot, worldPos, floorPoolToUse);

        var controller = roomRootGO.AddComponent<RoomController>();
        // 전투 중 방을 벗어났다가 돌아왔을 때 방을 처음 상태로 되돌릴 수 있도록 적 스폰 담당자를
        // 넘겨줍니다. 이 기능을 지원하지 않는 스폰 담당자(DummyEnemySpawner 등)면 null이 전달되고,
        // 그 경우 방은 초기화되지 않고 예전처럼 동작합니다.
        controller.Setup(room, visibilityManager, contentRootGO, levelPortalPrefab, treasureSpawner, enemySpawner as IEnemyRespawner);
        visibilityManager.Register(contentRootGO);
        roomControllers[room.id] = controller;
        foreach (var cell in room.occupiedCells)
        {
            cellToController[cell] = controller;
        }

        var neighborsByDirection = GetNeighborsByDirection(room);
        foreach (var dir in DirectionUtil.All)
        {
            bool isOpen = neighborsByDirection.TryGetValue(dir, out var neighborRoom);

            // 기본은 이 방 자체의 풀을 쓰고, 특수방을 향한 부모 방 쪽 벽이면 그 레이아웃의 입구 벽으로, 이 방이 사실상
            // 일반방이면 이웃 파사드로 대체합니다(아래 PickWallPoolsForSegment 참고).
            var poolsForThisWall = PickWallPoolsForSegment(room, hasOwnLook, wallPoolsToUse, isOpen, neighborRoom);

            var segment = BuildWallSegment(contentRoot, worldPos, dir, isOpen, poolsForThisWall, out var wallRoot);
            RecordWallRoot(room.id, dir, wallRoot);
            if (isOpen && segment != null) controller.RegisterGate(segment);

            // 특수방 잠금 바리케이드: 이 벽이 "잠긴 특수방(수류탄/해킹툴)을 향한 부모 방 쪽 벽"이면 세우고, 아니면
            // 벽 프리팹에 들어 있는 잠금 바리케이드를 끕니다. NavMesh 굽기보다 먼저 처리되어야 합니다(아래 BakeRoomNavMesh).
            if (isOpen) ApplySpecialLockToWall(room, dir, neighborRoom, wallRoot, contentRoot, worldPos);
        }

        // 동아리방(Club)은 클리어 개념이 없는 무료 아이템 방입니다. 방을 다 지은 직후,
        // 다른 방(적/게이트)과 무관하게 보물상자를 하나 스폰합니다.
        if (room.category == RoomCategory.Club)
        {
            SpawnClubItemPedestal(room, contentRoot);
        }

        // 특수방: 배정된 레이아웃의 내용물을 놓습니다. 지금은 "플레이어 아이템 받침대 1개"만 있습니다.
        if (room.category == RoomCategory.Special && room.specialLayout != null && room.specialLayout.spawnItemPedestal)
        {
            SpawnSpecialItemPedestal(room, contentRoot);
        }

        // RoomVisibilityManager.Register()가 이 시점엔 이미 contentRoot를 비활성화해둔
        // 상태입니다(한 번에 한 방만 보이도록 하는 구조 때문에 등록 즉시 꺼짐). NavMesh
        // 지오메트리 수집(굽기)과 NavMesh.SamplePosition(적 스폰 위치 탐색) 둘 다 활성화된
        // 오브젝트/데이터를 필요로 하므로, 두 작업이 끝날 때까지만 잠깐 다시 켰다가 원래
        // 상태(꺼짐)로 되돌립니다.
        bool contentWasActive = contentRootGO.activeSelf;
        if (!contentWasActive) contentRootGO.SetActive(true);

        // 바닥/벽(및 장애물)이 전부 배치된 뒤에 이 방의 콘텐츠만 대상으로 NavMesh를 굽습니다.
        // 적 스폰(NavMesh.SamplePosition 사용)보다 반드시 먼저 실행되어야 합니다.
        BakeRoomNavMesh(contentRootGO);

        if (enemySpawner != null && (room.category == RoomCategory.Normal || room.category == RoomCategory.Boss))
        {
            enemySpawner.SpawnEnemies(room, contentRoot);
        }

        if (!contentWasActive) contentRootGO.SetActive(false);
    }

    // 동아리방에 아이템 보물상자를 하나 스폰합니다. 이 방의 컨텍스트 키(ItemDatabase.GetContextKey)와
    // 일치하는 appearanceContexts를 가진 아이템만 itemDatabase에서 걸러 풀로 넘깁니다. 실제
    // 어떤 아이템이 뽑힐지는 ItemPedestal이 스스로 PlayerInventory를 찾은 시점에 결정하므로
    // (방을 지을 당시엔 플레이어가 아직 없을 수 있음), 여기서는 풀만 주입하고 위치만 잡아줍니다.
    //
    // [v1.13 변경 - Instantiate(prefab, position, rotation, parent) -> Instantiate(prefab, parent)]
    // 이전 방식은 항상 contentRoot 위치 + 회전 없음(Quaternion.identity)을 강제로 덮어써서,
    // 실제 3D 보물상자 프리팹을 만들어 연결했을 때 프리팹 자체에 미리 잡아둔 위치/회전이 전부
    // 무시되는 문제가 있었습니다(상자가 땅에 박히고 전면부가 하늘을 보는 등). 프리팹을 그
    // 프리팹이 원래 갖고 있던 로컬 위치/회전 그대로 배치하도록 Instantiate(prefab, parent)로
    // 바꿨습니다 - 즉 이제 보물상자의 실제 위치/회전은 프리팹 원본 자체를 에디터에서 어떻게
    // 앉혀뒀는지에 좌우됩니다(프리팹을 만들 때 원점 기준으로 원하는 모양대로 미리 배치해둘 것).
    // 플레이스홀더(Cube) 경로는 기존과 동일하게 명시적 위치를 그대로 사용합니다.
    private void SpawnClubItemPedestal(RoomInstance room, Transform contentRoot)
    {
        if (itemDatabase == null)
        {
            Debug.LogWarning("LevelBuilder: itemDatabase가 비어있어 동아리방에 아이템을 스폰하지 않았습니다.");
            return;
        }

        string contextKey = ItemDatabase.GetContextKey(room);
        // v1.24: 동아리방 보물상자는 Passive + Active를 합친 풀에서 뽑습니다(그 보물상자 자체의
        // TryResolveItem()이 FilterUnowned/FilterOutEquipped로 다시 걸러냅니다).
        // v1.26: 여기에 Trinket도 추가로 합칩니다.
        // v1.29: 여기에 Disposable도 추가로 합칩니다.
        // v1.34: 여기에 Food도 추가로 합칩니다.
        var pool = itemDatabase.GetItemsForContext(contextKey)
            .Concat(itemDatabase.GetItemsForContext(contextKey, ItemDefinition.ItemCategory.Active))
            .Concat(itemDatabase.GetItemsForContext(contextKey, ItemDefinition.ItemCategory.Trinket))
            .Concat(itemDatabase.GetItemsForContext(contextKey, ItemDefinition.ItemCategory.Disposable))
            .Concat(itemDatabase.GetItemsForContext(contextKey, ItemDefinition.ItemCategory.Food))
            .ToList();
        if (pool.Count == 0)
        {
            Debug.LogWarning($"LevelBuilder: itemDatabase에 컨텍스트 '{contextKey}'로 등장 가능한 아이템이 없어 " +
                              $"동아리방에 아이템을 스폰하지 않았습니다. ItemDefinition.appearanceContexts에 " +
                              $"'{contextKey}'를 추가한 아이템이 있는지 확인해주세요.");
            return;
        }

        CreateItemPedestal(contentRoot, pool, true);
    }

    // [특수방 통합] 특수방 레이아웃의 "플레이어 아이템 받침대" 내용물입니다. 동아리방과 같은 보물상자 프리팹/
    // 생성 방식(CreateItemPedestal)을 쓰고, 후보 풀만 특수방 규칙(ItemDatabase.GetAllCategoryItemsForRoom - 이
    // 레이아웃 전용 아이템 + 레이아웃이 허용하면 특수방 공용 아이템)으로 고릅니다.
    //
    // [동아리방과 다른 점]
    // - 동아리방과 달리 후보가 0개여도 받침대를 놓습니다. 열면 ItemPedestal이 기본 아이템으로 대체합니다(받침대가
    //   통째로 없으면 레이아웃이 비어 보이기 때문). 후보가 없다는 경고는 로그로 남깁니다.
    // - isClubPedestal = false라서 동아리방 전용 효과(27번 등)는 적용되지 않습니다(ItemPedestal 상단 주석 참고).
    private void SpawnSpecialItemPedestal(RoomInstance room, Transform contentRoot)
    {
        if (itemDatabase == null)
        {
            Debug.LogWarning("LevelBuilder: itemDatabase가 비어있어 특수방에 아이템 받침대를 놓지 않았습니다.");
            return;
        }

        var layout = room.specialLayout;
        var pool = itemDatabase.GetAllCategoryItemsForRoom(room, layout.includeSharedSpecialItems);
        if (pool.Count == 0)
        {
            Debug.LogWarning($"LevelBuilder: 특수방 레이아웃 '{layout.layoutId}'에 등장 가능한 아이템이 없습니다 " +
                             "(받침대를 열면 기본 아이템으로 대체됩니다). 아이템 에셋의 Appearance Contexts에 " +
                             $"Special + '{layout.layoutId}' 또는 Special + (카테고리 전체)가 있는지 확인해주세요.");
        }

        var pedestal = CreateItemPedestal(contentRoot, pool, false);
        if (pedestal != null) pedestal.gameObject.name = $"SpecialItemPedestal_{layout.layoutId}";
    }

    // ---------- 특수방 잠금 바리케이드 ----------

    // room의 dir 방향 열린 벽(wallRoot)에 특수방 잠금 바리케이드를 세우거나 끕니다.
    // - 세우는 경우: room이 특수방이 아니고, 그 방향 이웃(neighbor)이 수류탄/해킹툴 진입 조건의 특수방일 때
    //   (= 잠긴 특수방을 향한 부모 방 쪽 벽). 벽 프리팹의 WallSidePrefab.specialLockBarricade를 쓰고, 없으면 임시 큐브를
    //   세웁니다. 그 오브젝트에 SpecialRoomLock을 붙여 초기화합니다.
    // - 그 외(일반 연결, 그냥 진입/체력 소모 특수방, 특수방 자신의 벽): 벽 프리팹에 잠금 바리케이드가 들어 있으면 끕니다.
    // 잠금 바리케이드는 전투 바리케이드(RoomController 게이트)와 별개 오브젝트라 방 클리어로는 열리지 않습니다.
    //
    // [진입 조건별 바리케이드 모델 (형빈 확정 안 1)]
    // Special Room Entry Settings의 수류탄/해킹툴 칸에 Lock Barricade Prefab이 들어 있으면, 그 방에 뽑힌 진입 조건에 맞는
    // 프리팹을 세웁니다(CreateConditionLockBarricade). 벽 프리팹은 조건과 상관없이 하나만 쓰면 되고, 벽 프리팹 안의 Special
    // Lock Barricade 오브젝트는 "세울 위치/방향"으로만 쓰인 뒤 꺼집니다. 조건 칸의 프리팹이 비어 있으면 예전처럼 벽 프리팹의
    // 잠금 바리케이드(없으면 조건 색 임시 큐브)를 그대로 씁니다.
    // 세운 잠금 바리케이드 오브젝트를 돌려줍니다(세우지 않았으면 null - 16번 방 추가 때 등장 연출/밀어내기에 씀).
    //
    // [체력 소모 특수방 표시 장식 (형빈 확정)]
    // 체력 소모 특수방을 향한 부모 방 쪽 벽에는 잠금 대신 표시용 장식(Special Room Entry Settings > Health Marker Prefab,
    // 예: 가시철창)을 잠금 바리케이드와 같은 자리/방향에 세웁니다(TryCreateHealthMarker). 잠금이 아니라서 SpecialRoomLock을
    // 붙이지 않고 사라지지도 않으며(37번 보유 중에도 그대로), 콜라이더는 전부 꺼서 플레이어/총알이 통과합니다. 이 경우에도
    // 세운 장식을 돌려줘서 16번 방 추가 때 벽과 함께 떠오르는 연출을 받습니다(콜라이더가 없으므로 밀어내기는 일어나지 않음).
    private GameObject ApplySpecialLockToWall(RoomInstance room, Direction dir, RoomInstance neighbor, GameObject wallRoot,
                                              Transform contentRoot, Vector3 cellWorldPos)
    {
        GameObject prefabLock = GetValidPrefabLockBarricade(wallRoot);

        bool needsLock = room != null && room.category != RoomCategory.Special &&
                         neighbor != null && neighbor.category == RoomCategory.Special &&
                         neighbor.specialLayout != null &&
                         SpecialRoomEntrySettings.NeedsLockBarricade(neighbor.specialEntryCondition);

        if (!needsLock)
        {
            // 체력 소모 특수방이면 잠금 바리케이드 자리를 빌려 표시 장식을 세웁니다(자리 오브젝트를 끄기 전에 위치를 읽어야 함).
            GameObject healthMarker = TryCreateHealthMarker(room, neighbor, prefabLock, contentRoot, cellWorldPos, dir);
            if (prefabLock != null) prefabLock.SetActive(false);
            return healthMarker;
        }

        // 레이아웃이 아니라 그 특수방에 뽑힌 진입 조건입니다(레이아웃의 진입 조건이 랜덤일 수 있음).
        var condition = neighbor.specialEntryCondition;
        var lockSettings = specialRoomEntrySettings != null ? specialRoomEntrySettings.GetLockSettings(condition) : null;
        GameObject conditionPrefab = lockSettings != null ? lockSettings.lockBarricadePrefab : null;

        GameObject lockGO;
        if (conditionPrefab != null)
        {
            lockGO = CreateConditionLockBarricade(conditionPrefab, prefabLock, contentRoot, cellWorldPos, dir, condition);
            if (prefabLock != null) prefabLock.SetActive(false); // 자리만 빌려주고 꺼집니다.
        }
        else
        {
            lockGO = prefabLock != null ? prefabLock : CreateSpecialLockPlaceholder(contentRoot, cellWorldPos, dir, condition);
        }
        lockGO.SetActive(true);

        var lockComponent = lockGO.GetComponent<SpecialRoomLock>();
        if (lockComponent == null) lockComponent = lockGO.AddComponent<SpecialRoomLock>();
        lockComponent.Initialize(condition, neighbor, room, specialRoomEntrySettings);
        return lockGO;
    }

    // 진입 조건별 잠금 바리케이드 프리팹을 세웁니다.
    // - 벽 프리팹에 Special Lock Barricade 오브젝트(slot)가 있으면: 그 오브젝트와 같은 부모 밑에, 같은 위치/방향으로 세웁니다
    //   (크기는 프리팹 그대로). 그래서 벽 프리팹 안의 Special Lock Barricade를 Project 창으로 끌어다 프리팹으로 만든 뒤 모양만
    //   바꾸면 그대로 같은 자리에 맞습니다. 레이어가 기본(Default)인 부분은 slot의 레이어로 맞춥니다.
    // - slot이 없으면: 임시 큐브와 같은 자리(벽에서 벽 두께만큼 방 안쪽)에, 북쪽 벽 기준으로 만든 프리팹을 벽 방향만큼 돌려서
    //   세우고 바닥에 맞춥니다.
    // 총알이 막히고 수류탄/해킹툴 범위 판정에 걸리도록 콜라이더가 필요합니다. 콜라이더가 하나도 없으면 메시마다 BoxCollider를
    // 붙이고 경고를 남깁니다. 태그가 없는(Untagged) 콜라이더는 "Wall" 태그를 붙입니다(임시 큐브/전투 바리케이드와 같은 규칙).
    private GameObject CreateConditionLockBarricade(GameObject prefab, GameObject slot, Transform contentRoot, Vector3 cellWorldPos,
                                                    Direction dir, SpecialRoomEntryCondition condition)
    {
        GameObject go = InstantiateAtSpecialLockSlot(prefab, slot, contentRoot, cellWorldPos, dir);
        go.name = $"SpecialLock_{dir}_{condition}";

        var colliders = go.GetComponentsInChildren<Collider>(true);
        if (colliders.Length == 0)
        {
            Debug.LogWarning($"LevelBuilder: 잠금 바리케이드 프리팹 '{prefab.name}'에 콜라이더가 없어 메시마다 BoxCollider를 붙였습니다. " +
                             "플레이어/총알을 막는 모양을 정확히 하려면 프리팹에 직접 콜라이더를 넣어주세요.");
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
            {
                mf.gameObject.AddComponent<BoxCollider>();
            }
            colliders = go.GetComponentsInChildren<Collider>(true);
        }
        foreach (var col in colliders)
        {
            if (col.gameObject.CompareTag("Untagged")) col.gameObject.tag = "Wall";
        }

        return go;
    }

    // 특수방 잠금 바리케이드 자리에 prefab을 세웁니다(조건별 잠금 바리케이드와 체력 소모 표시 장식이 같이 씀).
    // - slot(벽 프리팹 안의 Special Lock Barricade)이 있으면: 그 오브젝트와 같은 부모 밑에, 같은 위치/방향으로 세웁니다
    //   (크기는 프리팹 그대로). 레이어가 기본(Default)인 부분은 slot의 레이어로 맞춥니다.
    // - slot이 없으면: 벽에서 벽 두께만큼 방 안쪽 자리에, 북쪽 벽 기준으로 만든 프리팹을 벽 방향만큼 돌려서 세우고 바닥에 맞춥니다.
    private GameObject InstantiateAtSpecialLockSlot(GameObject prefab, GameObject slot, Transform contentRoot, Vector3 cellWorldPos,
                                                    Direction dir)
    {
        Transform parent = slot != null && slot.transform.parent != null ? slot.transform.parent : contentRoot;
        GameObject go = Instantiate(prefab, parent);

        if (slot != null)
        {
            go.transform.SetPositionAndRotation(slot.transform.position, slot.transform.rotation);

            foreach (var t in go.GetComponentsInChildren<Transform>(true))
            {
                if (t.gameObject.layer == 0) t.gameObject.layer = slot.layer;
            }
        }
        else
        {
            Vector3 edgeOffset = GetWallOffset(dir);
            Vector3 inward = -edgeOffset.normalized * wallThickness;
            float yaw = GetCanonicalYaw(dir);
            if (invertWallRotationDirection) yaw = -yaw;
            go.transform.SetPositionAndRotation(cellWorldPos + edgeOffset + inward,
                                                Quaternion.Euler(0f, yaw, 0f) * prefab.transform.rotation);
            SnapBottomToHeight(go, cellWorldPos.y);
        }

        return go;
    }

    // room의 dir 방향 이웃(neighbor)이 체력 소모 특수방이면, 잠금 바리케이드 자리(slot)에 표시 장식(Health Marker Prefab -
    // 가시철창 등)을 세웁니다. 해당하지 않거나 프리팹이 비어 있으면 아무것도 세우지 않고 null을 돌려줍니다(예전과 같음).
    // 장식은 골목을 거의 막는 모양일 수 있으므로 프리팹에 들어 있는 콜라이더를 전부 꺼서 플레이어/총알이 반드시 통과하게
    // 합니다(지나가며 닿아서 피해를 입는 것처럼 보이게 - 형빈 확정). 방 NavMesh는 물리 콜라이더로 굽기 때문에 길찾기에도
    // 영향이 없습니다. 잠금이 아니므로 SpecialRoomLock은 붙이지 않습니다.
    private GameObject TryCreateHealthMarker(RoomInstance room, RoomInstance neighbor, GameObject slot, Transform contentRoot,
                                             Vector3 cellWorldPos, Direction dir)
    {
        bool isHealthParentWall = room != null && room.category != RoomCategory.Special &&
                                  neighbor != null && neighbor.category == RoomCategory.Special &&
                                  neighbor.specialLayout != null &&
                                  neighbor.specialEntryCondition == SpecialRoomEntryCondition.Health;
        if (!isHealthParentWall) return null;

        GameObject prefab = specialRoomEntrySettings != null ? specialRoomEntrySettings.healthMarkerPrefab : null;
        if (prefab == null) return null;

        GameObject go = InstantiateAtSpecialLockSlot(prefab, slot, contentRoot, cellWorldPos, dir);
        go.name = $"SpecialHealthMarker_{dir}";

        foreach (var col in go.GetComponentsInChildren<Collider>(true))
        {
            col.enabled = false;
        }

        go.SetActive(true);
        return go;
    }

    // 렌더러 전체의 가장 아래쪽이 floorY에 닿도록 오브젝트를 위아래로 옮깁니다(피벗 위치와 무관하게 바닥에 놓기).
    private static void SnapBottomToHeight(GameObject go, float floorY)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        Bounds combined = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) combined.Encapsulate(renderers[i].bounds);

        float offset = floorY - combined.min.y;
        if (Mathf.Abs(offset) > 0.0001f) go.transform.position += new Vector3(0f, offset, 0f);
    }

    // 벽 프리팹의 WallSidePrefab.specialLockBarricade를 꺼냅니다. 연결이 잘못되어 있으면(전투 바리케이드와 같은 오브젝트,
    // 또는 전투 바리케이드의 자식 - 전투 바리케이드와 함께 꺼져 버림) 경고를 남기고 null을 돌려줘 임시 큐브를 쓰게 합니다.
    private static GameObject GetValidPrefabLockBarricade(GameObject wallRoot)
    {
        if (wallRoot == null) return null;
        var side = wallRoot.GetComponent<WallSidePrefab>();
        if (side == null || side.specialLockBarricade == null) return null;

        var lockGO = side.specialLockBarricade;
        if (side.barricade != null &&
            (lockGO == side.barricade || lockGO.transform.IsChildOf(side.barricade.transform)))
        {
            Debug.LogWarning($"LevelBuilder: '{wallRoot.name}'의 Special Lock Barricade가 Barricade와 같은 오브젝트이거나 그 자식입니다. " +
                             "두 바리케이드는 서로 다른 형제 오브젝트여야 합니다. 이 벽에서는 임시 큐브를 대신 세웁니다.");
            lockGO.SetActive(false);
            return null;
        }
        return lockGO;
    }

    // 벽 프리팹에 잠금 바리케이드가 없을 때 대신 세우는 임시 큐브입니다(빈 자리를 눈에 보이게 남기는 원칙).
    // 전투 바리케이드 임시 큐브(Gate_*_Placeholder - 칸 경계선 위)보다 벽 두께만큼 방 안쪽에 세웁니다. 칸 경계선을 넘으면
    // 플레이어가 옆 방(특수방)에 들어간 것으로 처리되어 이 바리케이드가 플레이어를 막지 못하므로, 반드시 방 안쪽이어야 합니다.
    private GameObject CreateSpecialLockPlaceholder(Transform contentRoot, Vector3 cellWorldPos, Direction dir, SpecialRoomEntryCondition condition)
    {
        bool isNorthSouth = (dir == Direction.North || dir == Direction.South);
        Vector3 edgeOffset = GetWallOffset(dir);
        Vector3 inward = -edgeOffset.normalized * wallThickness;

        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = $"SpecialLock_{dir}_{condition}_Placeholder";
        go.transform.SetParent(contentRoot);
        go.transform.position = cellWorldPos + edgeOffset + inward + new Vector3(0f, wallHeight / 2f, 0f);
        go.transform.rotation = Quaternion.identity;
        go.transform.localScale = isNorthSouth
            ? new Vector3(cellSize, wallHeight, wallThickness)
            : new Vector3(wallThickness, wallHeight, cellSize);
        go.tag = "Wall"; // 총알이 막히도록(바리케이드와 같은 규칙)

        var lockSettings = specialRoomEntrySettings != null ? specialRoomEntrySettings.GetLockSettings(condition) : null;
        Color color = lockSettings != null ? lockSettings.placeholderColor : SpecialRoomEntrySettings.DefaultPlaceholderColor(condition);
        var rend = go.GetComponent<Renderer>();
        if (rend != null) rend.material.color = color;

        return go;
    }

    // 동아리방/특수방 공용 - 보물상자(ItemPedestal)를 contentRoot 밑에 만들고 풀을 주입합니다. 원래
    // SpawnClubItemPedestal 안에 있던 생성 부분을 그대로 옮긴 것이라 동아리방 동작은 바뀌지 않습니다.
    // isClubPedestal은 동아리방 전용 효과(27번 등)를 적용할지입니다.
    private ItemPedestal CreateItemPedestal(Transform contentRoot, List<ItemDefinition> pool, bool isClubPedestal)
    {
        GameObject pedestalGO;
        bool usingPrefab = itemPedestalPrefab != null;
        if (usingPrefab)
        {
            // 프리팹이 원래 갖고 있던 로컬 위치/회전을 그대로 보존합니다(v1.13 변경 참고).
            pedestalGO = Instantiate(itemPedestalPrefab, contentRoot);
        }
        else
        {
            pedestalGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pedestalGO.name = "ItemPedestal_Placeholder";
            pedestalGO.transform.SetParent(contentRoot);
            pedestalGO.transform.position = contentRoot.position + Vector3.up * 0.4f;
            pedestalGO.transform.localScale = new Vector3(0.8f, 0.8f, 0.8f);

            var col = pedestalGO.GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        var pedestal = pedestalGO.GetComponent<ItemPedestal>();
        if (pedestal == null) pedestal = pedestalGO.AddComponent<ItemPedestal>();

        // itemPedestalPrefab을 아직 안 만든 상태(플레이스홀더)라면, 인스펙터에서 바로
        // 튜닝할 수 있도록 여기서 스케일/회전값을 덮어씁니다. 실제 프리팹을 만들어
        // 연결하면 그 프리팹 자체에 설정된 값이 이미 있을 테니 여기서 건드리지 않습니다.
        // (GameObject가 아직 비활성 계층 안에 있어 Awake가 지연된 상태이므로, Awake가
        // 실제로 실행되기 전에 필드값만 먼저 세팅해두는 방식입니다.)
        if (!usingPrefab)
        {
            pedestal.iconScale = itemIconScale;
            pedestal.iconFixedEulerAngles = itemIconFixedEulerAngles;
        }

        pedestal.itemPool = pool;
        pedestal.isClubPedestal = isClubPedestal;

        // v1.30: 동아리방 보물상자도 "보물상자"이므로, 뽑을 아이템이 하나도 없을 때의 대체 기본 아이템
        // 풀을 NormalRoomTreasureSpawner(treasureSpawner)의 풀과 공유합니다 - 별도로
        // 중복 설정할 필요가 없습니다. treasureSpawner가 비어있으면 빈 풀을 그대로 두고,
        // ItemPedestal.AssignFallbackBasicItems()가 그 경우를 경고 로그로 알립니다.
        if (treasureSpawner != null)
        {
            pedestal.basicItemBundlePool = treasureSpawner.basicItemBundlePool;
        }

        return pedestal;
    }

    // [9번 음식 연동] 동아리방 보물상자를 복제합니다. SpawnClubItemPedestal()과 완전히 동일한 풀
    // 구성 로직(itemDatabase 기반 pool + treasureSpawner.basicItemBundlePool)을 그대로 재사용하되,
    // 이건 방을 지을 때 딱 한 번 호출되는 원래 메서드와 달리 "이미 있는 상자를 하나 더" 만드는
    // 별도 진입점입니다 - 확률 판정이 없고, 기존 상자 위치 근처(nearPosition)에 배치됩니다.
    // 새로 만든 상자는 아직 열리지 않은 상태이며, 내용물은 itemPool에서 F로 열 때 매번 새로
    // 뽑히므로(ItemPedestal.Open() 참고) 원본 상자와 다를 수 있습니다.
    public void SpawnDuplicateClubPedestal(RoomInstance room, Transform contentRoot, Vector3 nearPosition)
    {
        if (itemDatabase == null)
        {
            Debug.LogWarning("LevelBuilder: itemDatabase가 비어있어 동아리방 보물상자를 복제하지 못했습니다.");
            return;
        }

        string contextKey = ItemDatabase.GetContextKey(room);
        var pool = itemDatabase.GetItemsForContext(contextKey)
            .Concat(itemDatabase.GetItemsForContext(contextKey, ItemDefinition.ItemCategory.Active))
            .Concat(itemDatabase.GetItemsForContext(contextKey, ItemDefinition.ItemCategory.Trinket))
            .Concat(itemDatabase.GetItemsForContext(contextKey, ItemDefinition.ItemCategory.Disposable))
            .Concat(itemDatabase.GetItemsForContext(contextKey, ItemDefinition.ItemCategory.Food))
            .ToList();
        if (pool.Count == 0)
        {
            Debug.LogWarning($"LevelBuilder: itemDatabase에 컨텍스트 '{contextKey}'로 등장 가능한 아이템이 없어 " +
                              $"동아리방 보물상자를 복제하지 못했습니다.");
            return;
        }

        SpawnDuplicatePedestalNear(contentRoot, pool, nearPosition, true);
    }

    // [9번 음식 연동 - 특수방] 특수방의 플레이어 아이템 받침대를 복제합니다. 후보 풀은 SpawnSpecialItemPedestal과
    // 같은 특수방 규칙(이 레이아웃 전용 + 허용 시 특수방 공용)이고, 동아리방 전용 효과(27번 등)는 적용되지 않습니다
    // (isClubPedestal = false). 원래 받침대처럼 후보가 0개여도 놓습니다(열면 기본 아이템으로 대체).
    // 이 레이아웃에서 9번을 쓸 수 있는지(SpecialRoomLayout.allowChestDuplication)는 호출하는 쪽(Item09effect)이 확인합니다.
    public void SpawnDuplicateSpecialPedestal(RoomInstance room, Transform contentRoot, Vector3 nearPosition)
    {
        if (itemDatabase == null)
        {
            Debug.LogWarning("LevelBuilder: itemDatabase가 비어있어 특수방 아이템 받침대를 복제하지 못했습니다.");
            return;
        }
        if (room == null || room.specialLayout == null) return;

        var pool = itemDatabase.GetAllCategoryItemsForRoom(room, room.specialLayout.includeSharedSpecialItems);
        SpawnDuplicatePedestalNear(contentRoot, pool, nearPosition, false);
    }

    // 동아리방/특수방 받침대 복제 공용 - nearPosition에서 duplicatePedestalOffsetDistance만큼 떨어진 곳에 미개봉 받침대를
    // 하나 만들고 풀을 주입합니다. 원래 SpawnDuplicateClubPedestal 안에 있던 부분을 그대로 옮긴 것이라 동아리방 동작은
    // 바뀌지 않습니다. isClubPedestal은 동아리방 전용 효과(27번 등)를 적용할지입니다.
    private void SpawnDuplicatePedestalNear(Transform contentRoot, List<ItemDefinition> pool, Vector3 nearPosition, bool isClubPedestal)
    {
        // 원본 상자와 완전히 겹치면(모델도 겹쳐 보이고, 상호작용 범위도 겹쳐 F 한 번에 두 
        // 상자가 동시에 반응할 수 있음) 곤란하므로, 무작위 방향으로 duplicatePedestalOffsetDistance
        // 만큼 떨어뜨린 위치에 스폰합니다.
        float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        Vector3 offsetPosition = nearPosition + new Vector3(
            Mathf.Cos(angle) * duplicatePedestalOffsetDistance,
            0f,
            Mathf.Sin(angle) * duplicatePedestalOffsetDistance);

        GameObject pedestalGO;
        bool usingPrefab = itemPedestalPrefab != null;
        if (usingPrefab)
        {
            pedestalGO = Instantiate(itemPedestalPrefab, contentRoot);
            // 프리팹 원본의 로컬 회전/스케일은 그대로 보존하고, 위치만 원본 상자에서 살짝 떨어뜨립니다.
            pedestalGO.transform.position = offsetPosition;
        }
        else
        {
            pedestalGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pedestalGO.name = "ItemPedestal_Placeholder";
            pedestalGO.transform.SetParent(contentRoot);
            pedestalGO.transform.position = offsetPosition;
            pedestalGO.transform.localScale = new Vector3(0.8f, 0.8f, 0.8f);

            var col = pedestalGO.GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        // 원래 동아리방 상자는 방을 지을 때 이미 그 자리에 있던 것이라 등장 연출이 필요
        // 없었지만, 이건 플레이 도중 눈앞에 갑자기 생겨나는 것이라 "뿅" 나타나면 어색합니다.
        // 일반방/보스방 보물상자(NormalRoomTreasureSpawner.SpawnChestObject)와 동일하게
        // 흰색으로 빛나다가 서서히 원래 색으로 돌아오는 연출을 적용합니다(스프라이트 아이콘은
        // AppearFromWhite가 알아서 건드리지 않으므로, 나중에 Open()으로 아이콘이 생겨도 무관합니다).
        AppearFromWhite.Begin(pedestalGO);

        var pedestal = pedestalGO.GetComponent<ItemPedestal>();
        if (pedestal == null) pedestal = pedestalGO.AddComponent<ItemPedestal>();

        if (!usingPrefab)
        {
            pedestal.iconScale = itemIconScale;
            pedestal.iconFixedEulerAngles = itemIconFixedEulerAngles;
        }

        pedestal.itemPool = pool;
        pedestal.isClubPedestal = isClubPedestal;

        if (treasureSpawner != null)
        {
            pedestal.basicItemBundlePool = treasureSpawner.basicItemBundlePool;
        }
    }

    // 이 방의 Content 루트 밑에 있는 오브젝트(바닥/벽/장애물)만 대상으로 NavMesh를
    // 런타임으로 굽습니다. 바닥 프리팹이 방마다 랜덤으로 선택되고 그 안의 장애물 배치도
    // 매번 달라지므로, 에디터에서 미리 구워둔 정적 NavMesh는 쓸 수 없습니다.
    private void BakeRoomNavMesh(GameObject contentRoot)
    {
        if (!bakeNavMeshPerRoom) return;

        // useGeometry가 PhysicsColliders라 물리 엔진의 콜라이더 정보를 직접 읽어서 굽습니다.
        // 방금 Instantiate/활성화한 콜라이더가 물리 엔진에 아직 반영되지 않았을 수 있어
        // 강제로 동기화한 뒤 굽습니다.
        Physics.SyncTransforms();

        var surface = contentRoot.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.Children;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.agentTypeID = navMeshAgentTypeID;
        surface.BuildNavMesh();

        // 활성/비활성 전환은 여기서 하지 않습니다 - NavMeshSurface는 자기 오브젝트가
        // 꺼지는 순간 방금 구운 데이터를 자동으로 제거(OnDisable -> RemoveData)합니다.
        // 이 함수 끝에서 바로 꺼버리면 적 스폰(NavMesh.SamplePosition)이 실행되기도 전에
        // 데이터가 사라져버리므로, 활성화 상태 복구는 호출부(BuildRoom)가 적 스폰까지
        // 끝난 뒤에 처리합니다.
    }

    // room의 category(또는 variantId)에 맞는 특수방 전용 세트를 roomWallSets에서 찾습니다.
    // variantId가 있는 방은 variantId 일치를 최우선으로, 그다음 category 일치를 시도합니다.
    // 매칭되는 세트가 없으면 null을 반환해 일반방 기본 폴백 풀을 쓰게 됩니다.
    private RoomWallSet FindMatchingWallSet(RoomInstance room)
    {
        if (!string.IsNullOrEmpty(room.variantId))
        {
            var byVariant = roomWallSets.Find(s => !string.IsNullOrEmpty(s.variantId) && s.variantId == room.variantId);
            if (byVariant != null) return byVariant;
        }

        return roomWallSets.Find(s => string.IsNullOrEmpty(s.variantId) && s.category == room.category);
    }

    // neighborRoom의 category(또는 variantId)에 맞는 파사드 세트를 neighborFacingWallSets에서
    // 찾습니다. FindMatchingWallSet()과 동일한 우선순위 규칙(variantId 우선, 그다음 category)입니다.
    private NeighborFacingWallSet FindNeighborFacingWallSet(RoomInstance neighborRoom)
    {
        if (neighborRoom == null) return null;

        if (!string.IsNullOrEmpty(neighborRoom.variantId))
        {
            var byVariant = neighborFacingWallSets.Find(s => !string.IsNullOrEmpty(s.neighborVariantId) && s.neighborVariantId == neighborRoom.variantId);
            if (byVariant != null) return byVariant;
        }

        return neighborFacingWallSets.Find(s => string.IsNullOrEmpty(s.neighborVariantId) && s.neighborCategory == neighborRoom.category);
    }

    // 각 방향에 대해 "그 방향 너머에 있는 이웃 방"을 반환합니다 (앵커가 없는 방향은 딕셔너리에
    // 포함되지 않으며, 그게 곧 닫힌 방향입니다). 기존 GetOpenDirections()이 하던 "열림 여부"
    // 판정에, "그 너머가 어느 방인지"까지 함께 알려주도록 확장한 버전입니다.
    private Dictionary<Direction, RoomInstance> GetNeighborsByDirection(RoomInstance room)
    {
        var neighbors = new Dictionary<Direction, RoomInstance>();
        foreach (var link in room.anchors)
        {
            bool isRoomA = link.roomA == room;
            Direction dirFromThisRoom = isRoomA ? link.directionFromA : DirectionUtil.Opposite(link.directionFromA);
            RoomInstance neighborRoom = isRoomA ? link.roomB : link.roomA;
            neighbors[dirFromThisRoom] = neighborRoom;
        }
        return neighbors;
    }

    private void BuildFloor(Transform parent, Vector3 worldPos, List<WeightedPrefab> pool)
    {
        GameObject floorPrefabToUse = PickWeightedPrefab(pool);

        GameObject floor;
        if (floorPrefabToUse != null)
        {
            floor = Instantiate(floorPrefabToUse, worldPos, Quaternion.identity, parent);
        }
        else
        {
            floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor_Placeholder";
            floor.transform.SetParent(parent);
            floor.transform.position = worldPos + new Vector3(0f, -0.1f, 0f);
            float floorSize = cellSize + floorOverlapMargin;
            floor.transform.localScale = new Vector3(floorSize, 0.2f, floorSize);
        }
    }

    // 벽 하나를 지을 때 쓸 방향별 풀을 고릅니다. BuildRoom과 런타임 방 추가(부모방의 벽 교체)가 같은 규칙을 쓰도록 한 곳에
    // 모아둔 함수입니다. 열린 벽에 한해 아래 순서로 대체하고, 해당이 없으면 ownPools(이 방 자체의 풀)를 씁니다.
    // 1) 특수방 입구 벽: 이 방이 특수방이 아니고 이웃이 특수방이면(= 부모 방 쪽 벽) 그 레이아웃 외형 세트의
    //    "부모 방 쪽 입구 벽" → 없으면 Neighbor Facing Wall Sets의 그 특수방 항목(레이아웃 ID → Special 카테고리).
    //    특수방 부모는 일반방/시작방뿐이고 잠금 바리케이드가 이 벽에 서므로, 부모 방이 자기 외형(시작방 세트 등)을
    //    갖고 있어도 적용합니다.
    // 2) 그 외 이웃 파사드: 이 방이 자기 외형이 없는(hasOwnLook == false, 사실상 일반방) 경우에만 이웃 방을 향한
    //    파사드 풀(Neighbor Facing Wall Sets)로 대체합니다(예전 규칙 그대로).
    private List<DirectionalWallPool> PickWallPoolsForSegment(RoomInstance room, bool hasOwnLook, List<DirectionalWallPool> ownPools,
                                                              bool isOpen, RoomInstance neighborRoom)
    {
        if (!isOpen || neighborRoom == null) return ownPools;

        bool isSpecialEntrance = room != null && room.category != RoomCategory.Special &&
                                 neighborRoom.category == RoomCategory.Special;
        if (isSpecialEntrance)
        {
            var neighborVisualSet = GetVisualSet(neighborRoom);
            if (neighborVisualSet != null && neighborVisualSet.parentFacingWallPools.Count > 0)
            {
                return neighborVisualSet.parentFacingWallPools;
            }
        }

        if (isSpecialEntrance || !hasOwnLook)
        {
            var facingSet = FindNeighborFacingWallSet(neighborRoom);
            if (facingSet != null && facingSet.wallPools.Count > 0)
            {
                return facingSet.wallPools;
            }
        }
        return ownPools;
    }

    // 특수방이면 그 레이아웃의 외형 세트를, 아니면(또는 연결 안 했으면) null을 돌려줍니다.
    private static RoomVisualSet GetVisualSet(RoomInstance room)
    {
        if (room == null || room.category != RoomCategory.Special || room.specialLayout == null) return null;
        return room.specialLayout.visualSet;
    }

    // 이 방 자신의 벽 풀입니다: 외형 세트의 벽 → Room Wall Sets 항목의 벽 → 일반방 기본 벽 순서로 비어 있지 않은 것.
    private List<DirectionalWallPool> GetOwnWallPools(RoomVisualSet visualSet, RoomWallSet matchedSet)
    {
        if (visualSet != null && visualSet.wallPools.Count > 0) return visualSet.wallPools;
        if (matchedSet != null && matchedSet.wallPools.Count > 0) return matchedSet.wallPools;
        return wallPools;
    }

    // 특수방 외형 세트를 이번 빌드에서 처음 쓸 때 한 번만 확인해, 채워 놓은 칸에서 빠진 방향이 있으면 경고합니다.
    // 칸을 채웠으면 그 칸만 쓰므로(다른 세트로 넘어가지 않음) 빠진 방향에는 임시 큐브가 서기 때문입니다.
    // - 이 방의 벽: 특수방 열린 벽은 어느 방향에나 생길 수 있으므로 4방향 모두 닫힌 벽/열린 벽이 있어야 합니다.
    // - 부모 방 쪽 입구 벽: 부모 방 기준 4방향 모두 열린 벽이 있어야 합니다.
    // (짝 방향 회전 규칙 - North만 있으면 South에 회전 사용 - 을 고려해 판단합니다.)
    private void ValidateVisualSetOnce(RoomVisualSet visualSet, RoomInstance room)
    {
        if (visualSet == null || !validatedVisualSets.Add(visualSet)) return;

        string label = $"외형 세트 '{visualSet.name}'(레이아웃 '{room.variantId}')";

        if (visualSet.floorPool.Count > 0 && !visualSet.floorPool.Exists(c => c != null && c.prefab != null && c.weight > 0f))
        {
            Debug.LogWarning($"LevelBuilder: {label}의 Floor Pool에 쓸 수 있는 프리팹이 없습니다(비었거나 가중치 0). 임시 바닥이 깔립니다.");
        }

        if (visualSet.wallPools.Count > 0)
        {
            ValidateDirectionalPools(visualSet.wallPools, $"{label} 벽");
            var missing = new List<string>();
            foreach (var dir in DirectionUtil.All)
            {
                if (!CanSupplyWall(visualSet.wallPools, dir, false)) missing.Add($"{dir} 닫힌 벽");
                if (!CanSupplyWall(visualSet.wallPools, dir, true)) missing.Add($"{dir} 열린 벽");
            }
            if (missing.Count > 0)
            {
                Debug.LogWarning($"LevelBuilder: {label}의 Wall Pools에 빠진 칸이 있습니다({string.Join(", ", missing)}). " +
                                 "그 방향에 그 종류의 벽이 필요하면 임시 큐브가 섭니다.");
            }
        }

        if (visualSet.parentFacingWallPools.Count > 0)
        {
            ValidateDirectionalPools(visualSet.parentFacingWallPools, $"{label} 부모 방 쪽 입구 벽");
            var missing = new List<string>();
            foreach (var dir in DirectionUtil.All)
            {
                if (!CanSupplyWall(visualSet.parentFacingWallPools, dir, true)) missing.Add(dir.ToString());
            }
            if (missing.Count > 0)
            {
                Debug.LogWarning($"LevelBuilder: {label}의 Parent Facing Wall Pools에 열린 벽이 없는 방향이 있습니다({string.Join(", ", missing)}). " +
                                 "특수방이 부모 방의 그 방향에 붙으면 입구 벽 자리에 임시 큐브가 섭니다.");
            }
        }
    }

    // GetPoolPrefabWithRotation()이 dir/isOpen에 대해 프리팹을 하나라도 내줄 수 있는지(직접 또는 짝 방향 회전) 확인합니다.
    private static bool CanSupplyWall(List<DirectionalWallPool> pools, Direction dir, bool isOpen)
    {
        if (HasUsableCandidate(pools.Find(p => p.direction == dir), isOpen)) return true;
        return WallRotationPartner.TryGetValue(dir, out var partnerDir) &&
               HasUsableCandidate(pools.Find(p => p.direction == partnerDir), isOpen);
    }

    private static bool HasUsableCandidate(DirectionalWallPool pool, bool isOpen)
    {
        if (pool == null) return false;
        var candidates = isOpen ? pool.openWalls : pool.closedWalls;
        return candidates != null && candidates.Exists(c => c != null && c.prefab != null && c.weight > 0f);
    }

    // 방 roomId의 dir 방향에 지금 서 있는 벽 오브젝트를 기록합니다(런타임 방 추가 시 교체용).
    private void RecordWallRoot(int roomId, Direction dir, GameObject wallRoot)
    {
        if (!wallRoots.TryGetValue(roomId, out var byDirection))
        {
            byDirection = new Dictionary<Direction, GameObject>();
            wallRoots[roomId] = byDirection;
        }
        byDirection[dir] = wallRoot;
    }

    // wallRoot는 이 벽의 "루트 오브젝트"입니다(닫힌 벽이면 벽 자체, 골목이면 골목 프리팹 루트).
    // 반환값(게이트 대상)은 열린 벽의 바리케이드일 뿐이라, 벽 전체를 나중에 교체하려면 따로 필요합니다.
    private GameObject BuildWallSegment(Transform parent, Vector3 cellWorldPos, Direction dir, bool isOpen, List<DirectionalWallPool> pools, out GameObject wallRoot)
    {
        var (prefabToUse, extraYaw) = GetPoolPrefabWithRotation(dir, isOpen, pools);

        Vector3 offset = GetWallOffset(dir);
        Vector3 wallPos = cellWorldPos + offset;
        bool isNorthSouth = (dir == Direction.North || dir == Direction.South);

        GameObject wall;
        GameObject gateTarget = null; // Lock/UnlockGates가 실제로 켜고 끌 대상 (바리케이드 등)

        if (prefabToUse != null)
        {
            // 이 방향에 직접 등록된 프리팹을 쓴 경우 extraYaw는 0이라 회전 없이 그대로
            // 배치됩니다. 짝 방향(북<->서, 남<->동)의 프리팹을 재사용하는 경우에는
            // GetPoolPrefabWithRotation이 계산한 각도만큼 추가로 회전시켜 배치합니다
            // (예: 북쪽용으로 만든 프리팹을 서쪽에서 재사용 - 리소스를 두 배로 만들지
            // 않기 위한 구조입니다). 회전 방향이 반대로 보이면 인스펙터의
            // invertWallRotationDirection을 켜서 부호를 뒤집어보세요.
            wall = Instantiate(prefabToUse, wallPos, Quaternion.Euler(0f, extraYaw, 0f), parent);

            if (isOpen)
            {
                // 열린 벽(골목) 프리팹은 "벽 전체가 통째로 켜지고 꺼지는" 오브젝트가 아닙니다.
                // 빌딩/화분/자판기 같은 배경 오브젝트는 항상 고정으로 존재하고, 그 사이에
                // 낀 바리케이드만 클리어 여부에 따라 나타났다 사라졌다 합니다. WallSidePrefab
                // 컴포넌트를 통해 그 바리케이드 오브젝트를 찾아 게이트 대상으로 씁니다.
                var sidePrefab = wall.GetComponent<WallSidePrefab>();
                if (sidePrefab != null && sidePrefab.barricade != null)
                {
                    gateTarget = sidePrefab.barricade;
                }
                else
                {
                    // WallSidePrefab이 없거나 barricade가 연결되지 않은 프리팹이면, 예전처럼
                    // 벽 전체를 게이트로 취급합니다 (골목+바리케이드 구조를 쓰려면 프리팹 루트에
                    // WallSidePrefab을 추가하고 barricade를 연결해주세요). 클리어 조건이 없는
                    // 방(시작방 등)은 아무도 다시 열어주지 않으므로, 이 폴백에 걸리면 그 방향이
                    // 영구히 안 보이는 상태가 될 수 있습니다 - 열린 벽 프리팹에는 항상
                    // WallSidePrefab + barricade(빈 오브젝트라도 무방)를 연결해두세요.
                    Debug.LogWarning($"LevelBuilder: '{prefabToUse.name}' 프리팹에 WallSidePrefab/barricade 연결이 없어 벽 전체를 게이트로 취급합니다.");
                    gateTarget = wall;
                }

                // 게이트 대상은 기본적으로 "열림(비활성)" 상태로 시작해야 합니다. 실제 잠금은
                // RoomController.Start()가 아니라, 플레이어가 이 방에 실제로 들어와
                // RoomController.EnterRoom()이 호출되는 순간 "즉시" 처리됩니다(유예 시간
                // 없음). LevelBuilder.Update()가 EnterRoom()을 호출하기 직전에 플레이어를
                // roomEntryPushDistance만큼 진입 방향으로 먼저 밀어넣어 두므로, 즉시 잠가도
                // 겹칠 일이 없습니다 (PushPlayerForwardIntoRoom 참고). 클리어 조건이 없는
                // 방(시작방/동아리방/편의점 등)은 애초에 Lock/UnlockGates()를 호출하지
                // 않으므로 여기서 열어둔 상태가 그대로 유지되어야 정상 동작합니다.
                // (이 처리가 없으면 실제 프리팹 연결 시 클리어 조건 없는 방들의 바리케이드가
                // 영원히 막힌 상태로 남는 버그가 발생합니다.)
                gateTarget.SetActive(false);
            }
        }
        else if (!isOpen)
        {
            wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = $"Wall_{dir}_Placeholder";
            wall.transform.SetParent(parent);
            wall.transform.position = wallPos + new Vector3(0f, wallHeight / 2f, 0f);
            wall.transform.rotation = Quaternion.identity;
            wall.transform.localScale = isNorthSouth
                ? new Vector3(cellSize, wallHeight, wallThickness)
                : new Vector3(wallThickness, wallHeight, cellSize);
        }
        else
        {
            wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = $"Gate_{dir}_Placeholder";
            wall.transform.SetParent(parent);
            wall.transform.position = wallPos + new Vector3(0f, wallHeight / 2f, 0f);
            wall.transform.rotation = Quaternion.identity;
            wall.transform.localScale = isNorthSouth
                ? new Vector3(cellSize, wallHeight, wallThickness)
                : new Vector3(wallThickness, wallHeight, cellSize);

            var rend = wall.GetComponent<Renderer>();
            if (rend != null) rend.material.color = new Color(1f, 0.3f, 0.3f, 0.6f);

            wall.SetActive(false);
            gateTarget = wall; // 플레이스홀더는 벽 전체가 곧 바리케이드 역할
        }

        // 닫힌 벽(wallPools의 closedWalls 후보 또는 그 플레이스홀더)은 gateTarget이 null로 남아
        // RegisterGate 대상에서 자동으로 제외됩니다 (호출부의 BuildRoom 참고).
        wallRoot = wall;
        return gateTarget;
    }

    // [방향별 프리팹 재사용 - 북/서, 남/동 쌍]
    // 마름모 시야 기준 화면 위쪽(카메라에서 먼, 항상 보이는 두 변)은 북/서, 화면 아래쪽
    // (카메라에 가까워 숨겨지는 두 변)은 남/동입니다. 같은 그룹끼리는 같은 프리팹을
    // 90도 회전시켜 재사용할 수 있도록 짝을 지어뒀습니다 (북쪽 프리팹을 만들면 서쪽은
    // 그 프리팹을 회전해서 자동으로 재사용, 남쪽 프리팹을 만들면 동쪽도 마찬가지).
    //
    // 우선순위: 해당 방향에 직접 등록된 풀이 있으면 그걸 그대로 씁니다(회전 없음) - 즉
    // 특정 방향에만 다른 디자인을 주고 싶으면 그 방향에 개별 프리팹을 등록하면 되고,
    // 등록하지 않은 방향은 자동으로 짝 방향의 프리팹을 회전해서 재사용합니다.
    private static readonly Dictionary<Direction, Direction> WallRotationPartner = new Dictionary<Direction, Direction>
    {
        { Direction.North, Direction.West },
        { Direction.West, Direction.North },
        { Direction.South, Direction.East },
        { Direction.East, Direction.South },
    };

    // Unity의 Y축 회전 기준(북=0, 동=90, 남=180, 서=270)에 맞춘 각 방향의 기준 각도입니다.
    // GetWallOffset()이 쓰는 좌표축(북=+Z, 동=+X)과 Quaternion.Euler(0, yaw, 0)의 회전
    // 방향이 이미 일치하므로, 이 값들은 이론상 정확해야 합니다. 다만 실제 프리팹을 만들
    // 때 "정면"으로 삼은 방향이 예상과 다르면 회전이 반대로 보일 수 있으니, 그럴 경우
    // 인스펙터의 invertWallRotationDirection을 켜서 부호를 뒤집으세요 (카메라 각도를
    // 잡을 때와 동일한 성격의 문제입니다).
    private static float GetCanonicalYaw(Direction dir)
    {
        switch (dir)
        {
            case Direction.North: return 0f;
            case Direction.East: return 90f;
            case Direction.South: return 180f;
            case Direction.West: return 270f;
            default: return 0f;
        }
    }

    // 전달받은 방향별 풀 목록에서 해당 방향의 프리팹을 찾습니다. 그 방향에 직접 등록된
    // 후보가 있으면 그대로 반환하고(추가 회전 없음), 없으면 짝 방향(WallRotationPartner)의
    // 풀에서 후보를 가져와 그 방향에 맞는 회전값과 함께 반환합니다. 어느 쪽에도 후보가
    // 없으면 (null, 0)을 반환해 기존 플레이스홀더 생성 경로로 자연스럽게 폴백됩니다.
    private (GameObject prefab, float extraYaw) GetPoolPrefabWithRotation(Direction dir, bool isOpen, List<DirectionalWallPool> pools)
    {
        var directPool = pools.Find(p => p.direction == dir);
        if (directPool != null)
        {
            var directCandidates = isOpen ? directPool.openWalls : directPool.closedWalls;
            var directPrefab = PickWeightedPrefab(directCandidates);
            if (directPrefab != null) return (directPrefab, 0f);
        }

        if (WallRotationPartner.TryGetValue(dir, out var partnerDir))
        {
            var partnerPool = pools.Find(p => p.direction == partnerDir);
            if (partnerPool != null)
            {
                var partnerCandidates = isOpen ? partnerPool.openWalls : partnerPool.closedWalls;
                var partnerPrefab = PickWeightedPrefab(partnerCandidates);
                if (partnerPrefab != null)
                {
                    float yaw = GetCanonicalYaw(dir) - GetCanonicalYaw(partnerDir);
                    if (invertWallRotationDirection) yaw = -yaw;
                    return (partnerPrefab, yaw);
                }
            }
        }

        return (null, 0f);
    }

    private GameObject PickWeightedPrefab(List<WeightedPrefab> candidates)
    {
        if (candidates == null || candidates.Count == 0) return null;

        var valid = candidates.Where(c => c.prefab != null && c.weight > 0f).ToList();
        if (valid.Count == 0) return null;
        if (valid.Count == 1) return valid[0].prefab;

        float totalWeight = valid.Sum(c => c.weight);
        float roll = UnityEngine.Random.value * totalWeight;
        float cumulative = 0f;
        foreach (var candidate in valid)
        {
            cumulative += candidate.weight;
            if (roll <= cumulative) return candidate.prefab;
        }
        return valid[valid.Count - 1].prefab;
    }

    private Vector3 GetWallOffset(Direction dir)
    {
        float half = cellSize / 2f;
        switch (dir)
        {
            case Direction.North: return new Vector3(0f, 0f, half);
            case Direction.South: return new Vector3(0f, 0f, -half);
            case Direction.East: return new Vector3(half, 0f, 0f);
            case Direction.West: return new Vector3(-half, 0f, 0f);
            default: return Vector3.zero;
        }
    }

    private Vector3 CellToWorld(Vector2Int coord)
    {
        return new Vector3(coord.x * cellSize, 0f, coord.y * cellSize);
    }

    // existingPlayer가 있으면 이동, 없으면 새로 스폰합니다.
    private void SpawnPlayer(GameObject existingPlayer)
    {
        RoomInstance startRoom = generatedRooms.Find(r => r.category == RoomCategory.Start);
        if (startRoom == null)
        {
            Debug.LogWarning("LevelBuilder: 시작방을 찾지 못했습니다.");
            return;
        }

        Vector3 spawnPos = CellToWorld(startRoom.occupiedCells[0]) + Vector3.up * 1f;

        if (existingPlayer != null)
        {
            RelocatePlayer(existingPlayer, spawnPos);
            spawnedPlayer = existingPlayer;
        }
        else
        {
            if (playerPrefab == null)
            {
                Debug.LogWarning("LevelBuilder: playerPrefab이 비어있어 플레이어를 스폰하지 않았습니다.");
                return;
            }
            spawnedPlayer = Instantiate(playerPrefab, spawnPos, Quaternion.identity);
        }

        // 방 전환 시 플레이어를 밀어넣는 데 쓸 CharacterController를 매번 새로 찾아서
        // 캐싱해둡니다 (existingPlayer 재사용/신규 스폰 둘 다 이 시점에 다시 잡아야 함).
        spawnedPlayerController = spawnedPlayer.GetComponent<CharacterController>();

        // CameraFollow는 Awake() 시점에 "Player" 태그를 자동으로 찾는데, 최초 스폰 시점엔
        // 아직 플레이어가 없어서 못 찾습니다. 전환 시에도 매번 안전하게 재연결해줍니다.
        var cameraFollow = Object.FindFirstObjectByType<CameraFollow>();
        if (cameraFollow != null)
        {
            cameraFollow.target = spawnedPlayer.transform;
        }
        else
        {
            Debug.LogWarning("LevelBuilder: 씬에서 CameraFollow 컴포넌트를 찾지 못했습니다. Main Camera에 붙어있는지 확인해주세요.");
        }

        // 플레이어가 시작방 칸 "안에서" 배치되기 때문에, Update()의 칸 변경 감지가
        // 아직 이전 프레임 값과 비교할 기준이 없습니다. 그래서 시작방 진입 처리와
        // 현재 칸 상태를 여기서 직접 초기화해줍니다.
        if (roomControllers.TryGetValue(startRoom.id, out var startController))
        {
            startController.EnterRoom();
        }
        currentPlayerCell = startRoom.occupiedCells[0];
        hasPlayerCell = true;
    }

    // 기존 플레이어를 파괴하지 않고 위치만 옮깁니다.
    // CharacterController가 켜진 상태에서 transform.position을 직접 바꾸면 다음 프레임의
    // 충돌 보정과 충돌할 수 있어, 잠깐 꺼뒀다가 위치를 옮기고 다시 켭니다.
    private void RelocatePlayer(GameObject player, Vector3 newPosition)
    {
        var controller = player.GetComponent<CharacterController>();
        if (controller != null) controller.enabled = false;

        player.transform.position = newPosition;
        player.transform.rotation = Quaternion.identity;

        if (controller != null) controller.enabled = true;
    }

    // [v1.29 추가 - 일회용 아이템(22번) 등 "레벨 내 특정 카테고리 방으로 텔레포트" 기능용]
    // categories에 해당하는 방들을 전부 모아 그중 하나를 무작위로 고른 뒤(카테고리 우선순위
    // 없이 방 개수 기준 균등 추첨 - 예: 상점 3개 + 동아리방 1개면 상점이 뽑힐 확률이 더
    // 높습니다), 그 방의 앵커 근처 안전한 위치로 플레이어를 이동시킵니다.
    //
    // 걸어서 이동할 때의 칸 전환 감지(Update()의 ComputeAxisCellWithHysteresis)는 "한
    // 프레임에 최대 한 칸만 이동한다"는 전제라 원거리 텔레포트에는 그대로 쓸 수 없습니다
    // (그 메서드 자체 주석 참고). 그래서 이 메서드가 SpawnPlayer()의 시작방 진입 처리와
    // 동일한 방식으로 currentPlayerCell/hasPlayerCell을 직접 갱신하고, 도착한 방의
    // RoomController.EnterRoom()을 직접 호출해 "방 하나만 보이기" 전환과(클리어 조건이
    // 있는 방이라면) 바리케이드 잠금까지 즉시 맞춰줍니다. 해당 카테고리의 방이 레벨에
    // 하나도 없으면 아무 것도 하지 않고 false를 반환합니다.
    public bool TryTeleportPlayerToRandomRoom(params RoomCategory[] categories)
    {
        return TryTeleportPlayerToRandomRoom(false, categories);
    }

    // [22번 아이템 변경 - 안 가본 방 우선 (형빈 확정)] preferUnvisited가 true면 후보 중 아직 한 번도 들어가지 않은
    // 방(RoomInstance.visited == false)만 남겨서 그중에서 무작위로 고르고, 안 가본 방이 하나도 없을 때만 전체 후보
    // 중에서 고릅니다. 예: 동아리방 3개 중 2개를 이미 다녀왔으면 남은 1개로, 1개만 다녀왔으면 나머지 2개 중 하나로.
    // false면 위의 기존 동작(전체 후보 균등 추첨)과 완전히 같습니다.
    public bool TryTeleportPlayerToRandomRoom(bool preferUnvisited, params RoomCategory[] categories)
    {
        if (spawnedPlayer == null || generatedRooms == null || categories == null || categories.Length == 0)
        {
            return false;
        }

        var candidates = generatedRooms.Where(r => categories.Contains(r.category)).ToList();
        if (candidates.Count == 0) return false;

        if (preferUnvisited)
        {
            var unvisited = candidates.Where(r => !r.visited).ToList();
            if (unvisited.Count > 0) candidates = unvisited;
        }

        var room = candidates[UnityEngine.Random.Range(0, candidates.Count)];
        Vector2Int destinationCell = room.occupiedCells[0];
        Vector3 destination = GetRoomEntryPosition(room, destinationCell);

        RelocatePlayer(spawnedPlayer, destination);

        currentPlayerCell = destinationCell;
        hasPlayerCell = true;

        if (roomControllers.TryGetValue(room.id, out var controller))
        {
            controller.EnterRoom();
        }

        return true;
    }

    // room의 앵커(다른 방과 이어지는 연결 지점) 중 하나를 무작위로 골라, 그 앵커가 가리키는
    // 문 방향에서 방 안쪽으로 살짝 들어온 위치를 반환합니다(문 경계에 딱 걸치지 않도록
    // GetWallOffset의 60%만 이동 - RealEnemySpawner.GetDoorWorldPositions와 동일한 방향 ->
    // 오프셋 계산을 재사용합니다). 앵커가 없는 방(이론상 거의 없음)은 그 방 칸의 중심
    // 위치를 그대로 씁니다.
    private Vector3 GetRoomEntryPosition(RoomInstance room, Vector2Int cell)
    {
        Vector3 roomCenter = CellToWorld(cell) + Vector3.up * 1f;

        if (room.anchors == null || room.anchors.Count == 0)
        {
            return roomCenter;
        }

        var anchor = room.anchors[UnityEngine.Random.Range(0, room.anchors.Count)];
        bool isRoomA = anchor.roomA == room;
        Direction doorDirection = isRoomA ? anchor.directionFromA : DirectionUtil.Opposite(anchor.directionFromA);

        Vector3 inwardOffset = GetWallOffset(doorDirection) * 0.6f;
        return roomCenter + inwardOffset;
    }

    // ---------- 런타임 방 추가 (16번 액티브 아이템 등) ----------

    // 새 방을 붙일 수 있는 자리 하나입니다(부모방 + 그 방향 + 새 방이 들어갈 칸).
    private struct ExtraRoomSlot
    {
        public RoomInstance parent;
        public Direction direction;
        public Vector2Int cell;
    }

    // 지금 새 방을 붙일 자리가 하나라도 있는지 알려줍니다. 액티브 아이템이 코스트를 내기 전에
    // 사용 가능 여부를 확인하는 데 씁니다(전투 레벨이 아니거나 자리가 없으면 false).
    public bool CanAddExtraRoom()
    {
        return FindExtraRoomSlots().Count > 0;
    }

    // category 방을 지금 붙일 수 있는지입니다. 특수방은 붙일 수 있는 부모 방(Level Config의 Special Room Parent
    // Categories - 기본 시작방/일반방)이 있어야 하고, Level Config의 Special Layouts에 쓸 수 있는 레이아웃이 있어야 합니다.
    // 16번 아이템이 코스트를 내기 전에 뽑을 수 있는 방 종류를 고르는 데 씁니다.
    public bool CanAddExtraRoom(RoomCategory category)
    {
        if (category == RoomCategory.Special && GetUsableSpecialLayoutEntries().Count == 0) return false;
        return FindExtraRoomSlots(category).Count > 0;
    }

    // 지금 지어진 레벨에 category 카테고리의 방을 하나 더 붙입니다. 성공하면 true, 붙일 자리가
    // 없거나 실패하면 false를 반환하고 아무것도 바꾸지 않습니다. Level Config의 방 개수/맵 크기
    // 제한과 무관합니다(클래스 상단 주석 참고).
    public bool TryAddExtraRoom(RoomCategory category)
    {
        if (generatedRooms == null || levelRoot == null || visibilityManager == null) return false;

        var slots = FindExtraRoomSlots(category);
        if (slots.Count == 0) return false;

        // 특수방은 레이아웃을 먼저 뽑습니다(레벨 생성 때 AttachSpecialRooms와 같은 정보 - 레이아웃/variantId/진입 조건).
        SpecialRoomLayout specialLayout = null;
        if (category == RoomCategory.Special)
        {
            specialLayout = PickSpecialLayoutForExtraRoom();
            if (specialLayout == null)
            {
                Debug.LogWarning("LevelBuilder: Level Config의 Special Layouts에 쓸 수 있는 레이아웃이 없어 특수방을 추가하지 못했습니다.");
                return false;
            }
        }

        var slot = slots[UnityEngine.Random.Range(0, slots.Count)];
        var parent = slot.parent;
        Vector2Int parentCell = parent.occupiedCells[0];

        // 새 방 데이터와, 부모방과의 연결을 먼저 만듭니다(BuildRoom이 열린 방향을 이 연결로 알아냅니다).
        var newRoom = new RoomInstance
        {
            id = NextRoomId(),
            category = category,
            variantId = PickVariantIdForExtraRoom(category)
        };
        if (specialLayout != null)
        {
            newRoom.specialLayout = specialLayout;
            newRoom.variantId = string.IsNullOrEmpty(specialLayout.layoutId) ? null : specialLayout.layoutId;
            // 진입 조건도 레벨 생성 때처럼 이 방에서 한 번 뽑습니다(레이아웃의 Entry Condition Pool).
            newRoom.specialEntryCondition = specialLayout.PickEntryCondition(new System.Random(UnityEngine.Random.Range(0, int.MaxValue)));
        }
        newRoom.occupiedCells.Add(slot.cell);

        var link = new AnchorLink
        {
            roomA = parent,
            roomB = newRoom,
            cellA = parentCell,
            cellB = slot.cell,
            directionFromA = slot.direction,
            isOpen = false
        };
        parent.anchors.Add(link);
        newRoom.anchors.Add(link);
        generatedRooms.Add(newRoom);

        // 새 방을 짓습니다(기존 BuildRoom 그대로 - 바닥/벽/동아리방 보물상자/NavMesh까지 동일하게 처리).
        BuildRoom(newRoom);

        // 부모방의 닫힌 벽을 골목(열린 벽)으로 교체하고, 바뀐 지오메트리에 맞춰 NavMesh를 다시 굽습니다.
        ReplaceClosedWallWithOpen(parent, slot.direction, newRoom);

        // 미니맵이 방 목록이 바뀐 것을 감지해 아이콘/연결선을 다시 그리도록 합니다.
        levelBuildVersion++;
        return true;
    }

    // 새 방을 붙일 수 있는 모든 자리를 찾습니다. 원래 레벨 생성(LevelGraphGenerator.TryPlaceCell)과
    // 같은 규칙으로, 빈 칸이 부모방 외 다른 방과 맞닿지 않아야 합니다(그래야 새 방이 막다른 방).
    // forCategory가 특수방이면 부모 방을 Level Config의 Special Room Parent Categories(기본 시작방/일반방)로 더
    // 좁힙니다 - 레벨 생성 때 특수방이 붙는 규칙과 같습니다(동아리방/편의점에는 붙지 않음). 동아리방이면
    // extraClubParentCategories(기본 시작방/일반방/동아리방)로 좁힙니다(형빈 확정).
    private List<ExtraRoomSlot> FindExtraRoomSlots(RoomCategory? forCategory = null)
    {
        var slots = new List<ExtraRoomSlot>();
        if (!currentLevelIsCombat || generatedRooms == null) return slots;

        var occupied = new HashSet<Vector2Int>();
        foreach (var room in generatedRooms)
        {
            foreach (var cell in room.occupiedCells) occupied.Add(cell);
        }

        foreach (var room in generatedRooms)
        {
            if (!IsEligibleExtraRoomParent(room)) continue;
            if (forCategory == RoomCategory.Special && !IsAllowedSpecialParent(room)) continue;
            if (forCategory == RoomCategory.Club && !IsAllowedClubParent(room)) continue;

            Vector2Int parentCell = room.occupiedCells[0];
            foreach (var dir in DirectionUtil.All)
            {
                Vector2Int next = parentCell + DirectionUtil.ToOffset(dir);
                if (occupied.Contains(next)) continue;
                if (HasOtherOccupiedNeighbor(next, parentCell, occupied)) continue;

                // 이 방향에 지금 서 있는 벽 오브젝트를 알고 있어야 골목으로 갈아 끼울 수 있습니다.
                if (!wallRoots.TryGetValue(room.id, out var byDirection) ||
                    !byDirection.TryGetValue(dir, out var wall) || wall == null) continue;

                slots.Add(new ExtraRoomSlot { parent = room, direction = dir, cell = next });
            }
        }
        return slots;
    }

    // 부모방이 될 수 있는 방인지: 보스방/특수방은 제외하고, 적이 없는 방(전투가 없는 방이거나 이미
    // 클리어한 방)이어야 합니다. 특수방은 항상 막다른 방이어야 하므로(진입 조건으로 잠겨 있을 수 있어,
    // 그 너머에 방이 생기면 길이 막힘) 새 방을 붙이는 자리로 쓰지 않습니다.
    private bool IsEligibleExtraRoomParent(RoomInstance room)
    {
        if (room == null || room.occupiedCells.Count == 0) return false;
        if (room.category == RoomCategory.Boss) return false;
        if (room.category == RoomCategory.Special) return false;
        if (!roomControllers.TryGetValue(room.id, out var controller) || controller == null) return false;
        if (controller.ContentRoot == null) return false;

        return !controller.RequiresClear || room.isCleared;
    }

    // 특수방의 부모가 될 수 있는 종류인지: Level Config의 Special Room Parent Categories(비어 있거나 config가 없으면
    // 기본값 시작방/일반방)에 있어야 합니다. 보스방/특수방은 IsEligibleExtraRoomParent에서 이미 빠집니다.
    private bool IsAllowedSpecialParent(RoomInstance room)
    {
        var allowed = config != null ? config.specialRoomParentCategories : null;
        if (allowed == null || allowed.Count == 0)
        {
            return room.category == RoomCategory.Start || room.category == RoomCategory.Normal;
        }
        return allowed.Contains(room.category);
    }

    // 16번 동아리방의 부모가 될 수 있는 종류인지: extraClubParentCategories(비어 있으면 기본값 시작방/일반방/동아리방)에 있어야 합니다.
    private bool IsAllowedClubParent(RoomInstance room)
    {
        if (extraClubParentCategories == null || extraClubParentCategories.Count == 0)
        {
            return room.category == RoomCategory.Start || room.category == RoomCategory.Normal || room.category == RoomCategory.Club;
        }
        return extraClubParentCategories.Contains(room.category);
    }

    // Level Config의 Special Layouts 중 쓸 수 있는 줄(레이아웃이 있고 가중치 > 0)입니다.
    private List<SpecialLayoutEntry> GetUsableSpecialLayoutEntries()
    {
        if (config == null || config.specialLayouts == null) return new List<SpecialLayoutEntry>();
        return config.specialLayouts.Where(e => e != null && e.layout != null && e.weight > 0f).ToList();
    }

    // 16번으로 추가할 특수방의 레이아웃을 Level Config의 Special Layouts에서 가중치로 뽑습니다. 이 레벨에 이미 있는
    // 레이아웃은 되도록 피합니다(레벨 생성 때 서로 다른 레이아웃을 뽑는 것과 같은 방향 - 다른 게 없으면 겹쳐도 뽑음).
    private SpecialRoomLayout PickSpecialLayoutForExtraRoom()
    {
        var valid = GetUsableSpecialLayoutEntries();
        if (valid.Count == 0) return null;

        var used = new HashSet<SpecialRoomLayout>();
        foreach (var room in generatedRooms)
        {
            if (room.category == RoomCategory.Special && room.specialLayout != null) used.Add(room.specialLayout);
        }

        var unused = valid.Where(e => !used.Contains(e.layout)).ToList();
        var candidates = unused.Count > 0 ? unused : valid;

        float totalWeight = candidates.Sum(e => e.weight);
        float roll = UnityEngine.Random.value * totalWeight;
        float cumulative = 0f;
        foreach (var entry in candidates)
        {
            cumulative += entry.weight;
            if (roll <= cumulative) return entry.layout;
        }
        return candidates[candidates.Count - 1].layout;
    }

    // candidate가 parent 외에 이미 점유된 다른 칸과 맞닿아 있는지 확인합니다
    // (LevelGraphGenerator.HasExtraNeighbor와 같은 규칙).
    private bool HasOtherOccupiedNeighbor(Vector2Int candidate, Vector2Int parent, HashSet<Vector2Int> occupied)
    {
        foreach (var dir in DirectionUtil.All)
        {
            Vector2Int neighbor = candidate + DirectionUtil.ToOffset(dir);
            if (neighbor == parent) continue;
            if (occupied.Contains(neighbor)) return true;
        }
        return false;
    }

    private int NextRoomId()
    {
        int maxId = -1;
        foreach (var room in generatedRooms)
        {
            if (room.id > maxId) maxId = room.id;
        }
        return maxId + 1;
    }

    // 새 방의 variantId를 정합니다. 뽑힌 카테고리의 variant 풀(Level Config의 clubVariants 등)에서
    // 가중치로 하나를 뽑되, 이 레벨에서 같은 카테고리로 이미 쓰인 variant는 되도록 피합니다(레벨
    // 생성 때 같은 카테고리 방들이 서로 다른 variant를 받는 것과 같은 방향). 풀이 비어있으면
    // null을 반환해 variantId 없이 카테고리로만 구분됩니다.
    private string PickVariantIdForExtraRoom(RoomCategory category)
    {
        List<RoomVariant> pool = GetConfigVariantPool(category);
        if (pool == null || pool.Count == 0) return null;

        var used = new HashSet<string>();
        foreach (var room in generatedRooms)
        {
            if (room.category == category && !string.IsNullOrEmpty(room.variantId)) used.Add(room.variantId);
        }

        var valid = pool.Where(v => v != null && v.weight > 0f).ToList();
        if (valid.Count == 0) return null;

        var unused = valid.Where(v => !used.Contains(v.variantId)).ToList();
        var candidates = unused.Count > 0 ? unused : valid;

        float totalWeight = candidates.Sum(v => v.weight);
        float roll = UnityEngine.Random.value * totalWeight;
        float cumulative = 0f;
        foreach (var v in candidates)
        {
            cumulative += v.weight;
            if (roll <= cumulative) return v.variantId;
        }
        return candidates[candidates.Count - 1].variantId;
    }

    // 카테고리에 해당하는 variant 풀을 config에서 꺼냅니다. 풀이 따로 없는 카테고리는 null입니다.
    private List<RoomVariant> GetConfigVariantPool(RoomCategory category)
    {
        if (config == null) return null;

        switch (category)
        {
            case RoomCategory.Club: return config.clubVariants;
            case RoomCategory.Store: return config.storeVariants;
            // 특수방은 RoomVariant 풀이 아니라 레이아웃 에셋 목록(config.specialLayouts)을 쓰며, 레벨 생성 때는
            // LevelGraphGenerator.AttachSpecialRooms가, 16번 방 추가 때는 TryAddExtraRoom(PickSpecialLayoutForExtraRoom)이
            // 직접 배정합니다. 여기서는 풀이 없는 것으로 취급합니다.
            default: return null;
        }
    }

    // 부모방의 dir 방향 닫힌 벽을 없애고 그 자리에 골목(열린 벽)을 새로 세웁니다. 부모방은 적이 없는
    // 방이라 새 게이트(바리케이드)는 열린(꺼진) 상태로 시작하는 게 맞습니다. 벽이 바뀌었으니 부모방의
    // NavMesh를 다시 굽습니다(안 그러면 새 골목이 근접 적의 안전지대가 됩니다).
    private void ReplaceClosedWallWithOpen(RoomInstance parent, Direction dir, RoomInstance newRoom)
    {
        if (!roomControllers.TryGetValue(parent.id, out var controller) || controller == null) return;

        GameObject contentGO = controller.ContentRoot;
        if (contentGO == null) return;

        // 옛 닫힌 벽은 Destroy가 프레임 끝에야 처리되므로, 먼저 꺼서 NavMesh 굽기에서 빠지게 합니다.
        if (wallRoots.TryGetValue(parent.id, out var byDirection) &&
            byDirection.TryGetValue(dir, out var oldWall) && oldWall != null)
        {
            oldWall.SetActive(false);
            Destroy(oldWall);
        }

        Vector3 worldPos = CellToWorld(parent.occupiedCells[0]);
        var parentVisualSet = GetVisualSet(parent);
        var matchedSet = FindMatchingWallSet(parent);
        var ownPools = GetOwnWallPools(parentVisualSet, matchedSet);
        bool hasOwnLook = parentVisualSet != null || matchedSet != null;
        var pools = PickWallPoolsForSegment(parent, hasOwnLook, ownPools, true, newRoom);

        var segment = BuildWallSegment(contentGO.transform, worldPos, dir, true, pools, out var newWallRoot);
        RecordWallRoot(parent.id, dir, newWallRoot);
        if (segment != null) controller.RegisterGate(segment);
        GameObject lockGO = ApplySpecialLockToWall(parent, dir, newRoom, newWallRoot, contentGO.transform, worldPos);

        RebakeRoomNavMesh(contentGO);

        // 플레이어가 지금 이 부모 방에 있어 새 벽이 화면에 보이는 경우에만 연출과 밀어내기를 합니다. 다른 방에 있으면 이 방은
        // 꺼져 있어 바뀌는 장면이 안 보이고, 연출을 붙여 두면 나중에 들어올 때 뒤늦게 재생되므로 붙이지 않습니다.
        if (contentGO.activeInHierarchy)
        {
            PlayNewWallAppear(newWallRoot, lockGO);
            PushPlayerAwayFromNewWall(parent, dir, worldPos, newWallRoot, lockGO);
        }
    }

    // [16번 방 추가 - 벽 교체 연출 (형빈 확정)] 옛 벽은 이미 즉시 사라졌고, 새 벽(골목/파사드/특수방 입구 벽 - 종류와 상관없이
    // 전부)과 잠금 바리케이드는 보물상자 등장과 같은 "투명 -> 흰색 -> 본색" 연출로 떠오르게 합니다(AppearFromWhite).
    private static void PlayNewWallAppear(GameObject newWallRoot, GameObject lockGO)
    {
        if (newWallRoot != null) AppearFromWhite.Begin(newWallRoot);

        // 벽 밖에 따로 선 잠금 바리케이드(임시 큐브 등)만 따로 연출합니다. 벽 안에 들어 있으면 위에서 함께 떠오릅니다.
        if (lockGO != null && (newWallRoot == null || !lockGO.transform.IsChildOf(newWallRoot.transform)))
        {
            AppearFromWhite.Begin(lockGO);
        }
    }

    // [16번 방 추가 - 플레이어 밀어내기 (형빈 확정)] 플레이어가 이 부모 방(parent)에 있고 새 벽/잠금 바리케이드 앞의 "막히는
    // 구역" 안에 서 있으면, 벽과 겹치지 않을 만큼 방 안쪽(벽에 수직, 방 중앙 쪽)으로 짧은 넉백으로 밀어냅니다(ForcedPush).
    // 막히는 구역의 깊이 = 새 벽/바리케이드 콜라이더가 칸 경계선에서 방 안쪽으로 들어온 깊이 + 플레이어 몸 반지름 + 여유
    // (extraRoomWallPushMargin). 벽 모양을 직접 재므로 파사드/바리케이드 프리팹이 바뀌어도 자동으로 맞습니다. 같은 칸 안에서만
    // 움직이므로 방 전환은 일어나지 않습니다.
    private void PushPlayerAwayFromNewWall(RoomInstance parent, Direction dir, Vector3 cellWorldPos, GameObject newWallRoot, GameObject lockGO)
    {
        if (spawnedPlayer == null || spawnedPlayerController == null || !spawnedPlayerController.enabled) return;
        if (!hasPlayerCell || parent.occupiedCells.Count == 0 || currentPlayerCell != parent.occupiedCells[0]) return;

        Physics.SyncTransforms(); // 방금 만든 콜라이더의 경계 상자가 바로 맞도록

        var colliders = new List<Collider>();
        CollectSolidColliders(newWallRoot, colliders);
        CollectSolidColliders(lockGO, colliders);
        if (colliders.Count == 0) return;

        Vector3 edgeOffset = GetWallOffset(dir);
        Vector3 inward = -edgeOffset.normalized;
        Vector3 edgePoint = cellWorldPos + edgeOffset;

        // 새 벽/바리케이드가 칸 경계선에서 방 안쪽으로 가장 깊이 들어온 거리
        float wallDepth = 0f;
        foreach (var col in colliders)
        {
            Bounds b = col.bounds;
            float extentAlongInward = Mathf.Abs(inward.x) * b.extents.x + Mathf.Abs(inward.z) * b.extents.z;
            float depth = Vector3.Dot(b.center - edgePoint, inward) + extentAlongInward;
            if (depth > wallDepth) wallDepth = depth;
        }

        Transform playerT = spawnedPlayer.transform;
        Vector3 scale = playerT.lossyScale;
        float playerRadius = spawnedPlayerController.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
        float playerDepth = Vector3.Dot(playerT.position - edgePoint, inward);

        float required = wallDepth + playerRadius + extraRoomWallPushMargin;
        if (playerDepth >= required) return; // 이미 충분히 떨어져 있음

        ForcedPush.Apply(spawnedPlayer, inward, required - playerDepth, extraRoomWallPushDuration, colliders);
    }

    // root와 자식의 켜져 있는 "막는" 콜라이더(트리거 아님)를 모읍니다.
    private static void CollectSolidColliders(GameObject root, List<Collider> into)
    {
        if (root == null) return;
        foreach (var col in root.GetComponentsInChildren<Collider>())
        {
            if (col != null && col.enabled && !col.isTrigger && !into.Contains(col)) into.Add(col);
        }
    }

    // 이미 구워진 방의 NavMesh를 다시 굽습니다. 방 콘텐츠가 꺼져 있으면(다른 방에 있는 동안) 굽는 동안만
    // 잠깐 켰다가 원래 상태로 되돌립니다(BuildRoom이 처음 지을 때와 같은 방식). bakeNavMeshPerRoom이 꺼져
    // 있었거나 NavMeshSurface가 없으면 아무것도 하지 않습니다.
    private void RebakeRoomNavMesh(GameObject contentGO)
    {
        if (!bakeNavMeshPerRoom) return;

        var surface = contentGO.GetComponent<NavMeshSurface>();
        if (surface == null) return;

        bool wasActive = contentGO.activeSelf;
        if (!wasActive) contentGO.SetActive(true);

        Physics.SyncTransforms();
        surface.BuildNavMesh();

        if (!wasActive) contentGO.SetActive(false);
    }

    // existingPlayer(유지할 플레이어)는 파괴하지 않습니다. 그 외의 경우는 기존과 동일합니다.
    private void ClearPreviousLevel(GameObject playerToPreserve)
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i);
            if (child.name != "LevelRoot") continue;

            if (Application.isPlaying) Destroy(child.gameObject);
            else DestroyImmediate(child.gameObject);
        }
        levelRoot = null;

        if (spawnedPlayer != null && spawnedPlayer != playerToPreserve)
        {
            if (Application.isPlaying) Destroy(spawnedPlayer);
            else DestroyImmediate(spawnedPlayer);
        }
        spawnedPlayer = null;
        spawnedPlayerController = null;

        roomControllers.Clear();
        cellToController.Clear();
        wallRoots.Clear();
        hasPlayerCell = false;
    }
}