using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

// 패시브 아이템(PlayerInventory)과 별개로 관리되는 "소모성 자원" 보유량입니다.
// 수류탄/해킹툴/크레딧/청휘석/코스트 다섯 가지를 정수 카운터로 관리합니다. 현재 체력은 여기
// 포함하지 않습니다 - 획득 즉시 소모되는 성격(PlayerHealth.Heal())이라 "보유"라는 개념 자체가
// 없기 때문입니다.
//
// [PlayerInventory와 분리한 이유]
// PlayerInventory는 itemId 기준으로 "한 번만 얻을 수 있는지"를 판정하는 구조(ownedItemIds)라,
// 여러 번 계속 얻고(보물상자, 나중의 상점 등) 소모될 수 있는(수류탄 투척, 크레딧 소비 등) 이
// 자원들과는 데이터 모델 자체가 다릅니다. PlayerHealth(실시간 상태)와 PlayerStats(능력치)가
// 분리되어 있는 것과 같은 이유로, 이 자원들도 별도 컴포넌트로 분리했습니다.
//
// [수류탄/해킹툴 자체 스탯(데미지/범위/투척속도 등)에 대해]
// 이 채팅에서는 "보유 개수"만 다룹니다. 실제 수류탄/해킹툴 공격을 구현할 때 그 자체 스탯을
// 이 컴포넌트에 추가할지, 공격 스크립트 쪽에 별도로 둘지는 그 작업을 시작할 때 다시 논의합니다.
//
// [시작 시 보유량]
// 아이작 리펜던스의 시작 폭탄/열쇠처럼, 게임 시작 시 소량의 자원을 기본으로 지급하고 싶을 때
// 아래 starting* 필드를 인스펙터에서 원하는 값으로 채우세요. 기본값 0이면 기존과 동일하게
// 빈 상태로 시작합니다. Health는 이 컴포넌트가 관리하지 않으므로 시작 지급 대상에 포함하지
// 않습니다.
//
// [코스트(Cost) - 액티브 아이템용 소모성 재화, v1.23 추가]
// 코스트는 수류탄/해킹툴/크레딧/청휘석과 데이터 모델이 완전히 동일합니다 - 같은
// Dictionary<BasicItemType, int>로 관리되고, 보물상자에서도 다른 4종과 똑같이 나올 수
// 있습니다. 다른 점은 두 가지뿐입니다.
//   1. 최대치(maxCost, 기본 10)가 있습니다. Add()에서 Cost 타입일 때만 결과를 이 값으로
//      clamp합니다 - 다른 4종은 여전히 상한이 없습니다.
//   2. 보물상자 획득 외에, 전투방(일반방/보스방) 클리어 시에도 자동으로 얻습니다. 획득량은
//      그 순간의 PlayerStats.CostRecovery를 정수로 반올림한 값입니다(MagazineSize/
//      Multishot과 동일한 반올림 관례). RoomController.OnAnyRoomCleared 이벤트를
//      Awake()에서 구독해서 처리합니다(LevelPortal.OnAnyPortalInteractionRequested와
//      동일한 "정적 이벤트로 전역 브로드캐스트" 패턴).
// 액티브 아이템은 아직 없어서 "코스트를 소모해서 액티브 아이템을 발휘"하는 로직은 이
// 컴포넌트 바깥(액티브 아이템 스크립트)에서 기존 TrySpend(BasicItemType.Cost, 필요량)을
// 그대로 호출하는 방식으로 나중에 붙이면 됩니다 - 이 컴포넌트는 그 소모를 받아줄 준비만
// 되어 있으면 충분해서 TrySpend()는 손대지 않았습니다.
//
// [임시 디버그 로그 2곳 제거 - v1.31]
// Awake()의 "Awake 실행됨" 로그와 Add()의 코스트 획득 로그는 코스트 UI(PlayerCostDisplay)가
// 이미 완성되어 콘솔로 확인할 필요가 없어져 형빈 확인 후 제거했습니다. 데이터 로직 자체는
// 전혀 바뀌지 않았습니다.
public class PlayerResources : MonoBehaviour
{
    [Header("시작 시 보유량 (게임 시작 시 자동 지급 - 기본값 0)")]
    [Min(0)] public int startingGrenade = 0;
    [Min(0)] public int startingHackingTool = 0;
    [Min(0)] public int startingCredit = 0;
    [FormerlySerializedAs("startingChungwiStone")]
    [Min(0)] public int startingPyroxenes = 0;
    [Min(0)] public int startingCost = 0;

    [Header("코스트 상한선")]
    [Tooltip("코스트는 이 값을 넘어설 수 없습니다 (기본 10 = 아이작 리펜던스 액티브 아이템 " +
             "게이지와 동일). 방 클리어/보물상자로 얻는 양이 이 값을 넘으면 초과분은 그냥 " +
             "버려집니다.")]
    [Min(1)] public int maxCost = 10;

    [Header("참조 (비워두면 자동으로 찾습니다)")]
    [Tooltip("방 클리어 시 얻는 코스트량(PlayerStats.CostRecovery)을 읽어오기 위해 필요합니다.")]
    public PlayerStats playerStats;

    private readonly Dictionary<BasicItemType, int> amounts = new Dictionary<BasicItemType, int>();

    // 자원이 늘어나거나 줄어들 때마다 (종류, 변경 후 총량)을 흘려보냅니다. 나중에 수류탄/
    // 해킹툴/크레딧/청휘석/코스트 보유량을 보여주는 HUD가 생기면, 값이 바뀔 때만 갱신하도록
    // 이 이벤트를 구독하면 됩니다 (PlayerStats.OnMaxHealthChanged와 동일한 패턴).
    public event Action<BasicItemType, int> OnAmountChanged;

    void Awake()
    {
        ApplyStartingAmounts();
        RoomController.OnAnyRoomCleared += HandleRoomCleared;
    }

    void OnDestroy()
    {
        RoomController.OnAnyRoomCleared -= HandleRoomCleared;
    }

    // 인스펙터에 설정된 시작 지급량을 기존 Add()를 통해 그대로 적용합니다. Add()가 이미
    // "0이면 아무 일도 하지 않음", "Health는 거부", "Cost는 maxCost로 clamp" 같은 검증을
    // 갖고 있으므로 그대로 재사용합니다.
    private void ApplyStartingAmounts()
    {
        Add(BasicItemType.Grenade, startingGrenade);
        Add(BasicItemType.HackingTool, startingHackingTool);
        Add(BasicItemType.Credit, startingCredit);
        Add(BasicItemType.Pyroxenes, startingPyroxenes);
        Add(BasicItemType.Cost, startingCost);
    }

    // 전투방(일반방/보스방) 클리어 시 RoomController가 흘려보내는 이벤트를 받아, 그 순간의
    // PlayerStats.CostRecovery를 정수로 반올림한 만큼 코스트를 지급합니다. room 인자는
    // 지금은 쓰지 않지만, 나중에 방 종류별로 획득량을 다르게 주고 싶어지면 바로 활용할 수
    // 있도록 시그니처에 남겨뒀습니다.
    private void HandleRoomCleared(RoomInstance room)
    {
        if (playerStats == null)
        {
            playerStats = UnityEngine.Object.FindFirstObjectByType<PlayerStats>();
        }
        if (playerStats == null)
        {
            Debug.LogWarning("PlayerResources: PlayerStats를 찾지 못해 방 클리어 코스트 지급을 건너뜁니다.");
            return;
        }

        int amount = Mathf.RoundToInt(playerStats.CostRecovery);
        if (amount != 0)
        {
            Add(BasicItemType.Cost, amount);
        }
    }

    public int GetAmount(BasicItemType type)
    {
        return amounts.TryGetValue(type, out int value) ? value : 0;
    }

    // 보물상자 등에서 자원을 얻을 때 호출합니다. Health는 이 컴포넌트가 관리하지 않으므로
    // 호출하지 마세요 - 대신 PlayerHealth.Heal()을 사용하세요 (TreasureChest.cs 참고).
    // Cost는 maxCost를 넘지 않도록 여기서 자동으로 clamp됩니다.
    public void Add(BasicItemType type, int amount)
    {
        if (type == BasicItemType.Health)
        {
            Debug.LogWarning("PlayerResources: Health는 이 컴포넌트에서 관리하지 않습니다. PlayerHealth.Heal()을 사용하세요.");
            return;
        }
        if (amount == 0) return;

        int newAmount = GetAmount(type) + amount;

        if (type == BasicItemType.Cost)
        {
            newAmount = Mathf.Clamp(newAmount, 0, maxCost);
        }

        amounts[type] = newAmount;
        OnAmountChanged?.Invoke(type, newAmount);
    }

    // 상점(미구현)이나 수류탄/해킹툴 투척(미구현), 액티브 아이템 발휘(미구현) 등에서 자원을
    // 소모할 때 쓸 수 있도록 미리 열어둔 훅입니다. 보유량이 부족하면 아무것도 바꾸지 않고
    // false를 반환합니다.
    public bool TrySpend(BasicItemType type, int amount)
    {
        if (type == BasicItemType.Health || amount <= 0) return false;

        int current = GetAmount(type);
        if (current < amount) return false;

        int newAmount = current - amount;
        amounts[type] = newAmount;
        OnAmountChanged?.Invoke(type, newAmount);
        return true;
    }
}
