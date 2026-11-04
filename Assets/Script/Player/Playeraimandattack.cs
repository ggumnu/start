using UnityEngine;

// [사용 방법]
// 1. 플레이어 오브젝트에 이 스크립트를 추가합니다 (PlayerMovement, PlayerStats, PlayerShooting과 같은 오브젝트에).
// 2. Animator에 다음 파라미터를 추가하세요:
//    - IsAiming (Bool): 조준 중인지 여부. Base Layer는 이 값으로 AimLocomotion(조준 4방향
//      걷기 + 조준 대기) <-> NonAimLocomotion(비조준 대기 + 비조준 앞으로 걷기)를 전환하고,
//      Upper Body 레이어는 AimIdle <-> NonAimIdle 기본 포즈를 전환합니다.
//    - AimBlend (Float, 선택): 조준 여부를 0(비조준)~1(조준) 사이에서 부드럽게 바뀌는 값으로 보냅니다.
//      Upper Body의 Reloading 상태(블렌드 트리)가 이 값으로 "비조준 대기 기준 장전"과 "조준 대기 기준
//      장전" 클립을 섞습니다. 두 클립은 몸 전체(최상위 노드) 위치만 각각 비조준/조준 대기 자세에 맞춰
//      고정한 복사본이라, 장전 중 하체가 흔들리지 않고, 조준/비조준 어느 쪽에서 장전을 시작하거나
//      도중에 우클릭을 바꿔도 몸이 툭 튀지 않습니다. Animator에 이 파라미터가 없으면 보내지 않습니다.
//    - (예전의 Attack 트리거는 삭제했습니다. 사격 애니메이션은 이제 PlayerShooting이 총알이 실제로
//      나가는 연사 틱마다 Fire 상태를 처음부터 직접 재생합니다 - PlayerShooting.cs 상단 주석 참고.)
//
// [동작 방식]
// - 우클릭을 누르고 있는 동안(조준 모드): 이동은 자유롭게 하되, 캐릭터는 "마우스 커서가
//   가리키는 방향"을 바라봅니다. 하체는 이동 방향에 따라 조준 4방향 걷기 애니메이션이
//   재생됩니다(오버워치 스타일 스트레이프).
// - 우클릭을 누르지 않은 평소 상태: 캐릭터는 "이동 방향"을 바라보며, 비조준 대기/앞으로
//   걷기 애니메이션이 재생됩니다.
// - [비조준 사격 시 자동 조준] 평소 상태에서 좌클릭하면 바로 쏘지 않고 먼저 조준 자세로 바꾼 뒤,
//   조준 자세가 갖춰지면(AimBlend가 Aim Ready Blend 이상 + Aim Settle Time 경과) 발사합니다.
//   연사가 끝나고 Shot Aim Hold Time 동안 다시 쏘지 않으면 비조준 자세로 돌아갑니다.
//   순서: 비조준대기 -> 조준대기 -> 사격 -> (유지 시간) -> 비조준대기.
//   자동 조준 중에도 캐릭터는 마우스가 아니라 지금처럼 "이동 방향"을 바라보며, 조준선
//   (AimTrajectoryIndicator)은 우클릭 조준 때만 보입니다(IsAiming은 우클릭 여부만 알려줍니다).
//   Auto Aim On Fire를 끄면 예전처럼 비조준 상태에서 바로 쏩니다(허리쏘기).
// - 이 스크립트는 방향(회전)과 입력 감지만 담당하고, 실제 탄약/쿨다운/장전 판정은
//   PlayerShooting.TryFire()가 처리합니다.
[RequireComponent(typeof(PlayerMovement))]
[RequireComponent(typeof(PlayerShooting))]
public class PlayerAimAndAttack : MonoBehaviour
{
    [Header("회전 설정")]
    [Tooltip("이동 방향을 바라볼 때의 회전 속도 (평소 상태)")]
    public float moveFacingRotationSpeed = 20f;

    [Tooltip("마우스 방향을 바라볼 때의 회전 속도 (조준 상태)")]
    public float aimFacingRotationSpeed = 25f;

    [Tooltip("아이템 획득 연출 중 카메라 쪽(화면 정면)으로 돌아볼 때의 회전 속도")]
    public float pickupFacingRotationSpeed = 20f;

    [Header("장전 블렌드 (Animator AimBlend)")]
    [Tooltip("조준 여부를 0~1로 보낼 Animator Float 파라미터 이름입니다. Animator에 이 이름의 Float 파라미터가 " +
             "없으면 아무것도 하지 않습니다(오류 없음).")]
    public string aimBlendAnimatorParam = "AimBlend";

    [Tooltip("AimBlend 값이 0<->1 사이를 오가는 속도(초당)입니다. 10이면 0.1초 만에 완전히 바뀝니다 - " +
             "Animator의 AimIdle<->NonAimIdle 전이 시간과 비슷하게 맞추면 자연스럽습니다.")]
    [Min(0.01f)] public float aimBlendSpeed = 10f;

    [Header("비조준 사격 시 자동 조준")]
    [Tooltip("켜면 우클릭 없이 좌클릭했을 때 조준 자세로 바꾼 뒤 발사하고, 잠시 후 비조준 자세로 돌아갑니다. " +
             "끄면 예전처럼 비조준 자세 그대로 바로 쏩니다.")]
    public bool autoAimOnFire = true;

    [Tooltip("마지막 연사가 끝난 뒤 조준 자세를 유지하는 시간(초)입니다. 이 시간 안에 다시 쏘면 시간이 처음부터 " +
             "다시 셉니다. 너무 짧으면 연타할 때 조준/비조준이 깜빡거립니다.")]
    [Min(0f)] public float shotAimHoldTime = 0.4f;

    [Tooltip("첫 발을 쏘기 위한 AimBlend 기준값(0~1)입니다. 1이면 조준 자세로 완전히 바뀐 뒤 쏩니다.")]
    [Range(0f, 1f)] public float aimReadyBlend = 1f;

    [Tooltip("AimBlend가 기준값에 도달한 뒤 추가로 기다리는 시간(초)입니다. 팔/총구 정렬(HumanoidAimLookAt)이 " +
             "따라올 시간을 줍니다. 총구가 아직 덜 올라온 채로 쏘는 것처럼 보이면 조금 늘려 보세요.")]
    [Min(0f)] public float aimSettleTime = 0.08f;

    [Tooltip("조준 자세를 기다리는 최대 시간(초)입니다. 이 시간이 지나면 자세와 상관없이 발사합니다(안전장치).")]
    [Min(0f)] public float maxAimWaitTime = 0.4f;

    [Header("참조 (비워두면 자동으로 찾습니다)")]
    public Camera mainCamera;
    public Animator animator;

    private PlayerMovement playerMovement;
    private PlayerShooting playerShooting;

    // [무기 모드 연동] 1총/2수류탄/3해킹툴 모드를 관리하는 PlayerWeaponMode입니다. 수류탄/해킹툴이
    // 실행 중에 자동으로 붙여주기도 하므로 Awake에서 한 번만 찾지 않고 필요할 때 다시 찾습니다.
    // 없으면 이 스크립트는 무기 모드 기능이 없던 때와 100% 동일하게 동작합니다.
    private PlayerWeaponMode weaponMode;
    // isAiming: 애니메이션상 조준 자세 여부(우클릭 조준 + 자동 조준). Animator의 IsAiming/AimBlend로 보냅니다.
    // manualAiming: 우클릭을 누르고 있는지 여부. 바라보는 방향과 조준선(IsAiming 프로퍼티)은 이 값만 씁니다.
    private bool isAiming = false;
    private bool manualAiming = false;
    private float aimBlend = 0f;

    // [자동 조준 상태]
    private float autoAimTimer = 0f;     // 0보다 크면 자동 조준 자세 유지 중
    private bool pendingShot = false;    // 조준 자세가 갖춰지길 기다리는 발사 예약
    private float pendingWaitTimer = 0f; // 예약 후 흐른 시간
    private float aimReadyTimer = 0f;    // AimBlend가 기준값에 도달한 뒤 흐른 시간
    private bool animatorHasAimBlend;

    void Awake()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        if (animator == null) animator = GetComponentInChildren<Animator>();
        playerMovement = GetComponent<PlayerMovement>();
        playerShooting = GetComponent<PlayerShooting>();
        weaponMode = GetComponent<PlayerWeaponMode>();
        animatorHasAimBlend = HasFloatParameter(animator, aimBlendAnimatorParam);
    }

    void Update()
    {
        HandleAimState();
        UpdateFacingDirection();
        UpdateChargeShot();
        HandleAttackInput();
        UpdatePendingShot();
    }

    // [110번 장신구 - 충전 사격] PlayerChargeShot은 110번을 장착할 때 붙으므로 필요할 때 다시 찾습니다.
    private PlayerChargeShot chargeShot;

    PlayerChargeShot GetChargeShot()
    {
        if (chargeShot == null) chargeShot = GetComponent<PlayerChargeShot>();
        return chargeShot;
    }

    // [110번] 충전 중이면 매 프레임 진행하고, 좌클릭을 뗐을 때 발사합니다.
    // - 충전 중에는 자동 조준 자세를 유지합니다(우클릭 조준이 아니면 이동 방향을 바라보는 것은 평소와 같음).
    // - 충전 0발에서 뗌: 평소 클릭과 똑같이 처리합니다(연사 포함, 자동 조준 대기 포함).
    // - 1발 이상 충전하고 뗌: 바라보는 방향으로 강화 총알을 쏩니다.
    void UpdateChargeShot()
    {
        PlayerChargeShot charge = GetChargeShot();
        if (charge == null || !charge.IsCharging) return;

        if (autoAimOnFire && !manualAiming) autoAimTimer = shotAimHoldTime;

        PlayerChargeShot.TickResult result = charge.Tick(Input.GetMouseButton(0));
        if (result == PlayerChargeShot.TickResult.ReleasedNormal)
        {
            HandleClickFire();
        }
        else if (result == PlayerChargeShot.TickResult.ReleasedCharged)
        {
            charge.FireCharged(transform.forward);
            if (autoAimOnFire && !manualAiming) autoAimTimer = shotAimHoldTime;
        }
    }

    void HandleAimState()
    {
        manualAiming = Input.GetMouseButton(1); // 오른쪽 클릭을 누르고 있는 동안 조준 상태

        // [자동 조준] 자동 조준으로 쏜 연사가 진행되는 동안에는 유지 시간을 계속 가득 채워 두고,
        // 연사가 끝난 뒤부터 유지 시간이 줄어듭니다. 발사 예약 중에도 조준 자세를 유지합니다.
        if (autoAimTimer > 0f)
        {
            if (playerShooting != null && playerShooting.IsBursting) autoAimTimer = shotAimHoldTime;
            else autoAimTimer -= Time.deltaTime;
        }
        bool autoAiming = pendingShot || autoAimTimer > 0f;

        isAiming = manualAiming || autoAiming;
        if (animator != null) animator.SetBool("IsAiming", isAiming);

        // 장전 블렌드 트리용: 조준 여부를 0~1 사이에서 부드럽게 따라가는 값으로 보냅니다.
        aimBlend = Mathf.MoveTowards(aimBlend, isAiming ? 1f : 0f, aimBlendSpeed * Time.deltaTime);
        if (animatorHasAimBlend && animator != null) animator.SetFloat(aimBlendAnimatorParam, aimBlend);
    }

    static bool HasFloatParameter(Animator anim, string paramName)
    {
        if (anim == null || string.IsNullOrEmpty(paramName)) return false;
        if (anim.runtimeAnimatorController == null) return false;
        foreach (var p in anim.parameters)
        {
            if (p.type == AnimatorControllerParameterType.Float && p.name == paramName) return true;
        }
        return false;
    }

    void UpdateFacingDirection()
    {
        // [아이템 획득 연출] 획득 동작 중에는 이동/조준과 상관없이 항상 카메라 쪽(화면 정면)을 바라봅니다
        // (아이작 리펜던스의 아이템 들어 올리기처럼 - 형빈 확정). 연출이 끝나면 아래 원래 규칙(조준 중이면
        // 마우스 방향, 아니면 이동 방향)으로 자연스럽게 다시 돌아봅니다. 이동은 그대로 되며, 하체는
        // PlayerMovement가 바라보는 방향 기준으로 걷기 방향(앞/뒤/옆)을 계산하므로 뒷걸음/옆걸음으로 걷습니다.
        if (PlayerItemPickupPresenter.BlocksActions)
        {
            RotateTowardsCamera();
            return;
        }

        // 자동 조준 중에는 마우스가 아니라 지금처럼 이동 방향을 바라봅니다(우클릭 조준 때만 마우스 방향).
        if (manualAiming)
        {
            RotateTowardsMouse();
        }
        else
        {
            RotateTowardsMovement();
        }
    }

    // 조준 모드: 마우스 커서가 가리키는 바닥 위치 방향으로 회전
    void RotateTowardsMouse()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        Plane groundPlane = new Plane(Vector3.up, new Vector3(0f, transform.position.y, 0f));

        if (groundPlane.Raycast(ray, out float distance))
        {
            Vector3 aimPoint = ray.GetPoint(distance);
            Vector3 lookDir = aimPoint - transform.position;
            lookDir.y = 0f;

            if (lookDir.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(lookDir);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, aimFacingRotationSpeed * Time.deltaTime);
            }
        }
    }

    // 아이템 획득 연출 중: 카메라를 향하는 방향(카메라 시선의 반대, 수평 성분만)으로 회전
    void RotateTowardsCamera()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null) return;

        Vector3 toCamera = -mainCamera.transform.forward;
        toCamera.y = 0f;
        if (toCamera.sqrMagnitude < 0.001f) return;

        Quaternion targetRot = Quaternion.LookRotation(toCamera.normalized);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, pickupFacingRotationSpeed * Time.deltaTime);
    }

    // 평소 모드: 현재 이동 입력 방향으로 회전 (입력이 없으면 마지막 방향 유지)
    void RotateTowardsMovement()
    {
        Vector3 moveDir = playerMovement.GetMoveInput();
        if (moveDir.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(moveDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, moveFacingRotationSpeed * Time.deltaTime);
        }
    }

    void HandleAttackInput()
    {
        if (Input.GetMouseButtonDown(0)) // 왼쪽 클릭
        {
            // [무기 모드] 총 모드(1키)가 아니면 좌클릭은 수류탄 투척/해킹툴 전개용이라 총을
            // 쏘지 않습니다. 마지막 수류탄/해킹툴을 쓴 그 프레임에 총 모드로 자동 복귀하는
            // 경우에도, 같은 클릭으로 총까지 나가지 않도록 BlocksGunFireThisFrame이 그 프레임만큼
            // 함께 막아줍니다.
            if (weaponMode == null) weaponMode = GetComponent<PlayerWeaponMode>();
            if (weaponMode != null && weaponMode.BlocksGunFireThisFrame) return;

            // [HUD 접어보기] HUD 접기 버튼 위에서 누른 클릭은 버튼 조작용이라 총을 쏘지 않습니다.
            if (HUDClickBlocker.BlocksLeftClick) return;

            // [110번 장신구 - 충전 사격] 110번이 있고 지금 쏠 수 있으면, 누르는 순간 쏘지 않고 충전을 시작합니다(뗄 때 발사 -
            // UpdateChargeShot). 충전을 시작하지 못하면(탄약 0 등) 아래 평소 처리를 그대로 합니다.
            // (이미 자동 조준 발사 대기 중이면 그 발사가 먼저라 충전을 시작하지 않습니다.)
            PlayerChargeShot charge = GetChargeShot();
            if (!pendingShot && charge != null && charge.TryBeginCharge())
            {
                if (autoAimOnFire && !manualAiming) autoAimTimer = shotAimHoldTime;
                return;
            }

            HandleClickFire();
        }
    }

    // 평소 좌클릭 발사 처리입니다(자동 조준 대기 또는 즉시 발사). 110번 충전을 0발에서 뗐을 때도 이 처리를 씁니다.
    void HandleClickFire()
    {
        {
            // [자동 조준] 우클릭 없이 쏘면 바로 쏘지 않고 조준 자세로 바꾼 뒤 UpdatePendingShot에서 발사합니다.
            // 이미 자동 조준 자세가 갖춰진 상태(유지 시간 중 연타)에서는 기다리지 않고 바로 쏩니다.
            if (autoAimOnFire && !manualAiming && CanStartAimedShot())
            {
                if (autoAimTimer > 0f && aimBlend >= aimReadyBlend)
                {
                    FireNow(true);
                }
                else if (!pendingShot)
                {
                    pendingShot = true;
                    pendingWaitTimer = 0f;
                    aimReadyTimer = 0f;
                }
                return;
            }

            // 실제 발사 가능 여부(탄약/쿨다운/장전 중 여부)는 PlayerShooting이 판단합니다.
            // 사격 애니메이션(Fire)은 PlayerShooting이 연사 틱마다 직접 재생합니다.
            // (장전 중/탄약 0이면 여기로 와서 예전 규칙 그대로 처리됩니다 - 탄약 0이면 자동 장전 시작)
            FireNow(false);
        }
    }

    // 자동 조준 발사를 시작해도 되는지 여부입니다. 장전 중이거나 탄약이 0이면 조준 자세로 바꾸지 않고
    // 예전 규칙(TryFire가 무시하거나 자동 장전 시작)을 그대로 따릅니다.
    bool CanStartAimedShot()
    {
        if (playerShooting == null) return false;
        if (playerShooting.IsReloading) return false;
        if (playerShooting.CurrentAmmo <= 0) return false;
        if (PlayerItemPickupPresenter.BlocksActions) return false;
        return true;
    }

    // 발사 예약이 있으면 조준 자세가 갖춰질 때까지 기다렸다가 발사합니다.
    void UpdatePendingShot()
    {
        if (!pendingShot) return;

        // 기다리는 사이에 무기 모드가 바뀌었거나, 아이템 획득 연출이 시작됐거나, 장전이 시작됐으면
        // 예약을 취소합니다. (기다리는 사이에 우클릭을 누르면 예약은 그대로 발사됩니다)
        if (weaponMode == null) weaponMode = GetComponent<PlayerWeaponMode>();
        bool cancelled = (weaponMode != null && !weaponMode.IsGun) ||
                         PlayerItemPickupPresenter.BlocksActions ||
                         (playerShooting != null && playerShooting.IsReloading);
        if (cancelled)
        {
            pendingShot = false;
            return;
        }

        pendingWaitTimer += Time.deltaTime;
        if (aimBlend >= aimReadyBlend) aimReadyTimer += Time.deltaTime;

        bool ready = aimBlend >= aimReadyBlend && aimReadyTimer >= aimSettleTime;
        if (ready || pendingWaitTimer >= maxAimWaitTime)
        {
            pendingShot = false;
            FireNow(true);
        }
    }

    // 실제 발사 요청입니다. autoAim이 true(자동 조준 발사)면 발사 성공 여부와 상관없이 유지 시간을 채워
    // 조준 자세를 이어갑니다(쿨다운 때문에 이번 클릭이 무시돼도 조준 자세가 툭 끊기지 않게 하기 위함).
    void FireNow(bool autoAim)
    {
        playerShooting.TryFire(transform.forward);
        if (autoAim) autoAimTimer = shotAimHoldTime;
    }

    // 우클릭 조준 여부입니다. 조준선(AimTrajectoryIndicator)이 이 값으로 표시되므로,
    // 비조준 사격 시의 자동 조준 중에는 false입니다(조준선은 우클릭 조준 때만 보임).
    public bool IsAiming => manualAiming;
}