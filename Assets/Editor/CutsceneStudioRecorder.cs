using System.IO;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEditor.Recorder.Input;
using UnityEngine;

// [촬영 스튜디오 - 자동 녹화]
// CutsceneStudio의 녹화 시작/멈춤 알림을 받아 Unity Recorder로 Game 창을 녹화합니다.
// 저장: 프로젝트 폴더의 Recordings/장면이름_번호.mov (Apple ProRes 422, 1920x1080, 60fps 고정, 소리 없음). Recordings는 git에 올라가지 않습니다.
// MP4(H.264)는 움직임/파티클이 많은 게임 화면에서 화질이 뭉개져서 편집용 원본은 압축이 약한 ProRes로 찍습니다(파일은 훨씬 큼).
// 고정 프레임으로 찍기 때문에 촬영 중 화면이 느려 보여도 저장된 영상은 정확히 60fps로 매끄럽습니다.
// 에디터가 켜질 때(스크립트를 다시 읽을 때마다) 한 번 구독합니다.
[InitializeOnLoad]
public static class CutsceneStudioRecorder
{
    private const int Width = 1920;
    private const int Height = 1080;
    private const float FrameRate = 60f;
    private const string Extension = ".mov";

    private static RecorderController controller;
    private static RecorderControllerSettings controllerSettings;
    private static MovieRecorderSettings movieSettings;
    private static string currentFile;

    static CutsceneStudioRecorder()
    {
        CutsceneStudio.RecordingStartRequested -= StartRecording;
        CutsceneStudio.RecordingStartRequested += StartRecording;
        CutsceneStudio.RecordingStopRequested -= StopRecording;
        CutsceneStudio.RecordingStopRequested += StopRecording;
        EditorApplication.playModeStateChanged -= HandlePlayModeChanged;
        EditorApplication.playModeStateChanged += HandlePlayModeChanged;
    }

    private static void HandlePlayModeChanged(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.ExitingPlayMode) StopRecording();
    }

    private static void StartRecording(string shotName)
    {
        StopRecording();
        try
        {
            string folder = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Recordings");
            Directory.CreateDirectory(folder);
            string baseName = NextFileName(folder, SafeName(shotName));

            controllerSettings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
            movieSettings = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            movieSettings.name = "CutsceneStudio";
            movieSettings.Enabled = true;
            movieSettings.EncoderSettings = new ProResEncoderSettings
            {
                Format = ProResEncoderSettings.OutputFormat.ProRes422
            };
            movieSettings.CaptureAudio = false;
            movieSettings.ImageInputSettings = new GameViewInputSettings
            {
                OutputWidth = Width,
                OutputHeight = Height
            };
            movieSettings.OutputFile = Path.Combine(folder, baseName); // 확장자는 Recorder가 붙임

            controllerSettings.AddRecorderSettings(movieSettings);
            controllerSettings.SetRecordModeToManual();
            controllerSettings.FrameRatePlayback = FrameRatePlayback.Constant;
            controllerSettings.FrameRate = FrameRate;
            controllerSettings.CapFrameRate = true;

            controller = new RecorderController(controllerSettings);
            controller.PrepareRecording();
            if (!controller.StartRecording())
            {
                Debug.LogError("[촬영] 녹화를 시작하지 못했습니다. Console의 Recorder 메시지를 확인해주세요.");
                Cleanup();
                return;
            }
            currentFile = Path.Combine(folder, baseName + Extension);
        }
        catch (System.Exception e)
        {
            Debug.LogError("[촬영] 녹화를 시작하는 중 오류가 발생했습니다(장면 재생은 계속됩니다): " + e);
            Cleanup();
        }
    }

    private static void StopRecording()
    {
        if (controller == null) return;
        try
        {
            if (controller.IsRecording()) controller.StopRecording();
            if (!string.IsNullOrEmpty(currentFile)) Debug.Log("[촬영] 녹화 저장: " + currentFile);
        }
        catch (System.Exception e)
        {
            Debug.LogError("[촬영] 녹화를 멈추는 중 오류가 발생했습니다: " + e);
        }
        Cleanup();
    }

    private static void Cleanup()
    {
        controller = null;
        currentFile = null;
        movieSettings = null;
        controllerSettings = null;
    }

    // 장면이름_01, _02 ... 중 아직 없는 첫 번호
    private static string NextFileName(string folder, string shotName)
    {
        for (int n = 1; n < 1000; n++)
        {
            string candidate = shotName + "_" + n.ToString("00");
            if (!File.Exists(Path.Combine(folder, candidate + Extension))) return candidate;
        }
        return shotName + "_" + System.DateTime.Now.ToString("HHmmss");
    }

    private static string SafeName(string name)
    {
        if (string.IsNullOrEmpty(name)) return "Shot";
        foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
        return name.Trim();
    }
}
