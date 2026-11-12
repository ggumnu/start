using UnityEngine;

// 액티브 29번 - "코스트 3을 소모해 사용하면, 플레이어가 현재 위치한 방 안의 모든 적에게
// 현재 공격력의 100%에 해당하는 피해를 입힌다. 다른 방이나(플레이어가 지금 없는) 적에게는
// 영향을 주지 않는다. 플레이어가 보스방 안에 있다면 보스도 포함해서 피해를 입는다."
//
// [기획]
// - 코스트 소모(3)는 이 효과가 직접 처리하지 않습니다 - PlayerActiveItem.TryUse()가
//   ItemDefinition.activeCost를 먼저 TrySpend()로 확인/차감한 뒤에만 Activate()를
//   호출하므로, 이 아이템 에셋의 Active Cost를 3으로 설정하기만 하면 됩니다.
// - 피해량은 "그 순간의" 최종 공격력(PlayerStats.Damage - 모든 패시브 보너스가 이미
//   반영된 값)의 damageRatio(기본 1.0 = 100%)배입니다.
// - "플레이어가 현재 위치한 방"은 LevelBuilder.CurrentPlayerCell이 속한 RoomInstance를
//   찾아 판정합니다(GeneratedRooms.occupiedCells 기준) - 다른 방으로 새어나가지 않습니다.
//   방 카테고리와 무관하게(보스방 포함) 그 방 안의 모든 EnemyHealth가 대상입니다.
//
// [구현 - 방 안의 적 모으기]
// RoomObjectives.EnemyKillObjective가 이미 같은 방식(방의 ContentRoot 아래에서
// GetComponentsInChildren<EnemyHealth>(true))으로 "이 방의 적 전체"를 모으고 있어 동일한
// 패턴을 그대로 사용했습니다. EnemyHealth.TakeDamage()는 이미 죽은 적에게 다시 호출해도
// 안전합니다(isDead 가드).
//
// [사용 조건 - 형빈 확정] 지금 방에 살아 있는 적이 하나도 없으면 사용할 수 없습니다(코스트도 소모하지 않음). 손해만 보는 사용을 막는
// 다른 액티브(106번 등)와 같은 방식입니다 - IActiveItemUseRules.CanActivateNow로 코스트를 내기 전에 확인합니다. 87번 아군은 적으로 세지 않습니다.
[System.Serializable]
public class RoomWideAttackDamageEffect : IActiveItemUseRules, IHasOwnUseSound
{
    [Tooltip("현재 공격력(PlayerStats.Damage) 대비 피해 비율입니다. 1.0 = 100%.")]
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

    // 이펙트가 자동으로 사라지기까지 걸리는 시간(초)입니다 -
    // ExplodeOnImpactEffect.explosionVfxLifetime의 기본값과 동일합니다.
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

            enemy.TakeDamage(damage);
            SpawnDamageVfx(enemy.transform.position);
        }
    }

    // 피해를 받은 적마다 그 위치에 이펙트를 재생합니다. 프리팹이 비어있으면(미할당)
    // 아무 일도 하지 않습니다 - ExplodeOnImpactEffect.explosionVfxPrefab과 동일한 관례입니다.
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

    // ExplodeOnImpactEffect.ApplyVfxScale()과 동일한 방식입니다 - 파티클 시스템이 있으면
    // Transform.localScale이 아니라 파티클 자체의 시작 크기/속도에 곱해야 Scaling Mode/
    // Simulation Space 설정과 무관하게 항상 적용됩니다.
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

    // 지금 플레이어가 있는 방의 EnemyHealth 목록을 반환합니다. 방을 찾지 못하면 null.
    // RoomWideSleepEffect(31번)와 동일한 조회 로직입니다(GetRoomEnemies로 이름도 통일).
    //
    // [형빈 확인 - LevelBuilder가 아예 없는 씬(ItemTestArenaBuilder 테스트 아레나) 폴백]
    // 테스트 아레나는 LevelBuilder 자체가 없는 하나로 트인 공간이라(ItemTestArenaBuilder.cs
    // 상단 주석 참고), 이 경우 LevelBuilder를 못 찾으면 "씬 전체를 하나의 방으로" 취급해
    // 씬에 있는 모든 EnemyHealth(테스트 아레나의 더미 적 포함)를 대상으로 삼습니다. 실제
    // 게임(LevelBuilder가 있는 씬)에서는 이 분기를 절대 타지 않으므로 기존 동작은 전혀
    // 바뀌지 않습니다.
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
