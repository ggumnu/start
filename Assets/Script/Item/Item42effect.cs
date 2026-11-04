// 장신구 42번 - "장착하고 있는 동안 무적시간(피격 후 추가 피해를 받지 않는 시간)이
// invincibilityDurationBonus(기본 0.5초)만큼 늘어난다."
//
// [기획]
// - PlayerHealth.invincibilityDuration(기본 0.5초)에 이 값을 더한 시간이 실제 무적
//   시간으로 적용됩니다 - 기본 무적시간 자체를 바꾸는 게 아니라 그 위에 보너스를 얹는
//   방식입니다(PlayerHealth.TakeDamage() 참고).
// - 다른 장신구로 교체되면(Remove) 보너스가 즉시 사라져 원래 무적시간으로 돌아갑니다.
//
// [구현 - PlayerHealth.SetInvincibilityDurationBonus()]
// 21번 장신구(FixedDamageTrinketEffect)가 PlayerHealth.SetFixedIncomingDamage()로
// "장착 중에만 유지되는 단일 값"을 걸었다 해제하는 것과 완전히 동일한 패턴입니다.
// 장신구는 한 번에 하나만 장착할 수 있어 단일 값(보너스 하나)으로 충분하고, Remove()에서
// 항상 0으로 되돌리므로 아무 장신구도 없을 때/다른 장신구로 교체됐을 때는 기존 동작과
// 완전히 동일합니다.
//
// [아이템 에셋 설정]
// ItemDefinition: Item Id = 42, Category = Trinket, Trinket Effect =
// InvincibilityDurationTrinketEffect.
[System.Serializable]
public class InvincibilityDurationTrinketEffect : ITrinketEffect
{
    [UnityEngine.Tooltip("장착 중일 때 무적시간(초)에 더해지는 보너스입니다. " +
                          "PlayerHealth.invincibilityDuration(기본 0.5초)에 이 값이 " +
                          "더해집니다.")]
    public float invincibilityDurationBonus = 0.5f;

    public void Apply(TrinketEffectContext context, string sourceItemId)
    {
        context?.Health?.SetInvincibilityDurationBonus(invincibilityDurationBonus);
    }

    public void Remove(TrinketEffectContext context, string sourceItemId)
    {
        context?.Health?.SetInvincibilityDurationBonus(0f);
    }
}
