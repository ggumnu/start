using UnityEngine;

// [110번 장신구 - 충전 사격 진행 담당]
// 110번(ChargeShotTrinketEffect)을 장착하면 플레이어에 자동으로 붙습니다(에디터에서 따로 붙일 필요 없음). 110번이 없을 때는 아무것도
// 하지 않습니다(PlayerAimAndAttack이 좌클릭을 평소대로 처리).
//
// [흐름]
// 1) 좌클릭을 누르는 순간: PlayerAimAndAttack이 TryBeginCharge()를 부릅니다. 110번 보유 + 총을 쏠 수 있는 상태(장전/연사 중 아님,
//    공격속도 대기 끝, 탄약 1발 이상, 아이템 획득 연출 중 아님)면 발사될 총알 자체의 탄약 1발을 미리 빼고 충전을 시작합니다.
//    시작하지 못하면(탄약 0 등) false를 돌려주고, PlayerAimAndAttack이 평소 클릭 처리를 합니다(탄약 0이면 자동 장전).
// 2) 누르고 있는 동안: PlayerAimAndAttack이 매 프레임 Tick(누르고 있는지)를 부릅니다. chargeInterval(기본 0.5초)마다 탄약이 남아
//    있으면 1발 빼서 1발 충전하고, 그때마다 몸 중심에서 vfx_Implosion_01을 터뜨립니다. 탄약이 없으면 더 충전하지 않습니다.
// 3) 떼는 순간:
//    - 충전 0발: 미리 뺀 1발을 돌려주고 ReleasedNormal을 돌려줍니다 → PlayerAimAndAttack이 평소 클릭과 똑같이 발사(연사 포함).
//    - 충전 1발 이상: ReleasedCharged를 돌려줍니다 → PlayerAimAndAttack이 FireCharged(방향)를 불러 강화 총알을 쏩니다.
// 4) 취소(충전에 쓴 탄약은 돌려주지 않음 - 형빈 확정): 장전 시작(R) / 총 모드가 아님(2·3키) / 아이템 획득 연출 / 사망 / 110번을 잃음.
//
// [게임 일시정지(ESC) 중]
// 시간이 멈춘 동안에는 충전 시간도 흐르지 않고, 떼는 판정도 하지 않습니다. 일시정지가 풀린 뒤 버튼이 떨어져 있으면 그때 발사합니다.
//
// [충전 수 표시]
// 1발 이상 충전되면 ChargeShotIndicator가 장전 R 표시 자리 위쪽에 110번 아이콘 + 숫자(소모한 탄약 수 = 1 + 충전 수)를 띄웁니다.
// 표시는 이 컴포넌트가 처음 붙을 때 자동으로 만듭니다.
[RequireComponent(typeof(PlayerShooting))]
public class PlayerChargeShot : MonoBehaviour
{
    public enum TickResult
    {
        StillCharging,   // 계속 누르고 있음(또는 일시정지 중)
        ReleasedNormal,  // 충전 0발에서 뗌 - 평소 클릭처럼 발사할 것
        ReleasedCharged, // 1발 이상 충전하고 뗌 - FireCharged를 부를 것
        Cancelled,       // 취소됨
    }

    private PlayerShooting shooting;
    private PlayerWeaponMode weaponMode;
    private PlayerHealth health;

    private bool isCharging;
    private int chargeCount;
    private float holdTimer;
    private ChargeShotOverride.Settings activeSettings; // 충전을 시작한 순간의 설정
    private Sprite activeIcon;

    // 지금 충전 중인지(누르는 순간 ~ 떼거나 취소될 때까지)
    public bool IsCharging => isCharging;

    // 지금까지 충전한 발 수(누르는 순간의 1발은 제외)
    public int ChargeCount => isCharging ? chargeCount : 0;

    // 충전 수 표시용: 이번 사격에 소모한 탄약 수(누르는 순간 1발 + 충전 수). 충전 중이 아니면 0.
    public int ConsumedAmmoCount => isCharging ? chargeCount + 1 : 0;

    public ChargeShotOverride.Settings ActiveSettings => activeSettings;

    // [조준선 미리보기] 1발 이상 충전 중이면 true와 함께 충전으로 늘어난 사거리/총알 크기 가산과 관통 여부를 돌려줍니다
    // (AimTrajectoryIndicator가 매 프레임 읽음). PlayerShooting.FireChargedShot과 같은 계산입니다.
    public bool TryGetAimPreview(out float rangeBonus, out float sizeBonus, out bool piercing)
    {
        rangeBonus = 0f;
        sizeBonus = 0f;
        piercing = false;
        if (!isCharging || chargeCount <= 0 || activeSettings == null) return false;

        rangeBonus = activeSettings.rangePerCharge * chargeCount;
        sizeBonus = activeSettings.bulletSizePerCharge * chargeCount;
        piercing = activeSettings.pierceChargeThreshold > 0 && chargeCount >= activeSettings.pierceChargeThreshold;
        return true;
    }
    public Sprite ActiveIcon => activeIcon;

    public static PlayerChargeShot GetOrAdd(GameObject player)
    {
        if (player == null) return null;
        var comp = player.GetComponent<PlayerChargeShot>();
        if (comp == null) comp = player.AddComponent<PlayerChargeShot>();
        return comp;
    }

    void Awake()
    {
        shooting = GetComponent<PlayerShooting>();
        weaponMode = GetComponent<PlayerWeaponMode>();
        health = GetComponent<PlayerHealth>();
        ChargeShotIndicator.EnsureExists(this);
    }

    void OnDisable()
    {
        // 플레이어가 꺼지면 충전 상태를 정리합니다(탄약은 돌려주지 않음).
        ResetCharge();
    }

    // 좌클릭을 누르는 순간 부릅니다. 충전을 시작했으면 true(이번 클릭은 뗄 때 처리), 아니면 false(평소 클릭 처리).
    public bool TryBeginCharge()
    {
        if (isCharging) return true;
        if (!ChargeShotOverride.TryGetActive(out var settings, out var icon)) return false;
        if (shooting == null || !shooting.CanBeginChargeShot) return false;
        if (PlayerItemPickupPresenter.BlocksActions) return false;
        if (health != null && health.IsDead) return false;
        if (!IsGunMode()) return false;

        // 발사될 총알 자체의 1발을 미리 뺍니다(형빈 확정 - 누르는 순간 1발 소모).
        if (!shooting.TryConsumeAmmo(1)) return false;

        // 씬이 바뀌어 표시 오브젝트가 사라졌을 수 있으니 시작할 때마다 확인합니다.
        ChargeShotIndicator.EnsureExists(this);

        isCharging = true;
        chargeCount = 0;
        holdTimer = 0f;
        activeSettings = settings;
        activeIcon = icon;
        return true;
    }

    // 충전 중 매 프레임 부릅니다. buttonHeld = 좌클릭을 아직 누르고 있는지.
    public TickResult Tick(bool buttonHeld)
    {
        if (!isCharging) return TickResult.Cancelled;

        if (ShouldCancel())
        {
            ResetCharge();
            return TickResult.Cancelled;
        }

        // 일시정지 중에는 충전도, 떼는 판정도 하지 않습니다.
        if (Time.timeScale <= 0f) return TickResult.StillCharging;

        if (!buttonHeld)
        {
            if (chargeCount <= 0)
            {
                // 충전 0발: 미리 뺀 1발을 돌려주고 평소 클릭처럼 발사하게 합니다.
                shooting.RefundAmmo(1);
                ResetCharge();
                return TickResult.ReleasedNormal;
            }
            return TickResult.ReleasedCharged;
        }

        // 누르고 있는 동안 chargeInterval마다 탄약 1발 → 충전 1발.
        float interval = Mathf.Max(activeSettings != null ? activeSettings.chargeInterval : 0.5f, 0.05f);
        holdTimer += Time.deltaTime;
        while (holdTimer >= interval)
        {
            if (!shooting.TryConsumeAmmo(1))
            {
                // 탄약을 다 썼습니다 - 더 충전하지 않고 타이머만 묶어 둡니다.
                holdTimer = interval;
                break;
            }

            holdTimer -= interval;
            chargeCount++;
            GameAudio.Play(SfxId.ChargeShotCharge);
            SpawnImplosion();
        }

        return TickResult.StillCharging;
    }

    // ReleasedCharged를 받은 뒤 부릅니다. 강화 총알을 direction으로 쏘고 충전 상태를 정리합니다.
    public void FireCharged(Vector3 direction)
    {
        if (!isCharging) return;

        int charges = chargeCount;
        var settings = activeSettings;
        ResetCharge();

        if (charges > 0 && settings != null && shooting != null)
        {
            // [사운드] 강화 총알도 사격 알림(OnShotFired)을 보내므로 같은 프레임의 일반 사격 소리는 막고 전용 소리만 냅니다.
            GameAudio.SuppressThisFrame(SfxId.GunShot);
            GameAudio.Play(SfxId.ChargeShotFire);
            shooting.FireChargedShot(direction, charges, settings);
        }
    }

    // 충전을 완전히 취소합니다(탄약은 돌려주지 않음). 외부에서 강제로 끊을 때 씁니다.
    public void CancelCharge()
    {
        ResetCharge();
    }

    bool ShouldCancel()
    {
        if (!ChargeShotOverride.IsActive) return true;                 // 110번을 잃음(교체/버리기)
        if (shooting == null || shooting.IsReloading) return true;     // 장전 시작(R 등)
        if (!IsGunMode()) return true;                                 // 수류탄/해킹툴로 전환
        if (PlayerItemPickupPresenter.BlocksActions) return true;      // 아이템 획득 연출
        if (health != null && health.IsDead) return true;              // 사망
        return false;
    }

    bool IsGunMode()
    {
        if (weaponMode == null) weaponMode = GetComponent<PlayerWeaponMode>();
        return weaponMode == null || weaponMode.IsGun;
    }

    void ResetCharge()
    {
        isCharging = false;
        chargeCount = 0;
        holdTimer = 0f;
        activeSettings = null;
        activeIcon = null;
    }

    // 1발 충전될 때마다 플레이어 몸 중심에서 충전 이펙트(vfx_Implosion_01)를 한 번 터뜨립니다.
    void SpawnImplosion()
    {
        var s = activeSettings;
        if (s == null) return;

        // [관통 충전 이펙트] 관통 기준 이상으로 충전됐으면 관통 충전 이펙트(비어 있으면 평소 이펙트)를 씁니다.
        bool piercing = s.pierceChargeThreshold > 0 && chargeCount >= s.pierceChargeThreshold;
        GameObject prefab = piercing && s.piercingImplosionPrefab != null ? s.piercingImplosionPrefab : s.implosionPrefab;
        if (prefab == null) return;

        Vector3 pos = transform.position + s.implosionOffset;
        GameObject vfx = Instantiate(prefab, pos, Quaternion.identity);
        if (s.implosionFollowPlayer) vfx.transform.SetParent(transform, worldPositionStays: true);
        ApplyVfxScale(vfx, s.implosionScale);
        Destroy(vfx, Mathf.Max(s.implosionLifetime, 0.1f));
    }

    // 이펙트 크기를 배율만큼 조절합니다(수류탄 폭발 이펙트와 같은 방식 - 파티클 시작 크기/속도에 직접 곱함).
    static void ApplyVfxScale(GameObject instance, float multiplier)
    {
        if (instance == null || Mathf.Approximately(multiplier, 1f)) return;

        var systems = instance.GetComponentsInChildren<ParticleSystem>(true);
        if (systems.Length == 0)
        {
            instance.transform.localScale *= multiplier;
            return;
        }

        for (int i = 0; i < systems.Length; i++)
        {
            var main = systems[i].main;
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
