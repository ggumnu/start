using UnityEngine;
using UnityEngine.UI;

// 화면 왼쪽, 초상화/체력 하트 묶음 아래에 뜨는 "근처 아이템 설명칸"입니다.
// LevelTransitionConfirmUI.cs와 같은 검은 반투명 패널을 베이스로, 왼쪽 위에 아이템
// 아이콘 + 오른쪽에 이름, 그 아래에 능력치 목록, 그 아래에 특수 효과 설명, 그 아래에
// 스토리 설명을 보여줍니다.
//
// [데이터 출처 - ItemPedestal + TreasureChest 둘 다 인식]
// "플레이어 근처에 주울 수 있는 패시브 아이템이 있는지"는 두 군데에서 올 수 있습니다 -
// 동아리방 보물상자(ItemPedestal)와 일반맵 보물상자(TreasureChest, 패시브 아이템이 당첨된
// 경우만). 둘 다 같은 모양의 읽기 전용 정보(PlayerInRange + 아이템)를 공개하고 있어서,
// 두 종류를 한 후보 목록으로 합쳐 그중 플레이어와 가장 가까운 아이템 하나만 보여줍니다.
// 일반맵 보물상자가 [기본 아이템] 여러 개를 담고 있는 경우는 이 패널의 대상이 아닙니다 -
// 그건 TreasureChestInfoDisplay.cs가 별도로 담당합니다.
//
// [특수 효과 설명 - description(스토리)과 완전히 별개]
// description은 아이템의 스토리/설정 텍스트라 항상 표시되지만, specialEffectDescription은
// "이 아이템이 실제로 뭘 하는지"를 설명하는 별도 텍스트입니다. 능력치 목록과 마찬가지로
// 값이 비어있으면 이 구역 자체가 화면에서 완전히 생략됩니다(스탯형뿐인 기존 아이템은
// 화면이 전혀 바뀌지 않습니다). 보물상자 어느 쪽에서 왔든 동일하게 표시됩니다.
//
// [등급 표시 - 아이템 이름 바로 아래]
// 아이템 이름 바로 아래(아이콘 오른쪽 열)에, ItemDefinition.rarity(Tier0~Tier3)에 해당하는
// 등급 전용 스프라이트 한 장을 보여줍니다. 삼각형 개수로 등급을 나타내는 방식이 아니라,
// 등급마다 미리 준비된 스프라이트 4장 중 그 아이템의 등급에 맞는 한 장만 나타납니다.
// 이 이미지의 오른쪽 자리는 나중에 태그 UI(ItemDefinition.tags)가 붙을 자리이므로 비워둡니다.
//
// [분류 태그 - 등급 스프라이트 왼쪽, v1.34 추가]
// 등급 스프라이트 바로 왼쪽에 ItemDefinition.category를 "[패시브]" 형태로 보여줍니다.
// 텍스트의 실제 렌더링 폭을 매 프레임 계산해서 그 폭 + 간격만큼 등급 스프라이트를 오른쪽으로
// 밀어서 배치하므로, 분류 이름 길이가 서로 달라도(예: "[일회용]" vs "[음식]") 겹치지 않습니다.
//
// [패널 생성/배치 로직 분리 - v1.38]
// 아이콘/이름/능력치/특수효과/설명을 실제로 만들고 배치하는 로직은 ItemInfoPanelView.cs로
// 옮겨졌습니다(아이템 호버 툴팁(ItemTooltipDisplay.cs)도 완전히 같은 모양으로 떠야 해서,
// 그리기 로직을 공유하기 위함). 이 스크립트는 여전히 모든 스타일 값(색상/크기/폰트/오프셋
// 등)을 인스펙터 필드로 그대로 들고 있고, 매 프레임 GetStyle()로 그 값을 묶어서
// ItemInfoPanelView에 건네주기만 합니다. 그래서 인스펙터에 이미 넣어두신 값들은 전혀
// 영향받지 않고, "근처 아이템이 있으면 뜬다"는 동작도 예전과 완전히 동일합니다.
// IsShowingItem / PanelAnchoredPosition은 ItemTooltipDisplay가 액티브 아이템 호버 시
// 이 패널과 겹치지 않게 배치하기 위해 읽는 읽기 전용 값입니다.
[RequireComponent(typeof(RectTransform))]
public class PlayerItemInfoDisplay : MonoBehaviour
{
    [Header("참조 (비워두면 자동으로 찾습니다)")]
    public PlayerHUDManager hudManager;

    [Header("패널 위치 (초상화/하트 묶음 기준 아래쪽, Play 중에도 즉시 반영됩니다)")]
    public float panelMarginX = 35f;
    [Tooltip("초상화/하트 묶음 바로 아래로부터 이 패널까지의 세로 간격")]
    public float panelGapBelowHeader = 20f;

    [Header("패널 크기")]
    [Tooltip("패널의 가로 폭 (항상 고정, 세로만 늘어남)")]
    public float panelWidth = 440f;
    [Tooltip("설명이 짧아도 보장되는 최소 세로 높이")]
    public float panelMinHeight = 150f;
    [Tooltip("창 배경 스프라이트입니다. 넣으면 9-slice(Sliced)로 그려져서 모서리는 그대로 두고 " +
             "가운데만 늘어납니다(Sprite Editor에서 Border를 설정해 두세요). 비워두면 아래 Panel Color의 " +
             "단색 반투명 배경이 그대로 나옵니다.")]
    public Sprite backgroundSprite;
    [Tooltip("배경 스프라이트가 비어 있을 때만 쓰이는 단색 배경색입니다.")]
    public Color panelColor = new Color(0f, 0f, 0f, 0.82f);
    [Tooltip("배경 스프라이트가 비어 있을 때 단색 배경의 모서리 둥글기(1920x1080 기준 px)입니다. 0이면 각진 사각형입니다. " +
             "호버 툴팁도 같은 값을 씁니다.")]
    [Min(0f)] public float panelCornerRadius = 16f;
    [Tooltip("패널 안쪽 여백 (왼쪽/오른쪽/위/아래)")]
    public float paddingLeft = 16f;
    public float paddingRight = 16f;
    public float paddingTop = 14f;
    public float paddingBottom = 14f;
    [Tooltip("헤더(아이콘+이름) / 능력치 목록 / 특수효과 설명 / 스토리 설명, 구역 사이의 세로 간격")]
    public float sectionSpacing = 8f;

    [Header("아이콘 + 이름")]
    public float iconSize = 56f;
    [Tooltip("아이콘과 이름 텍스트 사이의 가로 간격")]
    public float iconToNameSpacing = 10f;
    public int nameFontSize = 22;
    [Tooltip("켜면 아이템 이름이 등급(Tier0~3)에 따라 아래 등급별 색으로 표시됩니다. 끄면 예전처럼 " +
             "모든 아이템 이름이 아래 Name Text Color 하나로 통일됩니다.")]
    public bool useTierNameColor = true;
    [Tooltip("등급별 이름 색을 쓰지 않을 때(Use Tier Name Color가 꺼져 있을 때) 모든 이름에 적용되는 색입니다.")]
    public Color nameTextColor = Color.white;
    public Color tier0NameColor = new Color(1f, 1f, 1f, 1f);
    public Color tier1NameColor = new Color(0f, 1f, 1f, 1f);
    public Color tier2NameColor = new Color(1f, 0.92156863f, 0.015686275f, 1f);
    public Color tier3NameColor = new Color(1f, 0f, 1f, 1f);

    [Header("분류 태그 (카테고리 - 등급 스프라이트 왼쪽에 표시)")]
    [Tooltip("[패시브]/[액티브]/[장신구]/[일회용]/[음식] 형태로 ItemDefinition.category를 보여줍니다. " +
             "등급 스프라이트와 같은 줄, 그 왼쪽에 배치되고, 이 텍스트의 실제 렌더링 폭만큼 등급 " +
             "스프라이트가 오른쪽으로 밀려서 배치됩니다.")]
    public int categoryFontSize = 18;
    public Color categoryTextColor = new Color(0.85f, 0.85f, 0.85f, 1f);
    [Tooltip("분류 태그 텍스트 오른쪽 끝에서 등급 스프라이트 왼쪽 끝까지의 가로 간격입니다.")]
    public float categoryToTierSpacing = 6f;

    [Header("등급 표시 (이름 바로 아래 - 등급별 전용 스프라이트 한 장이 나타남)")]
    [Tooltip("Tier0 아이템일 때 나타나는 스프라이트입니다. 비워두면 옅은 자리표시 사각형으로 보입니다.")]
    public Sprite tier0Sprite;
    [Tooltip("Tier1 아이템일 때 나타나는 스프라이트입니다.")]
    public Sprite tier1Sprite;
    [Tooltip("Tier2 아이템일 때 나타나는 스프라이트입니다.")]
    public Sprite tier2Sprite;
    [Tooltip("Tier3 아이템일 때 나타나는 스프라이트입니다.")]
    public Sprite tier3Sprite;
    [Tooltip("등급 스프라이트가 그려지는 칸의 크기입니다(가로, 세로). 스프라이트의 비율은 유지됩니다.")]
    public Vector2 tierIconSize = new Vector2(36f, 36f);
    [Tooltip("아이템 이름 글자 아래쪽 끝에서 등급 스프라이트 위쪽 끝까지의 세로 간격입니다.")]
    public float tierGapBelowName = 4f;
    [Tooltip("해당 등급의 스프라이트가 비어있을 때 대신 보이는 옅은 자리표시 색입니다.")]
    public Color tierPlaceholderColor = new Color(1f, 1f, 1f, 0.15f);

    [Header("능력치 목록")]
    [Tooltip("색은 별도 필드 없이 위 이름 색과 항상 동일하게(등급별로) 표시됩니다.")]
    public int statFontSize = 18;

    [Header("특수 효과 설명 (스탯이 아닌 능력)")]
    [Tooltip("색은 별도 필드 없이 위 이름 색과 항상 동일하게(등급별로) 표시됩니다.")]
    public int specialEffectFontSize = 18;

    [Header("스토리 설명 (description - 아이템의 설정/배경 이야기)")]
    public int descriptionFontSize = 16;
    public Color descriptionTextColor = new Color(0.85f, 0.85f, 0.85f, 1f);

    [Header("개별 위치 조절 (자동 배치된 위치에서 추가로 밀어서 조정)")]
    [Tooltip("x는 오른쪽(+)/왼쪽(-), y는 위(+)/아래(-)로 이동합니다. 0,0이면 자동 배치된 " +
             "원래 위치 그대로입니다. 너무 크게 밀면 다른 요소와 겹칠 수 있으니 참고하세요.")]
    public Vector2 iconOffset = Vector2.zero;
    public Vector2 nameOffset = Vector2.zero;
    public Vector2 categoryOffset = Vector2.zero;
    public Vector2 tierOffset = Vector2.zero;
    public Vector2 statOffset = Vector2.zero;
    public Vector2 specialEffectOffset = Vector2.zero;
    public Vector2 descriptionOffset = Vector2.zero;

    private readonly ItemInfoPanelView view = new ItemInfoPanelView();

    private Transform playerTransform;
    private ItemDefinition currentDisplayedItem;
    private bool built = false;

    // ItemTooltipDisplay(v1.38)가 액티브 아이템 호버 툴팁을 이 패널과 겹치지 않게 배치할 때
    // 읽는 읽기 전용 값들입니다. 이 스크립트 내부 로직에는 영향을 주지 않습니다.
    public bool IsShowingItem => view.IsActive;
    public Vector2 PanelAnchoredPosition => view.PanelRect != null ? view.PanelRect.anchoredPosition : Vector2.zero;

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

        ItemDefinition nearestItem = FindNearestAvailableItem();

        if (nearestItem == null)
        {
            HidePanel();
        }
        else if (nearestItem != currentDisplayedItem)
        {
            currentDisplayedItem = nearestItem;
            view.ShowItem(nearestItem, GetStyle());
        }
        else if (!view.IsActive)
        {
            view.PanelRoot.SetActive(true);
        }

        // 패널이 떠 있을 때만 의미가 있지만, 값 조절을 Play 중 바로 확인할 수 있도록
        // 매 프레임 다시 계산합니다(패널이 꺼져 있으면 다음에 켜질 때 바로 정확한 배치로
        // 나타나도록 하는 역할도 합니다).
        if (view.IsActive)
        {
            view.BringToFront();
            view.Reflow(GetStyle());
        }
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

        view.Build(canvasRoot, "ItemInfoPanel");
        built = true;
    }

    // 씬의 모든 ItemPedestal(동아리방 보물상자)과 TreasureChest(일반맵 보물상자 - 패시브 아이템이
    // 배정된 경우만) 중, "지금 플레이어가 상호작용 범위 안에 있는" 것들을 전부 한 후보
    // 목록으로 합쳐서 그중 플레이어와 가장 가까운 아이템 하나를 고릅니다. 두 종류 모두
    // 씬 하나 기준으로 개수가 몇 개 안 되기 때문에 매 프레임 전체 탐색해도 부담이 없습니다.
    private ItemDefinition FindNearestAvailableItem()
    {
        // [F 대상 공용 판정] 설명칸은 지금 F가 먹는 대상(InteractionTargetSelector)의 아이템만 보여줍니다 - F 안내/
        // 실제 F 대상과 항상 일치. 대상이 포탈이거나, 아직 열지 않았거나, 아이템 없이 기본 아이템만 든 상자면 null.
        var target = InteractionTargetSelector.Current;
        if (target is ItemPedestal pedestal) return pedestal.AssignedItem;
        if (target is TreasureChest chest) return chest.PassiveItem;
        if (target is HoldingPedestal holding) return holding.HeldItem; // 보관 좌대(상점 등)에 올려진 아이템
        return null;
    }

    private void HidePanel()
    {
        currentDisplayedItem = null;
        view.Hide();
    }

    // 패널 위치를 매 프레임 다시 계산합니다 (2단계 하트 위치 조절과 동일하게, Play 중에
    // 인스펙터 값을 바꾸면 바로 반영되도록).
    private void ApplyPanelPosition()
    {
        if (view.PanelRect == null || hudManager == null) return;

        view.PanelRect.anchoredPosition = new Vector2(
            panelMarginX,
            -(hudManager.topLeftMarginY + hudManager.portraitSize + panelGapBelowHeader));
    }

    // 지금 인스펙터 값들을 ItemInfoPanelView가 그림/배치에 쓸 수 있는 묶음으로 반환합니다.
    // ItemTooltipDisplay도 이 메서드를 매 프레임 그대로 호출해서 쓰기 때문에, 여기 값을
    // 하나 바꾸면 원래 설명칸과 호버 툴팁 둘 다 동시에 바뀝니다.
    public ItemInfoPanelStyle GetStyle()
    {
        return new ItemInfoPanelStyle
        {
            panelWidth = panelWidth,
            panelMinHeight = panelMinHeight,
            backgroundSprite = backgroundSprite,
            panelColor = panelColor,
            panelCornerRadius = panelCornerRadius,
            paddingLeft = paddingLeft,
            paddingRight = paddingRight,
            paddingTop = paddingTop,
            paddingBottom = paddingBottom,
            sectionSpacing = sectionSpacing,

            iconSize = iconSize,
            iconToNameSpacing = iconToNameSpacing,
            nameFontSize = nameFontSize,
            useTierNameColor = useTierNameColor,
            nameTextColor = nameTextColor,
            tier0NameColor = tier0NameColor,
            tier1NameColor = tier1NameColor,
            tier2NameColor = tier2NameColor,
            tier3NameColor = tier3NameColor,

            categoryFontSize = categoryFontSize,
            categoryTextColor = categoryTextColor,
            categoryToTierSpacing = categoryToTierSpacing,

            tier0Sprite = tier0Sprite,
            tier1Sprite = tier1Sprite,
            tier2Sprite = tier2Sprite,
            tier3Sprite = tier3Sprite,
            tierIconSize = tierIconSize,
            tierGapBelowName = tierGapBelowName,
            tierPlaceholderColor = tierPlaceholderColor,

            statFontSize = statFontSize,

            specialEffectFontSize = specialEffectFontSize,

            descriptionFontSize = descriptionFontSize,
            descriptionTextColor = descriptionTextColor,

            iconOffset = iconOffset,
            nameOffset = nameOffset,
            categoryOffset = categoryOffset,
            tierOffset = tierOffset,
            statOffset = statOffset,
            specialEffectOffset = specialEffectOffset,
            descriptionOffset = descriptionOffset,
        };
    }
}
