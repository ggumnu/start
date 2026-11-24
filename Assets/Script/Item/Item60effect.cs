using System.Collections.Generic;
using UnityEngine;

// 패시브 60번 - Drone_Cherino 패밀리어
// "획득하면 플레이어를 뒤따라오는 드론을 얻는다. 플레이어가 피격될 때(체력 또는 보호막이 깎일 때) 25% 확률로 보호막을 1개 얻는다."
//
// [기획 - 형빈 확정]
// - 59번과 같은 패밀리어: 투명 → 흰색 발광 → 원래 색으로 나타나고, 관성 있게 따라오며 위아래로 붕붕 떠 있음.
//   아이작 리펜던스처럼 얻은 순서대로 줄을 섬(59번 먼저면 플레이어 → 59번 → 60번) - FamiliarChain.
// - 확률 판정: 체력이 깎일 때뿐 아니라 보호막이 깨질 때도 굴림(= 피격될 때마다). 적 공격/수류탄·118번 포격 자해/체력 특수방 등 모든 피해 포함.
//   피해로 사망했으면 굴리지 않음. 쿨타임 없음(피격 무적 시간 때문에 연속 발동은 자연히 막힘). 확률은 LuckOdds(나중에 행운 반영).
// - 발동 연출: 기존 보호막 획득 이펙트(플레이어에서) + Cherino가 59번처럼 떠오르며 앞이 들림 + Cherino 뒤에 노란 발광/입자
//   (보물상자 아이템 빛과 같은 3D 이펙트 - UI 연출은 드론 뒤에 놓을 수 없어 3D로, 형빈 확정 A안).
// - 잃을 때(48/56/62번 등): 흰빛으로 물들며 사라짐(다른 오브젝트가 사라질 때와 같은 방식).
//
// [구현] 60번 고유 동작은 플레이어에 붙는 Item60DroneRunner, 드론 모습/따라가기/줄 서기는 FamiliarDrone/FamiliarChain이 합니다.
[System.Serializable]
public class ShieldDroneFamiliarEffect : IPassiveItemEffect, IRemovablePassiveEffect
{
    [Header("드론 모델")]
    [Tooltip("따라다닐 드론 모델입니다(Drone_Cherino FBX를 그대로 넣어도 됩니다). 붙어 있는 콜라이더는 자동으로 지웁니다.")]
    public GameObject droneModel;
    [Tooltip("모델 크기 배율입니다. 1이면 FBX 원래 크기입니다.")]
    [Min(0.01f)] public float modelScale = 1.5f;
    [Tooltip("모델의 앞부분이 진행 방향을 보도록 돌리는 보정 각도입니다(도). 59번(Drone_Hare)은 X -90이었습니다.")]
    public Vector3 modelRotationOffset = new Vector3(-90f, 0f, 0f);

    [Header("따라오기 (앞 대상 = 플레이어 또는 줄의 앞 패밀리어)")]
    [Tooltip("앞 대상이 멈춰 있을 때 머무는 거리입니다(m, 뒤쪽 수평 거리).")]
    [Min(0f)] public float idleDistance = 0.45f;
    [Tooltip("앞 대상이 움직이는 동안 이보다 멀어지지 않습니다(m, 수평 거리).")]
    [Min(0.01f)] public float movingMaxDistance = 0.6f;
    [Tooltip("떠 있는 높이입니다(m, 플레이어 발 기준).")]
    public float hoverHeight = 0.5f;
    [Tooltip("따라오는 부드러움(초)입니다. 클수록 더 늦게, 더 미끄러지듯 따라오고 멈춥니다.")]
    [Min(0.01f)] public float followSmoothTime = 0.15f;
    [Tooltip("따라올 때의 최대 속도입니다(m/초).")]
    [Min(0.1f)] public float maxFollowSpeed = 20f;
    [Tooltip("플레이어가 한 프레임에 이 거리(m) 이상 튀면(순간이동, 다음 레벨 등) 드론도 바로 뒤로 옮깁니다.")]
    [Min(0.5f)] public float snapDistance = 2f;
    [Tooltip("진행 방향으로 몸을 돌리는 속도입니다(도/초).")]
    [Min(1f)] public float turnSpeed = 540f;
    [Tooltip("움직일 때 앞으로 기우는 최대 각도입니다(도). 0이면 기울지 않습니다.")]
    [Range(0f, 45f)] public float leanAngle = 10f;
    [Tooltip("이 속도(m/초)로 날 때 최대로 기웁니다.")]
    [Min(0.1f)] public float leanFullSpeed = 3f;

    [Header("떠 있기 (위아래 붕붕)")]
    [Min(0f)] public float bobAmplitude = 0.03f;
    [Min(0.1f)] public float bobPeriod = 1.4f;

    [Header("나타남")]
    [Min(0.05f)] public float appearDuration = 0.6f;

    [Header("보호막 발동")]
    [Tooltip("피격될 때마다 보호막을 얻을 확률입니다(0~1, 기본 0.25 = 25%).")]
    [Range(0f, 1f)] public float shieldChance = 0.25f;
    [Tooltip("발동 시 얻는 보호막 개수입니다(최대 보호막을 넘으면 버려짐).")]
    [Min(1)] public int shieldAmount = 1;

    [Header("발동 연출 - 드론 자세")]
    public float poseLiftHeight = 0.12f;
    [Range(0f, 80f)] public float poseNoseUpAngle = 25f;
    [Min(0.1f)] public float poseDuration = 0.8f;

    [Header("발동 연출 - 드론 뒤 발광/입자")]
    [Tooltip("발광/입자를 물들일 색입니다(기본 = 등급 Tier2 노랑).")]
    public Color procColor = new Color(1f, 0.92156863f, 0.015686275f, 1f);
    [Tooltip("드론 뒤에 겹쳐 띄울 이펙트들입니다(보물상자 아이템 빛과 같은 항목). 예: 1번 = vfx_LootDrop_02(발광), 2번 = vfx_Projectile_01(흩날리는 입자, " +
             "Disabled Child Names에 \"Particle System\").")]
    public List<FamiliarProcVfxLayer> procVfxLayers = new List<FamiliarProcVfxLayer>
    {
        new FamiliarProcVfxLayer { scale = 0.5f, pushBehindDistance = 0.15f },
        new FamiliarProcVfxLayer
        {
            scale = 1f, worldOffset = new Vector3(0f, 0.1f, 0f), pushBehindDistance = 0.15f,
            emissionRateMultiplier = 2f, particleSizeMultiplier = 0.3f,
            disabledChildNames = new List<string> { "Particle System" },
        },
    };
    [Tooltip("발광/입자를 이 시간(초) 뒤에 지웁니다.")]
    [Min(0.1f)] public float procVfxLifetime = 1.2f;
    [Tooltip("지워지기 전 이 시간(초) 동안 서서히 투명해집니다(Proc Vfx Lifetime 안에서 진행).")]
    [Min(0f)] public float procVfxFadeOutDuration = 0.5f;

    public FamiliarDrone.Motion BuildMotion()
    {
        return new FamiliarDrone.Motion
        {
            idleDistance = idleDistance,
            movingMaxDistance = movingMaxDistance,
            hoverHeight = hoverHeight,
            followSmoothTime = followSmoothTime,
            maxFollowSpeed = maxFollowSpeed,
            snapDistance = snapDistance,
            turnSpeed = turnSpeed,
            leanAngle = leanAngle,
            leanFullSpeed = leanFullSpeed,
            bobAmplitude = bobAmplitude,
            bobPeriod = bobPeriod,
            poseLiftHeight = poseLiftHeight,
            poseNoseUpAngle = poseNoseUpAngle,
            poseDuration = poseDuration,
        };
    }

    public void Apply(PassiveEffectContext context, string sourceItemId)
    {
        if (context == null || context.PlayerObject == null) return;

        var runner = context.PlayerObject.GetComponent<Item60DroneRunner>();
        if (runner == null) runner = context.PlayerObject.AddComponent<Item60DroneRunner>();
        runner.Init(this);
    }

    // [패시브 잃기 - 48/56/62번] 드론은 흰빛으로 물들며 사라지고(DissolveAndVanish), 러너는 즉시 뗍니다
    // (같은 프레임에 다시 얻을 수 있어 DestroyImmediate - IRemovablePassiveEffect.cs 참고).
    public void Remove(PassiveEffectContext context, string sourceItemId)
    {
        if (context == null || context.PlayerObject == null) return;

        var runner = context.PlayerObject.GetComponent<Item60DroneRunner>();
        if (runner == null) return;

        runner.ReleaseDrone(true);
        Object.DestroyImmediate(runner);
    }
}
