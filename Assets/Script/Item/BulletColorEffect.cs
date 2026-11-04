using UnityEngine;

// [패시브 효과 - 플레이어 총알 색 바꾸기 (재사용용)]
// 아이템 에셋(ItemDefinition)의 Special Effects 목록에 넣으면, 그 아이템을 얻은 순간부터 플레이어 총알(분열탄 포함)과
// 꼬리가 Bullet Color로 바뀝니다. 어느 아이템에나 코드 없이 붙여 쓸 수 있습니다 - 첫 사용처는 38번(매료, 보라색).
//
// [여러 아이템이 총알 색을 바꿀 때 - 형빈 확정]
// Priority가 가장 높은 아이템의 색이 이깁니다. 같으면 나중에 얻은 아이템의 색이 이깁니다(ProjectileColorOverride 참고).
// 대부분의 아이템은 Priority 0으로 두면 "나중에 얻은 색"처럼 동작하고, 꼭 보여야 하는 색만 숫자를 올리면 됩니다.
//
// [패시브 잃기 - 48번 등] 이 아이템을 잃으면 Remove()에서 등록한 총알 색을 해제합니다. 장신구처럼 뺄 수 있는 아이템이
// 총알 색을 바꾸게 되면, 뺄 때 ProjectileColorOverride.RemoveColor()를 호출하는 장신구용 효과를 따로 만들어야 합니다.
[System.Serializable]
public class BulletColorEffect : IPassiveItemEffect, IRemovablePassiveEffect
{
    [Tooltip("이 아이템을 얻으면 바뀔 플레이어 총알(과 꼬리) 색입니다.")]
    public Color bulletColor = Color.white;

    [Tooltip("여러 아이템이 총알 색을 바꿀 때의 우선순위입니다. 높을수록 이기고, 같으면 나중에 얻은 아이템 색이 이깁니다.")]
    public int priority = 0;

    public void Apply(PassiveEffectContext context, string sourceItemId)
    {
        if (context == null || context.PlayerObject == null)
        {
            Debug.LogWarning("BulletColorEffect: 플레이어를 찾을 수 없어 총알 색을 적용하지 못했습니다.");
            return;
        }

        ProjectileColorOverride.SetColor("bullet_color_" + sourceItemId, context.PlayerObject, bulletColor, priority);
    }

    // [패시브 잃기 - 48번 등] 소지 중에만 유지되던 효과를 되돌립니다(IRemovablePassiveEffect.cs 참고).
    public void Remove(PassiveEffectContext context, string sourceItemId)
    {
        ProjectileColorOverride.RemoveColor("bullet_color_" + sourceItemId);
    }
}
