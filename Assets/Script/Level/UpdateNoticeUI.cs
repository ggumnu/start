using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;

// [업데이트 알림 (형빈 확정 - 방식 A)]
// 시작 화면에서 GitHub의 최신 Release 버전 번호를 확인해, 지금 게임보다 새 버전이 있으면
// "새 버전 vX.Y.Z이(가) 나왔습니다!" 창을 띄웁니다. [다운로드 페이지 열기] = 브라우저로 Release 화면 열기, [나중에] = 닫기.
// 자동으로 내려받거나 설치하지는 않습니다(보안 - 게임은 인터넷에서 받은 것을 실행하지 않음).
//
// [언제]
// - 게임을 실행한 뒤 시작 화면이 처음 열릴 때 한 번만 확인하고, 창도 실행당 한 번만 띄웁니다(게임 중이나 타이틀로 돌아올 때는 다시 묻지 않음).
// - 시작 연출이 끝나고, 도감/조작법/설정 창이 닫혀 있을 때 띄웁니다.
// - 인터넷이 없거나 GitHub가 답하지 않거나 형식이 이상하면 조용히 넘어갑니다(게임에 영향 없음).
//
// [버전 비교] Player Settings의 Version(Application.version)과 Release 태그(v1.0.1 등, 앞의 v는 무시)를 숫자로 비교합니다.
//
// [사용 방법] 따로 붙일 필요 없습니다. TitleScreenUI가 Awake에서 UpdateNoticeUI.Ensure(this)로 자동으로 만듭니다.
// 설정은 UpdateNoticeSettings(없으면 기본값).
//
// [모양/입력 - 다른 확인창(LevelTransitionConfirmUI)과 같은 기준]
// 흰 창 + 모서리 무늬(UIPatternBox), [다운로드 페이지 열기] = 긍정(노랑), [나중에] = 일반(하늘색). 색/모양은 PopupStyle.
// 클릭은 버튼 위에서 눌렀다가 뗀 순간, Enter = 열기, Esc = 나중에. 창이 떠 있는 동안 시작 화면 버튼은 반응하지 않습니다(BlocksInput).
public class UpdateNoticeUI : MonoBehaviour
{
    // ------------------------------------------------------------------
    // 정적 상태 (실행당 한 번만 확인/표시)
    // ------------------------------------------------------------------

    private static UpdateNoticeUI instance;
    private static bool checkStarted;
    private static bool checkDone;
    private static string latestVersionText; // 새 버전이 있을 때만 채움(예: "v1.0.1")
    private static bool shownThisRun;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        checkStarted = false;
        checkDone = false;
        latestVersionText = null;
        shownThisRun = false;
    }

    // 시작 화면 버튼을 막아야 하는지(창이 떠 있거나, 방금 닫힌 그 프레임)
    public static bool BlocksInput =>
        instance != null && (instance.isOpen || instance.closedFrame == Time.frameCount);

    // TitleScreenUI가 Awake에서 부릅니다. 시작 화면 아래에 이 창을 하나 만듭니다.
    public static void Ensure(TitleScreenUI title)
    {
        if (title == null || instance != null) return;
        var go = new GameObject("UpdateNoticeUI");
        go.transform.SetParent(title.transform, false);
        var ui = go.AddComponent<UpdateNoticeUI>();
        ui.title = title;
    }

    // ------------------------------------------------------------------
    // 인스턴스
    // ------------------------------------------------------------------

    [Serializable]
    private class ReleaseInfo
    {
        public string tag_name;
    }

    private TitleScreenUI title;
    private bool isOpen;
    private int openedFrame = -1;
    private int closedFrame = -1;
    private int lastHoveredButton = -1;

    private GameObject canvasGO;
    private Canvas canvas;
    private UnityEngine.UI.CanvasScaler scaler;
    private GameObject panelRoot;
    private RectTransform panelRect;
    private UIPatternBox panelBox;
    private TextMeshProUGUI messageText;
    private RectTransform openRect;
    private UIPatternBox openBox;
    private TextMeshProUGUI openText;
    private RectTransform laterRect;
    private UIPatternBox laterBox;
    private TextMeshProUGUI laterText;

    void Awake()
    {
        if (instance == null) instance = this;
        BuildUI();
        panelRoot.SetActive(false);
    }

    void Start()
    {
        if (checkStarted) return;
        var settings = UpdateNoticeSettings.Get();
        if (!settings.enabled) return;
        checkStarted = true;
        StartCoroutine(CheckRoutine(settings));
    }

    void OnDestroy()
    {
        // 확인 도중 시작 화면을 떠났으면 다음에 시작 화면이 열릴 때 다시 확인합니다.
        if (!checkDone) checkStarted = false;
        if (instance == this) instance = null;
    }

    private IEnumerator CheckRoutine(UpdateNoticeSettings settings)
    {
        string url = (settings.latestReleaseApiUrl ?? "").Trim();
        if (!url.StartsWith("https://api.github.com/", StringComparison.OrdinalIgnoreCase))
        {
            checkDone = true;
            yield break;
        }

        UnityWebRequest request = null;
        try
        {
            request = UnityWebRequest.Get(url);
            request.SetRequestHeader("Accept", "application/vnd.github+json");
            request.SetRequestHeader("User-Agent", "Reisa_Archive-UpdateCheck");
            request.timeout = Mathf.Max(1, settings.timeoutSeconds);
        }
        catch (Exception)
        {
            request = null;
        }

        if (request != null)
        {
            yield return request.SendWebRequest();

            try
            {
                if (request.result == UnityWebRequest.Result.Success)
                {
                    var info = JsonUtility.FromJson<ReleaseInfo>(request.downloadHandler.text);
                    if (info != null && IsNewer(info.tag_name, Application.version))
                        latestVersionText = Clean(info.tag_name);
                }
            }
            catch (Exception)
            {
                // 형식이 이상하면 조용히 넘어갑니다.
            }
            request.Dispose();
        }

        checkDone = true;
    }

    void Update()
    {
        if (!isOpen)
        {
            if (ShouldOpen()) Open();
            return;
        }

        ApplyLayout();

        bool overOpen = IsPointerOver(openRect);
        bool overLater = IsPointerOver(laterRect);
        bool clickOpen = openBox.TickButton(overOpen, true);
        bool clickLater = laterBox.TickButton(overLater, true);
        UISound.TrackHover(ref lastHoveredButton, UISound.HoveredId(0, overOpen, overLater), Time.frameCount == openedFrame);

        if (Time.frameCount == openedFrame) return; // 창을 연 그 프레임의 입력은 무시

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            OnOpenClicked();
            return;
        }
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            OnLaterClicked();
            return;
        }

        if (clickOpen) OnOpenClicked();
        else if (clickLater) OnLaterClicked();
    }

    private bool ShouldOpen()
    {
        if (shownThisRun) return false;
        if (title != null && title.IsBusy) return false; // 시작 연출 중이거나 씬을 떠나는 중
        if (ItemCodexUI.BlocksInput || ControlsUI.BlocksInput || SettingsUI.BlocksInput) return false;

        if (Application.isEditor && UpdateNoticeSettings.Get().forceShowInEditor)
        {
            if (string.IsNullOrEmpty(latestVersionText)) latestVersionText = "v9.9.9 (테스트)";
            return true;
        }
        return checkDone && !string.IsNullOrEmpty(latestVersionText);
    }

    private void Open()
    {
        shownThisRun = true;
        isOpen = true;
        openedFrame = Time.frameCount;
        lastHoveredButton = -1;
        openBox.ResetPress();
        laterBox.ResetPress();
        panelRoot.SetActive(true);
        ApplyLayout();
    }

    private void Hide()
    {
        isOpen = false;
        closedFrame = Time.frameCount;
        panelRoot.SetActive(false);
    }

    private void OnOpenClicked()
    {
        UISound.Confirm();
        Hide();
        // 서버가 알려준 주소가 아니라 설정에 적힌 GitHub 주소만 엽니다.
        string page = (UpdateNoticeSettings.Get().releasePageUrl ?? "").Trim();
        if (page.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase)) Application.OpenURL(page);
    }

    private void OnLaterClicked()
    {
        UISound.Cancel();
        Hide();
    }

    // ------------------------------------------------------------------
    // 버전 비교
    // ------------------------------------------------------------------

    // remote가 local보다 새 버전이면 true. 둘 중 하나라도 숫자로 읽지 못하면 false(창을 띄우지 않음).
    private static bool IsNewer(string remote, string local)
    {
        if (!TryParseVersion(remote, out Version r) || !TryParseVersion(local, out Version l)) return false;
        return r > l;
    }

    private static bool TryParseVersion(string text, out Version version)
    {
        version = null;
        string s = Clean(text);
        if (s.StartsWith("v", StringComparison.OrdinalIgnoreCase)) s = s.Substring(1);
        int cut = s.IndexOfAny(new[] { '-', '+', ' ' });
        if (cut >= 0) s = s.Substring(0, cut);
        if (!Version.TryParse(s, out Version parsed)) return false;
        // 1.0 과 1.0.0 을 같게 보도록 세 자리로 맞춥니다.
        version = new Version(parsed.Major, parsed.Minor, Math.Max(0, parsed.Build));
        return true;
    }

    // 화면에 보여줄 태그 글자를 안전하게 다듬습니다(짧게 자르고 리치 텍스트 기호 제거).
    private static string Clean(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        string s = text.Trim().Replace("<", "").Replace(">", "");
        return s.Length > 20 ? s.Substring(0, 20) : s;
    }

    // ------------------------------------------------------------------
    // UI 생성 / 배치 (LevelTransitionConfirmUI와 같은 방식)
    // ------------------------------------------------------------------

    private static bool IsPointerOver(RectTransform rect)
    {
        if (rect == null || !rect.gameObject.activeInHierarchy) return false;
        return RectTransformUtility.RectangleContainsScreenPoint(rect, Input.mousePosition, null);
    }

    private void BuildUI()
    {
        canvasGO = new GameObject("UpdateNoticeCanvas");
        canvasGO.transform.SetParent(transform);
        canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        scaler = canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        panelRoot = new GameObject("UpdatePanel");
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
        messageText.richText = true;

        CreateButton(panelRect, "OpenButton", out openRect, out openBox, out openText);
        CreateButton(panelRect, "LaterButton", out laterRect, out laterBox, out laterText);
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

    private void ApplyLayout()
    {
        var settings = UpdateNoticeSettings.Get();
        var style = PopupStyle.Get();

        canvas.sortingOrder = settings.canvasSortingOrder;
        if (Screen.height > 0)
        {
            float screenAspect = (float)Screen.width / Screen.height;
            scaler.matchWidthOrHeight = screenAspect > 1920f / 1080f ? 1f : 0f; // HUD와 같은 규칙
        }

        Vector2 size = new Vector2(Mathf.Max(100f, settings.panelSize.x), Mathf.Max(80f, settings.panelSize.y));
        panelRect.sizeDelta = size;
        panelRect.anchoredPosition = Vector2.zero;
        panelBox.SetupPanel(style);

        float buttonHeight = Mathf.Max(settings.openButtonSize.y, settings.laterButtonSize.y);
        float pad = 20f;
        float buttonsTop = settings.buttonBottomMargin + buttonHeight;
        var msgRect = messageText.rectTransform;
        msgRect.sizeDelta = new Vector2(size.x - pad * 2f, Mathf.Max(10f, size.y - buttonsTop - pad - 8f));
        msgRect.anchoredPosition = new Vector2(0f, -pad);
        messageText.text = FormatMessage(settings.message, latestVersionText, Application.version);
        messageText.fontSize = settings.messageFontSize;
        messageText.color = style.panelTextColor;

        // 두 버튼을 가운데 정렬로 나란히 놓습니다(크기가 달라도 가운데가 맞도록).
        float total = settings.openButtonSize.x + settings.buttonSpacing + settings.laterButtonSize.x;
        float openX = -total * 0.5f + settings.openButtonSize.x * 0.5f;
        float laterX = total * 0.5f - settings.laterButtonSize.x * 0.5f;
        LayoutButton(openRect, openBox, openText, settings.openLabel, UIButtonKind.Positive, settings.openButtonSize,
            new Vector2(openX, settings.buttonBottomMargin), settings.buttonFontSize, style);
        LayoutButton(laterRect, laterBox, laterText, settings.laterLabel, UIButtonKind.Normal, settings.laterButtonSize,
            new Vector2(laterX, settings.buttonBottomMargin), settings.buttonFontSize, style);
    }

    private static string FormatMessage(string format, string latest, string current)
    {
        try { return string.Format(format ?? "", latest ?? "", current ?? ""); }
        catch (FormatException) { return format ?? ""; }
    }

    private static void LayoutButton(RectTransform rect, UIPatternBox box, TextMeshProUGUI label, string text, UIButtonKind kind,
        Vector2 size, Vector2 pos, int fontSize, PopupStyle style)
    {
        rect.sizeDelta = size;
        rect.anchoredPosition = pos;
        box.SetupButton(style, kind); // 색은 Update의 TickButton이 호버에 따라 정함
        label.text = text;
        label.fontSize = fontSize;
        label.color = box.GetLabelColor(true);
    }
}
