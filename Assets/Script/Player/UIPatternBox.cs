using TMPro;
using UnityEngine;
using UnityEngine.UI;

// [공용 UI 상자 - 흰 창 / 평행사변형 버튼]
// 코드로 만드는 팝업의 창과 버튼을 블루 아카이브 스타일로 그립니다(셰이더 MobArchive/UIPatternBox). Image 대신 이 컴포넌트를 붙이고
// 창은 SetupPanel, 버튼은 SetupButton(종류)을 부릅니다. 모양/색/무늬/그림자 값은 전부 PopupStyle에서 읽습니다.
// - 창: 불투명 흰 둥근 사각형 + 왼쪽 위/오른쪽 아래 삼각형 무늬(반짝임) + 옅은 그림자
// - 단색 모양(SetupShape): 무늬 없는 둥근 사각형/평행사변형(+ 그림자 선택) - 슬라이더 손잡이, 이름 판 등
// - 버튼: 둥근 평행사변형 + 흰 삼각형 무늬 + 옅은 그림자. 색은 종류(긍정 노랑/일반 하늘색/그만두기 회색)별
// - 버튼 입력: 매 프레임 TickButton(마우스가 올라가 있는지, 누를 수 있는지)을 부르면 색(호버 = 어둡게)과 누름 연출(작아짐)을
//   맞추고, 버튼 위에서 눌렀다가 버튼 위에서 뗀 순간 true를 돌려줍니다(클릭은 떼는 순간 - 작아지는 연출이 보이도록).
//   누름 연출은 실제 시간 기준이라 게임이 멈춘 상태(ESC 중단 화면 등)에서도 동작합니다.
// - 작아질 때는 이 상자 그림과 바로 아래 자식들(버튼 글자)을 버튼 중심 기준으로 함께 줄입니다(버튼 크기/위치/클릭 범위는 그대로).
[RequireComponent(typeof(CanvasRenderer))]
public class UIPatternBox : MaskableGraphic
{
    private const string ShaderName = "MobArchive/UIPatternBox";

    private bool isButton;
    private UIButtonKind kind = UIButtonKind.Normal;

    private float cornerRadius = 16f;
    private float slantTan;
    private bool patternOn = true;
    private float patternSide = 70f;
    private float patternSpreadRatio = 0.37f;
    private float patternMaxAlpha = 0.75f;
    private Color patternTint = Color.white;
    private Color glowColor = Color.white;
    private float glowAmount;
    private float glowSpeed;
    private float glowSharpness = 6f;
    private Vector2 patternTopLeftOffset;       // 이 상자만의 무늬 조정(SetPatternTweak) - 기본 0/1
    private float patternTopLeftScale = 1f;
    private Vector2 patternBottomRightOffset;
    private float patternBottomRightScale = 1f;
    private float patternTriangleScale = 1f;
    private bool gradientOn;
    private Color gradientEndColor = Color.white;
    private Color shadowColor = Color.black;
    private Vector2 shadowOffset;
    private float shadowBlur;
    private float shadowAlpha;

    private float pressScale = 0.94f;
    private float pressDuration = 0.06f;
    private float pressAmount;   // 0 = 평소 크기, 1 = 가장 작음
    private bool pressing;       // 이 버튼 위에서 왼쪽 버튼을 누른 채인지

    private Material boxMaterial;
    private float meshPad = -1f; // 지금 메시에 들어간 그림자 여백(바뀔 때만 메시를 다시 만듦)
    private static bool shaderWarned;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        shaderWarned = false;
    }

    // go에 상자를 붙입니다(이미 있으면 그것을 돌려줌). 같은 오브젝트에 Image가 있으면 안 됩니다(그래픽은 하나만).
    public static UIPatternBox AddTo(GameObject go)
    {
        var box = go.GetComponent<UIPatternBox>();
        if (box == null) box = go.AddComponent<UIPatternBox>();
        box.raycastTarget = false;
        return box;
    }

    // ---------------- 설정 ----------------

    // 창(흰 둥근 사각형 + 무늬 + 그림자)으로 칠합니다. 매 프레임 불러도 됩니다(PopupStyle을 Play 중 바꾸면 바로 반영).
    public void SetupPanel(PopupStyle s)
    {
        isButton = false;
        color = s.panelColor;
        cornerRadius = s.panelCornerRadius;
        slantTan = 0f;
        patternOn = s.panelPattern;
        patternSide = s.panelPatternTriangleSide;
        patternSpreadRatio = s.panelPatternSpread;
        patternMaxAlpha = s.panelPatternMaxAlpha;
        patternTint = s.panelPatternTint;
        glowColor = s.panelPatternGlowColor;
        glowAmount = s.panelPatternGlowAmount;
        glowSpeed = s.panelPatternGlowSpeed;
        glowSharpness = s.panelPatternGlowSharpness;
        shadowColor = s.panelShadowColor;
        shadowOffset = s.panelShadowOffset;
        shadowBlur = s.panelShadowBlur;
        shadowAlpha = s.panelShadowAlpha;
        PushMaterial();
    }

    // 버튼(종류별 색의 둥근 평행사변형 + 무늬 + 그림자)으로 칠합니다. 색은 TickButton이 호버/누를 수 있는지에 따라 정합니다.
    public void SetupButton(PopupStyle s, UIButtonKind buttonKind)
    {
        if (!isButton || kind != buttonKind) color = s.GetKindColor(buttonKind, false, true); // 처음 한 번만(이후 색은 TickButton)
        isButton = true;
        kind = buttonKind;
        cornerRadius = s.buttonCornerRadiusNew;
        slantTan = Mathf.Tan(Mathf.Clamp(s.buttonSlantAngle, -45f, 45f) * Mathf.Deg2Rad);
        patternOn = s.buttonPattern;
        patternSide = s.buttonPatternTriangleSide;
        patternSpreadRatio = s.buttonPatternSpread;
        patternMaxAlpha = s.buttonPatternMaxAlpha;
        patternTint = s.buttonPatternTint;
        glowColor = s.buttonPatternTint;
        glowAmount = 0f;
        glowSpeed = 0f;
        glowSharpness = 6f;
        shadowColor = s.buttonShadowColor;
        shadowOffset = s.buttonShadowOffset;
        shadowBlur = s.buttonShadowBlur;
        shadowAlpha = s.buttonShadowAlpha;
        pressScale = s.buttonPressScale;
        pressDuration = s.buttonPressDuration;
        PushMaterial();
    }

    // 무늬 없는 단색 모양(슬라이더 손잡이, 아이템 이름 판, 상점 +/- 버튼 등). slantAngle = 기울기(도, 0이면 둥근 사각형),
    // withShadow = 버튼과 같은 옅은 그림자를 깔지. 색은 이 color로 정해지고, 이후 바꾸려면 .color를 직접 바꿉니다.
    public void SetupShape(PopupStyle s, Color shapeColor, float shapeCornerRadius, float slantAngle, bool withShadow)
    {
        isButton = false;
        gradientOn = false;
        color = shapeColor;
        cornerRadius = shapeCornerRadius;
        slantTan = Mathf.Tan(Mathf.Clamp(slantAngle, -45f, 45f) * Mathf.Deg2Rad);
        patternOn = false;
        glowAmount = 0f;
        shadowColor = s.buttonShadowColor;
        shadowOffset = s.buttonShadowOffset;
        shadowBlur = s.buttonShadowBlur;
        shadowAlpha = withShadow ? s.buttonShadowAlpha : 0f;
        PushMaterial();
    }

    // 바탕색을 왼쪽 위(지금 color) → 오른쪽 아래(endColor) 대각선 그라데이션으로 칠합니다. Setup 뒤에 부릅니다(SetupShape는 끔).
    public void SetGradient(bool on, Color endColor)
    {
        gradientOn = on;
        gradientEndColor = endColor;
        PushMaterial();
    }

    // 무늬를 직접 정합니다(SetupShape 뒤에 - 상점 카드/등급 칸처럼 창/버튼과 다른 무늬를 쓸 때). spreadRatio = (가로+세로)에 대한 퍼짐 비율.
    public void SetPattern(bool on, float triangleSide, float spreadRatio, float maxAlpha, Color tint)
    {
        patternOn = on;
        patternSide = Mathf.Max(4f, triangleSide);
        patternSpreadRatio = Mathf.Max(0.01f, spreadRatio);
        patternMaxAlpha = Mathf.Clamp01(maxAlpha);
        patternTint = tint;
        glowColor = tint;
        glowAmount = 0f;
        PushMaterial();
    }

    // 기울기만 바꿉니다(Setup 뒤에 - 상점 카드처럼 PopupStyle과 다른 기울기를 쓸 때). 도 단위.
    public void SetSlant(float slantAngle)
    {
        slantTan = Mathf.Tan(Mathf.Clamp(slantAngle, -45f, 45f) * Mathf.Deg2Rad);
        PushMaterial();
    }

    // 이 상자의 무늬만 조정합니다(PopupStyle 값에 더하거나 곱함 - Setup을 다시 불러도 유지).
    // 오프셋 = 무늬가 퍼져 나오는 모서리 기준점 이동(px, +x 오른쪽, +y 위), 배율 = 퍼지는 범위, 삼각형 배율 = 삼각형 한 개 크기.
    public void SetPatternTweak(Vector2 topLeftOffset, float topLeftScale, Vector2 bottomRightOffset, float bottomRightScale, float triangleScale)
    {
        patternTopLeftOffset = topLeftOffset;
        patternTopLeftScale = Mathf.Max(0f, topLeftScale);
        patternBottomRightOffset = bottomRightOffset;
        patternBottomRightScale = Mathf.Max(0f, bottomRightScale);
        patternTriangleScale = Mathf.Max(0.1f, triangleScale);
        PushMaterial();
    }

    public UIButtonKind Kind => kind;

    // 버튼 한 프레임 처리: 색(평소/호버/못 누름) + 누름 연출. 버튼 위에서 눌렀다가 버튼 위에서 뗀 프레임에 true(= 클릭).
    // hovered = 마우스가 버튼 위에 있는지(각 UI가 RectangleContainsScreenPoint로 판단), interactable = 누를 수 있는지.
    public bool TickButton(bool hovered, bool interactable)
    {
        bool clicked = TickPress(hovered, interactable);
        color = PopupStyle.Get().GetKindColor(kind, hovered, interactable);
        return clicked;
    }

    // 누름 연출(작아짐) + 클릭 판정만 합니다(색은 바꾸지 않음 - 상점 [-][+]처럼 색을 직접 정하는 버튼용).
    // 단색 모양(SetupShape)에 쓰면 PopupStyle의 버튼 누름 크기/시간을 씁니다.
    public bool TickPress(bool hovered, bool interactable)
    {
        if (!isButton)
        {
            var st = PopupStyle.Get();
            pressScale = st.buttonPressScale;
            pressDuration = st.buttonPressDuration;
        }
        bool clicked = false;

        if (!interactable)
        {
            pressing = false;
        }
        else
        {
            if (hovered && Input.GetMouseButtonDown(0)) pressing = true;
            if (pressing && Input.GetMouseButtonUp(0))
            {
                clicked = hovered;
                pressing = false;
            }
            else if (pressing && !Input.GetMouseButton(0))
            {
                pressing = false; // 다른 창이 입력을 가져가 뗀 순간을 못 본 경우
            }
        }

        float target = pressing && hovered ? 1f : 0f;
        float step = pressDuration > 0f ? Time.unscaledDeltaTime / pressDuration : 1f;
        float next = Mathf.MoveTowards(pressAmount, target, step);
        if (!Mathf.Approximately(next, pressAmount))
        {
            pressAmount = next;
            ApplyPressScale();
        }
        return clicked;
    }

    // 버튼 글자 색(종류별, 못 누르면 반투명)
    public Color GetLabelColor(bool interactable)
    {
        return PopupStyle.Get().GetKindTextColor(kind, interactable);
    }

    // 누름 상태를 지웁니다(창을 닫거나 다시 열 때).
    public void ResetPress()
    {
        pressing = false;
        if (pressAmount != 0f)
        {
            pressAmount = 0f;
            ApplyPressScale();
        }
    }

    private float CurrentScale()
    {
        float e = pressAmount * pressAmount * (3f - 2f * pressAmount);
        return Mathf.Lerp(1f, pressScale, e);
    }

    private void ApplyPressScale()
    {
        // 단색 모양을 누름 버튼으로 쓸 때도 자식(글자)을 함께 줄입니다.
        float sc = CurrentScale();
        var t = transform;
        for (int i = 0; i < t.childCount; i++) t.GetChild(i).localScale = new Vector3(sc, sc, 1f);
        SetVerticesDirty();
    }

    // ---------------- 그리기 ----------------

    protected override void OnDisable()
    {
        pressing = false;
        if (pressAmount != 0f)
        {
            pressAmount = 0f;
            var t = transform;
            for (int i = 0; i < t.childCount; i++) t.GetChild(i).localScale = Vector3.one;
        }
        base.OnDisable();
    }

    protected override void OnDestroy()
    {
        if (boxMaterial != null)
        {
            if (Application.isPlaying) Destroy(boxMaterial);
            else DestroyImmediate(boxMaterial);
            boxMaterial = null;
        }
        base.OnDestroy();
    }

    protected override void OnRectTransformDimensionsChange()
    {
        base.OnRectTransformDimensionsChange();
        PushMaterial();
    }

    void LateUpdate()
    {
        if (boxMaterial != null && glowAmount > 0f) boxMaterial.SetVector("_Glow", new Vector4(glowAmount, glowSpeed, glowSharpness, Time.unscaledTime));
    }

    private void EnsureMaterial()
    {
        if (boxMaterial != null) return;
        Shader shader = PopupStyle.Get().boxShader;
        if (shader == null) shader = Shader.Find(ShaderName);
        if (shader == null)
        {
            if (!shaderWarned)
            {
                shaderWarned = true; // 경고는 한 번만
                Debug.LogWarning("UIPatternBox: 셰이더(MobArchive/UIPatternBox)를 찾지 못해 단색 사각형으로 그립니다. " +
                                 "Assets/Resources/PopupStyle의 Box Shader 칸에 Assets/Shaders/UIPatternBox.shader를 넣어주세요.");
            }
            return;
        }
        boxMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        material = boxMaterial;
    }

    private void PushMaterial()
    {
        EnsureMaterial();
        if (boxMaterial == null) return;

        Rect r = rectTransform.rect;
        float w = Mathf.Max(1f, r.width);
        float h = Mathf.Max(1f, r.height);
        boxMaterial.SetVector("_Size", new Vector4(w, h, 0f, 0f));
        boxMaterial.SetVector("_Shape", new Vector4(cornerRadius, slantTan, 0f, 0f));
        float spread = Mathf.Max(1f, (w + h) * patternSpreadRatio);
        boxMaterial.SetVector("_Pattern", new Vector4(patternSide * patternTriangleScale, spread, patternMaxAlpha, patternOn ? 1f : 0f));
        boxMaterial.SetVector("_PatternCorners", new Vector4(patternTopLeftOffset.x, patternTopLeftOffset.y,
                                                             patternBottomRightOffset.x, patternBottomRightOffset.y));
        boxMaterial.SetVector("_PatternSpreads", new Vector4(Mathf.Max(0.01f, spread * patternTopLeftScale),
                                                             Mathf.Max(0.01f, spread * patternBottomRightScale), 0f, 0f));
        boxMaterial.SetColor("_PatternTint", patternTint);
        boxMaterial.SetColor("_GlowColor", glowColor);
        boxMaterial.SetVector("_Glow", new Vector4(glowAmount, glowSpeed, glowSharpness, Time.unscaledTime));
        boxMaterial.SetColor("_GradientColor", gradientEndColor);
        boxMaterial.SetVector("_Gradient", new Vector4(gradientOn ? 1f : 0f, 0f, 0f, 0f));
        boxMaterial.SetColor("_ShadowColor", shadowColor);
        boxMaterial.SetVector("_Shadow", new Vector4(shadowOffset.x, shadowOffset.y, shadowBlur, shadowAlpha));
        if (!Mathf.Approximately(MeshPad(), meshPad)) SetVerticesDirty();
    }

    private float MeshPad()
    {
        float pad = 2f;
        if (shadowAlpha > 0f) pad += shadowBlur + Mathf.Max(Mathf.Abs(shadowOffset.x), Mathf.Abs(shadowOffset.y));
        return pad;
    }

    // 상자 + 그림자가 들어갈 만큼 넓은 사각형 하나. uv = 상자 중심 기준 px(누름 연출로 줄어도 uv는 그대로 → 모양 전체가 같이 줄어듦).
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = rectTransform.rect;
        if (r.width <= 0f || r.height <= 0f) return;

        float pad = MeshPad();
        meshPad = pad;
        float hx = r.width * 0.5f + pad;
        float hy = r.height * 0.5f + pad;
        Vector2 c = r.center;
        float sc = CurrentScale();
        Color32 col = color;

        vh.AddVert(new Vector3(c.x - hx * sc, c.y - hy * sc), col, new Vector4(-hx, -hy));
        vh.AddVert(new Vector3(c.x - hx * sc, c.y + hy * sc), col, new Vector4(-hx, hy));
        vh.AddVert(new Vector3(c.x + hx * sc, c.y + hy * sc), col, new Vector4(hx, hy));
        vh.AddVert(new Vector3(c.x + hx * sc, c.y - hy * sc), col, new Vector4(hx, -hy));
        vh.AddTriangle(0, 1, 2);
        vh.AddTriangle(2, 3, 0);
    }
}

// [아이템 이름 판] 흰 창 위에서 아이템 이름(등급 색)이 잘 보이도록 이름 뒤에 남색 평행사변형 판(PopupStyle의 아이템 이름 판)을 깝니다.
// 슬롯 교체 확인창/버리기 확인창/패시브 교체 확인창/상점 구매 팝업이 씁니다. 위치는 만든 쪽이 rect로 정합니다.
public class UINamePlate
{
    public RectTransform rect;
    public UIPatternBox box;
    public TextMeshProUGUI text;

    public static UINamePlate Create(Transform parent, string name)
    {
        var plate = new UINamePlate();
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        plate.rect = go.AddComponent<RectTransform>();
        plate.rect.anchorMin = plate.rect.anchorMax = new Vector2(0.5f, 0.5f);
        plate.rect.pivot = new Vector2(0.5f, 0.5f);
        plate.box = UIPatternBox.AddTo(go);

        var textGO = new GameObject("Name");
        textGO.transform.SetParent(go.transform, false);
        var textRect = textGO.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        plate.text = HUDText.AddTo(textGO, TextAnchor.MiddleCenter);
        plate.text.raycastTarget = false;
        return plate;
    }

    // 판 높이(글자 크기 + 여백)
    public static float Height(int fontSize, PopupStyle s)
    {
        return fontSize + s.namePlateExtraHeight;
    }

    // 이름/색/글자 크기를 넣고 판 크기를 글자에 맞춥니다. 판이 maxWidth를 넘으면 maxWidth로 자르고, 글자를 판 안(기울어진 양끝 +
    // 여백 안쪽)에 들어갈 만큼 줄입니다. 가장 작은 크기(10)로도 안 들어가면 끝을 "…"로 자릅니다. 판 폭을 돌려줍니다.
    public float Fit(string name, Color nameColor, int fontSize, float maxWidth, PopupStyle s)
    {
        text.text = name;
        text.color = nameColor;
        text.enableAutoSizing = false;
        text.overflowMode = TextOverflowModes.Overflow;
        text.fontSize = fontSize;

        float height = Height(fontSize, s);
        float slantExtra = height * Mathf.Tan(Mathf.Clamp(s.buttonSlantAngle, 0f, 45f) * Mathf.Deg2Rad);
        float inset = s.namePlatePaddingX + slantExtra * 0.5f; // 글자 칸 양쪽 여백(기울어진 끝에 닿지 않게)
        float textWidth = text.GetPreferredValues(name).x;
        float width = textWidth + inset * 2f;
        if (width > maxWidth)
        {
            width = Mathf.Max(20f, maxWidth);
            float room = Mathf.Max(1f, width - inset * 2f);
            float scaled = Mathf.Max(10f, fontSize * room / Mathf.Max(1f, textWidth));
            text.fontSize = Mathf.Floor(scaled);
            if (fontSize * room / Mathf.Max(1f, textWidth) < 10f) text.overflowMode = TextOverflowModes.Ellipsis;
        }

        var textRect = text.rectTransform;
        textRect.offsetMin = new Vector2(inset, 0f);
        textRect.offsetMax = new Vector2(-inset, 0f);
        rect.sizeDelta = new Vector2(width, height);
        box.SetupShape(s, s.namePlateColor, s.namePlateCornerRadius, s.buttonSlantAngle, false);
        return width;
    }
}
