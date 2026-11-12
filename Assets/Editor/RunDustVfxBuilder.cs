using UnityEditor;
using UnityEngine;

// [뛰기 흙먼지 이펙트 만들기 메뉴] MobArchive/VFX/뛰기 흙먼지 이펙트 만들기
// 카툰 연기 뭉치 텍스처(T_RunDust_Puff, 흰색 - 갈색은 파티클 색으로 입힘) + 재질 Mat_RunDust로 파티클 프리팹 VFX_RunDust를 만들고,
// Resources/TravelBoostSettings(없으면 새로 만듦)의 Run Dust Prefab 칸에 넣습니다.
// 만든 뒤 모양을 다듬을 땐 이 메뉴를 다시 누르지 말고 프리팹을 직접 고칩니다(다시 누르면 덮어쓸지 묻습니다).
public static class RunDustVfxBuilder
{
    private const string PrefabPath = "Assets/VFX/Custom/VFX_RunDust.prefab";
    private const string MaterialPath = "Assets/VFX/Custom/Mat_RunDust.mat";
    private const string SettingsPath = "Assets/Resources/TravelBoostSettings.asset";

    [MenuItem("MobArchive/VFX/뛰기 흙먼지 이펙트 만들기")]
    public static void Build()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (mat == null) { EditorUtility.DisplayDialog("뛰기 흙먼지", "재질을 찾지 못했습니다:\n" + MaterialPath, "확인"); return; }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        bool build = prefab == null ||
                     EditorUtility.DisplayDialog("뛰기 흙먼지", "VFX_RunDust 프리팹이 이미 있습니다. 기본값으로 다시 만들까요?\n(직접 고친 내용은 사라집니다)", "다시 만들기", "그대로 두기");
        if (build) prefab = CreatePrefab(mat);

        var settings = AssetDatabase.LoadAssetAtPath<TravelBoostSettings>(SettingsPath);
        bool createdSettings = false;
        if (settings == null)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
            settings = ScriptableObject.CreateInstance<TravelBoostSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
            createdSettings = true;
        }
        Undo.RecordObject(settings, "뛰기 흙먼지 연결");
        settings.runDustPrefab = prefab;
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();

        EditorUtility.DisplayDialog("뛰기 흙먼지 완료",
            (build ? "VFX_RunDust 프리팹을 만들었습니다.\n" : "기존 VFX_RunDust 프리팹을 그대로 썼습니다.\n") +
            (createdSettings ? "Resources/TravelBoostSettings 에셋을 새로 만들고 " : "Resources/TravelBoostSettings의 ") +
            "Run Dust Prefab 칸에 연결했습니다.", "확인");
        EditorGUIUtility.PingObject(prefab);
    }

    private static GameObject CreatePrefab(Material mat)
    {
        var go = new GameObject("VFX_RunDust");
        try
        {
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = 0.3f;
            main.loop = false;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.55f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.55f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.10f, 0.18f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            // 회색~흰색 바탕에 갈색을 살짝 섞은 밝은 흙먼지색(형빈 확정)
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.66f, 0.62f, 0.57f, 1f), new Color(0.88f, 0.85f, 0.80f, 1f));
            main.gravityModifier = -0.03f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = 20;
            main.stopAction = ParticleSystemStopAction.None; // 지우기는 PlayerRunDust가 runDustLifetime 뒤에

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            // 짧은 간격으로 계속 나오므로(PlayerRunDust - 우다다다) 한 번에 2~3개만
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 2, 3) });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 30f;
            shape.radius = 0.02f;

            // 처음엔 빠르게 퍼졌다가 금방 느려지게(뭉게뭉게)
            var limit = ps.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.limit = 0f;
            limit.dampen = 0.12f;

            // 커졌다가 작아지며 사라짐(카툰 먼지 - 투명해지기보다 줄어들기)
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            var sizeCurve = new AnimationCurve(new Keyframe(0f, 0.55f), new Keyframe(0.25f, 1f), new Keyframe(1f, 0f));
            size.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            var color = ps.colorOverLifetime;
            color.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0.6f, 1f) });
            color.color = new ParticleSystem.MinMaxGradient(g);

            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-1.5f, 1.5f);

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            return PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }
}
