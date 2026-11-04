using UnityEngine;

// 패시브 37번 - "획득 시 수류탄 1개와 해킹툴 1개를 얻는다(레벨당 최초 1회). 보유하고 있으면 체력 소모 특수방을 드나들 때
// 체력을 소모하지 않는다(피격 자체가 일어나지 않음)."
//
// [기획 - 형빈 확정]
// 1) 자원 지급: 획득할 때 수류탄 grenadeAmount(기본 1)개 + 해킹툴 hackingToolAmount(기본 1)개를 줍니다. 단 "하나의 레벨"
//    기준 최초 1회입니다(10번/26번, 다른 일회용/장신구의 1회성 지급과 같은 규칙 - GameFlowManager.TryConsumeOneTimeGrant).
//    같은 레벨에서 잃어버린 뒤 다시 획득하면 효과는 다시 생기지만 자원은 주지 않고, 다음 레벨에서 다시 획득하면 다시
//    줍니다(n.5 선택방 레벨은 같은 레벨로 취급). 1회 기록은 37번 전용 키라 10번/26번 지급과 섞이지 않습니다.
// 2) 체력 소모 특수방 출입 피해 무효: 이 아이템을 들고 있는 동안, 체력 소모 특수방과 부모 방 사이를 지날 때 피해 처리
//    자체를 건너뜁니다. PlayerHealth.TakeDamage가 아예 호출되지 않으므로 피격 깜빡임/무적 시간/21번 장신구 판정도
//    일어나지 않습니다. 별도 연출은 없습니다 - "맞아야 할 곳에서 안 맞는 것" 자체가 아이템의 신호입니다(형빈 확정).
//
// [구현 - 기존 스크립트는 고치지 않았습니다]
// SpecialRoomHealthHazard를 만들 때 이 아이템을 위해 미리 열어둔 SpecialRoomHealthDamageOverride에 등록만 합니다.
// 등록돼 있는 동안 SpecialRoomHealthHazard가 피해를 주지 않습니다. 등록 소유자는 플레이어 오브젝트라 다음 레벨로
// 넘어가도 유지되고, 새 게임(씬 재시작)에서는 플레이어가 새로 만들어지면서 자동으로 빠집니다. 나중에 아이템을 잃는
// 기능이 생기면 SpecialRoomHealthDamageOverride.Unblock(BlockSourceId(itemId))를 호출하면 효과가 멈춥니다.
//
// [아이템 에셋 설정]
// ItemDefinition: Item Id = 37, Category = Passive, Special Effects에 HealthRoomPassEffect 하나.
[System.Serializable]
public class HealthRoomPassEffect : IPassiveItemEffect, IRemovablePassiveEffect
{
    [Tooltip("획득할 때 주는 수류탄 개수입니다. 같은 레벨에서는 한 번만 줍니다(잃어버렸다가 다시 획득해도 다시 주지 않음). 단, 48번으로 얻으면 제한 없이 줍니다.")]
    [Min(0)] public int grenadeAmount = 1;

    [Tooltip("획득할 때 주는 해킹툴 개수입니다. 수류탄과 같은 규칙으로 같은 레벨에서는 한 번만 줍니다.")]
    [Min(0)] public int hackingToolAmount = 1;

    // 피해 막기 등록에 쓰는 이름입니다. 나중에 아이템을 잃는 기능에서 Unblock할 때 같은 이름을 쓰면 됩니다.
    public static string BlockSourceId(string itemId) => "special_room_health_block_" + itemId;

    public void Apply(PassiveEffectContext context, string sourceItemId)
    {
        if (context == null || context.PlayerObject == null)
        {
            Debug.LogWarning("HealthRoomPassEffect: 플레이어를 찾을 수 없어 효과를 적용하지 못했습니다.");
            return;
        }

        GrantResourcesOncePerLevel(context, sourceItemId);

        SpecialRoomHealthDamageOverride.Block(BlockSourceId(sourceItemId), context.PlayerObject);
    }

    // [패시브 잃기 - 48번 등] 체력 특수방 피해 차단을 해제합니다. 1회성으로 받은 수류탄/해킹툴은 빼앗지 않습니다(형빈 확정).
    public void Remove(PassiveEffectContext context, string sourceItemId)
    {
        SpecialRoomHealthDamageOverride.Unblock(BlockSourceId(sourceItemId));
    }

    private void GrantResourcesOncePerLevel(PassiveEffectContext context, string sourceItemId)
    {
        if (grenadeAmount <= 0 && hackingToolAmount <= 0) return;

        if (context.Resources == null)
        {
            Debug.LogWarning("HealthRoomPassEffect: PlayerResources를 찾을 수 없어 수류탄/해킹툴을 지급하지 못했습니다.");
            return;
        }

        // 레벨당 1회 게이트입니다(GameFlowManager - 다음 정수 레벨로 넘어갈 때만 초기화). 수류탄과 해킹툴은 한 묶음으로
        // 같이 지급되므로 키 하나로 함께 막습니다. 아이템 테스트 씬처럼 GameFlowManager가 없는 곳에서는 막을 기준이
        // 없으므로 경고만 남기고 지급합니다.
        // [48번 연동, 형빈 확정] 48번으로 얻을 때는(PassiveEffectContext.IgnoreOneTimeGrantLimit) 레벨당 1회 제한 없이 지급합니다.
        if (!context.IgnoreOneTimeGrantLimit)
        {
            var gameFlow = Object.FindFirstObjectByType<GameFlowManager>();
            if (gameFlow != null)
            {
                if (!gameFlow.TryConsumeOneTimeGrant(sourceItemId + "_entry_kit_grant")) return;
            }
            else
            {
                Debug.LogWarning("HealthRoomPassEffect: GameFlowManager를 찾지 못해 레벨당 1회 제한 없이 수류탄/해킹툴을 지급합니다.");
            }
        }

        if (grenadeAmount > 0) context.Resources.Add(BasicItemType.Grenade, grenadeAmount);
        if (hackingToolAmount > 0) context.Resources.Add(BasicItemType.HackingTool, hackingToolAmount);
    }
}
