using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// [스탯 창 그리기 공용 부품]
// 실제 HUD의 스탯 창(PlayerStatDisplay)과 설정 창 HUD 탭의 견본(SettingsHUDTab)이 같은 코드로 그려지도록 모은 부품입니다.
// 한쪽만 고쳐서 견본과 실제 모양이 달라지는 일을 막기 위해, 패널 구조/줄 배치/흰 창/불투명도/그림자는 전부 여기서만 정합니다.
// 아이콘 그림, 숫자 글자/크기/색(수치 변화 연출 포함)은 부르는 쪽이 정합니다.
//
// [구조] Panel(패널 크기 = 내용 + 사방 여백, 왼쪽 아래 기준)
//   - Background : 흰 창(UIPatternBox.SetupPanel, 값은 PopupStyle) + CanvasGroup(창 불투명도 - 무늬/그림자도 함께 옅어짐)
//   - Content    : 모든 줄 + CanvasGroup(아이콘·숫자 불투명도)
//   - (접기 버튼은 CollapsibleHUDPanel이 Panel의 마지막 자식으로 붙임 - 불투명도의 영향을 받지 않음)
//
// [줄 배치] 목록 순서대로 왼쪽 열 맨 위부터 아래로, 한 열이 rowsPerColumn개로 차면 오른쪽 열로 넘어갑니다.
// 예전의 수류탄/해킹 스탯 별도 열은 없앴습니다(보이는 스탯을 고를 수 있게 되면서 따로 떨어진 열이 거슬려서 - 형빈 확정).
// 패널 폭은 마지막 열의 "실제로 쓰는 폭"까지만 셉니다(열 간격이 음수면 숫자 칸 중 오른쪽 열과 겹치는 부분은 원래 안 쓰는 자리).
//
// [숫자 그림자] 흰 숫자일 때 밝은 바닥에서도 보이도록, 숫자 뒤에 같은 글자를 그림자 색으로 한 번 더 그립니다(TMP는 UI Shadow
// 효과를 받지 않아서 글자를 하나 더 둠). 그림자 글자는 숫자보다 먼저(뒤에) 그려지는 형제이고, ApplyLook이 매 프레임 위치/크기/글자를 맞춥니다.
public struct StatPanelLayout
{
    public float iconSize;
    public float fontSize;
    public float valueTextWidth;
    public float iconToValueSpacing;
    public float rowSpacing;
    public int rowsPerColumn;
    public float columnSpacing;
    public float padding;          // 흰 창 사방 여백(흰 창을 안 쓰면 0)
}

public struct StatPanelLook
{
    public bool showBackground;
    public float backgroundAlpha;  // 0~1
    public float contentAlpha;     // 0~1
    public bool showIconShadow;
    public Color iconShadowColor;
    public Vector2 iconShadowOffset;
    public bool showValueShadow;
    public Color valueShadowColor;
    public Vector2 valueShadowOffset;
}

public class StatPanelRow
{
    public RectTransform rowRect;
    public RectTransform iconRect;
    public Image iconImage;
    public RectTransform valueShadowRect;
    public TextMeshProUGUI valueShadowText;
    public RectTransform valueRect;
    public TextMeshProUGUI valueText;
}

public class StatPanelView
{
    public RectTransform panelRect;
    public RectTransform backgroundRect;
    public UIPatternBox backgroundBox;
    public CanvasGroup backgroundGroup;
    public RectTransform contentRect;
    public CanvasGroup contentGroup;

    public GameObject gameObject => panelRect != null ? panelRect.gameObject : null;

    // parent 아래에 빈 패널을 만듭니다(왼쪽 아래 기준점 - 위치는 부르는 쪽이 anchoredPosition으로).
    public static StatPanelView Create(Transform parent, string name)
    {
        var view = new StatPanelView();

        var panelGO = new GameObject(name);
        panelGO.transform.SetParent(parent, false);
        view.panelRect = panelGO.AddComponent<RectTransform>();
        view.panelRect.anchorMin = Vector2.zero;
        view.panelRect.anchorMax = Vector2.zero;
        view.panelRect.pivot = Vector2.zero;
        view.panelRect.sizeDelta = Vector2.zero;

        // 흰 창 - 첫 자식이라 줄들보다 뒤. 클릭/툴팁은 마우스 좌표로 직접 판정하므로 레이캐스트는 끕니다(AddTo가 끔).
        var bgGO = new GameObject("Background");
        bgGO.transform.SetParent(view.panelRect, false);
        view.backgroundRect = bgGO.AddComponent<RectTransform>();
        Stretch(view.backgroundRect);
        view.backgroundBox = UIPatternBox.AddTo(bgGO);
        view.backgroundGroup = bgGO.AddComponent<CanvasGroup>();
        view.backgroundGroup.interactable = false;
        view.backgroundGroup.blocksRaycasts = false;

        var contentGO = new GameObject("Content");
        contentGO.transform.SetParent(view.panelRect, false);
        view.contentRect = contentGO.AddComponent<RectTransform>();
        Stretch(view.contentRect);
        view.contentGroup = contentGO.AddComponent<CanvasGroup>();
        view.contentGroup.interactable = false;
        view.contentGroup.blocksRaycasts = false;

        return view;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    // 줄 하나(아이콘 + 숫자 그림자 + 숫자)를 만듭니다. T = 부르는 쪽이 상태를 덧붙인 줄 클래스.
    public T CreateRow<T>(string name) where T : StatPanelRow, new()
    {
        var row = new T();

        var rowGO = new GameObject(name);
        rowGO.transform.SetParent(contentRect, false);
        row.rowRect = rowGO.AddComponent<RectTransform>();
        row.rowRect.anchorMin = Vector2.zero;
        row.rowRect.anchorMax = Vector2.zero;
        row.rowRect.pivot = Vector2.zero;

        var iconGO = new GameObject("Icon");
        iconGO.transform.SetParent(row.rowRect, false);
        row.iconRect = iconGO.AddComponent<RectTransform>();
        row.iconRect.anchorMin = Vector2.zero;
        row.iconRect.anchorMax = Vector2.zero;
        row.iconRect.pivot = Vector2.zero;
        row.iconImage = iconGO.AddComponent<Image>();
        row.iconImage.preserveAspect = true;
        row.iconImage.raycastTarget = false;

        // 그림자 글자를 숫자보다 먼저 만들어 뒤에 그려지게 합니다.
        var shadowGO = new GameObject("ValueShadow");
        shadowGO.transform.SetParent(row.rowRect, false);
        row.valueShadowRect = shadowGO.AddComponent<RectTransform>();
        row.valueShadowRect.anchorMin = Vector2.zero;
        row.valueShadowRect.anchorMax = Vector2.zero;
        row.valueShadowRect.pivot = new Vector2(0f, 0.5f);
        row.valueShadowText = HUDText.AddTo(shadowGO, TextAnchor.MiddleLeft);
        row.valueShadowText.raycastTarget = false;
        shadowGO.SetActive(false);

        var valueGO = new GameObject("Value");
        valueGO.transform.SetParent(row.rowRect, false);
        row.valueRect = valueGO.AddComponent<RectTransform>();
        row.valueRect.anchorMin = Vector2.zero;
        row.valueRect.anchorMax = Vector2.zero;
        // [수치 변화 연출] 커질 때 왼쪽 끝/세로 가운데 기준으로 커지도록 기준점을 왼쪽 가운데로 둡니다.
        row.valueRect.pivot = new Vector2(0f, 0.5f);
        row.valueText = HUDText.AddTo(valueGO, TextAnchor.MiddleLeft);
        row.valueText.raycastTarget = false;

        return row;
    }

    public static void DestroyRow(StatPanelRow row)
    {
        if (row != null && row.rowRect != null) Object.Destroy(row.rowRect.gameObject);
    }

    // 아이콘 그림(없으면 대체 색 원)과 크기를 넣습니다.
    public static void ApplyIcon(StatPanelRow row, Sprite icon, Color fallbackColor, float iconSize)
    {
        if (icon != null)
        {
            row.iconImage.sprite = icon;
            row.iconImage.color = Color.white;
        }
        else
        {
            row.iconImage.sprite = null;
            row.iconImage.color = fallbackColor;
        }
        row.iconRect.sizeDelta = new Vector2(iconSize, iconSize);
    }

    public static float RowHeight(StatPanelLayout L)
    {
        return Mathf.Max(L.iconSize, L.fontSize + 4f);
    }

    // 줄들을 배치하고 패널 크기(내용 + 사방 여백)를 정해 돌려줍니다.
    public Vector2 Layout<T>(IList<T> rows, StatPanelLayout L) where T : StatPanelRow
    {
        float pad = Mathf.Max(0f, L.padding);
        float rowHeight = RowHeight(L);
        float columnWidth = L.iconSize + L.iconToValueSpacing + L.valueTextWidth;
        int perColumn = Mathf.Max(1, L.rowsPerColumn);
        int count = rows.Count;

        int tallest = Mathf.Min(perColumn, count);
        int topSlot = tallest - 1;

        for (int i = 0; i < count; i++)
        {
            var row = rows[i];
            int column = i / perColumn;
            int indexInColumn = i % perColumn;

            float x = column * (columnWidth + L.columnSpacing);
            float y = (topSlot - indexInColumn) * (rowHeight + L.rowSpacing);

            row.rowRect.anchoredPosition = new Vector2(x + pad, y + pad);
            row.rowRect.sizeDelta = new Vector2(columnWidth, rowHeight);
            row.iconRect.anchoredPosition = new Vector2(0f, (rowHeight - L.iconSize) * 0.5f);
            // 숫자 칸 기준점이 왼쪽 가운데라 줄 높이의 가운데에 둡니다.
            row.valueRect.anchoredPosition = new Vector2(L.iconSize + L.iconToValueSpacing, rowHeight * 0.5f);
            row.valueRect.sizeDelta = new Vector2(L.valueTextWidth, L.iconSize);
        }

        int columns = (count + perColumn - 1) / perColumn;
        float lastColumnWidth = columnWidth + Mathf.Min(0f, L.columnSpacing);
        float contentWidth = columns > 0 ? (columns - 1) * (columnWidth + L.columnSpacing) + lastColumnWidth : 0f;
        float contentHeight = tallest > 0 ? tallest * rowHeight + (tallest - 1) * L.rowSpacing : 0f;
        Vector2 size = new Vector2(contentWidth + pad * 2f, contentHeight + pad * 2f);
        panelRect.sizeDelta = size;
        return size;
    }

    // 흰 창/불투명도/그림자를 적용합니다. 숫자 글자/크기/색/연출 배율을 정한 "뒤에" 불러야 그림자 글자가 맞춰집니다.
    public void ApplyLook<T>(IList<T> rows, StatPanelLook look) where T : StatPanelRow
    {
        if (backgroundBox.gameObject.activeSelf != look.showBackground) backgroundBox.gameObject.SetActive(look.showBackground);
        if (look.showBackground)
        {
            backgroundRect.SetAsFirstSibling(); // 항상 맨 뒤
            backgroundBox.SetupPanel(PopupStyle.Get()); // 매 프레임 - PopupStyle을 Play 중 바꿔도 바로 반영
            backgroundGroup.alpha = Mathf.Clamp01(look.backgroundAlpha);
        }
        contentGroup.alpha = Mathf.Clamp01(look.contentAlpha);

        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            ApplyIconShadow(row, look);
            SyncValueShadow(row, look);
        }
    }

    // 아이콘 그림자(UI Shadow 효과 - 같은 그림을 색만 바꿔 한 번 더 그림). 끄면 효과만 꺼 둡니다.
    private static void ApplyIconShadow(StatPanelRow row, StatPanelLook look)
    {
        var shadow = row.iconRect.GetComponent<UnityEngine.UI.Shadow>();
        if (shadow == null)
        {
            if (!look.showIconShadow) return;
            shadow = row.iconRect.gameObject.AddComponent<UnityEngine.UI.Shadow>();
            shadow.useGraphicAlpha = true;
        }
        shadow.enabled = look.showIconShadow;
        shadow.effectColor = look.iconShadowColor;
        shadow.effectDistance = look.iconShadowOffset;
    }

    private static void SyncValueShadow(StatPanelRow row, StatPanelLook look)
    {
        var go = row.valueShadowRect.gameObject;
        if (go.activeSelf != look.showValueShadow) go.SetActive(look.showValueShadow);
        if (!look.showValueShadow) return;

        row.valueShadowRect.sizeDelta = row.valueRect.sizeDelta;
        row.valueShadowRect.localScale = row.valueRect.localScale;
        row.valueShadowRect.anchoredPosition = row.valueRect.anchoredPosition + look.valueShadowOffset;
        if (row.valueShadowText.text != row.valueText.text) row.valueShadowText.text = row.valueText.text;
        row.valueShadowText.fontSize = row.valueText.fontSize;
        row.valueShadowText.color = look.valueShadowColor;
    }
}
