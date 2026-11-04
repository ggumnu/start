using UnityEngine;

// [아이템 도감 설정 에셋]
// 시작 화면의 아이템 도감(ItemCodexUI)이 쓰는 값을 모아둔 에셋입니다.
// Assets/Resources 폴더에 "ItemCodexSettings"라는 이름으로 만들어두면 자동으로 읽습니다.
// 에셋이 없으면 아래 기본값으로 동작하지만, 아이템 데이터베이스가 비어 있어 도감에 아무것도 나오지 않습니다.
//
// 만들기: Project 창에서 Assets/Resources 폴더 우클릭 > Create > MobArchive > Item Codex Settings, 이름을 ItemCodexSettings로.
// 그다음 Item Database 칸에 게임에서 쓰는 ItemDatabase 에셋을, Hud Prefab 칸에 플레이어 HUD 프리팹을 끌어다 넣습니다.
//
// 버튼 색(평소/마우스 올림)과 버튼 글자 색은 공용 스타일 에셋(PopupStyle)에서 읽습니다.
// 아이템 설명칸 모양(색/글자 크기/등급 스프라이트 등)은 HUD 프리팹 안의 PlayerItemInfoDisplay 값을 그대로 씁니다(인벤토리 설명칸과 항상 같은 모양).
[CreateAssetMenu(fileName = "ItemCodexSettings", menuName = "MobArchive/Item Codex Settings")]
public class ItemCodexSettings : ScriptableObject
{
    public const string ResourcePath = "ItemCodexSettings";

    [Header("참조")]
    [Tooltip("도감에 보여줄 아이템 목록입니다. 게임에서 쓰는 ItemDatabase 에셋을 넣으세요(여기 등록된 아이템이 전부 ID 순서로 나옵니다).")]
    public ItemDatabase itemDatabase;

    [Tooltip("플레이어 HUD 프리팹입니다. 이 프리팹 안(자식 포함)의 PlayerItemInfoDisplay 값으로 아이템 설명칸 모양을 정합니다. " +
             "비워두면 PlayerItemInfoDisplay의 기본값 모양(등급 스프라이트 없음)으로 뜹니다.")]
    public GameObject hudPrefab;

    [Header("키")]
    [Tooltip("도감이 열려 있을 때 이 키를 누르면 [돌아가기]와 같습니다.")]
    public KeyCode closeKey = KeyCode.Escape;

    [Header("문구")]
    public string title = "아이템 도감";
    public string backLabel = "돌아가기";

    [Header("화면 (1920x1080 기준 px)")]
    [Tooltip("화면 전체를 덮는 막의 색입니다(불투명 흰색).")]
    public Color overlayColor = Color.white;
    [Tooltip("시작 화면(기본 0)보다 위, 로딩 화면(1000)보다 아래입니다.")]
    public int canvasSortingOrder = 50;

    [Header("배경 무늬 (흰 막 위, 왼쪽 위 / 오른쪽 아래 모서리의 삼각형 무늬)")]
    [Tooltip("무늬를 그리는 셰이더입니다. Assets/Shaders/UICodexCornerPattern.shader를 넣으세요(빌드에 포함되도록). " +
             "비우면 이름으로 찾아 쓰고, 그래도 없으면 무늬 없이 흰 화면만 나옵니다.")]
    public Shader patternShader;
    [Tooltip("끄면 무늬 없이 흰 화면만 나옵니다.")]
    public bool showPattern = true;
    [Tooltip("삼각형 한 변의 길이(px)입니다.")]
    [Min(8f)] public float patternTriangleSide = 145f;
    [Tooltip("모서리에서 대각선 안쪽으로 이 거리(px, 가로+세로)만큼 가면 무늬가 완전히 사라집니다.")]
    [Min(1f)] public float patternSpread = 820f;
    [Tooltip("가장 진한 곳의 불투명도입니다. 낮출수록 연해집니다.")]
    [Range(0f, 1f)] public float patternMaxAlpha = 0.75f;
    [Tooltip("삼각형의 가장 진한 색입니다(흰색 ~ 이 색 사이에서 삼각형마다 진하기가 다름).")]
    public Color patternTint = new Color32(200, 228, 248, 255);
    [Tooltip("반짝일 때 바뀌는 색입니다.")]
    public Color patternGlowColor = new Color32(150, 205, 250, 255);
    [Tooltip("반짝임 세기입니다. 0이면 반짝이지 않습니다.")]
    [Range(0f, 1f)] public float patternGlowAmount = 0.35f;
    [Tooltip("반짝임 속도입니다(초당 횟수 - 0.15면 삼각형 하나가 평균 약 7초에 한 번).")]
    [Min(0f)] public float patternGlowSpeed = 0.15f;
    [Tooltip("클수록 반짝임이 짧고 드물게 보입니다.")]
    [Min(0.1f)] public float patternGlowSharpness = 6f;
    [Tooltip("바꾸면 삼각형 진하기 배치가 달라집니다.")]
    public float patternSeed = 0f;

    [Header("제목")]
    public int titleFontSize = 56;
    public Color titleColor = new Color32(0x34, 0x33, 0x65, 0xFF);
    [Tooltip("제목의 세로 위치입니다(화면 중앙 기준, 양수면 위로).")]
    public float titleOffsetY = 400f;

    [Header("아이템 패널 (연회색 둥근 판)")]
    public Color panelColor = new Color(0.86f, 0.86f, 0.86f, 1f);
    [Min(0f)] public float panelCornerRadius = 28f;
    [Tooltip("패널 중심의 세로 위치입니다(화면 중앙 기준).")]
    public float panelOffsetY = -10f;
    [Tooltip("패널 테두리와 아이콘 사이 여백입니다.")]
    [Min(0f)] public float panelPadding = 32f;

    [Header("아이콘 칸")]
    [Tooltip("한 줄에 놓을 아이콘 개수입니다.")]
    [Min(1)] public int columns = 10;
    [Min(8f)] public float iconSize = 80f;
    public float iconSpacingX = 16f;
    public float iconSpacingY = 16f;
    [Tooltip("패널에 한 번에 보이는 줄 수입니다. 이보다 많으면 마우스 휠로 스크롤합니다.")]
    [Min(1)] public int visibleRows = 6;
    [Tooltip("마우스 휠 한 칸에 움직이는 거리(px)입니다.")]
    [Min(1f)] public float scrollSpeed = 60f;
    [Tooltip("한 번도 얻지 않은 아이템 아이콘의 불투명도입니다.")]
    [Range(0f, 1f)] public float unacquiredAlpha = 0.35f;
    [Tooltip("아이콘 스프라이트가 아직 없는 아이템 자리에 칠할 색입니다.")]
    public Color missingIconColor = new Color(0.6f, 0.6f, 0.6f, 1f);

    [Header("돌아가기 버튼 (색/글자 색은 PopupStyle)")]
    public Vector2 buttonSize = new Vector2(220f, 80f);
    [Tooltip("버튼의 세로 위치입니다(화면 중앙 기준, 음수면 아래로).")]
    public float buttonOffsetY = -420f;
    [Min(0f)] public float buttonCornerRadius = 18f;
    public int buttonFontSize = 32;

    [Header("아이템 설명칸 위치")]
    [Tooltip("커서 기준 오프셋입니다. y는 커서보다 얼마나 위에 띄울지입니다(인벤토리 설명칸과 같은 기본값).")]
    public Vector2 tooltipCursorOffset = new Vector2(0f, 24f);
    [Tooltip("화면 가장자리와 설명칸 사이 최소 여백입니다.")]
    public float tooltipScreenEdgeMargin = 16f;

    [Header("열릴 때 연출 (실제 시간 기준, 초 - 기본값은 ESC 중단 화면과 같음)")]
    [Tooltip("흰 막이 짙어지는 시간입니다.")]
    [Min(0f)] public float overlayFadeInDuration = 0.3f;
    [Tooltip("[도감]을 누른 뒤 제목과 아이템 패널이 떠오르기 시작할 때까지의 시간입니다.")]
    [Min(0f)] public float contentDelay = 0.2f;
    [Tooltip("제목과 아이템 패널이 투명했다가 선명해지며 떠오르는 시간입니다.")]
    [Min(0f)] public float contentFadeInDuration = 0.3f;
    [Tooltip("[도감]을 누른 뒤 [돌아가기] 버튼이 떠오르기 시작할 때까지의 시간입니다.")]
    [Min(0f)] public float buttonDelay = 0.4f;
    [Tooltip("[돌아가기] 버튼이 떠오르는 시간입니다.")]
    [Min(0f)] public float buttonFadeInDuration = 0.25f;

    [Header("닫힐 때 연출 (실제 시간 기준, 초)")]
    [Tooltip("제목/패널/버튼이 사라지며 가라앉고, 흰 막이 걷히는 시간입니다.")]
    [Min(0f)] public float closeDuration = 0.35f;

    [Header("떠오름 / 가라앉음")]
    [Tooltip("떠오를 때 아래에서 올라오는 거리, 사라질 때 가라앉는 거리입니다.")]
    public float riseDistance = 30f;

    // ---------------- 개발용 ----------------

    [ContextMenu("아이템 도감 기록 초기화")]
    private void ResetCodexRecord()
    {
        ItemCodexRecord.ResetAll();
    }

    // ---------------- 읽기 ----------------

    private static ItemCodexSettings cached;
    private static bool loaded;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlayStart()
    {
        cached = null;
        loaded = false;
    }

    // Resources/ItemCodexSettings 에셋을 읽습니다. 없으면 기본값으로 만든 임시 설정을 돌려줍니다(항상 null이 아님).
    public static ItemCodexSettings Get()
    {
        if (!loaded || cached == null)
        {
            loaded = true;
            cached = Resources.Load<ItemCodexSettings>(ResourcePath);
            if (cached == null)
            {
                Debug.LogWarning("ItemCodexSettings: Assets/Resources/ItemCodexSettings 에셋이 없어 기본값으로 동작합니다. " +
                                 "아이템 데이터베이스가 연결되지 않아 도감에 아이템이 나오지 않습니다.");
                cached = CreateInstance<ItemCodexSettings>();
            }
        }
        return cached;
    }
}
