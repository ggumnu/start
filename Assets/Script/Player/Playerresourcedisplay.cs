using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

// 화면 왼쪽, 초상화/체력 하트 묶음 바로 아래에 소모성 자원(수류탄/해킹툴/크레딧/청휘석)의
// 보유량을 아이콘+숫자로 표시하는 HUD입니다.
//
// [배경 패널 없음]
// PlayerHeartDisplay.cs와 동일하게, 이 UI는 별도의 배경 패널 없이 캔버스 위에 아이콘+텍스트만
// 바로 올라갑니다(체력/스탯 UI와 통일감을 맞추기 위한 결정). 처음 화면 목업의 보라색 배경/
// 분홍 오각형은 단순히 위치를 보여주기 위한 참고용이었을 뿐, 실제로는 배경이 없습니다.
//
// [PlayerResources와의 연동 - 이벤트 구독, 폴링 아님]
// PlayerResources.cs에 이미 "값이 바뀔 때만 갱신하도록 이 이벤트를 구독하면 됩니다"라는
// 주석과 함께 OnAmountChanged 이벤트가 준비되어 있습니다. 이 스크립트는 그 의도대로,
// 매 프레임 GetAmount()를 읽어오는 대신 OnAmountChanged 이벤트를 구독해서 값이 실제로
// 바뀐 순간에만 텍스트를 갱신합니다. 최초 1회(참조를 처음 찾은 시점)만 GetAmount()로
// 초기값을 채웁니다. PlayerResources.cs는 전혀 수정하지 않습니다.
//
// [레이아웃 - 매 프레임 재계산]
// 아이콘/숫자 위치 자체는 인스펙터의 간격 값(iconToNumberSpacing/groupSpacing 등)에만
// 의존하므로, 다른 HUD 스크립트들과 동일하게 매 프레임 다시 계산합니다(라이브 트윅 -
// Play 도중 값을 바꾸면 즉시 반영). 이 재계산은 가벼운 연산이라 성능 부담이 없습니다.
// 반면 "숫자 자체"는 위에서 설명한 이벤트로만 갱신됩니다.
//
// [아이콘 - 지금은 사각형 placeholder]
// 참고 화면의 오각형 모양은 나중에 실제 아이콘 스프라이트(오각형 모양이 이미 그려진
// 이미지)로 교체될 예정이라, 지금은 다른 HUD들과 동일하게 사각형 Image + 스프라이트가
// 비어있으면 옅은 자리표시 색상으로 표시합니다.
[RequireComponent(typeof(RectTransform))]
public class PlayerResourceDisplay : MonoBehaviour
{
    [Header("참조 (비워두면 자동으로 찾습니다)")]
    public PlayerHUDManager hudManager;
    public PlayerResources playerResources;

    [Header("아이콘 (비워두면 옅은 자리표시 사각형으로 표시, 나중에 UI 스프라이트로 교체)")]
    public Sprite grenadeIcon;
    public Sprite hackingToolIcon;
    public Sprite creditIcon;
    [FormerlySerializedAs("chungwiStoneIcon")]
    public Sprite pyroxenesIcon;

    [Header("위치 (하트 묶음 기준 아래쪽, Play 중에도 즉시 반영됩니다)")]
    public float marginX = 35f;
    [Tooltip("하트 묶음(초상화 높이 기준) 바로 아래로부터의 세로 간격입니다.")]
    public float gapBelowHeartRow = 15f;

    [Header("아이콘 / 숫자 크기")]
    public float iconSize = 36f;
    public int fontSize = 22;
    public Color fontColor = Color.white;
    [Tooltip("숫자 텍스트 한 칸의 너비입니다. 자릿수가 늘어나도 잘리지 않도록 넉넉하게 잡아주세요.")]
    public float numberTextWidth = 56f;
    public Color placeholderColor = new Color(1f, 1f, 1f, 0.15f);

    [Header("간격")]
    [Tooltip("아이콘과 그 옆 숫자 사이의 간격입니다.")]
    public float iconToNumberSpacing = 8f;
    [Tooltip("한 묶음(아이콘+숫자)과 다음 묶음 사이의 간격입니다. 예: 수류탄 값과 해킹툴 값 사이.")]
    public float groupSpacing = 28f;

    // 화면에 보여줄 순서는 형빈님이 지정한 수류탄 -> 해킹툴 -> 크레딧 -> 청휘석 순서입니다.
    // (Health는 PlayerResources가 관리하지 않으므로 여기 포함하지 않습니다.)
    private static readonly BasicItemType[] DisplayOrder =
    {
        BasicItemType.Grenade,
        BasicItemType.HackingTool,
        BasicItemType.Credit,
        BasicItemType.Pyroxenes
    };

    private struct Slot
    {
        public RectTransform iconRect;
        public Image icon;
        public RectTransform textRect;
        public TextMeshProUGUI text;
    }

    private GameObject root;
    private RectTransform rootRect;
    private readonly Slot[] slots = new Slot[4];
    private bool built = false;
    private bool subscribed = false;

    void Update()
    {
        if (!EnsureReferences()) return;

        EnsureBuilt();
        if (!built) return;

        ApplyRootPosition();
        Reflow();
    }

    void OnDestroy()
    {
        if (subscribed && playerResources != null)
        {
            playerResources.OnAmountChanged -= HandleAmountChanged;
        }
    }

    // hudManager / playerResources를 찾을 때까지 매 프레임 조용히 재시도합니다
    // (ItemPedestal이 PlayerInventory를 찾는 방식과 동일한 패턴). playerResources를 처음
    // 찾은 시점에 딱 한 번 이벤트를 구독합니다.
    private bool EnsureReferences()
    {
        if (hudManager == null)
        {
            hudManager = Object.FindFirstObjectByType<PlayerHUDManager>();
            if (hudManager == null || hudManager.CanvasRoot == null) return false;
        }

        if (playerResources == null)
        {
            playerResources = Object.FindFirstObjectByType<PlayerResources>();
            if (playerResources == null) return false;
        }

        if (!subscribed)
        {
            playerResources.OnAmountChanged += HandleAmountChanged;
            subscribed = true;
        }

        return true;
    }

    // OnAmountChanged 이벤트 핸들러 - 값이 실제로 바뀐 순간에만 호출됩니다.
    private void HandleAmountChanged(BasicItemType type, int newAmount)
    {
        if (!built) return;

        int index = System.Array.IndexOf(DisplayOrder, type);
        if (index < 0) return;

        slots[index].text.text = newAmount.ToString();
    }

    private void EnsureBuilt()
    {
        if (built) return;

        BuildRoot(hudManager.CanvasRoot);
        built = true;
    }

    // 하트 컨테이너 바로 아래, 좌상단 기준으로 위치를 계산합니다
    // (TreasureChestInfoDisplay.ApplyPanelPosition과 동일한 방식).
    private void ApplyRootPosition()
    {
        if (rootRect == null) return;

        rootRect.anchoredPosition = new Vector2(
            marginX,
            -(hudManager.topLeftMarginY + hudManager.portraitSize + gapBelowHeartRow));
    }

    private void BuildRoot(Transform canvasRoot)
    {
        root = new GameObject("ResourceDisplay");
        root.transform.SetParent(canvasRoot, false);

        rootRect = root.AddComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0f, 1f);
        rootRect.anchorMax = new Vector2(0f, 1f);
        rootRect.pivot = new Vector2(0f, 1f);

        for (int i = 0; i < DisplayOrder.Length; i++)
        {
            slots[i] = BuildSlot(root.transform, DisplayOrder[i]);
        }
    }

    private Slot BuildSlot(Transform parent, BasicItemType type)
    {
        var slot = new Slot();

        var iconGO = new GameObject($"Icon_{type}");
        iconGO.transform.SetParent(parent, false);
        slot.iconRect = iconGO.AddComponent<RectTransform>();
        slot.iconRect.anchorMin = new Vector2(0f, 1f);
        slot.iconRect.anchorMax = new Vector2(0f, 1f);
        slot.iconRect.pivot = new Vector2(0f, 1f);
        slot.icon = iconGO.AddComponent<Image>();
        slot.icon.preserveAspect = true;

        var textGO = new GameObject($"Count_{type}");
        textGO.transform.SetParent(parent, false);
        slot.textRect = textGO.AddComponent<RectTransform>();
        slot.textRect.anchorMin = new Vector2(0f, 1f);
        slot.textRect.anchorMax = new Vector2(0f, 1f);
        slot.textRect.pivot = new Vector2(0f, 1f);
        // [TMP] 옛 방식 Text는 화면 배율에 따라 흐릿하게 보여서 TextMeshPro로 교체했습니다(HUDText 참고).
        // 줄바꿈 없음/넘쳐도 표시/왼쪽 가운데 정렬은 예전과 같습니다.
        slot.text = HUDText.AddTo(textGO, TextAnchor.MiddleLeft);

        // 초기값은 여기서 딱 한 번 GetAmount()로 채우고, 이후로는 전부 이벤트로만 갱신합니다.
        slot.text.text = playerResources != null ? playerResources.GetAmount(type).ToString() : "0";

        return slot;
    }

    // 인스펙터 간격 값들을 매 프레임 반영합니다 (라이브 트윅). 숫자 자체는 건드리지 않고,
    // 위치/크기/아이콘 스프라이트/폰트 설정만 다시 적용합니다.
    private void Reflow()
    {
        float cursorX = 0f;

        for (int i = 0; i < DisplayOrder.Length; i++)
        {
            var slot = slots[i];

            ApplyIconSprite(slot.icon, GetIcon(DisplayOrder[i]));
            slot.iconRect.sizeDelta = new Vector2(iconSize, iconSize);
            slot.iconRect.anchoredPosition = new Vector2(cursorX, 0f);

            slot.text.fontSize = fontSize;
            slot.text.color = fontColor;
            slot.textRect.sizeDelta = new Vector2(numberTextWidth, iconSize);
            slot.textRect.anchoredPosition = new Vector2(cursorX + iconSize + iconToNumberSpacing, 0f);

            cursorX += iconSize + iconToNumberSpacing + numberTextWidth;
            if (i < DisplayOrder.Length - 1) cursorX += groupSpacing;
        }

        rootRect.sizeDelta = new Vector2(cursorX, iconSize);
    }

    private Sprite GetIcon(BasicItemType type)
    {
        switch (type)
        {
            case BasicItemType.Grenade: return grenadeIcon;
            case BasicItemType.HackingTool: return hackingToolIcon;
            case BasicItemType.Credit: return creditIcon;
            case BasicItemType.Pyroxenes: return pyroxenesIcon;
            default: return null;
        }
    }

    private void ApplyIconSprite(Image image, Sprite sprite)
    {
        if (sprite != null)
        {
            image.sprite = sprite;
            image.color = Color.white;
        }
        else
        {
            image.sprite = null;
            image.color = placeholderColor;
        }
    }
}