using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

// [플레이 통계 (형빈 확정)]
// 판(런)마다 기록을 하나 만들어, 판이 끝나는 순간 구글 시트(Apps Script 웹 앱)로 한 번 보냅니다. 설정은 PlayStatsSettings.
//
// [판 기록에 담기는 것]
// - 시작 방식(startType): title(타이틀에서 시작) / restart(R 재시작·게임오버 다시하기·ESC 처음부터·클리어 후 다시하기) / direct(에디터에서 바로 플레이)
// - 종료 방식(endType): clear / death / restart(끝나기 전에 재시작) / quit_title(타이틀로 나감) / quit_game(게임 종료) / crash(강제 종료 - 다음 실행 때 보냄)
// - 리롤(reroll): 시작방 밖으로 한 번도 나가지 않고 끝난 판이면 1 (형빈 확정 기준)
// - 플레이 시간(playTime, 초): 클리어 화면의 전투 시간과 같은 타이머(일시 정지/로딩 제외)
// - 도달 레벨, 보스방에 들어간 레벨들, 처음 들어간 방 개수(시작방 제외)
// - 사망: 레벨, 방 종류(+특수방 레이아웃 등 variant), 그 방이 이번 판/이번 레벨에서 몇 번째로 처음 들어간 방인지, 원인(PlayerDamageSource)
// - 도감: 수록 개수/전체 아이템 수/수록된 아이템 ID 전체
// - 이번 판에서 얻은 아이템, 등장한 아이템(보물상자/받침대/상점에 배정된 것), 끝날 때 가지고 있던 아이템과 등급별 개수
// - 처치한 적 수, 클리어한 방 수
//
// [에러 기록] 게임 실행 중 난 에러(Error/Exception)를 같은 내용은 한 번만, 실행당 최대 maxErrorsPerSession개 보냅니다.
// 보내기 전에 파일 경로/사용자 이름/PC 이름을 지웁니다(개인정보 보호 - 형빈 확정).
//
// [안전 장치]
// - 통계 코드는 어떤 경우에도 예외를 밖으로 내보내지 않습니다. 인터넷이 없거나 서버가 응답하지 않아도 게임은 그대로 진행됩니다.
// - 보내지 못한 기록은 PC에 저장해 두었다가 다음 실행 때 다시 보냅니다(최대 maxQueuedRecords개).
// - 진행 중인 판은 15초마다/방에 들어갈 때마다 PC에 저장해 두므로, 강제 종료돼도 다음 실행 때 crash로 보냅니다.
// - 같은 기록이 두 번 전송될 수 있어(전송 직후 게임이 꺼지는 경우) 모든 기록에 고유 번호(recordId)를 붙입니다 - 집계할 때 중복을 걸러냄.
// - 유니티 에디터에서 한 판은 기본적으로 보내지 않습니다(PlayStatsSettings.sendInEditor). 아이템 테스트 씬은 기록하지 않습니다.
// - 게임은 보내기만 하고 서버 응답 내용으로 아무것도 하지 않습니다.
//
// [저장 위치] Application.persistentDataPath/play_stats.json (도감 기록과 같은 폴더). 무작위 플레이어 번호와 보내지 못한 기록이 들어 있습니다.
//
// [연결 지점] GameFlowManager.StartNewGameNow -> BeginRun, GameClearUI.Show -> NotifyGameClear, ItemCodexRecord.MarkAcquired -> NoteItemAcquired,
// GameFlowManager.MarkItemAssignedThisLevel -> NoteItemAppeared. 사망/방 진입/씬 전환/게임 종료/에러는 여기서 직접 구독합니다.
public static class RunAnalytics
{
    public const string StartTitle = "title";
    public const string StartRestart = "restart";
    public const string StartDirect = "direct";

    public const string EndClear = "clear";
    public const string EndDeath = "death";
    public const string EndRestart = "restart";
    public const string EndQuitTitle = "quit_title";
    public const string EndQuitGame = "quit_game";
    public const string EndCrash = "crash";

    private const string StateFileName = "play_stats.json";
    private const float SnapshotInterval = 15f; // 진행 중인 판을 PC에 저장하는 간격(초)
    private const float RetryInterval = 60f;    // 전송 실패 후 다시 시도할 때까지(초)
    private const string LogPrefix = "[플레이 통계]";

    // ---------------- 보내는 기록 형식 ----------------
    // 필드 이름이 그대로 시트의 열 이름이 됩니다(Apps Script의 RUN_COLUMNS/ERROR_COLUMNS와 맞아야 함).

    [Serializable]
    private class RunRecord
    {
        public string k;
        public string type = "run";
        public string recordId;
        public string version;
        public int editor;
        public string playerId;
        public string startType;
        public string endType;
        public int reroll;
        public int playTime;
        public string levelReached;
        public string bossReached;
        public int roomsEntered;
        public string deathLevel;
        public string deathRoom;
        public string deathRoomVariant;
        public int deathRoomOrder;
        public int deathRoomOrderInLevel;
        public string deathAttacker;
        public string deathAttack;
        public string deathEnemy;
        public int codexCount;
        public int codexTotal;
        public string codexIds;
        public string acquired;
        public string appeared;
        public string held;
        public int heldT0;
        public int heldT1;
        public int heldT2;
        public int heldT3;
        public int enemiesKilled;
        public int roomsCleared;
    }

    [Serializable]
    private class ErrorRecord
    {
        public string k;
        public string type = "error";
        public string recordId;
        public string version;
        public int editor;
        public string playerId;
        public string runId;
        public string level;
        public string scene;
        public string message;
        public string stack;
    }

    [Serializable]
    private class SaveData
    {
        public string playerId;
        public string openRun;                         // 진행 중인 판(강제 종료 대비). 판이 정상적으로 끝나면 비움
        public List<string> queue = new List<string>(); // 보내지 못한 기록(JSON)
    }

    // 진행 중인 판의 상태입니다.
    private class RunState
    {
        public RunRecord record;
        public GameFlowManager flow;
        public string sceneName;
        public bool finished;

        public readonly Dictionary<RoomInstance, Vector2Int> roomOrder = new Dictionary<RoomInstance, Vector2Int>(); // x = 판 기준 순서, y = 레벨 기준 순서
        public RoomInstance lastRoom;
        public int runRoomCount;
        public int levelRoomCount;
        public string countingLevel;

        public readonly HashSet<string> acquired = new HashSet<string>();
        public readonly HashSet<string> appeared = new HashSet<string>();
        public readonly List<string> bossReached = new List<string>();

        public HashSet<string> databaseIds; // 전체 아이템 ID(도감 분모)
    }

    private static bool initialized;
    private static SaveData save;
    private static RunState current;
    private static Runner runner;
    private static bool sending;
    private static float nextSendTime;
    private static float nextSnapshotTime;
    // [시작 방식 판별] 게임 안에서 씬을 바꾸기 직전(LoadingScreen.OnBeforeSceneChange)에 열려 있던 씬 이름입니다.
    // 빌드의 비동기 씬 불러오기에서는 sceneLoaded 알림이 새 씬의 Start보다 늦게 올 수 있어, sceneLoaded 대신 이 값으로 판별합니다.
    private static string sceneBeforeChange = "";
    private static readonly HashSet<string> reportedErrors = new HashSet<string>();
    private static int errorCount;
    private static bool warnedNoUrl;

    private static string FilePath => Path.Combine(Application.persistentDataPath, StateFileName);

    // ---------------- 시작 준비 ----------------

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        initialized = false;
        save = null;
        current = null;
        runner = null;
        sending = false;
        nextSendTime = 0f;
        nextSnapshotTime = 0f;
        sceneBeforeChange = "";
        reportedErrors.Clear();
        errorCount = 0;
        warnedNoUrl = false;
    }

    // 첫 씬이 열리기 전에 준비합니다(첫 씬부터 사망/방 진입/에러 알림을 놓치지 않도록).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InitOnLoad()
    {
        EnsureInit();
    }

    private static void EnsureInit()
    {
        if (initialized) return;
        initialized = true;

        try
        {
            LoadState();

            // 지난 실행에서 끝나지 않은 판이 남아 있으면 강제 종료(crash)로 보냅니다.
            if (!string.IsNullOrEmpty(save.openRun))
            {
                var unfinished = JsonUtility.FromJson<RunRecord>(save.openRun);
                save.openRun = null;
                if (unfinished != null && ShouldQueue())
                {
                    unfinished.endType = EndCrash;
                    Enqueue(JsonUtility.ToJson(unfinished));
                }
                SaveState();
            }

            // 도메인 다시 불러오기를 끈 에디터에서도 두 번 구독되지 않도록 먼저 뺍니다.
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            PlayerHealth.OnAnyPlayerDied -= HandlePlayerDied;
            PlayerHealth.OnAnyPlayerDied += HandlePlayerDied;
            RoomController.OnAnyRoomEntered -= HandleRoomEntered;
            RoomController.OnAnyRoomEntered += HandleRoomEntered;
            LoadingScreen.OnBeforeSceneChange -= HandleBeforeSceneChange;
            LoadingScreen.OnBeforeSceneChange += HandleBeforeSceneChange;
            Application.quitting -= HandleQuitting;
            Application.quitting += HandleQuitting;
            Application.logMessageReceived -= HandleLog;
            Application.logMessageReceived += HandleLog;

            EnsureRunner();
        }
        catch (Exception e)
        {
            Warn("준비 중 문제가 생겼습니다(게임 진행에는 영향 없음)", e);
        }
    }

    private static void EnsureRunner()
    {
        if (runner != null) return;
        var go = new GameObject("RunAnalytics");
        go.hideFlags = HideFlags.HideInHierarchy;
        UnityEngine.Object.DontDestroyOnLoad(go);
        runner = go.AddComponent<Runner>();
    }

    // 기록할지(에디터에서는 Console에 보여줄 때만) / 실제로 보낼지
    private static bool ShouldQueue()
    {
        var s = PlayStatsSettings.Get();
        return s.enabled && (!Application.isEditor || s.sendInEditor);
    }

    private static bool ShouldLogInEditor()
    {
        var s = PlayStatsSettings.Get();
        return s.enabled && Application.isEditor && s.logInEditor;
    }

    private static bool IsTracking() => ShouldQueue() || ShouldLogInEditor();

    // ---------------- 판 시작/끝 (다른 스크립트가 부름) ----------------

    // 새 판이 시작될 때 GameFlowManager가 부릅니다(레벨을 만들기 전).
    public static void BeginRun(GameFlowManager flow)
    {
        try
        {
            EnsureInit();

            // 끝나지 않은 판이 있으면(같은 씬 다시 불러오기 = 재시작) 재시작으로 마무리합니다.
            if (current != null && !current.finished) FinishRun(EndRestart);
            current = null;

            if (flow == null || !IsTracking()) return;
            if (IsTestScene()) return; // 아이템 테스트 씬은 기록하지 않음

            var s = PlayStatsSettings.Get();
            var record = new RunRecord
            {
                k = s.token,
                recordId = Guid.NewGuid().ToString("N"),
                version = Application.version,
                editor = Application.isEditor ? 1 : 0,
                playerId = save.playerId,
                startType = DetermineStartType(flow.gameObject.scene.name),
                reroll = 1
            };

            current = new RunState
            {
                record = record,
                flow = flow,
                sceneName = flow.gameObject.scene.name
            };

            SaveOpenRun();
        }
        catch (Exception e)
        {
            Warn("판 시작 기록 중 문제가 생겼습니다", e);
        }
    }

    // 게임 클리어 결과 화면이 열릴 때 GameClearUI가 부릅니다. 여러 번 불려도 한 번만 기록됩니다.
    public static void NotifyGameClear()
    {
        try
        {
            if (current == null || current.finished) return;
            FinishRun(EndClear);
        }
        catch (Exception e)
        {
            Warn("클리어 기록 중 문제가 생겼습니다", e);
        }
    }

    // 아이템이 인벤토리/슬롯에 들어올 때 ItemCodexRecord.MarkAcquired가 부릅니다.
    public static void NoteItemAcquired(ItemDefinition item)
    {
        try
        {
            if (current == null || current.finished || item == null || string.IsNullOrEmpty(item.itemId)) return;
            current.acquired.Add(item.itemId);
        }
        catch (Exception e)
        {
            Warn("아이템 획득 기록 중 문제가 생겼습니다", e);
        }
    }

    // 보물상자/받침대/상점이 아이템을 내용물로 확정할 때 GameFlowManager.MarkItemAssignedThisLevel이 부릅니다.
    public static void NoteItemAppeared(string itemId)
    {
        try
        {
            if (current == null || current.finished || string.IsNullOrEmpty(itemId)) return;
            current.appeared.Add(itemId);
        }
        catch (Exception e)
        {
            Warn("아이템 등장 기록 중 문제가 생겼습니다", e);
        }
    }

    // ---------------- 이벤트 처리 ----------------

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        try
        {
            if (mode != LoadSceneMode.Single) return;

            // 판이 진행 중인데 다른 씬(타이틀)이 열렸으면 타이틀로 나간 것입니다. 같은 씬이면 재시작이라 BeginRun에서 처리합니다.
            if (current != null && !current.finished && scene.name != current.sceneName) FinishRun(EndQuitTitle);
        }
        catch (Exception e)
        {
            Warn("씬 전환 기록 중 문제가 생겼습니다", e);
        }
    }

    // 씬을 바꾸기 직전(오브젝트가 아직 살아 있을 때) 진행 상황을 최신으로 저장해 둡니다.
    private static void HandleBeforeSceneChange()
    {
        try
        {
            sceneBeforeChange = SceneManager.GetActiveScene().name;
            if (current != null && !current.finished) Snapshot();
        }
        catch (Exception e)
        {
            Warn("씬 전환 전 저장 중 문제가 생겼습니다", e);
        }
    }

    private static void HandlePlayerDied(PlayerHealth health)
    {
        try
        {
            if (current == null || current.finished) return;

            var r = current.record;
            r.deathLevel = LevelLabel(current.flow) ?? r.levelReached ?? "";

            RoomInstance room = current.lastRoom;
            if (room != null)
            {
                r.deathRoom = room.category.ToString();
                r.deathRoomVariant = room.variantId ?? "";
                if (current.roomOrder.TryGetValue(room, out Vector2Int order))
                {
                    r.deathRoomOrder = order.x;
                    r.deathRoomOrderInLevel = order.y;
                }
            }

            PlayerDamageSource source = health != null ? health.LastDamageSource : PlayerDamageSource.Unknown;
            if (!source.IsSet) source = PlayerDamageSource.Unknown;
            r.deathAttacker = source.attacker;
            r.deathAttack = source.attack;
            r.deathEnemy = source.enemyName;

            FinishRun(EndDeath);
        }
        catch (Exception e)
        {
            Warn("사망 기록 중 문제가 생겼습니다", e);
        }
    }

    // 방에 들어갈 때마다 불립니다. 처음 들어가는 방에만 순서를 매깁니다(시작방은 0 - 세지 않음).
    private static void HandleRoomEntered(RoomInstance room)
    {
        try
        {
            if (current == null || current.finished || room == null) return;

            string level = LevelLabel(current.flow) ?? "";
            if (level != current.countingLevel)
            {
                current.countingLevel = level;
                current.levelRoomCount = 0;
            }

            current.lastRoom = room;

            if (!current.roomOrder.ContainsKey(room))
            {
                if (room.category == RoomCategory.Start)
                {
                    current.roomOrder[room] = Vector2Int.zero;
                }
                else
                {
                    current.runRoomCount++;
                    current.levelRoomCount++;
                    current.roomOrder[room] = new Vector2Int(current.runRoomCount, current.levelRoomCount);
                }
            }

            if (room.category == RoomCategory.Boss && !current.bossReached.Contains(level)) current.bossReached.Add(level);

            SaveOpenRun();
        }
        catch (Exception e)
        {
            Warn("방 진입 기록 중 문제가 생겼습니다", e);
        }
    }

    private static void HandleQuitting()
    {
        try
        {
            // 판 도중에 게임을 끄면 게임 종료로 마무리합니다. 꺼지는 중이라 바로 전송되지 못할 수 있지만, PC에 저장해 두었다가 다음 실행 때 보냅니다.
            if (current != null && !current.finished) FinishRun(EndQuitGame);
        }
        catch (Exception e)
        {
            Warn("게임 종료 기록 중 문제가 생겼습니다", e);
        }
    }

    // ---------------- 판 기록 채우기/마무리 ----------------

    private static void FinishRun(string endType)
    {
        if (current == null || current.finished) return;
        current.finished = true;

        try { Snapshot(); }
        catch (Exception e) { Warn("판 마무리 중 일부 값을 읽지 못했습니다", e); }

        var record = current.record;
        record.endType = endType;
        string json = JsonUtility.ToJson(record);

        if (ShouldLogInEditor())
        {
            Debug.Log($"{LogPrefix} 판 종료 - 결과: {endType}, 시간: {record.playTime}초, 도달: {record.levelReached}, " +
                      $"리롤: {record.reroll}, 사망: {record.deathLevel}/{record.deathRoom}/{record.deathAttacker}/{record.deathAttack}/{record.deathEnemy}, " +
                      $"도감: {record.codexCount}/{record.codexTotal}" +
                      (ShouldQueue() ? " (전송 대기열에 넣음)" : " (에디터 - 보내지 않음)") + $"\n{json}");
        }

        current = null;

        if (save != null)
        {
            save.openRun = null;
            if (ShouldQueue()) Enqueue(json);
            SaveState();
        }
    }

    // 살아 있는 게임 오브젝트에서 최신 값을 읽어 기록에 채웁니다. 오브젝트가 이미 사라졌으면(씬 전환 후) 마지막으로 읽은 값을 그대로 둡니다.
    private static void Snapshot()
    {
        if (current == null) return;
        var r = current.record;
        GameFlowManager flow = current.flow;

        if (flow != null)
        {
            r.playTime = Mathf.RoundToInt(flow.PlayTimeSeconds);
            r.levelReached = LevelLabel(flow);
            r.enemiesKilled = flow.EnemiesKilled;
            r.roomsCleared = flow.RoomsCleared;
            CaptureHeldItems(flow);
            if (current.databaseIds == null) current.databaseIds = CollectDatabaseIds(flow);
        }

        r.roomsEntered = current.runRoomCount;
        r.reroll = current.runRoomCount == 0 ? 1 : 0;
        r.bossReached = string.Join(",", current.bossReached);
        r.acquired = JoinSorted(current.acquired);
        r.appeared = JoinSorted(current.appeared);
        CaptureCodex();
    }

    private static void CaptureHeldItems(GameFlowManager flow)
    {
        GameObject player = flow.levelBuilder != null ? flow.levelBuilder.CurrentPlayer : null;
        if (player == null) return;

        var items = new List<ItemDefinition>();
        var inventory = player.GetComponent<PlayerInventory>();
        if (inventory != null && inventory.OwnedItems != null) items.AddRange(inventory.OwnedItems);
        var active = player.GetComponent<PlayerActiveItem>();
        if (active != null && active.EquippedItem != null) items.Add(active.EquippedItem);
        var trinket = player.GetComponent<PlayerTrinket>();
        if (trinket != null && trinket.EquippedItems != null) items.AddRange(trinket.EquippedItems);
        var disposable = player.GetComponent<PlayerDisposableItem>();
        if (disposable != null && disposable.EquippedItems != null) items.AddRange(disposable.EquippedItems);
        var food = player.GetComponent<PlayerFoodItem>();
        if (food != null && food.EquippedItems != null) items.AddRange(food.EquippedItems);

        var ids = new List<string>();
        int t0 = 0, t1 = 0, t2 = 0, t3 = 0;
        foreach (var item in items)
        {
            if (item == null || string.IsNullOrEmpty(item.itemId)) continue;
            ids.Add(item.itemId);
            switch (item.rarity)
            {
                case ItemDefinition.ItemRarity.Tier0: t0++; break;
                case ItemDefinition.ItemRarity.Tier1: t1++; break;
                case ItemDefinition.ItemRarity.Tier2: t2++; break;
                case ItemDefinition.ItemRarity.Tier3: t3++; break;
            }
        }

        var r = current.record;
        r.held = string.Join(",", ids);
        r.heldT0 = t0;
        r.heldT1 = t1;
        r.heldT2 = t2;
        r.heldT3 = t3;
    }

    private static HashSet<string> CollectDatabaseIds(GameFlowManager flow)
    {
        var database = flow.levelBuilder != null ? flow.levelBuilder.itemDatabase : null;
        if (database == null || database.allItems == null) return null;

        var ids = new HashSet<string>();
        foreach (var item in database.allItems)
        {
            if (item != null && !string.IsNullOrEmpty(item.itemId)) ids.Add(item.itemId);
        }
        return ids;
    }

    // 도감 수집율: 전체 아이템(아이템 데이터베이스)에 들어 있는 것만 셉니다.
    private static void CaptureCodex()
    {
        var r = current.record;
        List<string> codex = ItemCodexRecord.GetAcquiredIdsSnapshot();
        HashSet<string> database = current.databaseIds;

        if (database != null)
        {
            codex = codex.FindAll(id => database.Contains(id));
            r.codexTotal = database.Count;
        }
        r.codexCount = codex.Count;
        r.codexIds = string.Join(",", codex);
    }

    // 진행 중인 판을 PC에 저장합니다(강제 종료 대비). 실제로 보내는 경우에만 저장합니다.
    private static void SaveOpenRun()
    {
        if (current == null || current.finished || save == null) return;
        nextSnapshotTime = Time.unscaledTime + SnapshotInterval;
        if (!ShouldQueue()) return;

        Snapshot();
        current.record.endType = "";
        save.openRun = JsonUtility.ToJson(current.record);
        SaveState();
    }

    private static string LevelLabel(GameFlowManager flow)
    {
        if (flow == null) return null;
        int n = flow.CurrentLevelNumber;
        return flow.CurrentPhase == GameFlowManager.LevelPhase.Choice ? $"{n}.5" : n.ToString();
    }

    private static string DetermineStartType(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneBeforeChange)) return StartDirect;
        return sceneBeforeChange == sceneName ? StartRestart : StartTitle;
    }

    private static bool IsTestScene()
    {
        return UnityEngine.Object.FindFirstObjectByType<ItemTestArenaBuilder>(FindObjectsInactive.Include) != null;
    }

    private static string JoinSorted(HashSet<string> set)
    {
        var list = new List<string>(set);
        list.Sort(StringComparer.Ordinal);
        return string.Join(",", list);
    }

    // ---------------- 에러 기록 ----------------

    private static readonly Regex WindowsPath = new Regex(@"[A-Za-z]:[\\/][^\s""'<>|*?]*");
    private static readonly Regex UnixHomePath = new Regex(@"/(?:Users|home)/[^\s""'<>|*?]*");

    private static void HandleLog(string condition, string stackTrace, LogType type)
    {
        try
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            if (!ShouldQueue() || save == null) return;

            var s = PlayStatsSettings.Get();
            if (errorCount >= s.maxErrorsPerSession) return;
            if (condition != null && condition.StartsWith(LogPrefix, StringComparison.Ordinal)) return;

            string message = Sanitize(condition, 300);
            if (!reportedErrors.Add(message)) return; // 같은 에러는 한 번만
            errorCount++;

            var record = new ErrorRecord
            {
                k = s.token,
                recordId = Guid.NewGuid().ToString("N"),
                version = Application.version,
                editor = Application.isEditor ? 1 : 0,
                playerId = save.playerId,
                runId = current != null ? current.record.recordId : "",
                level = current != null ? (LevelLabel(current.flow) ?? "") : "",
                scene = SceneManager.GetActiveScene().name,
                message = message,
                stack = Sanitize(FirstLines(stackTrace, 4), 500)
            };

            Enqueue(JsonUtility.ToJson(record));
            SaveState();
        }
        catch (Exception)
        {
            // 에러 기록 중 에러가 나면 조용히 넘어갑니다(여기서 로그를 남기면 다시 이 함수가 불릴 수 있음).
        }
    }

    // 파일 경로/사용자 이름/PC 이름을 지우고 길이를 자릅니다.
    private static string Sanitize(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text)) return "";
        string s = text;

        s = ReplacePath(s, Application.persistentDataPath, "<data>");
        s = ReplacePath(s, Application.dataPath, "<game>");
        s = ReplacePath(s, Application.temporaryCachePath, "<temp>");
        s = WindowsPath.Replace(s, "<path>");
        s = UnixHomePath.Replace(s, "<path>");
        s = ReplaceWord(s, SafeGet(() => Environment.UserName), "<user>");
        s = ReplaceWord(s, SafeGet(() => Environment.MachineName), "<pc>");

        if (s.Length > maxLength) s = s.Substring(0, maxLength);
        return s;
    }

    private static string ReplacePath(string s, string path, string replacement)
    {
        if (string.IsNullOrEmpty(path)) return s;
        s = s.Replace(path, replacement);
        s = s.Replace(path.Replace('/', '\\'), replacement);
        return s;
    }

    private static string ReplaceWord(string s, string word, string replacement)
    {
        if (string.IsNullOrEmpty(word) || word.Length < 3) return s;
        return Regex.Replace(s, Regex.Escape(word), replacement, RegexOptions.IgnoreCase);
    }

    private static string SafeGet(Func<string> getter)
    {
        try { return getter(); }
        catch (Exception) { return null; }
    }

    private static string FirstLines(string text, int count)
    {
        if (string.IsNullOrEmpty(text)) return "";
        string[] lines = text.Split('\n');
        var sb = new StringBuilder();
        for (int i = 0; i < lines.Length && i < count; i++)
        {
            if (i > 0) sb.Append(" | ");
            sb.Append(lines[i].Trim());
        }
        return sb.ToString();
    }

    // ---------------- 저장/전송 ----------------

    private static void Enqueue(string json)
    {
        if (save == null || string.IsNullOrEmpty(json)) return;
        save.queue.Add(json);
        int max = Mathf.Max(1, PlayStatsSettings.Get().maxQueuedRecords);
        while (save.queue.Count > max) save.queue.RemoveAt(0);
        nextSendTime = 0f; // 바로 보내기 시도
    }

    private static void LoadState()
    {
        save = null;
        try
        {
            string path = File.Exists(FilePath) ? FilePath : FilePath + ".tmp";
            if (File.Exists(path)) save = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
        }
        catch (Exception e)
        {
            Warn("저장 파일을 읽지 못해 새로 시작합니다", e);
        }

        if (save == null) save = new SaveData();
        if (save.queue == null) save.queue = new List<string>();
        if (string.IsNullOrEmpty(save.playerId))
        {
            save.playerId = Guid.NewGuid().ToString("N"); // 게임이 만든 무작위 번호 - 사람과 연결되는 정보가 아님
            SaveState();
        }
    }

    // 임시 파일에 먼저 쓰고 바꿔치기해서, 저장 도중 꺼져도 원래 파일이 깨지지 않게 합니다.
    private static void SaveState()
    {
        if (save == null) return;
        try
        {
            string path = FilePath;
            string temp = path + ".tmp";
            File.WriteAllText(temp, JsonUtility.ToJson(save));
            if (File.Exists(path)) File.Replace(temp, path, null);
            else File.Move(temp, path);
        }
        catch (Exception e)
        {
            Warn("저장 파일을 쓰지 못했습니다", e);
        }
    }

    private static void Tick()
    {
        try
        {
            if (current != null && !current.finished && Time.unscaledTime >= nextSnapshotTime) SaveOpenRun();

            if (!sending && runner != null && save != null && save.queue.Count > 0 && ShouldQueue() &&
                Time.unscaledTime >= nextSendTime)
            {
                runner.StartCoroutine(SendNext());
            }
        }
        catch (Exception e)
        {
            Warn("주기 처리 중 문제가 생겼습니다", e);
            nextSendTime = Time.unscaledTime + RetryInterval;
        }
    }

    private static IEnumerator SendNext()
    {
        sending = true;

        string url = (PlayStatsSettings.Get().endpointUrl ?? "").Trim();
        if (!url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            if (!warnedNoUrl)
            {
                warnedNoUrl = true;
                Debug.LogWarning($"{LogPrefix} 전송 주소(PlayStatsSettings의 Endpoint Url)가 비어 있거나 https가 아니라 보내지 않습니다. 기록은 PC에 쌓아 둡니다.");
            }
            nextSendTime = Time.unscaledTime + RetryInterval;
            sending = false;
            yield break;
        }

        string payload = save.queue[0];
        UnityWebRequest request = null;
        bool requestStarted = false;
        try
        {
            request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(payload));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "text/plain;charset=utf-8");
            request.timeout = 20;
            requestStarted = true;
        }
        catch (Exception e)
        {
            Warn("전송 준비 중 문제가 생겼습니다", e);
        }

        if (requestStarted)
        {
            yield return request.SendWebRequest();

            bool success = request.result == UnityWebRequest.Result.Success;
            if (success)
            {
                // 서버에 닿았으면(받아들였든 형식 문제로 거절했든) 대기열에서 뺍니다 - 거절된 기록을 계속 다시 보내지 않도록.
                int index = save.queue.IndexOf(payload);
                if (index >= 0) save.queue.RemoveAt(index);
                SaveState();
                nextSendTime = Time.unscaledTime + 1f;
            }
            else
            {
                // 인터넷 문제 등 - 남겨 두었다가 나중에(또는 다음 실행 때) 다시 보냅니다.
                Debug.LogWarning($"{LogPrefix} 전송하지 못해 나중에 다시 보냅니다. ({request.error})");
                nextSendTime = Time.unscaledTime + RetryInterval;
            }
        }
        else
        {
            nextSendTime = Time.unscaledTime + RetryInterval;
        }

        if (request != null) request.Dispose();
        sending = false;
    }

    private static void Warn(string what, Exception e)
    {
        Debug.LogWarning($"{LogPrefix} {what}. {e.Message}");
    }

    // 매 프레임 Tick을 부르고 전송 코루틴을 돌리는 숨은 오브젝트입니다(씬이 바뀌어도 남음).
    private class Runner : MonoBehaviour
    {
        void Update()
        {
            Tick();
        }
    }

    // ---------------- 개발용 메뉴 ----------------
#if UNITY_EDITOR
    // 내 PC의 플레이어 번호를 복사합니다. 집계할 때 내 테스트 기록을 빼는 데 씁니다(에디터와 빌드가 같은 저장 폴더를 씀).
    [UnityEditor.MenuItem("MobArchive/플레이 통계/내 플레이어 번호 복사")]
    private static void CopyMyPlayerId()
    {
        LoadState();
        UnityEditor.EditorGUIUtility.systemCopyBuffer = save.playerId;
        UnityEditor.EditorUtility.DisplayDialog("플레이 통계", $"내 플레이어 번호를 복사했습니다.\n\n{save.playerId}", "확인");
    }

    // 아이템 목록(ID/이름/등급/종류)을 표 형태로 복사합니다. 구글 시트의 items 탭에 붙여 넣으면 통계를 이름으로 볼 수 있습니다.
    [UnityEditor.MenuItem("MobArchive/플레이 통계/아이템 목록 복사 (시트 붙여넣기용)")]
    private static void CopyItemTable()
    {
        var seen = new HashSet<string>();
        var sb = new StringBuilder("itemId\tname\ttier\tcategory\n");
        int count = 0;
        foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:ItemDatabase"))
        {
            var database = UnityEditor.AssetDatabase.LoadAssetAtPath<ItemDatabase>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
            if (database == null || database.allItems == null) continue;
            foreach (var item in database.allItems)
            {
                if (item == null || string.IsNullOrEmpty(item.itemId) || !seen.Add(item.itemId)) continue;
                string name = (item.displayName ?? "").Replace('\t', ' ').Replace('\n', ' ');
                sb.Append(item.itemId).Append('\t').Append(name).Append('\t')
                  .Append((int)item.rarity).Append('\t').Append(item.category.ToString()).Append('\n');
                count++;
            }
        }
        UnityEditor.EditorGUIUtility.systemCopyBuffer = sb.ToString();
        UnityEditor.EditorUtility.DisplayDialog("플레이 통계", $"아이템 {count}개의 목록을 복사했습니다.", "확인");
    }

    // 보내지 못하고 쌓인 기록과 진행 중 판 기록을 지웁니다(테스트 뒤 정리용). 플레이어 번호는 남깁니다.
    [UnityEditor.MenuItem("MobArchive/플레이 통계/보내지 않은 기록 지우기 (개발용)")]
    private static void ClearPending()
    {
        LoadState();
        int count = save.queue.Count;
        save.queue.Clear();
        save.openRun = null;
        SaveState();
        UnityEditor.EditorUtility.DisplayDialog("플레이 통계", $"보내지 않은 기록 {count}개를 지웠습니다.", "확인");
    }
#endif
}
