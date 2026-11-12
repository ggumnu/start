using TMPro;
using UnityEngine;
using UnityEngine.UI;

// [110번 장신구 - 충전 수 표시]
// 1발 이상 충전되면 플레이어 머리 위, 장전 R 표시(PlayerReloadIndicator)가 뜨는 자리의 바로 위쪽에 110번 아이콘을 띄우고, 아이콘
// 오른쪽 아래에 숫자(검은 글자 + 흰 테두리)를 표시합니다. 숫자 = 이번 사격에 소모한 탄약 수(누르는 순간 1발 + 충전 수 - 형빈 확정).
// 예) 1발 충전 = "2", 탄창 5발을 모두 쓰면 "5".
//
// - PlayerChargeShot이 처음 붙을 때 자동으로 만들어집니다(씬에 따로 배치할 필요 없음). 씬에 하나만 있습니다.
// - 위치는 PlayerReloadIndicator와 같은 방식(화면 공간 - 플레이어 위치 + 높이를 화면 좌표로 변환)이고, R 표시의 높이/오프셋/크기를
//   씬의 PlayerReloadIndicator에서 읽어 그 위쪽에 놓습니다(없으면 R 표시 기본값 기준). 충전 중에는 장전을 할 수 없어 두 표시가 동시에
//   뜨지 않습니다.
// - 크기/간격/글자 크기/테두리는 110번 Item Definition의 Settings(충전 수 표시)에서 조절합니다.
// - 정렬 순서는 R 표시와 같은 5(F 안내 10보다 낮게 - 겹치면 F 안내가 위)입니다.
// - [흰 테두리] TMP 자체 외곽선(HUDText.SetOutline)은 이 글자 크기에서 최대 두께에 걸려 글자 뒤에 흰 반투명 사각형이 비쳐 보였습니다
//   (외곽선 + 부풀림이 글꼴 여백 끝까지 차서 글자 칸 전체가 옅게 칠해짐). 그래서 외곽선 재질은 쓰지 않고, 같은 숫자를 흰색으로 8개
//   만들어 사방(테두리 두께만큼)으로 밀어 깐 뒤 그 위에 검은 숫자를 올립니다.
public class ChargeShotIndicator : MonoBehaviour
{
    private const int CanvasSortingOrder = 5;
    private static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

    // PlayerReloadIndicator가 씬에 없을 때 쓰는 R 표시 기본값(PlayerReloadIndicator의 기본값과 같음)
    private const float DefaultReloadHeight = 2f;
    private static readonly Vector2 DefaultReloadOffset = new Vector2(55f, 0f);
    private const float DefaultReloadSize = 56f;

    private static ChargeShotIndicator instance;

    private PlayerChargeShot chargeShot;
    private PlayerReloadIndicator reloadIndicator;

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
    private int shownNumber = -1;

    // chargeShot을 따라다니는 표시를 하나 만들어 둡니다(이미 있으면 대상만 바꿈).
    public static void EnsureExists(PlayerChargeShot target)
    {
        if (instance == null)
        {
            var go = new GameObject("ChargeShotIndicator");
            instance = go.AddComponent<ChargeShotIndicator>();
        }
        instance.chargeShot = target;
    }

    void Awake()
    {
        Build();
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    void Build()
    {
        var canvasGO = new GameObject("ChargeShotIndicatorCanvas");
        canvasGO.transform.SetParent(transform, false);
        canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = CanvasSortingOrder;

        canvasScaler = canvasGO.AddComponent<CanvasScaler>();
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = ReferenceResolution;
        canvasRect = canvasGO.GetComponent<RectTransform>();

        root = new GameObject("ChargeIndicator");
        root.transform.SetParent(canvasGO.transform, false);
        rootRect = root.AddComponent<RectTransform>();
        rootRect.anchorMin = rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.sizeDelta = Vector2.zero;

        // 110번 아이콘
        var iconGO = new GameObject("ChargeIcon");
        iconGO.transform.SetParent(root.transform, false);
        iconRect = iconGO.AddComponent<RectTransform>();
        iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconImage = iconGO.AddComponent<Image>();
        iconImage.preserveAspect = true;
        iconImage.raycastTarget = false;

        // 숫자 - 아이콘 오른쪽 아래 모서리에 글자 중심을 둡니다.
        var numberGO = new GameObject("ChargeCount");
        numberGO.transform.SetParent(iconGO.transform, false);
        numberRect = numberGO.AddComponent<RectTransform>();
        numberRect.anchorMin = numberRect.anchorMax = new Vector2(1f, 0f);
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
            outlineTexts[k].color = Color.white;
        }

        var faceGO = new GameObject("Face");
        faceGO.transform.SetParent(numberGO.transform, false);
        var faceRect = faceGO.AddComponent<RectTransform>();
        faceRect.anchorMin = Vector2.zero;
        faceRect.anchorMax = Vector2.one;
        faceRect.sizeDelta = Vector2.zero;
        numberText = HUDText.AddTo(faceGO, TextAnchor.MiddleCenter);
        numberText.color = new Color32(0x2C, 0x45, 0x63, 0xFF); // UI 글자 기본색(예전 검정에서 변경)

        root.SetActive(false);
    }

    void Update()
    {
        UpdateCanvasScalerMatch();

        int number = chargeShot != null && chargeShot.ChargeCount >= 1 ? chargeShot.ConsumedAmmoCount : 0;
        var settings = chargeShot != null ? chargeShot.ActiveSettings : null;
        if (number <= 0 || settings == null)
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

        // 표시 중일 때만 찾습니다(씬이 바뀌어 사라졌으면 다시 찾음).
        if (reloadIndicator == null) reloadIndicator = Object.FindFirstObjectByType<PlayerReloadIndicator>();

        float reloadHeight = reloadIndicator != null ? reloadIndicator.indicatorHeight : DefaultReloadHeight;
        Vector2 reloadOffset = reloadIndicator != null ? reloadIndicator.indicatorOffset : DefaultReloadOffset;
        float reloadSize = reloadIndicator != null ? reloadIndicator.indicatorSize : DefaultReloadSize;

        Vector3 worldPoint = chargeShot.transform.position + Vector3.up * reloadHeight;
        Vector3 screenPoint = mainCamera.WorldToScreenPoint(worldPoint);
        if (screenPoint.z < 0f)
        {
            Hide();
            return;
        }

        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, null, out Vector2 localPoint);

        root.SetActive(true);
        rootRect.anchoredPosition = localPoint;

        // R 표시 자리(reloadOffset)의 바로 위: R 크기의 절반 + 간격 + 아이콘 크기의 절반만큼 위로.
        float iconSize = settings.indicatorIconSize;
        Vector2 iconPos = reloadOffset + new Vector2(0f, reloadSize * 0.5f + settings.indicatorGap + iconSize * 0.5f)
                          + settings.indicatorExtraOffset;
        iconRect.anchoredPosition = iconPos;
        iconRect.sizeDelta = new Vector2(iconSize, iconSize);

        Sprite icon = chargeShot.ActiveIcon;
        iconImage.sprite = icon;
        iconImage.enabled = icon != null;

        numberRect.anchoredPosition = settings.numberOffset;
        if (!Mathf.Approximately(numberText.fontSize, settings.numberFontSize))
        {
            numberText.fontSize = settings.numberFontSize;
            foreach (var outline in outlineTexts) outline.fontSize = settings.numberFontSize;
        }
        ApplyOutlineOffsets(settings.numberOutlinePixels);

        if (number != shownNumber)
        {
            shownNumber = number;
            string label = number.ToString();
            numberText.text = label;
            foreach (var outline in outlineTexts) outline.text = label;
        }
    }

    // 흰 글자 8개를 테두리 두께만큼 사방(상하좌우 + 대각선)으로 밀어 둡니다. 두께가 0이면 흰 글자를 숨깁니다.
    void ApplyOutlineOffsets(float pixels)
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

    void Hide()
    {
        if (root != null && root.activeSelf) root.SetActive(false);
    }

    // 화면 비율이 16:9와 다를 때 크기가 일정하게 보이도록 PlayerReloadIndicator와 같은 규칙으로 맞춥니다.
    void UpdateCanvasScalerMatch()
    {
        if (canvasScaler == null || Screen.height <= 0) return;
        float screenAspect = (float)Screen.width / Screen.height;
        float referenceAspect = ReferenceResolution.x / ReferenceResolution.y;
        canvasScaler.matchWidthOrHeight = screenAspect > referenceAspect ? 1f : 0f;
    }
}
