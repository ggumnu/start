using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 화면 오른쪽 중앙에 뜨는 "패시브 아이템 인벤토리" HUD입니다. 소모성 자원(PlayerResources)과는
// 완전히 별개로, PlayerInventory.OwnedItems(클럽/보물상자 등에서 얻은 패시브 아이템 목록)를
// 아이콘 그리드로 보여줍니다.
//
// [배경 - Background Sprite 칸에 UI 스프라이트를 넣으면 그 그림이 배경이 됩니다]
// 칸을 비워두면 검은 반투명 Image(Panel Color)로 대체 표시합니다.
//
// [정렬 순서 - 최신 아이템이 맨 위 왼쪽부터]
// PlayerInventory.OwnedItems는 "획득한 순서 그대로"(가장 먼저 얻은 아이템이 0번) 쌓이는
// 리스트입니다. 이 UI는 그 반대 순서로 보여줍니다 - 가장 최근에 얻은 아이템이 맨 윗줄
// 왼쪽부터 채워지고, 오래된 아이템일수록 점점 뒤(아래쪽) 칸으로 밀려납니다. 한 줄에 5개씩
// (columns 필드로 조절 가능) 배치합니다.
//
// [PlayerInventory.cs는 전혀 수정하지 않음 - 변경 감지 방식]
// 새 아이템 획득을 감지하기 위해 이벤트를 새로 추가하는 대신(PlayerInventory.cs 비수정
// 원칙), 매 프레임 OwnedItems 리스트 전체(개수 + 각 원소)를 이전 프레임에 저장해둔
// 스냅샷과 비교합니다. 단순히 Count만 비교하지 않는 이유는, 나중에 "아이템을 잃는" 기능이
// 추가되면 같은 프레임에 하나가 빠지고 하나가 새로 들어와 Count는 그대로인데 내용물은
// 바뀌는 경우가 생길 수 있기 때문입니다 - 그런 경우도 놓치지 않도록 리스트 내용 자체를
// 비교합니다. 아이템 개수가 수십~수백 개 수준이라 이 비교는 매 프레임 해도 성능 부담이
// 없습니다. 이 방식은 PlayerInventory.cs가 나중에 어떻게 바뀌어도(제거 기능이 추가되고,
// 그 코드가 혹시 이벤트 호출을 깜빡하더라도) OwnedItems 원본 데이터에서 직접 다시
// 읽어오기 때문에 항상 정확합니다.
//
// [v1 버그 수정 - 그리드 레이아웃이 Play 중 인스펙터 값에 반응하지 않던 문제]
// 처음 버전은 "어떤 아이템을 어느 칸에 넣을지(스프라이트 할당)"와 "각 칸의 위치/크기
// (레이아웃)"를 한 함수(RebuildGrid)에 묶어놨는데, 이 함수 자체가 인벤토리 내용이 실제로
// 바뀔 때만 호출되고 있어서 iconSize/iconSpacingX/iconSpacingY를 Play 중 바꿔도 내용물이
// 안 바뀌면 반영되지 않는 문제가 있었습니다. 이제 두 역할을 분리했습니다 - 스프라이트
// 할당(AssignIcons)은 여전히 내용이 바뀔 때만, 칸 위치/크기 계산(ApplyGridLayout)은 다른
// HUD 스크립트들과 동일하게 매 프레임 다시 계산합니다. 그래서 이제 인스펙터 값을 Play
// 도중 바꾸면 즉시 반영됩니다.
//
// [넘칠 때 - 스크롤]
// 패널 크기(가로/세로)는 고정하고, 아이템이 많아져서 그리드가 패널보다 커지면 세로
// 스크롤(ScrollRect)로 넘어갑니다. 아이콘 크기 자체는 줄어들지 않습니다.
//
// [주의 - 씬에 EventSystem 필요]
// 마우스 휠/드래그로 스크롤하려면 씬에 EventSystem이 하나 있어야 합니다(Unity UI 표준
// 요구사항). 이 프로젝트에 아직 마우스 상호작용 UI가 없었다면, GameObject -> UI ->
// Event System으로 하나 만들어 두세요. 없어도 컴파일/실행 자체는 되지만 스크롤 입력만
// 동작하지 않습니다.
//
// [접어보기]
// 패널 왼쪽 세로 가운데의 버튼(또는 PlayerHUDManager의 전체 접기 키, 기본 Tab)으로 패널을 화면 오른쪽 밖으로
// 접었다 펼 수 있습니다. 버튼 생성/애니메이션/클릭 판정은 공용 클래스 CollapsibleHUDPanel이 맡고, 이 스크립트는
// 원래 위치를 넘겨주면 Tick이 접힘 이동량을 더한 최종 위치까지 정합니다(ApplyCollapse). 버튼 모양/시간/커브는 인스펙터의
// "접어보기" 묶음에서 조절합니다.
[RequireComponent(typeof(RectTransform))]
public class PlayerInventoryDisplay : MonoBehaviour
{
    [Header("참조 (비워두면 자동으로 찾습니다)")]
    public PlayerHUDManager hudManager;
    public PlayerInventory playerInventory;

    [Header("패널 위치 (화면 우측 중앙 기준 여백, px)")]
    public float marginX = 40f;
    [Tooltip("세로 중앙 기준 상하 미세 조정값입니다. 0이면 정확히 화면 세로 중앙.")]
    public float verticalOffset = 0f;

    [Header("패널 크기 / 배경 (스프라이트를 비워두면 검은 반투명)")]
    public float panelWidth = 320f;
    public float panelHeight = 600f;
    [Tooltip("배경으로 쓸 UI 스프라이트입니다. 비워두면 아래 Panel Color(검은 반투명)로 대체 표시됩니다.")]
    public Sprite backgroundSprite;
    public Color panelColor = new Color(0f, 0f, 0f, 0.6f);
    public float paddingLeft = 12f;
    public float paddingRight = 12f;
    public float paddingTop = 12f;
    public float paddingBottom = 12f;

    [Header("아이콘 그리드 (한 줄에 5개, 최신 아이템이 맨 위 왼쪽부터 - Play 중 값을 바꾸면 즉시 반영됩니다)")]
    [Min(1)] public int columns = 5;
    public float iconSize = 64f;
    public float iconSpacingX = 8f;
    public float iconSpacingY = 8f;
    [Tooltip("아이템의 ItemDefinition.icon이 비어있을 때 대신 보여줄 자리표시 색상입니다 " +
             "(옅은 반투명 흰색 사각형). 아이콘이 이미 다 채워져 있다면 보일 일이 없습니다.")]
    public Color placeholderColor = new Color(1f, 1f, 1f, 0.15f);

    [Header("접어보기 (패널 왼쪽 버튼 - 누르면 화면 오른쪽으로 접힘)")]
    public CollapsibleHUDSettings collapse = new CollapsibleHUDSettings();

    private GameObject panelRoot;
    private RectTransform panelRect;
    private Image panelImage;
    private RectTransform viewportRect;
    private RectTransform contentRect;
    private ScrollRect scrollRect;

    private readonly List<RectTransform> iconRects = new List<RectTransform>();
    private readonly List<Image> iconImages = new List<Image>();
    // 칸 순서대로(iconRects/iconImages와 같은 인덱스) 어떤 아이템이 들어있는지 기록해둡니다.
    // TryGetHoveredItem()이 "마우스가 올라간 칸이 어떤 아이템인지" 돌려줄 때 씁니다.
    private readonly List<ItemDefinition> iconItems = new List<ItemDefinition>();
    private readonly List<ItemDefinition> lastSnapshot = new List<ItemDefinition>();

    // 지금 실제로 보여줘야 하는 아이템 개수입니다. AssignIcons()가 내용 바뀔 때만 갱신하고,
    // ApplyGridLayout()은 매 프레임 이 값을 기준으로 몇 칸을 활성화할지만 결정합니다.
    private int activeCount = 0;

    private bool built = false;
    private CollapsibleHUDPanel collapser;

    void OnDestroy()
    {
        if (collapser != null) collapser.Dispose();
    }

    // [접어보기] 원래 위치(화면 우측 여백)를 넘겨 접힘 이동량(오른쪽 방향)이 더해진 위치로 둡니다.
    private void ApplyCollapse()
    {
        if (collapser == null) collapser = new CollapsibleHUDPanel("InventoryPanel", HUDCollapseDirection.Right);
        collapser.Tick(collapse, panelRect, new Vector2(panelWidth, panelHeight), marginX, new Vector2(-marginX, verticalOffset));
    }

    void Update()
    {
        if (!EnsureReferences()) return;

        EnsureBuilt();
        if (!built) return;

        ApplyPanelPosition();
        ApplyCollapse();

        if (HasInventoryChanged())
        {
            AssignIcons(playerInventory.OwnedItems);
        }

        // 내용이 그대로여도 iconSize/spacing/columns 같은 인스펙터 값은 매 프레임
        // 다시 반영합니다 (라이브 트윅).
        ApplyGridLayout();
    }

    private bool EnsureReferences()
    {
        if (hudManager == null)
        {
            hudManager = Object.FindFirstObjectByType<PlayerHUDManager>();
            if (hudManager == null || hudManager.CanvasRoot == null) return false;
        }

        if (playerInventory == null)
        {
            playerInventory = Object.FindFirstObjectByType<PlayerInventory>();
            if (playerInventory == null) return false;
        }

        return true;
    }

    private void EnsureBuilt()
    {
        if (built) return;

        BuildPanel(hudManager.CanvasRoot);
        built = true;
    }

    // OwnedItems가 실제로 바뀌었는지(개수 또는 순서/내용) 확인합니다. 개수만 비교하지
    // 않는 이유는 클래스 상단 주석의 [변경 감지 방식] 참고.
    private bool HasInventoryChanged()
    {
        var current = playerInventory.OwnedItems;

        bool changed = current.Count != lastSnapshot.Count;
        if (!changed)
        {
            for (int i = 0; i < current.Count; i++)
            {
                if (!ReferenceEquals(current[i], lastSnapshot[i]))
                {
                    changed = true;
                    break;
                }
            }
        }

        if (changed)
        {
            lastSnapshot.Clear();
            lastSnapshot.AddRange(current);
        }

        return changed;
    }

    private void ApplyPanelPosition()
    {
        panelRect.anchorMin = new Vector2(1f, 0.5f);
        panelRect.anchorMax = new Vector2(1f, 0.5f);
        panelRect.pivot = new Vector2(1f, 0.5f);
        panelRect.anchoredPosition = new Vector2(-marginX, verticalOffset);
        panelRect.sizeDelta = new Vector2(panelWidth, panelHeight);

        if (backgroundSprite != null)
        {
            panelImage.sprite = backgroundSprite;
            panelImage.color = Color.white;
        }
        else
        {
            panelImage.sprite = null;
            panelImage.color = panelColor;
        }

        viewportRect.offsetMin = new Vector2(paddingLeft, paddingBottom);
        viewportRect.offsetMax = new Vector2(-paddingRight, -paddingTop);
    }

    // 최신 아이템이 맨 위 왼쪽부터 오도록 OwnedItems를 역순으로 순회하며 슬롯마다
    // 스프라이트를 할당합니다. 위치/크기는 여기서 정하지 않고 ApplyGridLayout()이 매
    // 프레임 따로 계산합니다 - 그래야 내용이 그대로여도 iconSize 등을 바꾸면 즉시 반영됩니다.
    private void AssignIcons(IReadOnlyList<ItemDefinition> ownedItems)
    {
        int count = ownedItems.Count;
        EnsureSlotCount(count);

        for (int i = 0; i < count; i++)
        {
            var item = ownedItems[count - 1 - i]; // 역순 - 가장 최근 획득이 i=0
            var iconImage = iconImages[i];
            iconItems[i] = item;

            if (item != null && item.icon != null)
            {
                iconImage.sprite = item.icon;
                iconImage.color = Color.white;
            }
            else
            {
                iconImage.sprite = null;
                iconImage.color = placeholderColor;
            }
        }

        activeCount = count;
    }

    // ItemTooltipDisplay(v1.38)가 "지금 마우스가 이 인벤토리의 어떤 아이템 아이콘 위에
    // 있는지" 확인할 때 씁니다. 이 프로젝트의 다른 HUD와 마찬가지로 유니티 이벤트 시스템
    // (EventSystem/PointerEnter) 대신, 매 프레임 마우스 화면 좌표와 활성화된 칸의 사각형을
    // 직접 비교합니다. 읽기 전용이며 이 스크립트의 내부 로직에는 영향을 주지 않습니다.
    public bool TryGetHoveredItem(out ItemDefinition item, out RectTransform iconRect)
    {
        for (int i = 0; i < activeCount; i++)
        {
            if (iconItems[i] == null) continue;

            if (RectTransformUtility.RectangleContainsScreenPoint(iconRects[i], Input.mousePosition, null))
            {
                item = iconItems[i];
                iconRect = iconRects[i];
                return true;
            }
        }

        item = null;
        iconRect = null;
        return false;
    }

    // 매 프레임 호출됩니다 - activeCount(몇 칸을 보여줄지)는 내용이 바뀔 때만 갱신되지만,
    // 각 칸의 실제 위치/크기는 항상 지금 인스펙터 값(iconSize/iconSpacingX/iconSpacingY/
    // columns/placeholderColor)을 기준으로 다시 계산합니다.
    private void ApplyGridLayout()
    {
        for (int i = 0; i < iconRects.Count; i++)
        {
            bool active = i < activeCount;
            if (iconRects[i].gameObject.activeSelf != active)
            {
                iconRects[i].gameObject.SetActive(active);
            }
            if (!active) continue;

            int col = i % columns;
            int row = i / columns;

            iconRects[i].sizeDelta = new Vector2(iconSize, iconSize);
            iconRects[i].anchoredPosition = new Vector2(
                col * (iconSize + iconSpacingX),
                -(row * (iconSize + iconSpacingY)));
        }

        int rowCount = activeCount > 0 ? Mathf.CeilToInt(activeCount / (float)columns) : 0;
        float contentHeight = rowCount > 0
            ? rowCount * iconSize + Mathf.Max(0, rowCount - 1) * iconSpacingY
            : 0f;
        contentRect.sizeDelta = new Vector2(contentRect.sizeDelta.x, contentHeight);
    }

    // 아이콘 슬롯 풀을 필요한 개수만큼 늘립니다 (PlayerHeartDisplay.EnsureSlotCount와 동일한
    // 패턴). 지금은 아이템이 줄어들 일이 없지만, 나중에 생기더라도 ApplyGridLayout의 비활성화
    // 처리로 안전하게 대응됩니다.
    private void EnsureSlotCount(int count)
    {
        while (iconRects.Count < count)
        {
            var go = new GameObject($"ItemIcon_{iconRects.Count}");
            go.transform.SetParent(contentRect, false);

            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);

            var image = go.AddComponent<Image>();
            image.preserveAspect = true;

            iconRects.Add(rect);
            iconImages.Add(image);
            iconItems.Add(null);
        }
    }

    private void BuildPanel(Transform canvasRoot)
    {
        panelRoot = new GameObject("PassiveItemInventoryPanel");
        panelRoot.transform.SetParent(canvasRoot, false);

        panelRect = panelRoot.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1f, 0.5f);
        panelRect.anchorMax = new Vector2(1f, 0.5f);
        panelRect.pivot = new Vector2(1f, 0.5f);
        panelRect.sizeDelta = new Vector2(panelWidth, panelHeight);

        panelImage = panelRoot.AddComponent<Image>();
        panelImage.color = panelColor;

        var viewportGO = new GameObject("Viewport");
        viewportGO.transform.SetParent(panelRoot.transform, false);
        viewportRect = viewportGO.AddComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = new Vector2(paddingLeft, paddingBottom);
        viewportRect.offsetMax = new Vector2(-paddingRight, -paddingTop);
        viewportGO.AddComponent<RectMask2D>();

        var contentGO = new GameObject("Content");
        contentGO.transform.SetParent(viewportGO.transform, false);
        contentRect = contentGO.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(0f, 1f);
        contentRect.pivot = new Vector2(0f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0f, 0f);

        scrollRect = panelRoot.AddComponent<ScrollRect>();
        scrollRect.viewport = viewportRect;
        scrollRect.content = contentRect;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 20f;
    }
}