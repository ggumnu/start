using System.Collections.Generic;
using UnityEngine;

// [108번 폭죽 포대 - 설치된 포대 하나]
// FireworkTurretActiveEffect.Activate가 설치할 때마다 하나씩 만듭니다(씬/프리팹에 미리 둘 필요 없음). 기획은 Item108effect.cs 참고.
//
// [구조]
// 빈 루트(이 컴포넌트) 아래에 포대 모델을 붙입니다. 루트 전체가 수평으로 목표를 향해 돌고, 포구 부품(Object42)만 자기 피벗을 축으로
// 위아래로 기웁니다. 포구 방향은 FirePoint들의 파란 화살표(Z) 방향으로 알아냅니다(모델을 내보낸 축과 상관없이 동작).
// - 기우는 각도 = 폭죽탄 포물선이 FirePoint를 떠나는 순간의 각도(Grenade.EvaluateArc와 같은 공식) - 포구가 날아가는 방향을 바라봅니다.
// - 반동: 한 발마다 포구가 포신 방향 뒤로 밀렸다가 돌아오고("투투투투"), 4발을 다 쏘면 포구가 위로 들렸다가 내려옵니다.
// - 콜라이더/Rigidbody는 지웁니다(통과 가능, 파괴 불가). 적의 공격 대상에도 들어가지 않습니다.
// - 플레이어가 다른 방에 들어가면 즉시 지웁니다(사라지는 연출 없음 - 형빈 확정).
// - [3레벨 보스 그로기 비콘 - 형빈 확정] 수류탄 비콘이 남아 있으면 적(보스)보다 먼저 가장 가까운 수류탄 비콘을 쏩니다(바리케이드처럼
//   흩뿌리지 않고 몸의 가장 가까운 점). 다 해제되면 평소대로 가장 먼 적.
public class FireworkTurret : MonoBehaviour
{
    private static readonly List<EnemyHealth> enemyBuffer = new List<EnemyHealth>();

    private FireworkTurretActiveEffect s;
    private PlayerGrenadeThrower thrower;
    private RoomInstance room;

    private Transform muzzle;
    private readonly List<Transform> firePoints = new List<Transform>();
    private Quaternion muzzleRestLocalRotation;
    private Vector3 muzzleRestLocalPosition;
    private Vector3 barrelInMuzzle = Vector3.forward;   // 포구 기준 포신 방향(포구 로컬)
    private Quaternion barrelYawInRoot = Quaternion.identity; // 루트 기준 포신의 수평 방향

    private float age;
    private int volleysStarted;
    private int shotsLeft;
    private float shotTimer;
    private int shotIndex;

    private EnemyHealth lockedEnemy;
    private GroggyBeacon lockedBeacon;
    private SpecialRoomLock lockedBarricade;
    private bool hasAim;
    private Vector3 aimPoint;

    private float currentPitch;
    private float desiredPitch;
    private float kick;
    private float nodTimer = -1f;
    private bool vanishing;

    // ---------------- 생성 ----------------

    public static FireworkTurret Spawn(FireworkTurretActiveEffect settings, PlayerGrenadeThrower thrower, Transform player)
    {
        if (settings == null || thrower == null || player == null) return null;

        Vector3 forward = player.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
        forward.Normalize();

        Vector3 pos = player.position + forward * settings.spawnForwardDistance;
        pos = thrower.ClampToRoomBounds(player.position, pos);
        pos.y = player.position.y;

        var go = new GameObject("Drone_Umika (108)");
        go.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(forward, Vector3.up));
        var turret = go.AddComponent<FireworkTurret>();
        turret.Init(settings, thrower);
        return turret;
    }

    void Init(FireworkTurretActiveEffect settings, PlayerGrenadeThrower owner)
    {
        s = settings;
        thrower = owner;
        room = RoomScope.CurrentPlayerRoom();

        if (s.turretModel != null)
        {
            GameObject m = Instantiate(s.turretModel, transform);
            m.name = s.turretModel.name;
            m.transform.localPosition = Vector3.zero;
            m.transform.localRotation = Quaternion.Euler(s.modelRotationOffset);
            m.transform.localScale = s.turretModel.transform.localScale * s.modelScale;

            foreach (var rb in m.GetComponentsInChildren<Rigidbody>(true)) Destroy(rb);
            foreach (var col in m.GetComponentsInChildren<Collider>(true)) Destroy(col);

            muzzle = FindChild(m.transform, s.muzzlePartName);
            if (s.firePointNames != null)
            {
                foreach (var n in s.firePointNames)
                {
                    var fp = FindChild(m.transform, n);
                    if (fp != null) firePoints.Add(fp);
                }
            }
            if (muzzle == null)
                Debug.LogWarning($"FireworkTurret: 모델에서 포구 '{s.muzzlePartName}'를 찾지 못해 포구가 기울거나 반동하지 않습니다(발사는 동작함).");
            if (firePoints.Count == 0)
                Debug.LogWarning("FireworkTurret: 모델에서 FirePoint를 찾지 못해 포구(없으면 포대) 위치에서 쏩니다.");
        }
        else
        {
            Debug.LogWarning("FireworkTurret: 108번의 Turret Model 칸이 비어 있어 포대가 보이지 않습니다(발사는 동작함).");
        }

        CacheRestPose();

        // 설치하는 순간 목표가 있으면 바로 그쪽을 보고 나타납니다(나타나자마자 도는 모습이 보이지 않게).
        ResolveTarget();
        if (hasAim)
        {
            Vector3 flat = aimPoint - transform.position;
            flat.y = 0f;
            if (flat.sqrMagnitude > 0.0001f) transform.rotation = DesiredRootRotation(flat);
            desiredPitch = ComputeLaunchPitch();
            currentPitch = desiredPitch;
        }
        ApplyMuzzlePose();

        var appear = AppearFromWhite.Begin(gameObject);
        if (appear != null) appear.duration = s.appearDuration;

        RoomController.OnAnyRoomEntered -= HandleRoomEntered;
        RoomController.OnAnyRoomEntered += HandleRoomEntered;
    }

    void CacheRestPose()
    {
        Vector3 barrelWorld = transform.forward;
        if (firePoints.Count > 0)
        {
            Vector3 sum = Vector3.zero;
            foreach (var fp in firePoints) sum += fp.forward;
            if (sum.sqrMagnitude > 0.0001f) barrelWorld = sum.normalized;
        }

        if (muzzle != null)
        {
            muzzleRestLocalRotation = muzzle.localRotation;
            muzzleRestLocalPosition = muzzle.localPosition;
            barrelInMuzzle = Quaternion.Inverse(muzzle.rotation) * barrelWorld;
        }

        Vector3 flat = barrelWorld;
        flat.y = 0f;
        if (flat.sqrMagnitude < 0.0001f) flat = transform.forward;
        Vector3 local = Quaternion.Inverse(transform.rotation) * flat.normalized;
        local.y = 0f;
        barrelYawInRoot = local.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(local.normalized, Vector3.up) : Quaternion.identity;

        currentPitch = Mathf.Asin(Mathf.Clamp(barrelWorld.y, -1f, 1f)) * Mathf.Rad2Deg;
        desiredPitch = currentPitch;
    }

    static Transform FindChild(Transform root, string childName)
    {
        if (root == null || string.IsNullOrEmpty(childName)) return null;
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == childName) return t;
        }
        return null;
    }

    void OnDestroy()
    {
        RoomController.OnAnyRoomEntered -= HandleRoomEntered; // 정적 알림이라 반드시 해제
    }

    // 플레이어가 다른 방에 들어가면 곧바로 사라집니다(사라지는 중이어도).
    void HandleRoomEntered(RoomInstance entered)
    {
        if (this == null) return;
        if (room != null && !ReferenceEquals(entered, room)) Destroy(gameObject);
    }

    // ---------------- 매 프레임 ----------------

    void Update()
    {
        if (room != null && !RoomScope.IsPlayerInRoom(room))
        {
            Destroy(gameObject);
            return;
        }

        float dt = Time.deltaTime;
        age += dt;

        ResolveTarget();
        if (hasAim) desiredPitch = ComputeLaunchPitch();

        if (!vanishing)
        {
            float nextVolley = s.firstFireDelay + volleysStarted * s.volleyInterval;
            if (age >= nextVolley && nextVolley < s.lifetime - 0.0001f)
            {
                volleysStarted++;
                if (hasAim)
                {
                    shotsLeft = Mathf.Max(1, s.shotsPerVolley);
                    shotTimer = 0f;
                }
            }

            if (shotsLeft > 0)
            {
                shotTimer -= dt;
                while (shotsLeft > 0 && shotTimer <= 0f)
                {
                    FireShot();
                    shotsLeft--;
                    shotTimer += s.shotInterval;
                    if (shotsLeft == 0) nodTimer = 0f;
                }
            }

            if (age >= s.lifetime && shotsLeft == 0) BeginVanish();
        }

        UpdatePose(dt);
    }

    // ---------------- 목표 ----------------

    // 락온 유지: 쫓던 적이 살아 있고 이 방에 있으면 그대로, 아니면 가장 먼 적. 적이 없으면 잠긴 수류탄 바리케이드(가까운 것부터).
    // [3레벨 보스 그로기 비콘] 노릴 수 있는 수류탄 비콘이 있으면 그것이 가장 먼저입니다.
    void ResolveTarget()
    {
        if (lockedBeacon == null || !lockedBeacon.IsTargetable)
        {
            lockedBeacon = GroggyBeacon.FindNearest(transform.position, GroggyBeacon.BeaconType.Grenade);
        }
        if (lockedBeacon != null)
        {
            lockedEnemy = null;
            lockedBarricade = null;
            hasAim = true;
            aimPoint = lockedBeacon.ClosestFlatPoint(transform.position);
            aimPoint.y = transform.position.y;
            return;
        }

        if (!IsTargetEnemy(lockedEnemy))
        {
            lockedEnemy = FindFarthestEnemy(transform.position);
        }

        if (lockedEnemy != null)
        {
            lockedBarricade = null;
            hasAim = true;
            aimPoint = lockedEnemy.transform.position;
            aimPoint.y = transform.position.y;
            return;
        }

        if (!IsTargetBarricade(lockedBarricade, room)) lockedBarricade = FindNearestBarricade(transform.position, room);
        if (lockedBarricade != null)
        {
            hasAim = true;
            aimPoint = ClosestPointOnBarricade(lockedBarricade, transform.position);
            aimPoint.y = transform.position.y;
            return;
        }

        hasAim = false;
    }

    static bool IsTargetEnemy(EnemyHealth e)
    {
        if (!HomingMissile.IsValidTarget(e)) return false;
        var playerRoom = RoomScope.CurrentPlayerRoom();
        if (playerRoom == null) return true;
        var r = RoomScope.RoomOf(e.transform.position);
        return r == null || ReferenceEquals(r, playerRoom);
    }

    static EnemyHealth FindFarthestEnemy(Vector3 from)
    {
        // CollectRoomEnemies = 플레이어가 있는 방의 적(87번 아군 제외, 보스 포함), 가까운 순(같으면 체력 많은 순) → 마지막이 가장 먼 적.
        HomingMissile.CollectRoomEnemies(from, enemyBuffer);
        if (enemyBuffer.Count == 0) return null;
        float farthest = (enemyBuffer[enemyBuffer.Count - 1].transform.position - from).sqrMagnitude;
        // 같은 거리면 체력이 많은 쪽(정렬상 같은 거리 묶음의 맨 앞)
        for (int i = 0; i < enemyBuffer.Count; i++)
        {
            if (Mathf.Approximately((enemyBuffer[i].transform.position - from).sqrMagnitude, farthest)) return enemyBuffer[i];
        }
        return enemyBuffer[enemyBuffer.Count - 1];
    }

    // 잠긴 수류탄 특수방 바리케이드인지(켜져 있음 = 플레이어가 그 부모 방에 있음, 방을 알면 그 방의 것만).
    static bool IsTargetBarricade(SpecialRoomLock lockItem, RoomInstance turretRoom)
    {
        if (lockItem == null || !lockItem.IsLocked) return false;
        if (lockItem.Condition != SpecialRoomEntryCondition.Grenade) return false;
        if (!lockItem.gameObject.activeInHierarchy) return false;
        if (turretRoom != null && !ReferenceEquals(lockItem.ParentRoom, turretRoom)) return false;
        return true;
    }

    static SpecialRoomLock FindNearestBarricade(Vector3 from, RoomInstance turretRoom)
    {
        SpecialRoomLock best = null;
        float bestDist = float.MaxValue;
        var all = SpecialRoomLock.All;
        for (int i = 0; i < all.Count; i++)
        {
            var l = all[i];
            if (!IsTargetBarricade(l, turretRoom)) continue;
            Vector3 p = ClosestPointOnBarricade(l, from);
            p.y = from.y;
            float d = (p - from).sqrMagnitude;
            if (d < bestDist)
            {
                bestDist = d;
                best = l;
            }
        }
        return best;
    }

    // 바리케이드 몸체(콜라이더, 없으면 렌더러)에서 from에 가장 가까운 바닥 평면 위의 점입니다. 폭발 반경이 작아도 반드시 걸리도록 이 점을 쏩니다.
    static Vector3 ClosestPointOnBarricade(SpecialRoomLock lockItem, Vector3 from)
    {
        bool found = false;
        Vector3 best = lockItem.transform.position;
        float bestDist = float.MaxValue;

        foreach (var col in lockItem.GetComponentsInChildren<Collider>())
        {
            if (col == null || !col.enabled) continue;
            found = true;
            ConsiderBounds(col.bounds, from, ref best, ref bestDist);
        }
        if (!found)
        {
            foreach (var rend in lockItem.GetComponentsInChildren<Renderer>())
            {
                if (rend == null || !rend.enabled) continue;
                ConsiderBounds(rend.bounds, from, ref best, ref bestDist);
            }
        }
        return best;
    }

    static void ConsiderBounds(Bounds b, Vector3 from, ref Vector3 best, ref float bestDist)
    {
        var p = new Vector3(Mathf.Clamp(from.x, b.min.x, b.max.x), from.y, Mathf.Clamp(from.z, b.min.z, b.max.z));
        float d = (p - from).sqrMagnitude;
        if (d < bestDist)
        {
            bestDist = d;
            best = p;
        }
    }

    // 108번 사용 조건: 플레이어가 있는 방에 공격할 적이 있거나, 잠긴 수류탄 특수방 바리케이드가 있음.
    public static bool HasAnyTargetInPlayerRoom(Vector3 from)
    {
        HomingMissile.CollectRoomEnemies(from, enemyBuffer);
        if (enemyBuffer.Count > 0) return true;
        return FindNearestBarricade(from, RoomScope.CurrentPlayerRoom()) != null;
    }

    // ---------------- 발사 ----------------

    void FireShot()
    {
        if (thrower == null || thrower.stats == null) return;

        ResolveTarget();
        if (!hasAim) return; // 쏘는 도중 목표가 모두 사라지면 남은 발은 쏘지 않음

        Vector3 floor = transform.position;
        Vector3 landing;
        if (lockedEnemy != null)
        {
            landing = lockedEnemy.transform.position;
            Vector2 r = Random.insideUnitCircle * s.scatterRadius;
            landing.x += r.x;
            landing.z += r.y;
            landing.y = floor.y;
            landing = thrower.ClampToRoomBounds(floor, landing);
        }
        else
        {
            landing = aimPoint; // 바리케이드/수류탄 비콘: 흩뿌리지 않고 몸체의 가장 가까운 점(방 안쪽 보정도 하지 않음)
            landing.y = floor.y;
        }

        Vector3 origin = firePoints.Count > 0
            ? firePoints[shotIndex % firePoints.Count].position
            : (muzzle != null ? muzzle.position : transform.position + Vector3.up * 0.5f);
        shotIndex++;

        float damage = thrower.stats.GrenadeDamage * s.damageRatio;
        float radius = thrower.CurrentRadius * s.radiusRatio;
        Color color = s.firework != null ? s.firework.PickColor() : Color.white;

        thrower.SpawnFireworkShell(s.firework, color, origin, landing, s.flightTime, s.arcHeight, damage, s.selfDamage, radius);
        GameAudio.Play(SfxId.Firework108Launch);

        kick = s.kickDistance;
    }

    // ---------------- 자세(회전/반동) ----------------

    Quaternion DesiredRootRotation(Vector3 flatDirection)
    {
        return Quaternion.LookRotation(flatDirection.normalized, Vector3.up) * Quaternion.Inverse(barrelYawInRoot);
    }

    // 폭죽탄이 발사 위치를 떠나는 순간의 각도입니다(Grenade.EvaluateArc를 u로 미분: 수평 = 거리, 수직 = 높이 차 + 4 x 최고점 높이).
    float ComputeLaunchPitch()
    {
        Vector3 origin = AverageFirePoint();
        Vector3 d = aimPoint - origin;
        float horizontal = new Vector2(d.x, d.z).magnitude;
        float vertical = d.y + 4f * s.arcHeight;
        float pitch = Mathf.Atan2(vertical, Mathf.Max(horizontal, 0.01f)) * Mathf.Rad2Deg;
        return Mathf.Clamp(pitch, s.minPitch, s.maxPitch);
    }

    Vector3 AverageFirePoint()
    {
        if (firePoints.Count == 0) return muzzle != null ? muzzle.position : transform.position + Vector3.up * 0.5f;
        Vector3 sum = Vector3.zero;
        foreach (var fp in firePoints) sum += fp.position;
        return sum / firePoints.Count;
    }

    void UpdatePose(float dt)
    {
        if (hasAim)
        {
            Vector3 flat = aimPoint - transform.position;
            flat.y = 0f;
            if (flat.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, DesiredRootRotation(flat), s.turnSpeed * dt);
        }

        currentPitch = Mathf.MoveTowards(currentPitch, desiredPitch, s.pitchSpeed * dt);
        kick = Mathf.Lerp(kick, 0f, 1f - Mathf.Exp(-s.kickRecoverSpeed * dt));

        ApplyMuzzlePose();

        if (nodTimer >= 0f)
        {
            nodTimer += dt;
            if (nodTimer > s.nodRiseTime + s.nodFallTime) nodTimer = -1f;
        }
    }

    float NodAmount()
    {
        if (nodTimer < 0f) return 0f;
        if (nodTimer < s.nodRiseTime) return Mathf.SmoothStep(0f, 1f, nodTimer / s.nodRiseTime);
        return Mathf.SmoothStep(1f, 0f, (nodTimer - s.nodRiseTime) / s.nodFallTime);
    }

    // 포구를 쉬는 자세로 되돌린 뒤, 포신을 (기울기 + 들림)만큼 자기 피벗을 축으로 위로 돌리고 포신 방향 뒤로 밀어냅니다.
    void ApplyMuzzlePose()
    {
        if (muzzle == null) return;

        muzzle.localRotation = muzzleRestLocalRotation;
        muzzle.localPosition = muzzleRestLocalPosition;

        Vector3 b0 = muzzle.rotation * barrelInMuzzle;
        float restPitch = Mathf.Asin(Mathf.Clamp(b0.y, -1f, 1f)) * Mathf.Rad2Deg;
        Vector3 flat = new Vector3(b0.x, 0f, b0.z);
        if (flat.sqrMagnitude < 0.0001f) flat = transform.forward;
        Vector3 right = Vector3.Cross(Vector3.up, flat.normalized);

        float total = currentPitch + s.nodAngle * NodAmount();
        muzzle.rotation = Quaternion.AngleAxis(-(total - restPitch), right) * muzzle.rotation;

        if (kick > 0.0001f)
        {
            Vector3 barrel = muzzle.rotation * barrelInMuzzle;
            muzzle.position -= barrel * kick;
        }
    }

    // ---------------- 사라짐 ----------------

    void BeginVanish()
    {
        if (vanishing) return;
        vanishing = true;
        var appear = GetComponent<AppearFromWhite>();
        if (appear != null) Destroy(appear);
        var dissolve = DissolveAndVanish.Begin(gameObject, DissolveAndVanish.EndAction.Destroy);
        if (dissolve != null) dissolve.duration = s.vanishDuration;
    }
}
