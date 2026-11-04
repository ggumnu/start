using UnityEngine;

// [91번 패시브] 적 하나에 걸리는 "오한" 디버프를 전담하는 컴포넌트입니다. 처음 오한이 걸릴 때 GetOrAdd()로 자동으로 붙습니다
// (EnemyCharmState와 같은 패턴 - 프리팹에 미리 붙일 필요 없음).
//
// [오한 규칙 - 형빈 확정]
// - 걸리면 지속시간(기본 3초 + 6번 제어 지속시간 보너스 - EnemyControlDurationBonus) 동안 이동 속도와 공격 속도가 각각 배율(기본 0.5)만큼
//   느려집니다. 공격 속도는 공격과 공격 사이의 대기만 늘어납니다(EnemyAttackSpeedModifier 참고).
// - 오한 중에 다시 걸리면 지속시간이 처음부터 다시 갱신됩니다(누적은 안 됨).
// - 이동 감속은 EnemySpeedModifier에 "chill" 출처로 따로 등록되므로 매료 감속 등 다른 감속과 곱해집니다.
// - 오한은 "제어"(수면/해킹/매료)가 아니라 동시에 걸리는 디버프입니다 - 제어 칸을 차지하지 않고, 수면/해킹/매료 중에도 걸리고 유지됩니다.
// - 보스에게는 걸리지 않습니다(색도 바뀌지 않음).
//
// [색 / 이펙트 - 우선순위]
// 색은 EnemyControlState의 "상태 색" 칸(가장 낮은 우선순위)에 칠해 달라고 맡깁니다:
// 공격 충전 색 > 수면/해킹 색 > 매료 색 > 오한 색 > 원래 색. 그래서 수면/해킹/매료 중에는 오한 색이 안 보이고, 그것들이 끝나면
// (오한이 남아 있으면) 오한 색이 보입니다. 물방울 이펙트도 같은 규칙으로, 제어(수면/해킹/매료)가 걸려 있는 동안에는 숨기고(남은
// 물방울은 끝까지 떨어짐) 제어가 끝나면 다시 나옵니다.
[DisallowMultipleComponent]
public class EnemyChillState : MonoBehaviour
{
    private const string SourceId = "chill";
    private const string VfxKey = "chill";

    private EnemyHealth health;
    private EnemyControlState control;
    private EnemySpeedModifier speedModifier;
    private EnemyAttackSpeedModifier attackSpeedModifier;
    private EnemyStatusVfxSettings vfxSettings;
    private bool isBoss;

    private float timer;
    private bool active;

    public bool IsChilled => active && timer > 0f && (health == null || !health.IsDead);

    public static EnemyChillState GetOrAdd(GameObject enemy)
    {
        if (enemy == null) return null;
        var existing = enemy.GetComponent<EnemyChillState>();
        return existing != null ? existing : enemy.AddComponent<EnemyChillState>();
    }

    void Awake()
    {
        health = GetComponent<EnemyHealth>();
        isBoss = GetComponent<BossAIController>() != null;
    }

    // 오한을 걸거나(이미 걸려 있으면 지속시간을 새로 갱신) 합니다. 보스/죽은 적이면 아무것도 하지 않고 false를 돌려줍니다.
    public bool TryApply(float baseDuration, float moveSpeedMultiplier, float attackSpeedMultiplier, Color color,
                         EnemyStatusVfxSettings statusVfx)
    {
        if (isBoss) return false;
        if (health != null && health.IsDead) return false;

        float duration = EnemyControlDurationBonus.Resolve(baseDuration);
        if (duration <= 0f) return false;

        timer = duration;
        active = true;
        vfxSettings = statusVfx;

        if (speedModifier == null) speedModifier = EnemySpeedModifier.GetOrAdd(gameObject);
        speedModifier?.SetMultiplier(SourceId, moveSpeedMultiplier);

        if (attackSpeedModifier == null) attackSpeedModifier = EnemyAttackSpeedModifier.GetOrAdd(gameObject);
        attackSpeedModifier?.SetMultiplier(SourceId, attackSpeedMultiplier);

        if (control == null) control = EnemyControlState.GetOrAdd(gameObject);
        control?.SetStatusTint(SourceId, color);

        UpdateVfx();
        return true;
    }

    void Update()
    {
        if (!active) return;

        if (health != null && health.IsDead)
        {
            End();
            return;
        }

        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            End();
            return;
        }

        UpdateVfx();
    }

    // 물방울 이펙트: 제어(수면/해킹/매료)가 없을 때만 보입니다. 이미 붙어 있으면 EnemyStatusVfx가 그대로 이어가므로 매 프레임 불러도 됩니다.
    private void UpdateVfx()
    {
        if (vfxSettings == null || vfxSettings.prefab == null) return;
        if (!ShouldShowVfx()) return;
        EnemyStatusVfx.AttachCustom(gameObject, VfxKey, vfxSettings, ShouldShowVfx);
    }

    private bool ShouldShowVfx()
    {
        if (!IsChilled) return false;
        if (control == null) return true;
        return !control.IsStrongControlled && control.NormalType == EnemyControlType.None;
    }

    private void End()
    {
        active = false;
        timer = 0f;
        speedModifier?.RemoveMultiplier(SourceId);
        attackSpeedModifier?.RemoveMultiplier(SourceId);
        control?.ClearStatusTint(SourceId);
        // 물방울 이펙트는 ShouldShowVfx()가 false가 되므로 EnemyStatusVfx가 다음 프레임에 스스로 끕니다.
    }

    void OnDisable()
    {
        if (active) End();
    }
}
