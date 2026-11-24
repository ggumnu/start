using System.Collections.Generic;
using UnityEngine;

// 액티브 108번 "Drone_Umika" - "코스트 5를 소모해 플레이어 앞에 폭죽 포대를 설치한다. 포대는 5초 동안 그 자리에 있으며, 설치 0.5초 뒤부터
// 1초마다 가장 먼 적에게 폭죽탄 4발을 쏜다(수류탄 피해/폭발 반경의 25%)."
//
// [기획 - 형빈 확정]
// - 코스트 5(에셋의 Active Cost - 45번 등 코스트 감소 적용). 사용하면 플레이어 앞쪽(Spawn Forward Distance)에 바닥에 붙은 고정 포대가 하얗게
//   빛나며 나타납니다. 포대는 움직이지 않고 떠 있거나 흔들리지 않습니다. 플레이어/적이 통과할 수 있고 파괴되지 않습니다(콜라이더 없음).
// - 시간: 0.5/1.5/2.5/3.5/4.5초에 한 번씩(5번) 폭죽탄 4발을 쏘고, 5초가 되면 하얗게 빛나며 투명해져 사라집니다.
// - 목표: 포대에서 가장 먼 적(보스 포함, 87번 아군만 제외 - 매혹된 적도 공격). 한 번 정한 목표는 처치될 때까지 유지하고("락온"),
//   처치되면 그 순간 가장 먼 적으로 바꿉니다. 포대는 항상 목표를 향해 돌고, 포구(Object42)는 포물선 발사 각도에 맞춰 위아래로 기웁니다.
// - 적이 없고 지금 방에 잠긴 수류탄 특수방 바리케이드가 있으면 그 바리케이드를 쏩니다(적이 우선). 폭죽탄으로 바리케이드가 풀립니다.
// - 폭죽탄 = 진짜 수류탄(겉모습만 폭죽): 피해 = 쏘는 순간의 수류탄 피해 x 25%, 폭발 반경 x 25%, 자해 0.5 고정(57번이면 없음).
//   77/81/92/115번, 57/66번, 특수방 잠금 해제가 모두 일반 수류탄처럼 적용됩니다. 수류탄 개수는 쓰지 않습니다. 착탄 지점은 목표 주변에 살짝 흩뿌립니다.
// - 플레이어가 방을 나가면 포대는 곧바로 사라집니다(방이 클리어되어도 플레이어가 그 방에 있으면 시간이 다 될 때까지 남음). 이미 날아가던
//   폭죽탄은 수류탄 방 전환 규칙대로 그 방에서 조용히 터져 그 방의 바리케이드를 풀 수 있습니다.
// - 사용 조건: 지금 방에 공격할 적이 있거나, 잠긴 수류탄 특수방 바리케이드가 있어야 합니다(둘 다 없으면 코스트도 소모 X).
//   코스트만 있으면 여러 대를 설치할 수 있습니다.
//
// [에디터에서 할 일] ItemDefinition: Item Id = 108, Category = Active, Active Cost = 5, Active Effect = FireworkTurretActiveEffect.
// Turret Model에 FirePoint 4개를 붙인 Drone_Umika 프리팹(베리언트)을 넣고, 폭죽 겉모습의 Spark Template에 vfx_Sparks_01을 넣으세요.
[System.Serializable]
public class FireworkTurretActiveEffect : IActiveItemUseRules
{
    [Header("포대 모델")]
    [Tooltip("설치할 포대 모델(Drone_Umika 프리팹 - FirePoint 4개를 붙인 베리언트)입니다.")]
    public GameObject turretModel;

    [Min(0.01f)] public float modelScale = 1f;

    [Tooltip("모델 방향 보정(도)입니다. 조준은 FirePoint 방향을 기준으로 하므로 보통 0이면 됩니다.")]
    public Vector3 modelRotationOffset = Vector3.zero;

    [Tooltip("플레이어 발밑에서 바라보는 방향으로 이만큼(m) 앞에 설치합니다(방 밖이면 방 안쪽으로 당김).")]
    [Min(0f)] public float spawnForwardDistance = 1f;

    [Header("시간")]
    [Tooltip("포대가 있는 시간(초)입니다. 이 시간이 되면 하얗게 빛나며 사라집니다.")]
    [Min(0.1f)] public float lifetime = 5f;

    [Tooltip("설치 후 첫 발사까지의 시간(초)입니다.")]
    [Min(0f)] public float firstFireDelay = 0.5f;

    [Tooltip("발사 간격(초)입니다. 기본값이면 0.5/1.5/2.5/3.5/4.5초에 쏩니다.")]
    [Min(0.05f)] public float volleyInterval = 1f;

    [Tooltip("하얗게 빛나며 나타나는 시간(초)입니다.")]
    [Min(0.01f)] public float appearDuration = 0.4f;

    [Tooltip("하얗게 빛나며 투명해져 사라지는 시간(초)입니다.")]
    [Min(0.01f)] public float vanishDuration = 0.5f;

    [Header("발사")]
    [Tooltip("위아래로 기울고 반동하는 포구 부품 이름입니다.")]
    public string muzzlePartName = "Object42";

    [Tooltip("발사 위치 빈 오브젝트 이름들입니다(이 순서로 한 발씩). 파란 화살표(Z)가 포구 밖을 향하게 돌려 두세요.")]
    public List<string> firePointNames = new List<string> { "FirePoint_1", "FirePoint_2", "FirePoint_3", "FirePoint_4" };

    [Tooltip("한 번에 쏘는 폭죽탄 수입니다.")]
    [Min(1)] public int shotsPerVolley = 4;

    [Tooltip("한 번에 쏘는 폭죽탄 사이의 간격(초) - '투투투투'.")]
    [Min(0f)] public float shotInterval = 0.08f;

    [Header("폭죽탄 성능")]
    [Tooltip("피해 = 쏘는 순간의 수류탄 피해 x 이 값입니다.")]
    [Min(0f)] public float damageRatio = 0.25f;

    [Tooltip("폭발 반경 = 쏘는 순간의 수류탄 폭발 반경 x 이 값입니다.")]
    [Min(0f)] public float radiusRatio = 0.25f;

    [Tooltip("범위 안에 있는 플레이어가 받는 자해 피해(고정)입니다. 57번 등 자해 무효 아이템이 있으면 받지 않습니다.")]
    [Min(0f)] public float selfDamage = 0.5f;

    [Tooltip("발사 후 착탄(=폭발)까지의 시간(초)입니다.")]
    [Min(0.05f)] public float flightTime = 0.7f;

    [Tooltip("포물선 최고점 높이(m)입니다. 포구가 기우는 각도도 이 포물선에 맞춰 정해집니다.")]
    [Min(0f)] public float arcHeight = 1.2f;

    [Tooltip("착탄 지점을 목표 주변 이 반경(m) 안에 흩뿌립니다(바리케이드를 쏠 때는 흩뿌리지 않음).")]
    [Min(0f)] public float scatterRadius = 0.35f;

    [Header("조준 회전")]
    [Tooltip("포대가 목표 쪽으로 도는 속도(도/초)입니다.")]
    [Min(1f)] public float turnSpeed = 540f;

    [Tooltip("포구가 위아래로 기우는 속도(도/초)입니다.")]
    [Min(1f)] public float pitchSpeed = 360f;

    [Tooltip("포구가 기울 수 있는 범위(도, 수평 = 0, 위쪽 +)입니다.")]
    public float minPitch = -5f;
    public float maxPitch = 80f;

    [Header("반동")]
    [Tooltip("한 발마다 포구가 뒤로 밀리는 거리(m)입니다.")]
    [Min(0f)] public float kickDistance = 0.05f;

    [Tooltip("밀린 포구가 제자리로 돌아오는 빠르기입니다(클수록 빨리).")]
    [Min(0.1f)] public float kickRecoverSpeed = 14f;

    [Tooltip("4발을 다 쏜 뒤 포구가 위로 들리는 각도(도)입니다.")]
    [Min(0f)] public float nodAngle = 12f;

    [Tooltip("포구가 들리는 시간(초)입니다.")]
    [Min(0.01f)] public float nodRiseTime = 0.06f;

    [Tooltip("들린 포구가 내려오는 시간(초)입니다.")]
    [Min(0.01f)] public float nodFallTime = 0.3f;

    [Header("폭죽 겉모습")]
    public FireworkStyle firework = new FireworkStyle();

    // [사용 조건] 쿨타임 없음, 코스트는 평소와 같음.
    public float CooldownRemaining01 => 0f;

    public int ResolveUseCost(ActiveEffectContext context, int effectiveCost) => effectiveCost;

    public bool CanActivateNow(ActiveEffectContext context)
    {
        if (context == null || context.PlayerObject == null) return false;
        if (context.Health != null && context.Health.IsDead) return false;
        if (LoadingScreen.BlocksPlayerInput) return false;
        if (context.PlayerObject.GetComponent<PlayerGrenadeThrower>() == null) return false;
        return FireworkTurret.HasAnyTargetInPlayerRoom(context.PlayerTransform.position);
    }

    public void Activate(ActiveEffectContext context)
    {
        if (context == null || context.PlayerObject == null) return;
        var thrower = context.PlayerObject.GetComponent<PlayerGrenadeThrower>();
        if (thrower == null)
        {
            Debug.LogWarning("FireworkTurretActiveEffect: 플레이어에 PlayerGrenadeThrower가 없어 108번 포대를 설치하지 못했습니다.");
            return;
        }
        FireworkTurret.Spawn(this, thrower, context.PlayerTransform);
    }
}
