using System;
using System.Collections.Generic;
using UnityEngine;

// [조작법] 창(ControlsUI)의 문구/키 목록/크기 설정입니다. Assets/Resources/ControlsUISettings 에 두면 자동으로 읽고, 없으면 아래 기본값.
// 창 바탕/모서리/무늬/글자 색/버튼 색은 PopupStyle을 그대로 씁니다(흰 창 + 모서리 무늬, [닫기] = 하늘색 버튼).
// 만들기: Project 창에서 Assets/Resources 폴더 우클릭 > Create > MobArchive > Controls UI Settings, 이름을 ControlsUISettings로.
[CreateAssetMenu(menuName = "MobArchive/Controls UI Settings", fileName = "ControlsUISettings")]
public class ControlsUISettings : ScriptableObject
{
    public const string ResourcePath = "ControlsUISettings";

    [Serializable]
    public class Row
    {
        [Tooltip("왼쪽에 표시할 동작 이름입니다.")]
        public string action;
        [Tooltip("오른쪽 남색 키 배지에 들어갈 글자입니다. 여러 개면 배지가 나란히 놓이고 사이에 Key Separator 글자가 들어갑니다.")]
        public List<string> keys = new List<string>();

        public Row() { }
        public Row(string action, params string[] keys)
        {
            this.action = action;
            this.keys = new List<string>(keys);
        }
    }

    [Serializable]
    public class Section
    {
        [Tooltip("묶음 이름(하늘빛 작은 글자 + 오른쪽 가는 줄)입니다. 비우면 묶음 이름 줄 없이 항목만 놓입니다.")]
        public string title;
        public List<Row> rows = new List<Row>();

        public Section() { }
        public Section(string title, params Row[] rows)
        {
            this.title = title;
            this.rows = new List<Row>(rows);
        }
    }

    [Header("문구")]
    public string title = "조작법";
    public string closeLabel = "닫기";
    [Tooltip("한 줄에 키 배지가 여러 개일 때 배지 사이에 넣는 글자입니다.")]
    public string keySeparator = "/";

    [Header("키 목록 (창을 열 때마다 다시 읽음)")]
    public List<Section> sections = new List<Section>
    {
        new Section("기본",
            new Row("이동", "W A S D"),
            new Row("사격", "좌클릭"),
            new Row("조준", "우클릭 꾹"),
            new Row("재장전", "R"),
            new Row("상호작용", "F")),
        new Section("무기 전환",
            new Row("총 모드", "1"),
            new Row("수류탄 모드", "2"),
            new Row("해킹툴 모드", "3")),
        new Section("아이템",
            new Row("액티브 아이템", "Space"),
            new Row("일회용 아이템", "Q"),
            new Row("음식", "E"),
            new Row("슬롯 아이템 버리기", "Ctrl 꾹")),
        new Section("화면",
            new Row("HUD 접기 / 펴기", "Tab"),
            new Row("일시 정지", "ESC"),
            new Row("재시작", "R 2초 꾹")),
    };

    [Header("창 (1920x1080 기준 px)")]
    public Vector2 panelSize = new Vector2(760f, 820f);
    public int titleFontSize = 36;
    [Tooltip("표시 순서입니다(시작 화면/도감보다 위, 로딩 화면 1000보다 아래). 게임 중에는 중단 화면보다 위로 자동으로 올라갑니다.")]
    public int canvasSortingOrder = 150;
    [Tooltip("창 뒤 화면을 가리는 막 색입니다(뒤의 시작 화면 버튼도 이 막이 막음). 게임 중 중단 화면에서 열 때는 중단 화면의 막을 그대로 씁니다.")]
    public Color dimColor = new Color(0f, 0f, 0f, 0.5f);
    [Tooltip("제목 아래 가는 줄 색입니다.")]
    public Color dividerColor = new Color32(0xD6, 0xE4, 0xF0, 0xFF);

    [Header("목록 (스크롤 영역)")]
    [Tooltip("창 왼쪽/오른쪽 끝에서 목록까지의 여백입니다.")]
    public float sidePadding = 60f;
    [Tooltip("동작 이름 칸의 오른쪽 끝 위치입니다(창 왼쪽 끝 기준). 동작 이름은 여기에 오른쪽 정렬됩니다.")]
    public float labelColumnRight = 330f;
    [Tooltip("동작 이름과 키 배지 사이 간격입니다.")]
    public float labelToKeyGap = 40f;
    public int sectionFontSize = 22;
    public Color sectionColor = new Color32(0x5B, 0x9B, 0xC4, 0xFF);
    public Color sectionLineColor = new Color32(0xDE, 0xEA, 0xF4, 0xFF);
    public int labelFontSize = 25;
    [Tooltip("한 줄(동작 + 키 배지)의 높이입니다.")]
    public float rowHeight = 58f;
    [Tooltip("묶음 이름 줄의 높이입니다.")]
    public float sectionHeaderHeight = 50f;
    [Tooltip("묶음과 묶음 사이 추가 간격입니다.")]
    public float sectionSpacing = 14f;

    [Header("키 배지 (남색 평행사변형 - 모서리/기울기는 PopupStyle의 아이템 이름 판/버튼과 같음)")]
    public Color keyBadgeColor = new Color32(0x2C, 0x45, 0x63, 0xFF);
    public Color keyTextColor = Color.white;
    public int keyFontSize = 22;
    public float keyBadgeHeight = 44f;
    [Tooltip("배지의 가장 작은 폭입니다(R, F처럼 짧은 키도 이 폭).")]
    public float keyBadgeMinWidth = 120f;
    [Tooltip("배지 안 글자 양옆 여백입니다.")]
    public float keyBadgePaddingX = 22f;
    [Tooltip("배지 여러 개 사이 간격입니다(가운데에 Key Separator 글자).")]
    public float keyBadgeGap = 32f;

    [Header("스크롤")]
    [Tooltip("마우스 휠 한 칸에 움직이는 거리입니다.")]
    public float wheelStep = 80f;
    [Tooltip("스크롤이 목표 위치로 따라가는 빠르기입니다(클수록 즉시).")]
    [Min(1f)] public float scrollSmoothing = 18f;
    public float scrollbarWidth = 8f;
    [Tooltip("스크롤 막대와 창 오른쪽 끝 사이 거리입니다.")]
    public float scrollbarRightMargin = 26f;
    public Color scrollbarTrackColor = new Color32(0xE2, 0xEC, 0xF4, 0xFF);
    public Color scrollbarThumbColor = new Color32(0x8F, 0xE2, 0xFD, 0xFF);
    public Color scrollbarThumbHoverColor = new Color32(0x65, 0xAD, 0xC7, 0xFF);

    [Header("닫기 버튼")]
    public Vector2 buttonSize = new Vector2(160f, 50f);
    public float buttonBottomMargin = 26f;
    public int buttonFontSize = 24;

    private static ControlsUISettings cached;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlayStart()
    {
        cached = null;
    }

    public static ControlsUISettings Get()
    {
        if (cached == null) cached = Resources.Load<ControlsUISettings>(ResourcePath);
        if (cached == null) cached = CreateInstance<ControlsUISettings>();
        return cached;
    }
}
