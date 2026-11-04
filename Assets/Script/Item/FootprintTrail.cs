using System.Collections.Generic;
using UnityEngine;

// 아이템 2번(FootprintTrailEffect)을 얻으면 플레이어에 자동으로 붙는 실제 작동부입니다.
//
// [하는 일]
// 1. 발자국 놓기: 플레이어가 실제로 stepDistance만큼 움직일 때마다 발 높이에 납작한
//    스프라이트를 하나씩 놓습니다. 이동 방향으로 회전시키고, 좌우 발을 번갈아 씁니다.
//    발자국 오브젝트는 미리 만들어 재활용하므로(풀링) 많이 쌓여도 가볍습니다.
// 2. 경로 위 판정: 물리 충돌(트리거)을 쓰지 않고, 발자국 위치를 격자에 기록해두었다가
//    적의 바닥 좌표와의 거리만 계산합니다. 물리 레이어 설정에 의존하지 않아 안전합니다.
// 3. 적 감속: 경로 위에 있는 적에게 EnemySpeedModifier로 이동 속도 보정을 겁니다. 경로를
//    벗어나면 즉시 해제됩니다.
// 4. 수명: 발자국은 놓인 뒤 lifetime초 동안 유지되다가 fadeDuration초에 걸쳐 서서히
//    투명해지고 사라집니다(사라지는 순간 그 자리의 감속도 함께 풀립니다). 발자국은 항상
//    놓인 순서대로 쌓이므로 가장 오래된 것부터 차례로 처리합니다.
// 5. 초기화: 플레이어가 다른 방에 들어가는 순간(RoomController.OnAnyRoomEntered - 43번
//    아이템이 방을 나가면 버프를 해제할 때 쓰는 것과 같은 신호) 모든 발자국을 지웁니다.
//    새 레벨로 넘어갈 때도 시작방 EnterRoom에서 같은 신호가 나가므로 함께 초기화됩니다.
//
// [발자국이 놓이는 위치/높이]
// 플레이어 CharacterController의 바닥(bounds.min.y) + groundOffset에 놓습니다. 바닥
// 레이어를 따로 지정할 필요가 없고, 단차가 있는 바닥에서도 플레이어 발 높이를 따라갑니다.
// 발자국 오브젝트는 플레이어의 자식이 아니라 씬 최상위의 별도 루트("FootprintTrailRoot")
// 아래에 만들어집니다(플레이어를 따라 움직이면 안 되므로).
//
// [적 탐색]
// 활성화된 EnemyStats를 0.25초마다 한 번 찾아 EnemySpeedModifier를 붙여두고(꺼진 방의
// 적은 자동으로 제외됩니다), 0.05초마다 각 적이 경로 위인지 판정합니다.
//
// [돌진(대시)은 감속되지 않음]
// 근접 적 돌진은 NavMeshAgent.Move()로 직접 이동해서 agent.speed의 영향을 받지 않습니다
// (EnemySpeedModifier.cs 상단 주석 참고, 형빈 확정 - 걷기만 감속).
[DisallowMultipleComponent]
public class FootprintTrail : MonoBehaviour
{
    private class Footprint
    {
        public GameObject go;
        public Transform tr;
        public SpriteRenderer sr;
        public Vector3 position;
        public Vector2Int cell;
        public float spawnTime;   // 놓인 시각(Time.time)
        public Color baseColor;   // 놓일 때의 색(서서히 사라질 때 이 색의 투명도만 줄임)
    }

    // 격자 한 칸의 크기(월드 단위)입니다. 판정 반경이 이보다 커도 탐색 칸 수를 늘려서
    // 처리하므로(IsOnTrail 참고) 인스펙터 값과 무관하게 고정입니다.
    private const float GridCellSize = 1f;
    private const float EnemyScanInterval = 0.25f;
    private const float SlowCheckInterval = 0.05f;

    private FootprintTrailEffect settings;
    private string slowSourceId = "footprint";

    private CharacterController controller;
    private Transform root;

    // 놓인 순서대로(가장 오래된 것이 앞) 들어있는 사용 중인 발자국입니다.
    private readonly Queue<Footprint> active = new Queue<Footprint>();
    private readonly Stack<Footprint> pool = new Stack<Footprint>();
    private readonly Dictionary<Vector2Int, List<Footprint>> grid = new Dictionary<Vector2Int, List<Footprint>>();

    private Vector3 anchor;
    private bool hasAnchor = false;
    private bool nextIsLeft = true;

    private readonly List<EnemySpeedModifier> trackedEnemies = new List<EnemySpeedModifier>();
    private readonly List<EnemySpeedModifier> previousTracked = new List<EnemySpeedModifier>();
    private readonly HashSet<EnemySpeedModifier> scanSet = new HashSet<EnemySpeedModifier>();
    private float enemyScanTimer = 0f;
    private float slowCheckTimer = 0f;

    private static Sprite fallbackSprite;

    // 아이템 효과(FootprintTrailEffect.Apply)가 설정을 넘겨줍니다.
    public void Configure(FootprintTrailEffect effect, string sourceItemId)
    {
        settings = effect;
        slowSourceId = "footprint_" + sourceItemId;
    }

    void Awake()
    {
        controller = GetComponent<CharacterController>();
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
        ReleaseAllEnemySlows();

        if (root != null) Destroy(root.gameObject);
    }

    // 다른 방에 들어가는 순간 발자국과 감속을 전부 지웁니다.
    private void HandleRoomEntered(RoomInstance room)
    {
        ClearAll();
    }

    void Update()
    {
        if (settings == null) return;

        UpdateFootprintPlacement();
        UpdateFootprintLifetime();

        enemyScanTimer -= Time.deltaTime;
        if (enemyScanTimer <= 0f)
        {
            enemyScanTimer = EnemyScanInterval;
            ScanEnemies();
        }

        slowCheckTimer -= Time.deltaTime;
        if (slowCheckTimer <= 0f)
        {
            slowCheckTimer = SlowCheckInterval;
            UpdateEnemySlows();
        }
    }

    // ---------- 발자국 놓기 ----------
    private void UpdateFootprintPlacement()
    {
        Vector3 pos = transform.position;

        if (!hasAnchor)
        {
            anchor = pos;
            hasAnchor = true;
            return;
        }

        Vector3 delta = pos - anchor;
        delta.y = 0f;
        float dist = delta.magnitude;
        float step = Mathf.Max(0.05f, settings.stepDistance);

        // 한 프레임에 step의 8배 넘게 움직였다면 텔레포트 등으로 순간이동한 것입니다 -
        // 그 사이에 발자국을 줄줄이 깔지 않고 기준점만 새로 잡습니다.
        if (dist > step * 8f)
        {
            anchor = pos;
            return;
        }

        if (dist < step) return;

        Vector3 dir = delta / dist;
        int guard = 0;

        // 한 프레임에 여러 칸을 움직였어도(빠른 이동) 간격이 일정하도록 경로를 따라 하나씩 놓습니다.
        while (dist >= step && guard < 8)
        {
            anchor += dir * step;
            PlaceFootprint(anchor, dir);
            dist -= step;
            guard++;
        }

        anchor.y = pos.y;
    }

    // ---------- 발자국 수명 ----------
    // 수명이 지난 발자국을 서서히 투명하게 만들고, 완전히 사라진 것은 풀로 돌려보냅니다.
    // active 큐는 놓인 순서(가장 오래된 것이 앞)이므로 앞에서부터 보다가 아직 수명 안인
    // 발자국을 만나면 나머지도 전부 수명 안이므로 거기서 멈춥니다.
    private void UpdateFootprintLifetime()
    {
        if (settings.lifetime <= 0f) return;

        float lifetime = settings.lifetime;
        float fade = Mathf.Max(0f, settings.fadeDuration);
        float now = Time.time;

        // 완전히 사라질 때가 된 발자국 제거 (그 자리의 감속도 격자에서 빠지며 함께 풀림)
        while (active.Count > 0 && now - active.Peek().spawnTime >= lifetime + fade)
        {
            RecycleOldest();
        }

        if (fade <= 0f) return;

        // 수명이 지났지만 아직 완전히 사라지지 않은 발자국은 투명도를 줄입니다.
        foreach (Footprint fp in active)
        {
            float age = now - fp.spawnTime;
            if (age < lifetime) break;

            float t = Mathf.Clamp01((age - lifetime) / fade);
            Color c = fp.baseColor;
            c.a *= 1f - t;
            fp.sr.color = c;
        }
    }

    private void PlaceFootprint(Vector3 center, Vector3 dir)
    {
        EnsureRoot();

        Footprint fp = AcquireFootprint();

        Vector3 right = new Vector3(dir.z, 0f, -dir.x); // 진행 방향 기준 오른쪽 (위에서 봤을 때)
        bool isLeft = nextIsLeft;
        nextIsLeft = !nextIsLeft;

        Vector3 p = center + right * (isLeft ? -settings.lateralOffset : settings.lateralOffset);
        p.y = GetFootHeight() + settings.groundOffset;

        // 스프라이트 결정 - 오른발 칸이 비어 있으면 왼발을 좌우 반전해서 씁니다.
        Sprite left = settings.leftFootSprite != null ? settings.leftFootSprite : GetFallbackSprite();
        Sprite sprite;
        bool flip;
        if (isLeft)
        {
            sprite = left;
            flip = false;
        }
        else if (settings.rightFootSprite != null)
        {
            sprite = settings.rightFootSprite;
            flip = false;
        }
        else
        {
            sprite = left;
            flip = true;
        }

        fp.position = p;
        fp.cell = ToCell(p);

        fp.tr.position = p;
        // X를 90도 눕혀 바닥에 납작하게 깔고, Y 회전으로 스프라이트의 위쪽이 이동 방향을 향하게 합니다.
        float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        fp.tr.rotation = Quaternion.Euler(90f, yaw, 0f);

        float longest = Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);
        float scale = longest > 0.0001f ? settings.footprintSize / longest : 1f;
        fp.tr.localScale = new Vector3(scale, scale, scale);

        fp.sr.sprite = sprite;
        fp.sr.flipX = flip;
        fp.baseColor = settings.footprintColor;
        fp.spawnTime = Time.time;
        fp.sr.color = fp.baseColor;

        fp.go.SetActive(true);

        active.Enqueue(fp);
        AddToGrid(fp);
    }

    private float GetFootHeight()
    {
        return controller != null ? controller.bounds.min.y : transform.position.y;
    }

    // 최대 개수를 넘으면 가장 오래된 발자국을 재활용하고, 아니면 풀에서 꺼내거나 새로 만듭니다.
    private Footprint AcquireFootprint()
    {
        int max = Mathf.Max(1, settings.maxFootprints);

        // 인스펙터에서 최대 개수를 줄였을 때 초과분도 함께 정리합니다.
        while (active.Count > max)
        {
            RecycleOldest();
        }

        if (active.Count >= max)
        {
            Footprint oldest = active.Dequeue();
            RemoveFromGrid(oldest);
            return oldest;
        }

        if (pool.Count > 0) return pool.Pop();

        return CreateFootprint();
    }

    private void RecycleOldest()
    {
        Footprint oldest = active.Dequeue();
        RemoveFromGrid(oldest);
        oldest.go.SetActive(false);
        pool.Push(oldest);
    }

    private Footprint CreateFootprint()
    {
        var go = new GameObject("Footprint");
        go.transform.SetParent(root, false);

        var sr = go.AddComponent<SpriteRenderer>();

        return new Footprint { go = go, tr = go.transform, sr = sr };
    }

    private void EnsureRoot()
    {
        if (root != null) return;

        var rootGO = new GameObject("FootprintTrailRoot");
        root = rootGO.transform;
    }

    // ---------- 초기화 ----------
    private void ClearAll()
    {
        while (active.Count > 0)
        {
            Footprint fp = active.Dequeue();
            fp.go.SetActive(false);
            pool.Push(fp);
        }
        grid.Clear();

        ReleaseAllEnemySlows();

        // 다음 발자국은 지금 위치에서 새로 시작합니다(방 전환 직후 밀려난 거리만큼 이어 깔지 않도록).
        hasAnchor = false;
        nextIsLeft = true;
    }

    private void ReleaseAllEnemySlows()
    {
        for (int i = 0; i < trackedEnemies.Count; i++)
        {
            if (trackedEnemies[i] != null) trackedEnemies[i].RemoveMultiplier(slowSourceId);
        }
        trackedEnemies.Clear();
        previousTracked.Clear();
    }

    // ---------- 격자 (경로 위 판정용) ----------
    private static Vector2Int ToCell(Vector3 p)
    {
        return new Vector2Int(Mathf.FloorToInt(p.x / GridCellSize), Mathf.FloorToInt(p.z / GridCellSize));
    }

    private void AddToGrid(Footprint fp)
    {
        if (!grid.TryGetValue(fp.cell, out var list))
        {
            list = new List<Footprint>();
            grid[fp.cell] = list;
        }
        list.Add(fp);
    }

    private void RemoveFromGrid(Footprint fp)
    {
        if (grid.TryGetValue(fp.cell, out var list))
        {
            list.Remove(fp);
            if (list.Count == 0) grid.Remove(fp.cell);
        }
    }

    // 이 월드 좌표가 발자국 경로 위인지 판정합니다(높이는 무시하고 바닥면 거리만 봅니다).
    private bool IsOnTrail(Vector3 worldPos)
    {
        if (grid.Count == 0) return false;

        float radius = Mathf.Max(0.01f, settings.slowRadius);
        float radiusSqr = radius * radius;
        int range = Mathf.CeilToInt(radius / GridCellSize);
        Vector2Int center = ToCell(worldPos);

        for (int x = -range; x <= range; x++)
        {
            for (int z = -range; z <= range; z++)
            {
                if (!grid.TryGetValue(new Vector2Int(center.x + x, center.y + z), out var list)) continue;

                for (int i = 0; i < list.Count; i++)
                {
                    float dx = list[i].position.x - worldPos.x;
                    float dz = list[i].position.z - worldPos.z;
                    if (dx * dx + dz * dz <= radiusSqr) return true;
                }
            }
        }

        return false;
    }

    // ---------- 적 감속 ----------
    private void ScanEnemies()
    {
        scanSet.Clear();
        trackedEnemies.Clear();

        // 기본값(비활성 제외)이라 꺼져 있는 다른 방의 적은 잡히지 않습니다.
        var enemies = FindObjectsByType<EnemyStats>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < enemies.Length; i++)
        {
            if (AllyFaction.IsAlly(enemies[i].gameObject)) continue; // [87번] 포섭된 아군은 발자국에 감속되지 않습니다.
            var modifier = EnemySpeedModifier.GetOrAdd(enemies[i].gameObject);
            if (modifier == null) continue;

            trackedEnemies.Add(modifier);
            scanSet.Add(modifier);
        }

        // 이전 스캔에는 있었는데 이번에는 사라진 적(다른 방으로 꺼졌거나 초기화로 교체됨)에게
        // 걸려있던 감속은 그 자리에서 풀어줍니다 - 꺼진 채로 감속이 남아있지 않도록.
        for (int i = 0; i < previousTracked.Count; i++)
        {
            var old = previousTracked[i];
            if (old != null && !scanSet.Contains(old)) old.RemoveMultiplier(slowSourceId);
        }

        previousTracked.Clear();
        previousTracked.AddRange(trackedEnemies);

        // 새로 잡힌 적도 다음 판정 주기를 기다리지 않고 바로 판정합니다.
        slowCheckTimer = 0f;
    }

    private void UpdateEnemySlows()
    {
        for (int i = 0; i < trackedEnemies.Count; i++)
        {
            var modifier = trackedEnemies[i];
            if (modifier == null) continue;

            if (IsOnTrail(modifier.transform.position))
            {
                modifier.SetMultiplier(slowSourceId, settings.slowMultiplier);
            }
            else
            {
                modifier.RemoveMultiplier(slowSourceId);
            }
        }
    }

    // ---------- 임시 발자국 스프라이트 ----------
    // 스프라이트를 아직 넣지 않았을 때 쓰는 임시 타원(위쪽이 진행 방향인 길쭉한 모양)입니다.
    private static Sprite GetFallbackSprite()
    {
        if (fallbackSprite != null) return fallbackSprite;

        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;

        var pixels = new Color32[size * size];
        float cx = (size - 1) * 0.5f;
        float cy = (size - 1) * 0.5f;
        float rx = size * 0.28f; // 좌우 반지름 (좁게)
        float ry = size * 0.48f; // 위아래 반지름 (길게)

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float nx = (x - cx) / rx;
                float ny = (y - cy) / ry;
                float d = nx * nx + ny * ny;
                byte a = d <= 1f ? (byte)255 : (byte)0;
                pixels[y * size + x] = new Color32(255, 255, 255, a);
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();

        fallbackSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        return fallbackSprite;
    }
}
