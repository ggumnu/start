using UnityEngine;

// 패시브 57번 "토끼꼬리 수류탄" - "획득 시 수류탄 2개를 얻는다(레벨당 최초 1회). 보유하고 있으면 수류탄에 의한 자해 데미지를 입지 않는다."
//
// [기획 - 형빈 확정]
// 1) 수류탄 지급: 획득할 때 grenadeAmount(기본 2)개를 줍니다. 단 "하나의 레벨" 기준 최초 1회입니다(10번/26번/37번과 같은
//    규칙 - GameFlowManager.TryConsumeOneTimeGrant). 같은 레벨에서 잃어버린 뒤 다시 획득하면 효과는 다시 생기지만 수류탄은
//    주지 않고, 다음 레벨에서 다시 획득하면 다시 줍니다(n.5 선택방 레벨은 같은 레벨로 취급). 48번으로 얻으면 제한 없이
//    줍니다. 1회 기록은 57번 전용 키라 다른 아이템의 지급과 섞이지 않습니다.
// 2) 수류탄 자해 무효: 이 아이템을 들고 있는 동안 수류탄 폭발에 휘말려도 피해 처리 자체를 건너뜁니다(PlayerHealth.TakeDamage가
//    호출되지 않으므로 피격 점멸/무적 시간/21번 장신구 판정도 일어나지 않음). 나중에 생길 수류탄 계열 폭발(분열 수류탄 등)도
//    같은 등록소(GrenadeSelfDamageOverride)를 확인하게 만들어서 모두 막습니다.
// 3) 외형(보유 중에만): 수류탄 모델이 흰색이 되고, 폭발 이펙트에 흰색을 덧씌웁니다(검은 그을음 등 무채색 파티클 포함 -
//    tintAchromatic). 23번이 4번 폭발에 색을 덧씌우는 것과 같은 방식이고, 대체 폭발 프리팹 칸을 채우면 폭발 이펙트를 그
//    프리팹으로 통째로 바꿉니다(GrenadeVisualOverride.cs 참고).
// 4) 토끼 팝콘(보유 중에만): 수류탄이 터지는 순간 착탄 중심에서 토끼 스프라이트 3~4마리가 바닥 기준 사방으로 곡사로 튀어
//    나오며, 커졌다가 작아지면서 사라집니다. 순수 연출이라 피해는 없습니다(RabbitPopVfx.cs 참고).
//
// [구현]
// - 자해 무효: GrenadeSelfDamageOverride에 등록만 합니다. Grenade.DamageOwner()가 등록 여부를 확인합니다.
// - 외형: GrenadeVisualOverride에 grenadeVisual 묶음을 등록합니다. PlayerGrenadeThrower가 던질 때/터질 때 읽습니다.
//   등록은 이 묶음을 그대로 가리키므로 Play 중에 인스펙터 값을 바꾸면 다음 수류탄부터 바로 반영됩니다.
// - 토끼: PlayerGrenadeThrower.OnGrenadeExploded(폭발 위치, 반경)를 구독해서 RabbitPopVfx.Spawn을 부릅니다.
// 등록 소유자는 플레이어 오브젝트라 다음 레벨로 넘어가도 유지되고, 새 게임(씬 재시작)에서는 자동으로 빠집니다.
//
// [패시브를 잃을 때 - 48번 등]
// 자해 무효/흰 외형/토끼 연출은 사라지고, 1회성으로 받은 수류탄은 빼앗지 않습니다(형빈 확정 규칙).
//
// [아이템 에셋 설정]
// ItemDefinition: Item Id = 57, Category = Passive, Special Effects에 GrenadeSelfDamageImmunityEffect 하나.
[System.Serializable]
public class GrenadeSelfDamageImmunityEffect : IPassiveItemEffect, IRemovablePassiveEffect
{
    [Tooltip("획득할 때 주는 수류탄 개수입니다. 같은 레벨에서는 한 번만 줍니다(잃어버렸다가 다시 획득해도 다시 주지 않음). 단, 48번으로 얻으면 제한 없이 줍니다.")]
    [Min(0)] public int grenadeAmount = 2;

    [Header("수류탄 외형 (보유 중)")]
    [Tooltip("수류탄 모델 색과 폭발 이펙트 색입니다. 기본: 흰 수류탄 + 흰 폭발(무채색 그을음까지 흰색).")]
    public GrenadeVisualOverride.Style grenadeVisual = new GrenadeVisualOverride.Style();

    [Tooltip("다른 아이템도 수류탄 외형을 바꿀 때, 숫자가 큰 쪽이 이깁니다(같으면 나중에 얻은 아이템이 이김).")]
    public int visualPriority = 0;

    [Header("토끼 팝콘 (수류탄이 터질 때)")]
    public RabbitPopVfx.Settings rabbitPop = new RabbitPopVfx.Settings();

    // 자해 무효 등록에 쓰는 이름입니다.
    public static string BlockSourceId(string itemId) => "grenade_self_damage_block_" + itemId;

    // 수류탄 외형 등록에 쓰는 이름입니다.
    public static string VisualSourceId(string itemId) => "grenade_visual_" + itemId;

    public void Apply(PassiveEffectContext context, string sourceItemId)
    {
        if (context == null || context.PlayerObject == null)
        {
            Debug.LogWarning("GrenadeSelfDamageImmunityEffect: 플레이어를 찾을 수 없어 효과를 적용하지 못했습니다.");
            return;
        }

        GrantGrenadesOncePerLevel(context, sourceItemId);

        GrenadeSelfDamageOverride.Block(BlockSourceId(sourceItemId), context.PlayerObject);

        if (grenadeVisual != null)
        {
            GrenadeVisualOverride.SetStyle(VisualSourceId(sourceItemId), context.PlayerObject, grenadeVisual, visualPriority);
        }

        var thrower = context.PlayerObject.GetComponent<PlayerGrenadeThrower>();
        if (thrower != null)
        {
            thrower.OnGrenadeExploded -= HandleGrenadeExploded; // 혹시 남아 있을 이전 구독 정리(중복 방지)
            thrower.OnGrenadeExploded += HandleGrenadeExploded;
        }
        else
        {
            Debug.LogWarning("GrenadeSelfDamageImmunityEffect: PlayerGrenadeThrower를 찾지 못해 토끼 연출을 붙이지 못했습니다.");
        }
    }

    // [패시브 잃기 - 48번 등] 자해 무효를 해제합니다. 1회성으로 받은 수류탄은 빼앗지 않습니다(형빈 확정).
    public void Remove(PassiveEffectContext context, string sourceItemId)
    {
        GrenadeSelfDamageOverride.Unblock(BlockSourceId(sourceItemId));
        GrenadeVisualOverride.RemoveStyle(VisualSourceId(sourceItemId));

        var thrower = context?.PlayerObject != null ? context.PlayerObject.GetComponent<PlayerGrenadeThrower>() : null;
        if (thrower != null) thrower.OnGrenadeExploded -= HandleGrenadeExploded;
    }

    // 수류탄이 터진 직후(PlayerGrenadeThrower.NotifyExploded) 착탄 중심에서 토끼들을 튀어 나오게 합니다.
    private void HandleGrenadeExploded(Vector3 position, float radius)
    {
        RabbitPopVfx.Spawn(position, rabbitPop);
    }

    private void GrantGrenadesOncePerLevel(PassiveEffectContext context, string sourceItemId)
    {
        if (grenadeAmount <= 0) return;

        if (context.Resources == null)
        {
            Debug.LogWarning("GrenadeSelfDamageImmunityEffect: PlayerResources를 찾을 수 없어 수류탄을 지급하지 못했습니다.");
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
                Debug.LogWarning("GrenadeSelfDamageImmunityEffect: GameFlowManager를 찾지 못해 레벨당 1회 제한 없이 수류탄을 지급합니다.");
            }
        }

        context.Resources.Add(BasicItemType.Grenade, grenadeAmount);
    }
}
