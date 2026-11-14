using System.Collections.Generic;
using UnityEngine;

// [촬영 스튜디오 - 장면 재생]
// 대본(ShotScript)의 명령을 시간 순서대로 실행해 촬영 카메라를 움직입니다. CutsceneStudio가 매 프레임 Tick을 부릅니다.
// 카메라 상태는 "기본 위치/방향/화각"(전환/이동/돌기가 정함) 위에 바라보기, 줌, 흔들림을 차례로 얹어 계산합니다.
// 그래서 이동하는 도중에 바라보기나 줌을 겹쳐 써도 서로 덮어쓰지 않습니다.
// 플레이어 줄은 StudioPlayerActor에 넘기고, 대상 이름 '플레이어'는 그 배우의 위치로 읽습니다.
public class ShotPlayback
{
    private readonly ShotScript script;
    private readonly StudioShot shot;
    private readonly Camera cam;
    private readonly StudioPlayerActor actor; // 플레이어가 없는 장면이면 null
    private readonly StudioEnemyActors enemies; // 적 배우(없어도 빈 채로 있음)

    private int nextCue;
    private float time;

    // 기본 상태(전환/이동/돌기의 결과)
    private Vector3 pos;
    private Quaternion rot;
    private float fov;

    // 구도 사이 이동
    private bool moving;
    private readonly List<Vector3> pathPos = new List<Vector3>();
    private readonly List<Quaternion> pathRot = new List<Quaternion>();
    private readonly List<float> pathFov = new List<float>();
    private float moveStart, moveDuration;
    private ShotEase moveEase;

    // 돌기
    private bool orbiting;
    private Transform orbitTarget;
    private Vector3 orbitOffset;
    private Quaternion orbitStartRot;
    private float orbitAngle, orbitRise, orbitStart, orbitDuration;
    private ShotEase orbitEase;

    // 궤도(대상 기준 각도/거리/높이/시선을 시간에 따라 이어 움직임) - 대본의 궤도 줄 전체를 미리 모아 둠
    private readonly List<float> trackTimes = new List<float>();
    private readonly List<float> trackAngle = new List<float>();
    private readonly List<float> trackDistance = new List<float>();
    private readonly List<float> trackHeight = new List<float>();
    private readonly List<float> trackLook = new List<float>();
    private bool trackActive;
    private Transform trackTarget;
    private float trackYaw; // 궤도를 시작한 순간 대상이 향한 방향(도). 도는 중에 대상이 몸을 돌려도 카메라가 휘둘리지 않게 고정.

    // 경로(정해진 시간에 구도들을 지나감) - 대본의 경로 줄 전체를 미리 모아 둠. 방향은 좌우각/상하각으로 나눠 이어서 기울어지지 않게 함.
    // 위치와 방향은 따로 이어서, "먼저 돌아선 다음 가속"처럼 서로 다른 박자로 움직일 수 있습니다.
    // 시간 줄(세상이 흐르는 빠르기)
    private readonly List<float> speedTimes = new List<float>();
    private readonly List<float> speedValues = new List<float>();

    private readonly List<float> pathPosTimes = new List<float>();
    private readonly List<float> pathKeyX = new List<float>();
    private readonly List<float> pathKeyY = new List<float>();
    private readonly List<float> pathKeyZ = new List<float>();
    private readonly List<bool> pathPosStops = new List<bool>();
    private readonly List<float> pathRotTimes = new List<float>();
    private readonly List<float> pathKeyYaw = new List<float>();
    private readonly List<float> pathKeyPitch = new List<float>();
    private readonly List<float> pathKeyFov = new List<float>();
    private readonly List<bool> pathRotStops = new List<bool>();
    private bool pathActive;

    // 따라가기(대상과의 거리/방향을 유지)
    private Transform followTarget;
    private Vector3 followOffset;

    // 바라보기(가중치 0 = 기본 방향, 1 = 대상을 봄)
    private Transform lookTarget;
    private float lookHeight;
    private float lookFrom, lookTo, lookStart, lookDuration;

    // 줌(이동/전환이 새로 시작되면 풀림)
    private bool zoomActive;
    private float zoomFrom, zoomTo, zoomStart, zoomDuration;
    private ShotEase zoomEase;

    // 흔들림
    private float shakeAmplitude, shakeStart, shakeDuration;

    // 화면 어둡게/밝게 (1 = 완전히 검정)
    private float fadeFrom, fadeTo, fadeStart, fadeDuration;

    public float CurrentTime { get { return time; } }
    public float Duration { get { return script.endTime; } }
    // 대본에 '끝'이 없으면 플레이어가 걷기를 마칠 때까지 기다립니다(걷는 시간은 거리로 정해져 미리 알 수 없음).
    public bool Finished
    {
        get { return time >= script.endTime && (script.hasExplicitEnd || actor == null || !actor.IsBusy); }
    }
    public float FadeAlpha { get; private set; }

    // 대본에 시간 줄이 있는지 / 지금 장면 시간의 세상 빠르기(1 = 게임 속도)
    public bool HasWorldSpeed { get { return speedTimes.Count > 0; } }
    public float WorldSpeed
    {
        get
        {
            if (speedTimes.Count == 0) return 1f;
            float v = ShotScript.SampleKeys(speedTimes, speedValues, time);
            return Mathf.Clamp(v, ShotScript.MinWorldSpeed, ShotScript.MaxWorldSpeed);
        }
    }

    public ShotPlayback(ShotScript script, StudioShot shot, Camera cam, StudioPlayerActor actor, StudioEnemyActors enemies)
    {
        this.script = script;
        this.shot = shot;
        this.cam = cam;
        this.actor = actor;
        this.enemies = enemies;

        // 궤도 줄: 생략한 값은 앞 줄 값을 잇습니다(맨 처음 기본값 = 거리 1.2 / 높이 0.4 / 시선 0.25).
        float distance = 1.2f, height = 0.4f, look = 0.25f;
        foreach (ShotCue cue in script.cues)
        {
            if (cue.type != ShotCueType.OrbitKey) continue;
            if (!float.IsNaN(cue.distance)) distance = cue.distance;
            if (cue.hasHeight) height = cue.height;
            if (!float.IsNaN(cue.look)) look = cue.look;
            trackTimes.Add(cue.time);
            trackAngle.Add(cue.value);
            trackDistance.Add(distance);
            trackHeight.Add(height);
            trackLook.Add(look);
        }

        // 시간 줄: 빠르기가 넘치거나 모자라지 않게(앞뒤 값 사이에서만) 부드럽게 잇습니다.
        foreach (ShotCue cue in script.cues)
        {
            if (cue.type != ShotCueType.WorldSpeed) continue;
            speedTimes.Add(cue.time);
            speedValues.Add(cue.value);
        }

        // 경로 줄: 위치 지점과 방향 지점을 따로 모읍니다(좌우각은 앞 지점에서 가까운 쪽으로 이어지게 풀어서).
        foreach (ShotCue cue in script.cues)
        {
            if (cue.type != ShotCueType.PathKey) continue;
            ShotMark mark = Find(cue.marks[0], cue);
            if (mark == null) continue;

            if (!cue.rotationOnly)
            {
                Vector3 p = mark.transform.position;
                if (cue.fraction >= 0f && cue.marks.Count > 1)
                {
                    ShotMark to = Find(cue.marks[1], cue);
                    if (to == null) continue;
                    p = Vector3.Lerp(p, to.transform.position, cue.fraction);
                }
                pathPosTimes.Add(cue.time);
                pathKeyX.Add(p.x);
                pathKeyY.Add(p.y);
                pathKeyZ.Add(p.z);
                pathPosStops.Add(cue.stop);
            }

            if (!cue.positionOnly)
            {
                Vector3 f = mark.transform.forward;
                float yaw = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
                float pitch = -Mathf.Asin(Mathf.Clamp(f.y, -1f, 1f)) * Mathf.Rad2Deg;
                if (pathKeyYaw.Count > 0)
                {
                    float prev = pathKeyYaw[pathKeyYaw.Count - 1];
                    yaw = prev + Mathf.DeltaAngle(prev, yaw);
                }
                pathRotTimes.Add(cue.time);
                pathKeyYaw.Add(yaw);
                pathKeyPitch.Add(pitch);
                pathKeyFov.Add(mark.fieldOfView);
                pathRotStops.Add(cue.stop);
            }
        }
    }

    // 장면을 처음(0초)으로 맞춥니다. 0초 명령(첫 구도 등)이 바로 적용됩니다.
    public void Begin()
    {
        nextCue = 0;
        time = 0f;
        pos = cam.transform.position;
        rot = cam.transform.rotation;
        fov = cam.fieldOfView;
        moving = false;
        orbiting = false;
        followTarget = null;
        trackActive = false;
        pathActive = false;
        lookTarget = null;
        lookFrom = lookTo = 0f;
        lookDuration = 0f;
        zoomActive = false;
        shakeDuration = 0f;
        fadeFrom = fadeTo = 0f;
        fadeDuration = 0f;
        Advance();
        Apply();
    }

    public void Tick(float deltaTime)
    {
        if (Finished) return;
        time += Mathf.Max(0f, deltaTime);
        if (script.hasExplicitEnd && time > script.endTime) time = script.endTime;
        Advance();
        Apply();
    }

    private void Advance()
    {
        while (nextCue < script.cues.Count && script.cues[nextCue].time <= time)
        {
            ShotCue cue = script.cues[nextCue];
            nextCue++;
            EvaluateMotion(cue.time); // 새 명령이 "지금 위치"를 쓸 수 있게 그 순간까지 먼저 진행
            Fire(cue);
        }
        EvaluateMotion(time);
    }

    private void Fire(ShotCue cue)
    {
        if (cue.IsPlayerCue)
        {
            if (actor != null) actor.Execute(cue);
            else Warn(cue, "플레이어가 없어 이 줄을 건너뜁니다(CutsceneStudio의 Player Prefab 칸을 확인해주세요).");
            return;
        }
        if (cue.IsActorCue)
        {
            if (enemies != null) enemies.Execute(cue);
            return;
        }

        switch (cue.type)
        {
            case ShotCueType.CameraCut:
            {
                ShotMark mark = Find(cue.marks[0], cue);
                if (mark == null) return;
                pos = mark.transform.position;
                rot = mark.transform.rotation;
                fov = mark.fieldOfView;
                moving = false;
                orbiting = false;
                followTarget = null;
                trackActive = false;
                pathActive = false;
                zoomActive = false;
                break;
            }
            case ShotCueType.CameraMove:
            {
                var marks = new List<ShotMark>();
                foreach (string name in cue.marks)
                {
                    ShotMark mark = Find(name, cue);
                    if (mark == null) return;
                    marks.Add(mark);
                }
                if (!cue.fromCurrent && marks.Count < 2)
                {
                    Warn(cue, "이동할 구도가 2개 이상 필요합니다.");
                    return;
                }

                float startFov = CurrentFov(cue.time);
                zoomActive = false;
                orbiting = false;
                followTarget = null;
                trackActive = false;
                pathActive = false;
                pathPos.Clear();
                pathRot.Clear();
                pathFov.Clear();
                if (cue.fromCurrent)
                {
                    pathPos.Add(pos);
                    pathRot.Add(rot);
                    pathFov.Add(startFov);
                }
                foreach (ShotMark mark in marks)
                {
                    pathPos.Add(mark.transform.position);
                    pathRot.Add(mark.transform.rotation);
                    pathFov.Add(mark.fieldOfView);
                }
                moveStart = cue.time;
                moveDuration = cue.duration;
                moveEase = cue.ease;
                moving = true;
                EvaluateMotion(cue.time);
                break;
            }
            case ShotCueType.LookAt:
            {
                Transform target = ResolveTarget(cue.target, cue);
                if (target == null) return;
                lookFrom = LookWeight(cue.time);
                lookTarget = target;
                lookHeight = cue.height;
                lookTo = 1f;
                lookStart = cue.time;
                lookDuration = cue.duration;
                break;
            }
            case ShotCueType.LookAtOff:
                lookFrom = LookWeight(cue.time);
                lookTo = 0f;
                lookStart = cue.time;
                lookDuration = cue.duration;
                break;
            case ShotCueType.Orbit:
            {
                Transform target = ResolveTarget(cue.target, cue);
                if (target == null) return;
                moving = false;
                followTarget = null;
                trackActive = false;
                pathActive = false;
                orbitTarget = target;
                orbitOffset = pos - target.position;
                orbitStartRot = rot;
                orbitAngle = cue.value;
                orbitRise = cue.height;
                orbitStart = cue.time;
                orbitDuration = cue.duration;
                orbitEase = cue.ease;
                orbiting = true;
                EvaluateMotion(cue.time);
                break;
            }
            case ShotCueType.FollowOn:
            {
                Transform target = ResolveTarget(cue.target, cue);
                if (target == null) return;
                moving = false;
                orbiting = false;
                trackActive = false;
                pathActive = false;
                followTarget = target;
                followOffset = pos - target.position;
                break;
            }
            case ShotCueType.OrbitKey:
            {
                if (trackActive) return; // 이미 궤도 중이면 다음 줄은 이어지는 지점일 뿐
                Transform target = ResolveTarget(cue.target, cue);
                if (target == null) return;
                Vector3 forward = target.forward;
                forward.y = 0f;
                trackYaw = forward.sqrMagnitude > 0.0001f ? Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg : 0f;
                trackTarget = target;
                trackActive = true;
                pathActive = false;
                moving = false;
                orbiting = false;
                followTarget = null;
                EvaluateMotion(cue.time);
                break;
            }
            case ShotCueType.PathKey:
                if (pathActive || (pathPosTimes.Count == 0 && pathRotTimes.Count == 0)) return; // 이미 경로 중이면 다음 줄은 이어지는 지점일 뿐
                pathActive = true;
                zoomActive = false; // 화면 넓이(화각)도 찍어 둔 구도의 값을 그대로 따름
                trackActive = false;
                moving = false;
                orbiting = false;
                followTarget = null;
                EvaluateMotion(cue.time);
                break;
            case ShotCueType.FollowOff:
                followTarget = null;
                break;
            case ShotCueType.Zoom:
                zoomFrom = CurrentFov(cue.time);
                zoomTo = cue.value;
                zoomStart = cue.time;
                zoomDuration = cue.duration;
                zoomEase = cue.ease;
                zoomActive = true;
                break;
            case ShotCueType.Shake:
                shakeAmplitude = cue.value;
                shakeStart = cue.time;
                shakeDuration = cue.duration;
                break;
            case ShotCueType.FadeOut:
            case ShotCueType.FadeIn:
                fadeFrom = FadeAt(cue.time);
                fadeTo = cue.type == ShotCueType.FadeOut ? 1f : 0f;
                fadeStart = cue.time;
                fadeDuration = cue.duration;
                break;
        }
    }

    // 이동/돌기를 t초 시점까지 진행해 기본 위치/방향/화각을 갱신합니다.
    private void EvaluateMotion(float t)
    {
        if (pathActive)
        {
            if (pathPosTimes.Count > 0)
            {
                pos = new Vector3(
                    ShotScript.SampleKeysFlowing(pathPosTimes, pathKeyX, pathPosStops, t),
                    ShotScript.SampleKeysFlowing(pathPosTimes, pathKeyY, pathPosStops, t),
                    ShotScript.SampleKeysFlowing(pathPosTimes, pathKeyZ, pathPosStops, t));
            }
            if (pathRotTimes.Count > 0)
            {
                rot = Quaternion.Euler(
                    ShotScript.SampleKeysFlowing(pathRotTimes, pathKeyPitch, pathRotStops, t),
                    ShotScript.SampleKeysFlowing(pathRotTimes, pathKeyYaw, pathRotStops, t),
                    0f);
                fov = ShotScript.SampleKeysFlowing(pathRotTimes, pathKeyFov, pathRotStops, t);
            }
        }
        else if (trackActive && trackTarget != null)
        {
            float angle = ShotScript.SampleKeys(trackTimes, trackAngle, t);
            float distance = ShotScript.SampleKeys(trackTimes, trackDistance, t);
            float height = ShotScript.SampleKeys(trackTimes, trackHeight, t);
            float look = ShotScript.SampleKeys(trackTimes, trackLook, t);
            Vector3 origin = trackTarget.position;
            Vector3 dir = Quaternion.Euler(0f, trackYaw + angle, 0f) * Vector3.forward;
            pos = origin + dir * distance + Vector3.up * height;
            Vector3 toLook = origin + Vector3.up * look - pos;
            if (toLook.sqrMagnitude > 0.000001f) rot = Quaternion.LookRotation(toLook.normalized, Vector3.up);
        }
        else if (moving)
        {
            float u = Progress(t, moveStart, moveDuration);
            float e = ShotScript.Ease(moveEase, u);
            SamplePath(e);
            if (u >= 1f) moving = false;
        }
        else if (orbiting)
        {
            float u = Progress(t, orbitStart, orbitDuration);
            float e = ShotScript.Ease(orbitEase, u);
            Quaternion turn = Quaternion.AngleAxis(orbitAngle * e, Vector3.up);
            if (orbitTarget != null)
            {
                // 대상이 움직이면(플레이어) 도는 중심도 같이 따라갑니다.
                pos = orbitTarget.position + turn * orbitOffset + Vector3.up * (orbitRise * e);
            }
            rot = turn * orbitStartRot;
            if (u >= 1f) orbiting = false;
        }
        else if (followTarget != null)
        {
            pos = followTarget.position + followOffset;
        }
    }

    private void SamplePath(float e)
    {
        int count = pathPos.Count;
        if (count == 0) return;
        if (count == 1)
        {
            pos = pathPos[0];
            rot = pathRot[0];
            fov = pathFov[0];
            return;
        }

        float s = Mathf.Clamp01(e) * (count - 1);
        int i = Mathf.Min(Mathf.FloorToInt(s), count - 2);
        float f = s - i;

        if (count == 2)
        {
            pos = Vector3.Lerp(pathPos[0], pathPos[1], f);
        }
        else
        {
            // 구도를 3개 이상 거칠 때는 꺾이지 않고 둥글게 지나가도록 곡선으로 잇습니다.
            Vector3 p0 = pathPos[Mathf.Max(i - 1, 0)];
            Vector3 p1 = pathPos[i];
            Vector3 p2 = pathPos[i + 1];
            Vector3 p3 = pathPos[Mathf.Min(i + 2, count - 1)];
            pos = CatmullRom(p0, p1, p2, p3, f);
        }
        rot = Quaternion.Slerp(pathRot[i], pathRot[i + 1], f);
        fov = Mathf.Lerp(pathFov[i], pathFov[i + 1], f);
    }

    private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;
        return 0.5f * ((2f * p1) + (p2 - p0) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (3f * p1 - p0 - 3f * p2 + p3) * t3);
    }

    private void Apply()
    {
        Quaternion finalRot = rot;
        float weight = LookWeight(time);
        if (weight > 0f && lookTarget != null)
        {
            Vector3 dir = lookTarget.position + Vector3.up * lookHeight - pos;
            if (dir.sqrMagnitude > 0.000001f)
            {
                finalRot = Quaternion.Slerp(rot, Quaternion.LookRotation(dir.normalized, Vector3.up), weight);
            }
        }

        Vector3 finalPos = pos;
        if (shakeDuration > 0f && time >= shakeStart && time < shakeStart + shakeDuration)
        {
            float remain = 1f - (time - shakeStart) / shakeDuration; // 끝으로 갈수록 잦아듦
            float k = shakeAmplitude * remain;
            float nx = (Mathf.PerlinNoise(time * 25f, 0.37f) - 0.5f) * 2f;
            float ny = (Mathf.PerlinNoise(0.71f, time * 25f) - 0.5f) * 2f;
            finalPos += finalRot * new Vector3(nx * k, ny * k, 0f);
        }

        cam.transform.SetPositionAndRotation(finalPos, finalRot);
        cam.fieldOfView = CurrentFov(time);
        FadeAlpha = FadeAt(time);
    }

    private float CurrentFov(float t)
    {
        if (!zoomActive) return fov;
        return Mathf.Lerp(zoomFrom, zoomTo, ShotScript.Ease(zoomEase, Progress(t, zoomStart, zoomDuration)));
    }

    private float LookWeight(float t)
    {
        return Mathf.Lerp(lookFrom, lookTo, ShotScript.Ease(ShotEase.Smooth, Progress(t, lookStart, lookDuration)));
    }

    private float FadeAt(float t)
    {
        return Mathf.Lerp(fadeFrom, fadeTo, Progress(t, fadeStart, fadeDuration));
    }

    private static float Progress(float t, float start, float duration)
    {
        if (duration <= 0f) return t >= start ? 1f : 0f;
        return Mathf.Clamp01((t - start) / duration);
    }

    // 대상 이름을 위치로 바꿉니다: '플레이어' = 플레이어 배우, 그 밖에는 이 장면의 표시 이름.
    private Transform ResolveTarget(string name, ShotCue cue)
    {
        if (name == ShotScript.PlayerName)
        {
            Transform root = actor != null ? actor.Root : null;
            if (root == null) Warn(cue, "플레이어가 없어 이 줄을 건너뜁니다(대본에 '플레이어 등장'이 있는지 확인해주세요).");
            return root;
        }
        Transform enemy = enemies != null ? enemies.FindRoot(name) : null;
        if (enemy != null) return enemy;
        ShotMark mark = Find(name, cue);
        return mark != null ? mark.transform : null;
    }

    private ShotMark Find(string name, ShotCue cue)
    {
        ShotMark mark = shot.FindMark(name);
        if (mark == null) Warn(cue, "'" + name + "' 표시를 이 장면의 Marks에서 찾지 못해 이 줄을 건너뜁니다.");
        return mark;
    }

    private void Warn(ShotCue cue, string message)
    {
        Debug.LogError("[촬영] 장면 '" + shot.name + "' 대본 " + cue.line + "번째 줄: " + message, shot);
    }
}
