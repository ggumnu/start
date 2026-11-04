using UnityEngine;

// 장신구 효과(ITrinketEffect.Apply/Remove)가 필요로 할 수 있는 참조들을 한데 모아 담는
// 컨테이너입니다. 장신구는 "패시브 + 액티브"의 성격을 섞어놓은 분류라, PassiveEffectContext
// (Stats/Shooting)와 ActiveEffectContext(Stats/Health)를 합친 것에 더해, 같은 레벨 안에서의
// 1회성 지급 중복 방지를 위한 GameFlowManager 참조까지 갖고 있습니다.
//
// 나중에 또 다른 참조가 필요해지면, 이 클래스에 프로퍼티 하나만 추가하면 됩니다 -
// ITrinketEffect 인터페이스나 기존 효과 구현체들은 전혀 손댈 필요가 없습니다.
// [v1.27 추가] 크레딧 등 재화 지급 효과(39번 장신구)를 위해 Resources를 추가했습니다.
//
// PlayerTrinket.Awake()에서 한 번만 만들어 캐싱해두고, 장착/해제할 때마다 재사용합니다.
public class TrinketEffectContext
{
    public PlayerStats Stats { get; }
    public PlayerHealth Health { get; }
    public PlayerShooting Shooting { get; }
    public PlayerResources Resources { get; }
    public GameFlowManager GameFlow { get; }
    public GameObject PlayerObject { get; }

    public Transform PlayerTransform => PlayerObject != null ? PlayerObject.transform : null;

    public TrinketEffectContext(PlayerStats stats, PlayerHealth health, PlayerShooting shooting,
        PlayerResources resources, GameFlowManager gameFlow, GameObject playerObject)
    {
        Stats = stats;
        Health = health;
        Shooting = shooting;
        Resources = resources;
        GameFlow = gameFlow;
        PlayerObject = playerObject;
    }
}
