using UnityEngine;

// 패시브 아이템 7번 - "획득하면 적의 탐지 거리(Detect Range)가 3으로 제한된다."
//
// [효과]
// 적은 플레이어가 자기 탐지 거리 안에 들어와야 비로소 깨어나서 움직이고 공격합니다.
// 이 아이템을 가지고 있으면 탐지 거리가 detectRangeCap(기본 3) 이하로 제한되어, 그보다
// 훨씬 가까이 다가가기 전까지 적들이 깨어나지 않습니다.
//
// [알아둘 동작 - 코드 확인 결과]
// - 탐지 거리는 "처음 깨어나는 순간"에만 쓰입니다. 한번 깨어난 적은 다시 잠들지 않으므로,
//   깨어난 뒤에 탐지 거리 밖으로 멀어져도 멍하니 서 있지 않고 계속 싸웁니다.
// - 원거리 적의 Engage/Chase/Retreat Range는 깨어난 뒤에만 쓰이므로 이 아이템과 충돌하지 않습니다.
// - 잠든 적은 총알에 맞아도 깨어나지 않습니다(형빈 확정 - 이번에는 그대로 둠).
// - 보스는 탐지 거리를 쓰지 않아서 영향이 없습니다.
// - 상한 방식입니다 - 원래 탐지 거리가 3 이하인 적은 그대로 둡니다(늘어나지 않음).
//
// [이 파일의 역할]
// 인스펙터 설정값(detectRangeCap)만 들고 있고, 아이템을 획득하면 Apply()가 한 번 호출되어
// EnemyDetectRangeOverride에 상한을 등록합니다. 실제 적용은 근접/원거리 AI가 깨어날지
// 판단할 때 EnemyDetectRangeOverride.Resolve()를 거쳐서 이루어집니다. 이미 만들어진 적
// (다른 방, 다음 층, 방 재입장으로 새로 생기는 적 포함)에도 스폰 시점과 무관하게 항상
// 적용됩니다. 값은 매번 읽으므로 Play 중에 인스펙터 값을 바꾸면 곧바로 반영됩니다.
//
// [에디터 설정 방법]
// ItemDefinition 에셋 - Special Effects 리스트에 "+"를 눌러 DetectRangeCapEffect를 고르면
// Detect Range Cap 칸이 나타납니다.
[System.Serializable]
public class DetectRangeCapEffect : IPassiveItemEffect, IRemovablePassiveEffect
{
    [Tooltip("적의 탐지 거리가 이 값 이하로 제한됩니다. 원래 탐지 거리가 이 값보다 작은 " +
             "적은 그대로 둡니다.")]
    public float detectRangeCap = 3f;

    public void Apply(PassiveEffectContext context, string sourceItemId)
    {
        if (context == null || context.PlayerObject == null) return;

        EnemyDetectRangeOverride.SetCap("detect_cap_" + sourceItemId, context.PlayerObject, () => detectRangeCap);
    }

    // [패시브 잃기 - 48번 등] 소지 중에만 유지되던 효과를 되돌립니다(IRemovablePassiveEffect.cs 참고).
    public void Remove(PassiveEffectContext context, string sourceItemId)
    {
        EnemyDetectRangeOverride.RemoveCap("detect_cap_" + sourceItemId);
    }
}
