using System.Collections.Generic;
using UnityEngine;

// 음식 9번 - "사용하면 지금 있는 방의 보물상자 개수가 두 배로 늘어난다(원본과 같은 종류의
// 미개봉 보물상자가 하나 더 나타남). 그 방에 보물상자가 아예 없으면 사용할 수 없다."
//
// [기획]
// - 즉시형 음식입니다. 지금 플레이어가 있는 방(LevelBuilder.CurrentPlayerCell 기준)에 이미
//   존재하는 보물상자를 찾아, 그 상자와 같은 "종류"(동아리방=ItemPedestal / 일반방·보스방=
//   TreasureChest)의 새 미개봉 상자를 하나 더 스폰합니다.
// - 기존 상자가 열렸는지, 다 가져갔는지는 상관없습니다 - "그 방에 보물상자 오브젝트가 지금
//   존재하는가"만 봅니다. 이미 열었지만 아직 다 가져가지 않은 상자(예: 패시브 아이템은
//   챙겼지만 [현재 체력] 묶음이 남아있는 상자)도 그대로 인정됩니다. 반대로 전부 가져가서
//   이미 소멸(Destroy)된 상자나, 애초에 한 번도 스폰되지 않은 방(예: 확률에 당첨되지 않은
//   일반방)은 "상자 없음"으로 취급되어 사용할 수 없습니다 - 소모되지 않습니다(IFoodEffect.
//   CanUse 참고).
// - 새로 만든 상자는 원본의 내용물을 복사하지 않습니다. 완전히 새로 뽑는 "미개봉" 상자라서,
//   원본과 똑같은 풀(itemPool 또는 NormalRoomTreasureSpawner의 가중치 로직)에서 다시 추첨한
//   결과가 나옵니다 - 운이 좋으면 소모성 재화가 아닌 패시브/액티브 아이템이 나올 수도 있고,
//   원본이 최상급이 아니었어도 이번엔 다를 수 있습니다.
// - 방 종류별 대응:
//     · 동아리방(Club) - ItemPedestal을 하나 더 스폰 (LevelBuilder.SpawnDuplicateClubPedestal)
//     · 일반방(Normal) - TreasureChest를 하나 더 스폰, 원래 그 방에서 스폰된 적 수 기준으로
//       기본 아이템 슬롯 수를 계산 (NormalRoomTreasureSpawner.SpawnDuplicateChest)
//     · 보스방(Boss)   - 보스 확정 보상과 동일한 로직(패시브 우선)으로 TreasureChest를 하나 더
//       스폰 (NormalRoomTreasureSpawner.SpawnDuplicateBossChest)
//     · 특수방(Special) - 레이아웃 에셋의 Allow Chest Duplication이 켜진 레이아웃에서만 쓸 수 있습니다(형빈 확정 -
//       레이아웃마다 정함, 크레딧 더미는 켬). 꺼진 레이아웃에서는 "상자 없음"과 같아 쓸 수 없고 소모되지 않습니다.
//       상자는 같은 종류로만 복제됩니다:
//         - 바닥 프리팹의 크레딧 상자(PresetBasicItemChest) -> 같은 설정의 크레딧 상자
//           (NormalRoomTreasureSpawner.SpawnDuplicatePresetChest - 크레딧 더미 3개면 6개)
//         - 플레이어 아이템 받침대(ItemPedestal) -> 이 레이아웃 규칙의 받침대 (LevelBuilder.SpawnDuplicateSpecialPedestal)
//         - 그 외 상자(PresetBasicItemChest가 없는 일반 TreasureChest)는 특수방에 놓이지 않으므로 세지 않습니다.
//
// [개수 두 배 - 상자가 여러 개일 때 (형빈 확정 버그 수정)]
// 처음 버전은 방의 상자를 하나만 찾아 하나만 복제해서, 25번으로 9번을 두 번 쓰는 등 방에 상자가 이미 2개 이상이면
// "두 배"가 되지 않았습니다(2개 -> 3개). 이제 사용하는 순간 방에 있는 "살아있는" 상자를 전부 찾아(그 순간의 목록을
// 먼저 저장해두고) 각각 하나씩 복제하므로 항상 개수가 정확히 두 배가 됩니다(1 -> 2 -> 4 -> 8).
// - "살아있는" 상자 = 아직 안 열었거나, 열었지만 가져갈 것이 남아있는 상자. 전부 가져가서 사라지는 연출 중인
//   상자(판정은 이미 꺼짐)는 세지 않습니다 - 위 [기획]의 "이미 소멸된 상자는 상자 없음"과 같은 기준입니다.
// - 각 복제본은 자기 원본 상자 근처에 나타납니다. 한 프레임에 여러 개를 만들 때 서로 겹치지 않도록, 하나 만들
//   때마다 Physics.SyncTransforms()로 방금 만든 상자의 콜라이더를 겹침 검사에 반영한 뒤 다음 것을 만듭니다.
//
// [구현 - 기존 스폰 로직 재사용]
// 새로 뽑는 로직을 여기서 따로 만들지 않고, 방을 처음 지을 때/클리어할 때 쓰는 것과 완전히
// 동일한 내용물 결정 로직을 LevelBuilder/NormalRoomTreasureSpawner에 새로 추가한 public
// 메서드(SpawnDuplicateClubPedestal/SpawnDuplicateChest/SpawnDuplicateBossChest)를 통해
// 그대로 재사용합니다. 기존 스폰 경로(SpawnClubItemPedestal/TrySpawnTreasure/
// SpawnBossRewardChest)는 전혀 건드리지 않았습니다 - 새 진입점만 추가된 것입니다.
[System.Serializable]
public class DuplicateRoomChestFoodEffect : IFoodEffect
{
    public bool IsImmediate => true;

    // 획득 시 1회성 효과가 없는 음식이라 빈 본문입니다.
    public void OnAcquired(FoodEffectContext context, string sourceItemId)
    {
    }

    // [CanUse 게이트] 지금 있는 방에 보물상자가 하나도 없으면(한 번도 안 나왔거나 이미 다 주워
    // 사라졌으면) 사용할 수 없습니다.
    public bool CanUse(FoodEffectContext context)
    {
        return FindExistingChests(context, out _, out _, null) != ExistingChestKind.None;
    }

    public void ActivateImmediate(FoodEffectContext context, string sourceItemId)
    {
        // 복제하기 전에 "지금 방에 있는 상자 목록"을 먼저 저장해둡니다 - 복제본이 다시 복제되지 않도록.
        var existingChests = new List<GameObject>();
        var kind = FindExistingChests(context, out RoomInstance room, out RoomController controller, existingChests);
        if (kind == ExistingChestKind.None || controller == null || controller.ContentRoot == null || existingChests.Count == 0)
        {
            Debug.LogWarning("Item09effect: 사용 시점에 방/상자를 다시 찾지 못해 복제를 건너뜁니다.");
            return;
        }

        var levelBuilder = context?.LevelBuilder;
        Transform contentRootTransform = controller.ContentRoot.transform;

        // 상자마다 하나씩 복제 -> 개수가 정확히 두 배가 됩니다.
        foreach (var existingChestGO in existingChests)
        {
            if (existingChestGO == null) continue;
            Vector3 nearPosition = existingChestGO.transform.position;

            switch (kind)
            {
                case ExistingChestKind.Club:
                    levelBuilder?.SpawnDuplicateClubPedestal(room, contentRootTransform, nearPosition);
                    break;

                case ExistingChestKind.Normal:
                    if (levelBuilder != null && levelBuilder.treasureSpawner != null)
                    {
                        int enemyCount = controller.SpawnedEnemyCount;
                        levelBuilder.treasureSpawner.SpawnDuplicateChest(room, contentRootTransform, enemyCount, nearPosition);
                    }
                    break;

                case ExistingChestKind.Boss:
                    if (levelBuilder != null && levelBuilder.treasureSpawner != null)
                    {
                        levelBuilder.treasureSpawner.SpawnDuplicateBossChest(room, contentRootTransform, nearPosition);
                    }
                    break;

                case ExistingChestKind.Special:
                    // 특수방은 상자마다 종류가 다를 수 있어 상자별로 같은 종류를 복제합니다.
                    var preset = existingChestGO.GetComponent<PresetBasicItemChest>();
                    if (preset != null)
                    {
                        if (levelBuilder != null && levelBuilder.treasureSpawner != null)
                        {
                            levelBuilder.treasureSpawner.SpawnDuplicatePresetChest(preset, contentRootTransform);
                        }
                    }
                    else if (existingChestGO.GetComponent<ItemPedestal>() != null)
                    {
                        levelBuilder?.SpawnDuplicateSpecialPedestal(room, contentRootTransform, nearPosition);
                    }
                    break;
            }

            // 방금 만든 상자의 콜라이더를 겹침 검사에 바로 반영해, 다음 복제본이 같은 자리에 겹쳐 나오지 않게 합니다.
            Physics.SyncTransforms();
        }
    }

    // 즉시형이라 호출되지 않습니다.
    public void BeginPending(FoodEffectContext context, string sourceItemId) { }

    // 즉시형이라 호출되지 않습니다.
    public void CancelPending(FoodEffectContext context, string sourceItemId) { }

    private enum ExistingChestKind { None, Club, Normal, Boss, Special }

    // 지금 플레이어가 있는 방을 찾고, 그 방의 콘텐츠 루트 밑에서 "살아있는" 상자(ItemPedestal 또는
    // TreasureChest)를 전부 찾아 outChests에 담습니다(null이면 개수만 확인). 하나라도 있으면 그 방의 상자
    // 종류를, 없으면 None을 돌려줍니다. CanUse()와 ActivateImmediate() 양쪽에서 공유하는 조회
    // 로직입니다 - 사용하기까지 방을 이동할 수는 없으므로(같은 프레임 안에서 즉시
    // 이어지는 호출) 두 번 찾아도 결과가 달라지지 않습니다.
    private ExistingChestKind FindExistingChests(FoodEffectContext context, out RoomInstance room, out RoomController controller, List<GameObject> outChests)
    {
        room = null;
        controller = null;
        outChests?.Clear();

        var levelBuilder = context?.LevelBuilder;
        if (levelBuilder == null || !levelBuilder.HasPlayerCell) return ExistingChestKind.None;

        Vector2Int cell = levelBuilder.CurrentPlayerCell;
        foreach (var candidate in levelBuilder.GeneratedRooms)
        {
            if (candidate != null && candidate.occupiedCells != null && candidate.occupiedCells.Contains(cell))
            {
                room = candidate;
                break;
            }
        }
        if (room == null) return ExistingChestKind.None;

        controller = levelBuilder.GetRoomController(room.id);
        if (controller == null || controller.ContentRoot == null) return ExistingChestKind.None;

        if (room.category == RoomCategory.Club)
        {
            int count = 0;
            foreach (var pedestal in controller.ContentRoot.GetComponentsInChildren<ItemPedestal>())
            {
                // 살아있는 상자만: 아직 안 열었거나, 열었지만 가져갈 것이 남아있는 상자
                if (pedestal == null || (pedestal.IsOpened && !pedestal.HasAnythingToPickup)) continue;
                outChests?.Add(pedestal.gameObject);
                count++;
            }
            return count > 0 ? ExistingChestKind.Club : ExistingChestKind.None;
        }

        if (room.category == RoomCategory.Normal || room.category == RoomCategory.Boss)
        {
            int count = 0;
            foreach (var chest in controller.ContentRoot.GetComponentsInChildren<TreasureChest>())
            {
                if (chest == null || (chest.IsOpened && !chest.HasAnythingToPickup)) continue;
                outChests?.Add(chest.gameObject);
                count++;
            }
            if (count == 0) return ExistingChestKind.None;
            return room.category == RoomCategory.Boss ? ExistingChestKind.Boss : ExistingChestKind.Normal;
        }

        // 특수방: 레이아웃이 9번을 허용할 때만, 크레딧 상자(PresetBasicItemChest가 붙은 TreasureChest)와 플레이어 아이템
        // 받침대(ItemPedestal)를 셉니다. 살아있는 상자 기준은 위와 같습니다.
        if (room.category == RoomCategory.Special)
        {
            if (room.specialLayout == null || !room.specialLayout.allowChestDuplication) return ExistingChestKind.None;

            int count = 0;
            foreach (var chest in controller.ContentRoot.GetComponentsInChildren<TreasureChest>())
            {
                if (chest == null || (chest.IsOpened && !chest.HasAnythingToPickup)) continue;
                if (chest.GetComponent<PresetBasicItemChest>() == null) continue;
                outChests?.Add(chest.gameObject);
                count++;
            }
            foreach (var pedestal in controller.ContentRoot.GetComponentsInChildren<ItemPedestal>())
            {
                if (pedestal == null || (pedestal.IsOpened && !pedestal.HasAnythingToPickup)) continue;
                outChests?.Add(pedestal.gameObject);
                count++;
            }
            return count > 0 ? ExistingChestKind.Special : ExistingChestKind.None;
        }

        // 그 외 방 종류(상점/선택방/시작방 등)는 보물상자 개념이 없습니다.
        return ExistingChestKind.None;
    }
}
