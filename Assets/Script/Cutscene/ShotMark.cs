using UnityEngine;

// [촬영 스튜디오 - 구도 / 위치 표시]
// 장면(StudioShot)의 Marks 아래에 두는 표시입니다. 대본에서는 이 오브젝트의 이름(A, B, P1 ...)으로 부릅니다.
// - 구도(Camera): 카메라가 설 위치/방향/화각. 선택하면 촬영 카메라가 이 자리로 옮겨져 Game 창에 실제 화면이 보입니다.
// - 위치(Position): 배우가 설 자리, 카메라가 바라보거나 돌 중심점.
public class ShotMark : MonoBehaviour
{
    public enum MarkKind { Camera, Position }

    [Tooltip("Camera = 카메라 구도, Position = 배우 자리나 카메라가 바라볼 지점입니다.")]
    public MarkKind kind = MarkKind.Camera;

    [Tooltip("구도의 화각(FOV)입니다. 게임 카메라와 같은 60이 기본입니다. 작을수록 망원(확대), 클수록 광각.")]
    [Range(5f, 120f)] public float fieldOfView = 60f;

    void OnDrawGizmos()
    {
        if (kind == MarkKind.Camera)
        {
            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.9f);
            Matrix4x4 old = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
            Gizmos.DrawFrustum(Vector3.zero, fieldOfView, 0.6f, 0.05f, 16f / 9f);
            Gizmos.matrix = old;
        }
        else
        {
            Gizmos.color = new Color(0.3f, 0.9f, 1f, 0.9f);
            Gizmos.DrawWireSphere(transform.position, 0.12f);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 0.4f);
            Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 0.5f);
        }

#if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up * 0.15f, name);
#endif
    }
}
