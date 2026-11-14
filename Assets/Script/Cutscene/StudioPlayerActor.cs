using System.Collections.Generic;
using UnityEngine;

// [촬영 스튜디오 - 플레이어 배우]
// 촬영 씬에서 진짜 플레이어 프리팹을 대본대로 움직입니다. 입장 컷씬(IntroCutscene)과 같은 방법을 씁니다.
// - 이동: PlayerMovement가 CutsceneStudio.ScriptedMoveDirection을 읽어 걷습니다(걷기 애니메이션/발소리/중력은 게임 그대로).
//   뛰기는 CutsceneStudio.ForceRun으로 비전투 가속을 바로 최대로 올려 게임의 뛰기 모션/총 숨김/흙먼지/표정이 그대로 나오게 합니다.
// - 조작 잠금: 사격/조준/아이템 컴포넌트를 매 프레임 꺼 둡니다. PlayerWeaponMode는 뛰는 동안 총을 숨기는 일을 하므로 끄지 않습니다
//   (촬영 중 1/2/3 키를 누르면 무기 모드가 바뀌니 누르지 않습니다).
// - 장전/포즈/표정: PlayerShooting 등을 거치지 않고 Animator와 SDFaceController만 직접 움직입니다(탄약 변화 없음).
// - 조준: 꺼 둔 PlayerAimAndAttack 대신 같은 Animator 값(IsAiming / AimBlend)을 직접 넣습니다. 팔/총구 정렬(HumanoidAimLookAt)은
//   이 값을 읽어 게임과 똑같이 따라옵니다. 총은 플레이어가 바라보는 방향으로 겨눕니다(방향은 위치 표시의 방향 또는 '보기'로 정함).
//   장면 시간이 흐르기 전(시작 전 대기)에도 자세를 바꿔 두어, 0초 '조준'이면 녹화 첫 프레임부터 조준 자세입니다.
// Animator 상태/파라미터 이름과 섞임 시간은 입장 컷씬 설정(IntroCutsceneSettings)을 그대로 읽어, 이름이 바뀌어도 한 곳만 고치면 됩니다.
public class StudioPlayerActor
{
    private const float ArriveEpsilon = 0.005f;   // 마지막 지점 도착으로 보는 거리(m)
    private const float PassRadius = 0.08f;       // 중간 지점은 이 거리 안에 들어오면 다음 지점으로
    private const float StuckSeconds = 1.5f;      // 이 시간 동안 거의 못 움직이면 막힌 것으로 보고 멈춤

    private readonly PlayerHealth player;
    private readonly CharacterController controller;
    private readonly Animator animator;
    private readonly SDFaceController face;
    private readonly PlayerStats stats;
    private readonly PlayerTravelBoost boost;
    private readonly Camera cam;
    private StudioShot shot;

    // 이동
    private readonly List<Vector3> path = new List<Vector3>();
    private int pathIndex;
    private bool walking;
    private bool running;
    private float lastDist = float.MaxValue;
    private float stuckTime;
    private Vector3 lastWalkDir = Vector3.forward;
    private float animMove; // 멈춘 뒤 걷기 다리 자세를 서서히 줄이는 값(1 -> 0)

    // 몸 돌리기
    private bool turning;
    private Quaternion turnFrom, turnTo;
    private float turnTimer, turnDuration;

    // 장전 / 포즈
    private bool reloading;
    private float reloadTimer;
    private bool fullBodyPosing;

    // 조준(목표 0/1과 지금 값, 초당 바뀌는 양)
    private float aimTarget;
    private float aimBlend;
    private float aimRate;

    // 위치 표시가 아닌 대상 이름(적 배우 등)을 찾아 주는 곳 - CutsceneStudio가 연결
    public System.Func<string, Transform> ResolveExtra;

    // 표정(남은 시간 < 0 = 계속 유지)
    private readonly Dictionary<SDFaceExpression, float> heldFaces = new Dictionary<SDFaceExpression, float>();
    private readonly List<SDFaceExpression> faceBuffer = new List<SDFaceExpression>();

    public Transform Root { get { return player != null ? player.transform : null; } }
    public Vector3 MoveDirection { get; private set; }
    public bool ForceRun { get; private set; }
    // 0보다 크면 PlayerMovement가 이 속도(m/s)로 움직입니다 - 대본이 이동 시간을 정한 경우.
    public float SpeedOverride { get; private set; }
    public bool IsBusy { get { return walking; } }

    public StudioPlayerActor(PlayerHealth player, Camera cam)
    {
        this.player = player;
        this.cam = cam;
        controller = player.GetComponent<CharacterController>();
        animator = player.GetComponentInChildren<Animator>();
        face = player.GetComponentInChildren<SDFaceController>(true);
        stats = player.GetComponent<PlayerStats>();
        boost = player.GetComponent<PlayerTravelBoost>();
    }

    // 장면을 (다시) 시작할 때: 동작/표정/Animator를 처음 상태로 돌리고 시작 자리에 세웁니다.
    public void ResetForShot(StudioShot newShot, ShotScript script)
    {
        shot = newShot;
        StopWalking(false);
        turning = false;
        reloading = false;
        fullBodyPosing = false;
        animMove = 0f;
        aimTarget = 0f;
        aimBlend = 0f;
        aimRate = 0f;

        if (face != null)
        {
            foreach (var pair in heldFaces) face.Release(pair.Key);
        }
        heldFaces.Clear();

        if (animator != null && animator.runtimeAnimatorController != null)
        {
            animator.Rebind();
            animator.Update(0f);
        }

        Vector3 position = newShot.transform.position;
        Quaternion rotation = Quaternion.identity;
        ShotMark start = FindStartMark(script);
        if (start != null)
        {
            position = start.transform.position;
            rotation = YawOnly(start.transform.rotation);
        }
        Teleport(position);
        player.transform.rotation = rotation;
    }

    // 지정한 자리에 세웁니다(구도 잡기용 - 걷기/돌기는 멈춤).
    public void PlaceAt(Vector3 position, Quaternion rotation)
    {
        if (player == null) return;
        StopWalking(false);
        turning = false;
        Teleport(position);
        player.transform.rotation = YawOnly(rotation);
    }

    // 첫 '등장' 자리(없으면 첫 걷기/뛰기의 출발 자리)
    private ShotMark FindStartMark(ShotScript script)
    {
        foreach (ShotCue cue in script.cues)
        {
            if (cue.type == ShotCueType.PlayerSpawn) return shot.FindMark(cue.target);
        }
        foreach (ShotCue cue in script.cues)
        {
            if (cue.type == ShotCueType.PlayerMove && !cue.fromCurrent && cue.marks.Count > 0) return shot.FindMark(cue.marks[0]);
        }
        return null;
    }

    public void Execute(ShotCue cue)
    {
        if (player == null) return;
        IntroCutsceneSettings s = IntroCutsceneSettings.Get();

        switch (cue.type)
        {
            case ShotCueType.PlayerSpawn:
            {
                ShotMark mark = Find(cue.target, cue);
                if (mark == null) return;
                StopWalking(false);
                turning = false;
                Teleport(mark.transform.position);
                player.transform.rotation = YawOnly(mark.transform.rotation);
                break;
            }
            case ShotCueType.PlayerMove:
                BeginMove(cue);
                break;
            case ShotCueType.PlayerLook:
            {
                Vector3 targetPos;
                if (cue.target == ShotScript.CameraName)
                {
                    if (cam == null) return;
                    targetPos = cam.transform.position;
                }
                else
                {
                    Transform extra = ResolveExtra != null ? ResolveExtra(cue.target) : null;
                    if (extra != null)
                    {
                        targetPos = extra.position;
                    }
                    else
                    {
                        ShotMark mark = Find(cue.target, cue);
                        if (mark == null) return;
                        targetPos = mark.transform.position;
                    }
                }
                Vector3 dir = Flat(targetPos - player.transform.position);
                if (dir.sqrMagnitude < 0.0001f) return;
                turnFrom = player.transform.rotation;
                turnTo = Quaternion.LookRotation(dir.normalized);
                turnTimer = 0f;
                turnDuration = cue.duration;
                turning = true;
                break;
            }
            case ShotCueType.PlayerReload:
                BeginReload(s, Mathf.Max(0.1f, cue.duration));
                break;
            case ShotCueType.PlayerPose:
                BeginPose(s, cue.text == "짜잔");
                break;
            case ShotCueType.PlayerIdle:
                ReturnToIdle(s, cue.duration);
                break;
            case ShotCueType.PlayerFace:
                SetFace(cue.text, cue.duration);
                break;
            case ShotCueType.PlayerAim:
                aimTarget = cue.value;
                aimRate = cue.duration > 0f ? 1f / cue.duration : 0f;
                if (aimRate <= 0f) aimBlend = aimTarget;
                break;
        }
    }

    private void BeginMove(ShotCue cue)
    {
        var points = new List<Vector3>();
        foreach (string name in cue.marks)
        {
            ShotMark mark = Find(name, cue);
            if (mark == null) return;
            points.Add(mark.transform.position);
        }
        if (points.Count == 0) return;

        if (!cue.fromCurrent)
        {
            // 첫 지점이 출발 자리: 이미 그 자리에 있지 않으면 옮겨 세웁니다.
            if (Flat(points[0] - player.transform.position).magnitude > 0.05f) Teleport(points[0]);
            points.RemoveAt(0);
            if (points.Count == 0) return;
        }

        path.Clear();
        path.AddRange(points);

        // 대본이 이동 시간을 정했으면 전체 거리를 그 시간에 가는 속도로 맞춥니다.
        SpeedOverride = 0f;
        if (cue.duration > 0f)
        {
            float length = 0f;
            Vector3 from = player.transform.position;
            foreach (Vector3 point in path)
            {
                length += Flat(point - from).magnitude;
                from = point;
            }
            if (length > 0.01f) SpeedOverride = length / cue.duration;
        }
        pathIndex = 0;
        lastDist = float.MaxValue;
        stuckTime = 0f;
        animMove = 0f;
        turning = false;
        running = cue.run;
        ForceRun = cue.run;
        walking = true;
    }

    // 매 프레임(CutsceneStudio.Update, 다른 스크립트보다 늦게) 부릅니다.
    // advance = 장면 시간이 흐르는 중인지. 시작 전 대기/장면이 끝난 뒤에는 조작 잠금만 유지하고 동작은 멈춰 둡니다
    // (0초에 걸어둔 걷기가 녹화 시작 전에 먼저 출발하지 않도록).
    public void Tick(float deltaTime, bool advance)
    {
        if (player == null) return;
        IntroCutsceneSettings s = IntroCutsceneSettings.Get();

        EnforceLock();
        UpdateAim(deltaTime);
        if (!advance)
        {
            MoveDirection = Vector3.zero;
            return;
        }
        UpdateWalk(s, deltaTime);
        UpdateTurn(deltaTime);
        UpdateReload(s, deltaTime);
        UpdateFaces(deltaTime);
        UpdateStopBlend(s, deltaTime);
    }

    private void UpdateAim(float dt)
    {
        if (aimTarget <= 0f && aimBlend <= 0f) return; // 조준을 쓴 적이 없으면 Animator 값을 건드리지 않음
        aimBlend = aimRate > 0f ? Mathf.MoveTowards(aimBlend, aimTarget, aimRate * dt) : aimTarget;
        SetBool("IsAiming", aimTarget > 0f);
        SetFloat("AimBlend", aimBlend);
    }

    private void UpdateWalk(IntroCutsceneSettings s, float dt)
    {
        if (!walking)
        {
            MoveDirection = Vector3.zero;
            return;
        }

        Vector3 to = Flat(path[pathIndex] - player.transform.position);
        float dist = to.magnitude;
        bool last = pathIndex == path.Count - 1;

        if (lastDist - dist < 0.0002f) stuckTime += dt;
        else stuckTime = 0f;
        lastDist = dist;

        bool reached = dist <= (last ? ArriveEpsilon : PassRadius) || (last && dist <= 0.03f && stuckTime > 0f);
        if (!reached && stuckTime >= StuckSeconds)
        {
            Debug.LogWarning("[촬영] 플레이어가 길에 막혀 멈췄습니다(남은 거리 " + dist.ToString("0.00") + "m). 위치 표시 사이에 벽이나 소품이 있는지 확인해주세요.", shot);
            StopWalking(true);
            return;
        }
        if (reached)
        {
            if (last)
            {
                StopWalking(true);
                return;
            }
            pathIndex++;
            lastDist = float.MaxValue;
            stuckTime = 0f;
            return;
        }

        Vector3 dir = to / dist;
        lastWalkDir = dir;

        // 마지막 한 프레임에 지나치지 않도록 남은 거리만큼만 걷습니다(PlayerMovement가 속도를 곱함).
        float speed = stats != null ? Mathf.Max(0.01f, stats.MoveSpeed) : 2f;
        if (running && boost != null) speed *= Mathf.Max(1f, boost.CurrentSpeedMultiplier);
        if (SpeedOverride > 0f) speed = SpeedOverride;
        float step = speed * Mathf.Max(dt, 0.0001f);
        // 걷기는 마지막 지점 직전에 걸음을 줄여 걷기 -> 멈춤이 부드럽게 이어지게 합니다(뛰기는 그대로 달려와 멈춤).
        float slow = last && !running && s.arriveSlowDistance > 0f ? Mathf.Clamp(dist / s.arriveSlowDistance, s.arriveMinSpeed, 1f) : 1f;
        MoveDirection = dir * Mathf.Min(slow, Mathf.Clamp01(dist / step));

        player.transform.rotation = Quaternion.Slerp(player.transform.rotation, Quaternion.LookRotation(dir), s.turnSpeed * dt);
    }

    private void StopWalking(bool blendLegs)
    {
        if (walking && blendLegs && !running) animMove = 1f; // 걷다 멈춘 경우만 다리 자세를 서서히 줄임
        walking = false;
        running = false;
        ForceRun = false;
        SpeedOverride = 0f;
        MoveDirection = Vector3.zero;
        path.Clear();
    }

    private void UpdateTurn(float dt)
    {
        if (!turning || walking) return;
        turnTimer += dt;
        float t = turnDuration > 0f ? Mathf.Clamp01(turnTimer / turnDuration) : 1f;
        player.transform.rotation = Quaternion.Slerp(turnFrom, turnTo, Mathf.SmoothStep(0f, 1f, t));
        if (t >= 1f) turning = false;
    }

    // ------------------------------------------------------------------ 장전 / 포즈

    private void BeginReload(IntroCutsceneSettings s, float duration)
    {
        var shooting = player.GetComponent<PlayerShooting>();
        float clipLength = shooting != null && shooting.reloadClip != null ? shooting.reloadClip.length : 0f;
        SetFloat(s.reloadSpeedFloat, clipLength > 0f ? clipLength / duration : 1f);
        CrossFadeLayer(s.upperBodyLayerName, s.reloadStateName, s.reloadBlendIn, true);
        SetBool(s.reloadBool, true);
        HoldFace(SDFaceExpression.Reload, -1f);
        reloading = true;
        reloadTimer = duration;
    }

    private void UpdateReload(IntroCutsceneSettings s, float dt)
    {
        if (!reloading) return;
        reloadTimer -= dt;
        if (reloadTimer > 0f) return;
        reloading = false;
        SetBool(s.reloadBool, false); // 대기로 돌아가는 것은 Animator의 장전 -> 대기 전이에 맡김(게임과 같음)
        ReleaseFace(SDFaceExpression.Reload);
    }

    private void BeginPose(IntroCutsceneSettings s, bool fullBody)
    {
        if (reloading)
        {
            reloading = false;
            SetBool(s.reloadBool, false);
            ReleaseFace(SDFaceExpression.Reload);
        }

        fullBodyPosing = fullBody
            && HasLayerState(s.baseLayerName, s.fullBodyPoseStateName)
            && HasLayerState(s.upperBodyLayerName, s.fullBodyPoseStateName);
        if (fullBodyPosing)
        {
            CrossFadeLayer(s.baseLayerName, s.fullBodyPoseStateName, s.pickupBlendIn, true);
            CrossFadeLayer(s.upperBodyLayerName, s.fullBodyPoseStateName, s.pickupBlendIn, true);
        }
        else
        {
            if (fullBody) Debug.LogWarning("[촬영] Animator에서 전신 포즈 상태('" + s.fullBodyPoseStateName + "')를 찾지 못해 상체 획득 모션으로 대신합니다.");
            SetBool(s.pickupBool, true);
            if (!CrossFadeLayer(s.upperBodyLayerName, s.pickupStateName, s.pickupBlendIn, true)) SetTrigger(s.pickupTrigger);
        }
        HoldFace(SDFaceExpression.ItemPickup, -1f);
    }

    private void ReturnToIdle(IntroCutsceneSettings s, float duration)
    {
        reloading = false;
        SetBool(s.reloadBool, false);
        SetBool(s.pickupBool, false);
        ResetTrigger(s.pickupTrigger);
        CrossFadeLayer(s.upperBodyLayerName, s.idleStateName, duration, false);
        if (fullBodyPosing) CrossFadeLayer(s.baseLayerName, s.baseIdleStateName, duration, false);
        fullBodyPosing = false;

        // 표정은 자세가 다 돌아온 뒤에 풉니다.
        ReleaseFace(SDFaceExpression.Reload);
        if (heldFaces.ContainsKey(SDFaceExpression.ItemPickup)) heldFaces[SDFaceExpression.ItemPickup] = Mathf.Max(0.01f, duration);
    }

    // ------------------------------------------------------------------ 표정

    private void SetFace(string faceName, float duration)
    {
        if (face == null) return;
        if (faceName == "기본")
        {
            foreach (var pair in heldFaces) face.Release(pair.Key);
            heldFaces.Clear();
            return;
        }

        SDFaceExpression e;
        switch (faceName)
        {
            case "피격": e = SDFaceExpression.Hit; break;
            case "획득": e = SDFaceExpression.ItemPickup; break;
            case "장전": e = SDFaceExpression.Reload; break;
            case "사망": e = SDFaceExpression.Death; break;
            case "뛰기": e = SDFaceExpression.Run; break;
            default: return;
        }
        HoldFace(e, duration > 0f ? duration : -1f);
    }

    private void HoldFace(SDFaceExpression e, float seconds)
    {
        if (face == null) return;
        if (!heldFaces.ContainsKey(e)) face.Hold(e);
        heldFaces[e] = seconds;
    }

    private void ReleaseFace(SDFaceExpression e)
    {
        if (face == null || !heldFaces.Remove(e)) return;
        face.Release(e);
    }

    private void UpdateFaces(float dt)
    {
        if (heldFaces.Count == 0) return;
        faceBuffer.Clear();
        faceBuffer.AddRange(heldFaces.Keys);
        foreach (SDFaceExpression e in faceBuffer)
        {
            float remain = heldFaces[e];
            if (remain < 0f) continue;
            remain -= dt;
            if (remain <= 0f) ReleaseFace(e);
            else heldFaces[e] = remain;
        }
    }

    // ------------------------------------------------------------------ 조작 잠금 / Animator

    private void EnforceLock()
    {
        GameObject go = player.gameObject;
        var aim = go.GetComponent<PlayerAimAndAttack>();
        if (aim != null && aim.enabled)
        {
            // 조준 컴포넌트를 끄면 Animator의 조준 값이 마지막 상태로 남으므로 먼저 풀어둡니다.
            SetBool("IsAiming", false);
            SetFloat("AimBlend", 0f);
            aim.enabled = false;
        }
        Disable(go.GetComponent<PlayerShooting>());
        Disable(go.GetComponent<PlayerActiveItem>());
        Disable(go.GetComponent<PlayerDisposableItem>());
        Disable(go.GetComponent<PlayerFoodItem>());
        Disable(go.GetComponent<PlayerGrenadeThrower>());
        Disable(go.GetComponent<PlayerHackTool>());
    }

    private static void Disable(Behaviour behaviour)
    {
        if (behaviour != null && behaviour.enabled) behaviour.enabled = false;
    }

    // 걷다 멈춘 뒤: PlayerMovement가 이번 프레임에 넣은 이동 값(0)을 서서히 줄어드는 값으로 덮어써서 다리가 뚝 멈추지 않게 합니다.
    private void UpdateStopBlend(IntroCutsceneSettings s, float dt)
    {
        if (animMove <= 0f || walking) return;
        animMove = s.stopBlendDuration > 0f ? Mathf.MoveTowards(animMove, 0f, dt / s.stopBlendDuration) : 0f;
        float m = Mathf.SmoothStep(0f, 1f, animMove) * Mathf.Max(s.arriveMinSpeed, 0.05f);
        Vector3 local = player.transform.InverseTransformDirection(lastWalkDir) * m;
        SetFloat("MoveX", local.x);
        SetFloat("MoveZ", local.z);
        SetFloat("Speed", m);
    }

    private bool HasLayerState(string layerName, string stateName)
    {
        if (animator == null || animator.runtimeAnimatorController == null) return false;
        int layer = animator.GetLayerIndex(layerName);
        return layer >= 0 && animator.HasState(layer, Animator.StringToHash(layerName + "." + stateName));
    }

    // 지정한 레이어의 상태로 정해진 시간만큼 섞여 넘어갑니다. 상태를 못 찾으면 false.
    private bool CrossFadeLayer(string layerName, string stateName, float duration, bool fromStart)
    {
        if (!HasLayerState(layerName, stateName))
        {
            Debug.LogWarning("[촬영] Animator의 '" + layerName + "' 레이어에서 '" + stateName + "' 상태를 찾지 못했습니다.");
            return false;
        }
        int layer = animator.GetLayerIndex(layerName);
        int hash = Animator.StringToHash(layerName + "." + stateName);
        if (duration <= 0f) animator.Play(hash, layer, fromStart ? 0f : float.NegativeInfinity);
        else if (fromStart) animator.CrossFadeInFixedTime(hash, duration, layer, 0f);
        else animator.CrossFadeInFixedTime(hash, duration, layer);
        return true;
    }

    private bool HasParam(string name, AnimatorControllerParameterType type)
    {
        if (animator == null || animator.runtimeAnimatorController == null || string.IsNullOrEmpty(name)) return false;
        foreach (var p in animator.parameters)
        {
            if (p.name == name && p.type == type) return true;
        }
        return false;
    }

    private void SetBool(string name, bool v) { if (HasParam(name, AnimatorControllerParameterType.Bool)) animator.SetBool(name, v); }
    private void SetFloat(string name, float v) { if (HasParam(name, AnimatorControllerParameterType.Float)) animator.SetFloat(name, v); }
    private void SetTrigger(string name) { if (HasParam(name, AnimatorControllerParameterType.Trigger)) animator.SetTrigger(name); }
    private void ResetTrigger(string name) { if (HasParam(name, AnimatorControllerParameterType.Trigger)) animator.ResetTrigger(name); }

    // ------------------------------------------------------------------ 도우미

    // CharacterController는 켜진 채로 위치를 바꾸면 무시될 수 있어 잠깐 끄고 옮깁니다. 바닥에 끼지 않게 살짝 위에 놓습니다.
    private void Teleport(Vector3 position)
    {
        bool wasEnabled = controller != null && controller.enabled;
        if (wasEnabled) controller.enabled = false;
        player.transform.position = position + Vector3.up * 0.02f;
        if (wasEnabled) controller.enabled = true;
    }

    private ShotMark Find(string name, ShotCue cue)
    {
        ShotMark mark = shot != null ? shot.FindMark(name) : null;
        if (mark == null)
        {
            Debug.LogError("[촬영] 장면 '" + (shot != null ? shot.name : "?") + "' 대본 " + cue.line + "번째 줄: '" + name + "' 표시를 이 장면의 Marks에서 찾지 못해 이 줄을 건너뜁니다.", shot);
        }
        return mark;
    }

    private static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v;
    }

    private static Quaternion YawOnly(Quaternion rotation)
    {
        Vector3 forward = Flat(rotation * Vector3.forward);
        return forward.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(forward.normalized) : Quaternion.identity;
    }
}
