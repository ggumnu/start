using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

// LevelPortal 상호작용 시 뜨는 "다음 레벨로 이동하시겠습니까?" 확인창입니다.
//
// [사용 방법]
// 씬에 빈 오브젝트를 하나 만들어서 이 스크립트만 붙이면 끝입니다. Canvas/패널/버튼을 전부 코드로 자동 생성하므로 에디터에서
// Canvas 계층을 직접 만들 필요가 없습니다.
//
// [모양/입력 - 다른 팝업과 같은 기준 (형빈 확정)]
// - 1920x1080 기준 크기로 화면 해상도에 맞춰 커지고 줄어듭니다(다른 HUD/팝업과 같은 규칙).
// - 창/버튼은 블루 아카이브 스타일 공용 부품(UIPatternBox - 흰 창 + 모서리 무늬, 평행사변형 버튼)으로 그리고, 색/모양/글자 색은
//   공용 스타일 에셋(PopupStyle)에서 읽습니다. [예] = 긍정(노랑), [아니오] = 일반(하늘색).
// - 입력은 다른 팝업과 같이 EventSystem 없이 마우스 좌표를 직접 비교합니다.
//   [예] = 클릭 또는 F/Enter, [아니오] = 클릭 또는 Esc. 클릭은 버튼 위에서 눌렀다가 뗀 순간(누르는 동안 버튼이 작아짐).
//   창을 연 그 프레임의 입력(포탈에 누른 F)은 무시합니다.
// - 창이 떠 있는 동안(+ 닫힌 그 프레임)에는 상자/포탈/좌대/상인의 F와 공격 좌클릭을 막습니다(BlocksInteract - InteractionTargetSelector/
//   HUDClickBlocker가 확인). 그래서 확인창의 F(예)나 버튼 클릭이 포탈/사격으로 다시 전달되지 않습니다.
// - 플레이어 이동은 예전처럼 막지 않습니다.
//
// [게임 오버]
// GameOverUI가 플레이어 사망 시 ForceHide()로 이 창을 닫고 이 컴포넌트를 끕니다.
public class LevelTransitionConfirmUI : MonoBehaviour
{
    [Header("문구")]
    public string message = "다음 레벨로 이동하시겠습니까?";
    public string yesLabel = "예";
    public string noLabel = "아니오";

    [Header("키")]
    [Tooltip("이 키를 누르면 [예]와 같습니다(Enter/숫자패드 Enter도 [예]). 창을 연 그 프레임의 입력은 무시합니다.")]
    public KeyCode confirmKey = KeyCode.F;
    [Tooltip("이 키를 누르면 [아니오]와 같습니다.")]
    public KeyCode cancelKey = KeyCode.Escape;

    [Header("창 모양 (1920x1080 기준 px - 바탕색/모서리/글자 색/버튼 색은 PopupStyle)")]
    public Vector2 panelSize = new Vector2(480f, 200f);
    public int messageFontSize = 26;

    [Header("버튼")]
    public Vector2 buttonSize = new Vector2(140f, 46f);
    [Tooltip("두 버튼 사이 간격입니다.")]
    public float buttonSpacing = 30f;
    [Tooltip("창 아래쪽 끝에서 버튼까지의 거리입니다.")]
    public float buttonBottomMargin = 24f;
    public int buttonFontSize = 22;

    [Header("표시 순서")]
    [Tooltip("HUD/툴팁보다 위, 게임 오버 창(100)보다 아래입니다.")]
    public int canvasSortingOrder = 88;

    // ------------------------------------------------------------------
    // 외부에서 쓰는 정적 진입점
    // ------------------------------------------------------------------

    private static LevelTransitionConfirmUI instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
    }

    // 창이 떠 있는지
    public static bool IsOpen => instance != null && instance.isOpen;

    // 상자/포탈/좌대/상인의 F, 공격 좌클릭을 막아야 하는지(창이 떠 있거나, 방금 닫힌 그 프레임)
    public static bool BlocksInteract =>
        instance != null && (instance.isOpen || instance.closedFrame == Time.frameCount);

    // ------------------------------------------------------------------
    // 인스턴스
    // ------------------------------------------------------------------

    private LevelPortal pendingPortal;
    private bool isOpen;
    private int openedFrame = -1;
    private int closedFrame = -1;

    private GameObject canvasGO;
    private Canvas canvas;
    private CanvasScaler scaler;
    private GameObject panelRoot;
    private RectTransform panelRect;
    private UIPatternBox panelBox;
    private TextMeshProUGUI messageText;
    private RectTransform yesRect;
    private UIPatternBox yesBox;
    private TextMeshProUGUI yesText;
    private RectTransform noRect;
    private UIPatternBox noBox;
    private TextMeshProUGUI noText;

    void Awake()
    {
        if (instance == null) instance = this;

        EnsureEventSystem();
        BuildUI();
        panelRoot.SetActive(false);

        LevelPortal.OnAnyPortalInteractionRequested += HandlePortalRequest;
    }

    void OnDestroy()
    {
        LevelPortal.OnAnyPortalInteractionRequested -= HandlePortalRequest;
        if (instance == this) instance = null;
    }

    private void HandlePortalRequest(LevelPortal portal)
    {
        if (!enabled) return;
        if (isOpen) return; // 이미 떠 있으면 그대로

        pendingPortal = portal;
        isOpen = true;
        openedFrame = Time.frameCount;

        yesBox.ResetPress();
        noBox.ResetPress();
        panelRoot.SetActive(true);
        ApplyLayout();
    }

    private int lastHoveredButton = -1; // [사운드] 호버음용 - 마지막으로 마우스가 올라간 버튼 번호

    void Update()
    {
        if (!isOpen) return;

        ApplyLayout();

        bool overYes = IsPointerOver(yesRect);
        bool overNo = IsPointerOver(noRect);
        bool clickYes = yesBox.TickButton(overYes, true);
        bool clickNo = noBox.TickButton(overNo, true);
        UISound.TrackHover(ref lastHoveredButton, UISound.HoveredId(0, overYes, overNo), Time.frameCount == openedFrame);

        // 창을 연 그 프레임의 입력(포탈에 누른 F)은 무시합니다.
        if (Time.frameCount == openedFrame) return;

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

    // 게임 오버 등으로 진행 없이 창만 닫습니다.
    public void ForceHide()
    {
        if (!isOpen) return;
        Hide();
        pendingPortal = null;
    }

    private void Hide()
    {
        isOpen = false;
        closedFrame = Time.frameCount;
        panelRoot.SetActive(false);
    }

    private void OnYesClicked()
    {
        UISound.Confirm();
        Hide();
        var portal = pendingPortal;
        pendingPortal = null;
        if (portal != null) portal.ConfirmAdvance();
    }

    private void OnNoClicked()
    {
        UISound.Cancel();
        Hide();
        var portal = pendingPortal;
        pendingPortal = null;
        if (portal != null) portal.CancelAdvance();
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
        canvasGO = new GameObject("LevelTransitionCanvas");
        canvasGO.transform.SetParent(transform);
        canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = canvasSortingOrder;
        scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        panelRoot = new GameObject("ConfirmPanel");
        panelRoot.transform.SetParent(canvasGO.transform, false);
        panelRect = panelRoot.AddComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelBox = UIPatternBox.AddTo(panelRoot);

        var textGO = new GameObject("Message");
        textGO.transform.SetParent(panelRect, false);
        var textRect = textGO.AddComponent<RectTransform>();
        textRect.anchorMin = textRect.anchorMax = new Vector2(0.5f, 1f);
        textRect.pivot = new Vector2(0.5f, 1f);
        messageText = HUDText.AddTo(textGO, TextAnchor.MiddleCenter, true);
        messageText.raycastTarget = false;

        CreateButton(panelRect, "YesButton", out yesRect, out yesBox, out yesText);
        CreateButton(panelRect, "NoButton", out noRect, out noBox, out noText);
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

        // 문구: 위 여백 ~ 버튼 위 공간의 가운데
        float pad = 20f;
        float buttonsTop = buttonBottomMargin + buttonSize.y;
        var msgRect = messageText.rectTransform;
        msgRect.sizeDelta = new Vector2(size.x - pad * 2f, Mathf.Max(10f, size.y - buttonsTop - pad - 8f));
        msgRect.anchoredPosition = new Vector2(0f, -pad);
        messageText.text = message;
        messageText.fontSize = messageFontSize;
        messageText.color = style.panelTextColor;

        float half = (buttonSize.x + buttonSpacing) * 0.5f;
        LayoutButton(yesRect, yesBox, yesText, yesLabel, UIButtonKind.Positive, new Vector2(-half, buttonBottomMargin), style);
        LayoutButton(noRect, noBox, noText, noLabel, UIButtonKind.Normal, new Vector2(half, buttonBottomMargin), style);
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
