using System;
using UnityEngine;

// 패시브 111번 - "피해를 입지 않은 채로 방을 클리어하면 공격력이 5 증가한다. 피해를 입을 때마다 공격력이 1 감소한다."
//
// [기획 - 형빈 확정]
// - [장신구 -> 패시브로 변경 - 형빈 확정] 기능은 장신구 때와 같습니다.
// - 가지고 있는 동안 전투방(일반방/보스방 - 클리어가 있는 방)을 한 번도 맞지 않고 클리어하면 공격력 +5(더하기).
//   "맞았는지"는 방에 들어올 때마다 새로 셉니다. 클리어 못 한 방에서 맞고 나갔다가 다시 들어와(적 부활) 안 맞고 클리어하면 +5.
//   얻은 순간에도 새로 셉니다(방 도중에 얻으면 얻은 뒤의 피해만 셈 - 39번과 같은 규칙).
// - 피해를 입을 때마다 공격력 -1(한 방에서 여러 번 맞으면 그만큼 계속). 피해량과 상관없이 한 번에 1.
//   실제로 체력이 깎인 피해만 셉니다(PlayerHealth.OnDamageTaken) - 무적 시간 중/74번 대시 회피/37번 피격 차단은 피해가 없으므로 X.
//   특수방 체력 소모, 수류탄 자해, 81번 불 구역 등 종류와 상관없이 모두 셉니다. 78번(맞을 때마다 +1)과 같이 있으면 서로 상쇄됩니다.
// - 이 효과로 생긴 +/-는 영구 귀속입니다. 111번을 잃어도(48번 등) 그대로 남고, 다시 얻으면 그 위에 이어서 쌓입니다
//   (47/78번과 같은 "아이템 ID + _effect" 출처 - 패시브를 잃을 때 PlayerInventory가 지우는 출처(아이템 ID)와 달라서 남음).
// - 계산은 정직하게: 공격력 하한선(PlayerStats의 Stat Floors)은 "최종 값"에만 걸리고, 깎인 값은 하한선 밑으로도 계속 쌓입니다.
//   예) 실제 -10인데 하한선 1이라 1로 보이고 1로 적용 -> 성공하면 실제 -5, 여전히 1.
// - 연출(성공): 머리 위로 하늘색 발광/입자와 함께 111번 아이콘이 떠오릅니다(106번 성공과 같은 연출).
// - 연출(실패 - 형빈 확정): 성공 기준이 걸린 방(아직 클리어하지 않은 일반방/보스방)에서 "처음" 맞아 성공이 무산되는 순간에만, 빨간 발광/입자와
//   함께 111번 아이콘이 떠오른 뒤 VFX_ImpactCritical_1.1.0_Pink가 머리 위에서 터집니다(103번과 같은 연출). 같은 방에서 또 맞으면 -1만 되고 연출은
//   없습니다. 이미 클리어한 방/특수방/동아리방 등 성공 기준이 없는 방에서 맞으면 -1만 됩니다. 나갔다가 다시 들어오면(적 부활) 다시 셉니다.
// - 머리 위 알림은 다른 알림(103/39번 등)과 같이 생기면 순서대로 나옵니다(CreditPopupDisplay 줄 세우기).
// - 또 성공하면 스탯 패널의 공격력 숫자에 노란 확대 연출을 "항상" 보여줍니다 - 하한선 때문에 숫자가 그대로(1 -> 1)여도 올랐다는 걸
//   알리기 위함(PlayerStatDisplay.RequestFlash).
//
// [구현] 39번(무피해 클리어)과 78번(피격 시 영구 공격력)을 합친 구조입니다.
// - 피격: PlayerHealth.OnDamageTaken. 방 진입: RoomController.OnAnyRoomEntered. 클리어: RoomController.OnAnyRoomCleared.
// - 공격력은 변할 때마다 PlayerStats.AddModifier(Damage, Additive, +5 또는 -1, "111_effect")를 하나씩 더합니다(47번과 같은 방식 -
//   지울 일이 없는 영구 효과라 따로 추적하지 않음).
//
// [아이템 에셋 설정]
// ItemDefinition: Item Id = 111, Category = Passive, Special Effects에 NoDamageClearDamageEffect 하나. Stat Modifiers는 비워둡니다.
// Failure Vfx Prefab 칸에 VFX_ImpactCritical_1.1.0_Pink를 넣습니다.
[Serializable]
public class NoDamageClearDamageEffect : IPassiveItemEffect, IRemovablePassiveEffect
{
    // 영구 귀속 효과의 출처 이름 꼬리입니다(47/78번과 같은 규칙 - IRemovablePassiveEffect.cs 참고).
    private const string EffectSourceSuffix = "_effect";

    [Header("공격력 증감")]
    [Tooltip("피해 없이 방을 클리어했을 때 오르는 공격력입니다(기본 5, 더하기).")]
    public float damageOnNoDamageClear = 5f;

    [Tooltip("피해를 입을 때마다 깎이는 공격력입니다(기본 1, 피해량과 상관없이 한 번 맞을 때마다).")]
    [Min(0f)] public float damageLossPerHit = 1f;

    [Header("성공 연출 - 머리 위 아이콘 (하늘색)")]
    [Tooltip("피해 없이 클리어했을 때 머리 위에 띄우는 아이콘 연출입니다. Icon을 비우면 111번 아이템 아이콘을 씁니다.")]
    public CreditPopupSettings successPopup = new CreditPopupSettings
    {
        glowColor = new Color(0.45f, 0.85f, 1f, 0.9f),
        sparkleColor = new Color(0.6f, 0.92f, 1f, 1f),
    };

    [Tooltip("켜면 성공했을 때 스탯 패널의 공격력 숫자에 노란 확대 연출을 항상 보여줍니다(하한선 때문에 숫자가 그대로여도).")]
    public bool forceStatFlashOnSuccess = true;

    [Header("실패 연출 - 머리 위 아이콘 (빨강) + 이펙트")]
    [Tooltip("성공 기준이 걸린 방에서 처음 맞았을 때 머리 위에 띄우는 아이콘 연출입니다. Icon을 비우면 111번 아이템 아이콘을 씁니다.")]
    public CreditPopupSettings failurePopup = new CreditPopupSettings
    {
        glowColor = new Color(1f, 0.12f, 0.12f, 0.9f),
        sparkleColor = new Color(1f, 0.3f, 0.25f, 1f),
    };

    [Tooltip("아이콘이 떠오른 뒤 머리 위에서 터뜨릴 이펙트입니다(VFX_ImpactCritical_1.1.0_Pink). 비워두면 아이콘 연출만 나옵니다.")]
    public GameObject failureVfxPrefab;

    [Tooltip("아이콘이 나타난 뒤 이펙트를 터뜨리기까지의 시간(초)입니다(103번과 같은 0.25).")]
    [Min(0f)] public float failureVfxDelay = 0.25f;

    [Tooltip("머리 위 기준에서 이펙트를 옮기는 값입니다(월드 기준, m).")]
    public Vector3 failureVfxOffsetAboveHead = new Vector3(0f, 0.6f, 0f);

    [Tooltip("이펙트 크기 배율입니다.")]
    [Min(0.01f)] public float failureVfxScale = 1f;

    [Tooltip("켜면 이펙트가 플레이어를 따라다닙니다.")]
    public bool failureVfxFollowPlayer = true;

    [Tooltip("생성한 이펙트를 이 시간(초) 뒤에 지웁니다.")]
    [Min(0.1f)] public float failureVfxLifetime = 2f;

    // ---- 실행 중 상태(저장되지 않음) ----
    [NonSerialized] private PassiveEffectContext ctx;
    [NonSerialized] private string itemId;
    [NonSerialized] private string effectSourceId;
    [NonSerialized] private bool tookDamageThisRoom;
    [NonSerialized] private RoomInstance trackedRoom; // 마지막으로 들어온 방(얻은 직후에는 지금 방)
    [NonSerialized] private Sprite itemIcon;
    [NonSerialized] private Action<float> damageHandler;
    [NonSerialized] private Action<RoomInstance> roomEnteredHandler;
    [NonSerialized] private Action<RoomInstance> roomClearedHandler;

    public void Apply(PassiveEffectContext context, string sourceItemId)
    {
        if (context == null || context.Stats == null || context.Health == null)
        {
            Debug.LogWarning("NoDamageClearDamageEffect: PlayerStats/PlayerHealth를 찾을 수 없어 111번 효과를 적용하지 못했습니다.");
            return;
        }
        if (string.IsNullOrEmpty(sourceItemId))
        {
            Debug.LogWarning("NoDamageClearDamageEffect: 아이템 ID가 비어 있어 111번 효과를 적용하지 못했습니다. ItemDefinition의 Item Id를 채워주세요.");
            return;
        }

        Unsubscribe(); // 혹시 Remove 없이 다시 불려도 중복 구독되지 않도록

        ctx = context;
        effectSourceId = sourceItemId + EffectSourceSuffix;
        itemId = sourceItemId;
        tookDamageThisRoom = false; // 얻은 순간부터 새로 셉니다
        trackedRoom = HeadAlertRoomRules.FindCurrentRoom();
        itemIcon = null; // 획득 순간에는 아직 보유 목록에 없을 수 있어, 연출할 때 찾습니다(ItemIcon)

        damageHandler = HandleDamageTaken;
        roomEnteredHandler = HandleRoomEntered;
        roomClearedHandler = HandleRoomCleared;
        context.Health.OnDamageTaken += damageHandler;
        RoomController.OnAnyRoomEntered += roomEnteredHandler;
        RoomController.OnAnyRoomCleared += roomClearedHandler;
    }

    // [패시브 잃기 - 48번 등] 감시만 멈춥니다. 이미 생긴 공격력 증감("111_effect")은 그대로 남습니다.
    public void Remove(PassiveEffectContext context, string sourceItemId)
    {
        Unsubscribe();
        ctx = null;
        tookDamageThisRoom = false;
        trackedRoom = null;
    }

    private void Unsubscribe()
    {
        if (ctx?.Health != null && damageHandler != null) ctx.Health.OnDamageTaken -= damageHandler;
        if (roomEnteredHandler != null) RoomController.OnAnyRoomEntered -= roomEnteredHandler;
        if (roomClearedHandler != null) RoomController.OnAnyRoomCleared -= roomClearedHandler;
        damageHandler = null;
        roomEnteredHandler = null;
        roomClearedHandler = null;
    }

    private void HandleRoomEntered(RoomInstance room)
    {
        tookDamageThisRoom = false;
        trackedRoom = room;
    }

    private void HandleDamageTaken(float amount)
    {
        if (ctx == null || ctx.Stats == null) return;
        if (amount <= 0f) return;

        bool firstHit = !tookDamageThisRoom;
        tookDamageThisRoom = true;
        if (damageLossPerHit > 0f)
        {
            ctx.Stats.AddModifier(StatType.Damage, ModifierType.Additive, -damageLossPerHit, effectSourceId);
        }

        // 성공 기준이 걸린 방에서 처음 맞은 순간에만 실패를 알립니다(치명상이면 생략).
        if (firstHit && (ctx.Health == null || !ctx.Health.IsDead) && HeadAlertRoomRules.IsSuccessCriteriaActive(trackedRoom))
        {
            PlayFailurePopup();
        }
    }

    private void HandleRoomCleared(RoomInstance room)
    {
        // 정적 알림이라, 플레이어가 사라졌는데(이전 플레이 등) 구독이 남아 있으면 정리만 합니다.
        if (ctx == null || ctx.Stats == null || ctx.PlayerObject == null) { Unsubscribe(); return; }
        if (tookDamageThisRoom) return;
        if (ctx.Health != null && ctx.Health.IsDead) return;

        ctx.Stats.AddModifier(StatType.Damage, ModifierType.Additive, damageOnNoDamageClear, effectSourceId);
        if (forceStatFlashOnSuccess) PlayerStatDisplay.RequestFlash(StatType.Damage, damageOnNoDamageClear >= 0f);
        PlaySuccessPopup();
    }

    // 111번 아이템 아이콘입니다(보유 패시브 목록에서 찾아 기억 - 획득 순간에는 아직 목록에 없을 수 있어 연출할 때 찾음).
    private Sprite ItemIcon
    {
        get
        {
            if (itemIcon != null) return itemIcon;
            var inventory = ctx?.Inventory != null ? ctx.Inventory
                          : (ctx?.PlayerObject != null ? ctx.PlayerObject.GetComponent<PlayerInventory>() : null);
            if (inventory == null) return null;
            foreach (var item in inventory.OwnedItems)
            {
                if (item != null && item.itemId == itemId)
                {
                    itemIcon = item.icon;
                    break;
                }
            }
            return itemIcon;
        }
    }

    // 성공 연출(순수 장식 - 문제가 생겨도 공격력 처리에는 영향 없음)
    private void PlaySuccessPopup()
    {
        try
        {
            GameObject player = ctx?.PlayerObject;
            if (player == null) return;
            CreditPopupDisplay.Show(player, player.transform.position, HeadAlertRoomRules.Sanitize(successPopup, true), ItemIcon,
                                    followTarget: true);
        }
        catch (Exception e)
        {
            Debug.LogError($"NoDamageClearDamageEffect: 성공 연출 중 오류가 발생해 건너뜁니다(공격력 처리에는 영향 없음): {e}");
        }
    }

    // 실패 연출(순수 장식) - 103번과 같은 빨간 아이콘 + 핑크 이펙트. 이펙트는 아이콘이 실제로 떠오르는 순간부터 failureVfxDelay 뒤에 터집니다.
    private void PlayFailurePopup()
    {
        try
        {
            GameObject player = ctx?.PlayerObject;
            if (player == null) return;

            Action onStarted = null;
            if (failureVfxPrefab != null)
            {
                Transform playerT = player.transform;
                GameObject prefab = failureVfxPrefab;
                float delay = failureVfxDelay, scale = failureVfxScale, lifetime = failureVfxLifetime;
                Vector3 offset = failureVfxOffsetAboveHead;
                bool follow = failureVfxFollowPlayer;
                onStarted = () => GlassCannonPenaltyVfxRunner.Spawn(playerT, prefab, delay, offset, scale, follow, lifetime);
            }

            CreditPopupDisplay.Show(player, player.transform.position, HeadAlertRoomRules.Sanitize(failurePopup, false), ItemIcon,
                                    followTarget: true, onStarted: onStarted, sound: SfxId.ItemFail);
        }
        catch (Exception e)
        {
            Debug.LogError($"NoDamageClearDamageEffect: 실패 연출 중 오류가 발생해 건너뜁니다(공격력 처리에는 영향 없음): {e}");
        }
    }
}
