using UnityEngine;

// [플레이 통계 설정 (형빈 확정)]
// 플레이 통계(RunAnalytics)를 어디로 보낼지 정하는 설정입니다. Assets/Resources/PlayStatsSettings 에 두면 자동으로 읽습니다.
// 만들기: Project 창에서 Assets/Resources 폴더 우클릭 > Create > MobArchive > Play Stats Settings, 이름을 PlayStatsSettings로.
// 에셋이 없으면 기본값으로 동작합니다(전송 주소가 비어 있으므로 아무것도 보내지 않음).
//
// [보내는 것 / 보내지 않는 것]
// - 보내는 것: 게임이 만든 무작위 번호(플레이어 구분용), 게임 버전, 판 기록(시작 방식/결과/시간/레벨/사망 위치·원인/아이템), 도감 기록, 에러 내용
// - 보내지 않는 것: 이름, 계정, IP, PC 이름, 사용자 이름, 파일 경로(에러 내용에서도 지워서 보냄)
// - 게임은 보내기만 하고 서버에서 아무것도 받아 실행하지 않습니다.
[CreateAssetMenu(menuName = "MobArchive/Play Stats Settings", fileName = "PlayStatsSettings")]
public class PlayStatsSettings : ScriptableObject
{
    private const string ResourcePath = "PlayStatsSettings";

    [Header("전송")]
    [Tooltip("통계를 기록/전송할지입니다. 끄면 아무것도 기록하지 않고 보내지도 않습니다.")]
    public bool enabled = true;

    [Tooltip("구글 Apps Script 웹 앱 주소입니다(https://script.google.com/macros/s/.../exec). 비어 있으면 보내지 않습니다.")]
    public string endpointUrl = "";

    [Tooltip("Apps Script 쪽 TOKEN과 똑같아야 기록이 받아들여집니다(아무나 엉뚱한 기록을 보내는 것을 줄이는 용도).")]
    public string token = "mob-7c85583b9352d66b67de0f71";

    [Header("유니티 에디터")]
    [Tooltip("켜면 유니티 에디터에서 플레이한 판도 실제로 전송합니다(시트 연결 테스트용). 평소에는 꺼 두세요 - 테스트 기록이 통계에 섞입니다.")]
    public bool sendInEditor = false;

    [Tooltip("켜면 유니티 에디터에서 판이 끝날 때마다 보낼 내용을 Console에 보여줍니다(전송 여부와 상관없음).")]
    public bool logInEditor = true;

    [Header("제한")]
    [Tooltip("한 번 실행하는 동안 보낼 에러 기록의 최대 개수입니다(같은 에러는 한 번만).")]
    [Min(0)] public int maxErrorsPerSession = 10;

    [Tooltip("보내지 못하고 쌓아 둘 기록의 최대 개수입니다(인터넷이 안 될 때). 넘치면 오래된 것부터 버립니다.")]
    [Min(1)] public int maxQueuedRecords = 50;

    private static PlayStatsSettings cached;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlayStart()
    {
        cached = null;
    }

    public static PlayStatsSettings Get()
    {
        if (cached == null) cached = Resources.Load<PlayStatsSettings>(ResourcePath);
        if (cached == null) cached = CreateInstance<PlayStatsSettings>();
        return cached;
    }
}
