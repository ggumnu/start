using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 화면 우측 상단에 표시되는 미니맵입니다. LevelBuilder가 생성한 방 그래프(RoomInstance +
// AnchorLink)를 그대로 읽어서, 각 방을 아이콘 하나로, 방과 방 사이의 연결을 선 하나로
// 그립니다.
//
// [기획 - B안: 격자만 회전, 아이콘은 세워두기]
// 카메라가 항상 고정된 45도 대각선 각도(CameraFollow.fixedEulerAngles)로 내려다보기
// 때문에, 실제 방 배치(북/동/남/서 격자)를 미니맵에 그대로(회전 없이) 옮기면 실제
// 화면에서 느껴지는 방향감과 어긋나 보입니다. 그래서 "격자 좌표를 회전시켜 배치하되,
// 그 위에 올라가는 아이콘 하나하나는 회전시키지 않고 항상 똑바로 세워두는" 방식(B안)을
// 씁니다.
//
// [v1.17 변경 - 방 아이콘 배경 + 오버레이 아이콘 2단 구조]
// 방 아이콘은 두 겹으로 이뤄집니다.
//   1) 배경(마름모): 기존 그대로 카테고리별 3종 스프라이트(현재/방문함/미방문)를 그대로
//      씁니다 - "코드로 밝기/테두리를 입히지 않고 실제로 다른 스프라이트를 쓴다"는
//      원칙은 그대로입니다.
//   2) 오버레이: 배경 위에 상태별로 다른 내용이 하나 더 얹힙니다.
//      - 현재 방: 방 종류/아이템 유무와 무관하게 "플레이어 마커" 아이콘 단 하나만 정중앙에.
//      - 방문한 방: 그 방에 아직 남아있는 아이템/보상 아이콘을 최대 4개까지 상->좌->우->하
//        순서로 채웁니다 (markerIcons 리스트 순서가 곧 우선순위 - 5개 이상 남아있어도
//        앞쪽 4개만 표시).
//      - 미탐색 방: variantId에 맞는 아이콘이 있으면 그걸, 없으면 카테고리 기본 아이콘
//        (categoryOverlayIcon)을, 그것도 없으면 아무 것도 표시하지 않습니다(예: 일반방).
//
// [방문 표시 예외 - 형빈 확정]
// 배경/오버레이를 고를 때 RoomInstance.visited를 그대로 쓰지 않고 ShowAsVisited()로 한 번 걸러서 씁니다
// (visited 값 자체는 바꾸지 않음 - 22번 아이템 등 다른 곳이 그대로 씀).
// - 전투방(일반방/보스방)을 클리어하지 않고 벗어나면(일회용 아이템 탈출 등) 미방문으로 표시합니다 - 미방문 배경 +
//   방 종류 아이콘(보스방이면 보스 아이콘). 클리어하면 그때부터 방문으로 표시됩니다.
// - keepIconAfterVisitCategories에 든 카테고리(기본: 상점/특수방)와 보스방은 방문한 뒤에도 방 종류 아이콘을 정중앙에 계속 보여줍니다
//   (배경은 방문 표시, 남은 아이템 4격자도 함께 표시).
//
// [마커 스캔 방식 - 확장성]
// "방문한 방에 무엇이 남아있는지"는 그 방의 Content 루트 밑을 스캔해서 IMinimapMarker를
// 구현한 컴포넌트를 찾는 방식입니다(예: ItemPedestal이 "Item" 키로 구현). 나중에 돈/
// 청휘석/추가 체력 등이 추가돼도, 그 오브젝트가 IMinimapMarker만 구현하면 이 스크립트는
// 전혀 수정할 필요 없이 markerIcons 리스트에 새 키/아이콘만 등록하면 됩니다.
//
// [의존하는 다른 스크립트의 변경 사항]
// v1.16: RoomData.cs(RoomInstance.visited), RoomController.cs(EnterRoom에서 visited=true),
//        LevelBuilder.cs(GeneratedRooms/CurrentPlayerCell/HasPlayerCell/LevelBuildVersion).
// v1.17: RoomController.cs(ContentRoot 접근자), LevelBuilder.cs(GetRoomController(id)),
//        IMinimapMarker.cs(신규 인터페이스), ItemPedestal.cs(IMinimapMarker 구현).
// 전부 읽기 전용 접근자/작은 인터페이스 구현만 추가한 것이라 기존 로직은 바뀌지 않았습니다.
//
// [자동 정렬]
// 레벨마다 방 배치(가지 뻗는 방향)가 매번 랜덤이라, 매 프레임 회전된 좌표들의 우측/상단
// 끝을 찾아 그 지점이 패널의 우측 상단 모서리에 오도록 자동으로 밀어줍니다.
//
// [남아있는 아이템 표시 확장 - 상자 상태별 아이콘 + 포탈]
// 방문한 방의 4격자에 뜨는 아이콘 키는 이제 다음과 같습니다: Item(아이템), Chest(아직 안 연 상자),
// Health/Grenade/HackingTool/Credit/Pyroxenes/Cost(상자에 남은 기본 아이템 종류별). 상자는 IMinimapMultiMarker로
// 자신의 상태(안 열림/열림 + 안 가져간 것)에 맞는 키들을 알려줍니다. 포탈(Portal 키)은 미니맵에 표시하지 않습니다
// (형빈 확정 - 보스 클리어 뒤 보스방은 포탈 아이콘 대신 항상 보스방 아이콘, 남은 아이템/상자 4격자는 다른 방처럼 표시).
// 위 키들은 Marker Icons 리스트에 아직 없으면 자동으로 채워집니다(OnValidate) - 아이콘 스프라이트만
// 각 항목에 끌어다 놓으면 됩니다.
//
// [미니맵 구역 - 확대/축소/드래그 (형빈 확정)]
// 맵이 넓어지면서 미니맵이 아래(인벤토리)와 왼쪽(게임 화면)으로 계속 자라던 문제를 막기 위해, 우상단에
// 고정 크기의 "미니맵 구역"(반투명 검정 둥근 사각형, zoneWidth x zoneHeight)을 두고 미니맵을 그 안에서만
// 보여줍니다. 구역 배경 Image에 Mask를 붙여 둥근 모양 그대로 잘라내므로 모서리에서도 삐져나오지 않습니다.
// - 방/연결선은 구역 안의 "지도 판"(MapContent) 위에 그립니다. 예전의 "가장 오른쪽 위 방을 우상단 모서리에
//   붙이는 자동 정렬"은 없애고, 지도 판 기준 좌표(격자 회전 좌표 + mapOriginOffset)에 그대로 놓습니다.
// - 확대/축소: 구역 위에서 마우스 휠. 지도 판 전체 배율(zoom)을 바꾸므로 아이콘/선/간격이 함께 커지고
//   작아집니다. 커서가 가리키던 지점이 커서 아래에 고정된 채로 확대/축소됩니다(지도 앱 방식).
// - 이동: 구역 안에서 좌클릭으로 시작한 드래그. 드래그 중 커서가 구역 밖으로 나가도 버튼을 뗄 때까지 따라옵니다.
// - 이동 한계: 구역 가운데에 보이는 지점(viewCenter)이 항상 지도 범위(방 아이콘들의 외곽) 안에 있도록 제한 -
//   지도가 항상 구역에 걸쳐 있어서 너무 멀리 끌어 지도를 잃어버리는 일이 없습니다.
// - 현재 방 따라가기: 플레이어가 다른 방으로 옮길 때마다 현재 방이 구역 가운데 오도록 부드럽게 이동하고
//   (resetZoomOnRoomChange가 켜져 있으면 배율도 동시에 Initial Zoom으로 부드럽게 돌아감),
//   그 사이에는 드래그로 자유롭게 둘러볼 수 있습니다. 새 레벨이 지어지면 현재 방 기준으로 바로 맞춥니다.
// - "현재 위치" 버튼(구역 오른쪽 아래, 흰 둥근 사각형 + 검은 글씨): 누르면 방을 옮길 때와 같이 현재 방이
//   구역 가운데로 부드럽게 이동하고(Follow Smooth Speed) 배율도 Initial Zoom으로 부드럽게 돌아갑니다(Zoom Smooth Speed). 버튼 위에서 누른 클릭은 드래그를 시작하지 않습니다.
// - 구역 위에서 누른 좌클릭은 사격/투척/전개로 새지 않도록 HUDClickBlocker가 IsPointerOverZone()을 확인합니다.
// - 입력은 다른 HUD와 같이 EventSystem 없이 마우스 좌표를 직접 비교하고, 실제 시간 기준이라 게임이 멈춰도 동작합니다.
public class MinimapDisplay : MonoBehaviour
{
    // [v1.18 변경 - 배경 아이콘을 카테고리 무관 공용 3종으로 축소]
    // 방 아이콘(마름모 배경)은 "플레이어가 있는지/가 본 적 있는지/못 가봤는지" 딱 세 가지
    // 상태만 구분하면 되고, 그 방이 무슨 카테고리인지는 배경이 아니라 오버레이(아래
    // CategoryOverlayEntry/VariantIconEntry/MarkerIconEntry)가 알려줍니다. 카테고리별로
    // 배경을 따로 만들면 9종 x 3상태 = 27장이 필요해지므로, 배경은 상태 3장만 공용으로
    // 쓰고 카테고리 구분은 전부 오버레이 레이어로 옮겼습니다.
    [System.Serializable]
    public class CategoryOverlayEntry
    {
        public RoomCategory category;
        [Tooltip("이 카테고리에 속한 방이 미탐색 상태일 때, variantId 전용 아이콘이 없으면 " +
                 "대신 보여줄 오버레이 아이콘입니다. 비워두면(예: 일반방) 미탐색 상태에서 " +
                 "배경만 보이고 오버레이 아이콘은 아예 뜨지 않습니다.")]
        public Sprite icon;
    }

    [System.Serializable]
    public class VariantIconEntry
    {
        [Tooltip("RoomInstance.variantId와 정확히 일치해야 합니다 (예: convenience_store, cafe).")]
        public string variantId;
        [Tooltip("이 variant를 가진 방이 미탐색 상태일 때 정중앙에 표시할 아이콘입니다.")]
        public Sprite icon;
    }

    [System.Serializable]
    public class MarkerIconEntry
    {
        [Tooltip("IMinimapMarker.MinimapIconKey와 정확히 일치해야 합니다 (예: Item, Money, Gem, HeartUp).")]
        public string markerKey;
        public Sprite icon;
        [Tooltip("icon을 아직 안 만들었을 때 대신 보여줄 사각형 색상입니다.")]
        public Color fallbackColor = Color.white;
    }

    [Header("참조 (비워두면 씬에서 자동으로 찾습니다)")]
    public PlayerHUDManager hudManager;
    public LevelBuilder levelBuilder;

    [Header("배경 아이콘 (카테고리 무관 공용 3종 - 현재 / 방문함 / 미방문)")]
    public Sprite currentBackgroundIcon;
    public Sprite visitedBackgroundIcon;
    public Sprite unvisitedBackgroundIcon;
    [Tooltip("위 배경 스프라이트를 아직 안 만들었을 때 상태별로 대신 보여줄 색상입니다 (임시 표시).")]
    public Color currentFallbackColor = new Color(0.3f, 0.95f, 0.4f);
    public Color visitedFallbackColor = new Color(0.85f, 0.85f, 0.85f);
    public Color unvisitedFallbackColor = new Color(0.35f, 0.35f, 0.35f);

    [Header("미탐색 방 - 카테고리별 기본 오버레이 아이콘 (variantId 전용 아이콘이 없을 때 사용)")]
    public List<CategoryOverlayEntry> categoryOverlayIcons = new List<CategoryOverlayEntry>
    {
        new CategoryOverlayEntry { category = RoomCategory.Start },
        new CategoryOverlayEntry { category = RoomCategory.Boss },
        new CategoryOverlayEntry { category = RoomCategory.Club },
        new CategoryOverlayEntry { category = RoomCategory.Store },
        new CategoryOverlayEntry { category = RoomCategory.Special },
        new CategoryOverlayEntry { category = RoomCategory.Normal },
        new CategoryOverlayEntry { category = RoomCategory.Choice },
    };

    [Header("방문 후에도 방 종류 아이콘을 유지할 카테고리")]
    [Tooltip("여기에 넣은 카테고리의 방은 방문한 뒤에도 미탐색 때와 같은 방 종류 아이콘(variant 아이콘 또는 카테고리 아이콘)을 " +
             "정중앙에 계속 표시합니다 - 다시 찾아가야 하는 방(상점 등)을 미니맵에서 알아볼 수 있도록. 배경은 방문 표시가 됩니다.")]
    public List<RoomCategory> keepIconAfterVisitCategories = new List<RoomCategory> { RoomCategory.Store, RoomCategory.Special };

    [Header("미탐색 방 - variantId 전용 오버레이 아이콘 (카테고리 기본보다 우선)")]
    public List<VariantIconEntry> variantIcons = new List<VariantIconEntry>();

    [Header("플레이어 마커 (현재 방 오버레이, 카테고리 무관 공용 아이콘 하나)")]
    public Sprite playerMarkerIcon;
    public Color playerMarkerFallbackColor = Color.white;

    [Header("방문한 방 - 남아있는 아이템/보상 오버레이 (리스트 순서 = 우선순위, 최대 4개 표시)")]
    public List<MarkerIconEntry> markerIcons = new List<MarkerIconEntry>
    {
        new MarkerIconEntry { markerKey = MinimapKeys.Item },
        new MarkerIconEntry { markerKey = MinimapKeys.Chest, fallbackColor = new Color(0.65f, 0.42f, 0.2f) },
        new MarkerIconEntry { markerKey = "Health", fallbackColor = new Color(1f, 0.3f, 0.3f) },
        new MarkerIconEntry { markerKey = "Grenade", fallbackColor = new Color(1f, 0.6f, 0.1f) },
        new MarkerIconEntry { markerKey = "HackingTool", fallbackColor = new Color(0.3f, 0.9f, 1f) },
        new MarkerIconEntry { markerKey = "Credit", fallbackColor = new Color(1f, 0.9f, 0.3f) },
        new MarkerIconEntry { markerKey = "Pyroxenes", fallbackColor = new Color(0.6f, 0.6f, 1f) },
        new MarkerIconEntry { markerKey = "Cost", fallbackColor = new Color(0.4f, 1f, 0.5f) },
    };

    [Header("패널 위치 (화면 우측 상단 모서리 기준 여백, px)")]
    public float panelMarginX = 40f;
    public float panelMarginY = 40f;

    [Header("미니맵 구역 (이 사각형 밖으로는 미니맵이 보이지 않음)")]
    [Tooltip("미니맵 구역의 가로 크기입니다(1920x1080 기준 px).")]
    public float zoneWidth = 380f;
    [Tooltip("미니맵 구역의 세로 크기입니다(1920x1080 기준 px).")]
    public float zoneHeight = 340f;
    [Tooltip("구역 배경 색입니다. 알파(A)로 투명도를 조절합니다. 알파를 0으로 만들면 잘라내기(Mask)가 동작하지 " +
             "않을 수 있으니 아주 작은 값이라도 남겨두세요.")]
    public Color zoneColor = new Color(0f, 0f, 0f, 0.5f);
    [Tooltip("구역 모서리 둥글기(px)입니다. 0이면 각진 사각형입니다.")]
    [Min(0f)] public float zoneCornerRadius = 16f;

    [Header("미니맵 확대/축소 (구역 위에서 마우스 휠)")]
    [Tooltip("게임 시작 시 배율입니다(1 = 지금까지의 크기).")]
    public float initialZoom = 1f;
    [Tooltip("최소 배율(가장 작게 축소)입니다.")]
    public float minZoom = 0.4f;
    [Tooltip("최대 배율(가장 크게 확대)입니다.")]
    public float maxZoom = 2f;
    [Tooltip("휠 한 칸당 배율 변화량입니다(0.15 = 한 칸에 15%씩 커지거나 작아짐).")]
    public float zoomStepPerNotch = 0.15f;
    [Tooltip("배율이 목표값으로 따라가는 속도입니다. 클수록 빠르고, 0이면 즉시 바뀝니다.")]
    [Min(0f)] public float zoomSmoothSpeed = 14f;

    [Header("현재 방 따라가기")]
    [Tooltip("체크하면 플레이어가 다른 방으로 옮길 때마다 현재 방이 구역 가운데 오도록 이동합니다.")]
    public bool followCurrentRoom = true;
    [Tooltip("현재 방으로 이동하는 속도입니다. 클수록 빠르고, 0이면 즉시 이동합니다.")]
    [Min(0f)] public float followSmoothSpeed = 8f;
    [Tooltip("체크하면 다른 방으로 옮길 때 배율도 Initial Zoom(기본 배율)으로 부드럽게 돌아갑니다 " +
             "(돌아가는 속도는 Zoom Smooth Speed).")]
    public bool resetZoomOnRoomChange = true;

    [Header("현재 위치 버튼 (구역 오른쪽 아래 - 누르면 현재 방으로 부드럽게 이동 + 기본 배율)")]
    public bool showRecenterButton = true;
    public string recenterButtonText = "현재 위치";
    [Tooltip("버튼 크기(px)입니다.")]
    public Vector2 recenterButtonSize = new Vector2(96f, 34f);
    [Tooltip("구역 오른쪽 아래 모서리에서 버튼까지의 여백(x = 오른쪽, y = 아래)입니다.")]
    public Vector2 recenterButtonMargin = new Vector2(10f, 10f);
    [Min(0f)] public float recenterButtonCornerRadius = 8f;
    public Color recenterButtonColor = Color.white;
    [Tooltip("마우스를 올렸을 때의 버튼 색입니다.")]
    public Color recenterButtonHoverColor = new Color(0.82f, 0.82f, 0.82f, 1f);
    public Color recenterButtonTextColor = Color.black;
    public int recenterButtonFontSize = 18;

    [Header("격자 변환 (B안 - 격자만 회전, 아이콘은 세워둠)")]
    [Tooltip("카메라의 고정 각도(CameraFollow.fixedEulerAngles, 기본 Y축 45도)에 맞춰 " +
             "격자 좌표를 회전시키는 각도입니다. 방향이 반대로 보이면 부호를 뒤집어보세요.")]
    public float gridRotationDegrees = 45f;
    [Tooltip("격자 한 칸(방 하나)이 미니맵에서 차지하는 픽셀 거리입니다.")]
    public float cellPixelSize = 34f;
    [Tooltip("자동 정렬 위에 추가로 얹는 미세 조정 오프셋입니다. 보통 0,0으로 둬도 됩니다.")]
    public Vector2 mapOriginOffset = Vector2.zero;

    [Header("배경 아이콘 크기")]
    public float iconSize = 26f;

    [Header("오버레이 아이콘 크기/간격")]
    [Tooltip("플레이어 마커 / 미탐색 variant 아이콘처럼 정중앙에 단독으로 뜨는 아이콘의 크기입니다.")]
    public float centerOverlaySize = 16f;
    [Tooltip("방문한 방의 4격자 아이템 아이콘 하나하나의 크기입니다.")]
    public float quadrantIconSize = 12f;
    [Tooltip("방 아이콘 정중앙에서 상/좌/우/하 아이템 아이콘까지의 거리입니다.")]
    public float quadrantOffset = 8f;

    [Header("연결선")]
    public bool showConnections = true;
    public float lineThickness = 3f;
    public Color lineColor = new Color(1f, 1f, 1f, 0.5f);

    // 4격자 배치 순서 - 기획대로 상->좌->우->하 순서로 채웁니다.
    private static readonly Vector2[] QuadrantDirections =
    {
        new Vector2(0f, 1f),  // 상
        new Vector2(-1f, 0f), // 좌
        new Vector2(1f, 0f),  // 우
        new Vector2(0f, -1f), // 하
    };

    private class RoomIconRefs
    {
        public RoomInstance room;
        public RectTransform rect;
        public Image backgroundImage;
        public RectTransform centerOverlayRect;
        public Image centerOverlayImage;
        public RectTransform[] quadrantRects = new RectTransform[4];
        public Image[] quadrantImages = new Image[4];
    }

    private class LineRefs
    {
        public RectTransform rect;
        public Image image;
        public Vector2Int coordA;
        public Vector2Int coordB;
    }

    private GameObject panelRoot;
    private RectTransform panelRect;
    private RectTransform linesContainer;
    private RectTransform iconsContainer;
    private readonly List<RoomIconRefs> roomIcons = new List<RoomIconRefs>();
    private readonly List<LineRefs> lines = new List<LineRefs>();
    private readonly List<Sprite> scratchMarkerSprites = new List<Sprite>();
    private readonly List<Color> scratchMarkerColors = new List<Color>();
    private readonly HashSet<string> scratchFoundKeys = new HashSet<string>();
    private readonly List<string> scratchKeyList = new List<string>();
    private bool built = false;
    private int cachedVersion = -1;

    // 미니맵 구역 / 지도 판
    private Image zoneImage;
    private RectTransform mapContent;
    private Sprite zoneRoundedSprite;
    private Texture2D zoneRoundedTexture;
    private const int ZoneTexSize = 96;
    private const int ZoneTexRadius = 32;

    // 보기 상태: 구역 가운데에 보이는 지도 판 좌표(viewCenter)와 배율(zoom)
    private Vector2 viewCenter = Vector2.zero;
    private float zoom = 1f;
    private float targetZoom = 1f;
    private bool zoomInitialized = false;
    // 휠 확대/축소 기준점: 구역 가운데 기준 커서 위치(zoneLocal)와 그 아래 지도 판 좌표(content)
    private bool zoomAnchorActive = false;
    private Vector2 zoomAnchorZoneLocal;
    private Vector2 zoomAnchorContent;
    // 드래그
    private bool dragging = false;
    private Vector2 lastDragZoneLocal;
    // 현재 방 따라가기
    private bool hasFollowTarget = false;
    private Vector2 followTarget;
    private bool hasLastPlayerCell = false;
    private Vector2Int lastPlayerCell;
    private bool snapOnNextCell = true; // 새 레벨: 현재 방이 정해지는 즉시 이동 애니메이션 없이 바로 맞춤
    // 지도 범위(방 아이콘 외곽, 지도 판 좌표) - 이동 한계 계산용
    private bool hasMapBounds = false;
    private Vector2 mapBoundsMin;
    private Vector2 mapBoundsMax;

    // HUDClickBlocker가 "지금 마우스가 미니맵 구역 위인가"를 물어볼 때 쓰는 현재 인스턴스
    private static MinimapDisplay activeInstance;

    // 현재 위치 버튼
    private RectTransform recenterButtonRect;
    private Image recenterButtonImage;
    private TextMeshProUGUI recenterButtonLabel;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        activeInstance = null;
    }

    // 지금 마우스가 미니맵 구역 위에 있는지(구역 위 좌클릭이 사격으로 새지 않도록 HUDClickBlocker가 사용).
    public static bool IsPointerOverZone()
    {
        return activeInstance != null && activeInstance.IsPointerOverZoneInternal();
    }

    void OnEnable()
    {
        activeInstance = this;
    }

    void OnDisable()
    {
        if (activeInstance == this) activeInstance = null;
        dragging = false;
    }

    void OnDestroy()
    {
        if (zoneRoundedSprite != null) Destroy(zoneRoundedSprite);
        if (zoneRoundedTexture != null) Destroy(zoneRoundedTexture);
    }

    // 이미 만들어져 있는 MinimapDisplay 컴포넌트의 Marker Icons 리스트에는 새로 생긴 키가 없으므로,
    // 스크립트가 다시 로드될 때마다 빠진 키를 리스트 끝에 자동으로 추가합니다(이미 있는 항목/순서/
    // 스프라이트는 절대 건드리지 않습니다). 키를 일부러 지워도 다시 채워지니, 안 쓰는 키는
    // 지우지 말고 스프라이트만 비워두세요.
    void OnValidate()
    {
        if (markerIcons == null) markerIcons = new List<MarkerIconEntry>();

        EnsureMarkerEntry(MinimapKeys.Item, Color.white);
        EnsureMarkerEntry(MinimapKeys.Chest, new Color(0.65f, 0.42f, 0.2f));
        EnsureMarkerEntry("Health", new Color(1f, 0.3f, 0.3f));
        EnsureMarkerEntry("Grenade", new Color(1f, 0.6f, 0.1f));
        EnsureMarkerEntry("HackingTool", new Color(0.3f, 0.9f, 1f));
        EnsureMarkerEntry("Credit", new Color(1f, 0.9f, 0.3f));
        EnsureMarkerEntry("Pyroxenes", new Color(0.6f, 0.6f, 1f));
        EnsureMarkerEntry("Cost", new Color(0.4f, 1f, 0.5f));
    }

    private void EnsureMarkerEntry(string key, Color fallbackColor)
    {
        foreach (var entry in markerIcons)
        {
            if (entry != null && entry.markerKey == key) return;
        }
        markerIcons.Add(new MarkerIconEntry { markerKey = key, fallbackColor = fallbackColor });
    }

    void Update()
    {
        if (!EnsureReferences()) return;

        EnsureBuilt();
        RebuildIfVersionChanged();
        ApplyZoneLayout();
        ApplyRecenterButtonLayout();
        Reflow();
        UpdateView();
    }

    private bool EnsureReferences()
    {
        if (hudManager == null)
        {
            hudManager = Object.FindFirstObjectByType<PlayerHUDManager>();
            if (hudManager == null || hudManager.CanvasRoot == null) return false;
        }

        if (levelBuilder == null)
        {
            levelBuilder = Object.FindFirstObjectByType<LevelBuilder>();
            if (levelBuilder == null) return false;
        }

        return true;
    }

    // 패널/아이콘 컨테이너/선 컨테이너는 한 번만 만듭니다. 선 컨테이너를 아이콘
    // 컨테이너보다 먼저(=계층에서 더 위쪽 형제로) 만들어서, 선이 항상 아이콘 뒤에
    // 그려지도록 합니다.
    private void EnsureBuilt()
    {
        if (built) return;

        panelRoot = new GameObject("MinimapDisplay_Panel");
        panelRoot.transform.SetParent(hudManager.CanvasRoot, false);

        // 패널 = 미니맵 구역. 배경 Image(둥근 사각형) + Mask로, 자식(지도 판)은 이 모양 밖으로 보이지 않습니다.
        panelRect = panelRoot.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1f, 1f);
        panelRect.anchorMax = new Vector2(1f, 1f);
        panelRect.pivot = new Vector2(1f, 1f);
        panelRect.sizeDelta = new Vector2(zoneWidth, zoneHeight);

        zoneImage = panelRoot.AddComponent<Image>();
        zoneImage.raycastTarget = false; // 입력은 좌표 비교로 직접 판정 - UI 입력을 가로채지 않음
        var mask = panelRoot.AddComponent<Mask>();
        mask.showMaskGraphic = true; // 배경(반투명 검정)도 보이게

        // 지도 판: 구역 가운데 기준. 배율(localScale)과 위치(anchoredPosition)로 확대/축소/이동합니다.
        var contentGO = new GameObject("MapContent");
        contentGO.transform.SetParent(panelRect, false);
        mapContent = contentGO.AddComponent<RectTransform>();
        mapContent.anchorMin = new Vector2(0.5f, 0.5f);
        mapContent.anchorMax = new Vector2(0.5f, 0.5f);
        mapContent.pivot = new Vector2(0.5f, 0.5f);
        mapContent.sizeDelta = Vector2.zero;

        var linesGO = new GameObject("Lines");
        linesGO.transform.SetParent(mapContent, false);
        linesContainer = linesGO.AddComponent<RectTransform>();
        linesContainer.anchorMin = new Vector2(0.5f, 0.5f);
        linesContainer.anchorMax = new Vector2(0.5f, 0.5f);
        linesContainer.pivot = new Vector2(0.5f, 0.5f);

        var iconsGO = new GameObject("Icons");
        iconsGO.transform.SetParent(mapContent, false);
        iconsContainer = iconsGO.AddComponent<RectTransform>();
        iconsContainer.anchorMin = new Vector2(0.5f, 0.5f);
        iconsContainer.anchorMax = new Vector2(0.5f, 0.5f);
        iconsContainer.pivot = new Vector2(0.5f, 0.5f);

        BuildRecenterButton();

        built = true;
    }

    // LevelBuilder가 레벨을 새로 지을 때마다(최초 빌드 + n.5 선택방 전환 등) 방 목록
    // 자체가 통째로 새 리스트로 교체되므로, LevelBuildVersion이 바뀌면 아이콘/선을
    // 전부 지우고 새 방 목록 기준으로 다시 만듭니다.
    private void RebuildIfVersionChanged()
    {
        if (levelBuilder.LevelBuildVersion == cachedVersion) return;
        cachedVersion = levelBuilder.LevelBuildVersion;

        // [미니맵 구역] 새 레벨: 현재 방이 정해지는 즉시 그 방이 구역 가운데 오도록 바로 맞춥니다(배율은 유지).
        snapOnNextCell = true;
        hasFollowTarget = false;
        zoomAnchorActive = false;
        dragging = false;

        foreach (var icon in roomIcons) Destroy(icon.rect.gameObject);
        roomIcons.Clear();

        foreach (var line in lines) Destroy(line.rect.gameObject);
        lines.Clear();

        var rooms = levelBuilder.GeneratedRooms;
        foreach (var room in rooms)
        {
            roomIcons.Add(CreateIcon(room));
        }

        // LevelGraphDebugger.cs와 동일한 방식으로 같은 연결(AnchorLink)이 두 번
        // 그려지지 않도록 중복 제거합니다.
        var drawnLinks = new HashSet<AnchorLink>();
        foreach (var room in rooms)
        {
            foreach (var link in room.anchors)
            {
                if (drawnLinks.Contains(link)) continue;
                drawnLinks.Add(link);
                lines.Add(CreateLine(link.cellA, link.cellB));
            }
        }
    }

    private RoomIconRefs CreateIcon(RoomInstance room)
    {
        var go = new GameObject("RoomIcon_" + room.id);
        go.transform.SetParent(iconsContainer, false);

        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.localRotation = Quaternion.identity; // 격자는 회전해도 아이콘 자체는 항상 정면(B안)

        var backgroundImage = go.AddComponent<Image>();
        backgroundImage.preserveAspect = true;

        var centerGO = new GameObject("CenterOverlay");
        centerGO.transform.SetParent(rect, false);
        var centerRect = centerGO.AddComponent<RectTransform>();
        centerRect.anchorMin = new Vector2(0.5f, 0.5f);
        centerRect.anchorMax = new Vector2(0.5f, 0.5f);
        centerRect.pivot = new Vector2(0.5f, 0.5f);
        var centerImage = centerGO.AddComponent<Image>();
        centerImage.preserveAspect = true;
        centerGO.SetActive(false);

        var refs = new RoomIconRefs
        {
            room = room,
            rect = rect,
            backgroundImage = backgroundImage,
            centerOverlayRect = centerRect,
            centerOverlayImage = centerImage,
        };

        string[] quadrantNames = { "Quadrant_Top", "Quadrant_Left", "Quadrant_Right", "Quadrant_Bottom" };
        for (int i = 0; i < 4; i++)
        {
            var qGO = new GameObject(quadrantNames[i]);
            qGO.transform.SetParent(rect, false);
            var qRect = qGO.AddComponent<RectTransform>();
            qRect.anchorMin = new Vector2(0.5f, 0.5f);
            qRect.anchorMax = new Vector2(0.5f, 0.5f);
            qRect.pivot = new Vector2(0.5f, 0.5f);
            var qImage = qGO.AddComponent<Image>();
            qImage.preserveAspect = true;
            qGO.SetActive(false);

            refs.quadrantRects[i] = qRect;
            refs.quadrantImages[i] = qImage;
        }

        return refs;
    }

    private LineRefs CreateLine(Vector2Int coordA, Vector2Int coordB)
    {
        var go = new GameObject("Line");
        go.transform.SetParent(linesContainer, false);

        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f); // 왼쪽 끝을 기준점으로 삼아 길이/회전을 조절

        var image = go.AddComponent<Image>();

        return new LineRefs { rect = rect, image = image, coordA = coordA, coordB = coordB };
    }

    // gridRotationDegrees/cellPixelSize만 적용한 "원점 기준 회전 좌표"입니다.
    // 자동 정렬 오프셋을 더하기 전 단계의 값이라, 방 전체의 우측/상단 끝을 찾는 데 씁니다.
    private Vector2 RawRotate(Vector2Int coord)
    {
        float rad = gridRotationDegrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad);
        float sin = Mathf.Sin(rad);
        float rx = coord.x * cos - coord.y * sin;
        float ry = coord.x * sin + coord.y * cos;
        return new Vector2(rx, ry) * cellPixelSize;
    }

    private void Reflow()
    {
        hasMapBounds = false;
        if (roomIcons.Count == 0) return;

        // [미니맵 구역] 예전의 "가장 오른쪽 위 방을 우상단 모서리에 붙이는 자동 정렬"은 없애고, 지도 판 기준
        // 좌표에 그대로 놓습니다(보이는 위치는 UpdateView의 viewCenter/zoom이 정함). 대신 이동 한계 계산용으로
        // 방 아이콘들의 외곽(지도 범위)을 구합니다.
        Vector2 autoBase = Vector2.zero;
        float half = iconSize * 0.5f;
        mapBoundsMin = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        mapBoundsMax = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        foreach (var icon in roomIcons)
        {
            Vector2 p = RawRotate(icon.room.occupiedCells[0]) + mapOriginOffset;
            mapBoundsMin = Vector2.Min(mapBoundsMin, p - new Vector2(half, half));
            mapBoundsMax = Vector2.Max(mapBoundsMax, p + new Vector2(half, half));
        }
        hasMapBounds = true;

        foreach (var icon in roomIcons)
        {
            Vector2 pos = RawRotate(icon.room.occupiedCells[0]) + autoBase + mapOriginOffset;
            icon.rect.anchoredPosition = pos;
            icon.rect.sizeDelta = new Vector2(iconSize, iconSize);
            ApplyBackground(icon);
            ApplyOverlay(icon);
        }

        foreach (var line in lines)
        {
            line.rect.gameObject.SetActive(showConnections);
            if (!showConnections) continue;

            Vector2 a = RawRotate(line.coordA) + autoBase + mapOriginOffset;
            Vector2 b = RawRotate(line.coordB) + autoBase + mapOriginOffset;
            Vector2 delta = b - a;
            float distance = delta.magnitude;
            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;

            line.rect.anchoredPosition = a;
            line.rect.sizeDelta = new Vector2(distance, lineThickness);
            line.rect.localRotation = Quaternion.Euler(0f, 0f, angle);
            line.image.color = lineColor;
        }
    }

    // 배경(마름모) 3종 중 현재 상태에 맞는 스프라이트를 적용합니다. 스프라이트가 없으면
    // fallbackColor를 상태별 밝기로 조절한 사각형으로 대체합니다.
    private void ApplyBackground(RoomIconRefs icon)
    {
        bool isCurrent = IsCurrentRoom(icon.room);
        bool isVisited = ShowAsVisited(icon.room);

        // [v1.18 변경] 배경은 더 이상 카테고리별이 아니라, 상태(현재/방문함/미탐색)에 따라
        // 모든 방이 공유하는 공용 3종 스프라이트 중 하나를 그대로 씁니다.
        Sprite sprite = isCurrent ? currentBackgroundIcon : (isVisited ? visitedBackgroundIcon : unvisitedBackgroundIcon);
        Color fallback = isCurrent ? currentFallbackColor : (isVisited ? visitedFallbackColor : unvisitedFallbackColor);

        if (sprite != null)
        {
            icon.backgroundImage.sprite = sprite;
            icon.backgroundImage.color = Color.white;
        }
        else
        {
            icon.backgroundImage.sprite = null;
            icon.backgroundImage.color = fallback;
        }
    }

    // 상태별 오버레이(현재=플레이어 마커 / 방문함=아이템 4격자 / 미탐색=variant 아이콘)를 적용합니다.
    private void ApplyOverlay(RoomIconRefs icon)
    {
        bool isCurrent = IsCurrentRoom(icon.room);
        bool isVisited = ShowAsVisited(icon.room);

        if (isCurrent)
        {
            SetCenterOverlay(icon, playerMarkerIcon, playerMarkerFallbackColor, true);
            HideAllQuadrants(icon);
        }
        else if (isVisited)
        {
            ApplyQuadrantMarkers(icon);

            // 상점/보스방 등: 방문한 뒤에도 정중앙에 방 종류 아이콘을 유지합니다. 나머지 방문한 방의 정중앙은 비움.
            if (KeepsTypeIconAfterVisit(icon.room.category))
            {
                SetCenterOverlay(icon, FindRoomTypeIcon(icon.room), Color.white, false);
            }
            else
            {
                icon.centerOverlayRect.gameObject.SetActive(false);
            }
        }
        else
        {
            Sprite variantSprite = FindRoomTypeIcon(icon.room);

            if (variantSprite != null)
            {
                SetCenterOverlay(icon, variantSprite, Color.white, false);
            }
            else
            {
                icon.centerOverlayRect.gameObject.SetActive(false);
            }
            HideAllQuadrants(icon);
        }
    }

    // sprite가 있으면 그대로, 없고 useFallbackColor가 true면(플레이어 마커 전용) 색상
    // 사각형으로 대체 표시합니다. 미탐색 variant 아이콘은 useFallbackColor=false로 호출되어,
    // sprite가 없으면 그냥 숨겨집니다(일반방처럼 "아무 것도 안 뜨는" 케이스를 위함).
    private void SetCenterOverlay(RoomIconRefs icon, Sprite sprite, Color fallbackColor, bool useFallbackColor)
    {
        if (sprite == null && !useFallbackColor)
        {
            icon.centerOverlayRect.gameObject.SetActive(false);
            return;
        }

        icon.centerOverlayRect.gameObject.SetActive(true);
        icon.centerOverlayRect.sizeDelta = new Vector2(centerOverlaySize, centerOverlaySize);

        if (sprite != null)
        {
            icon.centerOverlayImage.sprite = sprite;
            icon.centerOverlayImage.color = Color.white;
        }
        else
        {
            icon.centerOverlayImage.sprite = null;
            icon.centerOverlayImage.color = fallbackColor;
        }
    }

    private void HideAllQuadrants(RoomIconRefs icon)
    {
        for (int i = 0; i < 4; i++)
        {
            icon.quadrantRects[i].gameObject.SetActive(false);
        }
    }

    // 이 방에 남아있는 마커(아이템/보상)를 우선순위(markerIcons 리스트 순서) 기준으로 최대
    // 4개까지 찾아 상->좌->우->하 순서로 채웁니다.
    private void ApplyQuadrantMarkers(RoomIconRefs icon)
    {
        CollectMarkerVisuals(icon.room, scratchMarkerSprites, scratchMarkerColors);

        for (int i = 0; i < 4; i++)
        {
            if (i < scratchMarkerSprites.Count)
            {
                icon.quadrantRects[i].gameObject.SetActive(true);
                icon.quadrantRects[i].sizeDelta = new Vector2(quadrantIconSize, quadrantIconSize);
                icon.quadrantRects[i].anchoredPosition = QuadrantDirections[i] * quadrantOffset;

                var sprite = scratchMarkerSprites[i];
                if (sprite != null)
                {
                    icon.quadrantImages[i].sprite = sprite;
                    icon.quadrantImages[i].color = Color.white;
                }
                else
                {
                    icon.quadrantImages[i].sprite = null;
                    icon.quadrantImages[i].color = scratchMarkerColors[i];
                }
            }
            else
            {
                icon.quadrantRects[i].gameObject.SetActive(false);
            }
        }
    }

    // 보스방은 항상, 그 밖에는 keepIconAfterVisitCategories(기본: 상점/특수방)에 든 방만 방문 후에도 방 종류 아이콘을 유지합니다.
    private bool KeepsTypeIconAfterVisit(RoomCategory category)
    {
        if (category == RoomCategory.Boss) return true; // 보스 클리어 뒤 포탈 아이콘 대신 보스방 아이콘(형빈 확정)
        return keepIconAfterVisitCategories != null && keepIconAfterVisitCategories.Contains(category);
    }

    // room.id로 그 방의 RoomController -> ContentRoot를 찾아, 그 밑에 존재하는 IMinimapMarker /
    // IMinimapMultiMarker 구현체들을 스캔합니다. 같은 키가 여러 개 있어도 한 번만 세고,
    // markerIcons 리스트 순서(우선순위) 기준으로 최대 4개까지만 out 리스트에 채웁니다.
    // 포탈 키는 표시하지 않습니다(포탈이 있는 방도 남은 아이템은 다른 방처럼 표시).
    private void CollectMarkerVisuals(RoomInstance room, List<Sprite> outSprites, List<Color> outColors)
    {
        outSprites.Clear();
        outColors.Clear();

        var controller = levelBuilder.GetRoomController(room.id);
        if (controller == null || controller.ContentRoot == null) return;

        scratchFoundKeys.Clear();
        foreach (var mb in controller.ContentRoot.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (mb is IMinimapMarker marker && !string.IsNullOrEmpty(marker.MinimapIconKey))
            {
                scratchFoundKeys.Add(marker.MinimapIconKey);
            }

            if (mb is IMinimapMultiMarker multiMarker)
            {
                scratchKeyList.Clear();
                multiMarker.CollectMinimapKeys(scratchKeyList);
                foreach (var key in scratchKeyList)
                {
                    if (!string.IsNullOrEmpty(key)) scratchFoundKeys.Add(key);
                }
            }
        }

        foreach (var entry in markerIcons)
        {
            if (outSprites.Count >= 4) break;
            if (entry.markerKey == MinimapKeys.Portal) continue;
            if (!scratchFoundKeys.Contains(entry.markerKey)) continue;

            outSprites.Add(entry.icon);
            outColors.Add(entry.fallbackColor);
        }
    }

    private bool IsCurrentRoom(RoomInstance room)
    {
        return levelBuilder.HasPlayerCell && room.occupiedCells.Contains(levelBuilder.CurrentPlayerCell);
    }

    // [v1.18 변경] 미탐색 방에서 variantId 전용 아이콘이 없을 때 대신 쓸, 카테고리별
    // 기본 오버레이 아이콘을 찾습니다. 일반방처럼 icon이 비어있으면 null을 반환해
    // SetCenterOverlay가 그대로 숨깁니다.
    // 미니맵에 "방문함"으로 표시할지 정합니다. 클리어하지 않고 벗어난 전투방(일반방/보스방)은 visited여도 미방문으로
    // 표시합니다(형빈 확정 - 보스를 잡지 않고 탈출했는데 다녀간 방처럼 보이지 않도록).
    private static bool ShowAsVisited(RoomInstance room)
    {
        if (room == null || !room.visited) return false;
        bool isCombatRoom = room.category == RoomCategory.Normal || room.category == RoomCategory.Boss;
        return !isCombatRoom || room.isCleared;
    }

    // 방 종류 아이콘: variantId 전용 아이콘이 있으면 그것, 없으면 카테고리 기본 아이콘(없으면 null).
    private Sprite FindRoomTypeIcon(RoomInstance room)
    {
        Sprite sprite = FindVariantIcon(room.variantId);
        return sprite != null ? sprite : FindCategoryOverlay(room.category);
    }

    private Sprite FindCategoryOverlay(RoomCategory category)
    {
        var entry = categoryOverlayIcons.Find(e => e.category == category);
        return entry != null ? entry.icon : null;
    }

    private Sprite FindVariantIcon(string variantId)
    {
        if (string.IsNullOrEmpty(variantId)) return null;
        var entry = variantIcons.Find(v => v.variantId == variantId);
        return entry != null ? entry.icon : null;
    }

    // ------------------------------------------------------------------
    // 미니맵 구역 - 배치 / 입력 / 보기(확대·축소, 이동, 현재 방 따라가기)
    // ------------------------------------------------------------------

    // 구역 위치/크기/배경을 매 프레임 반영합니다(Play 중 인스펙터 값을 바꾸면 즉시 반영).
    private void ApplyZoneLayout()
    {
        panelRect.anchoredPosition = new Vector2(-panelMarginX, -panelMarginY);
        panelRect.sizeDelta = new Vector2(Mathf.Max(10f, zoneWidth), Mathf.Max(10f, zoneHeight));

        zoneImage.color = zoneColor;
        float radius = Mathf.Min(zoneCornerRadius, Mathf.Min(panelRect.sizeDelta.x, panelRect.sizeDelta.y) * 0.5f);
        if (radius < 0.5f)
        {
            zoneImage.sprite = null;
            zoneImage.type = Image.Type.Simple;
            return;
        }

        EnsureZoneRoundedSprite();
        zoneImage.sprite = zoneRoundedSprite;
        zoneImage.type = Image.Type.Sliced;
        // 스프라이트 모서리(ZoneTexRadius 텍셀)가 화면에서 radius(캔버스 px)로 보이도록 배율을 맞춥니다.
        zoneImage.pixelsPerUnitMultiplier = ZoneTexRadius / radius;
    }

    // 모서리가 ZoneTexRadius 텍셀인 흰색 둥근 사각형(가장자리 부드럽게)을 한 번만 만듭니다. 9-Slice로 늘려 씁니다.
    private void EnsureZoneRoundedSprite()
    {
        if (zoneRoundedSprite != null) return;

        int size = ZoneTexSize;
        float r = ZoneTexRadius;
        zoneRoundedTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        zoneRoundedTexture.wrapMode = TextureWrapMode.Clamp;
        zoneRoundedTexture.filterMode = FilterMode.Bilinear;

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
        zoneRoundedTexture.SetPixels32(pixels);
        zoneRoundedTexture.Apply();

        zoneRoundedSprite = Sprite.Create(
            zoneRoundedTexture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f),
            100f, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
    }

    private bool IsPointerOverZoneInternal()
    {
        if (!built || panelRect == null || !panelRect.gameObject.activeInHierarchy) return false;
        // HUD 캔버스는 Screen Space - Overlay라 카메라 없이(null) 비교합니다.
        return RectTransformUtility.RectangleContainsScreenPoint(panelRect, Input.mousePosition, null);
    }

    // 마우스 위치를 "구역 가운데 기준" 캔버스 좌표로 바꿉니다(구역 밖이어도 계산됨 - 드래그 중 사용).
    private Vector2 MouseZoneLocal()
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(panelRect, Input.mousePosition, null, out Vector2 local);
        return local - panelRect.rect.center;
    }

    private void UpdateView()
    {
        float dt = Time.unscaledDeltaTime;
        float zMin = Mathf.Max(0.01f, Mathf.Min(minZoom, maxZoom));
        float zMax = Mathf.Max(zMin, maxZoom);

        if (!zoomInitialized)
        {
            zoom = targetZoom = Mathf.Clamp(initialZoom, zMin, zMax);
            zoomInitialized = true;
        }
        targetZoom = Mathf.Clamp(targetZoom, zMin, zMax); // Play 중 최소/최대를 바꿔도 반영

        HandleZoneInput(zMin, zMax);
        UpdateFollowTarget();

        // 배율: 목표값으로 부드럽게. 휠로 바꾼 경우에는 커서 아래 지점이 고정되도록 viewCenter를 함께 맞춥니다.
        zoom = SmoothTowards(zoom, targetZoom, zoomSmoothSpeed, dt);
        if (Mathf.Abs(zoom - targetZoom) < 0.0005f) zoom = targetZoom;
        if (zoomAnchorActive)
        {
            viewCenter = zoomAnchorContent - zoomAnchorZoneLocal / zoom;
            if (zoom == targetZoom) zoomAnchorActive = false;
        }

        // 현재 방 따라가기 (드래그 중에는 멈춤)
        if (hasFollowTarget && !dragging)
        {
            viewCenter = SmoothTowards(viewCenter, followTarget, followSmoothSpeed, dt);
            if ((viewCenter - followTarget).sqrMagnitude < 0.01f)
            {
                viewCenter = followTarget;
                hasFollowTarget = false;
            }
        }

        // 이동 한계: 구역 가운데에 보이는 지점이 항상 지도 범위 안 -> 지도가 항상 구역에 걸쳐 있음
        if (hasMapBounds)
        {
            viewCenter = new Vector2(
                Mathf.Clamp(viewCenter.x, mapBoundsMin.x, mapBoundsMax.x),
                Mathf.Clamp(viewCenter.y, mapBoundsMin.y, mapBoundsMax.y));
        }

        mapContent.localScale = new Vector3(zoom, zoom, 1f);
        mapContent.anchoredPosition = -viewCenter * zoom;
    }

    // 휠(확대/축소)과 드래그(이동) 입력. 둘 다 구역 위에서 시작한 입력만 받습니다.
    private void HandleZoneInput(float zMin, float zMax)
    {
        bool over = IsPointerOverZoneInternal();

        // 현재 위치 버튼 - 버튼 위 클릭은 드래그를 시작하지 않고 바로 현재 방으로 맞춥니다.
        if (!dragging && over && IsPointerOverRecenterButton() && Input.GetMouseButtonDown(0))
        {
            RecenterNow(zMin, zMax);
            return;
        }

        // 휠 - 커서가 가리키는 지점을 고정한 채 확대/축소
        float wheel = Input.mouseScrollDelta.y;
        if (over && !dragging && Mathf.Abs(wheel) > 0.01f)
        {
            float factor = Mathf.Pow(1f + Mathf.Max(0.001f, zoomStepPerNotch), wheel);
            targetZoom = Mathf.Clamp(targetZoom * factor, zMin, zMax);

            Vector2 cursor = MouseZoneLocal();
            zoomAnchorZoneLocal = cursor;
            zoomAnchorContent = viewCenter + cursor / zoom;
            zoomAnchorActive = true;
            hasFollowTarget = false; // 직접 조작하면 진행 중이던 따라가기는 멈춤
        }

        // 드래그 - 구역 안에서 좌클릭으로 시작, 버튼을 뗄 때까지 계속(구역 밖으로 나가도)
        if (!dragging && over && Input.GetMouseButtonDown(0))
        {
            dragging = true;
            lastDragZoneLocal = MouseZoneLocal();
            hasFollowTarget = false;
            zoomAnchorActive = false;
        }

        if (dragging)
        {
            if (!Input.GetMouseButton(0))
            {
                dragging = false;
            }
            else
            {
                Vector2 now = MouseZoneLocal();
                viewCenter -= (now - lastDragZoneLocal) / zoom;
                lastDragZoneLocal = now;
            }
        }
    }

    // 플레이어가 다른 방으로 옮겼는지 확인해 따라갈 목표를 정합니다. 새 레벨이면 첫 방에 바로 맞춥니다.
    private void UpdateFollowTarget()
    {
        if (!levelBuilder.HasPlayerCell) return;

        Vector2Int cell = levelBuilder.CurrentPlayerCell;
        bool changed = !hasLastPlayerCell || cell != lastPlayerCell;
        if (!changed && !snapOnNextCell) return;

        if (!TryGetCurrentRoomPosition(out Vector2 roomPos)) return;
        hasLastPlayerCell = true;
        lastPlayerCell = cell;

        if (snapOnNextCell)
        {
            viewCenter = roomPos;
            hasFollowTarget = false;
            zoomAnchorActive = false;
            snapOnNextCell = false;
        }
        else
        {
            if (followCurrentRoom)
            {
                followTarget = roomPos;
                hasFollowTarget = true;
            }
            // 방을 옮기면 배율도 기본값으로 되돌림(형빈 요청). 커서 기준점은 끄므로 위치는 위 따라가기가 정합니다.
            if (resetZoomOnRoomChange)
            {
                float zMin = Mathf.Max(0.01f, Mathf.Min(minZoom, maxZoom));
                float zMax = Mathf.Max(zMin, maxZoom);
                targetZoom = Mathf.Clamp(initialZoom, zMin, zMax);
            }
            zoomAnchorActive = false;
        }
    }

    // 현재 방 아이콘의 지도 판 좌표(Reflow와 같은 계산)를 찾습니다.
    private bool TryGetCurrentRoomPosition(out Vector2 pos)
    {
        foreach (var icon in roomIcons)
        {
            if (IsCurrentRoom(icon.room))
            {
                pos = RawRotate(icon.room.occupiedCells[0]) + mapOriginOffset;
                return true;
            }
        }
        pos = Vector2.zero;
        return false;
    }

    // speed가 0이면 즉시, 아니면 프레임 속도와 무관하게 일정한 비율로 목표에 다가갑니다.
    private static float SmoothTowards(float current, float target, float speed, float dt)
    {
        if (speed <= 0f) return target;
        return Mathf.Lerp(current, target, 1f - Mathf.Exp(-speed * dt));
    }

    private static Vector2 SmoothTowards(Vector2 current, Vector2 target, float speed, float dt)
    {
        if (speed <= 0f) return target;
        return Vector2.Lerp(current, target, 1f - Mathf.Exp(-speed * dt));
    }

    // ------------------------------------------------------------------
    // 현재 위치 버튼
    // ------------------------------------------------------------------

    // 지도 판보다 나중에 만들어 항상 지도 위에 그려집니다(구역 Mask 안쪽이라 구역 밖으로는 안 나감).
    private void BuildRecenterButton()
    {
        var go = new GameObject("RecenterButton");
        go.transform.SetParent(panelRect, false);
        recenterButtonRect = go.AddComponent<RectTransform>();
        recenterButtonRect.anchorMin = recenterButtonRect.anchorMax = new Vector2(1f, 0f);
        recenterButtonRect.pivot = new Vector2(1f, 0f);
        recenterButtonImage = go.AddComponent<Image>();
        recenterButtonImage.raycastTarget = false;

        var labelGO = new GameObject("Label");
        labelGO.transform.SetParent(go.transform, false);
        var labelRect = labelGO.AddComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        // [TMP] 옛 방식 Text는 화면 배율에 따라 흐릿하게 보여서 TextMeshPro로 교체했습니다(HUDText 참고).
        recenterButtonLabel = HUDText.AddTo(labelGO, TextAnchor.MiddleCenter);

        go.transform.SetAsLastSibling();
    }

    // 버튼 위치/크기/색/글자를 매 프레임 반영합니다(Play 중 인스펙터 값을 바꾸면 즉시 반영).
    private void ApplyRecenterButtonLayout()
    {
        if (recenterButtonRect == null) return;

        if (recenterButtonRect.gameObject.activeSelf != showRecenterButton)
            recenterButtonRect.gameObject.SetActive(showRecenterButton);
        if (!showRecenterButton) return;

        Vector2 size = new Vector2(Mathf.Max(4f, recenterButtonSize.x), Mathf.Max(4f, recenterButtonSize.y));
        recenterButtonRect.sizeDelta = size;
        recenterButtonRect.anchoredPosition = new Vector2(-recenterButtonMargin.x, recenterButtonMargin.y);

        bool hovered = IsPointerOverRecenterButton();
        recenterButtonImage.color = hovered ? recenterButtonHoverColor : recenterButtonColor;

        // 구역 배경과 같은 흰색 둥근 사각형 스프라이트를 9-Slice로 늘려 씁니다.
        float radius = Mathf.Min(recenterButtonCornerRadius, Mathf.Min(size.x, size.y) * 0.5f);
        if (radius < 0.5f)
        {
            recenterButtonImage.sprite = null;
            recenterButtonImage.type = Image.Type.Simple;
        }
        else
        {
            EnsureZoneRoundedSprite();
            recenterButtonImage.sprite = zoneRoundedSprite;
            recenterButtonImage.type = Image.Type.Sliced;
            recenterButtonImage.pixelsPerUnitMultiplier = ZoneTexRadius / radius;
        }

        recenterButtonLabel.text = recenterButtonText;
        recenterButtonLabel.fontSize = recenterButtonFontSize;
        recenterButtonLabel.color = recenterButtonTextColor;
    }

    private bool IsPointerOverRecenterButton()
    {
        if (recenterButtonRect == null || !recenterButtonRect.gameObject.activeInHierarchy) return false;
        return RectTransformUtility.RectangleContainsScreenPoint(recenterButtonRect, Input.mousePosition, null);
    }

    // 방을 옮길 때와 같은 방식으로, 현재 방이 구역 가운데 오도록 부드럽게 이동하고(followSmoothSpeed) 배율도
    // Initial Zoom으로 부드럽게 되돌립니다(zoomSmoothSpeed). (형빈 요청으로 즉시 이동에서 부드러운 이동으로 변경)
    private void RecenterNow(float zMin, float zMax)
    {
        targetZoom = Mathf.Clamp(initialZoom, zMin, zMax);
        zoomAnchorActive = false;
        if (TryGetCurrentRoomPosition(out Vector2 roomPos))
        {
            followTarget = roomPos;
            hasFollowTarget = true;
        }
    }
}