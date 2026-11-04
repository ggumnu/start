using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// [패시브 교체 창 - 56번 일회용 (형빈 확정)]
// 56번을 쓰면 화면이 반투명 검정으로 덮이고, 흰 창에 지금 가진 패시브 아이템 목록(화면 왼쪽 패시브 인벤토리와 같은 칸 배치/순서 -
// 최근 것이 앞)이 보입니다. 교체할 아이템을 하나 고르고 [교체하기]를 누르면 검은 확인 창("OO을(를) 교체하시겠습니까?")이 뜨고,
// 거기서 [교체하기]를 눌러야 교체됩니다. 실제 교체(후보 뽑기/잃기/얻기)는 56번 효과(Item56effect)가 하고, 이 창은 고르기만 맡습니다.
//
// [선택 창]
// - 아이콘을 클릭하면 선택(반투명 + 작아짐). 다른 아이콘을 클릭하면 이전 선택은 풀리고 그것이 선택됩니다(하나만).
// - 교체 후보가 없는 아이템(한 등급 낮은 패시브를 하나도 얻을 수 없음)은 어둡게 보이고 선택할 수 없습니다(크기/불투명도는 그대로라
//   선택한 아이콘과 구분됨).
// - 아이콘에 커서를 올리면 아이템 설명칸이 뜹니다(ItemTooltipDisplay가 TryGetHoveredItem을 읽음).
// - 칸이 많으면 마우스 휠로 스크롤합니다.
// - [교체하기]는 하나를 골랐을 때만 눌립니다. [취소하기] 또는 Esc = 아무 일 없이 닫힘(56번은 소모되지 않고 그대로).
//
// [확인 창 (검은 창)]
// [교체하기] = 교체 확정, [취소하기] 또는 Esc = 확인 창만 닫히고 선택 창은 그대로(고른 상태 유지).
//
// [사용할 수 없음 창 (검은 창)]
// 56번을 쓸 수 없을 때(교체할 수 있는 패시브가 없음) 이유를 알려주는 창입니다. 전투 중에는 창 없이 그냥 쓰이지 않습니다. [확인]/Enter/Esc로 닫힙니다.
//
// [창이 떠 있는 동안 - 다른 팝업과 같은 규칙]
// 게임은 멈추지 않고 플레이어의 이동/조준/공격/장전/무기 전환/아이템 사용만 막습니다. 좌클릭 공격(HUDClickBlocker)과 상자/포탈/좌대/
// 상인의 F(InteractionTargetSelector)도 막고(닫힌 그 프레임 포함), ESC 중단 화면/Ctrl 버리기 창도 열리지 않습니다. 피해를 입으면
// 아무것도 교체하지 않고 닫히고(56번 그대로), 죽으면 창만 닫습니다.
//
// [획득 연출]
// 교체가 끝나면 새로 얻은 아이템들의 획득 연출(ItemPickupEvents)을 하나씩 순서대로 보여줍니다(앞의 연출이 끝나면 다음 것).
//
// [설정]
// 씬에 붙일 필요가 없습니다 - 처음 필요할 때 스스로 만들어집니다(씬이 바뀌어도 유지). 문구/크기는 PassiveRerollSettings 에셋,
// 색/모서리는 PopupStyle 에셋.
public class PassiveRerollUI : MonoBehaviour
{
    private enum Mode { Closed, Main, Confirm, Message }

    // ------------------------------------------------------------------
    // 외부에서 쓰는 정적 진입점
    // ------------------------------------------------------------------

    private static PassiveRerollUI instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
    }

    private static PassiveRerollUI GetOrCreate()
    {
        if (instance == null)
        {
            var go = new GameObject("PassiveRerollUI");
            DontDestroyOnLoad(go);
            go.AddComponent<PassiveRerollUI>();
        }
        return instance;
    }

    // 창(선택 창/확인 창/사용할 수 없음 창 중 하나)이 떠 있는지
    public static bool IsOpen => instance != null && instance.mode != Mode.Closed;

    // 상자/포탈/좌대/상인의 F, 공격 좌클릭을 막아야 하는지(창이 떠 있거나, 방금 닫힌 그 프레임)
    public static bool BlocksInteract =>
        instance != null && (instance.mode != Mode.Closed || instance.closedFrame == Time.frameCount);

    // 이 창의 Canvas 정렬 순서(아이템 설명칸이 이 창 위에 뜨도록 ItemTooltipDisplay가 읽음)
    public static int SortingOrder => PassiveRerollSettings.Get().canvasSortingOrder;

    // 지금 마우스가 선택 창의 아이템 아이콘 위에 있으면 그 아이템(설명칸용). 확인 창/사용할 수 없음 창이 떠 있으면 없음.
    public static bool TryGetHoveredItem(out ItemDefinition item)
    {
        item = null;
        if (instance == null || instance.mode != Mode.Main) return false;
        int index = instance.GetHoveredIndex();
        if (index < 0) return false;
        item = instance.displayItems[index];
        return item != null;
    }

    // 선택 창을 엽니다. items = 보여줄 패시브(최근 것이 앞), selectable = 고를 수 있는 아이템. onConfirm은 확인 창에서 [교체하기]를
    // 눌렀을 때(고른 아이템), onCancel은 그 밖의 모든 닫힘(취소/Esc/피해)에서 한 번 호출됩니다. 이미 떠 있으면 false.
    public static bool OpenSelection(GameObject player, IReadOnlyList<ItemDefinition> items, ICollection<ItemDefinition> selectable,
                                     Action<ItemDefinition> onConfirm, Action onCancel)
    {
        var ui = GetOrCreate();
        if (ui == null || ui.mode != Mode.Closed || player == null) return false;
        ui.BeginSelection(player, items, selectable, onConfirm, onCancel);
        return true;
    }

    // 사용할 수 없는 이유를 알리는 검은 창을 엽니다([확인]/Enter/Esc로 닫힘). 이미 다른 창이 떠 있으면 false.
    public static bool ShowMessage(GameObject player, string message)
    {
        var ui = GetOrCreate();
        if (ui == null || ui.mode != Mode.Closed) return false;
        ui.BeginMessage(player, message);
        return true;
    }

    // 창을 강제로 닫습니다(콜백 없음). 56번이 대기 중에 다른 이유로 취소됐을 때 씁니다.
    public static void ForceClose()
    {
        if (instance != null) instance.CloseAll(false);
    }

    // 새로 얻은 아이템들의 획득 연출을 하나씩 순서대로 보여줍니다(앞의 연출이 끝나면 다음 것).
    public static void RaisePickupsInSequence(IReadOnlyList<ItemDefinition> items)
    {
        if (items == null || items.Count == 0) return;
        var ui = GetOrCreate();
        if (ui == null)
        {
            foreach (var item in items) ItemPickupEvents.Raise(item);
            return;
        }
        ui.StartCoroutine(ui.PickupSequence(new List<ItemDefinition>(items)));
    }

    // ------------------------------------------------------------------
    // 인스턴스 상태
    // ------------------------------------------------------------------

    private Mode mode = Mode.Closed;
    private int openedFrame = -1;
    private int closedFrame = -1;

    private GameObject player;
    private PlayerHealth watchedHealth;
    private readonly List<Behaviour> disabledBehaviours = new List<Behaviour>();

    private readonly List<ItemDefinition> displayItems = new List<ItemDefinition>();
    private readonly HashSet<ItemDefinition> selectableItems = new HashSet<ItemDefinition>();
    private int selectedIndex = -1;
    private float scrollOffset;
    private string messageText = "";

    private Action<ItemDefinition> pendingConfirm;
    private Action pendingCancel;

    // ---------------- UI ----------------
    private class ButtonView
    {
        public RectTransform rect;
        public Image outline;
        public Image image;
        public TextMeshProUGUI label;
    }

    private class IconView
    {
        public RectTransform rect;
        public Image image;
    }

    private GameObject canvasGO;
    private Canvas canvas;
    private CanvasScaler scaler;

    private GameObject dimGO;
    private Image dimImage;
    private GameObject mainRoot;
    private RectTransform mainRect;
    private Image windowOutline;
    private Image windowImage;
    private TextMeshProUGUI titleText;
    private RectTransform gridPanelRect;
    private Image gridPanelImage;
    private RectTransform viewportRect;
    private RectTransform contentRect;
    private readonly List<IconView> iconViews = new List<IconView>();
    private ButtonView confirmButton;
    private ButtonView cancelButton;

    private GameObject darkRoot;
    private RectTransform darkRect;
    private Image darkImage;
    private TextMeshProUGUI darkMessage;
    private ButtonView darkLeftButton;  // 확인 창: [교체하기] / 사용할 수 없음 창: [확인]
    private ButtonView darkRightButton; // 확인 창: [취소하기] / 사용할 수 없음 창: 숨김

    // 패시브 인벤토리 HUD(칸 배치 값)
    private PlayerInventoryDisplay inventoryHud;

    void Awake()
    {
        if (instance == null) instance = this;
        else if (instance != this)
        {
            Destroy(gameObject);
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
        if (mode == Mode.Closed) return;

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
    // 열기 / 닫기
    // ------------------------------------------------------------------

    private void BeginSelection(GameObject livePlayer, IReadOnlyList<ItemDefinition> items, ICollection<ItemDefinition> selectable,
                                Action<ItemDefinition> onConfirm, Action onCancel)
    {
        player = livePlayer;
        inventoryHud = UnityEngine.Object.FindFirstObjectByType<PlayerInventoryDisplay>();

        displayItems.Clear();
        if (items != null)
        {
            foreach (var item in items) if (item != null) displayItems.Add(item);
        }
        selectableItems.Clear();
        if (selectable != null)
        {
            foreach (var item in selectable) if (item != null) selectableItems.Add(item);
        }
        selectedIndex = -1;
        scrollOffset = 0f;
        pendingConfirm = onConfirm;
        pendingCancel = onCancel;

        EnsureIconViews(displayItems.Count);
        LockPlayer();
        SetMode(Mode.Main);
    }

    private void BeginMessage(GameObject livePlayer, string message)
    {
        player = livePlayer != null ? livePlayer : FindPlayer();
        messageText = message ?? "";
        pendingConfirm = null;
        pendingCancel = null;
        LockPlayer();
        SetMode(Mode.Message);
    }

    private static GameObject FindPlayer()
    {
        var health = UnityEngine.Object.FindFirstObjectByType<PlayerHealth>();
        return health != null ? health.gameObject : null;
    }

    private void LockPlayer()
    {
        var health = player != null ? player.GetComponent<PlayerHealth>() : null;
        DisablePlayerControls(health);
        WatchDamage(health);
    }

    private void SetMode(Mode newMode)
    {
        mode = newMode;
        openedFrame = Time.frameCount;

        canvasGO.SetActive(true);
        // 확인 창은 선택 창 위에 겹쳐 뜨고, 사용할 수 없음 창은 혼자 뜹니다(막 없이).
        dimGO.SetActive(newMode != Mode.Message);
        mainRoot.SetActive(newMode == Mode.Main || newMode == Mode.Confirm);
        darkRoot.SetActive(newMode == Mode.Confirm || newMode == Mode.Message);

        if (newMode == Mode.Confirm || newMode == Mode.Message) FillDarkPopup(newMode);
        ApplyLayout();
    }

    // 모든 창을 닫고 조작을 되돌립니다. invokeCancel = true면 취소 콜백을 부릅니다(56번 대기 해제 - 아이템 그대로).
    private void CloseAll(bool invokeCancel)
    {
        if (mode == Mode.Closed) return;
        mode = Mode.Closed;
        closedFrame = Time.frameCount;
        canvasGO.SetActive(false);

        UnwatchDamage();
        RestorePlayerControls();
        player = null;

        Action cancel = pendingCancel;
        pendingConfirm = null;
        pendingCancel = null;
        if (invokeCancel) cancel?.Invoke();
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
        pendingConfirm = null;
        pendingCancel = null;
    }

    private void HandlePlayerDied(PlayerHealth health)
    {
        ForceCloseWithoutRestore();
    }

    // 창이 떠 있는 동안 피해를 입으면 아무것도 교체하지 않고 닫습니다(56번 그대로).
    private void HandleDamageTaken(float amount)
    {
        CloseAll(true);
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
    // 선택 창
    // ------------------------------------------------------------------

    private void UpdateMain()
    {
        bool canConfirm = selectedIndex >= 0;
        bool overConfirm = canConfirm && IsPointerOver(confirmButton.rect);
        bool overCancel = IsPointerOver(cancelButton.rect);
        SetButtonColor(confirmButton, canConfirm, overConfirm);
        SetButtonColor(cancelButton, true, overCancel);

        // 마우스 휠 스크롤(목록 위에서만)
        if (IsPointerOver(viewportRect))
        {
            float wheel = Input.mouseScrollDelta.y;
            if (Mathf.Abs(wheel) > 0.01f) scrollOffset -= wheel * PassiveRerollSettings.Get().scrollSpeed;
        }

        if (Time.frameCount == openedFrame) return;

        if (Input.GetKeyDown(KeyCode.Escape)) { CloseAll(true); return; }

        if (!Input.GetMouseButtonDown(0)) return;

        int hovered = GetHoveredIndex();
        if (hovered >= 0)
        {
            // 교체 후보가 없는 아이템은 고를 수 없습니다. 고를 수 있으면 하나만 선택(다시 누르면 선택 해제).
            if (selectableItems.Contains(displayItems[hovered]))
            {
                selectedIndex = selectedIndex == hovered ? -1 : hovered;
            }
            return;
        }
        if (overConfirm) { SetMode(Mode.Confirm); return; }
        if (overCancel) { CloseAll(true); return; }
    }

    private int GetHoveredIndex()
    {
        if (!IsPointerOver(viewportRect)) return -1; // 스크롤로 가려진 칸은 대상 아님
        for (int i = 0; i < displayItems.Count && i < iconViews.Count; i++)
        {
            if (IsPointerOver(iconViews[i].rect)) return i;
        }
        return -1;
    }

    // ------------------------------------------------------------------
    // 검은 창 (확인 / 사용할 수 없음)
    // ------------------------------------------------------------------

    private void UpdateConfirm()
    {
        var style = PopupStyle.Get();
        bool overLeft = IsPointerOver(darkLeftButton.rect);
        bool overRight = IsPointerOver(darkRightButton.rect);
        darkLeftButton.image.color = style.GetButtonColor(overLeft, true);
        darkRightButton.image.color = style.GetButtonColor(overRight, true);

        // 뒤에 보이는 선택 창 버튼은 호버 표시 없이 평소 색으로
        SetButtonColor(confirmButton, selectedIndex >= 0, false);
        SetButtonColor(cancelButton, true, false);

        if (Time.frameCount == openedFrame) return;

        // Esc / [취소하기] = 확인 창만 닫고 선택 창으로(고른 상태 그대로)
        if (Input.GetKeyDown(KeyCode.Escape)) { SetMode(Mode.Main); return; }

        if (Input.GetMouseButtonDown(0))
        {
            if (overLeft) Confirm();
            else if (overRight) SetMode(Mode.Main);
        }
    }

    // 확인 창 [교체하기]: 창을 닫고(조작을 먼저 되돌림) 교체 콜백을 부릅니다.
    private void Confirm()
    {
        ItemDefinition chosen = selectedIndex >= 0 && selectedIndex < displayItems.Count ? displayItems[selectedIndex] : null;
        Action<ItemDefinition> confirm = pendingConfirm;
        pendingConfirm = null;
        pendingCancel = null; // 확정했으므로 취소 콜백은 부르지 않음

        CloseAll(false);
        if (chosen != null) confirm?.Invoke(chosen);
    }

    private void UpdateMessage()
    {
        var style = PopupStyle.Get();
        bool overOk = IsPointerOver(darkLeftButton.rect);
        darkLeftButton.image.color = style.GetButtonColor(overOk, true);

        if (Time.frameCount == openedFrame) return;

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Escape))
        {
            CloseAll(false);
            return;
        }
        if (Input.GetMouseButtonDown(0) && overOk) CloseAll(false);
    }

    private void FillDarkPopup(Mode popupMode)
    {
        var s = PassiveRerollSettings.Get();
        darkMessage.richText = true;

        if (popupMode == Mode.Confirm)
        {
            ItemDefinition chosen = selectedIndex >= 0 && selectedIndex < displayItems.Count ? displayItems[selectedIndex] : null;
            string name = ItemName(chosen);
            darkMessage.text = $"{ColoredName(chosen)}{ObjectParticle(name)} {s.confirmQuestion}";
            darkLeftButton.label.text = s.confirmLabel;
            darkRightButton.label.text = s.cancelLabel;
            darkRightButton.rect.gameObject.SetActive(true);
        }
        else
        {
            darkMessage.text = messageText;
            darkLeftButton.label.text = s.okLabel;
            darkRightButton.rect.gameObject.SetActive(false);
        }

        var style = PopupStyle.Get();
        darkLeftButton.image.color = style.GetButtonColor(false, true);
        darkRightButton.image.color = style.GetButtonColor(false, true);
    }

    private static string ItemName(ItemDefinition item)
    {
        if (item == null) return "-";
        return string.IsNullOrEmpty(item.displayName) ? item.name : item.displayName;
    }

    // 이름 마지막 글자에 받침이 있으면 "을", 없으면 "를". 한글이 아니면 "을(를)".
    private static string ObjectParticle(string word)
    {
        if (string.IsNullOrEmpty(word)) return "을(를)";
        char last = word[word.Length - 1];
        if (last < 0xAC00 || last > 0xD7A3) return "을(를)";
        return (last - 0xAC00) % 28 != 0 ? "을" : "를";
    }

    // 등급 색을 입힌 아이템 이름 - 아이템 설명칸(PlayerItemInfoDisplay)의 등급별 이름 색을 그대로 씁니다(없으면 흰색).
    private static string ColoredName(ItemDefinition item)
    {
        if (item == null) return "-";
        Color color = Color.white;
        var info = UnityEngine.Object.FindFirstObjectByType<PlayerItemInfoDisplay>();
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
        return $"<color=#{ColorUtility.ToHtmlStringRGBA(color)}>{ItemName(item)}</color>";
    }

    private static bool IsPointerOver(RectTransform rect)
    {
        if (rect == null || !rect.gameObject.activeInHierarchy) return false;
        return RectTransformUtility.RectangleContainsScreenPoint(rect, Input.mousePosition, null);
    }

    // ------------------------------------------------------------------
    // 획득 연출 순서대로
    // ------------------------------------------------------------------

    private IEnumerator PickupSequence(List<ItemDefinition> items)
    {
        for (int i = 0; i < items.Count; i++)
        {
            // 앞의 연출(이 아이템들 것이든, 다른 획득 것이든)이 끝날 때까지 기다립니다.
            while (PlayerItemPickupPresenter.BlocksActions) yield return null;
            ItemPickupEvents.Raise(items[i]);
            yield return null; // 연출이 시작되도록 한 프레임
        }
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
        return c != null ? c : UnityEngine.Object.FindFirstObjectByType<T>();
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
        canvasGO = new GameObject("PassiveRerollCanvas");
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
        dimImage = dimGO.AddComponent<Image>();
        dimImage.raycastTarget = false;

        // 선택 창
        mainRoot = new GameObject("MainWindow");
        mainRoot.transform.SetParent(canvasGO.transform, false);
        mainRect = mainRoot.AddComponent<RectTransform>();
        mainRect.anchorMin = mainRect.anchorMax = new Vector2(0.5f, 0.5f);
        mainRect.pivot = new Vector2(0.5f, 0.5f);

        windowOutline = CreateImage(mainRect, "Outline");
        windowImage = CreateImage(mainRect, "Window");

        var titleGO = new GameObject("Title");
        titleGO.transform.SetParent(mainRect, false);
        var titleRect = titleGO.AddComponent<RectTransform>();
        titleRect.anchorMin = titleRect.anchorMax = new Vector2(0.5f, 1f);
        titleRect.pivot = new Vector2(0.5f, 0.5f);
        titleText = HUDText.AddTo(titleGO, TextAnchor.MiddleCenter, true);
        titleText.raycastTarget = false;

        // 패시브 목록: 배경 패널 -> 보이는 영역(밖은 잘림) -> 내용(스크롤)
        var panelGO = new GameObject("GridPanel");
        panelGO.transform.SetParent(mainRect, false);
        gridPanelRect = panelGO.AddComponent<RectTransform>();
        gridPanelRect.anchorMin = gridPanelRect.anchorMax = new Vector2(0.5f, 0.5f);
        gridPanelRect.pivot = new Vector2(0.5f, 0.5f);
        gridPanelImage = panelGO.AddComponent<Image>();
        gridPanelImage.raycastTarget = false;

        var viewportGO = new GameObject("Viewport");
        viewportGO.transform.SetParent(gridPanelRect, false);
        viewportRect = viewportGO.AddComponent<RectTransform>();
        viewportRect.anchorMin = viewportRect.anchorMax = new Vector2(0f, 1f);
        viewportRect.pivot = new Vector2(0f, 1f);
        viewportGO.AddComponent<RectMask2D>();

        var contentGO = new GameObject("Content");
        contentGO.transform.SetParent(viewportRect, false);
        contentRect = contentGO.AddComponent<RectTransform>();
        contentRect.anchorMin = contentRect.anchorMax = new Vector2(0f, 1f);
        contentRect.pivot = new Vector2(0f, 1f);

        confirmButton = CreateButton(mainRect, "ConfirmButton", true);
        cancelButton = CreateButton(mainRect, "CancelButton", true);

        // 검은 창
        darkRoot = new GameObject("DarkPopup");
        darkRoot.transform.SetParent(canvasGO.transform, false);
        darkRect = darkRoot.AddComponent<RectTransform>();
        darkRect.anchorMin = darkRect.anchorMax = new Vector2(0.5f, 0.5f);
        darkRect.pivot = new Vector2(0.5f, 0.5f);
        darkImage = darkRoot.AddComponent<Image>();
        darkImage.raycastTarget = false;

        var msgGO = new GameObject("Message");
        msgGO.transform.SetParent(darkRect, false);
        var msgRect = msgGO.AddComponent<RectTransform>();
        msgRect.anchorMin = msgRect.anchorMax = new Vector2(0.5f, 1f);
        msgRect.pivot = new Vector2(0.5f, 1f);
        darkMessage = HUDText.AddTo(msgGO, TextAnchor.MiddleCenter, true);
        darkMessage.raycastTarget = false;

        darkLeftButton = CreateButton(darkRect, "LeftButton", false);
        darkRightButton = CreateButton(darkRect, "RightButton", false);
    }

    private static Image CreateImage(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        var image = go.AddComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    private void EnsureIconViews(int count)
    {
        while (iconViews.Count < count)
        {
            var go = new GameObject($"Icon_{iconViews.Count}");
            go.transform.SetParent(contentRect, false);
            var view = new IconView();
            view.rect = go.AddComponent<RectTransform>();
            view.rect.anchorMin = view.rect.anchorMax = new Vector2(0f, 1f);
            view.rect.pivot = new Vector2(0.5f, 0.5f); // 작아질 때 칸 가운데를 기준으로
            view.image = go.AddComponent<Image>();
            view.image.preserveAspect = true;
            view.image.raycastTarget = false;
            iconViews.Add(view);
        }
        for (int i = 0; i < iconViews.Count; i++)
        {
            iconViews[i].rect.gameObject.SetActive(i < count);
        }
    }

    // 버튼: 테두리(뒤, 선택 창 버튼만) + 버튼 면 + 글자
    private static ButtonView CreateButton(Transform parent, string name, bool withOutline)
    {
        var view = new ButtonView();

        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        view.rect = go.AddComponent<RectTransform>();
        view.rect.anchorMin = view.rect.anchorMax = new Vector2(0.5f, 0f);
        view.rect.pivot = new Vector2(0.5f, 0f);

        if (withOutline)
        {
            var outlineGO = new GameObject("Outline");
            outlineGO.transform.SetParent(go.transform, false);
            var outlineRect = outlineGO.AddComponent<RectTransform>();
            outlineRect.anchorMin = outlineRect.anchorMax = new Vector2(0.5f, 0.5f);
            outlineRect.pivot = new Vector2(0.5f, 0.5f);
            view.outline = outlineGO.AddComponent<Image>();
            view.outline.raycastTarget = false;
        }

        var faceGO = new GameObject("Face");
        faceGO.transform.SetParent(go.transform, false);
        var faceRect = faceGO.AddComponent<RectTransform>();
        faceRect.anchorMin = Vector2.zero;
        faceRect.anchorMax = Vector2.one;
        faceRect.offsetMin = Vector2.zero;
        faceRect.offsetMax = Vector2.zero;
        view.image = faceGO.AddComponent<Image>();
        view.image.raycastTarget = false;

        var labelGO = new GameObject("Label");
        labelGO.transform.SetParent(go.transform, false);
        var labelRect = labelGO.AddComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        view.label = HUDText.AddTo(labelGO, TextAnchor.MiddleCenter);
        view.label.raycastTarget = false;

        return view;
    }

    // ------------------------------------------------------------------
    // 배치 (설정 값을 매 프레임 반영 - Play 중 에셋 값을 바꾸면 즉시 반영)
    // ------------------------------------------------------------------

    private void ApplyLayout()
    {
        var s = PassiveRerollSettings.Get();
        var style = PopupStyle.Get();

        canvas.sortingOrder = s.canvasSortingOrder;
        if (Screen.height > 0)
        {
            float screenAspect = (float)Screen.width / Screen.height;
            scaler.matchWidthOrHeight = screenAspect > 1920f / 1080f ? 1f : 0f; // HUD와 같은 규칙
        }
        dimImage.color = s.dimColor;

        if (mainRoot.activeSelf) LayoutMain(s);
        if (darkRoot.activeSelf) LayoutDark(s, style);
    }

    private void LayoutMain(PassiveRerollSettings s)
    {
        Vector2 size = new Vector2(Mathf.Max(200f, s.windowSize.x), Mathf.Max(200f, s.windowSize.y));
        mainRect.sizeDelta = size;
        mainRect.anchoredPosition = Vector2.zero;

        float t = s.windowOutlineThickness;
        Vector2 outlineSize = size + new Vector2(t * 2f, t * 2f);
        windowOutline.rectTransform.sizeDelta = outlineSize;
        windowOutline.rectTransform.anchoredPosition = Vector2.zero;
        windowOutline.enabled = t > 0f;
        UIRoundedBox.Apply(windowOutline, s.windowOutlineColor, s.windowCornerRadius + t, outlineSize);

        windowImage.rectTransform.sizeDelta = size;
        windowImage.rectTransform.anchoredPosition = Vector2.zero;
        UIRoundedBox.Apply(windowImage, s.windowColor, s.windowCornerRadius, size);

        var titleRect = titleText.rectTransform;
        titleRect.sizeDelta = new Vector2(size.x - 40f, s.titleFontSize + 16f);
        titleRect.anchoredPosition = new Vector2(0f, -s.titleTopMargin);
        titleText.text = s.title;
        titleText.fontSize = s.titleFontSize;
        titleText.color = s.titleColor;

        LayoutGrid(s);

        float half = (s.mainButtonSize.x + s.mainButtonSpacing) * 0.5f;
        LayoutMainButton(confirmButton, s.confirmLabel, new Vector2(-half, s.mainButtonBottomMargin), s);
        LayoutMainButton(cancelButton, s.cancelLabel, new Vector2(half, s.mainButtonBottomMargin), s);
    }

    // 패시브 인벤토리 HUD(PlayerInventoryDisplay)의 배경/칸 배치 값을 그대로 가져옵니다. 씬에 없으면 그 스크립트의 기본값과 같은 값.
    private void LayoutGrid(PassiveRerollSettings s)
    {
        int columns = inventoryHud != null ? Mathf.Max(1, inventoryHud.columns) : 5;
        float iconSize = inventoryHud != null ? inventoryHud.iconSize : 64f;
        float spacingX = inventoryHud != null ? inventoryHud.iconSpacingX : 8f;
        float spacingY = inventoryHud != null ? inventoryHud.iconSpacingY : 8f;
        float padL = inventoryHud != null ? inventoryHud.paddingLeft : 12f;
        float padR = inventoryHud != null ? inventoryHud.paddingRight : 12f;
        float padT = inventoryHud != null ? inventoryHud.paddingTop : 12f;
        float padB = inventoryHud != null ? inventoryHud.paddingBottom : 12f;
        Sprite background = inventoryHud != null ? inventoryHud.backgroundSprite : null;
        Color panelColor = inventoryHud != null ? inventoryHud.panelColor : new Color(0f, 0f, 0f, 0.6f);
        Color placeholder = inventoryHud != null ? inventoryHud.placeholderColor : new Color(1f, 1f, 1f, 0.15f);

        int rows = Mathf.Max(1, s.visibleRows);
        float gridWidth = columns * iconSize + (columns - 1) * spacingX;
        float viewHeight = rows * iconSize + (rows - 1) * spacingY;

        gridPanelRect.sizeDelta = new Vector2(padL + gridWidth + padR, padT + viewHeight + padB);
        gridPanelRect.anchoredPosition = new Vector2(0f, s.gridOffsetY);
        gridPanelRect.localScale = new Vector3(s.gridScale, s.gridScale, 1f);
        if (background != null)
        {
            gridPanelImage.sprite = background;
            gridPanelImage.type = Image.Type.Simple;
            gridPanelImage.color = Color.white;
        }
        else
        {
            gridPanelImage.sprite = null;
            gridPanelImage.type = Image.Type.Simple;
            gridPanelImage.color = panelColor;
        }

        viewportRect.sizeDelta = new Vector2(gridWidth, viewHeight);
        viewportRect.anchoredPosition = new Vector2(padL, -padT);

        int totalRows = Mathf.CeilToInt(displayItems.Count / (float)columns);
        float contentHeight = totalRows > 0 ? totalRows * iconSize + (totalRows - 1) * spacingY : 0f;
        float maxScroll = Mathf.Max(0f, contentHeight - viewHeight);
        scrollOffset = Mathf.Clamp(scrollOffset, 0f, maxScroll);
        contentRect.sizeDelta = new Vector2(gridWidth, contentHeight);
        contentRect.anchoredPosition = new Vector2(0f, scrollOffset);

        for (int i = 0; i < displayItems.Count && i < iconViews.Count; i++)
        {
            var view = iconViews[i];
            ItemDefinition item = displayItems[i];
            int col = i % columns;
            int row = i / columns;
            view.rect.sizeDelta = new Vector2(iconSize, iconSize);
            view.rect.anchoredPosition = new Vector2(col * (iconSize + spacingX) + iconSize * 0.5f,
                                                     -(row * (iconSize + spacingY) + iconSize * 0.5f));

            Color color;
            if (item.icon != null)
            {
                view.image.sprite = item.icon;
                color = Color.white;
            }
            else
            {
                view.image.sprite = null;
                color = placeholder;
            }

            bool selected = i == selectedIndex;
            if (!selectableItems.Contains(item))
            {
                // 고를 수 없는 아이템: 어둡게(크기/불투명도는 그대로)
                color = new Color(color.r * s.unavailableTint.r, color.g * s.unavailableTint.g,
                                  color.b * s.unavailableTint.b, color.a * s.unavailableTint.a);
            }
            else if (selected)
            {
                color.a *= s.selectedAlpha;
            }
            view.image.color = color;

            float scale = selected ? s.selectedScale : 1f;
            view.rect.localScale = new Vector3(scale, scale, 1f);
        }
    }

    private void LayoutMainButton(ButtonView button, string text, Vector2 pos, PassiveRerollSettings s)
    {
        Vector2 size = s.mainButtonSize;
        button.rect.sizeDelta = size;
        button.rect.anchoredPosition = pos;
        UIRoundedBox.Apply(button.image, button.image.color, s.mainButtonCornerRadius, size); // 색은 Update에서 호버/활성에 따라

        if (button.outline != null)
        {
            float t = s.mainButtonOutlineThickness;
            Vector2 outlineSize = size + new Vector2(t * 2f, t * 2f);
            button.outline.rectTransform.sizeDelta = outlineSize;
            button.outline.rectTransform.anchoredPosition = Vector2.zero;
            button.outline.enabled = t > 0f;
            Color c = s.mainButtonOutlineColor;
            c.a *= button.image.color.a; // 못 누르는(반투명) 버튼이면 테두리도 같이 흐리게
            UIRoundedBox.Apply(button.outline, c, s.mainButtonCornerRadius + t, outlineSize);
        }

        button.label.text = text;
        button.label.fontSize = s.mainButtonFontSize;
        Color tc = PopupStyle.Get().buttonTextColor;
        tc.a *= button.image.color.a;
        button.label.color = tc;
    }

    private static void SetButtonColor(ButtonView button, bool enabled, bool hovered)
    {
        button.image.color = PopupStyle.Get().GetButtonColor(hovered, enabled);
    }

    private void LayoutDark(PassiveRerollSettings s, PopupStyle style)
    {
        Vector2 size = mode == Mode.Confirm ? s.confirmSize : s.messageSize;
        size = new Vector2(Mathf.Max(100f, size.x), Mathf.Max(80f, size.y));
        darkRect.sizeDelta = size;
        darkRect.anchoredPosition = Vector2.zero;
        UIRoundedBox.Apply(darkImage, style.darkPanelColor, style.darkPanelCornerRadius, size);

        float pad = 20f;
        float buttonsTop = s.darkButtonBottomMargin + s.darkButtonSize.y;
        var msgRect = darkMessage.rectTransform;
        msgRect.sizeDelta = new Vector2(size.x - pad * 2f, Mathf.Max(10f, size.y - buttonsTop - pad - 8f));
        msgRect.anchoredPosition = new Vector2(0f, -pad);
        darkMessage.fontSize = s.darkMessageFontSize;
        darkMessage.color = s.darkMessageColor;

        if (mode == Mode.Confirm)
        {
            float half = (s.darkButtonSize.x + s.darkButtonSpacing) * 0.5f;
            LayoutDarkButton(darkLeftButton, new Vector2(-half, s.darkButtonBottomMargin), s, style);
            LayoutDarkButton(darkRightButton, new Vector2(half, s.darkButtonBottomMargin), s, style);
        }
        else
        {
            LayoutDarkButton(darkLeftButton, new Vector2(0f, s.darkButtonBottomMargin), s, style);
        }
    }

    private static void LayoutDarkButton(ButtonView button, Vector2 pos, PassiveRerollSettings s, PopupStyle style)
    {
        button.rect.sizeDelta = s.darkButtonSize;
        button.rect.anchoredPosition = pos;
        UIRoundedBox.Apply(button.image, button.image.color, style.darkPanelButtonCornerRadius, s.darkButtonSize);
        button.label.fontSize = s.darkButtonFontSize;
        button.label.color = style.GetButtonTextColor(true);
    }
}
