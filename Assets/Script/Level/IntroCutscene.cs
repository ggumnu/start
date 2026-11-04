using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// [입장 컷씬 - 게임을 처음 시작할 때만 (형빈 확정)]
// 시작 화면의 [게임 시작]으로 들어왔을 때만 나옵니다. ESC 중단 > 처음부터, 게임 오버 재시작, R 꾹 누르기 재시작(모두 지금 씬을 다시 불러옴),
// 다음 레벨 이동에서는 나오지 않습니다. 별도 씬이 아니라 게임 씬 안에서 진행되고, 끝나면 그대로 플레이가 시작됩니다.
//
// [진행 순서]
// 1. 로딩 화면이 걷히기 시작하면 검은 화면 뒤에서 먼저 걷기 시작하고, 잠시 뒤 점점 밝아집니다(보이는 순간 이미 걷는 중, HUD는 숨김).
// 2. 플레이어가 방 정중앙 바깥(설정 위치)에서 조작 없이 정중앙까지 걸어옵니다. 카메라는 낮은 높이에서 사선으로 플레이어를 따라가며,
//    다가올수록 점점 가까워집니다.
// 3. 정중앙 도착 -> 장전(1.5초, 장전 표정).
// 4. 카메라 쪽을 보며 획득 모션/표정. 획득 시작 1초 뒤부터 카메라가 플레이어 주위를 나선형으로 돌며 올라가 게임 시점에 정확히 도착합니다.
//    도는 동안 시선은 계속 플레이어에 맞춰져 있고, 끝에서 게임 카메라의 시선과 정확히 같아집니다.
//    획득 유지 시간(2초)이 지나면 천천히 대기 모션으로 돌아가며 몸도 게임 카메라 쪽으로 돌립니다.
// 5. 카메라 도착 + 대기 복귀가 모두 끝나면 조작이 풀리고 HUD가 나타납니다.
// 장전/획득은 PlayerShooting/PlayerItemPickupPresenter를 거치지 않고 Animator와 표정(SDFaceController)만 직접 움직입니다
// (탄약은 그대로, 총은 숨기지 않음, 머리 위 아이콘 없음).
//
// [건너뛰기] 걷기부터 끝까지 Space/ESC/Enter: 잠깐 검게 -> 정중앙, 대기 자세, 게임 카메라, HUD 상태로 바로 맞춤 -> 다시 밝아짐.
//
// [처음 시작 판별]
// TitleScreenUI가 [게임 시작] 순간 RequestOnNextGameStart()를 부르면, 다음에 열리는 씬에서 한 번만 컷씬을 겁니다.
// 재시작 경로들은 이 함수를 부르지 않으므로 컷씬이 나오지 않습니다. 에디터 테스트용으로 설정 에셋의 Editor Always Play를 켜면
// Play를 누른 뒤 처음 열린 씬에서도 한 번 나옵니다.
//
// [조작 잠금 - 기존 스크립트를 최소한으로만 건드림]
// - 사격/조준/무기 전환/수류탄/해킹/액티브/일회용/음식: 로딩 화면(LoadingScreen)과 같은 방식으로 컴포넌트를 껐다가, 끝나면 이 스크립트가 끈 것만
//   다시 켭니다. 로딩 화면이 사라지면서 자기가 끈 것을 다시 켜도, 이 스크립트가 매 프레임(다른 스크립트보다 늦게) 다시 꺼서 컷씬 동안 계속 잠급니다.
// - 이동: PlayerMovement가 OverridesMovement를 보고 키 입력 대신 ScriptedMoveDirection으로 걷습니다(걷기 애니메이션/중력 그대로).
// - ESC 중단 화면(PauseMenuUI), R 꾹 누르기 재시작(HoldRestart): BlocksInput을 보고 열리지 않습니다.
// - 몸 방향: 조준 컴포넌트를 꺼 두므로 이 스크립트가 직접 돌립니다.
//
// [카메라 / 화면 / HUD - 다른 스크립트는 고치지 않음]
// - 카메라: 컷씬 동안 Main Camera의 CameraFollow를 끄고 이 스크립트가 직접 움직입니다. 끝 지점은 CameraFollow의 실제 값
//   (대상 위치 + Offset, Fixed Euler Angles)으로 계산하므로, 다시 켜는 순간 화면이 튀지 않습니다.
// - 밝아지는 연출: 로딩 화면은 그대로 두고, 그 아래(정렬 순서 998)에 이 스크립트의 검은 막을 미리 깔아둡니다. 로딩 화면이 사라진 뒤
//   이 막을 천천히 걷습니다.
// - HUD: PlayerHUDManager의 Canvas에 CanvasGroup을 붙여 투명하게 숨겼다가 끝나면 다시 보이게 합니다.
//
// [컷씬 없이 시작할 때의 방향]
// 컷씬이 없는 레벨 1 시작(재시작 포함)과 건너뛰기 직후에는 플레이어가 카메라 쪽(게임 카메라 오프셋의 수평 방향 = X+와 Z- 사이)을 바라봅니다.
// LevelBuilder는 고치지 않고, 플레이어가 처음 생긴 순간 이 스크립트가 방향만 돌립니다(설정의 Face Camera On Start).
//
// [설정]
// 씬에 붙일 필요가 없습니다 - 게임이 시작되면 스스로 만들어집니다(씬이 바뀌어도 유지). 값은 IntroCutsceneSettings 에셋(Resources).
[DefaultExecutionOrder(1000)] // 로딩 화면이 조작 컴포넌트를 다시 켠 같은 프레임에 바로 다시 끄기 위해 늦게 실행
public class IntroCutscene : MonoBehaviour
{
    private enum Phase { Idle, WaitLoading, Walking, Arrived, Reloading, Pickup, Skipping }

    private static IntroCutscene instance;
    private static bool pendingFromTitle;   // [게임 시작]으로 들어옴 -> 다음 씬에서 한 번 재생
    private static bool firstSceneHandled;  // Play 후 처음 열린 씬을 이미 처리했는지(에디터 테스트용)

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        pendingFromTitle = false;
        firstSceneHandled = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null) return;
        var go = new GameObject("IntroCutscene");
        DontDestroyOnLoad(go);
        go.AddComponent<IntroCutscene>();
        // 처음 열린 씬은 sceneLoaded 신호가 이 오브젝트보다 먼저 지나갔으므로 직접 처리합니다.
        instance.HandleSceneReady();
    }

    // 시작 화면의 [게임 시작]에서 부릅니다. 다음에 열리는 게임 씬에서 한 번만 컷씬이 나옵니다.
    public static void RequestOnNextGameStart()
    {
        pendingFromTitle = true;
    }

    // ---------------- 다른 스크립트가 읽는 상태 ----------------

    // 컷씬이 진행 중(건너뛰기 검은 화면이 걷힐 때까지 포함)이면 true - ESC 중단 화면, R 꾹 누르기 재시작을 막습니다.
    public static bool BlocksInput => instance != null && instance.phase != Phase.Idle;
    public static bool IsPlaying => BlocksInput;
    // true인 동안 PlayerMovement는 키 입력 대신 ScriptedMoveDirection(월드 기준, 길이 0~1)으로 움직입니다.
    public static bool OverridesMovement => instance != null && instance.phase != Phase.Idle && instance.player != null;
    public static Vector3 ScriptedMoveDirection => instance != null ? instance.moveDir : Vector3.zero;

    // ---------------- 내부 상태 ----------------

    private const float ArriveEpsilon = 0.005f; // 이 거리(m) 안이면 도착(눈에 보이지 않는 차이)

    private Phase phase = Phase.Idle;
    private bool armed;              // 이 씬에서 컷씬을 시작해야 함(플레이어가 생기기를 기다리는 중)
    private bool faceCameraPending;  // 컷씬이 없는 시작: 플레이어가 생기면 카메라 쪽으로 돌림
    private PlayerHealth player;
    private CharacterController controller;
    private Animator animator;
    private SDFaceController face;
    private readonly HashSet<SDFaceExpression> heldFaces = new HashSet<SDFaceExpression>();
    private Vector3 center;          // 방 정중앙(원래 시작 위치)
    private Vector3 walkDir;         // 걸어오는 방향(시작 위치 -> 정중앙, 수평)
    private float groundY;           // 바닥 높이(플레이어가 착지한 높이)
    private float walkLength;        // 시작 위치 -> 정중앙 거리
    private float lastWalkDist = float.MaxValue;
    private float animMove;          // 도착 뒤 걷기 -> 멈춤 다리 자세를 서서히 줄이는 값(1 = 걷는 중, 0 = 멈춤)
    private bool revealStarted;      // 밝아지기 시작했는지
    private float frontTurnTimer = -1f; // 사선 -> 정면으로 도는 진행 시간(-1 = 아직 시작 안 함)
    private Vector3 moveDir;
    private float timer;
    private bool returnStarted;      // 획득 포즈 -> 대기 복귀를 시작했는지
    private bool fullBodyPosing;     // 전신 '짜잔' 포즈(다리/몸통 레이어까지)를 재생 중인지
    private float returnTimer;
    private readonly List<Behaviour> disabledBehaviours = new List<Behaviour>();

    // 카메라
    private Camera cam;
    private CameraFollow follow;
    private bool followWasEnabled;
    private bool camSnapped;         // 첫 장면 시선을 한 번 바로 맞췄는지
    private bool spiralStarted;
    private Vector3 spiralStartPos;
    private Quaternion turnFrom;     // 몸 돌리기 시작 방향
    private Quaternion turnTo;       // 몸 돌리기 목표 방향(획득 포즈: 첫 장면 카메라의 최종 위치 쪽 / 복귀: 게임 카메라 쪽)

    // 화면/HUD
    private Canvas canvas;
    private Image overlayImage;
    private float overlayAlpha;
    private Coroutine introFadeRoutine;
    private CanvasGroup hudGroup;

    void Awake()
    {
        if (instance == null) instance = this;
        else if (instance != this)
        {
            Destroy(gameObject);
            return;
        }
        BuildUI();
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    void OnDestroy()
    {
        if (instance != this) return;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        instance = null;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single) return;
        HandleSceneReady();
    }

    // 새 씬이 열릴 때마다: 진행 중이던 것은 정리하고, 이번 씬에서 컷씬을 걸지 정합니다.
    private void HandleSceneReady()
    {
        StopAllCoroutines();
        introFadeRoutine = null;
        if (phase != Phase.Idle) AbortCutscene();
        ApplyOverlay(0f);

        bool play = false;
        if (pendingFromTitle)
        {
            play = true;
        }
#if UNITY_EDITOR
        else if (!firstSceneHandled && IntroCutsceneSettings.Get().editorAlwaysPlay)
        {
            play = true;
        }
#endif
        firstSceneHandled = true;

        // 시작 화면처럼 레벨이 없는 씬이면 [게임 시작] 요청은 다음 씬까지 남겨둡니다.
        if (Object.FindFirstObjectByType<LevelBuilder>() == null)
        {
            armed = false;
            faceCameraPending = false;
            return;
        }
        pendingFromTitle = false;
        armed = play;
        faceCameraPending = !play && IntroCutsceneSettings.Get().faceCameraOnStart;
    }

    void Update()
    {
        var s = IntroCutsceneSettings.Get();

        if (faceCameraPending) TryFaceCameraOnStart();
        if (armed && phase == Phase.Idle) TryBegin(s);
        if (phase == Phase.Idle) return;

        // 플레이어가 사라졌거나(씬 정리) 죽으면 바로 끝냅니다.
        if (player == null || player.IsDead)
        {
            StopAllCoroutines();
            introFadeRoutine = null;
            AbortCutscene();
            ApplyOverlay(0f);
            return;
        }

        if (phase == Phase.Skipping) return;

        EnforceLock();
        if (controller != null && controller.isGrounded) groundY = player.transform.position.y;

        switch (phase)
        {
            case Phase.WaitLoading:
                moveDir = Vector3.zero;
                // 로딩 화면이 사라지기 시작하면(착지 완료) 검은 막 뒤에서 먼저 걷기 시작합니다.
                if (!LoadingScreen.BlocksPlayerInput)
                {
                    phase = Phase.Walking;
                    timer = 0f;
                    revealStarted = false;
                }
                break;

            case Phase.Walking:
                // 로딩 화면이 완전히 사라졌고 정해진 시간만큼 걸었으면 밝아지기 시작합니다(보이는 순간 이미 걷는 중).
                if (!revealStarted && !LoadingScreen.IsShowing && timer >= s.revealAfterWalkStart)
                {
                    revealStarted = true;
                    introFadeRoutine = StartCoroutine(Fade(1f, 0f, s.introFadeIn));
                }
                if (revealStarted && CheckSkip(s)) return;
                UpdateWalk(s);
                break;

            case Phase.Arrived:
                if (CheckSkip(s)) return;
                moveDir = Vector3.zero;
                timer += Time.deltaTime;
                if (timer >= s.arriveHold) BeginReload(s);
                break;

            case Phase.Reloading:
                if (CheckSkip(s)) return;
                moveDir = Vector3.zero;
                timer += Time.deltaTime;
                if (timer >= s.reloadDuration) BeginPickup(s);
                break;

            case Phase.Pickup:
                if (CheckSkip(s)) return;
                UpdatePickup(s);
                break;
        }

        UpdateStopBlend(s);
    }

    // 카메라는 다른 모든 움직임이 끝난 뒤에 맞춥니다.
    void LateUpdate()
    {
        if (phase == Phase.Idle || phase == Phase.Skipping) return;
        if (player == null || cam == null) return;
        UpdateCamera(IntroCutsceneSettings.Get());
    }

    // ------------------------------------------------------------------
    // 시작
    // ------------------------------------------------------------------

    private void TryBegin(IntroCutsceneSettings s)
    {
        // LevelBuilder가 시작방 정중앙(1m 위)에 플레이어를 만든 직후를 기다립니다.
        var found = Object.FindFirstObjectByType<PlayerHealth>();
        if (found == null || found.IsDead) return;

        armed = false;
        player = found;
        controller = player.GetComponent<CharacterController>();
        animator = player.GetComponentInChildren<Animator>();
        face = player.GetComponentInChildren<SDFaceController>(true);
        heldFaces.Clear();
        returnStarted = false;
        fullBodyPosing = false;
        spiralStarted = false;
        camSnapped = false;
        frontTurnTimer = -1f;
        lastWalkDist = float.MaxValue;

        // 아직 떨어지기 전이라 지금 위치가 곧 시작방 정중앙입니다(1m 위에서 생성되므로 바닥은 대략 1m 아래 - 착지하면 다시 잼).
        center = player.transform.position;
        groundY = center.y - 1f;

        var builder = Object.FindFirstObjectByType<LevelBuilder>();
        float half = builder != null ? builder.cellSize * 0.5f : 5f;
        if (Mathf.Abs(s.walkStartOffset.x) > half - 0.3f || Mathf.Abs(s.walkStartOffset.y) > half - 0.3f)
        {
            Debug.LogWarning($"IntroCutscene: 걷기 시작 위치({s.walkStartOffset})가 방 칸 경계({half}m)에 너무 가깝습니다. " +
                             "옆 방으로 넘어간 것으로 처리될 수 있으니 줄여주세요.");
        }

        Vector3 start = center + new Vector3(s.walkStartOffset.x, 0f, s.walkStartOffset.y);
        Teleport(start);

        walkDir = Flat(center - start);
        walkLength = walkDir.magnitude;
        walkDir = walkDir.sqrMagnitude > 0.0001f ? walkDir.normalized : Vector3.forward;
        player.transform.rotation = Quaternion.LookRotation(walkDir);

        // 카메라를 넘겨받고, 화면을 검게 덮고, HUD를 숨깁니다(아직 로딩 화면 아래라 보이지 않음).
        cam = Camera.main;
        follow = cam != null ? cam.GetComponent<CameraFollow>() : null;
        followWasEnabled = follow != null && follow.enabled;
        if (follow != null) follow.enabled = false;
        if (cam == null) Debug.LogWarning("IntroCutscene: Main Camera를 찾지 못해 카메라 연출 없이 진행합니다.");

        ApplyOverlay(1f);
        SetHudVisible(0f);

        disabledBehaviours.Clear();
        EnforceLock();
        moveDir = Vector3.zero;
        phase = Phase.WaitLoading;
        timer = 0f;
    }

    // ------------------------------------------------------------------
    // 걷기
    // ------------------------------------------------------------------

    private void UpdateWalk(IntroCutsceneSettings s)
    {
        timer += Time.deltaTime;
        float walkTime = timer;

        Vector3 toCenter = Flat(center - player.transform.position);
        float dist = toCenter.magnitude;

        // 도착: 순간 이동 없이 끝까지 걸어서 정중앙에 멈춥니다(마지막 프레임은 남은 거리만큼만 걸음 - 아래 moveDir 계산).
        // 예전에는 8cm 안에 들어오면 정중앙으로 순간 이동시켜서, 도착 순간 플레이어와 카메라 시선이 툭 튀었습니다.
        // 너무 오래 걸릴 때(무언가에 막힘)만 정중앙으로 옮깁니다.
        // 바닥 충돌 등으로 마지막 몇 mm를 못 가고 멈춰 있으면(3cm 이내) 그 자리를 도착으로 봅니다(순간 이동 없음).
        bool stuckNear = dist <= 0.03f && lastWalkDist - dist < 0.0002f;
        lastWalkDist = dist;
        bool arrived = dist <= ArriveEpsilon || stuckNear;
        bool timedOut = walkTime >= s.walkTimeout;
        if (arrived || timedOut)
        {
            moveDir = Vector3.zero;
            if (timedOut && !arrived)
            {
                Debug.LogWarning("IntroCutscene: 정해진 시간 안에 정중앙에 도착하지 못해 바로 옮깁니다(길에 막힌 것이 있는지 확인해주세요).");
                Teleport(new Vector3(center.x, player.transform.position.y, center.z));
            }
            // 아직 밝아지기 전에 도착했으면(걷는 거리가 아주 짧은 경우) 바로 밝아지기 시작합니다.
            if (!revealStarted)
            {
                revealStarted = true;
                if (introFadeRoutine != null) StopCoroutine(introFadeRoutine);
                introFadeRoutine = StartCoroutine(Fade(overlayAlpha, 0f, s.introFadeIn));
            }
            animMove = 1f; // 여기서부터 걷기 다리 자세를 서서히 줄입니다(UpdateStopBlend)
            phase = Phase.Arrived;
            timer = 0f;
            return;
        }

        Vector3 dir = toCenter / dist;
        // 마지막 한 프레임에 지나치지 않도록 남은 거리만큼만 걷습니다(PlayerMovement가 MoveSpeed를 곱함).
        var stats = player.GetComponent<PlayerStats>();
        float speed = stats != null ? Mathf.Max(0.01f, stats.MoveSpeed) : 5f;
        float step = speed * Mathf.Max(Time.deltaTime, 0.0001f);
        // 도착 직전에는 걸음을 점점 줄여서 걷기 -> 멈춤(Animator Speed/MoveZ)이 부드럽게 이어지게 합니다.
        float slow = s.arriveSlowDistance > 0f ? Mathf.Clamp(dist / s.arriveSlowDistance, s.arriveMinSpeed, 1f) : 1f;
        moveDir = dir * Mathf.Min(slow, Mathf.Clamp01(dist / step));

        Quaternion target = Quaternion.LookRotation(dir);
        player.transform.rotation = Quaternion.Slerp(player.transform.rotation, target, s.turnSpeed * Time.deltaTime);
    }

    // ------------------------------------------------------------------
    // 장전 -> 획득 포즈(+ 카메라 나선 상승) -> 대기로 복귀
    // ------------------------------------------------------------------

    private void BeginReload(IntroCutsceneSettings s)
    {
        phase = Phase.Reloading;
        timer = 0f;

        // 장전 클립을 정해진 시간에 맞춰 재생합니다(PlayerShooting과 같은 계산: 클립 길이 / 장전 시간).
        var shooting = player.GetComponent<PlayerShooting>();
        float clipLength = shooting != null && shooting.reloadClip != null ? shooting.reloadClip.length : 0f;
        float speed = clipLength > 0f ? clipLength / Mathf.Max(0.1f, s.reloadDuration) : 1f;
        SetFloat(s.reloadSpeedFloat, speed);
        // 기본 전이(0.1초) 대신 정해진 시간만큼 섞여 장전 상태로 들어갑니다. Bool도 켜 두어 상태에 머물게 합니다.
        CrossFadeUpper(s, s.reloadStateName, s.reloadBlendIn);
        SetBool(s.reloadBool, true);
        HoldFace(SDFaceExpression.Reload);
    }

    private void BeginPickup(IntroCutsceneSettings s)
    {
        phase = Phase.Pickup;
        timer = 0f;
        returnStarted = false;
        returnTimer = 0f;
        spiralStarted = false;

        // 장전 -> 획득도 정해진 시간만큼 섞여 넘어갑니다(Trigger 대신 직접 지정 - Any State 전이가 겹쳐 다시 시작되지 않도록).
        // 전신 '짜잔' 포즈: 다리/몸통(Locomotion)과 팔/머리(UpperBody) 두 레이어를 같은 포즈 상태로 함께 섞어 넣습니다.
        // 상태를 못 찾으면(또는 설정에서 끄면) 예전 획득 모션(상체만)을 씁니다.
        fullBodyPosing = s.useFullBodyPose
            && HasLayerState(s.baseLayerName, s.fullBodyPoseStateName)
            && HasLayerState(s.upperBodyLayerName, s.fullBodyPoseStateName);
        if (fullBodyPosing)
        {
            CrossFadeLayer(s.baseLayerName, s.fullBodyPoseStateName, s.pickupBlendIn, true);
            CrossFadeLayer(s.upperBodyLayerName, s.fullBodyPoseStateName, s.pickupBlendIn, true);
        }
        else
        {
            SetBool(s.pickupBool, true);
            if (!CrossFadeUpper(s, s.pickupStateName, s.pickupBlendIn)) SetTrigger(s.pickupTrigger);
        }
        SetBool(s.reloadBool, false);
        ReleaseFace(SDFaceExpression.Reload);
        HoldFace(SDFaceExpression.ItemPickup);

        // 획득 포즈는 첫 장면 카메라가 "최종적으로 서는" 쪽(Shot End Side Angle)을 봅니다. 그 순간 카메라가 아직 돌고 있어도
        // 그 위치를 쫓지 않고 최종 방향으로 정해진 시간 동안 부드럽게 돌기 때문에, 훽 돈 뒤 카메라가 더 움직이는 어긋남이 없습니다.
        Vector3 finalCamDir = Quaternion.AngleAxis(s.shotEndSideAngle, Vector3.up) * walkDir;
        turnFrom = player.transform.rotation;
        turnTo = Quaternion.LookRotation(finalCamDir);
    }

    private void UpdatePickup(IntroCutsceneSettings s)
    {
        moveDir = Vector3.zero;
        timer += Time.deltaTime;

        // 획득 유지 시간이 지나면 천천히 대기 모션으로 돌아갑니다.
        if (!returnStarted && timer >= s.pickupHold)
        {
            returnStarted = true;
            returnTimer = 0f;
            SetBool(s.pickupBool, false);
            ResetTrigger(s.pickupTrigger);
            CrossFadeToIdle(s, s.returnBlendDuration);
            if (fullBodyPosing) CrossFadeLayer(s.baseLayerName, s.baseIdleStateName, s.returnBlendDuration, false);
            fullBodyPosing = false;
            turnFrom = player.transform.rotation;
            turnTo = Quaternion.LookRotation(GameCameraDirection());
        }
        if (returnStarted)
        {
            returnTimer += Time.deltaTime;
            if (returnTimer >= s.returnBlendDuration) ReleaseFace(SDFaceExpression.ItemPickup);
        }

        // 몸 방향: 정해진 시간 동안 시작 방향 -> 목표 방향으로 천천히 출발해 천천히 멈추는 곡선으로 돌립니다(속도 고정 추적이 아니라 튀지 않음).
        // 획득 포즈 동안은 첫 장면 카메라의 최종 위치 쪽, 대기로 돌아가는 동안은 게임 카메라 쪽(컷씬 없이 시작할 때와 같은 방향).
        float turnT = returnStarted
            ? returnTimer / Mathf.Max(0.05f, s.returnTurnDuration)
            : timer / Mathf.Max(0.05f, s.faceTurnDuration);
        player.transform.rotation = Quaternion.Slerp(turnFrom, turnTo, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(turnT)));

        // 카메라가 게임 시점에 도착하고, 대기 복귀도 끝나면 컷씬을 마칩니다.
        bool cameraDone = cam == null || timer >= s.cameraRiseDelay + s.spiralDuration;
        bool poseDone = returnStarted && returnTimer >= Mathf.Max(s.returnBlendDuration, s.returnTurnDuration);
        if (cameraDone && poseDone) CompleteCutscene(s);
    }

    // ------------------------------------------------------------------
    // 카메라
    // ------------------------------------------------------------------

    private void UpdateCamera(IntroCutsceneSettings s)
    {
        Vector3 playerPos = player.transform.position;
        Vector3 lookPoint = playerPos + Vector3.up * s.lookHeight;

        if (phase != Phase.Pickup || timer < s.cameraRiseDelay)
        {
            // 첫 장면: 플레이어가 걸어오는 방향의 앞쪽에서 사선(Shot Side Angle)으로, 낮은 높이에서 플레이어를 따라갑니다.
            // 걸어올수록(정중앙에 가까울수록) 시작 거리 -> 도착 거리로 점점 가까워집니다.
            float remain = Flat(center - playerPos).magnitude;
            float progress = walkLength > 0.0001f ? 1f - Mathf.Clamp01(remain / walkLength) : 1f;
            float dist = Mathf.Lerp(s.shotStartDistance, s.shotDistance, Mathf.SmoothStep(0f, 1f, progress));

            // 걷기 끝부분부터(도착 후 장전 중에도 이어서) 사선 각도 -> 정면 각도로 부드럽게 돕니다.
            // 각도만 바꾸고 거리는 그대로라, 카메라가 플레이어 주위를 둥근 곡선으로 돌아 들어옵니다.
            if (frontTurnTimer < 0f)
            {
                if (phase != Phase.WaitLoading && (phase != Phase.Walking || progress >= s.frontTurnStartProgress)) frontTurnTimer = 0f;
            }
            else frontTurnTimer += Time.deltaTime;
            float turn = frontTurnTimer < 0f ? 0f : Mathf.SmoothStep(0f, 1f, frontTurnTimer / Mathf.Max(0.05f, s.frontTurnDuration));
            float angle = Mathf.Lerp(s.shotSideAngle, s.shotEndSideAngle, turn);
            Vector3 side = Quaternion.AngleAxis(angle, Vector3.up) * walkDir;
            Vector3 targetPos = new Vector3(playerPos.x, groundY + s.shotHeight, playerPos.z) + side * dist;

            Vector3 pos = camSnapped
                ? Vector3.Lerp(cam.transform.position, targetPos, 1f - Mathf.Exp(-s.shotFollowSpeed * Time.deltaTime))
                : targetPos;
            cam.transform.position = pos;

            // 시선은 항상 플레이어에 정확히 맞춥니다(위치만 부드럽게 따라감).
            Vector3 look = lookPoint - pos;
            if (look.sqrMagnitude > 0.0001f) cam.transform.rotation = Quaternion.LookRotation(look, Vector3.up);
            camSnapped = true;
            return;
        }

        // 나선형 상승: 시작 위치 -> 게임 카메라 위치로, 플레이어 주위를 돌면서 반지름/높이를 함께 바꿉니다.
        if (!spiralStarted)
        {
            spiralStarted = true;
            spiralStartPos = cam.transform.position;
        }
        float u = Mathf.Clamp01((timer - s.cameraRiseDelay) / Mathf.Max(0.1f, s.spiralDuration));
        float e = Mathf.SmoothStep(0f, 1f, u);

        GetGameCameraPose(out Vector3 endPos, out Quaternion endRot, out Vector3 pivot);
        Vector3 rel0 = spiralStartPos - pivot;
        Vector3 rel1 = endPos - pivot;
        float a0 = Mathf.Atan2(rel0.z, rel0.x);
        float a1 = Mathf.Atan2(rel1.z, rel1.x);
        float r0 = new Vector2(rel0.x, rel0.z).magnitude;
        float r1 = new Vector2(rel1.x, rel1.z).magnitude;

        // 위에서 내려다볼 때 각도가 커지는 쪽 = 반시계. 정한 방향으로만 돌고, 추가 바퀴 수만큼 더 돕니다.
        float sign = s.spiralClockwise ? -1f : 1f;
        float delta = Mathf.DeltaAngle(a0 * Mathf.Rad2Deg, a1 * Mathf.Rad2Deg) * Mathf.Deg2Rad;
        if (Mathf.Abs(delta) > 0.001f && Mathf.Sign(delta) != sign) delta += sign * Mathf.PI * 2f;
        delta += sign * Mathf.PI * 2f * s.spiralTurns;

        float a = a0 + delta * e;
        float r = Mathf.Lerp(r0, r1, e);
        float h = Mathf.Lerp(rel0.y, rel1.y, e);
        Vector3 camPos = u >= 1f ? endPos : pivot + new Vector3(Mathf.Cos(a) * r, h, Mathf.Sin(a) * r);
        cam.transform.position = camPos;

        // 시선: 도는 내내 플레이어(정중앙)를 바라봅니다. 게임 카메라가 실제로 바라보는 지점(플레이어 근처)으로
        // 바라보는 점만 조금씩 옮겨서, 끝에서 게임 카메라 각도와 정확히 같아지게 합니다(두 각도를 섞지 않으므로 회전이 튀지 않음).
        Vector3 endLookPoint = endPos + (endRot * Vector3.forward) * Vector3.Distance(endPos, lookPoint);
        Vector3 focus = Vector3.Lerp(lookPoint, endLookPoint, e);
        Vector3 dir = focus - camPos;
        if (u >= 1f) cam.transform.rotation = endRot;
        else if (dir.sqrMagnitude > 0.0001f) cam.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
    }

    // CameraFollow가 다시 켜졌을 때 있을 자리(대상 위치 + Offset, Fixed Euler Angles)를 계산합니다.
    private void GetGameCameraPose(out Vector3 pos, out Quaternion rot, out Vector3 pivot)
    {
        Transform t = follow != null && follow.target != null ? follow.target : (player != null ? player.transform : null);
        pivot = t != null ? t.position : center;
        Vector3 offset = follow != null ? follow.offset : new Vector3(2.66f, 4f, -2.66f);
        pos = pivot + offset;
        if (follow != null && follow.lockRotation) rot = Quaternion.Euler(follow.fixedEulerAngles);
        else
        {
            Vector3 look = pivot - pos;
            rot = look.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(look) : Quaternion.identity;
        }
    }

    // 카메라를 게임 시점에 정확히 놓고 CameraFollow에 다시 넘깁니다.
    private void HandCameraBack()
    {
        if (cam != null)
        {
            GetGameCameraPose(out Vector3 pos, out Quaternion rot, out _);
            cam.transform.SetPositionAndRotation(pos, rot);
        }
        if (follow != null && followWasEnabled) follow.enabled = true;
        cam = null;
        follow = null;
        followWasEnabled = false;
    }

    // 플레이어 -> 게임 카메라 수평 방향(컷씬 없이 시작할 때 바라보는 방향).
    private static Vector3 GameCameraDirection()
    {
        Camera c = Camera.main;
        var f = c != null ? c.GetComponent<CameraFollow>() : null;
        Vector3 dir = Vector3.zero;
        if (f != null) dir = Flat(f.offset);
        else if (c != null) dir = Flat(-c.transform.forward);
        if (dir.sqrMagnitude < 0.0001f) dir = new Vector3(1f, 0f, -1f);
        return dir.normalized;
    }

    // ------------------------------------------------------------------
    // 끝내기
    // ------------------------------------------------------------------

    // 정상 종료: 카메라를 넘기고, 조작을 풀고, HUD를 천천히 보이게 합니다.
    private void CompleteCutscene(IntroCutsceneSettings s)
    {
        moveDir = Vector3.zero;
        ResetCutsceneAnimation(false);
        HandCameraBack();
        RestoreControls();
        phase = Phase.Idle;
        StartCoroutine(FadeHud(s.hudFadeIn));
    }

    // 중단(씬이 바뀜/플레이어 사라짐): 원래 상태로만 돌립니다.
    private void AbortCutscene()
    {
        moveDir = Vector3.zero;
        ResetCutsceneAnimation(false);
        if (follow != null && followWasEnabled) follow.enabled = true;
        cam = null;
        follow = null;
        followWasEnabled = false;
        SetHudVisible(1f);
        hudGroup = null;
        RestoreControls();
        phase = Phase.Idle;
    }

    // ------------------------------------------------------------------
    // 건너뛰기: 잠깐 검게 -> 정중앙/대기/게임 카메라/HUD로 바로 맞추고 컷씬 종료 -> 다시 밝아짐
    // ------------------------------------------------------------------

    private bool CheckSkip(IntroCutsceneSettings s)
    {
        if (s.skipKeys == null) return false;
        foreach (var key in s.skipKeys)
        {
            if (key != KeyCode.None && Input.GetKeyDown(key))
            {
                StartCoroutine(SkipRoutine(s));
                return true;
            }
        }
        return false;
    }

    private IEnumerator SkipRoutine(IntroCutsceneSettings s)
    {
        Phase before = phase;
        phase = Phase.Skipping;
        moveDir = Vector3.zero;
        if (introFadeRoutine != null)
        {
            StopCoroutine(introFadeRoutine);
            introFadeRoutine = null;
        }

        yield return Fade(overlayAlpha, 1f, s.skipFadeIn * (1f - overlayAlpha));

        phase = before; // 장전/획득 중이었는지 보고 대기 모션으로 바로 되돌리기 위해 잠깐 되돌림
        ResetCutsceneAnimation(true);
        phase = Phase.Skipping;
        if (player != null)
        {
            Teleport(new Vector3(center.x, player.transform.position.y, center.z));
            if (s.faceCameraOnStart) player.transform.rotation = Quaternion.LookRotation(GameCameraDirection());
        }
        HandCameraBack();
        SetHudVisible(1f);
        RestoreControls();

        yield return new WaitForSecondsRealtime(s.skipHold);
        yield return Fade(1f, 0f, s.skipFadeOut);

        phase = Phase.Idle; // 검은 화면이 다 걷힐 때까지 ESC 중단 화면 등을 막아둠
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        if (duration <= 0f)
        {
            ApplyOverlay(to);
            yield break;
        }
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            ApplyOverlay(Mathf.Lerp(from, to, Mathf.Clamp01(t / duration)));
            yield return null;
        }
        ApplyOverlay(to);
    }

    // ------------------------------------------------------------------
    // HUD 숨김/등장
    // ------------------------------------------------------------------

    private void SetHudVisible(float alpha)
    {
        if (hudGroup == null)
        {
            var hud = Object.FindFirstObjectByType<PlayerHUDManager>();
            if (hud == null || hud.CanvasRoot == null) return;
            Canvas hudCanvas = hud.CanvasRoot.GetComponentInParent<Canvas>();
            GameObject root = hudCanvas != null ? hudCanvas.rootCanvas.gameObject : hud.CanvasRoot.gameObject;
            hudGroup = root.GetComponent<CanvasGroup>();
            if (hudGroup == null) hudGroup = root.AddComponent<CanvasGroup>();
        }
        hudGroup.alpha = Mathf.Clamp01(alpha);
    }

    private IEnumerator FadeHud(float duration)
    {
        if (hudGroup == null) yield break;
        float t = 0f;
        float from = hudGroup.alpha;
        while (duration > 0f && t < duration && hudGroup != null)
        {
            t += Time.unscaledDeltaTime;
            hudGroup.alpha = Mathf.Lerp(from, 1f, Mathf.Clamp01(t / duration));
            yield return null;
        }
        if (hudGroup != null) hudGroup.alpha = 1f;
        hudGroup = null;
    }

    // ------------------------------------------------------------------
    // 조작 잠금
    // ------------------------------------------------------------------

    // 컷씬 동안 막을 조작 컴포넌트를 끕니다. 이미 꺼져 있는 것(로딩 화면이 끈 것 등)은 기록하지 않고, 켜져 있으면 끄고 기록합니다.
    private void EnforceLock()
    {
        if (player == null) return;
        GameObject go = player.gameObject;

        var aim = go.GetComponent<PlayerAimAndAttack>();
        if (aim != null && aim.enabled) ResetAimAnimator();
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
    }

    private void TryDisable(Behaviour behaviour)
    {
        if (behaviour == null || !behaviour.enabled) return;
        behaviour.enabled = false;
        if (!disabledBehaviours.Contains(behaviour)) disabledBehaviours.Add(behaviour);
    }

    private void RestoreControls()
    {
        bool alive = player != null && !player.IsDead;
        if (alive)
        {
            foreach (var b in disabledBehaviours)
            {
                if (b != null) b.enabled = true;
            }
        }
        disabledBehaviours.Clear();
    }

    // 조준 컴포넌트를 끄면 Animator의 조준 값이 마지막 상태로 남으므로 먼저 풀어둡니다.
    private void ResetAimAnimator()
    {
        if (animator == null || animator.runtimeAnimatorController == null) return;
        foreach (var p in animator.parameters)
        {
            if (p.name == "IsAiming" && p.type == AnimatorControllerParameterType.Bool) animator.SetBool("IsAiming", false);
            else if (p.name == "AimBlend" && p.type == AnimatorControllerParameterType.Float) animator.SetFloat("AimBlend", 0f);
        }
    }

    // ------------------------------------------------------------------
    // Animator / 표정
    // ------------------------------------------------------------------

    // 컷씬 중 걸어둔 Animator 값/표정을 모두 원래대로 돌립니다(건너뛰기/중단 대비 - 정상 종료 때는 이미 풀려 있음).
    private void ResetCutsceneAnimation(bool snapToIdle)
    {
        var s = IntroCutsceneSettings.Get();
        bool wasPosing = phase == Phase.Reloading || phase == Phase.Pickup;
        SetBool(s.reloadBool, false);
        SetBool(s.pickupBool, false);
        ResetTrigger(s.pickupTrigger);
        if (snapToIdle && wasPosing) CrossFadeToIdle(s, 0f);
        if (fullBodyPosing) CrossFadeLayer(s.baseLayerName, s.baseIdleStateName, snapToIdle ? 0f : s.returnBlendDuration, false);
        fullBodyPosing = false;

        if (face != null)
        {
            foreach (var e in heldFaces) face.Release(e);
        }
        heldFaces.Clear();
    }

    // 도착 뒤 걷기 다리 자세를 서서히 대기로: PlayerMovement가 이번 프레임에 넣은 이동 값(0)을, 이 스크립트가 더 늦게
    // 서서히 줄어드는 값으로 덮어씁니다(이동 자체는 없음 - 애니메이션 값만).
    private void UpdateStopBlend(IntroCutsceneSettings s)
    {
        if (phase != Phase.Arrived && phase != Phase.Reloading && phase != Phase.Pickup) return;
        if (animMove <= 0f) return;
        animMove = s.stopBlendDuration > 0f ? Mathf.MoveTowards(animMove, 0f, Time.deltaTime / s.stopBlendDuration) : 0f;
        float m = Mathf.SmoothStep(0f, 1f, animMove) * Mathf.Max(s.arriveMinSpeed, 0.05f); // 도착 직전 걸음 크기에서 시작
        Vector3 local = player.transform.InverseTransformDirection(walkDir) * m;
        SetFloat("MoveX", local.x);
        SetFloat("MoveZ", local.z);
        SetFloat("Speed", m);
    }

    // UpperBody 레이어의 상태로 정해진 시간만큼 섞여 넘어갑니다. 상태를 못 찾으면 false.
    private bool CrossFadeUpper(IntroCutsceneSettings s, string stateName, float duration)
    {
        if (animator == null || animator.runtimeAnimatorController == null) return false;
        int layer = animator.GetLayerIndex(s.upperBodyLayerName);
        if (layer < 0) return false;
        int hash = Animator.StringToHash(s.upperBodyLayerName + "." + stateName);
        if (!animator.HasState(layer, hash))
        {
            Debug.LogWarning($"IntroCutscene: Animator의 '{s.upperBodyLayerName}' 레이어에서 '{stateName}' 상태를 찾지 못했습니다.");
            return false;
        }
        if (duration <= 0f) animator.Play(hash, layer, 0f);
        else animator.CrossFadeInFixedTime(hash, duration, layer, 0f);
        return true;
    }

    private bool HasLayerState(string layerName, string stateName)
    {
        if (animator == null || animator.runtimeAnimatorController == null) return false;
        int layer = animator.GetLayerIndex(layerName);
        if (layer < 0) return false;
        bool ok = animator.HasState(layer, Animator.StringToHash(layerName + "." + stateName));
        if (!ok) Debug.LogWarning($"IntroCutscene: Animator의 '{layerName}' 레이어에서 '{stateName}' 상태를 찾지 못해 예전 획득 모션을 씁니다.");
        return ok;
    }

    // 지정한 레이어의 상태로 정해진 시간만큼 섞여 넘어갑니다. fromStart = 처음부터 재생.
    private void CrossFadeLayer(string layerName, string stateName, float duration, bool fromStart)
    {
        if (animator == null || animator.runtimeAnimatorController == null) return;
        int layer = animator.GetLayerIndex(layerName);
        if (layer < 0) return;
        int hash = Animator.StringToHash(layerName + "." + stateName);
        if (!animator.HasState(layer, hash)) return;
        if (duration <= 0f) animator.Play(hash, layer, fromStart ? 0f : float.NegativeInfinity);
        else if (fromStart) animator.CrossFadeInFixedTime(hash, duration, layer, 0f);
        else animator.CrossFadeInFixedTime(hash, duration, layer);
    }

    private void CrossFadeToIdle(IntroCutsceneSettings s, float duration)
    {
        if (animator == null || animator.runtimeAnimatorController == null) return;
        int layer = animator.GetLayerIndex(s.upperBodyLayerName);
        if (layer < 0) return;
        int hash = Animator.StringToHash(s.upperBodyLayerName + "." + s.idleStateName);
        if (!animator.HasState(layer, hash))
        {
            Debug.LogWarning($"IntroCutscene: Animator의 '{s.upperBodyLayerName}' 레이어에서 '{s.idleStateName}' 상태를 찾지 못했습니다.");
            return;
        }
        if (duration <= 0f) animator.Play(hash, layer, 0f);
        else animator.CrossFadeInFixedTime(hash, duration, layer);
    }

    private void HoldFace(SDFaceExpression e)
    {
        if (face == null || heldFaces.Contains(e)) return;
        face.Hold(e);
        heldFaces.Add(e);
    }

    private void ReleaseFace(SDFaceExpression e)
    {
        if (face == null || !heldFaces.Remove(e)) return;
        face.Release(e);
    }

    private bool HasParam(string name, AnimatorControllerParameterType type)
    {
        if (animator == null || animator.runtimeAnimatorController == null || string.IsNullOrEmpty(name)) return false;
        foreach (var p in animator.parameters)
        {
            if (p.name == name && p.type == type) return true;
        }
        return false;
    }

    private void SetBool(string name, bool v) { if (HasParam(name, AnimatorControllerParameterType.Bool)) animator.SetBool(name, v); }
    private void SetFloat(string name, float v) { if (HasParam(name, AnimatorControllerParameterType.Float)) animator.SetFloat(name, v); }
    private void SetTrigger(string name) { if (HasParam(name, AnimatorControllerParameterType.Trigger)) animator.SetTrigger(name); }
    private void ResetTrigger(string name) { if (HasParam(name, AnimatorControllerParameterType.Trigger)) animator.ResetTrigger(name); }

    // ------------------------------------------------------------------
    // 컷씬 없이 시작할 때의 방향
    // ------------------------------------------------------------------

    private void TryFaceCameraOnStart()
    {
        var found = Object.FindFirstObjectByType<PlayerHealth>();
        if (found == null) return;
        faceCameraPending = false;
        found.transform.rotation = Quaternion.LookRotation(GameCameraDirection());
    }

    // ------------------------------------------------------------------
    // 도우미
    // ------------------------------------------------------------------

    // CharacterController는 켜진 채로 위치를 바꾸면 무시될 수 있어 잠깐 끄고 옮깁니다.
    private void Teleport(Vector3 position)
    {
        if (player == null) return;
        bool wasEnabled = controller != null && controller.enabled;
        if (wasEnabled) controller.enabled = false;
        player.transform.position = position;
        if (wasEnabled) controller.enabled = true;
    }

    private static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v;
    }

    private void BuildUI()
    {
        var canvasGO = new GameObject("IntroCutsceneCanvas");
        canvasGO.transform.SetParent(transform, false);
        canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        var overlayGO = new GameObject("Overlay");
        overlayGO.transform.SetParent(canvasGO.transform, false);
        var rect = overlayGO.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        overlayImage = overlayGO.AddComponent<Image>();
        overlayImage.raycastTarget = false;

        canvasGO.SetActive(false);
    }

    private void ApplyOverlay(float alpha)
    {
        overlayAlpha = Mathf.Clamp01(alpha);
        if (canvas == null) return;
        bool visible = overlayAlpha > 0.0001f;
        if (canvas.gameObject.activeSelf != visible) canvas.gameObject.SetActive(visible);
        if (!visible) return;

        var s = IntroCutsceneSettings.Get();
        canvas.sortingOrder = s.canvasSortingOrder;
        Color c = s.overlayColor;
        c.a *= overlayAlpha;
        overlayImage.color = c;
    }
}
