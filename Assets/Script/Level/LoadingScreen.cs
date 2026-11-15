using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 로딩 화면입니다. 검은 배경 + 가운데 캐릭터 아이콘이 좌우로 갸우뚱거립니다.
//
// [사용 방법]
// 빈 오브젝트에 이 스크립트만 붙여서 프리팹(LoadingScreen)으로 만든 뒤, Title 씬과 LevelBuilder 씬에
// 둘 다 넣어 둡니다. Canvas/배경/아이콘은 전부 코드가 자동으로 만듭니다.
// - 씬이 바뀌어도 사라지지 않습니다(DontDestroyOnLoad). 다음 씬에도 같은 프리팹이 있으면, 먼저 있던
//   하나만 남고 새로 생긴 쪽은 스스로 지워집니다. 그래서 값은 프리팹 한 곳에서만 고치면 됩니다.
// - 씬에 이 오브젝트가 없으면 로딩 화면 없이 예전과 100% 똑같이 동작합니다.
//
// [언제 뜨나]
// - 레벨을 새로 만들 때(GameFlowManager.StartNewGame / AdvanceToNextPhase): 게임 시작, 포탈로 다음
//   레벨 진입, 게임 오버 [예] 재시작.
// - 씬을 바꿀 때(LoadSceneWithLoading): 시작 화면의 [게임 시작], 게임 오버 [예]/[아니오].
//
// [흐름]
// 1. 검은 화면을 즉시 띄웁니다(나타날 때는 페이드 없음 - 끊기는 순간을 바로 가리기 위해).
// 2. 진행 중인 게임에서는 화면을 한 프레임 그린 뒤에 레벨을 만듭니다. 레벨 빌드는 한 프레임 안에서
//    한꺼번에 실행되어 그 프레임이 끊기는데, 그 끊김이 검은 화면 뒤에서 일어나게 하려는 것입니다.
//    (씬이 처음 열릴 때는 아직 화면을 한 번도 그리지 않았으므로 기다리지 않고 바로 만듭니다.)
// 3. "최소 표시 시간(minDisplayTime)"과 "작업이 끝난 뒤 착지 대기 시간(settleTimeAfterWork - 플레이어가
//    시작방 1m 위에서 떨어지는 시간)"을 둘 다 채우면 fadeOutDuration초 동안 서서히 사라집니다.
//    모든 시간은 실제 시간 기준이라 게임이 멈춰 있어도(Time.timeScale = 0) 똑같이 흐릅니다.
//
// [로딩 중 조작 차단]
// 로딩 화면이 떠 있는 동안 사격/무기 전환/수류탄/해킹/액티브/일회용/음식 컴포넌트를 꺼 두었다가,
// 사라지기 시작할 때 이 스크립트가 끈 것만 다시 켭니다(SlotReplaceConfirmUI와 같은 방식).
// 수류탄 투척/해킹 사용 동작이 이미 진행 중이면 그 동작이 끊기지 않도록 그 컴포넌트는 끄지 않습니다.
// 이동은 컴포넌트를 끄면 중력까지 멈춰 플레이어가 공중에 떠 버리므로, PlayerMovement가
// BlocksPlayerInput을 보고 이동 입력만 0으로 만듭니다(떨어지는 동작은 정상).
public class LoadingScreen : MonoBehaviour
{
    // [판 정리 알림] 씬을 바꾸기 직전(죽은 뒤 다시하기 / R 재시작 / 재시작하기 / 그만하기 / 게임 시작)에 한 번 보냅니다.
    // 아이템 효과 인스턴스는 아이템 에셋에 하나뿐이라, 정적 알림 구독이 씬을 넘어 다음 판까지 남습니다.
    // 에디터는 Play를 누를 때마다 정적 메모리를 비우지만 빌드는 프로그램이 꺼질 때까지 이어지므로, 여기서 직접 정리하게 합니다.
    // 받는 곳: PlayerInventory/PlayerTrinket(보유 효과 Remove), 43/27/40번(대기 구독 해제), Next...Override(무장 해제).
    public static event Action OnBeforeSceneChange;

    private static void RaiseBeforeSceneChange()
    {
        Action handlers = OnBeforeSceneChange;
        if (handlers == null) return;
        // 받는 곳마다 따로 실행 - 한 곳의 오류가 나머지 정리를 막지 않게.
        foreach (Delegate d in handlers.GetInvocationList())
        {
            try { ((Action)d).Invoke(); }
            catch (Exception e) { Debug.LogError($"LoadingScreen: 씬 전환 전 정리 중 오류가 나 이 항목만 건너뜁니다: {e}"); }
        }
    }

    [Header("아이콘")]
    [Tooltip("가운데에 띄울 캐릭터 아이콘입니다. 비우면 검은 화면만 보입니다.")]
    public Sprite characterIcon;

    [Tooltip("아이콘 크기입니다(1920x1080 화면 기준 픽셀).")]
    public Vector2 iconSize = new Vector2(220f, 220f);

    [Tooltip("화면 가운데에서 아이콘을 옮길 거리입니다(1920x1080 화면 기준 픽셀).")]
    public Vector2 iconOffset = Vector2.zero;

    [Tooltip("켜면 스프라이트 원래 비율을 유지합니다(크기 칸 안에 맞춰 들어감).")]
    public bool preserveIconAspect = true;

    [Header("갸우뚱 연출")]
    [Tooltip("좌우로 기우는 최대 각도(도)입니다.")]
    public float tiltAngle = 10f;

    [Tooltip("왼쪽 → 오른쪽 → 왼쪽 한 번 왕복하는 데 걸리는 시간(초)입니다.")]
    [Min(0.05f)] public float tiltPeriod = 1.2f;

    [Header("시간 (실제 시간 기준, 초)")]
    [Tooltip("로딩 화면을 최소한 이 시간만큼은 띄워 둡니다.")]
    [Min(0f)] public float minDisplayTime = 1f;

    [Tooltip("레벨 빌드/씬 불러오기가 끝난 뒤 추가로 기다리는 시간입니다. 플레이어가 떨어져 착지하는 모습을 " +
             "가리기 위한 값입니다.")]
    [Min(0f)] public float settleTimeAfterWork = 0.5f;

    [Tooltip("사라질 때 서서히 투명해지는 시간입니다. 0이면 바로 사라집니다.")]
    [Min(0f)] public float fadeOutDuration = 0.2f;

    [Header("표시")]
    public Color backgroundColor = Color.black;

    [Tooltip("다른 모든 UI(게임 오버 창 100 포함)보다 위에 보이도록 높은 값을 씁니다.")]
    public int canvasSortingOrder = 1000;

    [Header("조작 차단")]
    [Tooltip("켜면 로딩 화면이 떠 있는 동안 플레이어 조작을 막습니다.")]
    public bool blockPlayerInput = true;

    // ------------------------------------------------------------------
    // 정적 접근
    // ------------------------------------------------------------------

    public static LoadingScreen Instance { get; private set; }

    // 로딩 화면이 떠 있는지(사라지는 페이드 중 포함).
    public static bool IsShowing => Instance != null && Instance.showing;

    // PlayerMovement가 이동 입력을 0으로 만들지 판단할 때 씁니다. 사라지는 페이드가 시작되면
    // 바로 조작할 수 있도록 페이드 중에는 false입니다.
    public static bool BlocksPlayerInput =>
        Instance != null && Instance.showing && !Instance.fading && Instance.blockPlayerInput;

    // ------------------------------------------------------------------
    // 내부 상태
    // ------------------------------------------------------------------

    private GameObject canvasGO;
    private CanvasGroup canvasGroup;
    private Canvas canvas;
    private Image backgroundImage;
    private RectTransform iconRect;
    private Image iconImage;

    private bool showing;
    private bool fading;
    private float showStartTime;
    private float lastWorkDoneTime;
    private float fadeStartTime;
    private int pendingWork;

    private PlayerHealth blockedPlayer;
    private readonly List<Behaviour> disabledBehaviours = new List<Behaviour>();

    private static float Now => Time.realtimeSinceStartup;

    void Awake()
    {
        // 다음 씬에 들어 있던 프리팹이면 먼저 살아 있던 하나만 남기고 스스로 지웁니다.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (transform.parent != null) transform.SetParent(null); // DontDestroyOnLoad는 최상위 오브젝트만 가능
        DontDestroyOnLoad(gameObject);

        BuildUI();
        canvasGO.SetActive(false);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        if (!showing) return;

        ApplyLiveSettings();
        UpdateTilt();

        // 새 씬에서 플레이어가 생기면(게임 시작) 그때 조작을 막습니다.
        if (!fading && blockedPlayer == null) BlockPlayerControls();

        if (!fading)
        {
            float now = Now;
            bool ready = pendingWork == 0 &&
                         now >= showStartTime + minDisplayTime &&
                         now >= lastWorkDoneTime + settleTimeAfterWork;
            if (ready) BeginFadeOut();
        }
        else
        {
            float t = fadeOutDuration > 0f ? (Now - fadeStartTime) / fadeOutDuration : 1f;
            canvasGroup.alpha = Mathf.Clamp01(1f - t);
            if (t >= 1f) HideImmediately();
        }
    }

    // ------------------------------------------------------------------
    // 공개 기능
    // ------------------------------------------------------------------

    // 레벨 빌드처럼 "한 프레임 안에 끝나는 작업"을 로딩 화면을 띄운 채 실행합니다.
    // waitOneFrame = true: 검은 화면을 한 프레임 그린 뒤 실행(진행 중인 게임에서 사용).
    // waitOneFrame = false: 바로 실행(씬이 막 열려 아직 화면을 그리기 전일 때 사용 - 다른 스크립트의
    // Start 순서가 예전과 똑같이 유지됩니다).
    // 로딩 화면이 씬에 없으면 그냥 바로 실행합니다.
    public static void RunLevelWork(Action work, bool waitOneFrame)
    {
        if (work == null) return;
        if (Instance == null)
        {
            work();
            return;
        }
        Instance.RunWork(work, waitOneFrame);
    }

    // 로딩 화면을 띄운 뒤 씬을 불러옵니다. startLoad는 실제 불러오기를 시작하고 AsyncOperation을
    // 돌려주는 함수입니다(StartSceneLoad / StartReloadActiveScene 사용).
    // 로딩 화면이 씬에 없으면 로딩 화면 없이 바로 불러옵니다.
    public static void LoadSceneWithLoading(Func<AsyncOperation> startLoad)
    {
        if (startLoad == null) return;
        if (Instance == null)
        {
            RaiseBeforeSceneChange();
            startLoad();
            return;
        }
        Instance.Show();
        Instance.StartCoroutine(Instance.LoadSceneRoutine(startLoad));
    }

    // 이름으로 씬을 불러올 수 있는지 확인합니다. 실제 빌드에서는 File > Build Profiles의 씬 목록에
    // 있어야 합니다. 에디터 Play 중에는 목록에 없어도 프로젝트 안에 같은 이름의 씬 파일이 있으면 됩니다.
    public static bool CanLoadScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) return false;
        if (Application.CanStreamedLevelBeLoaded(sceneName)) return true;
#if UNITY_EDITOR
        return FindScenePathInEditor(sceneName) != null;
#else
        return false;
#endif
    }

    // 이름으로 씬 불러오기를 시작합니다. 실패하면 오류를 남기고 null을 돌려줍니다.
    public static AsyncOperation StartSceneLoad(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("LoadingScreen: 불러올 씬 이름이 비어 있습니다.");
            return null;
        }

        if (Application.CanStreamedLevelBeLoaded(sceneName))
            return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);

#if UNITY_EDITOR
        string path = FindScenePathInEditor(sceneName);
        if (path != null)
        {
            return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                path, new LoadSceneParameters(LoadSceneMode.Single));
        }
#endif
        Debug.LogError($"LoadingScreen: '{sceneName}' 씬을 찾지 못했습니다. 씬 이름이 맞는지, " +
                       "File > Build Profiles의 씬 목록에 들어 있는지 확인해주세요.");
        return null;
    }

    // 지금 씬을 다시 불러오기 시작합니다(게임 오버 [예] 재시작용).
    public static AsyncOperation StartReloadActiveScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.buildIndex >= 0)
            return SceneManager.LoadSceneAsync(scene.buildIndex, LoadSceneMode.Single);

#if UNITY_EDITOR
        // Build Profiles 씬 목록에 없는 씬도 에디터 Play 중에는 경로로 다시 불러올 수 있습니다.
        return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
            scene.path, new LoadSceneParameters(LoadSceneMode.Single));
#else
        Debug.LogError("LoadingScreen: 현재 씬이 Build Profiles의 씬 목록에 없어 다시 불러올 수 없습니다. " +
                       "File > Build Profiles에서 이 씬을 씬 목록에 추가해주세요.");
        return null;
#endif
    }

    // 검은 화면을 즉시 띄웁니다. 사라지는 중이었다면 다시 완전히 검게 되돌리고 최소 시간을 새로 셉니다.
    public void Show()
    {
        if (!showing || fading)
        {
            showing = true;
            fading = false;
            showStartTime = Now;
            lastWorkDoneTime = Now;
            canvasGO.SetActive(true);
            canvasGroup.alpha = 1f;
            ApplyLiveSettings();
            UpdateTilt();
        }
        BlockPlayerControls();
    }

    // ------------------------------------------------------------------
    // 작업 실행
    // ------------------------------------------------------------------

    private void RunWork(Action work, bool waitOneFrame)
    {
        Show();
        if (waitOneFrame) StartCoroutine(RunWorkNextFrame(work));
        else ExecuteWork(work);
    }

    private IEnumerator RunWorkNextFrame(Action work)
    {
        pendingWork++;
        yield return null; // 이 사이에 검은 화면이 한 번 그려집니다.
        pendingWork--;
        ExecuteWork(work);
    }

    private void ExecuteWork(Action work)
    {
        pendingWork++;
        try
        {
            work();
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
        finally
        {
            pendingWork--;
            lastWorkDoneTime = Now;
        }

        // 레벨 빌드로 새 플레이어가 생겼을 수 있으므로 다시 확인합니다.
        BlockPlayerControls();
    }

    private IEnumerator LoadSceneRoutine(Func<AsyncOperation> startLoad)
    {
        pendingWork++;
        yield return null; // 검은 화면을 먼저 한 번 그립니다.

        RaiseBeforeSceneChange(); // 플레이어가 아직 살아 있을 때 지난 판의 효과를 정리(검은 화면 뒤).

        AsyncOperation op = null;
        try
        {
            op = startLoad();
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }

        if (op != null)
        {
            while (!op.isDone) yield return null;
            // 새 씬의 Start(GameFlowManager의 레벨 빌드 포함)가 끝날 때까지 한 프레임 더 기다립니다.
            yield return null;
        }

        pendingWork--;
        lastWorkDoneTime = Now;
    }

    // ------------------------------------------------------------------
    // 표시 / 숨김
    // ------------------------------------------------------------------

    private void BeginFadeOut()
    {
        RestorePlayerControls();

        if (fadeOutDuration <= 0f)
        {
            HideImmediately();
            return;
        }
        fading = true;
        fadeStartTime = Now;
    }

    private void HideImmediately()
    {
        RestorePlayerControls();
        showing = false;
        fading = false;
        canvasGroup.alpha = 0f;
        canvasGO.SetActive(false);
    }

    private void UpdateTilt()
    {
        if (iconRect == null) return;
        float t = Now - showStartTime;
        float angle = Mathf.Sin(t * Mathf.PI * 2f / Mathf.Max(0.05f, tiltPeriod)) * tiltAngle;
        iconRect.localEulerAngles = new Vector3(0f, 0f, angle);
    }

    // ------------------------------------------------------------------
    // 조작 차단
    // ------------------------------------------------------------------

    private void BlockPlayerControls()
    {
        if (!blockPlayerInput) return;

        PlayerHealth player = UnityEngine.Object.FindFirstObjectByType<PlayerHealth>();
        if (player == null || player.IsDead) return;
        if (player == blockedPlayer) return;

        // 다른 플레이어를 막고 있었다면(보통은 없음) 먼저 되돌립니다.
        RestorePlayerControls();
        blockedPlayer = player;

        GameObject go = player.gameObject;

        // 조준 컴포넌트를 끄면 Animator의 조준 값이 마지막 상태로 남으므로 먼저 풀어 둡니다.
        var aim = go.GetComponent<PlayerAimAndAttack>();
        if (aim != null) ResetAimAnimator(go.GetComponentInChildren<Animator>());

        TryDisable(aim);
        TryDisable(go.GetComponent<PlayerShooting>());
        TryDisable(go.GetComponent<PlayerWeaponMode>());
        TryDisable(go.GetComponent<PlayerActiveItem>());
        TryDisable(go.GetComponent<PlayerDisposableItem>());
        TryDisable(go.GetComponent<PlayerFoodItem>());

        // 투척/사용 동작이 진행 중이면 끊기지 않도록 끄지 않습니다.
        var grenade = go.GetComponent<PlayerGrenadeThrower>();
        if (grenade != null && !grenade.IsThrowing) TryDisable(grenade);
        var hack = go.GetComponent<PlayerHackTool>();
        if (hack != null && !hack.IsDeploying) TryDisable(hack);
    }

    private void TryDisable(Behaviour behaviour)
    {
        if (behaviour == null || !behaviour.enabled) return;
        behaviour.enabled = false;
        disabledBehaviours.Add(behaviour);
    }

    private void RestorePlayerControls()
    {
        // 그 사이 플레이어가 죽었다면 사망 처리(PlayerDeathHandler)가 끈 상태를 그대로 둡니다.
        bool playerAlive = blockedPlayer != null && !blockedPlayer.IsDead;
        if (playerAlive)
        {
            foreach (var b in disabledBehaviours)
            {
                if (b != null) b.enabled = true;
            }
        }
        disabledBehaviours.Clear();
        blockedPlayer = null;
    }

    private static void ResetAimAnimator(Animator animator)
    {
        if (animator == null || animator.runtimeAnimatorController == null) return;
        foreach (var p in animator.parameters)
        {
            if (p.type == AnimatorControllerParameterType.Bool && p.name == "IsAiming")
                animator.SetBool(p.name, false);
        }
    }

    // ------------------------------------------------------------------
    // UI 생성
    // ------------------------------------------------------------------

    private void BuildUI()
    {
        canvasGO = new GameObject("LoadingCanvas");
        canvasGO.transform.SetParent(transform, false);
        canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = canvasSortingOrder;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

        canvasGO.AddComponent<GraphicRaycaster>(); // 로딩 중에는 아래 UI가 클릭되지 않도록 막습니다.
        canvasGroup = canvasGO.AddComponent<CanvasGroup>();
        canvasGroup.blocksRaycasts = true;
        canvasGroup.interactable = false;

        var bgGO = new GameObject("Background");
        bgGO.transform.SetParent(canvasGO.transform, false);
        var bgRect = bgGO.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;
        backgroundImage = bgGO.AddComponent<Image>();
        backgroundImage.raycastTarget = true;

        var iconGO = new GameObject("CharacterIcon");
        iconGO.transform.SetParent(canvasGO.transform, false);
        iconRect = iconGO.AddComponent<RectTransform>();
        iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconImage = iconGO.AddComponent<Image>();
        iconImage.raycastTarget = false;

        ApplyLiveSettings();
    }

    // 인스펙터 값을 Play 중에 바꿔도 바로 반영되도록 합니다.
    private void ApplyLiveSettings()
    {
        if (canvas != null) canvas.sortingOrder = canvasSortingOrder;
        if (backgroundImage != null) backgroundImage.color = backgroundColor;

        if (iconImage != null)
        {
            iconImage.sprite = characterIcon;
            iconImage.enabled = characterIcon != null;
            iconImage.preserveAspect = preserveIconAspect;
        }
        if (iconRect != null)
        {
            iconRect.sizeDelta = iconSize;
            iconRect.anchoredPosition = iconOffset;
        }
    }

#if UNITY_EDITOR
    // 프로젝트 안에서 이름이 정확히 같은 씬 파일의 경로를 찾습니다(에디터 전용).
    private static string FindScenePathInEditor(string sceneName)
    {
        foreach (string guid in UnityEditor.AssetDatabase.FindAssets($"t:Scene {sceneName}"))
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            if (System.IO.Path.GetFileNameWithoutExtension(path) == sceneName) return path;
        }
        return null;
    }
#endif
}
