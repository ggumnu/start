using System.Collections.Generic;
using UnityEngine;

// 특수방의 진입 조건 종류입니다. 종류는 이 4개로 고정입니다(형빈 확정) - 레이아웃 에셋에서는 이 중 하나를
// 드롭다운으로 고르기만 하고, 각 조건이 실제로 어떻게 동작하는지(잠금 바리케이드 이펙트, 체력 피해량 등)는
// 레이아웃마다 따로 적지 않고 공용 설정 한 곳에서 관리합니다(2단계 이후 추가 예정).
//
// [번호를 직접 적어둔 이유]
// Unity는 enum 값을 번호로 저장합니다. 나중에 순서를 바꾸거나 중간에 끼워 넣어도 기존 레이아웃 에셋의
// 값이 엉뚱한 조건으로 바뀌지 않도록 번호를 고정했습니다. 새 조건을 추가할 때는 4 이상을 쓰세요.
public enum SpecialRoomEntryCondition
{
    Free = 0,           // 그냥 진입 - 잠금 없음(부모 방 전투가 끝나면 일반 열린 벽처럼 드나듦)
    Grenade = 1,        // 수류탄 - 수류탄 폭발 범위에 잠금 바리케이드가 걸리면 해제
    HackingTool = 2,    // 해킹툴 - 해킹툴 범위에 잠금 바리케이드가 걸리면 해제
    Health = 3,         // 체력 소모 - 잠금 없음, 대신 드나들 때마다 피격(아이작 가시방)
}

// 특수방 레이아웃 하나(예: 크레딧 더미 방)의 설정 에셋입니다.
// Project 창에서 Create > MobArchive > Special Room Layout으로 만들고, Level Config의 특수방 목록
// (Special Layouts)에 가중치와 함께 등록하면 그 레벨의 특수방으로 뽑힐 수 있습니다.
//
// [레이아웃 ID]
// 맵 생성 시 이 값이 그 특수방의 variantId가 됩니다. 그래서 variantId로 연결되는 기존 기능들 -
// 미니맵 variant 전용 아이콘(Minimap Display > Variant Icons), 아이템 등장 위치(Appearance Contexts에서
// category = Special을 고르면 오른쪽 목록에 이 ID가 나옴) - 에 이 ID를 그대로 쓰면 됩니다.
// 영문 소문자 + 밑줄을 권장합니다(예: credit_pile).
//
// [진입 조건 - 고정 또는 랜덤]
// Entry Condition Pool에 후보(조건 + 가중치)를 넣으면 맵을 만들 때 특수방마다 하나를 뽑습니다(예: 크레딧 더미 = 그냥/
// 수류탄/해킹툴 중 랜덤). 비워 두면 Entry Condition 하나로 고정입니다. 뽑힌 조건은 그 방 데이터
// (RoomInstance.specialEntryCondition)에 저장되고, 잠금 바리케이드/체력 피해는 그 값을 따릅니다.
//
// [외형 - Visual Set]
// 이 방의 바닥/벽/부모 방 쪽 입구 벽은 Room Visual Set 에셋(Create > MobArchive > Room Visual Set)으로 따로 만들어
// 아래 Visual Set 칸에 연결합니다(형빈 확정). 비워 둔 칸은 예전 방식(LevelBuilder > Room Wall Sets의 Variant Id =
// 이 레이아웃 ID → Special 카테고리 세트 → 일반방 기본)으로 폴백하므로, 외형 세트가 없어도 방은 지어집니다.
//
// [내용물]
// 크레딧 더미 같은 방 내용물은 Visual Set의 바닥 프리팹 안에 직접 배치합니다(상점 바닥 프리팹의 ShopNPC와 같은 방식).
// 아래 "플레이어 아이템 받침대"는 코드가 방 가운데에 놓는 공용 내용물이라 여기서 켜고 끕니다.
//
// [이 레이아웃에서 나오는 아이템 - 아이템 쪽에서 등록(형빈 확정 A안)]
// 아이템 에셋의 Appearance Contexts에 아래 둘 중 하나가 있으면 이 레이아웃의 후보가 됩니다.
// - category = Special + 이 레이아웃 ID  → 이 레이아웃 전용 아이템
// - category = Special + (카테고리 전체) → 특수방 공용 아이템. 단 includeSharedSpecialItems가 켜진 레이아웃에서만
// 실제 후보 목록은 이 에셋 Inspector 맨 아래 "아이템 미리보기"에서 바로 확인할 수 있습니다(SpecialRoomLayoutEditor).
[CreateAssetMenu(fileName = "SpecialRoomLayout", menuName = "MobArchive/Special Room Layout")]
public class SpecialRoomLayout : ScriptableObject
{
    [Header("식별")]
    [Tooltip("이 레이아웃을 구분하는 고유 이름입니다(예: credit_pile). 맵 생성 시 이 특수방의 variantId가 되어 " +
             "미니맵 variant 아이콘/아이템 등장 위치/방 전용 벽 세트와 연결됩니다. 다른 레이아웃과 겹치면 안 됩니다.")]
    public string layoutId;

    [Tooltip("사람이 알아보기 위한 이름입니다(예: 크레딧 더미). 로직에는 쓰이지 않습니다.")]
    public string displayName;

    [Header("진입 조건")]
    [Tooltip("이 특수방에 들어가는 방법입니다. 아래 Entry Condition Pool이 비어 있을 때만 쓰입니다(고정 조건).\n" +
             "- Free: 잠금 없음\n" +
             "- Grenade: 수류탄 폭발로 잠금 바리케이드를 파괴해야 함\n" +
             "- Hacking Tool: 해킹툴 범위로 잠금 바리케이드를 파괴해야 함\n" +
             "- Health: 잠금 없음, 드나들 때마다 체력 피해(피격)")]
    public SpecialRoomEntryCondition entryCondition = SpecialRoomEntryCondition.Free;

    [Tooltip("진입 조건을 랜덤으로 정하고 싶을 때 후보를 넣습니다(조건 + 가중치). 맵을 만들 때 이 특수방마다 한 번 " +
             "뽑습니다(확률 = 그 줄의 가중치 / 가중치 합). 비어 있거나 가중치가 전부 0이면 위 Entry Condition을 그대로 씁니다. " +
             "실제 확률은 Inspector 아래 '진입 조건 확률'에서 확인할 수 있습니다.")]
    public List<WeightedEntryCondition> entryConditionPool = new List<WeightedEntryCondition>();

    [Header("외형")]
    [Tooltip("이 레이아웃의 바닥(방 내용물 포함)/벽/부모 방 쪽 입구 벽 세트입니다(Create > MobArchive > Room Visual Set). " +
             "비워 두거나 세트 안의 칸을 비워 두면 그 칸만 LevelBuilder의 Room Wall Sets / Neighbor Facing Wall Sets / " +
             "일반방 기본으로 폴백합니다.")]
    public RoomVisualSet visualSet;

    [Header("등장 아이템 규칙")]
    [Tooltip("켜면 이 레이아웃 전용 아이템(Special + 이 레이아웃 ID)에 더해, 특수방 공용 아이템(Special + 카테고리 전체)도 " +
             "후보가 됩니다. 끄면 이 레이아웃 전용 아이템만 나옵니다(예: 신비방처럼 전용 아이템만 나와야 하는 레이아웃).")]
    public bool includeSharedSpecialItems = true;

    [Header("내용물")]
    [Tooltip("켜면 방 가운데에 플레이어 아이템 받침대(동아리방과 같은 보물상자 - LevelBuilder의 Item Pedestal Prefab)를 " +
             "하나 놓습니다. 후보는 위 '등장 아이템 규칙'으로 정해지고, 후보가 없거나 전부 보유 중이면 동아리방과 똑같이 " +
             "기본 아이템으로 대체됩니다. 단 동아리방 전용 효과(27번 음식의 '다음 동아리방 상자 최상급' 등)는 적용되지 않습니다.")]
    public bool spawnItemPedestal = false;

    [Header("9번 음식(상자 복제)")]
    [Tooltip("켜면 이 특수방에서 9번 음식(방의 보물상자 개수 두 배)을 쓸 수 있습니다. 상자는 같은 종류로만 복제됩니다 - " +
             "바닥 프리팹의 크레딧 상자(PresetBasicItemChest)는 같은 설정의 크레딧 상자로, 플레이어 아이템 받침대는 이 레이아웃 " +
             "규칙의 받침대로 복제됩니다. 끄면(기본) 이 방에서는 9번을 쓸 수 없습니다(소모되지 않음).")]
    public bool allowChestDuplication = false;

    // 맵 생성 시 LevelGraphGenerator가 특수방마다 한 번 호출합니다. rng는 맵 생성기의 난수를 그대로 받아 씁니다.
    public SpecialRoomEntryCondition PickEntryCondition(System.Random rng)
    {
        float total = GetEntryConditionPoolTotalWeight();
        if (total <= 0f || rng == null) return entryCondition;

        float roll = (float)(rng.NextDouble() * total);
        float cumulative = 0f;
        WeightedEntryCondition last = null;
        foreach (var option in entryConditionPool)
        {
            if (option == null || option.weight <= 0f) continue;
            cumulative += option.weight;
            last = option;
            if (roll <= cumulative) return option.condition;
        }
        return last != null ? last.condition : entryCondition;
    }

    // 후보 목록에서 쓸 수 있는 줄(가중치 > 0)의 가중치 합입니다. 0이면 고정 조건(entryCondition)을 씁니다.
    public float GetEntryConditionPoolTotalWeight()
    {
        float total = 0f;
        if (entryConditionPool == null) return total;
        foreach (var option in entryConditionPool)
        {
            if (option != null && option.weight > 0f) total += option.weight;
        }
        return total;
    }

    void OnValidate()
    {
        if (layoutId != null) layoutId = layoutId.Trim();
    }
}

// 진입 조건 후보 한 줄입니다(조건 + 가중치).
[System.Serializable]
public class WeightedEntryCondition
{
    public SpecialRoomEntryCondition condition = SpecialRoomEntryCondition.Free;

    [Tooltip("이 조건이 뽑힐 가중치입니다(확률 = 이 값 / 후보 전체 가중치 합). 0이면 뽑히지 않습니다.")]
    [Min(0f)] public float weight = 1f;
}

// Level Config의 특수방 목록 한 줄입니다(레이아웃 에셋 + 그 레벨에서의 가중치).
// 같은 레이아웃이라도 레벨마다 가중치를 다르게 줄 수 있습니다.
[System.Serializable]
public class SpecialLayoutEntry
{
    [Tooltip("Create > MobArchive > Special Room Layout으로 만든 레이아웃 에셋을 넣으세요.")]
    public SpecialRoomLayout layout;

    [Tooltip("이 레벨에서 이 레이아웃이 뽑힐 가중치입니다(확률 = 이 값 / 목록 전체 가중치 합). 0이면 뽑히지 않습니다.")]
    [Min(0f)] public float weight = 1f;
}
