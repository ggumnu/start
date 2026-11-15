using UnityEngine;

// [업데이트 알림 설정 (형빈 확정)]
// 시작 화면에서 새 버전이 나왔는지 확인하는 업데이트 알림(UpdateNoticeUI)의 설정입니다.
// Assets/Resources/UpdateNoticeSettings 에 두면 자동으로 읽고, 없으면 아래 기본값으로 동작합니다(기본값에 배포 저장소 주소가 들어 있음).
// 만들기(문구/크기를 바꾸고 싶을 때만): Project 창에서 Assets/Resources 우클릭 > Create > MobArchive > Update Notice Settings, 이름을 UpdateNoticeSettings로.
//
// [안전 장치]
// - GitHub에서 최신 버전 번호(글자)만 읽고, 아무것도 내려받거나 실행하지 않습니다.
// - [다운로드 페이지 열기]는 서버가 알려준 주소가 아니라 이 설정에 적힌 주소(https://github.com/ 로 시작하는 것만)를 엽니다.
[CreateAssetMenu(menuName = "MobArchive/Update Notice Settings", fileName = "UpdateNoticeSettings")]
public class UpdateNoticeSettings : ScriptableObject
{
    private const string ResourcePath = "UpdateNoticeSettings";

    [Header("확인")]
    [Tooltip("업데이트 확인을 할지입니다. 끄면 GitHub에 아무것도 묻지 않습니다.")]
    public bool enabled = true;

    [Tooltip("최신 Release 정보를 묻는 GitHub 주소입니다(버전 번호만 읽음).")]
    public string latestReleaseApiUrl = "https://api.github.com/repos/ggumnu/Reisa_archive-release/releases/latest";

    [Tooltip("[다운로드 페이지 열기]를 누르면 브라우저로 여는 주소입니다. https://github.com/ 으로 시작해야 열립니다.")]
    public string releasePageUrl = "https://github.com/ggumnu/Reisa_archive-release/releases/latest";

    [Tooltip("GitHub가 이 시간(초) 안에 답하지 않으면 확인을 그만둡니다(창 없이 조용히 넘어감).")]
    [Min(1)] public int timeoutSeconds = 8;

    [Header("문구 ({0} = 새 버전, {1} = 지금 버전)")]
    [TextArea(2, 4)]
    public string message = "새 버전 {0}이(가) 나왔습니다!\n<size=80%>지금 버전: {1}</size>";
    public string openLabel = "다운로드 페이지 열기";
    public string laterLabel = "나중에";

    [Header("창 모양 (1920x1080 기준 px - 바탕색/모서리/글자 색/버튼 색은 PopupStyle)")]
    public Vector2 panelSize = new Vector2(560f, 230f);
    public int messageFontSize = 26;
    public Vector2 openButtonSize = new Vector2(240f, 46f);
    public Vector2 laterButtonSize = new Vector2(140f, 46f);
    public float buttonSpacing = 30f;
    public float buttonBottomMargin = 24f;
    public int buttonFontSize = 22;
    [Tooltip("시작 화면(0)과 도감/설정 창보다 위에 그립니다.")]
    public int canvasSortingOrder = 150;

    [Header("에디터 테스트")]
    [Tooltip("켜면 유니티 에디터에서 버전과 상관없이 창을 띄웁니다(모양 확인용). 빌드에는 영향이 없습니다.")]
    public bool forceShowInEditor = false;

    private static UpdateNoticeSettings cached;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlayStart()
    {
        cached = null;
    }

    public static UpdateNoticeSettings Get()
    {
        if (cached == null) cached = Resources.Load<UpdateNoticeSettings>(ResourcePath);
        if (cached == null) cached = CreateInstance<UpdateNoticeSettings>();
        return cached;
    }
}
