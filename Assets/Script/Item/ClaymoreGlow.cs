using UnityEngine;

// 115번 크레모어가 설치된 동안의 빨간 점멸입니다. Grenade가 크레모어를 설치하는 순간 자동으로 붙입니다.
// - 81번 소이탄 발광과 같은 방식(항상 화면을 향하는 빛 그림 + 점광원)이지만 더 크고, 깜빡이지 않고 "밝음 → 서서히 어두움 → 서서히 밝음"을
//   부드럽게 반복합니다(기뢰가 물속에서 빛나는 느낌 - 형빈 확정).
// - 크레모어가 굴러가도(77번) 빛은 회전하지 않도록 자식이 아닌 별도 오브젝트로 만들어 위치만 따라갑니다. 크레모어가 터지거나 사라지면
//   함께 사라집니다.
// - SetVisible(false)이면 숨깁니다(플레이어가 다른 방에 있을 때 - Grenade가 부름).
public class ClaymoreGlow : MonoBehaviour
{
    private static Sprite defaultGlowSprite;

    private ClaymoreOverride.Settings settings;
    private GameObject glowRoot;
    private SpriteRenderer glowRenderer;
    private Light glowLight;
    private float age;
    private bool visible = true;

    public static ClaymoreGlow Attach(GameObject claymore, ClaymoreOverride.Settings settings)
    {
        if (claymore == null || settings == null) return null;

        var glow = claymore.GetComponent<ClaymoreGlow>();
        if (glow == null) glow = claymore.AddComponent<ClaymoreGlow>();
        glow.Init(settings);
        return glow;
    }

    private void Init(ClaymoreOverride.Settings s)
    {
        settings = s;
        age = 0f;
        if (glowRoot != null) Destroy(glowRoot);

        glowRoot = new GameObject("ClaymoreGlow");

        glowRenderer = glowRoot.AddComponent<SpriteRenderer>();
        glowRenderer.sprite = GetDefaultGlowSprite();
        glowRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        glowRenderer.receiveShadows = false;

        if (s.enableGlowLight)
        {
            glowLight = glowRoot.AddComponent<Light>();
            glowLight.type = LightType.Point;
            glowLight.color = s.glowLightColor;
            glowLight.range = s.glowLightRange;
            glowLight.shadows = LightShadows.None;
        }

        glowRoot.SetActive(visible);
        UpdateGlow();
    }

    public void SetVisible(bool value)
    {
        visible = value;
        if (glowRoot != null && glowRoot.activeSelf != value) glowRoot.SetActive(value);
    }

    void LateUpdate()
    {
        age += Time.deltaTime;
        UpdateGlow();
    }

    private void UpdateGlow()
    {
        if (glowRoot == null || settings == null) return;

        glowRoot.transform.position = transform.position + settings.glowOffset;

        Camera cam = Camera.main;
        if (cam != null) glowRoot.transform.rotation = cam.transform.rotation; // 항상 화면을 정면으로 향함

        // 0(가장 어두움) ~ 1(가장 밝음)을 코사인으로 부드럽게 오갑니다. 설치 순간이 가장 밝습니다.
        float period = Mathf.Max(settings.pulsePeriod, 0.1f);
        float wave = 0.5f + 0.5f * Mathf.Cos(age * Mathf.PI * 2f / period);
        float brightness = Mathf.Lerp(settings.glowMinBrightness, 1f, wave);

        if (glowRenderer != null)
        {
            float spriteWorld = glowRenderer.sprite != null ? glowRenderer.sprite.bounds.size.x : 1f;
            float sizeFactor = Mathf.Lerp(settings.glowMinScale, 1f, wave);
            float scale = settings.glowSize * sizeFactor / Mathf.Max(spriteWorld, 0.0001f);
            glowRoot.transform.localScale = new Vector3(scale, scale, scale);

            Color c = settings.glowColor;
            c.a *= brightness;
            glowRenderer.color = c;
        }

        if (glowLight != null)
        {
            glowLight.intensity = settings.glowLightIntensity * brightness;
        }
    }

    void OnDestroy()
    {
        if (glowRoot != null) Destroy(glowRoot);
        glowRoot = null;
    }

    // 가운데가 가장 밝고 바깥으로 부드럽게 사라지는 흰 원 그림입니다(색은 glowColor로 입힘). 한 번만 만들어 재사용합니다.
    private static Sprite GetDefaultGlowSprite()
    {
        if (defaultGlowSprite != null) return defaultGlowSprite;

        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "ClaymoreGlow_Default",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };

        var pixels = new Color32[size * size];
        float half = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - half) / half;
                float dy = (y - half) / half;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1f - r);
                a = a * a;
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply();

        defaultGlowSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        defaultGlowSprite.name = "ClaymoreGlow_Default";
        return defaultGlowSprite;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlayStart()
    {
        defaultGlowSprite = null;
    }
}
