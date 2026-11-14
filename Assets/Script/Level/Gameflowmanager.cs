using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 현재 레벨 번호와 페이즈(전투 레벨 vs n.5 선택방 레벨)를 추적하고 LevelBuilder에게 무엇을 빌드할지 지시하는 매니저입니다.
// 진행: 레벨 N 전투 -> N.5 선택방 -> N+1 전투 ... (정수 레벨 번호 + LevelPhase, 표시는 DisplayLabel). 전투 레벨 설정은 combatLevelConfigs 순서.
// gameClearLevel 레벨 보스를 깨고 헬기 [예]를 누르면 다음 레벨 대신 게임 클리어 결과 화면(GameClearUI)이 뜹니다(LevelPortal).
// 결과 화면용 기록(전투 시간/처치한 적/클리어한 방)도 여기서 쌓습니다 - 씬을 새로 불러오면(재시작/새 게임) 0부터.
// skipChoiceLevels가 켜져 있으면 n.5를 건너뛰고 레벨 N 전투 -> N+1 전투로 바로 갑니다(데모용 - n.5 코드는 그대로 남겨 둠).
// 레벨을 만들 때 그 레벨 설정의 배경 재질로 화면 배경/초상화 하늘을 바꿉니다(LevelBackground). 설계 배경은 Docs/설계노트.md [게임 흐름].
public class GameFlowManager : MonoBehaviour
{
    public enum LevelPhase { Combat, Choice }

    [Header("참조")]
    public LevelBuilder levelBuilder;

    [Header("전투 레벨 설정 목록 (인덱스 0 = 레벨 1, 인덱스 1 = 레벨 2 ...)")]
    [Tooltip("레벨이 늘어날 때마다 여기에 RoomGenerationConfig 에셋을 순서대로 추가하세요.")]
    public List<RoomGenerationConfig> combatLevelConfigs;

    [Header("n.5 선택방 레벨")]
    [Tooltip("켜면 n.5 선택방 레벨을 건너뛰고 전투 레벨에서 바로 다음 전투 레벨로 갑니다(1 -> 2). " +
             "끄면 예전처럼 1 -> 1.5 -> 2로 진행합니다. 최대 체력 칸 정리는 어느 쪽이든 다음 전투 레벨로 들어가기 직전에 합니다.")]
    public bool skipChoiceLevels = true;

    [Header("게임 클리어")]
    [Tooltip("이 번호의 전투 레벨 보스를 깨고 헬기 [예]를 누르면 다음 레벨로 가지 않고 게임 클리어 결과 화면이 뜹니다. 0이면 끔(계속 다음 레벨로).")]
    [Min(0)] public int gameClearLevel = 2;

    [Header("밤 전용 빛 자동 부착")]
    [Tooltip("Night Light Auto Rules 에셋. 전투 레벨이 만들어진 직후 같은 모델을 쓰는 오브젝트에 밤 전용 빛을 자동으로 붙입니다. 비우면 자동 부착 안 함.")]
    public NightLightAutoRules nightLightRules;

    [Header("현재 상태 (자동 갱신 - 직접 수정하지 마세요)")]
    [SerializeField] private int currentLevelNumber = 1;
    [SerializeField] private LevelPhase currentPhase = LevelPhase.Combat;

    public int CurrentLevelNumber => currentLevelNumber;
    // 지금이 게임 클리어 레벨(의 전투 단계)인지 - LevelPortal이 헬기 [예]에서 확인합니다.
    public bool IsGameClearLevel => gameClearLevel > 0 && currentPhase == LevelPhase.Combat && currentLevelNumber == gameClearLevel;
    public LevelPhase CurrentPhase => currentPhase;
    public string DisplayLabel => currentPhase == LevelPhase.Combat
        ? $"레벨 {currentLevelNumber}"
        : $"레벨 {currentLevelNumber}.5";

    // [획득 시 1회성 지급 기록 - 교체 반복 복제 방지 (2026-10 개편, 형빈 확정)]
    // 아이템을 얻을 때 주는 1회성 스탯/재화(10/13/14/17/20/21/26/32/34/37/39/45/54/55/57/66번 등)는 "정당하게 새로 나온 사본"을
    // 얻을 때마다 받을 수 있고, "교체/버리기/칸 감소로 플레이어가 월드에 내놓은 사본"을 다시 주울 때는 절대 받지 못합니다.
    //
    // [동작]
    // - 각 효과는 지급 직전에 TryConsumeOneTimeGrant("{아이템ID}_용도")로 기록을 겁니다. 이미 걸려 있으면 지급하지 않습니다.
    // - 이 기록은 런 전체에서 유지됩니다(다음 레벨로 넘어가도 지우지 않음 - 장착한 채 다음 레벨로 가서 버렸다 다시 줍는 복제 방지).
    //   새 게임을 시작할 때만 지웁니다.
    // - 보물상자/동아리방·특수방 받침대/상점이 "새로 뽑은 사본"을 플레이어에게 줄 때만 AcquireItemCopy(.., isFreshCopy: true, ..)로
    //   그 아이템의 기록을 잠깐 풀고 장착합니다(장착하면서 지급되고, 끝나면 기록을 다시 겁니다).
    // - 교체로 상자/받침대에 들어간 아이템, 상점 보관 좌대/버린 아이템 좌대의 아이템은 기록을 풀지 않으므로 다시 주워도 지급이 없습니다.
    // - 48번/56번처럼 처음부터 제한 없이 주는 경로는 PassiveEffectContext.IgnoreOneTimeGrantLimit로 이 기록을 거치지 않습니다.
    // - 이 매니저가 없는 씬(아이템 테스트 씬)에서는 각 효과가 경고만 남기고 지급합니다(예전과 같음).
    //
    // 기록 이름은 반드시 "{아이템ID}_용도" 형식이어야 합니다 - 새 사본을 줄 때 "{아이템ID}_"로 시작하는 기록을 풀기 때문입니다.
    // 형식을 지키지 않은 기록은 풀리지 않아 "지급이 안 되는" 쪽으로만 틀립니다(교체 복제가 생기는 쪽으로는 틀리지 않음).
    private readonly HashSet<string> grantedOneTimeIds = new HashSet<string>();

    // key가 아직 걸려 있지 않으면 걸고 true를 반환합니다(=이번이 그 지급을 실제로 적용해야 하는 순간). 이미 걸려 있으면
    // 아무것도 바꾸지 않고 false를 반환합니다(=건너뛰어야 함). key는 "{itemId}_heal"처럼 아이템ID로 시작해야 합니다(위 주석 참고).
    public bool TryConsumeOneTimeGrant(string key)
    {
        if (string.IsNullOrEmpty(key)) return false;
        return grantedOneTimeIds.Add(key); // HashSet.Add는 이미 있으면 false, 새로 추가되면 true
    }

    // 아이템을 플레이어에게 줍니다. isFreshCopy가 true(보물상자/받침대/상점이 새로 뽑은 사본)이면 그 아이템의 1회성 지급 기록을
    // 잠깐 풀어서 이번 획득에서 지급되게 하고, 획득이 끝나면(성공/실패 모두) 풀었던 기록을 다시 겁니다 - 그래서 이 사본이 나중에
    // 교체로 월드에 나갔다가 다시 주워져도 지급되지 않습니다. isFreshCopy가 false면(교체로 들어온 사본) 기록을 건드리지 않고
    // 그냥 획득만 합니다. acquire는 실제 획득(장착) 처리이며, 성공하면 true를 돌려줘야 합니다.
    public static bool AcquireItemCopy(ItemDefinition item, bool isFreshCopy, System.Func<bool> acquire)
    {
        if (acquire == null) return false;
        if (!isFreshCopy || item == null || string.IsNullOrEmpty(item.itemId)) return acquire();

        var gameFlow = FindFirstObjectByType<GameFlowManager>();
        if (gameFlow == null) return acquire(); // 테스트 씬 등 - 기록이 없으므로 그대로 획득

        List<string> released = gameFlow.ReleaseOneTimeGrantsFor(item.itemId);
        try
        {
            return acquire();
        }
        finally
        {
            // 이번 획득에서 효과가 다시 건 기록은 그대로 걸려 있고, 다시 걸지 않은 기록(지급할 게 없었거나 획득 실패)도
            // 원래대로 걸어 둡니다 - 이 사본이 나중에 다시 주워질 때 지급되지 않도록.
            foreach (var key in released) gameFlow.grantedOneTimeIds.Add(key);
        }
    }

    // "{itemId}_"로 시작하는 1회성 지급 기록을 지우고, 지운 기록 이름들을 돌려줍니다.
    private List<string> ReleaseOneTimeGrantsFor(string itemId)
    {
        string prefix = itemId + "_";
        var released = new List<string>();
        foreach (var key in grantedOneTimeIds)
        {
            if (key.StartsWith(prefix, System.StringComparison.Ordinal)) released.Add(key);
        }
        foreach (var key in released) grantedOneTimeIds.Remove(key);
        return released;
    }

    // [레벨 내 아이템 중복 등장 방지] 패시브/액티브/장신구/일회용은 "보물상자/받침대/상점이 내용물로 확정한 순간"에 기록합니다
    // (줍지 않고 넘어가도 그 레벨에서는 소진된 것 - 획득 여부만 보면 줍지 않은 아이템이 다른 상자에서 또 나옴).
    // 확정 지점: NormalRoomTreasureSpawner.ResolvePassiveItemCandidate(), ItemPedestal.Open(). 기본 재화 묶음은 대상 아님.
    // 다음 전투 레벨로 넘어갈 때 초기화(StartNewGame/EnterNextCombatLevel) - 1회성 지급 기록과 달리 런 전체에서 유지하지 않습니다.
    private readonly HashSet<string> assignedUniqueItemIdsThisLevel = new HashSet<string>();

    // 보물상자가 아이템을 실제 내용물로 확정하는 바로 그 시점에 호출합니다(뽑기 직후,
    // 플레이어가 줍기 전이어도 상관없습니다 - "뽑힌 순간" 그 레벨에서는 소진된 것으로
    // 취급합니다).
    public void MarkItemAssignedThisLevel(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return;
        assignedUniqueItemIdsThisLevel.Add(itemId);
    }

    // [상점 갱신하기 연동] 기록을 하나 지웁니다. 상점에 진열됐다가 사지 않은 채 갱신으로 사라진 아이템을 다시 보물상자/
    // 48번/56번 후보로 돌려보낼 때 ShopNPC.TryRefresh가 부릅니다(형빈 확정). 상점은 이미 기록된 아이템을 진열하지 않으므로,
    // 지우는 기록은 항상 그 상점이 남긴 것입니다.
    public void UnmarkItemAssignedThisLevel(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return;
        assignedUniqueItemIdsThisLevel.Remove(itemId);
    }

    // 전달받은 풀에서, "이 레벨의 어느 보물상자에선가 이미 내용물로 확정된 적이 있는
    // 아이템"을 걸러낸 목록을 반환합니다. FilterUnowned/FilterOutEquipped들과 함께, 뽑기
    // 직전에 호출하면 됩니다(ItemPedestal.Open() / NormalRoomTreasureSpawner.
    // ResolvePassiveItemCandidate() 참고).
    // [상극 아이템 - 99번 때 추가, 형빈 확정] 이 레벨에서 이미 나온 아이템과 같이 가질 수 없는 아이템(예: 69번이 상자에 떴으면 99번)도 뺍니다 -
    // 줍지 않고 넘어갔어도 그 레벨의 다른 보물상자/상점에서 나오지 않습니다(ItemCompatibility.cs 참고).
    public List<ItemDefinition> FilterOutAlreadyAssignedThisLevel(List<ItemDefinition> pool)
    {
        if (pool == null) return new List<ItemDefinition>();
        if (assignedUniqueItemIdsThisLevel.Count == 0) return pool;

        System.Func<string, ItemDefinition> resolve = ResolveItemById();
        return pool
            .Where(item => item != null && !assignedUniqueItemIdsThisLevel.Contains(item.itemId) &&
                           !ItemCompatibility.ConflictsWithIds(item, assignedUniqueItemIdsThisLevel, resolve))
            .ToList();
    }

    // 아이템 ID로 ItemDefinition을 찾는 함수입니다(LevelBuilder의 Item Database 기준). 못 찾으면 null을 돌려주는 함수가 됩니다.
    private System.Func<string, ItemDefinition> ResolveItemById()
    {
        var database = levelBuilder != null ? levelBuilder.itemDatabase : null;
        if (database == null || database.allItems == null) return id => null;

        var map = new Dictionary<string, ItemDefinition>();
        foreach (var item in database.allItems)
        {
            if (item == null || string.IsNullOrEmpty(item.itemId)) continue;
            if (!map.ContainsKey(item.itemId)) map.Add(item.itemId, item);
        }
        return id => id != null && map.TryGetValue(id, out var found) ? found : null;
    }

    // ---------- 게임 클리어 결과 화면용 기록 ----------
    // 전투 시간 = 새 게임 시작부터 마지막 헬기 [예]까지(StopRunTimer). 게임 시간(Time.deltaTime)으로 재므로 일시 정지(시간 멈춤)는 빠지고,
    // 로딩 화면이 떠 있는 동안도 뺍니다. 처치한 적 = 전역 처치 알림(87번 아군의 죽음은 원래 오지 않음),
    // 클리어한 방 = 방 클리어 알림(일반방/보스방만, 방마다 한 번).
    private float playTimeSeconds;
    private int enemiesKilled;
    private int roomsCleared;
    private bool runTimerStopped;

    public float PlayTimeSeconds => playTimeSeconds;
    public int EnemiesKilled => enemiesKilled;
    public int RoomsCleared => roomsCleared;

    // 마지막 헬기 [예]에서 부릅니다. 이후로는 시간/처치/방 기록이 늘지 않습니다.
    public void StopRunTimer()
    {
        runTimerStopped = true;
    }

    void OnEnable()
    {
        EnemyHealth.OnAnyEnemyKilled += HandleEnemyKilled;
        RoomController.OnAnyRoomCleared += HandleRoomCleared;
    }

    void OnDisable()
    {
        EnemyHealth.OnAnyEnemyKilled -= HandleEnemyKilled;
        RoomController.OnAnyRoomCleared -= HandleRoomCleared;
    }

    void Update()
    {
        if (!runTimerStopped && !LoadingScreen.IsShowing) playTimeSeconds += Time.deltaTime;
    }

    private void HandleEnemyKilled(EnemyHealth enemy, Vector3 position)
    {
        if (!runTimerStopped) enemiesKilled++;
    }

    private void HandleRoomCleared(RoomInstance room)
    {
        if (!runTimerStopped) roomsCleared++;
    }

    [Header("동작 설정")]
    [Tooltip("Play 모드 진입 시 자동으로 새 게임(레벨 1)을 시작할지 여부입니다.")]
    public bool startOnAwake = true;

    void Start()
    {
        // 예전에 LevelBuilder와 다른 빌더가 동시에 자동 빌드를 시도해서 LevelRoot가
        // 중복 생성된 적이 있어서, 같은 실수가 반복되지 않도록 미리 경고합니다.
        if (levelBuilder != null && levelBuilder.buildOnStart)
        {
            Debug.LogWarning("GameFlowManager: LevelBuilder의 Build On Start가 켜져 있으면 " +
                              "GameFlowManager와 동시에 레벨을 빌드해 레벨이 중복 생성될 수 있습니다. " +
                              "LevelBuilder 인스펙터에서 Build On Start를 꺼주세요.");
        }

        if (startOnAwake) StartNewGame();
    }

    // ---------- 새 게임 시작 (레벨 1 전투부터) ----------
    // [로딩 화면] 씬에 LoadingScreen이 있으면 검은 로딩 화면을 띄운 채 레벨을 만듭니다(없으면 예전과
    // 100% 동일). 씬이 막 열린 Start에서 불리므로 화면을 한 번도 그리기 전이라, 한 프레임 기다리지 않고
    // 바로 만듭니다 - 다른 스크립트들의 Start 순서도 예전과 똑같이 유지됩니다.
    [ContextMenu("Start New Game (Level 1)")]
    public void StartNewGame()
    {
        LoadingScreen.RunLevelWork(StartNewGameNow, false);
    }

    private void StartNewGameNow()
    {
        currentLevelNumber = 1;
        currentPhase = LevelPhase.Combat;
        grantedOneTimeIds.Clear();
        assignedUniqueItemIdsThisLevel.Clear();
        playTimeSeconds = 0f;
        enemiesKilled = 0;
        roomsCleared = 0;
        runTimerStopped = false;
        BuildCurrentPhase(null); // 최초 진입이므로 유지할 플레이어가 없음 = 새로 스폰
    }

    // ---------- 다음 페이즈로 전환 ----------
    // LevelPortal(헬기 확인창 [예])이 부릅니다. ContextMenu(TestAdvanceToNextPhase)로도 부를 수 있습니다.
    //
    // [로딩 화면] 씬에 LoadingScreen이 있으면 검은 로딩 화면을 먼저 한 프레임 그린 뒤 레벨을 만듭니다
    // (빌드 순간의 끊김과 플레이어 낙하를 가림). 그 한 프레임 사이에 [예]가 두 번 눌려 두 레벨을
    // 건너뛰는 일이 없도록, 전환이 예약되어 있는 동안 들어온 요청은 무시합니다.
    public void AdvanceToNextPhase()
    {
        if (advancePending) return;
        advancePending = true;
        LoadingScreen.RunLevelWork(AdvanceToNextPhaseNow, true);
    }

    private bool advancePending;

    private void AdvanceToNextPhaseNow()
    {
        advancePending = false;

        if (levelBuilder == null)
        {
            Debug.LogWarning("GameFlowManager: levelBuilder가 연결되어 있지 않습니다.");
            return;
        }

        GameObject playerToKeep = levelBuilder.CurrentPlayer;
        if (playerToKeep == null)
        {
            Debug.LogWarning("GameFlowManager: 유지할 플레이어가 없습니다 (아직 레벨이 빌드되지 않은 상태일 수 있습니다). " +
                              "새 플레이어로 진행합니다.");
        }

        switch (currentPhase)
        {
            case LevelPhase.Combat:
                if (skipChoiceLevels)
                {
                    // [n.5 건너뛰기 - 데모, 형빈 확정] 전투 레벨 클리어 -> 다음 번호의 전투 레벨로 바로 갑니다.
                    // n.5 레벨이 없으니 최대 체력 칸 정리도 여기서 합니다(전투 레벨의 상점이 마지막 회복 기회).
                    EnterNextCombatLevel(playerToKeep);
                }
                else
                {
                    // 전투 레벨 클리어 -> 같은 번호의 n.5 선택방 레벨로
                    currentPhase = LevelPhase.Choice;
                }
                break;
            case LevelPhase.Choice:
                // 선택방 레벨 통과 -> 다음 번호의 전투 레벨로
                EnterNextCombatLevel(playerToKeep);
                break;
        }

        BuildCurrentPhase(playerToKeep);
        // [헬기 퇴각 -> 도착] 헬기 사다리로 넘어왔으면, 아직 검은 로딩 화면 뒤인 지금 다음 레벨 시작방에 도착 연출(사다리 타고 내려오기)을 겁니다.
        HeliExtraction.TryStartArrival(levelBuilder);
    }

    // 다음 번호의 전투 레벨로 넘어가는 상태 처리입니다(n.5를 거쳐 오든 건너뛰든 같은 처리).
    private void EnterNextCombatLevel(GameObject playerToKeep)
    {
        // [최대 체력 정리 - 형빈 확정] 다음 전투 레벨로 들어가기 직전(로딩 화면 뒤)에 채워지지 않은 최대 체력 칸을 1칸 단위로
        // 지웁니다(3/5 -> 3/3, 3.5/5 -> 3.5/4). n.5를 거칠 때는 전투 레벨 -> n.5에서 지우지 않습니다(n.5 레벨이 마지막 회복 기회).
        ApplyLevelEndMaxHealthLoss(playerToKeep);
        currentLevelNumber++;
        currentPhase = LevelPhase.Combat;
        // 1회성 지급 기록(grantedOneTimeIds)은 런 전체에서 유지합니다(위 [획득 시 1회성 지급 기록] 참고).
        assignedUniqueItemIdsThisLevel.Clear();
        // [들고 온 슬롯 아이템 - 형빈 확정] 지금 가지고 있는 슬롯 아이템(액티브/장신구/일회용/음식)은 이 레벨에 이미 나온 것으로
        // 기록합니다 - 이 레벨에서 교체/버리기/칸 감소로 내려놓아도 다른 보물상자/상점에서 같은 아이템이 또 나오지 않게 합니다.
        // 들고 온 일회용/음식을 이 레벨에서 다 써도 이 레벨에서는 다시 나오지 않습니다.
        MarkCarriedSlotItemsAssigned(playerToKeep);
    }

    // [최대 체력 정리] 유지되는 플레이어의 채워지지 않은 최대 체력을 지웁니다(PlayerHealth.ApplyLevelEndMaxHealthLoss).
    private static void ApplyLevelEndMaxHealthLoss(GameObject player)
    {
        if (player == null) return;
        var health = player.GetComponent<PlayerHealth>();
        if (health != null) health.ApplyLevelEndMaxHealthLoss();
    }

    // [들고 온 슬롯 아이템] 플레이어가 지금 장착 중인 슬롯 아이템을 전부 "이 레벨에 이미 나온 아이템"으로 기록합니다.
    private void MarkCarriedSlotItemsAssigned(GameObject player)
    {
        if (player == null) return;

        var active = player.GetComponent<PlayerActiveItem>();
        if (active != null && active.EquippedItem != null) MarkItemAssignedThisLevel(active.EquippedItem.itemId);

        var trinket = player.GetComponent<PlayerTrinket>();
        if (trinket != null) MarkAll(trinket.EquippedItems);

        var disposable = player.GetComponent<PlayerDisposableItem>();
        if (disposable != null) MarkAll(disposable.EquippedItems);

        var food = player.GetComponent<PlayerFoodItem>();
        if (food != null) MarkAll(food.EquippedItems);
    }

    private void MarkAll(IReadOnlyList<ItemDefinition> items)
    {
        if (items == null) return;
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] != null) MarkItemAssignedThisLevel(items[i].itemId);
        }
    }

    [ContextMenu("TEST: Advance To Next Phase")]
    public void TestAdvanceToNextPhase() => AdvanceToNextPhase();

    // ---------- 현재 상태(레벨 번호 + 페이즈)에 맞는 레벨을 실제로 빌드 ----------
    private void BuildCurrentPhase(GameObject existingPlayer)
    {
        if (levelBuilder == null)
        {
            Debug.LogWarning("GameFlowManager: levelBuilder가 연결되어 있지 않습니다.");
            return;
        }

        switch (currentPhase)
        {
            case LevelPhase.Combat:
                var combatConfig = GetCombatConfigFor(currentLevelNumber);
                if (combatConfig == null) return; // 경고는 GetCombatConfigFor 안에서 이미 출력됨
                levelBuilder.config = combatConfig;
                // [레벨별 조명] 레벨을 만들기 전에 적용합니다(밤 전용 조명 프리팹이 만들어지는 순간 밤인지 알 수 있도록). 비어 있으면 원래 조명.
                LevelLighting.Apply(combatConfig.lightingProfile);
                levelBuilder.BuildLevel(existingPlayer);
                NightLightAutoRules.AttachInLevel(levelBuilder, nightLightRules); // [밤 전용 빛] 같은 모델에 견본 빛 자동 부착(밤이 아니면 숨겨진 채로 있음)
                LevelBackground.Apply(combatConfig.backgroundMaterial); // [레벨별 배경] 비어 있으면 원래 배경
                break;

            case LevelPhase.Choice:
                LevelLighting.Apply(null); // [레벨별 조명] n.5 선택방 레벨은 항상 원래 조명(1레벨과 같은 밝은 낮 - 형빈 확정)
                levelBuilder.BuildChoiceLevel(existingPlayer);
                LevelBackground.Apply(levelBuilder.choiceLevelConfig != null ? levelBuilder.choiceLevelConfig.backgroundMaterial : null);
                break;
        }

        Debug.Log($"GameFlowManager: {DisplayLabel} 진행 중");
    }

    private RoomGenerationConfig GetCombatConfigFor(int levelNumber)
    {
        int index = levelNumber - 1;
        if (combatLevelConfigs == null || index < 0 || index >= combatLevelConfigs.Count || combatLevelConfigs[index] == null)
        {
            Debug.LogWarning($"GameFlowManager: 레벨 {levelNumber}에 대한 RoomGenerationConfig가 " +
                              "combatLevelConfigs에 없습니다. 인스펙터에서 리스트를 확인해주세요.");
            return null;
        }
        return combatLevelConfigs[index];
    }
}