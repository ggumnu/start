using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// [2레벨 보스 소환] 소환된 적이 일정 시간 동안 흰 빛에서 원래 색으로 서서히 나타나게 하고(AppearFromWhite), 그동안은 움직이거나 공격하지 않고
// 접촉 피해도 주지 않게 합니다(AI 컨트롤러와 접촉 트리거 콜라이더를 잠시 끔 - 피격은 그대로 받음). 시간이 지나면 끈 것만 다시 켜고 스스로 지워집니다.
// AI가 다시 켜질 때 평소 입장 유예(Entry Attack Delay, 기본 0.5초)가 한 번 더 적용됩니다. 설계 배경은 Docs/설계노트.md [2레벨 보스].
public class SummonMaterialize : MonoBehaviour
{
    private readonly List<Behaviour> disabledBehaviours = new List<Behaviour>();
    private readonly List<Collider> disabledColliders = new List<Collider>();
    private EnemyHealth health;
    private NavMeshAgent agent;
    private float endTime;
    private bool running;

    public static SummonMaterialize Begin(GameObject enemy, float duration)
    {
        if (enemy == null) return null;
        var m = enemy.GetComponent<SummonMaterialize>();
        if (m == null) m = enemy.AddComponent<SummonMaterialize>();
        m.Run(duration);
        return m;
    }

    private void Run(float duration)
    {
        if (running) return;
        running = true;
        endTime = Time.time + Mathf.Max(duration, 0f);
        health = GetComponent<EnemyHealth>();
        agent = GetComponent<NavMeshAgent>();

        // 연출은 실패해도 공격/접촉 잠금은 그대로 진행합니다.
        try
        {
            var appear = AppearFromWhite.Begin(gameObject);
            if (appear != null) appear.duration = Mathf.Max(duration, 0.01f);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"SummonMaterialize ({gameObject.name}): 나타나는 연출 중 오류가 발생해 연출 없이 진행합니다: {e}");
        }

        // 공격/이동 판단(AI 컨트롤러)을 끕니다.
        DisableBehaviour(GetComponent<EnemyAIController>());
        DisableBehaviour(GetComponent<EnemyRangedAIController>());

        // 접촉 피해(돌진 포함)는 접촉 트리거 콜라이더를 꺼서 막습니다(트리거 콜백은 컴포넌트를 꺼도 오므로 콜라이더를 끔).
        foreach (var contact in GetComponentsInChildren<EnemyMeleeContactTrigger>(true))
        {
            foreach (var col in contact.GetComponents<Collider>())
            {
                if (col != null && col.enabled)
                {
                    col.enabled = false;
                    disabledColliders.Add(col);
                }
            }
        }

        StopAgent();
    }

    private void DisableBehaviour(Behaviour b)
    {
        if (b == null || !b.enabled) return;
        b.enabled = false;
        disabledBehaviours.Add(b);
    }

    private void StopAgent()
    {
        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }
    }

    void Update()
    {
        if (!running) return;

        // 나타나는 도중 죽었으면 되살리지 않고 그대로 둡니다(사라짐 연출이 컴포넌트/콜라이더를 정리함).
        if (health != null && health.IsDead)
        {
            running = false;
            Destroy(this);
            return;
        }

        if (Time.time < endTime)
        {
            StopAgent();
            return;
        }

        running = false;
        foreach (var col in disabledColliders)
        {
            if (col != null) col.enabled = true;
        }
        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh) agent.isStopped = false;
        foreach (var b in disabledBehaviours)
        {
            if (b != null) b.enabled = true;
        }
        disabledColliders.Clear();
        disabledBehaviours.Clear();
        Destroy(this);
    }
}
