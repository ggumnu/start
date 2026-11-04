using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

// 일반방 적과 보스방 보스를 스폰하는 IEnemySpawner 구현체입니다(LevelBuilder의 Enemy Spawner Component 칸에 연결).
// 일반 적 마릿수/후보는 지금 레벨의 RoomGenerationConfig(일반 적 칸)가 있으면 그것을, 없으면 이 컴포넌트 값을 씁니다.
// 처음 스폰 기록을 남겨 두었다가, 클리어 전에 벗어난 전투방에 다시 들어오면 RoomController가 RespawnEnemies로 똑같이 되살립니다.
// 2레벨 보스의 소환도 SpawnSummonedEnemy로 같은 방식으로 만듭니다(스폰 기록에는 남기지 않음).
// 설계 배경(출입구 회피, 보스 전용 경로, 스폰 기록)은 Docs/설계노트.md [적 스폰].
public class RealEnemySpawner : MonoBehaviour, IEnemySpawner, IEnemyRespawner
{
    [Header("기본 적 후보 (Level Config의 Normal Enemy Pool이 비어 있을 때)")]
    [Tooltip("근접/원거리를 한 목록에 섞어 등록. 등장 비율은 각 적 에셋의 Weight.")]
    public List<EnemyDefinition> enemyPool = new List<EnemyDefinition>();

    [Header("기본 마릿수 (Level Config에서 덮어쓰지 않을 때, 최소~최대 균등)")]
    public int minCount = 1;
    public int maxCount = 3;

    [Header("스폰 위치 탐색")]
    [Tooltip("방 중심 기준 스폰 후보 반경.")]
    public float spawnRadius = 4f;
    [Tooltip("NavMesh.SamplePosition 허용 거리(보스 중앙 스냅에도 사용).")]
    public float navMeshSampleDistance = 1f;
    [Tooltip("스폰 순간 다른 적/장애물과 겹치는지 검사하는 반경(적끼리도 떨어뜨리려면 마스크에 Enemy 포함).")]
    public float overlapCheckRadius = 0.5f;
    [Tooltip("겹침 검사에 포함할 레이어.")]
    public LayerMask overlapCheckMask = ~0;
    [Tooltip("한 마리당 자리 찾기 시도 횟수. 다 실패하면 그 한 마리는 건너뜀.")]
    public int maxPositionAttempts = 10;

    [Header("플레이어 출입구 근처 스폰 방지 (일반방 전용)")]
    [Tooltip("LevelBuilder의 cellSize와 반드시 같은 값(자동 동기화 안 됨) - 출입구 위치 계산용.")]
    public float cellSize = 10f;
    [Tooltip("출입구에서 이 거리보다 가깝게는 스폰하지 않음(입장 직후 접촉 피해 방지).")]
    public float minPlayerDistance = 3f;

    [Header("적 몸통 물리 레이어 (프로젝트에 미리 만들어둬야 합니다)")]
    [Tooltip("Tags and Layers에 이 레이어 추가 + Layer Collision Matrix에서 Player 레이어와 충돌 끄기. 보스도 동일.")]
    public string enemyBodyLayerName = "Enemy";

    [Header("보스 스폰 (보스방 전용)")]
    [Tooltip("보스방 variantId와 같은 항목이 우선, 없으면 variantId가 빈 기본 항목. 항목 안에선 가중치로 하나.")]
    public List<BossPoolEntry> bossPool = new List<BossPoolEntry>();
    [Tooltip("보스 스폰 첫 순간의 방향 계산용 카메라 각도 - CameraFollow의 Fixed Euler Angles와 같게(현재 50/-45/0).")]
    public Vector3 bossSpawnFacingEulerAngles = new Vector3(50f, -45f, 0f);

    // 방 하나에 처음 스폰된 적 한 마리의 기록입니다. 일반 적은 definition, 보스는 bossPrefab을 채웁니다.
    private class SpawnRecord
    {
        public EnemyDefinition definition;
        public GameObject bossPrefab;
        public Vector3 position;
        public Quaternion rotation;
    }

    // 방 id -> 그 방의 처음 스폰 기록. 새 레벨에서 같은 id의 방이 스폰되면 덮어씁니다.
    private readonly Dictionary<int, List<SpawnRecord>> spawnRecords = new Dictionary<int, List<SpawnRecord>>();

    // 레벨 설정을 읽기 위한 LevelBuilder(GameFlowManager가 레벨을 만들 때마다 config를 바꿔 끼움). 없으면(다른 씬) 이 컴포넌트 값만 씀.
    private LevelBuilder levelBuilder;
    // 마릿수 가중치 칸 수가 안 맞는 설정 에셋은 경고를 한 번만 띄웁니다(방마다 반복 X).
    private readonly HashSet<RoomGenerationConfig> warnedCountWeightMismatch = new HashSet<RoomGenerationConfig>();

    // LevelBuilder.GetWallOffset()과 같은 방향 -> 단위 오프셋(북=+Z, 동=+X).
    private static readonly Dictionary<Direction, Vector3> DirectionUnitOffset = new Dictionary<Direction, Vector3>
    {
        { Direction.North, new Vector3(0f, 0f, 1f) },
        { Direction.South, new Vector3(0f, 0f, -1f) },
        { Direction.East,  new Vector3(1f, 0f, 0f) },
        { Direction.West,  new Vector3(-1f, 0f, 0f) },
    };

    public void SpawnEnemies(RoomInstance room, Transform parent)
    {
        if (room.category == RoomCategory.Boss)
        {
            var bossRecords = new List<SpawnRecord>();
            spawnRecords[room.id] = bossRecords;
            SpawnBoss(room, parent, bossRecords);
            return;
        }

        if (room.category != RoomCategory.Normal)
        {
            return;
        }

        RoomGenerationConfig config = GetCurrentLevelConfig();
        List<EnemyDefinition> pool = ResolveEnemyPool(config);
        if (pool == null || pool.Count == 0)
        {
            Debug.LogWarning("RealEnemySpawner: 적 후보 목록(Level Config의 Normal Enemy Pool / 이 컴포넌트의 Enemy Pool)이 비어 있어 적을 스폰하지 않았습니다.");
            return;
        }

        var records = new List<SpawnRecord>();
        spawnRecords[room.id] = records;

        List<Vector3> doorPositions = GetDoorWorldPositions(room, parent.position);

        // 방 바닥 기준점. 방 중심이 NavMesh 위에 없는 극단적인 경우엔 도달 가능성 검사를 생략합니다.
        bool hasRoomAnchor = NavMesh.SamplePosition(parent.position, out NavMeshHit anchorHit, navMeshSampleDistance, NavMesh.AllAreas);
        Vector3 roomAnchor = hasRoomAnchor ? anchorHit.position : parent.position;

        int count = RollEnemyCount(config);
        for (int i = 0; i < count; i++)
        {
            var definition = PickWeighted(pool);
            if (definition == null) continue;

            if (!TryFindSpawnPosition(parent.position, roomAnchor, hasRoomAnchor, doorPositions, out Vector3 spawnPos))
            {
                Debug.LogWarning($"RealEnemySpawner: {maxPositionAttempts}번 시도했지만 유효한 스폰 위치를 찾지 못해 " +
                                  $"'{definition.variantId}' 스폰을 건너뜁니다.");
                continue;
            }

            SpawnOne(definition, spawnPos, parent);
            records.Add(new SpawnRecord { definition = definition, position = spawnPos, rotation = Quaternion.identity });
        }
    }

    // 이 방의 처음 스폰 기록대로 적을 다시 만들어 돌려줍니다(IEnemyRespawner). 기존 적 삭제는 RoomController가 합니다.
    // 추첨/위치 탐색을 다시 하지 않으므로 레벨 설정과 무관하게 처음과 같은 수/종류/위치입니다. 기록이 없으면 null.
    public List<EnemyHealth> RespawnEnemies(RoomInstance room, Transform parent)
    {
        if (!spawnRecords.TryGetValue(room.id, out var records) || records == null || records.Count == 0)
        {
            return null;
        }

        var spawned = new List<EnemyHealth>();
        foreach (var record in records)
        {
            GameObject go = null;

            if (record.bossPrefab != null)
            {
                go = SpawnBossFromPrefab(record.bossPrefab, record.position, record.rotation, parent);
            }
            else if (record.definition != null)
            {
                go = SpawnOne(record.definition, record.position, parent);
            }

            if (go == null) continue;

            var health = go.GetComponent<EnemyHealth>();
            if (health != null) spawned.Add(health);
        }

        return spawned;
    }

    // [2레벨 보스 소환] 일반 적 하나를 일반 방 스폰과 똑같이 만듭니다(정의 스탯/공격 방식/AI). 스폰 기록에는 남기지 않으므로 방 초기화 때
    // 되살아나지 않고(방 초기화가 방 콘텐츠 밑의 적을 지울 때 함께 사라짐), 방 클리어 조건에 더하는 일은 부르는 쪽이 합니다.
    public EnemyHealth SpawnSummonedEnemy(EnemyDefinition definition, Vector3 position, Transform parent)
    {
        if (definition == null) return null;
        GameObject go = SpawnOne(definition, position, parent);
        return go != null ? go.GetComponent<EnemyHealth>() : null;
    }

    // ---------- 레벨별 일반 적 설정 ----------

    private RoomGenerationConfig GetCurrentLevelConfig()
    {
        if (levelBuilder == null) levelBuilder = FindFirstObjectByType<LevelBuilder>();
        return levelBuilder != null ? levelBuilder.config : null;
    }

    // 레벨 설정의 Normal Enemy Pool에 유효한 적이 하나라도 있으면 그 목록, 아니면 이 컴포넌트의 Enemy Pool.
    private List<EnemyDefinition> ResolveEnemyPool(RoomGenerationConfig config)
    {
        if (config != null && config.normalEnemyPool != null && config.normalEnemyPool.Any(d => d != null))
        {
            return config.normalEnemyPool;
        }
        return enemyPool;
    }

    // 방 하나의 적 마릿수를 뽑습니다. 레벨 설정이 덮어쓰기를 켰으면 그 최소/최대 + 개수별 가중치, 아니면 이 컴포넌트의 최소~최대 균등.
    private int RollEnemyCount(RoomGenerationConfig config)
    {
        if (config == null || !config.overrideNormalEnemyCount)
        {
            return Random.Range(minCount, maxCount + 1);
        }

        int min = Mathf.Max(0, config.normalEnemyMinCount);
        int max = Mathf.Max(min, config.normalEnemyMaxCount);
        List<float> weights = config.normalEnemyCountWeights;

        if (weights == null || weights.Count == 0)
        {
            return Random.Range(min, max + 1);
        }

        float total = weights.Sum(w => Mathf.Max(0f, w));
        if (weights.Count != max - min + 1 || total <= 0f)
        {
            if (warnedCountWeightMismatch.Add(config))
            {
                Debug.LogWarning($"RealEnemySpawner: '{config.name}'의 Normal Enemy Count Weights 칸 수({weights.Count})가 " +
                                  $"최대 - 최소 + 1({max - min + 1})과 다르거나 가중치 합이 0이라 {min}~{max}마리 균등으로 뽑습니다.");
            }
            return Random.Range(min, max + 1);
        }

        float roll = Random.value * total;
        float cumulative = 0f;
        int lastPositive = max;
        for (int i = 0; i < weights.Count; i++)
        {
            if (weights[i] <= 0f) continue;
            lastPositive = min + i;
            cumulative += weights[i];
            if (roll <= cumulative) return min + i;
        }
        return lastPositive;
    }

    // ---------- 일반 적 ----------

    private GameObject SpawnOne(EnemyDefinition definition, Vector3 position, Transform parent)
    {
        GameObject enemyGO;
        if (definition.prefab != null)
        {
            enemyGO = Instantiate(definition.prefab, position, Quaternion.identity, parent);
        }
        else
        {
            enemyGO = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            enemyGO.transform.SetParent(parent);
            enemyGO.transform.position = position;
        }

        enemyGO.name = $"Enemy_{definition.variantId}";
        ApplyEnemyTagAndLayer(enemyGO);

        var stats = enemyGO.GetComponent<EnemyStats>();
        if (stats == null) stats = enemyGO.AddComponent<EnemyStats>();
        stats.ApplyFrom(definition); // 모든 적 공통 스탯
        definition.attackConfig?.ApplyTo(stats, enemyGO); // 공격 방식별 스탯

        // 주의: Initialize(stats)는 스탯이 전부 확정된 뒤에 호출해야 currentHealth가 맞게 계산됩니다(EnemyHealth.cs 참고).
        var health = enemyGO.GetComponent<EnemyHealth>();
        if (health == null) health = enemyGO.AddComponent<EnemyHealth>();
        health.Initialize(stats);

        var agent = enemyGO.GetComponent<NavMeshAgent>();
        if (agent == null) agent = enemyGO.AddComponent<NavMeshAgent>();
        agent.speed = stats.moveSpeed;

        AttachAppropriateAIController(enemyGO);

        return enemyGO;
    }

    // ---------- 보스 ----------

    // 주의: 보스는 SpawnOne()/AttachAppropriateAIController()를 거치면 안 됩니다 - 수치는 보스 프리팹 컴포넌트에 직접 설정되어 있어
    // ApplyFrom/ApplyTo가 덮어쓰고, EnemyRangedAttack 때문에 EnemyRangedAIController가 붙어 BossAIController와 충돌합니다.
    private void SpawnBoss(RoomInstance room, Transform parent, List<SpawnRecord> records)
    {
        var entry = FindMatchingBossPoolEntry(room);
        if (entry == null || entry.bossPrefabs == null || entry.bossPrefabs.Count == 0)
        {
            Debug.LogWarning($"RealEnemySpawner: 보스방(variantId='{room.variantId}')에 매칭되는 " +
                              "보스 풀이 없어 보스를 스폰하지 않았습니다. bossPool에 variantId가 " +
                              "비어있는 기본 항목을 최소 하나는 등록해두세요.");
            return;
        }

        GameObject bossPrefab = PickWeightedPrefab(entry.bossPrefabs);
        if (bossPrefab == null)
        {
            Debug.LogWarning("RealEnemySpawner: 매칭된 보스 풀에 유효한 프리팹이 없어 보스를 스폰하지 않았습니다.");
            return;
        }

        Vector3 spawnPos = GetBossSpawnPosition(parent.position);
        Quaternion spawnRot = GetInitialBossFacingRotation();

        SpawnBossFromPrefab(bossPrefab, spawnPos, spawnRot, parent);
        records.Add(new SpawnRecord { bossPrefab = bossPrefab, position = spawnPos, rotation = spawnRot });
    }

    // 보스 한 마리를 실제로 만들어 세팅합니다(처음 스폰과 재스폰 공용).
    private GameObject SpawnBossFromPrefab(GameObject bossPrefab, Vector3 spawnPos, Quaternion spawnRot, Transform parent)
    {
        GameObject bossGO = Instantiate(bossPrefab, spawnPos, spawnRot, parent);
        bossGO.name = $"Boss_{bossPrefab.name}";
        ApplyEnemyTagAndLayer(bossGO);

        // 주의: EnemyHealth.Initialize(stats)는 보스도 반드시 호출 - 안 하면 currentHealth가 0으로 남아 첫 피격에 바로 죽습니다.
        var stats = bossGO.GetComponent<EnemyStats>();
        var health = bossGO.GetComponent<EnemyHealth>();
        if (stats != null && health != null)
        {
            health.Initialize(stats);
        }
        else
        {
            Debug.LogWarning($"RealEnemySpawner: 보스 프리팹 '{bossPrefab.name}'에 EnemyStats 또는 " +
                              "EnemyHealth가 없어 체력을 초기화하지 못했습니다. 보스 프리팹 루트에 " +
                              "두 컴포넌트가 모두 있는지 확인해주세요.");
        }

        return bossGO;
    }

    // variantId 일치 항목 우선, 없으면 variantId가 빈 기본 항목(LevelBuilder.FindMatchingWallSet()과 같은 규칙).
    private BossPoolEntry FindMatchingBossPoolEntry(RoomInstance room)
    {
        if (!string.IsNullOrEmpty(room.variantId))
        {
            var byVariant = bossPool.Find(e => !string.IsNullOrEmpty(e.variantId) && e.variantId == room.variantId);
            if (byVariant != null) return byVariant;
        }

        return bossPool.Find(e => string.IsNullOrEmpty(e.variantId));
    }

    // 방 정중앙을 NavMesh 위로 스냅(실패하면 정중앙 그대로 - 보스는 반드시 나와야 하므로).
    private Vector3 GetBossSpawnPosition(Vector3 roomCenter)
    {
        if (NavMesh.SamplePosition(roomCenter, out NavMeshHit hit, navMeshSampleDistance, NavMesh.AllAreas))
        {
            return hit.position;
        }
        return roomCenter;
    }

    // 카메라가 보는 수평 방향(피치 제외)을 보스의 첫 방향으로 씁니다. 이후엔 BossAIController가 플레이어 쪽으로 돌립니다.
    private Quaternion GetInitialBossFacingRotation()
    {
        Vector3 camForward = Quaternion.Euler(bossSpawnFacingEulerAngles) * Vector3.forward;
        camForward.y = 0f;

        if (camForward.sqrMagnitude < 0.0001f) return Quaternion.identity;

        return Quaternion.LookRotation(camForward.normalized);
    }

    // ---------- 공용 ----------

    // Enemy 태그 + 적 물리 레이어 적용(보스 프리팹에 이미 맞춰져 있어도 다시 적용 - 바뀌면 총알이 그냥 통과하는 버그 방지).
    private void ApplyEnemyTagAndLayer(GameObject enemyGO)
    {
        enemyGO.tag = "Enemy";

        int layerIndex = LayerMask.NameToLayer(enemyBodyLayerName);
        if (layerIndex >= 0)
        {
            enemyGO.layer = layerIndex;
        }
        else
        {
            Debug.LogWarning($"RealEnemySpawner: '{enemyBodyLayerName}' 레이어가 프로젝트에 없습니다.");
        }
    }

    // 붙어 있는 공격 방식 컴포넌트에 맞는 AI 컨트롤러를 붙입니다. 일반 적 전용(보스는 위 주의 참고).
    private void AttachAppropriateAIController(GameObject enemyGO)
    {
        if (enemyGO.GetComponent<EnemyRangedAttack>() != null)
        {
            if (enemyGO.GetComponent<EnemyRangedAIController>() == null)
            {
                enemyGO.AddComponent<EnemyRangedAIController>();
            }
            return;
        }

        if (enemyGO.GetComponent<IEnemyAttackBehavior>() != null) // EnemyMeleeAttack 등
        {
            if (enemyGO.GetComponent<EnemyAIController>() == null)
            {
                enemyGO.AddComponent<EnemyAIController>();
            }
            return;
        }

        Debug.LogWarning($"RealEnemySpawner: '{enemyGO.name}'에서 공격 방식 컴포넌트를 찾지 못해 " +
                          "AI 컨트롤러를 붙이지 못했습니다. 이 적은 아무 행동도 하지 않습니다.");
    }

    // 후보 자리가 (1) NavMesh 위, (2) 다른 물체와 안 겹침, (3) 출입구에서 충분히 떨어짐, (4) 방 바닥에서 걸어갈 수 있음 - 을 모두 만족하면 채택.
    private bool TryFindSpawnPosition(Vector3 roomCenter, Vector3 roomAnchor, bool hasRoomAnchor, List<Vector3> doorPositions, out Vector3 result)
    {
        for (int attempt = 0; attempt < maxPositionAttempts; attempt++)
        {
            Vector2 offset = Random.insideUnitCircle * spawnRadius;
            Vector3 candidate = roomCenter + new Vector3(offset.x, 0f, offset.y);

            if (!NavMesh.SamplePosition(candidate, out NavMeshHit navHit, navMeshSampleDistance, NavMesh.AllAreas))
                continue;

            if (Physics.CheckSphere(navHit.position, overlapCheckRadius, overlapCheckMask, QueryTriggerInteraction.Ignore))
                continue;

            if (IsTooCloseToAnyDoor(navHit.position, doorPositions))
                continue;

            if (hasRoomAnchor && !IsReachableFromAnchor(roomAnchor, navHit.position))
                continue; // 벽 뒤, 장애물 위처럼 방 바닥과 끊어진 NavMesh

            result = navHit.position;
            return true;
        }

        result = Vector3.zero;
        return false;
    }

    // EnemyRangedAIController.TryRetreat()의 도달 가능성 검사와 같은 방식.
    private bool IsReachableFromAnchor(Vector3 from, Vector3 to)
    {
        NavMeshPath path = new NavMeshPath();
        if (!NavMesh.CalculatePath(from, to, NavMesh.AllAreas, path)) return false;
        return path.status == NavMeshPathStatus.PathComplete;
    }

    // 적 스폰 시점엔 플레이어가 아직 이 방에 없으므로, 플레이어가 들어올 출입구 위치로 대신 거리 판정합니다.
    private bool IsTooCloseToAnyDoor(Vector3 position, List<Vector3> doorPositions)
    {
        if (doorPositions == null) return false;

        foreach (var door in doorPositions)
        {
            if (Vector3.Distance(position, door) < minPlayerDistance) return true;
        }
        return false;
    }

    // 열린 방향마다 방 중심 + cellSize/2 오프셋(LevelBuilder.GetNeighborsByDirection()/GetWallOffset()과 같은 계산).
    private List<Vector3> GetDoorWorldPositions(RoomInstance room, Vector3 roomCenter)
    {
        var doors = new List<Vector3>();
        float half = cellSize / 2f;

        foreach (var link in room.anchors)
        {
            Direction dir = (link.roomA == room) ? link.directionFromA : DirectionUtil.Opposite(link.directionFromA);
            if (DirectionUnitOffset.TryGetValue(dir, out var unit))
            {
                doors.Add(roomCenter + unit * half);
            }
        }

        return doors;
    }

    private EnemyDefinition PickWeighted(List<EnemyDefinition> pool)
    {
        var valid = pool.Where(d => d != null && d.weight > 0f).ToList();
        if (valid.Count == 0) return null;
        if (valid.Count == 1) return valid[0];

        float totalWeight = valid.Sum(d => d.weight);
        float roll = Random.value * totalWeight;
        float cumulative = 0f;
        foreach (var d in valid)
        {
            cumulative += d.weight;
            if (roll <= cumulative) return d;
        }
        return valid[valid.Count - 1];
    }

    // LevelBuilder.PickWeightedPrefab()과 같은 로직(그쪽이 private이라 복제).
    private GameObject PickWeightedPrefab(List<WeightedPrefab> candidates)
    {
        if (candidates == null || candidates.Count == 0) return null;

        var valid = candidates.Where(c => c.prefab != null && c.weight > 0f).ToList();
        if (valid.Count == 0) return null;
        if (valid.Count == 1) return valid[0].prefab;

        float totalWeight = valid.Sum(c => c.weight);
        float roll = Random.value * totalWeight;
        float cumulative = 0f;
        foreach (var candidate in valid)
        {
            cumulative += candidate.weight;
            if (roll <= cumulative) return candidate.prefab;
        }
        return valid[valid.Count - 1].prefab;
    }
}

// 보스 풀 항목 - variantId(그 보스방의 RoomInstance.variantId)로 우선 매칭, 비어 있으면 기본/폴백 항목.
[System.Serializable]
public class BossPoolEntry
{
    [Tooltip("채우면 그 variantId의 보스방에서만 사용, 비우면 기본 항목(기본 항목이 하나도 없으면 매칭 실패 시 보스가 안 나옴).")]
    public string variantId;

    [Tooltip("매칭됐을 때 가중치로 하나를 뽑을 보스 프리팹들.")]
    public List<WeightedPrefab> bossPrefabs = new List<WeightedPrefab>();
}
