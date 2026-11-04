using UnityEngine;

// 액티브 31번 - "코스트 4를 소모해 사용하면, 즉시 플레이어가 있는 방의 모든 적을 일정 시간 동안
// 잠재운다(수면). 보스도 포함한다."
//
// [기획 - 형빈 확정]
// - 수면 중에는 이동/사격/스킬을 전혀 하지 못합니다(보스 포함, 예외 없음) - 접촉 데미지는
//   그대로 유지됩니다. 실제 수면 로직/보스 처리/매료(38번)와의 상호작용은 전부
//   EnemyControlState.cs가 담당합니다(수면 = "강력 제어") - 이 클래스는 "방 안의 적을
//   모아서 하나씩 TryApplyStrong(Sleep)을 호출"하는 일만 합니다.
// - 잠재울 수 있는 적이 없으면(방에 적이 아예 없거나 전부 죽었으면) 사용할 수 없고
//   코스트도 소모하지 않습니다(16번처럼 코스트를 내기 전에 CanActivate로 미리 확인).
// - [제어 2단계 개편] 이미 수면(또는 해킹) 중인 적에게 다시 걸리면, 이전 강력 제어는
//   즉시 끝나고 새 수면 지속시간으로 교체됩니다(예전의 "남은 시간에 누적"은 폐지 - 형빈
//   확정). 매료 중이던 적은 매료가 즉시 끝나고 재매료 면역/유예가 적용됩니다.
// - 6번 패시브(제어 지속시간 보너스) 연동도 EnemyControlState.TryApplyStrong() 내부에서
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
// [수면 지속 이펙트 - 방울 (형빈 확정)]
// 수면을 거는 순간 1회 재생되는 이펙트(sleepVfxPrefab)와 별개로, 수면이 걸려 있는 동안 적의
// 머리 위에서 방울이 계속 올라오는 이펙트를 붙입니다. 해킹툴의 해킹 지속 이펙트와 똑같이
// EnemyStatusVfx.Attach()를 사용하므로 동작 규칙도 같습니다.
// - 수면 중에는 적을 따라다니며 계속 재생되고, 수면 중 31번을 다시 써도 새로 만들지 않고 이어감
// - 수면이 끝나면(시간 종료 / 해킹으로 교체 / 사망 / 사라지는 연출) 새 방울이 멈추고, 이미 떠 있던
//   방울도 전부 "사라지는 시간(Fade Out Time, 기본 0.25초)" 동안 함께 투명해지며 사라짐(형빈 확정 -
//   깨어난 뒤에 방울이 남아 보이지 않도록). 설정의 "Fade Out Remaining Particles" 체크가 이 동작이며,
//   끄면 예전처럼 이미 나온 방울이 남은 수명대로 끝까지 재생됩니다.
// 방울의 모양/움직임(크기 변화, 좌우 흔들림, 터짐)은 전부 vfx_Heal_02를 복제한 프리팹의
// 파티클 설정에서 만듭니다(이펙트는 프리팹에서 직접 수정 - 형빈 확정).
//
// [아이템 에셋 설정]
// ItemDefinition: Item Id = 31, Category = Active, Active Cost = 4, Active Effect =
// RoomWideSleepEffect.
[System.Serializable]
public class RoomWideSleepEffect : IDelayedActiveItemEffect
{
    [Tooltip("기본 수면 지속시간(초). 실제 적용값은 6번 패시브 보너스가 합산된 뒤의 값입니다" +
             "(EnemyControlState.TryApplyStrong 참고).")]
    public float sleepDuration = 2f;

    [Header("수면 색상 / 이펙트")]
    [Tooltip("수면 중 표시할 색상입니다. 발광(Emission) 효과 없이 오브젝트 색(_BaseColor/" +
             "_Color)만 이 색으로 바뀝니다 - 매료(38번)의 색 표시 방식과 동일합니다. 실제 " +
             "적용/우선순위 처리는 EnemyControlState가 담당합니다(그 파일 상단 주석 참고).")]
    public Color sleepColor = new Color(1f, 0.85f, 0.1f);

    [Tooltip("수면을 걸 때(=새 수면으로 교체될 때 포함)마다 그 적의 위치에 재생할 이펙트 " +
             "프리팹입니다. 이미 수면 중인 적에게 다시 걸려 새 수면으로 교체될 때도 매번 " +
             "똑같이 재생됩니다. 비워두면 이펙트 없이 색 변화만 적용됩니다.")]
    public GameObject sleepVfxPrefab;

    [Tooltip("이펙트 생성 위치에 더할 오프셋입니다(월드 기준, 미터 단위). 기본값은 적 " +
             "위치 그대로입니다.")]
    public Vector3 sleepVfxPositionOffset = Vector3.zero;

    [Tooltip("이펙트 크기 배율입니다. 1이 원본 크기입니다.")]
    public float sleepVfxScaleMultiplier = 1f;

    [Tooltip("이펙트가 자동으로 사라지기까지 걸리는 시간(초)입니다. 0 이하로 두면 " +
             "파괴하지 않고 그대로 둡니다(이펙트 자체에 Stop Action 등 자동 소멸 로직이 " +
             "있는 경우).")]
    public float sleepVfxLifetime = 1.5f;

    [Header("수면 지속 이펙트 (수면 중인 동안 적에게 계속 붙어 있음)")]
    [Tooltip("수면이 걸려 있는 동안 적에게 붙여둘 방울 이펙트입니다. 이펙트의 색/크기/움직임은 프리팹을 " +
             "복제해서 그 프리팹에서 직접 수정하세요. 수면이 끝나면 떠 있던 방울까지 전부 Fade Out Time " +
             "동안 투명해지며 사라집니다(Fade Out Remaining Particles 체크). 위의 Sleep Vfx Prefab" +
             "(걸리는 순간 한 번)과는 별개라 함께 씁니다.")]
    public EnemyStatusVfxSettings sleepStatusVfx = new EnemyStatusVfxSettings
    {
        fadeOutTime = 0.25f,
        fadeOutRemainingParticles = true,
    };

    // 코스트를 내기 전에 호출됩니다. 지금 방에 잠재울 수 있는(살아있는) 적이 하나라도
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

            var control = EnemyControlState.GetOrAdd(enemy.gameObject);
            if (control == null) continue;

            if (control.TryApplyStrong(EnemyControlType.Sleep, sleepDuration, sleepColor))
            {
                SpawnSleepVfx(enemy.transform.position);
                EnemyStatusVfx.Attach(enemy.gameObject, EnemyControlType.Sleep, sleepStatusVfx);
            }
        }
    }

    // 수면을 걸 때(=새 수면으로 교체될 때 포함)마다 그 적의 위치에 이펙트를 재생합니다. 프리팹이
    // 비어있으면(미할당) 아무 일도 하지 않습니다 - ExplodeOnImpactEffect.
    // explosionVfxPrefab과 동일한 관례입니다.
    private void SpawnSleepVfx(Vector3 position)
    {
        if (sleepVfxPrefab == null) return;

        GameObject vfx = Object.Instantiate(
            sleepVfxPrefab, position + sleepVfxPositionOffset, Quaternion.identity);
        // [방 전환] 플레이어가 다른 방에 들어가면 바로 지워지도록 등록합니다(RoomScope - 다른 방 위 허공에 남지 않게).
        RoomScope.DestroyWhenPlayerLeaves(vfx);

        if (!Mathf.Approximately(sleepVfxScaleMultiplier, 1f))
        {
            ApplyVfxScale(vfx, sleepVfxScaleMultiplier);
        }

        if (sleepVfxLifetime > 0f)
        {
            Object.Destroy(vfx, sleepVfxLifetime);
        }
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

    // 지연 소모가 없는 효과라 취소할 대기 상태가 없습니다.
    public void CancelPending(ActiveEffectContext context)
    {
    }

    // 지금 플레이어가 있는 방의 EnemyHealth 목록을 반환합니다. 방을 찾지 못하면 null.
    // RoomWideAttackDamageEffect(29번)와 동일한 조회 로직입니다.
    //
    // [형빈 확인 - LevelBuilder가 아예 없는 씬(ItemTestArenaBuilder 테스트 아레나) 폴백]
    // 테스트 아레나는 실제 레벨 생성 파이프라인을 거치지 않아 씬에 LevelBuilder 자체가
    // 없습니다(ItemTestArenaBuilder.cs 상단 주석 참고) - 즉 "방"이라는 개념이 아예
    // 없는 하나로 트인 공간입니다. 이 경우 CanActivate()가 항상 false를 반환해 31번을
    // 테스트 아레나에서 전혀 쓸 수 없었던 문제가 있어, LevelBuilder를 못 찾으면 "씬 전체를
    // 하나의 방으로" 취급해 씬에 있는 모든 EnemyHealth(테스트 아레나의 더미 적 포함)를
    // 대상으로 삼습니다. 실제 게임(LevelBuilder가 있는 씬)에서는 이 분기를 절대 타지
    // 않으므로 기존 동작은 전혀 바뀌지 않습니다. RoomWideAttackDamageEffect(29번)에도
    // 동일한 폴백을 추가했습니다.
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
