using UnityEngine;

// 패시브 78번 - "피해를 받을 때마다 공격력이 1 증가한다."
//
// [기획 - 형빈 확정]
// - 피해량(0.5, 1, 2 ...)과 상관없이 피해를 입는 순간마다 공격력 damagePerHit(기본 1)을 얻습니다.
// - 무적 시간 중에 받은(=무시된) 피해로는 얻지 않습니다.
// - 피해 종류와 상관없이 모든 피해에 적용됩니다(적 공격, 체력 소모 특수방 출입, 수류탄 자해 등 - 전부
//   PlayerHealth.TakeDamage를 거침).
// - 실제 피해가 0보다 클 때만 얻습니다(플레이어 피해는 최소 0.5). 37번처럼 피격 자체를 막는 경우는 TakeDamage가 아예 불리지
//   않으므로 당연히 얻지 않습니다.
// - 오른 공격력은 영구 귀속입니다. 48번 등으로 78번을 잃어도 남습니다(47번과 같은 방식 - "아이템 ID + _effect" 출처로
//   등록, IRemovablePassiveEffect.cs 참고). 잃으면 피격 감시만 멈추고, 48번으로 다시 얻으면 그때부터 다시 쌓입니다.
//
// [언제 반응하나 - PlayerHealth.OnDamageTaken]
// 이 알림은 TakeDamage 안에서 사망/테스트 무적/무적 시간 확인을 모두 통과해 실제로 체력이 깎인 뒤에만 울립니다. 그래서
// "무적 시간 중 피해 제외"가 따로 검사하지 않아도 지켜집니다. (테스트 무적을 켜면 피해가 안 들어가므로 공격력도 안 오릅니다.)
//
// [피격 하트 연출 - 38번 하트, 형빈 확정]
// 피격 순간 38번 매료와 같은 하트 이펙트(VFX_Charm_Status 프리팹)를 플레이어 몸통 중심에 띄웁니다. 플레이어 색은 바꾸지
// 않습니다. 하트는 피격 무적 시간이 끝날 때까지 계속 올라오고(PlayerHealth.IsInvincible을 매 프레임 확인 - 무적 시간 보너스
// 아이템도 자동 반영), 끝나면 38번과 같이 떠 있던 하트까지 fadeOutTime 동안 함께 투명해지며 사라집니다
// (StatusVfxParticleFader 재사용). 이펙트는 플레이어의 자식이 아니라 따로 생성해서 위치만 따라가므로, 플레이어가 돌아도
// 프리팹에 정한 방향이 유지됩니다(38번과 같은 방식). 치명상(체력 0)일 때는 띄우지 않습니다.
//
// [아이템 에셋 설정]
// ItemDefinition: Item Id = 78, Category = Passive, Special Effects에 HitDamageBoostEffect 하나. Stat Modifiers는 비워둡니다.
// Hit Heart Vfx Prefab 칸에 Assets/VFX/Custom/VFX_Charm_Status를 넣습니다(78번만 모양을 바꾸려면 복제해서 넣기).
[System.Serializable]
public class HitDamageBoostEffect : IPassiveItemEffect, IRemovablePassiveEffect
{
    [Tooltip("피해를 입을 때마다 얻는 공격력입니다(기본 1). 피해량과 상관없이 한 번 맞을 때마다 줍니다.")]
    public float damagePerHit = 1f;

    [Header("피격 하트 연출 (38번 하트)")]
    [Tooltip("피격 시 플레이어에게 띄울 하트 이펙트 프리팹입니다(38번과 같은 VFX_Charm_Status). 비워두면 연출 없이 공격력만 오릅니다. " +
             "프리팹 안의 파티클은 Looping이 켜져 있어야 무적 시간 동안 계속 올라옵니다.")]
    public GameObject hitHeartVfxPrefab;

    [Tooltip("몸통 중심에서 이펙트를 옮기는 값입니다(월드 기준, m). 예: (0, 0.3, 0)이면 0.3m 위.")]
    public Vector3 positionOffset = Vector3.zero;

    [Tooltip("무적 시간이 끝났을 때 떠 있던 하트까지 함께 투명해지며 사라지는 시간(초)입니다(38번과 같은 0.25). 0이면 즉시 지웁니다.")]
    [Min(0f)] public float fadeOutTime = 0.25f;

    private PlayerHealth health;
    private PlayerStats stats;
    private string effectSourceId;

    public void Apply(PassiveEffectContext context, string sourceItemId)
    {
        if (context == null || context.PlayerObject == null)
        {
            Debug.LogWarning("HitDamageBoostEffect: 플레이어를 찾을 수 없어 효과를 적용하지 못했습니다.");
            return;
        }

        // 재적용 시 이전 구독이 남아 있으면 한 번 맞을 때 여러 번 오르므로 먼저 정리합니다.
        Unsubscribe();

        health = context.Health != null ? context.Health : context.PlayerObject.GetComponent<PlayerHealth>();
        stats = context.Stats != null ? context.Stats : context.PlayerObject.GetComponent<PlayerStats>();
        if (health == null || stats == null)
        {
            Debug.LogWarning("HitDamageBoostEffect: PlayerHealth/PlayerStats를 찾을 수 없어 효과를 적용하지 못했습니다.");
            return;
        }

        effectSourceId = PassiveEffectIds.EffectStatSourceId(sourceItemId);
        health.OnDamageTaken += HandleDamageTaken;
    }

    // [패시브 잃기 - 48번 등] 피격 감시를 멈춥니다. 이미 오른 공격력은 영구 귀속이라 그대로 둡니다(형빈 확정).
    // 지금 떠 있는 하트는 무적 시간이 끝날 때까지 그대로 재생되다 사라집니다.
    public void Remove(PassiveEffectContext context, string sourceItemId)
    {
        Unsubscribe();
    }

    private void Unsubscribe()
    {
        if (health != null) health.OnDamageTaken -= HandleDamageTaken;
    }

    private void HandleDamageTaken(float amount)
    {
        if (amount <= 0f || stats == null) return;

        stats.AddModifier(StatType.Damage, ModifierType.Additive, damagePerHit, effectSourceId);

        // 치명상이면 연출은 생략합니다(이 알림은 사망 처리 직전에 옵니다).
        if (health == null || health.CurrentHealth <= 0f) return;
        if (hitHeartVfxPrefab == null) return;

        var runner = health.GetComponent<PlayerHitHeartVfx>();
        if (runner == null) runner = health.gameObject.AddComponent<PlayerHitHeartVfx>();
        runner.Play(hitHeartVfxPrefab, positionOffset, fadeOutTime);
    }
}

// 78번 피격 하트 연출을 담당하는 컴포넌트입니다. HitDamageBoostEffect가 플레이어에 자동으로 붙이므로 직접 붙일 필요가 없습니다.
// 피격 무적 시간(PlayerHealth.IsInvincible) 동안 하트 이펙트를 플레이어 몸통 중심에 띄워 따라가게 하고, 무적이 끝나면 새 하트를
// 멈추고 떠 있던 하트까지 fadeOutTime 동안 투명하게 지웁니다(38번 매료 하트가 끝나는 방식과 동일).
[DisallowMultipleComponent]
public class PlayerHitHeartVfx : MonoBehaviour
{
    private PlayerHealth health;
    private CharacterController body;
    private GameObject instance;
    private Vector3 offset;
    private float fadeOutTime;

    // 하트를 띄웁니다. 이미 떠 있으면 새로 만들지 않고 그대로 이어갑니다(무적 시간이 다시 시작되므로 자연히 연장됨).
    public void Play(GameObject prefab, Vector3 positionOffset, float fadeTime)
    {
        if (health == null) health = GetComponent<PlayerHealth>();
        if (body == null) body = GetComponent<CharacterController>();
        offset = positionOffset;
        fadeOutTime = fadeTime;

        if (instance != null || prefab == null) return;

        // 플레이어의 자식으로 붙이지 않고 따로 만든 뒤 위치만 따라갑니다(회전/크기는 프리팹에 정한 그대로).
        instance = Instantiate(prefab, GetPosition(), prefab.transform.rotation);
    }

    void LateUpdate()
    {
        if (instance == null) return;

        bool stillInvincible = health != null && !health.IsDead && health.IsInvincible;
        if (!stillInvincible)
        {
            Release();
            return;
        }

        instance.transform.position = GetPosition();
    }

    void OnDisable()
    {
        Release();
    }

    // 몸통 중심 = 플레이어 CharacterController의 중심(파츠가 떨어져 있는 모델이어도 일정함). 없으면 렌더러 전체 중심, 그것도 없으면 발밑.
    private Vector3 GetPosition()
    {
        Vector3 basePos = transform.position;

        if (body != null)
        {
            basePos = body.bounds.center;
        }
        else
        {
            var renderers = GetComponentsInChildren<Renderer>();
            bool found = false;
            Bounds b = default;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null || renderers[i] is ParticleSystemRenderer) continue;
                if (!found) { b = renderers[i].bounds; found = true; }
                else b.Encapsulate(renderers[i].bounds);
            }
            if (found) basePos = b.center;
        }

        return basePos + offset;
    }

    // 새 하트를 멈추고, 떠 있던 하트까지 fadeOutTime 동안 함께 투명하게 지웁니다(0이면 즉시 지움).
    private void Release()
    {
        if (instance == null) return;

        GameObject inst = instance;
        instance = null;

        var systems = inst.GetComponentsInChildren<ParticleSystem>(true);
        if (fadeOutTime > 0f && systems.Length > 0)
        {
            for (int i = 0; i < systems.Length; i++)
            {
                systems[i].Stop(false, ParticleSystemStopBehavior.StopEmitting);
            }
            StatusVfxParticleFader.Begin(inst, fadeOutTime);
        }
        else
        {
            Destroy(inst);
        }
    }
}
