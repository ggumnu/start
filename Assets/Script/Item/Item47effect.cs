using System;
using System.Collections.Generic;
using UnityEngine;

// 패시브 아이템 47번 - "소지하고 있으면 적을 처치할 때마다 공격력이 1 증가한다. 단, 이
// 증가는 한 방에서 최대 3번까지만 적용된다(방의 적이 몇 마리든 상관없이 그 방에서는 최대
// 공격력 +3). 보스도 동일하게 처치 시 +1 대상이다."
//
// [기획]
// - 게임 전체에서 적이 죽을 때마다(보스 포함) 공격력이 damagePerKill(기본 1)만큼 증가합니다.
// - 단, "방 하나"당 최대 maxIncreasesPerRoom(기본 3)번까지만 증가합니다. 예: 적 5마리가
//   있는 방을 전부 처치해도 공격력은 +3만 됩니다(4, 5번째 처치는 증가 없음).
// - 여기서 "방 하나"는 방의 종류가 아니라 그 방 인스턴스(RoomInstance.id) 자체를 뜻합니다.
//   같은 방이라도 다른 레벨/다른 판에서 다시 만난 방은 다른 id를 가지므로 별개로
//   취급됩니다.
//
// [v1.38 - 방 재입장 파밍 방지, 형빈 확정]
// 이 게임은 "클리어하지 못한 전투방을 나갔다가 다시 들어오면 적이 전부 부활한다"는 기존
// 규칙이 있습니다(RoomController의 전투방 초기화 - 클래스 주석 참고). 만약 이 효과의 증가
// 횟수를 "방에 들어올 때마다" 리셋했다면, 일부러 방에 들어가 3마리 잡고 클리어 전에
// 나갔다가 다시 들어와(적 부활) 또 3마리를 잡는 식으로 반복해 공격력을 무한정 farming할
// 수 있었습니다. 그래서 리셋 방식이 아니라, 방 인스턴스(id)별로 "이 방에서 지금까지 이
// 효과로 몇 번 증가했는지"를 영구적으로 기억합니다 - 같은 방 id는 몇 번을 나갔다 들어와도
// (적이 부활해도) 이미 다 채운 한도까지는 절대 다시 채워지지 않고, 처음 보는 방 id를
// 만나야만 다시 3번의 여유가 생깁니다.
//
// [구현 - EnemyHealth.OnAnyEnemyDied + RoomController.OnAnyRoomEntered]
// "게임 전체 처치"를 감지하기 위해 32번 장신구와 동일하게 EnemyHealth.OnAnyEnemyDied(보스
// 포함, 정적 전역 이벤트)를 구독합니다. 다만 이 이벤트는 "누가 죽었는지" 방 정보를 주지
// 않으므로(사망 위치만 넘겨줍니다), "지금 플레이어가 어느 방에 있는지"를 별도로
// RoomController.OnAnyRoomEntered(39번 장신구가 쓰는 것과 동일한 이벤트)로 추적해두고,
// 처치가 발생하면 그 캐싱된 현재 방 id를 기준으로 판정합니다. 공격력 반영은 다른 모든
// 스탯형 효과와 동일하게 PlayerStats.AddModifier(Damage, Additive, ...)를 그때그때
// 호출하는 방식입니다 - 나중에 복원할 일이 없는 영구 효과라 누적된 모디파이어를 따로
// 추적/제거할 필요가 없습니다.
//
// [재적용 안전장치]
// 이 효과 인스턴스는 ItemDefinition 에셋에 저장된 하나뿐인 객체라, 같은 에디터 세션
// 안에서 런을 재시작해 이 아이템을 다시 주우면 Apply()가 또 호출될 수 있습니다. 그때
// 이전 구독이 남아있으면 처치 한 번에 공격력이 여러 번 오르는 버그가 생기므로, Apply()
// 시작 시 이전 구독을 먼저 해지하고 상태(방별 카운트 포함)를 전부 초기화한 뒤 새로
// 구독합니다.
//
// [패시브 잃기 / 48번 - 형빈 확정]
// - 이 효과로 오른 공격력은 영구 귀속이라 47번을 잃어도 남습니다. 기본 스탯과 구분되도록 PlayerStats에 "아이템 ID +
//   _effect" 출처로 등록합니다(IRemovablePassiveEffect.cs 참고). 잃으면 처치 감시만 멈춥니다.
// - 48번으로 47번을 다시 얻으면 방별 증가 횟수 기록을 초기화합니다 - 이미 +3을 채운 방에서도 다시 +3까지 오릅니다
//   (다시 얻을 기회가 드문 만큼 다시 얻은 이점을 주기 위함). 같은 판(같은 플레이어)에서 다시 얻은 경우에는 "지금 있는
//   방"을 그대로 기억해, 48번을 쓴 그 방에서 바로 처치가 집계됩니다.
[Serializable]
public class KillCountDamageBoostEffect : IPassiveItemEffect, IRemovablePassiveEffect
{
    [Tooltip("적을 처치할 때마다 증가하는 공격력입니다.")]
    public float damagePerKill = 1f;

    [Tooltip("방 하나(같은 RoomInstance)당 이 효과로 공격력이 증가할 수 있는 최대 횟수입니다. " +
             "그 방에서 이 횟수만큼 이미 증가했다면, 같은 방에서 몇 마리를 더 잡거나 나갔다 " +
             "다시 들어와도(적 부활) 더 이상 증가하지 않습니다.")]
    public int maxIncreasesPerRoom = 3;

    private PlayerStats stats;
    private string effectSourceId;
    private GameObject owner; // 이 효과를 건 플레이어(같은 판에서 다시 얻었는지 구분용)

    // 지금 플레이어가 있는 방의 id입니다. RoomController.OnAnyRoomEntered로 갱신되며,
    // 아직 한 번도 방에 들어온 적이 없으면(이론상 거의 없음) -1입니다.
    private int currentRoomId = -1;

    // 방 id -> 그 방에서 이 효과로 지금까지 증가한 횟수. 방을 나갔다 다시 들어와도
    // 지워지지 않고 영구적으로 유지됩니다(위 [v1.38] 참고) - 그래야 같은 방 id에서
    // 한도를 다 채운 뒤 재입장으로 파밍하는 것을 막을 수 있습니다.
    private readonly Dictionary<int, int> increaseCountByRoomId = new Dictionary<int, int>();

    private Action<RoomInstance> roomEnteredHandler;
    private Action<Vector3> enemyDiedHandler;

    public void Apply(PassiveEffectContext context, string sourceItemId)
    {
        if (context?.Stats == null) return;

        // 재적용 시 이전 구독/상태가 남아있으면 먼저 정리합니다(위 [재적용 안전장치] 참고).
        Unsubscribe();

        // 같은 플레이어가 다시 얻은 경우(48번)에는 지금 있는 방을 그대로 기억하고, 새 판이면 처음부터 시작합니다.
        bool samePlayer = owner != null && owner == context.PlayerObject;
        if (!samePlayer) currentRoomId = -1;
        owner = context.PlayerObject;

        stats = context.Stats;
        effectSourceId = PassiveEffectIds.EffectStatSourceId(sourceItemId);
        increaseCountByRoomId.Clear();

        roomEnteredHandler = room =>
        {
            currentRoomId = room != null ? room.id : -1;
        };

        enemyDiedHandler = _ =>
        {
            if (currentRoomId < 0) return;

            increaseCountByRoomId.TryGetValue(currentRoomId, out int countSoFar);
            if (countSoFar >= maxIncreasesPerRoom) return;

            increaseCountByRoomId[currentRoomId] = countSoFar + 1;
            stats.AddModifier(StatType.Damage, ModifierType.Additive, damagePerKill, effectSourceId);
        };

        RoomController.OnAnyRoomEntered += roomEnteredHandler;
        EnemyHealth.OnAnyEnemyDied += enemyDiedHandler;
    }

    // [패시브 잃기 - 48번 등] 처치 감시를 멈춥니다. 이미 오른 공격력은 영구 귀속이라 그대로 둡니다(형빈 확정).
    public void Remove(PassiveEffectContext context, string sourceItemId)
    {
        Unsubscribe();
    }

    private void Unsubscribe()
    {
        if (roomEnteredHandler != null) RoomController.OnAnyRoomEntered -= roomEnteredHandler;
        if (enemyDiedHandler != null) EnemyHealth.OnAnyEnemyDied -= enemyDiedHandler;
        roomEnteredHandler = null;
        enemyDiedHandler = null;
    }
}
