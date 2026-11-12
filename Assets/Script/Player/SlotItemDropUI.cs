using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// [슬롯 아이템 버리기 창 (형빈 확정)]
// Ctrl(왼쪽/오른쪽)을 꾹 누르면(기본 0.8초, 차오르는 표시 없음) 화면 전체가 반투명 검정으로 덮이고, 흰 창에 지금 가진
// 장신구/일회용/음식 칸이 보입니다. 원하는 아이템을 골라 [버리기]를 누르면 그 아이템이 지금 방 바닥에 좌대로 놓입니다.
// 실제 판정/자리 찾기/버리기는 SlotItemDropper가 하고, 이 스크립트는 화면과 입력만 맡습니다. 조절 값은 SlotDropSettings 에셋.
//
// [열 수 있을 때]
// 전투가 없는 방(또는 이미 클리어한 전투방)에 있고, 장신구/일회용/음식 중 하나라도 가지고 있을 때만 열립니다. 상점 창/슬롯 교체
// 확인창/다음 레벨 이동 확인창/게임 중단 화면/아이템 획득 연출/로딩 화면 중이거나 플레이어가 죽었으면 Ctrl을 눌러도 아무 일도 없습니다. 액티브 아이템은 대상이 아닙니다.
// 한 번 열렸다 닫히면 Ctrl을 뗐다가 다시 꾹 눌러야 다시 열립니다.
//
// [첫 창 - 흰 창]
// - 칸 배경/칸 틀/칸 배치는 화면 오른쪽 슬롯 HUD(PlayerSlotItemDisplay)의 값을 그대로 가져와 키워서 보여줍니다.
// - 지연 소모 대기 중인 아이템도 여기서는 선명하게 보입니다.
// - 아이콘에 커서를 올리면 아이템 설명칸이 뜹니다(ItemTooltipDisplay가 TryGetHoveredItem을 읽음).
// - 아이콘을 클릭하면 선택(반투명 + 작아짐), 다시 클릭하면 선택 해제. 여러 개를 고를 수 있습니다.
// - [버리기]는 하나 이상 골랐을 때만 눌립니다. [취소하기] 또는 Esc = 아무 일 없이 닫힘.
//
// [버리기를 누르면]
// 1. 고른 것 중 "교체 시 확인"(confirmBeforeReplace, 예: 20번) 아이템이 있으면 작은 확인 창("정말 버리시겠습니까?" + 그 아이템
//    이름 판)이 하나 더 뜹니다. [취소하기]/Esc = 첫 창으로 돌아감(고른 상태 그대로). [버리기] = 다음 단계로.
// 2. 지금 방에 고른 개수만큼 놓을 자리가 있는지 확인합니다. 모자라면 창을 닫고 작은 창 "현재 방에서 버릴 수 없습니다"를
//    띄웁니다([확인]/Enter/Esc로 닫힘). 이때는 아무것도 버려지지 않습니다.
// 3. 자리가 있으면 창을 모두 닫고 고른 아이템을 슬롯에서 빼서 각 자리에 좌대로 놓습니다.
//
// [창이 떠 있는 동안 - SlotReplaceConfirmUI와 같은 규칙]
// 게임은 멈추지 않고 플레이어의 이동/조준/공격/장전/무기 전환/아이템 사용만 막습니다. 좌클릭 공격(HUDClickBlocker)과 상자/포탈/
// 좌대/상인의 F(InteractionTargetSelector)도 막습니다(닫힌 그 프레임 포함). 피해를 입으면 아무것도 버리지 않고 모든 창이 닫히고,
// 죽으면 창만 닫습니다.
//
// [색/모양]
// 창/버튼은 공용 부품 UIPatternBox(흰 창 + 모서리 무늬, 평행사변형 버튼 - 떼는 순간 클릭)로 그리고, 색/모양/글자 색은 PopupStyle에서
// 읽습니다. 첫 창 [버리기]/[취소하기] = 하늘색, 확인 창 [버리기] = 노랑 / [취소하기] = 하늘색, 버릴 수 없음 창 [확인] = 노랑.
// 아이템 이름은 등급 색 + 이름 판(UINamePlate). 아이템 칸 선택은 예전처럼 누르는 순간입니다.
//
// [설정]
// 씬에 붙일 필요가 없습니다 - 게임이 시작되면 스스로 만들어집니다(씬이 바뀌어도 유지).
public class SlotItemDropUI : MonoBehaviour
{
    private enum Mode { Closed, Main, Confirm, Message }

    // ------------------------------------------------------------------
    // 외부에서 쓰는 정적 진입점
    // ------------------------------------------------------------------

    private static SlotItemDropUI instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null) return;
        var go = new GameObject("SlotItemDropUI");
        DontDestroyOnLoad(go);
        go.AddComponent<SlotItemDropUI>();
    }

    // 버리기 창(첫 창/확인 창/버릴 수 없음 창 중 하나)이 떠 있는지
    public static bool IsOpen => instance != null && instance.mode != Mode.Closed;

    // 상자/포탈/좌대/상인의 F, 공격 좌클릭을 막아야 하는지(창이 떠 있거나, 방금 닫힌 그 프레임)
    public static bool BlocksInteract =>
        instance != null && (instance.mode != Mode.Closed || instance.closedFrame == Time.frameCount);

    // 이 창의 Canvas 정렬 순서(아이템 설명칸이 이 창 위에 뜨도록 ItemTooltipDisplay가 읽음)
    public static int SortingOrder => SlotDropSettings.Get().canvasSortingOrder;

    // 지금 마우스가 첫 창의 아이템 칸 위에 있으면 그 아이템(설명칸용). 확인 창/버릴 수 없음 창이 떠 있으면 없음.
    public static bool TryGetHoveredItem(out ItemDefinition item)
    {
        item = null;
        if (instance == null || instance.mode != Mode.Main) return false;
        int index = instance.GetHoveredSlotIndex();
        if (index < 0) return false;
        item = instance.slotItems[index];
        return item != null;
    }

    // ------------------------------------------------------------------
    // 인스턴스 상태
    // ------------------------------------------------------------------

    private const int CategoryCount = SlotItemDropper.SlotKindCount;
    private const int SlotsPerCategory = 2; // 슬롯 HUD와 같은 6칸(종류마다 2칸 - 장신구는 55번, 일회용은 61번, 음식은 79번으로 2칸까지)

    private Mode mode = Mode.Closed;
    private int openedFrame = -1;    // 창(또는 다음 창)이 열린 프레임 - 그 프레임의 입력은 무시
    private int closedFrame = -1;

    private float holdTimer;
    private bool holdConsumed;       // 이번 Ctrl 누름으로 이미 열었음(떼야 다시 열 수 있음)

    private GameObject player;
    private PlayerHealth watchedHealth;
    private readonly List<Behaviour> disabledBehaviours = new List<Behaviour>();

    // 칸마다 지금 들어 있는 아이템(첫 칸만 채워짐)과 선택 여부. 인덱스 = 종류 * 2 + k
    private readonly ItemDefinition[] slotItems = new ItemDefinition[CategoryCount * SlotsPerCategory];
    private readonly bool[] selected = new bool[CategoryCount * SlotsPerCategory];

    // ---------------- UI ----------------
    private class SlotView
    {
        public RectTransform frameRect;
        public Image frame;
        public RectTransform iconRect;
        public Image icon;
    }

    private class ButtonView
    {
        public RectTransform rect;
        public UIPatternBox box;
        public TextMeshProUGUI label;
        public UIButtonKind kind = UIButtonKind.Normal;
    }

    private GameObject canvasGO;
    private Canvas canvas;
    private CanvasScaler scaler;

    private GameObject dimGO;
    private GameObject confirmDimGO; // 확인 창이 뜰 때 첫 창을 한 번 더 어둡게 덮는 막(흰 창 위 흰 창이 묻히지 않도록)
    private GameObject mainRoot;
    private RectTransform mainRect;
    private UIPatternBox windowBox;
    private TextMeshProUGUI titleText;
    private RectTransform slotPanelRect;
    private Image slotPanelImage;
    private readonly SlotView[] slotViews = new SlotView[CategoryCount * SlotsPerCategory];
    private ButtonView dropButton;
    private ButtonView cancelButton;

    private GameObject darkRoot;
    private RectTransform darkRect;
    private UIPatternBox darkBox;
    private TextMeshProUGUI darkMessage;
    private RectTransform darkNamesRect;                                  // 확인 창의 아이템 이름 판들을 담는 곳
    private readonly List<UINamePlate> darkNamePlates = new List<UINamePlate>();
    private readonly List<ItemDefinition> darkNameItems = new List<ItemDefinition>();
    private ButtonView darkLeftButton;  // 확인 창: [버리기] / 버릴 수 없음 창: [확인]
    private ButtonView darkRightButton; // 확인 창: [취소하기] / 버릴 수 없음 창: 숨김

    // 슬롯 HUD 값(창이 열릴 때 읽어둠)
    private PlayerSlotItemDisplay slotHud;

    void Awake()
    {
        if (instance == null) instance = this;
        else if (instance != this)
        {
            Destroy(gameObject); // 하나만 씁니다
            return;
        }

        BuildUI();
        canvasGO.SetActive(false);
        PlayerHealth.OnAnyPlayerDied += HandlePlayerDied;
    }

    void OnDestroy()
    {
        if (instance != this) return;
        PlayerHealth.OnAnyPlayerDied -= HandlePlayerDied;
        UnwatchDamage();
        if (mode != Mode.Closed) RestorePlayerControls();
        instance = null;
    }

    void Update()
    {
        if (mode == Mode.Closed)
        {
            UpdateHoldToOpen();
            return;
        }

        // 창이 떠 있는 동안 씬이 바뀌어 플레이어가 사라졌으면 그냥 닫습니다.
        if (player == null)
        {
            ForceCloseWithoutRestore();
            return;
        }

        ApplyLayout();

        switch (mode)
        {
            case Mode.Main: UpdateMain(); break;
            case Mode.Confirm: UpdateConfirm(); break;
            case Mode.Message: UpdateMessage(); break;
        }
    }

    // ------------------------------------------------------------------
    // Ctrl 꾹 눌러 열기
    // ------------------------------------------------------------------

    private void UpdateHoldToOpen()
    {
        bool ctrlHeld = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        if (!ctrlHeld)
        {
            holdTimer = 0f;
            holdConsumed = false;
            return;
        }
        if (holdConsumed) return;

        // 지금 열 수 없는 상태면 누르고 있던 시간도 쌓지 않습니다(조건이 풀린 뒤부터 다시 0.8초).
        if (!CanOpenNow(out GameObject livePlayer))
        {
            holdTimer = 0f;
            return;
        }

        holdTimer += Time.unscaledDeltaTime;
        if (holdTimer < SlotDropSettings.Get().holdDuration) return;

        holdConsumed = true;
        holdTimer = 0f;
        OpenMain(livePlayer);
    }

    private static bool CanOpenNow(out GameObject livePlayer)
    {
        livePlayer = null;
        if (ShopUI.IsOpen || ShopUI.BlocksInteract) return false;
        if (SlotReplaceConfirmUI.IsOpen || SlotReplaceConfirmUI.BlocksInteract) return false;
        if (LevelTransitionConfirmUI.IsOpen || LevelTransitionConfirmUI.BlocksInteract) return false;
        if (PauseMenuUI.IsOpen || PauseMenuUI.BlocksInteract) return false;
        if (PassiveRerollUI.IsOpen || PassiveRerollUI.BlocksInteract) return false;
        if (PlayerItemPickupPresenter.BlocksActions) return false;
        if (LoadingScreen.IsShowing) return false;
        if (!SlotItemDropper.TryGetLivePlayer(out livePlayer)) return false;
        if (!SlotItemDropper.HasAnySlotItem(livePlayer)) return false;
        return SlotItemDropper.IsInCombatFreeRoom();
    }

    // ------------------------------------------------------------------
    // 열기 / 닫기
    // ------------------------------------------------------------------

    private void OpenMain(GameObject livePlayer)
    {
        player = livePlayer;
        slotHud = Object.FindFirstObjectByType<PlayerSlotItemDisplay>();

        for (int i = 0; i < selected.Length; i++) selected[i] = false;
        RefreshSlotItems();

        var health = player.GetComponent<PlayerHealth>();
        DisablePlayerControls(health);
        WatchDamage(health);

        SetMode(Mode.Main);
    }

    private void SetMode(Mode newMode)
    {
        mode = newMode;
        openedFrame = Time.frameCount;

        canvasGO.SetActive(true);
        // 확인 창은 첫 창 위에 겹쳐 뜨고(뒤에 첫 창이 보임), 버릴 수 없음 창은 첫 창을 닫은 뒤 혼자 뜹니다.
        dimGO.SetActive(newMode != Mode.Message);
        mainRoot.SetActive(newMode == Mode.Main || newMode == Mode.Confirm);
        darkRoot.SetActive(newMode == Mode.Confirm || newMode == Mode.Message);
        confirmDimGO.SetActive(newMode == Mode.Confirm);

        if (newMode == Mode.Confirm || newMode == Mode.Message) FillDarkPopup(newMode);
        ApplyLayout();
    }

    // 모든 창을 닫고 조작을 되돌립니다(아무것도 버리지 않음).
    private void CloseAll()
    {
        if (mode == Mode.Closed) return;
        mode = Mode.Closed;
        closedFrame = Time.frameCount;
        canvasGO.SetActive(false);

        UnwatchDamage();
        RestorePlayerControls();
        player = null;
    }

    // 플레이어 사망/사라짐: 창만 닫습니다(조작 해제는 PlayerDeathHandler 몫이라 되돌리지 않음).
    private void ForceCloseWithoutRestore()
    {
        if (mode == Mode.Closed) return;
        mode = Mode.Closed;
        closedFrame = Time.frameCount;
        UnwatchDamage();
        disabledBehaviours.Clear();
        canvasGO.SetActive(false);
        player = null;
    }

    private void HandlePlayerDied(PlayerHealth health)
    {
        ForceCloseWithoutRestore();
    }

    // 창이 떠 있는 동안 피해를 입으면 아무것도 버리지 않고 모든 창을 닫습니다.
    private void HandleDamageTaken(float amount)
    {
        CloseAll();
    }

    private void WatchDamage(PlayerHealth health)
    {
        UnwatchDamage();
        if (health == null) return;
        watchedHealth = health;
        watchedHealth.OnDamageTaken += HandleDamageTaken;
    }

    private void UnwatchDamage()
    {
        if (watchedHealth != null) watchedHealth.OnDamageTaken -= HandleDamageTaken;
        watchedHealth = null;
    }

    // ------------------------------------------------------------------
    // 첫 창
    // ------------------------------------------------------------------

    private int lastHoveredButton = -1; // [사운드] 호버음용 - 마지막으로 마우스가 올라간 버튼 번호

    private void UpdateMain()
    {
        RefreshSlotItems();

        bool canDrop = AnySelected();
        bool overDrop = canDrop && IsPointerOver(dropButton.rect);
        bool overCancel = IsPointerOver(cancelButton.rect);

        bool clickDrop = dropButton.box.TickButton(overDrop, canDrop);
        bool clickCancel = cancelButton.box.TickButton(overCancel, true);
        dropButton.label.color = dropButton.box.GetLabelColor(canDrop);
        cancelButton.label.color = cancelButton.box.GetLabelColor(true);
        UISound.TrackHover(ref lastHoveredButton, UISound.HoveredId(0, overDrop, overCancel), Time.frameCount == openedFrame);

        if (Time.frameCount == openedFrame) return;

        if (Input.GetKeyDown(KeyCode.Escape)) { UISound.Cancel(); CloseAll(); return; }

        if (clickDrop) { UISound.Confirm(); OnDropPressed(); return; }
        if (clickCancel) { UISound.Cancel(); CloseAll(); return; }

        // 아이템 칸 선택/해제는 누르는 순간
        if (!Input.GetMouseButtonDown(0)) return;
        int hovered = GetHoveredSlotIndex();
        if (hovered >= 0)
        {
            selected[hovered] = !selected[hovered];
            UISound.Confirm();
        }
    }

    // 칸마다 지금 장착 중인 아이템을 다시 읽습니다. 창이 떠 있는 동안 아이템이 사라졌으면(지연 소모가 끝나는 등) 선택도 풉니다.
    private void RefreshSlotItems()
    {
        for (int category = 0; category < CategoryCount; category++)
        {
            for (int k = 0; k < SlotsPerCategory; k++)
            {
                int index = category * SlotsPerCategory + k;
                // 장신구(55번)/일회용(61번)/음식(79번)은 2칸일 수 있어 k번째 칸까지 읽습니다.
                ItemDefinition item = SlotItemDropper.GetEquipped(player, (SlotItemDropper.SlotKind)category, k);
                if (item != slotItems[index]) selected[index] = false;
                slotItems[index] = item;
            }
        }
    }

    private bool AnySelected()
    {
        for (int i = 0; i < selected.Length; i++)
        {
            if (selected[i] && slotItems[i] != null) return true;
        }
        return false;
    }

    private int GetHoveredSlotIndex()
    {
        for (int i = 0; i < slotViews.Length; i++)
        {
            if (slotItems[i] == null) continue;
            if (IsPointerOver(slotViews[i].frameRect)) return i;
        }
        return -1;
    }

    private void OnDropPressed()
    {
        if (!AnySelected()) return;

        foreach (var item in SelectedItems())
        {
            if (SlotReplaceConfirmUI.NeedsConfirm(item))
            {
                SetMode(Mode.Confirm);
                return;
            }
        }
        ExecuteDrop();
    }

    private List<ItemDefinition> SelectedItems()
    {
        var list = new List<ItemDefinition>();
        for (int i = 0; i < selected.Length; i++)
        {
            if (selected[i] && slotItems[i] != null) list.Add(slotItems[i]);
        }
        return list;
    }

    // 고른 개수만큼 자리를 찾고, 다 찾으면 창을 닫고 버립니다. 모자라면 첫 창을 닫고 "현재 방에서 버릴 수 없습니다".
    private void ExecuteDrop()
    {
        var kinds = new List<SlotItemDropper.SlotKind>();
        var items = new List<ItemDefinition>();
        for (int i = 0; i < selected.Length; i++)
        {
            if (selected[i] && slotItems[i] != null)
            {
                kinds.Add((SlotItemDropper.SlotKind)(i / SlotsPerCategory));
                items.Add(slotItems[i]);
            }
        }
        if (kinds.Count == 0) { SetMode(Mode.Main); return; }

        if (!SlotItemDropper.TryFindDropPositions(player, kinds.Count, out List<Vector3> positions, out Transform parent))
        {
            SetMode(Mode.Message);
            return;
        }

        GameObject dropPlayer = player;
        CloseAll(); // 조작을 먼저 되돌린 뒤 버립니다(SlotReplaceConfirmUI와 같은 순서)

        for (int i = 0; i < kinds.Count; i++)
        {
            if (!SlotItemDropper.TryUnequip(dropPlayer, kinds[i], items[i], out ItemDefinition removed) || removed == null) continue;
            SlotItemDropper.SpawnDroppedPedestal(removed, positions[i], parent);
            Debug.Log($"SlotItemDropUI: '{removed.displayName}'을(를) 버렸습니다.");
        }
    }

    // ------------------------------------------------------------------
    // 검은 창 (확인 / 버릴 수 없음)
    // ------------------------------------------------------------------

    private void UpdateConfirm()
    {
        bool overLeft = IsPointerOver(darkLeftButton.rect);
        bool overRight = IsPointerOver(darkRightButton.rect);
        bool clickLeft = darkLeftButton.box.TickButton(overLeft, true);
        bool clickRight = darkRightButton.box.TickButton(overRight, true);

        // 뒤에 보이는 첫 창의 버튼은 호버/누름 표시 없이 평소 색으로
        SetIdle(dropButton, AnySelected());
        SetIdle(cancelButton, true);
        UISound.TrackHover(ref lastHoveredButton, UISound.HoveredId(10, overLeft, overRight), Time.frameCount == openedFrame);

        if (Time.frameCount == openedFrame) return;

        // Esc / [취소하기] = 첫 창으로(고른 상태 그대로)
        if (Input.GetKeyDown(KeyCode.Escape)) { UISound.Cancel(); SetMode(Mode.Main); return; }

        if (clickLeft) { UISound.Confirm(); ExecuteDrop(); }
        else if (clickRight) { UISound.Cancel(); SetMode(Mode.Main); }
    }

    private void UpdateMessage()
    {
        bool overOk = IsPointerOver(darkLeftButton.rect);
        bool clickOk = darkLeftButton.box.TickButton(overOk, true);
        UISound.TrackHover(ref lastHoveredButton, UISound.HoveredId(20, overOk), Time.frameCount == openedFrame);

        if (Time.frameCount == openedFrame) return;

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Escape))
        {
            UISound.Confirm();
            CloseAll();
            return;
        }
        if (clickOk) { UISound.Confirm(); CloseAll(); }
    }

    private void FillDarkPopup(Mode popupMode)
    {
        var s = SlotDropSettings.Get();
        darkLeftButton.box.ResetPress();
        darkRightButton.box.ResetPress();
        darkNameItems.Clear();

        if (popupMode == Mode.Confirm)
        {
            darkMessage.text = s.confirmMessage;
            foreach (var item in SelectedItems())
            {
                if (SlotReplaceConfirmUI.NeedsConfirm(item)) darkNameItems.Add(item);
            }
            darkNamesRect.gameObject.SetActive(true);
            darkLeftButton.label.text = s.dropLabel;
            darkLeftButton.kind = UIButtonKind.Positive; // 재확인창의 실행 버튼
            darkRightButton.label.text = s.cancelLabel;
            darkRightButton.rect.gameObject.SetActive(true);
        }
        else
        {
            darkMessage.text = s.noSpaceMessage;
            darkNamesRect.gameObject.SetActive(false);
            darkLeftButton.label.text = s.okLabel;
            darkLeftButton.kind = UIButtonKind.Positive; // 알림 창 [확인]
            darkRightButton.rect.gameObject.SetActive(false);
        }
    }

    private static string ItemName(ItemDefinition item)
    {
        if (item == null) return "-";
        return string.IsNullOrEmpty(item.displayName) ? item.name : item.displayName;
    }

    // 아이템 이름 색 - 아이템 설명칸(PlayerItemInfoDisplay)의 등급별 이름 색을 그대로 씁니다(없으면 흰색).
    private static Color TierColor(ItemDefinition item)
    {
        Color color = Color.white;
        var info = Object.FindFirstObjectByType<PlayerItemInfoDisplay>();
        if (info != null)
        {
            if (!info.useTierNameColor) color = info.nameTextColor;
            else
            {
                switch (item.rarity)
                {
                    case ItemDefinition.ItemRarity.Tier0: color = info.tier0NameColor; break;
                    case ItemDefinition.ItemRarity.Tier1: color = info.tier1NameColor; break;
                    case ItemDefinition.ItemRarity.Tier2: color = info.tier2NameColor; break;
                    case ItemDefinition.ItemRarity.Tier3: color = info.tier3NameColor; break;
                }
            }
        }
        return color;
    }

    private static bool IsPointerOver(RectTransform rect)
    {
        if (rect == null || !rect.gameObject.activeInHierarchy) return false;
        return RectTransformUtility.RectangleContainsScreenPoint(rect, Input.mousePosition, null);
    }

    // ------------------------------------------------------------------
    // 플레이어 조작 잠금 (SlotReplaceConfirmUI와 같은 방식 - 게임은 계속 흐름)
    // ------------------------------------------------------------------

    private void DisablePlayerControls(PlayerHealth health)
    {
        disabledBehaviours.Clear();
        GameObject p = health != null ? health.gameObject : player;

        var movement = FindOnPlayer<PlayerMovement>(p);
        var aim = FindOnPlayer<PlayerAimAndAttack>(p);
        ResetLocomotionAnimator(movement != null ? movement.animator : null, aim != null);

        TryDisable(movement);
        TryDisable(aim);
        TryDisable(FindOnPlayer<PlayerShooting>(p));
        TryDisable(FindOnPlayer<PlayerWeaponMode>(p));
        TryDisable(FindOnPlayer<PlayerActiveItem>(p));
        TryDisable(FindOnPlayer<PlayerDisposableItem>(p));
        TryDisable(FindOnPlayer<PlayerFoodItem>(p));
    }

    private static T FindOnPlayer<T>(GameObject p) where T : Component
    {
        T c = p != null ? p.GetComponent<T>() : null;
        return c != null ? c : Object.FindFirstObjectByType<T>();
    }

    private static void ResetLocomotionAnimator(Animator animator, bool resetAiming)
    {
        if (animator == null || animator.runtimeAnimatorController == null) return;
        foreach (var p in animator.parameters)
        {
            if (p.type == AnimatorControllerParameterType.Float &&
                (p.name == "MoveX" || p.name == "MoveZ" || p.name == "Speed"))
                animator.SetFloat(p.name, 0f);
            else if (resetAiming && p.type == AnimatorControllerParameterType.Bool && p.name == "IsAiming")
                animator.SetBool(p.name, false);
        }
    }

    private void TryDisable(Behaviour behaviour)
    {
        if (behaviour == null || !behaviour.enabled) return;
        behaviour.enabled = false;
        disabledBehaviours.Add(behaviour);
    }

    private void RestorePlayerControls()
    {
        foreach (var b in disabledBehaviours)
        {
            if (b != null) b.enabled = true;
        }
        disabledBehaviours.Clear();
    }

    // ------------------------------------------------------------------
    // UI 생성
    // ------------------------------------------------------------------

    private void BuildUI()
    {
        canvasGO = new GameObject("SlotItemDropCanvas");
        canvasGO.transform.SetParent(transform, false);
        canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        // 화면 전체 반투명 검정
        dimGO = new GameObject("Dim");
        dimGO.transform.SetParent(canvasGO.transform, false);
        var dimRect = dimGO.AddComponent<RectTransform>();
        dimRect.anchorMin = Vector2.zero;
        dimRect.anchorMax = Vector2.one;
        dimRect.offsetMin = Vector2.zero;
        dimRect.offsetMax = Vector2.zero;
        var dimImage = dimGO.AddComponent<Image>();
        dimImage.raycastTarget = false;
        dimImage.color = SlotDropSettings.Get().dimColor;

        // 첫 창
        mainRoot = new GameObject("MainWindow");
        mainRoot.transform.SetParent(canvasGO.transform, false);
        mainRect = mainRoot.AddComponent<RectTransform>();
        mainRect.anchorMin = mainRect.anchorMax = new Vector2(0.5f, 0.5f);
        mainRect.pivot = new Vector2(0.5f, 0.5f);

        var windowGO = new GameObject("Window");
        windowGO.transform.SetParent(mainRect, false);
        var windowRect = windowGO.AddComponent<RectTransform>();
        windowRect.anchorMin = Vector2.zero;
        windowRect.anchorMax = Vector2.one;
        windowRect.offsetMin = Vector2.zero;
        windowRect.offsetMax = Vector2.zero;
        windowBox = UIPatternBox.AddTo(windowGO);

        var titleGO = new GameObject("Title");
        titleGO.transform.SetParent(mainRect, false);
        var titleRect = titleGO.AddComponent<RectTransform>();
        titleRect.anchorMin = titleRect.anchorMax = new Vector2(0.5f, 1f);
        titleRect.pivot = new Vector2(0.5f, 0.5f);
        titleText = HUDText.AddTo(titleGO, TextAnchor.MiddleCenter, true);

        // 슬롯 칸 묶음 - 슬롯 HUD와 같은 크기/배치로 만들고 통째로 배율을 키웁니다.
        var panelGO = new GameObject("SlotPanel");
        panelGO.transform.SetParent(mainRect, false);
        slotPanelRect = panelGO.AddComponent<RectTransform>();
        slotPanelRect.anchorMin = slotPanelRect.anchorMax = new Vector2(0.5f, 0.5f);
        slotPanelRect.pivot = new Vector2(0.5f, 0.5f);
        slotPanelImage = panelGO.AddComponent<Image>();
        slotPanelImage.raycastTarget = false;

        for (int i = 0; i < slotViews.Length; i++) slotViews[i] = BuildSlot(slotPanelRect, i);

        dropButton = CreateButton(mainRect, "DropButton");
        cancelButton = CreateButton(mainRect, "CancelButton");

        // 확인 창이 떠 있는 동안 첫 창 위를 덮는 막(첫 창과 작은 창 사이)
        confirmDimGO = new GameObject("ConfirmDim");
        confirmDimGO.transform.SetParent(canvasGO.transform, false);
        var confirmDimRect = confirmDimGO.AddComponent<RectTransform>();
        confirmDimRect.anchorMin = Vector2.zero;
        confirmDimRect.anchorMax = Vector2.one;
        confirmDimRect.offsetMin = Vector2.zero;
        confirmDimRect.offsetMax = Vector2.zero;
        var confirmDimImage = confirmDimGO.AddComponent<Image>();
        confirmDimImage.raycastTarget = false;
        confirmDimGO.SetActive(false);

        // 작은 흰 창(확인 / 버릴 수 없음)
        darkRoot = new GameObject("DarkPopup");
        darkRoot.transform.SetParent(canvasGO.transform, false);
        darkRect = darkRoot.AddComponent<RectTransform>();
        darkRect.anchorMin = darkRect.anchorMax = new Vector2(0.5f, 0.5f);
        darkRect.pivot = new Vector2(0.5f, 0.5f);
        darkBox = UIPatternBox.AddTo(darkRoot);

        darkMessage = CreateTopText(darkRect, "Message");
        var namesGO = new GameObject("ItemNames");
        namesGO.transform.SetParent(darkRect, false);
        darkNamesRect = namesGO.AddComponent<RectTransform>();
        darkNamesRect.anchorMin = darkNamesRect.anchorMax = new Vector2(0.5f, 1f);
        darkNamesRect.pivot = new Vector2(0.5f, 1f);
        darkLeftButton = CreateButton(darkRect, "LeftButton");
        darkRightButton = CreateButton(darkRect, "RightButton");
    }

    private static TextMeshProUGUI CreateTopText(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        return HUDText.AddTo(go, TextAnchor.MiddleCenter, true);
    }

    private SlotView BuildSlot(Transform parent, int index)
    {
        var slot = new SlotView();

        var frameGO = new GameObject($"Slot_{index / SlotsPerCategory}_{index % SlotsPerCategory}");
        frameGO.transform.SetParent(parent, false);
        slot.frameRect = frameGO.AddComponent<RectTransform>();
        slot.frameRect.anchorMin = slot.frameRect.anchorMax = new Vector2(0f, 1f);
        slot.frameRect.pivot = new Vector2(0f, 1f);
        slot.frame = frameGO.AddComponent<Image>();
        slot.frame.preserveAspect = true;
        slot.frame.raycastTarget = false;

        var iconGO = new GameObject("ItemIcon");
        iconGO.transform.SetParent(frameGO.transform, false);
        slot.iconRect = iconGO.AddComponent<RectTransform>();
        slot.iconRect.anchorMin = Vector2.zero;
        slot.iconRect.anchorMax = Vector2.one;
        slot.iconRect.pivot = new Vector2(0.5f, 0.5f);
        slot.icon = iconGO.AddComponent<Image>();
        slot.icon.preserveAspect = true;
        slot.icon.raycastTarget = false;
        slot.icon.enabled = false;

        return slot;
    }

    // 버튼: 평행사변형 면(UIPatternBox) + 글자
    private static ButtonView CreateButton(Transform parent, string name)
    {
        var view = new ButtonView();

        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        view.rect = go.AddComponent<RectTransform>();
        view.rect.anchorMin = view.rect.anchorMax = new Vector2(0.5f, 0f);
        view.rect.pivot = new Vector2(0.5f, 0f);
        view.box = UIPatternBox.AddTo(go);

        var labelGO = new GameObject("Label");
        labelGO.transform.SetParent(go.transform, false);
        var labelRect = labelGO.AddComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        view.label = HUDText.AddTo(labelGO, TextAnchor.MiddleCenter);

        return view;
    }

    // ------------------------------------------------------------------
    // 배치 (설정 값을 매 프레임 반영 - Play 중 에셋 값을 바꾸면 즉시 반영)
    // ------------------------------------------------------------------

    private void ApplyLayout()
    {
        var s = SlotDropSettings.Get();

        canvas.sortingOrder = s.canvasSortingOrder;
        if (Screen.height > 0)
        {
            float screenAspect = (float)Screen.width / Screen.height;
            scaler.matchWidthOrHeight = screenAspect > 1920f / 1080f ? 1f : 0f; // HUD와 같은 규칙
        }

        dimGO.GetComponent<Image>().color = s.dimColor;
        confirmDimGO.GetComponent<Image>().color = s.dimColor;

        if (mainRoot.activeSelf) LayoutMain(s);
        if (darkRoot.activeSelf) LayoutDark(s);
    }

    private void LayoutMain(SlotDropSettings s)
    {
        Vector2 size = new Vector2(Mathf.Max(200f, s.windowSize.x), Mathf.Max(150f, s.windowSize.y));
        mainRect.sizeDelta = size;
        mainRect.anchoredPosition = Vector2.zero;
        var style = PopupStyle.Get();
        windowBox.SetupPanel(style);

        var titleRect = titleText.rectTransform;
        titleRect.sizeDelta = new Vector2(size.x - 40f, s.titleFontSize + 16f);
        titleRect.anchoredPosition = new Vector2(0f, -s.titleTopMargin);
        titleText.text = s.title;
        titleText.fontSize = s.titleFontSize;
        titleText.color = style.panelTextColor;

        LayoutSlotPanel(s);

        float half = (s.mainButtonSize.x + s.mainButtonSpacing) * 0.5f;
        LayoutButton(dropButton, s.dropLabel, new Vector2(-half, s.mainButtonBottomMargin), s.mainButtonSize, s.mainButtonFontSize, style);
        LayoutButton(cancelButton, s.cancelLabel, new Vector2(half, s.mainButtonBottomMargin), s.mainButtonSize, s.mainButtonFontSize, style);
    }

    // 슬롯 HUD(PlayerSlotItemDisplay)의 배경/칸 틀/배치 값을 그대로 가져옵니다. 씬에 없으면 그 스크립트의 기본값과 같은 값.
    private void LayoutSlotPanel(SlotDropSettings s)
    {
        float panelWidth = slotHud != null ? slotHud.panelWidth : 366f;
        float panelHeight = slotHud != null ? slotHud.panelHeight : 165f;
        float paddingLeft = slotHud != null ? slotHud.paddingLeft : 12f;
        float slotRowTop = slotHud != null ? slotHud.slotRowTop : 77f;
        float slotSize = slotHud != null ? slotHud.slotSize : 50f;
        float slotSpacing = slotHud != null ? slotHud.slotSpacing : 6f;
        float groupSpacing = slotHud != null ? slotHud.groupSpacing : 12f;
        float iconInset = slotHud != null ? slotHud.itemIconInset : 5f;
        Sprite background = slotHud != null ? slotHud.backgroundSprite : null;
        Color panelColor = slotHud != null ? slotHud.panelColor : new Color(0f, 0f, 0f, 0.6f);
        Sprite frameSprite = slotHud != null ? slotHud.slotFrameSprite : null;
        Color framePlaceholder = slotHud != null ? slotHud.slotFramePlaceholderColor : new Color(1f, 1f, 1f, 0.15f);
        Color missingIcon = slotHud != null ? slotHud.missingItemIconColor : new Color(1f, 1f, 1f, 0.4f);
        // 잠긴 칸(55/61/79번이 없어 쓸 수 없는 두 번째 칸) 모양도 슬롯 HUD 값을 그대로 씁니다.
        float lockedScale = slotHud != null ? slotHud.lockedSlotScale : 0.75f;
        float lockedAlpha = slotHud != null ? slotHud.lockedSlotAlpha : 0.45f;

        slotPanelRect.sizeDelta = new Vector2(panelWidth, panelHeight);
        slotPanelRect.anchoredPosition = new Vector2(0f, s.slotPanelOffsetY);
        slotPanelRect.localScale = new Vector3(s.slotPanelScale, s.slotPanelScale, 1f);

        if (background != null)
        {
            slotPanelImage.sprite = background;
            slotPanelImage.color = Color.white;
        }
        else
        {
            slotPanelImage.sprite = null;
            slotPanelImage.color = panelColor;
        }

        float groupWidth = SlotsPerCategory * slotSize + (SlotsPerCategory - 1) * slotSpacing;

        for (int i = 0; i < slotViews.Length; i++)
        {
            int category = i / SlotsPerCategory;
            int k = i % SlotsPerCategory;
            var slot = slotViews[i];

            float x = paddingLeft + category * (groupWidth + groupSpacing) + k * (slotSize + slotSpacing);
            // 칸 중심을 기준점으로 둡니다(잠긴 칸이 가운데로 작아지도록). 위치는 예전(왼쪽 위 기준)과 같습니다.
            slot.frameRect.pivot = new Vector2(0.5f, 0.5f);
            slot.frameRect.sizeDelta = new Vector2(slotSize, slotSize);
            slot.frameRect.anchoredPosition = new Vector2(x + slotSize * 0.5f, -slotRowTop - slotSize * 0.5f);
            slot.iconRect.offsetMin = new Vector2(iconInset, iconInset);
            slot.iconRect.offsetMax = new Vector2(-iconInset, -iconInset);

            ItemDefinition item = slotItems[i];

            // 잠긴 칸: 칸 틀을 작고 반투명하게(창이 떠 있는 동안은 칸 수가 바뀌지 않으므로 연출 없이 그 상태로).
            bool locked = item == null && k >= GetUsableSlotCount(category);
            Color frameColor = frameSprite != null ? Color.white : framePlaceholder;
            if (frameSprite != null) slot.frame.sprite = frameSprite;
            else slot.frame.sprite = null;
            if (locked) frameColor.a *= lockedAlpha;
            slot.frame.color = frameColor;
            float frameScale = locked ? lockedScale : 1f;
            slot.frameRect.localScale = new Vector3(frameScale, frameScale, 1f);
            bool hasItem = item != null;
            if (slot.icon.enabled != hasItem) slot.icon.enabled = hasItem;
            if (!hasItem)
            {
                slot.iconRect.localScale = Vector3.one;
                continue;
            }

            // 지연 소모 대기 중이어도 여기서는 선명하게. 선택한 아이콘만 반투명 + 작게.
            Color color;
            if (item.icon != null)
            {
                slot.icon.sprite = item.icon;
                color = Color.white;
            }
            else
            {
                slot.icon.sprite = null;
                color = missingIcon;
            }
            if (selected[i]) color.a *= s.selectedAlpha;
            slot.icon.color = color;

            float scale = selected[i] ? s.selectedScale : 1f;
            slot.iconRect.localScale = new Vector3(scale, scale, 1f);
        }
    }

    // 종류마다 지금 쓸 수 있는 칸 수(기본 1칸, 55번 장신구/61번 일회용/79번 음식이 있으면 2칸). 컴포넌트가 없으면 1칸.
    private int GetUsableSlotCount(int category)
    {
        if (player == null) return 1;
        switch ((SlotItemDropper.SlotKind)category)
        {
            case SlotItemDropper.SlotKind.Trinket:
            {
                var c = player.GetComponent<PlayerTrinket>();
                return c != null ? c.SlotCount : 1;
            }
            case SlotItemDropper.SlotKind.Disposable:
            {
                var c = player.GetComponent<PlayerDisposableItem>();
                return c != null ? c.SlotCount : 1;
            }
            case SlotItemDropper.SlotKind.Food:
            {
                var c = player.GetComponent<PlayerFoodItem>();
                return c != null ? c.SlotCount : 1;
            }
        }
        return 1;
    }

    private void LayoutDark(SlotDropSettings s)
    {
        Vector2 size = mode == Mode.Confirm ? s.confirmSize : s.noSpaceSize;
        size = new Vector2(Mathf.Max(100f, size.x), Mathf.Max(80f, size.y));
        darkRect.sizeDelta = size;
        darkRect.anchoredPosition = Vector2.zero;
        var style = PopupStyle.Get();
        darkBox.SetupPanel(style);

        float pad = 20f;
        float buttonsTop = s.darkButtonBottomMargin + s.darkButtonSize.y;

        var msgRect = darkMessage.rectTransform;
        darkMessage.fontSize = s.darkMessageFontSize;
        darkMessage.color = style.panelTextColor;

        if (mode == Mode.Confirm)
        {
            msgRect.sizeDelta = new Vector2(size.x - pad * 2f, s.darkMessageFontSize + 12f);
            msgRect.anchoredPosition = new Vector2(0f, -pad - 4f);

            float detailTop = pad + 4f + s.darkMessageFontSize + 12f + 8f;
            float detailHeight = Mathf.Max(10f, size.y - detailTop - buttonsTop - 8f);
            darkNamesRect.sizeDelta = new Vector2(size.x - pad * 2f, detailHeight);
            darkNamesRect.anchoredPosition = new Vector2(0f, -detailTop);
            LayoutNamePlates(size.x - pad * 2f, detailHeight, s.darkDetailFontSize, style);

            float half = (s.darkButtonSize.x + s.darkButtonSpacing) * 0.5f;
            LayoutDarkButton(darkLeftButton, new Vector2(-half, s.darkButtonBottomMargin), s, style);
            LayoutDarkButton(darkRightButton, new Vector2(half, s.darkButtonBottomMargin), s, style);
        }
        else
        {
            // 버릴 수 없음 창: 문구는 버튼 위 남은 공간의 가운데
            msgRect.sizeDelta = new Vector2(size.x - pad * 2f, Mathf.Max(10f, size.y - buttonsTop - pad - 8f));
            msgRect.anchoredPosition = new Vector2(0f, -pad);
            LayoutDarkButton(darkLeftButton, new Vector2(0f, s.darkButtonBottomMargin), s, style);
        }
    }

    // 확인 창의 아이템 이름 판들을 이름 영역 가운데에 위아래로 쌓습니다(필요한 만큼 판을 만들어 둠).
    private void LayoutNamePlates(float maxWidth, float areaHeight, int fontSize, PopupStyle style)
    {
        while (darkNamePlates.Count < darkNameItems.Count)
            darkNamePlates.Add(UINamePlate.Create(darkNamesRect, "NamePlate" + darkNamePlates.Count));

        float plateHeight = UINamePlate.Height(fontSize, style);
        const float gap = 8f;
        float total = darkNameItems.Count * plateHeight + Mathf.Max(0, darkNameItems.Count - 1) * gap;
        float top = -(areaHeight - total) * 0.5f;
        for (int i = 0; i < darkNamePlates.Count; i++)
        {
            var plate = darkNamePlates[i];
            bool used = i < darkNameItems.Count;
            if (plate.rect.gameObject.activeSelf != used) plate.rect.gameObject.SetActive(used);
            if (!used) continue;

            plate.rect.anchorMin = plate.rect.anchorMax = new Vector2(0.5f, 1f);
            plate.rect.pivot = new Vector2(0.5f, 1f);
            plate.Fit(ItemName(darkNameItems[i]), TierColor(darkNameItems[i]), fontSize, maxWidth, style);
            plate.rect.anchoredPosition = new Vector2(0f, top - i * (plateHeight + gap));
        }
    }

    private void LayoutDarkButton(ButtonView button, Vector2 pos, SlotDropSettings s, PopupStyle style)
    {
        button.rect.sizeDelta = s.darkButtonSize;
        button.rect.anchoredPosition = pos;
        button.box.SetupButton(style, button.kind); // 색은 Update의 TickButton이 호버에 따라 정함
        button.label.fontSize = s.darkButtonFontSize;
        button.label.color = button.box.GetLabelColor(true);
    }

    private void LayoutButton(ButtonView button, string text, Vector2 pos, Vector2 size, int fontSize, PopupStyle style)
    {
        button.rect.sizeDelta = size;
        button.rect.anchoredPosition = pos;
        button.box.SetupButton(style, button.kind); // 색/글자 색은 Update의 TickButton이 호버/누를 수 있는지에 따라 정함
        button.label.text = text;
        button.label.fontSize = fontSize;
    }

    // 입력을 받지 않는 버튼: 호버/누름 표시 없이 평소 색(못 누르면 반투명)
    private static void SetIdle(ButtonView button, bool enabled)
    {
        button.box.ResetPress();
        button.box.color = PopupStyle.Get().GetKindColor(button.kind, false, enabled);
        button.label.color = button.box.GetLabelColor(enabled);
    }
}
