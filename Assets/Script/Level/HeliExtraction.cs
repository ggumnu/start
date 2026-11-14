using System.Collections.Generic;
using UnityEngine;

// [헬기 퇴각 연출 - 블루 아카이브 퇴각 화면 레퍼런스 (형빈 기획)]
// 다음 레벨로 가는 자리에 헬기(본체 없이 그림자만)가 X- 쪽에서 날아 들어와 멈추고, 프로펠러 바람으로 바닥 먼지 물결이 계속 퍼지며,
// 하늘에서 밧줄 사다리가 흔들리며 떨어집니다. 이 컴포넌트가 붙은 오브젝트의 위치(바닥)가 헬기가 멈추는 자리입니다.
//
// [진행 단계]
// 2단계: 그림자 날아오기 + 로터 그림 반복 + 호버링 / 갈색 먼지 물결 / 사다리 떨어짐 + 흔들림.
// 3단계: 사다리가 다 내려오면 F(LevelPortal) -> 확인창 [예] -> BeginExtraction:
//        조작 잠금 + HUD 숨김 + 카메라 고정 -> 총을 등에 사선으로 멤 -> 사다리 앞으로 걸어감 -> 점프해서 사다리를 붙잡음(매달린 자세 =
//        기존 획득 모션) -> 사다리가 출렁인 뒤 하늘로 끌어올려지고 헬기 그림자는 떠남 -> 화면이 서서히 검게 -> 다음 레벨(로딩 화면).
//        Space/ESC/Enter로 건너뛰면 바로 검게 하고 다음 레벨로. 넘어가기 직전(검은 화면 뒤)에 플레이어 상태를 모두 원래대로 돌립니다.
// 4단계: 다음 레벨 도착(TryStartArrival - GameFlowManager가 다음 레벨을 만든 직후 부름): 검은 로딩 화면 뒤에서 시작방 정중앙 하늘 위
//        (화면 밖)에 사다리와 매달린 플레이어를 걸어 둡니다. 로딩 화면이 걷히기 시작하면 헬기 그림자만 보스방 때처럼 X- 쪽에서 날아와
//        정중앙에 멈추고(사다리/플레이어는 그동안 시작방 위에서 움직이지 않음 - 플레이어 위치가 다른 방 칸으로 넘어가 그 방에 들어간
//        것으로 처리되지 않도록), Ladder Drop Delay 뒤에 사다리가 내려옴 -> 손을 놓고 착지 -> 총을 다시 손에 듦 ->
//        조작/HUD 복귀 -> 사다리가 다시 올라가고 헬기가 떠나며 사라짐. n.5 레벨처럼 시작방에 이미 다음 레벨용 헬기 포탈이 있으면 그 헬기로
//        내려오고, 떠나지 않고 그대로 남아 다음 레벨용 사다리가 됩니다. Space/ESC/Enter로 건너뛰면 잠깐 검게 -> 바로 착지 상태.
//
// [게임 클리어 퇴각 - BeginGameClearExtraction (LevelPortal이 게임 클리어 레벨에서 부름)]
//        3단계와 같이 걸어가 점프해 매달리지만, 화면을 검게 만들지 않고 다음 레벨로도 가지 않습니다. 사다리가 끌어올려지기 시작하는 순간
//        결과 화면(GameClearUI - 반투명 검은 막 + 연출)을 띄우고, 사다리/헬기는 그 뒤에서 평소처럼 올라가 떠나며 그림자가 흐려집니다.
//        사다리가 다 걷힌 뒤에는 플레이어를 그 자리(화면 밖 하늘)에 멈춰 둡니다(헬기를 따라 다른 방 칸으로 넘어가 그 방에 들어간 것으로
//        처리되지 않도록). Space/Enter 건너뛰기는 없고, [예] 뒤 어느 때든 ESC = 결과 화면을 연출 없이 바로(뒤의 동작은 그대로 이어짐).
//        플레이어/카메라/HUD를 되돌리지 않습니다(결과 화면에서 씬을 새로 불러옴).
//
// [그림]
// - 그림자: 바닥 위에 깐 판 하나(HeliUnlit 재질)에 그림자 그림 3장을 번갈아 넣습니다. 기수는 날아온 진행 방향을 봅니다.
// - 먼지: 90번 물결과 같은 RoomWave 셰이더 재질로, 그림자 중심에서 일정 간격으로 고리가 퍼집니다(방 밖은 잘림). 90번의 총알 제거 등
//   기능은 쓰지 않고 그림만 씁니다.
// - 사다리: 마디 여러 개로 나눈 밧줄을 직접 계산합니다(Unity 물리 엔진 없음). 위 끝은 헬기에 고정, 아래로 떨어지며 풀리고,
//   다 풀리는 순간 살짝 늘어났다 튕겨 오르며, 프로펠러 바람으로 계속 살랑입니다. 그 위에 사다리 그림을 이어 붙인 띠를 그리고,
//   띠는 항상 카메라를 향하므로 옆으로 납작해 보이지 않습니다.
// 모든 그림은 이 오브젝트의 자식이 아닌 별도 오브젝트로 만들어(부모 회전/크기의 영향 없음), 이 오브젝트가 사라질 때 함께 지웁니다.
//
// [방 전환 - 다른 방에서는 안 보임]
// 그림(그림자/사다리/먼지)은 별도 오브젝트라 방 콘텐츠가 꺼져도 저절로 꺼지지 않으므로, 플레이어가 헬기가 있는 방에 없으면 숨깁니다.
// - 보스방 포탈처럼 방 콘텐츠 밑에 있는 헬기: 플레이어가 다른 방으로 가면 방 콘텐츠와 함께 이 컴포넌트가 꺼지고(OnDisable) 그림도
//   숨깁니다. 꺼져 있는 동안은 Update가 멈춰 연출이 그 자리에서 멈춰 있다가, 방에 돌아와 다시 켜지면(OnEnable) 그대로 이어집니다.
// - 다음 레벨 도착용으로 씬 최상위에 만든 헬기(HeliArrival): 꺼지지 않으므로 매 프레임 RoomScope로 플레이어가 그 방에 있는지 보고
//   숨기거나 다시 보입니다(숨어 있어도 떠나는 연출은 계속 진행되어 끝나면 스스로 사라짐).
// - 방을 판단할 수 없으면(테스트 씬 등) 항상 보입니다.
//
// [설정] 값과 그림/재질은 HeliExtractionSettings 에셋(Resources).
// [테스트] 아무 씬의 바닥 위에 빈 오브젝트를 만들고 이 컴포넌트를 붙인 뒤 Play(Play On Start 켜짐). 다시 보려면 컴포넌트 오른쪽 위 ⋮ > Replay.
public class HeliExtraction : MonoBehaviour
{
    [Tooltip("켜면 오브젝트가 생기자마자 헬기가 날아옵니다.")]
    public bool playOnStart = true;

    // ---------------- 다른 스크립트가 읽는 상태 ----------------

    // 사다리가 다 내려와 흔들림이 어느 정도 가라앉았으면 true(이 뒤부터 탈 수 있음).
    public bool LadderReady => playing && ladderDropped && elapsed >= ladderReadyTime;
    // LevelPortal이 F를 받을지 판단합니다(사다리 준비 + 아직 퇴각 중이 아님).
    public bool CanStartExtraction => LadderReady && extractPhase == ExtractPhase.None;

    // 퇴각 연출 중이면 true - ESC 중단 화면, R 꾹 누르기 재시작을 막습니다.
    public static bool BlocksInput => active != null && active.extractPhase != ExtractPhase.None;
    // true인 동안 PlayerMovement는 키 입력 대신 ScriptedMoveDirection으로 걷습니다(사다리 앞으로 걸어가는 동안).
    public static bool OverridesMovement => active != null && active.extractPhase == ExtractPhase.Approach;
    public static Vector3 ScriptedMoveDirection => active != null ? active.moveDir : Vector3.zero;
    // 헬기로 다음 레벨에 넘어갔음(4단계 내려오기 연출이 읽고 지움).
    public static bool ArriveByHeliPending;

    // 도착 연출 상태
    private bool arrivalStarted;
    private bool arrivalStay;        // 다음 레벨용 헬기 포탈이라 착지 후에도 남음
    private bool leaving;            // 사다리가 올라가며 헬기가 떠나는 중
    private float leaveTimer;
    private float leaveFade = 1f;
    private float dropVelocity;
    private float arrivalGroundY;
    private bool controlsReturned;
    private bool arrivalSkipping;
    // 도착: 로딩 화면이 걷히기 시작할 때까지 헬기 날아오기 시계(elapsed)를 0에 붙잡아 둠
    private bool arrivalWaitingForLoad;
    // 도착: 헬기가 날아오는 동안 사다리 위 끝을 헬기 대신 시작방 정중앙(home) 하늘에 고정
    private bool arrivalHoldLadder;
    private bool IsArrivalPhase => extractPhase == ExtractPhase.ArriveHang || extractPhase == ExtractPhase.ArriveDrop || extractPhase == ExtractPhase.ArriveLanded;
    // 사다리 아래 끝 위치(월드). 사다리가 없으면 헬기 자리.
    public Vector3 LadderBottom => points != null && points.Length > 0 ? points[points.Length - 1] : transform.position;

    // ---------------- 내부 상태 ----------------

    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int CenterId = Shader.PropertyToID("_Center");
    private static readonly int RadiusId = Shader.PropertyToID("_Radius");
    private static readonly int WidthId = Shader.PropertyToID("_Width");
    private static readonly int RippleCountId = Shader.PropertyToID("_RippleCount");
    private static readonly int RippleSpacingId = Shader.PropertyToID("_RippleSpacing");
    private static readonly int RippleFalloffId = Shader.PropertyToID("_RippleFalloff");
    private static readonly int RoomRectId = Shader.PropertyToID("_RoomRect");
    private static readonly int EdgeSoftnessId = Shader.PropertyToID("_EdgeSoftness");
    private static readonly int FadeId = Shader.PropertyToID("_Fade");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private bool playing;
    private float elapsed;
    private Vector3 home;           // 헬기가 멈추는 자리(바닥)
    private Vector3 travelDir;      // 날아가는 방향(수평)
    private Vector3 heliPos;        // 지금 헬기(그림자 중심) 위치(바닥 높이)

    // 그림자
    private GameObject shadowGO;
    private MeshRenderer shadowRenderer;
    private MaterialPropertyBlock shadowBlock;

    // 먼지
    private class DustPulse { public GameObject go; public MeshRenderer renderer; public MaterialPropertyBlock block; public Vector3 center; public float start; }
    private readonly List<DustPulse> dustPulses = new List<DustPulse>();
    private float nextDustTime;
    private Rect roomRect;
    private Mesh dustMesh;

    // 사다리
    private Vector3[] points;
    private Vector3[] prevPoints;
    private float restLength;
    private bool ladderDropped;
    private float ladderReadyTime;
    private float simAccumulator;
    // 도착 하강 중 사다리를 헬기와 함께 통째로 옮기기 위한 값(사다리가 접히거나 뒤집히지 않도록)
    private bool carryValid;
    private Vector3 carryAnchor, carryVel, carryInertia;
    private GameObject ladderGO;
    private Mesh ladderMesh;
    private MeshRenderer ladderRenderer;
    private MaterialPropertyBlock ladderBlock;
    private Vector3[] ladderVerts;
    private Vector2[] ladderUVs;
    private float swaySeed;

    private const float SimStep = 1f / 120f;

    // 퇴각 연출
    private enum ExtractPhase { None, Approach, Jump, Hang, ArriveHang, ArriveDrop, ArriveLanded }
    private static HeliExtraction active;
    // 퇴각을 마쳐 이전 레벨과 함께 지워질 헬기입니다. 도착 연출용 헬기를 찾을 때 건너뜁니다(TryStartArrival 참고).
    private bool usedForExtraction;
    // 게임 클리어 퇴각(결과 화면으로 끝남)
    private bool gameClearMode;
    private bool gameClearShown;
    private ExtractPhase extractPhase = ExtractPhase.None;
    private System.Action onExtracted;
    private PlayerHealth player;
    private CharacterController playerController;
    private bool controllerWasEnabled;
    private Animator playerAnimator;
    private Vector3 moveDir;
    private float phaseTimer;
    private readonly List<Behaviour> disabledBehaviours = new List<Behaviour>();
    private Vector3 jumpStart;
    private int grabIndex;
    private float liftOffset, liftSpeed, departOffset, departSpeed;
    private float departWait = -1f; // 사다리가 다 걷힌 뒤 흐른 시간(초). -1 = 아직 걷히는 중
    private bool lifting;
    private bool fading;
    private float fadeTimer, fadeTime;
    private Camera lockedCam;
    private CameraFollow lockedFollow;
    private bool followWasEnabled;
    private CanvasGroup hudGroup;
    private GameObject hiddenGun;   // 연출 동안 숨긴 총(되돌릴 때 원래 켜짐 상태로)
    private bool hiddenGunWasActive;
    private Canvas overlayCanvas;
    private UnityEngine.UI.Image overlayImage;
    private float overlayAlpha;
    private int rotorLoopHandle; // [사운드] 헬기 로터 반복 소리
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        active = null;
        ArriveByHeliPending = false;
    }

    void Start()
    {
        // 다음 레벨 도착 연출이 이미 이 헬기를 쓰기 시작했으면(n.5 레벨 시작방 포탈) 날아오기를 다시 시작하지 않습니다.
        if (playOnStart && !arrivalStarted) Play();
    }

    // [방 전환] 방 콘텐츠가 다시 켜지면 그림도 다시 보이게, 꺼지면 함께 숨깁니다(클래스 주석 [방 전환] 참고).
    void OnEnable()
    {
        ApplyVisualsVisible(IsPlayerInHeliRoom());
    }

    void OnDisable()
    {
        ApplyVisualsVisible(false);
        GameAudio.StopLoop(ref rotorLoopHandle);
    }

    void OnDestroy()
    {
        // 연출 도중 사라지면 플레이어를 원래대로(게임 클리어 퇴각은 씬을 떠나며 사라지는 것이므로 되돌리지 않음)
        if (extractPhase != ExtractPhase.None && !gameClearMode) RestorePlayer();
        Cleanup();
        if (active == this) active = null;
    }

    [ContextMenu("Replay")]
    public void Play()
    {
        Cleanup();
        var s = HeliExtractionSettings.Get();
        home = transform.position;
        Vector3 from = new Vector3(s.flyInFrom.x, 0f, s.flyInFrom.y);
        if (from.sqrMagnitude < 0.0001f) from = Vector3.left;
        from.Normalize();
        travelDir = -from;
        roomRect = FindRoomRect(home, s.dustRadius);
        elapsed = 0f;
        nextDustTime = Mathf.Max(0f, s.flyInDuration - s.dustStartBeforeArrive);
        ladderDropped = false;
        points = null;
        swaySeed = Random.value * 100f;
        BuildShadow(s);
        playing = true;
        UpdateHeli(s);
    }

    void Update()
    {
        if (!playing) return;
        var s = HeliExtractionSettings.Get();
        elapsed += Time.deltaTime;
        // 도착: 검은 로딩 화면 뒤에서 헬기가 미리 날아와 버리지 않도록, 로딩 화면이 걷히기 시작할 때부터 날아오기 시작합니다.
        if (arrivalWaitingForLoad)
        {
            if (LoadingScreen.BlocksPlayerInput) elapsed = 0f;
            else arrivalWaitingForLoad = false;
        }

        UpdateHeli(s);
        UpdateDust(s);

        float dropTime = s.flyInDuration + s.ladderDropDelay;
        if (!ladderDropped && elapsed >= dropTime) DropLadder(s);
        if (ladderDropped) SimulateLadder(s, Time.deltaTime);

        if (IsArrivalPhase) UpdateArrival(s);
        else if (extractPhase != ExtractPhase.None) UpdateExtraction(s);
        if (leaving) UpdateLeave(s);
    }

    void LateUpdate()
    {
        // [방 전환] 플레이어가 이 헬기의 방에 없으면 그림을 숨깁니다. 이번 프레임에 새로 생긴 먼지 물결/사다리도 여기서 함께 맞춥니다
        // (화면 그리기는 LateUpdate 뒤라 한 프레임도 보이지 않음).
        if (playing) ApplyVisualsVisible(IsPlayerInHeliRoom());
        UpdateRotorSound();

        var s = HeliExtractionSettings.Get();
        // 사다리 띠는 카메라를 향해야 하므로 카메라가 움직인 뒤에 만듭니다.
        if (playing && ladderDropped) BuildLadderMesh(s);
    }

    // ------------------------------------------------------------------
    // 헬기 그림자: 날아오기 + 호버링 + 로터 그림 반복
    // ------------------------------------------------------------------

    private void UpdateHeli(HeliExtractionSettings s)
    {
        float t = Mathf.Clamp01(elapsed / Mathf.Max(0.05f, s.flyInDuration));
        float eased = 1f - Mathf.Pow(1f - t, 3f); // 끝으로 갈수록 느려지며 멈춤
        Vector3 pos = home - travelDir * s.flyInDistance * (1f - eased);

        // 호버링: 도착할 즈음부터 제자리에서 천천히 원을 그리듯 흔들림
        float hoverWeight = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.6f, 1f, t));
        float w = Mathf.PI * 2f / Mathf.Max(0.1f, s.hoverPeriod);
        Vector3 hover = new Vector3(Mathf.Sin(elapsed * w), 0f, Mathf.Sin(elapsed * w * 0.73f + 1.3f) * 0.6f) * s.hoverDrift;
        heliPos = pos + hover * hoverWeight + travelDir * departOffset; // 퇴각 때는 진행 방향으로 떠남

        if (shadowGO == null) return;
        shadowGO.transform.SetPositionAndRotation(
            heliPos + Vector3.up * s.groundOffset,
            Quaternion.FromToRotation(Vector3.right, travelDir));
        shadowGO.transform.localScale = Vector3.one * s.shadowSize;

        // 처음엔 옅게 시작해 날아오는 동안 진해짐
        float alpha = s.shadowOpacity * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.35f)) * leaveFade;
        shadowBlock.SetColor(BaseColorId, new Color(0f, 0f, 0f, alpha));
        if (s.shadowFrames != null && s.shadowFrames.Length > 0)
        {
            int frame = Mathf.FloorToInt(elapsed * s.shadowFps) % s.shadowFrames.Length;
            if (s.shadowFrames[frame] != null) shadowBlock.SetTexture(BaseMapId, s.shadowFrames[frame]);
        }
        shadowRenderer.SetPropertyBlock(shadowBlock);
    }

    private void BuildShadow(HeliExtractionSettings s)
    {
        if (s.shadowMaterial == null)
        {
            Debug.LogWarning("HeliExtraction: 설정 에셋의 Shadow Material이 비어 있어 헬기 그림자를 그리지 않습니다.");
            return;
        }
        shadowGO = new GameObject("HeliShadow");
        var mesh = new Mesh { name = "HeliShadowQuad" };
        // 바닥에 눕힌 1x1 판: 그림 오른쪽(U) = 로컬 +X(기수), 그림 위쪽(V) = 로컬 +Z
        mesh.vertices = new[] { new Vector3(-0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, -0.5f), new Vector3(-0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, 0.5f) };
        mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
        mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
        mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        mesh.RecalculateBounds();
        shadowGO.AddComponent<MeshFilter>().sharedMesh = mesh;
        shadowRenderer = shadowGO.AddComponent<MeshRenderer>();
        SetupRenderer(shadowRenderer, s.shadowMaterial);
        shadowBlock = new MaterialPropertyBlock();
    }

    // ------------------------------------------------------------------
    // 바닥 먼지 물결
    // ------------------------------------------------------------------

    private void UpdateDust(HeliExtractionSettings s)
    {
        if (s.dustMaterial != null && elapsed >= nextDustTime && !leaving)
        {
            nextDustTime = elapsed + s.dustInterval;
            SpawnDust(s);
        }

        for (int i = dustPulses.Count - 1; i >= 0; i--)
        {
            var p = dustPulses[i];
            float u = (elapsed - p.start) / Mathf.Max(0.05f, s.dustDuration);
            if (u >= 1f || p.go == null)
            {
                if (p.go != null) Destroy(p.go);
                dustPulses.RemoveAt(i);
                continue;
            }
            float eased = 1f - (1f - u) * (1f - u); // 처음엔 빠르게 퍼지고 점점 느려짐
            float fade = u <= s.dustFadeStart ? 1f : 1f - Mathf.InverseLerp(s.dustFadeStart, 1f, u);
            p.block.SetFloat(RadiusId, s.dustRadius * eased);
            p.block.SetFloat(FadeId, fade);
            p.renderer.SetPropertyBlock(p.block);
        }
    }

    private void SpawnDust(HeliExtractionSettings s)
    {
        if (dustMesh == null) dustMesh = BuildDustMesh(roomRect);
        var go = new GameObject("HeliDustPulse");
        go.transform.position = new Vector3(0f, home.y + s.groundOffset * 0.5f, 0f);
        go.AddComponent<MeshFilter>().sharedMesh = dustMesh;
        var r = go.AddComponent<MeshRenderer>();
        SetupRenderer(r, s.dustMaterial);
        var block = new MaterialPropertyBlock();
        Vector3 c = heliPos;
        block.SetVector(CenterId, new Vector4(c.x, c.y, c.z, 0f));
        block.SetVector(RoomRectId, new Vector4(roomRect.xMin, roomRect.yMin, roomRect.xMax, roomRect.yMax));
        block.SetFloat(WidthId, s.dustRingWidth);
        block.SetFloat(RippleCountId, s.dustRippleCount);
        block.SetFloat(RippleSpacingId, s.dustRippleSpacing);
        block.SetFloat(RippleFalloffId, s.dustRippleFalloff);
        block.SetFloat(EdgeSoftnessId, 0.3f);
        block.SetColor(ColorId, s.dustColor);
        block.SetFloat(RadiusId, 0f);
        block.SetFloat(FadeId, 1f);
        r.SetPropertyBlock(block);
        dustPulses.Add(new DustPulse { go = go, renderer = r, block = block, center = c, start = elapsed });
    }

    // 방 바닥 사각형(월드 X/Z)으로 된 판입니다(로컬 높이 0 - 오브젝트 위치가 높이를 정함).
    private static Mesh BuildDustMesh(Rect rect)
    {
        var mesh = new Mesh { name = "HeliDustQuad" };
        mesh.vertices = new[]
        {
            new Vector3(rect.xMin, 0f, rect.yMin), new Vector3(rect.xMax, 0f, rect.yMin),
            new Vector3(rect.xMin, 0f, rect.yMax), new Vector3(rect.xMax, 0f, rect.yMax),
        };
        mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
        mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
        mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        mesh.RecalculateBounds();
        return mesh;
    }

    // 헬기 자리가 속한 방의 바닥 사각형(칸 중심 = 칸 좌표 x cellSize - LevelBuilder와 같은 규칙). 방을 못 찾으면 반경만큼의 사각형.
    private static Rect FindRoomRect(Vector3 pos, float radius)
    {
        Rect fallback = Rect.MinMaxRect(pos.x - radius * 1.2f, pos.z - radius * 1.2f, pos.x + radius * 1.2f, pos.z + radius * 1.2f);
        var builder = Object.FindFirstObjectByType<LevelBuilder>();
        if (builder == null || builder.GeneratedRooms == null) return fallback;
        float cs = builder.cellSize;
        var cell = new Vector2Int(Mathf.RoundToInt(pos.x / cs), Mathf.RoundToInt(pos.z / cs));
        foreach (var room in builder.GeneratedRooms)
        {
            if (room == null || room.occupiedCells == null || !room.occupiedCells.Contains(cell)) continue;
            float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;
            foreach (var c in room.occupiedCells)
            {
                minX = Mathf.Min(minX, c.x * cs - cs * 0.5f); maxX = Mathf.Max(maxX, c.x * cs + cs * 0.5f);
                minZ = Mathf.Min(minZ, c.y * cs - cs * 0.5f); maxZ = Mathf.Max(maxZ, c.y * cs + cs * 0.5f);
            }
            return Rect.MinMaxRect(minX, minZ, maxX, maxZ);
        }
        return fallback;
    }

    // ------------------------------------------------------------------
    // 사다리: 떨어뜨리기 / 흔들림 계산
    // ------------------------------------------------------------------

    // 헬기 출발: 사다리 아래 끝이 헬기 높이까지 다 걷힌 뒤 Depart After Lift(초)만큼 기다렸다가 진행 방향으로 떠납니다.
    // 돌려주는 값 = 출발한 뒤 흐른 시간(초). 아직 출발 전이면 0.
    private float UpdateDepart(HeliExtractionSettings s, float dt)
    {
        if (departWait < 0f)
        {
            float ladderLength = Mathf.Max(0.05f, s.ladderTopHeight - s.ladderBottomClearance);
            if (liftOffset < ladderLength) return 0f;
            departWait = 0f;
        }
        departWait += dt;
        float departTime = departWait - s.departAfterLift;
        if (departTime <= 0f) return 0f;
        departSpeed += s.departAcceleration * dt;
        departOffset += departSpeed * dt;
        return departTime;
    }

    private Vector3 LadderAnchor(HeliExtractionSettings s)
    {
        Vector3 left = Vector3.Cross(Vector3.up, travelDir).normalized; // 헬기 기준 왼쪽
        // 도착 연출에서 헬기가 날아오는 동안은 사다리(와 매달린 플레이어)를 시작방 정중앙 하늘에 그대로 둡니다.
        Vector3 basePos = arrivalHoldLadder ? home : heliPos;
        return basePos + travelDir * s.ladderOffset.x + left * s.ladderOffset.y + Vector3.up * (s.ladderTopHeight + liftOffset);
    }

    private void DropLadder(HeliExtractionSettings s)
    {
        if (s.ladderMaterial == null)
        {
            Debug.LogWarning("HeliExtraction: 설정 에셋의 Ladder Material이 비어 있어 사다리를 그리지 않습니다.");
        }
        ladderDropped = true;
        ladderReadyTime = elapsed + s.ladderSettleTime + Mathf.Sqrt(2f * s.ladderTopHeight / Mathf.Max(0.1f, s.ladderGravity));

        int n = Mathf.Max(4, s.ladderSegments) + 1;
        points = new Vector3[n];
        prevPoints = new Vector3[n];
        restLength = Mathf.Max(0.05f, s.ladderTopHeight - s.ladderBottomClearance) / (n - 1);

        // 처음엔 모든 마디가 헬기 아래에 뭉쳐 있다가 떨어지며 위에서부터 풀립니다.
        Vector3 anchor = LadderAnchor(s);
        Vector3 kickDir = Camera.main != null ? Camera.main.transform.right : Vector3.right;
        kickDir.y = 0f;
        kickDir = kickDir.sqrMagnitude > 0.0001f ? kickDir.normalized : Vector3.right;
        if (Random.value < 0.5f) kickDir = -kickDir;
        for (int i = 0; i < n; i++)
        {
            float k = (float)i / (n - 1);
            points[i] = anchor + Vector3.down * (k * 0.3f);
            // 아래쪽일수록 옆으로 살짝 던져서 곧게 떨어지지 않고 휘청이게 합니다.
            Vector3 v = kickDir * s.ladderThrowKick * k;
            prevPoints[i] = points[i] - v * SimStep;
        }
        simAccumulator = 0f;
        BuildLadderObject(s);
    }

    private void SimulateLadder(HeliExtractionSettings s, float deltaTime)
    {
        if (points == null) return;
        CarryLadderWithHeli(s, Mathf.Min(deltaTime, 0.1f));
        simAccumulator += Mathf.Min(deltaTime, 0.1f);
        int steps = 0;
        while (simAccumulator >= SimStep && steps < 12)
        {
            simAccumulator -= SimStep;
            steps++;
            Step(s, elapsed - simAccumulator);
        }
    }

    // 도착 하강(사다리에 매달려 내려오는 동안): 위 끝이 빠르게 내려가면 아래 마디들이 못 따라와 사다리가 접히고 뒤집혔습니다.
    // 그래서 사다리 전체를 위 끝이 움직인 만큼 함께 옮기고, 출발/멈춤의 반동은 Arrival Ladder Inertia 비율만큼만 힘으로 줍니다.
    // 위로 밀어 올리는 반동(내려가기 시작할 때)은 중력보다 약하게 제한해서 사다리가 접히지 않게 합니다.
    private void CarryLadderWithHeli(HeliExtractionSettings s, float dt)
    {
        if (extractPhase != ExtractPhase.ArriveHang || dt <= 0f)
        {
            carryValid = false;
            carryInertia = Vector3.zero;
            return;
        }
        Vector3 anchorNow = LadderAnchor(s);
        if (!carryValid)
        {
            carryValid = true;
            carryAnchor = anchorNow;
            carryVel = Vector3.zero;
            carryInertia = Vector3.zero;
            return;
        }
        Vector3 delta = anchorNow - carryAnchor;
        Vector3 vel = delta / dt;
        Vector3 acc = (vel - carryVel) / dt;
        carryAnchor = anchorNow;
        carryVel = vel;
        for (int i = 1; i < points.Length; i++)
        {
            points[i] += delta;
            prevPoints[i] += delta; // 같이 옮겨서 속도는 그대로(갑자기 튀지 않음)
        }
        Vector3 inertia = -acc * s.arrivalLadderInertia;
        inertia.y = Mathf.Min(inertia.y, s.ladderGravity * 0.6f); // 위로 미는 힘은 중력보다 약하게 = 접히지 않음
        inertia = Vector3.ClampMagnitude(inertia, s.ladderGravity * 1.5f);
        carryInertia = Vector3.Lerp(carryInertia, inertia, 1f - Mathf.Exp(-dt * 20f)); // 프레임마다 튀지 않게 부드럽게
    }

    private void Step(HeliExtractionSettings s, float time)
    {
        int n = points.Length;
        float damping = Mathf.Pow(s.ladderDamping, SimStep * 60f);
        float groundY = home.y + 0.02f;
        float f = s.ladderSwaySpeed * Mathf.PI * 2f;

        // 1) 움직임(Verlet): 관성 + 중력 + 프로펠러 바람
        for (int i = 1; i < n; i++)
        {
            float k = (float)i / (n - 1);
            Vector3 vel = (points[i] - prevPoints[i]) * damping;
            Vector3 wind = new Vector3(
                Mathf.Sin(time * f + swaySeed + i * 0.22f),
                0f,
                Mathf.Sin(time * f * 0.71f + swaySeed * 1.7f + i * 0.17f) * 0.7f) * (s.ladderSwayStrength * k);
            Vector3 acc = Vector3.down * s.ladderGravity + wind + carryInertia * k;
            prevPoints[i] = points[i];
            points[i] += vel + acc * (SimStep * SimStep);
        }
        points[0] = LadderAnchor(s);
        prevPoints[0] = points[0];

        // 2) 길이 유지: 마디 사이가 원래 길이보다 멀어지면 당깁니다(가까워지는 건 허용 = 밧줄처럼 접힘).
        //    Stretch가 클수록 한 번에 덜 당겨서 순간적으로 늘어났다가 튕겨 오릅니다.
        float stiffness = Mathf.Lerp(1f, 0.12f, s.ladderStretch);
        for (int iter = 0; iter < 8; iter++)
        {
            points[0] = LadderAnchor(s);
            for (int i = 0; i < n - 1; i++)
            {
                Vector3 d = points[i + 1] - points[i];
                float len = d.magnitude;
                if (len <= restLength || len < 1e-5f) continue;
                Vector3 corr = d * ((len - restLength) / len) * stiffness;
                if (i == 0) points[i + 1] -= corr;          // 위 끝은 헬기에 고정
                else
                {
                    points[i] += corr * 0.5f;
                    points[i + 1] -= corr * 0.5f;
                }
            }
            // 도착 하강 중: 아래 마디가 위 마디보다 높이 올라가지 못하게(사다리가 접히거나 뒤집히지 않도록)
            if (carryValid)
            {
                for (int i = 1; i < n; i++)
                {
                    float maxY = points[i - 1].y - restLength * 0.5f;
                    if (points[i].y > maxY) points[i].y = maxY;
                }
            }
            // 바닥을 뚫지 않게
            for (int i = 1; i < n; i++)
            {
                if (points[i].y < groundY) points[i].y = groundY;
            }
        }
    }

    // ------------------------------------------------------------------
    // 사다리 그림: 카메라를 향하는 띠 + 사다리 그림 반복
    // ------------------------------------------------------------------

    private void BuildLadderObject(HeliExtractionSettings s)
    {
        if (ladderGO != null) Destroy(ladderGO);
        ladderGO = new GameObject("HeliLadder");
        ladderMesh = new Mesh { name = "HeliLadderRibbon" };
        ladderMesh.MarkDynamic();
        int n = points.Length;
        ladderVerts = new Vector3[n * 2];
        ladderUVs = new Vector2[n * 2];
        var tris = new int[(n - 1) * 6];
        for (int i = 0; i < n - 1; i++)
        {
            int a = i * 2, t = i * 6;
            tris[t] = a; tris[t + 1] = a + 2; tris[t + 2] = a + 1;
            tris[t + 3] = a + 1; tris[t + 4] = a + 2; tris[t + 5] = a + 3;
        }
        ladderMesh.vertices = ladderVerts;
        ladderMesh.uv = ladderUVs;
        ladderMesh.triangles = tris;
        ladderGO.AddComponent<MeshFilter>().sharedMesh = ladderMesh;
        ladderRenderer = ladderGO.AddComponent<MeshRenderer>();
        if (s.ladderMaterial != null) SetupRenderer(ladderRenderer, s.ladderMaterial);
        else ladderRenderer.enabled = false;
        ladderBlock = new MaterialPropertyBlock();
        if (s.ladderTexture != null) ladderBlock.SetTexture(BaseMapId, s.ladderTexture);
        ladderBlock.SetColor(BaseColorId, Color.white);
        ladderRenderer.SetPropertyBlock(ladderBlock);
    }

    private void BuildLadderMesh(HeliExtractionSettings s)
    {
        if (ladderMesh == null || points == null) return;
        Camera cam = Camera.main;
        Vector3 camFwd = cam != null ? cam.transform.forward : Vector3.forward;
        Vector3 camRight = cam != null ? cam.transform.right : Vector3.right;
        int n = points.Length;
        float half = s.ladderWidth * 0.5f;
        float tile = Mathf.Max(0.05f, s.ladderTileLength);

        // 그림은 위(헬기)에서부터 이어 붙입니다 - 사다리가 풀려 내려오는 동안 발판이 미끄러지지 않도록 마디 기준 고정 간격.
        for (int i = 0; i < n; i++)
        {
            Vector3 prev = points[Mathf.Max(0, i - 1)];
            Vector3 next = points[Mathf.Min(n - 1, i + 1)];
            Vector3 along = next - prev;
            if (along.sqrMagnitude < 1e-6f) along = Vector3.down;
            Vector3 side = Vector3.Cross(along.normalized, camFwd);
            if (side.sqrMagnitude < 1e-6f) side = camRight;
            side = side.normalized * half;
            // 그림의 좌우가 화면에서 뒤집히지 않도록 카메라 오른쪽과 같은 방향으로 맞춤
            if (Vector3.Dot(side, camRight) < 0f) side = -side;

            ladderVerts[i * 2] = points[i] - side;
            ladderVerts[i * 2 + 1] = points[i] + side;
            float v = -(i * restLength) / tile;
            ladderUVs[i * 2] = new Vector2(0f, v);
            ladderUVs[i * 2 + 1] = new Vector2(1f, v);
        }
        ladderMesh.vertices = ladderVerts;
        ladderMesh.uv = ladderUVs;
        ladderMesh.RecalculateBounds();
    }

    // ------------------------------------------------------------------
    // 도착 연출 (4단계) - 다음 레벨에서 사다리를 타고 내려옴
    // ------------------------------------------------------------------

    // GameFlowManager가 다음 레벨을 만든 직후(아직 검은 로딩 화면 뒤) 부릅니다. 헬기로 넘어온 경우에만 도착 연출을 겁니다.
    public static void TryStartArrival(LevelBuilder builder)
    {
        if (!ArriveByHeliPending) return;
        ArriveByHeliPending = false;

        GameObject pgo = builder != null ? builder.CurrentPlayer : null;
        PlayerHealth p = pgo != null ? pgo.GetComponent<PlayerHealth>() : Object.FindFirstObjectByType<PlayerHealth>();
        if (p == null || p.IsDead) return;

        // LevelBuilder는 플레이어를 시작방 바닥 1m 위(정중앙)에 놓습니다.
        Vector3 ground = p.transform.position + Vector3.down * 1f;

        // n.5 레벨처럼 시작방 정중앙에 이미 다음 레벨용 헬기 포탈이 있으면 그 헬기로 내려오고, 착지 후에도 남겨 둡니다.
        HeliExtraction heli = null;
        float best = 1.5f;
        foreach (var h in Object.FindObjectsByType<HeliExtraction>(FindObjectsSortMode.None))
        {
            if (h == null || h.extractPhase != ExtractPhase.None) continue;
            // 방금 퇴각에 쓴 이전 레벨 헬기는 Destroy가 프레임 끝에야 처리되어 아직 남아 있고, n.5 레벨 시작방처럼 다음 레벨 시작방과
            // 같은 자리(정중앙)에 있을 수 있습니다. 그 헬기를 고르면 곧바로 지워지며 도착 연출이 끊기므로 건너뜁니다.
            if (h.usedForExtraction) continue;
            float d = Flat(h.transform.position - ground).magnitude;
            if (d < best) { best = d; heli = h; }
        }
        bool stay = heli != null;
        if (heli == null)
        {
            var go = new GameObject("HeliArrival");
            go.transform.position = ground;
            heli = go.AddComponent<HeliExtraction>();
            heli.playOnStart = false;
        }
        heli.BeginArrival(p, stay);
    }

    private void BeginArrival(PlayerHealth p, bool stay)
    {
        var s = HeliExtractionSettings.Get();
        Cleanup();
        arrivalStarted = true;
        arrivalStay = stay;
        leaving = false;
        leaveFade = 1f;
        arrivalSkipping = false;
        controlsReturned = false;

        // 헬기는 보스방 때처럼 X- 쪽 처음 자리에서 날아오기 시작하고(로딩 화면이 걷힐 때까지는 그 자리에서 대기), 사다리는 다 내려온
        // 상태에서 위로 끌어올려진 채 시작방 정중앙 하늘(화면 밖)에 걸려 있습니다.
        home = transform.position;
        Vector3 from = new Vector3(s.flyInFrom.x, 0f, s.flyInFrom.y);
        if (from.sqrMagnitude < 0.0001f) from = Vector3.left;
        travelDir = -from.normalized;
        roomRect = FindRoomRect(home, s.dustRadius);
        elapsed = 0f;
        arrivalWaitingForLoad = true;
        arrivalHoldLadder = true;
        nextDustTime = Mathf.Max(0f, s.flyInDuration - s.dustStartBeforeArrive);
        swaySeed = Random.value * 100f;
        liftOffset = s.arrivalStartLift;
        liftSpeed = departOffset = departSpeed = 0f;
        departWait = -1f;
        lifting = false;
        BuildShadow(s);
        playing = true;
        UpdateHeli(s);

        int n = Mathf.Max(4, s.ladderSegments) + 1;
        points = new Vector3[n];
        prevPoints = new Vector3[n];
        restLength = Mathf.Max(0.05f, s.ladderTopHeight - s.ladderBottomClearance) / (n - 1);
        Vector3 anchor = LadderAnchor(s);
        for (int i = 0; i < n; i++)
        {
            points[i] = anchor + Vector3.down * (restLength * i);
            prevPoints[i] = points[i];
        }
        ladderDropped = true;
        ladderReadyTime = float.MaxValue; // 착지 전에는 F로 탈 수 없음
        simAccumulator = 0f;
        BuildLadderObject(s);

        // 플레이어: 조작 잠금 + 이동 끔 + 매달린 자세 + 총은 등에
        active = this;
        player = p;
        playerController = p.GetComponent<CharacterController>();
        playerAnimator = p.GetComponentInChildren<Animator>();
        disabledBehaviours.Clear();
        EnforceLock(true);
        if (playerController != null)
        {
            controllerWasEnabled = playerController.enabled;
            playerController.enabled = false;
        }

        // 카메라는 착지할 자리(시작방 정중앙)의 게임 시점에 고정합니다(높이 매달린 플레이어를 따라 올라가지 않도록).
        lockedCam = Camera.main;
        lockedFollow = lockedCam != null ? lockedCam.GetComponent<CameraFollow>() : null;
        followWasEnabled = lockedFollow != null && lockedFollow.enabled;
        if (lockedFollow != null)
        {
            lockedFollow.enabled = false;
            Quaternion rot = lockedFollow.lockRotation ? Quaternion.Euler(lockedFollow.fixedEulerAngles) : lockedCam.transform.rotation;
            lockedCam.transform.SetPositionAndRotation(home + lockedFollow.offset, rot);
        }
        SetHudAlpha(0f);
        HideGun();
        SetAnimBool(s.hangBool, true);
        CrossFadeUpper(s.hangStateName, 0f);
        SetAnimFloat("Speed", 0f); SetAnimFloat("MoveX", 0f); SetAnimFloat("MoveZ", 0f);

        grabIndex = Mathf.Clamp(Mathf.RoundToInt((s.ladderTopHeight - s.grabHeight) / Mathf.Max(0.01f, restLength)), 1, n - 1);
        arrivalGroundY = home.y;
        player.transform.position = HangPosition(s);
        Vector3 faceDir = lockedFollow != null ? Flat(lockedFollow.offset) : -travelDir;
        if (faceDir.sqrMagnitude > 0.0001f) player.transform.rotation = Quaternion.LookRotation(faceDir.normalized);

        phaseTimer = 0f;
        extractPhase = ExtractPhase.ArriveHang;
    }

    private void UpdateArrival(HeliExtractionSettings s)
    {
        if (player == null)
        {
            RestorePlayer();
            return;
        }
        float dt = Time.deltaTime;
        if (!controlsReturned) EnforceLock(true);

        // 건너뛰기: 로딩 화면이 걷힌 뒤부터
        if (!arrivalSkipping && extractPhase != ExtractPhase.ArriveLanded && !LoadingScreen.BlocksPlayerInput && s.skipKeys != null)
        {
            foreach (var key in s.skipKeys)
            {
                if (key != KeyCode.None && Input.GetKeyDown(key))
                {
                    StartCoroutine(ArrivalSkip(s));
                    return;
                }
            }
        }
        if (arrivalSkipping) return;

        switch (extractPhase)
        {
            case ExtractPhase.ArriveHang:
                player.transform.position = HangPosition(s);
                FaceHangDirection(s, 8f);
                if (LoadingScreen.BlocksPlayerInput) break; // 로딩 화면이 걷히기 시작할 때까지 기다림
                // 헬기가 날아와 멈추고 Ladder Drop Delay가 지날 때까지 사다리는 시작방 위에서 기다립니다.
                if (elapsed < s.flyInDuration + s.ladderDropDelay) break;
                // 이제부터 사다리 위 끝은 헬기를 따라갑니다(멈춘 헬기의 호버링만큼만 움직이므로 거의 그 자리).
                arrivalHoldLadder = false;
                phaseTimer += dt;
                float t = Mathf.Clamp01(phaseTimer / Mathf.Max(0.1f, s.descendDuration));
                liftOffset = s.arrivalStartLift * Mathf.Pow(1f - t, 3f); // 끝으로 갈수록 느려지며 멈춤
                if (phaseTimer >= s.descendDuration + s.letGoDelay)
                {
                    dropVelocity = 0f;
                    phaseTimer = 0f;
                    extractPhase = ExtractPhase.ArriveDrop;
                }
                break;

            case ExtractPhase.ArriveDrop:
                dropVelocity += s.dropGravity * dt;
                Vector3 pos = player.transform.position;
                pos.y -= dropVelocity * dt;
                if (pos.y <= arrivalGroundY)
                {
                    pos.y = arrivalGroundY;
                    player.transform.position = pos;
                    Land(s);
                }
                else player.transform.position = pos;
                break;

            case ExtractPhase.ArriveLanded:
                phaseTimer += dt;
                if (!controlsReturned && phaseTimer >= s.controlReturnDelay) ReturnControls(s);
                break;
        }
    }

    // 착지: 매달린 자세 -> 대기 자세로 섞여 돌아가고, 총을 다시 손에 듭니다.
    private void Land(HeliExtractionSettings s)
    {
        if (playerController != null && controllerWasEnabled) playerController.enabled = true;
        controllerWasEnabled = false;
        SetAnimBool(s.hangBool, false);
        CrossFadeUpper("NonAimIdle", s.landBlend);
        RestoreGun();
        if (arrivalStay) ladderReadyTime = elapsed + 0.5f; // 다음 레벨용 사다리로 남음
        else { leaving = true; leaveTimer = 0f; departWait = -1f; }
        phaseTimer = 0f;
        extractPhase = ExtractPhase.ArriveLanded;
    }

    // 조작/카메라/HUD를 돌려줍니다(사다리/헬기가 떠나는 연출은 계속).
    private void ReturnControls(HeliExtractionSettings s)
    {
        controlsReturned = true;
        bool alive = player != null && !player.IsDead;
        foreach (var b in disabledBehaviours) if (b != null && alive) b.enabled = true;
        disabledBehaviours.Clear();
        if (lockedFollow != null && followWasEnabled) lockedFollow.enabled = true;
        lockedFollow = null;
        lockedCam = null;
        followWasEnabled = false;
        StartCoroutine(FadeHudIn(s.arrivalHudFadeIn));
        extractPhase = ExtractPhase.None;
        if (active == this) active = null;
    }

    private System.Collections.IEnumerator FadeHudIn(float duration)
    {
        var group = hudGroup;
        float t = 0f;
        while (group != null && duration > 0f && t < duration)
        {
            t += Time.unscaledDeltaTime;
            group.alpha = Mathf.Clamp01(t / duration);
            yield return null;
        }
        if (group != null) group.alpha = 1f;
        if (hudGroup == group) hudGroup = null;
    }

    // 착지 후 사다리가 다시 올라가고 헬기가 떠나며 그림자가 사라집니다. 다 사라지면 도착용 헬기는 지워집니다.
    private void UpdateLeave(HeliExtractionSettings s)
    {
        float dt = Time.deltaTime;
        leaveTimer += dt;
        if (leaveTimer < s.leaveDelay) return;
        liftSpeed = Mathf.Min(s.liftMaxSpeed, liftSpeed + s.liftAcceleration * dt);
        liftOffset += liftSpeed * dt;
        // 사다리가 다 걷힌 뒤에야 헬기가 출발하고, 그때부터 그림자가 흐려집니다.
        float departTime = UpdateDepart(s, dt);
        leaveFade = 1f - Mathf.Clamp01(departTime / Mathf.Max(0.1f, s.leaveDuration));
        if (leaveFade <= 0f && extractPhase == ExtractPhase.None && !arrivalSkipping) Destroy(gameObject);
    }

    // 건너뛰기: 잠깐 검게 -> 바로 착지한 상태(정중앙, 대기 자세, 총은 손에, 조작/카메라/HUD 복귀) -> 다시 밝아짐
    private System.Collections.IEnumerator ArrivalSkip(HeliExtractionSettings s)
    {
        arrivalSkipping = true;
        float t = 0f;
        float d = Mathf.Max(0.01f, s.skipFadeDuration);
        while (t < d)
        {
            t += Time.unscaledDeltaTime;
            SetOverlay(t / d);
            yield return null;
        }
        SetOverlay(1f);

        if (player != null)
        {
            if (playerController != null) playerController.enabled = false;
            player.transform.position = home;
            if (playerController != null) playerController.enabled = true;
            controllerWasEnabled = false;
            if (lockedFollow != null) player.transform.rotation = Quaternion.LookRotation(Flat(lockedFollow.offset).normalized);
        }
        // RestorePlayer: 총을 손에, 매달린 자세 해제(대기 자세로 바로), 조작/카메라/HUD 복귀
        RestorePlayer();
        controlsReturned = true;

        // 헬기가 날아오는 도중에 건너뛰었으면 헬기를 바로 정중앙에 멈춘 상태로 둡니다.
        arrivalWaitingForLoad = false;
        arrivalHoldLadder = false;
        elapsed = Mathf.Max(elapsed, s.flyInDuration + s.ladderDropDelay);
        UpdateHeli(s);

        if (arrivalStay)
        {
            // 다음 레벨용 사다리로 남김: 사다리를 원래 높이로 바로 내려놓음
            liftOffset = 0f;
            Vector3 anchor = LadderAnchor(s);
            for (int i = 0; i < points.Length; i++) { points[i] = anchor + Vector3.down * (restLength * i); prevPoints[i] = points[i]; }
            ladderReadyTime = elapsed + 0.3f;
        }
        else
        {
            // 도착용 헬기는 검은 화면 뒤에서 바로 치움
            Cleanup();
        }

        yield return new WaitForSecondsRealtime(0.1f);
        t = 0f;
        float o = 0.3f;
        while (t < o)
        {
            t += Time.unscaledDeltaTime;
            SetOverlay(1f - t / o);
            yield return null;
        }
        SetOverlay(0f);
        arrivalSkipping = false;
        if (!arrivalStay) Destroy(gameObject);
    }

    // ------------------------------------------------------------------
    // 퇴각 연출 (3단계) - LevelPortal의 확인창 [예]에서 부름
    // ------------------------------------------------------------------

    // 퇴각 연출을 시작합니다. 끝나면(화면이 완전히 검어지면) onDone을 부릅니다(= 다음 레벨로 이동). 시작할 수 없으면 바로 onDone.
    public void BeginExtraction(System.Action onDone)
    {
        var s = HeliExtractionSettings.Get();
        player = Object.FindFirstObjectByType<PlayerHealth>();
        if (!CanStartExtraction || player == null || player.IsDead || points == null)
        {
            onDone?.Invoke();
            return;
        }

        active = this;
        onExtracted = onDone;
        playerController = player.GetComponent<CharacterController>();
        playerAnimator = player.GetComponentInChildren<Animator>();
        disabledBehaviours.Clear();
        EnforceLock(false);

        // 카메라 고정(따라가지 않음) + HUD 숨김
        lockedCam = Camera.main;
        lockedFollow = lockedCam != null ? lockedCam.GetComponent<CameraFollow>() : null;
        followWasEnabled = lockedFollow != null && lockedFollow.enabled;
        if (lockedFollow != null) lockedFollow.enabled = false;
        SetHudAlpha(0f);

        // 총 숨김(걸어가기 시작할 때 - 형빈 확정, 예전 "등에 메기" 대체)
        HideGun();

        int n = points.Length;
        grabIndex = Mathf.Clamp(Mathf.RoundToInt((s.ladderTopHeight - s.grabHeight) / Mathf.Max(0.01f, restLength)), 1, n - 1);
        liftOffset = liftSpeed = departOffset = departSpeed = 0f;
        departWait = -1f;
        lifting = false;
        fading = false;
        moveDir = Vector3.zero;
        phaseTimer = 0f;
        extractPhase = ExtractPhase.Approach;
    }

    // [게임 클리어 퇴각] 걸어가 매달리는 것까지는 퇴각 연출과 같고, 끌어올려지기 시작하면 결과 화면을 띄웁니다(클래스 주석 참고).
    // 퇴각을 시작할 수 없는 상태면 결과 화면만 바로 띄웁니다.
    public void BeginGameClearExtraction()
    {
        BeginExtraction(null);
        if (extractPhase == ExtractPhase.None)
        {
            GameClearUI.Show(false);
            return;
        }
        gameClearMode = true;
        gameClearShown = false;
        leaveFade = 1f;
    }

    private void ShowGameClear(bool skipToEnd)
    {
        gameClearShown = true;
        GameClearUI.Show(skipToEnd);
    }

    // 헬기가 다 떠나 그림자가 사라지면 이 헬기의 움직임/소리를 멈춥니다(플레이어는 화면 밖에 멈춘 채, 결과 화면만 남음).
    private void StopAfterGameClear()
    {
        playing = false;
        ApplyVisualsVisible(false);
        GameAudio.StopLoop(ref rotorLoopHandle);
    }

    private void UpdateExtraction(HeliExtractionSettings s)
    {
        if (player == null)
        {
            FinishExtraction();
            return;
        }
        float dt = Time.deltaTime;
        phaseTimer += dt;
        EnforceLock(extractPhase != ExtractPhase.Approach);

        // [게임 클리어 퇴각] ESC = 결과 화면을 바로(완성된 모습으로). 결과 화면이 이미 떠 있으면 그 화면이 ESC를 받음.
        if (gameClearMode)
        {
            if (!gameClearShown && Input.GetKeyDown(KeyCode.Escape)) ShowGameClear(true);
        }
        // 건너뛰기: 바로 검게 하고 다음 레벨로
        else if (!fading && s.skipKeys != null)
        {
            foreach (var key in s.skipKeys)
            {
                if (key != KeyCode.None && Input.GetKeyDown(key))
                {
                    StartFade(s.skipFadeDuration);
                    break;
                }
            }
        }

        switch (extractPhase)
        {
            case ExtractPhase.Approach: UpdateApproach(s); break;
            case ExtractPhase.Jump: UpdateJump(s); break;
            case ExtractPhase.Hang: UpdateHang(s, dt); break;
        }

        if (fading)
        {
            fadeTimer += Time.unscaledDeltaTime;
            SetOverlay(fadeTime <= 0f ? 1f : Mathf.Clamp01(fadeTimer / fadeTime));
            if (overlayAlpha >= 1f) FinishExtraction();
        }
    }

    // 1) 사다리 앞으로 걸어감(PlayerMovement가 ScriptedMoveDirection으로 걸음 - 걷기 애니메이션/중력 그대로)
    private void UpdateApproach(HeliExtractionSettings s)
    {
        Vector3 pos = player.transform.position;
        Vector3 ladder = Flat(LadderBottom);
        Vector3 toLadder = ladder - Flat(pos);
        Vector3 fromLadder = toLadder.sqrMagnitude > 0.0001f ? -toLadder.normalized : -travelDir;
        Vector3 stand = ladder + fromLadder * s.approachDistance;
        Vector3 toStand = stand - Flat(pos);
        float dist = toStand.magnitude;

        if (dist <= 0.05f || phaseTimer >= s.approachTimeout)
        {
            BeginJump(s);
            return;
        }
        var stats = player.GetComponent<PlayerStats>();
        float speed = stats != null ? Mathf.Max(0.01f, stats.MoveSpeed) : 5f;
        moveDir = (toStand / dist) * Mathf.Clamp01(dist / (speed * Mathf.Max(Time.deltaTime, 0.0001f)));
        if (toLadder.sqrMagnitude > 0.0001f)
        {
            player.transform.rotation = Quaternion.Slerp(player.transform.rotation,
                Quaternion.LookRotation(toLadder.normalized), s.approachTurnSpeed * Time.deltaTime);
        }
    }

    // 2) 점프해서 붙잡음: 이동 컴포넌트와 CharacterController를 끄고 포물선으로 직접 옮김
    private void BeginJump(HeliExtractionSettings s)
    {
        moveDir = Vector3.zero;
        TryDisable(player.GetComponent<PlayerMovement>());
        if (playerController != null)
        {
            controllerWasEnabled = playerController.enabled;
            playerController.enabled = false;
        }
        jumpStart = player.transform.position;
        // 매달린 자세(기존 획득 모션)로 섞여 들어감
        SetAnimBool(s.hangBool, true);
        CrossFadeUpper(s.hangStateName, s.hangBlendIn);
        SetAnimFloat("Speed", 0f); SetAnimFloat("MoveX", 0f); SetAnimFloat("MoveZ", 0f);
        phaseTimer = 0f;
        extractPhase = ExtractPhase.Jump;
    }

    private void UpdateJump(HeliExtractionSettings s)
    {
        float t = Mathf.Clamp01(phaseTimer / Mathf.Max(0.05f, s.jumpDuration));
        Vector3 target = HangPosition(s);
        float e = Mathf.SmoothStep(0f, 1f, t);
        player.transform.position = Vector3.Lerp(jumpStart, target, e) + Vector3.up * (s.jumpArcHeight * 4f * t * (1f - t));
        FaceHangDirection(s, 12f);
        if (t < 1f) return;

        // 붙잡는 순간: 붙잡은 곳 아래쪽을 아래로 출렁이게 합니다(무게가 실린 반동).
        for (int i = grabIndex; i < points.Length; i++)
        {
            float k = 1f - 0.5f * (i - grabIndex) / Mathf.Max(1, points.Length - grabIndex);
            prevPoints[i] += Vector3.up * (s.grabJolt * k * SimStep);
        }
        for (int i = 1; i < grabIndex; i++)
        {
            float k = (float)i / grabIndex;
            prevPoints[i] += Vector3.up * (s.grabJolt * k * SimStep);
        }
        phaseTimer = 0f;
        extractPhase = ExtractPhase.Hang;
    }

    // 3) 매달림: 플레이어는 붙잡은 마디를 따라 움직이며 반동만 받음 -> 사다리 자체가 끌어올려짐 -> 화면이 검게
    private void UpdateHang(HeliExtractionSettings s, float dt)
    {
        // [게임 클리어 퇴각] 사다리가 다 걷힌 뒤(헬기 출발)에는 플레이어를 그 자리(화면 밖 하늘)에 멈춰 둡니다.
        bool frozen = gameClearMode && departWait >= 0f;
        if (!frozen)
        {
            player.transform.position = HangPosition(s);
            FaceHangDirection(s, 8f);
        }

        if (!lifting && phaseTimer >= s.liftDelay)
        {
            lifting = true;
            phaseTimer = 0f;
            if (gameClearMode && !gameClearShown) ShowGameClear(false); // 끌어올려지기 시작하는 순간 결과 화면
        }
        if (lifting)
        {
            liftSpeed = Mathf.Min(s.liftMaxSpeed, liftSpeed + s.liftAcceleration * dt);
            liftOffset += liftSpeed * dt;
            float departTime = UpdateDepart(s, dt); // 사다리가 다 걷힌 뒤에야 헬기가 출발
            if (gameClearMode)
            {
                // 화면을 검게 만들지 않고, 도착 연출이 끝나고 떠날 때처럼 헬기 그림자가 흐려지며 사라짐
                leaveFade = 1f - Mathf.Clamp01(departTime / Mathf.Max(0.1f, s.leaveDuration));
                if (leaveFade <= 0f) StopAfterGameClear();
            }
            else if (!fading && phaseTimer >= s.fadeStartAfterLift) StartFade(s.fadeDuration);
        }
    }

    private Vector3 HangPosition(HeliExtractionSettings s)
    {
        int i = Mathf.Clamp(grabIndex, 0, points.Length - 1);
        return points[i] + Vector3.down * s.hangOffset;
    }

    private void FaceHangDirection(HeliExtractionSettings s, float speed)
    {
        Vector3 dir;
        if (s.faceCameraWhileHanging)
        {
            dir = lockedFollow != null ? Flat(lockedFollow.offset) : (lockedCam != null ? Flat(-lockedCam.transform.forward) : -travelDir);
        }
        else dir = Flat(LadderBottom - player.transform.position);
        if (dir.sqrMagnitude < 0.0001f) return;
        player.transform.rotation = Quaternion.Slerp(player.transform.rotation, Quaternion.LookRotation(dir.normalized), speed * Time.deltaTime);
    }

    private void StartFade(float duration)
    {
        fading = true;
        fadeTimer = overlayAlpha * duration; // 이미 어두운 만큼은 건너뜀
        fadeTime = duration;
        if (duration <= 0f) SetOverlay(1f);
    }

    // 4) 완전히 검어짐: 검은 화면 뒤에서 플레이어를 모두 원래대로 돌리고 다음 레벨로(로딩 화면이 바로 위를 덮음)
    private void FinishExtraction()
    {
        var done = onExtracted;
        onExtracted = null;
        if (player != null)
        {
            // 다음 레벨에서 LevelBuilder가 시작방으로 옮기지만, 그 전까지 공중에 떠 있지 않도록 헬기 자리 바닥에 둡니다.
            bool cc = playerController != null && playerController.enabled;
            if (cc) playerController.enabled = false;
            player.transform.position = home;
            if (cc) playerController.enabled = true;
        }
        RestorePlayer();
        usedForExtraction = true;
        ArriveByHeliPending = true;
        done?.Invoke();
        // 로딩 화면이 이미 덮었으면 검은 막을 바로 걷습니다(이 오브젝트는 다음 레벨을 만들 때 함께 지워짐).
        if (LoadingScreen.IsShowing || done == null) SetOverlay(0f);
    }

    // 플레이어/카메라/HUD/총/애니메이션을 원래대로 돌립니다(정상 종료, 중단 모두).
    private void RestorePlayer()
    {
        var s = HeliExtractionSettings.Get();
        extractPhase = ExtractPhase.None;
        moveDir = Vector3.zero;
        if (active == this) active = null;

        RestoreGun();
        SetAnimBool(s.hangBool, false);
        if (playerAnimator != null && playerAnimator.runtimeAnimatorController != null)
        {
            int layer = playerAnimator.GetLayerIndex("UpperBody");
            int hash = Animator.StringToHash("UpperBody.NonAimIdle");
            if (layer >= 0 && playerAnimator.HasState(layer, hash)) playerAnimator.Play(hash, layer, 0f);
        }
        if (playerController != null && controllerWasEnabled) playerController.enabled = true;
        controllerWasEnabled = false;

        bool alive = player != null && !player.IsDead;
        foreach (var b in disabledBehaviours) if (b != null && alive) b.enabled = true;
        disabledBehaviours.Clear();

        if (lockedFollow != null && followWasEnabled) lockedFollow.enabled = true;
        lockedFollow = null;
        lockedCam = null;
        followWasEnabled = false;
        SetHudAlpha(1f);
        hudGroup = null;
    }

    // ---- 조작 잠금 (입장 컷씬과 같은 목록) ----
    private void EnforceLock(bool includeMovement)
    {
        if (player == null) return;
        GameObject go = player.gameObject;
        var aim = go.GetComponent<PlayerAimAndAttack>();
        if (aim != null && aim.enabled)
        {
            SetAnimBool("IsAiming", false);
            SetAnimFloat("AimBlend", 0f);
        }
        TryDisable(aim);
        TryDisable(go.GetComponent<PlayerShooting>());
        TryDisable(go.GetComponent<PlayerWeaponMode>());
        TryDisable(go.GetComponent<PlayerActiveItem>());
        TryDisable(go.GetComponent<PlayerDisposableItem>());
        TryDisable(go.GetComponent<PlayerFoodItem>());
        var grenade = go.GetComponent<PlayerGrenadeThrower>();
        if (grenade != null && !grenade.IsThrowing) TryDisable(grenade);
        var hack = go.GetComponent<PlayerHackTool>();
        if (hack != null && !hack.IsDeploying) TryDisable(hack);
        if (includeMovement) TryDisable(go.GetComponent<PlayerMovement>());
    }

    private void TryDisable(Behaviour b)
    {
        if (b == null || !b.enabled) return;
        b.enabled = false;
        if (!disabledBehaviours.Contains(b)) disabledBehaviours.Add(b);
    }

    // ---- 총 숨기기 (예전 "등에 메기" 대체 - 형빈 확정: 가방과 겹쳐서 연출 동안 총을 숨김) ----
    private void HideGun()
    {
        RestoreGun(); // 이미 숨긴 총이 있으면 먼저 되돌려 상태가 꼬이지 않게 합니다.
        GameObject g = FindPlayerGun();
        if (g == null) return;
        hiddenGun = g;
        hiddenGunWasActive = g.activeSelf;
        if (g.activeSelf) g.SetActive(false);
    }

    private void RestoreGun()
    {
        if (hiddenGun != null && hiddenGunWasActive && !hiddenGun.activeSelf) hiddenGun.SetActive(true);
        hiddenGun = null;
        hiddenGunWasActive = false;
    }

    // 숨길 총: PlayerWeaponMode의 Gun Model 칸 → 설정의 이름 → 오른손 뼈 아래 첫 그림 있는 오브젝트 순서로 찾습니다.
    private GameObject FindPlayerGun()
    {
        if (player != null)
        {
            var mode = player.GetComponent<PlayerWeaponMode>();
            if (mode != null && mode.gunModel != null) return mode.gunModel;
        }

        if (playerAnimator == null || !playerAnimator.isHuman) return null;
        Transform hand = playerAnimator.GetBoneTransform(HumanBodyBones.RightHand);
        if (hand == null) return null;

        var s = HeliExtractionSettings.Get();
        Transform found = !string.IsNullOrEmpty(s.gunObjectName) ? FindChildByName(hand, s.gunObjectName) : null;
        if (found == null)
        {
            foreach (Transform c in hand)
            {
                if (c.GetComponentInChildren<Renderer>() != null) { found = c; break; }
            }
        }
        if (found == null) Debug.LogWarning("HeliExtraction: 숨길 총을 찾지 못했습니다(PlayerWeaponMode의 Gun Model 칸 확인).");
        return found != null ? found.gameObject : null;
    }

    private static Transform FindChildByName(Transform root, string name)
    {
        foreach (Transform c in root)
        {
            if (c.name == name) return c;
            var found = FindChildByName(c, name);
            if (found != null) return found;
        }
        return null;
    }

    // ---- Animator 도우미 ----
    private bool HasAnimParam(string name, AnimatorControllerParameterType type)
    {
        if (playerAnimator == null || playerAnimator.runtimeAnimatorController == null || string.IsNullOrEmpty(name)) return false;
        foreach (var p in playerAnimator.parameters) if (p.name == name && p.type == type) return true;
        return false;
    }
    private void SetAnimBool(string n, bool v) { if (HasAnimParam(n, AnimatorControllerParameterType.Bool)) playerAnimator.SetBool(n, v); }
    private void SetAnimFloat(string n, float v) { if (HasAnimParam(n, AnimatorControllerParameterType.Float)) playerAnimator.SetFloat(n, v); }

    private void CrossFadeUpper(string stateName, float duration)
    {
        if (playerAnimator == null || playerAnimator.runtimeAnimatorController == null) return;
        int layer = playerAnimator.GetLayerIndex("UpperBody");
        if (layer < 0) return;
        int hash = Animator.StringToHash("UpperBody." + stateName);
        if (!playerAnimator.HasState(layer, hash))
        {
            Debug.LogWarning($"HeliExtraction: Animator의 UpperBody 레이어에서 '{stateName}' 상태를 찾지 못했습니다.");
            return;
        }
        if (duration <= 0f) playerAnimator.Play(hash, layer, 0f);
        else playerAnimator.CrossFadeInFixedTime(hash, duration, layer, 0f);
    }

    // ---- HUD / 검은 막 ----
    private void SetHudAlpha(float alpha)
    {
        if (hudGroup == null)
        {
            var hud = Object.FindFirstObjectByType<PlayerHUDManager>();
            if (hud == null || hud.CanvasRoot == null) return;
            Canvas c = hud.CanvasRoot.GetComponentInParent<Canvas>();
            GameObject root = c != null ? c.rootCanvas.gameObject : hud.CanvasRoot.gameObject;
            hudGroup = root.GetComponent<CanvasGroup>();
            if (hudGroup == null) hudGroup = root.AddComponent<CanvasGroup>();
        }
        hudGroup.alpha = Mathf.Clamp01(alpha);
    }

    private void SetOverlay(float alpha)
    {
        overlayAlpha = Mathf.Clamp01(alpha);
        if (overlayCanvas == null)
        {
            if (overlayAlpha <= 0f) return;
            var go = new GameObject("HeliExtractionOverlay");
            go.transform.SetParent(transform, false);
            overlayCanvas = go.AddComponent<Canvas>();
            overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            overlayCanvas.sortingOrder = HeliExtractionSettings.Get().overlaySortingOrder;
            var img = new GameObject("Black");
            img.transform.SetParent(go.transform, false);
            var rt = img.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            overlayImage = img.AddComponent<UnityEngine.UI.Image>();
            overlayImage.raycastTarget = false;
        }
        overlayCanvas.gameObject.SetActive(overlayAlpha > 0f);
        overlayImage.color = new Color(0f, 0f, 0f, overlayAlpha);
    }

    private static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v;
    }

    // ------------------------------------------------------------------
    // 공통
    // ------------------------------------------------------------------

    private static void SetupRenderer(MeshRenderer r, Material mat)
    {
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
    }

    // [방 전환] 이 헬기가 있는 방에 플레이어가 있는지입니다(방을 판단할 수 없으면 true - 항상 보임).
    private bool IsPlayerInHeliRoom()
    {
        return RoomScope.IsPlayerInRoom(RoomScope.RoomOf(transform.position));
    }

    // [방 전환] 별도 오브젝트로 만든 그림(그림자/사다리/먼지 물결)을 한꺼번에 숨기거나 보입니다. 그림 오브젝트만 켜고 끄므로
    // 사다리 계산/연출 진행에는 영향이 없습니다.
    private void ApplyVisualsVisible(bool visible)
    {
        SetVisualActive(shadowGO, visible);
        SetVisualActive(ladderGO, visible);
        for (int i = 0; i < dustPulses.Count; i++) SetVisualActive(dustPulses[i].go, visible);
    }

    // [사운드] 그림자가 보이는 동안만 로터 소리를 냅니다(다른 방/로딩 화면 뒤에서 기다리는 동안/연출이 끝나면 끔).
    private void UpdateRotorSound()
    {
        bool audible = playing && !arrivalWaitingForLoad && shadowGO != null && shadowGO.activeInHierarchy;
        if (audible)
        {
            if (!GameAudio.IsLoopPlaying(rotorLoopHandle)) rotorLoopHandle = GameAudio.PlayLoop(SfxId.HeliRotor, false);
        }
        else if (rotorLoopHandle != 0)
        {
            GameAudio.StopLoop(ref rotorLoopHandle);
        }
    }

    private static void SetVisualActive(GameObject go, bool active)
    {
        if (go != null && go.activeSelf != active) go.SetActive(active);
    }

    private void Cleanup()
    {
        GameAudio.StopLoop(ref rotorLoopHandle);
        playing = false;
        arrivalWaitingForLoad = false;
        arrivalHoldLadder = false;
        if (shadowGO != null) Destroy(shadowGO);
        shadowGO = null;
        foreach (var p in dustPulses) if (p.go != null) Destroy(p.go);
        dustPulses.Clear();
        if (ladderGO != null) Destroy(ladderGO);
        ladderGO = null;
        points = null;
        ladderDropped = false;
    }
}
