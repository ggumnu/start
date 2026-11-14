using UnityEngine;

// [게임 클리어] 결과 화면(GameClearUI)의 문구/크기/색/연출 시간 설정입니다. Assets/Resources/GameClearUISettings 에 두면 자동으로 읽고,
// 없으면 아래 기본값. 팝업 창/버튼 모양과 글자 색은 PopupStyle을 그대로 씁니다(흰 창 + 모서리 무늬, 평행사변형 버튼).
// 만들기: Project 창에서 Assets/Resources 폴더 우클릭 > Create > MobArchive > Game Clear UI Settings, 이름을 GameClearUISettings로.
[CreateAssetMenu(menuName = "MobArchive/Game Clear UI Settings", fileName = "GameClearUISettings")]
public class GameClearUISettings : ScriptableObject
{
    public const string ResourcePath = "GameClearUISettings";

    [Header("문구")]
    public string title = "BATTLE COMPLETE";
    public string timeLabel = "전투 시간";
    public string killsLabel = "처치한 적";
    public string roomsLabel = "클리어한 방";
    public string restartLabel = "재시작하기";
    public string quitLabel = "그만하기";
    [Tooltip("[그만하기]로 돌아갈 시작 화면 씬 이름입니다.")]
    public string titleSceneName = "Title";

    [Header("화면")]
    [Tooltip("다른 HUD/팝업보다 위, 로딩 화면(1000)보다 아래입니다.")]
    public int canvasSortingOrder = 110;
    [Tooltip("화면 전체를 덮는 반투명 검은 막의 색입니다.")]
    public Color dimColor = new Color(0f, 0f, 0f, 0.55f);

    [Header("BATTLE COMPLETE 글자 - 크기/위치")]
    public int titleFontSize = 70;
    [Tooltip("글자 왼쪽 아래 끝의 위치입니다. 팝업 창 왼쪽 위 모서리 기준(x = 오른쪽으로, y = 위로, 1920x1080 기준 px).")]
    public Vector2 titleOffset = new Vector2(-6f, 6f);
    [Tooltip("오른쪽으로 기울이는 각도(도)입니다. 0이면 똑바로.")]
    [Range(0f, 30f)] public float titleSlantAngle = 14f;

    [Header("BATTLE COMPLETE 글자 - 색 (위 -> 아래, 글자 높이 기준 0~1)")]
    public Color titleTopRimColor = new Color32(0xFF, 0xFA, 0xA0, 0xFF);
    public Color titleTopColor = new Color32(0xFF, 0xEA, 0x50, 0xFF);
    public Color titleMiddleColor = new Color32(0xFF, 0xE0, 0x3E, 0xFF);
    public Color titleBottomColor = new Color32(0xFF, 0xD2, 0x2C, 0xFF);
    public Color titleBottomRimColor = new Color32(0xFD, 0xE8, 0x6E, 0xFF);
    [Tooltip("맨 위 밝은 띠의 두께(글자 높이 대비)입니다.")]
    [Range(0f, 0.4f)] public float titleTopRimSize = 0.1f;
    [Tooltip("맨 아래 밝은 띠의 두께(글자 높이 대비)입니다.")]
    [Range(0f, 0.4f)] public float titleBottomRimSize = 0.1f;

    [Header("BATTLE COMPLETE 글자 - 삼각 무늬 (글자 안, 양 끝에서 가운데로 옅어짐)")]
    public Color titlePatternColor = new Color32(0xFF, 0xFA, 0xBE, 0xFF);
    [Range(0f, 1f)] public float titlePatternAlpha = 0.7f;
    [Tooltip("삼각형 한 변의 길이(글자 높이 대비)입니다.")]
    [Range(0.05f, 1f)] public float titlePatternSize = 0.34f;
    [Tooltip("무늬가 양 끝에서 가운데로 퍼지는 폭(글자 줄 전체 폭 대비)입니다.")]
    [Range(0.05f, 0.5f)] public float titlePatternSpread = 0.3f;

    [Header("BATTLE COMPLETE 글자 - 그림자 (TextMeshPro Underlay 값)")]
    public Color titleShadowColor = new Color(0.106f, 0.165f, 0.251f, 0.55f);
    [Range(-1f, 1f)] public float titleShadowOffsetX = 0f;
    [Range(-1f, 1f)] public float titleShadowOffsetY = -0.6f;
    [Range(-1f, 1f)] public float titleShadowDilate = 0.25f;
    [Range(0f, 1f)] public float titleShadowSoftness = 0.5f;

    [Header("팝업 창 (화면 정중앙, 1920x1080 기준 px)")]
    public Vector2 panelSize = new Vector2(640f, 420f);
    public int labelFontSize = 28;
    [Tooltip("첫 줄의 세로 위치(창 위쪽 끝에서 아래로)입니다.")]
    public float firstRowY = 70f;
    public float rowSpacing = 84f;
    [Tooltip("항목 이름의 오른쪽 끝 위치(창 왼쪽 끝 기준)입니다.")]
    public float labelRight = 215f;
    [Tooltip("남색 수치 판의 가운데 위치(창 왼쪽 끝 기준)입니다.")]
    public float valueCenterX = 410f;
    public Vector2 valueBadgeSize = new Vector2(300f, 50f);
    public Color valueBadgeColor = new Color32(0x2C, 0x45, 0x63, 0xFF);
    public Color valueTextColor = Color.white;
    public int valueFontSize = 28;

    [Header("버튼")]
    public Vector2 buttonSize = new Vector2(200f, 64f);
    [Tooltip("두 버튼 가운데 사이 거리의 절반입니다.")]
    public float buttonHalfDistance = 130f;
    [Tooltip("창 아래쪽 끝에서 버튼 아래쪽 끝까지의 거리입니다.")]
    public float buttonBottomMargin = 30f;
    public int buttonFontSize = 28;

    [Header("연출 (실제 시간 기준, 초)")]
    [Tooltip("검은 막이 짙어지는 시간입니다.")]
    [Min(0f)] public float dimFadeDuration = 0.4f;
    [Tooltip("검은 막이 깔리기 시작한 뒤 글자가 나타나기 시작할 때까지의 시간입니다.")]
    [Min(0f)] public float titleDelay = 0.1f;
    [Tooltip("글자가 오른쪽에서 밀려 들어오며 나타나는 시간입니다.")]
    [Min(0f)] public float titleAppearDuration = 0.45f;
    [Tooltip("글자가 오른쪽에서 밀려 들어오는 거리입니다.")]
    public float titleSlideDistance = 160f;
    [Tooltip("다 나타난 뒤 흰빛이 걷히며 본래 색이 되는 시간입니다.")]
    [Min(0f)] public float titleFlashFadeDuration = 0.5f;
    [Tooltip("글자가 나타나기 시작한 뒤 팝업 창이 올라오기 시작할 때까지의 시간입니다.")]
    [Min(0f)] public float popupDelay = 0.4f;
    [Min(0f)] public float popupAppearDuration = 0.35f;
    [Tooltip("팝업 창이 아래에서 올라오는 거리입니다.")]
    public float popupRiseDistance = 40f;
    [Tooltip("팝업 창이 다 올라온 뒤 첫 수치 판이 나타나기 시작할 때까지의 시간입니다.")]
    [Min(0f)] public float rowsDelay = 0.1f;
    [Tooltip("수치 판끼리 나타나는 시간차입니다.")]
    [Min(0f)] public float rowInterval = 0.45f;
    [Min(0f)] public float badgeAppearDuration = 0.25f;
    [Tooltip("수치 판이 아래에서 올라오는 거리입니다.")]
    public float badgeRiseDistance = 20f;
    [Tooltip("수치가 0에서 실제 값까지 올라가는 시간입니다.")]
    [Min(0f)] public float countDuration = 0.6f;

    private static GameClearUISettings cached;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlayStart()
    {
        cached = null;
    }

    public static GameClearUISettings Get()
    {
        if (cached == null) cached = Resources.Load<GameClearUISettings>(ResourcePath);
        if (cached == null) cached = CreateInstance<GameClearUISettings>();
        return cached;
    }
}
