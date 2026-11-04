using UnityEngine;

// 가장 단순한 액티브 아이템 효과 - 사용 시 현재 체력을 healAmount만큼 회복합니다.
//
// [체력 회복 규칙 - PlayerHealth.Heal()에 그대로 위임]
// PlayerHealth.Heal(float amount)이 이미 원하는 규칙(최대 체력을 넘지 않도록 클램프, 부분
// 회복 허용)을 그대로 구현하고 있어 별도 로직이 필요 없습니다. 현재 체력이 이미 꽉 차있어도
// Heal() 호출 자체는 안전합니다(그냥 아무 변화가 없을 뿐입니다) - 코스트 소비 여부는 이 효과가
// 결정하는 게 아니라 PlayerActiveItem.TryUse()가 코스트 지불에 성공한 뒤에만 Activate()를
// 호출하므로, 이미 체력이 꽉 찬 상태에서 사용해도 코스트는 정상적으로 소모됩니다.
[System.Serializable]
public class HealActiveEffect : IActiveItemEffect
{
    [Tooltip("사용 시 회복할 체력입니다.")]
    public float healAmount = 1f;

    public void Activate(ActiveEffectContext context)
    {
        context.Health?.Heal(healAmount);
    }
}
