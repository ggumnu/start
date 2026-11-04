using System;
using UnityEngine;

// 기본 근접 적(EnemyContactOnlyAttack - 돌진 없이 계속 접근하다가 닿으면 회전 연출을
// 재생하는 적)의 공격 방식 세부 스탯입니다. EnemyDefinition.attackConfig에 이 타입을
// 선택하면 아래 필드들만 인스펙터에 나타납니다 - DashAttackConfig(EnemyMeleeAttack용)와
// 완전히 동일한 패턴입니다.
//
// 여기 있는 값들은 EnemyContactOnlyAttack만 사용하고 다른 컴포넌트(AIController 등)는
// 참조하지 않으므로, DashAttackConfig가 그러듯 EnemyStats를 거치지 않고
// EnemyContactOnlyAttack에 직접 대입합니다.
[Serializable]
public class ContactOnlyAttackConfig : IEnemyAttackConfig
{
    [Header("사거리 / 접촉")]
    [Tooltip("이 거리 이내로 플레이어가 들어오면 AIController가 공격 시퀀스를 시작시킵니다. " +
             "이동형 공격이 아니라 제자리에 가까운 회전이므로, 프리팹 콜라이더 반지름 + " +
             "플레이어 콜라이더 반지름 정도의 작은 값을 권장합니다(사실상 '몸이 닿음'과 같아지도록).")]
    public float attackRange = 1.4f;

    [Header("공격 시퀀스 - 회전하며 계속 접근")]
    [Tooltip("회전에 걸리는 시간(초)입니다. 이 구간에도 플레이어를 향해 계속 이동합니다.")]
    public float spinDuration = 0.4f;
    [Tooltip("spinDuration 동안 몇 바퀴 회전할지입니다. 1이면 360도, 2면 720도.")]
    public float spinRotations = 1f;
    [Tooltip("공격 시퀀스 종료 후 다음 공격까지 대기시간입니다.")]
    public float postAttackCooldown = 0.6f;

    public Type ExpectedComponentType => typeof(EnemyContactOnlyAttack);

    public void ApplyTo(EnemyStats stats, GameObject enemyGO)
    {
        var attack = enemyGO.GetComponent<EnemyContactOnlyAttack>();
        if (attack == null)
        {
            Debug.LogWarning($"ContactOnlyAttackConfig: '{enemyGO.name}'에서 EnemyContactOnlyAttack을 찾지 못해 " +
                              "수치를 적용하지 못했습니다. 프리팹에 EnemyContactOnlyAttack이 붙어있는지 확인하세요.");
            return;
        }

        attack.attackRange = attackRange;
        attack.spinDuration = spinDuration;
        attack.spinRotations = spinRotations;
        attack.postAttackCooldown = postAttackCooldown;
    }
}