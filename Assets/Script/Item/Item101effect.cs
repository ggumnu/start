using UnityEngine;

// 패시브 101번 - "보유 중 회복량이 두 배가 된다."
//
// [기획 - 형빈 확정]
// - 체력이 오르는 모든 경우에 두 배가 적용됩니다.
//   - 보물상자/상점의 "현재 체력+1"(1칸 → 2칸), 아이템 회복 효과(21/32/36/72/88번 등).
//   - 최대 체력이 오르는 모든 경우: 최대 체력이 오른 만큼의 두 배를 현재 체력에 더합니다(예: 3/5에서 최대 체력+1 → 5/6).
//     기본 스탯 아이템의 최대 체력 증가, 17번/34번의 최대 체력 증가 모두 포함.
// - 회복은 최대 체력을 넘지 않습니다(넘는 만큼은 버려짐). 보물상자 체력 묶음은 그대로 소모되고, 남는 회복량을 돌려주지 않습니다.
// - 101번을 가지고 있을 때만 두 배이고, 잃으면(48번 등) 그 뒤로는 다시 1배입니다. 이미 회복된 체력은 그대로 둡니다.
// - 최대 체력이 줄어드는 경우에는 영향이 없습니다.
//
// [구현]
// HealBoostOverride 등록소에 배율을 등록/해제합니다. 실제 계산은 PlayerHealth(Heal / 최대 체력 변화 처리)가 합니다.
//
// [아이템 에셋 설정]
// ItemDefinition: Item Id = 101, Category = Passive, Special Effects에 HealBoostEffect 하나. Stat Modifiers는 비워둡니다.
[System.Serializable]
public class HealBoostEffect : IPassiveItemEffect, IRemovablePassiveEffect
{
    [Tooltip("회복량에 곱하는 배율입니다(기본 2 = 두 배). 최대 체력이 오를 때 현재 체력이 오르는 양에도 똑같이 곱합니다.")]
    [Min(1f)] public float healMultiplier = 2f;

    public void Apply(PassiveEffectContext context, string sourceItemId)
    {
        if (context == null || context.PlayerObject == null)
        {
            Debug.LogWarning("HealBoostEffect: 플레이어를 찾을 수 없어 101번 효과를 적용하지 못했습니다.");
            return;
        }

        HealBoostOverride.Register(SourceKey(sourceItemId), context.PlayerObject, healMultiplier);
    }

    // [패시브 잃기 - 48번 등] 그 뒤로는 1배로 돌아갑니다. 이미 회복된 체력은 그대로 둡니다.
    public void Remove(PassiveEffectContext context, string sourceItemId)
    {
        HealBoostOverride.Unregister(SourceKey(sourceItemId));
    }

    private static string SourceKey(string sourceItemId) => "heal_boost_" + sourceItemId;
}
