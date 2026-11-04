using UnityEngine;
using UnityEngine.UI;

// 장전 중일 때 플레이어 머리 오른쪽에 뜨는 장전 표시입니다(v1.37).
//
// [탄약 박스 UI(AmmoBoxDisplay.cs)와의 관계]
// 시각 효과(평소 R 아이콘 위에 반전된 R 아이콘이 원형으로 채워졌다가 장전 시간 동안
// 시계방향으로 사라지는 것)는 AmmoBoxDisplay.cs의 장전 아이콘과 완전히 동일한 방식입니다
// (Image.type = Filled, fillMethod = Radial360, fillOrigin = Top, fillClockwise = false로
// 두고 fillAmount를 1 -> 0으로 줄이면, 반시계 방향으로 채움이 줄어드는 것처럼 계산되지만
// 화면에는 사라지는 경계가 시계방향으로 도는 것처럼 보입니다). 장전이 시작되는 순간의 시각과
// PlayerShooting.CurrentReloadDuration(보너스까지 반영된 실제 장전 시간)을 여기서 직접
// 기록해 진행률을 계산하는 것도 AmmoBoxDisplay와 같습니다.
//
// 다만 표시 방식은 다릅니다. 탄약 박스는 평소 아이콘이 항상 화면에 남아있지만, 이 컴포넌트는
// "장전 중이 아니면 평소 아이콘까지 포함해 통째로 안 보이게" 만듭니다. 장전이 시작되면
// 평소 아이콘 + 반전 아이콘이 함께 나타났다가, 장전이 끝나는 순간 둘 다 함께 사라집니다.
//
// [위치 - 화면 공간(Screen Space), InteractPromptDisplay.cs와 동일 기법]
// 앵커가 상호작용 대상이 아니라 플레이어라는 점만 다르고, 매 프레임 플레이어 위치보다
// indicatorHeight만큼 위(대략 머리 높이)인 지점을 Camera.main.WorldToScreenPoint로 화면
// 좌표로 변환해서 이 컴포넌트 전용 Screen Space - Overlay Canvas 위에 아이콘을 배치하는
// 방식은 InteractPromptDisplay.cs와 완전히 같습니다. 화면 공간 UI라 깊이 테스트를 받지
// 않으므로 벽 등 장애물에 가려지는 문제도 없습니다.
//
// [F 안내(InteractPromptDisplay)와 겹칠 때의 우선순위]
// 보물상자/좌대를 여는 F 안내와 이 장전 표시는 화면상 위치가 가까울 때(플레이어가 상호작용
// 범위 안, 즉 상자 바로 옆에 있을 때) 실제로 겹칠 수 있습니다. 이런 상황은 대부분 전투 중이
// 아니라 아이템을 줍는 상황이라 F 안내가 더 중요하므로, 이 컴포넌트의 canvasSortingOrder
// 기본값(5)을 InteractPromptDisplay의 기본값(10)보다 낮게 둬서 겹칠 때 F 안내가 항상 위에
// 그려지도록 했습니다. 두 값 중 하나를 나중에 바꾸실 경우 이 관계(장전 표시 < F 안내)만
// 유지해주시면 됩니다.
//
// [씬에 배치하는 법]
// InteractPromptDisplay와 마찬가지로 빈 오브젝트(또는 다른 HUD 표시 스크립트들이 있는
// 오브젝트)에 이 컴포넌트만 추가하면 됩니다. PlayerHUDManager의 Canvas와 무관하게 동작하므로
// 그 자식일 필요는 없습니다.
public class PlayerReloadIndicator : MonoBehaviour
{
    [Header("참조 (비워두면 자동으로 찾습니다)")]
    public PlayerShooting playerShooting;
    public PlayerStats playerStats;

    [Header("아이콘")]
    [Tooltip("평소(반전 전) R 아이콘입니다. 장전 중에만, 반전 아이콘과 함께 나타납니다. 비워두면 노란색 단색으로 대체 표시됩니다.")]
    public Sprite reloadNormalIcon;
    [Tooltip("장전 중 평소 아이콘 위에 덮이는 반전된 R 아이콘입니다. 장전 시간 동안 시계방향으로 점점 사라집니다. 비워두면 빨간색 단색으로 대체 표시됩니다.")]
    public Sprite reloadActiveIcon;
    public Color reloadNormalFallbackColor = new Color(0.95f, 0.85f, 0.15f, 1f);
    public Color reloadActiveFallbackColor = new Color(0.9f, 0.2f, 0.2f, 1f);

    [Header("위치 (화면 공간 - 플레이어 머리 오른쪽)")]
    [Tooltip("플레이어 위치보다 이만큼 위(월드 Y축 기준)인 지점을 화면 좌표로 변환해 기준점으로 삼습니다(대략 머리 높이). 인게임에서 보면서 맞춰보세요.")]
    public float indicatorHeight = 2f;
    [Tooltip("기준점에서 아이콘까지의 화면상 오프셋입니다(1920x1080 기준 픽셀). x가 커지면 오른쪽으로, y가 커지면 위쪽으로 이동합니다. 기본값은 오른쪽으로만 이동합니다.")]
    public Vector2 indicatorOffset = new Vector2(55f, 0f);
    [Tooltip("아이콘의 화면상 크기입니다(1920x1080 기준 픽셀).")]
    public float indicatorSize = 56f;

    [Header("화면 해상도 대응 / 정렬 (일반적으로 건드릴 필요 없음)")]
    [Tooltip("PlayerHUDManager와 동일한 기준 해상도입니다.")]
    public Vector2 referenceResolution = new Vector2(1920f, 1080f);
    [Tooltip("F 안내(InteractPromptDisplay, 기본 10)와 겹칠 때 F 안내가 항상 위에 보이도록, 이 값은 " +
             "그보다 낮게 유지하세요. 기본 5.")]
    public int canvasSortingOrder = 5;

    private GameObject indicatorRoot;
    private RectTransform indicatorRootRect;
    private Image normalIconImage;
    private Image activeIconImage;
    private Canvas canvas;
    private RectTransform canvasRect;
    private CanvasScaler canvasScaler;
    private Camera mainCamera;
    private bool built = false;

    // 장전 진행도 계산용입니다. AmmoBoxDisplay.cs와 동일하게, 장전이 시작되는 순간의 시각과
    // 장전 시간을 여기서 직접 기록해 둡니다(PlayerShooting이 남은 시간을 공개하지 않으므로).
    private bool wasReloading = false;
    private float reloadStartTime = 0f;
    private float reloadDuration = 1f;

    // 스프라이트가 비어 있어도 원형 채움(Filled)이 동작하도록 쓰는 흰색 1x1 스프라이트입니다.
    private Texture2D whiteTexture;
    private Sprite whiteSprite;

    void Update()
    {
        if (!EnsureReferences())
        {
            HideIndicator();
            return;
        }

        EnsureBuilt();
        UpdateCanvasScalerMatch();

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        bool isReloading = playerShooting.IsReloading;

        // PlayerShooting.CurrentReloadDuration은 15번 패시브(남은 탄약 적을 때 장전 시간
        // 감소) 같은 보너스가 이미 반영된, 이번 장전에 실제로 걸리는 시간입니다
        // (PlayerStats.ReloadTime은 보너스 반영 전 스탯값이라 그대로 쓰면 진행률이 실제
        // 완료 시점과 어긋납니다 - AmmoBoxDisplay.cs와 동일한 이유).
        if (isReloading && !wasReloading)
        {
            reloadStartTime = Time.time;
            reloadDuration = Mathf.Max(playerShooting.CurrentReloadDuration, 0.01f);
        }
        wasReloading = isReloading;

        if (!isReloading || mainCamera == null)
        {
            HideIndicator();
            return;
        }

        ShowIndicator();
    }

    // PlayerShooting을 먼저 찾고, PlayerStats는 같은 오브젝트에서 가져옵니다
    // (PlayerShooting이 [RequireComponent(typeof(PlayerStats))]라 항상 같이 붙어있습니다).
    private bool EnsureReferences()
    {
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

        var canvasGO = new GameObject("PlayerReloadIndicatorCanvas");
        canvasGO.transform.SetParent(transform, false);
        canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = canvasSortingOrder;

        canvasScaler = canvasGO.AddComponent<CanvasScaler>();
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = referenceResolution;

        canvasRect = canvasGO.GetComponent<RectTransform>();

        indicatorRoot = new GameObject("ReloadIndicator");
        indicatorRoot.transform.SetParent(canvasGO.transform, false);
        indicatorRootRect = indicatorRoot.AddComponent<RectTransform>();
        indicatorRootRect.anchorMin = indicatorRootRect.anchorMax = new Vector2(0.5f, 0.5f);
        indicatorRootRect.pivot = new Vector2(0.5f, 0.5f);
        indicatorRootRect.sizeDelta = Vector2.zero;

        // 평소 아이콘 - 기준점 자리
        var normalGO = new GameObject("ReloadNormalIcon");
        normalGO.transform.SetParent(indicatorRoot.transform, false);
        var normalRect = normalGO.AddComponent<RectTransform>();
        normalRect.anchorMin = normalRect.anchorMax = new Vector2(0.5f, 0.5f);
        normalRect.pivot = new Vector2(0.5f, 0.5f);
        normalIconImage = normalGO.AddComponent<Image>();
        normalIconImage.preserveAspect = true;
        normalIconImage.raycastTarget = false;

        // 반전 아이콘 - 평소 아이콘의 자식으로 같은 영역을 꽉 채워 덮어씌웁니다. 원형(시계) 채움
        // 방식이라 스프라이트가 필요합니다(AmmoBoxDisplay.cs의 ReloadActiveIcon과 동일 설정).
        var activeGO = new GameObject("ReloadActiveIcon");
        activeGO.transform.SetParent(normalGO.transform, false);
        var activeRect = activeGO.AddComponent<RectTransform>();
        activeRect.anchorMin = Vector2.zero;
        activeRect.anchorMax = Vector2.one;
        activeRect.offsetMin = Vector2.zero;
        activeRect.offsetMax = Vector2.zero;
        activeIconImage = activeGO.AddComponent<Image>();
        activeIconImage.preserveAspect = true;
        activeIconImage.raycastTarget = false;
        activeIconImage.type = Image.Type.Filled;
        activeIconImage.fillMethod = Image.FillMethod.Radial360;
        activeIconImage.fillOrigin = (int)Image.Origin360.Top;
        activeIconImage.fillClockwise = false;
        activeIconImage.fillAmount = 1f;

        indicatorRoot.SetActive(false); // 장전 중일 때만 켜집니다.
        built = true;
    }

    private void BuildWhiteSprite()
    {
        whiteTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        whiteTexture.SetPixel(0, 0, Color.white);
        whiteTexture.Apply();
        whiteSprite = Sprite.Create(whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 100f);
    }

    // 화면 비율이 기준 해상도(16:9)와 다를 때 크기가 일정하게 보이도록, "화면에서 더 좁은 쪽"을
    // 기준으로 matchWidthOrHeight를 자동 전환합니다. InteractPromptDisplay.cs /
    // PlayerHUDManager.cs와 동일한 로직입니다.
    private void UpdateCanvasScalerMatch()
    {
        if (canvasScaler == null) return;
        if (referenceResolution.y <= 0f || Screen.height <= 0f) return;

        float screenAspect = (float)Screen.width / Screen.height;
        float referenceAspect = referenceResolution.x / referenceResolution.y;

        canvasScaler.matchWidthOrHeight = screenAspect > referenceAspect ? 1f : 0f;
    }

    private void ShowIndicator()
    {
        Vector3 worldPoint = playerShooting.transform.position + Vector3.up * indicatorHeight;
        Vector3 screenPoint = mainCamera.WorldToScreenPoint(worldPoint);

        // 카메라 뒤(고정 등각 카메라에선 사실상 없는 상황이지만 안전장치로 둡니다)면 숨깁니다.
        if (screenPoint.z < 0f)
        {
            HideIndicator();
            return;
        }

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect, screenPoint, null, out Vector2 localPoint);

        indicatorRoot.SetActive(true);
        indicatorRootRect.anchoredPosition = localPoint;

        var normalRect = (RectTransform)normalIconImage.transform;
        normalRect.anchoredPosition = indicatorOffset;
        normalRect.sizeDelta = new Vector2(indicatorSize, indicatorSize);

        if (reloadNormalIcon != null)
        {
            normalIconImage.sprite = reloadNormalIcon;
            normalIconImage.color = Color.white;
        }
        else
        {
            normalIconImage.sprite = null;
            normalIconImage.color = reloadNormalFallbackColor;
        }

        float elapsed01 = Mathf.Clamp01((Time.time - reloadStartTime) / reloadDuration);

        if (reloadActiveIcon != null)
        {
            activeIconImage.sprite = reloadActiveIcon;
            activeIconImage.color = Color.white;
        }
        else
        {
            activeIconImage.sprite = whiteSprite;
            activeIconImage.color = reloadActiveFallbackColor;
        }

        activeIconImage.fillAmount = 1f - elapsed01;
    }

    private void HideIndicator()
    {
        if (indicatorRoot != null && indicatorRoot.activeSelf)
        {
            indicatorRoot.SetActive(false);
        }
    }
}
