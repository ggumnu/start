using System.Collections.Generic;
using UnityEngine;

// 액티브 106번 - "보물상자가 있는 방에서만 사용할 수 있다. 25% 확률로 방의 보물상자가 3배가 되고, 75% 확률로 방의 모든 보물상자가 사라진다."
//
// [기획 - 형빈 확정]
// - 코스트 5(ItemDefinition의 Active Cost = 5, 45번이면 4). 지금 있는 방에 "살아있는" 보물상자가 없으면 코스트도 쓰지 않고 아무 반응도 없습니다.
//   보물상자 판정은 9번과 같습니다: 동아리방 좌대(ItemPedestal), 일반방/보스방 상자(TreasureChest), 복제가 허용된 특수방 레이아웃(Allow Chest
//   Duplication - 크레딧 더미 등)의 크레딧 상자/좌대. "살아있는" = 안 열었거나, 열었지만 가져갈 것이 남은 상자. 보스방 확정 보상 상자도 포함됩니다.
// - 오직 정상적으로 열 수 있는 보물상자만 대상입니다. 상점의 임시 보관 좌대, Ctrl 길게 눌러 내려놓은 슬롯 아이템 좌대(HoldingPedestal)는
//   복제/제거 대상이 아닙니다.
// - 25%(성공): 사용 순간의 상자 목록을 먼저 저장하고, 상자마다 9번과 같은 복제 함수로 2개씩 더 만듭니다(1 -> 3, 2 -> 6). 복제본은 내용물을 새로 뽑는
//   미개봉 상자입니다. 연출 = 하늘색 발광/입자와 함께 106번 아이콘이 플레이어 머리 위에 떠오름.
// - 75%(실패): 방 안의 보물상자를 열렸든 닫혔든(내용물을 가져갔든 아니든) 전부 하얗게 물들며 사라지게 합니다(DissolveAndVanish). 상자에 떠 있던
//   플레이어 아이템은 이미 "이번 레벨에 나온 아이템"으로 기록돼 있으므로 이 레벨에서는 다시 나오지 않습니다. 연출 = 빨간 발광/입자와 함께 106번
//   아이콘이 떠오른 뒤 VFX_ImpactCritical_1.1.0_Pink가 머리 위에서 터짐(103번과 같은 연출).
// - 확률은 LuckOdds를 거칩니다(나중에 행운을 정립하면 거기서만 바꾸면 됨 - 지금은 영향 없음).
//
// [구현] 74번 때 만든 사용 규칙(IActiveItemUseRules)으로 "상자가 있는지"를 코스트를 내기 전에 확인합니다. 9번(Item09effect.cs)은 수정하지 않았습니다.
//
// [아이템 에셋 설정]
// ItemDefinition: Item Id = 106, Category = Active, Active Cost = 5, Active Effect = ChestGambleActiveEffect.
// Failure Vfx Prefab 칸에 Assets/VFX/VFXPACK_IMPACT_WALLCOEUR_FreeVersion/00_Prefab/2_Impact_1.1.0/VFX_ImpactCritical_1.1.0_Pink를 넣습니다.
[System.Serializable]
public class ChestGambleActiveEffect : IActiveItemUseRules
{
    [Header("확률 / 배수")]
    [Tooltip("성공(상자 복제) 확률입니다(기본 0.25 = 25%). 실패하면 방의 상자가 전부 사라집니다. 행운 보정은 LuckOdds를 거칩니다(지금은 영향 없음).")]
    [Range(0f, 1f)] public float successChance = 0.25f;

    [Tooltip("성공했을 때 상자 개수 배수입니다(기본 3 = 상자마다 2개씩 더 생김).")]
    [Min(2)] public int duplicateMultiplier = 3;

    [Header("성공 연출 - 머리 위 아이콘 (하늘색)")]
    [Tooltip("성공 시 머리 위에 띄우는 아이콘 연출입니다. Icon을 비우면 106번 아이템 아이콘을 씁니다.")]
    public CreditPopupSettings successPopup = new CreditPopupSettings
    {
        glowColor = new Color(0.45f, 0.85f, 1f, 0.9f),
        sparkleColor = new Color(0.6f, 0.92f, 1f, 1f),
    };

    [Header("실패 연출 - 머리 위 아이콘 (빨강) + 이펙트")]
    [Tooltip("실패 시 머리 위에 띄우는 아이콘 연출입니다. Icon을 비우면 106번 아이템 아이콘을 씁니다.")]
    public CreditPopupSettings failurePopup = new CreditPopupSettings
    {
        glowColor = new Color(1f, 0.12f, 0.12f, 0.9f),
        sparkleColor = new Color(1f, 0.3f, 0.25f, 1f),
    };

    [Tooltip("아이콘이 떠오른 뒤 머리 위에서 터뜨릴 이펙트입니다(VFX_ImpactCritical_1.1.0_Pink). 비워두면 아이콘 연출만 나옵니다.")]
    public GameObject failureVfxPrefab;

    [Tooltip("아이콘이 나타난 뒤 이펙트를 터뜨리기까지의 시간(초)입니다(103번과 같은 0.25).")]
    [Min(0f)] public float failureVfxDelay = 0.25f;

    [Tooltip("머리 위 기준에서 이펙트를 옮기는 값입니다(월드 기준, m).")]
    public Vector3 failureVfxOffsetAboveHead = new Vector3(0f, 0.6f, 0f);

    [Tooltip("이펙트 크기 배율입니다.")]
    [Min(0.01f)] public float failureVfxScale = 1f;

    [Tooltip("켜면 이펙트가 플레이어를 따라다닙니다.")]
    public bool failureVfxFollowPlayer = true;

    [Tooltip("이펙트를 이 시간(초) 뒤에 지웁니다.")]
    [Min(0.1f)] public float failureVfxLifetime = 2f;

    // 쿨타임이 없는 액티브입니다.
    public float CooldownRemaining01 => 0f;

    public bool CanActivateNow(ActiveEffectContext context)
    {
        if (context == null || context.PlayerObject == null) return false;
        if (context.Health != null && context.Health.IsDead) return false;
        return FindRoomChests(context, aliveOnly: true, null, out _, out _, out _) > 0;
    }

    public int ResolveUseCost(ActiveEffectContext context, int effectiveCost) => effectiveCost;

    public void Activate(ActiveEffectContext context)
    {
        if (context == null) return;

        var aliveChests = new List<GameObject>();
        int count = FindRoomChests(context, aliveOnly: true, aliveChests, out RoomInstance room, out RoomController controller, out RoomKind kind);
        if (count == 0 || room == null || controller == null || controller.ContentRoot == null)
        {
            Debug.LogWarning("ChestGambleActiveEffect: 사용 시점에 방/상자를 다시 찾지 못했습니다.");
            return;
        }

        if (LuckOdds.Roll(successChance))
        {
            Duplicate(context, aliveChests, room, controller, kind);
            PlayPopup(context, success: true);
        }
        else
        {
            var allChests = new List<GameObject>();
            FindRoomChests(context, aliveOnly: false, allChests, out _, out _, out _);
            RemoveAll(allChests);
            PlayPopup(context, success: false);
        }
    }

    // ------------------------------------------------------------------
    // 복제 / 제거
    // ------------------------------------------------------------------

    private void Duplicate(ActiveEffectContext context, List<GameObject> chests, RoomInstance room, RoomController controller, RoomKind kind)
    {
        var levelBuilder = context.LevelBuilder;
        Transform contentRoot = controller.ContentRoot.transform;
        int extraPerChest = Mathf.Max(1, duplicateMultiplier - 1);

        // 사용 순간의 목록만 복제합니다(복제본이 다시 복제되지 않음).
        foreach (var chestGO in chests)
        {
            if (chestGO == null) continue;
            Vector3 near = chestGO.transform.position;

            for (int n = 0; n < extraPerChest; n++)
            {
                try
                {
                    switch (kind)
                    {
                        case RoomKind.Club:
                            levelBuilder?.SpawnDuplicateClubPedestal(room, contentRoot, near);
                            break;
                        case RoomKind.Normal:
                            if (levelBuilder != null && levelBuilder.treasureSpawner != null)
                                levelBuilder.treasureSpawner.SpawnDuplicateChest(room, contentRoot, controller.SpawnedEnemyCount, near);
                            break;
                        case RoomKind.Boss:
                            if (levelBuilder != null && levelBuilder.treasureSpawner != null)
                                levelBuilder.treasureSpawner.SpawnDuplicateBossChest(room, contentRoot, near);
                            break;
                        case RoomKind.Special:
                            var preset = chestGO.GetComponent<PresetBasicItemChest>();
                            if (preset != null)
                            {
                                if (levelBuilder != null && levelBuilder.treasureSpawner != null)
                                    levelBuilder.treasureSpawner.SpawnDuplicatePresetChest(preset, contentRoot);
                            }
                            else if (chestGO.GetComponent<ItemPedestal>() != null)
                            {
                                levelBuilder?.SpawnDuplicateSpecialPedestal(room, contentRoot, near);
                            }
                            break;
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"ChestGambleActiveEffect: 상자 복제 중 오류가 발생해 이 복제본은 건너뜁니다: {e}");
                }

                // 방금 만든 상자의 콜라이더를 겹침 검사에 바로 반영해, 다음 복제본이 같은 자리에 겹쳐 나오지 않게 합니다(9번과 같음).
                Physics.SyncTransforms();
            }
        }
    }

    private static void RemoveAll(List<GameObject> chests)
    {
        foreach (var chestGO in chests)
        {
            if (chestGO == null) continue;
            try
            {
                // 하얗게 물들며 사라집니다(판정은 즉시 꺼지고, 페이드가 끝나면 삭제).
                DissolveAndVanish.Begin(chestGO, DissolveAndVanish.EndAction.Destroy);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"ChestGambleActiveEffect: 상자 제거 연출 중 오류가 발생해 바로 지웁니다: {e}");
                Object.Destroy(chestGO);
            }
        }
    }

    // ------------------------------------------------------------------
    // 연출 (순수 장식 - 문제가 생겨도 복제/제거에는 영향 없음)
    // ------------------------------------------------------------------

    private void PlayPopup(ActiveEffectContext context, bool success)
    {
        try
        {
            GameObject player = context.PlayerObject;
            if (player == null) return;

            var active = player.GetComponent<PlayerActiveItem>();
            Sprite icon = active != null && active.EquippedItem != null ? active.EquippedItem.icon : null;

            // [머리 위 알림 줄 세우기] 실패 이펙트는 아이콘이 실제로 떠오르는 순간에 시작합니다(103번과 같은 "잠깐 뒤 머리 위에서 터짐" 실행기).
            System.Action onStarted = null;
            if (!success && failureVfxPrefab != null)
            {
                Transform playerT = player.transform;
                GameObject prefab = failureVfxPrefab;
                float delay = failureVfxDelay, scale = failureVfxScale, lifetime = failureVfxLifetime;
                Vector3 offset = failureVfxOffsetAboveHead;
                bool follow = failureVfxFollowPlayer;
                onStarted = () => GlassCannonPenaltyVfxRunner.Spawn(playerT, prefab, delay, offset, scale, follow, lifetime);
            }

            CreditPopupDisplay.Show(player, player.transform.position, success ? successPopup : failurePopup, icon, followTarget: true,
                                    onStarted: onStarted);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"ChestGambleActiveEffect: 연출 중 오류가 발생해 건너뜁니다(복제/제거에는 영향 없음): {e}");
        }
    }

    // ------------------------------------------------------------------
    // 방의 보물상자 찾기 (9번과 같은 기준)
    // ------------------------------------------------------------------

    private enum RoomKind { None, Club, Normal, Boss, Special }

    // 지금 방의 보물상자를 outChests에 담고 개수를 돌려줍니다(null이면 개수만). aliveOnly면 "살아있는"(안 열었거나 가져갈 것이 남은) 상자만,
    // 아니면 모든 상자입니다(이미 사라지는 중인 상자에 다시 사라짐을 걸어도 아무 일도 없음). 상점 보관 좌대/내려놓은 슬롯 아이템 좌대(HoldingPedestal)는 절대 세지 않습니다.
    private static int FindRoomChests(ActiveEffectContext context, bool aliveOnly, List<GameObject> outChests,
                                      out RoomInstance room, out RoomController controller, out RoomKind kind)
    {
        room = null;
        controller = null;
        kind = RoomKind.None;
        outChests?.Clear();

        var levelBuilder = context?.LevelBuilder;
        if (levelBuilder == null || !levelBuilder.HasPlayerCell) return 0;

        Vector2Int cell = levelBuilder.CurrentPlayerCell;
        foreach (var candidate in levelBuilder.GeneratedRooms)
        {
            if (candidate != null && candidate.occupiedCells != null && candidate.occupiedCells.Contains(cell))
            {
                room = candidate;
                break;
            }
        }
        if (room == null) return 0;

        controller = levelBuilder.GetRoomController(room.id);
        if (controller == null || controller.ContentRoot == null) return 0;
        Transform root = controller.ContentRoot.transform;

        int count = 0;
        switch (room.category)
        {
            case RoomCategory.Club:
                kind = RoomKind.Club;
                foreach (var p in root.GetComponentsInChildren<ItemPedestal>())
                    if (Accept(p != null ? p.gameObject : null, p != null && p.IsOpened, p != null && p.HasAnythingToPickup, aliveOnly, outChests)) count++;
                break;

            case RoomCategory.Normal:
            case RoomCategory.Boss:
                kind = room.category == RoomCategory.Boss ? RoomKind.Boss : RoomKind.Normal;
                foreach (var c in root.GetComponentsInChildren<TreasureChest>())
                    if (Accept(c != null ? c.gameObject : null, c != null && c.IsOpened, c != null && c.HasAnythingToPickup, aliveOnly, outChests)) count++;
                break;

            case RoomCategory.Special:
                // 9번과 같이 복제가 허용된 레이아웃만. 크레딧 상자(PresetBasicItemChest가 붙은 TreasureChest)와 플레이어 아이템 좌대(ItemPedestal).
                if (room.specialLayout == null || !room.specialLayout.allowChestDuplication) return 0;
                kind = RoomKind.Special;
                foreach (var c in root.GetComponentsInChildren<TreasureChest>())
                {
                    if (c == null || c.GetComponent<PresetBasicItemChest>() == null) continue;
                    if (Accept(c.gameObject, c.IsOpened, c.HasAnythingToPickup, aliveOnly, outChests)) count++;
                }
                foreach (var p in root.GetComponentsInChildren<ItemPedestal>())
                    if (Accept(p != null ? p.gameObject : null, p != null && p.IsOpened, p != null && p.HasAnythingToPickup, aliveOnly, outChests)) count++;
                break;

            default:
                return 0; // 상점/선택방/시작방 등은 보물상자 개념이 없습니다.
        }

        if (count == 0) kind = RoomKind.None;
        return count;
    }

    private static bool Accept(GameObject go, bool opened, bool hasAnything, bool aliveOnly, List<GameObject> outChests)
    {
        if (go == null) return false;
        if (go.GetComponent<HoldingPedestal>() != null) return false; // 보관/내려놓은 좌대는 보물상자가 아님
        if (aliveOnly && opened && !hasAnything) return false;
        outChests?.Add(go);
        return true;
    }
}
