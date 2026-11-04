using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

// 화면에 "근처 보물상자 안에 무엇이 들어있는지"를 보여주는 설명칸입니다.
// PlayerItemInfoDisplay.cs(근처 패시브 아이템 설명칸)와 같은 위치/스타일을 그대로
// 재사용하지만, 항목이 하나가 아니라 여러 개(기본 아이템 묶음들)일 수 있어서 아이콘+
// 텍스트 한 줄짜리 "행(row)"을 여러 개 나열하는 구조로 만들었습니다.
//
// [패시브 아이템 케이스는 이제 이 스크립트가 다루지 않습니다 - 일반맵 보물상자 연동]
// 처음엔 패시브 아이템이 배정된 경우도 이 스크립트가(이름만 한 줄로) 보여줬는데, 동아리방
// 보물상자와 동일한 상세 정보(아이콘/이름/능력치/설명)를 보여주기로 결정하면서 그 역할을
// PlayerItemInfoDisplay.cs로 옮겼습니다(동아리방 보물상자와 완전히 같은 표시 로직을 재사용하기
// 위함 - PlayerItemInfoDisplay.cs 상단 주석 참고). 그래서 이 스크립트는 이제
// chest.PassiveItem은 아예 쳐다보지 않고, chest.BasicStacks(기본 아이템 여러 개)만
// 다룹니다. 한 상자에는 둘 중 하나만 들어있으므로(설계상 동시에 존재하지 않음), 두
// 패널이 동시에 뜨는 일은 없습니다.
//
// [데이터 출처 - TreasureChest + ItemPedestal, IBasicItemContainer로 통합]
// "플레이어 근처에 열 수 있는 컨테이너가 있는지"는 각 컴포넌트의 상호작용 판정이 이미
// 하고 있어서(PlayerInRange), 그 값을 그대로 사용합니다. 씬의 모든 TreasureChest +
// ItemPedestal 중 지금 범위 안에 있고 아직 보여줄 기본 아이템이 남은 것들만 후보로
// 놓고 가장 가까운 것 하나만 보여줍니다.
//
// [v1.30 - ItemPedestal도 기본 아이템을 줄 수 있게 됨]
// ItemPedestal(동아리방 보물상자)도 이제 "결국은 보물상자"로 취급되어, 뽑을 아이템이 하나도 없을 때 기본
// 아이템을 대체 지급할 수 있습니다(ItemPedestal.cs 참고). ItemPedestal과 TreasureChest는 상호작용 판정
// 방식이 서로 달라 클래스를 합치지 않았으므로, 이 화면은 IBasicItemContainer.cs라는
// 최소 인터페이스로 둘을 동일하게 취급합니다.
//
// [행(row) 개수가 매번 달라지는 문제 - 고정 개수 풀링]
// 상자 내용물 개수가 매번 다르기 때문에, maxRows개의 행을 미리 만들어두고 실제로 보여줄
// 개수만큼만 활성화하는 방식을 씁니다. PlayerItemInfoDisplay.ReflowContent()와 동일하게
// Layout Group 없이 RectTransform을 매 프레임 직접 계산해서 배치합니다.
//
// [아이콘 - 기본 아이템은 고정 6종이라 인스펙터에서 직접 연결]
// 기본 아이템(체력/수류탄/해킹툴/크레딧/청휘석/코스트)은 종류가 고정되어 있어 인스펙터에
// 스프라이트 6개를 직접 연결합니다. 아직 실제 아이콘이 없으면 비워둬도 되고, 그러면
// PlayerItemInfoDisplay와 동일하게 옅은 자리표시 사각형으로 보입니다.
[RequireComponent(typeof(RectTransform))]
public class TreasureChestInfoDisplay : MonoBehaviour
{
    [Header("참조 (비워두면 자동으로 찾습니다)")]
    public PlayerHUDManager hudManager;

    [Header("기본 아이템 아이콘 (비워두면 옅은 자리표시로 표시)")]
    public Sprite healthIcon;
    public Sprite grenadeIcon;
    public Sprite hackingToolIcon;
    public Sprite creditIcon;
    [FormerlySerializedAs("chungwiStoneIcon")]
    public Sprite pyroxenesIcon;
    public Sprite costIcon;

    [Header("패널 위치 (초상화/하트 묶음 기준 아래쪽, Play 중에도 즉시 반영됩니다)")]
    [Tooltip("근처 패시브 아이템 설명칸(PlayerItemInfoDisplay)과 같은 자리를 기본값으로 " +
             "씁니다 - 일반맵 방과 동아리방은 서로 다른 방이라 동시에 뜰 일이 없습니다. " +
             "혹시 겹쳐 보이면 이 값을 다르게 조정하세요.")]
    public float panelMarginX = 35f;
    public float panelGapBelowHeader = 20f;

    [Header("패널 크기")]
    public float panelWidth = 440f;
    public float panelMinHeight = 90f;
    [Tooltip("창 배경 스프라이트입니다. 넣으면 9-slice(Sliced)로 그려져서 모서리는 그대로 두고 " +
             "가운데만 늘어납니다(Sprite Editor에서 Border를 설정해 두세요). 비워두면 아래 Panel Color의 " +
             "단색 반투명 배경이 그대로 나옵니다.")]
    public Sprite backgroundSprite;
    [Tooltip("배경 스프라이트가 비어 있을 때만 쓰이는 단색 배경색입니다.")]
    public Color panelColor = new Color(0f, 0f, 0f, 0.82f);
    public float paddingLeft = 16f;
    public float paddingRight = 16f;
    public float paddingTop = 14f;
    public float paddingBottom = 14f;
    [Tooltip("행(아이콘+텍스트) 사이의 세로 간격")]
    public float rowSpacing = 6f;

    [Header("행 하나 (아이콘 + 텍스트)")]
    [Tooltip("동시에 보여줄 수 있는 최대 행 개수입니다. 상자 내용물이 이보다 많으면 " +
             "뒤쪽은 표시되지 않으니, 기본 아이템 묶음 풀 설정에 따라 넉넉하게 잡아주세요.")]
    public int maxRows = 8;
    public float rowIconSize = 36f;
    public float iconToTextSpacing = 10f;
    public int rowFontSize = 18;
    public Color rowTextColor = Color.white;

    private GameObject panelRoot;
    private RectTransform panelRect;
    private Image panelImage;

    private readonly List<RectTransform> rowIconRects = new List<RectTransform>();
    private readonly List<Image> rowIcons = new List<Image>();
    private readonly List<RectTransform> rowTextRects = new List<RectTransform>();
    private readonly List<TextMeshProUGUI> rowTexts = new List<TextMeshProUGUI>();

    private Transform playerTransform;
    private bool built = false;

    // ItemTooltipDisplay(v1.38 - 아이템 호버 툴팁)가 액티브 아이템 호버 툴팁을 이 패널과
    // 겹치지 않게 배치할 때 읽는 읽기 전용 값들입니다(PlayerItemInfoDisplay.IsShowingItem /
    // PanelAnchoredPosition과 동일한 역할 - 이 패널과 PlayerItemInfoDisplay는 한 상자에
    // 동시에 뜨지 않으므로 ItemTooltipDisplay가 둘 중 지금 떠 있는 쪽을 확인해서 피합니다).
    // 이 스크립트 내부 로직에는 영향을 주지 않습니다.
    public bool IsShowingInfo => panelRoot != null && panelRoot.activeSelf;
    public Vector2 PanelAnchoredPosition => panelRect != null ? panelRect.anchoredPosition : Vector2.zero;

    void Update()
    {
        EnsureBuilt();
        if (!built) return;

        ApplyPanelPosition();

        if (playerTransform == null)
        {
            var playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) playerTransform = playerObj.transform;
        }

        if (playerTransform == null)
        {
            HidePanel();
            return;
        }

        var nearest = FindNearestAvailableContainer();

        if (nearest == null)
        {
            HidePanel();
            return;
        }

        panelRoot.SetActive(true);
        ReflowContent(nearest);
    }

    // hudManager를 찾고, 그 Canvas 위에 패널을 한 번만 만듭니다. hudManager가 아직
    // Awake되지 않았을 수 있으므로(스크립트 실행 순서는 보장되지 않음) 매 프레임
    // 조용히 재시도합니다.
    private void EnsureBuilt()
    {
        if (built) return;

        if (hudManager == null)
        {
            hudManager = Object.FindFirstObjectByType<PlayerHUDManager>();
            if (hudManager == null) return;
        }

        var canvasRoot = hudManager.CanvasRoot;
        if (canvasRoot == null) return;

        BuildPanel(canvasRoot);
        built = true;
    }

    // 씬의 모든 TreasureChest + ItemPedestal 중, "지금 플레이어가 상호작용 범위 안에 있고
    // 아직 남은 기본 아이템이 있는" 것들만 후보로 놓고 가장 가까운 것을 고릅니다.
    //
    // [v1.30 - ItemPedestal도 후보에 포함]
    // ItemPedestal은 "뽑을 아이템이 없을 때의 대체 기본 아이템" 폴백으로도 기본 아이템을 줄 수
    // 있게 됐습니다(ItemPedestal.cs 참고). TreasureChest와 ItemPedestal은 상호작용 판정
    // 방식이 서로 달라(TreasureChest=거리 기반 통과 가능, ItemPedestal=트리거 Enter/Exit 물리적 장애물)
    // 클래스를 하나로 합치지 않고, 이 화면에 필요한 최소 정보만 IBasicItemContainer로
    // 뽑아내서 두 타입을 동일하게 취급합니다.
    private IBasicItemContainer FindNearestAvailableContainer()
    {
        // [F 대상 공용 판정] 지금 F가 먹는 대상(InteractionTargetSelector)이 기본 아이템을 보여줄 게 있는 상자일 때만
        // 그 상자를 돌려줍니다 - F 안내/실제 F 대상과 항상 일치. 대상이 포탈이거나 보여줄 게 없으면 null.
        var target = InteractionTargetSelector.Current;
        if (target is TreasureChest chest && HasAnythingToShow(chest)) return chest;
        if (target is ItemPedestal pedestal && HasAnythingToShow(pedestal)) return pedestal;
        return null;
    }

    // 패시브 아이템은 이제 이 스크립트가 다루지 않습니다(PlayerItemInfoDisplay가 담당) -
    // 그래서 여기서는 기본 아이템 재고만 확인합니다.
    private bool HasAnythingToShow(IBasicItemContainer container)
    {
        foreach (var stack in container.BasicStacks)
        {
            if (!stack.consumed) return true;
        }
        return false;
    }

    private void HidePanel()
    {
        if (panelRoot != null && panelRoot.activeSelf)
        {
            panelRoot.SetActive(false);
        }
    }

    // 패널 위치를 매 프레임 다시 계산합니다 (PlayerItemInfoDisplay.ApplyPanelPosition과
    // 동일한 방식 - Play 중 인스펙터 값을 바꾸면 바로 반영됩니다).
    private void ApplyPanelPosition()
    {
        if (panelRect == null || hudManager == null) return;

        panelRect.anchoredPosition = new Vector2(
            panelMarginX,
            -(hudManager.topLeftMarginY + hudManager.portraitSize + panelGapBelowHeader));
    }

    // 행들을 매 프레임 다시 채웁니다 (라이브 트윅 - 인스펙터 값을 Play 중 바꿔도 바로 반영).
    private void ReflowContent(IBasicItemContainer container)
    {
        float contentWidth = panelWidth - paddingLeft - paddingRight;
        float cursorY = -paddingTop;
        int rowIndex = 0;

        // 패시브 아이템은 PlayerItemInfoDisplay가 담당하므로 여기서는 기본 아이템만
        // 나열합니다.
        foreach (var stack in container.BasicStacks)
        {
            if (stack.consumed) continue;
            if (rowIndex >= maxRows) break;

            string label = $"{GetBasicItemDisplayName(stack.type)} +{stack.count}";
            cursorY = LayoutRow(rowIndex++, GetBasicItemIcon(stack.type), label, contentWidth, cursorY);
        }

        for (int i = rowIndex; i < maxRows; i++)
        {
            rowIconRects[i].gameObject.SetActive(false);
            rowTextRects[i].gameObject.SetActive(false);
        }

        float totalContentHeight = paddingTop + (-cursorY) + paddingBottom;
        float panelHeight = Mathf.Max(panelMinHeight, totalContentHeight);
        panelRect.sizeDelta = new Vector2(panelWidth, panelHeight);
        ApplyPanelBackground();
    }

    // 배경 스프라이트가 있으면 9-slice(Sliced)로 그리고(색은 흰색 = 색 변형 없음), 없으면 기존처럼
    // 단색 반투명 배경(panelColor)을 씁니다. 대입만 하는 가벼운 작업이라 매 프레임 적용합니다.
    private void ApplyPanelBackground()
    {
        if (backgroundSprite != null)
        {
            panelImage.sprite = backgroundSprite;
            panelImage.type = Image.Type.Sliced;
            panelImage.color = Color.white;
        }
        else
        {
            panelImage.sprite = null;
            panelImage.type = Image.Type.Simple;
            panelImage.color = panelColor;
        }
    }

    // 행 하나(아이콘 + 텍스트)를 배치하고, 다음 행이 시작될 cursorY를 반환합니다.
    private float LayoutRow(int index, Sprite icon, string label, float contentWidth, float cursorY)
    {
        var iconRect = rowIconRects[index];
        var iconImage = rowIcons[index];
        var textRect = rowTextRects[index];
        var text = rowTexts[index];

        iconRect.gameObject.SetActive(true);
        textRect.gameObject.SetActive(true);

        if (icon != null)
        {
            iconImage.sprite = icon;
            iconImage.color = Color.white;
        }
        else
        {
            iconImage.sprite = null;
            iconImage.color = new Color(1f, 1f, 1f, 0.15f); // 아이콘이 아직 없는 항목용 옅은 자리표시
        }

        iconRect.sizeDelta = new Vector2(rowIconSize, rowIconSize);
        iconRect.anchoredPosition = new Vector2(paddingLeft, cursorY);

        float textWidth = Mathf.Max(0f, contentWidth - rowIconSize - iconToTextSpacing);
        text.text = label;
        text.fontSize = rowFontSize;
        text.color = rowTextColor;
        textRect.sizeDelta = new Vector2(textWidth, rowIconSize);
        textRect.anchoredPosition = new Vector2(paddingLeft + rowIconSize + iconToTextSpacing, cursorY);

        return cursorY - rowIconSize - rowSpacing;
    }

    private Sprite GetBasicItemIcon(BasicItemType type)
    {
        switch (type)
        {
            case BasicItemType.Health: return healthIcon;
            case BasicItemType.Grenade: return grenadeIcon;
            case BasicItemType.HackingTool: return hackingToolIcon;
            case BasicItemType.Credit: return creditIcon;
            case BasicItemType.Pyroxenes: return pyroxenesIcon;
            case BasicItemType.Cost: return costIcon;
            default: return null;
        }
    }

    private string GetBasicItemDisplayName(BasicItemType type)
    {
        switch (type)
        {
            case BasicItemType.Health: return "현재 체력";
            case BasicItemType.Grenade: return "수류탄";
            case BasicItemType.HackingTool: return "해킹툴";
            case BasicItemType.Credit: return "크레딧";
            case BasicItemType.Pyroxenes: return "청휘석";
            case BasicItemType.Cost: return "코스트";
            default: return type.ToString();
        }
    }

    private void BuildPanel(Transform canvasRoot)
    {
        panelRoot = new GameObject("TreasureChestInfoPanel");
        panelRoot.transform.SetParent(canvasRoot, false);

        panelRect = panelRoot.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 1f);
        panelRect.anchorMax = new Vector2(0f, 1f);
        panelRect.pivot = new Vector2(0f, 1f);
        panelRect.sizeDelta = new Vector2(panelWidth, panelMinHeight);

        panelImage = panelRoot.AddComponent<Image>();
        panelImage.color = panelColor;

        for (int i = 0; i < maxRows; i++)
        {
            BuildRow(i);
        }

        panelRoot.SetActive(false); // 근처에 상자가 있을 때만 켜집니다.
    }

    private void BuildRow(int index)
    {
        var iconGO = new GameObject($"RowIcon_{index}");
        iconGO.transform.SetParent(panelRoot.transform, false);
        var iconRect = iconGO.AddComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0f, 1f);
        iconRect.anchorMax = new Vector2(0f, 1f);
        iconRect.pivot = new Vector2(0f, 1f);
        var iconImage = iconGO.AddComponent<Image>();
        iconImage.preserveAspect = true;

        var textGO = new GameObject($"RowText_{index}");
        textGO.transform.SetParent(panelRoot.transform, false);
        var textRect = textGO.AddComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0f, 1f);
        textRect.anchorMax = new Vector2(0f, 1f);
        textRect.pivot = new Vector2(0f, 1f);
        // [TMP] 옛 방식 Text는 화면 배율에 따라 흐릿하게 보여서 TextMeshPro로 교체했습니다(HUDText 참고).
        // HUDText 기본값은 클릭을 가로채지 않음(raycastTarget = false) - 예전 Text는 기본으로 켜져 있었지만 이 칸은 클릭을 받지 않습니다.
        var text = HUDText.AddTo(textGO, TextAnchor.MiddleLeft, true);

        rowIconRects.Add(iconRect);
        rowIcons.Add(iconImage);
        rowTextRects.Add(textRect);
        rowTexts.Add(text);

        iconGO.SetActive(false);
        textGO.SetActive(false);
    }
}
