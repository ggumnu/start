using UnityEngine;

// 피격 시 원래 색으로 돌아오는 EnemyHealth의 페이드가, "지금은 원래 색이 아니라 다른 색을
// 유지해야 하는" 상태(돌진/전방위 공격 등의 충전-텔레그래프 색)를 무시하고 진짜 원래 색으로
// 덮어써버리는 문제를 막기 위한 인터페이스입니다.
//
// [배경 - 충전 중 피격 시 색이 깜빡 원래색으로 돌아왔다 사라지는 문제]
// EnemyHealth는 피격 시 hitColor로 스냅했다가 flashFadeDuration에 걸쳐 자신이 Awake()에서
// 캐시해둔 "진짜 원래 색"으로 서서히 되돌립니다. 그런데 돌진/전방위 공격 등은 충전
// (텔레그래프) 중에 자기 색을 계속 다른 색(예: 주황색)으로 유지하는데, 이 둘이 서로 모르고
// 매 프레임 서로의 SetColor를 덮어쓰다가 결국 EnemyHealth의 페이드가 "진짜 원래 색"으로
// 완전히 끝나버려, 충전이 끝나지도 않았는데 색이 원래색으로 돌아와 보였습니다.
//
// EnemyHealth는 Awake()에서 자신과 같은 오브젝트에 있는 이 인터페이스 구현체를 전부
// 찾아두고(GetComponents), 피격 페이드가 매 프레임 되돌아가야 할 목표 색을 계산할 때
// HasColorOverride가 true인 구현체가 하나라도 있으면 그 OverrideColor/OverrideEmission을
// 목표로 쓰고, 없으면(=아무도 색을 오버라이드하고 있지 않으면) 원래대로 자신이 캐시해둔
// 진짜 원래 색으로 되돌아갑니다. 돌진/전방위 공격 컴포넌트(EnemyMeleeAttack/
// EnemyChainDashAttack/EnemyRadialAttack)가 이 인터페이스를 구현합니다.
public interface IEnemyColorOverride
{
    // 지금 이 컴포넌트가 색을 원래 색이 아닌 다른 색으로 유지하고 있어야 하는 상태인지
    // 여부입니다. true인 동안에는 EnemyHealth의 피격 페이드가 진짜 원래 색이 아니라
    // OverrideColor/OverrideEmission으로 되돌아갑니다.
    bool HasColorOverride { get; }

    // HasColorOverride가 true인 동안 유지해야 할 _BaseColor/_Color 값입니다.
    Color OverrideColor { get; }

    // HasColorOverride가 true인 동안 유지해야 할 자체발광(Emission) 값입니다.
    // (해당 컴포넌트가 Emission을 쓰지 않는다면 아무 의미 없는 값이라도 무방합니다 -
    // EnemyHealth 쪽에서도 자신이 usesEmission일 때만 이 값을 사용합니다.)
    Color OverrideEmission { get; }
}
