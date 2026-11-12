using System.Collections.Generic;
using UnityEngine;

// [118번 포격 - 포격 하나]
// Item118effect가 플레이어 총알이 적에게 맞을 때마다 그 자리에 하나씩 만듭니다(직접 배치할 필요 없음).
//
// [진행 - 형빈 확정]
// 1) 조준(기본 2초): 맞은 자리 바닥에 조준 표시(빨간 반투명 원 + 빨간 테두리 + 안쪽 원 테두리 + 가운데 십자선)와, 원 중심에서 하늘로 빠르게
//    점멸하는 빨간 레이저를 띄웁니다. 맞은 적을 따라가지 않고 그 자리에 고정입니다.
//    - 77번 보유 중이면 조준하는 동안 조준 원이 가장 가까운 적에게 다가갑니다(77번 수류탄 유도와 같은 규칙 - 플레이어 이동속도 x 77번 배율,
//      "Wall"에 막히면 벽을 따라 미끄러짐). 터진 뒤에는 유도하지 않습니다.
// 2) 마지막 fallTime(기본 0.5초) 동안 하늘에서 포탄(수류탄 모델 x 1.2배)이 떨어지고, 땅에 닿는 순간(조준 시작 2초 뒤) 터집니다.
// 3) 폭발은 수류탄 폭발과 같은 처리입니다(Grenade.Explode와 같은 순서):
//    - 적: 원기둥(반경 x 높이 explosionHeight) 안의 모든 적에게 수류탄 피해.
//    - 플레이어: 범위 안이면 수류탄 자해 피해(57번 등 자해 무효면 없음, 74번 돌진 중이면 없음).
//    - 81번 소이탄(맞은 순간 보유): 적 피해 x 비율, 자해 = 고정값, 폭발 뒤 불타는 구역.
//    - 폭발 이펙트 + 수류탄 폭발 알림(PlayerGrenadeThrower.NotifyExploded) - 66번 해킹/57번 외형·토끼/특수방 수류탄 잠금 해제가 그대로 적용됩니다.
// 4) 92번(맞은 순간 보유): 폭발이 끝나면 그 자리에서 "총알이 날아가던 방향" 기준 X자 4방향에 분열 포격 4개를 새로 시작합니다(각자 2초 조준 후
//    폭발). 분열 포격의 피해/범위/자해는 92번 비율(자해는 0.5 단위 올림)이고 다시 분열하지 않습니다.
// - 피해/범위/자해는 총알이 맞은 순간의 수류탄 스탯입니다(수류탄 개수는 쓰지 않음). 5번 분열탄이 맞혀 생긴 포격은 그 분열탄의 효과 배율
//   (EffectScale - 세대마다 50%)만큼 피해/범위/자해가 줄어듭니다(자해는 0.5 단위 올림).
// - 플레이어가 다른 방에 들어가면 조준 중인(크레모어가 아닌) 포격은 취소됩니다(폭발/분열 없음).
//
// [115번 크레모어 포격 - 형빈 확정]
// 총알이 맞은 순간 115번 보유 중이면 크레모어 포격입니다.
// - 조준 표시만 깔고 기다립니다(시간 제한 없음). 수류탄 모드 R(ClaymoreDetonator)로 그 방의 크레모어를 터뜨릴 때, 이 방에 깔린 크레모어
//   포격도 모두 포탄이 떨어지기 시작해 fallTime 뒤에 터집니다. 115번을 잃으면 모든 방의 크레모어 포격이 바로 떨어지기 시작합니다.
// - 다른 방으로 옮겨도 남습니다(그 방에 있을 때만 보임 - 115번 크레모어와 같음). 다음 레벨로 넘어가 방이 사라지면 조용히 없어집니다.
// - [방 전환] 플레이어가 그 방에 없을 때 터지면(115번을 잃어 모든 방의 포격이 떨어지는 경우 등) 수류탄과 같은 "조용한 폭발"입니다:
//   폭발 이펙트/적 피해/자해/66번/57번/81번 불 구역/92번 분열을 건너뛰고 그 방의 특수방 잠금 해제만 합니다(Grenade.cs [방 전환] 참고).
// - 77번: 터뜨릴 때까지 시간 제한 없이, 그 방의 적만 쫓습니다.
// - 92번: 첫 크레모어 포격이 깔리고 2초(prepTime) 뒤에 X자 4개의 분열 크레모어 포격이 깔립니다. 깔리기 전에는 R을 눌러도 분열 포격은
//   터지지 않으므로, 첫 포격이 먼저 터진 뒤에도 분열 포격이 깔려 남을 수 있습니다. 깔리기 전에 115번을 잃으면 분열 포격은 일반 포격
//   (2초 조준 후 폭발)으로 깔립니다(날아가던 크레모어가 일반 수류탄이 되는 것과 같은 규칙).
public class ArtilleryStrike : MonoBehaviour
{
    // Item118effect 인스펙터에서 조절하는 포격 설정입니다.
    [System.Serializable]
    public class Settings
    {
        [Header("시간")]
        [Tooltip("총알이 맞은 뒤 포탄이 땅에 닿아 터지기까지의 시간(초)입니다(기본 2). 92번 분열 포격도 각자 이만큼 조준합니다.")]
        [Min(0.1f)] public float prepTime = 2f;

        [Tooltip("포탄이 하늘에서 떨어지는 시간(초)입니다(조준 시간의 마지막 부분 - 기본 0.5). 크레모어 포격은 R을 누른 뒤 이만큼 떨어지고 터집니다.")]
        [Min(0.05f)] public float fallTime = 0.5f;

        [Header("포탄 (수류탄 모델)")]
        [Tooltip("포탄이 떨어지기 시작하는 높이(바닥 기준, m)입니다.")]
        [Min(0.5f)] public float dropHeight = 12f;

        [Tooltip("포탄 크기 = 수류탄 모델 크기 x 이 값입니다(기본 1.2).")]
        [Min(0.01f)] public float shellScale = 1.2f;

        [Header("조준 표시 (바닥)")]
        [Tooltip("범위 안쪽(반투명) 색입니다.")]
        public Color fillColor = new Color(1f, 0.08f, 0.08f, 0.22f);

        [Tooltip("바깥 테두리/안쪽 원/십자선 색입니다.")]
        public Color lineColor = new Color(1f, 0.1f, 0.1f, 0.95f);

        [Tooltip("바깥 테두리 두께(m)입니다.")]
        [Min(0.001f)] public float outlineWidth = 0.06f;

        [Tooltip("안쪽 원 반지름 = 범위 반지름 x 이 값입니다.")]
        [Range(0.05f, 0.95f)] public float innerRingRatio = 0.5f;

        [Tooltip("안쪽 원/십자선 두께(m)입니다.")]
        [Min(0.001f)] public float innerLineWidth = 0.04f;

        [Tooltip("십자선 한쪽 길이 = 범위 반지름 x 이 값입니다(가운데에서 끝까지).")]
        [Range(0.05f, 1f)] public float crossRatio = 0.3f;

        [Tooltip("바닥에서 띄우는 높이(m)입니다(바닥에 묻혀 깜빡이면 올리세요).")]
        public float groundOffset = 0.035f;

        [Header("레이저 (원 중심 → 하늘, 빠르게 점멸)")]
        public Color laserColor = new Color(1f, 0.1f, 0.1f, 0.85f);

        [Tooltip("레이저 굵기(m)입니다.")]
        [Min(0.001f)] public float laserWidth = 0.05f;

        [Tooltip("레이저 길이(바닥에서 위로, m)입니다.")]
        [Min(0.5f)] public float laserHeight = 20f;

        [Tooltip("레이저가 켜짐/꺼짐을 바꾸는 간격(초)입니다(작을수록 빠르게 점멸).")]
        [Min(0.01f)] public float laserBlinkInterval = 0.06f;

        [Tooltip("점멸할 때 꺼진 순간의 밝기 비율입니다(0 = 완전히 꺼짐).")]
        [Range(0f, 1f)] public float laserMinBrightness = 0.1f;
    }

    private const int CircleSegments = 64;
    private const float WallSkin = 0.01f;
    private const float TargetSearchInterval = 0.1f;
    private const float RoomCheckInterval = 0.5f;
    private const string DetonatorKey = "artillery_118";
    private static readonly RaycastHit[] wallHitBuffer = new RaycastHit[16];
    private static Mesh sharedDiscMesh;

    // 크레모어 포격 목록(R/115번 잃기 반응용)
    private static readonly List<ArtilleryStrike> claymoreStrikes = new List<ArtilleryStrike>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        claymoreStrikes.Clear();
        sharedDiscMesh = null;
    }

    private enum Phase { Aiming, Waiting, Falling, Done }

    // ---- 포격 값 ----
    private PlayerGrenadeThrower owner;
    private Settings settings;
    private Vector3 center;            // 바닥 중심(유도하면 움직임)
    private float groundY;
    private float enemyDamage;
    private float selfDamage;
    private float radius;
    private float explosionHeight;
    private string enemyTag;
    private Vector3 forward;           // 분열 X자 기준 방향(수평 단위)
    private IncendiaryGrenadeOverride.Settings incendiary;
    private GrenadeSplitOverride.Settings split;   // null이면 분열 없음
    private ClaymoreOverride.Settings claymore;     // null이면 일반 포격
    private RoomInstance claymoreRoom;

    // ---- 진행 상태 ----
    private Phase phase;
    private float timer;               // Aiming: 조준 경과 / Falling: 낙하 경과
    private int createdFrame;
    private bool splitPending;         // [115번 + 92번] 깔린 뒤 prepTime이 지나면 분열 크레모어 포격을 깔아야 함
    private float splitTimer;
    private bool splitAsClaymore;      // 분열 포격을 크레모어로 깔지(115번을 잃으면 false)
    private bool visible = true;
    private float roomCheckTimer;

    // ---- 77번 유도 ----
    private EnemyHealth homingTarget;
    private float targetSearchTimer;

    // ---- 표시 ----
    private GameObject markerRoot;
    private LineRenderer laser;
    private Material fillMaterial, lineMaterial, laserMaterial;
    private GameObject shell;
    private Material shellMaterial;
    private float blinkTimer;
    private bool laserBright = true;

    public bool IsClaymore => claymore != null;

    // ------------------------------------------------------------------
    // 생성
    // ------------------------------------------------------------------

    // 포격 하나를 시작합니다. 크레모어(claymoreSettings != null)면 조준 표시만 깔고 R을 기다립니다.
    public static ArtilleryStrike Create(PlayerGrenadeThrower owner, Settings settings, Vector3 groundPoint, Vector3 forward,
                                         float enemyDamage, float selfDamage, float radius,
                                         IncendiaryGrenadeOverride.Settings incendiary, GrenadeSplitOverride.Settings split,
                                         ClaymoreOverride.Settings claymore, RoomInstance claymoreRoom)
    {
        if (owner == null || settings == null || radius <= 0f) return null;

        var go = new GameObject("ArtilleryStrike");
        go.transform.position = groundPoint;
        var strike = go.AddComponent<ArtilleryStrike>();
        strike.Init(owner, settings, groundPoint, forward, enemyDamage, selfDamage, radius, incendiary, split, claymore, claymoreRoom);
        return strike;
    }

    private void Init(PlayerGrenadeThrower owner, Settings settings, Vector3 groundPoint, Vector3 forward,
                      float enemyDamage, float selfDamage, float radius,
                      IncendiaryGrenadeOverride.Settings incendiary, GrenadeSplitOverride.Settings split,
                      ClaymoreOverride.Settings claymore, RoomInstance claymoreRoom)
    {
        this.owner = owner;
        this.settings = settings;
        center = groundPoint;
        groundY = groundPoint.y;
        this.enemyDamage = enemyDamage;
        this.selfDamage = selfDamage;
        this.radius = radius;
        explosionHeight = owner.explosionHeight;
        enemyTag = owner.enemyTag;
        forward.y = 0f;
        this.forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
        this.incendiary = incendiary;
        this.claymore = claymore;
        this.claymoreRoom = claymoreRoom;
        createdFrame = Time.frameCount;

        BuildMarker();

        if (claymore != null)
        {
            phase = Phase.Waiting;
            claymoreStrikes.Add(this);
            ClaymoreOverride.SetExternalDetonator(DetonatorKey, DetonateClaymores);

            // [92번] 깔리고 prepTime 뒤에 분열 크레모어 포격을 깝니다. 이 포격이 나중에 터질 때는 분열하지 않습니다.
            if (split != null)
            {
                this.split = split;
                splitPending = true;
                splitTimer = 0f;
                splitAsClaymore = true;
            }
        }
        else
        {
            phase = Phase.Aiming;
            this.split = split;
        }
        timer = 0f;
    }

    void OnEnable()
    {
        RoomController.OnAnyRoomEntered += HandleRoomEntered;
    }

    void OnDisable()
    {
        RoomController.OnAnyRoomEntered -= HandleRoomEntered;
    }

    void OnDestroy()
    {
        claymoreStrikes.Remove(this);
        DestroyVisuals();
    }

    // 일반 포격은 플레이어가 다른 방에 들어가면 취소됩니다(이 프레임에 생긴 것은 남김). 크레모어 포격은 남습니다.
    void HandleRoomEntered(RoomInstance room)
    {
        if (claymore != null || splitPending) return;
        if (Time.frameCount == createdFrame) return;
        Destroy(gameObject);
    }

    // ------------------------------------------------------------------
    // 진행
    // ------------------------------------------------------------------

    void Update()
    {
        if (owner == null) { Destroy(gameObject); return; }

        // [115번] 크레모어 포격(또는 분열을 기다리는 것): 방이 사라졌으면 조용히 없애고, 다른 방이면 숨깁니다.
        if (claymoreRoom != null && (claymore != null || splitPending))
        {
            roomCheckTimer -= Time.deltaTime;
            if (roomCheckTimer <= 0f)
            {
                roomCheckTimer = RoomCheckInterval;
                if (ClaymoreOverride.IsRoomGone(claymoreRoom)) { Destroy(gameObject); return; }
            }
            bool shouldShow = ReferenceEquals(claymoreRoom, ClaymoreOverride.CurrentPlayerRoom());
            if (shouldShow != visible) SetVisible(shouldShow);
        }

        // [115번 + 92번] 깔린 뒤 prepTime이 지나면 분열 포격을 깝니다(첫 포격이 이미 터졌어도).
        if (splitPending)
        {
            splitTimer += Time.deltaTime;
            if (splitTimer >= settings.prepTime)
            {
                splitPending = false;
                SpawnSplitStrikes(splitAsClaymore);
                split = null;
                if (phase == Phase.Done) { Destroy(gameObject); return; }
            }
        }

        switch (phase)
        {
            case Phase.Aiming:
                timer += Time.deltaTime;
                UpdateHoming();
                if (shell == null && timer >= settings.prepTime - settings.fallTime) BeginShell();
                UpdateShell(Mathf.InverseLerp(settings.prepTime - settings.fallTime, settings.prepTime, timer));
                if (timer >= settings.prepTime) Explode();
                break;

            case Phase.Waiting:
                UpdateHoming();
                break;

            case Phase.Falling:
                timer += Time.deltaTime;
                UpdateHoming();
                UpdateShell(timer / settings.fallTime);
                if (timer >= settings.fallTime) Explode();
                break;
        }

        if (phase != Phase.Done) UpdateVisuals();
    }

    // [115번] R(그 방) / 115번 잃기(모든 방)로 불립니다. 기다리던 크레모어 포격의 포탄을 떨어뜨리기 시작하고 그 개수를 돌려줍니다.
    // 115번을 잃을 때는 아직 깔리지 않은 분열 포격도 일반 포격으로 깔리게 바꿉니다.
    private static int DetonateClaymores(RoomInstance room, bool allRooms)
    {
        int count = 0;
        foreach (var strike in new List<ArtilleryStrike>(claymoreStrikes))
        {
            if (strike == null) continue;
            if (!allRooms && !ReferenceEquals(strike.claymoreRoom, room)) continue;

            if (allRooms) strike.splitAsClaymore = false;
            if (strike.phase == Phase.Waiting)
            {
                strike.phase = Phase.Falling;
                strike.timer = 0f;
                strike.BeginShell();
                count++;
            }
        }
        claymoreStrikes.RemoveAll(s => s == null);
        return count;
    }

    // ------------------------------------------------------------------
    // 77번 유도 (Grenade의 유도와 같은 규칙)
    // ------------------------------------------------------------------

    void UpdateHoming()
    {
        if (!GrenadeHomingOverride.TryGetActive(out var homing)) return;

        targetSearchTimer -= Time.deltaTime;
        if (targetSearchTimer <= 0f || homingTarget == null || homingTarget.IsDead || !homingTarget.isActiveAndEnabled)
        {
            homingTarget = FindNearestEnemy();
            targetSearchTimer = TargetSearchInterval;
        }
        if (homingTarget == null) return;

        float speed = owner.stats != null ? Mathf.Max(0f, owner.stats.MoveSpeed) * homing.speedMultiplier : 0f;
        Vector3 toTarget = homingTarget.transform.position - center;
        toTarget.y = 0f;
        float distance = toTarget.magnitude;
        float step = Mathf.Min(speed * Time.deltaTime, distance);
        if (step > 0.0001f) MoveAlongGround(toTarget / distance, step, homing.collisionRadius);
    }

    EnemyHealth FindNearestEnemy()
    {
        EnemyHealth best = null;
        float bestSqr = float.MaxValue;
        foreach (var enemy in FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None))
        {
            if (enemy == null || enemy.IsDead || !enemy.isActiveAndEnabled) continue;
            if (!string.IsNullOrEmpty(enemyTag) && !enemy.CompareTag(enemyTag)) continue;
            // [115번] 크레모어 포격은 깔린 방의 적만 쫓습니다.
            if (claymore != null && claymoreRoom != null &&
                !ReferenceEquals(ClaymoreOverride.RoomOf(enemy.transform.position), claymoreRoom)) continue;

            Vector3 d = enemy.transform.position - center;
            d.y = 0f;
            if (d.sqrMagnitude < bestSqr)
            {
                bestSqr = d.sqrMagnitude;
                best = enemy;
            }
        }
        return best;
    }

    void MoveAlongGround(Vector3 dir, float distance, float collisionRadius)
    {
        Vector3 remaining = dir * distance;
        for (int iteration = 0; iteration < 2 && remaining.sqrMagnitude > 0.000001f; iteration++)
        {
            float length = remaining.magnitude;
            Vector3 moveDir = remaining / length;

            if (CastWall(moveDir, length, collisionRadius, out float hitDistance, out Vector3 hitNormal))
            {
                float allowed = Mathf.Max(0f, hitDistance - WallSkin);
                center += moveDir * allowed;

                Vector3 normal = new Vector3(hitNormal.x, 0f, hitNormal.z);
                if (normal.sqrMagnitude < 0.0001f) break;
                normal.Normalize();
                Vector3 left = moveDir * (length - allowed);
                remaining = left - Vector3.Dot(left, normal) * normal;
            }
            else
            {
                center += remaining;
                break;
            }
        }
        center.y = groundY;
    }

    bool CastWall(Vector3 dir, float maxDistance, float r, out float hitDistance, out Vector3 hitNormal)
    {
        hitDistance = maxDistance;
        hitNormal = Vector3.zero;
        r = Mathf.Max(r, 0.01f);
        Vector3 origin = new Vector3(center.x, groundY + r + 0.05f, center.z);

        int count = Physics.SphereCastNonAlloc(origin, r, dir, wallHitBuffer, maxDistance + WallSkin,
                                               Physics.AllLayers, QueryTriggerInteraction.Ignore);
        bool found = false;
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = wallHitBuffer[i];
            if (hit.collider == null || !hit.collider.CompareTag("Wall")) continue;
            if (hit.distance <= 0f && hit.point == Vector3.zero) continue;
            if (hit.distance < hitDistance)
            {
                hitDistance = hit.distance;
                hitNormal = hit.normal;
                found = true;
            }
        }
        return found;
    }

    // ------------------------------------------------------------------
    // 폭발 (Grenade.Explode와 같은 처리)
    // ------------------------------------------------------------------

    void Explode()
    {
        if (phase == Phase.Done) return;
        phase = Phase.Done;
        // 분열을 기다리는 크레모어 포격은 115번을 잃을 때 분열 포격을 일반 포격으로 바꿔야 하므로 목록에 남겨 둡니다(OnDestroy에서 빠짐).
        if (!splitPending) claymoreStrikes.Remove(this);

        Vector3 c = new Vector3(center.x, groundY, center.z);
        DestroyVisuals();

        // [방 전환] 크레모어 포격이 플레이어가 없는 방에서 터지면 조용한 폭발: 그 방의 특수방 잠금 해제만 하고 나머지는 건너뜁니다.
        if (claymoreRoom != null && !RoomScope.IsPlayerInRoom(claymoreRoom))
        {
            SpecialRoomLock.TryUnlockByGrenade(c, radius, claymoreRoom);
            split = null; // 분열 포격도 만들지 않음
        }
        else
        {
            try
            {
                float explosionEnemyDamage = enemyDamage;
                float explosionSelfDamage = selfDamage;
                if (incendiary != null)
                {
                    explosionEnemyDamage = enemyDamage * incendiary.explosionDamageRatio;
                    explosionSelfDamage = selfDamage > 0f ? incendiary.explosionSelfDamage : 0f;
                }

                DamageEnemies(c, explosionEnemyDamage);
                DamageOwner(c, explosionSelfDamage);

                // 폭발 이펙트 + 수류탄 폭발 알림(66번 해킹/57번/특수방 잠금 해제)
                // [방 전환] 크레모어 포격이면 특수방 잠금 해제를 그 포격의 방 기준으로 판정하도록 방을 넘깁니다(일반 포격은 null = 지금 방 기준).
                owner.NotifyExploded(c, radius, c, claymoreRoom);

                if (incendiary != null)
                {
                    IncendiaryFireZone.Spawn(owner, c, radius, explosionHeight, enemyTag, enemyDamage, incendiary);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"ArtilleryStrike: 포격 폭발 처리 중 오류가 발생했습니다: {e}");
            }
        }

        // [92번] 일반 포격은 폭발이 끝나면 바로 분열 포격 4개를 시작합니다. 크레모어 포격은 깔릴 때 정해진 시간에 깝니다(splitPending).
        if (split != null && !splitPending)
        {
            SpawnSplitStrikes(false);
            split = null;
        }

        // 분열을 아직 기다리는 크레모어 포격이면 그때까지 남아 있습니다(표시는 이미 지움).
        if (!splitPending) Destroy(gameObject);
    }

    void DamageEnemies(Vector3 c, float damage)
    {
        if (radius <= 0f || damage <= 0f) return;

        Vector3 top = c + Vector3.up * Mathf.Max(explosionHeight, 0.1f);
        Collider[] hits = Physics.OverlapCapsule(c, top, radius);
        var damaged = new HashSet<EnemyHealth>();
        foreach (var hit in hits)
        {
            if (hit == null) continue;
            EnemyHealth enemyHealth = hit.GetComponent<EnemyHealth>();
            if (enemyHealth == null) enemyHealth = hit.GetComponentInParent<EnemyHealth>();
            if (enemyHealth == null) continue;

            bool isEnemy = string.IsNullOrEmpty(enemyTag) || hit.CompareTag(enemyTag) || enemyHealth.CompareTag(enemyTag);
            if (!isEnemy) continue;
            if (enemyHealth.IsDead) continue;
            if (!damaged.Add(enemyHealth)) continue;

            enemyHealth.TakeWeakDamage(damage); // 수류탄 폭발과 같은 규칙: 피해 숫자 항상 Weak
        }
    }

    void DamageOwner(Vector3 c, float damage)
    {
        if (damage <= 0f) return;
        if (GrenadeSelfDamageOverride.IsBlocked) return; // 57번 등 수류탄 자해 무효
        if (PlayerDash.IsDodging) return;                // 74번 돌진 회피

        PlayerHealth health = owner.OwnerHealth;
        if (health == null || health.IsDead) return;

        Vector3 p = owner.transform.position;
        Vector2 flat = new Vector2(p.x - c.x, p.z - c.z);
        if (flat.magnitude <= radius) health.TakeDamage(damage);
    }

    // [92번] 지금 중심에서 forward 기준 X자 4방향(앞 ±splitAngle, 뒤 ±(180 - splitAngle))에 분열 포격을 시작합니다(다시 분열 X).
    void SpawnSplitStrikes(bool asClaymore)
    {
        var s = split;
        if (s == null || owner == null) return;

        try
        {
            Vector3 c = new Vector3(center.x, groundY, center.z);
            float childRadius = radius * s.radiusRatio;
            float childDamage = enemyDamage * s.damageRatio;
            float childSelf = selfDamage > 0f ? RoundUpToHalf(selfDamage * s.selfDamageRatio) : 0f;
            float distance = radius * s.distanceRatio;

            float front = s.splitAngle;
            float back = 180f - s.splitAngle;
            float[] angles = { front, -front, back, -back };
            foreach (float angle in angles)
            {
                Vector3 dir = Quaternion.AngleAxis(angle, Vector3.up) * forward;
                Vector3 target = c + dir * distance;
                target.y = groundY;
                target = owner.ClampToRoomBounds(c, target);
                target.y = groundY;

                Create(owner, settings, target, dir, childDamage, childSelf, childRadius, incendiary, null,
                       asClaymore ? claymore : null, claymoreRoom);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"ArtilleryStrike: 분열 포격 생성 중 오류가 발생해 건너뜁니다: {e}");
        }
    }

    public static float RoundUpToHalf(float value)
    {
        if (value <= 0f) return 0f;
        return Mathf.Ceil(value * 2f - 0.0001f) * 0.5f;
    }

    // ------------------------------------------------------------------
    // 표시: 조준 원/십자선/레이저 + 포탄
    // ------------------------------------------------------------------

    void BuildMarker()
    {
        markerRoot = new GameObject("ArtilleryLockOn");
        markerRoot.transform.position = MarkerPosition();

        if (sharedDiscMesh == null) sharedDiscMesh = GrenadeAimIndicator.BuildUnitDiscMesh(CircleSegments);

        // 반투명 범위
        fillMaterial = GrenadeAimIndicator.BuildMaterial("ArtilleryFill_Material", settings.fillColor);
        var fill = new GameObject("Fill");
        fill.transform.SetParent(markerRoot.transform, false);
        fill.transform.localScale = new Vector3(radius, 1f, radius);
        fill.AddComponent<MeshFilter>().sharedMesh = sharedDiscMesh;
        ConfigureRenderer(fill.AddComponent<MeshRenderer>(), fillMaterial);

        lineMaterial = GrenadeAimIndicator.BuildMaterial("ArtilleryLine_Material", settings.lineColor);

        // 바깥 테두리 / 안쪽 원
        AddCircle("Outline", radius, settings.outlineWidth);
        AddCircle("InnerRing", radius * settings.innerRingRatio, settings.innerLineWidth);

        // 가운데 십자선
        float half = radius * settings.crossRatio;
        AddLine("CrossX", new Vector3(-half, 0.002f, 0f), new Vector3(half, 0.002f, 0f), settings.innerLineWidth);
        AddLine("CrossZ", new Vector3(0f, 0.002f, -half), new Vector3(0f, 0.002f, half), settings.innerLineWidth);

        // 하늘로 뻗는 레이저(빠르게 점멸)
        laserMaterial = GrenadeAimIndicator.BuildMaterial("ArtilleryLaser_Material", settings.laserColor);
        var laserGO = new GameObject("Laser");
        laserGO.transform.SetParent(markerRoot.transform, false);
        laser = laserGO.AddComponent<LineRenderer>();
        laser.useWorldSpace = false;
        laser.alignment = LineAlignment.View;
        laser.startWidth = settings.laserWidth;
        laser.endWidth = settings.laserWidth;
        laser.positionCount = 2;
        laser.SetPosition(0, Vector3.zero);
        laser.SetPosition(1, Vector3.up * settings.laserHeight);
        ConfigureRenderer(laser, laserMaterial);
    }

    void AddCircle(string name, float r, float width)
    {
        var go = new GameObject(name);
        go.transform.SetParent(markerRoot.transform, false);
        var line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = true;
        line.alignment = LineAlignment.View;
        line.numCornerVertices = 2;
        line.startWidth = width;
        line.endWidth = width;
        line.positionCount = CircleSegments;
        for (int i = 0; i < CircleSegments; i++)
        {
            float a = (float)i / CircleSegments * Mathf.PI * 2f;
            line.SetPosition(i, new Vector3(Mathf.Cos(a) * r, 0.002f, Mathf.Sin(a) * r));
        }
        ConfigureRenderer(line, lineMaterial);
    }

    void AddLine(string name, Vector3 a, Vector3 b, float width)
    {
        var go = new GameObject(name);
        go.transform.SetParent(markerRoot.transform, false);
        var line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.alignment = LineAlignment.View;
        line.startWidth = width;
        line.endWidth = width;
        line.positionCount = 2;
        line.SetPosition(0, a);
        line.SetPosition(1, b);
        ConfigureRenderer(line, lineMaterial);
    }

    static void ConfigureRenderer(Renderer rend, Material mat)
    {
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rend.receiveShadows = false;
        rend.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        rend.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        rend.sharedMaterial = mat;
    }

    Vector3 MarkerPosition() => new Vector3(center.x, groundY + settings.groundOffset, center.z);

    void UpdateVisuals()
    {
        if (markerRoot != null) markerRoot.transform.position = MarkerPosition();

        // 레이저 점멸(실제 시간 기준이 아니라 게임 시간 - 일시정지 중엔 멈춤)
        if (laserMaterial != null)
        {
            blinkTimer += Time.deltaTime;
            if (blinkTimer >= settings.laserBlinkInterval)
            {
                blinkTimer = 0f;
                laserBright = !laserBright;
                Color col = settings.laserColor;
                col.a *= laserBright ? 1f : settings.laserMinBrightness;
                laserMaterial.color = col;
            }
        }
    }

    // 포탄을 하늘(중심 위 dropHeight)에 만듭니다. 수류탄 모델(없으면 수류탄 임시 초록 구체) x shellScale, 57번 외형/81번 발광 그대로.
    void BeginShell()
    {
        if (shell != null) return;
        GameAudio.Play(SfxId.ArtilleryFall);
        try
        {
            if (owner.grenadePrefab != null)
            {
                shell = Instantiate(owner.grenadePrefab);
                // 프리팹에 Grenade가 붙어 있어도 스스로 날아가지 않도록 끕니다(위치는 여기서 직접 움직임).
                foreach (var g in shell.GetComponentsInChildren<Grenade>(true)) g.enabled = false;
                shell.transform.localScale = owner.grenadePrefab.transform.localScale * settings.shellScale;
            }
            else
            {
                shell = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                shell.transform.localScale = Vector3.one * Mathf.Max(owner.fallbackSphereSize, 0.01f) * settings.shellScale;
                var rend = shell.GetComponent<Renderer>();
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Universal Render Pipeline/Lit")
                                ?? Shader.Find("Sprites/Default");
                if (rend != null && shader != null)
                {
                    shellMaterial = new Material(shader) { name = "ArtilleryShell_Material", color = owner.fallbackSphereColor };
                    rend.sharedMaterial = shellMaterial;
                }
            }
            shell.name = "ArtilleryShell";

            foreach (var col in shell.GetComponentsInChildren<Collider>(true)) col.enabled = false;
            foreach (var rb in shell.GetComponentsInChildren<Rigidbody>(true)) { rb.isKinematic = true; rb.useGravity = false; }

            GrenadeVisualOverride.ApplyToGrenade(shell);
            if (incendiary != null) IncendiaryGrenadeGlow.Attach(shell, incendiary);

            UpdateShell(0f);
            SetShellVisible(visible);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"ArtilleryStrike: 포탄 생성 중 오류가 발생해 포탄 없이 진행합니다(폭발에는 영향 없음): {e}");
        }
    }

    // u = 0(하늘) ~ 1(바닥). 점점 빨라지게 떨어집니다.
    void UpdateShell(float u)
    {
        if (shell == null) return;
        u = Mathf.Clamp01(u);
        float height = settings.dropHeight * (1f - u * u);
        shell.transform.position = new Vector3(center.x, groundY + height, center.z);
    }

    void SetVisible(bool show)
    {
        visible = show;
        if (markerRoot != null) markerRoot.SetActive(show);
        SetShellVisible(show);
    }

    void SetShellVisible(bool show)
    {
        if (shell == null) return;
        foreach (var r in shell.GetComponentsInChildren<Renderer>(true)) r.enabled = show;
        var glow = shell.GetComponent<IncendiaryGrenadeGlow>();
        if (glow != null) glow.SetVisible(show);
    }

    void DestroyVisuals()
    {
        if (markerRoot != null) Destroy(markerRoot);
        if (shell != null) Destroy(shell);
        if (fillMaterial != null) Destroy(fillMaterial);
        if (lineMaterial != null) Destroy(lineMaterial);
        if (laserMaterial != null) Destroy(laserMaterial);
        if (shellMaterial != null) Destroy(shellMaterial);
        markerRoot = null;
        shell = null;
        fillMaterial = lineMaterial = laserMaterial = shellMaterial = null;
        laser = null;
    }
}
