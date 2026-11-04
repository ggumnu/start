using UnityEngine;

// [슬롯 아이템 버리기 설정 에셋]
// Ctrl을 꾹 눌러 장신구/일회용/음식을 버리는 기능(SlotItemDropUI / SlotItemDropper)의 조절 값을 모아둔 에셋입니다.
// Assets/Resources 폴더에 "SlotDropSettings"라는 이름으로 만들어두면 자동으로 읽습니다(BulletTrailSettings와 같은 방식).
// 에셋이 없으면 아래 기본값으로 동작합니다(버린 아이템은 모델 없이 아이콘만 바닥 위에 뜸).
//
// 창 바탕색(검은 창)/모서리, 버튼 색(평소/마우스 올림/못 누름)/글자 색은 공용 스타일 에셋(PopupStyle)에서 읽습니다.
//
// 만들기: Project 창에서 Assets/Resources 폴더 우클릭 > Create > MobArchive > Slot Drop Settings, 이름을 SlotDropSettings로.
[CreateAssetMenu(fileName = "SlotDropSettings", menuName = "MobArchive/Slot Drop Settings")]
public class SlotDropSettings : ScriptableObject
{
    public const string ResourcePath = "SlotDropSettings";

    [Header("열기")]
    [Tooltip("Ctrl(왼쪽/오른쪽)을 이 시간(초) 동안 꾹 누르면 버리기 창이 열립니다.")]
    [Min(0f)] public float holdDuration = 0.8f;

    [Header("버린 아이템이 놓이는 좌대")]
    [Tooltip("버린 아이템을 올려둘 좌대 프리팹입니다(지금은 상점 좌대처럼 색만 다른 보물상자 모델을 넣으면 됩니다). " +
             "HoldingPedestal 컴포넌트가 없으면 자동으로 붙이고, 보물상자 컴포넌트(TreasureChest 등)가 있으면 떼어냅니다 - " +
             "보물상자로 취급되면 9번 복제 대상이 되기 때문입니다. 비워두면 모델 없이 아이템 아이콘만 바닥 위에 뜹니다.")]
    public GameObject droppedPedestalPrefab;
    [Tooltip("켜면 좌대 프리팹의 모든 콜라이더를 트리거로 바꿔 플레이어가 통과할 수 있게 합니다(일반방 보물상자와 같은 처리).")]
    public bool makeCollidersTrigger = true;

    [Header("버릴 자리 찾기 (보물상자 자리와 같은 기준 - 바닥(NavMesh) 위 + 벽/장애물과 안 겹침)")]
    [Tooltip("플레이어로부터 이 거리(월드 단위)까지 버릴 자리를 찾습니다. 이 안에 자리가 부족하면 '현재 방에서 버릴 수 없습니다'.")]
    [Min(0.5f)] public float searchMaxRadius = 4f;
    [Tooltip("자리 후보를 이 간격(월드 단위)으로 촘촘히 확인합니다. 작을수록 꼼꼼하지만 계산이 조금 늘어납니다.")]
    [Min(0.1f)] public float searchStep = 0.5f;
    [Tooltip("버린 아이템은 플레이어로부터 최소 이 거리만큼 떨어진 곳에 놓입니다(발밑에 겹쳐 보이지 않게).")]
    [Min(0f)] public float minDistanceFromPlayer = 1f;
    [Tooltip("버린 아이템끼리, 그리고 방에 있는 다른 상자/좌대/상인/포탈과 최소 이 거리만큼 떨어뜨립니다(바닥 평면 거리).")]
    [Min(0f)] public float minSpacing = 1.2f;

    [Tooltip("일반방 보물상자 스포너(LevelBuilder의 Treasure Spawner)가 있으면 그 값(NavMesh 허용 오차/겹침 반경/겹침 레이어)을 " +
             "그대로 써서 보물상자와 똑같은 기준으로 자리를 고릅니다. 스포너가 없을 때(아이템 테스트 씬 등)만 아래 값을 씁니다.")]
    public bool useTreasureSpawnerValues = true;
    [Tooltip("NavMesh.SamplePosition 허용 오차 거리입니다(스포너 값을 못 쓸 때).")]
    public float navMeshSampleDistance = 1f;
    [Tooltip("벽/장애물과 겹치는지 확인하는 반경입니다(스포너 값을 못 쓸 때).")]
    public float overlapCheckRadius = 0.5f;
    [Tooltip("겹침 검사에 포함할 레이어입니다(스포너 값을 못 쓸 때).")]
    public LayerMask overlapCheckMask = ~0;

    [Header("창 공통 (1920x1080 기준 px)")]
    [Tooltip("다른 HUD보다 위, 슬롯 교체 확인창(90)/게임 오버 창(100)보다 아래입니다.")]
    public int canvasSortingOrder = 85;
    [Tooltip("창이 열려 있는 동안 화면 전체를 덮는 색입니다(반투명 검정).")]
    public Color dimColor = new Color(0f, 0f, 0f, 0.55f);

    [Header("첫 창 (흰 창)")]
    public string title = "해제할 슬롯 아이템을 선택하세요";
    public string dropLabel = "버리기";
    public string cancelLabel = "취소하기";
    public Vector2 windowSize = new Vector2(760f, 560f);
    public Color windowColor = new Color(1f, 1f, 1f, 0.9f);
    public Color windowOutlineColor = new Color(0.08f, 0.08f, 0.1f, 1f);
    [Min(0f)] public float windowOutlineThickness = 5f;
    [Min(0f)] public float windowCornerRadius = 24f;
    public int titleFontSize = 34;
    public Color titleColor = new Color(0.1f, 0.1f, 0.1f, 1f);
    [Tooltip("창 위쪽 끝에서 제목까지의 거리입니다.")]
    public float titleTopMargin = 60f;

    [Tooltip("창 안의 슬롯 칸 묶음 크기 배율입니다. 칸 배경/칸 틀/칸 배치는 화면 오른쪽 슬롯 HUD(PlayerSlotItemDisplay)의 값을 그대로 가져와 이 배율로 키웁니다.")]
    [Min(0.1f)] public float slotPanelScale = 1.4f;
    [Tooltip("슬롯 칸 묶음의 세로 위치입니다(창 중앙 기준, 양수면 위로).")]
    public float slotPanelOffsetY = 15f;
    [Tooltip("선택한 아이템 아이콘의 불투명도입니다(지연 소모 상태처럼 반투명).")]
    [Range(0f, 1f)] public float selectedAlpha = 0.45f;
    [Tooltip("선택한 아이템 아이콘의 크기 배율입니다(1보다 작으면 작아짐).")]
    [Range(0.1f, 1f)] public float selectedScale = 0.75f;

    [Header("첫 창 버튼 (색/글자 색/못 누를 때 불투명도는 PopupStyle)")]
    public Vector2 mainButtonSize = new Vector2(220f, 90f);
    public float mainButtonSpacing = 60f;
    [Tooltip("창 아래쪽 끝에서 버튼까지의 거리입니다.")]
    public float mainButtonBottomMargin = 45f;
    [Min(0f)] public float mainButtonCornerRadius = 18f;
    public Color mainButtonOutlineColor = new Color(0.08f, 0.08f, 0.1f, 1f);
    [Min(0f)] public float mainButtonOutlineThickness = 4f;
    public int mainButtonFontSize = 34;

    [Header("확인 창 (검은 창 - '교체 시 확인' 아이템을 버릴 때)")]
    public string confirmMessage = "정말 버리시겠습니까?";
    public Vector2 confirmSize = new Vector2(520f, 300f);
    [Header("버릴 수 없음 창 (검은 창)")]
    public string noSpaceMessage = "현재 방에서 버릴 수 없습니다";
    public string okLabel = "확인";
    public Vector2 noSpaceSize = new Vector2(480f, 220f);

    [Header("검은 창 공통 (바탕색/모서리/버튼 색/버튼 모서리는 PopupStyle)")]
    public int darkMessageFontSize = 26;
    public Color darkMessageColor = Color.white;
    public int darkDetailFontSize = 21;
    public Vector2 darkButtonSize = new Vector2(140f, 46f);
    public float darkButtonSpacing = 30f;
    public float darkButtonBottomMargin = 24f;
    public int darkButtonFontSize = 22;

    // ---------------- 읽기 ----------------

    private static SlotDropSettings cached;
    private static bool loaded;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlayStart()
    {
        cached = null;
        loaded = false;
    }

    // Resources/SlotDropSettings 에셋을 읽습니다. 없으면 기본값으로 만든 임시 설정을 돌려줍니다(항상 null이 아님).
    public static SlotDropSettings Get()
    {
        if (!loaded)
        {
            loaded = true;
            cached = Resources.Load<SlotDropSettings>(ResourcePath);
            if (cached == null)
            {
                Debug.Log("SlotDropSettings: Assets/Resources/SlotDropSettings 에셋이 없어 기본값으로 동작합니다(버린 아이템은 아이콘만 표시).");
                cached = CreateInstance<SlotDropSettings>();
            }
        }
        return cached;
    }
}
