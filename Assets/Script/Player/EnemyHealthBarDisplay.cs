using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 일반 적의 체력바입니다. 평소에는 보이지 않다가 적이 처음 피격을 받는 순간부터 머리 위에
// 나타나고, 처치될 때까지 계속 표시됩니다. (보스는 BossHealthBarDisplay라는 전용 체력바가
// 있으므로 BossAIController가 붙은 적에게는 만들지 않습니다.)
//
// [구성]
// 흰 테두리 틀(줄지 않음) -> 검은 배경 -> 빨간 현재 체력 순으로 겹쳐 그립니다. 빨간 바만
// (현재 체력 / 최대 체력) 비율로 줄어들고, 줄어든 자리에는 검은 배경이 드러납니다.
// 별도의 스프라이트 이미지 없이 Unity 기본 흰 네모(스프라이트가 없는 UI Image)를 색만 바꿔
// 씁니다. 테두리 굵기를 바꿔도 안쪽 바의 크기는 그대로이고 바깥쪽 틀만 커집니다.
//
// [동작 방식]
// - EnemyHealth.OnAnyEnemyDamaged(정적 이벤트)를 구독해 그 적의 체력바를 처음 만듭니다.
// - 이 컴포넌트 전용 Screen Space - Overlay Canvas에 그리므로 카메라 각도, 벽, 적 모델 회전과
//   상관없이 항상 같은 크기로 또렷하게 보입니다(DamagePopupDisplay와 같은 방식).
// - 적 머리 위 위치는 Collider(없으면 Renderer) 최상단을 자동으로 재서 headroom을 더한
//   월드 좌표를 매 프레임 화면 좌표로 바꿔 따라갑니다.
// - 빨간 바 길이는 EnemyHealth의 CurrentHealth/MaxHealth를 매 프레임 읽어 계산합니다.
//
// [처치 연출 - 두 가지 방식 중 선택(Dying Style)]
// 적이 죽으면(EnemyHealth.IsDead) 체력바가 사라지는데, 방식을 둘 중 하나로 고를 수 있습니다.
// - Fade(원래 방식): 그 적의 DissolveAndVanish와 같은 시간(duration)·같은 구간 비율
//   (whitenPortion)로 체력바 전체가 흰색으로 물든 뒤 서서히 투명해지며 사라집니다.
// - Pop(뿅): popDuration 동안 체력바 전체가 popScale까지 잠깐 커졌다가, 그 순간 색 변화나
//   페이드 없이 바로 사라집니다(피해량 숫자의 '뿅' 등장과 비슷한 인상).
// DissolveAndVanish는 화면 위 UI를 다루지 못하고, 죽는 순간 그 적의 스크립트를 전부 꺼버리기
// 때문에, 체력바는 적과 별개의 오브젝트로 두고 이 스크립트가 직접 연출을 담당합니다.
// 마무리 일격이면 체력이 0으로 줄어든 상태 그대로 이 연출이 시작됩니다.
//
// [설치]
// 씬의 HUD 관련 오브젝트(DamagePopupDisplay를 붙인 곳 등)에 이 컴포넌트를 붙이면 됩니다.
// 아래 값들은 플레이 중에도 매 프레임 다시 읽으므로 인스펙터에서 바로 조절하며 확인할 수 있습니다.
public class EnemyHealthBarDisplay : MonoBehaviour
{
    [Header("크기")]
    [Tooltip("체력바 안쪽(검은 배경/빨간 체력)의 가로·세로 크기(캔버스 픽셀)입니다. " +
             "모든 일반 적이 같은 크기를 씁니다.")]
    public Vector2 barSize = new Vector2(90f, 10f);

    [Tooltip("흰 테두리 굵기(캔버스 픽셀)입니다. 안쪽 바 크기는 그대로이고 바깥 틀만 커집니다. " +
             "0이면 테두리가 없습니다.")]
    [Min(0f)] public float borderThickness = 2f;

    [Header("색")]
    [Tooltip("현재 체력(줄어드는 바)의 색입니다.")]
    public Color fillColor = new Color(0.9f, 0.1f, 0.1f, 1f);

    [Tooltip("[87번] 포섭된 아군의 현재 체력 색입니다(적은 위 Fill Color).")]
    public Color allyFillColor = new Color(0.2f, 0.85f, 0.3f, 1f);

    [Tooltip("체력이 줄어들면 뒤로 드러나는 배경 바의 색입니다.")]
    public Color backColor = new Color(0f, 0f, 0f, 1f);

    [Tooltip("줄어들지 않는 테두리 틀의 색입니다.")]
    public Color borderColor = new Color(1f, 1f, 1f, 1f);

    [Header("위치")]
    [Tooltip("적의 자동 계산된 머리 높이(Collider/Renderer 최상단)에 더할 여유 높이(월드 단위)입니다.")]
    public float headroom = 0.1f;

    [Tooltip("머리 위 기준점에서 화면 기준으로 더 띄울 거리(캔버스 픽셀)입니다. " +
             "Y가 클수록 위로 올라갑니다. X는 좌우 이동입니다.")]
    public Vector2 screenOffset = new Vector2(0f, 12f);

    public enum DyingStyle { Fade, Pop }

    [Header("처치 연출")]
    [Tooltip("체력바가 사라지는 방식입니다. Fade = 적 몸처럼 흰색으로 물들었다가 서서히 " +
             "투명해짐. Pop = 잠깐 커졌다가 즉시(페이드 없이) 사라짐.")]
    public DyingStyle dyingStyle = DyingStyle.Pop;

    [Header("처치 연출 - Pop")]
    [Tooltip("Pop 연출이 끝까지 진행되는 데 걸리는 시간(초)입니다. 이 시간이 지나면 바로 사라집니다.")]
    [Min(0.01f)] public float popDuration = 0.15f;

    [Tooltip("Pop 연출에서 커지는 최대 배율입니다(1.4 = 원래 크기의 140%까지 커짐).")]
    [Min(1f)] public float popScale = 1.4f;

    [Header("처치 연출 - Fade (적의 DissolveAndVanish를 찾지 못했을 때만 쓰는 기본값)")]
    [Tooltip("적에게 DissolveAndVanish가 없을 때 사라지는 데 걸리는 시간(초)입니다.")]
    public float fallbackDuration = 0.5f;

    [Range(0.05f, 0.95f)]
    [Tooltip("적에게 DissolveAndVanish가 없을 때, 앞 몇 %를 흰색으로 물드는 데 쓸지 정합니다.")]
    public float fallbackWhitenPortion = 0.35f;

    [Header("캔버스")]
    [Tooltip("다른 HUD 위에 그려지는 정렬 순서입니다. 피해량 숫자(8)보다 낮게 둬서 숫자가 체력바 " +
             "위에 보이게 합니다. (장전 표시 5, F 안내 10, 툴팁 20)")]
    public int canvasSortingOrder = 7;

    [Tooltip("CanvasScaler의 기준 해상도입니다. 다른 HUD와 동일하게 1920x1080을 씁니다.")]
    public Vector2 referenceResolution = new Vector2(1920f, 1080f);

    private class Bar
    {
        public GameObject go;
        public RectTransform rootRect;   // 흰 테두리 틀
        public Image frame;
        public RectTransform backRect;   // 검은 배경
        public Image back;
        public RectTransform fillRect;   // 빨간 현재 체력
        public Image fill;

        public bool active;
        public EnemyHealth enemy;        // 따라다닐 적(파괴되면 null)
        public EnemyHealth key;          // 사전 등록용 원본 참조(적이 파괴돼도 유지)
        public bool isAlly;              // [87번] 포섭된 아군이면 초록색(사라지는 중에도 유지되도록 기억)
        public float headHeight;
        public Vector3 lastEnemyPos;

        public bool dying;
        public float dyingElapsed;
        public float dyingDuration;
        public float dyingWhiten;
        public float lastRatio;          // 적이 사라진 뒤에도 유지할 마지막 체력 비율
    }

    private readonly List<Bar> bars = new List<Bar>();
    private readonly Dictionary<EnemyHealth, Bar> barByEnemy = new Dictionary<EnemyHealth, Bar>();

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
    }

    void OnDisable()
    {
        EnemyHealth.OnAnyEnemyDamaged -= HandleEnemyDamaged;
        for (int i = 0; i < bars.Count; i++) Release(bars[i]);
        barByEnemy.Clear();
    }

    private void HandleEnemyDamaged(EnemyHealth enemy, float amount)
    {
        if (enemy == null) return;

        // 보스는 전용 체력바(BossHealthBarDisplay)가 있으므로 제외합니다.
        if (enemy.GetComponent<BossAIController>() != null) return;

        // 이미 체력바가 있으면(두 번째 이후 피격) 매 프레임 비율을 읽으므로 할 일이 없습니다.
        if (barByEnemy.ContainsKey(enemy)) return;

        EnsureBuilt();

        Bar bar = AcquireBar();
        bar.active = true;
        bar.enemy = enemy;
        bar.key = enemy;
        bar.headHeight = MeasureHeadHeight(enemy);
        bar.lastEnemyPos = enemy.transform.position;
        bar.dying = false;
        bar.dyingElapsed = 0f;
        bar.lastRatio = 1f;
        bar.rootRect.localScale = Vector3.one;
        bar.go.SetActive(true);
        barByEnemy[enemy] = bar;

        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera != null) UpdateBar(bar, 0f); // 첫 프레임 깜빡임 방지
    }

    void LateUpdate()
    {
        if (!built) return;

        UpdateCanvasScalerMatch();

        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null) return;

        float dt = Time.deltaTime;

        for (int i = 0; i < bars.Count; i++)
        {
            Bar bar = bars[i];
            if (!bar.active) continue;

            // 적이 처치되면 연출을 시작합니다(마무리 일격이면 체력 0 상태 그대로 빛남).
            if (!bar.dying && bar.enemy != null && bar.enemy.IsDead) BeginDying(bar);

            // 처치가 아니라 방 정리 등으로 그냥 사라진 적이면 체력바도 즉시 없앱니다.
            if (!bar.dying && bar.enemy == null)
            {
                Release(bar);
                continue;
            }

            if (bar.dying)
            {
                bar.dyingElapsed += dt;
                if (bar.dyingElapsed >= bar.dyingDuration)
                {
                    Release(bar);
                    continue;
                }
            }

            UpdateBar(bar, dt);
        }
    }

    private void BeginDying(Bar bar)
    {
        bar.dying = true;
        bar.dyingElapsed = 0f;

        if (dyingStyle == DyingStyle.Pop)
        {
            // Pop은 적의 DissolveAndVanish와 무관하게 독립된 짧은 시간으로 동작합니다.
            bar.dyingDuration = Mathf.Max(0.01f, popDuration);
            return;
        }

        var dissolve = bar.enemy.GetComponent<DissolveAndVanish>();
        if (dissolve != null)
        {
            bar.dyingDuration = Mathf.Max(0.01f, dissolve.duration);
            bar.dyingWhiten = dissolve.whitenPortion;
        }
        else
        {
            bar.dyingDuration = Mathf.Max(0.01f, fallbackDuration);
            bar.dyingWhiten = fallbackWhitenPortion;
        }
    }

    // 위치, 크기, 체력 비율, 색(처치 연출 포함)을 한꺼번에 갱신합니다.
    private void UpdateBar(Bar bar, float dt)
    {
        // 적이 살아있는 동안만 위치/체력을 갱신하고, 사라지면 마지막 값에 고정합니다.
        if (bar.enemy != null)
        {
            bar.lastEnemyPos = bar.enemy.transform.position;
            bar.isAlly = AllyFaction.IsAlly(bar.enemy.gameObject);
            if (bar.enemy.MaxHealth > 0f)
                bar.lastRatio = Mathf.Clamp01(bar.enemy.CurrentHealth / bar.enemy.MaxHealth);

            // 다른 방 등으로 적이 꺼져 있으면 체력바도 숨깁니다.
            if (!bar.enemy.gameObject.activeInHierarchy)
            {
                if (bar.go.activeSelf) bar.go.SetActive(false);
                return;
            }
        }

        Vector3 world = bar.lastEnemyPos + Vector3.up * (bar.headHeight + headroom);
        Vector3 screen = mainCamera.WorldToScreenPoint(world);
        if (screen.z < 0f)
        {
            if (bar.go.activeSelf) bar.go.SetActive(false);
            return;
        }
        if (!bar.go.activeSelf) bar.go.SetActive(true);

        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out Vector2 local);

        float border = Mathf.Max(0f, borderThickness);
        bar.rootRect.anchoredPosition = local + screenOffset;
        bar.rootRect.sizeDelta = new Vector2(barSize.x + border * 2f, barSize.y + border * 2f);
        bar.backRect.sizeDelta = barSize;

        bar.fillRect.anchoredPosition = new Vector2(-barSize.x * 0.5f, 0f);
        bar.fillRect.sizeDelta = new Vector2(barSize.x * bar.lastRatio, barSize.y);

        // 색/크기: 평소엔 지정한 색 그대로. 처치 중이면 스타일에 따라 다르게 진행합니다.
        // - Fade: DissolveAndVanish와 같은 2단계(앞 구간 흰색으로 물듦 / 뒷 구간 흰색 상태로 투명해짐)
        // - Pop: 색은 그대로 두고 크기만 popScale까지 커졌다가, dyingDuration이 끝나면 즉시 사라짐
        Color frameC = borderColor, backC = backColor, fillC = bar.isAlly ? allyFillColor : fillColor;
        if (bar.dying)
        {
            if (dyingStyle == DyingStyle.Pop)
            {
                float pt = Mathf.Clamp01(bar.dyingElapsed / bar.dyingDuration);
                float scale = Mathf.Lerp(1f, Mathf.Max(1f, popScale), pt);
                bar.rootRect.localScale = new Vector3(scale, scale, 1f);
            }
            else
            {
                float t = Mathf.Clamp01(bar.dyingElapsed / bar.dyingDuration);
                float whiten = bar.dyingWhiten;
                float colorT = whiten > 0f ? Mathf.Clamp01(t / whiten) : 1f;
                float alphaT = whiten < 1f ? Mathf.Clamp01((t - whiten) / (1f - whiten)) : (t >= 1f ? 1f : 0f);
                frameC = Dissolved(frameC, colorT, alphaT);
                backC = Dissolved(backC, colorT, alphaT);
                fillC = Dissolved(fillC, colorT, alphaT);
            }
        }

        bar.frame.color = frameC;
        bar.back.color = backC;
        bar.fill.color = fillC;
        bar.frame.enabled = border > 0f;
    }

    private static Color Dissolved(Color original, float colorT, float alphaT)
    {
        Color c = Color.Lerp(original, Color.white, colorT);
        c.a = Mathf.Lerp(original.a, 0f, alphaT);
        return c;
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

    private Bar AcquireBar()
    {
        for (int i = 0; i < bars.Count; i++)
            if (!bars[i].active) return bars[i];
        return CreateBar();
    }

    private Bar CreateBar()
    {
        var root = new GameObject("EnemyHealthBar");
        root.transform.SetParent(canvas.transform, false);
        var rootRect = root.AddComponent<RectTransform>();
        rootRect.anchorMin = rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        var frame = root.AddComponent<Image>(); // 스프라이트 없음 = 기본 흰 네모
        frame.raycastTarget = false;

        var backGO = new GameObject("Back");
        backGO.transform.SetParent(root.transform, false);
        var backRect = backGO.AddComponent<RectTransform>();
        backRect.anchorMin = backRect.anchorMax = new Vector2(0.5f, 0.5f);
        backRect.pivot = new Vector2(0.5f, 0.5f);
        backRect.anchoredPosition = Vector2.zero;
        var back = backGO.AddComponent<Image>();
        back.raycastTarget = false;

        var fillGO = new GameObject("Fill");
        fillGO.transform.SetParent(root.transform, false);
        var fillRect = fillGO.AddComponent<RectTransform>();
        fillRect.anchorMin = fillRect.anchorMax = new Vector2(0.5f, 0.5f);
        fillRect.pivot = new Vector2(0f, 0.5f); // 왼쪽 끝 고정, 오른쪽으로 줄어듦
        var fill = fillGO.AddComponent<Image>();
        fill.raycastTarget = false;

        root.SetActive(false);

        var bar = new Bar
        {
            go = root, rootRect = rootRect, frame = frame,
            backRect = backRect, back = back, fillRect = fillRect, fill = fill
        };
        bars.Add(bar);
        return bar;
    }

    private void Release(Bar bar)
    {
        if ((object)bar.key != null) barByEnemy.Remove(bar.key);
        bar.active = false;
        bar.dying = false;
        bar.enemy = null;
        bar.key = null;
        bar.rootRect.localScale = Vector3.one;
        if (bar.go != null) bar.go.SetActive(false);
    }

    private void EnsureBuilt()
    {
        if (built) return;

        var canvasGO = new GameObject("EnemyHealthBarCanvas");
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
