using UnityEngine;

// [헬기 퇴각(다음 레벨 이동) 연출 설정 에셋]
// 헬기 그림자 / 바닥 먼지 물결 / 밧줄 사다리(HeliExtraction)의 그림, 재질, 시간, 흔들림 값을 모아둔 에셋입니다.
// Assets/Resources 폴더에 "HeliExtractionSettings"라는 이름으로 두면 자동으로 읽습니다(에셋이 없으면 아래 기본값 - 그림/재질은 비어 있음).
//
// 만들기: Project 창에서 Assets/Resources 폴더 우클릭 > Create > MobArchive > Heli Extraction Settings, 이름을 HeliExtractionSettings로.
[CreateAssetMenu(fileName = "HeliExtractionSettings", menuName = "MobArchive/Heli Extraction Settings")]
public class HeliExtractionSettings : ScriptableObject
{
    public const string ResourcePath = "HeliExtractionSettings";

    [Header("그림 / 재질")]
    [Tooltip("헬기 그림자 그림들입니다(위에서 본 모습, 기수 = 그림 오른쪽). 순서대로 반복해서 로터가 도는 것처럼 보입니다.")]
    public Texture2D[] shadowFrames;
    [Tooltip("MobArchive/HeliUnlit 셰이더 재질(Cutoff 0, ZWrite Off)입니다.")]
    public Material shadowMaterial;
    [Tooltip("사다리 한 칸 그림입니다(세로로 이어 붙여도 이음매가 없는 그림).")]
    public Texture2D ladderTexture;
    [Tooltip("MobArchive/HeliUnlit 셰이더 재질(Cutoff 0.5, ZWrite On)입니다.")]
    public Material ladderMaterial;
    [Tooltip("MobArchive/RoomWave 셰이더 재질입니다(90번 물결과 같은 셰이더 - 굴절 세기만 재질에서 조절).")]
    public Material dustMaterial;

    [Header("헬기 그림자")]
    [Tooltip("그림자 크기(m, 한 변)입니다. 그림 전체(로터 원판 포함)가 이 크기의 정사각형에 그려집니다.")]
    [Min(0.1f)] public float shadowSize = 7f;
    [Tooltip("그림자 진하기(0~1)입니다.")]
    [Range(0f, 1f)] public float shadowOpacity = 0.5f;
    [Tooltip("로터 그림 바뀌는 속도(초당 장 수)입니다. 3장 기준 24면 초당 8바퀴 남짓 도는 느낌입니다.")]
    [Min(1f)] public float shadowFps = 24f;
    [Tooltip("헬기가 날아오는 방향(월드 기준, 수평)입니다. (-1, 0) = X-쪽에서 날아와 X+쪽을 향해 멈춤. 그림자 기수도 이 진행 방향을 봅니다.")]
    public Vector2 flyInFrom = new Vector2(-1f, 0f);
    [Tooltip("날아 들어오기 시작하는 거리(m)입니다(화면 밖에서 시작하도록 충분히 멀게).")]
    [Min(0f)] public float flyInDistance = 18f;
    [Tooltip("날아 들어오는 시간(초)입니다. 끝으로 갈수록 느려지며 멈춥니다.")]
    [Min(0.05f)] public float flyInDuration = 2.2f;
    [Tooltip("멈춘 뒤 제자리에서 살짝 흔들리는 크기(m)입니다(호버링).")]
    [Min(0f)] public float hoverDrift = 0.15f;
    [Tooltip("호버링 흔들림 한 번의 시간(초)입니다.")]
    [Min(0.1f)] public float hoverPeriod = 2.6f;
    [Tooltip("바닥 위로 띄우는 높이(m)입니다(바닥과 겹쳐 깜빡이지 않도록).")]
    public float groundOffset = 0.03f;

    [Header("바닥 먼지 물결 (프로펠러 바람)")]
    [Tooltip("먼지 물결 색입니다(갈색, 알파 = 진하기).")]
    [ColorUsage(true, true)] public Color dustColor = new Color(0.55f, 0.42f, 0.28f, 0.55f);
    [Tooltip("헬기가 도착하기 이 시간(초) 전부터 먼지 물결이 퍼지기 시작합니다.")]
    [Min(0f)] public float dustStartBeforeArrive = 0.6f;
    [Tooltip("먼지 물결이 새로 퍼지는 간격(초)입니다.")]
    [Min(0.05f)] public float dustInterval = 0.45f;
    [Tooltip("물결 하나가 최대 반경까지 퍼지는 시간(초)입니다.")]
    [Min(0.05f)] public float dustDuration = 1.4f;
    [Tooltip("물결 최대 반경(m)입니다. 방 밖(벽 너머)은 잘려서 그려지지 않습니다.")]
    [Min(0.1f)] public float dustRadius = 6f;
    [Tooltip("물결 고리 두께(m)입니다.")]
    [Min(0.01f)] public float dustRingWidth = 0.6f;
    [Tooltip("고리 뒤를 따라오는 잔물결 수(0~3)입니다.")]
    [Range(0, 3)] public int dustRippleCount = 2;
    [Tooltip("잔물결 간격(m)입니다.")]
    [Min(0.01f)] public float dustRippleSpacing = 0.5f;
    [Tooltip("잔물결이 뒤로 갈수록 옅어지는 정도(0~1)입니다.")]
    [Range(0f, 1f)] public float dustRippleFalloff = 0.5f;
    [Tooltip("물결이 이만큼(0~1) 퍼진 뒤부터 서서히 사라집니다.")]
    [Range(0f, 1f)] public float dustFadeStart = 0.35f;

    [Header("사다리 - 모양")]
    [Tooltip("헬기 그림자 중심에서 사다리가 매달린 자리까지의 거리(m, 헬기 기준: X = 앞쪽, Y = 왼쪽)입니다. 플레이어가 잡으러 오는 자리입니다.")]
    public Vector2 ladderOffset = new Vector2(0f, 0f);
    [Tooltip("사다리 윗끝(헬기)의 높이(m, 바닥 기준)입니다. 화면 위로 벗어날 만큼 높게 둡니다.")]
    [Min(1f)] public float ladderTopHeight = 9f;
    [Tooltip("다 내려왔을 때 사다리 아래 끝과 바닥 사이 높이(m)입니다.")]
    [Min(0f)] public float ladderBottomClearance = 0.25f;
    [Tooltip("사다리 폭(m)입니다.")]
    [Min(0.05f)] public float ladderWidth = 0.55f;
    [Tooltip("사다리 그림 한 칸이 차지하는 길이(m)입니다(작을수록 발판이 촘촘).")]
    [Min(0.05f)] public float ladderTileLength = 0.55f;
    [Tooltip("흔들림을 계산하는 마디 수입니다(많을수록 부드럽게 휘지만 조금 무거워짐).")]
    [Range(4, 60)] public int ladderSegments = 24;

    [Header("사다리 - 떨어짐 / 흔들림")]
    [Tooltip("헬기가 멈춘 뒤 사다리를 떨어뜨리기까지의 시간(초)입니다.")]
    [Min(0f)] public float ladderDropDelay = 0.6f;
    [Tooltip("중력 가속도(m/s²)입니다. 클수록 빨리 떨어지고 다 풀렸을 때 반동이 셉니다.")]
    [Min(0.1f)] public float ladderGravity = 16f;
    [Tooltip("다 풀리는 순간 사다리가 늘어났다가 튕겨 오르는 정도(0 = 늘어나지 않음, 클수록 고무줄처럼 크게 출렁임).")]
    [Range(0f, 0.9f)] public float ladderStretch = 0.25f;
    [Tooltip("흔들림이 줄어드는 정도(0~1, 프레임당 남는 속도 비율 - 1에 가까울수록 오래 흔들림).")]
    [Range(0.9f, 1f)] public float ladderDamping = 0.985f;
    [Tooltip("떨어지기 시작할 때 아랫부분을 옆으로 살짝 던지는 세기(m/s)입니다(곧게 떨어지지 않고 휘청이도록).")]
    [Min(0f)] public float ladderThrowKick = 1.6f;
    [Tooltip("프로펠러 바람으로 계속 살랑이는 세기(m/s², 아래로 갈수록 크게 받음)입니다.")]
    [Min(0f)] public float ladderSwayStrength = 1.4f;
    [Tooltip("살랑이는 빠르기(초당 흔들림 횟수)입니다.")]
    [Min(0.01f)] public float ladderSwaySpeed = 0.9f;
    [Tooltip("사다리가 다 내려와 흔들림이 어느 정도 가라앉기까지 기다리는 시간(초)입니다. 이 뒤부터 F로 탈 수 있습니다.")]
    [Min(0f)] public float ladderSettleTime = 1.2f;

    [Header("퇴각 - 걸어가기 / 점프 / 매달리기")]
    [Tooltip("확인창 [예] 뒤 플레이어가 걸어가 멈추는 자리: 사다리 아래 끝에서 플레이어 쪽으로 이만큼(m) 떨어진 곳입니다.")]
    [Min(0f)] public float approachDistance = 0.45f;
    [Tooltip("이 시간(초) 안에 사다리 앞에 도착하지 못하면(막힌 경우) 그 자리에서 바로 점프합니다.")]
    [Min(0.5f)] public float approachTimeout = 4f;
    [Tooltip("걸어가는 동안 몸을 돌리는 속도입니다.")]
    [Min(0.1f)] public float approachTurnSpeed = 12f;
    [Tooltip("점프해서 사다리를 붙잡기까지의 시간(초)입니다.")]
    [Min(0.05f)] public float jumpDuration = 0.45f;
    [Tooltip("점프 포물선의 추가 높이(m)입니다.")]
    [Min(0f)] public float jumpArcHeight = 0.35f;
    [Tooltip("붙잡는 사다리 지점의 높이(m, 바닥 기준)입니다.")]
    [Min(0.1f)] public float grabHeight = 0.9f;
    [Tooltip("붙잡은 지점에서 플레이어 발밑까지의 거리(m)입니다(매달렸을 때 손 위치가 사다리에 오도록 조절).")]
    [Min(0f)] public float hangOffset = 0.6f;
    [Tooltip("켜면 매달린 동안 게임 카메라 쪽(화면 정면)을 봅니다. 끄면 사다리 쪽을 봅니다.")]
    public bool faceCameraWhileHanging = true;
    [Tooltip("붙잡는 순간 사다리를 아래로 출렁이게 하는 세기(m/s)입니다.")]
    [Min(0f)] public float grabJolt = 2f;
    [Tooltip("매달린 자세에 쓸 Animator 상태(UpperBody 레이어)와 Bool입니다(기존 획득 모션 재사용).")]
    public string hangStateName = "ItemPickup";
    public string hangBool = "IsPickingUp";
    [Tooltip("매달린 자세로 섞여 들어가는 시간(초)입니다.")]
    [Min(0f)] public float hangBlendIn = 0.2f;

    [Header("퇴각 - 끌어올리기 / 화면")]
    [Tooltip("붙잡은 뒤 끌어올리기 시작하기까지의 시간(초)입니다(반동을 잠깐 보여줌).")]
    [Min(0f)] public float liftDelay = 0.5f;
    [Tooltip("끌어올리는 가속도(m/s²)입니다.")]
    [Min(0.1f)] public float liftAcceleration = 5f;
    [Tooltip("끌어올리는 최고 속도(m/s)입니다.")]
    [Min(0.1f)] public float liftMaxSpeed = 9f;
    [Tooltip("헬기 그림자가 진행 방향으로 떠나는 가속도(m/s²)입니다(0 = 제자리). 사다리가 다 걷힌 뒤에 출발합니다.")]
    [Min(0f)] public float departAcceleration = 2.5f;
    [Tooltip("사다리가 헬기까지 다 걷힌 뒤, 헬기가 출발하기까지 더 기다리는 시간(초)입니다(퇴각/도착 공통).")]
    [Min(0f)] public float departAfterLift = 0.3f;
    [Tooltip("끌어올리기 시작하고 이 시간(초) 뒤에 화면이 검게 변하기 시작합니다.")]
    [Min(0f)] public float fadeStartAfterLift = 0.9f;
    [Tooltip("검게 변하는 시간(초)입니다. 완전히 검어지면 다음 레벨로 넘어갑니다(로딩 화면).")]
    [Min(0.05f)] public float fadeDuration = 0.8f;
    [Tooltip("퇴각 중 이 키 중 하나를 누르면 바로 검게 하고 다음 레벨로 넘어갑니다.")]
    public KeyCode[] skipKeys = { KeyCode.Space, KeyCode.Escape, KeyCode.Return, KeyCode.KeypadEnter };
    [Tooltip("건너뛸 때 검게 변하는 시간(초)입니다.")]
    [Min(0f)] public float skipFadeDuration = 0.15f;
    [Tooltip("검은 막 정렬 순서(게임 오버 창 100보다 위, 로딩 화면 1000보다 아래)입니다.")]
    public int overlaySortingOrder = 998;

    [Header("퇴각 - 총을 등에 메기")]
    [Tooltip("손에 든 총 오브젝트 이름입니다(오른손 뼈 아래). 못 찾으면 오른손 뼈 아래 첫 번째 그림 있는 오브젝트를 씁니다.")]
    public string gunObjectName = "Reisa_gun_Fixed";
    [Tooltip("등에 멘 총 위치: 가슴 뼈 기준, 캐릭터 방향으로 X = 오른쪽, Y = 위, Z = 앞(음수 = 등 뒤) (m). Play 중에 바꾸면 바로 보입니다.")]
    public Vector3 gunBackOffset = new Vector3(0f, 0f, -0.12f);
    [Tooltip("등에 멘 총 회전(캐릭터 방향 기준 오일러 각)입니다. 기본값 = 총구가 오른쪽 어깨 위로 비스듬히. Play 중에 바꾸면 바로 보입니다.")]
    public Vector3 gunBackEuler = new Vector3(49.6f, -89.8f, 179.5f);

    [Header("도착 - 다음 레벨에서 사다리 타고 내려오기")]
    [Tooltip("도착 연출을 시작할 때 사다리가 원래 위치보다 이만큼(m) 위에 있습니다(매달린 플레이어가 화면 위에서 내려오도록 충분히 높게).")]
    [Min(0f)] public float arrivalStartLift = 7f;
    [Tooltip("사다리가 내려오는 시간(초)입니다. 끝으로 갈수록 느려지며 멈춥니다.")]
    [Min(0.1f)] public float descendDuration = 2.2f;
    [Tooltip("내려오는 동안 사다리가 출발/멈춤에 반응해 흔들리는 정도입니다(0 = 반동 없이 헬기와 딱 붙어 움직임, 1 = 실제 관성 그대로). 높일수록 멈출 때 출렁임이 커집니다.")]
    [Range(0f, 1f)] public float arrivalLadderInertia = 0.35f;
    [Tooltip("다 내려온 뒤 손을 놓기까지 잠깐 매달려 있는 시간(초)입니다.")]
    [Min(0f)] public float letGoDelay = 0.25f;
    [Tooltip("손을 놓은 뒤 바닥까지 떨어지는 중력(m/s²)입니다.")]
    [Min(0.1f)] public float dropGravity = 14f;
    [Tooltip("착지한 뒤 매달린 자세 -> 대기 자세로 섞여 돌아가는 시간(초)입니다.")]
    [Min(0f)] public float landBlend = 0.3f;
    [Tooltip("착지한 뒤 이 시간(초)이 지나면 조작이 풀리고 HUD가 나타납니다.")]
    [Min(0f)] public float controlReturnDelay = 0.2f;
    [Tooltip("착지한 뒤 사다리가 다시 올라가기 시작하기까지의 시간(초)입니다(n.5 레벨처럼 다음 레벨용 헬기가 남는 경우는 올라가지 않음).")]
    [Min(0f)] public float leaveDelay = 0.4f;
    [Tooltip("헬기가 출발한 뒤(사다리가 다 걷힌 뒤) 그림자가 사라지는 시간(초)입니다. 다 사라지면 도착용 헬기는 지워집니다.")]
    [Min(0.1f)] public float leaveDuration = 2.5f;
    [Tooltip("HUD가 나타나는 시간(초)입니다.")]
    [Min(0f)] public float arrivalHudFadeIn = 0.4f;

    // ---------------- 읽기 ----------------

    private static HeliExtractionSettings cached;
    private static bool loaded;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlayStart()
    {
        cached = null;
        loaded = false;
    }

    // Resources/HeliExtractionSettings 에셋을 읽습니다. 없으면 기본값으로 만든 임시 설정을 돌려줍니다(항상 null이 아님).
    public static HeliExtractionSettings Get()
    {
        if (!loaded || cached == null)
        {
            loaded = true;
            cached = Resources.Load<HeliExtractionSettings>(ResourcePath);
            if (cached == null) cached = CreateInstance<HeliExtractionSettings>();
        }
        return cached;
    }
}
