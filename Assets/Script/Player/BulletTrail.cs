using System.Collections.Generic;
using UnityEngine;

// 꼬리를 붙일 대상의 종류입니다. 종류마다 BulletTrailSettings의 다른 템플릿을 씁니다.
public enum BulletTrailKind
{
    Bullet,   // 플레이어 총알 + 적 총알
    Grenade,  // 수류탄
}

// [탄환 꼬리(궤적) - 별똥별처럼 따라오는 꼬리]
// 총알/수류탄이 발사되는 순간(각자의 Launch() 끝) Attach()를 한 번 부르면, 설정 에셋(BulletTrailSettings)의
// 꼬리 템플릿을 복제해 그 탄환을 따라다니게 합니다.
//
// [탄환의 자식으로 붙이지 않는 이유]
// 탄환이 사라질 때(적 명중/사거리 끝/벽/해킹으로 제거/수류탄 폭발 등 - 지우는 코드가 여러 곳에 흩어져 있음) 자식이면
// 꼬리도 그 순간 같이 뚝 끊깁니다. 그래서 꼬리는 따로 만들어 매 프레임 위치만 따라가게 하고, 따라가던 탄환이
// 사라지면 꼬리가 스스로 마무리 연출을 한 뒤 지워집니다. 덕분에 탄환을 지우는 기존 코드는 하나도 고칠 필요가 없습니다.
//
// [사라질 때 - 고정된 선으로 바꿔 그리고 앞쪽 끝을 둥글게 다듬은 뒤 함께 옅어짐 (형빈 확정)]
// Trail Renderer의 앞쪽 끝은 일자로 잘린 단면이라, 날아가는 동안 둥근 탄환이 가려주던 그 단면이 탄환이 사라지면 드러나고
// 꼬리가 그 단면 쪽으로 빨려 들어가며 줄어들어 "투명한 벽 뒤로 사라지는" 것처럼 보였습니다. Trail Renderer는 점마다
// 생성 시각을 기억하고 시간이 지난 점을 지우는 구조라, 점을 다시 배치해 모양을 다듬으려 하면 새 점이 "이미 오래된 점"으로
// 판단돼 꼬리가 한 번에 사라지는 문제도 있었습니다. 그래서 탄환이 사라지는 순간:
//   1) 남은 꼬리의 점 위치를 읽어, 시간 개념이 없는 Line Renderer(같은 재질/색/굵기/정렬)로 그 자리에 똑같이 옮겨 그리고
//      Trail Renderer는 끕니다(화면에서 바뀌는 순간은 보이지 않음). Line Renderer는 원래 움직이지 않는 선이라 꼬리가
//      저절로 줄어들지 않습니다.
//   2) 점을 촘촘하고 고르게 다시 놓고, 굵기 곡선의 맨 앞 "총알 반지름" 길이 구간을 0에서 그 지점의 굵기까지 반원처럼
//      부풀어 오르게 바꿉니다 - 앞쪽 끝이 총알처럼 둥글고 볼록하게 마감됩니다.
//   3) 설정의 Release Fade Time 동안 선 전체의 투명도와 굵기를 함께 0까지 낮춘 뒤 지웁니다.
// 옮겨 그리지 못하는 드문 경우(남은 점이 너무 적음 등)에는 Trail Renderer 그대로 옅어지며 사라집니다. 날아가는 동안의
// 모습은 바뀌지 않습니다.
//
// [색/굵기]
// - 굵기: 템플릿 Trail Renderer의 Width(1 = 총알 지름)에 탄환의 실제 크기를 곱합니다. 크기 스탯이 바뀌거나 분열탄처럼
//   작아져도 자동으로 맞춰집니다.
// - 색: 설정의 "탄환 색 따라가기"가 켜져 있으면 탄환 재질 색(_BaseColor 또는 _Color)을 템플릿 색에 곱합니다.
//   투명도 변화는 템플릿 그대로입니다. 수류탄은 기본으로 꺼져 있어 템플릿 색(흰색) 그대로입니다.
public class BulletTrail : MonoBehaviour
{
    private static BulletTrailSettings cachedSettings;
    private static bool settingsLoaded;
    private static bool warnedMissing;

    // 같은 탄환에 꼬리가 두 번 붙지 않도록, 꼬리가 붙어 있는 탄환을 기억합니다.
    private static readonly HashSet<int> attachedTargets = new HashSet<int>();

    private Transform target;
    private int targetId;
    private TrailRenderer trail;
    private bool released;

    // 사라질 때 연출 설정
    private float releaseFadeTime;

    // 사라질 때 연출 상태 - 옅어지게 할 대상은 고정된 선(lineCopy)이고, 옮겨 그리지 못했으면 Trail Renderer 자체입니다.
    private bool fading;
    private float fadeElapsed;
    private LineRenderer lineCopy;
    private float baseWidthMultiplier;
    private GradientColorKey[] baseColorKeys;
    private GradientAlphaKey[] baseAlphaKeys;
    private GradientMode baseGradientMode;

    // 에디터에서 도메인 리로드를 끈 채 Play를 다시 눌러도 이전 Play의 값이 남지 않도록, Play 시작 때마다 비웁니다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlayStart()
    {
        cachedSettings = null;
        settingsLoaded = false;
        warnedMissing = false;
        attachedTargets.Clear();
    }

    private static BulletTrailSettings Settings
    {
        get
        {
            if (!settingsLoaded)
            {
                cachedSettings = Resources.Load<BulletTrailSettings>(BulletTrailSettings.ResourcePath);
                settingsLoaded = true;

                if (cachedSettings == null && !warnedMissing)
                {
                    warnedMissing = true;
                    Debug.Log("BulletTrail: Assets/Resources/BulletTrailSettings 에셋이 없어 탄환 꼬리를 표시하지 않습니다.");
                }
            }
            return cachedSettings;
        }
    }

    // 탄환이 발사된 직후(위치/크기/색이 정해진 뒤) 한 번 호출합니다. 설정이나 템플릿이 없으면 아무 일도 하지 않습니다.
    public static void Attach(GameObject projectile, BulletTrailKind kind)
    {
        if (projectile == null) return;

        BulletTrailSettings settings = Settings;
        if (settings == null) return;

        GameObject template = kind == BulletTrailKind.Grenade ? settings.grenadeTrailTemplate : settings.bulletTrailTemplate;
        if (template == null) return;

        int id = projectile.GetInstanceID();
        if (attachedTargets.Contains(id)) return;

        bool useProjectileColor = kind == BulletTrailKind.Grenade
            ? settings.grenadeUseProjectileColor
            : settings.bulletUseProjectileColor;

        Transform targetTransform = projectile.transform;
        GameObject trailObj = Instantiate(template, targetTransform.position, Quaternion.identity);
        trailObj.name = template.name + " (" + projectile.name + ")";

        TrailRenderer tr = trailObj.GetComponentInChildren<TrailRenderer>();
        if (tr == null)
        {
            Debug.LogWarning("BulletTrail: 꼬리 템플릿 '" + template.name + "'에 Trail Renderer가 없습니다.");
            Destroy(trailObj);
            return;
        }

        // 굵기: 템플릿 Width(1 = 총알 지름) x 탄환의 실제 지름.
        float diameter = Mathf.Max(targetTransform.lossyScale.x, 0.0001f);
        tr.widthMultiplier *= diameter;

        // 색: 템플릿 색에 탄환 색을 곱합니다(투명도 변화는 그대로).
        if (useProjectileColor && TryGetProjectileColor(projectile, out Color projectileColor))
        {
            tr.colorGradient = TintGradient(tr.colorGradient, projectileColor);
        }

        // 생성 위치에서 시작하도록 혹시 남아 있을 수 있는 점을 지웁니다.
        tr.Clear();

        BulletTrail follower = trailObj.AddComponent<BulletTrail>();
        follower.target = targetTransform;
        follower.targetId = id;
        follower.trail = tr;
        follower.releaseFadeTime = kind == BulletTrailKind.Grenade
            ? settings.grenadeReleaseFadeTime
            : settings.bulletReleaseFadeTime;
        attachedTargets.Add(id);
    }

    // [방 전환] projectile에 붙은 꼬리를 마무리 연출 없이 바로 지웁니다. 플레이어가 다른 방으로 가서 그 방의 탄환/수류탄을 숨길 때
    // 다른 방 위 허공에 꼬리가 남지 않도록 씁니다(Grenade 등). 붙은 꼬리가 없으면 아무 일도 하지 않습니다.
    public static void RemoveNow(GameObject projectile)
    {
        if (projectile == null) return;
        int id = projectile.GetInstanceID();
        if (!attachedTargets.Contains(id)) return;
        attachedTargets.Remove(id);

        foreach (var trailFollower in FindObjectsByType<BulletTrail>(FindObjectsSortMode.None))
        {
            if (trailFollower == null || trailFollower.released || trailFollower.targetId != id) continue;
            trailFollower.released = true;
            Destroy(trailFollower.gameObject);
        }
    }

    void LateUpdate()
    {
        if (fading)
        {
            UpdateFade();
            return;
        }

        if (released) return;

        // 따라가던 탄환이 사라졌거나 꺼졌으면 마무리 연출을 시작합니다.
        if (target == null || !target.gameObject.activeInHierarchy)
        {
            Release();
            return;
        }

        transform.position = target.position;
    }

    private void Release()
    {
        released = true;
        attachedTargets.Remove(targetId);

        if (trail == null || releaseFadeTime <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        // 더 늘어나지 않게 멈춥니다.
        trail.emitting = false;

        // 시간 개념이 없는 Line Renderer로 옮겨 그립니다(실패하면 Trail Renderer 그대로 옅어지게 함).
        HandOffToLine();

        // 옅어지게 할 대상의 지금 색/굵기를 기준값으로 기억합니다.
        Gradient g = lineCopy != null ? lineCopy.colorGradient : trail.colorGradient;
        baseColorKeys = g.colorKeys;
        baseAlphaKeys = g.alphaKeys;
        baseGradientMode = g.mode;
        baseWidthMultiplier = lineCopy != null ? lineCopy.widthMultiplier : trail.widthMultiplier;

        fadeElapsed = 0f;
        fading = true;

        // 안전장치: 어떤 경우에도 옅어지는 시간이 지나면 지워지도록 예약합니다.
        Destroy(gameObject, releaseFadeTime + 0.5f);
    }

    // 남은 꼬리를 Line Renderer로 옮겨 그립니다. 옮기지 못하면(점이 너무 적음 등) lineCopy는 비어 있고, 그때는 Trail
    // Renderer가 그대로 옅어집니다.
    private void HandOffToLine()
    {
        int count = trail.positionCount;
        if (count < 1) return;

        var points = new Vector3[count];
        count = trail.GetPositions(points);
        if (count < 1) return;

        // 지금 꼬리 오브젝트 위치 = 탄환이 마지막으로 있던 곳 = 꼬리 앞쪽 끝입니다. 배열의 어느 쪽이 앞쪽(최신)인지 판단합니다.
        Vector3 head = transform.position;
        bool newestIsLast = (points[count - 1] - head).sqrMagnitude <= (points[0] - head).sqrMagnitude;

        // 앞쪽 끝 → 뒤쪽 끝 순서로 정리합니다(겹친 점은 건너뜀).
        var line = new List<Vector3>(count + 1) { head };
        for (int i = 0; i < count; i++)
        {
            Vector3 p = newestIsLast ? points[count - 1 - i] : points[i];
            if ((p - line[line.Count - 1]).sqrMagnitude > 1e-8f) line.Add(p);
        }
        if (line.Count < 2) return;

        // 앞에서부터 잰 누적 길이
        var cumulative = new float[line.Count];
        for (int i = 1; i < line.Count; i++)
        {
            cumulative[i] = cumulative[i - 1] + Vector3.Distance(line[i - 1], line[i]);
        }
        float length = cumulative[line.Count - 1];
        if (length < 0.0001f) return;

        AnimationCurve widthCurve = trail.widthCurve;
        float radius = trail.widthMultiplier * widthCurve.Evaluate(0f) * 0.5f;
        bool round = radius > 0f;

        // 점 배치: 앞쪽 끝을 둥글게 다듬을 수 있도록 코 구간에 점이 충분히 들어가게 촘촘하고 고르게(반지름의 1/8 간격,
        // 최대 512개) 다시 놓습니다. 굵기가 0이라 둥글게 할 필요가 없으면 원래 점을 그대로 씁니다.
        Vector3[] linePoints;
        if (round)
        {
            float spacing = Mathf.Max(radius / 8f, 0.0005f);
            int newCount = Mathf.Clamp(Mathf.CeilToInt(length / spacing) + 1, 8, 512);
            linePoints = new Vector3[newCount];
            int seg = 0;
            for (int k = 0; k < newCount; k++)
            {
                float d = length * k / (newCount - 1);
                while (seg < line.Count - 2 && cumulative[seg + 1] < d) seg++;
                float segLen = cumulative[seg + 1] - cumulative[seg];
                float u = segLen > 1e-6f ? (d - cumulative[seg]) / segLen : 0f;
                linePoints[k] = Vector3.Lerp(line[seg], line[seg + 1], u);
            }

            widthCurve = MakeRoundHeadCurve(widthCurve, Mathf.Clamp(radius / length, 0.0001f, 0.5f));
        }
        else
        {
            linePoints = line.ToArray();
        }

        // 같은 오브젝트에는 렌더러를 두 개 붙일 수 없어 자식 오브젝트에 Line Renderer를 만듭니다.
        var lineObj = new GameObject("ReleasedTrailLine");
        lineObj.transform.SetParent(transform, false);

        LineRenderer lr = lineObj.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.sharedMaterials = trail.sharedMaterials;
        lr.alignment = trail.alignment;
        lr.textureMode = trail.textureMode;
        lr.numCapVertices = trail.numCapVertices;
        lr.numCornerVertices = trail.numCornerVertices;
        lr.shadowCastingMode = trail.shadowCastingMode;
        lr.receiveShadows = false;
        lr.sortingLayerID = trail.sortingLayerID;
        lr.sortingOrder = trail.sortingOrder;
        lr.generateLightingData = false;

        // Line Renderer의 굵기 곡선/색은 첫 점(0) → 마지막 점(1) 순서라, 앞쪽 끝을 첫 점으로 넣으면 Trail Renderer와 같은
        // 방향(0 = 앞쪽 끝)이 됩니다.
        lr.positionCount = linePoints.Length;
        lr.SetPositions(linePoints);
        lr.widthCurve = widthCurve;
        lr.widthMultiplier = trail.widthMultiplier;
        lr.colorGradient = trail.colorGradient;

        // 원래 꼬리는 끄고, 이제부터는 이 선이 옅어지며 사라집니다.
        trail.enabled = false;
        lineCopy = lr;
    }

    // 굵기 곡선의 맨 앞(0 ~ noseT) 구간을 반원 모양(0 → 그 지점의 원래 굵기)으로 바꾸고, 나머지는 원래 곡선 그대로 씁니다.
    private static AnimationCurve MakeRoundHeadCurve(AnimationCurve baseCurve, float noseT)
    {
        const int noseSteps = 8;
        float noseWidth = baseCurve.Evaluate(noseT);

        var keys = new List<Keyframe>();
        for (int i = 0; i <= noseSteps; i++)
        {
            float s = (float)i / noseSteps;
            float w = noseWidth * Mathf.Sqrt(Mathf.Max(0f, 1f - (1f - s) * (1f - s)));
            keys.Add(new Keyframe(noseT * s, w));
        }
        foreach (Keyframe key in baseCurve.keys)
        {
            if (key.time > noseT + 0.0001f)
            {
                keys.Add(new Keyframe(key.time, key.value, key.inTangent, key.outTangent));
            }
        }

        var curve = new AnimationCurve(keys.ToArray());
        for (int i = 0; i <= noseSteps; i++)
        {
            curve.SmoothTangents(i, 0f);
        }
        return curve;
    }

    // 남은 꼬리(고정된 선 또는 Trail Renderer) 전체의 투명도와 굵기를 함께 0까지 낮춥니다.
    private void UpdateFade()
    {
        if (lineCopy == null && trail == null)
        {
            Destroy(gameObject);
            return;
        }

        fadeElapsed += Time.deltaTime;
        float k = 1f - Mathf.Clamp01(fadeElapsed / releaseFadeTime);

        var alphaKeys = new GradientAlphaKey[baseAlphaKeys.Length];
        for (int i = 0; i < baseAlphaKeys.Length; i++)
        {
            alphaKeys[i] = new GradientAlphaKey(baseAlphaKeys[i].alpha * k, baseAlphaKeys[i].time);
        }

        var g = new Gradient();
        g.mode = baseGradientMode;
        g.SetKeys(baseColorKeys, alphaKeys);

        if (lineCopy != null)
        {
            lineCopy.colorGradient = g;
            lineCopy.widthMultiplier = baseWidthMultiplier * k;
        }
        else
        {
            trail.colorGradient = g;
            trail.widthMultiplier = baseWidthMultiplier * k;
        }

        if (k <= 0f)
        {
            fading = false;
            Destroy(gameObject);
        }
    }

    void OnDestroy()
    {
        attachedTargets.Remove(targetId);
    }

    // 탄환(또는 자식)의 렌더러 색을 읽습니다. 아이템이 그 총알에만 덮어쓴 색(ProjectileColorOverride - 38번 보라색 등)이
    // 있으면 그 색을 먼저 쓰고, 없으면 재질 색(URP 재질은 _BaseColor, 그 외는 _Color)을 씁니다.
    private static bool TryGetProjectileColor(GameObject projectile, out Color color)
    {
        color = Color.white;

        Renderer rend = projectile.GetComponentInChildren<Renderer>();
        if (rend == null) return false;

        if (ProjectileColorOverride.TryGetRendererOverrideColor(rend, out color)) return true;

        Material mat = rend.sharedMaterial;
        if (mat == null) return false;

        if (mat.HasProperty("_BaseColor"))
        {
            color = mat.GetColor("_BaseColor");
            return true;
        }
        if (mat.HasProperty("_Color"))
        {
            color = mat.GetColor("_Color");
            return true;
        }
        return false;
    }

    // 그라데이션의 색 키에만 tint를 곱하고, 투명도 키는 그대로 둡니다.
    private static Gradient TintGradient(Gradient source, Color tint)
    {
        GradientColorKey[] colorKeys = source.colorKeys;
        for (int i = 0; i < colorKeys.Length; i++)
        {
            Color c = colorKeys[i].color;
            colorKeys[i].color = new Color(c.r * tint.r, c.g * tint.g, c.b * tint.b, c.a);
        }

        var result = new Gradient();
        result.mode = source.mode;
        result.SetKeys(colorKeys, source.alphaKeys);
        return result;
    }
}
