using System;
using UnityEngine;

// 연속 돌진(EnemyChainDashAttack)의 세부 스탯입니다. EnemyDefinition.attackConfig에 이 타입을 고르면 아래 칸만 보이고, 스폰 때 EnemyChainDashAttack에
// 직접 대입합니다. 첫 돌진 충전 이펙트 칸은 채웠을 때만 프리팹 값을 덮어씁니다. 설계 배경은 Docs/설계노트.md [연속 돌진 수치].
[Serializable]
public class ChainDashAttackConfig : IEnemyAttackConfig
{
    [Header("사거리 / 최초 충전")]
    [Tooltip("이 거리 이내로 플레이어가 들어오면 AIController가 공격을 시작시킵니다.")]
    public float attackRange = 5f;
    [Tooltip("최초 돌진의 충전(텔레그래프) 시간입니다 - 이 순간의 플레이어 위치가 첫 " +
             "번째 돌진 방향으로 고정됩니다. 위협감을 주기 위해 충분히 길게 잡는 걸 " +
             "권장합니다.")]
    public float initialTelegraphDuration = 0.6f;

    [Header("연속 돌진")]
    [Tooltip("최초 돌진 이후 추가로 몇 번 더 돌진할지입니다. 0이면 일반 돌진 적과 동일하게 " +
             "한 번만 돌진합니다. 1이면 총 두 번 돌진합니다.")]
    public int chainDashCount = 1;
    [Tooltip("두 번째 이상의 돌진에서 쓰는 충전 시간입니다. initialTelegraphDuration과 " +
             "별개로 설정할 수 있습니다 - 보통 최초보다 짧게 잡아서 반응할 틈을 줄이는 " +
             "식으로 씁니다.")]
    public float chainTelegraphDuration = 0.25f;

    [Header("돌진 (모든 회차 공통)")]
    public float dashSpeed = 8f;
    [Tooltip("이 시간 동안 정해진 방향으로 계속 돌진합니다. 모든 돌진 회차가 동일하게 " +
             "이 값을 씁니다.")]
    public float dashDuration = 0.5f;
    [Tooltip("연속 돌진 전체가 끝난 뒤(모든 회차 종료 후) 다음 공격까지 대기시간입니다. " +
             "돌진 사이사이(충전으로 넘어가는 구간)에는 적용되지 않습니다.")]
    public float postDashCooldown = 1f;
    [Tooltip("돌진 중(Dashing 상태)에만 적용되는 접촉 데미지입니다. 모든 돌진 회차가 " +
             "동일하게 이 값을 쓰며, 평소(대기/추적/충전 중)에는 대신 EnemyDefinition " +
             "공용 스탯의 Attack Power가 적용됩니다.")]
    public float dashDamage = 2f;

    [Header("첫 돌진 충전 이펙트 (비워두면 프리팹의 칸을 그대로 씀)")]
    public DashChargeVfxSettings firstDashChargeVfx = new DashChargeVfxSettings();

    public Type ExpectedComponentType => typeof(EnemyChainDashAttack);

    public void ApplyTo(EnemyStats stats, GameObject enemyGO)
    {
        var attack = enemyGO.GetComponent<EnemyChainDashAttack>();
        if (attack == null)
        {
            Debug.LogWarning($"ChainDashAttackConfig: '{enemyGO.name}'에서 EnemyChainDashAttack을 찾지 못해 " +
                              "연속 돌진 수치를 적용하지 못했습니다. 프리팹에 EnemyChainDashAttack이 붙어있는지 확인하세요.");
            return;
        }

        attack.attackRange = attackRange;
        attack.initialTelegraphDuration = initialTelegraphDuration;
        attack.chainDashCount = chainDashCount;
        attack.chainTelegraphDuration = chainTelegraphDuration;
        attack.dashSpeed = dashSpeed;
        attack.dashDuration = dashDuration;
        attack.postDashCooldown = postDashCooldown;
        attack.dashDamage = dashDamage;
        if (firstDashChargeVfx != null && firstDashChargeVfx.HasPrefab) attack.firstDashChargeVfx = firstDashChargeVfx.Clone();
    }
}