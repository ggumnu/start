using UnityEngine;
using UnityEngine.UI;

// 플레이어가 보는 HUD(체력/스탯/미니맵 등) 전체를 담당하는 최상위 매니저입니다.
// LevelTransitionConfirmUI.cs와 동일한 패턴으로, Canvas/패널 계층을 에디터에서 미리
// 손으로 만들어둘 필요 없이 전부 코드로 자동 생성합니다. 빈 오브젝트 하나에 이 스크립트만
// 붙이면 끝입니다.
//
// [이번 단계에서 만드는 것]
// 1. HUD 전체를 담을 Canvas (다른 UI들도 전부 이 위에 얹힐 예정)
// 2. 화면 왼쪽 위: 캐릭터 초상화 자리 (하늘색 placeholder 사각형 + 스프라이트 교체 기능)
// 3. 초상화 오른쪽에 나중에 체력 하트들이 들어갈 빈 자리(HeartContainer)
//
// [초상화 테두리]
// 초상화 사각형 "바깥쪽"에 단색 테두리를 두릅니다(인스펙터의 초상화 테두리 색 / 두께).
// 초상화 위에 덮어 그리지 않고 바깥으로 두께만큼 튀어나오는 막대 4개(위/아래/왼쪽/오른쪽)로
// 만들기 때문에, 초상화 그림이 가려지지 않고 투명한 부분이 있어도 뒤가 하얗게 비치지 않습니다.
// 막대들은 Portrait의 자식이라 TopLeftGroup의 자동 배치(HorizontalLayoutGroup)에는 영향이
// 없습니다 - 대신 두께가 portraitToHeartSpacing보다 크면 하트 쪽으로 겹쳐 보일 수 있습니다.
// 색/두께는 매 프레임 반영되므로 Play 도중에 바꿔도 바로 보이고, 두께를 0으로 두면 테두리가 꺼집니다.
//
// [v1.16 변경 - 미사용 StatPanelContainer 제거]
// 4단계 스탯 패널을 B안(화면 좌측 하단, 아이템 설명칸과 독립된 위치)으로 정하면서
// PlayerStatDisplay.cs가 CanvasRoot 아래에 자기 컨테이너를 직접 만들어 쓰게 됐습니다.
// 그 결과 예전 A안 시절 여기 만들어뒀던 StatPanelContainer(좌측 상단 근처, Vertical
// Layout Group)는 아무도 참조하지 않는 죽은 코드가 되어 제거했습니다.
//
// 미니맵(오른쪽 위)과 탄약 UI(오른쪽 아래)는 아직 세부 사항이 정해지지 않아서 이번 단계에는
// 포함하지 않았습니다. 나중에 해당 단계에서 이 스크립트에 컨테이너를 추가하면 됩니다.
//
// [화면 종횡비에 따라 여백이 밀리는 문제 - 대응]
// CanvasScaler를 ScaleWithScreenSize로 쓰면, 화면 종횡비가 referenceResolution(16:9)과
// 다를 때 matchWidthOrHeight 배율이 가로/세로 어느 쪽에도 정확히 맞지 않게 되어, 각 HUD가
// 모서리에 맞춰둔 여백(panelMarginX/Y 등)이 설계한 것보다 화면 바깥쪽으로 밀려 보이는
// 현상이 생깁니다(16:9가 아닌 모니터를 쓰는 플레이어라면 누구나 겪습니다). 이를 막기 위해
// 매 프레임 "화면에서 더 좁은 쪽 축"을 기준으로 matchWidthOrHeight를 자동으로 0 또는 1로
// 전환합니다 - UpdateCanvasScalerMatch() 참고. 이 계산은 Canvas를 만드는 이 스크립트에만
// 필요하고, 나머지 HUD 스크립트들(Heart/Stat/ItemInfo/AmmoBox/Minimap)은 전부 이 Canvas
// 밑에서 코너 anchor + 고정 오프셋만 쓰고 있어서 따로 손댈 필요가 없습니다.
public class PlayerHUDManager : MonoBehaviour
{
    [Header("캔버스 해상도 기준 (참고 화면 기준 1920x1080)")]
    [Tooltip("이 해상도를 기준으로 UI 크기가 비율 유지되며 스케일링됩니다.")]
    public Vector2 referenceResolution = new Vector2(1920f, 1080f);

    [Header("초상화 설정")]
    [Tooltip("시작 시 적용할 초상화 스프라이트. 비워두면 참고 화면과 동일한 하늘색 placeholder만 표시됩니다.")]
    public Sprite defaultPortraitSprite;

    [Tooltip("초상화 사각형의 한 변 길이 (px, 기준 해상도 1920x1080 기준)")]
    public float portraitSize = 90f;

    [Header("초상화 테두리 (Play 중에도 즉시 반영됩니다)")]
    [Tooltip("초상화 바깥쪽 테두리 색입니다.")]
    public Color portraitBorderColor = Color.white;

    [Tooltip("초상화 바깥쪽 테두리 두께입니다(기준 해상도 1920x1080 기준). 0이면 테두리를 표시하지 않습니다.")]
    [Min(0f)]
    public float portraitBorderThickness = 4f;

    [Header("좌상단 여백 설정")]
    public float topLeftMarginX = 35f;
    public float topLeftMarginY = 35f;

    [Tooltip("초상화와 그 오른쪽에 올 체력 하트 묶음 사이의 가로 간격")]
    public float portraitToHeartSpacing = 20f;

    [Tooltip("하트 묶음의 상하 위치 조절값입니다. 기본값 0은 초상화 세로 중앙에 맞춘 위치이고, " +
             "값을 양수로 올리면 위로, 음수로 내리면 아래로 이동합니다. Play 도중에 바꿔도 즉시 반영됩니다.")]
    public float heartVerticalOffset = 0f;

    [Header("HUD 접어보기")]
    [Tooltip("이 키를 누르면 접어보기 버튼이 있는 HUD 패널(스탯 / 장신구·일회용·음식 / 패시브 인벤토리)을 " +
             "한꺼번에 접거나 펼칩니다. 하나라도 펼쳐져 있으면 전부 접고, 전부 접혀 있을 때만 전부 펼칩니다. " +
             "None으로 두면 키로는 동작하지 않습니다(각 패널의 버튼은 그대로 동작).")]
    public KeyCode collapseAllKey = KeyCode.Tab;

    private Image portraitImage;
    private RectTransform heartContainer;

    // 초상화 바깥 테두리 막대 4개 (위 / 아래 / 왼쪽 / 오른쪽 순서)
    private readonly Image[] portraitBorderImages = new Image[4];
    private readonly RectTransform[] portraitBorderRects = new RectTransform[4];

    // 다음 단계(체력 하트 UI)에서 이 컨테이너 아래에 하트 아이콘들을 채우면 됩니다.
    public RectTransform HeartContainer => heartContainer;

    // 이후 단계(스탯 패널, 아이템 설명칸, 미니맵, 탄약 UI 등)가 각자 필요한 컨테이너를 직접 만들어
    // 이 캔버스 밑에 붙일 수 있도록 열어둔 진입점입니다. 매번 이 매니저에 새 컨테이너
    // 필드를 추가하지 않아도, 각 UI 스크립트가 스스로 자기 자리를 만들 수 있습니다.
    public RectTransform CanvasRoot => canvasRect;

    // [실시간 초상화] 초상화 사각형(흰 테두리 안쪽)입니다. PlayerPortraitCamera가 이 안을 실시간 얼굴 화면으로 꽉 채웁니다.
    public RectTransform PortraitRect => portraitImage != null ? portraitImage.rectTransform : null;

    void Awake()
    {
        BuildCanvas();
        UpdateCanvasScalerMatch();
        BuildTopLeftGroup();
        ApplyHeartContainerPosition();
        ApplyPortraitBorder();
    }

    // 인스펙터의 heartVerticalOffset을 Play 도중에 바꿔도 바로 보이도록, 그리고 화면
    // 종횡비가 바뀌었을 때(창 크기 조절, 모니터 변경 등) 여백이 바로 다시 맞춰지도록
    // 매 프레임 반영합니다. (둘 다 수치 몇 개만 갱신하는 가벼운 연산이라 성능 부담은 없습니다.)
    void Update()
    {
        UpdateCanvasScalerMatch();
        ApplyHeartContainerPosition();
        ApplyPortraitBorder();

        // HUD 접어보기 - 전체 접기/펼치기 키 (자세한 동작은 CollapsibleHUDPanel.ToggleAll 참고)
        if (collapseAllKey != KeyCode.None && Input.GetKeyDown(collapseAllKey))
        {
            CollapsibleHUDPanel.ToggleAll();
        }
    }

    // 하트 컨테이너는 TopLeftGroup의 HorizontalLayoutGroup 자동 배치 대상에서 제외하고
    // (LayoutElement.ignoreLayout, BuildHeartContainerPlaceholder 참고) 위치를 직접
    // 계산합니다. x는 "초상화 너비 + 간격"으로 고정, y는 초상화 세로 중앙 기준에
    // heartVerticalOffset을 더한 값입니다.
    private void ApplyHeartContainerPosition()
    {
        if (heartContainer == null) return;

        heartContainer.anchoredPosition = new Vector2(
            portraitSize + portraitToHeartSpacing,
            -(portraitSize / 2f) + heartVerticalOffset);
    }

    // 캔버스와 스케일러를 만듭니다. ScaleWithScreenSize로 설정해서 해상도가 달라져도
    // 참고 화면과 비슷한 느낌으로 스케일링되게 했습니다. matchWidthOrHeight는 여기서
    // 고정값을 주지 않고, UpdateCanvasScalerMatch()가 매 프레임 화면 종횡비에 맞춰
    // 자동으로 계산합니다.
    private Canvas rootCanvas;
    private RectTransform canvasRect;
    private CanvasScaler canvasScaler;

    private void BuildCanvas()
    {
        var canvasGO = new GameObject("PlayerHUD_Canvas");
        canvasGO.transform.SetParent(transform, false);

        rootCanvas = canvasGO.AddComponent<Canvas>();
        rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

        canvasScaler = canvasGO.AddComponent<CanvasScaler>();
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = referenceResolution;

        canvasGO.AddComponent<GraphicRaycaster>();

        canvasRect = canvasGO.GetComponent<RectTransform>();
    }

    // 화면 종횡비가 참고 해상도(referenceResolution, 기본 16:9)와 다를 때도 각 모서리에
    // 맞춰둔 여백이 절대 바깥으로 밀려나지 않도록, "화면에서 더 좁은 쪽 축"을 기준으로
    // matchWidthOrHeight를 자동으로 0 또는 1로 전환합니다.
    // - 화면이 참고 비율보다 더 넓으면(울트라와이드 등) → 세로(Height) 기준(1)으로 맞춰서,
    //   위/아래 여백은 항상 정확히 유지되고 남는 가로 공간은 좌/우 그룹 사이로 흡수됩니다.
    // - 화면이 참고 비율보다 더 좁으면(세로형 등) → 가로(Width) 기준(0)으로 맞춰서,
    //   좌/우 여백은 항상 정확히 유지되고 남는 세로 공간은 위/아래 그룹 사이로 흡수됩니다.
    // 해상도 자체(4K vs 1080p 등)에 따른 비례 확대/축소는 지금처럼 ScaleWithScreenSize가
    // 그대로 담당하고, 이 함수는 오직 "종횡비 차이로 인한 여백 밀림"만 막는 역할입니다.
    private void UpdateCanvasScalerMatch()
    {
        if (canvasScaler == null) return;
        if (referenceResolution.y <= 0f || Screen.height <= 0f) return;

        float screenAspect = (float)Screen.width / Screen.height;
        float referenceAspect = referenceResolution.x / referenceResolution.y;

        canvasScaler.matchWidthOrHeight = screenAspect > referenceAspect ? 1f : 0f;
    }

    // 좌상단: 초상화 + (다음 단계에서 채워질) 하트 컨테이너를 가로로 나란히 배치합니다.
    private void BuildTopLeftGroup()
    {
        var groupGO = new GameObject("TopLeftGroup");
        groupGO.transform.SetParent(canvasRect, false);

        var groupRect = groupGO.AddComponent<RectTransform>();
        groupRect.anchorMin = new Vector2(0f, 1f);
        groupRect.anchorMax = new Vector2(0f, 1f);
        groupRect.pivot = new Vector2(0f, 1f);
        groupRect.anchoredPosition = new Vector2(topLeftMarginX, -topLeftMarginY);
        groupRect.sizeDelta = new Vector2(0f, portraitSize);

        var layout = groupGO.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.spacing = portraitToHeartSpacing;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        layout.childControlWidth = false;
        layout.childControlHeight = false;

        var fitter = groupGO.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

        BuildPortrait(groupRect);
        BuildHeartContainerPlaceholder(groupRect);
    }

    // 참고 화면의 하늘색 사각형: Image 컴포넌트 하나로 처리합니다.
    // defaultPortraitSprite가 비어있으면 하늘색 단색으로, 있으면 그 스프라이트로 보여줍니다.
    private void BuildPortrait(Transform parent)
    {
        var portraitGO = new GameObject("Portrait");
        portraitGO.transform.SetParent(parent, false);

        var rect = portraitGO.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(portraitSize, portraitSize);

        portraitImage = portraitGO.AddComponent<Image>();

        if (defaultPortraitSprite != null)
        {
            portraitImage.sprite = defaultPortraitSprite;
            portraitImage.color = Color.white;
        }
        else
        {
            // 참고 화면과 동일한 하늘색 placeholder
            portraitImage.sprite = null;
            portraitImage.color = new Color(0.35f, 0.75f, 0.95f, 1f);
        }

        BuildPortraitBorder(rect);
    }

    // 초상화 바깥 테두리용 막대 4개를 초상화(Portrait)의 자식으로 만듭니다.
    // 실제 위치/두께/색은 매 프레임 ApplyPortraitBorder()에서 정합니다.
    private void BuildPortraitBorder(RectTransform portraitRect)
    {
        string[] names = { "PortraitBorder_Top", "PortraitBorder_Bottom", "PortraitBorder_Left", "PortraitBorder_Right" };

        for (int i = 0; i < 4; i++)
        {
            var go = new GameObject(names[i]);
            go.transform.SetParent(portraitRect, false);

            portraitBorderRects[i] = go.AddComponent<RectTransform>();
            portraitBorderImages[i] = go.AddComponent<Image>();
            portraitBorderImages[i].sprite = null; // 스프라이트 없이 단색으로 그립니다
            portraitBorderImages[i].raycastTarget = false; // HUD가 마우스 입력을 가로채지 않도록
        }
    }

    // 테두리 막대 4개의 위치/두께/색을 인스펙터 값대로 맞춥니다.
    // 막대는 초상화 사각형의 각 변에 앵커를 붙이고 바깥쪽으로 두께만큼 튀어나오게 배치합니다.
    // 위/아래 막대는 좌우로 두께만큼 더 길게 만들어 네 모서리 빈틈을 메웁니다.
    // 앵커 기준이라 초상화 크기가 바뀌어도 테두리가 자동으로 따라갑니다.
    private void ApplyPortraitBorder()
    {
        if (portraitBorderImages[0] == null) return;

        float t = Mathf.Max(0f, portraitBorderThickness);
        bool visible = t > 0f;

        for (int i = 0; i < 4; i++)
        {
            portraitBorderImages[i].enabled = visible;
            portraitBorderImages[i].color = portraitBorderColor;
        }

        if (!visible) return;

        // 위
        SetBorderRect(portraitBorderRects[0], new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(-t, 0f), new Vector2(t, t));
        // 아래
        SetBorderRect(portraitBorderRects[1], new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(-t, -t), new Vector2(t, 0f));
        // 왼쪽
        SetBorderRect(portraitBorderRects[2], new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(-t, 0f), new Vector2(0f, 0f));
        // 오른쪽
        SetBorderRect(portraitBorderRects[3], new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(t, 0f));
    }

    private static void SetBorderRect(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
    }

    // 하트 UI는 다음 단계에서 채울 예정이라, 지금은 자리만 잡아둔 빈 RectTransform입니다.
    // 폭 0으로 시작하지만, 다음 단계 스크립트가 하트를 자식으로 추가하면
    // ContentSizeFitter/HorizontalLayoutGroup 덕분에 자동으로 자리가 늘어납니다.
    //
    // [상하 위치를 인스펙터에서 조절할 수 있게 한 방식]
    // 부모(TopLeftGroup)의 HorizontalLayoutGroup이 자동으로 배치하면, 그 정렬 방식이
    // 위치를 계속 덮어써서 수동으로 위/아래로 옮길 수가 없습니다. 그래서 LayoutElement의
    // ignoreLayout을 켜서 이 컨테이너만 자동 배치 대상에서 빼고, 대신 ApplyHeartContainerPosition()이
    // heartVerticalOffset 값을 반영해 직접 위치를 계산합니다.
    private void BuildHeartContainerPlaceholder(Transform parent)
    {
        var go = new GameObject("HeartContainer");
        go.transform.SetParent(parent, false);

        heartContainer = go.AddComponent<RectTransform>();
        heartContainer.anchorMin = new Vector2(0f, 1f);
        heartContainer.anchorMax = new Vector2(0f, 1f);
        heartContainer.pivot = new Vector2(0f, 0.5f);
        heartContainer.sizeDelta = new Vector2(0f, portraitSize);

        var ignoreLayout = go.AddComponent<LayoutElement>();
        ignoreLayout.ignoreLayout = true;

        var layout = go.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        layout.childControlWidth = false;
        layout.childControlHeight = false;

        var fitter = go.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
    }

    // 나중에 캐릭터 외형 변경 등으로 초상화를 바꿔야 할 일이 생기면 이 함수를 호출하면 됩니다.
    // 지금 당장은 호출하는 곳이 없지만, 코드에서 언제든 바꿀 수 있도록 열어둔 진입점입니다.
    public void SetPortrait(Sprite newPortrait)
    {
        if (portraitImage == null) return;

        if (newPortrait != null)
        {
            portraitImage.sprite = newPortrait;
            portraitImage.color = Color.white;
        }
        else
        {
            portraitImage.sprite = null;
            portraitImage.color = new Color(0.35f, 0.75f, 0.95f, 1f);
        }
    }
}