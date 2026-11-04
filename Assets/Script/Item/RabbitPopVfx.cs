using System.Collections.Generic;
using UnityEngine;

// [토끼 팝콘 연출 - 57번 토끼꼬리 수류탄]
// 수류탄이 터지는 순간, 착탄 중심에서 토끼 스프라이트 여러 마리가 팝콘처럼 사방으로 튀어 나갑니다(순수 연출 - 피해 없음).
//
// [움직임 - 형빈 확정]
// - 방향: 바닥 기준 사방. 마릿수만큼 360도를 고르게 나눈 뒤(시작 각도는 매번 랜덤) 각자 조금씩 랜덤으로 비틉니다.
// - 궤적: 수류탄과 같은 포물선 공식(Grenade.EvaluateArc)으로, 착탄 중심에서 바깥으로 솟았다 떨어지는 곡사입니다.
// - 크기: 0에서 시작해 빠르게 커지며 살짝 넘쳤다가 돌아오고(팝), 잠깐 유지한 뒤 작아지면서 사라집니다.
//   완전히 커졌을 때의 크기는 토끼마다 랜덤입니다.
// - 그림: 등록된 스프라이트 중 마리마다 랜덤으로 고릅니다(한 폭발 안에서도 A, B, B, C처럼 섞임). 항상 카메라 정면을 보는
//   2D 그림(조명 영향 없음)입니다.
// - 가려짐: 일반 오브젝트처럼 벽/장애물 뒤로 가면 가려집니다(형빈 확정 - 폭발 반경을 헷갈리지 않게). 폭발 파티클보다는
//   앞에 그리도록 Sorting Order를 높여둡니다.
//
// [크기 단위]
// 스프라이트 원본 해상도(Pixels Per Unit)와 상관없이, 완전히 커졌을 때 "세로 길이(m)"가 peakSize 범위 안의 값이 되도록
// 자동으로 맞춥니다. 그래서 PNG 해상도가 서로 달라도 같은 기준으로 크기가 정해집니다.
//
// [그리는 방식 - SpriteRenderer 대신 메시 판 (한 폭발 안 토끼 그림이 전부 같아지던 문제 수정)]
// 처음에는 SpriteRenderer로 그렸는데, 토끼마다 그림을 따로 뽑았는데도 한 폭발의 토끼가 전부 같은 그림으로 보였고, 토끼 하나가
// 사라지는 순간 다른 그림이 잠깐 보였습니다(형빈 확인). Unity가 토끼 여러 마리를 한 묶음으로 그리면서 묶음 첫 토끼의 그림을
// 나머지에도 씌운 것으로 보입니다. 그래서 토끼를 일반 메시 판(MeshFilter + MeshRenderer)으로 그리고, 그림(텍스처)마다 그 그림을
// 직접 들고 있는 재질을 하나씩 씁니다. 재질이 다르면 절대 같이 묶어 그리지 않으므로 그림이 섞이지 않습니다.
// 판 모양은 스프라이트의 실제 모양(Sprite의 정점/UV)을 그대로 따오므로 겉보기는 SpriteRenderer와 같습니다. 메시/재질은 그림마다
// 한 번만 만들고 재사용합니다(그림 3장이면 3개씩).
//
// [사용]
// RabbitPopVfx.Spawn(중심 위치, 설정)을 부르면 토끼들이 생성되고, 다 날아간 토끼는 스스로 사라집니다. 설정 값은 57번 아이템
// 에셋(Special Effects의 GrenadeSelfDamageImmunityEffect)에서 조절합니다.
public class RabbitPopVfx : MonoBehaviour
{
    [System.Serializable]
    public class Settings
    {
        [Tooltip("튀어 나올 토끼 스프라이트들입니다. 마리마다 이 중에서 랜덤으로 고릅니다. 비어 있으면 토끼가 나오지 않습니다. " +
                 "PNG는 Texture Type을 Sprite (2D and UI)로 지정해야 넣을 수 있습니다.")]
        public List<Sprite> sprites = new List<Sprite>();

        [Header("마릿수")]
        [Min(0)] public int minCount = 3;
        [Min(0)] public int maxCount = 4;

        [Header("크기 (완전히 커졌을 때의 세로 길이, m)")]
        [Tooltip("토끼마다 이 범위 안에서 랜덤으로 정해집니다.")]
        [Min(0.01f)] public float minPeakSize = 0.5f;
        [Min(0.01f)] public float maxPeakSize = 0.9f;

        [Header("궤적")]
        [Tooltip("착탄 중심에서 바닥 기준으로 날아가는 거리(m) 범위입니다.")]
        [Min(0f)] public float minDistance = 1.2f;
        [Min(0f)] public float maxDistance = 2.2f;
        [Tooltip("포물선 최고점 높이(m) 범위입니다.")]
        [Min(0f)] public float minArcHeight = 1.0f;
        [Min(0f)] public float maxArcHeight = 1.6f;
        [Tooltip("나타나서 사라질 때까지의 시간(초) 범위입니다.")]
        [Min(0.05f)] public float minDuration = 0.6f;
        [Min(0.05f)] public float maxDuration = 0.85f;
        [Tooltip("고르게 나눈 방향에서 각자 얼마나(도) 랜덤으로 비틀지입니다. 0이면 정확히 고르게 퍼집니다.")]
        [Range(0f, 90f)] public float angleJitter = 20f;
        [Tooltip("출발 높이(m)입니다. 바닥(착탄점)에서 이만큼 위에서 튀어 나옵니다.")]
        public float startHeight = 0.3f;

        [Header("크기 변화 (전체 시간 대비 비율)")]
        [Tooltip("0에서 최대 크기까지 커지는 구간입니다. 예: 0.25면 전체 시간의 처음 25% 동안 커집니다.")]
        [Range(0.01f, 0.9f)] public float growFraction = 0.25f;
        [Tooltip("최대 크기를 유지하는 구간입니다. 커지는 구간 + 유지 구간이 끝나면 남은 시간 동안 작아지며 사라집니다.")]
        [Range(0f, 0.9f)] public float holdFraction = 0.2f;
        [Tooltip("커질 때 최대 크기를 살짝 넘쳤다가 돌아오는 정도입니다(팝 느낌). 0이면 넘치지 않습니다.")]
        [Range(0f, 3f)] public float popOvershoot = 1.7f;

        [Header("회전 / 방향")]
        [Tooltip("토끼마다 화면 기준으로 이 범위(±도) 안에서 랜덤하게 기울어진 채 나옵니다.")]
        [Range(0f, 45f)] public float maxTiltDegrees = 15f;
        [Tooltip("켜면 화면에서 왼쪽으로 날아가는 토끼는 좌우를 뒤집어 진행 방향을 보게 합니다.")]
        public bool faceTravelDirection = true;
        [Tooltip("스프라이트 원본이 오른쪽을 보고 있으면 켜고, 왼쪽을 보고 있으면 끄세요(진행 방향 맞추기용).")]
        public bool spriteFacesRight = true;

        [Header("그리기 순서")]
        [Tooltip("폭발 파티클보다 앞에 그리기 위한 Sorting Order입니다. 벽/장애물에는 그대로 가려집니다.")]
        public int sortingOrder = 10;
    }

    // ---- 한 마리의 상태 ----
    private Settings settings;
    private Vector3 start;
    private Vector3 end;
    private float arcHeight;
    private float duration;
    private float peakScale;
    private float tilt;
    private float elapsed;
    private Transform cameraTransform;
    private float flipSign = 1f;

    // 조명 영향을 받지 않는 기본 스프라이트 셰이더(Sprites/Default)로 만든 재질을 그림(텍스처)마다 하나씩 씁니다. 렌더 파이프라인
    // 설정과 상관없이 아이템 아이콘처럼 항상 원래 색 그대로 보이고, 재질 자체가 그 그림을 들고 있어서 묶어 그리기에 영향받지 않습니다.
    private static readonly Dictionary<Texture, Material> spriteMaterials = new Dictionary<Texture, Material>();

    // 스프라이트 모양 그대로의 판 메시를 그림마다 하나씩 씁니다.
    private static readonly Dictionary<Sprite, Mesh> spriteMeshes = new Dictionary<Sprite, Mesh>();

    // Play를 새로 시작할 때 이전 Play에서 만든 재질/메시 목록을 비웁니다(에디터에서 도메인 리로드를 꺼 둔 경우 대비).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlayStart()
    {
        spriteMaterials.Clear();
        spriteMeshes.Clear();
    }

    static Material GetSpriteMaterial(Sprite sprite)
    {
        Texture texture = sprite != null ? sprite.texture : null;
        if (texture == null) return null;

        if (spriteMaterials.TryGetValue(texture, out Material cached) && cached != null) return cached;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            Debug.LogWarning("RabbitPopVfx: Sprites/Default 셰이더를 찾지 못해 토끼를 그리지 못했습니다.");
            return null;
        }

        var mat = new Material(shader) { name = "RabbitPop_Material_" + texture.name };
        mat.mainTexture = texture;
        mat.color = Color.white;
        spriteMaterials[texture] = mat;
        return mat;
    }

    // 스프라이트의 실제 모양(정점/UV/삼각형)으로 판 메시를 만듭니다. 정점 위치는 스프라이트의 Pivot 기준이고 단위는 m
    // (픽셀 / Pixels Per Unit)라, SpriteRenderer로 그렸을 때와 같은 크기/모양이 됩니다.
    static Mesh GetSpriteMesh(Sprite sprite)
    {
        if (sprite == null) return null;
        if (spriteMeshes.TryGetValue(sprite, out Mesh cached) && cached != null) return cached;

        Vector2[] spriteVertices = sprite.vertices;
        Vector2[] spriteUvs = sprite.uv;
        ushort[] spriteTriangles = sprite.triangles;

        var vertices = new Vector3[spriteVertices.Length];
        var colors = new Color[spriteVertices.Length];
        for (int i = 0; i < spriteVertices.Length; i++)
        {
            vertices[i] = new Vector3(spriteVertices[i].x, spriteVertices[i].y, 0f);
            colors[i] = Color.white; // Sprites/Default는 정점 색을 곱하므로 흰색으로 채웁니다.
        }

        var triangles = new int[spriteTriangles.Length];
        for (int i = 0; i < spriteTriangles.Length; i++) triangles[i] = spriteTriangles[i];

        var mesh = new Mesh { name = "RabbitPop_Mesh_" + sprite.name };
        mesh.vertices = vertices;
        mesh.uv = spriteUvs;
        mesh.colors = colors;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();

        spriteMeshes[sprite] = mesh;
        return mesh;
    }

    // 착탄 중심 center에서 토끼들을 생성합니다.
    public static void Spawn(Vector3 center, Settings settings)
    {
        if (settings == null || settings.sprites == null) return;

        // 비어 있는 칸은 건너뛰고 실제 스프라이트만 모읍니다.
        var usable = new List<Sprite>();
        foreach (var s in settings.sprites)
        {
            if (s != null) usable.Add(s);
        }
        if (usable.Count == 0) return;

        int minCount = Mathf.Min(settings.minCount, settings.maxCount);
        int maxCount = Mathf.Max(settings.minCount, settings.maxCount);
        int count = Random.Range(minCount, maxCount + 1);
        if (count <= 0) return;

        Camera cam = Camera.main;
        Transform camT = cam != null ? cam.transform : null;

        float baseAngle = Random.Range(0f, 360f);
        float step = 360f / count;

        for (int i = 0; i < count; i++)
        {
            float angle = baseAngle + step * i + Random.Range(-settings.angleJitter, settings.angleJitter);
            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;

            float distance = RandomBetween(settings.minDistance, settings.maxDistance);
            Vector3 from = center + Vector3.up * settings.startHeight;
            Vector3 to = from + dir * distance;

            // 토끼 한 마리마다 그림을 새로 뽑습니다.
            Sprite sprite = usable[Random.Range(0, usable.Count)];

            Material mat = GetSpriteMaterial(sprite);
            Mesh mesh = GetSpriteMesh(sprite);
            if (mat == null || mesh == null) continue;

            var go = new GameObject("RabbitPop (" + sprite.name + ")");
            go.transform.position = from;
            // [방 전환] 플레이어가 다른 방에 들어가면 바로 지워지도록 등록합니다(RoomScope - 다른 방 위 허공에 남지 않게).
            RoomScope.DestroyWhenPlayerLeaves(go);

            var meshFilter = go.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = mesh;

            var meshRenderer = go.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = mat;
            meshRenderer.sortingOrder = settings.sortingOrder;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            meshRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

            var rabbit = go.AddComponent<RabbitPopVfx>();
            rabbit.settings = settings;
            rabbit.start = from;
            rabbit.end = to;
            rabbit.arcHeight = RandomBetween(settings.minArcHeight, settings.maxArcHeight);
            rabbit.duration = Mathf.Max(0.05f, RandomBetween(settings.minDuration, settings.maxDuration));
            rabbit.tilt = Random.Range(-settings.maxTiltDegrees, settings.maxTiltDegrees);
            rabbit.cameraTransform = camT;

            // 원하는 세로 길이(m)가 되도록 스프라이트 원래 크기로 나눠 배율을 정합니다.
            float spriteHeight = sprite.bounds.size.y;
            float targetHeight = RandomBetween(settings.minPeakSize, settings.maxPeakSize);
            rabbit.peakScale = spriteHeight > 0.0001f ? targetHeight / spriteHeight : targetHeight;

            // 화면에서 왼쪽으로 날아가면 진행 방향을 보도록 좌우 반전(가로 배율을 -1로 - 셰이더가 양면을 그리므로 그대로 보임)
            rabbit.flipSign = 1f;
            if (settings.faceTravelDirection && camT != null)
            {
                bool movesRightOnScreen = Vector3.Dot(dir, camT.right) >= 0f;
                if (movesRightOnScreen != settings.spriteFacesRight) rabbit.flipSign = -1f;
            }

            go.transform.localScale = Vector3.zero;
            rabbit.UpdatePose(0f);
        }
    }

    private static float RandomBetween(float a, float b)
    {
        return a <= b ? Random.Range(a, b) : Random.Range(b, a);
    }

    void Update()
    {
        elapsed += Time.deltaTime;
        float u = elapsed / duration;

        if (u >= 1f)
        {
            Destroy(gameObject);
            return;
        }

        UpdatePose(u);
    }

    void UpdatePose(float u)
    {
        transform.position = Grenade.EvaluateArc(start, end, arcHeight, u);

        // 항상 카메라 정면을 보게 하고, 화면 기준으로 살짝 기울입니다.
        if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
        if (cameraTransform != null)
        {
            transform.rotation = cameraTransform.rotation * Quaternion.Euler(0f, 0f, tilt);
        }

        float size = peakScale * EvaluateScale(u);
        transform.localScale = new Vector3(size * flipSign, size, size);
    }

    // 전체 진행도 u(0~1)에 따른 크기 비율(0~1, 커질 때 살짝 넘침)입니다.
    float EvaluateScale(float u)
    {
        float grow = Mathf.Clamp(settings.growFraction, 0.01f, 0.9f);
        float hold = Mathf.Clamp(settings.holdFraction, 0f, 0.98f - grow);

        if (u < grow)
        {
            return EaseOutBack(u / grow, settings.popOvershoot);
        }

        if (u < grow + hold) return 1f;

        float shrinkLength = Mathf.Max(0.0001f, 1f - grow - hold);
        float t = Mathf.Clamp01((u - grow - hold) / shrinkLength);
        return 1f - t * t; // 처음엔 천천히, 끝에서 빠르게 작아짐
    }

    // 0 -> 1로 가되 끝에서 살짝 넘쳤다가 돌아오는 곡선입니다(overshoot 0이면 넘치지 않음).
    static float EaseOutBack(float t, float overshoot)
    {
        t = Mathf.Clamp01(t) - 1f;
        return t * t * ((overshoot + 1f) * t + overshoot) + 1f;
    }
}
