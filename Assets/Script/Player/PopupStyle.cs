using UnityEngine;

// [공용 팝업/버튼 스타일 에셋 (형빈 확정)]
// 게임 안의 모든 팝업 창과 버튼이 같은 색/모양을 쓰도록 한 곳에 모아둔 설정입니다. 여기 값을 바꾸면 모든 UI가 같이 바뀝니다.
// 버튼/창의 크기와 글자 크기는 UI마다 다르므로 각 UI(또는 그 설정 에셋)에 남아 있습니다.
//
// [블루 아카이브 스타일 - "새 스타일" 칸들]
// 창 = 불투명 흰 둥근 사각형 + 왼쪽 위/오른쪽 아래 삼각형 무늬 + 옅은 그림자, 창 안 글자 = 2C4563.
// 버튼 = 둥근 평행사변형 + 흰 삼각형 무늬 + 옅은 그림자. 종류(UIButtonKind)별 색:
//   긍정(노랑, 갈색 글자) = 재확인창의 실행 버튼(예/재시작하기/버리기 등), 알림 창 [확인], 타이틀 [게임 시작]
//   그만두기(회색) = 중단 화면 [그만하기], 타이틀/상점 [나가기]
//   일반(하늘색) = 그 외 전부(재확인창의 아니오/취소하기 포함)
// 마우스를 올리면 색만 어두워지고, 누르는 동안 작아집니다(클릭은 떼는 순간). 그리는 부품은 UIPatternBox.
// 셰이더 Assets/Shaders/UIPatternBox.shader를 Box Shader 칸에 넣어두세요(빌드에 포함되도록 - 비우면 이름으로 찾음).
//
// [예전 스타일 칸] 검은 팝업/빨간 위험 버튼 시절 값입니다. 이제 아무 UI도 쓰지 않아 Inspector에서 숨겨 두었습니다.
//
// [못 누르는 버튼]
// 버튼 색과 글자 색을 Disabled Button Alpha만큼 반투명하게 합니다(예: 돈이 부족할 때 [구매하기], 아무것도 고르지 않았을 때 [버리기]).
//
// [만들기]
// Project 창에서 Assets/Resources 폴더 우클릭 > Create > MobArchive > Popup Style, 이름을 PopupStyle로. 에셋이 없으면 아래 기본값으로 동작합니다.
[CreateAssetMenu(fileName = "PopupStyle", menuName = "MobArchive/Popup Style")]
public class PopupStyle : ScriptableObject
{
    public const string ResourcePath = "PopupStyle";

    [Header("새 스타일 - 공통")]
    [Tooltip("창/버튼을 그리는 셰이더입니다. Assets/Shaders/UIPatternBox.shader를 넣으세요(빌드에 포함되도록). 비우면 이름으로 찾습니다.")]
    public Shader boxShader;
    [Tooltip("창 안 글자 색, 하늘색/회색 버튼 글자 색입니다(#2C4563).")]
    public Color panelTextColor = new Color32(0x2C, 0x45, 0x63, 0xFF);

    [Header("새 스타일 - 창 (불투명 흰 둥근 사각형)")]
    public Color panelColor = Color.white;
    [Min(0f)] public float panelCornerRadius = 16f;
    [Tooltip("끄면 무늬 없이 흰 창만 나옵니다.")]
    public bool panelPattern = true;
    [Tooltip("삼각형 한 변의 길이(px)입니다.")]
    [Min(4f)] public float panelPatternTriangleSide = 70f;
    [Tooltip("모서리에서 무늬가 완전히 사라지는 거리입니다(창 가로+세로에 대한 비율 - 창 크기가 달라도 같은 느낌).")]
    [Range(0.05f, 1f)] public float panelPatternSpread = 0.37f;
    [Tooltip("가장 진한 곳의 불투명도입니다.")]
    [Range(0f, 1f)] public float panelPatternMaxAlpha = 0.75f;
    [Tooltip("삼각형의 가장 진한 색입니다(아이템 도감과 같은 색).")]
    public Color panelPatternTint = new Color32(200, 228, 248, 255);
    [Tooltip("반짝일 때 바뀌는 색입니다.")]
    public Color panelPatternGlowColor = new Color32(150, 205, 250, 255);
    [Tooltip("반짝임 세기입니다. 0이면 반짝이지 않습니다.")]
    [Range(0f, 1f)] public float panelPatternGlowAmount = 0.35f;
    [Min(0f)] public float panelPatternGlowSpeed = 0.15f;
    [Min(0.1f)] public float panelPatternGlowSharpness = 6f;
    public Color panelShadowColor = new Color32(0x1B, 0x2A, 0x40, 0xFF);
    [Tooltip("그림자 위치(px, 음수 y = 아래로)입니다.")]
    public Vector2 panelShadowOffset = new Vector2(0f, -4f);
    [Min(0f)] public float panelShadowBlur = 10f;
    [Range(0f, 1f)] public float panelShadowAlpha = 0.3f;

    [Header("새 스타일 - 버튼 모양 (둥근 평행사변형)")]
    [Tooltip("기울기(도)입니다. 양수면 위쪽이 오른쪽으로 기웁니다(/ 모양).")]
    [Range(0f, 30f)] public float buttonSlantAngle = 12f;
    [Tooltip("버튼 모서리 둥글기(px)입니다.")]
    [Min(0f)] public float buttonCornerRadiusNew = 10f;
    public bool buttonPattern = true;
    [Min(4f)] public float buttonPatternTriangleSide = 24f;
    [Tooltip("모서리에서 무늬가 사라지는 거리입니다(버튼 가로+세로에 대한 비율).")]
    [Range(0.05f, 1f)] public float buttonPatternSpread = 0.3f;
    [Range(0f, 1f)] public float buttonPatternMaxAlpha = 0.7f;
    [Tooltip("버튼 무늬 색입니다(흰색 = 바탕보다 밝은 삼각형).")]
    public Color buttonPatternTint = Color.white;
    public Color buttonShadowColor = new Color32(0x1B, 0x2A, 0x40, 0xFF);
    public Vector2 buttonShadowOffset = new Vector2(0f, -4f);
    [Min(0f)] public float buttonShadowBlur = 8f;
    [Range(0f, 1f)] public float buttonShadowAlpha = 0.3f;
    [Tooltip("누르고 있는 동안의 크기 배율입니다.")]
    [Range(0.5f, 1f)] public float buttonPressScale = 0.94f;
    [Tooltip("작아지고/돌아오는 데 걸리는 시간(초, 실제 시간)입니다.")]
    [Min(0f)] public float buttonPressDuration = 0.06f;

    [Header("새 스타일 - 아이템 이름 판 (흰 창 위 아이템 이름 뒤에 까는 평행사변형 - 등급 색 이름이 잘 보이도록)")]
    public Color namePlateColor = new Color32(0x2C, 0x45, 0x63, 0xFF);
    [Min(0f)] public float namePlateCornerRadius = 6f;
    [Tooltip("이름 글자 양옆 여백(px)입니다.")]
    [Min(0f)] public float namePlatePaddingX = 18f;
    [Tooltip("판 높이 = 글자 크기 + 이 값(px)입니다.")]
    [Min(0f)] public float namePlateExtraHeight = 14f;

    [Header("새 스타일 - 버튼 색: 긍정 (노랑 - 재확인창 실행/확인/게임 시작)")]
    public Color positiveColor = new Color32(0xFF, 0xE0, 0x4A, 0xFF);
    public Color positiveHoverColor = new Color32(0xE8, 0xC7, 0x35, 0xFF);
    [Tooltip("노란 버튼 글자 색입니다(어두운 갈색).")]
    public Color positiveTextColor = new Color32(0x5C, 0x2C, 0x10, 0xFF);

    [Header("새 스타일 - 버튼 색: 일반 (하늘색 - 그 외 전부)")]
    public Color normalColor = new Color32(0x8F, 0xE2, 0xFD, 0xFF);
    public Color normalHoverColor = new Color32(0x65, 0xAD, 0xC7, 0xFF);

    [Header("새 스타일 - 버튼 색: 그만두기 (회색 - 그만하기/나가기)")]
    public Color quitColor = new Color32(0xD5, 0xDF, 0xE7, 0xFF);
    public Color quitHoverColor = new Color32(0xB3, 0xC0, 0xCB, 0xFF);

    [Header("새 스타일 - 못 누르는 버튼")]
    [Tooltip("못 누르는 버튼의 불투명도입니다(버튼 색과 글자 색 모두에 곱함).")]
    [Range(0f, 1f)] public float disabledButtonAlpha = 0.35f;

    // ================= 예전 스타일 (검은 팝업/빨간 위험 버튼 - 이제 아무 UI도 쓰지 않음) =================
    // 혹시 남아 있는 옛 코드가 컴파일되도록 값만 남겨 두고 Inspector에서는 숨깁니다. 새 UI는 위의 "새 스타일" 칸을 쓰세요.

    [HideInInspector] public Color darkPanelColor = new Color(0f, 0f, 0f, 0.9f);
    [HideInInspector] public float darkPanelCornerRadius = 16f;
    [HideInInspector] public float darkPanelButtonCornerRadius = 10f;
    [HideInInspector] public Color buttonColor = new Color32(0x8F, 0xE2, 0xFD, 0xFF);
    [HideInInspector] public Color buttonHoverColor = new Color32(0x65, 0xAD, 0xC7, 0xFF);
    [HideInInspector] public Color buttonTextColor = new Color32(0x2C, 0x45, 0x63, 0xFF);
    [HideInInspector] public Color dangerButtonColor = new Color32(0xE5, 0x48, 0x4D, 0xFF);
    [HideInInspector] public Color dangerButtonHoverColor = new Color32(0xB8, 0x38, 0x3C, 0xFF);
    [HideInInspector] public Color dangerButtonTextColor = Color.white;

    // 새 스타일: 종류/상태(못 누름 > 마우스 올림 > 평소)에 맞는 버튼 색
    public Color GetKindColor(UIButtonKind kind, bool hovered, bool enabled)
    {
        Color normal, hover;
        switch (kind)
        {
            case UIButtonKind.Positive: normal = positiveColor; hover = positiveHoverColor; break;
            case UIButtonKind.Quit: normal = quitColor; hover = quitHoverColor; break;
            default: normal = normalColor; hover = normalHoverColor; break;
        }
        if (!enabled)
        {
            Color c = normal;
            c.a *= disabledButtonAlpha;
            return c;
        }
        return hovered ? hover : normal;
    }

    // 새 스타일: 종류별 버튼 글자 색(못 누르면 반투명)
    public Color GetKindTextColor(UIButtonKind kind, bool enabled)
    {
        Color c = kind == UIButtonKind.Positive ? positiveTextColor : panelTextColor;
        if (!enabled) c.a *= disabledButtonAlpha;
        return c;
    }

    // 상태(못 누름 > 마우스 올림 > 평소)에 맞는 버튼 색
    public Color GetButtonColor(bool hovered, bool enabled)
    {
        if (!enabled)
        {
            Color c = buttonColor;
            c.a *= disabledButtonAlpha;
            return c;
        }
        return hovered ? buttonHoverColor : buttonColor;
    }

    // 위험 버튼 색(평소/마우스 올림)
    public Color GetDangerButtonColor(bool hovered)
    {
        return hovered ? dangerButtonHoverColor : dangerButtonColor;
    }

    // 상태에 맞는 버튼 글자 색
    public Color GetButtonTextColor(bool enabled)
    {
        Color c = buttonTextColor;
        if (!enabled) c.a *= disabledButtonAlpha;
        return c;
    }

    // ---------------- 읽기 ----------------

    private static PopupStyle cached;
    private static bool loaded;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlayStart()
    {
        cached = null;
        loaded = false;
    }

    // Resources/PopupStyle 에셋을 읽습니다. 없으면 기본값으로 만든 임시 설정을 돌려줍니다(항상 null이 아님).
    public static PopupStyle Get()
    {
        if (!loaded || cached == null)
        {
            loaded = true;
            cached = Resources.Load<PopupStyle>(ResourcePath);
            if (cached == null)
            {
                Debug.Log("PopupStyle: Assets/Resources/PopupStyle 에셋이 없어 기본값으로 동작합니다.");
                cached = CreateInstance<PopupStyle>();
            }
        }
        return cached;
    }
}

// 버튼 종류(색). 새 버튼을 만들 때 어떤 종류인지 정해 UIPatternBox.SetupButton에 넘깁니다.
public enum UIButtonKind
{
    Normal = 0,   // 하늘색 - 그 외 전부(재확인창의 아니오/취소하기 포함)
    Positive = 1, // 노랑 - 재확인창의 실행 버튼, 알림 창 [확인], 타이틀 [게임 시작]
    Quit = 2,     // 회색 - [그만하기], [나가기]
}
