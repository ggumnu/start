using System;
using System.Collections.Generic;
using UnityEngine;

// Humanoid 캐릭터(예: SD 유니티짱)의 조준 자세를 다듬는 보정 컴포넌트입니다. 두 가지를 합니다.
//
// 1) 상체 돌리기 (LookAt)
//    Mixamo 소총 조준 동작은 골반을 비틀고 허리/등을 반대로 꺾어 총구를 정면에 맞추는 자세입니다.
//    상체 레이어 마스크(UpperAvatarMask_Combined)에서 Humanoid의 Body(허리/등)를 끄면 "림보 자세"는
//    사라지지만, 상체를 비트는 동작도 같이 빠져서 총이 옆을 향할 수 있습니다. Unity의 Humanoid 전용
//    기능(Animator IK의 LookAt)으로 허리/가슴/머리를 조준 방향 쪽으로 돌려 그 빈자리를 메웁니다.
//
// 2) 양손 내리기 (Hand IK)
//    Mixamo 조준 동작은 일반 체형 기준이라 SD 체형에서는 총이 머리 높이에 걸립니다. 애니메이션이 정한
//    오른손/왼손 위치를 읽어 "같은 만큼" 옮기고(Hand Offset), 손 방향은 애니메이션 그대로 둡니다.
//    그래서 총은 각도(바닥과 평행 등)를 유지한 채 평행이동하고, 왼손도 총을 잡은 채 함께 따라옵니다.
//    팔꿈치는 Unity가 팔 길이에 맞게 자동으로 굽히고 폅니다(너무 많이 옮기면 팔이 다 펴진 채 멈춥니다).
//    Elbow Hint로 팔꿈치가 옆으로 벌어지지 않고 아래를 향하도록 유도할 수 있습니다.
//
// 3) 양손 흔들림 줄이기 (Hand Stabilize)
//    상체 마스크에서 Body를 뺐기 때문에 허리/가슴은 걷기 클립을 따르고, 팔은 그 출렁이는 가슴에 업혀서
//    조준하며 걸을 때 총이 위아래로 흔들립니다. 손 목표를 걸어도 출렁이지 않는 플레이어 루트 기준으로
//    바꿔 걸음마다 생기는 빠른 흔들림만 걸러냅니다(느린 변화는 따라감). 몸통이 출렁인 만큼은 팔꿈치가
//    흡수하므로 걷기 동작(다리/골반)은 그대로입니다. 조준을 시작하는 순간에는 지금 손 위치에서 바로
//    출발하므로 손이 날아오지 않습니다.
//
// 4) 총구 자동 정렬 (Muzzle Direction)
//    총이 오른손에 붙어 있으면 총구 방향은 애니메이션의 손목 각도에 달려 있어 정면을 향하지 않을 수 있습니다.
//    총에 만든 "총구 방향 표시용" 빈 오브젝트(파란 Z축 = 총열 방향)를 기준으로, 총구가 항상 조준 방향
//    (= 총알이 나가는 방향, 바닥과 평행)을 향하도록 오른손을 중심으로 양손을 함께 돌립니다.
//    LateUpdate에서 실제 총구 방향을 재서 남은 차이를 손으로 직접 보정하고, 그 차이를 다음 프레임의 IK에
//    미리 반영해 왼손도 총을 따라오게 합니다. 사격 반동(UpperBodyRecoil)보다 먼저 실행되므로 반동은 그대로 보입니다.
//
// 클립/FBX/Animator 그래프는 건드리지 않습니다.
//
// [Generic 캐릭터(레이사)에는 영향 없음]
// IK(OnAnimatorIK)는 Humanoid 아바타에서만 호출됩니다. 이 컴포넌트는 유니티짱 모델에만 붙입니다.
//
// [에디터 설정 - 꼭 필요]
// 1) 이 컴포넌트는 Animator가 붙은 오브젝트(SD_unitychan_humanoid)에 붙여야 합니다. Unity는
//    OnAnimatorIK를 Animator와 같은 오브젝트의 스크립트에만 보내기 때문입니다.
// 2) Animator 창에서 아래 두 레이어의 톱니바퀴 -> IK Pass를 켜야 합니다.
//    - Look At Layer Name(기본 Locomotion): 상체 돌리기용 (허리/가슴이 이 레이어에서 오기 때문)
//    - Hand Layer Name(기본 UpperBody): 양손 내리기용 (팔이 이 레이어에서 오기 때문)
//    IK Pass가 꺼진 레이어의 기능은 동작하지 않습니다. (Generic인 레이사에는 영향 없음)
//
// [방향 기준]
// 플레이어 루트(PlayerAimAndAttack이 붙은 오브젝트)의 정면 = 실제 총알이 나가는 방향입니다.
// - 상체 돌리기: 그 방향으로 Aim Distance만큼 떨어진 가슴 높이의 점을 바라봅니다. 어긋나 보이면 Yaw Offset으로.
// - 양손 내리기: Hand Offset의 X/Y/Z도 이 플레이어 루트 기준(X 오른쪽, Y 위, Z 앞, 단위 m)입니다.
//
// [켜지는 조건]
// - Only When Aiming / Hand Only When Aiming을 켜면(기본) Animator의 AimBlend(0~1) 값만큼만 적용됩니다.
// - Blocked States 목록의 상태(장전/수류탄/해킹/획득/사망 등)가 재생되는 동안에는 꺼집니다.
//   전이 중에는 전이 비율만큼 서서히 켜지고 꺼집니다. IsDead가 true면 꺼집니다.
// 모든 값은 Play 중에 바꾸면 바로 반영됩니다. Play를 끄면 되돌아가므로 맞춘 값은 적어두었다가
// Play를 끈 뒤 프리팹에 다시 넣으세요.
[DisallowMultipleComponent]
[DefaultExecutionOrder(-50)] // 사격 반동(UpperBodyRecoil, LateUpdate)보다 먼저 실행 -> 반동은 총구 정렬 위에 그대로 보임
[RequireComponent(typeof(Animator))]
public class HumanoidAimLookAt : MonoBehaviour
{
    [Serializable]
    public class StateRef
    {
        [Tooltip("상태가 있는 Animator 레이어 이름입니다(예: UpperBody).")]
        public string layerName = "UpperBody";
        [Tooltip("이 상태가 재생되는 동안 보정을 끕니다.")]
        public string stateName = "Reloading";

        public StateRef() { }
        public StateRef(string layer, string state) { layerName = layer; stateName = state; }
    }

    [Header("참조 (비워두면 자동으로 찾습니다)")]
    [Tooltip("비워두면 같은 오브젝트의 Animator를 씁니다.")]
    public Animator animator;
    [Tooltip("방향의 기준이 되는 오브젝트입니다. 비워두면 부모 중 PlayerAimAndAttack이 붙은 오브젝트(플레이어 루트)를 씁니다.")]
    public Transform aimReference;

    [Header("IK 레이어 (Animator에서 이 레이어들의 IK Pass를 켜야 합니다)")]
    [Tooltip("상체 돌리기를 할 레이어 이름입니다(허리/가슴이 오는 레이어).")]
    public string ikLayerName = "Locomotion";
    [Tooltip("양손 내리기를 할 레이어 이름입니다(팔이 오는 레이어).")]
    public string handLayerName = "UpperBody";

    [Header("상체 돌리기 - 조준 방향")]
    [Tooltip("조준 방향을 좌우로 돌리는 각도(도)입니다. 양수 = 오른쪽. 상체가 조준선보다 왼쪽/오른쪽을 보면 이 값으로 맞추세요.")]
    public float yawOffset = 0f;
    [Tooltip("바라볼 점까지의 거리(m)입니다. 3~5를 권장합니다. 너무 짧으면 상체 방향이 조금만 흔들려도 각도가 크게 튑니다.")]
    [Min(0.1f)] public float aimDistance = 5f;
    [Tooltip("바라볼 점의 높이 보정(m)입니다. 가슴 높이 기준이며, 양수면 위를, 음수면 아래를 봅니다.")]
    public float heightOffset = 0f;

    [Header("상체 돌리기 - 강도 (0~1)")]
    [Tooltip("전체 보정 강도입니다. 0이면 상체 돌리기를 끈 것과 같습니다.")]
    [Range(0f, 1f)] public float weight = 1f;
    [Tooltip("허리/가슴을 얼마나 돌릴지입니다. 총구 방향에 가장 큰 영향을 줍니다.")]
    [Range(0f, 1f)] public float bodyWeight = 0.6f;
    [Tooltip("머리를 얼마나 돌릴지입니다. (상체 레이어 마스크에서 Head가 켜져 있으면 클립의 머리 동작이 우선됩니다)")]
    [Range(0f, 1f)] public float headWeight = 0.5f;
    [Tooltip("눈을 얼마나 돌릴지입니다.")]
    [Range(0f, 1f)] public float eyesWeight = 0f;
    [Tooltip("회전 제한입니다. 0 = 제한 없음, 1 = 거의 돌지 않음. 0.5 전후를 권장합니다.")]
    [Range(0f, 1f)] public float clampWeight = 0.5f;

    [Header("양손 내리기 (총과 양손을 함께 옮김, 총 각도는 그대로)")]
    [Tooltip("양손을 옮길 거리(m)입니다. 플레이어 루트 기준 X = 오른쪽, Y = 위, Z = 앞. 총을 내리려면 Y를 음수로(예: -0.05씩) 조금씩 내려보세요.")]
    public Vector3 handOffset = Vector3.zero;
    [Tooltip("양손 내리기 강도입니다. 0 = 원래 애니메이션, 1 = Hand Offset만큼 완전히 옮김.")]
    [Range(0f, 1f)] public float handWeight = 1f;
    [Tooltip("팔꿈치가 옆으로 벌어지지 않고 아래를 향하도록 유도하는 강도입니다. 0이면 Unity가 알아서 정합니다.")]
    [Range(0f, 1f)] public float elbowHintWeight = 0.5f;
    [Tooltip("팔꿈치를 유도할 지점을 어깨-손 중간에서 얼마나 아래로 둘지(m)입니다.")]
    [Min(0f)] public float elbowHintDown = 0.2f;
    [Tooltip("켜면 조준 중(AimBlend)일 때만 양손을 내립니다. 끄면 평소(비조준)에도 내립니다(Blocked States/사망 제외).")]
    public bool handOnlyWhenAiming = true;

    [Header("양손 흔들림 줄이기 (조준하며 걸을 때 총을 견착한 듯 고정)")]
    [Tooltip("0 = 몸통을 따라 그대로 흔들림, 1 = 플레이어 기준으로 거의 고정. 0.7~0.9를 권장합니다. " +
             "켜고 꺼지는 조건은 양손 내리기와 같습니다(Hand Only When Aiming).")]
    [Range(0f, 1f)] public float handStabilize = 0.8f;
    [Tooltip("손이 애니메이션 위치를 따라가는 데 걸리는 시간(초)입니다. 클수록 걸을 때 흔들림이 더 많이 걸러집니다. " +
             "플레이어 기준 좌표라 조준 방향 전환은 늦어지지 않습니다. 0.3~0.6을 권장합니다.")]
    [Min(0.01f)] public float stabilizeSmoothTime = 0.4f;
    [Tooltip("손 목표를 팔 길이의 몇 배까지 허용할지입니다. 1에 가까우면 팔이 다 펴질 때까지 뻗고, " +
             "낮추면 팔꿈치가 항상 조금 굽은 채로 유지됩니다(손이 툭 떨어지는 현상 방지). 0.9~0.97을 권장합니다.")]
    [Range(0.7f, 1f)] public float maxArmReach = 0.95f;

    [Header("총구 자동 정렬 (총구가 항상 조준 방향 = 총알 방향을 향하게)")]
    [Tooltip("총에 만든 '총구 방향 표시용' 빈 오브젝트입니다. 이 오브젝트의 파란 화살표(Z축)가 총열 방향을 가리켜야 합니다. " +
             "비워두면 총구 자동 정렬을 하지 않습니다.")]
    public Transform muzzleDirection;
    [Tooltip("켜면 조준 중에 총구를 조준 방향(바닥과 평행)으로 맞춥니다. 오른손을 중심으로 양손을 함께 돌리므로 왼손이 총에서 떨어지지 않습니다.")]
    public bool alignMuzzle = true;
    [Tooltip("일부러 총구를 좌우로 살짝 비스듬하게 두고 싶을 때만 쓰는 각도(도)입니다. 양수 = 오른쪽. 보통은 0.")]
    public float muzzleYawOffset = 0f;

    [Header("켜지는 조건")]
    [Tooltip("켜면 Animator의 AimBlend(조준 0~1) 값만큼만 상체를 돌립니다. 끄면 항상 돌립니다(Blocked States 제외).")]
    public bool onlyWhenAiming = true;
    [Tooltip("조준 정도를 읽을 Float 파라미터 이름입니다. 없으면 IsAiming(Bool)을 대신 읽습니다.")]
    public string aimBlendParam = "AimBlend";
    [Tooltip("AimBlend가 없을 때 대신 읽을 Bool 파라미터 이름입니다.")]
    public string isAimingParam = "IsAiming";
    [Tooltip("true면 보정을 끌 Bool 파라미터 이름입니다(사망).")]
    public string isDeadParam = "IsDead";
    [Tooltip("이 상태들이 재생되는 동안에는 보정(상체 돌리기 + 양손 내리기)을 끕니다.")]
    public List<StateRef> blockedStates = new List<StateRef>
    {
        new StateRef("UpperBody", "Reloading"),
        new StateRef("UpperBody", "Grenade_Idle"),
        new StateRef("UpperBody", "Grenade_Ready"),
        new StateRef("UpperBody", "Grenade_Throw"),
        new StateRef("UpperBody", "Hack_Idle"),
        new StateRef("UpperBody", "Hack_Ready"),
        new StateRef("UpperBody", "Hack"),
        new StateRef("UpperBody", "ItemPickup"),
        new StateRef("UpperBody", "Death"),
        new StateRef("Locomotion", "Death"),
    };
    [Tooltip("보정이 켜지고 꺼지는 속도(초당)입니다. 8이면 약 0.125초 만에 완전히 바뀝니다.")]
    [Min(0.1f)] public float blendSpeed = 8f;

    [Header("디버그")]
    [Tooltip("Scene 창에서 이 오브젝트를 선택했을 때 바라보는 점(노란 선)과 손 목표(하늘색 공)를 보여줍니다.")]
    public bool drawGizmo = true;

    private int aimBlendHash, isAimingHash, isDeadHash;
    private bool hasAimBlend, hasIsAiming, hasIsDead;
    private float currentWeight;     // 상체 돌리기
    private float currentHandWeight; // 양손 내리기 / 흔들림 줄이기

    // 손 하나의 흔들림 걸러내기 상태(플레이어 루트 기준 좌표)입니다.
    private struct HandSmoothState
    {
        public bool initialized;
        public Vector3 localPos;
        public Vector3 velocity;
        public Quaternion localRot;
    }
    private HandSmoothState rightSmooth, leftSmooth;

    // 총구 정렬용 누적 회전입니다. IK 단계에서 양손 목표에 미리 적용하고(왼손이 총을 따라오도록),
    // LateUpdate에서 실제 총구 방향을 재서 남은 차이만큼 손을 직접 돌려 정확히 맞춘 뒤 그 차이를 여기에 더합니다.
    // 손목 각도는 프레임마다 거의 같으므로 몇 프레임 안에 차이가 0에 가까워집니다.
    private Quaternion muzzleCorrection = Quaternion.identity;
    private int lastLookAtFrame = -1, lastHandFrame = -1;
    private Vector3 lastTarget, lastRightHandGoal, lastLeftHandGoal;
    private bool hasLastTarget, hasLastHandGoals;

    void Awake()
    {
        if (animator == null) animator = GetComponent<Animator>();
        if (aimReference == null)
        {
            var aim = GetComponentInParent<PlayerAimAndAttack>();
            aimReference = aim != null ? aim.transform : transform.root;
        }
        CacheParameters();
    }

    void OnEnable()
    {
        currentWeight = 0f;
        currentHandWeight = 0f;
        rightSmooth.initialized = false;
        leftSmooth.initialized = false;
        muzzleCorrection = Quaternion.identity;
    }

    private void CacheParameters()
    {
        hasAimBlend = hasIsAiming = hasIsDead = false;
        if (animator == null) return;

        aimBlendHash = Animator.StringToHash(aimBlendParam ?? "");
        isAimingHash = Animator.StringToHash(isAimingParam ?? "");
        isDeadHash = Animator.StringToHash(isDeadParam ?? "");

        foreach (var p in animator.parameters)
        {
            if (p.nameHash == aimBlendHash && p.type == AnimatorControllerParameterType.Float) hasAimBlend = true;
            if (p.nameHash == isAimingHash && p.type == AnimatorControllerParameterType.Bool) hasIsAiming = true;
            if (p.nameHash == isDeadHash && p.type == AnimatorControllerParameterType.Bool) hasIsDead = true;
        }
    }

    // 조준 정도(0~1)입니다. AimBlend가 없으면 IsAiming, 둘 다 없으면 1입니다.
    private float GetAimFactor()
    {
        if (hasAimBlend) return Mathf.Clamp01(animator.GetFloat(aimBlendHash));
        if (hasIsAiming) return animator.GetBool(isAimingHash) ? 1f : 0f;
        return 1f;
    }

    // 사망/차단 상태를 반영한 공통 배율(0~1)입니다.
    private float GetCommonFactor()
    {
        if (hasIsDead && animator.GetBool(isDeadHash)) return 0f;
        return 1f - GetBlockedWeight();
    }

    void OnAnimatorIK(int layerIndex)
    {
        if (animator == null || !animator.isHuman) return;

        // 레이어 이름은 Play 중에도 바뀔 수 있으니 매번 확인합니다(가벼운 연산).
        int lookAtLayer = string.IsNullOrEmpty(ikLayerName) ? -1 : animator.GetLayerIndex(ikLayerName);
        int handLayer = string.IsNullOrEmpty(handLayerName) ? -1 : animator.GetLayerIndex(handLayerName);

        if (layerIndex == lookAtLayer) ApplyLookAt();
        if (layerIndex == handLayer) ApplyHandOffset();
    }

    // ------------------------------------------------------------------
    // 1) 상체 돌리기
    // ------------------------------------------------------------------
    private void ApplyLookAt()
    {
        // 한 프레임에 한 번만 강도를 갱신합니다(같은 레이어가 두 번 불려도 속도가 두 배가 되지 않도록).
        if (lastLookAtFrame != Time.frameCount)
        {
            lastLookAtFrame = Time.frameCount;
            float target = weight * GetCommonFactor();
            if (onlyWhenAiming) target *= GetAimFactor();
            currentWeight = Mathf.MoveTowards(currentWeight, target, blendSpeed * Time.deltaTime);
        }

        if (currentWeight <= 0.0001f || aimReference == null)
        {
            animator.SetLookAtWeight(0f);
            hasLastTarget = false;
            return;
        }

        // 바라볼 점: 가슴 높이에서 플레이어 정면(+Yaw Offset) 방향으로 Aim Distance만큼 떨어진 곳
        Transform chest = animator.GetBoneTransform(HumanBodyBones.Chest);
        if (chest == null) chest = animator.GetBoneTransform(HumanBodyBones.Spine);
        Vector3 origin = chest != null ? chest.position : transform.position;

        Vector3 forward = aimReference.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f) forward = transform.forward;
        forward = Quaternion.Euler(0f, yawOffset, 0f) * forward.normalized;

        Vector3 lookTarget = origin + forward * aimDistance + Vector3.up * heightOffset;

        animator.SetLookAtWeight(currentWeight, bodyWeight, headWeight, eyesWeight, clampWeight);
        animator.SetLookAtPosition(lookTarget);

        lastTarget = lookTarget;
        hasLastTarget = true;
    }

    // ------------------------------------------------------------------
    // 2) 양손 내리기
    // ------------------------------------------------------------------
    private void ApplyHandOffset()
    {
        // 한 프레임에 한 번만 강도와 흔들림 걸러내기(스무딩)를 갱신합니다.
        bool newFrame = lastHandFrame != Time.frameCount;
        if (newFrame)
        {
            lastHandFrame = Time.frameCount;
            float target = handWeight * GetCommonFactor();
            if (handOnlyWhenAiming) target *= GetAimFactor();
            // 옮길 거리도 없고 흔들림 줄이기도 꺼져 있으면 손을 건드릴 이유가 없습니다.
            if (handOffset.sqrMagnitude < 1e-8f && handStabilize <= 0f && !IsMuzzleAlignActive()) target = 0f;
            currentHandWeight = Mathf.MoveTowards(currentHandWeight, target, blendSpeed * Time.deltaTime);
        }

        if (currentHandWeight <= 0.0001f || aimReference == null)
        {
            ClearHandIK();
            hasLastHandGoals = false;
            // 다음에 다시 켜질 때 지금 손 위치에서 바로 출발하도록 초기화합니다(손이 날아오지 않게).
            rightSmooth.initialized = false;
            leftSmooth.initialized = false;
            muzzleCorrection = Quaternion.identity;
            return;
        }

        // 플레이어 루트 기준 오프셋을 월드 방향으로 바꿉니다(크기 배율은 적용하지 않음 = 단위 m).
        Vector3 worldOffset = aimReference.TransformDirection(handOffset);

        ComputeHandGoal(AvatarIKGoal.RightHand, ref rightSmooth, newFrame, out Vector3 rightPos, out Quaternion rightRot);
        ComputeHandGoal(AvatarIKGoal.LeftHand, ref leftSmooth, newFrame, out Vector3 leftPos, out Quaternion leftRot);
        rightPos += worldOffset;
        leftPos += worldOffset;

        // [총구 자동 정렬 - 1단계] 지금까지 쌓인 보정 회전을 오른손 중심으로 양손에 적용합니다.
        // 총은 오른손에 붙어 있어 같이 돌고, 왼손도 같은 만큼 돌아 총을 잡은 채 따라옵니다.
        if (IsMuzzleAlignActive())
        {
            rightRot = muzzleCorrection * rightRot;
            leftPos = rightPos + muzzleCorrection * (leftPos - rightPos);
            leftRot = muzzleCorrection * leftRot;
        }

        // [팔 길이 제한] 오른손 목표가 팔이 닿지 않는 곳이면 닿는 곳까지 당깁니다. 왼손도 같은 만큼 당겨서
        // 두 손 사이 간격(=총을 잡은 모양)이 유지되게 합니다. 팔이 다 펴진 채 손이 툭 떨어지는 현상을 막습니다.
        Vector3 pull = GetReachCorrection(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm,
                                          HumanBodyBones.RightHand, rightPos);
        rightPos += pull;
        leftPos += pull;

        SetHandIK(AvatarIKGoal.RightHand, AvatarIKHint.RightElbow, HumanBodyBones.RightUpperArm, rightPos, rightRot);
        SetHandIK(AvatarIKGoal.LeftHand, AvatarIKHint.LeftElbow, HumanBodyBones.LeftUpperArm, leftPos, leftRot);

        lastRightHandGoal = rightPos;
        lastLeftHandGoal = leftPos;
        hasLastHandGoals = true;
    }

    // 애니메이션이 정한 손 위치/방향을 읽고, 흔들림 줄이기를 적용한 위치/방향을 돌려줍니다(오프셋 적용 전).
    private void ComputeHandGoal(AvatarIKGoal goal, ref HandSmoothState smooth, bool advance,
                                 out Vector3 pos, out Quaternion rot)
    {
        Vector3 animPos = animator.GetIKPosition(goal);
        Quaternion animRot = animator.GetIKRotation(goal);

        pos = animPos;
        rot = animRot;

        // [흔들림 줄이기] 손 위치/방향을 플레이어 루트 기준 좌표로 바꿔 천천히 따라가게 합니다.
        // 플레이어 루트 기준이라 조준 방향을 돌려도(루트가 돌면) 손은 같이 돌아가므로, 따라가는 시간을
        // 길게 잡아도 조준이 늦어지지 않습니다. 걸음마다 생기는 위아래 흔들림만 걸러집니다.
        if (handStabilize <= 0f) return;

        Vector3 localPos = aimReference.InverseTransformPoint(animPos);
        Quaternion localRot = Quaternion.Inverse(aimReference.rotation) * animRot;

        if (!smooth.initialized)
        {
            smooth.localPos = localPos;
            smooth.velocity = Vector3.zero;
            smooth.localRot = localRot;
            smooth.initialized = true;
        }
        else if (advance)
        {
            float dt = Time.deltaTime;
            smooth.localPos = Vector3.SmoothDamp(smooth.localPos, localPos, ref smooth.velocity,
                                                 stabilizeSmoothTime, Mathf.Infinity, dt);
            float k = 1f - Mathf.Exp(-dt / stabilizeSmoothTime);
            smooth.localRot = Quaternion.Slerp(smooth.localRot, localRot, k);
        }

        Vector3 stablePos = aimReference.TransformPoint(smooth.localPos);
        Quaternion stableRot = aimReference.rotation * smooth.localRot;

        pos = Vector3.Lerp(animPos, stablePos, handStabilize);
        rot = Quaternion.Slerp(animRot, stableRot, handStabilize);
    }

    // 목표가 팔 길이 x Max Arm Reach보다 멀면, 닿는 곳까지 당기는 이동량을 돌려줍니다(닿으면 0).
    private Vector3 GetReachCorrection(HumanBodyBones upperBone, HumanBodyBones lowerBone, HumanBodyBones handBone, Vector3 goalPos)
    {
        Transform upper = animator.GetBoneTransform(upperBone);
        Transform lower = animator.GetBoneTransform(lowerBone);
        Transform hand = animator.GetBoneTransform(handBone);
        if (upper == null || lower == null || hand == null) return Vector3.zero;

        float armLength = Vector3.Distance(upper.position, lower.position) + Vector3.Distance(lower.position, hand.position);
        float maxDist = armLength * maxArmReach;

        Vector3 toGoal = goalPos - upper.position;
        float dist = toGoal.magnitude;
        if (dist <= maxDist || dist < 1e-6f) return Vector3.zero;

        return upper.position + toGoal / dist * maxDist - goalPos;
    }

    // IK 목표(손 위치/방향)와 팔꿈치 유도를 Animator에 넣습니다.
    private void SetHandIK(AvatarIKGoal goal, AvatarIKHint hint, HumanBodyBones upperArmBone, Vector3 goalPos, Quaternion goalRot)
    {
        animator.SetIKPositionWeight(goal, currentHandWeight);
        animator.SetIKRotationWeight(goal, currentHandWeight);
        animator.SetIKPosition(goal, goalPos);
        animator.SetIKRotation(goal, goalRot);

        // 팔꿈치 유도: 어깨와 손 목표의 중간에서 Elbow Hint Down만큼 아래
        Transform upperArm = animator.GetBoneTransform(upperArmBone);
        if (upperArm != null && elbowHintWeight > 0f)
        {
            Vector3 hintPos = Vector3.Lerp(upperArm.position, goalPos, 0.5f) + Vector3.down * elbowHintDown;
            animator.SetIKHintPositionWeight(hint, elbowHintWeight * currentHandWeight);
            animator.SetIKHintPosition(hint, hintPos);
        }
        else
        {
            animator.SetIKHintPositionWeight(hint, 0f);
        }
    }

    private bool IsMuzzleAlignActive()
    {
        return alignMuzzle && muzzleDirection != null;
    }

    // 총구가 향해야 할 방향: 플레이어 정면(=총알 방향)을 바닥과 평행하게 + Muzzle Yaw Offset
    private Vector3 GetDesiredMuzzleDirection()
    {
        Vector3 forward = aimReference.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f) forward = transform.forward;
        return Quaternion.Euler(0f, muzzleYawOffset, 0f) * forward.normalized;
    }

    // [총구 자동 정렬 - 2단계] Animator(IK 포함)가 자세를 다 계산한 뒤 실제 총구 방향을 재서,
    // 남은 차이만큼 오른손을 직접 돌려 이번 프레임에 정확히 맞추고, 그 차이를 다음 프레임 IK용 보정에 더합니다.
    void LateUpdate()
    {
        if (!IsMuzzleAlignActive() || animator == null || !animator.isHuman || aimReference == null) return;
        if (currentHandWeight <= 0.0001f) return;

        Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        if (hand == null) return;

        Vector3 current = muzzleDirection.forward;
        if (current.sqrMagnitude < 1e-8f) return;

        Quaternion residual = Quaternion.FromToRotation(current, GetDesiredMuzzleDirection());

        // 이번 프레임: 손을 직접 돌려 총구를 맞춥니다(조준 시작/끝 전환 중에는 강도만큼만).
        hand.rotation = Quaternion.Slerp(Quaternion.identity, residual, currentHandWeight) * hand.rotation;

        // 다음 프레임: IK가 완전히 적용된 상태에서만 보정을 쌓습니다(전환 중에 쌓으면 넘치므로).
        if (currentHandWeight >= 0.99f)
        {
            muzzleCorrection = residual * muzzleCorrection;
            muzzleCorrection = Quaternion.Normalize(muzzleCorrection);
        }
    }

    private void ClearHandIK()
    {
        animator.SetIKPositionWeight(AvatarIKGoal.RightHand, 0f);
        animator.SetIKRotationWeight(AvatarIKGoal.RightHand, 0f);
        animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 0f);
        animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, 0f);
        animator.SetIKHintPositionWeight(AvatarIKHint.RightElbow, 0f);
        animator.SetIKHintPositionWeight(AvatarIKHint.LeftElbow, 0f);
    }

    // ------------------------------------------------------------------
    // 공통: 차단 상태
    // ------------------------------------------------------------------

    // Blocked States 중 지금 재생 중인 정도(0~1)의 최댓값입니다. 전이 중이면 전이 비율만큼 섞습니다.
    private float GetBlockedWeight()
    {
        if (blockedStates == null) return 0f;

        float max = 0f;
        for (int i = 0; i < blockedStates.Count; i++)
        {
            var s = blockedStates[i];
            if (s == null) continue;
            float w = GetStateWeight(s.layerName, s.stateName);
            if (w > max) max = w;
        }
        return max;
    }

    private float GetStateWeight(string layerName, string stateName)
    {
        if (string.IsNullOrEmpty(layerName) || string.IsNullOrEmpty(stateName)) return 0f;

        int layer = animator.GetLayerIndex(layerName);
        if (layer < 0) return 0f;

        int hash = Animator.StringToHash(layerName + "." + stateName);
        AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(layer);
        float currentMatch = current.fullPathHash == hash ? 1f : 0f;

        float w;
        if (animator.IsInTransition(layer))
        {
            AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(layer);
            float nextMatch = next.fullPathHash == hash ? 1f : 0f;
            float t = Mathf.Clamp01(animator.GetAnimatorTransitionInfo(layer).normalizedTime);
            w = currentMatch * (1f - t) + nextMatch * t;
        }
        else
        {
            w = currentMatch;
        }

        float layerWeight = layer == 0 ? 1f : animator.GetLayerWeight(layer);
        return w * layerWeight;
    }

    void OnValidate()
    {
        // 인스펙터에서 파라미터 이름을 바꾸면 다시 확인합니다.
        if (Application.isPlaying && animator != null) CacheParameters();
    }

    void OnDrawGizmosSelected()
    {
        if (!drawGizmo || animator == null) return;

        if (hasLastTarget)
        {
            Transform chest = animator.GetBoneTransform(HumanBodyBones.Chest);
            Vector3 origin = chest != null ? chest.position : transform.position;
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(origin, lastTarget);
            Gizmos.DrawWireSphere(lastTarget, 0.1f);
        }

        if (muzzleDirection != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawRay(muzzleDirection.position, muzzleDirection.forward * 0.5f);
        }

        if (hasLastHandGoals)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(lastRightHandGoal, 0.03f);
            Gizmos.DrawWireSphere(lastLeftHandGoal, 0.03f);
        }
    }
}
