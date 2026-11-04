using UnityEngine;

// 액티브 74번 - "누르고 있는 방향으로 짧게 돌진한다. 돌진하는 동안 적의 접촉 피해, 총알, 수류탄 자해 피해를 회피한다.
// 전투 중이 아닐 때는 코스트를 소모하지 않는다. 3초에 한 번만 사용할 수 있다."
//
// [기획 - 형빈 확정]
// - 코스트 1(ItemDefinition의 Active Cost = 1). 전투 중이 아닐 때는 코스트 없이 사용합니다(좌상단 소모 코스트 숫자도 0으로 보임).
//   "전투 중" = 지금 있는 방이 클리어 전인 전투방(일반방/보스방)일 때입니다. 56번/80번과 같은 기준(SlotItemDropper.
//   IsInCombatFreeRoom)을 그대로 쓰므로, 나중에 전투가 있는 특수방이 생겨 그 방이 클리어 조건(RequiresClear)을 갖게 되면
//   자동으로 "전투 중"에 포함됩니다.
// - 방향: 방향키를 누르고 있으면 그 방향, 안 누르고 있으면 플레이어가 바라보는 방향으로 돌진합니다.
// - 돌진 시간 0.25초, 거리 0.5(아래 칸에서 조절). 벽에는 막히고 적은 통과합니다. 돌진 중에도 사격/수류탄/해킹툴 사용 가능.
// - 돌진하는 동안만 적 접촉 피해/적 총알(통과)/수류탄 자해(81번 불 구역 포함)를 회피합니다. 체력 소모 특수방 출입 피해는
//   그대로 받습니다. 끝난 뒤 적 몸 안이나 불 구역 안이면 평소처럼 피해를 받습니다(돌진/회피 처리는 PlayerDash.cs 참고).
// - 쿨타임: 어떤 경우든 "지금 플레이어 장전 시간 x 2"(기본 장전 1.5초 -> 3초, 장전 1초 -> 2초 - 형빈 확정). 장전 시간은
//   PlayerStats.ReloadTime(아이템 효과가 반영된 장전 시간 스탯)을 돌진을 시작하는 순간에 읽습니다. 돌진을 "시작한" 순간부터 셉니다. 아이템을 교체했다가 다시 얻어도 쿨타임은 이어집니다
//   (쿨타임을 아이템 효과 객체가 아니라 정적 값으로 기억하기 때문). 쿨타임 중에는 스페이스를 눌러도 아무 일도 없고 코스트도
//   소모되지 않습니다. 좌상단 액티브 아이콘이 쿨타임 동안 어두워졌다가 시계방향으로 밝아집니다(PlayerCostDisplay).
// - 전투 중 코스트가 부족하면 사용되지 않습니다(쿨타임도 돌지 않음).
// - 잔상: 돌진하는 동안 일정 간격으로 플레이어 모델의 그 순간 모습을 반투명 단색으로 남기고, 금방 투명해지며 사라집니다
//   (아래 "잔상" 칸, DashAfterimage.cs 참고).
//
// [아이템 에셋 설정]
// ItemDefinition: Item Id = 74, Category = Active, Active Cost = 1, Active Effect에 DashActiveEffect를 고릅니다.
[System.Serializable]
public class DashActiveEffect : IActiveItemUseRules
{
    [Header("돌진")]
    [Tooltip("돌진 거리입니다(월드 단위).")]
    [Min(0.01f)] public float dashDistance = 0.5f;

    [Tooltip("돌진에 걸리는 시간(초)입니다. 회피도 정확히 이 시간 동안만 유지됩니다.")]
    [Min(0.01f)] public float dashDuration = 0.25f;

    [Header("쿨타임 / 코스트")]
    [Tooltip("쿨타임 = 지금 플레이어 장전 시간(PlayerStats.ReloadTime) x 이 배율입니다. 기본 2배(장전 1.5초 -> 쿨타임 3초). " +
             "돌진을 시작한 순간의 장전 시간으로 정하고, 그 순간부터 셉니다.")]
    [Min(0f)] public float cooldownReloadMultiplier = 2f;

    [Tooltip("켜면 전투 중이 아닐 때 코스트 없이 사용합니다.")]
    public bool freeOutsideCombat = true;

    [Header("잔상")]
    public DashAfterimageSettings afterimage = new DashAfterimageSettings();

    // 장전 시간 스탯을 못 찾았을 때(PlayerStats 없음) 쓰는 기본 장전 시간입니다(기본값 1.5초).
    private const float FallbackReloadTime = 1.5f;

    // 쿨타임이 끝나는 시각(Time.time 기준)입니다. 교체 후 다시 얻어도 이어지도록 정적 값으로 둡니다. 쿨타임 길이는 "마지막으로
    // 사용한 순간의 cooldown 값"을 같이 기억해서, 진행률 표시가 그 사이 Inspector 값을 바꿔도 튀지 않게 합니다.
    private static float readyTime;
    private static float lastCooldownLength;

    // 매 프레임 여러 번(입력 확인 + HUD 숫자) 불려도 방 검색을 한 번만 하도록 프레임 단위로 기억합니다.
    private static int cachedFrame = -1;
    private static bool cachedCombatFree;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        readyTime = 0f;
        lastCooldownLength = 0f;
        cachedFrame = -1;
        cachedCombatFree = false;
    }

    public float CooldownRemaining01
    {
        get
        {
            if (lastCooldownLength <= 0f) return 0f;
            return Mathf.Clamp01((readyTime - Time.time) / lastCooldownLength);
        }
    }

    public bool CanActivateNow(ActiveEffectContext context)
    {
        if (context == null || context.PlayerObject == null) return false;
        if (context.Health != null && context.Health.IsDead) return false;
        if (Time.time < readyTime) return false;

        // 로딩 화면/밀려나는 중에는 이동 자체가 막혀 있으므로 돌진도 하지 않습니다(코스트도 소모 X).
        if (LoadingScreen.BlocksPlayerInput || ForcedPush.BlocksPlayerInput) return false;

        var controller = context.PlayerObject.GetComponent<CharacterController>();
        if (controller == null || !controller.enabled) return false;

        var dash = context.PlayerObject.GetComponent<PlayerDash>();
        if (dash != null && dash.IsDashing) return false;

        return true;
    }

    public int ResolveUseCost(ActiveEffectContext context, int effectiveCost)
    {
        if (freeOutsideCombat && IsCombatFree()) return 0;
        return effectiveCost;
    }

    public void Activate(ActiveEffectContext context)
    {
        if (context == null || context.PlayerObject == null) return;

        var dash = PlayerDash.GetOrAdd(context.PlayerObject);
        if (dash == null)
        {
            Debug.LogWarning("DashActiveEffect: 플레이어에 CharacterController가 없어 돌진할 수 없습니다.");
            return;
        }

        Vector3 direction = GetDashDirection(context.PlayerObject);
        if (dash.StartDash(direction, dashDistance, dashDuration, afterimage))
        {
            // 돌진을 시작한 순간부터 쿨타임을 셉니다. 길이 = 지금 장전 시간 x 배율.
            float reloadTime = context.Stats != null ? context.Stats.ReloadTime : FallbackReloadTime;
            float cooldown = Mathf.Max(0f, reloadTime) * cooldownReloadMultiplier;
            lastCooldownLength = cooldown;
            readyTime = Time.time + cooldown;
        }
    }

    // 방향키를 누르고 있으면 그 방향(PlayerMovement가 카메라 기준으로 계산한 이동 방향), 아니면 플레이어가 바라보는 방향입니다.
    private static Vector3 GetDashDirection(GameObject player)
    {
        var movement = player.GetComponent<PlayerMovement>();
        if (movement != null)
        {
            Vector3 input = movement.GetMoveInput();
            input.y = 0f;
            if (input.sqrMagnitude > 0.0001f) return input.normalized;
        }

        Vector3 forward = player.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
        return forward.normalized;
    }

    private static bool IsCombatFree()
    {
        if (cachedFrame != Time.frameCount)
        {
            cachedFrame = Time.frameCount;
            cachedCombatFree = SlotItemDropper.IsInCombatFreeRoom();
        }
        return cachedCombatFree;
    }
}
