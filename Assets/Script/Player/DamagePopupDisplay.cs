using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 적이 피해를 받을 때 실제 피해량을 적 머리 오른쪽 위에 숫자로 띄우는 HUD입니다(EnemyHealth.OnAnyEnemyDamaged 구독 - 모든 피해가 지나는 통로).
// "뿅" 나타났다가 떨어지며 사라지고, 같은 적의 이전 숫자는 아래로 밀림. 98번 섬멸은 숫자 대신 글자. 2레벨 보스 보호막이 받은 피해는
// OnAnyEnemyShieldDamaged로 받아 보호막 색(기본 주황 글자 + 흰 테두리)으로 띄웁니다. 설계 배경은 Docs/설계노트.md [피해 숫자].
public class DamagePopupDisplay : MonoBehaviour
{
    [Header("글자 모양")]
    [Tooltip("숫자의 글자색입니다.")]
    public Color textColor = new Color(1f, 0.92f, 0.35f, 1f);

    [Tooltip("숫자의 테두리 색입니다.")]
    public Color outlineColor = new Color(0.1f, 0.05f, 0f, 1f);

    [Tooltip("2레벨 보스 보호막이 받은 피해 숫자의 글자색입니다.")]
    public Color shieldTextColor = new Color(1f, 0.55f, 0.1f, 1f);

    [Tooltip("2레벨 보스 보호막이 받은 피해 숫자의 테두리 색입니다.")]
    public Color shieldOutlineColor = Color.white;

    [Tooltip("테두리 두께(캔버스 픽셀)입니다. 너무 키우면(약 5 이상) 테두리가 각져 보일 수 있습니다.")]
    [Min(0f)] public float outlineThickness = 2f;

    [Tooltip("숫자 글자 크기입니다.")]
    public int fontSize = 40;

    [Header("표시 시간과 연출")]
    [Tooltip("숫자가 화면에 보이는 총 시간(초)입니다.")]
    [Min(0.05f)] public float lifetime = 0.5f;

    [Tooltip("'뿅' 하고 커지며 나타나는 데 걸리는 시간(초)입니다.")]
    [Min(0.01f)] public float popDuration = 0.12f;

    [Tooltip("나타날 때 시작하는 크기 배율입니다(0.3 = 30% 크기에서 시작).")]
    [Min(0.01f)] public float popStartScale = 0.3f;

    [Tooltip("나타날 때 잠깐 커지는 최대 크기 배율입니다(1보다 크면 살짝 튀어나온 뒤 안착).")]
    [Min(0.01f)] public float popOvershoot = 1.3f;

    [Tooltip("전체 시간 중 어느 시점(0~1)부터 아래로 내려가며 투명해지기 시작할지 정합니다.")]
    [Range(0f, 0.95f)] public float fadeStartRatio = 0.4f;

    [Tooltip("사라지는 동안 아래로 떨어지는 거리(캔버스 픽셀)입니다.")]
    public float fallDistance = 35f;

    [Header("위치")]
    [Tooltip("적 머리 기준점에서 '화면 기준' 오른쪽 위로 얼마나 띄울지(캔버스 픽셀) 정합니다. " +
             "X가 클수록 오른쪽, Y가 클수록 위입니다.")]
    public Vector2 screenOffset = new Vector2(45f, 25f);

    [Tooltip("적의 자동 계산된 머리 높이(Collider/Renderer 최상단)에 더할 여유 높이(월드 단위)입니다.")]
    public float headroom = 0.15f;

    [Header("연속 피격")]
    [Tooltip("같은 적이 보이는 시간 안에 또 맞을 때, 이전 숫자들을 아래로 밀어내는 거리(캔버스 픽셀)입니다. " +
             "0이면 밀지 않고 같은 자리에 겹쳐서(최신 숫자가 위) 표시합니다.")]
    [Min(0f)] public float stackPushDistance = 34f;

    [Tooltip("이전 숫자가 밀려 내려가는 속도(캔버스 픽셀/초)입니다. 클수록 즉시 밀립니다.")]
    [Min(1f)] public float stackPushSpeed = 900f;

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
        public float pushCurrent;      // 이전 숫자 밀어내기로 이미 내려간 거리
        public float pushTarget;       // 최종적으로 내려가야 할 거리

        // [98번] "섬멸" 글자(숫자 대신)인지와 그 모양입니다.
        public bool isLabel;
        public Color labelTextColor;
        public Color labelOutlineColor;
        public float labelOutlinePixels;
        public int labelFontSize;
        public float labelFadeOutTime;
        public float labelFadeElapsed;   // 적이 사라진 뒤 흐른 시간(-1 = 아직 적이 남아 있음)

        public bool isShield;            // [2레벨 보스] 보호막이 받은 피해(보호막 색으로 표시)
    }

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

            if (p.age >= lifetime)
            {
                Deactivate(p);
                continue;
            }

            UpdatePopup(p, dt);
        }
    }

    private void HandleEnemyDamaged(EnemyHealth enemy, float amount)
    {
        ShowPopup(enemy, amount, false);
    }

    private void HandleShieldDamaged(EnemyHealth enemy, float amount)
    {
        ShowPopup(enemy, amount, true);
    }

    private void ShowPopup(EnemyHealth enemy, float amount, bool isShield)
    {
        if (enemy == null || amount <= 0f) return;
        if (AllyFaction.IsAlly(enemy.gameObject)) return; // [87번] 아군이 맞을 때는 피해 숫자를 띄우지 않습니다.
        EnsureBuilt();

        int shown = Mathf.Max(1, Mathf.RoundToInt(amount));

        // 같은 적의 이전 숫자들을 아래로 밀어냅니다(0이면 겹침 모드).
        if (stackPushDistance > 0f)
        {
            for (int i = 0; i < popups.Count; i++)
            {
                Popup old = popups[i];
                if (old.active && old.enemy == enemy) old.pushTarget += stackPushDistance;
            }
        }

        Popup p = AcquirePopup();
        p.active = true;
        p.age = 0f;
        p.enemy = enemy;
        p.headHeight = MeasureHeadHeight(enemy);
        p.lastEnemyPos = enemy.transform.position;
        p.pushCurrent = 0f;
        p.pushTarget = 0f;
        p.isShield = isShield;

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
        p.go.SetActive(true);
        p.go.transform.SetAsLastSibling(); // 가장 최근 피해가 항상 맨 위에 그려짐

        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera != null) UpdatePopup(p, 0f); // 첫 프레임 깜빡임 방지
    }

    // 적 pivot(transform.position) 기준으로 Collider(없으면 Renderer) 최상단이 얼마나 위인지 잽니다.
    private float MeasureHeadHeight(EnemyHealth enemy)
    {
        float baseY = enemy.transform.position.y;
        float top = float.NegativeInfinity;

        var colliders = enemy.GetComponentsInChildren<Collider>();
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] == null || colliders[i].isTrigger) continue;
            top = Mathf.Max(top, colliders[i].bounds.max.y);
        }

        if (float.IsNegativeInfinity(top))
        {
            var renderers = enemy.GetComponentsInChildren<Renderer>();
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                top = Mathf.Max(top, renderers[i].bounds.max.y);
            }
        }

        if (float.IsNegativeInfinity(top)) return 1f; // 재지 못하면 대략적인 기본 높이
        return Mathf.Max(0f, top - baseY);
    }

    private void UpdatePopup(Popup p, float dt)
    {
        // 적이 살아있는 동안만 위치를 갱신하고, 사라지면 마지막 위치에 고정합니다.
        if (p.enemy != null) p.lastEnemyPos = p.enemy.transform.position;

        Vector3 world = p.lastEnemyPos + Vector3.up * (p.headHeight + headroom);
        Vector3 screen = mainCamera.WorldToScreenPoint(world);

        if (screen.z < 0f)
        {
            p.go.SetActive(false);
            return;
        }
        if (!p.go.activeSelf) p.go.SetActive(true);

        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out Vector2 local);

        // 이전 숫자 밀어내기: 목표 거리까지 일정 속도로 내려갑니다.
        p.pushCurrent = Mathf.MoveTowards(p.pushCurrent, p.pushTarget, stackPushSpeed * dt);

        float t = Mathf.Clamp01(p.age / lifetime);

        // 크기: 뿅(작게 -> 오버슈트 -> 1)
        float scale = 1f;
        if (p.age < popDuration)
        {
            float a = p.age / popDuration;
            if (a < 0.6f) scale = Mathf.Lerp(popStartScale, popOvershoot, a / 0.6f);
            else scale = Mathf.Lerp(popOvershoot, 1f, (a - 0.6f) / 0.4f);
        }

        // 투명도/낙하: fadeStartRatio 이후 ease-in(처음엔 천천히, 갈수록 빠르게)으로 진행
        float alpha = 1f;
        float fall = 0f;
        if (p.isLabel)
        {
            // [98번] "섬멸" 글자는 떨어지지 않고, 적이 사라진 뒤에만 옅어집니다.
            if (p.labelFadeElapsed >= 0f)
            {
                alpha = p.labelFadeOutTime > 0f ? 1f - Mathf.Clamp01(p.labelFadeElapsed / p.labelFadeOutTime) : 0f;
            }
        }
        else if (t > fadeStartRatio)
        {
            float f = Mathf.Clamp01((t - fadeStartRatio) / Mathf.Max(0.0001f, 1f - fadeStartRatio));
            float eased = f * f;
            alpha = 1f - f;
            fall = fallDistance * eased;
        }

        p.rect.anchoredPosition = local + screenOffset + new Vector2(0f, -(fall + p.pushCurrent));
        p.rect.localScale = new Vector3(scale, scale, 1f);

        p.text.fontSize = p.isLabel ? p.labelFontSize : fontSize;
        Color c = p.isLabel ? p.labelTextColor : (p.isShield ? shieldTextColor : textColor); c.a *= alpha;
        p.text.color = c;
        // [TMP] 외곽선은 Outline 컴포넌트 대신 TMP 자체 외곽선을 씁니다(두께는 예전처럼 픽셀 값 - HUDText.SetOutline이
        // TMP 비율로 바꿈). 예전 Outline(useGraphicAlpha)처럼 외곽선도 글자와 함께 투명해집니다.
        Color oc = p.isLabel ? p.labelOutlineColor : (p.isShield ? shieldOutlineColor : outlineColor); oc.a *= alpha;
        HUDText.SetOutline(p.text, p.isLabel ? p.labelOutlinePixels : outlineThickness, oc);
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

        // [TMP] 옛 방식 Text는 화면 배율에 따라 흐릿하게 보여서 TextMeshPro로 교체했습니다(HUDText 참고).
        // TMP SDF 글자라 확대 연출(localScale)을 해도 흐려지지 않습니다.
        var text = HUDText.AddTo(go, TextAnchor.MiddleCenter);

        go.SetActive(false);

        var p = new Popup { go = go, rect = rect, text = text };
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
