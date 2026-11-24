using System.Collections.Generic;
using UnityEngine;

// [3레벨 보스 1페이즈 - 지원 드론 보급] 지원 드론(Drone_Ayane)이 보급 상자를 매달고 방 모서리로 내려와 상자를 떨어뜨린 뒤 날아갑니다.
// BossLv3AIController가 만들고(Spawn) 관리합니다(보급 판단/정리는 보스 쪽). 프리팹에 미리 붙이는 컴포넌트가 아닙니다.
// [흐름] 위에서 감속하며 내려옴(프로펠러 회전) -> 상자 바닥이 Carry Height에 오면 잠깐 멈춤 -> 상자를 놓음(상자는 가속하며 바닥까지 떨어짐)
//        -> 잠깐 뒤 방 바깥쪽 위로 날아가며 사라짐.
// [보급 상자 - 형빈 확정] 일반 보물상자(TreasureChest)와 같이 F로 열고 F로 가져갑니다. 나타날 때 흰 빛 연출 없음, 다 가져가면 다른 상자처럼
//   흰 빛으로 사라짐. 상자 관련 아이템이 반응하지 않습니다 - 열기 신호 없음(suppressOpenEvents: 117/97/27/48번 X - 보급은 계속 오므로
//   117/97번이 반응하면 회복/보호막을 끝없이 얻을 수 있어 막음, 형빈 확정), 방 콘텐츠 밖에 두어 9번/106번(상자 복제/제거)이 찾지 못함.
//   방을 나가면 지워집니다(RoomScope).
// [상자 모델 - 형빈 확정] 일반방 상자(TreasureChest)든 동아리방 빨간 상자(ItemPedestal)든 넣을 수 있습니다. 동아리방 상자 프리팹이면
//   ItemPedestal(아이템 뽑기 상자 기능)을 떼고 TreasureChest(기본 아이템 상자 기능)를 붙여 씁니다.
// [상자 위치] 프리팹 루트의 회전(3ds Max에서 가져와 X -90 등)을 그대로 두고, 모델의 실제 모양(렌더러 범위)으로 '바닥 중앙'을 찾아
//   드론 바로 아래/바닥 자리에 맞춥니다 - 피벗이 모델 중앙이 아니어도 치우치지 않습니다.
public class BeaconSupplyDrone : MonoBehaviour
{
    private enum Phase { Descending, Hovering, Releasing, Departing, Done }

    private BeaconSupplySettings settings;
    private Phase phase;
    private float timer;

    private Vector3 floorPoint;      // 상자가 놓일 바닥 자리
    private Vector3 hoverPoint;      // 상자를 놓는 드론 위치
    private Vector3 startPoint;
    private Vector3 departDirection;

    private GameObject model;
    private readonly List<Transform> propellers = new List<Transform>();
    private Vector3 propellerAxis = Vector3.forward;

    private GameObject box;          // 매달린/떨어지는/놓인 상자
    private TreasureChest chest;     // 바닥에 놓인 뒤에 붙음
    private Vector3 boxDropStart;
    private Vector3 boxPivotFromBottom; // 상자 '바닥 중앙'에서 상자 피벗까지(월드, 피벗이 모델 중앙이 아니어도 바닥 중앙을 자리에 맞추려고)
    private readonly List<BasicItemType> contents = new List<BasicItemType>();

    // 상자를 아직 놓지 않았거나(떨어지는 중 포함) 놓인 상자가 있는지(다 가져가 사라지는 중이면 false).
    public bool HasLiveBox => box != null && (chest == null || !chest.IsOpened || chest.HasAnythingToPickup);
    // 아직 바닥에 닿지 않았거나, 닿았지만 열지 않은 상자.
    public bool HasUnopenedBox => box != null && (chest == null || !chest.IsOpened);
    // 상자가 놓일 바닥 자리(같은 모서리에 겹쳐 놓지 않게).
    public Vector3 FloorPoint => floorPoint;

    // 이 보급품 안에 type이 아직 남아 있는지(열지 않은 상자면 들어 있는 것 전부, 연 상자면 안 가져간 것).
    public bool HasUnpicked(BasicItemType type)
    {
        if (box == null) return false;
        if (chest == null || !chest.IsOpened) return contents.Contains(type);
        foreach (var stack in chest.BasicStacks)
        {
            if (stack != null && !stack.consumed && stack.type == type) return true;
        }
        return false;
    }

    // floor = 상자가 놓일 바닥 자리, lookTarget = 드론이 바라볼 곳(보스), items = 상자에 넣을 기본 아이템(각 1개씩 - 묶음 수는 settings).
    public static BeaconSupplyDrone Spawn(BeaconSupplySettings settings, Vector3 floor, Vector3 lookTarget, Vector3 roomCenter,
                                          IList<BasicItemType> items)
    {
        if (settings == null) return null;
        var root = new GameObject("Lv3SupplyDrone");
        RoomScope.DestroyWhenPlayerLeaves(root);
        var drone = root.AddComponent<BeaconSupplyDrone>();
        try
        {
            drone.Setup(settings, floor, lookTarget, roomCenter, items);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"BeaconSupplyDrone: 지원 드론 생성 중 오류가 발생해 보급을 건너뜁니다: {e}");
            if (drone.box != null) Destroy(drone.box);
            Destroy(root);
            return null;
        }
        return drone;
    }

    private void Setup(BeaconSupplySettings s, Vector3 floor, Vector3 lookTarget, Vector3 roomCenter, IList<BasicItemType> items)
    {
        settings = s;
        floorPoint = floor;
        if (items != null) contents.AddRange(items);

        Vector3 toLook = lookTarget - floor;
        toLook.y = 0f;
        Quaternion facing = toLook.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(toLook.normalized, Vector3.up) : Quaternion.identity;
        Vector3 outward = floor - roomCenter;
        outward.y = 0f;
        departDirection = outward.sqrMagnitude > 0.0001f ? outward.normalized : -(facing * Vector3.forward);

        // 드론 모델
        if (s.dronePrefab != null)
        {
            model = Instantiate(s.dronePrefab, transform);
            model.transform.localPosition = Vector3.zero;
        }
        else
        {
            model = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            model.transform.SetParent(transform, false);
            model.transform.localScale = Vector3.one * 0.5f;
            Destroy(model.GetComponent<Collider>());
        }
        model.name = "Model";
        foreach (var c in model.GetComponentsInChildren<Collider>(true)) c.enabled = false; // 드론은 막지 않음
        FindPropellers();

        // 매달린 상자(놓기 전에는 열 수 없음 - 상자 기능은 바닥에 닿을 때 켬)
        box = s.chestPrefab != null ? CreatePrefabBox(s.chestPrefab) : CreateFallbackBox();
        box.name = "Lv3SupplyBox";
        foreach (var c in box.GetComponentsInChildren<Collider>(true)) c.enabled = false;

        // 프리팹 루트 회전은 그대로(일반방/동아리방 상자처럼) 두고, 모델의 바닥 중앙이 드론 아래 Box Hang Offset 자리에 오게 맞춥니다.
        // (예전에는 회전을 0으로 덮어쓰고 피벗을 그대로 붙여서, 피벗이 모델 중앙이 아닌 상자는 한쪽으로 치우쳐 보였습니다.)
        Quaternion prefabRotation = s.chestPrefab != null ? s.chestPrefab.transform.rotation : Quaternion.identity;
        box.transform.SetPositionAndRotation(Vector3.zero, prefabRotation);
        boxPivotFromBottom = MeasurePivotFromBottomCenter(box);
        box.transform.SetParent(transform, false);
        box.transform.localPosition = Vector3.zero;
        box.transform.rotation = prefabRotation;

        // 상자 바닥이 Carry Height에 오는 드론 위치에서 놓습니다.
        hoverPoint = floor + Vector3.up * (s.carryHeight - s.boxHangOffset.y);
        startPoint = hoverPoint + Vector3.up * Mathf.Max(0f, s.spawnHeight);
        transform.SetPositionAndRotation(startPoint, facing);
        // 드론이 자리에 놓인 뒤 상자 위치를 맞춥니다(드론 회전과 상관없이 상자는 프리팹 회전 그대로).
        box.transform.SetPositionAndRotation(transform.TransformPoint(s.boxHangOffset) + boxPivotFromBottom, prefabRotation);
        phase = s.descendTime > 0f ? Phase.Descending : Phase.Hovering;
        timer = 0f;
    }

    // 프리팹 상자를 만듭니다. 꺼진 임시 부모 밑에서 먼저 만들어 Awake가 돌기 전에 상자 기능을 정리합니다:
    // - 동아리방 상자(ItemPedestal)면 ItemPedestal을 뗍니다(아이템 뽑기/아이콘/9번 복제용 기능이라 보급 상자에는 맞지 않음).
    // - TreasureChest가 없으면 미리 붙여 둡니다 - 뚜껑 연출(ChestLidOpener)이 Awake에서 열림 상태를 알려줄 상자 스크립트를 찾기 때문입니다.
    private static GameObject CreatePrefabBox(GameObject prefab)
    {
        var holder = new GameObject("Lv3SupplyBoxHolder");
        holder.SetActive(false);
        GameObject go = null;
        try
        {
            go = Instantiate(prefab, holder.transform);
            foreach (var pedestal in go.GetComponentsInChildren<ItemPedestal>(true)) DestroyImmediate(pedestal);

            var chest = go.GetComponent<TreasureChest>();
            if (chest == null)
            {
                if (go.GetComponent<Collider>() == null) go.AddComponent<BoxCollider>(); // TreasureChest는 콜라이더가 필요
                chest = go.AddComponent<TreasureChest>();
            }
            chest.suppressOpenEvents = true;
            chest.enabled = false; // 매달린 동안은 열 수 없음(바닥에 닿으면 켬)

            go.transform.SetParent(null, false); // 여기서 켜지며 Awake가 돕니다
        }
        finally
        {
            Destroy(holder);
        }
        return go;
    }

    // 지금 놓인 상태에서 모델의 '바닥 중앙'(렌더러 범위의 가로 중앙 + 가장 낮은 높이)에서 피벗까지의 거리를 잽니다.
    // 아이콘 같은 스프라이트/파티클은 빼고 메시 렌더러만 봅니다. 렌더러가 없으면 피벗 = 바닥 중앙으로 봅니다.
    private static Vector3 MeasurePivotFromBottomCenter(GameObject go)
    {
        bool has = false;
        Bounds b = default;
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;
            if (!has) { b = r.bounds; has = true; }
            else b.Encapsulate(r.bounds);
        }
        if (!has) return Vector3.zero;
        Vector3 bottomCenter = new Vector3(b.center.x, b.min.y, b.center.z);
        return go.transform.position - bottomCenter;
    }

    private static GameObject CreateFallbackBox()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.transform.localScale = new Vector3(0.6f, 0.4f, 0.4f);
        var r = go.GetComponent<Renderer>();
        if (r != null) r.material.color = new Color(0.85f, 0.7f, 0.2f);
        Debug.LogWarning("BeaconSupplyDrone: Supply Chest Prefab이 비어 임시 상자로 보급합니다.");
        return go;
    }

    private void FindPropellers()
    {
        propellers.Clear();
        propellerAxis = settings.propellerAxis.sqrMagnitude > 0.0001f ? settings.propellerAxis.normalized : Vector3.forward;
        if (settings.propellerNames == null || model == null) return;
        var all = model.GetComponentsInChildren<Transform>(true);
        foreach (string partName in settings.propellerNames)
        {
            if (string.IsNullOrEmpty(partName)) continue;
            Transform found = null;
            foreach (var t in all)
            {
                if (t.name == partName) { found = t; break; }
            }
            if (found != null) propellers.Add(found);
            else Debug.LogWarning($"BeaconSupplyDrone: 드론 모델 안에서 '{partName}' 프로펠러를 찾지 못해 회전시키지 않습니다(Hierarchy 이름 확인).");
        }
    }

    void Update()
    {
        SpinPropellers();
        timer += Time.deltaTime;

        switch (phase)
        {
            case Phase.Descending:
            {
                float t = Mathf.Clamp01(timer / Mathf.Max(settings.descendTime, 0.01f));
                float remain = 1f - t;
                transform.position = Vector3.Lerp(hoverPoint, startPoint, remain * remain * remain); // 감속하며 내려옴
                if (t >= 1f) { phase = Phase.Hovering; timer = 0f; }
                break;
            }
            case Phase.Hovering:
            {
                float bob = Mathf.Sin(timer * Mathf.PI * 2f * 2f) * 0.03f;
                transform.position = hoverPoint + Vector3.up * bob;
                if (timer >= settings.releasePause) ReleaseBox();
                break;
            }
            case Phase.Releasing:
            {
                UpdateBoxDrop();
                if (timer >= settings.departDelay && (chest != null || box == null)) { phase = Phase.Departing; timer = 0f; }
                break;
            }
            case Phase.Departing:
            {
                UpdateBoxDrop();
                float t = Mathf.Clamp01(timer / Mathf.Max(settings.departTime, 0.01f));
                float k = t * t; // 가속하며 떠남
                transform.position = hoverPoint + Vector3.up * (settings.departHeight * k) + departDirection * (settings.departDistance * k);
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(departDirection, Vector3.up), Time.deltaTime * 4f);
                if (t >= 1f)
                {
                    phase = Phase.Done;
                    if (model != null) DissolveAndVanish.Begin(model, DissolveAndVanish.EndAction.Destroy);
                }
                break;
            }
            case Phase.Done:
                // 드론 모델이 사라진 뒤에도 이 오브젝트는 상자 상태를 알려 주는 용도로 남습니다(상자가 사라지면 정리).
                if (box == null && model == null) Destroy(gameObject);
                break;
        }
    }

    private void SpinPropellers()
    {
        if (propellers.Count == 0 || settings.propellerSpeed == 0f) return;
        Quaternion step = Quaternion.AngleAxis(settings.propellerSpeed * Time.deltaTime, propellerAxis);
        for (int i = propellers.Count - 1; i >= 0; i--)
        {
            if (propellers[i] == null) { propellers.RemoveAt(i); continue; }
            propellers[i].localRotation = propellers[i].localRotation * step;
        }
    }

    // 상자를 놓습니다: 드론에서 떼어 방 소속으로 두고, 가속하며 바닥까지 떨어뜨립니다.
    private void ReleaseBox()
    {
        phase = Phase.Releasing;
        timer = 0f;
        if (box == null) return;
        box.transform.SetParent(null, true);
        RoomScope.DestroyWhenPlayerLeaves(box);
        boxDropStart = box.transform.position;
        if (settings.boxDropTime <= 0f) LandBox();
    }

    private void UpdateBoxDrop()
    {
        if (box == null || chest != null) return;
        float t = Mathf.Clamp01(timer / Mathf.Max(settings.boxDropTime, 0.01f));
        box.transform.position = Vector3.Lerp(boxDropStart, floorPoint + boxPivotFromBottom, t * t);
        if (t >= 1f) LandBox();
    }

    // 바닥에 닿은 상자에 보물상자 기능을 붙입니다(일반방 보물상자와 같이 통과 가능 + 같은 통과 레이어).
    private void LandBox()
    {
        if (box == null) return;
        box.transform.position = floorPoint + boxPivotFromBottom; // 상자 바닥 중앙이 바닥 자리에 오게
        try
        {
            foreach (var c in box.GetComponentsInChildren<Collider>(true)) c.enabled = true;
            if (box.GetComponent<Collider>() == null) box.AddComponent<BoxCollider>();
            chest = box.GetComponent<TreasureChest>();
            if (chest == null) chest = box.AddComponent<TreasureChest>();
            chest.suppressOpenEvents = true;
            chest.enabled = true;

            foreach (var col in box.GetComponentsInChildren<Collider>(true))
            {
                if (col is MeshCollider meshCollider && !meshCollider.convex) meshCollider.convex = true;
                col.isTrigger = true;
            }
            ApplyWalkthroughLayer(box);

            int amount = Mathf.Max(1, settings.amountPerItem);
            var itemsCopy = new List<BasicItemType>(contents);
            TreasureChest target = chest;
            chest.SetContentResolver(() =>
            {
                var stacks = new List<ResolvedBasicItemStack>();
                foreach (var type in itemsCopy)
                {
                    stacks.Add(new ResolvedBasicItemStack { type = type, count = amount, consumed = false });
                }
                target.SetBasicItemContent(stacks);
            });
            if (settings.playLandingSound) GameAudio.Play(settings.landingSound);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"BeaconSupplyDrone: 보급 상자 준비 중 오류가 발생했습니다: {e}");
        }
    }

    // 일반방 보물상자 스포너에 통과 레이어가 정해져 있으면 같은 레이어로(PresetBasicItemChest와 같은 방식).
    private static void ApplyWalkthroughLayer(GameObject go)
    {
        var spawner = FindFirstObjectByType<NormalRoomTreasureSpawner>();
        if (spawner == null || string.IsNullOrEmpty(spawner.walkthroughLayerName)) return;
        int layerIndex = LayerMask.NameToLayer(spawner.walkthroughLayerName);
        if (layerIndex < 0) return;
        foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layerIndex;
    }

    // 기믹이 풀리면(모든 비콘 해제) 보급 상자와 안의 것을 바로 없앱니다 - 흰 빛으로 사라짐(fade) 또는 즉시(보스 사망/방 꺼짐 등).
    // 아직 상자를 매달고 있는 드론도 함께 사라집니다. 상자를 놓고 떠나는 중인 드론은 그대로 날아갑니다.
    public void Vanish(bool fade)
    {
        bool carrying = phase == Phase.Descending || phase == Phase.Hovering;
        if (box != null)
        {
            if (chest != null) chest.enabled = false; // 사라지는 동안 F로 가져가지 못하게
            if (fade && box.activeInHierarchy && !carrying) DissolveAndVanish.Begin(box, DissolveAndVanish.EndAction.Destroy);
            else if (!carrying) Destroy(box);
        }
        box = null;
        chest = null;

        if (carrying || !fade)
        {
            phase = Phase.Done;
            if (fade && gameObject.activeInHierarchy) DissolveAndVanish.Begin(gameObject, DissolveAndVanish.EndAction.Destroy);
            else Destroy(gameObject);
        }
    }

}

// 보스 Inspector에서 고치는 지원 드론 보급 설정입니다(BossLv3AIController.supplySettings).
[System.Serializable]
public class BeaconSupplySettings
{
    [Header("모델")]
    [Tooltip("지원 드론 모델 프리팹(Drone_Ayane). 비우면 임시 공 모양.")]
    public GameObject dronePrefab;
    [Tooltip("보급 상자 모델 프리팹 - 일반방 보물상자(흰 상자) 또는 동아리방 보물상자(빨간 상자) 프리팹을 그대로 넣으면 됩니다(뚜껑 열림 연출 포함). " +
             "동아리방 상자의 아이템 뽑기 기능은 자동으로 빼고 보급 상자로 씁니다. 비우면 임시 상자.")]
    public GameObject chestPrefab;
    [Tooltip("드론 기준 매달린 상자의 '바닥 중앙' 위치(로컬, 드론 아래쪽이면 Y 음수). 상자는 모델 모양 기준으로 이 점에 맞춰집니다. " +
             "상자 윗면이 드론에 파묻히면 Y를 더 낮게(예: -0.8), 너무 떨어져 보이면 높게.")]
    public Vector3 boxHangOffset = new Vector3(0f, -0.6f, 0f);

    [Header("프로펠러 (63번 드론과 같은 방식)")]
    [Tooltip("계속 돌릴 부품 이름(모델 Hierarchy에 보이는 이름 그대로).")]
    public List<string> propellerNames = new List<string> { "Object7872", "Object7873", "Object7874", "Object7875" };
    [Tooltip("각 프로펠러가 도는 축(그 부품의 로컬 축). 3ds Max의 위쪽 축 = Z(0, 0, 1). 엉뚱하게 돌면 (0, 1, 0)으로 바꿔 보세요.")]
    public Vector3 propellerAxis = new Vector3(0f, 0f, 1f);
    [Tooltip("프로펠러 회전 속도(도/초). 음수면 반대로.")]
    public float propellerSpeed = 1440f;

    [Header("내려오기 / 놓기 / 떠나기")]
    [Tooltip("내려오기 시작하는 높이(상자를 놓는 높이 위, 월드 단위).")]
    [Min(0f)] public float spawnHeight = 8f;
    [Tooltip("내려오는 시간(초). 비콘과 같이 도착하게 비콘 하강 시간과 비슷하게.")]
    [Min(0f)] public float descendTime = 1.6f;
    [Tooltip("상자를 놓을 때 상자 바닥 높이(바닥 위, 월드 단위). 이 높이에서 떨어집니다.")]
    [Min(0f)] public float carryHeight = 0.5f;
    [Tooltip("내려온 뒤 상자를 놓기까지 머무는 시간(초).")]
    [Min(0f)] public float releasePause = 0.25f;
    [Tooltip("상자가 바닥까지 떨어지는 시간(초).")]
    [Min(0f)] public float boxDropTime = 0.25f;
    [Tooltip("상자를 놓고 떠나기 시작하기까지(초).")]
    [Min(0f)] public float departDelay = 0.35f;
    [Tooltip("떠나는 시간(초) - 이 동안 위로/방 바깥쪽으로 가속하며 날아가고 끝나면 사라짐.")]
    [Min(0.05f)] public float departTime = 1.5f;
    [Min(0f)] public float departHeight = 6f;
    [Min(0f)] public float departDistance = 5f;
    public bool playLandingSound = false;
    public SfxId landingSound = SfxId.ItemPickup;

    [Header("보급")]
    [Tooltip("상자에 든 아이템 하나당 개수(수류탄/해킹툴).")]
    [Min(1)] public int amountPerItem = 1;
    [Tooltip("재보급 조건(수류탄/해킹 비콘이 남아 있음 + 그 재화 0개 + 열지 않은 상자/안 가져간 그 재화 없음)이 이 시간(초) 동안 이어지면 드론이 다시 옵니다.")]
    [Min(0f)] public float resupplyDelay = 3f;
}
