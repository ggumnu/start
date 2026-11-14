using System;
using System.Collections.Generic;
using System.Globalization;

// [촬영 스튜디오 - 대본 해석]
// 장면 대본(텍스트 파일)을 읽어 "몇 초에 무엇을 한다"는 명령 목록으로 바꿉니다. 재생은 ShotPlayback이 합니다.
// Unity 기능을 쓰지 않는 순수 계산이라 대본 문법만 따로 검사할 수 있습니다.
//
// [대본 문법] 한 줄 = "시간  대상  명령". 낱말 사이는 띄어쓰기(여러 칸/탭 가능). // 또는 # 뒤는 메모.
//   시간: 장면 시작부터의 초(예: 2.5). +1.5 처럼 +를 붙이면 "바로 윗줄 시간 + 1.5초".
//   0     카메라  A                          구도 A로 바로 전환
//   0.5   카메라  A > B  3초  부드럽게        A에서 B로 3초 동안 이동 (A > B > C 처럼 여러 구도를 거칠 수 있음)
//   4     카메라  > C  2초                    지금 위치에서 C로 이동
//   0     카메라  바라보기  P1  높이 0.3  1초   P1을 계속 바라봄(높이 = 바라볼 지점의 높이, 마지막 숫자 = 시선이 옮겨가는 시간)
//   6     카메라  바라보기  해제  1초
//   2     카메라  돌기  P1  90도  3초  높이 1.5  P1 주위를 90도 돎(+ = 위에서 볼 때 시계 방향), 높이 = 도는 동안 올라가는 양
//   2     카메라  줌  40  2초                 화각(FOV)을 40으로
//   3     카메라  흔들림  0.1  0.5초           세기(m), 시간
//   0     카메라  궤도  플레이어  각도 150  거리 0.6  높이 0.3  시선 0.25
//                                          대상 기준 자리로 카메라를 둠. 구도 표시 없이 쓸 수 있음.
//                                          각도: 0 = 대상의 정면, 90 = 오른쪽, 180 = 뒤, 270 = 왼쪽 (360을 넘겨 계속 돌 수 있음)
//                                          거리/높이 = 대상 발밑 기준(m), 시선 = 카메라가 바라보는 높이. 생략하면 앞 줄 값 유지.
//                                          궤도 줄을 시간 순서로 여러 개 적으면 그 사이를 부드럽게 이어 움직임(줄 간격이 좁을수록 빠름).
//   0     카메라  경로  A                      "이 시간에 구도 A에 있다". 경로 줄을 시간 순서로 여러 개 적으면 그 구도들을
//   1.9   카메라  경로  B                      정해진 시간에 지나가며 위치/방향을 끊김 없이 부드럽게 잇습니다(구도에서 멈추거나 느려지지 않음).
//   4     카메라  경로  D  멈춤                 '멈춤'을 붙이면 그 구도에 서서히 느려지며 멈춰 섭니다.
//   2.4   카메라  경로  B > D  39%             위치만: B에서 D로 가는 길의 39% 지점을 이 시간에 지남(방향은 건드리지 않음)
//   2.5   카메라  경로  C  위치만               위치만: 구도 C의 자리를 지나되 방향은 건드리지 않음
//   2.1   카메라  경로방향  C                   방향만: 이 시간에 구도 C가 보는 방향을 봄(위치는 건드리지 않음)
//                                              위치와 방향을 따로 적으면 "먼저 돌아선 다음 가속" 같은 움직임을 만들 수 있습니다.
//   2     카메라  따라가기  플레이어            지금 거리/방향을 유지한 채 플레이어를 따라감 (해제: 카메라 따라가기 해제)
//   0     화면  밝게  1초  /  화면  어둡게  1초
//   0.2   시간  0.15배                         세상(플레이어/적/총알/이펙트)이 흐르는 빠르기. 시간 줄을 여러 개 적으면 그 사이를
//   0.6   시간  0.15배                         부드럽게 이어 바꿈(슬로모션 / 빨리감기). 카메라와 대본 시간은 영향을 받지 않음.
//                                              시간 줄이 없는 장면은 게임 속도 그대로.
//   -- 플레이어 (대본에 플레이어 줄이 하나라도 있으면 장면 시작부터 첫 '등장' 자리에 서 있음) --
//   0     플레이어  등장  P1                    P1 자리에, P1이 향한 방향으로 섬
//   1     플레이어  걷기  P1 > P2 > P3          게임과 같은 속도로 걸어감 ('걷기 P2'처럼 하나만 적으면 지금 자리에서 P2로)
//   1     플레이어  뛰기  P1 > P2               뛰기 모션(총 숨김, 흙먼지)
//   1     플레이어  뛰기  P1 > P2  3초          끝에 시간을 적으면 그 시간에 딱 도착하도록 이동 속도를 맞춤(걷기도 같음).
//                                              게임 속도(걷기 2, 뛰기 3 m/s)와 많이 다르면 발이 미끄러져 보일 수 있음.
//   4     플레이어  보기  P4  0.5초              몸을 P4 쪽으로 돌림 ('보기 카메라'면 카메라 쪽)
//   5     플레이어  장전  1.5초
//   7     플레이어  포즈  짜잔                   전신 포즈(입장 컷씬과 같음) / '포즈 획득' = 상체만 획득 모션
//   9     플레이어  대기  0.6초                  포즈/장전을 풀고 대기 자세로
//   3     플레이어  표정  획득  2초              기본 / 피격 / 획득 / 장전 / 사망 / 뛰기. 시간을 빼면 '표정 기본'까지 유지
//   0     플레이어  조준  0.1초                   총 조준 자세(게임의 우클릭 조준과 같은 자세, 바라보는 방향으로 총을 겨눔)
//   3     플레이어  조준  해제  0.2초              조준을 풀고 평소 자세로 (마지막 숫자 = 자세가 바뀌는 시간)
//   대상 이름 자리(바라보기/돌기/따라가기)에 '플레이어'를 쓸 수 있습니다.
//   -- 적 배우 (장면 안에 놓아둔 적 프리팹. 대상 자리에 그 오브젝트 이름을 적음. 장면에 적이 하나뿐이면 '보스' 또는 '적'으로 적어도 됨) --
//   0     보스  확산충전  2초                   전방위(확산) 공격의 충전만 2초 동안 보여 주고 쏘지 않음(충전이 끝난 모습으로 멈춰 있음)
//   0     보스  확산공격                        게임과 같은 전방위 공격(충전 → 발사 → 회전). '확산공격 충전 0.3초'처럼 충전 시간을 바꿀 수 있음
//   0     보스  확산공격  충전 2초  남은 0.12초    2초 충전 중 0.12초만 남은 모습(색/이펙트)에서 시작해 0.12초 뒤 발사
//                                              (앞 장면의 '확산충전 2초' 끝 모습에 이어 붙일 때)
//   1     보스  공격취소                        하던 충전/공격을 끊고 원래 색으로
//   0     보스  보호막생성                      (2레벨 보스) 보스방에 처음 들어왔을 때처럼 충전 이펙트 + 보호막이 커지며 생김.
//                                              '보호막생성 충전 1.2초'처럼 충전 시간을 바꿀 수 있음(빼면 프리팹 값 그대로)
//   0     보스  보호막두르기                    (2레벨 보스) 장면 첫 프레임부터 보호막이 다 생긴 모습으로 시작(충전/번쩍임 없이). 0초에만 씀
//   0     보스  포격모션                        (2레벨 보스) 게임의 포격 발동 모션 - 전면을 하늘로 들고, 돌며 수류탄을 쏘아 올리고, 내림
//   0     보스  포격모션  계속                  처음부터 하늘로 든 채 장면 끝까지 돌며 수류탄을 쏘아 올림(들기/내리기 없음).
//                                              도는 빠르기는 '시간' 줄을 따라감(느린 구간은 느리게, 빠른 구간은 빠르게). 0초에만 씀
//   0     보스  포격모션  계속  회전 3배         도는 빠르기만 게임의 3배(수류탄 간격은 그대로). '포격모션 회전 2배'처럼 '계속' 없이도 씀
//   1.2   보스  조준표시  P2                    (2레벨 보스) 위치 표시에 포격 조준 표시(바닥 주황 원 + 하늘로 점멸하는 레이저)만 띄움.
//                                              포탄/폭발/피해 없이 장면이 끝날 때까지 떠 있음
//   1     보스  보기  플레이어  0.5초            몸을 대상 쪽으로 돌림(위치 표시 / 플레이어 / 카메라)
//   1     보스  이동  E1 > E2  2초              위치 표시를 따라 미끄러지듯 이동(시간을 빼면 1초). '이동 E2'처럼 하나만 적으면 지금 자리에서
//   적 배우 이름도 카메라의 대상 이름 자리(바라보기/돌기/따라가기/궤도)에 쓸 수 있습니다.
//   8     끝                                  장면 끝(녹화도 여기서 멈춤). 없으면 마지막 명령이 끝난 0.5초 뒤.
//   속도 낱말: 부드럽게(기본) / 일정하게 / 가속 / 감속 / 돌진(짧게 확 가속한 뒤 길게 느려지며 멈춤)
public enum ShotEase { Smooth, Linear, EaseIn, EaseOut, Dash }

public enum ShotCueType
{
    CameraCut, CameraMove, LookAt, LookAtOff, Orbit, Zoom, Shake, FadeOut, FadeIn, End,
    FollowOn, FollowOff, OrbitKey, PathKey,
    WorldSpeed,
    PlayerSpawn, PlayerMove, PlayerLook, PlayerReload, PlayerPose, PlayerIdle, PlayerFace, PlayerAim,
    ActorRadial, ActorCancel, ActorLook, ActorMove, ActorShield, ActorShieldOn, ActorLaunch, ActorStrikeMark
}

public class ShotCue
{
    public float time;
    public int line;
    public ShotCueType type;
    public List<string> marks = new List<string>(); // 전환: 구도 1개 / 이동: 거쳐갈 구도들
    public bool fromCurrent;                        // 이동: 지금 카메라 위치에서 출발
    public string target;                           // 바라보기/돌기 대상
    public float duration;
    public ShotEase ease = ShotEase.Smooth;
    public float value;                             // 돌기 각도 / 줌 화각 / 흔들림 세기
    public float height;                            // 바라보기 높이 / 돌기 상승량
    public bool run;                                // 플레이어 이동: 뛰기
    public string text;                             // 플레이어 포즈 / 표정 이름
    public float distance = float.NaN;              // 궤도: 거리 (NaN = 앞 줄 값 유지)
    public float look = float.NaN;                  // 궤도: 시선 높이 (NaN = 앞 줄 값 유지)
    public bool hasHeight;                          // 궤도: 높이를 적었는지
    public bool stop;                               // 경로: 이 구도에서 멈춰 섬
    public bool positionOnly;                       // 경로: 위치만(방향은 그대로)
    public bool rotationOnly;                       // 경로방향: 방향만(위치는 그대로)
    public float fraction = -1f;                    // 경로 'B > D 39%': 두 구도 사이의 지점(0~1). -1 = 구도 자리 그대로

    public string actor;                            // 적 배우 줄: 배우 이름
    public bool hold;                               // 확산충전: 충전만 하고 쏘지 않음

    public float remain = -1f;                      // 확산공격 '남은': 충전이 남은 시간(-1 = 처음부터 충전)

    public bool IsPlayerCue { get { return type >= ShotCueType.PlayerSpawn && type <= ShotCueType.PlayerAim; } }
    public bool IsActorCue { get { return type >= ShotCueType.ActorRadial; } }

    public float EndTime { get { return time + Math.Max(0f, duration); } }
}

public class ShotScript
{
    public readonly List<ShotCue> cues = new List<ShotCue>();
    public readonly List<string> errors = new List<string>();
    public float endTime;
    public bool hasExplicitEnd;   // 대본에 '끝'이 있는지(없으면 플레이어가 걷기를 마칠 때까지 기다림)
    public bool usesPlayer;       // 플레이어 줄이 하나라도 있는지
    public bool usesWorldSpeed;   // 시간 줄이 하나라도 있는지

    public const string PlayerName = "플레이어";   // 대상 이름 자리에 쓰는 플레이어
    public const string CameraName = "카메라";     // '플레이어 보기 카메라'의 대상

    private const string SubjectCamera = "카메라";
    private const string SubjectScreen = "화면";
    private const string SubjectTime = "시간";
    private const string SubjectEnd = "끝";

    public static ShotScript Parse(string text)
    {
        var script = new ShotScript();
        if (text == null) text = "";
        text = text.Replace("﻿", "");

        string[] lines = text.Split('\n');
        float prevTime = 0f;
        bool hasEnd = false;
        float explicitEnd = 0f;

        for (int i = 0; i < lines.Length; i++)
        {
            int lineNo = i + 1;
            string line = StripComment(lines[i]).Replace("->", ">").Replace("→", ">").Replace(">", " > ");
            string[] t = line.Split(new[] { ' ', '\t', '\r', '　' }, StringSplitOptions.RemoveEmptyEntries);
            if (t.Length == 0) continue;

            float time;
            if (!TryTime(t[0], prevTime, out time))
            {
                script.Error(lineNo, "맨 앞 '" + t[0] + "'을(를) 시간(초)으로 읽을 수 없습니다. 예: 2.5 또는 +1");
                continue;
            }
            if (time < 0f)
            {
                script.Error(lineNo, "시간이 0보다 작습니다.");
                continue;
            }
            prevTime = time;

            if (t.Length < 2)
            {
                script.Error(lineNo, "시간 뒤에 대상(카메라 / 화면 / 끝)이 없습니다.");
                continue;
            }

            var cue = new ShotCue { time = time, line = lineNo };
            bool ok;
            switch (t[1])
            {
                case SubjectCamera: ok = script.ParseCamera(cue, t, lineNo); break;
                case SubjectScreen: ok = script.ParseScreen(cue, t, lineNo); break;
                case SubjectTime: ok = script.ParseWorldSpeed(cue, t, lineNo); break;
                case PlayerName: ok = script.ParsePlayer(cue, t, lineNo); break;
                case SubjectEnd:
                    cue.type = ShotCueType.End;
                    ok = script.NoExtra(t, 2, lineNo);
                    if (ok) { hasEnd = true; explicitEnd = time; }
                    break;
                default:
                    // 그 밖의 이름은 적 배우 이름으로 봅니다. 오타('카매라')를 잡을 수 있게 명령이 적 배우 명령일 때만 받습니다.
                    if (t.Length >= 3 && IsActorCommand(t[2]))
                    {
                        ok = script.ParseActor(cue, t, lineNo);
                    }
                    else
                    {
                        script.Error(lineNo, "'" + t[1] + "'은(는) 모르는 대상입니다. 쓸 수 있는 대상: 카메라, 플레이어, 화면, 시간, 끝, 적 배우 이름(명령: " + ActorCommands + ")");
                        ok = false;
                    }
                    break;
            }
            if (ok)
            {
                script.cues.Add(cue);
                if (cue.IsPlayerCue) script.usesPlayer = true;
                if (cue.type == ShotCueType.WorldSpeed) script.usesWorldSpeed = true;
            }
        }

        // 시간 순서로 정렬(같은 시간은 적힌 순서 유지)
        var ordered = new List<ShotCue>(script.cues);
        ordered.Sort((a, b) =>
        {
            int c = a.time.CompareTo(b.time);
            return c != 0 ? c : a.line.CompareTo(b.line);
        });
        script.cues.Clear();
        script.cues.AddRange(ordered);

        script.hasExplicitEnd = hasEnd;
        if (hasEnd)
        {
            script.endTime = explicitEnd;
        }
        else
        {
            float last = 0f;
            foreach (var c in script.cues) last = Math.Max(last, c.EndTime);
            script.endTime = last + 0.5f;
        }
        return script;
    }

    private bool ParseCamera(ShotCue cue, string[] t, int lineNo)
    {
        if (t.Length < 3)
        {
            Error(lineNo, "'카메라' 뒤에 구도 이름이나 명령이 없습니다.");
            return false;
        }

        switch (t[2])
        {
            case "바라보기": return ParseLookAt(cue, t, lineNo);
            case "돌기": return ParseOrbit(cue, t, lineNo);
            case "줌": return ParseZoom(cue, t, lineNo);
            case "흔들림": return ParseShake(cue, t, lineNo);
            case "따라가기": return ParseFollow(cue, t, lineNo);
            case "궤도": return ParseOrbitKey(cue, t, lineNo);
            case "경로": return ParsePathKey(cue, t, lineNo, false);
            case "경로방향": return ParsePathKey(cue, t, lineNo, true);
        }

        // 구도 전환 / 구도 사이 이동
        int i = 2;
        if (!ParsePath(cue, t, ref i, lineNo, out bool sawArrow)) return false;

        if (!sawArrow)
        {
            cue.type = ShotCueType.CameraCut;
            return NoExtra(t, i, lineNo);
        }

        cue.type = ShotCueType.CameraMove;
        if (i >= t.Length || !TryNumber(t[i], out cue.duration))
        {
            Error(lineNo, "카메라 이동에는 걸리는 시간이 필요합니다. 예: 카메라 A > B 3초");
            return false;
        }
        i++;
        return ParseTail(cue, t, i, lineNo, false);
    }

    // "A > B > C" 또는 "> B"(지금 자리에서 출발)를 읽습니다. i는 다음 낱말 위치로 옮겨집니다.
    private bool ParsePath(ShotCue cue, string[] t, ref int i, int lineNo, out bool sawArrow)
    {
        sawArrow = false;
        if (i >= t.Length)
        {
            Error(lineNo, "이름이 없습니다.");
            return false;
        }
        if (t[i] == ">")
        {
            cue.fromCurrent = true;
            sawArrow = true;
            i++;
            if (i >= t.Length || t[i] == ">")
            {
                Error(lineNo, "'>' 뒤에 이름이 없습니다.");
                return false;
            }
        }
        cue.marks.Add(t[i]);
        i++;
        while (i < t.Length && t[i] == ">")
        {
            sawArrow = true;
            if (i + 1 >= t.Length || t[i + 1] == ">")
            {
                Error(lineNo, "'>' 뒤에 이름이 없습니다.");
                return false;
            }
            cue.marks.Add(t[i + 1]);
            i += 2;
        }
        return true;
    }

    private bool ParsePathKey(ShotCue cue, string[] t, int lineNo, bool rotationOnly)
    {
        cue.type = ShotCueType.PathKey;
        cue.rotationOnly = rotationOnly;
        if (t.Length < 4 || t[3] == ">")
        {
            Error(lineNo, "'" + t[2] + "' 뒤에 구도 이름이 없습니다. 예: 카메라 경로 A");
            return false;
        }
        cue.marks.Add(t[3]);
        int i = 4;

        // 'B > D 39%' = 두 구도 사이의 지점(위치만)
        if (i < t.Length && t[i] == ">")
        {
            if (rotationOnly)
            {
                Error(lineNo, "'경로방향'에는 구도 이름 하나만 적습니다.");
                return false;
            }
            if (i + 2 >= t.Length || !t[i + 2].EndsWith("%", StringComparison.Ordinal) || !TryNumber(t[i + 2], out float percent)
                || percent < 0f || percent > 100f)
            {
                Error(lineNo, "두 구도 사이 지점은 '카메라 경로 B > D 39%'처럼 적습니다(0~100%).");
                return false;
            }
            cue.marks.Add(t[i + 1]);
            cue.fraction = percent / 100f;
            cue.positionOnly = true;
            i += 3;
        }

        while (i < t.Length)
        {
            if (t[i] == "멈춤") cue.stop = true;
            else if (t[i] == "위치만" && !rotationOnly) cue.positionOnly = true;
            else return NoExtra(t, i, lineNo);
            i++;
        }
        return true;
    }

    private bool ParseOrbitKey(ShotCue cue, string[] t, int lineNo)
    {
        cue.type = ShotCueType.OrbitKey;
        if (t.Length < 4)
        {
            Error(lineNo, "궤도는 '카메라 궤도 대상 각도 N 거리 N 높이 N 시선 N' 형태입니다. 예: 카메라 궤도 플레이어 각도 150 거리 0.6 높이 0.3");
            return false;
        }
        cue.target = t[3];
        bool hasAngle = false;
        for (int i = 4; i < t.Length; i += 2)
        {
            if (i + 1 >= t.Length || !TryNumber(t[i + 1], out float number))
            {
                Error(lineNo, "'" + t[i] + "' 뒤에 숫자가 없습니다.");
                return false;
            }
            switch (t[i])
            {
                case "각도": cue.value = number; hasAngle = true; break;
                case "거리": cue.distance = number; break;
                case "높이": cue.height = number; cue.hasHeight = true; break;
                case "시선": cue.look = number; break;
                default:
                    Error(lineNo, "'" + t[i] + "'을(를) 알 수 없습니다. 궤도에 쓸 수 있는 낱말: 각도, 거리, 높이, 시선");
                    return false;
            }
        }
        if (!hasAngle)
        {
            Error(lineNo, "궤도에는 '각도 N'이 꼭 필요합니다.");
            return false;
        }
        if (!float.IsNaN(cue.distance) && cue.distance <= 0f)
        {
            Error(lineNo, "거리는 0보다 커야 합니다.");
            return false;
        }
        return true;
    }

    // 시간-값 표(times는 오름차순)를 t 시점에서 읽습니다. 사이는 부드럽게 잇되, 적은 값을 넘어갔다 돌아오지 않게 합니다
    // (느린 구간 옆에 빠른 구간이 있어도 카메라가 지나쳤다가 되돌아오지 않도록 단조 보간을 씀).
    public static float SampleKeys(IList<float> times, IList<float> values, float t)
    {
        return SampleKeys(times, values, null, t);
    }

    // stops[i]가 true인 지점에서는 속도가 0이 되도록(그 지점에 서서히 멈춰 서도록) 잇습니다.
    public static float SampleKeys(IList<float> times, IList<float> values, IList<bool> stops, float t)
    {
        int n = times.Count;
        if (n == 0) return 0f;
        if (n == 1 || t <= times[0]) return values[0];
        if (t >= times[n - 1]) return values[n - 1];

        int i = 0;
        while (i < n - 2 && t >= times[i + 1]) i++;
        float h = times[i + 1] - times[i];
        if (h <= 0.00001f) return values[i + 1];

        float m0 = stops != null && stops[i] ? 0f : KeyTangent(times, values, i);
        float m1 = stops != null && stops[i + 1] ? 0f : KeyTangent(times, values, i + 1);
        float u = (t - times[i]) / h;
        float u2 = u * u;
        float u3 = u2 * u;
        return (2f * u3 - 3f * u2 + 1f) * values[i]
             + (u3 - 2f * u2 + u) * h * m0
             + (-2f * u3 + 3f * u2) * values[i + 1]
             + (u3 - u2) * h * m1;
    }

    // 경로용: 지점을 지날 때 멈추지 않고 그대로 흘러가도록 잇습니다.
    // SampleKeys는 방향이 바뀌는 지점마다 잠깐 멈추므로, 카메라가 쉬지 않고 움직여야 하는 경로에는 이쪽을 씁니다.
    // stops[i]가 true인 지점에서만 속도가 0이 됩니다.
    public static float SampleKeysFlowing(IList<float> times, IList<float> values, IList<bool> stops, float t)
    {
        int n = times.Count;
        if (n == 0) return 0f;
        if (n == 1 || t <= times[0]) return values[0];
        if (t >= times[n - 1]) return values[n - 1];

        int i = 0;
        while (i < n - 2 && t >= times[i + 1]) i++;
        float h = times[i + 1] - times[i];
        if (h <= 0.00001f) return values[i + 1];

        float m0 = stops != null && stops[i] ? 0f : FlowTangent(times, values, i);
        float m1 = stops != null && stops[i + 1] ? 0f : FlowTangent(times, values, i + 1);
        float u = (t - times[i]) / h;
        float u2 = u * u;
        float u3 = u2 * u;
        return (2f * u3 - 3f * u2 + 1f) * values[i]
             + (u3 - 2f * u2 + u) * h * m0
             + (-2f * u3 + 3f * u2) * values[i + 1]
             + (u3 - u2) * h * m1;
    }

    // 경로용 기울기:
    // - 값이 한쪽으로 계속 가는 지점: 지나쳤다 돌아오지 않는 기울기(느린 구간 옆의 빠른 구간에서도 튀지 않음)
    // - 방향이 바뀌는 지점(올라갔다 내려감 등): 멈추지 않고 둥글게 넘어가는 기울기
    // - 한쪽이 거의 평평한 지점(같은 값이 이어지다 변하기 시작): 0 - 평평한 구간에서 미리 반대로 흔들리는 것을 막음
    private static float FlowTangent(IList<float> times, IList<float> values, int i)
    {
        int n = times.Count;
        if (i <= 0) return Secant(times, values, 0);
        if (i >= n - 1) return Secant(times, values, n - 2);

        float h0 = times[i] - times[i - 1];
        float h1 = times[i + 1] - times[i];
        if (h0 <= 0.00001f || h1 <= 0.00001f) return 0f;
        float d0 = Secant(times, values, i - 1);
        float d1 = Secant(times, values, i);
        if (d0 * d1 > 0f) return 3f * (h0 + h1) / ((2f * h1 + h0) / d0 + (h1 + 2f * h0) / d1);

        float a0 = Math.Abs(d0);
        float a1 = Math.Abs(d1);
        if (Math.Min(a0, a1) < 0.1f * Math.Max(a0, a1)) return 0f;
        return (values[i + 1] - values[i - 1]) / (h0 + h1);
    }

    private static float KeyTangent(IList<float> times, IList<float> values, int i)
    {
        int n = times.Count;
        if (i <= 0) return Secant(times, values, 0);
        if (i >= n - 1) return Secant(times, values, n - 2);

        float h0 = times[i] - times[i - 1];
        float h1 = times[i + 1] - times[i];
        float d0 = Secant(times, values, i - 1);
        float d1 = Secant(times, values, i);
        if (h0 <= 0.00001f || h1 <= 0.00001f || d0 * d1 <= 0f) return 0f;
        return 3f * (h0 + h1) / ((2f * h1 + h0) / d0 + (h1 + 2f * h0) / d1);
    }

    private static float Secant(IList<float> times, IList<float> values, int i)
    {
        float h = times[i + 1] - times[i];
        return h <= 0.00001f ? 0f : (values[i + 1] - values[i]) / h;
    }

    private bool ParseFollow(ShotCue cue, string[] t, int lineNo)
    {
        if (t.Length < 4)
        {
            Error(lineNo, "'따라가기' 뒤에 대상 이름(또는 '해제')이 없습니다.");
            return false;
        }
        if (t[3] == "해제" || t[3] == "끄기")
        {
            cue.type = ShotCueType.FollowOff;
        }
        else
        {
            cue.type = ShotCueType.FollowOn;
            cue.target = t[3];
        }
        return NoExtra(t, 4, lineNo);
    }

    private bool ParsePlayer(ShotCue cue, string[] t, int lineNo)
    {
        if (t.Length < 3)
        {
            Error(lineNo, "'플레이어' 뒤에 명령이 없습니다. 쓸 수 있는 명령: 등장, 걷기, 뛰기, 보기, 장전, 포즈, 대기, 표정, 조준");
            return false;
        }

        int i = 3;
        switch (t[2])
        {
            case "등장":
                cue.type = ShotCueType.PlayerSpawn;
                if (t.Length < 4)
                {
                    Error(lineNo, "'등장' 뒤에 위치 이름이 없습니다. 예: 플레이어 등장 P1");
                    return false;
                }
                cue.target = t[3];
                return NoExtra(t, 4, lineNo);

            case "걷기":
            case "뛰기":
                cue.type = ShotCueType.PlayerMove;
                cue.run = t[2] == "뛰기";
                if (!ParsePath(cue, t, ref i, lineNo, out bool sawArrow)) return false;
                if (!sawArrow) cue.fromCurrent = true; // '걷기 P2' = 지금 자리에서 P2로
                cue.duration = 0f;                     // 0 = 게임 속도 그대로, 시간을 적으면 그 시간에 도착
                if (i < t.Length && TryNumber(t[i], out float moveTime))
                {
                    if (moveTime <= 0f)
                    {
                        Error(lineNo, "이동 시간은 0보다 커야 합니다.");
                        return false;
                    }
                    cue.duration = moveTime;
                    i++;
                }
                return NoExtra(t, i, lineNo);

            case "보기":
                cue.type = ShotCueType.PlayerLook;
                if (t.Length < 4)
                {
                    Error(lineNo, "'보기' 뒤에 대상 이름이 없습니다. 예: 플레이어 보기 P4  또는  플레이어 보기 카메라");
                    return false;
                }
                cue.target = t[3];
                cue.duration = 0.5f;
                return OptionalDuration(cue, t, 4, lineNo);

            case "장전":
                cue.type = ShotCueType.PlayerReload;
                cue.duration = 1.5f;
                return OptionalDuration(cue, t, 3, lineNo);

            case "포즈":
                cue.type = ShotCueType.PlayerPose;
                if (t.Length < 4 || (t[3] != "짜잔" && t[3] != "획득"))
                {
                    Error(lineNo, "'포즈' 뒤에는 '짜잔' 또는 '획득'이 와야 합니다.");
                    return false;
                }
                cue.text = t[3];
                return NoExtra(t, 4, lineNo);

            case "대기":
                cue.type = ShotCueType.PlayerIdle;
                cue.duration = 0.6f;
                return OptionalDuration(cue, t, 3, lineNo);

            case "조준":
                cue.type = ShotCueType.PlayerAim;
                cue.value = 1f;
                cue.duration = 0.1f;
                if (t.Length >= 4 && t[3] == "해제")
                {
                    cue.value = 0f;
                    cue.duration = 0.2f;
                    return OptionalDuration(cue, t, 4, lineNo);
                }
                return OptionalDuration(cue, t, 3, lineNo);

            case "표정":
                cue.type = ShotCueType.PlayerFace;
                if (t.Length < 4 || !IsFaceName(t[3]))
                {
                    Error(lineNo, "'표정' 뒤에는 기본 / 피격 / 획득 / 장전 / 사망 / 뛰기 중 하나가 와야 합니다.");
                    return false;
                }
                cue.text = t[3];
                cue.duration = 0f;
                return OptionalDuration(cue, t, 4, lineNo);
        }

        Error(lineNo, "'" + t[2] + "'은(는) 모르는 플레이어 명령입니다. 쓸 수 있는 명령: 등장, 걷기, 뛰기, 보기, 장전, 포즈, 대기, 표정, 조준");
        return false;
    }

    // 세상이 흐르는 빠르기: 1 = 게임 속도. 너무 작거나 크면 물리가 불안정해져 범위를 제한합니다.
    public const float MinWorldSpeed = 0.02f;
    public const float MaxWorldSpeed = 10f;

    private bool ParseWorldSpeed(ShotCue cue, string[] t, int lineNo)
    {
        if (t.Length < 3 || !TryNumber(t[2], out float speed))
        {
            Error(lineNo, "'시간' 뒤에 빠르기가 없습니다. 예: 시간 0.15배  (1배 = 게임 속도)");
            return false;
        }
        if (speed < MinWorldSpeed || speed > MaxWorldSpeed)
        {
            Error(lineNo, "시간 빠르기는 " + MinWorldSpeed + "배 ~ " + MaxWorldSpeed + "배 사이로 적어주세요.");
            return false;
        }
        cue.type = ShotCueType.WorldSpeed;
        cue.value = speed;
        return NoExtra(t, 3, lineNo);
    }

    private const string ActorCommands = "확산충전, 확산공격, 공격취소, 보기, 이동, 보호막생성, 보호막두르기, 포격모션, 조준표시";

    private static bool IsActorCommand(string word)
    {
        return word == "확산충전" || word == "확산공격" || word == "공격취소" || word == "보기" || word == "이동" || word == "보호막생성"
            || word == "보호막두르기" || word == "포격모션" || word == "조준표시";
    }

    private bool ParseActor(ShotCue cue, string[] t, int lineNo)
    {
        cue.actor = t[1];
        int i = 3;
        switch (t[2])
        {
            case "확산충전":
                cue.type = ShotCueType.ActorRadial;
                cue.hold = true;
                if (t.Length < 4 || !TryNumber(t[3], out float holdTime) || holdTime <= 0f)
                {
                    Error(lineNo, "'확산충전' 뒤에 충전 시간이 없습니다. 예: 보스 확산충전 2초");
                    return false;
                }
                cue.duration = holdTime;
                return NoExtra(t, 4, lineNo);

            case "확산공격":
                cue.type = ShotCueType.ActorRadial;
                cue.duration = 0f; // 0 = 프리팹에 적힌 충전 시간 그대로
                if (t.Length >= 4 && t[3] == "충전")
                {
                    if (t.Length < 5 || !TryNumber(t[4], out float chargeTime) || chargeTime <= 0f)
                    {
                        Error(lineNo, "'충전' 뒤에 시간이 없습니다. 예: 보스 확산공격 충전 0.3초");
                        return false;
                    }
                    cue.duration = chargeTime;
                    if (t.Length >= 6 && t[5] == "남은")
                    {
                        if (t.Length < 7 || !TryNumber(t[6], out float remain) || remain <= 0f || remain > chargeTime)
                        {
                            Error(lineNo, "'남은' 뒤에 충전 시간보다 짧은 시간을 적어주세요. 예: 보스 확산공격 충전 2초 남은 0.12초");
                            return false;
                        }
                        cue.remain = remain;
                        return NoExtra(t, 7, lineNo);
                    }
                    return NoExtra(t, 5, lineNo);
                }
                return NoExtra(t, 3, lineNo);

            case "보호막생성":
                cue.type = ShotCueType.ActorShield;
                cue.duration = 0f; // 0 = 프리팹에 적힌 충전 시간 그대로
                if (t.Length >= 4 && t[3] == "충전")
                {
                    if (t.Length < 5 || !TryNumber(t[4], out float shieldCharge) || shieldCharge <= 0f)
                    {
                        Error(lineNo, "'충전' 뒤에 시간이 없습니다. 예: 보스 보호막생성 충전 1.2초");
                        return false;
                    }
                    cue.duration = shieldCharge;
                    return NoExtra(t, 5, lineNo);
                }
                return NoExtra(t, 3, lineNo);

            case "보호막두르기":
                cue.type = ShotCueType.ActorShieldOn;
                if (cue.time > 0f)
                {
                    Error(lineNo, "'보호막두르기'는 장면을 보호막이 있는 모습으로 시작하는 명령이라 0초에만 쓸 수 있습니다. 도중에 생기게 하려면 '보호막생성'을 써주세요.");
                    return false;
                }
                return NoExtra(t, 3, lineNo);

            case "포격모션":
            {
                cue.type = ShotCueType.ActorLaunch;
                cue.value = 1f; // 도는 빠르기 배율
                int k = 3;
                if (k < t.Length && t[k] == "계속")
                {
                    if (cue.time > 0f)
                    {
                        Error(lineNo, "'포격모션 계속'은 하늘로 든 모습으로 장면을 시작하는 명령이라 0초에만 쓸 수 있습니다.");
                        return false;
                    }
                    cue.hold = true;
                    k++;
                }
                if (k < t.Length && t[k] == "회전")
                {
                    if (k + 1 >= t.Length || !TryNumber(t[k + 1], out float spinScale) || spinScale <= 0f)
                    {
                        Error(lineNo, "'회전' 뒤에 배율이 없습니다. 예: 보스 포격모션 계속 회전 3배");
                        return false;
                    }
                    cue.value = spinScale;
                    k += 2;
                }
                return NoExtra(t, k, lineNo);
            }

            case "조준표시":
                cue.type = ShotCueType.ActorStrikeMark;
                if (t.Length < 4)
                {
                    Error(lineNo, "'조준표시' 뒤에 위치 표시 이름이 없습니다. 예: 보스 조준표시 P2");
                    return false;
                }
                cue.target = t[3];
                return NoExtra(t, 4, lineNo);

            case "공격취소":
                cue.type = ShotCueType.ActorCancel;
                return NoExtra(t, 3, lineNo);

            case "보기":
                cue.type = ShotCueType.ActorLook;
                if (t.Length < 4)
                {
                    Error(lineNo, "'보기' 뒤에 대상 이름이 없습니다. 예: 보스 보기 플레이어");
                    return false;
                }
                cue.target = t[3];
                cue.duration = 0.5f;
                return OptionalDuration(cue, t, 4, lineNo);

            case "이동":
                cue.type = ShotCueType.ActorMove;
                if (!ParsePath(cue, t, ref i, lineNo, out bool sawArrow)) return false;
                if (!sawArrow) cue.fromCurrent = true;
                cue.duration = 1f;
                if (i < t.Length && TryNumber(t[i], out float moveTime))
                {
                    if (moveTime <= 0f)
                    {
                        Error(lineNo, "이동 시간은 0보다 커야 합니다.");
                        return false;
                    }
                    cue.duration = moveTime;
                    i++;
                }
                return NoExtra(t, i, lineNo);
        }
        return false;
    }

    public static bool IsFaceName(string name)
    {
        return name == "기본" || name == "피격" || name == "획득" || name == "장전" || name == "사망" || name == "뛰기";
    }

    private bool OptionalDuration(ShotCue cue, string[] t, int i, int lineNo)
    {
        if (i < t.Length && TryNumber(t[i], out float d))
        {
            if (d < 0f)
            {
                Error(lineNo, "시간이 0보다 작습니다.");
                return false;
            }
            cue.duration = d;
            i++;
        }
        return NoExtra(t, i, lineNo);
    }

    private bool ParseLookAt(ShotCue cue, string[] t, int lineNo)
    {
        if (t.Length < 4)
        {
            Error(lineNo, "'바라보기' 뒤에 대상 이름(또는 '해제')이 없습니다.");
            return false;
        }

        int i = 4;
        if (t[3] == "해제" || t[3] == "끄기")
        {
            cue.type = ShotCueType.LookAtOff;
        }
        else
        {
            cue.type = ShotCueType.LookAt;
            cue.target = t[3];
        }

        while (i < t.Length)
        {
            if (t[i] == "높이" && cue.type == ShotCueType.LookAt)
            {
                if (i + 1 >= t.Length || !TryNumber(t[i + 1], out cue.height))
                {
                    Error(lineNo, "'높이' 뒤에 숫자가 없습니다.");
                    return false;
                }
                i += 2;
            }
            else if (TryNumber(t[i], out float d))
            {
                cue.duration = d;
                i++;
            }
            else
            {
                Error(lineNo, "'" + t[i] + "'을(를) 알 수 없습니다.");
                return false;
            }
        }
        return true;
    }

    private bool ParseOrbit(ShotCue cue, string[] t, int lineNo)
    {
        cue.type = ShotCueType.Orbit;
        if (t.Length < 6 || !TryNumber(t[4], out cue.value) || !TryNumber(t[5], out cue.duration))
        {
            Error(lineNo, "돌기는 '카메라 돌기 대상 각도 시간' 순서입니다. 예: 카메라 돌기 P1 90도 3초");
            return false;
        }
        cue.target = t[3];
        return ParseTail(cue, t, 6, lineNo, true);
    }

    private bool ParseZoom(ShotCue cue, string[] t, int lineNo)
    {
        cue.type = ShotCueType.Zoom;
        if (t.Length < 4 || !TryNumber(t[3], out cue.value))
        {
            Error(lineNo, "줌은 '카메라 줌 화각 시간' 순서입니다. 예: 카메라 줌 40 2초");
            return false;
        }
        if (cue.value < 1f || cue.value > 170f)
        {
            Error(lineNo, "줌 화각은 1~170 사이여야 합니다.");
            return false;
        }
        int i = 4;
        if (i < t.Length && TryNumber(t[i], out float d))
        {
            cue.duration = d;
            i++;
        }
        return ParseTail(cue, t, i, lineNo, false);
    }

    private bool ParseShake(ShotCue cue, string[] t, int lineNo)
    {
        cue.type = ShotCueType.Shake;
        if (t.Length < 5 || !TryNumber(t[3], out cue.value) || !TryNumber(t[4], out cue.duration))
        {
            Error(lineNo, "흔들림은 '카메라 흔들림 세기 시간' 순서입니다. 예: 카메라 흔들림 0.1 0.5초");
            return false;
        }
        return NoExtra(t, 5, lineNo);
    }

    private bool ParseScreen(ShotCue cue, string[] t, int lineNo)
    {
        if (t.Length < 3 || (t[2] != "어둡게" && t[2] != "밝게"))
        {
            Error(lineNo, "'화면' 뒤에는 '어둡게' 또는 '밝게'가 와야 합니다.");
            return false;
        }
        cue.type = t[2] == "어둡게" ? ShotCueType.FadeOut : ShotCueType.FadeIn;
        int i = 3;
        if (i < t.Length && TryNumber(t[i], out float d))
        {
            cue.duration = d;
            i++;
        }
        return NoExtra(t, i, lineNo);
    }

    // 명령 끝의 속도 낱말(부드럽게 등)과 '높이 N'을 읽습니다.
    private bool ParseTail(ShotCue cue, string[] t, int i, int lineNo, bool allowHeight)
    {
        while (i < t.Length)
        {
            if (TryEase(t[i], out ShotEase ease))
            {
                cue.ease = ease;
                i++;
            }
            else if (allowHeight && t[i] == "높이")
            {
                if (i + 1 >= t.Length || !TryNumber(t[i + 1], out cue.height))
                {
                    Error(lineNo, "'높이' 뒤에 숫자가 없습니다.");
                    return false;
                }
                i += 2;
            }
            else
            {
                Error(lineNo, "'" + t[i] + "'을(를) 알 수 없습니다. 속도 낱말은 부드럽게 / 일정하게 / 가속 / 감속 / 돌진입니다.");
                return false;
            }
        }
        if (cue.duration < 0f)
        {
            Error(lineNo, "시간이 0보다 작습니다.");
            return false;
        }
        return true;
    }

    private bool NoExtra(string[] t, int i, int lineNo)
    {
        if (i >= t.Length) return true;
        Error(lineNo, "'" + t[i] + "'을(를) 알 수 없습니다.");
        return false;
    }

    private void Error(int lineNo, string message)
    {
        errors.Add(lineNo + "번째 줄: " + message);
    }

    private static string StripComment(string line)
    {
        int a = line.IndexOf("//", StringComparison.Ordinal);
        if (a >= 0) line = line.Substring(0, a);
        int b = line.IndexOf('#');
        if (b >= 0) line = line.Substring(0, b);
        return line;
    }

    private static bool TryTime(string token, float prevTime, out float time)
    {
        if (token.StartsWith("+", StringComparison.Ordinal))
        {
            if (TryNumber(token.Substring(1), out float delta))
            {
                time = prevTime + delta;
                return true;
            }
            time = 0f;
            return false;
        }
        return TryNumber(token, out time);
    }

    // "3", "3.5초", "90도", "-45도", "1.5m" 같은 숫자를 읽습니다.
    public static bool TryNumber(string token, out float value)
    {
        value = 0f;
        if (string.IsNullOrEmpty(token)) return false;
        string s = token;
        if (s.EndsWith("초", StringComparison.Ordinal) || s.EndsWith("도", StringComparison.Ordinal) || s.EndsWith("%", StringComparison.Ordinal) || s.EndsWith("배", StringComparison.Ordinal)) s = s.Substring(0, s.Length - 1);
        else if (s.EndsWith("m", StringComparison.Ordinal)) s = s.Substring(0, s.Length - 1);
        if (s.Length == 0) return false;
        return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryEase(string token, out ShotEase ease)
    {
        switch (token)
        {
            case "부드럽게": ease = ShotEase.Smooth; return true;
            case "일정하게": ease = ShotEase.Linear; return true;
            case "가속": ease = ShotEase.EaseIn; return true;
            case "감속": ease = ShotEase.EaseOut; return true;
            case "돌진": ease = ShotEase.Dash; return true;
        }
        ease = ShotEase.Smooth;
        return false;
    }

    // 진행도 0~1을 속도 낱말에 맞게 바꿉니다.
    public static float Ease(ShotEase ease, float u)
    {
        if (u <= 0f) return 0f;
        if (u >= 1f) return 1f;
        switch (ease)
        {
            case ShotEase.Linear: return u;
            case ShotEase.EaseIn: return u * u;
            case ShotEase.EaseOut: return 1f - (1f - u) * (1f - u);
            case ShotEase.Dash:
            {
                // 멈춘 상태에서 출발해 전체 시간의 1/4 지점에서 가장 빠르고(평균의 약 2배), 남은 3/4 동안 서서히 멈춤
                float r = 1f - u;
                return 1f - r * r * r * r * (1f + 4f * u);
            }
            default: return u * u * (3f - 2f * u);
        }
    }
}
