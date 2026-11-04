using UnityEngine;

// 패시브 92번 - "획득 시 수류탄 2개를 얻는다. 보유하고 있으면 수류탄이 폭발할 때 던진 방향 기준 X자 4방향으로 분열 수류탄이
// 나타나 한 번 더 폭발한다."
//
// [기획 - 형빈 확정]
// - 방향 " X- ": -는 플레이어가 수류탄을 던진 방향(손을 떠난 위치 → 착지점, 수평), X는 그 방향 기준 앞쪽 ±45도/뒤쪽 ±135도의
//   4방향입니다(서로 90도 간격). 발밑에 던져 방향이 없으면 플레이어가 바라보는 방향을 씁니다. 77번으로 유도된 뒤 터져도 방향은
//   처음 던진 방향 기준이고, 실제로 터진 위치에서 분열합니다.
// - 분열탄: 원래 수류탄이 터진 직후(81번 불 구역 생성까지 끝난 뒤) 폭발 지점에서 4개가 튀어나와 짧은 곡사로 날아가고, 착지하면
//   터집니다. 분열 후 착탄(=폭발 또는 77번 유도 시작)까지 걸리는 시간은 원래 수류탄과 같습니다(PlayerGrenadeThrower의 Flight Time).
//   날아가는 거리 = 원래 폭발 반경(distanceRatio 1) → 원래 폭발 원의 가장자리에 떨어집니다. 방 밖으로 나갈 위치는 일반
//   수류탄처럼 방 안쪽 가장자리로 당깁니다. 각 착지점에 착탄 원을 표시합니다.
// - 분열탄 위력: 피해 50%, 폭발 반경 50%(던진 순간의 원래 수류탄 값 기준). 자해 피해도 50%(0.5 단위로 올림). 모델 크기 0.6배.
// - 분열탄은 다시 분열하지 않습니다(무한 폭발 방지).
// - 다른 수류탄 아이템 효과를 모두 받습니다.
//   - 57번: 자해 무효 / 흰 수류탄·흰 폭발 / 토끼 팝콘(분열탄 폭발마다).
//   - 66번: 분열탄 폭발마다 그 반경 안 적 해킹 + 적 총알 제거.
//   - 77번: 분열탄도 착지 후 1초 동안 가장 가까운 적에게 유도된 뒤 터집니다(착지 순간 77번 보유 기준 - 일반 수류탄과 같은 규칙).
//   - 81번: 원래 수류탄이 소이탄이면 분열탄도 소이탄입니다(빨간 발광 + 폭발 피해 비율 + 분열탄 반경의 불 구역). 원래 수류탄을
//     "던진 순간"에 정해진 값을 그대로 물려받습니다.
//   - 특수방 잠금 바리케이드: 분열탄 폭발로도 해제됩니다(OnGrenadeExploded 공통).
// - 분열 여부는 원래 수류탄을 "던지는 순간" 정합니다(81번과 같은 시점). 날아가는 도중 92번을 잃어도 그 수류탄은 분열합니다.
// - [획득 시 지급] 얻을 때 수류탄 grenadeAmount(기본 2)개를 줍니다. 77번과 같은 1회성 지급 규칙입니다
//   (GameFlowManager.TryConsumeOneTimeGrant - 보물상자/받침대/상점에서 새로 얻으면 지급, 교체/버리기로 월드에 나갔던 사본을
//   다시 주우면 지급 없음, 48번으로 얻으면 제한 없이 지급). 잃어도(48번 등) 받은 수류탄은 빼앗지 않습니다.
//
// [구현]
// GrenadeSplitOverride 등록소에 설정 묶음을 등록/해제만 합니다. 실제 분열은 Grenade.cs(터질 때 분열탄 생성 요청)와
// PlayerGrenadeThrower.SpawnSplitGrenade(분열탄 오브젝트 생성 - 수류탄 모델/57번 외형 공통)가 담당합니다.
// 수류탄 지급은 PlayerResources.Add(BasicItemType.Grenade, ..)로 합니다(기록 이름 "92_grenade_grant").
//
// [아이템 에셋 설정]
// ItemDefinition: Item Id = 92, Category = Passive, Special Effects에 GrenadeSplitEffect 하나. Stat Modifiers는 비워둡니다.
[System.Serializable]
public class GrenadeSplitEffect : IPassiveItemEffect, IRemovablePassiveEffect
{
    [Tooltip("획득할 때 주는 수류탄 개수입니다(기본 2). 교체/버리기로 월드에 나갔던 사본을 다시 주우면 주지 않습니다. 48번으로 얻으면 제한 없이 줍니다.")]
    [Min(0)] public int grenadeAmount = 2;

    [Tooltip("분열 수류탄 설정입니다(위력 비율/방향/거리/포물선 높이/크기).")]
    public GrenadeSplitOverride.Settings settings = new GrenadeSplitOverride.Settings();

    public void Apply(PassiveEffectContext context, string sourceItemId)
    {
        if (context == null || context.PlayerObject == null)
        {
            Debug.LogWarning("GrenadeSplitEffect: 플레이어를 찾을 수 없어 92번 효과를 적용하지 못했습니다.");
            return;
        }

        GrantGrenades(context, sourceItemId);

        if (settings == null) settings = new GrenadeSplitOverride.Settings();
        GrenadeSplitOverride.Register(SourceKey(sourceItemId), context.PlayerObject, settings);
    }

    // [패시브 잃기 - 48번 등] 그 뒤로 던지는 수류탄부터 분열하지 않습니다. 받은 수류탄은 빼앗지 않습니다.
    public void Remove(PassiveEffectContext context, string sourceItemId)
    {
        GrenadeSplitOverride.Unregister(SourceKey(sourceItemId));
    }

    private static string SourceKey(string sourceItemId) => "grenade_split_" + sourceItemId;

    // 획득 시 수류탄 지급입니다. 77번 GrantGrenades와 같은 규칙입니다(위 [획득 시 지급] 참고).
    private void GrantGrenades(PassiveEffectContext context, string sourceItemId)
    {
        if (grenadeAmount <= 0) return;

        if (context.Resources == null)
        {
            Debug.LogWarning("GrenadeSplitEffect: PlayerResources를 찾을 수 없어 수류탄을 지급하지 못했습니다.");
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
                Debug.LogWarning("GrenadeSplitEffect: GameFlowManager를 찾지 못해 1회성 지급 제한 없이 수류탄을 지급합니다.");
            }
        }

        context.Resources.Add(BasicItemType.Grenade, grenadeAmount);
    }
}
