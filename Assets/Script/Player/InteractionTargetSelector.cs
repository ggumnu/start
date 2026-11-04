using UnityEngine;

// "지금 F를 누르면 어떤 대상이 반응하는가"를 한 곳에서 정하는 공용 판정입니다.
//
// [왜 필요한가 - 형빈 확정]
// 예전에는 보물상자(TreasureChest)/동아리방 보물상자(ItemPedestal)/포탈(LevelPortal)이 각자 "플레이어가 내 범위
// 안인가 + F가 눌렸나"만 확인해서, 범위가 겹치면 F 한 번에 여러 대상이 한꺼번에 반응했습니다(예: 보스방에서 상자를
// 여는 F가 포탈에도 전달됨). 이제 각 대상은 F를 처리하기 전에 IsCurrentTarget(this)로 "내가 지금 F 대상인가"를
// 확인하고, 대상이 아니면 F를 무시합니다 - F 한 번에 항상 딱 하나만 반응합니다.
//
// [대상 고르는 규칙]
// - 후보: 플레이어가 범위 안에 있는 대상 중
//     · 상자(TreasureChest/ItemPedestal): 아직 안 열었거나, 열었지만 가져갈 것이 남아있는 것
//       (다 가져가서 사라지는 연출 중인 상자는 제외)
//     · 포탈(LevelPortal): 항상
//     · 상점 상인(ShopNPC): 항상
//     · 보관 좌대(HoldingPedestal): 아이템이 올려져 있을 때만
// - 그중 플레이어와 바닥 평면 거리(높이 무시 - 탑다운 화면에서 보이는 거리)가 가장 가까운 하나.
// - 범위 판정 자체는 각 대상의 기존 PlayerInRange를 그대로 씁니다(상자는 거리, 포탈/동아리방 상자는 트리거).
//
// [같은 판정을 쓰는 곳 - 항상 서로 일치]
// F 안내(InteractPromptDisplay), 아이템 설명칸(PlayerItemInfoDisplay), 기본 아이템 목록 설명칸
// (TreasureChestInfoDisplay)도 이 판정의 결과를 그대로 씁니다. 그래서 "F 안내가 뜬 대상 = 설명칸에 나온 상자 =
// 실제로 F가 먹는 대상"이 항상 같습니다.
//
// [계산 시점]
// 한 프레임에 처음 물어볼 때 한 번만 계산하고, 그 프레임 안에서는 같은 결과를 돌려줍니다(누가 먼저 물어보든 같은
// 답). 그래서 F로 상자를 비운 바로 그 프레임에 다른 상자가 이어서 반응하는 일도 없습니다.
public static class InteractionTargetSelector
{
    private static int computedFrame = -1;
    private static Component current;
    private static Transform playerTransform;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        computedFrame = -1;
        current = null;
        playerTransform = null;
    }

    // 지금 F 대상(TreasureChest / ItemPedestal / LevelPortal / ShopNPC / HoldingPedestal 중 하나, 없으면 null)
    public static Component Current
    {
        get
        {
            Recompute();
            return current;
        }
    }

    // 이 대상이 지금 F 대상인가
    public static bool IsCurrentTarget(Component candidate)
    {
        return candidate != null && Current == candidate;
    }

    private static void Recompute()
    {
        if (computedFrame == Time.frameCount) return;
        computedFrame = Time.frameCount;
        current = null;

        // 상점 창 / 슬롯 아이템 버리기 창 / 다음 레벨 이동 확인창 / 게임 중단 화면 / 패시브 교체 창(56번)이 떠 있는 동안(+ 닫힌 그 프레임)에는 F 대상이
        // 없습니다 - F 안내도 숨고, 상자/포탈/상인/좌대 모두 F를 무시합니다.
        if (ShopUI.BlocksInteract || SlotItemDropUI.BlocksInteract || LevelTransitionConfirmUI.BlocksInteract ||
            PauseMenuUI.BlocksInteract || PassiveRerollUI.BlocksInteract) return;

        if (playerTransform == null)
        {
            var playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) playerTransform = playerObj.transform;
        }
        if (playerTransform == null) return;

        Vector3 playerPos = playerTransform.position;
        float bestDistance = float.MaxValue;

        foreach (var pedestal in Object.FindObjectsByType<ItemPedestal>(FindObjectsSortMode.None))
        {
            if (!IsCandidate(pedestal)) continue;
            Consider(pedestal, playerPos, ref bestDistance);
        }

        foreach (var chest in Object.FindObjectsByType<TreasureChest>(FindObjectsSortMode.None))
        {
            if (!IsCandidate(chest)) continue;
            Consider(chest, playerPos, ref bestDistance);
        }

        foreach (var portal in Object.FindObjectsByType<LevelPortal>(FindObjectsSortMode.None))
        {
            if (portal == null || !portal.PlayerInRange) continue;
            Consider(portal, playerPos, ref bestDistance);
        }

        // 상점 상인 - 범위 판정은 ShopNPC 자신의 거리 계산(PlayerInRange, 사망 중엔 false)을 그대로 씁니다.
        foreach (var shop in Object.FindObjectsByType<ShopNPC>(FindObjectsSortMode.None))
        {
            if (shop == null || !shop.PlayerInRange) continue;
            Consider(shop, playerPos, ref bestDistance);
        }

        // 보관 좌대 - 아이템이 올려져 있을 때만(거리 판정은 HoldingPedestal 자신의 PlayerInRange, 사망 중엔 false)
        foreach (var holding in Object.FindObjectsByType<HoldingPedestal>(FindObjectsSortMode.None))
        {
            if (holding == null || !holding.PlayerInRange || holding.IsEmpty) continue;
            Consider(holding, playerPos, ref bestDistance);
        }
    }

    private static bool IsCandidate(ItemPedestal pedestal)
    {
        return pedestal != null && pedestal.PlayerInRange && (!pedestal.IsOpened || pedestal.HasAnythingToPickup);
    }

    private static bool IsCandidate(TreasureChest chest)
    {
        return chest != null && chest.PlayerInRange && (!chest.IsOpened || chest.HasAnythingToPickup);
    }

    private static void Consider(Component candidate, Vector3 playerPos, ref float bestDistance)
    {
        Vector3 p = candidate.transform.position;
        float dx = p.x - playerPos.x;
        float dz = p.z - playerPos.z;
        float distance = dx * dx + dz * dz; // 바닥 평면 거리의 제곱(비교만 하므로 제곱으로 충분)
        if (distance < bestDistance)
        {
            bestDistance = distance;
            current = candidate;
        }
    }
}
