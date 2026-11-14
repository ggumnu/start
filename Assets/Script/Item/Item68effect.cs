using System.Collections.Generic;
using UnityEngine;

// 패시브 68번 - "플레이어 총알에 맞은 적이 밀려난다(넉백). 밀려난 적이 다른 적과 닿으면, 밀려난 적과 닿은 적 모두
// 공격력의 50%만큼 피해를 입는다."
//
// [기획 - 형빈 확정]
// - 밀려나는 거리 = 총알 속도 x distancePerBulletSpeed(기본 0.1). 예) 기본 총알 속도 3 -> 0.3.
//   5번 분열탄도 밀어내며, 분열탄의 아이템 효과 배율(Projectile.EffectScale - 1세대 0.5, 2세대 0.25 ...)을 거리에 곱합니다.
// - 최종 거리가 minPushDistance(기본 0.1)보다 짧으면 밀리지 않은 것으로 칩니다(충돌 피해도 없음).
//   maxPushDistance가 0보다 크면 그 거리까지만 밉니다(기본 0 = 제한 없음).
// - 밀리는 방향은 총알이 날아가던 방향(수평)입니다.
// - 밀리지 않는 적: 보스(BossAIController가 붙은 적), 돌진 중인 적(IDashDamageSource.IsDashing - 일반 돌진/연속 돌진 모두),
//   맞고 죽은 적. 돌진 "충전" 중인 적은 밀립니다. 수면/해킹/매료 중인 적도 밀립니다. 밀리는 도중 돌진을 시작하면 그 자리에서
//   밀림이 끝납니다.
// - 밀리다가 벽이나 다른 적에 부딪히면 그 자리에서 멈춥니다. 연쇄 넉백(닿은 적이 또 밀리는 것)은 없습니다.
// - 충돌 피해: 밀려난 적이 다른 적과 닿으면, 닿은 적은 각각 한 번씩, 밀려난 적은 닿은 적이 몇 마리든 딱 한 번만 피해를 입습니다.
//   예) 3마리가 뭉쳐 있을 때 한 마리를 쏘면, 나머지 2마리가 각각 1번, 밀려난 적도 1번 피해를 입습니다.
//   넉백이 시작되는 순간부터 이미 닿아 있던 적도 충돌로 칩니다(이 경우 밀려난 적은 거의 움직이지 않고 바로 멈춤).
//   보스도 "닿은 적"으로서는 피해를 입습니다(밀리지만 않음).
// - 충돌 피해량 = 맞은 순간 플레이어의 공격력 스탯(PlayerStats.Damage) x impactDamageRatio(기본 0.5) x 분열탄 효과 배율.
//   총알 피해(23번 랜덤 피해, 11번 거리 피해, 35번 튕김 보너스 등)와는 별개입니다(형빈 확정 - 다른 아이템과 꼬이지 않게).
//   또한 총알 적중이 아니므로 38번 매료 같은 "총알 명중 시" 효과는 걸리지 않습니다. 피해 숫자는 항상 Weak(아이템 부가 피해 - 형빈 확정).
// - 충돌마다 두 적 사이에 충돌 이펙트를 생성합니다(비워두면 이펙트 없음).
//
// [구현]
// - 총알마다 OnDespawning(적중 시점)을 구독해서, HitEnemy일 때 그 적을 ForcedPush.Apply로 밉니다(적 밀기는 ForcedPush가
//   담당 - NavMeshAgent.Move, 벽 앞 정지, 밀리는 동안 적 AI 이동 일시정지).
// - 충돌 판정은 ForcedPush.AfterStep(한 걸음 민 직후마다 불림)에서 밀려난 적의 몸통 콜라이더와 겹치는 다른 적을 찾습니다.
//   넉백 시작 순간에도 한 번 확인합니다(이미 닿아 있던 적).
// - 잃으면(48번 교체 등) 그 뒤로 쏘는 총알부터 적용되지 않습니다(이미 날아가는 총알은 그대로 - 4번 등과 같은 관례).
//
// [아이템 에셋 설정]
// ItemDefinition: Item Id = 68, Category = Passive, Special Effects에 BulletKnockbackEffect 하나. Stat Modifiers는 비워둡니다.
[System.Serializable]
public class BulletKnockbackEffect : IPassiveItemEffect, IRemovablePassiveEffect
{
    [Header("넉백 거리")]
    [Tooltip("총알 속도 1당 밀려나는 거리입니다(기본 0.1). 예) 총알 속도 3 -> 0.3.")]
    public float distancePerBulletSpeed = 0.1f;
    [Tooltip("최종 넉백 거리가 이보다 짧으면 밀리지 않은 것으로 칩니다(충돌 피해도 없음). 기본 0.1.")]
    public float minPushDistance = 0.1f;
    [Tooltip("넉백 거리의 최대값입니다. 0 이하면 제한 없음(기본).")]
    public float maxPushDistance = 0f;
    [Tooltip("밀려나는 데 걸리는 시간(초)입니다. 처음엔 빠르고 끝으로 갈수록 느려집니다. 이 시간 동안 적은 이동 판단을 쉽니다.")]
    public float pushDuration = 0.15f;

    [Header("충돌 피해")]
    [Tooltip("충돌 피해 = 플레이어 공격력 x 이 값(기본 0.5 = 50%). 닿은 적과 밀려난 적 모두 이만큼 받습니다.")]
    public float impactDamageRatio = 0.5f;

    [Header("충돌 이펙트 (선택 - 비워두면 이펙트 없이 피해만 적용)")]
    [Tooltip("밀려난 적이 다른 적과 닿을 때 두 적 사이에 생성할 이펙트 프리팹입니다. 원본 이펙트를 복제해서 그 프리팹을 직접 수정하세요.")]
    public GameObject impactVfxPrefab;
    [Tooltip("이펙트 생성 위치(두 적 몸통의 중간 지점)에 더할 오프셋입니다.")]
    public Vector3 impactVfxOffset = Vector3.zero;
    [Tooltip("이펙트 크기 배율입니다(기본 1 = 프리팹 그대로). 파티클의 시작 크기/속도에 곱해서 모양 그대로 크기만 바뀝니다 " +
             "(파티클이 없는 이펙트는 오브젝트 크기에 곱함). 4번 폭발 이펙트와 같은 방식입니다.")]
    [Min(0.01f)] public float impactVfxScale = 1f;
    [Tooltip("생성한 이펙트를 이 시간(초) 뒤에 자동으로 파괴합니다. 0 이하면 파괴하지 않습니다(프리팹에 자동 소멸 설정이 있는 경우).")]
    public float impactVfxLifetime = 2f;

    // 겹침 판정 때 몸통에 더하는 여유 거리입니다(m). 딱 맞닿은 경우도 닿은 것으로 칩니다.
    private const float ContactSkin = 0.01f;

    [System.NonSerialized] private PassiveEffectContext context;

    private static readonly Collider[] overlapBuffer = new Collider[32];
    private static readonly List<IDashDamageSource> dashSourceBuffer = new List<IDashDamageSource>();

    public void Apply(PassiveEffectContext context, string sourceItemId)
    {
        if (context == null || context.Shooting == null)
        {
            Debug.LogWarning("BulletKnockbackEffect: PlayerShooting을 찾을 수 없어 68번 효과를 적용하지 못했습니다.");
            return;
        }

        this.context = context;
        context.Shooting.OnProjectileSpawned -= HandleProjectileSpawned; // 혹시 남아 있을 이전 구독 정리(중복 방지)
        context.Shooting.OnProjectileSpawned += HandleProjectileSpawned;
    }

    // [패시브 잃기 - 48번 등] 그 뒤로 쏘는 총알부터 넉백이 걸리지 않습니다.
    public void Remove(PassiveEffectContext context, string sourceItemId)
    {
        if (context?.Shooting != null) context.Shooting.OnProjectileSpawned -= HandleProjectileSpawned;
    }

    private void HandleProjectileSpawned(Projectile projectile)
    {
        if (projectile == null) return;
        projectile.OnDespawning += despawnEvent => HandleDespawning(projectile, despawnEvent);
    }

    private void HandleDespawning(Projectile projectile, ProjectileDespawnEvent despawnEvent)
    {
        if (despawnEvent.Reason != ProjectileDespawnReason.HitEnemy || despawnEvent.HitObject == null) return;

        EnemyHealth enemy = despawnEvent.HitObject.GetComponent<EnemyHealth>();
        if (enemy == null || enemy.IsDead) return;              // 맞고 죽은 적은 밀지 않음
        if (enemy.GetComponent<BossAIController>() != null) return; // 보스는 밀리지 않음
        if (IsDashing(enemy.gameObject)) return;                 // 돌진 중인 적은 밀리지 않음(충전 중은 밀림)

        float effectScale = projectile != null ? projectile.EffectScale : 1f;
        float speed = projectile != null ? projectile.Speed : 0f;

        float pushDistance = speed * distancePerBulletSpeed * effectScale;
        if (maxPushDistance > 0f) pushDistance = Mathf.Min(pushDistance, maxPushDistance);
        if (pushDistance < minPushDistance - 0.0001f) return; // 너무 짧으면 밀리지 않은 것으로 침

        Vector3 dir = despawnEvent.IncomingDirection;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;

        float playerDamage = (context != null && context.Stats != null) ? context.Stats.Damage : 0f;
        float impactDamage = Mathf.Max(0f, playerDamage * impactDamageRatio * effectScale);

        ForcedPush push = ForcedPush.Apply(enemy.gameObject, dir, pushDistance, pushDuration);
        if (push == null) return;

        Collider body = FindBodyCollider(enemy);
        push.AfterStep = p => CheckImpact(p, enemy, body, impactDamage);

        // 넉백이 시작되는 순간 이미 닿아 있는 적도 충돌로 칩니다.
        Physics.SyncTransforms();
        CheckImpact(push, enemy, body, impactDamage);
    }

    // 밀려난 적이 다른 적과 닿았는지 확인하고, 닿았으면 충돌 피해/이펙트를 주고 그 자리에서 멈춥니다.
    private void CheckImpact(ForcedPush push, EnemyHealth pushed, Collider body, float impactDamage)
    {
        if (push == null || !push.IsPushing) return;

        if (pushed == null || pushed.IsDead || IsDashing(pushed.gameObject))
        {
            push.Stop(); // 죽었거나 밀리는 도중 돌진을 시작하면 밀림을 끝냅니다.
            return;
        }

        if (body == null || !body.enabled) return;

        List<EnemyHealth> touched = FindTouchingEnemies(pushed, body);
        if (touched.Count == 0) return;

        // 먼저 멈춥니다(피해 처리 중 사망 등으로 상태가 바뀌어도 밀림이 남지 않도록).
        push.Stop();

        Vector3 pushedCenter = body.bounds.center;
        foreach (var other in touched)
        {
            if (other == null) continue;

            Vector3 otherCenter = GetBodyCenter(other);
            other.TakeWeakDamage(impactDamage); // 피해 숫자 항상 Weak(형빈 확정)
            SpawnImpactVfx((pushedCenter + otherCenter) * 0.5f);
        }

        // 밀려난 적은 닿은 적이 몇 마리든 딱 한 번만 피해를 입습니다(형빈 확정).
        pushed.TakeWeakDamage(impactDamage);
    }

    // 밀려난 적의 몸통과 겹치는(살아 있는) 다른 적 목록입니다. 한 적의 콜라이더가 여러 개여도 한 번만 들어갑니다.
    private static List<EnemyHealth> FindTouchingEnemies(EnemyHealth pushed, Collider body)
    {
        var result = new List<EnemyHealth>();
        int count = OverlapBody(body, ContactSkin, overlapBuffer);

        for (int i = 0; i < count; i++)
        {
            Collider col = overlapBuffer[i];
            if (col == null) continue;
            if (col.transform.IsChildOf(pushed.transform)) continue; // 자기 자신

            EnemyHealth other = col.GetComponentInParent<EnemyHealth>();
            if (other == null || other == pushed || other.IsDead) continue;
            if (AllyFaction.IsAlly(other.gameObject)) continue; // [87번] 포섭된 아군은 넉백 충돌 대상이 아닙니다(피해 X, 밀림도 멈추지 않음).
            if (!result.Contains(other)) result.Add(other);
        }
        return result;
    }

    // 몸통 콜라이더 모양 그대로(조금 키워서) 겹치는 콜라이더를 찾습니다. 트리거(근접 공격 판정 등)는 제외합니다.
    private static int OverlapBody(Collider body, float skin, Collider[] buffer)
    {
        if (body is CapsuleCollider cap)
        {
            Transform t = cap.transform;
            Vector3 s = t.lossyScale;
            float ax = Mathf.Abs(s.x), ay = Mathf.Abs(s.y), az = Mathf.Abs(s.z);

            float radiusScale, heightScale;
            Vector3 axis;
            switch (cap.direction)
            {
                case 0: radiusScale = Mathf.Max(ay, az); heightScale = ax; axis = t.right; break;
                case 2: radiusScale = Mathf.Max(ax, ay); heightScale = az; axis = t.forward; break;
                default: radiusScale = Mathf.Max(ax, az); heightScale = ay; axis = t.up; break;
            }

            float radius = cap.radius * radiusScale;
            float half = Mathf.Max(0f, cap.height * heightScale * 0.5f - radius);
            Vector3 center = t.TransformPoint(cap.center);
            return Physics.OverlapCapsuleNonAlloc(center + axis * half, center - axis * half, radius + skin, buffer,
                                                  Physics.AllLayers, QueryTriggerInteraction.Ignore);
        }

        if (body is SphereCollider sphere)
        {
            Transform t = sphere.transform;
            Vector3 s = t.lossyScale;
            float radius = sphere.radius * Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
            return Physics.OverlapSphereNonAlloc(t.TransformPoint(sphere.center), radius + skin, buffer,
                                                 Physics.AllLayers, QueryTriggerInteraction.Ignore);
        }

        Bounds b = body.bounds;
        return Physics.OverlapBoxNonAlloc(b.center, b.extents + Vector3.one * skin, buffer, Quaternion.identity,
                                          Physics.AllLayers, QueryTriggerInteraction.Ignore);
    }

    // 적의 몸통(트리거가 아닌) 콜라이더입니다. EnemyHealth가 붙은 오브젝트 자신의 것을 먼저 찾고, 없으면 자식에서 찾습니다.
    private static Collider FindBodyCollider(EnemyHealth enemy)
    {
        if (enemy == null) return null;
        foreach (var col in enemy.GetComponents<Collider>())
        {
            if (col != null && !col.isTrigger) return col;
        }
        foreach (var col in enemy.GetComponentsInChildren<Collider>())
        {
            if (col != null && !col.isTrigger) return col;
        }
        return null;
    }

    private static Vector3 GetBodyCenter(EnemyHealth enemy)
    {
        Collider col = FindBodyCollider(enemy);
        return col != null ? col.bounds.center : enemy.transform.position;
    }

    // 일반 돌진/연속 돌진 등 IDashDamageSource 중 하나라도 지금 돌진 중이면 true입니다.
    private static bool IsDashing(GameObject enemy)
    {
        if (enemy == null) return false;
        dashSourceBuffer.Clear();
        enemy.GetComponents(dashSourceBuffer);
        for (int i = 0; i < dashSourceBuffer.Count; i++)
        {
            if (dashSourceBuffer[i] != null && dashSourceBuffer[i].IsDashing) return true;
        }
        return false;
    }

    private void SpawnImpactVfx(Vector3 position)
    {
        if (impactVfxPrefab == null) return;

        GameObject vfx = Object.Instantiate(impactVfxPrefab, position + impactVfxOffset, Quaternion.identity);
        // [방 전환] 플레이어가 다른 방에 들어가면 바로 지워지도록 등록합니다(RoomScope - 다른 방 위 허공에 남지 않게).
        RoomScope.DestroyWhenPlayerLeaves(vfx);
        ApplyVfxScale(vfx, impactVfxScale);
        if (impactVfxLifetime > 0f) Object.Destroy(vfx, impactVfxLifetime);
    }

    // 이펙트 크기를 배율만큼 바꿉니다(4번 ExplodeOnImpactEffect와 같은 방식). 오브젝트의 localScale이 아니라 파티클의 시작
    // 크기/속도에 직접 곱합니다 - 일부 이펙트는 Scaling Mode 때문에 localScale을 바꿔도 반응하지 않기 때문입니다. 파티클이
    // 없는 이펙트만 localScale을 씁니다. 생성된 이 이펙트 하나만 바뀌고 원본 프리팹은 건드리지 않습니다.
    private static void ApplyVfxScale(GameObject instance, float multiplier)
    {
        if (instance == null || Mathf.Approximately(multiplier, 1f)) return;

        var systems = instance.GetComponentsInChildren<ParticleSystem>(true);
        if (systems.Length == 0)
        {
            instance.transform.localScale *= multiplier;
            return;
        }

        for (int i = 0; i < systems.Length; i++)
        {
            var main = systems[i].main;

            main.startSizeMultiplier *= multiplier;
            if (main.startSize3D)
            {
                main.startSizeXMultiplier *= multiplier;
                main.startSizeYMultiplier *= multiplier;
                main.startSizeZMultiplier *= multiplier;
            }
            main.startSpeedMultiplier *= multiplier;
        }
    }
}
