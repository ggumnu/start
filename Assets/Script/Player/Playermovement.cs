using UnityEngine;

// [사용 방법]
// 1. 플레이어 캐릭터 오브젝트에 CharacterController 컴포넌트를 추가합니다.
//    (Radius, Height, Center 값을 모델 크기에 맞게 조절해주세요)
// 2. 이 스크립트를 같은 오브젝트에 추가합니다. PlayerStats도 함께 있어야 합니다.
// 3. Animator에 float 파라미터 3개를 만드세요:
//    - MoveX, MoveZ: 캐릭터 로컬 기준 좌우/전후 이동값(-1~1). 조준 중(Base Layer의
//      AimLocomotion 2D Freeform Directional 블렌드트리: 대기/앞/뒤/좌/우 5개 클립)에 사용.
//    - Speed: moveInput의 크기(0~1). 비조준 중(Base Layer의 NonAimLocomotion 1D
//      블렌드트리: 비조준 대기 <-> 비조준 앞으로 걷기)에 사용.
//
// [단차(계단) 처리 관련]
// CharacterController 스크립트만으로 단차 등반을 막으려는 시도(이동 후 위치 보정,
// 이동 전 레이캐스트로 막기 등)는 슬라이드 이동이 끊기는 등 부작용이 있어 폐기했습니다.
// 대신 레벨 쪽에서, 오르면 안 되는 단차의 콜라이더 높이를 캐릭터 키보다 훨씬 높게
// (일반 벽처럼) 만들어서 CharacterController의 기본 충돌/슬라이드 처리만으로 자연스럽게
// 막는 방식으로 해결하기로 했습니다. 이 스크립트 자체에는 단차 관련 로직이 없습니다.
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerStats))]
public class PlayerMovement : MonoBehaviour
{
    [Header("이동 설정")]
    [Tooltip("중력 가속도 (바닥에 붙어있도록 하기 위한 값)")]
    public float gravity = -20f;

    [Header("참조 (비워두면 자동으로 찾습니다)")]
    public Camera mainCamera;
    public Animator animator;

    private CharacterController controller;
    private PlayerStats stats;
    private Vector3 velocity;
    private Vector3 moveInput;
    private PlayerDash dash; // [74번] 처음 사용할 때 자동으로 붙으므로 없으면 매 프레임 다시 찾습니다(가벼운 GetComponent)

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        stats = GetComponent<PlayerStats>();
        if (mainCamera == null) mainCamera = Camera.main;
        if (animator == null) animator = GetComponentInChildren<Animator>();
    }

    void Update()
    {
        HandleMovementInput();
        ApplyGravity();
        UpdateAnimator();
    }

    void HandleMovementInput()
    {
        // 방향키 또는 WASD 입력 (관성 없이 -1~1 값을 그대로 받음)
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        // [로딩 화면] 로딩 화면(LoadingScreen)이 떠 있는 동안에는 이동 입력만 0으로 만듭니다.
        // 컴포넌트를 끄지 않고 입력만 막는 이유: 끄면 아래 중력(ApplyGravity)까지 멈춰서, 시작방 위에서
        // 생성된 플레이어가 공중에 뜬 채로 남기 때문입니다. 중력은 그대로라 떨어져 착지하는 동작은 정상입니다.
        if (LoadingScreen.BlocksPlayerInput)
        {
            h = 0f;
            v = 0f;
        }

        // [밀어내기 넉백] 플레이어가 ForcedPush로 밀리는 짧은 동안(16번 방 추가 때 새 벽 앞에서 밀려나는 경우 등)에도 이동 입력만
        // 0으로 막습니다. 안 막으면 밀리는 반대쪽 키를 누르고 있을 때 밀림이 상쇄됩니다. 밀림 자체와 중력은 그대로 적용됩니다.
        if (ForcedPush.BlocksPlayerInput)
        {
            h = 0f;
            v = 0f;
        }

        // [74번 돌진] 돌진하는 짧은 동안(PlayerDash)에도 이동 입력을 막습니다 - 돌진 중에는 방향을 바꿀 수 없습니다(형빈 확정).
        // 실제 돌진 이동은 PlayerDash가 직접 CharacterController.Move로 처리하고, 중력은 그대로 적용됩니다.
        if (PlayerDash.BlocksPlayerInput)
        {
            h = 0f;
            v = 0f;
        }

        // 카메라가 바라보는 방향을 기준으로 이동 방향을 계산합니다.
        Vector3 camForward = mainCamera.transform.forward;
        Vector3 camRight = mainCamera.transform.right;
        camForward.y = 0f;
        camRight.y = 0f;
        camForward.Normalize();
        camRight.Normalize();

        Vector3 inputDir = (camForward * v + camRight * h);
        if (inputDir.sqrMagnitude > 1f) inputDir.Normalize();

        // [입장 컷씬] 컷씬(IntroCutscene) 중에는 키 입력 대신 컷씬이 정한 방향(월드 기준)으로 걷습니다 - 걷기 애니메이션/중력은 평소와 같습니다.
        if (IntroCutscene.OverridesMovement) inputDir = IntroCutscene.ScriptedMoveDirection;
        // [헬기 퇴각] 퇴각 연출(HeliExtraction) 중 사다리 앞으로 걸어가는 동안에도 같은 방식으로 정해진 방향으로 걷습니다.
        else if (HeliExtraction.OverridesMovement) inputDir = HeliExtraction.ScriptedMoveDirection;

        moveInput = inputDir;

        // [74번 돌진] 돌진 중에는 이동 입력 대신 돌진 방향을 moveInput으로 둡니다 - 걷기 애니메이션과 비조준 시 바라보는 방향이
        // 돌진 방향을 따라가게 하기 위함입니다. 실제 이동은 PlayerDash가 하므로 여기서는 움직이지 않습니다(아래 Move 생략).
        if (dash == null) dash = GetComponent<PlayerDash>();
        if (dash != null && dash.IsDashing)
        {
            moveInput = dash.DashDirection;
            return;
        }

        // moveSpeed는 이제 PlayerStats에서 가져옵니다 (아이템으로 조절될 예정).
        Vector3 horizontalMove = moveInput * stats.MoveSpeed;
        controller.Move(new Vector3(horizontalMove.x, 0f, horizontalMove.z) * Time.deltaTime);
    }

    void ApplyGravity()
    {
        if (controller.isGrounded && velocity.y < 0f)
        {
            velocity.y = -2f; // 바닥에서 살짝 눌러주는 힘 (안정적인 접지 판정용)
        }
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }

    // 조준 중(IsAiming)에는 캐릭터가 마우스 방향을 보고 있으므로, 이동 방향을 캐릭터
    // 로컬 좌표로 변환한 MoveX/MoveZ가 "몸이 보는 방향 대비 어느 쪽으로 스트레이프
    // 중인가"를 나타내 Base Layer의 AimLocomotion 블렌드트리에 그대로 씁니다.
    //
    // 비조준 중에는 캐릭터가 항상 이동 방향을 보도록 회전하므로(PlayerAimAndAttack의
    // RotateTowardsMovement), 방향 구분 없이 "지금 움직이고 있는가(0~1)"만 있으면
    // 충분합니다. 그래서 moveInput의 크기를 Speed로 넘겨 NonAimLocomotion의
    // 비조준 대기 <-> 비조준 앞으로 걷기 1D 블렌드트리에 씁니다.
    void UpdateAnimator()
    {
        if (animator == null) return;

        Vector3 localMove = transform.InverseTransformDirection(moveInput);
        animator.SetFloat("MoveX", localMove.x);
        animator.SetFloat("MoveZ", localMove.z);
        animator.SetFloat("Speed", moveInput.magnitude);
    }

    public Vector3 GetMoveInput() => moveInput;
}