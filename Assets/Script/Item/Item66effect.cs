using UnityEngine;

// 패시브 66번 - "획득 시 수류탄 2개를 얻는다(레벨당 최초 1회). 보유하고 있으면 수류탄 폭발에 휘말린 적이 해킹된다."
//
// [기획 - 형빈 확정]
// - 수류탄 지급: 획득할 때 grenadeAmount(기본 2)개를 줍니다. "하나의 레벨" 기준 최초 1회입니다(10/37/57번과 같은 규칙 -
//   GameFlowManager.TryConsumeOneTimeGrant). 같은 레벨에서 잃었다가 다시 얻으면 효과는 다시 생기지만 수류탄은 주지 않고, 다음
//   레벨에서 다시 얻으면 다시 줍니다(n.5 선택방 레벨은 같은 레벨). 48번으로 얻으면 제한 없이 줍니다. 1회 기록은 66번 전용 키라
//   다른 아이템의 지급과 섞이지 않습니다. 잃어도 받은 수류탄은 빼앗지 않습니다.
// - 수류탄이 터지는 순간, 폭발 범위(수류탄 폭발 반경/높이 그대로) 안의 살아 있는 적에게 해킹툴과 똑같은 "해킹"을 겁니다.
//   해킹된 적은 기존 해킹 규칙대로 이동/공격을 하지 않습니다(EnemyControlState).
// - 범위는 수류탄 폭발 범위 그대로입니다. 해킹툴 전개 범위(해킹 범위 스탯)는 전혀 영향을 주지 않습니다.
//   예) 수류탄 폭발 반경 2, 해킹 범위 3 -> 66번의 해킹 범위는 2.
// - 해킹툴에서 가져오는 것은 해킹 지속시간(해킹 지속시간 스탯 + 6번 보너스)과 해킹 이펙트/색뿐입니다. 해킹 데미지 스탯은 쓰지
//   않습니다(이 해킹은 피해 0).
// - 같은 순간 폭발 범위 안의 적 총알도 없애고, 해킹툴 전개 이펙트(폭발 반경에 맞춘 크기)와 총알 제거 이펙트를 함께 생성합니다.
// - 해킹툴과 같은 해킹 상태라 41번(해킹된 적이 받는 피해 +50%)이 그대로 적용됩니다. 수류탄 피해가 먼저 들어가고 해킹은 그
//   직후에 걸리므로, 해킹을 거는 그 폭발 피해에는 41번이 붙지 않습니다(형빈 확정 - 해킹툴과 같은 순서). 이미 해킹된 적에게
//   다음 폭발이 들어가면 그 피해에는 붙습니다.
// - 그 폭발 피해로 죽은 적에게는 해킹을 걸지 않습니다.
//
// [구현]
// PlayerGrenadeThrower.OnGrenadeExploded(폭발 위치, 폭발 반경 - 폭발 피해 직후 발생)를 구독해서 PlayerHackTool.ApplyHackArea
// (해킹툴 전개와 같은 공용 해킹 처리)를 피해 0으로 부릅니다. 해킹툴의 "해킹툴 전개" 알림(OnHackDeployed)은 보내지 않습니다.
// 잃으면 구독만 풀고, 이미 걸린 해킹은 원래 시간대로 끝납니다. 수류탄 지급은 57번과 같은 방식입니다.
//
// [아이템 에셋 설정]
// ItemDefinition: Item Id = 66, Category = Passive, Special Effects에 GrenadeHackEffect 하나(수류탄 지급 개수만 설정). 이펙트/색은
// 플레이어 프리팹의 PlayerHackTool 값을 그대로 쓰므로 따로 설정할 것이 없습니다.
[System.Serializable]
public class GrenadeHackEffect : IPassiveItemEffect, IRemovablePassiveEffect
{
    [Tooltip("획득할 때 주는 수류탄 개수입니다. 같은 레벨에서는 한 번만 줍니다(잃어버렸다가 다시 획득해도 다시 주지 않음). 단, 48번으로 얻으면 제한 없이 줍니다.")]
    [Min(0)] public int grenadeAmount = 2;

    [System.NonSerialized] private PlayerGrenadeThrower subscribedThrower;
    [System.NonSerialized] private PlayerHackTool hackTool;

    public void Apply(PassiveEffectContext context, string sourceItemId)
    {
        if (context == null || context.PlayerObject == null)
        {
            Debug.LogWarning("GrenadeHackEffect: 플레이어를 찾을 수 없어 66번 효과를 적용하지 못했습니다.");
            return;
        }

        var thrower = context.PlayerObject.GetComponent<PlayerGrenadeThrower>();
        var tool = context.PlayerObject.GetComponent<PlayerHackTool>();
        if (thrower == null || tool == null)
        {
            Debug.LogWarning("GrenadeHackEffect: PlayerGrenadeThrower 또는 PlayerHackTool을 찾을 수 없어 66번 효과를 적용하지 못했습니다.");
            return;
        }

        GrantGrenadesOncePerLevel(context, sourceItemId);

        Unsubscribe(); // 혹시 남아 있을 이전 구독 정리(중복 방지)

        hackTool = tool;
        subscribedThrower = thrower;
        subscribedThrower.OnGrenadeExploded += HandleGrenadeExploded;

        // [81번 연동] 66번 보유 중임을 알립니다 - 81번 소이탄의 불타는 구역이 피해를 준 적에게도 틱마다 해킹을 겁니다.
        GrenadeHackOverride.Register(HackOverrideSourceId(sourceItemId), context.PlayerObject);
    }

    // [패시브 잃기 - 48번 등] 구독만 풉니다. 이미 걸린 해킹은 원래 시간대로 끝납니다.
    public void Remove(PassiveEffectContext context, string sourceItemId)
    {
        Unsubscribe();
        GrenadeHackOverride.Unregister(HackOverrideSourceId(sourceItemId));
    }

    private static string HackOverrideSourceId(string itemId) => "grenade_hack_" + itemId;

    private void GrantGrenadesOncePerLevel(PassiveEffectContext context, string sourceItemId)
    {
        if (grenadeAmount <= 0) return;

        if (context.Resources == null)
        {
            Debug.LogWarning("GrenadeHackEffect: PlayerResources를 찾을 수 없어 수류탄을 지급하지 못했습니다.");
            return;
        }

        // 레벨당 1회 게이트입니다(GameFlowManager - 다음 정수 레벨로 넘어갈 때만 초기화). 아이템 테스트 씬처럼
        // GameFlowManager가 없는 곳에서는 막을 기준이 없으므로 경고만 남기고 지급합니다.
        // [48번 연동, 형빈 확정] 48번으로 얻을 때는(PassiveEffectContext.IgnoreOneTimeGrantLimit) 레벨당 1회 제한 없이 지급합니다.
        if (!context.IgnoreOneTimeGrantLimit)
        {
            var gameFlow = Object.FindFirstObjectByType<GameFlowManager>();
            if (gameFlow != null)
            {
                if (!gameFlow.TryConsumeOneTimeGrant(sourceItemId + "_grenade_grant")) return;
            }
            else
            {
                Debug.LogWarning("GrenadeHackEffect: GameFlowManager를 찾지 못해 레벨당 1회 제한 없이 수류탄을 지급합니다.");
            }
        }

        context.Resources.Add(BasicItemType.Grenade, grenadeAmount);
    }

    private void Unsubscribe()
    {
        if (subscribedThrower != null) subscribedThrower.OnGrenadeExploded -= HandleGrenadeExploded;
        subscribedThrower = null;
        hackTool = null;
    }

    // 수류탄이 터진 직후: 폭발 위치/폭발 반경/폭발 높이 그대로, 피해 0으로 해킹을 겁니다.
    private void HandleGrenadeExploded(Vector3 position, float radius)
    {
        if (hackTool == null || subscribedThrower == null || radius <= 0f) return;

        hackTool.ApplyHackArea(position, radius, Mathf.Max(subscribedThrower.explosionHeight, 0.1f), 0f, null);
    }
}
