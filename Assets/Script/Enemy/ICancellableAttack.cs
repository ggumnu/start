// [38번 패시브 연동] "지금 진행 중인 공격 시퀀스(텔레그래프/돌진/연사 등)를 즉시 강제로
// 중단시킬 수 있는지"를 표시하는 선택적 확장 인터페이스입니다. IDelayedActiveItemEffect가
// IActiveItemEffect를 선택적으로 확장하는 것과 동일한 패턴입니다 - IEnemyAttackBehavior를
// 구현하는 컴포넌트 중, "진행 중인 실행 구간을 즉시 끊어도 안전한" 것들만 이 인터페이스를
// 추가로 구현합니다(예: EnemyMeleeAttack, EnemyChainDashAttack). EnemyContactOnlyAttack처럼
// 애초에 강제로 끊어야 할 "실행 구간"이 없는 경우는 구현하지 않아도 무방합니다 - 호출하는
// 쪽(EnemyCharmState)은 항상 as 캐스팅으로 안전하게 확인 후 호출합니다.
public interface ICancellableAttack
{
    // 지금 텔레그래프/돌진 등 진행 중인 구간이 있다면 즉시 중단하고 쿨다운 상태로
    // 전환합니다. 진행 중인 구간이 없다면(phase == None 등) 아무 일도 일어나지 않습니다.
    void CancelAttack();
}
