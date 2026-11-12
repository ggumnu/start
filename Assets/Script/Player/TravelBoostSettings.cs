using UnityEngine;

// [비전투 이동 가속 설정 에셋] PlayerTravelBoost가 읽는 값입니다.
// Assets/Resources 폴더에 "TravelBoostSettings"라는 이름으로 만들어두면 자동으로 읽고, 없으면 아래 기본값으로 동작합니다.
// 만들기: Project 창에서 Assets/Resources 폴더 우클릭 > Create > MobArchive > Travel Boost Settings, 이름을 TravelBoostSettings로.
[CreateAssetMenu(fileName = "TravelBoostSettings", menuName = "MobArchive/Travel Boost Settings")]
public class TravelBoostSettings : ScriptableObject
{
    public const string ResourcePath = "TravelBoostSettings";

    [Tooltip("끄면 비전투 가속이 전혀 걸리지 않습니다.")]
    public bool enableBoost = true;

    [Header("시간 (초)")]
    [Tooltip("전투가 끝난 상태(비전투 방에 들어왔거나 지금 방을 클리어한 순간)가 된 뒤 이 시간이 지나야 가속이 시작될 수 있습니다." +
             " 비전투 방끼리 옮겨 다닐 때는 다시 기다리지 않습니다.")]
    [Min(0f)] public float combatFreeDelay = 1f;
    [Tooltip("걷는 동안 가속이 0%에서 최대치까지 오르는 데 걸리는 시간입니다(일정한 속도로 오름).")]
    [Min(0.01f)] public float rampDuration = 2f;

    [Header("속도")]
    [Tooltip("최대 가속 때 늘어나는 비율입니다(0.5 = 50% = 현재 이동 속도 x1.5).")]
    [Min(0f)] public float bonusRatio = 0.5f;
    [Tooltip("가속이 붙은 최종 이동 속도의 상한입니다. 아이템 이동 속도 상한(PlayerStats의 Move Speed Cap, 4)보다 높게 두어" +
             " 아이템으로 아무리 빨라져도 비전투 가속보다는 느리게 합니다.")]
    [Min(0f)] public float boostedSpeedCap = 5f;

    [Header("뛰기 애니메이션 (SD_Run)")]
    [Tooltip("가속이 이 비율 이상일 때만 뛰기 애니메이션을 씁니다(1 = 최대 가속에 이른 뒤에만, 가속이 오르는 동안은 평소 걷기).")]
    [Range(0f, 1f)] public float runAnimStartRatio = 1f;
    [Tooltip("뛰기 애니메이션이 1배속으로 재생되는 이동 속도입니다. 실제 이동 속도 / 이 값 = 재생 속도(RunSpeed). 발이 미끄러져 보이면 낮추고, 너무 바쁘면 높입니다.")]
    [Min(0.1f)] public float runAnimReferenceSpeed = 2f;
    [Tooltip("뛰기 애니메이션 재생 속도의 최솟값/최댓값입니다.")]
    [Min(0.05f)] public float runAnimSpeedMin = 0.8f;
    [Min(0.05f)] public float runAnimSpeedMax = 2f;
    [Tooltip("이동 입력이 이 시간보다 오래 끊겨야 뛰기 애니메이션을 멈춥니다(키를 바꿔 누르는 순간 대기 자세로 튀지 않도록).")]
    [Min(0f)] public float runAnimStopGraceTime = 0.1f;
    [Tooltip("뛰는 동안 플레이어 프리팹의 MeshRotationOffset Y 회전을 이 값으로 바꿉니다(평소 -20은 Mixamo 조준/걷기 모션용 보정, SD_Run은 정면 기준이라 0)." +
             " 멈추면 원래 값으로 돌아갑니다.")]
    public float runMeshYaw = 0f;
    [Tooltip("MeshRotationOffset 회전이 바뀌는 데 걸리는 시간(초)입니다. 애니메이션 전환 시간(0.15초)과 맞춰 두었습니다.")]
    [Min(0.01f)] public float runMeshYawBlendTime = 0.15f;

    [Header("뛰기 흙먼지 (PlayerRunDust)")]
    [Tooltip("발 뒤에 일어나는 흙먼지 이펙트 프리팹입니다(메뉴 MobArchive/VFX/뛰기 흙먼지 이펙트 만들기가 VFX_RunDust를 만들어 넣음). 비우면 먼지가 나오지 않습니다.")]
    public GameObject runDustPrefab;
    [Tooltip("먼지가 나오는 간격(초)입니다. 발 박자와 상관없이 이 간격마다 왼발/오른발 뒤를 번갈아 가며 우다다다 튀어나옵니다(형빈 확정)." +
             " 0이면 예전처럼 발이 뒤로 차는 순간(아래 Run Dust Phase)에만 나옵니다.")]
    [Min(0f)] public float runDustInterval = 0.07f;
    [Tooltip("간격의 무작위 흔들림 비율입니다(0.3 = 간격의 ±30%). 너무 기계적으로 똑같이 나오지 않게 합니다.")]
    [Range(0f, 0.9f)] public float runDustIntervalJitter = 0.3f;
    [Tooltip("먼지 자리의 좌우 무작위 흔들림(m)입니다.")]
    [Min(0f)] public float runDustSideJitter = 0.03f;
    [Tooltip("(Run Dust Interval이 0일 때만) 뛰기 모션 한 바퀴(0~1) 중 왼발 먼지가 나오는 시점입니다. 오른발은 +0.5.")]
    [Range(0f, 1f)] public float runDustPhase = 0.45f;
    [Tooltip("먼지가 나오는 자리: 발에서 뒤쪽으로 이만큼(m).")]
    public float runDustBackOffset = 0.05f;
    [Tooltip("먼지가 나오는 자리: 바닥(플레이어 발밑)에서 위로 이만큼(m).")]
    public float runDustHeight = 0.03f;
    [Tooltip("먼지가 퍼지는 방향: 0 = 바닥과 수평으로 뒤, 클수록 위로.")]
    [Min(0f)] public float runDustUpward = 0.6f;
    [Tooltip("먼지 크기 배율입니다(프리팹 크기 x 이 값).")]
    [Min(0.01f)] public float runDustScale = 1f;
    [Tooltip("먼지 오브젝트를 지우기까지의 시간(초)입니다. 파티클 수명보다 길게 둡니다.")]
    [Min(0.1f)] public float runDustLifetime = 1.2f;

    // ---------------- 읽기 ----------------

    private static TravelBoostSettings cached;
    private static bool loaded;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlayStart()
    {
        cached = null;
        loaded = false;
    }

    // Resources/TravelBoostSettings 에셋을 읽습니다. 없으면 기본값으로 만든 임시 설정을 돌려줍니다(항상 null이 아님).
    public static TravelBoostSettings Get()
    {
        if (!loaded || cached == null)
        {
            loaded = true;
            cached = Resources.Load<TravelBoostSettings>(ResourcePath);
            if (cached == null) cached = CreateInstance<TravelBoostSettings>();
        }
        return cached;
    }
}
