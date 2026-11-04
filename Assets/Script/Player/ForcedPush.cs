using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// 대상을 정해진 방향으로 짧게 "밀어내는" 공용 넉백입니다(형빈 확정 - 짧은 넉백, 감속하며 미끄러지듯 밀림).
// 지금은 16번 방 추가 때 새로 생긴 벽/잠금 바리케이드에 플레이어가 겹치지 않도록 바깥(방 안쪽)으로 밀어내는 데 쓰고,
// 68번 패시브(총알 넉백)가 적을 밀어내는 데도 씁니다.
//
// [쓰는 법]
// ForcedPush.Apply(대상 오브젝트, 방향, 거리, 시간, 밀리는 동안 무시할 콜라이더 목록(선택))
// - 방향은 수평(Y 무시)으로 바뀌어 쓰입니다. 거리만큼, 시간 동안 처음엔 빠르고 끝으로 갈수록 느려지게 밀립니다.
// - 같은 대상을 밀리는 도중 다시 밀면 앞의 밀림은 끝나고 새 밀림으로 바뀝니다.
// - 밀림을 도중에 끝내려면 돌려받은 ForcedPush의 Stop()을 부릅니다.
//
// [지원하는 대상]
// - CharacterController로 움직이는 대상(플레이어): CharacterController.Move로 밉니다. 그래서 방 안의 다른 벽/장애물에는 그대로
//   막힙니다. 플레이어가 밀리는 동안에는 이동 입력이 막힙니다(PlayerMovement가 BlocksPlayerInput을 확인 - 안 막으면 밀리는
//   반대쪽 키를 누를 때 밀림이 상쇄됨). 중력은 그대로 적용됩니다.
// - [68번 추가] 적(CharacterController가 없는 대상): 켜진 NavMeshAgent가 NavMesh 위에 있으면 NavMeshAgent.Move로 밀고, 아니면
//   (예: 테스트 아레나의 AI를 뗀 더미 적 - 에이전트가 꺼져 있음) transform을 직접 옮깁니다.
//   - 벽("Wall" 태그 콜라이더)에 닿으면 그 자리에서 밀림이 끝납니다(형빈 확정 - 부딪히면 멈춤). 몸통 콜라이더 크기로 미리
//     훑어서(SphereCast) 벽 바로 앞까지만 옮깁니다. NavMesh 가장자리(벽이 아닌 장애물 등)에 막혀 거의 못 나아갈 때도 끝납니다.
//   - 적이 밀리는 동안에는 적 AI(EnemyAIController/EnemyRangedAIController/EnemyContactOnlyAttack)가 이동 판단을 쉽니다
//     (IsBeingPushed 확인 - 안 쉬면 매 프레임 플레이어 쪽으로 다시 걸어와 밀림이 상쇄됨). 진행 중인 충전/공격 타이머는 각
//     공격 컴포넌트가 그대로 진행합니다.
//   - AfterStep: 매번 한 걸음 민 직후(아직 밀리는 중일 때) 불리는 칸입니다. 68번이 여기서 다른 적과 닿았는지 확인하고, 닿으면
//     Stop()으로 멈춥니다. 새 밀림이 시작되거나 밀림이 끝나면 자동으로 비워집니다(다른 밀림에 남아 불리지 않음).
//
// [무시할 콜라이더 - 겹친 채로 밀기 (플레이어 전용)]
// 방금 생긴 벽처럼 이미 대상과 겹쳐 있는 콜라이더는 밀리는 동안만 대상과 충돌하지 않게 해 둡니다(Physics.IgnoreCollision).
// 이미 겹친 콜라이더와 충돌을 켠 채로 밀면 그 콜라이더에 걸려 끼일 수 있기 때문입니다. 밀림이 끝나면 다시 충돌하게 되돌리고,
// 그때도 아직 겹쳐 있으면(밀리는 길이 장애물에 막힌 경우) 겹치지 않는 곳까지 같은 방향으로 조금씩 더 옮겨 끼임을 막습니다.
public class ForcedPush : MonoBehaviour
{
    // 끝났는데도 겹쳐 있을 때 같은 방향으로 더 옮겨 보는 간격과 최대 거리입니다(m).
    private const float ResolveStep = 0.1f;
    private const float ResolveMaxDistance = 2f;

    // [적] 벽 바로 앞에서 멈출 때 벽과 남겨 두는 틈입니다(m).
    private const float WallSkin = 0.01f;
    // [적] 한 걸음에서 의도한 거리의 이 비율보다 덜 나아갔으면 "막혔다"고 보고 밀림을 끝냅니다.
    private const float BlockedAdvanceRatio = 0.5f;
    // [적] 이보다 짧은 걸음은 막힘 판정을 하지 않습니다(m) - 끝부분의 아주 작은 걸음이 오차로 막힘 판정되는 것 방지.
    private const float MinStepForBlockCheck = 0.0005f;

    private enum PushMode { Player, Agent, Transform }

    // 플레이어가 지금 밀리는 중인지입니다. PlayerMovement가 이 값이 true인 동안 이동 입력을 무시합니다.
    public static bool BlocksPlayerInput => activePlayerPushes > 0;
    private static int activePlayerPushes;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        activePlayerPushes = 0;
    }

    // [68번 추가] 이 오브젝트(적)가 지금 밀리는 중인지입니다. 적 AI가 매 프레임 호출해서, true인 동안 이동 판단을 쉽니다.
    // 한 번도 밀린 적 없는 적(ForcedPush가 안 붙음)은 항상 false입니다.
    public static bool IsBeingPushed(GameObject target)
    {
        if (target == null) return false;
        var push = target.GetComponent<ForcedPush>();
        return push != null && push.isPushing;
    }

    private CharacterController characterController;
    private bool isPlayer;

    // [68번 추가] 적을 밀 때 쓰는 참조입니다.
    private PushMode mode;
    private NavMeshAgent agent;
    private Collider bodyCollider;

    private bool isPushing;
    private bool countedAsPlayerPush;
    private Vector3 direction;
    private float distance;
    private float duration;
    private float elapsed;
    private float travelledFraction;
    private readonly List<Collider> ignoredColliders = new List<Collider>();

    private static readonly RaycastHit[] wallHitBuffer = new RaycastHit[16];

    // 지금 밀리는 중인지입니다.
    public bool IsPushing => isPushing;
    // 지금 밀리는 수평 방향(정규화)입니다.
    public Vector3 Direction => direction;

    // [68번 추가] 적을 한 걸음 민 직후마다 불립니다(위 [지원하는 대상] 주석 참고). 적 밀림에서만 불립니다.
    public Action<ForcedPush> AfterStep;

    // target을 direction 방향(수평)으로 pushDistance만큼, pushDuration초 동안 밉니다. 밀 수 없는 대상이면 null.
    public static ForcedPush Apply(GameObject target, Vector3 direction, float pushDistance, float pushDuration,
                                   IList<Collider> ignoreWhilePushing = null)
    {
        if (target == null || pushDistance <= 0f) return null;

        Vector3 flat = new Vector3(direction.x, 0f, direction.z);
        if (flat.sqrMagnitude < 0.0001f) return null;

        var controller = target.GetComponent<CharacterController>();
        if (controller != null)
        {
            var playerPush = target.GetComponent<ForcedPush>();
            if (playerPush == null) playerPush = target.AddComponent<ForcedPush>();
            playerPush.Begin(controller, flat.normalized, pushDistance, pushDuration, ignoreWhilePushing);
            return playerPush;
        }

        // [68번 추가] CharacterController가 없으면 적으로 보고 NavMeshAgent(또는 transform)로 밉니다.
        var push = target.GetComponent<ForcedPush>();
        if (push == null) push = target.AddComponent<ForcedPush>();
        push.BeginEnemy(flat.normalized, pushDistance, pushDuration);
        return push;
    }

    // 지금 밀림을 그 자리에서 끝냅니다(밀리는 중이 아니면 아무 일도 없음).
    public void Stop()
    {
        if (isPushing) Finish(false);
    }

    private void Begin(CharacterController controller, Vector3 dir, float pushDistance, float pushDuration,
                       IList<Collider> ignoreWhilePushing)
    {
        if (isPushing) Finish(false); // 앞의 밀림을 정리하고 새로 시작합니다.

        mode = PushMode.Player;
        AfterStep = null;
        characterController = controller;
        isPlayer = controller.CompareTag("Player");
        direction = dir;
        distance = pushDistance;
        duration = Mathf.Max(0.01f, pushDuration);
        elapsed = 0f;
        travelledFraction = 0f;
        isPushing = true;
        enabled = true;

        if (isPlayer)
        {
            activePlayerPushes++;
            countedAsPlayerPush = true;
        }

        ignoredColliders.Clear();
        if (ignoreWhilePushing != null)
        {
            foreach (var col in ignoreWhilePushing)
            {
                if (col == null || col == characterController || col.isTrigger) continue;
                Physics.IgnoreCollision(characterController, col, true);
                ignoredColliders.Add(col);
            }
        }
    }

    // [68번 추가] 적 밀림을 시작합니다.
    private void BeginEnemy(Vector3 dir, float pushDistance, float pushDuration)
    {
        if (isPushing) Finish(false); // 앞의 밀림을 정리하고 새로 시작합니다.

        AfterStep = null;
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (bodyCollider == null) bodyCollider = FindBodyCollider();

        mode = (agent != null && agent.enabled && agent.isOnNavMesh) ? PushMode.Agent : PushMode.Transform;
        direction = dir;
        distance = pushDistance;
        duration = Mathf.Max(0.01f, pushDuration);
        elapsed = 0f;
        travelledFraction = 0f;
        isPushing = true;
        enabled = true;
    }

    void Update()
    {
        if (!isPushing) return;

        if (mode != PushMode.Player)
        {
            UpdateEnemy();
            return;
        }

        if (characterController == null || !characterController.enabled)
        {
            Finish(false);
            return;
        }

        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / duration);
        float fraction = 1f - (1f - t) * (1f - t); // 처음엔 빠르고 끝으로 갈수록 느려짐(ease-out)

        float step = (fraction - travelledFraction) * distance;
        travelledFraction = fraction;
        if (step > 0f) characterController.Move(direction * step);

        if (t >= 1f) Finish(true);
    }

    // [68번 추가] 적 한 걸음입니다. 벽 앞에서 멈추거나 막히면 끝내고, 아직 밀리는 중이면 AfterStep을 부릅니다.
    private void UpdateEnemy()
    {
        if (mode == PushMode.Agent && (agent == null || !agent.enabled || !agent.isOnNavMesh))
        {
            Finish(false);
            return;
        }

        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / duration);
        float fraction = 1f - (1f - t) * (1f - t); // 처음엔 빠르고 끝으로 갈수록 느려짐(ease-out)

        float step = (fraction - travelledFraction) * distance;
        travelledFraction = fraction;

        bool hitWall = false;
        if (step > 0f)
        {
            // 벽까지 남은 거리보다 멀리 가지 않습니다.
            float wallDistance = DistanceToWall(step);
            if (wallDistance < step)
            {
                step = wallDistance;
                hitWall = true;
            }

            if (step > 0f)
            {
                Vector3 before = transform.position;
                if (mode == PushMode.Agent)
                {
                    agent.Move(direction * step);
                }
                else
                {
                    transform.position += direction * step;
                }

                // NavMesh 가장자리 등에 막혀 거의 못 나아갔으면 그 자리에서 끝냅니다.
                float advanced = Vector3.Dot(transform.position - before, direction);
                if (step >= MinStepForBlockCheck && advanced < step * BlockedAdvanceRatio) hitWall = true;
            }
        }

        if (hitWall || t >= 1f)
        {
            // 마지막 걸음에도 닿았는지 확인할 수 있게, 끝내기 전에 한 번 더 부릅니다.
            InvokeAfterStep();
            if (isPushing) Finish(false);
            return;
        }

        InvokeAfterStep();
    }

    private void InvokeAfterStep()
    {
        if (!isPushing || AfterStep == null) return;
        Physics.SyncTransforms(); // 방금 옮긴 위치로 겹침 검사를 할 수 있게 콜라이더 위치를 맞춥니다.
        AfterStep(this);
    }

    // [68번 추가] 몸통 콜라이더로 진행 방향을 훑어, maxDistance 안에 "Wall" 태그 콜라이더가 있으면 그 앞까지의 거리를,
    // 없으면 maxDistance를 돌려줍니다. 시작부터 이미 겹쳐 있는 벽은 무시합니다(그 벽에서 멀어지는 밀림을 막지 않도록).
    private float DistanceToWall(float maxDistance)
    {
        Vector3 origin;
        float radius;
        GetBodySphere(out origin, out radius);

        int count = Physics.SphereCastNonAlloc(origin, radius, direction, wallHitBuffer, maxDistance + WallSkin,
                                               Physics.AllLayers, QueryTriggerInteraction.Ignore);
        float nearest = maxDistance;
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = wallHitBuffer[i];
            if (hit.collider == null || !hit.collider.CompareTag("Wall")) continue;
            if (hit.distance <= 0f && hit.point == Vector3.zero) continue; // 시작부터 겹쳐 있던 콜라이더

            float allowed = Mathf.Max(0f, hit.distance - WallSkin);
            if (allowed < nearest) nearest = allowed;
        }
        return nearest;
    }

    // 몸통 콜라이더를 감싸는 수평 구체(중심, 반지름)입니다. 콜라이더가 없으면 아주 작은 구체로 대신합니다.
    private void GetBodySphere(out Vector3 center, out float radius)
    {
        if (bodyCollider != null && bodyCollider.enabled)
        {
            Bounds b = bodyCollider.bounds;
            center = b.center;
            // 위아래 다른 물체(바닥 등)에 걸리지 않도록 수평 크기의 90%만 씁니다.
            radius = Mathf.Max(0.001f, Mathf.Min(b.extents.x, b.extents.z) * 0.9f);
            return;
        }

        center = transform.position;
        radius = 0.01f;
    }

    // 적의 몸통(트리거가 아닌) 콜라이더입니다. 이 오브젝트 자신에 붙은 것을 먼저 찾고, 없으면 자식에서 찾습니다.
    private Collider FindBodyCollider()
    {
        foreach (var col in GetComponents<Collider>())
        {
            if (col != null && !col.isTrigger) return col;
        }
        foreach (var col in GetComponentsInChildren<Collider>())
        {
            if (col != null && !col.isTrigger) return col;
        }
        return null;
    }

    void OnDisable()
    {
        if (isPushing) Finish(false);
    }

    // 밀림을 끝냅니다. resolveOverlap이면 무시했던 콜라이더와 아직 겹쳐 있을 때 같은 방향으로 더 옮겨 끼임을 막습니다.
    private void Finish(bool resolveOverlap)
    {
        isPushing = false;
        AfterStep = null;

        if (countedAsPlayerPush)
        {
            activePlayerPushes = Mathf.Max(0, activePlayerPushes - 1);
            countedAsPlayerPush = false;
        }

        if (mode != PushMode.Player) return; // 아래는 플레이어(CharacterController) 전용 정리입니다.

        if (resolveOverlap && characterController != null && characterController.enabled)
        {
            ResolveRemainingOverlap();
        }

        if (characterController != null)
        {
            foreach (var col in ignoredColliders)
            {
                if (col != null) Physics.IgnoreCollision(characterController, col, false);
            }
        }
        ignoredColliders.Clear();
    }

    private void ResolveRemainingOverlap()
    {
        if (ignoredColliders.Count == 0) return;

        float moved = 0f;
        while (OverlapsAnyIgnored() && moved < ResolveMaxDistance)
        {
            // 이미 겹친 콜라이더와는 충돌이 꺼져 있으므로 Move가 막히지 않습니다. 다른 장애물에 막혀 더 못 가면 멈춥니다.
            Vector3 before = characterController.transform.position;
            characterController.Move(direction * ResolveStep);
            Physics.SyncTransforms();
            Vector3 after = characterController.transform.position;

            float advanced = Vector3.Dot(after - before, direction);
            if (advanced < ResolveStep * 0.25f) break; // 다른 장애물에 막힘 - 더 밀어도 소용없음
            moved += ResolveStep;
        }
    }

    // CharacterController 캡슐이 무시 중인 콜라이더 중 하나와 겹치는지 확인합니다.
    private bool OverlapsAnyIgnored()
    {
        Transform t = characterController.transform;
        Vector3 scale = t.lossyScale;
        float radius = characterController.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
        float height = Mathf.Max(characterController.height * Mathf.Abs(scale.y), radius * 2f);
        Vector3 center = t.TransformPoint(characterController.center);
        float half = height * 0.5f - radius;
        Vector3 top = center + Vector3.up * half;
        Vector3 bottom = center - Vector3.up * half;

        var hits = Physics.OverlapCapsule(top, bottom, radius, ~0, QueryTriggerInteraction.Ignore);
        foreach (var hit in hits)
        {
            if (hit != null && ignoredColliders.Contains(hit)) return true;
        }
        return false;
    }
}
