using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// [비전투 달리기 애니메이터 설정 메뉴] MobArchive/애니메이션/비전투 달리기 - A(닌자) 또는 B(클레)
// 컨트롤러 파일을 손으로 고치지 않고 Unity API로 Run 상태/전환/파라미터를 넣습니다. 여러 번 눌러도 같은 결과(기존 Run 상태는 지우고 다시 만듦) -
// 그래서 두 메뉴를 번갈아 누르는 것만으로 뛰기 모션(SD_Run = A, SD_Run_B = B)을 바꿔 끼울 수 있습니다.
// 하는 일: ReisaAnimatorController에 IsRunning(Bool)/RunSpeed(Float) + Locomotion·UpperBody 레이어 Run 상태(고른 클립, 재생 속도 = RunSpeed),
// UpperBodyTorso(Sync 레이어)에 Run 모션 지정, 레이사 Generic Override에 Run 대체 클립(걷기) 지정,
// 플레이어 프리팹 3개의 HumanoidAimLookAt 막을 상태에 UpperBody.Run 추가(뛰는 동안 양손을 총에 붙이는 보정을 끔).
public static class RunAnimatorSetupTool
{
    private const string ControllerPath = "Assets/Player/Character/Animation/ReisaAnimatorController.controller";
    private const string RunClipPathA = "Assets/Player/Character/Animation/SD_Run.fbx";   // A: 닌자 달리기(팔 뒤로, 상체 숙임)
    private const string RunClipPathB = "Assets/Player/Character/Animation/SD_Run_B.fbx"; // B: 클레 달리기(팔 좌우로, 허리 폄)
    private const string ReisaOverridePath = "Assets/Player/Character/Animation/Generic/ReisaAnimatorOverride_Generic.overrideController";
    private const string ReisaRunFallbackClipPath = "Assets/Player/Character/Animation/Generic/Aiming Walk Forward_Generic.fbx";
    private static readonly string[] PlayerPrefabPaths =
    {
        "Assets/Player/Character/Player/SD_Player.prefab",
        "Assets/Player/Character/Player/UnityChan_Player.prefab",
        "Assets/Player/Character/Player/Reisa_Player_Fixed.prefab",
    };

    private const string RunState = "Run";
    private const string IsRunningParam = "IsRunning";
    private const string RunSpeedParam = "RunSpeed";
    private const float BlendDuration = 0.15f;

    [MenuItem("MobArchive/애니메이션/비전투 달리기 - A (닌자 달리기)")]
    public static void SetupA() => Setup(RunClipPathA);

    [MenuItem("MobArchive/애니메이션/비전투 달리기 - B (클레 달리기)")]
    public static void SetupB() => Setup(RunClipPathB);

    public static void Setup(string runClipPath)
    {
        var log = new List<string>();

        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null) { EditorUtility.DisplayDialog("달리기 상태 추가", "컨트롤러를 찾지 못했습니다:\n" + ControllerPath, "확인"); return; }
        AnimationClip runClip = LoadClip(runClipPath);
        if (runClip == null) { EditorUtility.DisplayDialog("달리기 상태 추가", "뛰기 클립을 찾지 못했습니다:\n" + runClipPath, "확인"); return; }
        log.Add("뛰기 클립: " + runClip.name);

        Undo.RecordObject(controller, "달리기 상태 추가");
        EnsureParameter(controller, IsRunningParam, AnimatorControllerParameterType.Bool, 0f, log);
        EnsureParameter(controller, RunSpeedParam, AnimatorControllerParameterType.Float, 1f, log);

        AnimatorControllerLayer[] layers = controller.layers;
        int locoIndex = FindLayer(layers, "Locomotion");
        int upperIndex = FindLayer(layers, "UpperBody");
        int torsoIndex = FindLayer(layers, "UpperBodyTorso");
        if (locoIndex < 0 || upperIndex < 0)
        {
            EditorUtility.DisplayDialog("달리기 상태 추가", "Locomotion 또는 UpperBody 레이어가 없습니다.", "확인");
            return;
        }

        // ---- Locomotion: AimLocomotion <-> Run ----
        AnimatorStateMachine loco = layers[locoIndex].stateMachine;
        AnimatorState locoRun = RecreateState(loco, runClip, new Vector3(330, 290, 0));
        AnimatorState aimLoco = FindState(loco, "AimLocomotion");
        if (aimLoco != null)
        {
            AddTransition(aimLoco, locoRun, (AnimatorConditionMode.If, IsRunningParam, 0f));
            AddTransition(locoRun, aimLoco, (AnimatorConditionMode.IfNot, IsRunningParam, 0f));
            log.Add("Locomotion: AimLocomotion <-> Run 전환");
        }
        else log.Add("[경고] Locomotion에 AimLocomotion 상태가 없어 전환을 만들지 못함");

        // ---- UpperBody: 무기 모드별 대기 <-> Run ----
        AnimatorStateMachine upper = layers[upperIndex].stateMachine;
        AnimatorState upperRun = RecreateState(upper, runClip, new Vector3(-80, 120, 0));
        var idleByMode = new (string state, int mode)[] { ("NonAimIdle", 0), ("Grenade_Idle", 1), ("Hack_Idle", 2) };
        foreach (var (stateName, mode) in idleByMode)
        {
            AnimatorState idle = FindState(upper, stateName);
            if (idle == null) { log.Add("[경고] UpperBody에 " + stateName + " 상태가 없음"); continue; }
            AddTransition(idle, upperRun, (AnimatorConditionMode.If, IsRunningParam, 0f));
            AddTransition(upperRun, idle, (AnimatorConditionMode.IfNot, IsRunningParam, 0f), (AnimatorConditionMode.Equals, "WeaponMode", mode));
        }
        AnimatorState reloading = FindState(upper, "Reloading");
        if (reloading != null) AddTransition(upperRun, reloading, (AnimatorConditionMode.If, "IsReloading", 0f));
        log.Add("UpperBody: NonAimIdle/Grenade_Idle/Hack_Idle <-> Run 전환 (+ Run -> Reloading)");

        // ---- UpperBodyTorso(Sync): Run 모션 지정 (Weight는 TorsoLayerWeightByState가 정하므로 평소 0) ----
        if (torsoIndex >= 0 && layers[torsoIndex].syncedLayerIndex == upperIndex)
        {
            layers[torsoIndex].SetOverrideMotion(upperRun, runClip);
            log.Add("UpperBodyTorso: Run 모션 지정");
        }
        controller.layers = layers;
        EditorUtility.SetDirty(controller);

        // ---- 레이사(Generic) Override: Humanoid 클립은 Generic 뼈대에 안 붙으므로 걷기 클립으로 대체 ----
        var reisaOverride = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(ReisaOverridePath);
        AnimationClip reisaRun = LoadClip(ReisaRunFallbackClipPath);
        if (reisaOverride != null && reisaRun != null)
        {
            Undo.RecordObject(reisaOverride, "달리기 상태 추가");
            var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>(reisaOverride.overridesCount);
            reisaOverride.GetOverrides(overrides);
            for (int i = 0; i < overrides.Count; i++)
                if (overrides[i].Key == runClip) overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(runClip, reisaRun);
            reisaOverride.ApplyOverrides(overrides);
            EditorUtility.SetDirty(reisaOverride);
            log.Add("레이사 Generic Override: Run -> " + reisaRun.name);
        }
        else log.Add("[경고] 레이사 Generic Override 또는 대체 클립을 찾지 못함(레이사를 안 쓰면 무시해도 됨)");

        // ---- 프리팹: HumanoidAimLookAt 막을 상태에 UpperBody.Run ----
        foreach (string path in PlayerPrefabPaths)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) { log.Add("[경고] 프리팹 없음: " + path); continue; }
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            int added = 0, found = 0;
            try
            {
                foreach (var look in root.GetComponentsInChildren<HumanoidAimLookAt>(true))
                {
                    found++;
                    if (look.blockedStates == null) look.blockedStates = new List<HumanoidAimLookAt.StateRef>();
                    bool has = look.blockedStates.Any(s => s != null && s.layerName == "UpperBody" && s.stateName == RunState);
                    if (!has) { look.blockedStates.Add(new HumanoidAimLookAt.StateRef("UpperBody", RunState)); added++; }
                }
                if (added > 0) PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            log.Add(System.IO.Path.GetFileNameWithoutExtension(path) + ": HumanoidAimLookAt " + found + "개 중 " + added + "개에 Run 추가");
        }

        AssetDatabase.SaveAssets();
        string msg = string.Join("\n", log);
        Debug.Log("[달리기 상태 추가]\n" + msg);
        EditorUtility.DisplayDialog("달리기 상태 추가 완료", msg, "확인");
    }

    private static AnimationClip LoadClip(string path)
    {
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
            .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
    }

    private static int FindLayer(AnimatorControllerLayer[] layers, string name)
    {
        for (int i = 0; i < layers.Length; i++) if (layers[i].name == name) return i;
        return -1;
    }

    private static AnimatorState FindState(AnimatorStateMachine sm, string name)
    {
        foreach (var cs in sm.states) if (cs.state.name == name) return cs.state;
        return null;
    }

    private static void EnsureParameter(AnimatorController c, string name, AnimatorControllerParameterType type, float defaultValue, List<string> log)
    {
        if (c.parameters.Any(p => p.name == name)) return;
        var p = new AnimatorControllerParameter { name = name, type = type, defaultFloat = defaultValue };
        c.AddParameter(p);
        log.Add("파라미터 추가: " + name);
    }

    // 같은 이름 상태가 있으면 그 상태와 그 상태로 가는 전환을 지우고 새로 만듭니다(메뉴를 다시 눌러도 중복되지 않게).
    private static AnimatorState RecreateState(AnimatorStateMachine sm, AnimationClip clip, Vector3 position)
    {
        AnimatorState old = FindState(sm, RunState);
        if (old != null)
        {
            foreach (var cs in sm.states)
                foreach (var t in cs.state.transitions.Where(t => t.destinationState == old).ToArray())
                    cs.state.RemoveTransition(t);
            foreach (var t in sm.anyStateTransitions.Where(t => t.destinationState == old).ToArray())
                sm.RemoveAnyStateTransition(t);
            sm.RemoveState(old);
        }

        AnimatorState s = sm.AddState(RunState, position);
        s.motion = clip;
        s.speedParameterActive = true;
        s.speedParameter = RunSpeedParam;
        // 같은 레이어 다른 상태의 Write Defaults 설정을 따라갑니다(섞이면 자세가 튈 수 있음).
        foreach (var cs in sm.states)
            if (cs.state != s) { s.writeDefaultValues = cs.state.writeDefaultValues; break; }
        return s;
    }

    private static void AddTransition(AnimatorState from, AnimatorState to, params (AnimatorConditionMode mode, string param, float threshold)[] conditions)
    {
        AnimatorStateTransition t = from.AddTransition(to);
        t.hasExitTime = false;
        t.duration = BlendDuration;
        t.hasFixedDuration = true;
        foreach (var c in conditions) t.AddCondition(c.mode, c.threshold, c.param);
    }
}
