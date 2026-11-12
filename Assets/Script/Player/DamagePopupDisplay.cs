using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 적이 피해를 받을 때 실제 피해량을 숫자로 띄우는 HUD입니다(EnemyHealth.OnAnyEnemyDamaged 구독 - 모든 피해가 지나는 통로).
// [포물선 연출 - 형빈 확정] 피격 즉시 적 몸통 가운데에서 "뿅" 나타나 오른쪽 위로 포물선을 그리며 튀어나가고, 꼭대기를 지나 내려가는 동안
// 서서히 사라집니다(여러 번 맞으면 숫자마다 튀는 방향이 조금씩 달라 겹치지 않음). 98번 섬멸은 숫자 대신 글자(머리 위 고정, 예전 그대로).
// [블루 아카이브식 스타일 - 형빈 확정] "현재 공격력"(PlayerStats.Damage)과 비교해 세 가지로 띄웁니다(기울인 글씨):
//   보통   = 피해 == 공격력          : 빨간 폭발 배경 + 흰 숫자 / 검붉은 테두리
//   Weak   = 피해 > 공격력, 지속 피해 : 빨간 폭발 배경 + 노란 숫자 / 어두운 주황 테두리 + 위에 주황 "Weak"
//   Resist = 피해 < 공격력, 보호막 피해(2레벨 보스, OnAnyEnemyShieldDamaged) : 폭발 없이 하늘색 숫자 / 파란 테두리 + "Resist"
// 지속 피해(EnemyHealth.IsDotDamageInProgress)와 "항상 Weak" 피해(IsForcedWeakDamageInProgress - 4번 폭발, 81번 연소, 수류탄, 해킹툴,
// 118번 포격)는 공격력과 상관없이 Weak입니다(보호막에 들어간 피해는 보호막 규칙 = Resist).
// 수류탄 등 총알이 아닌 피해도 같은 공격력 기준으로 나눕니다. 설계 배경은 Docs/설계노트.md [피해 숫자].
public class DamagePopupDisplay : MonoBehaviour
{
    [Header("글자 모양")]
    [Tooltip("숫자 테두리 두께(캔버스 픽셀)입니다. 너무 키우면(약 5 이상) 테두리가 각져 보일 수 있습니다.")]
    [Min(0f)] public float outlineThickness = 2f;

    [Tooltip("숫자 글자 크기입니다.")]
    public int fontSize = 40;

    [Tooltip("켜면 숫자와 Weak/Resist 글자를 블루 아카이브처럼 기울여 씁니다(TMP 기울임꼴).")]
    public bool italic = true;

    [Header("보통 (피해 = 현재 공격력)")]
    public Color normalTextColor = Color.white;
    public Color normalOutlineColor = new Color(0.42f, 0.05f, 0.06f, 1f);

    [Header("Weak (피해 > 현재 공격력, 지속 피해)")]
    public Color weakTextColor = new Color(1f, 0.89f, 0.24f, 1f);
    public Color weakOutlineColor = new Color(0.62f, 0.26f, 0.04f, 1f);
    public string weakLabel = "Weak";
    public Color weakLabelColor = new Color(1f, 0.6f, 0.12f, 1f);
    public Color weakLabelOutlineColor = new Color(0.55f, 0.21f, 0.03f, 1f);

    [Header("Resist (피해 < 현재 공격력, 보호막 피해)")]
    public Color resistTextColor = new Color(0.6f, 0.88f, 1f, 1f);
    public Color resistOutlineColor = new Color(0.12f, 0.32f, 0.78f, 1f);
    public string resistLabel = "Resist";
    public Color resistLabelColor = new Color(0.55f, 0.84f, 1f, 1f);
    public Color resistLabelOutlineColor = new Color(0.12f, 0.32f, 0.78f, 1f);

    [Header("Weak / Resist 글자")]
    [Tooltip("Weak/Resist 글자 크기 = 숫자 글자 크기 x 이 값.")]
    [Min(0.1f)] public float labelFontScale = 0.55f;
    [Tooltip("Weak/Resist 글자 위치: 숫자 가운데 기준(캔버스 픽셀, X = 오른쪽, Y = 위).")]
    public Vector2 labelOffset = new Vector2(-14f, 24f);
    [Tooltip("Weak/Resist 글자 테두리 두께(캔버스 픽셀)입니다.")]
    [Min(0f)] public float labelOutlineThickness = 1.5f;

    [Header("폭발 배경 (보통 / Weak)")]
    [Tooltip("숫자 뒤의 빨간 폭발 이미지입니다. 비우면 Resources/DamageBurst를 씁니다.")]
    public Sprite burstSprite;
    [Tooltip("폭발 이미지 크기 = 숫자 글자 크기 x 이 값(캔버스 픽셀).")]
    [Min(0.1f)] public float burstSizeScale = 2.6f;
    [Tooltip("폭발 이미지 위치: 숫자 가운데 기준(캔버스 픽셀).")]
    public Vector2 burstOffset = new Vector2(0f, -2f);
    [Tooltip("숫자마다 폭발 이미지를 무작위로 돌리는 최대 각도(도)입니다.")]
    [Range(0f, 180f)] public float burstRandomRotation = 25f;

    [Header("공격력 비교")]
    [Tooltip("피해와 현재 공격력의 차이가 이 값 이하이면 같은 것으로 봅니다(소수점 오차 방지).")]
    [Min(0f)] public float attackCompareTolerance = 0.01f;

    [Header("표시 시간과 연출")]
    [Tooltip("숫자가 화면에 보이는 총 시간(초)입니다. 포물선 꼭대기를 지난 뒤 남은 시간 동안 서서히 사라집니다.")]
    [Min(0.05f)] public float popupLifetime = 0.8f;

    [Tooltip("'뿅' 하고 커지며 나타나는 데 걸리는 시간(초)입니다.")]
    [Min(0.01f)] public float popDuration = 0.12f;

    [Tooltip("나타날 때 시작하는 크기 배율입니다(0.3 = 30% 크기에서 시작).")]
    [Min(0.01f)] public float popStartScale = 0.3f;

    [Tooltip("나타날 때 잠깐 커지는 최대 크기 배율입니다(1보다 크면 살짝 튀어나온 뒤 안착).")]
    [Min(0.01f)] public float popOvershoot = 1.3f;

    [Header("포물선")]
    [Tooltip("튀어나가는 처음 속도의 최솟값(캔버스 픽셀/초, X = 오른쪽, Y = 위). 숫자마다 최솟값~최댓값 사이에서 무작위로 고릅니다.")]
    public Vector2 launchVelocityMin = new Vector2(110f, 380f);
    [Tooltip("튀어나가는 처음 속도의 최댓값(캔버스 픽셀/초).")]
    public Vector2 launchVelocityMax = new Vector2(210f, 470f);
    [Tooltip("포물선을 아래로 당기는 힘(캔버스 픽셀/초²)입니다. 클수록 낮고 빠르게 꺾입니다(꼭대기 시간 = 위 속도 / 이 값).")]
    [Min(1f)] public float launchGravity = 1400f;
    [Tooltip("시작 위치: 적 몸통 가운데에서 화면 기준으로 이만큼 옮깁니다(캔버스 픽셀).")]
    public Vector2 launchStartOffset = Vector2.zero;

    [Header("98번 섬멸 글자 위치")]
    [Tooltip("섬멸 글자: 적 머리 기준점에서 화면 기준 오른쪽 위로 띄울 거리(캔버스 픽셀)입니다.")]
    public Vector2 screenOffset = new Vector2(45f, 25f);
    [Tooltip("섬멸 글자: 적의 자동 계산된 머리 높이(Collider/Renderer 최상단)에 더할 여유 높이(월드 단위)입니다.")]
    public float headroom = 0.15f;

    [Header("개수")]

    [Tooltip("동시에 화면에 띄울 수 있는 숫자 최대 개수입니다. 넘으면 가장 오래된 숫자를 재사용합니다.")]
    [Min(1)] public int maxPopups = 60;

    [Header("캔버스")]
    [Tooltip("다른 HUD 위에 그려지는 정렬 순서입니다. (장전 표시 5, F 안내 10, 툴팁 20이므로 " +
             "그보다 낮게 둬서 UI 안내를 가리지 않게 합니다.)")]
    public int canvasSortingOrder = 8;

    [Tooltip("CanvasScaler의 기준 해상도입니다. 다른 HUD와 동일하게 1920x1080을 씁니다.")]
    public Vector2 referenceResolution = new Vector2(1920f, 1080f);

    private class Popup
    {
        public GameObject go;
        public RectTransform rect;
        public TextMeshProUGUI text;
        public bool active;
        public float age;
        public EnemyHealth enemy;      // 따라다닐 적(파괴되면 null)
        public float headHeight;       // 적 pivot 기준 머리 높이(월드 단위)
        public Vector3 lastEnemyPos;   // 적이 사라진 뒤 고정할 마지막 위치
        public float bodyHeight;       // 적 pivot 기준 몸통 가운데 높이(월드 단위) - 포물선 시작점
        public Vector2 velocity;       // 포물선 처음 속도(캔버스 픽셀/초)
        public float peakTime;         // 포물선 꼭대기에 닿는 시간(초) - 이후 서서히 사라짐

        // [98번] "섬멸" 글자(숫자 대신)인지와 그 모양입니다.
        public bool isLabel;
        public Color labelTextColor;
        public Color labelOutlineColor;
        public float labelOutlinePixels;
        public int labelFontSize;
        public float labelFadeOutTime;
        public float labelFadeElapsed;   // 적이 사라진 뒤 흐른 시간(-1 = 아직 적이 남아 있음)

        public bool isShield;            // [2레벨 보스] 보호막이 받은 피해(Resist로 표시)

        public HitStyle style;
        public RectTransform burstRect;
        public Image burstImage;
        public TextMeshProUGUI labelText;
    }

    private enum HitStyle { Normal, Weak, Resist }

    private PlayerStats playerStats;

    // [98번] 적이 사라지지 않는 예외 상황에서도 "섬멸" 글자가 남지 않도록 하는 최대 표시 시간(초)입니다.
    private const float LabelMaxLifetime = 5f;

    private readonly List<Popup> popups = new List<Popup>();
    private Canvas canvas;
    private CanvasScaler canvasScaler;
    private RectTransform canvasRect;
    private Camera mainCamera;
    private bool built;

    void Awake()
    {
        EnsureBuilt();
    }

    void OnEnable()
    {
        EnemyHealth.OnAnyEnemyDamaged += HandleEnemyDamaged;
        EnemyHealth.OnAnyEnemyShieldDamaged += HandleShieldDamaged;
    }

    void OnDisable()
    {
        EnemyHealth.OnAnyEnemyDamaged -= HandleEnemyDamaged;
        EnemyHealth.OnAnyEnemyShieldDamaged -= HandleShieldDamaged;
        for (int i = 0; i < popups.Count; i++) Deactivate(popups[i]);
    }

    void LateUpdate()
    {
        if (!built) return;

        UpdateCanvasScalerMatch();

        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null) return;

        float dt = Time.deltaTime;
        for (int i = 0; i < popups.Count; i++)
        {
            Popup p = popups[i];
            if (!p.active) continue;

            p.age += dt;

            // [98번] "섬멸" 글자는 적이 완전히 사라질 때까지 유지하고, 사라진 뒤 짧게 옅어집니다.
            if (p.isLabel)
            {
                bool enemyGone = p.enemy == null || !p.enemy.gameObject.activeInHierarchy;
                if (enemyGone && p.labelFadeElapsed < 0f) p.labelFadeElapsed = 0f;
                if (p.labelFadeElapsed >= 0f) p.labelFadeElapsed += dt;

                if (p.labelFadeElapsed >= p.labelFadeOutTime || p.age >= LabelMaxLifetime)
                {
                    Deactivate(p);
                    continue;
                }

                UpdatePopup(p, dt);
                continue;
            }

            if (p.age >= popupLifetime)
            {
                Deactivate(p);
                continue;
            }

            UpdatePopup(p, dt);
        }
    }

    private void HandleEnemyDamaged(EnemyHealth enemy, float amount)
    {
        ShowPopup(enemy, amount, false, EnemyHealth.IsDotDamageInProgress || EnemyHealth.IsForcedWeakDamageInProgress);
    }

    private void HandleShieldDamaged(EnemyHealth enemy, float amount)
    {
        ShowPopup(enemy, amount, true, false);
    }

    // 피해와 현재 공격력(PlayerStats.Damage)을 비교해 스타일을 정합니다. 플레이어를 못 찾으면 보통.
    private HitStyle ResolveStyle(float amount, bool isShield, bool alwaysWeak)
    {
        if (isShield) return HitStyle.Resist;
        if (alwaysWeak) return HitStyle.Weak;
        if (playerStats == null)
        {
            var health = FindFirstObjectByType<PlayerHealth>();
            if (health != null) playerStats = health.GetComponent<PlayerStats>();
        }
        if (playerStats == null) return HitStyle.Normal;
        float attack = playerStats.Damage;
        if (amount > attack + attackCompareTolerance) return HitStyle.Weak;
        if (amount < attack - attackCompareTolerance) return HitStyle.Resist;
        return HitStyle.Normal;
    }

    private void ShowPopup(EnemyHealth enemy, float amount, bool isShield, bool alwaysWeak)
    {
        if (enemy == null || amount <= 0f) return;
        if (AllyFaction.IsAlly(enemy.gameObject)) return; // [87번] 아군이 맞을 때는 피해 숫자를 띄우지 않습니다.
        EnsureBuilt();

        int shown = Mathf.Max(1, Mathf.RoundToInt(amount));

        Popup p = AcquirePopup();
        p.active = true;
        p.age = 0f;
        p.enemy = enemy;
        MeasureHeights(enemy, out p.headHeight, out p.bodyHeight);
        p.lastEnemyPos = enemy.transform.position;
        p.velocity = new Vector2(Random.Range(launchVelocityMin.x, launchVelocityMax.x), Random.Range(launchVelocityMin.y, launchVelocityMax.y));
        p.peakTime = Mathf.Max(0f, p.velocity.y) / Mathf.Max(1f, launchGravity);
        p.isShield = isShield;
        p.style = ResolveStyle(amount, isShield, alwaysWeak);

        // [98번] 즉시 처치(섬멸)면 숫자 대신 설정의 글자를 띄웁니다.
        var execute = !isShield && enemy.WasExecuted ? enemy.ExecuteSettings : null;
        p.isLabel = execute != null;
        if (p.isLabel)
        {
            p.labelTextColor = execute.popupTextColor;
            p.labelOutlineColor = execute.popupOutlineColor;
            p.labelOutlinePixels = execute.popupOutlinePixels;
            p.labelFontSize = execute.popupFontSize;
            p.labelFadeOutTime = Mathf.Max(execute.popupFadeOutTime, 0f);
            p.labelFadeElapsed = -1f;
            p.text.text = string.IsNullOrEmpty(execute.popupText) ? shown.ToString() : execute.popupText;
        }
        else
        {
            p.text.text = shown.ToString();
        }

        // 폭발 배경(보통/Weak)과 Weak/Resist 글자. 섬멸 글자에는 붙이지 않습니다.
        bool showBurst = !p.isLabel && p.style != HitStyle.Resist && GetBurstSprite() != null;
        p.burstImage.gameObject.SetActive(showBurst);
        if (showBurst)
        {
            p.burstImage.sprite = GetBurstSprite();
            float size = fontSize * burstSizeScale;
            p.burstRect.sizeDelta = new Vector2(size, size);
            p.burstRect.anchoredPosition = burstOffset;
            p.burstRect.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-burstRandomRotation, burstRandomRotation));
        }
        bool showLabel = !p.isLabel && p.style != HitStyle.Normal;
        p.labelText.gameObject.SetActive(showLabel);
        if (showLabel)
        {
            p.labelText.text = p.style == HitStyle.Weak ? weakLabel : resistLabel;
            p.labelText.rectTransform.anchoredPosition = labelOffset;
        }
        FontStyles fs = italic ? FontStyles.Italic : FontStyles.Normal;
        p.text.fontStyle = p.isLabel ? FontStyles.Normal : fs;
        p.labelText.fontStyle = fs;
        p.go.SetActive(true);
        p.go.transform.SetAsLastSibling(); // 가장 최근 피해가 항상 맨 위에 그려짐

        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera != null) UpdatePopup(p, 0f); // 첫 프레임 깜빡임 방지
    }

    // 적 pivot(transform.position) 기준으로 Collider(없으면 Renderer) 최상단(머리)과 가운데(몸통)가 얼마나 위인지 잽니다.
    private void MeasureHeights(EnemyHealth enemy, out float head, out float body)
    {
        float baseY = enemy.transform.position.y;
        float top = float.NegativeInfinity, bottom = float.PositiveInfinity;

        var colliders = enemy.GetComponentsInChildren<Collider>();
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] == null || colliders[i].isTrigger) continue;
            top = Mathf.Max(top, colliders[i].bounds.max.y);
            bottom = Mathf.Min(bottom, colliders[i].bounds.min.y);
        }

        if (float.IsNegativeInfinity(top))
        {
            var renderers = enemy.GetComponentsInChildren<Renderer>();
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                top = Mathf.Max(top, renderers[i].bounds.max.y);
                bottom = Mathf.Min(bottom, renderers[i].bounds.min.y);
            }
        }

        if (float.IsNegativeInfinity(top)) { head = 1f; body = 0.5f; return; } // 재지 못하면 대략적인 기본 높이
        head = Mathf.Max(0f, top - baseY);
        body = Mathf.Max(0f, (top + bottom) * 0.5f - baseY);
    }

    private void UpdatePopup(Popup p, float dt)
    {
        // 적이 살아있는 동안만 위치를 갱신하고, 사라지면 마지막 위치에 고정합니다.
        if (p.enemy != null) p.lastEnemyPos = p.enemy.transform.position;

        // 섬멸 글자는 머리 위 고정, 숫자는 몸통 가운데에서 포물선 시작.
        Vector3 world = p.isLabel ? p.lastEnemyPos + Vector3.up * (p.headHeight + headroom)
                                  : p.lastEnemyPos + Vector3.up * p.bodyHeight;
        Vector3 screen = mainCamera.WorldToScreenPoint(world);

        if (screen.z < 0f)
        {
            p.go.SetActive(false);
            return;
        }
        if (!p.go.activeSelf) p.go.SetActive(true);

        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out Vector2 local);

        // 크기: 뿅(작게 -> 오버슈트 -> 1)
        float scale = 1f;
        if (p.age < popDuration)
        {
            float a = p.age / popDuration;
            if (a < 0.6f) scale = Mathf.Lerp(popStartScale, popOvershoot, a / 0.6f);
            else scale = Mathf.Lerp(popOvershoot, 1f, (a - 0.6f) / 0.4f);
        }

        // 투명도: 포물선 꼭대기까지는 그대로, 꼭대기를 지나 내려가는 동안 남은 시간에 걸쳐 서서히 사라짐.
        float alpha = 1f;
        Vector2 arc = Vector2.zero;
        if (p.isLabel)
        {
            // [98번] "섬멸" 글자는 떨어지지 않고, 적이 사라진 뒤에만 옅어집니다.
            if (p.labelFadeElapsed >= 0f)
            {
                alpha = p.labelFadeOutTime > 0f ? 1f - Mathf.Clamp01(p.labelFadeElapsed / p.labelFadeOutTime) : 0f;
            }
        }
        else
        {
            float a = p.age;
            arc = new Vector2(p.velocity.x * a, p.velocity.y * a - 0.5f * launchGravity * a * a);
            // 꼭대기가 표시 시간보다 늦으면 마지막 30% 동안 사라지게 합니다.
            float fadeStart = Mathf.Min(p.peakTime, popupLifetime * 0.7f);
            if (a > fadeStart) alpha = 1f - Mathf.Clamp01((a - fadeStart) / Mathf.Max(0.0001f, popupLifetime - fadeStart));
        }

        p.rect.anchoredPosition = p.isLabel ? local + screenOffset : local + launchStartOffset + arc;
        p.rect.localScale = new Vector3(scale, scale, 1f);

        p.text.fontSize = p.isLabel ? p.labelFontSize : fontSize;
        GetStyleColors(p.style, out Color styleText, out Color styleOutline, out Color styleLabel, out Color styleLabelOutline);
        Color c = p.isLabel ? p.labelTextColor : styleText; c.a *= alpha;
        p.text.color = c;
        // [TMP] 외곽선은 Outline 컴포넌트 대신 TMP 자체 외곽선을 씁니다(두께는 예전처럼 픽셀 값 - HUDText.SetOutline이
        // TMP 비율로 바꿈). 예전 Outline(useGraphicAlpha)처럼 외곽선도 글자와 함께 투명해집니다.
        Color oc = p.isLabel ? p.labelOutlineColor : styleOutline; oc.a *= alpha;
        HUDText.SetOutline(p.text, p.isLabel ? p.labelOutlinePixels : outlineThickness, oc);

        if (p.burstImage.gameObject.activeSelf)
        {
            p.burstImage.color = new Color(1f, 1f, 1f, alpha);
        }
        if (p.labelText.gameObject.activeSelf)
        {
            p.labelText.fontSize = fontSize * labelFontScale;
            Color lc = styleLabel; lc.a *= alpha;
            p.labelText.color = lc;
            Color loc = styleLabelOutline; loc.a *= alpha;
            HUDText.SetOutline(p.labelText, labelOutlineThickness, loc);
        }
    }

    private void GetStyleColors(HitStyle style, out Color text, out Color outline, out Color label, out Color labelOutline)
    {
        switch (style)
        {
            case HitStyle.Weak:
                text = weakTextColor; outline = weakOutlineColor; label = weakLabelColor; labelOutline = weakLabelOutlineColor;
                break;
            case HitStyle.Resist:
                text = resistTextColor; outline = resistOutlineColor; label = resistLabelColor; labelOutline = resistLabelOutlineColor;
                break;
            default:
                text = normalTextColor; outline = normalOutlineColor; label = normalTextColor; labelOutline = normalOutlineColor;
                break;
        }
    }

    private Sprite cachedBurstSprite;
    private bool burstSpriteLoaded;

    // 인스펙터 칸이 비어 있으면 Resources/DamageBurst를 한 번 읽어 씁니다(프리팹을 고치지 않아도 바로 동작하도록).
    private Sprite GetBurstSprite()
    {
        if (burstSprite != null) return burstSprite;
        if (!burstSpriteLoaded)
        {
            burstSpriteLoaded = true;
            cachedBurstSprite = Resources.Load<Sprite>("DamageBurst");
        }
        return cachedBurstSprite;
    }

    private Popup AcquirePopup()
    {
        // 1) 비활성 숫자 재사용
        for (int i = 0; i < popups.Count; i++)
            if (!popups[i].active) return popups[i];

        // 2) 아직 최대 개수 전이면 새로 만듭니다.
        if (popups.Count < maxPopups) return CreatePopup();

        // 3) 가득 찼으면 가장 오래된 숫자를 재사용합니다.
        Popup oldest = popups[0];
        for (int i = 1; i < popups.Count; i++)
            if (popups[i].age > oldest.age) oldest = popups[i];
        return oldest;
    }

    private Popup CreatePopup()
    {
        var go = new GameObject("DamagePopup");
        go.transform.SetParent(canvas.transform, false);

        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(240f, 80f);

        // 자식 순서 = 그리는 순서: 폭발 배경(맨 뒤) -> 숫자 -> Weak/Resist 글자(맨 앞).
        var burstGo = new GameObject("Burst");
        burstGo.transform.SetParent(go.transform, false);
        var burstRect = burstGo.AddComponent<RectTransform>();
        burstRect.anchorMin = burstRect.anchorMax = new Vector2(0.5f, 0.5f);
        burstRect.pivot = new Vector2(0.5f, 0.5f);
        var burstImage = burstGo.AddComponent<Image>();
        burstImage.raycastTarget = false;
        burstImage.preserveAspect = true;

        // [TMP] 옛 방식 Text는 화면 배율에 따라 흐릿하게 보여서 TextMeshPro로 교체했습니다(HUDText 참고).
        // TMP SDF 글자라 확대 연출(localScale)을 해도 흐려지지 않습니다.
        var textGo = new GameObject("Number");
        textGo.transform.SetParent(go.transform, false);
        var textRect = textGo.AddComponent<RectTransform>();
        textRect.anchorMin = textRect.anchorMax = new Vector2(0.5f, 0.5f);
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.sizeDelta = new Vector2(240f, 80f);
        var text = HUDText.AddTo(textGo, TextAnchor.MiddleCenter);

        var labelGo = new GameObject("Label");
        labelGo.transform.SetParent(go.transform, false);
        var labelRect = labelGo.AddComponent<RectTransform>();
        labelRect.anchorMin = labelRect.anchorMax = new Vector2(0.5f, 0.5f);
        labelRect.pivot = new Vector2(0.5f, 0.5f);
        labelRect.sizeDelta = new Vector2(200f, 50f);
        var label = HUDText.AddTo(labelGo, TextAnchor.MiddleCenter);

        go.SetActive(false);

        var p = new Popup { go = go, rect = rect, text = text, burstRect = burstRect, burstImage = burstImage, labelText = label };
        popups.Add(p);
        return p;
    }

    private void Deactivate(Popup p)
    {
        p.active = false;
        p.enemy = null;
        p.isLabel = false;
        if (p.go != null) p.go.SetActive(false);
    }

    private void EnsureBuilt()
    {
        if (built) return;

        var canvasGO = new GameObject("DamagePopupCanvas");
        canvasGO.transform.SetParent(transform, false);
        canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = canvasSortingOrder;

        canvasScaler = canvasGO.AddComponent<CanvasScaler>();
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = referenceResolution;

        canvasRect = canvasGO.GetComponent<RectTransform>();
        built = true;
    }

    // PlayerHUDManager / InteractPromptDisplay와 동일한 방식으로 화면 비율에 맞춰 match를 자동 전환합니다.
    private void UpdateCanvasScalerMatch()
    {
        if (canvasScaler == null) return;
        if (referenceResolution.y <= 0f || Screen.height <= 0f) return;

        float screenAspect = (float)Screen.width / Screen.height;
        float referenceAspect = referenceResolution.x / referenceResolution.y;
        canvasScaler.matchWidthOrHeight = screenAspect > referenceAspect ? 1f : 0f;
        canvas.sortingOrder = canvasSortingOrder;
    }
}
