using System;

// 장신구 20번 - "획득 시 최초 한정으로 크레딧 10을 얻는다. 장착 중에는 보유/획득하는 모든
// 크레딧을 즉시 흡수하며, 흡수한 크레딧 1당 공격력 0.1을 얻는다. 다른 장신구와 교체되거나
// 이 아이템을 잃으면 흡수한 크레딧과 그로 인한 공격력 보너스를 전부 잃는다."
//
// [기획]
// - 장착하는 순간 크레딧을 creditOnFirstEquip(기본 10)만큼 얻습니다 - 39번 장신구와 동일하게
//   같은 레벨 안에서는 딱 한 번만(GameFlowManager.TryConsumeOneTimeGrant 참고). 같은 레벨
//   안에서 이 장신구를 뺐다가 다시 껴도 또 받지 못하고, 다음 레벨로 넘어가면 다시 받을 수
//   있습니다.
// - 장착하는 순간, 그 시점까지 보유 중이던 크레딧(방금 지급받은 10 포함)을 전부 흡수합니다.
//   흡수 = 그만큼 크레딧을 소모시켜 플레이어의 크레딧 보유량을 0으로 만들고, 흡수한 총량에
//   attackPowerPerCredit(기본 0.1)을 곱한 값을 공격력(PlayerStats.Damage)에 더합니다. 예:
//   장착 즉시 10을 흡수하면 그 자리에서 공격력 +1이 됩니다.
// - 장착하고 있는 동안 이후로 얻는 모든 크레딧도 늘어나는 즉시 똑같이 흡수됩니다
//   (PlayerResources.OnAmountChanged를 구독해서, 크레딧이 늘어날 때마다 그 자리에서 바로
//   소모시키고 흡수량에 더한 뒤 공격력을 다시 계산합니다) - 그래서 이 장신구를 장착하고
//   있는 동안 플레이어의 실제 크레딧 보유량은 항상 0으로 유지됩니다.
// - 다른 장신구와 교체되거나(Remove) 이 아이템을 잃으면, 흡수해둔 크레딧은 돌려받지
//   못하고 그냥 사라지며(형빈 확정 - 잃어버림), 그로 인해 얻었던 공격력 보너스도 함께
//   사라집니다.
//
// [구현 - PlayerResources.OnAmountChanged 구독으로 크레딧 즉시 흡수]
// OnAmountChanged는 "그 종류의 변경 후 총 보유량"을 흘려보내는 이벤트입니다(델타가 아닙니다).
// 이 효과가 장착되어 있는 동안에는 크레딧 보유량이 항상 0으로 유지되므로(매번 즉시 흡수),
// 다음에 크레딧이 늘어날 때 이 이벤트로 들어오는 newAmount는 곧 "이번에 새로 늘어난 양"과
// 같습니다. 핸들러 안에서 TrySpend로 그만큼 다시 소모시키면 OnAmountChanged가 (Credit, 0)으로
// 한 번 더 재귀 호출되지만, newAmount<=0 가드로 그 자리에서 끝나 무한 루프에 빠지지 않습니다.
//
// [공격력 재계산 - AddModifier는 누적 등록 방식]
// PlayerStats.AddModifier는 걸 때마다 리스트에 추가되는 방식이라, 흡수량이 바뀔 때마다 매번
// 새로 더하면 이전 값이 남은 채로 중첩됩니다. 그래서 매번 RemoveModifiersFromSource로 이
// 아이템이 걸어둔 이전 모디파이어를 먼저 지운 뒤, 갱신된 흡수량 기준으로 다시 겁니다.
[Serializable]
public class CreditAbsorptionTrinketEffect : ITrinketEffect
{
    public int creditOnFirstEquip = 10;
    public float attackPowerPerCredit = 0.1f;

    // 지금까지 흡수한 크레딧의 총량입니다. 장착될 때마다 0에서 다시 시작합니다 - 장신구를
    // 잃으면 흡수량도 함께 사라지는 기획이라, 재장착해도 이전 흡수량이 이어지지 않습니다.
    private int absorbedCredits;

    private PlayerResources resources;
    private PlayerStats stats;
    private string effectSourceId;
    private Action<BasicItemType, int> amountChangedHandler;

    public void Apply(TrinketEffectContext context, string sourceItemId)
    {
        if (context == null) return;

        resources = context.Resources;
        stats = context.Stats;
        effectSourceId = sourceItemId;
        absorbedCredits = 0;

        bool grantedNow = context.GameFlow != null &&
                           context.GameFlow.TryConsumeOneTimeGrant($"{sourceItemId}_credit");
        if (grantedNow && resources != null)
        {
            resources.Add(BasicItemType.Credit, creditOnFirstEquip);
        }

        // 장착 시점까지 쌓여있던 크레딧(방금 지급받은 첫 지급분 포함)을 전부 흡수합니다.
        AbsorbCurrentCredits();

        if (resources != null)
        {
            amountChangedHandler = (type, newAmount) =>
            {
                if (type != BasicItemType.Credit || newAmount <= 0) return;
                AbsorbCurrentCredits();
            };
            resources.OnAmountChanged += amountChangedHandler;
        }
    }

    public void Remove(TrinketEffectContext context, string sourceItemId)
    {
        if (resources != null && amountChangedHandler != null)
        {
            resources.OnAmountChanged -= amountChangedHandler;
        }
        amountChangedHandler = null;

        // 흡수해둔 크레딧은 돌려주지 않고(잃어버림), 그로 인한 공격력 보너스만 제거합니다.
        context?.Stats?.RemoveModifiersFromSource(sourceItemId);

        absorbedCredits = 0;
        resources = null;
        stats = null;
        effectSourceId = null;
    }

    // 지금 보유 중인 크레딧을 전부 소모시켜 흡수량에 더하고, 공격력 보너스를 다시 계산합니다.
    private void AbsorbCurrentCredits()
    {
        if (resources == null) return;

        int current = resources.GetAmount(BasicItemType.Credit);
        if (current <= 0) return;

        if (!resources.TrySpend(BasicItemType.Credit, current)) return;

        absorbedCredits += current;
        UpdateAttackModifier();
    }

    private void UpdateAttackModifier()
    {
        if (stats == null || string.IsNullOrEmpty(effectSourceId)) return;

        stats.RemoveModifiersFromSource(effectSourceId);
        stats.AddModifier(StatType.Damage, ModifierType.Additive, absorbedCredits * attackPowerPerCredit, effectSourceId);
    }
}
