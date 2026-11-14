using UnityEngine;

// 액티브 29번 - "코스트 3을 소모해 사용하면, 플레이어가 현재 위치한 방 안의 모든 적에게 현재 공격력의 Damage Ratio배(에셋 값)
// 피해를 입힌다. 플레이어가 없는 방의 적에게는 영향을 주지 않는다. 보스방이면 보스도 포함."
//
// [기획 - 형빈 확정]
// - 코스트는 PlayerActiveItem.TryUse()가 에셋의 Active Cost(3)로 먼저 차감합니다(이 효과는 코스트를 다루지 않음).
// - 피해량 = 사용 순간의 최종 공격력(PlayerStats.Damage) x damageRatio. 피해 숫자는 공격력과 상관없이 항상 Weak(아이템 효과 피해).
// - 대상 = 플레이어가 있는 방의 ContentRoot 아래 모든 EnemyHealth(EnemyKillObjective와 같은 방식 - 다른 방으로 새지 않음).
// - 사용 조건: 지금 방에 살아 있는 적(87번 아군 제외)이 없으면 사용 불가(코스트도 소모 X) - 손해만 보는 사용을 막는 다른 액티브
//   (106번 등)와 같은 방식(IActiveItemUseRules.CanActivateNow).
[System.Serializable]
public class RoomWideAttackDamageEffect : IActiveItemUseRules, IHasOwnUseSound
{
    [Tooltip("현재 공격력(PlayerStats.Damage) 대비 피해 비율입니다. 1.0 = 100%, 2.0 = 200%.")]
    public float damageRatio = 1.0f;

    [Header("이펙트")]
    [Tooltip("방 전체에 피해를 줄 때, 피해를 받은 적마다 그 위치에 재생할 이펙트 " +
             "프리팹입니다. 비워두면(미할당) 이펙트 없이 피해만 적용됩니다.")]
    public GameObject damageVfxPrefab;
    [Tooltip("이펙트 생성 위치에 더할 오프셋입니다(월드 기준, 미터 단위). 기본값은 적 " +
             "위치 그대로입니다.")]
    public Vector3 damageVfxPositionOffset = Vector3.zero;
    [Tooltip("이펙트 크기 배율입니다. 1이 원본 크기입니다.")]
    public float damageVfxScaleMultiplier = 1f;

    // 이펙트 자동 삭제 시간(초) - 4번 폭발 이펙트 기본값과 같음.
    private const float DamageVfxLifetime = 2f;

    // [사용 조건] 쿨타임 없음, 코스트는 평소와 같음.
    public float CooldownRemaining01 => 0f;

    public int ResolveUseCost(ActiveEffectContext context, int effectiveCost) => effectiveCost;

    // [사용 조건] 플레이어가 살아 있고, 지금 방에 살아 있는 적(아군 제외)이 하나라도 있어야 사용할 수 있습니다.
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
        GameAudio.Play(SfxId.Active29); // [사운드] 전용 사용음(공용 발동음 대신)
        if (context?.Stats == null) return;

        var enemies = GetRoomEnemies(context);
        if (enemies == null) return;

        float damage = context.Stats.Damage * damageRatio;
        if (damage <= 0f) return;

        foreach (var enemy in enemies)
        {
            if (enemy == null || enemy.IsDead) continue;

            enemy.TakeWeakDamage(damage); // 피해 숫자 항상 Weak(형빈 확정)
            SpawnDamageVfx(enemy.transform.position);
        }
    }

    // 피해를 받은 적 위치마다 이펙트(프리팹이 비면 없음).
    private void SpawnDamageVfx(Vector3 position)
    {
        if (damageVfxPrefab == null) return;

        GameObject vfx = Object.Instantiate(
            damageVfxPrefab, position + damageVfxPositionOffset, Quaternion.identity);
        // [방 전환] 플레이어가 다른 방에 들어가면 바로 지워지도록 등록합니다(RoomScope - 다른 방 위 허공에 남지 않게).
        RoomScope.DestroyWhenPlayerLeaves(vfx);

        if (!Mathf.Approximately(damageVfxScaleMultiplier, 1f))
        {
            ApplyVfxScale(vfx, damageVfxScaleMultiplier);
        }

        Object.Destroy(vfx, DamageVfxLifetime);
    }

    // 4번 폭발과 같은 방식 - 파티클은 localScale이 아니라 시작 크기/속도에 곱해야 Scaling Mode와 무관하게 적용됨.
    private static void ApplyVfxScale(GameObject instance, float multiplier)
    {
        if (instance == null) return;

        var systems = instance.GetComponentsInChildren<ParticleSystem>(true);
        if (systems.Length == 0)
        {
            instance.transform.localScale *= multiplier;
            return;
        }

        for (int i = 0; i < systems.Length; i++)
        {
            var main = systems[i].main;

            main.startSizeMultiplier *= multiplier;
            if (main.startSize3D)
            {
                main.startSizeXMultiplier *= multiplier;
                main.startSizeYMultiplier *= multiplier;
                main.startSizeZMultiplier *= multiplier;
            }
            main.startSpeedMultiplier *= multiplier;
        }
    }

    // 지금 플레이어가 있는 방의 EnemyHealth 목록(못 찾으면 null - 31번과 같은 조회).
    // LevelBuilder가 없는 테스트 아레나는 하나로 트인 공간이라 씬 전체를 한 방으로 취급(형빈 확인).
    private EnemyHealth[] GetRoomEnemies(ActiveEffectContext context)
    {
        if (context?.LevelBuilder == null)
        {
            return Object.FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None);
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
