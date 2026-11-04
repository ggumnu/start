using System;
using UnityEngine;

// 원거리(사격) 공격 방식의 세부 스탯입니다. EnemyDefinition.attackConfig에 이 타입을
// 선택하면 아래 필드들만 인스펙터에 나타납니다.
//
// DashAttackConfig와 달리 rangedDamage 등 대부분의 값은 EnemyRangedAttack뿐 아니라
// EnemyRangedAIController도 같이 읽습니다(engageRange로 사격 여부, chaseRange/
// retreatRange로 이동 여부를 판단 - EnemyRangedAIController.UpdateMovement()/
// UpdateFiring() 참고). 그래서 특정 컴포넌트에 직접 꽂지 않고 지금까지와 동일하게
// 공용 버스인 EnemyStats에 대입합니다.
//
// [연사 - burstCount/burstInterval은 EnemyRangedAttack 전용]
// 이 두 값은 AIController가 아니라 EnemyRangedAttack만 사용하므로(연사 자체는 그
// 컴포넌트 안에서 코루틴으로 완결됩니다), DashAttackConfig가 돌진 수치를 EnemyMeleeAttack에
// 직접 대입하던 것과 동일한 방식으로 EnemyStats를 거치지 않고 EnemyRangedAttack에 바로
// 대입합니다. burstCount를 1로 두면(기본값) 기존과 동일한 "한 발만 쏘는" 원거리 적이 되고,
// 2 이상으로 설정하면 연사형이 됩니다.
//
// [샷건 - pelletCount/totalSpreadAngle도 EnemyRangedAttack 전용]
// 연사(시간 축)와 완전히 독립된 축입니다 - 한 번의 발사(틱)마다 부채꼴로 몇 발을 동시에
// 퍼뜨려 쏠지를 정합니다. pelletCount를 1로 두면(기본값) 기존과 동일하게 한 발만 나가고,
// 2 이상으로 설정하면 totalSpreadAngle 범위 안에 균등하게 벌어진 여러 발이 동시에
// 나갑니다(EnemyRangedAttack.SpawnPelletSpread() 참고). 연사와 샷건을 동시에 켜면
// "매 틱마다 부채꼴 산탄"이 되는 것도 가능하지만, 그건 두 값을 각각 설정하기 나름입니다 -
// 이 설정 자체가 둘을 강제로 묶지는 않습니다.
//
// 같은 프리팹(EnemyRangedAttack이 붙은)을 여러 EnemyDefinition이 공유하면서 이 값들만
// 다르게 줄 수 있습니다 - 일반 원거리 적/연사 원거리 적/샷건 원거리 적을 전부 같은
// 프리팹에서, EnemyDefinition만 따로 만들어 구분 가능합니다.
//
// [변경 - 이동 판단을 "사격 개시 거리" 하나가 아니라 세 단계로 세분화 (형빈 요청)]
// rangedAttackRange(탄환이 날아가는 거리) / engageRange(사격 여부만 결정) / chaseRange
// (추격을 멈추는 거리 - 이동 여부 결정) / retreatRange(후퇴 거리)로 나눴습니다. 자세한
// 이유와 동작 예시는 EnemyStats.cs 상단 주석 참고. chaseRange를 engageRange와 같게
// 두면 예전(사격 개시 거리에 도달하는 즉시 멈춤)과 동일한 동작이 됩니다.
//
// [변경 - Retreat Duration도 이 에셋에서 설정 가능 (형빈 요청)]
// retreatDuration(후퇴 지속 시간 - 후퇴가 시작되면 거리와 무관하게 최소 이 시간 동안은
// 무조건 후퇴를 이어가는 값)은 EnemyStats가 아니라 EnemyRangedAIController 컴포넌트
// 자신의 필드입니다(retreatSearchRadius 등 다른 "후퇴 위치 탐색" 값들과 동일한 위치 -
// EnemyRangedAIController.cs 참고). 그래서 아래 ApplyTo()에서 stats가 아니라 enemyGO에서
// EnemyRangedAIController를 직접 찾아 대입합니다. wallLayerMask/losOrigin처럼 이
// 컴포넌트가 원래도 프리팹에 미리 붙어있어야 하는 값들과 같은 이유로, 이 컴포넌트가
// 아직 프리팹에 없다면(=RealEnemySpawner가 스폰 시점에 처음 자동으로 붙이는 경우) 이
// 값은 적용되지 않고 경고 로그만 남습니다 - 정상적으로 구성된 원거리 적 프리팹이라면
// 이미 wallLayerMask/losOrigin을 연결하기 위해 이 컴포넌트가 붙어있으므로 문제되지
// 않습니다.
[Serializable]
public class ShootAttackConfig : IEnemyAttackConfig
{
    [Header("원거리 공격 스탯")]
    [Tooltip("EnemyProjectile 한 발당 데미지입니다.")]
    public float rangedDamage = 2f;
    [Tooltip("EnemyProjectile 오브젝트의 스케일 배율입니다.")]
    public float rangedProjectileSize = 1f;
    [Tooltip("EnemyProjectile의 이동 속도입니다.")]
    public float rangedProjectileSpeed = 14f;
    [Tooltip("탄환이 소멸하기까지 날아가는 거리입니다. AI의 이동/사격 판단과는 무관하고 " +
             "순수하게 탄환 자체의 사거리입니다 - 이동/사격 판단은 아래 Engage/Chase/" +
             "Retreat Range가 따로 담당합니다.")]
    public float rangedAttackRange = 8f;
    [Tooltip("AI가 '이 거리 안에 들어오면 사격을 시작한다'고 판단하는 거리입니다. 오직 " +
             "사격 여부만 결정하고 이동에는 영향을 주지 않습니다(이동 중이든 정지 중이든 " +
             "이 거리 안이면 쏩니다). Ranged Attack Range(탄환이 실제로 날아가는 거리)보다 " +
             "작거나 같게 설정하세요 - 더 크게 설정하면 탄환이 도달하지 못하는 거리에서 " +
             "쏘려고 시도하게 됩니다.")]
    public float engageRange = 6f;
    [Tooltip("AI가 '이 거리보다 가까워지면 더 이상 접근하지 않고 제자리에 멈춘다'고 " +
             "판단하는 거리입니다(추격을 멈추는 거리 - 사격 여부는 위 Engage Range가 " +
             "따로 담당). Engage Range보다 작거나 같게 설정하세요 - Engage Range 밖에서는 " +
             "애초에 사격을 하지 않으므로, Chase Range가 Engage Range보다 크면 '사격도 안 " +
             "하면서 계속 다가가기만 하는' 구간이 생깁니다. Retreat Range보다는 충분히 커야 " +
             "'접근 -> 제자리+사격 -> 후퇴' 세 구간이 자연스럽게 나뉩니다.")]
    public float chaseRange = 5f;
    [Tooltip("플레이어와의 거리가 이 값보다 가까워지면 후퇴를 시도합니다. Chase Range보다 " +
             "충분히 작게 설정해야 '접근 -> 제자리+사격 -> 후퇴' 간격이 자연스럽습니다.")]
    public float retreatRange = 4f;
    [Tooltip("후퇴가 시작된 직후부터 최소 이 시간(초) 동안은 플레이어와의 거리와 무관하게 " +
             "무조건 후퇴를 계속합니다(EnemyRangedAIController.retreatDuration에 그대로 " +
             "대입됩니다 - 이 값은 EnemyStats가 아니라 그 컴포넌트 자신의 필드입니다).")]
    public float retreatDuration = 0.5f;
    [Tooltip("초당 발사 가능 횟수입니다 (값이 높을수록 다음 발사를 시작할 때까지의 대기시간이 " +
             "짧아집니다 - 연사 안에서 발사되는 개별 틱 사이의 간격은 아래 Burst Interval이 " +
             "따로 담당합니다).")]
    public float rangedAttackSpeed = 1f;

    [Header("연사 (한 번의 공격 시점에 몇 '틱'을 시간차로 쏠지 - 플레이어 연사와 동일한 개념)")]
    [Tooltip("1이면 지금까지와 동일하게 한 틱만 발사합니다.")]
    public int burstCount = 1;
    [Tooltip("연사 중 틱 사이의 발사 간격(초)입니다. Burst Count가 1이면 사용되지 않습니다.")]
    public float burstInterval = 0.08f;

    [Header("샷건 (한 틱마다 부채꼴로 몇 발을 동시에 쏠지)")]
    [Tooltip("1이면 지금까지와 동일하게 중앙 방향 한 발만 나갑니다. 예) 3발+총합 30도 -> " +
             "-15/0/+15도. 5발+총합 60도 -> -30/-15/0/+15/+30도.")]
    public int pelletCount = 1;
    [Tooltip("가장 왼쪽 탄환과 가장 오른쪽 탄환 사이의 전체 각도(도)입니다. Pellet Count가 " +
             "1이면 사용되지 않습니다.")]
    public float totalSpreadAngle = 0f;

    public Type ExpectedComponentType => typeof(EnemyRangedAttack);

    public void ApplyTo(EnemyStats stats, GameObject enemyGO)
    {
        stats.rangedDamage = rangedDamage;
        stats.rangedProjectileSize = rangedProjectileSize;
        stats.rangedProjectileSpeed = rangedProjectileSpeed;
        stats.rangedAttackRange = rangedAttackRange;
        stats.engageRange = engageRange;
        stats.chaseRange = chaseRange;
        stats.retreatRange = retreatRange;
        stats.rangedAttackSpeed = rangedAttackSpeed;

        // retreatDuration은 EnemyStats가 아니라 EnemyRangedAIController 자신의 필드라
        // stats가 아니라 enemyGO에서 직접 컴포넌트를 찾아 대입합니다(이 파일 상단
        // "[변경 - Retreat Duration도 이 에셋에서 설정 가능]" 주석 참고). 연사/샷건
        // 설정과는 별도이므로, 아래 EnemyRangedAttack 유무와 무관하게 먼저 적용합니다.
        var rangedAI = enemyGO.GetComponent<EnemyRangedAIController>();
        if (rangedAI != null)
        {
            rangedAI.retreatDuration = retreatDuration;
        }
        else
        {
            Debug.LogWarning($"ShootAttackConfig: '{enemyGO.name}'에서 EnemyRangedAIController를 찾지 " +
                              "못해 Retreat Duration을 적용하지 못했습니다. 프리팹에 " +
                              "EnemyRangedAIController가 미리 붙어있는지 확인하세요(wallLayerMask/" +
                              "losOrigin을 연결하려면 원래도 미리 붙어있어야 합니다).");
        }

        var attack = enemyGO.GetComponent<EnemyRangedAttack>();
        if (attack == null)
        {
            Debug.LogWarning($"ShootAttackConfig: '{enemyGO.name}'에서 EnemyRangedAttack을 찾지 못해 " +
                              "연사/샷건 수치를 적용하지 못했습니다. 프리팹에 EnemyRangedAttack이 붙어있는지 확인하세요.");
            return;
        }

        attack.burstCount = burstCount;
        attack.burstInterval = burstInterval;
        attack.pelletCount = pelletCount;
        attack.totalSpreadAngle = totalSpreadAngle;
    }
}