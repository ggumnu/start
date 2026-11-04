using TMPro;
using UnityEngine;
using UnityEngine.UI;

// F 키로 상호작용할 수 있는 대상(동아리방 좌대/일반맵 보물상자/포탈) 근처에 있을 때, "지금 F를
// 누르면 무슨 일이 일어나는지" 안내를 띄우는 단일 컴포넌트입니다.
//
// [왜 각 오브젝트가 아니라 여기서 한 번에 관리하는가 - v1.35]
// 처음엔 ItemPedestal/TreasureChest가 각자 F 안내(아이콘+텍스트)를 직접 만들었는데, 위치값/
// 스프라이트/글자 크기 등을 상자 종류마다 따로 설정해야 해서 과했습니다. 실제로는 모든
// 상호작용 대상에 완전히 동일한 위치/크기 값을 쓰면 충분하고, 근처에 있을 때 뭐라고 쓸지
// (문구)만 대상 종류/상태에 따라 다르면 됩니다. 그래서 이 컴포넌트 하나가 씬의 모든
// ItemPedestal/TreasureChest/LevelPortal을 매 프레임 스캔해서, 그중 플레이어와 가장 가까운
// "지금 상호작용 가능한" 대상 하나만 골라 그 위치에 안내를 띄웁니다(PlayerItemInfoDisplay가
// 여러 좌대/상자 중 하나를 골라 설명칸을 띄우는 것과 동일한 패턴입니다).
//
// [문구 규칙]
// - ItemPedestal/TreasureChest: 아직 안 열었으면 openLabel("열기"), 열었는데 아직 가져갈 게
//   남아있으면 pickupLabel("획득하기"). 다 가져가서 상자가 사라지기 직전(HasAnythingToPickup
//   == false)이면 그 대상은 후보에서 아예 제외합니다.
// - LevelPortal: 상태 구분 없이 항상 portalLabel("다음 레벨").
//
// [위치 - 화면 공간(Screen Space)로 전환 - v1.36]
// 처음엔 대상 위치에 3D 월드 스페이스(SpriteRenderer + World Space Canvas)로 안내를 그렸는데,
// 이러면 카메라와 안내 사이에 벽 등 장애물이 끼면 깊이 테스트에 걸려 UI가 장애물을 뚫고 나온
// 것처럼 보이는 문제가 있었습니다. 높이(promptHeight)를 올려서 피하면 이번엔 반대로 글자가
// 허공에 떠 있는 것처럼 보이고요.
//
// 그래서 안내가 "그려지는 방식" 자체를 화면 공간으로 바꿨습니다: 대상의 위치(여전히 대상 바로
// 위, promptHeight만큼 위)를 매 프레임 Camera.main.WorldToScreenPoint로 화면 좌표로 변환하고,
// 그 화면 좌표에 이 컴포넌트 전용 Screen Space - Overlay Canvas 위의 UI(아이콘 Image + Text)를
// 배치합니다. 화면 공간 UI는 3D 씬의 깊이 버퍼와 무관하게 항상 맨 위에 따로 그려지므로 벽 뚫림
// 문제 자체가 없어지고, promptHeight를 낮게 유지해도(=대상 바로 위) 전혀 문제가 없습니다.
// 이 방식은 LevelTransitionConfirmUI(포탈 확인창)가 이미 쓰고 있는 화면 공간 UI와 같은
// 방식이라 프로젝트에 새로운 기법을 들이는 것도 아닙니다.
//
// 화면 크기/해상도에 따라 위치·크기가 일정하게 보이도록, CanvasScaler 설정(ScaleWithScreenSize +
// 화면 비율에 따라 matchWidthOrHeight 자동 전환)은 PlayerHUDManager와 동일한 방식을 그대로
// 따랐습니다. 그래서 아래 오프셋/크기 값들의 단위는 "1920x1080 기준 픽셀"이라고 보면 됩니다
// (interactPromptIconOffset 등 - 이전 버전(월드 스페이스 로컬 좌표)과는 단위가 달라졌으니,
// 이 컴포넌트를 이미 씬에 올려두셨다면 아이콘/텍스트 오프셋과 크기 값은 Inspector에서 새로
// 맞춰주셔야 합니다. F 안내 스프라이트(interactPromptSprite) 참조는 필드 이름이 그대로라
// 그대로 유지됩니다).
//
// [씬에 배치하는 법]
// 빈 오브젝트(또는 다른 HUD 표시 스크립트들이 있는 오브젝트)에 이 컴포넌트만 추가하면 됩니다.
// PlayerHUDManager의 Canvas와 무관하게 동작하므로 그 자식일 필요는 없습니다. 카메라는
// Camera.main(Main Camera 태그가 붙은 카메라 - CameraFollow.cs가 올라간 그 카메라)을 씁니다.
public class InteractPromptDisplay : MonoBehaviour
{
    [Header("F 안내 아이콘")]
    public Sprite interactPromptSprite;
    [Tooltip("F 안내 아이콘의 화면상 크기입니다(1920x1080 기준 픽셀).")]
    public Vector2 interactPromptIconSize = new Vector2(56f, 56f);

    [Header("위치 (보물상자/좌대/포탈 전부 동일하게 적용됩니다)")]
    [Tooltip("대상의 위치보다 이만큼 위(월드 Y축 기준)인 지점을 화면 좌표로 변환해 기준점으로 " +
             "삼습니다. 화면 공간 UI라 값을 키워도 '허공에 뜬 것처럼' 보이지 않으니, 대상 바로 " +
             "위(1 안팎)로 두는 걸 추천합니다.")]
    public float promptHeight = 1f;
    [Tooltip("기준점에서 F 아이콘까지의 화면상 오프셋입니다(1920x1080 기준 픽셀). x가 커지면 " +
             "오른쪽으로, y가 작아지면(음수) 아래쪽으로 이동합니다.")]
    public Vector2 interactPromptIconOffset = new Vector2(40f, -15f);
    [Tooltip("F 아이콘과 같은 기준으로 계산되는 텍스트 위치 오프셋입니다.")]
    public Vector2 interactPromptTextOffset = new Vector2(90f, -15f);

    [Header("텍스트")]
    [Tooltip("1920x1080 기준 픽셀 크기입니다.")]
    public int interactPromptFontSize = 28;
    public Color interactPromptTextColor = Color.white;

    [Header("글자 테두리")]
    [Tooltip("F 안내 글자 테두리 두께(픽셀)입니다. 0이면 테두리가 없습니다. 글꼴 에셋 설정에 따라 일정 두께 이상은 " +
             "더 두꺼워지지 않습니다(HUDText 참고).")]
    [Min(0f)] public float interactPromptOutlineThickness = 1.5f;
    [Tooltip("F 안내 글자 테두리 색입니다.")]
    public Color interactPromptOutlineColor = new Color(0f, 0f, 0f, 0.8f);

    [Header("문구")]
    [Tooltip("아직 열지 않은 보물상자/좌대 근처에서 보여줄 문구입니다.")]
    public string openLabel = "열기";
    [Tooltip("이미 열려서 아직 가져갈 게 남아있는 보물상자/좌대 근처에서 보여줄 문구입니다.")]
    public string pickupLabel = "획득하기";
    [Tooltip("포탈 근처에서 보여줄 문구입니다.")]
    public string portalLabel = "다음 레벨";
    [Tooltip("상점 상인(ShopNPC) 근처에서 보여줄 문구입니다.")]
    public string shopLabel = "거래하기";

    [Header("화면 해상도 대응 (일반적으로 건드릴 필요 없음)")]
    [Tooltip("PlayerHUDManager와 동일한 기준 해상도입니다. HUD 전체와 크기 비율을 맞추려면 " +
             "같은 값을 유지하세요.")]
    public Vector2 referenceResolution = new Vector2(1920f, 1080f);
    [Tooltip("다른 HUD 패널 위에 확실히 보이도록 하는 정렬 순서입니다. 다른 화면 공간 Canvas와 " +
             "겹쳐서 안 보이면 이 값을 올려보세요.")]
    public int canvasSortingOrder = 10;

    private GameObject promptRoot;
    private RectTransform promptRootRect;
    private Image promptIconImage;
    private TextMeshProUGUI promptText;
    private Canvas canvas;
    private RectTransform canvasRect;
    private CanvasScaler canvasScaler;
    private Transform playerTransform;
    private Camera mainCamera;
    private bool built = false;

    void Update()
    {
        EnsureBuilt();
        if (!built) return;

        UpdateCanvasScalerMatch();

        if (playerTransform == null)
        {
            var playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) playerTransform = playerObj.transform;
        }

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        if (playerTransform == null || mainCamera == null)
        {
            HidePrompt();
            return;
        }

        var target = FindNearestInteractable(out string label);

        if (target == null)
        {
            HidePrompt();
            return;
        }

        ShowPrompt(target, label);
    }

    private void EnsureBuilt()
    {
        if (built) return;

        var canvasGO = new GameObject("InteractPromptCanvas");
        canvasGO.transform.SetParent(transform, false);
        canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = canvasSortingOrder;

        canvasScaler = canvasGO.AddComponent<CanvasScaler>();
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = referenceResolution;

        canvasRect = canvasGO.GetComponent<RectTransform>();

        promptRoot = new GameObject("InteractPrompt");
        promptRoot.transform.SetParent(canvasGO.transform, false);
        promptRootRect = promptRoot.AddComponent<RectTransform>();
        promptRootRect.anchorMin = promptRootRect.anchorMax = new Vector2(0.5f, 0.5f);
        promptRootRect.pivot = new Vector2(0.5f, 0.5f);
        promptRootRect.sizeDelta = Vector2.zero;

        var promptIconGO = new GameObject("PromptIcon");
        promptIconGO.transform.SetParent(promptRoot.transform, false);
        var iconRect = promptIconGO.AddComponent<RectTransform>();
        iconRect.anchorMin = iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.pivot = new Vector2(0f, 0.5f);
        promptIconImage = promptIconGO.AddComponent<Image>();
        promptIconImage.preserveAspect = true;
        promptIconImage.raycastTarget = false;

        var textGO = new GameObject("PromptText");
        textGO.transform.SetParent(promptRoot.transform, false);
        var textRect = textGO.AddComponent<RectTransform>();
        textRect.anchorMin = textRect.anchorMax = new Vector2(0f, 0.5f);
        textRect.pivot = new Vector2(0f, 0.5f);
        textRect.sizeDelta = new Vector2(240f, 80f);
        // [TMP] 옛 방식 Text는 화면 배율에 따라 흐릿하게 보여서 TextMeshPro로 교체했습니다(HUDText 참고).
        promptText = HUDText.AddTo(textGO, TextAnchor.MiddleLeft);

        // 배경 패널이 없는 화면 공간 글자라 아무 배경 위에서나 읽히도록 검은 외곽선을 둡니다.
        // (TMP 자체 외곽선 - 두께/색은 인스펙터의 "글자 테두리" 값, 기본은 예전 Outline 컴포넌트와 같은 1.5픽셀/검정 80%.
        // 글자 크기나 값이 바뀌면 안내를 띄울 때마다 다시 맞춥니다.)
        promptText.fontSize = interactPromptFontSize;
        HUDText.SetOutline(promptText, interactPromptOutlineThickness, interactPromptOutlineColor);

        promptRoot.SetActive(false); // 상호작용 가능한 대상이 근처에 있을 때만 켜집니다.
        built = true;
    }

    // 화면 비율이 기준 해상도(16:9)와 다를 때 모서리가 잘리거나 여백이 생기지 않도록,
    // "화면에서 더 좁은 쪽"을 기준으로 matchWidthOrHeight를 자동으로 전환합니다.
    // PlayerHUDManager.UpdateCanvasScalerMatch()와 동일한 로직입니다.
    private void UpdateCanvasScalerMatch()
    {
        if (canvasScaler == null) return;
        if (referenceResolution.y <= 0f || Screen.height <= 0f) return;

        float screenAspect = (float)Screen.width / Screen.height;
        float referenceAspect = referenceResolution.x / referenceResolution.y;

        canvasScaler.matchWidthOrHeight = screenAspect > referenceAspect ? 1f : 0f;
    }

    // 씬의 모든 ItemPedestal/TreasureChest/LevelPortal 중, "지금 플레이어가 상호작용 범위
    // 안에 있고 F 안내를 보여줄 이유가 있는" 것들만 후보로 놓고 가장 가까운 것 하나를
    // 고릅니다. 각 후보가 몇 개 안 되므로 매 프레임 전체 탐색해도 부담이 없습니다
    // (PlayerItemInfoDisplay.FindNearestAvailableItem과 동일한 방식).
    private Transform FindNearestInteractable(out string label)
    {
        // [F 대상 공용 판정] 대상 고르기는 InteractionTargetSelector가 맡습니다(실제로 F가 먹는 대상과 항상 같음 -
        // 범위 안 + 상호작용할 것이 남은 대상 중 바닥 평면 거리로 가장 가까운 하나). 여기서는 문구만 정합니다.
        label = null;
        var target = InteractionTargetSelector.Current;
        if (target == null) return null;

        if (target is ItemPedestal pedestal)
        {
            label = !pedestal.IsOpened ? openLabel : pickupLabel;
        }
        else if (target is TreasureChest chest)
        {
            label = !chest.IsOpened ? openLabel : pickupLabel;
        }
        else if (target is LevelPortal)
        {
            label = portalLabel;
        }
        else if (target is ShopNPC)
        {
            label = shopLabel;
        }
        else if (target is HoldingPedestal)
        {
            label = pickupLabel; // 보관 좌대는 열기 단계 없이 바로 "획득하기"
        }
        else
        {
            return null;
        }

        return target.transform;
    }

    private void ShowPrompt(Transform target, string label)
    {
        Vector3 worldPoint = target.position + Vector3.up * promptHeight;
        Vector3 screenPoint = mainCamera.WorldToScreenPoint(worldPoint);

        // 카메라 뒤(고정 등각 카메라에선 사실상 없는 상황이지만 안전장치로 둡니다)면 숨깁니다.
        if (screenPoint.z < 0f)
        {
            HidePrompt();
            return;
        }

        // 화면 좌표를 캔버스(Screen Space - Overlay) 로컬 좌표로 변환합니다. Overlay 모드라
        // 카메라 인자는 null입니다. 캔버스 피벗이 (0.5, 0.5)라 결과값은 promptRootRect의
        // anchoredPosition(피벗도 0.5, 0.5)에 바로 대입할 수 있습니다.
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect, screenPoint, null, out Vector2 localPoint);

        promptRoot.SetActive(true);
        promptRootRect.anchoredPosition = localPoint;

        promptIconImage.sprite = interactPromptSprite;
        var iconRect = (RectTransform)promptIconImage.transform;
        iconRect.anchoredPosition = interactPromptIconOffset;
        iconRect.sizeDelta = interactPromptIconSize;

        var textRect = (RectTransform)promptText.transform;
        textRect.anchoredPosition = interactPromptTextOffset;

        promptText.text = label;
        promptText.fontSize = interactPromptFontSize;
        promptText.color = interactPromptTextColor;
        HUDText.SetOutline(promptText, interactPromptOutlineThickness, interactPromptOutlineColor); // 글자 크기/인스펙터 값 반영
    }

    private void HidePrompt()
    {
        if (promptRoot != null && promptRoot.activeSelf)
        {
            promptRoot.SetActive(false);
        }
    }
}
