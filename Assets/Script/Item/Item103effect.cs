using System;
using System.Collections;
using UnityEngine;

// 장신구 103번 - "가지고 있으면 공격력이 30% 증가한다. 피해를 입으면 대신 30% 감소한다. 방에 들어갈 때마다 초기화된다."
//
// [기획 - 형빈 확정]
// - 장착하면 공격력 x1.3, 피해를 입으면 x0.7로 바뀝니다. 기준은 항상 "103번이 없을 때의 공격력"입니다(100 → 130, 맞으면 70.
//   130의 30% 감소가 아님). 곱하기 수정치라서 다른 공격력 아이템을 나중에 얻어도 자동으로 같은 기준이 됩니다.
// - 한 번 맞으면 그 방에서는 계속 x0.7입니다(더 맞아도 더 깎이지 않음).
// - 다른 방에 들어갈 때마다 x1.3으로 돌아갑니다(다음 레벨 시작방 포함).
// - 특수방 출입 피해(SpecialRoomHealthHazard)로는 감소하지 않습니다. 그 피해는 방에 들어가는 순간 생기므로 "방에 들어가는
//   중에 받은 피해"는 세지 않습니다 - 특수방에 들어간 뒤에도 x1.3이 유지됩니다(나올 때도 마찬가지).
// - 그 외의 피해(적 공격, 수류탄 자폭, 81번 불 구역 등)는 모두 피격으로 칩니다. 무적 시간 중에는 피해 자체가 없으므로 감소하지 않습니다.
// - 버리거나 교체하거나 잃으면 수정치가 사라져 원래 공격력으로 돌아갑니다. (전투 중인 방에서는 Ctrl 버리기 창이 열리지 않습니다.)
// - 클리어한 방에서 버렸다가 다시 주우면 x1.3으로 다시 시작합니다(형빈 확정 - 그대로 둠).
// - 공격력 하한선(PlayerStats의 Stat Floors)은 그대로 지켜집니다 - 최종 공격력 계산이 하한선에서 자릅니다.
//
// [감소 연출 - 형빈 확정]
// 맞아서 x0.7이 되는 순간 103번 아이콘이 빨간 발광/입자와 함께 플레이어 머리 위로 떠오르고(72번과 같은 아이콘 연출 -
// CreditPopupDisplay), 떠오른 뒤 penaltyVfxPrefab(VFX_ImpactCritical_1.1.0_Pink)이 머리 위에서 터지며 아이콘이 투명하게 사라집니다.
// 아이콘은 화면 UI라 파티클보다 항상 앞에 그려집니다(이펙트는 아이콘 뒤/주변으로 보임).
// [머리 위 알림 줄 세우기] 다른 머리 위 알림(111/39번 등)과 같이 생기면 순서대로 나오고, 이펙트는 아이콘이 실제로 떠오르는 순간부터
// vfxDelay 뒤에 터집니다(CreditPopupDisplay의 onStarted). 다른 방에 들어가면 남은 연출/이펙트는 건너뜁니다.
//
// [구현]
// - PlayerStats에 Damage 곱하기 수정치(x1.3 또는 x0.7)를 하나만 걸어 둡니다(이 아이템 전용 출처 이름으로).
// - 피격: PlayerHealth.OnDamageTaken. 방 진입: RoomController.OnAnyRoomEntered.
// - 특수방 출입 피해 제외: 그 피해는 방 진입 알림 도중에 생기므로, 피해를 받는 순간 "플레이어가 있는 방"(LevelBuilder 기준)이
//   이 효과가 마지막으로 알고 있는 방과 다르거나, 방에 들어간 바로 그 프레임이면 무시합니다(알림 순서와 상관없이 동작).
//
// [아이템 에셋 설정]
// ItemDefinition: Item Id = 103, Category = Trinket, Trinket Effect = GlassCannonTrinketEffect.
// Penalty Vfx Prefab 칸에 VFX_ImpactCritical_1.1.0_Pink를 넣습니다.
[Serializable]
public class GlassCannonTrinketEffect : ITrinketEffect
{
    [Header("공격력 배율")]
    [Tooltip("피격 전 공격력 배율입니다(기본 1.3 = 30% 증가). 103번이 없을 때의 공격력 기준입니다.")]
    [Min(0f)] public float attackBonusMultiplier = 1.3f;

    [Tooltip("피격 후 공격력 배율입니다(기본 0.7 = 30% 감소). 103번이 없을 때의 공격력 기준입니다.")]
    [Min(0f)] public float attackPenaltyMultiplier = 0.7f;

    [Header("감소 연출 - 머리 위 아이콘 (72번과 같은 연출, 빨강)")]
    [Tooltip("맞아서 공격력이 깎일 때 머리 위에 띄우는 아이콘 연출입니다. Icon을 비우면 103번 아이템 아이콘을 씁니다.")]
    public CreditPopupSettings penaltyPopup = new CreditPopupSettings
    {
        glowColor = new Color(1f, 0.12f, 0.12f, 0.9f),
        sparkleColor = new Color(1f, 0.3f, 0.25f, 1f),
    };

    [Header("감소 연출 - 이펙트")]
    [Tooltip("아이콘이 다 떠오른 뒤 머리 위에서 터뜨릴 이펙트입니다(VFX_ImpactCritical_1.1.0_Pink). 비워두면 아이콘 연출만 나옵니다.")]
    public GameObject penaltyVfxPrefab;

    [Tooltip("아이콘 연출이 시작된 뒤 이 시간(초) 후에 이펙트가 터집니다. 기본은 위 Penalty Popup의 Appear Duration(떠오르는 시간)과 같은 0.25초입니다.")]
    [Min(0f)] public float vfxDelay = 0.25f;

    [Tooltip("플레이어 머리 꼭대기에서 이펙트를 옮기는 값입니다(월드 기준, m). 아이콘 위치에 맞게 Y를 조절하세요.")]
    public Vector3 vfxOffsetAboveHead = new Vector3(0f, 0.6f, 0f);

    [Tooltip("이펙트 크기 배율입니다(파티클 크기/속도에 곱함).")]
    [Min(0.01f)] public float vfxScale = 1f;

    [Tooltip("켜면 이펙트가 플레이어를 따라다닙니다(아이콘처럼). 끄면 터진 자리에 남습니다.")]
    public bool vfxFollowPlayer = true;

    [Tooltip("생성한 이펙트를 이 시간(초) 뒤에 지웁니다.")]
    [Min(0.1f)] public float vfxLifetime = 2f;

    // ---- 실행 중 상태(저장되지 않음) ----
    [NonSerialized] private TrinketEffectContext ctx;
    [NonSerialized] private string modifierSourceId;
    [NonSerialized] private bool penalized;
    [NonSerialized] private RoomInstance trackedRoom;
    [NonSerialized] private int roomEnteredFrame = -1;
    [NonSerialized] private Sprite itemIcon;
    [NonSerialized] private Action<float> damageHandler;
    [NonSerialized] private Action<RoomInstance> roomEnteredHandler;

    public void Apply(TrinketEffectContext context, string sourceItemId)
    {
        if (context == null || context.Stats == null)
        {
            Debug.LogWarning("GlassCannonTrinketEffect: 플레이어 스탯을 찾을 수 없어 103번 효과를 적용하지 못했습니다.");
            return;
        }

        Unsubscribe(); // 혹시 Remove 없이 다시 불려도 중복 구독되지 않도록

        ctx = context;
        modifierSourceId = sourceItemId + "_glass_cannon";
        penalized = false;
        trackedRoom = FindCurrentRoom();
        roomEnteredFrame = -1;
        itemIcon = FindItemIcon(context, sourceItemId);

        SetMultiplier(attackBonusMultiplier);

        damageHandler = HandleDamageTaken;
        roomEnteredHandler = HandleRoomEntered;
        if (context.Health != null) context.Health.OnDamageTaken += damageHandler;
        RoomController.OnAnyRoomEntered += roomEnteredHandler;
    }

    public void Remove(TrinketEffectContext context, string sourceItemId)
    {
        Unsubscribe();

        var stats = context?.Stats ?? ctx?.Stats;
        string key = modifierSourceId ?? (sourceItemId + "_glass_cannon");
        if (stats != null) stats.RemoveModifiersFromSource(key);

        ctx = null;
        penalized = false;
        trackedRoom = null;
    }

    private void Unsubscribe()
    {
        if (ctx?.Health != null && damageHandler != null) ctx.Health.OnDamageTaken -= damageHandler;
        if (roomEnteredHandler != null) RoomController.OnAnyRoomEntered -= roomEnteredHandler;
        damageHandler = null;
        roomEnteredHandler = null;
    }

    // 이 아이템이 걸어 둔 Damage 곱하기 수정치를 지우고 multiplier 하나로 다시 겁니다.
    private void SetMultiplier(float multiplier)
    {
        if (ctx?.Stats == null || string.IsNullOrEmpty(modifierSourceId)) return;
        ctx.Stats.RemoveModifiersFromSource(modifierSourceId);
        ctx.Stats.AddModifier(StatType.Damage, ModifierType.Multiplicative, multiplier, modifierSourceId);
    }

    private void HandleRoomEntered(RoomInstance room)
    {
        trackedRoom = room;
        roomEnteredFrame = Time.frameCount;

        if (penalized)
        {
            penalized = false;
            SetMultiplier(attackBonusMultiplier);
        }
    }

    private void HandleDamageTaken(float amount)
    {
        if (penalized || ctx == null) return;
        if (ctx.Health != null && ctx.Health.IsDead) return;

        // 방에 들어가는 중에 받은 피해(특수방 출입 피해)는 세지 않습니다.
        if (Time.frameCount == roomEnteredFrame) return;
        if (!ReferenceEquals(FindCurrentRoom(), trackedRoom)) return;

        penalized = true;
        SetMultiplier(attackPenaltyMultiplier);
        PlayPenaltyEffect();
    }

    // 지금 플레이어가 있는 방입니다(LevelBuilder가 없거나 모르면 null - 테스트 씬에서는 항상 null이라 모든 피해를 셉니다).
    private static RoomInstance FindCurrentRoom()
    {
        var levelBuilder = UnityEngine.Object.FindFirstObjectByType<LevelBuilder>();
        if (levelBuilder == null || !levelBuilder.HasPlayerCell) return null;

        Vector2Int cell = levelBuilder.CurrentPlayerCell;
        foreach (var r in levelBuilder.GeneratedRooms)
        {
            if (r != null && r.occupiedCells != null && r.occupiedCells.Contains(cell)) return r;
        }
        return null;
    }

    // 장착한 103번의 아이콘을 찾아 둡니다(장신구 칸에 이미 들어간 뒤 Apply가 불림).
    private static Sprite FindItemIcon(TrinketEffectContext context, string sourceItemId)
    {
        var trinket = context.PlayerObject != null ? context.PlayerObject.GetComponent<PlayerTrinket>() : null;
        if (trinket == null) return null;
        foreach (var item in trinket.EquippedItems)
        {
            if (item != null && item.itemId == sourceItemId) return item.icon;
        }
        return null;
    }

    // ------------------------------------------------------------------
    // 감소 연출 (순수 장식 - 문제가 생겨도 공격력 처리에는 영향 없음)
    // ------------------------------------------------------------------

    private void PlayPenaltyEffect()
    {
        try
        {
            GameObject player = ctx?.PlayerObject;
            if (player == null) return;

            Transform playerT = player.transform;
            GameObject prefab = penaltyVfxPrefab;
            float delay = vfxDelay, scale = vfxScale, lifetime = vfxLifetime;
            Vector3 offset = vfxOffsetAboveHead;
            bool follow = vfxFollowPlayer;

            // 줄을 서면 아이콘이 늦게 뜨므로, 이펙트는 아이콘이 실제로 떠오르는 순간에 시작합니다.
            CreditPopupDisplay.Show(player, player.transform.position, penaltyPopup, itemIcon, followTarget: true,
                onStarted: prefab != null
                    ? () => GlassCannonPenaltyVfxRunner.Spawn(playerT, prefab, delay, offset, scale, follow, lifetime)
                    : (Action)null);
        }
        catch (Exception e)
        {
            Debug.LogError($"GlassCannonTrinketEffect: 감소 연출 중 오류가 발생해 건너뜁니다(공격력 처리에는 영향 없음): {e}");
        }
    }
}

// 103번 감소 연출의 이펙트를 잠깐 기다렸다가 플레이어 머리 위에 터뜨리는 일회용 도우미입니다(코드가 자동으로 만들고 지움).
// 106/111/39번 실패 이펙트도 같이 씁니다. 다른 방에 들어가면 아직 안 터진 이펙트는 취소하고, 터진 이펙트도 바로 지웁니다
// (머리 위 알림을 방 이동 시 건너뛰는 것과 같은 규칙).
public class GlassCannonPenaltyVfxRunner : MonoBehaviour
{
    private GameObject spawnedVfx;
    private int beganFrame;

    // 실행기를 하나 만들어 바로 시작합니다(prefab이 비어 있으면 아무것도 하지 않음).
    public static void Spawn(Transform player, GameObject prefab, float delay, Vector3 offsetAboveHead, float scale, bool follow, float lifetime)
    {
        if (player == null || prefab == null) return;
        var runnerGo = new GameObject("HeadAlertVfx");
        var runner = runnerGo.AddComponent<GlassCannonPenaltyVfxRunner>();
        runner.Begin(player, prefab, delay, offsetAboveHead, scale, follow, lifetime);
    }

    public void Begin(Transform player, GameObject prefab, float delay, Vector3 offsetAboveHead, float scale, bool follow, float lifetime)
    {
        beganFrame = Time.frameCount;
        StartCoroutine(Run(player, prefab, delay, offsetAboveHead, scale, follow, lifetime));
    }

    void OnEnable()
    {
        RoomController.OnAnyRoomEntered += HandleRoomEntered;
    }

    void OnDisable()
    {
        RoomController.OnAnyRoomEntered -= HandleRoomEntered;
    }

    private void HandleRoomEntered(RoomInstance room)
    {
        if (Time.frameCount == beganFrame) return; // 방에 들어온 바로 그 프레임에 시작한 이펙트는 남김
        if (spawnedVfx != null) Destroy(spawnedVfx);
        Destroy(gameObject);
    }

    private IEnumerator Run(Transform player, GameObject prefab, float delay, Vector3 offsetAboveHead, float scale, bool follow, float lifetime)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);

        float life = lifetime > 0f ? lifetime : 2f;
        try
        {
            if (player != null && prefab != null)
            {
                Vector3 position = new Vector3(player.position.x, MeasureHeadTop(player), player.position.z) + offsetAboveHead;
                spawnedVfx = Instantiate(prefab, position, Quaternion.identity);
                if (follow) spawnedVfx.transform.SetParent(player, true);
                ApplyVfxScale(spawnedVfx, scale > 0f ? scale : 1f);
                Destroy(spawnedVfx, Mathf.Max(life, 0.1f));
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"GlassCannonPenaltyVfxRunner: 이펙트 생성 중 오류가 발생해 건너뜁니다: {e}");
        }

        // 이펙트가 사는 동안 남아 있다가(방 이동 시 지우기 위해) 같이 사라집니다.
        if (spawnedVfx != null) yield return new WaitForSeconds(Mathf.Max(life, 0.1f));
        Destroy(gameObject);
    }

    // 플레이어 머리 꼭대기 높이(월드 Y)입니다. 트리거가 아닌 콜라이더 맨 위, 없으면 렌더러 맨 위, 둘 다 없으면 발밑 + 1.8m.
    private static float MeasureHeadTop(Transform player)
    {
        bool found = false;
        float top = float.MinValue;
        foreach (var col in player.GetComponentsInChildren<Collider>())
        {
            if (col == null || col.isTrigger || !col.enabled) continue;
            top = Mathf.Max(top, col.bounds.max.y);
            found = true;
        }
        if (!found)
        {
            foreach (var r in player.GetComponentsInChildren<Renderer>())
            {
                if (r == null || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                top = Mathf.Max(top, r.bounds.max.y);
                found = true;
            }
        }
        return found ? top : player.position.y + 1.8f;
    }

    // 파티클 시작 크기/속도에 직접 곱합니다(98번 처치 이펙트와 같은 방식).
    private static void ApplyVfxScale(GameObject instance, float multiplier)
    {
        if (instance == null || Mathf.Approximately(multiplier, 1f)) return;

        var systems = instance.GetComponentsInChildren<ParticleSystem>(true);
        if (systems.Length == 0)
        {
            instance.transform.localScale *= multiplier;
            return;
        }

        foreach (var ps in systems)
        {
            var main = ps.main;
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
