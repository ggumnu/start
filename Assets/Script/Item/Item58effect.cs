using UnityEngine;

// 패시브 58번 - "획득 시 해킹툴 2개를 얻는다. 보유하고 있으면 해킹툴을 소모할 때마다 보호막을 1개 얻는다."
//
// [기획 - 형빈 확정]
// - 해킹툴 지급: 획득할 때 hackingToolAmount(기본 2)개를 줍니다. 115번/92번과 같은 1회성 지급 규칙입니다
//   (GameFlowManager.TryConsumeOneTimeGrant - 보물상자/받침대/상점에서 새로 얻으면 지급, 교체로 버렸던 사본을 다시 주우면 지급 없음,
//   48번으로 얻으면 제한 없이 지급). 기록 이름은 "58_hack_grant"입니다.
// - 보호막: 보유 중 해킹툴을 써서 1개가 실제로 소모되는 순간(클릭 순간 - 효과 발동 전, 급할 때 바로 얻도록) 보호막을
//   shieldPerUse(기본 1)개 얻습니다. 여러 번 쓰면 쌓이고, 최대 5개(PlayerHealth.maxShield)입니다. 5개가 꽉 차 있으면 아무 일도
//   일어나지 않습니다(초과분은 기억하지 않음 - 체력이 가득일 때 회복 아이템을 먹는 것과 같음). 해킹 효과 자체는 평소대로 나갑니다.
// - 보호막은 최대 체력과 별개로, 맞기 전까지 레벨 이동/방 클리어와 상관없이 계속 유지됩니다(아이작 소울하트 개념 - PlayerHealth).
// - 보호막 자체는 클릭 순간 바로 얻지만, 보호막 연출(깎은 정이십면체, 0.5초 - PlayerShieldBreakVfx.acquireDuration)은 해킹 효과가
//   발동하는 순간(사용 동작 중간, PlayerHackTool.OnHackDeployed)에 나타납니다(형빈 확정). 그 사용으로 보호막이 실제로 늘었을 때만
//   나옵니다(5개가 꽉 차 있었으면 연출도 없음).
// - 해킹툴을 소모하지 않는 해킹(66번/81번 수류탄 해킹, 26번 해킹툴 특수방 무료 진입)으로는 보호막을 얻지 않습니다.
//
// [패시브를 잃을 때 - 48번 등]
// 보호막 얻기만 멈춥니다. 이미 얻은 보호막과 1회성으로 받은 해킹툴은 빼앗지 않습니다(형빈 확정).
//
// [구현]
// PlayerHackTool.OnHackToolSpent(해킹툴이 소모된 순간)에 PlayerHealth.AddShield(연출 없이)를 부르고, 보호막이 늘었으면 표시해 뒀다가
// PlayerHackTool.OnHackDeployed(해킹 효과 발동 순간)에 PlayerShieldBreakVfx.PlayAcquire()로 연출을 띄웁니다.
//
// [아이템 에셋 설정]
// ItemDefinition: Item Id = 58, Category = Passive, Special Effects에 HackShieldEffect 하나. Stat Modifiers는 비워둡니다.
[System.Serializable]
public class HackShieldEffect : IPassiveItemEffect, IRemovablePassiveEffect
{
    [Tooltip("얻을 때 주는 해킹툴 개수입니다(1회성 지급 - 115번과 같은 규칙).")]
    [Min(0)] public int hackingToolAmount = 2;

    [Tooltip("해킹툴을 1개 쓸 때마다 얻는 보호막 개수입니다(최대 보호막 5개를 넘는 만큼은 버려짐).")]
    [Min(1)] public int shieldPerUse = 1;

    [System.NonSerialized] private PlayerHackTool subscribedTool;
    [System.NonSerialized] private PlayerHealth health;
    [System.NonSerialized] private PlayerShieldBreakVfx shieldVfx;
    [System.NonSerialized] private bool vfxPending; // 이번 사용으로 보호막이 늘어서, 해킹 발동 순간 연출을 띄워야 하는지

    public void Apply(PassiveEffectContext context, string sourceItemId)
    {
        if (context == null || context.PlayerObject == null)
        {
            Debug.LogWarning("HackShieldEffect: 플레이어를 찾을 수 없어 58번 효과를 적용하지 못했습니다.");
            return;
        }

        GrantHackingTools(context, sourceItemId);

        Unsubscribe(); // 혹시 남아 있을 이전 구독 정리(중복 방지 - 효과 객체는 아이템 에셋에 하나뿐이라 재사용됨)

        var tool = context.PlayerObject.GetComponent<PlayerHackTool>();
        health = context.Health != null ? context.Health : context.PlayerObject.GetComponent<PlayerHealth>();
        if (tool == null || health == null)
        {
            Debug.LogWarning("HackShieldEffect: PlayerHackTool 또는 PlayerHealth를 찾을 수 없어 보호막 효과를 붙이지 못했습니다.");
            return;
        }

        shieldVfx = context.PlayerObject.GetComponent<PlayerShieldBreakVfx>();
        vfxPending = false;

        subscribedTool = tool;
        subscribedTool.OnHackToolSpent += HandleHackToolSpent;
        subscribedTool.OnHackDeployed += HandleHackDeployed;
    }

    // [패시브 잃기 - 48번 등] 보호막 얻기만 멈춥니다. 이미 얻은 보호막/해킹툴은 그대로 둡니다(형빈 확정).
    public void Remove(PassiveEffectContext context, string sourceItemId)
    {
        Unsubscribe();
    }

    private void Unsubscribe()
    {
        if (subscribedTool != null)
        {
            subscribedTool.OnHackToolSpent -= HandleHackToolSpent;
            subscribedTool.OnHackDeployed -= HandleHackDeployed;
        }
        subscribedTool = null;
        vfxPending = false;
    }

    private void HandleHackToolSpent()
    {
        if (health == null || health.IsDead) return;
        // 보호막은 지금 바로 주고(최대치면 늘지 않고 아무 일도 없음), 연출은 해킹 발동 순간으로 미룹니다.
        int gained = health.AddShield(shieldPerUse, false);
        if (gained > 0) vfxPending = true;
    }

    // 해킹 효과가 발동하는 순간(사용 동작 중간) - 이번 사용으로 보호막이 늘었으면 획득 연출을 띄웁니다.
    private void HandleHackDeployed(Vector3 center, float radius, System.Collections.Generic.IReadOnlyList<EnemyHealth> hacked)
    {
        if (!vfxPending) return;
        vfxPending = false;

        if (health != null && health.IsDead) return;
        if (shieldVfx == null && health != null) shieldVfx = health.GetComponent<PlayerShieldBreakVfx>();
        if (shieldVfx != null) shieldVfx.PlayAcquire();
    }

    // 획득 시 해킹툴 지급입니다. 115번 GrantGrenades와 같은 규칙입니다(위 [기획] 참고).
    private void GrantHackingTools(PassiveEffectContext context, string sourceItemId)
    {
        if (hackingToolAmount <= 0) return;

        if (context.Resources == null)
        {
            Debug.LogWarning("HackShieldEffect: PlayerResources를 찾을 수 없어 해킹툴을 지급하지 못했습니다.");
            return;
        }

        // 1회성 지급 기록(GameFlowManager)으로 교체/버리기를 통한 무한 복제를 막습니다. 48번으로 얻을 때는
        // (PassiveEffectContext.IgnoreOneTimeGrantLimit) 기록을 거치지 않고 지급합니다. GameFlowManager가 없는 씬(아이템 테스트
        // 씬)에서는 경고만 남기고 지급합니다.
        if (!context.IgnoreOneTimeGrantLimit)
        {
            var gameFlow = Object.FindFirstObjectByType<GameFlowManager>();
            if (gameFlow != null)
            {
                if (!gameFlow.TryConsumeOneTimeGrant(sourceItemId + "_hack_grant")) return;
            }
            else
            {
                Debug.LogWarning("HackShieldEffect: GameFlowManager를 찾지 못해 1회성 지급 제한 없이 해킹툴을 지급합니다.");
            }
        }

        context.Resources.Add(BasicItemType.HackingTool, hackingToolAmount);
    }
}
