using UnityEngine;

// "지금 돌진(Dash) 중인지"와 "돌진 중일 때 적용할 접촉 데미지가 얼마인지"를 표현하는
// 아주 작은 인터페이스입니다. EnemyMeleeContactTrigger가 접촉 데미지를 계산할 때, 자기
// 주인이 정확히 어떤 클래스인지는 몰라도 되고 이 인터페이스만 구현하고 있으면 됩니다.
//
// [왜 필요한가]
// 원래 EnemyMeleeContactTrigger는 EnemyMeleeAttack이라는 구체 클래스를 직접 참조해서
// IsDashing/dashDamage를 읽었습니다. 돌진하는 적 종류가 EnemyMeleeAttack 하나뿐일 때는
// 문제가 없었지만, EnemyChainDashAttack(연속 돌진)처럼 "돌진"이라는 같은 개념을 가진 다른
// 컴포넌트가 추가되면서, 트리거가 그 새 클래스는 전혀 인식하지 못하는 문제가 생겼습니다
// (C#은 멤버 이름이 같다고 같은 타입으로 취급하지 않습니다 - 완전히 다른 클래스로 봅니다).
// 이 인터페이스를 대신 참조하게 하면, 앞으로 어떤 새 돌진형 공격 컴포넌트가 추가되든
// (IEnemyAttackConfig/IEnemyAttackBehavior 확장 패턴과 동일하게) 이 인터페이스만 구현하면
// EnemyMeleeContactTrigger 쪽 코드는 전혀 손댈 필요가 없습니다.
//
// [주의 - 평소 접촉 데미지와는 완전히 다른 개념]
// 이 인터페이스는 "돌진 중일 때 얼마를 줄지"만 알려줄 뿐입니다. 돌진 중이 아닐 때 적용되는
// 평소 접촉 데미지(EnemyStats.attackPower)와는 무관하며, 그 판단은 여전히
// EnemyMeleeContactTrigger가 직접 담당합니다(IsDashing이 false거나 이 인터페이스 자체가
// 없으면 평소처럼 attackPower를 씁니다).
public interface IDashDamageSource
{
    // 지금 이 순간 실제로 돌진해서 밀고 나가는 중인지 여부입니다 (충전/쿨다운/평소 이동
    // 중에는 false).
    bool IsDashing { get; }

    // 돌진 중(IsDashing == true)일 때 접촉 데미지로 사용할 값입니다. 평소 접촉 데미지와는
    // 별개의 값이며, 실제 값은 각 공격 방식의 config(DashAttackConfig, ChainDashAttackConfig
    // 등)가 스폰 시점에 채워줍니다.
    float DashDamage { get; }
}