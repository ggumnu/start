using System;
using UnityEngine;

// 액티브 아이템 43호 - "사용 시 현재 방에서만 공격력이 50% 증가한다".
//
// [기획]
// - 사용하면 그 즉시 공격력이 attackMultiplier배가 됩니다(기본값 1.5 = 50% 증가).
// - 그 방에 머무는 동안은 계속 유지됩니다.
// - 다른 방으로 나가는 순간 즉시 해제됩니다.
// - 나갔던 방으로 다시 돌아와도 재적용되지 않습니다 - "방 자체"에 걸리는 효과가 아니라
//   "사용한 시점부터 다음 방 이동 전까지" 한정으로 걸리는 1회성 효과입니다.
//
// [구현 - RoomController.OnAnyRoomEntered 구독]
// LevelBuilder.Update()는 플레이어가 있는 칸(cell)이 "실제로 바뀔 때만"
// RoomController.EnterRoom()을 호출합니다(같은 방에 머무는 동안은 절대 호출되지 않음).
// 그래서 EnterRoom()이 흘려보내는 RoomController.OnAnyRoomEntered를 "다른 방으로
// 넘어갔다"는 신호로 그대로 쓸 수 있습니다. 사용하는 순간 이 이벤트를 한 번 구독해두고,
// 처음 발생하는 순간(=다음 방 진입) 수정자를 지우고 스스로 구독을 해지합니다.
[Serializable]
public class RoomAttackBuffEffect : IActiveItemEffect
{
    [Tooltip("사용 시 적용될 공격력 배율입니다 (1.5 = 50% 증가). PlayerStats.AddModifier의 " +
             "Multiplicative 규칙과 동일하게 '배율 자체'를 넣습니다(0.5가 아니라 1.5).")]
    public float attackMultiplier = 1.5f;

    private const string SourceId = "item43_room_attack_buff";

    // 방을 나가면 해제하기 위해 구독해둔 핸들러입니다. null이 아니면 "지금 버프가 걸려있고
    // 구독도 이미 되어있다"는 뜻입니다 - 같은 방에서 다시 사용해도 중복 구독되지 않도록
    // 이 필드로 막습니다.
    private Action<RoomInstance> roomEnteredHandler;

    public void Activate(ActiveEffectContext context)
    {
        if (context?.Stats == null) return;

        // 이미 버프가 걸린 상태에서 다시 사용해도(코스트를 또 지불했더라도) 중첩되지
        // 않도록, 먼저 기존 수정자를 지우고 새로 겁니다.
        context.Stats.RemoveModifiersFromSource(SourceId);
        context.Stats.AddModifier(StatType.Damage, ModifierType.Multiplicative, attackMultiplier, SourceId);

        if (roomEnteredHandler != null)
        {
            // 이미 같은 방에서 구독 중이면 새로 구독하지 않습니다.
            return;
        }

        var stats = context.Stats;
        roomEnteredHandler = _ =>
        {
            stats.RemoveModifiersFromSource(SourceId);
            RoomController.OnAnyRoomEntered -= roomEnteredHandler;
            roomEnteredHandler = null;
        };
        RoomController.OnAnyRoomEntered += roomEnteredHandler;
    }
}
