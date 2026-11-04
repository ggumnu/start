using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 게임 시작 화면입니다. 배경 스프라이트 한 장이 화면 전체를 채우고, 화면 가운데 살짝 아래에
// [게임 시작] / [도감] / [설정] / [나가기] 버튼 네 개가 세로로 놓입니다.
//
// [사용 방법]
// Title 씬에 빈 오브젝트를 하나 만들어 이 스크립트만 붙이면 됩니다. Canvas/배경/버튼은 전부 코드가
// 자동으로 만듭니다(GameOverUI와 같은 방식). 인스펙터 값은 Play 중에 바꿔도 바로 반영됩니다.
//
// [버튼 동작]
// - 게임 시작: 로딩 화면(LoadingScreen)을 띄우고 gameSceneName 씬(LevelBuilder)을 불러옵니다. 그 씬의
//   GameFlowManager가 평소처럼 레벨 1을 만듭니다.
// - 도감: 아이템 도감 화면(ItemCodexUI)을 엽니다. 도감이 떠 있는 동안에는 이 화면의 버튼이 눌리지 않습니다.
// - 설정: 지금은 아무 기능 없음(버튼 반응만). 나중에 OnSettingsClicked()에 내용을 넣으면 됩니다.
// - 나가기: 게임을 종료합니다. 에디터에서는 Play가 멈춥니다.
//
// [버튼 모양]
// 버튼 스프라이트를 비우면 반투명 검은 박스로 그립니다. 직접 그린 버튼 스프라이트를 넣을 때는
// "평상시 색"을 흰색으로 바꾸세요(색은 스프라이트에 곱해집니다). 버튼마다 다른 스프라이트를 쓰려면
// 각 버튼의 "스프라이트" 칸에 넣으면 되고, 비워 두면 공통 스프라이트를 씁니다. 글자를 비우면 글자 없이
// 스프라이트만 보입니다(스프라이트에 글자까지 그려 넣은 경우).
public class TitleScreenUI : MonoBehaviour
{
    [Serializable]
    public class ButtonSettings
    {
        [Tooltip("버튼에 표시할 글자입니다. 비우면 글자를 표시하지 않습니다.")]
        public string label;

        [Tooltip("이 버튼만 따로 쓸 스프라이트입니다. 비우면 공통 버튼 스프라이트를 씁니다.")]
        public Sprite sprite;

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

    [Header("버튼 배치 (1920x1080 화면 기준 픽셀)")]
    public Vector2 buttonSize = new Vector2(360f, 80f);

    [Tooltip("버튼 사이 간격입니다.")]
    public float buttonSpacing = 24f;

    [Tooltip("버튼 묶음의 중심 위치입니다. (0, 0)이 화면 정중앙이고, Y가 음수면 아래쪽입니다.")]
    public Vector2 buttonsCenterOffset = new Vector2(0f, -200f);

    [Header("버튼 모양")]
    [Tooltip("네 버튼 공통 스프라이트입니다. 비우면 단색 박스로 그립니다. 9-slice 테두리가 설정된 스프라이트는 " +
             "늘려도 모서리가 깨지지 않게 그립니다.")]
    public Sprite buttonSprite;

    [Tooltip("평상시 색입니다(스프라이트에 곱해짐). 스프라이트를 넣었다면 흰색으로 바꾸세요.")]
    public Color normalColor = new Color(0f, 0f, 0f, 0.55f);

    [Tooltip("마우스를 올렸을 때 색입니다.")]
    public Color highlightedColor = new Color(0.25f, 0.45f, 0.75f, 0.8f);

    [Tooltip("누르고 있을 때 색입니다.")]
    public Color pressedColor = new Color(0.15f, 0.3f, 0.55f, 0.9f);

    [Tooltip("색이 바뀌는 데 걸리는 시간(초)입니다.")]
    [Min(0f)] public float colorFadeDuration = 0.1f;

    [Header("버튼 글자")]
    public int fontSize = 34;
    public Color textColor = Color.white;

    [Header("버튼별 설정")]
    public ButtonSettings startButton = new ButtonSettings("게임 시작");
    public ButtonSettings codexButton = new ButtonSettings("도감");
    public ButtonSettings settingsButton = new ButtonSettings("설정");
    public ButtonSettings quitButton = new ButtonSettings("나가기");

    [Header("표시 순서")]
    public int canvasSortingOrder = 0;

    private class ButtonView
    {
        public RectTransform rect;
        public Image image;
        public Button button;
        public TextMeshProUGUI text;
    }

    private Canvas canvas;
    private Image backgroundImage;
    private AspectRatioFitter backgroundFitter;
    private ButtonView startView;
    private ButtonView codexView;
    private ButtonView settingsView;
    private ButtonView quitView;

    private bool leaving; // 게임 시작/나가기를 이미 눌렀으면 중복 처리하지 않습니다.

    void Awake()
    {
        // 게임 오버 등에서 정지된 채 넘어온 경우를 대비해 시간 흐름과 커서를 원래대로 돌려 둡니다.
        Time.timeScale = 1f;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        EnsureEventSystem();
        BuildUI();
        ApplyLiveSettings();
    }

    void Update()
    {
        ApplyLiveSettings();
    }

    // ------------------------------------------------------------------
    // 버튼 동작
    // ------------------------------------------------------------------

    private void OnStartClicked()
    {
        if (leaving || ItemCodexUI.BlocksInput) return;

        if (!LoadingScreen.CanLoadScene(gameSceneName))
        {
            Debug.LogError($"TitleScreenUI: '{gameSceneName}' 씬을 찾지 못했습니다. 씬 이름이 맞는지, " +
                           "File > Build Profiles의 씬 목록에 들어 있는지 확인해주세요.");
            return;
        }

        leaving = true;
        // [입장 컷씬] [게임 시작]으로 들어갈 때만 다음 게임 씬에서 입장 컷씬이 한 번 나옵니다(재시작 경로들은 부르지 않음).
        IntroCutscene.RequestOnNextGameStart();
        string sceneName = gameSceneName;
        LoadingScreen.LoadSceneWithLoading(() => LoadingScreen.StartSceneLoad(sceneName));
    }

    // 아이템 도감 화면을 엽니다(ItemCodexUI.cs).
    private void OnCodexClicked()
    {
        if (leaving || ItemCodexUI.BlocksInput) return;
        ItemCodexUI.Open();
    }

    // 설정 화면은 아직 없습니다. 버튼 반응(색 변화)만 있습니다.
    public void OnSettingsClicked()
    {
        if (ItemCodexUI.BlocksInput) return;
    }

    private void OnQuitClicked()
    {
        if (leaving || ItemCodexUI.BlocksInput) return;
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

        // 버튼 묶음
        var groupGO = new GameObject("Buttons");
        groupGO.transform.SetParent(canvasGO.transform, false);
        var groupRect = groupGO.AddComponent<RectTransform>();
        groupRect.anchorMin = groupRect.anchorMax = new Vector2(0.5f, 0.5f);
        groupRect.pivot = new Vector2(0.5f, 0.5f);
        groupRect.sizeDelta = Vector2.zero;

        startView = CreateButton(groupGO.transform, "StartButton", OnStartClicked);
        codexView = CreateButton(groupGO.transform, "CodexButton", OnCodexClicked);
        settingsView = CreateButton(groupGO.transform, "SettingsButton", OnSettingsClicked);
        quitView = CreateButton(groupGO.transform, "QuitButton", OnQuitClicked);
    }

    private ButtonView CreateButton(Transform parent, string name, UnityEngine.Events.UnityAction onClick)
    {
        var view = new ButtonView();

        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        view.rect = go.AddComponent<RectTransform>();
        view.rect.anchorMin = view.rect.anchorMax = new Vector2(0.5f, 0.5f);
        view.rect.pivot = new Vector2(0.5f, 0.5f);

        view.image = go.AddComponent<Image>();
        view.image.color = Color.white; // 실제 색은 버튼의 색 전환(ColorTint)이 곱해서 정합니다.

        view.button = go.AddComponent<Button>();
        view.button.targetGraphic = view.image;
        view.button.transition = Selectable.Transition.ColorTint;
        view.button.navigation = new Navigation { mode = Navigation.Mode.None };
        view.button.onClick.AddListener(onClick);

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

        // 네 버튼을 위에서부터 [게임 시작] → [도감] → [설정] → [나가기] 순서로, 묶음 중심을 기준으로 배치합니다
        // (묶음 중심 = [도감]과 [설정] 사이).
        float step = buttonSize.y + buttonSpacing;
        ApplyButton(startView, startButton, buttonsCenterOffset + new Vector2(0f, step * 1.5f));
        ApplyButton(codexView, codexButton, buttonsCenterOffset + new Vector2(0f, step * 0.5f));
        ApplyButton(settingsView, settingsButton, buttonsCenterOffset - new Vector2(0f, step * 0.5f));
        ApplyButton(quitView, quitButton, buttonsCenterOffset - new Vector2(0f, step * 1.5f));
    }

    private void ApplyButton(ButtonView view, ButtonSettings settings, Vector2 position)
    {
        if (view == null || settings == null) return;

        view.rect.sizeDelta = buttonSize;
        view.rect.anchoredPosition = position;

        Sprite sprite = settings.sprite != null ? settings.sprite : buttonSprite;
        if (view.image.sprite != sprite) view.image.sprite = sprite;
        Image.Type wantedType = (sprite != null && sprite.border != Vector4.zero) ? Image.Type.Sliced : Image.Type.Simple;
        if (view.image.type != wantedType) view.image.type = wantedType;

        ColorBlock colors = view.button.colors;
        if (colors.normalColor != normalColor || colors.highlightedColor != highlightedColor ||
            colors.pressedColor != pressedColor || colors.selectedColor != normalColor ||
            !Mathf.Approximately(colors.fadeDuration, colorFadeDuration))
        {
            colors.normalColor = normalColor;
            colors.highlightedColor = highlightedColor;
            colors.pressedColor = pressedColor;
            colors.selectedColor = normalColor; // 클릭 뒤에도 "선택됨" 색으로 남지 않게 평상시 색과 같게 둡니다.
            colors.colorMultiplier = 1f;
            colors.fadeDuration = colorFadeDuration;
            view.button.colors = colors;
        }

        string label = settings.label ?? string.Empty;
        if (view.text.text != label) view.text.text = label;
        view.text.enabled = label.Length > 0;
        if (view.text.fontSize != fontSize) view.text.fontSize = fontSize;
        if (view.text.color != textColor) view.text.color = textColor;
    }
}
