using UnityEngine;

// 패시브 아이템 18번 - "소지하고 있으면 일반맵에서 보물상자가 등장할 확률이 50% 증가한다."
//
// [효과]
// 일반방 클리어 시 보물상자가 등장할 확률은 그 방에서 실제로 스폰된 적 한 마리당
// chancePerEnemy(기본 10%)를 합연산으로 더해서 정해집니다(NormalRoomTreasureSpawner.
// TrySpawnTreasure() 참고). 이 아이템은 그렇게 계산된 최종 확률에 (1 + chanceBonusRatio)를
// 곱합니다 - 예를 들어 적 4마리로 원래 40%였다면 60%로, 5마리로 원래 50%였다면 75%로
// 늘어납니다.
//
// [계산 방식 - 최종 결과값에 배수를 곱함]
// "적 한 마리당 확률"이라는 중간 상수(chancePerEnemy) 자체를 바꾸는 대신, 계산이 끝난
// 최종 확률에 배수를 곱하는 방식을 씁니다. 두 방식은 결과가 수학적으로 완전히 동일하지만
// (n x (0.1 x 1.5) = (n x 0.1) x 1.5), 최종 계산값에 개입하는 이 방식이 EnemyDetectRangeOverride/
// EnemySpeedModifier 등 이 프로젝트의 기존 등록소 관례와 일치하고, chancePerEnemy를 쓰는
// 계산식 자체가 나중에 바뀌어도 영향을 받지 않습니다.
//
// [적용 범위]
// 확률 굴림이 있는 곳은 일반방(NormalRoomTreasureSpawner.TrySpawnTreasure)뿐입니다.
// 동아리방 좌대(ItemPedestal)와 보스방 확정 보상 상자(SpawnBossRewardChest)는 애초에
// 확률 없이 항상 스폰되므로 이 아이템과 무관합니다.
//
// [여러 개가 동시에 있을 때]
// TreasureChanceBonusOverride는 곱연산 방식입니다 - 나중에 같은 종류의 다른 보너스 아이템이
// 추가돼서 둘 다 보유하게 되면 1.5 x (1+다른 보너스율)로 곱해집니다(합연산이 아닙니다).
//
// [이 파일의 역할]
// 인스펙터 설정값(chanceBonusRatio)만 들고 있고, 아이템을 획득하면 Apply()가 한 번 호출되어
// TreasureChanceBonusOverride에 보너스를 등록합니다. 실제 적용은 NormalRoomTreasureSpawner.
// TrySpawnTreasure()가 확률을 계산한 직후 TreasureChanceBonusOverride.CurrentMultiplier를
// 곱하는 시점에 이루어집니다. 값은 매번 읽으므로 Play 중에 인스펙터 값을 바꾸면 곧바로
// 반영됩니다.
//
// [에디터 설정 방법]
// ItemDefinition 에셋 - Special Effects 리스트에 "+"를 눌러 TreasureChanceBoostEffect를
// 고르면 Chance Bonus Ratio 칸이 나타납니다.
[System.Serializable]
public class TreasureChanceBoostEffect : IPassiveItemEffect, IRemovablePassiveEffect
{
    [Tooltip("일반맵 보물상자 등장 확률에 곱연산으로 적용될 보너스율입니다 (0.5 = 50% 증가). " +
             "같은 종류의 보너스가 여러 개 등록되면 (1+보너스율)들을 전부 곱해서 합쳐집니다.")]
    public float chanceBonusRatio = 0.5f;

    public void Apply(PassiveEffectContext context, string sourceItemId)
    {
        if (context == null || context.PlayerObject == null) return;

        TreasureChanceBonusOverride.SetBonus("treasure_chance_" + sourceItemId, context.PlayerObject, () => chanceBonusRatio);
    }

    // [패시브 잃기 - 48번 등] 소지 중에만 유지되던 효과를 되돌립니다(IRemovablePassiveEffect.cs 참고).
    public void Remove(PassiveEffectContext context, string sourceItemId)
    {
        TreasureChanceBonusOverride.RemoveBonus("treasure_chance_" + sourceItemId);
    }
}
