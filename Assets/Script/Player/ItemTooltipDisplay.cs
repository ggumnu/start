using UnityEngine;
using UnityEngine.UI;

// 인벤토리에 뜨는 아이템 아이콘(패시브/장신구/일회용/음식/액티브)에 커서를 올리면 그 아이템의
// 설명칸을 다시 보여주는 호버 툴팁입니다(v1.38).
//
// [모양 - 원래 설명칸(PlayerItemInfoDisplay)과 완전히 동일]
// 실제로 패널을 만들고 채우고 배치하는 로직은 ItemInfoPanelView.cs를 그대로 씁니다.
// PlayerItemInfoDisplay.GetStyle()이 반환하는 "지금 이 순간의 스타일 값"을 매 프레임 그대로
// 받아서 쓰기 때문에, 원래 설명칸의 색상/크기/폰트/등급 스프라이트 등을 나중에 손보면 이
// 툴팁도 자동으로 같이 바뀝니다.
//
// [호버 판정 - 매 프레임 좌표 비교]
// 이 프로젝트의 다른 HUD와 마찬가지로 유니티 이벤트 시스템(EventSystem/PointerEnter) 대신,
// PlayerInventoryDisplay.TryGetHoveredItem() / PlayerSlotItemDisplay.TryGetHoveredSlotItem() /
// PlayerCostDisplay.TryGetHoveredActiveItem()을 매 프레임 확인해서 "지금 마우스가 어떤
// 아이템 위에 있는지"를 찾습니다.
//
// [상점 카드]
// 상점 창(ShopUI)이 열려 있으면 플레이어 아이템 카드 호버도 같은 설명칸을 커서 위에 띄웁니다(ShopUI.TryGetHoveredItem).
//
// [위치 - 종류에 따라 두 가지 방식]
// - 패시브 / 장신구 / 일회용 / 음식: 커서 위쪽에 중앙 정렬로 뜨되, 패널 크기(가변 높이 포함)를
//   반영해서 화면 밖으로 나가지 않도록 항상 화면 안쪽으로 clamp합니다. 오른쪽 끝의 패시브
//   아이템이나 음식처럼 화면 가장자리에 있는 아이콘에 커서를 올려도 패널 전체가 항상
//   화면 안에 보입니다.
// - 액티브: 커서를 따라가지 않고, PlayerItemInfoDisplay가 원래 뜨는 자리(초상화 아래,
//   PlayerItemInfoDisplay.PanelAnchoredPosition)에 그대로 나타납니다. 단, 그 순간 근처 상자
//   때문에 PlayerItemInfoDisplay(패시브 아이템) 또는 TreasureChestInfoDisplay(기본 아이템
//   묶음) 패널이 이미 떠 있으면, 겹치지 않도록 그 패널 폭만큼 오른쪽으로 밀려서 나란히
//   나타납니다(비교용). 두 패널은 한 상자에 동시에 뜨는 일이 없어서 어느 쪽이 떠 있는지만
//   확인하면 됩니다.
//
// [정렬 순서]
// 전용 Canvas를 하나 더 만들고 sortingOrder를 다른 HUD보다 높게 둬서(기본 20 - F 안내
// InteractPromptDisplay의 10, 장전 표시 PlayerReloadIndicator의 5보다 위) 항상 맨 위에
// 그려지도록 했습니다. 호버 툴팁은 원래 무엇에도 가려지면 안 되는 요소라 이렇게 뒀습니다.
//
// [화면 해상도 대응 - PlayerHUDManager와 항상 같은 기준을 씀]
// 이 컴포넌트의 Canvas는 PlayerHUDManager.CanvasRoot와는 다른 별도 Canvas지만, 액티브
// 아이템 호버 시 PlayerItemInfoDisplay.PanelAnchoredPosition(다른 Canvas 위의 값)을 그대로
// 가져다 쓰기 때문에, 두 Canvas의 기준 해상도가 어긋나면 위치가 틀어집니다. 그래서 자체
// referenceResolution 필드를 따로 두지 않고, 매 프레임 hudManager.referenceResolution을
// 그대로 읽어 씁니다 - PlayerHUDManager 쪽 기준 해상도를 나중에 바꾸더라도 항상 같이
// 맞춰집니다.
public class ItemTooltipDisplay : MonoBehaviour
{
    [Header("참조 (비워두면 자동으로 찾습니다)")]
    public PlayerHUDManager hudManager;
    public PlayerItemInfoDisplay playerItemInfoDisplay;
    public TreasureChestInfoDisplay treasureChestInfoDisplay;
    public PlayerInventoryDisplay playerInventoryDisplay;
    public PlayerSlotItemDisplay playerSlotItemDisplay;
    public PlayerCostDisplay playerCostDisplay;

    [Header("커서 추적 위치 (패시브 / 장신구 / 일회용 / 음식 호버)")]
    [Tooltip("커서 기준 오프셋입니다(1920x1080 기준 픽셀). x는 좌우로 밀 때, y는 커서보다 " +
             "얼마나 위에 띄울지입니다. 기본값은 커서 바로 위 중앙 정렬입니다.")]
    public Vector2 cursorOffset = new Vector2(0f, 24f);
    [Tooltip("화면 가장자리와 패널 사이 최소 여백입니다(1920x1080 기준 픽셀). 패널이 항상 이 " +
             "여백 안쪽에 머무르도록 위치를 조정합니다.")]
    public float screenEdgeMargin = 16f;

    [Header("고정 위치 (액티브 아이템 호버 - 원래 아이템 설명칸과 같은 자리)")]
    [Tooltip("근처 상자 설명칸(PlayerItemInfoDisplay)이 이미 떠 있을 때, 겹치지 않도록 그 " +
             "오른쪽으로 미는 간격입니다(1920x1080 기준 픽셀).")]
    public float activeItemOverlapGap = 20f;

    [Header("정렬 (일반적으로 건드릴 필요 없음)")]
    [Tooltip("다른 HUD보다 항상 위에 그려지도록 하는 정렬 순서입니다. F 안내(기본 10)/장전 " +
             "표시(기본 5)보다 높게 유지하세요.")]
    public int canvasSortingOrder = 20;

    private readonly ItemInfoPanelView view = new ItemInfoPanelView();

    private Canvas canvas;
    private RectTransform canvasRect;
    private CanvasScaler canvasScaler;
    private bool built = false;

    // 씬에 아직 없는 컴포넌트를 찾느라 매 프레임 씬 전체를 뒤지지 않도록 재시도 간격을 둡니다
    // (PlayerSlotItemDisplay.cs / PlayerCostDisplay.cs와 동일한 패턴).
    private const float RefSearchInterval = 0.5f;
    private float refSearchTimer = 0f;

    void Update()
    {
        if (!EnsureReferences()) return;

        EnsureBuilt();
        if (!built) return;

        UpdateCanvasScalerMatch();

        // [게임 중단 화면] 떠 있는 동안에는 아무 설명칸도 띄우지 않습니다.
        if (PauseMenuUI.IsOpen)
        {
            view.Hide();
            return;
        }

        // [상점 카드 호버] 상점 창(ShopUI)의 플레이어 아이템 카드 위면 커서 위에 설명칸을 띄웁니다. 상점 창 Canvas가 이 툴팁보다
        // 정렬 순서가 높아서, 이때만 상점 창 바로 위로 올립니다(슬롯 교체 확인창/게임 오버 창보다는 아래).
        if (ShopUI.TryGetHoveredItem(out ItemDefinition shopItem))
        {
            canvas.sortingOrder = Mathf.Max(canvasSortingOrder, ShopUI.SortingOrder + 1);
            ShowAtCursor(shopItem);
            return;
        }
        // [슬롯 아이템 버리기 창] 창이 떠 있으면 창 안의 아이템 아이콘 호버만 설명칸을 띄웁니다(창 위로 올림). 창 뒤의 HUD
        // 아이콘 호버는 무시합니다.
        // [패시브 교체 창 - 56번] 창이 떠 있으면 창 안의 아이템 아이콘 호버만 설명칸을 띄웁니다(창 위로 올림).
        if (PassiveRerollUI.IsOpen)
        {
            if (PassiveRerollUI.TryGetHoveredItem(out ItemDefinition rerollItem))
            {
                canvas.sortingOrder = Mathf.Max(canvasSortingOrder, PassiveRerollUI.SortingOrder + 1);
                ShowAtCursor(rerollItem);
                return;
            }
            canvas.sortingOrder = canvasSortingOrder;
            view.Hide();
            return;
        }

        if (SlotItemDropUI.IsOpen)
        {
            if (SlotItemDropUI.TryGetHoveredItem(out ItemDefinition dropItem))
            {
                canvas.sortingOrder = Mathf.Max(canvasSortingOrder, SlotItemDropUI.SortingOrder + 1);
                ShowAtCursor(dropItem);
                return;
            }
            canvas.sortingOrder = canvasSortingOrder;
            view.Hide();
            return;
        }

        canvas.sortingOrder = canvasSortingOrder;

        if (playerCostDisplay != null &&
            playerCostDisplay.TryGetHoveredActiveItem(out ItemDefinition activeItem, out _))
        {
            ShowAtActiveSlot(activeItem);
            return;
        }

        if (TryGetCursorHoverItem(out ItemDefinition cursorItem))
        {
            ShowAtCursor(cursorItem);
            return;
        }

        view.Hide();
    }

    // ------------------------------------------------------------------
    // 참조
    // ------------------------------------------------------------------

    // hudManager와 playerItemInfoDisplay는 필수입니다(스타일 값의 원본이자, 액티브 아이템
    // 고정 위치를 계산하는 기준이라). 나머지 셋(패시브/슬롯/액티브 인벤토리)은 없어도 그
    // 종류의 호버만 동작하지 않을 뿐 나머지는 정상 동작하므로 필수로 두지 않았습니다.
    private bool EnsureReferences()
    {
        bool needSearch = hudManager == null || hudManager.CanvasRoot == null ||
                          playerItemInfoDisplay == null || treasureChestInfoDisplay == null ||
                          playerInventoryDisplay == null ||
                          playerSlotItemDisplay == null || playerCostDisplay == null;

        if (needSearch)
        {
            refSearchTimer -= Time.unscaledDeltaTime;
            if (refSearchTimer <= 0f)
            {
                refSearchTimer = RefSearchInterval;

                if (hudManager == null)
                    hudManager = Object.FindFirstObjectByType<PlayerHUDManager>();
                if (playerItemInfoDisplay == null)
                    playerItemInfoDisplay = Object.FindFirstObjectByType<PlayerItemInfoDisplay>();
                if (treasureChestInfoDisplay == null)
                    treasureChestInfoDisplay = Object.FindFirstObjectByType<TreasureChestInfoDisplay>();
                if (playerInventoryDisplay == null)
                    playerInventoryDisplay = Object.FindFirstObjectByType<PlayerInventoryDisplay>();
                if (playerSlotItemDisplay == null)
                    playerSlotItemDisplay = Object.FindFirstObjectByType<PlayerSlotItemDisplay>();
                if (playerCostDisplay == null)
                    playerCostDisplay = Object.FindFirstObjectByType<PlayerCostDisplay>();
            }
        }

        return hudManager != null && hudManager.CanvasRoot != null && playerItemInfoDisplay != null;
    }

    private bool TryGetCursorHoverItem(out ItemDefinition item)
    {
        if (playerInventoryDisplay != null && playerInventoryDisplay.TryGetHoveredItem(out item, out _))
            return true;

        if (playerSlotItemDisplay != null && playerSlotItemDisplay.TryGetHoveredSlotItem(out item, out _))
            return true;

        item = null;
        return false;
    }

    // ------------------------------------------------------------------
    // 오브젝트 생성 (한 번만)
    // ------------------------------------------------------------------

    private void EnsureBuilt()
    {
        if (built) return;

        var canvasGO = new GameObject("ItemTooltipCanvas");
        canvasGO.transform.SetParent(transform, false);
        canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = canvasSortingOrder;

        canvasScaler = canvasGO.AddComponent<CanvasScaler>();
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;

        canvasRect = canvasGO.GetComponent<RectTransform>();

        view.Build(canvasGO.transform, "ItemTooltipPanel");

        // 모든 시각 요소는 클릭/스크롤 등 다른 UI 입력을 가로채면 안 되므로 raycast 대상에서
        // 제외합니다(이 프로젝트의 다른 HUD 텍스트/아이콘과 동일한 관례).
        SetRaycastTargetsFalse(canvasGO.transform);

        built = true;
    }

    private void SetRaycastTargetsFalse(Transform root)
    {
        foreach (var graphic in root.GetComponentsInChildren<Graphic>(true))
        {
            graphic.raycastTarget = false;
        }
    }

    // 이 Canvas의 CanvasScaler를 항상 PlayerHUDManager와 같은 기준 해상도/화면비 대응으로
    // 맞춥니다(클래스 상단 주석의 [화면 해상도 대응] 참고).
    private void UpdateCanvasScalerMatch()
    {
        if (canvasScaler == null || hudManager == null) return;

        Vector2 refRes = hudManager.referenceResolution;
        if (refRes.y <= 0f || Screen.height <= 0f) return;

        canvasScaler.referenceResolution = refRes;

        float screenAspect = (float)Screen.width / Screen.height;
        float referenceAspect = refRes.x / refRes.y;

        canvasScaler.matchWidthOrHeight = screenAspect > referenceAspect ? 1f : 0f;
    }

    // ------------------------------------------------------------------
    // 위치 계산 / 표시
    // ------------------------------------------------------------------

    // 액티브 아이템 호버: PlayerItemInfoDisplay/TreasureChestInfoDisplay가 원래 뜨는 자리에
    // 그대로 나타나되, 둘 중 하나가 지금 떠 있으면(근처 상자에 패시브 아이템이 있거나, 기본
    // 아이템 묶음이 있으면) 겹치지 않도록 그 오른쪽으로 밀립니다. 두 패널은 같은 자리를
    // 쓰지만 한 상자에 동시에 뜨는 일이 없으므로(TreasureChestInfoDisplay.cs 상단 주석
    // 참고), 어느 쪽이 떠 있는지만 확인하면 됩니다.
    private void ShowAtActiveSlot(ItemDefinition item)
    {
        var style = playerItemInfoDisplay.GetStyle();
        view.ShowItem(item, style);
        view.Reflow(style);

        Vector2 basePos = playerItemInfoDisplay.PanelAnchoredPosition;
        float shiftX = 0f;

        if (playerItemInfoDisplay.IsShowingItem)
        {
            shiftX = playerItemInfoDisplay.panelWidth + activeItemOverlapGap;
        }
        else if (treasureChestInfoDisplay != null && treasureChestInfoDisplay.IsShowingInfo)
        {
            shiftX = treasureChestInfoDisplay.panelWidth + activeItemOverlapGap;
        }

        view.PanelRect.anchoredPosition = new Vector2(basePos.x + shiftX, basePos.y);
    }

    // 패시브/장신구/일회용/음식 호버: 커서 위쪽 중앙에 뜨되, 화면 밖으로 나가지 않도록
    // 항상 화면 안쪽으로 clamp합니다.
    private void ShowAtCursor(ItemDefinition item)
    {
        var style = playerItemInfoDisplay.GetStyle();
        view.ShowItem(item, style);
        view.Reflow(style);

        Vector2 cursorTopLeft = ScreenPointToCanvasTopLeftLocal(Input.mousePosition);

        float panelW = view.PanelRect.sizeDelta.x;
        float panelH = view.PanelRect.sizeDelta.y;

        // 커서 X 중앙 정렬, 패널 아래쪽 끝이 커서보다 cursorOffset.y만큼 위에 오도록.
        float px = cursorTopLeft.x - panelW * 0.5f + cursorOffset.x;
        float py = cursorTopLeft.y + cursorOffset.y + panelH;

        Rect canvasBounds = canvasRect.rect;
        float canvasWidth = canvasBounds.width;
        float canvasHeight = canvasBounds.height;

        float minPx = screenEdgeMargin;
        float maxPx = Mathf.Max(minPx, canvasWidth - panelW - screenEdgeMargin);
        px = Mathf.Clamp(px, minPx, maxPx);

        float maxPy = -screenEdgeMargin;
        float minPy = Mathf.Min(maxPy, panelH - canvasHeight + screenEdgeMargin);
        py = Mathf.Clamp(py, minPy, maxPy);

        view.PanelRect.anchoredPosition = new Vector2(px, py);
    }

    // 화면 좌표를 이 Canvas의 "좌상단 기준" 로컬 좌표로 변환합니다(x: 오른쪽으로 갈수록 증가,
    // y: 아래로 갈수록 감소하는 음수) - PlayerItemInfoDisplay/ItemInfoPanelView가 쓰는
    // anchoredPosition과 동일한 좌표계입니다.
    private Vector2 ScreenPointToCanvasTopLeftLocal(Vector3 screenPoint)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, null, out Vector2 centerLocal);

        Rect r = canvasRect.rect;
        return new Vector2(centerLocal.x - r.xMin, centerLocal.y - r.yMax);
    }
}
