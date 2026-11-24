using UnityEngine;
using UnityEngine.Serialization;

// 패시브 59번 - 통칭 '아테나 3호'(Drone_Hare)
// "획득하면 플레이어를 뒤따라오는 드론을 얻는다. 적이 플레이어의 해킹툴 범위에 스스로 다가오면 해킹툴을 전개한다
//  (해킹툴 소모 없음, 쿨타임 15초)."
//
// [기획 - 형빈 확정]
// - 드론은 투명 → 흰색 발광 → 원래 색으로 나타나고(AppearFromWhite), 관성이 있는 것처럼 한 박자 늦게 따라오고 멈추며, 위아래로 웅웅 떠 있음.
// - 범위: 플레이어 해킹툴 범위의 75%(Range Multiplier) - 원래 범위가 커서 너무 자주 전개되던 것을 줄임(형빈 확정).
// - 따라오기 간격: 멈춰 있을 때 거리(Idle Distance)와 이동 중 최대 거리(Moving Max Distance)를 따로 둠(형빈 확정).
// - 전개 조건: 플레이어 해킹툴 범위(플레이어 기준) 안에 "자기 뜻으로 움직이는" 적이 있을 때. 해킹/수면 중, 매료 중(보스 제외 - 보스는
//   매료돼도 감속만 받음), 넉백으로 밀리는 중인 적, 87번 아군은 세지 않음. 91번 오한은 제어가 아니라 디버프라 셈.
// - 전개 = 플레이어 해킹툴과 같은 범위/피해/해킹/적 총알 제거, 중심은 플레이어. "해킹툴 사용"으로 쳐서 특수방 해제, 해킹 효과음,
//   2레벨 보스 보호막 추가 피해가 모두 적용되지만, 해킹툴을 소모하지 않으므로 58번 보호막은 생기지 않음(PlayerHackTool.DeployFromItem).
// - 연출: 기존 해킹툴 전개 이펙트 + 드론에 vfx_Shield_01(끝날 때 확 꺼지지 않고 서서히 투명해짐), 드론이 살짝 떠오르며 앞부분이 위를 봄.
// - 쿨타임 15초(전개 직후부터, 획득 직후에는 바로 가능). 쿨타임 동안 드론이 회색(38번 매료 색처럼 색만 바뀜), 쿨타임이 끝나는 순간부터
//   원래 색으로 서서히 돌아옴(15초 동안 애매하게 변하면 헷갈리므로).
// - 헬기 퇴각/도착 연출 때도 숨기지 않고 플레이어를 따라 올라가고 내려옴(플레이어 위치를 그대로 따라가므로 따로 처리하지 않음).
//
// [구현] 59번 고유 동작은 플레이어에 붙는 Item59DroneRunner, 드론 모습/따라가기/줄 서기는 패밀리어 공통 부품(FamiliarDrone/FamiliarChain)이
// 합니다(60번부터 공용 - 얻은 순서대로 줄을 섬, 형빈 확정). 이 클래스는 설정 칸 + 러너 붙이기/떼기만 합니다.
// 에셋의 효과 인스턴스는 공유되므로 런타임 상태는 전부 러너에 둡니다.
[System.Serializable]
public class HackDroneFamiliarEffect : IPassiveItemEffect, IRemovablePassiveEffect
{
    [Header("드론 모델")]
    [Tooltip("따라다닐 드론 모델입니다(Drone_Hare FBX를 그대로 넣어도 됩니다). 붙어 있는 콜라이더는 자동으로 지웁니다 - 총알/적과 부딪히지 않음.")]
    public GameObject droneModel;
    [Tooltip("모델 크기 배율입니다. 1이면 FBX 원래 크기입니다.")]
    [Min(0.01f)] public float modelScale = 1f;
    [Tooltip("모델의 앞부분이 진행 방향(앞)을 보도록 돌리는 보정 각도입니다(도). 드론이 옆이나 뒤를 보며 날면 Y를 90/180/-90으로 바꿔 보세요.")]
    public Vector3 modelRotationOffset = Vector3.zero;

    [Header("따라오기")]
    [Tooltip("앞 대상(플레이어 또는 줄의 앞 패밀리어)이 멈춰 있을 때 드론이 머무는 거리입니다(m, 뒤쪽 수평 거리). 너무 붙지 않게 하려면 키우세요.")]
    [FormerlySerializedAs("followDistance")]
    [Min(0f)] public float idleDistance = 0.45f;
    [Tooltip("앞 대상이 움직이는 동안 드론이 이보다 멀어지지 않습니다(m, 수평 거리). 따라오는 관성 때문에 벌어지는 간격의 상한입니다. " +
             "Idle Distance보다 작으면 이동 중엔 더 가까이 붙었다가 멈추면 Idle Distance까지 물러납니다.")]
    [Min(0f)] public float movingMaxDistance = 0.6f;
    [Tooltip("떠 있는 높이입니다(m, 플레이어 발 기준).")]
    public float hoverHeight = 0.5f;
    [Tooltip("따라오는 부드러움(초)입니다. 클수록 더 늦게, 더 미끄러지듯 따라오고 멈춥니다(관성 느낌).")]
    [Min(0.01f)] public float followSmoothTime = 0.3f;
    [Tooltip("따라올 때의 최대 속도입니다(m/초).")]
    [Min(0.1f)] public float maxFollowSpeed = 12f;
    [Tooltip("플레이어가 한 프레임에 이 거리(m) 이상 튀면(순간이동, 다음 레벨 등) 드론도 바로 뒤로 옮깁니다.")]
    [Min(0.5f)] public float snapDistance = 2f;
    [Tooltip("진행 방향으로 몸을 돌리는 속도입니다(도/초).")]
    [Min(1f)] public float turnSpeed = 540f;
    [Tooltip("움직일 때 진행 방향으로 앞으로 기우는 최대 각도입니다(도). 0이면 기울지 않습니다.")]
    [Range(0f, 45f)] public float leanAngle = 10f;
    [Tooltip("이 속도(m/초)로 날 때 최대로 기웁니다.")]
    [Min(0.1f)] public float leanFullSpeed = 3f;

    [Header("떠 있기 (위아래 웅웅)")]
    [Tooltip("위아래로 흔들리는 폭입니다(m). 0이면 흔들리지 않습니다.")]
    [Min(0f)] public float bobAmplitude = 0.03f;
    [Tooltip("위아래로 한 번 오르내리는 시간입니다(초).")]
    [Min(0.1f)] public float bobPeriod = 1.4f;

    [Header("나타남")]
    [Tooltip("획득할 때 투명 → 흰색 → 원래 색으로 나타나는 시간입니다(초).")]
    [Min(0.05f)] public float appearDuration = 0.6f;

    [Header("해킹툴 전개")]
    [Tooltip("59번의 범위 = 플레이어 해킹툴 범위 x 이 배율입니다(기본 0.75 = 75%). 해킹툴 범위가 아이템으로 커지면 함께 커집니다.")]
    [Range(0.1f, 2f)] public float rangeMultiplier = 0.75f;

    // 이 칸들이 생기기 전에 만든 59번 에셋은 새 칸이 0으로 들어오므로 기본값으로 읽습니다(0이면 범위 0 = 전개 안 됨, 간격 0 = 플레이어에 붙음).
    public float EffectiveRangeMultiplier => rangeMultiplier > 0f ? rangeMultiplier : 0.75f;
    public float EffectiveMovingMaxDistance => movingMaxDistance > 0f ? movingMaxDistance : 0.6f;
    [Tooltip("끄면(기본) 실제 해킹이 걸리는 범위(피해/해킹/적 총알 제거/특수방 해제, 전개 이펙트 크기)도 위 배율로 줄어듭니다. " +
             "켜면 \"적이 다가왔는지\" 확인할 때만 줄인 범위를 쓰고, 해킹은 플레이어 해킹툴과 같은 범위로 겁니다.")]
    public bool rangeOnlyForDetection = false;
    [Tooltip("전개 후 다시 전개할 수 있을 때까지의 쿨타임입니다(초). 전개 직후부터 돕니다.")]
    [Min(0f)] public float cooldown = 15f;
    [Tooltip("범위 안에 적이 들어왔는지 확인하는 간격입니다(초).")]
    [Min(0.02f)] public float checkInterval = 0.1f;

    [Header("전개 연출 - 드론 자세")]
    [Tooltip("전개할 때 위로 떠오르는 높이입니다(m).")]
    public float poseLiftHeight = 0.12f;
    [Tooltip("전개할 때 앞부분이 위로 들리는 각도입니다(도).")]
    [Range(0f, 80f)] public float poseNoseUpAngle = 25f;
    [Tooltip("떠올랐다가 제자리로 돌아오기까지 전체 시간입니다(초).")]
    [Min(0.1f)] public float poseDuration = 0.8f;

    [Header("전개 연출 - 드론 쉴드 이펙트")]
    [Tooltip("전개할 때 드론에 붙일 이펙트입니다(vfx_Shield_01). 비워두면 이펙트 없이 자세만 바뀝니다. 색/모양은 프리팹을 복제해서 수정하세요.")]
    public GameObject shieldVfxPrefab;
    [Tooltip("드론 중심에서 이펙트를 얼마나 옮길지입니다(m, 드론 기준).")]
    public Vector3 shieldVfxOffset = Vector3.zero;
    [Tooltip("이펙트 크기 배율입니다(파티클 크기/속도에 곱함). 1이면 프리팹 원래 크기입니다.")]
    [Min(0.01f)] public float shieldVfxScale = 1f;
    [Tooltip("이펙트를 이 시간(초) 뒤에 지웁니다. 0 이하면 지우지 않습니다(반복 이펙트면 반드시 넣으세요).")]
    public float shieldVfxLifetime = 1.5f;
    [Tooltip("이펙트가 지워지기 전 이 시간(초) 동안 서서히 투명해지며 사라집니다(Shield Vfx Lifetime 안에서 진행, 발광 없음). " +
             "0이면 기본값 0.5초로 처리합니다.")]
    [Min(0f)] public float shieldVfxFadeOutDuration = 0.5f;

    // 이 칸이 생기기 전에 만든 에셋은 0으로 들어오므로 기본값으로 읽습니다.
    public float EffectiveShieldFadeOut => shieldVfxFadeOutDuration > 0f ? shieldVfxFadeOutDuration : 0.5f;

    // 패밀리어 공통 부품에 넘길 움직임 값입니다(칸 이름은 그대로 두고 옮겨 담기만 함 - 이미 맞춘 에셋 값 유지).
    public FamiliarDrone.Motion BuildMotion()
    {
        return new FamiliarDrone.Motion
        {
            idleDistance = idleDistance,
            movingMaxDistance = EffectiveMovingMaxDistance,
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

    [Header("쿨타임 색")]
    [Tooltip("쿨타임 동안 드론 색에 곱할 색입니다(38번 매료 색처럼 색만 바뀜, 발광 없음). 흰색에 가까울수록 원래 색에 가깝습니다.")]
    public Color cooldownColor = new Color(0.45f, 0.45f, 0.48f, 1f);
    [Tooltip("전개 자세가 끝난 뒤 회색이 되기까지 걸리는 시간입니다(초).")]
    [Min(0f)] public float grayInDuration = 0.2f;
    [Tooltip("쿨타임이 끝나는 순간부터 원래 색으로 돌아오는 시간입니다(초).")]
    [Min(0f)] public float restoreDuration = 0.5f;

    public void Apply(PassiveEffectContext context, string sourceItemId)
    {
        if (context == null || context.PlayerObject == null) return;

        var runner = context.PlayerObject.GetComponent<Item59DroneRunner>();
        if (runner == null) runner = context.PlayerObject.AddComponent<Item59DroneRunner>();
        runner.Init(this);
    }

    // [패시브 잃기 - 48/56/62번] 드론은 사라지는 연출(DissolveAndVanish)과 함께 없어지고, 러너는 즉시 뗍니다
    // (같은 프레임에 다시 얻을 수 있어 DestroyImmediate - IRemovablePassiveEffect.cs 참고).
    public void Remove(PassiveEffectContext context, string sourceItemId)
    {
        if (context == null || context.PlayerObject == null) return;

        var runner = context.PlayerObject.GetComponent<Item59DroneRunner>();
        if (runner == null) return;

        runner.ReleaseDrone(true);
        Object.DestroyImmediate(runner);
    }
}
