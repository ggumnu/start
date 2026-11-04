using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

// 화면 오른쪽 아래에 뜨는 탄약 박스 UI입니다. PlayerHUDManager가 만든 CanvasRoot 아래에
// 이 스크립트가 직접 자신의 패널을 만들어 붙입니다 (미니맵/스탯 패널과 마찬가지로 서로
// 독립된 위치이며, PlayerHUDManager 쪽에 미리 만들어둔 컨테이너를 쓰지 않습니다).
//
// [표시 항목 4가지]
// 1. 현재 탄약 (PlayerShooting.CurrentAmmo) - 사격할 때마다 실시간으로 줄어듭니다.
// 2. 최대 탄창 (PlayerStats.MagazineSize)
// 3. 연사 (PlayerStats.Multishot) - 숫자만 표시합니다. "연사" 글자는 배경 스프라이트에
//    이미 그려져 있습니다.
// 4. 장전 여부 (PlayerShooting.IsReloading) - 평소 아이콘은 항상 표시되고, 장전에 들어가면
//    그 위에 장전용 아이콘이 덮어씌워진 뒤 장전 시간 동안 시계방향으로 점점 사라집니다.
//    두 아이콘 스프라이트는 인스펙터에서 직접 넣을 수 있고, 비워두면 단색(노랑/빨강)으로
//    대체 표시됩니다.
//
// [배치 방식 - 완전 수동 좌표]
// 이 패널은 항목이 늘어나거나 줄어드는 목록이 아니라 자리가 고정된 4개 요소라서,
// PlayerStatDisplay.cs처럼 자동으로 줄을 쌓는 대신, PlayerItemInfoDisplay.cs와 비슷하게
// 각 요소의 위치를 패널 좌상단 기준 좌표(Vector2)로 직접 지정합니다. 사용자가 준비할
// 흰색 배경 스프라이트에 아이콘/숫자 자리가 미리 그려져 있을 것이므로, 그 자리에 정확히
// 맞출 수 있도록 각 요소를 완전히 독립적으로 옮길 수 있게 했습니다.
//
// [현재/최대 탄약 구분 - "/" 텍스트]
// 별도의 사선 이미지 오브젝트 없이, 가운데에 있는 작은 Text로 "/" 문자를 표시하는
// 방식으로 구현했습니다 (기획 확정 사항).
//
// [Play 모드 라이브 트윅]
// 다른 HUD 스크립트들과 동일하게, 패널은 한 번만 만들고 매 프레임 위치/크기/색상/텍스트를
// 다시 적용합니다. 인스펙터 값을 Play 도중 바꿔도 즉시 반영됩니다.
//
// [수류탄/해킹툴 보조 박스 - 무기 모드 UI 편의성 작업 때 추가]
// 기본총 모드에서는 탄약 박스 뒤에 숨어 있다가, 수류탄 모드(2번)가 되면 수류탄 보조 박스가, 해킹툴 모드(3번)가 되면
// 해킹툴 보조 박스가 탄약 박스 왼쪽으로 튀어나옵니다. 보조 박스 위에는 그 모드의 아이콘과 남은 개수가 표시됩니다.
// - 그리는 순서: 이미지가 붙은 패널의 자식은 항상 패널 "위"에 그려지므로, 보조 박스를 탄약 박스의 자식으로 둘 수 없습니다.
//   그래서 화면 우하단에 빈 묶음(AmmoBoxGroup)을 만들고 그 아래에 [수류탄 보조 박스, 해킹툴 보조 박스, 기존 탄약 박스(AmmoBoxPanel)]
//   순서로 둡니다. 먼저 있는 것이 먼저(아래에) 그려지므로 보조 박스는 항상 탄약 박스 밑에 깔립니다. 패널 여백(panelMargin)은
//   이제 이 묶음에 적용되고, 탄약 박스는 묶음 안 우하단에 그대로 놓이므로 기존 위치는 변하지 않습니다.
// - 움직임: 박스마다 숨은 위치 <-> 나온 위치(수류탄/해킹툴 각각 따로 - 위아래로 나눠 둘 수 있음) 사이를 subBoxSlideDuration 동안,
//   subBoxSlideCurve(기본: 끝으로 갈수록 느려지는 곡선) 모양으로 움직입니다. [형빈 확정] 수류탄 <-> 해킹툴 전환 시 들어가는 박스와
//   나오는 박스가 동시에 움직이고, 나오는 박스가 들어가는 박스보다 위에 그려집니다. 움직이는 도중 모드를 다시 바꾸면 처음 위치로
//   튀지 않고 "지금 위치"에서 새 목표로 이어서 움직입니다. 위치를 0~1 비율로 기억하므로 Play 중에 두 위치 값을 바꿔도 바로 반영됩니다.
// - 처음 화면에 뜰 때(또는 플레이어가 새로 잡힐 때)는 애니메이션 없이 바로 제자리에 놓습니다.
// - [형빈 확정] 보조 박스 색은 0개여도 바뀌지 않습니다(0개일 때 아이콘 반투명은 플레이어 옆 표시 WeaponModeResourceIndicator만 해당).
// - [형빈 확정] 보조 박스 색(subBoxColor)은 지금은 확인용 회색이고, 최종적으로는 흰색(= 스프라이트 그대로)으로 쓸 예정입니다.
// - 숨은 위치를 맞출 때는 탄약 박스에 가려 안 보이므로, subBoxDebugDrawOnTop을 켜면 보조 박스가 잠시 탄약 박스 위에 그려집니다.
//   위치를 다 맞춘 뒤에는 반드시 꺼 주세요.
// - 모드/개수는 매 프레임 PlayerWeaponMode.Current와 PlayerResources.GetAmount()를 읽습니다(두 스크립트는 수정하지 않음).
[RequireComponent(typeof(RectTransform))]
public class AmmoBoxDisplay : MonoBehaviour
{
    [Header("참조 (비워두면 자동으로 찾습니다)")]
    public PlayerHUDManager hudManager;
    public PlayerShooting playerShooting;
    public PlayerStats playerStats;

    [Header("패널 위치 (화면 우측 하단 모서리 기준 여백, px)")]
    public float panelMarginX = 40f;
    public float panelMarginY = 40f;

    [Header("패널 크기 / 배경")]
    public float panelWidth = 260f;
    public float panelHeight = 140f;
    [Tooltip("배경으로 쓸 흰색 UI 스프라이트입니다. 비워두면 흰색 반투명 사각형으로 대체 표시됩니다.")]
    public Sprite backgroundSprite;
    public Color backgroundFallbackColor = new Color(1f, 1f, 1f, 0.85f);

    [Header("장전 아이콘")]
    [Tooltip("평소 아이콘입니다. 장전 중에도 아래에 그대로 깔려 있습니다. 비워두면 노란색 단색으로 대체 표시됩니다.")]
    public Sprite reloadNormalIcon;
    [Tooltip("장전 중에만 평소 아이콘 위에 덮어씌워지는 아이콘입니다. 장전 시간 동안 시계방향으로 점점 사라집니다. 비워두면 빨간색 단색으로 대체 표시됩니다.")]
    public Sprite reloadActiveIcon;
    public Color reloadNormalFallbackColor = new Color(0.95f, 0.85f, 0.15f, 1f);
    public Color reloadActiveFallbackColor = new Color(0.9f, 0.2f, 0.2f, 1f);
    [Tooltip("패널 좌상단 기준 위치 (x: 오른쪽+, y: 아래쪽으로 갈수록 음수)")]
    public Vector2 reloadIconPosition = new Vector2(30f, -30f);
    public float reloadIconSize = 44f;

    [Header("연사 텍스트")]
    public Vector2 multishotTextPosition = new Vector2(120f, -30f);
    public Vector2 multishotTextSize = new Vector2(110f, 34f);
    public int multishotFontSize = 24;
    public Color multishotTextColor = Color.black;

    [Header("현재 탄약 텍스트")]
    public Vector2 currentAmmoTextPosition = new Vector2(25f, -95f);
    public Vector2 currentAmmoTextSize = new Vector2(70f, 40f);
    public int currentAmmoFontSize = 30;
    public Color currentAmmoTextColor = new Color(0.8f, 0.1f, 0.1f, 1f);

    [Header("구분 기호 ( / )")]
    public Vector2 separatorPosition = new Vector2(100f, -95f);
    public Vector2 separatorSize = new Vector2(20f, 40f);
    public int separatorFontSize = 26;
    public Color separatorTextColor = Color.black;

    [Header("최대 탄창 텍스트")]
    public Vector2 maxAmmoTextPosition = new Vector2(125f, -95f);
    public Vector2 maxAmmoTextSize = new Vector2(70f, 40f);
    public int maxAmmoFontSize = 30;
    public Color maxAmmoTextColor = new Color(0.9f, 0.5f, 0.05f, 1f);

    [Header("수류탄/해킹툴 보조 박스 - 참조 (비워두면 자동으로 찾습니다)")]
    public PlayerWeaponMode weaponMode;
    public PlayerResources playerResources;
    [Tooltip("보조 박스 아이콘 칸이 비어 있을 때 수류탄/해킹툴 아이콘을 빌려올 왼쪽 위 자원 UI입니다.")]
    public PlayerResourceDisplay resourceDisplay;

    [Header("수류탄/해킹툴 보조 박스 - 모양")]
    [Tooltip("보조 박스 배경 스프라이트입니다(UI_AmmoBox). 비워두면 단색 사각형으로 표시됩니다.")]
    public Sprite subBoxSprite;
    [Tooltip("보조 박스에 곱할 색입니다. 흰색이면 스프라이트 그대로 나옵니다(지금은 확인용 회색).")]
    public Color subBoxColor = new Color(0.62f, 0.62f, 0.62f, 1f);
    [Tooltip("보조 박스 크기입니다(px).")]
    public Vector2 subBoxSize = new Vector2(150f, 80f);

    [Header("수류탄/해킹툴 보조 박스 - 위치 (탄약 박스 우하단 모서리 기준, x: 왼쪽으로 갈수록 음수, y: 위쪽+)")]
    // [박스별 위치로 분리] 처음엔 두 보조 박스가 같은 위치를 썼는데, 수류탄/해킹툴을 위아래로 나눠 둘 수 있게 박스마다 따로 둡니다.
    // 수류탄 칸은 예전 공용 칸(subBoxHiddenPosition/subBoxShownPosition)에 맞춰 둔 값을 그대로 이어받고, 해킹툴 칸은 처음 한 번
    // 수류탄 값으로 채워집니다(아래 OnValidate 참고) - 맞춰 둔 위치가 사라지지 않게 하기 위함입니다.
    [Tooltip("수류탄 보조 박스가 숨어 있을 때 우하단 모서리 위치입니다. 탄약 박스에 완전히 가려지는 곳으로 맞춰 주세요.")]
    [FormerlySerializedAs("subBoxHiddenPosition")]
    public Vector2 subBoxGrenadeHiddenPosition = new Vector2(-60f, 50f);
    [Tooltip("수류탄 보조 박스가 튀어나왔을 때 우하단 모서리 위치입니다.")]
    [FormerlySerializedAs("subBoxShownPosition")]
    public Vector2 subBoxGrenadeShownPosition = new Vector2(-190f, 50f);
    [Tooltip("해킹툴 보조 박스가 숨어 있을 때 우하단 모서리 위치입니다. 탄약 박스에 완전히 가려지는 곳으로 맞춰 주세요.")]
    public Vector2 subBoxHackHiddenPosition = new Vector2(-60f, 10f);
    [Tooltip("해킹툴 보조 박스가 튀어나왔을 때 우하단 모서리 위치입니다.")]
    public Vector2 subBoxHackShownPosition = new Vector2(-190f, 10f);
    // 해킹툴 위치를 수류탄 값으로 한 번 채웠는지 기록합니다(인스펙터에는 안 보임).
    [SerializeField, HideInInspector] private bool subBoxHackPositionInitialized = false;
    [Tooltip("켜면 보조 박스가 탄약 박스 위에 그려집니다(숨은 위치를 맞출 때만 잠깐 켜고 반드시 끄세요).")]
    public bool subBoxDebugDrawOnTop = false;

    [Header("수류탄/해킹툴 보조 박스 - 움직임")]
    [Tooltip("숨은 위치 <-> 나온 위치를 오가는 데 걸리는 시간(초)입니다.")]
    [Min(0.01f)] public float subBoxSlideDuration = 0.25f;
    [Tooltip("움직임 모양입니다. 가로 = 시간(0~1), 세로 = 이동 비율(0~1). 기본값은 끝으로 갈수록 천천히 멈추는 곡선입니다.")]
    public AnimationCurve subBoxSlideCurve = new AnimationCurve(new Keyframe(0f, 0f, 0f, 2.5f), new Keyframe(1f, 1f, 0f, 0f));

    [Header("수류탄/해킹툴 보조 박스 - 아이콘 / 개수 (보조 박스 좌상단 기준, y: 아래쪽으로 갈수록 음수)")]
    [Tooltip("비워두면 PlayerResourceDisplay의 수류탄 아이콘을 사용합니다.")]
    public Sprite subBoxGrenadeIcon;
    [Tooltip("비워두면 PlayerResourceDisplay의 해킹툴 아이콘을 사용합니다.")]
    public Sprite subBoxHackingToolIcon;
    public Vector2 subBoxIconPosition = new Vector2(35f, -40f);
    public float subBoxIconSize = 44f;
    public Vector2 subBoxCountTextPosition = new Vector2(60f, -20f);
    public Vector2 subBoxCountTextSize = new Vector2(50f, 40f);
    public int subBoxCountFontSize = 28;
    public Color subBoxCountTextColor = Color.black;

    private GameObject groupRoot;
    private RectTransform groupRect;

    // 보조 박스 하나(수류탄 또는 해킹툴)의 오브젝트와 움직임 상태입니다.
    private class SubBox
    {
        public BasicItemType resourceType;
        public RectTransform rect;
        public Image image;
        public RectTransform iconRect;
        public Image iconImage;
        public RectTransform countRect;
        public TextMeshProUGUI countText;
        public int shownCount = int.MinValue;

        public bool targetShown;   // 지금 목표가 "나온 상태"인지
        public float fromRatio;    // 이번 움직임을 시작한 비율(0 = 숨음, 1 = 나옴)
        public float currentRatio; // 지금 비율
        public float moveStartTime;
    }

    private SubBox grenadeBox;
    private SubBox hackBox;
    private bool snapSubBoxes = true; // true면 다음 프레임에 애니메이션 없이 제자리로 놓습니다.

    private GameObject panelRoot;
    private RectTransform panelRect;
    private Image panelImage;

    private RectTransform reloadIconRect;
    private Image reloadIconImage;
    private Image reloadActiveImage;

    // 장전 진행도 계산용입니다. PlayerShooting은 장전 남은 시간을 공개하지 않으므로, 장전이
    // 시작되는 순간의 시각과 장전 시간을 여기서 직접 기록해 둡니다.
    private bool wasReloading = false;
    private float reloadStartTime = 0f;
    private float reloadDuration = 1f;

    // 스프라이트가 비어 있어도 원형 채움(Filled)이 동작하도록 쓰는 흰색 1x1 스프라이트입니다.
    private Texture2D whiteTexture;
    private Sprite whiteSprite;

    private RectTransform multishotRect;
    private TextMeshProUGUI multishotText;

    private RectTransform currentAmmoRect;
    private TextMeshProUGUI currentAmmoText;

    private RectTransform separatorRect;
    private TextMeshProUGUI separatorText;

    private RectTransform maxAmmoRect;
    private TextMeshProUGUI maxAmmoText;

    private bool built = false;

    void Update()
    {
        if (!EnsureReferences()) return;

        EnsureBuilt();
        ApplyPanelPosition();
        ApplyLiveValues();
        Reflow();
        ApplySubBoxes();
    }

    // PlayerShooting을 먼저 찾고, PlayerStats는 같은 오브젝트에서 가져옵니다
    // (PlayerShooting이 [RequireComponent(typeof(PlayerStats))]라 항상 같이 붙어있습니다).
    private bool EnsureReferences()
    {
        if (hudManager == null)
        {
            hudManager = Object.FindFirstObjectByType<PlayerHUDManager>();
            if (hudManager == null || hudManager.CanvasRoot == null) return false;
        }

        if (playerShooting == null)
        {
            playerShooting = Object.FindFirstObjectByType<PlayerShooting>();
            if (playerShooting == null) return false;
        }

        if (playerStats == null)
        {
            playerStats = playerShooting.GetComponent<PlayerStats>();
            if (playerStats == null) return false;
        }

        return true;
    }

    private void EnsureBuilt()
    {
        if (built) return;

        BuildWhiteSprite();
        BuildPanel(hudManager.CanvasRoot);
        built = true;
    }

    private void ApplyPanelPosition()
    {
        // 화면 우하단 여백은 묶음(AmmoBoxGroup)에 적용하고, 탄약 박스는 묶음 안 우하단에 그대로 붙입니다(보이는 위치는 예전과 같음).
        groupRect.anchorMin = new Vector2(1f, 0f);
        groupRect.anchorMax = new Vector2(1f, 0f);
        groupRect.pivot = new Vector2(1f, 0f);
        groupRect.anchoredPosition = new Vector2(-panelMarginX, panelMarginY);
        groupRect.sizeDelta = new Vector2(panelWidth, panelHeight);

        panelRect.anchorMin = new Vector2(1f, 0f);
        panelRect.anchorMax = new Vector2(1f, 0f);
        panelRect.pivot = new Vector2(1f, 0f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(panelWidth, panelHeight);

        if (backgroundSprite != null)
        {
            panelImage.sprite = backgroundSprite;
            panelImage.color = Color.white;
        }
        else
        {
            panelImage.sprite = null;
            panelImage.color = backgroundFallbackColor;
        }
    }

    // 매 프레임 실시간 값(현재 탄약/장전 여부)과 스탯 값(최대 탄창/연사)을 갱신합니다.
    private void ApplyLiveValues()
    {
        bool isReloading = playerShooting.IsReloading;

        // 평소 아이콘: 장전 여부와 상관없이 항상 그대로 보입니다.
        if (reloadNormalIcon != null)
        {
            reloadIconImage.sprite = reloadNormalIcon;
            reloadIconImage.color = Color.white;
        }
        else
        {
            reloadIconImage.sprite = null;
            reloadIconImage.color = reloadNormalFallbackColor;
        }

        // 장전이 막 시작된 순간의 시각과 장전 시간을 기록합니다. PlayerShooting.CurrentReloadDuration은
        // 15번 패시브(남은 탄약 적을 때 장전 시간 감소) 같은 보너스가 이미 반영된, 이번 장전에
        // 실제로 걸리는 시간이라서(PlayerStats.ReloadTime은 보너스 반영 전 스탯값), 이걸 써야
        // 진행률 표시가 실제 장전 완료 시점과 정확히 맞습니다.
        if (isReloading && !wasReloading)
        {
            reloadStartTime = Time.time;
            reloadDuration = Mathf.Max(playerShooting.CurrentReloadDuration, 0.01f);
        }
        wasReloading = isReloading;

        // 장전용 아이콘: 장전 중에만 켜지고, 남은 비율(1 -> 0)만큼만 그려집니다. 반시계 방향으로
        // 채우는 설정에서 채움량이 줄어들면, 사라지는 경계가 시계방향으로 움직입니다.
        if (reloadActiveImage.enabled != isReloading) reloadActiveImage.enabled = isReloading;

        if (isReloading)
        {
            float elapsed01 = Mathf.Clamp01((Time.time - reloadStartTime) / reloadDuration);

            if (reloadActiveIcon != null)
            {
                reloadActiveImage.sprite = reloadActiveIcon;
                reloadActiveImage.color = Color.white;
            }
            else
            {
                reloadActiveImage.sprite = whiteSprite;
                reloadActiveImage.color = reloadActiveFallbackColor;
            }

            reloadActiveImage.fillAmount = 1f - elapsed01;
        }

        multishotText.text = playerStats.Multishot.ToString(CultureInfo.InvariantCulture);
        multishotText.fontSize = multishotFontSize;
        multishotText.color = multishotTextColor;

        currentAmmoText.text = playerShooting.CurrentAmmo.ToString(CultureInfo.InvariantCulture);
        currentAmmoText.fontSize = currentAmmoFontSize;
        currentAmmoText.color = currentAmmoTextColor;

        separatorText.fontSize = separatorFontSize;
        separatorText.color = separatorTextColor;

        maxAmmoText.text = playerStats.MagazineSize.ToString(CultureInfo.InvariantCulture);
        maxAmmoText.fontSize = maxAmmoFontSize;
        maxAmmoText.color = maxAmmoTextColor;
    }

    // 각 요소의 위치/크기를 인스펙터 값 그대로 매 프레임 다시 적용합니다 (Play 중 실시간 트윅용).
    private void Reflow()
    {
        reloadIconRect.anchoredPosition = reloadIconPosition;
        reloadIconRect.sizeDelta = new Vector2(reloadIconSize, reloadIconSize);

        multishotRect.anchoredPosition = multishotTextPosition;
        multishotRect.sizeDelta = multishotTextSize;

        currentAmmoRect.anchoredPosition = currentAmmoTextPosition;
        currentAmmoRect.sizeDelta = currentAmmoTextSize;

        separatorRect.anchoredPosition = separatorPosition;
        separatorRect.sizeDelta = separatorSize;

        maxAmmoRect.anchoredPosition = maxAmmoTextPosition;
        maxAmmoRect.sizeDelta = maxAmmoTextSize;
    }

    private void BuildWhiteSprite()
    {
        whiteTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        whiteTexture.SetPixel(0, 0, Color.white);
        whiteTexture.Apply();
        whiteSprite = Sprite.Create(whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 100f);
    }

    private void BuildPanel(Transform canvasRoot)
    {
        // 묶음 - 이미지 없는 빈 오브젝트입니다. 자식 순서 = 그리는 순서(먼저 있는 것이 아래).
        groupRoot = new GameObject("AmmoBoxGroup");
        groupRoot.transform.SetParent(canvasRoot, false);
        groupRect = groupRoot.AddComponent<RectTransform>();
        groupRect.anchorMin = new Vector2(1f, 0f);
        groupRect.anchorMax = new Vector2(1f, 0f);
        groupRect.pivot = new Vector2(1f, 0f);
        groupRect.sizeDelta = new Vector2(panelWidth, panelHeight);

        // 보조 박스 2개를 탄약 박스보다 먼저 만들어 탄약 박스 밑에 깔리게 합니다.
        grenadeBox = BuildSubBox(groupRoot.transform, "SubBox_Grenade", BasicItemType.Grenade);
        hackBox = BuildSubBox(groupRoot.transform, "SubBox_Hack", BasicItemType.HackingTool);

        panelRoot = new GameObject("AmmoBoxPanel");
        panelRoot.transform.SetParent(groupRoot.transform, false);

        panelRect = panelRoot.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1f, 0f);
        panelRect.anchorMax = new Vector2(1f, 0f);
        panelRect.pivot = new Vector2(1f, 0f);
        panelRect.sizeDelta = new Vector2(panelWidth, panelHeight);

        panelImage = panelRoot.AddComponent<Image>();

        // 장전 아이콘 - 패널 좌상단 기준
        var reloadGO = new GameObject("ReloadIcon");
        reloadGO.transform.SetParent(panelRoot.transform, false);
        reloadIconRect = reloadGO.AddComponent<RectTransform>();
        reloadIconRect.anchorMin = new Vector2(0f, 1f);
        reloadIconRect.anchorMax = new Vector2(0f, 1f);
        reloadIconRect.pivot = new Vector2(0.5f, 0.5f);
        reloadIconImage = reloadGO.AddComponent<Image>();
        reloadIconImage.preserveAspect = true;

        // 장전용 아이콘 - 평소 아이콘의 자식으로 같은 영역을 꽉 채워 덮어씌웁니다(위치/크기를
        // 따로 맞출 필요가 없습니다). 원형(시계) 채움 방식이라 스프라이트가 필요합니다.
        var reloadActiveGO = new GameObject("ReloadActiveIcon");
        reloadActiveGO.transform.SetParent(reloadGO.transform, false);
        var reloadActiveRect = reloadActiveGO.AddComponent<RectTransform>();
        reloadActiveRect.anchorMin = Vector2.zero;
        reloadActiveRect.anchorMax = Vector2.one;
        reloadActiveRect.offsetMin = Vector2.zero;
        reloadActiveRect.offsetMax = Vector2.zero;
        reloadActiveImage = reloadActiveGO.AddComponent<Image>();
        reloadActiveImage.preserveAspect = true;
        reloadActiveImage.type = Image.Type.Filled;
        reloadActiveImage.fillMethod = Image.FillMethod.Radial360;
        reloadActiveImage.fillOrigin = (int)Image.Origin360.Top;
        reloadActiveImage.fillClockwise = false;
        reloadActiveImage.fillAmount = 1f;
        reloadActiveImage.enabled = false;

        // 연사 텍스트
        multishotRect = CreateText(panelRoot.transform, "MultishotText", out multishotText, TextAnchor.MiddleLeft);

        // 현재 탄약 텍스트
        currentAmmoRect = CreateText(panelRoot.transform, "CurrentAmmoText", out currentAmmoText, TextAnchor.MiddleCenter);

        // 구분 기호
        separatorRect = CreateText(panelRoot.transform, "Separator", out separatorText, TextAnchor.MiddleCenter);
        separatorText.text = "/";

        // 최대 탄창 텍스트
        maxAmmoRect = CreateText(panelRoot.transform, "MaxAmmoText", out maxAmmoText, TextAnchor.MiddleCenter);
    }

    // ───────────── 수류탄/해킹툴 보조 박스 ─────────────

    // 박스별 위치로 나눈 직후 딱 한 번, 해킹툴 위치를 수류탄 위치(예전 공용 칸에서 이어받은 값)로 채웁니다.
    // 이후로는 두 칸이 완전히 따로 움직입니다. 에디터에서 컴포넌트를 불러올 때 실행됩니다.
    void OnValidate()
    {
        if (subBoxHackPositionInitialized) return;
        subBoxHackHiddenPosition = subBoxGrenadeHiddenPosition;
        subBoxHackShownPosition = subBoxGrenadeShownPosition;
        subBoxHackPositionInitialized = true;
    }

    // 보조 박스 하나(배경 + 아이콘 + 개수)를 만듭니다. 위치/크기/색은 ApplySubBoxes()가 매 프레임 채워 넣습니다.
    private SubBox BuildSubBox(Transform parent, string name, BasicItemType type)
    {
        var box = new SubBox { resourceType = type };

        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        box.rect = go.AddComponent<RectTransform>();
        box.rect.anchorMin = new Vector2(1f, 0f);
        box.rect.anchorMax = new Vector2(1f, 0f);
        box.rect.pivot = new Vector2(1f, 0f);
        box.image = go.AddComponent<Image>();
        box.image.raycastTarget = false;

        // 아이콘 - 보조 박스 좌상단 기준
        var iconGO = new GameObject("Icon");
        iconGO.transform.SetParent(go.transform, false);
        box.iconRect = iconGO.AddComponent<RectTransform>();
        box.iconRect.anchorMin = new Vector2(0f, 1f);
        box.iconRect.anchorMax = new Vector2(0f, 1f);
        box.iconRect.pivot = new Vector2(0.5f, 0.5f);
        box.iconImage = iconGO.AddComponent<Image>();
        box.iconImage.preserveAspect = true;
        box.iconImage.raycastTarget = false;

        // 개수 - 보조 박스 좌상단 기준 (다른 탄약 박스 글자와 같은 방식)
        box.countRect = CreateText(go.transform, "Count", out box.countText, TextAnchor.MiddleCenter);
        box.countText.raycastTarget = false;

        go.SetActive(false);
        return box;
    }

    // 현재 무기 모드에 맞춰 보조 박스의 목표(나옴/숨음)를 정하고, 움직임/그리는 순서/모양/개수를 반영합니다.
    private void ApplySubBoxes()
    {
        if (grenadeBox == null || hackBox == null) return;

        // 참조 찾기 - 무기 모드를 못 찾으면 둘 다 숨긴 상태로 둡니다(탄약 박스 자체는 정상 동작).
        if (weaponMode == null)
        {
            weaponMode = playerShooting.GetComponent<PlayerWeaponMode>();
            if (weaponMode == null) weaponMode = Object.FindFirstObjectByType<PlayerWeaponMode>();
            // 플레이어를 새로 잡았으면 애니메이션 없이 지금 모드 자리에 바로 놓습니다.
            if (weaponMode != null) snapSubBoxes = true;
        }
        if (playerResources == null && weaponMode != null)
        {
            playerResources = weaponMode.resources != null ? weaponMode.resources : weaponMode.GetComponent<PlayerResources>();
            if (playerResources == null) playerResources = Object.FindFirstObjectByType<PlayerResources>();
        }

        PlayerWeapon mode = weaponMode != null ? weaponMode.Current : PlayerWeapon.Gun;
        bool grenadeTarget = mode == PlayerWeapon.Grenade;
        bool hackTarget = mode == PlayerWeapon.Hack;

        // 일시정지/슬로우와 상관없이 UI는 일정한 속도로 움직이도록 실제 시간을 씁니다.
        float now = Time.unscaledTime;

        if (snapSubBoxes)
        {
            SnapSubBox(grenadeBox, grenadeTarget);
            SnapSubBox(hackBox, hackTarget);
            snapSubBoxes = false;
        }
        else
        {
            SetSubBoxTarget(grenadeBox, grenadeTarget, now);
            SetSubBoxTarget(hackBox, hackTarget, now);
        }

        UpdateSubBoxRatio(grenadeBox, now);
        UpdateSubBoxRatio(hackBox, now);

        ApplySubBoxOrder();
        ApplySubBoxVisual(grenadeBox);
        ApplySubBoxVisual(hackBox);
    }

    private void SnapSubBox(SubBox box, bool shown)
    {
        box.targetShown = shown;
        box.currentRatio = shown ? 1f : 0f;
        box.fromRatio = box.currentRatio;
        box.moveStartTime = -999f;
    }

    // 목표가 바뀐 순간에만, "지금 비율"에서 새 목표로 가는 움직임을 새로 시작합니다(중간에 바꿔도 튀지 않음).
    private void SetSubBoxTarget(SubBox box, bool shown, float now)
    {
        if (box.targetShown == shown) return;
        box.targetShown = shown;
        box.fromRatio = box.currentRatio;
        box.moveStartTime = now;
    }

    private void UpdateSubBoxRatio(SubBox box, float now)
    {
        float to = box.targetShown ? 1f : 0f;
        float t = Mathf.Clamp01((now - box.moveStartTime) / Mathf.Max(subBoxSlideDuration, 0.01f));
        if (t >= 1f)
        {
            box.currentRatio = to;
            return;
        }

        float eased = (subBoxSlideCurve != null && subBoxSlideCurve.length > 0) ? subBoxSlideCurve.Evaluate(t) : t;
        box.currentRatio = Mathf.LerpUnclamped(box.fromRatio, to, eased);
    }

    // 그리는 순서: [아래] 들어가는 보조 박스 -> 나오는 보조 박스 -> 탄약 박스 [위].
    // subBoxDebugDrawOnTop이 켜져 있으면 탄약 박스를 맨 아래로 내려 보조 박스가 위에 보이게 합니다.
    private void ApplySubBoxOrder()
    {
        SubBox front = hackBox.targetShown ? hackBox : (grenadeBox.targetShown ? grenadeBox : null);
        if (front != null)
        {
            SubBox back = front == hackBox ? grenadeBox : hackBox;
            int backIndex = back.rect.GetSiblingIndex();
            int frontIndex = front.rect.GetSiblingIndex();
            if (frontIndex < backIndex) front.rect.SetSiblingIndex(backIndex);
        }

        if (subBoxDebugDrawOnTop)
        {
            if (panelRect.GetSiblingIndex() != 0) panelRect.SetAsFirstSibling();
        }
        else
        {
            if (panelRect.GetSiblingIndex() != panelRect.parent.childCount - 1) panelRect.SetAsLastSibling();
        }
    }

    private void ApplySubBoxVisual(SubBox box)
    {
        // 완전히 숨은 상태면 꺼 둡니다(디버그 표시 중에는 숨은 위치를 볼 수 있게 켜 둠).
        bool visible = box.currentRatio > 0.0001f || subBoxDebugDrawOnTop;
        if (box.rect.gameObject.activeSelf != visible) box.rect.gameObject.SetActive(visible);
        if (!visible) return;

        box.rect.sizeDelta = subBoxSize;
        bool isGrenade = box.resourceType == BasicItemType.Grenade;
        Vector2 hidden = isGrenade ? subBoxGrenadeHiddenPosition : subBoxHackHiddenPosition;
        Vector2 shown = isGrenade ? subBoxGrenadeShownPosition : subBoxHackShownPosition;
        box.rect.anchoredPosition = Vector2.LerpUnclamped(hidden, shown, box.currentRatio);

        box.image.sprite = subBoxSprite;
        box.image.color = subBoxColor;

        // 아이콘 - 0개여도 색을 바꾸지 않습니다(형빈 확정).
        Sprite icon = GetSubBoxIcon(box.resourceType);
        box.iconImage.sprite = icon;
        box.iconImage.enabled = icon != null;
        box.iconRect.anchoredPosition = subBoxIconPosition;
        box.iconRect.sizeDelta = new Vector2(subBoxIconSize, subBoxIconSize);

        // 개수
        box.countRect.anchoredPosition = subBoxCountTextPosition;
        box.countRect.sizeDelta = subBoxCountTextSize;
        box.countText.fontSize = subBoxCountFontSize;
        box.countText.color = subBoxCountTextColor;

        int count = playerResources != null ? playerResources.GetAmount(box.resourceType) : 0;
        if (count != box.shownCount)
        {
            box.shownCount = count;
            box.countText.text = count.ToString(CultureInfo.InvariantCulture);
        }
    }

    // 이 컴포넌트에 넣은 아이콘이 우선이고, 비어 있으면 왼쪽 위 자원 UI의 아이콘을 씁니다.
    private Sprite GetSubBoxIcon(BasicItemType type)
    {
        Sprite own = type == BasicItemType.Grenade ? subBoxGrenadeIcon : subBoxHackingToolIcon;
        if (own != null) return own;

        if (resourceDisplay == null) resourceDisplay = Object.FindFirstObjectByType<PlayerResourceDisplay>();
        if (resourceDisplay == null) return null;
        return type == BasicItemType.Grenade ? resourceDisplay.grenadeIcon : resourceDisplay.hackingToolIcon;
    }

    // 패널 좌상단(0,1) 기준으로 배치되는 Text 요소 하나를 만듭니다. 위치/크기는 Reflow()가
    // 매 프레임 채워 넣으므로 여기서는 앵커/폰트/정렬 등 변하지 않는 값만 설정합니다.
    private RectTransform CreateText(Transform parent, string name, out TextMeshProUGUI text, TextAnchor alignment)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);

        // [TMP] 옛 방식 Text는 화면 배율에 따라 흐릿하게 보여서 TextMeshPro로 교체했습니다(HUDText 참고).
        // 줄바꿈 없음 + 박스 크기와 무관하게 항상 보이는 Overflow 동작은 예전 설정과 같습니다(예전에 Truncate였을 때
        // 박스 높이가 폰트 크기보다 작으면 글자가 통째로 사라지던 문제를 Overflow로 고친 것 그대로 유지).
        text = HUDText.AddTo(go, alignment);

        return rect;
    }
}