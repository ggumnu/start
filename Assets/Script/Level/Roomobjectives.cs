using System;
using System.Collections.Generic;
using UnityEngine;

// 방 클리어 조건을 표현하는 인터페이스입니다.
// 지금은 "적 전멸"만 구현하지만, 나중에 퍼즐 등 다른 조건이 추가되면
// 이 인터페이스를 구현하는 새 클래스만 만들면 RoomController 쪽 코드는 그대로 재사용됩니다.
public interface IRoomObjective
{
    event Action OnCompleted;
    bool IsComplete { get; }
    void Initialize(Transform roomContentRoot);
}

// "이 방 안의 모든 EnemyHealth가 죽으면 클리어" 조건입니다. 일반맵 보물상자용으로 처음 적 수(InitialCount)와 마지막 사망 위치를 함께 들고 있고,
// 87번 포섭(TryExclude)으로 뺄 수 있으며, 2레벨 보스가 소환한 적은 TryAddAlongside로 더합니다. 설계 배경은 Docs/설계노트.md [방 클리어 조건].
public class EnemyKillObjective : IRoomObjective
{
    public event Action OnCompleted;
    public bool IsComplete { get; private set; }

    // 이 방에 실제로 스폰되어 존재했던 적의 수입니다 (Initialize() 시점에 확정된 뒤 불변).
    public int InitialCount { get; private set; }

    // 가장 최근에(=지금까지 죽은 적 중 마지막으로) 죽은 적의 사망 위치입니다. 아직 아무도
    // 죽지 않았다면 Vector3.zero입니다.
    public Vector3 LastDeathPosition { get; private set; }

    private int remaining;

    // [87번 포섭] 이 목표가 세고 있는 적들과, 아직 끝나지 않은 목표 목록입니다(포섭한 적을 빼기 위함).
    private readonly HashSet<EnemyHealth> tracked = new HashSet<EnemyHealth>();
    private static readonly List<EnemyKillObjective> activeObjectives = new List<EnemyKillObjective>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        activeObjectives.Clear();
    }

    // [87번 포섭] enemy를 세고 있는 목표에서 그 적을 뺍니다(아군이 되어 더 이상 죽일 필요가 없음). 남은 적이 없으면 그 방은 바로
    // 클리어됩니다(형빈 확정). 이때 "마지막으로 죽은 위치"(보물상자 위치 계산용)는 포섭한 적의 위치로 둡니다. 뺐으면 true.
    public static bool TryExclude(EnemyHealth enemy, Vector3 position)
    {
        if (enemy == null) return false;
        for (int i = activeObjectives.Count - 1; i >= 0; i--)
        {
            var o = activeObjectives[i];
            if (o == null || o.IsComplete)
            {
                activeObjectives.RemoveAt(i);
                continue;
            }
            if (!o.tracked.Remove(enemy)) continue;

            enemy.OnDied -= o.HandleEnemyDied;
            o.LastDeathPosition = position;
            o.remaining--;
            if (o.remaining <= 0 && !o.IsComplete)
            {
                o.IsComplete = true;
                activeObjectives.Remove(o);
                o.OnCompleted?.Invoke();
            }
            return true;
        }
        return false;
    }

    // [2레벨 보스 소환] existing(이미 세고 있는 적 - 보스)을 세는 목표에 extra(새로 소환된 적)를 더합니다 - extra까지 죽어야 방이 클리어됩니다.
    // existing을 세는 목표가 없거나 이미 끝났으면 더하지 않고 false.
    public static bool TryAddAlongside(EnemyHealth existing, EnemyHealth extra)
    {
        if (existing == null || extra == null || extra.IsDead) return false;
        for (int i = activeObjectives.Count - 1; i >= 0; i--)
        {
            var o = activeObjectives[i];
            if (o == null || o.IsComplete)
            {
                activeObjectives.RemoveAt(i);
                continue;
            }
            if (!o.tracked.Contains(existing)) continue;
            if (!o.tracked.Add(extra)) return true; // 이미 세는 중

            extra.OnDied += o.HandleEnemyDied;
            o.remaining++;
            return true;
        }
        return false;
    }

    public void Initialize(Transform roomContentRoot)
    {
        InitializeWithEnemies(roomContentRoot.GetComponentsInChildren<EnemyHealth>(true));
    }

    // 세어야 할 적 목록을 직접 넘겨받는 버전입니다. 전투 중 방을 벗어났다가 돌아와 방을 처음
    // 상태로 되돌릴 때(RoomController.ResetCombatRoom), 옛 적은 Destroy가 이번 프레임 끝에야
    // 처리되어 아직 방 안에 남아있으므로 방 전체를 다시 훑으면 옛 적까지 세어버립니다. 그래서
    // 새로 만든 적들만 명시적으로 넘겨서 사용합니다.
    public void InitializeWithEnemies(IList<EnemyHealth> enemies)
    {
        remaining = enemies.Count;
        InitialCount = remaining;

        if (remaining <= 0)
        {
            IsComplete = true;
            OnCompleted?.Invoke();
            return;
        }

        foreach (var enemy in enemies)
        {
            enemy.OnDied += HandleEnemyDied;
            if (enemy != null) tracked.Add(enemy); // [87번]
        }
        if (!activeObjectives.Contains(this)) activeObjectives.Add(this); // [87번]
    }

    private void HandleEnemyDied(Vector3 deathPosition)
    {
        LastDeathPosition = deathPosition;

        remaining--;
        if (remaining <= 0 && !IsComplete)
        {
            IsComplete = true;
            activeObjectives.Remove(this); // [87번]
            OnCompleted?.Invoke();
        }
    }
}