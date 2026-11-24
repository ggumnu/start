using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// [설정 창 - HUD 탭] (SettingsUI가 만들고 HUD 탭일 때 매 프레임 Layout/Tick을 부름)
// 위에서 아래로(형빈 목업):
//   (대항목 제목 = 남색 평행사변형 판 위 흰 글자 - PopupStyle의 이름 판 값, 형빈 확정)
//   1) 스탯 보기: 스탯 창에 보일 스탯을 체크로 고름(PlayerHUD 프리팹 스탯 창에서 체크된 것만 목록에 나옴 - 목록 순서 그대로)
//   (창 높이는 고정이고 내용이 길면 세로 스크롤 - 마우스 휠 또는 오른쪽 스크롤 막대. 제목/[닫기]/탭은 고정 - 형빈 확정)
//   2) 스탯 불투명도: 창 불투명도 / 숫자를 흰색으로 [전투 화면][비전투 화면] / 아이콘·숫자 불투명도 | 전투 중 자동 반투명 + [배경 창만/전체] / 전투 중 불투명도
//   2-2) 인벤토리 불투명도: 창 / 슬롯 보조틀 / 슬롯 아이콘 | 패시브 아이콘 / 전투 중 자동 반투명 + [배경 창만/전체] / 전투 중 불투명도
//   3) 견본: 전투 화면 / 비전투 화면 - 배경 그림 위에 실제 HUD와 같은 코드(StatPanelView/SlotPanelView/InventoryPanelView)로 그린
//      견본 스탯 창(왼쪽, 고정 예시 숫자) + 슬롯/패시브 인벤토리(오른쪽 위/아래, 고정 예시 아이콘 - SettingsUISettings의 견본 아이콘 칸).
//      실제 화면에선 떨어져 있지만 견본이라 둘을 바짝 붙여 같은 배율로 칸에 꽉 차게 크게 보여줍니다(형빈 확정).
// 값은 전부 HUDUserSettings에 바로 들어가고(실제 HUD도 그 프레임에 바뀜), 창을 닫을 때 디스크에 기록됩니다(SettingsUI.Close).
// 전투 중 불투명도는 상한(HUDUserSettings.CombatOpacityCap)에서 손잡이가 멈추고, 상한 위치를 눈금으로 보여줍니다.
// 마우스 판정은 다른 팝업처럼 좌표를 직접 비교합니다(EventSystem 불필요). 글자/크기/문구는 SettingsUISettings.
public class SettingsHUDTab
{
    // ------------------------------------------------------------------
    // 부품
    // ------------------------------------------------------------------

    private class CheckBoxView
    {
        public RectTransform rect;
        public UIPatternBox border;
        public RectTransform innerRect;
        public UIPatternBox inner;
        public RectTransform checkRect;
        public Image check;
    }

    private class StatCellView
    {
        public PlayerStatDisplay.StatEntry entry;
        public RectTransform rect;
        public CheckBoxView box;
        public RectTransform iconRect;
        public Image icon;
        public TextMeshProUGUI name;
    }

    private class SliderView
    {
        public RectTransform row;
        public TextMeshProUGUI label;
        public RectTransform track;
        public Image trackImage;
        public RectTransform fill;
        public Image fillImage;
        public RectTransform capMark;
        public Image capMarkImage;
        public RectTransform knob;
        public UIPatternBox knobBox;
        public TextMeshProUGUI valueText;
    }

    private class ModeButtonView
    {
        public RectTransform rect;
        public UIPatternBox box;
        public TextMeshProUGUI label;
    }

    private class PreviewView
    {
        public TextMeshProUGUI title;
        public RectTransform boxRect;
        public Image maskImage;
        public RectTransform bgRect;
        public Image bgImage;
        public StatPanelView panel;
        public readonly List<PreviewRow> rows = new List<PreviewRow>();
        public string signature = "";
        public CollapsibleHUDPanel button;

        // 인벤토리 견본
        public SlotPanelView slotPanel;
        public CollapsibleHUDPanel slotButton;
        public InventoryPanelView passivePanel;
        public CollapsibleHUDPanel passiveButton;
    }

    // 견본 줄 = 공용 줄 구조 + 원본 항목
    private class PreviewRow : StatPanelRow
    {
        public PlayerStatDisplay.StatEntry entry;
    }

    private enum Drag { None, Scroll, Window, Content, Combat, InvWindow, SubFrame, SlotIcon, PassiveIcon, InvCombat }

    private readonly RectTransform root;
    private readonly RectTransform viewport;
    private readonly RectTransform content;
    private readonly RectTransform scrollTrack;
    private readonly Image scrollTrackImage;
    private readonly RectTransform scrollThumb;
    private readonly Image scrollThumbImage;
    private readonly RectTransform divider2;
    private readonly Image divider2Image;
    private float scrollY;          // 0 = 맨 위
    private float maxScroll;
    private float scrollGrabOffset; // 스크롤 막대를 잡은 위치(손잡이 위쪽 끝 기준)

    // 대항목 제목 판(남색 평행사변형 + 흰 글자)
    private class SectionHeaderView
    {
        public RectTransform rect;
        public UIPatternBox box;
        public TextMeshProUGUI label;
    }

    private SectionHeaderView statsLabel;
    private readonly List<StatCellView> cells = new List<StatCellView>();
    private string cellsSignature = null;
    private RectTransform divider;
    private Image dividerImage;
    private SectionHeaderView opacityLabel;

    private SliderView windowSlider;
    private SliderView contentSlider;
    private SliderView combatSlider;
    private TextMeshProUGUI whiteTextLabel;
    private RectTransform whiteCombatHit;
    private CheckBoxView whiteCombatBox;
    private TextMeshProUGUI whiteCombatLabel;
    private RectTransform whiteNormalHit;
    private CheckBoxView whiteNormalBox;
    private TextMeshProUGUI whiteNormalLabel;
    private CheckBoxView combatBox;
    private TextMeshProUGUI combatLabel;
    private RectTransform combatHit;
    private ModeButtonView modeBackground;
    private ModeButtonView modeAll;

    // 인벤토리 불투명도
    private SectionHeaderView inventoryLabel;
    private SliderView invWindowSlider;
    private SliderView subFrameSlider;
    private SliderView slotIconSlider;
    private SliderView passiveIconSlider;
    private SliderView invCombatSlider;
    private CheckBoxView invCombatBox;
    private TextMeshProUGUI invCombatLabel;
    private RectTransform invCombatHit;
    private ModeButtonView invModeBackground;
    private ModeButtonView invModeAll;

    private PreviewView combatPreview;
    private PreviewView normalPreview;

    private Drag dragging = Drag.None;
    private PlayerStatDisplay sourceCache;
    private PlayerSlotItemDisplay slotSourceCache;
    private PlayerInventoryDisplay passiveSourceCache;

    public bool IsDragging => dragging != Drag.None;

    // ------------------------------------------------------------------
    // 만들기
    // ------------------------------------------------------------------

    public SettingsHUDTab(RectTransform panel)
    {
        var go = new GameObject("HUDTab");
        go.transform.SetParent(panel, false);
        root = go.AddComponent<RectTransform>();
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;

        // 스크롤: 보이는 영역(viewport, 넘치면 잘림) 안의 content를 위아래로 옮깁니다. 흰 창 셰이더도 잘림(clip rect)을 지원합니다.
        viewport = CreateRect(root, "Viewport");
        viewport.gameObject.AddComponent<RectMask2D>();
        content = CreateRect(viewport, "Content");
        scrollTrackImage = CreateImage(root, "ScrollTrack", out scrollTrack);
        scrollThumbImage = CreateImage(scrollTrack, "ScrollThumb", out scrollThumb);
        divider2Image = CreateImage(content, "Divider2", out divider2);

        statsLabel = CreateSectionHeader(content, "StatViewHeader");
        dividerImage = CreateImage(content, "Divider", out divider);
        opacityLabel = CreateSectionHeader(content, "StatOpacityHeader");

        windowSlider = CreateSlider(content, "WindowOpacity");
        contentSlider = CreateSlider(content, "ContentOpacity");
        combatSlider = CreateSlider(content, "CombatOpacity");

        whiteTextLabel = CreateText(content, "WhiteTextLabel", TextAnchor.MiddleLeft);
        whiteCombatHit = CreateRect(content, "WhiteTextCombat");
        whiteCombatBox = CreateCheckBox(whiteCombatHit, "Check");
        whiteCombatLabel = CreateText(whiteCombatHit, "Label", TextAnchor.MiddleLeft);
        whiteNormalHit = CreateRect(content, "WhiteTextNormal");
        whiteNormalBox = CreateCheckBox(whiteNormalHit, "Check");
        whiteNormalLabel = CreateText(whiteNormalHit, "Label", TextAnchor.MiddleLeft);

        combatHit = CreateRect(content, "CombatFadeRow");
        combatBox = CreateCheckBox(combatHit, "Check");
        combatLabel = CreateText(combatHit, "Label", TextAnchor.MiddleLeft);
        modeBackground = CreateModeButton(content, "ModeBackground");
        modeAll = CreateModeButton(content, "ModeAll");

        inventoryLabel = CreateSectionHeader(content, "InventoryOpacityHeader");
        invWindowSlider = CreateSlider(content, "InvWindowOpacity");
        subFrameSlider = CreateSlider(content, "SlotSubFrameOpacity");
        slotIconSlider = CreateSlider(content, "SlotIconOpacity");
        passiveIconSlider = CreateSlider(content, "PassiveIconOpacity");
        invCombatSlider = CreateSlider(content, "InvCombatOpacity");
        invCombatHit = CreateRect(content, "InvCombatFadeRow");
        invCombatBox = CreateCheckBox(invCombatHit, "Check");
        invCombatLabel = CreateText(invCombatHit, "Label", TextAnchor.MiddleLeft);
        invModeBackground = CreateModeButton(content, "InvModeBackground");
        invModeAll = CreateModeButton(content, "InvModeAll");

        combatPreview = CreatePreview(content, "CombatPreview");
        normalPreview = CreatePreview(content, "NormalPreview");
    }

    public void SetVisible(bool visible)
    {
        if (root.gameObject.activeSelf != visible) root.gameObject.SetActive(visible);
        if (!visible) dragging = Drag.None;
    }

    public void CancelDrag()
    {
        dragging = Drag.None;
    }

    // 탭을 열 때 맨 위부터 보이도록
    public void ResetScroll()
    {
        scrollY = 0f;
    }

    public void Dispose()
    {
        foreach (var p in new[] { combatPreview, normalPreview })
        {
            if (p.button != null) p.button.Dispose();
            if (p.slotButton != null) p.slotButton.Dispose();
            if (p.passiveButton != null) p.passiveButton.Dispose();
        }
    }

    // ------------------------------------------------------------------
    // 견본/목록의 원본 = 실제 스탯 창 설정 (PlayerHUD 프리팹 -> 없으면 씬의 스탯 창)
    // ------------------------------------------------------------------

    private PlayerStatDisplay GetSource(SettingsUISettings set)
    {
        if (sourceCache != null) return sourceCache;
        if (set.playerHUDPrefab != null) sourceCache = set.playerHUDPrefab.GetComponentInChildren<PlayerStatDisplay>(true);
        if (sourceCache == null) sourceCache = Object.FindFirstObjectByType<PlayerStatDisplay>();
        return sourceCache;
    }

    private PlayerSlotItemDisplay GetSlotSource(SettingsUISettings set)
    {
        if (slotSourceCache != null) return slotSourceCache;
        if (set.playerHUDPrefab != null) slotSourceCache = set.playerHUDPrefab.GetComponentInChildren<PlayerSlotItemDisplay>(true);
        if (slotSourceCache == null) slotSourceCache = Object.FindFirstObjectByType<PlayerSlotItemDisplay>();
        return slotSourceCache;
    }

    private PlayerInventoryDisplay GetPassiveSource(SettingsUISettings set)
    {
        if (passiveSourceCache != null) return passiveSourceCache;
        if (set.playerHUDPrefab != null) passiveSourceCache = set.playerHUDPrefab.GetComponentInChildren<PlayerInventoryDisplay>(true);
        if (passiveSourceCache == null) passiveSourceCache = Object.FindFirstObjectByType<PlayerInventoryDisplay>();
        return passiveSourceCache;
    }

    // ------------------------------------------------------------------
    // 세로 배치 (스크롤 내용 기준 - 창 위쪽 끝에서의 거리, 스크롤 0일 때)
    // ------------------------------------------------------------------

    private static float GridTop() => 85f + 24f;
    private static float DividerY(SettingsUISettings set, int gridRows) => GridTop() + gridRows * set.hudStatRowHeight + 14f;
    private static float StatOpacityLabelY(SettingsUISettings set, int gridRows) => DividerY(set, gridRows) + 30f;
    private static float StatRow0(SettingsUISettings set, int gridRows) => StatOpacityLabelY(set, gridRows) + 46f;
    // 스탯/인벤토리 불투명도 사이 구분선과 간격(형빈 확정 - 넉넉하게)
    private static float Divider2Y(SettingsUISettings set, int gridRows) => StatRow0(set, gridRows) + 2f * set.hudControlRowSpacing + set.hudSectionGap * 0.5f;
    private static float InventoryLabelY(SettingsUISettings set, int gridRows) => Divider2Y(set, gridRows) + set.hudSectionGap * 0.5f + 16f;
    private static float InventoryRow0(SettingsUISettings set, int gridRows) => InventoryLabelY(set, gridRows) + 46f;
    private static float PreviewTitleY(SettingsUISettings set, int gridRows) => InventoryRow0(set, gridRows) + 2f * set.hudControlRowSpacing + 56f;
    private static float PreviewTop(SettingsUISettings set, int gridRows) => PreviewTitleY(set, gridRows) + 26f;

    // ------------------------------------------------------------------
    // 매 프레임 - 배치(설정 값 반영)
    // ------------------------------------------------------------------

    public void Layout(Vector2 panelSize)
    {
        var set = SettingsUISettings.Get();
        var style = PopupStyle.Get();
        var source = GetSource(set);
        Color navy = style.panelTextColor;

        float W = panelSize.x;
        float H = panelSize.y;
        float M = set.hudSideMargin;

        // 보이는 영역 = 제목 아래 ~ [닫기] 위. 스크롤 막대는 창 오른쪽 안쪽.
        float viewTop = set.hudScrollTop;
        float viewBottom = H - set.buttonBottomMargin - set.buttonSize.y - 14f;
        float viewH = Mathf.Max(50f, viewBottom - viewTop);
        Place(viewport, 0f, viewTop, W, viewH);
        content.anchorMin = content.anchorMax = new Vector2(0f, 1f);
        content.pivot = new Vector2(0f, 1f);

        // 1) 보이는 스탯
        float y = 85f;
        PlaceSectionHeader(statsLabel, set.statViewLabel, M, y, set, style);

        RebuildCellsIfNeeded(source);
        int cols = Mathf.Max(1, set.hudStatColumns);
        float cellW = (W - 2f * M) / cols;
        float rowH = set.hudStatRowHeight;
        float gridTop = GridTop();
        for (int i = 0; i < cells.Count; i++)
        {
            var c = cells[i];
            int col = i % cols;
            int r = i / cols;
            Place(c.rect, M + col * cellW, gridTop + r * rowH, cellW, rowH);

            float cb = set.hudCheckboxSize;
            Place(c.box.rect, 0f, (rowH - cb) * 0.5f, cb, cb);
            bool on = !HUDUserSettings.IsStatHidden(c.entry.statType);
            ApplyCheckBox(c.box, on, true, style);

            float iconSz = set.hudStatIconSize;
            Place(c.iconRect, cb + 8f, (rowH - iconSz) * 0.5f, iconSz, iconSz);
            c.icon.sprite = c.entry.icon;
            c.icon.color = c.entry.icon != null ? Color.white : source != null ? source.fallbackIconColor : Color.gray;

            float nameX = cb + 8f + iconSz + 8f;
            Place(c.name.rectTransform, nameX, 0f, Mathf.Max(10f, cellW - nameX - 4f), rowH);
            c.name.text = DisplayName(c.entry);
            c.name.fontSize = set.hudStatNameFontSize;
            c.name.enableAutoSizing = false;
            c.name.color = navy;
        }
        int gridRows = (cells.Count + cols - 1) / cols;

        float dividerY = DividerY(set, gridRows);
        Place(divider, 40f, dividerY - 1f, W - 80f, 2f);
        dividerImage.color = set.sliderTrackColor;
        float divider2Y = Divider2Y(set, gridRows);
        Place(divider2, 40f, divider2Y - 1f, W - 80f, 2f);
        divider2Image.color = set.sliderTrackColor;

        // 2) 불투명도
        float opacityY = StatOpacityLabelY(set, gridRows);
        PlaceSectionHeader(opacityLabel, set.statOpacitySectionLabel, M, opacityY, set, style);

        float colW = (W - 2f * M) / 2f;
        float leftX = M;
        float rightX = M + colW + 30f;
        float controlW = colW - 30f;
        float r0 = StatRow0(set, gridRows);
        float rs = set.hudControlRowSpacing;

        LayoutSlider(windowSlider, set, style, set.windowOpacityLabel, HUDUserSettings.WindowOpacity, true, -1f, leftX, r0, controlW);

        // [숫자를 흰색으로] 이름 + 오른쪽에 [전투 화면][비전투 화면] 체크 두 개(형빈 확정)
        float whiteY = r0 + rs;
        PlaceText(whiteTextLabel, set.whiteTextLabel, leftX, whiteY, set.hudSliderLabelWidth, set.hudLabelFontSize, navy);
        float checkX = leftX + Mathf.Max(set.hudSliderLabelWidth, whiteTextLabel.GetPreferredValues(set.whiteTextLabel).x + 16f);
        float checkW = (leftX + controlW - checkX - 16f) * 0.5f;
        LayoutCheckRow(whiteCombatHit, whiteCombatBox, whiteCombatLabel, set, style, set.combatPreviewLabel,
                       HUDUserSettings.WhiteTextCombat, true, checkX, whiteY, checkW);
        LayoutCheckRow(whiteNormalHit, whiteNormalBox, whiteNormalLabel, set, style, set.normalPreviewLabel,
                       HUDUserSettings.WhiteTextNormal, true, checkX + checkW + 16f, whiteY, checkW);

        LayoutSlider(contentSlider, set, style, set.contentOpacityLabel, HUDUserSettings.ContentOpacity, true, -1f, leftX, r0 + 2f * rs, controlW);

        bool combatOn = HUDUserSettings.CombatFade;
        LayoutCheckRow(combatHit, combatBox, combatLabel, set, style, set.combatFadeLabel, combatOn, true, rightX, r0,
                       controlW - set.hudModeBackgroundButtonSize.x - set.hudModeAllButtonSize.x - 16f);

        Vector2 allSize = set.hudModeAllButtonSize;
        Vector2 bgSize = set.hudModeBackgroundButtonSize;
        float allX = rightX + controlW - allSize.x;
        float bgX = allX - 8f - bgSize.x;
        bool modeAllSelected = HUDUserSettings.CombatMode == HUDCombatFadeMode.All;
        LayoutModeButton(modeBackground, set, style, set.combatModeBackgroundLabel, !modeAllSelected, combatOn, bgX, r0 - bgSize.y * 0.5f, bgSize);
        LayoutModeButton(modeAll, set, style, set.combatModeAllLabel, modeAllSelected, combatOn, allX, r0 - allSize.y * 0.5f, allSize);

        float cap = HUDUserSettings.CombatOpacityCap;
        LayoutSlider(combatSlider, set, style, set.combatOpacityLabel, HUDUserSettings.CombatOpacity, combatOn,
                     cap < 0.999f ? cap : -1f, rightX, r0 + rs, controlW);

        // 2-2) 인벤토리 불투명도
        float invLabelY = InventoryLabelY(set, gridRows);
        PlaceSectionHeader(inventoryLabel, set.inventoryOpacitySectionLabel, M, invLabelY, set, style);
        float i0 = InventoryRow0(set, gridRows);
        LayoutSlider(invWindowSlider, set, style, set.inventoryWindowOpacityLabel, HUDUserSettings.InventoryWindowOpacity, true, -1f, leftX, i0, controlW);
        LayoutSlider(subFrameSlider, set, style, set.slotSubFrameOpacityLabel, HUDUserSettings.SlotSubFrameOpacity, true, -1f, leftX, i0 + rs, controlW);
        LayoutSlider(slotIconSlider, set, style, set.slotIconOpacityLabel, HUDUserSettings.SlotIconOpacity, true, -1f, leftX, i0 + 2f * rs, controlW);

        LayoutSlider(passiveIconSlider, set, style, set.passiveIconOpacityLabel, HUDUserSettings.PassiveIconOpacity, true, -1f, rightX, i0, controlW);
        bool invCombatOn = HUDUserSettings.InventoryCombatFade;
        LayoutCheckRow(invCombatHit, invCombatBox, invCombatLabel, set, style, set.combatFadeLabel, invCombatOn, true, rightX, i0 + rs,
                       controlW - set.hudModeBackgroundButtonSize.x - set.hudModeAllButtonSize.x - 16f);
        bool invAllSelected = HUDUserSettings.InventoryCombatMode == HUDCombatFadeMode.All;
        LayoutModeButton(invModeBackground, set, style, set.combatModeBackgroundLabel, !invAllSelected, invCombatOn,
                         bgX, i0 + rs - bgSize.y * 0.5f, bgSize);
        LayoutModeButton(invModeAll, set, style, set.combatModeAllLabel, invAllSelected, invCombatOn, allX, i0 + rs - allSize.y * 0.5f, allSize);
        float invCap = HUDUserSettings.InventoryCombatOpacityCap;
        LayoutSlider(invCombatSlider, set, style, set.combatOpacityLabel, HUDUserSettings.InventoryCombatOpacity, invCombatOn,
                     invCap < 0.999f ? invCap : -1f, rightX, i0 + 2f * rs, controlW);

        // 3) 견본
        float previewTitleY = PreviewTitleY(set, gridRows);
        float boxTop = previewTitleY + 26f;
        float boxH = Mathf.Max(120f, set.previewBoxHeight);
        float boxW = (W - 2f * M - 40f) / 2f;

        string combatTitle = set.combatPreviewLabel + (combatOn || invCombatOn ? "" : set.previewOffSuffix);
        HUDUserSettings.GetOpacities(true, out float cWin, out float cCon);
        HUDUserSettings.GetOpacities(false, out float nWin, out float nCon);
        var slotSource = GetSlotSource(set);
        var passiveSource = GetPassiveSource(set);
        LayoutPreview(combatPreview, set, style, source, combatTitle,
                      set.combatPreviewBackground != null ? set.combatPreviewBackground : set.normalPreviewBackground,
                      cWin, cCon, HUDUserSettings.GetWhiteText(true) ? 1f : 0f, M, previewTitleY, boxTop, boxW, boxH);
        LayoutInventoryPreview(combatPreview, set, slotSource, passiveSource, true);
        ArrangePreview(combatPreview, set, source, slotSource, passiveSource, boxW, boxH);
        LayoutPreview(normalPreview, set, style, source, set.normalPreviewLabel, set.normalPreviewBackground,
                      nWin, nCon, HUDUserSettings.GetWhiteText(false) ? 1f : 0f, M + boxW + 40f, previewTitleY, boxTop, boxW, boxH);
        LayoutInventoryPreview(normalPreview, set, slotSource, passiveSource, false);
        ArrangePreview(normalPreview, set, source, slotSource, passiveSource, boxW, boxH);

        // 스크롤 범위/위치/막대
        float contentH = boxTop + boxH + 24f;
        content.sizeDelta = new Vector2(W, contentH);
        maxScroll = Mathf.Max(0f, contentH - viewBottom);
        scrollY = Mathf.Clamp(scrollY, 0f, maxScroll);
        content.anchoredPosition = new Vector2(0f, viewTop + scrollY); // 내용 좌표 = 창 위쪽 끝 기준(위 GridTop 등)

        bool canScroll = maxScroll > 0.5f;
        if (scrollTrack.gameObject.activeSelf != canScroll) scrollTrack.gameObject.SetActive(canScroll);
        if (canScroll)
        {
            float trackW = set.hudScrollBarWidth;
            Place(scrollTrack, W - trackW - 10f, viewTop + 6f, trackW, viewH - 12f);
            UIRoundedBox.Apply(scrollTrackImage, set.sliderTrackColor, trackW * 0.5f, scrollTrack.sizeDelta);
            float trackH = scrollTrack.sizeDelta.y;
            float visibleRatio = Mathf.Clamp01((viewBottom - viewTop) / Mathf.Max(1f, contentH - viewTop));
            float thumbH = Mathf.Max(40f, trackH * visibleRatio);
            float thumbY = (trackH - thumbH) * (maxScroll > 0f ? scrollY / maxScroll : 0f);
            Place(scrollThumb, 0f, thumbY, trackW, thumbH);
            Color thumbColor = dragging == Drag.Scroll ? style.panelTextColor : set.fillColor;
            UIRoundedBox.Apply(scrollThumbImage, thumbColor, trackW * 0.5f, scrollThumb.sizeDelta);
        }
    }

    // ------------------------------------------------------------------
    // 매 프레임 - 입력 (SettingsUI가 열린 직후 프레임/닫기 처리 뒤에 부름)
    // ------------------------------------------------------------------

    public void Tick()
    {
        // 끌고 있는 슬라이더/스크롤 막대: 버튼을 누르고 있는 동안 값 갱신, 떼면 끝.
        if (dragging != Drag.None)
        {
            if (!Input.GetMouseButton(0))
            {
                dragging = Drag.None;
                return;
            }
            if (dragging == Drag.Scroll) DragScroll(); else SetDragValue();
            return;
        }

        // 마우스 휠 = 위아래 스크롤(창 위에 있을 때)
        float wheel = Input.mouseScrollDelta.y;
        if (wheel != 0f && maxScroll > 0f && IsPointerOver(root))
        {
            scrollY = Mathf.Clamp(scrollY - wheel * SettingsUISettings.Get().hudScrollSpeed, 0f, maxScroll);
        }

        if (!Input.GetMouseButtonDown(0)) return;

        // 스크롤 막대: 손잡이를 잡으면 그 위치 그대로, 빈 곳을 누르면 손잡이 가운데를 그 위치로 옮겨 끌기 시작
        if (IsPointerOver(scrollTrack))
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(scrollThumb, Input.mousePosition, null, out Vector2 local);
            bool onThumb = IsPointerOver(scrollThumb);
            scrollGrabOffset = onThumb ? -local.y : scrollThumb.sizeDelta.y * 0.5f; // 손잡이 위쪽 끝에서 잡은 곳까지
            dragging = Drag.Scroll;
            DragScroll();
            return;
        }

        // 보이는 영역 밖(스크롤로 가려진 부분)은 누를 수 없음
        if (!IsPointerOver(viewport)) return;

        if (IsPointerOverSlider(windowSlider)) { dragging = Drag.Window; SetDragValue(); return; }
        if (IsPointerOverSlider(contentSlider)) { dragging = Drag.Content; SetDragValue(); return; }
        if (HUDUserSettings.CombatFade && IsPointerOverSlider(combatSlider)) { dragging = Drag.Combat; SetDragValue(); return; }
        if (IsPointerOverSlider(invWindowSlider)) { dragging = Drag.InvWindow; SetDragValue(); return; }
        if (IsPointerOverSlider(subFrameSlider)) { dragging = Drag.SubFrame; SetDragValue(); return; }
        if (IsPointerOverSlider(slotIconSlider)) { dragging = Drag.SlotIcon; SetDragValue(); return; }
        if (IsPointerOverSlider(passiveIconSlider)) { dragging = Drag.PassiveIcon; SetDragValue(); return; }
        if (HUDUserSettings.InventoryCombatFade && IsPointerOverSlider(invCombatSlider)) { dragging = Drag.InvCombat; SetDragValue(); return; }

        if (IsPointerOver(invCombatHit))
        {
            HUDUserSettings.InventoryCombatFade = !HUDUserSettings.InventoryCombatFade;
            if (HUDUserSettings.InventoryCombatFade) UISound.Confirm(); else UISound.Cancel();
            return;
        }
        if (HUDUserSettings.InventoryCombatFade)
        {
            if (IsPointerOver(invModeBackground.rect) && HUDUserSettings.InventoryCombatMode != HUDCombatFadeMode.BackgroundOnly)
            {
                HUDUserSettings.InventoryCombatMode = HUDCombatFadeMode.BackgroundOnly;
                UISound.Confirm();
                return;
            }
            if (IsPointerOver(invModeAll.rect) && HUDUserSettings.InventoryCombatMode != HUDCombatFadeMode.All)
            {
                HUDUserSettings.InventoryCombatMode = HUDCombatFadeMode.All;
                UISound.Confirm();
                return;
            }
        }

        foreach (var c in cells)
        {
            if (!IsPointerOver(c.rect)) continue;
            bool hiddenNow = !HUDUserSettings.IsStatHidden(c.entry.statType);
            HUDUserSettings.SetStatHidden(c.entry.statType, hiddenNow);
            if (hiddenNow) UISound.Cancel(); else UISound.Confirm();
            return;
        }

        if (IsPointerOver(whiteCombatHit))
        {
            HUDUserSettings.WhiteTextCombat = !HUDUserSettings.WhiteTextCombat;
            if (HUDUserSettings.WhiteTextCombat) UISound.Confirm(); else UISound.Cancel();
            return;
        }
        if (IsPointerOver(whiteNormalHit))
        {
            HUDUserSettings.WhiteTextNormal = !HUDUserSettings.WhiteTextNormal;
            if (HUDUserSettings.WhiteTextNormal) UISound.Confirm(); else UISound.Cancel();
            return;
        }

        if (IsPointerOver(combatHit))
        {
            HUDUserSettings.CombatFade = !HUDUserSettings.CombatFade;
            if (HUDUserSettings.CombatFade) UISound.Confirm(); else UISound.Cancel();
            return;
        }

        if (HUDUserSettings.CombatFade)
        {
            if (IsPointerOver(modeBackground.rect) && HUDUserSettings.CombatMode != HUDCombatFadeMode.BackgroundOnly)
            {
                HUDUserSettings.CombatMode = HUDCombatFadeMode.BackgroundOnly;
                UISound.Confirm();
                return;
            }
            if (IsPointerOver(modeAll.rect) && HUDUserSettings.CombatMode != HUDCombatFadeMode.All)
            {
                HUDUserSettings.CombatMode = HUDCombatFadeMode.All;
                UISound.Confirm();
            }
        }
    }

    // 스크롤 막대 손잡이 끌기: 잡은 곳이 마우스를 따라가도록 scrollY를 정합니다.
    private void DragScroll()
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(scrollTrack, Input.mousePosition, null, out Vector2 local);
        Rect r = scrollTrack.rect;
        float fromTop = r.yMax - local.y - scrollGrabOffset; // 손잡이 위쪽 끝이 와야 할 위치(트랙 위에서부터)
        float range = Mathf.Max(1f, r.height - scrollThumb.sizeDelta.y);
        scrollY = Mathf.Clamp(fromTop / range, 0f, 1f) * maxScroll;
    }

    private void SetDragValue()
    {
        switch (dragging)
        {
            case Drag.Window: HUDUserSettings.WindowOpacity = ValueFromPointer(windowSlider); break;
            case Drag.Content: HUDUserSettings.ContentOpacity = ValueFromPointer(contentSlider); break;
            case Drag.Combat: HUDUserSettings.CombatOpacity = ValueFromPointer(combatSlider); break; // 상한에서 잘림
            case Drag.InvWindow: HUDUserSettings.InventoryWindowOpacity = ValueFromPointer(invWindowSlider); break;
            case Drag.SubFrame: HUDUserSettings.SlotSubFrameOpacity = ValueFromPointer(subFrameSlider); break;
            case Drag.SlotIcon: HUDUserSettings.SlotIconOpacity = ValueFromPointer(slotIconSlider); break;
            case Drag.PassiveIcon: HUDUserSettings.PassiveIconOpacity = ValueFromPointer(passiveIconSlider); break;
            case Drag.InvCombat: HUDUserSettings.InventoryCombatOpacity = ValueFromPointer(invCombatSlider); break; // 상한에서 잘림
        }
    }

    // ------------------------------------------------------------------
    // 보이는 스탯 목록
    // ------------------------------------------------------------------

    private void RebuildCellsIfNeeded(PlayerStatDisplay source)
    {
        var sb = new System.Text.StringBuilder();
        if (source != null && source.statEntries != null)
        {
            foreach (var e in source.statEntries)
            {
                if (e != null && e.enabled) sb.Append((int)e.statType).Append(',');
            }
        }
        string sig = sb.ToString();
        if (sig == cellsSignature) return;
        cellsSignature = sig;

        foreach (var c in cells) if (c.rect != null) Object.Destroy(c.rect.gameObject);
        cells.Clear();
        if (source == null || source.statEntries == null) return;

        foreach (var e in source.statEntries)
        {
            if (e == null || !e.enabled) continue;
            var c = new StatCellView { entry = e };
            c.rect = CreateRect(content, "Stat_" + e.statType);
            c.box = CreateCheckBox(c.rect, "Check");
            c.icon = CreateImage(c.rect, "Icon", out c.iconRect);
            c.icon.preserveAspect = true;
            c.name = CreateText(c.rect, "Name", TextAnchor.MiddleLeft);
            cells.Add(c);
        }
    }

    private static string DisplayName(PlayerStatDisplay.StatEntry e)
    {
        if (!string.IsNullOrEmpty(e.tooltipName)) return e.tooltipName;
        return e.statType.ToString(); // 툴팁 이름 칸이 비어 있을 때만(지금 PlayerHUD는 전부 채워져 있음)
    }

    // ------------------------------------------------------------------
    // 견본
    // ------------------------------------------------------------------

    private PreviewView CreatePreview(RectTransform parent, string name)
    {
        var p = new PreviewView();
        p.title = CreateText(parent, name + "_Title", TextAnchor.MiddleCenter);

        // 둥근 칸 = 마스크(그림이 칸 밖으로 나가지 않게). 마스크 그림 자체는 보이지 않습니다.
        p.boxRect = CreateRect(parent, name + "_Box");
        p.maskImage = p.boxRect.gameObject.AddComponent<Image>();
        p.maskImage.raycastTarget = false;
        var mask = p.boxRect.gameObject.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        p.bgImage = CreateImage(p.boxRect, "Background", out p.bgRect);
        p.bgRect.anchorMin = p.bgRect.anchorMax = new Vector2(0.5f, 0.5f);
        p.bgRect.pivot = new Vector2(0.5f, 0.5f);

        p.panel = StatPanelView.Create(p.boxRect, "PreviewStatPanel");
        p.button = new CollapsibleHUDPanel(name + "_Button", HUDCollapseDirection.Down, false);

        p.slotPanel = SlotPanelView.Create(p.boxRect, "PreviewSlotPanel");
        p.slotButton = new CollapsibleHUDPanel(name + "_SlotButton", HUDCollapseDirection.Right, false);
        p.passivePanel = InventoryPanelView.Create(p.boxRect, "PreviewPassivePanel", false);
        p.passiveButton = new CollapsibleHUDPanel(name + "_PassiveButton", HUDCollapseDirection.Right, false);
        // 견본 칸 = 장신구 1/일회용 1/음식 1 칸에 예시 아이콘, 두 번째 칸들은 잠긴 모습(기본 상태)
        for (int i = 0; i < SlotPanelView.SlotCount; i++)
        {
            bool locked = i % PlayerSlotItemDisplay.SlotsPerCategory != 0;
            p.slotPanel.slots[i].lockAmount = locked ? 1f : 0f;
            p.slotPanel.slots[i].lockInitialized = true;
        }
        return p;
    }

    private void LayoutPreview(PreviewView p, SettingsUISettings set, PopupStyle style, PlayerStatDisplay source, string title,
                               Sprite background, float windowAlpha, float contentAlpha, float whiteWeight,
                               float x, float titleY, float boxTop, float boxW, float boxH)
    {
        PlaceText(p.title, title, x, titleY, boxW, set.hudLabelFontSize + 1, style.panelTextColor);
        p.title.alignment = TextAlignmentOptions.Center;

        Place(p.boxRect, x, boxTop, boxW, boxH);
        UIRoundedBox.Apply(p.maskImage, Color.white, set.previewCornerRadius, new Vector2(boxW, boxH));

        // 그림은 칸을 꽉 채우도록(비율 유지, 넘치는 부분은 마스크가 자름)
        if (background != null)
        {
            p.bgImage.enabled = true;
            p.bgImage.sprite = background;
            p.bgImage.color = Color.white;
            Vector2 sz = background.rect.size;
            float scale = Mathf.Max(boxW / Mathf.Max(1f, sz.x), boxH / Mathf.Max(1f, sz.y));
            p.bgRect.sizeDelta = sz * scale;
        }
        else
        {
            p.bgImage.enabled = true;
            p.bgImage.sprite = null;
            p.bgImage.color = new Color32(0x6E, 0x7F, 0x8C, 0xFF); // 그림이 없을 때 회색 바닥
            p.bgRect.sizeDelta = new Vector2(boxW, boxH);
        }
        p.bgRect.anchoredPosition = Vector2.zero;

        // 견본 스탯 창 - 실제 HUD와 같은 규칙(보이는 스탯/배치/모양)으로, 숫자만 고정 예시
        bool visible = source != null && !HUDUserSettings.HidesWholePanel && RebuildPreviewRows(p, source) > 0;
        if (p.panel.gameObject.activeSelf != visible) p.panel.gameObject.SetActive(visible);
        if (!visible) return;

        StatPanelLayout layout = source.BuildLayout();
        Color valueColor = source.GetValueTextColor(whiteWeight);
        for (int i = 0; i < p.rows.Count; i++)
        {
            var row = p.rows[i];
            var entry = row.entry;
            StatPanelView.ApplyIcon(row, entry.icon, source.fallbackIconColor, layout.iconSize);
            row.valueText.text = SampleText(entry);
            row.valueText.fontSize = layout.fontSize;
            row.valueText.color = valueColor;
            row.valueRect.localScale = Vector3.one;
        }
        p.panel.Layout(p.rows, layout);
        p.panel.ApplyLook(p.rows, source.BuildLook(windowAlpha, contentAlpha, whiteWeight));

        // 접기 버튼 모양(누를 수 없음)도 실제처럼 붙입니다.
        p.button.TickPreview(source.collapse, p.panel.panelRect);

        // 크기/위치는 인벤토리 견본과 함께 ArrangePreview가 정합니다.
    }

    // 인벤토리 견본 내용(슬롯 = 장신구/일회용/음식 첫 칸 예시 아이콘, 패시브 = 예시 아이콘)을 실제 HUD 코드로 그립니다.
    // 각 아이콘 0%면 그 인벤토리는 통째로 숨김(실제와 같음). 크기/위치는 ArrangePreview.
    private void LayoutInventoryPreview(PreviewView p, SettingsUISettings set, PlayerSlotItemDisplay slotSource,
                                        PlayerInventoryDisplay passiveSource, bool combat)
    {
        HUDUserSettings.GetInventoryOpacities(combat, out float window, out float subFrame, out float slotIcon, out float passiveIcon);

        bool showSlot = slotSource != null && !HUDUserSettings.HidesSlotPanel;
        bool showPassive = passiveSource != null && !HUDUserSettings.HidesPassivePanel;
        if (p.slotPanel.gameObject.activeSelf != showSlot) p.slotPanel.gameObject.SetActive(showSlot);
        if (p.passivePanel.gameObject.activeSelf != showPassive) p.passivePanel.gameObject.SetActive(showPassive);

        if (showSlot)
        {
            p.slotPanel.Layout(slotSource, window, subFrame);
            var icons = set.previewSlotIcons;
            for (int i = 0; i < SlotPanelView.SlotCount; i++)
            {
                int category = i / PlayerSlotItemDisplay.SlotsPerCategory;
                bool first = i % PlayerSlotItemDisplay.SlotsPerCategory == 0;
                Sprite icon = first && icons != null && category < icons.Length ? icons[category] : null;
                p.slotPanel.ApplySlot(slotSource, i, icon != null, icon, false, subFrame, slotIcon);
            }
            p.slotButton.TickPreview(slotSource.collapse, p.slotPanel.panelRect);
        }
        if (showPassive)
        {
            p.passivePanel.ApplyPanel(passiveSource, window);
            var icons = set.previewPassiveIcons;
            int count = icons != null ? icons.Length : 0;
            p.passivePanel.EnsureIconCount(count);
            for (int i = 0; i < count; i++) p.passivePanel.iconImages[i].sprite = icons[i];
            p.passivePanel.ApplyGrid(passiveSource, count, passiveIcon);
            p.passiveButton.TickPreview(passiveSource.collapse, p.passivePanel.panelRect);
        }
    }

    // 견본 배치: 스탯 창(왼쪽) + 인벤토리(오른쪽, 슬롯 위/패시브 아래)를 바짝 붙여 한 묶음으로 보고, 같은 배율로 칸에 꽉 차게 키운 뒤
    // 칸 가운데에 둡니다(형빈 확정 - 실제 화면 거리는 무시하고 크게). 접기 버튼이 차지하는 자리도 포함합니다.
    private static void ArrangePreview(PreviewView p, SettingsUISettings set, PlayerStatDisplay statSource,
                                       PlayerSlotItemDisplay slotSource, PlayerInventoryDisplay passiveSource, float boxW, float boxH)
    {
        float margin = set.previewInnerMargin;
        float gap = set.previewGroupGap;

        // 스탯(배율 1 기준): 위쪽 접기 버튼 높이 포함
        bool showStat = p.panel.gameObject.activeSelf;
        Vector2 statSize = showStat ? p.panel.panelRect.sizeDelta : Vector2.zero;
        var sc = statSource != null ? statSource.collapse : null;
        float statButtonH = showStat && sc != null ? Mathf.Max(0f, sc.buttonGap + sc.buttonOffset.y + sc.buttonDepth) : 0f;
        float statW = statSize.x;
        float statH = statSize.y + statButtonH;

        // 인벤토리(배율 1 기준): 왼쪽 접기 버튼 폭 포함, 슬롯 위 + 패시브 아래
        bool showSlot = p.slotPanel.gameObject.activeSelf;
        bool showPassive = p.passivePanel.gameObject.activeSelf;
        Vector2 slotSize = showSlot ? p.slotPanel.panelRect.sizeDelta : Vector2.zero;
        Vector2 passiveSize = showPassive ? p.passivePanel.panelRect.sizeDelta : Vector2.zero;
        float slotButtonW = showSlot && slotSource != null ? ButtonExtent(slotSource.collapse) : 0f;
        float passiveButtonW = showPassive && passiveSource != null ? ButtonExtent(passiveSource.collapse) : 0f;
        float invInnerGap = showSlot && showPassive ? set.previewInventoryGap : 0f;
        float invW = Mathf.Max(showSlot ? slotSize.x + slotButtonW : 0f, showPassive ? passiveSize.x + passiveButtonW : 0f);
        float invH = slotSize.y + passiveSize.y + invInnerGap;

        bool both = statW > 0f && invW > 0f;
        float groupW = statW + invW + (both ? gap : 0f);
        float groupH = Mathf.Max(statH, invH);
        if (groupW <= 0f || groupH <= 0f) return;

        float s = Mathf.Min(set.previewMaxScale, Mathf.Min((boxW - margin * 2f) / groupW, (boxH - margin * 2f) / groupH));
        s = Mathf.Max(0.05f, s);

        // 묶음을 칸 가운데에 (칸 왼쪽 위 기준 좌표)
        float left = (boxW - groupW * s) * 0.5f;
        float top = (boxH - groupH * s) * 0.5f;

        if (showStat)
        {
            // 스탯 패널은 왼쪽 아래 기준점 - 묶음 아래쪽에 맞춤
            var r = p.panel.panelRect;
            r.anchorMin = r.anchorMax = Vector2.zero;
            r.pivot = Vector2.zero;
            r.localScale = new Vector3(s, s, 1f);
            float bottom = boxH - (top + groupH * s);
            r.anchoredPosition = new Vector2(left, bottom + (groupH - statH) * 0.5f * s);
        }

        float invLeft = left + (statW > 0f ? (statW + gap) * s : 0f);
        float invTop = top + (groupH - invH) * 0.5f * s;
        if (showSlot)
        {
            PlaceTopLeft(p.slotPanel.panelRect, invLeft + slotButtonW * s, invTop, s);
            invTop += (slotSize.y + invInnerGap) * s;
        }
        if (showPassive)
        {
            PlaceTopLeft(p.passivePanel.panelRect, invLeft + passiveButtonW * s, invTop, s);
        }
    }

    // 오른쪽 접기 버튼(패널 왼쪽에 붙음)이 차지하는 폭
    private static float ButtonExtent(CollapsibleHUDSettings c)
    {
        return c != null ? Mathf.Max(0f, c.buttonGap - c.buttonOffset.x + c.buttonDepth) : 0f;
    }

    // 칸의 왼쪽 위 기준으로 (x, 위에서 top) 자리에 배율 s로 놓습니다(패널 왼쪽 위가 그 자리).
    private static void PlaceTopLeft(RectTransform rect, float x, float top, float s)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.localScale = new Vector3(s, s, 1f);
        rect.anchoredPosition = new Vector2(x, -top);
    }

    // 보이는 스탯 목록이 바뀌었을 때만 견본 줄을 다시 만듭니다. 줄 수를 돌려줍니다.
    private static int RebuildPreviewRows(PreviewView p, PlayerStatDisplay source)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var e in source.statEntries)
        {
            if (PlayerStatDisplay.IsShown(e)) sb.Append((int)e.statType).Append(',');
        }
        string sig = sb.ToString();
        if (sig == p.signature) return p.rows.Count;
        p.signature = sig;

        foreach (var row in p.rows) StatPanelView.DestroyRow(row);
        p.rows.Clear();
        foreach (var e in source.statEntries)
        {
            if (!PlayerStatDisplay.IsShown(e)) continue;
            var row = p.panel.CreateRow<PreviewRow>("Row_" + e.statType);
            row.entry = e;
            p.rows.Add(row);
        }
        return p.rows.Count;
    }

    // 견본용 고정 예시 숫자(형빈 확정) - 표기 규칙(배율/단위/소수점)은 실제 HUD와 같은 StatDisplayFormat.
    private static string SampleText(PlayerStatDisplay.StatEntry e)
    {
        float v;
        switch (e.statType)
        {
            case StatType.Damage: v = 3.5f; break;
            case StatType.MaxHealth: v = 3f; break;
            case StatType.MoveSpeed: v = 2.5f; break;
            case StatType.AttackSpeed: v = 2.7f; break;
            case StatType.Range: v = 6.5f; break;
            case StatType.ReloadTime: v = 1.2f; break;
            case StatType.MagazineSize: v = 5f; break;
            case StatType.Multishot: v = 1f; break;
            case StatType.MultishotInterval: v = 0.1f; break;
            case StatType.BulletSize: v = 0.1f; break;
            case StatType.BulletSpeed: v = 10f; break;
            case StatType.CostRecovery: v = 1f; break;
            case StatType.Luck: v = 0f; break;
            case StatType.SchaleChance: v = 0.05f; break;
            case StatType.GematriaChance: v = 0.05f; break;
            case StatType.GrenadeDamage: v = 10f; break;
            case StatType.GrenadeSelfDamage: v = 1f; break;
            case StatType.GrenadeRadius: v = 3f; break;
            case StatType.GrenadeRange: v = 2f; break;
            case StatType.HackDamage: v = 4f; break;
            case StatType.HackRadius: v = 2f; break;
            case StatType.HackDuration: v = 1.5f; break;
            default: v = 1f; break;
        }
        return StatDisplayFormat.FormatValue(e.statType, v, e.showDecimal ? "0.#" : "0");
    }

    // ------------------------------------------------------------------
    // 부품 만들기 / 배치
    // ------------------------------------------------------------------

    private static RectTransform CreateRect(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        return rect;
    }

    private static TextMeshProUGUI CreateText(Transform parent, string name, TextAnchor anchor)
    {
        var rect = CreateRect(parent, name);
        var text = HUDText.AddTo(rect.gameObject, anchor);
        text.raycastTarget = false;
        return text;
    }

    private static Image CreateImage(Transform parent, string name, out RectTransform rect)
    {
        rect = CreateRect(parent, name);
        var image = rect.gameObject.AddComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    // 부모의 왼쪽 위 기준으로 (x, 위에서 y) 위치에 (w, h) 크기로 놓습니다.
    private static void Place(RectTransform rect, float x, float yTop, float w, float h)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = new Vector2(w, h);
        rect.anchoredPosition = new Vector2(x, -yTop);
    }

    // 글자를 세로 가운데 yCenter에 놓습니다.
    private static void PlaceText(TextMeshProUGUI text, string value, float x, float yCenter, float w, int fontSize, Color color)
    {
        float h = fontSize + 12f;
        Place(text.rectTransform, x, yCenter - h * 0.5f, w, h);
        text.text = value;
        text.fontSize = fontSize;
        text.color = color;
    }

    private static SectionHeaderView CreateSectionHeader(Transform parent, string name)
    {
        var v = new SectionHeaderView();
        v.rect = CreateRect(parent, name);
        v.box = UIPatternBox.AddTo(v.rect.gameObject);
        v.label = CreateText(v.rect, "Label", TextAnchor.MiddleCenter);
        return v;
    }

    // 대항목 제목: 글자 폭 + 양옆 여백만큼의 남색 평행사변형 판(이름 판과 같은 색/둥글기/여백 - PopupStyle) 위에 흰 글자.
    // 판 왼쪽 끝 = x, 세로 가운데 = yCenter.
    private static void PlaceSectionHeader(SectionHeaderView v, string text, float x, float yCenter, SettingsUISettings set, PopupStyle style)
    {
        v.label.text = text;
        v.label.fontSize = set.hudSectionFontSize;
        v.label.color = Color.white;
        float h = set.hudSectionFontSize + style.namePlateExtraHeight;
        float slant = h * Mathf.Tan(style.buttonSlantAngle * Mathf.Deg2Rad); // 기운 만큼 판을 넓혀 글자가 가장자리에 닿지 않게
        float w = v.label.GetPreferredValues(text).x + style.namePlatePaddingX * 2f + slant;
        Place(v.rect, x, yCenter - h * 0.5f, w, h);
        v.box.SetupShape(style, style.namePlateColor, style.namePlateCornerRadius, style.buttonSlantAngle, false);
        v.label.rectTransform.anchorMin = Vector2.zero;
        v.label.rectTransform.anchorMax = Vector2.one;
        v.label.rectTransform.offsetMin = Vector2.zero;
        v.label.rectTransform.offsetMax = Vector2.zero;
    }

    private static CheckBoxView CreateCheckBox(Transform parent, string name)
    {
        var v = new CheckBoxView();
        v.rect = CreateRect(parent, name);
        v.border = UIPatternBox.AddTo(v.rect.gameObject);
        v.innerRect = CreateRect(v.rect, "Inner");
        v.inner = UIPatternBox.AddTo(v.innerRect.gameObject);
        v.check = CreateImage(v.rect, "Mark", out v.checkRect);
        v.check.sprite = GetCheckSprite();
        v.check.preserveAspect = true;
        return v;
    }

    // 체크 상자: 남색 테두리 + 흰 안쪽(켜면 하늘색 + 체크 표시). 못 누르면 반투명.
    private static void ApplyCheckBox(CheckBoxView v, bool on, bool enabled, PopupStyle style)
    {
        float size = v.rect.sizeDelta.x;
        Color navy = style.panelTextColor;
        Color sky = style.normalColor;
        if (!enabled)
        {
            navy.a *= style.disabledButtonAlpha;
            sky.a *= style.disabledButtonAlpha;
        }
        v.border.SetupShape(style, navy, 6f, 0f, false);

        const float border = 2f;
        v.innerRect.anchorMin = v.innerRect.anchorMax = new Vector2(0.5f, 0.5f);
        v.innerRect.pivot = new Vector2(0.5f, 0.5f);
        v.innerRect.sizeDelta = new Vector2(size - border * 2f, size - border * 2f);
        v.innerRect.anchoredPosition = Vector2.zero;
        Color innerColor = on ? sky : Color.white;
        v.inner.SetupShape(style, innerColor, 4f, 0f, false);

        v.check.enabled = on;
        v.checkRect.anchorMin = v.checkRect.anchorMax = new Vector2(0.5f, 0.5f);
        v.checkRect.pivot = new Vector2(0.5f, 0.5f);
        v.checkRect.sizeDelta = new Vector2(size * 0.8f, size * 0.8f);
        v.checkRect.anchoredPosition = Vector2.zero;
        v.check.color = navy;
    }

    // [체크 상자 + 이름] 한 줄. 줄 전체가 클릭 범위입니다.
    private static void LayoutCheckRow(RectTransform hit, CheckBoxView box, TextMeshProUGUI label, SettingsUISettings set, PopupStyle style,
                                       string text, bool on, bool enabled, float x, float yCenter, float width)
    {
        float h = Mathf.Max(set.hudCheckboxSize, set.hudLabelFontSize + 8f);
        Place(hit, x, yCenter - h * 0.5f, width, h);
        float cb = set.hudCheckboxSize;
        Place(box.rect, 0f, (h - cb) * 0.5f, cb, cb);
        ApplyCheckBox(box, on, enabled, style);

        Color c = style.panelTextColor;
        if (!enabled) c.a *= style.disabledButtonAlpha;
        Place(label.rectTransform, cb + 10f, 0f, Mathf.Max(10f, width - cb - 10f), h);
        label.text = text;
        label.fontSize = set.hudLabelFontSize;
        label.color = c;
    }

    private static ModeButtonView CreateModeButton(Transform parent, string name)
    {
        var v = new ModeButtonView();
        v.rect = CreateRect(parent, name);
        v.box = UIPatternBox.AddTo(v.rect.gameObject);
        v.label = CreateText(v.rect, "Label", TextAnchor.MiddleCenter);
        return v;
    }

    // [배경 창만 / 전체] 고른 쪽 = 하늘색, 아닌 쪽 = 연한 회색(글자도 회색). 전투 반투명이 꺼져 있으면 반투명.
    private static void LayoutModeButton(ModeButtonView v, SettingsUISettings set, PopupStyle style, string text, bool selected,
                                         bool enabled, float x, float yTop, Vector2 size)
    {
        Place(v.rect, x, yTop, size.x, size.y);
        Color fill = selected ? style.normalColor : set.hudModeUnselectedColor;
        Color textColor = selected ? style.panelTextColor : set.hudSliderCapMarkColor;
        if (!enabled)
        {
            fill.a *= style.disabledButtonAlpha;
            textColor.a *= style.disabledButtonAlpha;
        }
        v.box.SetupShape(style, fill, style.buttonCornerRadiusNew * 0.6f, style.buttonSlantAngle, false);
        v.label.rectTransform.anchorMin = Vector2.zero;
        v.label.rectTransform.anchorMax = Vector2.one;
        v.label.rectTransform.offsetMin = Vector2.zero;
        v.label.rectTransform.offsetMax = Vector2.zero;
        v.label.text = text;
        v.label.fontSize = set.hudModeButtonFontSize;
        v.label.color = textColor;
    }

    private static SliderView CreateSlider(Transform parent, string name)
    {
        var s = new SliderView();
        s.row = CreateRect(parent, name);
        s.label = CreateText(s.row, "Label", TextAnchor.MiddleLeft);
        s.trackImage = CreateImage(s.row, "Track", out s.track);
        s.fillImage = CreateImage(s.track, "Fill", out s.fill);
        s.capMarkImage = CreateImage(s.track, "CapMark", out s.capMark);
        var knobGO = new GameObject("Knob");
        knobGO.transform.SetParent(s.track, false);
        s.knob = knobGO.AddComponent<RectTransform>();
        s.knobBox = UIPatternBox.AddTo(knobGO);
        s.valueText = CreateText(s.row, "Value", TextAnchor.MiddleRight);
        return s;
    }

    // 한 줄 = [이름][슬라이더][00%]. cap >= 0이면 그 위치에 "여기까지만" 눈금을 그립니다. 못 쓰면 회색.
    private static void LayoutSlider(SliderView s, SettingsUISettings set, PopupStyle style, string label, float value, bool enabled,
                                     float cap, float x, float yCenter, float width)
    {
        const float valueWidth = 80f;
        const float gap = 15f;
        float rowHeight = Mathf.Max(set.knobSize, set.hudLabelFontSize + 8f);
        Place(s.row, x, yCenter - rowHeight * 0.5f, width, rowHeight);

        Color navy = style.panelTextColor;
        Color fillColor = set.fillColor;
        if (!enabled)
        {
            navy.a *= style.disabledButtonAlpha;
            fillColor.a *= style.disabledButtonAlpha;
        }

        Place(s.label.rectTransform, 0f, 0f, set.hudSliderLabelWidth, rowHeight);
        s.label.text = label;
        s.label.fontSize = set.hudLabelFontSize;
        s.label.color = navy;

        float trackW = Mathf.Max(40f, width - set.hudSliderLabelWidth - gap - valueWidth);
        s.track.anchorMin = s.track.anchorMax = new Vector2(0f, 0.5f);
        s.track.pivot = new Vector2(0f, 0.5f);
        Vector2 trackSize = new Vector2(trackW, set.sliderHeight);
        s.track.sizeDelta = trackSize;
        s.track.anchoredPosition = new Vector2(set.hudSliderLabelWidth, 0f);
        UIRoundedBox.Apply(s.trackImage, set.sliderTrackColor, set.sliderHeight * 0.5f, trackSize);

        float v = Mathf.Clamp01(value);
        float fillWidth = Mathf.Max(set.sliderHeight, trackW * v);
        s.fill.anchorMin = s.fill.anchorMax = new Vector2(0f, 0.5f);
        s.fill.pivot = new Vector2(0f, 0.5f);
        Vector2 fillSize = new Vector2(fillWidth, set.sliderHeight);
        s.fill.sizeDelta = fillSize;
        s.fill.anchoredPosition = Vector2.zero;
        UIRoundedBox.Apply(s.fillImage, v > 0f ? fillColor : Color.clear, set.sliderHeight * 0.5f, fillSize);

        bool showCap = cap >= 0f;
        if (s.capMark.gameObject.activeSelf != showCap) s.capMark.gameObject.SetActive(showCap);
        if (showCap)
        {
            s.capMark.anchorMin = s.capMark.anchorMax = new Vector2(0f, 0.5f);
            s.capMark.pivot = new Vector2(0.5f, 0.5f);
            s.capMark.sizeDelta = new Vector2(2f, set.sliderHeight + 10f);
            s.capMark.anchoredPosition = new Vector2(trackW * Mathf.Clamp01(cap), 0f);
            Color capColor = set.hudSliderCapMarkColor;
            if (!enabled) capColor.a *= style.disabledButtonAlpha;
            s.capMarkImage.color = capColor;
            s.capMarkImage.sprite = null;
        }

        s.knob.anchorMin = s.knob.anchorMax = new Vector2(0f, 0.5f);
        s.knob.pivot = new Vector2(0.5f, 0.5f);
        s.knob.sizeDelta = new Vector2(set.knobSize, set.knobSize);
        s.knob.anchoredPosition = new Vector2(trackW * v, 0f);
        Color knobColor = set.knobColor;
        if (!enabled) knobColor.a *= style.disabledButtonAlpha;
        s.knobBox.SetupShape(style, knobColor, set.knobSize * 0.5f, 0f, enabled);
        s.knob.SetAsLastSibling();

        s.valueText.rectTransform.anchorMin = s.valueText.rectTransform.anchorMax = new Vector2(1f, 0.5f);
        s.valueText.rectTransform.pivot = new Vector2(1f, 0.5f);
        s.valueText.rectTransform.sizeDelta = new Vector2(valueWidth, rowHeight);
        s.valueText.rectTransform.anchoredPosition = Vector2.zero;
        s.valueText.text = Mathf.RoundToInt(v * 100f) + "%";
        s.valueText.fontSize = set.hudLabelFontSize;
        s.valueText.color = navy;
    }

    // ------------------------------------------------------------------
    // 마우스 판정
    // ------------------------------------------------------------------

    private static bool IsPointerOver(RectTransform rect)
    {
        if (rect == null || !rect.gameObject.activeInHierarchy) return false;
        return RectTransformUtility.RectangleContainsScreenPoint(rect, Input.mousePosition, null);
    }

    // 손잡이가 잡기 쉽도록 슬라이더 줄 전체(손잡이 높이만큼) + 손잡이 반지름만큼 양옆을 판정 범위로 씁니다(소리 탭과 같음).
    private static bool IsPointerOverSlider(SliderView s)
    {
        if (s == null || s.track == null || !s.track.gameObject.activeInHierarchy) return false;
        var set = SettingsUISettings.Get();
        RectTransformUtility.ScreenPointToLocalPointInRectangle(s.track, Input.mousePosition, null, out Vector2 local);
        Rect r = s.track.rect;
        float padX = set.knobSize * 0.5f;
        float halfH = Mathf.Max(r.height, set.knobSize) * 0.5f;
        return local.x >= r.xMin - padX && local.x <= r.xMax + padX && Mathf.Abs(local.y - r.center.y) <= halfH;
    }

    private static float ValueFromPointer(SliderView s)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(s.track, Input.mousePosition, null, out Vector2 local);
        float width = Mathf.Max(1f, s.track.rect.width);
        return Mathf.Clamp01((local.x - s.track.rect.xMin) / width);
    }

    // ------------------------------------------------------------------
    // 체크 표시 그림 (이미지 파일 없이 실행 중에 한 번 생성 - 가장자리 부드럽게)
    // ------------------------------------------------------------------

    private static Sprite checkSprite;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        checkSprite = null;
    }

    private static Sprite GetCheckSprite()
    {
        if (checkSprite != null) return checkSprite;

        const int n = 64;
        var a = new Vector2(n * 0.18f, n * 0.52f);
        var b = new Vector2(n * 0.42f, n * 0.28f);
        var c = new Vector2(n * 0.84f, n * 0.74f);
        float half = n * 0.07f; // 선 두께의 절반

        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        var pixels = new Color32[n * n];
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                float d = Mathf.Min(DistanceToSegment(p, a, b), DistanceToSegment(p, b, c));
                float alpha = Mathf.Clamp01(half - d + 0.5f);
                pixels[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply();
        checkSprite = Sprite.Create(tex, new Rect(0f, 0f, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        return checkSprite;
    }

    private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
        return Vector2.Distance(p, a + ab * t);
    }
}
