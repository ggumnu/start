using System;
using UnityEngine;
using UnityEngine.AI;

// 적이 받는 제어 효과(강력 = 수면/해킹, 일반 = 매료)를 한 곳에서 관리합니다(처음 걸릴 때 GetOrAdd로 자동 부착). 등급마다 칸 하나:
// 새 강력 제어는 이전 것을 교체(누적 X)하고 일반 제어를 끝냄, 강력 제어 중엔 일반 제어가 안 걸림. 강력 제어가 걸리면 진행 중 공격을 끊고
// AI는 IsStrongControlledFor 동안 멈춤. 몸 색은 이 컴포넌트만 칠함(공격 충전 색 > 강력 > 일반 > 상태 색 > 원래 색).
// 제어 거르기: 같은 오브젝트에 IEnemyControlFilter가 있으면 강력 제어를 막거나(Block) 표시만(MarkOnly - 칸/색/지속시간은 걸리지만 공격을 끊거나
// 멈추게 하지 않음, 2레벨 보스 보호막의 해킹) 할 수 있습니다. 설계 배경은 Docs/설계노트.md [적 제어].
public enum EnemyControlType
{
    None,
    Charm,
    Sleep,
    Hack,
}

public enum EnemyControlTier
{
    Normal,
    Strong,
}

// 강력 제어를 거르는 결과입니다(IEnemyControlFilter).
public enum EnemyControlFilterResult
{
    Allow,    // 평소대로 걸림
    Block,    // 걸리지 않음(TryApplyStrong이 false)
    MarkOnly, // 칸/색/지속시간만 걸림(41번 같은 "제어 중" 판정과 이펙트는 그대로, 공격을 끊거나 멈추게 하지는 않음 - AI가 스스로 무시해야 함)
}

// 적 오브젝트에 붙어 강력 제어를 거르는 컴포넌트가 구현합니다(2레벨 보스 보호막 - BossShield).
public interface IEnemyControlFilter
{
    EnemyControlFilterResult FilterStrong(EnemyControlType type);
}

[DisallowMultipleComponent]
public class EnemyControlState : MonoBehaviour
{
    // 제어 종류별 등급입니다. 새 제어를 추가하면 여기에 등급만 지정하세요.
    public static EnemyControlTier GetTier(EnemyControlType type)
    {
        switch (type)
        {
            case EnemyControlType.Sleep:
            case EnemyControlType.Hack:
                return EnemyControlTier.Strong;
            default:
                return EnemyControlTier.Normal;
        }
    }

    // ---- 강력 제어 칸 ----
    private EnemyControlType strongType = EnemyControlType.None;
    private float strongTimer;
    private Color strongColor;

    // ---- 잠깐 색 표시 (FlashTint) ----
    private Color flashColor;
    private float flashTimer;
    private float flashFadeTime;
    private float flashBoost = 2f;

    // ---- 상태 색 칸 (91번 오한 등 - 가장 낮은 우선순위) ----
    private readonly System.Collections.Generic.List<string> statusTintOrder = new System.Collections.Generic.List<string>();
    private readonly System.Collections.Generic.Dictionary<string, Color> statusTints = new System.Collections.Generic.Dictionary<string, Color>();

    // ---- 일반 제어 칸 (실제 로직은 해당 제어 컴포넌트가 담당) ----
    private EnemyControlType normalType = EnemyControlType.None;
    private Color normalColor;
    private Action normalForceEnd;

    private EnemyHealth health;
    private NavMeshAgent agent;
    private ICancellableAttack[] cancellableAttacks;
    private IEnemyControlFilter controlFilter;
    private EnemyRangedAttack rangedAttack;

    // ---- 색 ----
    private IEnemyColorOverride[] siblingColorOverrides;
    private Renderer rend;
    private Color originalColor;
    private Color originalEmission;
    private bool rendSupportsColor;
    private bool usesBaseColor;
    private bool usesColor;
    private bool usesEmission;
    private bool tintActive;

    private bool IsDead => health != null && health.IsDead;

    public bool IsStrongControlled => !IsDead && strongType != EnemyControlType.None && strongTimer > 0f;
    public EnemyControlType StrongType => IsStrongControlled ? strongType : EnemyControlType.None;
    public float StrongRemaining => IsStrongControlled ? strongTimer : 0f;
    public EnemyControlType NormalType => normalType;

    // 일반 제어를 새로 걸 수 있는지입니다(살아 있고, 강력 제어 중이 아니어야 함).
    public bool CanBeginNormal => !IsDead && !IsStrongControlled;

    // 근접/원거리/보스 AI가 매 프레임 호출합니다. 이 컴포넌트가 아직 붙지 않은(=제어를 한 번도
    // 받지 않은) 적은 항상 false입니다.
    public static bool IsStrongControlledFor(GameObject enemy)
    {
        if (enemy == null) return false;
        var state = enemy.GetComponent<EnemyControlState>();
        return state != null && state.IsStrongControlled;
    }

    public static EnemyControlState GetOrAdd(GameObject enemy)
    {
        if (enemy == null) return null;

        var existing = enemy.GetComponent<EnemyControlState>();
        if (existing != null) return existing;

        return enemy.AddComponent<EnemyControlState>();
    }

    void Awake()
    {
        health = GetComponent<EnemyHealth>();
        agent = GetComponent<NavMeshAgent>();
        cancellableAttacks = GetComponents<ICancellableAttack>();
        controlFilter = GetComponent<IEnemyControlFilter>();
        rangedAttack = GetComponent<EnemyRangedAttack>();
        siblingColorOverrides = GetComponents<IEnemyColorOverride>();

        if (health != null)
        {
            rend = health.TargetRenderer;
            rendSupportsColor = health.RendSupportsColor && rend != null;
            usesBaseColor = health.UsesBaseColor;
            usesColor = health.UsesColor;
            usesEmission = health.UsesEmission;
            originalColor = health.OriginalColor;
            originalEmission = health.OriginalEmission;
        }
    }

    // 강력 제어(수면/해킹)를 겁니다. baseDuration은 스킬/아이템이 정한 기본 지속시간이고, 실제
    // 적용값은 6번 패시브 보너스가 합산된 값입니다. 이전 강력 제어는 종류와 무관하게 즉시 끝나고
    // 새 지속시간으로 교체되며(누적 없음), 걸려 있던 일반 제어(매료)도 즉시 끝납니다.
    // 실제로 걸렸으면 true를 돌려줍니다.
    public bool TryApplyStrong(EnemyControlType type, float baseDuration, Color color)
    {
        if (IsDead) return false;
        if (type == EnemyControlType.None || GetTier(type) != EnemyControlTier.Strong) return false;

        EnemyControlFilterResult filter = controlFilter != null ? controlFilter.FilterStrong(type) : EnemyControlFilterResult.Allow;
        if (filter == EnemyControlFilterResult.Block) return false;

        float duration = EnemyControlDurationBonus.Resolve(baseDuration);
        if (duration <= 0f) return false;

        // 일반 제어가 걸려 있었다면 즉시 끝냅니다(매료는 면역/유예 포함 - 파일 상단 규칙 2).
        ForceEndNormal();

        strongType = type;
        strongTimer = duration;
        strongColor = color;

        if (filter == EnemyControlFilterResult.MarkOnly) return true; // 공격을 끊거나 멈추게 하지 않음

        // 진행 중이던 공격을 모두 즉시 끊습니다(보스 포함 - 여러 공격 컴포넌트가 있어도 전부).
        if (cancellableAttacks != null)
        {
            for (int i = 0; i < cancellableAttacks.Length; i++)
            {
                cancellableAttacks[i]?.CancelAttack();
            }
        }
        rangedAttack?.CancelBurst();

        // AI의 다음 Update를 기다리지 않고 이 프레임에 바로 멈춥니다.
        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
        }

        return true;
    }

    // 잠깐 발광시킵니다(제어 아님 - 위 [잠깐 발광 표시] 참고). 발광 = color x boost. duration = 전체 시간(초), fadeTime = 끝부분에서
    // 원래 발광으로 돌아가는 시간(초).
    public void FlashGlow(Color color, float boost, float duration, float fadeTime)
    {
        if (IsDead || duration <= 0f) return;
        flashColor = color;
        flashBoost = Mathf.Max(boost, 0f);
        flashTimer = duration;
        flashFadeTime = Mathf.Clamp(fadeTime, 0f, duration);
    }

    // [91번 오한 등] 상태 색을 겁니다(같은 출처면 색만 바꾸고 가장 나중 것으로 취급). 제어 색보다 우선순위가 낮습니다.
    public void SetStatusTint(string sourceId, Color color)
    {
        if (string.IsNullOrEmpty(sourceId)) return;
        statusTints[sourceId] = color;
        statusTintOrder.Remove(sourceId);
        statusTintOrder.Add(sourceId);
    }

    // 상태 색을 풉니다. 걸린 적이 없는 출처여도 안전합니다.
    public void ClearStatusTint(string sourceId)
    {
        if (string.IsNullOrEmpty(sourceId)) return;
        statusTints.Remove(sourceId);
        statusTintOrder.Remove(sourceId);
    }

    // 지금 칠할 상태 색입니다(가장 나중에 건 것). 없으면 false.
    private bool TryGetStatusTint(out Color color)
    {
        color = originalColor;
        if (statusTintOrder.Count == 0) return false;
        return statusTints.TryGetValue(statusTintOrder[statusTintOrder.Count - 1], out color);
    }

    // 일반 제어(매료 등)가 "지금 걸리려 한다"고 알립니다. 강력 제어 중이면 false를 돌려주며,
    // 이때 호출한 쪽은 아무것도 적용하지 않아야 합니다(면역 등도 주지 않음 - 규칙 3). 다른 종류의
    // 일반 제어가 걸려 있었다면 그것을 즉시 끝내고 새 것으로 교체합니다(규칙 4). forceEnd는 이
    // 일반 제어를 바깥에서 강제로 끝내야 할 때(강력 제어가 걸리거나 다른 일반 제어로 교체될 때)
    // 호출할 함수입니다.
    public bool BeginNormal(EnemyControlType type, Color color, Action forceEnd)
    {
        if (!CanBeginNormal) return false;
        if (type == EnemyControlType.None || GetTier(type) != EnemyControlTier.Normal) return false;

        if (normalType != EnemyControlType.None && normalType != type)
        {
            ForceEndNormal();
        }

        normalType = type;
        normalColor = color;
        normalForceEnd = forceEnd;
        return true;
    }

    // 일반 제어가 스스로(자연 종료/적 사망 등) 끝났을 때 호출합니다. 지금 칸에 있는 제어와
    // 종류가 같을 때만 비웁니다.
    public void EndNormal(EnemyControlType type)
    {
        if (normalType != type) return;
        normalType = EnemyControlType.None;
        normalForceEnd = null;
    }

    // 지금 걸린 일반 제어를 바깥에서 강제로 끝냅니다. 칸을 먼저 비운 뒤 종료 함수를 호출해서,
    // 그 함수 안에서 EndNormal()이 다시 불려도 문제가 없게 합니다.
    private void ForceEndNormal()
    {
        if (normalType == EnemyControlType.None) return;

        Action ender = normalForceEnd;
        normalType = EnemyControlType.None;
        normalForceEnd = null;
        ender?.Invoke();
    }

    void Update()
    {
        if (IsDead)
        {
            strongTimer = 0f;
            strongType = EnemyControlType.None;
            return;
        }

        if (flashTimer > 0f)
        {
            flashTimer -= Time.deltaTime;
            if (flashTimer < 0f) flashTimer = 0f;
        }

        if (strongTimer > 0f)
        {
            strongTimer -= Time.deltaTime;
            if (strongTimer <= 0f)
            {
                strongTimer = 0f;
                strongType = EnemyControlType.None;
            }
        }
    }

    private bool IsSiblingOverrideActive()
    {
        if (siblingColorOverrides == null) return false;

        for (int i = 0; i < siblingColorOverrides.Length; i++)
        {
            var o = siblingColorOverrides[i];
            if (o != null && o.HasColorOverride) return true;
        }
        return false;
    }

    private void ApplyTint(Color color)
    {
        if (usesBaseColor) rend.material.SetColor("_BaseColor", color);
        if (usesColor) rend.material.SetColor("_Color", color);
        if (usesEmission) rend.material.SetColor("_EmissionColor", originalEmission);
        tintActive = true;
    }

    // 몸 색은 bodyColor, 발광은 emission으로 칠합니다(잠깐 발광 표시용 - 돌진 충전 발광과 같은 방식으로 _EMISSION을 켬).
    private void ApplyGlow(Color bodyColor, Color emission)
    {
        if (usesBaseColor) rend.material.SetColor("_BaseColor", bodyColor);
        if (usesColor) rend.material.SetColor("_Color", bodyColor);
        if (usesEmission)
        {
            rend.material.EnableKeyword("_EMISSION");
            rend.material.SetColor("_EmissionColor", emission);
        }
        tintActive = true;
    }

    private void RevertTint()
    {
        if (!tintActive) return;

        if (usesBaseColor) rend.material.SetColor("_BaseColor", originalColor);
        if (usesColor) rend.material.SetColor("_Color", originalColor);
        if (usesEmission) rend.material.SetColor("_EmissionColor", originalEmission);
        tintActive = false;
    }

    void LateUpdate()
    {
        // 모든 Update()(피격 플래시 포함)가 끝난 뒤 이 프레임의 최종 색을 확정합니다.
        if (!rendSupportsColor) return;

        // 다른 공격(돌진/전방위 등)의 충전 색이 표시 중이면 완전히 양보합니다.
        if (IsSiblingOverrideActive()) return;

        if (IsDead)
        {
            RevertTint();
            return;
        }

        // [잠깐 발광 표시] 지속 중이면 몸 색은 지금 보여야 할 색(제어 색 또는 원래 색) 그대로, 발광만 올리고 끝부분에서 원래 발광으로
        // 부드럽게 돌아갑니다. 발광 칸이 없는 재질이면 대신 몸 색을 잠깐 바꿉니다.
        if (flashTimer > 0f)
        {
            Color under = IsStrongControlled ? strongColor
                        : normalType != EnemyControlType.None ? normalColor
                        : TryGetStatusTint(out Color statusUnder) ? statusUnder
                        : originalColor;
            float k = flashFadeTime > 0f ? Mathf.Clamp01(flashTimer / flashFadeTime) : 1f;
            if (usesEmission)
            {
                ApplyGlow(under, Color.Lerp(originalEmission, flashColor * flashBoost, k));
            }
            else
            {
                ApplyTint(Color.Lerp(under, flashColor, k));
            }
            return;
        }

        if (IsStrongControlled)
        {
            ApplyTint(strongColor);
        }
        else if (normalType != EnemyControlType.None)
        {
            ApplyTint(normalColor);
        }
        else if (TryGetStatusTint(out Color statusColor))
        {
            ApplyTint(statusColor);
        }
        else
        {
            RevertTint();
        }
    }
}
