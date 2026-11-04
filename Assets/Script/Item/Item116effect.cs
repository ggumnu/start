using System;
using UnityEngine;

// 장신구 116번 - "획득 시 최초 한정으로 청휘석 1개. 가지고 있으면 일반방 보물상자에서 반드시 플레이어 아이템이 나온다."
//
// [기획 - 형빈 확정]
// - 획득(장착)하면 청휘석 pyroxenesOnFirstEquip(기본 1)개. 기존 1회 지급 규칙(GameFlowManager.TryConsumeOneTimeGrant - 21/39번과 같음)을
//   따릅니다: 교체로 버렸다 다시 줍거나, 다음 레벨에서 Ctrl 길게 눌러 내려놓았다 다시 주워도 다시 받지 않습니다. 정상적인 방법(상점 구매,
//   보물상자에서 새로 얻음 등 - 새 사본)으로 얻을 때만 받습니다.
// - 끼고 있는 동안 일반방 보물상자(9번/106번으로 복제한 일반방 상자 포함)를 열면 "재화냐 플레이어 아이템이냐" 확률 없이 항상 플레이어 아이템이
//   나옵니다. 더 나올 수 있는 플레이어 아이템이 없으면 원래처럼 재화가 나옵니다(연출 없음).
// - 판정 기준은 상자를 "여는 순간" 116번을 가지고 있느냐입니다(클리어할 때 끼고 있었는지는 상관없음). 이미 열려 아이템이 나온 상자는 116번을
//   버려도 그대로 남습니다. 116번을 잃으면 그 뒤에 여는 일반방 상자는 원래 확률로 돌아갑니다.
// - 상자가 나타날 확률은 바꾸지 않습니다. 보스방/동아리방/특수방 상자에는 영향이 없습니다.
//
// [구현] NormalChestItemGuaranteeOverride에 등록/해제만 합니다(실제 판정은 NormalRoomTreasureSpawner.ResolveContent).
//
// [아이템 에셋 설정]
// ItemDefinition: Item Id = 116, Category = Trinket, Trinket Effect = NormalChestItemGuaranteeTrinketEffect.
[Serializable]
public class NormalChestItemGuaranteeTrinketEffect : ITrinketEffect
{
    [Tooltip("획득 시 최초 한정으로 지급하는 청휘석 개수입니다(기본 1).")]
    [Min(0)] public int pyroxenesOnFirstEquip = 1;

    private static string GuaranteeKey(string sourceItemId) => "normal_chest_item_guarantee_" + sourceItemId;

    public void Apply(TrinketEffectContext context, string sourceItemId)
    {
        if (context == null) return;

        // [1회 지급] "{아이템ID}_용도" 형식이어야 새 사본을 얻을 때 기록이 풀립니다(GameFlowManager 참고).
        if (pyroxenesOnFirstEquip > 0)
        {
            bool grantNow;
            if (context.GameFlow != null)
            {
                grantNow = context.GameFlow.TryConsumeOneTimeGrant($"{sourceItemId}_pyroxenes");
            }
            else
            {
                Debug.LogWarning("NormalChestItemGuaranteeTrinketEffect: GameFlowManager를 찾지 못해 1회 제한 없이 청휘석을 지급합니다(테스트 씬).");
                grantNow = true;
            }

            if (grantNow)
            {
                if (context.Resources != null) context.Resources.Add(BasicItemType.Pyroxenes, pyroxenesOnFirstEquip);
                else Debug.LogWarning("NormalChestItemGuaranteeTrinketEffect: PlayerResources를 찾지 못해 청휘석을 지급하지 못했습니다.");
            }
        }

        NormalChestItemGuaranteeOverride.Register(GuaranteeKey(sourceItemId), context.PlayerObject);
    }

    // 버리기/교체/잃기: 보장만 끕니다. 받은 청휘석과 이미 나온 아이템은 그대로입니다.
    public void Remove(TrinketEffectContext context, string sourceItemId)
    {
        NormalChestItemGuaranteeOverride.Unregister(GuaranteeKey(sourceItemId));
    }
}
