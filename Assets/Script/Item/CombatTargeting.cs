using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// [87번 포섭 - 2단계: 목표 정하기]
// 적/아군 AI와 공격 컴포넌트가 "지금 누구를 노리는지"를 한 곳에서 정합니다. 예전에는 모든 적 스크립트가 "Player" 태그를 직접 찾아
// 쫓았는데, 이제는 이 창구가 정해 준 목표(Transform)를 쫓습니다. 아군이 하나도 없으면 결과는 예전과 같습니다(적의 목표 = 플레이어).
//
// [목표 규칙 - 형빈 확정]
// - 아군: 같은 방(켜져 있는 방)의 살아 있는 적 중 가장 가까운 적(보스 포함). 적이 없으면 목표 없이 플레이어를 따라다닙니다(공격 X).
// - 적: 플레이어와 아군 중 가장 가까운 쪽. 플레이어가 112번으로 투명하면 아군만 노립니다(아군도 없으면 목표 없음 = 대기).
// - 탐지 범위/시야 규칙(적의 대기 → 추적)은 각 AI가 지금처럼 목표 기준으로 판단합니다. 아군은 탐지 범위와 상관없이 바로 싸웁니다.
//
// [구현]
// 목표는 적/아군마다 붙는 CombatTargetState에 기억해 두고 targetReevaluateInterval(0.2초)마다 다시 고릅니다(매 프레임 바꾸면 목표가
// 흔들림). 목표가 죽거나 사라지거나 투명해지면 즉시 다시 고릅니다. 공격 컴포넌트는 공격을 시작할 때의 목표를 그대로 읽습니다
// (CurrentTarget / TargetIsInvisiblePlayer).
public static class CombatTargeting
{
    public const float TargetReevaluateInterval = 0.2f;

    // self의 이번 프레임 목표를 정해 돌려줍니다. followPlayer = 아군인데 싸울 적이 없어 플레이어를 따라다녀야 하는 상태(이때 반환값 null).
    public static Transform Resolve(GameObject self, bool selfIsAlly, Transform player, out bool followPlayer)
    {
        var state = CombatTargetState.GetOrAdd(self);
        if (state == null)
        {
            followPlayer = false;
            return selfIsAlly ? null : player;
        }
        return state.Resolve(selfIsAlly, player, false, out followPlayer);
    }

    // 캐시를 무시하고 지금 바로 다시 고릅니다(예: 연속 돌진 사이 재조준).
    public static Transform ResolveNow(GameObject self, bool selfIsAlly, Transform player, out bool followPlayer)
    {
        var state = CombatTargetState.GetOrAdd(self);
        if (state == null)
        {
            followPlayer = false;
            return selfIsAlly ? null : player;
        }
        return state.Resolve(selfIsAlly, player, true, out followPlayer);
    }

    // AI가 마지막으로 정한 목표입니다(공격 컴포넌트용). 아직 정한 적이 없거나 사라졌으면 fallback(보통 플레이어).
    public static Transform CurrentTarget(GameObject self, Transform fallback)
    {
        var state = self != null ? self.GetComponent<CombatTargetState>() : null;
        if (state == null || state.Target == null) return state != null && state.FollowingPlayer ? null : fallback;
        return state.Target;
    }

    // 지금 목표가 "투명해진 플레이어"인지입니다(112번 - 이때만 공격 준비를 취소). 목표 정보가 없으면 예전처럼 투명 여부만 봅니다.
    public static bool TargetIsInvisiblePlayer(GameObject self)
    {
        if (!PlayerInvisibility.IsActive) return false;
        var state = self != null ? self.GetComponent<CombatTargetState>() : null;
        if (state == null) return true;
        return state.TargetIsPlayer;
    }

    // 아군이 싸울 수 있는 적인지(살아 있고, 켜져 있고, 아군이 아님).
    public static bool IsHostileAlive(EnemyHealth e)
    {
        return e != null && !e.IsDead && e.isActiveAndEnabled && !AllyFaction.IsAlly(e.gameObject);
    }

    // 적이 노릴 수 있는 아군인지(살아 있고, 켜져 있음).
    public static bool IsAllyAlive(AllyFaction a)
    {
        return a != null && a.isActiveAndEnabled && a.Health != null && !a.Health.IsDead;
    }

    // 아군이 싸울 적이 없을 때 플레이어를 따라다닙니다: followDistance보다 멀면 다가가고, 가까우면 멈춥니다(공격하지 않음).
    // 다가가는 동안은 가는 방향을, 멈춰 있을 때는 플레이어 쪽을 바라봅니다.
    public static void FollowPlayer(NavMeshAgent agent, Transform self, Transform player, AllyFaction.Settings settings, float rotationSpeed)
    {
        if (agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh || player == null || self == null) return;

        float followDistance = settings != null ? settings.followDistance : 2f;
        float resumeDistance = followDistance + (settings != null ? settings.followResumeMargin : 0.75f);

        Vector3 flat = player.position - self.position;
        flat.y = 0f;
        float distance = flat.magnitude;

        bool moving = !agent.isStopped;
        if (distance > resumeDistance || (moving && distance > followDistance))
        {
            agent.isStopped = false;
            agent.SetDestination(player.position);
        }
        else
        {
            agent.isStopped = true;
        }

        Vector3 look = !agent.isStopped && agent.velocity.sqrMagnitude > 0.01f ? agent.velocity : flat;
        look.y = 0f;
        if (look.sqrMagnitude < 0.0001f) return;
        self.rotation = Quaternion.Slerp(self.rotation, Quaternion.LookRotation(look), rotationSpeed * Time.deltaTime);
    }
}

// 적/아군 하나의 현재 목표 기억(CombatTargeting이 자동으로 붙임 - 프리팹에 미리 붙일 필요 없음).
public class CombatTargetState : MonoBehaviour
{
    public Transform Target { get; private set; }
    public bool TargetIsPlayer { get; private set; }
    public bool FollowingPlayer { get; private set; }

    private EnemyHealth targetEnemy;
    private float nextEvaluateTime;

    public static CombatTargetState GetOrAdd(GameObject go)
    {
        if (go == null) return null;
        var s = go.GetComponent<CombatTargetState>();
        return s != null ? s : go.AddComponent<CombatTargetState>();
    }

    public Transform Resolve(bool selfIsAlly, Transform player, bool force, out bool followPlayer)
    {
        if (!force && Time.time < nextEvaluateTime && IsCurrentValid(selfIsAlly, player))
        {
            followPlayer = FollowingPlayer;
            return Target;
        }

        nextEvaluateTime = Time.time + CombatTargeting.TargetReevaluateInterval;
        if (selfIsAlly) PickForAlly(player);
        else PickForEnemy(player);

        followPlayer = FollowingPlayer;
        return Target;
    }

    private bool IsCurrentValid(bool selfIsAlly, Transform player)
    {
        if (selfIsAlly)
        {
            if (FollowingPlayer) return true; // 따라다니는 중에는 주기적으로만 다시 확인
            return targetEnemy != null && CombatTargeting.IsHostileAlive(targetEnemy);
        }

        if (Target == null) return false;
        if (TargetIsPlayer) return Target == player && !PlayerInvisibility.IsActive;
        return targetEnemy != null && !targetEnemy.IsDead && targetEnemy.isActiveAndEnabled && AllyFaction.IsAlly(targetEnemy.gameObject);
    }

    // 아군: 같은 방의 가장 가까운 적. 없으면 플레이어 따라다니기.
    private void PickForAlly(Transform player)
    {
        EnemyHealth best = null;
        float bestSqr = float.MaxValue;
        Vector3 pos = transform.position;

        var enemies = EnemyHealth.ActiveEnemies;
        for (int i = 0; i < enemies.Count; i++)
        {
            var e = enemies[i];
            if (!CombatTargeting.IsHostileAlive(e)) continue;
            Vector3 d = e.transform.position - pos;
            d.y = 0f;
            float sqr = d.sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = e;
            }
        }

        targetEnemy = best;
        Target = best != null ? best.transform : null;
        TargetIsPlayer = false;
        FollowingPlayer = best == null;
    }

    // 적: 플레이어(투명하지 않으면)와 아군 중 가장 가까운 쪽.
    private void PickForEnemy(Transform player)
    {
        Transform best = null;
        EnemyHealth bestEnemy = null;
        bool bestIsPlayer = false;
        float bestSqr = float.MaxValue;
        Vector3 pos = transform.position;

        if (player != null && !PlayerInvisibility.IsActive)
        {
            Vector3 d = player.position - pos;
            d.y = 0f;
            bestSqr = d.sqrMagnitude;
            best = player;
            bestIsPlayer = true;
        }

        var allies = AllyFaction.All;
        for (int i = 0; i < allies.Count; i++)
        {
            var a = allies[i];
            if (!CombatTargeting.IsAllyAlive(a)) continue;
            Vector3 d = a.transform.position - pos;
            d.y = 0f;
            float sqr = d.sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = a.transform;
                bestEnemy = a.Health;
                bestIsPlayer = false;
            }
        }

        Target = best;
        targetEnemy = bestEnemy;
        TargetIsPlayer = bestIsPlayer;
        FollowingPlayer = false;
    }
}
