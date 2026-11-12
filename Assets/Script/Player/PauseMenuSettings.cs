using UnityEngine;

// [게임 중단 화면 설정 에셋]
// ESC로 여는 게임 중단 화면(PauseMenuUI)의 문구/크기/연출 시간 값을 모아둔 에셋입니다.
// Assets/Resources 폴더에 "PauseMenuSettings"라는 이름으로 만들어두면 자동으로 읽습니다. 에셋이 없으면 아래 기본값으로 동작합니다.
// 버튼 색/모양/글자 색, 확인창 바탕/모서리/글자 색은 공용 스타일 에셋(PopupStyle)에서 읽습니다.
//
// 만들기: Project 창에서 Assets/Resources 폴더 우클릭 > Create > MobArchive > Pause Menu Settings, 이름을 PauseMenuSettings로.
[CreateAssetMenu(fileName = "PauseMenuSettings", menuName = "MobArchive/Pause Menu Settings")]
public class PauseMenuSettings : ScriptableObject
{
    public const string ResourcePath = "PauseMenuSettings";

    [Header("키")]
    [Tooltip("게임 중 이 키를 누르면 중단 화면이 열리고, 열려 있을 때 누르면 [계속하기]와 같습니다. 확인창이 떠 있을 때는 [취소하기]와 같습니다.")]
    public KeyCode pauseKey = KeyCode.Escape;

    [Header("문구")]
    public string message = "게임을 중단하시겠습니까?";
    public string resumeLabel = "계속하기";
    public string settingsLabel = "설정";
    public string codexLabel = "도감";
    public string restartLabel = "재시작하기";
    public string quitLabel = "그만하기";

    [Header("확인창 문구 ([재시작하기]/[그만하기]를 누르면 뜨는 흰 창)")]
    [TextArea] public string restartConfirmMessage = "게임을 재시작하시겠습니까?\n지금까지의 진행 상황은 사라집니다.";
    [TextArea] public string quitConfirmMessage = "시작 화면으로 돌아가시겠습니까?\n지금까지의 진행 상황은 사라집니다.";
    [Tooltip("확인창의 실행 버튼 글자입니다. 비우면 누른 버튼 이름([재시작하기]/[그만하기])을 그대로 씁니다.")]
    public string confirmLabel = "";
    public string cancelLabel = "취소하기";

    [Header("그만하기")]
    [Tooltip("[그만하기]를 누르면 불러올 시작 화면 씬 이름입니다(게임 오버 창과 같은 값).")]
    public string titleSceneName = "Title";

    [Header("화면 (1920x1080 기준 px)")]
    [Tooltip("화면 전체를 덮는 막의 색입니다(검정 80%).")]
    public Color dimColor = new Color(0f, 0f, 0f, 0.8f);
    [Tooltip("다른 HUD/팝업(슬롯 교체 확인창 90 등)보다 위, 게임 오버 창(100)/로딩 화면(1000)보다 아래입니다.")]
    public int canvasSortingOrder = 95;

    [Header("문구 모양")]
    public int messageFontSize = 44;
    public Color messageColor = Color.white;
    [Tooltip("문구의 세로 위치입니다(화면 중앙 기준, 양수면 위로).")]
    public float messageOffsetY = 70f;

    [Header("버튼 모양 (색/모양/글자 색은 PopupStyle)")]
    public Vector2 buttonSize = new Vector2(220f, 80f);
    [Tooltip("같은 줄 버튼 사이 가로 간격입니다(윗줄 계속하기/설정/도감, 아랫줄 재시작하기/그만하기).")]
    public float buttonSpacing = 60f;
    [Tooltip("윗줄과 아랫줄 버튼 사이 세로 간격입니다.")]
    public float buttonRowSpacing = 30f;
    [Tooltip("윗줄 버튼의 세로 위치입니다(화면 중앙 기준, 음수면 아래로). 아랫줄은 그 아래에 붙습니다.")]
    public float buttonsOffsetY = -60f;
    public int buttonFontSize = 32;

    [Header("확인창 모양 (바탕/모서리/글자 색/버튼 색은 PopupStyle)")]
    public Vector2 confirmSize = new Vector2(620f, 260f);
    public int confirmMessageFontSize = 30;
    public Vector2 confirmButtonSize = new Vector2(200f, 64f);
    [Tooltip("확인창 두 버튼 사이 간격입니다.")]
    public float confirmButtonSpacing = 40f;
    [Tooltip("확인창 아래쪽 끝에서 버튼 아래쪽 끝까지의 거리입니다.")]
    public float confirmButtonBottomMargin = 28f;
    public int confirmButtonFontSize = 28;

    [Header("열릴 때 연출 (실제 시간 기준, 초)")]
    [Tooltip("검은 막이 짙어지는 시간입니다.")]
    [Min(0f)] public float dimFadeInDuration = 0.3f;
    [Tooltip("ESC를 누른 뒤 문구가 떠오르기 시작할 때까지의 시간입니다.")]
    [Min(0f)] public float messageDelay = 0.2f;
    [Tooltip("문구가 투명했다가 선명해지며 떠오르는 시간입니다.")]
    [Min(0f)] public float messageFadeInDuration = 0.3f;
    [Tooltip("ESC를 누른 뒤 버튼이 떠오르기 시작할 때까지의 시간입니다.")]
    [Min(0f)] public float buttonsDelay = 0.4f;
    [Tooltip("버튼이 투명했다가 원래 색으로 돌아오며 떠오르는 시간입니다.")]
    [Min(0f)] public float buttonsFadeInDuration = 0.25f;

    [Header("닫힐 때 연출 (실제 시간 기준, 초)")]
    [Tooltip("문구/버튼이 사라지며 가라앉고, 검은 막이 밝아지는 시간입니다. 다 밝아져야 게임이 다시 흐릅니다.")]
    [Min(0f)] public float closeDuration = 0.35f;

    [Header("떠오름 / 가라앉음")]
    [Tooltip("문구/버튼이 떠오를 때 아래에서 올라오는 거리, 사라질 때 가라앉는 거리입니다.")]
    public float riseDistance = 30f;

    // ---------------- 읽기 ----------------

    private static PauseMenuSettings cached;
    private static bool loaded;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlayStart()
    {
        cached = null;
        loaded = false;
    }

    // Resources/PauseMenuSettings 에셋을 읽습니다. 없으면 기본값으로 만든 임시 설정을 돌려줍니다(항상 null이 아님).
    public static PauseMenuSettings Get()
    {
        if (!loaded || cached == null)
        {
            loaded = true;
            cached = Resources.Load<PauseMenuSettings>(ResourcePath);
            if (cached == null)
            {
                Debug.Log("PauseMenuSettings: Assets/Resources/PauseMenuSettings 에셋이 없어 기본값으로 동작합니다.");
                cached = CreateInstance<PauseMenuSettings>();
            }
        }
        return cached;
    }
}
