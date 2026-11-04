using UnityEngine;

// 패시브 41번 - "획득 시 최초 한정으로 해킹툴 3개를 얻는다. 소지하고 있으면, 해킹툴에 해킹당한 적이 해킹 지속 시간 동안
// 받는 모든 피해가 50% 증가한다."
//
// [기획 - 형빈 확정]
// - 획득하는 순간 해킹툴을 hackingToolGrant(기본 3)개 지급합니다. 패시브는 한 번만 얻을 수 있고(itemId 중복 방지)
//   Apply()도 획득 시 한 번만 실행되므로 자연스럽게 "최초 한정"입니다.
// - 소지하고 있는 동안, 해킹(EnemyControlType.Hack - 강력 제어)에 걸려 있는 적이 받는 모든 피해에 damageMultiplier
//   (기본 1.5 = +50%)를 곱합니다. 총알/폭발(4번)/수류탄/액티브 추가 피해 등 피해 종류와 무관하며, 보스도 해킹에 걸려
//   있으면 적용됩니다. 최종 피해 기준이고, 다른 "% 피해 증가" 효과와는 곱연산입니다(EnemyDamageTakenModifier 참고).
// - 해킹이 끝나면(지속시간 종료, 또는 31번 수면처럼 다른 강력 제어로 바뀌면) 효과도 바로 끝납니다.
// - 해킹툴은 피해를 먼저 주고 그 다음 해킹을 거는 순서라, 처음 해킹을 거는 그 피해에는 적용되지 않습니다. 이미 해킹된
//   적에게 해킹툴을 다시 쓰면 그 피해에는 적용됩니다(형빈 확정 - 순서를 바꾸지 않음).
//
// [구현]
// 하나의 효과 클래스로 두 가지를 모두 처리합니다. ItemDefinition의 Special Effects에 이 효과 하나만 추가하면 됩니다.
// - 지급: PlayerResources.Add(HackingTool) (30번 크레딧 지급과 같은 방식)
// - 피해 증가: EnemyDamageTakenModifier에 "이 적이 지금 해킹 중이면 damageMultiplier, 아니면 1"을 등록
[System.Serializable]
public class HackVulnerabilityEffect : IPassiveItemEffect, IRemovablePassiveEffect
{
    [Tooltip("획득 시 지급하는 해킹툴 개수입니다.")]
    public int hackingToolGrant = 3;
    [Tooltip("해킹에 걸려 있는 적이 받는 피해 배율입니다(1.5 = +50%). 다른 % 피해 증가와는 곱연산입니다.")]
    public float damageMultiplier = 1.5f;

    public void Apply(PassiveEffectContext context, string sourceItemId)
    {
        if (context == null) return;

        if (hackingToolGrant > 0)
        {
            if (context.Resources != null)
                context.Resources.Add(BasicItemType.HackingTool, hackingToolGrant);
            else
                Debug.LogWarning("HackVulnerabilityEffect: PlayerResources를 찾을 수 없어 해킹툴을 지급하지 못했습니다.");
        }

        if (context.PlayerObject == null)
        {
            Debug.LogWarning("HackVulnerabilityEffect: 플레이어 오브젝트를 찾을 수 없어 피해 증가 효과를 등록하지 못했습니다.");
            return;
        }

        float multiplier = damageMultiplier;
        EnemyDamageTakenModifier.SetMultiplier("hack_vulnerability_" + sourceItemId, context.PlayerObject,
            target => IsHacked(target) ? multiplier : 1f);
    }

    // [패시브 잃기 - 48번 등] 소지 중에만 유지되던 효과를 되돌립니다(IRemovablePassiveEffect.cs 참고).
    public void Remove(PassiveEffectContext context, string sourceItemId)
    {
        EnemyDamageTakenModifier.RemoveMultiplier("hack_vulnerability_" + sourceItemId);
    }

    // 이 적이 지금 해킹(강력 제어 Hack)에 걸려 있는지
    private static bool IsHacked(EnemyHealth target)
    {
        if (target == null) return false;
        var control = target.GetComponent<EnemyControlState>();
        return control != null && control.StrongType == EnemyControlType.Hack;
    }
}
