using System;
using UnityEngine;

// 플레이어의 "현재" 체력, 피격, 사망을 처리하는 스크립트입니다.
// 최대 체력 값은 PlayerStats에서 가져오고, 이 스크립트는 그 안에서 움직이는
// 실시간 체력만 관리합니다.
//
// [MaxHealth 아이템과의 연동]
// PlayerStats.OnMaxHealthChanged를 구독해서, 아이템 등으로 최종 MaxHealth가 바뀔 때마다
// (증가든 감소든) currentHealth를 같은 폭만큼 함께 조정합니다. 예: 9/10 상태에서
// MaxHealth +1 아이템을 먹으면 10/11이 됩니다 (9/11이 아님).
//
// [다음 전투 레벨 진입 시 최대 체력 정리] ApplyLevelEndMaxHealthLoss - 채워지지 않은 최대 체력 칸을 1칸 단위로 지웁니다
// (PlayerStats.ApplyLevelEndMaxHealthLoss 참고). 이때는 최대 체력만 줄고 현재 체력은 그대로입니다.
//
// [보호막 - 체력 UI 개편 때 추가, 형빈 확정]
// - 보호막은 최대 maxShield(기본 5)개까지 쌓이고, 피해량과 상관없이 공격 1번을 통째로 막습니다(0.5 피해든 2 피해든 1개).
// - 보호막이 있으면 체력 대신 보호막이 먼저 깎입니다. 모든 피해가 지나는 TakeDamage에서 처리하므로 적 공격/체력 소모 특수방
//   출입/내 수류탄 자해(81번 불 구역 포함) 모두 똑같이 보호막이 막습니다.
// - 보호막으로 막으면 무적 시간은 평소처럼 주지만 "피격"으로 치지 않습니다: 체력 감소/OnDamageTaken(78번 등 피격 반응 아이템,
//   39번 무피해 조건)/피격 점멸(PlayerHitFlash)/피격 표정(SDFaceGameEvents)/피격 충격 이펙트(CombatVfxSpawner) 모두 없습니다.
//   점멸/표정은 IsInvincible 대신 IsHitInvincible(체력이 깎여서 생긴 무적만)을 보고, 충격 이펙트는 적 탄환/근접 판정이
//   WillShieldBlockNextHit로 미리 확인해 건너뜁니다. 대신 OnShieldBroken이 울리고, 보호막 파괴 연출(PlayerShieldBreakVfx - 깎은 정이십면체)이 이걸 씁니다.
// - 보호막은 레벨이 바뀌어도 유지됩니다(이 컴포넌트가 플레이어와 함께 다음 레벨로 넘어감).
[RequireComponent(typeof(PlayerStats))]
public class PlayerHealth : MonoBehaviour
{
    [Header("무적 시간 설정")]
    [Tooltip("피격 후 잠시 동안 추가 피해를 받지 않는 시간 (초)")]
    public float invincibilityDuration = 0.5f;

    [Header("보호막")]
    [Tooltip("보호막 최대 개수입니다(기본 5). 이보다 많이 얻으면 넘치는 만큼은 버려집니다.")]
    [Min(0)] public int maxShield = 5;
    [Tooltip("게임 시작 시 가지고 시작하는 보호막 개수입니다(기본 0). 테스트 씬에서는 PlayerTestProfile이 덮어쓸 수 있습니다.")]
    [Min(0)] public int startingShield = 0;

    private PlayerStats stats;
    private float currentHealth;
    private float invincibilityTimer;
    private int currentShield;
    // 지금 무적 시간이 "체력이 깎여서" 생긴 것인지(true) "보호막이 막아서" 생긴 것인지(false)입니다.
    private bool invincibilityFromHit;

    public float CurrentHealth => currentHealth;
    public bool IsDead { get; private set; } = false;

    // [v1.26 추가 - 장신구 등의 "받는 피해량 고정" 효과용]
    // null이면 평소처럼 TakeDamage(amount)에 들어온 amount 그대로 적용됩니다. 값이 있으면
    // 실제로 들어온 amount와 무관하게 항상 이 값만큼만 깎입니다(예: 21번 장신구 - 받는 모든
    // 피해량을 0.5로 고정). 장신구는 한 번에 하나만 장착할 수 있어 동시에 여러 소스가 이걸
    // 거는 상황이 아직 없으므로 단일 값으로 충분합니다 - 나중에 여러 소스가 동시에 걸 수
    // 있게 되면 리스트/우선순위 규칙이 필요해집니다.
    private float? fixedIncomingDamage;

    public void SetFixedIncomingDamage(float? value)
    {
        fixedIncomingDamage = value;
    }

    // [42번 장신구 연동 - 무적시간 증가] fixedIncomingDamage와 동일한 패턴입니다 -
    // 장신구는 한 번에 하나만 장착할 수 있어 단일 값으로 충분하고, 장착 시
    // SetInvincibilityDurationBonus(양수)로 걸었다가 해제 시 0으로 되돌립니다
    // (ITrinketEffect.Remove()). 아무 장신구도 없으면 항상 0이라 기존 동작과 완전히
    // 동일합니다.
    private float invincibilityDurationBonus;

    public void SetInvincibilityDurationBonus(float bonus)
    {
        invincibilityDurationBonus = bonus;
    }

    // [v1.27 추가 - "이번 방에서 무피해로 클리어" 같은 조건형 장신구 효과용]
    // 실제로 체력이 깎일 때마다(무적 시간 중 무시된 공격은 제외) 호출됩니다. 몇 대 맞았는지나
    // 얼마나 맞았는지에는 관심 없고 "맞았다는 사실"만 필요한 효과(39번 장신구 등)를 위한
    // 범용 훅입니다 - 필요하면 나중에 카메라 흔들림/피격 사운드 등도 이 이벤트에 얹을 수
    // 있습니다.
    public event Action<float> OnDamageTaken;

    // PlayerHitFlash.cs가 매 프레임 읽어서, 무적 시간 동안 점멸 연출을 표시할지 판단하는 데 씁니다.
    // 무적 판정 로직 자체(invincibilityTimer)는 전혀 바뀌지 않았고, 그 값을 밖에서 읽을 수 있게
    // 창구 하나만 열어둔 것입니다.
    public bool IsInvincible => invincibilityTimer > 0f;

    // [보호막] 체력이 실제로 깎여서 생긴 무적 시간일 때만 true입니다(보호막이 막아서 생긴 무적은 false).
    // 피격 점멸(PlayerHitFlash)과 피격 표정(SDFaceGameEvents)은 이 값을 봅니다. 공격이 막히는지 판정은 여전히 IsInvincible입니다.
    public bool IsHitInvincible => invincibilityTimer > 0f && invincibilityFromHit;

    // [보호막] 현재/최대 보호막 개수입니다(보호막 UI가 읽음).
    public int CurrentShield => currentShield;
    public int MaxShield => testNoShieldCap ? 9999 : Mathf.Max(0, maxShield);

    // [보호막] 지금 공격이 들어오면 보호막이 막게 되는지입니다. 적 탄환/근접 판정이 피격 충격 이펙트를 띄울지 정할 때 씁니다.
    // TakeDamage가 보호막을 깎는 조건과 같습니다(사망X, 테스트 무적X, 무적 시간X, 보호막 1개 이상).
    public bool WillShieldBlockNextHit => !IsDead && !testInvincible && invincibilityTimer <= 0f && currentShield > 0;

    // [보호막] 개수가 바뀔 때마다 새 개수를 알려줍니다.
    public event Action<int> OnShieldChanged;
    // [보호막] 보호막이 공격을 막고 1개 깎인 순간 울립니다(보호막 파괴 연출용). 피격 알림(OnDamageTaken)과는 별개입니다.
    public event Action OnShieldBroken;
    // [보호막] AddShield로 보호막을 실제로 얻은 순간 (늘어난 개수, 획득 연출을 지금 바로 재생할지) 를 알려줍니다. 이미 최대라 늘지
    // 않았으면 울리지 않습니다. 보호막 획득 연출(PlayerShieldBreakVfx)은 두 번째 값이 true일 때만 바로 재생합니다 - 58번처럼 보호막은
    // 즉시 주되 연출은 나중(해킹 발동 순간)에 직접 재생하는 경우 false로 부릅니다. 테스트용 SetShield(채우기)에서는 울리지 않습니다.
    public event Action<int, bool> OnShieldGained;

    // [테스트 전용 - PlayerTestProfile의 "무적 모드"] 켜져 있으면 TakeDamage()가 아무것도 하지
    // 않습니다(체력 감소/피격 무적 시간/OnDamageTaken 모두 없음). 테스트 씬에서 PlayerTestProfile이
    // 켤 때만 true가 되고, 인스펙터에 표시되거나 프리팹에 저장되지 않는 실행 전용 값입니다.
    [System.NonSerialized] public bool testInvincible = false;

    // [테스트 전용 - PlayerTestProfile의 "보호막 상한 해제"] 켜져 있으면 보호막 최대 개수(maxShield) 제한 없이 쌓을 수 있습니다.
    // 보호막 바는 그래도 maxShield(5)칸까지만 나눠 그립니다. 실행 전용 값이라 프리팹에 저장되지 않습니다.
    [System.NonSerialized] public bool testNoShieldCap = false;

    // [사망 처리 추가] 체력이 0이 되어 Die()가 호출되는 순간 딱 한 번 발생합니다.
    // - OnDied: 이 플레이어 자신에게 붙은 컴포넌트용(PlayerDeathHandler가 조작 정지 + 사망
    //   애니메이션을 처리합니다).
    // - OnAnyPlayerDied: 플레이어를 미리 알 수 없는 씬 쪽 UI용(GameOverUI가 게임 정지 + 다시
    //   시작 확인창을 처리합니다). LevelPortal.OnAnyPortalInteractionRequested와 같은 방식입니다.
    // 이 스크립트 자체는 "죽었다"는 사실만 알리고, 그 뒤 연출/UI는 전부 위 두 스크립트가 담당합니다.
    public event Action OnDied;
    public static event Action<PlayerHealth> OnAnyPlayerDied;

    void Awake()
    {
        stats = GetComponent<PlayerStats>();
        currentHealth = stats.MaxHealth; // 시작 시 최대 체력으로 초기화
        currentShield = Mathf.Clamp(startingShield, 0, MaxShield); // [보호막] 시작 보호막
        stats.OnMaxHealthChanged += HandleMaxHealthChanged;
        stats.OnMaxHealthIncreaseCapped += HandleMaxHealthIncreaseCapped;

        // [사망 처리 추가] 사망 시 조작 정지/사망 애니메이션 담당 컴포넌트를 자동으로 붙입니다
        // (수류탄/해킹툴 컴포넌트가 PlayerWeaponMode를 자동으로 붙이는 것과 같은 방식 - 플레이어
        // 프리팹에 직접 추가할 필요 없음).
        if (GetComponent<PlayerDeathHandler>() == null)
        {
            gameObject.AddComponent<PlayerDeathHandler>();
        }

        // [보호막] 보호막 파괴 연출(깎은 정이십면체) 컴포넌트도 없으면 자동으로 붙입니다(기본값으로 동작). 값을 조절하려면
        // 플레이어 프리팹에 PlayerShieldBreakVfx를 직접 붙여서 인스펙터에서 바꾸면 됩니다.
        if (GetComponent<PlayerShieldBreakVfx>() == null)
        {
            gameObject.AddComponent<PlayerShieldBreakVfx>();
        }
    }

    void OnDestroy()
    {
        if (stats != null)
        {
            stats.OnMaxHealthChanged -= HandleMaxHealthChanged;
            stats.OnMaxHealthIncreaseCapped -= HandleMaxHealthIncreaseCapped;
        }
    }

    void Update()
    {
        if (invincibilityTimer > 0f)
        {
            invincibilityTimer -= Time.deltaTime;
        }
    }

    // 적 공격 등 외부에서 호출하는 피격 함수
    public void TakeDamage(float amount)
    {
        if (IsDead) return;
        if (testInvincible) return; // 테스트 무적 모드
        if (invincibilityTimer > 0f) return; // 무적 시간 중에는 피해 무시

        // [보호막] 보호막이 있으면 피해량과 상관없이 보호막 1개만 깎고 끝냅니다. 무적 시간은 주지만 피격으로 치지 않습니다
        // (체력 감소/OnDamageTaken/피격 점멸/피격 표정 없음 - 클래스 위 [보호막] 설명 참고).
        if (currentShield > 0)
        {
            currentShield--;
            invincibilityTimer = invincibilityDuration + invincibilityDurationBonus;
            invincibilityFromHit = false;
            OnShieldChanged?.Invoke(currentShield);
            OnShieldBroken?.Invoke();
            return;
        }

        float actualAmount = fixedIncomingDamage ?? amount;
        currentHealth -= actualAmount;
        invincibilityTimer = invincibilityDuration + invincibilityDurationBonus;
        invincibilityFromHit = true;

        OnDamageTaken?.Invoke(actualAmount);

        if (currentHealth <= 0f)
        {
            currentHealth = 0f;
            Die();
        }

        // TODO: 카메라 흔들림, 피격 사운드, Animator "Hit" 트리거 등을 여기에 연결할 수 있습니다.
        // (피격 시 점멸 연출은 PlayerHitFlash.cs가 IsHitInvincible을 읽어서 독립적으로 처리합니다 - 보호막으로 막은 무적에는 점멸하지 않음.)
    }

    // [보호막] 보호막을 amount개 얻습니다(최대 maxShield까지, 넘치는 만큼은 버림). 실제로 늘어난 개수를 돌려줍니다.
    // 보호막을 주는 아이템은 이 함수를 부르면 됩니다.
    // playGainVfx: 획득 연출을 지금 바로 재생할지입니다(기본 true). false면 보호막만 늘리고 연출은 부르는 쪽이 원하는 때에
    // PlayerShieldBreakVfx.PlayAcquire()로 재생합니다(58번 - 해킹 발동 순간).
    public int AddShield(int amount, bool playGainVfx = true)
    {
        if (IsDead || amount <= 0) return 0;
        int before = currentShield;
        currentShield = Mathf.Clamp(currentShield + amount, 0, MaxShield);
        int gained = currentShield - before;
        if (gained != 0)
        {
            OnShieldChanged?.Invoke(currentShield);
            OnShieldGained?.Invoke(gained, playGainVfx);
        }
        return gained;
    }

    // [보호막] 보호막 개수를 정확히 value개로 맞춥니다(0 ~ maxShield). 테스트용 "보호막 채우기"처럼 값을 바로 정할 때 씁니다.
    // 이걸로 줄이는 것은 공격을 막은 것이 아니므로 OnShieldBroken은 울리지 않습니다.
    public void SetShield(int value)
    {
        int clamped = Mathf.Clamp(value, 0, MaxShield);
        if (clamped == currentShield) return;
        currentShield = clamped;
        OnShieldChanged?.Invoke(currentShield);
    }

    // 회복 아이템 등에서 사용할 함수 (최대 체력은 넘지 않음)
    // [101번] 회복 배율(HealBoostOverride, 없으면 1)을 곱한 뒤 최대 체력에서 자릅니다.
    public void Heal(float amount)
    {
        if (IsDead) return;
        if (amount > 0f) amount *= HealBoostOverride.GetMultiplier();
        currentHealth = Mathf.Min(currentHealth + amount, stats.MaxHealth);
    }

    // 최대 체력 자체를 늘려주는 아이템(체력방 등)을 위한 함수 - 나중에 사용
    // [101번] 현재 체력이 오르는 양에 회복 배율을 곱하고, 새 최대 체력을 넘지 않게 자릅니다.
    public void IncreaseMaxHealthAndHeal(float amount)
    {
        stats.maxHealth += amount;
        float gain = amount > 0f ? amount * HealBoostOverride.GetMultiplier() : amount;
        currentHealth = Mathf.Min(currentHealth + gain, stats.MaxHealth);
    }

    // [다음 전투 레벨 진입 시 최대 체력 정리 - 형빈 확정] n.5 레벨에서 다음 전투 레벨로 넘어갈 때 GameFlowManager가 부릅니다.
    // 채워지지 않은 최대 체력 칸을 1칸 단위로 지우고(예: 3/5 -> 3/3, 3.5/5 -> 3.5/4), 현재 체력은 그대로 둡니다. 지운 양을 돌려줍니다.
    public float ApplyLevelEndMaxHealthLoss()
    {
        if (IsDead || stats == null) return 0f;

        // 최대 체력 감소 알림(HandleMaxHealthChanged)은 현재 체력도 같은 폭만큼 깎으므로, 그 전 값을 기억했다가 되돌립니다.
        float keep = currentHealth;
        float lost = stats.ApplyLevelEndMaxHealthLoss(currentHealth);
        currentHealth = Mathf.Min(keep, stats.MaxHealth);
        return lost;
    }

    // PlayerStats에서 아이템의 MaxHealth 모디파이어가 추가/제거되어 최종 MaxHealth가
    // 바뀔 때마다 호출됩니다. delta는 (변경 후 MaxHealth - 변경 전 MaxHealth)입니다.
    // 증가분만큼 현재 체력도 같이 채워주고, 트레이드오프 등으로 줄어드는 경우에도
    // 같은 폭으로 맞춘 뒤 새 최대치를 넘지 않도록 clamp합니다.
    // [101번] 최대 체력이 오를 때(delta > 0)는 현재 체력을 delta x 회복 배율만큼 올립니다(예: 3/5에서 +1 → 5/6).
    // 줄어들 때는 배율을 쓰지 않습니다.
    private void HandleMaxHealthChanged(float delta)
    {
        if (IsDead) return;
        float gain = delta > 0f ? delta * HealBoostOverride.GetMultiplier() : delta;
        currentHealth = Mathf.Clamp(currentHealth + gain, 0f, stats.MaxHealth);
    }

    // [최대 체력 상한 - 형빈 확정] 최대 체력이 상한(PlayerStats.maxHealthCap, 기본 10)에 막혀 오르지 못한 만큼 현재 체력을 회복합니다.
    // 일반 회복과 같이 Heal()을 거치므로 회복 배율(101번)이 적용되고 최대 체력을 넘지 않습니다.
    private void HandleMaxHealthIncreaseCapped(float blockedAmount)
    {
        if (blockedAmount <= 0f) return;
        Heal(blockedAmount);
    }

    void Die()
    {
        if (IsDead) return;
        IsDead = true;
        Debug.Log("플레이어 사망");

        // 조작 정지/사망 애니메이션(PlayerDeathHandler)이 먼저 처리된 뒤, 게임 정지/확인창
        // (GameOverUI)이 이어지도록 이 순서로 알립니다.
        OnDied?.Invoke();
        OnAnyPlayerDied?.Invoke(this);
    }
}