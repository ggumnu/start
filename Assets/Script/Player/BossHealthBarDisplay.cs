using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 보스방에서 화면 위쪽 가운데에 뜨는 보스 체력바 HUD입니다(초상화 + 줄어드는 붉은 바 + 흰 테두리 + 위쪽 보스 이름, 숫자 없음).
// 켜져 있는 BossAIController(상속한 2레벨 보스 포함)를 찾아 EnemyHealth 값을 매 프레임 읽습니다 - 플레이어가 보스방에 있을 때만 보스가 켜져 있어
// 자연히 그때만 보임. 초상화/이름/이름 색은 보스 프리팹의 BossPortrait. 2레벨 보스(BossShield)는 체력바 위에 보호막 바(주황, 깎인 자리는 검은 빈칸)가
// 더해지고 이름은 그 위로 올라갑니다. 위치/크기/색은 매 프레임 다시 적용(Play 중 조절 가능). 설계 배경은 Docs/설계노트.md [보스 체력바].
// [그로기 게이지 - 3레벨 보스] 보스가 IBossGroggyGauge를 구현하면 체력바 오른쪽에 흰 테두리 + 회색 원 + 노란 도넛 게이지가 뜨고, 도넛이 12시부터
// 시계 방향으로 차오릅니다(형빈 확정). 그림 파일 없이 코드로 원/도넛 그림을 만들어 씁니다. 회색 원에는 시계처럼 12칸 눈금선, 노란 도넛에는
// 노란 버튼과 같은 삼각형 무늬(UIPatternBox 셰이더와 같은 계산 - 반짝임 없이 고정)를 그려 넣습니다.
[RequireComponent(typeof(RectTransform))]
public class BossHealthBarDisplay : MonoBehaviour
{
    [Header("참조 (비워두면 자동으로 찾습니다)")]
    public PlayerHUDManager hudManager;

    [Header("위치 (화면 위쪽 가운데 기준, Play 중에도 즉시 반영됩니다)")]
    [Tooltip("화면 가운데를 기준으로 한 좌우 위치입니다. 양수면 오른쪽, 음수면 왼쪽입니다.")]
    public float offsetX = 0f;
    [Tooltip("화면 위쪽 끝에서 이 HUD 위쪽 끝까지의 거리입니다. 값을 키우면 아래로 내려옵니다.")]
    public float topMargin = 40f;

    [Header("초상화 (목업의 파란 하트)")]
    [Tooltip("보스 프리팹에 BossPortrait가 없거나 그 칸이 비어 있을 때 대신 쓰는 초상화입니다.")]
    public Sprite defaultPortraitSprite;
    public Vector2 portraitSize = new Vector2(90f, 90f);
    [Tooltip("초상화와 체력바 사이의 가로 간격입니다.")]
    public float portraitToBarSpacing = 10f;
    [Tooltip("초상화 위치를 미세 조정합니다(X: 좌우, Y: 상하). 양수면 각각 오른쪽/위쪽입니다.")]
    public Vector2 portraitOffset = Vector2.zero;
    [Tooltip("초상화 스프라이트가 비어 있을 때 대신 보이는 옅은 자리표시 색입니다.")]
    public Color portraitPlaceholderColor = new Color(0.3f, 0.55f, 1f, 0.9f);

    [Header("체력바 (붉은 바)")]
    public Vector2 barSize = new Vector2(640f, 26f);
    [Tooltip("체력바 위치를 미세 조정합니다(X: 좌우, Y: 상하). 양수면 각각 오른쪽/위쪽입니다. " +
             "기본은 초상화와 세로 가운데가 맞는 위치입니다.")]
    public Vector2 barOffset = Vector2.zero;
    [Tooltip("붉은 바 스프라이트(체력이 꽉 찬 모습 한 장)입니다. 비워두면 단색 자리표시 바가 됩니다. " +
             "Sprite Editor에서 9-slice Border는 설정하지 마세요.")]
    public Sprite barFillSprite;
    public Color barFillPlaceholderColor = new Color(0.85f, 0.15f, 0.15f, 1f);
    [Tooltip("체력이 빠진 자리에 보이는 빈 바 스프라이트입니다. 비워두면 어두운 단색으로 보입니다.")]
    public Sprite barBackgroundSprite;
    public Color barBackgroundPlaceholderColor = new Color(0.1f, 0.1f, 0.1f, 0.85f);

    [Header("테두리 (흰 테두리 - 줄어들지 않고 항상 그대로)")]
    [Tooltip("테두리 스프라이트입니다. 바 전체를 덮는 틀 그림을 넣으세요. 비워두면 아래 두께의 " +
             "흰색 직사각형 테두리가 자리표시로 보입니다.")]
    public Sprite frameSprite;
    [Tooltip("테두리 스프라이트가 바 사방으로 얼마나 더 바깥까지 나가는지입니다(스프라이트를 " +
             "넣었을 때만 적용). 그림의 테두리가 바보다 큰 경우 이 값을 키우세요.")]
    public float framePadding = 0f;
    [Tooltip("스프라이트가 비어 있을 때 보이는 흰 자리표시 테두리의 두께입니다.")]
    public float placeholderBorderThickness = 3f;
    public Color placeholderBorderColor = Color.white;

    [Header("보호막 바 (2레벨 보스 - 체력바 위, 보호막이 있을 때만)")]
    [Tooltip("보호막 바 높이(테두리 제외).")]
    [Min(1f)] public float shieldBarHeight = 12f;
    [Tooltip("체력바 테두리 위쪽 끝과 보호막 바 테두리 아래쪽 끝 사이 간격.")]
    public float shieldBarGap = 6f;
    [Tooltip("보호막 바 그림(꽉 찬 모습 한 장, 왼쪽부터 줄어듦). 비우면 아래 색 단색.")]
    public Sprite shieldFillSprite;
    public Color shieldFillColor = new Color(1f, 0.55f, 0.1f, 1f);
    [Tooltip("보호막이 깎인 자리에 보이는 빈칸 색.")]
    public Color shieldBackgroundColor = new Color(0f, 0f, 0f, 0.9f);
    [Tooltip("켜면 보호막 바 테두리 두께를 체력바 테두리와 같게 해서 양 끝을 정확히 맞춤(테두리 스프라이트를 쓰면 Frame Padding, 아니면 Placeholder Border Thickness).")]
    public bool shieldBorderMatchesBar = true;
    [Tooltip("위를 끈 경우의 보호막 바 흰 테두리 두께(0이면 없음).")]
    [Min(0f)] public float shieldBorderThickness = 2f;
    public Color shieldBorderColor = Color.white;

    [Header("그로기 게이지 (3레벨 보스 - 체력바 오른쪽, IBossGroggyGauge를 구현한 보스만)")]
    [Tooltip("게이지 전체 지름(흰 테두리 포함, 픽셀).")]
    [Min(4f)] public float groggyGaugeDiameter = 46f;
    [Tooltip("체력바 테두리 오른쪽 끝과 게이지 왼쪽 끝 사이 간격.")]
    public float groggyGaugeSpacing = 10f;
    [Tooltip("게이지 위치 미세 조정(X: 좌우, Y: 상하). 기본은 체력바와 세로 가운데가 맞는 위치.")]
    public Vector2 groggyGaugeOffset = Vector2.zero;
    [Tooltip("흰 테두리 두께(픽셀).")]
    [Min(0f)] public float groggyGaugeBorder = 3f;
    [Tooltip("노란 도넛 바깥 지름 = 회색 원 지름 x 이 값.")]
    [Range(0.1f, 1f)] public float groggyGaugeRingOuter = 0.82f;
    [Tooltip("노란 도넛 안쪽 구멍 지름 = 도넛 바깥 지름 x 이 값(클수록 도넛이 얇아짐).")]
    [Range(0f, 0.95f)] public float groggyGaugeRingInner = 0.45f;
    public Color groggyGaugeBorderColor = Color.white;
    [Tooltip("회색 원(도넛이 아직 안 찬 자리) 색.")]
    public Color groggyGaugeBackgroundColor = new Color(0.62f, 0.62f, 0.62f, 1f);
    [Tooltip("도넛 가운데 구멍 색.")]
    public Color groggyGaugeHoleColor = new Color(0.32f, 0.32f, 0.32f, 1f);
    public Color groggyGaugeFillColor = new Color(1f, 0.8f, 0.1f, 1f);
    [Tooltip("도넛이 목표량까지 차오르는/비워지는 속도(초당 게이지 전체 비율). 3이면 가득 차는 데 약 0.33초.")]
    [Min(0.01f)] public float groggyGaugeFillSpeed = 3f;
    [Tooltip("회색 원에 그리는 시계 눈금선 개수(12시부터 같은 간격, 구멍 가장자리에서 회색 원 가장자리까지). 0이면 없음.")]
    [Min(0)] public int groggyGaugeTickCount = 12;
    [Tooltip("눈금선 두께(픽셀).")]
    [Min(0.1f)] public float groggyGaugeTickWidth = 1.5f;
    public Color groggyGaugeTickColor = new Color(0.08f, 0.08f, 0.08f, 1f);
    [Tooltip("노란 도넛에 삼각형 무늬(노란 버튼과 같은 모양)를 넣을지.")]
    public bool groggyGaugePattern = true;
    [Tooltip("삼각형 한 변 길이(픽셀). 작을수록 촘촘.")]
    [Min(1f)] public float groggyGaugePatternSide = 7f;
    [Tooltip("무늬 최대 진하기(0이면 안 보임, 1이면 가장 밝은 삼각형이 무늬 색 그대로).")]
    [Range(0f, 1f)] public float groggyGaugePatternAlpha = 0.6f;
    [Tooltip("무늬 색(노란색 위에 이 색이 삼각형마다 다른 진하기로 섞임).")]
    public Color groggyGaugePatternColor = Color.white;
    [Tooltip("무늬 배치를 바꾸는 값 - 바꾸면 밝은 삼각형 자리가 달라집니다.")]
    public float groggyGaugePatternSeed = 3f;

    [Header("보스 이름 (체력바 위쪽 - 이름/색은 보스 프리팹의 BossPortrait에서 정합니다)")]
    [Tooltip("이름 위치를 조정합니다(X: 좌우, Y: 상하). 기준점은 체력바 위쪽 변의 가운데이며, " +
             "양수면 각각 오른쪽/위쪽입니다.")]
    public Vector2 nameOffset = new Vector2(0f, 4f);
    [Min(1)] public int nameFontSize = 28;
    [Tooltip("이름 테두리의 두께입니다(픽셀). 0이면 테두리가 없습니다. 색은 보스 프리팹에서 정합니다.")]
    [Min(0f)] public float nameOutlineThickness = 2f;

    // 켜져 있는 보스를 찾는 간격입니다. 추적할 보스가 없는 동안(대부분의 플레이 시간)
    // 매 프레임 씬을 뒤지지 않도록 이 간격으로만 다시 찾습니다.
    private const float BossSearchInterval = 0.25f;
    private float bossSearchTimer = 0f;

    private BossAIController currentBoss;
    private EnemyHealth currentHealth;

    private GameObject panelRoot;
    private RectTransform panelRect;
    private RectTransform portraitRect;
    private Image portraitImage;
    private RectTransform borderRect;   // 스프라이트가 없을 때 쓰는 흰 자리표시 테두리
    private Image borderImage;
    private RectTransform barBgRect;
    private Image barBgImage;
    private RectTransform barFillRect;
    private Image barFillImage;
    private RectTransform frameRect;    // 테두리 스프라이트가 있을 때 바를 덮는 틀
    private Image frameImage;
    private RectTransform nameRect;     // 체력바 위쪽에 표시되는 보스 이름
    private RectTransform shieldBorderRect;
    private Image shieldBorderImage;
    private RectTransform shieldBgRect;
    private Image shieldBgImage;
    private RectTransform shieldFillRect;
    private Image shieldFillImage;
    private bool shieldShown;           // 이번 프레임 보호막 바를 보이는지(레이아웃이 이름 위치에 반영)
    private RectTransform gaugeBorderRect;
    private Image gaugeBorderImage;
    private RectTransform gaugeBgRect;
    private Image gaugeBgImage;
    private RectTransform gaugeHoleRect;
    private Image gaugeHoleImage;
    private RectTransform gaugeFillRect;
    private Image gaugeFillImage;
    private bool gaugeShown;
    private float gaugeDisplayed;       // 화면에 보이는 게이지 양(목표량을 부드럽게 따라감)
    private Object gaugeOwner;          // 게이지 값을 읽던 보스(바뀌면 바로 그 값으로 맞춤)
    private Texture2D circleTexture;    // 코드로 만든 원 그림(테두리/회색 원/구멍)
    private Sprite circleSprite;
    private Texture2D ringTexture;      // 코드로 만든 노란 도넛(+ 삼각형 무늬) 그림 - 색/무늬/크기 값이 바뀌면 다시 만듦
    private Sprite ringSprite;
    private string ringTextureKey;
    private Texture2D dialTexture;      // 코드로 만든 회색 원(+ 눈금선) 그림 - 색/눈금/크기 값이 바뀌면 다시 만듦
    private Sprite dialSprite;
    private string dialTextureKey;
    private const int GaugeTextureSize = 256;
    private TextMeshProUGUI nameText;

    // Filled 방식은 스프라이트가 있어야 동작하므로, 자리표시 바용 1x1 흰색 스프라이트를 직접 만듭니다.
    private Texture2D whiteTexture;
    private Sprite whiteSprite;

    private bool built = false;

    void Update()
    {
        if (!EnsureHudManager()) return;

        UpdateCurrentBoss();

        bool show = currentBoss != null;

        if (!show)
        {
            if (built && panelRoot.activeSelf) panelRoot.SetActive(false);
            return;
        }

        EnsureBuilt();
        if (!built) return;

        if (!panelRoot.activeSelf) panelRoot.SetActive(true);

        ApplyVisuals();
        ApplyLayout();
    }

    void OnDestroy()
    {
        if (whiteSprite != null) Destroy(whiteSprite);
        if (whiteTexture != null) Destroy(whiteTexture);
        if (circleSprite != null) Destroy(circleSprite);
        if (circleTexture != null) Destroy(circleTexture);
        if (ringSprite != null) Destroy(ringSprite);
        if (ringTexture != null) Destroy(ringTexture);
        if (dialSprite != null) Destroy(dialSprite);
        if (dialTexture != null) Destroy(dialTexture);
    }

    // ------------------------------------------------------------------
    // 참조 / 보스 찾기
    // ------------------------------------------------------------------

    private bool EnsureHudManager()
    {
        if (hudManager != null && hudManager.CanvasRoot != null) return true;

        if (hudManager == null)
        {
            hudManager = Object.FindFirstObjectByType<PlayerHUDManager>();
        }

        return hudManager != null && hudManager.CanvasRoot != null;
    }

    // 지금 추적 중인 보스가 여전히 유효한지(파괴되지 않았고, 방이 꺼지지 않았고, 살아 있는지)
    // 확인하고, 아니면 일정 간격으로 켜져 있는 다른 보스를 찾습니다.
    private void UpdateCurrentBoss()
    {
        if (currentBoss != null && !IsBossShowable(currentBoss, currentHealth))
        {
            currentBoss = null;
            currentHealth = null;
        }

        if (currentBoss != null) return;

        bossSearchTimer -= Time.unscaledDeltaTime;
        if (bossSearchTimer > 0f) return;
        bossSearchTimer = BossSearchInterval;

        // 기본 검색은 "켜져 있는" 오브젝트만 찾습니다 - 플레이어가 없는 방의 보스는 방이
        // 통째로 꺼져 있어서 여기에 잡히지 않습니다.
        var bosses = Object.FindObjectsByType<BossAIController>(FindObjectsSortMode.None);
        for (int i = 0; i < bosses.Length; i++)
        {
            var health = bosses[i].GetComponent<EnemyHealth>();
            if (!IsBossShowable(bosses[i], health)) continue;

            currentBoss = bosses[i];
            currentHealth = health;
            return;
        }
    }

    private static bool IsBossShowable(BossAIController boss, EnemyHealth health)
    {
        if (boss == null || health == null) return false;
        if (!boss.gameObject.activeInHierarchy) return false;
        if (health.MaxHealth <= 0f) return false;   // 아직 Initialize 전이거나 최대 체력이 0
        if (health.CurrentHealth <= 0f) return false; // 죽음
        return true;
    }

    // ------------------------------------------------------------------
    // 화면에 반영
    // ------------------------------------------------------------------

    private void ApplyVisuals()
    {
        float ratio = Mathf.Clamp01(currentHealth.CurrentHealth / currentHealth.MaxHealth);

        // 초상화: 보스 프리팹의 BossPortrait가 우선, 없으면 Default Portrait Sprite
        var portraitComp = currentBoss.GetComponent<BossPortrait>();
        Sprite portrait = (portraitComp != null && portraitComp.portrait != null) ? portraitComp.portrait : defaultPortraitSprite;
        if (portrait != null)
        {
            portraitImage.sprite = portrait;
            portraitImage.color = Color.white;
        }
        else
        {
            portraitImage.sprite = null;
            portraitImage.color = portraitPlaceholderColor;
        }

        // 빈 바
        if (barBackgroundSprite != null)
        {
            barBgImage.sprite = barBackgroundSprite;
            barBgImage.color = Color.white;
        }
        else
        {
            barBgImage.sprite = whiteSprite;
            barBgImage.color = barBackgroundPlaceholderColor;
        }

        // 줄어드는 붉은 바 (Filled: 왼쪽 고정, 오른쪽 끝부터 사라짐)
        if (barFillSprite != null)
        {
            barFillImage.sprite = barFillSprite;
            barFillImage.color = Color.white;
        }
        else
        {
            barFillImage.sprite = whiteSprite;
            barFillImage.color = barFillPlaceholderColor;
        }
        barFillImage.fillAmount = ratio;

        // [2레벨 보스] 보호막 바 - 충전 중에는 차오르고, 있는 동안은 남은 양만큼
        var shield = currentBoss.GetComponent<BossShield>();
        shieldShown = shield != null && shield.ShowsBar;
        SetActiveIfChanged(shieldBorderRect, shieldShown && ShieldBorder > 0f);
        SetActiveIfChanged(shieldBgRect, shieldShown);
        SetActiveIfChanged(shieldFillRect, shieldShown);
        if (shieldShown)
        {
            shieldBorderImage.sprite = whiteSprite;
            shieldBorderImage.color = shieldBorderColor;
            shieldBgImage.sprite = whiteSprite;
            shieldBgImage.color = shieldBackgroundColor;
            if (shieldFillSprite != null)
            {
                shieldFillImage.sprite = shieldFillSprite;
                shieldFillImage.color = Color.white;
            }
            else
            {
                shieldFillImage.sprite = whiteSprite;
                shieldFillImage.color = shieldFillColor;
            }
            shieldFillImage.fillAmount = Mathf.Clamp01(shield.DisplayRatio);
        }

        // [3레벨 보스] 그로기 게이지 - 보스가 IBossGroggyGauge를 구현했을 때만
        ApplyGroggyGauge();

        // 이름: 보스 프리팹의 BossPortrait에 이름이 있을 때만 표시
        string bossName = portraitComp != null ? portraitComp.bossName : null;
        bool hasName = !string.IsNullOrEmpty(bossName);
        if (nameText.enabled != hasName) nameText.enabled = hasName;
        if (hasName)
        {
            nameText.text = bossName;
            nameText.fontSize = nameFontSize;
            nameText.color = portraitComp.nameColor;

            // [TMP] 외곽선은 Outline 컴포넌트 대신 TMP 자체 외곽선을 씁니다. 두께는 예전처럼 픽셀 값을 넣으면
            // HUDText.SetOutline이 TMP 비율로 바꿔 줍니다(0이면 외곽선 없음).
            HUDText.SetOutline(nameText, nameOutlineThickness, portraitComp.nameOutlineColor);
        }

        // 테두리: 스프라이트가 있으면 그 틀을, 없으면 흰 직사각형 자리표시 테두리를 사용
        bool hasFrame = frameSprite != null;
        if (frameImage.enabled != hasFrame) frameImage.enabled = hasFrame;
        if (borderImage.enabled == hasFrame) borderImage.enabled = !hasFrame;

        if (hasFrame)
        {
            frameImage.sprite = frameSprite;
            frameImage.color = Color.white;
        }
        else
        {
            borderImage.sprite = whiteSprite;
            borderImage.color = placeholderBorderColor;
        }
    }

    private void ApplyLayout()
    {
        float totalWidth = portraitSize.x + portraitToBarSpacing + barSize.x;
        float totalHeight = Mathf.Max(portraitSize.y, barSize.y);

        panelRect.anchorMin = new Vector2(0.5f, 1f);
        panelRect.anchorMax = new Vector2(0.5f, 1f);
        panelRect.pivot = new Vector2(0.5f, 1f);
        panelRect.anchoredPosition = new Vector2(offsetX, -topMargin);
        panelRect.sizeDelta = new Vector2(totalWidth, totalHeight);

        // 초상화: 왼쪽 끝, 세로 가운데
        portraitRect.sizeDelta = portraitSize;
        portraitRect.anchoredPosition = new Vector2(0f, -(totalHeight - portraitSize.y) * 0.5f) + portraitOffset;

        // 체력바: 초상화 오른쪽, 세로 가운데
        float barLeft = portraitSize.x + portraitToBarSpacing;
        Vector2 barPos = new Vector2(barLeft, -(totalHeight - barSize.y) * 0.5f) + barOffset;

        SetTopLeft(barBgRect, barSize, barPos);
        SetTopLeft(barFillRect, barSize, barPos);

        // 스프라이트가 없을 때의 자리표시 테두리: 바보다 두께만큼 사방으로 크게
        float t = placeholderBorderThickness;
        SetTopLeft(borderRect, barSize + new Vector2(2f * t, 2f * t), barPos + new Vector2(-t, t));

        // 테두리 스프라이트: 바보다 framePadding만큼 사방으로 크게
        float p = framePadding;
        SetTopLeft(frameRect, barSize + new Vector2(2f * p, 2f * p), barPos + new Vector2(-p, p));

        // [2레벨 보스] 보호막 바: 체력바 바로 위(같은 너비), 테두리는 사방으로 두께만큼 크게
        float nameBaseY = barPos.y;
        if (shieldShown)
        {
            float sh = Mathf.Max(1f, shieldBarHeight);
            float st = ShieldBorder;
            Vector2 shieldPos = new Vector2(barPos.x, barPos.y + BarBorder + shieldBarGap + st + sh);
            SetTopLeft(shieldBgRect, new Vector2(barSize.x, sh), shieldPos);
            SetTopLeft(shieldFillRect, new Vector2(barSize.x, sh), shieldPos);
            SetTopLeft(shieldBorderRect, new Vector2(barSize.x + 2f * st, sh + 2f * st), shieldPos + new Vector2(-st, st));
            nameBaseY = shieldPos.y + st;
        }

        // [3레벨 보스] 그로기 게이지: 체력바 테두리 오른쪽, 세로 가운데. 테두리 원 -> 회색 원 -> 구멍 원 -> 노란 도넛 순서로 겹침(모두 같은 중심).
        if (gaugeShown)
        {
            float d = Mathf.Max(4f, groggyGaugeDiameter);
            float inner = Mathf.Max(1f, d - 2f * Mathf.Max(0f, groggyGaugeBorder));
            float ringOuter = inner * Mathf.Clamp(groggyGaugeRingOuter, 0.1f, 1f);
            float hole = ringOuter * Mathf.Clamp(groggyGaugeRingInner, 0f, 0.95f);
            Vector2 gaugeCenter = new Vector2(barPos.x + barSize.x + BarBorder + groggyGaugeSpacing + d * 0.5f,
                                              barPos.y - barSize.y * 0.5f) + groggyGaugeOffset;
            SetCentered(gaugeBorderRect, d, gaugeCenter);
            SetCentered(gaugeBgRect, inner, gaugeCenter);
            SetCentered(gaugeHoleRect, hole, gaugeCenter);
            SetCentered(gaugeFillRect, ringOuter, gaugeCenter);
        }

        // 보스 이름: 체력바(보호막이 있으면 보호막 바) 위쪽 변 가운데에 아래쪽 가운데가 닿게(글자는 아래 정렬).
        nameRect.sizeDelta = new Vector2(barSize.x, nameFontSize * 1.5f);
        nameRect.anchoredPosition = new Vector2(barPos.x + barSize.x * 0.5f, nameBaseY) + nameOffset;
    }

    // 체력바 테두리가 바 바깥으로 나가는 두께(스프라이트 틀이면 Frame Padding, 자리표시 테두리면 그 두께).
    private float BarBorder => frameSprite != null ? framePadding : placeholderBorderThickness;
    private float ShieldBorder => shieldBorderMatchesBar ? Mathf.Max(0f, BarBorder) : shieldBorderThickness;

    private static void SetActiveIfChanged(RectTransform rt, bool active)
    {
        if (rt != null && rt.gameObject.activeSelf != active) rt.gameObject.SetActive(active);
    }

    private static void SetTopLeft(RectTransform rt, Vector2 size, Vector2 anchoredPos)
    {
        rt.sizeDelta = size;
        rt.anchoredPosition = anchoredPos;
    }

    // 지름 diameter인 원을 center(패널 왼쪽 위 기준 좌표)에 중심이 오게 둡니다(이 함수용 이미지는 위-왼쪽 기준이라 반지름만큼 옮김).
    private static void SetCentered(RectTransform rt, float diameter, Vector2 center)
    {
        rt.sizeDelta = new Vector2(diameter, diameter);
        rt.anchoredPosition = center + new Vector2(-diameter * 0.5f, diameter * 0.5f);
    }

    // 그로기 게이지 상태/색/채움. 게이지 양은 목표량을 Fill Speed로 따라가 도넛이 시계 방향으로 스르륵 차오르게(줄 때는 거꾸로) 합니다.
    private void ApplyGroggyGauge()
    {
        IBossGroggyGauge gauge = null;
        foreach (var mb in currentBoss.GetComponents<MonoBehaviour>())
        {
            if (mb is IBossGroggyGauge g) { gauge = g; break; }
        }
        gaugeShown = gauge != null && gauge.ShowsGroggyGauge;
        SetActiveIfChanged(gaugeBorderRect, gaugeShown && groggyGaugeBorder > 0f);
        SetActiveIfChanged(gaugeBgRect, gaugeShown);
        SetActiveIfChanged(gaugeHoleRect, gaugeShown);
        SetActiveIfChanged(gaugeFillRect, gaugeShown);
        if (!gaugeShown) return;

        EnsureGaugeSprites();
        gaugeBorderImage.sprite = circleSprite;
        gaugeBorderImage.color = groggyGaugeBorderColor;
        gaugeBgImage.sprite = dialSprite;      // 색/눈금이 그림에 들어 있음
        gaugeBgImage.color = Color.white;
        gaugeHoleImage.sprite = circleSprite;
        gaugeHoleImage.color = groggyGaugeHoleColor;
        gaugeFillImage.sprite = ringSprite;    // 색/무늬가 그림에 들어 있음
        gaugeFillImage.color = Color.white;

        float target = Mathf.Clamp01(gauge.GroggyGaugeRatio);
        if (gaugeOwner != currentBoss)
        {
            gaugeOwner = currentBoss; // 다른 보스로 바뀐 첫 프레임은 바로 그 값으로(이전 보스 값에서 차오르는 것처럼 보이지 않게)
            gaugeDisplayed = target;
        }
        else
        {
            gaugeDisplayed = Mathf.MoveTowards(gaugeDisplayed, target, groggyGaugeFillSpeed * Time.deltaTime);
        }
        gaugeFillImage.fillAmount = gaugeDisplayed;
    }

    // 회색 원(눈금선)과 노란 도넛(삼각형 무늬) 그림: 관련 값이 바뀌었을 때만 다시 만듭니다(Play 중 Inspector에서 바꿔도 반영).
    // 눈금 두께/삼각형 크기를 화면 픽셀로 맞추려고 지금 게이지 크기(ApplyLayout과 같은 계산)도 함께 봅니다.
    private void EnsureGaugeSprites()
    {
        float d = Mathf.Max(4f, groggyGaugeDiameter);
        float inner = Mathf.Max(1f, d - 2f * Mathf.Max(0f, groggyGaugeBorder));
        float outerRatio = Mathf.Clamp(groggyGaugeRingOuter, 0.1f, 1f);
        float innerRatio = Mathf.Clamp(groggyGaugeRingInner, 0f, 0.95f);
        float ringOuterPx = inner * outerRatio;
        // 눈금선은 구멍 가장자리부터(회색 원 기준 반지름 비율).
        float holeOnDial = outerRatio * innerRatio;

        string dialKey = $"{inner:F2}|{holeOnDial:F4}|{groggyGaugeTickCount}|{groggyGaugeTickWidth:F2}|{groggyGaugeBackgroundColor}|{groggyGaugeTickColor}";
        if (dialSprite == null || dialKey != dialTextureKey)
        {
            if (dialSprite != null) Destroy(dialSprite);
            if (dialTexture != null) Destroy(dialTexture);
            dialTexture = BuildDialTexture(inner, holeOnDial);
            dialSprite = Sprite.Create(dialTexture, new Rect(0f, 0f, GaugeTextureSize, GaugeTextureSize), new Vector2(0.5f, 0.5f), 100f);
            dialTextureKey = dialKey;
        }

        string ringKey = $"{ringOuterPx:F2}|{innerRatio:F4}|{groggyGaugeFillColor}|{groggyGaugePattern}|{groggyGaugePatternSide:F2}|" +
                         $"{groggyGaugePatternAlpha:F3}|{groggyGaugePatternColor}|{groggyGaugePatternSeed:F3}";
        if (ringSprite == null || ringKey != ringTextureKey)
        {
            if (ringSprite != null) Destroy(ringSprite);
            if (ringTexture != null) Destroy(ringTexture);
            ringTexture = BuildRingTexture(ringOuterPx, innerRatio);
            ringSprite = Sprite.Create(ringTexture, new Rect(0f, 0f, GaugeTextureSize, GaugeTextureSize), new Vector2(0.5f, 0.5f), 100f);
            ringTextureKey = ringKey;
        }
    }

    // 회색 원 + 12시부터 같은 간격의 눈금선(구멍 가장자리 ~ 원 가장자리). dialPx = 화면에서의 지름(눈금 두께를 화면 픽셀로 맞춤).
    private Texture2D BuildDialTexture(float dialPx, float holeRatio)
    {
        int size = GaugeTextureSize;
        var tex = NewGaugeTexture();
        float outer = size * 0.5f - 1f;
        float hole = outer * holeRatio;
        float c = (size - 1) * 0.5f;
        float texelsPerPixel = (outer * 2f) / Mathf.Max(dialPx, 1f);
        float halfLine = Mathf.Max(0.05f, groggyGaugeTickWidth) * 0.5f * texelsPerPixel;
        int ticks = Mathf.Max(0, groggyGaugeTickCount);
        float step = ticks > 0 ? Mathf.PI * 2f / ticks : 0f;
        Color bg = groggyGaugeBackgroundColor;
        Color line = groggyGaugeTickColor;

        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - c, dy = y - c;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float cover = Mathf.Clamp01(outer - dist + 0.5f);
                Color col = bg;
                if (ticks > 0 && dist > hole)
                {
                    // 12시(위)에서 시계 방향 각도 -> 가장 가까운 눈금까지의 수직 거리
                    float angle = Mathf.Atan2(dx, dy);
                    float nearest = Mathf.Round(angle / step) * step;
                    float perp = Mathf.Abs(Mathf.Sin(angle - nearest)) * dist;
                    float lineA = Mathf.Clamp01(halfLine - perp + 0.5f) * Mathf.Clamp01(dist - hole + 0.5f) * line.a;
                    col = Color.Lerp(bg, new Color(line.r, line.g, line.b, bg.a), lineA);
                }
                col.a *= cover;
                pixels[y * size + x] = col;
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply();
        return tex;
    }

    // 노란 도넛 + 삼각형 무늬(UIPatternBox 셰이더의 Pattern과 같은 삼각형 배치/진하기 규칙, 반짝임 없음). ringPx = 화면에서의 바깥 지름.
    private Texture2D BuildRingTexture(float ringPx, float innerRatio)
    {
        int size = GaugeTextureSize;
        var tex = NewGaugeTexture();
        float outer = size * 0.5f - 1f;
        float inner = outer * innerRatio;
        float c = (size - 1) * 0.5f;
        float pixelsPerTexel = Mathf.Max(ringPx, 1f) / (outer * 2f);
        float side = Mathf.Max(1f, groggyGaugePatternSide);
        float h = side * 0.8660254f;
        Color fill = groggyGaugeFillColor;
        Color tint = groggyGaugePatternColor;

        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - c, dy = y - c;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float cover = Mathf.Clamp01(outer - dist + 0.5f);
                if (innerRatio > 0f) cover = Mathf.Min(cover, Mathf.Clamp01(dist - inner + 0.5f));

                Color col = fill;
                if (groggyGaugePattern && cover > 0f)
                {
                    float k = TrianglePattern(x * pixelsPerTexel, y * pixelsPerTexel, side, h, pixelsPerTexel) * groggyGaugePatternAlpha;
                    col = Color.Lerp(fill, new Color(tint.r, tint.g, tint.b, fill.a), k * tint.a);
                }
                col.a = fill.a * cover;
                pixels[y * size + x] = col;
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply();
        return tex;
    }

    // 삼각형 무늬 진하기(0~1). p = 화면 픽셀 좌표. 위/아래로 번갈아 놓인 정삼각형마다 다른 진하기(가끔 아주 밝은 삼각형).
    private float TrianglePattern(float px, float py, float side, float h, float pixelsPerTexel)
    {
        float row = Mathf.Floor(py / h);
        float fy = py / h - row;
        float xs = px / side + (Mathf.Abs(row) % 2f >= 1f ? 0.5f : 0f);
        float col = Mathf.Floor(xs);
        float fx = xs - col;

        float g = (1f - Mathf.Abs(2f * fx - 1f)) - fy;
        float upIdx = 2f * col;
        float downIdx = fx < 0.5f ? 2f * col - 1f : 2f * col + 1f;
        float aa = Mathf.Max((2f / side + 1f / h) * pixelsPerTexel, 1e-5f);
        float w = Mathf.Clamp01(g / aa + 0.5f);
        return Mathf.Lerp(TriValue(row, downIdx), TriValue(row, upIdx), w);
    }

    private float TriValue(float row, float idx)
    {
        float h0 = Hash2(row, idx);
        float h1 = Hash2(row + 17f, idx + 3f);
        return h0 < 0.07f ? 0.95f : h1 * 0.62f;
    }

    private float Hash2(float a, float b)
    {
        float v = Mathf.Sin(a * 127.1f + b * 311.7f + groggyGaugePatternSeed * 74.7f) * 43758.5453f;
        return v - Mathf.Floor(v);
    }

    private static Texture2D NewGaugeTexture()
    {
        var tex = new Texture2D(GaugeTextureSize, GaugeTextureSize, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        return tex;
    }

    // 가장자리를 부드럽게 처리한 흰 원(innerRatio > 0이면 가운데가 뚫린 도넛) 그림을 만듭니다. 색은 Image.color로 입힙니다.
    private static Texture2D BuildDiscTexture(float innerRatio)
    {
        const int size = 256;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        float outer = size * 0.5f - 1f;
        float inner = outer * innerRatio;
        float c = (size - 1) * 0.5f;
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                float a = Mathf.Clamp01(outer - dist + 0.5f);
                if (innerRatio > 0f) a = Mathf.Min(a, Mathf.Clamp01(dist - inner + 0.5f));
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply();
        return tex;
    }

    // ------------------------------------------------------------------
    // 오브젝트 생성 (한 번만)
    // ------------------------------------------------------------------

    private void EnsureBuilt()
    {
        if (built) return;

        BuildWhiteSprite();
        BuildPanel(hudManager.CanvasRoot);
        built = true;
    }

    private void BuildWhiteSprite()
    {
        whiteTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        whiteTexture.SetPixel(0, 0, Color.white);
        whiteTexture.Apply();
        whiteSprite = Sprite.Create(whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 100f);

        circleTexture = BuildDiscTexture(0f);
        circleSprite = Sprite.Create(circleTexture, new Rect(0f, 0f, circleTexture.width, circleTexture.height), new Vector2(0.5f, 0.5f), 100f);
    }

    private void BuildPanel(Transform canvasRoot)
    {
        panelRoot = new GameObject("BossHealthBar");
        panelRoot.transform.SetParent(canvasRoot, false);
        panelRect = panelRoot.AddComponent<RectTransform>();

        // 그려지는 순서(아래에 있을수록 위에 보임): 자리표시 테두리 -> 빈 바 -> 붉은 바 -> 테두리 스프라이트 -> 초상화
        // (초상화는 아래에서 만든 뒤 맨 마지막으로 옮겨, 체력바와 겹쳐도 항상 가장 위에 보이게 합니다)
        portraitRect = CreateImage("Portrait", out portraitImage);
        portraitImage.preserveAspect = true;

        borderRect = CreateImage("PlaceholderBorder", out borderImage);
        barBgRect = CreateImage("BarBackground", out barBgImage);
        barFillRect = CreateImage("BarFill", out barFillImage);

        barFillImage.type = Image.Type.Filled;
        barFillImage.fillMethod = Image.FillMethod.Horizontal;
        barFillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        barFillImage.fillAmount = 1f;

        frameRect = CreateImage("BarFrame", out frameImage);
        frameImage.enabled = false;

        // [2레벨 보스] 보호막 바(테두리 -> 빈칸 -> 채움 순서로 그려짐)
        shieldBorderRect = CreateImage("ShieldBorder", out shieldBorderImage);
        shieldBgRect = CreateImage("ShieldBackground", out shieldBgImage);
        shieldFillRect = CreateImage("ShieldFill", out shieldFillImage);
        shieldFillImage.type = Image.Type.Filled;
        shieldFillImage.fillMethod = Image.FillMethod.Horizontal;
        shieldFillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        shieldBorderRect.gameObject.SetActive(false);
        shieldBgRect.gameObject.SetActive(false);
        shieldFillRect.gameObject.SetActive(false);

        // [3레벨 보스] 그로기 게이지(테두리 원 -> 회색 원 -> 구멍 원 -> 노란 도넛 순서로 그려짐). 도넛은 12시부터 시계 방향으로 차오름.
        gaugeBorderRect = CreateImage("GroggyGaugeBorder", out gaugeBorderImage);
        gaugeBgRect = CreateImage("GroggyGaugeBackground", out gaugeBgImage);
        gaugeHoleRect = CreateImage("GroggyGaugeHole", out gaugeHoleImage);
        gaugeFillRect = CreateImage("GroggyGaugeFill", out gaugeFillImage);
        gaugeFillImage.type = Image.Type.Filled;
        gaugeFillImage.fillMethod = Image.FillMethod.Radial360;
        gaugeFillImage.fillOrigin = (int)Image.Origin360.Top;
        gaugeFillImage.fillClockwise = true;
        gaugeFillImage.fillAmount = 0f;
        gaugeBorderRect.gameObject.SetActive(false);
        gaugeBgRect.gameObject.SetActive(false);
        gaugeHoleRect.gameObject.SetActive(false);
        gaugeFillRect.gameObject.SetActive(false);

        // 보스 이름 (체력바 위쪽). 초상화보다 먼저 만들어야 아래의 SetAsLastSibling으로 초상화가 계속 맨 위에 옵니다.
        var nameGo = new GameObject("BossName");
        nameGo.transform.SetParent(panelRoot.transform, false);
        nameRect = nameGo.AddComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0f, 1f);
        nameRect.anchorMax = new Vector2(0f, 1f);
        nameRect.pivot = new Vector2(0.5f, 0f);
        // [TMP] 옛 방식 Text는 화면 배율에 따라 흐릿하게 보여서 TextMeshPro로 교체했습니다(HUDText 참고).
        nameText = HUDText.AddTo(nameGo, TextAnchor.LowerCenter);
        nameText.raycastTarget = false; // HUD가 마우스 입력을 가로채지 않도록
        nameText.enabled = false;

        portraitRect.SetAsLastSibling();
    }

    private RectTransform CreateImage(string objectName, out Image image)
    {
        var go = new GameObject(objectName);
        go.transform.SetParent(panelRoot.transform, false);

        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);

        image = go.AddComponent<Image>();
        image.raycastTarget = false; // HUD가 마우스 입력을 가로채지 않도록
        return rt;
    }
}
