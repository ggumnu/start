using System.Collections.Generic;
using UnityEngine;

// SD 파츠의 흔들림 본(이름이 "Jiggle_"로 시작하고 자식 본이 있는 본)을 캐릭터 움직임에 관성으로 뒤처지며 흔들리게 합니다.
// (가방 인형 등. 3ds Max에서 본 끝점(Nub)을 남겨 두면 "본 -> 첫 자식" 방향을 매달린 방향으로 씁니다 - 본의 축 방향은 무관.)
//
// [에디터 설정]
// SD_AltBody_v02(SDPartAttacher가 붙은 오브젝트)에 Add Component로 붙입니다. 설정 칸은 없고, 값은 Resources/SDJiggleSettings 에셋
// (본 이름별로 다른 값 - 예: Jiggle_Ahoge, Jiggle_Tail).
// 본이 본의 자식인 사슬(아호게 A -> B)도 부모부터 차례로 계산됩니다.
//
// [원리]
// 본 끝점을 월드 공간의 점으로 따로 들고 있다가, 매 프레임(애니메이션 뒤) 모델링한 자세의 끝점 쪽으로 스프링처럼 당깁니다.
// 캐릭터가 움직이면 끝점이 늦게 따라와 흔들리고, 감쇠로 금방 멈춥니다. 계산은 1/120초 단위로 나눠 프레임 속도와 무관하게 합니다.
// - 파츠를 붙이거나 떼면(SDPartAttacher.OnPartsChanged) 본 목록을 다시 찾습니다.
// - 순간이동(레벨 이동/부활 등)으로 한 프레임에 크게 움직이면 흔들림을 초기화해 튀지 않게 합니다.
// - 게임이 멈추면(Time.timeScale = 0) 흔들림도 멈춥니다.
[DisallowMultipleComponent]
public class SDJiggleBones : MonoBehaviour
{
    private const string Prefix = "Jiggle_";
    private const float Step = 1f / 120f;
    private const float TeleportDistance = 0.5f;

    private class JiggleBone
    {
        public Transform bone;
        public Transform tipChild;
        public Quaternion restLocalRotation;
        public Vector3 tip;          // 시뮬레이션 중인 끝점(월드)
        public Vector3 velocity;
        public Vector3 lastBonePos;
        public bool initialized;
    }

    private readonly List<JiggleBone> bones = new List<JiggleBone>();
    private SDPartAttacher attacher;
    private float accumulator;

    void OnEnable()
    {
        if (attacher == null) attacher = GetComponent<SDPartAttacher>();
        if (attacher != null)
        {
            attacher.OnPartsChanged -= Rescan;
            attacher.OnPartsChanged += Rescan;
        }
        Rescan();
    }

    void OnDisable()
    {
        if (attacher != null) attacher.OnPartsChanged -= Rescan;
        foreach (JiggleBone j in bones)
        {
            if (j.bone != null) j.bone.localRotation = j.restLocalRotation;
        }
    }

    // 캐릭터 아래의 흔들림 본을 다시 찾습니다(부모 본부터 계산되도록 계층 순서대로).
    public void Rescan()
    {
        foreach (JiggleBone j in bones)
        {
            if (j.bone != null) j.bone.localRotation = j.restLocalRotation;
        }
        bones.Clear();
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
        {
            if (!t.name.StartsWith(Prefix) || t.childCount == 0) continue;
            bones.Add(new JiggleBone { bone = t, tipChild = t.GetChild(0), restLocalRotation = t.localRotation });
        }
    }

    void LateUpdate()
    {
        if (bones.Count == 0) return;
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        SDJiggleSettings s = SDJiggleSettings.Get();
        accumulator = Mathf.Min(accumulator + dt, 0.1f);
        int steps = Mathf.FloorToInt(accumulator / Step);
        accumulator -= steps * Step;

        for (int i = 0; i < bones.Count; i++)
        {
            JiggleBone j = bones[i];
            if (j.bone == null || j.tipChild == null) continue;
            Simulate(j, s.For(j.bone.name), steps);
        }
    }

    private void Simulate(JiggleBone j, SDJiggleSettings.Profile s, int steps)
    {
        Transform b = j.bone;
        // 모델링한 자세(부모 본 기준)로 되돌린 상태에서 쉬는 끝점과 길이를 구함
        b.localRotation = j.restLocalRotation;
        Vector3 pos = b.position;
        Vector3 restTip = j.tipChild.position;
        Vector3 restDir = restTip - pos;
        float len = restDir.magnitude;
        if (len < 1e-6f) return;
        restDir /= len;

        if (!j.initialized || (pos - j.lastBonePos).sqrMagnitude > TeleportDistance * TeleportDistance)
        {
            j.tip = restTip; j.velocity = Vector3.zero; j.lastBonePos = pos; j.initialized = true;
            return;
        }

        // 관성: 1보다 작으면 본이 움직인 만큼 끝점도 일부 같이 옮겨 덜 흔들리게
        j.tip += (pos - j.lastBonePos) * (1f - s.inertia);
        j.lastBonePos = pos;

        Vector3 gravity = Vector3.down * (s.gravity * len * 9.81f);
        for (int k = 0; k < steps; k++)
        {
            Vector3 acc = (restTip - j.tip) * s.stiffness - j.velocity * s.damping + gravity;
            j.velocity += acc * Step;
            j.tip += j.velocity * Step;
        }

        // 길이 유지 + 최대 각도 제한
        Vector3 dir = j.tip - pos;
        if (dir.sqrMagnitude < 1e-12f) dir = restDir; else dir.Normalize();
        float angle = Vector3.Angle(restDir, dir);
        if (angle > s.maxAngle && angle > 1e-3f)
        {
            dir = Vector3.Slerp(restDir, dir, s.maxAngle / angle);
            Vector3 clampedTip = pos + dir * len;
            j.velocity -= Vector3.Project(j.velocity, j.tip - clampedTip); // 벽에 막힌 방향의 속도는 버림
            j.tip = clampedTip;
        }
        else
        {
            j.tip = pos + dir * len;
        }

        b.rotation = Quaternion.FromToRotation(restDir, dir) * b.rotation;
    }
}
