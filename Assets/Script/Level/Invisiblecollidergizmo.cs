using UnityEngine;

// 렌더러(Mesh Renderer) 없이 콜라이더만 있는 "투명한" 오브젝트(숨겨진 벽, 탄환 차단 경계 등)에
// 붙이면, 실제 게임(Play 모드/빌드)에는 전혀 보이지 않으면서 씬 뷰에서는 반투명 박스로
// 표시해줘서 조립/배치 작업을 눈으로 확인하며 할 수 있게 해줍니다.
//
// [왜 필요한가]
// 마름모 시야로 바뀌면서 카메라에 가까운 두 방향(서/남)의 벽은 렌더러가 아예 없는 순수
// 콜라이더 오브젝트가 됩니다. 오브젝트 자체가 안 보이는 게 목적이므로 게임 안에서는 당연히
// 아무것도 그려지면 안 되지만, 조립하는 입장에서는 정확한 위치/크기를 눈으로 확인하면서
// 배치해야 합니다. Unity의 Gizmos는 Scene 뷰(그리고 원하면 에디터의 Game 뷰) 전용 표시
// 기능이라 실제 빌드나 Play 모드의 최종 화면에는 절대 나타나지 않습니다 - 즉 이 컴포넌트를
// 붙여도 게임 내 시각적 결과물에는 아무 영향이 없습니다.
//
// [사용 방법]
// 1. 렌더러 없이 콜라이더(주로 Box Collider)만 있는 오브젝트를 만듭니다
//    (빈 GameObject를 만들고 Add Component로 Box Collider만 추가하면 됩니다.
//    Plane이나 Cube 프리미티브로 만들 필요는 없습니다 - 오히려 기본으로 딸려오는
//    Mesh Renderer를 따로 지워야 해서 번거롭습니다).
// 2. 그 오브젝트에 이 컴포넌트를 추가합니다.
// 3. 인스펙터에서 gizmoColor를 원하는 색으로 지정합니다 (기본은 RoomAssemblyGuide의
//    탄환 차단 경계 가이드와 동일한 주황색입니다. 숨겨진 벽 콜라이더처럼 다른 용도로 쓸
//    때는 구분되는 다른 색을 지정하면 여러 개를 동시에 봐도 헷갈리지 않습니다).
// 4. onlyWhenSelected를 켜두면 오브젝트를 선택했을 때만 표시되고, 끄면 항상(선택 안 해도)
//    씬 뷰에 표시됩니다. 여러 개를 한 번에 확인하고 싶으면 끄고, 하나씩 정밀하게 맞출
//    때는 켜두는 걸 추천합니다.
[RequireComponent(typeof(Collider))]
public class InvisibleColliderGizmo : MonoBehaviour
{
    [Tooltip("씬 뷰에서 표시할 색상입니다. 알파값을 낮추면 반투명하게 보입니다.")]
    public Color gizmoColor = new Color(1f, 0.55f, 0.1f, 0.5f);

    [Tooltip("켜두면 이 오브젝트를 선택했을 때만 표시됩니다. 끄면 항상(선택 안 해도) 표시됩니다.")]
    public bool onlyWhenSelected = false;

    private void OnDrawGizmos()
    {
        if (!onlyWhenSelected) DrawGizmo();
    }

    private void OnDrawGizmosSelected()
    {
        if (onlyWhenSelected) DrawGizmo();
    }

    private void DrawGizmo()
    {
        var col = GetComponent<Collider>();
        if (col == null) return;

        if (col is BoxCollider box)
        {
            // BoxCollider는 로컬 좌표계(center/size) 기준이라, 오브젝트가 회전되어 있어도
            // 정확한 모양으로 그리기 위해 Gizmos.matrix를 오브젝트의 로컬-월드 변환으로 맞춥니다.
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = gizmoColor;
            Gizmos.DrawCube(box.center, box.size);

            var outline = gizmoColor;
            outline.a = 1f;
            Gizmos.color = outline;
            Gizmos.DrawWireCube(box.center, box.size);

            Gizmos.matrix = Matrix4x4.identity;
        }
        else
        {
            // Box Collider가 아닌 경우(Capsule 등)는 정확한 모양 대신 월드 기준 바운드 박스로
            // 대략 표시합니다 (대부분의 투명 벽/경계는 Box Collider를 쓰게 될 것이라 이 분기는
            // 예외적인 경우를 위한 안전장치입니다).
            Gizmos.color = gizmoColor;
            Gizmos.DrawCube(col.bounds.center, col.bounds.size);

            var outline = gizmoColor;
            outline.a = 1f;
            Gizmos.color = outline;
            Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
        }
    }
}