using UnityEngine;

// 액티브 31번 - "코스트 4를 소모해 사용하면, 즉시 플레이어가 있는 방의 모든 적을 2초 동안
// 기절시킨다. 보스도 포함한다."
//
// [기획 - 형빈 확정]
// - 기절 중에는 이동/사격/스킬을 전혀 하지 못합니다(보스 포함, 예외 없음) - 접촉 데미지는
//   그대로 유지됩니다. 실제 기절 로직/보스 처리/매료(38번)와의 상호작용은 전부
//   EnemyStunState.cs가 담당합니다 - 이 클래스는 "방 안의 적을 모아서 하나씩
//   TryApplyStun()을 호출"하는 일만 합니다.
// - 기절시킬 수 있는 적이 없으면(방에 적이 아예 없거나 전부 죽었으면) 사용할 수 없고
//   코스트도 소모하지 않습니다(16번처럼 코스트를 내기 전에 CanActivate로 미리 확인).
// - 이미 기절 중인 적에게 다시 걸리면 지속시간이 늘어납니다 - EnemyStunState.
//   TryApplyStun()이 이미 기절 중이어도 무시하지 않고 항상 적용하므로, 이 클래스가 따로
//   신경 쓸 필요 없이 모든 적에게 동일하게 호출하기만 하면 자연스럽게 처리됩니다.
// - 6번 패시브(제어 지속시간 보너스) 연동도 EnemyStunState.TryApplyStun() 내부에서
//   EnemyControlDurationBonus.Resolve()로 처리됩니다.
//
// [구현 - 방 안의 적 모으기]
// 29번(RoomWideAttackDamageEffect)이 이미 같은 방식(LevelBuilder.CurrentPlayerCell ->
// GeneratedRooms.occupiedCells로 현재 방 찾기 -> GetRoomController -> ContentRoot.
// GetComponentsInChildren<EnemyHealth>(true))으로 "이 방의 적 전체"를 모으고 있어 동일한
// 패턴을 그대로 사용했습니다.
//
// [IDelayedActiveItemEffect를 구현한 이유]
// 이 효과는 지연 소모(대기)가 없습니다. 다만 "코스트를 내기 전에 사용 가능 여부를 먼저
// 확인"하는 장치(CanActivate)를 PlayerActiveItem이 이 인터페이스로만 호출하기 때문에
// 구현만 해두었고, 대기 상태에 들어가지 않으므로 CancelPending은 빈 본문입니다(16번과
// 동일한 이유).
//
// [아이템 에셋 설정]
// ItemDefinition: Item Id = 31, Category = Active, Active Cost = 4, Active Effect =
// RoomWideStunEffect.
[System.Serializable]
public class RoomWideStunEffect : IDelayedActiveItemEffect
{
    [Tooltip("기본 기절 지속시간(초). 실제 적용값은 6번 패시브 보너스가 합산된 뒤의 값입니다" +
             "(EnemyStunState.TryApplyStun 참고).")]
    public float stunDuration = 2f;

    // 코스트를 내기 전에 호출됩니다. 지금 방에 기절시킬 수 있는(살아있는) 적이 하나라도
    // 있어야 true입니다.
    public bool CanActivate(ActiveEffectContext context)
    {
        var enemies = GetRoomEnemies(context);
        if (enemies == null) return false;

        foreach (var enemy in enemies)
        {
            if (enemy != null && !enemy.IsDead) return true;
        }
        return false;
    }

    public void Activate(ActiveEffectContext context)
    {
        var enemies = GetRoomEnemies(context);
        if (enemies == null) return;

        foreach (var enemy in enemies)
        {
            if (enemy == null || enemy.IsDead) continue;

            var stunState = EnemyStunState.GetOrAdd(enemy.gameObject);
            stunState?.TryApplyStun(stunDuration);
        }
    }

    // 지연 소모가 없는 효과라 취소할 대기 상태가 없습니다.
    public void CancelPending(ActiveEffectContext context)
    {
    }

    // 지금 플레이어가 있는 방의 EnemyHealth 목록을 반환합니다. 방을 찾지 못하면 null.
    // RoomWideAttackDamageEffect(29번)와 동일한 조회 로직입니다.
    private EnemyHealth[] GetRoomEnemies(ActiveEffectContext context)
    {
        if (context?.LevelBuilder == null) return null;

        var levelBuilder = context.LevelBuilder;
        if (!levelBuilder.HasPlayerCell) return null;

        Vector2Int currentCell = levelBuilder.CurrentPlayerCell;

        RoomInstance currentRoom = null;
        foreach (var room in levelBuilder.GeneratedRooms)
        {
            if (room != null && room.occupiedCells.Contains(currentCell))
            {
                currentRoom = room;
                break;
            }
        }
        if (currentRoom == null) return null;

        var controller = levelBuilder.GetRoomController(currentRoom.id);
        if (controller == null || controller.ContentRoot == null) return null;

        return controller.ContentRoot.GetComponentsInChildren<EnemyHealth>(true);
    }
}
