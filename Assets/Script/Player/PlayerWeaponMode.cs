using System;
using UnityEngine;

// 플레이어의 "지금 들고 있는 무기 모드"(1 총 / 2 수류탄 / 3 해킹툴)를 한 곳에서 관리합니다.
//
// [왜 따로 만들었나]
// 처음에는 1/2키 전환이 PlayerGrenadeThrower 안에 있었는데, 3번(해킹툴)이 생기면서 수류탄
// 스크립트가 해킹툴까지 알아야 하고, 총 사격/재장전/총 조준선도 "수류탄 모드인지, 해킹 모드인지"를
// 각각 따로 확인해야 하는 구조가 되어버렸습니다. 이제는:
// - 키 입력, 현재 모드, "자원이 0이면 전환 불가", "다 쓰면 총으로 자동 복귀", "그 클릭으로 총이
//   같이 나가지 않게 막기"를 전부 이 컴포넌트가 맡습니다.
// - 수류탄(PlayerGrenadeThrower)/해킹툴(PlayerHackTool)은 "지금 내 모드인가?"만 물어보고, 다
//   쓰면 ReturnToGunAfterUse()로 총 복귀만 요청합니다.
// - 총 사격(PlayerAimAndAttack)/재장전(PlayerShooting)/총 조준선(AimTrajectoryIndicator)은
//   "지금 총 모드인가?"만 확인합니다. 나중에 4번 무기가 생겨도 이 세 파일은 고칠 필요가
//   없습니다 - 여기 enum과 ResourceFor()/HasHandler()에만 추가하면 됩니다.
//
// [에디터 설정]
// Player 오브젝트에 Add Component로 추가하면 됩니다. 추가하지 않아도 PlayerGrenadeThrower/
// PlayerHackTool이 실행 시 자동으로 붙여주므로 동작에는 문제가 없습니다(키를 바꾸고 싶을 때만
// 직접 추가해서 인스펙터에서 조절하세요).
//
// [애니메이션 연동 - 수류탄 애니메이션 적용 시 추가]
// - 모드가 바뀔 때마다 Animator의 Int 파라미터 weaponModeAnimatorParam(기본 "WeaponMode")에 현재 모드
//   번호를 넣습니다(0=총, 1=수류탄, 2=해킹툴 - 아래 enum 순서와 같음). Animator의 UpperBody 레이어가
//   이 값으로 총 대기 <-> 수류탄 대기 상태를 오갑니다. Animator에 이 파라미터가 없으면 건너뜁니다.
//   나중에 해킹툴 애니메이션도 Animator에서 "WeaponMode == 2" 조건만 쓰면 되도록 숫자 하나로 통일했습니다.
// - gunModel(손에 든 총 모델)을 연결해두면, hideGunInGrenadeMode/hideGunInHackMode가 켜진 모드에서는
//   총 모델 오브젝트를 꺼서 숨기고 총 모드로 돌아오면 다시 켭니다. Renderer가 아니라 오브젝트 자체를
//   끄는 이유: 피격 무적 점멸(PlayerHitFlash)이 Renderer를 껐다 켰다 하므로, Renderer만 끄면 점멸 중에
//   숨긴 총이 다시 보이게 됩니다. 총구(FirePoint)는 총 모델의 자식이 아니라 플레이어 바로 아래에 있어서
//   총 모델을 꺼도 영향이 없습니다.
// - [총 숨기기 - 형빈 확정, 등에 메기 대체] 수류탄/해킹툴 모드, 비전투 뛰기 모션 동안(hideGunWhileRunning), 아이템 획득 연출 중에는
//   총을 숨깁니다(블루 아카이브처럼 연출상 사라졌다 나타남). 등에 메던 기능은 가방(몸통 후면 악세서리)과 겹쳐서 삭제했습니다.
//   표시 여부는 매 프레임(LateUpdate) 다시 계산하므로 뛰기가 끝나는 순간 바로 다시 보입니다. 헬기 연출은 이 컴포넌트를 끄고
//   HeliExtraction이 직접 숨겼다가 되돌립니다(gunModel 칸을 같이 씀).
// - 수류탄을 던지는 동작 중(PlayerGrenadeThrower.IsThrowing)에는 1/2/3키 전환과 "자원 0이면 총으로
//   복귀"를 잠시 미룹니다. 던지는 순간 마지막 수류탄이 소모되어 바로 총 모드로 돌아가버리면 투척
//   애니메이션이 중간에 끊기기 때문입니다. 마지막 수류탄의 총 복귀는 투척 동작이 끝난 뒤
//   PlayerGrenadeThrower가 ReturnToGunAfterUse()로 요청합니다.
// - [투척 캔슬 - 형빈 확정] 단, 수류탄이 이미 손을 떠난 뒤(PlayerGrenadeThrower.CanInterruptThrow)에는
//   1번(총)/3번(해킹툴)을 누르면 투척 동작을 즉시 끊고(InterruptThrow) 그 모드로 바로 바꿉니다.
//   손을 떠나기 전(releaseDelay 동안)에는 지금처럼 모든 전환 키를 무시합니다. 끊으면 투척 대기시간도
//   함께 사라지므로, 1->2를 빠르게 누르면 바로 다음 수류탄을 던질 수 있습니다(의도된 동작).
// - [해킹툴 애니메이션 적용 시 추가] 해킹툴 사용 동작 중(PlayerHackTool.IsDeploying)에도 수류탄과 똑같이
//   전환/자동 총 복귀를 미룹니다. 효과가 발동된 뒤(PlayerHackTool.CanInterruptDeploy)에는 1번(총)/2번
//   (수류탄)으로 남은 동작을 즉시 끊고(InterruptDeploy) 그 모드로 바꿉니다. 발동 전에는 모든 전환 키를
//   무시합니다(형빈 확정 - 수류탄 투척 캔슬과 동일한 규칙).
// - [아이템 획득 연출 시 추가] 보물상자에서 플레이어 아이템을 주워 획득 연출(PlayerItemPickupPresenter)이
//   진행되는 동안에는 1/2/3 전환과 "자원 0이면 총 복귀"를 모두 미루고, 총 사격도 막으며(BlocksGunFireThisFrame),
//   손에 든 총 모델을 숨깁니다(어느 모드든). 연출이 끝나면 PlayerItemPickupPresenter가 RefreshModeVisuals()를
//   호출해 원래 모드의 표시로 돌아옵니다.
public enum PlayerWeapon
{
    Gun,
    Grenade,
    Hack,
}

// 키 입력을 수류탄/해킹툴의 Update보다 먼저 처리하도록 실행 순서를 앞당깁니다.
[DefaultExecutionOrder(-50)]
[DisallowMultipleComponent]
public class PlayerWeaponMode : MonoBehaviour
{
    [Header("키 설정")]
    public KeyCode gunKey = KeyCode.Alpha1;
    public KeyCode grenadeKey = KeyCode.Alpha2;
    public KeyCode hackKey = KeyCode.Alpha3;

    [Header("참조 (비워두면 자동으로 찾습니다)")]
    public PlayerResources resources;
    public Animator animator;

    [Header("애니메이션")]
    [Tooltip("현재 무기 모드 번호(0=총, 1=수류탄, 2=해킹툴)를 넣을 Animator Int 파라미터 이름입니다. " +
             "Animator에 이 이름의 Int 파라미터가 없으면 건너뜁니다.")]
    public string weaponModeAnimatorParam = "WeaponMode";

    [Header("총 모델 표시")]
    [Tooltip("손에 든 총 모델 오브젝트(예: Reisa_gun_Fixed)를 연결하세요. 비워두면 총 모델은 항상 보입니다.")]
    public GameObject gunModel;
    [Tooltip("수류탄 모드(2번)에서 총 모델을 숨길지 여부입니다.")]
    public bool hideGunInGrenadeMode = true;
    [Tooltip("해킹툴 모드(3번)에서 총 모델을 숨길지 여부입니다.")]
    public bool hideGunInHackMode = true;
    [Tooltip("비전투 뛰기 모션(PlayerMovement.IsRunAnimating) 동안 총을 숨깁니다. 뛰기가 끝나면(멈춤/사격/조준/장전/피격) 바로 다시 보입니다.")]
    public bool hideGunWhileRunning = true;

    // 모드가 바뀔 때마다 새 모드를 알려줍니다(나중에 무기 모드 HUD가 생기면 구독용).
    public event Action<PlayerWeapon> OnModeChanged;

    private PlayerWeapon current = PlayerWeapon.Gun;
    private int gunFireBlockedFrame = -1;

    // [115번 때 추가] 지금 모드(수류탄/해킹툴)의 직전 자원 개수입니다. "0이 되는 순간"에만 총으로 돌려보내기 위해 씁니다.
    private int lastModeAmount;

    public PlayerWeapon Current => current;
    public bool IsGun => current == PlayerWeapon.Gun;
    public bool IsGrenade => current == PlayerWeapon.Grenade;
    public bool IsHack => current == PlayerWeapon.Hack;

    // PlayerAimAndAttack이 좌클릭 총 사격을 막을지 판단할 때 씁니다. 총 모드가 아니거나, 이번
    // 프레임에 마지막 수류탄/해킹툴을 써서 총 모드로 자동 복귀한 경우(같은 클릭으로 총이 나가면
    // 안 됨) true입니다.
    public bool BlocksGunFireThisFrame => current != PlayerWeapon.Gun || gunFireBlockedFrame == Time.frameCount ||
                                          PlayerItemPickupPresenter.BlocksActions;

    // 수류탄/해킹툴 컴포넌트가 없는 경우를 대비해 PlayerGrenadeThrower/PlayerHackTool이 이 함수로
    // 자동으로 붙입니다.
    public static PlayerWeaponMode GetOrAdd(GameObject player)
    {
        if (player == null) return null;
        var existing = player.GetComponent<PlayerWeaponMode>();
        if (existing != null) return existing;
        return player.AddComponent<PlayerWeaponMode>();
    }

    private PlayerGrenadeThrower grenadeThrower;
    private PlayerHackTool hackTool;

    void Awake()
    {
        if (resources == null) resources = GetComponent<PlayerResources>();
        if (resources == null) resources = FindFirstObjectByType<PlayerResources>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
    }

    private PlayerMovement movement;

    // 뛰기 모션은 모드 변경 없이 켜지고 꺼지므로 총 표시를 매 프레임 다시 맞춥니다(애니메이션 뒤).
    void LateUpdate()
    {
        UpdateGunVisibility();
    }

    void Start()
    {
        // 시작 모드(총)를 Animator/총 모델 표시에 한 번 반영해둡니다.
        ApplyModeVisuals();
    }

    // 수류탄 투척 동작 중이면 true입니다(이 동안은 모드 전환/자동 총 복귀를 미룹니다).
    private bool IsThrowingGrenade
    {
        get
        {
            if (grenadeThrower == null) grenadeThrower = GetComponent<PlayerGrenadeThrower>();
            return grenadeThrower != null && grenadeThrower.IsThrowing;
        }
    }

    // 해킹툴 사용 동작 중이면 true입니다(수류탄 투척과 같은 이유로 모드 전환/자동 총 복귀를 미룹니다).
    private bool IsDeployingHack
    {
        get
        {
            if (hackTool == null) hackTool = GetComponent<PlayerHackTool>();
            return hackTool != null && hackTool.IsDeploying;
        }
    }

    // 수류탄 투척 또는 해킹툴 사용 동작 중이면 true입니다.
    private bool IsActionLocked => IsThrowingGrenade || IsDeployingHack;

    void Update()
    {
        // 아이템 획득 연출 중에는 모드 전환/자동 총 복귀를 모두 미룹니다(기획 확정 - 연출 중 무기 전환 불가).
        if (PlayerItemPickupPresenter.BlocksActions) return;

        if (IsActionLocked)
        {
            if (IsThrowingGrenade) HandleKeysWhileThrowing();
            else HandleKeysWhileDeployingHack();
            // 동작 중에는 "자원 0이면 총 복귀"도 미룹니다(애니메이션이 끊기지 않도록).
            return;
        }

        if (Input.GetKeyDown(gunKey))
        {
            SetMode(PlayerWeapon.Gun);
        }
        else if (Input.GetKeyDown(grenadeKey))
        {
            TrySwitch(PlayerWeapon.Grenade);
        }
        else if (Input.GetKeyDown(hackKey))
        {
            TrySwitch(PlayerWeapon.Hack);
        }

        // 자원이 어떤 이유로든 "0이 되는 순간" 그 모드를 유지할 수 없으므로 총으로 돌아갑니다.
        // [115번 때 변경 - 형빈 확정] 0개여도 2/3번으로 수류탄/해킹툴 모드에 들어갈 수 있게 바뀌어서(던지기/사용은 못 함 - 각 컴포넌트가
        // 자원 소모에 실패해 막힘), 처음부터 0개로 들어온 경우에는 돌려보내지 않습니다(115번 크레모어를 R로 터뜨릴 수 있도록).
        if (current != PlayerWeapon.Gun)
        {
            int amount = GetAmount(current);
            if (lastModeAmount > 0 && amount <= 0)
            {
                SetMode(PlayerWeapon.Gun);
            }
            else
            {
                lastModeAmount = amount;
            }
        }
    }

    // 투척 동작 중의 키 처리: 수류탄이 손을 떠난 뒤에만 1번/3번으로 투척을 끊고 전환합니다.
    private void HandleKeysWhileThrowing()
    {
        if (grenadeThrower == null || !grenadeThrower.CanInterruptThrow) return;

        if (Input.GetKeyDown(gunKey))
        {
            grenadeThrower.InterruptThrow();
            SetMode(PlayerWeapon.Gun);
        }
        else if (Input.GetKeyDown(hackKey))
        {
            // 해킹툴로 바꿀 수 없는 상황(해킹툴 컴포넌트 없음)이면 투척도 끊지 않고 그대로 둡니다. [115번 때 변경] 0개여도 전환은 됩니다.
            if (HasHandler(PlayerWeapon.Hack))
            {
                grenadeThrower.InterruptThrow();
                SetMode(PlayerWeapon.Hack);
            }
        }
    }

    // 해킹툴 사용 동작 중의 키 처리: 효과가 발동된 뒤에만 1번/2번으로 사용 동작을 끊고 전환합니다.
    private void HandleKeysWhileDeployingHack()
    {
        if (hackTool == null || !hackTool.CanInterruptDeploy) return;

        if (Input.GetKeyDown(gunKey))
        {
            hackTool.InterruptDeploy();
            SetMode(PlayerWeapon.Gun);
        }
        else if (Input.GetKeyDown(grenadeKey))
        {
            // 수류탄으로 바꿀 수 없는 상황(수류탄 컴포넌트 없음)이면 사용 동작도 끊지 않고 그대로 둡니다. [115번 때 변경] 0개여도 전환은 됩니다.
            if (HasHandler(PlayerWeapon.Grenade))
            {
                hackTool.InterruptDeploy();
                SetMode(PlayerWeapon.Grenade);
            }
        }
    }

    // 해당 모드로 바꿔봅니다. 자원이 0개이거나 그 무기를 처리할 컴포넌트가 없으면 바뀌지 않습니다
    // (기획 확정 - 수류탄/해킹툴이 없으면 2/3키 전환 불가).
    public bool TrySwitch(PlayerWeapon weapon)
    {
        if (weapon == PlayerWeapon.Gun)
        {
            SetMode(PlayerWeapon.Gun);
            return true;
        }

        if (!HasHandler(weapon)) return false;
        // [115번 때 변경 - 형빈 확정] 자원이 0개여도 모드에는 들어갈 수 있습니다(던지기/사용은 각 컴포넌트가 막음).

        SetMode(weapon);
        return true;
    }

    // 수류탄/해킹툴이 마지막 하나를 쓴 직후 호출합니다. 총 모드로 돌아가면서, 같은 클릭으로
    // 총까지 나가지 않도록 이번 프레임 번호를 기록합니다.
    public void ReturnToGunAfterUse()
    {
        SetMode(PlayerWeapon.Gun);
        gunFireBlockedFrame = Time.frameCount;
    }

    private void SetMode(PlayerWeapon weapon)
    {
        if (current == weapon) return;
        current = weapon;
        lastModeAmount = weapon == PlayerWeapon.Gun ? 0 : GetAmount(weapon);
        ApplyModeVisuals();
        OnModeChanged?.Invoke(current);
    }

    // 아이템 획득 연출이 시작/종료될 때 PlayerItemPickupPresenter가 호출합니다(총 모델 숨김/다시 보이기).
    public void RefreshModeVisuals()
    {
        ApplyModeVisuals();
    }

    // 현재 모드를 Animator(WeaponMode)와 총 모델 표시에 반영합니다.
    private void ApplyModeVisuals()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (animator != null && HasIntParameter(animator, weaponModeAnimatorParam))
        {
            animator.SetInteger(weaponModeAnimatorParam, (int)current);
        }

        UpdateGunVisibility();
    }

    // 총 모델을 지금 상황에 맞게 숨기거나 보입니다. 오브젝트 자체를 끄는 이유는 위 [애니메이션 연동] 설명 참고.
    private void UpdateGunVisibility()
    {
        if (gunModel == null) return;
        if (movement == null) movement = GetComponent<PlayerMovement>();

        bool hide = (current == PlayerWeapon.Grenade && hideGunInGrenadeMode) ||
                    (current == PlayerWeapon.Hack && hideGunInHackMode) ||
                    (hideGunWhileRunning && movement != null && movement.IsRunAnimating) ||
                    PlayerItemPickupPresenter.BlocksActions; // 아이템 획득 연출 중에는 항상 숨김
        if (gunModel.activeSelf == hide) gunModel.SetActive(!hide);
    }

    private static bool HasIntParameter(Animator anim, string paramName)
    {
        if (anim == null || string.IsNullOrEmpty(paramName)) return false;
        if (anim.runtimeAnimatorController == null) return false;
        foreach (var p in anim.parameters)
        {
            if (p.type == AnimatorControllerParameterType.Int && p.name == paramName) return true;
        }
        return false;
    }

    private int GetAmount(PlayerWeapon weapon)
    {
        if (resources == null) return 0;

        switch (weapon)
        {
            case PlayerWeapon.Grenade: return resources.GetAmount(BasicItemType.Grenade);
            case PlayerWeapon.Hack: return resources.GetAmount(BasicItemType.HackingTool);
            default: return 1;
        }
    }

    private bool HasHandler(PlayerWeapon weapon)
    {
        switch (weapon)
        {
            case PlayerWeapon.Grenade: return GetComponent<PlayerGrenadeThrower>() != null;
            case PlayerWeapon.Hack: return GetComponent<PlayerHackTool>() != null;
            default: return true;
        }
    }
}
