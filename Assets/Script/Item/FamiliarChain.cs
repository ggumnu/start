using System.Collections.Generic;
using UnityEngine;

// [패밀리어 줄 서기 - 형빈 확정: 아이작 리펜던스처럼 얻은 순서대로 줄을 섬]
// 플레이어에 붙어 패밀리어(FamiliarDrone)들을 얻은 순서대로 들고 있고, 매 프레임 앞에서부터 차례로 따라가게 합니다.
// 첫 번째는 플레이어 뒤, 그다음은 바로 앞 패밀리어 뒤를 따라갑니다(예: 59번 먼저 → 플레이어 → 59번 → 60번).
// - 중간 패밀리어를 잃으면 목록에서 빠지고, 뒤 패밀리어들은 새 앞 대상을 향해 자연스럽게 당겨집니다.
// - 플레이어가 한 프레임에 크게 튀면(순간이동/다음 레벨/헬기 도착) 줄 전체를 바로 뒤로 붙입니다.
// - 플레이어 이동(Update)이 끝난 뒤 따라가도록 LateUpdate에서 움직입니다.
// 패밀리어가 처음 생길 때 FamiliarDrone.Spawn이 자동으로 붙이므로 따로 배치할 필요가 없습니다.
public class FamiliarChain : MonoBehaviour
{
    private readonly List<FamiliarDrone> members = new List<FamiliarDrone>();

    private Vector3 lastPlayerPos;
    private bool hasLastPlayerPos;
    private Vector3 playerMoveDir = Vector3.forward;

    public static FamiliarChain GetOrAdd(GameObject player)
    {
        var chain = player.GetComponent<FamiliarChain>();
        if (chain == null) chain = player.AddComponent<FamiliarChain>();
        return chain;
    }

    public void Register(FamiliarDrone drone)
    {
        if (drone == null || members.Contains(drone)) return;
        if (!hasLastPlayerPos) ResetPlayerTracking();
        members.Add(drone);
    }

    public void Unregister(FamiliarDrone drone)
    {
        members.Remove(drone);
    }

    // 카메라 반대쪽(화면 안쪽) 수평 방향입니다. 패밀리어가 처음 설 자리와 "드론 뒤" 이펙트 방향에 씁니다.
    public static Vector3 CameraAwayDirection()
    {
        var cam = Camera.main;
        if (cam == null) return Vector3.forward;
        Vector3 f = cam.transform.forward;
        f.y = 0f;
        return f.sqrMagnitude > 0.0001f ? f.normalized : Vector3.forward;
    }

    // 카메라가 바라보는 방향 그대로(높이 포함)입니다. 이펙트를 모델 "뒤"로 밀 때 씁니다.
    public static Vector3 CameraAwayDirection3D()
    {
        var cam = Camera.main;
        return cam != null ? cam.transform.forward : Vector3.forward;
    }

    // 새 패밀리어가 처음 설 자리 = 바로 앞 대상의 카메라 반대쪽 뒤.
    public Vector3 InitialPositionFor(FamiliarDrone drone, Vector3 away)
    {
        GetLeader(drone, out Vector3 leaderPos, out _, out _);
        return drone.TargetBehind(leaderPos, -away, transform.position.y);
    }

    void ResetPlayerTracking()
    {
        lastPlayerPos = transform.position;
        hasLastPlayerPos = true;
        playerMoveDir = -CameraAwayDirection();
    }

    // drone 바로 앞 대상의 위치/움직인 방향/움직이는 중인지를 알려줍니다(맨 앞이면 플레이어).
    void GetLeader(FamiliarDrone drone, out Vector3 pos, out Vector3 moveDir, out bool moving)
    {
        int index = members.IndexOf(drone);
        for (int i = index - 1; i >= 0; i--)
        {
            var lead = members[i];
            if (lead == null || !lead.Exists) continue;
            pos = lead.BasePosition;
            moveDir = lead.MoveDir;
            moving = lead.IsMoving;
            return;
        }
        pos = transform.position;
        moveDir = playerMoveDir;
        moving = playerMovingThisFrame;
    }

    private bool playerMovingThisFrame;

    void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        // 빈 자리(이미 사라진 패밀리어) 정리
        for (int i = members.Count - 1; i >= 0; i--)
        {
            if (members[i] == null || !members[i].Exists) members.RemoveAt(i);
        }
        if (members.Count == 0)
        {
            hasLastPlayerPos = false;
            return;
        }
        if (!hasLastPlayerPos) ResetPlayerTracking();

        Vector3 playerPos = transform.position;
        Vector3 jump = playerPos - lastPlayerPos;
        lastPlayerPos = playerPos;

        Vector3 planar = jump;
        planar.y = 0f;
        playerMovingThisFrame = planar.magnitude / dt > 0.3f;
        if (playerMovingThisFrame) playerMoveDir = planar.normalized;

        // 앞에서부터 차례로(앞 대상이 먼저 움직인 자리를 뒤가 따라감)
        for (int i = 0; i < members.Count; i++)
        {
            var drone = members[i];
            bool snap = jump.magnitude >= Mathf.Max(drone.motion.snapDistance, 0.5f);
            GetLeader(drone, out Vector3 leaderPos, out Vector3 leaderDir, out bool leaderMoving);
            drone.Follow(leaderPos, leaderDir, leaderMoving, playerPos.y, snap, dt);
        }
    }
}
