using UnityEngine;

// 보관 좌대입니다. 아이템 하나를 올려두고, 플레이어가 F로 가져가는 자리입니다.
//
// [왜 보물상자와 따로 두나 - 형빈 확정]
// 보물상자(TreasureChest/ItemPedestal)는 상자를 복제하거나(9번) 내용물을 바꾸는(48번/27번 등) 아이템의 대상입니다. 상점에서
// 교체된 아이템을 올려둘 자리가 보물상자로 취급되면 상점 안에서 상자 복제 같은 일이 생길 수 있어서, 보물상자 쪽 시스템
// (ContainerOpenEvents/IBasicItemContainer/IOpenableContainer 등)에 전혀 등록하지 않는 별도 컴포넌트로 만들었습니다.
// "열기" 단계도 없고, 올려진 아이템을 그대로 보여주고 가져가게만 합니다. 상점 밖에서도 쓸 수 있는 범용 좌대입니다.
//
// [상점에서의 쓰임]
// 슬롯 아이템(액티브/장신구/일회용/음식)을 이미 장착한 채로 상점에서 같은 분류 아이템을 사면, 원래 장착하던 아이템이 상점의
// 빈 보관 좌대에 올라갑니다(ShopNPC가 TryPlace 호출). 빈 좌대가 없으면 그 구매는 막힙니다.
//
// [가져가기 규칙 - 보물상자의 슬롯 아이템과 같음]
// - 슬롯 아이템: 지금 그 분류에 장착 중인 아이템과 맞바꿉니다 - 장착 중이던 아이템은 이 좌대에 남습니다(빈 슬롯이면 좌대가
//   비워짐). 장착 중인 아이템에 "교체 시 확인"이 체크돼 있으면 교체 확인창(SlotReplaceConfirmUI)을 먼저 띄웁니다.
// - 패시브 아이템: 그냥 획득하고 좌대가 비워집니다(이미 가진 패시브면 가져가지 않음).
// 가져갈 때 보물상자와 같은 획득 연출(ItemPickupEvents)이 나옵니다.
//
// [배치]
// 상점 바닥 프리팹 안에 좌대 모델을 두고 이 컴포넌트를 붙이면 됩니다(열린 벽 프리팹의 바리케이드처럼 프리팹 안에 미리 배치).
// 범위 판정은 보물상자와 같이 거리 계산(바닥 평면 거리)이라 콜라이더 구성과 무관합니다. 아이템 아이콘은 이 오브젝트 위
// iconHeight 높이(Icon Anchor를 넣으면 그 위치)에 상자 아이콘과 같은 크기/각도로 뜹니다.
//
// [다른 시스템과의 연결]
// - F 대상/F 안내: InteractionTargetSelector가 "아이템이 올려진 좌대"를 후보로 넣고, F 안내 문구는 "획득하기"입니다.
// - 아이템 설명칸(PlayerItemInfoDisplay)에 올려진 아이템 정보가 뜹니다.
// - 미니맵: 아이템이 올려져 있으면 그 방에 "Item" 아이콘이 남습니다(IMinimapMarker).
[DisallowMultipleComponent]
public class HoldingPedestal : MonoBehaviour, IMinimapMarker
{
    [Header("상호작용")]
    public KeyCode interactKey = KeyCode.F;
    [Tooltip("이 거리(바닥 평면 기준) 안에 플레이어가 들어오면 F로 아이템을 가져갈 수 있습니다.")]
    [Min(0.1f)] public float interactRadius = 1.2f;

    [Header("아이템 아이콘 (보물상자 아이콘과 같은 기본값)")]
    [Tooltip("넣으면 아이콘이 이 오브젝트 위치에 뜹니다. 비워두면 좌대 위 Icon Height 높이에 뜹니다.")]
    public Transform iconAnchor;
    public float iconHeight = 1f;
    public Vector3 iconScale = new Vector3(0.05f, 0.05f, 0.05f);
    [Tooltip("아이콘이 카메라 정면을 보도록 하는 고정 회전값입니다(상자 아이콘과 같은 값).")]
    public Vector3 iconFixedEulerAngles = new Vector3(55f, -45f, 0f);

    private ItemDefinition heldItem;
    private SpriteRenderer iconRenderer;

    private bool playerInRange = false;
    private Transform playerTransform;
    private PlayerHealth playerHealth;

    // 읽기 전용 상태 (F 대상 판정/F 안내/설명칸/상점이 읽음)
    public ItemDefinition HeldItem => heldItem;
    public bool IsEmpty => heldItem == null;
    public bool PlayerInRange => playerInRange;

    // IMinimapMarker - 아이템이 올려져 있을 때만 미니맵에 "Item" 아이콘을 남깁니다.
    public string MinimapIconKey => heldItem != null ? MinimapKeys.Item : null;

    // 아이템을 올립니다. 이미 다른 아이템이 올려져 있으면 올리지 않고 false.
    public bool TryPlace(ItemDefinition item)
    {
        if (item == null || heldItem != null) return false;
        heldItem = item;
        UpdateIconVisual();
        return true;
    }

    void Update()
    {
        UpdatePlayerInRange();

        if (!playerInRange || heldItem == null) return;
        if (SlotReplaceConfirmUI.BlocksInteract) return; // 슬롯 교체 확인창이 떠 있거나 방금 닫힌 프레임
        if (!InteractionTargetSelector.IsCurrentTarget(this)) return;
        if (Input.GetKeyDown(interactKey))
        {
            TryPickup();
        }
    }

    // 상자 아이템과 같은 빛 이펙트(등급 색)를 아이콘에 붙입니다(ItemGlowSpawner가 씬에 없으면 아무 일도 없음).
    void LateUpdate()
    {
        ItemGlowSpawner.Sync(iconRenderer, heldItem);
    }

    private void UpdatePlayerInRange()
    {
        // 플레이어 루트(PlayerHealth가 붙은 오브젝트)를 찾습니다. "Player" 태그는 플레이어 프리팹의 자식(FirePoint/모델 등)에도
        // 붙어 있어서 태그로 찾으면 슬롯 컴포넌트가 없는 자식이 잡힐 수 있습니다.
        if (playerTransform == null)
        {
            var health = UnityEngine.Object.FindFirstObjectByType<PlayerHealth>();
            if (health != null)
            {
                playerHealth = health;
                playerTransform = health.transform;
            }
        }

        // 플레이어가 없거나 사망했으면(게임 오버 중) 범위 밖으로 취급합니다.
        if (playerTransform == null || (playerHealth != null && playerHealth.IsDead))
        {
            playerInRange = false;
            return;
        }

        Vector3 a = transform.position;
        Vector3 b = playerTransform.position;
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        playerInRange = dx * dx + dz * dz <= interactRadius * interactRadius;
    }

    private void TryPickup()
    {
        if (heldItem == null || playerTransform == null) return;
        GameObject player = playerTransform.gameObject;

        if (IsSlotCategory(heldItem.category))
        {
            ItemDefinition equipped = GetEquippedSlotItem(player, heldItem.category);
            if (SlotReplaceConfirmUI.NeedsConfirm(equipped))
            {
                ItemDefinition incoming = heldItem;
                SlotReplaceConfirmUI.Show(equipped, incoming, () => ConfirmSlotSwap(incoming));
                return;
            }
            DoSlotSwap(player);
            return;
        }

        // 패시브
        var inventory = player.GetComponent<PlayerInventory>();
        if (inventory != null && inventory.TryAcquireItem(heldItem))
        {
            ItemPickupEvents.Raise(heldItem);
            heldItem = null;
            UpdateIconVisual();
        }
    }

    // 교체 확인창에서 [예]를 눌렀을 때. 그 사이 좌대 내용이 바뀌었으면 아무것도 하지 않습니다.
    private void ConfirmSlotSwap(ItemDefinition incoming)
    {
        if (this == null || heldItem != incoming || playerTransform == null) return;
        DoSlotSwap(playerTransform.gameObject);
    }

    // 좌대의 슬롯 아이템을 장착하고, 원래 장착하던 아이템(있으면)을 이 좌대에 올립니다.
    private void DoSlotSwap(GameObject player)
    {
        ItemDefinition incoming = heldItem;
        if (!TryEquipSlotItem(player, incoming, out ItemDefinition displaced)) return;

        ItemPickupEvents.Raise(incoming); // 보물상자에서 주울 때와 같은 획득 연출
        heldItem = displaced;
        UpdateIconVisual();
    }

    // ------------------------------------------------------------------
    // 슬롯 아이템 공용 도우미 (상점 ShopNPC도 같이 씀)
    // ------------------------------------------------------------------

    public static bool IsSlotCategory(ItemDefinition.ItemCategory category)
    {
        return category == ItemDefinition.ItemCategory.Active ||
               category == ItemDefinition.ItemCategory.Trinket ||
               category == ItemDefinition.ItemCategory.Disposable ||
               category == ItemDefinition.ItemCategory.Food;
    }

    // 그 분류에 새 아이템을 넣으면 밀려날(교체될) 아이템(없으면 null). 교체 확인창/상점의 보관 좌대 필요 여부 판단에 씁니다.
    // 장신구는 빈칸이 있으면 null, 꽉 찼으면 가장 먼저 얻은 장신구입니다(55번으로 2칸일 수 있음 - PlayerTrinket.ReplacedOnAcquire).
    // 일회용(61번)/음식(79번)도 같습니다(각 컴포넌트의 ReplacedOnAcquire).
    public static ItemDefinition GetEquippedSlotItem(GameObject player, ItemDefinition.ItemCategory category)
    {
        if (player == null) return null;
        switch (category)
        {
            case ItemDefinition.ItemCategory.Active:
                var active = player.GetComponent<PlayerActiveItem>();
                return active != null ? active.EquippedItem : null;
            case ItemDefinition.ItemCategory.Trinket:
                var trinket = player.GetComponent<PlayerTrinket>();
                return trinket != null ? trinket.ReplacedOnAcquire : null;
            case ItemDefinition.ItemCategory.Disposable:
                var disposable = player.GetComponent<PlayerDisposableItem>();
                return disposable != null ? disposable.ReplacedOnAcquire : null; // 61번으로 2칸일 수 있음
            case ItemDefinition.ItemCategory.Food:
                var food = player.GetComponent<PlayerFoodItem>();
                return food != null ? food.ReplacedOnAcquire : null; // 79번으로 2칸일 수 있음
            default:
                return null;
        }
    }

    // 이 슬롯 아이템을 지금 장착 중인지(장신구/일회용/음식은 모든 칸 확인)
    public static bool IsSlotItemEquipped(GameObject player, ItemDefinition item)
    {
        if (player == null || item == null) return false;
        if (item.category == ItemDefinition.ItemCategory.Trinket)
        {
            var trinket = player.GetComponent<PlayerTrinket>();
            return trinket != null && trinket.IsEquipped(item);
        }
        if (item.category == ItemDefinition.ItemCategory.Disposable)
        {
            var disposable = player.GetComponent<PlayerDisposableItem>();
            return disposable != null && disposable.IsEquipped(item);
        }
        if (item.category == ItemDefinition.ItemCategory.Food)
        {
            var food = player.GetComponent<PlayerFoodItem>();
            return food != null && food.IsEquipped(item);
        }
        ItemDefinition equipped = GetEquippedSlotItem(player, item.category);
        return equipped != null && equipped.itemId == item.itemId;
    }

    // 그 분류의 슬롯 컴포넌트가 플레이어에게 있는지
    public static bool HasSlotComponent(GameObject player, ItemDefinition.ItemCategory category)
    {
        if (player == null) return false;
        switch (category)
        {
            case ItemDefinition.ItemCategory.Active: return player.GetComponent<PlayerActiveItem>() != null;
            case ItemDefinition.ItemCategory.Trinket: return player.GetComponent<PlayerTrinket>() != null;
            case ItemDefinition.ItemCategory.Disposable: return player.GetComponent<PlayerDisposableItem>() != null;
            case ItemDefinition.ItemCategory.Food: return player.GetComponent<PlayerFoodItem>() != null;
            default: return false;
        }
    }

    // 슬롯 아이템을 장착합니다(기존 즉시 교체와 같은 규칙 - 각 슬롯 컴포넌트의 TryAcquireItem). 원래 장착하던 아이템은 displaced.
    public static bool TryEquipSlotItem(GameObject player, ItemDefinition item, out ItemDefinition displaced)
    {
        displaced = null;
        if (player == null || item == null) return false;
        switch (item.category)
        {
            case ItemDefinition.ItemCategory.Active:
                var active = player.GetComponent<PlayerActiveItem>();
                return active != null && active.TryAcquireItem(item, out displaced);
            case ItemDefinition.ItemCategory.Trinket:
                var trinket = player.GetComponent<PlayerTrinket>();
                return trinket != null && trinket.TryAcquireItem(item, out displaced);
            case ItemDefinition.ItemCategory.Disposable:
                var disposable = player.GetComponent<PlayerDisposableItem>();
                return disposable != null && disposable.TryAcquireItem(item, out displaced);
            case ItemDefinition.ItemCategory.Food:
                var food = player.GetComponent<PlayerFoodItem>();
                return food != null && food.TryAcquireItem(item, out displaced);
            default:
                return false;
        }
    }

    // ------------------------------------------------------------------
    // 아이콘
    // ------------------------------------------------------------------

    // 보물상자(TreasureChest)와 같은 방식 - 좌대 회전과 상관없이 항상 "좌대 위 iconHeight" 월드 위치에, 카메라 정면 고정 각도로.
    private void UpdateIconVisual()
    {
        if (heldItem == null)
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
            iconRenderer = iconGO.AddComponent<SpriteRenderer>();
        }

        iconRenderer.transform.position = iconAnchor != null ? iconAnchor.position : transform.position + Vector3.up * iconHeight;
        iconRenderer.transform.rotation = Quaternion.Euler(iconFixedEulerAngles);
        iconRenderer.transform.localScale = iconScale;
        iconRenderer.sprite = heldItem.icon;
    }
}
