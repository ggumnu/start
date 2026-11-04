using UnityEngine;

// 플레이어의 짧은 돌진(대시)입니다. 지금은 74번 액티브(Item74effect.cs - DashActiveEffect)가 씁니다.
// 74번을 처음 사용할 때 플레이어에 자동으로 붙으므로(GetOrAdd), 플레이어 프리팹에 미리 넣을 필요가 없습니다.
//
// [이동 - 형빈 확정]
// - 정해진 방향으로 dashDuration(초) 동안 dashDistance만큼 일정한 속도로 움직입니다. 돌진 중에는 방향을 바꿀 수 없습니다.
// - 평소 이동과 똑같이 CharacterController.Move로 움직이므로, 벽/바리케이드/장애물/투명 이동 제한 벽에는 평소처럼 막힙니다
//   (벽을 넘을 수 없음). 비스듬히 부딪히면 평소 이동처럼 벽을 따라 미끄러집니다.
// - 적 몸통은 원래부터 플레이어와 물리 충돌이 꺼져 있어(Enemy 레이어 - EnemyMeleeContactTrigger.cs 주석 참고) 그대로 통과합니다.
// - 돌진 중에는 PlayerMovement가 이동 입력을 무시합니다(BlocksPlayerInput). 중력은 그대로 적용됩니다.
// - 사격/수류탄/해킹툴은 이 스크립트와 무관하게 평소처럼 쓸 수 있습니다.
//
// [회피 - IsDodging]
// 돌진하는 동안(정확히 dashDuration 동안)만 true입니다. 아래 피해들은 이 값을 확인해서 true면 피해 처리를 건너뜁니다.
// - 적 접촉 피해(EnemyMeleeContactTrigger) - 몸통 박치기/돌진 공격/접촉형 적 전부
// - 적 총알(EnemyProjectile) - 맞지 않고 그대로 통과해 계속 날아감
// - 수류탄 자해(Grenade.DamageOwner) + 81번 소이탄 불 구역 피해(IncendiaryFireZone)
// PlayerHealth.TakeDamage 자체는 막지 않으므로, 체력 소모 특수방 출입 피해 등 나머지 피해는 돌진 중에도 그대로 들어옵니다.
// 돌진이 끝난 순간 적 몸 안이나 불 구역 안에 있으면 그때부터는 평소처럼 피해를 받습니다(추가 무적 없음 - 형빈 확정).
//
// [잔상]
// StartDash에 잔상 설정(DashAfterimageSettings)을 넘기면, 돌진을 시작하는 순간 한 번 + 돌진하는 동안 Spawn Interval마다 플레이어
// 모델의 그 순간 모습을 잔상으로 남깁니다(DashAfterimage.cs). 잔상은 이동/판정과 완전히 별개인 연출이라, 잔상에서 오류가 나도
// 돌진 자체는 그대로 진행됩니다.
[RequireComponent(typeof(CharacterController))]
public class PlayerDash : MonoBehaviour
{
    // 지금 돌진 중인 플레이어가 있으면 true입니다(회피 판정용). 플레이어는 한 명이라 정적 값으로 둡니다.
    public static bool IsDodging => activeDashes > 0;

    // PlayerMovement가 이 값이 true인 동안 이동 입력을 무시합니다(ForcedPush.BlocksPlayerInput과 같은 방식).
    public static bool BlocksPlayerInput => activeDashes > 0;

    private static int activeDashes;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        activeDashes = 0;
    }

    private CharacterController controller;
    private bool dashing;
    private Vector3 dashDirection;
    private float dashSpeed;
    private float remainingTime;
    private float elapsedTime;
    private DashAfterimageSettings afterimageSettings;
    private float nextAfterimageTime;

    // 이 플레이어가 지금 돌진 중인지입니다.
    public bool IsDashing => dashing;

    // 돌진 중인 방향(수평, 길이 1)입니다. 돌진 중이 아니면 Vector3.zero입니다. PlayerMovement가 애니메이션/바라보는 방향에 씁니다.
    public Vector3 DashDirection => dashing ? dashDirection : Vector3.zero;

    public static PlayerDash GetOrAdd(GameObject player)
    {
        if (player == null) return null;
        var dash = player.GetComponent<PlayerDash>();
        if (dash == null && player.GetComponent<CharacterController>() != null)
        {
            dash = player.AddComponent<PlayerDash>();
        }
        return dash;
    }

    void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    // direction 방향(수평으로 바꿔 씀)으로 돌진을 시작합니다. 이미 돌진 중이거나 값이 잘못됐으면 false를 돌려줍니다.
    public bool StartDash(Vector3 direction, float distance, float duration, DashAfterimageSettings afterimage = null)
    {
        if (dashing) return false;
        if (controller == null || !controller.enabled) return false;

        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f || distance <= 0f) return false;

        dashDirection = direction.normalized;
        duration = Mathf.Max(duration, 0.01f);
        dashSpeed = distance / duration;
        remainingTime = duration;
        elapsedTime = 0f;

        dashing = true;
        activeDashes++;

        // 잔상: 출발하는 자리에 하나 남기고, 이후 간격마다 남깁니다.
        afterimageSettings = (afterimage != null && afterimage.show) ? afterimage : null;
        if (afterimageSettings != null)
        {
            SpawnAfterimage();
            nextAfterimageTime = Mathf.Max(afterimageSettings.spawnInterval, 0.01f);
        }
        return true;
    }

    void Update()
    {
        if (!dashing) return;

        // 레벨 전환 등으로 CharacterController가 꺼지면 그 자리에서 돌진을 끝냅니다.
        if (controller == null || !controller.enabled)
        {
            EndDash();
            return;
        }

        // 마지막 프레임은 남은 시간만큼만 움직여서 총 이동 거리가 정확히 dashDistance가 되게 합니다.
        float step = Mathf.Min(Time.deltaTime, remainingTime);
        if (step > 0f)
        {
            controller.Move(dashDirection * (dashSpeed * step));
        }

        remainingTime -= step;
        elapsedTime += step;

        // 잔상: 돌진하는 동안 간격마다 지금 모습을 남깁니다(이번 프레임에 이동한 뒤의 위치).
        if (afterimageSettings != null && remainingTime > 0f && elapsedTime >= nextAfterimageTime)
        {
            SpawnAfterimage();
            nextAfterimageTime = elapsedTime + Mathf.Max(afterimageSettings.spawnInterval, 0.01f);
        }

        if (remainingTime <= 0f) EndDash();
    }

    private void SpawnAfterimage()
    {
        try
        {
            DashAfterimage.Spawn(gameObject, afterimageSettings);
        }
        catch (System.Exception e)
        {
            // 연출 오류로 돌진이 멈추지 않도록 이번 잔상만 건너뛰고, 이번 돌진의 나머지 잔상도 끕니다.
            Debug.LogError($"PlayerDash: 잔상 생성 중 오류가 발생해 이번 돌진의 잔상을 건너뜁니다(돌진 자체에는 영향 없음): {e}");
            afterimageSettings = null;
        }
    }

    void OnDisable()
    {
        if (dashing) EndDash();
    }

    private void EndDash()
    {
        if (!dashing) return;
        dashing = false;
        afterimageSettings = null;
        activeDashes = Mathf.Max(0, activeDashes - 1);
    }
}
