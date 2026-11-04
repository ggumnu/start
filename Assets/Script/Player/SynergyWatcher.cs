using System;
using System.Collections.Generic;
using UnityEngine;

// [시너지 아이템 - 102/109번 때 추가, 형빈 확정]
// 혼자서는 효과가 없고, 정해진 아이템들을 "전부" 가지고 있을 때만 효과가 켜지는 아이템을 위한 감시 도구입니다.
// 효과 클래스가 SynergyWatcher를 하나 만들어 Start(인벤토리, 필요한 아이템 ID 목록, 켜짐/꺼짐 알림)를 부르면, 패시브를 얻거나 잃을 때마다
// (PlayerInventory.OnPassiveItemsChanged - 48번 일괄 교체도 교체가 끝난 직후 한 번) 조건을 다시 확인해서 바뀌었을 때만 알려줍니다.
// 효과를 잃으면(IRemovablePassiveEffect.Remove) Stop()을 불러 구독을 풀고 꺼짐을 알립니다.
//
// 앞으로 시너지 아이템이 더 생기면 필요한 아이템 ID 목록만 바꿔서 그대로 쓰면 됩니다.
public class SynergyWatcher
{
    private PlayerInventory inventory;
    private readonly List<string> requiredIds = new List<string>();
    private Action<bool> onChanged;
    private bool isActive;

    public bool IsActive => isActive;

    // 감시를 시작합니다. 이미 시작돼 있으면 정리한 뒤 다시 시작합니다. 지금 조건도 바로 한 번 확인합니다.
    public void Start(PlayerInventory targetInventory, IEnumerable<string> itemIds, Action<bool> changed)
    {
        Stop(notify: false);

        inventory = targetInventory;
        onChanged = changed;
        requiredIds.Clear();
        if (itemIds != null)
        {
            foreach (var id in itemIds)
            {
                if (!string.IsNullOrEmpty(id)) requiredIds.Add(id.Trim());
            }
        }

        if (inventory == null) return;
        inventory.OnPassiveItemsChanged += Evaluate;
        Evaluate();
    }

    // 감시를 멈춥니다. 켜져 있었다면(notify가 true일 때) 꺼짐을 알립니다.
    public void Stop(bool notify = true)
    {
        if (inventory != null) inventory.OnPassiveItemsChanged -= Evaluate;
        inventory = null;

        bool wasActive = isActive;
        isActive = false;
        if (notify && wasActive) onChanged?.Invoke(false);
    }

    private void Evaluate()
    {
        // 플레이어가 사라졌으면(씬 재시작 등) 꺼진 것으로 봅니다.
        bool now = inventory != null && requiredIds.Count > 0;
        if (now)
        {
            foreach (var id in requiredIds)
            {
                if (!inventory.HasItem(id)) { now = false; break; }
            }
        }

        if (now == isActive) return;
        isActive = now;
        try
        {
            onChanged?.Invoke(isActive);
        }
        catch (Exception e)
        {
            Debug.LogError($"SynergyWatcher: 시너지 효과 처리 중 오류가 발생했습니다: {e}");
        }
    }
}
