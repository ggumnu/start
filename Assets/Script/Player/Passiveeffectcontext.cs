using UnityEngine;

// 패시브 아이템 효과(IPassiveItemEffect.Apply)가 필요로 할 수 있는 참조들을 한데 모아 담는
// 컨테이너입니다. 원래는 Apply(PlayerStats stats, ...)처럼 스탯만 넘겨받았지만, 공격 방식을
// 바꾸거나 특수 효과를 거는 아이템은 PlayerShooting 등 다른 컴포넌트에도 접근해야 해서
// 이 컨텍스트를 대신 넘기는 방식으로 바꿨습니다.
//
// 나중에 또 다른 참조(예: PlayerHealth, PlayerAimAndAttack)가 필요해지면, 이 클래스에
// 프로퍼티 하나만 추가하면 됩니다 - IPassiveItemEffect 인터페이스나 기존 효과 구현체들은
// 전혀 손댈 필요가 없습니다.
//
// PlayerInventory.Awake()에서 한 번만 만들어 캐싱해두고, 아이템을 획득할 때마다 재사용합니다.
public class PassiveEffectContext
{
    public PlayerStats Stats { get; }
    public PlayerShooting Shooting { get; }
    public PlayerInventory Inventory { get; }
    // 최대 코스트(maxCost) 등 재화를 직접 건드려야 하는 패시브 아이템(24번 등)을 위해 추가.
    // TrinketEffectContext에 이미 같은 이유로 Resources가 있던 것과 동일한 패턴입니다.
    public PlayerResources Resources { get; }
    // 현재 체력(CurrentHealth)을 읽어야 하는 패시브 아이템(17번 등)을 위해 추가. 위와 동일한
    // 패턴 - PlayerHealth가 없어도 컨텍스트 생성 자체는 막지 않고, null 그대로 담아둡니다.
    public PlayerHealth Health { get; }
    public GameObject PlayerObject { get; }

    // 자주 쓰일 만한 파생 값이라 편의상 프로퍼티로 열어둡니다.
    public Transform PlayerTransform => PlayerObject != null ? PlayerObject.transform : null;

    // [48번 연동 - 레벨당 1회 제한 풀기, 형빈 확정] true인 동안 얻는 패시브는 "같은 레벨에서는 한 번만" 주던 1회성
    // 지급(10/26/37/54번의 수류탄/해킹툴/크레딧 등)을 제한 없이 줍니다. PlayerInventory.ReplaceItems(48번)가 새
    // 아이템을 얻게 하는 동안에만 잠깐 켰다가 바로 끕니다. 평소 획득(보물상자 등)에서는 항상 false라 기존과 같습니다.
    public bool IgnoreOneTimeGrantLimit { get; set; }

    public PassiveEffectContext(PlayerStats stats, PlayerShooting shooting, PlayerInventory inventory, PlayerResources resources, PlayerHealth health, GameObject playerObject)
    {
        Stats = stats;
        Shooting = shooting;
        Inventory = inventory;
        Resources = resources;
        Health = health;
        PlayerObject = playerObject;
    }
}
