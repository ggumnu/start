using System.Collections.Generic;
using UnityEngine;

// [63번 유도 미사일 - 조종기] 미사일 한 발은 플레이어 총알(Projectile 미사일 모드)이고, 이 컴포넌트는 그 위에 붙어 유도/연기/폭발 연출만 합니다.
// 총알과 같은 취급(형빈 확정): PlayerShooting.SpawnMissileProjectile이 OnProjectileSpawned를 보내 38번 매료/4번 폭발/94번 후광/68번 넉백/
// 69번 튕김/5번 분열/118번 포격/23번 랜덤 피해·색 등이 그대로 붙습니다. 미사일만의 예외는 Projectile.cs [63번 유도 미사일] 주석과
// 각 아이템(5/11/69/113번, BulletHalo)에 있습니다.
// - 비행: 처음 Straight Duration 동안 직진 → 그 뒤 목표 쪽으로 꺾음. 꺾는 속도에 상한(Turn Rate)이 있어 회전 반경이 생기고 즉시 반대로
//   꺾지 않습니다. 목표 주위를 계속 맴도는 것을 막기 위해 유도 시간이 지날수록 꺾는 속도가 커지고, 목표 중심에 Hit Radius만큼 다가오면
//   콜라이더가 겹치지 않아도 명중시킵니다. Max Flight Time이 지나면 그 자리에서 폭발합니다(피해 없음, 아이템 반응 없음).
// - 목표가 맞기 전에 죽으면 그 미사일에서 가장 가까운 적으로 다시 유도, 없으면 그 자리에서 폭발(형빈 확정).
// - 색: 총알 색 아이템(38번/23번 등)을 따라가고, 없으면 Missile Color(00FDFF). 별똥별 꼬리 없이 연기만 - 색 아이템이 있으면 연기도 그 색.
// - 방 범위: 플레이어가 방을 나가면 미사일/연기/폭발 이펙트 모두 바로 지워집니다(RoomScope).
// - [3레벨 보스 그로기 비콘 - 형빈 확정] 총알 비콘도 적과 함께 목표 후보입니다(CollectRoomTargets - 가까운 순). 비콘에 맞으면 총알 한 발로
//   셉니다. 목표는 Component(EnemyHealth 또는 GroggyBeacon)로 다룹니다. 이미 반응한 비콘은 다시 노리지 않습니다.
public class HomingMissile : MonoBehaviour
{
    private MissileDroneFamiliarEffect settings;
    private Projectile projectile;

    private float age;
    private float smokeTimer;
    private bool finished;
    private GameObject smokePrefab;
    private bool hasSmokeTint;
    private Color smokeTint;
    private Vector3 lastPosition;
    private Vector3 heading; // 수평 진행 방향(높이는 따로 맞춤 - Update의 [수평 회전 + 높이 따로] 참고)
    private int knownEnemyBounceCount;

    // ---------------- 생성 ----------------

    // 드론이 쏘는 미사일 한 발(원본). damage = 공격력 x 미사일 피해 배율.
    // target = 적(EnemyHealth) 또는 총알 비콘(GroggyBeacon).
    public static Projectile Launch(PlayerShooting shooting, Vector3 position, Vector3 forward, Component target, float damage,
                                    MissileDroneFamiliarEffect settings)
    {
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
        // 아이템 효과 배율 = 미사일 피해 배율(50%) - 4번 폭발/94번 후광/68번/118번/23번이 분열탄처럼 줄어듦(형빈 확정 A안).
        return Spawn(shooting, settings, position, forward.normalized, target, damage, settings.missileSize,
                     settings.damageMultiplier, 0, null);
    }

    // [5번 연동] 분열 미사일 count갈래. 8방향으로 잠깐 직진한 뒤, 맞은 적을 뺀 방 안의 적에게 골고루 나눠 유도합니다(다른 적이 없으면 그 적에게
    // 다시). 분열 미사일은 더 분열하지 않습니다(Item05effect).
    public static void SpawnSplitMissiles(Projectile parent, PlayerShooting shooting, Vector3 origin, Vector3 baseDirection, int count,
                                          float childDamage, float childSize, int childGeneration, float childEffectScale,
                                          EnemyHealth hitEnemy, int maxLiveSplit)
    {
        if (parent == null || shooting == null) return;
        var parentDriver = parent.GetComponent<HomingMissile>();
        if (parentDriver == null || parentDriver.settings == null) return;
        var s = parentDriver.settings;

        var candidates = new List<Component>();
        CollectRoomTargets(origin, candidates, null);
        candidates.Remove(hitEnemy);
        // [3레벨 보스 그로기 비콘] 비콘을 맞혀 분열했으면 그 비콘도 뺍니다(분열 미사일은 그 비콘을 통과 - 한 발 = 한 번).
        GroggyBeacon hitBeacon = Projectile.CurrentBeaconHit;
        if (hitBeacon != null) candidates.Remove(hitBeacon);
        if (candidates.Count == 0 && IsValidTarget(hitEnemy)) candidates.Add(hitEnemy);
        if (candidates.Count == 0) return;

        baseDirection.y = 0f;
        if (baseDirection.sqrMagnitude < 0.0001f) baseDirection = Vector3.forward;
        baseDirection.Normalize();

        int n = Mathf.Max(1, count);
        float step = 360f / n;
        for (int i = 0; i < n; i++)
        {
            if (Projectile.LiveSplitCount >= maxLiveSplit) break;
            Vector3 dir = Quaternion.AngleAxis(step * i, Vector3.up) * baseDirection;
            Component target = candidates[i % candidates.Count];
            Spawn(shooting, s, origin, dir, target, childDamage, childSize, childEffectScale, childGeneration, hitEnemy);
        }
    }

    static Projectile Spawn(PlayerShooting shooting, MissileDroneFamiliarEffect s, Vector3 position, Vector3 direction,
                            Component target, float damage, float size, float effectScale, int generation, EnemyHealth enemyToIgnore)
    {
        if (shooting == null || s == null) return null;

        // 비콘 목표는 아이템 훅보다 먼저(Setup과 같은 자리에서) 정합니다.
        var beaconTarget = target as GroggyBeacon;
        return shooting.SpawnMissileProjectile(position, direction, damage, s.missileSpeed, size, target as EnemyHealth,
                                               effectScale, s.damageMultiplier, generation, enemyToIgnore,
                                               p =>
                                               {
                                                   if (beaconTarget != null) p.SetMissileTargetObject(beaconTarget);
                                                   Setup(p, s);
                                               });
    }

    // 아이템 훅보다 먼저: 색(94번 후광이 총알 색을 읽음)과 조종기를 붙입니다.
    static void Setup(Projectile p, MissileDroneFamiliarEffect s)
    {
        bool tinted = false;
        Color tint = s.missileColor;
        if (p.HasRandomDamageRoll)
        {
            tinted = true; // 23번 색은 Launch에서 이미 입혀짐
            tint = p.RandomDamageColor;
        }
        else if (ProjectileColorOverride.TryGetColor(out Color itemColor))
        {
            tinted = true; // 38번 등 총알 색 아이템도 Launch에서 이미 입혀짐
            tint = itemColor;
        }
        else
        {
            ProjectileColorOverride.ApplyColor(p.gameObject, s.missileColor);
        }

        var driver = p.gameObject.AddComponent<HomingMissile>();
        driver.settings = s;
        driver.projectile = p;
        driver.hasSmokeTint = tinted;
        driver.smokeTint = tint;
        driver.smokePrefab = s.smokePrefab != null ? s.smokePrefab : TravelBoostSettings.Get().runDustPrefab;
        driver.lastPosition = p.transform.position;
        Vector3 flat = p.Direction;
        flat.y = 0f;
        driver.heading = flat.sqrMagnitude > 0.0001f ? flat.normalized : Vector3.forward;
        p.OnDespawning += driver.HandleDespawning;
    }

    // ---------------- 비행 ----------------

    void Update()
    {
        if (finished || settings == null || projectile == null) return;
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        age += dt;
        if (age >= settings.maxFlightTime) { Explode(); return; }

        // 발사/분열 직후 겹쳐 태어나도 바로 터지지 않게 잠깐 뒤에 무장합니다.
        if (!projectile.MissileArmed && age >= settings.straightDuration * 0.5f) projectile.ArmMissile();

        // 목표가 사라졌으면 다시 유도하거나 폭발합니다.
        Component target = projectile.MissileTargetObject;
        if (!IsValidTargetFor(target, projectile))
        {
            Component replacement = settings.retargetOnTargetDeath ? FindNearestTarget(transform.position, projectile) : null;
            if (replacement == null) { Explode(); return; }
            projectile.SetMissileTargetObject(replacement);
            target = replacement;
        }

        // [69번] 적에게 튕기면 방향이 그 순간 바뀌므로(튕김), 수평 진행 방향도 튕긴 방향으로 맞춥니다.
        if (projectile.EnemyBounceCount != knownEnemyBounceCount)
        {
            knownEnemyBounceCount = projectile.EnemyBounceCount;
            Vector3 bounced = projectile.Direction;
            bounced.y = 0f;
            if (bounced.sqrMagnitude > 0.0001f) heading = bounced.normalized;
        }

        Vector3 pos = transform.position;
        Vector3 aim = TargetPoint(target);

        // 직진 구간이 끝나면 목표 쪽으로 꺾습니다(꺾는 속도 상한 = 회전 반경).
        // [수평 회전 + 높이 따로 - 형빈 확인 버그] 예전에는 3D 방향을 그대로 목표 쪽으로 돌렸는데, 목표가 뒤쪽 아래에 있으면 가장 짧은 회전이
        // 위/아래로 넘어가는 세로 회전이 되어, 위에서 보면 "직진 → 잠깐 멈춤 → 갑자기 반대로 꺾음"처럼 보였습니다. 그래서 진행 방향은 수평으로만
        // 돌리고(반대쪽이어도 한쪽으로 크게 돌아 회전 반경이 보임), 높이는 남은 수평 거리에 맞춰 완만하게 목표 높이로 맞춥니다.
        if (age > settings.straightDuration)
        {
            Vector3 toTarget = aim - pos;
            Vector3 flatToTarget = new Vector3(toTarget.x, 0f, toTarget.z);
            if (flatToTarget.sqrMagnitude > 0.000001f)
            {
                float rate = settings.turnRate + settings.turnRateGrowth * (age - settings.straightDuration);
                float angle = Vector3.SignedAngle(heading, flatToTarget, Vector3.up);
                float step = Mathf.Clamp(angle, -rate * dt, rate * dt);
                heading = (Quaternion.AngleAxis(step, Vector3.up) * heading).normalized;
            }
        }

        // 높이: 남은 수평 거리에 비례해 목표 높이로 서서히 맞춥니다(가까울수록 기울기가 커지되 상한이 있음).
        float flatDistance = new Vector2(aim.x - pos.x, aim.z - pos.z).magnitude;
        float slope = age > settings.straightDuration
            ? Mathf.Clamp((aim.y - pos.y) / Mathf.Max(flatDistance, 0.25f), -1.5f, 1.5f)
            : 0f;
        projectile.SetMissileDirection(heading + Vector3.up * slope);

        // 콜라이더가 겹치지 않고 스쳐 지나가도 목표 중심 가까이면 명중(지난 프레임부터 지금까지의 이동 구간 기준).
        if (projectile.MissileArmed && SegmentDistance(lastPosition, pos, aim) <= settings.hitRadius)
        {
            projectile.ForceMissileHit();
            if (finished || projectile == null) return;
        }
        lastPosition = pos;

        UpdateSmoke(dt);
    }

    static Vector3 TargetPoint(Component target)
    {
        if (target is GroggyBeacon beacon) return beacon.AimPoint;
        var col = target.GetComponentInChildren<Collider>();
        if (col != null && col.enabled) return col.bounds.center;
        return target.transform.position + Vector3.up * 0.25f;
    }

    static float SegmentDistance(Vector3 a, Vector3 b, Vector3 p)
    {
        Vector3 ab = b - a;
        float len2 = ab.sqrMagnitude;
        if (len2 < 0.0000001f) return (p - a).magnitude;
        float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / len2);
        return (a + ab * t - p).magnitude;
    }

    // ---------------- 목표 ----------------

    public static bool IsValidTarget(EnemyHealth e)
    {
        if (e == null || e.IsDead || !e.isActiveAndEnabled) return false;
        if (AllyFaction.IsAlly(e.gameObject)) return false;
        return true;
    }

    // 적(IsValidTarget) 또는 노릴 수 있는 총알 비콘(missile이 이미 반응한 비콘 제외)인지.
    public static bool IsValidTargetFor(Component target, Projectile missile)
    {
        if (target is GroggyBeacon beacon)
        {
            return beacon != null && beacon.Type == GroggyBeacon.BeaconType.Bullet && beacon.IsTargetable &&
                   (missile == null || !missile.HasReactedToBeacon(beacon));
        }
        return IsValidTarget(target as EnemyHealth);
    }

    // [3레벨 보스 그로기 비콘] 방 안의 적(CollectRoomEnemies)과 노릴 수 있는 총알 비콘을 from에서 가까운 순으로 모읍니다
    // (기존과 같은 피벗 기준 거리 - 비콘 피벗은 바닥. 거리가 같으면 적이 체력 많은 순, 비콘은 그 뒤). missile이 있으면 그 미사일이 이미 반응한 비콘은 뺍니다.
    private static readonly List<EnemyHealth> targetEnemyBuffer = new List<EnemyHealth>();

    public static void CollectRoomTargets(Vector3 from, List<Component> result, Projectile missile)
    {
        result.Clear();
        CollectRoomEnemies(from, targetEnemyBuffer);
        for (int i = 0; i < targetEnemyBuffer.Count; i++) result.Add(targetEnemyBuffer[i]);

        var beacons = GroggyBeacon.Active;
        for (int i = 0; i < beacons.Count; i++)
        {
            var beacon = beacons[i];
            if (beacon == null || !beacon.IsInPlayerRoom() || !IsValidTargetFor(beacon, missile)) continue;
            result.Add(beacon);
        }

        result.Sort((a, b) =>
        {
            float da = (a.transform.position - from).sqrMagnitude;
            float db = (b.transform.position - from).sqrMagnitude;
            if (!Mathf.Approximately(da, db)) return da.CompareTo(db);
            var ea = a as EnemyHealth;
            var eb = b as EnemyHealth;
            if (ea != null && eb != null) return eb.CurrentHealth.CompareTo(ea.CurrentHealth);
            if (ea != null) return -1;
            if (eb != null) return 1;
            return 0;
        });
    }

    // 플레이어가 있는 방의 적을 from에서 가까운 순(같으면 체력 많은 순)으로 모읍니다. 87번 아군 제외, 보스 포함.
    public static void CollectRoomEnemies(Vector3 from, List<EnemyHealth> result)
    {
        result.Clear();
        var playerRoom = RoomScope.CurrentPlayerRoom();
        var all = EnemyHealth.ActiveEnemies;
        if (all == null) return;
        for (int i = 0; i < all.Count; i++)
        {
            var e = all[i];
            if (!IsValidTarget(e)) continue;
            if (playerRoom != null)
            {
                var room = RoomScope.RoomOf(e.transform.position);
                if (room != null && room != playerRoom) continue;
            }
            result.Add(e);
        }

        result.Sort((a, b) =>
        {
            float da = (a.transform.position - from).sqrMagnitude;
            float db = (b.transform.position - from).sqrMagnitude;
            if (!Mathf.Approximately(da, db)) return da.CompareTo(db);
            return b.CurrentHealth.CompareTo(a.CurrentHealth);
        });
    }

    private static readonly List<Component> nearestBuffer = new List<Component>();

    // 다시 유도할 대상: 가장 가까운 적 또는 총알 비콘(이 미사일이 이미 반응한 비콘 제외).
    static Component FindNearestTarget(Vector3 from, Projectile missile)
    {
        CollectRoomTargets(from, nearestBuffer, missile);
        return nearestBuffer.Count > 0 ? nearestBuffer[0] : null;
    }

    // ---------------- 연출 ----------------

    void UpdateSmoke(float dt)
    {
        if (smokePrefab == null) return;
        smokeTimer -= dt;
        if (smokeTimer > 0f) return;
        smokeTimer += Mathf.Max(settings.smokeInterval, 0.01f);

        Vector3 dir = projectile.Direction;
        Vector3 tail = transform.position - dir * (transform.lossyScale.x * 0.5f);
        GameObject puff = Instantiate(smokePrefab, tail, Quaternion.LookRotation(-dir, Vector3.up));
        puff.transform.localScale = smokePrefab.transform.localScale * settings.smokeScale;

        // 기본은 밝은 회색~흰색, 총알 색 아이템이 있으면 그 색(형빈 확정)
        Color a = settings.smokeColorA;
        Color b = settings.smokeColorB;
        if (hasSmokeTint)
        {
            a = new Color(smokeTint.r, smokeTint.g, smokeTint.b, 1f);
            b = Color.Lerp(a, Color.white, 0.35f);
        }
        foreach (var ps in puff.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = ps.main;
            main.startColor = new ParticleSystem.MinMaxGradient(a, b);
        }
        FamiliarVfx.Restart(puff);

        RoomScope.DestroyWhenPlayerLeaves(puff);
        Destroy(puff, settings.smokeLifetime);
    }

    // 적에게 맞을 때마다(69번으로 튕겨 계속 날아가도) 명중 이펙트/소리를 냅니다. 조용한 소멸(비행 시간 초과)은 Explode가 따로 냅니다.
    void HandleDespawning(ProjectileDespawnEvent e)
    {
        if (e.Reason != ProjectileDespawnReason.HitEnemy) return;
        SpawnHitVfx(e.Position);
        GameAudio.Play(SfxId.Missile63Hit); // [사운드] 63번 미사일 명중
    }

    // 목표가 없거나 비행 시간이 다 돼 그 자리에서 폭발(피해 없음 - 형빈 확정).
    void Explode()
    {
        if (finished) return;
        finished = true;
        SpawnHitVfx(transform.position);
        GameAudio.Play(SfxId.Missile63Hit);
        if (projectile != null) projectile.DespawnQuietly();
    }

    void SpawnHitVfx(Vector3 position)
    {
        if (settings == null || settings.hitVfxPrefab == null) return;
        GameObject vfx = Instantiate(settings.hitVfxPrefab, position, Quaternion.identity);
        FamiliarVfx.ApplySizeMultiplier(vfx, settings.hitVfxScale);
        FamiliarVfx.Restart(vfx);
        RoomScope.DestroyWhenPlayerLeaves(vfx);
        Destroy(vfx, settings.hitVfxLifetime);
    }

    void OnDestroy()
    {
        finished = true;
        if (projectile != null) projectile.OnDespawning -= HandleDespawning;
    }
}
