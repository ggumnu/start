using UnityEngine;

// 패시브 아이템 8번 - "벽 뒤로 숨으면 적이 더 이상 추적하지 않는다."
//
// [효과]
// 이 아이템을 가지고 있으면 적은 자기 눈 높이에서 플레이어까지 벽 레이어로 막혀 있는지를
// 계속 확인합니다.
// - 이미 추적 중인 적이 플레이어를 놓쳐서(시야가 lossGraceTime초 넘게 계속 막힘) Idle(잠든
//   상태)로 돌아가고 완전히 멈춥니다 - 이동, 회전, 사격을 전부 하지 않습니다.
// - Idle 적은 "탐지 거리 안"이면서 "시야가 열려 있을" 때만 다시 깨어납니다. 벽 뒤에서는
//   탐지 거리 안이어도 깨어나지 않습니다. 시야가 다시 열렸어도 탐지 거리 밖이면 계속 Idle
//   입니다(7번 아이템도 있으면 탐지 거리는 3으로 제한됩니다).
// - 시야를 계속 유지하고 있는 적은 지금처럼 끝까지 쫓아옵니다.
// - 이미 시작된 돌진/전방위 공격은 끝까지 진행됩니다(공격 컴포넌트가 전권을 가지고 있어서).
// - 보스(BossAIController)는 이 규칙을 쓰지 않아 영향이 없습니다(형빈 확정).
//
// [이 파일의 역할]
// 인스펙터 설정값만 들고 있고, 아이템을 획득하면 Apply()가 한 번 호출되어 EnemySightOverride
// 에 시야 규칙을 등록합니다. 실제 판정은 근접/원거리 AI가 EnemySightOverride를 거쳐서 합니다.
// 값은 매번 읽으므로 Play 중에 인스펙터 값을 바꾸면 곧바로 반영됩니다.
//
// [에디터 설정 방법]
// ItemDefinition 에셋 - Special Effects 리스트에 "+"를 눌러 HideBehindWallEffect를 고르면
// 아래 필드들이 나타납니다. Wall Layer Mask는 반드시 지정해야 합니다(비워두면 어떤 것도
// 시야를 가리지 못해 효과가 없습니다). 기둥/바위처럼 시야를 가려야 하는 장애물이 벽과 다른
// 레이어에 있다면 그 레이어도 함께 체크하세요. 적/플레이어 레이어는 체크하지 마세요.
[System.Serializable]
public class HideBehindWallEffect : IPassiveItemEffect, IRemovablePassiveEffect
{
    [Tooltip("이 레이어의 콜라이더가 적과 플레이어 사이에 있으면 시야가 막힌 것으로 봅니다. " +
             "벽 레이어(필요하면 시야를 가리는 장애물 레이어도)만 체크하세요.")]
    public LayerMask wallLayerMask;
    [Tooltip("시야 판정 점을 바닥에서 얼마나 올릴지입니다(월드 단위). 벽/장애물 높이보다 " +
             "낮게 잡으면 낮은 장애물도 시야를 가리고, 높게 잡으면 낮은 장애물은 무시합니다. " +
             "적과 플레이어 양쪽에 같은 높이가 적용됩니다.")]
    public float eyeHeight = 1f;
    [Tooltip("시야가 막힌 뒤 이 시간(초) 동안 계속 막혀 있어야 적이 플레이어를 놓쳤다고 " +
             "판정합니다. 벽 모서리에서 시야가 깜빡일 때 적이 덜덜 떠는 것을 막아줍니다. " +
             "0으로 두면 막히는 즉시 놓칩니다.")]
    public float lossGraceTime = 0.3f;

    public void Apply(PassiveEffectContext context, string sourceItemId)
    {
        if (context == null || context.PlayerObject == null) return;

        EnemySightOverride.SetRule("sight_" + sourceItemId, context.PlayerObject, () => new EnemySightRule
        {
            wallMask = wallLayerMask,
            eyeHeight = eyeHeight,
            lossGraceTime = lossGraceTime
        });
    }

    // [패시브 잃기 - 48번 등] 소지 중에만 유지되던 효과를 되돌립니다(IRemovablePassiveEffect.cs 참고).
    public void Remove(PassiveEffectContext context, string sourceItemId)
    {
        EnemySightOverride.RemoveRule("sight_" + sourceItemId);
    }
}
