using System;
using UnityEngine;

// 장신구 32번 - "획득 시 최초 한정으로 현재 체력을 2 회복한다. 소지 중 적을 처치할 때마다
// 25% 확률로 현재 체력을 1 회복한다."
//
// [기획]
// - 장착하는 순간 현재 체력을 firstEquipHealAmount(기본 2)만큼 회복합니다 - 20번/39번
//   장신구와 동일하게 같은 레벨 안에서는 딱 한 번만(GameFlowManager.TryConsumeOneTimeGrant
//   참고). 같은 레벨 안에서 이 장신구를 뺐다가 다시 껴도 또 받지 못하고, 다음 레벨로
//   넘어가면 다시 받을 수 있습니다.
// - 장착하고 있는 동안, 게임 전체에서 적이 죽을 때마다 healChancePerKill(기본 0.25) 확률로
//   현재 체력을 healAmountPerKill(기본 1)만큼 회복합니다. "게임 전체의 처치"를 감지하기
//   위해 EnemyHealth.cs에 새로 추가한 정적 이벤트 OnAnyEnemyDied를 구독합니다(적 하나마다
//   개별 구독할 필요 없이, 이후 새로 스폰되는 적까지 포함해 전역으로 받습니다).
// - PlayerHealth.Heal()은 이미 최대 체력을 넘지 않도록 clamp되어 있어 별도 처리가
//   필요 없습니다.
// - 다른 장신구와 교체되거나 이 아이템을 잃으면(Remove) 처치 시 회복 효과만 사라집니다.
//   이미 회복된 체력 자체는(스탯 모디파이어가 아니라 실제로 일어난 회복이라) 되돌릴 대상이
//   없어 그대로 유지됩니다.
//
// [연출 - 117번과 같은 아이콘 연출 (CreditPopupDisplay), 형빈 확정]
// 처치 시 회복이 실제로 일어나면(체력이 가득 차 있지 않을 때) 플레이어 머리 위에 꽉 찬 하트 아이콘 + 빨간 발광/입자를 띄우고, 연출이
// 끝날 때까지 플레이어를 따라다닙니다. 체력이 가득 차 있어 회복이 없으면 띄우지 않습니다. 획득 순간의 최초 회복에는 띄우지 않습니다.
// - 아이콘 칸(Heal Popup > Icon)을 비워두면 HUD의 꽉 찬 하트(PlayerHeartDisplay.fullHeartSprite)를 자동으로 씁니다.
[Serializable]
public class KillChanceHealTrinketEffect : ITrinketEffect
{
    public float firstEquipHealAmount = 2f;
    public float healChancePerKill = 0.25f;
    public float healAmountPerKill = 1f;

    [UnityEngine.Tooltip("처치 시 회복이 일어났을 때 플레이어 머리 위에 띄우는 연출입니다(117번과 같은 연출, 빨강). Icon을 비우면 HUD의 꽉 찬 하트를 씁니다.")]
    public CreditPopupSettings healPopup = new CreditPopupSettings
    {
        glowColor = new Color(1f, 0.3f, 0.3f, 0.85f),
        sparkleColor = new Color(1f, 0.45f, 0.45f, 1f),
    };

    private PlayerHealth health;
    [System.NonSerialized] private GameObject playerObject;
    private Action<Vector3> enemyDiedHandler;

    public void Apply(TrinketEffectContext context, string sourceItemId)
    {
        if (context == null) return;

        health = context.Health;
        playerObject = context.PlayerObject;

        bool grantedNow = context.GameFlow != null &&
                           context.GameFlow.TryConsumeOneTimeGrant($"{sourceItemId}_heal");
        if (grantedNow && health != null)
        {
            health.Heal(firstEquipHealAmount);
        }

        if (health != null)
        {
            enemyDiedHandler = _ =>
            {
                if (UnityEngine.Random.value < healChancePerKill)
                {
                    // 체력이 가득 차 있으면 회복도 연출도 없습니다(실제로 회복이 일어날 때만 연출).
                    if (health == null || health.IsDead) return;
                    var stats = playerObject != null ? playerObject.GetComponent<PlayerStats>() : null;
                    bool wasFull = stats != null && health.CurrentHealth >= stats.MaxHealth - 0.001f;

                    health.Heal(healAmountPerKill);
                    if (!wasFull) ShowHealPopup();
                }
            };
            EnemyHealth.OnAnyEnemyDied += enemyDiedHandler;
        }
    }

    public void Remove(TrinketEffectContext context, string sourceItemId)
    {
        if (enemyDiedHandler != null)
        {
            EnemyHealth.OnAnyEnemyDied -= enemyDiedHandler;
        }
        enemyDiedHandler = null;
        health = null;
        playerObject = null;
    }

    // 플레이어 머리 위에 회복 연출을 띄우고, 끝날 때까지 플레이어를 따라다니게 합니다(117번과 같은 방식). 연출 오류가 회복/처치
    // 처리를 막지 않도록 감쌉니다.
    private void ShowHealPopup()
    {
        try
        {
            if (playerObject == null) return;
            CreditPopupDisplay.Show(playerObject, playerObject.transform.position, healPopup, GetHudFullHeartIcon(), followTarget: true);
        }
        catch (Exception e)
        {
            Debug.LogError($"KillChanceHealTrinketEffect: 회복 연출 표시 중 오류가 발생해 건너뜁니다(회복 자체에는 영향 없음): {e}");
        }
    }

    private static Sprite GetHudFullHeartIcon()
    {
        var hud = UnityEngine.Object.FindFirstObjectByType<PlayerHeartDisplay>();
        return hud != null ? hud.fullHeartSprite : null;
    }
}
