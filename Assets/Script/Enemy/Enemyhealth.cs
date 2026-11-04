using System;
using System.Collections.Generic;
using UnityEngine;

// 적 하나의 체력/피격/사망입니다. 최대 체력은 EnemyStats가 유일한 원본(Initialize에서 확정). 모든 피해는 TakeDamage 한 곳을 지납니다
// (받는 피해 배율 -> [2레벨 보스 보호막 흡수] -> 98번 섬멸 판정 -> 체력 감소 -> 알림 -> 피격 색 -> 사망). 사망은 isDead로 한 번만 처리됩니다.
// 주의: 이 컴포넌트를 지우면 안 됨(Projectile/RoomObjectives 등이 직접 의존). 처치 알림 구독자는 try/catch 필수(OnAnyEnemyKilled).
// 설계 배경(Initialize 타이밍 버그, isDead 가드, 처치 알림 3종, 98번)은 Docs/설계노트.md [적 체력].
public class EnemyHealth : MonoBehaviour
{
    private EnemyStats stats;
    private float currentHealth;
    private bool isDead = false;

    // [98번] 즉시 처치(섬멸)로 죽었는지, 그때의 설정입니다(위 [98번 패시브] 참고).
    private bool wasExecuted = false;
    private ExecuteOverride.Settings executeSettings;
    public bool WasExecuted => wasExecuted;
    public ExecuteOverride.Settings ExecuteSettings => executeSettings;

    [Header("피격 시 간단한 색상 피드백 (선택)")]
    public bool flashOnHit = true;
    public Color hitColor = Color.red;
    [Tooltip("피격 색상에서 원래 색으로 서서히 돌아오는 데 걸리는 시간 (초)")]
    public float flashFadeDuration = 0.4f;

    [Tooltip("자체발광도 함께 올림(흰색에 가까운 피격 색은 _BaseColor 곱만으로는 변화가 안 보여서).")]
    public bool flashUsesEmission = true;
    [Tooltip("피격 시 자체발광 세기.")]
    public float flashEmissionBoost = 2f;

    private Renderer rend;
    private Color originalColor;
    private bool rendSupportsColor;
    private bool usesBaseColor;
    private bool usesColor;
    private bool usesEmission;
    private Color originalEmission;
    private float fadeTimer = 0f; // 0이면 페이드 중이 아님
    private BossShield shield;    // [2레벨 보스] 있으면 피해를 먼저 흡수

    // 이 적이 죽었을 때(사망 위치) - 방 클리어 판정/보물상자 위치용. 적 한 마리당 정확히 한 번.
    public event Action<Vector3> OnDied;

    // 어떤 적이든 죽을 때(OnDied와 함께) - 아이템이 한 번만 구독해 전역으로 받음.
    public static event Action<Vector3> OnAnyEnemyDied;

    // [54번] 죽은 적 자체 + 위치(OnAnyEnemyDied 바로 뒤, 사라짐 연출 전이라 모양이 그대로).
    // 주의: Die() 안에서 바로 실행되므로 구독하는 쪽은 반드시 try/catch(예외가 나면 사라짐 연출/파괴가 막힘 - Item54effect 참고).
    public static event Action<EnemyHealth, Vector3> OnAnyEnemyKilled;

    // 어떤 적이든 체력에 피해를 받을 때(최종 피해량 전체, 남은 체력으로 자르지 않음) - 피해 숫자/적 체력바용 알림 전용.
    public static event Action<EnemyHealth, float> OnAnyEnemyDamaged;

    // [2레벨 보스] 보호막(BossShield)이 피해를 흡수했을 때(흡수한 최종 피해량, 남은 보호막으로 자르지 않음) - 주황 피해 숫자용 알림 전용.
    // 이때는 OnAnyEnemyDamaged가 울리지 않고 체력/피격 색도 그대로입니다(보호막이 흰색으로 깜빡임).
    public static event Action<EnemyHealth, float> OnAnyEnemyShieldDamaged;

    // 읽기 전용(값을 바꾸는 통로는 TakeDamage/Initialize뿐).
    public float CurrentHealth => currentHealth;
    public float MaxHealth => stats != null ? stats.maxHealth : 0f;

    // 주의: 죽은 뒤에도 사라짐 연출이 끝날 때까지 오브젝트가 살아 있어 AI가 계속 돕니다 - 각 AI는 이 값으로 스스로 손을 뗄 것.
    public bool IsDead => isDead;

    // 진짜 원래 색/발광(스폰 시점에 캐시). 주의: 다른 컴포넌트가 렌더러 색을 직접 다시 읽으면 피격 플래시 색을 원래 색으로 잘못 캐시함 - 이 값을 쓸 것.
    public Renderer TargetRenderer => rend;
    public bool RendSupportsColor => rendSupportsColor;
    public bool UsesBaseColor => usesBaseColor;
    public bool UsesColor => usesColor;
    public bool UsesEmission => usesEmission;
    public Color OriginalColor => originalColor;
    public Color OriginalEmission => originalEmission;

    // 주의: 스탯이 전부 확정된 뒤에 호출(RealEnemySpawner/보스 스폰) - Awake에서 정하면 기본값으로 굳는 버그가 있었음.
    public void Initialize(EnemyStats ownerStats)
    {
        stats = ownerStats;
        currentHealth = stats.maxHealth;
    }

    // [87번 포섭 - 2단계] 지금 켜져 있는(=플레이어가 있는 방의, 또는 따라다니는 아군) 적 목록입니다. 아군/적이 목표를 고를 때
    // 매 프레임 FindObjectsByType을 부르지 않도록 씁니다(CombatTargeting). 아군도 포함됩니다(AllyFaction으로 구분).
    private static readonly List<EnemyHealth> activeEnemies = new List<EnemyHealth>();
    public static IReadOnlyList<EnemyHealth> ActiveEnemies => activeEnemies;

    void OnEnable()
    {
        if (!activeEnemies.Contains(this)) activeEnemies.Add(this);
    }

    void OnDisable()
    {
        activeEnemies.Remove(this);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetActiveEnemies()
    {
        activeEnemies.Clear();
    }

    // [87번 포섭] 체력을 (아군 기준으로 환산된) 최대 체력까지 가득 채웁니다(닳아 있던 적을 포섭해도 가득 참 - 형빈 확정).
    public void RestoreFullHealth()
    {
        if (isDead) return;
        currentHealth = MaxHealth;
    }

    void Awake()
    {
        // 모델(Renderer)이 자식에 있는 구조가 많아 GetComponentInChildren.
        shield = GetComponent<BossShield>();
        rend = GetComponentInChildren<Renderer>();
        if (rend != null)
        {
            var mat = rend.material; // 최초 접근 시 자동으로 인스턴스화됨
            usesBaseColor = mat.HasProperty("_BaseColor");
            usesColor = mat.HasProperty("_Color");
            rendSupportsColor = usesBaseColor || usesColor;

            if (rendSupportsColor)
            {
                originalColor = usesBaseColor ? mat.GetColor("_BaseColor") : mat.GetColor("_Color");
            }
            else if (flashOnHit)
            {
                Debug.LogWarning($"EnemyHealth ({gameObject.name}): '{mat.shader.name}' 셰이더에 " +
                                  "_Color/_BaseColor 프로퍼티가 없어 피격 색상 피드백을 표시할 수 없습니다. " +
                                  "flashOnHit를 꺼서 이 경고를 없앨 수 있습니다.");
            }

            usesEmission = mat.HasProperty("_EmissionColor");
            if (usesEmission)
            {
                originalEmission = mat.GetColor("_EmissionColor");
                mat.EnableKeyword("_EMISSION");
            }
        }
    }

    // 지금 피격 페이드가 되돌아가야 할 목표 색/자체발광을 계산합니다. 돌진/전방위 공격/매료
    // 등 다른 컴포넌트가 지금 색을 오버라이드하고 있다면(IEnemyColorOverride.HasColorOverride)
    // 진짜 원래 색이 아니라 그 색으로 되돌아갑니다 - 충전 중이거나 매료된 채로 맞아도 그
    // 색을 유지하기 위함입니다. 매 프레임 다시 계산하므로, 페이드 도중 충전/매료가 끝나면
    // 자연스럽게 남은 페이드가 진짜 원래 색 쪽으로 방향을 틉니다.
    //
    // [38번 패시브 연동 - 매번 새로 조회] 예전엔 Awake()에서 GetComponents로 한 번만
    // 캐시해뒀었는데, EnemyCharmState처럼 이 오브젝트가 스폰된 뒤 한참 지나서야(총알에
    // 맞는 순간) AddComponent로 붙는 컴포넌트는 그 캐시에 잡히지 않는 문제가 있었습니다.
    // 이 메서드는 피격 페이드가 실제로 진행 중인 프레임에만 호출되므로, 매번 새로
    // GetComponents로 조회해도 비용은 무시할 만합니다.
    private void GetFadeTarget(out Color targetColor, out Color targetEmission)
    {
        var overrides = GetComponents<IEnemyColorOverride>();
        for (int i = 0; i < overrides.Length; i++)
        {
            var o = overrides[i];
            if (o != null && o.HasColorOverride)
            {
                targetColor = o.OverrideColor;
                targetEmission = o.OverrideEmission;
                return;
            }
        }

        targetColor = originalColor;
        targetEmission = originalEmission;
    }

    void Update()
    {
        // 피격 색상이 서서히 (진짜 원래 색 또는 지금 충전/돌진 중인 색으로) 돌아오는 처리
        if (fadeTimer > 0f && rend != null && rendSupportsColor)
        {
            fadeTimer -= Time.deltaTime;
            float t = 1f - Mathf.Clamp01(fadeTimer / flashFadeDuration); // 0(방금 피격) -> 1(목표색 도달)

            GetFadeTarget(out Color targetColor, out Color targetEmission);

            Color c = Color.Lerp(hitColor, targetColor, t);
            if (usesBaseColor) rend.material.SetColor("_BaseColor", c);
            if (usesColor) rend.material.SetColor("_Color", c);

            if (flashUsesEmission && usesEmission)
            {
                Color e = Color.Lerp(hitColor * flashEmissionBoost, targetEmission, t);
                rend.material.SetColor("_EmissionColor", e);
            }
        }
    }

    public void TakeDamage(float amount)
    {
        // 주의: 같은 프레임에 뒤늦게 들어온 피해(폭발 범위 등)로 Die가 두 번 불리면 방이 적이 남은 채 클리어됨 - 반드시 막을 것.
        if (isDead) return;

        // [2레벨 보스] 보호막 충전 중에는 무적입니다(숫자도 없음).
        if (shield != null && shield.IsInvulnerable) return;

        // [87번 아군] 피해를 받은 뒤 무적 시간(기본 1초) 동안은 피해를 받지 않습니다. 아군에게는 플레이어 쪽 효과(받는 피해 배율 /
        // 98번 섬멸)가 적용되지 않습니다.
        AllyFaction ally = GetComponent<AllyFaction>();
        if (ally != null && ally.IsInvincible) return;

        // [적이 받는 피해 배율] 등록된 "% 피해 증가" 효과(예: 41번 - 해킹된 적 x1.5)를 전부 곱해 최종 피해량을 만듭니다.
        // 등록된 것이 없으면 배율 1이라 원래와 같습니다. 팝업(OnAnyEnemyDamaged)도 이 최종값으로 표시됩니다.
        if (amount > 0f && ally == null) amount *= EnemyDamageTakenModifier.Resolve(this);

        // [2레벨 보스] 보호막이 있으면 보호막이 전부 받습니다(깨뜨리고 남은 피해도 체력으로 넘기지 않음 - 형빈 확정). 섬멸/체력/피격 색은 건너뜀.
        if (amount > 0f && shield != null && shield.IsActive)
        {
            shield.AbsorbDamage(amount);
            OnAnyEnemyShieldDamaged?.Invoke(this, amount);
            return;
        }

        // [98번 즉시 처치] 피해를 빼기 직전의 체력이 기준(25%) 이하이면 이 피해로 처치합니다(이번 피해로 기준 이하가 되는 경우는 X).
        if (amount > 0f && ally == null && !wasExecuted && ExecuteOverride.ShouldExecute(currentHealth, MaxHealth, out var execute))
        {
            wasExecuted = true;
            executeSettings = execute;
        }

        currentHealth -= amount;
        if (wasExecuted && currentHealth > 0f) currentHealth = 0f;

        // 사망 처리(Die)보다 먼저 알려서 마무리 일격의 피해량도 표시되게 합니다.
        if (amount > 0f) OnAnyEnemyDamaged?.Invoke(this, amount);

        if (flashOnHit && rend != null && rendSupportsColor)
        {
            if (usesBaseColor) rend.material.SetColor("_BaseColor", hitColor);
            if (usesColor) rend.material.SetColor("_Color", hitColor);

            if (flashUsesEmission && usesEmission)
            {
                rend.material.SetColor("_EmissionColor", hitColor * flashEmissionBoost);
            }

            fadeTimer = flashFadeDuration; // 이 순간부터 서서히 원래 색으로 돌아감
        }

        // [87번 아군] 맞고도 살아 있으면 무적 시간을 시작합니다(그 동안 깜빡임).
        if (ally != null && amount > 0f && currentHealth > 0f) ally.BeginInvincibility();

        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    void Die()
    {
        // TakeDamage에서 이미 한 번 걸러지지만, Die()가 혹시 다른 경로로 직접 호출되더라도
        // 안전하도록 여기서도 한 번 더 확인합니다.
        if (isDead) return;
        isDead = true;

        // Destroy()는 이번 프레임 끝에 실제로 처리되므로 transform 자체는 아직 유효하지만,
        // 구독자가 이 함수 실행 도중 다른 처리를 하다가 늦게 읽을 수도 있어 명시적으로
        // 지금 시점의 위치를 변수에 담아 이벤트와 함께 넘깁니다.
        Vector3 deathPosition = transform.position;
        OnDied?.Invoke(deathPosition); // 방 클리어 판정/보물상자 스폰 위치 계산 등 구독자에게 알림
        // [87번 아군] 아군의 죽음은 적 처치로 치지 않습니다(형빈 확정) - 전역 처치 알림(처치 발동 아이템/98번/처치 폭발 이펙트)을 보내지
        // 않고, 하늘색 테두리를 뗀 뒤 그냥 사라집니다.
        var deadAlly = GetComponent<AllyFaction>();
        if (deadAlly != null)
        {
            deadAlly.PrepareForDeath();
            DissolveAndVanish.Begin(gameObject, DissolveAndVanish.EndAction.Destroy);
            return;
        }

        OnAnyEnemyDied?.Invoke(deathPosition); // 게임 전체에서 적 처치에 반응하는 아이템용 전역 알림
        OnAnyEnemyKilled?.Invoke(this, deathPosition); // [54번 연동] 죽은 적 자체도 함께 알림(위 선언부 주석 참고)
        // 사라짐 연출: 판정(Collider)은 즉시 꺼지고, 시각적으로만 서서히 투명해지다가
        // 완전히 사라집니다 (DissolveAndVanish 참고).
        // [98번] 즉시 처치(섬멸)면 흰색 대신 설정의 색(기본 빨강)으로 물들며 사라집니다.
        if (wasExecuted && executeSettings != null)
        {
            DissolveAndVanish.Begin(gameObject, DissolveAndVanish.EndAction.Destroy, executeSettings.dissolveColor);
        }
        else
        {
            DissolveAndVanish.Begin(gameObject, DissolveAndVanish.EndAction.Destroy);
        }
    }
}
