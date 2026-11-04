using UnityEngine;

// 일회용 아이템 효과(IDisposableItemEffect)가 필요로 할 수 있는 참조들을 한데 모아 담는
// 컨테이너입니다. ActiveEffectContext/TrinketEffectContext와 같은 목적에, 텔레포트처럼
// 레벨 구조(방/앵커)에 접근해야 하는 효과(22번 등)를 위한 LevelBuilder 참조와, 지연형
// 효과가 트리거 발생 시 스스로 소멸을 요청할 수 있는 콜백(CompletePendingUse)까지 추가로
// 갖고 있습니다.
//
// 나중에 또 다른 참조가 필요해지면, 이 클래스에 프로퍼티 하나만 추가하면 됩니다 -
// IDisposableItemEffect 인터페이스나 기존 효과 구현체들은 전혀 손댈 필요가 없습니다.
//
// PlayerDisposableItem.Awake()에서 한 번만 만들어 캐싱해두고, 사용/대기 중일 때마다
// 재사용합니다.
public class DisposableEffectContext
{
    public PlayerStats Stats { get; }
    public PlayerHealth Health { get; }
    public PlayerResources Resources { get; }
    public GameFlowManager GameFlow { get; }
    public LevelBuilder LevelBuilder { get; }
    public GameObject PlayerObject { get; }

    public Transform PlayerTransform => PlayerObject != null ? PlayerObject.transform : null;

    private readonly PlayerDisposableItem owner;

    // [85번 연동] 지연형 완료/취소 신호를 일회용 슬롯(owner) 대신 다른 곳으로 보내고 싶을 때(85번 액티브가 마지막 일회용을
    // 재현할 때) 쓰는 대체 콜백입니다. null이면 평소처럼 owner로 보냅니다. FoodEffectContext의 completionOverride와 같은 방식입니다.
    private readonly System.Action<string> completionOverride;
    private readonly System.Action<string> cancelOverride;

    // [85번 연동] 지연형 완료/취소 신호를 owner(일회용 슬롯) 대신 onPendingCompleted/onPendingCancelled로 보내는 컨텍스트입니다.
    // 일회용 슬롯과 무관하게 효과를 재현할 때 씁니다 - 이 컨텍스트로 실행한 효과가 끝났다/취소됐다고 알려도 슬롯의 아이템이
    // 소모되거나 대기가 풀리지 않습니다.
    public DisposableEffectContext(PlayerStats stats, PlayerHealth health, PlayerResources resources,
        GameFlowManager gameFlow, LevelBuilder levelBuilder, GameObject playerObject,
        System.Action<string> onPendingCompleted, System.Action<string> onPendingCancelled)
    {
        Stats = stats;
        Health = health;
        Resources = resources;
        GameFlow = gameFlow;
        LevelBuilder = levelBuilder;
        PlayerObject = playerObject;
        completionOverride = onPendingCompleted ?? (_ => { });
        cancelOverride = onPendingCancelled ?? (_ => { });
    }

    public DisposableEffectContext(PlayerStats stats, PlayerHealth health, PlayerResources resources,
        GameFlowManager gameFlow, LevelBuilder levelBuilder, GameObject playerObject, PlayerDisposableItem owner)
    {
        Stats = stats;
        Health = health;
        Resources = resources;
        GameFlow = gameFlow;
        LevelBuilder = levelBuilder;
        PlayerObject = playerObject;
        this.owner = owner;
    }

    // 지연형 효과가 실제 트리거를 감지했을 때 호출합니다 - sourceItemId가 지금 대기 중인
    // 아이템과 일치할 때만 실제로 소멸 처리됩니다(PlayerDisposableItem.CompletePendingUse 참고).
    public void CompletePendingUse(string sourceItemId)
    {
        if (completionOverride != null)
        {
            completionOverride(sourceItemId); // [85번 연동] 재현용 컨텍스트
            return;
        }

        owner?.CompletePendingUse(sourceItemId);
    }

    // [56번 연동] 지연형 효과가 스스로 사용을 취소할 때 호출합니다(예: 창에서 [취소하기]) - 대기만 풀리고 아이템은 소모되지 않고
    // 그대로 남습니다. sourceItemId가 지금 대기 중인 아이템과 일치할 때만 처리됩니다(PlayerDisposableItem.CancelPendingUse 참고).
    public void CancelPendingUse(string sourceItemId)
    {
        if (cancelOverride != null)
        {
            cancelOverride(sourceItemId); // [85번 연동] 재현용 컨텍스트
            return;
        }

        owner?.CancelPendingUse(sourceItemId);
    }
}
