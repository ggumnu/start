using UnityEngine;

// [사용 방법]
// 1. 씬에 Cinemachine 카메라를 사용 중이었다면: CinemachineCamera 오브젝트는 비활성화(또는 삭제)하고,
//    Main Camera에 있는 CinemachineBrain 컴포넌트도 제거하거나 비활성화하세요.
//    (Cinemachine이 계속 살아있으면 이 스크립트와 카메라 제어권을 두고 충돌합니다)
// 2. 이 스크립트를 Main Camera 오브젝트에 직접 추가합니다.
// 3. Target 필드에 플레이어를 연결합니다.
// 4. Offset과 Fixed Euler Angles 값을 조절해서 원하는 탑다운 각도를 잡습니다.
//
// [마름모(등각) 시야로 바꾸는 방법 - 참고]
// 기본값은 북쪽/동쪽이 화면 위쪽 두 변, 서쪽/남쪽이 화면 아래쪽 두 변(카메라에 가까운 쪽)에
// 오도록 맞춘 값입니다. 계산 방식: 원래 정남쪽만 바라보던 offset의 수평 성분(0, -8)을 Y축
// 기준 +45도 회전시키면 (-5.66, -5.66)이 되어 카메라가 남서쪽 대각선에서 북동쪽을 바라보게
// 됩니다. 이때 fixedEulerAngles의 Y(요) 값도 offset과 반드시 같은 각도(+45)만큼 같이
// 돌려줘야 카메라가 여전히 플레이어를 정확히 바라봅니다 (위치만 돌리고 시선 각도를 그대로
// 두면 엉뚱한 곳을 보게 됩니다). 높이/거리/피치(X값)는 기존과 동일하게 유지했습니다.
//
// 만약 실제로 확인했을 때 의도와 반대로 나온다면(남/동이 위로 온다거나) 부호를 통째로
// 뒤집으면 됩니다: Offset (5.66, 12, 5.66), Fixed Euler Angles (55, -45, 0).
public class CameraFollow : MonoBehaviour
{
    [Header("추적 대상")]
    public Transform target; // 플레이어

    [Header("오프셋 (월드 기준 고정 - 플레이어 회전과 무관하게 항상 같은 방향)")]
    [Tooltip("마름모 시야: 원래 정남쪽 offset(0,12,-8)의 수평 성분을 Y축 +45도 회전시킨 값입니다 " +
             "(북/동이 화면 위쪽, 서/남이 화면 아래쪽/카메라 가까운 쪽이 되도록). 반대로 보이면 " +
             "(5.66, 12, 5.66)으로 부호를 뒤집어보세요.")]
    public Vector3 offset = new Vector3(-5.66f, 12f, -5.66f);

    [Header("추적 부드러움 (0에 가까울수록 뻣뻣하게, 값이 클수록 즉각 반응)")]
    public float followSpeed = 10f;

    [Header("카메라 각도 고정")]
    public bool lockRotation = true;
    [Tooltip("X값이 커질수록 더 아래를 내려다보는 각도가 됩니다. 90 = 완전히 수직으로 내려다봄. " +
             "Y값(요)은 반드시 위 offset과 같은 각도만큼 같이 돌려야 카메라가 플레이어를 정확히 " +
             "바라봅니다. 반대로 보이면 (55, -45, 0)으로 부호를 뒤집어보세요.")]
    public Vector3 fixedEulerAngles = new Vector3(55f, 45f, 0f);

    void Awake()
    {
        // Target을 수동으로 연결하지 않았다면, "Player" 태그를 가진 오브젝트를 자동으로 찾습니다.
        // 나중에 플레이어가 런 시작 시 동적으로 생성되는 구조가 되어도 이 카메라가 알아서 찾아 붙습니다.
        if (target == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) target = playerObj.transform;
        }
    }

    void LateUpdate()
    {
        if (target == null) return;

        // 오프셋은 항상 월드 좌표 기준이라, 플레이어가 어느 방향을 보든 카메라 위치는 흔들리지 않습니다.
        Vector3 desiredPosition = target.position + offset;
        transform.position = Vector3.Lerp(transform.position, desiredPosition, followSpeed * Time.deltaTime);

        // 매 프레임 다시 적용하므로, Play 모드 중 인스펙터에서 값을 바꾸면 즉시 반영됩니다.
        if (lockRotation)
        {
            transform.rotation = Quaternion.Euler(fixedEulerAngles);
        }
    }
}