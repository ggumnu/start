using UnityEngine;

// [공용 팝업/버튼 스타일 에셋 (형빈 확정)]
// 게임 안의 모든 팝업 창과 버튼이 같은 색/모서리를 쓰도록 한 곳에 모아둔 설정입니다. 여기 값을 바꾸면 아래 UI가 모두 같이 바뀝니다.
// - 검은 반투명 팝업: 슬롯 교체 확인창(SlotReplaceConfirmUI), 슬롯 버리기 확인/버릴 수 없음 창(SlotItemDropUI), 상점 구매/갱신 팝업
//   (ShopUI), 게임 오버 창(GameOverUI), 다음 레벨 이동 확인창(LevelTransitionConfirmUI)
// - 버튼: 위 팝업들의 버튼 + 흰 창의 버튼(상점 하단 [갱신하기]/[나가기], 슬롯 버리기 첫 창 [버리기]/[취소하기])
// - 위험 버튼(빨강): 되돌릴 수 없는 동작 버튼 - ESC 중단 화면의 [재시작하기]/[그만하기]와 그 확인창의 실행 버튼
// 앞으로 새 UI를 만들 때도 이 에셋의 값을 읽어 씁니다. 버튼/창의 크기와 글자 크기는 UI마다 다르므로 각 UI(또는 그 설정 에셋)에 남아 있습니다.
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

    [Header("검은 반투명 팝업 창")]
    [Tooltip("검은 팝업 창 바탕색입니다(검정 90%).")]
    public Color darkPanelColor = new Color(0f, 0f, 0f, 0.9f);
    [Tooltip("검은 팝업 창의 모서리 둥글기입니다(1920x1080 기준 px).")]
    [Min(0f)] public float darkPanelCornerRadius = 16f;
    [Tooltip("검은 팝업 창 안의 버튼 모서리 둥글기입니다. 흰 창의 버튼은 각 UI의 값을 씁니다(버튼 크기가 달라서).")]
    [Min(0f)] public float darkPanelButtonCornerRadius = 10f;

    [Header("버튼 (모든 팝업/창 공통)")]
    [Tooltip("평소 버튼 색입니다(#8FE2FD).")]
    public Color buttonColor = new Color32(0x8F, 0xE2, 0xFD, 0xFF);
    [Tooltip("마우스를 올렸을 때 버튼 색입니다(#65ADC7).")]
    public Color buttonHoverColor = new Color32(0x65, 0xAD, 0xC7, 0xFF);
    [Tooltip("버튼 글자 색입니다(#343365).")]
    public Color buttonTextColor = new Color32(0x34, 0x33, 0x65, 0xFF);
    [Tooltip("못 누르는 버튼의 불투명도입니다(버튼 색과 글자 색 모두에 곱함).")]
    [Range(0f, 1f)] public float disabledButtonAlpha = 0.35f;

    [Header("위험 버튼 (되돌릴 수 없는 동작 - ESC 중단 화면의 [재시작하기]/[그만하기], 그 확인창의 실행 버튼)")]
    [Tooltip("위험 버튼 평소 색입니다(#E5484D).")]
    public Color dangerButtonColor = new Color32(0xE5, 0x48, 0x4D, 0xFF);
    [Tooltip("위험 버튼에 마우스를 올렸을 때 색입니다(#B8383C).")]
    public Color dangerButtonHoverColor = new Color32(0xB8, 0x38, 0x3C, 0xFF);
    [Tooltip("위험 버튼 글자 색입니다(흰색).")]
    public Color dangerButtonTextColor = Color.white;

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
