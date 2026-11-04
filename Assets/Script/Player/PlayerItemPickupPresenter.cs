using System.Collections.Generic;
using UnityEngine;

// [아이템 획득 연출 - 아이작 리펜던스식 "아이템 들어 올리기"]
// 보물상자(동아리방 ItemPedestal / 일반방·보스방 TreasureChest)에서 플레이어 아이템(패시브/액티브/
// 장신구/일회용/음식)을 주우면, 플레이어가 상체 획득 동작을 하면서 머리 위에 방금 얻은 아이템을
// 자랑스럽게 보여줍니다. 기본 아이템(재화) 묶음은 연출이 없습니다(ItemPickupEvents 참고).
//
// [에디터 설정]
// 플레이어 프리팹(Reisa_Player_Fixed) 루트에 Add Component로 추가하세요. 없으면 연출 없이 기존과 100%
// 동일하게 동작합니다. Pickup Clip에 획득 애니메이션 클립(ItemPickup_RootFixed)을 연결하면 연출 시간이
// 그 클립 길이로 자동 설정되고(PlayerShooting.reloadClip과 같은 방식), Flash Shader에는
// MobArchive/SpriteFlash 셰이더를 연결하세요(비워두면 이름으로 찾지만, 실제 빌드(exe)에서는 연결해둬야
// 셰이더가 빌드에 포함됩니다).
//
// [연출 순서]
// 1) 획득 순간: Animator에 Trigger "ItemPickup"(시작/재시작) + Bool "IsPickingUp" = true. 손에 든 총을
//    숨기고(PlayerWeaponMode), 공격/아이템 사용을 막습니다(BlocksActions).
// 2) 머리 위 아이콘: 발밑 기준 iconHeight 높이에, 상자 아이콘과 같은 크기/각도로 뜨고 같은 빛
//    이펙트(ItemGlowSpawner - 등급 색 + 흩날리는 입자)가 붙습니다.
//    - 나타남(appearDuration): 살짝 아래에서 떠오르며 투명 -> 흰색 -> 원래 색.
//    - 사라짐(disappearDuration): 연출이 끝나는 시점에 맞춰 원래 색 -> 흰색 -> 투명, 머리 아래로 가라앉음.
//    - 빛 이펙트도 아이콘이 보이는 정도에 맞춰 커졌다가 줄어듭니다.
// 3) 연출 시간이 끝나면 전부 원래대로(Bool false, 총 다시 보이기, 잠금 해제).
//    [형빈 확정] 연출이 끝나면 직전 무기 모드와 상관없이 항상 1번(기본 총) 모드로 돌아옵니다. 그래서
//    Animator의 ItemPickup 상태에서 나가는 전이는 총 쪽(AimIdle/NonAimIdle/Reloading)만 있으면 되고,
//    Grenade_Idle/Hack_Idle로 가는 전이는 필요 없습니다(Bool을 끄기 전에 WeaponMode를 0으로 먼저 바꿈).
//
// [즉시 종료되는 경우]
// - 다른 방에 들어갔을 때(RoomController.OnAnyRoomEntered) - 획득한 방에서만 보이도록(기획 확정).
// - 사망 등으로 이 컴포넌트가 꺼질 때(PlayerDeathHandler가 끔).
// [연출 중 또 주웠을 때] 이전 아이콘을 바로 치우고 새 아이템으로 처음부터 다시 시작합니다.
//
// [머리 위 알림이 먼저 - 형빈 확정]
// 플레이어 머리 위 알림(CreditPopupDisplay - 72/103/106/111/39번, 일회용/음식 사용 등)이 줄 서 있거나 떠오르는 중에 아이템을 얻으면,
// 획득 연출은 알림이 사라지기 시작할 때까지 기다렸다가 시작합니다(얻은 아이템 자체는 이미 들어와 있고, 기다리는 동안에는 행동을 막지 않음).
// 기다리는 중에 또 얻은 아이템은 그 뒤에 순서대로 이어집니다. 기다리던 연출은 다른 방에 들어가면 건너뜁니다.
// 반대로 획득 연출 중에 생긴 알림은 이 연출이 끝난 뒤에 나옵니다(CreditPopupDisplay가 IsPresenting을 확인).
//
// [연출 중 막는 것 - 기획 확정]
// 총 사격, 수류탄 투척, 해킹툴 전개, 무기 전환(1/2/3), 새 재장전(R), 액티브(Space)/일회용(Q)/음식(E) 사용.
// 이동은 됩니다. 이미 진행 중이던 장전은 끊지 않고 계속 진행합니다. 각 스크립트는 BlocksActions만
// 확인합니다.
[DisallowMultipleComponent]
public class PlayerItemPickupPresenter : MonoBehaviour
{
    [Header("연출 시간")]
    [Tooltip("획득 애니메이션 클립(ItemPickup_RootFixed)입니다. 연결하면 연출 시간 = 클립 길이 / Animation Speed로 " +
             "자동 계산됩니다. 비워두면 아래 Fallback Duration을 씁니다.")]
    public AnimationClip pickupClip;

    [Tooltip("Animator의 ItemPickup 상태 Speed 값과 똑같이 맞추세요(배속을 바꿨다면 그 값). 연출 시간 계산에만 씁니다.")]
    [Min(0.01f)] public float animationSpeed = 1f;

    [Tooltip("Pickup Clip이 비어 있을 때 쓰는 연출 시간(초)입니다.")]
    [Min(0.1f)] public float fallbackDuration = 1.5f;

    [Header("Animator 파라미터 (없으면 건너뜀)")]
    [Tooltip("연출을 시작/재시작할 때 보내는 Trigger 이름입니다. Animator의 Any State -> ItemPickup 전이 조건으로 씁니다.")]
    public string pickupTrigger = "ItemPickup";
    [Tooltip("연출 중 true인 Bool 이름입니다. ItemPickup 상태에서 빠져나가는 전이 조건(false)으로 씁니다.")]
    public string pickingUpBool = "IsPickingUp";

    [Header("머리 위 아이콘")]
    [Tooltip("플레이어 발밑 기준 아이콘 높이(m)입니다(애니메이션과 무관하게 고정).")]
    public float iconHeight = 1.6f;
    [Tooltip("아이콘 스케일입니다. 상자 위 아이콘과 같은 0.05가 기본값입니다.")]
    public Vector3 iconScale = new Vector3(0.05f, 0.05f, 0.05f);
    [Tooltip("아이콘이 카메라 정면을 보도록 하는 고정 회전값입니다(상자 아이콘과 같은 값 - 카메라 각도가 바뀌면 함께 조정).")]
    public Vector3 iconFixedEulerAngles = new Vector3(55f, -45f, 0f);
    [Tooltip("아이콘 Sorting Order입니다. 빛 이펙트는 이보다 1 낮게 그려집니다.")]
    public int iconSortingOrder = 10;

    [Header("나타남 (투명 -> 흰색 -> 원래 색, 아래에서 떠오름)")]
    [Min(0.01f)] public float appearDuration = 0.35f;
    [Tooltip("나타날 때 이만큼(m) 아래에서 시작해 제자리로 떠오릅니다.")]
    public float appearRiseDistance = 0.3f;

    [Header("사라짐 (원래 색 -> 흰색 -> 투명, 아래로 가라앉음)")]
    [Min(0.01f)] public float disappearDuration = 0.35f;
    [Tooltip("사라지면서 이만큼(m) 아래로 가라앉습니다.")]
    public float disappearSinkDistance = 0.4f;

    [Header("흰색 플래시 셰이더")]
    [Tooltip("MobArchive/SpriteFlash 셰이더를 연결하세요. 비워두면 이름으로 찾습니다(실제 빌드에서는 연결 필요).")]
    public Shader flashShader;

    [Header("참조 (비워두면 자동으로 찾습니다)")]
    public Animator animator;

    // ---- 다른 스크립트가 읽는 상태 ----
    // 연출 중이면 true - 총/수류탄/해킹/무기 전환/재장전/액티브·일회용·음식 사용을 막는 데 씁니다.
    public static bool BlocksActions => activeInstance != null && activeInstance.isPresenting;
    public bool IsPresenting => isPresenting;

    private static PlayerItemPickupPresenter activeInstance;

    private bool isPresenting;
    // 머리 위 알림이 끝나기를 기다리는 획득 연출들(먼저 얻은 것부터)과 그 아이템을 얻은 프레임입니다.
    private readonly List<KeyValuePair<ItemDefinition, int>> waitingPickups = new List<KeyValuePair<ItemDefinition, int>>();
    private float timer;
    private float duration;
    private ItemDefinition currentItem;

    private SpriteRenderer iconRenderer;
    private Material iconMaterial;
    private Transform glowRoot;
    private Vector3 glowBaseScale;

    private PlayerWeaponMode weaponMode;
    private bool animatorHasTrigger;
    private bool animatorHasBool;

    void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
        weaponMode = GetComponent<PlayerWeaponMode>();
        animatorHasTrigger = HasParameter(animator, pickupTrigger, AnimatorControllerParameterType.Trigger);
        animatorHasBool = HasParameter(animator, pickingUpBool, AnimatorControllerParameterType.Bool);
    }

    void OnEnable()
    {
        activeInstance = this;
        ItemPickupEvents.OnPlayerItemPickedUp += HandleItemPickedUp;
        RoomController.OnAnyRoomEntered += HandleRoomEntered;
    }

    void OnDisable()
    {
        ItemPickupEvents.OnPlayerItemPickedUp -= HandleItemPickedUp;
        RoomController.OnAnyRoomEntered -= HandleRoomEntered;
        waitingPickups.Clear();
        EndPresentation();
        if (activeInstance == this) activeInstance = null;
    }

    void HandleRoomEntered(RoomInstance room)
    {
        // 다른 방으로 넘어가면 즉시 종료합니다(획득한 방에서만 보이도록). 기다리던 연출도 건너뜁니다(이번 프레임에 얻은 것은 남김).
        int frame = Time.frameCount;
        waitingPickups.RemoveAll(w => w.Value < frame);
        if (isPresenting) EndPresentation();
    }

    void HandleItemPickedUp(ItemDefinition item)
    {
        if (item == null) return;

        // [머리 위 알림이 먼저] 이미 기다리는 연출이 있거나, 연출 중이 아닌데 알림이 나오는 중이면 줄을 섭니다.
        if (waitingPickups.Count > 0 || (!isPresenting && IsHeadAlertBusy()))
        {
            waitingPickups.Add(new KeyValuePair<ItemDefinition, int>(item, Time.frameCount));
            return;
        }

        StartPresentation(item);
    }

    bool IsHeadAlertBusy()
    {
        try
        {
            return CreditPopupDisplay.IsBusyFor(gameObject);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"PlayerItemPickupPresenter: 머리 위 알림 확인 중 오류가 발생해 기다리지 않습니다: {e}");
            return false;
        }
    }

    void StartPresentation(ItemDefinition item)
    {
        // 연출 중 또 주웠으면 이전 아이콘을 바로 치우고 처음부터 다시 시작합니다.
        DestroyIcon();

        currentItem = item;
        duration = pickupClip != null ? pickupClip.length / Mathf.Max(animationSpeed, 0.01f) : fallbackDuration;
        duration = Mathf.Max(duration, 0.1f);
        timer = 0f;
        isPresenting = true;

        CreateIcon(item);

        if (animator != null)
        {
            if (animatorHasBool) animator.SetBool(pickingUpBool, true);
            if (animatorHasTrigger) animator.SetTrigger(pickupTrigger);
        }

        RefreshGunVisual();
        UpdateIconVisual();
    }

    void Update()
    {
        if (!isPresenting)
        {
            // 기다리던 획득 연출: 머리 위 알림이 사라지기 시작하면 다음 것을 시작합니다.
            if (waitingPickups.Count > 0 && !IsHeadAlertBusy())
            {
                var next = waitingPickups[0].Key;
                waitingPickups.RemoveAt(0);
                if (next != null) StartPresentation(next);
            }
            return;
        }

        timer += Time.deltaTime;
        if (timer >= duration)
        {
            EndPresentation();
        }
    }

    void LateUpdate()
    {
        if (!isPresenting) return;
        UpdateIconVisual();
    }

    // 연출을 끝내고 전부 원래대로 돌립니다(정상 종료/방 이동/비활성화 공통). 여러 번 호출해도 안전합니다.
    void EndPresentation()
    {
        bool wasPresenting = isPresenting;
        isPresenting = false;
        currentItem = null;
        DestroyIcon();

        // 연출이 끝나면(정상 종료/방 이동 모두) 항상 기본 총 모드로 돌아갑니다(형빈 확정). Animator가
        // ItemPickup에서 나가는 순간 WeaponMode가 이미 0이도록, Bool을 끄기 전에 먼저 바꿉니다.
        if (wasPresenting)
        {
            if (weaponMode == null) weaponMode = GetComponent<PlayerWeaponMode>();
            if (weaponMode != null) weaponMode.TrySwitch(PlayerWeapon.Gun);
        }

        if (animator != null)
        {
            if (animatorHasBool) animator.SetBool(pickingUpBool, false);
            if (animatorHasTrigger) animator.ResetTrigger(pickupTrigger);
        }

        if (wasPresenting) RefreshGunVisual();
    }

    void RefreshGunVisual()
    {
        if (weaponMode == null) weaponMode = GetComponent<PlayerWeaponMode>();
        if (weaponMode != null) weaponMode.RefreshModeVisuals();
    }

    void CreateIcon(ItemDefinition item)
    {
        var go = new GameObject("PickupIcon (Held Item)");
        iconRenderer = go.AddComponent<SpriteRenderer>();
        iconRenderer.sprite = item.icon;
        iconRenderer.sortingOrder = iconSortingOrder;
        go.transform.rotation = Quaternion.Euler(iconFixedEulerAngles);
        go.transform.localScale = iconScale;
        go.transform.position = transform.position + Vector3.up * (iconHeight - appearRiseDistance);

        Shader shader = flashShader != null ? flashShader : Shader.Find("MobArchive/SpriteFlash");
        if (shader != null)
        {
            iconMaterial = new Material(shader);
            iconRenderer.sharedMaterial = iconMaterial;
        }
        else
        {
            Debug.LogWarning("PlayerItemPickupPresenter: MobArchive/SpriteFlash 셰이더를 찾지 못해 흰색 플래시 없이 표시합니다.");
        }

        glowRoot = null;
    }

    void DestroyIcon()
    {
        if (iconRenderer != null) Destroy(iconRenderer.gameObject);
        iconRenderer = null;
        glowRoot = null;
        if (iconMaterial != null) Destroy(iconMaterial);
        iconMaterial = null;
    }

    // 지금 시간에 맞춰 아이콘 위치/투명도/흰색 정도와 빛 이펙트 크기를 갱신합니다.
    void UpdateIconVisual()
    {
        if (iconRenderer == null) return;

        float appear = Mathf.Min(appearDuration, duration * 0.5f);
        float disappear = Mathf.Min(disappearDuration, duration * 0.5f);
        float disappearStart = duration - disappear;

        float alpha = 1f;
        float flash = 0f;
        float heightOffset = 0f;

        if (timer < appear)
        {
            // 앞 절반: 투명 -> 흰색(불투명), 뒤 절반: 흰색 -> 원래 색. 아래에서 제자리로 떠오름.
            float t = Mathf.Clamp01(timer / appear);
            if (t < 0.5f) { alpha = t / 0.5f; flash = 1f; }
            else { alpha = 1f; flash = 1f - (t - 0.5f) / 0.5f; }
            heightOffset = -appearRiseDistance * (1f - EaseOut(t));
        }
        else if (timer >= disappearStart)
        {
            // 앞 절반: 원래 색 -> 흰색, 뒤 절반: 흰색 -> 투명. 아래로 가라앉음.
            float t = Mathf.Clamp01((timer - disappearStart) / disappear);
            if (t < 0.5f) { alpha = 1f; flash = t / 0.5f; }
            else { alpha = 1f - (t - 0.5f) / 0.5f; flash = 1f; }
            heightOffset = -disappearSinkDistance * EaseIn(t);
        }

        Transform iconT = iconRenderer.transform;
        iconT.position = transform.position + Vector3.up * (iconHeight + heightOffset);
        iconT.rotation = Quaternion.Euler(iconFixedEulerAngles);

        Color c = iconRenderer.color;
        c.a = alpha;
        iconRenderer.color = c;
        if (iconMaterial != null) iconMaterial.SetFloat("_FlashAmount", flash);

        // 빛 이펙트(상자 아이콘과 동일 - ItemGlowSpawner)를 붙이고, 보이는 정도에 맞춰 크기를 조절합니다.
        ItemGlowSpawner.Sync(iconRenderer, currentItem);
        UpdateGlowScale(alpha);
    }

    void UpdateGlowScale(float visibility)
    {
        var marker = iconRenderer.GetComponentInChildren<ItemGlowMarker>(true);
        if (marker == null) return;

        // 빛이 새로 만들어졌으면(처음 또는 설정 변경으로 재생성) 원래 크기를 다시 기억합니다.
        if (glowRoot != marker.transform)
        {
            glowRoot = marker.transform;
            glowBaseScale = glowRoot.localScale;
        }
        glowRoot.localScale = glowBaseScale * Mathf.Clamp01(visibility);
    }

    static float EaseOut(float t) { return 1f - (1f - t) * (1f - t); }
    static float EaseIn(float t) { return t * t; }

    static bool HasParameter(Animator anim, string paramName, AnimatorControllerParameterType type)
    {
        if (anim == null || string.IsNullOrEmpty(paramName)) return false;
        if (anim.runtimeAnimatorController == null) return false;
        foreach (var p in anim.parameters)
        {
            if (p.type == type && p.name == paramName) return true;
        }
        return false;
    }
}
