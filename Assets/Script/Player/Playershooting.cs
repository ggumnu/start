using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// 발사 가능 여부(공격속도 쿨다운), 탄약, 장전, 연사(버스트)를 관리하는 스크립트입니다.
// 실제 발사 "입력"은 PlayerAimAndAttack에서 감지하고, 이 스크립트의 TryFire()를 호출합니다.
//
// [장전 규칙]
// - 탄약이 0이 되면 자동으로 장전을 시작합니다(TryStartAutoReload, 형빈 확정). 연사 중이면 마지막 발이 나간 뒤,
//   110번 충전 중이면 충전이 끝난 뒤, 수류탄/해킹 모드면 총 모드로 돌아온 뒤, 아이템 획득 연출 중이면 연출이 끝난 뒤 시작합니다.
//   입장 컷씬/헬기 연출/결과 화면/사망 중에는 시작하지 않고, 진행 중이던 장전도 끊습니다(총이 숨은 채 장전 모션이 돌지 않게).
// - 다음 레벨로 넘어가면 장전 완료 상태로 채웁니다(RefillForNewLevel - GameFlowManager가 로딩 화면 뒤에서 부름).
// - 탄약을 하나라도 쓴 상태라면(가득 차지 않은 상태) R키로 수동 장전이 가능합니다.
//   단, 연사(버스트) 진행 중에는 수동 장전을 할 수 없습니다 - 연사가 끝난 뒤에만 가능합니다.
//
// [연사(멀티샷) 규칙]
// - 좌클릭 1번 = 짧은 간격(multishotInterval)을 두고 연속으로 stats.Multishot 발을 발사
// - 탄약은 클릭 1번에 1개만 소모합니다(첫 발을 쏠 때) - 연사분은 탄약을 쓰지 않습니다(형빈 확정, 원래 방식으로 복구).
//   예: 최대 탄창 5, 현재 탄약 5, 연사 2일 때 공격 1회 -> 2발 발사, 탄약 5 -> 4. 남은 탄약이 1이어도 연사분은 전부 나갑니다.
// - 이 연속 발사(버스트)가 끝나기 전까지는 새 공격 입력도, 수동 장전(R키)도 받지 않습니다.
// - 나중에 아이템으로 "동시 발사 탄환 추가"를 만들 경우, SpawnProjectile()을
//   FireBurst 코루틴 밖에서 간격 없이 여러 번 호출하면 됩니다 (아래 주석 참고).
//
// [OnProjectileSpawned - 패시브 아이템 특수효과용 훅]
// 탄환 하나가 실제로 생성될 때마다(=SpawnProjectile 호출마다) 이 이벤트가 발생합니다.
// "발사할 때마다 반응하는" 훅 등록형 특수효과 아이템(IPassiveItemEffect 구현체)이
// PlayerInventory를 통해 여기 구독을 걸어두는 방식으로 사용합니다 - 이 스크립트 자체는
// 그런 아이템이 몇 개나 있는지, 뭘 하는지 전혀 알 필요가 없습니다.
//
// [장전 애니메이션 - 장전 시간에 맞춘 재생 속도]
// 장전이 시작되는 순간 Animator에 두 값을 보냅니다(Animator에 해당 파라미터가 없으면 건너뜀).
// - IsReloading(Bool): 장전 중 true, 완료/취소 시 false. UpperBody 레이어가 이 값으로 Reload 상태에
//   들어가고 나옵니다.
// - ReloadSpeed(Float): (장전 클립 길이 / 이번 장전 시간). Reload 상태의 Speed Multiplier로 연결되어,
//   아이템으로 장전 시간이 길어지거나 짧아져도 애니메이션이 끝나는 순간 = 탄창이 채워지는 순간이
//   되도록 재생 속도를 맞춥니다. 장전 시간은 진행률 UI와 같은 CurrentReloadDuration(15번 보너스 등이
//   전부 반영된 값)을 씁니다. 클립 길이는 인스펙터의 reloadClip에서 직접 읽습니다(비어 있으면 1배속).
//
// [사격 틱 이벤트 - OnShotFired]
// 연사(FireBurst)에서 탄약 1발을 소모하는 틱마다 한 번 발생합니다(클릭 사격/연사 모두 같은 지점이라
// 버스트 간격에 정확히 맞고, 19번 양갈래처럼 한 틱에 두 발이 나가도 한 번만 발생). 상체 반동 연출
// (UpperBodyRecoil)이 구독합니다. OnProjectileSpawned와 달리 5번 분열탄 생성에는 발생하지 않습니다.
// 인자는 "이 버스트에서 다음 발까지 남은 시간(초)"입니다 - 다음 발이 있으면 MultishotInterval, 버스트의
// 마지막 발(탄약이 떨어져 끝나는 경우 포함)이나 단발이면 0. 반동 연출이 연사 간격 안에 한 번씩
// 끝나도록 압축하는 데 씁니다.
// (예전의 Fire 애니메이션 직접 재생은 상체 마스크 문제로 폐기하고 이 이벤트로 대체했습니다.)
//
// [110번 장신구 - 충전 사격]
// 좌클릭 누름/뗌은 PlayerAimAndAttack, 충전 진행은 PlayerChargeShot이 맡고, 이 스크립트는 탄약 처리와 발사만 합니다.
// - CanBeginChargeShot: 지금 충전을 시작해도 되는지(장전/연사 중 아님, 공격속도 대기 끝, 탄약 1발 이상).
// - TryConsumeAmmo / RefundAmmo: 충전용 탄약 소모(누르는 순간 1발 + 0.5초마다 1발) / 충전 0발에서 뗐을 때 1발 돌려주기.
// - FireChargedShot: 강화 총알 발사. 연사는 무시하고 한 번만 쏘며(19번 양갈래/137번 확산이면 일반 사격 한 발과 같은 각도들로 갈라짐), 탄약은 충전하면서 이미 뺐으므로 여기서는 빼지
//   않습니다. 피해 = 현재 공격력 x (1 + 충전 수 x 0.5), 사거리/크기/속도 = 현재 스탯 + 충전 수 x 가산값(110번 설정). 일반 총알과 같이
//   OnProjectileSpawned(총알 아이템 효과)와 OnShotFired(반동 연출)를 보내고, 공격속도 대기시간도 일반 클릭과 똑같이 걸립니다.
// - 충전 중 R키: 충전하며 탄약을 뺐으므로 탄창이 가득 차 있지 않아 평소처럼 장전이 시작되고, PlayerChargeShot이 장전을 보고 충전을
//   취소합니다(충전 탄약은 돌려주지 않음 - 형빈 확정).
//
// [장전 중 무기 전환 - 형빈 확정]
// 장전 중에 총 모드가 아니게 되면(2번 수류탄/3번 해킹툴) 장전을 즉시 취소합니다(탄창은 채워지지
// 않음). 총으로 돌아온 뒤 R키 또는 빈 탄창 클릭으로 처음부터 다시 장전합니다.
[RequireComponent(typeof(PlayerStats))]
public class PlayerShooting : MonoBehaviour
{
    [Header("발사 설정")]
    [Tooltip("생성할 탄환 프리팹 (임시로 3D Sphere를 프리팹화해서 사용)")]
    public GameObject projectilePrefab;

    [Tooltip("탄환이 생성될 위치 (캐릭터 손/무기 위치)")]
    public Transform firePoint;

    [Header("장전 시간 단축 보너스 (15번 패시브 연동)")]
    [Tooltip("남은 탄환이 탄창 대비 이 비율 미만일 때만 장전 시간 단축 보너스(등록된 아이템 효과)가 " +
             "적용됩니다(기본 0.5 = 50%). 탄창이 완전히 빈 상태(0)에서의 장전에는 이 값과 무관하게 " +
             "항상 보너스가 적용되지 않습니다 - 수동/자동 재장전 모두 동일합니다.")]
    public float lowAmmoReloadBonusThreshold = 0.5f;

    [Header("장전 애니메이션")]
    [Tooltip("장전 애니메이션 클립(Reloading)을 연결하세요. 이 클립의 길이를 읽어서, 장전 시간에 딱 맞도록 " +
             "Animator의 재생 속도(ReloadSpeed)를 계산합니다. 비워두면 1배속으로 재생됩니다.")]
    public AnimationClip reloadClip;
    [Tooltip("장전 중 여부를 넣을 Animator Bool 파라미터 이름입니다. 없으면 건너뜁니다.")]
    public string isReloadingAnimatorParam = "IsReloading";
    [Tooltip("장전 재생 속도 배율을 넣을 Animator Float 파라미터 이름입니다. 없으면 건너뜁니다.")]
    public string reloadSpeedAnimatorParam = "ReloadSpeed";
    [Tooltip("비워두면 플레이어 자식에서 자동으로 찾습니다.")]
    public Animator animator;

    private PlayerStats stats;
    private PlayerWeaponMode weaponMode; // 총 모드가 아닐 때 R키 재장전 차단용 (없으면 null)
    private PlayerHealth health;
    private PlayerChargeShot chargeShot; // 110번 충전 - 아이템을 얻을 때 붙을 수 있어 없으면 다시 찾음
    private int currentAmmo;
    private float fireCooldownTimer = 0f;
    private float reloadTimer = 0f;
    private float currentReloadDuration = 0f;
    private bool isReloading = false;
    private bool isBursting = false;

    public int CurrentAmmo => currentAmmo;
    public bool IsReloading => isReloading;
    public bool IsBursting => isBursting;

    // [장전 표시 UI 연동] 이번 장전에 실제로 걸리는 총 시간(15번 패시브 보너스 등이 이미
    // 전부 반영된 값)입니다. AmmoBoxDisplay/PlayerReloadIndicator 같은 진행률 UI가
    // PlayerStats.ReloadTime(보너스 반영 전 스탯 값)을 직접 읽으면 보너스가 적용된 장전과
    // 괴리가 생기므로, 반드시 이 프로퍼티를 기준으로 진행률을 계산해야 합니다.
    public float CurrentReloadDuration => currentReloadDuration;

    // 탄환이 실제로 생성된 직후(Launch() 호출 직후) 매번 호출됩니다.
    public event Action<Projectile> OnProjectileSpawned;

    // 사격 틱(탄약 1발 소모)마다 한 번 발생합니다. 인자 = 다음 발까지 남은 시간(초, 없으면 0).
    // 상체 반동 연출(UpperBodyRecoil)이 구독합니다.
    public event Action<float> OnShotFired;

    // [104번 연동] 탄약이 실제로 줄어드는 순간마다 발생합니다. 인자 = 이번에 준 발 수.
    // - 일반 사격: 한 틱(탄약 1발)마다 1 - 총알을 만들기 "직전"에 보내므로, 이 알림으로 오른 공격력이 그 총알부터 반영됩니다(형빈 확정).
    // - 110번 충전: TryConsumeAmmo로 뺄 때마다(누르는 순간 1발, 충전 1회마다 1발).
    public event Action<int> OnAmmoConsumed;

    // [104번 연동] RefundAmmo로 탄약을 실제로 돌려받은 순간 발생합니다(110번 충전 0발에서 뗐을 때). 인자 = 실제로 돌려받은 발 수.
    public event Action<int> OnAmmoRefunded;

    // [104번 연동] 장전이 시작되는 순간 발생합니다(R 수동 장전, 탄약 0 자동 장전 모두). 레벨 이동 채움(RefillForNewLevel)은 제외.
    public event Action OnReloadStarted;

    void Awake()
    {
        stats = GetComponent<PlayerStats>();
        weaponMode = GetComponent<PlayerWeaponMode>();
        health = GetComponent<PlayerHealth>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        currentAmmo = stats.MagazineSize; // 시작 시 탄창 가득 채움
    }

    void Update()
    {
        if (fireCooldownTimer > 0f) fireCooldownTimer -= Time.deltaTime;

        CancelReloadIfNotGunMode();
        CancelReloadDuringScriptedScene();
        HandleManualReloadInput();
        TryStartAutoReload();
        HandleReloadProgress();
    }

    // 입장 컷씬/헬기 연출/결과 화면 중이거나 사망했는지 - 이때는 장전 모션을 돌리지 않습니다(형빈 확정).
    bool InScriptedSceneOrDead()
    {
        if (IntroCutscene.BlocksInput || HeliExtraction.BlocksInput || GameClearUI.BlocksInput) return true;
        return health != null && health.IsDead;
    }

    void CancelReloadDuringScriptedScene()
    {
        if (isReloading && InScriptedSceneOrDead()) CancelReload();
    }

    // [자동 장전] 탄약이 0이고 지금 장전을 시작할 수 있으면 바로 시작합니다. 조건이 안 맞는 동안은 매 프레임 기다렸다가
    // 맞는 순간 시작하므로, 연사 끝/충전 끝/총 모드 복귀/획득 연출 끝/연출 종료가 모두 이 한 곳으로 처리됩니다.
    void TryStartAutoReload()
    {
        if (stats == null || isReloading || isBursting || currentAmmo > 0) return;
        if (stats.MagazineSize <= 0) return; // 채울 탄창이 없으면 장전을 무한 반복하지 않게
        if (weaponMode == null) weaponMode = GetComponent<PlayerWeaponMode>();
        if (weaponMode != null && !weaponMode.IsGun) return;
        if (PlayerItemPickupPresenter.BlocksActions) return; // R 장전과 같은 규칙 - 연출이 끝나면 시작
        if (LoadingScreen.BlocksPlayerInput) return;
        if (InScriptedSceneOrDead()) return;
        if (chargeShot == null) chargeShot = GetComponent<PlayerChargeShot>();
        if (chargeShot != null && chargeShot.IsCharging) return; // 충전 중 탄약이 0이 돼도 떼기 전엔 채우지 않음(되돌려주기 계산이 깨짐)

        StartReload();
    }

    // [레벨 이동] 다음 레벨로 넘어갈 때(로딩 화면 뒤) 장전 완료 상태로 채웁니다(형빈 확정). 장전으로 치지 않으므로
    // 장전 알림(OnReloadStarted - 104번 초기화 등)과 소리는 보내지 않습니다.
    public void RefillForNewLevel()
    {
        if (stats == null) return;
        if (isReloading) CancelReload();
        currentAmmo = Mathf.Max(currentAmmo, stats.MagazineSize);
    }

    // 장전 중에 총 모드가 아니게 되면(수류탄/해킹툴로 전환) 장전을 취소합니다(형빈 확정).
    void CancelReloadIfNotGunMode()
    {
        if (!isReloading) return;

        if (weaponMode == null) weaponMode = GetComponent<PlayerWeaponMode>();
        if (weaponMode != null && !weaponMode.IsGun)
        {
            CancelReload();
        }
    }

    void HandleManualReloadInput()
    {
        // [무기 모드] 총 모드(1키)가 아니면(수류탄/해킹툴 모드) R키로 총을 재장전할 수 없습니다
        // (기획 확정). 진행 중이던 장전은 CancelReloadIfNotGunMode()가 취소합니다.
        if (weaponMode == null) weaponMode = GetComponent<PlayerWeaponMode>();
        if (weaponMode != null && !weaponMode.IsGun) return;

        // 아이템 획득 연출(PlayerItemPickupPresenter) 중에는 새 재장전을 시작할 수 없습니다. 이미 진행 중이던
        // 장전은 끊지 않고 계속 진행합니다(형빈 확정).
        if (PlayerItemPickupPresenter.BlocksActions) return;

        // 탄약을 하나라도 쓴 상태 + 현재 장전 중이 아닐 때만 수동 장전 가능.
        // 연사(버스트) 진행 중에는 수동 장전을 막습니다 - 연사가 끝난 뒤에만 R키가 먹힙니다.
        if (Input.GetKeyDown(KeyCode.R) && !isReloading && !isBursting && currentAmmo < stats.MagazineSize)
        {
            StartReload();
        }
    }

    void HandleReloadProgress()
    {
        if (!isReloading) return;

        reloadTimer -= Time.deltaTime;
        if (reloadTimer <= 0f)
        {
            FinishReload();
        }
    }

    void StartReload()
    {
        isReloading = true;
        currentReloadDuration = ComputeReloadDuration();
        reloadTimer = currentReloadDuration;

        // 장전 애니메이션: 재생 속도를 이번 장전 시간에 맞춘 뒤 장전 상태로 들어갑니다.
        SetAnimatorFloat(reloadSpeedAnimatorParam, ComputeReloadAnimSpeed(currentReloadDuration));
        SetAnimatorBool(isReloadingAnimatorParam, true);
        // 장전 시작음은 SoundEventHooks가 아래 OnReloadStarted로 냅니다.

        OnReloadStarted?.Invoke();
    }

    // 장전을 탄창 보충 없이 중단합니다(장전 중 무기 전환 시).
    void CancelReload()
    {
        isReloading = false;
        reloadTimer = 0f;
        SetAnimatorBool(isReloadingAnimatorParam, false);
    }

    // 클립 길이 / 장전 시간 = 재생 속도 배율. 클립이 없거나 값이 이상하면 1배속입니다.
    float ComputeReloadAnimSpeed(float reloadDuration)
    {
        if (reloadClip == null || reloadClip.length <= 0f || reloadDuration <= 0.0001f) return 1f;
        return reloadClip.length / reloadDuration;
    }

    void SetAnimatorBool(string paramName, bool value)
    {
        if (HasAnimatorParam(paramName, AnimatorControllerParameterType.Bool)) animator.SetBool(paramName, value);
    }

    void SetAnimatorFloat(string paramName, float value)
    {
        if (HasAnimatorParam(paramName, AnimatorControllerParameterType.Float)) animator.SetFloat(paramName, value);
    }

    bool HasAnimatorParam(string paramName, AnimatorControllerParameterType type)
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (animator == null || string.IsNullOrEmpty(paramName)) return false;
        if (animator.runtimeAnimatorController == null) return false;
        foreach (var p in animator.parameters)
        {
            if (p.type == type && p.name == paramName) return true;
        }
        return false;
    }

    // [15번 패시브 연동] 기본 장전 시간(stats.ReloadTime, 다른 아이템 효과가 이미 전부 반영된
    // 값)에서 출발해, 탄창이 완전히 빈 상태(0)가 아니면서 남은 탄환 비율이
    // lowAmmoReloadBonusThreshold 미만일 때만 ReloadSpeedBonusOverride에 등록된 보너스를
    // 적용합니다. 수동 재장전(R)과 자동 재장전(TryFire에서 탄약 0으로 StartReload 호출) 둘 다
    // 이 메서드 하나로 모이므로, 두 경로 모두 동일한 규칙이 적용됩니다.
    float ComputeReloadDuration()
    {
        float baseTime = stats.ReloadTime;

        // 탄창이 완전히 빈 상태에서의 장전에는 보너스가 적용되지 않습니다(기획 확정 사항).
        if (currentAmmo <= 0) return baseTime;

        float ammoRatio = (float)currentAmmo / stats.MagazineSize;
        if (ammoRatio >= lowAmmoReloadBonusThreshold) return baseTime;

        float floor = stats.TryGetStatFloor(StatType.ReloadTime, out float statFloor) ? statFloor : 0f;
        return ReloadSpeedBonusOverride.Resolve(baseTime, floor);
    }

    void FinishReload()
    {
        isReloading = false;
        currentAmmo = stats.MagazineSize;
        SetAnimatorBool(isReloadingAnimatorParam, false);
        GameAudio.Play(SfxId.ReloadComplete);
    }

    // [105번 연동 - 적 처치 시 즉시 장전] 장전 시간/장전 모션 없이 탄창을 즉시 가득 채웁니다. "장전"으로 취급합니다.
    // - 탄창이 이미 가득 차 있고 장전 중도 아니면 아무 일도 하지 않고 false.
    // - 장전 중이 아니었으면 OnReloadStarted를 보냅니다(104번 초기화 등). 장전 중이었으면 이미 보냈으므로 다시 보내지 않고,
    //   남은 장전 시간과 상관없이 그 장전을 바로 끝냅니다(장전 모션도 즉시 꺼짐).
    // - 무기 모드(총/수류탄/해킹툴)와 상관없이 채웁니다.
    // - "장전 중" 상태를 거치지 않으므로 110번 충전은 끊기지 않습니다(충전 수 유지 - 새로 채운 탄약으로 계속 충전).
    public bool TryInstantReload()
    {
        if (stats == null) return false;
        if (!isReloading && currentAmmo >= stats.MagazineSize) return false;

        if (isReloading)
        {
            isReloading = false;
            reloadTimer = 0f;
            SetAnimatorBool(isReloadingAnimatorParam, false);
        }
        else
        {
            OnReloadStarted?.Invoke();
        }

        currentAmmo = Mathf.Max(currentAmmo, stats.MagazineSize);
        GameAudio.Play(SfxId.ReloadComplete); // [사운드] 105번 즉시 장전도 장전 완료음
        return true;
    }

    // PlayerAimAndAttack에서 좌클릭 시 호출합니다. direction은 캐릭터가 바라보는 방향입니다.
    // 반환값: 실제로 발사가 시작되었는지 여부
    public bool TryFire(Vector3 direction)
    {
        if (isReloading) return false;
        if (isBursting) return false;       // 이전 클릭의 연사가 끝나기 전엔 새 입력 무시
        if (fireCooldownTimer > 0f) return false;

        if (currentAmmo <= 0)
        {
            // 보통은 TryStartAutoReload가 먼저 장전을 시작해 여기 오지 않습니다. 자동 장전이 기다리는 중(획득 연출 등)에
            // 클릭하면 예전처럼 빈 탄창 소리와 함께 바로 장전합니다.
            GameAudio.Play(SfxId.GunEmpty); // [사운드] 빈 탄창 클릭(바로 이어서 장전 시작음)
            StartReload();
            return false;
        }

        fireCooldownTimer = 1f / Mathf.Max(stats.AttackSpeed, 0.01f); // 공격속도 → 다음 클릭까지 대기시간

        StartCoroutine(FireBurst(direction));
        return true;
    }

    // 클릭 1번에 대해 stats.Multishot 발을 짧은 간격으로 연속 발사합니다. 탄약은 첫 발에서만 1개 소모합니다.
    // [19번/137번] 한 발에 나갈 총알 각도 목록을 매번 새로 만들지 않도록 재사용합니다.
    private readonly List<float> shotAngles = new List<float>(SpreadShotOverride.MaxShotCount);

    IEnumerator FireBurst(Vector3 direction)
    {
        isBursting = true;
        int shotsPlanned = Mathf.Max(stats.Multishot, 1);

        for (int i = 0; i < shotsPlanned; i++)
        {
            bool firstShot = i == 0;
            if (firstShot)
            {
                if (currentAmmo <= 0) break; // 안전장치 - TryFire가 탄약을 확인하므로 정상적인 흐름에서는 도달하지 않습니다.
                // [104번] 클릭의 탄약 1발 소모를 첫 총알을 만들기 직전에 알립니다(그 총알부터 오른 공격력이 반영됨).
                OnAmmoConsumed?.Invoke(1);
            }

            // [19번 양갈래 / 137번 확산] 이번 한 발로 나갈 총알 각도들을 받아 그만큼 쏩니다(SpreadShotOverride.FillShotAngles -
            // 아무것도 없으면 0도 한 발, 19번이면 ±30도 두 발, 137번이면 -15/0/+15도 세 발, 둘 다면 19번 각 총구에서 세 발씩 6발).
            // EnemyRangedAttack의 샷건형 부채꼴 발사와 같은 계산 방식입니다. 연사(Multishot)와는 완전히 독립된 축입니다.
            // 조준선(AimTrajectoryIndicator)도 같은 함수를 써서 실제 탄도와 항상 일치합니다.
            SpreadShotOverride.FillShotAngles(shotAngles);
            for (int a = 0; a < shotAngles.Count; a++)
            {
                float angle = shotAngles[a];
                SpawnProjectile(Mathf.Approximately(angle, 0f) ? direction : Quaternion.Euler(0f, angle, 0f) * direction);
            }
            // 연사 한 발(틱)마다 한 번 - 19번 양갈래/137번 확산이어도 한 번만(반동 연출/총소리). 연사분이 남아 있으면 다음 발까지
            // MultishotInterval, 아니면 0을 함께 알립니다.
            bool hasNextShot = i < shotsPlanned - 1;
            OnShotFired?.Invoke(hasNextShot ? stats.MultishotInterval : 0f);
            // 자동 장전은 여기서 시작하지 않습니다 - 연사가 모두 끝나 isBursting이 풀린 뒤 TryStartAutoReload가 시작합니다(모션 겹침 방지).
            if (firstShot) currentAmmo -= 1;

            if (i < shotsPlanned - 1)
            {
                yield return new WaitForSeconds(stats.MultishotInterval);
            }
        }

        isBursting = false;
    }

    // 탄환 하나를 생성하고 능력치 값을 전달합니다.
    // 나중에 "동시 발사 추가 탄환" 아이템을 만들면, 이 함수를 같은 프레임에
    // 여러 번 호출(예: 좌우로 살짝 벌어진 각도로)하는 식으로 확장할 수 있습니다.
    void SpawnProjectile(Vector3 direction)
    {
        if (projectilePrefab == null)
        {
            Debug.LogWarning("PlayerShooting: projectilePrefab이 설정되지 않았습니다.");
            return;
        }

        Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position;
        GameObject bulletObj = Instantiate(projectilePrefab, spawnPos, Quaternion.LookRotation(direction));

        Projectile projectile = bulletObj.GetComponent<Projectile>();
        if (projectile != null)
        {
            // [23번 30색 물감 세트] 23번이 있으면 이 총알의 랜덤 추가 피해/색을 굴립니다(원본 탄환은 효과 배율 1).
            BulletDamageRoll? damageRoll = null;
            if (RandomBulletDamageOverride.TryRoll(1f, out BulletDamageRoll roll)) damageRoll = roll;

            projectile.Launch(direction, stats.Damage, stats.Range, stats.BulletSpeed, stats.BulletSize, damageRoll);
            OnProjectileSpawned?.Invoke(projectile);
        }
        else
        {
            Debug.LogWarning("PlayerShooting: 탄환 프리팹에 Projectile 스크립트가 없습니다.");
        }
    }

    // ---- [110번 장신구 - 충전 사격] ----

    // 지금 충전을 시작해도 되는지입니다(TryFire가 발사를 시작하는 조건과 같음 - 탄약이 0이면 false라 평소 클릭 처리로 자동 장전).
    public bool CanBeginChargeShot => !isReloading && !isBursting && fireCooldownTimer <= 0f && currentAmmo > 0;

    // 탄약을 amount만큼 뺍니다. 모자라면 빼지 않고 false.
    public bool TryConsumeAmmo(int amount)
    {
        if (amount <= 0) return true;
        if (currentAmmo < amount) return false;
        currentAmmo -= amount;
        OnAmmoConsumed?.Invoke(amount); // [104번]
        return true;
    }

    // 탄약을 amount만큼 돌려줍니다(탄창 크기를 넘지 않음).
    public void RefundAmmo(int amount)
    {
        if (amount <= 0) return;
        int before = currentAmmo;
        currentAmmo = Mathf.Min(currentAmmo + amount, Mathf.Max(stats.MagazineSize, currentAmmo));
        int refunded = currentAmmo - before;
        if (refunded > 0) OnAmmoRefunded?.Invoke(refunded); // [104번]
    }

    // 강화 총알을 direction으로 쏩니다(위 [110번 장신구 - 충전 사격] 참고). chargeCount = 충전한 발 수(1 이상).
    public void FireChargedShot(Vector3 direction, int chargeCount, ChargeShotOverride.Settings settings)
    {
        if (chargeCount <= 0 || settings == null) return;
        if (direction.sqrMagnitude < 0.0001f) direction = transform.forward;

        fireCooldownTimer = 1f / Mathf.Max(stats.AttackSpeed, 0.01f); // 일반 클릭과 같은 공격속도 대기시간

        float damage = stats.Damage * (1f + settings.damageRatioPerCharge * chargeCount);
        float range = stats.Range + settings.rangePerCharge * chargeCount;
        float size = stats.BulletSize + settings.bulletSizePerCharge * chargeCount;
        float speed = stats.BulletSpeed + settings.bulletSpeedPerCharge * chargeCount;

        // [19번 양갈래 / 137번 확산] 강화 총알도 일반 사격 한 발과 같은 각도들로 갈라집니다(19번이면 두 발 - 형빈 확정).
        // 일반 사격(FireBurst)과 같은 SpreadShotOverride.FillShotAngles를 써서 조준선/일반 사격과 항상 같은 각도입니다.
        SpreadShotOverride.FillShotAngles(shotAngles);
        for (int a = 0; a < shotAngles.Count; a++)
        {
            float angle = shotAngles[a];
            Vector3 dir = Mathf.Approximately(angle, 0f) ? direction : Quaternion.Euler(0f, angle, 0f) * direction;
            SpawnChargedProjectile(dir, damage, range, speed, size, chargeCount, settings);
        }

        // 반동 연출은 한 번(연사가 아니므로 다음 발까지 0초).
        OnShotFired?.Invoke(0f);
    }

    // 강화 총알 한 발을 만듭니다. 일반 총알(SpawnProjectile)과 같은 순서이고, 발사 직후 강화 외형을 붙인 뒤 OnProjectileSpawned를 보냅니다.
    void SpawnChargedProjectile(Vector3 direction, float damage, float range, float speed, float size, int chargeCount,
                                ChargeShotOverride.Settings settings)
    {
        if (projectilePrefab == null)
        {
            Debug.LogWarning("PlayerShooting: projectilePrefab이 설정되지 않았습니다.");
            return;
        }

        Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position;
        GameObject bulletObj = Instantiate(projectilePrefab, spawnPos, Quaternion.LookRotation(direction));

        Projectile projectile = bulletObj.GetComponent<Projectile>();
        if (projectile == null)
        {
            Debug.LogWarning("PlayerShooting: 탄환 프리팹에 Projectile 스크립트가 없습니다.");
            return;
        }

        // [23번 30색 물감 세트] 강화 총알도 일반 총알처럼 랜덤 추가 피해/색을 굴립니다(효과 배율 1).
        BulletDamageRoll? damageRoll = null;
        if (RandomBulletDamageOverride.TryRoll(1f, out BulletDamageRoll roll)) damageRoll = roll;

        projectile.Launch(direction, damage, range, speed, size, damageRoll);

        // [110번 관통] 충전 수가 관통 기준(기본 4발 = 표시 숫자 5) 이상이면 적과 벽을 관통합니다. 5번 분열탄은 관통하지 않습니다.
        if (settings.pierceChargeThreshold > 0 && chargeCount >= settings.pierceChargeThreshold) projectile.SetPiercing(true);

        ChargedBulletVisual.Attach(projectile, chargeCount, settings.bulletVisual, false);
        OnProjectileSpawned?.Invoke(projectile);
    }

    // [63번 유도 미사일] 미사일 한 발을 플레이어 총알로 만듭니다(같은 총알 프리팹 + Projectile 미사일 모드). 일반 총알처럼
    // OnProjectileSpawned를 보내 총알 아이템 효과가 붙지만, OnShotFired(반동/총소리)/탄약 소모/OnAmmoConsumed는 없습니다(사격이 아님).
    // 순서: Launch(꼬리 없음) -> 미사일 모드(훅이 IsMissile/EffectScale을 읽도록 먼저) -> 분열이면 MarkAsSplit -> 훅 -> 미사일 규칙 재적용
    // (99/120번 관통 등 훅이 켠 것을 끔). 23번 랜덤 추가 피해는 효과 배율(effectScale)만큼 줄여 굴립니다(5번 분열탄과 같은 규칙).
    // 피해/사거리/속도/크기와 목표는 HomingMissile이 정합니다. 미사일의 연기/유도는 돌려받은 총알에 HomingMissile이 붙입니다.
    public Projectile SpawnMissileProjectile(Vector3 position, Vector3 direction, float damage, float speed, float size,
                                             EnemyHealth target, float effectScale, float damageReferenceScale,
                                             int generation = 0, EnemyHealth enemyToIgnore = null,
                                             Action<Projectile> beforeHooks = null)
    {
        if (projectilePrefab == null) return null;
        if (direction.sqrMagnitude < 0.0001f) direction = transform.forward;

        GameObject bulletObj = Instantiate(projectilePrefab, position, Quaternion.LookRotation(direction));
        Projectile projectile = bulletObj.GetComponent<Projectile>();
        if (projectile == null)
        {
            Destroy(bulletObj);
            return null;
        }

        BulletDamageRoll? damageRoll = null;
        if (RandomBulletDamageOverride.TryRoll(effectScale, out BulletDamageRoll roll)) damageRoll = roll;

        // 사거리는 쓰지 않으므로(미사일은 최대 비행 시간으로 사라짐) 충분히 큰 값을 넣습니다.
        projectile.Launch(direction, damage, 1000f, speed, size, damageRoll, false);
        projectile.ConfigureAsMissile(target, effectScale, damageReferenceScale);
        if (generation > 0) projectile.MarkAsSplit(generation, effectScale, enemyToIgnore);

        beforeHooks?.Invoke(projectile);
        OnProjectileSpawned?.Invoke(projectile);
        projectile.EnforceMissileRules();
        return projectile;
    }

    // [5번 패시브 - 분열탄 생성]
    // 원본 탄환과 같은 프리팹으로 탄환 하나를 만들고, 스탯이 아니라 호출한 쪽(5번 아이템)이
    // 정한 값으로 발사합니다. 생성 후에는 원본 발사와 똑같이 OnProjectileSpawned를 발생시켜서,
    // 4번(폭발)/35번(튕김)/11번(거리 피해)/5번 자신(재분열) 같은 훅 등록형 효과가 분열탄에도
    // 자동으로 붙습니다. 탄약은 소모하지 않습니다(발사가 아니라 분열이므로).
    // Launch() 직후, OnProjectileSpawned 발생 전에 MarkAsSplit()을 호출하는 순서가 중요합니다 -
    // 구독자들이 탄환을 받자마자 세대/효과 배율을 읽을 수 있어야 하기 때문입니다.
    public Projectile SpawnSplitProjectile(Vector3 position, Vector3 direction, float damage, float range,
                                           float speed, float size, int generation, float effectScale,
                                           EnemyHealth enemyToIgnore)
    {
        if (projectilePrefab == null) return null;
        if (direction.sqrMagnitude < 0.0001f) return null;

        GameObject bulletObj = Instantiate(projectilePrefab, position, Quaternion.LookRotation(direction));

        Projectile projectile = bulletObj.GetComponent<Projectile>();
        if (projectile == null)
        {
            Destroy(bulletObj);
            return null;
        }

        // [23번 30색 물감 세트] 분열탄도 새로 굴리고, 굴린 값에 이 분열탄의 효과 배율을 곱합니다(형빈 확정 -
        // 원본 ±30, 1세대 ±15, 2세대 ±7.5). 색은 배율을 곱하기 전의 굴린 값으로 정해져 각자 다를 수 있습니다.
        BulletDamageRoll? damageRoll = null;
        if (RandomBulletDamageOverride.TryRoll(effectScale, out BulletDamageRoll roll)) damageRoll = roll;

        projectile.Launch(direction, damage, range, speed, size, damageRoll);
        projectile.MarkAsSplit(generation, effectScale, enemyToIgnore);
        OnProjectileSpawned?.Invoke(projectile);
        return projectile;
    }
}