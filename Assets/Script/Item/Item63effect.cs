using System.Collections.Generic;
using UnityEngine;

// 패시브 63번 - Drone_Shiroko 패밀리어(미사일 드론)
// "획득하면 플레이어 오른쪽 어깨 위를 따라다니는 드론을 얻는다. 10초마다 방 안의 적에게 유도 미사일 4발을 발사해 각각 공격력 50%의 피해를 준다."
//
// [기획 - 형빈 확정]
// - 자리: 줄을 서지 않고 플레이어가 바라보는 방향 기준 오른쪽 머리 위, 살짝 뒤(59/60번 줄은 그대로 플레이어 뒤에서 시작). 붕붕 떠 있기,
//   투명 → 흰빛으로 나타남, 잃을 때 흰빛으로 사라짐은 다른 패밀리어와 같음. 프로펠러(Object055/056)는 항상 회전.
// - 발사: 10초마다(발사 시작 기준) 4발을 왼쪽 → 오른쪽 → 왼쪽 → 오른쪽 포드(FirePoint_L/R - 형빈이 프리팹에 배치)에서 "투투투투" 순차 발사,
//   쏠 때마다 드론이 반동으로 앞뒤로 흔들림(연사 버스트처럼). 쿨타임 동안 색 변화 없음(자동 포탑).
// - 준비됐는데 방에 적이 없으면 기다렸다가 적이 생기면 바로 발사. 단, 방에 들어간 직후에는 적의 입장 공격 대기(entryAttackDelay 0.5초)처럼
//   Room Entry Fire Delay 동안 쏘지 않음.
// - 목표: 방 안의 모든 적 중 가까운 순(플레이어 기준, 같으면 체력 많은 순)으로 서로 다른 적을 최대 4명 고르고, 4발을 번갈아 나눔
//   (골고루 분배가 가장 중요 - 3명: 가까운 적이 2발, 2명: 2발씩, 1명: 4발 모두). 87번 아군 제외, 보스 포함.
// - 미사일: 드론 정면으로 잠깐 직진한 뒤 회전 반경을 가지고 꺾으며 목표를 쫓음(즉시 반대로 꺾지 않음). 자기 목표에만 맞고 벽은 무시.
//   피해 = 공격력 x 50%. 목표가 맞기 전에 죽으면 그 미사일에서 가장 가까운 적으로 다시 유도(없으면 그 자리에서 폭발 - 피해 없음).
//   명중 이펙트는 따로 지정.
// - [플레이어 총알과 같은 취급 - 형빈 확정] 미사일은 플레이어 총알(Projectile 미사일 모드)이라 총알 아이템 효과가 붙음:
//   미사일 수 = 4 x 연사(19/137번 갈래는 영향 없음). 38번 매료/91번 오한/102+109번 중독/4번 폭발/94번 후광/68번 넉백/118번 포격/23번 등 적용.
//   예외: 35번 튕김/99번 적 관통/120번 벽 관통 효과 없음, 11번 거리 보정 없음, 113번 슈가(중첩 쌓기/추가 피해) 없음,
//   5번 분열은 처음 미사일만 한 번(분열 미사일 8갈래는 잠깐 직진 후 각자 유도), 69번 튕김은 한 번만.
//   아이템 효과 배율 50%(5번 분열탄처럼): 4번 폭발 피해(미사일 피해 x 0.5)/범위, 94번 후광 반경, 68번 넉백, 118번 포격, 23번 랜덤 피해 폭.
//   피해 숫자는 "공격력 x 50%"를 기준으로 보통/Weak/Resist(23번으로 늘면 Weak, 분열/튕김으로 줄면 Resist).
// - 색: 총알 색 아이템(38번/23번 등)을 따라가고, 없으면 00FDFF. 별똥별 꼬리 없이 연기만(기본 밝은 회색~흰색, 색 아이템이 있으면 그 색).
// - 효과음: 63번 미사일 발사/명중 칸(SfxId)을 새로 만들고, 임시로 118번 포격 소리와 같은 클립을 넣어 둠.
//
// [구현] 63번 고유 동작은 Item63DroneRunner, 미사일 한 발은 Projectile(미사일 모드) + 유도 조종기 HomingMissile,
// 드론 모습/떠 있기/반동/프로펠러는 FamiliarDrone이 합니다.
[System.Serializable]
public class MissileDroneFamiliarEffect : IPassiveItemEffect, IRemovablePassiveEffect
{
    [Header("드론 모델")]
    [Tooltip("따라다닐 드론 모델입니다. FirePoint_L/FirePoint_R 빈 오브젝트를 미사일 포드 끝에 붙여 저장한 Drone_Shiroko 프리팹을 넣으세요.")]
    public GameObject droneModel;
    [Min(0.01f)] public float modelScale = 1.5f;
    [Tooltip("모델의 앞부분이 플레이어가 바라보는 방향을 보도록 돌리는 보정 각도입니다(도).")]
    public Vector3 modelRotationOffset = new Vector3(-90f, 0f, 0f);

    [Header("자리 (플레이어가 바라보는 방향 기준)")]
    [Tooltip("플레이어 발 기준 위치입니다(m). X = 오른쪽(+), Y = 위, Z = 앞(+)/뒤(-). 기본 = 오른쪽 머리 위, 살짝 뒤.")]
    public Vector3 anchorOffset = new Vector3(0.3f, 0.75f, -0.15f);
    [Tooltip("따라오는 부드러움(초)입니다. 클수록 더 늦게, 더 미끄러지듯 따라옵니다.")]
    [Min(0.01f)] public float followSmoothTime = 0.12f;
    [Min(0.1f)] public float maxFollowSpeed = 20f;
    [Tooltip("이동 중 관성 때문에 드론이 자리(Anchor Offset)에서 벌어질 수 있는 최대 수평 거리입니다(m). 작을수록 플레이어에 더 딱 붙어 따라옵니다. " +
             "0이면 기본값 0.15로 처리합니다.")]
    [Min(0f)] public float movingMaxDistance = 0.15f;
    [Tooltip("플레이어가 한 프레임에 이 거리(m) 이상 튀면(순간이동, 다음 레벨 등) 드론도 바로 그 자리로 옮깁니다.")]
    [Min(0.5f)] public float snapDistance = 2f;
    [Tooltip("플레이어가 바라보는 방향으로 몸을 돌리는 속도입니다(도/초).")]
    [Min(1f)] public float turnSpeed = 540f;
    [Tooltip("움직일 때 앞으로 기우는 최대 각도입니다(도).")]
    [Range(0f, 45f)] public float leanAngle = 8f;
    [Min(0.1f)] public float leanFullSpeed = 3f;

    [Header("떠 있기 (위아래 붕붕)")]
    [Min(0f)] public float bobAmplitude = 0.03f;
    [Min(0.1f)] public float bobPeriod = 1.4f;

    [Header("나타남")]
    [Min(0.05f)] public float appearDuration = 0.6f;

    [Header("프로펠러")]
    [Tooltip("계속 돌릴 부품 이름입니다(모델 Hierarchy에 보이는 이름 그대로).")]
    public List<string> propellerNames = new List<string> { "Object055", "Object056" };
    [Tooltip("각 프로펠러가 도는 축(그 부품의 로컬 축)입니다. 3ds Max의 위쪽 축 = Z(0, 0, 1). 엉뚱하게 돌면 (0, 1, 0)으로 바꿔 보세요.")]
    public Vector3 propellerAxis = new Vector3(0f, 0f, 1f);
    [Tooltip("프로펠러 회전 속도입니다(도/초). 음수면 반대로 돕니다.")]
    public float propellerSpeed = 1440f;

    [Header("발사 위치")]
    [Tooltip("왼쪽 미사일 포드 끝에 붙인 빈 오브젝트 이름입니다.")]
    public string firePointLeftName = "FirePoint_L";
    [Tooltip("오른쪽 미사일 포드 끝에 붙인 빈 오브젝트 이름입니다.")]
    public string firePointRightName = "FirePoint_R";
    [Tooltip("FirePoint를 찾지 못했을 때 쓸 드론 중심 기준 좌우 거리(m)입니다.")]
    [Min(0f)] public float fallbackSideOffset = 0.15f;

    [Header("발사")]
    [Tooltip("발사 간격(초)입니다. 발사를 시작한 순간부터 셉니다.")]
    [Min(0.1f)] public float volleyInterval = 10f;
    [Tooltip("한 번에 쏘는 기본 미사일 수입니다. 실제 수 = 이 값 x 연사(Multishot).")]
    [Min(1)] public int missilesPerVolley = 4;
    [Tooltip("미사일 사이 간격(초)입니다(투투투투).")]
    [Min(0f)] public float shotInterval = 0.1f;
    [Tooltip("미사일 한 발의 피해 = 공격력 x 이 배율입니다(기본 0.5 = 50%). 아이템 효과 배율(4번 폭발/94번 후광 등)과 피해 숫자 기준도 이 값입니다.")]
    [Min(0f)] public float damageMultiplier = 0.5f;
    [Tooltip("방에 들어간 직후 이 시간(초) 동안은 쏘지 않습니다(적의 입장 공격 대기 entryAttackDelay와 같은 값 - 기본 0.5).")]
    [Min(0f)] public float roomEntryFireDelay = 0.5f;

    [Header("발사 반동 (드론이 앞뒤로 흔들림)")]
    [Tooltip("한 발마다 앞이 들리는 각도입니다(도).")]
    public float recoilPitch = 8f;
    [Tooltip("한 발마다 뒤로 밀리는 거리입니다(m).")]
    public float recoilBack = 0.04f;
    [Tooltip("제자리로 돌아오는 빠르기입니다. 클수록 빨리 돌아옵니다.")]
    [Min(0.1f)] public float recoilRecoverSpeed = 12f;

    [Header("미사일")]
    [Tooltip("미사일 크기입니다(플레이어 총알 프리팹의 크기 값 - 총알 크기 스탯과 같은 단위).")]
    [Min(0.005f)] public float missileSize = 0.06f;
    [Tooltip("총알 색 아이템(38번/23번 등)이 없을 때의 미사일 색입니다(기본 00FDFF). 모양/재질은 플레이어 총알 프리팹을 따릅니다.")]
    public Color missileColor = new Color(0f, 0.9921569f, 1f, 1f);
    [Tooltip("날아가는 속도입니다(m/초).")]
    [Min(0.1f)] public float missileSpeed = 4f;
    [Tooltip("발사 직후 드론 정면으로 직진하는 시간(초)입니다. 그 뒤 유도가 시작됩니다.")]
    [Min(0f)] public float straightDuration = 0.2f;
    [Tooltip("유도할 때 꺾는 속도입니다(도/초). 작을수록 회전 반경이 큽니다.")]
    [Min(1f)] public float turnRate = 240f;
    [Tooltip("유도 시간이 지날수록 꺾는 속도가 초당 이만큼 늘어납니다(목표 주위를 계속 맴도는 것 방지).")]
    [Min(0f)] public float turnRateGrowth = 360f;
    [Tooltip("이 시간(초)이 지나도 맞히지 못하면 그 자리에서 폭발합니다(피해 없음).")]
    [Min(0.5f)] public float maxFlightTime = 4f;
    [Tooltip("목표 중심에서 이 거리(m) 안에 들어오면 콜라이더가 겹치지 않아도 명중입니다.")]
    [Min(0.01f)] public float hitRadius = 0.15f;
    [Tooltip("켜면 목표가 맞기 전에 죽었을 때 그 미사일에서 가장 가까운 적에게 다시 유도합니다(형빈 확정). 끄면 그 자리에서 폭발합니다.")]
    public bool retargetOnTargetDeath = true;

    [Header("미사일 꼬리 연기")]
    [Tooltip("꼬리에 남길 연기 이펙트입니다. 비우면 뛰기 흙먼지 이펙트(TravelBoostSettings의 Run Dust Prefab)를 씁니다.")]
    public GameObject smokePrefab;
    [Tooltip("연기 색 범위입니다(파티클마다 두 색 사이에서 고름 - 밝은 회색~흰색). 총알 색 아이템이 있으면 그 색으로 바뀝니다.")]
    public Color smokeColorA = new Color(0.82f, 0.82f, 0.84f, 1f);
    public Color smokeColorB = new Color(1f, 1f, 1f, 1f);
    [Tooltip("연기를 남기는 간격(초)입니다.")]
    [Min(0.01f)] public float smokeInterval = 0.03f;
    [Tooltip("연기 크기 배율입니다(Transform 크기).")]
    [Min(0.01f)] public float smokeScale = 0.5f;
    [Tooltip("연기를 이 시간(초) 뒤에 지웁니다.")]
    [Min(0.1f)] public float smokeLifetime = 0.8f;

    [Header("명중 이펙트")]
    [Tooltip("명중(또는 목표가 없어 폭발)할 때 생성할 이펙트입니다.")]
    public GameObject hitVfxPrefab;
    [Tooltip("명중 이펙트 크기 배율입니다(파티클 크기/속도에 곱함).")]
    [Min(0.01f)] public float hitVfxScale = 1f;
    [Tooltip("명중 이펙트를 이 시간(초) 뒤에 지웁니다.")]
    [Min(0.1f)] public float hitVfxLifetime = 1.5f;

    // 이 칸이 생기기 전에 만든 에셋은 0으로 들어오므로 기본값으로 읽습니다.
    public float EffectiveMovingMaxDistance => movingMaxDistance > 0f ? movingMaxDistance : 0.15f;

    public FamiliarDrone.Motion BuildMotion()
    {
        return new FamiliarDrone.Motion
        {
            hoverHeight = 0f, // 높이는 Anchor Offset Y로 정함
            followSmoothTime = followSmoothTime,
            maxFollowSpeed = maxFollowSpeed,
            snapDistance = snapDistance,
            turnSpeed = turnSpeed,
            leanAngle = leanAngle,
            leanFullSpeed = leanFullSpeed,
            bobAmplitude = bobAmplitude,
            bobPeriod = bobPeriod,
            poseDuration = 0.5f,
        };
    }

    public void Apply(PassiveEffectContext context, string sourceItemId)
    {
        if (context == null || context.PlayerObject == null) return;

        var runner = context.PlayerObject.GetComponent<Item63DroneRunner>();
        if (runner == null) runner = context.PlayerObject.AddComponent<Item63DroneRunner>();
        runner.Init(this);
    }

    // [패시브 잃기 - 48/56/62번] 드론은 흰빛으로 물들며 사라지고, 러너는 즉시 뗍니다(이미 날아간 미사일은 끝까지 날아감).
    public void Remove(PassiveEffectContext context, string sourceItemId)
    {
        if (context == null || context.PlayerObject == null) return;

        var runner = context.PlayerObject.GetComponent<Item63DroneRunner>();
        if (runner == null) return;

        runner.ReleaseDrone(true);
        Object.DestroyImmediate(runner);
    }
}
