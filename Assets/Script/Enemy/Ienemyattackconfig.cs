using UnityEngine;

// EnemyDefinition이 담는 "공격 방식별 세부 스탯" 데이터의 공통 인터페이스입니다.
// IEnemyAttackBehavior(컴포넌트가 실제로 어떻게 행동하는지)와는 다른 레이어입니다 -
// 이쪽은 순수 데이터(SerializeReference로 다형성 직렬화됨)이고, 그 데이터를 실제
// 컴포넌트(EnemyMeleeAttack, EnemyRangedAttack 등)에 적용하는 역할만 합니다.
//
// [왜 이렇게 나눴는가]
// 예전엔 EnemyDefinition 하나에 근접/원거리 스탯 필드를 전부 나열해서, 근접 적을 만들
// 때도 원거리 필드가 인스펙터에 그대로 보이는 문제가 있었습니다. EnemyDefinition에
// [SerializeReference] IEnemyAttackConfig 필드 하나만 두고 그 안에서 구체적인 타입
// (DashAttackConfig, ShootAttackConfig 등)을 고르면, 유니티 인스펙터가 그 타입의
// 필드만 보여줍니다. 새 공격 방식이 추가돼도 이 인터페이스를 구현하는 클래스만 새로
// 만들면 되고, EnemyDefinition/RealEnemySpawner 코드는 전혀 손댈 필요가 없습니다.
public interface IEnemyAttackConfig
{
    // 이 설정이 실제로 동작하려면 프리팹에 어떤 컴포넌트가 붙어있어야 하는지입니다.
    // EnemyDefinition.OnValidate()가 이 값으로 "설정 타입"과 "실제 연결된 프리팹"이
    // 서로 맞는지 검증해 어긋나면 콘솔에 에러를 띄웁니다.
    System.Type ExpectedComponentType { get; }

    // 스폰 시점에 이 설정의 값들을 실제 컴포넌트(그리고 필요하다면 공용 버스인
    // EnemyStats)에 적용합니다. RealEnemySpawner.SpawnOne()이 stats.ApplyFrom(definition)
    // 직후에 호출합니다.
    void ApplyTo(EnemyStats stats, GameObject enemyGO);
}