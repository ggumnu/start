using UnityEngine;

// 아이템 하나가 등장할 수 있는 방 하나를 표현합니다. ItemDefinition.appearanceContexts에서
// 사용됩니다. category는 드롭다운(기존 RoomCategory enum 재사용)이라 오타가 날 수 없고,
// variantId도 AppearanceContextDrawer(에디터 전용 커스텀 드로어)를 통해 드롭다운으로
// 표시됩니다 - 프로젝트의 모든 RoomGenerationConfig/ChoiceLevelConfig 에셋(특수방은 SpecialRoomLayout
// 에셋)을 스캔해서 지금 선택된 category에 실제로 등록된 variantId만 자동으로 보여주므로, 여기서도 직접
// 타이핑할 일이 없습니다. 특정 variantId를 고르면(예: 카페='cafe', 편의점='convenience_store') 그 방에서만
// 등장합니다.
//
// [비워둘 때("카테고리 전체")의 실제 의미 - 주의]
// - 특수방(Special): ItemDatabase.GetItemsForRoom이 "카테고리 전체"도 함께 확인하므로, 모든 특수방 레이아웃
//   (공용 아이템을 허용한 레이아웃)에서 등장합니다.
// - 그 외 variantId가 있는 방(편의점/카페/선택방): 기존 방식(ItemDatabase.GetItemsForContext - 키 하나만
//   비교)을 그대로 쓰므로, 지금은 "카테고리 전체"가 그 방들과 매칭되지 않습니다. 그 방들은 지금처럼
//   variantId를 직접 고르세요. 상점은 Shop Config의 등장 위치와 같은 값끼리만 매칭됩니다.
// - variantId가 없는 방(동아리방/일반방/보스방): 카테고리 이름 그대로 매칭됩니다(기존과 동일).
[System.Serializable]
public class AppearanceContext
{
    public RoomCategory category;

    [Tooltip("특정 variant(특수방이면 레이아웃 ID)에서만 등장시키려면 오른쪽 목록에서 고르세요. 비워두면(카테고리 전체) " +
             "variantId가 없는 방(동아리방/일반방/보스방)과 특수방 공용 아이템으로 쓰입니다 - 편의점/카페/선택방은 지금은 " +
             "variantId를 직접 골라야 합니다(AppearanceContext.cs 상단 주석 참고).")]
    public string variantId;

    // ItemDatabase.GetContextKey(RoomInstance)와 동일한 규칙입니다: variantId가 있으면
    // 그 문자열, 없으면 category 이름을 컨텍스트 키로 사용합니다.
    public string ToContextKey()
    {
        return string.IsNullOrEmpty(variantId) ? category.ToString() : variantId;
    }
}