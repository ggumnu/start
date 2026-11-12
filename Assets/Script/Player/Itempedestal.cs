using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 아이템 보물상자입니다. 지금은 동아리방(Club) 전용으로 LevelBuilder가 스폰하지만, 나중에
// 보스방 100%/일반방 저확률/상점 등 다른 곳에서도 이 컴포넌트를 그대로 재사용할 수
// 있도록 "어떤 풀에서 뽑을지"만 외부(itemPool)에서 주입받는 구조로 만들었습니다.
//
// [ItemPedestal도 결국은 "보물상자"입니다 - v1.30]
// 클래스 이름은 ItemPedestal이지만, 게임 안에서는 일반방/동아리방/보스방 상자와
// 똑같은 "보물상자"로 취급합니다. 그래서 TreasureChest와 동일하게 "F로 열기 전까지는 뭐가
// 들었는지 모른다"는 규칙을 그대로 적용합니다 - 아래 [연다(Open) 개념] 항목 참고.
//
// [2D 아이콘]
// 보물상자 자체(보급드론 등)는 3D 모델이지만, 그 위에 떠 있는 아이템은 2D 스프라이트입니다.
// 카메라 각도가 항상 고정(CameraFollow.fixedEulerAngles, 기본값 (55, 45, 0))이므로,
// 아이콘의 회전값도 그와 짝을 이루는 고정값을 한 번만 넣어두면 항상 카메라 정면을
// 보게 됩니다 - 매 프레임 계산하는 빌보드 로직이 필요 없습니다. 카메라 값을 바꾸면
// (부호가 반대로 보여 (55,-45,0)으로 뒤집는 경우 등) 아래 iconFixedEulerAngles도 함께
// 맞춰주세요.
//
// [v1.13 변경 - 아이콘이 땅에 박히는 문제 수정 (localPosition -> position)]
// 실제 3D 보물상자 프리팹을 만들어 세워보니, 보물상자 루트 자체가 (원래 상자 위쪽이 전면부였던
// 것을 앞으로 눕히면서) Identity가 아닌 로컬 회전을 갖게 됐습니다. 그 상태에서
// iconGO.transform.localPosition = Vector3.up * iconHeight로 아이콘을 배치하면, "로컬
// Up"이 더 이상 "월드 Up"과 같지 않아 아이콘이 회전된 방향(결과적으로 땅 쪽)으로
// 튀어버리는 문제가 있었습니다. 부모의 회전과 무관하게 항상 "보물상자보다 iconHeight만큼
// 위"에 뜨도록, 월드 좌표를 직접 계산하는 방식(transform.position + Vector3.up *
// iconHeight)으로 수정했습니다.
//
// [v1.15 변경 - HUD 아이템 설명칸을 위한 읽기 전용 접근자 추가]
// 화면 왼쪽의 "근처 아이템 설명칸" UI(PlayerItemInfoDisplay)가 "플레이어가 지금 이
// 보물상자의 상호작용 범위 안에 있는지"와 "이 보물상자에 배정된 아이템이 뭔지"를 읽어야 해서,
// 기존 private 필드는 그대로 두고 읽기 전용 프로퍼티만 추가했습니다. 로직은 전혀
// 바뀌지 않았습니다 - 외부에서 값을 "보기만" 할 수 있게 열어둔 것뿐입니다.
//
// [v1.17 변경 - 미니맵 오버레이용 IMinimapMarker 구현]
// 미니맵(MinimapDisplay)이 "방문한 방에 아이템이 남아있는지"를 판단할 때, 이 보물상자가
// (비활성화된 방 안에서도) 아직 존재하는지만 보면 됩니다 - 아이템을 먹으면 완전히 소모된
// 시점에 이 오브젝트를 Destroy()하므로 별도의 "먹었는지" 플래그가 필요 없습니다. 그래서
// IMinimapMarker를 구현해 "Item" 키만 알려주도록 했고, 기존 로직은 전혀 건드리지
// 않았습니다.
//
// [v1.24 변경 - 액티브 아이템 교체, v1.26 변경 - 장신구 교체, v1.29 변경 - 일회용 교체도
// 동일하게 추가]
// itemPool에는 이제 패시브뿐 아니라 액티브/장신구/일회용도 섞여 들어올 수 있습니다
// (LevelBuilder가 네 분류를 합친 풀을 넘겨줍니다). 뽑힌 assignedItem이 Active/Trinket/
// Disposable 분류면 습득 시 각각 PlayerActiveItem.TryAcquireItem() /
// PlayerTrinket.TryAcquireItem() / PlayerDisposableItem.TryAcquireItem()을 호출하고,
// 이미 같은 분류의 다른 아이템을 장착 중이었다면 그 아이템이 방출되어 돌아옵니다 - 이 경우
// 보물상자를 파괴하지 않고(HandleSingleSlotSwapResult) assignedItem을 방출된 아이템으로 갈아
// 끼워서(아이콘도 갱신) 계속 살려둡니다(=플레이어가 마음이 바뀌면 다시 주울 수 있음).
//
// [연다(Open) 개념 - v1.30 추가]
// 지금까지는 플레이어가 스폰되자마자(F를 누르기 한참 전에) 자동으로 아이템을 뽑아서
// 아이콘을 띄웠는데, 이제는 플레이어가 상호작용 범위 안에서 F를 처음 눌렀을 때 비로소
// 뽑습니다(그 전까지는 아이콘도 HUD 설명창도 없습니다 - "뭐가 들었는지 모르는" 상태).
// 그 다음 F 입력부터 실제 획득(TryPickup)이 이뤄집니다 - TreasureChest.cs의 Open()과
// 완전히 동일한 2단계 구조입니다.
//
// 뽑기 자체가 F 입력(=플레이어가 이미 범위 안에 들어와 있다는 뜻) 시점에만 일어나므로,
// 예전처럼 "플레이어가 아직 스폰되지 않았을 수도 있어 매 프레임 재시도"할 필요가 없어져
// EnsurePlayerRefsCached()는 Open() 때 딱 한 번만 호출됩니다.
//
// [뽑을 아이템이 하나도 없을 때 - 기본 아이템 폴백]
// 예전에는 후보가 하나도 없으면(전부 이미 보유 중 등) 보물상자를 그냥 비활성화했는데,
// 이제는 일반맵 보물상자처럼 기본 아이템(재화 등)을 fallbackBasicItemSlotCount개만큼
// 가중치 추첨해 확정 지급하는 것으로 대체합니다 - basicItemBundlePool은 LevelBuilder가
// NormalRoomTreasureSpawner(treasureSpawner)의 풀을 그대로 넘겨줘서, 별도로 중복
// 설정할 필요가 없습니다.
//
// [48번 아이템 연동 - v1.31 추가]
// Open()은 실제 내용물을 확정하기 전(isOpened를 true로 바꾼 직후, 후보를 거르거나 뽑기
// 전)에 반드시 ContainerOpenEvents.RaiseOpened()를 먼저 호출합니다 - 48번 일회용
// 아이템("사용하면 다음에 여는 상자의 모든 가중치가 1~5로 뒤섞인다")이 이 신호를 받아
// NextItemPickWeightOverride를 무장시켜야, 그 직후 일어나는 실제 가중치 뽑기(PickWeighted/
// PickWeightedBundle - 단일 아이템/기본 아이템 폴백 둘 다)에 반영됩니다. Open()이 끝나는
// 시점(단일 아이템이 뽑혔든 기본 아이템 폴백으로 빠졌든 상관없이)엔 항상
// NextItemPickWeightOverride.ClearAfterThisOpen()을 호출해 무장을 풀어, 48번을 쓰지
// 않았을 때는 물론 다음다음 상자에도 영향이 새지 않도록 합니다.
//
// [레벨 내 아이템 중복 등장 방지 - v1.32 추가, v1.33에서 기준 수정]
// Open()의 후보 필터링 체인 마지막에 GameFlowManager.FilterOutAlreadyAssignedThisLevel()도
// 함께 호출해서, 이 레벨의 다른 보물상자에서 이미 뽑혀 배정된 적이 있는 아이템(줍지
// 않았어도)은 후보에서 제외합니다. 이 보물상자가 새로 뽑은 아이템도 즉시 GameFlowManager.
// MarkItemAssignedThisLevel()로 기록해둡니다(GameFlowManager.cs 상단 주석 참고).
//
// [F 키 안내 - v1.34, v1.35에서 중앙 관리로 이동]
// 처음엔 이 스크립트가 직접 F 스프라이트+텍스트를 만들었지만, 보물상자/좌대/포탈마다 위치값을
// 따로 설정하는 게 과하다고 판단해 InteractPromptDisplay.cs(HUD 쪽 단일 컴포넌트)가 모든
// 상호작용 가능한 오브젝트를 스캔해서 하나의 F 안내를 대신 띄우는 구조로 바뀌었습니다. 이
// 스크립트는 그 판단에 필요한 읽기 전용 정보(PlayerInRange/IsOpened/HasAnythingToPickup)만
// 공개합니다.
//
// [아이템 획득 연출 신호]
// 플레이어 아이템(패시브/액티브/장신구/일회용/음식) 획득에 성공한 순간 ItemPickupEvents.Raise()로
// 방금 가져간 아이템을 알립니다(PlayerItemPickupPresenter가 머리 위로 들어 올리는 연출). 기본 아이템
// 묶음은 신호를 보내지 않습니다.
//
// [통과 가능 + 거리 기반 상호작용 범위 (형빈 확정)]
// 예전에는 몸통 콜라이더가 플레이어를 막고, 상호작용 범위는 트리거 콜라이더의 OnTriggerEnter/Exit로 판정했습니다.
// 이제 일반방 보물상자(TreasureChest)와 같은 방식으로 바꿨습니다.
// - Walk Through가 켜져 있으면(기본) 시작할 때 상자와 자식의 모든 콜라이더를 트리거로 바꿔 플레이어가 지나갈 수 있습니다.
//   (Non-convex Mesh Collider는 트리거가 안 되므로 Convex로 바꿉니다.)
// - 상호작용 범위는 매 프레임 플레이어와의 거리(Interact Radius)로 판정합니다. 트리거 콜라이더가 여러 개면 하나에서
//   나가는 순간 범위 밖으로 처리되는 오작동이 생기고, 9번 음식 복제처럼 플레이어와 겹친 채로 생기면 OnTriggerEnter가
//   아예 오지 않는 문제가 있어서입니다(TreasureChest.UpdatePlayerInRange와 같은 이유).
// - 그래서 이제 프리팹의 트리거 콜라이더 크기는 상호작용 범위와 관계없습니다. 범위는 Interact Radius로 조정하세요.
[RequireComponent(typeof(Collider))]
public class ItemPedestal : MonoBehaviour, IMinimapMultiMarker, IBasicItemContainer, IOpenableContainer
{
    // [특수방 통합 - 동아리방 전용 효과 분리]
    // 이 보물상자는 동아리방 외에 특수방(플레이어 아이템 받침대 레이아웃)에도 놓입니다. 동아리방 상자만 특정하는
    // 효과 - ClubChestOpenEvents 신호(27번 음식이 듣는 "동아리방 상자가 열렸다")와 NextClubPedestalLegendaryOverride
    // (27번의 "다음 동아리방 상자 최상급 확정") - 는 이 값이 true일 때만 적용됩니다. LevelBuilder가 특수방에 놓을 때만
    // false로 바꾸고, 그 외(동아리방/복제/테스트 전시대)는 기본값 true 그대로라 기존 동작과 같습니다.
    // 프리팹/인스펙터에 저장할 값이 아니라 스폰 시점에 정해지는 값이라 직렬화하지 않습니다.
    [System.NonSerialized] public bool isClubPedestal = true;

    [Header("이 보물상자가 아이템을 뽑아올 후보 풀 (LevelBuilder가 스폰 시 주입)")]
    public List<ItemDefinition> itemPool = new List<ItemDefinition>();

    [Header("뽑을 아이템이 하나도 없을 때의 대체 기본 아이템 (LevelBuilder가 스폰 시 주입)")]
    [Tooltip("itemPool에서 뽑을 수 있는 아이템이 하나도 없을 때(전부 이미 보유 중이거나 풀이 " +
             "비어있을 때), 이 풀에서 가중치 추첨으로 fallbackBasicItemSlotCount개만큼 확정 " +
             "지급합니다. NormalRoomTreasureSpawner.basicItemBundlePool과 동일한 구조입니다.")]
    public List<BasicItemBundle> basicItemBundlePool = new List<BasicItemBundle>();
    [Tooltip("대체 기본 아이템 지급 시 뽑을 슬롯 개수입니다 (기본값 2).")]
    public int fallbackBasicItemSlotCount = 2;

    [Header("상호작용 설정")]
    public KeyCode interactKey = KeyCode.F;
    [Tooltip("이 거리 이내에 플레이어가 있으면 상호작용(F) 가능 + HUD에 내용물이 표시됩니다(일반방 보물상자의 " +
             "Interact Radius와 같은 방식). 예전처럼 트리거 콜라이더 크기로 정하지 않습니다.")]
    public float interactRadius = 1f;
    [Tooltip("켜면(기본) 시작할 때 이 상자와 자식의 모든 콜라이더를 트리거로 바꿔 플레이어가 지나갈 수 있게 합니다 " +
             "(일반방 보물상자와 같음). 끄면 프리팹 콜라이더 설정 그대로(몸통이 막음)입니다.")]
    public bool walkThrough = true;

    [Header("아이콘 표시 (비워두면 자동 생성)")]
    public SpriteRenderer iconRenderer;
    [Tooltip("스프라이트가 항상 카메라 정면을 보도록 적용할 고정 회전값입니다. " +
             "CameraFollow.fixedEulerAngles(기본값 (55, 45, 0))와 짝을 이루는 값이므로, " +
             "카메라 각도가 바뀌면 이 값도 함께 조정해야 합니다.")]
    public Vector3 iconFixedEulerAngles = new Vector3(55f, 45f, 0f);
    [Tooltip("아이콘 스프라이트의 로컬 스케일입니다. 스프라이트 원본 해상도가 크면 칸 크기보다 " +
             "훨씬 커 보일 수 있으니 여기서 줄여주세요 (예: 0.5 = 원래 크기의 절반).")]
    public Vector3 iconScale = new Vector3(0.5f, 0.5f, 0.5f);
    [Tooltip("보물상자 기준 아이콘이 떠 있을 높이입니다 (월드 Y축 기준 - 보물상자 자체의 로컬 " +
             "회전과 무관하게 항상 보물상자보다 이만큼 위에 뜹니다).")]
    public float iconHeight = 1f;

    private readonly List<ResolvedBasicItemStack> basicStacks = new List<ResolvedBasicItemStack>();
    private ItemDefinition assignedItem; // 패시브/액티브/장신구/일회용 공용 (기본 아이템 폴백과는 배타적)
    // [1회성 지급 - 교체 복제 방지] assignedItem이 이 받침대가 새로 뽑은 사본이면 true, 플레이어가 교체로 올려둔 사본이면 false입니다.
    // 새 사본을 줄 때만 획득 시 1회성 지급을 받을 수 있습니다(GameFlowManager.AcquireItemCopy 참고).
    private bool assignedItemIsFresh;
    private bool isOpened = false;
    private bool playerInRange = false;
    private Transform playerTransform;

    private PlayerInventory playerInventory;
    private PlayerActiveItem playerActiveItem;
    private PlayerTrinket playerTrinket;
    private PlayerDisposableItem playerDisposableItem;
    private PlayerFoodItem playerFoodItem;
    private PlayerHealth playerHealth;
    private PlayerStats playerStats;
    private PlayerResources playerResources;
    private GameFlowManager gameFlow; // v1.32 - 레벨당 1회 사용 제한된 일회용 아이템 필터링용

    // HUD의 아이템 설명칸이 "플레이어와 가장 가까운, 상호작용 가능한 아이템"을 고를 때
    // 사용합니다. 둘 다 읽기 전용이며, 이 스크립트 내부 로직에는 영향을 주지 않습니다.
    public bool PlayerInRange => playerInRange;
    public ItemDefinition AssignedItem => assignedItem;
    public bool IsOpened => isOpened;
    // InteractPromptDisplay(F 안내 중앙 관리 컴포넌트)가 isOpened인데도 지금 당장 가져갈 게
    // 있는지 판단할 때 씁니다(예: 다 가져가서 상자가 사라지기 직전엔 false) - 읽기 전용입니다.
    public bool HasAnythingToPickup => !IsFullyConsumed();

    // IBasicItemContainer 구현 - TreasureChestInfoDisplay가 "기본 아이템 폴백"이 나온
    // 보물상자도 상자와 동일한 UI로 보여줄 수 있게 해줍니다.
    public IReadOnlyList<ResolvedBasicItemStack> BasicStacks => basicStacks;

    // IMinimapMultiMarker 구현 - 지금 이 보물상자 상태에 맞는 미니맵 아이콘 키를 알려줍니다.
    // - 아직 F로 열지 않았다: 내용물이 정해지지 않았으므로 "Chest" 하나.
    // - 열었다: 아직 안 가져간 것만 - 아이템이 남아있으면 "Item", 안 가져간 기본 아이템 묶음은
    //   그 종류별 키(예: 체력이 꽉 차서 못 가져간 "Health"). 전부 가져가 소멸 연출 중이면 아무것도 없음.
    public void CollectMinimapKeys(List<string> outKeys)
    {
        if (!isOpened)
        {
            outKeys.Add(MinimapKeys.Chest);
            return;
        }

        if (assignedItem != null) outKeys.Add(MinimapKeys.Item);

        foreach (var stack in basicStacks)
        {
            if (!stack.consumed) outKeys.Add(MinimapKeys.ForBasicItem(stack.type));
        }
    }

    void Awake()
    {
        if (iconRenderer == null)
        {
            var iconGO = new GameObject("Icon");
            iconGO.transform.SetParent(transform);
            // 보물상자 루트가 Identity가 아닌 회전을 가질 수 있으므로(예: 상자를 눕혀 세운
            // 프리팹), localPosition이 아니라 월드 좌표를 직접 계산해 배치합니다 - 그래야
            // 보물상자의 회전과 무관하게 항상 "월드 기준 위쪽"에 아이콘이 뜹니다.
            iconGO.transform.position = transform.position + Vector3.up * iconHeight;
            iconRenderer = iconGO.AddComponent<SpriteRenderer>();
        }
        iconRenderer.transform.rotation = Quaternion.Euler(iconFixedEulerAngles);
        iconRenderer.transform.localScale = iconScale;

        if (walkThrough) MakeWalkThrough();
    }

    // 상자와 자식의 모든 콜라이더를 트리거로 바꿔 플레이어가 지나갈 수 있게 합니다(NormalRoomTreasureSpawner.
    // PrepareChestComponents와 같은 처리). 상호작용 범위는 거리로 판정하므로 콜라이더 모양은 상관없습니다.
    private void MakeWalkThrough()
    {
        foreach (var col in GetComponentsInChildren<Collider>(true))
        {
            if (col is MeshCollider meshCollider && !meshCollider.convex) meshCollider.convex = true;
            col.isTrigger = true;
        }
    }

    // 매 프레임 플레이어와의 거리로 상호작용 범위를 판정합니다(TreasureChest.UpdatePlayerInRange와 같은 방식).
    private void UpdatePlayerInRange()
    {
        if (playerTransform == null)
        {
            var playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) playerTransform = playerObj.transform;
        }

        if (playerTransform == null)
        {
            playerInRange = false;
            return;
        }

        playerInRange = Vector3.Distance(transform.position, playerTransform.position) <= interactRadius;
    }

    // [아이템 아이콘 뒤 빛 이펙트] 지금 아이콘/아이템 상태를 ItemGlowSpawner에 알려줍니다. 아이템이
    // 바뀌었을 때만 실제로 빛을 만들거나(등급 색) 지우고, 같으면 아무것도 하지 않습니다. 열기 전(아이템
    // 미정)에는 빛이 없고, 씬에 ItemGlowSpawner가 없으면 아무 일도 일어나지 않습니다.
    void LateUpdate()
    {
        ItemGlowSpawner.Sync(iconRenderer, assignedItem);
    }

    void Update()
    {
        UpdatePlayerInRange();

        if (!playerInRange) return;
        if (SlotReplaceConfirmUI.BlocksInteract) return; // 슬롯 교체 확인창이 떠 있거나 방금 닫힌 프레임
        // F 대상은 범위 안에서 플레이어와 가장 가까운 하나뿐입니다(InteractionTargetSelector) - 내가 지금 F
        // 대상일 때만 처리합니다.
        if (!InteractionTargetSelector.IsCurrentTarget(this)) return;

        if (Input.GetKeyDown(interactKey))
        {
            if (!isOpened)
            {
                Open();
            }
            else
            {
                TryPickup();
            }
        }
    }


    // F(또는 나중에 추가될 다른 개방 수단)로 보물상자를 엽니다. ContainerOpenEvents.
    // RaiseOpened()를 먼저 알린 뒤(48번 아이템이 여기서 다음 뽑기의 가중치 무장 여부를
    // 결정합니다) 이 시점에 실제 내용물을 확정합니다 - itemPool에서 뽑을 수 있으면 그
    // 아이템 하나, 없으면 기본 아이템 폴백으로 대체합니다. 어느 경로든 끝나면 무장을 항상
    // 해제합니다. 이미 열려있으면 아무 일도 하지 않습니다.
    public void Open()
    {
        if (isOpened) return;
        isOpened = true;

        ContainerOpenEvents.RaiseOpened();
        // v1.35 - 27번 음식("사용하면 다음에 여는 동아리방 상자에서 반드시 최상급 아이템이
        // 나온다")이 "다음 상자 아무거나"가 아니라 "다음 동아리방 보물상자"를 정확히 특정해야
        // 하므로, 이 보물상자 전용 신호도 함께 울립니다(ClubChestOpenEvents.cs 참고).
        if (isClubPedestal) ClubChestOpenEvents.RaiseOpened();

        EnsurePlayerRefsCached();

        var candidates = playerInventory != null
            ? playerInventory.FilterUnowned(itemPool)
            : new List<ItemDefinition>(itemPool ?? new List<ItemDefinition>());
        if (playerActiveItem != null) candidates = playerActiveItem.FilterOutEquipped(candidates);
        if (playerTrinket != null) candidates = playerTrinket.FilterOutEquipped(candidates);
        if (playerDisposableItem != null) candidates = playerDisposableItem.FilterOutEquipped(candidates);
        if (playerFoodItem != null) candidates = playerFoodItem.FilterOutEquipped(candidates);
        // v1.33 - 이 레벨의 다른 보물상자에서 이미 뽑혀 배정된(줍지 않았어도) 아이템은
        // 다시 뽑히지 않도록 걸러냅니다.
        if (gameFlow != null) candidates = gameFlow.FilterOutAlreadyAssignedThisLevel(candidates);

        assignedItem = PickWeighted(candidates);
        assignedItemIsFresh = assignedItem != null; // 새로 뽑은 사본

        if (assignedItem != null)
        {
            if (iconRenderer != null) iconRenderer.sprite = assignedItem.icon;
            // v1.33 - 이 보물상자가 방금 이 아이템을 내용물로 확정했으므로, 줍기 전이라도
            // 이 레벨의 다른 보물상자에서 다시 뽑히지 않도록 즉시 기록합니다.
            if (gameFlow != null) gameFlow.MarkItemAssignedThisLevel(assignedItem.itemId);
            NextItemPickWeightOverride.ClearAfterThisOpen();
            if (isClubPedestal) NextClubPedestalLegendaryOverride.ClearAfterThisOpen();
            return;
        }

        // 뽑을 아이템이 하나도 없으면(전부 이미 보유 중이거나 풀이 비어있으면) 보물상자를
        // 비활성화하는 대신, 일반맵 보물상자처럼 기본 아이템을 확정 지급합니다.
        AssignFallbackBasicItems();
        NextItemPickWeightOverride.ClearAfterThisOpen();
        if (isClubPedestal) NextClubPedestalLegendaryOverride.ClearAfterThisOpen();
    }

    // [아이템 테스트 씬 전용 - v1.36 추가] ItemTestArenaBuilder가 "설명칸 전시대"를 만들 때 씁니다.
    // F로 여는 절차(Open()) 없이, 이 보물상자에 특정 아이템을 즉시 확정하고 아이콘도 채워 넣어서
    // 플레이어가 상호작용 범위 안에 들어오기만 해도 PlayerItemInfoDisplay가 바로 설명칸을 띄우게
    // 합니다(그 패널은 PlayerInRange + AssignedItem != null만 확인하고 IsOpened는 보지 않으므로
    // 이것만으로 충분합니다). isOpened도 함께 true로 표시해두므로, 플레이어가 그래도 F를 누르면
    // TryPickup()이 정상적으로 실행됩니다(이 메서드는 Open()/TryPickup() 로직을 전혀 건드리지
    // 않습니다 - 실제 동아리방/일반맵 보물상자 동작에는 영향이 없습니다).
    public void AssignForDisplayOnly(ItemDefinition item)
    {
        if (item == null) return;

        assignedItem = item;
        assignedItemIsFresh = item != null; // 새로 배정된 사본
        isOpened = true;
        if (iconRenderer != null) iconRenderer.sprite = item.icon;
    }

    private void EnsurePlayerRefsCached()
    {
        if (playerInventory == null) playerInventory = Object.FindFirstObjectByType<PlayerInventory>();
        if (playerActiveItem == null) playerActiveItem = Object.FindFirstObjectByType<PlayerActiveItem>();
        if (playerTrinket == null) playerTrinket = Object.FindFirstObjectByType<PlayerTrinket>();
        if (playerDisposableItem == null) playerDisposableItem = Object.FindFirstObjectByType<PlayerDisposableItem>();
        if (playerFoodItem == null) playerFoodItem = Object.FindFirstObjectByType<PlayerFoodItem>();
        if (playerHealth == null) playerHealth = Object.FindFirstObjectByType<PlayerHealth>();
        if (playerStats == null) playerStats = Object.FindFirstObjectByType<PlayerStats>();
        if (playerResources == null) playerResources = Object.FindFirstObjectByType<PlayerResources>();
        if (gameFlow == null) gameFlow = Object.FindFirstObjectByType<GameFlowManager>();
    }

    // NormalRoomTreasureSpawner.AssignBasicItems()와 동일한 가중치 추첨 방식이지만,
    // 슬롯 수가 적 수 기반 공식이 아니라 fallbackBasicItemSlotCount로 고정됩니다.
    private void AssignFallbackBasicItems()
    {
        basicStacks.Clear();

        for (int i = 0; i < fallbackBasicItemSlotCount; i++)
        {
            var bundle = PickWeightedBundle(basicItemBundlePool);
            if (bundle == null) continue;

            basicStacks.Add(new ResolvedBasicItemStack
            {
                type = bundle.type,
                count = bundle.count,
                consumed = false,
            });
        }

        if (basicStacks.Count == 0)
        {
            Debug.LogWarning("ItemPedestal: 뽑을 수 있는 아이템도, 대체 기본 아이템도 없어 " +
                              "빈 보물상자로 열렸습니다. basicItemBundlePool 설정을 확인해 주세요.");
        }
    }

    private void TryPickup()
    {
        // 스왑형 분류(액티브/장신구/일회용)는 자체적으로 destroy/refill을 처리하고 즉시
        // 반환합니다. Open()에서 assignedItem과 기본 아이템 폴백은 배타적으로만 채워지므로
        // (둘 다 채워지는 일이 없음), 이 분류가 뽑혔을 때는 아래 기본 아이템 경로와 섞이지
        // 않습니다.
        if (assignedItem != null && IsSlotCategory(assignedItem.category))
        {
            // [슬롯 교체 확인] 장착 중인 아이템에 "교체 시 확인"이 체크돼 있으면 바로 바꾸지 않고 확인창을 띄웁니다.
            // [예]를 누르면 ConfirmSlotSwap()이 아래 즉시 교체와 똑같은 처리(DoSlotSwap)를 합니다.
            ItemDefinition equipped = GetEquippedSlotItem(assignedItem.category);
            if (SlotReplaceConfirmUI.NeedsConfirm(equipped))
            {
                ItemDefinition incoming = assignedItem;
                SlotReplaceConfirmUI.Show(equipped, incoming, () => ConfirmSlotSwap(incoming));
                return;
            }

            DoSlotSwap();
            return;
        }

        // 여기부터는 [패시브 아이템 하나] 또는 [기본 아이템 여러 개(폴백)] 경로입니다 -
        // TreasureChest.TryPickup()과 동일하게 부분 습득(anyConsumedThisTry)과 완전 소모
        // 판정을 함께 처리합니다.
        bool anyConsumedThisTry = false;

        foreach (var stack in basicStacks)
        {
            if (stack.consumed) continue;

            if (stack.type == BasicItemType.Health)
            {
                if (playerHealth == null || playerStats == null || playerHealth.CurrentHealth >= playerStats.MaxHealth)
                {
                    continue; // 꽉 차 있으면 이번엔 건너뜁니다 - 보물상자에 그대로 남습니다.
                }
                playerHealth.Heal(stack.count);
            }
            else if (playerResources != null)
            {
                playerResources.Add(stack.type, stack.count);
            }
            else
            {
                continue; // PlayerResources를 못 찾았으면 이번엔 건너뜁니다.
            }

            stack.consumed = true;
            anyConsumedThisTry = true;
        }

        if (anyConsumedThisTry) GameAudio.Play(SfxId.ItemPickup); // [사운드] 기본 아이템을 하나라도 가져가면 획득음 한 번

        if (assignedItem != null && playerInventory != null)
        {
            ItemDefinition passiveToAcquire = assignedItem;
            if (GameFlowManager.AcquireItemCopy(passiveToAcquire, assignedItemIsFresh,
                    () => playerInventory.TryAcquireItem(passiveToAcquire)))
            {
                ItemPickupEvents.Raise(assignedItem); // 방금 가져간 아이템 - 획득 연출 신호
                assignedItem = null;
                assignedItemIsFresh = false;
                if (iconRenderer != null)
                {
                    Destroy(iconRenderer.gameObject);
                    iconRenderer = null;
                }
                anyConsumedThisTry = true;
            }
        }

        if (IsFullyConsumed())
        {
            DissolveAndVanish.Begin(gameObject, DissolveAndVanish.EndAction.Destroy);
        }
        else if (!anyConsumedThisTry)
        {
            Debug.Log("ItemPedestal: 지금은 더 가져갈 수 있는 것이 없습니다 (예: 체력이 이미 가득 참).");
        }
    }

    private bool IsFullyConsumed()
    {
        if (assignedItem != null) return false;
        foreach (var stack in basicStacks)
        {
            if (!stack.consumed) return false;
        }
        return true;
    }

    // 액티브/장신구/일회용 등 "단일 슬롯 + 교체" 아이템들의 공통 후처리입니다. 교체로 방출된
    // 아이템이 있으면 그걸로 보물상자를 다시 채우고(파괴하지 않음), 없으면(처음 장착) 기존
    // 패시브 경로와 동일하게 보물상자를 파괴합니다.
    // ------------------------------------------------------------------
    // 슬롯 아이템(액티브/장신구/일회용/음식) 교체 - 확인창 연동
    // (장착 중인 아이템에 "교체 시 확인"이 체크돼 있으면 SlotReplaceConfirmUI로 먼저 묻습니다)
    // ------------------------------------------------------------------

    private static bool IsSlotCategory(ItemDefinition.ItemCategory category)
    {
        return category == ItemDefinition.ItemCategory.Active ||
               category == ItemDefinition.ItemCategory.Trinket ||
               category == ItemDefinition.ItemCategory.Disposable ||
               category == ItemDefinition.ItemCategory.Food;
    }

    // 그 분류에 새 아이템을 넣으면 밀려날 아이템(없으면 null). 장신구는 빈칸이 있으면 null, 꽉 찼으면 가장 먼저 얻은
    // 장신구입니다(55번으로 2칸일 수 있음 - PlayerTrinket.ReplacedOnAcquire). 일회용(61번)/음식(79번)도 같습니다.
    private ItemDefinition GetEquippedSlotItem(ItemDefinition.ItemCategory category)
    {
        switch (category)
        {
            case ItemDefinition.ItemCategory.Active: return playerActiveItem != null ? playerActiveItem.EquippedItem : null;
            case ItemDefinition.ItemCategory.Trinket: return playerTrinket != null ? playerTrinket.ReplacedOnAcquire : null;
            case ItemDefinition.ItemCategory.Disposable: return playerDisposableItem != null ? playerDisposableItem.ReplacedOnAcquire : null;
            case ItemDefinition.ItemCategory.Food: return playerFoodItem != null ? playerFoodItem.ReplacedOnAcquire : null;
            default: return null;
        }
    }

    // 기존 즉시 교체와 동일한 처리입니다(해당 컴포넌트가 없으면 아무것도 하지 않음 - 원래 코드의 return과 같음).
    private void DoSlotSwap()
    {
        if (assignedItem == null) return;

        ItemDefinition incoming = assignedItem;
        ItemDefinition displacedItem = null;
        // [1회성 지급 - 교체 복제 방지] 새로 뽑은 사본일 때만 획득 시 1회성 지급을 받습니다(GameFlowManager.AcquireItemCopy).
        bool acquired = GameFlowManager.AcquireItemCopy(incoming, assignedItemIsFresh, () =>
        {
            ItemDefinition displaced = null;
            bool ok = false;
            switch (incoming.category)
            {
                case ItemDefinition.ItemCategory.Active:
                    ok = playerActiveItem != null && playerActiveItem.TryAcquireItem(incoming, out displaced);
                    break;
                case ItemDefinition.ItemCategory.Trinket:
                    ok = playerTrinket != null && playerTrinket.TryAcquireItem(incoming, out displaced);
                    break;
                case ItemDefinition.ItemCategory.Disposable:
                    ok = playerDisposableItem != null && playerDisposableItem.TryAcquireItem(incoming, out displaced);
                    break;
                case ItemDefinition.ItemCategory.Food:
                    ok = playerFoodItem != null && playerFoodItem.TryAcquireItem(incoming, out displaced);
                    break;
            }
            displacedItem = displaced;
            return ok;
        });
        if (!acquired) return;

        ItemPickupEvents.Raise(incoming); // 방금 가져간 아이템 - 획득 연출 신호
        HandleSingleSlotSwapResult(displacedItem);
    }

    // 확인창에서 [예]를 눌렀을 때. 그 사이 보물상자 내용이 바뀌었으면(이론상 없음) 아무것도 하지 않습니다.
    private void ConfirmSlotSwap(ItemDefinition incoming)
    {
        if (this == null || assignedItem == null || assignedItem != incoming) return;
        DoSlotSwap();
    }

    private void HandleSingleSlotSwapResult(ItemDefinition displacedItem)
    {
        assignedItemIsFresh = false; // 교체로 들어온 사본(또는 비었음) - 다시 주워도 1회성 지급 없음
        if (displacedItem != null)
        {
            assignedItem = displacedItem;
            if (iconRenderer != null) iconRenderer.sprite = assignedItem.icon;
        }
        else
        {
            assignedItem = null;
            if (iconRenderer != null)
            {
                Destroy(iconRenderer.gameObject);
            }
            DissolveAndVanish.Begin(gameObject, DissolveAndVanish.EndAction.Destroy);
        }
    }

    // [48번 아이템 연동 - v1.31] NextItemPickWeightOverride.IsArmed가 켜져 있으면, 각
    // 후보의 실제 weight 대신 1~5 사이의 랜덤 정수를 그 후보만의 가중치로 새로 매겨서
    // 뽑습니다(후보마다 독립적으로 굴리므로 전부 같은 값이 나올 수도, 전부 다를 수도
    // 있습니다). 같은 굴림 값을 총합 계산과 누적 판정 양쪽에 그대로 재사용하기 위해
    // effectiveWeights 배열에 한 번만 계산해 둡니다(pool.Count == 1인 경우는 애초에 뽑기가
    // 일어나지 않으므로 무장 여부와 무관하게 그대로 반환).
    private ItemDefinition PickWeighted(List<ItemDefinition> pool)
    {
        // v1.35 - 27번 음식(동아리방 다음 상자 최상급 확정) 연동: 무장되어 있으면 뽑기 대상
        // 자체를 "최상급(Tier 3) 등급 후보만"으로 좁힌 뒤 그 안에서 뽑습니다(48번의 가중치
        // 스크램블과는 서로 독립적으로 함께 적용될 수 있음 - 최상급 후보들 중에서 실제 weight를
        // 쓸지 1~5 랜덤을 쓸지는 그대로 scramble 플래그를 따릅니다). 최상급 후보가 하나도
        // 없으면(pool 자체가 비어있는 경우 포함) 보장을 지킬 수 없으므로 경고를 남기고 원래
        // pool 그대로 진행합니다(기본 아이템 폴백이든 완전히 빈 보물상자든 기존 로직이 처리합니다).
        if (isClubPedestal && NextClubPedestalLegendaryOverride.IsArmed)
        {
            var legendaryOnly = (pool ?? new List<ItemDefinition>())
                .Where(item => item != null && item.rarity == ItemDefinition.ItemRarity.Tier3)
                .ToList();
            if (legendaryOnly.Count > 0)
            {
                pool = legendaryOnly;
            }
            else
            {
                Debug.LogWarning("ItemPedestal: 27번 음식으로 최상급 아이템을 보장하려 했지만, 뽑을 수 있는 " +
                                  "최상급 후보가 없어(전부 이미 보유/배정되었거나 등록되지 않음) 보장 없이 진행합니다.");
            }
        }

        if (pool == null || pool.Count == 0) return null;
        if (pool.Count == 1) return pool[0];

        bool scramble = NextItemPickWeightOverride.IsArmed;
        float[] effectiveWeights = new float[pool.Count];
        float totalWeight = 0f;
        for (int i = 0; i < pool.Count; i++)
        {
            // [v1.39 - 48번 장신구 조정, 형빈 확정] 1~5 대신 1~3 사이로 좁혔습니다.
            // 평소에는 67번 등의 등장 가중치 보정이 적용된 값을 씁니다(ItemWeightModifier).
            effectiveWeights[i] = scramble ? Random.Range(1, 4) : ItemWeightModifier.GetWeight(pool[i]);
            totalWeight += effectiveWeights[i];
        }
        if (totalWeight <= 0f) return pool[0];

        float roll = Random.value * totalWeight;
        float cumulative = 0f;
        for (int i = 0; i < pool.Count; i++)
        {
            cumulative += effectiveWeights[i];
            if (roll <= cumulative)
            {
                // [48번 장신구 디버그 로그 - v1.39 추가] 스크램블이 실제로 걸려 있을 때만,
                // 뽑힌 아이템이 어떤 임시 가중치로 등장했는지 콘솔에 남깁니다(형빈이 랜덤이
                // 제대로 도는지 눈으로 확인하기 위한 용도 - 평소(스크램블 없음)에는 출력되지
                // 않습니다).
                if (scramble)
                {
                    Debug.Log($"[48번 장신구] '{pool[i].displayName}' 아이템이 임시 가중치 {effectiveWeights[i]}로 등장했습니다.");
                }
                return pool[i];
            }
        }
        if (scramble)
        {
            Debug.Log($"[48번 장신구] '{pool[pool.Count - 1].displayName}' 아이템이 임시 가중치 {effectiveWeights[pool.Count - 1]}로 등장했습니다.");
        }
        return pool[pool.Count - 1];
    }

    // PickWeighted()와 동일한 48번 연동입니다 - 기본 아이템 번들도 가중치 기반 추첨이므로
    // 똑같이 뒤섞입니다.
    private BasicItemBundle PickWeightedBundle(List<BasicItemBundle> pool)
    {
        var valid = pool?.Where(b => b != null && b.weight > 0f).ToList();
        if (valid == null || valid.Count == 0) return null;
        if (valid.Count == 1) return valid[0];

        bool scramble = NextItemPickWeightOverride.IsArmed;
        float[] effectiveWeights = new float[valid.Count];
        float totalWeight = 0f;
        for (int i = 0; i < valid.Count; i++)
        {
            // [v1.39 - 48번 장신구 조정] 1~5 대신 1~3 사이로 좁혔습니다.
            effectiveWeights[i] = scramble ? Random.Range(1, 4) : valid[i].weight;
            totalWeight += effectiveWeights[i];
        }

        float roll = Random.value * totalWeight;
        float cumulative = 0f;
        for (int i = 0; i < valid.Count; i++)
        {
            cumulative += effectiveWeights[i];
            if (roll <= cumulative)
            {
                // [48번 장신구 디버그 로그 - v1.39 추가] 위 PickWeighted()와 동일한 이유입니다.
                if (scramble)
                {
                    Debug.Log($"[48번 장신구] 기본 아이템 묶음 '{valid[i].type} x{valid[i].count}'가 임시 가중치 {effectiveWeights[i]}로 등장했습니다.");
                }
                return valid[i];
            }
        }
        if (scramble)
        {
            Debug.Log($"[48번 장신구] 기본 아이템 묶음 '{valid[valid.Count - 1].type} x{valid[valid.Count - 1].count}'가 임시 가중치 {effectiveWeights[valid.Count - 1]}로 등장했습니다.");
        }
        return valid[valid.Count - 1];
    }
}
