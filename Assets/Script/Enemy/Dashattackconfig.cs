using System;
using UnityEngine;

// 일반 돌진(EnemyMeleeAttack)의 세부 스탯입니다. EnemyDefinition.attackConfig에 이 타입을 고르면 아래 칸만 보이고, 스폰 때 EnemyMeleeAttack에 직접 대입합니다
// (EnemyStats를 거치지 않음). 첫 돌진 충전 이펙트 칸은 채웠을 때만 프리팹 값을 덮어씁니다. 설계 배경은 Docs/설계노트.md [돌진 수치].
[Serializable]
public class DashAttackConfig : IEnemyAttackConfig
{
    [Header("사거리 / 충전")]
    [Tooltip("이 거리 이내로 플레이어가 들어오면 AIController가 공격을 시작시킵니다.")]
    public float attackRange = 5f;
    [Tooltip("충전(텔레그래프) 시간 - 이 순간의 플레이어 위치가 돌진 방향으로 고정됩니다.")]
    public float telegraphDuration = 0.6f;

    [Header("돌진")]
    public float dashSpeed = 8f;
    [Tooltip("이 시간 동안 정해진 방향으로 계속 돌진합니다.")]
    public float dashDuration = 0.5f;
    [Tooltip("돌진 종료 후 다음 공격까지 대기시간입니다.")]
    public float postDashCooldown = 1f;
    [Tooltip("돌진 중(Dashing 상태)에만 적용되는 접촉 데미지입니다. 평소(대기/추적/충전 " +
             "중)에는 대신 EnemyDefinition 공용 스탯의 Attack Power가 적용됩니다.")]
    public float dashDamage = 2f;

    [Header("첫 돌진 충전 이펙트 (비워두면 프리팹의 칸을 그대로 씀)")]
    public DashChargeVfxSettings firstDashChargeVfx = new DashChargeVfxSettings();

    public Type ExpectedComponentType => typeof(EnemyMeleeAttack);

    public void ApplyTo(EnemyStats stats, GameObject enemyGO)
    {
        var attack = enemyGO.GetComponent<EnemyMeleeAttack>();
        if (attack == null)
        {
            Debug.LogWarning($"DashAttackConfig: '{enemyGO.name}'에서 EnemyMeleeAttack을 찾지 못해 " +
                              "돌진 수치를 적용하지 못했습니다. 프리팹에 EnemyMeleeAttack이 붙어있는지 확인하세요.");
            return;
        }

        attack.attackRange = attackRange;
        attack.telegraphDuration = telegraphDuration;
        attack.dashSpeed = dashSpeed;
        attack.dashDuration = dashDuration;
        attack.postDashCooldown = postDashCooldown;
        attack.dashDamage = dashDamage;
        if (firstDashChargeVfx != null && firstDashChargeVfx.HasPrefab) attack.firstDashChargeVfx = firstDashChargeVfx.Clone();
    }
}