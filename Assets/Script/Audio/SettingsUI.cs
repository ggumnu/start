using TMPro;
using UnityEngine;
using UnityEngine.UI;

// [설정] 창입니다. 시작 화면(TitleScreenUI)과 게임 중 ESC 중단 화면(PauseMenuUI)이 Open으로 엽니다 - 두 곳 모두 같은 창. BGM/효과음 볼륨 슬라이더 2개 + [닫기].
// 값은 GameAudio.UserBgmVolume/UserSfxVolume(PlayerPrefs 저장)에 바로 반영되고, 닫을 때 디스크에 기록합니다.
// 다른 팝업처럼 마우스 좌표를 직접 비교하고, 뒤 화면의 uGUI 버튼은 전체 화면 막(raycast)이 막습니다. 창 문구/크기는 SettingsUISettings.
// 창/[닫기]는 공용 부품 UIPatternBox(흰 창 + 모서리 무늬, 하늘색 평행사변형 버튼 - 떼는 순간 클릭), 글자 색은 PopupStyle.
//
// [탭 - 형빈 확정] 창 왼쪽에 책갈피 탭 [소리]/[HUD]. 탭은 창보다 먼저(뒤에) 그려서 오른쪽 부분이 창에 가려지고, 고른 탭만 왼쪽으로
// 튀어나옵니다(둘 다 하늘색). 클릭은 창 밖으로 보이는 부분에서만 받습니다. 처음 보이는 탭 = 마지막으로 본 탭(게임을 끄면 소리 탭부터).
// - 소리 탭: BGM/효과음 볼륨(예전 내용 그대로).
// - HUD 탭: 스탯 창 설정 + 전투/비전투 견본(SettingsHUDTab). 창 크기는 탭마다 다릅니다(SettingsUISettings의 panelSize / hudPanelSize).
public class SettingsUI : MonoBehaviour
{
    private static SettingsUI instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        lastTab = SoundTab;
    }

    public static bool IsOpen => instance != null && instance.isOpen;

    // 열려 있거나 방금 닫힌 프레임이면 true - 시작 화면 버튼이 같은 클릭/Esc에 반응하지 않도록.
    public static bool BlocksInput => instance != null && (instance.isOpen || instance.closedFrame == Time.frameCount);

    public static void Open()
    {
        Open(int.MinValue);
    }

    // minSortingOrder: 이 값보다 아래에 그려지지 않게 합니다(게임 중 ESC 중단 화면에서 열 때 중단 화면 위로 - PauseMenuUI).
    // showDim: false면 뒤를 어둡게 하는 막을 투명하게 둡니다(이미 검은 막이 있는 중단 화면에서 열 때). 막은 투명해도 뒤 클릭은 막습니다.
    public static void Open(int minSortingOrder, bool showDim = true)
    {
        if (instance == null)
        {
            var go = new GameObject("SettingsUI");
            instance = go.AddComponent<SettingsUI>();
        }
        instance.minSortingOrder = minSortingOrder;
        instance.showDim = showDim;
        instance.Show();
    }

    private class SliderView
    {
        public RectTransform row;
        public TextMeshProUGUI label;
        public RectTransform track;
        public Image trackImage;
        public RectTransform fill;
        public Image fillImage;
        public RectTransform knob;
        public UIPatternBox knobBox;
        public TextMeshProUGUI valueText;
    }

    private class TabView
    {
        public RectTransform rect;
        public UIPatternBox box;
        public TextMeshProUGUI label;
        public float pop; // 0 = 들어감, 1 = 튀어나옴(고른 탭)
    }

    private const int SoundTab = 0;
    private const int HudTab = 1;

    // 마지막으로 본 탭(게임을 켜 있는 동안만 기억 - 형빈 확정)
    private static int lastTab = SoundTab;

    private int currentTab = SoundTab;
    private TabView[] tabs;
    private SettingsHUDTab hudTab;

    private bool isOpen;
    private int minSortingOrder = int.MinValue;
    private bool showDim = true;
    private int openedFrame = -1;
    private int closedFrame = -1;
    private int draggingSlider = -1; // 0 = BGM, 1 = 효과음
    private int lastHoveredButton = -1;

    private GameObject canvasGO;
    private Canvas canvas;
    private CanvasScaler scaler;
    private Image dimImage;
    private RectTransform panelRect;
    private UIPatternBox panelBox;
    private TextMeshProUGUI titleText;
    private SliderView bgmSlider;
    private SliderView sfxSlider;
    private RectTransform closeRect;
    private UIPatternBox closeBox;
    private TextMeshProUGUI closeText;

    void Awake()
    {
        BuildUI();
        canvasGO.SetActive(false);
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
        if (hudTab != null) hudTab.Dispose();
    }

    private void Show()
    {
        if (isOpen) return;
        isOpen = true;
        openedFrame = Time.frameCount;
        draggingSlider = -1;
        closeBox.ResetPress();
        currentTab = lastTab;
        hudTab.CancelDrag();
        hudTab.ResetScroll();
        foreach (var tab in tabs)
        {
            tab.box.ResetPress();
        }
        for (int i = 0; i < tabs.Length; i++) tabs[i].pop = i == currentTab ? 1f : 0f; // 열 때는 애니메이션 없이
        canvasGO.SetActive(true);
        ApplyLayout();
    }

    private void Close()
    {
        if (!isOpen) return;
        isOpen = false;
        closedFrame = Time.frameCount;
        draggingSlider = -1;
        hudTab.CancelDrag();
        GameAudio.SaveUserVolumes();
        HUDUserSettings.Save();
        canvasGO.SetActive(false);
    }

    private void SwitchTab(int tab)
    {
        if (tab == currentTab) return;
        currentTab = tab;
        lastTab = tab;
        draggingSlider = -1;
        hudTab.CancelDrag();
        hudTab.ResetScroll();
        ApplyLayout();
    }

    void Update()
    {
        if (!isOpen) return;

        ApplyLayout();

        bool soundTab = currentTab == SoundTab;
        bool busy = draggingSlider >= 0 || hudTab.IsDragging;
        bool overClose = IsPointerOver(closeRect);
        bool overBgm = soundTab && IsPointerOverSlider(bgmSlider);
        bool overSfx = soundTab && IsPointerOverSlider(sfxSlider);
        // 탭은 창 밖으로 보이는 부분만 판정합니다(창 뒤에 숨은 부분을 눌러도 반응하지 않게).
        bool overPanel = IsPointerOver(panelRect);
        bool overSoundTab = !overPanel && IsPointerOver(tabs[SoundTab].rect);
        bool overHudTab = !overPanel && !overSoundTab && IsPointerOver(tabs[HudTab].rect);
        bool clickClose = closeBox.TickButton(overClose && !busy, true);
        bool clickSoundTab = tabs[SoundTab].box.TickButton(overSoundTab && !busy, true);
        bool clickHudTab = tabs[HudTab].box.TickButton(overHudTab && !busy, true);
        UISound.TrackHover(ref lastHoveredButton, UISound.HoveredId(0, overBgm, overSfx, overClose, overSoundTab, overHudTab),
                           Time.frameCount == openedFrame);

        if (Time.frameCount == openedFrame) return;

        // 끌고 있는 슬라이더: 버튼을 누르고 있는 동안 값 갱신, 떼면 끝(효과음은 뗄 때 바뀐 크기를 들려줌).
        if (draggingSlider >= 0)
        {
            if (Input.GetMouseButton(0))
            {
                SetValue(draggingSlider, ValueFromPointer(draggingSlider == 0 ? bgmSlider : sfxSlider));
            }
            else
            {
                if (draggingSlider == 1) UISound.Confirm();
                draggingSlider = -1;
            }
            return;
        }
        if (hudTab.IsDragging)
        {
            hudTab.Tick();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape)) { UISound.Cancel(); Close(); return; }

        if (clickClose) { UISound.Cancel(); Close(); return; }
        if (clickSoundTab) { UISound.Confirm(); SwitchTab(SoundTab); return; }
        if (clickHudTab) { UISound.Confirm(); SwitchTab(HudTab); return; }

        if (soundTab)
        {
            if (Input.GetMouseButtonDown(0))
            {
                if (overBgm) { draggingSlider = 0; SetValue(0, ValueFromPointer(bgmSlider)); return; }
                if (overSfx) { draggingSlider = 1; SetValue(1, ValueFromPointer(sfxSlider)); return; }
            }
        }
        else
        {
            hudTab.Tick();
        }
    }

    private static void SetValue(int slider, float value)
    {
        if (slider == 0) GameAudio.UserBgmVolume = value;
        else GameAudio.UserSfxVolume = value;
    }

    private static float ValueFromPointer(SliderView s)
    {
        if (s == null || s.track == null) return 0f;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(s.track, Input.mousePosition, null, out Vector2 local);
        float width = Mathf.Max(1f, s.track.rect.width);
        return Mathf.Clamp01((local.x - s.track.rect.xMin) / width);
    }

    private static bool IsPointerOver(RectTransform rect)
    {
        if (rect == null || !rect.gameObject.activeInHierarchy) return false;
        return RectTransformUtility.RectangleContainsScreenPoint(rect, Input.mousePosition, null);
    }

    // 손잡이가 잡기 쉽도록 슬라이더 줄 전체(손잡이 높이만큼) + 손잡이 반지름만큼 양옆을 판정 범위로 씁니다.
    private bool IsPointerOverSlider(SliderView s)
    {
        if (s == null || s.track == null || !s.track.gameObject.activeInHierarchy) return false;
        var set = SettingsUISettings.Get();
        RectTransformUtility.ScreenPointToLocalPointInRectangle(s.track, Input.mousePosition, null, out Vector2 local);
        Rect r = s.track.rect;
        float padX = set.knobSize * 0.5f;
        float halfH = Mathf.Max(r.height, set.knobSize) * 0.5f;
        return local.x >= r.xMin - padX && local.x <= r.xMax + padX && Mathf.Abs(local.y - r.center.y) <= halfH;
    }

    // ------------------------------------------------------------------
    // UI 생성 / 배치
    // ------------------------------------------------------------------

    private void BuildUI()
    {
        canvasGO = new GameObject("SettingsCanvas");
        canvasGO.transform.SetParent(transform, false);
        canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasGO.AddComponent<GraphicRaycaster>(); // 뒤 화면 uGUI 버튼 클릭을 막는 막(dim)용

        var dimGO = new GameObject("Dim");
        dimGO.transform.SetParent(canvasGO.transform, false);
        var dimRect = dimGO.AddComponent<RectTransform>();
        dimRect.anchorMin = Vector2.zero;
        dimRect.anchorMax = Vector2.one;
        dimRect.offsetMin = Vector2.zero;
        dimRect.offsetMax = Vector2.zero;
        dimImage = dimGO.AddComponent<Image>();
        dimImage.raycastTarget = true;

        // 탭은 창보다 먼저 만들어 창 뒤에 그려지게 합니다(책갈피처럼 오른쪽이 창에 가려짐).
        tabs = new TabView[2];
        for (int i = 0; i < tabs.Length; i++)
        {
            var tabGO = new GameObject(i == SoundTab ? "Tab_Sound" : "Tab_HUD");
            tabGO.transform.SetParent(canvasGO.transform, false);
            var tab = new TabView();
            tab.rect = tabGO.AddComponent<RectTransform>();
            tab.box = UIPatternBox.AddTo(tabGO);
            tab.label = CreateText(tab.rect, "Label", TextAnchor.MiddleCenter);
            tabs[i] = tab;
        }

        var panelGO = new GameObject("Panel");
        panelGO.transform.SetParent(canvasGO.transform, false);
        panelRect = panelGO.AddComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelBox = UIPatternBox.AddTo(panelGO);

        titleText = CreateText(panelRect, "Title", TextAnchor.MiddleCenter);

        bgmSlider = CreateSlider(panelRect, "BgmRow");
        sfxSlider = CreateSlider(panelRect, "SfxRow");
        hudTab = new SettingsHUDTab(panelRect);

        var closeGO = new GameObject("CloseButton");
        closeGO.transform.SetParent(panelRect, false);
        closeRect = closeGO.AddComponent<RectTransform>();
        closeRect.anchorMin = closeRect.anchorMax = new Vector2(0.5f, 0f);
        closeRect.pivot = new Vector2(0.5f, 0f);
        closeBox = UIPatternBox.AddTo(closeGO);
        closeText = CreateText(closeRect, "Label", TextAnchor.MiddleCenter);
        Stretch(closeText.rectTransform);
    }

    private static TextMeshProUGUI CreateText(Transform parent, string name, TextAnchor anchor)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        var text = HUDText.AddTo(go, anchor);
        text.raycastTarget = false;
        return text;
    }

    private static Image CreateImage(Transform parent, string name, out RectTransform rect)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        rect = go.AddComponent<RectTransform>();
        var image = go.AddComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static SliderView CreateSlider(Transform parent, string name)
    {
        var s = new SliderView();
        var rowGO = new GameObject(name);
        rowGO.transform.SetParent(parent, false);
        s.row = rowGO.AddComponent<RectTransform>();
        s.row.anchorMin = s.row.anchorMax = new Vector2(0.5f, 1f);
        s.row.pivot = new Vector2(0.5f, 0.5f);

        s.label = CreateText(s.row, "Label", TextAnchor.MiddleLeft);
        s.trackImage = CreateImage(s.row, "Track", out s.track);
        s.fillImage = CreateImage(s.track, "Fill", out s.fill);
        var knobGO = new GameObject("Knob");
        knobGO.transform.SetParent(s.track, false);
        s.knob = knobGO.AddComponent<RectTransform>();
        s.knobBox = UIPatternBox.AddTo(knobGO);
        s.valueText = CreateText(s.row, "Value", TextAnchor.MiddleRight);
        return s;
    }

    // 설정/스타일 값을 매 프레임 반영합니다(Play 중 에셋 값을 바꾸면 바로 보임).
    private void ApplyLayout()
    {
        var set = SettingsUISettings.Get();
        var style = PopupStyle.Get();

        canvas.sortingOrder = Mathf.Max(set.canvasSortingOrder, minSortingOrder);
        if (Screen.height > 0)
        {
            float screenAspect = (float)Screen.width / Screen.height;
            scaler.matchWidthOrHeight = screenAspect > 1920f / 1080f ? 1f : 0f; // HUD와 같은 규칙
        }
        dimImage.color = showDim ? set.dimColor : Color.clear;

        bool soundTab = currentTab == SoundTab;
        Vector2 size = soundTab
            ? new Vector2(Mathf.Max(300f, set.panelSize.x), Mathf.Max(200f, set.panelSize.y))
            // HUD 탭은 높이 고정 - 넘치는 내용은 탭 안에서 세로 스크롤(SettingsHUDTab)
            : new Vector2(Mathf.Max(600f, set.hudPanelSize.x), Mathf.Clamp(set.hudPanelVisibleHeight, 400f, 1070f));
        panelRect.sizeDelta = size;
        panelRect.anchoredPosition = soundTab ? Vector2.zero : set.hudPanelOffset;
        panelBox.SetupPanel(style);

        LayoutTabs(set, style, size);

        var titleRect = titleText.rectTransform;
        titleRect.anchorMin = titleRect.anchorMax = new Vector2(0.5f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.sizeDelta = new Vector2(size.x - 40f, set.titleFontSize + 16f);
        titleRect.anchoredPosition = new Vector2(0f, -20f);
        titleText.text = set.title;
        titleText.fontSize = set.titleFontSize;
        titleText.color = style.panelTextColor;

        if (bgmSlider.row.gameObject.activeSelf != soundTab) bgmSlider.row.gameObject.SetActive(soundTab);
        if (sfxSlider.row.gameObject.activeSelf != soundTab) sfxSlider.row.gameObject.SetActive(soundTab);
        hudTab.SetVisible(!soundTab);
        if (soundTab)
        {
            float firstRowY = -(20f + set.titleFontSize + 16f + 40f);
            LayoutSlider(bgmSlider, set, set.bgmLabel, GameAudio.UserBgmVolume, size.x, firstRowY, style);
            LayoutSlider(sfxSlider, set, set.sfxLabel, GameAudio.UserSfxVolume, size.x, firstRowY - set.rowSpacing, style);
        }
        else
        {
            hudTab.Layout(size);
        }

        closeRect.sizeDelta = set.buttonSize;
        closeRect.anchoredPosition = new Vector2(0f, set.buttonBottomMargin);
        closeBox.SetupButton(style, UIButtonKind.Normal); // 색은 Update의 TickButton이 호버에 따라 정함
        closeText.text = set.closeLabel;
        closeText.fontSize = set.buttonFontSize;
        closeText.color = closeBox.GetLabelColor(true);
    }

    // 책갈피 탭: 창 왼쪽 끝 뒤에 tabHiddenWidth만큼 숨고, 고른 탭은 tabPopOut만큼 더 튀어나옵니다(실제 시간 기준으로 부드럽게).
    private void LayoutTabs(SettingsUISettings set, PopupStyle style, Vector2 panelSize)
    {
        float step = set.tabMoveDuration > 0f ? Time.unscaledDeltaTime / set.tabMoveDuration : 1f;
        Vector2 panelPos = panelRect.anchoredPosition;
        float panelLeft = panelPos.x - panelSize.x * 0.5f;
        float panelTop = panelPos.y + panelSize.y * 0.5f;

        for (int i = 0; i < tabs.Length; i++)
        {
            var tab = tabs[i];
            tab.pop = Mathf.MoveTowards(tab.pop, i == currentTab ? 1f : 0f, step);
            float eased = tab.pop * tab.pop * (3f - 2f * tab.pop);

            tab.rect.anchorMin = tab.rect.anchorMax = new Vector2(0.5f, 0.5f);
            tab.rect.pivot = new Vector2(1f, 1f); // 오른쪽 위 기준(오른쪽 끝이 창 뒤에 숨음)
            tab.rect.sizeDelta = set.tabSize;
            tab.rect.anchoredPosition = new Vector2(panelLeft + set.tabHiddenWidth - set.tabPopOut * eased,
                                                    panelTop - set.tabTopMargin - i * (set.tabSize.y + set.tabSpacing));
            tab.box.SetupButton(style, UIButtonKind.Normal); // 색은 Update의 TickButton이 호버에 따라 정함

            // 글자는 창 밖으로 보이는 부분의 가운데에
            var labelRect = tab.label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = new Vector2(-set.tabHiddenWidth, 0f);
            tab.label.text = i == SoundTab ? set.soundTabLabel : set.hudTabLabel;
            tab.label.fontSize = set.tabFontSize;
            tab.label.color = tab.box.GetLabelColor(true);
        }
    }

    // 한 줄 = [이름][슬라이더][00%] - 가운데 정렬.
    private static void LayoutSlider(SliderView s, SettingsUISettings set, string label, float value, float panelWidth, float y,
                                     PopupStyle style)
    {
        const float labelWidth = 130f;
        const float valueWidth = 80f;
        const float gap = 16f;
        float rowWidth = labelWidth + gap + set.sliderWidth + gap + valueWidth;
        float rowHeight = Mathf.Max(set.knobSize, set.labelFontSize + 8f);

        s.row.sizeDelta = new Vector2(rowWidth, rowHeight);
        s.row.anchoredPosition = new Vector2(0f, y);

        var labelRect = s.label.rectTransform;
        labelRect.anchorMin = labelRect.anchorMax = new Vector2(0f, 0.5f);
        labelRect.pivot = new Vector2(0f, 0.5f);
        labelRect.sizeDelta = new Vector2(labelWidth, rowHeight);
        labelRect.anchoredPosition = Vector2.zero;
        s.label.text = label;
        s.label.fontSize = set.labelFontSize;
        s.label.color = style.panelTextColor;

        s.track.anchorMin = s.track.anchorMax = new Vector2(0f, 0.5f);
        s.track.pivot = new Vector2(0f, 0.5f);
        Vector2 trackSize = new Vector2(set.sliderWidth, set.sliderHeight);
        s.track.sizeDelta = trackSize;
        s.track.anchoredPosition = new Vector2(labelWidth + gap, 0f);
        UIRoundedBox.Apply(s.trackImage, set.sliderTrackColor, set.sliderHeight * 0.5f, trackSize);

        float v = Mathf.Clamp01(value);
        float fillWidth = Mathf.Max(set.sliderHeight, set.sliderWidth * v);
        s.fill.anchorMin = s.fill.anchorMax = new Vector2(0f, 0.5f);
        s.fill.pivot = new Vector2(0f, 0.5f);
        Vector2 fillSize = new Vector2(fillWidth, set.sliderHeight);
        s.fill.sizeDelta = fillSize;
        s.fill.anchoredPosition = Vector2.zero;
        UIRoundedBox.Apply(s.fillImage, v > 0f ? set.fillColor : Color.clear, set.sliderHeight * 0.5f, fillSize);

        s.knob.anchorMin = s.knob.anchorMax = new Vector2(0f, 0.5f);
        s.knob.pivot = new Vector2(0.5f, 0.5f);
        Vector2 knobSize = new Vector2(set.knobSize, set.knobSize);
        s.knob.sizeDelta = knobSize;
        s.knob.anchoredPosition = new Vector2(set.sliderWidth * v, 0f);
        s.knobBox.SetupShape(style, set.knobColor, set.knobSize * 0.5f, 0f, true);

        var valueRect = s.valueText.rectTransform;
        valueRect.anchorMin = valueRect.anchorMax = new Vector2(1f, 0.5f);
        valueRect.pivot = new Vector2(1f, 0.5f);
        valueRect.sizeDelta = new Vector2(valueWidth, rowHeight);
        valueRect.anchoredPosition = Vector2.zero;
        s.valueText.text = Mathf.RoundToInt(v * 100f) + "%";
        s.valueText.fontSize = set.labelFontSize;
        s.valueText.color = style.panelTextColor;
    }
}
