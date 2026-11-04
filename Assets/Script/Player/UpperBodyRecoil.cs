using UnityEngine;

// 사격할 때 상체가 반동으로 잠깐 뒤로 젖혀졌다가 돌아오는 연출입니다(애니메이션 클립 없이 코드로).
//
// [왜 클립이 아니라 코드인가 - 형빈 확정]
// Fire 클립은 상체 마스크(UpperAvatarMask)가 뼈를 섞는 방식 때문에 총이 옆으로 꺾이고, 각도 보정으로
// 맞춰도 조준 자세와 달리 몸을 크게 꺾는 느낌이 남아서 폐기했습니다. 대신 지금 재생 중인 조준/비조준
// 자세는 그대로 두고, Animator가 자세를 다 계산한 직후(LateUpdate) 상체 시작 뼈(Spine1)만 짧게
// 젖혔다가 되돌립니다. 클립이 없으니 마스크 문제와 무관하고, 총알이 나가는 위치(FirePoint, 플레이어
// 루트의 자식)와 조준선(플레이어 정면 기준)에도 전혀 영향이 없습니다.
//
// [언제 흔들리나]
// PlayerShooting.OnShotFired(사격 틱 = 탄약 1발 소모)마다 한 번 Kick()됩니다. 연사면 버스트 간격에
// 맞춰 여러 번, 19번 양갈래는 한 틱에 두 발이 나가도 한 번입니다. 5번 분열탄 생성에는 반응하지 않습니다.
//
// [움직임]
// Kick -> kickDuration 동안 (지금 흔들림 정도에서) 목표 각도까지 젖힘 -> returnDuration 동안 returnCurve를
// 따라 원위치. stackOnRapidFire가 꺼져 있으면(기본) 매 사격마다 같은 크기로 처음부터 다시 흔들리고, 켜면
// 아직 돌아오는 중에 다시 쏠 때 흔들림이 쌓입니다(최대 maxStackMultiplier배).
// 각도는 플레이어 기준 X(앞뒤 - 음수 = 뒤로 젖힘)/Y(좌우 비틀기)/Z(옆 기울기)입니다. Play 중에 바꾸면 바로
// 반영되지만 Play를 끄면 되돌아가므로, 맞춘 값은 적어뒀다가 프리팹에 다시 넣으세요.
// 플레이어가 죽으면 흔들림을 즉시 끕니다(사망 애니메이션에 섞이지 않도록).
//
// [흔들 뼈 찾기 - 레이사/유니티짱 공용]
// 1) Bone 칸에 직접 넣은 뼈가 있으면 그 뼈를 씁니다.
// 2) 비어 있고 Animator가 Humanoid(유니티짱 등)면 이름 대신 Humanoid Bone 종류로 찾습니다
//    (기본 Chest = 유니티짱 Character1_Spine2. 반동이 약하면 Spine으로 바꾸면 한 단계 아래 허리에서 젖혀집니다).
//    아바타에 그 뼈가 없으면(예: Chest가 비어 있는 아바타) 한 단계 아래 뼈(Spine)로 대신합니다.
// 3) Humanoid가 아니면(레이사 Generic) Bone Name 이름으로 자식에서 찾습니다.
// [실행 순서] HumanoidAimLookAt(손 IK + 총구 정렬)이 먼저 실행된 뒤 이 반동이 적용되므로, 총구를 정면에 맞춘
// 자세에서 가슴과 함께 총이 위로 튀고, 총구 정렬 보정값에는 반동이 섞이지 않습니다.
//
// [연사 압축 - 형빈 확정]
// 연사 간격(예: 0.08초)이 반동 한 번(0.04 + 0.15초)보다 짧으면 몸이 돌아오기 전에 다음 발이 와서
// 젖혀진 채 멈춘 것처럼 보입니다. 그래서 PlayerShooting이 알려주는 "다음 발까지 남은 시간"이 있으면,
// 이번 반동을 (그 시간 x burstFillRatio) 안에 끝내도록 젖힘(burstKickRatio 비율)/복귀를 압축합니다.
// 버스트의 마지막 발과 단발은 원래 시간(kickDuration/returnDuration)을 그대로 씁니다. 원래 시간이
// 압축 시간보다 이미 짧으면 늘리지 않고 원래 시간을 씁니다.
[DisallowMultipleComponent]
public class UpperBodyRecoil : MonoBehaviour
{
    [Header("참조 (비워두면 자동으로 찾습니다)")]
    public PlayerShooting shooting;
    public PlayerHealth playerHealth;
    [Tooltip("비워두면 플레이어 자식에서 자동으로 찾습니다. Humanoid인지 확인하는 데 씁니다.")]
    public Animator animator;
    [Tooltip("흔들 상체 뼈입니다. 비워두면 아래 규칙(Humanoid Bone / Bone Name)으로 자동으로 찾습니다.")]
    public Transform bone;
    [Tooltip("Animator가 Humanoid(유니티짱 등)일 때 흔들 뼈 종류입니다. Chest = 가슴(기본), Spine = 한 단계 아래 허리(더 크게 젖혀짐).")]
    public HumanBodyBones humanoidBone = HumanBodyBones.Chest;
    [Tooltip("Animator가 Humanoid가 아닐 때(레이사 Generic) 이름으로 찾을 뼈입니다.")]
    public string boneName = "Bip001 Spine1";

    [Header("반동 각도 (플레이어 기준, 도)")]
    [Tooltip("X: 앞뒤(음수 = 뒤로 젖힘), Y: 좌우 비틀기, Z: 옆으로 기울기. 5~10도 정도에서 시작해 맞추는 걸 권장합니다.")]
    public Vector3 recoilEuler = new Vector3(-8f, 0f, 0f);

    [Header("반동 시간")]
    [Tooltip("젖혀지는 데 걸리는 시간(초)입니다. 짧을수록 '툭' 튀는 느낌입니다.")]
    [Min(0.001f)] public float kickDuration = 0.04f;
    [Tooltip("원위치로 돌아오는 데 걸리는 시간(초)입니다.")]
    [Min(0.001f)] public float returnDuration = 0.15f;
    [Tooltip("돌아올 때의 움직임 곡선입니다(가로 0~1 = 진행도, 세로 1 -> 0 = 흔들림 크기).")]
    public AnimationCurve returnCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

    [Header("연사 압축 (버스트 중 다음 발이 있을 때만)")]
    [Tooltip("다음 발까지의 시간 중 반동(젖힘+복귀)에 쓸 비율입니다. 나머지는 원위치에서 잠깐 멈춰 있는 시간입니다.")]
    [Range(0.1f, 1f)] public float burstFillRatio = 0.9f;
    [Tooltip("압축된 반동 시간 중 젖히는 데 쓸 비율입니다. 나머지는 복귀에 씁니다.")]
    [Range(0.05f, 0.95f)] public float burstKickRatio = 0.3f;

    [Header("빠른 연사")]
    [Tooltip("켜면 돌아오는 중에 다시 쏠 때 흔들림이 쌓입니다(최대 Max Stack Multiplier배). 끄면 매번 같은 크기로 처음부터 다시 흔들립니다.")]
    public bool stackOnRapidFire = false;
    [Tooltip("흔들림이 쌓일 때의 최대 배율입니다.")]
    [Min(1f)] public float maxStackMultiplier = 1.5f;

    private float timer = float.MaxValue;   // 마지막 Kick 이후 흐른 시간
    private float startWeight;              // Kick 순간의 흔들림 크기(이어서 젖히기 시작점)
    private float targetWeight = 1f;        // 이번 Kick의 최대 크기
    private float currentWeight;            // 이번 프레임의 흔들림 크기(0 = 원래 자세)
    private float activeKickDuration = 0.04f;   // 이번 반동에 실제로 쓰는 젖힘 시간(연사 압축 반영)
    private float activeReturnDuration = 0.15f; // 이번 반동에 실제로 쓰는 복귀 시간(연사 압축 반영)
    private bool warnedMissingBone;

    void Awake()
    {
        if (shooting == null) shooting = GetComponent<PlayerShooting>();
        if (playerHealth == null) playerHealth = GetComponent<PlayerHealth>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (bone == null) bone = ResolveBone();
    }

    void OnEnable()
    {
        if (shooting == null) shooting = GetComponent<PlayerShooting>();
        if (shooting != null) shooting.OnShotFired += Kick;
    }

    void OnDisable()
    {
        if (shooting != null) shooting.OnShotFired -= Kick;
    }

    // 사격 틱마다 호출됩니다. 지금 흔들림 정도에서 이어서 다시 젖히기 시작합니다(튀지 않도록).
    // nextShotDelay > 0이면(버스트 중 다음 발이 있음) 반동을 그 시간 안에 끝내도록 압축합니다.
    public void Kick(float nextShotDelay)
    {
        activeKickDuration = kickDuration;
        activeReturnDuration = returnDuration;

        if (nextShotDelay > 0f)
        {
            float budget = nextShotDelay * burstFillRatio;
            float kickBudget = budget * burstKickRatio;
            float returnBudget = budget - kickBudget;
            activeKickDuration = Mathf.Min(kickDuration, kickBudget);
            activeReturnDuration = Mathf.Min(returnDuration, returnBudget);
        }

        activeKickDuration = Mathf.Max(activeKickDuration, 0.001f);
        activeReturnDuration = Mathf.Max(activeReturnDuration, 0.001f);

        startWeight = currentWeight;
        targetWeight = stackOnRapidFire ? Mathf.Min(currentWeight + 1f, maxStackMultiplier) : 1f;
        timer = 0f;
    }

    void LateUpdate()
    {
        if (playerHealth != null && playerHealth.IsDead)
        {
            currentWeight = 0f;
            timer = float.MaxValue;
            return;
        }

        timer += Time.deltaTime;
        currentWeight = EvaluateWeight(timer);
        if (currentWeight <= 0.0001f) return;

        if (bone == null)
        {
            bone = ResolveBone();
            if (bone == null)
            {
                if (!warnedMissingBone)
                {
                    bool human = animator != null && animator.isHuman;
                    string what = human ? $"Humanoid 뼈 '{humanoidBone}'" : $"이름 '{boneName}'의 뼈";
                    Debug.LogWarning($"UpperBodyRecoil({name}): {what}를 찾지 못해 반동 연출을 하지 않습니다. " +
                                     "Inspector의 Bone 칸에 흔들 뼈를 직접 넣어 주세요.", this);
                    warnedMissingBone = true;
                }
                return;
            }
        }

        // 플레이어 기준 회전을 월드 기준으로 바꿔서, Animator가 방금 계산한 뼈 방향 앞에 곱합니다
        // (UpperBodyAngleCorrector와 같은 방식).
        Quaternion root = transform.rotation;
        Quaternion offset = Quaternion.Euler(recoilEuler * currentWeight);
        bone.rotation = root * offset * Quaternion.Inverse(root) * bone.rotation;
    }

    private float EvaluateWeight(float t)
    {
        if (t < activeKickDuration)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / activeKickDuration);
            return Mathf.Lerp(startWeight, targetWeight, k);
        }

        float r = (t - activeKickDuration) / activeReturnDuration;
        if (r >= 1f) return 0f;

        float curve = returnCurve != null && returnCurve.length > 0 ? returnCurve.Evaluate(r) : 1f - r;
        return targetWeight * Mathf.Max(curve, 0f);
    }

    // 흔들 뼈를 찾습니다: Humanoid면 뼈 종류로(없으면 한 단계 아래 뼈로), 아니면 이름으로.
    private Transform ResolveBone()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();

        if (animator != null && animator.isHuman && animator.avatar != null)
        {
            Transform t = animator.GetBoneTransform(humanoidBone);
            if (t == null && humanoidBone == HumanBodyBones.UpperChest) t = animator.GetBoneTransform(HumanBodyBones.Chest);
            if (t == null && (humanoidBone == HumanBodyBones.UpperChest || humanoidBone == HumanBodyBones.Chest))
                t = animator.GetBoneTransform(HumanBodyBones.Spine);
            if (t != null) return t;
        }

        return FindChildRecursive(transform, boneName);
    }

    private static Transform FindChildRecursive(Transform parent, string name)
    {
        if (parent == null || string.IsNullOrEmpty(name)) return null;
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            var found = FindChildRecursive(child, name);
            if (found != null) return found;
        }
        return null;
    }
}
