using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// [87번 - 포섭된 아군]
// 87번 액티브로 포섭한 적에게 붙는 컴포넌트입니다(Convert()가 자동으로 붙임 - 프리팹에 미리 붙일 필요 없음).
// "이 적은 아군인가"를 다른 스크립트가 물어보는 창구(IsAlly)이기도 합니다.
//
// [포섭 순간 - 형빈 확정]
// - 방의 "적 전멸" 클리어 목록에서 빠집니다. 남은 적이 없으면 그 방은 바로 클리어됩니다(EnemyKillObjective.TryExclude).
// - 걸려 있던 상태(매료/오한/해킹·기절 제어/도트/113번 중첩/이동·공격 속도 보정/상태 이펙트)를 모두 지우고 원래 색으로 되돌립니다.
// - 피해가 플레이어 기준으로 환산됩니다: 모든 피해(접촉/돌진/사격/전방위) x damageScale(기본 30). 0.5 → 15, 2 → 60.
// - [플레이어 공격력 연동] 환산된 피해 각각을 기본값으로 삼아 플레이어의 공격력 아이템 효과를 똑같이 적용합니다:
//   (환산 피해 + 공격력 덧셈 합) x 공격력 곱셈 곱. 예) 사격 15 → 공격력 +10이면 25, x1.2면 18. 103번/104번처럼 잠깐씩 바뀌는
//   공격력 효과도 따라갑니다. 하한선은 플레이어 설정과 상관없이 damageFloor(고정 1)입니다. 매 프레임 다시 계산합니다.
// - 체력이 플레이어 기준으로 환산되고 가득 찹니다: healthPerHalfHeart(기본 15)마다 0.5, 나머지는 0.5로 올림.
//   60 → 2, 80 → 3, 130 → 4.5(닳아 있던 적도 가득 참).
// - 태그가 "Enemy"에서 "Untagged"로 바뀝니다("Enemy" 태그로 대상을 찾는 플레이어 총알/수류탄/4번 폭발 등이 자동으로 건너뜀).
// - 몸통 테두리가 하늘색으로 살짝 빛나고(AllyOutline 셰이더), 체력바가 초록색이 됩니다(EnemyHealthBarDisplay).
//   (몸통 오른쪽 위 87번 아이콘은 테두리/초록 체력바로 충분해서 뺐습니다 - 형빈 확정)
//
// [아군이 피해를 받을 때]
// 피해를 받으면 invincibilityDuration(기본 1초) 동안 무적이고, 그 동안 플레이어처럼 깜빡입니다(렌더러를 껐다 켬).
//
// [아군이 죽을 때]
// 적 처치로 치지 않습니다(처치 발동 아이템/98번/처치 폭발 이펙트 없음 - EnemyHealth.Die). 하늘색 테두리를 떼고 그냥 사라집니다.
//
// [방 이동 / 층 이동 - 3단계, AllyRoomTransfer.cs]
// 포섭하는 순간 방 오브젝트 밑에서 빠져나와 층 전체 묶음(LevelRoot) 밑의 아군 묶음으로 옮겨집니다. 그래서 플레이어가 다른 방으로
// 가면 플레이어 곁(적과 겹치지 않는 곳)으로 순간이동하고, 다음 층으로 넘어가 LevelRoot가 지워질 때 함께 사라집니다.
//
// [아군 탄환]
// 아군이 쏜 탄환(사격/전방위)은 projectileColor(기본 하늘색)로 바뀝니다(꼬리도 같은 색).
public class AllyFaction : MonoBehaviour
{
    // 87번 Item Definition 인스펙터에서 조절하는 아군 설정입니다(포섭할 때 그 아군에게 넘겨줌).
    [System.Serializable]
    public class Settings
    {
        [Header("수치 환산 (플레이어 기준)")]
        [Tooltip("아군이 주는 모든 피해(접촉/돌진/사격/전방위)에 곱하는 값입니다(기본 30: 0.5 → 15, 1 → 30, 2 → 60).")]
        [Min(0f)] public float damageScale = 30f;

        [Tooltip("원래 체력 이만큼이 아군 체력 0.5가 됩니다(기본 15). 나머지는 0.5로 올림합니다(60 → 2, 80 → 3, 130 → 4.5).")]
        [Min(0.01f)] public float healthPerHalfHeart = 15f;

        [Header("플레이어 공격력 연동")]
        [Tooltip("아군 피해의 최소값입니다(기본 1). 공격력 감소 아이템을 먹어도 아군 피해(접촉/돌진/사격/전방위 각각)는 이 값 밑으로 내려가지 않습니다.")]
        [Min(0f)] public float damageFloor = 1f;

        [Header("피격 무적")]
        [Tooltip("아군이 피해를 받은 뒤 무적인 시간(초)입니다(플레이어처럼 깜빡임).")]
        [Min(0f)] public float invincibilityDuration = 1f;

        [Tooltip("무적 중 깜빡이는 간격(초)입니다(켜짐/꺼짐이 이 간격으로 바뀜).")]
        [Min(0.02f)] public float blinkInterval = 0.08f;

        [Header("하늘색 테두리 (살짝 발광)")]
        [Tooltip("AllyOutline 셰이더로 만든 재질입니다(Assets/Shaders/AllyOutline.shader). 비워두면 실행 중에 셰이더를 찾아 만들지만, " +
                 "빌드에서는 빠질 수 있으니 꼭 넣어 주세요. 색/두께는 아래 값이 덮어씁니다.")]
        public Material outlineMaterial;

        [Tooltip("테두리 색입니다(밝기 1을 조금 넘기면 은은하게 빛남).")]
        [ColorUsage(true, true)] public Color outlineColor = new Color(0.45f, 0.9f, 1.25f, 1f);

        [Tooltip("테두리 두께(1080p 기준 대략 픽셀)입니다.")]
        [Range(0f, 10f)] public float outlineWidth = 2.5f;

        [Header("싸울 적이 없을 때 플레이어 따라다니기")]
        [Tooltip("플레이어와 이 거리(m)까지 다가가면 멈춥니다.")]
        [Min(0.5f)] public float followDistance = 2f;

        [Tooltip("멈춘 뒤 플레이어가 followDistance + 이 값(m)보다 멀어지면 다시 따라갑니다(멈췄다 움직였다 떨리는 것 방지).")]
        [Min(0f)] public float followResumeMargin = 0.75f;

        [Header("방 이동 시 플레이어 곁으로 순간이동")]
        [Tooltip("플레이어에게서 최소 이 거리(m) 떨어진 곳에 나타납니다.")]
        [Min(0f)] public float teleportMinDistance = 1.2f;

        [Tooltip("플레이어에게서 최대 이 거리(m) 안에 나타납니다.")]
        [Min(0.5f)] public float teleportMaxDistance = 2.5f;

        [Tooltip("적과 최소 이 거리(m)는 떨어진 곳에 나타납니다. 그런 자리를 못 찾으면 이 조건만 빼고 다시 찾습니다.")]
        [Min(0f)] public float enemyClearance = 2.5f;

        [Header("아군 탄환")]
        [Tooltip("아군이 쏜 탄환(사격/전방위)의 색입니다. 탄환 꼬리도 이 색을 따라갑니다.")]
        public Color projectileColor = new Color(0.35f, 0.85f, 1f, 1f);
    }

    private static readonly List<AllyFaction> all = new List<AllyFaction>();
    public static IReadOnlyList<AllyFaction> All => all;

    private Settings settings;
    private EnemyHealth health;
    private float invincibleTimer;
    private float blinkTimer;
    private bool blinkVisible = true;
    private readonly List<Renderer> blinkRenderers = new List<Renderer>();
    private readonly List<Renderer> outlinedRenderers = new List<Renderer>();
    private Material outlineInstance;

    // [공격력 연동] 환산된(x30) 기본 피해들입니다. 매 프레임 여기에 플레이어 공격력 효과를 적용해 실제 값에 씁니다.
    private EnemyStats allyStats;
    private float baseAttackPower;
    private float baseRangedDamage;
    private readonly List<KeyValuePair<EnemyMeleeAttack, float>> baseMeleeDash = new List<KeyValuePair<EnemyMeleeAttack, float>>();
    private readonly List<KeyValuePair<EnemyChainDashAttack, float>> baseChainDash = new List<KeyValuePair<EnemyChainDashAttack, float>>();
    private readonly List<KeyValuePair<EnemyRadialAttack, float>> baseRadial = new List<KeyValuePair<EnemyRadialAttack, float>>();
    private static PlayerStats cachedPlayerStats;

    public Settings ActiveSettings => settings;
    public EnemyHealth Health => health;
    public bool IsInvincible => invincibleTimer > 0f;

    // ---------------- 판정 창구 ----------------

    // 탄환 색: go가 아군이면 아군 탄환 색, 아니면 fallback(원래 색)입니다. EnemyRangedAttack/EnemyRadialAttack이 탄환을 만들 때 씁니다.
    public static Color ProjectileColorFor(GameObject go, Color fallback)
    {
        var ally = go != null ? go.GetComponentInParent<AllyFaction>() : null;
        if (ally == null) return fallback;
        return ally.settings != null ? ally.settings.projectileColor : new Settings().projectileColor;
    }

    // 이 오브젝트(또는 그 부모)가 아군인지입니다(자식 콜라이더로 물어봐도 됨).
    public static bool IsAlly(GameObject go)
    {
        return go != null && go.GetComponentInParent<AllyFaction>() != null;
    }

    public static bool IsAlly(Component c)
    {
        return c != null && IsAlly(c.gameObject);
    }

    // 87번으로 포섭할 수 있는 적인지입니다: 살아 있고, 켜져 있고(지금 방), 보스가 아니고, 이미 아군이 아님.
    public static bool IsConvertible(EnemyHealth enemy)
    {
        if (enemy == null || enemy.IsDead || !enemy.isActiveAndEnabled) return false;
        if (enemy.GetComponent<BossAIController>() != null) return false;
        if (IsAlly(enemy.gameObject)) return false;
        return true;
    }

    // 포섭할 수 있는 적 목록을 채웁니다(지금 켜져 있는 방의 적 = 플레이어가 있는 방).
    public static void CollectConvertible(List<EnemyHealth> result)
    {
        result.Clear();
        var enemies = Object.FindObjectsByType<EnemyHealth>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var e in enemies)
        {
            if (IsConvertible(e)) result.Add(e);
        }
    }

    // ---------------- 포섭 ----------------

    public static AllyFaction Convert(EnemyHealth enemy, Settings settings)
    {
        if (!IsConvertible(enemy)) return null;
        if (settings == null) settings = new Settings();

        var ally = enemy.gameObject.AddComponent<AllyFaction>();
        ally.Setup(enemy, settings);
        return ally;
    }

    private void Setup(EnemyHealth enemy, Settings s)
    {
        settings = s;
        health = enemy;

        // 1. 방 클리어 목록에서 빠짐(남은 적이 없으면 그 방은 바로 클리어 - 상자는 이 적 위치 기준)
        EnemyKillObjective.TryExclude(enemy, transform.position);

        // 2. 걸려 있던 상태 지우기
        ClearStatuses();

        // 2-1. 진행 중이던 공격(돌진 준비/돌진/전방위/연사) 즉시 중단 - 38번 매료와 같은 방식
        foreach (var c in GetComponentsInChildren<ICancellableAttack>(true)) c.CancelAttack();
        foreach (var r in GetComponentsInChildren<EnemyRangedAttack>(true)) r.CancelBurst();

        // 3. 피해/체력 환산
        ScaleDamage(s.damageScale);
        var stats = GetComponent<EnemyStats>();
        if (stats != null)
        {
            stats.maxHealth = ConvertHealth(stats.maxHealth, s.healthPerHalfHeart);
        }
        enemy.RestoreFullHealth();

        // 4. "Enemy" 태그 떼기(자식 포함)
        foreach (var t in GetComponentsInChildren<Transform>(true))
        {
            if (t.CompareTag("Enemy")) t.gameObject.tag = "Untagged";
        }

        // 5. 표시(테두리/깜빡임 대상)
        CollectBlinkRenderers();
        ApplyOutline();

        all.Add(this);

        // 6. 방 오브젝트 밑에서 빼서 층 전체의 아군 묶음으로 옮김(방을 옮겨도 따라오고, 다음 층에서 함께 사라짐)
        AllyRoomTransfer.Adopt(this);
    }

    // 원래 체력을 아군 체력으로 바꿉니다: healthPerHalfHeart마다 0.5, 나머지는 0.5로 올림(최소 0.5).
    public static float ConvertHealth(float originalHealth, float healthPerHalfHeart)
    {
        float per = Mathf.Max(healthPerHalfHeart, 0.01f);
        int halves = Mathf.CeilToInt(originalHealth / per - 0.0001f);
        return Mathf.Max(1, halves) * 0.5f;
    }

    // 모든 피해 값에 scale을 곱한 값을 기본 피해로 기억하고(접촉 = attackPower, 사격 = rangedDamage, 돌진 = dashDamage, 전방위 = damage),
    // 곧바로 플레이어 공격력 효과를 적용해 씁니다.
    private void ScaleDamage(float scale)
    {
        allyStats = GetComponent<EnemyStats>();
        if (allyStats != null)
        {
            baseAttackPower = allyStats.attackPower * scale;
            baseRangedDamage = allyStats.rangedDamage * scale;
        }
        baseMeleeDash.Clear();
        baseChainDash.Clear();
        baseRadial.Clear();
        foreach (var m in GetComponentsInChildren<EnemyMeleeAttack>(true)) baseMeleeDash.Add(new KeyValuePair<EnemyMeleeAttack, float>(m, m.dashDamage * scale));
        foreach (var c in GetComponentsInChildren<EnemyChainDashAttack>(true)) baseChainDash.Add(new KeyValuePair<EnemyChainDashAttack, float>(c, c.dashDamage * scale));
        foreach (var r in GetComponentsInChildren<EnemyRadialAttack>(true)) baseRadial.Add(new KeyValuePair<EnemyRadialAttack, float>(r, r.damage * scale));

        RefreshDamage();
    }

    // [공격력 연동] 기본 피해 각각에 플레이어 공격력 효과를 적용하고 하한선(damageFloor)을 걸어 실제 값에 씁니다.
    private void RefreshDamage()
    {
        var ps = ResolvePlayerStats();
        float floor = settings != null ? settings.damageFloor : 1f;

        if (allyStats != null)
        {
            allyStats.attackPower = WithPlayerDamage(ps, baseAttackPower, floor);
            allyStats.rangedDamage = WithPlayerDamage(ps, baseRangedDamage, floor);
        }
        foreach (var p in baseMeleeDash) if (p.Key != null) p.Key.dashDamage = WithPlayerDamage(ps, p.Value, floor);
        foreach (var p in baseChainDash) if (p.Key != null) p.Key.dashDamage = WithPlayerDamage(ps, p.Value, floor);
        foreach (var p in baseRadial) if (p.Key != null) p.Key.damage = WithPlayerDamage(ps, p.Value, floor);
    }

    private static float WithPlayerDamage(PlayerStats ps, float baseDamage, float floor)
    {
        float value = ps != null ? ps.ApplyModifiersTo(StatType.Damage, baseDamage) : baseDamage;
        return Mathf.Max(value, floor);
    }

    private static PlayerStats ResolvePlayerStats()
    {
        if (cachedPlayerStats != null) return cachedPlayerStats;
        // 태그로 찾으면 "Player" 태그가 붙은 자식이 잡힐 수 있어 루트를 직접 찾음(PlayerStats는 PlayerHealth와 같은 루트에 있음).
        var health = Object.FindFirstObjectByType<PlayerHealth>();
        if (health != null) cachedPlayerStats = health.GetComponent<PlayerStats>();
        return cachedPlayerStats;
    }

    // 매료/오한/제어/도트/113번 중첩/속도 보정/상태 이펙트를 모두 지우고 색과 이동 속도를 원래대로 되돌립니다.
    private void ClearStatuses()
    {
        DisableAndDestroy(GetComponents<EnemyCharmState>());
        DisableAndDestroy(GetComponents<EnemyChillState>());
        DisableAndDestroy(GetComponents<EnemyControlState>());
        DisableAndDestroy(GetComponents<EnemyDotState>());
        DisableAndDestroy(GetComponents<EnemySugarStack>());
        DisableAndDestroy(GetComponents<EnemySpeedModifier>());
        DisableAndDestroy(GetComponents<EnemyAttackSpeedModifier>());
        DisableAndDestroy(GetComponentsInChildren<EnemyStatusVfx>(true));

        var stats = GetComponent<EnemyStats>();
        var agent = GetComponent<NavMeshAgent>();
        if (stats != null && agent != null) agent.speed = stats.moveSpeed;

        // 원래 색/발광으로 되돌림(EnemyHealth가 스폰 때 기억해 둔 값)
        if (health != null && health.TargetRenderer != null)
        {
            var mat = health.TargetRenderer.material;
            if (health.UsesBaseColor) mat.SetColor("_BaseColor", health.OriginalColor);
            if (health.UsesColor) mat.SetColor("_Color", health.OriginalColor);
            if (health.UsesEmission) mat.SetColor("_EmissionColor", health.OriginalEmission);
        }
    }

    private static void DisableAndDestroy<T>(T[] components) where T : Behaviour
    {
        if (components == null) return;
        foreach (var c in components)
        {
            if (c == null) continue;
            c.enabled = false; // OnDisable 정리를 즉시 실행(이번 프레임의 LateUpdate 색 덮어쓰기도 막음)
            Destroy(c);
        }
    }

    // ---------------- 피격 무적 / 깜빡임 ----------------

    // EnemyHealth가 아군이 피해를 받은 직후 부릅니다.
    public void BeginInvincibility()
    {
        if (settings == null || settings.invincibilityDuration <= 0f) return;
        invincibleTimer = settings.invincibilityDuration;
        blinkTimer = 0f;
        SetBlinkVisible(false);
    }

    void Update()
    {
        if (health != null && !health.IsDead) RefreshDamage();

        if (invincibleTimer <= 0f) return;

        invincibleTimer -= Time.deltaTime;
        if (invincibleTimer <= 0f || (health != null && health.IsDead))
        {
            invincibleTimer = 0f;
            SetBlinkVisible(true);
            return;
        }

        blinkTimer += Time.deltaTime;
        float interval = settings != null ? Mathf.Max(settings.blinkInterval, 0.02f) : 0.08f;
        if (blinkTimer >= interval)
        {
            blinkTimer -= interval;
            SetBlinkVisible(!blinkVisible);
        }
    }

    private void CollectBlinkRenderers()
    {
        blinkRenderers.Clear();
        foreach (var r in GetComponentsInChildren<Renderer>(false))
        {
            if (r == null || !r.enabled) continue;
            if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
            blinkRenderers.Add(r);
        }
    }

    private void SetBlinkVisible(bool visible)
    {
        blinkVisible = visible;
        foreach (var r in blinkRenderers)
        {
            if (r != null) r.enabled = visible;
        }
    }

    // ---------------- 하늘색 테두리 ----------------

    private void ApplyOutline()
    {
        if (settings == null) return;

        Material source = settings.outlineMaterial;
        if (source == null)
        {
            var shader = Shader.Find("MobArchive/AllyOutline");
            if (shader == null)
            {
                Debug.LogWarning("AllyFaction: AllyOutline 셰이더를 찾지 못해 하늘색 테두리 없이 포섭합니다. 87번의 Outline Material 칸을 채워 주세요.");
                return;
            }
            source = new Material(shader);
        }

        outlineInstance = new Material(source) { name = "AllyOutline (Instance)" };
        if (outlineInstance.HasProperty("_OutlineColor")) outlineInstance.SetColor("_OutlineColor", settings.outlineColor);
        if (outlineInstance.HasProperty("_OutlineWidth")) outlineInstance.SetFloat("_OutlineWidth", settings.outlineWidth);

        foreach (var r in blinkRenderers)
        {
            if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;
            var mats = r.materials; // 이미 인스턴스면 같은 인스턴스를 돌려줌(EnemyHealth가 쓰는 0번 재질 유지)
            var extended = new Material[mats.Length + 1];
            for (int i = 0; i < mats.Length; i++) extended[i] = mats[i];
            extended[mats.Length] = outlineInstance;
            r.materials = extended;
            outlinedRenderers.Add(r);
        }
    }

    // 하늘색 테두리를 뗍니다(아군이 죽어 사라질 때 - 테두리만 남아 보이지 않도록).
    public void RemoveOutline()
    {
        if (outlineInstance == null) return;
        foreach (var r in outlinedRenderers)
        {
            if (r == null) continue;
            var mats = r.materials;
            var kept = new List<Material>(mats.Length);
            foreach (var m in mats)
            {
                if (m != null && m.name.StartsWith("AllyOutline")) continue;
                kept.Add(m);
            }
            r.materials = kept.ToArray();
        }
        outlinedRenderers.Clear();
    }

    // 방을 옮기기 직전 AllyRoomTransfer가 부릅니다: 진행 중이던 공격을 멈추고, 무적 깜빡임을 끝내 보이는 상태로 둡니다.
    public void PrepareForRoomTransfer()
    {
        foreach (var c in GetComponentsInChildren<ICancellableAttack>(true)) c.CancelAttack();
        foreach (var r in GetComponentsInChildren<EnemyRangedAttack>(true)) r.CancelBurst();
        invincibleTimer = 0f;
        SetBlinkVisible(true);
    }

    // 죽는 순간 EnemyHealth가 부릅니다: 깜빡임을 멈추고(보이게) 테두리를 뗍니다.
    public void PrepareForDeath()
    {
        invincibleTimer = 0f;
        SetBlinkVisible(true);
        RemoveOutline();
    }

    void OnDestroy()
    {
        all.Remove(this);
        if (outlineInstance != null) Destroy(outlineInstance);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlayStart()
    {
        all.Clear();
        cachedPlayerStats = null;
    }
}
