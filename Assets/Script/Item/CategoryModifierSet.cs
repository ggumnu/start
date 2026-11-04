using System.Collections.Generic;
using UnityEngine;

// [아이템 분류별 수치 보정 묶음 - 공용]
// "음식의 등장 가중치 +50", "음식 가격 25% 할인"처럼 아이템 분류(패시브/장신구/일회용/음식 등) 단위로 어떤 수치를 보정하는
// 효과들을 모아 두는 공용 묶음입니다. ItemWeightModifier(등장 가중치)와 ShopPriceModifier(상점 가격)가 이것을 하나씩 씁니다.
//
// - 효과마다 출처 이름(보통 아이템 ID)으로 등록합니다. 같은 출처로 다시 등록하면 덮어씁니다(중복으로 쌓이지 않음).
// - 계산 규칙은 스탯과 같습니다: (기본값 + 더하기 합계) x 곱하기 전부 곱한 값.
//   예) 기본 60, 더하기 +50과 +20, 곱하기 x1.5 -> (60 + 70) x 1.5 = 195
// - 소유자(보통 플레이어 오브젝트)가 사라지면 그 등록은 자동으로 정리됩니다(새 게임에서 플레이어가 새로 만들어질 때).
public class CategoryModifierSet
{
    private struct Entry
    {
        public ItemDefinition.ItemCategory category;
        public float additive;
        public float multiplier;
        public Object owner;
    }

    private readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>();

    // additive = 더할 값(없으면 0), multiplier = 곱할 값(없으면 1).
    public void Register(string sourceId, ItemDefinition.ItemCategory category, float additive, float multiplier, Object owner)
    {
        if (string.IsNullOrEmpty(sourceId)) return;
        entries[sourceId] = new Entry { category = category, additive = additive, multiplier = multiplier, owner = owner };
    }

    public void Unregister(string sourceId)
    {
        if (string.IsNullOrEmpty(sourceId)) return;
        entries.Remove(sourceId);
    }

    public void Clear()
    {
        entries.Clear();
    }

    // 이 분류에 걸린 보정이 하나라도 있는지
    public bool HasAny(ItemDefinition.ItemCategory category)
    {
        PruneDead();
        foreach (var entry in entries.Values)
        {
            if (entry.category == category) return true;
        }
        return false;
    }

    // (기본값 + 더하기 합계) x 곱하기 전부 곱한 값. 걸린 보정이 없으면 기본값 그대로입니다.
    public float Apply(ItemDefinition.ItemCategory category, float baseValue)
    {
        PruneDead();
        float additiveSum = 0f;
        float multiplierProduct = 1f;
        foreach (var entry in entries.Values)
        {
            if (entry.category != category) continue;
            additiveSum += entry.additive;
            multiplierProduct *= entry.multiplier;
        }
        return (baseValue + additiveSum) * multiplierProduct;
    }

    private void PruneDead()
    {
        List<string> deadKeys = null;
        foreach (var kvp in entries)
        {
            if (kvp.Value.owner != null) continue;
            if (deadKeys == null) deadKeys = new List<string>();
            deadKeys.Add(kvp.Key);
        }
        if (deadKeys == null) return;
        foreach (var key in deadKeys) entries.Remove(key);
    }
}
