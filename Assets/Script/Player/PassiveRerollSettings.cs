using UnityEngine;

// [패시브 교체 창 설정 에셋 - 56번]
// 56번 일회용(패시브 하나를 한 등급 낮은 패시브 여러 개로 교체)이 띄우는 창(PassiveRerollUI)의 문구/크기 값을 모아둔 에셋입니다.
// Assets/Resources 폴더에 "PassiveRerollSettings"라는 이름으로 만들어두면 자동으로 읽습니다. 에셋이 없으면 아래 기본값으로 동작합니다.
// 검은 창 바탕색/모서리, 버튼 색(평소/마우스 올림/못 누름)/글자 색은 공용 스타일 에셋(PopupStyle)에서 읽습니다.
// 패시브 칸 배경/칸 배치(한 줄 칸 수/아이콘 크기/간격)는 화면 왼쪽 패시브 인벤토리(PlayerInventoryDisplay)의 값을 그대로 가져옵니다.
//
// 만들기: Project 창에서 Assets/Resources 폴더 우클릭 > Create > MobArchive > Passive Reroll Settings, 이름을 PassiveRerollSettings로.
[CreateAssetMenu(fileName = "PassiveRerollSettings", menuName = "MobArchive/Passive Reroll Settings")]
public class PassiveRerollSettings : ScriptableObject
{
    public const string ResourcePath = "PassiveRerollSettings";

    [Header("문구")]
    public string title = "교체할 패시브 아이템을 선택하세요";
    public string confirmLabel = "교체하기";
    public string cancelLabel = "취소하기";
    [Tooltip("확인 창 아랫줄 문구입니다(윗줄은 고른 아이템의 이름 판).")]
    public string confirmQuestionLine = "정말 교체하시겠습니까?";
    [Tooltip("교체할 수 있는 패시브가 하나도 없을 때(패시브가 없거나, 모든 패시브가 교체 후보가 없을 때) 문구입니다.")]
    public string noItemMessage = "교체할 수 있는 아이템이 없습니다";
    public string okLabel = "확인";

    [Header("창 공통 (1920x1080 기준 px)")]
    [Tooltip("다른 HUD보다 위, 슬롯 교체 확인창(90)/게임 오버 창(100)보다 아래입니다.")]
    public int canvasSortingOrder = 85;
    [Tooltip("창이 열려 있는 동안 화면 전체를 덮는 색입니다(반투명 검정).")]
    public Color dimColor = new Color(0f, 0f, 0f, 0.55f);

    [Header("선택 창 (흰 창 - 바탕/모서리/무늬/글자 색은 PopupStyle)")]
    public Vector2 windowSize = new Vector2(760f, 720f);
    public int titleFontSize = 34;
    [Tooltip("창 위쪽 끝에서 제목까지의 거리입니다.")]
    public float titleTopMargin = 60f;

    [Header("패시브 목록 (칸 배치는 패시브 인벤토리 HUD 값을 그대로 씀)")]
    [Tooltip("목록 크기 배율입니다.")]
    [Min(0.1f)] public float gridScale = 1.1f;
    [Tooltip("한 번에 보이는 줄 수입니다. 이보다 많으면 마우스 휠로 스크롤합니다.")]
    [Min(1)] public int visibleRows = 4;
    [Tooltip("목록의 세로 위치입니다(창 중앙 기준, 양수면 위로).")]
    public float gridOffsetY = 5f;
    [Tooltip("마우스 휠 한 칸에 움직이는 거리(px)입니다.")]
    [Min(1f)] public float scrollSpeed = 60f;
    [Tooltip("선택한 아이템 아이콘의 불투명도입니다(반투명).")]
    [Range(0f, 1f)] public float selectedAlpha = 0.45f;
    [Tooltip("선택한 아이템 아이콘의 크기 배율입니다(1보다 작으면 작아짐).")]
    [Range(0.1f, 1f)] public float selectedScale = 0.75f;
    [Tooltip("교체 후보가 없어 선택할 수 없는 아이템 아이콘에 곱하는 색입니다(어둡게 - 크기/불투명도는 그대로라 선택한 아이콘과 구분됨).")]
    public Color unavailableTint = new Color(0.3f, 0.3f, 0.3f, 1f);

    [Header("선택 창 버튼 (색/모양/글자 색은 PopupStyle - [교체하기]/[취소하기] 모두 하늘색)")]
    public Vector2 mainButtonSize = new Vector2(220f, 90f);
    public float mainButtonSpacing = 60f;
    [Tooltip("창 아래쪽 끝에서 버튼까지의 거리입니다.")]
    public float mainButtonBottomMargin = 45f;
    public int mainButtonFontSize = 34;

    [Header("작은 흰 창 (확인 창 [교체하기] = 노랑 / [취소하기] = 하늘색, 사용할 수 없음 창 [확인] = 노랑 - 바탕/글자 색/버튼은 PopupStyle)")]
    public Vector2 confirmSize = new Vector2(560f, 240f);
    public Vector2 messageSize = new Vector2(480f, 220f);
    public int darkMessageFontSize = 26;
    public Vector2 darkButtonSize = new Vector2(140f, 46f);
    public float darkButtonSpacing = 30f;
    public float darkButtonBottomMargin = 24f;
    public int darkButtonFontSize = 22;

    // ---------------- 읽기 ----------------

    private static PassiveRerollSettings cached;
    private static bool loaded;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlayStart()
    {
        cached = null;
        loaded = false;
    }

    // Resources/PassiveRerollSettings 에셋을 읽습니다. 없으면 기본값으로 만든 임시 설정을 돌려줍니다(항상 null이 아님).
    public static PassiveRerollSettings Get()
    {
        if (!loaded || cached == null)
        {
            loaded = true;
            cached = Resources.Load<PassiveRerollSettings>(ResourcePath);
            if (cached == null)
            {
                Debug.Log("PassiveRerollSettings: Assets/Resources/PassiveRerollSettings 에셋이 없어 기본값으로 동작합니다.");
                cached = CreateInstance<PassiveRerollSettings>();
            }
        }
        return cached;
    }
}
