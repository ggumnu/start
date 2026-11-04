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
[Serializable]
public class KillChanceHealTrinketEffect : ITrinketEffect
{
    public float firstEquipHealAmount = 2f;
    public float healChancePerKill = 0.25f;
    public float healAmountPerKill = 1f;

    private PlayerHealth health;
    private Action<Vector3> enemyDiedHandler;

    public void Apply(TrinketEffectContext context, string sourceItemId)
    {
        if (context == null) return;

        health = context.Health;

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
                    health.Heal(healAmountPerKill);
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
    }
}
