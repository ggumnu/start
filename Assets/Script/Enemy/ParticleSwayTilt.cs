using UnityEngine;

// [파티클 기울이기 - 좌우로 움직이는 방향에 맞춰 입자를 살짝 기울임]
// 이펙트 프리팹의 최상위 오브젝트에 붙여두면, 그 프리팹 안의 파티클 입자 하나하나가 "화면 기준으로
// 왼쪽으로 움직이는 중이면 왼쪽으로, 오른쪽으로 움직이는 중이면 오른쪽으로" 살짝 기울어집니다.
// 38번 매료 하트(VFX_Charm_Status)에 쓰려고 만들었고, 붙인 프리팹에만 적용되므로 31번 수면 방울 등
// 다른 이펙트에는 아무 영향이 없습니다(형빈 확정 - 방법 B).
//
// [어떻게 동작하나]
// 매 프레임 각 입자의 실제 이동 속도(Velocity over Lifetime 등 모듈이 만든 속도 포함)를 읽어서,
// 그중 "카메라 화면의 좌우 방향" 성분만 뽑아냅니다. 그 좌우 속도가 Full Tilt Speed에 도달하면
// Max Tilt Angle만큼 기울고, 그보다 느리면 비례해서 덜 기웁니다. 기울기는 Smoothing 값에 따라
// 부드럽게 따라갑니다. 카메라 기준으로 계산하므로 비스듬한 카메라 각도에서도 화면에서 보이는
// 좌우 움직임 그대로 기울어집니다. 움직임 설정(곡선/랜덤/Noise 등)을 나중에 바꿔도 실제 움직임을
// 따라가므로 이 값들을 다시 맞출 필요가 없습니다.
//
// [주의 - 프리팹 쪽 설정]
// - 입자의 회전값을 이 컴포넌트가 직접 정하므로, 같은 파티클에 Rotation over Lifetime / Rotation by
//   Speed 모듈은 꺼두세요(켜두면 서로 덮어씁니다). Start Rotation은 0으로 두세요.
// - Renderer의 Render Mode는 Billboard(기본값)여야 합니다.
// - 매료가 끝날 때 하트를 투명하게 지우는 StatusVfxParticleFader와 함께 붙어 있어도 문제없습니다
//   (서로 다른 값 - 회전/투명도 - 만 바꿈).
[DisallowMultipleComponent]
public class ParticleSwayTilt : MonoBehaviour
{
    [Tooltip("가장 많이 기울어질 때의 각도(도)입니다. 예: 15면 좌우로 최대 15도까지 기울어집니다.")]
    [Range(0f, 60f)] public float maxTiltAngle = 15f;

    [Tooltip("이 속도(m/초)로 좌우로 움직일 때 최대 각도까지 기울어집니다. 값을 낮추면 조금만 움직여도 " +
             "많이 기울고, 높이면 덜 기웁니다.")]
    [Min(0.01f)] public float fullTiltSpeed = 0.3f;

    [Tooltip("기울기가 움직임을 따라가는 빠르기입니다. 클수록 즉시 따라가고, 작을수록 천천히 부드럽게 " +
             "따라갑니다.")]
    [Min(0.1f)] public float smoothing = 10f;

    [Tooltip("기울어지는 방향이 반대로 보이면(오른쪽으로 갈 때 왼쪽으로 기움) 켜세요.")]
    public bool invertDirection = false;

    private ParticleSystem[] systems;
    private ParticleSystem.Particle[] buffer = new ParticleSystem.Particle[64];
    private Camera cachedCamera;

    void Awake()
    {
        systems = GetComponentsInChildren<ParticleSystem>(true);
    }

    void LateUpdate()
    {
        if (systems == null || systems.Length == 0) return;

        if (cachedCamera == null) cachedCamera = Camera.main;
        if (cachedCamera == null) return;

        Vector3 screenRight = cachedCamera.transform.right;
        float blend = 1f - Mathf.Exp(-smoothing * Time.deltaTime);
        float sign = invertDirection ? -1f : 1f;

        for (int s = 0; s < systems.Length; s++)
        {
            ParticleSystem ps = systems[s];
            if (ps == null) continue;

            int count = ps.particleCount;
            if (count == 0) continue;

            if (buffer.Length < count)
            {
                buffer = new ParticleSystem.Particle[Mathf.NextPowerOfTwo(count)];
            }

            count = ps.GetParticles(buffer, count);

            // Simulation Space가 Local이면 입자 속도가 파티클 오브젝트 기준이라 월드 방향으로 바꿔줍니다.
            bool localSpace = ps.main.simulationSpace == ParticleSystemSimulationSpace.Local;
            Transform psTransform = ps.transform;

            for (int i = 0; i < count; i++)
            {
                Vector3 velocity = buffer[i].totalVelocity;
                if (localSpace) velocity = psTransform.TransformDirection(velocity);

                // 화면 기준 좌우 속도(오른쪽 +, 왼쪽 -)를 -1~1로 바꾼 뒤 각도로 만듭니다.
                float lateral = Vector3.Dot(velocity, screenRight);
                float t = Mathf.Clamp(lateral / fullTiltSpeed, -1f, 1f);
                float target = t * maxTiltAngle * sign;

                buffer[i].rotation = Mathf.Lerp(buffer[i].rotation, target, blend);
            }

            ps.SetParticles(buffer, count);
        }
    }
}
