using UnityEngine;

// 패시브 117번 - "보물상자를 열 때마다 체력을 1 회복한다. 체력이 가득 차 있으면 대신 코스트를 1 회복한다."
//
// [기획 - 형빈 확정]
// - "상자 열기"가 기준입니다. 보물상자가 처음 열리는 순간(첫 F)마다 한 번씩 발동합니다. 이미 열린 상자를 다시 눌러도 발동하지 않습니다.
//   - 포함: 일반방/보스방 보물상자, 동아리방 아이템 상자, 9번으로 복제된 상자, 방 바닥에 미리 놓인 상자(특수방 크레딧 더미 상자 등).
//   - 제외: Ctrl로 내려놓은 슬롯 아이템 좌대, 상점의 임시 좌대(상자를 여는 것이 아니므로 - 이 좌대들은 열기 신호를 보내지 않음).
// - 체력이 가득 차 있지 않으면 healAmount(기본 1)만큼 회복합니다(최대 체력은 넘지 않음 - 0.5만 깎였으면 0.5만 참. 101번이 있으면 2배).
// - 체력이 가득 차 있으면 회복 대신 코스트를 costAmount(기본 1)만큼 줍니다. 코스트도 가득 차 있으면 아무 일도 없습니다(연출도 없음).
// - 회복은 첫 F(열기) 때 일어나고 상자 내용물은 두 번째 F부터 줍기 때문에, 117번으로 체력이 가득 차면 상자 안의 "현재 체력+1"은
//   평소처럼 상자에 그대로 남습니다(낭비되지 않음).
// - 플레이어가 죽어 있으면 아무것도 하지 않습니다. 잃으면(48번 등) 그 뒤로는 발동하지 않습니다(이미 받은 체력/코스트는 그대로).
//
// [연출 - 72번과 같은 아이콘 연출 (CreditPopupDisplay)]
// 체력을 얻으면 하트 아이콘 + 빨간 발광/입자, 코스트를 얻으면 코스트 아이콘 + 초록 발광/입자를 플레이어 머리 위에 띄우고, 연출이 끝날
// 때까지 플레이어를 따라다닙니다. 회복했다는 알림이므로 실제 회복량과 상관없이 꽉 찬 하트를 씁니다(형빈 확정).
// - 아이콘 칸(Heal Popup > Icon / Cost Popup > Icon)을 비워두면 HUD의 꽉 찬 하트(PlayerHeartDisplay.fullHeartSprite)와
//   코스트 아이콘(TreasureChestInfoDisplay.costIcon)을 자동으로 씁니다.
//
// [구현]
// ContainerOpenEvents.OnAnyContainerOpened(TreasureChest.Open / ItemPedestal.Open이 상자를 처음 열 때 보내는 신호)를 구독합니다.
//
// [아이템 에셋 설정]
// ItemDefinition: Item Id = 117, Category = Passive, Special Effects에 ChestOpenHealOrCostEffect 하나. Stat Modifiers는 비워둡니다.
[System.Serializable]
public class ChestOpenHealOrCostEffect : IPassiveItemEffect, IRemovablePassiveEffect
{
    [Tooltip("보물상자를 열 때 회복하는 체력입니다(기본 1). 체력이 가득 차 있으면 회복 대신 코스트를 줍니다.")]
    [Min(0f)] public float healAmount = 1f;

    [Tooltip("체력이 가득 차 있을 때 대신 주는 코스트입니다(기본 1).")]
    [Min(0)] public int costAmount = 1;

    [Tooltip("체력을 회복했을 때 플레이어 머리 위에 띄우는 연출입니다(72번과 같은 연출, 빨강). Icon을 비우면 HUD의 꽉 찬 하트를 씁니다.")]
    public CreditPopupSettings healPopup = new CreditPopupSettings
    {
        glowColor = new Color(1f, 0.3f, 0.3f, 0.85f),
        sparkleColor = new Color(1f, 0.45f, 0.45f, 1f),
    };

    [Tooltip("코스트를 얻었을 때 플레이어 머리 위에 띄우는 연출입니다(72번과 같은 연출, 초록). Icon을 비우면 HUD의 코스트 아이콘을 씁니다.")]
    public CreditPopupSettings costPopup = new CreditPopupSettings
    {
        glowColor = new Color(0.35f, 1f, 0.45f, 0.85f),
        sparkleColor = new Color(0.55f, 1f, 0.6f, 1f),
    };

    // 상자가 열릴 때 쓸 플레이어 정보입니다(Apply에서 받아둠).
    [System.NonSerialized] private PassiveEffectContext context;

    public void Apply(PassiveEffectContext context, string sourceItemId)
    {
        if (context == null || context.PlayerObject == null)
        {
            Debug.LogWarning("ChestOpenHealOrCostEffect: 플레이어를 찾을 수 없어 117번 효과를 적용하지 못했습니다.");
            return;
        }

        this.context = context;
        ContainerOpenEvents.OnAnyContainerOpened -= HandleContainerOpened; // 혹시 남아 있을 이전 구독 정리(중복 방지)
        ContainerOpenEvents.OnAnyContainerOpened += HandleContainerOpened;
    }

    public void Remove(PassiveEffectContext context, string sourceItemId)
    {
        ContainerOpenEvents.OnAnyContainerOpened -= HandleContainerOpened;
        this.context = null;
    }

    // 이 알림은 상자가 내용물을 정하기 직전에 동기적으로 불립니다. 여기서 예외가 나면 상자 열기가 막힐 수 있으므로 전부 감쌉니다.
    private void HandleContainerOpened()
    {
        try
        {
            if (context == null || context.PlayerObject == null) return;

            PlayerHealth health = context.Health != null ? context.Health : context.PlayerObject.GetComponent<PlayerHealth>();
            PlayerStats stats = context.Stats != null ? context.Stats : context.PlayerObject.GetComponent<PlayerStats>();
            if (health == null || stats == null || health.IsDead) return;

            bool isFullHealth = health.CurrentHealth >= stats.MaxHealth - 0.001f;

            if (!isFullHealth)
            {
                if (healAmount <= 0f) return;

                health.Heal(healAmount);
                ShowPopup(healPopup, GetHudFullHeartIcon());
                return;
            }

            // 체력이 가득 차 있으면 코스트를 줍니다. 코스트도 꽉 차 있으면 아무 일도 없습니다(연출도 없음).
            PlayerResources resources = context.Resources != null ? context.Resources : context.PlayerObject.GetComponent<PlayerResources>();
            if (resources == null || costAmount <= 0) return;
            if (resources.GetAmount(BasicItemType.Cost) >= resources.maxCost) return;

            resources.Add(BasicItemType.Cost, costAmount);
            ShowPopup(costPopup, GetHudCostIcon());
        }
        catch (System.Exception e)
        {
            Debug.LogError($"ChestOpenHealOrCostEffect: 상자 열기 회복 처리 중 오류가 발생해 건너뜁니다(상자 열기 자체에는 영향 없음): {e}");
        }
    }

    // 플레이어 머리 위에 연출을 띄우고, 끝날 때까지 플레이어를 따라다니게 합니다.
    private void ShowPopup(CreditPopupSettings settings, Sprite fallbackIcon)
    {
        GameObject player = context.PlayerObject;
        CreditPopupDisplay.Show(player, player.transform.position, settings, fallbackIcon, followTarget: true);
    }

    private static Sprite GetHudFullHeartIcon()
    {
        var hud = Object.FindFirstObjectByType<PlayerHeartDisplay>();
        return hud != null ? hud.fullHeartSprite : null;
    }

    // 코스트 아이콘은 보물상자 정보 HUD(TreasureChestInfoDisplay)의 Cost Icon을 씁니다(72번과 같음).
    private static Sprite GetHudCostIcon()
    {
        var hud = Object.FindFirstObjectByType<TreasureChestInfoDisplay>();
        return hud != null ? hud.costIcon : null;
    }
}
