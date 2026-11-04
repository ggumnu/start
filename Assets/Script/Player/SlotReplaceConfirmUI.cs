using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 슬롯 아이템(액티브/장신구/일회용/음식) 교체 확인창입니다.
// "아이템 슬롯을 교체하시겠습니까?" + 무엇이 바뀌는지(장착 중 / 새 아이템) + [예]/[아니오].
//
// [언제 뜨나 - 형빈 확정]
// 보물상자(TreasureChest)/동아리방 보물상자(ItemPedestal)에서 슬롯 아이템을 주우려 할 때, 그 분류에 이미
// 장착 중인 아이템이 있고 그 아이템의 ItemDefinition "교체 시 확인"(confirmBeforeReplace)이 체크돼 있으면
// 바로 교체하지 않고 이 창을 띄웁니다(예: 20번 - 교체되면 흡수한 크레딧/공격력을 잃음). 판단 기준은
// "잃게 되는 쪽(장착 중인 아이템)"입니다. 체크되지 않은 아이템이나 빈 슬롯은 지금처럼 바로 교체됩니다.
// - [예](또는 F) = 상자 쪽이 넘겨준 교체 함수를 실행(평소 즉시 교체와 똑같은 처리 - 원래 아이템은 상자에 남음)
// - [아니오](또는 Esc) = 아무 일도 없음
//
// [창이 떠 있는 동안 - 게임은 멈추지 않고 플레이어 행동만 막음 (형빈 요청으로 일시정지에서 변경)]
// - 게임 자체(적/이펙트/시간)는 그대로 흘러갑니다.
// - 플레이어의 이동/조준/공격/장전/무기 전환/아이템 사용(Space/Q/E)은 해당 플레이어 컴포넌트(PlayerMovement/
//   PlayerAimAndAttack/PlayerShooting/PlayerWeaponMode/PlayerActiveItem/PlayerDisposableItem/PlayerFoodItem)를
//   잠시 꺼서 막고, 닫으면 원래 상태로 되돌립니다(각 스크립트는 수정하지 않음). 끄기 직전에 Animator의 걷기
//   파라미터(MoveX/MoveZ/Speed)와 IsAiming을 0/false로 맞춰, 제자리 걷기나 조준 자세로 굳지 않게 합니다.
// - 수류탄(PlayerGrenadeThrower)/해킹툴(PlayerHackTool)은 끄면 진행 중인 투척/사용 동작이 끊길 수 있어 끄지 않고,
//   좌클릭 차단(HUDClickBlocker가 BlocksInteract 확인)으로만 막습니다 - 화면 어디를 눌러도 공격이 나가지 않습니다.
// - 상자/동아리방 보물상자/포탈은 F를 무시합니다(BlocksInteract). 창을 닫은 그 프레임도 무시하므로, 확인창의
//   F(예)가 상자나 포탈에 다시 전달되지 않습니다. 창을 연 그 프레임의 F로는 바로 확정되지 않습니다.
// - 창이 떠 있는 동안 플레이어가 피해를 입으면 [아니오](Esc)와 똑같이 취소로 닫힙니다(교체 안 됨, 형빈 확정).
// - 플레이어가 죽으면 교체 없이 창만 닫습니다(조작 해제는 PlayerDeathHandler 몫이라 되돌리지 않음).
//
// [아이템 이름 색]
// "장착 중"/"새 아이템" 뒤의 이름은 등급(Tier0~3)별 색으로 칠합니다. 색은 씬의 PlayerItemInfoDisplay(아이템
// 설명칸)의 등급별 이름 색을 그대로 가져와 설명칸과 항상 같게 맞추고, 그 컴포넌트가 없으면 아래 기본값
// (Tier0 흰색 / Tier1 파랑 / Tier2 노랑 / Tier3 보라)을 씁니다. 두 줄 사이에는 빈 줄을 하나 둡니다.
//
// [색/모서리 - 공용 팝업 스타일]
// 창 바탕색/모서리, 버튼 색(평소/마우스 올림)/글자 색/모서리는 공용 스타일 에셋(PopupStyle)에서 읽습니다 - 모든 팝업이 같은 값을 씁니다.
//
// [설정]
// 씬에 따로 붙일 필요가 없습니다 - 처음 필요할 때 스스로 만들어집니다. 문구/색/크기/키를 바꾸고 싶으면 씬의 빈
// 오브젝트에 이 컴포넌트를 직접 붙여 인스펙터에서 조절하세요(그러면 그 컴포넌트를 사용합니다).
// 입력은 다른 HUD와 같이 EventSystem 없이 마우스 좌표를 직접 비교합니다.
public class SlotReplaceConfirmUI : MonoBehaviour
{
    [Header("문구")]
    public string message = "아이템 슬롯을 교체하시겠습니까?";
    public string equippedLabel = "장착 중";
    public string incomingLabel = "새 아이템";
    public string yesLabel = "예";
    public string noLabel = "아니오";

    [Header("키")]
    [Tooltip("이 키를 누르면 [예]와 같습니다(창을 연 그 프레임의 입력은 무시).")]
    public KeyCode confirmKey = KeyCode.F;
    [Tooltip("이 키를 누르면 [아니오]와 같습니다.")]
    public KeyCode cancelKey = KeyCode.Escape;

    [Header("등급별 이름 색 (씬에 PlayerItemInfoDisplay가 없을 때만 사용 - 있으면 설명칸의 색을 그대로 따름)")]
    public Color fallbackTier0Color = Color.white;
    public Color fallbackTier1Color = new Color(0.45f, 0.75f, 1f, 1f);
    public Color fallbackTier2Color = new Color(1f, 0.85f, 0.25f, 1f);
    public Color fallbackTier3Color = new Color(0.75f, 0.5f, 1f, 1f);

    [Header("창 모양 (1920x1080 기준 px - 바탕색/모서리는 PopupStyle)")]
    public Vector2 panelSize = new Vector2(480f, 260f);
    public int messageFontSize = 26;
    public Color messageColor = Color.white;
    public int detailFontSize = 19;
    public Color detailColor = new Color(0.8f, 0.8f, 0.8f, 1f);

    [Header("버튼 (색/글자 색/모서리는 PopupStyle)")]
    public Vector2 buttonSize = new Vector2(140f, 46f);
    [Tooltip("두 버튼 사이 간격입니다.")]
    public float buttonSpacing = 30f;
    [Tooltip("창 아래쪽 끝에서 버튼까지의 거리입니다.")]
    public float buttonBottomMargin = 24f;
    public int buttonFontSize = 22;

    [Header("정렬 (일반적으로 건드릴 필요 없음)")]
    [Tooltip("HUD/툴팁보다 위, 게임 오버 창(100)보다 아래입니다.")]
    public int canvasSortingOrder = 90;

    // ------------------------------------------------------------------
    // 외부에서 쓰는 정적 진입점
    // ------------------------------------------------------------------

    private static SlotReplaceConfirmUI instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
    }

    // 창이 떠 있는지
    public static bool IsOpen => instance != null && instance.isOpen;

    // 상자/포탈의 F, 공격 좌클릭을 막아야 하는지(창이 떠 있거나, 방금 닫힌 그 프레임)
    public static bool BlocksInteract =>
        instance != null && (instance.isOpen || instance.closedFrame == Time.frameCount);

    // 장착 중인 아이템이 "교체 시 확인" 대상인지
    public static bool NeedsConfirm(ItemDefinition equipped)
    {
        return equipped != null && equipped.confirmBeforeReplace;
    }

    // 확인창을 띄웁니다. onConfirm은 [예]일 때만 실행됩니다. 이미 떠 있거나 플레이어가 죽었으면 띄우지 않고 false.
    public static bool Show(ItemDefinition equipped, ItemDefinition incoming, Action onConfirm)
    {
        if (instance == null) instance = UnityEngine.Object.FindFirstObjectByType<SlotReplaceConfirmUI>();
        if (instance == null)
        {
            var go = new GameObject("SlotReplaceConfirmUI");
            instance = go.AddComponent<SlotReplaceConfirmUI>();
        }
        return instance.Open(equipped, incoming, onConfirm);
    }

    // ------------------------------------------------------------------
    // 인스턴스
    // ------------------------------------------------------------------

    private const int RoundedTexSize = 96;
    private const int RoundedTexRadius = 32;

    private bool isOpen;
    private int openedFrame = -1;
    private int closedFrame = -1;
    private Action pendingConfirm;
    private PlayerHealth watchedHealth; // 창이 떠 있는 동안 피해를 감지할 플레이어(피해 = 취소)

    // 창이 떠 있는 동안 잠시 끈 플레이어 컴포넌트들(닫을 때 원래대로 되돌림)
    private readonly List<Behaviour> disabledBehaviours = new List<Behaviour>();

    private GameObject canvasGO;
    private Canvas canvas;
    private CanvasScaler scaler;
    private RectTransform panelRect;
    private Image panelImage;
    private TextMeshProUGUI messageText;
    private TextMeshProUGUI detailText;
    private RectTransform yesRect;
    private Image yesImage;
    private TextMeshProUGUI yesText;
    private RectTransform noRect;
    private Image noImage;
    private TextMeshProUGUI noText;
    private Sprite roundedSprite;
    private Texture2D roundedTexture;

    void Awake()
    {
        if (instance == null) instance = this;
        else if (instance != this)
        {
            Destroy(this); // 중복 방지 - 하나만 씁니다
            return;
        }

        BuildUI();
        canvasGO.SetActive(false);
        PlayerHealth.OnAnyPlayerDied += HandlePlayerDied;
    }

    void OnDestroy()
    {
        PlayerHealth.OnAnyPlayerDied -= HandlePlayerDied;
        UnwatchDamage();
        if (isOpen) RestorePlayerControls();
        if (instance == this) instance = null;
        if (roundedSprite != null) Destroy(roundedSprite);
        if (roundedTexture != null) Destroy(roundedTexture);
    }

    private bool Open(ItemDefinition equipped, ItemDefinition incoming, Action onConfirm)
    {
        if (isOpen) return false;

        var health = UnityEngine.Object.FindFirstObjectByType<PlayerHealth>();
        if (health != null && health.IsDead) return false;

        isOpen = true;
        openedFrame = Time.frameCount;
        pendingConfirm = onConfirm;

        messageText.text = message;
        // 두 줄 사이에 빈 줄 하나, 이름은 등급 색(리치 텍스트)
        detailText.richText = true;
        detailText.text = $"{equippedLabel}: {ColoredName(equipped)}\n\n{incomingLabel}: {ColoredName(incoming)}";

        DisablePlayerControls(health);
        WatchDamage(health);

        canvasGO.SetActive(true);
        ApplyLayout();
        return true;
    }

    private void Close(bool confirmed)
    {
        if (!isOpen) return;

        isOpen = false;
        closedFrame = Time.frameCount;
        canvasGO.SetActive(false);

        UnwatchDamage();
        RestorePlayerControls();

        // 조작을 먼저 원래대로 돌린 뒤 교체합니다(획득 연출 등이 정상 상태에서 시작되도록).
        Action callback = pendingConfirm;
        pendingConfirm = null;
        if (confirmed) callback?.Invoke();
    }

    // 플레이어 사망: 교체 없이 창만 닫습니다. 조작 해제(PlayerDeathHandler)를 덮어쓰지 않도록 컴포넌트는 되돌리지
    // 않습니다. (보통은 피해를 입는 순간 HandleDamageTaken이 먼저 창을 닫으므로 여기까지 오지 않습니다.)
    private void HandlePlayerDied(PlayerHealth health)
    {
        if (!isOpen) return;
        isOpen = false;
        closedFrame = Time.frameCount;
        pendingConfirm = null;
        UnwatchDamage();
        disabledBehaviours.Clear();
        canvasGO.SetActive(false);
    }

    // 창이 떠 있는 동안 피해를 입으면 [아니오](Esc)와 똑같이 취소로 닫습니다(형빈 확정).
    private void HandleDamageTaken(float amount)
    {
        if (isOpen) Close(false);
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

    void Update()
    {
        if (!isOpen) return;

        ApplyLayout();

        bool overYes = IsPointerOver(yesRect);
        bool overNo = IsPointerOver(noRect);
        var style = PopupStyle.Get();
        yesImage.color = style.GetButtonColor(overYes, true);
        noImage.color = style.GetButtonColor(overNo, true);

        // 창을 연 그 프레임의 입력(상자를 연 F, 같은 프레임의 클릭)은 무시합니다.
        if (Time.frameCount == openedFrame) return;

        if (Input.GetKeyDown(confirmKey)) { Close(true); return; }
        if (Input.GetKeyDown(cancelKey)) { Close(false); return; }

        if (Input.GetMouseButtonDown(0))
        {
            if (overYes) Close(true);
            else if (overNo) Close(false);
        }
    }

    private static string ItemName(ItemDefinition item)
    {
        if (item == null) return "-";
        return string.IsNullOrEmpty(item.displayName) ? item.name : item.displayName;
    }

    // 등급 색을 입힌 아이템 이름(리치 텍스트)
    private string ColoredName(ItemDefinition item)
    {
        if (item == null) return "-";
        string hex = ColorUtility.ToHtmlStringRGBA(GetTierColor(item.rarity));
        return $"<color=#{hex}>{ItemName(item)}</color>";
    }

    // 씬의 PlayerItemInfoDisplay(아이템 설명칸)와 같은 등급 색을 씁니다. 없으면 이 컴포넌트의 기본값.
    private Color GetTierColor(ItemDefinition.ItemRarity rarity)
    {
        var info = UnityEngine.Object.FindFirstObjectByType<PlayerItemInfoDisplay>();
        if (info != null)
        {
            if (!info.useTierNameColor) return info.nameTextColor;
            switch (rarity)
            {
                case ItemDefinition.ItemRarity.Tier0: return info.tier0NameColor;
                case ItemDefinition.ItemRarity.Tier1: return info.tier1NameColor;
                case ItemDefinition.ItemRarity.Tier2: return info.tier2NameColor;
                case ItemDefinition.ItemRarity.Tier3: return info.tier3NameColor;
            }
        }

        switch (rarity)
        {
            case ItemDefinition.ItemRarity.Tier0: return fallbackTier0Color;
            case ItemDefinition.ItemRarity.Tier1: return fallbackTier1Color;
            case ItemDefinition.ItemRarity.Tier2: return fallbackTier2Color;
            case ItemDefinition.ItemRarity.Tier3: return fallbackTier3Color;
            default: return Color.white;
        }
    }

    private static bool IsPointerOver(RectTransform rect)
    {
        if (rect == null || !rect.gameObject.activeInHierarchy) return false;
        return RectTransformUtility.RectangleContainsScreenPoint(rect, Input.mousePosition, null);
    }

    // ------------------------------------------------------------------
    // 플레이어 조작 잠금 (게임은 계속 흐름)
    // ------------------------------------------------------------------

    // 이동/조준·공격/장전(R)/무기 전환(1·2·3)/아이템 사용(Space·Q·E)을 막기 위해 켜져 있던 것만 잠시 끕니다.
    // 수류탄/해킹툴은 진행 중인 동작이 끊기지 않도록 끄지 않고 좌클릭 차단으로만 막습니다(파일 상단 참고).
    private void DisablePlayerControls(PlayerHealth health)
    {
        disabledBehaviours.Clear();

        GameObject player = health != null ? health.gameObject : null;

        // 걷기/조준 컴포넌트를 끄면 Animator 값이 마지막 상태로 남으므로, 끄기 전에 멈춘 상태로 맞춥니다.
        var movement = FindOnPlayer<PlayerMovement>(player);
        var aim = FindOnPlayer<PlayerAimAndAttack>(player);
        ResetLocomotionAnimator(movement != null ? movement.animator : null, aim != null);

        TryDisable(movement);
        TryDisable(aim);
        TryDisable(FindOnPlayer<PlayerShooting>(player));
        TryDisable(FindOnPlayer<PlayerWeaponMode>(player));
        TryDisable(FindOnPlayer<PlayerActiveItem>(player));
        TryDisable(FindOnPlayer<PlayerDisposableItem>(player));
        TryDisable(FindOnPlayer<PlayerFoodItem>(player));
    }

    // 플레이어 오브젝트에서 먼저 찾고, 없으면 씬 전체에서 찾습니다.
    private static T FindOnPlayer<T>(GameObject player) where T : Component
    {
        T c = player != null ? player.GetComponent<T>() : null;
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
    // UI 생성 / 배치
    // ------------------------------------------------------------------

    private void BuildUI()
    {
        canvasGO = new GameObject("SlotReplaceConfirmCanvas");
        canvasGO.transform.SetParent(transform, false);
        canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        var panelGO = new GameObject("Panel");
        panelGO.transform.SetParent(canvasGO.transform, false);
        panelRect = panelGO.AddComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelImage = panelGO.AddComponent<Image>();
        panelImage.raycastTarget = false;

        messageText = CreateText(panelRect, "Message", TextAnchor.MiddleCenter);
        detailText = CreateText(panelRect, "Detail", TextAnchor.MiddleCenter);

        CreateButton(panelRect, "YesButton", out yesRect, out yesImage, out yesText);
        CreateButton(panelRect, "NoButton", out noRect, out noImage, out noText);
    }

    private static TextMeshProUGUI CreateText(Transform parent, string name, TextAnchor anchor)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        // [TMP] 옛 방식 Text는 화면 배율에 따라 흐릿하게 보여서 TextMeshPro로 교체했습니다(HUDText 참고).
        // 리치 텍스트(<color=#...>) 문법은 TMP도 같습니다.
        return HUDText.AddTo(go, anchor, true);
    }

    private static void CreateButton(Transform parent, string name, out RectTransform rect, out Image image, out TextMeshProUGUI label)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        rect = go.AddComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        image = go.AddComponent<Image>();
        image.raycastTarget = false;

        var labelGO = new GameObject("Label");
        labelGO.transform.SetParent(go.transform, false);
        var labelRect = labelGO.AddComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        label = HUDText.AddTo(labelGO, TextAnchor.MiddleCenter);
    }

    // 인스펙터 값을 매 프레임 반영합니다(Play 중 바꾸면 즉시 반영).
    private void ApplyLayout()
    {
        canvas.sortingOrder = canvasSortingOrder;
        if (Screen.height > 0)
        {
            float screenAspect = (float)Screen.width / Screen.height;
            scaler.matchWidthOrHeight = screenAspect > 1920f / 1080f ? 1f : 0f; // HUD와 같은 규칙
        }

        Vector2 size = new Vector2(Mathf.Max(100f, panelSize.x), Mathf.Max(80f, panelSize.y));
        panelRect.sizeDelta = size;
        panelRect.anchoredPosition = Vector2.zero;
        var style = PopupStyle.Get();
        ApplyRounded(panelImage, style.darkPanelColor, style.darkPanelCornerRadius, size);

        float pad = 20f;
        var msgRect = messageText.rectTransform;
        msgRect.sizeDelta = new Vector2(size.x - pad * 2f, messageFontSize + 12f);
        msgRect.anchoredPosition = new Vector2(0f, -pad - 4f);
        messageText.fontSize = messageFontSize;
        messageText.color = messageColor;

        var detailRect = detailText.rectTransform;
        float buttonsTop = buttonBottomMargin + buttonSize.y;
        float detailTop = pad + 4f + messageFontSize + 12f + 8f;
        detailRect.sizeDelta = new Vector2(size.x - pad * 2f, Mathf.Max(10f, size.y - detailTop - buttonsTop - 8f));
        detailRect.anchoredPosition = new Vector2(0f, -detailTop);
        detailText.fontSize = detailFontSize;
        detailText.color = detailColor;

        float half = (buttonSize.x + buttonSpacing) * 0.5f;
        LayoutButton(yesRect, yesImage, yesText, yesLabel, new Vector2(-half, buttonBottomMargin));
        LayoutButton(noRect, noImage, noText, noLabel, new Vector2(half, buttonBottomMargin));
    }

    private void LayoutButton(RectTransform rect, Image image, TextMeshProUGUI label, string text, Vector2 pos)
    {
        rect.sizeDelta = buttonSize;
        rect.anchoredPosition = pos;
        var style = PopupStyle.Get();
        ApplyRounded(image, image.color, style.darkPanelButtonCornerRadius, buttonSize); // 색은 Update에서 호버에 따라 정함
        label.text = text;
        label.fontSize = buttonFontSize;
        label.color = style.GetButtonTextColor(true);
    }

    // 실행 중에 만든 흰색 둥근 사각형을 9-Slice로 늘려 씁니다(스탯 툴팁/미니맵 구역과 같은 방식).
    private void ApplyRounded(Image image, Color color, float cornerRadius, Vector2 size)
    {
        image.color = color;
        float radius = Mathf.Min(cornerRadius, Mathf.Min(size.x, size.y) * 0.5f);
        if (radius < 0.5f)
        {
            image.sprite = null;
            image.type = Image.Type.Simple;
            return;
        }
        EnsureRoundedSprite();
        image.sprite = roundedSprite;
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = RoundedTexRadius / radius;
    }

    private void EnsureRoundedSprite()
    {
        if (roundedSprite != null) return;

        int size = RoundedTexSize;
        float r = RoundedTexRadius;
        roundedTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        roundedTexture.wrapMode = TextureWrapMode.Clamp;
        roundedTexture.filterMode = FilterMode.Bilinear;

        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float cx = Mathf.Clamp(x + 0.5f, r, size - r);
                float cy = Mathf.Clamp(y + 0.5f, r, size - r);
                float dx = x + 0.5f - cx;
                float dy = y + 0.5f - cy;
                float alpha = Mathf.Clamp01(r - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
            }
        }
        roundedTexture.SetPixels32(pixels);
        roundedTexture.Apply();

        roundedSprite = Sprite.Create(roundedTexture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f),
                                      100f, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
    }
}
