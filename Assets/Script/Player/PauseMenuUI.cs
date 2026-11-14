using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// [게임 중단 화면 (형빈 확정)]
// 게임 중 ESC를 누르면 그 즉시 게임 전체가 멈추고(Time.timeScale = 0), 화면이 검은 반투명 막으로 서서히 가려진 뒤
// "게임을 중단하시겠습니까?" 문구와 버튼 두 줄(윗줄 [계속하기] [설정] [조작법], 아랫줄 [도감] [재시작하기] [그만하기])이 차례로 떠오릅니다.
// 팝업 창 없이 막 위에 바로 그려집니다.
//
// [입력] (버튼은 다 떠오른 뒤에만 눌림 - 실수 방지)
// - ESC / [계속하기]: 문구와 버튼이 사라지며 가라앉고 막이 서서히 밝아집니다. 막이 완전히 사라진 뒤에야 게임 시간과 플레이어
//   조작이 다시 흐릅니다(ESC를 누르고 대비할 시간). 버튼이 다 뜨기 전에 ESC를 눌러도 그 자리에서 바로 사라지기 시작합니다.
// - [설정]: 시작 화면과 같은 설정 창(SettingsUI - BGM/효과음 볼륨)을 중단 화면 위에 띄웁니다. 설정 창에서 ESC/[닫기]를 누르면
//   설정 창만 닫히고 중단 화면으로 돌아옵니다(도감과 같은 방식).
// - [조작법]: 시작 화면과 같은 조작법 창(ControlsUI)을 중단 화면 위에 띄웁니다. 설정 창과 같은 방식(ESC/[닫기] = 조작법 창만 닫힘).
// - [도감]: 시작 화면과 같은 아이템 도감(ItemCodexUI)을 중단 화면 위에 띄웁니다. 도감에서 ESC/[돌아가기]를 누르면 도감만 닫히고
//   중단 화면으로 돌아옵니다(도감이 떠 있는 동안 중단 화면은 입력을 받지 않음 - ESC 한 번에 둘 다 닫히지 않도록).
// - [재시작하기] / [그만하기]: 흰 확인창("...하시겠습니까?" [실행] [취소하기])이 뜹니다. 확인창이 떠 있는 동안 중단 화면의 문구와
//   버튼은 숨겨집니다. [취소하기] 또는 ESC = 확인창만 닫히고 중단 화면으로 돌아옵니다.
// - Enter 키는 아무 동작도 하지 않습니다(형빈 확정). 클릭은 버튼 위에서 눌렀다가 뗀 순간입니다(누르는 동안 버튼이 작아짐).
// - 사라지는 동안에는 ESC를 다시 눌러도 열리지 않습니다 - 막이 완전히 사라지고 게임이 다시 흐른 뒤에 다시 열 수 있습니다.
//
// [열리지 않는 때 - 그때의 ESC는 원래 그 창의 닫기 키]
// 상점 창 / 슬롯 교체 확인창 / 슬롯 버리기 창 / 다음 레벨 이동 확인창 / 패시브 교체 창(56번)이 떠 있거나 방금 닫힌 프레임, 아이템 획득 연출 중, 로딩 화면 중,
// 플레이어 사망(게임 오버) 중, 플레이어가 없는 씬(타이틀 화면), 이미 다른 것이 게임을 멈춘 상태(Time.timeScale = 0).
//
// [멈춘 동안]
// 시간이 멈춰도 키 입력은 들어오므로, 다른 팝업과 같이 플레이어의 이동/조준·공격/장전/무기 전환/아이템 사용 컴포넌트를 잠시 끄고,
// 상자/포탈/좌대/상인의 F(InteractionTargetSelector)와 좌클릭 공격(HUDClickBlocker)도 막습니다(BlocksInteract). HUD 아이콘의
// 설명칸도 뜨지 않습니다(ItemTooltipDisplay). 연출은 모두 실제 시간 기준이라 멈춘 동안에도 움직입니다.
//
// [재시작하기]
// 게임 오버 창의 [예]와 같이 로딩 화면을 띄운 채 지금 씬을 다시 불러옵니다. GameFlowManager가 처음 시작할 때처럼 레벨 1을 새로
// (랜덤 구조로) 만들고, 플레이어/HUD/아이템 등 한 판 동안 쌓인 상태는 전부 초기화됩니다(도감 기록은 PC에 저장돼 있어 그대로).
// 게임은 멈춘 채로 넘어가고(로딩 중 적이 움직이지 않도록), 새 씬이 열린 뒤 시간을 원래대로 돌립니다.
//
// [그만하기]
// 게임 오버 창의 [아니오]와 같이 로딩 화면을 띄운 채 시작 화면 씬을 불러옵니다. 게임은 멈춘 채로 넘어가고, 새 씬이 열린 뒤 시간을
// 원래대로 돌립니다. 시작 화면 씬을 못 찾으면 오류를 남기고 중단 화면을 그대로 둡니다.
// 지금은 이어하기가 없어서 진행 상황은 사라집니다 - 이어하기를 만들 때 LeaveScene()의 표시된 자리에서 저장하면 됩니다.
//
// [색/설정]
// 버튼/확인창은 공용 부품 UIPatternBox(평행사변형 버튼, 흰 창 + 모서리 무늬)로 그리고 색/모양/글자 색은 PopupStyle, 그 밖의 문구/크기/
// 막 색/연출 시간은 PauseMenuSettings 에셋. 버튼 종류: [계속하기]/확인창의 실행 버튼 = 긍정(노랑), [그만하기] = 그만두기(회색),
// 나머지 = 일반(하늘색).
// 되돌릴 수 없는 버튼([재시작하기]/[그만하기]/확인창 실행)의 소리는 위험 효과음 그대로입니다.
// 씬에 붙일 필요가 없습니다 - 게임이 시작되면 스스로 만들어집니다(씬이 바뀌어도 유지).
public class PauseMenuUI : MonoBehaviour
{
    private enum Mode { Closed, Opening, Ready, Settings, Codex, Controls, Confirm, Closing, Quitting }
    private enum ConfirmAction { None, Restart, Quit }

    // ------------------------------------------------------------------
    // 외부에서 쓰는 정적 진입점
    // ------------------------------------------------------------------

    private static PauseMenuUI instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null) return;
        var go = new GameObject("PauseMenuUI");
        DontDestroyOnLoad(go);
        go.AddComponent<PauseMenuUI>();
    }

    // 중단 화면이 떠 있는지(열리는 중/사라지는 중/도감/확인창 포함 - 이 동안은 게임이 멈춰 있음)
    public static bool IsOpen => instance != null && instance.mode != Mode.Closed;

    // 상자/포탈/좌대/상인의 F, 공격 좌클릭을 막아야 하는지(떠 있거나, 방금 닫힌 그 프레임)
    public static bool BlocksInteract =>
        instance != null && (instance.mode != Mode.Closed || instance.closedFrame == Time.frameCount);

    // ------------------------------------------------------------------
    // 인스턴스 상태
    // ------------------------------------------------------------------

    private Mode mode = Mode.Closed;
    private ConfirmAction confirmAction = ConfirmAction.None;
    private float openTime;        // 열리기 시작한 실제 시간
    private float closeStartTime;  // 사라지기 시작한 실제 시간
    private int modeFrame = -1;    // 지금 상태가 시작된 프레임(그 프레임의 입력은 무시)
    private int closedFrame = -1;

    // 사라지기 시작한 순간의 값(그 자리에서 사라지기 시작하도록)
    private float closeFromDim;
    private float closeFromMessage;
    private float closeFromButtons;
    private float closeFromMessageOffset;
    private float closeFromButtonsOffset;

    // 지금 화면 값(0~1)
    private float dimAlpha;
    private float messageAlpha;
    private float buttonsAlpha;
    private float messageOffset;   // 문구 세로 이동량(px, 음수 = 아래)
    private float buttonsOffset;   // 버튼 세로 이동량(px)

    private float savedTimeScale = 1f;
    private readonly List<Behaviour> disabledBehaviours = new List<Behaviour>();

    // ---------------- UI ----------------
    private class ButtonView
    {
        public RectTransform rect;
        public UIPatternBox box;
        public TextMeshProUGUI label;
        public UIButtonKind kind = UIButtonKind.Normal;
    }

    private GameObject canvasGO;
    private Canvas canvas;
    private CanvasScaler scaler;
    private Image dimImage;
    private RectTransform messageRect;
    private TextMeshProUGUI messageText;
    private CanvasGroup messageGroup;
    private RectTransform buttonsRect;
    private CanvasGroup buttonsGroup;
    private ButtonView resumeButton;
    private ButtonView settingsButton;
    private ButtonView codexButton;
    private ButtonView controlsButton;
    private ButtonView restartButton;
    private ButtonView quitButton;

    // 확인창(검은 창)
    private GameObject confirmRoot;
    private RectTransform confirmRect;
    private UIPatternBox confirmBox;
    private TextMeshProUGUI confirmMessage;
    private ButtonView confirmOkButton;
    private ButtonView confirmCancelButton;

    void Awake()
    {
        if (instance == null) instance = this;
        else if (instance != this)
        {
            Destroy(gameObject);
            return;
        }

        BuildUI();
        canvasGO.SetActive(false);
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    void OnDestroy()
    {
        if (instance != this) return;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        if (mode != Mode.Closed)
        {
            Time.timeScale = savedTimeScale;
            RestorePlayerControls();
        }
        instance = null;
    }

    void Update()
    {
        var s = PauseMenuSettings.Get();

        switch (mode)
        {
            case Mode.Closed:
                if (Input.GetKeyDown(s.pauseKey) && CanOpenNow()) Open();
                return;

            case Mode.Opening:
                UpdateOpening(s);
                break;

            case Mode.Ready:
                UpdateReady(s);
                break;

            case Mode.Settings:
                UpdateSettings();
                break;

            case Mode.Codex:
                UpdateCodex();
                break;

            case Mode.Controls:
                UpdateControls();
                break;

            case Mode.Confirm:
                UpdateConfirm(s);
                break;

            case Mode.Closing:
                UpdateClosing(s);
                break;

            case Mode.Quitting:
                return; // 로딩 화면이 덮고 있는 동안 기다림(HandleSceneLoaded에서 정리)
        }

        if (mode != Mode.Closed && mode != Mode.Quitting) ApplyVisuals(s);
    }

    // ------------------------------------------------------------------
    // 열기
    // ------------------------------------------------------------------

    private static bool CanOpenNow()
    {
        if (Time.timeScale <= 0f) return false; // 이미 다른 것이 게임을 멈춤(게임 오버 등)
        if (ShopUI.IsOpen || ShopUI.BlocksInteract) return false;
        if (SlotReplaceConfirmUI.IsOpen || SlotReplaceConfirmUI.BlocksInteract) return false;
        if (SlotItemDropUI.IsOpen || SlotItemDropUI.BlocksInteract) return false;
        if (LevelTransitionConfirmUI.IsOpen || LevelTransitionConfirmUI.BlocksInteract) return false;
        if (PassiveRerollUI.IsOpen || PassiveRerollUI.BlocksInteract) return false;
        if (PlayerItemPickupPresenter.BlocksActions) return false;
        if (LoadingScreen.IsShowing) return false;
        if (IntroCutscene.BlocksInput) return false; // 입장 컷씬 중(ESC는 컷씬 건너뛰기 키)
        if (HeliExtraction.BlocksInput) return false; // 헬기 퇴각 연출 중(ESC는 건너뛰기 키)
        if (ItemCodexUI.BlocksInput) return false;
        if (SettingsUI.BlocksInput) return false;
        if (ControlsUI.BlocksInput) return false;
        if (GameClearUI.BlocksInput) return false; // 게임 클리어 결과 화면(ESC는 연출 건너뛰기 키)
        if (CutsceneStudio.IsActive) return false; // 촬영 스튜디오 씬

        var health = Object.FindFirstObjectByType<PlayerHealth>();
        return health != null && !health.IsDead;
    }

    private void Open()
    {
        GameAudio.Play(SfxId.PauseOpen);
        savedTimeScale = Time.timeScale;
        Time.timeScale = 0f;

        var health = Object.FindFirstObjectByType<PlayerHealth>();
        DisablePlayerControls(health);

        mode = Mode.Opening;
        modeFrame = Time.frameCount;
        openTime = Time.unscaledTime;
        confirmAction = ConfirmAction.None;

        dimAlpha = messageAlpha = buttonsAlpha = 0f;
        var s = PauseMenuSettings.Get();
        messageOffset = buttonsOffset = -s.riseDistance;

        SetMainButtonsNormal(); // 떠오르는 동안은 평소 색
        confirmRoot.SetActive(false);

        canvasGO.SetActive(true);
        ApplyVisuals(s);
    }

    private void UpdateOpening(PauseMenuSettings s)
    {
        float t = Time.unscaledTime - openTime;

        dimAlpha = Progress(t, 0f, s.dimFadeInDuration);
        float m = Progress(t, s.messageDelay, s.messageFadeInDuration);
        float b = Progress(t, s.buttonsDelay, s.buttonsFadeInDuration);
        messageAlpha = m;
        buttonsAlpha = b;
        messageOffset = -s.riseDistance * (1f - m);
        buttonsOffset = -s.riseDistance * (1f - b);

        // 버튼이 다 뜨기 전에 ESC를 누르면 그 자리에서 바로 사라지기 시작합니다(연 그 프레임의 ESC는 제외).
        if (Time.frameCount != modeFrame && Input.GetKeyDown(s.pauseKey))
        {
            BeginClose();
            return;
        }

        if (dimAlpha >= 1f && m >= 1f && b >= 1f) SetMode(Mode.Ready);
    }

    // 0~1 진행도(부드럽게)
    private static float Progress(float t, float delay, float duration)
    {
        float x = duration <= 0f ? (t >= delay ? 1f : 0f) : Mathf.Clamp01((t - delay) / duration);
        return x * x * (3f - 2f * x);
    }

    private void SetMode(Mode newMode)
    {
        mode = newMode;
        modeFrame = Time.frameCount;
    }

    // ------------------------------------------------------------------
    // 입력 대기
    // ------------------------------------------------------------------

    private int lastHoveredButton = -1; // [사운드] 호버음용 - 마지막으로 마우스가 올라간 버튼 번호

    private void UpdateReady(PauseMenuSettings s)
    {
        bool overResume = IsPointerOver(resumeButton.rect);
        bool overSettings = IsPointerOver(settingsButton.rect);
        bool overCodex = IsPointerOver(codexButton.rect);
        bool overControls = IsPointerOver(controlsButton.rect);
        bool overRestart = IsPointerOver(restartButton.rect);
        bool overQuit = IsPointerOver(quitButton.rect);
        bool clickResume = resumeButton.box.TickButton(overResume, true);
        bool clickSettings = settingsButton.box.TickButton(overSettings, true);
        bool clickCodex = codexButton.box.TickButton(overCodex, true);
        bool clickControls = controlsButton.box.TickButton(overControls, true);
        bool clickRestart = restartButton.box.TickButton(overRestart, true);
        bool clickQuit = quitButton.box.TickButton(overQuit, true);
        // [사운드] HoveredId는 한 번에 5개까지라 여섯 번째([조작법])는 번호 5로 따로 봅니다.
        int hoveredId = UISound.HoveredId(0, overResume, overSettings, overCodex, overRestart, overQuit);
        if (hoveredId < 0) hoveredId = UISound.HoveredId(5, overControls);
        UISound.TrackHover(ref lastHoveredButton, hoveredId, Time.frameCount == modeFrame);

        if (Time.frameCount == modeFrame) return;

        if (Input.GetKeyDown(s.pauseKey)) { UISound.Cancel(); BeginClose(); return; }

        if (clickResume) { UISound.Confirm(); BeginClose(); }
        else if (clickSettings) { UISound.Confirm(); OpenSettings(); }
        else if (clickCodex) { UISound.Confirm(); OpenCodex(); }
        else if (clickControls) { UISound.Confirm(); OpenControls(); }
        else if (clickRestart) { UISound.Danger(); OpenConfirm(ConfirmAction.Restart); }
        else if (clickQuit) { UISound.Danger(); OpenConfirm(ConfirmAction.Quit); }
    }

    // ------------------------------------------------------------------
    // 설정
    // ------------------------------------------------------------------

    private void OpenSettings()
    {
        SetMainButtonsNormal();
        // 이 중단 화면보다 위에 뜨도록(로딩 화면보다는 아래)
        // 중단 화면의 검은 막을 그대로 쓰므로 설정 창 자체의 막은 투명하게(확인창과 같은 어둡기)
        SettingsUI.Open(PauseMenuSettings.Get().canvasSortingOrder + 1, false);
        SetMode(Mode.Settings);
    }

    // 설정 창이 닫히면(그 프레임도 지나면) 중단 화면으로 돌아옵니다 - 설정 창을 닫은 ESC로 중단 화면까지 닫히지 않도록.
    private void UpdateSettings()
    {
        if (SettingsUI.BlocksInput) return;
        SetMode(Mode.Ready);
    }

    // ------------------------------------------------------------------
    // 도감
    // ------------------------------------------------------------------

    private void OpenCodex()
    {
        SetMainButtonsNormal();
        // 이 중단 화면보다 위에 뜨도록(로딩 화면/게임 오버 창보다는 아래)
        ItemCodexUI.Open(PauseMenuSettings.Get().canvasSortingOrder + 1);
        SetMode(Mode.Codex);
    }

    // 도감이 완전히 닫히면(사라지는 연출까지 끝나고, 그 프레임도 지나면) 중단 화면으로 돌아옵니다.
    private void UpdateCodex()
    {
        if (ItemCodexUI.BlocksInput) return;
        SetMode(Mode.Ready);
    }

    // ------------------------------------------------------------------
    // 조작법
    // ------------------------------------------------------------------

    private void OpenControls()
    {
        SetMainButtonsNormal();
        // 설정 창과 같이: 중단 화면보다 위에, 막은 중단 화면의 검은 막을 그대로 씀
        ControlsUI.Open(PauseMenuSettings.Get().canvasSortingOrder + 1, false);
        SetMode(Mode.Controls);
    }

    // 조작법 창이 닫히면(그 프레임도 지나면) 중단 화면으로 돌아옵니다 - 조작법 창을 닫은 ESC로 중단 화면까지 닫히지 않도록.
    private void UpdateControls()
    {
        if (ControlsUI.BlocksInput) return;
        SetMode(Mode.Ready);
    }

    // ------------------------------------------------------------------
    // 확인창 (재시작하기 / 그만하기)
    // ------------------------------------------------------------------

    private void OpenConfirm(ConfirmAction action)
    {
        var s = PauseMenuSettings.Get();
        confirmAction = action;

        string actionLabel = action == ConfirmAction.Restart ? s.restartLabel : s.quitLabel;
        confirmMessage.text = action == ConfirmAction.Restart ? s.restartConfirmMessage : s.quitConfirmMessage;
        confirmOkButton.label.text = string.IsNullOrEmpty(s.confirmLabel) ? actionLabel : s.confirmLabel;
        confirmCancelButton.label.text = s.cancelLabel;

        SetNormal(confirmOkButton);
        SetNormal(confirmCancelButton);
        SetMainButtonsNormal();

        confirmRoot.SetActive(true);
        SetMode(Mode.Confirm);
    }

    private void CloseConfirm()
    {
        confirmRoot.SetActive(false);
        confirmAction = ConfirmAction.None;
        SetMode(Mode.Ready);
    }

    private void UpdateConfirm(PauseMenuSettings s)
    {
        bool overOk = IsPointerOver(confirmOkButton.rect);
        bool overCancel = IsPointerOver(confirmCancelButton.rect);
        bool clickOk = confirmOkButton.box.TickButton(overOk, true);
        bool clickCancel = confirmCancelButton.box.TickButton(overCancel, true);
        UISound.TrackHover(ref lastHoveredButton, UISound.HoveredId(10, overOk, overCancel), Time.frameCount == modeFrame);

        if (Time.frameCount == modeFrame) return;

        if (Input.GetKeyDown(s.pauseKey)) { UISound.Cancel(); CloseConfirm(); return; }

        if (clickCancel) { UISound.Cancel(); CloseConfirm(); return; }
        if (clickOk)
        {
            UISound.Danger();
            if (confirmAction == ConfirmAction.Restart) RestartLevel();
            else if (confirmAction == ConfirmAction.Quit) QuitToTitle();
        }
    }

    // ------------------------------------------------------------------
    // 닫기 (계속하기)
    // ------------------------------------------------------------------

    private void BeginClose()
    {
        mode = Mode.Closing;
        modeFrame = Time.frameCount;
        closeStartTime = Time.unscaledTime;
        closeFromDim = dimAlpha;
        closeFromMessage = messageAlpha;
        closeFromButtons = buttonsAlpha;
        closeFromMessageOffset = messageOffset;
        closeFromButtonsOffset = buttonsOffset;

        SetMainButtonsNormal(); // 사라지는 동안 호버 표시 없음
    }

    private void UpdateClosing(PauseMenuSettings s)
    {
        float t = Time.unscaledTime - closeStartTime;
        float p = Progress(t, 0f, s.closeDuration);

        dimAlpha = closeFromDim * (1f - p);
        messageAlpha = closeFromMessage * (1f - p);
        buttonsAlpha = closeFromButtons * (1f - p);
        // 가라앉기: 사라지기 시작한 자리에서 riseDistance만큼 아래로
        float sink = s.riseDistance * p;
        messageOffset = closeFromMessageOffset - sink;
        buttonsOffset = closeFromButtonsOffset - sink;

        if (p >= 1f) FinishClose();
    }

    // 막이 완전히 사라진 순간: 이제서야 게임 시간과 조작을 되돌립니다.
    private void FinishClose()
    {
        mode = Mode.Closed;
        closedFrame = Time.frameCount;
        canvasGO.SetActive(false);

        Time.timeScale = savedTimeScale > 0f ? savedTimeScale : 1f;
        RestorePlayerControls();
    }

    // ------------------------------------------------------------------
    // 재시작하기 / 그만하기
    // ------------------------------------------------------------------

    // 게임 오버 창의 [예]와 같이 지금 씬을 다시 불러옵니다(레벨 1부터 새로 생성).
    private void RestartLevel()
    {
        LeaveScene(LoadingScreen.StartReloadActiveScene);
    }

    private void QuitToTitle()
    {
        var s = PauseMenuSettings.Get();
        if (!LoadingScreen.CanLoadScene(s.titleSceneName))
        {
            Debug.LogError($"PauseMenuUI: 시작 화면 씬 '{s.titleSceneName}'을 찾지 못했습니다. 씬 이름이 맞는지(Pause Menu Settings의 " +
                           "Title Scene Name), File > Build Profiles의 씬 목록에 들어 있는지 확인해주세요.");
            CloseConfirm();
            return;
        }

        string sceneName = s.titleSceneName;
        LeaveScene(() => LoadingScreen.StartSceneLoad(sceneName));
    }

    private void LeaveScene(System.Func<AsyncOperation> startLoad)
    {
        // [이어하기 연동 예정] 이어하기를 만들면 여기서 현재 진행 상황을 저장합니다(전투 중이 아닐 때 - 그만하기만).

        mode = Mode.Quitting;
        modeFrame = Time.frameCount;
        confirmAction = ConfirmAction.None;

        // 게임은 멈춘 채로 둡니다(로딩 중 적이 움직이지 않도록). 시간은 새 씬이 열린 뒤 HandleSceneLoaded에서 돌립니다.
        // 로딩 화면이 즉시 검게 덮으므로 중단 화면은 바로 숨깁니다. 끈 조작 컴포넌트는 씬과 함께 사라지므로 되돌리지 않습니다.
        confirmRoot.SetActive(false);
        canvasGO.SetActive(false);
        disabledBehaviours.Clear();

        LoadingScreen.LoadSceneWithLoading(startLoad);
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode loadMode)
    {
        if (mode != Mode.Quitting) return;
        mode = Mode.Closed;
        closedFrame = Time.frameCount;
        Time.timeScale = savedTimeScale > 0f ? savedTimeScale : 1f;
    }

    // ------------------------------------------------------------------
    // 플레이어 조작 잠금 (SlotReplaceConfirmUI와 같은 방식)
    // ------------------------------------------------------------------

    private void DisablePlayerControls(PlayerHealth health)
    {
        disabledBehaviours.Clear();
        GameObject p = health != null ? health.gameObject : null;

        var movement = FindOnPlayer<PlayerMovement>(p);
        var aim = FindOnPlayer<PlayerAimAndAttack>(p);
        ResetLocomotionAnimator(movement != null ? movement.animator : null, aim != null);

        TryDisable(movement);
        TryDisable(aim);
        TryDisable(FindOnPlayer<PlayerShooting>(p));
        TryDisable(FindOnPlayer<PlayerWeaponMode>(p));
        TryDisable(FindOnPlayer<PlayerActiveItem>(p));
        TryDisable(FindOnPlayer<PlayerDisposableItem>(p));
        TryDisable(FindOnPlayer<PlayerFoodItem>(p));
    }

    private static T FindOnPlayer<T>(GameObject p) where T : Component
    {
        T c = p != null ? p.GetComponent<T>() : null;
        return c != null ? c : Object.FindFirstObjectByType<T>();
    }

    private static void ResetLocomotionAnimator(Animator animator, bool resetAiming)
    {
        if (animator == null || animator.runtimeAnimatorController == null) return;
        foreach (var p in animator.parameters)
        {
            if (p.type == AnimatorControllerParameterType.Float &&
                (p.name == "MoveX" || p.name == "MoveZ" || p.name == "Speed"))
                animator.SetFloat(p.name, 0f);
            else if (resetAiming && p.type == AnimatorControllerParameterType.Bool && p.name == "IsAiming")
                animator.SetBool(p.name, false);
        }
    }

    private void TryDisable(Behaviour behaviour)
    {
        if (behaviour == null || !behaviour.enabled) return;
        behaviour.enabled = false;
        disabledBehaviours.Add(behaviour);
    }

    private void RestorePlayerControls()
    {
        foreach (var b in disabledBehaviours)
        {
            if (b != null) b.enabled = true;
        }
        disabledBehaviours.Clear();
    }

    private static bool IsPointerOver(RectTransform rect)
    {
        if (rect == null || !rect.gameObject.activeInHierarchy) return false;
        return RectTransformUtility.RectangleContainsScreenPoint(rect, Input.mousePosition, null);
    }

    private void SetMainButtonsNormal()
    {
        SetNormal(resumeButton);
        SetNormal(settingsButton);
        SetNormal(codexButton);
        SetNormal(controlsButton);
        SetNormal(restartButton);
        SetNormal(quitButton);
    }

    // 호버/누름 표시 없이 평소 색으로(입력을 받지 않는 동안)
    private static void SetNormal(ButtonView button)
    {
        button.box.ResetPress();
        button.box.color = PopupStyle.Get().GetKindColor(button.kind, false, true);
    }

    // ------------------------------------------------------------------
    // UI 생성 / 배치
    // ------------------------------------------------------------------

    private void BuildUI()
    {
        canvasGO = new GameObject("PauseMenuCanvas");
        canvasGO.transform.SetParent(transform, false);
        canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        // 화면 전체 검은 막
        var dimGO = new GameObject("Dim");
        dimGO.transform.SetParent(canvasGO.transform, false);
        var dimRect = dimGO.AddComponent<RectTransform>();
        dimRect.anchorMin = Vector2.zero;
        dimRect.anchorMax = Vector2.one;
        dimRect.offsetMin = Vector2.zero;
        dimRect.offsetMax = Vector2.zero;
        dimImage = dimGO.AddComponent<Image>();
        dimImage.raycastTarget = false;

        // 문구
        var messageGO = new GameObject("Message");
        messageGO.transform.SetParent(canvasGO.transform, false);
        messageRect = messageGO.AddComponent<RectTransform>();
        messageRect.anchorMin = messageRect.anchorMax = new Vector2(0.5f, 0.5f);
        messageRect.pivot = new Vector2(0.5f, 0.5f);
        messageGroup = messageGO.AddComponent<CanvasGroup>();
        messageGroup.blocksRaycasts = false;
        messageGroup.interactable = false;
        messageText = HUDText.AddTo(messageGO, TextAnchor.MiddleCenter, true);
        messageText.raycastTarget = false;

        // 버튼 두 줄(윗줄 계속하기 / 설정 / 조작법, 아랫줄 도감 / 재시작하기 / 그만하기)
        var buttonsGO = new GameObject("Buttons");
        buttonsGO.transform.SetParent(canvasGO.transform, false);
        buttonsRect = buttonsGO.AddComponent<RectTransform>();
        buttonsRect.anchorMin = buttonsRect.anchorMax = new Vector2(0.5f, 0.5f);
        buttonsRect.pivot = new Vector2(0.5f, 0.5f);
        buttonsRect.sizeDelta = Vector2.zero;
        buttonsGroup = buttonsGO.AddComponent<CanvasGroup>();
        buttonsGroup.blocksRaycasts = false;
        buttonsGroup.interactable = false;

        resumeButton = CreateButton(buttonsRect, "ResumeButton");
        settingsButton = CreateButton(buttonsRect, "SettingsButton");
        codexButton = CreateButton(buttonsRect, "CodexButton");
        controlsButton = CreateButton(buttonsRect, "ControlsButton");
        restartButton = CreateButton(buttonsRect, "RestartButton");
        quitButton = CreateButton(buttonsRect, "QuitButton");
        resumeButton.kind = UIButtonKind.Positive; // [계속하기] = 노랑(형빈 확정)
        quitButton.kind = UIButtonKind.Quit;

        // 확인창(흰 창) - 화면 가운데
        confirmRoot = new GameObject("ConfirmPopup");
        confirmRoot.transform.SetParent(canvasGO.transform, false);
        confirmRect = confirmRoot.AddComponent<RectTransform>();
        confirmRect.anchorMin = confirmRect.anchorMax = new Vector2(0.5f, 0.5f);
        confirmRect.pivot = new Vector2(0.5f, 0.5f);
        confirmBox = UIPatternBox.AddTo(confirmRoot);

        var confirmMsgGO = new GameObject("Message");
        confirmMsgGO.transform.SetParent(confirmRect, false);
        var confirmMsgRect = confirmMsgGO.AddComponent<RectTransform>();
        confirmMsgRect.anchorMin = confirmMsgRect.anchorMax = new Vector2(0.5f, 1f);
        confirmMsgRect.pivot = new Vector2(0.5f, 1f);
        confirmMessage = HUDText.AddTo(confirmMsgGO, TextAnchor.MiddleCenter, true);
        confirmMessage.raycastTarget = false;

        confirmOkButton = CreateButton(confirmRect, "OkButton");
        confirmCancelButton = CreateButton(confirmRect, "CancelButton");
        confirmOkButton.kind = UIButtonKind.Positive; // 재확인창의 실행 버튼 = 노랑, [취소하기] = 하늘색
        confirmOkButton.rect.anchorMin = confirmOkButton.rect.anchorMax = new Vector2(0.5f, 0f);
        confirmOkButton.rect.pivot = new Vector2(0.5f, 0f);
        confirmCancelButton.rect.anchorMin = confirmCancelButton.rect.anchorMax = new Vector2(0.5f, 0f);
        confirmCancelButton.rect.pivot = new Vector2(0.5f, 0f);

        confirmRoot.SetActive(false);
    }

    private static ButtonView CreateButton(Transform parent, string name)
    {
        var view = new ButtonView();

        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        view.rect = go.AddComponent<RectTransform>();
        view.rect.anchorMin = view.rect.anchorMax = new Vector2(0.5f, 0.5f);
        view.rect.pivot = new Vector2(0.5f, 0.5f);
        view.box = UIPatternBox.AddTo(go);

        var labelGO = new GameObject("Label");
        labelGO.transform.SetParent(go.transform, false);
        var labelRect = labelGO.AddComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        view.label = HUDText.AddTo(labelGO, TextAnchor.MiddleCenter);
        view.label.raycastTarget = false;

        return view;
    }

    // 설정/스타일 값과 지금 연출 값을 매 프레임 반영합니다(Play 중 에셋 값을 바꾸면 즉시 반영).
    private void ApplyVisuals(PauseMenuSettings s)
    {
        var style = PopupStyle.Get();

        canvas.sortingOrder = s.canvasSortingOrder;
        if (Screen.height > 0)
        {
            float screenAspect = (float)Screen.width / Screen.height;
            scaler.matchWidthOrHeight = screenAspect > 1920f / 1080f ? 1f : 0f; // HUD와 같은 규칙
        }

        Color dim = s.dimColor;
        dim.a *= dimAlpha;
        dimImage.color = dim;

        // 확인창/설정 창/조작법 창이 떠 있는 동안에는 중단 화면의 문구/버튼을 숨깁니다(검은 막 위에 그 창만).
        bool confirmShown = mode == Mode.Confirm || mode == Mode.Settings || mode == Mode.Controls;

        messageRect.sizeDelta = new Vector2(1600f, s.messageFontSize * 2f + 20f);
        messageRect.anchoredPosition = new Vector2(0f, s.messageOffsetY + messageOffset);
        messageText.text = s.message;
        messageText.fontSize = s.messageFontSize;
        messageText.color = s.messageColor;
        messageGroup.alpha = confirmShown ? 0f : messageAlpha;

        buttonsRect.anchoredPosition = new Vector2(0f, s.buttonsOffsetY + buttonsOffset);
        buttonsGroup.alpha = confirmShown ? 0f : buttonsAlpha;

        // 두 줄(형빈 확정): 윗줄 [계속하기] [설정] [조작법], 아랫줄 [도감] [재시작하기] [그만하기] - 각 줄 가운데 정렬
        float step = s.buttonSize.x + s.buttonSpacing;
        float lowerY = -(s.buttonSize.y + s.buttonRowSpacing);
        LayoutButton(resumeButton, s.resumeLabel, new Vector2(-step, 0f), s.buttonSize, s.buttonFontSize, style);
        LayoutButton(settingsButton, s.settingsLabel, new Vector2(0f, 0f), s.buttonSize, s.buttonFontSize, style);
        LayoutButton(controlsButton, s.controlsLabel, new Vector2(step, 0f), s.buttonSize, s.buttonFontSize, style);
        LayoutButton(codexButton, s.codexLabel, new Vector2(-step, lowerY), s.buttonSize, s.buttonFontSize, style);
        LayoutButton(restartButton, s.restartLabel, new Vector2(0f, lowerY), s.buttonSize, s.buttonFontSize, style);
        LayoutButton(quitButton, s.quitLabel, new Vector2(step, lowerY), s.buttonSize, s.buttonFontSize, style);

        if (confirmRoot.activeSelf) LayoutConfirm(s, style);
    }

    private void LayoutConfirm(PauseMenuSettings s, PopupStyle style)
    {
        Vector2 size = new Vector2(Mathf.Max(100f, s.confirmSize.x), Mathf.Max(80f, s.confirmSize.y));
        confirmRect.sizeDelta = size;
        confirmRect.anchoredPosition = Vector2.zero;
        confirmBox.SetupPanel(style);

        float pad = 20f;
        float buttonsTop = s.confirmButtonBottomMargin + s.confirmButtonSize.y;
        var msgRect = confirmMessage.rectTransform;
        msgRect.sizeDelta = new Vector2(size.x - pad * 2f, Mathf.Max(10f, size.y - buttonsTop - pad - 8f));
        msgRect.anchoredPosition = new Vector2(0f, -pad);
        confirmMessage.fontSize = s.confirmMessageFontSize;
        confirmMessage.color = style.panelTextColor;

        float half = (s.confirmButtonSize.x + s.confirmButtonSpacing) * 0.5f;
        LayoutButton(confirmOkButton, confirmOkButton.label.text, new Vector2(-half, s.confirmButtonBottomMargin), s.confirmButtonSize,
                     s.confirmButtonFontSize, style);
        LayoutButton(confirmCancelButton, confirmCancelButton.label.text, new Vector2(half, s.confirmButtonBottomMargin), s.confirmButtonSize,
                     s.confirmButtonFontSize, style);
    }

    private static void LayoutButton(ButtonView button, string text, Vector2 pos, Vector2 size, int fontSize, PopupStyle style)
    {
        button.rect.sizeDelta = size;
        button.rect.anchoredPosition = pos;
        // 색은 입력 대기 중 TickButton이 호버에 따라 정하고, 그 밖에는 평소 색(SetMainButtonsNormal/OpenConfirm에서 맞춤)
        button.box.SetupButton(style, button.kind);
        button.label.text = text;
        button.label.fontSize = fontSize;
        button.label.color = button.box.GetLabelColor(true);
    }
}
