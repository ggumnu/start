using UnityEngine;

// [입장 컷씬 설정 에셋]
// 게임을 처음 시작할 때 나오는 입장 컷씬(IntroCutscene)의 위치/시간/키 값을 모아둔 에셋입니다.
// Assets/Resources 폴더에 "IntroCutsceneSettings"라는 이름으로 만들어두면 자동으로 읽습니다. 에셋이 없으면 아래 기본값으로 동작합니다.
//
// 만들기: Project 창에서 Assets/Resources 폴더 우클릭 > Create > MobArchive > Intro Cutscene Settings, 이름을 IntroCutsceneSettings로.
[CreateAssetMenu(fileName = "IntroCutsceneSettings", menuName = "MobArchive/Intro Cutscene Settings")]
public class IntroCutsceneSettings : ScriptableObject
{
    public const string ResourcePath = "IntroCutsceneSettings";

    [Header("테스트")]
    [Tooltip("켜면 시작 화면을 거치지 않고 LevelBuilder 씬에서 바로 Play 해도 컷씬이 나옵니다(Play를 누른 뒤 처음 열린 씬에서 한 번만 - " +
             "재시작/다음 레벨에서는 나오지 않음). 에디터에서만 적용되고, 실제 빌드(exe)에서는 이 값과 상관없이 시작 화면의 [게임 시작]으로만 나옵니다.")]
    public bool editorAlwaysPlay = false;

    [Header("컷씬 없이 시작할 때")]
    [Tooltip("켜면 컷씬 없이 레벨 1을 시작할 때(재시작 포함) 플레이어가 카메라 쪽(화면 정면)을 바라보고 서 있습니다. " +
             "끄면 예전처럼 월드 Z+ 쪽을 봅니다. 다음 레벨로 넘어갈 때는 바꾸지 않습니다.")]
    public bool faceCameraOnStart = true;

    [Header("화면 밝아짐 / HUD")]
    [Tooltip("검은 화면에서 점점 밝아지는 시간(초, 실제 시간)입니다.")]
    [Min(0f)] public float introFadeIn = 1.2f;
    [Tooltip("검은 화면 뒤에서 먼저 걷기 시작하고, 이 시간(초)이 지나면 밝아지기 시작합니다(화면이 보이는 순간 이미 걷고 있도록). " +
             "로딩 화면이 사라지는 시간보다 짧으면 로딩 화면이 다 사라진 순간부터 밝아집니다.")]
    [Min(0f)] public float revealAfterWalkStart = 0.5f;
    [Tooltip("컷씬이 끝난 뒤 HUD(체력/미니맵 등)가 나타나는 시간(초)입니다. 컷씬 중에는 HUD가 숨겨집니다.")]
    [Min(0f)] public float hudFadeIn = 0.5f;

    [Header("걸어오기")]
    [Tooltip("걷기를 시작하는 위치입니다. 방 정중앙(원래 시작 위치) 기준으로 X(월드 X축), Y칸 = Z(월드 Z축) 거리(m)입니다. " +
             "칸 크기의 절반(5m)보다 멀면 옆 방으로 넘어간 것으로 처리될 수 있으니 4.5m 이내를 권장합니다.")]
    public Vector2 walkStartOffset = new Vector2(-4f, 0f);
    [Tooltip("걷는 동안 몸을 걷는 방향으로 돌리는 속도입니다.")]
    [Min(0.1f)] public float turnSpeed = 12f;
    [Tooltip("정중앙까지 이 거리(m) 안으로 들어오면 걸음이 점점 느려집니다(도착 순간 걷기 -> 멈춤이 뚝 끊기지 않도록). 0이면 감속 없음.")]
    [Min(0f)] public float arriveSlowDistance = 0.6f;
    [Tooltip("감속할 때 가장 느린 속도 비율(0~1)입니다. 너무 낮으면 도착 직전에 거의 멈춘 것처럼 보입니다.")]
    [Range(0.05f, 1f)] public float arriveMinSpeed = 0.25f;
    [Tooltip("이 시간(초) 안에 도착하지 못하면(무언가에 막힌 경우 등) 정중앙으로 바로 옮깁니다.")]
    [Min(0.5f)] public float walkTimeout = 8f;
    [Tooltip("정중앙에 도착한 뒤 장전을 시작하기까지 잠깐 멈추는 시간(초)입니다.")]
    [Min(0f)] public float arriveHold = 0.15f;

    [Header("모션 사이 섞임 (블렌딩)")]
    [Tooltip("도착한 뒤 걷기 -> 대기 다리 자세로 서서히 바뀌는 시간(초)입니다.")]
    [Min(0f)] public float stopBlendDuration = 0.35f;
    [Tooltip("대기 -> 장전 모션으로 섞여 들어가는 시간(초)입니다(Animator 기본값 0.1초 대신).")]
    [Min(0f)] public float reloadBlendIn = 0.35f;
    [Tooltip("장전 -> 획득 모션으로 섞여 들어가는 시간(초)입니다(Animator 기본값 0.1초 대신).")]
    [Min(0f)] public float pickupBlendIn = 0.35f;

    [Header("장전")]
    [Tooltip("장전 동작 시간(초)입니다. 장전 클립(PlayerShooting의 Reload Clip)을 이 시간에 맞춰 재생합니다. 탄약은 바뀌지 않습니다.")]
    [Min(0.1f)] public float reloadDuration = 1.5f;

    [Header("획득 포즈")]
    [Tooltip("장전이 끝나면 카메라 쪽을 보며 획득 모션/표정을 시작하고, 이 시간(초) 뒤 대기 모션으로 돌아가기 시작합니다.")]
    [Min(0f)] public float pickupHold = 2f;
    [Tooltip("획득 포즈를 시작할 때 몸을 첫 장면 카메라의 최종 위치(Shot End Side Angle) 쪽으로 돌리는 시간(초)입니다. " +
             "Shot End Side Angle이 0이면 이미 정면이라 거의 돌지 않습니다.")]
    [Min(0.05f)] public float faceTurnDuration = 0.6f;
    [Tooltip("대기 모션으로 돌아가면서 몸을 게임 카메라 쪽(컷씬 없이 시작할 때 방향)으로 돌리는 시간(초)입니다.")]
    [Min(0.05f)] public float returnTurnDuration = 1.2f;
    [Tooltip("획득 포즈에서 대기 모션으로 섞여 돌아가는 시간(초)입니다. 클수록 천천히 돌아갑니다.")]
    [Min(0f)] public float returnBlendDuration = 0.6f;

    [Header("카메라 - 첫 장면 (걸어오기 ~ 획득 포즈, 플레이어를 따라감)")]
    [Tooltip("걷기 시작할 때 카메라와 플레이어 사이 거리(m)입니다. 걸어오는 동안 아래 Shot Distance까지 점점 가까워집니다.")]
    [Min(0.1f)] public float shotStartDistance = 2.6f;
    [Tooltip("정중앙에 도착했을 때(장전/획득 포즈) 카메라와 플레이어 사이 거리(m)입니다.")]
    [Min(0.1f)] public float shotDistance = 1.1f;
    [Tooltip("카메라 높이(m, 바닥 기준)입니다. 낮을수록 올려다보는 시점이 됩니다.")]
    public float shotHeight = 0.35f;
    [Tooltip("사선 각도(도)입니다. 0이면 플레이어가 걸어오는 방향의 정면, 값이 클수록 옆에서 비스듬히 찍습니다. " +
             "음수로 하면 반대쪽 옆에서 찍습니다.")]
    public float shotSideAngle = 35f;
    [Tooltip("걷기 끝부분부터 카메라가 플레이어 주위를 둥글게 돌아 이 각도로 옵니다(0 = 정면). 장전/획득 포즈는 이 각도에서 찍습니다.")]
    public float shotEndSideAngle = 0f;
    [Tooltip("걸어온 거리의 이 비율(0~1)부터 사선 -> 정면으로 돌기 시작합니다. 0.6이면 60% 걸었을 때부터.")]
    [Range(0f, 1f)] public float frontTurnStartProgress = 0.6f;
    [Tooltip("사선 -> 정면으로 도는 데 걸리는 시간(초)입니다. 도착 후 장전하는 동안에도 이어서 부드럽게 돕니다.")]
    [Min(0.05f)] public float frontTurnDuration = 1.6f;
    [Tooltip("카메라가 바라보는 플레이어 높이(m, 발 기준)입니다.")]
    public float lookHeight = 0.3f;
    [Tooltip("카메라가 플레이어를 따라 움직이는 부드러움입니다(클수록 즉각, 작을수록 살짝 늦게 따라옴).")]
    [Min(0.1f)] public float shotFollowSpeed = 6f;

    [Header("카메라 - 나선형 상승 (게임 시점으로)")]
    [Tooltip("획득 모션을 시작하고 이 시간(초) 뒤에 카메라가 올라가기 시작합니다.")]
    [Min(0f)] public float cameraRiseDelay = 1f;
    [Tooltip("나선형으로 돌며 게임 시점에 도착하기까지의 시간(초)입니다.")]
    [Min(0.1f)] public float spiralDuration = 3f;
    [Tooltip("도착하기 전까지 플레이어 주위를 추가로 도는 바퀴 수입니다. 0이면 가장 짧은 쪽으로만 돕니다.")]
    [Min(0f)] public float spiralTurns = 1f;
    [Tooltip("켜면 위에서 내려다봤을 때 시계 방향으로, 끄면 반시계 방향으로 돕니다.")]
    public bool spiralClockwise = true;

    [Header("Animator (SD_Player 기준 이름 - 바꾸지 않았다면 그대로)")]
    public string reloadBool = "IsReloading";
    public string reloadSpeedFloat = "ReloadSpeed";
    public string pickupTrigger = "ItemPickup";
    public string pickupBool = "IsPickingUp";
    [Tooltip("장전/획득 상태 이름입니다(UpperBody 레이어). 정해진 시간만큼 섞여 들어가기 위해 직접 지정해 넘어갑니다.")]
    public string reloadStateName = "Reloading";
    public string pickupStateName = "ItemPickup";
    [Tooltip("획득 포즈에서 돌아갈 대기 상태 이름과 그 레이어입니다.")]
    public string idleStateName = "NonAimIdle";
    public string upperBodyLayerName = "UpperBody";

    [Header("획득 포즈 - 전신 '짜잔' 모션 (SD_IntroPose)")]
    [Tooltip("켜면 획득 포즈로 전신 '짜잔' 모션(살짝 점프 -> 다리 벌려 착지 -> 총을 위로)을 씁니다. 끄면 예전 획득 모션(ItemPickup, 상체만)을 씁니다.")]
    public bool useFullBodyPose = true;
    [Tooltip("전신 포즈 상태 이름입니다. 다리/몸통(Locomotion 레이어)과 팔/머리(UpperBody 레이어)에 같은 이름으로 들어 있습니다.")]
    public string fullBodyPoseStateName = "IntroPose";
    [Tooltip("다리/몸통 레이어 이름과, 포즈가 끝나면 돌아갈 그 레이어의 대기(이동) 상태 이름입니다.")]
    public string baseLayerName = "Locomotion";
    public string baseIdleStateName = "AimLocomotion";

    [Header("건너뛰기")]
    [Tooltip("컷씬 중 이 키 중 하나를 누르면 화면이 잠깐 검게 변했다가 정중앙에서 바로 시작합니다.")]
    public KeyCode[] skipKeys = { KeyCode.Space, KeyCode.Escape, KeyCode.Return, KeyCode.KeypadEnter };
    [Tooltip("검게 변하는 시간(초, 실제 시간)입니다.")]
    [Min(0f)] public float skipFadeIn = 0.12f;
    [Tooltip("완전히 검은 상태로 머무는 시간(초)입니다.")]
    [Min(0f)] public float skipHold = 0.1f;
    [Tooltip("다시 밝아지는 시간(초)입니다.")]
    [Min(0f)] public float skipFadeOut = 0.3f;

    [Header("검은 막")]
    public Color overlayColor = Color.black;
    [Tooltip("게임 오버 창(100)보다 위, 로딩 화면(1000)보다 아래입니다.")]
    public int canvasSortingOrder = 998;

    // ---------------- 읽기 ----------------

    private static IntroCutsceneSettings cached;
    private static bool loaded;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlayStart()
    {
        cached = null;
        loaded = false;
    }

    // Resources/IntroCutsceneSettings 에셋을 읽습니다. 없으면 기본값으로 만든 임시 설정을 돌려줍니다(항상 null이 아님).
    public static IntroCutsceneSettings Get()
    {
        if (!loaded || cached == null)
        {
            loaded = true;
            cached = Resources.Load<IntroCutsceneSettings>(ResourcePath);
            if (cached == null) cached = CreateInstance<IntroCutsceneSettings>();
        }
        return cached;
    }
}
