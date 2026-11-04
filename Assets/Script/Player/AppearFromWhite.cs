using System.Collections.Generic;
using UnityEngine;

// 일반방/보스방 보물상자가 스폰되는 순간 갑자기 뿅 하고 나타나는 대신, 투명한
// 상태에서 흰색으로 떠오르듯 나타난 뒤 서서히 원래 색으로 돌아오는 연출을 맡는
// 컴포넌트입니다. NormalRoomTreasureSpawner.SpawnChestObject()에서 상자를
// Instantiate한 직후에 붙여서 사용합니다(일반방/보스방 상자 모두 이 메서드를
// 공유하므로 한 곳만 고치면 둘 다 적용됩니다).
//
// [DissolveAndVanish와의 관계]
// 사라질 때 쓰는 DissolveAndVanish를 거꾸로 뒤집은 형태입니다 - 그쪽은 원래색 ->
// 흰색(불투명 유지) -> 흰색 -> 투명 순서였다면, 이쪽은 투명 -> 흰색(불투명해짐)
// -> 흰색 -> 원래색 순서입니다. 다만 이 효과가 끝난 뒤에도 상자는 계속 존재하며
// 평소처럼 렌더링돼야 하므로(Destroy되지 않음), 연출이 끝나면 머티리얼을 다시
// 원래의 Opaque 모드로 되돌리는 마무리 작업이 추가로 필요합니다. 이 컴포넌트도
// 다 쓰고 나면 스스로를 제거합니다(Destroy(this) - 오브젝트가 아니라 컴포넌트만).
//
// [아이템 아이콘은 대상에서 제외]
// 상자 위에 뜨는 아이템 아이콘(SpriteRenderer)은 이 연출과 무관하게 평소대로
// 즉시 나타나도록 그대로 두었습니다(요청 범위가 상자 본체였기 때문). 필요하면
// 아이콘도 함께 서서히 나타나도록 추가할 수 있습니다.
public class AppearFromWhite : MonoBehaviour
{
    [Tooltip("나타나는 연출이 시작부터 끝까지 걸리는 시간(초)입니다.")]
    public float duration = 0.4f;

    [Range(0.05f, 0.95f)]
    [Tooltip("duration 중 앞부분 몇 %를 '투명 -> 흰색으로 떠오르는' 구간에 쓸지 정합니다. " +
             "나머지 구간에서는 그 흰색 상태에서 서서히 원래 색으로 돌아옵니다.")]
    public float appearPortion = 0.5f;

    [Tooltip("나타나는 동안 자체발광(Emission) 값을 얼마나 끌어올릴지 정합니다. " +
             "값을 키울수록 조명/그림자와 무관하게 확실한 흰색으로 떠오릅니다. " +
             "머티리얼이 Emission을 지원하지 않으면 무시됩니다.")]
    public float emissionBoost = 1.5f;

    private struct RendererEntry
    {
        public Material[] materials;
        public Color[] originalColors;
        public bool[] usesBaseColor;
        public bool[] usesColor;
        public bool[] usesEmission;
        public Color[] originalEmission;
        public bool[] convertedToTransparent; // 이 컴포넌트가 Opaque->Transparent로 바꿨는지 (끝나면 되돌리기 위함)
    }

    private readonly List<RendererEntry> rendererEntries = new List<RendererEntry>();
    private float elapsed = 0f;
    private bool isAppearing = false;

    // 이미 붙어있으면 그걸 재사용(프리팹에 미리 붙여 duration 등을 조정해도 존중),
    // 없으면 기본값으로 새로 붙입니다.
    public static AppearFromWhite Begin(GameObject target)
    {
        var appear = target.GetComponent<AppearFromWhite>();
        if (appear == null)
        {
            appear = target.AddComponent<AppearFromWhite>();
        }
        appear.BeginAppear();
        return appear;
    }

    public void BeginAppear()
    {
        if (isAppearing) return;
        isAppearing = true;
        CacheRenderers();
    }

    private void CacheRenderers()
    {
        rendererEntries.Clear();

        foreach (var rend in GetComponentsInChildren<Renderer>(true))
        {
            if (rend is SpriteRenderer) continue; // 아이템 아이콘은 제외

            var materials = rend.materials;
            var originalColors = new Color[materials.Length];
            var usesBaseColor = new bool[materials.Length];
            var usesColor = new bool[materials.Length];
            var usesEmission = new bool[materials.Length];
            var originalEmission = new Color[materials.Length];
            var convertedToTransparent = new bool[materials.Length];

            for (int i = 0; i < materials.Length; i++)
            {
                var mat = materials[i];
                usesBaseColor[i] = mat.HasProperty("_BaseColor");
                usesColor[i] = mat.HasProperty("_Color");
                usesEmission[i] = mat.HasProperty("_EmissionColor");

                if (usesEmission[i])
                {
                    originalEmission[i] = mat.GetColor("_EmissionColor");
                    mat.EnableKeyword("_EMISSION");
                }

                if (!usesBaseColor[i] && !usesColor[i])
                {
                    continue;
                }

                originalColors[i] = usesBaseColor[i] ? mat.GetColor("_BaseColor") : mat.GetColor("_Color");
                convertedToTransparent[i] = MakeTransparent(mat);

                // 시작 프레임부터 바로 투명한 상태로 보이도록, 첫 색을 미리 적용해둡니다
                // (Update()가 이번 프레임에 실행되기 전 한 프레임이라도 원래 색이 그대로
                // 보이는 것을 막기 위함).
                Color startColor = Color.white;
                startColor.a = 0f;
                if (usesBaseColor[i]) mat.SetColor("_BaseColor", startColor);
                if (usesColor[i]) mat.SetColor("_Color", startColor);
            }

            rendererEntries.Add(new RendererEntry
            {
                materials = materials,
                originalColors = originalColors,
                usesBaseColor = usesBaseColor,
                usesColor = usesColor,
                usesEmission = usesEmission,
                originalEmission = originalEmission,
                convertedToTransparent = convertedToTransparent,
            });
        }
    }

    // Opaque -> Transparent 전환. 이미 Transparent였다면 손대지 않고 false를 반환합니다
    // (그래야 연출이 끝났을 때 원래부터 Transparent였던 걸 실수로 Opaque로 되돌리지
    // 않습니다).
    private bool MakeTransparent(Material mat)
    {
        if (!mat.HasProperty("_Surface")) return false;
        if (Mathf.Approximately(mat.GetFloat("_Surface"), 1f)) return false; // 이미 Transparent

        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 0f);
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.DisableKeyword("_SURFACE_TYPE_OPAQUE");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        return true;
    }

    // MakeTransparent()로 이 컴포넌트가 직접 바꿔놓은 경우에만, 연출이 끝난 뒤 원래의
    // Opaque 상태로 되돌립니다.
    private void MakeOpaque(Material mat)
    {
        mat.SetFloat("_Surface", 0f);
        mat.SetFloat("_Blend", 0f);
        mat.SetOverrideTag("RenderType", "Opaque");
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
        mat.SetInt("_ZWrite", 1);
        mat.EnableKeyword("_SURFACE_TYPE_OPAQUE");
        mat.DisableKeyword("_ALPHABLEND_ON");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
    }

    void Update()
    {
        if (!isAppearing) return;

        elapsed += Time.deltaTime;
        float t = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;

        // 앞 구간(appear) 동안은 흰색으로 고정한 채 알파만 0 -> 원래값으로 보간(투명에서
        // 서서히 불투명해짐), 뒤 구간에서는 완전히 불투명한 채로 색만 흰색 -> 원래색으로
        // 보간합니다.
        float appear = appearPortion;
        float alphaT = appear > 0f ? Mathf.Clamp01(t / appear) : 1f;
        float colorT = appear < 1f ? Mathf.Clamp01((t - appear) / (1f - appear)) : (t >= 1f ? 1f : 0f);

        foreach (var entry in rendererEntries)
        {
            for (int i = 0; i < entry.materials.Length; i++)
            {
                if (entry.usesBaseColor[i] || entry.usesColor[i])
                {
                    Color c = Color.Lerp(Color.white, entry.originalColors[i], colorT);
                    c.a = Mathf.Lerp(0f, entry.originalColors[i].a, alphaT);

                    if (entry.usesBaseColor[i]) entry.materials[i].SetColor("_BaseColor", c);
                    if (entry.usesColor[i]) entry.materials[i].SetColor("_Color", c);
                }

                if (entry.usesEmission[i])
                {
                    Color e = Color.Lerp(Color.white * emissionBoost, entry.originalEmission[i], colorT);
                    entry.materials[i].SetColor("_EmissionColor", e);
                }
            }
        }

        if (t >= 1f)
        {
            Finish();
        }
    }

    private void Finish()
    {
        isAppearing = false;

        foreach (var entry in rendererEntries)
        {
            for (int i = 0; i < entry.materials.Length; i++)
            {
                if (entry.convertedToTransparent[i])
                {
                    MakeOpaque(entry.materials[i]);
                }
            }
        }

        Destroy(this); // 오브젝트는 그대로 두고, 다 쓴 이 연출용 컴포넌트만 제거합니다.
    }
}
