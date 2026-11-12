using UnityEngine;

// [뛰기 흙먼지] 비전투 뛰기 모션(SD_Run / SD_Run_B) 동안 발 뒤에 카툰 흙먼지를 남깁니다.
// PlayerMovement가 Awake에서 자동으로 붙입니다. 설정(프리팹/간격/위치) = TravelBoostSettings의 "뛰기 흙먼지".
// 기본은 발 박자와 상관없이 짧은 간격(runDustInterval)마다 왼발/오른발 뒤를 번갈아 가며 우다다다 튀어나옵니다(형빈 확정).
// runDustInterval = 0이면 예전 방식(Run 상태 재생 위치 기준, 발이 뒤로 차는 순간에만)으로 동작합니다.
[DisallowMultipleComponent]
public class PlayerRunDust : MonoBehaviour
{
    private const string LocomotionLayer = "Locomotion";
    private const string RunState = "Run";

    private PlayerMovement movement;
    private Animator animator;
    private int locoLayer = -2; // -2 = 아직 안 찾음, -1 = 없음
    private float lastPhase = -1f;
    private Transform leftFoot, rightFoot;
    private float intervalTimer;   // 다음 먼지까지 남은 시간(간격 방식)
    private bool nextIsLeft = true;

    void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        animator = GetComponentInChildren<Animator>();
        if (animator != null && animator.isHuman)
        {
            leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
        }
    }

    void LateUpdate()
    {
        TravelBoostSettings s = TravelBoostSettings.Get();
        if (s.runDustPrefab == null || movement == null || animator == null || !movement.IsRunAnimating)
        {
            lastPhase = -1f;
            intervalTimer = 0f; // 다시 뛰기 시작하면 첫 먼지가 바로 나오도록
            return;
        }

        if (s.runDustInterval > 0f)
        {
            intervalTimer -= Time.deltaTime;
            // 프레임이 길게 끊겨도 한 번에 몰아서 나오지 않도록 한 프레임에 최대 2개까지만
            for (int n = 0; intervalTimer <= 0f && n < 2; n++)
            {
                Spawn(nextIsLeft ? leftFoot : rightFoot, s);
                nextIsLeft = !nextIsLeft;
                intervalTimer += s.runDustInterval * (1f + Random.Range(-s.runDustIntervalJitter, s.runDustIntervalJitter));
            }
            if (intervalTimer < 0f) intervalTimer = 0f;
            return;
        }

        if (locoLayer == -2) locoLayer = animator.GetLayerIndex(LocomotionLayer);
        if (locoLayer < 0) return;

        AnimatorStateInfo st = animator.GetCurrentAnimatorStateInfo(locoLayer);
        if (!st.IsName(RunState))
        {
            lastPhase = -1f;
            return;
        }

        float phase = Mathf.Repeat(st.normalizedTime, 1f);
        if (lastPhase >= 0f)
        {
            float left = Mathf.Repeat(s.runDustPhase, 1f);
            float right = Mathf.Repeat(s.runDustPhase + 0.5f, 1f);
            if (Crossed(lastPhase, phase, left)) Spawn(leftFoot, s);
            if (Crossed(lastPhase, phase, right)) Spawn(rightFoot, s);
        }
        lastPhase = phase;
    }

    // a -> b 사이(한 바퀴를 넘어 0으로 돌아온 경우 포함)에 p를 지났는지.
    private static bool Crossed(float a, float b, float p)
    {
        if (b >= a) return p > a && p <= b;
        return p > a || p <= b;
    }

    private void Spawn(Transform foot, TravelBoostSettings s)
    {
        Vector3 forward = transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
        forward.Normalize();

        Vector3 pos = foot != null ? foot.position : transform.position;
        pos.y = transform.position.y + s.runDustHeight;
        pos -= forward * s.runDustBackOffset;
        if (s.runDustSideJitter > 0f)
            pos += Vector3.Cross(Vector3.up, forward) * Random.Range(-s.runDustSideJitter, s.runDustSideJitter);
        Quaternion rot = Quaternion.LookRotation((-forward + Vector3.up * s.runDustUpward).normalized, Vector3.up);

        GameObject go = Instantiate(s.runDustPrefab, pos, rot);
        go.transform.localScale = s.runDustPrefab.transform.localScale * s.runDustScale;
        // 방 범위 규칙: 다른 방으로 넘어가면 바로 지웁니다(RoomScope [27]).
        RoomScope.DestroyWhenPlayerLeaves(go);
        Destroy(go, s.runDustLifetime);
    }
}
