using System.Reflection;
using UnityEngine;

// 플레이어가 죽는 순간(PlayerHealth.OnDied) 플레이어 쪽에서 일어나야 하는 일을 전부 담당합니다.
// 씬 쪽 처리(게임 전체 정지 + "다시 시작하시겠습니까?" 확인창 + 재시작)는 GameOverUI가 따로
// 담당하고, 이 스크립트는 "플레이어 본인"만 다룹니다.
//
// [붙이는 방법]
// 직접 붙일 필요 없습니다. PlayerHealth.Awake()가 없으면 자동으로 붙입니다.
//
// [사망 순간 하는 일]
// 1. 조작 정지: 이동/조준·사격/재장전/무기 전환(1·2·3)/수류탄/해킹툴/액티브(Space)/음식(E)/
//    일회용(Q) 컴포넌트를 전부 끕니다(enabled = false). 각 스크립트는 수정하지 않습니다.
//    - 끄기 전에 무기 모드를 총으로 되돌려 둡니다. 수류탄/해킹 범위 표시(GrenadeAimIndicator/
//      HackRangeIndicator)는 "수류탄/해킹 모드 + 우클릭 중"일 때만 보이므로, 총 모드로 돌아가면
//      우클릭을 누르고 있어도 스스로 숨습니다.
//    - 총 조준선(AimTrajectoryIndicator)은 직접 끄고 선 렌더러도 숨깁니다(조준 중에 죽으면
//      PlayerAimAndAttack이 꺼진 채로 "조준 중" 값이 남아 선이 계속 보이는 것을 막기 위함).
// 2. 피격 점멸 정지: 치명타를 맞으면 무적 시간이 시작되는데, 곧바로 게임이 정지(Time.timeScale
//    = 0)되므로 PlayerHitFlash가 "안 보이는/흰색" 상태에서 멈춰버릴 수 있습니다. 그래서
//    PlayerHitFlash를 원래 상태로 되돌린 뒤 끕니다(아래 ResetHitFlash 참고).
// 3. 사망 애니메이션: Animator를 "정지와 무관하게 실제 시간으로 재생"(UnscaledTime)으로 바꾸고,
//    걷기/조준 파라미터를 0/false로 돌린 뒤, 사망 신호를 보냅니다. 신호는 두 가지를 모두 지원하며
//    Animator에 있는 것만 보냅니다(없으면 그냥 넘어감 - 수류탄 Throw/해킹 Hack 트리거와 같은 방식).
//    - deathAnimatorBool(기본 "IsDead", Bool): true로 켜고 계속 유지합니다. [확정 방식] 이
//      프로젝트의 Animator는 Locomotion(전신) + UpperBody(상체 마스크, Weight 1) 두 레이어라, 두
//      레이어 모두 Any State -> Death 전이를 걸어야 상체까지 함께 쓰러집니다. Trigger는 한 번 쓰이면
//      바로 꺼지는 신호라 두 레이어가 함께 받는다는 보장이 약해서, 켜진 채 유지되는 Bool을 씁니다.
//    - deathAnimatorTrigger(기본 "Die", Trigger): 예전 방식 호환용으로 남겨둔 것입니다.
//
// [되돌리는 처리가 없는 이유]
// 사망 후 재시작은 씬을 통째로 다시 불러오는 방식(GameOverUI)이라, 여기서 끈 것들은 새 씬에서
// 새 플레이어가 처음부터 다시 만들어지며 자연스럽게 원래대로 돌아옵니다.
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerHealth))]
public class PlayerDeathHandler : MonoBehaviour
{
    [Header("사망 애니메이션")]
    [Tooltip("사망 시 재생할 Animator 트리거 이름입니다. Animator에 이 이름의 Trigger 파라미터가 " +
             "없으면 애니메이션 없이 넘어갑니다.")]
    public string deathAnimatorTrigger = "Die";

    [Tooltip("사망 시 true로 켤 Animator Bool 파라미터 이름입니다(두 레이어 모두 이 값으로 Death 상태로 " +
             "전이시키는 용도). Animator에 이 이름의 Bool 파라미터가 없으면 건너뜁니다.")]
    public string deathAnimatorBool = "IsDead";

    [Tooltip("비워두면 플레이어 자식에서 자동으로 찾습니다.")]
    public Animator animator;

    private PlayerHealth playerHealth;
    private bool handled;

    void Awake()
    {
        playerHealth = GetComponent<PlayerHealth>();
        if (playerHealth != null) playerHealth.OnDied += HandleDied;
    }

    void OnDestroy()
    {
        if (playerHealth != null) playerHealth.OnDied -= HandleDied;
    }

    private void HandleDied()
    {
        if (handled) return;
        handled = true;

        StopPlayerControl();
        ResetHitFlash();
        PlayDeathAnimation();
    }

    // ---------- 1. 조작 정지 ----------
    private void StopPlayerControl()
    {
        // 무기 모드를 먼저 총으로 되돌립니다(수류탄/해킹 범위 표시가 스스로 숨도록).
        var weaponMode = GetComponent<PlayerWeaponMode>();
        if (weaponMode != null) weaponMode.TrySwitch(PlayerWeapon.Gun);

        // 총 조준선: 컴포넌트를 끄고 선 렌더러를 직접 숨깁니다.
        var aimIndicator = GetComponent<AimTrajectoryIndicator>();
        if (aimIndicator != null)
        {
            aimIndicator.enabled = false;
            var lines = aimIndicator.LineRenderers;
            if (lines != null)
            {
                foreach (var line in lines)
                {
                    if (line != null) line.enabled = false;
                }
            }
        }

        DisableAll<PlayerMovement>();
        DisableAll<PlayerAimAndAttack>();
        DisableAll<PlayerShooting>();
        DisableAll<PlayerWeaponMode>();
        DisableAll<PlayerGrenadeThrower>();
        DisableAll<PlayerHackTool>();
        DisableAll<PlayerActiveItem>();
        DisableAll<PlayerFoodItem>();
        DisableAll<PlayerDisposableItem>();
        // 아이템 획득 연출 중에 죽으면 머리 위 아이콘/획득 동작을 즉시 정리합니다(꺼지는 순간 스스로 정리).
        DisableAll<PlayerItemPickupPresenter>();
    }

    private void DisableAll<T>() where T : Behaviour
    {
        foreach (var component in GetComponentsInChildren<T>(true))
        {
            component.enabled = false;
        }
    }

    // ---------- 2. 피격 점멸 정지 ----------
    // PlayerHitFlash에는 "원래 상태(모두 보이기 + 원래 색)로 되돌리기"를 하는 ResetToNormal()이
    // 이미 있지만 private이라 밖에서 바로 부를 수 없습니다. PlayerHitFlash.cs를 수정하지 않기 위해
    // 그 메서드를 이름으로 찾아 호출합니다. 나중에 PlayerHitFlash의 ResetToNormal 이름이
    // 바뀌면 여기서 경고가 뜨니, 그때 이 문자열도 같이 바꿔주세요.
    private void ResetHitFlash()
    {
        foreach (var flash in GetComponentsInChildren<PlayerHitFlash>(true))
        {
            MethodInfo reset = typeof(PlayerHitFlash).GetMethod(
                "ResetToNormal", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

            if (reset != null)
            {
                reset.Invoke(flash, null);
            }
            else
            {
                Debug.LogWarning("PlayerDeathHandler: PlayerHitFlash.ResetToNormal()을 찾지 못해 사망 시 " +
                                 "점멸 상태를 되돌리지 못했습니다. 메서드 이름이 바뀌었는지 확인해주세요.");
            }

            flash.enabled = false;
        }
    }

    // ---------- 3. 사망 애니메이션 ----------
    private void PlayDeathAnimation()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (animator == null) return;

        // GameOverUI가 곧바로 게임 전체를 정지(Time.timeScale = 0)시키므로, 플레이어 애니메이션만은
        // 실제 시간으로 계속 재생되도록 바꿉니다.
        animator.updateMode = AnimatorUpdateMode.UnscaledTime;

        // 꺼진 PlayerMovement/PlayerAimAndAttack이 마지막으로 넣어둔 값이 남아 걷기/조준 동작이
        // 계속 재생되지 않도록 정지 상태로 돌려둡니다.
        SetFloatIfExists("MoveX", 0f);
        SetFloatIfExists("MoveZ", 0f);
        SetFloatIfExists("Speed", 0f);
        SetBoolIfExists("IsAiming", false);

        SetBoolIfExists(deathAnimatorBool, true);

        if (HasParameter(deathAnimatorTrigger, AnimatorControllerParameterType.Trigger))
        {
            animator.SetTrigger(deathAnimatorTrigger);
        }
    }

    private void SetFloatIfExists(string name, float value)
    {
        if (HasParameter(name, AnimatorControllerParameterType.Float)) animator.SetFloat(name, value);
    }

    private void SetBoolIfExists(string name, bool value)
    {
        if (HasParameter(name, AnimatorControllerParameterType.Bool)) animator.SetBool(name, value);
    }

    private bool HasParameter(string name, AnimatorControllerParameterType type)
    {
        if (animator == null || string.IsNullOrEmpty(name)) return false;
        if (animator.runtimeAnimatorController == null) return false;

        foreach (var p in animator.parameters)
        {
            if (p.type == type && p.name == name) return true;
        }
        return false;
    }
}
