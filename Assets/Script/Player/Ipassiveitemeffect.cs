// 패시브 아이템이 플레이어에게 실제로 어떤 효과를 가하는지를 표현하는 인터페이스입니다.
//
// [사용 방식 - 두 갈래가 전부 이 인터페이스 하나로 표현됩니다]
// - 즉시 적용형: Apply() 안에서 값을 한 번 바꾸고 끝냅니다 (예: StatModifierEffect).
// - 훅 등록형: Apply() 안에서 값을 바꾸는 대신, context가 제공하는 이벤트에 구독을 걸어둡니다.
//   이후 그 이벤트가 실제로 발생할 때마다(발사할 때마다, 탄이 사라질 때마다) 등록해둔 반응이
//   실행됩니다. 지금 사용 가능한 훅은 두 가지입니다:
//     - context.Shooting.OnProjectileSpawned (PlayerShooting.cs) - 발사마다 호출되며, 새로
//       생성된 Projectile을 넘겨줍니다.
//     - 위에서 넘겨받은 Projectile의 OnDespawning (Projectile.cs) - 그 탄환 하나가 사라지기
//       직전에 호출되며, 구독자가 ProjectileDespawnEvent.CancelDespawn을 true로 바꾸면 그
//       탄환은 사라지지 않습니다(관통/튕김 같은 효과를 위해 열어둔 자리입니다).
//
// IEnemyAttackBehavior와 동일한 "교체/확장 가능한 조각은 인터페이스로 분리" 패턴입니다.
//
// [ItemDefinition.specialEffects에 등록할 새 구현체를 만들 때 지켜야 할 것]
// - 클래스 선언 위에 [System.Serializable]을 붙일 것 (DashAttackConfig/ShootAttackConfig와
//   동일 - 안 붙이면 "[SerializeReference]로 직렬화되는데 [Serializable]이 없다"는 경고가 뜹니다).
// - 매개변수 없는 생성자를 가질 것 (필드 + 기본값만으로 구성) - PassiveItemEffectDrawer.cs가
//   인스펙터 드롭다운에 그 타입을 보여주려면 필요합니다. (StatModifierEffect처럼 매개변수
//   있는 생성자만 있는 클래스는 specialEffects 자리에는 올 수 없고, statModifiers 전용입니다.)
public interface IPassiveItemEffect
{
    // item 획득 시 PlayerInventory가 정확히 1번 호출합니다.
    // context에는 이 효과가 필요로 할 만한 참조(PlayerStats/PlayerShooting/PlayerInventory/
    // 플레이어 GameObject)가 담겨 있습니다.
    // sourceItemId는 어떤 아이템이 이 효과를 걸었는지 추적하기 위한 값으로,
    // 나중에 저주받은 아이템 해제 등 "특정 아이템 효과만 제거"가 필요해지면
    // PlayerStats.RemoveModifiersFromSource()와 함께 사용할 수 있습니다.
    void Apply(PassiveEffectContext context, string sourceItemId);
}