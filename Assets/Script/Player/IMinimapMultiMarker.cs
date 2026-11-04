using System.Collections.Generic;

// IMinimapMarker(오브젝트 하나가 고정된 키 하나만 알림)로는 표현할 수 없는 경우를 위한 인터페이스입니다.
// 보물상자처럼 "지금 상태에 따라 미니맵에 띄울 아이콘이 달라지거나, 한 번에 여러 종류가 남아있을 수
// 있는" 오브젝트가 구현합니다. MinimapDisplay는 방 안에서 IMinimapMarker와 이 인터페이스를 모두
// 찾아 키를 모읍니다.
public interface IMinimapMultiMarker
{
    // 지금 이 오브젝트가 미니맵에 띄워달라고 요청하는 아이콘 키들을 outKeys에 추가합니다.
    // (outKeys는 호출 전에 이미 비워져 있으므로 Clear()하지 말고 Add만 하세요.)
    void CollectMinimapKeys(List<string> outKeys);
}

// MinimapDisplay의 Marker Icons 리스트에 등록된 markerKey와 정확히 일치해야 하는 키 이름들입니다.
public static class MinimapKeys
{
    public const string Item = "Item";      // 플레이어가 가져갈 수 있는 아이템(패시브/액티브/장신구/일회용/음식 공통)
    public const string Chest = "Chest";    // 아직 F로 열지 않아 내용물이 정해지지 않은 상자
    public const string Portal = "Portal";  // 다음 레벨로 가는 포탈 (방 정중앙에 별도로 표시됩니다)

    // 기본 아이템 종류 -> 키. 매 프레임 불려도 문자열을 새로 만들지 않도록 switch로 고정 문자열을 돌려줍니다.
    public static string ForBasicItem(BasicItemType type)
    {
        switch (type)
        {
            case BasicItemType.Health: return "Health";
            case BasicItemType.Grenade: return "Grenade";
            case BasicItemType.HackingTool: return "HackingTool";
            case BasicItemType.Credit: return "Credit";
            case BasicItemType.Pyroxenes: return "Pyroxenes";
            case BasicItemType.Cost: return "Cost";
            default: return type.ToString();
        }
    }
}
