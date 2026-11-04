using System.Collections.Generic;
using UnityEngine;

// 보물상자가 열리는 순간 뚜껑(Lid)을 경첩을 축으로 회전시켜 "활짝 열리는" 연출을 보여주는
// 컴포넌트입니다. 애니메이션 에셋 없이 코드로 회전값만 바꿉니다(사격 시 상체 젖힘과 같은 방식).
//
// [붙이는 위치와 준비물]
// 보물상자 프리팹의 루트(TreasureChest 또는 ItemPedestal이 붙어 있는 오브젝트)에 붙입니다.
// 뚜껑은 별도 자식 오브젝트("Lid")로 분리되어 있어야 하는데, 이 작업은 에디터 메뉴
// MobArchive > 보물상자 뚜껑 분리(ChestLidSplitTool)가 이 컴포넌트 추가까지 자동으로 해줍니다.
//
// [상자 스크립트를 건드리지 않는 이유]
// 이 컴포넌트는 상자의 IsOpened(IOpenableContainer)만 매 프레임 확인합니다. 그래서 F키로 열든,
// 다른 경로로 "열림" 상태가 되든(예: ItemPedestal의 아이템 즉시 확정) 똑같이 반응하고, 상자의
// 보상/획득 로직과는 완전히 분리됩니다. 열림 연출이 끝나면 스스로 꺼져서 이후엔 아무 계산도 하지 않습니다.
//
// [경첩 위치 계산]
// 뚜껑 메시에 피벗을 따로 맞추지 않습니다. 열리기 시작하는 순간 뚜껑 메시의 정점을 읽어서,
// hingeSide 방향 끝 + 뚜껑 바닥 높이에 경첩선을 자동으로 잡습니다. 이때 옆면 걸쇠 장식은 빼고
// 뚜껑 본체의 모서리를 기준으로 삼습니다(TryGetHingeEdge 참고). 그래서 경첩 방향은 언제든
// 인스펙터 드롭다운만 바꾸면 됩니다. 방향은 벽 프리팹과 같은 동서남북 기준(북=+Z, 동=+X)입니다.
//
// [상자를 돌려 놓았을 때 - 경첩이 상자를 따라 돎 (형빈 확정)]
// 예전에는 hingeSide가 월드 기준 동서남북으로 고정이라, 바닥 프리팹 안에서 상자를 Y축으로 돌려 놓으면 경첩은 그대로
// 월드 북쪽에 남아 뚜껑이 엉뚱한 모서리를 축으로 열렸습니다. 이제 Rotate With Chest가 켜져 있으면(기본) hingeSide를
// "상자를 돌리지 않았을 때의 방향"으로 보고, 상자가 Y축으로 돈 만큼 경첩 방향(과 Hinge Offset)도 함께 돌립니다. 그래서
// 30도/45도처럼 비스듬히 돌려도 항상 같은 모서리를 축으로 열립니다.
// - 상자가 돈 각도는 상자 루트의 좌우축(X축, 없으면 앞뒤축)이 수평면에서 가리키는 방향으로 잽니다. 3ds Max에서 가져온
//   상자처럼 루트가 X축으로 -90도 눕혀져 있어도(DroneBox) 정확히 잴 수 있습니다(Rotation의 Y 숫자를 그대로 읽으면
//   X가 -90일 때 값이 Z칸으로 섞여 나와 믿을 수 없습니다).
// - 기준은 "프리팹 원본 루트에 Y 회전이 없다"입니다(지금 DroneBox 프리팹들이 그렇습니다). 일반방/보스방/동아리방 상자는
//   프리팹 회전 그대로 생성되므로 돈 각도가 0이라 예전과 똑같이 열립니다.
// - Rotate With Chest를 끄면 예전처럼 월드 기준 동서남북입니다.
//
// [사라질 때]
// 상자가 사라질 때 쓰는 DissolveAndVanish는 자식 렌더러까지 함께 처리하므로, 뚜껑도 같이 사라집니다.
public class ChestLidOpener : MonoBehaviour
{
    public enum HingeSide { North, East, South, West }

    [Header("참조 (비워두면 자동으로 찾습니다)")]
    [Tooltip("회전시킬 뚜껑 오브젝트입니다. 비워두면 자식 중 이름이 \"Lid\"인 오브젝트를 찾습니다.")]
    public Transform lid;

    [Header("경첩")]
    [Tooltip("경첩이 달린 쪽입니다. 벽 프리팹과 같은 동서남북 기준(북=+Z, 동=+X)이며, 화면에서는 북쪽이 " +
             "오른쪽 위, 서쪽이 왼쪽 위 모서리입니다. 뚜껑은 이 반대쪽이 들리면서 열립니다.")]
    public HingeSide hingeSide = HingeSide.North;
    [Tooltip("켜면(기본) 위 Hinge Side를 '상자를 돌리지 않았을 때의 방향'으로 보고, 상자를 Y축으로 돌린 만큼 경첩 방향도 " +
             "함께 돌립니다 - 바닥 프리팹 안에서 상자를 몇 도로 돌려 놓든 항상 같은 모서리를 축으로 열립니다. " +
             "끄면 예전처럼 월드 기준 동서남북으로 고정됩니다.")]
    public bool rotateWithChest = true;
    [Tooltip("자동으로 계산한 경첩선 위치를 미세하게 옮깁니다. 열리는 모습이 어색할 때만 조정하세요. 기본값 (0, 0, 0). " +
             "Rotate With Chest가 켜져 있으면 이 값도 상자가 돈 만큼 함께 돕니다(상자를 돌리지 않았을 때 기준으로 입력).")]
    public Vector3 hingeOffset = Vector3.zero;

    [Header("열림 연출")]
    [Tooltip("최종적으로 열려 있을 각도(도)입니다.")]
    [Range(0f, 180f)]
    public float openAngle = 130f;
    [Tooltip("다 열릴 때까지 걸리는 시간(초)입니다. 튕김 구간도 이 시간 안에 포함됩니다.")]
    [Min(0.01f)]
    public float openDuration = 0.35f;
    [Tooltip("끝에서 살짝 더 열렸다가 돌아오는 튕김 세기입니다. 0이면 튕김 없음. " +
             "참고: 1.0 = 약 5도, 1.2 = 약 7도, 1.7 = 약 13도 더 열렸다가 돌아옵니다(열림 각도 130도 기준).")]
    [Min(0f)]
    public float bounceStrength = 1.2f;

    private IOpenableContainer container;
    private bool isAnimating = false;
    private float elapsed = 0f;
    private Vector3 hingePoint;
    private Vector3 hingeAxis;
    private Vector3 closedPosition;
    private Quaternion closedRotation;

    void Awake()
    {
        container = FindOpenableContainer();

        if (lid == null)
        {
            lid = FindChildRecursive(transform, "Lid");
        }
    }

    void Update()
    {
        if (container == null || lid == null)
        {
            Debug.LogWarning($"ChestLidOpener({name}): " +
                             (container == null ? "열림 상태를 알려줄 상자 스크립트(IOpenableContainer)를 찾지 못했습니다. "
                                                : "\"Lid\" 뚜껑 오브젝트를 찾지 못했습니다. ") +
                             "뚜껑 연출을 끕니다.", this);
            enabled = false;
            return;
        }

        if (!isAnimating)
        {
            if (!container.IsOpened) return;
            BeginOpen();
        }

        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / openDuration);
        ApplyAngle(openAngle * EaseOutBack(t, bounceStrength));

        if (t >= 1f)
        {
            ApplyAngle(openAngle);
            enabled = false; // 연출 끝 - 이후엔 열린 상태 그대로 유지, 더 이상 계산하지 않음
        }
    }

    // 열리기 시작하는 순간의 뚜껑 위치를 "닫힌 상태"로 기억하고, 그 상태의 외곽 박스로 경첩선을 계산합니다.
    // (Awake 시점이 아니라 이 시점에 계산하는 이유: 상자를 생성한 뒤 스포너가 위치를 옮겨도 항상 정확하도록)
    private void BeginOpen()
    {
        isAnimating = true;
        elapsed = 0f;
        closedPosition = lid.position;
        closedRotation = lid.rotation;

        // 상자를 Y축으로 돌려 놓았으면 경첩 방향/오프셋도 그만큼 함께 돌립니다(위 [상자를 돌려 놓았을 때] 참고).
        Quaternion chestYaw = rotateWithChest ? Quaternion.Euler(0f, GetChestYawDegrees(), 0f) : Quaternion.identity;
        Vector3 dir = chestYaw * GetHingeDirection();
        Vector3 offset = chestYaw * hingeOffset;
        hingeAxis = Vector3.Cross(Vector3.up, dir); // 이 축으로 +회전하면 경첩 반대쪽이 들립니다

        if (TryGetHingeEdge(dir, out Vector3 edgePoint))
        {
            hingePoint = edgePoint + offset;
        }
        else
        {
            hingePoint = lid.position + offset;
        }
    }

    private void ApplyAngle(float angle)
    {
        Quaternion q = Quaternion.AngleAxis(angle, hingeAxis);
        lid.position = hingePoint + q * (closedPosition - hingePoint);
        lid.rotation = q * closedRotation;
    }

    // 상자 루트가 월드 Y축 기준으로 몇 도 돌아 있는지 잽니다(시계 방향 = +, Rotation Y와 같은 방향).
    // 루트의 좌우축(transform.right)이 수평이면 그 방향이 월드 +X에서 얼마나 돌았는지로 재고, 좌우축이 거의 세로로 서
    // 있으면(루트가 Z축으로 눕혀진 경우) 앞뒤축(transform.forward)이 월드 +Z에서 얼마나 돌았는지로 잽니다. 둘 다
    // 세로면(잴 수 없음) 0도 - 예전과 같은 월드 기준입니다.
    private float GetChestYawDegrees()
    {
        Vector3 right = transform.right;
        Vector3 flatRight = new Vector3(right.x, 0f, right.z);
        if (flatRight.sqrMagnitude > 0.25f)
        {
            return Vector3.SignedAngle(Vector3.right, flatRight.normalized, Vector3.up);
        }

        Vector3 forward = transform.forward;
        Vector3 flatForward = new Vector3(forward.x, 0f, forward.z);
        if (flatForward.sqrMagnitude > 0.25f)
        {
            return Vector3.SignedAngle(Vector3.forward, flatForward.normalized, Vector3.up);
        }
        return 0f;
    }

    private Vector3 GetHingeDirection()
    {
        switch (hingeSide)
        {
            case HingeSide.East: return Vector3.right;
            case HingeSide.South: return Vector3.back;
            case HingeSide.West: return Vector3.left;
            default: return Vector3.forward; // North
        }
    }

    // 경첩선 위치를 뚜껑 메시의 정점으로 직접 계산합니다.
    // - 높이: 뚜껑의 가장 낮은 높이(= 몸통과 맞닿는 면).
    // - 경첩 방향으로의 끝: 뚜껑 "위쪽 절반"에 있는 정점들 중 경첩 방향으로 가장 먼 곳.
    //   뚜껑 옆면의 걸쇠 장식은 뚜껑 아래쪽 절반에만 달려 있어서 이 계산에서 빠집니다. 그래서 걸쇠가
    //   아니라 뚜껑 본체의 모서리(몸통 모서리와 맞닿는 변)가 경첩이 됩니다.
    private bool TryGetHingeEdge(Vector3 dir, out Vector3 edgePoint)
    {
        edgePoint = default;

        var worldVerts = new List<Vector3>();
        foreach (var mf in lid.GetComponentsInChildren<MeshFilter>())
        {
            Mesh mesh = mf.sharedMesh;
            if (mesh == null || !mesh.isReadable) continue;
            foreach (var v in mesh.vertices)
            {
                worldVerts.Add(mf.transform.TransformPoint(v));
            }
        }
        if (worldVerts.Count == 0) return false;

        float minY = float.MaxValue, maxY = float.MinValue;
        Vector3 sum = Vector3.zero;
        foreach (var p in worldVerts)
        {
            if (p.y < minY) minY = p.y;
            if (p.y > maxY) maxY = p.y;
            sum += p;
        }
        Vector3 center = sum / worldVerts.Count;
        float midY = (minY + maxY) * 0.5f;

        float maxAlong = float.MinValue;
        foreach (var p in worldVerts)
        {
            if (p.y < midY) continue;
            float along = Vector3.Dot(p, dir);
            if (along > maxAlong) maxAlong = along;
        }
        if (maxAlong == float.MinValue) return false;

        // 경첩선 위의 한 점: 중심에서 경첩 방향으로 maxAlong까지 이동 + 높이는 뚜껑 바닥.
        // (경첩선은 경첩 방향과 수직인 수평선이라, 선 위의 어느 점이든 회전 결과는 같습니다.)
        Vector3 point = center + dir * (maxAlong - Vector3.Dot(center, dir));
        point.y = minY;
        edgePoint = point;
        return true;
    }

    // 0~1 진행도를 "끝에서 살짝 넘었다가 돌아오는" 곡선으로 바꿉니다(easeOutBack).
    // strength가 0이면 일반적인 감속 곡선(easeOutCubic)이 되어 튕김이 없습니다.
    private static float EaseOutBack(float t, float strength)
    {
        float c3 = strength + 1f;
        float x = t - 1f;
        return 1f + c3 * x * x * x + strength * x * x;
    }

    // 자기 자신 -> 부모 순으로 IOpenableContainer를 구현한 스크립트를 찾습니다. 인터페이스 타입으로
    // GetComponent를 쓰면 에디터에서 "못 찾았는데 null이 아닌" 값이 돌아오는 경우가 있어, 컴포넌트를
    // 직접 훑으며 확인하는 안전한 방식을 씁니다.
    private IOpenableContainer FindOpenableContainer()
    {
        foreach (var mb in GetComponentsInParent<MonoBehaviour>(true))
        {
            if (mb is IOpenableContainer c) return c;
        }
        return null;
    }

    private static Transform FindChildRecursive(Transform parent, string childName)
    {
        foreach (Transform child in parent)
        {
            if (child.name == childName) return child;
            var found = FindChildRecursive(child, childName);
            if (found != null) return found;
        }
        return null;
    }
}
