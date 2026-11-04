using UnityEditor;
using UnityEngine;

// [91번 오한 - 물방울 지속 이펙트 자동 생성 도구]
// 메뉴 "MobArchive/VFX/91번 오한 이펙트 만들기"를 누르면, 원본 vfx_Rain_02(넓은 영역에 비가 쏟아지는 이펙트)를 복제해서
// "적 몸에서 물기가 뚝뚝 떨어지는" 이펙트 Assets/VFX/Custom/VFX_Chill_Status.prefab으로 바꿔 저장합니다. 원본은 건드리지 않습니다.
//
// [바꾸는 내용]
// - 최상위 vfx_Rain_02: 재질 없는 빈 방출기라 방출을 끕니다.
// - Rain(빗줄기 -> 물방울): 50x50 넓은 상자 대신 몸통 크기의 구(반지름 0.15)에서 초당 10개, 처음엔 멈춰 있다가 중력으로 떨어짐
//   (빗줄기용 빠른 낙하 속도 모듈은 끔), 수명 0.35~0.6초, 크기 0.015~0.03, 연하늘색, 끝에서 투명해짐, 월드 공간(적이 움직여도 물방울은
//   그 자리에서 떨어짐), 미리 재생 끔, 최대 50개. 바닥에 닿으면 사라지며 물튀김을 남기고(기존 충돌 + 서브 이미터 유지), 적/플레이어 몸에는
//   부딪히지 않게 합니다(몸 안에서 생긴 물방울이 공중에서 바로 튀는 것 방지).
// - Shockwave(물튀김 고리): 크기를 0.04~0.1로 줄이고 연하늘색으로.
// 숫자는 이 파일 맨 위 상수에서 바꿀 수 있고, 만든 뒤 프리팹에서 직접 다듬어도 됩니다(다시 메뉴를 누르면 아래 값으로 다시 덮어씀).
//
// [사용 후]
// 91번 ItemDefinition - Item91effect - Chill Status Vfx - Prefab 칸에 만들어진 VFX_Chill_Status를 넣으세요.
public static class ChillVfxBuilder
{
    private const string SourcePath = "Assets/VFX/FreeQuickEffectsVol1/Prefabs/vfx_Rain_02.prefab";
    private const string TargetPath = "Assets/VFX/Custom/VFX_Chill_Status.prefab";

    // ---- 물방울(Rain) ----
    private const float DropEmitRadius = 0.15f;      // 물방울이 생기는 구의 반지름(적 몸통 크기에 맞춰 조절)
    private const float DropRate = 10f;              // 초당 물방울 수
    private const float DropLifetimeMin = 0.35f;
    private const float DropLifetimeMax = 0.6f;
    private const float DropSizeMin = 0.015f;
    private const float DropSizeMax = 0.03f;
    private const float DropGravity = 0.5f;          // 중력 배율(클수록 빨리 떨어짐)
    private const float DropLengthScale = 2.5f;      // 떨어지는 방향으로 늘어나는 정도(길쭉한 물방울)
    private const int DropMaxParticles = 50;
    private static readonly Color DropColorA = new Color(0.66f, 0.85f, 1f, 0.5f);  // A8D8FF, 반투명
    private static readonly Color DropColorB = new Color(0.66f, 0.85f, 1f, 0.9f);

    // ---- 물튀김(Shockwave) ----
    private const float SplashSizeMin = 0.04f;
    private const float SplashSizeMax = 0.1f;
    private static readonly Color SplashColorA = new Color(0.66f, 0.85f, 1f, 0.67f);
    private static readonly Color SplashColorB = new Color(0.66f, 0.85f, 1f, 0.21f);

    [MenuItem("MobArchive/VFX/91번 오한 이펙트 만들기 (VFX_Chill_Status)")]
    private static void Build()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(SourcePath) == null)
        {
            EditorUtility.DisplayDialog("오한 이펙트 만들기", "원본 이펙트를 찾지 못했습니다:\n" + SourcePath, "확인");
            return;
        }

        bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(TargetPath) != null;
        if (exists)
        {
            // 이미 있으면 같은 파일을 그대로 수정합니다(파일을 새로 만들지 않으므로 91번 아이템에 넣어 둔 연결은 유지됨).
            bool ok = EditorUtility.DisplayDialog("오한 이펙트 만들기",
                "VFX_Chill_Status가 이미 있습니다.\n직접 다듬은 값이 있다면 이 도구의 기본값으로 다시 덮어씁니다. 계속할까요?",
                "덮어쓰기", "취소");
            if (!ok) return;
        }
        else
        {
            if (!AssetDatabase.IsValidFolder("Assets/VFX/Custom")) AssetDatabase.CreateFolder("Assets/VFX", "Custom");
            if (!AssetDatabase.CopyAsset(SourcePath, TargetPath))
            {
                EditorUtility.DisplayDialog("오한 이펙트 만들기", "원본을 복제하지 못했습니다.", "확인");
                return;
            }
        }

        GameObject root = PrefabUtility.LoadPrefabContents(TargetPath);
        try
        {
            root.name = "VFX_Chill_Status";

            // 최상위: 재질 없는 빈 방출기 - 방출 끔
            var rootPs = root.GetComponent<ParticleSystem>();
            if (rootPs != null)
            {
                var em = rootPs.emission;
                em.enabled = false;
            }

            Transform rain = FindDeep(root.transform, "Rain");
            Transform splash = FindDeep(root.transform, "Shockwave");
            if (rain == null || splash == null)
            {
                EditorUtility.DisplayDialog("오한 이펙트 만들기", "원본 안에서 Rain/Shockwave 오브젝트를 찾지 못했습니다(원본 구조가 바뀐 것 같습니다).", "확인");
                return;
            }

            SetupDrops(rain.GetComponent<ParticleSystem>(), rain.GetComponent<ParticleSystemRenderer>());
            SetupSplash(splash.GetComponent<ParticleSystem>());

            PrefabUtility.SaveAsPrefabAsset(root, TargetPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        var created = AssetDatabase.LoadAssetAtPath<GameObject>(TargetPath);
        Selection.activeObject = created;
        EditorGUIUtility.PingObject(created);
        Debug.Log("ChillVfxBuilder: " + TargetPath + " 를 만들었습니다. 91번 Item91effect의 Chill Status Vfx - Prefab 칸에 넣으세요.");
    }

    private static void SetupDrops(ParticleSystem ps, ParticleSystemRenderer renderer)
    {
        if (ps == null) return;

        // 재생 중이면 일부 값(Duration 등)을 바꿀 수 없으므로 먼저 멈춥니다(편집 중인 프리팹 안에서만 - 실제 게임에는 영향 없음).
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 1f;
        main.loop = true;
        main.prewarm = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(DropLifetimeMin, DropLifetimeMax);
        main.startSpeed = 0f;
        main.startSize3D = false;
        main.startSize = new ParticleSystem.MinMaxCurve(DropSizeMin, DropSizeMax);
        main.startColor = new ParticleSystem.MinMaxGradient(DropColorA, DropColorB);
        main.gravityModifier = DropGravity;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = DropMaxParticles;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = DropRate;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = DropEmitRadius;
        shape.radiusThickness = 1f;
        shape.position = Vector3.zero;
        shape.rotation = Vector3.zero;
        shape.scale = Vector3.one;

        // 빗줄기용 빠른 낙하 속도(-10~-30)는 끄고, 중력으로 천천히 시작해 점점 빨라지게 떨어뜨립니다.
        var velocity = ps.velocityOverLifetime;
        velocity.enabled = false;

        // 끝에서 투명해지며 사라짐
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(grad);

        // 바닥에 닿으면 사라지며 물튀김(서브 이미터)을 남김. 적/플레이어 몸에는 부딪히지 않게 합니다.
        var collision = ps.collision;
        collision.enabled = true;
        collision.type = ParticleSystemCollisionType.World;
        collision.mode = ParticleSystemCollisionMode.Collision3D;
        collision.lifetimeLoss = 1f;
        collision.bounce = 0f;
        int excluded = LayerMask.GetMask("Enemy", "Player");
        collision.collidesWith = ~excluded;

        if (renderer != null)
        {
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = DropLengthScale;
            renderer.velocityScale = 0f;
        }
    }

    private static void SetupSplash(ParticleSystem ps)
    {
        if (ps == null) return;

        var main = ps.main;
        main.startSize3D = false;
        main.startSize = new ParticleSystem.MinMaxCurve(SplashSizeMin, SplashSizeMax);
        main.startColor = new ParticleSystem.MinMaxGradient(SplashColorA, SplashColorB);
        main.maxParticles = DropMaxParticles;
    }

    private static Transform FindDeep(Transform parent, string name)
    {
        if (parent.name == name) return parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindDeep(parent.GetChild(i), name);
            if (found != null) return found;
        }
        return null;
    }
}
