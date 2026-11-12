using System.Collections.Generic;
using UnityEngine;

// 패시브 81번 - "수류탄이 소이탄이 된다. 폭발 시 수류탄 피해의 50%를 주고, 그 범위를 3초 동안 불태워 0.5초마다 수류탄 피해의
// 25%를 준다."
//
// [기획 - 형빈 확정]
// - 폭발: 적에게 수류탄 피해 x 50%(explosionDamageRatio). 플레이어 자해 피해는 0.5 고정(explosionSelfDamage, 수류탄 자해 피해
//   스탯이 0이면 없음). 폭발 이펙트/57번 토끼/66번 해킹(해킹툴 전개 이펙트 + 적 총알 제거)/특수방 잠금 해제는 평소처럼 폭발
//   순간에 일어납니다.
// - 불타는 구역: 폭발 위치에 수류탄 폭발 반경 크기의 구역이 burnDuration(3초) 동안 남습니다. 터진 직후에는 피해가 없고,
//   tickInterval(0.5초)마다(0.5/1.0/.../3.0초, 6번) 구역 안의 모든 대상에게 피해를 줍니다.
//   - 적: 수류탄 피해 x 25%(tickDamageRatio). 보스 포함. 41번(해킹된 적 피해 +50%)도 그대로 적용됩니다.
//   - 플레이어: 0.5 고정(playerTickDamage). 일반 피격이라 무적 시간 중에는 받지 않습니다. 57번이 있으면 받지 않습니다.
//   - 수류탄 피해는 던진 순간의 값입니다(기본 수류탄과 같음).
// - 구역 표시: 착탄 지점 원과 같은 방식(반투명 원 + 테두리)을 빨갛게 3초 동안 표시합니다.
// - 불꽃: 구역 안 랜덤 위치에 flamePrefab(vfx_Flames_01)을 flameSpawnInterval마다 띄웁니다. 각 불꽃은 생긴 자리에 남아 잠깐
//   불길을 뿜다가 사라집니다. 구역이 끝나면 남은 불꽃도 새 불길을 멈추고 사라집니다.
// - 66번과 함께: 불 구역 피해를 받은 적(그 피해로 죽지 않은 적)은 틱마다 해킹이 다시 걸려 해킹 시간이 갱신됩니다(형빈 확정 -
//   시너지). 해킹이 걸리는 순간의 적 이펙트는 틱마다 반복하고, 해킹 지속 이펙트는 중첩되지 않고 이어집니다. 해킹툴 전개
//   이펙트와 적 총알 제거는 폭발 순간(66번 자체 처리)에만 있고, 불 구역에서는 하지 않습니다.
// - 77번과 함께: 수류탄 유도 후 터지면, 불타는 구역도 3초 동안 가장 가까운 적을 따라 움직입니다(수류탄 유도와 같은 속도 -
//   플레이어 현재 이동속도 x 77번 속도 배율, 벽에 막히면 벽을 따라 미끄러짐). 구역 원도 따라 움직이고, 불꽃은 생긴 자리에 남습니다.
// - 여러 구역이 겹치면 각각 피해를 줍니다(형빈 확정 - 수류탄을 그만큼 쓴 보상).
// - 소이탄 발광: 던진 순간부터 터질 때까지(77번 유도 중 포함) 수류탄 주변에 빨간 빛 그림 + 빨간 점광원을 띄웁니다. 수류탄 모델/
//   폭발 이펙트 색은 바꾸지 않습니다(57번 흰 외형과 그대로 함께 쓸 수 있음).
// - 소이탄 여부는 "던지는 순간" 정합니다. 날아가는 도중 81번을 잃어도 그 수류탄은 소이탄으로 터집니다.
//
// [구현]
// - IncendiaryGrenadeOverride 등록소에 설정 묶음을 등록/해제만 합니다. Grenade.cs가 던질 때 확인해서 발광을 붙이고, 터질 때
//   피해를 줄인 뒤 IncendiaryFireZone.Spawn으로 구역을 만듭니다.
// - 66번 보유 여부는 GrenadeHackOverride로 확인하고, 해킹은 PlayerHackTool.ApplyHackToEnemy(전개 이펙트/총알 제거 없는 한 명 해킹)로 겁니다.
// - 잃으면(48번 등) 등록만 풉니다. 이미 타고 있는 구역은 끝까지 탑니다.
//
// [아이템 에셋 설정]
// ItemDefinition: Item Id = 81, Category = Passive, Special Effects에 IncendiaryGrenadeEffect 하나. Stat Modifiers는 비워둡니다.
// Settings > Flame Prefab 칸에 vfx_Flames_01을 넣습니다.
[System.Serializable]
public class IncendiaryGrenadeEffect : IPassiveItemEffect, IRemovablePassiveEffect
{
    [Tooltip("소이탄 설정입니다(폭발/불타는 구역/불꽃/발광).")]
    public IncendiaryGrenadeOverride.Settings settings = new IncendiaryGrenadeOverride.Settings();

    public static string RegisterSourceId(string itemId) => "incendiary_grenade_" + itemId;

    public void Apply(PassiveEffectContext context, string sourceItemId)
    {
        if (context == null || context.PlayerObject == null)
        {
            Debug.LogWarning("IncendiaryGrenadeEffect: 플레이어를 찾을 수 없어 81번 효과를 적용하지 못했습니다.");
            return;
        }

        IncendiaryGrenadeOverride.Register(RegisterSourceId(sourceItemId), context.PlayerObject, settings);
    }

    public void Remove(PassiveEffectContext context, string sourceItemId)
    {
        IncendiaryGrenadeOverride.Unregister(RegisterSourceId(sourceItemId));
    }
}

// 81번 소이탄이 남기는 불타는 구역 하나입니다. Grenade가 터질 때 Spawn으로 만들어지며, burnDuration이 지나면 스스로 사라집니다.
public class IncendiaryFireZone : MonoBehaviour
{
    private const int MarkerSegments = 64;
    private const float WallSkin = 0.01f;
    private const float TargetSearchInterval = 0.1f;
    private static readonly RaycastHit[] wallHitBuffer = new RaycastHit[16];

    private PlayerGrenadeThrower owner;
    private IncendiaryGrenadeOverride.Settings settings;
    private float radius;
    private float height;
    private string enemyTag;
    private float baseEnemyDamage;
    private float groundY;

    private float elapsed;
    private float nextTickTime;
    private float flameTimer;
    private bool finished;

    // 77번 유도
    private bool homing;
    private GrenadeHomingOverride.Settings homingSettings;
    private EnemyHealth homingTarget;
    private float targetSearchTimer;

    // 구역 표시
    private GameObject markerRoot;
    private Mesh markerDiscMesh;
    private Material markerFillMaterial;
    private Material markerOutlineMaterial;

    private class Flame
    {
        public GameObject go;
        public float age;
        public bool stopped;
    }
    private readonly List<Flame> flames = new List<Flame>();

    private int burnLoopHandle;

    // center(바닥 위치)에 구역을 만듭니다. baseEnemyDamage는 던진 순간의 수류탄 피해(비율을 곱하기 전)입니다.
    public static IncendiaryFireZone Spawn(PlayerGrenadeThrower owner, Vector3 center, float radius, float height, string enemyTag,
                                           float baseEnemyDamage, IncendiaryGrenadeOverride.Settings settings)
    {
        if (settings == null || radius <= 0f) return null;

        var go = new GameObject("IncendiaryFireZone");
        go.transform.position = center;
        var zone = go.AddComponent<IncendiaryFireZone>();
        // [방 전환] 플레이어가 다른 방에 들어가면 불 구역을 바로 지웁니다(불꽃/구역 원도 OnDestroy에서 함께 지워짐 - 다른 방에서 보이거나 문 너머 플레이어에게 피해를 주지 않도록).
        RoomScope.DestroyWhenPlayerLeaves(go);
        zone.Init(owner, center, radius, height, enemyTag, baseEnemyDamage, settings);
        return zone;
    }

    private void Init(PlayerGrenadeThrower owner, Vector3 center, float radius, float height, string enemyTag,
                      float baseEnemyDamage, IncendiaryGrenadeOverride.Settings settings)
    {
        this.owner = owner;
        this.settings = settings;
        this.radius = radius;
        this.height = Mathf.Max(height, 0.1f);
        this.enemyTag = enemyTag;
        this.baseEnemyDamage = baseEnemyDamage;
        groundY = center.y;

        elapsed = 0f;
        nextTickTime = settings.tickInterval; // 터진 직후에는 피해 없음(형빈 확정)
        flameTimer = 0f;

        // [77번] 터지는 순간 유도 아이템을 보유 중이면 구역도 적을 따라 움직입니다.
        if (GrenadeHomingOverride.TryGetActive(out var hs))
        {
            homing = true;
            homingSettings = hs;
        }

        CreateMarker();

        // [사운드] 연소 반복 소리. 플레이어가 없는 방에서 생긴 구역은 소리 없음(방 범위 규칙 - 방을 나가면 GameAudio가 자동으로 끔).
        if (RoomScope.IsPlayerInRoom(RoomScope.RoomOf(center))) burnLoopHandle = GameAudio.PlayLoop(SfxId.IncendiaryBurn);
    }

    void Update()
    {
        if (finished) return;

        float dt = Time.deltaTime;
        elapsed += dt;

        if (homing) UpdateHoming(dt);
        MoveMarker();

        // 피해 틱(한 프레임이 길어도 빠진 틱 없이 처리)
        while (nextTickTime <= elapsed + 0.0001f && nextTickTime <= settings.burnDuration + 0.0001f)
        {
            Tick();
            nextTickTime += settings.tickInterval;
        }

        UpdateFlames(dt, spawnNew: true);

        if (elapsed >= settings.burnDuration)
        {
            Finish();
        }
    }

    // ---------------- 피해 ----------------

    private void Tick()
    {
        Vector3 center = transform.position;

        // 적: 수류탄 피해 x 비율, 66번 보유 중이면 살아남은 적에게 해킹(시간 갱신 + 걸리는 순간 이펙트 반복)
        float enemyDamage = baseEnemyDamage * settings.tickDamageRatio;
        bool hack = GrenadeHackOverride.IsActive;
        PlayerHackTool hackTool = (hack && owner != null) ? owner.GetComponent<PlayerHackTool>() : null;

        foreach (var enemy in CollectEnemies(center))
        {
            try
            {
                if (enemyDamage > 0f) enemy.TakeWeakDamage(enemyDamage); // 피해 숫자 항상 Weak(형빈 확정)
                if (hackTool != null && !enemy.IsDead) hackTool.ApplyHackToEnemy(enemy);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"IncendiaryFireZone: 적 피해/해킹 처리 중 오류가 발생해 이 적은 건너뜁니다: {e}");
            }
        }

        // 플레이어: 고정 피해, 57번이면 피해 처리 자체를 건너뜀(Grenade.DamageOwner와 같은 기준 - 수평 거리)
        // [74번 돌진 회피] 이번 틱 순간 돌진 중이면 건너뜁니다. 돌진이 끝난 뒤에도 구역 안이면 다음 틱부터 평소처럼 피해를 받습니다.
        if (settings.playerTickDamage > 0f && !GrenadeSelfDamageOverride.IsBlocked && !PlayerDash.IsDodging && owner != null)
        {
            PlayerHealth health = owner.OwnerHealth;
            if (health != null && !health.IsDead)
            {
                Vector3 p = owner.transform.position;
                Vector2 flat = new Vector2(p.x - center.x, p.z - center.z);
                if (flat.magnitude <= radius) health.TakeDamage(settings.playerTickDamage);
            }
        }
    }

    // 원기둥(반경 radius, 높이 height) 안의 살아 있는 적을 EnemyHealth 단위로 중복 없이 모읍니다(Grenade.DamageEnemies와 같은 기준).
    private List<EnemyHealth> CollectEnemies(Vector3 center)
    {
        var result = new List<EnemyHealth>();
        var seen = new HashSet<EnemyHealth>();

        Collider[] hits = Physics.OverlapCapsule(center, center + Vector3.up * height, radius);
        foreach (var hit in hits)
        {
            if (hit == null) continue;

            EnemyHealth enemy = hit.GetComponent<EnemyHealth>();
            if (enemy == null) enemy = hit.GetComponentInParent<EnemyHealth>();
            if (enemy == null) continue;

            bool isEnemy = string.IsNullOrEmpty(enemyTag) || hit.CompareTag(enemyTag) || enemy.CompareTag(enemyTag);
            if (!isEnemy || enemy.IsDead) continue;
            if (!seen.Add(enemy)) continue;

            result.Add(enemy);
        }
        return result;
    }

    // ---------------- 77번 유도 (Grenade.cs 유도 단계와 같은 방식) ----------------

    private void UpdateHoming(float dt)
    {
        targetSearchTimer -= dt;
        if (targetSearchTimer <= 0f || homingTarget == null || homingTarget.IsDead || !homingTarget.isActiveAndEnabled)
        {
            homingTarget = FindNearestEnemy();
            targetSearchTimer = TargetSearchInterval;
        }
        if (homingTarget == null) return;

        float speed = 0f;
        if (owner != null && owner.stats != null) speed = Mathf.Max(0f, owner.stats.MoveSpeed) * homingSettings.speedMultiplier;

        Vector3 toTarget = homingTarget.transform.position - transform.position;
        toTarget.y = 0f;
        float distance = toTarget.magnitude;
        float step = Mathf.Min(speed * dt, distance);
        if (step > 0.0001f) MoveAlongGround(toTarget / distance, step);
    }

    private EnemyHealth FindNearestEnemy()
    {
        EnemyHealth best = null;
        float bestSqr = float.MaxValue;
        Vector3 pos = transform.position;

        foreach (var enemy in FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None))
        {
            if (enemy == null || enemy.IsDead || !enemy.isActiveAndEnabled) continue;
            if (!string.IsNullOrEmpty(enemyTag) && !enemy.CompareTag(enemyTag)) continue;

            Vector3 d = enemy.transform.position - pos;
            d.y = 0f;
            if (d.sqrMagnitude < bestSqr)
            {
                bestSqr = d.sqrMagnitude;
                best = enemy;
            }
        }
        return best;
    }

    private void MoveAlongGround(Vector3 dir, float distance)
    {
        Vector3 remaining = dir * distance;
        for (int iteration = 0; iteration < 2 && remaining.sqrMagnitude > 0.000001f; iteration++)
        {
            float length = remaining.magnitude;
            Vector3 moveDir = remaining / length;

            if (CastWall(moveDir, length, out float hitDistance, out Vector3 hitNormal))
            {
                float allowed = Mathf.Max(0f, hitDistance - WallSkin);
                transform.position += moveDir * allowed;

                Vector3 normal = new Vector3(hitNormal.x, 0f, hitNormal.z);
                if (normal.sqrMagnitude < 0.0001f) break;
                normal.Normalize();
                Vector3 left = moveDir * (length - allowed);
                remaining = left - Vector3.Dot(left, normal) * normal;
            }
            else
            {
                transform.position += remaining;
                break;
            }
        }

        Vector3 p = transform.position;
        p.y = groundY;
        transform.position = p;
    }

    private bool CastWall(Vector3 dir, float maxDistance, out float hitDistance, out Vector3 hitNormal)
    {
        hitDistance = maxDistance;
        hitNormal = Vector3.zero;

        float r = homingSettings.collisionRadius;
        Vector3 origin = new Vector3(transform.position.x, groundY + r + 0.05f, transform.position.z);

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

    // ---------------- 불꽃 ----------------

    private void UpdateFlames(float dt, bool spawnNew)
    {
        if (spawnNew && settings.flamePrefab != null)
        {
            flameTimer -= dt;
            while (flameTimer <= 0f)
            {
                SpawnFlame();
                flameTimer += settings.flameSpawnInterval;
            }
        }

        for (int i = flames.Count - 1; i >= 0; i--)
        {
            var f = flames[i];
            if (f.go == null) { flames.RemoveAt(i); continue; }

            f.age += dt;
            if (!f.stopped && f.age >= settings.flameEmitTime) StopFlame(f);
        }
    }

    // 구역 안 랜덤 위치(원 안에 고르게)에 불꽃을 하나 띄웁니다. 불꽃은 구역의 자식이 아니라 생긴 자리에 남습니다.
    private void SpawnFlame()
    {
        float angle = Random.Range(0f, Mathf.PI * 2f);
        float dist = radius * Mathf.Sqrt(Random.value);
        Vector3 pos = transform.position + new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist) + settings.flameOffset;

        GameObject go = Instantiate(settings.flamePrefab, pos, settings.flamePrefab.transform.rotation);
        float factor = owner != null ? owner.GetRadiusVfxFactor(radius) : 1f; // 폭발 반경이 커지면 불꽃도 커짐
        ApplyVfxScale(go, settings.flameScale * factor);

        flames.Add(new Flame { go = go, age = 0f, stopped = false });
    }

    // 새 불길을 멈추고, 남은 불길이 사라질 시간(flameLingerTime) 뒤에 오브젝트를 지웁니다.
    private void StopFlame(Flame f)
    {
        f.stopped = true;
        if (f.go == null) return;

        foreach (var ps in f.go.GetComponentsInChildren<ParticleSystem>(true))
        {
            ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        }
        Destroy(f.go, settings.flameLingerTime);
    }

    // 파티클 시작 크기에 배율을 곱합니다(PlayerHackTool/수류탄 폭발 이펙트와 같은 방식). 파티클이 없으면 localScale을 키웁니다.
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
        }
    }

    // ---------------- 구역 표시 ----------------

    private void CreateMarker()
    {
        if (owner != null && !owner.showLandingMarker) return;

        float groundOffset = owner != null ? owner.landingMarkerGroundOffset : 0.03f;
        float outlineWidth = owner != null ? owner.landingMarkerOutlineWidth : 0.05f;

        markerRoot = new GameObject("IncendiaryZoneMarker");
        markerRoot.transform.position = transform.position + Vector3.up * groundOffset;

        if (settings.zoneFillColor.a > 0f)
        {
            markerDiscMesh = GrenadeAimIndicator.BuildUnitDiscMesh(MarkerSegments);
            markerFillMaterial = GrenadeAimIndicator.BuildMaterial("IncendiaryZoneFill_Material", settings.zoneFillColor);

            var fill = new GameObject("Fill");
            fill.transform.SetParent(markerRoot.transform, worldPositionStays: false);
            fill.transform.localScale = new Vector3(radius, 1f, radius);
            fill.AddComponent<MeshFilter>().sharedMesh = markerDiscMesh;
            ConfigureMarkerRenderer(fill.AddComponent<MeshRenderer>(), markerFillMaterial);
        }

        if (settings.zoneOutlineColor.a > 0f)
        {
            markerOutlineMaterial = GrenadeAimIndicator.BuildMaterial("IncendiaryZoneOutline_Material", settings.zoneOutlineColor);

            var outline = new GameObject("Outline");
            outline.transform.SetParent(markerRoot.transform, worldPositionStays: false);
            var line = outline.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.alignment = LineAlignment.View;
            line.numCornerVertices = 2;
            line.startWidth = outlineWidth;
            line.endWidth = outlineWidth;
            line.positionCount = MarkerSegments;
            for (int i = 0; i < MarkerSegments; i++)
            {
                float a = (float)i / MarkerSegments * Mathf.PI * 2f;
                line.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius));
            }
            ConfigureMarkerRenderer(line, markerOutlineMaterial);
        }
    }

    private static void ConfigureMarkerRenderer(Renderer rend, Material mat)
    {
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rend.receiveShadows = false;
        rend.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        rend.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        rend.sharedMaterial = mat;
    }

    private void MoveMarker()
    {
        if (markerRoot == null) return;
        float groundOffset = owner != null ? owner.landingMarkerGroundOffset : 0.03f;
        markerRoot.transform.position = transform.position + Vector3.up * groundOffset;
    }

    private void DestroyMarker()
    {
        if (markerRoot != null) Destroy(markerRoot);
        if (markerDiscMesh != null) Destroy(markerDiscMesh);
        if (markerFillMaterial != null) Destroy(markerFillMaterial);
        if (markerOutlineMaterial != null) Destroy(markerOutlineMaterial);
        markerRoot = null;
        markerDiscMesh = null;
        markerFillMaterial = null;
        markerOutlineMaterial = null;
    }

    // ---------------- 종료 ----------------

    private void Finish()
    {
        finished = true;
        GameAudio.StopLoop(ref burnLoopHandle);
        DestroyMarker();

        // 남은 불꽃은 새 불길을 멈추고 자연스럽게 사라지게 합니다(구역 오브젝트와 별개로 남아서 정리됨).
        foreach (var f in flames)
        {
            if (f.go != null && !f.stopped) StopFlame(f);
        }
        flames.Clear();

        Destroy(gameObject);
    }

    // 씬 전환 등으로 구역이 갑자기 사라져도 표시가 남지 않게 합니다.
    void OnDestroy()
    {
        GameAudio.StopLoop(ref burnLoopHandle);
        DestroyMarker();
        foreach (var f in flames)
        {
            if (f.go != null) Destroy(f.go);
        }
        flames.Clear();
    }
}

// 81번 소이탄 발광입니다. Grenade가 던질 때 소이탄이면 수류탄 오브젝트에 자동으로 붙입니다. 수류탄 주변에 빨간 빛 그림(항상 카메라를
// 향함, 맥박치듯 커졌다 작아짐)과 빨간 점광원을 띄우고, 수류탄이 터져 사라지면 함께 사라집니다. 수류탄이 회전/굴러가도 빛은
// 회전하지 않도록 자식이 아닌 별도 오브젝트로 만들어 위치만 따라갑니다.
public class IncendiaryGrenadeGlow : MonoBehaviour
{
    private static Sprite defaultGlowSprite;

    private IncendiaryGrenadeOverride.Settings settings;
    private GameObject glowRoot;
    private SpriteRenderer glowRenderer;
    private float age;

    public static void Attach(GameObject grenade, IncendiaryGrenadeOverride.Settings settings)
    {
        if (grenade == null || settings == null) return;
        if (!settings.enableGlowSprite && !settings.enableGlowLight) return;

        var glow = grenade.GetComponent<IncendiaryGrenadeGlow>();
        if (glow == null) glow = grenade.AddComponent<IncendiaryGrenadeGlow>();
        glow.Init(settings);
    }

    // [115번 크레모어] 플레이어가 다른 방에 있을 때 숨깁니다(Grenade가 부름).
    public void SetVisible(bool visible)
    {
        if (glowRoot != null && glowRoot.activeSelf != visible) glowRoot.SetActive(visible);
    }

    private void Init(IncendiaryGrenadeOverride.Settings s)
    {
        settings = s;
        if (glowRoot != null) Destroy(glowRoot);

        glowRoot = new GameObject("IncendiaryGrenadeGlow");
        glowRoot.transform.position = transform.position;

        if (s.enableGlowSprite)
        {
            glowRenderer = glowRoot.AddComponent<SpriteRenderer>();
            glowRenderer.sprite = GetDefaultGlowSprite();
            glowRenderer.color = s.glowColor;
            glowRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            glowRenderer.receiveShadows = false;
        }

        if (s.enableGlowLight)
        {
            var light = glowRoot.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = s.glowLightColor;
            light.intensity = s.glowLightIntensity;
            light.range = s.glowLightRange;
            light.shadows = LightShadows.None;
        }

        UpdateGlow();
    }

    void LateUpdate()
    {
        age += Time.deltaTime;
        UpdateGlow();
    }

    private void UpdateGlow()
    {
        if (glowRoot == null || settings == null) return;

        glowRoot.transform.position = transform.position;

        Camera cam = Camera.main;
        if (cam != null) glowRoot.transform.rotation = cam.transform.rotation; // 항상 화면을 정면으로 향함

        if (glowRenderer != null)
        {
            float pulse = 1f + settings.glowPulseAmount * Mathf.Sin(age * settings.glowPulseSpeed);
            // 스프라이트 원본 크기를 지름 glowSize(월드 m)에 맞춥니다.
            float spriteWorld = glowRenderer.sprite != null ? glowRenderer.sprite.bounds.size.x : 1f;
            float scale = settings.glowSize * pulse / Mathf.Max(spriteWorld, 0.0001f);
            glowRoot.transform.localScale = new Vector3(scale, scale, scale);
            glowRenderer.color = settings.glowColor;
        }
    }

    void OnDestroy()
    {
        if (glowRoot != null) Destroy(glowRoot);
        glowRoot = null;
    }

    // 가운데가 가장 밝고 바깥으로 부드럽게 사라지는 흰 원 그림입니다(색은 glowColor로 입힘). 한 번만 만들어 재사용합니다.
    private static Sprite GetDefaultGlowSprite()
    {
        if (defaultGlowSprite != null) return defaultGlowSprite;

        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "IncendiaryGlow_Default",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };

        var pixels = new Color32[size * size];
        float center = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - center) / center;
                float dy = (y - center) / center;
                float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                float a = Mathf.Pow(1f - d, 2f);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply();

        defaultGlowSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        return defaultGlowSprite;
    }
}
