using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// [아이템 도감 기록 (형빈 확정)]
// "한 번이라도 인벤토리에 들어온 적이 있는 아이템"의 itemId를 PC에 저장합니다. 게임을 껐다 켜도 남고, 판이 끝나도 남습니다.
// 시작 화면의 아이템 도감(ItemCodexUI)이 이 기록을 읽어서 얻은 아이템은 원래 색, 안 얻은 아이템은 반투명으로 보여줍니다.
//
// [언제 기록되나 - 어떤 경로든 인벤토리에 들어오면]
// 아이템이 인벤토리/슬롯에 실제로 들어오는 다섯 곳에서 MarkAcquired를 부릅니다. 보물상자/받침대/상점/56번/48번/80번 등 어떤
// 경로로 얻었든 결국 이 다섯 곳을 지나므로 전부 기록됩니다.
// - 패시브: PlayerInventory.TryAcquireItem, PlayerInventory.ReplaceItems(48번 교체)
// - 액티브: PlayerActiveItem.TryAcquireItem / 장신구: PlayerTrinket.TryAcquireItem
// - 일회용: PlayerDisposableItem.TryAcquireItem / 음식: PlayerFoodItem.TryAcquireItem
//
// [테스트 씬은 기록하지 않음]
// 아이템 테스트 씬(씬 안에 ItemTestArenaBuilder가 있는 씬 - 꺼져 있어도 해당)에서 얻은 아이템은 기록하지 않습니다.
//
// [저장 위치]
// Application.persistentDataPath/item_codex.json (Windows: C:\Users\<사용자>\AppData\LocalLow\<회사 이름>\<제품 이름>\item_codex.json).
// 처음 얻는 순간 바로 저장하므로 게임이 강제로 꺼져도 기록이 남습니다. 저장 실패는 경고만 남기고 게임 진행을 막지 않습니다.
//
// [초기화 (개발용)]
// - 유니티 위쪽 메뉴 MobArchive > 아이템 도감 기록 초기화
// - 또는 ItemCodexSettings 에셋 인스펙터 오른쪽 위 ⋮ 버튼 > "아이템 도감 기록 초기화"
// Play 중이든 아니든 됩니다. 도감이 열려 있으면 바로 반영됩니다.
public static class ItemCodexRecord
{
    private const string FileName = "item_codex.json";

    [Serializable]
    private class SaveData
    {
        public List<string> acquiredItemIds = new List<string>();
    }

    private static HashSet<string> acquiredIds;

    // 기록이 바뀌었을 때(새로 얻음/초기화) 알립니다. 도감이 떠 있으면 다시 그립니다.
    public static event Action OnChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        acquiredIds = null; // 다음에 필요할 때 파일에서 다시 읽음
        OnChanged = null;
    }

    private static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

    // 지금까지 얻은 아이템 종류 수
    public static int Count
    {
        get { EnsureLoaded(); return acquiredIds.Count; }
    }

    // 이 아이템을 한 번이라도 얻은 적이 있는지
    public static bool HasAcquired(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return false;
        EnsureLoaded();
        return acquiredIds.Contains(itemId);
    }

    public static bool HasAcquired(ItemDefinition item)
    {
        return item != null && HasAcquired(item.itemId);
    }

    // [플레이 통계] 지금까지 도감에 기록된 아이템 ID 전체의 사본입니다(정렬됨). 도감 수집율 통계용입니다.
    public static List<string> GetAcquiredIdsSnapshot()
    {
        EnsureLoaded();
        var list = new List<string>(acquiredIds);
        list.Sort(StringComparer.Ordinal);
        return list;
    }

    // 아이템이 인벤토리/슬롯에 들어온 순간 부릅니다. 처음 얻은 아이템이면 기록하고 바로 저장합니다.
    // 무슨 일이 있어도 예외를 밖으로 내보내지 않습니다(아이템 획득 처리를 절대 막지 않도록).
    public static void MarkAcquired(ItemDefinition item)
    {
        try
        {
            if (item == null || string.IsNullOrEmpty(item.itemId)) return;
            // [플레이 통계] 이번 판에서 얻은 아이템으로 기록합니다(도감에 이미 있는 아이템도 판마다 따로 셈). 통계 쪽은 절대 예외를 내지 않습니다.
            RunAnalytics.NoteItemAcquired(item);
            if (IsTestScene()) return;

            EnsureLoaded();
            if (!acquiredIds.Add(item.itemId)) return; // 이미 기록된 아이템

            Save();
            OnChanged?.Invoke();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"ItemCodexRecord: 도감 기록 중 문제가 생겼습니다(게임 진행에는 영향 없음). {e.Message}");
        }
    }

    // 기록을 전부 지웁니다(개발용).
    public static void ResetAll()
    {
        acquiredIds = new HashSet<string>();
        try
        {
            if (File.Exists(FilePath)) File.Delete(FilePath);
            if (File.Exists(FilePath + ".tmp")) File.Delete(FilePath + ".tmp");
            Debug.Log("ItemCodexRecord: 아이템 도감 기록을 초기화했습니다.");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"ItemCodexRecord: 도감 기록 파일을 지우지 못했습니다. {e.Message}");
        }
        OnChanged?.Invoke();
    }

#if UNITY_EDITOR
    [UnityEditor.MenuItem("MobArchive/아이템 도감 기록 초기화")]
    private static void ResetFromMenu()
    {
        if (!UnityEditor.EditorUtility.DisplayDialog("아이템 도감 기록 초기화",
                "지금까지 얻은 아이템 기록을 전부 지웁니다. 되돌릴 수 없습니다.", "초기화", "취소"))
            return;
        ResetAll();
    }
#endif

    // ---------------- 내부 ----------------

    // 아이템 테스트 씬인지(씬 안에 ItemTestArenaBuilder가 있으면 - 꺼져 있어도 해당)
    private static bool IsTestScene()
    {
        return UnityEngine.Object.FindFirstObjectByType<ItemTestArenaBuilder>(FindObjectsInactive.Include) != null;
    }

    private static void EnsureLoaded()
    {
        if (acquiredIds != null) return;
        acquiredIds = new HashSet<string>();

        try
        {
            // 저장 도중 꺼져서 임시 파일만 남은 경우 그것을 읽습니다.
            string path = File.Exists(FilePath) ? FilePath : FilePath + ".tmp";
            if (!File.Exists(path)) return;
            var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
            if (data == null || data.acquiredItemIds == null) return;
            foreach (var id in data.acquiredItemIds)
            {
                if (!string.IsNullOrEmpty(id)) acquiredIds.Add(id);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"ItemCodexRecord: 도감 기록 파일을 읽지 못해 빈 기록으로 시작합니다. {e.Message}");
        }
    }

    // 임시 파일에 먼저 쓰고 바꿔치기해서, 저장 도중 꺼져도 원래 파일이 깨지지 않게 합니다.
    private static void Save()
    {
        try
        {
            var data = new SaveData();
            data.acquiredItemIds.AddRange(acquiredIds);
            data.acquiredItemIds.Sort(StringComparer.Ordinal);

            string path = FilePath;
            string temp = path + ".tmp";
            File.WriteAllText(temp, JsonUtility.ToJson(data, true));
            if (File.Exists(path)) File.Replace(temp, path, null);
            else File.Move(temp, path);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"ItemCodexRecord: 도감 기록을 저장하지 못했습니다. {e.Message}");
        }
    }
}
