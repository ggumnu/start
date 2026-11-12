using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// 효과음 재생 창구입니다. 어디서든 GameAudio.Play(SfxId.XXX)로 부릅니다. 게임 시작 때 스스로 만들어지고 씬이 바뀌어도 유지됩니다.
// 설정 = Resources/SoundLibrary(SoundLibrary.cs). 모든 소리는 2D입니다(고정 탑다운 카메라라 거리 감쇠를 두면 방 구석 소리가 묻힘).
// 반복 소리(81번 연소 등)는 PlayLoop/StopLoop - 방 소속 반복 소리는 다른 방에 들어가면 자동으로 꺼짐(방 범위 규칙).
// 게임이 멈춘 동안(ESC - Time.timeScale 0) 반복 소리는 일시 정지하고, 한 번 소리(UI 등)는 그대로 납니다.
// 이미 있는 게임 알림에 소리를 다는 일은 SoundEventHooks(같은 오브젝트에 자동으로 붙음)가 맡습니다.
// [BGM] SoundLibrary의 곡 하나를 씬과 상관없이 계속 반복합니다(반복 구간 지원). 게임이 멈춘 동안은 작게, 보스방 입장 때는
// PlayBossAlert로 경고음 -> BGM 끊김 -> 경고음이 끝나면 곡 처음부터 서서히 커짐.
// [인트로] bgmIntroClip이 있으면 게임을 켠 뒤 처음 한 번만 인트로를 틀고, 오디오 시계(PlayScheduled)로 끝나는 순간에 맞춰
// 반복 곡을 이어 붙입니다. 시작 순간 게임이 잠깐 멈칫해서 인트로가 늦게 시작되면, 실제 시작 시각을 재서 반복 곡 예약을 고칩니다.
// [플레이어 볼륨] UserBgmVolume/UserSfxVolume(0~1, PlayerPrefs에 저장 - 설정 창 SettingsUI)이 라이브러리 볼륨에 곱해집니다.
public class GameAudio : MonoBehaviour
{
    private const int PoolSize = 24;

    private static GameAudio instance;
    private static bool quitting;
    private static bool warnedMissingLibrary;
    private static readonly Dictionary<SfxId, int> suppressedFrame = new Dictionary<SfxId, int>();

    private const string BgmVolumePrefKey = "MobArchive.Volume.Bgm";
    private const string SfxVolumePrefKey = "MobArchive.Volume.Sfx";
    private static float userBgmVolume = -1f; // -1 = 아직 PlayerPrefs에서 안 읽음
    private static float userSfxVolume = -1f;

    private class LoopInfo
    {
        public AudioSource source;
        public bool roomScoped;
        public bool pausedByGame;
        public float baseVolume; // 플레이어 효과음 볼륨을 곱하기 전 크기
    }

    private enum BgmPhase { Normal, Cutting, Silent, FadingIn }

    private SoundLibrary library;
    private readonly List<AudioSource> pool = new List<AudioSource>();
    private readonly Dictionary<SfxId, float> lastPlayTime = new Dictionary<SfxId, float>();
    private readonly Dictionary<int, LoopInfo> loops = new Dictionary<int, LoopInfo>();
    private readonly List<int> loopKeysBuffer = new List<int>();
    private int nextLoopHandle = 1;
    private AudioListener fallbackListener;

    private AudioSource bgmSource;
    private AudioSource bgmIntroSource;
    private AudioClip bgmIntroClipUsed;   // 지금 BGM을 시작할 때 쓴 인트로(라이브러리에서 바꿨는지 알아보는 용도)
    private bool bgmIntroActive;          // 인트로 재생 중(반복 곡은 예약만 된 상태)
    private double bgmLoopStartDsp;       // 반복 곡이 시작되도록 예약한 오디오 시계 시각
    private bool bgmIntroStartChecked;    // 인트로 실제 시작 시각을 한 번 확인했는지
    private float bossAlertEndTime;       // 보스방 경고음이 끝나는 시각(unscaledTime)
    private BgmPhase bgmPhase = BgmPhase.Normal;
    private float bgmLevel = 1f;          // 0~1 끊김/서서히 커짐 배율
    private float bgmPhaseStartTime;
    private float bgmCutFromLevel = 1f;
    private float bgmLastTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        quitting = false;
        warnedMissingLibrary = false;
        suppressedFrame.Clear();
        userBgmVolume = -1f;
        userSfxVolume = -1f;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        EnsureInstance();
    }

    private static GameAudio EnsureInstance()
    {
        if (instance != null || quitting || !Application.isPlaying) return instance;
        var go = new GameObject("GameAudio");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<GameAudio>();
        return instance;
    }

    // 걸음 소리 간격(유닛). 라이브러리가 없으면 기본값.
    public static float FootstepDistance
    {
        get
        {
            var a = EnsureInstance();
            return a != null && a.library != null ? Mathf.Max(0.1f, a.library.footstepDistance) : 1.2f;
        }
    }

    // 플레이어가 설정 창에서 정한 BGM/효과음 볼륨(0~1)입니다. 바꾸면 바로 반영되고 PlayerPrefs에 저장됩니다(다음 실행에도 유지).
    public static float UserBgmVolume
    {
        get
        {
            if (userBgmVolume < 0f) userBgmVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(BgmVolumePrefKey, 1f));
            return userBgmVolume;
        }
        set
        {
            userBgmVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(BgmVolumePrefKey, userBgmVolume);
        }
    }

    public static float UserSfxVolume
    {
        get
        {
            if (userSfxVolume < 0f) userSfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(SfxVolumePrefKey, 1f));
            return userSfxVolume;
        }
        set
        {
            userSfxVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(SfxVolumePrefKey, userSfxVolume);
        }
    }

    // 볼륨을 디스크에 바로 기록합니다(설정 창을 닫을 때).
    public static void SaveUserVolumes()
    {
        PlayerPrefs.Save();
    }

    // [보스방 입장] 경고음을 내고, BGM을 끊었다가 조용한 시간 뒤 곡 처음부터 서서히 키웁니다(시간 값은 SoundLibrary).
    public static void PlayBossAlert()
    {
        try
        {
            var a = EnsureInstance();
            if (a != null) a.BeginBossAlert();
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }

    // 한 번 소리. 소리 칸이 비어 있거나 라이브러리가 없으면 아무 일도 없습니다(호출한 쪽을 절대 멈추지 않음).
    public static void Play(SfxId id, float volumeScale = 1f)
    {
        try
        {
            var a = EnsureInstance();
            if (a != null) a.PlayInternal(id, volumeScale);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }

    // 이번 프레임에 id 소리가 다시 불려도 무시합니다(예: 110번 강화 총알은 사격 알림도 함께 보내므로 일반 사격 소리를 막음).
    public static void SuppressThisFrame(SfxId id)
    {
        suppressedFrame[id] = Time.frameCount;
    }

    // 반복 소리를 켜고 핸들을 돌려줍니다(0 = 실패/소리 없음). roomScoped면 플레이어가 다른 방에 들어가는 순간 자동으로 꺼집니다.
    public static int PlayLoop(SfxId id, bool roomScoped = true, float volumeScale = 1f)
    {
        try
        {
            var a = EnsureInstance();
            return a != null ? a.PlayLoopInternal(id, roomScoped, volumeScale) : 0;
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            return 0;
        }
    }

    // 반복 소리를 끄고 핸들을 0으로 만듭니다. 이미 꺼졌거나 0이어도 안전합니다.
    public static void StopLoop(ref int handle)
    {
        if (handle != 0 && instance != null) instance.StopLoopInternal(handle);
        handle = 0;
    }

    public static bool IsLoopPlaying(int handle)
    {
        return handle != 0 && instance != null && instance.loops.ContainsKey(handle);
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;

        library = Resources.Load<SoundLibrary>(SoundLibrary.ResourcePath);
        if (library == null && !warnedMissingLibrary)
        {
            warnedMissingLibrary = true;
            Debug.LogWarning("GameAudio: Resources/SoundLibrary 에셋이 없어 효과음이 나지 않습니다. " +
                             "메뉴 MobArchive/사운드/사운드 라이브러리 만들기로 만드세요.");
        }

        for (int i = 0; i < PoolSize; i++) pool.Add(CreateSource("Sfx_" + i));
        bgmSource = CreateSource("Bgm");
        bgmSource.loop = true;
        bgmSource.priority = 0; // 효과음이 많아도 BGM이 밀려나지 않게
        bgmIntroSource = CreateSource("BgmIntro");
        bgmIntroSource.loop = false;
        bgmIntroSource.priority = 0;
        StartBgm(true);

        if (GetComponent<SoundEventHooks>() == null) gameObject.AddComponent<SoundEventHooks>();
    }

    void OnEnable()
    {
        RoomController.OnAnyRoomEntered += HandleRoomEntered;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        EnsureListener();
    }

    void OnDisable()
    {
        RoomController.OnAnyRoomEntered -= HandleRoomEntered;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    void OnApplicationQuit()
    {
        quitting = true;
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    void Update()
    {
        bool gamePaused = Time.timeScale <= 0f;
        UpdateBgm(gamePaused);

        float sfxScale = UserSfxVolume;
        foreach (var info in loops.Values)
        {
            if (info.source == null) continue;
            info.source.volume = Mathf.Clamp01(info.baseVolume * sfxScale);
            if (gamePaused && !info.pausedByGame)
            {
                info.source.Pause();
                info.pausedByGame = true;
            }
            else if (!gamePaused && info.pausedByGame)
            {
                info.source.UnPause();
                info.pausedByGame = false;
            }
        }
    }

    private AudioSource CreateSource(string name)
    {
        var child = new GameObject(name);
        child.transform.SetParent(transform, false);
        var src = child.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.spatialBlend = 0f;
        src.loop = false;
        return src;
    }

    private bool TryResolve(SfxId id, out SoundLibrary.Entry entry, out AudioClip clip)
    {
        entry = null;
        clip = null;
        if (id == SfxId.None || library == null) return false;
        entry = library.Find(id);
        if (entry == null || entry.clips == null || entry.clips.Length == 0) return false;

        int count = 0;
        foreach (var c in entry.clips) if (c != null) count++;
        if (count == 0) return false;

        int pick = UnityEngine.Random.Range(0, count);
        foreach (var c in entry.clips)
        {
            if (c == null) continue;
            if (pick == 0) { clip = c; break; }
            pick--;
        }
        return clip != null;
    }

    private float RandomPitch(SoundLibrary.Entry entry)
    {
        float min = Mathf.Max(0.1f, Mathf.Min(entry.pitchRange.x, entry.pitchRange.y));
        float max = Mathf.Max(0.1f, Mathf.Max(entry.pitchRange.x, entry.pitchRange.y));
        return UnityEngine.Random.Range(min, max);
    }

    private AudioSource PlayInternal(SfxId id, float volumeScale)
    {
        if (suppressedFrame.TryGetValue(id, out int frame) && frame == Time.frameCount) return null;
        if (!TryResolve(id, out var entry, out var clip)) return null;

        // 게임이 멈춘 동안에도 간격이 맞도록 실제 시간 기준.
        float now = Time.unscaledTime;
        if (entry.minInterval > 0f && lastPlayTime.TryGetValue(id, out float last) && now - last < entry.minInterval) return null;
        lastPlayTime[id] = now;

        var src = GetFreeSource();
        src.clip = clip;
        src.volume = Mathf.Clamp01(entry.volume * library.sfxVolume * volumeScale * UserSfxVolume);
        src.pitch = RandomPitch(entry);
        src.Play();
        return src;
    }

    // 쉬는 소스를 쓰고, 모두 재생 중이면 가장 오래 재생된 소스를 빼앗습니다.
    private AudioSource GetFreeSource()
    {
        AudioSource oldest = pool[0];
        float oldestProgress = -1f;
        foreach (var src in pool)
        {
            if (!src.isPlaying) return src;
            float progress = src.clip != null && src.clip.length > 0f ? src.time / src.clip.length : 1f;
            if (progress > oldestProgress)
            {
                oldestProgress = progress;
                oldest = src;
            }
        }
        oldest.Stop();
        return oldest;
    }

    private int PlayLoopInternal(SfxId id, bool roomScoped, float volumeScale)
    {
        if (!TryResolve(id, out var entry, out var clip)) return 0;

        var src = CreateSource("Loop_" + id);
        src.clip = clip;
        src.loop = true;
        float baseVolume = entry.volume * library.sfxVolume * volumeScale;
        src.volume = Mathf.Clamp01(baseVolume * UserSfxVolume);
        src.pitch = RandomPitch(entry);
        src.Play();

        int handle = nextLoopHandle++;
        var info = new LoopInfo { source = src, roomScoped = roomScoped, baseVolume = baseVolume };
        if (Time.timeScale <= 0f)
        {
            src.Pause();
            info.pausedByGame = true;
        }
        loops.Add(handle, info);
        return handle;
    }

    private void StopLoopInternal(int handle)
    {
        if (!loops.TryGetValue(handle, out var info)) return;
        loops.Remove(handle);
        if (info.source != null) Destroy(info.source.gameObject);
    }

    private void StopLoops(bool roomScopedOnly)
    {
        loopKeysBuffer.Clear();
        foreach (var pair in loops)
        {
            if (!roomScopedOnly || pair.Value.roomScoped) loopKeysBuffer.Add(pair.Key);
        }
        foreach (int key in loopKeysBuffer) StopLoopInternal(key);
    }

    private void HandleRoomEntered(RoomInstance room)
    {
        StopLoops(true);
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 씬이 바뀌면 이전 씬의 반복 소리는 전부 끕니다(방 소속이 아니어도).
        StopLoops(false);
        EnsureListener();
    }

    // ------------------------------------------------------------------
    // BGM
    // ------------------------------------------------------------------

    private void StartBgm(bool withIntro)
    {
        if (bgmSource == null) return;
        AudioClip clip = library != null ? library.bgmClip : null;
        AudioClip intro = library != null ? library.bgmIntroClip : null;
        bgmSource.Stop();
        if (bgmIntroSource != null) bgmIntroSource.Stop();
        bgmSource.clip = clip;
        bgmIntroClipUsed = intro;
        bgmIntroActive = false;
        bgmPhase = BgmPhase.Normal;
        bgmLevel = 1f;
        bgmLastTime = 0f;
        if (clip == null) return;

        bgmSource.time = 0f;
        if (withIntro && intro != null && bgmIntroSource != null && intro.frequency > 0)
        {
            // 조금 뒤 시각에 인트로를 예약하고, 인트로 길이(샘플 단위)만큼 뒤에 반복 곡을 예약 -> 이음매에 빈틈이 없음
            double startDsp = AudioSettings.dspTime + 0.25;
            bgmIntroSource.clip = intro;
            bgmIntroSource.time = 0f;
            bgmIntroSource.PlayScheduled(startDsp);
            bgmLoopStartDsp = startDsp + (double)intro.samples / intro.frequency;
            bgmSource.PlayScheduled(bgmLoopStartDsp);
            bgmIntroStartChecked = false;
            bgmIntroActive = true;
            ApplyBgmVolume(false);
            return;
        }
        bgmSource.Play();
    }

    // 인트로가 예약보다 늦게 시작됐으면(시작 순간 게임이 멈칫한 경우) 반복 곡 예약을 실제 인트로 끝 시각으로 옮깁니다.
    // 작은 차이는 측정 오차일 수 있어 0.03초 넘게 어긋났을 때만 고칩니다.
    private void CorrectIntroSchedule()
    {
        if (!bgmIntroActive || bgmIntroStartChecked || bgmIntroSource == null) return;
        AudioClip intro = bgmIntroSource.clip;
        if (intro == null || intro.frequency <= 0) { bgmIntroStartChecked = true; return; }

        double nowDsp = AudioSettings.dspTime;
        if (!bgmIntroSource.isPlaying || bgmIntroSource.timeSamples <= 0)
        {
            // 예약 시각이 한참 지났는데도 시작을 못 했으면 다음 프레임에 다시 봅니다.
            return;
        }
        bgmIntroStartChecked = true;

        double actualStart = nowDsp - (double)bgmIntroSource.timeSamples / intro.frequency;
        double correctLoopStart = actualStart + (double)intro.samples / intro.frequency;
        if (Math.Abs(correctLoopStart - bgmLoopStartDsp) > 0.03 && correctLoopStart - nowDsp > 0.1)
        {
            bgmLoopStartDsp = correctLoopStart;
            bgmSource.SetScheduledStartTime(correctLoopStart);
        }
    }

    // 인트로를 멈추고 예약된 반복 곡도 취소한 뒤, 반복 곡을 처음부터 바로 틉니다.
    private void RestartBgmLoopFromStart()
    {
        if (bgmIntroSource != null) bgmIntroSource.Stop();
        bgmIntroActive = false;
        bgmSource.Stop();
        bgmSource.time = 0f;
        bgmSource.Play();
        bgmLastTime = 0f;
    }

    private void BeginBossAlert()
    {
        AudioSource alert = PlayInternal(SfxId.BossRoomAlert, 1f);
        if (bgmSource == null || bgmSource.clip == null || library == null) return;

        // 경고음 길이(음 높이 변화 반영)만큼 BGM을 꺼 둡니다. 경고음 클립이 비어 있으면 바로 다시 틉니다.
        bossAlertEndTime = Time.unscaledTime;
        if (alert != null && alert.clip != null)
            bossAlertEndTime += alert.clip.length / Mathf.Max(0.01f, Mathf.Abs(alert.pitch));

        bgmPhaseStartTime = Time.unscaledTime;
        bgmCutFromLevel = bgmLevel;
        if (library.bossBgmCutDuration > 0f)
        {
            bgmPhase = BgmPhase.Cutting;
        }
        else
        {
            bgmPhase = BgmPhase.Silent;
            bgmLevel = 0f;
        }
    }

    private void UpdateBgm(bool gamePaused)
    {
        if (bgmSource == null || library == null) return;

        // Play 중에 라이브러리의 곡(또는 인트로)을 바꾸면 새 곡으로 처음부터.
        if (bgmSource.clip != library.bgmClip || bgmIntroClipUsed != library.bgmIntroClip) StartBgm(true);
        if (bgmSource.clip == null) return;

        CorrectIntroSchedule();

        // 인트로가 끝나 반복 곡이 실제로 시작된 뒤부터는 평소처럼 관리합니다.
        if (bgmIntroActive && AudioSettings.dspTime >= bgmLoopStartDsp + 0.5) bgmIntroActive = false;
        if (!bgmIntroActive && !bgmSource.isPlaying) bgmSource.Play(); // 포커스를 잃었다 돌아오는 등으로 멈췄을 때

        float now = Time.unscaledTime;
        float elapsed = now - bgmPhaseStartTime;
        switch (bgmPhase)
        {
            case BgmPhase.Cutting:
                bgmLevel = bgmCutFromLevel * (1f - Mathf.Clamp01(elapsed / Mathf.Max(0.01f, library.bossBgmCutDuration)));
                if (bgmLevel <= 0f)
                {
                    bgmLevel = 0f;
                    bgmPhase = BgmPhase.Silent;
                    bgmPhaseStartTime = now;
                }
                break;
            case BgmPhase.Silent:
                bgmLevel = 0f;
                if (now >= bossAlertEndTime + library.bossBgmSilenceAfterAlert)
                {
                    // 경고음이 끝나면 곡 처음부터(형빈 확정) 0에서 서서히 커짐.
                    if (library.bossBgmRestartWithIntro && library.bgmIntroClip != null) StartBgm(true);
                    else RestartBgmLoopFromStart();
                    bgmLevel = 0f;
                    bgmPhase = BgmPhase.FadingIn;
                    bgmPhaseStartTime = now;
                }
                break;
            case BgmPhase.FadingIn:
                bgmLevel = Mathf.Clamp01(elapsed / Mathf.Max(0.05f, library.bossBgmFadeInDuration));
                if (bgmLevel >= 1f) bgmPhase = BgmPhase.Normal;
                break;
        }

        if (!bgmIntroActive) ApplyBgmLoopRegion();

        ApplyBgmVolume(gamePaused);
    }

    private void ApplyBgmVolume(bool gamePaused)
    {
        if (library == null) return;
        float pauseScale = gamePaused ? library.bgmPausedVolumeRatio : 1f;
        float volume = Mathf.Clamp01(library.bgmVolume * UserBgmVolume * bgmLevel * pauseScale);
        if (bgmSource != null) bgmSource.volume = volume;
        if (bgmIntroSource != null) bgmIntroSource.volume = volume;
    }

    // 반복 구간: 끝(bgmLoopEnd)에 닿거나 곡 끝에서 처음으로 돌아가면 반복 구간 시작(bgmLoopStart)으로 옮깁니다.
    private void ApplyBgmLoopRegion()
    {
        float length = bgmSource.clip.length;
        float t = bgmSource.time;
        float start = Mathf.Clamp(library.bgmLoopStart, 0f, length);
        float end = library.bgmLoopEnd > 0f ? Mathf.Min(library.bgmLoopEnd, length) : length;

        if (end - start > 0.1f)
        {
            if (library.bgmLoopEnd > 0f && t >= end)
            {
                bgmSource.time = Mathf.Min(start + (t - end), end - 0.01f);
            }
            else if (start > 0f && t + 0.05f < bgmLastTime && bgmLastTime > end - 1f)
            {
                // 곡 끝까지 재생돼 Unity가 0초로 되돌린 순간 -> 반복 구간 시작으로
                bgmSource.time = Mathf.Min(start + t, end - 0.01f);
            }
        }
        bgmLastTime = bgmSource.time;
    }

    // 씬에 켜진 AudioListener가 없으면 소리가 안 들리므로 이 오브젝트에 하나 둡니다. 씬에 이미 있으면 꺼서 중복 경고를 막습니다.
    private void EnsureListener()
    {
        bool sceneHasListener = false;
        var listeners = FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
        foreach (var l in listeners)
        {
            if (l != null && l != fallbackListener && l.isActiveAndEnabled)
            {
                sceneHasListener = true;
                break;
            }
        }

        if (sceneHasListener)
        {
            if (fallbackListener != null) fallbackListener.enabled = false;
        }
        else
        {
            if (fallbackListener == null) fallbackListener = gameObject.AddComponent<AudioListener>();
            fallbackListener.enabled = true;
        }
    }
}
