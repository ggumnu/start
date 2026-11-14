using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 게임 시작 화면입니다. 배경 스프라이트 한 장이 화면 전체를 채우고, 왼쪽 위에 게임 로고, 화면 가운데 살짝 아래에
// [게임 시작] / [도감] / [조작법] / [설정] / [나가기] 버튼 다섯 개가 세로로 놓입니다.
//
// [시작 연출 - 게임을 실행한 뒤 처음 시작 화면이 열릴 때만]
// 1. 화면 전체가 흰색이었다가 서서히 걷히며 시작 화면이 밝아집니다.
// 2. 동시에 로고가 흰 빛(로고 모양 흰색 + 둘레 후광)으로 나타났다가, 빛이 걷히며 본래 색을 되찾습니다(셰이더 UIFlash).
// 3. 이어서 버튼이 위에서부터 차례로, 왼쪽에서 오른쪽으로 밀려 들어오며 투명한 상태에서 본래 색으로 나타납니다.
// - 버튼이 다 나타날 때까지 버튼은 반응하지 않습니다(호버/소리/클릭).
// - 연출 중 아무 곳이나 클릭하거나 아무 키를 누르면 연출을 건너뛰고 바로 완성된 화면이 됩니다(그 클릭은 버튼에 전달되지 않음).
// - 게임 중 그만두기/게임 오버 [그만하기]로 돌아올 때는 연출 없이 로딩 화면 뒤에 바로 완성된 화면이 보입니다.
// - 로고 셰이더 칸(Logo Flash Shader)이 비어 있고 셰이더도 찾지 못하면 흰 빛/후광 없이 로고가 서서히 나타나기만 합니다.
//
// [사용 방법]
// Title 씬에 빈 오브젝트를 하나 만들어 이 스크립트만 붙이면 됩니다. Canvas/배경/버튼은 전부 코드가
// 자동으로 만듭니다(GameOverUI와 같은 방식). 인스펙터 값은 Play 중에 바꿔도 바로 반영됩니다.
//
// [버튼 동작]
// - 게임 시작: 로딩 화면(LoadingScreen)을 띄우고 gameSceneName 씬(LevelBuilder)을 불러옵니다. 그 씬의
//   GameFlowManager가 평소처럼 레벨 1을 만듭니다.
// - 도감: 아이템 도감 화면(ItemCodexUI)을 엽니다. 도감이 떠 있는 동안에는 이 화면의 버튼이 눌리지 않습니다.
// - 조작법: 조작법 창(ControlsUI)을 엽니다.
// - 설정: 볼륨 설정 창(SettingsUI)을 엽니다.
// - 나가기: 게임을 종료합니다. 에디터에서는 Play가 멈춥니다.
//
// [버튼 모양]
// 다른 팝업과 같은 블루 아카이브 스타일 평행사변형 버튼(공용 부품 UIPatternBox - 색/모양/글자 색은 PopupStyle)입니다.
// [게임 시작] = 긍정(노랑), [도감]/[조작법]/[설정] = 일반(하늘색), [나가기] = 그만두기(회색). 마우스를 올리면 색만 어두워지고,
// 누르는 동안 작아지며, 버튼 위에서 뗀 순간 실행됩니다. 도감/조작법/설정 창이 떠 있는 동안에는 반응하지 않습니다.
public class TitleScreenUI : MonoBehaviour
{
    [Serializable]
    public class ButtonSettings
    {
        [Tooltip("버튼에 표시할 글자입니다. 비우면 글자를 표시하지 않습니다.")]
        public string label;

        public ButtonSettings(string label) { this.label = label; }
    }

    [Header("씬")]
    [Tooltip("[게임 시작]을 누르면 불러올 씬 이름입니다.")]
    public string gameSceneName = "LevelBuilder";

    [Header("배경")]
    [Tooltip("화면 전체를 채울 배경 스프라이트입니다. 원래 비율을 유지한 채 화면을 꽉 채우고, 남는 부분은 잘립니다.")]
    public Sprite backgroundSprite;

    [Tooltip("배경 스프라이트에 곱해지는 색입니다. 스프라이트가 비어 있으면 이 색으로 화면을 칠합니다.")]
    public Color backgroundColor = Color.white;

    [Header("로고 (왼쪽 위)")]
    [Tooltip("왼쪽 위에 띄울 게임 로고입니다(Logo_ReisaArchive). 비우면 로고를 표시하지 않습니다. " +
             "가져오기 설정의 Sprite Mode가 Single이어야 넣을 수 있습니다.")]
    public Sprite logoSprite;
    [Tooltip("로고 가로 크기입니다(1920x1080 화면 기준 픽셀). 세로는 원래 비율에 맞춰 자동으로 정해집니다.")]
    [Min(10f)] public float logoWidth = 720f;
    [Tooltip("화면 왼쪽 위 모서리에서 로고까지의 여백입니다. x = 왼쪽 여백, y = 위쪽 여백.")]
    public Vector2 logoMargin = new Vector2(60f, 40f);

    [Header("시작 연출 (게임 첫 실행 때만, 실제 시간 기준 초)")]
    [Tooltip("로고 흰 빛/후광 셰이더입니다(Shaders/UIFlash). 게임을 빌드했을 때 셰이더가 빠지지 않도록 여기에 넣어 두세요.")]
    public Shader logoFlashShader;
    [Tooltip("처음에 화면 전체를 덮고 있다가 걷히는 색입니다.")]
    public Color introScreenColor = Color.white;
    [Tooltip("화면 전체가 흰색에서 시작 화면으로 밝아지는 시간입니다.")]
    [Min(0f)] public float screenFadeDuration = 1.0f;
    [Tooltip("연출 시작 후 로고가 나타나기 시작하는 시점입니다.")]
    [Min(0f)] public float logoAppearStart = 0.3f;
    [Tooltip("로고가 흰 빛으로 나타나는 데 걸리는 시간입니다.")]
    [Min(0f)] public float logoAppearDuration = 0.6f;
    [Tooltip("흰 빛이 걷히며 로고가 본래 색을 되찾는 데 걸리는 시간입니다.")]
    [Min(0f)] public float logoRestoreDuration = 0.7f;
    [Tooltip("로고 둘레 후광 색입니다.")]
    public Color logoGlowColor = Color.white;
    [Tooltip("후광이 로고 밖으로 번지는 거리입니다(1920x1080 기준 픽셀). 0이면 후광 없음.")]
    [Min(0f)] public float logoGlowSize = 40f;
    [Tooltip("후광 밝기입니다. 클수록 진하고 넓게 보입니다.")]
    [Min(0f)] public float logoGlowStrength = 2.5f;
    [Tooltip("연출 시작 후 첫 버튼([게임 시작])이 나타나기 시작하는 시점입니다.")]
    [Min(0f)] public float buttonsAppearStart = 1.1f;
    [Tooltip("버튼끼리 나타나는 시간차입니다(위에서부터 차례로).")]
    [Min(0f)] public float buttonStagger = 0.08f;
    [Tooltip("버튼 하나가 나타나는 데 걸리는 시간입니다.")]
    [Min(0f)] public float buttonAppearDuration = 0.3f;
    [Tooltip("버튼이 왼쪽에서 밀려 들어오는 거리입니다(처음에 이만큼 왼쪽에서 시작).")]
    public float buttonSlideDistance = 60f;
    [Tooltip("켜면 연출 중 아무 곳이나 클릭하거나 아무 키를 눌러 연출을 건너뛸 수 있습니다.")]
    public bool allowSkip = true;

    [Header("버튼 배치 (1920x1080 화면 기준 픽셀)")]
    public Vector2 buttonSize = new Vector2(360f, 80f);

    [Tooltip("버튼 사이 간격입니다.")]
    public float buttonSpacing = 24f;

    [Tooltip("버튼 묶음의 중심 위치입니다. (0, 0)이 화면 정중앙이고, Y가 음수면 아래쪽입니다.")]
    public Vector2 buttonsCenterOffset = new Vector2(0f, -200f);

    [Header("버튼 글자 (색/버튼 모양은 PopupStyle)")]
    public int fontSize = 34;

    [Header("버튼별 설정")]
    public ButtonSettings startButton = new ButtonSettings("게임 시작");
    public ButtonSettings codexButton = new ButtonSettings("도감");
    public ButtonSettings controlsButton = new ButtonSettings("조작법");
    public ButtonSettings settingsButton = new ButtonSettings("설정");
    public ButtonSettings quitButton = new ButtonSettings("나가기");

    [Header("표시 순서")]
    public int canvasSortingOrder = 0;

    private class ButtonView
    {
        public RectTransform rect;
        public UIPatternBox box;
        public TextMeshProUGUI text;
        public UIButtonKind kind;
        public Action onClick;
        public CanvasGroup group; // 시작 연출 - 버튼 전체(바탕+글자) 투명도
    }

    // 게임을 실행한 뒤 시작 연출을 이미 보여줬는지(씬이 바뀌어도 유지 - 그만두기/게임 오버로 돌아오면 연출 없음)
    private static bool introPlayedThisRun;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        introPlayedThisRun = false; // 에디터에서 Play를 새로 누를 때마다 "첫 실행"으로 봅니다
    }

    private Canvas canvas;
    private Image backgroundImage;
    private AspectRatioFitter backgroundFitter;
    private ButtonView startView;
    private ButtonView codexView;
    private ButtonView controlsView;
    private ButtonView settingsView;
    private ButtonView quitView;

    // 로고 / 시작 연출
    private RectTransform logoRect;
    private Image logoImage;
    private RawImage logoGlow;
    private RectTransform logoGlowRect;
    private Material logoFlashMat;
    private Material logoGlowMat;
    private Image introOverlay;
    private bool introPlaying;
    private float introTime;

    private bool leaving; // 게임 시작/나가기를 이미 눌렀으면 중복 처리하지 않습니다.

    void Awake()
    {
        // 게임 오버 등에서 정지된 채 넘어온 경우를 대비해 시간 흐름과 커서를 원래대로 돌려 둡니다.
        Time.timeScale = 1f;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        EnsureEventSystem();
        BuildUI();

        // 게임을 실행한 뒤 처음 열린 시작 화면에서만 연출합니다.
        introPlaying = !introPlayedThisRun;
        introPlayedThisRun = true;
        introTime = 0f;

        ApplyLiveSettings();
        ApplyIntroVisual(); // 첫 프레임부터 흰 화면/투명한 버튼으로 그려지도록
    }

    void OnDestroy()
    {
        if (logoFlashMat != null) Destroy(logoFlashMat);
        if (logoGlowMat != null) Destroy(logoGlowMat);
    }

    void Update()
    {
        ApplyLiveSettings();

        // 시작 연출 진행. 아무 곳이나 클릭/아무 키 = 건너뛰기(그 프레임의 클릭은 버튼에 전달하지 않음).
        bool skippedNow = false;
        if (introPlaying)
        {
            introTime += Mathf.Min(Time.unscaledDeltaTime, 1f / 30f); // 실행 직후 긴 프레임에 연출이 건너뛰어지지 않도록 상한
            if (allowSkip && Input.anyKeyDown)
            {
                introPlaying = false;
                skippedNow = true;
            }
            else if (introTime >= IntroLength())
            {
                introPlaying = false;
            }
        }
        ApplyIntroVisual();

        // 도감/조작법/설정 창이 떠 있거나, 씬을 떠나는 중이거나, 시작 연출 중이면 호버/누름/클릭 없이 평소 색으로 둡니다.
        bool blocked = leaving || introPlaying || skippedNow || ItemCodexUI.BlocksInput || ControlsUI.BlocksInput || SettingsUI.BlocksInput;
        TickButton(startView, blocked);
        TickButton(codexView, blocked);
        TickButton(controlsView, blocked);
        TickButton(settingsView, blocked);
        TickButton(quitView, blocked);
    }

    private void TickButton(ButtonView view, bool blocked)
    {
        if (view == null) return;
        bool hovered = !blocked && RectTransformUtility.RectangleContainsScreenPoint(view.rect, Input.mousePosition, null);
        bool clicked = view.box.TickButton(hovered, true);
        if (clicked && !blocked) view.onClick?.Invoke();
    }

    // ------------------------------------------------------------------
    // 버튼 동작
    // ------------------------------------------------------------------

    private void OnStartClicked()
    {
        if (leaving || ItemCodexUI.BlocksInput || ControlsUI.BlocksInput || SettingsUI.BlocksInput) return;

        if (!LoadingScreen.CanLoadScene(gameSceneName))
        {
            Debug.LogError($"TitleScreenUI: '{gameSceneName}' 씬을 찾지 못했습니다. 씬 이름이 맞는지, " +
                           "File > Build Profiles의 씬 목록에 들어 있는지 확인해주세요.");
            return;
        }

        UISound.Confirm();
        leaving = true;
        // [입장 컷씬] [게임 시작]으로 들어갈 때만 다음 게임 씬에서 입장 컷씬이 한 번 나옵니다(재시작 경로들은 부르지 않음).
        IntroCutscene.RequestOnNextGameStart();
        string sceneName = gameSceneName;
        LoadingScreen.LoadSceneWithLoading(() => LoadingScreen.StartSceneLoad(sceneName));
    }

    // 아이템 도감 화면을 엽니다(ItemCodexUI.cs).
    private void OnCodexClicked()
    {
        if (leaving || ItemCodexUI.BlocksInput || ControlsUI.BlocksInput || SettingsUI.BlocksInput) return;
        UISound.Confirm();
        ItemCodexUI.Open();
    }

    // 조작법 창(ControlsUI)을 엽니다.
    private void OnControlsClicked()
    {
        if (leaving || ItemCodexUI.BlocksInput || ControlsUI.BlocksInput || SettingsUI.BlocksInput) return;
        UISound.Confirm();
        ControlsUI.Open();
    }

    // 볼륨 설정 창(SettingsUI)을 엽니다.
    public void OnSettingsClicked()
    {
        if (ItemCodexUI.BlocksInput || ControlsUI.BlocksInput || SettingsUI.BlocksInput) return;
        UISound.Confirm();
        SettingsUI.Open();
    }

    private void OnQuitClicked()
    {
        if (leaving || ItemCodexUI.BlocksInput || ControlsUI.BlocksInput || SettingsUI.BlocksInput) return;
        UISound.Cancel();
        leaving = true;

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ------------------------------------------------------------------
    // UI 생성
    // ------------------------------------------------------------------

    // uGUI 버튼 클릭에는 EventSystem이 필요합니다. 씬에 이미 하나 있으면 그걸 쓰고, 없으면 만듭니다.
    private void EnsureEventSystem()
    {
        if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() != null) return;

        var esGO = new GameObject("EventSystem");
        esGO.AddComponent<EventSystem>();
        esGO.AddComponent<StandaloneInputModule>();
    }

    private void BuildUI()
    {
        var canvasGO = new GameObject("TitleCanvas");
        canvasGO.transform.SetParent(transform, false);
        canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        canvasGO.AddComponent<GraphicRaycaster>();

        // 배경: 화면 전체 크기의 틀 안에서, 스프라이트 비율을 유지한 채 틀을 꽉 덮도록(EnvelopeParent) 그립니다.
        var bgFrame = new GameObject("BackgroundFrame");
        bgFrame.transform.SetParent(canvasGO.transform, false);
        var frameRect = bgFrame.AddComponent<RectTransform>();
        frameRect.anchorMin = Vector2.zero;
        frameRect.anchorMax = Vector2.one;
        frameRect.offsetMin = Vector2.zero;
        frameRect.offsetMax = Vector2.zero;
        bgFrame.AddComponent<RectMask2D>(); // 화면 밖으로 넘친 부분이 다른 UI 위로 보이지 않게 잘라냅니다.

        var bgGO = new GameObject("Background");
        bgGO.transform.SetParent(bgFrame.transform, false);
        var bgRect = bgGO.AddComponent<RectTransform>();
        bgRect.anchorMin = bgRect.anchorMax = new Vector2(0.5f, 0.5f);
        bgRect.pivot = new Vector2(0.5f, 0.5f);
        backgroundImage = bgGO.AddComponent<Image>();
        backgroundImage.raycastTarget = false;
        backgroundFitter = bgGO.AddComponent<AspectRatioFitter>();
        backgroundFitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;

        // 로고(왼쪽 위) - 후광(로고보다 넓은 칸) 위에 로고 본체. 흰 화면 막보다 위에 그려지도록 막 다음에 순서를 맞춥니다.
        var logoGO = new GameObject("Logo");
        logoGO.transform.SetParent(canvasGO.transform, false);
        logoRect = logoGO.AddComponent<RectTransform>();
        logoRect.anchorMin = logoRect.anchorMax = new Vector2(0f, 1f);
        logoRect.pivot = new Vector2(0f, 1f);

        var glowGO = new GameObject("LogoGlow");
        glowGO.transform.SetParent(logoGO.transform, false);
        logoGlowRect = glowGO.AddComponent<RectTransform>();
        logoGlowRect.anchorMin = Vector2.zero;
        logoGlowRect.anchorMax = Vector2.one;
        logoGlow = glowGO.AddComponent<RawImage>();
        logoGlow.raycastTarget = false;

        var logoImageGO = new GameObject("LogoImage");
        logoImageGO.transform.SetParent(logoGO.transform, false);
        var logoImageRect = logoImageGO.AddComponent<RectTransform>();
        logoImageRect.anchorMin = Vector2.zero;
        logoImageRect.anchorMax = Vector2.one;
        logoImageRect.offsetMin = Vector2.zero;
        logoImageRect.offsetMax = Vector2.zero;
        logoImage = logoImageGO.AddComponent<Image>();
        logoImage.raycastTarget = false;

        Shader flashShader = logoFlashShader != null ? logoFlashShader : Shader.Find("MobArchive/UIFlash");
        if (flashShader != null && flashShader.isSupported)
        {
            logoFlashMat = new Material(flashShader) { name = "LogoFlash (Runtime)" };
            logoFlashMat.SetFloat("_GlowMode", 0f);
            logoGlowMat = new Material(flashShader) { name = "LogoGlow (Runtime)" };
            logoGlowMat.SetFloat("_GlowMode", 1f);
            logoImage.material = logoFlashMat;
            logoGlow.material = logoGlowMat;
        }

        // 버튼 묶음
        var groupGO = new GameObject("Buttons");
        groupGO.transform.SetParent(canvasGO.transform, false);
        var groupRect = groupGO.AddComponent<RectTransform>();
        groupRect.anchorMin = groupRect.anchorMax = new Vector2(0.5f, 0.5f);
        groupRect.pivot = new Vector2(0.5f, 0.5f);
        groupRect.sizeDelta = Vector2.zero;

        startView = CreateButton(groupGO.transform, "StartButton", UIButtonKind.Positive, OnStartClicked);
        codexView = CreateButton(groupGO.transform, "CodexButton", UIButtonKind.Normal, OnCodexClicked);
        controlsView = CreateButton(groupGO.transform, "ControlsButton", UIButtonKind.Normal, OnControlsClicked);
        settingsView = CreateButton(groupGO.transform, "SettingsButton", UIButtonKind.Normal, OnSettingsClicked);
        quitView = CreateButton(groupGO.transform, "QuitButton", UIButtonKind.Quit, OnQuitClicked);

        // 시작 연출의 흰 화면 막(버튼 위, 로고 아래)
        var overlayGO = new GameObject("IntroOverlay");
        overlayGO.transform.SetParent(canvasGO.transform, false);
        var overlayRect = overlayGO.AddComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;
        introOverlay = overlayGO.AddComponent<Image>();
        introOverlay.raycastTarget = false;

        logoGO.transform.SetAsLastSibling();
    }

    private ButtonView CreateButton(Transform parent, string name, UIButtonKind kind, Action onClick)
    {
        var view = new ButtonView { kind = kind, onClick = onClick };

        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        view.rect = go.AddComponent<RectTransform>();
        view.rect.anchorMin = view.rect.anchorMax = new Vector2(0.5f, 0.5f);
        view.rect.pivot = new Vector2(0.5f, 0.5f);

        // 클릭/호버/누름은 Update의 TickButton이 직접 처리합니다. raycastTarget은 호버음(UIButtonHoverSound)이 마우스를 감지하도록 켬.
        view.box = UIPatternBox.AddTo(go);
        view.box.raycastTarget = true;
        view.group = go.AddComponent<CanvasGroup>();
        // [사운드] 마우스를 올리면 호버음(도감이 떠 있거나 씬을 떠나는 중이거나 시작 연출 중이면 소리 없음)
        go.AddComponent<UIButtonHoverSound>().isBlocked = () => leaving || introPlaying || ItemCodexUI.BlocksInput || ControlsUI.BlocksInput || SettingsUI.BlocksInput;

        var textGO = new GameObject("Label");
        textGO.transform.SetParent(go.transform, false);
        var textRect = textGO.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        // [TMP] 옛 방식 Text는 화면 배율에 따라 흐릿하게 보여서 TextMeshPro로 교체했습니다(HUDText 참고).
        view.text = HUDText.AddTo(textGO, TextAnchor.MiddleCenter);

        return view;
    }

    // ------------------------------------------------------------------
    // 인스펙터 값 실시간 반영
    // ------------------------------------------------------------------

    private void ApplyLiveSettings()
    {
        if (canvas != null) canvas.sortingOrder = canvasSortingOrder;

        if (backgroundImage != null)
        {
            backgroundImage.sprite = backgroundSprite;
            backgroundImage.color = backgroundColor;

            float aspect = 16f / 9f;
            if (backgroundSprite != null && backgroundSprite.rect.height > 0f)
                aspect = backgroundSprite.rect.width / backgroundSprite.rect.height;
            if (!Mathf.Approximately(backgroundFitter.aspectRatio, aspect))
                backgroundFitter.aspectRatio = aspect;
        }

        // 다섯 버튼을 위에서부터 [게임 시작] → [도감] → [조작법] → [설정] → [나가기] 순서로, 묶음 중심을 기준으로 배치합니다
        // (묶음 중심 = 가운데 [조작법]).
        float step = buttonSize.y + buttonSpacing;
        ApplyButton(startView, startButton, buttonsCenterOffset + new Vector2(0f, step * 2f));
        ApplyButton(codexView, codexButton, buttonsCenterOffset + new Vector2(0f, step * 1f));
        ApplyButton(controlsView, controlsButton, buttonsCenterOffset);
        ApplyButton(settingsView, settingsButton, buttonsCenterOffset - new Vector2(0f, step * 1f));
        ApplyButton(quitView, quitButton, buttonsCenterOffset - new Vector2(0f, step * 2f));

        ApplyLogo();
    }

    // 로고 크기/위치와 후광 칸(로고보다 logoGlowSize만큼 넓음 - 넓어진 만큼 로고를 작게 비춰서 둘레에 빛이 번질 자리를 만듦).
    private void ApplyLogo()
    {
        if (logoRect == null) return;

        bool show = logoSprite != null;
        if (logoRect.gameObject.activeSelf != show) logoRect.gameObject.SetActive(show);
        if (!show) return;

        float w = logoWidth;
        float h = logoSprite.rect.width > 0f ? w * logoSprite.rect.height / logoSprite.rect.width : w;
        logoRect.sizeDelta = new Vector2(w, h);
        logoRect.anchoredPosition = new Vector2(logoMargin.x, -logoMargin.y);
        if (logoImage.sprite != logoSprite) logoImage.sprite = logoSprite;

        bool glowOn = logoGlowMat != null && logoGlowSize > 0f;
        if (logoGlow.gameObject.activeSelf != glowOn) logoGlow.gameObject.SetActive(glowOn);
        if (!glowOn) return;

        float pad = logoGlowSize;
        logoGlowRect.offsetMin = new Vector2(-pad, -pad);
        logoGlowRect.offsetMax = new Vector2(pad, pad);

        Texture tex = logoSprite.texture;
        if (logoGlow.texture != tex) logoGlow.texture = tex;
        Rect tr = logoSprite.textureRect;
        float tw = Mathf.Max(1f, tex.width), th = Mathf.Max(1f, tex.height);
        float u0 = tr.xMin / tw, v0 = tr.yMin / th, uw = tr.width / tw, vh = tr.height / th;
        float du = pad / w * uw, dv = pad / h * vh;
        logoGlow.uvRect = new Rect(u0 - du, v0 - dv, uw + du * 2f, vh + dv * 2f);
        logoGlowMat.SetVector("_UVBounds", new Vector4(u0, v0, u0 + uw, v0 + vh));
        logoGlowMat.SetVector("_GlowRadius", new Vector4(du, dv, 0f, 0f));
        logoGlowMat.SetFloat("_GlowStrength", logoGlowStrength);
        logoGlowMat.SetColor("_GlowColor", logoGlowColor);
    }

    // ------------------------------------------------------------------
    // 시작 연출
    // ------------------------------------------------------------------

    // 연출 전체 길이(화면 밝아짐 / 로고 / 마지막 버튼 중 가장 늦게 끝나는 것)
    private float IntroLength()
    {
        float logoEnd = logoAppearStart + logoAppearDuration + logoRestoreDuration;
        float buttonsEnd = buttonsAppearStart + buttonStagger * 4f + buttonAppearDuration;
        return Mathf.Max(screenFadeDuration, Mathf.Max(logoEnd, buttonsEnd));
    }

    private static float Progress(float time, float start, float duration)
    {
        if (duration <= 0f) return time >= start ? 1f : 0f;
        return Mathf.Clamp01((time - start) / duration);
    }

    private static float EaseOutCubic(float t)
    {
        float u = 1f - t;
        return 1f - u * u * u;
    }

    private static float SmoothStep01(float t)
    {
        return t * t * (3f - 2f * t);
    }

    // 지금 연출 시각에 맞춰 흰 화면 막/로고/버튼을 그립니다. ApplyLiveSettings 뒤에 불러야 합니다(버튼 원래 자리에 이동량만 더함).
    // 연출이 끝났거나 없으면 막은 꺼지고 로고/버튼은 본래 모습입니다.
    private void ApplyIntroVisual()
    {
        float t = introTime;

        // 흰 화면 막
        float overlayA = introPlaying ? 1f - SmoothStep01(Progress(t, 0f, screenFadeDuration)) : 0f;
        bool overlayOn = overlayA > 0f;
        if (introOverlay.gameObject.activeSelf != overlayOn) introOverlay.gameObject.SetActive(overlayOn);
        if (overlayOn)
        {
            Color c = introScreenColor;
            c.a *= overlayA;
            introOverlay.color = c;
        }

        // 로고: 흰 빛으로 나타남 → 빛이 걷히며 본래 색
        float appear = introPlaying ? EaseOutCubic(Progress(t, logoAppearStart, logoAppearDuration)) : 1f;
        float restore = introPlaying ? SmoothStep01(Progress(t, logoAppearStart + logoAppearDuration, logoRestoreDuration)) : 1f;
        float flash = 1f - restore;
        if (logoImage != null)
        {
            logoImage.color = new Color(1f, 1f, 1f, appear);
            if (logoFlashMat != null) logoFlashMat.SetFloat("_Flash", flash);
        }
        if (logoGlow != null) logoGlow.color = new Color(1f, 1f, 1f, appear * flash);

        // 버튼: 위에서부터 차례로, 왼쪽에서 밀려 들어오며 나타남
        IntroButton(startView, 0, t);
        IntroButton(codexView, 1, t);
        IntroButton(controlsView, 2, t);
        IntroButton(settingsView, 3, t);
        IntroButton(quitView, 4, t);
    }

    private void IntroButton(ButtonView view, int order, float t)
    {
        if (view == null) return;
        float e = introPlaying ? EaseOutCubic(Progress(t, buttonsAppearStart + buttonStagger * order, buttonAppearDuration)) : 1f;
        view.group.alpha = e;
        if (e < 1f) view.rect.anchoredPosition += new Vector2(-buttonSlideDistance * (1f - e), 0f);
    }

    private void ApplyButton(ButtonView view, ButtonSettings settings, Vector2 position)
    {
        if (view == null || settings == null) return;

        view.rect.sizeDelta = buttonSize;
        view.rect.anchoredPosition = position;
        view.box.SetupButton(PopupStyle.Get(), view.kind); // 색은 Update의 TickButton이 호버에 따라 정함

        string label = settings.label ?? string.Empty;
        if (view.text.text != label) view.text.text = label;
        view.text.enabled = label.Length > 0;
        if (view.text.fontSize != fontSize) view.text.fontSize = fontSize;
        Color textColor = view.box.GetLabelColor(true);
        if (view.text.color != textColor) view.text.color = textColor;
    }
}
