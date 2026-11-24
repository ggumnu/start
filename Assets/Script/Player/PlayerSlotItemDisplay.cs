﻿using UnityEngine;
using UnityEngine.UI;

// 화면 오른쪽에 뜨는 "장신구 / 일회용 / 음식" 인벤토리 HUD입니다. 세 종류는 각각 별개의
// 컴포넌트(PlayerTrinket / PlayerDisposableItem / PlayerFoodItem)가 따로 관리하지만, 화면에서는
// 하나의 검은 반투명 패널 안에 한 줄로 함께 보여줍니다. 패시브 아이템 인벤토리(PlayerInventoryDisplay)
// 바로 위에 놓이는 자리입니다.
//
// [칸 구성 - 종류마다 2칸씩, 총 6칸]
//   [장신구 1][장신구 2]  [일회용 1][일회용 2]  [음식 1][음식 2]
// 한 줄로 늘어서 있지만 실제로는 종류마다 두 칸씩 묶여 있습니다(묶음 사이 간격은 groupSpacing).
// 기본은 종류마다 1칸만 쓸 수 있고, 장신구는 55번, 일회용은 61번, 음식은 79번을 가지고 있으면 2칸이 됩니다.
//
// [잠긴 칸]
// 지금 쓸 수 없는 칸(55/61/79번이 없을 때의 두 번째 칸)은 칸 틀이 작고 반투명하게 보입니다(Ctrl 버리기 창에서 선택한
// 아이콘과 같은 느낌). 칸이 풀리거나(아이템 획득) 다시 잠길 때(48번 등으로 잃음)는 lockTransitionDuration 동안 부드럽게
// 커지며 진해지거나, 작아지며 옅어집니다. 게임을 시작해 처음 그릴 때는 연출 없이 바로 그 상태로 보입니다.
// 버리기 창(SlotItemDropUI)도 이 값(크기/불투명도)을 그대로 가져가 똑같이 보여줍니다.
//
// [지연 소모 - 반투명]
// 일회용과 음식은 "사용은 했지만 아직 소모되지 않은(대기 중)" 상태가 있습니다(IsPending).
// 대기 중인 아이템은 아이콘이 pendingAlpha만큼 반투명하게 보이고, 실제로 소모되거나 다른
// 아이템으로 교체되어 대기가 끝나면 원래대로 돌아옵니다. 장신구에는 사용 개념이 없어서 해당
// 없습니다.
//
// [기존 스크립트는 수정하지 않습니다]
// 세 컴포넌트에는 "장착이 바뀌었다"는 이벤트가 없으므로, 매 프레임 EquippedItem/IsPending을
// 읽어서 화면에 반영합니다(PlayerInventoryDisplay와 같은 방식이라 교체/소모 경로가 늘어나도
// 놓치지 않습니다). 이미지에 같은 스프라이트/색을 다시 넣는 것은 유니티가 변경으로 취급하지
// 않으므로 매 프레임 해도 부담이 없습니다.
//
// [배경 - Background Sprite 칸에 UI 스프라이트를 넣으면 그 그림이 배경이 됩니다]
// 칸을 비워두면 PlayerInventoryDisplay와 같은 검은 반투명 Image(Panel Color)로 대체 표시합니다.
// 사용 키(Q/E) 안내 문구는 나중에 배경 UI를 만들 때 함께 넣기로 했으므로 여기서는 표시하지
// 않습니다.
//
// [레이아웃 - 매 프레임 재계산]
// 위치/크기/간격/색은 인스펙터 값에만 의존하는 가벼운 계산이라 다른 HUD들과 동일하게 매
// 프레임 다시 적용합니다(Play 도중 값을 바꾸면 즉시 반영).
//
// [그림 두 장 - 형빈 확정] 배경 그림을 흰 창(Background Sprite = UI_PlayerSlotItem_OnlyFrame)과 보조틀(Button Sprite =
// UI_PlayerSlotItem_OnlyButton - 장신구/일회용/음식 + Q/E)로 나눴습니다. 두 그림은 크기가 같아 패널 전체에 겹쳐 그립니다
// (흰 창 -> 보조틀 -> 칸 틀 -> 아이콘 순). Button Sprite가 비어 있으면 보조틀 없이 예전처럼 배경 한 장만 그립니다.
//
// [불투명도 - 설정 창 HUD 탭, 형빈 확정] (값은 HUDUserSettings)
// - 흰 창 = "인벤토리 창 불투명도"(패시브 인벤토리와 함께), 보조틀 + 칸 틀 = "슬롯 보조틀 불투명도", 아이콘 = "슬롯 아이콘 불투명도".
// - 슬롯 아이콘이 0%면 패널을 창/접기 버튼까지 통째로 숨기고, 숨긴 동안은 아이콘 설명(툴팁)도 뜨지 않습니다.
// - 전투 중 자동 반투명(인벤토리용) - 들어가고 나올 때 combatFadeDuration초 동안 부드럽게 바뀝니다(HUDCombatTracker).
// - 패널을 그리는 부분은 아래 SlotPanelView로 떼어 설정 창 견본(SettingsHUDTab)도 같은 코드로 그립니다.
//
// [접어보기]
// 패널 왼쪽 세로 가운데의 버튼(또는 PlayerHUDManager의 전체 접기 키, 기본 Tab)으로 패널을 화면 오른쪽 밖으로
// 접었다 펼 수 있습니다. 버튼 생성/애니메이션/클릭 판정은 공용 클래스 CollapsibleHUDPanel이 맡고, 이 스크립트는
// 원래 위치를 넘겨주면 Tick이 접힘 이동량을 더한 최종 위치까지 정합니다(ApplyCollapse). 버튼 모양/시간/커브는 인스펙터의
// "접어보기" 묶음에서 조절합니다.
[RequireComponent(typeof(RectTransform))]
public class PlayerSlotItemDisplay : MonoBehaviour
{
    [Header("참조 (비워두면 자동으로 찾습니다 - 씬에 없는 종류는 항상 빈 칸으로 보입니다)")]
    public PlayerHUDManager hudManager;
    public PlayerTrinket playerTrinket;
    public PlayerDisposableItem playerDisposableItem;
    public PlayerFoodItem playerFoodItem;

    [Header("패널 위치 (화면 우측 중앙 기준, Play 중에도 즉시 반영됩니다)")]
    [Tooltip("화면 오른쪽 끝에서 패널 오른쪽 끝까지의 여백입니다.")]
    public float marginX = 60f;
    [Tooltip("화면 세로 중앙 기준 상하 위치입니다. 양수면 위로, 음수면 아래로 이동합니다. " +
             "패시브 아이템 인벤토리 패널 바로 위에 오도록 조절하세요.")]
    public float verticalOffset = 45f;

    [Header("패널 크기 / 배경 (스프라이트를 비워두면 검은 반투명)")]
    public float panelWidth = 366f;
    public float panelHeight = 165f;
    [Tooltip("배경으로 쓸 UI 스프라이트입니다. 비워두면 아래 Panel Color(검은 반투명)로 대체 표시됩니다.")]
    public Sprite backgroundSprite;
    public Color panelColor = new Color(0f, 0f, 0f, 0.6f);
    [Tooltip("흰 창 위에 겹쳐 그릴 보조틀 그림입니다(장신구/일회용/음식 + Q/E - UI_PlayerSlotItem_OnlyButton). 배경 그림과 같은 크기로 " +
             "패널 전체에 그려지고, 설정 창의 '슬롯 보조틀 불투명도'를 따릅니다. 비워두면 그리지 않습니다.")]
    public Sprite buttonSprite;
    [Tooltip("패널 왼쪽 끝에서 첫 번째 칸까지의 여백입니다.")]
    public float paddingLeft = 12f;
    [Tooltip("패널 위쪽 끝에서 칸이 놓인 줄의 위쪽 끝까지의 거리입니다. 값을 키우면 칸이 아래로 내려갑니다.")]
    public float slotRowTop = 77f;

    [Header("칸 (장신구 2 / 일회용 2 / 음식 2)")]
    public float slotSize = 50f;
    [Tooltip("같은 종류 두 칸 사이의 가로 간격입니다.")]
    public float slotSpacing = 6f;
    [Tooltip("종류와 종류 사이(장신구 묶음과 일회용 묶음 사이 등)의 가로 간격입니다.")]
    public float groupSpacing = 12f;
    [Tooltip("칸 틀 안쪽 여백입니다. 아이템 아이콘은 이 여백만큼 안쪽으로 줄어들어 틀 안에 들어갑니다.")]
    public float itemIconInset = 5f;

    [Header("스프라이트 / 색 (비워두면 옅은 자리표시 사각형으로 표시, 나중에 UI 스프라이트로 교체)")]
    [Tooltip("여섯 칸에 공통으로 쓰는 칸 틀 스프라이트입니다(목업의 노란 오각형). 아이템이 없는 " +
             "빈 칸일 때는 이 틀만 보입니다.")]
    public Sprite slotFrameSprite;
    [Tooltip("칸 틀 스프라이트가 비어있을 때 대신 보이는 옅은 자리표시 색입니다.")]
    public Color slotFramePlaceholderColor = new Color(1f, 1f, 1f, 0.15f);
    [Tooltip("ItemDefinition.icon이 아직 비어있는 아이템을 들고 있을 때 칸 안에 보이는 옅은 자리표시 색입니다.")]
    public Color missingItemIconColor = new Color(1f, 1f, 1f, 0.4f);

    [Header("지연 소모 (사용했지만 아직 소모되지 않은 일회용/음식)")]
    [Range(0f, 1f)]
    [Tooltip("대기 중인 아이템 아이콘의 불투명도입니다. 1이면 평소와 같고, 0에 가까울수록 투명해집니다.")]
    public float pendingAlpha = 0.5f;

    [Header("잠긴 칸 (55/61/79번이 없어 쓸 수 없는 두 번째 칸 - 버리기 창도 같은 값을 씀)")]
    [Range(0.1f, 1f)]
    [Tooltip("잠긴 칸 틀의 크기 배율입니다(칸 중심 기준으로 작아짐).")]
    public float lockedSlotScale = 0.75f;
    [Range(0f, 1f)]
    [Tooltip("잠긴 칸 틀의 불투명도입니다. 1이면 평소와 같고, 0에 가까울수록 투명해집니다.")]
    public float lockedSlotAlpha = 0.45f;
    [Min(0f)]
    [Tooltip("칸이 풀리거나 다시 잠길 때 커지거나/작아지는 연출 시간(초)입니다. 0이면 바로 바뀝니다.")]
    public float lockTransitionDuration = 0.25f;

    [Header("전투 중 자동 반투명 (켜고 끄기/값은 설정 창 HUD 탭)")]
    [Tooltip("전투 방에 들어가거나 클리어될 때 불투명도가 바뀌는 시간(초, 실제 시간 기준)입니다.")]
    [Min(0f)] public float combatFadeDuration = 0.3f;

    [Header("접어보기 (패널 왼쪽 버튼 - 누르면 화면 오른쪽으로 접힘)")]
    public CollapsibleHUDSettings collapse = new CollapsibleHUDSettings();

    // 종류 순서는 화면 왼쪽부터 장신구 -> 일회용 -> 음식입니다(SlotPanelView와 같은 순서).
    public const int CategoryTrinket = 0;
    public const int CategoryDisposable = 1;
    public const int CategoryFood = 2;
    public const int CategoryCount = 3;
    public const int SlotsPerCategory = 2;

    private SlotPanelView view;
    private RectTransform panelRect;
    private readonly HUDCombatTracker combat = new HUDCombatTracker();

    private bool built = false;
    private CollapsibleHUDPanel collapser;

    // 씬에 아직 없는 컴포넌트를 찾느라 매 프레임 씬 전체를 뒤지지 않도록 재시도 간격을 둡니다.
    private const float RefSearchInterval = 0.5f;
    private float refSearchTimer = 0f;

    void OnEnable()
    {
        combat.Subscribe();
    }

    void OnDisable()
    {
        combat.Unsubscribe();
    }

    void OnDestroy()
    {
        if (collapser != null) collapser.Dispose();
    }

    // [접어보기] 원래 위치(화면 우측 여백)를 넘겨 접힘 이동량(오른쪽 방향)이 더해진 위치로 둡니다.
    private void ApplyCollapse()
    {
        if (collapser == null) collapser = new CollapsibleHUDPanel("SlotItemPanel", HUDCollapseDirection.Right);
        collapser.Tick(collapse, panelRect, new Vector2(panelWidth, panelHeight), marginX, new Vector2(-marginX, verticalOffset));
    }

    void Update()
    {
        if (!EnsureReferences()) return;

        EnsureBuilt();
        if (!built) return;

        // [불투명도] 슬롯 아이콘 0%면 창/접기 버튼까지 통째로 숨깁니다(형빈 확정).
        bool visible = !HUDUserSettings.HidesSlotPanel;
        if (view.gameObject.activeSelf != visible) view.gameObject.SetActive(visible);

        float blend = combat.UpdateBlend(combatFadeDuration);
        HUDUserSettings.GetInventoryOpacities(false, out float nWindow, out float nSub, out float nSlot, out float nPassive);
        HUDUserSettings.GetInventoryOpacities(true, out float cWindow, out float cSub, out float cSlot, out float cPassive);
        float windowAlpha = Mathf.Lerp(nWindow, cWindow, blend);
        float subAlpha = Mathf.Lerp(nSub, cSub, blend);
        float iconAlpha = Mathf.Lerp(nSlot, cSlot, blend);

        ApplyPanelLayout(windowAlpha, subAlpha);
        ApplyCollapse();

        for (int category = 0; category < CategoryCount; category++)
        {
            for (int k = 0; k < SlotsPerCategory; k++)
            {
                GetSlotContent(category, k, out ItemDefinition item, out bool pending);
                int index = category * SlotsPerCategory + k;
                UpdateLock(view.slots[index], IsSlotLocked(category, k, item));
                view.ApplySlot(this, index, item != null, item != null ? item.icon : null, pending, subAlpha, iconAlpha);
            }
        }
    }

    // ------------------------------------------------------------------
    // 참조 / 데이터 읽기
    // ------------------------------------------------------------------

    // hudManager는 이 HUD가 동작하기 위한 필수 참조입니다. 나머지 세 컴포넌트는 없어도 해당
    // 종류 칸이 항상 비어 보일 뿐 패널 자체는 정상 동작합니다. 플레이어가 나중에 생성되는 경우를
    // 위해 못 찾은 참조는 일정 간격으로 다시 시도합니다.
    private bool EnsureReferences()
    {
        bool needSearch = hudManager == null || hudManager.CanvasRoot == null ||
                          playerTrinket == null || playerDisposableItem == null || playerFoodItem == null;

        if (needSearch)
        {
            refSearchTimer -= Time.unscaledDeltaTime;
            if (refSearchTimer <= 0f)
            {
                refSearchTimer = RefSearchInterval;

                if (hudManager == null)
                    hudManager = Object.FindFirstObjectByType<PlayerHUDManager>();
                if (playerTrinket == null)
                    playerTrinket = Object.FindFirstObjectByType<PlayerTrinket>();
                if (playerDisposableItem == null)
                    playerDisposableItem = Object.FindFirstObjectByType<PlayerDisposableItem>();
                if (playerFoodItem == null)
                    playerFoodItem = Object.FindFirstObjectByType<PlayerFoodItem>();
            }
        }

        return hudManager != null && hudManager.CanvasRoot != null;
    }

    // 종류(category)의 k번째 칸에 들어갈 아이템과 지연 소모 대기 여부를 돌려줍니다.
    // 장신구(55번)/일회용(61번)/음식(79번)은 2칸까지 쓸 수 있어 두 번째 칸에 나중에 얻은 아이템이 들어갑니다(그 아이템이
    // 없으면 빈칸 틀).
    private void GetSlotContent(int category, int k, out ItemDefinition item, out bool pending)
    {
        item = null;
        pending = false;

        // [55번] 장신구는 칸이 2개까지 늘어날 수 있어 k번째 칸에 k번째로 장착한(먼저 얻은 것이 앞) 장신구가 들어갑니다.
        if (category == CategoryTrinket)
        {
            if (playerTrinket != null) item = playerTrinket.GetEquippedAt(k);
            return;
        }

        // [61번] 일회용도 칸이 2개까지 늘어날 수 있어 k번째 칸에 k번째로 얻은 아이템이 들어갑니다. 지연 소모 대기는 맨 앞 칸만.
        if (category == CategoryDisposable)
        {
            if (playerDisposableItem != null)
            {
                item = playerDisposableItem.GetEquippedAt(k);
                pending = playerDisposableItem.IsPendingAt(k);
            }
            return;
        }

        // [79번] 음식도 칸이 2개까지 늘어날 수 있어 k번째 칸에 k번째로 얻은 음식이 들어갑니다. 지연 소모 대기는 맨 앞 칸만.
        if (category == CategoryFood)
        {
            if (playerFoodItem != null)
            {
                item = playerFoodItem.GetEquippedAt(k);
                pending = playerFoodItem.IsPendingAt(k);
            }
        }
    }

    // 종류(category)마다 지금 쓸 수 있는 칸 수입니다. 기본 1칸, 55번(장신구)/61번(일회용)/79번(음식)이 있으면 2칸.
    // 그 종류의 컴포넌트가 씬에 없으면 기본 1칸으로 봅니다.
    private int GetUsableSlotCount(int category)
    {
        if (category == CategoryTrinket) return playerTrinket != null ? playerTrinket.SlotCount : 1;
        if (category == CategoryDisposable) return playerDisposableItem != null ? playerDisposableItem.SlotCount : 1;
        if (category == CategoryFood) return playerFoodItem != null ? playerFoodItem.SlotCount : 1;
        return 1;
    }

    // 칸 번호가 쓸 수 있는 칸 수 이상이면 잠긴 칸입니다. 단, 아이템이 들어 있으면(55번 등을 잃은 바로 그 순간, 넘친 아이템이
    // 바닥으로 떨어지기 직전) 평소처럼 보여줍니다.
    private bool IsSlotLocked(int category, int k, ItemDefinition item)
    {
        return item == null && k >= GetUsableSlotCount(category);
    }

    // 잠김 정도를 목표(잠김 1 / 풀림 0)로 lockTransitionDuration 동안 옮깁니다. 일시정지 중에도 HUD는 실제 시간 기준으로 움직입니다.
    private void UpdateLock(SlotPanelView.Slot slot, bool locked)
    {
        float target = locked ? 1f : 0f;
        if (!slot.lockInitialized || lockTransitionDuration <= 0f)
        {
            slot.lockAmount = target;
            slot.lockInitialized = true;
            return;
        }
        slot.lockAmount = Mathf.MoveTowards(slot.lockAmount, target, Time.unscaledDeltaTime / lockTransitionDuration);
    }

    // ItemTooltipDisplay(v1.38)가 "지금 마우스가 장신구/일회용/음식 칸의 어떤 아이템 위에
    // 있는지" 확인할 때 씁니다. 아이템이 있는 칸만 대상이며, 이 프로젝트의 다른 HUD와
    // 동일하게 유니티 이벤트 시스템 대신 매 프레임 마우스 화면 좌표와 칸 사각형을 직접
    // 비교합니다. 읽기 전용이며 이 스크립트의 내부 로직에는 영향을 주지 않습니다.
    // 패널을 숨긴 동안(슬롯 아이콘 0%)은 항상 없음.
    public bool TryGetHoveredSlotItem(out ItemDefinition item, out RectTransform iconRect)
    {
        if (view == null || !view.gameObject.activeInHierarchy)
        {
            item = null;
            iconRect = null;
            return false;
        }

        for (int category = 0; category < CategoryCount; category++)
        {
            for (int k = 0; k < SlotsPerCategory; k++)
            {
                GetSlotContent(category, k, out ItemDefinition slotItem, out bool pending);
                if (slotItem == null) continue;

                var slot = view.slots[category * SlotsPerCategory + k];
                if (RectTransformUtility.RectangleContainsScreenPoint(slot.iconRect, Input.mousePosition, null))
                {
                    item = slotItem;
                    iconRect = slot.iconRect;
                    return true;
                }
            }
        }

        item = null;
        iconRect = null;
        return false;
    }

    // ------------------------------------------------------------------
    // 화면에 반영
    // ------------------------------------------------------------------

    // 패널 위치(화면 우측 중앙 기준)를 정하고, 크기/그림/칸 배치는 SlotPanelView에 맡깁니다(매 프레임 - 라이브 트윅).
    private void ApplyPanelLayout(float windowAlpha, float subAlpha)
    {
        panelRect.anchorMin = new Vector2(1f, 0.5f);
        panelRect.anchorMax = new Vector2(1f, 0.5f);
        panelRect.pivot = new Vector2(1f, 0.5f);
        panelRect.anchoredPosition = new Vector2(-marginX, verticalOffset);
        view.Layout(this, windowAlpha, subAlpha);
    }

    // ------------------------------------------------------------------
    // 오브젝트 생성 (한 번만)
    // ------------------------------------------------------------------

    private void EnsureBuilt()
    {
        if (built) return;

        view = SlotPanelView.Create(hudManager.CanvasRoot, "SlotItemPanel");
        panelRect = view.panelRect;
        built = true;
    }
}

// [슬롯 인벤토리 그리기 - 실제 HUD(PlayerSlotItemDisplay)와 설정 창 견본(SettingsHUDTab)이 같이 씀]
// 패널 = 흰 창(배경 그림) + 보조틀(같은 크기 그림, 패널 전체) + 칸 6개(칸 틀 + 그 안의 아이콘). 값은 PlayerSlotItemDisplay의 인스펙터 값.
// 패널 위치(anchoredPosition)는 부르는 쪽이 정합니다. 칸 순서 = 장신구 1, 2, 일회용 1, 2, 음식 1, 2.
public class SlotPanelView
{
    public class Slot
    {
        public RectTransform frameRect;
        public Image frame;
        public RectTransform iconRect;
        public Image icon;
        public float lockAmount;   // 0 = 풀림(평소), 1 = 잠김(작고 반투명). 연출 중에는 그 사이 값
        public bool lockInitialized; // 처음 그릴 때는 연출 없이 바로 맞춤
    }

    public const int SlotCount = PlayerSlotItemDisplay.CategoryCount * PlayerSlotItemDisplay.SlotsPerCategory;

    public RectTransform panelRect;
    public Image panelImage;
    public RectTransform buttonRect;
    public Image buttonImage;
    public readonly Slot[] slots = new Slot[SlotCount];

    public GameObject gameObject => panelRect.gameObject;

    public static SlotPanelView Create(Transform parent, string name)
    {
        var v = new SlotPanelView();

        var panelRoot = new GameObject(name);
        panelRoot.transform.SetParent(parent, false);

        v.panelRect = panelRoot.AddComponent<RectTransform>();
        v.panelRect.anchorMin = new Vector2(1f, 0.5f);
        v.panelRect.anchorMax = new Vector2(1f, 0.5f);
        v.panelRect.pivot = new Vector2(1f, 0.5f);

        v.panelImage = panelRoot.AddComponent<Image>();
        v.panelImage.raycastTarget = false; // HUD가 마우스 입력을 가로채지 않도록

        // 보조틀 - 흰 창 바로 위, 칸들보다 먼저(뒤)
        var buttonGO = new GameObject("SubFrame");
        buttonGO.transform.SetParent(panelRoot.transform, false);
        v.buttonRect = buttonGO.AddComponent<RectTransform>();
        v.buttonRect.anchorMin = Vector2.zero;
        v.buttonRect.anchorMax = Vector2.one;
        v.buttonRect.offsetMin = Vector2.zero;
        v.buttonRect.offsetMax = Vector2.zero;
        v.buttonImage = buttonGO.AddComponent<Image>();
        v.buttonImage.raycastTarget = false;

        for (int category = 0; category < PlayerSlotItemDisplay.CategoryCount; category++)
        {
            for (int k = 0; k < PlayerSlotItemDisplay.SlotsPerCategory; k++)
            {
                v.slots[category * PlayerSlotItemDisplay.SlotsPerCategory + k] = BuildSlot(panelRoot.transform, category, k);
            }
        }
        return v;
    }

    private static Slot BuildSlot(Transform parent, int category, int k)
    {
        var slot = new Slot();

        var frameGO = new GameObject($"Slot_{category}_{k}");
        frameGO.transform.SetParent(parent, false);
        slot.frameRect = frameGO.AddComponent<RectTransform>();
        slot.frameRect.anchorMin = new Vector2(0f, 1f);
        slot.frameRect.anchorMax = new Vector2(0f, 1f);
        slot.frameRect.pivot = new Vector2(0f, 1f);
        slot.frame = frameGO.AddComponent<Image>();
        slot.frame.preserveAspect = true;
        slot.frame.raycastTarget = false;

        var iconGO = new GameObject("ItemIcon");
        iconGO.transform.SetParent(frameGO.transform, false);
        slot.iconRect = iconGO.AddComponent<RectTransform>();
        slot.iconRect.anchorMin = Vector2.zero;
        slot.iconRect.anchorMax = Vector2.one;
        slot.icon = iconGO.AddComponent<Image>();
        slot.icon.preserveAspect = true;
        slot.icon.raycastTarget = false;
        slot.icon.enabled = false;

        return slot;
    }

    // 패널 크기, 흰 창/보조틀 그림(불투명도 적용), 여섯 칸의 위치/크기. windowAlpha/subAlpha = 0~1.
    public void Layout(PlayerSlotItemDisplay s, float windowAlpha, float subAlpha)
    {
        panelRect.sizeDelta = new Vector2(s.panelWidth, s.panelHeight);

        Color bg;
        if (s.backgroundSprite != null)
        {
            panelImage.sprite = s.backgroundSprite;
            bg = Color.white;
        }
        else
        {
            panelImage.sprite = null;
            bg = s.panelColor;
        }
        bg.a *= Mathf.Clamp01(windowAlpha);
        panelImage.color = bg;

        bool hasButton = s.buttonSprite != null;
        if (buttonImage.enabled != hasButton) buttonImage.enabled = hasButton;
        if (hasButton)
        {
            buttonImage.sprite = s.buttonSprite;
            buttonImage.color = new Color(1f, 1f, 1f, Mathf.Clamp01(subAlpha));
        }

        int perCategory = PlayerSlotItemDisplay.SlotsPerCategory;
        float groupWidth = perCategory * s.slotSize + (perCategory - 1) * s.slotSpacing;

        for (int category = 0; category < PlayerSlotItemDisplay.CategoryCount; category++)
        {
            for (int k = 0; k < perCategory; k++)
            {
                var slot = slots[category * perCategory + k];

                float x = s.paddingLeft + category * (groupWidth + s.groupSpacing) + k * (s.slotSize + s.slotSpacing);

                // 칸 중심을 기준점으로 둡니다(잠긴 칸이 가운데로 작아지도록). 위치는 예전(왼쪽 위 기준)과 같습니다.
                slot.frameRect.pivot = new Vector2(0.5f, 0.5f);
                slot.frameRect.sizeDelta = new Vector2(s.slotSize, s.slotSize);
                slot.frameRect.anchoredPosition = new Vector2(x + s.slotSize * 0.5f, -s.slotRowTop - s.slotSize * 0.5f);

                // 아이콘은 칸 틀의 자식으로, 틀 전체에서 itemIconInset만큼 안쪽을 채웁니다.
                slot.iconRect.offsetMin = new Vector2(s.itemIconInset, s.itemIconInset);
                slot.iconRect.offsetMax = new Vector2(-s.itemIconInset, -s.itemIconInset);
            }
        }
    }

    // 칸 틀은 항상 보이고(보조틀 불투명도), 아이템이 있으면 그 위(틀 안쪽)에 아이콘이 겹쳐집니다(아이콘 불투명도). 대기 중인
    // 아이템은 아이콘만 pendingAlpha만큼 반투명해집니다. 잠긴 칸은 틀이 작고 반투명해집니다(연출 중에는 그 사이 - slot.lockAmount).
    // 아이콘이 틀의 자식이라 틀 색의 알파는 아이콘에 영향을 주지 않습니다(Image 색은 자식에게 곱해지지 않음).
    public void ApplySlot(PlayerSlotItemDisplay s, int index, bool hasItem, Sprite iconSprite, bool pending, float subAlpha, float iconAlpha)
    {
        var slot = slots[index];

        Color frameColor;
        if (s.slotFrameSprite != null)
        {
            slot.frame.sprite = s.slotFrameSprite;
            frameColor = Color.white;
        }
        else
        {
            slot.frame.sprite = null;
            frameColor = s.slotFramePlaceholderColor;
        }

        float t = Mathf.SmoothStep(0f, 1f, slot.lockAmount);
        frameColor.a *= Mathf.Lerp(1f, s.lockedSlotAlpha, t) * Mathf.Clamp01(subAlpha);
        slot.frame.color = frameColor;
        float scale = Mathf.Lerp(1f, s.lockedSlotScale, t);
        slot.frameRect.localScale = new Vector3(scale, scale, 1f);

        if (slot.icon.enabled != hasItem) slot.icon.enabled = hasItem;
        if (!hasItem) return;

        Color color;
        if (iconSprite != null)
        {
            slot.icon.sprite = iconSprite;
            color = Color.white;
        }
        else
        {
            slot.icon.sprite = null;
            color = s.missingItemIconColor;
        }

        if (pending) color.a *= s.pendingAlpha;
        color.a *= Mathf.Clamp01(iconAlpha);
        slot.icon.color = color;
    }
}
