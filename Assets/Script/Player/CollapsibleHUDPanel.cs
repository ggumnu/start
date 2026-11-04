using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// HUD 패널 "접어보기" 기능의 공용 클래스입니다. 스탯 패널(PlayerStatDisplay), 장신구/일회용/음식 패널
// (PlayerSlotItemDisplay), 패시브 아이템 인벤토리(PlayerInventoryDisplay)가 각자 하나씩 가지고 씁니다
// (ItemInfoPanelView처럼 MonoBehaviour가 아닌 일반 클래스 - 각 패널 스크립트가 만들고 매 프레임 Tick을 부릅니다).
//
// [동작]
// - 패널에 붙은 버튼(반투명 검정의 둥근 사다리꼴 + 흰 화살표)을 좌클릭하면 패널이 화면 밖으로 빠지고,
//   다시 누르면 돌아옵니다. 스탯 패널은 아래로(Down), 오른쪽 두 패널은 오른쪽으로(Right) 빠집니다.
// - 버튼은 패널의 자식이라 패널과 함께 움직이고, 패널은 정확히 "패널 크기 + 화면 가장자리 여백 + 버튼 간격"만큼
//   빠지므로 다 접혔을 때 버튼이 화면 끝에 딱 붙어서 남습니다(다시 눌러 펼칠 수 있음).
// - 이동은 인스펙터의 커브(기본 EaseInOut)를 따라 시작/끝이 부드럽고, 실제 시간 기준이라 게임 오버로
//   게임이 멈춰도(Time.timeScale = 0) 동작합니다. 이동 도중 다시 누르면 그 자리에서 바로 되돌아갑니다.
// - 화살표는 "누르면 패널이 갈 방향"을 가리키고, 접히는 동안 180도 돌아 반대 방향을 가리킵니다.
// - PlayerHUDManager의 "전체 접기 키"(기본 Tab)를 누르면 ToggleAll()로 세 패널을 한꺼번에 접거나 펼칩니다
//   (하나라도 펼쳐져 있으면 전부 접고, 전부 접혀 있을 때만 전부 펼침 - 형빈 확정).
//
// [버튼 모양 - 이미지 파일 없이 실행 중에 생성]
// 둥근 사다리꼴과 화살표(삼각형)는 스탯 툴팁의 둥근 네모와 같은 방식으로 실행 중에 텍스처를 직접 그립니다
// (가장자리 안티앨리어싱 포함). 사다리꼴은 넓은 쪽이 패널에 붙는 탭 모양입니다. 직접 그린 UI 이미지로
// 바꾸고 싶으면 설정의 Button Sprite / Arrow Sprite 칸에 넣으면 됩니다(화살표 스프라이트는 "위쪽"을 가리키는
// 그림으로 넣어야 회전이 맞습니다).
//
// [클릭 판정 - EventSystem 불필요]
// 이 프로젝트의 다른 HUD(툴팁 호버 등)와 같은 방식으로, 매 프레임 마우스 화면 좌표와 버튼 사각형을 직접
// 비교합니다. 그래서 씬에 EventSystem이 없어도 동작합니다. 버튼 위에서 누른 좌클릭이 총 사격/수류탄 투척/
// 해킹툴 전개로 새지 않도록, 공격 스크립트들은 HUDClickBlocker.BlocksLeftClick(= IsPointerOverAnyButton)을
// 확인합니다 - 실행 순서와 무관하게 "지금 마우스가 버튼 위인가"만 보므로 프레임 순서 문제가 없습니다.
//
// [버그 수정 - 접힌 뒤 버튼을 눌러도 다시 안 펼쳐지던 문제]
// 패널 스크립트들은 매 프레임 패널을 원래(펼친) 위치로 되돌린 뒤 접힘 이동량을 더하는데, 처음 버전은 그 사이
// (= 패널이 잠깐 펼친 위치에 있는 순간)에 클릭 판정을 해서, 접힌 상태에서는 화면에 보이는 버튼이 아니라 "펼쳤을 때
// 버튼 자리"를 기준으로 판정하고 있었습니다(그래서 펼친 상태에서는 정상, 접힌 상태에서는 호버/클릭이 전혀 안 먹음).
// 이제 Tick이 패널 위치를 직접 정합니다 - 클릭 판정 전에 먼저 "원래 위치 + 현재 이동량"(= 화면에 보이던 위치)으로
// 옮겨놓고 판정한 뒤, 애니메이션을 진행시켜 최종 위치를 넣습니다. 패널 스크립트가 위치를 언제 되돌리든 상관없습니다.
// 또, 오른쪽 두 패널이 모두 접히면 두 버튼이 화면 끝에서 일부 겹칠 수 있어서, 한 번의 클릭은 한 프레임에 버튼 하나만
// 반응하도록 했습니다(겹친 곳을 눌러도 둘이 동시에 토글되지 않음).
public enum HUDCollapseDirection
{
    Down,   // 아래로 빠짐 - 버튼은 패널 위쪽 가운데
    Right,  // 오른쪽으로 빠짐 - 버튼은 패널 왼쪽 세로 가운데
}

// 각 패널 스크립트 인스펙터에 "접어보기" 묶음으로 보이는 설정입니다. Play 중에 바꿔도 즉시 반영됩니다.
[System.Serializable]
public class CollapsibleHUDSettings
{
    [Tooltip("체크하면 게임 시작 시 이 패널이 접힌 상태로 시작합니다.")]
    public bool startCollapsed = false;

    [Header("애니메이션")]
    [Tooltip("접히거나 펼쳐지는 데 걸리는 시간(초, 실제 시간 기준)입니다.")]
    [Min(0.01f)] public float duration = 0.3f;
    [Tooltip("이동 곡선입니다. 가로축 0~1 = 시간 진행, 세로축 0~1 = 이동 진행(0 펼침, 1 접힘). " +
             "기본 EaseInOut은 시작과 끝이 부드럽습니다.")]
    public AnimationCurve curve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("버튼 크기 / 위치")]
    [Tooltip("버튼의 긴 쪽(패널에 붙는 넓은 변) 길이입니다.")]
    public float buttonLength = 96f;
    [Tooltip("버튼의 짧은 쪽(패널에서 튀어나온 두께)입니다.")]
    public float buttonDepth = 30f;
    [Tooltip("패널 가장자리와 버튼 사이의 간격입니다.")]
    public float buttonGap = 4f;
    [Tooltip("버튼 위치 미세조정입니다(px). 스탯 패널은 x = 좌우, 오른쪽 패널들은 y = 상하로 쓰는 것을 권장합니다.")]
    public Vector2 buttonOffset = Vector2.zero;

    [Header("버튼 모양")]
    [Tooltip("사다리꼴의 좁은 쪽이 양옆에서 얼마나 들어갈지(긴 쪽 길이 대비 비율)입니다. 0이면 직사각형입니다.")]
    [Range(0f, 0.4f)] public float trapezoidInset = 0.2f;
    [Tooltip("버튼 모서리 둥글기(px)입니다.")]
    [Min(0f)] public float cornerRadius = 8f;
    [Tooltip("버튼 색입니다. 알파(A)로 투명도를 조절합니다.")]
    public Color buttonColor = new Color(0f, 0f, 0f, 0.6f);
    [Tooltip("마우스를 올렸을 때의 버튼 색입니다.")]
    public Color buttonHoverColor = new Color(0f, 0f, 0f, 0.8f);
    [Tooltip("화살표 크기(px)입니다.")]
    public float arrowSize = 16f;
    public Color arrowColor = Color.white;

    [Header("직접 그린 이미지로 교체 (비워두면 실행 중에 생성한 모양 사용)")]
    [Tooltip("버튼 배경 스프라이트입니다. 위 버튼 색이 곱해지므로 그림 색 그대로 쓰려면 버튼 색을 흰색으로 바꾸세요.")]
    public Sprite buttonSprite;
    [Tooltip("화살표 스프라이트입니다. 반드시 \"위쪽\"을 가리키는 그림으로 넣으세요(방향은 코드가 회전시킵니다).")]
    public Sprite arrowSprite;
}

public class CollapsibleHUDPanel
{
    // ------------------------------------------------------------------
    // 전체 등록소 (Tab 전체 접기 / 클릭 차단 판정용)
    // ------------------------------------------------------------------

    private static readonly List<CollapsibleHUDPanel> registry = new List<CollapsibleHUDPanel>();

    // 이번 프레임의 좌클릭을 이미 어떤 버튼이 썼는지(겹친 버튼이 동시에 토글되는 것 방지)
    private static int clickConsumedFrame = -1;

    // 에디터에서 Domain Reload를 끈 상태로 Play를 반복해도 이전 Play의 항목이 남지 않도록 비웁니다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        registry.Clear();
        clickConsumedFrame = -1;
    }

    // 하나라도 펼쳐져(또는 펼쳐지는 중) 있으면 전부 접고, 전부 접혀 있으면 전부 펼칩니다.
    public static void ToggleAll()
    {
        bool anyExpanded = false;
        foreach (var p in registry)
        {
            if (p != null && p.IsBuilt && !p.collapsed) { anyExpanded = true; break; }
        }

        foreach (var p in registry)
        {
            if (p != null && p.IsBuilt) p.collapsed = anyExpanded;
        }
    }

    // 지금 마우스가 화면에 있는 접기 버튼 중 하나 위에 있는지. HUDClickBlocker가 사용합니다.
    public static bool IsPointerOverAnyButton()
    {
        foreach (var p in registry)
        {
            if (p != null && p.IsPointerOver()) return true;
        }
        return false;
    }

    // ------------------------------------------------------------------
    // 인스턴스
    // ------------------------------------------------------------------

    private const float TextureSuperSample = 2f; // 버튼 텍스처를 화면 크기의 2배 해상도로 그려 선명하게
    private const int ArrowTexSize = 64;

    private readonly string name;
    private readonly HUDCollapseDirection direction;

    private RectTransform buttonRect;
    private Image buttonImage;
    private RectTransform arrowRect;
    private Image arrowImage;

    private bool collapsed;
    private float t; // 0 = 펼침, 1 = 접힘 (시간 진행값 - 실제 이동량은 curve로 변환)
    private bool initialized;

    // 실행 중에 생성한 텍스처/스프라이트 (모양 설정이 바뀌면 다시 만듭니다)
    private Texture2D buttonTexture;
    private Sprite buttonGeneratedSprite;
    private float cachedLength = -1f, cachedDepth = -1f, cachedInset = -1f, cachedRadius = -1f;
    private Texture2D arrowTexture;
    private Sprite arrowGeneratedSprite;

    public bool IsBuilt => buttonRect != null;
    public bool IsCollapsed => collapsed;

    public CollapsibleHUDPanel(string name, HUDCollapseDirection direction)
    {
        this.name = name;
        this.direction = direction;
        registry.Add(this);
    }

    public void SetCollapsed(bool value)
    {
        collapsed = value;
    }

    public void Toggle()
    {
        collapsed = !collapsed;
    }

    // 패널 스크립트의 OnDestroy에서 호출합니다(등록 해제 + 생성한 텍스처 정리).
    public void Dispose()
    {
        registry.Remove(this);
        DestroyGenerated(ref buttonGeneratedSprite, ref buttonTexture);
        DestroyGenerated(ref arrowGeneratedSprite, ref arrowTexture);
    }

    // 패널이 만들어진 뒤 매 프레임 호출합니다. 버튼 생성(처음 한 번)/클릭 판정/애니메이션/버튼 모양과 위치
    // 갱신을 하고, 패널 위치(panelRect.anchoredPosition = 원래 위치 + 접힘 이동량)까지 직접 정합니다.
    //   panelRect    : 버튼을 붙일 패널(버튼은 이 패널의 자식이 됨)
    //   panelSize    : 패널의 실제 크기(빠지는 거리 계산용)
    //   edgeMargin   : 화면 가장자리에서 패널까지의 여백(Down이면 아래 여백, Right면 오른쪽 여백)
    //   basePosition : 펼쳤을 때의 패널 위치(anchoredPosition)
    public void Tick(CollapsibleHUDSettings s, RectTransform panelRect, Vector2 panelSize, float edgeMargin,
                     Vector2 basePosition)
    {
        if (s == null || panelRect == null) return;

        if (buttonRect == null) Build(panelRect);
        if (!initialized)
        {
            collapsed = s.startCollapsed;
            t = collapsed ? 1f : 0f;
            initialized = true;
        }

        // 클릭 판정 전에 패널을 "지금 화면에 보이는 위치"(원래 위치 + 현재 이동량)에 둡니다 - 파일 상단 [버그 수정] 참고.
        panelRect.anchoredPosition = basePosition + ComputeOffset(s, panelSize, edgeMargin, EvaluateCurve(s.curve, t));

        // 클릭 판정 (버튼 위에서 좌클릭한 순간, 한 프레임에 버튼 하나만)
        bool hovered = IsPointerOver();
        if (hovered && Input.GetMouseButtonDown(0) && clickConsumedFrame != Time.frameCount)
        {
            clickConsumedFrame = Time.frameCount;
            Toggle();
        }

        // 애니메이션 진행 (실제 시간 기준)
        float target = collapsed ? 1f : 0f;
        float step = Time.unscaledDeltaTime / Mathf.Max(0.01f, s.duration);
        t = Mathf.MoveTowards(t, target, step);
        float eased = EvaluateCurve(s.curve, t);

        ApplyButtonVisual(s, hovered, eased);
        panelRect.anchoredPosition = basePosition + ComputeOffset(s, panelSize, edgeMargin, eased);
    }

    // 빠지는 거리 = 패널 크기 + 화면 가장자리 여백 + 버튼 간격 -> 다 접히면 버튼이 화면 끝에 붙음
    private Vector2 ComputeOffset(CollapsibleHUDSettings s, Vector2 panelSize, float edgeMargin, float eased)
    {
        if (direction == HUDCollapseDirection.Down)
        {
            float hide = Mathf.Max(0f, panelSize.y) + edgeMargin + s.buttonGap;
            return new Vector2(0f, -hide * eased);
        }
        else
        {
            float hide = Mathf.Max(0f, panelSize.x) + edgeMargin + s.buttonGap;
            return new Vector2(hide * eased, 0f);
        }
    }

    private bool IsPointerOver()
    {
        if (buttonRect == null || !buttonRect.gameObject.activeInHierarchy) return false;
        // HUD 캔버스는 Screen Space - Overlay라 카메라 없이(null) 비교합니다.
        return RectTransformUtility.RectangleContainsScreenPoint(buttonRect, Input.mousePosition, null);
    }

    // 커브가 비어 있거나 키가 없으면 직선 이동으로 대체합니다.
    private static float EvaluateCurve(AnimationCurve curve, float x)
    {
        if (curve == null || curve.length == 0) return x;
        if (x <= 0f) return 0f;
        if (x >= 1f) return 1f;
        return curve.Evaluate(x);
    }

    // ------------------------------------------------------------------
    // 버튼 생성 / 갱신
    // ------------------------------------------------------------------

    private void Build(RectTransform panelRect)
    {
        var buttonGO = new GameObject(name + "_CollapseButton");
        buttonGO.transform.SetParent(panelRect, false);
        buttonRect = buttonGO.AddComponent<RectTransform>();
        buttonImage = buttonGO.AddComponent<Image>();
        buttonImage.raycastTarget = false; // 클릭은 좌표 비교로 직접 판정 - UI 입력을 가로채지 않음

        var arrowGO = new GameObject("Arrow");
        arrowGO.transform.SetParent(buttonGO.transform, false);
        arrowRect = arrowGO.AddComponent<RectTransform>();
        arrowRect.anchorMin = arrowRect.anchorMax = new Vector2(0.5f, 0.5f);
        arrowRect.pivot = new Vector2(0.5f, 0.5f);
        arrowImage = arrowGO.AddComponent<Image>();
        arrowImage.preserveAspect = true;
        arrowImage.raycastTarget = false;

        // 다른 패널 내용(아이콘 등)보다 위에 그려지도록 맨 마지막 자식으로 둡니다.
        buttonGO.transform.SetAsLastSibling();
    }

    private void ApplyButtonVisual(CollapsibleHUDSettings s, bool hovered, float eased)
    {
        float length = Mathf.Max(4f, s.buttonLength);
        float depth = Mathf.Max(4f, s.buttonDepth);

        // 위치/크기 - 버튼은 패널 가장자리 바깥에 붙습니다.
        if (direction == HUDCollapseDirection.Down)
        {
            buttonRect.anchorMin = buttonRect.anchorMax = new Vector2(0.5f, 1f);
            buttonRect.pivot = new Vector2(0.5f, 0f);
            buttonRect.sizeDelta = new Vector2(length, depth);
            buttonRect.anchoredPosition = new Vector2(s.buttonOffset.x, s.buttonGap + s.buttonOffset.y);
        }
        else
        {
            buttonRect.anchorMin = buttonRect.anchorMax = new Vector2(0f, 0.5f);
            buttonRect.pivot = new Vector2(1f, 0.5f);
            buttonRect.sizeDelta = new Vector2(depth, length);
            buttonRect.anchoredPosition = new Vector2(-s.buttonGap + s.buttonOffset.x, s.buttonOffset.y);
        }

        // 배경
        if (s.buttonSprite != null)
        {
            buttonImage.sprite = s.buttonSprite;
        }
        else
        {
            EnsureButtonSprite(length, depth, s.trapezoidInset, s.cornerRadius);
            buttonImage.sprite = buttonGeneratedSprite;
        }
        buttonImage.type = Image.Type.Simple;
        buttonImage.color = hovered ? s.buttonHoverColor : s.buttonColor;

        // 화살표 - "누르면 갈 방향"을 가리키고 접히는 동안 180도 회전
        if (s.arrowSprite != null)
        {
            arrowImage.sprite = s.arrowSprite;
        }
        else
        {
            EnsureArrowSprite();
            arrowImage.sprite = arrowGeneratedSprite;
        }
        arrowImage.color = s.arrowColor;
        arrowRect.sizeDelta = new Vector2(s.arrowSize, s.arrowSize);
        arrowRect.anchoredPosition = Vector2.zero;

        // 화살표 그림은 위쪽(0도)을 가리킴. Down 패널: 펼침 = 아래(180도). Right 패널: 펼침 = 오른쪽(-90도).
        float expandedAngle = direction == HUDCollapseDirection.Down ? 180f : -90f;
        arrowRect.localEulerAngles = new Vector3(0f, 0f, expandedAngle + 180f * eased);
    }

    // ------------------------------------------------------------------
    // 텍스처 생성 (둥근 사다리꼴 / 삼각형 화살표)
    // ------------------------------------------------------------------

    private void EnsureButtonSprite(float length, float depth, float inset, float radius)
    {
        if (buttonGeneratedSprite != null &&
            Mathf.Approximately(cachedLength, length) && Mathf.Approximately(cachedDepth, depth) &&
            Mathf.Approximately(cachedInset, inset) && Mathf.Approximately(cachedRadius, radius))
            return;

        cachedLength = length;
        cachedDepth = depth;
        cachedInset = inset;
        cachedRadius = radius;
        DestroyGenerated(ref buttonGeneratedSprite, ref buttonTexture);

        float ss = TextureSuperSample;
        float L = length * ss;
        float D = depth * ss;
        float i = Mathf.Clamp(inset, 0f, 0.4f) * L;
        float r = radius * ss;
        const float pad = 1.5f; // 텍스처 가장자리에서 안티앨리어싱이 잘리지 않도록 여유

        int w, h;
        Vector2[] poly; // 반시계 방향
        if (direction == HUDCollapseDirection.Down)
        {
            // 넓은 변이 아래(패널 쪽), 좁은 변이 위
            w = Mathf.CeilToInt(L);
            h = Mathf.CeilToInt(D);
            poly = new[]
            {
                new Vector2(pad, pad),
                new Vector2(w - pad, pad),
                new Vector2(w - pad - i, h - pad),
                new Vector2(pad + i, h - pad),
            };
        }
        else
        {
            // 넓은 변이 오른쪽(패널 쪽), 좁은 변이 왼쪽
            w = Mathf.CeilToInt(D);
            h = Mathf.CeilToInt(L);
            poly = new[]
            {
                new Vector2(pad, pad + i),
                new Vector2(w - pad, pad),
                new Vector2(w - pad, h - pad),
                new Vector2(pad, h - pad - i),
            };
        }

        // 모서리 반지름이 버튼 두께에 비해 너무 크면 모양이 무너지므로 줄입니다.
        r = Mathf.Min(r, Mathf.Min(w, h) * 0.45f - pad);
        buttonTexture = BuildRoundedPolygonTexture(poly, w, h, Mathf.Max(0f, r));
        buttonGeneratedSprite = Sprite.Create(buttonTexture, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.5f), 100f,
                                              0, SpriteMeshType.FullRect);
    }

    private void EnsureArrowSprite()
    {
        if (arrowGeneratedSprite != null) return;

        int n = ArrowTexSize;
        // 위쪽을 가리키는 삼각형(반시계 방향)
        var poly = new[]
        {
            new Vector2(n * 0.12f, n * 0.22f),
            new Vector2(n * 0.88f, n * 0.22f),
            new Vector2(n * 0.50f, n * 0.80f),
        };
        arrowTexture = BuildRoundedPolygonTexture(poly, n, n, n * 0.06f);
        arrowGeneratedSprite = Sprite.Create(arrowTexture, new Rect(0f, 0f, n, n), new Vector2(0.5f, 0.5f), 100f,
                                             0, SpriteMeshType.FullRect);
    }

    // 볼록 다각형(반시계 방향 꼭짓점)을 모서리 반지름 radius로 둥글게 칠한 흰색 텍스처를 만듭니다.
    // 원리: 다각형을 radius만큼 안쪽으로 줄인 다각형까지의 거리 - radius = 둥근 다각형까지의 거리.
    // 거리가 0 근처인 픽셀은 반투명으로 칠해 가장자리를 부드럽게 합니다(안티앨리어싱).
    private static Texture2D BuildRoundedPolygonTexture(Vector2[] poly, int w, int h, float radius)
    {
        int n = poly.Length;

        // 각 변의 안쪽 방향 법선(반시계 방향이면 진행 방향의 왼쪽이 안쪽)
        var normals = new Vector2[n];
        for (int k = 0; k < n; k++)
        {
            Vector2 e = poly[(k + 1) % n] - poly[k];
            normals[k] = new Vector2(-e.y, e.x).normalized;
        }

        // 안쪽으로 radius만큼 민 변들의 교점 = 줄어든 다각형의 꼭짓점
        var inner = new Vector2[n];
        for (int k = 0; k < n; k++)
        {
            int prev = (k - 1 + n) % n;
            Vector2 n1 = normals[prev];
            Vector2 n2 = normals[k];
            float c1 = Vector2.Dot(n1, poly[prev]) + radius;
            float c2 = Vector2.Dot(n2, poly[k]) + radius;
            float det = n1.x * n2.y - n1.y * n2.x;
            if (Mathf.Abs(det) < 1e-5f)
                inner[k] = poly[k] + n2 * radius;
            else
                inner[k] = new Vector2((c1 * n2.y - c2 * n1.y) / det, (n1.x * c2 - n2.x * c1) / det);
        }

        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        var pixels = new Color32[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                float d = SignedDistanceConvex(inner, normals, p) - radius;
                float alpha = Mathf.Clamp01(0.5f - d);
                pixels[y * w + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply();
        return tex;
    }

    // 볼록 다각형까지의 부호 있는 거리(안쪽이면 음수).
    private static float SignedDistanceConvex(Vector2[] poly, Vector2[] normals, Vector2 p)
    {
        int n = poly.Length;
        float maxPlane = float.NegativeInfinity; // 안쪽이면 모든 값이 음수
        for (int k = 0; k < n; k++)
        {
            float dk = -Vector2.Dot(normals[k], p - poly[k]); // 변 바깥쪽이면 양수
            if (dk > maxPlane) maxPlane = dk;
        }
        if (maxPlane <= 0f) return maxPlane;

        // 바깥: 가장 가까운 변(선분)까지의 거리
        float best = float.PositiveInfinity;
        for (int k = 0; k < n; k++)
        {
            Vector2 a = poly[k];
            Vector2 b = poly[(k + 1) % n];
            Vector2 ab = b - a;
            float len2 = ab.sqrMagnitude;
            float u = len2 > 1e-6f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2) : 0f;
            float dist = (p - (a + ab * u)).magnitude;
            if (dist < best) best = dist;
        }
        return best;
    }

    private static void DestroyGenerated(ref Sprite sprite, ref Texture2D texture)
    {
        if (sprite != null) Object.Destroy(sprite);
        if (texture != null) Object.Destroy(texture);
        sprite = null;
        texture = null;
    }
}
