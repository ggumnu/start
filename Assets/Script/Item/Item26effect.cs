using System.Collections.Generic;
using UnityEngine;

// 패시브 26번 - "획득 시 해킹툴 2개를 얻는다(레벨당 최초 1회). 보유하고 있으면 진입 조건이 해킹툴인 특수방을
// 해킹툴 소모 없이 진입할 수 있다."
//
// [기획 - 형빈 확정]
// 1) 해킹툴 지급: 획득할 때 hackingToolAmount(기본 2)개를 줍니다. 단 "하나의 레벨" 기준 최초 1회입니다(다른 일회용/
//    장신구의 1회성 지급과 같은 규칙 - GameFlowManager.TryConsumeOneTimeGrant). 같은 레벨에서 잃어버린 뒤 다시
//    획득하면 효과는 다시 생기지만 해킹툴은 주지 않고, 다음 레벨에서 다시 획득하면 다시 줍니다(n.5 선택방 레벨은
//    같은 레벨로 취급).
// 2) 해킹툴 특수방 잠금 해제: 이 아이템을 들고 있는 동안, 해킹툴 특수방이 붙은 부모 방(시작방/일반방)에 플레이어가
//    있으면 잠금 바리케이드가 해제 이펙트와 함께 사라집니다. "아이템 덕분에 열렸다"는 신호가 보이도록 항상 플레이어
//    눈앞에서만 풀립니다.
//    - 아직 클리어하지 않은 일반방: 그 방을 클리어하는 순간, 열린 벽 전투 바리케이드와 같은 프레임에 함께 풀립니다.
//    - 이미 클리어한 일반방 / 시작방(클리어 개념이 없는 방): 그 방에 들어오면 unlockDelay초 뒤에 풀립니다.
//      다음 레벨 시작방처럼 로딩 화면이 떠 있는 동안에는 기다렸다가, 로딩 화면이 다 사라진 뒤에 풉니다.
//    - 부모 방 안에서 이 아이템을 획득한 경우(예: 클리어한 일반방의 보물상자): 획득 직후 그 자리에서 풀립니다.
//    - 16번 액티브로 지금 있는 방에 해킹툴 특수방이 새로 붙은 경우: 바리케이드가 나타나는 연출(AppearFromWhite)이
//      끝난 뒤 풀립니다(두 연출이 같은 머티리얼을 동시에 건드리지 않도록).
//    - 해제 판정은 "푸는 순간 들고 있는지"입니다. 부모 방에 가기 전이나 전투 중에 잃어버리면 효과가 없고, 이미 풀린
//      잠금은 잃어버려도 다시 잠기지 않습니다.
//
// [하나의 규칙으로 정리한 이유]
// 잠금 바리케이드는 부모 방 콘텐츠의 자식이라, 플레이어가 그 부모 방에 있을 때만 켜져 있습니다(activeInHierarchy).
// 그래서 "보유 중 + 바리케이드가 켜져 있음(=플레이어가 부모 방 안) + 부모 방이 클리어됐거나 클리어 개념이 없는 방"
// 하나만 매 프레임 확인하면, 방 진입/현장 획득/16번 방 추가/다음 레벨 시작이 전부 같은 방식으로 처리됩니다.
// 클리어 순간만은 전투 바리케이드와 정확히 같은 프레임에 풀리도록 방 클리어 이벤트로 따로 처리합니다.
//
// [기존 스크립트는 고치지 않았습니다]
// SpecialRoomLock(All/Condition/ParentRoom/IsLocked/Unlock), RoomController(OnAnyRoomCleared/RequiresClear),
// LoadingScreen.IsShowing, GameFlowManager.TryConsumeOneTimeGrant 등 이미 열려 있는 것만 씁니다. 해제 자체는
// SpecialRoomLock.Unlock()을 그대로 불러서, 해킹툴로 열 때와 똑같은 해제 이펙트(Special Room Entry Settings의
// 해킹툴 칸) + 흰색 소멸 + 콜라이더 즉시 꺼짐이 그대로 나옵니다.
//
// [10번과의 관계]
// 10번(수류탄 특수방)과 기능이 같고 진입 조건/지급 자원만 해킹툴로 바뀐 아이템입니다. 10번을 테스트가 끝난 상태로
// 그대로 두기 위해 코드를 공유하지 않고 별도 파일로 만들었습니다(감시 컴포넌트도 따로 붙음). 둘을 함께 들고 있으면
// 각자 자기 조건의 잠금만 풀고 서로 간섭하지 않으며, 1회 지급 기록도 아이템마다 따로 남습니다.
//
// [아이템 에셋 설정]
// ItemDefinition: Item Id = 26, Category = Passive, Special Effects에 HackingToolRoomPassEffect 하나.
[System.Serializable]
public class HackingToolRoomPassEffect : IPassiveItemEffect, IRemovablePassiveEffect
{
    [Tooltip("획득할 때 주는 해킹툴 개수입니다. 같은 레벨에서는 한 번만 줍니다(잃어버렸다가 다시 획득해도 다시 주지 않음). 단, 48번으로 얻으면 제한 없이 줍니다.")]
    [Min(0)] public int hackingToolAmount = 2;

    [Tooltip("해킹툴 특수방이 붙은 부모 방(이미 클리어한 일반방/시작방)에 들어온 뒤 잠금이 풀리기까지의 시간(초)입니다. " +
             "들어오자마자 터지는 것보다 '들어왔더니 열린다'는 느낌을 주기 위한 값입니다. 방을 클리어하는 순간에는 이 " +
             "값과 상관없이 전투 바리케이드와 함께 즉시 풀립니다.")]
    [Min(0f)] public float unlockDelay = 0.3f;

    public void Apply(PassiveEffectContext context, string sourceItemId)
    {
        if (context == null || context.PlayerObject == null)
        {
            Debug.LogWarning("HackingToolRoomPassEffect: 플레이어를 찾을 수 없어 효과를 적용하지 못했습니다.");
            return;
        }

        GrantHackingToolsOncePerLevel(context, sourceItemId);

        // 플레이어에 감시 컴포넌트를 붙입니다(이미 있으면 그대로 쓰고 값만 갱신). 이 컴포넌트가 붙어 있는 것 자체가
        // "26번 보유 중"이라는 뜻이라, 나중에 아이템을 잃는 기능이 생기면 이 컴포넌트를 떼면 효과가 멈춥니다.
        var runner = context.PlayerObject.GetComponent<HackingToolRoomPassRunner>();
        if (runner == null) runner = context.PlayerObject.AddComponent<HackingToolRoomPassRunner>();
        runner.unlockDelay = unlockDelay;
    }

    // [패시브 잃기 - 48번 등] 소지 중에만 유지되던 효과를 되돌립니다(IRemovablePassiveEffect.cs 참고).
    // 특수방 해제 감시 컴포넌트를 즉시 떼어냅니다(같은 프레임에 다시 얻을 수 있어 DestroyImmediate). 1회성으로 받은 재화는
    // 빼앗지 않습니다(형빈 확정).
    public void Remove(PassiveEffectContext context, string sourceItemId)
    {
        if (context == null || context.PlayerObject == null) return;

        var runner = context.PlayerObject.GetComponent<HackingToolRoomPassRunner>();
        if (runner != null) Object.DestroyImmediate(runner);
    }

    private void GrantHackingToolsOncePerLevel(PassiveEffectContext context, string sourceItemId)
    {
        if (hackingToolAmount <= 0) return;

        if (context.Resources == null)
        {
            Debug.LogWarning("HackingToolRoomPassEffect: PlayerResources를 찾을 수 없어 해킹툴을 지급하지 못했습니다.");
            return;
        }

        // 레벨당 1회 게이트입니다(GameFlowManager - 다음 정수 레벨로 넘어갈 때만 초기화). 아이템 테스트 씬처럼
        // GameFlowManager가 없는 곳에서는 막을 기준이 없으므로 경고만 남기고 지급합니다.
        // [48번 연동, 형빈 확정] 48번으로 얻을 때는(PassiveEffectContext.IgnoreOneTimeGrantLimit) 레벨당 1회 제한 없이 지급합니다.
        if (!context.IgnoreOneTimeGrantLimit)
        {
            var gameFlow = Object.FindFirstObjectByType<GameFlowManager>();
            if (gameFlow != null)
            {
                if (!gameFlow.TryConsumeOneTimeGrant(sourceItemId + "_hackingtool_grant")) return;
            }
            else
            {
                Debug.LogWarning("HackingToolRoomPassEffect: GameFlowManager를 찾지 못해 레벨당 1회 제한 없이 해킹툴을 지급합니다.");
            }
        }

        context.Resources.Add(BasicItemType.HackingTool, hackingToolAmount);
    }
}

// 26번 효과의 실제 감시/해제를 맡는 런타임 컴포넌트입니다. HackingToolRoomPassEffect.Apply()가 플레이어에 자동으로
// 붙이므로 프리팹이나 씬에 직접 붙일 필요가 없습니다. 플레이어와 함께 다음 레벨로 넘어가고, 새 게임(씬 재시작)에서는
// 플레이어와 함께 사라집니다.
public class HackingToolRoomPassRunner : MonoBehaviour
{
    [Tooltip("부모 방에 들어온 뒤 잠금이 풀리기까지의 시간(초)입니다. 아이템 에셋의 Unlock Delay 값이 획득 시 복사됩니다.")]
    [Min(0f)] public float unlockDelay = 0.3f;

    // 해제 조건을 만족하기 시작한 시각입니다(바리케이드별). 조건이 깨지면(방을 나감, 로딩 화면 등) 지워져서
    // 다음에 다시 조건을 만족할 때 처음부터 다시 셉니다.
    private readonly Dictionary<SpecialRoomLock, float> eligibleSince = new Dictionary<SpecialRoomLock, float>();

    // 매 프레임 SpecialRoomLock.All을 복사해 돌 때 쓰는 재사용 목록입니다(Unlock()이 All에서 자신을 빼기 때문).
    private readonly List<SpecialRoomLock> snapshot = new List<SpecialRoomLock>();
    private readonly List<SpecialRoomLock> staleKeys = new List<SpecialRoomLock>();

    void OnEnable()
    {
        RoomController.OnAnyRoomCleared += HandleRoomCleared;
    }

    void OnDisable()
    {
        RoomController.OnAnyRoomCleared -= HandleRoomCleared;
        eligibleSince.Clear();
    }

    // 전투방이 클리어되는 순간입니다. RoomController가 전투 바리케이드를 흰색으로 사라지게 시작한 직후 같은 프레임에
    // 호출되므로, 여기서 풀면 잠금 바리케이드도 함께 사라집니다.
    //
    // [try/catch로 감싼 이유] 이 이벤트는 마지막 적의 EnemyHealth.Die() 안에서 동기적으로 호출됩니다. 여기서 예외가
    // 나면 그 예외가 Die()까지 올라가 보물상자 스폰/적 파괴가 막히므로(RoomController.HandleCleared 주석 참고), 이
    // 효과에서 생긴 문제는 여기서 확실히 막습니다.
    private void HandleRoomCleared(RoomInstance room)
    {
        try
        {
            if (room == null) return;

            CopyLocks();
            foreach (var lockItem in snapshot)
            {
                if (!IsTargetLock(lockItem)) continue;
                if (lockItem.ParentRoom != room) continue;
                if (IsAppearing(lockItem)) continue; // 나타나는 연출 중이면 Update가 연출이 끝난 뒤 풉니다.

                eligibleSince.Remove(lockItem);
                lockItem.Unlock();
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"HackingToolRoomPassRunner: 방 클리어 시 해킹툴 특수방 해제 중 오류가 발생해 건너뜁니다 (방 클리어 자체에는 영향 없음): {e}");
        }
    }

    void Update()
    {
        // 로딩 화면이 떠 있는 동안에는 해제 연출이 가려지므로 기다립니다(다음 레벨 시작방 - 로딩 화면이 다 사라진
        // 뒤부터 unlockDelay를 셉니다).
        if (LoadingScreen.IsShowing)
        {
            eligibleSince.Clear();
            return;
        }

        CopyLocks();
        float now = Time.time;

        foreach (var lockItem in snapshot)
        {
            if (!IsTargetLock(lockItem) || !IsParentReady(lockItem) || IsAppearing(lockItem))
            {
                if (lockItem != null) eligibleSince.Remove(lockItem);
                continue;
            }

            if (!eligibleSince.TryGetValue(lockItem, out float since))
            {
                eligibleSince[lockItem] = now;
                since = now;
            }

            if (now - since >= unlockDelay)
            {
                eligibleSince.Remove(lockItem);
                lockItem.Unlock();
            }
        }

        RemoveStaleEntries();
    }

    // 이 효과가 풀 수 있는 바리케이드인지: 아직 잠긴 해킹툴 특수방 잠금이고, 지금 화면에 켜져 있어야 합니다
    // (켜져 있다 = 플레이어가 그 부모 방 안에 있다).
    private static bool IsTargetLock(SpecialRoomLock lockItem)
    {
        return lockItem != null &&
               lockItem.IsLocked &&
               lockItem.Condition == SpecialRoomEntryCondition.HackingTool &&
               lockItem.gameObject.activeInHierarchy;
    }

    // 부모 방이 지금 풀어도 되는 상태인지: 클리어가 필요 없는 방(시작방 등)이거나 이미 클리어한 방이어야 합니다.
    // 잠금 바리케이드는 부모 방 콘텐츠 밑에 있으므로 부모 방의 RoomController를 그대로 찾아 판단합니다(전투방 기준을
    // 이 스크립트에 따로 두지 않음). 혹시 못 찾으면 RoomController와 같은 기준(일반방/보스방만 클리어 필요)을 씁니다.
    private static bool IsParentReady(SpecialRoomLock lockItem)
    {
        var parent = lockItem.ParentRoom;
        if (parent == null) return false;
        if (parent.isCleared) return true;

        var controller = lockItem.GetComponentInParent<RoomController>();
        bool requiresClear = controller != null
            ? controller.RequiresClear
            : parent.category == RoomCategory.Normal || parent.category == RoomCategory.Boss;
        return !requiresClear;
    }

    // 16번 방 추가로 바리케이드(또는 그 바리케이드가 들어 있는 벽)가 아직 나타나는 연출 중인지입니다.
    // AppearFromWhite는 연출이 끝나면 스스로 컴포넌트를 지우므로, 남아 있으면 진행 중입니다.
    private static bool IsAppearing(SpecialRoomLock lockItem)
    {
        return lockItem.GetComponentInParent<AppearFromWhite>() != null ||
               lockItem.GetComponentInChildren<AppearFromWhite>() != null;
    }

    private void CopyLocks()
    {
        snapshot.Clear();
        var all = SpecialRoomLock.All;
        for (int i = 0; i < all.Count; i++) snapshot.Add(all[i]);
    }

    // 파괴됐거나(레벨 전환) 이미 풀린 바리케이드의 기록을 정리합니다.
    private void RemoveStaleEntries()
    {
        if (eligibleSince.Count == 0) return;

        staleKeys.Clear();
        foreach (var key in eligibleSince.Keys)
        {
            if (key == null || !key.IsLocked) staleKeys.Add(key);
        }
        foreach (var key in staleKeys) eligibleSince.Remove(key);
    }
}
