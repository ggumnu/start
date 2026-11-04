using System.Collections.Generic;
using UnityEngine;

// 액티브 87번 - "코스트 6을 소모해 방 안의 랜덤한 적 하나를 아군으로 포섭한다."
//
// [기획 - 형빈 확정]
// - 지금 방(켜져 있는 방)에서 보스를 뺀 살아 있는 적 중 하나를 랜덤으로 포섭합니다. 포섭할 적이 없으면 사용되지 않습니다(코스트도 그대로).
// - 포섭된 적은 아군이 됩니다(자세한 규칙은 AllyFaction.cs).
//   - 피해/체력이 플레이어 기준으로 환산되고 체력이 가득 찹니다(0.5 피해 → 15, 체력 15당 0.5, 나머지 올림).
//   - 방의 클리어 목록에서 빠집니다(마지막 적을 포섭하면 바로 클리어).
//   - 하늘색 테두리 + 초록 체력바로 표시됩니다(머리 위 아이콘은 뺌 - 형빈 확정).
//   - 피해를 받으면 1초 무적(깜빡임). 죽어도 적 처치로 치지 않고 그냥 사라집니다.
// - 아군은 죽지 않는 한 계속 남고, 여러 마리를 포섭할 수 있습니다. 다음 레벨로 넘어가면 모두 사라집니다.
//
// [단계별 구현 - 형빈 확정 계획]
// 1단계: 포섭/수치 환산/표시/클리어 처리. 2단계: 아군·적의 목표와 서로의 피해(CombatTargeting). 3단계: 방 이동(순간이동)과 레벨.
// 4단계: 플레이어 쪽 공격/효과에서 아군 제외.
//
// [아이템 에셋 설정]
// ItemDefinition: Item Id = 87, Category = Active, Active Cost = 6, Active Effect = RecruitEnemyActiveEffect.
// Ally Settings > Outline Material 칸에 AllyOutline 셰이더로 만든 재질을 넣습니다.
[System.Serializable]
public class RecruitEnemyActiveEffect : IActiveItemUseRules
{
    [Tooltip("포섭된 아군의 수치 환산/무적/표시 설정입니다.")]
    public AllyFaction.Settings allySettings = new AllyFaction.Settings();

    [System.NonSerialized] private readonly List<EnemyHealth> candidates = new List<EnemyHealth>();

    // 포섭할 수 있는 적이 하나라도 있어야 사용됩니다(없으면 코스트도 빠지지 않음).
    public bool CanActivateNow(ActiveEffectContext context)
    {
        if (context == null || context.PlayerObject == null) return false;
        if (context.Health != null && context.Health.IsDead) return false;

        AllyFaction.CollectConvertible(candidates);
        bool any = candidates.Count > 0;
        candidates.Clear();
        return any;
    }

    public int ResolveUseCost(ActiveEffectContext context, int effectiveCost) => effectiveCost;

    public float CooldownRemaining01 => 0f;

    public void Activate(ActiveEffectContext context)
    {
        if (context == null || context.PlayerObject == null) return;

        AllyFaction.CollectConvertible(candidates);
        if (candidates.Count == 0) return;

        EnemyHealth target = candidates[Random.Range(0, candidates.Count)];
        candidates.Clear();

        try
        {
            AllyFaction.Convert(target, allySettings);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"RecruitEnemyActiveEffect: 포섭 중 오류가 발생했습니다: {e}");
        }
    }
}
