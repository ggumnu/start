﻿using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

// 화면 왼쪽 상단, 초상화 바로 아래에 "액티브 아이템 + 코스트" 묶음을 표시하는 HUD입니다.
// 목업 기준으로 네 가지 요소가 한 그룹(CostDisplay)으로 묶여 있고, 그 위에 기능 없는 안내창
// 자리가 하나 더 있습니다.
//
//   1. 별(액티브 아이템 칸)    : 항상 별 틀이 보이고, 액티브 아이템을 장착하면 그 위에 아이템
//                                아이콘이 겹쳐집니다. 장착 중인 게 없으면 별 틀만 보입니다("빈 아이콘").
//   2. 빨간 원(소모 코스트)    : 항상 원 틀이 보이고, 액티브 아이템을 장착하면 그 아이템이 한 번
//                                사용할 때 소모하는 코스트(ItemDefinition.activeCost)가 숫자로
//                                써집니다. 장착 중인 게 없으면 숫자 없이 원 틀만 보입니다.
//   3. 보라 사각형(현재 코스트) : 지금 플레이어가 가진 코스트 수량입니다. 액티브 아이템이 없어도
//                                코스트는 계속 차므로 항상 표시됩니다.
//   4. 코스트 칸들(분홍/빨강)  : 코스트 자체를 칸으로 보여줍니다. 가진 만큼 왼쪽부터 "채워진 칸"
//                                스프라이트, 나머지는 "빈 칸" 스프라이트로 교체됩니다. 칸의 총
//                                개수는 PlayerResources.maxCost를 그대로 따라갑니다(기본 10칸).
//   5. SPACE 안내창(그림만)    : 코스트 칸 위에 뜨는 안내 그림 자리입니다. 처음 하는 사람이 액티브
//                                아이템 사용 키를 알 수 있도록 미리 만든 그림 한 장을 그대로 띄우기만
//                                하며, 클릭/조건 표시 같은 기능은 없습니다. 스프라이트가 비어 있으면
//                                아무것도 표시하지 않습니다.
//
// [기존 스크립트는 수정하지 않습니다]
// PlayerResources / PlayerActiveItem / ItemDefinition은 전혀 건드리지 않고, 이미 공개되어 있는
// 값만 읽습니다.
//   - 현재 코스트: PlayerResources.OnAmountChanged 이벤트를 구독해서 Cost 값이 바뀐 순간에만
//     숫자 텍스트를 갱신합니다(PlayerResourceDisplay와 동일한 방식).
//   - 액티브 아이템: PlayerActiveItem에는 "장착이 바뀌었다"는 이벤트가 없으므로, 매 프레임
//     EquippedItem 참조(와 activeCost)를 지난 프레임 값과 비교해서 달라졌을 때만 아이콘/숫자를
//     갱신합니다(PlayerInventoryDisplay가 OwnedItems를 비교하는 것과 같은 방식이라, 나중에
//     교체/소멸 경로가 늘어나도 놓치지 않습니다).
//
// [레이아웃 - 매 프레임 재계산]
// 다른 HUD 스크립트들과 동일하게, 위치/크기/간격/폰트/자리표시 색은 인스펙터 값에만 의존하는
// 가벼운 계산이라 매 프레임 다시 적용합니다(Play 도중 값을 바꾸면 즉시 반영). 반면 "숫자
// 텍스트"는 값이 실제로 바뀔 때만 다시 씁니다. 데이터가 바뀔 때만 할 일과 매 프레임 반영할
// 레이아웃 계산은 서로 다른 함수로 분리되어 있습니다.
//
// [추가 보조 아이콘 - 액티브 아이템이 별 왼쪽 아래에 작게 띄울 수 있는 범용 자리]
// 액티브 아이템이 필요로 할 때 별의 왼쪽 아래에 작은 아이콘을 하나 더 보여주는 자리입니다.
// 지금은 93번(효과: ReplayLastFoodEffect)만 이 자리를 쓰며, 재현할 음식(PlayerFoodItem.
// LastUsedFood)의 아이콘을 보여줍니다. 음식을 아직 한 번도 소모하지 않았거나, 이 자리를 쓰지
// 않는 액티브 아이템을 장착 중이면 아무것도 표시하지 않습니다. 이 아이콘은 지연 소모 대기 중에도
// 반투명해지지 않습니다(반투명은 액티브 아이콘에만 적용됩니다). 다른 액티브 아이템이 이 자리를
// 쓰려면 SyncAuxIcon()에서 "어떤 아이템일 때 어떤 아이콘을 보여줄지"만 추가하면 됩니다.
// [85번 추가] 85번(효과: ReplayLastDisposableEffect)도 이 자리를 써서, 재현할 일회용(PlayerDisposableItem.
// LastUsedDisposable)의 아이콘을 보여줍니다(93번과 같은 규칙).
//
// [스프라이트 - 비워두면 목업 색 자리표시]
// 다섯 스프라이트를 비워두면 목업과 같은 색의 단색 사각형이 자리표시로 나옵니다. 나중에 실제
// UI 스프라이트를 인스펙터에 넣으면 그 즉시 교체됩니다.
//
// [쿨타임 연출 - 74번 등 IActiveItemUseRules 액티브]
// 장착 중인 액티브에 쿨타임이 남아 있으면(PlayerActiveItem.CooldownRemaining01 > 0), 아이템 아이콘 위에 같은 아이콘을 어둡게
// 칠한 덮개(Cooldown Overlay Color)를 겹칩니다. 덮개는 장전 R 표시(PlayerReloadIndicator/AmmoBoxDisplay)와 같은 원형 채우기
// (Radial360, 위쪽 시작, fillClockwise = false, fillAmount 1 -> 0)라서, 쿨타임 동안 12시 방향부터 시계방향으로 걷히며 아이콘이
// 밝아지는 것처럼 보입니다. 쿨타임이 없는 액티브에서는 덮개가 나오지 않습니다.
// 소모 코스트 숫자도 PlayerActiveItem.GetCurrentUseCost()를 보여주므로, 74번은 전투 중이 아닐 때 0으로 보입니다.
[RequireComponent(typeof(RectTransform))]
public class PlayerCostDisplay : MonoBehaviour
{
    [Header("참조 (비워두면 자동으로 찾습니다)")]
    public PlayerHUDManager hudManager;
    public PlayerResources playerResources;
    public PlayerActiveItem playerActiveItem;

    [Header("스프라이트 (비워두면 목업 색 자리표시 사각형으로 표시, 나중에 UI 스프라이트로 교체)")]
    [Tooltip("별 틀입니다. 액티브 아이템이 없을 때의 \"빈 아이콘\" 역할도 겸합니다. 액티브 " +
             "아이템을 장착하면 이 틀 위에 아이템 아이콘이 겹쳐집니다.")]
    public Sprite activeFrameSprite;
    [Tooltip("액티브 아이템의 소모 코스트를 표시하는 원 틀입니다. 액티브 아이템이 없을 때는 " +
             "숫자 없이 이 틀만 보입니다(\"빈 코스트 아이콘\").")]
    public Sprite costBadgeSprite;
    [Tooltip("현재 보유 코스트 숫자 뒤에 깔리는 사각형입니다.")]
    public Sprite currentCostBoxSprite;
    [Tooltip("코스트 칸 중 \"가진\" 칸에 쓰는 스프라이트입니다(목업의 분홍색).")]
    public Sprite costFilledSprite;
    [Tooltip("코스트 칸 중 \"비어있는\" 칸에 쓰는 스프라이트입니다(목업의 빨간색).")]
    public Sprite costEmptySprite;

    [Header("자리표시 색 (해당 스프라이트가 비어있을 때만 사용)")]
    public Color activeFramePlaceholderColor = new Color(0.20f, 0.55f, 1.00f, 1f);
    public Color costBadgePlaceholderColor = new Color(0.90f, 0.10f, 0.15f, 1f);
    public Color currentCostBoxPlaceholderColor = new Color(0.60f, 0.25f, 0.70f, 1f);
    public Color costFilledPlaceholderColor = new Color(1.00f, 0.60f, 0.75f, 1f);
    public Color costEmptyPlaceholderColor = new Color(0.90f, 0.10f, 0.15f, 1f);
    [Tooltip("아이콘 스프라이트가 아직 없는 아이템을 장착했을 때 별 위에 보이는 옅은 자리표시 색입니다.")]
    public Color missingItemIconColor = new Color(1f, 1f, 1f, 0.15f);

    [Header("지연 소모 (사용했지만 아직 끝나지 않은 액티브 아이템 - 93번 등)")]
    [Range(0f, 1f)]
    [Tooltip("대기 중인 액티브 아이템 아이콘의 불투명도입니다. 1이면 평소와 같고, 0에 가까울수록 " +
             "투명해집니다. 음식/일회용 칸(PlayerSlotItemDisplay)의 Pending Alpha와 같은 뜻입니다.")]
    public float pendingAlpha = 0.5f;

    [Header("쿨타임 (74번 등 - 쿨타임 동안 아이콘이 어두워졌다가 시계방향으로 밝아짐)")]
    [Tooltip("쿨타임이 남은 부분의 아이콘을 덮는 색입니다. 검은색의 불투명도(A)를 높일수록 더 어둡게 보입니다.")]
    public Color cooldownOverlayColor = new Color(0f, 0f, 0f, 0.6f);

    [Header("6. 추가 보조 아이콘 (별의 왼쪽 아래에 표시 - 지금은 93번의 직전 음식 아이콘)")]
    [Tooltip("추가 보조 아이콘의 한 변 길이입니다.")]
    [FormerlySerializedAs("lastFoodIconSize")]
    public float auxIconSize = 40f;
    [Tooltip("아이콘의 중심을 별의 왼쪽 아래 모서리 기준으로 밀어서 조정합니다. 기본값 (20, 20)은 " +
             "아이콘이 별의 왼쪽 아래 모서리에 살짝 걸치는 위치입니다(빨간 원과 좌우 대칭). " +
             "x는 오른쪽(+), y는 위(+)입니다.")]
    [FormerlySerializedAs("lastFoodIconOffset")]
    public Vector2 auxIconOffset = new Vector2(20f, 20f);

    [Header("그룹 위치 (초상화 바로 아래, 좌상단 기준 - Play 중에도 즉시 반영됩니다)")]
    public float marginX = 35f;
    [Tooltip("초상화 아래쪽 끝에서 별 위쪽 끝까지의 세로 간격입니다. 0이면 초상화에 바로 붙습니다.")]
    public float gapBelowPortrait = 0f;

    [Header("1. 별 (액티브 아이템 칸)")]
    public float starSize = 90f;
    [Tooltip("별 위에 겹쳐지는 아이템 아이콘의 한 변 길이입니다.")]
    public float itemIconSize = 60f;
    [Tooltip("아이템 아이콘을 별 중앙 기준으로 밀어서 조정합니다. x는 오른쪽(+), y는 위(+)입니다.")]
    public Vector2 itemIconOffset = Vector2.zero;

    [Header("2. 빨간 원 (액티브 아이템 소모 코스트)")]
    public float badgeSize = 48f;
    [Tooltip("원의 중심을 별의 오른쪽 아래 모서리 기준으로 밀어서 조정합니다. 기본값 (-20, 20)은 " +
             "원이 별의 오른쪽 아래 모서리에 살짝 걸치는 위치입니다. x는 오른쪽(+), y는 위(+)입니다.")]
    public Vector2 badgeOffset = new Vector2(-20f, 20f);
    public int badgeFontSize = 26;
    public Color badgeFontColor = Color.white;

    [Header("3. 보라 사각형 (현재 코스트 숫자)")]
    [Tooltip("사각형의 왼쪽 위 모서리 위치입니다. 별의 왼쪽 위 모서리 기준이고, x는 오른쪽(+), " +
             "y는 위(+)입니다(기본값의 y가 음수인 것은 별보다 살짝 아래에 있다는 뜻입니다).")]
    public Vector2 boxOffset = new Vector2(107f, -18f);
    [Tooltip("두 자리 수(10)도 들어가도록 넉넉하게 잡아주세요.")]
    public float boxSize = 60f;
    public int boxFontSize = 32;
    public Color boxFontColor = Color.white;
    [Tooltip("현재 코스트 숫자의 위치를 사각형 가운데 기준으로 밀어서 조정합니다(사각형 자체는 " +
             "움직이지 않고 숫자만 움직입니다). x는 오른쪽(+), y는 위(+)입니다.")]
    public Vector2 boxTextOffset = Vector2.zero;

    [Header("4. 코스트 칸 (분홍/빨강)")]
    [Tooltip("첫 번째 칸의 왼쪽 위 모서리 위치입니다. 별의 왼쪽 위 모서리 기준이고, x는 오른쪽(+), " +
             "y는 위(+)입니다.")]
    public Vector2 barOffset = new Vector2(177f, -37f);
    public float slotWidth = 34f;
    public float slotHeight = 36f;
    [Tooltip("칸과 칸 사이의 가로 간격입니다.")]
    public float slotSpacing = 4.5f;

    [Header("5. SPACE 안내창 (코스트 칸 위 - 기능 없이 그림만 표시)")]
    [Tooltip("코스트 칸 위에 띄울 안내 그림입니다. 비워두면 아무것도 표시하지 않습니다.")]
    public Sprite spaceHintSprite;
    [Tooltip("그림이 그려지는 크기입니다(가로, 세로). 0, 0으로 두면 스프라이트 원본 픽셀 크기로 " +
             "표시됩니다. 원본과 비율이 다르게 넣으면 그림이 찌그러집니다.")]
    public Vector2 spaceHintSize = Vector2.zero;
    [Tooltip("그림의 왼쪽 아래 모서리를 첫 번째 코스트 칸의 왼쪽 위 모서리 기준으로 밀어서 " +
             "조정합니다. x는 오른쪽(+), y는 위(+)입니다. 기본값 (0, 6)은 첫 번째 칸 바로 위에 " +
             "6만큼 띄워서 왼쪽을 맞춘 위치입니다.")]
    public Vector2 spaceHintOffset = new Vector2(0f, 6f);

    private GameObject root;
    private RectTransform rootRect;

    private RectTransform starRect;
    private Image starImage;
    private RectTransform itemIconRect;
    private Image itemIconImage;
    private RectTransform cooldownOverlayRect;
    private Image cooldownOverlayImage;
    private RectTransform auxIconRect;
    private Image auxIconImage;
    private RectTransform badgeRect;
    private Image badgeImage;
    private TextMeshProUGUI badgeText;
    private RectTransform spaceHintRect;
    private Image spaceHintImage;
    private RectTransform boxRect;
    private Image boxImage;
    private TextMeshProUGUI boxText;
    private readonly List<Image> slotImages = new List<Image>();
    private readonly List<RectTransform> slotRects = new List<RectTransform>();

    private bool built = false;
    private PlayerResources subscribedResources;

    // 지금 화면에 반영되어 있는 값들입니다. 값이 달라졌을 때만 텍스트/아이콘을 다시 씁니다.
    private int shownCost = 0;
    private bool costTextDirty = true;
    private ItemDefinition shownItem;
    private int shownActiveCost = 0;
    private bool shownPending = false;
    private float shownPendingAlpha = 1f;
    private bool activeDirty = true;
    private PlayerFoodItem playerFoodItem;
    private PlayerDisposableItem playerDisposableItem; // [85번] 직전 일회용 보조 아이콘용
    private ItemDefinition shownAuxItem;
    private bool auxIconDirty = true;

    // 씬에 아직 없는 컴포넌트를 찾느라 매 프레임 씬 전체를 뒤지지 않도록 재시도 간격을 둡니다.
    private const float RefSearchInterval = 0.5f;
    private float refSearchTimer = 0f;

    void Update()
    {
        if (!EnsureReferences()) return;

        EnsureBuilt();
        if (!built) return;

        SyncSlotCount();
        SyncActiveItem();
        SyncCooldownOverlay();
        SyncAuxIcon();

        ApplyCostTextIfDirty();

        ApplyRootPosition();
        ApplyLayoutAndVisuals();
    }

    void OnDestroy()
    {
        if (subscribedResources != null)
        {
            subscribedResources.OnAmountChanged -= HandleAmountChanged;
        }
    }

    // ------------------------------------------------------------------
    // 참조 / 이벤트 구독
    // ------------------------------------------------------------------

    // hudManager와 playerResources는 이 HUD가 동작하기 위한 필수 참조입니다. 못 찾으면 false를
    // 반환해서 아직 아무것도 만들지 않습니다. playerActiveItem은 없어도 코스트 표시는 정상
    // 동작하므로 필수가 아닙니다(못 찾는 동안은 "장착 중인 액티브 아이템 없음"으로 취급).
    // 플레이어가 나중에 생성되는 경우를 위해 못 찾은 참조는 일정 간격으로 다시 시도합니다.
    private bool EnsureReferences()
    {
        bool needSearch = hudManager == null || hudManager.CanvasRoot == null ||
                          playerResources == null || playerActiveItem == null;

        if (needSearch)
        {
            refSearchTimer -= Time.unscaledDeltaTime;
            if (refSearchTimer <= 0f)
            {
                refSearchTimer = RefSearchInterval;

                if (hudManager == null)
                    hudManager = Object.FindFirstObjectByType<PlayerHUDManager>();
                if (playerResources == null)
                    playerResources = Object.FindFirstObjectByType<PlayerResources>();
                if (playerActiveItem == null)
                    playerActiveItem = Object.FindFirstObjectByType<PlayerActiveItem>();
            }
        }

        if (hudManager == null || hudManager.CanvasRoot == null) return false;
        if (playerResources == null) return false;

        EnsureSubscription();
        return true;
    }

    // playerResources가 바뀌었거나(플레이어가 다시 만들어진 경우 등) 처음 찾은 경우에만 구독을
    // 새로 겁니다. 그 순간 현재 코스트도 한 번 읽어서 초기값으로 채웁니다.
    private void EnsureSubscription()
    {
        if (subscribedResources == playerResources) return;

        if (subscribedResources != null)
        {
            subscribedResources.OnAmountChanged -= HandleAmountChanged;
        }

        subscribedResources = playerResources;

        if (subscribedResources != null)
        {
            subscribedResources.OnAmountChanged += HandleAmountChanged;
            shownCost = subscribedResources.GetAmount(BasicItemType.Cost);
            costTextDirty = true;
        }
    }

    // OnAmountChanged 이벤트 핸들러 - Cost가 실제로 바뀐 순간에만 의미가 있습니다.
    private void HandleAmountChanged(BasicItemType type, int newAmount)
    {
        if (type != BasicItemType.Cost) return;

        shownCost = newAmount;
        costTextDirty = true;
    }

    // ------------------------------------------------------------------
    // 데이터가 바뀔 때만 하는 일
    // ------------------------------------------------------------------

    // PlayerResources.maxCost가 바뀌면(나중에 최대 코스트를 늘리는 아이템 등) 칸 개수를 그에 맞춥니다.
    private void SyncSlotCount()
    {
        int wanted = Mathf.Max(1, playerResources.maxCost);
        if (slotImages.Count == wanted) return;

        while (slotImages.Count < wanted)
        {
            CreateSlot(slotImages.Count);
        }
        while (slotImages.Count > wanted)
        {
            int last = slotImages.Count - 1;
            if (slotImages[last] != null) Destroy(slotImages[last].gameObject);
            slotImages.RemoveAt(last);
            slotRects.RemoveAt(last);
        }
    }

    // 장착 중인 액티브 아이템(참조)과 그 소모 코스트가 지난 프레임과 달라졌을 때만 아이콘/숫자를
    // 갱신합니다. 이벤트가 아니라 매 프레임 비교하므로 어떤 경로로 바뀌어도 놓치지 않습니다.
    private void SyncActiveItem()
    {
        ItemDefinition item = playerActiveItem != null ? playerActiveItem.EquippedItem : null;
        // 실제 소모 코스트(45번 등 감소 반영)를 보여줍니다 - 매 프레임 비교하므로 장신구를 교체하는 순간 숫자도 따라 바뀝니다.
        // [74번] 사용 규칙이 있는 액티브는 그 규칙이 정한 이번 사용 코스트(비전투 중 0 등)를 보여줍니다(GetCurrentUseCost).
        int cost = item != null ? playerActiveItem.GetCurrentUseCost() : 0;
        bool pending = item != null && playerActiveItem.IsPending;

        if (!activeDirty && item == shownItem && cost == shownActiveCost &&
            pending == shownPending && Mathf.Approximately(pendingAlpha, shownPendingAlpha)) return;

        shownItem = item;
        shownActiveCost = cost;
        shownPending = pending;
        shownPendingAlpha = pendingAlpha;
        activeDirty = false;

        if (item == null)
        {
            // 장착 중인 액티브 아이템 없음: 별 틀만 보이고, 빨간 원은 숫자 없이 틀만 보입니다.
            itemIconImage.enabled = false;
            badgeText.enabled = false;
            return;
        }

        itemIconImage.enabled = true;

        Color iconColor;
        if (item.icon != null)
        {
            itemIconImage.sprite = item.icon;
            iconColor = Color.white;
        }
        else
        {
            itemIconImage.sprite = null;
            iconColor = missingItemIconColor;
        }

        // 지연 소모 대기 중이면 아이콘만 pendingAlpha만큼 반투명하게 보입니다.
        if (pending) iconColor.a *= pendingAlpha;
        itemIconImage.color = iconColor;

        badgeText.text = cost.ToString();
        badgeText.enabled = true;
    }

    // [74번 등] 쿨타임이 남아 있으면 아이템 아이콘 위에 같은 아이콘 모양의 어두운 덮개를 원형으로 채워 겹칩니다. 남은 비율이
    // 매 프레임 바뀌므로 매 프레임 반영합니다(같은 값을 다시 넣는 것은 유니티가 변경으로 취급하지 않아 부담이 없습니다).
    private void SyncCooldownOverlay()
    {
        float remaining = (playerActiveItem != null && shownItem != null) ? playerActiveItem.CooldownRemaining01 : 0f;

        // 원형 채우기는 스프라이트가 있어야 동작하므로, 아이콘 스프라이트가 아직 없는 아이템은 덮개를 띄우지 않습니다.
        bool show = remaining > 0f && shownItem != null && shownItem.icon != null && itemIconImage.enabled;
        if (cooldownOverlayImage.enabled != show) cooldownOverlayImage.enabled = show;
        if (!show) return;

        cooldownOverlayImage.sprite = shownItem.icon;
        cooldownOverlayImage.color = cooldownOverlayColor;
        cooldownOverlayImage.fillAmount = remaining;
    }

    // ItemTooltipDisplay(v1.38)가 "지금 마우스가 액티브 아이템(별) 위에 있는지" 확인할 때
    // 씁니다. 액티브 아이템이 장착돼 있을 때만 대상이며, 이 프로젝트의 다른 HUD와 동일하게
    // 유니티 이벤트 시스템 대신 매 프레임 마우스 화면 좌표와 아이콘 사각형을 직접
    // 비교합니다. 읽기 전용이며 이 스크립트의 내부 로직에는 영향을 주지 않습니다.
    // [보조 아이콘 설명 - 93/85번] 별 왼쪽 아래 보조 아이콘(직전 음식/일회용)에 커서가 있으면 그 아이템을 돌려줍니다 - 그래서
    // 액티브 아이템과 같은 자리에 그 아이템의 설명칸이 뜹니다(ItemTooltipDisplay는 그대로). 보조 아이콘이 별 위에 그려지므로
    // 보조 아이콘을 먼저 확인합니다.
    public bool TryGetHoveredActiveItem(out ItemDefinition item, out RectTransform iconRect)
    {
        if (shownAuxItem != null && auxIconRect != null && auxIconImage != null && auxIconImage.enabled &&
            RectTransformUtility.RectangleContainsScreenPoint(auxIconRect, Input.mousePosition, null))
        {
            item = shownAuxItem;
            iconRect = auxIconRect;
            return true;
        }

        if (shownItem != null && itemIconRect != null &&
            RectTransformUtility.RectangleContainsScreenPoint(itemIconRect, Input.mousePosition, null))
        {
            item = shownItem;
            iconRect = itemIconRect;
            return true;
        }

        item = null;
        iconRect = null;
        return false;
    }

    // 별 왼쪽 아래의 추가 보조 아이콘을 갱신합니다. 지금은 93번처럼 직전 음식을 재현하는 액티브
    // 아이템을 장착 중일 때만, 재현될 음식(직전에 소모한 음식)의 아이콘을 보여줍니다. 보여줄
    // 아이템 참조가 지난 프레임과 달라졌을 때만 아이콘을 다시 씁니다(SyncActiveItem과 같은
    // 방식이라 어떤 경로로 바뀌어도 놓치지 않습니다).
    private void SyncAuxIcon()
    {
        ItemDefinition active = playerActiveItem != null ? playerActiveItem.EquippedItem : null;
        ItemDefinition auxItem = null; // 보조 아이콘으로 보여줄 아이템(없으면 null)

        if (active != null && active.activeEffect is ReplayLastFoodEffect)
        {
            if (playerFoodItem == null) playerFoodItem = playerActiveItem.GetComponent<PlayerFoodItem>();
            if (playerFoodItem != null) auxItem = playerFoodItem.LastUsedFood;
        }
        else if (active != null && active.activeEffect is ReplayLastDisposableEffect)
        {
            // [85번] 직전에 소모한 일회용의 아이콘
            if (playerDisposableItem == null) playerDisposableItem = playerActiveItem.GetComponent<PlayerDisposableItem>();
            if (playerDisposableItem != null) auxItem = playerDisposableItem.LastUsedDisposable;
        }

        if (!auxIconDirty && auxItem == shownAuxItem) return;

        shownAuxItem = auxItem;
        auxIconDirty = false;

        if (auxItem == null)
        {
            auxIconImage.enabled = false;
            return;
        }

        auxIconImage.enabled = true;
        if (auxItem.icon != null)
        {
            auxIconImage.sprite = auxItem.icon;
            auxIconImage.color = Color.white;
        }
        else
        {
            auxIconImage.sprite = null;
            auxIconImage.color = missingItemIconColor;
        }
    }

    private void ApplyCostTextIfDirty()
    {
        if (!costTextDirty) return;
        costTextDirty = false;

        boxText.text = shownCost.ToString();
    }

    // ------------------------------------------------------------------
    // 매 프레임 반영하는 레이아웃 (인스펙터 값 라이브 트윅)
    // ------------------------------------------------------------------

    // 초상화 바로 아래, 좌상단 기준으로 그룹 전체의 위치를 잡습니다.
    private void ApplyRootPosition()
    {
        rootRect.anchoredPosition = new Vector2(
            marginX,
            -(hudManager.topLeftMarginY + hudManager.portraitSize + gapBelowPortrait));

        int slotCount = slotImages.Count;
        float barRight = barOffset.x + slotCount * slotWidth + Mathf.Max(0, slotCount - 1) * slotSpacing;
        rootRect.sizeDelta = new Vector2(Mathf.Max(starSize, barRight), starSize);
    }

    private void ApplyLayoutAndVisuals()
    {
        // ---- 1. 별 + 겹쳐지는 아이템 아이콘 ----
        SetTopLeftRect(starRect, Vector2.zero, new Vector2(starSize, starSize));
        ApplySprite(starImage, activeFrameSprite, activeFramePlaceholderColor);

        Vector2 starCenter = new Vector2(starSize * 0.5f, -starSize * 0.5f);
        SetCenteredRect(itemIconRect, starCenter + itemIconOffset, new Vector2(itemIconSize, itemIconSize));
        SetCenteredRect(cooldownOverlayRect, starCenter + itemIconOffset, new Vector2(itemIconSize, itemIconSize));

        // ---- 1-2. 추가 보조 아이콘 (별의 왼쪽 아래 모서리 기준) ----
        Vector2 starBottomLeft = new Vector2(0f, -starSize);
        SetCenteredRect(auxIconRect, starBottomLeft + auxIconOffset, new Vector2(auxIconSize, auxIconSize));

        // ---- 2. 빨간 원 + 소모 코스트 숫자 ----
        Vector2 starBottomRight = new Vector2(starSize, -starSize);
        SetCenteredRect(badgeRect, starBottomRight + badgeOffset, new Vector2(badgeSize, badgeSize));
        ApplySprite(badgeImage, costBadgeSprite, costBadgePlaceholderColor);
        badgeText.fontSize = badgeFontSize;
        badgeText.color = badgeFontColor;

        // ---- 3. 보라 사각형 + 현재 코스트 숫자 ----
        SetTopLeftRect(boxRect, boxOffset, new Vector2(boxSize, boxSize));
        ApplySprite(boxImage, currentCostBoxSprite, currentCostBoxPlaceholderColor);
        boxText.fontSize = boxFontSize;
        boxText.color = boxFontColor;
        // 글자 영역은 사각형을 꽉 채우고 있으므로, 양쪽 끝을 같은 값만큼 밀면 숫자만 이동합니다.
        boxText.rectTransform.offsetMin = boxTextOffset;
        boxText.rectTransform.offsetMax = boxTextOffset;

        // ---- 4. 코스트 칸 (가진 만큼 왼쪽부터 채워진 스프라이트) ----
        for (int i = 0; i < slotImages.Count; i++)
        {
            float x = barOffset.x + i * (slotWidth + slotSpacing);
            SetTopLeftRect(slotRects[i], new Vector2(x, barOffset.y), new Vector2(slotWidth, slotHeight));

            bool filled = i < shownCost;
            if (filled) ApplySprite(slotImages[i], costFilledSprite, costFilledPlaceholderColor);
            else ApplySprite(slotImages[i], costEmptySprite, costEmptyPlaceholderColor);
        }

        // ---- 5. SPACE 안내창 (그림만 - 스프라이트가 있을 때만 표시) ----
        bool hasHint = spaceHintSprite != null;
        if (spaceHintImage.enabled != hasHint) spaceHintImage.enabled = hasHint;
        if (hasHint)
        {
            Vector2 hintSize = spaceHintSize;
            if (hintSize.x <= 0f || hintSize.y <= 0f) hintSize = spaceHintSprite.rect.size;

            spaceHintImage.sprite = spaceHintSprite;
            spaceHintImage.color = Color.white;
            // 왼쪽 아래 모서리가 첫 번째 코스트 칸의 왼쪽 위 모서리(barOffset) 기준이므로, 왼쪽 위
            // 모서리 좌표는 그 위로 그림의 높이만큼 올라간 곳입니다.
            SetTopLeftRect(spaceHintRect,
                new Vector2(barOffset.x + spaceHintOffset.x, barOffset.y + hintSize.y + spaceHintOffset.y),
                hintSize);
        }
    }

    // 스프라이트가 있으면 원본 색(흰색 = 색 변형 없음), 없으면 자리표시 색의 단색 사각형으로 보여줍니다.
    private void ApplySprite(Image image, Sprite sprite, Color placeholderColor)
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

    // 왼쪽 위 모서리 기준으로 위치를 잡습니다(그룹 루트의 왼쪽 위가 원점, 아래쪽이 -y).
    private void SetTopLeftRect(RectTransform rect, Vector2 topLeft, Vector2 size)
    {
        rect.sizeDelta = size;
        rect.anchoredPosition = topLeft;
    }

    // 중심 좌표 기준으로 위치를 잡습니다(pivot이 왼쪽 위이므로 절반 크기만큼 보정).
    private void SetCenteredRect(RectTransform rect, Vector2 center, Vector2 size)
    {
        rect.sizeDelta = size;
        rect.anchoredPosition = new Vector2(center.x - size.x * 0.5f, center.y + size.y * 0.5f);
    }

    // ------------------------------------------------------------------
    // 오브젝트 생성 (한 번만)
    // ------------------------------------------------------------------

    private void EnsureBuilt()
    {
        if (built) return;

        BuildRoot(hudManager.CanvasRoot);
        built = true;

        // 만들어진 직후에 현재 상태를 한 번 화면에 반영하도록 표시해 둡니다.
        shownCost = playerResources.GetAmount(BasicItemType.Cost);
        costTextDirty = true;
        activeDirty = true;
        auxIconDirty = true;
    }

    private void BuildRoot(Transform canvasRoot)
    {
        root = new GameObject("CostDisplay");
        root.transform.SetParent(canvasRoot, false);

        rootRect = root.AddComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0f, 1f);
        rootRect.anchorMax = new Vector2(0f, 1f);
        rootRect.pivot = new Vector2(0f, 1f);

        // 그리는 순서: 별 -> 아이템 아이콘(별 위) -> 쿨타임 덮개 -> 추가 보조 아이콘 -> 빨간 원 -> 보라 사각형 -> 코스트 칸
        starImage = CreateImage("Star", root.transform, out starRect);
        itemIconImage = CreateImage("ActiveItemIcon", root.transform, out itemIconRect);
        itemIconImage.preserveAspect = true;
        itemIconImage.enabled = false;

        // [74번 등] 쿨타임 덮개 - 아이템 아이콘 바로 위에 같은 크기로 겹칩니다(장전 R 표시와 같은 원형 채우기 설정).
        cooldownOverlayImage = CreateImage("ActiveCooldownOverlay", root.transform, out cooldownOverlayRect);
        cooldownOverlayImage.preserveAspect = true;
        cooldownOverlayImage.type = Image.Type.Filled;
        cooldownOverlayImage.fillMethod = Image.FillMethod.Radial360;
        cooldownOverlayImage.fillOrigin = (int)Image.Origin360.Top;
        cooldownOverlayImage.fillClockwise = false;
        cooldownOverlayImage.fillAmount = 1f;
        cooldownOverlayImage.enabled = false;

        auxIconImage = CreateImage("AuxIcon", root.transform, out auxIconRect);
        auxIconImage.preserveAspect = true;
        auxIconImage.enabled = false;

        badgeImage = CreateImage("ActiveCostBadge", root.transform, out badgeRect);
        badgeText = CreateStretchedText("CostText", badgeRect);
        badgeText.enabled = false;

        boxImage = CreateImage("CurrentCostBox", root.transform, out boxRect);
        boxText = CreateStretchedText("CurrentCostText", boxRect);
        boxText.text = "0";

        // SPACE 안내창은 코스트 칸과 겹칠 일은 없지만, 어긋나게 옮겼을 때도 그림이 위에 보이도록
        // 마지막에 만들고(코스트 칸이 새로 생길 때마다 다시 맨 위로 옮깁니다).
        spaceHintImage = CreateImage("SpaceHint", root.transform, out spaceHintRect);
        spaceHintImage.enabled = false;
    }

    private void CreateSlot(int index)
    {
        var image = CreateImage($"CostSlot_{index}", root.transform, out RectTransform rect);
        slotImages.Add(image);
        slotRects.Add(rect);

        if (spaceHintRect != null) spaceHintRect.SetAsLastSibling();
    }

    private Image CreateImage(string objectName, Transform parent, out RectTransform rect)
    {
        var go = new GameObject(objectName);
        go.transform.SetParent(parent, false);

        rect = go.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);

        var image = go.AddComponent<Image>();
        image.raycastTarget = false; // HUD가 마우스 입력을 가로채지 않도록
        return image;
    }

    // 부모(원/사각형) 전체를 꽉 채우는 가운데 정렬 텍스트를 만듭니다. 부모의 크기가 바뀌어도
    // 숫자는 항상 정중앙에 옵니다.
    private TextMeshProUGUI CreateStretchedText(string objectName, RectTransform parent)
    {
        var go = new GameObject(objectName);
        go.transform.SetParent(parent, false);

        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        // [TMP] 옛 방식 Text는 화면 배율에 따라 흐릿하게 보여서 TextMeshPro로 교체했습니다(HUDText 참고).
        // 예전에는 이미 굵은 글꼴(경기천년제목_Bold)에 Bold를 한 번 더 걸어 테두리가 뭉개졌기 때문에, 추가 Bold는 뺐습니다.
        return HUDText.AddTo(go, TextAnchor.MiddleCenter);
    }
}
