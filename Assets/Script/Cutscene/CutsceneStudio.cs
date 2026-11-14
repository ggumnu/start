using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// [촬영 스튜디오 - 포트폴리오 영상용 컷씬 촬영 씬의 관리자]
// 촬영 전용 씬(메뉴 MobArchive/촬영 스튜디오/씬 만들기)에 하나만 둡니다. 이 오브젝트 아래에 장면(StudioShot)을 여러 개 두고,
// Play를 누르면 Current Shot으로 고른 장면의 대본을 읽어 촬영 카메라를 움직입니다. 게임 씬(Title / LevelBuilder)에는 넣지 않습니다.
//
// [Play 중 키] Enter = 지금 장면 다시 재생 / PageDown, PageUp = 다음, 이전 장면 재생
//   (R, Space, ESC는 게임 조작 키라 쓰지 않습니다.)
//
// [녹화] Record를 켜면 장면이 시작될 때 녹화를 시작하고 끝나면 멈춥니다. 실제 녹화는 에디터 쪽 CutsceneStudioRecorder가
// 아래 알림을 받아 Unity Recorder로 처리합니다(Recorder는 에디터 전용이라 이 스크립트가 직접 부를 수 없음).
// 알림은 에디터 스크립트가 에디터가 켜질 때 한 번 구독하므로, 이 스크립트에서 Play 시작 때 비우면 안 됩니다.
//
// [구도 잡기] 대본이 비어 있는 장면을 Play하면 아무것도 재생하지 않고 플레이어만 첫 위치 표시(P1)에 세워 둡니다.
// 캐릭터의 머리카락/옷/총은 Play 중에만 붙으므로, 이 상태에서 캐릭터를 보며 구도(A, B)와 위치(P1, P2)를 한 번에 찍습니다.
//
// [카메라] 게임 카메라 프리팹을 그대로 쓰되 CameraFollow는 꺼 둡니다(켜져 있으면 대본과 서로 카메라를 끌어당김).
//
// [플레이어] 대본에 플레이어 줄이 있는 장면을 처음 재생할 때 Player Prefab(진짜 플레이어)과 HUD Prefab을 한 번 만들고,
// 이후 장면을 바꾸거나 다시 재생할 때는 새로 만들지 않고 같은 플레이어를 처음 상태로 되돌려 씁니다.
// HUD는 플레이어 쪽 스크립트들이 게임과 같은 환경에서 돌도록 만들어 두되, 입장 컷씬과 같은 방법(CanvasGroup)으로 투명하게 숨깁니다.
// 이동은 PlayerMovement가 아래 OverridesMovement / ScriptedMoveDirection / ForceRun을 읽어 처리합니다.
// 걷다 멈춘 뒤 다리 자세 값을 PlayerMovement보다 늦게 덮어써야 해서 실행 순서를 늦춥니다.
//
// [시간(슬로모션)] 대본의 시간 줄은 Time.timeScale로 세상 전체(플레이어/적/총알/이펙트)의 빠르기를 바꿉니다.
// 카메라와 대본 시간은 이 영향을 받지 않도록 "프레임 시간"으로 따로 셉니다. 녹화 중에는 Recorder가 프레임 간격을 1/60초로
// 고정하는데 Time.unscaledDeltaTime에는 그 고정이 적용되지 않으므로, 녹화 중에는 Time.captureDeltaTime을 씁니다.
// 적 총알은 물리 단계(FixedUpdate)마다 움직여서, 느리게 할 때 물리 단계 간격도 같은 비율로 줄여야 끊겨 보이지 않습니다.
// 장면이 끝나거나 다시 재생하거나 Play를 끝내면 원래 값(1배, 원래 물리 간격)으로 되돌립니다.
//
// [적 배우] 장면 안에 놓아둔 적 프리팹은 Play가 시작되면 숨기고, 장면을 재생할 때마다 복사해서 대본대로 움직입니다(StudioEnemyActors).
// 적의 공격 부품이 그 프레임 일을 마친 뒤에 지시해야 해서, 이 스크립트의 실행 순서가 늦은 것을 함께 이용합니다.
[DefaultExecutionOrder(1000)]
public class CutsceneStudio : MonoBehaviour
{
    private const string DefaultPlayerPrefabPath = "Assets/Player/Character/Player/SD_Player.prefab";
    private const string DefaultHudPrefabPath = "Assets/Config/Level/PlayerHUD.prefab";

    private static CutsceneStudio instance;

    // 촬영 씬이 열려 있는지입니다. 게임 쪽 스크립트가 "촬영 중에는 조용히 있기" 판단에 쓸 수 있습니다.
    public static bool IsActive { get { return instance != null; } }

    // 지금 장면이 재생(시간이 흐르는) 중인지 - 에디터의 구도 미리보기가 카메라를 건드려도 되는지 판단에 씁니다.
    public bool IsShotRunning { get { return running; } }

    // [PlayerMovement용] 촬영 씬에 플레이어 배우가 있는 동안에는 키 입력 대신 대본이 정한 방향으로만 걷습니다.
    public static bool OverridesMovement { get { return instance != null && instance.actor != null && instance.actor.Root != null; } }
    public static Vector3 ScriptedMoveDirection { get { return OverridesMovement ? instance.actor.MoveDirection : Vector3.zero; } }
    // 대본의 '뛰기' 동안 비전투 가속을 바로 최대로 둡니다.
    public static bool ForceRun { get { return OverridesMovement && instance.actor.ForceRun; } }

    // 대본이 이동 시간을 정한 경우의 이동 속도(m/s). 0이면 게임 속도 그대로.
    public static float ScriptedSpeed { get { return OverridesMovement ? instance.actor.SpeedOverride : 0f; } }

    // 녹화 시작(장면 이름) / 멈춤 알림 - 에디터의 CutsceneStudioRecorder가 받습니다.
    public static event Action<string> RecordingStartRequested;
    public static event Action RecordingStopRequested;

    [Header("촬영")]
    [Tooltip("촬영에 쓰는 카메라입니다. [씬 만들기]가 자동으로 연결합니다.")]
    public Camera studioCamera;
    [Tooltip("Play를 누르면 재생할 장면입니다. 아래 '재생할 장면' 목록에서 고르면 자동으로 들어갑니다.")]
    public StudioShot currentShot;
    [Tooltip("켜면 장면을 재생할 때마다 Recordings 폴더에 '장면이름_번호.mov'로 녹화합니다(ProRes 422, 1920x1080, 60fps, 소리 없음).")]
    public bool record = false;
    [Tooltip("Play를 누른 뒤(또는 장면을 바꾼 뒤) 재생을 시작하기까지 기다리는 시간(초)입니다. 첫 프레임이 버벅이는 것을 피합니다.")]
    [Min(0f)] public float startDelay = 0.5f;
    [Tooltip("촬영 카메라가 이 거리(m)보다 가까운 것은 잘려서 안 보입니다. 게임 카메라는 0.3이라 캐릭터에 바짝 붙는 구도에서 가방/머리가 잘리므로 촬영 씬에서는 작게 둡니다.")]
    [Min(0.01f)] public float nearClip = 0.05f;

    [Header("플레이어 (대본에 플레이어 줄이 있는 장면에서만 만들어짐)")]
    [Tooltip("진짜 플레이어 프리팹입니다(LevelBuilder의 Player Prefab과 같은 것). 비워두면 SD_Player를 자동으로 넣습니다.")]
    public GameObject playerPrefab;
    [Tooltip("PlayerHUD 프리팹입니다. 화면에는 보이지 않게 숨기지만, 플레이어 스크립트들이 게임과 같은 환경에서 돌도록 함께 만듭니다.")]
    public GameObject hudPrefab;

    private StudioPlayerActor actor;
    private GameObject playerObject;
    private CanvasGroup hudGroup;
    private StudioEnemyActors enemies;
    private float baseFixedDeltaTime = -1f;
    private bool worldSpeedChanged;

    // 녹화 프레임 간격(1/60초)에 맞춘 물리 단계 간격 - 한 프레임에 물리 한 번이 되어 총알이 고르게 움직입니다.
    private const float StudioPhysicsStep = 1f / 60f;

    private ShotPlayback playback;
    private bool running;
    private bool recording;
    private Coroutine playRoutine;
    private Image fadeImage;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
    }

    void Awake()
    {
        instance = this;
        // 놓아둔 적이 스스로 움직이기 전에(첫 Update 전에) 숨깁니다.
        StudioEnemyActors.HideTemplates(GetShots());
    }

    void OnDestroy()
    {
        RestoreWorldSpeed();
        StopRecording();
        if (instance == this) instance = null;
    }

    void Start()
    {
        if (studioCamera == null) studioCamera = Camera.main;
        if (studioCamera == null)
        {
            Debug.LogError("[촬영] 촬영 카메라가 없습니다. CutsceneStudio의 Studio Camera 칸에 카메라를 넣어주세요.", this);
            return;
        }

        var follow = studioCamera.GetComponent<CameraFollow>();
        if (follow != null) follow.enabled = false;
        studioCamera.nearClipPlane = Mathf.Max(0.01f, nearClip);

        BuildFadeOverlay();
        baseFixedDeltaTime = Time.fixedDeltaTime;
        enemies = new StudioEnemyActors(studioCamera);

        if (currentShot == null)
        {
            var shots = GetShots();
            if (shots.Count > 0) currentShot = shots[0];
        }
        if (currentShot == null)
        {
            Debug.LogWarning("[촬영] 재생할 장면이 없습니다. CutsceneStudio 인스펙터의 [새 장면 만들기]로 장면을 먼저 만들어주세요.", this);
            return;
        }
        PlayShot(currentShot);
    }

    void Update()
    {
        if (studioCamera == null) return;

        // 재생 중이 아닐 때도 불러서 조작 잠금과 제자리 서 있기를 유지합니다.
        if (actor != null && playerObject != null && playerObject.activeSelf)
        {
            actor.Tick(Time.deltaTime, running);
            HideHud();
        }
        if (enemies != null) enemies.Tick(Time.deltaTime, running);

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            if (currentShot != null) PlayShot(currentShot);
        }
        else if (Input.GetKeyDown(KeyCode.PageDown))
        {
            StepShot(1);
        }
        else if (Input.GetKeyDown(KeyCode.PageUp))
        {
            StepShot(-1);
        }
    }

    // 다른 스크립트가 카메라를 건드린 뒤에 덮어쓰도록 LateUpdate에서 움직입니다.
    void LateUpdate()
    {
        if (!running || playback == null) return;

        playback.Tick(FrameDeltaTime());
        SetFade(playback.FadeAlpha);
        ApplyWorldSpeed();

        if (playback.Finished)
        {
            running = false;
            RestoreWorldSpeed();
            StopRecording();
            Debug.Log("[촬영] 장면 '" + currentShot.name + "' 끝. Enter = 다시 재생, PageDown/PageUp = 다음/이전 장면.");
        }
    }

    // 이 오브젝트 아래의 장면들(Hierarchy 순서, 꺼진 장면 포함)
    public List<StudioShot> GetShots()
    {
        return new List<StudioShot>(GetComponentsInChildren<StudioShot>(true));
    }

    public void PlayShot(StudioShot shot)
    {
        if (shot == null || studioCamera == null) return;
        if (playRoutine != null) StopCoroutine(playRoutine);
        running = false;
        RestoreWorldSpeed();
        StopRecording();
        playRoutine = StartCoroutine(PlayRoutine(shot));
    }

    private void StepShot(int step)
    {
        var shots = GetShots();
        if (shots.Count == 0) return;
        int index = Mathf.Max(0, shots.IndexOf(currentShot));
        PlayShot(shots[(index + step + shots.Count) % shots.Count]);
    }

    private IEnumerator PlayRoutine(StudioShot shot)
    {
        currentShot = shot;
        foreach (var other in GetShots()) other.gameObject.SetActive(other == shot);

        LevelLighting.Apply(shot.lightingProfile);
        LevelBackground.Apply(shot.backgroundMaterial);

        // 대본이 비어 있는 구도 잡기 때도 적이 놓아둔 자리에 보이도록 대본을 읽기 전에 만듭니다.
        enemies.ResetForShot(shot);

        ShotScript parsed = ShotScript.Parse(shot.script != null ? shot.script.text : "");
        foreach (string error in parsed.errors)
        {
            Debug.LogError("[촬영] 장면 '" + shot.name + "' 대본 " + error + " (이 줄은 건너뜁니다)", shot.script);
        }
        if (parsed.cues.Count == 0)
        {
            // 대본이 비어 있으면 구도 잡기: 플레이어만 세워 두고 아무것도 재생하지 않습니다.
            playback = null;
            EnsurePlayer(shot);
            if (actor != null)
            {
                playerObject.SetActive(true);
                actor.ResetForShot(shot, parsed);
                ShotMark stand = FindStandMark(shot);
                if (stand != null) actor.PlaceAt(stand.transform.position, stand.transform.rotation);
                HideHud();
            }
            enemies.PlayerRoot = actor != null && playerObject != null ? actor.Root : null;
            Debug.Log("[촬영] 장면 '" + shot.name + "'은(는) 대본이 비어 있어 구도 잡기 상태입니다. 플레이어를 보면서 장면 인스펙터의 " +
                      "[구도 저장] / [위치 저장]으로 찍으세요. Play를 끝내면 찍은 것이 씬에 들어갑니다.", shot);
            playRoutine = null;
            yield break;
        }

        StudioPlayerActor shotActor = null;
        if (parsed.usesPlayer)
        {
            bool created = EnsurePlayer(shot);
            if (actor != null)
            {
                playerObject.SetActive(true);
                actor.ResetForShot(shot, parsed);
                shotActor = actor;
                HideHud();
                if (created) yield return null; // 방금 만든 플레이어의 Start가 끝난 뒤에 진행
            }
        }
        else if (playerObject != null)
        {
            playerObject.SetActive(false);
        }

        enemies.PlayerRoot = shotActor != null ? shotActor.Root : null;
        playback = new ShotPlayback(parsed, shot, studioCamera, shotActor, enemies);
        playback.Begin(); // 기다리는 동안에도 첫 구도가 화면에 보이도록 먼저 0초 상태로 맞춤
        SetFade(playback.FadeAlpha);
        ApplyWorldSpeed();

        if (startDelay > 0f) yield return new WaitForSecondsRealtime(startDelay);

        if (record)
        {
            recording = true;
            try { RecordingStartRequested?.Invoke(shot.name); }
            catch (Exception e) { Debug.LogError("[촬영] 녹화를 시작하지 못했습니다: " + e); }
            // 녹화 준비가 끝난 다음 프레임부터 장면이 흐르도록 두 프레임 쉽니다.
            yield return null;
            yield return null;
        }

        Debug.Log("[촬영] 장면 '" + shot.name + "' 재생 (길이 " + playback.Duration.ToString("0.##") + "초" + (record ? ", 녹화 중" : "") + ")");
        running = true;
        playRoutine = null;
    }

    // 구도 잡기 때 플레이어를 세울 자리: P1이 있으면 P1, 없으면 첫 위치 표시, 그것도 없으면 장면 한가운데(null).
    private static ShotMark FindStandMark(StudioShot shot)
    {
        ShotMark p1 = shot.FindMark("P1");
        if (p1 != null && p1.kind == ShotMark.MarkKind.Position) return p1;
        foreach (ShotMark mark in shot.GetComponentsInChildren<ShotMark>(true))
        {
            if (mark.kind == ShotMark.MarkKind.Position) return mark;
        }
        return null;
    }

    // 장면이 재생 중이 아닐 때(구도 잡기 / 장면이 끝난 뒤) 플레이어를 위치 표시 자리에 세웁니다. 세웠으면 true.
    public bool PlacePlayerAt(ShotMark mark)
    {
        if (mark == null || running || currentShot == null) return false;
        EnsurePlayer(currentShot);
        if (actor == null) return false;
        playerObject.SetActive(true);
        actor.PlaceAt(mark.transform.position, mark.transform.rotation);
        HideHud();
        return true;
    }

    // 플레이어(와 숨긴 HUD)를 아직 안 만들었으면 만듭니다. 새로 만들었으면 true.
    private bool EnsurePlayer(StudioShot shot)
    {
        if (actor != null && playerObject != null) return false;

#if UNITY_EDITOR
        if (playerPrefab == null) playerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(DefaultPlayerPrefabPath);
        if (hudPrefab == null) hudPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(DefaultHudPrefabPath);
#endif
        if (playerPrefab == null)
        {
            Debug.LogError("[촬영] 플레이어 프리팹이 없습니다. CutsceneStudio의 Player Prefab 칸에 SD_Player 프리팹을 넣어주세요.", this);
            return false;
        }

        // 게임 씬과 같은 순서: HUD가 먼저 있고 플레이어가 나중에 생깁니다.
        if (hudPrefab != null) Instantiate(hudPrefab);
        else Debug.LogWarning("[촬영] HUD Prefab 칸이 비어 있어 HUD 없이 플레이어를 만듭니다. Console에 오류가 나오면 PlayerHUD 프리팹을 넣어주세요.", this);

        playerObject = Instantiate(playerPrefab, shot.transform.position + Vector3.up * 0.02f, Quaternion.identity);
        playerObject.name = playerPrefab.name;
        var health = playerObject.GetComponent<PlayerHealth>();
        if (health == null)
        {
            Debug.LogError("[촬영] Player Prefab에 PlayerHealth가 없습니다. 진짜 플레이어 프리팹(SD_Player)인지 확인해주세요.", this);
            Destroy(playerObject);
            playerObject = null;
            return false;
        }
        actor = new StudioPlayerActor(health, studioCamera);
        actor.ResolveExtra = name => enemies != null ? enemies.FindRoot(name) : null;
        return true;
    }

    // 입장 컷씬과 같은 방법: HUD Canvas에 CanvasGroup을 붙여 투명하게 숨깁니다(HUD가 만들어질 때까지 매 프레임 찾음).
    private void HideHud()
    {
        if (hudGroup == null)
        {
            var hud = FindFirstObjectByType<PlayerHUDManager>();
            if (hud == null || hud.CanvasRoot == null) return;
            Canvas hudCanvas = hud.CanvasRoot.GetComponentInParent<Canvas>();
            GameObject root = hudCanvas != null ? hudCanvas.rootCanvas.gameObject : hud.CanvasRoot.gameObject;
            hudGroup = root.GetComponent<CanvasGroup>();
            if (hudGroup == null) hudGroup = root.AddComponent<CanvasGroup>();
        }
        if (hudGroup.alpha != 0f) hudGroup.alpha = 0f;
    }

    // 카메라/대본 시간이 한 프레임에 흐르는 양(세상 빠르기와 무관)
    private static float FrameDeltaTime()
    {
        float dt = Time.captureDeltaTime > 0f ? Time.captureDeltaTime : Time.unscaledDeltaTime;
        return Mathf.Min(dt, Time.maximumDeltaTime);
    }

    private void ApplyWorldSpeed()
    {
        if (playback == null || !playback.HasWorldSpeed) return;
        if (baseFixedDeltaTime <= 0f) baseFixedDeltaTime = Time.fixedDeltaTime;
        float speed = playback.WorldSpeed;
        Time.timeScale = speed;
        Time.fixedDeltaTime = Mathf.Min(baseFixedDeltaTime, StudioPhysicsStep) * speed;
        worldSpeedChanged = true;
    }

    private void RestoreWorldSpeed()
    {
        if (!worldSpeedChanged) return;
        worldSpeedChanged = false;
        Time.timeScale = 1f;
        if (baseFixedDeltaTime > 0f) Time.fixedDeltaTime = baseFixedDeltaTime;
    }

    private void StopRecording()
    {
        if (!recording) return;
        recording = false;
        try { RecordingStopRequested?.Invoke(); }
        catch (Exception e) { Debug.LogError("[촬영] 녹화를 멈추는 중 오류가 발생했습니다: " + e); }
    }

    // 화면 어둡게/밝게용 검은 막(녹화에도 그대로 찍힘)
    private void BuildFadeOverlay()
    {
        var canvasObject = new GameObject("StudioFade");
        canvasObject.transform.SetParent(transform, false);
        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900;

        var imageObject = new GameObject("Black");
        imageObject.transform.SetParent(canvasObject.transform, false);
        fadeImage = imageObject.AddComponent<Image>();
        fadeImage.raycastTarget = false;
        var rect = fadeImage.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        SetFade(0f);
    }

    private void SetFade(float alpha)
    {
        if (fadeImage == null) return;
        fadeImage.color = new Color(0f, 0f, 0f, Mathf.Clamp01(alpha));
        fadeImage.enabled = alpha > 0.001f;
    }
}
