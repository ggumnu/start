using System;
using UnityEngine;

// 적의 "공격 상태(Attack)"에서 실제로 무엇을 하는지를 표현하는 인터페이스입니다.
// EnemyAIController는 Idle/Chase 상태 전환과 "지금 공격을 시작해도 되는지" 판단만 담당하고,
// 공격 상태에 진입했을 때 정확히 무슨 동작을 할지는 이 인터페이스를 구현하는 컴포넌트
// (EnemyMeleeAttack 등)가 전담합니다. 나중에 새 근접형 공격 방식을 추가할 때도 이 인터페이스만
// 구현하면 EnemyAIController 쪽 코드는 전혀 손댈 필요가 없습니다. IRoomObjective/IEnemySpawner와
// 동일한 "교체 가능한 조각은 인터페이스로 분리" 패턴입니다.
public interface IEnemyAttackBehavior
{
    // AIController가 Chase 상태에서 이 거리 이내로 플레이어가 들어오면 공격 시작을 시도합니다.
    float AttackRange { get; }

    // [v1.9] "지금 이 순간 실제로 공격 동작(텔레그래프/돌진 등 방식별로 다른 실행 시퀀스)을
    // 수행 중이라 EnemyAIController가 이동+회전에서 완전히 손을 떼야 하는지" 여부입니다.
    // true인 동안 EnemyAIController.Update()는 조기 리턴하고, 이동/회전을 포함한 모든 것을
    // 이 컴포넌트가 직접 책임집니다. 쿨다운처럼 "아직 다음 공격은 못 하지만 평소처럼 접근은
    // 해도 되는" 구간은 여기 포함시키지 않습니다 (그런 구간은 CanBeginAttack이 false인 것으로
    // 표현하고, IsAttacking은 false로 두어 컨트롤러가 계속 접근하도록 둡니다).
    bool IsAttacking { get; }

    // [v1.9 추가] 지금 새로운 공격을 "시작"해도 되는지 여부입니다. IsAttacking과는 독립적인
    // 개념입니다 - 예를 들어 돌진 쿨다운 중에는 IsAttacking은 false(컨트롤러가 이동을 관리해도
    // 됨)이지만 CanBeginAttack도 false라서, 컨트롤러가 사거리 안이라고 바로 또 BeginAttack을
    // 호출해버리는 일을 막습니다. 사거리 판정(AttackRange)과 별개로, "지금 시작 가능한 상태인지"
    // 만 담당합니다.
    bool CanBeginAttack { get; }

    // 공격 시퀀스가 완전히 끝났을 때 호출됩니다. v1.9부터 EnemyAIController는 자체 상태를
    // 매 프레임 IsAttacking/CanBeginAttack/거리로 다시 계산하기 때문에 이 이벤트를 상태 전환
    // 트리거로 사용하지 않지만, 추후 공격 종료 시점에 반응해야 하는 다른 기능(사운드, 이펙트 등)을
    // 붙일 자리로 인터페이스에는 계속 남겨둡니다.
    event Action OnAttackFinished;

    void Initialize(EnemyStats stats, Transform self);

    // AIController가 공격 시작 조건(사거리 안 + CanBeginAttack)을 모두 만족했을 때 호출합니다.
    // targetPosition은 호출 시점의 플레이어 위치입니다 (실시간 추적이 아니라 스냅샷).
    //
    // [v1.10 변경 - 반환값 추가] 예전엔 반환값이 없어 컨트롤러가 무조건 공격이 시작된다고
    // 가정했습니다. 하지만 골목처럼 이 적이 물리적으로 도달할 수 없는 위치(NavMesh가 없는
    // 구역)로 targetPosition이 주어지면, 억지로 공격 시퀀스를 시작해봤자 목표에 닿지 못한 채
    // 벽/네브메시 경계에서 하염없이 부딪히거나 미끄러지기만 합니다. 이제 구현체는 목표가
    // 도달 불가능하다고 판단되면 false를 반환해 공격을 시작하지 않을 수 있고, 컨트롤러는
    // false를 받으면 즉시 평소 접근(SetDestination) 행동으로 되돌아갑니다 - NavMeshAgent의
    // 정상적인 길찾기는 도달 불가능한 목적지에도 가장 가까운 지점까지 얌전히 걸어가 멈추므로,
    // 억지 돌진보다 훨씬 자연스럽습니다.
    bool BeginAttack(Vector3 targetPosition);
}