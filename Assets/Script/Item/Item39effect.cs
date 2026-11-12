using System;
using UnityEngine;

// 장신구 39번 - "획득 시 최초 한정으로 크레딧 10 + 소지 중일 때 피격 없이 전투방을 클리어할
// 때마다 크레딧 3을 확정 획득".
//
// [기획]
// - 획득(장착)하면 그 즉시 크레딧을 10 얻습니다 - 단, 21번 장신구와 동일하게 같은 레벨
//   안에서는 딱 한 번만(GameFlowManager.TryConsumeOneTimeGrant 참고).
// - 장착하고 있는 동안, 전투방(일반방/보스방 - RoomController.OnAnyRoomCleared가 원래
//   그 두 종류에서만 발생함)을 "피격 없이" 클리어할 때마다 크레딧 3을 얻습니다. 이건
//   1회성 지급이 아니라 조건을 만족할 때마다 매번 반복 지급됩니다(같은 레벨이든 다른
//   레벨이든, 몇 번을 클리어하든 매번).
// - "피격 없이"는 방에 들어온 시점(RoomController.OnAnyRoomEntered) 또는 장착한 시점부터 셉니다(v1.28 - 장착 전에 맞은 것은 세지 않음).
//
// [성공/실패 알림 - 형빈 확정, 111번과 같은 규칙]
// - 성공(피격 없이 클리어): 크레딧 +3과 함께 머리 위로 하늘색 발광/입자와 39번 아이콘이 떠오릅니다(106/111번 성공과 같은 색).
// - 실패: 성공 기준이 걸린 방(아직 클리어하지 않은 일반방/보스방)에서 "처음" 맞는 순간에만, 빨간 발광/입자와 함께 39번 아이콘이 떠오른 뒤
//   VFX_ImpactCritical_1.1.0_Pink가 머리 위에서 터집니다(103번과 같은 연출). 같은 방에서 또 맞거나, 성공 기준이 없는 방(이미 클리어한 방,
//   특수방 등)에서 맞으면 알림이 없습니다.
// - 머리 위 알림은 다른 알림(103/111번 등)과 같이 생기면 순서대로 나옵니다(CreditPopupDisplay 줄 세우기).
//
// [구현 - PlayerHealth.OnDamageTaken + RoomController.OnAnyRoomEntered/OnAnyRoomCleared]
// 세 이벤트를 모두 구독해 "이번 방에서 맞았는지" 플래그 하나를 직접 관리합니다. 장착 해제
// 시(Remove) 전부 구독 해지합니다 - 안 그러면 장신구를 빼도 계속 크레딧이 지급됩니다.
//
// [아이템 에셋 설정]
// Failure Vfx Prefab 칸에 Assets/VFX/VFXPACK_IMPACT_WALLCOEUR_FreeVersion/00_Prefab/2_Impact_1.1.0/VFX_ImpactCritical_1.1.0_Pink를 넣습니다.
[Serializable]
public class CreditBonusTrinketEffect : ITrinketEffect
{
    public int creditOnFirstEquip = 10;
    public int creditOnNoDamageClear = 3;

    [Header("성공 연출 - 머리 위 아이콘 (하늘색)")]
    [Tooltip("피격 없이 클리어했을 때 머리 위에 띄우는 아이콘 연출입니다. Icon을 비우면 39번 아이템 아이콘을 씁니다.")]
    public CreditPopupSettings successPopup = new CreditPopupSettings
    {
        glowColor = new Color(0.45f, 0.85f, 1f, 0.9f),
        sparkleColor = new Color(0.6f, 0.92f, 1f, 1f),
    };

    [Header("실패 연출 - 머리 위 아이콘 (빨강) + 이펙트")]
    [Tooltip("성공 기준이 걸린 방에서 처음 맞았을 때 머리 위에 띄우는 아이콘 연출입니다. Icon을 비우면 39번 아이템 아이콘을 씁니다.")]
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

    // "이번 방에서 한 번이라도 맞았는지" - RoomController.OnAnyRoomEntered로 방이 바뀔 때마다
    // false로 초기화되고, PlayerHealth.OnDamageTaken이 발생하면 true가 됩니다.
    [NonSerialized] private bool tookDamageThisRoom;
    [NonSerialized] private RoomInstance trackedRoom;
    [NonSerialized] private TrinketEffectContext ctx;
    [NonSerialized] private Sprite itemIcon;

    [NonSerialized] private Action<float> damageHandler;
    [NonSerialized] private Action<RoomInstance> roomEnteredHandler;
    [NonSerialized] private Action<RoomInstance> roomClearedHandler;

    public void Apply(TrinketEffectContext context, string sourceItemId)
    {
        if (context == null) return;

        Unsubscribe(); // 혹시 Remove 없이 다시 불려도 중복 구독되지 않도록

        bool grantedNow = context.GameFlow != null &&
                           context.GameFlow.TryConsumeOneTimeGrant($"{sourceItemId}_credit");
        if (grantedNow && context.Resources != null)
        {
            context.Resources.Add(BasicItemType.Credit, creditOnFirstEquip);
        }

        // [v1.28 변경 - 무피해 판정 시작 시점을 "방 진입"이 아니라 "장착"으로]
        // 여기서 명시적으로 초기화해야, 아이템을 얻기도 전에 이미 그 방에서 맞았는지가
        // 판정에 영향을 주지 않습니다(장착한 순간부터 새로 셉니다). 방이 이미 클리어된
        // 뒤에 장착하는 경우는 별도 처리가 필요 없습니다 - RoomController.OnAnyRoomCleared는
        // 그 방의 적이 전멸하는 순간 단 한 번만 발생하므로, 클리어가 이미 끝난 뒤에 구독을
        // 시작해도 그 방의 클리어 이벤트를 다시 받을 일이 없습니다.
        tookDamageThisRoom = false;
        trackedRoom = HeadAlertRoomRules.FindCurrentRoom();
        ctx = context;
        itemIcon = FindItemIcon(context, sourceItemId);

        damageHandler = HandleDamageTaken;
        roomEnteredHandler = HandleRoomEntered;
        roomClearedHandler = HandleRoomCleared;

        if (context.Health != null) context.Health.OnDamageTaken += damageHandler;
        RoomController.OnAnyRoomEntered += roomEnteredHandler;
        RoomController.OnAnyRoomCleared += roomClearedHandler;
    }

    public void Remove(TrinketEffectContext context, string sourceItemId)
    {
        Unsubscribe();
        ctx = null;
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
        bool firstHit = !tookDamageThisRoom;
        tookDamageThisRoom = true;

        // 성공 기준이 걸린 방에서 처음 맞은 순간에만 실패를 알립니다(치명상이면 생략).
        if (!firstHit || ctx == null) return;
        if (ctx.Health != null && ctx.Health.IsDead) return;
        if (!HeadAlertRoomRules.IsSuccessCriteriaActive(trackedRoom)) return;

        PlayPopup(success: false);
    }

    private void HandleRoomCleared(RoomInstance room)
    {
        // 정적 알림이라, 플레이어가 사라졌는데(이전 플레이 등) 구독이 남아 있으면 정리만 합니다.
        if (ctx == null || ctx.PlayerObject == null) { Unsubscribe(); return; }
        if (tookDamageThisRoom) return;

        if (ctx.Resources != null) ctx.Resources.Add(BasicItemType.Credit, creditOnNoDamageClear);
        PlayPopup(success: true);
    }

    // 장착한 39번의 아이콘을 찾아 둡니다(장신구 칸에 이미 들어간 뒤 Apply가 불림 - 103번과 같은 방식).
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

    // 성공/실패 연출(순수 장식 - 문제가 생겨도 크레딧 처리에는 영향 없음)
    private void PlayPopup(bool success)
    {
        try
        {
            GameObject player = ctx?.PlayerObject;
            if (player == null) return;

            Action onStarted = null;
            if (!success && failureVfxPrefab != null)
            {
                Transform playerT = player.transform;
                GameObject prefab = failureVfxPrefab;
                float delay = failureVfxDelay, scale = failureVfxScale, lifetime = failureVfxLifetime;
                Vector3 offset = failureVfxOffsetAboveHead;
                bool follow = failureVfxFollowPlayer;
                onStarted = () => GlassCannonPenaltyVfxRunner.Spawn(playerT, prefab, delay, offset, scale, follow, lifetime);
            }

            CreditPopupSettings settings = HeadAlertRoomRules.Sanitize(success ? successPopup : failurePopup, success);
            CreditPopupDisplay.Show(player, player.transform.position, settings, itemIcon, followTarget: true, onStarted: onStarted,
                                    sound: success ? SfxId.ItemActivate : SfxId.ItemFail);
        }
        catch (Exception e)
        {
            Debug.LogError($"CreditBonusTrinketEffect: 연출 중 오류가 발생해 건너뜁니다(크레딧 처리에는 영향 없음): {e}");
        }
    }
}
