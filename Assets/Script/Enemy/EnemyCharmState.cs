using UnityEngine;

// [38번 패시브] "플레이어 총알에 맞으면 매료된다" 디버프 하나를 이 적에게서 전담해서
// 관리하는 컴포넌트입니다. EnemySpeedModifier와 동일하게 프리팹에 미리 붙여둘 필요 없이
// GetOrAdd()로 필요할 때(=이 적이 처음으로 총알에 맞을 때) 자동으로 붙습니다.
//
// [매료 규칙 - 형빈 기획]
// - 매료되면 1.5초 동안 이동속도 감소(EnemySpeedModifier에 "charm" 배율로 등록) + (보스가
//   아니면) 새 스킬/사격 개시 금지 + 원거리 적은 후퇴 대신 무조건 접근합니다.
// - 매료가 걸리는 즉시, 보스가 아니라면 지금 진행 중이던 공격(텔레그래프/돌진/연사)을
//   즉시 강제로 중단시킵니다(ICancellableAttack.CancelAttack / EnemyRangedAttack.CancelBurst).
// - 보스는 이동속도 감소만 적용되고 스킬/사격은 전혀 억제되지 않습니다 - 단, "한 번
//   매료되면 끝난 후 3초는 재매료되지 않는다"는 타이밍 자체는 보스에게도 동일하게
//   적용됩니다(그 3초 면역 동안엔 보스도 감속이 없습니다 - 형빈 확인).
// - 이미 매료 중일 때 다시 총알에 맞아도 지속시간은 갱신하지 않고 무시합니다(형빈 확인).
// - 매료가 끝나면 3초의 재매료 면역이 시작되고, 그 동안엔 감속이 적용되지 않습니다.
//
// [매료 색 변화 - 발광 없는 단색, 매 프레임 LateUpdate에서 강제 고정 (형빈 확인/버그 수정)]
// 처음엔 매료 중 색을 EnemyHealth의 피격 페이드 시스템(IEnemyColorOverride의
// HasColorOverride/OverrideColor/OverrideEmission)에 맡겨 "다음 목표 색"으로만 알려주는
// 방식으로 구현했습니다. 그런데 이 방식은 총알에 맞을 때마다 EnemyHealth.TakeDamage()가
// 그 즉시 색/발광을 무조건 흰색(hitColor)+발광으로 스냅해버리고 나서야 서서히(약 0.4초)
// 목표 색으로 되돌아가는 구조라, 연사 무기로 계속 맞는(=매료된 적은 플레이어에게 바짝
// 붙어 있어 계속 맞기 쉬운) 상황에서는 그 0.4초가 채 지나기 전에 다음 총알이 또 맞아
// 매번 흰색으로 다시 스냅되기를 반복 - 결과적으로 보라색이 아니라 흰색으로 계속 "발광"한
// 채로 멈춰있는 것처럼 보였습니다(형빈 리포트). 매료가 끝난 뒤에도 플레이어가 계속 쏘고
// 있으면 같은 이유로 계속 흰색이 유지됐습니다.
//
// 그래서 EnemyHealth의 공용 피격 플래시 로직은 전혀 건드리지 않고(다른 모든 적/다른
// 공격의 히트 피드백에 영향 없음), 대신 이 컴포넌트가 매 프레임 LateUpdate()에서
// "이번 프레임에 피격 플래시가 색을 뭘로 바꿔놨든 상관없이 마지막에 다시 덮어쓰는"
// 방식으로 바꿨습니다. LateUpdate는 Unity 실행 순서상 모든 오브젝트의 Update()(=
// EnemyHealth의 피격 플래시 포함)가 끝난 뒤에 항상 실행되므로, 매료 중에는 총알을 아무리
// 빨리 연속으로 맞아도 그 프레임의 마지막 색은 항상 발광 없는 평범한 보라색으로
// 고정되고, 매료가 끝나는 즉시(다음 프레임부터) 더 이상 덮어쓰지 않아 원래 색으로
// 돌아갑니다.
//
// [두 번째 버그 수정 - "원래 색"을 이 컴포넌트 스스로 잘못 캐시하던 문제]
// 위 LateUpdate 수정 이후에도 여전히 보라색 대신 흰색이 보이고, 매료가 끝나도 흰색으로
// 남아있는 문제가 있었습니다(형빈 재확인). 원인은 "원래 색/발광"을 이 컴포넌트가 직접
// 캐시하는 타이밍이었습니다 - 이 컴포넌트는 이 적이 총알에 맨 처음 맞는 바로 그 순간에야
// AddComponent로 붙는데, 그 시점엔 이미 같은 프레임 안에서 EnemyHealth.TakeDamage()가
// 먼저 실행되어 렌더러 색을 hitColor(피격 플래시, 흰색)로 스냅해둔 뒤입니다. 그래서
// Awake()에서 렌더러 색을 다시 읽어 "원래 색"으로 캐시하면, 실제로는 "방금 스냅된 흰색
// 피격 플래시"를 원래 색으로 잘못 저장하게 됩니다 - ApplyTint()가 매료 색으로 바꿔도
// 발광(_EmissionColor)은 이 잘못된 "원래 발광"(=흰색 피격 발광)으로 계속 되돌려졌고,
// 매료가 끝나면 이 잘못된 "원래 색"(=흰색)으로 되돌아갔습니다. 그래서 이제 이 컴포넌트는
// 렌더러 색을 스스로 읽지 않고, 스폰 시점(전투 시작 전)에 이미 정확히 캐시해둔
// EnemyHealth.OriginalColor/OriginalEmission을 그대로 가져다 씁니다(아래 Awake() 참고).
//
// [다른 공격의 색 연출과 충돌 방지 - 우선순위]
// 보스는 매료 중에도 스킬/사격이 억제되지 않으므로, 매료된 채로 동시에 돌진/전방위 공격을
// 텔레그래프할 수 있습니다. 이때 그 공격의 텔레그래프 색(예: 주황색)이 매료의 보라색보다
// 우선해야 한다는 것이 형빈이 이전에 확인한 규칙입니다. 그래서 같은 오브젝트의 다른
// IEnemyColorOverride 구현체(EnemyMeleeAttack/EnemyChainDashAttack/EnemyRadialAttack)가
// 지금 자기 색을 표시 중이면, 이 컴포넌트는 LateUpdate에서 색을 아예 건드리지 않고
// 완전히 양보합니다(강제로 원래 색으로 되돌리지도 않습니다 - 그 공격이 이미 자기 색으로
// 칠해둔 것을 실수로 덮어쓰지 않기 위함). 그 공격이 끝나면 다음 프레임부터 다시 매료
// 색을 정상적으로 고정합니다.
//
// [제어 2단계 개편 - 매료는 "일반 제어" (형빈 기획)]
// 제어 효과는 이제 EnemyControlState 한 곳에서 등급(강력/일반)별로 관리합니다(그 파일 상단 규칙
// 참고). 매료는 "일반 제어"입니다.
// - 강력 제어(수면/해킹) 중인 적에게는 매료가 아예 걸리지 않고, 이때는 재매료 면역도 생기지
//   않습니다(TryApplyCharm이 EnemyControlState.BeginNormal에 먼저 물어봄).
// - 매료 중에 강력 제어가 걸리면 매료는 즉시 끝나고, 자연 종료와 똑같이 재매료 면역 + 사격/스킬
//   유예가 적용됩니다(ForceEndByControl).
// - 매료 중 또는 재매료 면역 중에 다시 매료가 걸리면 지금처럼 무시합니다.
// - 매료 색은 더 이상 이 컴포넌트가 직접 칠하지 않습니다 - BeginNormal에 색을 넘겨두면
//   EnemyControlState가 "강력 제어 색 > 매료 색 > 원래 색" 순서로 한 곳에서 칠합니다. 위의
//   색 관련 설명(LateUpdate 강제 고정/원래 색 캐시 버그/충전 색 양보)은 그대로
//   EnemyControlState로 옮겨졌습니다.
[DisallowMultipleComponent]
public class EnemyCharmState : MonoBehaviour
{
    private const string SpeedModifierSourceId = "charm";

    private float charmTimer;
    private float immunityTimer;
    private float pendingImmunityDuration;

    // 매료가 자연스럽게 끝난 뒤 "사격/스킬 개시 금지"만 조금 더 연장하는 유예 타이머입니다.
    // (이동 강제 접근(SuppressesMovement)에는 영향을 주지 않습니다 - 매료가 끝나면 접근은
    // 즉시 멈춥니다.) TryApplyCharm() 호출 시점에 넘어온 postCharmAttackDelay 값을
    // pendingPostCharmDelay에 담아뒀다가, 매료가 끝나는 순간 postCharmDelayTimer로
    // 옮겨 담아 카운트다운을 시작합니다.
    private float postCharmDelayTimer;
    private float pendingPostCharmDelay;

    private bool isBoss;

    private EnemySpeedModifier speedModifier;
    private ICancellableAttack cancellableAttack;
    private EnemyRangedAttack rangedAttack;
    private EnemyHealth health;

    // 매료 색/우선순위는 EnemyControlState가 관리합니다(파일 상단 [제어 2단계 개편] 참고).
    private EnemyControlState controlState;

    public bool IsCharmed => charmTimer > 0f;

    // [죽은 적 처리 공통] 죽은 뒤에도 Destroy(gameObject)가 처리되기 전까지 이 컴포넌트/
    // AIController들의 Update()가 계속 돌아가는데, 매료가 계속 "억제 중"이라고 보고하면
    // AIController가 죽은 적을 계속 억지로 움직이려다 NavMeshAgent 오류가 나거나 죽은
    // 채로 계속 행동하는 문제가 있어 죽은 뒤엔 항상 false를 돌려줍니다.
    private bool IsAliveAndNotBoss => !isBoss && (health == null || !health.IsDead);

    // "원거리 적이 후퇴 대신 접근해야 하는가"만을 나타냅니다. 매료가 끝나면 유예 없이
    // 즉시 false가 되어야 하므로(형빈 확인 - 매료 종료 후 유예는 공격에만 적용), IsCharmed에만
    // 묶여 있습니다.
    public bool SuppressesMovement => IsCharmed && IsAliveAndNotBoss;

    // "새 스킬/사격 개시를 금지해야 하는가"입니다. 매료 중이거나, 매료가 막 끝난 뒤의
    // 유예시간(postCharmDelayTimer) 안이면 true입니다 - 원거리 적이 매료로 플레이어에게
    // 바짝 붙은 채로 매료가 풀리자마자 영거리 사격을 하는 것을 막기 위한 유예입니다.
    public bool SuppressesBehavior => (IsCharmed || postCharmDelayTimer > 0f) && IsAliveAndNotBoss;

    // EnemyAIController/EnemyRangedAIController가 매 프레임 호출하는 정적 유틸리티입니다
    // (EnemyDetectRangeOverride.Resolve 등과 동일한 호출 방식). 이 컴포넌트가 아직
    // 붙어있지 않은(=한 번도 매료된 적 없는) 적은 항상 false입니다.
    public static bool SuppressesMovementFor(GameObject enemy)
    {
        if (enemy == null) return false;
        var state = enemy.GetComponent<EnemyCharmState>();
        return state != null && state.SuppressesMovement;
    }

    // (인스턴스 프로퍼티 SuppressesBehavior와 이름이 겹쳐 컴파일 에러가 나서 Suppresses로
    // 이름을 바꿨습니다 - C#은 정적/인스턴스 여부와 무관하게 프로퍼티와 메서드가 같은
    // 이름을 가질 수 없습니다.)
    public static bool Suppresses(GameObject enemy)
    {
        if (enemy == null) return false;
        var state = enemy.GetComponent<EnemyCharmState>();
        return state != null && state.SuppressesBehavior;
    }

    // 이 적에게 붙어있는 부품을 돌려주고, 없으면 새로 붙여서 돌려줍니다(EnemySpeedModifier와
    // 동일한 패턴).
    public static EnemyCharmState GetOrAdd(GameObject enemy)
    {
        if (enemy == null) return null;

        var existing = enemy.GetComponent<EnemyCharmState>();
        if (existing != null) return existing;

        return enemy.AddComponent<EnemyCharmState>();
    }

    void Awake()
    {
        isBoss = GetComponent<BossAIController>() != null;
        speedModifier = EnemySpeedModifier.GetOrAdd(gameObject);
        cancellableAttack = GetComponent<IEnemyAttackBehavior>() as ICancellableAttack;
        rangedAttack = GetComponent<EnemyRangedAttack>();
        health = GetComponent<EnemyHealth>();

        controlState = EnemyControlState.GetOrAdd(gameObject);
    }

    // Item38effect(BulletHitCharmEffect)가 플레이어 총알이 이 적에게 명중할 때마다
    // 호출합니다. 면역 중이거나 이미 매료 중이면 아무 일도 하지 않습니다. 색상/배율/유예시간
    // 값은 전부 아이템 에셋 쪽(BulletHitCharmEffect)에서 인스펙터로 조절하도록 매개변수로
    // 받습니다 - 이 컴포넌트는 프리팹이 아니라 게임 도중 AddComponent로 붙는 런타임 전용
    // 컴포넌트라 자체 필드로는 인스펙터에서 편집할 수 없기 때문입니다.
    //
    // [죽은 적 처리] 이 적을 죽인 바로 그 총알이라도(TakeDamage 직후 이 호출이 옴) 이미
    // 죽은 상태라면 매료를 걸지 않습니다 - 죽은 적에게 감속/공격 억제를 걸어봐야 의미가
    // 없고, AIController들이 죽은 뒤에도 계속 억지로 움직이려는 원인이 되기 때문입니다.
    //
    // [6번 패시브 연동 - 제어 지속시간 증가] 실제로 몇 초 동안 매료 상태를 유지할지는
    // 호출자(BulletHitCharmEffect 등)가 넘겨준 duration을 그대로 쓰지 않고, 반드시
    // EnemyControlDurationBonus.Resolve()를 거칩니다 - 매료는 "적의 행동을 일정 시간
    // 억제하는" 제어 스킬의 대표 사례라, 6번(제어 스킬 지속시간 +1초) 등 앞으로 등록되는
    // 모든 제어 지속시간 보너스가 자동으로 합산 적용됩니다(등록된 게 없으면 원래 duration
    // 그대로 - EnemyDetectRangeOverride 등과 동일한 "무등록 시 원래 동작과 100% 동일"
    // 원칙). 앞으로 새 제어 스킬(빙결 등)을 추가할 때도, 그 스킬이 자기
    // 지속시간을 확정하는 바로 이 지점에서 동일하게 Resolve()를 거쳐야 6번의 혜택을
    // 받습니다 - 자동으로 모든 디버프에 적용되는 것이 아니라 각 제어 스킬이 직접
    // 참여해야 하는 opt-in 구조입니다.
    //
    // [반환값 - 매료 지속 이펙트 연동] 이번 호출로 매료가 "실제로 새로 걸렸으면" true, 아무것도
    // 하지 않았으면(죽음/이미 매료 중/재매료 면역 중/강력 제어 중) false를 돌려줍니다. 호출한 쪽
    // (38번)은 true일 때만 하트 지속 이펙트를 붙입니다 - 31번/해킹툴이 TryApplyStrong()의 결과가
    // true일 때만 이펙트를 붙이는 것과 같은 원칙입니다. 매료 동작 자체는 반환값 추가 전과 완전히
    // 같습니다.
    public bool TryApplyCharm(float duration, float immunityDuration, float speedMultiplier, Color color, float postCharmAttackDelay)
    {
        if (health != null && health.IsDead) return false;
        // 매료 중이거나 재매료 면역 중이면 무시합니다(기존 규칙 그대로).
        if (immunityTimer > 0f || IsCharmed) return false;

        // [제어 2단계 개편] 일반 제어 칸에 매료를 등록합니다. 강력 제어(수면/해킹) 중이면
        // false가 돌아오고, 이때는 아무것도 적용하지 않습니다 - 재매료 면역도 생기지 않습니다.
        if (controlState == null) controlState = EnemyControlState.GetOrAdd(gameObject);
        if (controlState == null || !controlState.BeginNormal(EnemyControlType.Charm, color, ForceEndByControl)) return false;

        charmTimer = EnemyControlDurationBonus.Resolve(duration);
        pendingImmunityDuration = immunityDuration;
        pendingPostCharmDelay = postCharmAttackDelay;

        speedModifier.SetMultiplier(SpeedModifierSourceId, speedMultiplier);

        if (!isBoss)
        {
            cancellableAttack?.CancelAttack();
            rangedAttack?.CancelBurst();
        }

        // 색은 EnemyControlState가 이번 프레임의 LateUpdate에서 확정합니다.
        // (지속시간이 0 이하로 설정된 경우까지 고려해, "지금 실제로 매료 중인지"를 그대로 돌려줍니다.)
        return IsCharmed;
    }

    // [제어 2단계 개편] 강력 제어(수면/해킹)가 걸리거나 다른 일반 제어로 교체될 때
    // EnemyControlState가 호출합니다. 매료가 "자연 종료"될 때와 동일한 후처리(재매료 면역 시작 +
    // 사격/스킬 개시 유예 시작)를 그대로 적용합니다(형빈 확정). 지금 매료 중이 아니면 아무 일도
    // 하지 않습니다.
    private void ForceEndByControl()
    {
        if (!IsCharmed) return;

        charmTimer = 0f;
        speedModifier.RemoveMultiplier(SpeedModifierSourceId);
        immunityTimer = pendingImmunityDuration;
        postCharmDelayTimer = pendingPostCharmDelay;
    }

    void Update()
    {
        // 매료 도중 적이 죽으면(다른 피해원 등으로) 더 끌 이유가 없으니 즉시 감속을
        // 해제하고 정리합니다(색 되돌리기는 EnemyControlState가 처리합니다). 죽은 뒤에도
        // 이 컴포넌트는 Destroy(gameObject)가 처리될 때까지 계속 Update()가 돌므로, 여기서
        // 스스로 정리하지 않으면 감속이 그대로 남아있는 채로 디졸브 연출이 진행됩니다.
        if (health != null && health.IsDead)
        {
            if (charmTimer > 0f)
            {
                charmTimer = 0f;
                speedModifier.RemoveMultiplier(SpeedModifierSourceId);
            }
            controlState?.EndNormal(EnemyControlType.Charm);
            postCharmDelayTimer = 0f;
            return;
        }

        if (charmTimer > 0f)
        {
            charmTimer -= Time.deltaTime;
            if (charmTimer <= 0f)
            {
                charmTimer = 0f;
                speedModifier.RemoveMultiplier(SpeedModifierSourceId);
                immunityTimer = pendingImmunityDuration;
                postCharmDelayTimer = pendingPostCharmDelay;
                controlState?.EndNormal(EnemyControlType.Charm);
            }
        }
        else
        {
            // 사격/스킬 유예와 재매료 면역은 서로 다른 두 타이머로, 매료가 끝난 순간
            // 동시에 시작해 각자 독립적으로 줄어듭니다.
            if (postCharmDelayTimer > 0f) postCharmDelayTimer -= Time.deltaTime;
            if (immunityTimer > 0f) immunityTimer -= Time.deltaTime;
        }
    }
}
