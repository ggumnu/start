using System.Collections.Generic;
using UnityEngine;

// SD 캐릭터의 표정(눈/입)을 바꾸고, 평소에는 눈을 깜빡이게 하는 컴포넌트입니다.
//
// [원리]
// 머리 앞의 얼굴 평면 두 장(Body_Eye, Body_Mouth)은 "MobArchive/Toon_Face_SD" 셰이더를 쓰고, 셰이더의
// "표시할 칸"(_Cell) 값에 따라 표정 시트의 한 칸을 그립니다. 이 컴포넌트는 지금 보여야 할 표정을 정해서
// 그 칸 번호를 렌더러마다 덮어씁니다(MaterialPropertyBlock - 재질 자체는 바꾸지 않으므로 다른 캐릭터에 영향 없음).
//
// [표정 요청 방법 - 다른 스크립트가 부르는 함수]
// - Hold(표정) / Release(표정): "그 상태인 동안" 유지할 때. 예) 장전 시작 Hold(Reload), 장전 끝 Release(Reload)
// - Play(표정, 초): 정해진 시간만 보여줄 때. 예) Play(Hit, 0.8f)
// - 사망처럼 계속 유지할 표정은 Hold만 하고 Release하지 않습니다. 부활/재시작 때는 ClearAll().
// 여러 표정이 동시에 요청되면 표정 세트의 우선순위가 가장 큰 표정이 보이고, 아무것도 없으면 기본 표정입니다.
// (게임 상황 - 피격/아이템 획득/장전/사망 - 과의 실제 연결은 SD 캐릭터를 플레이어 프리팹에 붙이는 작업 때 합니다.)
//
// [눈 깜빡임]
// 지금 표정이 "깜빡임" 허용(기본 표정)일 때만, 표정 세트에 정한 간격마다 눈만 감은 눈 칸으로 잠깐 바꿉니다.
//
// [명암 맞추기]
// 얼굴 평면이 머리와 같은 명암으로 보이도록, 시작할 때 머리(Body_Head) 재질의 명암 값(그림자 색 등)을 복사해 씁니다.
//
// [에디터 설정]
// 캐릭터 최상위 오브젝트(SD_AltBody_v02)에 붙이고 "표정 세트"만 넣으면 됩니다. 렌더러 칸은 비워두면 이름으로 찾습니다.
// [테스트] 플레이 중에 "테스트 모드"를 켜고 "테스트 표정"을 바꾸면 그 표정이 바로 보입니다.
[DisallowMultipleComponent]
public class SDFaceController : MonoBehaviour
{
    [Tooltip("표정별 눈/입 칸 번호와 깜빡임 설정이 들어 있는 표정 세트 에셋입니다.")]
    public SDFaceExpressionSet expressionSet;

    [Header("렌더러 (비워두면 이름으로 자동으로 찾습니다)")]
    [Tooltip("눈 평면 (기본 이름: Body_Eye)")]
    public Renderer eyeRenderer;

    [Tooltip("입 평면 (기본 이름: Body_Mouth)")]
    public Renderer mouthRenderer;

    [Tooltip("명암 값을 복사해 올 머리 (기본 이름: Body_Head)")]
    public Renderer headRenderer;

    [Tooltip("켜두면 시작할 때 머리 재질의 명암 값(그림자 색, 경계 위치 등)을 얼굴에도 똑같이 적용합니다.")]
    public bool copyShadingFromHead = true;

    [Header("테스트 (플레이 중에만 동작)")]
    [Tooltip("켜면 아래 '테스트 표정'이 보입니다(다른 표정보다 우선).")]
    public bool testMode;
    public SDFaceExpression testExpression = SDFaceExpression.Default;

    private static readonly int CellId = Shader.PropertyToID("_Cell");
    private static readonly string[] ShadingFloats =
        { "_ShadowThreshold", "_ShadowSmoothness", "_ShadeAmount", "_ReceiveShadowAmount", "_Brightness" };
    private static readonly string[] ShadingColors = { "_ShadowColor" };

    private readonly HashSet<SDFaceExpression> held = new HashSet<SDFaceExpression>();
    private readonly Dictionary<SDFaceExpression, float> timedUntil = new Dictionary<SDFaceExpression, float>();
    private readonly List<SDFaceExpression> expiredBuffer = new List<SDFaceExpression>();

    private MaterialPropertyBlock block;
    private float nextBlinkTime;
    private float blinkEndTime = -1f;
    private int shownEyeCell = -1;
    private int shownMouthCell = -1;

    // 지금 보이는 표정 (깜빡임은 표정이 아니라 기본 표정 안의 동작으로 봅니다)
    public SDFaceExpression CurrentExpression { get; private set; } = SDFaceExpression.Default;

    void Awake()
    {
        if (eyeRenderer == null) eyeRenderer = FindRenderer("Body_Eye");
        if (mouthRenderer == null) mouthRenderer = FindRenderer("Body_Mouth");
        if (headRenderer == null) headRenderer = FindRenderer("Body_Head");

        if (eyeRenderer == null || mouthRenderer == null)
        {
            Debug.LogWarning("SDFaceController: 얼굴 평면(Body_Eye / Body_Mouth)을 찾지 못했습니다. " +
                             "알바디 FBX에 두 평면이 있는지, 또는 렌더러 칸에 직접 넣었는지 확인해주세요.", this);
        }
        if (expressionSet == null)
        {
            Debug.LogWarning("SDFaceController: 표정 세트가 비어 있어 기본 표정(눈 1, 입 1)만 보입니다.", this);
        }

        if (copyShadingFromHead) CopyShadingFromHead();
        ScheduleNextBlink();
        Refresh(true);
    }

    void Update()
    {
        Refresh(false);
    }

    // ------------------------------------------------------------------ 다른 스크립트가 부르는 함수

    // 그 표정을 Release할 때까지 유지합니다.
    public void Hold(SDFaceExpression expression)
    {
        held.Add(expression);
        Refresh(false);
    }

    // Hold로 유지하던 표정을 끝냅니다.
    public void Release(SDFaceExpression expression)
    {
        held.Remove(expression);
        Refresh(false);
    }

    // 그 표정을 정해진 시간(초) 동안 보여줍니다. 이미 재생 중이면 더 긴 쪽으로 늘어납니다.
    public void Play(SDFaceExpression expression, float seconds)
    {
        float until = Time.time + Mathf.Max(0f, seconds);
        if (!timedUntil.TryGetValue(expression, out float current) || current < until)
        {
            timedUntil[expression] = until;
        }
        Refresh(false);
    }

    // 모든 표정 요청을 지우고 기본 표정으로 돌아갑니다(부활, 레벨 재시작 등).
    public void ClearAll()
    {
        held.Clear();
        timedUntil.Clear();
        Refresh(false);
    }

    // ------------------------------------------------------------------ 내부 처리

    private void Refresh(bool force)
    {
        float now = Time.time;

        // 시간이 끝난 표정 정리
        expiredBuffer.Clear();
        foreach (var pair in timedUntil)
        {
            if (pair.Value <= now) expiredBuffer.Add(pair.Key);
        }
        foreach (SDFaceExpression e in expiredBuffer) timedUntil.Remove(e);

        // 지금 보여야 할 표정 고르기 (우선순위가 가장 큰 것)
        SDFaceExpression best = SDFaceExpression.Default;
        int bestPriority = int.MinValue;
        if (testMode && Application.isPlaying) Consider(testExpression, int.MaxValue, ref best, ref bestPriority);
        foreach (SDFaceExpression e in held) Consider(e, GetPriority(e), ref best, ref bestPriority);
        foreach (var pair in timedUntil) Consider(pair.Key, GetPriority(pair.Key), ref best, ref bestPriority);
        CurrentExpression = best;

        SDFaceExpressionSet.Entry entry = expressionSet != null ? expressionSet.Find(best) : null;
        if (entry == null && expressionSet != null) entry = expressionSet.Find(SDFaceExpression.Default);

        int eyeCell = entry != null ? entry.eyeCell : 1;
        int mouthCell = entry != null ? entry.mouthCell : 1;

        // 눈 깜빡임 (깜빡임을 허용하는 표정일 때만)
        bool canBlink = entry != null && entry.allowBlink && expressionSet != null;
        if (canBlink)
        {
            if (now >= nextBlinkTime && now >= blinkEndTime)
            {
                blinkEndTime = now + expressionSet.blinkDuration;
                ScheduleNextBlink();
            }
            if (now < blinkEndTime) eyeCell = expressionSet.blinkEyeCell;
        }
        else
        {
            // 다른 표정이 끝나고 기본 표정으로 돌아왔을 때 곧바로 깜빡이지 않도록 다음 깜빡임을 다시 잡습니다.
            blinkEndTime = -1f;
            if (nextBlinkTime < now) ScheduleNextBlink();
        }

        if (force || eyeCell != shownEyeCell) { SetCell(eyeRenderer, eyeCell); shownEyeCell = eyeCell; }
        if (force || mouthCell != shownMouthCell) { SetCell(mouthRenderer, mouthCell); shownMouthCell = mouthCell; }
    }

    private static void Consider(SDFaceExpression e, int priority, ref SDFaceExpression best, ref int bestPriority)
    {
        if (priority > bestPriority)
        {
            best = e;
            bestPriority = priority;
        }
    }

    private int GetPriority(SDFaceExpression e)
    {
        SDFaceExpressionSet.Entry entry = expressionSet != null ? expressionSet.Find(e) : null;
        return entry != null ? entry.priority : 0;
    }

    private void ScheduleNextBlink()
    {
        float min = expressionSet != null ? expressionSet.blinkIntervalMin : 2f;
        float max = expressionSet != null ? expressionSet.blinkIntervalMax : 5f;
        nextBlinkTime = Time.time + Random.Range(min, max);
    }

    private void SetCell(Renderer r, int cell)
    {
        if (r == null) return;
        if (block == null) block = new MaterialPropertyBlock();
        r.GetPropertyBlock(block);
        block.SetFloat(CellId, cell);
        r.SetPropertyBlock(block);
        block.Clear();
    }

    // 머리 재질의 명암 값을 얼굴 평면 두 장에 똑같이 넣습니다.
    private void CopyShadingFromHead()
    {
        if (headRenderer == null || headRenderer.sharedMaterial == null) return;
        Material head = headRenderer.sharedMaterial;
        if (block == null) block = new MaterialPropertyBlock();

        foreach (Renderer r in new[] { eyeRenderer, mouthRenderer })
        {
            if (r == null || r.sharedMaterial == null) continue;
            r.GetPropertyBlock(block);
            foreach (string name in ShadingFloats)
            {
                if (head.HasProperty(name) && r.sharedMaterial.HasProperty(name)) block.SetFloat(name, head.GetFloat(name));
            }
            foreach (string name in ShadingColors)
            {
                if (head.HasProperty(name) && r.sharedMaterial.HasProperty(name)) block.SetColor(name, head.GetColor(name));
            }
            r.SetPropertyBlock(block);
            block.Clear();
        }
    }

    private Renderer FindRenderer(string objectName)
    {
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
        {
            if (t.name == objectName) return t.GetComponent<Renderer>();
        }
        return null;
    }
}
