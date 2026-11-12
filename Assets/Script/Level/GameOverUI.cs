using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 플레이어가 죽었을 때 뜨는 "게임 오버 / 다시 시작하시겠습니까?" 확인창입니다.
//
// [사용 방법]
// 씬에 빈 오브젝트를 하나 만들어서 이 스크립트만 붙이면 끝입니다. Canvas/패널/버튼을 전부 코드로 자동 생성합니다.
//
// [흐름]
// 1. PlayerHealth.OnAnyPlayerDied를 받으면 즉시 게임 전체를 정지합니다(Time.timeScale = 0 -
//    적/총알/이펙트/물리가 전부 멈춥니다. 플레이어 사망 애니메이션만 PlayerDeathHandler가 실제
//    시간으로 재생되도록 바꿔둡니다).
// 2. 정지와 동시에, 정지 중에도 키 입력은 계속 들어오므로 F키 상호작용(포탈/동아리방 보물상자/
//    일반맵 보물상자)을 꺼둡니다. 특히 포탈이 살아있으면 죽은 상태에서 다음 레벨로 넘어가는
//    문제가 생길 수 있어서입니다. 이미 열려 있던 포탈 확인창도 숨깁니다. (재시작이 씬을 통째로
//    다시 불러오는 방식이라 여기서 끈 것들은 되돌릴 필요가 없습니다.)
// 3. showDelay(실제 시간 기준 초) 뒤에 확인창을 띄웁니다.
// 4. [예](또는 F/Enter) → 정지를 풀고 로딩 화면(LoadingScreen)을 띄운 채 지금 씬을 다시 불러옵니다.
//    GameFlowManager가 평소처럼 레벨 1을 새로 빌드하는데, 방 구조는 매번 랜덤(LevelGraphGenerator가
//    시드 없이 생성)이라 항상 다른 구조가 나옵니다. 플레이어/HUD/아이템 등록소/총알 등 한 판 동안
//    쌓인 상태는 전부 초기화됩니다.
// 5. [아니오](또는 Esc) → 정지를 풀고 로딩 화면을 띄운 채 시작 화면 씬(titleSceneName)을 불러옵니다.
//    씬에 LoadingScreen이 없으면 두 버튼 모두 로딩 화면 없이 바로 씬을 불러옵니다.
//
// [모양/입력 - 다른 팝업과 같은 기준 (형빈 확정)]
// - 1920x1080 기준 크기로 화면 해상도에 맞춰 커지고 줄어듭니다(다른 HUD/팝업과 같은 규칙).
// - 창/버튼은 공용 부품 UIPatternBox(흰 창 + 모서리 무늬, 평행사변형 버튼)로 그리고, 색/모양/글자 색은 PopupStyle에서 읽습니다.
//   [예] = 긍정(노랑), [아니오] = 일반(하늘색).
// - 입력은 다른 팝업과 같이 EventSystem 없이 마우스 좌표를 직접 비교하고, 키(예: F/Enter, 아니오: Esc)도 받습니다.
//   클릭은 버튼 위에서 눌렀다가 뗀 순간입니다. 창이 뜬 그 프레임의 입력은 무시합니다.
//
// [씬 불러오기 - 주의]
// 에디터에서 Play 중일 때는 씬이 Build Profiles의 씬 목록에 없어도 불러올 수 있도록 처리해
// 뒀습니다(LoadingScreen.StartSceneLoad / StartReloadActiveScene). 하지만 실제로 게임을 빌드(exe)했을
// 때는 이 씬과 시작 화면 씬이 File > Build Profiles의 씬 목록에 반드시 들어 있어야 합니다.
public class GameOverUI : MonoBehaviour
{
    [Header("타이밍")]
    [Tooltip("플레이어가 죽은 뒤 이 시간(초, 실제 시간 기준)이 지나면 확인창을 띄웁니다. " +
             "사망 애니메이션 길이에 맞춰 조절하세요.")]
    public float showDelay = 2f;

    [Header("문구")]
    public string titleText = "게임 오버";
    public string messageText = "다시 시작하시겠습니까?";
    public string yesLabel = "예";
    public string noLabel = "아니오";

    [Header("키")]
    [Tooltip("이 키를 누르면 [예]와 같습니다(Enter/숫자패드 Enter도 [예]).")]
    public KeyCode confirmKey = KeyCode.F;
    [Tooltip("이 키를 누르면 [아니오]와 같습니다.")]
    public KeyCode cancelKey = KeyCode.Escape;

    [Header("시작 화면")]
    [Tooltip("[아니오]를 누르면 불러올 시작 화면 씬 이름입니다.")]
    public string titleSceneName = "Title";

    [Header("창 모양 (1920x1080 기준 px - 바탕색/모서리/글자 색/버튼 색은 PopupStyle)")]
    public Vector2 panelSize = new Vector2(480f, 270f);
    public int titleFontSize = 36;
    public int messageFontSize = 24;

    [Header("버튼")]
    public Vector2 buttonSize = new Vector2(140f, 46f);
    [Tooltip("두 버튼 사이 간격입니다.")]
    public float buttonSpacing = 30f;
    [Tooltip("창 아래쪽 끝에서 버튼까지의 거리입니다.")]
    public float buttonBottomMargin = 24f;
    public int buttonFontSize = 22;

    [Header("표시 순서")]
    [Tooltip("다른 HUD/팝업(툴팁 20, 슬롯 교체 확인창 90 등)보다 위에 보이도록 높은 값을 씁니다.")]
    public int canvasSortingOrder = 100;

    private Canvas canvas;
    private CanvasScaler scaler;
    private GameObject panelRoot;
    private RectTransform panelRect;
    private UIPatternBox panelBox;
    private TextMeshProUGUI titleTextComponent;
    private TextMeshProUGUI messageTextComponent;
    private RectTransform yesRect;
    private UIPatternBox yesBox;
    private TextMeshProUGUI yesLabelComponent;
    private RectTransform noRect;
    private UIPatternBox noBox;
    private TextMeshProUGUI noLabelComponent;

    private bool isGameOver;
    private bool restarting;
    private bool pausedByThis;
    private int shownFrame = -1;

    void Awake()
    {
        EnsureEventSystem();
        BuildUI();
        panelRoot.SetActive(false);

        PlayerHealth.OnAnyPlayerDied += HandlePlayerDied;
    }

    void OnDestroy()
    {
        PlayerHealth.OnAnyPlayerDied -= HandlePlayerDied;

        // 정지시킨 상태로 이 오브젝트가 사라지는 경우(Play 종료 등)에도 정지가 남지 않도록 풀어둡니다.
        if (pausedByThis) Time.timeScale = 1f;
    }

    private int lastHoveredButton = -1; // [사운드] 호버음용 - 마지막으로 마우스가 올라간 버튼 번호

    void Update()
    {
        if (panelRoot == null || !panelRoot.activeSelf) return;

        ApplyLayout();

        bool overYes = IsPointerOver(yesRect);
        bool overNo = IsPointerOver(noRect);
        bool clickYes = yesBox.TickButton(overYes, true);
        bool clickNo = noBox.TickButton(overNo, true);
        UISound.TrackHover(ref lastHoveredButton, UISound.HoveredId(0, overYes, overNo), Time.frameCount == shownFrame);

        // 창이 뜬 그 프레임의 입력은 무시합니다.
        if (Time.frameCount == shownFrame || restarting) return;

        if (Input.GetKeyDown(confirmKey) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            OnYesClicked();
            return;
        }
        if (Input.GetKeyDown(cancelKey))
        {
            OnNoClicked();
            return;
        }

        if (clickYes) OnYesClicked();
        else if (clickNo) OnNoClicked();
    }

    private void HandlePlayerDied(PlayerHealth player)
    {
        if (isGameOver) return;
        isGameOver = true;

        Time.timeScale = 0f;
        pausedByThis = true;

        DisableWorldInteractions();
        StartCoroutine(ShowAfterDelay());
    }

    private IEnumerator ShowAfterDelay()
    {
        if (showDelay > 0f) yield return new WaitForSecondsRealtime(showDelay);
        panelRoot.SetActive(true);
        shownFrame = Time.frameCount;
        GameAudio.Play(SfxId.GameOverShow);
        yesBox.ResetPress();
        noBox.ResetPress();
        ApplyLayout();
    }

    // 정지 중에도 Input은 계속 들어오므로, F키로 반응하는 오브젝트들을 꺼둡니다.
    private void DisableWorldInteractions()
    {
        foreach (var portal in Object.FindObjectsByType<LevelPortal>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            portal.enabled = false;
        foreach (var pedestal in Object.FindObjectsByType<ItemPedestal>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            pedestal.enabled = false;
        foreach (var chest in Object.FindObjectsByType<TreasureChest>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            chest.enabled = false;

        // 포탈 확인창이 이미 떠 있었다면 숨깁니다.
        foreach (var confirmUI in Object.FindObjectsByType<LevelTransitionConfirmUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            confirmUI.ForceHide();
            confirmUI.enabled = false;
        }
    }

    private void OnYesClicked()
    {
        if (restarting) return;
        UISound.Confirm();
        restarting = true;

        Time.timeScale = 1f;
        pausedByThis = false;

        LoadingScreen.LoadSceneWithLoading(LoadingScreen.StartReloadActiveScene);
    }

    private void OnNoClicked()
    {
        if (restarting) return;
        UISound.Cancel();

        // 시작 화면 씬을 못 찾으면 게임 오버 창을 그대로 둡니다(정지 상태 유지).
        if (!LoadingScreen.CanLoadScene(titleSceneName))
        {
            Debug.LogError($"GameOverUI: 시작 화면 씬 '{titleSceneName}'을 찾지 못했습니다. 씬 이름이 맞는지, " +
                           "File > Build Profiles의 씬 목록에 들어 있는지 확인해주세요.");
            return;
        }

        restarting = true;

        Time.timeScale = 1f;
        pausedByThis = false;

        string sceneName = titleSceneName;
        LoadingScreen.LoadSceneWithLoading(() => LoadingScreen.StartSceneLoad(sceneName));
    }

    private static bool IsPointerOver(RectTransform rect)
    {
        if (rect == null || !rect.gameObject.activeInHierarchy) return false;
        return RectTransformUtility.RectangleContainsScreenPoint(rect, Input.mousePosition, null);
    }

    // 이 창은 이제 마우스 좌표를 직접 비교하므로 EventSystem이 필요 없지만, 이 씬의 다른 uGUI가 기대고 있을 수 있어 예전처럼
    // 없으면 하나 만들어 둡니다.
    private void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>() != null) return;

        var esGO = new GameObject("EventSystem");
        esGO.AddComponent<EventSystem>();
        esGO.AddComponent<StandaloneInputModule>();
    }

    // ------------------------------------------------------------------
    // UI 생성 / 배치
    // ------------------------------------------------------------------

    private void BuildUI()
    {
        var canvasGO = new GameObject("GameOverCanvas");
        canvasGO.transform.SetParent(transform);
        canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = canvasSortingOrder;
        scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        panelRoot = new GameObject("GameOverPanel");
        panelRoot.transform.SetParent(canvasGO.transform, false);
        panelRect = panelRoot.AddComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelBox = UIPatternBox.AddTo(panelRoot);

        titleTextComponent = CreateText(panelRect, "Title");
        messageTextComponent = CreateText(panelRect, "Message");

        CreateButton(panelRect, "YesButton", out yesRect, out yesBox, out yesLabelComponent);
        CreateButton(panelRect, "NoButton", out noRect, out noBox, out noLabelComponent);
    }

    private static TextMeshProUGUI CreateText(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        var text = HUDText.AddTo(go, TextAnchor.MiddleCenter, true);
        text.raycastTarget = false;
        return text;
    }

    private static void CreateButton(Transform parent, string name, out RectTransform rect, out UIPatternBox box, out TextMeshProUGUI label)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        rect = go.AddComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        box = UIPatternBox.AddTo(go);

        var labelGO = new GameObject("Label");
        labelGO.transform.SetParent(go.transform, false);
        var labelRect = labelGO.AddComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        label = HUDText.AddTo(labelGO, TextAnchor.MiddleCenter);
        label.raycastTarget = false;
    }

    // 인스펙터/스타일 값을 매 프레임 반영합니다(Play 중 바꾸면 즉시 반영).
    private void ApplyLayout()
    {
        var style = PopupStyle.Get();

        canvas.sortingOrder = canvasSortingOrder;
        if (Screen.height > 0)
        {
            float screenAspect = (float)Screen.width / Screen.height;
            scaler.matchWidthOrHeight = screenAspect > 1920f / 1080f ? 1f : 0f; // HUD와 같은 규칙
        }

        Vector2 size = new Vector2(Mathf.Max(100f, panelSize.x), Mathf.Max(80f, panelSize.y));
        panelRect.sizeDelta = size;
        panelRect.anchoredPosition = Vector2.zero;
        panelBox.SetupPanel(style);

        float pad = 20f;
        float titleHeight = titleFontSize + 14f;
        var titleRect = titleTextComponent.rectTransform;
        titleRect.sizeDelta = new Vector2(size.x - pad * 2f, titleHeight);
        titleRect.anchoredPosition = new Vector2(0f, -pad - 4f);
        titleTextComponent.text = titleText;
        titleTextComponent.fontSize = titleFontSize;
        titleTextComponent.color = style.panelTextColor;

        float buttonsTop = buttonBottomMargin + buttonSize.y;
        float messageTop = pad + 4f + titleHeight + 4f;
        var messageRect = messageTextComponent.rectTransform;
        messageRect.sizeDelta = new Vector2(size.x - pad * 2f, Mathf.Max(10f, size.y - messageTop - buttonsTop - 8f));
        messageRect.anchoredPosition = new Vector2(0f, -messageTop);
        messageTextComponent.text = messageText;
        messageTextComponent.fontSize = messageFontSize;
        messageTextComponent.color = style.panelTextColor;

        float half = (buttonSize.x + buttonSpacing) * 0.5f;
        LayoutButton(yesRect, yesBox, yesLabelComponent, yesLabel, UIButtonKind.Positive, new Vector2(-half, buttonBottomMargin), style);
        LayoutButton(noRect, noBox, noLabelComponent, noLabel, UIButtonKind.Normal, new Vector2(half, buttonBottomMargin), style);
    }

    private void LayoutButton(RectTransform rect, UIPatternBox box, TextMeshProUGUI label, string text, UIButtonKind kind, Vector2 pos, PopupStyle style)
    {
        rect.sizeDelta = buttonSize;
        rect.anchoredPosition = pos;
        box.SetupButton(style, kind); // 색은 Update의 TickButton이 호버에 따라 정함
        label.text = text;
        label.fontSize = buttonFontSize;
        label.color = box.GetLabelColor(true);
    }
}
