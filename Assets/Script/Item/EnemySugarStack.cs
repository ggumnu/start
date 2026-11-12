using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// [113번 "슈가 중첩" - 적마다 붙는 중첩 수]
// 플레이어 총알이 처음 명중할 때 GetOrAdd()로 자동으로 붙습니다(프리팹에 미리 붙일 필요 없음 - EnemyDotState와 같은 패턴).
// - 중첩은 적이 죽을 때까지 무한히 쌓입니다. 적이 죽거나, 방 초기화로 적이 새로 만들어지면 자연히 사라집니다(이 컴포넌트도 같이 사라짐).
// - 113번을 잃으면 ClearAll()로 모든 적의 중첩을 0으로 만듭니다(다시 얻으면 0부터).
// - 화면 표시는 아래 SugarStackDisplay가 맡습니다(중첩이 1 이상인 살아 있는 적의 몸통 오른쪽 아래에 113번 아이콘 + 숫자).
[DisallowMultipleComponent]
public class EnemySugarStack : MonoBehaviour
{
    private static readonly HashSet<EnemySugarStack> all = new HashSet<EnemySugarStack>();
    public static IEnumerable<EnemySugarStack> All => all;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        all.Clear();
    }

    private int stacks;
    private EnemyHealth health;

    public int Stacks => stacks;
    public EnemyHealth Health => health;

    public static EnemySugarStack GetOrAdd(GameObject enemy)
    {
        if (enemy == null) return null;
        var existing = enemy.GetComponent<EnemySugarStack>();
        return existing != null ? existing : enemy.AddComponent<EnemySugarStack>();
    }

    // 이 적의 지금 중첩 수입니다(한 번도 맞지 않았으면 0).
    public static int GetStacks(EnemyHealth enemy)
    {
        if (enemy == null) return 0;
        var s = enemy.GetComponent<EnemySugarStack>();
        return s != null ? s.stacks : 0;
    }

    // 모든 적의 중첩을 0으로 만듭니다(113번을 잃었을 때).
    public static void ClearAll()
    {
        foreach (var s in all)
        {
            if (s != null) s.stacks = 0;
        }
    }

    void Awake()
    {
        health = GetComponent<EnemyHealth>();
        all.Add(this);
    }

    void OnDestroy()
    {
        all.Remove(this);
    }

    public void AddStack(int amount = 1)
    {
        if (amount <= 0) return;
        if (health != null && health.IsDead) return;
        stacks += amount;
    }
}

// [113번 슈가 중첩 표시] 중첩이 1 이상인 살아 있는 적마다, 몸통 오른쪽 아래에 113번 아이콘과 중첩 수(검은 글자 + 흰 테두리)를 띄웁니다.
// 110번 충전 수 표시(ChargeShotIndicator)와 같은 모양/방식입니다(화면 공간 캔버스 - 벽/모델에 가려지지 않음, 흰 테두리 = 흰 글자 8개를 사방으로 깔기).
// - 113번 효과가 Configure()로 설정/아이콘을 넘기면 켜지고, Disable()로 꺼집니다(씬에 따로 배치할 필요 없음).
// - 위치: 적 몸통(EnemyHealth의 렌더러) 범위를 화면에 비춘 사각형의 오른쪽 아래 모서리 + 설정의 오프셋.
// - 다른 방의 적(꺼져 있음)/죽은 적은 표시하지 않습니다. 정렬 순서는 110번 표시와 같은 5(F 안내 10보다 아래)입니다.
public class SugarStackDisplay : MonoBehaviour
{
    [System.Serializable]
    public class Settings
    {
        [Tooltip("113번 아이콘의 화면상 크기입니다(1920x1080 기준 픽셀).")]
        [Min(1f)] public float iconSize = 40f;

        [Tooltip("아이콘 위치를 적 몸통 오른쪽 아래 모서리에서 옮기는 값입니다(1920x1080 기준 픽셀, x 오른쪽 / y 위쪽).")]
        public Vector2 iconOffset = new Vector2(4f, 4f);

        [Tooltip("숫자 글자 크기입니다.")]
        [Min(1f)] public float numberFontSize = 24f;

        [Tooltip("숫자의 흰 테두리 두께(픽셀)입니다. 흰 글자 8개를 이 거리만큼 사방으로 밀어 깔아 만듭니다.")]
        [Min(0f)] public float numberOutlinePixels = 2f;

        [Tooltip("숫자를 아이콘 오른쪽 아래 모서리에서 옮기는 값입니다(픽셀, x 오른쪽 / y 위쪽).")]
        public Vector2 numberOffset = Vector2.zero;
    }

    private const int CanvasSortingOrder = 5;
    private static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

    private static SugarStackDisplay instance;
    private static Settings activeSettings;
    private static System.Func<Sprite> iconGetter;

    private class View
    {
        public GameObject root;
        public RectTransform rootRect;
        public Image icon;
        public RectTransform iconRect;
        public RectTransform numberRect;
        public TextMeshProUGUI number;
        public readonly TextMeshProUGUI[] outlines = new TextMeshProUGUI[8];
        public readonly RectTransform[] outlineRects = new RectTransform[8];
        public float appliedOutline = -1f;
        public float appliedFont = -1f;
        public int shown = -1;
    }

    private readonly Dictionary<EnemySugarStack, View> views = new Dictionary<EnemySugarStack, View>();
    private readonly List<View> pool = new List<View>();
    private readonly List<EnemySugarStack> removeBuffer = new List<EnemySugarStack>();
    private readonly HashSet<EnemySugarStack> seen = new HashSet<EnemySugarStack>();
    // [위치 기준 - 회전 떨림 수정] 적마다 처음 표시할 때 한 번 잰 몸통 크기입니다(x = 수평 반지름, y = 적 위치에서 몸통 바닥까지 높이 차).
    private readonly Dictionary<EnemySugarStack, Vector2> bodySizes = new Dictionary<EnemySugarStack, Vector2>();

    private Canvas canvas;
    private RectTransform canvasRect;
    private CanvasScaler canvasScaler;
    private Camera mainCamera;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        activeSettings = null;
        iconGetter = null;
    }

    // 표시를 켭니다(113번 효과가 부름). getIcon은 띄울 아이콘을 돌려줍니다(113번 아이템 아이콘).
    public static void Configure(Settings settings, System.Func<Sprite> getIcon)
    {
        activeSettings = settings ?? new Settings();
        iconGetter = getIcon;
        if (instance == null)
        {
            var go = new GameObject("SugarStackDisplay");
            instance = go.AddComponent<SugarStackDisplay>();
        }
    }

    // 표시를 끕니다(113번을 잃었을 때).
    public static void Disable()
    {
        activeSettings = null;
        iconGetter = null;
        if (instance != null) instance.HideAll();
    }

    void Awake()
    {
        var canvasGO = new GameObject("SugarStackCanvas");
        canvasGO.transform.SetParent(transform, false);
        canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = CanvasSortingOrder;
        canvasScaler = canvasGO.AddComponent<CanvasScaler>();
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = ReferenceResolution;
        canvasRect = canvasGO.GetComponent<RectTransform>();
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    void LateUpdate()
    {
        UpdateCanvasScalerMatch();

        Settings settings = activeSettings;
        if (settings == null) { HideAll(); return; }

        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null) { HideAll(); return; }

        Sprite icon = null;
        try { icon = iconGetter != null ? iconGetter() : null; }
        catch (System.Exception e) { Debug.LogError($"SugarStackDisplay: 아이콘을 찾는 중 오류가 발생했습니다: {e}"); }

        seen.Clear();
        foreach (var stack in EnemySugarStack.All)
        {
            if (stack == null || stack.Stacks <= 0 || !stack.isActiveAndEnabled) continue;
            var health = stack.Health;
            if (health == null || health.IsDead) continue;
            if (!TryGetScreenAnchor(stack, health, out Vector2 localPoint)) continue;

            if (!views.TryGetValue(stack, out var view))
            {
                view = TakeView();
                views[stack] = view;
            }
            seen.Add(stack);
            ApplyView(view, settings, icon, localPoint, stack.Stacks);
        }

        // 이번 프레임에 표시하지 않은 것은 숨기고 돌려놓습니다.
        removeBuffer.Clear();
        foreach (var kvp in views)
        {
            if (!seen.Contains(kvp.Key)) removeBuffer.Add(kvp.Key);
        }
        foreach (var key in removeBuffer)
        {
            var view = views[key];
            views.Remove(key);
            bodySizes.Remove(key);
            ReturnView(view);
        }
    }

    // 적 몸통 오른쪽 아래 모서리(캔버스 좌표)입니다.
    // [회전 떨림 수정 - 형빈 확인] 예전에는 매 프레임 몸통 경계 상자의 화면 모서리를 썼는데, 경계 상자는 적이 회전할 때마다 크기가
    // 바뀌어서 회전이 잦은 적(접촉형)에서 아이콘이 심하게 떨렸습니다. 이제는 적의 위치(transform)를 기준으로 하고, 몸통 크기는 처음
    // 표시할 때 한 번만 재서 고정합니다. 오른쪽 = 적 위치에서 화면 오른쪽으로 수평 반지름만큼, 아래 = 몸통 바닥 높이에서 카메라 쪽으로
    // 수평 반지름만큼 나온 점입니다(예전 경계 상자 모서리와 비슷한 자리). 회전해도 바뀌지 않습니다.
    private bool TryGetScreenAnchor(EnemySugarStack stack, EnemyHealth health, out Vector2 localPoint)
    {
        localPoint = Vector2.zero;
        Vector3 pos = health.transform.position;

        if (!bodySizes.TryGetValue(stack, out Vector2 size))
        {
            Bounds bounds;
            Renderer body = health.TargetRenderer;
            if (body != null) bounds = body.bounds;
            else
            {
                var col = stack.GetComponent<Collider>();
                if (col == null) return false;
                bounds = col.bounds;
            }
            float radius = Mathf.Max(bounds.extents.x, bounds.extents.z);
            size = new Vector2(radius, bounds.min.y - pos.y);
            bodySizes[stack] = size;
        }

        Vector3 camRight = mainCamera.transform.right;
        camRight.y = 0f;
        camRight = camRight.sqrMagnitude > 0.0001f ? camRight.normalized : Vector3.right;
        Vector3 camForward = mainCamera.transform.forward;
        camForward.y = 0f;
        camForward = camForward.sqrMagnitude > 0.0001f ? camForward.normalized : Vector3.forward;

        Vector3 bottomCenter = new Vector3(pos.x, pos.y + size.y, pos.z);
        Vector3 rightPoint = bottomCenter + camRight * size.x;
        Vector3 nearPoint = bottomCenter - camForward * size.x;

        Vector3 rightScreen = mainCamera.WorldToScreenPoint(rightPoint);
        Vector3 nearScreen = mainCamera.WorldToScreenPoint(nearPoint);
        if (rightScreen.z < 0f || nearScreen.z < 0f) return false; // 카메라 뒤

        return RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, new Vector2(rightScreen.x, nearScreen.y), null, out localPoint);
    }

    private void ApplyView(View view, Settings settings, Sprite icon, Vector2 anchor, int count)
    {
        if (!view.root.activeSelf) view.root.SetActive(true);

        float size = settings.iconSize;
        view.rootRect.anchoredPosition = anchor + settings.iconOffset;
        view.iconRect.sizeDelta = new Vector2(size, size);
        view.icon.sprite = icon;
        view.icon.enabled = icon != null;

        view.numberRect.anchoredPosition = settings.numberOffset;
        if (!Mathf.Approximately(view.appliedFont, settings.numberFontSize))
        {
            view.appliedFont = settings.numberFontSize;
            view.number.fontSize = settings.numberFontSize;
            foreach (var o in view.outlines) o.fontSize = settings.numberFontSize;
        }

        if (!Mathf.Approximately(view.appliedOutline, settings.numberOutlinePixels))
        {
            view.appliedOutline = settings.numberOutlinePixels;
            bool show = settings.numberOutlinePixels > 0f;
            for (int k = 0; k < view.outlineRects.Length; k++)
            {
                float angle = k * Mathf.PI * 0.25f;
                view.outlineRects[k].anchoredPosition = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * settings.numberOutlinePixels;
                view.outlines[k].enabled = show;
            }
        }

        if (count != view.shown)
        {
            view.shown = count;
            string label = count.ToString();
            view.number.text = label;
            foreach (var o in view.outlines) o.text = label;
        }
    }

    private View TakeView()
    {
        if (pool.Count > 0)
        {
            var v = pool[pool.Count - 1];
            pool.RemoveAt(pool.Count - 1);
            return v;
        }
        return BuildView();
    }

    private void ReturnView(View view)
    {
        if (view == null || view.root == null) return;
        view.root.SetActive(false);
        pool.Add(view);
    }

    private void HideAll()
    {
        foreach (var kvp in views) ReturnView(kvp.Value);
        views.Clear();
    }

    // 110번 충전 수 표시와 같은 구성: 아이콘 + 아이콘 오른쪽 아래 모서리에 숫자(흰 글자 8개 + 검은 글자).
    // 아이콘의 왼쪽 위 모서리가 기준점(적 몸통 오른쪽 아래)에 오도록 놓습니다.
    private View BuildView()
    {
        var v = new View();
        v.root = new GameObject("SugarStack");
        v.root.transform.SetParent(canvasRect, false);
        v.rootRect = v.root.AddComponent<RectTransform>();
        v.rootRect.anchorMin = v.rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        v.rootRect.pivot = new Vector2(0.5f, 0.5f);
        v.rootRect.sizeDelta = Vector2.zero;

        var iconGO = new GameObject("SugarIcon");
        iconGO.transform.SetParent(v.root.transform, false);
        v.iconRect = iconGO.AddComponent<RectTransform>();
        v.iconRect.anchorMin = v.iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        v.iconRect.pivot = new Vector2(0f, 1f); // 왼쪽 위 = 기준점
        v.iconRect.anchoredPosition = Vector2.zero;
        v.icon = iconGO.AddComponent<Image>();
        v.icon.preserveAspect = true;
        v.icon.raycastTarget = false;

        var numberGO = new GameObject("SugarCount");
        numberGO.transform.SetParent(iconGO.transform, false);
        v.numberRect = numberGO.AddComponent<RectTransform>();
        v.numberRect.anchorMin = v.numberRect.anchorMax = new Vector2(1f, 0f);
        v.numberRect.pivot = new Vector2(0.5f, 0.5f);
        v.numberRect.sizeDelta = new Vector2(60f, 40f);

        for (int k = 0; k < v.outlines.Length; k++)
        {
            var outlineGO = new GameObject("Outline" + k);
            outlineGO.transform.SetParent(numberGO.transform, false);
            v.outlineRects[k] = outlineGO.AddComponent<RectTransform>();
            v.outlineRects[k].anchorMin = Vector2.zero;
            v.outlineRects[k].anchorMax = Vector2.one;
            v.outlineRects[k].sizeDelta = Vector2.zero;
            v.outlines[k] = HUDText.AddTo(outlineGO, TextAnchor.MiddleCenter);
            v.outlines[k].color = Color.white;
        }

        var faceGO = new GameObject("Face");
        faceGO.transform.SetParent(numberGO.transform, false);
        var faceRect = faceGO.AddComponent<RectTransform>();
        faceRect.anchorMin = Vector2.zero;
        faceRect.anchorMax = Vector2.one;
        faceRect.sizeDelta = Vector2.zero;
        v.number = HUDText.AddTo(faceGO, TextAnchor.MiddleCenter);
        v.number.color = Color.black;

        v.root.SetActive(false);
        return v;
    }

    private void UpdateCanvasScalerMatch()
    {
        if (canvasScaler == null || Screen.height <= 0) return;
        float screenAspect = (float)Screen.width / Screen.height;
        float referenceAspect = ReferenceResolution.x / ReferenceResolution.y;
        canvasScaler.matchWidthOrHeight = screenAspect > referenceAspect ? 1f : 0f;
    }
}
