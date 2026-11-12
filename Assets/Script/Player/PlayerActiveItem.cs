using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 플레이어가 장착 중인 "액티브 아이템" 한 개를 관리합니다. PlayerInventory(패시브, 무제한
// 보유)와 완전히 별도의 저장소입니다 - 액티브 아이템은 [아이작 리펜던스]의 주사위류와
// 동일하게 항상 정확히 0개 또는 1개만 장착할 수 있고, 다른 액티브 아이템을 획득하면
// 기존 아이템과 "교체"됩니다(획득 X, 교체 O).
//
// [교체(swap) 계약 - TryAcquireItem]
// 새 액티브 아이템을 얻으려는 쪽(ItemPedestal/TreasureChest)이 이 메서드를 호출합니다.
// - 아직 장착 중인 게 없으면: newItem을 장착하고, displacedItem = null, 반환값 true.
// - 이미 장착 중인 게 있으면: newItem으로 갈아 끼우고, 원래 장착하고 있던 아이템을
//   displacedItem으로 돌려줍니다(반환값 true) - 호출한 쪽(보물상자)이 이 displacedItem을
//   자기 자리에 다시 채워 넣는 방식으로 "교체된 아이템이 보물상자로 이동"하는 것처럼
//   보이게 합니다. 이 스크립트는 월드에 있는 보물상자를 직접 건드리지 않습니다.
//
// [사용 - 스페이스 바]
// 장착 중인 아이템이 있고, 코스트가 충분하면(PlayerResources.TrySpend) Update()에서
// 스페이스 바 입력을 감지해 사용합니다. 코스트가 부족하면 아무 일도 일어나지 않습니다
// (TrySpend가 false를 반환하므로 효과도 호출되지 않고, 코스트도 소모되지 않습니다).
// 이미 최대 상태(예: 체력이 꽉 찬 상태에서 회복형 액티브를 사용)라도 코스트 지불에
// 성공했다면 Activate()는 호출되고 코스트는 소모됩니다 - 효과가 "일어났지만 변화가
// 없었을 뿐"이라는 의미입니다.
//
// [지연 소모 액티브 - IDelayedActiveItemEffect를 구현한 효과만 해당]
// 일부 효과(예: 93번)는 사용 뒤에도 "대기" 상태로 남습니다(IsPending). 이때는 스페이스 입력을
// 받지 않고(재사용 불가), HUD의 액티브 아이콘이 반투명해집니다. 대기 중에 다른 액티브 아이템으로
// 교체되면 효과의 CancelPending()만 호출한 뒤 그냥 교체합니다(이미 낸 코스트는 돌려주지
// 않습니다). 또 이런 효과는 코스트를 내기 전에 CanActivate()로 사용 가능 여부를 먼저 물어봐서,
// false면 코스트도 소모하지 않고 사용도 일어나지 않습니다. 이 인터페이스를 구현하지 않은 기존
// 효과들은 이 규칙과 무관하게 예전과 똑같이 동작합니다.
//
// [사용 규칙 액티브 - IActiveItemUseRules를 구현한 효과만 해당 (74번 등)]
// 코스트를 내기 전에 CanActivateNow()로 쿨타임 등 사용 조건을 먼저 확인합니다(false면 코스트도 소모하지 않고 사용도 일어나지
// 않음). 이번에 낼 코스트는 ResolveUseCost()가 정하며(GetCurrentUseCost), 0 이하면 코스트 없이 사용합니다(74번 - 전투 중이
// 아닐 때 무료). 남은 쿨타임 비율(CooldownRemaining01)은 좌상단 액티브 아이콘의 쿨타임 연출(PlayerCostDisplay)이 읽습니다.
// 이 인터페이스를 구현하지 않은 효과들은 예전과 똑같이 동작합니다.
[RequireComponent(typeof(PlayerStats))]
public class PlayerActiveItem : MonoBehaviour
{
    [Header("사용 입력")]
    public KeyCode useKey = KeyCode.Space;

    private PlayerStats stats;
    private PlayerHealth health;
    private PlayerResources resources;
    private ActiveEffectContext effectContext;

    private ItemDefinition equippedItem;
    private bool isPending;

    public ItemDefinition EquippedItem => equippedItem;

    // 지연 소모 액티브가 사용된 뒤 아직 끝나지 않은 대기 상태인지 여부입니다.
    public bool IsPending => isPending;

    void Awake()
    {
        stats = GetComponent<PlayerStats>();
        health = GetComponent<PlayerHealth>();
        resources = GetComponent<PlayerResources>();

        if (health == null)
        {
            Debug.LogWarning("PlayerActiveItem: 같은 오브젝트에서 PlayerHealth를 찾지 못했습니다. " +
                              "체력 관련 액티브 효과는 정상 동작하지 않을 수 있습니다.");
        }
        if (resources == null)
        {
            Debug.LogWarning("PlayerActiveItem: 같은 오브젝트에서 PlayerResources를 찾지 못했습니다. " +
                              "코스트를 지불할 수 없어 액티브 아이템을 사용할 수 없습니다.");
        }

        // 레벨 구조를 다루는 액티브 효과(예: 16번)를 위해 LevelBuilder도 함께 찾아 넘깁니다.
        var levelBuilder = Object.FindFirstObjectByType<LevelBuilder>();

        effectContext = new ActiveEffectContext(stats, health, resources, gameObject, this, levelBuilder);
    }

    void Update()
    {
        if (equippedItem == null || isPending) return;
        // 아이템 획득 연출(PlayerItemPickupPresenter) 중에는 액티브(Space) 사용을 막습니다(형빈 확정).
        if (PlayerItemPickupPresenter.BlocksActions) return;
        if (Input.GetKeyDown(useKey))
        {
            if (!TryUse()) GameAudio.Play(SfxId.ActionFail); // [사운드] 코스트 부족/지금 못 씀
        }
    }

    // 코스트 지불에 성공하면 효과를 발휘하고 true를 반환합니다. 코스트가 부족하면
    // 아무것도 바꾸지 않고 false를 반환합니다.
    public bool TryUse()
    {
        if (equippedItem == null || resources == null) return false;
        if (isPending) return false; // 지연 소모 대기 중에는 다시 사용할 수 없습니다.

        var effect = equippedItem.activeEffect;

        // 지연 소모 액티브는 코스트를 내기 전에 사용 가능 여부를 먼저 확인합니다 - 조건을
        // 만족하지 못하면 코스트도 소모하지 않고 사용도 일어나지 않습니다.
        if (effect is IDelayedActiveItemEffect delayed && !delayed.CanActivate(effectContext))
        {
            return false;
        }

        // [74번 등 - IActiveItemUseRules] 쿨타임 등 사용 조건도 코스트를 내기 전에 확인합니다 - false면 코스트도 소모하지 않습니다.
        var rules = effect as IActiveItemUseRules;
        if (rules != null && !rules.CanActivateNow(effectContext))
        {
            return false;
        }

        // 실제 소모 코스트(45번 등 코스트 감소 효과 반영 - ActiveCostModifier, 74번처럼 사용 규칙이 있으면 그 규칙 반영)를
        // 지불합니다. 사용 규칙이 코스트를 0 이하로 정했으면 지불 없이 사용합니다(그 외에는 예전과 같습니다).
        int cost = GetCurrentUseCost();
        bool free = rules != null && cost <= 0;
        if (!free && !resources.TrySpend(BasicItemType.Cost, cost))
        {
            return false;
        }

        // [사운드] 전용 소리가 있는 액티브(IHasOwnUseSound - 74/90번 등)는 공용 발동음 없이 전용 소리만 냅니다.
        if (!(effect is IHasOwnUseSound)) GameAudio.Play(SfxId.ItemActivate);
        effect?.Activate(effectContext);
        return true;
    }

    // 효과가 ActiveEffectContext.EnterPending()/CompletePendingUse()로 대기 상태를 알릴 때
    // 호출됩니다(다른 스크립트에서 직접 부르지 마세요).
    // 이 액티브 아이템을 쓸 때 실제로 지불하는 코스트입니다(아이템 원래 소모량 activeCost에 45번 등의 감소를 반영,
    // 최소 1 - ActiveCostModifier 참고). 좌상단 소모 코스트 표시(PlayerCostDisplay)도 이 값을 그대로 씁니다.
    public static int GetEffectiveCost(ItemDefinition item)
    {
        if (item == null) return 0;
        return ActiveCostModifier.Resolve(item.activeCost);
    }

    // 지금 스페이스를 누르면 실제로 낼 코스트입니다. 사용 규칙이 있는 액티브(74번 등)는 그 규칙이 정한 값(비전투 중 0 등), 그 외에는
    // GetEffectiveCost와 같습니다. 좌상단 소모 코스트 숫자(PlayerCostDisplay)도 이 값을 그대로 보여줍니다.
    public int GetCurrentUseCost()
    {
        if (equippedItem == null) return 0;
        int effectiveCost = GetEffectiveCost(equippedItem);
        if (equippedItem.activeEffect is IActiveItemUseRules rules)
        {
            return Mathf.Max(0, rules.ResolveUseCost(effectContext, effectiveCost));
        }
        return effectiveCost;
    }

    // 장착 중인 액티브의 남은 쿨타임 비율(1 = 막 사용함, 0 = 사용 가능)입니다. 쿨타임이 없는 액티브는 항상 0입니다.
    public float CooldownRemaining01
    {
        get
        {
            if (equippedItem != null && equippedItem.activeEffect is IActiveItemUseRules rules)
            {
                return Mathf.Clamp01(rules.CooldownRemaining01);
            }
            return 0f;
        }
    }

    public void SetPending(bool value)
    {
        isPending = value;
    }

    // newItem 장착을 시도합니다. 항상 true를 반환합니다(액티브는 "이미 갖고 있어서 실패"하는
    // 경우가 없습니다 - 대신 교체가 일어납니다). 원래 장착 중이던 아이템이 있었다면
    // displacedItem으로 돌려주고, 없었다면 null을 돌려줍니다.
    public bool TryAcquireItem(ItemDefinition newItem, out ItemDefinition displacedItem)
    {
        displacedItem = null;
        if (newItem == null) return false;

        // 지연 소모 대기 중이던 액티브라면 효과를 발동시키지 않고 대기만 취소한 뒤 그냥
        // 교체합니다(이미 낸 코스트는 돌려주지 않습니다).
        if (equippedItem != null && isPending)
        {
            (equippedItem.activeEffect as IDelayedActiveItemEffect)?.CancelPending(effectContext);
            isPending = false;
        }

        displacedItem = equippedItem;
        equippedItem = newItem;
        ItemCodexRecord.MarkAcquired(newItem); // [도감] 처음 인벤토리에 들어온 아이템이면 도감 기록에 남깁니다(테스트 씬 제외 - ItemCodexRecord.cs).

        Debug.Log(displacedItem == null
            ? $"PlayerActiveItem: '{newItem.displayName}' 장착."
            : $"PlayerActiveItem: '{newItem.displayName}' 장착 (기존 '{displacedItem.displayName}'은 방출됨).");

        return true;
    }

    // 이미 장착 중인 액티브 아이템(itemId 기준)을 풀에서 제외한 목록을 반환합니다.
    // ItemPedestal/TreasureChest가 뽑기 직전에 PlayerInventory.FilterUnowned()와 함께
    // 호출해야 하는 유틸입니다 - 그래야 지금 들고 있는 액티브 아이템이 다시 뽑히는 일이
    // 없습니다(장착 중이 아닌 다른 액티브 아이템은 계속 등장 가능합니다).
    public List<ItemDefinition> FilterOutEquipped(List<ItemDefinition> pool)
    {
        if (pool == null) return new List<ItemDefinition>();
        if (equippedItem == null || string.IsNullOrEmpty(equippedItem.itemId)) return pool;

        return pool
            .Where(item => item != null && item.itemId != equippedItem.itemId)
            .ToList();
    }
}
