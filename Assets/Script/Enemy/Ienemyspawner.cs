using UnityEngine;

// 방 하나에 적을 스폰하는 방식을 표현하는 인터페이스입니다.
// 나중에 실제 적 스폰 로직(RealEnemySpawner 등)을 만들면 이 인터페이스만 구현해서
// LevelBuilder의 Enemy Spawner Component 필드에 그 컴포넌트로 갈아 끼우기만 하면 됩니다.
public interface IEnemySpawner
{
    void SpawnEnemies(RoomInstance room, Transform parent);
}