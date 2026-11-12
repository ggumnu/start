using System.Collections.Generic;
using UnityEngine;

// 액티브 90번 - 사용하면 플레이어 자리에서 방 전체로 물결이 퍼져, 지나간 자리의 적 총알(보스 총알 포함)과 2레벨 보스 포격 표시를 없애고(피해 없음)
// 닿은 일반 적의 공격을 끊습니다(다 퍼질 때까지 일반 적은 새 공격 금지 - EnemyAttackLock). 보스는 진행 중인 연사만 끊기고 충전/돌진/전방위/포격 모션
// 등은 그대로이며 발광도 없고 공격 금지도 받지 않습니다. 물결 그림은 RoomWavePulse + RoomWave 셰이더. 2레벨 보스 소환 신호도 같은 그림만(SpawnVisualOnly).
// 아이템 에셋: Item Id = 90, Category = Active, Active Cost = 2, Active Effect = RoomWaveClearEffect, Wave Material = RoomWave 재질.
// 기획/설계 배경은 Docs/설계노트.md [90번 물결].
[System.Serializable]
public class RoomWaveClearEffect : IDelayedActiveItemEffect, IHasOwnUseSound
{
    [Header("물결 진행")]
    [Tooltip("물결이 플레이어 위치에서 방의 가장 먼 모서리까지 퍼지는 데 걸리는 시간(초)입니다. 이 시간 동안 적은 새 공격을 시작하지 못합니다.")]
    [Min(0.05f)] public float expandDuration = 0.6f;

    [Tooltip("물결이 이만큼(최대 반경 대비 비율) 퍼진 지점부터 서서히 투명해지기 시작해, 끝(1)에 닿는 순간 완전히 사라집니다(연출만 - " +
             "총알 제거/공격 중단/공격 금지와는 무관). 예) 0.6 = 반경의 60%까지는 진하게, 그 뒤로 점점 옅어짐. 1이면 끝까지 진하다가 바로 사라짐.")]
    [Range(0f, 1f)] public float fadeStartProgress = 0.6f;

    [Tooltip("물결이 다 퍼진 뒤에도 적의 공격 금지를 이 시간(초)만큼 더 유지합니다(기본 0 = 다 퍼지는 순간 풀림).")]
    [Min(0f)] public float extraLockTime = 0f;

    [Tooltip("방이 없는 씬(아이템 테스트 아레나)에서 쓰는 물결 최대 반경(m)입니다.")]
    [Min(0.1f)] public float fallbackRadius = 15f;

    [Header("물결 모양")]
    [Tooltip("RoomWave 셰이더로 만든 재질입니다(Assets/Shaders/RoomWave.shader). 비워두면 실행 중에 셰이더를 찾아 만들지만, 빌드에서는 빠질 수 있으니 꼭 넣어 주세요.")]
    public Material waveMaterial;

    [Tooltip("물결 색입니다(밝기 1 이상 = 빛 번짐).")]
    [ColorUsage(true, true)] public Color waveColor = new Color(0.55f, 0.9f, 1.6f, 1f);

    [Tooltip("앞 고리의 두께 = 최대 반경 x 이 값입니다.")]
    [Range(0.005f, 0.3f)] public float ringWidthRatio = 0.05f;

    [Tooltip("앞 고리 뒤를 따라오는 잔물결 수입니다(0~3).")]
    [Range(0, 3)] public int rippleCount = 2;

    [Tooltip("잔물결 사이 간격 = 최대 반경 x 이 값입니다.")]
    [Range(0.01f, 0.5f)] public float rippleSpacingRatio = 0.1f;

    [Tooltip("잔물결이 하나 뒤로 갈 때마다 곱해지는 밝기 비율입니다.")]
    [Range(0f, 1f)] public float rippleFalloff = 0.55f;

    [Tooltip("방 가장자리에서 물결이 부드럽게 사라지는 폭(m)입니다.")]
    [Min(0f)] public float edgeSoftness = 0.3f;

    [Tooltip("방 가장자리에서 안쪽으로 이만큼(m) 들어온 곳까지만 물결을 그립니다.")]
    [Min(0f)] public float roomInset = 0f;

    [Tooltip("바닥과 겹쳐 깜빡이지 않도록 물결을 바닥에서 띄우는 높이(m)입니다.")]
    public float groundOffset = 0.04f;

    [Header("물결이 닿은 적 표시 (잠깐 발광)")]
    [Tooltip("물결이 닿은 적이 잠깐 발광하는 색입니다(몸 색은 그대로, 발광만 - 돌진 충전 발광과 같은 방식).")]
    public Color enemyHitTintColor = new Color(0.45f, 0.85f, 1f, 1f);

    [Tooltip("발광 밝기 배율입니다(발광 = 위 색 x 이 값, 돌진 충전 발광 기본값과 같은 2).")]
    [Min(0f)] public float enemyHitGlowBoost = 2f;

    [Tooltip("발광하는 전체 시간(초)입니다. 0이면 발광하지 않습니다.")]
    [Min(0f)] public float enemyHitTintDuration = 0.5f;

    [Tooltip("위 시간의 끝부분에서 원래대로 부드럽게 돌아가는 시간(초)입니다.")]
    [Min(0f)] public float enemyHitTintFadeTime = 0.2f;

    [Header("총알 제거 이펙트 (선택)")]
    [Tooltip("물결에 지워진 적 총알 자리마다 재생할 이펙트입니다. 비워두면 이펙트 없이 사라집니다.")]
    public GameObject bulletClearVfxPrefab;
    [Min(0.01f)] public float bulletClearVfxScale = 1f;
    [Min(0.1f)] public float bulletClearVfxLifetime = 1f;

    public const string LockSourceId = "item90_room_wave";

    // 코스트를 내기 전에 호출됩니다. 지금 방에 살아 있는 적이나 적 총알이 하나라도 있어야 true입니다.
    public bool CanActivate(ActiveEffectContext context)
    {
        if (EnemyProjectile.Active.Count > 0) return true;
        if (BossArtilleryStrike.ActiveCount > 0) return true; // [2레벨 보스] 포격 표시만 떠 있어도 사용 가능

        var enemies = GetRoomEnemies(context);
        if (enemies == null) return false;
        foreach (var enemy in enemies)
        {
            if (enemy != null && !enemy.IsDead && enemy.gameObject.activeInHierarchy) return true;
        }
        return false;
    }

    public void Activate(ActiveEffectContext context)
    {
        if (context == null || context.PlayerTransform == null) return;

        Vector3 center = context.PlayerTransform.position;
        var enemies = GetRoomEnemies(context);

        // 방 바닥 사각형(x/z 최소~최대)과 물결 최대 반경(가장 먼 모서리까지)을 정합니다.
        Rect room;
        bool hasRoom = TryGetRoomRect(context, out room);
        float maxRadius;
        if (hasRoom)
        {
            float dx = Mathf.Max(Mathf.Abs(center.x - room.xMin), Mathf.Abs(center.x - room.xMax));
            float dz = Mathf.Max(Mathf.Abs(center.z - room.yMin), Mathf.Abs(center.z - room.yMax));
            maxRadius = Mathf.Sqrt(dx * dx + dz * dz);
        }
        else
        {
            maxRadius = fallbackRadius;
            room = new Rect(center.x - maxRadius, center.z - maxRadius, maxRadius * 2f, maxRadius * 2f);
        }

        // 물결이 다 퍼질 때까지 적의 새 공격을 막습니다(형빈 확정).
        EnemyAttackLock.Lock(LockSourceId, expandDuration + extraLockTime);

        GameAudio.Play(SfxId.RoomWave);
        RoomWavePulse.Spawn(this, center, maxRadius, room, enemies);
    }

    // 지연 소모가 없는 효과라 취소할 대기 상태가 없습니다.
    public void CancelPending(ActiveEffectContext context)
    {
    }

    // 지금 플레이어가 있는 방의 EnemyHealth 목록입니다(31번과 같은 조회). 방이 없는 씬(테스트 아레나)은 씬 전체.
    private static EnemyHealth[] GetRoomEnemies(ActiveEffectContext context)
    {
        if (context?.LevelBuilder == null)
        {
            return Object.FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None);
        }

        RoomInstance room = FindCurrentRoom(context.LevelBuilder);
        if (room == null) return null;

        var controller = context.LevelBuilder.GetRoomController(room.id);
        if (controller == null || controller.ContentRoot == null) return null;

        return controller.ContentRoot.GetComponentsInChildren<EnemyHealth>(true);
    }

    private static RoomInstance FindCurrentRoom(LevelBuilder levelBuilder)
    {
        if (levelBuilder == null || !levelBuilder.HasPlayerCell) return null;

        Vector2Int currentCell = levelBuilder.CurrentPlayerCell;
        foreach (var room in levelBuilder.GeneratedRooms)
        {
            if (room != null && room.occupiedCells.Contains(currentCell)) return room;
        }
        return null;
    }

    // 지금 방이 차지한 칸들을 모두 덮는 바닥 사각형입니다(칸 중심 = 칸 좌표 x cellSize - LevelBuilder.CellToWorld와 같은 규칙).
    private bool TryGetRoomRect(ActiveEffectContext context, out Rect rect)
    {
        rect = default;
        var levelBuilder = context?.LevelBuilder;
        RoomInstance room = FindCurrentRoom(levelBuilder);
        if (room == null || room.occupiedCells == null || room.occupiedCells.Count == 0) return false;

        float cell = levelBuilder.cellSize;
        if (cell <= 0f) return false;

        float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;
        foreach (var c in room.occupiedCells)
        {
            minX = Mathf.Min(minX, c.x * cell - cell * 0.5f);
            maxX = Mathf.Max(maxX, c.x * cell + cell * 0.5f);
            minZ = Mathf.Min(minZ, c.y * cell - cell * 0.5f);
            maxZ = Mathf.Max(maxZ, c.y * cell + cell * 0.5f);
        }

        float inset = Mathf.Min(roomInset, (maxX - minX) * 0.45f, (maxZ - minZ) * 0.45f);
        rect = Rect.MinMaxRect(minX + inset, minZ + inset, maxX - inset, maxZ - inset);
        return true;
    }
}

// [90번 물결 한 번] 물결 그림(바닥 사각형 + RoomWave 재질)을 움직이고, 물결 앞부분이 지나간 자리의 적 총알을 지우며, 물결이 닿은 적의
// 진행 중인 공격을 끊습니다. 다 퍼지면 남은 적/총알을 한 번 더 정리하고, 옅어진 뒤 스스로 지워집니다.
public class RoomWavePulse : MonoBehaviour
{
    private static readonly int CenterId = Shader.PropertyToID("_Center");
    private static readonly int RadiusId = Shader.PropertyToID("_Radius");
    private static readonly int WidthId = Shader.PropertyToID("_Width");
    private static readonly int RippleCountId = Shader.PropertyToID("_RippleCount");
    private static readonly int RippleSpacingId = Shader.PropertyToID("_RippleSpacing");
    private static readonly int RippleFalloffId = Shader.PropertyToID("_RippleFalloff");
    private static readonly int RoomRectId = Shader.PropertyToID("_RoomRect");
    private static readonly int EdgeSoftnessId = Shader.PropertyToID("_EdgeSoftness");
    private static readonly int FadeId = Shader.PropertyToID("_Fade");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private static Material fallbackMaterial;

    private RoomWaveClearEffect settings;
    private Vector3 center;
    private float maxRadius;
    private float elapsed;
    private bool expandFinished;
    private bool visualOnly; // [2레벨 보스 소환 신호] 그림만 - 총알 제거/공격 중단/발광 없음
    private readonly List<EnemyHealth> pendingEnemies = new List<EnemyHealth>();
    private readonly List<Vector3> clearedPositions = new List<Vector3>();

    private MeshRenderer meshRenderer;
    private Mesh mesh;
    private MaterialPropertyBlock block;

    // [2레벨 보스 소환 신호] 같은 물결 그림만 퍼뜨립니다 - 적 총알 제거/공격 중단/발광이 없고, 공격 금지(EnemyAttackLock)도 걸지 않습니다.
    public static void SpawnVisualOnly(RoomWaveClearEffect settings, Vector3 center, float maxRadius, Rect room)
    {
        Spawn(settings, center, maxRadius, room, null, true);
    }

    public static void Spawn(RoomWaveClearEffect settings, Vector3 center, float maxRadius, Rect room, EnemyHealth[] enemies, bool visualOnly = false)
    {
        var go = new GameObject("RoomWavePulse");
        var pulse = go.AddComponent<RoomWavePulse>();
        // [방 전환] 플레이어가 다른 방에 들어가면 바로 지워지도록 등록합니다(RoomScope - 다른 방 위 허공에 남지 않게).
        RoomScope.DestroyWhenPlayerLeaves(go);
        pulse.settings = settings;
        pulse.visualOnly = visualOnly;
        pulse.center = center;
        pulse.maxRadius = Mathf.Max(maxRadius, 0.1f);

        if (enemies != null)
        {
            foreach (var enemy in enemies)
            {
                if (enemy != null && !enemy.IsDead) pulse.pendingEnemies.Add(enemy);
            }
        }

        try
        {
            pulse.BuildVisual(room);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"RoomWavePulse: 물결 그림 생성 중 오류가 발생해 그림 없이 진행합니다(총알 제거/공격 중단은 그대로): {e}");
        }

        pulse.Step(0f); // 사용한 순간(반경 0) 처리
    }

    void Update()
    {
        Step(Time.deltaTime);
    }

    void Step(float deltaTime)
    {
        elapsed += deltaTime;
        float expand = Mathf.Max(settings.expandDuration, 0.05f);
        float t = Mathf.Clamp01(elapsed / expand);

        // 처음엔 빠르게, 끝으로 갈수록 느려지는 퍼짐
        float eased = 1f - (1f - t) * (1f - t);
        float radius = maxRadius * eased;

        if (!expandFinished)
        {
            if (!visualOnly) ApplyToRadius(radius);
            if (t >= 1f)
            {
                expandFinished = true;
                // 다 퍼졌습니다 - 혹시 남은 적/총알(가장자리 끝 등)을 한 번 더 정리합니다.
                if (!visualOnly) ApplyToRadius(maxRadius * 1.05f + 0.5f);
                pendingEnemies.Clear();
            }
        }

        // 그림: 퍼지면서 fadeStartProgress 지점부터 서서히 옅어져, 끝(최대 반경)에 닿는 순간 완전히 사라집니다(형빈 확정 - 끝에서 멈춘
        // 채로 사라지는 모습이 보이지 않도록). 진행도는 반경 기준(eased)이라 "바깥쪽까지 나갔을 즈음"부터 옅어집니다.
        float fadeStart = Mathf.Clamp01(settings.fadeStartProgress);
        float fade = fadeStart < 1f ? 1f - Mathf.Clamp01((eased - fadeStart) / (1f - fadeStart)) : (eased < 1f ? 1f : 0f);
        fade = fade * fade * (3f - 2f * fade); // 부드럽게
        UpdateVisual(radius, fade);

        if (expandFinished) Destroy(gameObject);
    }

    // radius 안의 적 총알을 지우고, 아직 공격을 끊지 않은 적 중 radius 안에 들어온 적의 공격을 끊습니다.
    void ApplyToRadius(float radius)
    {
        if (radius > 0f)
        {
            clearedPositions.Clear();
            EnemyProjectile.ClearInRadius(center, radius, 1000f, settings.bulletClearVfxPrefab != null ? clearedPositions : null);
            // [2레벨 보스] 물결 앞이 닿은 포격 표시(떨어지던 포탄 포함)도 터지지 않고 사라집니다 - 그 순간 떠 있는 것만.
            BossArtilleryStrike.ClearInRadius(center, radius, settings.bulletClearVfxPrefab != null ? clearedPositions : null);
            if (settings.bulletClearVfxPrefab != null)
            {
                foreach (var pos in clearedPositions) SpawnClearVfx(pos);
            }
        }

        float sqr = radius * radius;
        for (int i = pendingEnemies.Count - 1; i >= 0; i--)
        {
            var enemy = pendingEnemies[i];
            if (enemy == null || enemy.IsDead)
            {
                pendingEnemies.RemoveAt(i);
                continue;
            }

            Vector3 p = enemy.transform.position;
            float dx = p.x - center.x;
            float dz = p.z - center.z;
            if (dx * dx + dz * dz > sqr) continue;

            InterruptEnemy(enemy);
            if (!IsBoss(enemy)) FlashEnemy(enemy); // 보스는 연사만 끊기고 나머지는 그대로라 "효과가 들어갔다"는 발광을 하지 않음
            pendingEnemies.RemoveAt(i);
        }
    }

    // 일반 적: 진행 중인 공격을 모두 끊습니다(돌진/충전 - ICancellableAttack, 사격 연사 - EnemyRangedAttack). 강력 제어(수면/해킹)가
    // 공격을 끊는 것과 같은 방식이고, 끊긴 공격은 각자 대기시간(쿨다운) 상태로 넘어갑니다.
    // 보스(형빈 확정): 진행 중인 사격 연사만 끊습니다 - 1레벨 연속 돌진/돌진 충전/전방위 충전, 2레벨 보호막 충전/포격 모션/무한 돌진은 그대로.
    static void InterruptEnemy(EnemyHealth enemy)
    {
        if (!IsBoss(enemy))
        {
            foreach (var attack in enemy.GetComponents<ICancellableAttack>())
            {
                attack?.CancelAttack();
            }
        }
        var ranged = enemy.GetComponent<EnemyRangedAttack>();
        if (ranged != null) ranged.CancelBurst();
    }

    // 프로젝트 공통 보스 판별(BossAIController 또는 그것을 상속한 컨트롤러).
    static bool IsBoss(EnemyHealth enemy)
    {
        return enemy != null && enemy.GetComponent<BossAIController>() != null;
    }

    // 물결이 닿았다는 표시로 잠깐 하늘색으로 발광시킵니다(EnemyControlState.FlashGlow - 제어 아님, 발광만).
    void FlashEnemy(EnemyHealth enemy)
    {
        if (settings.enemyHitTintDuration <= 0f) return;
        var control = EnemyControlState.GetOrAdd(enemy.gameObject);
        if (control != null)
        {
            control.FlashGlow(settings.enemyHitTintColor, settings.enemyHitGlowBoost,
                              settings.enemyHitTintDuration, settings.enemyHitTintFadeTime);
        }
    }

    void SpawnClearVfx(Vector3 position)
    {
        GameObject vfx = Instantiate(settings.bulletClearVfxPrefab, position, Quaternion.identity);
        // [방 전환] 플레이어가 다른 방에 들어가면 바로 지워지도록 등록합니다(RoomScope - 다른 방 위 허공에 남지 않게).
        RoomScope.DestroyWhenPlayerLeaves(vfx);
        if (!Mathf.Approximately(settings.bulletClearVfxScale, 1f))
        {
            foreach (var ps in vfx.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                main.startSizeMultiplier *= settings.bulletClearVfxScale;
                main.startSpeedMultiplier *= settings.bulletClearVfxScale;
            }
        }
        Destroy(vfx, settings.bulletClearVfxLifetime);
    }

    // ---- 그림 ----

    void BuildVisual(Rect room)
    {
        Material mat = settings.waveMaterial != null ? settings.waveMaterial : GetFallbackMaterial();
        if (mat == null)
        {
            Debug.LogWarning("RoomWavePulse: RoomWave 재질/셰이더를 찾지 못해 물결 그림 없이 진행합니다. 90번 Item Definition의 Wave Material 칸을 채워 주세요.");
            return;
        }

        float y = center.y + settings.groundOffset;
        mesh = new Mesh { name = "RoomWaveQuad" };
        mesh.vertices = new[]
        {
            new Vector3(room.xMin, y, room.yMin), new Vector3(room.xMax, y, room.yMin),
            new Vector3(room.xMin, y, room.yMax), new Vector3(room.xMax, y, room.yMax),
        };
        mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
        mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
        mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        mesh.RecalculateBounds();

        gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        meshRenderer = gameObject.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = mat;
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        meshRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        meshRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

        block = new MaterialPropertyBlock();
        block.SetVector(CenterId, new Vector4(center.x, center.y, center.z, 0f));
        block.SetVector(RoomRectId, new Vector4(room.xMin, room.yMin, room.xMax, room.yMax));
        block.SetFloat(WidthId, Mathf.Max(maxRadius * settings.ringWidthRatio, 0.01f));
        block.SetFloat(RippleCountId, settings.rippleCount);
        block.SetFloat(RippleSpacingId, Mathf.Max(maxRadius * settings.rippleSpacingRatio, 0.01f));
        block.SetFloat(RippleFalloffId, settings.rippleFalloff);
        block.SetFloat(EdgeSoftnessId, settings.edgeSoftness);
        block.SetColor(ColorId, settings.waveColor);
    }

    void UpdateVisual(float radius, float fade)
    {
        if (meshRenderer == null || block == null) return;
        block.SetFloat(RadiusId, radius);
        block.SetFloat(FadeId, fade);
        meshRenderer.SetPropertyBlock(block);
    }

    static Material GetFallbackMaterial()
    {
        if (fallbackMaterial != null) return fallbackMaterial;
        Shader shader = Shader.Find("MobArchive/RoomWave");
        if (shader == null) return null;
        fallbackMaterial = new Material(shader) { name = "RoomWave_Runtime" };
        return fallbackMaterial;
    }

    void OnDestroy()
    {
        if (mesh != null) Destroy(mesh);
    }
}
