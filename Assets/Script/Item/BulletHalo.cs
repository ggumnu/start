using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// 94번 패시브의 총알 후광입니다(Item94effect.cs 참고). 총알이 생길 때 Attach()로 그 총알에 붙고, 총알이 사라지면 함께 사라집니다.
// 씬이나 프리팹에 미리 둘 것은 없습니다.
//
// [피해]
// tickInterval(0.1초)마다 후광 안(총알 중심에서 반지름 = haloRadius x 지금 총알 크기 / 기본 총알 크기)에 콜라이더가 걸친 살아 있는 적
// 전부에게 "지금 총알 피해 x 매초 비율 x 간격"을 EnemyHealth.TakeWeakDamage로 줍니다(아이템 부가 피해 - 피해 숫자 항상 Weak, 형빈 확정).
// 콜라이더가 여러 개인 적도 한 틱에 한 번만 맞습니다(EnemyHealth 단위로 중복 제거). 순수 피해만 주므로 총알 명중 효과(매료/넉백/폭발 등)는
// 일어나지 않고, 41번 피해 증가/처치 판정은 그대로 적용됩니다. 첫 피해는 총알이 생기고 한 간격 뒤입니다.
//
// [크기]
// 후광 이펙트는 총알의 자식이라 총알 크기를 그대로 따라갑니다. 자식 크기를 "1 / 기본 총알 크기"로 두면, 기본 총알 크기일 때 정확히
// 크기 1(= 반지름 Halo Radius인 후광)이 되고 총알이 커지면 같은 비율로 커집니다. 파티클 프리팹도 부모 크기를 따르도록 Scaling Mode를
// Hierarchy로 바꿔 씁니다(이 인스턴스만 바꾸고 원본 프리팹은 그대로).
//
// [색]
// 총알 렌더러에 덮어써진 색(ProjectileColorOverride - 23번/38번 등)이 있으면 그 색, 없으면 Default Color입니다. 파티클 프리팹은 4번 폭발과
// 같은 규칙으로 시작 색만 바꿉니다(흰색/검정 같은 무채색 파티클은 그대로).
//
// [외곽 발광]
// 후광 바깥 가장자리에 셰이더 외곽선처럼 은은한 빛을 두릅니다. 카메라를 항상 바라보는 사각 그림 한 장(빌보드)에, 가운데는 비어 있고
// 후광 가장자리에서 가장 밝다가 바깥으로 갈수록 옅어지는 고리 모양 빛 그림(코드로 만듦)을 더하기 방식으로 그립니다. 플레이어가 보는
// 화면에서는 구체의 윤곽을 따라 빛나는 것처럼 보입니다. 색은 후광 색(총알 색/노란색)을 따라가고 크기는 후광과 같은 비율로 커집니다.
public class BulletHalo : MonoBehaviour
{
    private const int TransparentQueue = 3000;
    private const float TintMinSaturation = 0.2f; // 이 채도 미만은 무채색으로 보고 그대로 둠(Item04effect와 같은 기준)
    private const float TintMinValue = 0.1f;      // 이 밝기 미만(거의 검정)도 그대로 둠

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private static Material fallbackMaterial;
    private static Mesh sphereMesh;
    private static Mesh quadMesh;
    private static Material rimFallbackMaterial;
    private static Texture2D rimTexture;
    private static float rimTextureSize = -1f;   // 지금 그림을 만든 기준값(값이 바뀌면 다시 만듦)
    private static float rimTextureInner = -1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        fallbackMaterial = null;
        sphereMesh = null;
        quadMesh = null;
        rimFallbackMaterial = null;
        rimTexture = null;
        rimTextureSize = -1f;
        rimTextureInner = -1f;
    }

    private Transform rimQuad;
    private Camera cam;

    private Projectile projectile;
    private float haloRadius;
    private float baseBulletSize;
    private float damagePerTick01; // 한 틱에 주는 비율(매초 비율 x 간격)
    private float tickInterval;
    private string enemyTag;
    private float tickTimer;

    private readonly HashSet<EnemyHealth> damagedThisTick = new HashSet<EnemyHealth>();
    private static readonly Collider[] overlapBuffer = new Collider[64];

    public static BulletHalo Attach(Projectile projectile, Item94effect settings, float baseBulletSize)
    {
        if (projectile == null || settings == null) return null;

        var halo = projectile.gameObject.AddComponent<BulletHalo>();
        halo.projectile = projectile;
        halo.haloRadius = settings.haloRadius;
        halo.baseBulletSize = Mathf.Max(baseBulletSize, 0.0001f);

        // [63번 미사일] 후광 반경 = 기본 총알 후광 x 미사일 효과 배율(50%, 분열 미사일은 25%)입니다 - 미사일 구체 크기와 상관없이
        // (형빈 확정 A안). 반경/외곽 그림이 모두 "현재 크기 / 기준 크기"로 정해지므로 기준 크기를 그 비율이 나오게 맞춥니다.
        // 피해는 미사일 피해(공격력 50%) 기준이라 이미 절반입니다.
        if (projectile.IsMissile)
        {
            float scale = Mathf.Max(projectile.EffectScale, 0.01f);
            halo.baseBulletSize = Mathf.Max(projectile.transform.lossyScale.x / scale, 0.0001f);
        }
        halo.tickInterval = Mathf.Max(settings.tickInterval, 0.02f);
        halo.damagePerTick01 = settings.damageRatioPerSecond * halo.tickInterval;
        halo.enemyTag = settings.enemyTag;

        // 색은 총알에 색이 입혀진 뒤(Launch 안에서 입힘)에 읽어야 하므로 지금 읽습니다(OnProjectileSpawned는 Launch 뒤에 불림).
        Color color = settings.defaultColor;
        if (TryGetBulletColor(projectile.gameObject, out Color bulletColor))
        {
            color = new Color(bulletColor.r, bulletColor.g, bulletColor.b, settings.tintedAlpha);
        }

        halo.BuildVisual(settings, color);
        try
        {
            if (settings.showRimGlow) halo.BuildRimGlow(settings, color);
        }
        catch (System.Exception e)
        {
            // 외곽 발광은 연출일 뿐이라, 오류가 나도 후광/피해는 그대로 둡니다.
            Debug.LogError($"BulletHalo: 외곽 발광을 만드는 중 오류가 발생해 이 총알은 외곽 발광 없이 날아갑니다: {e}");
        }
        return halo;
    }

    void Update()
    {
        if (projectile == null) return;

        tickTimer += Time.deltaTime;
        while (tickTimer >= tickInterval)
        {
            tickTimer -= tickInterval;
            DealTick();
        }
    }

    // 외곽 발광 그림이 항상 카메라를 정면으로 바라보게 합니다(총알이 어느 방향으로 날아가도 동그란 테두리로 보이게).
    void LateUpdate()
    {
        if (rimQuad == null) return;
        if (cam == null) cam = Camera.main;
        if (cam == null) return;
        rimQuad.rotation = cam.transform.rotation;
    }

    private void DealTick()
    {
        float damage = projectile.Damage * damagePerTick01;
        if (damage <= 0f) return;

        // 지금 총알 크기(월드) / 기본 총알 크기 = 후광 배율입니다(총알이 커지면 후광도 같은 비율로 커짐).
        float scale = transform.lossyScale.x / baseBulletSize;
        float radius = haloRadius * Mathf.Max(scale, 0f);
        if (radius <= 0f) return;

        int count = Physics.OverlapSphereNonAlloc(transform.position, radius, overlapBuffer, ~0, QueryTriggerInteraction.Collide);

        damagedThisTick.Clear();
        for (int i = 0; i < count; i++)
        {
            Collider hit = overlapBuffer[i];
            overlapBuffer[i] = null;
            if (hit == null) continue;

            EnemyHealth enemy = hit.GetComponent<EnemyHealth>();
            if (enemy == null) enemy = hit.GetComponentInParent<EnemyHealth>();
            if (enemy == null || enemy.IsDead) continue;

            // 콜라이더나 그 적 본체 중 하나라도 적 태그여야 합니다(4번 폭발/수류탄과 같은 기준).
            bool isEnemy = string.IsNullOrEmpty(enemyTag) || hit.CompareTag(enemyTag) || enemy.CompareTag(enemyTag);
            if (!isEnemy) continue;
            if (!damagedThisTick.Add(enemy)) continue;

            enemy.TakeWeakDamage(damage); // 피해 숫자 항상 Weak(형빈 확정)
        }
    }

    // ------------------------------------------------------------------
    // 연출
    // ------------------------------------------------------------------

    private void BuildVisual(Item94effect settings, Color color)
    {
        GameObject visual;
        if (settings.haloVfxPrefab != null)
        {
            visual = Instantiate(settings.haloVfxPrefab, transform);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one * (1f / baseBulletSize);

            // 판정은 이 스크립트가 하므로 이펙트에 콜라이더가 있으면 꺼 둡니다(총알 충돌에 끼어들지 않도록).
            foreach (var col in visual.GetComponentsInChildren<Collider>(true)) col.enabled = false;

            foreach (var ps in visual.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                main.startColor = TintGradient(main.startColor, color);
            }
        }
        else
        {
            Material material = GetFallbackMaterial();
            if (material == null) return;

            visual = new GameObject("BulletHalo");
            visual.transform.SetParent(transform, false);
            // 구체 메시는 지름 1이므로 지름 = 반지름 x 2.
            visual.transform.localScale = Vector3.one * (settings.haloRadius * 2f / baseBulletSize);

            visual.AddComponent<MeshFilter>().sharedMesh = GetSphereMesh();
            var rend = visual.AddComponent<MeshRenderer>();
            rend.sharedMaterial = material;
            rend.shadowCastingMode = ShadowCastingMode.Off;
            rend.receiveShadows = false;
            rend.lightProbeUsage = LightProbeUsage.Off;
            rend.reflectionProbeUsage = ReflectionProbeUsage.Off;

            // 밝기만 올리고(HDR) 불투명도는 그대로 둡니다.
            Color glow = new Color(color.r * settings.glowIntensity, color.g * settings.glowIntensity,
                                   color.b * settings.glowIntensity, color.a);
            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColorId, glow);
            block.SetColor(ColorId, glow);
            rend.SetPropertyBlock(block);
        }
    }

    private void BuildRimGlow(Item94effect settings, Color color)
    {
        Material material = settings.rimGlowMaterial != null ? settings.rimGlowMaterial : GetRimFallbackMaterial();
        if (material == null) return;

        float size = Mathf.Max(settings.rimGlowSize, 1.05f);
        Texture2D texture = GetRimTexture(size, settings.rimGlowInnerWidth);

        var go = new GameObject("BulletHaloRimGlow");
        rimQuad = go.transform;
        rimQuad.SetParent(transform, false);
        // 그림 한 변 = 후광 지름 x 번짐 배율(총알 기준 크기로 나눠, 기본 총알 크기에서 정확히 이 크기가 되게 함 - 후광과 같은 규칙).
        rimQuad.localScale = Vector3.one * (settings.haloRadius * 2f * size / baseBulletSize);

        go.AddComponent<MeshFilter>().sharedMesh = GetQuadMesh();
        var rend = go.AddComponent<MeshRenderer>();
        rend.sharedMaterial = material;
        rend.shadowCastingMode = ShadowCastingMode.Off;
        rend.receiveShadows = false;
        rend.lightProbeUsage = LightProbeUsage.Off;
        rend.reflectionProbeUsage = ReflectionProbeUsage.Off;

        float k = settings.rimGlowIntensity;
        Color glow = new Color(color.r * k, color.g * k, color.b * k, settings.rimGlowAlpha);
        var block = new MaterialPropertyBlock();
        block.SetColor(BaseColorId, glow);
        block.SetColor(ColorId, glow);
        block.SetTexture("_BaseMap", texture);
        block.SetTexture("_MainTex", texture);
        rend.SetPropertyBlock(block);

        cam = Camera.main;
        if (cam != null) rimQuad.rotation = cam.transform.rotation;
    }

    // 고리 모양 빛 그림입니다. 그림 가장자리(반지름 1) = 후광 반지름 x size. 후광 가장자리(1/size)에서 가장 밝고, 안쪽으로는 innerWidth만큼만
    // 부드럽게 들어오며, 바깥쪽으로는 그림 끝까지 부드럽게 옅어집니다. 가운데는 비어 있어 후광 구체를 가리지 않습니다.
    private static Texture2D GetRimTexture(float size, float innerWidth)
    {
        if (rimTexture != null && Mathf.Approximately(rimTextureSize, size) && Mathf.Approximately(rimTextureInner, innerWidth))
        {
            return rimTexture;
        }

        const int res = 128;
        if (rimTexture == null)
        {
            rimTexture = new Texture2D(res, res, TextureFormat.RGBA32, false)
            {
                name = "BulletHalo_RimGlow",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
        }

        float edge = 1f / size;                          // 후광 가장자리 위치(그림 반지름 기준)
        float inner = Mathf.Max(edge * innerWidth, 0.001f); // 안쪽으로 들어오는 폭
        float outer = Mathf.Max(1f - edge, 0.001f);          // 바깥으로 번지는 폭

        var pixels = new Color[res * res];
        for (int y = 0; y < res; y++)
        {
            for (int x = 0; x < res; x++)
            {
                float dx = (x + 0.5f) / res * 2f - 1f;
                float dy = (y + 0.5f) / res * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);

                float a;
                if (r <= edge)
                {
                    float t = Mathf.Clamp01(1f - (edge - r) / inner);
                    a = t * t * (3f - 2f * t);
                }
                else
                {
                    float t = Mathf.Clamp01(1f - (r - edge) / outer);
                    a = t * t; // 바깥으로 갈수록 빠르게 옅어짐(은은하게)
                }
                pixels[y * res + x] = new Color(1f, 1f, 1f, a);
            }
        }
        rimTexture.SetPixels(pixels);
        rimTexture.Apply();
        rimTextureSize = size;
        rimTextureInner = innerWidth;
        return rimTexture;
    }

    private static Mesh GetQuadMesh()
    {
        if (quadMesh != null) return quadMesh;
        // 유니티 기본 사각 메시만 빌려 쓰고, 임시로 만든 오브젝트(콜라이더 포함)는 바로 지웁니다.
        var temp = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quadMesh = temp.GetComponent<MeshFilter>().sharedMesh;
        var col = temp.GetComponent<Collider>();
        if (col != null) col.enabled = false;
        Destroy(temp);
        return quadMesh;
    }

    // URP Particles/Unlit 더하기(빛) 재질입니다(110번 강화 총알 외형과 같은 방식). 양면, 깊이 안 씀.
    private static Material GetRimFallbackMaterial()
    {
        if (rimFallbackMaterial != null) return rimFallbackMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            Debug.LogWarning("BulletHalo: URP Particles/Unlit 셰이더를 찾지 못해 외곽 발광을 그릴 수 없습니다. 94번 Inspector의 Rim Glow Material 칸에 " +
                             "재질을 넣어 주세요.");
            return null;
        }

        var mat = new Material(shader) { name = "BulletHalo_RimGlow_Auto" };
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
        if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f); // Transparent
        if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 2f);     // Additive
        if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 0f);       // 양면
        if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
        if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)BlendMode.One);
        if (mat.HasProperty("_SrcBlendAlpha")) mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        if (mat.HasProperty("_DstBlendAlpha")) mat.SetFloat("_DstBlendAlpha", (float)BlendMode.One);
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.renderQueue = TransparentQueue;

        rimFallbackMaterial = mat;
        return rimFallbackMaterial;
    }

    // 총알(과 자식) 렌더러 중 색이 덮어써진 것이 있으면 그 색입니다(23번/38번 등). 없으면 false.
    private static bool TryGetBulletColor(GameObject bullet, out Color color)
    {
        // 총알 자신의 렌더러를 먼저 봅니다(110번 강화 총알 외형 등 다른 자식 연출의 색을 잘못 읽지 않도록).
        var own = bullet.GetComponent<Renderer>();
        if (own != null && ProjectileColorOverride.TryGetRendererOverrideColor(own, out color)) return true;

        foreach (var r in bullet.GetComponentsInChildren<Renderer>())
        {
            if (ProjectileColorOverride.TryGetRendererOverrideColor(r, out color)) return true;
        }
        color = Color.white;
        return false;
    }

    private static ParticleSystem.MinMaxGradient TintGradient(ParticleSystem.MinMaxGradient source, Color tint)
    {
        switch (source.mode)
        {
            case ParticleSystemGradientMode.Color:
                return new ParticleSystem.MinMaxGradient(TintColor(source.color, tint));
            case ParticleSystemGradientMode.TwoColors:
                return new ParticleSystem.MinMaxGradient(TintColor(source.colorMin, tint), TintColor(source.colorMax, tint));
            case ParticleSystemGradientMode.Gradient:
                return new ParticleSystem.MinMaxGradient(TintGradientKeys(source.gradient, tint));
            case ParticleSystemGradientMode.TwoGradients:
                return new ParticleSystem.MinMaxGradient(TintGradientKeys(source.gradientMin, tint), TintGradientKeys(source.gradientMax, tint));
            case ParticleSystemGradientMode.RandomColor:
                var random = new ParticleSystem.MinMaxGradient(TintGradientKeys(source.gradient, tint));
                random.mode = ParticleSystemGradientMode.RandomColor;
                return random;
        }
        return source;
    }

    private static Gradient TintGradientKeys(Gradient source, Color tint)
    {
        if (source == null) return null;
        var result = new Gradient();
        GradientColorKey[] colorKeys = source.colorKeys;
        for (int i = 0; i < colorKeys.Length; i++) colorKeys[i].color = TintColor(colorKeys[i].color, tint);
        result.SetKeys(colorKeys, source.alphaKeys);
        result.mode = source.mode;
        return result;
    }

    // 무채색(흰색/회색/검정)은 그대로, 색이 있는 부분만 tint 색으로 바꿉니다. 투명도는 원래 값 그대로입니다.
    private static Color TintColor(Color source, Color tint)
    {
        Color.RGBToHSV(source, out _, out float saturation, out float value);
        if (saturation < TintMinSaturation || value < TintMinValue) return source;
        return new Color(tint.r, tint.g, tint.b, source.a);
    }

    private static Mesh GetSphereMesh()
    {
        if (sphereMesh != null) return sphereMesh;
        // 유니티 기본 구체 메시만 빌려 쓰고, 임시로 만든 오브젝트(콜라이더 포함)는 바로 지웁니다.
        var temp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphereMesh = temp.GetComponent<MeshFilter>().sharedMesh;
        var col = temp.GetComponent<Collider>();
        if (col != null) col.enabled = false;
        Destroy(temp);
        return sphereMesh;
    }

    // URP Unlit 반투명 재질입니다. 안쪽의 총알이 가려지지 않도록 깊이를 쓰지 않습니다(ZWrite Off).
    private static Material GetFallbackMaterial()
    {
        if (fallbackMaterial != null) return fallbackMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            Debug.LogWarning("BulletHalo: URP Unlit 셰이더를 찾지 못해 후광 구체를 그릴 수 없습니다(피해는 그대로 들어감). " +
                             "94번 Inspector의 Halo Vfx Prefab 칸에 이펙트를 넣어 주세요.");
            return null;
        }

        var mat = new Material(shader) { name = "BulletHalo_Auto" };
        mat.SetFloat("_Surface", 1f); // Transparent
        mat.SetFloat("_Blend", 0f);   // Alpha
        mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        mat.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_ZWrite", 0f);
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = TransparentQueue;

        fallbackMaterial = mat;
        return fallbackMaterial;
    }
}
