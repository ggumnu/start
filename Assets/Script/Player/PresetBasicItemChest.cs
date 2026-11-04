using System.Collections.Generic;
using UnityEngine;

// 방 바닥 프리팹 안에 미리 놓아두는 "내용물이 정해진 종류의 기본 아이템 상자"입니다(예: 크레딧 더미 레이아웃의 크레딧 상자).
// 일반방 보물상자(TreasureChest)에 이 컴포넌트를 함께 붙이면, 그 상자는 열릴 때 아래 설정대로 내용물을 채웁니다.
//
// [크레딧 더미 기획 - 형빈 확정]
// - 상자를 F로 열면 크레딧이 2~5 들어 있습니다(2/3/4/5가 각각 25% - 아래 Line Count Weights가 모두 1).
// - 툴팁에는 합계 한 줄("크레딧 +3")이 아니라 1크레딧짜리 줄이 그 개수만큼("크레딧 +1" 3줄) 보입니다 - 많아 보이게.
// - 각 줄이 실제 획득 단위이기도 합니다(형빈 확정 A안). 그래서 30번처럼 "크레딧 획득 시 +N" 아이템이 있으면 줄마다
//   보너스가 붙습니다(3줄 = (1+N) x 3). 일반방 상자에서 크레딧 묶음 두 개가 나왔을 때 묶음마다 보너스가 붙는 것과 같은
//   규칙입니다(PlayerResources.Add가 호출될 때마다 보너스).
//
// [동작]
// - TreasureChest와 똑같이 "열기 전까지 내용물이 정해지지 않습니다". Awake에서 TreasureChest에 내용물 결정 함수만
//   맡겨 두고(SetContentResolver), 플레이어가 F로 처음 여는 순간 줄 수를 뽑습니다. F 안내/툴팁(TreasureChestInfoDisplay)/
//   미니맵 아이콘/획득은 전부 기존 TreasureChest 로직 그대로입니다.
// - 48번(가중치 뒤섞기)이 있으면 줄 수 가중치도 일반 상자와 같은 방식(1~3 랜덤)으로 뒤섞입니다 - TreasureChest.Open()이
//   내용물 결정 직전에 48번 무장 신호를 보내고 직후에 풀어 주므로, 여기서는 무장 여부만 확인합니다.
// - 통과 가능: Walk Through가 켜져 있으면 시작할 때 상자의 모든 콜라이더를 트리거로 바꿔 플레이어가 지나갈 수 있게 합니다
//   (일반방 보물상자와 같은 방식 - NormalRoomTreasureSpawner.PrepareChestComponents). 일반방 보물상자 스포너에
//   Walkthrough Layer Name이 설정되어 있으면 그 레이어도 똑같이 적용합니다.
//
// [9번 음식]
// 레이아웃 에셋의 Allow Chest Duplication이 켜진 특수방에서 9번을 쓰면, 이 상자는 같은 설정의 상자로 복제됩니다
// (NormalRoomTreasureSpawner.SpawnDuplicatePresetChest). 복제 상자의 모델은 Duplicate Prefab 칸(비우면 일반방 상자 프리팹)입니다.
//
// [설정]
// 바닥 프리팹 안에 보물상자 프리팹을 놓고, 그 상자 루트(TreasureChest가 있거나 붙을 오브젝트)에 이 컴포넌트를 추가합니다.
// TreasureChest가 없으면 자동으로 붙이고, 루트에 콜라이더가 없으면 BoxCollider를 붙입니다. 방 콘텐츠 안에 들어 있으므로
// 그 방에 있을 때만 보입니다.
public class PresetBasicItemChest : MonoBehaviour
{
    [Header("내용물")]
    [Tooltip("상자에 들어갈 기본 아이템 종류입니다(크레딧 더미 = Credit).")]
    public BasicItemType itemType = BasicItemType.Credit;

    [Tooltip("줄 하나당 개수입니다. 크레딧 더미는 1(= \"크레딧 +1\" 줄이 여러 개).")]
    [Min(1)] public int amountPerLine = 1;

    [Tooltip("몇 줄이 들어갈지와 그 가중치입니다(확률 = 그 줄의 가중치 / 가중치 합). 기본값은 2~5줄이 각각 25%입니다. " +
             "가중치가 0인 줄은 뽑히지 않습니다.")]
    public List<PresetLineCountOption> lineCountWeights = new List<PresetLineCountOption>
    {
        new PresetLineCountOption { lineCount = 2, weight = 1f },
        new PresetLineCountOption { lineCount = 3, weight = 1f },
        new PresetLineCountOption { lineCount = 4, weight = 1f },
        new PresetLineCountOption { lineCount = 5, weight = 1f },
    };

    [Header("9번 음식 복제")]
    [Tooltip("이 방에서 9번 음식(상자 복제)을 썼을 때 복제 상자에 쓸 모델 프리팹입니다. 비워 두면 일반방 보물상자 프리팹" +
             "(NormalRoomTreasureSpawner > Treasure Chest Prefab)을 씁니다. 복제 상자에도 이 컴포넌트와 같은 설정이 붙으므로 " +
             "크레딧 상자는 크레딧 상자로만 복제됩니다. 9번을 쓸 수 있는지는 레이아웃 에셋의 Allow Chest Duplication으로 정합니다.")]
    public GameObject duplicatePrefab;

    [Header("통과")]
    [Tooltip("켜면 시작할 때 상자의 모든 콜라이더를 트리거로 바꿔 플레이어가 지나갈 수 있게 합니다(일반방 보물상자와 같음).")]
    public bool walkThrough = true;

    private TreasureChest chest;

    // 9번 음식 복제용 - 원본의 설정을 그대로 복사합니다(NormalRoomTreasureSpawner.SpawnDuplicatePresetChest).
    // 내용물 결정 함수는 열릴 때 이 값들을 읽으므로, 컴포넌트를 붙인 뒤에 복사해도 됩니다.
    public void CopySettingsFrom(PresetBasicItemChest source)
    {
        if (source == null) return;
        itemType = source.itemType;
        amountPerLine = source.amountPerLine;
        duplicatePrefab = source.duplicatePrefab;
        walkThrough = source.walkThrough;

        lineCountWeights = new List<PresetLineCountOption>();
        if (source.lineCountWeights != null)
        {
            foreach (var option in source.lineCountWeights)
            {
                if (option == null) continue;
                lineCountWeights.Add(new PresetLineCountOption { lineCount = option.lineCount, weight = option.weight });
            }
        }
    }

    void Awake()
    {
        // TreasureChest는 Collider를 요구하는데 Collider는 추상 타입이라 Unity가 자동으로 붙여 주지 못하므로, 먼저 확인합니다
        // (NormalRoomTreasureSpawner.PrepareChestComponents와 같은 순서).
        if (GetComponent<Collider>() == null) gameObject.AddComponent<BoxCollider>();

        chest = GetComponent<TreasureChest>();
        if (chest == null) chest = gameObject.AddComponent<TreasureChest>();

        if (walkThrough) MakeWalkThrough();

        chest.SetContentResolver(ResolveContent);
    }

    // TreasureChest.Open()이 처음 열릴 때 딱 한 번 호출합니다.
    private void ResolveContent()
    {
        int lineCount = PickLineCount();
        var stacks = new List<ResolvedBasicItemStack>();
        for (int i = 0; i < lineCount; i++)
        {
            stacks.Add(new ResolvedBasicItemStack
            {
                type = itemType,
                count = Mathf.Max(1, amountPerLine),
                consumed = false,
            });
        }
        chest.SetBasicItemContent(stacks);
    }

    // 줄 수를 가중치로 뽑습니다. 48번이 무장되어 있으면 각 후보의 가중치를 1~3 랜덤 정수로 바꿔 뽑습니다(일반방 상자의
    // PickWeightedBundle과 같은 규칙, 같은 디버그 로그). 쓸 수 있는 후보가 없으면 0줄(빈 상자 - 열자마자 사라짐)입니다.
    private int PickLineCount()
    {
        var valid = new List<PresetLineCountOption>();
        if (lineCountWeights != null)
        {
            foreach (var option in lineCountWeights)
            {
                if (option != null && option.weight > 0f && option.lineCount > 0) valid.Add(option);
            }
        }

        if (valid.Count == 0)
        {
            Debug.LogWarning($"PresetBasicItemChest: '{name}'의 Line Count Weights에 쓸 수 있는 줄이 없어 빈 상자가 됩니다.");
            return 0;
        }
        if (valid.Count == 1) return valid[0].lineCount;

        bool scramble = NextItemPickWeightOverride.IsArmed;
        float[] effectiveWeights = new float[valid.Count];
        float totalWeight = 0f;
        for (int i = 0; i < valid.Count; i++)
        {
            effectiveWeights[i] = scramble ? Random.Range(1, 4) : valid[i].weight;
            totalWeight += effectiveWeights[i];
        }

        float roll = Random.value * totalWeight;
        float cumulative = 0f;
        int pickedIndex = valid.Count - 1;
        for (int i = 0; i < valid.Count; i++)
        {
            cumulative += effectiveWeights[i];
            if (roll <= cumulative)
            {
                pickedIndex = i;
                break;
            }
        }

        if (scramble)
        {
            Debug.Log($"[48번] {itemType} {valid[pickedIndex].lineCount}줄이 임시 가중치 {effectiveWeights[pickedIndex]}로 등장했습니다.");
        }
        return valid[pickedIndex].lineCount;
    }

    // 상자와 자식의 모든 콜라이더를 트리거로 바꿉니다. Non-convex Mesh Collider는 트리거가 되지 않으므로 Convex로 바꿉니다
    // (상호작용 범위는 거리로 판정하므로 콜라이더 모양은 상관없음). 일반방 상자 스포너에 통과용 레이어가 설정되어 있으면 그것도
    // 똑같이 적용합니다.
    private void MakeWalkThrough()
    {
        foreach (var col in GetComponentsInChildren<Collider>(true))
        {
            if (col is MeshCollider meshCollider && !meshCollider.convex) meshCollider.convex = true;
            col.isTrigger = true;
        }

        var spawner = Object.FindFirstObjectByType<NormalRoomTreasureSpawner>();
        if (spawner == null || string.IsNullOrEmpty(spawner.walkthroughLayerName)) return;

        int layerIndex = LayerMask.NameToLayer(spawner.walkthroughLayerName);
        if (layerIndex < 0) return; // 레이어가 없다는 경고는 일반방 상자 스포너가 이미 남깁니다.

        foreach (var t in GetComponentsInChildren<Transform>(true))
        {
            t.gameObject.layer = layerIndex;
        }
    }
}

// 줄 수 후보 한 줄입니다(줄 수 + 가중치).
[System.Serializable]
public class PresetLineCountOption
{
    [Min(1)] public int lineCount = 1;

    [Tooltip("이 줄 수가 뽑힐 가중치입니다(확률 = 이 값 / 가중치 합). 0이면 뽑히지 않습니다.")]
    [Min(0f)] public float weight = 1f;
}
