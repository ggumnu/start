using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// [R 꾹 누르기 재시작 (형빈 확정 - 아이작 리펜던스와 같은 기능)]
// 게임 중 R을 2초 동안 꾹 누르고 있으면 화면이 점점 검게 변하고, 2초가 되는 순간 경고 없이 로딩 화면과 함께 재시작합니다
// (시작방 옆 보물방이 마음에 들지 않을 때 등). 재시작은 게임 오버 [예] / ESC [재시작하기]와 같이 지금 씬을 다시 불러오는 방식이라,
// 레벨 1을 새 랜덤 구조로 만들고 한 판 동안 쌓인 상태는 전부 초기화됩니다(도감 기록은 그대로). 아이템 테스트 씬에서도 됩니다
// (테스트 씬이 처음 상태로 다시 열림).
//
// [누르는 동안]
// - 게임은 멈추지 않습니다(이동/사격 모두 그대로). R을 누르는 순간의 원래 동작(총 모드 장전, 수류탄 모드 115번 크레모어 터뜨리기)도
//   평소처럼 일어납니다 - 그래서 장전하려고 짧게 누를 때 거의 티가 나지 않도록 처음엔 천천히, 끝에 가서 빠르게 어두워집니다.
// - 2초가 되기 전에 떼면 재시작 없이 지금 어두운 정도에서 빠르게 다시 밝아집니다. 밝아지는 도중에 다시 누르면 남은 어두운 정도에서
//   이어서 진해집니다(화면 밝기와 남은 시간이 항상 일치).
// - 재시작한 뒤에는 R을 한 번 뗐다가 다시 눌러야 다시 셉니다(계속 누른 채로 새 판에서 바로 또 어두워지지 않도록).
//
// [동작하지 않는 때 - 누르는 도중에 생기면 그 자리에서 취소되고 다시 밝아짐]
// ESC 중단 화면, 아이템 도감, 상점 창, 슬롯 교체 확인창, 슬롯 버리기 창, 다음 레벨 이동 확인창, 패시브 교체 창(56번)이 떠 있거나 방금
// 닫힌 프레임, 아이템 획득 연출 중, 로딩 화면 중, 게임이 멈춘 상태(게임 오버 등), 플레이어가 없거나(시작 화면) 죽었을 때.
//
// [설정]
// 씬에 붙일 필요가 없습니다 - 게임이 시작되면 스스로 만들어집니다(씬이 바뀌어도 유지). 키/시간/막 색은 HoldRestartSettings 에셋.
public class HoldRestart : MonoBehaviour
{
    private static HoldRestart instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null) return;
        var go = new GameObject("HoldRestart");
        DontDestroyOnLoad(go);
        go.AddComponent<HoldRestart>();
    }

    // 지금 재시작 중인지(검게 덮인 뒤 새 씬이 열릴 때까지)
    public static bool IsRestarting => instance != null && instance.restarting;

    private float progress;          // 0~1 (누른 시간 / holdDuration)
    private bool restarting;
    private bool waitForRelease;     // 재시작 뒤, 키를 한 번 뗄 때까지 다시 세지 않음

    private Canvas canvas;
    private CanvasScaler scaler;
    private Image overlayImage;

    void Awake()
    {
        if (instance == null) instance = this;
        else if (instance != this)
        {
            Destroy(gameObject);
            return;
        }

        BuildUI();
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    void OnDestroy()
    {
        if (instance != this) return;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        instance = null;
    }

    void Update()
    {
        var s = HoldRestartSettings.Get();

        if (restarting)
        {
            // 로딩 화면이 덮고 있는 동안 기다림. 로딩 화면이 없는 씬이면 새 씬이 열릴 때(HandleSceneLoaded) 정리됩니다.
            ApplyOverlay(s, 1f);
            return;
        }

        bool held = Input.GetKey(s.restartKey);
        if (!held) waitForRelease = false;

        if (held && !waitForRelease && CanHoldNow())
        {
            progress += Time.unscaledDeltaTime / Mathf.Max(0.1f, s.holdDuration);
            if (progress >= 1f)
            {
                progress = 1f;
                BeginRestart(s);
                return;
            }
        }
        else if (progress > 0f)
        {
            // 떼었거나 막힌 상황: 빠르게 다시 밝아짐
            progress -= Time.unscaledDeltaTime / Mathf.Max(0.01f, s.releaseFadeDuration);
            if (progress < 0f) progress = 0f;
        }

        ApplyOverlay(s, Mathf.Pow(progress, Mathf.Max(1f, s.darkenExponent)));
    }

    // ------------------------------------------------------------------
    // 판정 / 재시작
    // ------------------------------------------------------------------

    private static bool CanHoldNow()
    {
        if (Time.timeScale <= 0f) return false; // ESC 중단 화면/게임 오버 등으로 멈춤
        if (PauseMenuUI.IsOpen || PauseMenuUI.BlocksInteract) return false;
        if (ItemCodexUI.BlocksInput) return false;
        if (GameClearUI.BlocksInput) return false; // 게임 클리어 결과 화면
        if (ShopUI.IsOpen || ShopUI.BlocksInteract) return false;
        if (SlotReplaceConfirmUI.IsOpen || SlotReplaceConfirmUI.BlocksInteract) return false;
        if (SlotItemDropUI.IsOpen || SlotItemDropUI.BlocksInteract) return false;
        if (LevelTransitionConfirmUI.IsOpen || LevelTransitionConfirmUI.BlocksInteract) return false;
        if (PassiveRerollUI.IsOpen || PassiveRerollUI.BlocksInteract) return false;
        if (PlayerItemPickupPresenter.BlocksActions) return false;
        if (LoadingScreen.IsShowing) return false;
        if (IntroCutscene.BlocksInput) return false; // 입장 컷씬 중
        if (HeliExtraction.BlocksInput) return false; // 헬기 퇴각 연출 중
        if (CutsceneStudio.IsActive) return false; // 촬영 스튜디오 씬

        var health = Object.FindFirstObjectByType<PlayerHealth>();
        return health != null && !health.IsDead;
    }

    private void BeginRestart(HoldRestartSettings s)
    {
        GameAudio.Play(SfxId.HoldRestart);
        restarting = true;
        waitForRelease = true;
        ApplyOverlay(s, 1f);
        // 게임 오버 [예]와 같은 방식 - 로딩 화면(즉시 검게 뜸)을 띄운 채 지금 씬을 다시 불러옵니다.
        LoadingScreen.LoadSceneWithLoading(LoadingScreen.StartReloadActiveScene);
    }

    // 새 씬이 열리면 검은 막을 걷습니다(그 위를 로딩 화면이 덮고 있다가 서서히 사라짐).
    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!restarting) return;
        restarting = false;
        progress = 0f;
        ApplyOverlay(HoldRestartSettings.Get(), 0f);
    }

    // ------------------------------------------------------------------
    // 검은 막
    // ------------------------------------------------------------------

    private void BuildUI()
    {
        var canvasGO = new GameObject("HoldRestartCanvas");
        canvasGO.transform.SetParent(transform, false);
        canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        var overlayGO = new GameObject("Overlay");
        overlayGO.transform.SetParent(canvasGO.transform, false);
        var rect = overlayGO.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        overlayImage = overlayGO.AddComponent<Image>();
        overlayImage.raycastTarget = false; // 클릭을 막지 않음(누르는 동안에도 게임은 그대로)

        canvasGO.SetActive(false);
    }

    private void ApplyOverlay(HoldRestartSettings s, float alpha)
    {
        bool visible = alpha > 0.0001f;
        if (canvas.gameObject.activeSelf != visible) canvas.gameObject.SetActive(visible);
        if (!visible) return;

        canvas.sortingOrder = s.canvasSortingOrder;
        Color c = s.overlayColor;
        c.a *= Mathf.Clamp01(alpha);
        overlayImage.color = c;
    }
}
