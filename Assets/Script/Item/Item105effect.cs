using System;
using UnityEngine;

// 패시브 105번 - "적을 처치하면 즉시 재장전을 완료한다."
//
// [기획 - 형빈 확정]
// - 적이 죽을 때마다(무엇으로 죽였든 - 총알/수류탄/해킹/4번 폭발/81번 불/29·68번/98번 섬멸 등 모든 처치) 장전 시간·장전 모션 없이
//   탄창을 즉시 가득 채웁니다(2/5 → 5/5). 연출은 없습니다.
// - 무기 모드(총/수류탄/해킹툴)와 상관없이 채웁니다.
// - "장전"으로 취급하므로 104번의 공격력 증가가 초기화됩니다.
// - 장전 중이었다면 남은 장전 시간과 상관없이 그 장전을 바로 끝냅니다(마지막 탄환을 쏘고 장전을 시작했는데 그 탄환이 적을 처치한 경우 등).
// - 탄창이 이미 가득 차 있고 장전 중도 아니면 아무 일도 없습니다(104번도 그대로).
// - 110번 충전 중이면 충전은 끊기지 않고 충전 수를 유지한 채, 새로 채운 탄약으로 계속 충전합니다(과충전 가능 - 형빈 확정).
//   탄약을 다 써서 충전이 멈춰 있던 상태여도 버튼을 누르고 있으면 다음 충전 간격에 다시 이어집니다.
// - 플레이어가 죽어 있으면 아무것도 하지 않습니다. 잃으면(48번 등) 그 뒤로는 발동하지 않습니다.
//
// [구현]
// EnemyHealth.OnAnyEnemyKilled를 구독해 PlayerShooting.TryInstantReload()를 부릅니다(예외가 나도 적의 사라짐 연출이 막히지 않도록 감쌈).
//
// [아이템 에셋 설정]
// ItemDefinition: Item Id = 105, Category = Passive, Special Effects에 KillInstantReloadEffect 하나. Stat Modifiers는 비워둡니다.
[Serializable]
public class KillInstantReloadEffect : IPassiveItemEffect, IRemovablePassiveEffect
{
    [NonSerialized] private PlayerShooting shooting;
    [NonSerialized] private PlayerHealth health;
    [NonSerialized] private Action<EnemyHealth, Vector3> killHandler;

    public void Apply(PassiveEffectContext context, string sourceItemId)
    {
        if (context == null || context.Shooting == null)
        {
            Debug.LogWarning("KillInstantReloadEffect: 플레이어 사격 컴포넌트를 찾을 수 없어 105번 효과를 적용하지 못했습니다.");
            return;
        }

        Unsubscribe(); // 혹시 Remove 없이 다시 불려도 중복 구독되지 않도록

        shooting = context.Shooting;
        health = context.Health;
        killHandler = HandleEnemyKilled;
        EnemyHealth.OnAnyEnemyKilled += killHandler;
    }

    // [패시브 잃기 - 48번 등] 그 뒤로는 처치해도 즉시 장전하지 않습니다.
    public void Remove(PassiveEffectContext context, string sourceItemId)
    {
        Unsubscribe();
        shooting = null;
        health = null;
    }

    private void Unsubscribe()
    {
        if (killHandler != null) EnemyHealth.OnAnyEnemyKilled -= killHandler;
        killHandler = null;
    }

    private void HandleEnemyKilled(EnemyHealth enemy, Vector3 position)
    {
        try
        {
            if (shooting == null) return;
            if (health != null && health.IsDead) return;
            shooting.TryInstantReload();
        }
        catch (Exception e)
        {
            Debug.LogError($"KillInstantReloadEffect: 즉시 장전 중 오류가 발생해 건너뜁니다: {e}");
        }
    }
}
