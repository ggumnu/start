using UnityEngine;

// [R 꾹 누르기 재시작 설정 에셋]
// R 꾹 누르기 재시작(HoldRestart)의 키/시간/막 색 값을 모아둔 에셋입니다.
// Assets/Resources 폴더에 "HoldRestartSettings"라는 이름으로 만들어두면 자동으로 읽습니다. 에셋이 없으면 아래 기본값으로 동작합니다.
//
// 만들기: Project 창에서 Assets/Resources 폴더 우클릭 > Create > MobArchive > Hold Restart Settings, 이름을 HoldRestartSettings로.
[CreateAssetMenu(fileName = "HoldRestartSettings", menuName = "MobArchive/Hold Restart Settings")]
public class HoldRestartSettings : ScriptableObject
{
    public const string ResourcePath = "HoldRestartSettings";

    [Header("키 / 시간 (실제 시간 기준, 초)")]
    [Tooltip("이 키를 꾹 누르고 있으면 화면이 점점 검게 변하다가 재시작합니다.")]
    public KeyCode restartKey = KeyCode.R;
    [Tooltip("이 시간 동안 계속 누르고 있으면 재시작합니다.")]
    [Min(0.1f)] public float holdDuration = 2f;
    [Tooltip("중간에 키를 떼면, 완전히 검은 상태에서 다시 밝아지기까지 걸리는 시간입니다(덜 어두웠으면 그만큼 더 빨리 밝아짐).")]
    [Min(0.01f)] public float releaseFadeDuration = 0.3f;

    [Header("검은 막")]
    public Color overlayColor = Color.black;
    [Tooltip("어두워지는 곡선입니다. 1 = 일정한 속도, 클수록 처음엔 천천히, 끝에 가서 빠르게 어두워집니다" +
             "(장전하려고 R을 짧게 눌렀을 때 거의 티가 나지 않도록 - 기본 2).")]
    [Min(1f)] public float darkenExponent = 2f;
    [Tooltip("다른 HUD/팝업/ESC 중단 화면(95)보다 위, 게임 오버 창(100)/로딩 화면(1000)보다 아래입니다.")]
    public int canvasSortingOrder = 99;

    // ---------------- 읽기 ----------------

    private static HoldRestartSettings cached;
    private static bool loaded;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlayStart()
    {
        cached = null;
        loaded = false;
    }

    // Resources/HoldRestartSettings 에셋을 읽습니다. 없으면 기본값으로 만든 임시 설정을 돌려줍니다(항상 null이 아님).
    public static HoldRestartSettings Get()
    {
        if (!loaded || cached == null)
        {
            loaded = true;
            cached = Resources.Load<HoldRestartSettings>(ResourcePath);
            if (cached == null) cached = CreateInstance<HoldRestartSettings>();
        }
        return cached;
    }
}
