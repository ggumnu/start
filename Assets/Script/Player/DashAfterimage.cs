using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// 74번 돌진의 잔상 연출입니다(형빈 확정 - 돌진 애니메이션 없이, 지나간 자리에 플레이어 모델 잔상이 살짝 남았다가 금방 사라짐).
// PlayerDash가 돌진을 시작할 때 한 번 + 돌진하는 동안 Spawn Interval마다 Spawn()을 부릅니다. 씬에 미리 둘 것은 없습니다.
//
// [만드는 방식]
// - 플레이어 아래의 SkinnedMeshRenderer(몸/머리카락/옷 등 뼈로 움직이는 메시)는 그 순간 자세를 BakeMesh로 복사하고,
//   MeshRenderer(총/파츠 등 고정 메시)는 메시를 그대로 가져와 같은 위치/회전/크기에 둡니다. 레이사와 SD 캐릭터 모두 자동입니다.
// - 꺼져 있는 렌더러(비활성 오브젝트, 피격 점멸로 잠깐 꺼진 순간 포함)와, 재질이 전부 반투명(Render Queue 3000 이상 - 얼굴
//   표정 평면 등 투명 PNG 그림)인 렌더러는 건너뜁니다(단색으로 칠하면 사각형이 그대로 보이기 때문).
// - 잔상 재질은 Inspector의 Material 칸에 넣은 것을 쓰고, 비워두면 URP Unlit 반투명 재질을 코드로 하나 만들어 씁니다.
//   잔상 색은 재질의 _BaseColor/_Color에 Color 값(알파 = 시작 불투명도)을 덮어써서 정합니다(MaterialPropertyBlock).
// - 잔상은 플레이어의 자식이 아니라 따로 생성되어 그 자리에 남고, Lifetime 동안 투명해지며 사라진 뒤 복사한 메시까지 정리됩니다.
//   그림자를 만들거나 받지 않고, 콜라이더도 없어서 판정에는 아무 영향이 없습니다.
[System.Serializable]
public class DashAfterimageSettings
{
    [Tooltip("끄면 잔상을 만들지 않습니다.")]
    public bool show = true;

    [Tooltip("잔상에 쓸 재질입니다. 비워두면 URP Unlit 반투명 재질을 자동으로 만들어 씁니다. 직접 만들 때는 URP/Unlit + " +
             "Surface Type = Transparent로 만든 재질을 넣으세요(색은 아래 Color가 덮어씁니다).")]
    public Material material;

    [Tooltip("잔상 색입니다. 알파(A)가 처음 나타날 때의 불투명도입니다.")]
    public Color color = new Color(0.55f, 0.85f, 1f, 0.45f);

    [Tooltip("돌진하는 동안 잔상을 남기는 간격(초)입니다. 출발하는 순간에도 하나 남깁니다.")]
    [Min(0.01f)] public float spawnInterval = 0.04f;

    [Tooltip("잔상 하나가 나타난 뒤 완전히 사라질 때까지의 시간(초)입니다.")]
    [Min(0.01f)] public float lifetime = 0.25f;
}

public class DashAfterimage : MonoBehaviour
{
    private const int TransparentQueue = 3000;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    // 재질 칸이 비었을 때 쓰는 자동 생성 재질입니다(한 번만 만들어 모든 잔상이 같이 씀).
    private static Material fallbackMaterial;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        fallbackMaterial = null;
    }

    private readonly List<Renderer> ghostRenderers = new List<Renderer>();
    private readonly List<Mesh> bakedMeshes = new List<Mesh>();
    private MaterialPropertyBlock block;
    private Color baseColor;
    private float lifetime;
    private float age;

    // player 아래의 렌더러들로 지금 모습의 잔상 하나를 만듭니다. 만들 것이 하나도 없으면 아무것도 남기지 않습니다.
    public static void Spawn(GameObject player, DashAfterimageSettings settings)
    {
        if (player == null || settings == null || !settings.show) return;

        Material material = settings.material != null ? settings.material : GetFallbackMaterial();
        if (material == null) return;

        var root = new GameObject("DashAfterimage");
        var ghost = root.AddComponent<DashAfterimage>();
        ghost.baseColor = settings.color;
        ghost.lifetime = Mathf.Max(settings.lifetime, 0.01f);
        ghost.block = new MaterialPropertyBlock();

        foreach (var rend in player.GetComponentsInChildren<Renderer>())
        {
            if (rend == null || !rend.enabled || !rend.gameObject.activeInHierarchy) continue;
            if (IsAllTransparent(rend)) continue;

            if (rend is SkinnedMeshRenderer skinned)
            {
                if (skinned.sharedMesh == null) continue;
                var baked = new Mesh();
                // useScale = true: 크기까지 반영해 굽기 때문에 잔상은 위치/회전만 맞추면 됩니다.
                skinned.BakeMesh(baked, true);
                ghost.bakedMeshes.Add(baked);
                ghost.AddPart(skinned.transform.position, skinned.transform.rotation, Vector3.one, baked, material);
            }
            else if (rend is MeshRenderer)
            {
                var filter = rend.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;
                ghost.AddPart(rend.transform.position, rend.transform.rotation, rend.transform.lossyScale,
                    filter.sharedMesh, material);
            }
        }

        if (ghost.ghostRenderers.Count == 0)
        {
            Destroy(root);
            return;
        }

        ghost.ApplyAlpha(1f);
    }

    private void AddPart(Vector3 position, Quaternion rotation, Vector3 scale, Mesh mesh, Material material)
    {
        var part = new GameObject("Part");
        part.transform.SetParent(transform, false);
        part.transform.SetPositionAndRotation(position, rotation);
        part.transform.localScale = scale;

        part.AddComponent<MeshFilter>().sharedMesh = mesh;
        var rend = part.AddComponent<MeshRenderer>();

        int count = Mathf.Max(1, mesh.subMeshCount);
        var materials = new Material[count];
        for (int i = 0; i < count; i++) materials[i] = material;
        rend.sharedMaterials = materials;

        rend.shadowCastingMode = ShadowCastingMode.Off;
        rend.receiveShadows = false;
        rend.lightProbeUsage = LightProbeUsage.Off;
        rend.reflectionProbeUsage = ReflectionProbeUsage.Off;

        ghostRenderers.Add(rend);
    }

    void Update()
    {
        age += Time.deltaTime;
        float t = Mathf.Clamp01(age / lifetime);
        ApplyAlpha(1f - t);

        if (age >= lifetime) Destroy(gameObject);
    }

    void OnDestroy()
    {
        // BakeMesh로 만든 메시는 직접 지우지 않으면 메모리에 남습니다.
        for (int i = 0; i < bakedMeshes.Count; i++)
        {
            if (bakedMeshes[i] != null) Destroy(bakedMeshes[i]);
        }
        bakedMeshes.Clear();
    }

    private void ApplyAlpha(float factor)
    {
        Color c = baseColor;
        c.a = baseColor.a * factor;

        block.Clear();
        block.SetColor(BaseColorId, c);
        block.SetColor(ColorId, c);
        for (int i = 0; i < ghostRenderers.Count; i++)
        {
            if (ghostRenderers[i] != null) ghostRenderers[i].SetPropertyBlock(block);
        }
    }

    // 재질이 하나도 없거나 전부 반투명이면 true입니다(얼굴 표정 평면 같은 투명 그림 제외용).
    private static bool IsAllTransparent(Renderer rend)
    {
        var mats = rend.sharedMaterials;
        if (mats == null || mats.Length == 0) return true;
        bool any = false;
        foreach (var m in mats)
        {
            if (m == null) continue;
            any = true;
            if (m.renderQueue < TransparentQueue) return false;
        }
        return any;
    }

    // URP Unlit 셰이더로 반투명(알파 블렌드) 재질을 만듭니다. 잔상끼리/자기 몸끼리 겹친 부분이 진해지지 않도록 깊이는 씁니다(ZWrite On).
    private static Material GetFallbackMaterial()
    {
        if (fallbackMaterial != null) return fallbackMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            Debug.LogWarning("DashAfterimage: URP Unlit 셰이더를 찾지 못해 잔상을 만들 수 없습니다. 74번 Inspector의 잔상 Material 칸에 " +
                             "반투명 재질을 넣어 주세요.");
            return null;
        }

        var mat = new Material(shader) { name = "DashAfterimage_Auto" };
        mat.SetFloat("_Surface", 1f); // Transparent
        mat.SetFloat("_Blend", 0f);   // Alpha
        mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        mat.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_ZWrite", 1f);
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = TransparentQueue;

        fallbackMaterial = mat;
        return fallbackMaterial;
    }
}
