using System.Collections.Generic;
using UnityEngine;

// [밤 전용 빛 자동 부착 규칙 - Project 창 우클릭 > Create > MobArchive > Night Light Auto Rules]
// 견본 프리팹(모델 1개 + NightLight 오브젝트들)을 등록해 두면, 전투 레벨이 만들어진 직후
// 레벨 안에서 "같은 모델(메시)"을 쓰는 오브젝트를 모두 찾아 견본의 NightLight를 같은 상대 위치에 복제해 붙입니다.
// 프리팹 연결이 끊어진 벽 프리팹이라도 안의 모델(FBX)이 같으면 적용됩니다.
// 빛은 모델의 자식으로 붙기 때문에 모델의 위치/회전/크기 조절을 그대로 따라갑니다.
// 팔레트 스왑처럼 모양은 같고 FBX만 다른 모델은 "같은 모양 모델" 목록에 넣으면 같은 견본을 씁니다.
[CreateAssetMenu(menuName = "MobArchive/Night Light Auto Rules", fileName = "NightLightAutoRules")]
public class NightLightAutoRules : ScriptableObject
{
    [System.Serializable]
    public class Rule
    {
        [Tooltip("구분용 이름(아무거나)")]
        public string label;

        [Tooltip("견본 프리팹. 안에 모델(FBX) 1개와 NightLight가 붙은 오브젝트들이 들어 있어야 합니다. 모델은 견본 안에서 가장 먼저 나오는 메시를 기준으로 삼습니다.")]
        public GameObject template;

        [Tooltip("견본 모델과 모양/기준점이 같은 다른 모델들(팔레트 스왑 FBX 등). FBX 파일을 그대로 끌어다 넣으면 됩니다.")]
        public List<GameObject> sameShapeModels = new List<GameObject>();
    }

    [Tooltip("자동 부착 규칙 목록(견본 1개 = 규칙 1개)")]
    public List<Rule> rules = new List<Rule>();

    private const string AutoPrefix = "NightLight_Auto_";

    private struct Entry
    {
        public GameObject source;
        public Vector3 localPosition;
        public Quaternion localRotation;
        public Vector3 localScale;
    }

    // 전투 레벨 빌드 직후 호출합니다(GameFlowManager). 가장 최근에 만들어진 LevelRoot 안만 찾습니다.
    public static void AttachInLevel(LevelBuilder builder, NightLightAutoRules rulesAsset)
    {
        if (builder == null || rulesAsset == null)
            return;

        Transform levelRoot = null;
        Transform bt = builder.transform;
        for (int i = bt.childCount - 1; i >= 0; i--)
        {
            Transform child = bt.GetChild(i);
            if (child.name == "LevelRoot")
            {
                levelRoot = child;
                break;
            }
        }

        if (levelRoot == null)
            return;

        rulesAsset.AttachUnder(levelRoot);
    }

    public void AttachUnder(Transform root)
    {
        if (root == null)
            return;

        Dictionary<Mesh, List<Entry>> map = BuildMap();
        if (map.Count == 0)
            return;

        MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);
        int modelCount = 0;
        int lightCount = 0;

        for (int i = 0; i < filters.Length; i++)
        {
            MeshFilter mf = filters[i];
            if (mf == null || mf.sharedMesh == null)
                continue;

            if (!map.TryGetValue(mf.sharedMesh, out List<Entry> entries))
                continue;

            Transform target = mf.transform;
            if (AlreadyHasLights(target))
                continue;

            for (int e = 0; e < entries.Count; e++)
            {
                Entry entry = entries[e];
                GameObject go = Instantiate(entry.source, target, false);
                go.name = AutoPrefix + entry.source.name;
                go.layer = target.gameObject.layer;
                Transform t = go.transform;
                t.localPosition = entry.localPosition;
                t.localRotation = entry.localRotation;
                t.localScale = entry.localScale;
                lightCount++;
            }

            modelCount++;
        }

        if (lightCount > 0)
            Debug.Log($"[NightLight] 자동 부착: 모델 {modelCount}개에 빛 {lightCount}개");
    }

    // 이미 빛이 붙어 있는 모델은 건너뜁니다(자동으로 붙인 것, 또는 벽 프리팹에 손으로 남아 있는 것 - 중복 방지).
    private static bool AlreadyHasLights(Transform target)
    {
        for (int i = 0; i < target.childCount; i++)
        {
            if (target.GetChild(i).name.StartsWith(AutoPrefix))
                return true;
        }

        // 같은 벽 프리팹 안에 손으로 붙인 NightLight가 남아 있으면 건너뜁니다(자동으로 붙인 것은 제외하고 셉니다).
        Transform owner = target.parent != null ? target.parent : target;
        NightLight[] existing = owner.GetComponentsInChildren<NightLight>(true);
        for (int i = 0; i < existing.Length; i++)
        {
            if (!existing[i].gameObject.name.StartsWith(AutoPrefix))
                return true;
        }
        return false;
    }

    private Dictionary<Mesh, List<Entry>> BuildMap()
    {
        var map = new Dictionary<Mesh, List<Entry>>();
        if (rules == null)
            return map;

        for (int r = 0; r < rules.Count; r++)
        {
            Rule rule = rules[r];
            if (rule == null || rule.template == null)
                continue;

            string ruleName = string.IsNullOrEmpty(rule.label) ? rule.template.name : rule.label;

            MeshFilter baseFilter = FindFirstMesh(rule.template);
            if (baseFilter == null)
            {
                Debug.LogWarning($"[NightLight] 견본 '{ruleName}' 안에 모델(메시)이 없습니다.", this);
                continue;
            }

            NightLight[] lights = rule.template.GetComponentsInChildren<NightLight>(true);
            if (lights.Length == 0)
            {
                Debug.LogWarning($"[NightLight] 견본 '{ruleName}' 안에 NightLight가 없습니다.", this);
                continue;
            }

            // 견본 모델 기준 상대 위치/회전/크기.
            Matrix4x4 toModel = baseFilter.transform.worldToLocalMatrix;
            var entries = new List<Entry>(lights.Length);
            for (int i = 0; i < lights.Length; i++)
            {
                Transform lt = lights[i].transform;
                Matrix4x4 rel = toModel * lt.localToWorldMatrix;
                entries.Add(new Entry
                {
                    source = lights[i].gameObject,
                    localPosition = rel.GetColumn(3),
                    localRotation = rel.rotation,
                    localScale = rel.lossyScale
                });
            }

            AddMesh(map, baseFilter.sharedMesh, entries, ruleName);

            if (rule.sameShapeModels != null)
            {
                for (int m = 0; m < rule.sameShapeModels.Count; m++)
                {
                    GameObject model = rule.sameShapeModels[m];
                    if (model == null)
                        continue;

                    MeshFilter mf = FindFirstMesh(model);
                    if (mf == null)
                    {
                        Debug.LogWarning($"[NightLight] 견본 '{ruleName}'의 같은 모양 모델 '{model.name}'에서 메시를 찾지 못했습니다.", this);
                        continue;
                    }

                    AddMesh(map, mf.sharedMesh, entries, ruleName);
                }
            }
        }

        return map;
    }

    private void AddMesh(Dictionary<Mesh, List<Entry>> map, Mesh mesh, List<Entry> entries, string ruleName)
    {
        if (mesh == null)
            return;

        if (map.ContainsKey(mesh))
        {
            Debug.LogWarning($"[NightLight] 모델 '{mesh.name}'이 여러 견본에 등록되어 있습니다. 먼저 등록된 견본을 씁니다('{ruleName}' 무시).", this);
            return;
        }

        map.Add(mesh, entries);
    }

    private static MeshFilter FindFirstMesh(GameObject go)
    {
        MeshFilter[] filters = go.GetComponentsInChildren<MeshFilter>(true);
        for (int i = 0; i < filters.Length; i++)
        {
            if (filters[i] != null && filters[i].sharedMesh != null)
                return filters[i];
        }
        return null;
    }
}
