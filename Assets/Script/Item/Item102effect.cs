using UnityEngine;

// 패시브 102번 - 혼자서는 효과가 없습니다. 109번과 함께 가지고 있으면 총알 적중 시 적을 "중독"시킵니다(시너지).
//
// [기획 - 형빈 확정]
// - 102번과 109번을 둘 다 가지고 있는 동안, 플레이어 총알이 적에게 명중하거나(5번 분열탄/99번 관통/69번 튕김 포함) 4번 폭발에 휘말린 적을
//   중독시킵니다. 94번 후광과 수류탄으로는 걸리지 않습니다.
// - 중독: 3초 동안 1초마다 "그 순간의 플레이어 공격력 스탯"(109번 +40 포함) x 20%의 피해. 23번/11번 같은 총알 보정은 따르지 않습니다.
// - 다시 맞으면 남은 시간만 3초로 되돌아갑니다(피해 중첩 X, 6초로 늘어나지도 않음). 1초 간격은 끊기지 않고 이어집니다(계속 맞혀도 1초마다 피해).
// - 보스에게도 걸립니다(제어가 아닌 순수 피해).
// - 중독 중에는 몸이 초록색이 되고(색 우선순위: 공격 충전 > 수면·해킹 > 매료 > 상태 색(오한/중독 - 나중에 걸린 것) > 원래 색),
//   피해가 들어갈 때마다 이펙트(VFX_Poison_02)가 몸통에 나타납니다. 실제 처리는 EnemyDotState.cs가 합니다.
// - 둘 중 하나라도 잃으면 그 뒤로는 중독을 걸지 않습니다(이미 걸린 중독은 남은 시간 동안 유지).
// - [총알 색 - 형빈 확정] 시너지가 켜져 있는 동안 플레이어 총알(과 꼬리)이 초록색(Poison Bullet Color)이 됩니다. 총알 색 등록소
//   (ProjectileColorOverride)에 우선순위 0으로 "시너지가 켜진 순간" 등록하므로, 같은 우선순위인 38번 보라색과는 나중에 생긴 쪽이 이깁니다
//   (102·109 → 38 순서면 보라, 102·38 → 109 순서면 초록). 23번 무지개색은 언제나 우선입니다. 94번 후광/110번 강화 총알 중심부처럼 총알 색을
//   따라가는 연출도 자동으로 초록이 됩니다. 시너지가 꺼지면 등록을 지웁니다.
//
// [아이템 에셋 설정]
// ItemDefinition: Item Id = 102, Category = Passive, Special Effects에 Item102effect 하나. Stat Modifiers는 비워둡니다.
// Poison Tick Vfx Prefab 칸에 Assets/VFX/VFXPACK_IMPACT_WALLCOEUR_FreeVersion/00_Prefab/0_Classic/VFX_Poison_02를 넣습니다.
[System.Serializable]
public class Item102effect : IPassiveItemEffect, IRemovablePassiveEffect
{
    private const string PoisonKey = "poison";

    [Tooltip("함께 가지고 있어야 하는 짝 아이템의 Item Id입니다(기본 109).")]
    public string partnerItemId = "109";

    [Header("중독")]
    [Tooltip("중독 지속시간(초)입니다. 다시 맞으면 남은 시간이 이 값으로 되돌아갑니다(늘어나지 않음).")]
    [Min(0.1f)] public float poisonDuration = 3f;

    [Tooltip("피해 간격(초)입니다.")]
    [Min(0.1f)] public float tickInterval = 1f;

    [Tooltip("한 번에 주는 피해 = 그 순간의 플레이어 공격력 x 이 비율입니다(기본 0.2 = 20%).")]
    [Min(0f)] public float damageRatioPerTick = 0.2f;

    [Tooltip("중독 중 적의 색입니다(초록). 발광 없이 몸 색만 바뀝니다.")]
    public Color poisonColor = new Color(0.30f, 0.80f, 0.25f, 1f);

    [Header("총알 색 (시너지가 켜져 있는 동안)")]
    [Tooltip("시너지가 켜져 있는 동안의 플레이어 총알 색입니다(초록).")]
    public Color poisonBulletColor = new Color(0.35f, 0.90f, 0.30f, 1f);

    [Tooltip("총알 색 우선순위입니다. 0이면 38번 등 다른 색 아이템(기본 0)과 '나중에 생긴 쪽'이 이깁니다. 23번은 항상 우선입니다.")]
    public int poisonBulletColorPriority = 0;

    [Header("피해 이펙트 (피해가 들어갈 때마다 몸통에 한 번씩)")]
    [Tooltip("중독 피해가 들어갈 때마다 몸통 중심에 생성할 이펙트입니다(VFX_Poison_02). 비워두면 이펙트 없이 피해/색만 적용됩니다.")]
    public GameObject poisonTickVfxPrefab;

    [Tooltip("이펙트 크기 배율입니다(1 = 프리팹 그대로).")]
    [Min(0.01f)] public float poisonTickVfxScale = 1f;

    [Tooltip("몸통 중심에서 이펙트를 옮기는 값입니다(월드 기준, m).")]
    public Vector3 poisonTickVfxOffset = Vector3.zero;

    [Tooltip("생성한 이펙트를 이 시간(초) 뒤에 지웁니다.")]
    [Min(0.1f)] public float poisonTickVfxLifetime = 2f;

    [System.NonSerialized] private SynergyWatcher watcher;
    [System.NonSerialized] private PlayerStats stats;
    [System.NonSerialized] private bool subscribed;
    [System.NonSerialized] private GameObject playerObject;
    [System.NonSerialized] private string colorSourceId;

    public void Apply(PassiveEffectContext context, string sourceItemId)
    {
        if (context == null || context.Stats == null || context.Inventory == null || string.IsNullOrEmpty(sourceItemId))
        {
            Debug.LogWarning("Item102effect: PlayerStats/PlayerInventory/아이템 ID를 찾을 수 없어 102번 효과를 적용하지 못했습니다.");
            return;
        }

        stats = context.Stats;
        playerObject = context.PlayerObject;
        colorSourceId = "synergy_poison_" + sourceItemId;
        if (watcher == null) watcher = new SynergyWatcher();
        watcher.Start(context.Inventory, new[] { sourceItemId, partnerItemId }, HandleSynergyChanged);
    }

    // [패시브 잃기 - 48번 등] 감시와 적중 구독을 멈춥니다. 이미 걸린 중독은 남은 시간 동안 유지됩니다.
    public void Remove(PassiveEffectContext context, string sourceItemId)
    {
        watcher?.Stop();
        Unsubscribe();
        ClearBulletColor();
    }

    private void HandleSynergyChanged(bool active)
    {
        if (active)
        {
            Subscribe();
            // 시너지가 켜진 "지금" 등록해서, 같은 우선순위의 다른 총알 색보다 나중 것으로 취급됩니다.
            if (!string.IsNullOrEmpty(colorSourceId))
            {
                ProjectileColorOverride.SetColor(colorSourceId, playerObject, poisonBulletColor, poisonBulletColorPriority);
            }
        }
        else
        {
            Unsubscribe();
            ClearBulletColor();
        }
    }

    private void ClearBulletColor()
    {
        if (!string.IsNullOrEmpty(colorSourceId)) ProjectileColorOverride.RemoveColor(colorSourceId);
    }

    private void Subscribe()
    {
        Unsubscribe();
        Projectile.OnPlayerBulletHitEnemy += OnEnemyHit;
        ExplodeOnImpactEffect.OnExplosionHitEnemy += OnEnemyHit;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        Projectile.OnPlayerBulletHitEnemy -= OnEnemyHit;
        ExplodeOnImpactEffect.OnExplosionHitEnemy -= OnEnemyHit;
        subscribed = false;
    }

    private void OnEnemyHit(EnemyHealth enemy)
    {
        if (!subscribed || enemy == null || enemy.IsDead) return;
        // 플레이어가 사라졌는데(이전 플레이 등) 구독이 남아 있으면 정리하고 아무것도 하지 않습니다.
        if (stats == null) { Unsubscribe(); return; }

        try
        {
            var dot = EnemyDotState.GetOrAdd(enemy.gameObject);
            if (dot == null) return;

            PlayerStats source = stats;
            float ratio = damageRatioPerTick;
            dot.Apply(PoisonKey, poisonDuration, tickInterval,
                () => source != null ? source.Damage * ratio : 0f,
                poisonColor, poisonTickVfxPrefab, poisonTickVfxScale, poisonTickVfxOffset, poisonTickVfxLifetime);
        }
        catch (System.Exception e)
        {
            // 명중 알림 안에서 실행되므로, 오류가 나도 총알/폭발 처리가 막히지 않게 이 적만 건너뜁니다.
            Debug.LogError($"Item102effect: 중독을 거는 중 오류가 발생해 이 적은 건너뜁니다: {e}");
        }
    }
}
