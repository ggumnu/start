using UnityEngine;

// 패시브 72번 - "방을 클리어하면 체력을 0.5 회복한다. 체력이 가득 차 있으면 대신 코스트를 1 회복한다."
//
// [기획 - 형빈 확정]
// - 전투방(일반방/보스방)을 클리어할 때마다 발동합니다(RoomController.OnAnyRoomCleared - 일반방/보스방에서만 발생).
// - 현재 체력이 최대 체력보다 낮으면 healAmount(기본 0.5)만큼 회복합니다(최대 체력은 넘지 않음).
// - 체력이 가득 차 있으면 회복 대신 코스트를 costAmount(기본 1)만큼 줍니다. 이 코스트는 전투방 클리어 시 받는 기본 코스트
//   (PlayerStats.CostRecovery - PlayerResources가 지급)와 별개로 "추가로" 주는 것입니다(코스트 회복 스탯과 무관).
// - 코스트도 이미 최대치면 아무 일도 일어나지 않습니다(연출도 없음).
// - 클리어 순간 플레이어가 죽어 있으면 아무것도 하지 않습니다.
//
// [순서 - 기본 코스트 지급 뒤에 판정]
// PlayerResources는 시작할 때(Awake) 같은 알림을 먼저 구독하고, 이 효과는 아이템을 얻을 때 구독합니다. 알림은 구독한 순서대로
// 불리므로 항상 "기본 코스트 지급 -> 72번" 순서입니다. 그래서 기본 지급으로 코스트가 이미 꽉 찼다면 72번 코스트는 연출 없이
// 건너뜁니다(실제로 얻은 게 없으므로).
//
// [연출 - 54번과 같은 아이콘 연출 (CreditPopupDisplay 범용화, 형빈 확정)]
// 체력을 얻으면 반 하트 아이콘 + 빨간 발광/입자, 코스트를 얻으면 코스트 아이콘 + 초록 발광/입자를 플레이어 머리 위에 띄우고,
// 연출이 끝날 때까지 플레이어를 따라다닙니다. 크기/시간/입자 수 등은 54번과 같은 값입니다(색만 다름).
// - 아이콘 칸(Heal Popup > Icon / Cost Popup > Icon)을 비워두면 HUD의 반 하트(PlayerHeartDisplay.halfHeartSprite)와
//   코스트 아이콘(보물상자 정보 HUD - TreasureChestInfoDisplay.costIcon)을 자동으로 씁니다. 다른 그림으로 바꾸려면 그 칸에 스프라이트를 넣으면 됩니다.
//
// [잃을 때 - 48번 등]
// 클리어 알림 구독을 풀어서 그 뒤로는 발동하지 않습니다. 이미 받은 체력/코스트는 그대로입니다.
//
// [테스트 주의]
// ItemTestArena는 실제 방 클리어 흐름(RoomController)을 거치지 않으므로, 실제 레벨의 일반방/보스방에서 확인해야 합니다.
//
// [아이템 에셋 설정]
// ItemDefinition: Item Id = 72, Category = Passive, Special Effects에 RoomClearHealOrCostEffect 하나. Stat Modifiers는 비워둡니다.
[System.Serializable]
public class RoomClearHealOrCostEffect : IPassiveItemEffect, IRemovablePassiveEffect
{
    [Tooltip("방 클리어 시 회복하는 체력입니다(기본 0.5). 체력이 가득 차 있으면 회복 대신 코스트를 줍니다.")]
    [Min(0f)] public float healAmount = 0.5f;

    [Tooltip("체력이 가득 차 있을 때 대신 주는 코스트입니다(기본 1). 기본 클리어 코스트와 별개로 추가 지급합니다.")]
    [Min(0)] public int costAmount = 1;

    [Tooltip("체력을 회복했을 때 플레이어 머리 위에 띄우는 연출입니다(54번과 같은 연출, 빨강). Icon을 비우면 HUD의 반 하트를 씁니다.")]
    public CreditPopupSettings healPopup = new CreditPopupSettings
    {
        glowColor = new Color(1f, 0.3f, 0.3f, 0.85f),
        sparkleColor = new Color(1f, 0.45f, 0.45f, 1f),
    };

    [Tooltip("코스트를 얻었을 때 플레이어 머리 위에 띄우는 연출입니다(54번과 같은 연출, 초록). Icon을 비우면 HUD의 코스트 아이콘을 씁니다.")]
    public CreditPopupSettings costPopup = new CreditPopupSettings
    {
        glowColor = new Color(0.35f, 1f, 0.45f, 0.85f),
        sparkleColor = new Color(0.55f, 1f, 0.6f, 1f),
    };

    // 클리어 알림을 받을 때 쓸 플레이어 정보입니다(Apply에서 받아둠).
    private PassiveEffectContext context;

    public void Apply(PassiveEffectContext context, string sourceItemId)
    {
        if (context == null || context.PlayerObject == null)
        {
            Debug.LogWarning("RoomClearHealOrCostEffect: 플레이어를 찾을 수 없어 효과를 적용하지 못했습니다.");
            return;
        }

        this.context = context;
        RoomController.OnAnyRoomCleared -= HandleRoomCleared; // 혹시 남아 있을 이전 구독 정리(중복 방지)
        RoomController.OnAnyRoomCleared += HandleRoomCleared;
    }

    public void Remove(PassiveEffectContext context, string sourceItemId)
    {
        RoomController.OnAnyRoomCleared -= HandleRoomCleared;
        this.context = null;
    }

    // 이 알림은 마지막 적이 죽는 순간(EnemyHealth.Die() 안)에 동기적으로 불립니다. 여기서 예외가 나면 그 적의 사라짐/방 클리어
    // 처리가 막힐 수 있으므로(RoomController.HandleCleared 주석 참고) 전부 try/catch로 감쌉니다.
    private void HandleRoomCleared(RoomInstance room)
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
                ShowPopup(healPopup, GetHudHalfHeartIcon());
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
            Debug.LogError($"RoomClearHealOrCostEffect: 방 클리어 회복 처리 중 오류가 발생해 건너뜁니다 (방 클리어 자체에는 영향 없음): {e}");
        }
    }

    // 플레이어 머리 위에 연출을 띄우고, 끝날 때까지 플레이어를 따라다니게 합니다.
    private void ShowPopup(CreditPopupSettings settings, Sprite fallbackIcon)
    {
        GameObject player = context.PlayerObject;
        CreditPopupDisplay.Show(player, player.transform.position, settings, fallbackIcon, followTarget: true);
    }

    private static Sprite GetHudHalfHeartIcon()
    {
        var hud = Object.FindFirstObjectByType<PlayerHeartDisplay>();
        return hud != null ? hud.halfHeartSprite : null;
    }

    // 코스트 아이콘은 보물상자 정보 HUD(TreasureChestInfoDisplay)의 Cost Icon을 씁니다(보물상자에서 코스트가 나올 때 보이는 아이콘).
    private static Sprite GetHudCostIcon()
    {
        var hud = Object.FindFirstObjectByType<TreasureChestInfoDisplay>();
        return hud != null ? hud.costIcon : null;
    }
}
