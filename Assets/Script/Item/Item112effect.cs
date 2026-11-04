using UnityEngine;

// 액티브 112번 - "코스트 4를 소모해 즉시 사용. 5초 동안 투명 상태가 된다."
//
// [기획 - 형빈 확정]
// - 사용 즉시 5초 동안 투명 상태가 됩니다(자세한 규칙은 PlayerInvisibility.cs).
//   - 적(보스 포함)이 플레이어를 인식하지 못합니다. 적이 피해를 입어도 인식하지 못합니다.
//   - 사용하는 순간 진행 중이던 적의 공격 준비(돌진 충전/전방위 충전/쫓아가며 도는 공격)는 취소됩니다. 이미 진행 중인 돌진은 그 돌진까지만
//     (연속 돌진이면 지금 돌진을 끝내고 다음 돌진 취소), 이미 시작한 연사는 끝까지 쏩니다.
//   - 적과 닿아도 접촉 피해를 받지 않습니다. 이미 날아오는 탄환/연사 중 남은 탄환/수류탄 자폭 피해는 그대로 받습니다.
//   - 체력 소모 특수방을 피해 없이 드나들 수 있습니다.
//   - 플레이어가 점 무늬(디더)로 반투명하게 보입니다.
// - 5초가 끝나면 적이 다시 평소 규칙대로 플레이어를 인식할 수 있습니다.
// - 투명한 동안에는 다시 사용할 수 없습니다(코스트도 빠지지 않음). 좌상단 액티브 아이콘에 남은 투명 시간이 쿨타임처럼 표시됩니다.
//
// [아이템 에셋 설정]
// ItemDefinition: Item Id = 112, Category = Active, Active Cost = 4, Active Effect = InvisibilityActiveEffect.
[System.Serializable]
public class InvisibilityActiveEffect : IActiveItemUseRules
{
    [Tooltip("투명 상태가 유지되는 시간(초)입니다.")]
    [Min(0.1f)] public float duration = 5f;

    [Tooltip("투명한 동안 보이는 정도입니다(0~1). 0.4 = 캐릭터 픽셀의 40%만 그려져 반투명하게 보임. 0이면 완전히 안 보임.")]
    [Range(0f, 1f)] public float visibleFraction = 0.4f;

    public bool CanActivateNow(ActiveEffectContext context)
    {
        if (context == null || context.PlayerObject == null) return false;
        if (context.Health != null && context.Health.IsDead) return false;
        if (PlayerInvisibility.IsActive) return false; // 투명한 동안에는 다시 쓸 수 없음(형빈 확정)
        return true;
    }

    public int ResolveUseCost(ActiveEffectContext context, int effectiveCost) => effectiveCost;

    // 투명한 동안 남은 시간을 쿨타임처럼 보여줍니다(그 동안 사용할 수 없음을 알리기 위함).
    public float CooldownRemaining01 => PlayerInvisibility.Remaining01;

    public void Activate(ActiveEffectContext context)
    {
        if (context == null || context.PlayerObject == null) return;
        PlayerInvisibility.Begin(context.PlayerObject, duration, visibleFraction);
    }
}
