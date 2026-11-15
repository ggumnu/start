using TMPro;
using UnityEngine;
using UnityEngine.UI;

// [게임 클리어 결과 화면 (형빈 기획)]
// 마지막 레벨(GameFlowManager.gameClearLevel) 보스를 깨고 헬기 [예] -> 사다리가 끌어올려지기 시작하는 순간 HeliExtraction이 Show로 엽니다.
// 1) 화면 전체에 반투명 검은 막이 깔림(헬기/플레이어는 그 뒤에서 평소처럼 떠남).
// 2) "BATTLE COMPLETE"가 오른쪽에서 밀려 들어오며 투명 -> 흰빛 -> 본래 색으로 나타남. 글자는 오른쪽으로 기울어지고(OnPreRenderText에서
//    모양을 직접 기울임), 안쪽은 코드로 만든 그림(위->아래 노랑 그라데이션 + 양 끝 삼각 무늬)을 입히며(TMP Face Texture, 줄 단위 매핑),
//    아래에 그림자(TMP Underlay)가 깔림. 흰빛은 같은 모양의 흰 글자를 위에 겹쳤다가 걷는 방식.
// 3) 화면 정중앙의 흰 팝업 창이 아래에서 올라오며 나타남(글자는 창 왼쪽 위 모서리에 붙음).
// 4) 전투 시간 / 처치한 적 / 클리어한 방 - 항목 이름은 창과 함께 보이고, 오른쪽 남색 판이 차례로 아래에서 올라오며 나타난 뒤 수치가 0부터 올라감.
// ESC = 남은 연출을 모두 건너뛰고 완성된 화면으로(연출이 끝난 뒤의 ESC/Enter는 아무 동작 없음).
// [재시작하기](노랑) = 지금 씬을 다시 불러와 레벨 1부터, [그만하기](하늘색) = 시작 화면. 재확인 창 없이 바로, 소리는 진행음.
// 수치는 GameFlowManager의 기록(PlayTimeSeconds/EnemiesKilled/RoomsCleared)을 열 때 한 번 읽습니다. 설정은 GameClearUISettings.
// 씬 오브젝트로 만들어져 씬을 떠나면 함께 사라집니다(재시작/그만하기는 로딩 화면을 띄운 채 씬을 불러옴).
public class GameClearUI : MonoBehaviour
{
    private static GameClearUI instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
    }

    public static bool IsOpen => instance != null && instance.isOpen;

    // 결과 화면이 떠 있으면 true - ESC 중단 화면/R 꾹 재시작을 막습니다(한 번 뜨면 씬을 떠날 때까지 닫히지 않음).
    public static bool BlocksInput => IsOpen;

    // 결과 화면을 엽니다. skipToEnd = 연출 없이 완성된 화면으로(헬기 탑승 중 ESC). 이미 떠 있으면 skipToEnd일 때만 끝으로 건너뜁니다.
    public static void Show(bool skipToEnd)
    {
        RunAnalytics.NotifyGameClear(); // [플레이 통계] 클리어 기록(여러 번 불려도 한 번만 기록됨)
        if (instance == null)
        {
            var go = new GameObject("GameClearUI");
            instance = go.AddComponent<GameClearUI>();
        }
        instance.Open(skipToEnd);
    }

    private class RowView
    {
        public TextMeshProUGUI label;
        public RectTransform badgeRect;
        public CanvasGroup badgeGroup;
        public UIPatternBox badgeBox;
        public TextMeshProUGUI value;
    }

    private class ButtonView
    {
        public RectTransform rect;
        public UIPatternBox box;
        public TextMeshProUGUI label;
    }

    private bool isOpen;
    private bool leaving;
    private float timeline;          // 연출 시간(실제 시간 기준)
    private int openedFrame = -1;
    private int lastHoveredButton = -1;

    // 열 때 읽은 기록
    private float statSeconds;
    private int statKills;
    private int statRooms;

    private GameObject canvasGO;
    private Canvas canvas;
    private CanvasScaler scaler;
    private Image dimImage;

    private RectTransform titleRect;
    private CanvasGroup titleGroup;
    private TextMeshProUGUI titleText;
    private CanvasGroup flashGroup;
    private TextMeshProUGUI flashText;
    private Material titleMaterial;
    private Texture2D titleTexture;
    private float appliedSlant = -1f;

    private RectTransform panelRect;
    private CanvasGroup panelGroup;
    private UIPatternBox panelBox;
    private readonly RowView[] rows = new RowView[3];
    private ButtonView restartButton;
    private ButtonView quitButton;

    void Awake()
    {
        BuildUI();
        canvasGO.SetActive(false);
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
        if (titleText != null) titleText.OnPreRenderText -= ShearText;
        if (flashText != null) flashText.OnPreRenderText -= ShearText;
        if (titleTexture != null) Destroy(titleTexture);
    }

    private void Open(bool skipToEnd)
    {
        var set = GameClearUISettings.Get();
        if (!isOpen)
        {
            isOpen = true;
            leaving = false;
            openedFrame = Time.frameCount;
            timeline = 0f;

            var flow = FindFirstObjectByType<GameFlowManager>();
            statSeconds = flow != null ? flow.PlayTimeSeconds : 0f;
            statKills = flow != null ? flow.EnemiesKilled : 0;
            statRooms = flow != null ? flow.RoomsCleared : 0;

            canvasGO.SetActive(true);
            SetupTitle(set);
            restartButton.box.ResetPress();
            quitButton.box.ResetPress();
        }
        if (skipToEnd) timeline = Mathf.Max(timeline, EndTime(set));
        ApplyLayout(set);
        ApplyTimeline(set);
    }

    void Update()
    {
        if (!isOpen) return;
        var set = GameClearUISettings.Get();

        float end = EndTime(set);
        if (timeline < end)
        {
            // ESC = 남은 연출 건너뛰기(연 그 프레임의 ESC는 헬기가 이미 처리했으므로 제외)
            if (Time.frameCount != openedFrame && Input.GetKeyDown(KeyCode.Escape)) timeline = end;
            else timeline += Time.unscaledDeltaTime;
        }

        ApplyLayout(set);
        float popupProgress = ApplyTimeline(set);

        bool interactable = !leaving && popupProgress >= 1f;
        bool overRestart = interactable && IsPointerOver(restartButton.rect);
        bool overQuit = interactable && IsPointerOver(quitButton.rect);
        bool clickRestart = restartButton.box.TickButton(overRestart, interactable);
        bool clickQuit = quitButton.box.TickButton(overQuit, interactable);
        // 못 누르는 동안(창이 올라오는 중)에도 버튼은 본래 색으로 보이게(창 투명도로만 나타남)
        if (!interactable)
        {
            restartButton.box.color = PopupStyle.Get().GetKindColor(UIButtonKind.Positive, false, true);
            quitButton.box.color = PopupStyle.Get().GetKindColor(UIButtonKind.Normal, false, true);
        }
        UISound.TrackHover(ref lastHoveredButton, UISound.HoveredId(0, overRestart, overQuit), Time.frameCount == openedFrame);

        if (clickRestart) Leave(true);
        else if (clickQuit) Leave(false);
    }

    // ------------------------------------------------------------------
    // 버튼 동작
    // ------------------------------------------------------------------

    private void Leave(bool restart)
    {
        if (leaving) return;
        var set = GameClearUISettings.Get();

        if (!restart && !LoadingScreen.CanLoadScene(set.titleSceneName))
        {
            Debug.LogError($"GameClearUI: 시작 화면 씬 '{set.titleSceneName}'을 찾지 못했습니다. 씬 이름이 맞는지(Game Clear UI Settings의 " +
                           "Title Scene Name), File > Build Profiles의 씬 목록에 들어 있는지 확인해주세요.");
            return;
        }

        leaving = true;
        UISound.Confirm();
        Time.timeScale = 1f;
        if (restart)
        {
            LoadingScreen.LoadSceneWithLoading(LoadingScreen.StartReloadActiveScene);
        }
        else
        {
            string sceneName = set.titleSceneName;
            LoadingScreen.LoadSceneWithLoading(() => LoadingScreen.StartSceneLoad(sceneName));
        }
    }

    // ------------------------------------------------------------------
    // 연출
    // ------------------------------------------------------------------

    private static float PopupStart(GameClearUISettings s) => s.titleDelay + s.popupDelay;
    private static float RowsStart(GameClearUISettings s) => PopupStart(s) + s.popupAppearDuration + s.rowsDelay;
    private static float RowStart(GameClearUISettings s, int i) => RowsStart(s) + s.rowInterval * i;
    private static float EndTime(GameClearUISettings s) => RowStart(s, 2) + s.badgeAppearDuration + s.countDuration;

    private static float Progress(float t, float start, float duration)
    {
        if (duration <= 0f) return t >= start ? 1f : 0f;
        return Mathf.Clamp01((t - start) / duration);
    }

    private static float EaseOutCubic(float x)
    {
        float u = 1f - x;
        return 1f - u * u * u;
    }

    // 지금 연출 시간에 맞춰 막/글자/창/수치를 그립니다. 창이 올라온 정도(0~1)를 돌려줍니다(버튼을 누를 수 있는지 판단용).
    private float ApplyTimeline(GameClearUISettings s)
    {
        float t = timeline;

        // 막
        Color dim = s.dimColor;
        dim.a *= EaseOutCubic(Progress(t, 0f, s.dimFadeDuration));
        dimImage.color = dim;

        // 글자: 오른쪽에서 밀려오며 투명 -> 흰빛, 다 나타난 뒤 흰빛이 걷힘
        float appear = EaseOutCubic(Progress(t, s.titleDelay, s.titleAppearDuration));
        float restore = Progress(t, s.titleDelay + s.titleAppearDuration, s.titleFlashFadeDuration);
        titleGroup.alpha = appear;
        flashGroup.alpha = 1f - restore * restore * (3f - 2f * restore);
        titleRect.anchoredPosition += new Vector2(s.titleSlideDistance * (1f - appear), 0f);

        // 창
        float popup = EaseOutCubic(Progress(t, PopupStart(s), s.popupAppearDuration));
        panelGroup.alpha = popup;
        panelRect.anchoredPosition = new Vector2(0f, -s.popupRiseDistance * (1f - popup));

        // 수치 판: 차례로 올라오며 나타난 뒤 0부터 올라감
        int[] targets = { Mathf.Max(0, Mathf.FloorToInt(statSeconds)), statKills, statRooms };
        for (int i = 0; i < rows.Length; i++)
        {
            var row = rows[i];
            float start = RowStart(s, i);
            float b = EaseOutCubic(Progress(t, start, s.badgeAppearDuration));
            row.badgeGroup.alpha = b;
            row.badgeRect.anchoredPosition += new Vector2(0f, -s.badgeRiseDistance * (1f - b));

            float c = Progress(t, start + s.badgeAppearDuration, s.countDuration);
            c = 1f - (1f - c) * (1f - c); // 빠르게 올라가다 끝에서 느려짐
            int shown = Mathf.RoundToInt(targets[i] * c);
            string text = i == 0 ? FormatTime(shown) : shown.ToString();
            if (row.value.text != text) row.value.text = text;
        }
        return popup;
    }

    private static string FormatTime(int totalSeconds)
    {
        int h = totalSeconds / 3600;
        int m = (totalSeconds / 60) % 60;
        int sec = totalSeconds % 60;
        return $"{h:00}:{m:00}:{sec:00}";
    }

    private static bool IsPointerOver(RectTransform rect)
    {
        if (rect == null || !rect.gameObject.activeInHierarchy) return false;
        return RectTransformUtility.RectangleContainsScreenPoint(rect, Input.mousePosition, null);
    }

    // ------------------------------------------------------------------
    // UI 생성 / 배치
    // ------------------------------------------------------------------

    private void BuildUI()
    {
        canvasGO = new GameObject("GameClearCanvas");
        canvasGO.transform.SetParent(transform, false);
        canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasGO.AddComponent<GraphicRaycaster>();

        var dimGO = new GameObject("Dim");
        dimGO.transform.SetParent(canvasGO.transform, false);
        Stretch(dimGO.AddComponent<RectTransform>());
        dimImage = dimGO.AddComponent<Image>();
        dimImage.raycastTarget = true;

        // 팝업 창(화면 정중앙)
        var panelGO = new GameObject("Panel");
        panelGO.transform.SetParent(canvasGO.transform, false);
        panelRect = panelGO.AddComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelBox = UIPatternBox.AddTo(panelGO);
        panelGroup = panelGO.AddComponent<CanvasGroup>();
        panelGroup.blocksRaycasts = false;
        panelGroup.interactable = false;

        for (int i = 0; i < rows.Length; i++)
        {
            var row = new RowView();
            row.label = CreateText(panelRect, "Label" + i, TextAnchor.MiddleRight);

            var badgeGO = new GameObject("ValueBadge" + i);
            badgeGO.transform.SetParent(panelRect, false);
            row.badgeRect = badgeGO.AddComponent<RectTransform>();
            row.badgeRect.anchorMin = row.badgeRect.anchorMax = new Vector2(0f, 1f);
            row.badgeRect.pivot = new Vector2(0.5f, 0.5f);
            row.badgeBox = UIPatternBox.AddTo(badgeGO);
            row.badgeGroup = badgeGO.AddComponent<CanvasGroup>();
            row.badgeGroup.blocksRaycasts = false;
            row.value = CreateText(row.badgeRect, "Value", TextAnchor.MiddleCenter);
            Stretch(row.value.rectTransform);
            rows[i] = row;
        }

        restartButton = CreateButton(panelRect, "RestartButton");
        quitButton = CreateButton(panelRect, "QuitButton");

        // 글자(창 위에 그려지도록 창 다음에). 그림자/무늬 글자 위에 흰빛 글자를 겹침.
        var titleGO = new GameObject("Title");
        titleGO.transform.SetParent(canvasGO.transform, false);
        titleRect = titleGO.AddComponent<RectTransform>();
        titleRect.anchorMin = titleRect.anchorMax = new Vector2(0.5f, 0.5f);
        titleRect.pivot = new Vector2(0f, 0f);
        titleGroup = titleGO.AddComponent<CanvasGroup>();
        titleGroup.blocksRaycasts = false;

        titleText = CreateText(titleRect, "Text", TextAnchor.LowerLeft);
        Stretch(titleText.rectTransform);
        titleText.OnPreRenderText += ShearText;

        var flashGO = new GameObject("Flash");
        flashGO.transform.SetParent(titleRect, false);
        Stretch(flashGO.AddComponent<RectTransform>());
        flashGroup = flashGO.AddComponent<CanvasGroup>();
        flashGroup.blocksRaycasts = false;
        flashText = CreateText(flashGO.transform, "Text", TextAnchor.LowerLeft);
        Stretch(flashText.rectTransform);
        flashText.color = Color.white;
        flashText.OnPreRenderText += ShearText;
    }

    private static TextMeshProUGUI CreateText(Transform parent, string name, TextAnchor anchor)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        var text = HUDText.AddTo(go, anchor);
        text.raycastTarget = false;
        return text;
    }

    private static ButtonView CreateButton(Transform parent, string name)
    {
        var view = new ButtonView();
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        view.rect = go.AddComponent<RectTransform>();
        view.rect.anchorMin = view.rect.anchorMax = new Vector2(0.5f, 0f);
        view.rect.pivot = new Vector2(0.5f, 0f);
        view.box = UIPatternBox.AddTo(go);
        view.label = CreateText(view.rect, "Label", TextAnchor.MiddleCenter);
        Stretch(view.label.rectTransform);
        return view;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    // 크기/위치/문구를 매 프레임 반영합니다(Play 중 에셋 값을 바꾸면 바로 보임 - 글자 색/무늬/그림자는 화면이 뜰 때 한 번 만듦).
    // 연출 이동량(밀려옴/올라옴)은 ApplyTimeline이 이 위치에 더합니다.
    private void ApplyLayout(GameClearUISettings s)
    {
        var style = PopupStyle.Get();

        canvas.sortingOrder = s.canvasSortingOrder;
        if (Screen.height > 0)
        {
            float screenAspect = (float)Screen.width / Screen.height;
            scaler.matchWidthOrHeight = screenAspect > 1920f / 1080f ? 1f : 0f; // HUD와 같은 규칙
        }

        Vector2 size = new Vector2(Mathf.Max(200f, s.panelSize.x), Mathf.Max(200f, s.panelSize.y));
        panelRect.sizeDelta = size;
        panelBox.SetupPanel(style);

        // 글자: 기준점(글자 줄의 바닥선) = 창 왼쪽 위 모서리 + Title Offset. 글자 칸 아래쪽 끝은 바닥선보다 글꼴 아래 여백만큼 아래.
        titleText.fontSize = s.titleFontSize;
        flashText.fontSize = s.titleFontSize;
        if (titleText.text != s.title) { titleText.text = s.title; flashText.text = s.title; }
        float descent = DescentPixels(titleText);
        float lineHeight = s.titleFontSize * 1.6f;
        titleRect.sizeDelta = new Vector2(Mathf.Max(size.x * 2f, s.titleFontSize * 20f), lineHeight);
        titleRect.anchoredPosition = new Vector2(-size.x * 0.5f + s.titleOffset.x, size.y * 0.5f + s.titleOffset.y - descent);
        if (!Mathf.Approximately(appliedSlant, s.titleSlantAngle))
        {
            appliedSlant = s.titleSlantAngle;
            titleText.SetVerticesDirty(); // 기울기를 새로 걸도록 글자 모양을 다시 만듦(OnPreRenderText)
            flashText.SetVerticesDirty();
        }

        // 항목 줄
        string[] labels = { s.timeLabel, s.killsLabel, s.roomsLabel };
        for (int i = 0; i < rows.Length; i++)
        {
            var row = rows[i];
            float y = -(s.firstRowY + s.rowSpacing * i);

            var labelRect = row.label.rectTransform;
            labelRect.anchorMin = labelRect.anchorMax = new Vector2(0f, 1f);
            labelRect.pivot = new Vector2(1f, 0.5f);
            labelRect.sizeDelta = new Vector2(Mathf.Max(10f, s.labelRight - 20f), s.labelFontSize + 16f);
            labelRect.anchoredPosition = new Vector2(s.labelRight, y);
            if (row.label.text != labels[i]) row.label.text = labels[i];
            row.label.fontSize = s.labelFontSize;
            row.label.color = style.panelTextColor;

            row.badgeRect.sizeDelta = s.valueBadgeSize;
            row.badgeRect.anchoredPosition = new Vector2(s.valueCenterX, y);
            row.badgeBox.SetupShape(style, s.valueBadgeColor, style.namePlateCornerRadius, style.buttonSlantAngle, false);
            row.value.fontSize = s.valueFontSize;
            row.value.color = s.valueTextColor;
        }

        LayoutButton(restartButton, s.restartLabel, UIButtonKind.Positive, new Vector2(-s.buttonHalfDistance, s.buttonBottomMargin), s, style);
        LayoutButton(quitButton, s.quitLabel, UIButtonKind.Normal, new Vector2(s.buttonHalfDistance, s.buttonBottomMargin), s, style);
    }

    private static void LayoutButton(ButtonView button, string text, UIButtonKind kind, Vector2 pos, GameClearUISettings s, PopupStyle style)
    {
        button.rect.sizeDelta = s.buttonSize;
        button.rect.anchoredPosition = pos;
        button.box.SetupButton(style, kind); // 색은 Update의 TickButton이 호버에 따라 정함
        if (button.label.text != text) button.label.text = text;
        button.label.fontSize = s.buttonFontSize;
        button.label.color = button.box.GetLabelColor(true);
    }

    // 글꼴의 바닥선 아래 여백(px, 양수)입니다.
    private static float DescentPixels(TextMeshProUGUI text)
    {
        var font = text.font;
        if (font == null || font.faceInfo.pointSize <= 0) return text.fontSize * 0.25f;
        return Mathf.Max(0f, -font.faceInfo.descentLine) * text.fontSize / font.faceInfo.pointSize;
    }

    // ------------------------------------------------------------------
    // BATTLE COMPLETE 글자 꾸미기
    // ------------------------------------------------------------------

    // 글자 모양이 만들어질 때마다(TMP가 그리기 직전) 각 글자의 꼭짓점을 바닥선 기준으로 오른쪽으로 기울입니다.
    // TMP가 매번 새 모양에서 시작하므로 여러 번 불려도 기울기가 겹쳐 쌓이지 않습니다.
    private void ShearText(TMP_TextInfo info)
    {
        if (info == null) return;
        float tan = Mathf.Tan(Mathf.Clamp(GameClearUISettings.Get().titleSlantAngle, 0f, 45f) * Mathf.Deg2Rad);
        if (Mathf.Approximately(tan, 0f)) return;

        for (int i = 0; i < info.characterCount; i++)
        {
            var ch = info.characterInfo[i];
            if (!ch.isVisible) continue;
            var verts = info.meshInfo[ch.materialReferenceIndex].vertices;
            if (verts == null) continue;
            int v0 = ch.vertexIndex;
            for (int k = 0; k < 4 && v0 + k < verts.Length; k++)
            {
                Vector3 p = verts[v0 + k];
                p.x += (p.y - ch.baseLine) * tan;
                verts[v0 + k] = p;
            }
        }
    }

    // 무늬 글자 재질(이 글자 전용 사본): 안쪽 그림(그라데이션 + 무늬) + 그림자(Underlay). 화면이 뜰 때 한 번.
    private void SetupTitle(GameClearUISettings s)
    {
        titleText.text = s.title;
        flashText.text = s.title;
        titleText.fontSize = s.titleFontSize;
        flashText.fontSize = s.titleFontSize;
        titleText.color = Color.white; // 그림 색을 그대로 보이게(글자 색은 그림에 곱해짐)
        titleText.horizontalMapping = TextureMappingOptions.Line; // 그림 가로 = 줄 전체 폭(무늬가 양 끝에만)
        titleText.verticalMapping = TextureMappingOptions.Line;   // 그림 세로 = 줄 높이(그라데이션이 글자마다가 아니라 줄 기준)

        if (titleTexture != null) Destroy(titleTexture);
        titleTexture = BuildTitleTexture(s, titleText);

        titleMaterial = titleText.fontMaterial;
        if (titleMaterial != null)
        {
            if (titleMaterial.HasProperty(ShaderUtilities.ID_FaceTex)) titleMaterial.SetTexture(ShaderUtilities.ID_FaceTex, titleTexture);
            else Debug.LogWarning("GameClearUI: 글꼴 재질 셰이더에 Face Texture 칸이 없어 글자 안 그라데이션/무늬를 넣지 못했습니다" +
                                  "(TextMeshPro/Distance Field 셰이더가 필요).");

            if (titleMaterial.HasProperty(ShaderUtilities.ID_UnderlayColor))
            {
                titleMaterial.EnableKeyword(ShaderUtilities.Keyword_Underlay);
                titleMaterial.SetColor(ShaderUtilities.ID_UnderlayColor, s.titleShadowColor);
                titleMaterial.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, s.titleShadowOffsetX);
                titleMaterial.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, s.titleShadowOffsetY);
                titleMaterial.SetFloat(ShaderUtilities.ID_UnderlayDilate, s.titleShadowDilate);
                titleMaterial.SetFloat(ShaderUtilities.ID_UnderlaySoftness, s.titleShadowSoftness);
            }
            ShaderUtilities.UpdateShaderRatios(titleMaterial);
            titleText.UpdateMeshPadding();
        }
        titleText.SetVerticesDirty();
        titleText.SetMaterialDirty();
        flashText.SetVerticesDirty();
    }

    // 글자 안에 입힐 그림: 세로 = 위->아래 그라데이션(글자 머리선~바닥선 사이), 가로 = 양 끝에서 가운데로 옅어지는 삼각 무늬.
    // 그림의 가로세로 비율을 실제 글자 줄과 맞춰 삼각형이 찌그러지지 않게 합니다.
    private static Texture2D BuildTitleTexture(GameClearUISettings s, TextMeshProUGUI text)
    {
        // 줄 높이(글꼴 위 여백 ~ 아래 여백) 안에서 바닥선/글자 머리선의 위치(0~1)
        float vBase = 0.25f, vCap = 0.95f;
        float lineUnits = 1f;
        var font = text.font;
        if (font != null)
        {
            var face = font.faceInfo;
            float asc = face.ascentLine, desc = face.descentLine;
            float span = asc - desc;
            if (span > 0.0001f)
            {
                float cap = face.capLine > 0f ? face.capLine : asc * 0.75f;
                vBase = Mathf.Clamp01(-desc / span);
                vCap = Mathf.Clamp01((cap - desc) / span);
                lineUnits = span;
            }
        }
        if (vCap <= vBase + 0.05f) vCap = Mathf.Min(1f, vBase + 0.6f);

        // 가로세로 비율 = 줄 폭 / 줄 높이
        float pointSize = font != null && font.faceInfo.pointSize > 0 ? font.faceInfo.pointSize : 90f;
        float lineHeightPx = lineUnits * text.fontSize / pointSize;
        float lineWidthPx = text.GetPreferredValues(text.text).x;
        float aspect = lineHeightPx > 1f ? Mathf.Clamp(lineWidthPx / lineHeightPx, 1f, 24f) : 10f;

        const int texH = 128;
        int texW = Mathf.Clamp(Mathf.RoundToInt(texH * aspect), 64, 2048);
        var tex = new Texture2D(texW, texH, TextureFormat.RGBA32, false)
        {
            name = "BattleCompleteFace (Runtime)",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        float glyphH = (vCap - vBase) * texH;
        float side = Mathf.Max(4f, s.titlePatternSize * glyphH);
        float triH = side * 0.8660254f;
        float spread = Mathf.Max(0.01f, s.titlePatternSpread);
        Color patternColor = s.titlePatternColor;

        var pixels = new Color32[texW * texH];
        for (int y = 0; y < texH; y++)
        {
            float v = (y + 0.5f) / texH;
            float g = Mathf.Clamp01((vCap - v) / Mathf.Max(0.0001f, vCap - vBase)); // 0 = 글자 머리선, 1 = 바닥선
            Color baseColor = GradientAt(s, g);

            int r = Mathf.FloorToInt(y / triH);
            float localY = y - r * triH;               // 줄 안에서의 높이(아래 = 0)
            float shift = (r & 1) == 0 ? 0f : side * 0.5f;

            for (int x = 0; x < texW; x++)
            {
                float u = (x + 0.5f) / texW;
                float edge = Mathf.Min(u, 1f - u);
                float fade = Mathf.Clamp01(1f - edge / spread);
                Color c = baseColor;
                if (fade > 0f && s.titlePatternAlpha > 0f)
                {
                    float xs = x + shift;
                    int col = Mathf.FloorToInt(xs / side);
                    float lx = xs - col * side;
                    bool up = localY <= triH * (1f - Mathf.Abs(lx - side * 0.5f) / (side * 0.5f));
                    int id = up ? col * 2 : (lx < side * 0.5f ? col * 2 - 1 : col * 2 + 1);
                    float k = up ? 1f : 0.4f;
                    if (((r * 5 + id * 3) % 4 + 4) % 4 == 0) k *= 0.5f;
                    c = Color.Lerp(baseColor, patternColor, fade * k * s.titlePatternAlpha);
                }
                c.a = 1f;
                pixels[y * texW + x] = c;
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply(false, true);
        return tex;
    }

    // g = 0(글자 머리선) ~ 1(바닥선)의 색: 위 밝은 띠 -> 위 노랑 -> 가운데 노랑 -> 아래 노랑 -> 아래 밝은 띠
    private static Color GradientAt(GameClearUISettings s, float g)
    {
        float top = Mathf.Clamp(s.titleTopRimSize, 0f, 0.45f);
        float bottom = 1f - Mathf.Clamp(s.titleBottomRimSize, 0f, 0.45f);
        if (g <= top) return Color.Lerp(s.titleTopRimColor, s.titleTopColor, top > 0f ? g / top : 1f);
        if (g <= 0.5f) return Color.Lerp(s.titleTopColor, s.titleMiddleColor, (g - top) / Mathf.Max(0.0001f, 0.5f - top));
        if (g <= bottom) return Color.Lerp(s.titleMiddleColor, s.titleBottomColor, (g - 0.5f) / Mathf.Max(0.0001f, bottom - 0.5f));
        return Color.Lerp(s.titleBottomColor, s.titleBottomRimColor, (g - bottom) / Mathf.Max(0.0001f, 1f - bottom));
    }
}
