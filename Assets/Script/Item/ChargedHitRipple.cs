using UnityEngine;

// [110번 관통 강화 총알 - 피격 굴절]
// 관통 강화 총알(충전 수가 관통 기준 이상 - 기본 4발 = 머리 위 숫자 5 이상)이 적에게 맞는 순간, 그 적 몸통 앞에 HitRipple 셰이더 판을
// 띄워 적 몸이 물결처럼 출렁 굴절됐다가 돌아오게 합니다(90번 물결 굴절과 같은 원리 - 화면을 비틀어 다시 그림). 타격감용 연출입니다.
//
// [규칙 - 형빈 확정]
// - 관통 강화 총알 자신만 해당합니다(Projectile.IsPiercing). 5번으로 갈라진 분열탄/관통 기준 미만 강화 총알/120번 벽 관통 총알은 없음.
// - 기존 피격 흰색 점멸/피해 숫자 등은 그대로이고, 이 굴절이 "추가"로 나옵니다.
// - 관통 기준보다 많이 충전할수록 크고 세게: 배율 = 1 + (충전 수 - 관통 기준) x Per Extra Charge (최대 Max Scale).
//   크기와 굴절 세기 둘 다 이 배율을 곱합니다.
// - 적을 따라다니고(적 렌더러 중심), 항상 카메라를 향하며, Duration 뒤에 사라집니다. 적이 죽어도 끝까지 재생합니다(사라지는 연출 위로).
// - 재질(Material)이 비어 있으면 실행 중에 셰이더를 찾아 만듭니다(에디터에서는 동작하지만 빌드에서는 빠질 수 있으니 재질을 넣어 두세요 -
//   메뉴 MobArchive/VFX/110번 피격 굴절 재질 만들기가 만들고 110번 에셋에 넣어 줌).
// - URP Opaque Texture가 켜져 있어야 합니다(HitRipple.shader 참고).
//
// [연결] Projectile.OnPlayerBulletHitEnemyDetailed를 게임 시작 때 한 번 구독합니다. 110번 설정은 ChargeShotOverride(지금 장착한 110번)에서 읽습니다.
//
// [보스 전방위 강화 탄환 - 플레이어 피격]
// EnemyRadialAttack이 강화 외형을 켜면, 그 탄환이 플레이어에게 실제로 피해를 줄 때(무적/보호막/돌진 회피 제외) SpawnOnPlayer로 플레이어 몸에
// 같은 굴절을 띄웁니다(설정은 EnemyRadialAttack의 Player Hit Ripple 칸, 배율 1).
public class ChargedHitRipple : MonoBehaviour
{
    [System.Serializable]
    public class Settings
    {
        [Tooltip("켜면 관통 강화 총알에 맞은 적이 굴절됩니다.")]
        public bool enabled = true;

        [Tooltip("HitRipple 셰이더 재질입니다(메뉴 MobArchive/VFX/110번 피격 굴절 재질 만들기로 자동 생성/연결).")]
        public Material material;

        [Tooltip("지속 시간(초)입니다.")]
        [Min(0.05f)] public float duration = 0.35f;

        [Tooltip("물결 판 크기 = 적 몸통(렌더러) 크기 x 이 값입니다.")]
        [Min(0.1f)] public float sizeMultiplier = 1.6f;

        [Tooltip("굴절 세기(화면 비율)입니다. 클수록 많이 비틀립니다.")]
        [Range(0f, 0.2f)] public float strength = 0.035f;

        [Tooltip("고리 두께(판 크기 대비)입니다.")]
        [Range(0.01f, 0.5f)] public float ringWidth = 0.12f;

        [Tooltip("앞 고리 뒤를 따라오는 잔물결 수입니다(0~3).")]
        [Range(0, 3)] public int rippleCount = 2;

        [Tooltip("잔물결 간격(판 크기 대비)입니다.")]
        [Range(0.01f, 0.5f)] public float rippleSpacing = 0.12f;

        [Tooltip("잔물결이 하나 뒤로 갈 때마다 곱해지는 세기입니다.")]
        [Range(0f, 1f)] public float rippleFalloff = 0.55f;

        [Tooltip("물결에 살짝 더하는 빛 색입니다(알파 = 진하기, 0이면 색 없이 굴절만).")]
        [ColorUsage(true, true)] public Color tint = new Color(0.8f, 0.6f, 1.4f, 0.15f);

        [Header("충전 수에 따른 세기 (관통 기준 이상부터)")]
        [Tooltip("관통 기준보다 1발 더 충전할 때마다 더해지는 배율입니다(크기/굴절 세기 둘 다).")]
        [Min(0f)] public float perExtraCharge = 0.15f;

        [Tooltip("배율의 최대값입니다.")]
        [Min(1f)] public float maxScale = 2f;

        // 1레벨 보스 전방위 탄환(하늘색)에 맞춘 기본값 - 빛 색만 하늘색이고 나머지는 110번 기본값과 같습니다.
        public static Settings CreateCyan()
        {
            return new Settings { tint = new Color(0.4f, 1.1f, 1.4f, 0.15f) };
        }
    }

    private static readonly int ProgressId = Shader.PropertyToID("_Progress");
    private static readonly int StrengthId = Shader.PropertyToID("_Strength");
    private static readonly int FadeId = Shader.PropertyToID("_Fade");
    private static readonly int RingWidthId = Shader.PropertyToID("_RingWidth");
    private static readonly int RippleCountId = Shader.PropertyToID("_RippleCount");
    private static readonly int RippleSpacingId = Shader.PropertyToID("_RippleSpacing");
    private static readonly int RippleFalloffId = Shader.PropertyToID("_RippleFalloff");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private static Mesh quadMesh;
    private static Material fallbackMaterial;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        quadMesh = null;
        fallbackMaterial = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Hook()
    {
        Projectile.OnPlayerBulletHitEnemyDetailed -= HandleHit;
        Projectile.OnPlayerBulletHitEnemyDetailed += HandleHit;
    }

    private static void HandleHit(Projectile projectile, EnemyHealth enemy)
    {
        try
        {
            if (projectile == null || enemy == null || !projectile.IsPiercing) return;
            var visual = projectile.GetComponent<ChargedBulletVisual>();
            if (visual == null) return;
            if (!ChargeShotOverride.TryGetActive(out var charge) || charge == null) return;

            var s = charge.hitRipple;
            if (s == null || !s.enabled) return;
            int threshold = Mathf.Max(1, charge.pierceChargeThreshold);
            if (visual.ChargeCount < threshold) return;

            float scale = Mathf.Min(1f + (visual.ChargeCount - threshold) * s.perExtraCharge, Mathf.Max(1f, s.maxScale));
            Spawn(enemy.transform, enemy.TargetRenderer, s, scale);
        }
        catch (System.Exception e)
        {
            // 명중 알림 안에서 실행되므로, 오류가 나도 총알 처리가 막히지 않게 건너뜁니다(순수 연출).
            Debug.LogError($"ChargedHitRipple: 피격 굴절 생성 중 오류가 발생해 건너뜁니다: {e}");
        }
    }

    // 플레이어 몸(루트 콜라이더 = CharacterController 크기 기준)에 굴절을 띄웁니다. 순수 연출이라 오류가 나도 건너뜁니다.
    public static void SpawnOnPlayer(PlayerHealth player, Settings s)
    {
        try
        {
            if (player == null || s == null || !s.enabled) return;
            Spawn(player.transform, null, s, 1f);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"ChargedHitRipple: 플레이어 피격 굴절 생성 중 오류가 발생해 건너뜁니다: {e}");
        }
    }

    private static void Spawn(Transform target, Renderer targetRenderer, Settings s, float scale)
    {
        Material source = s.material != null ? s.material : GetFallbackMaterial();
        if (source == null) return;

        var go = new GameObject("ChargedHitRipple");
        var ripple = go.AddComponent<ChargedHitRipple>();
        // [방 전환] 플레이어가 다른 방에 들어가면 바로 지워지도록 등록합니다(RoomScope - 다른 방 위 허공에 남지 않게).
        RoomScope.DestroyWhenPlayerLeaves(go);
        ripple.Init(target, targetRenderer, s, scale, source);
    }

    private static Material GetFallbackMaterial()
    {
        if (fallbackMaterial != null) return fallbackMaterial;
        Shader shader = Shader.Find("MobArchive/HitRipple");
        if (shader == null)
        {
            Debug.LogWarning("ChargedHitRipple: MobArchive/HitRipple 셰이더를 찾지 못했습니다. 110번 설정의 Hit Ripple > Material 칸에 재질을 넣어 주세요.");
            return null;
        }
        fallbackMaterial = new Material(shader) { name = "ChargedHitRipple_Runtime" };
        return fallbackMaterial;
    }

    private static Mesh GetQuad()
    {
        if (quadMesh != null) return quadMesh;
        quadMesh = new Mesh { name = "ChargedHitRippleQuad" };
        quadMesh.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
            new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f),
        };
        quadMesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
        quadMesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        quadMesh.RecalculateBounds();
        return quadMesh;
    }

    // ---- 인스턴스 ----
    private Transform target;
    private Renderer targetRenderer;
    private Settings settings;
    private float scale;
    private Material material;
    private float age;
    private Vector3 lastCenter;
    private float baseSize = 1f;
    private Camera cam;

    // targetRenderer가 없으면 target의 콜라이더 크기를 씁니다(플레이어).
    private void Init(Transform targetTransform, Renderer renderer, Settings s, float scale, Material source)
    {
        target = targetTransform;
        targetRenderer = renderer;
        settings = s;
        this.scale = scale;

        material = new Material(source);
        material.SetFloat(RingWidthId, s.ringWidth);
        material.SetFloat(RippleCountId, s.rippleCount);
        material.SetFloat(RippleSpacingId, s.rippleSpacing);
        material.SetFloat(RippleFalloffId, s.rippleFalloff);
        material.SetColor(ColorId, s.tint);

        gameObject.AddComponent<MeshFilter>().sharedMesh = GetQuad();
        var mr = gameObject.AddComponent<MeshRenderer>();
        mr.sharedMaterial = material;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

        // 처음 맞은 순간의 몸통 크기로 판 크기를 정합니다(적이 죽어서 사라지는 중에 크기가 바뀌지 않게).
        Bounds b = GetBounds(out lastCenter);
        baseSize = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
        if (baseSize <= 0.01f) baseSize = 1f;

        UpdateRipple();
    }

    private Bounds GetBounds(out Vector3 center)
    {
        if (targetRenderer != null)
        {
            Bounds b = targetRenderer.bounds;
            center = b.center;
            return b;
        }
        if (target != null)
        {
            var col = target.GetComponent<Collider>();
            if (col != null) { center = col.bounds.center; return col.bounds; }
            center = target.position + Vector3.up * 0.8f;
            return new Bounds(center, Vector3.one);
        }
        center = lastCenter;
        return new Bounds(center, Vector3.one * baseSize);
    }

    void LateUpdate()
    {
        age += Time.deltaTime;
        if (age >= settings.duration)
        {
            Destroy(gameObject);
            return;
        }
        UpdateRipple();
    }

    private void UpdateRipple()
    {
        if (target != null) GetBounds(out lastCenter);

        if (cam == null) cam = Camera.main;
        float size = baseSize * settings.sizeMultiplier * scale;

        Vector3 pos = lastCenter;
        if (cam != null)
        {
            // 카메라를 향하고, 몸통에 묻혀 가려지지 않도록 몸통 반지름만큼 카메라 쪽으로 당깁니다.
            Vector3 toCam = cam.transform.position - lastCenter;
            if (toCam.sqrMagnitude > 0.0001f) pos += toCam.normalized * (baseSize * 0.5f);
            transform.rotation = cam.transform.rotation;
        }
        transform.position = pos;
        transform.localScale = new Vector3(size, size, 1f);

        float t = Mathf.Clamp01(age / settings.duration);
        float progress = 1f - (1f - t) * (1f - t); // 처음엔 빠르게 퍼지고 점점 느려짐
        material.SetFloat(ProgressId, progress);
        material.SetFloat(StrengthId, settings.strength * scale);
        material.SetFloat(FadeId, 1f - t);
    }

    void OnDestroy()
    {
        if (material != null) Destroy(material);
    }
}
