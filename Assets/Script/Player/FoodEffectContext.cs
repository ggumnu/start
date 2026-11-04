using System;
using UnityEngine;

// 음식 효과(IFoodEffect)가 필요로 할 수 있는 참조들을 한데 모아 담는 컨테이너입니다.
// DisposableEffectContext와 완전히 동일한 구성입니다 - 음식은 일회용과 사용 방식이 같아서
// 필요한 참조도 동일합니다(스탯/체력/재화/레벨당 1회 지급 게이트/레벨 구조/지연형 완료 콜백).
//
// 나중에 또 다른 참조가 필요해지면, 이 클래스에 프로퍼티 하나만 추가하면 됩니다 -
// IFoodEffect 인터페이스나 기존 효과 구현체들은 전혀 손댈 필요가 없습니다.
//
// PlayerFoodItem.Awake()에서 한 번만 만들어 캐싱해두고, 획득/사용/대기 중일 때마다 재사용합니다.
public class FoodEffectContext
{
    public PlayerStats Stats { get; }
    public PlayerHealth Health { get; }
    public PlayerResources Resources { get; }
    public GameFlowManager GameFlow { get; }
    public LevelBuilder LevelBuilder { get; }
    public GameObject PlayerObject { get; }

    public Transform PlayerTransform => PlayerObject != null ? PlayerObject.transform : null;

    private readonly PlayerFoodItem owner;

    // 지연형 완료 신호를 음식 슬롯(owner) 대신 다른 곳으로 보내고 싶을 때(예: 93번 액티브가 직전
    // 음식을 재현할 때) 쓰는 대체 콜백입니다. null이면 평소처럼 owner로 보냅니다.
    private readonly Action<string> completionOverride;

    public FoodEffectContext(PlayerStats stats, PlayerHealth health, PlayerResources resources,
        GameFlowManager gameFlow, LevelBuilder levelBuilder, GameObject playerObject, PlayerFoodItem owner)
    {
        Stats = stats;
        Health = health;
        Resources = resources;
        GameFlow = gameFlow;
        LevelBuilder = levelBuilder;
        PlayerObject = playerObject;
        this.owner = owner;
    }

    // 지연형 완료 신호를 owner(음식 슬롯) 대신 onPendingCompleted로 보내는 컨텍스트입니다.
    // 음식 슬롯과 무관하게 음식 효과를 재현할 때(예: 93번 액티브) 씁니다 - 이 컨텍스트로 실행한
    // 효과가 끝났다고 알려도 음식 슬롯의 아이템이 소멸하지 않습니다.
    public FoodEffectContext(PlayerStats stats, PlayerHealth health, PlayerResources resources,
        GameFlowManager gameFlow, LevelBuilder levelBuilder, GameObject playerObject, Action<string> onPendingCompleted)
    {
        Stats = stats;
        Health = health;
        Resources = resources;
        GameFlow = gameFlow;
        LevelBuilder = levelBuilder;
        PlayerObject = playerObject;
        completionOverride = onPendingCompleted;
    }

    // 지연형 효과가 실제 트리거를 감지했을 때 호출합니다 - sourceItemId가 지금 대기 중인
    // 아이템과 일치할 때만 실제로 소멸 처리됩니다(PlayerFoodItem.CompletePendingUse 참고).
    public void CompletePendingUse(string sourceItemId)
    {
        if (completionOverride != null)
        {
            completionOverride(sourceItemId);
            return;
        }

        owner?.CompletePendingUse(sourceItemId);
    }
}
