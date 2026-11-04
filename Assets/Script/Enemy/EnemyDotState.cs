using System;
using System.Collections.Generic;
using UnityEngine;

// 적에게 걸리는 "지속 피해"(도트 - 지금은 102/109번 시너지의 중독)를 전담하는 컴포넌트입니다. 처음 걸릴 때 GetOrAdd()로 자동으로 붙습니다
// (EnemyChillState와 같은 패턴 - 프리팹에 미리 붙일 필요 없음).
//
// [규칙 - 형빈 확정 (중독 기준)]
// - Apply(이름, 지속시간, 간격, 피해 계산 함수, 색, 이펙트...)로 겁니다. 이름(예: "poison")마다 하나씩만 걸립니다.
// - 이미 걸려 있으면 남은 시간만 지속시간으로 되돌립니다(피해 중첩 X, 시간 누적 X). 피해 간격은 끊기지 않고 이어집니다 - 그래서 계속 맞아도
//   간격마다 피해가 들어갑니다. 처음 걸리면 한 간격 뒤에 첫 피해가 들어갑니다(3초/1초면 1, 2, 3초에 세 번).
// - 피해량은 피해가 들어가는 순간 계산 함수를 불러 정합니다(중독 = 그 순간 플레이어 공격력 x 20%). EnemyHealth.TakeDamage로 주는 순수 피해라
//   41번 피해 증가/피해 숫자/처치 판정은 그대로 적용되고, 총알 명중 효과(매료/넉백 등)는 일어나지 않습니다.
// - 보스에게도 걸립니다.
// - 색은 EnemyControlState의 "상태 색" 칸에 맡깁니다(공격 충전 > 수면·해킹 > 매료 > 상태 색 > 원래 색 - 오한과 함께면 나중에 걸린 색).
// - 피해가 들어갈 때마다 이펙트를 몸통 중심에 한 번 생성합니다(적을 따라다니지 않는 일회성, 일정 시간 뒤 삭제).
// - 적이 죽으면 전부 끝납니다.
[DisallowMultipleComponent]
public class EnemyDotState : MonoBehaviour
{
    private class Dot
    {
        public string key;
        public float remaining;
        public float tickTimer;
        public float interval;
        public Func<float> damage;
        public Color color;
        public GameObject vfxPrefab;
        public float vfxScale;
        public Vector3 vfxOffset;
        public float vfxLifetime;
    }

    private readonly List<Dot> dots = new List<Dot>();
    private EnemyHealth health;
    private EnemyControlState control;

    public static EnemyDotState GetOrAdd(GameObject enemy)
    {
        if (enemy == null) return null;
        var existing = enemy.GetComponent<EnemyDotState>();
        return existing != null ? existing : enemy.AddComponent<EnemyDotState>();
    }

    // 이 이름의 지속 피해가 지금 걸려 있는지입니다.
    public bool Has(string key) => dots.Exists(d => d.key == key);

    void Awake()
    {
        health = GetComponent<EnemyHealth>();
    }

    public void Apply(string key, float duration, float interval, Func<float> damage, Color color,
                      GameObject vfxPrefab, float vfxScale, Vector3 vfxOffset, float vfxLifetime)
    {
        if (string.IsNullOrEmpty(key) || duration <= 0f || damage == null) return;
        if (health != null && health.IsDead) return;

        Dot dot = dots.Find(d => d.key == key);
        if (dot == null)
        {
            dot = new Dot { key = key, tickTimer = 0f };
            dots.Add(dot);
        }

        // 남은 시간만 되돌리고, 피해 간격(tickTimer)은 그대로 이어갑니다.
        dot.remaining = duration;
        dot.interval = Mathf.Max(interval, 0.05f);
        dot.damage = damage;
        dot.color = color;
        dot.vfxPrefab = vfxPrefab;
        dot.vfxScale = vfxScale;
        dot.vfxOffset = vfxOffset;
        dot.vfxLifetime = vfxLifetime;

        if (control == null) control = EnemyControlState.GetOrAdd(gameObject);
        control?.SetStatusTint(key, color);
    }

    void Update()
    {
        if (dots.Count == 0) return;

        if (health != null && health.IsDead)
        {
            EndAll();
            return;
        }

        float dt = Time.deltaTime;
        for (int i = dots.Count - 1; i >= 0; i--)
        {
            Dot dot = dots[i];
            dot.tickTimer += dt;
            dot.remaining -= dt;

            // 간격마다 피해. 마지막 피해(지속시간이 딱 끝나는 순간)도 들어가도록 시간 확인보다 먼저 처리합니다.
            while (dot.tickTimer >= dot.interval)
            {
                dot.tickTimer -= dot.interval;
                DealTick(dot);
                if (health != null && health.IsDead)
                {
                    EndAll();
                    return;
                }
            }

            if (dot.remaining <= 0f)
            {
                control?.ClearStatusTint(dot.key);
                dots.RemoveAt(i);
            }
        }
    }

    private void DealTick(Dot dot)
    {
        float amount = 0f;
        try
        {
            amount = dot.damage != null ? dot.damage() : 0f;
        }
        catch (Exception e)
        {
            Debug.LogError($"EnemyDotState: 지속 피해({dot.key}) 계산 중 오류가 발생해 이번 피해를 건너뜁니다: {e}");
            return;
        }

        SpawnVfx(dot);
        if (amount > 0f && health != null) health.TakeDamage(amount);
    }

    private void SpawnVfx(Dot dot)
    {
        if (dot.vfxPrefab == null) return;

        Vector3 pos = transform.position;
        Renderer body = health != null ? health.TargetRenderer : null;
        if (body != null) pos = body.bounds.center;
        pos += dot.vfxOffset;

        GameObject vfx = Instantiate(dot.vfxPrefab, pos, dot.vfxPrefab.transform.rotation);
        // [방 전환] 플레이어가 다른 방에 들어가면 바로 지워지도록 등록합니다(RoomScope - 다른 방 위 허공에 남지 않게).
        RoomScope.DestroyWhenPlayerLeaves(vfx);
        if (!Mathf.Approximately(dot.vfxScale, 1f))
        {
            vfx.transform.localScale = dot.vfxPrefab.transform.localScale * dot.vfxScale;
            foreach (var ps in vfx.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy; // 크기 배율이 파티클에도 적용되도록(이 인스턴스만)
            }
        }
        Destroy(vfx, Mathf.Max(dot.vfxLifetime, 0.1f));
    }

    private void EndAll()
    {
        for (int i = 0; i < dots.Count; i++) control?.ClearStatusTint(dots[i].key);
        dots.Clear();
    }

    void OnDisable()
    {
        EndAll();
    }
}
