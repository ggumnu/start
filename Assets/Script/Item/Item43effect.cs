using System;
using UnityEngine;

// 액티브 아이템 43호 - "사용 시 현재 방에서만 공격력이 50% 증가한다".
//
// [기획 - 형빈 확정]
// - 사용하면 그 즉시 공격력이 attackMultiplier배가 되고, 그 방에 머무는 동안 유지됩니다. 다른 방으로 나가는 순간 즉시 해제되고,
//   나갔던 방으로 돌아와도 재적용되지 않습니다("사용한 시점부터 다음 방 이동 전까지" 한정).
// - 같은 방에서 여러 번 사용하면 곱으로 계속 쌓입니다(코스트가 있는 만큼, 상한 없음): 1번 x1.5, 2번 x2.25, 3번 x3.375 ...
//   같은 출처로 Multiplicative 수정자를 하나씩 더 거는 방식이라(PlayerStats가 전부 곱함), 방을 나갈 때 출처째 한 번에 지웁니다.
// - 사용 조건: 지금 방에 살아 있는 적(87번 아군 제외)이 없으면 사용 불가(코스트도 소모 X, 알림 없음) - 29번과 같은 방식
//   (IActiveItemUseRules.CanActivateNow). 보스방은 보스도 적으로 셉니다.
//
// [해제 - RoomController.OnAnyRoomEntered 구독]
// LevelBuilder는 플레이어가 있는 칸이 실제로 바뀔 때만 EnterRoom()을 부르므로, OnAnyRoomEntered를 "다른 방으로 넘어갔다"는 신호로
// 씁니다. 처음 사용할 때 한 번 구독하고, 다음 방 진입 순간 수정자를 지우고 스스로 구독을 해지합니다.
[Serializable]
public class RoomAttackBuffEffect : IActiveItemUseRules
{
    [Tooltip("사용 1번마다 곱해질 공격력 배율입니다 (1.5 = 50% 증가). PlayerStats.AddModifier의 " +
             "Multiplicative 규칙과 동일하게 '배율 자체'를 넣습니다(0.5가 아니라 1.5).")]
    public float attackMultiplier = 1.5f;

    private const string SourceId = "item43_room_attack_buff";

    // 방을 나가면 해제하기 위해 구독해둔 핸들러입니다. null이 아니면 "지금 버프가 걸려있고 구독도 이미 되어있다"는 뜻입니다
    // (같은 방에서 다시 사용해도 중복 구독되지 않도록).
    [NonSerialized] private Action<RoomInstance> roomEnteredHandler;
    [NonSerialized] private Action sceneChangeHandler;

    // [사용 조건] 쿨타임 없음, 코스트는 평소와 같음.
    public float CooldownRemaining01 => 0f;

    public int ResolveUseCost(ActiveEffectContext context, int effectiveCost) => effectiveCost;

    // 플레이어가 살아 있고, 지금 방에 살아 있는 적(아군 제외)이 하나라도 있어야 사용할 수 있습니다.
    public bool CanActivateNow(ActiveEffectContext context)
    {
        if (context == null || context.Stats == null) return false;
        if (context.Health != null && context.Health.IsDead) return false;

        var enemies = GetRoomEnemies(context);
        if (enemies == null) return false;

        foreach (var enemy in enemies)
        {
            if (enemy == null || enemy.IsDead || !enemy.gameObject.activeInHierarchy) continue;
            if (enemy.GetComponent<AllyFaction>() != null) continue;
            return true;
        }
        return false;
    }

    public void Activate(ActiveEffectContext context)
    {
        if (context?.Stats == null) return;

        // 사용할 때마다 x배율을 하나 더 겁니다(곱으로 중첩).
        context.Stats.AddModifier(StatType.Damage, ModifierType.Multiplicative, attackMultiplier, SourceId);

        if (roomEnteredHandler != null) return; // 이미 이 방에서 해제 구독 중

        var stats = context.Stats;
        roomEnteredHandler = _ =>
        {
            if (stats != null) stats.RemoveModifiersFromSource(SourceId);
            StopWatching();
        };
        RoomController.OnAnyRoomEntered += roomEnteredHandler;

        // 버프 중에 판이 끝나면(사망 후 다시하기 등) 구독이 에셋에 남아 다음 판에 섞이지 않게 함께 정리.
        sceneChangeHandler = StopWatching;
        LoadingScreen.OnBeforeSceneChange += sceneChangeHandler;
    }

    private void StopWatching()
    {
        if (roomEnteredHandler != null) RoomController.OnAnyRoomEntered -= roomEnteredHandler;
        if (sceneChangeHandler != null) LoadingScreen.OnBeforeSceneChange -= sceneChangeHandler;
        roomEnteredHandler = null;
        sceneChangeHandler = null;
    }

    // 플레이어가 있는 방의 ContentRoot 아래 모든 적(29번과 같은 방식). 레벨 생성기가 없는 씬(아이템 테스트 씬)은 씬 전체.
    private static EnemyHealth[] GetRoomEnemies(ActiveEffectContext context)
    {
        if (context?.LevelBuilder == null)
        {
            return UnityEngine.Object.FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None);
        }

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
