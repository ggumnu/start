using UnityEngine;

// 패시브 77번 - "수류탄이 땅에 닿기 직전 폭발이 보류되고, 1초 동안 바닥에 붙어 가까운 적에게 유도된 뒤 터진다."
//
// [기획 - 형빈 확정]
// - 수류탄이 착지하는 순간 바로 터지지 않고, homingDuration(기본 1초) 동안 바닥에 붙어 움직이다가 그 자리에서 터집니다.
//   피해는 기본 수류탄과 같습니다(던진 순간의 수류탄 피해/자해 피해/폭발 반경 그대로).
// - 유도 대상: 매 순간 가장 가까운 살아 있는 적(보스 포함). 쫓던 적이 죽거나 더 가까운 적이 생기면 바로 갈아탑니다.
//   적이 없으면 그 자리에 머물다가, 도중에 적이 나타나면 그쪽으로 유도됩니다. 적에게 닿아도 바로 터지지 않고 1초가 될
//   때까지 따라붙습니다(플레이어가 대응할 시간을 주기 위함).
// - 유도 속도 = 플레이어의 "현재" 이동속도 x speedMultiplier(기본 1). 유도 중 이동속도가 바뀌면 바로 따라갑니다.
// - 벽("Wall" 태그)에는 막히고, 벽을 따라 미끄러지며 계속 쫓습니다.
// - 착탄 지점 원은 유도 중에도 계속 표시되고 수류탄을 따라다닙니다.
// - 기본 수류탄과 같이 폭발 반경 안의 플레이어는 자해 피해를 입습니다(57번 보유 시 피해 없음). 폭발 이펙트/57번 토끼/
//   특수방 잠금 바리케이드 해제도 모두 실제로 터진 위치 기준으로 동작합니다.
// - 날아가는 도중 77번을 잃으면 그 수류탄은 평소처럼 착지 즉시 터집니다(착지 순간에 보유 여부를 확인).
// - [획득 시 지급] 얻을 때 수류탄 grenadeAmount(기본 2)개를 줍니다. 57번과 같은 1회성 지급 규칙입니다
//   (GameFlowManager.TryConsumeOneTimeGrant - 보물상자/받침대/상점에서 새로 얻으면 지급, 교체/버리기로 월드에 나갔던 사본을
//   다시 주우면 지급 없음, 48번으로 얻으면 제한 없이 지급). 잃어도(48번 등) 받은 수류탄은 빼앗지 않습니다.
//
// [구현]
// GrenadeHomingOverride 등록소에 설정을 등록/해제만 합니다. 실제 유도는 Grenade.cs의 유도 단계가 담당합니다.
// 수류탄 지급은 PlayerResources.Add(BasicItemType.Grenade, ..)로 합니다(기록 이름 "77_grenade_grant").
//
// [아이템 에셋 설정]
// ItemDefinition: Item Id = 77, Category = Passive, Special Effects에 GrenadeHomingEffect 하나. Stat Modifiers는 비워둡니다.
[System.Serializable]
public class GrenadeHomingEffect : IPassiveItemEffect, IRemovablePassiveEffect
{
    [Tooltip("획득할 때 주는 수류탄 개수입니다(기본 2). 교체/버리기로 월드에 나갔던 사본을 다시 주우면 주지 않습니다. 48번으로 얻으면 제한 없이 줍니다.")]
    [Min(0)] public int grenadeAmount = 2;

    [Tooltip("착지 후 터지기까지 유도하는 시간(초)입니다(기본 1).")]
    [Min(0f)] public float homingDuration = 1f;

    [Tooltip("유도 속도 = 플레이어 현재 이동속도 x 이 값입니다(기본 1 = 플레이어와 같은 속도).")]
    [Min(0f)] public float speedMultiplier = 1f;

    [Tooltip("유도 중 바닥에서 수류탄 중심을 띄우는 높이(m)입니다. 수류탄이 바닥에 묻혀 보이면 올리세요.")]
    public float groundOffset = 0.05f;

    [Tooltip("벽에 막히는 판정과 굴러가는 회전에 쓰는 수류탄 반지름(m)입니다. 수류탄 모델 크기에 맞추세요.")]
    [Min(0.001f)] public float collisionRadius = 0.1f;

    public void Apply(PassiveEffectContext context, string sourceItemId)
    {
        if (context == null || context.PlayerObject == null)
        {
            Debug.LogWarning("GrenadeHomingEffect: 플레이어를 찾을 수 없어 77번 효과를 적용하지 못했습니다.");
            return;
        }

        GrantGrenades(context, sourceItemId);

        GrenadeHomingOverride.Register(SourceKey(sourceItemId), context.PlayerObject, new GrenadeHomingOverride.Settings
        {
            homingDuration = homingDuration,
            speedMultiplier = speedMultiplier,
            groundOffset = groundOffset,
            collisionRadius = collisionRadius,
        });
    }

    // [패시브 잃기 - 48번 등] 그 뒤로 착지하는 수류탄부터 유도하지 않습니다. 받은 수류탄은 빼앗지 않습니다.
    public void Remove(PassiveEffectContext context, string sourceItemId)
    {
        GrenadeHomingOverride.Unregister(SourceKey(sourceItemId));
    }

    private static string SourceKey(string sourceItemId) => "grenade_homing_" + sourceItemId;

    // 획득 시 수류탄 지급입니다. 57번 GrantGrenadesOncePerLevel과 같은 규칙입니다(위 [획득 시 지급] 참고).
    private void GrantGrenades(PassiveEffectContext context, string sourceItemId)
    {
        if (grenadeAmount <= 0) return;

        if (context.Resources == null)
        {
            Debug.LogWarning("GrenadeHomingEffect: PlayerResources를 찾을 수 없어 수류탄을 지급하지 못했습니다.");
            return;
        }

        // 1회성 지급 기록(GameFlowManager)으로 교체를 통한 무한 복제를 막습니다. 48번으로 얻을 때는
        // (PassiveEffectContext.IgnoreOneTimeGrantLimit) 기록을 거치지 않고 지급합니다. GameFlowManager가 없는 씬(아이템 테스트
        // 씬)에서는 경고만 남기고 지급합니다.
        if (!context.IgnoreOneTimeGrantLimit)
        {
            var gameFlow = Object.FindFirstObjectByType<GameFlowManager>();
            if (gameFlow != null)
            {
                if (!gameFlow.TryConsumeOneTimeGrant(sourceItemId + "_grenade_grant")) return;
            }
            else
            {
                Debug.LogWarning("GrenadeHomingEffect: GameFlowManager를 찾지 못해 1회성 지급 제한 없이 수류탄을 지급합니다.");
            }
        }

        context.Resources.Add(BasicItemType.Grenade, grenadeAmount);
    }
}
