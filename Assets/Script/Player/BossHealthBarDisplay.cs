using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 보스방에서 화면 위쪽 가운데에 뜨는 보스 체력바 HUD입니다(초상화 + 줄어드는 붉은 바 + 흰 테두리 + 위쪽 보스 이름, 숫자 없음).
// 켜져 있는 BossAIController(상속한 2레벨 보스 포함)를 찾아 EnemyHealth 값을 매 프레임 읽습니다 - 플레이어가 보스방에 있을 때만 보스가 켜져 있어
// 자연히 그때만 보임. 초상화/이름/이름 색은 보스 프리팹의 BossPortrait. 2레벨 보스(BossShield)는 체력바 위에 보호막 바(주황, 깎인 자리는 검은 빈칸)가
// 더해지고 이름은 그 위로 올라갑니다. 위치/크기/색은 매 프레임 다시 적용(Play 중 조절 가능). 설계 배경은 Docs/설계노트.md [보스 체력바].
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
