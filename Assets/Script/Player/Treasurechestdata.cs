using UnityEngine;

// 일반맵 보물상자 관련 데이터 모델을 모아둔 파일입니다. RoomData.cs가 방 관련 데이터 모델을
// 한 파일에 모아두는 것과 동일한 패턴입니다.

// 상자에 들어갈 수 있는 "기본 아이템" 여섯 종류입니다. 패시브 아이템(ItemDefinition, 동아리방
// 계열)과 달리 종류가 확장되지 않는 고정 목록이라 별도 에셋 풀이 아니라 enum으로 관리합니다.
// 새 기본 아이템이 추가되면 여기에 값을 하나 더 추가하고, PlayerResources.cs/TreasureChest.cs/
// TreasureChestInfoDisplay.cs의 switch 문에도 분기를 추가해주세요.
//
// [Cost 추가 - v1.23]
// 코스트(액티브 아이템 소모 자원)도 이 목록에 포함시켰습니다 - 수류탄/해킹툴/크레딧/청휘석과
// 동일하게 보물상자에서 나올 수 있고, PlayerResources가 동일한 Dictionary<BasicItemType, int>
// 카운터로 관리합니다. 다만 Cost만 최대치(PlayerResources.maxCost)가 있고, 전투방(일반방/
// 보스방) 클리어 시에도 추가로 획득할 수 있다는 점이 다릅니다 - PlayerResources.cs 참고.
public enum BasicItemType
{
    Health,
    Grenade,
    HackingTool,
    Credit,
    Pyroxenes,
    Cost
}

// 인스펙터에서 형빈이 직접 구성하는 "묶음" 하나입니다. 예: 수류탄 1개짜리 묶음(가중치 1)과
// 수류탄 3개짜리 묶음(가중치 0.3)을 각각 따로 등록하면, 같은 종류라도 개수별로 다른 확률을
// 줄 수 있습니다. NormalRoomTreasureSpawner.basicItemBundlePool에서 이 클래스의 리스트를
// 그대로 사용합니다.
[System.Serializable]
public class BasicItemBundle
{
    public BasicItemType type;
    [Min(1)] public int count = 1;
    [Tooltip("같은 풀 안에서 뽑힐 가중치입니다. 값이 클수록 더 자주 등장합니다.")]
    public float weight = 1f;
}

// 상자 하나에 실제로 확정된 기본 아이템 묶음 하나의 런타임 상태입니다 (인스펙터에 노출되는
// 데이터가 아니라, 스폰된 상자 인스턴스마다 따로 갖는 결과값입니다).
//
// [consumed 플래그가 필요한 이유]
// 현재 체력 묶음은 플레이어 체력이 꽉 차 있으면 그 항목만 건너뛰고 나머지는 정상 습득됩니다
// (TreasureChest.cs 참고). 그래서 "상자 전체를 습득했는지"가 아니라 "이 묶음 하나를
// 습득했는지"를 개별적으로 추적해야 합니다.
public class ResolvedBasicItemStack
{
    public BasicItemType type;
    public int count;
    public bool consumed;
}
