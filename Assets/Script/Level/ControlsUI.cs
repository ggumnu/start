using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// [조작법] 창입니다. 시작 화면(TitleScreenUI)과 게임 중 ESC 중단 화면(PauseMenuUI)이 Open으로 엽니다 - 두 곳 모두 같은 창.
// 흰 창(제목 + 가는 줄) 안에 묶음별로 "동작 이름 + 남색 키 배지" 줄이 놓이고, 창보다 길면 마우스 휠/오른쪽 스크롤 막대로 내려 봅니다. 아래에 [닫기].
// 설정 창(SettingsUI)과 같은 방식: 마우스 좌표를 직접 비교하고, 뒤 화면의 uGUI 버튼은 전체 화면 막(raycast)이 막습니다.
// 문구/키 목록/크기는 ControlsUISettings(창을 열 때마다 목록을 다시 만듦), 창/버튼/글자 색은 PopupStyle.
public class ControlsUI : MonoBehaviour
{
    private static ControlsUI instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
    }

    public static bool IsOpen => instance != null && instance.isOpen;

    // 열려 있거나 방금 닫힌 프레임이면 true - 시작 화면/중단 화면 버튼이 같은 클릭/Esc에 반응하지 않도록.
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
            var go = new GameObject("ControlsUI");
            instance = go.AddComponent<ControlsUI>();
        }
        instance.minSortingOrder = minSortingOrder;
        instance.showDim = showDim;
        instance.Show();
    }

    private bool isOpen;
    private int minSortingOrder = int.MinValue;
    private bool showDim = true;
    private int openedFrame = -1;
    private int closedFrame = -1;
    private int lastHoveredButton = -1;

    // 스크롤(px, 0 = 맨 위)
    private float scroll;
    private float scrollTarget;
    private float contentHeight;
    private bool draggingThumb;
    private float dragGrabOffset; // 막대를 잡은 지점과 막대 위쪽 끝 사이 거리

    private GameObject canvasGO;
    private Canvas canvas;
    private CanvasScaler scaler;
    private Image dimImage;
    private RectTransform panelRect;
    private UIPatternBox panelBox;
    private TextMeshProUGUI titleText;
    private RectTransform dividerRect;
    private Image dividerImage;
    private RectTransform viewportRect;
    private RectTransform contentRect;
    private RectTransform trackRect;
    private Image trackImage;
    private RectTransform thumbRect;
    private Image thumbImage;
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
    }

    private void Show()
    {
        if (isOpen) return;
        isOpen = true;
        openedFrame = Time.frameCount;
        draggingThumb = false;
        scroll = scrollTarget = 0f; // 열 때마다 맨 위부터
        closeBox.ResetPress();
        canvasGO.SetActive(true);
        RebuildRows();
        ApplyLayout();
    }

    private void Close()
    {
        if (!isOpen) return;
        isOpen = false;
        closedFrame = Time.frameCount;
        draggingThumb = false;
        canvasGO.SetActive(false);
    }

    void Update()
    {
        if (!isOpen) return;

        var set = ControlsUISettings.Get();
        ApplyLayout();

        bool overClose = IsPointerOver(closeRect);
        bool clickClose = closeBox.TickButton(overClose && !draggingThumb, true);
        UISound.TrackHover(ref lastHoveredButton, UISound.HoveredId(0, overClose), Time.frameCount == openedFrame);

        if (Time.frameCount != openedFrame)
        {
            if (Input.GetKeyDown(KeyCode.Escape)) { UISound.Cancel(); Close(); return; }
            if (clickClose) { UISound.Cancel(); Close(); return; }
            UpdateScrollInput(set);
        }

        UpdateScrollMotion(set);
        ApplyScrollVisuals(set);
    }

    // ------------------------------------------------------------------
    // 스크롤
    // ------------------------------------------------------------------

    private float ViewHeight => viewportRect != null ? Mathf.Max(1f, viewportRect.rect.height) : 1f;
    private float MaxScroll => Mathf.Max(0f, contentHeight - ViewHeight);
    private bool CanScroll => MaxScroll > 0.5f;

    private void UpdateScrollInput(ControlsUISettings set)
    {
        if (!CanScroll)
        {
            draggingThumb = false;
            scroll = scrollTarget = 0f;
            return;
        }

        // 막대 끌기: 누르고 있는 동안 막대가 마우스를 따라감(즉시), 떼면 끝.
        if (draggingThumb)
        {
            if (Input.GetMouseButton(0))
            {
                scrollTarget = ScrollFromThumbTop(PointerYInTrackFromTop() - dragGrabOffset);
                scroll = scrollTarget;
            }
            else
            {
                draggingThumb = false;
            }
            return;
        }

        // 마우스 휠(창 위에 있을 때)
        float wheel = Input.mouseScrollDelta.y;
        if (Mathf.Abs(wheel) > 0.01f && IsPointerOver(panelRect))
        {
            scrollTarget = Mathf.Clamp(scrollTarget - wheel * set.wheelStep, 0f, MaxScroll);
        }

        // 막대 잡기 / 빈 막대 칸 누르면 그 자리로
        if (Input.GetMouseButtonDown(0))
        {
            if (IsPointerOver(thumbRect))
            {
                draggingThumb = true;
                dragGrabOffset = PointerYInTrackFromTop() - ThumbTop();
            }
            else if (IsPointerOver(trackRect))
            {
                draggingThumb = true;
                dragGrabOffset = ThumbHeight() * 0.5f;
                scrollTarget = ScrollFromThumbTop(PointerYInTrackFromTop() - dragGrabOffset);
                scroll = scrollTarget;
            }
        }
    }

    private void UpdateScrollMotion(ControlsUISettings set)
    {
        scrollTarget = Mathf.Clamp(scrollTarget, 0f, MaxScroll);
        float t = 1f - Mathf.Exp(-set.scrollSmoothing * Time.unscaledDeltaTime);
        scroll = Mathf.Lerp(scroll, scrollTarget, t);
        if (Mathf.Abs(scroll - scrollTarget) < 0.5f) scroll = scrollTarget;
        scroll = Mathf.Clamp(scroll, 0f, MaxScroll);
    }

    private float TrackHeight => trackRect != null ? Mathf.Max(1f, trackRect.rect.height) : 1f;

    private float ThumbHeight()
    {
        if (contentHeight <= 0f) return TrackHeight;
        return Mathf.Clamp(TrackHeight * ViewHeight / contentHeight, 40f, TrackHeight);
    }

    private float ThumbTop()
    {
        float room = TrackHeight - ThumbHeight();
        return MaxScroll > 0f ? room * (scroll / MaxScroll) : 0f;
    }

    private float ScrollFromThumbTop(float thumbTop)
    {
        float room = TrackHeight - ThumbHeight();
        if (room <= 0f) return 0f;
        return Mathf.Clamp01(thumbTop / room) * MaxScroll;
    }

    // 마우스의 세로 위치(막대 칸 위쪽 끝 기준, 아래로 +)
    private float PointerYInTrackFromTop()
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(trackRect, Input.mousePosition, null, out Vector2 local);
        return trackRect.rect.yMax - local.y;
    }

    private void ApplyScrollVisuals(ControlsUISettings set)
    {
        contentRect.anchoredPosition = new Vector2(0f, scroll);

        bool show = CanScroll;
        if (trackRect.gameObject.activeSelf != show) trackRect.gameObject.SetActive(show);
        if (!show) return;

        float w = Mathf.Max(2f, set.scrollbarWidth);
        Vector2 trackSize = trackRect.rect.size;
        UIRoundedBox.Apply(trackImage, set.scrollbarTrackColor, w * 0.5f, trackSize);

        float h = ThumbHeight();
        thumbRect.sizeDelta = new Vector2(w, h);
        thumbRect.anchoredPosition = new Vector2(0f, -ThumbTop());
        bool hot = draggingThumb || IsPointerOver(thumbRect);
        UIRoundedBox.Apply(thumbImage, hot ? set.scrollbarThumbHoverColor : set.scrollbarThumbColor, w * 0.5f, new Vector2(w, h));
    }

    private static bool IsPointerOver(RectTransform rect)
    {
        if (rect == null || !rect.gameObject.activeInHierarchy) return false;
        return RectTransformUtility.RectangleContainsScreenPoint(rect, Input.mousePosition, null);
    }

    // ------------------------------------------------------------------
    // UI 생성 / 배치
    // ------------------------------------------------------------------

    private void BuildUI()
    {
        canvasGO = new GameObject("ControlsCanvas");
        canvasGO.transform.SetParent(transform, false);
        canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasGO.AddComponent<GraphicRaycaster>(); // 뒤 화면 uGUI 버튼 클릭을 막는 막(dim)용

        var dimGO = new GameObject("Dim");
        dimGO.transform.SetParent(canvasGO.transform, false);
        Stretch(dimGO.AddComponent<RectTransform>());
        dimImage = dimGO.AddComponent<Image>();
        dimImage.raycastTarget = true;

        var panelGO = new GameObject("Panel");
        panelGO.transform.SetParent(canvasGO.transform, false);
        panelRect = panelGO.AddComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelBox = UIPatternBox.AddTo(panelGO);

        titleText = CreateText(panelRect, "Title", TextAnchor.MiddleCenter);
        dividerImage = CreateImage(panelRect, "Divider", out dividerRect);

        // 스크롤 영역: 창 폭 전체(줄 위치를 창 왼쪽 끝 기준으로 쓰기 위해), 위아래만 잘라 냄
        var viewportGO = new GameObject("Viewport");
        viewportGO.transform.SetParent(panelRect, false);
        viewportRect = viewportGO.AddComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportGO.AddComponent<RectMask2D>();

        var contentGO = new GameObject("Content");
        contentGO.transform.SetParent(viewportRect, false);
        contentRect = contentGO.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);

        // 스크롤 막대(칸 + 손잡이)
        trackImage = CreateImage(panelRect, "ScrollTrack", out trackRect);
        trackRect.anchorMin = new Vector2(1f, 0f);
        trackRect.anchorMax = new Vector2(1f, 1f);
        trackRect.pivot = new Vector2(1f, 0.5f);
        thumbImage = CreateImage(trackRect, "ScrollThumb", out thumbRect);
        thumbRect.anchorMin = thumbRect.anchorMax = new Vector2(0.5f, 1f);
        thumbRect.pivot = new Vector2(0.5f, 1f);

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

    // 창 위쪽 끝에서 스크롤 영역 위쪽 끝까지의 거리(제목 + 가는 줄 아래)
    private static float HeaderHeight(ControlsUISettings set) => 20f + set.titleFontSize + 16f + 22f;

    // 창 아래쪽 끝에서 스크롤 영역 아래쪽 끝까지의 거리([닫기] 위)
    private static float FooterHeight(ControlsUISettings set) => set.buttonBottomMargin + set.buttonSize.y + 20f;

    // 설정/스타일 값을 매 프레임 반영합니다(Play 중 에셋 값을 바꾸면 바로 보임 - 키 목록은 다시 열 때 반영).
    private void ApplyLayout()
    {
        var set = ControlsUISettings.Get();
        var style = PopupStyle.Get();

        canvas.sortingOrder = Mathf.Max(set.canvasSortingOrder, minSortingOrder);
        if (Screen.height > 0)
        {
            float screenAspect = (float)Screen.width / Screen.height;
            scaler.matchWidthOrHeight = screenAspect > 1920f / 1080f ? 1f : 0f; // HUD와 같은 규칙
        }
        dimImage.color = showDim ? set.dimColor : Color.clear;

        Vector2 size = new Vector2(Mathf.Max(300f, set.panelSize.x), Mathf.Max(300f, set.panelSize.y));
        panelRect.sizeDelta = size;
        panelRect.anchoredPosition = Vector2.zero;
        panelBox.SetupPanel(style);

        var titleRect = titleText.rectTransform;
        titleRect.anchorMin = titleRect.anchorMax = new Vector2(0.5f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.sizeDelta = new Vector2(size.x - 40f, set.titleFontSize + 16f);
        titleRect.anchoredPosition = new Vector2(0f, -20f);
        titleText.text = set.title;
        titleText.fontSize = set.titleFontSize;
        titleText.color = style.panelTextColor;

        dividerRect.anchorMin = dividerRect.anchorMax = new Vector2(0.5f, 1f);
        dividerRect.pivot = new Vector2(0.5f, 0.5f);
        dividerRect.sizeDelta = new Vector2(Mathf.Max(10f, size.x - 120f), 2f);
        dividerRect.anchoredPosition = new Vector2(0f, -(20f + set.titleFontSize + 16f + 8f));
        dividerImage.color = set.dividerColor;

        float header = HeaderHeight(set);
        float footer = FooterHeight(set);
        viewportRect.offsetMin = new Vector2(0f, footer);
        viewportRect.offsetMax = new Vector2(0f, -header);
        contentRect.sizeDelta = new Vector2(0f, contentHeight);

        trackRect.sizeDelta = new Vector2(Mathf.Max(2f, set.scrollbarWidth), -(header + footer + 16f));
        trackRect.anchoredPosition = new Vector2(-set.scrollbarRightMargin, (footer - header) * 0.5f);

        closeRect.sizeDelta = set.buttonSize;
        closeRect.anchoredPosition = new Vector2(0f, set.buttonBottomMargin);
        closeBox.SetupButton(style, UIButtonKind.Normal); // 색은 Update의 TickButton이 호버에 따라 정함
        closeText.text = set.closeLabel;
        closeText.fontSize = set.buttonFontSize;
        closeText.color = closeBox.GetLabelColor(true);
    }

    // 키 목록을 지우고 설정 에셋 내용으로 다시 만듭니다(창을 열 때마다). 줄 위치는 창 왼쪽 끝 / 목록 위쪽 끝 기준입니다.
    private void RebuildRows()
    {
        for (int i = contentRect.childCount - 1; i >= 0; i--)
        {
            var old = contentRect.GetChild(i).gameObject;
            old.SetActive(false); // Destroy는 프레임 끝에 지워지므로, 새 목록과 한 프레임 겹쳐 보이지 않게 먼저 숨김
            Destroy(old);
        }

        var set = ControlsUISettings.Get();
        var style = PopupStyle.Get();
        float panelWidth = Mathf.Max(300f, set.panelSize.x);
        float lineRight = panelWidth - set.sidePadding - 20f;

        float y = 0f;
        var sections = set.sections ?? new List<ControlsUISettings.Section>();
        for (int si = 0; si < sections.Count; si++)
        {
            var section = sections[si];
            if (section == null) continue;

            if (!string.IsNullOrEmpty(section.title))
            {
                float centerY = y + set.sectionHeaderHeight * 0.5f;
                var header = CreateText(contentRect, "Section_" + section.title, TextAnchor.MiddleLeft);
                header.text = section.title;
                header.fontSize = set.sectionFontSize;
                header.color = set.sectionColor;
                float textWidth = header.GetPreferredValues(section.title).x;
                PlaceTopLeft(header.rectTransform, new Vector2(0f, 0.5f), set.sidePadding + 10f, centerY,
                             new Vector2(textWidth + 4f, set.sectionHeaderHeight));

                float lineLeft = set.sidePadding + 10f + textWidth + 14f;
                if (lineRight > lineLeft)
                {
                    var line = CreateImage(contentRect, "SectionLine", out RectTransform lineRect);
                    line.color = set.sectionLineColor;
                    PlaceTopLeft(lineRect, new Vector2(0f, 0.5f), lineLeft, centerY, new Vector2(lineRight - lineLeft, 2f));
                }
                y += set.sectionHeaderHeight;
            }

            var rows = section.rows ?? new List<ControlsUISettings.Row>();
            for (int ri = 0; ri < rows.Count; ri++)
            {
                if (rows[ri] == null) continue;
                BuildRow(rows[ri], set, style, y + set.rowHeight * 0.5f);
                y += set.rowHeight;
            }

            if (si < sections.Count - 1) y += set.sectionSpacing;
        }

        contentHeight = y + 10f;
        contentRect.sizeDelta = new Vector2(0f, contentHeight);
    }

    private void BuildRow(ControlsUISettings.Row row, ControlsUISettings set, PopupStyle style, float centerY)
    {
        var label = CreateText(contentRect, "Action", TextAnchor.MiddleRight);
        label.text = row.action ?? string.Empty;
        label.fontSize = set.labelFontSize;
        label.color = style.panelTextColor;
        PlaceTopLeft(label.rectTransform, new Vector2(1f, 0.5f), set.labelColumnRight, centerY,
                     new Vector2(Mathf.Max(10f, set.labelColumnRight - set.sidePadding), set.rowHeight));

        float x = set.labelColumnRight + set.labelToKeyGap;
        var keys = row.keys ?? new List<string>();
        for (int k = 0; k < keys.Count; k++)
        {
            if (k > 0)
            {
                var sep = CreateText(contentRect, "Separator", TextAnchor.MiddleCenter);
                sep.text = set.keySeparator ?? string.Empty;
                sep.fontSize = set.labelFontSize;
                sep.color = style.panelTextColor;
                PlaceTopLeft(sep.rectTransform, new Vector2(0.5f, 0.5f), x + set.keyBadgeGap * 0.5f, centerY,
                             new Vector2(set.keyBadgeGap, set.rowHeight));
                x += set.keyBadgeGap;
            }
            x += BuildKeyBadge(keys[k] ?? string.Empty, set, style, x, centerY);
        }
    }

    // 남색 평행사변형 키 배지 하나를 left 위치에 만들고 폭을 돌려줍니다.
    private float BuildKeyBadge(string key, ControlsUISettings set, PopupStyle style, float left, float centerY)
    {
        var go = new GameObject("Key_" + key);
        go.transform.SetParent(contentRect, false);
        var rect = go.AddComponent<RectTransform>();
        var box = UIPatternBox.AddTo(go);
        box.SetupShape(style, set.keyBadgeColor, style.namePlateCornerRadius, style.buttonSlantAngle, false);

        var text = CreateText(rect, "Key", TextAnchor.MiddleCenter);
        text.text = key;
        text.fontSize = set.keyFontSize;
        text.color = set.keyTextColor;

        float height = Mathf.Max(10f, set.keyBadgeHeight);
        float slantExtra = height * Mathf.Tan(Mathf.Clamp(style.buttonSlantAngle, 0f, 45f) * Mathf.Deg2Rad);
        float inset = set.keyBadgePaddingX + slantExtra * 0.5f; // 기울어진 양끝에 글자가 닿지 않게
        float width = Mathf.Max(set.keyBadgeMinWidth, text.GetPreferredValues(key).x + inset * 2f);

        PlaceTopLeft(rect, new Vector2(0f, 0.5f), left, centerY, new Vector2(width, height));
        var textRect = text.rectTransform;
        Stretch(textRect);
        textRect.offsetMin = new Vector2(inset, 0f);
        textRect.offsetMax = new Vector2(-inset, 0f);
        return width;
    }

    // 목록 안 위치 지정: x = 창 왼쪽 끝 기준, centerY = 목록 위쪽 끝에서 아래로의 거리. pivot으로 x가 가리키는 지점을 정합니다.
    private static void PlaceTopLeft(RectTransform rect, Vector2 pivot, float x, float centerY, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = pivot;
        rect.sizeDelta = size;
        rect.anchoredPosition = new Vector2(x, -centerY);
    }
}
