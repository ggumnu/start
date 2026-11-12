using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// [아이템 도감 화면 - 시작 화면 (형빈 확정)]
// 시작 화면에서 [도감]을 누르면 화면이 불투명 흰 막으로 서서히 덮인 뒤, "아이템 도감" 제목과 연회색 둥근 패널(아이템 아이콘 목록),
// [돌아가기] 버튼이 ESC 중단 화면처럼 차례로 아래에서 떠오릅니다.
//
// [아이템 목록]
// - ItemCodexSettings에 연결한 ItemDatabase의 모든 아이템을 분류(패시브/액티브 등)와 상관없이 ID 숫자 순서로 놓습니다(한 줄 10개).
//   ID가 숫자가 아닌 아이템은 숫자 ID 뒤에 이름순으로 놓고, ID가 비어 있거나 같은 ID가 두 번 있으면 한 번만 놓습니다.
// - 한 번이라도 얻은 아이템(ItemCodexRecord)은 원래 색, 한 번도 얻지 않은 아이템은 반투명입니다.
// - 줄이 많아 패널을 넘치면 패널 위에서 마우스 휠로 스크롤합니다.
// - 얻은 아이템 아이콘에 커서를 올리면 인벤토리와 같은 아이템 설명칸이 커서 위에 뜹니다(화면 밖으로 나가지 않음).
//   안 얻은 아이템에는 뜨지 않습니다.
//
// [배경 무늬]
// 흰 막 위 왼쪽 위 / 오른쪽 아래 모서리에 연한 하늘색 삼각형 무늬가 중앙으로 갈수록 옅어지며 깔리고, 삼각형이 하나씩 은은하게 반짝입니다
// (UICodexCornerPattern 셰이더). 흰 막과 함께 나타나고 사라집니다. 값은 ItemCodexSettings의 "배경 무늬".
//
// [닫기]
// [돌아가기] 클릭 또는 ESC. 제목/패널/버튼이 곡선을 그리며 가라앉아 사라지고 흰 막이 걷힙니다. 다 떠오르기 전에 ESC를 눌러도
// 그 자리에서 바로 사라지기 시작합니다. [돌아가기] 클릭은 버튼이 다 떠오른 뒤에만 됩니다(ESC 중단 화면과 같은 규칙).
// 도감이 열려 있는 동안(사라지는 중 포함)에는 흰 막이 시작 화면 버튼 클릭을 막습니다.
//
// [설정]
// 씬에 붙일 필요가 없습니다 - 시작 화면(TitleScreenUI)이나 게임 중 ESC 중단 화면(PauseMenuUI)의 [도감]이 Open()을 불러서 스스로
// 만들어집니다(그 씬과 함께 사라짐). 게임 중에는 게임이 멈춘 상태로 열리고(연출은 실제 시간 기준), 중단 화면 위에 그려집니다.
// 문구/크기/색/연출 시간은 ItemCodexSettings 에셋, 버튼 색은 PopupStyle 에셋, 설명칸 모양은 HUD 프리팹의 PlayerItemInfoDisplay.
public class ItemCodexUI : MonoBehaviour
{
    private enum Mode { Closed, Opening, Ready, Closing }

    // ------------------------------------------------------------------
    // 외부에서 쓰는 정적 진입점
    // ------------------------------------------------------------------

    private static ItemCodexUI instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
    }

    // 도감이 떠 있는지(열리는 중/사라지는 중 포함)
    public static bool IsOpen => instance != null && instance.mode != Mode.Closed;

    // 시작 화면 버튼/키 입력을 막아야 하는지(떠 있거나, 방금 닫힌 그 프레임)
    public static bool BlocksInput =>
        instance != null && (instance.mode != Mode.Closed || instance.closedFrame == Time.frameCount);

    // 도감을 엽니다. 이미 떠 있으면 아무 일도 하지 않습니다.
    public static void Open()
    {
        Open(int.MinValue);
    }

    // minSortingOrder: 이 값보다 아래에 그려지지 않게 합니다(게임 중 ESC 중단 화면에서 열 때 중단 화면 위로 - PauseMenuUI).
    public static void Open(int minSortingOrder)
    {
        if (instance == null)
        {
            var go = new GameObject("ItemCodexUI");
            go.AddComponent<ItemCodexUI>();
        }
        if (instance != null && instance.mode == Mode.Closed)
        {
            instance.minSortingOrder = minSortingOrder;
            instance.BeginOpen();
        }
    }

    // ------------------------------------------------------------------
    // 인스턴스 상태
    // ------------------------------------------------------------------

    private Mode mode = Mode.Closed;
    private int minSortingOrder = int.MinValue;
    private float openTime;
    private float closeStartTime;
    private int modeFrame = -1;
    private int closedFrame = -1;

    // 사라지기 시작한 순간의 값(그 자리에서 사라지기 시작하도록)
    private float closeFromOverlay;
    private float closeFromContent;
    private float closeFromButton;
    private float closeFromContentOffset;
    private float closeFromButtonOffset;

    // 지금 화면 값
    private float overlayAlpha;
    private float contentAlpha;   // 제목 + 패널
    private float buttonAlpha;
    private float contentOffset;  // 세로 이동량(px, 음수 = 아래)
    private float buttonOffset;

    private readonly List<ItemDefinition> displayItems = new List<ItemDefinition>();
    private float scrollOffset;
    private bool recordDirty;

    // 설명칸 모양 원본(HUD 프리팹 안의 PlayerItemInfoDisplay)
    private GameObject styleSourcePrefab;
    private PlayerItemInfoDisplay styleSource;
    private GameObject fallbackStyleGO;

    // ---------------- UI ----------------
    private class IconView
    {
        public RectTransform rect;
        public Image image;
    }

    private GameObject canvasGO;
    private Canvas canvas;
    private RectTransform canvasRect;
    private CanvasScaler scaler;
    private Image overlayImage;
    private RawImage patternImage;
    private Material patternMaterial;
    private Shader patternShaderInUse;
    private bool patternWarned;

    private RectTransform titleRect;
    private TextMeshProUGUI titleText;
    private CanvasGroup titleGroup;

    private RectTransform panelRect;
    private Image panelImage;
    private CanvasGroup panelGroup;
    private RectTransform viewportRect;
    private RectTransform contentRect;
    private readonly List<IconView> iconViews = new List<IconView>();

    private RectTransform buttonRect;
    private UIPatternBox buttonBox;
    private TextMeshProUGUI buttonText;
    private CanvasGroup buttonGroup;

    private readonly ItemInfoPanelView tooltip = new ItemInfoPanelView();

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
        ItemCodexRecord.OnChanged += HandleRecordChanged;
    }

    void OnDestroy()
    {
        if (instance != this) return;
        ItemCodexRecord.OnChanged -= HandleRecordChanged;
        if (fallbackStyleGO != null) Destroy(fallbackStyleGO);
        if (patternMaterial != null) Destroy(patternMaterial);
        instance = null;
    }

    void Update()
    {
        if (mode == Mode.Closed) return;

        var s = ItemCodexSettings.Get();
        if (recordDirty)
        {
            recordDirty = false;
            RebuildItemList(s); // 열려 있는 동안 기록이 바뀌면(개발용 초기화) 바로 다시 그림
        }

        switch (mode)
        {
            case Mode.Opening: UpdateOpening(s); break;
            case Mode.Ready: UpdateReady(s); break;
            case Mode.Closing: UpdateClosing(s); break;
        }

        if (mode == Mode.Closed) return;

        UpdateScroll(s);
        ApplyVisuals(s);
        UpdateTooltip(s);
    }

    private void HandleRecordChanged()
    {
        recordDirty = true;
    }

    // ------------------------------------------------------------------
    // 열기
    // ------------------------------------------------------------------

    private void BeginOpen()
    {
        var s = ItemCodexSettings.Get();
        RebuildItemList(s);
        scrollOffset = 0f;

        mode = Mode.Opening;
        modeFrame = Time.frameCount;
        openTime = Time.unscaledTime;

        overlayAlpha = contentAlpha = buttonAlpha = 0f;
        contentOffset = buttonOffset = -s.riseDistance;
        SetButtonIdle(); // 떠오르는 동안은 평소 색

        tooltip.Hide();
        canvasGO.SetActive(true);
        ApplyVisuals(s);
    }

    private void UpdateOpening(ItemCodexSettings s)
    {
        float t = Time.unscaledTime - openTime;

        overlayAlpha = Progress(t, 0f, s.overlayFadeInDuration);
        float c = Progress(t, s.contentDelay, s.contentFadeInDuration);
        float b = Progress(t, s.buttonDelay, s.buttonFadeInDuration);
        contentAlpha = c;
        buttonAlpha = b;
        contentOffset = -s.riseDistance * (1f - c);
        buttonOffset = -s.riseDistance * (1f - b);

        // 다 떠오르기 전에 ESC를 누르면 그 자리에서 바로 사라지기 시작합니다(연 그 프레임의 입력은 제외).
        if (Time.frameCount != modeFrame && Input.GetKeyDown(s.closeKey))
        {
            BeginClose();
            return;
        }

        if (overlayAlpha >= 1f && c >= 1f && b >= 1f)
        {
            mode = Mode.Ready;
            modeFrame = Time.frameCount;
        }
    }

    // 0~1 진행도(부드럽게 - ESC 중단 화면과 같은 곡선)
    private static float Progress(float t, float delay, float duration)
    {
        float x = duration <= 0f ? (t >= delay ? 1f : 0f) : Mathf.Clamp01((t - delay) / duration);
        return x * x * (3f - 2f * x);
    }

    // ------------------------------------------------------------------
    // 입력 대기
    // ------------------------------------------------------------------

    private int lastHoveredButton = -1; // [사운드] 호버음용 - 마지막으로 마우스가 올라간 버튼 번호

    private void UpdateReady(ItemCodexSettings s)
    {
        bool overButton = IsPointerOver(buttonRect);
        bool clickButton = buttonBox.TickButton(overButton, true); // 클릭은 떼는 순간(누르는 동안 작아짐)
        UISound.TrackHover(ref lastHoveredButton, UISound.HoveredId(0, overButton), Time.frameCount == modeFrame);

        if (Time.frameCount == modeFrame) return;

        if (Input.GetKeyDown(s.closeKey)) { BeginClose(); return; }
        if (clickButton) BeginClose();
    }

    private void SetButtonIdle()
    {
        buttonBox.ResetPress();
        buttonBox.color = PopupStyle.Get().GetKindColor(UIButtonKind.Normal, false, true);
    }

    private void UpdateScroll(ItemCodexSettings s)
    {
        if (mode == Mode.Closing) return;
        if (!IsPointerOver(viewportRect)) return;
        float wheel = Input.mouseScrollDelta.y;
        if (Mathf.Abs(wheel) > 0.01f) scrollOffset -= wheel * s.scrollSpeed; // 범위 제한은 LayoutGrid에서
    }

    // ------------------------------------------------------------------
    // 닫기
    // ------------------------------------------------------------------

    private void BeginClose()
    {
        UISound.Cancel(); // [사운드] 닫기 버튼/Esc 모두 여기로 옴
        mode = Mode.Closing;
        modeFrame = Time.frameCount;
        closeStartTime = Time.unscaledTime;
        closeFromOverlay = overlayAlpha;
        closeFromContent = contentAlpha;
        closeFromButton = buttonAlpha;
        closeFromContentOffset = contentOffset;
        closeFromButtonOffset = buttonOffset;

        SetButtonIdle(); // 사라지는 동안 호버 표시 없음
        tooltip.Hide();
    }

    private void UpdateClosing(ItemCodexSettings s)
    {
        float t = Time.unscaledTime - closeStartTime;
        float p = Progress(t, 0f, s.closeDuration);

        overlayAlpha = closeFromOverlay * (1f - p);
        contentAlpha = closeFromContent * (1f - p);
        buttonAlpha = closeFromButton * (1f - p);
        // 가라앉기: 사라지기 시작한 자리에서 riseDistance만큼 아래로
        float sink = s.riseDistance * p;
        contentOffset = closeFromContentOffset - sink;
        buttonOffset = closeFromButtonOffset - sink;

        if (p >= 1f) FinishClose();
    }

    private void FinishClose()
    {
        mode = Mode.Closed;
        closedFrame = Time.frameCount;
        tooltip.Hide();
        canvasGO.SetActive(false);
    }

    // ------------------------------------------------------------------
    // 아이템 목록
    // ------------------------------------------------------------------

    private void RebuildItemList(ItemCodexSettings s)
    {
        displayItems.Clear();

        if (s.itemDatabase == null || s.itemDatabase.allItems == null)
        {
            Debug.LogWarning("ItemCodexUI: ItemCodexSettings에 Item Database가 연결되지 않아 도감에 아이템이 나오지 않습니다.");
        }
        else
        {
            var seen = new HashSet<string>();
            foreach (var item in s.itemDatabase.allItems)
            {
                if (item == null || string.IsNullOrEmpty(item.itemId)) continue;
                if (!seen.Add(item.itemId)) continue; // 같은 ID는 한 번만
                displayItems.Add(item);
            }
            displayItems.Sort(CompareById);
        }

        EnsureIconViews(displayItems.Count);
    }

    // ID 숫자 순서(1, 2, ..., 10). 숫자가 아닌 ID는 숫자 ID 뒤에 이름순.
    private static int CompareById(ItemDefinition a, ItemDefinition b)
    {
        bool aNum = int.TryParse(a.itemId, out int an);
        bool bNum = int.TryParse(b.itemId, out int bn);
        if (aNum && bNum) return an != bn ? an.CompareTo(bn) : string.CompareOrdinal(a.itemId, b.itemId);
        if (aNum) return -1;
        if (bNum) return 1;
        return string.CompareOrdinal(a.itemId, b.itemId);
    }

    private int GetHoveredIndex()
    {
        if (!IsPointerOver(viewportRect)) return -1; // 스크롤로 가려진 칸은 대상 아님
        for (int i = 0; i < displayItems.Count && i < iconViews.Count; i++)
        {
            if (IsPointerOver(iconViews[i].rect)) return i;
        }
        return -1;
    }

    private static bool IsPointerOver(RectTransform rect)
    {
        if (rect == null || !rect.gameObject.activeInHierarchy) return false;
        return RectTransformUtility.RectangleContainsScreenPoint(rect, Input.mousePosition, null);
    }

    // ------------------------------------------------------------------
    // 아이템 설명칸 (얻은 아이템만)
    // ------------------------------------------------------------------

    private void UpdateTooltip(ItemCodexSettings s)
    {
        if (mode != Mode.Ready)
        {
            tooltip.Hide();
            return;
        }

        int index = GetHoveredIndex();
        ItemDefinition item = index >= 0 ? displayItems[index] : null;
        if (item == null || !ItemCodexRecord.HasAcquired(item))
        {
            tooltip.Hide();
            return;
        }

        PlayerItemInfoDisplay source = GetStyleSource(s);
        if (source == null)
        {
            tooltip.Hide();
            return;
        }

        var style = source.GetStyle();
        tooltip.ShowItem(item, style);
        tooltip.Reflow(style);
        tooltip.BringToFront();
        PlaceTooltipAtCursor(s);
    }

    // HUD 프리팹 안의 PlayerItemInfoDisplay(값만 읽음). 없으면 기본값을 가진 임시 PlayerItemInfoDisplay(꺼진 오브젝트라 동작하지 않음).
    private PlayerItemInfoDisplay GetStyleSource(ItemCodexSettings s)
    {
        if (styleSource != null && styleSourcePrefab == s.hudPrefab) return styleSource;

        styleSourcePrefab = s.hudPrefab;
        styleSource = s.hudPrefab != null ? s.hudPrefab.GetComponentInChildren<PlayerItemInfoDisplay>(true) : null;
        if (styleSource != null) return styleSource;

        Debug.LogWarning(s.hudPrefab == null
            ? "ItemCodexUI: ItemCodexSettings의 Hud Prefab이 비어 있어 아이템 설명칸을 기본 모양으로 띄웁니다."
            : $"ItemCodexUI: '{s.hudPrefab.name}' 프리팹 안에서 PlayerItemInfoDisplay를 찾지 못해 아이템 설명칸을 기본 모양으로 띄웁니다.");

        if (fallbackStyleGO == null)
        {
            fallbackStyleGO = new GameObject("ItemCodexDefaultTooltipStyle");
            fallbackStyleGO.SetActive(false); // 꺼둔 채 붙여서 Awake/Update가 돌지 않게(값만 씀)
            fallbackStyleGO.transform.SetParent(transform, false);
            fallbackStyleGO.AddComponent<PlayerItemInfoDisplay>();
        }
        styleSource = fallbackStyleGO.GetComponent<PlayerItemInfoDisplay>();
        return styleSource;
    }

    // 커서 위쪽 중앙에 뜨되 화면 밖으로 나가지 않게(인벤토리 설명칸 ItemTooltipDisplay와 같은 방식)
    private void PlaceTooltipAtCursor(ItemCodexSettings s)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, Input.mousePosition, null, out Vector2 centerLocal);
        Rect r = canvasRect.rect;
        Vector2 cursorTopLeft = new Vector2(centerLocal.x - r.xMin, centerLocal.y - r.yMax);

        float panelW = tooltip.PanelRect.sizeDelta.x;
        float panelH = tooltip.PanelRect.sizeDelta.y;
        float margin = s.tooltipScreenEdgeMargin;

        float px = cursorTopLeft.x - panelW * 0.5f + s.tooltipCursorOffset.x;
        float py = cursorTopLeft.y + s.tooltipCursorOffset.y + panelH;

        float maxPx = Mathf.Max(margin, r.width - panelW - margin);
        px = Mathf.Clamp(px, margin, maxPx);
        float maxPy = -margin;
        float minPy = Mathf.Min(maxPy, panelH - r.height + margin);
        py = Mathf.Clamp(py, minPy, maxPy);

        tooltip.PanelRect.anchoredPosition = new Vector2(px, py);
    }

    // ------------------------------------------------------------------
    // UI 생성
    // ------------------------------------------------------------------

    private void BuildUI()
    {
        canvasGO = new GameObject("ItemCodexCanvas");
        canvasGO.transform.SetParent(transform, false);
        canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasGO.AddComponent<GraphicRaycaster>(); // 흰 막이 뒤의 시작 화면 버튼 클릭을 막도록
        canvasRect = canvasGO.GetComponent<RectTransform>();

        // 화면 전체 흰 막(클릭을 받아서 뒤의 버튼으로 가지 않게 함)
        var overlayGO = new GameObject("Overlay");
        overlayGO.transform.SetParent(canvasGO.transform, false);
        var overlayRect = overlayGO.AddComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;
        overlayImage = overlayGO.AddComponent<Image>();
        overlayImage.raycastTarget = true;

        // 배경 무늬(흰 막 바로 위, 제목/패널 아래) - 화면 전체를 덮고 셰이더가 모서리에만 무늬를 그림
        var patternGO = new GameObject("CornerPattern");
        patternGO.transform.SetParent(canvasGO.transform, false);
        var patternRect = patternGO.AddComponent<RectTransform>();
        patternRect.anchorMin = Vector2.zero;
        patternRect.anchorMax = Vector2.one;
        patternRect.offsetMin = Vector2.zero;
        patternRect.offsetMax = Vector2.zero;
        patternImage = patternGO.AddComponent<RawImage>();
        patternImage.raycastTarget = false;
        patternImage.enabled = false;

        // 제목
        var titleGO = new GameObject("Title");
        titleGO.transform.SetParent(canvasGO.transform, false);
        titleRect = titleGO.AddComponent<RectTransform>();
        titleRect.anchorMin = titleRect.anchorMax = new Vector2(0.5f, 0.5f);
        titleRect.pivot = new Vector2(0.5f, 0.5f);
        titleGroup = titleGO.AddComponent<CanvasGroup>();
        titleGroup.blocksRaycasts = false;
        titleGroup.interactable = false;
        titleText = HUDText.AddTo(titleGO, TextAnchor.MiddleCenter);
        titleText.raycastTarget = false;

        // 아이템 패널: 연회색 판 -> 보이는 영역(밖은 잘림) -> 내용(스크롤)
        var panelGO = new GameObject("ItemPanel");
        panelGO.transform.SetParent(canvasGO.transform, false);
        panelRect = panelGO.AddComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelImage = panelGO.AddComponent<Image>();
        panelImage.raycastTarget = false;
        panelGroup = panelGO.AddComponent<CanvasGroup>();
        panelGroup.blocksRaycasts = false;
        panelGroup.interactable = false;

        var viewportGO = new GameObject("Viewport");
        viewportGO.transform.SetParent(panelRect, false);
        viewportRect = viewportGO.AddComponent<RectTransform>();
        viewportRect.anchorMin = viewportRect.anchorMax = new Vector2(0f, 1f);
        viewportRect.pivot = new Vector2(0f, 1f);
        viewportGO.AddComponent<RectMask2D>();

        var contentGO = new GameObject("Content");
        contentGO.transform.SetParent(viewportRect, false);
        contentRect = contentGO.AddComponent<RectTransform>();
        contentRect.anchorMin = contentRect.anchorMax = new Vector2(0f, 1f);
        contentRect.pivot = new Vector2(0f, 1f);

        // [돌아가기] 버튼
        var buttonGO = new GameObject("BackButton");
        buttonGO.transform.SetParent(canvasGO.transform, false);
        buttonRect = buttonGO.AddComponent<RectTransform>();
        buttonRect.anchorMin = buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
        buttonRect.pivot = new Vector2(0.5f, 0.5f);
        buttonBox = UIPatternBox.AddTo(buttonGO);
        buttonGroup = buttonGO.AddComponent<CanvasGroup>();
        buttonGroup.blocksRaycasts = false;
        buttonGroup.interactable = false;

        var labelGO = new GameObject("Label");
        labelGO.transform.SetParent(buttonGO.transform, false);
        var labelRect = labelGO.AddComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        buttonText = HUDText.AddTo(labelGO, TextAnchor.MiddleCenter);
        buttonText.raycastTarget = false;

        // 아이템 설명칸(맨 위에 그려지도록 마지막 자식)
        tooltip.Build(canvasGO.transform, "ItemCodexTooltip");
        foreach (var graphic in tooltip.PanelRoot.GetComponentsInChildren<Graphic>(true))
        {
            graphic.raycastTarget = false;
        }
    }

    private void EnsureIconViews(int count)
    {
        while (iconViews.Count < count)
        {
            var go = new GameObject($"Icon_{iconViews.Count}");
            go.transform.SetParent(contentRect, false);
            var view = new IconView();
            view.rect = go.AddComponent<RectTransform>();
            view.rect.anchorMin = view.rect.anchorMax = new Vector2(0f, 1f);
            view.rect.pivot = new Vector2(0.5f, 0.5f);
            view.image = go.AddComponent<Image>();
            view.image.preserveAspect = true;
            view.image.raycastTarget = false;
            iconViews.Add(view);
        }
        for (int i = 0; i < iconViews.Count; i++)
        {
            iconViews[i].rect.gameObject.SetActive(i < count);
        }
    }

    // ------------------------------------------------------------------
    // 배치 (설정 값을 매 프레임 반영 - Play 중 에셋 값을 바꾸면 즉시 반영)
    // ------------------------------------------------------------------

    private void ApplyVisuals(ItemCodexSettings s)
    {
        var style = PopupStyle.Get();

        canvas.sortingOrder = Mathf.Max(s.canvasSortingOrder, minSortingOrder);
        if (Screen.height > 0)
        {
            float screenAspect = (float)Screen.width / Screen.height;
            scaler.matchWidthOrHeight = screenAspect > 1920f / 1080f ? 1f : 0f; // HUD와 같은 규칙
        }

        Color overlay = s.overlayColor;
        overlay.a *= overlayAlpha;
        overlayImage.color = overlay;
        ApplyPattern(s);

        // 제목
        titleRect.sizeDelta = new Vector2(1600f, s.titleFontSize * 2f + 20f);
        titleRect.anchoredPosition = new Vector2(0f, s.titleOffsetY + contentOffset);
        titleText.text = s.title;
        titleText.fontSize = s.titleFontSize;
        titleText.color = style.panelTextColor;
        titleGroup.alpha = contentAlpha;

        // 아이템 패널
        LayoutGrid(s);
        panelGroup.alpha = contentAlpha;

        // [돌아가기]
        buttonRect.sizeDelta = s.buttonSize;
        buttonRect.anchoredPosition = new Vector2(0f, s.buttonOffsetY + buttonOffset);
        buttonBox.SetupButton(style, UIButtonKind.Normal); // 색은 호버에 따라(UpdateReady의 TickButton)
        buttonText.text = s.backLabel;
        buttonText.fontSize = s.buttonFontSize;
        buttonText.color = buttonBox.GetLabelColor(true);
        buttonGroup.alpha = buttonAlpha;
    }

    // 배경 무늬: 설정 값을 셰이더에 넣습니다. 흰 막과 같은 투명도로 나타나고 사라집니다.
    private void ApplyPattern(ItemCodexSettings s)
    {
        Shader shader = s.patternShader != null ? s.patternShader : Shader.Find("MobArchive/UICodexCornerPattern");
        if (!s.showPattern || shader == null)
        {
            if (s.showPattern && shader == null && !patternWarned)
            {
                patternWarned = true; // 경고는 한 번만
                Debug.LogWarning("ItemCodexUI: 배경 무늬 셰이더(MobArchive/UICodexCornerPattern)를 찾지 못해 무늬 없이 흰 화면으로 표시합니다. " +
                                 "ItemCodexSettings의 Pattern Shader 칸에 UICodexCornerPattern.shader를 넣어주세요.");
            }
            patternImage.enabled = false;
            return;
        }

        if (patternMaterial == null || patternShaderInUse != shader)
        {
            if (patternMaterial != null) Destroy(patternMaterial);
            patternMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            patternShaderInUse = shader;
            patternImage.material = patternMaterial;
        }

        patternImage.enabled = true;
        Rect r = canvasRect.rect; // 1920x1080 기준 화면 크기(화면 비율에 따라 가로나 세로가 늘어남)
        patternMaterial.SetVector("_RectSize", new Vector4(r.width, r.height, 0f, 0f));
        patternMaterial.SetFloat("_TriSide", s.patternTriangleSide);
        patternMaterial.SetFloat("_Spread", s.patternSpread);
        patternMaterial.SetFloat("_MaxAlpha", s.patternMaxAlpha);
        patternMaterial.SetColor("_Tint", s.patternTint);
        patternMaterial.SetColor("_GlowColor", s.patternGlowColor);
        patternMaterial.SetFloat("_GlowAmount", s.patternGlowAmount);
        patternMaterial.SetFloat("_GlowSpeed", s.patternGlowSpeed);
        patternMaterial.SetFloat("_GlowSharpness", s.patternGlowSharpness);
        patternMaterial.SetFloat("_PatternTime", Time.unscaledTime);
        patternMaterial.SetFloat("_Seed", s.patternSeed);

        patternImage.color = new Color(1f, 1f, 1f, overlayAlpha);
    }

    private void LayoutGrid(ItemCodexSettings s)
    {
        int columns = Mathf.Max(1, s.columns);
        int rows = Mathf.Max(1, s.visibleRows);
        float iconSize = s.iconSize;
        float gridWidth = columns * iconSize + (columns - 1) * s.iconSpacingX;
        float viewHeight = rows * iconSize + (rows - 1) * s.iconSpacingY;
        float pad = s.panelPadding;

        Vector2 panelSize = new Vector2(pad * 2f + gridWidth, pad * 2f + viewHeight);
        panelRect.sizeDelta = panelSize;
        panelRect.anchoredPosition = new Vector2(0f, s.panelOffsetY + contentOffset);
        UIRoundedBox.Apply(panelImage, s.panelColor, s.panelCornerRadius, panelSize);

        viewportRect.sizeDelta = new Vector2(gridWidth, viewHeight);
        viewportRect.anchoredPosition = new Vector2(pad, -pad);

        int totalRows = Mathf.CeilToInt(displayItems.Count / (float)columns);
        float contentHeight = totalRows > 0 ? totalRows * iconSize + (totalRows - 1) * s.iconSpacingY : 0f;
        float maxScroll = Mathf.Max(0f, contentHeight - viewHeight);
        scrollOffset = Mathf.Clamp(scrollOffset, 0f, maxScroll);
        contentRect.sizeDelta = new Vector2(gridWidth, contentHeight);
        contentRect.anchoredPosition = new Vector2(0f, scrollOffset);

        for (int i = 0; i < displayItems.Count && i < iconViews.Count; i++)
        {
            var view = iconViews[i];
            ItemDefinition item = displayItems[i];
            int col = i % columns;
            int row = i / columns;
            view.rect.sizeDelta = new Vector2(iconSize, iconSize);
            view.rect.anchoredPosition = new Vector2(col * (iconSize + s.iconSpacingX) + iconSize * 0.5f,
                                                     -(row * (iconSize + s.iconSpacingY) + iconSize * 0.5f));

            Color color;
            if (item.icon != null)
            {
                if (view.image.sprite != item.icon) view.image.sprite = item.icon;
                color = Color.white;
            }
            else
            {
                view.image.sprite = null;
                color = s.missingIconColor;
            }

            if (!ItemCodexRecord.HasAcquired(item)) color.a *= s.unacquiredAlpha; // 한 번도 얻지 않은 아이템은 반투명
            view.image.color = color;
        }
    }
}
