using System;
using System.Collections.Generic;
using UnityEngine;

// 방 하나를 담당하는 컨트롤러입니다. LevelBuilder가 방을 만들 때 자동으로 붙입니다.
// - 플레이어가 이 방에 들어오면(LevelBuilder가 좌표 계산으로 판단해 EnterRoom을 호출):
//   이 방만 보이게 하고, 클리어 전이면 출입구를 즉시 잠급니다.
// - 클리어 조건(IRoomObjective)이 완료되면: 출입구를 엽니다.
//   보스방(RoomCategory.Boss)이면 추가로 그 자리에 사다리(포탈)를 스폰하고,
//   NormalRoomTreasureSpawner.SpawnBossRewardChest()로 확정 보상 상자도 하나 스폰합니다.
//   일반방(RoomCategory.Normal)이면 추가로 일반맵 보물상자 스폰을 시도합니다(아래 참고).
//
// [게이트(바리케이드) 잠금 타이밍 - 중요, v2]
// v1은 Start()에서 즉시 LockGates()를 호출했다가(방 Content가 아직 꺼져있어 사실상
// 무효), 나중에 플레이어가 실제로 경계선을 넘어 Content가 켜지는 순간 이미 잠긴
// 바리케이드와 곧바로 겹쳐버리는 문제가 있었습니다. 그래서 Start()의 즉시 잠금을 없애고
// EnterRoom() 이후 gateLockDelay만큼 유예를 두고 잠그도록 바꿨는데(v1.5), 이번엔
// 반대로 그 유예 시간 동안 플레이어가 바로 뒤로 후진하면 클리어 없이 이전 방으로
// 탈출할 수 있는 구멍이 생겼습니다.
//
// v2는 "언제 잠그느냐"가 아니라 "잠그기 전에 플레이어를 어디에 세워두느냐"로 접근을
// 바꿨습니다. LevelBuilder.Update()가 칸(cell) 전환을 감지하면, EnterRoom()을 호출하기
// 직전에 플레이어를 진입 방향으로 roomEntryPushDistance만큼 먼저 밀어넣어 바리케이드
// 구간보다 확실히 안쪽에 세워둡니다(LevelBuilder.PushPlayerForwardIntoRoom 참고). 그
// 다음에야 EnterRoom()이 호출되어 Content를 켜고 게이트를 유예 없이 "즉시" 잠급니다.
// 플레이어가 이미 바리케이드보다 안쪽에 있으므로 겹칠 일이 없고, 유예 시간 자체가
// 없으니 후진 탈출도 불가능합니다.
//
// [일반맵 보물상자 연동]
// 일반방이 클리어(=적 전멸)되는 순간, EnemyKillObjective가 들고 있던 "실제 스폰된 적 수"와
// "마지막으로 죽은 적의 위치"를 NormalRoomTreasureSpawner에 그대로 넘겨 보물상자 스폰을
// 시도합니다. objective 필드는 인터페이스(IRoomObjective) 타입이라 이 두 값에 접근할 수
// 없어서, EnemyKillObjective 구체 타입 참조(enemyKillObjective)를 별도로 하나 더 들고
// 있습니다 - 씬에 별도 오브젝트가 생기는 게 아니라, RoomController 내부에서만 쓰이는
// 순수 C# 참조라 플레이 중에는 보이지 않습니다(objective와 정확히 같은 객체를 가리킵니다).
//
// [코스트 지급 연동 - v1.23]
// 방 클리어 시 코스트(액티브 아이템 소모 자원)를 지급하는 로직은 PlayerResources.cs가
// 담당하는데, RoomController 입장에서는 "누가 듣고 있는지" 알 필요가 없어야 합니다. 그래서
// LevelPortal.OnAnyPortalInteractionRequested와 동일한 패턴으로 static 이벤트
// OnAnyRoomCleared를 노출해두고, HandleCleared()에서 이 방(RoomInstance)을 실어 흘려보냅니다.
// requiresClear가 일반방/보스방에만 true이므로, 이 이벤트도 자연히 그 두 종류에서만
// 발생합니다 - 나중에 전투방 종류가 늘어나 requiresClear 조건이 넓어지면 코스트 지급 대상도
// 자동으로 함께 넓어집니다.
//
// [전투 중 방을 벗어나면 방 초기화]
// 클리어하지 못한 전투방(일반방/보스방)에서 텔레포트 등으로 다른 방에 들어가면, 그 전투방은
// 나중에 다시 들어왔을 때 처음 상태로 돌아가 있습니다 - 이미 처치한 적도 되살아나고 깎인 체력도
// 가득 찹니다(적의 수/종류/위치는 처음과 같음). 다른 방에 들어오는 순간(EnterRoom) 직전에 있던
// 방이 "클리어 전 전투방"이었으면 그 방에 초기화 표시를 하고, 그 방에 다시 들어올 때 남은 적을
// 지우고 처음 스폰 기록대로 다시 만듭니다(IEnemyRespawner). 클리어된 방, 동아리방/상점 등
// 전투가 없는 방은 이 로직의 대상이 아니므로 어떤 상태도 바뀌지 않습니다(열린 상자가 다시
// 닫히는 일 없음). 플레이어가 사망 후 시작방에서 부활하는 경우도 시작방에 EnterRoom이 호출되기만
// 하면 똑같이 동작합니다.
public class RoomController : MonoBehaviour
{
    private RoomInstance roomData;
    private RoomVisibilityManager visibilityManager;
    private GameObject contentRoot;
    // 벽 전체가 아니라 "바리케이드"처럼 벽 프리팹 안의 일부 오브젝트만 담길 수 있습니다.
    // 실제로 무엇이 담기는지는 LevelBuilder.BuildWallSegment()가 결정합니다
    // (WallSidePrefab 컴포넌트가 있으면 그 barricade만, 없으면 벽 전체).
    private readonly List<GameObject> gates = new List<GameObject>();
    private IRoomObjective objective;
    private EnemyKillObjective enemyKillObjective; // objective와 동일한 객체를 구체 타입으로 참조 (일반/보스방만 사용)
    private bool requiresClear;
    private GameObject portalPrefab;
    private NormalRoomTreasureSpawner treasureSpawner;
    private IEnemyRespawner enemyRespawner;

    // 클리어하지 못한 채 플레이어가 이 방을 벗어났음을 뜻합니다. 다음에 이 방에 들어올 때 초기화합니다.
    private bool needsReset = false;

    // 플레이어가 가장 최근에 들어간 방입니다. 새 방에 들어올 때 "직전에 있던 방"이 어디였는지 알기
    // 위한 것으로, 방 안에서 칸만 옮겨 EnterRoom이 다시 호출되는 경우(같은 방)는 무시합니다.
    private static RoomController lastEnteredRoom;

    // [v1.17 변경 - 미니맵 오버레이 아이콘을 위한 읽기 전용 접근자]
    // 미니맵(MinimapDisplay)이 "이 방 안에 지금 어떤 아이템/보상이 남아있는지"를 알아내려면
    // 이 방의 콘텐츠 루트 밑을 직접 스캔해야 합니다(IMinimapMarker 구현체 검색). 기존
    // private 필드는 그대로 두고 읽기 전용 프로퍼티만 열었습니다.
    public GameObject ContentRoot => contentRoot;

    // 이 방이 "적 전멸 전까지 잠기는 전투방"(일반방/보스방)인지 알려주는 읽기 전용 접근자입니다.
    // LevelBuilder가 런타임에 방을 추가할 때(예: 16번 액티브 아이템) "적이 없는 방"인지 판단하는 데
    // 씁니다 - 판단 기준을 LevelBuilder에 중복으로 두지 않고 이 컨트롤러가 이미 쓰는 값을 그대로 읽습니다.
    public bool RequiresClear => requiresClear;

    // [9번 음식 연동] 일반방 보물상자를 복제할 때 원본과 동일한 기준(AssignBasicItems의 슬롯 수
    // 계산)을 쓰기 위해, 이 방에서 실제로 스폰된 적 수를 읽기 전용으로 노출합니다. 전투방이
    // 아니거나(enemyKillObjective가 없음) 아직 Start()가 실행되지 않았으면 0을 반환합니다.
    public int SpawnedEnemyCount => enemyKillObjective?.InitialCount ?? 0;

    // 방 하나가 클리어될 때마다(일반방/보스방만) 그 RoomInstance를 실어 흘려보냅니다.
    // 어떤 RoomController 인스턴스가 클리어되었는지와 무관하게 전역으로 받을 수 있도록
    // static 이벤트로 열어뒀습니다 (LevelPortal.OnAnyPortalInteractionRequested와 동일한
    // 패턴). 지금은 PlayerResources가 코스트 지급을 위해 구독합니다.
    public static event Action<RoomInstance> OnAnyRoomCleared;

    // [v1.25 추가 - 방 스코프 액티브 아이템 버프 해제용]
    // 플레이어가 이 방에 "들어올 때마다"(=EnterRoom이 호출될 때마다) 그 RoomInstance를
    // 실어 흘려보냅니다. LevelBuilder.Update()는 플레이어가 있는 칸(cell)이 실제로 바뀔
    // 때만 EnterRoom()을 호출하므로(같은 방에 머무는 동안은 절대 호출되지 않습니다 -
    // Levelbuilder.cs의 `if (hasPlayerCell && cell == currentPlayerCell) return;` 참고),
    // 이 이벤트는 자연히 "플레이어가 다른 방으로 넘어갔다"는 신호로 쓸 수 있습니다.
    // 지금은 방에 있는 동안만 유지되는 액티브 아이템 효과(예: Item43effect의 공격력 증가)가
    // "방을 나가면 해제"를 구현하기 위해 구독합니다.
    public static event Action<RoomInstance> OnAnyRoomEntered;

    // 방 알림을 받는 곳마다 따로 실행합니다 - 한 곳(아이템 효과 등)의 오류가 뒤에 등록된 곳(클리어 기록/크레딧 지급/효과음)을 막지 않게.
    private static void RaiseEach(Action<RoomInstance> handlers, RoomInstance room, string label)
    {
        if (handlers == null) return;
        foreach (Delegate d in handlers.GetInvocationList())
        {
            try { ((Action<RoomInstance>)d).Invoke(room); }
            catch (Exception e) { Debug.LogError($"RoomController: {label} 알림을 받는 곳에서 오류가 나 이 항목만 건너뜁니다: {e}"); }
        }
    }

    public void Setup(RoomInstance data, RoomVisibilityManager manager, GameObject content, GameObject portalPrefab, NormalRoomTreasureSpawner treasureSpawner, IEnemyRespawner enemyRespawner = null)
    {
        roomData = data;
        this.enemyRespawner = enemyRespawner;
        visibilityManager = manager;
        contentRoot = content;
        this.portalPrefab = portalPrefab;
        this.treasureSpawner = treasureSpawner;

        // 지금은 일반방/보스방만 "적 전멸 전까지 잠금" 대상입니다.
        // 동아리방/편의점/소비방/거래방/신비방/선택방 등은 전투 없는 방이라 잠그지 않습니다.
        requiresClear = data.category == RoomCategory.Normal || data.category == RoomCategory.Boss;
    }

    public void RegisterGate(GameObject gate)
    {
        gates.Add(gate);
    }

    void Start()
    {
        if (!requiresClear || roomData.isCleared) return;

        var kill = new EnemyKillObjective();
        kill.Initialize(contentRoot.transform);
        objective = kill;
        enemyKillObjective = kill;
        objective.OnCompleted += HandleCleared;

        // 여기서는 게이트를 건드리지 않습니다. 실제 잠금은 플레이어가 이 방에 들어와
        // EnterRoom()이 호출될 때 처리됩니다(클래스 주석 참고). 게이트는
        // BuildWallSegment에서 이미 기본적으로 열린 상태로 만들어집니다.
        if (objective.IsComplete)
        {
            // 적이 원래 0마리였던 경우(예: 더미 스폰 개수가 우연히 0인 경우) 즉시 클리어 처리합니다.
            HandleCleared();
        }
    }

    // 플레이어가 이 방에 들어왔을 때 처리 (LevelBuilder가 매 프레임 좌표 계산으로
    // 판단해 호출하거나, 시작방은 LevelBuilder가 플레이어 스폰 직후 직접 호출합니다).
    // LevelBuilder는 이 함수를 호출하기 "직전"에 플레이어를 진입 방향으로 미리 밀어넣어
    // 바리케이드 구간보다 안쪽에 세워두므로, 여기서는 유예 없이 곧바로 잠가도 안전합니다.
    //
    // [v1.16 변경 - 미니맵용 visited 플래그]
    // 미니맵(MinimapDisplay)이 "지나간 적 있는 방"을 구분해서 그릴 수 있도록, 이 방에
    // 처음 들어오는 순간을 그대로 재사용해 roomData.visited를 true로 남깁니다. 게이트/
    // 클리어 로직에는 전혀 영향을 주지 않는 별도의 읽기용 플래그입니다.
    public void EnterRoom()
    {
        roomData.visited = true;

        // 직전에 있던 다른 방이 클리어하지 못한 전투방이었다면 그 방에 "초기화 필요" 표시를 합니다.
        if (lastEnteredRoom != this)
        {
            if (lastEnteredRoom != null) lastEnteredRoom.HandlePlayerLeft();
            lastEnteredRoom = this;
        }

        // 방 스코프 액티브 아이템 버프 해제 등 "플레이어가 이 방에 들어왔다"에만 관심 있는
        // 리스너들에게 알립니다. 구독자가 없어도(?.Invoke) 안전합니다.
        RaiseEach(OnAnyRoomEntered, roomData, "방 입장");

        visibilityManager.ShowOnly(contentRoot);

        // 이 방이 클리어 전에 벗어났던 방이면, 방이 켜진 직후(NavMesh 등이 살아난 뒤)에 처음
        // 상태로 되돌립니다. 이 뒤에서 이어지는 게이트 잠금은 새 상태 기준으로 그대로 실행됩니다.
        if (needsReset) ResetCombatRoom();

        if (requiresClear && !roomData.isCleared)
        {
            LockGates();
        }
    }

    void OnDestroy()
    {
        if (lastEnteredRoom == this) lastEnteredRoom = null;
    }

    // 플레이어가 이 방을 벗어났을 때(다른 방의 EnterRoom이 호출될 때) 불립니다. 클리어 전 전투방이면
    // 다음 진입 때 초기화하도록 표시하고, 그 방에서 쏜 채 아직 날아다니는 적 탄환도 정리합니다.
    private void HandlePlayerLeft()
    {
        if (contentRoot == null) return;
        if (!requiresClear || roomData.isCleared) return;
        if (enemyRespawner == null) return;

        needsReset = true;

        // 적 탄환은 방 콘텐츠의 자식이 아니라 씬 최상위에 생성되어 방이 꺼진 뒤에도 계속 날아갑니다.
        // 전투가 이 방 하나에서만 일어나므로, 이 시점에 존재하는 적 탄환은 전부 이 방에서 나온 것입니다.
        var projectiles = FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None);
        for (int i = 0; i < projectiles.Length; i++)
        {
            Destroy(projectiles[i].gameObject);
        }
    }

    // 남아있는 적(죽어가는 중인 적 포함)을 전부 지우고 처음 스폰 기록대로 다시 만든 뒤, "적 전멸"
    // 클리어 조건도 새 적 기준으로 다시 만듭니다. 이 함수는 방 콘텐츠가 켜진 뒤에 호출되어야 합니다.
    private void ResetCombatRoom()
    {
        needsReset = false;

        if (enemyRespawner == null || roomData.isCleared) return;

        // 새 적을 만들기 전에 옛 적 목록을 먼저 확보합니다(새 적이 섞이지 않도록).
        var oldEnemies = contentRoot.GetComponentsInChildren<EnemyHealth>(true);

        var newEnemies = enemyRespawner.RespawnEnemies(roomData, contentRoot.transform);
        if (newEnemies == null || newEnemies.Count == 0)
        {
            Debug.LogWarning($"RoomController: 방 {roomData.id}({roomData.category})의 스폰 기록이 없어 초기화하지 못했습니다. 기존 상태를 그대로 둡니다.");
            return;
        }

        // 옛 적은 Destroy가 이번 프레임 끝에야 처리되므로, 그 전에 즉시 꺼서 한 프레임이라도
        // 공격하거나 움직이지 못하게 합니다.
        for (int i = 0; i < oldEnemies.Length; i++)
        {
            if (oldEnemies[i] == null) continue;
            oldEnemies[i].gameObject.SetActive(false);
            Destroy(oldEnemies[i].gameObject);
        }

        if (objective != null) objective.OnCompleted -= HandleCleared;

        var kill = new EnemyKillObjective();
        kill.InitializeWithEnemies(newEnemies);
        objective = kill;
        enemyKillObjective = kill;
        objective.OnCompleted += HandleCleared;

        if (objective.IsComplete)
        {
            HandleCleared();
        }
    }

    private void HandleCleared()
    {
        // [임시 디버그 로그 - 원인 확인 후 지워도 됩니다]
        Debug.Log($"RoomController: HandleCleared 실행됨 (category={roomData.category})");

        roomData.isCleared = true;
        UnlockGates();

        // 클리어 기록/코스트 지급 등 "방이 클리어됐다"에만 관심 있는 곳에 알립니다(받는 곳마다 따로 실행 - RaiseEach).
        RaiseEach(OnAnyRoomCleared, roomData, "방 클리어");

        if (roomData.category == RoomCategory.Boss)
        {
            SpawnPortal();

            // 보스방은 클리어 시 확정 보상 상자가 반드시 하나 나와야 합니다(형빈 확정 - 확률
            // 없이 항상 스폰). 일반방(TrySpawnTreasure)과 마찬가지로 부가 기능이라 예외가
            // 나도 방 클리어/포탈 스폰 자체를 막으면 안 되므로 동일하게 try/catch로 감쌉니다.
            if (treasureSpawner != null)
            {
                try
                {
                    treasureSpawner.SpawnBossRewardChest(roomData, contentRoot.transform);
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"RoomController: 보스방 보상 상자 스폰 중 오류가 발생해 건너뜁니다: {e}");
                }
            }
        }

        // 일반방 클리어(=적 전멸) 직후 보물상자 스폰을 시도합니다. enemyKillObjective가
        // 들고 있던 "실제 스폰된 적 수"와 "마지막으로 죽은 적의 위치"를 그대로 넘깁니다.
        // 등장 확률/위치 탐색/내용물 결정은 전부 NormalRoomTreasureSpawner 쪽 책임입니다.
        //
        // [try/catch로 감싼 이유]
        // HandleCleared()는 EnemyHealth.Die()가 OnDied 이벤트를 통해 동기적으로 호출하는
        // 체인의 일부입니다(Die -> OnDied.Invoke -> EnemyKillObjective.HandleEnemyDied ->
        // OnCompleted.Invoke -> 여기). 만약 이 안에서 예외가 발생하면 그 예외가 체인을 타고
        // 위로 전파되어, Die() 안에서 이 호출보다 "뒤에" 있는 Destroy(gameObject)가 아예
        // 실행되지 못합니다 - 그러면 마지막 적이 데미지도 정상으로 받고 공격도 계속하는데
        // 죽지는 않는 것처럼 보입니다. 보물상자 스폰은 부가 기능이라 여기서 문제가 생겨도
        // 방 클리어/적 처치 자체가 막히면 안 되므로 예외를 여기서 확실히 막습니다.
        if (roomData.category == RoomCategory.Normal && treasureSpawner != null && enemyKillObjective != null)
        {
            try
            {
                treasureSpawner.TrySpawnTreasure(roomData, contentRoot.transform, enemyKillObjective.InitialCount, enemyKillObjective.LastDeathPosition);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"RoomController: 보물상자 스폰 중 오류가 발생해 건너뜁니다 (방 클리어/적 처치 자체에는 영향 없음): {e}");
            }
        }
    }

    private void LockGates()
    {
        foreach (var gate in gates) gate.SetActive(true);
    }

    // 방이 클리어되어 바리케이드가 열릴 때, 뚝 꺼지지 않고 흰색으로 물들었다가 서서히 투명해지며
    // 사라지도록 DissolveAndVanish를 씁니다(EndAction.Deactivate - 다 사라진 뒤 Destroy가 아니라
    // SetActive(false)만 하므로 바리케이드 오브젝트/계층 구조는 그대로 남습니다). 이미 꺼져 있는
    // 게이트는 Update가 돌지 않아 페이드가 진행되지 않으므로, 그런 경우에는 예전처럼 바로 끕니다.
    private void UnlockGates()
    {
        foreach (var gate in gates)
        {
            if (gate == null) continue;

            if (gate.activeInHierarchy)
            {
                DissolveAndVanish.Begin(gate, DissolveAndVanish.EndAction.Deactivate);
            }
            else
            {
                gate.SetActive(false);
            }
        }
    }

    // n.5 선택방 레벨의 시작방처럼, "클리어"라는 개념 자체가 없는 방에도
    // LevelBuilder가 빌드 직후 즉시 포탈을 심을 수 있도록 외부에 노출한 진입점입니다.
    public void ForceSpawnPortalNow()
    {
        SpawnPortal();
    }

    // 보스방 클리어 시 이 방의 콘텐츠 루트 자리에 사다리(포탈)를 스폰합니다.
    // portalPrefab이 비어있으면 다른 플레이스홀더들과 동일한 방식으로 기본 Capsule을 생성합니다.
    private void SpawnPortal()
    {
        GameObject portalGO;

        if (portalPrefab != null)
        {
            portalGO = Instantiate(portalPrefab, contentRoot.transform.position, Quaternion.identity, contentRoot.transform);
        }
        else
        {
            portalGO = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            portalGO.name = "LevelPortal_Placeholder";
            portalGO.transform.SetParent(contentRoot.transform);
            portalGO.transform.position = contentRoot.transform.position + Vector3.up * 1f;
            portalGO.transform.localScale = new Vector3(1.2f, 1.2f, 1.2f);

            var rend = portalGO.GetComponent<Renderer>();
            if (rend != null) rend.material.color = new Color(0.3f, 0.8f, 1f); // 임시 식별용 하늘색

            // 플레이어 근접 감지를 트리거로 처리하므로, 기본 Capsule의 콜라이더를 트리거로 전환합니다.
            var col = portalGO.GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        if (portalGO.GetComponent<LevelPortal>() == null)
        {
            portalGO.AddComponent<LevelPortal>();
        }
    }
}
