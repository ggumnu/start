using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 화면 좌측 하단에 "스탯 아이콘 + 현재 수치"를 세로로 쌓아 보여주는 UI입니다(한 열에
// rowsPerColumn개까지 위에서 아래로 놓고, 넘치면 오른쪽 열로 넘어갑니다).
// PlayerHUDManager가 만들어둔 CanvasRoot 아래에 이 스크립트가 직접 자신의 패널을
// 만들어 붙입니다 (초상화/하트와 달리 PlayerHUDManager 쪽에 미리 만들어둔 컨테이너가
// 없으므로, "왼쪽 상단 그룹"과는 완전히 독립된 위치입니다 - 기획 단계에서 합의한 대로
// 아이템 설명칸의 위치/크기와도 서로 영향을 주지 않습니다).
//
// [표시 방식]
// statEntries 리스트에서 enabled가 체크된 항목만, 리스트에 적힌 순서대로 왼쪽 열의 맨 위
// 줄부터 아래로 한 줄씩 놓이고, 한 열이 rowsPerColumn개(기본 6개)로 차면 그 오른쪽 열의
// 맨 위부터 이어서 놓입니다(Element 0이 가장 왼쪽 위). 각 줄은 "아이콘 + 그 오른쪽에 숫자"로만 구성되고, 이름 텍스트는
// 넣지 않습니다(기획 그대로 - 초록 원 아이콘 자체가 어떤 스탯인지 알려주는 방식).
// 스탯 종류가 많아서(PlayerStats.cs 기준 15종) 아이템 설명칸처럼 항목별 위치를 각각
// 잡지 않고, 대신 "전체 여백(panelMarginX/Y)"과 "줄 사이 간격(rowSpacing)",
// "아이콘-숫자 사이 간격(iconToValueSpacing)"만 조절하도록 했습니다 - 항목을 몇 개
// 켜두느냐에 따라 자동으로 줄이 늘거나 줄어듭니다.
//
// [스킬 스탯 묶음 - 수류탄/해킹툴 등]
// statEntries 각 항목의 "Is Skill Stat" 체크박스가 켜진 스탯은 일반 스탯과 섞이지 않고 따로
// 묶입니다. 일반 스탯을 먼저 위 규칙대로 왼쪽부터 배치하고, 스킬 스탯은 일반 스탯의 마지막
// 열이 꽉 찼든 아니든 항상 그 오른쪽 새 열부터 배치합니다(스킬 스탯이 rowsPerColumn개를 넘으면
// 스킬 묶음 안에서 다시 오른쪽 열로 넘어갑니다). 두 묶음 사이 간격은 일반 열 간격
// (columnSpacing)과 같습니다. 각 묶음 안의 순서는 statEntries 목록 순서를 따르고, 모든 열의
// 윗줄은 가장 긴 열의 윗줄에 맞춰집니다. PlayerStats 쪽은 이 구분과 무관합니다(화면 배치만).
//
// [Play 모드 라이브 트윅]
// PlayerItemInfoDisplay.cs와 동일한 이유로, 매 프레임 값을 다시 읽어 적용합니다.
// 다만 여기는 LayoutGroup을 아예 쓰지 않고 애초에 직접 좌표를 계산하는 방식이라
// (아이템 설명칸에서 겪었던 "Canvas 자동 리빌드가 수동 배치를 덮어쓰는" 문제가 없습니다.
//
// [체크박스로 항목을 켜고 끄면]
// statEntries의 enabled를 Play 도중 바꿔도 즉시 줄이 추가/제거되도록, 매 프레임
// "지금 켜진 항목들의 순서 서명(signature)"이 이전과 다르면 자동으로 다시 만듭니다.
//
// [스탯 설명 플로팅박스]
// 스탯 아이콘 위에 커서를 올리면 커서 바로 위에 검은 반투명 둥근 박스가 떠서, 그 스탯의
// 이름과 설명문을 보여줍니다. 이름과 설명문은 statEntries의 각 항목에 있는 "툴팁 이름"/
// "툴팁 설명문" 칸에 직접 적습니다(이름 칸이 비어 있는 스탯은 박스가 뜨지 않습니다).
// 박스는 이 스크립트가 전용 Canvas(정렬 순서 tooltipCanvasSortingOrder)에 직접 그리고,
// 모서리 둥글기(tooltipCornerRadius)는 이미지 없이 실행 중에 둥근 네모를 생성해서 적용합니다.
// 호버 판정은 다른 HUD와 같은 방식(매 프레임 마우스 좌표와 아이콘 영역 비교)이며 아이콘 영역만
// 대상입니다. 박스는 화면 가장자리 밖으로 나가지 않도록 항상 안쪽으로 보정됩니다.
//
// [접어보기]
// 패널 위쪽 가운데의 버튼(또는 PlayerHUDManager의 전체 접기 키, 기본 Tab)으로 패널을 화면 아래로 접었다
// 펼 수 있습니다. 버튼 생성/애니메이션/클릭 판정은 공용 클래스 CollapsibleHUDPanel이 맡고, 이 스크립트는
// Reflow()에서 계산한 패널 크기와 원래 위치를 넘겨주면 Tick이 최종 위치까지 정합니다(ApplyCollapse).
// 버튼 모양/시간/커브는 인스펙터의 "접어보기" 묶음에서 조절합니다. 패널 크기(panelRect.sizeDelta)는 원래
// 0이었는데, 빠지는 거리와 버튼 위치(패널 위쪽 가운데)를 정하기 위해 Reflow()가 실제 내용 크기로 채웁니다
// (줄들은 왼쪽 아래 기준으로 놓이므로 크기를 채워도 배치는 그대로입니다).
//
// [수치 변화 연출]
// 화면에 보이는 숫자가 바뀌는 순간, 그 줄의 숫자만 잠깐 커지면서 색이 바뀌었다가(flashPopDuration) 원래 크기/색으로
// 부드럽게 돌아옵니다(flashReturnDuration). 원인(아이템 획득, 20번 흡수, 음식/액티브 효과 등)은 따지지 않고 "표시
// 글자가 바뀌었는가"만 봅니다 - 그래서 화면에 안 보이는 미세한 변화(소수점 아래)에는 반응하지 않습니다.
// - 색: 플레이어에게 유리하면 increaseFlashColor(노랑), 불리하면 decreaseFlashColor(빨강). 숫자가 작을수록 좋은
//   스탯(장전 시간/연사 간격/수류탄 자해 데미지)은 statEntries 각 항목의 "Lower Is Better" 체크박스로 구분합니다.
//   이 체크박스는 처음 한 번만 스탯 종류에 맞춰 자동으로 채워지고, 그 뒤로는 직접 바꾼 값을 덮어쓰지 않습니다.
// - 게임 시작/줄 재구성 직후 flashStartupGrace초 동안은 기준값만 잡고 연출하지 않습니다(시작할 때 초기값이 자리
//   잡으면서 숫자가 바뀌는 것을 연출로 착각하지 않도록).
// - 연출 중에 또 바뀌면 처음부터 다시 시작합니다. 숫자는 왼쪽 끝/세로 가운데 기준으로 커집니다.
// - 시간은 실제 시간 기준(게임이 멈춰 있어도 연출은 끝까지 진행).
// - [강제 연출 - 111번] 하한선 때문에 숫자가 그대로여도(1 -> 1) "올랐다/내렸다"를 보여줘야 할 때, 다른 스크립트가
//   PlayerStatDisplay.RequestFlash(스탯, 유리한지)를 부르면 그 스탯 줄이 숫자 변화와 상관없이 한 번 연출됩니다.
//   요청은 FlashRequestLifetime초 안에 처리되지 않으면(그 스탯 줄이 꺼져 있는 등) 버려집니다.
//
// [긴 숫자 처리 - 형빈 확정]
// 숫자 칸 폭(valueTextWidth)은 모든 줄이 같아서, 숫자가 길어지면 오른쪽 열 아이콘 위로 겹쳤습니다. 그래서 두 단계로 막습니다.
// 1) 큰 수는 소수점 숨김: 화면에 보일 값이 hideDecimalAtOrAbove(기본 100) 이상이면 소수 자리를 버리고 정수로만 보여줍니다
//    (112.5 -> 112). "보이는 글자"만 바꾸는 것이고 실제 스탯 값/게임 계산은 소수점까지 그대로입니다. 0이면 이 기능을 끕니다.
// 2) 글자 자동 축소: 그래도 숫자가 "실제로 쓸 수 있는 폭"보다 넓으면 그 줄의 글자만 그 폭에 맞게 줄입니다
//    (autoShrinkValueText). 단 minValueFontSize보다 작아지지는 않습니다. 글자가 바뀔 때만 다시 재므로 가볍습니다. 수치
//    변화 연출은 줄어든 크기를 기준으로 커졌다 돌아옵니다.
//    "실제로 쓸 수 있는 폭" = 숫자 시작점부터 오른쪽 열 아이콘이 시작하는 곳까지 = valueTextWidth + columnSpacing
//    (columnSpacing이 0 이상이면 valueTextWidth 그대로). 열 간격을 음수로 두어 열을 촘촘히 붙인 경우, 숫자가 칸 폭보다
//    짧아도 오른쪽 아이콘에 가려질 수 있어서(처음 버전은 칸 폭만 봐서 이때 줄어들지 않았음) 실제 공간 기준으로 잽니다.
//    아이콘에 딱 붙지 않도록 ValueFitSafetyMargin만큼 여유를 둡니다.
public class PlayerStatDisplay : MonoBehaviour
{
    [System.Serializable]
    public class StatEntry
    {
        public StatType statType = StatType.Damage;
        [Tooltip("체크하면 이 스탯이 화면에 표시됩니다.")]
        public bool enabled = true;
        [Tooltip("이 스탯의 아이콘 스프라이트입니다. 비워두면 초록색 원으로 대체 표시됩니다.")]
        public Sprite icon;
        [Tooltip("체크 해제하면 소수점 없이 정수로 반올림해서 표시합니다 " +
                 "(탄창 크기/연사 횟수처럼 원래 정수인 스탯에 추천).")]
        public bool showDecimal = true;
        [Tooltip("아이콘에 커서를 올렸을 때 뜨는 플로팅박스의 능력치 이름입니다. " +
                 "비워두면 이 스탯은 박스가 뜨지 않습니다.")]
        public string tooltipName = "";
        [Tooltip("아이콘에 커서를 올렸을 때 뜨는 플로팅박스의 설명문입니다. 여러 줄로 적을 수 있고, " +
                 "박스 최대 폭을 넘으면 자동으로 줄바꿈됩니다.")]
        [TextArea(2, 6)] public string tooltipDescription = "";
        [Tooltip("체크하면 이 스탯은 일반 스탯과 섞이지 않고, 일반 스탯 열들의 오른쪽에 따로 묶여서 " +
                 "표시됩니다(수류탄/해킹툴 같은 스킬 스탯용).")]
        public bool isSkillStat = false;
        [Tooltip("체크하면 숫자가 작아질수록 좋은 스탯으로 취급합니다(수치 변화 연출 색: 작아지면 노랑, 커지면 빨강). " +
                 "장전 시간/연사 간격/수류탄 자해 데미지는 처음 한 번 자동으로 체크됩니다.")]
        public bool lowerIsBetter = false;
        // lowerIsBetter를 스탯 종류에 맞춰 자동으로 채웠는지 기록합니다(한 번 채운 뒤로는 직접 바꾼 값을 유지).
        [SerializeField, HideInInspector] public bool lowerIsBetterInitialized = false;
    }

    [Header("참조 (비워두면 씬에서 자동으로 찾습니다)")]
    public PlayerHUDManager hudManager;
    public PlayerStats playerStats;

    [Header("표시할 스탯 목록 - 체크된 것만, 이 순서대로 왼쪽 열 위에서 아래로 놓이고 열이 차면 오른쪽 열로 넘어갑니다")]
    public List<StatEntry> statEntries = new List<StatEntry>
    {
        new StatEntry { statType = StatType.Damage,      enabled = true  },
        new StatEntry { statType = StatType.AttackSpeed, enabled = true  },
        new StatEntry { statType = StatType.MoveSpeed,   enabled = true  },
        new StatEntry { statType = StatType.Range,       enabled = true  },
        new StatEntry { statType = StatType.ReloadTime,        enabled = false },
        new StatEntry { statType = StatType.MagazineSize,      enabled = false, showDecimal = false },
        new StatEntry { statType = StatType.Multishot,         enabled = false, showDecimal = false },
        new StatEntry { statType = StatType.MultishotInterval, enabled = false },
        new StatEntry { statType = StatType.BulletSize,        enabled = false },
        new StatEntry { statType = StatType.BulletSpeed,       enabled = false },
        new StatEntry { statType = StatType.Luck,              enabled = false },
        new StatEntry { statType = StatType.CostRecovery,      enabled = false },
        new StatEntry { statType = StatType.MaxHealth,         enabled = false },
        new StatEntry { statType = StatType.GematriaChance,    enabled = false },
        new StatEntry { statType = StatType.SchaleChance,       enabled = false },
        new StatEntry { statType = StatType.GrenadeDamage,     enabled = false, isSkillStat = true },
        new StatEntry { statType = StatType.GrenadeSelfDamage, enabled = false, isSkillStat = true },
        new StatEntry { statType = StatType.GrenadeRadius,     enabled = false, isSkillStat = true },
        new StatEntry { statType = StatType.GrenadeRange,      enabled = false, isSkillStat = true },
        new StatEntry { statType = StatType.HackDamage,        enabled = false, isSkillStat = true },
        new StatEntry { statType = StatType.HackRadius,        enabled = false, isSkillStat = true },
        new StatEntry { statType = StatType.HackDuration,      enabled = false, isSkillStat = true },
    };

    [Header("패널 위치 (화면 좌측 하단 모서리 기준 여백, px)")]
    public float panelMarginX = 40f;
    public float panelMarginY = 40f;

    [Header("아이콘 / 글자 크기")]
    public float iconSize = 40f;
    public int fontSize = 24;
    public float valueTextWidth = 100f;

    [Tooltip("켜면 숫자가 오른쪽 열 아이콘까지의 실제 공간(Value Text Width + 음수인 Column Spacing)보다 길어질 때 그 줄의 글자만 그 공간에 맞게 줄입니다.")]
    public bool autoShrinkValueText = true;
    [Tooltip("자동 축소로 줄어들 수 있는 가장 작은 글자 크기입니다. 이보다 더 길면 칸을 조금 넘칠 수 있습니다(그때는 Value Text Width를 늘리세요).")]
    [Min(1f)] public float minValueFontSize = 14f;
    [Tooltip("화면에 보일 값이 이 값 이상이면 소수 자리를 버리고 정수로만 보여줍니다(예: 112.5 -> 112). 보이는 글자만 바뀌고 " +
             "실제 스탯 값/계산은 소수점까지 그대로입니다. 0이면 끕니다.")]
    [Min(0f)] public float hideDecimalAtOrAbove = 100f;

    [Header("색상")]
    [Tooltip("아이콘 스프라이트를 아직 안 넣었을 때 대신 보여줄 원 색상입니다.")]
    public Color fallbackIconColor = new Color(0.25f, 0.85f, 0.35f, 1f);
    public Color valueTextColor = Color.white;

    [Header("수치 변화 연출 (숫자가 바뀌는 순간 잠깐 커지며 색이 바뀜)")]
    [Tooltip("끄면 예전처럼 숫자만 바뀝니다.")]
    public bool flashOnChange = true;
    [Tooltip("플레이어에게 유리하게 바뀌었을 때의 색입니다.")]
    public Color increaseFlashColor = new Color(1f, 0.85f, 0.2f, 1f);
    [Tooltip("플레이어에게 불리하게 바뀌었을 때의 색입니다.")]
    public Color decreaseFlashColor = new Color(1f, 0.3f, 0.25f, 1f);
    [Tooltip("가장 커졌을 때의 배율입니다. 옆 열과 겹치면 줄이세요.")]
    [Min(1f)] public float flashPeakScale = 1.4f;
    [Tooltip("커지면서 색이 바뀌는 시간(초)입니다.")]
    [Min(0f)] public float flashPopDuration = 0.08f;
    [Tooltip("원래 크기/색으로 돌아오는 시간(초)입니다.")]
    [Min(0f)] public float flashReturnDuration = 0.4f;
    [Tooltip("게임 시작/줄 재구성 직후 이 시간(초) 동안은 숫자가 바뀌어도 연출하지 않습니다.")]
    [Min(0f)] public float flashStartupGrace = 0.5f;

    [Header("간격")]
    [Tooltip("한 줄 안에서 아이콘과 숫자 사이의 가로 간격입니다.")]
    public float iconToValueSpacing = 10f;
    [Tooltip("줄과 줄 사이의 세로 간격입니다.")]
    public float rowSpacing = 8f;

    [Header("열 배치")]
    [Tooltip("한 열에 놓을 줄 수입니다. 이 수를 넘으면 오른쪽 열의 맨 위부터 이어서 놓입니다.")]
    [Min(1)] public int rowsPerColumn = 6;
    [Tooltip("열과 열 사이의 가로 간격입니다(왼쪽 열 숫자칸 끝에서 오른쪽 열 아이콘 시작까지).")]
    public float columnSpacing = 20f;

    [Header("설명 플로팅박스 - 위치")]
    [Tooltip("커서 기준 오프셋입니다(1920x1080 기준 픽셀). x는 좌우, y는 커서보다 얼마나 위에 " +
             "띄울지입니다. 기본값은 커서 바로 위 중앙 정렬입니다.")]
    public Vector2 tooltipCursorOffset = new Vector2(0f, 24f);
    [Tooltip("화면 가장자리와 박스 사이 최소 여백입니다.")]
    public float tooltipScreenEdgeMargin = 16f;

    [Header("설명 플로팅박스 - 모양")]
    [Tooltip("박스 배경 색입니다. 알파(A)로 투명도를 조절합니다(기본: 반투명 검정).")]
    public Color tooltipBackgroundColor = new Color(0f, 0f, 0f, 0.8f);
    [Tooltip("박스 모서리 둥글기(픽셀)입니다. 0이면 각진 네모입니다. 박스가 작으면 자동으로 " +
             "가능한 최대 둥글기로 줄어듭니다.")]
    [Min(0f)] public float tooltipCornerRadius = 12f;
    [Tooltip("박스 안쪽 여백입니다.")]
    public float tooltipPadding = 14f;
    [Tooltip("박스 최대 폭입니다(여백 포함). 설명문이 이보다 길면 줄바꿈됩니다.")]
    public float tooltipMaxWidth = 360f;
    [Tooltip("이름과 설명문 사이의 세로 간격입니다.")]
    public float tooltipNameDescSpacing = 6f;

    [Header("설명 플로팅박스 - 글자")]
    public int tooltipNameFontSize = 26;
    public Color tooltipNameColor = new Color(1f, 0.92f, 0.45f, 1f);
    public int tooltipDescriptionFontSize = 20;
    public Color tooltipDescriptionColor = new Color(0.9f, 0.9f, 0.9f, 1f);

    [Header("설명 플로팅박스 - 정렬 (일반적으로 건드릴 필요 없음)")]
    [Tooltip("다른 HUD보다 항상 위에 그려지도록 하는 정렬 순서입니다. 아이템 툴팁과 같은 20입니다.")]
    public int tooltipCanvasSortingOrder = 20;

    [Header("접어보기 (패널 위쪽 버튼 - 누르면 화면 아래로 접힘)")]
    public CollapsibleHUDSettings collapse = new CollapsibleHUDSettings();

    private class RowRefs
    {
        public RectTransform rowRect;
        public RectTransform iconRect;
        public Image iconImage;
        public RectTransform valueRect;
        public TextMeshProUGUI valueText;
        public StatEntry entry;

        // [수치 변화 연출] 상태
        public float createdTime;       // 줄을 만든 실제 시간(시작 직후 유예용)

        // [긴 숫자 - 자동 축소] 마지막으로 크기를 맞춘 조건과 그 결과(조건이 같으면 다시 재지 않음)
        public string fitText;
        public float fitBaseSize = -1f;
        public float fitWidth = -1f;
        public float fitMinSize = -1f;
        public float fittedFontSize;
        public bool hasBaseline;        // 비교 기준값을 잡았는지
        public string lastText;         // 지난번 화면에 표시한 글자
        public float lastValue;         // 지난번 실제 값(유리/불리 판정용)
        public float flashStartTime = -1f; // 연출 시작 시간(음수면 연출 없음)
        public Color flashColor;
    }

    private GameObject panelRoot;
    private RectTransform panelRect;
    private readonly List<RowRefs> activeRows = new List<RowRefs>();
    private bool built = false;
    private string lastSignature = "";
    private CollapsibleHUDPanel collapser;

    // 설명 플로팅박스 (처음 호버될 때 한 번만 만듭니다)
    private const int RoundedTexSize = 96;
    private const int RoundedTexRadius = 32;
    private GameObject tooltipCanvasGO;
    private Canvas tooltipCanvas;
    private CanvasScaler tooltipScaler;
    private RectTransform tooltipCanvasRect;
    private RectTransform tooltipBoxRect;
    private Image tooltipBoxImage;
    private TextMeshProUGUI tooltipNameText;
    private RectTransform tooltipNameRect;
    private TextMeshProUGUI tooltipDescText;
    private RectTransform tooltipDescRect;
    private Sprite roundedSprite;
    private Texture2D roundedTexture;

    // [강제 연출 요청] 스탯 종류 -> (유리한지, 요청 시간). 숫자가 그대로여도 다음 갱신 때 그 줄을 연출합니다.
    private const float FlashRequestLifetime = 1f;
    private static readonly Dictionary<StatType, KeyValuePair<bool, float>> flashRequests =
        new Dictionary<StatType, KeyValuePair<bool, float>>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        flashRequests.Clear();
    }

    // 숫자가 바뀌지 않아도 이 스탯 줄의 수치 변화 연출을 한 번 보여줍니다(favorable = true면 노랑, false면 빨강).
    // 예: 111번 - 공격력이 하한선에 붙어 화면 숫자는 그대로지만 실제로는 올랐을 때.
    public static void RequestFlash(StatType type, bool favorable)
    {
        flashRequests[type] = new KeyValuePair<bool, float>(favorable, Time.unscaledTime);
    }

    private static bool TryConsumeFlashRequest(StatType type, out bool favorable)
    {
        favorable = true;
        if (!flashRequests.TryGetValue(type, out var request)) return false;
        flashRequests.Remove(type);
        if (Time.unscaledTime - request.Value > FlashRequestLifetime) return false; // 너무 오래된 요청은 버림
        favorable = request.Key;
        return true;
    }

    void Awake()
    {
        EnsureLowerIsBetterDefaults();
    }

    void Update()
    {
        if (!EnsureReferences()) return;

        EnsureBuilt();
        RebuildRowsIfNeeded();
        ApplyLiveValuesAndTweaks();
        Reflow();
        ApplyCollapse();
        UpdateTooltip();
    }

    void OnDisable()
    {
        HideTooltip();
    }

    void OnDestroy()
    {
        if (collapser != null) collapser.Dispose();
        if (roundedSprite != null) Destroy(roundedSprite);
        if (roundedTexture != null) Destroy(roundedTexture);
    }

    private bool EnsureReferences()
    {
        if (hudManager == null)
        {
            hudManager = Object.FindFirstObjectByType<PlayerHUDManager>();
            if (hudManager == null || hudManager.CanvasRoot == null) return false;
        }

        if (playerStats == null)
        {
            playerStats = Object.FindFirstObjectByType<PlayerStats>();
            if (playerStats == null) return false;
        }

        return true;
    }

    // 패널 루트 오브젝트 자체는 한 번만 만듭니다 (배경 이미지 없이, 줄들을 담는
    // 빈 컨테이너 역할만 합니다 - 기획에 배경 패널 요구사항이 없었습니다).
    private void EnsureBuilt()
    {
        if (built) return;

        panelRoot = new GameObject("PlayerStatDisplay_Panel");
        panelRoot.transform.SetParent(hudManager.CanvasRoot, false);

        panelRect = panelRoot.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 0f);
        panelRect.anchorMax = new Vector2(0f, 0f);
        panelRect.pivot = new Vector2(0f, 0f);
        panelRect.sizeDelta = Vector2.zero;

        built = true;
    }

    // "지금 켜진 항목이 어떤 것들이고 순서가 어떤지"를 문자열로 요약해서, 이전 프레임과
    // 달라졌을 때만 줄(Row) 오브젝트를 통째로 새로 만듭니다. 아이콘 스프라이트나
    // 색상/크기는 매 프레임 ApplyLiveValuesAndTweaks()에서 갱신하므로 여기서 신경 쓸
    // 필요 없습니다 - 오직 "몇 줄이 어떤 순서로 있어야 하는가"만 이 서명으로 판단합니다.
    private void RebuildRowsIfNeeded()
    {
        string signature = BuildSignature();
        if (signature == lastSignature) return;
        lastSignature = signature;

        foreach (var row in activeRows)
        {
            if (row.rowRect != null) Destroy(row.rowRect.gameObject);
        }
        activeRows.Clear();

        foreach (var entry in statEntries)
        {
            if (!entry.enabled) continue;
            activeRows.Add(CreateRow(entry));
        }
    }

    private string BuildSignature()
    {
        var sb = new System.Text.StringBuilder();
        foreach (var entry in statEntries)
        {
            if (!entry.enabled) continue;
            sb.Append((int)entry.statType).Append(',');
        }
        return sb.ToString();
    }

    private RowRefs CreateRow(StatEntry entry)
    {
        var rowGO = new GameObject("Row_" + entry.statType);
        rowGO.transform.SetParent(panelRoot.transform, false);
        var rowRect = rowGO.AddComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0f, 0f);
        rowRect.anchorMax = new Vector2(0f, 0f);
        rowRect.pivot = new Vector2(0f, 0f);

        var iconGO = new GameObject("Icon");
        iconGO.transform.SetParent(rowRect, false);
        var iconRect = iconGO.AddComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0f, 0f);
        iconRect.anchorMax = new Vector2(0f, 0f);
        iconRect.pivot = new Vector2(0f, 0f);
        var iconImage = iconGO.AddComponent<Image>();
        iconImage.preserveAspect = true;

        var valueGO = new GameObject("Value");
        valueGO.transform.SetParent(rowRect, false);
        var valueRect = valueGO.AddComponent<RectTransform>();
        valueRect.anchorMin = new Vector2(0f, 0f);
        valueRect.anchorMax = new Vector2(0f, 0f);
        // [수치 변화 연출] 커질 때 왼쪽 끝/세로 가운데 기준으로 커지도록 기준점을 왼쪽 가운데로 둡니다
        // (위치는 Reflow()에서 이 기준점에 맞춰 놓으므로 평소 모습은 예전과 같습니다).
        valueRect.pivot = new Vector2(0f, 0.5f);
        // [TMP] 옛 방식 Text는 화면 배율에 따라 흐릿하게 보여서 TextMeshPro로 교체했습니다(HUDText 참고).
        // 예전엔 세로 Truncate였지만 TMP의 Truncate는 글자를 잘라내므로, 줄 높이와 상관없이 항상 보이는 Overflow를 씁니다.
        var valueText = HUDText.AddTo(valueGO, TextAnchor.MiddleLeft);

        return new RowRefs
        {
            rowRect = rowRect,
            iconRect = iconRect,
            iconImage = iconImage,
            valueRect = valueRect,
            valueText = valueText,
            entry = entry,
            createdTime = Time.unscaledTime,
        };
    }

    // 아이콘/색/폰트 크기/실시간 스탯 수치 - 매 프레임 다시 밀어넣어서 Play 도중 인스펙터
    // 값을 바꿔도 바로바로 반영되게 합니다.
    private void ApplyLiveValuesAndTweaks()
    {
        foreach (var row in activeRows)
        {
            if (row.entry.icon != null)
            {
                row.iconImage.sprite = row.entry.icon;
                row.iconImage.color = Color.white;
            }
            else
            {
                row.iconImage.sprite = null;
                row.iconImage.color = fallbackIconColor;
            }
            row.iconRect.sizeDelta = new Vector2(iconSize, iconSize);

            float value = GetStatValue(playerStats, row.entry.statType);
            // 화면 표시 배율/단위(총알 크기 x100, 확률 스탯 x100 + "%" 등)는 StatDisplayFormat 한 곳에서
            // 정합니다 - 아이템 설명칸(ItemInfoPanelView)과 같은 규칙을 씁니다. 실제 스탯 값은 그대로입니다.
            string text = BuildValueText(row.entry, value);
            DetectValueChange(row, text, value);
            row.valueText.text = text;
            row.valueText.fontSize = GetFittedFontSize(row, text);
            row.valueRect.sizeDelta = new Vector2(valueTextWidth, iconSize);
            ApplyFlash(row);
        }
    }

    // 일반 스탯을 목록 순서대로 왼쪽 열의 맨 위 줄부터 아래로 놓고(Element 0이 가장 왼쪽 위), 한
    // 열이 rowsPerColumn개로 차면 오른쪽 열로 넘어갑니다. 스킬 스탯(isSkillStat)은 일반 스탯의
    // 마지막 열 다음 새 열부터 같은 규칙으로 놓습니다(파일 상단 [스킬 스탯 묶음] 참고). 패널
    // 전체는 계속 화면 좌측 하단 모서리 기준 여백에 붙어 있고, 모든 열의 윗줄은 가장 긴 열의
    // 윗줄에 맞춰집니다.
    private void Reflow()
    {
        panelRect.anchoredPosition = new Vector2(panelMarginX, panelMarginY);

        float rowHeight = Mathf.Max(iconSize, fontSize + 4f);
        float columnWidth = iconSize + iconToValueSpacing + valueTextWidth;
        int perColumn = Mathf.Max(1, rowsPerColumn);

        int generalCount = 0;
        int skillCount = 0;
        foreach (var row in activeRows)
        {
            if (row.entry != null && row.entry.isSkillStat) skillCount++;
            else generalCount++;
        }

        // 일반 묶음이 차지하는 열 수 - 스킬 묶음은 그 다음 열부터 시작합니다.
        int generalColumns = (generalCount + perColumn - 1) / perColumn;

        // 가장 긴 열의 줄 수 - 이 열의 윗줄 높이가 모든 열의 윗줄 기준이 됩니다.
        int tallest = Mathf.Max(Mathf.Min(perColumn, generalCount), Mathf.Min(perColumn, skillCount));
        int topSlot = tallest - 1;

        int generalIndex = 0;
        int skillIndex = 0;
        for (int i = 0; i < activeRows.Count; i++)
        {
            var row = activeRows[i];
            bool isSkill = row.entry != null && row.entry.isSkillStat;

            int orderInGroup = isSkill ? skillIndex++ : generalIndex++;
            int column = (isSkill ? generalColumns : 0) + orderInGroup / perColumn;
            int indexInColumn = orderInGroup % perColumn;

            float x = column * (columnWidth + columnSpacing);
            float y = (topSlot - indexInColumn) * (rowHeight + rowSpacing);

            row.rowRect.anchoredPosition = new Vector2(x, y);
            row.rowRect.sizeDelta = new Vector2(columnWidth, rowHeight);

            row.iconRect.anchoredPosition = new Vector2(0f, (rowHeight - iconSize) * 0.5f);
            // 숫자 칸 기준점이 왼쪽 가운데라 줄 높이의 가운데에 둡니다(예전 왼쪽 아래 기준 위치와 같은 자리).
            row.valueRect.anchoredPosition = new Vector2(iconSize + iconToValueSpacing, rowHeight * 0.5f);
        }

        // [접어보기] 패널 크기를 실제 내용 크기로 채웁니다(빠지는 거리/버튼 위치 계산용 - 배치에는 영향 없음).
        int skillColumns = (skillCount + perColumn - 1) / perColumn;
        int totalColumns = generalColumns + skillColumns;
        float contentWidth = totalColumns > 0 ? totalColumns * columnWidth + (totalColumns - 1) * columnSpacing : 0f;
        float contentHeight = tallest > 0 ? tallest * rowHeight + (tallest - 1) * rowSpacing : 0f;
        panelRect.sizeDelta = new Vector2(contentWidth, contentHeight);
    }

    // [접어보기] 원래 위치(좌측 하단 여백)를 넘겨 접힘 이동량(아래 방향)이 더해진 위치로 둡니다.
    private void ApplyCollapse()
    {
        if (collapser == null) collapser = new CollapsibleHUDPanel("StatPanel", HUDCollapseDirection.Down);
        collapser.Tick(collapse, panelRect, panelRect.sizeDelta, panelMarginY, new Vector2(panelMarginX, panelMarginY));
    }

    // ------------------------------------------------------------------
    // 설명 플로팅박스
    // ------------------------------------------------------------------

    // 커서가 올라가 있는 스탯 아이콘(이름이 적힌 것만)을 찾아 박스를 띄우고, 없으면 숨깁니다.
    private void UpdateTooltip()
    {
        StatEntry hovered = FindHoveredEntry();
        if (hovered == null)
        {
            HideTooltip();
            return;
        }

        EnsureTooltipBuilt();
        UpdateTooltipScalerMatch();
        ShowTooltip(hovered);
    }

    private StatEntry FindHoveredEntry()
    {
        Camera cam = null;
        var hudCanvas = hudManager.CanvasRoot != null ? hudManager.CanvasRoot.GetComponentInParent<Canvas>() : null;
        if (hudCanvas != null && hudCanvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            cam = hudCanvas.rootCanvas.worldCamera;

        Vector2 mouse = Input.mousePosition;
        foreach (var row in activeRows)
        {
            if (row.entry == null || string.IsNullOrEmpty(row.entry.tooltipName)) continue;
            if (row.iconRect == null || !row.iconRect.gameObject.activeInHierarchy) continue;
            if (RectTransformUtility.RectangleContainsScreenPoint(row.iconRect, mouse, cam)) return row.entry;
        }
        return null;
    }

    private void HideTooltip()
    {
        if (tooltipCanvasGO != null && tooltipCanvasGO.activeSelf) tooltipCanvasGO.SetActive(false);
    }

    private void EnsureTooltipBuilt()
    {
        if (tooltipCanvasGO != null) return;

        tooltipCanvasGO = new GameObject("PlayerStatTooltipCanvas");
        tooltipCanvasGO.transform.SetParent(transform, false);
        tooltipCanvas = tooltipCanvasGO.AddComponent<Canvas>();
        tooltipCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        tooltipCanvas.sortingOrder = tooltipCanvasSortingOrder;

        tooltipScaler = tooltipCanvasGO.AddComponent<CanvasScaler>();
        tooltipScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        tooltipCanvasRect = tooltipCanvasGO.GetComponent<RectTransform>();

        var boxGO = new GameObject("Box");
        boxGO.transform.SetParent(tooltipCanvasGO.transform, false);
        tooltipBoxRect = boxGO.AddComponent<RectTransform>();
        tooltipBoxRect.anchorMin = tooltipBoxRect.anchorMax = new Vector2(0.5f, 0.5f);
        tooltipBoxRect.pivot = new Vector2(0.5f, 0f); // 아래쪽 중앙이 기준(커서 바로 위에 붙이기 위해)
        tooltipBoxImage = boxGO.AddComponent<Image>();
        tooltipBoxImage.raycastTarget = false;

        var nameGO = new GameObject("Name");
        nameGO.transform.SetParent(boxGO.transform, false);
        tooltipNameRect = nameGO.AddComponent<RectTransform>();
        tooltipNameRect.anchorMin = tooltipNameRect.anchorMax = new Vector2(0f, 1f);
        tooltipNameRect.pivot = new Vector2(0f, 1f);
        // [TMP] 옛 방식 Text는 화면 배율에 따라 흐릿하게 보여서 TextMeshPro로 교체했습니다(HUDText 참고).
        tooltipNameText = HUDText.AddTo(nameGO, TextAnchor.UpperLeft, true);
        tooltipNameText.raycastTarget = false;

        var descGO = new GameObject("Description");
        descGO.transform.SetParent(boxGO.transform, false);
        tooltipDescRect = descGO.AddComponent<RectTransform>();
        tooltipDescRect.anchorMin = tooltipDescRect.anchorMax = new Vector2(0f, 1f);
        tooltipDescRect.pivot = new Vector2(0f, 1f);
        tooltipDescText = HUDText.AddTo(descGO, TextAnchor.UpperLeft, true);
        tooltipDescText.raycastTarget = false;

        tooltipCanvasGO.SetActive(false);
    }

    // HUD와 같은 기준 해상도/match 방식을 매 프레임 그대로 따라갑니다(ItemTooltipDisplay와 동일).
    private void UpdateTooltipScalerMatch()
    {
        Vector2 refRes = hudManager.referenceResolution;
        tooltipScaler.referenceResolution = refRes;
        tooltipCanvas.sortingOrder = tooltipCanvasSortingOrder;

        if (refRes.y <= 0f || Screen.height <= 0f) return;
        float screenAspect = (float)Screen.width / Screen.height;
        float referenceAspect = refRes.x / refRes.y;
        tooltipScaler.matchWidthOrHeight = screenAspect > referenceAspect ? 1f : 0f;
    }

    private void ShowTooltip(StatEntry entry)
    {
        if (!tooltipCanvasGO.activeSelf) tooltipCanvasGO.SetActive(true);

        // 글자 내용/크기/색
        tooltipNameText.text = entry.tooltipName;
        tooltipNameText.fontSize = tooltipNameFontSize;
        tooltipNameText.color = tooltipNameColor;

        bool hasDesc = !string.IsNullOrEmpty(entry.tooltipDescription);
        tooltipDescText.gameObject.SetActive(hasDesc);
        tooltipDescText.text = hasDesc ? entry.tooltipDescription : "";
        tooltipDescText.fontSize = tooltipDescriptionFontSize;
        tooltipDescText.color = tooltipDescriptionColor;

        // 폭 계산: 줄바꿈 없이 한 줄로 썼을 때의 폭 중 가장 큰 값을 쓰되 최대 폭을 넘지 않게 합니다.
        float pad = Mathf.Max(0f, tooltipPadding);
        float maxContentW = Mathf.Max(20f, tooltipMaxWidth - pad * 2f);

        // (TMP의 preferredWidth도 예전 Text처럼 줄바꿈 없이 한 줄로 썼을 때의 폭이지만, 확실하게 하려고 잠시 줄바꿈을 끄고 잽니다.)
        tooltipNameText.textWrappingMode = TextWrappingModes.NoWrap;
        float nameW = tooltipNameText.preferredWidth;
        tooltipNameText.textWrappingMode = TextWrappingModes.Normal;

        float descW = 0f;
        if (hasDesc)
        {
            tooltipDescText.textWrappingMode = TextWrappingModes.NoWrap;
            descW = tooltipDescText.preferredWidth;
            tooltipDescText.textWrappingMode = TextWrappingModes.Normal;
        }

        float contentW = Mathf.Min(maxContentW, Mathf.Max(nameW, descW) + 2f); // +2: 반올림 오차로 마지막 글자가 다음 줄로 넘어가는 것 방지

        // 높이 계산: 폭이 정해진 뒤에 줄바꿈이 반영된 높이를 읽습니다.
        tooltipNameRect.sizeDelta = new Vector2(contentW, 1000f);
        float nameH = tooltipNameText.preferredHeight;

        float descH = 0f;
        if (hasDesc)
        {
            tooltipDescRect.sizeDelta = new Vector2(contentW, 1000f);
            descH = tooltipDescText.preferredHeight;
        }

        float gap = hasDesc ? tooltipNameDescSpacing : 0f;
        float boxW = contentW + pad * 2f;
        float boxH = pad + nameH + gap + descH + pad;

        tooltipNameRect.sizeDelta = new Vector2(contentW, nameH);
        tooltipNameRect.anchoredPosition = new Vector2(pad, -pad);
        if (hasDesc)
        {
            tooltipDescRect.sizeDelta = new Vector2(contentW, descH);
            tooltipDescRect.anchoredPosition = new Vector2(pad, -(pad + nameH + gap));
        }

        tooltipBoxRect.sizeDelta = new Vector2(boxW, boxH);
        ApplyTooltipBackground(boxW, boxH);

        // 위치: 커서 위 중앙, 화면 안쪽으로 보정
        RectTransformUtility.ScreenPointToLocalPointInRectangle(tooltipCanvasRect, Input.mousePosition, null, out Vector2 cursorLocal);
        Rect cr = tooltipCanvasRect.rect;
        float margin = tooltipScreenEdgeMargin;

        float px = cursorLocal.x + tooltipCursorOffset.x;
        float py = cursorLocal.y + tooltipCursorOffset.y; // 박스 아래쪽 끝의 y

        float minX = cr.xMin + margin + boxW * 0.5f;
        float maxX = cr.xMax - margin - boxW * 0.5f;
        if (maxX < minX) maxX = minX;
        px = Mathf.Clamp(px, minX, maxX);

        float minY = cr.yMin + margin;
        float maxY = cr.yMax - margin - boxH;
        if (maxY < minY) maxY = minY;
        py = Mathf.Clamp(py, minY, maxY);

        tooltipBoxRect.anchoredPosition = new Vector2(px, py);
    }

    // 배경 색과 모서리 둥글기를 적용합니다. 이미지 없이 실행 중에 만든 둥근 네모 스프라이트를
    // 9-Slice(Sliced)로 늘려 씁니다 - 네 모서리의 반지름은 유지되고 가운데만 늘어납니다.
    private void ApplyTooltipBackground(float boxW, float boxH)
    {
        tooltipBoxImage.color = tooltipBackgroundColor;

        // 박스 크기가 반지름의 두 배보다 작으면 반지름이 찌그러지지 않도록 줄입니다.
        float radius = Mathf.Min(tooltipCornerRadius, Mathf.Min(boxW, boxH) * 0.5f);
        if (radius < 0.5f)
        {
            tooltipBoxImage.sprite = null;
            tooltipBoxImage.type = Image.Type.Simple;
            return;
        }

        EnsureRoundedSprite();
        tooltipBoxImage.sprite = roundedSprite;
        tooltipBoxImage.type = Image.Type.Sliced;
        // 스프라이트 모서리(RoundedTexRadius 텍셀)가 화면에서 radius(캔버스 픽셀)로 보이도록 배율을 맞춥니다.
        tooltipBoxImage.pixelsPerUnitMultiplier = RoundedTexRadius / radius;
    }

    // 모서리가 RoundedTexRadius 텍셀인 둥근 네모(흰색)를 한 번만 생성합니다. 가장자리는
    // 안티앨리어싱(부드럽게)해서 계단 현상이 없게 합니다.
    private void EnsureRoundedSprite()
    {
        if (roundedSprite != null) return;

        int size = RoundedTexSize;
        float r = RoundedTexRadius;
        roundedTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        roundedTexture.wrapMode = TextureWrapMode.Clamp;
        roundedTexture.filterMode = FilterMode.Bilinear;

        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // 픽셀 중심에서 모서리 원의 중심까지의 거리로 바깥쪽 여부를 판정합니다.
                float cx = Mathf.Clamp(x + 0.5f, r, size - r);
                float cy = Mathf.Clamp(y + 0.5f, r, size - r);
                float dx = x + 0.5f - cx;
                float dy = y + 0.5f - cy;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = Mathf.Clamp01(r - dist + 0.5f);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
            }
        }
        roundedTexture.SetPixels32(pixels);
        roundedTexture.Apply();

        roundedSprite = Sprite.Create(
            roundedTexture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f),
            100f, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
    }

    // ------------------------------------------------------------------
    // 수치 변화 연출
    // ------------------------------------------------------------------

    // 표시 글자가 바뀌었으면 연출을 시작합니다. 실제 값이 같은데 글자만 바뀐 경우(소수점 표시 체크를 바꾼 경우 등)는
    // 연출하지 않습니다.
    private void DetectValueChange(RowRefs row, string text, float value)
    {
        bool inGrace = Time.unscaledTime - row.createdTime < flashStartupGrace;

        if (row.hasBaseline && flashOnChange && !inGrace && text != row.lastText && !Mathf.Approximately(value, row.lastValue))
        {
            bool increased = value > row.lastValue;
            bool better = row.entry.lowerIsBetter ? !increased : increased;
            row.flashColor = better ? increaseFlashColor : decreaseFlashColor;
            row.flashStartTime = Time.unscaledTime;
        }

        // [강제 연출 - 111번] 숫자가 그대로여도 요청받은 스탯이면 요청한 색으로 연출합니다(숫자가 바뀌어 위에서 이미
        // 시작했으면 요청한 색으로 맞춰 다시 시작 - 같은 순간이라 겉보기엔 한 번).
        if (row.hasBaseline && flashOnChange && !inGrace && TryConsumeFlashRequest(row.entry.statType, out bool favorable))
        {
            row.flashColor = favorable ? increaseFlashColor : decreaseFlashColor;
            row.flashStartTime = Time.unscaledTime;
        }

        row.hasBaseline = true;
        row.lastText = text;
        row.lastValue = value;
    }

    // [긴 숫자 - 소수점 숨김] 화면에 보일 글자를 만듭니다. 화면에 보일 값(표시 배율 적용 후)의 크기가 hideDecimalAtOrAbove
    // 이상이면 소수 자리를 버리고(반올림 아님 - 112.9도 112) 정수로만 보여줍니다. 글자만 바뀌고 실제 값은 그대로입니다.
    private string BuildValueText(StatEntry entry, float value)
    {
        bool showDecimal = entry.showDecimal;
        float shownValue = value;

        if (showDecimal && hideDecimalAtOrAbove > 0f)
        {
            float multiplier = StatDisplayFormat.GetMultiplier(entry.statType);
            float shown = value * multiplier;
            if (Mathf.Abs(shown) >= hideDecimalAtOrAbove)
            {
                showDecimal = false;
                // 버린 정수를 다시 실제 값 기준으로 되돌려 넘깁니다(FormatValue가 표시 배율을 다시 곱하므로).
                float truncated = (float)System.Math.Truncate(shown);
                shownValue = multiplier != 0f ? truncated / multiplier : truncated;
            }
        }

        return StatDisplayFormat.FormatValue(entry.statType, shownValue, showDecimal ? "0.#" : "0");
    }

    // 자동 축소 때 오른쪽 열 아이콘과 숫자 사이에 남겨둘 최소 여유(px)입니다.
    private const float ValueFitSafetyMargin = 4f;

    // 숫자가 실제로 쓸 수 있는 폭입니다 - 숫자 시작점부터 오른쪽 열 아이콘이 시작하는 곳까지(파일 상단 [긴 숫자 처리] 참고).
    private float GetAvailableValueWidth()
    {
        float width = valueTextWidth + Mathf.Min(0f, columnSpacing) - ValueFitSafetyMargin;
        return Mathf.Max(1f, width);
    }

    // [긴 숫자 - 자동 축소] 이 줄에 쓸 글자 크기를 돌려줍니다. 기본 크기(fontSize)로 그렸을 때의 실제 폭이 쓸 수 있는 폭
    // (GetAvailableValueWidth)보다 넓으면 그 비율만큼 줄이고, minValueFontSize 아래로는 줄이지 않습니다. 글자/기본 크기/
    // 쓸 수 있는 폭/최소 크기가 지난번과 같으면 다시 재지 않고 저장해 둔 값을 씁니다.
    private float GetFittedFontSize(RowRefs row, string text)
    {
        float baseSize = fontSize;
        if (!autoShrinkValueText || valueTextWidth <= 0f) return baseSize;

        float available = GetAvailableValueWidth();

        if (row.fitText == text && Mathf.Approximately(row.fitBaseSize, baseSize) &&
            Mathf.Approximately(row.fitWidth, available) && Mathf.Approximately(row.fitMinSize, minValueFontSize))
        {
            return row.fittedFontSize;
        }

        float fitted = baseSize;
        row.valueText.fontSize = baseSize; // 기본 크기 기준으로 폭을 잽니다.
        float preferredWidth = row.valueText.GetPreferredValues(text, float.PositiveInfinity, float.PositiveInfinity).x;
        if (preferredWidth > available && preferredWidth > 0f)
        {
            fitted = Mathf.Max(Mathf.Min(minValueFontSize, baseSize), baseSize * (available / preferredWidth));
        }

        row.fitText = text;
        row.fitBaseSize = baseSize;
        row.fitWidth = available;
        row.fitMinSize = minValueFontSize;
        row.fittedFontSize = fitted;
        return fitted;
    }

    // 연출 진행에 맞춰 숫자 크기/색을 정합니다(연출이 없으면 원래 크기/색).
    private void ApplyFlash(RowRefs row)
    {
        float scale = 1f;
        Color color = valueTextColor;

        if (row.flashStartTime >= 0f)
        {
            float elapsed = Time.unscaledTime - row.flashStartTime;
            float pop = Mathf.Max(0f, flashPopDuration);
            float ret = Mathf.Max(0f, flashReturnDuration);

            if (!flashOnChange || elapsed >= pop + ret)
            {
                row.flashStartTime = -1f; // 끝 - 원래 모습으로
            }
            else if (elapsed < pop)
            {
                // 커지는 단계: 빠르게 시작해서 부드럽게 멈춤(ease-out)
                float t = pop > 0f ? elapsed / pop : 1f;
                float eased = 1f - (1f - t) * (1f - t);
                scale = Mathf.Lerp(1f, flashPeakScale, eased);
                color = Color.Lerp(valueTextColor, row.flashColor, eased);
            }
            else
            {
                // 돌아오는 단계: 부드럽게 시작하고 부드럽게 멈춤(ease-in-out)
                float t = ret > 0f ? (elapsed - pop) / ret : 1f;
                float eased = t * t * (3f - 2f * t);
                scale = Mathf.Lerp(flashPeakScale, 1f, eased);
                color = Color.Lerp(row.flashColor, valueTextColor, eased);
            }
        }

        row.valueText.color = color;
        row.valueRect.localScale = new Vector3(scale, scale, 1f);
    }

    // statEntries의 "Lower Is Better"를 스탯 종류에 맞춰 처음 한 번만 채웁니다(이미 채운 항목은 건드리지 않음).
    private void EnsureLowerIsBetterDefaults()
    {
        if (statEntries == null) return;
        foreach (var entry in statEntries)
        {
            if (entry == null || entry.lowerIsBetterInitialized) continue;
            entry.lowerIsBetter = IsLowerBetterByDefault(entry.statType);
            entry.lowerIsBetterInitialized = true;
        }
    }

    // 숫자가 작을수록 플레이어에게 유리한 스탯의 기본 목록입니다.
    private static bool IsLowerBetterByDefault(StatType type)
    {
        return type == StatType.ReloadTime ||
               type == StatType.MultishotInterval ||
               type == StatType.GrenadeSelfDamage;
    }

    void OnValidate()
    {
        EnsureLowerIsBetterDefaults();
    }

    // PlayerStats의 최종값(아이템 효과까지 반영된 값)을 StatType으로 조회합니다.
    // PlayerItemInfoDisplay.cs의 FormatStatModifiers()와 비슷한 매핑이지만, 그쪽은
    // "아이템 하나의 증감분"을 보여주고 여긴 "현재 최종 수치"를 보여준다는 점이 다릅니다.
    private static float GetStatValue(PlayerStats s, StatType type)
    {
        switch (type)
        {
            case StatType.MaxHealth: return s.MaxHealth;
            case StatType.MoveSpeed: return s.MoveSpeed;
            case StatType.AttackSpeed: return s.AttackSpeed;
            case StatType.ReloadTime: return s.ReloadTime;
            case StatType.Luck: return s.Luck;
            case StatType.CostRecovery: return s.CostRecovery;
            case StatType.Damage: return s.Damage;
            case StatType.Range: return s.Range;
            case StatType.MagazineSize: return s.MagazineSize;
            case StatType.BulletSize: return s.BulletSize;
            case StatType.BulletSpeed: return s.BulletSpeed;
            case StatType.Multishot: return s.Multishot;
            case StatType.MultishotInterval: return s.MultishotInterval;
            case StatType.GematriaChance: return s.GematriaChance;
            case StatType.SchaleChance: return s.SchaleChance;
            case StatType.GrenadeDamage: return s.GrenadeDamage;
            case StatType.GrenadeSelfDamage: return s.GrenadeSelfDamage;
            case StatType.GrenadeRadius: return s.GrenadeRadius;
            case StatType.GrenadeRange: return s.GrenadeRange;
            case StatType.HackDamage: return s.HackDamage;
            case StatType.HackRadius: return s.HackRadius;
            case StatType.HackDuration: return s.HackDuration;
            default: return 0f;
        }
    }
}