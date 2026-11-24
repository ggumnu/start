using System.Collections.Generic;
using UnityEngine;

// 일반맵 보물상자입니다. NormalRoomTreasureSpawner가 스폰 직후 내용물(SetBasicItemContent/
// SetPassiveItemContent)을 정확히 한 번씩 채워 넣습니다.
//
// [Club의 ItemPedestal과의 차이]
// - 콜라이더가 트리거라 플레이어가 자유롭게 통과할 수 있습니다(NormalRoomTreasureSpawner가
//   스폰 시점에 강제 설정). 다만 상호작용 범위 판정은 이 트리거의 Enter/Exit 이벤트가 아니라
//   매 프레임 거리 계산(interactRadius)으로 합니다 - 상자가 플레이어와 이미 겹친 채로
//   스폰되면 OnTriggerEnter가 아예 호출되지 않는 경우가 있기 때문입니다(UpdatePlayerInRange 참고).
// - [기본 아이템]이 나온 경우엔 동아리방 보물상자처럼 아이콘이 뜨지 않습니다 - 근처 상호작용 범위에
//   들어오면 TreasureChestInfoDisplay(HUD)가 내부 아이템 목록을 보여줍니다.
// - [패시브/액티브 아이템]이 나온 경우엔 동아리방 보물상자와 동일하게 상자 위에 아이템 아이콘이 뜹니다
//   (UpdateIconVisual 참고 - ItemPedestal.Awake()와 동일한 방식). 근처 상세 정보(아이콘/
//   이름/능력치/설명)는 PlayerItemInfoDisplay가 동아리방 보물상자와 완전히 같은 방식으로 보여줍니다
//   (TreasureChestInfoDisplay는 이제 패시브 아이템 케이스를 다루지 않습니다).
// - 안에 [기본 아이템 여러 개](묶음별로 수량이 다를 수 있음) 또는 [패시브/액티브 아이템 하나] 중
//   하나만 들어있습니다(둘 다 있는 경우는 없음 - NormalRoomTreasureSpawner가 스폰 시점에 결정).
//
// [부분 습득 - 현재 체력 규칙]
// 현재 체력 묶음은 플레이어 체력이 이미 가득 차 있으면 그 항목만 건너뛰고, 나머지(다른 기본
// 아이템/패시브 아이템)는 정상적으로 습득됩니다 (아이작 시리즈의 하트 픽업과 동일한 규칙).
// F키를 누를 때마다 "그 순간 가져갈 수 있는 것"만 가져가고, 못 가져간 항목이 남아있으면
// 상자는 사라지지 않고 계속 존재합니다 - 나중에 체력이 깎인 뒤 다시 상호작용하면 그때 마저
// 습득됩니다. 모든 항목이 소비되어야 상자가 파괴됩니다.
//
// [액티브 아이템 교체 - v1.24 추가]
// passiveItem 필드는 이름과 달리 이제 패시브뿐 아니라 액티브 아이템도 담을 수 있습니다
// (NormalRoomTreasureSpawner가 두 분류를 합친 풀에서 하나를 뽑아 넣어줍니다). 습득 시점에
// passiveItem.category로 분기해서, Active면 PlayerActiveItem.TryAcquireItem()을 호출합니다.
// 이미 다른 액티브 아이템을 장착 중이었다면 그 아이템이 반환되는데, 이 상자를 비우는 대신
// passiveItem을 그 방출된 아이템으로 다시 채워서(아이콘도 갱신) 상자를 계속 살려둡니다 -
// 그래야 플레이어가 마음이 바뀌면 방출된 아이템을 다시 주울 수 있습니다. 방출된 아이템이
// 없으면(처음 장착하는 경우) 기존 패시브 경로와 동일하게 비웁니다.
//
// [연다(Open) 개념 - v1.30 추가]
// 지금까지는 상자가 스폰되자마자 NormalRoomTreasureSpawner가 내용물을 바로 확정해서
// 넣어줬는데(SetBasicItemContent/SetPassiveItemContent를 스폰 직후 바로 호출), 이제는
// 상자가 "열리기 전까지" 내용물을 아예 확정하지 않습니다. NormalRoomTreasureSpawner는
// SetContentResolver()로 "내용물을 실제로 정하는 함수"만 미리 맡겨두고, 이 상자는 그
// 함수를 F로 처음 열리는 그 순간에 딱 한 번 실행합니다(내용물 확정 + 아이콘/HUD 표시가
// 이때 동시에 일어남). 그래서 TryPickup()의 첫 F 입력은 실제 획득이 아니라 Open()만
// 수행하고, 그 다음 F 입력부터 기존 획득 로직(아래)이 그대로 실행됩니다.
//
// 나중에 F가 아닌 다른 수단(수류탄/해킹툴 등)으로 열리는 상자를 만들 때를 대비해, Open()
// 자체는 F 입력 처리와 분리된 독립적인 public 메서드로 만들었습니다 - 그런 상자는 F 처리
// 코드를 거치지 않고 그 수단의 이펙트 쪽에서 이 상자의 Open()을 직접 호출하면 됩니다.
//
// [48번 아이템 연동 - v1.31 추가]
// Open()은 실제 내용물을 확정하기(contentResolver 실행) 전에 반드시 ContainerOpenEvents.
// RaiseOpened()를 먼저 호출합니다 - 48번 일회용 아이템("사용하면 다음에 여는 상자의 모든
// 가중치가 1~5로 뒤섞인다")이 이 신호를 받아 NextItemPickWeightOverride를 무장시켜야,
// 그 직후 contentResolver 안에서 일어나는 실제 가중치 뽑기(PickWeightedItem/
// PickWeightedBundle)에 반영됩니다. contentResolver 실행이 끝나면(내용물이 기본 아이템이든
// 패시브 아이템이든 상관없이) 항상 NextItemPickWeightOverride.ClearAfterThisOpen()을 호출해
// 무장을 풀어, 48번을 쓰지 않았을 때는 물론 다음다음 상자에도 영향이 새지 않도록 합니다.
//
// [F 키 안내 - v1.34, v1.35에서 중앙 관리로 이동]
// 처음엔 이 스크립트가 직접 F 스프라이트+텍스트를 만들었지만, 보물상자/좌대/포탈마다 위치값을
// 따로 설정하는 게 과하다고 판단해 InteractPromptDisplay.cs(HUD 쪽 단일 컴포넌트)가 모든
// 상호작용 가능한 오브젝트를 스캔해서 하나의 F 안내를 대신 띄우는 구조로 바뀌었습니다. 이
// 스크립트는 그 판단에 필요한 읽기 전용 정보(PlayerInRange/IsOpened/HasAnythingToPickup)만
// 공개합니다.
//
// [슬롯 교체 확인]
// 액티브/장신구/일회용/음식은 이미 장착 중인 아이템이 있으면 교체됩니다. 그 장착 중인 아이템에 "교체 시 확인"
// (ItemDefinition.confirmBeforeReplace)이 체크돼 있으면 바로 교체하지 않고 SlotReplaceConfirmUI 확인창을 띄우고,
// [예]를 누른 순간 즉시 교체와 똑같은 처리(DoSlotSwap)를 합니다. 확인창이 떠 있거나 방금 닫힌 프레임에는 F 입력을
// 무시합니다(확인창의 F(예)가 상자에 다시 전달되어 도로 바뀌는 것 방지).
//
// [아이템 획득 연출 신호]
// 플레이어 아이템(패시브/액티브/장신구/일회용/음식) 획득에 성공한 순간 ItemPickupEvents.Raise()로
// 방금 가져간 아이템을 알립니다(PlayerItemPickupPresenter가 머리 위로 들어 올리는 연출). 기본 아이템
// 묶음은 신호를 보내지 않습니다.
[RequireComponent(typeof(Collider))]
public class TreasureChest : MonoBehaviour, IBasicItemContainer, IMinimapMultiMarker, IOpenableContainer
{
    [Header("상호작용 설정")]
    public KeyCode interactKey = KeyCode.F;
    [Tooltip("이 거리 이내에 플레이어가 있으면 상호작용(F) 가능 + HUD에 내용물이 표시됩니다.")]
    public float interactRadius = 1.5f;

    [Header("패시브 아이템 아이콘 (당첨된 경우에만 상자 위에 표시 - 동아리방 보물상자와 동일한 방식)")]
    [Tooltip("스프라이트가 항상 카메라 정면을 보도록 적용할 고정 회전값입니다. " +
             "ItemPedestal.iconFixedEulerAngles/CameraFollow.fixedEulerAngles와 짝을 이루는 " +
             "값이므로, 카메라 각도가 바뀌면 이 값도 함께 조정해야 합니다. (현재 프로젝트의 " +
             "카메라 각도인 X:55, Y:-45에 맞춘 기본값입니다.)")]
    public Vector3 iconFixedEulerAngles = new Vector3(55f, -45f, 0f);
    [Tooltip("아이콘 스프라이트의 로컬 스케일입니다. (동아리방 보물상자 아이콘과 동일한 0.05 기본값)")]
    public Vector3 iconScale = new Vector3(0.05f, 0.05f, 0.05f);
    [Tooltip("상자 기준 아이콘이 떠 있을 높이입니다 (월드 Y축 기준 - 상자 자체의 회전과 " +
             "무관하게 항상 상자보다 이만큼 위에 뜹니다).")]
    public float iconHeight = 1f;

    private readonly List<ResolvedBasicItemStack> basicStacks = new List<ResolvedBasicItemStack>();
    private ItemDefinition passiveItem; // 아직 안 정해졌거나 이미 습득했으면 null (패시브/액티브 공용)
    // [1회성 지급 - 교체 복제 방지] passiveItem이 이 상자가 새로 뽑은 사본이면 true, 플레이어가 교체로 넣어둔 사본이면 false입니다.
    // 새 사본을 줄 때만 획득 시 1회성 지급을 받을 수 있습니다(GameFlowManager.AcquireItemCopy 참고).
    private bool passiveItemIsFresh;
    private SpriteRenderer iconRenderer; // 패시브/액티브 아이템이 배정된 경우에만 생성됩니다.

    private bool isOpened = false;
    private System.Action contentResolver;

    // [3레벨 보스 지원 드론 보급 상자] 켜면 열 때 "보물상자가 열렸다" 신호(ContainerOpenEvents - 117/97/27/48번 등)를 보내지 않습니다.
    // 보급 상자는 정당한 보물상자가 아니라 기믹용 보급품이라 상자 관련 아이템이 반응하지 않게 합니다(형빈 확정).
    [HideInInspector] public bool suppressOpenEvents;

    private bool playerInRange = false;
    private Transform playerTransform;
    private PlayerHealth playerHealth;
    private PlayerStats playerStats;
    private PlayerResources playerResources;
    private PlayerInventory playerInventory;
    private PlayerActiveItem playerActiveItem;
    private PlayerTrinket playerTrinket;
    private PlayerDisposableItem playerDisposableItem;
    private PlayerFoodItem playerFoodItem;

    // TreasureChestInfoDisplay(HUD)가 "지금 이 상자 상호작용 범위 안에 플레이어가 있는지"와
    // "안에 뭐가 남아있는지"를 읽을 때 사용합니다. 둘 다 읽기 전용이며, 이 스크립트 내부
    // 로직에는 영향을 주지 않습니다.
    public bool PlayerInRange => playerInRange;
    public IReadOnlyList<ResolvedBasicItemStack> BasicStacks => basicStacks;
    public ItemDefinition PassiveItem => passiveItem;
    public bool IsOpened => isOpened;
    // InteractPromptDisplay(F 안내 중앙 관리 컴포넌트)가 isOpened인데도 지금 당장 가져갈 게
    // 있는지 판단할 때 씁니다(예: 다 가져가서 상자가 사라지기 직전엔 false) - 읽기 전용입니다.
    public bool HasAnythingToPickup => !IsFullyConsumed();

    // IMinimapMultiMarker 구현 - 지금 이 상자 상태에 맞는 미니맵 아이콘 키를 알려줍니다.
    // - 아직 F로 열지 않았다(내용물을 정해줄 resolver가 대기 중): "Chest" 하나.
    // - 열었다: 아직 안 가져간 것만 - 아이템이 남아있으면 "Item", 안 가져간 기본 아이템 묶음은
    //   그 종류별 키(예: 체력이 꽉 차서 못 가져간 "Health"). 전부 가져가 소멸 연출 중이면 아무것도 없음.
    public void CollectMinimapKeys(List<string> outKeys)
    {
        if (!isOpened && contentResolver != null)
        {
            outKeys.Add(MinimapKeys.Chest);
            return;
        }

        if (passiveItem != null) outKeys.Add(MinimapKeys.Item);

        foreach (var stack in basicStacks)
        {
            if (!stack.consumed) outKeys.Add(MinimapKeys.ForBasicItem(stack.type));
        }
    }

    // NormalRoomTreasureSpawner가 스폰 직후 정확히 한 번씩 호출합니다. 실제로 내용물을
    // 정하는 함수(resolver)만 맡아두고, 이 시점엔 아무것도 확정하지 않습니다 - 이 함수는
    // Open()이 처음 호출될 때 딱 한 번만 실행됩니다.
    public void SetContentResolver(System.Action resolver)
    {
        contentResolver = resolver;
    }

    // F(또는 나중에 추가될 다른 개방 수단)로 상자를 엽니다. ContainerOpenEvents.
    // RaiseOpened()를 먼저 알려(48번 아이템이 여기서 다음 뽑기의 가중치 무장 여부를
    // 결정합니다), 맡아둔 contentResolver를 이 시점에 딱 한 번 실행해서 실제 내용물을
    // 확정하고(그 안에서 SetBasicItemContent/SetPassiveItemContent가 호출되어 아이콘/HUD가
    // 이때 함께 드러남), isOpened를 true로 바꿉니다. contentResolver 실행이 끝나면 무장을
    // 항상 해제합니다. 이미 열려있으면 아무 일도 하지 않습니다(안전하게 여러 번 호출 가능).
    public void Open()
    {
        if (isOpened) return;
        isOpened = true;

        if (suppressOpenEvents)
        {
            contentResolver?.Invoke(); // 보급 상자: 신호 없이 내용물만(48번 무장도 건드리지 않음)
            return;
        }

        ContainerOpenEvents.RaiseOpened();
        contentResolver?.Invoke();
        NextItemPickWeightOverride.ClearAfterThisOpen();
    }

    // NormalRoomTreasureSpawner가 스폰 직후 정확히 한 번씩 호출합니다.
    public void SetBasicItemContent(List<ResolvedBasicItemStack> stacks)
    {
        basicStacks.Clear();
        if (stacks != null) basicStacks.AddRange(stacks);
    }

    public void SetPassiveItemContent(ItemDefinition item)
    {
        passiveItem = item;
        passiveItemIsFresh = item != null; // 상자가 새로 뽑아 넣은 사본
        UpdateIconVisual();
    }

    // ItemPedestal.Awake()와 동일한 방식입니다 - 상자 루트가 Identity가 아닌 회전을 갖고
    // 있어도(실제 3D 프리팹은 임의의 회전일 수 있음) 항상 "상자보다 iconHeight만큼 위"에
    // 뜨도록 월드 좌표를 직접 계산합니다. [기본 아이템]이 나온 경우(passiveItem == null)엔
    // 아이콘을 아예 만들지 않고, 만들어져 있었다면(패시브 아이템을 습득해 null이 된 경우)
    // 제거합니다.
    private void UpdateIconVisual()
    {
        if (passiveItem == null)
        {
            if (iconRenderer != null)
            {
                Destroy(iconRenderer.gameObject);
                iconRenderer = null;
            }
            return;
        }

        if (iconRenderer == null)
        {
            var iconGO = new GameObject("Icon");
            iconGO.transform.SetParent(transform);
            iconGO.transform.position = transform.position + Vector3.up * iconHeight;
            iconRenderer = iconGO.AddComponent<SpriteRenderer>();
            iconRenderer.transform.rotation = Quaternion.Euler(iconFixedEulerAngles);
            iconRenderer.transform.localScale = iconScale;
        }

        iconRenderer.sprite = passiveItem.icon;
    }

    // [범위 판정을 트리거 이벤트가 아니라 매 프레임 거리 계산으로 하는 이유]
    // 상자는 마지막 적을 처치한 "그 자리 근처"에서 스폰되는데, 근접 공격 등으로 플레이어가
    // 이미 그 지점을 밟고 있는 채로 상자가 생성되는 경우가 흔합니다. Unity의
    // OnTriggerEnter는 "겹쳐있지 않다가 겹치게 되는 전환 순간"에만 호출되므로, 이미 겹친
    // 채로 콜라이더가 생성되면 그 겹침에 대해서는 OnTriggerEnter가 아예 호출되지 않습니다.
    // 그러면 playerInRange가 영원히 false로 남아 F키도 안 먹고 HUD도 안 뜨는 문제가
    // 생깁니다. 그래서 트리거 이벤트 대신 거리 계산으로 매 프레임 직접 판정합니다
    // (콜라이더 자체는 여전히 트리거로 남아있어 플레이어가 통과할 수 있습니다).
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
    // 바뀌었을 때만 실제로 빛을 만들거나(등급 색) 지우고, 같으면 아무것도 하지 않습니다. 씬에
    // ItemGlowSpawner가 없으면 아무 일도 일어나지 않습니다(ItemGlowSpawner.cs 참고).
    void LateUpdate()
    {
        ItemGlowSpawner.Sync(iconRenderer, passiveItem);
    }

    void Update()
    {
        UpdatePlayerInRange();

        if (!playerInRange) return;
        if (SlotReplaceConfirmUI.BlocksInteract) return; // 슬롯 교체 확인창이 떠 있거나 방금 닫힌 프레임
        // F 대상은 범위 안에서 플레이어와 가장 가까운 하나뿐입니다(InteractionTargetSelector) - 범위가 겹친
        // 다른 상자/포탈이 같은 F에 함께 반응하지 않도록, 내가 지금 F 대상일 때만 처리합니다.
        if (!InteractionTargetSelector.IsCurrentTarget(this)) return;
        if (Input.GetKeyDown(interactKey))
        {
            TryPickup();
        }
    }

    private void TryPickup()
    {
        // 첫 F 입력은 "열기"만 수행합니다 - 실제 획득은 그 다음 F 입력부터입니다.
        if (!isOpened)
        {
            Open();
            return;
        }

        EnsurePlayerRefsCached();

        bool anyConsumedThisTry = false;

        // 기본 아이템 - 현재 체력만 "꽉 차면 건너뛴다"는 조건이 있고, 나머지는 항상 성공합니다.
        foreach (var stack in basicStacks)
        {
            if (stack.consumed) continue;

            if (stack.type == BasicItemType.Health)
            {
                if (playerHealth == null || playerStats == null || playerHealth.CurrentHealth >= playerStats.MaxHealth)
                {
                    continue; // 꽉 차 있으면 이번엔 건너뜁니다 - 상자에 그대로 남습니다.
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

        // 패시브/액티브/장신구/일회용 아이템 (분류에 따라 습득 경로가 다릅니다)
        if (passiveItem != null)
        {
            if (IsSlotCategory(passiveItem.category))
            {
                // [슬롯 교체 확인] 장착 중인 아이템에 "교체 시 확인"이 체크돼 있으면 바로 바꾸지 않고 확인창을
                // 띄웁니다. [예]를 누르면 ConfirmSlotSwap()이 아래 즉시 교체와 똑같은 처리(DoSlotSwap)를 합니다.
                // (상자에는 기본 아이템과 플레이어 아이템이 함께 들어있지 않으므로 여기서 바로 끝내도 됩니다.)
                ItemDefinition equipped = GetEquippedSlotItem(passiveItem.category);
                if (SlotReplaceConfirmUI.NeedsConfirm(equipped))
                {
                    ItemDefinition incoming = passiveItem;
                    SlotReplaceConfirmUI.Show(equipped, incoming, () => ConfirmSlotSwap(incoming));
                    return;
                }

                if (DoSlotSwap()) anyConsumedThisTry = true;
            }
            else if (playerInventory != null)
            {
                ItemDefinition passiveToAcquire = passiveItem;
                if (GameFlowManager.AcquireItemCopy(passiveToAcquire, passiveItemIsFresh,
                        () => playerInventory.TryAcquireItem(passiveToAcquire)))
                {
                    ItemPickupEvents.Raise(passiveItem); // 방금 가져간 아이템 - 획득 연출 신호
                    passiveItem = null;
                    passiveItemIsFresh = false;
                    UpdateIconVisual(); // 습득했으니 상자 위 아이콘도 함께 제거합니다.
                    anyConsumedThisTry = true;
                }
            }
        }

        if (IsFullyConsumed())
        {
            // 판정(Collider)은 즉시 꺼지고, 상자 자체는 서서히 투명해지다가 사라집니다.
            // 액티브 아이템으로 다시 채워진 경우엔 UpdateIconVisual()에서 이미 살아 남았을
            // 것이므로 여기 도달하지 않습니다.
            DissolveAndVanish.Begin(gameObject, DissolveAndVanish.EndAction.Destroy);
        }
        else if (!anyConsumedThisTry)
        {
            Debug.Log("TreasureChest: 지금은 더 가져갈 수 있는 것이 없습니다 (예: 체력이 이미 가득 참).");
        }
    }

    // ------------------------------------------------------------------
    // 슬롯 아이템(액티브/장신구/일회용/음식) 교체
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

    // 상자 안의 슬롯 아이템을 장착합니다(기존 즉시 교체와 동일한 규칙). 방출된 아이템이 있으면(이미 같은 분류를
    // 장착 중이었던 경우) 상자를 비우는 대신 그 아이템으로 다시 채우고 아이콘도 갈아 끼웁니다 - 그래야 마음이
    // 바뀌면 다시 주울 수 있습니다. 없으면(처음 장착) 비웁니다. 장착에 성공하면 true.
    private bool DoSlotSwap()
    {
        if (passiveItem == null) return false;

        ItemDefinition incoming = passiveItem;
        ItemDefinition displacedItem = null;
        // [1회성 지급 - 교체 복제 방지] 새로 뽑은 사본일 때만 획득 시 1회성 지급을 받습니다(GameFlowManager.AcquireItemCopy).
        bool acquired = GameFlowManager.AcquireItemCopy(incoming, passiveItemIsFresh, () =>
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
        if (!acquired) return false;

        ItemPickupEvents.Raise(incoming); // 방금 가져간 아이템 - 획득 연출 신호
        passiveItem = displacedItem;
        passiveItemIsFresh = false; // 교체로 들어온 사본(또는 비었음) - 다시 주워도 1회성 지급 없음
        UpdateIconVisual();
        return true;
    }

    // 확인창에서 [예]를 눌렀을 때. 그 사이 상자 내용이 바뀌었으면(이론상 없음) 아무것도 하지 않습니다.
    private void ConfirmSlotSwap(ItemDefinition incoming)
    {
        if (this == null || passiveItem == null || passiveItem != incoming) return;

        EnsurePlayerRefsCached();
        DoSlotSwap();

        if (IsFullyConsumed())
        {
            DissolveAndVanish.Begin(gameObject, DissolveAndVanish.EndAction.Destroy);
        }
    }

    private bool IsFullyConsumed()
    {
        if (passiveItem != null) return false;
        foreach (var stack in basicStacks)
        {
            if (!stack.consumed) return false;
        }
        return true;
    }

    private void EnsurePlayerRefsCached()
    {
        if (playerHealth == null) playerHealth = Object.FindFirstObjectByType<PlayerHealth>();
        if (playerStats == null) playerStats = Object.FindFirstObjectByType<PlayerStats>();
        if (playerResources == null) playerResources = Object.FindFirstObjectByType<PlayerResources>();
        if (playerInventory == null) playerInventory = Object.FindFirstObjectByType<PlayerInventory>();
        if (playerActiveItem == null) playerActiveItem = Object.FindFirstObjectByType<PlayerActiveItem>();
        if (playerTrinket == null) playerTrinket = Object.FindFirstObjectByType<PlayerTrinket>();
        if (playerDisposableItem == null) playerDisposableItem = Object.FindFirstObjectByType<PlayerDisposableItem>();
        if (playerFoodItem == null) playerFoodItem = Object.FindFirstObjectByType<PlayerFoodItem>();
    }
}
