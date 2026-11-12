using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 상점 창입니다. 상인(ShopNPC) 앞에서 F를 누르면 열립니다.
//
// [구성 - 형빈 기획]
// - 상점 틀(흰 창 + 모서리 무늬 - 공용 부품 UIPatternBox) 안에 카드 8칸(한 줄 4칸 x 2줄). 진열 목록(ShopNPC.Listings)의 i번째 상품이 i번째 칸에
//   놓이고, 상품이 없는 칸은 빈 공간으로 둡니다. 플레이어 아이템을 산 칸은 카드를 그대로 두고 반투명 검은 덮개를 씌워 누를 수
//   없게 합니다(형빈 확정).
// - 카드(블루 아카이브 상점식 - 모두 같은 기울기의 둥근 평행사변형, 공용 부품 UIPatternBox):
//   연회색 그라데이션 카드 + 모서리 무늬 + 그림자. 맨 위 이름(PopupStyle 글자 색) → 아이콘 틀(등급별 대각선 그라데이션 + 흰 무늬)
//   → 가격표(하늘색 버튼 + 무늬: 재화 아이콘 + 남색 평행사변형 가격 칸 / "구매하기"). 안쪽 요소는 카드의 기울어진 변과 평행하게
//   자동 정렬됩니다(카드 기울기 = Card Slant Angle). 가격표는 호버 시 어두워지고, 누르는 동안 작아지며, 뗀 순간 구매 팝업이 열립니다.
// - 묶음 상품(1회 구매 시 2개 이상)만 아이콘 틀 오른쪽 아래에 "x5"를 표시합니다(블루 아카이브식 "x1"/"N회 구매 가능" 띠는 안 씀 - 형빈 확정).
// - 등급 구분은 이름 색 대신 아이콘 틀로 합니다(형빈 확정): 소모성 재화는 소모품 색(Tier0과 같은 회색), 플레이어 아이템은 등급별 색.
// - 오른쪽 아래 [갱신하기](하늘색, 횟수를 다 쓰면 반투명 비활성) / [나가기](회색). Esc도 나가기와 같습니다.
//
// [구매 팝업 - 3단계]
// [구매하기]를 누르면 흰 팝업이 뜹니다(뒤의 상점 창은 막으로 한 번 어둡게): 윗줄 [아이템 이름 판(등급 색, 소모품은 흰색)] /
// 아랫줄 "구매하시겠습니까?" → [-] 수량 [+](흰 둥근 사각형 + 그림자) → [재화 아이콘 + 합계 가격 / 구매하기](노랑) [아니오](하늘색).
// - 수량: 소모성 재화만 바꿀 수 있습니다(플레이어 아이템은 1개 고정 - [-]/[+]가 흐리게 표시). 현재 체력은 깎인 체력만큼까지.
// - 합계 가격 = 1회 가격 x 수량. 재화가 부족하면 가격 숫자가 빨갛게 되고 [구매하기]가 눌리지 않습니다. 재화 말고 다른 이유
//   (체력 가득/이미 보유 등)로 살 수 없으면 그 이유를 진한 빨간 글씨로 보여주고 역시 눌리지 않습니다.
// - [아니오]/Esc = 팝업만 닫고 상점 창으로 돌아갑니다. 팝업이 떠 있는 동안 상점 창은 클릭에 반응하지 않습니다.
// - 판정/거래는 ShopNPC(CheckPurchase/TryPurchase)가 합니다.
// - 슬롯 아이템(4단계): 장착 중인 아이템이 바뀌면 원래 아이템은 상점의 빈 보관 좌대로 갑니다. 빈 좌대가 없으면 안내 문구와 함께
//   살 수 없습니다. 바뀌는 아이템에 "교체 시 확인"이 체크돼 있으면 슬롯 교체 확인창을 먼저 띄우고, 그 창이 떠 있는 동안 상점 창은
//   입력을 받지 않습니다.
//
// [갱신 팝업 - 5단계]
// [갱신하기]를 누르면 흰 팝업 "1 크레딧으로 갱신하시겠습니까?" + [갱신하기](노랑) [아니오](하늘색)가 뜹니다(가격/재화는 Shop Config).
// 재화가 부족하거나 새로 진열할 아이템이 없으면 이유를 빨간 글씨로 보여주고 [갱신하기]가 눌리지 않습니다. [아니오]/Esc = 팝업만 닫힘.
// 갱신 횟수를 다 쓰면 상점 창의 [갱신하기] 버튼이 어두워지고 눌리지 않습니다. 판정/처리는 ShopNPC(CheckRefresh/TryRefresh).
//
// [흐르는 이름]
// 카드 이름은 글자 크기를 그대로 두고, 칸보다 길면 왼쪽으로 천천히 흐르며 끝까지 보여준 뒤
// 살짝 사라졌다가 처음 위치에서 다시 나타납니다(반복). 칸 밖 글자는 잘려서 카드를 뚫고 나가지 않습니다(Marquee 클래스).
// 구매 팝업의 이름 판은 이름이 길면 글자를 줄여 판 안에 맞춥니다(UINamePlate).
//
// [아이템 설명 툴팁 - 6단계]
// 플레이어 아이템 카드(팔린 카드 포함)의 아이콘 틀(아이콘 포함)에 마우스를 올리면 인벤토리 아이콘 호버와 같은 아이템 설명칸이 커서 위에 뜹니다.
// 그리기는 ItemTooltipDisplay가 하고(TryGetHoveredItem을 매 프레임 확인), 상점 창 위에 보이도록 그때만 정렬 순서를 올립니다.
// 소모성 재화 카드는 설명칸이 없습니다.
//
// [열기/닫기 연출]
// 열 때: 상점 창 전체가 투명한 상태에서 밝아지며 아래에서 올라오고, 이어서 카드들이 왼쪽 위부터 하나씩 짧은 시간차로 밝아지며
// 올라옵니다. 카드가 다 나타날 때까지 카드/[갱신하기]는 반응하지 않습니다(커서 반응/설명칸 포함). Esc/[나가기]는 바로 받아서,
// 그 순간의 모습 그대로 닫힘 연출로 넘어갑니다.
// 닫을 때: 창과 카드가 다 같이 투명해지며 아래로 내려가 사라집니다. 플레이어 조작은 닫힘 연출이 끝난 뒤 돌아오고, 그동안 상인의 F로
// 다시 열리지 않습니다(BlocksInteract). 플레이어 사망/상인이 사라짐은 연출 없이 바로 닫습니다.
// 시간은 게임 시간과 무관한 실제 시간(unscaled)이고, 값은 [열기/닫기 연출] 칸에서 조절합니다.
//
// [창이 떠 있는 동안 - 게임은 멈추지 않고 플레이어 조작만 막음 (형빈 확정)]
// 슬롯 교체 확인창(SlotReplaceConfirmUI)과 같은 방식입니다: 이동/조준·공격/장전/무기 전환/아이템 사용 컴포넌트를 잠시 끄고 닫으면
// 되돌립니다. 수류탄/해킹툴 좌클릭은 HUDClickBlocker, 상자/포탈/상인의 F는 InteractionTargetSelector가 막습니다(BlocksInteract).
// 플레이어가 죽으면 창만 닫습니다(조작 해제는 PlayerDeathHandler 몫이라 되돌리지 않음).
//
// [스프라이트 교체]
// 카드 안의 틀(카드/아이콘 틀/가격표/가격 칸/팔린 덮개)과 재화 아이콘에 스프라이트 칸이 있습니다(상점 창/팝업/버튼은 공용 스타일이라 없음). 비워두면 아래 색 값으로 만든 기본 둥근 사각형(목업)을 쓰고, 넣으면 그 스프라이트를
// 원래 색 그대로 씁니다(색 값은 무시 - 버튼의 마우스 올림/비활성만 Hover Tint/Disabled Tint로 어둡게). 9-Slice로 늘리고 싶은
// 스프라이트는 Sprite Editor에서 Border를 지정해 두면 자동으로 Sliced로 그립니다.
// 재화 아이콘 칸이 비어 있으면 씬의 자원 카운터(PlayerResourceDisplay)의 크레딧/청휘석 아이콘을 빌려 쓰고, 그것도 없으면 노란 사각형.
//
// [색/모양 - 공용 팝업 스타일]
// 상점 창/구매·갱신 팝업(흰 창 + 무늬), 하단 버튼과 팝업 버튼(평행사변형 - 떼는 순간 클릭, 누르는 동안 작아짐), 창 안 글자 색은
// 공용 스타일 에셋(PopupStyle)에서 읽습니다 - 다른 팝업과 항상 같습니다. 카드의 가격표/[-][+] 수량 버튼 색은 이 파일 값.
//
// [설정]
// 씬에 따로 붙일 필요가 없습니다 - 처음 열 때 스스로 만들어집니다. 크기/색/스프라이트를 바꾸고 싶으면 씬의 빈 오브젝트에 이 컴포넌트를
// 직접 붙여 인스펙터에서 조절하세요(그러면 그 컴포넌트를 사용하고, Play 중에 바꿔도 바로 반영됩니다). 크기 단위는 1920x1080 기준 px.
// 입력은 다른 HUD와 같이 EventSystem 없이 마우스 좌표를 직접 비교합니다.
public class ShopUI : MonoBehaviour
{
    [Header("문구")]
    public string buyLabel = "구매하기";
    public string refreshLabel = "갱신하기";
    public string exitLabel = "나가기";

    [Header("키")]
    [Tooltip("구매 팝업이 떠 있으면 팝업만 닫고, 아니면 상점 창을 닫습니다.")]
    public KeyCode closeKey = KeyCode.Escape;

    [Header("재화 아이콘")]
    [Tooltip("비워두면 씬의 PlayerResourceDisplay(자원 카운터)의 크레딧 아이콘을 씁니다.")]
    public Sprite creditIcon;
    [Tooltip("비워두면 씬의 PlayerResourceDisplay(자원 카운터)의 청휘석 아이콘을 씁니다.")]
    public Sprite pyroxenesIcon;

    [Header("상점 틀 (흰 창 - 바탕/모서리/무늬는 PopupStyle)")]
    [Tooltip("틀 가장자리에서 카드까지의 여백입니다.")]
    [Min(0f)] public float panelPadding = 24f;
    [Tooltip("카드 아래, 하단 버튼이 놓이는 줄의 높이입니다(틀 아래 여백 포함).")]
    [Min(0f)] public float bottomBarHeight = 72f;
    [Tooltip("화면 가운데에서 틀을 이만큼 옮깁니다.")]
    public Vector2 panelOffset = Vector2.zero;

    [Header("카드 (한 줄 4칸 x 2줄) - 둥근 평행사변형")]
    public Vector2 cardSize = new Vector2(170f, 240f);
    public Vector2 cardSpacing = new Vector2(18f, 18f);
    [Tooltip("카드 기울기(도)입니다. 안쪽 요소(이름/아이콘 틀/가격표)도 이 기울기로 맞춰집니다. 0이면 둥근 사각형.")]
    [Range(0f, 30f)] public float cardSlantAngle = 12f;
    [Tooltip("카드 바탕 왼쪽 위 색입니다(오른쪽 아래 색으로 대각선 그라데이션).")]
    public Color cardColorTop = new Color32(0xF4, 0xF6, 0xF9, 0xFF);
    [Tooltip("카드 바탕 오른쪽 아래 색입니다.")]
    public Color cardColorBottom = new Color32(0xE3, 0xE8, 0xEE, 0xFF);
    [Min(0f)] public float cardCornerRadius = 14f;
    [Tooltip("카드 아래에 옅은 그림자를 깝니다(PopupStyle의 버튼 그림자 값).")]
    public bool cardShadow = true;
    [Tooltip("카드 바탕의 흰 모서리 무늬입니다.")]
    public bool cardPattern = true;
    [Min(4f)] public float cardPatternTriangleSide = 22f;
    [Tooltip("모서리에서 무늬가 사라지는 거리입니다(카드 가로+세로에 대한 비율).")]
    [Range(0.05f, 1f)] public float cardPatternSpread = 0.32f;
    [Range(0f, 1f)] public float cardPatternMaxAlpha = 0.55f;
    [Tooltip("카드 가장자리에서 안쪽 요소까지의 여백입니다.")]
    [Min(0f)] public float cardPadding = 10f;
    [Tooltip("카드 안 요소(이름/아이콘 틀/가격표 틀) 사이 간격입니다.")]
    [Min(0f)] public float cardElementSpacing = 6f;

    [Header("이름")]
    public int nameFontSize = 20;

    [Header("흐르는 이름 (이름이 칸보다 길 때 - 카드 이름/구매 팝업 제목 공용)")]
    [Tooltip("카드 안쪽 폭에서 이름 구역을 좌우로 이만큼 더 좁힙니다(구역 밖 글자는 안 보임).")]
    [Min(0f)] public float nameAreaPadding = 0f;
    [Tooltip("이름이 흘러가는 속도입니다(초당 px).")]
    [Min(1f)] public float nameScrollSpeed = 40f;
    [Tooltip("처음(앞부분이 보이는 상태)에서 멈춰 있는 시간(초)입니다.")]
    [Min(0f)] public float nameScrollStartDelay = 1f;
    [Tooltip("이름 끝까지 보인 뒤 멈춰 있는 시간(초)입니다.")]
    [Min(0f)] public float nameScrollEndDelay = 1f;
    [Tooltip("끝에서 사라졌다가 처음 위치에서 다시 나타나는 페이드 시간(초, 사라짐/나타남 각각)입니다.")]
    [Min(0.01f)] public float nameScrollFadeDuration = 0.25f;
    [Tooltip("이름 구역 좌우 가장자리를 부드럽게 흐리는 폭(px)입니다. 0이면 칼같이 잘립니다.")]
    [Min(0f)] public float nameMaskSoftness = 8f;

    [Header("아이콘 틀 (등급 칸 - 왼쪽 위 → 오른쪽 아래 대각선 그라데이션 + 흰 무늬)")]
    [Tooltip("소모성 재화 카드의 아이콘 틀 왼쪽 위 색입니다.")]
    public Color basicFrameColorTop = new Color32(0xEE, 0xF1, 0xF4, 0xFF);
    [Tooltip("소모성 재화 카드의 아이콘 틀 오른쪽 아래 색입니다.")]
    public Color basicFrameColorBottom = new Color32(0xB9, 0xC3, 0xCD, 0xFF);
    public Color tier0FrameColorTop = new Color32(0xEE, 0xF1, 0xF4, 0xFF);
    public Color tier0FrameColorBottom = new Color32(0xB9, 0xC3, 0xCD, 0xFF);
    public Color tier1FrameColorTop = new Color32(0xD3, 0xEB, 0xFF, 0xFF);
    public Color tier1FrameColorBottom = new Color32(0x5A, 0xA8, 0xF2, 0xFF);
    public Color tier2FrameColorTop = new Color32(0xFF, 0xED, 0xB0, 0xFF);
    public Color tier2FrameColorBottom = new Color32(0xF3, 0xA6, 0x2A, 0xFF);
    public Color tier3FrameColorTop = new Color32(0xEF, 0xDC, 0xFF, 0xFF);
    public Color tier3FrameColorBottom = new Color32(0xA6, 0x5C, 0xE6, 0xFF);
    [Min(0f)] public float iconFrameCornerRadius = 10f;
    public bool iconFramePattern = true;
    [Min(4f)] public float iconFramePatternTriangleSide = 18f;
    [Range(0.05f, 1f)] public float iconFramePatternSpread = 0.42f;
    [Range(0f, 1f)] public float iconFramePatternMaxAlpha = 0.75f;
    [Tooltip("아이콘 틀 가장자리에서 아이콘까지의 여백입니다.")]
    [Min(0f)] public float iconPadding = 10f;
    [Tooltip("아이콘이 없는 상품에 표시할 빈 아이콘 색입니다.")]
    public Color missingIconColor = new Color(0f, 0f, 0f, 0.08f);
    [Tooltip("묶음 상품(1회 구매 시 2개 이상)의 \"x5\" 글자 크기/색입니다.")]
    public int amountFontSize = 20;
    public Color amountColor = new Color(0.1f, 0.15f, 0.3f, 1f);

    [Header("가격표 틀 (전체가 구매 버튼 - 하늘색 평행사변형, 색/무늬/글자 색/누름 연출은 PopupStyle의 일반 버튼)")]
    [Min(10f)] public float priceFrameHeight = 76f;
    [Tooltip("남색 가격 칸 색입니다(카드/구매 팝업 공용 - 평행사변형).")]
    public Color priceBoxColor = new Color32(0x2C, 0x45, 0x63, 0xFF);
    [Min(0f)] public float priceBoxCornerRadius = 7f;
    [Tooltip("카드 가격표 안 남색 가격 칸 위치 조정값입니다(+x 오른쪽, +y 위). 가격 숫자도 따라 움직입니다.")]
    public Vector2 priceBoxOffset = Vector2.zero;
    [Tooltip("카드 가격표 안 남색 가격 칸 크기 조정값입니다(기본 크기에 더함 - 음수면 작아짐).")]
    public Vector2 priceBoxSizeAdjust = Vector2.zero;
    [Min(8f)] public float currencyIconSize = 30f;
    [Tooltip("재화 아이콘 위치 조정값입니다(기본 위치 = 가격표 틀 왼쪽 위 줄의 왼쪽 끝). x+ 오른쪽, y+ 위쪽.")]
    public Vector2 currencyIconOffset = Vector2.zero;
    [Tooltip("가격 칸이 가격표 틀 왼쪽 끝에서 얼마나 떨어져 시작하는지입니다(재화 아이콘 크기와 무관).")]
    [Min(0f)] public float priceBoxLeftInset = 23f;
    [Tooltip("가격 숫자 위치 조정값입니다(기본 위치 = 가격 칸 가운데). x+ 오른쪽, y+ 위쪽.")]
    public Vector2 priceTextOffset = new Vector2(8f, 0f);
    [Tooltip("재화 아이콘이 하나도 없을 때 대신 칠하는 색입니다.")]
    public Color placeholderCurrencyColor = new Color(1f, 0.85f, 0.1f, 1f);
    public int priceFontSize = 20;
    public Color priceTextColor = Color.white;
    [Tooltip("\"구매하기\" 글자 크기입니다(색은 PopupStyle의 버튼 글자 색).")]
    public int buyLabelFontSize = 20;

    [Header("하단 버튼 (갱신하기 = 하늘색 / 나가기 = 회색) - 색/모양/글자 색/못 누를 때 표시는 PopupStyle")]
    public Vector2 bottomButtonSize = new Vector2(130f, 44f);
    [Min(0f)] public float bottomButtonSpacing = 12f;
    public int bottomButtonFontSize = 20;

    [Header("팔린 카드 (플레이어 아이템을 산 칸)")]
    [Tooltip("팔린 카드 전체 위에 덮는 색입니다(반투명 검정). 카드는 그대로 남고 누를 수 없게 됩니다.")]
    public Color soldOutOverlayColor = new Color(0f, 0f, 0f, 0.55f);

    [Header("구매 팝업 - 문구")]
    [Tooltip("이름(+을/를) 아래 줄에 표시되는 문구입니다.")]
    public string popupQuestion = "구매하시겠습니까?";
    public string popupNoLabel = "아니오";
    public string healthFullMessage = "체력이 가득 찼습니다";
    public string alreadyOwnedMessage = "이미 보유한 아이템입니다";
    [Tooltip("슬롯 아이템을 사면 원래 장착하던 아이템을 올려둘 빈 보관 좌대가 없을 때 문구입니다.")]
    public string noHoldingSpaceMessage = "보관 좌대에 빈 자리가 없습니다";
    [Tooltip("재화가 부족할 때 보여줄 문구입니다. 비워두면 가격 숫자만 빨갛게 됩니다.")]
    public string notEnoughCurrencyMessage = "";

    [Header("팝업 공통 (구매/갱신)")]
    [Tooltip("팝업이 떠 있는 동안 뒤의 상점 창을 덮는 막 색입니다(흰 창 위 흰 팝업이 묻히지 않도록).")]
    public Color popupDimColor = new Color(0f, 0f, 0f, 0.55f);

    [Header("구매 팝업 - 창 무늬 조정 (이 팝업에만 적용 - 기본값은 PopupStyle)")]
    [Tooltip("왼쪽 위 무늬가 퍼져 나오는 기준점 이동(px, +x 오른쪽, +y 위). 음수 x/양수 y로 옮기면 무늬가 창 바깥쪽으로 물러납니다.")]
    public Vector2 popupPatternTopLeftOffset = Vector2.zero;
    [Tooltip("왼쪽 위 무늬가 퍼지는 범위 배율입니다(1 = 기본, 작을수록 모서리에 작게).")]
    [Min(0f)] public float popupPatternTopLeftScale = 1f;
    [Tooltip("오른쪽 아래 무늬가 퍼져 나오는 기준점 이동(px, +x 오른쪽, +y 위).")]
    public Vector2 popupPatternBottomRightOffset = Vector2.zero;
    [Tooltip("오른쪽 아래 무늬가 퍼지는 범위 배율입니다(1 = 기본).")]
    [Min(0f)] public float popupPatternBottomRightScale = 1f;
    [Tooltip("삼각형 한 개 크기 배율입니다(1 = 기본).")]
    [Min(0.1f)] public float popupPatternTriangleScale = 1f;

    [Header("구매 팝업 - 틀/제목 (바탕/모서리/글자 색/이름 판은 PopupStyle)")]
    public Vector2 popupSize = new Vector2(580f, 330f);
    public Vector2 popupOffset = Vector2.zero;
    [Min(0f)] public float popupPadding = 20f;
    public int popupTitleFontSize = 30;
    [Tooltip("켜면 이름 판의 플레이어 아이템 이름을 등급 색으로 칠합니다(소모품 이름은 흰색).")]
    public bool popupNameUseTierColor = true;
    [Tooltip("켜면 등급 색을 씬의 아이템 설명칸(PlayerItemInfoDisplay)과 같게 맞춥니다. 끄거나 그 컴포넌트가 없으면 아래 색을 씁니다.")]
    public bool popupNameFollowItemInfoColors = true;
    public Color popupTier0NameColor = Color.white;
    public Color popupTier1NameColor = new Color(0.45f, 0.75f, 1f, 1f);
    public Color popupTier2NameColor = new Color(1f, 0.85f, 0.25f, 1f);
    public Color popupTier3NameColor = new Color(0.75f, 0.5f, 1f, 1f);

    [Header("구매 팝업 - 수량")]
    public Vector2 quantityButtonSize = new Vector2(52f, 48f);
    public Vector2 quantityBoxSize = new Vector2(240f, 48f);
    [Min(0f)] public float quantitySpacing = 16f;
    [Tooltip("제목 아래에서 수량 줄까지의 간격입니다.")]
    [Min(0f)] public float quantityRowGap = 14f;
    [Tooltip("[-]/[+] 버튼과 수량 칸에는 버튼과 같은 옅은 그림자가 깔립니다(PopupStyle).")]
    public Color quantityButtonColor = Color.white;
    public Color quantityButtonHoverColor = new Color(0.8f, 0.8f, 0.8f, 1f);
    public Color quantityButtonDisabledColor = new Color(1f, 1f, 1f, 0.3f);
    public Color quantityBoxColor = Color.white;
    [Min(0f)] public float quantityCornerRadius = 10f;
    [Tooltip("수량 숫자/[-][+] 글자 색은 PopupStyle의 창 글자 색입니다.")]
    public int quantityFontSize = 28;

    [Header("구매/갱신 팝업 - 안내 문구 (체력 가득 등)")]
    public int reasonFontSize = 20;
    [Tooltip("흰 팝업 위에서 잘 보이는 진한 빨강입니다.")]
    public Color reasonTextColor = new Color32(0xD9, 0x3A, 0x3A, 0xFF);

    [Header("구매 팝업 - 버튼 ([구매하기] = 노랑 / [아니오] = 하늘색) - 색/모양/글자 색은 PopupStyle")]
    public Vector2 popupButtonSize = new Vector2(180f, 90f);
    [Min(0f)] public float popupButtonSpacing = 30f;
    [Tooltip("팝업 아래쪽 끝에서 버튼까지의 거리입니다.")]
    [Min(0f)] public float popupButtonBottomMargin = 22f;
    public int popupBuyLabelFontSize = 26;
    public int popupNoLabelFontSize = 30;
    [Min(8f)] public float popupCurrencyIconSize = 34f;
    [Tooltip("[구매하기] 안 재화 아이콘 위치 조정값입니다(기본 위치 = 버튼 위쪽 줄의 왼쪽 끝).")]
    public Vector2 popupCurrencyIconOffset = Vector2.zero;
    [Tooltip("[구매하기] 안 가격 칸이 버튼 왼쪽 끝에서 얼마나 떨어져 시작하는지입니다.")]
    [Min(0f)] public float popupPriceBoxLeftInset = 25f;
    [Tooltip("[구매하기] 안 남색 가격 칸 위치 조정값입니다(+x 오른쪽, +y 위). 가격 숫자/줄도 칸을 따라 움직입니다.")]
    public Vector2 popupPriceBoxOffset = Vector2.zero;
    [Tooltip("[구매하기] 안 남색 가격 칸 크기 조정값입니다(기본 크기에 더함 - 음수면 작아짐, 칸은 왼쪽 끝 기준으로 늘고 줆).")]
    public Vector2 popupPriceBoxSizeAdjust = Vector2.zero;
    [Tooltip("[구매하기] 안 가격 숫자 위치 조정값입니다(기본 위치 = 가격 칸 가운데).")]
    public Vector2 popupPriceTextOffset = new Vector2(8f, 0f);
    public int popupPriceFontSize = 24;
    [Tooltip("재화가 부족할 때 가격 숫자 색입니다.")]
    public Color unaffordablePriceColor = new Color(1f, 0.25f, 0.25f, 1f);

    [Header("할인 가격 표시 (67번 등 - 원래 가격에 줄을 긋고 옆에 실제 가격)")]
    [Tooltip("할인 전 원래 가격 숫자 색입니다.")]
    public Color originalPriceColor = new Color(0.6f, 0.6f, 0.6f, 1f);
    [Tooltip("원래 가격 위에 긋는 줄 색입니다.")]
    public Color originalPriceStrikeColor = new Color(1f, 0.2f, 0.2f, 1f);
    [Tooltip("원래 가격 위에 긋는 줄 굵기입니다.")]
    [Min(0.5f)] public float originalPriceStrikeThickness = 2f;
    [Tooltip("줄 위치 조정값입니다(기본 위치 = 원래 가격 숫자의 정중앙). x+ 오른쪽, y+ 위쪽.")]
    public Vector2 originalPriceStrikeOffset = Vector2.zero;
    [Tooltip("줄이 원래 가격 숫자보다 양옆으로 더 길게 나가는 길이입니다(0이면 숫자 폭과 같음, 음수면 더 짧게).")]
    public float originalPriceStrikeExtend = 2f;
    [Tooltip("원래 가격 글자 크기 비율(%)입니다. 100이면 실제 가격과 같은 크기입니다.")]
    [Range(30f, 150f)] public float originalPriceSizePercent = 75f;
    [Tooltip("원래 가격과 실제 가격 사이 간격입니다.")]
    [Min(0f)] public float originalPriceSpacing = 6f;

    [Header("갱신 팝업 - 문구 (\"1 크레딧으로 갱신하시겠습니까?\")")]
    [Tooltip("\"(가격) (재화)으로\" 뒤에 붙는 문구입니다.")]
    public string refreshQuestionSuffix = "갱신하시겠습니까?";
    public string refreshConfirmLabel = "갱신하기";
    public string refreshNoLabel = "아니오";
    [Tooltip("재화가 부족할 때 \"(재화)이(가)\" 뒤에 붙는 문구입니다.")]
    public string refreshNotEnoughSuffix = "부족합니다";
    [Tooltip("새로 진열할 아이템 후보가 하나도 없어 갱신할 수 없을 때 문구입니다.")]
    public string noRefreshCandidatesMessage = "새로 진열할 아이템이 없습니다";

    [Header("갱신 팝업 - 모양 (색/스프라이트는 구매 팝업과 공용)")]
    public Vector2 refreshPopupSize = new Vector2(520f, 230f);
    public Vector2 refreshPopupOffset = Vector2.zero;
    public int refreshTitleFontSize = 28;
    public Vector2 refreshButtonSize = new Vector2(170f, 60f);
    [Min(0f)] public float refreshButtonSpacing = 30f;
    [Min(0f)] public float refreshButtonBottomMargin = 22f;
    public int refreshButtonFontSize = 26;
    [Tooltip("\"크레딧이 부족합니다\" 등 안내 문구의 위치 조정값입니다(기본 위치 = 제목 바로 아래). x+ 오른쪽, y+ 위쪽.")]
    public Vector2 refreshReasonOffset = Vector2.zero;

    [Header("열기/닫기 연출 (실제 시간 기준, 초)")]
    [Tooltip("창 전체가 투명한 상태에서 밝아지며 올라오는 시간입니다. 0이면 바로 나타납니다.")]
    [Min(0f)] public float openWindowDuration = 0.2f;
    [Tooltip("창 전체가 올라오는 거리입니다(처음에 이만큼 아래에서 시작).")]
    public float openWindowRise = 40f;
    [Tooltip("창이 나타나기 시작한 뒤 첫 카드가 나타나기 시작할 때까지의 시간입니다.")]
    [Min(0f)] public float cardStartDelay = 0.12f;
    [Tooltip("카드 한 장이 투명한 상태에서 밝아지며 올라오는 시간입니다.")]
    [Min(0f)] public float cardAppearDuration = 0.16f;
    [Tooltip("카드가 올라오는 거리입니다(처음에 이만큼 아래에서 시작).")]
    public float cardRise = 24f;
    [Tooltip("카드끼리 나타나는 시간차입니다(왼쪽 위부터 순서대로). 0이면 한꺼번에 나타납니다.")]
    [Min(0f)] public float cardStagger = 0.03f;
    [Tooltip("닫을 때 창과 카드가 다 같이 투명해지며 내려가는 시간입니다. 0이면 바로 사라집니다.")]
    [Min(0f)] public float closeDuration = 0.16f;
    [Tooltip("닫을 때 내려가는 거리입니다.")]
    public float closeDrop = 40f;

    [Header("정렬 (일반적으로 건드릴 필요 없음)")]
    [Tooltip("HUD/툴팁보다 위, 슬롯 교체 확인창(90)/게임 오버 창(100)보다 아래입니다.")]
    public int canvasSortingOrder = 85;

    // ------------------------------------------------------------------
    // 외부에서 쓰는 정적 진입점
    // ------------------------------------------------------------------

    private const int Columns = 4;
    private const int Rows = 2;

    private static ShopUI instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
    }

    // 창이 떠 있는지(닫힘 연출 중은 제외 - 이미 닫힌 것으로 봄)
    public static bool IsOpen => instance != null && instance.isOpen;

    // 창이 화면에 보이는지(닫힘 연출 중 포함)
    public static bool IsVisible => instance != null && (instance.isOpen || instance.IsClosing);

    // 구매/갱신 팝업이 떠 있어 화면 전체가 막으로 어둡게 덮였는지(그동안 팝업 말고는 반응하면 안 됨 - ItemTooltipDisplay가 읽음)
    public static bool IsDimmed => instance != null && instance.isOpen && (instance.IsPopupOpen || instance.refreshPopupOpen);

    // 지금 마우스가 상점 창(틀) 위에 있는지(창에 가려진 HUD 아이콘 호버를 무시하도록 ItemTooltipDisplay가 읽음)
    public static bool IsPointerOverWindow => IsVisible && IsPointerOver(instance.panelRect);

    // 상자/포탈/상인의 F, 공격 좌클릭을 막아야 하는지(창이 떠 있거나, 닫힘 연출 중이거나, 방금 닫힌 그 프레임)
    public static bool BlocksInteract =>
        instance != null && (instance.isOpen || instance.IsClosing || instance.closedFrame == Time.frameCount);

    // 상점 창의 Canvas 정렬 순서(아이템 툴팁이 상점 창 위에 뜨도록 ItemTooltipDisplay가 읽음)
    public static int SortingOrder => instance != null ? instance.canvasSortingOrder : 0;

    // 지금 마우스가 아이콘 틀 위에 올라가 있는 플레이어 아이템 카드의 아이템(호버 툴팁용, ItemTooltipDisplay가 매 프레임 확인).
    // 소모성 재화 카드는 아이템 정보가 없어 제외하고, 팔린 카드는 포함합니다. 구매/갱신 팝업이나 교체 확인창이 떠 있으면 없음.
    public static bool TryGetHoveredItem(out ItemDefinition item)
    {
        item = null;
        if (instance == null || !instance.isOpen || instance.currentNpc == null) return false;
        if (!instance.CardsReady) return false; // 카드가 다 나타나기 전에는 반응하지 않음
        if (instance.IsPopupOpen || instance.refreshPopupOpen || SlotReplaceConfirmUI.IsOpen) return false;

        var listings = instance.currentNpc.Listings;
        for (int i = 0; i < instance.cards.Length && i < listings.Count; i++)
        {
            var listing = listings[i];
            if (listing == null || listing.isBasic || listing.item == null) continue;
            if (!instance.cards[i].root.gameObject.activeSelf) continue;
            if (IsPointerOver(instance.cards[i].iconFrameRect)) // 아이콘 틀(아이콘 포함) 위에서만 - 이름/가격표/카드 여백 제외
            {
                item = listing.item;
                return true;
            }
        }
        return false;
    }

    // 상점 창을 엽니다. 이미 떠 있거나 플레이어가 죽었으면 열지 않고 false.
    public static bool Show(ShopNPC npc)
    {
        if (npc == null) return false;
        if (instance == null) instance = UnityEngine.Object.FindFirstObjectByType<ShopUI>();
        if (instance == null)
        {
            var go = new GameObject("ShopUI");
            instance = go.AddComponent<ShopUI>();
        }
        return instance.Open(npc);
    }

    // ------------------------------------------------------------------
    // 인스턴스
    // ------------------------------------------------------------------

    private const float PopupTitleGap = 6f; // 이름 판과 "구매하시겠습니까?" 사이

    private class CardView
    {
        public RectTransform root;
        public CanvasGroup group; // 열기 연출용 투명도
        public UIPatternBox background;
        public TextMeshProUGUI nameText;
        public Marquee nameMarquee;
        public RectTransform iconFrameRect;
        public UIPatternBox iconFrame;
        public RectTransform iconRect;
        public Image icon;
        public TextMeshProUGUI amountText;
        public RectTransform priceFrameRect;
        public UIPatternBox priceFrame;
        public RectTransform currencyRect;
        public Image currencyIcon;
        public RectTransform priceBoxRect;
        public UIPatternBox priceBox;
        public TextMeshProUGUI priceText;
        public RectTransform strikeRect; // 원래 가격 위에 긋는 줄(할인 중일 때만 보임)
        public Image strikeLine;
        public TextMeshProUGUI buyText;
        public RectTransform soldOutRect;
        public UIPatternBox soldOutOverlay;
    }

    private bool isOpen;
    private int openedFrame = -1;

    // 열기/닫기 연출 상태
    private enum AnimPhase { None, Opening, Closing }
    private AnimPhase animPhase = AnimPhase.None;
    private float animTime;           // 연출이 시작된 뒤 흐른 실제 시간
    private float panelAnimOffset;    // 지금 창이 원래 자리에서 위아래로 옮겨진 양(아래 = 음수)
    private float closeStartAlpha = 1f;
    private float closeStartOffset;
    private bool IsClosing => animPhase == AnimPhase.Closing;
    private bool CardsReady => animPhase != AnimPhase.Opening; // 카드가 다 나타났는지(열기 연출이 끝났는지)
    private int closedFrame = -1;
    private ShopNPC currentNpc;

    // 구매 팝업 상태
    private ShopListing popupListing;   // null이면 팝업이 닫혀 있음
    private int popupQuantity = 1;
    private int popupOpenedFrame = -1;

    // 창이 떠 있는 동안 잠시 끈 플레이어 컴포넌트들(닫을 때 원래대로 되돌림)
    private readonly List<Behaviour> disabledBehaviours = new List<Behaviour>();

    private GameObject canvasGO;
    private Canvas canvas;
    private CanvasScaler scaler;
    private RectTransform panelRect;
    private CanvasGroup panelGroup; // 열기/닫기 연출용 창 전체 투명도
    private UIPatternBox panelBox;
    private RectTransform popupDimRect; // 팝업과 상점 창 사이의 막
    private readonly CardView[] cards = new CardView[Columns * Rows];
    private RectTransform refreshRect;
    private UIPatternBox refreshBox;
    private TextMeshProUGUI refreshText;
    private RectTransform exitRect;
    private UIPatternBox exitBox;
    private TextMeshProUGUI exitText;

    // 구매 팝업 UI
    private RectTransform popupRect;
    private UIPatternBox popupBox;
    private TextMeshProUGUI popupTitleText;   // 둘째 줄 "구매하시겠습니까?"(고정)
    private UINamePlate popupNamePlate;        // 첫째 줄 아이템 이름 판
    private RectTransform minusRect;
    private UIPatternBox minusBox;
    private TextMeshProUGUI minusText;
    private RectTransform plusRect;
    private UIPatternBox plusBox;
    private TextMeshProUGUI plusText;
    private RectTransform quantityBoxRect;
    private UIPatternBox quantityBox;
    private TextMeshProUGUI quantityText;
    private TextMeshProUGUI reasonText;
    private RectTransform popupBuyRect;
    private UIPatternBox popupBuyBox;
    private RectTransform popupCurrencyRect;
    private Image popupCurrencyIcon;
    private RectTransform popupPriceBoxRect;
    private UIPatternBox popupPriceBox;
    private TextMeshProUGUI popupPriceText;
    private RectTransform popupStrikeRect; // 원래 가격 위에 긋는 줄(할인 중일 때만 보임)
    private Image popupStrikeLine;
    private TextMeshProUGUI popupBuyText;
    private RectTransform popupNoRect;
    private UIPatternBox popupNoBox;
    private TextMeshProUGUI popupNoText;

    private bool IsPopupOpen => popupListing != null;

    // 갱신 팝업 상태/UI
    private bool refreshPopupOpen;
    private bool refreshPopupCanRefresh = true; // 갱신 팝업 [갱신하기]를 누를 수 있는지(글자 색용, 매 프레임 갱신)
    private bool popupCanBuy = true;            // 구매 팝업 [구매하기]를 누를 수 있는지(글자 색용, 매 프레임 갱신)
    private int refreshPopupOpenedFrame = -1;
    private RectTransform refreshPopupRect;
    private UIPatternBox refreshPopupBox;
    private TextMeshProUGUI refreshTitleText;
    private TextMeshProUGUI refreshReasonText;
    private RectTransform refreshYesRect;
    private UIPatternBox refreshYesBox;
    private TextMeshProUGUI refreshYesText;
    private RectTransform refreshNoRect;
    private UIPatternBox refreshNoBox;
    private TextMeshProUGUI refreshNoText;

    void Awake()
    {
        if (instance == null) instance = this;
        else if (instance != this)
        {
            Destroy(this); // 중복 방지 - 하나만 씁니다
            return;
        }

        BuildUI();
        canvasGO.SetActive(false);
        PlayerHealth.OnAnyPlayerDied += HandlePlayerDied;
    }

    void OnDestroy()
    {
        PlayerHealth.OnAnyPlayerDied -= HandlePlayerDied;
        if (isOpen || IsClosing) RestorePlayerControls();
        if (instance == this) instance = null;
    }

    private bool Open(ShopNPC npc)
    {
        if (isOpen) return false;
        if (IsClosing) FinishClose(); // 닫힘 연출 중에 다시 열면 닫힘을 바로 마무리하고 엽니다

        var health = UnityEngine.Object.FindFirstObjectByType<PlayerHealth>();
        if (health != null && health.IsDead) return false;

        isOpen = true;
        openedFrame = Time.frameCount;
        currentNpc = npc;
        npc.EnsureStocked();
        ClosePopup();
        CloseRefreshPopup();
        foreach (var c in cards) c.nameMarquee.Reset(); // 다시 열면 흐르는 이름도 처음부터

        DisablePlayerControls(health);

        canvasGO.SetActive(true);
        animPhase = AnimPhase.Opening;
        animTime = 0f;
        ApplyLayout(); // 창/카드가 투명한 시작 상태로 첫 프레임부터 그려지도록
        return true;
    }

    // 창을 닫습니다. instant = true면 연출 없이 바로 사라집니다(상인이 사라졌을 때 등).
    private void Close(bool instant = false)
    {
        if (!isOpen) return;

        isOpen = false;
        closedFrame = Time.frameCount;
        currentNpc = null;
        ClosePopup();
        CloseRefreshPopup();
        if (popupDimRect.gameObject.activeSelf) popupDimRect.gameObject.SetActive(false);

        if (instant || closeDuration <= 0f)
        {
            FinishClose();
            return;
        }

        // 지금 보이는 모습 그대로(열기 연출 도중이어도) 닫힘 연출을 시작합니다.
        closeStartAlpha = panelGroup.alpha;
        closeStartOffset = panelAnimOffset;
        animPhase = AnimPhase.Closing;
        animTime = 0f;
    }

    // 닫힘 연출이 끝났을 때(또는 바로 닫을 때): 창을 끄고 플레이어 조작을 되돌립니다.
    private void FinishClose()
    {
        animPhase = AnimPhase.None;
        closedFrame = Time.frameCount;
        canvasGO.SetActive(false);
        RestorePlayerControls();
    }

    // 닫힘 연출 한 프레임: 창 전체(카드 포함)가 투명해지며 내려갑니다.
    private void TickClosing()
    {
        animTime += AnimStep();
        float t = closeDuration > 0f ? Mathf.Clamp01(animTime / closeDuration) : 1f;
        float e = t * (2f - t);
        panelGroup.alpha = closeStartAlpha * (1f - e);
        panelAnimOffset = closeStartOffset - closeDrop * e;
        panelRect.anchoredPosition = panelOffset + new Vector2(0f, panelAnimOffset);
        exitBox.TickButton(false, true); // 방금 누른 [나가기]가 눌린 크기로 굳지 않도록 원래 크기로 되돌림
        if (t >= 1f) FinishClose();
    }

    // 연출 시간 한 프레임 분량(실제 시간). 로딩 등으로 한 프레임이 길어져도 연출을 건너뛰지 않도록 상한을 둡니다.
    private static float AnimStep()
    {
        return Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);
    }

    private static float EaseOutCubic(float t)
    {
        float u = 1f - t;
        return 1f - u * u * u;
    }

    // 열기 연출을 지금 레이아웃 위에 덧입힙니다(ApplyLayout 마지막에 호출 - 원래 자리를 매번 새로 잡은 뒤 이동량만 더함).
    // 연출이 끝난 뒤에는 창/카드 모두 불투명, 이동량 0입니다.
    private void ApplyOpenVisual()
    {
        if (IsClosing) return;
        bool opening = animPhase == AnimPhase.Opening;

        float wt = !opening ? 1f : openWindowDuration > 0f ? Mathf.Clamp01(animTime / openWindowDuration) : 1f;
        float we = EaseOutCubic(wt);
        panelGroup.alpha = we;
        panelAnimOffset = -openWindowRise * (1f - we);
        panelRect.anchoredPosition = panelOffset + new Vector2(0f, panelAnimOffset);

        int order = 0; // 보이는 카드끼리의 순서(빈 칸은 건너뜀)
        for (int i = 0; i < cards.Length; i++)
        {
            var view = cards[i];
            if (!view.root.gameObject.activeSelf) continue;
            float ct = 1f;
            if (opening)
            {
                float local = animTime - cardStartDelay - order * cardStagger;
                ct = cardAppearDuration > 0f ? Mathf.Clamp01(local / cardAppearDuration) : (local >= 0f ? 1f : 0f);
            }
            float ce = EaseOutCubic(ct);
            view.group.alpha = ce;
            view.root.anchoredPosition += new Vector2(0f, -cardRise * (1f - ce));
            order++;
        }
    }

    // 열기 연출 전체 길이(창 + 마지막 카드까지)
    private float OpenAnimLength()
    {
        int visible = 0;
        for (int i = 0; i < cards.Length; i++) if (cards[i].root.gameObject.activeSelf) visible++;
        float cardsEnd = visible > 0 ? cardStartDelay + (visible - 1) * cardStagger + cardAppearDuration : 0f;
        return Mathf.Max(openWindowDuration, cardsEnd);
    }

    // 플레이어 사망: 창만 닫습니다. 조작 해제(PlayerDeathHandler)를 덮어쓰지 않도록 컴포넌트는 되돌리지 않습니다.
    private void HandlePlayerDied(PlayerHealth health)
    {
        if (!isOpen && !IsClosing) return;
        isOpen = false;
        animPhase = AnimPhase.None;
        closedFrame = Time.frameCount;
        currentNpc = null;
        ClosePopup();
        CloseRefreshPopup();
        disabledBehaviours.Clear();
        canvasGO.SetActive(false);
    }

    void Update()
    {
        if (IsClosing)
        {
            TickClosing();
            return;
        }
        if (!isOpen) return;

        // 상인이 사라졌으면(씬 전환 등) 창을 연출 없이 바로 닫습니다.
        if (currentNpc == null)
        {
            Close(true);
            return;
        }

        if (animPhase == AnimPhase.Opening) animTime += AnimStep();
        ApplyLayout();
        if (animPhase == AnimPhase.Opening && animTime >= OpenAnimLength()) animPhase = AnimPhase.None; // 카드가 다 나타남
        bool cardsReady = CardsReady;

        // 슬롯 교체 확인창이 떠 있거나 방금 닫힌 프레임이면 상점 창은 입력을 받지 않습니다(그 창의 F/Esc/클릭이 겹치지 않도록).
        if (SlotReplaceConfirmUI.BlocksInteract) return;

        if (refreshPopupOpen)
        {
            UpdateRefreshPopup();
            return; // 갱신 팝업이 떠 있는 동안 상점 창은 반응하지 않습니다.
        }

        if (IsPopupOpen)
        {
            UpdatePopup();
            return; // 팝업이 떠 있는 동안 상점 창은 반응하지 않습니다.
        }

        int hoveredCard = -1;
        int clickedCard = -1;
        for (int i = 0; i < cards.Length; i++)
        {
            bool visible = cards[i].root.gameObject.activeSelf;
            if (!visible) continue;
            bool sold = cards[i].soldOutRect.gameObject.activeSelf; // 팔린 카드는 누를 수 없음(덮개가 어둡게 덮음 - 버튼 색은 평소대로)
            bool over = cardsReady && !sold && IsPointerOver(cards[i].priceFrameRect); // 카드가 다 나타나기 전에는 반응 없음
            if (over) hoveredCard = i;
            if (cards[i].priceFrame.TickButton(over, true) && !sold) clickedCard = i; // 클릭 = 가격표 위에서 눌렀다가 뗀 순간
        }

        bool refreshAvailable = currentNpc.RemainingRefreshes > 0;
        bool overRefresh = cardsReady && refreshAvailable && IsPointerOver(refreshRect); // 카드가 다 나타나기 전에는 반응 없음
        bool overExit = IsPointerOver(exitRect);
        bool clickRefresh = refreshBox.TickButton(overRefresh, refreshAvailable);
        bool clickExit = exitBox.TickButton(overExit, true);
        UISound.TrackHover(ref lastHoveredButton, overExit ? 21 : overRefresh ? 20 : hoveredCard, Time.frameCount == openedFrame);

        // 창을 연 그 프레임의 입력(상인에게 누른 F, 같은 프레임의 클릭)은 무시합니다.
        if (Time.frameCount == openedFrame) return;

        if (Input.GetKeyDown(closeKey)) { UISound.Cancel(); Close(); return; }

        if (clickExit) { UISound.Cancel(); Close(); return; }
        if (clickRefresh) { UISound.Confirm(); OnRefreshClicked(); return; }
        if (clickedCard >= 0) { UISound.Confirm(); OpenPopup(clickedCard); }
    }

    private int lastHoveredButton = -1; // [사운드] 호버음용 - 마지막으로 마우스가 올라간 버튼 번호

    private void OnRefreshClicked()
    {
        OpenRefreshPopup();
    }

    // ------------------------------------------------------------------
    // 갱신 팝업 ("1 크레딧으로 갱신하시겠습니까?" [갱신하기] [아니오])
    // ------------------------------------------------------------------

    private void OpenRefreshPopup()
    {
        if (currentNpc == null || currentNpc.RemainingRefreshes <= 0) return;
        refreshPopupOpen = true;
        refreshPopupOpenedFrame = Time.frameCount;
        refreshPopupRect.gameObject.SetActive(true);
        refreshYesBox.ResetPress();
        refreshNoBox.ResetPress();
        RefreshRefreshPopupVisuals(out _, out _); // 내용을 먼저 채워 빈 팝업이 한 프레임 보이지 않게
        ApplyLayout();
    }

    private void CloseRefreshPopup()
    {
        refreshPopupOpen = false;
        if (refreshPopupRect != null && refreshPopupRect.gameObject.activeSelf) refreshPopupRect.gameObject.SetActive(false);
    }

    private void RefreshRefreshPopupVisuals(out bool overYes, out bool overNo)
    {
        var block = currentNpc.CheckRefresh();
        bool canRefresh = block == ShopNPC.RefreshBlock.None;

        overYes = canRefresh && IsPointerOver(refreshYesRect);
        overNo = IsPointerOver(refreshNoRect);
        refreshPopupCanRefresh = canRefresh;

        string currencyName = ShopConfig.GetCurrencyName(currentNpc.RefreshCurrency);
        refreshTitleText.text = $"{currentNpc.RefreshPrice} {currencyName}{InstrumentalParticle(currencyName)} {refreshQuestionSuffix}";

        string reason = "";
        if (block == ShopNPC.RefreshBlock.NotEnoughCurrency)
            reason = $"{currencyName}{SubjectParticle(currencyName)} {refreshNotEnoughSuffix}";
        else if (block == ShopNPC.RefreshBlock.NoCandidates)
            reason = noRefreshCandidatesMessage;
        refreshReasonText.text = reason;
        bool showReason = !string.IsNullOrEmpty(reason);
        if (refreshReasonText.gameObject.activeSelf != showReason) refreshReasonText.gameObject.SetActive(showReason);
    }

    private void UpdateRefreshPopup()
    {
        RefreshRefreshPopupVisuals(out bool overYes, out bool overNo);
        bool clickYes = refreshYesBox.TickButton(overYes, refreshPopupCanRefresh);
        bool clickNo = refreshNoBox.TickButton(overNo, true);
        UISound.TrackHover(ref lastHoveredButton, UISound.HoveredId(40, overYes, overNo), Time.frameCount == refreshPopupOpenedFrame);

        // 팝업을 연 그 프레임의 클릭([갱신하기] 버튼)은 무시합니다.
        if (Time.frameCount == refreshPopupOpenedFrame) return;

        if (Input.GetKeyDown(closeKey)) { UISound.Cancel(); CloseRefreshPopup(); return; }

        if (clickNo) { UISound.Cancel(); CloseRefreshPopup(); return; }
        if (clickYes)
        {
            UISound.Confirm();
            currentNpc.TryRefresh();
            CloseRefreshPopup();
        }
        // 팝업 바깥(뒤의 상점 창 포함) 클릭은 무시합니다.
    }

    // 받침이 있으면(ㄹ 받침 제외) "으로", 없으면 "로" (크레딧으로/청휘석으로)
    private static string InstrumentalParticle(string word)
    {
        if (string.IsNullOrEmpty(word)) return "(으)로";
        char last = word[word.Length - 1];
        if (last < '\uAC00' || last > '\uD7A3') return "(으)로";
        int jong = (last - '\uAC00') % 28;
        return (jong == 0 || jong == 8) ? "로" : "으로";
    }

    // 받침이 있으면 "이", 없으면 "가" (크레딧이/청휘석이)
    private static string SubjectParticle(string word)
    {
        if (string.IsNullOrEmpty(word)) return "이(가)";
        char last = word[word.Length - 1];
        if (last < '\uAC00' || last > '\uD7A3') return "이(가)";
        return (last - '\uAC00') % 28 != 0 ? "이" : "가";
    }

    private static bool IsPointerOver(RectTransform rect)
    {
        if (rect == null || !rect.gameObject.activeInHierarchy) return false;
        return RectTransformUtility.RectangleContainsScreenPoint(rect, Input.mousePosition, null);
    }

    // ------------------------------------------------------------------
    // 구매 팝업
    // ------------------------------------------------------------------

    private void OpenPopup(int index)
    {
        var listings = currentNpc.Listings;
        if (index < 0 || index >= listings.Count) return;
        var listing = listings[index];
        if (listing == null || listing.soldOut) return;

        popupListing = listing;
        popupQuantity = 1;
        popupOpenedFrame = Time.frameCount;
        minusBox.ResetPress();
        plusBox.ResetPress();
        popupBuyBox.ResetPress();
        popupNoBox.ResetPress();
        popupRect.gameObject.SetActive(true);
        RefreshPopupVisuals(out _, out _, out _, out _); // 이전 팝업 내용이 한 프레임 보이지 않도록 먼저 채웁니다
        ApplyLayout();
    }

    private void ClosePopup()
    {
        popupListing = null;
        popupQuantity = 1;
        if (popupRect != null && popupRect.gameObject.activeSelf) popupRect.gameObject.SetActive(false);
    }

    // 팝업에 표시되는 내용(제목/수량/가격/버튼 색/안내 문구)을 지금 상태로 채웁니다. 마우스 올림 상태를 돌려줍니다.
    private void RefreshPopupVisuals(out bool overMinus, out bool overPlus, out bool overBuy, out bool overNo)
    {
        var listing = popupListing;
        bool quantityEditable = listing.isBasic;
        int maxQuantity = currentNpc.GetMaxQuantity(listing);
        popupQuantity = Mathf.Clamp(popupQuantity, 1, maxQuantity); // 체력이 바뀌는 등으로 상한이 줄어든 경우

        var block = currentNpc.CheckPurchase(listing, popupQuantity);
        bool canBuy = block == ShopNPC.PurchaseBlock.None;
        bool canMinus = quantityEditable && popupQuantity > 1;
        bool canPlus = quantityEditable && popupQuantity < maxQuantity;

        overMinus = canMinus && IsPointerOver(minusRect);
        overPlus = canPlus && IsPointerOver(plusRect);
        overBuy = canBuy && IsPointerOver(popupBuyRect);
        overNo = IsPointerOver(popupNoRect);

        minusBox.color = QuantityButtonColor(overMinus, canMinus);
        plusBox.color = QuantityButtonColor(overPlus, canPlus);
        popupCanMinus = canMinus;
        popupCanPlus = canPlus;
        popupCanBuy = canBuy;

        popupTitleText.text = popupQuestion; // 첫째 줄(이름 + 을/를)은 LayoutPopup에서 흐르는 이름으로 그립니다
        quantityText.text = popupQuantity.ToString();
        popupPriceText.text = FormatPrice(ShopNPC.GetOriginalTotalPrice(listing, popupQuantity),
                                          ShopNPC.GetTotalPrice(listing, popupQuantity), ShopNPC.IsPriceModified(listing));
        popupPriceText.color = currentNpc.CanAfford(listing, popupQuantity) ? priceTextColor : unaffordablePriceColor;
        UpdatePopupStrike();

        Sprite currencySprite = GetPriceIcon(listing);
        popupCurrencyIcon.sprite = currencySprite;
        popupCurrencyIcon.color = currencySprite != null ? Color.white : placeholderCurrencyColor;

        string reason = GetBlockMessage(block);
        reasonText.text = reason;
        bool showReason = !string.IsNullOrEmpty(reason);
        if (reasonText.gameObject.activeSelf != showReason) reasonText.gameObject.SetActive(showReason);
    }

    private bool popupCanMinus;
    private bool popupCanPlus;

    // [-]/[+] 색(흰 둥근 사각형): 못 누름 > 마우스 올림 > 평소
    private Color QuantityButtonColor(bool hovered, bool enabled)
    {
        return !enabled ? quantityButtonDisabledColor : (hovered ? quantityButtonHoverColor : quantityButtonColor);
    }

    private void UpdatePopup()
    {
        RefreshPopupVisuals(out bool overMinus, out bool overPlus, out bool overBuy, out bool overNo);
        bool clickMinus = minusBox.TickPress(overMinus, popupCanMinus);
        bool clickPlus = plusBox.TickPress(overPlus, popupCanPlus);
        bool clickBuy = popupBuyBox.TickButton(overBuy, popupCanBuy);
        bool clickNo = popupNoBox.TickButton(overNo, true);
        // 살 수 없어 어두운 [구매하기]도 호버음은 냅니다(누르면 구매 실패음).
        UISound.TrackHover(ref lastHoveredButton, UISound.HoveredId(30, overMinus, overPlus, overBuy || IsPointerOver(popupBuyRect), overNo), Time.frameCount == popupOpenedFrame);

        // 팝업을 연 그 프레임의 클릭(카드의 [구매하기])은 무시합니다.
        if (Time.frameCount == popupOpenedFrame) return;

        if (Input.GetKeyDown(closeKey)) { UISound.Cancel(); ClosePopup(); return; }

        if (clickNo) { UISound.Cancel(); ClosePopup(); return; }
        if (clickMinus) { UISound.Confirm(); popupQuantity--; return; }
        if (clickPlus) { UISound.Confirm(); popupQuantity++; return; }
        if (clickBuy)
        {
            BuyFromPopup();
        }
        else if (!popupCanBuy && Input.GetMouseButtonDown(0) && IsPointerOver(popupBuyRect))
        {
            GameAudio.Play(SfxId.ShopPurchaseFail); // 살 수 없는 상태(재화 부족 등)에서 [구매하기]를 누름
        }
        // 팝업 바깥(뒤의 상점 창 포함) 클릭은 무시합니다.
    }

    // [구매하기]: 슬롯 아이템을 사서 장착 중인 아이템이 교체되는데 그 아이템에 "교체 시 확인"이 체크돼 있으면, 구매 팝업을 닫고
    // 슬롯 교체 확인창(SlotReplaceConfirmUI)을 먼저 띄워 [예]일 때만 삽니다(재화도 그때 냄). 아니면 바로 삽니다.
    private void BuyFromPopup()
    {
        ShopNPC npc = currentNpc;
        ShopListing listing = popupListing;
        int quantity = popupQuantity;
        ClosePopup();

        ItemDefinition equipped = npc.GetEquippedItemReplacedBy(listing);
        if (SlotReplaceConfirmUI.NeedsConfirm(equipped))
        {
            UISound.Confirm(); // 바로 사지 않고 교체 확인창을 띄울 때(바로 사면 구매음 - ShopNPC)
            SlotReplaceConfirmUI.Show(equipped, listing.item, () =>
            {
                if (npc != null) npc.TryPurchase(listing, quantity); // 그 사이 상황이 바뀌었으면 TryPurchase가 다시 막음
            });
            return;
        }

        npc.TryPurchase(listing, quantity);
    }

    // 재화 부족은 가격 숫자 색으로만 알립니다(notEnoughCurrencyMessage를 채우면 문구도 표시).
    private string GetBlockMessage(ShopNPC.PurchaseBlock block)
    {
        switch (block)
        {
            case ShopNPC.PurchaseBlock.HealthFull: return healthFullMessage;
            case ShopNPC.PurchaseBlock.AlreadyOwned: return alreadyOwnedMessage;
            case ShopNPC.PurchaseBlock.NoHoldingSpace: return noHoldingSpaceMessage;
            case ShopNPC.PurchaseBlock.NotEnoughCurrency: return notEnoughCurrencyMessage;
            default: return "";
        }
    }

    // 구매 팝업 이름 판의 이름 색: 플레이어 아이템이면 등급 색, 소모품이면 흰색.
    private Color PopupNameColor(ShopListing listing)
    {
        if (!popupNameUseTierColor || listing.isBasic || listing.item == null) return Color.white;
        return GetPopupTierColor(listing.item.rarity);
    }

    // 씬의 아이템 설명칸(PlayerItemInfoDisplay)과 같은 등급 색(슬롯 교체 확인창과 같은 규칙). 없으면 이 컴포넌트의 기본값.
    private Color GetPopupTierColor(ItemDefinition.ItemRarity rarity)
    {
        if (popupNameFollowItemInfoColors)
        {
            var info = UnityEngine.Object.FindFirstObjectByType<PlayerItemInfoDisplay>();
            if (info != null && info.useTierNameColor)
            {
                switch (rarity)
                {
                    case ItemDefinition.ItemRarity.Tier0: return info.tier0NameColor;
                    case ItemDefinition.ItemRarity.Tier1: return info.tier1NameColor;
                    case ItemDefinition.ItemRarity.Tier2: return info.tier2NameColor;
                    case ItemDefinition.ItemRarity.Tier3: return info.tier3NameColor;
                }
            }
        }
        switch (rarity)
        {
            case ItemDefinition.ItemRarity.Tier0: return popupTier0NameColor;
            case ItemDefinition.ItemRarity.Tier1: return popupTier1NameColor;
            case ItemDefinition.ItemRarity.Tier2: return popupTier2NameColor;
            case ItemDefinition.ItemRarity.Tier3: return popupTier3NameColor;
            default: return Color.white;
        }
    }

    // ------------------------------------------------------------------
    // 표시 내용
    // ------------------------------------------------------------------

    // 아이콘 틀: 소모성 재화는 기본 틀, 플레이어 아이템은 등급별 틀(스프라이트가 비어 있으면 등급별 색)
    // 아이콘 틀(등급 칸) 그라데이션 두 색: 소모품이면 소모품 색, 플레이어 아이템이면 등급별 색
    private void GetIconFrameColors(ShopListing listing, out Color top, out Color bottom)
    {
        if (listing.isBasic || listing.item == null)
        {
            top = basicFrameColorTop;
            bottom = basicFrameColorBottom;
            return;
        }

        switch (listing.item.rarity)
        {
            case ItemDefinition.ItemRarity.Tier0: top = tier0FrameColorTop; bottom = tier0FrameColorBottom; break;
            case ItemDefinition.ItemRarity.Tier1: top = tier1FrameColorTop; bottom = tier1FrameColorBottom; break;
            case ItemDefinition.ItemRarity.Tier2: top = tier2FrameColorTop; bottom = tier2FrameColorBottom; break;
            case ItemDefinition.ItemRarity.Tier3: top = tier3FrameColorTop; bottom = tier3FrameColorBottom; break;
            default: top = basicFrameColorTop; bottom = basicFrameColorBottom; break;
        }
    }

    // [86번] 가격 옆 아이콘: 레벨 한정 무료 상품이면 그 할인을 준 아이템의 아이콘, 아니면 재화 아이콘입니다.
    private Sprite GetPriceIcon(ShopListing listing)
    {
        Sprite free = GetLevelFreeIcon(listing);
        return free != null ? free : GetCurrencyIcon(listing.currency);
    }

    // [86번] 플레이어 아이템 상품이 지금 레벨 한정 무료(원래 가격이 있는데 0이 됨)이면 등록된 아이콘, 아니면 null.
    private static Sprite GetLevelFreeIcon(ShopListing listing)
    {
        if (listing == null || listing.isBasic || listing.item == null || listing.unitPrice <= 0) return null;
        return ShopPriceModifier.GetLevelFreeIcon(listing.item);
    }

    // 재화 아이콘: 이 컴포넌트의 칸 → 씬의 자원 카운터 아이콘 → 없음(null) 순서.
    private Sprite GetCurrencyIcon(ShopCurrency currency)
    {
        Sprite own = currency == ShopCurrency.Pyroxenes ? pyroxenesIcon : creditIcon;
        if (own != null) return own;

        var resourceDisplay = UnityEngine.Object.FindFirstObjectByType<PlayerResourceDisplay>();
        if (resourceDisplay != null)
        {
            return currency == ShopCurrency.Pyroxenes ? resourceDisplay.pyroxenesIcon : resourceDisplay.creditIcon;
        }
        return null;
    }

    // ------------------------------------------------------------------
    // 플레이어 조작 잠금 (SlotReplaceConfirmUI와 같은 방식 - 게임은 계속 흐름)
    // ------------------------------------------------------------------

    private void DisablePlayerControls(PlayerHealth health)
    {
        disabledBehaviours.Clear();

        GameObject player = health != null ? health.gameObject : null;

        // 걷기/조준 컴포넌트를 끄면 Animator 값이 마지막 상태로 남으므로, 끄기 전에 멈춘 상태로 맞춥니다.
        var movement = FindOnPlayer<PlayerMovement>(player);
        var aim = FindOnPlayer<PlayerAimAndAttack>(player);
        ResetLocomotionAnimator(movement != null ? movement.animator : null, aim != null);

        TryDisable(movement);
        TryDisable(aim);
        TryDisable(FindOnPlayer<PlayerShooting>(player));
        TryDisable(FindOnPlayer<PlayerWeaponMode>(player));
        TryDisable(FindOnPlayer<PlayerActiveItem>(player));
        TryDisable(FindOnPlayer<PlayerDisposableItem>(player));
        TryDisable(FindOnPlayer<PlayerFoodItem>(player));
    }

    private static T FindOnPlayer<T>(GameObject player) where T : Component
    {
        T c = player != null ? player.GetComponent<T>() : null;
        return c != null ? c : UnityEngine.Object.FindFirstObjectByType<T>();
    }

    private static void ResetLocomotionAnimator(Animator animator, bool resetAiming)
    {
        if (animator == null || animator.runtimeAnimatorController == null) return;
        foreach (var p in animator.parameters)
        {
            if (p.type == AnimatorControllerParameterType.Float &&
                (p.name == "MoveX" || p.name == "MoveZ" || p.name == "Speed"))
                animator.SetFloat(p.name, 0f);
            else if (resetAiming && p.type == AnimatorControllerParameterType.Bool && p.name == "IsAiming")
                animator.SetBool(p.name, false);
        }
    }

    private void TryDisable(Behaviour behaviour)
    {
        if (behaviour == null || !behaviour.enabled) return;
        behaviour.enabled = false;
        disabledBehaviours.Add(behaviour);
    }

    private void RestorePlayerControls()
    {
        foreach (var b in disabledBehaviours)
        {
            if (b != null) b.enabled = true;
        }
        disabledBehaviours.Clear();
    }

    // ------------------------------------------------------------------
    // UI 생성
    // ------------------------------------------------------------------

    private void BuildUI()
    {
        canvasGO = new GameObject("ShopCanvas");
        canvasGO.transform.SetParent(transform, false);
        canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        panelRect = CreateRect(canvasGO.transform, "Panel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        panelBox = UIPatternBox.AddTo(panelRect.gameObject);
        panelGroup = panelRect.gameObject.AddComponent<CanvasGroup>(); // 열기/닫기 연출 - 창 안 모든 것(카드/버튼)이 함께 투명해짐
        panelGroup.interactable = false;
        panelGroup.blocksRaycasts = false;

        for (int i = 0; i < cards.Length; i++)
        {
            cards[i] = CreateCard(panelRect, i);
        }

        CreateButton(panelRect, "RefreshButton", new Vector2(1f, 0f), out refreshRect, out refreshBox, out refreshText);
        CreateButton(panelRect, "ExitButton", new Vector2(1f, 0f), out exitRect, out exitBox, out exitText);

        // 팝업이 떠 있는 동안 상점 창 위를 덮는 막(팝업보다 아래 - ApplyLayout에서 순서를 맞춤)
        popupDimRect = CreateRect(canvasGO.transform, "PopupDim", Vector2.zero, new Vector2(0.5f, 0.5f));
        popupDimRect.anchorMin = Vector2.zero;
        popupDimRect.anchorMax = Vector2.one;
        popupDimRect.offsetMin = Vector2.zero;
        popupDimRect.offsetMax = Vector2.zero;
        AddImage(popupDimRect);
        popupDimRect.gameObject.SetActive(false);

        BuildPopup();
        BuildRefreshPopup();
    }

    private void BuildRefreshPopup()
    {
        var center = new Vector2(0.5f, 0.5f);
        var top = new Vector2(0.5f, 1f);
        var bottom = new Vector2(0.5f, 0f);

        refreshPopupRect = CreateRect(canvasGO.transform, "RefreshPopup", center, center);
        refreshPopupBox = UIPatternBox.AddTo(refreshPopupRect.gameObject);
        refreshTitleText = CreateText(refreshPopupRect, "Title", top, top, TextAnchor.MiddleCenter);
        refreshReasonText = CreateText(refreshPopupRect, "Reason", top, top, TextAnchor.MiddleCenter);
        CreateButton(refreshPopupRect, "RefreshYesButton", bottom, out refreshYesRect, out refreshYesBox, out refreshYesText);
        CreateButton(refreshPopupRect, "RefreshNoButton", bottom, out refreshNoRect, out refreshNoBox, out refreshNoText);
        refreshPopupRect.gameObject.SetActive(false);
    }

    private void BuildPopup()
    {
        // 상점 틀보다 나중에 만들어서(= 위에 그려짐) 팝업이 상점 창을 덮습니다.
        var center = new Vector2(0.5f, 0.5f);
        var top = new Vector2(0.5f, 1f);

        popupRect = CreateRect(canvasGO.transform, "BuyPopup", center, center);
        popupBox = UIPatternBox.AddTo(popupRect.gameObject);

        popupNamePlate = UINamePlate.Create(popupRect, "TitleNamePlate");
        popupNamePlate.rect.anchorMin = popupNamePlate.rect.anchorMax = top;
        popupNamePlate.rect.pivot = top;
        popupTitleText = CreateText(popupRect, "Title", top, top, TextAnchor.MiddleCenter);

        CreateButton(popupRect, "MinusButton", top, out minusRect, out minusBox, out minusText);
        CreateButton(popupRect, "PlusButton", top, out plusRect, out plusBox, out plusText);
        quantityBoxRect = CreateRect(popupRect, "QuantityBox", top, top);
        quantityBox = UIPatternBox.AddTo(quantityBoxRect.gameObject);
        quantityText = CreateText(quantityBoxRect, "Quantity", center, center, TextAnchor.MiddleCenter);

        reasonText = CreateText(popupRect, "Reason", top, top, TextAnchor.MiddleCenter);

        var bottom = new Vector2(0.5f, 0f);
        popupBuyRect = CreateRect(popupRect, "BuyButton", bottom, bottom);
        popupBuyBox = UIPatternBox.AddTo(popupBuyRect.gameObject);
        popupPriceBoxRect = CreateRect(popupBuyRect, "PriceBox", new Vector2(0f, 1f), new Vector2(0f, 0.5f));
        popupPriceBox = UIPatternBox.AddTo(popupPriceBoxRect.gameObject);
        popupPriceText = CreateText(popupPriceBoxRect, "Price", center, center, TextAnchor.MiddleCenter);
        popupPriceText.richText = true; // 할인 가격 표시(원래 가격 글자 크기/색)에 필요
        popupStrikeRect = CreateRect(popupPriceText.rectTransform, "OriginalPriceStrike", center, center);
        popupStrikeLine = AddImage(popupStrikeRect);
        popupStrikeRect.gameObject.SetActive(false);
        popupCurrencyRect = CreateRect(popupBuyRect, "CurrencyIcon", new Vector2(0f, 1f), new Vector2(0f, 0.5f));
        popupCurrencyIcon = AddImage(popupCurrencyRect);
        popupCurrencyIcon.preserveAspect = true;
        popupBuyText = CreateText(popupBuyRect, "BuyLabel", bottom, bottom, TextAnchor.MiddleCenter);

        CreateButton(popupRect, "NoButton", bottom, out popupNoRect, out popupNoBox, out popupNoText);

        popupRect.gameObject.SetActive(false);
    }

    // 흐르는 이름 구역을 만듭니다: 마스크(RectMask2D)가 붙은 구역 + 그 안의 글자(구역 왼쪽 가운데 기준).
    private static Marquee CreateMarquee(Transform parent, string name, Vector2 anchor)
    {
        var marquee = new Marquee();
        marquee.viewport = CreateRect(parent, name + "Area", anchor, anchor);
        marquee.mask = marquee.viewport.gameObject.AddComponent<RectMask2D>();
        // 사라짐/나타남은 구역 전체 투명도로 - 이름에 색 태그(등급 색)가 있어도 함께 투명해지도록
        marquee.group = marquee.viewport.gameObject.AddComponent<CanvasGroup>();
        marquee.group.interactable = false;
        marquee.group.blocksRaycasts = false;

        marquee.text = CreateText(marquee.viewport, name, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), TextAnchor.MiddleCenter);
        marquee.text.enableAutoSizing = false;
        marquee.text.richText = true;
        return marquee;
    }

    // ------------------------------------------------------------------
    // 흐르는 이름 (형빈 확정 - 글자 크기는 그대로, 칸보다 긴 이름만 왼쪽으로 천천히 흐름)
    // 처음 멈춤 → 왼쪽으로 흐름 → 끝까지 보이면 멈춤 → 살짝 사라졌다가 처음 위치에서 다시 나타남 → 반복.
    // 구역 밖 글자는 RectMask2D가 잘라내므로 칸을 뚫고 나가지 않고, 가장자리는 Softness로 부드럽게 흐려집니다.
    // 칸보다 짧은 이름은 가운데 정렬로 멈춰 있습니다. 이름(또는 글자 크기)이 바뀌면 처음부터 다시 시작합니다.
    // ------------------------------------------------------------------
    private class Marquee
    {
        public RectTransform viewport;
        public RectMask2D mask;
        public CanvasGroup group;
        public TextMeshProUGUI text;

        private enum Phase { StartWait, Scrolling, EndWait, FadeOut, FadeIn }

        private string shownText;
        private float shownFontSize = -1f;
        private float textWidth;
        private Phase phase;
        private float timer;
        private float offset;
        private float alpha = 1f;
        private int lastStepFrame = -1; // 각 이름 칸이 프레임당 한 번만 움직이도록(ApplyLayout이 한 프레임에 여러 번 불려도)

        public void Reset()
        {
            shownText = null;
        }

        public void Update(ShopUI ui, string value, float fontSize, Color color, Vector2 viewportSize)
        {
            float dt = 0f;
            if (lastStepFrame != Time.frameCount)
            {
                lastStepFrame = Time.frameCount;
                dt = Time.unscaledDeltaTime;
            }

            if (value == null) value = "";
            viewport.sizeDelta = viewportSize;
            mask.softness = new Vector2Int(Mathf.RoundToInt(ui.nameMaskSoftness), 0);
            text.fontSize = fontSize;

            if (value != shownText || !Mathf.Approximately(fontSize, shownFontSize))
            {
                shownText = value;
                shownFontSize = fontSize;
                text.text = value;
                textWidth = MeasureWidth(value);
                phase = Phase.StartWait;
                timer = 0f;
                offset = 0f;
                alpha = 1f;
            }

            // 처음 잴 때 글꼴 준비 전이라 폭이 0으로 읽혔으면 다시 잽니다.
            if (textWidth < 1f && value.Length > 0) textWidth = MeasureWidth(value);

            var rect = text.rectTransform;
            float range = textWidth - viewportSize.x;
            if (range <= 0.5f)
            {
                // 칸에 들어가는 이름: 가운데 정렬로 멈춤
                alpha = 1f;
                text.alignment = TextAlignmentOptions.Center;
                rect.sizeDelta = viewportSize;
                rect.anchoredPosition = Vector2.zero;
            }
            else
            {
                Step(ui, range, dt);
                text.alignment = TextAlignmentOptions.Left;
                rect.sizeDelta = new Vector2(textWidth + 2f, viewportSize.y);
                rect.anchoredPosition = new Vector2(-offset, 0f);
            }

            text.color = color;
            group.alpha = alpha;
        }

        // 줄바꿈 없이 한 줄로 썼을 때의 실제 글자 폭
        private float MeasureWidth(string value)
        {
            return text.GetPreferredValues(value, float.PositiveInfinity, float.PositiveInfinity).x;
        }

        private void Step(ShopUI ui, float range, float dt)
        {
            float fade = Mathf.Max(0.01f, ui.nameScrollFadeDuration);
            switch (phase)
            {
                case Phase.StartWait:
                    alpha = 1f;
                    timer += dt;
                    if (timer >= ui.nameScrollStartDelay) { phase = Phase.Scrolling; timer = 0f; }
                    break;
                case Phase.Scrolling:
                    offset += Mathf.Max(1f, ui.nameScrollSpeed) * dt;
                    if (offset >= range) { offset = range; phase = Phase.EndWait; timer = 0f; }
                    break;
                case Phase.EndWait:
                    offset = range;
                    timer += dt;
                    if (timer >= ui.nameScrollEndDelay) { phase = Phase.FadeOut; timer = 0f; }
                    break;
                case Phase.FadeOut:
                    timer += dt;
                    alpha = 1f - Mathf.Clamp01(timer / fade);
                    if (timer >= fade) { offset = 0f; alpha = 0f; phase = Phase.FadeIn; timer = 0f; }
                    break;
                case Phase.FadeIn:
                    timer += dt;
                    alpha = Mathf.Clamp01(timer / fade);
                    if (timer >= fade) { alpha = 1f; phase = Phase.StartWait; timer = 0f; }
                    break;
            }
            if (offset > range) offset = range; // 칸 크기가 바뀌어 범위가 줄어든 경우
        }
    }

    private static RectTransform CreateRect(Transform parent, string name, Vector2 anchor, Vector2 pivot)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = pivot;
        return rect;
    }

    private static Image AddImage(RectTransform rect)
    {
        var image = rect.gameObject.AddComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    private static TextMeshProUGUI CreateText(Transform parent, string name, Vector2 anchor, Vector2 pivot, TextAnchor alignment)
    {
        var rect = CreateRect(parent, name, anchor, pivot);
        return HUDText.AddTo(rect.gameObject, alignment);
    }

    private CardView CreateCard(Transform parent, int index)
    {
        var top = new Vector2(0.5f, 1f);
        var card = new CardView();

        // 카드 기준점 = 왼쪽 위(틀의 왼쪽 위에서 칸 위치만큼 떨어짐)
        card.root = CreateRect(parent, $"Card_{index + 1}", new Vector2(0f, 1f), new Vector2(0f, 1f));
        card.background = UIPatternBox.AddTo(card.root.gameObject);
        card.group = card.root.gameObject.AddComponent<CanvasGroup>(); // 열기 연출 - 카드 안 모든 것이 함께 투명해짐
        card.group.interactable = false;
        card.group.blocksRaycasts = false;

        card.nameMarquee = CreateMarquee(card.root, "Name", top);
        card.nameText = card.nameMarquee.text;

        card.iconFrameRect = CreateRect(card.root, "IconFrame", top, top);
        card.iconFrame = UIPatternBox.AddTo(card.iconFrameRect.gameObject);
        card.iconRect = CreateRect(card.iconFrameRect, "Icon", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        card.icon = AddImage(card.iconRect);
        card.icon.preserveAspect = true;
        card.amountText = CreateText(card.iconFrameRect, "Amount", new Vector2(1f, 0f), new Vector2(1f, 0f), TextAnchor.LowerRight);

        card.priceFrameRect = CreateRect(card.root, "PriceFrame", top, top);
        card.priceFrame = UIPatternBox.AddTo(card.priceFrameRect.gameObject);
        card.priceBoxRect = CreateRect(card.priceFrameRect, "PriceBox", new Vector2(0f, 1f), new Vector2(0f, 0.5f));
        card.priceBox = UIPatternBox.AddTo(card.priceBoxRect.gameObject);
        card.priceText = CreateText(card.priceBoxRect, "Price", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), TextAnchor.MiddleCenter);
        card.priceText.richText = true; // 할인 가격 표시(원래 가격 글자 크기/색)에 필요
        // 가격 글자의 자식으로 만들어서 글자 위에 그려지고, 글자와 같은 좌표 기준으로 위치를 맞춥니다.
        card.strikeRect = CreateRect(card.priceText.rectTransform, "OriginalPriceStrike", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        card.strikeLine = AddImage(card.strikeRect);
        card.strikeRect.gameObject.SetActive(false);
        // 재화 아이콘은 가격 칸보다 나중에 만들어서 칸 위에 살짝 겹쳐 그려집니다(구현0처럼).
        card.currencyRect = CreateRect(card.priceFrameRect, "CurrencyIcon", new Vector2(0f, 1f), new Vector2(0f, 0.5f));
        card.currencyIcon = AddImage(card.currencyRect);
        card.currencyIcon.preserveAspect = true;
        card.buyText = CreateText(card.priceFrameRect, "BuyLabel", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), TextAnchor.MiddleCenter);

        // 팔린 카드 덮개 - 카드 전체를 덮도록 마지막에 만들고 카드 크기로 늘립니다.
        card.soldOutRect = CreateRect(card.root, "SoldOutOverlay", Vector2.zero, new Vector2(0.5f, 0.5f));
        card.soldOutRect.anchorMin = Vector2.zero;
        card.soldOutRect.anchorMax = Vector2.one;
        card.soldOutRect.offsetMin = Vector2.zero;
        card.soldOutRect.offsetMax = Vector2.zero;
        card.soldOutOverlay = UIPatternBox.AddTo(card.soldOutRect.gameObject);
        card.soldOutRect.gameObject.SetActive(false);
        return card;
    }

    private static void CreateButton(Transform parent, string name, Vector2 anchor, out RectTransform rect, out UIPatternBox box, out TextMeshProUGUI label)
    {
        rect = CreateRect(parent, name, anchor, anchor);
        box = UIPatternBox.AddTo(rect.gameObject);

        var labelRect = CreateRect(rect, "Label", Vector2.zero, new Vector2(0.5f, 0.5f));
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        label = HUDText.AddTo(labelRect.gameObject, TextAnchor.MiddleCenter);
    }

    // ------------------------------------------------------------------
    // 배치 (인스펙터 값을 매 프레임 반영 - Play 중 바꾸면 즉시 반영)
    // ------------------------------------------------------------------

    private void ApplyLayout()
    {
        canvas.sortingOrder = canvasSortingOrder;
        if (Screen.height > 0)
        {
            float screenAspect = (float)Screen.width / Screen.height;
            scaler.matchWidthOrHeight = screenAspect > 1920f / 1080f ? 1f : 0f; // HUD와 같은 규칙
        }

        Vector2 card = new Vector2(Mathf.Max(60f, cardSize.x), Mathf.Max(80f, cardSize.y));
        Vector2 panelSize = new Vector2(
            panelPadding * 2f + card.x * Columns + cardSpacing.x * (Columns - 1),
            panelPadding + card.y * Rows + cardSpacing.y * (Rows - 1) + bottomBarHeight);

        panelRect.sizeDelta = panelSize;
        panelRect.anchoredPosition = panelOffset;
        var style = PopupStyle.Get();
        panelBox.SetupPanel(style);

        var listings = currentNpc != null ? currentNpc.Listings : null;
        for (int i = 0; i < cards.Length; i++)
        {
            ShopListing listing = (listings != null && i < listings.Count) ? listings[i] : null;
            bool show = listing != null; // 팔린 칸도 카드는 남기고 덮개만 씌웁니다(형빈 확정)
            var view = cards[i];
            if (view.root.gameObject.activeSelf != show) view.root.gameObject.SetActive(show);
            if (!show) continue;

            int col = i % Columns;
            int row = i / Columns;
            view.root.sizeDelta = card;
            view.root.anchoredPosition = new Vector2(panelPadding + col * (card.x + cardSpacing.x),
                                                     -(panelPadding + row * (card.y + cardSpacing.y)));
            LayoutCard(view, listing, card);

            bool sold = listing.soldOut;
            if (view.soldOutRect.gameObject.activeSelf != sold) view.soldOutRect.gameObject.SetActive(sold);
            if (sold)
            {
                view.soldOutRect.SetAsLastSibling();
                view.soldOutOverlay.SetupShape(PopupStyle.Get(), soldOutOverlayColor, cardCornerRadius, cardSlantAngle, false);
            }
        }

        // 하단 버튼: 오른쪽부터 [나가기], 그 왼쪽에 [갱신하기](갱신 횟수를 다 쓰면 어둡게 - 누를 수 없음)
        float buttonY = Mathf.Max(0f, (bottomBarHeight - bottomButtonSize.y) * 0.5f);
        exitBox.SetupButton(style, UIButtonKind.Quit);
        LayoutLabelButton(exitRect, exitText, exitLabel, bottomButtonSize, new Vector2(-panelPadding, buttonY),
                          bottomButtonFontSize, exitBox.GetLabelColor(true));

        bool refreshAvailable = currentNpc != null && currentNpc.RemainingRefreshes > 0;
        refreshBox.SetupButton(style, UIButtonKind.Normal);
        LayoutLabelButton(refreshRect, refreshText, refreshLabel, bottomButtonSize,
                          new Vector2(-panelPadding - bottomButtonSize.x - bottomButtonSpacing, buttonY),
                          bottomButtonFontSize, refreshBox.GetLabelColor(refreshAvailable));

        // 팝업이 떠 있으면 막(상점 창 위) → 팝업 순서로 맨 위에 둡니다.
        bool anyPopup = IsPopupOpen || refreshPopupOpen;
        if (popupDimRect.gameObject.activeSelf != anyPopup) popupDimRect.gameObject.SetActive(anyPopup);
        if (anyPopup)
        {
            popupDimRect.GetComponent<Image>().color = popupDimColor;
            popupDimRect.SetAsLastSibling();
        }

        if (IsPopupOpen) LayoutPopup();
        if (refreshPopupOpen) LayoutRefreshPopup();

        ApplyOpenVisual();
    }

    private void LayoutRefreshPopup()
    {
        Vector2 size = new Vector2(Mathf.Max(200f, refreshPopupSize.x), Mathf.Max(120f, refreshPopupSize.y));
        refreshPopupRect.sizeDelta = size;
        refreshPopupRect.anchoredPosition = refreshPopupOffset;
        refreshPopupRect.SetAsLastSibling();
        var style = PopupStyle.Get();
        refreshPopupBox.SetupPanel(style);

        float innerWidth = size.x - popupPadding * 2f;
        float buttonsTop = refreshButtonBottomMargin + refreshButtonSize.y;

        // 제목: 위 여백 ~ 버튼 사이 공간의 위쪽, 안내 문구: 그 아래 한 줄
        float reasonHeight = reasonFontSize + 8f;
        float titleHeight = Mathf.Max(refreshTitleFontSize + 10f, size.y - popupPadding - buttonsTop - reasonHeight - 6f);
        var titleRect = refreshTitleText.rectTransform;
        titleRect.sizeDelta = new Vector2(innerWidth, titleHeight);
        titleRect.anchoredPosition = new Vector2(0f, -popupPadding);
        refreshTitleText.fontSize = refreshTitleFontSize;
        refreshTitleText.color = style.panelTextColor;

        var reasonRect = refreshReasonText.rectTransform;
        reasonRect.sizeDelta = new Vector2(innerWidth, reasonHeight);
        reasonRect.anchoredPosition = new Vector2(0f, -popupPadding - titleHeight) + refreshReasonOffset;
        refreshReasonText.fontSize = reasonFontSize;
        refreshReasonText.color = reasonTextColor;

        float half = (refreshButtonSize.x + refreshButtonSpacing) * 0.5f;
        refreshYesBox.SetupButton(style, UIButtonKind.Positive);
        refreshNoBox.SetupButton(style, UIButtonKind.Normal);
        LayoutLabelButton(refreshYesRect, refreshYesText, refreshConfirmLabel, refreshButtonSize,
                          new Vector2(-half, refreshButtonBottomMargin), refreshButtonFontSize,
                          refreshYesBox.GetLabelColor(refreshPopupCanRefresh));
        LayoutLabelButton(refreshNoRect, refreshNoText, refreshNoLabel, refreshButtonSize,
                          new Vector2(half, refreshButtonBottomMargin), refreshButtonFontSize, refreshNoBox.GetLabelColor(true));
    }

    // 카드 안 요소 배치 도우미: 카드 위에서 top만큼 내려온, 높이 h인 요소의 (전체 폭, 가로 위치). 카드가 평행사변형이면 요소도 같은
    // 기울기로 그려지므로, 양옆 여백이 일정하도록 폭을 넓히고 높이에 따라 옆으로 옮깁니다(위쪽 요소는 오른쪽, 아래쪽은 왼쪽).
    private Vector2 CardInner(Vector2 card, float top, float h, float slantTan)
    {
        float yFromCenter = card.y * 0.5f - top - h * 0.5f;
        float width = Mathf.Max(10f, card.x - card.y * slantTan + h * slantTan - cardPadding * 2f);
        return new Vector2(width, yFromCenter * slantTan);
    }

    private void LayoutCard(CardView view, ShopListing listing, Vector2 card)
    {
        var style = PopupStyle.Get();
        float slantTan = Mathf.Tan(Mathf.Clamp(cardSlantAngle, 0f, 30f) * Mathf.Deg2Rad);

        view.background.SetupShape(style, cardColorTop, cardCornerRadius, cardSlantAngle, cardShadow);
        view.background.SetGradient(true, cardColorBottom);
        view.background.SetPattern(cardPattern, cardPatternTriangleSide, cardPatternSpread, cardPatternMaxAlpha, Color.white);

        float y = cardPadding;

        // 이름 (모두 PopupStyle 글자 색, 글자 크기 고정 - 칸보다 길면 흐르는 이름)
        float nameHeight = nameFontSize + 10f;
        Vector2 nameInner = CardInner(card, y, nameHeight, slantTan);
        view.nameMarquee.viewport.anchoredPosition = new Vector2(nameInner.y, -y);
        view.nameMarquee.Update(this, listing.displayName, nameFontSize, style.panelTextColor,
                                new Vector2(Mathf.Max(10f, nameInner.x - nameHeight * slantTan - nameAreaPadding * 2f), nameHeight));
        y += nameHeight + cardElementSpacing;

        // 아이콘 틀 (남는 높이를 전부 사용, 등급별 그라데이션 + 흰 무늬)
        float iconFrameHeight = Mathf.Max(20f, card.y - y - cardElementSpacing - priceFrameHeight - cardPadding);
        Vector2 frameInner = CardInner(card, y, iconFrameHeight, slantTan);
        Vector2 iconFrameSize = new Vector2(frameInner.x, iconFrameHeight);
        view.iconFrameRect.sizeDelta = iconFrameSize;
        view.iconFrameRect.anchoredPosition = new Vector2(frameInner.y, -y);
        GetIconFrameColors(listing, out Color frameTop, out Color frameBottom);
        view.iconFrame.SetupShape(style, frameTop, iconFrameCornerRadius, cardSlantAngle, false);
        view.iconFrame.SetGradient(true, frameBottom);
        view.iconFrame.SetPattern(iconFramePattern, iconFramePatternTriangleSide, iconFramePatternSpread, iconFramePatternMaxAlpha, Color.white);

        // 아이콘은 기울어진 틀의 몸통(가운데 높이에서의 폭) 안에 들어가는 정사각형
        float frameBodyWidth = iconFrameSize.x - iconFrameHeight * slantTan;
        float iconSide = Mathf.Max(4f, Mathf.Min(frameBodyWidth, iconFrameSize.y) - iconPadding * 2f);
        view.iconRect.sizeDelta = new Vector2(iconSide, iconSide);
        view.iconRect.anchoredPosition = Vector2.zero;
        view.icon.sprite = listing.icon;
        view.icon.color = listing.icon != null ? Color.white : missingIconColor;

        bool bundle = listing.amountPerPurchase > 1;
        if (view.amountText.gameObject.activeSelf != bundle) view.amountText.gameObject.SetActive(bundle);
        if (bundle)
        {
            var amountRect = view.amountText.rectTransform;
            amountRect.sizeDelta = new Vector2(iconFrameSize.x, amountFontSize + 6f);
            amountRect.anchoredPosition = new Vector2(-6f - iconFrameHeight * slantTan, 4f); // 기울어진 틀의 오른쪽 아래 끝 안쪽
            view.amountText.text = $"x{listing.amountPerPurchase}";
            view.amountText.fontSize = amountFontSize;
            view.amountText.color = amountColor;
        }
        y += iconFrameHeight + cardElementSpacing;

        // 가격표 틀 = 하늘색 평행사변형 버튼(색/마우스 올림/누름은 Update의 TickButton)
        Vector2 priceInner = CardInner(card, y, priceFrameHeight, slantTan);
        Vector2 priceFrameSize = new Vector2(priceInner.x, priceFrameHeight);
        view.priceFrameRect.sizeDelta = priceFrameSize;
        view.priceFrameRect.anchoredPosition = new Vector2(priceInner.y, -y);
        view.priceFrame.SetupButton(style, UIButtonKind.Normal);
        view.priceFrame.SetSlant(cardSlantAngle);

        LayoutPriceRow(priceFrameSize, view.currencyRect, view.currencyIcon, view.priceBoxRect, view.priceBox, view.priceText,
                       view.buyText, listing.currency,
                       FormatPrice(listing.unitPrice, ShopNPC.GetUnitPrice(listing), ShopNPC.IsPriceModified(listing)), priceTextColor,
                       currencyIconSize, currencyIconOffset, priceBoxLeftInset, priceTextOffset, priceFontSize, buyLabelFontSize,
                       view.priceFrame.GetLabelColor(true), GetLevelFreeIcon(listing),
                       priceFrameHeight * 0.5f * slantTan, priceBoxOffset, priceBoxSizeAdjust, cardSlantAngle);
        UpdateStrikeLine(view.priceText, view.strikeRect, view.strikeLine, ShopNPC.IsPriceModified(listing),
                         listing.unitPrice.ToString().Length);
    }

    // 가격 글자: 보정이 없으면 숫자만, 있으면 "원래 가격(회색, 조금 작게)  실제 가격"입니다. 원래 가격 위의 빨간 줄은 글자가 아니라
    // 따로 그리는 이미지입니다(UpdateStrikeLine - 굵기/위치를 인스펙터에서 조정). 실제 가격 색은 가격 글자 색(재화가 부족하면
    // 빨강)을 그대로 따릅니다. 매 프레임 다시 만들어서 가격이 바뀌면 바로 반영됩니다.
    private string FormatPrice(int originalPrice, int currentPrice, bool modified)
    {
        if (!modified) return currentPrice.ToString();
        string grey = ColorUtility.ToHtmlStringRGBA(originalPriceColor);
        string size = originalPriceSizePercent.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
        string space = originalPriceSpacing.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
        return $"<size={size}%><color=#{grey}>{originalPrice}</color></size><space={space}>{currentPrice}";
    }

    // 원래 가격 위의 줄: 가격 글자의 맨 앞 originalDigits개 글자(= 원래 가격 숫자)가 실제로 그려진 범위를 재서, 그 가운데에 줄을
    // 놓습니다. 숫자 자리수/글자 크기/위치가 바뀌어도 따라갑니다. 할인 중이 아니면 숨깁니다.
    private void UpdateStrikeLine(TextMeshProUGUI text, RectTransform strikeRect, Image strikeLine, bool modified, int originalDigits)
    {
        if (strikeRect == null) return;
        bool show = modified && text != null && originalDigits > 0;
        if (show)
        {
            text.ForceMeshUpdate(); // 방금 바꾼 글자 기준으로 글자 위치를 계산
            var info = text.textInfo;
            if (info == null || info.characterCount < originalDigits) show = false;
            else
            {
                float left = float.MaxValue, right = float.MinValue, bottom = float.MaxValue, top = float.MinValue;
                for (int i = 0; i < originalDigits; i++)
                {
                    var ch = info.characterInfo[i];
                    left = Mathf.Min(left, ch.bottomLeft.x);
                    right = Mathf.Max(right, ch.topRight.x);
                    bottom = Mathf.Min(bottom, ch.bottomLeft.y);
                    top = Mathf.Max(top, ch.topRight.y);
                }
                float width = Mathf.Max(0f, right - left + originalPriceStrikeExtend * 2f);
                strikeRect.sizeDelta = new Vector2(width, originalPriceStrikeThickness);
                strikeRect.anchoredPosition = new Vector2((left + right) * 0.5f, (bottom + top) * 0.5f) + originalPriceStrikeOffset;
                strikeLine.color = originalPriceStrikeColor;
            }
        }
        if (strikeRect.gameObject.activeSelf != show) strikeRect.gameObject.SetActive(show);
    }

    private void UpdatePopupStrike()
    {
        if (popupListing == null) return;
        UpdateStrikeLine(popupPriceText, popupStrikeRect, popupStrikeLine, ShopNPC.IsPriceModified(popupListing),
                         ShopNPC.GetOriginalTotalPrice(popupListing, popupQuantity).ToString().Length);
    }

    // 가격표(카드의 가격표 틀 / 팝업의 [구매하기])의 공통 배치: 위 절반 = 재화 아이콘 + 가격 칸, 아래 절반 = "구매하기".
    // 재화 아이콘/가격 칸/가격 숫자는 각각 따로 위치를 조정합니다(아이콘 크기를 바꿔도 가격 숫자가 밀리지 않음).
    // slantAngle = 가격표(버튼)의 기울기(도). 남색 가격 칸도 같은 기울기의 평행사변형으로 그리고, "구매하기"는 아랫줄의 기울어진 가운데에 둡니다.
    private void LayoutPriceRow(Vector2 frameSize, RectTransform currencyRect, Image currencyImage, RectTransform boxRect, UIPatternBox boxShape,
                                TextMeshProUGUI priceText, TextMeshProUGUI buyText, ShopCurrency currency, string price, Color priceColor,
                                float iconSize, Vector2 iconOffset, float boxLeftInset, Vector2 textOffset, int priceSize,
                                int buySize, Color buyColor, Sprite iconOverride = null, float slantInset = 0f,
                                Vector2 boxOffset = default, Vector2 boxSizeAdjust = default, float slantAngle = 0f)
    {
        float slantTan = Mathf.Tan(Mathf.Clamp(slantAngle, 0f, 45f) * Mathf.Deg2Rad);
        float halfHeight = frameSize.y * 0.5f;
        float rowCenterY = -halfHeight * 0.5f - 2f;
        const float sidePad = 8f;

        // slantInset: 평행사변형 버튼(팝업 [구매하기])이면 기울어진 왼쪽 끝에 아이콘이 걸치지 않도록 그만큼 안쪽에서 시작합니다.
        currencyRect.sizeDelta = new Vector2(iconSize, iconSize);
        currencyRect.anchoredPosition = new Vector2(sidePad + slantInset + iconOffset.x, rowCenterY + iconOffset.y);
        // [86번] 레벨 한정 무료 상품이면 재화 아이콘 대신 그 할인을 준 아이템의 아이콘을 보여줍니다.
        Sprite currencySprite = iconOverride != null ? iconOverride : GetCurrencyIcon(currency);
        currencyImage.sprite = currencySprite;
        currencyImage.color = currencySprite != null ? Color.white : placeholderCurrencyColor;

        // 평행사변형이면 오른쪽 끝도 기울기만큼 안쪽으로(칸 아래 모서리가 버튼 밖으로 튀어나오지 않게)
        Vector2 boxSize = new Vector2(Mathf.Max(10f, frameSize.x - boxLeftInset - slantInset * 2f - sidePad + boxSizeAdjust.x),
                                      Mathf.Max(10f, halfHeight - 8f + boxSizeAdjust.y));
        boxRect.sizeDelta = boxSize;
        boxRect.anchoredPosition = new Vector2(boxLeftInset + slantInset, rowCenterY) + boxOffset;
        boxShape.SetupShape(PopupStyle.Get(), priceBoxColor, priceBoxCornerRadius, slantAngle, false);

        var priceRect = priceText.rectTransform;
        priceRect.sizeDelta = boxSize;
        priceRect.anchoredPosition = textOffset;
        priceText.text = price;
        priceText.fontSize = priceSize;
        priceText.color = priceColor;

        var buyRect = buyText.rectTransform;
        buyRect.sizeDelta = new Vector2(frameSize.x, halfHeight);
        buyRect.anchoredPosition = new Vector2(-frameSize.y * 0.25f * slantTan, 2f); // 아랫줄 가운데(기울어진 만큼 왼쪽)
        buyText.text = buyLabel;
        buyText.fontSize = buySize;
        buyText.color = buyColor;
    }

    private void LayoutPopup()
    {
        Vector2 size = new Vector2(Mathf.Max(200f, popupSize.x), Mathf.Max(150f, popupSize.y));
        popupRect.sizeDelta = size;
        popupRect.anchoredPosition = popupOffset;
        popupRect.SetAsLastSibling();
        var style = PopupStyle.Get();
        popupBox.SetupPanel(style);
        popupBox.SetPatternTweak(popupPatternTopLeftOffset, popupPatternTopLeftScale,
                                 popupPatternBottomRightOffset, popupPatternBottomRightScale, popupPatternTriangleScale);

        // 제목 두 줄: 첫째 줄 = 아이템 이름 판(길면 글자가 줄어듦), 둘째 줄 = "구매하시겠습니까?"(고정)
        float plateHeight = UINamePlate.Height(popupTitleFontSize, style);
        float questionHeight = popupTitleFontSize + 10f;
        float titleHeight = plateHeight + PopupTitleGap + questionHeight;
        float titleWidth = size.x - popupPadding * 2f;
        popupNamePlate.rect.anchoredPosition = new Vector2(0f, -popupPadding);
        if (popupListing != null)
            popupNamePlate.Fit(popupListing.displayName, PopupNameColor(popupListing), popupTitleFontSize, titleWidth, style);

        var titleRect = popupTitleText.rectTransform;
        titleRect.sizeDelta = new Vector2(titleWidth, questionHeight);
        titleRect.anchoredPosition = new Vector2(0f, -popupPadding - plateHeight - PopupTitleGap);
        popupTitleText.fontSize = popupTitleFontSize;
        popupTitleText.color = style.panelTextColor;

        // 수량 줄: [-] [수량] [+]
        float rowTop = popupPadding + titleHeight + quantityRowGap;
        float rowHeight = Mathf.Max(quantityButtonSize.y, quantityBoxSize.y);
        float halfBox = quantityBoxSize.x * 0.5f;
        float buttonCenterX = halfBox + quantitySpacing + quantityButtonSize.x * 0.5f;

        quantityBoxRect.sizeDelta = quantityBoxSize;
        quantityBoxRect.anchoredPosition = new Vector2(0f, -rowTop - (rowHeight - quantityBoxSize.y) * 0.5f);
        quantityBox.SetupShape(style, quantityBoxColor, quantityCornerRadius, 0f, true);
        var qtyRect = quantityText.rectTransform;
        qtyRect.sizeDelta = quantityBoxSize;
        qtyRect.anchoredPosition = Vector2.zero;
        quantityText.fontSize = quantityFontSize;
        quantityText.color = style.panelTextColor;

        // [-]/[+] = 흰 둥근 사각형 + 그림자(형빈 확정). 색은 RefreshPopupVisuals가 상태에 따라 칠합니다.
        float buttonY = -rowTop - (rowHeight - quantityButtonSize.y) * 0.5f;
        minusBox.SetupShape(style, minusBox.color, quantityCornerRadius, 0f, true);
        plusBox.SetupShape(style, plusBox.color, quantityCornerRadius, 0f, true);
        Color minusTextColor = style.panelTextColor;
        Color plusTextColor = style.panelTextColor;
        if (!popupCanMinus) minusTextColor.a *= style.disabledButtonAlpha;
        if (!popupCanPlus) plusTextColor.a *= style.disabledButtonAlpha;
        LayoutLabelButton(minusRect, minusText, "-", quantityButtonSize, new Vector2(-buttonCenterX, buttonY), quantityFontSize, minusTextColor);
        LayoutLabelButton(plusRect, plusText, "+", quantityButtonSize, new Vector2(buttonCenterX, buttonY), quantityFontSize, plusTextColor);

        // 안내 문구: 수량 줄과 버튼 사이 가운데
        float buttonsTop = popupButtonBottomMargin + popupButtonSize.y;
        float reasonTop = rowTop + rowHeight;
        float reasonSpace = Mathf.Max(reasonFontSize + 6f, size.y - reasonTop - buttonsTop);
        var reasonRect = reasonText.rectTransform;
        reasonRect.sizeDelta = new Vector2(size.x - popupPadding * 2f, reasonSpace);
        reasonRect.anchoredPosition = new Vector2(0f, -reasonTop);
        reasonText.fontSize = reasonFontSize;
        reasonText.color = reasonTextColor;

        // 버튼: [구매하기](왼쪽) / [아니오](오른쪽)
        float half = (popupButtonSize.x + popupButtonSpacing) * 0.5f;
        popupBuyRect.sizeDelta = popupButtonSize;
        popupBuyRect.anchoredPosition = new Vector2(-half, popupButtonBottomMargin);
        popupBuyBox.SetupButton(style, UIButtonKind.Positive); // 재확인창의 실행 버튼
        popupNoBox.SetupButton(style, UIButtonKind.Normal);
        if (popupListing != null)
        {
            // 가격 숫자/색/재화 아이콘은 UpdatePopup에서 매 프레임 다시 채웁니다.
            LayoutPriceRow(popupButtonSize, popupCurrencyRect, popupCurrencyIcon, popupPriceBoxRect, popupPriceBox, popupPriceText,
                           popupBuyText, popupListing.currency, popupPriceText.text, popupPriceText.color,
                           popupCurrencyIconSize, popupCurrencyIconOffset, popupPriceBoxLeftInset, popupPriceTextOffset,
                           popupPriceFontSize, popupBuyLabelFontSize, popupBuyBox.GetLabelColor(popupCanBuy),
                           GetLevelFreeIcon(popupListing),
                           popupButtonSize.y * 0.5f * Mathf.Tan(Mathf.Clamp(style.buttonSlantAngle, 0f, 45f) * Mathf.Deg2Rad),
                           popupPriceBoxOffset, popupPriceBoxSizeAdjust, style.buttonSlantAngle);
            UpdatePopupStrike();
        }

        LayoutLabelButton(popupNoRect, popupNoText, popupNoLabel, popupButtonSize, new Vector2(half, popupButtonBottomMargin),
                          popupNoLabelFontSize, popupNoBox.GetLabelColor(true));
    }

    private static void LayoutLabelButton(RectTransform rect, TextMeshProUGUI label, string text, Vector2 size, Vector2 pos,
                                          int fontSize, Color textColor)
    {
        rect.sizeDelta = size;
        rect.anchoredPosition = pos;
        label.text = text;
        label.fontSize = fontSize;
        label.color = textColor;
        // 버튼 색(마우스 올림/비활성 포함)은 Update에서 칠합니다.
    }
}
