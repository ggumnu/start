using System.Collections.Generic;
using UnityEngine;

// [108번 폭죽탄 - 겉모습/꼬리/터지는 이펙트]
// 108번 포대(FireworkTurret)가 쏘는 폭죽탄은 "진짜 수류탄"(Grenade)입니다. 날아가기/착탄/피해/77·81·92·115·57·66번/특수방 해제는 모두
// 수류탄 그대로이고, 이 파일은 겉모습만 맡습니다(형빈 확정 - 수류탄 색 바꾸기보다 폭죽 쪽이 우선).
// - 폭죽탄: 작은 발광 구체 + 같은 색 꼬리(63번 미사일과 같은 느낌). 색은 발마다 Colors 목록에서 하나를 뽑습니다.
// - 터질 때: 수류탄 폭발 이펙트 대신 폭죽 이펙트를 띄웁니다(PlayerGrenadeThrower.NotifyExploded의 폭죽 버전이 부름).
//   1) 불꽃 줄기: Spark Template(vfx_Sparks_01 권장 - 원래는 위로 뿜는 불꽃 분수)을 복제해, 한 번에 사방(구 모양)으로 퍼졌다가
//      빠르게 느려지고 살짝 처지며 사라지게 값을 바꿔 재생합니다. 퍼지는 크기 = 폭발 반경 x Bloom Radius Multiplier.
//   2) 잔 반짝임(선택): 같은 템플릿을 한 번 더 써서, 조금 뒤에 퍼진 자리 근처에서 작은 반짝임이 잠깐 남습니다.
//   3) 섬광(선택): Flash Prefab(VFX_ImpactToon_1.1.0 등)을 폭죽 색으로 물들여 가운데에 띄웁니다.
// - 이펙트는 플레이어가 다른 방에 들어가면 바로 지워집니다(RoomScope). 플레이어가 없는 방의 "조용한 폭발"은 이펙트가 없습니다.
[System.Serializable]
public class FireworkStyle
{
    [Header("색 (발마다 하나를 무작위로 뽑음)")]
    public List<Color> colors = new List<Color>
    {
        new Color(1f, 0.37f, 0.66f, 1f),   // 분홍
        new Color(1f, 0.88f, 0.3f, 1f),    // 노랑
        new Color(0.35f, 0.85f, 1f, 1f),   // 하늘
        new Color(0.55f, 1f, 0.35f, 1f),   // 연두
        new Color(0.72f, 0.48f, 1f, 1f),   // 보라
    };

    [Header("폭죽탄 (날아가는 구체)")]
    [Tooltip("폭죽탄 구체의 지름(m)입니다.")]
    [Min(0.01f)] public float shellSize = 0.14f;

    [Tooltip("켜면 수류탄처럼 착탄 지점에 폭발 범위 원을 띄웁니다(자해 피해가 있으므로 피할 수 있게).")]
    public bool showLandingMarker = true;

    [Header("폭죽탄 꼬리")]
    [Tooltip("꼬리 재질입니다. 비워두면 기본(Sprites/Default)을 씁니다. Mat_BulletTrail을 넣으면 총알 꼬리와 같은 느낌이 됩니다.")]
    public Material trailMaterial;

    [Tooltip("꼬리 길이(초)입니다.")]
    [Min(0f)] public float trailTime = 0.25f;

    [Tooltip("꼬리 굵기 = 구체 지름 x 이 값입니다.")]
    [Min(0f)] public float trailWidthRatio = 0.8f;

    [Header("터지는 위치")]
    [Tooltip("폭죽 이펙트를 착탄 지점에서 이만큼 위(m)에 띄웁니다.")]
    public float burstHeight = 0.4f;

    [Header("불꽃 줄기")]
    [Tooltip("불꽃 줄기 템플릿입니다(vfx_Sparks_01 권장). 비워두면 불꽃 줄기/잔 반짝임 없이 섬광만 나옵니다.")]
    public GameObject sparkTemplate;

    [Tooltip("한 번에 퍼지는 불꽃 줄기 개수입니다.")]
    [Min(1)] public int sparkCount = 40;

    [Tooltip("불꽃 줄기가 사라질 때까지의 시간(초)입니다.")]
    [Min(0.05f)] public float sparkLifetime = 0.9f;

    [Tooltip("불꽃이 퍼지는 크기 = 폭발 반경 x 이 값입니다(피해 범위보다 크게 퍼져야 축제 폭죽처럼 보임).")]
    [Min(0.1f)] public float bloomRadiusMultiplier = 2.5f;

    [Tooltip("불꽃 줄기 굵기 배율입니다(템플릿 크기 x 이 값).")]
    [Min(0.01f)] public float sparkSizeMultiplier = 1f;

    [Tooltip("불꽃이 처지는 정도(중력)입니다. 0이면 처지지 않습니다.")]
    public float sparkGravity = 0.35f;

    [Tooltip("불꽃마다 색을 이만큼 흰색 쪽으로 섞은 값과 원래 색 사이에서 고릅니다(0 = 모두 같은 색).")]
    [Range(0f, 1f)] public float whiteMix = 0.35f;

    [Header("잔 반짝임")]
    public bool glitterEnabled = true;

    [Tooltip("잔 반짝임 개수입니다.")]
    [Min(1)] public int glitterCount = 24;

    [Tooltip("터지고 나서 잔 반짝임이 나타나기까지의 시간(초)입니다.")]
    [Min(0f)] public float glitterDelay = 0.35f;

    [Tooltip("잔 반짝임이 남아 있는 시간(초)입니다.")]
    [Min(0.05f)] public float glitterLifetime = 0.5f;

    [Tooltip("잔 반짝임 크기 배율입니다(템플릿 크기 x 이 값).")]
    [Min(0.01f)] public float glitterSizeMultiplier = 0.5f;

    public Color glitterColor = new Color(1f, 0.95f, 0.75f, 1f);

    [Header("섬광 (가운데 '팡')")]
    [Tooltip("가운데에 띄울 섬광 이펙트입니다(VFX_ImpactToon_1.1.0 등). 비워두면 없습니다.")]
    public GameObject flashPrefab;

    [Tooltip("섬광 크기 = 폭발 반경(m) x 이 값입니다.")]
    [Min(0.01f)] public float flashScalePerRadius = 2f;

    [Tooltip("켜면 섬광을 폭죽 색으로 물들입니다.")]
    public bool tintFlash = true;

    [Tooltip("섬광을 지우기까지의 시간(초)입니다.")]
    [Min(0.1f)] public float flashLifetime = 1.5f;

    public Color PickColor()
    {
        if (colors == null || colors.Count == 0) return Color.white;
        Color c = colors[Random.Range(0, colors.Count)];
        c.a = 1f;
        return c;
    }
}

public static class FireworkShell
{
    private static readonly Dictionary<Color32, Material> shellMaterials = new Dictionary<Color32, Material>();
    private static Material defaultTrailMaterial;
    private static AnimationCurve speedCurve;
    private static float speedCurveIntegral = -1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        shellMaterials.Clear();
        defaultTrailMaterial = null;
        speedCurve = null;
        speedCurveIntegral = -1f;
    }

    // ---------------- 폭죽탄 오브젝트 ----------------

    // 폭죽탄(발광 구체) 오브젝트를 만듭니다. Grenade는 호출하는 쪽(PlayerGrenadeThrower)이 붙입니다. scale은 92번 분열탄 크기 배율입니다.
    public static GameObject CreateShellObject(FireworkStyle style, Color color, Vector3 position, float scale)
    {
        GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        obj.name = "FireworkShell (108)";
        obj.transform.position = position;
        float size = (style != null ? style.shellSize : 0.14f) * Mathf.Max(scale, 0.01f);
        obj.transform.localScale = Vector3.one * Mathf.Max(size, 0.005f);

        var col = obj.GetComponent<Collider>();
        if (col != null) Object.Destroy(col);

        var rend = obj.GetComponent<Renderer>();
        Material mat = GetShellMaterial(color);
        if (rend != null && mat != null) rend.sharedMaterial = mat;
        if (rend != null)
        {
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;
        }
        return obj;
    }

    // 빌드 핑크 방지 - 수류탄 임시 구체(PlayerGrenadeThrower.GetFallbackSphereMaterial)와 같은 URP 셰이더 재질입니다. 색마다 하나만 만듭니다.
    static Material GetShellMaterial(Color color)
    {
        Color32 key = color;
        if (shellMaterials.TryGetValue(key, out var cached) && cached != null) return cached;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
            ?? Shader.Find("Universal Render Pipeline/Lit")
            ?? Shader.Find("Sprites/Default");
        if (shader == null) return null;

        var mat = new Material(shader) { name = "FireworkShell_Material" };
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        mat.color = color;
        shellMaterials[key] = mat;
        return mat;
    }

    // 폭죽탄에 같은 색 꼬리를 붙입니다(Grenade.Launch가 일반 수류탄 꼬리 대신 부름).
    public static void AttachTrail(GameObject shell, FireworkStyle style, Color color)
    {
        if (shell == null || style == null || style.trailTime <= 0f || style.trailWidthRatio <= 0f) return;

        var go = new GameObject("FireworkTrail (108)");
        go.transform.position = shell.transform.position;
        var tr = go.AddComponent<TrailRenderer>();
        tr.time = style.trailTime;
        tr.minVertexDistance = 0.02f;
        tr.widthMultiplier = Mathf.Max(shell.transform.lossyScale.x, 0.001f) * style.trailWidthRatio;
        tr.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
        tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        tr.receiveShadows = false;

        Material mat = style.trailMaterial != null ? style.trailMaterial : GetDefaultTrailMaterial();
        if (mat != null) tr.sharedMaterial = mat;

        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.Lerp(color, Color.white, 0.4f), 0f), new GradientColorKey(color, 1f) },
                  new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        tr.colorGradient = g;
        tr.Clear();

        var follower = go.AddComponent<FireworkTrailFollower>();
        follower.Init(shell.transform, tr);
    }

    static Material GetDefaultTrailMaterial()
    {
        if (defaultTrailMaterial != null) return defaultTrailMaterial;
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) return null;
        defaultTrailMaterial = new Material(shader) { name = "FireworkTrail_Material" };
        return defaultTrailMaterial;
    }

    // ---------------- 터지는 이펙트 ----------------

    // 착탄 지점 groundPoint에서 폭죽 이펙트를 띄웁니다. radius = 이 폭죽탄의 폭발 반경(크기 기준).
    public static void SpawnBurst(FireworkStyle style, Vector3 groundPoint, Color color, float radius)
    {
        if (style == null) return;
        Vector3 pos = groundPoint + Vector3.up * style.burstHeight;
        float r = Mathf.Max(radius, 0.05f);

        if (style.sparkTemplate != null)
        {
            float bloom = r * style.bloomRadiusMultiplier;
            SpawnSparkLayer(style.sparkTemplate, pos, style.sparkCount, style.sparkLifetime, bloom,
                            style.sparkSizeMultiplier, style.sparkGravity, color,
                            Color.Lerp(color, Color.white, style.whiteMix), 0f, false, 0f);

            if (style.glitterEnabled)
            {
                SpawnSparkLayer(style.sparkTemplate, pos, style.glitterCount, style.glitterLifetime, bloom * 0.08f,
                                style.glitterSizeMultiplier, style.sparkGravity * 0.4f, style.glitterColor,
                                Color.Lerp(style.glitterColor, color, 0.5f), style.glitterDelay, true, bloom * 0.85f);
            }
        }

        if (style.flashPrefab != null)
        {
            GameObject flash = Object.Instantiate(style.flashPrefab, pos, Quaternion.identity);
            RoomScope.DestroyWhenPlayerLeaves(flash);
            Object.Destroy(flash, style.flashLifetime);
            FamiliarVfx.ApplySizeMultiplier(flash, r * style.flashScalePerRadius);
            if (style.tintFlash) GrenadeVisualOverride.TintParticles(flash, color, true);
            FamiliarVfx.Restart(flash);
        }
    }

    // 템플릿을 복제해 "한 번에 사방으로 퍼짐"으로 값을 바꿔 재생합니다.
    // spreadDistance = 퍼지는 거리(m), shellRadius > 0이면 그 반지름 구 껍질 위에서 생깁니다(잔 반짝임), asBillboard = 늘어나지 않는 점.
    static void SpawnSparkLayer(GameObject template, Vector3 pos, int count, float lifetime, float spreadDistance,
                                float sizeMultiplier, float gravity, Color colorA, Color colorB, float delay,
                                bool asBillboard, float shellRadius)
    {
        GameObject go = Object.Instantiate(template, pos, Quaternion.identity);
        go.name = template.name + " (108 폭죽)";
        RoomScope.DestroyWhenPlayerLeaves(go);
        Object.Destroy(go, delay + lifetime + 0.5f);

        float speed = SpeedForDistance(spreadDistance, lifetime * 0.875f);

        foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
        {
            ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = false;
            main.prewarm = false;
            main.duration = Mathf.Max(delay + 0.05f, 0.1f);
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startDelay = 0f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.75f, lifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.8f, speed);
            main.startSizeMultiplier *= sizeMultiplier;
            if (main.startSize3D)
            {
                main.startSizeXMultiplier *= sizeMultiplier;
                main.startSizeYMultiplier *= sizeMultiplier;
                main.startSizeZMultiplier *= sizeMultiplier;
            }
            main.gravityModifier = gravity;
            main.startColor = new ParticleSystem.MinMaxGradient(colorA, colorB);
            if (main.maxParticles < count * 2) main.maxParticles = count * 2;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.rateOverDistance = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(delay, (short)Mathf.Clamp(count, 1, 1000)) });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = shellRadius > 0f ? shellRadius : 0.05f;
            shape.radiusThickness = shellRadius > 0f ? 0.25f : 1f;
            shape.randomDirectionAmount = 0f;
            shape.position = Vector3.zero;
            shape.rotation = Vector3.zero;

            // 처음엔 빠르게 퍼지고 금방 느려지게(폭죽이 "팡" 하고 피는 모양) - 속도에 곱하는 곡선입니다.
            var velocity = ps.velocityOverLifetime;
            velocity.x = new ParticleSystem.MinMaxCurve(0f);
            velocity.y = new ParticleSystem.MinMaxCurve(0f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f);
            velocity.speedModifier = new ParticleSystem.MinMaxCurve(1f, GetSpeedCurve());
            velocity.enabled = true;

            // 끝무렵에 서서히 투명해짐
            var colorOverLifetime = ps.colorOverLifetime;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                         new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.55f), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(fade);
            colorOverLifetime.enabled = true;

            if (asBillboard)
            {
                var psr = ps.GetComponent<ParticleSystemRenderer>();
                if (psr != null) psr.renderMode = ParticleSystemRenderMode.Billboard;
            }

            ps.Play(false);
        }
    }

    static AnimationCurve GetSpeedCurve()
    {
        if (speedCurve == null)
        {
            speedCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.2f, 0.35f),
                                            new Keyframe(0.55f, 0.12f), new Keyframe(1f, 0.04f));
            speedCurveIntegral = -1f;
        }
        return speedCurve;
    }

    // 속도 곡선을 적용했을 때 lifetime 동안 distance만큼 가는 시작 속도입니다.
    static float SpeedForDistance(float distance, float lifetime)
    {
        var curve = GetSpeedCurve();
        if (speedCurveIntegral < 0f)
        {
            const int steps = 40;
            float sum = 0f;
            for (int i = 0; i < steps; i++) sum += curve.Evaluate((i + 0.5f) / steps);
            speedCurveIntegral = Mathf.Max(sum / steps, 0.01f);
        }
        return Mathf.Max(distance, 0f) / (Mathf.Max(lifetime, 0.01f) * speedCurveIntegral);
    }
}

// 폭죽탄 꼬리가 폭죽탄을 따라다닙니다. 폭죽탄이 터져 사라지면 꼬리가 늘어나지 않게 멈추고 남은 꼬리가 줄어들며 사라집니다.
// 폭죽탄이 숨겨지면(플레이어가 다른 방으로 감 - Grenade가 렌더러를 끔) 다른 방 위 허공에 남지 않도록 바로 지웁니다.
public class FireworkTrailFollower : MonoBehaviour
{
    private Transform target;
    private Renderer targetRenderer;
    private TrailRenderer trail;
    private bool released;

    public void Init(Transform shell, TrailRenderer tr)
    {
        target = shell;
        targetRenderer = shell != null ? shell.GetComponent<Renderer>() : null;
        trail = tr;
    }

    void LateUpdate()
    {
        if (released) return;

        if (target == null || !target.gameObject.activeInHierarchy)
        {
            released = true;
            if (trail != null) trail.emitting = false;
            Destroy(gameObject, trail != null ? trail.time + 0.1f : 0f);
            return;
        }

        if (targetRenderer != null && !targetRenderer.enabled)
        {
            released = true;
            Destroy(gameObject);
            return;
        }

        transform.position = target.position;
    }
}
