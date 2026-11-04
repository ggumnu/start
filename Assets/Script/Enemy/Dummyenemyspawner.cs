using UnityEngine;

// 테스트용: 캡슐 모양 더미 적을 1~3마리 랜덤한 위치에 스폰합니다.
public class DummyEnemySpawner : MonoBehaviour, IEnemySpawner
{
    [Header("스폰 개수 범위")]
    public int minCount = 1;
    public int maxCount = 3;

    [Header("스폰 영역 반경")]
    public float spawnRadius = 3f;

    public void SpawnEnemies(RoomInstance room, Transform parent)
    {
        int count = Random.Range(minCount, maxCount + 1);
        for (int i = 0; i < count; i++)
        {
            var enemy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            enemy.name = $"DummyEnemy_{i}";
            enemy.tag = "Enemy"; // Projectile.cs가 이 태그로 피격 판정을 합니다.
            enemy.transform.SetParent(parent);

            Vector2 offset = Random.insideUnitCircle * spawnRadius;
            enemy.transform.localPosition = new Vector3(offset.x, 1f, offset.y);

            enemy.AddComponent<EnemyHealth>();
        }
    }
}