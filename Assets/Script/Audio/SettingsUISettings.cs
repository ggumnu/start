using UnityEngine;

// 시작 화면 [설정] 창(SettingsUI)의 문구/크기 설정입니다. Assets/Resources/SettingsUISettings 에 두면 자동으로 읽고, 없으면 기본값.
// 창 바탕/모서리/글자 색/버튼 색은 PopupStyle을 그대로 씁니다(흰 창 + 모서리 무늬, [닫기] = 하늘색 버튼).
[CreateAssetMenu(menuName = "MobArchive/Settings UI Settings", fileName = "SettingsUISettings")]
public class SettingsUISettings : ScriptableObject
{
    public const string ResourcePath = "SettingsUISettings";

    [Header("문구")]
    public string title = "설정";
    public string bgmLabel = "배경음악";
    public string sfxLabel = "효과음";
    public string closeLabel = "닫기";

    [Header("창 (1920x1080 기준 px)")]
    public Vector2 panelSize = new Vector2(620f, 320f);
    public int titleFontSize = 30;
    public int labelFontSize = 24;
    [Tooltip("표시 순서입니다(시작 화면/도감보다 위, 로딩 화면 1000보다 아래).")]
    public int canvasSortingOrder = 150;
    [Tooltip("창 뒤 화면을 가리는 막 색입니다(뒤의 시작 화면 버튼도 이 막이 막음).")]
    public Color dimColor = new Color(0f, 0f, 0f, 0.5f);

    [Header("슬라이더")]
    public float sliderWidth = 320f;
    public float sliderHeight = 14f;
    public float knobSize = 30f;
    [Tooltip("슬라이더 빈 부분 색입니다(연한 회청색).")]
    public Color sliderTrackColor = new Color32(0xDC, 0xE6, 0xEE, 0xFF);
    [Tooltip("채워진 부분/손잡이 색입니다(비우면 투명이므로 꼭 색을 넣으세요). 기본은 PopupStyle 버튼색과 같은 하늘색.")]
    public Color fillColor = new Color32(0x8F, 0xE2, 0xFD, 0xFF);
    [Tooltip("손잡이 색입니다(아래에 버튼과 같은 옅은 그림자가 깔림).")]
    public Color knobColor = Color.white;
    [Tooltip("두 줄(배경음악/효과음) 사이 간격입니다.")]
    public float rowSpacing = 70f;

    [Header("닫기 버튼")]
    public Vector2 buttonSize = new Vector2(140f, 46f);
    public float buttonBottomMargin = 24f;
    public int buttonFontSize = 22;

    private static SettingsUISettings cached;

    public static SettingsUISettings Get()
    {
        if (cached == null) cached = Resources.Load<SettingsUISettings>(ResourcePath);
        if (cached == null) cached = CreateInstance<SettingsUISettings>();
        return cached;
    }
}
