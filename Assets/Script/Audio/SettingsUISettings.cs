using UnityEngine;

// [설정] 창(SettingsUI - 시작 화면/ESC 중단 화면 공용)의 문구/크기 설정입니다. Assets/Resources/SettingsUISettings 에 두면 자동으로 읽고, 없으면 기본값.
// 창 바탕/모서리/글자 색/버튼 색은 PopupStyle을 그대로 씁니다(흰 창 + 모서리 무늬, [닫기] = 하늘색 버튼).
// 창 왼쪽 책갈피 탭(소리/HUD)과 HUD 탭(SettingsHUDTab - 보이는 스탯/불투명도/견본) 값도 여기 있습니다.
// HUD 탭 견본은 Player HUD Prefab(Assets/Config/Level/PlayerHUD)의 스탯 창 값을 읽어 실제 HUD와 같은 모양으로 그립니다 -
// 메뉴 MobArchive/설정 창/HUD 견본 연결 자동 채우기 로 프리팹/배경 그림을 채울 수 있습니다.
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

    [Header("탭 (창 왼쪽 책갈피 - 오른쪽 부분은 창 뒤에 숨고, 고른 탭만 왼쪽으로 튀어나옴)")]
    public string soundTabLabel = "소리";
    public string hudTabLabel = "HUD";
    [Tooltip("탭 하나의 크기(창 뒤에 숨는 부분 포함)입니다.")]
    public Vector2 tabSize = new Vector2(200f, 64f);
    [Tooltip("탭 중 창 뒤에 숨는 폭입니다.")]
    public float tabHiddenWidth = 50f;
    [Tooltip("고른 탭이 왼쪽으로 더 튀어나오는 거리입니다.")]
    public float tabPopOut = 28f;
    [Tooltip("창 위쪽 끝에서 첫 탭까지의 거리입니다.")]
    public float tabTopMargin = 40f;
    [Tooltip("탭 사이 세로 간격입니다.")]
    public float tabSpacing = 12f;
    public int tabFontSize = 28;
    [Tooltip("탭이 튀어나오고 들어가는 시간(초, 실제 시간 기준)입니다.")]
    [Min(0f)] public float tabMoveDuration = 0.15f;

    [Header("HUD 탭 - 창 (높이는 고정, 넘치는 내용은 세로 스크롤)")]
    [Tooltip("HUD 탭 창의 폭입니다(높이 값은 쓰지 않음 - 아래 Hud Panel Visible Height).")]
    public Vector2 hudPanelSize = new Vector2(1280f, 940f);
    [Tooltip("HUD 탭 창의 높이입니다. 내용이 이보다 길면 마우스 휠/오른쪽 스크롤 막대로 내려 봅니다.")]
    public float hudPanelVisibleHeight = 860f;
    [Tooltip("스크롤 영역이 시작하는 높이(창 위쪽 끝에서의 거리 - 제목 아래)입니다.")]
    public float hudScrollTop = 66f;
    [Tooltip("마우스 휠 한 칸에 움직이는 거리(px)입니다.")]
    public float hudScrollSpeed = 70f;
    public float hudScrollBarWidth = 8f;
    [Tooltip("스탯 불투명도 묶음과 인벤토리 불투명도 묶음 사이 간격(가운데에 구분선)입니다.")]
    public float hudSectionGap = 70f;
    [Tooltip("HUD 탭일 때 창 위치 이동(화면 가운데 기준)입니다. 왼쪽 탭까지 화면 가운데에 오도록 조금 오른쪽으로 둡니다.")]
    public Vector2 hudPanelOffset = new Vector2(40f, 0f);
    [Tooltip("창 왼쪽/오른쪽 안쪽 여백입니다.")]
    public float hudSideMargin = 50f;
    public int hudSectionFontSize = 24;
    public int hudLabelFontSize = 21;

    [Header("HUD 탭 - 문구")]
    public string statViewLabel = "스탯 보기";
    public string statOpacitySectionLabel = "스탯 불투명도";
    public string windowOpacityLabel = "창 불투명도";
    public string whiteTextLabel = "숫자를 흰색으로";
    public string contentOpacityLabel = "아이콘·숫자 불투명도";
    public string combatFadeLabel = "전투 중 자동 반투명";
    public string combatModeBackgroundLabel = "배경 창만";
    public string combatModeAllLabel = "전체";
    public string combatOpacityLabel = "전투 중 불투명도";
    public string combatPreviewLabel = "전투 화면";
    public string normalPreviewLabel = "비전투 화면";
    public string inventoryOpacitySectionLabel = "인벤토리 불투명도";
    public string inventoryWindowOpacityLabel = "창 불투명도";
    public string slotSubFrameOpacityLabel = "슬롯 보조틀 불투명도";
    public string slotIconOpacityLabel = "슬롯 아이콘 불투명도";
    public string passiveIconOpacityLabel = "패시브 아이콘 불투명도";
    [Tooltip("스탯/인벤토리 전투 중 자동 반투명이 둘 다 꺼져 있을 때 '전투 화면' 제목 뒤에 붙는 말입니다.")]
    public string previewOffSuffix = " (꺼짐)";

    [Header("HUD 탭 - 보이는 스탯 목록")]
    [Min(1)] public int hudStatColumns = 5;
    public float hudStatRowHeight = 44f;
    public float hudCheckboxSize = 26f;
    public float hudStatIconSize = 34f;
    public int hudStatNameFontSize = 19;

    [Header("HUD 탭 - 불투명도 줄")]
    public float hudControlRowSpacing = 54f;
    public float hudSliderLabelWidth = 200f;
    [Tooltip("배경 창만 / 전체 버튼 크기입니다(높이는 둘이 같음).")]
    public Vector2 hudModeBackgroundButtonSize = new Vector2(120f, 32f);
    public Vector2 hudModeAllButtonSize = new Vector2(90f, 32f);
    public int hudModeButtonFontSize = 17;
    [Tooltip("배경 창만 / 전체 중 고르지 않은 버튼 색입니다.")]
    public Color hudModeUnselectedColor = new Color32(0xE9, 0xF0, 0xF5, 0xFF);
    [Tooltip("전투 중 불투명도 슬라이더에 '여기까지만 올릴 수 있음'을 보여주는 눈금 색입니다.")]
    public Color hudSliderCapMarkColor = new Color32(0xAA, 0xB9, 0xC8, 0xFF);

    [Header("HUD 탭 - 견본 (전투/비전투 화면)")]
    [Tooltip("실제 스탯 창 값을 읽어 올 PlayerHUD 프리팹(Assets/Config/Level/PlayerHUD)입니다. 비우면 게임 중엔 씬의 스탯 창에서 읽고, 시작 화면에선 견본이 비어 보입니다.")]
    public GameObject playerHUDPrefab;
    [Tooltip("전투 화면 견본 뒤에 깔 그림입니다(비우면 비전투 그림을 씀).")]
    public Sprite combatPreviewBackground;
    [Tooltip("비전투 화면 견본 뒤에 깔 그림입니다.")]
    public Sprite normalPreviewBackground;
    public float previewCornerRadius = 18f;
    [Tooltip("견본 칸 안쪽 여백(견본 스탯 창과 칸 가장자리 사이)입니다.")]
    public float previewInnerMargin = 22f;
    [Tooltip("견본(스탯 + 인벤토리 묶음)의 최대 배율입니다(칸에 맞게 자동으로 더 줄어듦).")]
    [Range(0.1f, 1f)] public float previewMaxScale = 1f;
    [Tooltip("견본 칸의 높이입니다(스크롤되므로 창 높이와 상관없이 크게 둘 수 있음).")]
    public float previewBoxHeight = 340f;
    [Tooltip("견본에서 스탯 창과 인벤토리 사이 간격(원래 크기 기준)입니다.")]
    public float previewGroupGap = 24f;
    [Tooltip("인벤토리 견본에서 슬롯 인벤토리와 패시브 인벤토리 사이 간격(원래 크기 기준)입니다.")]
    public float previewInventoryGap = 20f;
    [Tooltip("인벤토리 견본 슬롯 칸의 예시 아이콘입니다(0 = 장신구, 1 = 일회용, 2 = 음식 - 각 첫 칸). 메뉴 MobArchive/설정 창/HUD 견본 연결 " +
             "자동 채우기로 아이템 목록에서 한 번 골라 넣습니다(실행 중에 계산하지 않음).")]
    public Sprite[] previewSlotIcons = new Sprite[3];
    [Tooltip("인벤토리 견본 패시브 칸의 예시 아이콘입니다(위 메뉴로 한 번 채움).")]
    public Sprite[] previewPassiveIcons = new Sprite[0];

    private static SettingsUISettings cached;

    public static SettingsUISettings Get()
    {
        if (cached == null) cached = Resources.Load<SettingsUISettings>(ResourcePath);
        if (cached == null) cached = CreateInstance<SettingsUISettings>();
        return cached;
    }
}
