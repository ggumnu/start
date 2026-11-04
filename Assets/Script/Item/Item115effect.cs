using UnityEngine;

// 패시브 115번 - "수류탄이 크레모어로 바뀐다. 착탄하면 터지지 않고 설치되고, 수류탄 모드에서 R을 누르면 한꺼번에 터진다."
//
// [기획 - 형빈 확정]
// - 던지는 순간 115번이 있으면 그 수류탄은 크레모어입니다. 포물선으로 날아가는 것/수류탄 스탯(피해/반경/자해/사거리)은 그대로이고,
//   착지하면 터지지 않고 그 자리에 설치됩니다. 설치된 동안에도 착탄 범위 원이 계속 보이고, 81번 발광보다 큰 빨간 빛이
//   "밝음 → 서서히 어두움 → 서서히 밝음"으로 점멸합니다(ClaymoreGlow).
// - 수류탄 모드(2번)에서 R을 누르면 "지금 있는 방"의 크레모어가 모두 터집니다. 다른 방에 설치한 크레모어는 남아 있고, 그 방으로
//   돌아가서 R을 누르면 터집니다. 총/해킹툴 모드에서는 R로 터뜨릴 수 없습니다(총 모드 R은 평소처럼 장전).
//   - 날아가는 도중에는 R을 눌러도 터지지 않습니다. 반드시 착지해서 설치된 크레모어만 터집니다(형빈 확정 - 공중 폭파 없음).
//     착탄 범위 원은 착지점에 고정되어 보입니다(77번 유도 중에는 크레모어를 따라다님).
//   - 터뜨린 뒤 수류탄이 0개면 기본 총 모드로 돌아가고, 남아 있으면 수류탄 모드를 유지합니다.
//   - 수류탄이 0개여도 2번으로 수류탄 모드에 들어갈 수 있어서(던질 수는 없음) 마지막 수류탄으로 설치한 크레모어도 터뜨릴 수 있습니다
//     (PlayerWeaponMode).
// - 다른 수류탄 아이템과 그대로 함께 동작합니다.
//   - 57번 자해 무효/토끼, 66번 수류탄 해킹, 81번 소이탄(불 구역): 크레모어가 터질 때 평소 폭발과 똑같이.
//   - 77번: 착지(설치)하면 터뜨리기 전까지 계속 유도합니다(1초 제한 없음). 설치된 방의 적만 쫓고, 적이 없으면 그 자리에 머뭅니다.
//   - 92번: 첫 크레모어가 착지(설치)하는 순간 그 자리에서 X자 4방향으로 분열 크레모어 4개가 날아가 설치됩니다(한 번에 5개).
//     분열 크레모어는 다시 분열하지 않습니다.
// - 다음 레벨로 넘어가면 설치된 크레모어는 모두 조용히 사라집니다.
// - 115번을 잃으면(48번 등) 모든 방에 설치된 크레모어가 그 즉시 모두 터집니다. 아직 날아가던 크레모어는 일반 수류탄으로 돌아가 착지하면
//   평소처럼 터집니다. 이후 던지는 수류탄은 일반 수류탄입니다.
// - [획득 시 지급] 얻을 때 수류탄 grenadeAmount(기본 2)개를 줍니다. 92번/77번과 같은 1회성 지급 규칙입니다
//   (GameFlowManager.TryConsumeOneTimeGrant - 보물상자/받침대/상점에서 새로 얻으면 지급, 버렸던 사본을 다시 주우면 지급 없음,
//   48번으로 얻으면 제한 없이 지급). 잃어도 받은 수류탄은 빼앗지 않습니다(기록 이름 "115_grenade_grant").
//
// [구현]
// ClaymoreOverride 등록소에 설정을 등록/해제하고, 수류탄 모드 R 입력을 받는 ClaymoreDetonator를 플레이어에 붙입니다. 크레모어의 설치/
// 유도/분열/폭발은 Grenade가 합니다.
//
// [아이템 에셋 설정]
// ItemDefinition: Item Id = 115, Category = Passive, Special Effects에 ClaymoreEffect 하나. Stat Modifiers는 비워둡니다.
[System.Serializable]
public class ClaymoreEffect : IPassiveItemEffect, IRemovablePassiveEffect
{
    [Tooltip("크레모어 설치 중 빨간 점멸 설정입니다.")]
    public ClaymoreOverride.Settings settings = new ClaymoreOverride.Settings();

    [Tooltip("얻을 때 주는 수류탄 개수입니다(1회성 지급 - 92번과 같은 규칙).")]
    [Min(0)] public int grenadeAmount = 2;

    public void Apply(PassiveEffectContext context, string sourceItemId)
    {
        if (context == null || context.PlayerObject == null)
        {
            Debug.LogWarning("ClaymoreEffect: 플레이어를 찾을 수 없어 115번 효과를 적용하지 못했습니다.");
            return;
        }

        if (settings == null) settings = new ClaymoreOverride.Settings();
        ClaymoreOverride.Register(SourceKey(sourceItemId), context.PlayerObject, settings);

        if (context.PlayerObject.GetComponent<ClaymoreDetonator>() == null)
        {
            context.PlayerObject.AddComponent<ClaymoreDetonator>();
        }

        GrantGrenades(context, sourceItemId);
    }

    // 획득 시 수류탄 지급입니다. 92번 GrantGrenades와 같은 규칙입니다(위 [획득 시 지급] 참고).
    private void GrantGrenades(PassiveEffectContext context, string sourceItemId)
    {
        if (grenadeAmount <= 0) return;

        if (context.Resources == null)
        {
            Debug.LogWarning("ClaymoreEffect: PlayerResources를 찾을 수 없어 수류탄을 지급하지 못했습니다.");
            return;
        }

        // 1회성 지급 기록(GameFlowManager)으로 교체/버리기를 통한 무한 복제를 막습니다. 48번으로 얻을 때는
        // (PassiveEffectContext.IgnoreOneTimeGrantLimit) 기록을 거치지 않고 지급합니다. GameFlowManager가 없는 씬(아이템 테스트
        // 씬)에서는 경고만 남기고 지급합니다.
        if (!context.IgnoreOneTimeGrantLimit)
        {
            var gameFlow = Object.FindFirstObjectByType<GameFlowManager>();
            if (gameFlow != null)
            {
                if (!gameFlow.TryConsumeOneTimeGrant(sourceItemId + "_grenade_grant")) return;
            }
            else
            {
                Debug.LogWarning("ClaymoreEffect: GameFlowManager를 찾지 못해 1회성 지급 제한 없이 수류탄을 지급합니다.");
            }
        }

        context.Resources.Add(BasicItemType.Grenade, grenadeAmount);
    }

    // [패시브 잃기 - 48번 등] 모든 방의 크레모어를 즉시 터뜨리고(형빈 확정), 이후로는 일반 수류탄입니다.
    public void Remove(PassiveEffectContext context, string sourceItemId)
    {
        ClaymoreOverride.Unregister(SourceKey(sourceItemId));
        ClaymoreOverride.DetonateAll();

        if (context?.PlayerObject != null && !ClaymoreOverride.TryGetActive(out _))
        {
            var detonator = context.PlayerObject.GetComponent<ClaymoreDetonator>();
            if (detonator != null) Object.Destroy(detonator);
        }
    }

    private static string SourceKey(string sourceItemId) => "claymore_" + sourceItemId;
}

// 115번이 플레이어에 붙이는 "수류탄 모드 R = 지금 방의 크레모어 모두 터뜨리기" 입력 담당입니다(직접 붙일 필요 없음).
public class ClaymoreDetonator : MonoBehaviour
{
    public KeyCode detonateKey = KeyCode.R;

    private PlayerWeaponMode weaponMode;
    private PlayerGrenadeThrower thrower;
    private PlayerHealth health;

    void Awake()
    {
        weaponMode = PlayerWeaponMode.GetOrAdd(gameObject);
        thrower = GetComponent<PlayerGrenadeThrower>();
        health = GetComponent<PlayerHealth>();
    }

    void Update()
    {
        if (!Input.GetKeyDown(detonateKey)) return;
        if (Time.timeScale <= 0f) return;                          // 일시정지 중
        if (weaponMode == null || !weaponMode.IsGrenade) return;   // 수류탄 모드에서만(총 모드 R은 장전)
        if (PlayerItemPickupPresenter.BlocksActions) return;       // 아이템 획득 연출 중
        if (health != null && health.IsDead) return;

        int detonated = ClaymoreOverride.DetonateInRoom(ClaymoreOverride.CurrentPlayerRoom());
        if (detonated <= 0) return;

        // 터뜨렸는데 수류탄이 0개면 기본 총 모드로 돌아갑니다(형빈 확정). 던지는 동작 중이면 동작이 끝날 때 PlayerGrenadeThrower가
        // 알아서 돌려보내므로 여기서는 건드리지 않습니다(애니메이션이 끊기지 않도록).
        if (thrower == null) thrower = GetComponent<PlayerGrenadeThrower>();
        bool throwing = thrower != null && thrower.IsThrowing;
        int grenades = thrower != null ? thrower.GrenadeCount : 0;
        if (!throwing && grenades <= 0) weaponMode.ReturnToGunAfterUse();
    }
}
