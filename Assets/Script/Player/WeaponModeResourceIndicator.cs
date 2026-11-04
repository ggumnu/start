using TMPro;
using UnityEngine;
using UnityEngine.UI;

// [수류탄/해킹툴 모드 - 플레이어 옆 재화 표시]
// 수류탄 모드(2번)나 해킹툴 모드(3번)일 때, 플레이어 오른쪽 아래에 그 모드의 아이콘과 남은 개수를 띄웁니다.
// 왼쪽 위 자원 UI는 전투 중에 보기 어려워서, 플레이어 바로 옆에서 남은 개수를 확인할 수 있게 하려는 표시입니다.
// 기본총 모드(1번)에서는 숨깁니다.
//
// - 표시 방식은 110번 충전 표시(ChargeShotIndicator)와 같습니다: 매 프레임 플레이어 위치(+ 높이)를 화면 좌표로 바꿔서
//   이 컴포넌트 전용 Screen Space - Overlay 캔버스 위에 놓습니다. 화면 UI라 벽에 가려지지 않습니다.
// - 숫자는 검은 글자 + 흰 테두리입니다. TMP 외곽선(HUDText.SetOutline)은 이 크기에서 글자 뒤에 흰 반투명 사각형이 비쳐
//   보이는 문제가 있어서, ChargeShotIndicator와 똑같이 흰 글자 8개를 사방으로 밀어 깔고 그 위에 검은 글자를 올립니다.
// - [형빈 확정] 개수가 0개일 때(115번 때문에 0개여도 모드에 들어갈 수 있음) 아이콘만 반투명(iconAlphaWhenZero)으로 하고,
//   숫자는 원래 색 그대로 보여줍니다.
// - [형빈 확정] 아이템 획득 연출 중에도 숨기지 않습니다(모드가 바뀌지 않으므로 그대로 둠).
// - 아이콘 칸을 비워두면 왼쪽 위 자원 UI(PlayerResourceDisplay)에 넣어둔 수류탄/해킹툴 아이콘을 자동으로 가져옵니다.
// - 정렬 순서 기본 5: 장전 R 표시/110번 충전 표시와 같고, F 안내(10)보다 낮게 둬서 겹치면 F 안내가 위에 보입니다.
//
// [씬에 배치하는 법]
// 장전 표시(PlayerReloadIndicator)처럼 HUD 스크립트들이 붙어 있는 오브젝트(또는 빈 오브젝트)에 Add Component로 이 컴포넌트를
// 추가하면 됩니다. PlayerHUDManager의 캔버스와 무관하게 동작합니다. 위치/크기/테두리 값은 Play 중에 바꿔도 바로 반영됩니다.
public class WeaponModeResourceIndicator : MonoBehaviour
{
    [Header("참조 (비워두면 자동으로 찾습니다)")]
    public PlayerWeaponMode weaponMode;
    public PlayerResources playerResources;
    [Tooltip("아이콘 칸이 비어 있을 때 수류탄/해킹툴 아이콘을 빌려올 왼쪽 위 자원 UI입니다.")]
    public PlayerResourceDisplay resourceDisplay;

    [Header("아이콘 (비워두면 PlayerResourceDisplay의 아이콘을 사용)")]
    public Sprite grenadeIcon;
    public Sprite hackingToolIcon;
    [Tooltip("개수가 0개일 때 아이콘의 불투명도입니다(0 = 완전 투명, 1 = 그대로). 숫자는 영향을 받지 않습니다.")]
    [Range(0f, 1f)] public float iconAlphaWhenZero = 0.4f;

    [Header("위치 (화면 공간 - 플레이어 오른쪽 아래)")]
    [Tooltip("플레이어 위치보다 이만큼 위(월드 Y축)인 지점을 화면 좌표로 바꿔 기준점으로 삼습니다. 0이면 발밑입니다.")]
    public float anchorHeight = 0.5f;
    [Tooltip("기준점에서 아이콘 중심까지의 화면상 오프셋입니다(1920x1080 기준 픽셀). x+ = 오른쪽, y+ = 위쪽.")]
    public Vector2 iconOffset = new Vector2(45f, -20f);
    [Tooltip("아이콘의 화면상 크기입니다(1920x1080 기준 픽셀).")]
    public float iconSize = 44f;

    [Header("숫자")]
    [Tooltip("아이콘 오른쪽 아래 모서리를 기준으로 한 숫자 중심 위치 오프셋입니다(픽셀).")]
    public Vector2 numberOffset = new Vector2(4f, 4f);
    public float numberFontSize = 28f;
    public Color numberColor = Color.black;
    public Color numberOutlineColor = Color.white;
    [Tooltip("흰 테두리 두께(픽셀)입니다. 0이면 테두리 없이 표시합니다.")]
    [Min(0f)] public float numberOutlinePixels = 2.5f;

    [Header("화면 해상도 대응 / 정렬 (일반적으로 건드릴 필요 없음)")]
    public Vector2 referenceResolution = new Vector2(1920f, 1080f);
    [Tooltip("F 안내(InteractPromptDisplay, 기본 10)보다 낮게 유지하세요. 기본 5.")]
    public int canvasSortingOrder = 5;

    private Canvas canvas;
    private RectTransform canvasRect;
    private CanvasScaler canvasScaler;
    private GameObject root;
    private RectTransform rootRect;
    private RectTransform iconRect;
    private Image iconImage;
    private RectTransform numberRect;
    private TextMeshProUGUI numberText;
    private readonly TextMeshProUGUI[] outlineTexts = new TextMeshProUGUI[8];
    private readonly RectTransform[] outlineRects = new RectTransform[8];
    private float appliedOutlinePixels = -1f;
    private Camera mainCamera;
    private int shownNumber = int.MinValue;
    private bool built = false;

    void Update()
    {
        if (!EnsureReferences())
        {
            Hide();
            return;
        }

        EnsureBuilt();
        UpdateCanvasScalerMatch();

        // 기본총 모드면 숨깁니다.
        PlayerWeapon mode = weaponMode.Current;
        if (mode != PlayerWeapon.Grenade && mode != PlayerWeapon.Hack)
        {
            Hide();
            return;
        }

        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Hide();
            return;
        }

        Vector3 worldPoint = weaponMode.transform.position + Vector3.up * anchorHeight;
        Vector3 screenPoint = mainCamera.WorldToScreenPoint(worldPoint);
        if (screenPoint.z < 0f)
        {
            Hide();
            return;
        }

        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, null, out Vector2 localPoint);

        if (!root.activeSelf) root.SetActive(true);
        rootRect.anchoredPosition = localPoint;

        // 아이콘
        iconRect.anchoredPosition = iconOffset;
        iconRect.sizeDelta = new Vector2(iconSize, iconSize);

        int amount = playerResources.GetAmount(mode == PlayerWeapon.Grenade ? BasicItemType.Grenade : BasicItemType.HackingTool);

        Sprite icon = GetIcon(mode);
        iconImage.sprite = icon;
        iconImage.enabled = icon != null;
        // 0개면 아이콘만 반투명으로(숫자는 그대로 - 형빈 확정)
        iconImage.color = new Color(1f, 1f, 1f, amount <= 0 ? iconAlphaWhenZero : 1f);

        // 숫자 - "아이콘 오른쪽 아래 모서리 + numberOffset". 숫자는 아이콘의 형제라서 모서리 위치를 여기서 더해 줍니다.
        numberRect.anchoredPosition = iconOffset + new Vector2(iconSize * 0.5f, -iconSize * 0.5f) + numberOffset;
        if (!Mathf.Approximately(numberText.fontSize, numberFontSize))
        {
            numberText.fontSize = numberFontSize;
            foreach (var outline in outlineTexts) outline.fontSize = numberFontSize;
        }
        numberText.color = numberColor;
        foreach (var outline in outlineTexts) outline.color = numberOutlineColor;
        ApplyOutlineOffsets(numberOutlinePixels);

        if (amount != shownNumber)
        {
            shownNumber = amount;
            string label = amount.ToString();
            numberText.text = label;
            foreach (var outline in outlineTexts) outline.text = label;
        }
    }

    // 플레이어 쪽 컴포넌트를 못 찾으면 매 프레임 조용히 다시 찾습니다(레벨 전환 등으로 플레이어가 바뀌어도 대응).
    private bool EnsureReferences()
    {
        if (weaponMode == null)
        {
            weaponMode = Object.FindFirstObjectByType<PlayerWeaponMode>();
            if (weaponMode == null) return false;
        }

        if (playerResources == null)
        {
            playerResources = weaponMode.resources != null ? weaponMode.resources : weaponMode.GetComponent<PlayerResources>();
            if (playerResources == null) playerResources = Object.FindFirstObjectByType<PlayerResources>();
            if (playerResources == null) return false;
        }

        return true;
    }

    // 이 컴포넌트에 넣은 아이콘이 우선이고, 비어 있으면 왼쪽 위 자원 UI의 아이콘을 씁니다.
    private Sprite GetIcon(PlayerWeapon mode)
    {
        Sprite own = mode == PlayerWeapon.Grenade ? grenadeIcon : hackingToolIcon;
        if (own != null) return own;

        if (resourceDisplay == null) resourceDisplay = Object.FindFirstObjectByType<PlayerResourceDisplay>();
        if (resourceDisplay == null) return null;
        return mode == PlayerWeapon.Grenade ? resourceDisplay.grenadeIcon : resourceDisplay.hackingToolIcon;
    }

    private void EnsureBuilt()
    {
        if (built) return;

        var canvasGO = new GameObject("WeaponModeResourceIndicatorCanvas");
        canvasGO.transform.SetParent(transform, false);
        canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = canvasSortingOrder;

        canvasScaler = canvasGO.AddComponent<CanvasScaler>();
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = referenceResolution;
        canvasRect = canvasGO.GetComponent<RectTransform>();

        root = new GameObject("ResourceIndicator");
        root.transform.SetParent(canvasGO.transform, false);
        rootRect = root.AddComponent<RectTransform>();
        rootRect.anchorMin = rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.sizeDelta = Vector2.zero;

        // 아이콘
        var iconGO = new GameObject("ModeIcon");
        iconGO.transform.SetParent(root.transform, false);
        iconRect = iconGO.AddComponent<RectTransform>();
        iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconImage = iconGO.AddComponent<Image>();
        iconImage.preserveAspect = true;
        iconImage.raycastTarget = false;

        // 숫자 - 아이콘 오른쪽 아래 모서리 기준. 아이콘의 자식이 아니라 형제로 둬서, 0개일 때 아이콘을
        // 반투명으로 해도 숫자는 영향을 받지 않게 합니다(아이콘 다음에 만들어 아이콘 위에 그려짐).
        var numberGO = new GameObject("ModeCount");
        numberGO.transform.SetParent(iconGO.transform.parent, false);
        numberRect = numberGO.AddComponent<RectTransform>();
        numberRect.anchorMin = numberRect.anchorMax = new Vector2(0.5f, 0.5f);
        numberRect.pivot = new Vector2(0.5f, 0.5f);
        numberRect.sizeDelta = new Vector2(60f, 40f);

        // 흰 테두리용 글자 8개(먼저 만들어 뒤에 그려짐) + 맨 위 검은 글자
        for (int k = 0; k < outlineTexts.Length; k++)
        {
            var outlineGO = new GameObject("Outline" + k);
            outlineGO.transform.SetParent(numberGO.transform, false);
            outlineRects[k] = outlineGO.AddComponent<RectTransform>();
            outlineRects[k].anchorMin = Vector2.zero;
            outlineRects[k].anchorMax = Vector2.one;
            outlineRects[k].sizeDelta = Vector2.zero;
            outlineTexts[k] = HUDText.AddTo(outlineGO, TextAnchor.MiddleCenter);
            outlineTexts[k].raycastTarget = false;
        }

        var faceGO = new GameObject("Face");
        faceGO.transform.SetParent(numberGO.transform, false);
        var faceRect = faceGO.AddComponent<RectTransform>();
        faceRect.anchorMin = Vector2.zero;
        faceRect.anchorMax = Vector2.one;
        faceRect.sizeDelta = Vector2.zero;
        numberText = HUDText.AddTo(faceGO, TextAnchor.MiddleCenter);
        numberText.raycastTarget = false;

        root.SetActive(false);
        built = true;
    }

    // 흰 글자 8개를 테두리 두께만큼 사방(상하좌우 + 대각선)으로 밀어 둡니다. 두께가 0이면 흰 글자를 숨깁니다.
    private void ApplyOutlineOffsets(float pixels)
    {
        if (Mathf.Approximately(pixels, appliedOutlinePixels)) return;
        appliedOutlinePixels = pixels;

        bool show = pixels > 0f;
        for (int k = 0; k < outlineRects.Length; k++)
        {
            float angle = k * Mathf.PI * 0.25f;
            outlineRects[k].anchoredPosition = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * pixels;
            outlineTexts[k].enabled = show;
        }
    }

    private void Hide()
    {
        if (root != null && root.activeSelf) root.SetActive(false);
    }

    // 화면 비율이 16:9와 다를 때 크기가 일정하게 보이도록 PlayerReloadIndicator와 같은 규칙으로 맞춥니다.
    private void UpdateCanvasScalerMatch()
    {
        if (canvasScaler == null || Screen.height <= 0 || referenceResolution.y <= 0f) return;
        float screenAspect = (float)Screen.width / Screen.height;
        float referenceAspect = referenceResolution.x / referenceResolution.y;
        canvasScaler.matchWidthOrHeight = screenAspect > referenceAspect ? 1f : 0f;
    }
}
