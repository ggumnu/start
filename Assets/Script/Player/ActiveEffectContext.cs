using UnityEngine;

// 액티브 아이템 효과(IActiveItemEffect.Activate)가 필요로 할 수 있는 참조들을 한데 모아 담는
// 컨테이너입니다. PassiveEffectContext와 같은 목적이지만, 액티브 효과는 체력 회복처럼
// PlayerHealth를 직접 건드리는 경우가 많아 PassiveEffectContext에는 없는 PlayerHealth를
// 대신 담습니다(패시브 효과는 지금까지 PlayerHealth가 필요한 경우가 없었습니다).
//
// 지연 소모(대기) 상태를 가지는 액티브 효과(IDelayedActiveItemEffect)는 EnterPending()/
// CompletePendingUse()로 PlayerActiveItem에 "대기에 들어갔다/끝났다"를 알립니다.
//
// 나중에 또 다른 참조가 필요해지면, 이 클래스에 프로퍼티 하나만 추가하면 됩니다 -
// IActiveItemEffect 인터페이스나 기존 효과 구현체들은 전혀 손댈 필요가 없습니다.
//
// PlayerActiveItem.Awake()에서 한 번만 만들어 캐싱해두고, 사용할 때마다 재사용합니다.
public class ActiveEffectContext
{
    public PlayerStats Stats { get; }
    public PlayerHealth Health { get; }
    public PlayerResources Resources { get; }
    public GameObject PlayerObject { get; }
    // 레벨 구조를 다루는 액티브 효과(예: 16번 - 방 추가)가 씁니다. 못 찾았으면 null일 수 있습니다.
    public LevelBuilder LevelBuilder { get; }

    public Transform PlayerTransform => PlayerObject != null ? PlayerObject.transform : null;

    private readonly PlayerActiveItem owner;

    public ActiveEffectContext(PlayerStats stats, PlayerHealth health, PlayerResources resources, GameObject playerObject)
        : this(stats, health, resources, playerObject, null)
    {
    }

    public ActiveEffectContext(PlayerStats stats, PlayerHealth health, PlayerResources resources, GameObject playerObject, PlayerActiveItem owner)
        : this(stats, health, resources, playerObject, owner, null)
    {
    }

    public ActiveEffectContext(PlayerStats stats, PlayerHealth health, PlayerResources resources, GameObject playerObject, PlayerActiveItem owner, LevelBuilder levelBuilder)
    {
        Stats = stats;
        Health = health;
        Resources = resources;
        PlayerObject = playerObject;
        this.owner = owner;
        LevelBuilder = levelBuilder;
    }

    // 이 액티브 아이템이 지연 소모 대기 상태에 들어갔음을 알립니다. 대기 중에는 스페이스 입력을
    // 받지 않고 HUD 아이콘이 반투명해집니다. 효과의 Activate() 안에서 호출하세요.
    public void EnterPending()
    {
        owner?.SetPending(true);
    }

    // 대기가 끝났음을 알립니다(효과가 실제로 발동을 마쳤을 때). 이후 다시 사용할 수 있습니다.
    public void CompletePendingUse()
    {
        owner?.SetPending(false);
    }
}
