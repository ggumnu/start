using System;
using System.Collections.Generic;
using UnityEngine;

// 16번 액티브 아이템 효과 - 사용하면 지금 레벨에 방을 하나 더 만들어 붙입니다.
// (코스트는 ItemDefinition의 activeCost로 지정합니다 - 16번은 7.)
//
// [어떤 방이 생기는가]
// 아래 roomOptions(방 카테고리 + 가중치 목록)에서 가중치에 따라 카테고리를 하나 뽑습니다.
// 목록에 항목(Element)을 추가하고 카테고리와 가중치만 채우면 됩니다(동아리방/편의점/특수방). 뽑기는
// 카테고리로만 굴리고, 뽑힌 카테고리의 variantId(세부 종류)는 LevelBuilder가 기존 방 생성과 같은
// 규칙(Level Config의 해당 variant 풀, 특수방은 Special Layouts)으로 정합니다.
//
// [특수방 (형빈 확정)]
// roomOptions에 Special을 넣으면 특수방도 나옵니다. 레이아웃은 Level Config의 Special Layouts에서 가중치로 뽑고(이 레벨에
// 이미 있는 레이아웃은 되도록 피함), 진입 조건도 그 레이아웃의 Entry Condition Pool에서 뽑습니다 - 레벨 생성 때 생기는
// 특수방과 똑같이 외형 세트/내용물/잠금 바리케이드/체력 피해가 적용됩니다. 단 특수방은 레벨 생성 때와 같은 규칙으로
// 시작방/일반방(Level Config의 Special Room Parent Categories)에만 붙습니다(동아리방/편의점에는 안 붙음).
//
// [뽑을 수 있는 방만 뽑습니다]
// 방 종류마다 붙일 자리가 다르므로(특수방은 시작방/일반방 옆에만, Special Layouts가 비어 있으면 불가), 사용하는 순간
// "지금 붙일 자리가 있는 종류"만 모아 그 안에서 가중치로 뽑습니다. 그래서 특수방 자리가 없으면 다른 종류가 나오고, 어느
// 종류도 붙일 수 없으면 사용할 수 없습니다(코스트 소모 없음).
//
// [어디에 생기는가 - LevelBuilder.TryAddExtraRoom 참고]
// 적이 없는 방(보스방 제외)의 빈 옆칸 중 다른 방과 맞닿지 않는 칸에 붙습니다. Level Config에 정해둔
// 방 개수/맵 크기 제한과는 무관합니다.
//
// [사용할 수 없는 경우]
// 붙일 자리가 없거나, n.5 선택방 레벨이거나, 뽑을 수 있는 카테고리가 하나도 없으면 사용할 수 없고
// 코스트도 소모하지 않습니다(93번처럼 코스트를 내기 전에 CanActivate로 미리 확인합니다).
//
// [IDelayedActiveItemEffect를 구현한 이유]
// 이 효과는 지연 소모(대기)가 없습니다. 다만 "코스트를 내기 전에 사용 가능 여부를 먼저 확인"하는
// 장치(CanActivate)를 PlayerActiveItem이 이 인터페이스로만 호출하기 때문에 구현만 해두었고,
// 대기 상태에 들어가지 않으므로 CancelPending은 빈 본문입니다.
//
// [아이템 에셋 설정]
// ItemDefinition: Item Id = 16, Category = Active, Active Cost = 7, Active Effect = SpawnExtraRoomEffect.
[Serializable]
public class SpawnExtraRoomEffect : IDelayedActiveItemEffect
{
    [Tooltip("사용할 때 뽑힐 방의 종류와 가중치 목록입니다. 새 방을 추가하려면 Element를 하나 늘리고 " +
             "Category와 Weight를 채우세요(확률 = 그 Element의 Weight / 전체 Weight 합). " +
             "동아리방/편의점/특수방만 쓸 수 있고, 시작방/보스방/일반방/선택방이나 " +
             "Weight가 0 이하인 Element는 무시됩니다.")]
    public List<ExtraRoomOption> roomOptions = new List<ExtraRoomOption>
    {
        new ExtraRoomOption { category = RoomCategory.Club, weight = 1f }
    };

    // 코스트를 내기 전에 호출됩니다. 뽑을 수 있는 방이 있고, 붙일 자리도 있을 때만 true입니다.
    public bool CanActivate(ActiveEffectContext context)
    {
        if (context == null || context.LevelBuilder == null) return false;
        return GetTotalWeight(context.LevelBuilder) > 0f;
    }

    public void Activate(ActiveEffectContext context)
    {
        if (context == null || context.LevelBuilder == null) return;

        if (!TryRollCategory(context.LevelBuilder, out RoomCategory category)) return;

        if (!context.LevelBuilder.TryAddExtraRoom(category))
        {
            Debug.LogWarning("Item16effect: 방을 추가하지 못했습니다(붙일 자리가 사라졌을 수 있습니다).");
        }
    }

    // 지연 소모가 없는 효과라 취소할 대기 상태가 없습니다.
    public void CancelPending(ActiveEffectContext context)
    {
    }

    // roomOptions에서 지금 쓸 수 있는(허용된 종류 + 가중치 > 0 + 지금 붙일 자리가 있는) Element의 가중치 합입니다.
    private float GetTotalWeight(LevelBuilder levelBuilder)
    {
        float total = 0f;
        if (roomOptions == null) return total;

        foreach (var option in roomOptions)
        {
            if (IsUsableNow(option, levelBuilder)) total += option.weight;
        }
        return total;
    }

    private static bool IsUsableNow(ExtraRoomOption option, LevelBuilder levelBuilder)
    {
        return IsUsable(option) && levelBuilder != null && levelBuilder.CanAddExtraRoom(option.category);
    }

    // 가중치에 따라 카테고리를 하나 뽑습니다(적/아이템 스폰과 같은 총 가중치 방식). 지금 붙일 자리가 있는 종류만 뽑습니다.
    private bool TryRollCategory(LevelBuilder levelBuilder, out RoomCategory category)
    {
        category = RoomCategory.Club;

        float total = GetTotalWeight(levelBuilder);
        if (total <= 0f) return false;

        float roll = UnityEngine.Random.value * total;
        float cumulative = 0f;
        RoomCategory lastUsable = RoomCategory.Club;
        foreach (var option in roomOptions)
        {
            if (!IsUsableNow(option, levelBuilder)) continue;

            lastUsable = option.category;
            cumulative += option.weight;
            if (roll <= cumulative)
            {
                category = option.category;
                return true;
            }
        }

        category = lastUsable; // 부동소수점 오차 방어
        return true;
    }

    // 방 추가에 쓸 수 있는 Element인지: 허용된 카테고리이고 가중치가 0보다 커야 합니다.
    private static bool IsUsable(ExtraRoomOption option)
    {
        if (option == null || option.weight <= 0f) return false;

        switch (option.category)
        {
            case RoomCategory.Club:
            case RoomCategory.Store:
            case RoomCategory.Special:
                return true;
            default:
                return false;
        }
    }
}

// 16번 효과의 방 종류 목록 Element 하나입니다(카테고리 + 가중치).
[Serializable]
public class ExtraRoomOption
{
    public RoomCategory category = RoomCategory.Club;

    [Tooltip("이 방이 뽑힐 가중치입니다(확률 = 이 값 / 전체 가중치 합).")]
    public float weight = 1f;
}
