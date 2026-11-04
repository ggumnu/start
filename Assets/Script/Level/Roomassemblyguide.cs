using UnityEngine;

// 벽/바닥 프리팹을 3dsmax에서 만든 소품들로 Unity에서 "조립"할 때 쓰는 가이드입니다.
// LevelBuilder.cs의 실제 배치 공식(BuildFloor/BuildWallSegment)과 완전히 동일한 크기/위치로
// 반투명 박스를 만들어주므로, 이 박스 안에 소품(빌딩/화분/자판기/바리케이드 등)을 배치하면
// 실제 게임에서 쓸 크기와 정확히 맞아떨어집니다.
//
// [사용 방법]
// 1. 빈 오브젝트를 하나 만들고 이 스크립트를 붙입니다 (별도 조립용 씬 권장).
// 2. 인스펙터에서 cellSize/wallHeight/wallThickness/floorOverlapMargin 값을
//    LevelBuilder 인스펙터의 값과 동일하게 맞춥니다 (기본값이 이미 LevelBuilder 기본값과 동일).
// 3. 컴포넌트 우클릭(또는 점 3개 메뉴) - "가이드 생성"을 누르면 바닥 1개 + 동서남북 벽 슬롯 4개,
//    그리고 실제 조립 작업물을 담을 빈 오브젝트(피벗) 5개가 함께 생성됩니다.
//    이 오브젝트를 옮기면 가이드 전체가 같이 움직입니다.
// 4. 실제 조립은 반드시 "_pivots"로 끝나는 이름이 붙은 빈 오브젝트 밑에서 진행하세요. 이
//    피벗들은 바닥/동서남북 각 위치의 정중앙에 정확히 생성되며, LevelBuilder가 실제로
//    프리팹을 꽂아넣는 기준 좌표(wallPos)와 완전히 동일합니다. "가이드 생성"을 다시 눌러도
//    피벗과 그 안의 작업물은 삭제되지 않으니(이미 있으면 건너뜀), 시각 가이드만 다시
//    보고 싶을 때 안심하고 재생성해도 됩니다.
//    [참고] LevelBuilder는 이제 북<->서, 남<->동 방향 프리팹을 서로 회전시켜 재사용할 수
//    있습니다. 즉 네 방향 모두 프리팹을 따로 만들 필요는 없고, 예를 들어 North_pivots와
//    South_pivots만 조립해두면 West/East는 코드가 자동으로 회전해서 재사용합니다. East/West
//    피벗은 특정 방향에만 다른 디자인을 주고 싶을 때만 별도로 조립하면 됩니다.
// 5. 슬롯 경계를 넘어가는 장식(간판 등, 플레이어가 닿지 않는 높이/범위)은 괜찮습니다 -
//    실제 게임에서도 한 번에 한 방만 보이는 구조라 문제 없습니다.
// 6. 남쪽(South)과 동쪽(East) 슬롯은 카메라 시야 확보를 위해 다른 방향과 다르게 만들어야
//    한다는 걸 잊지 않도록 색을 다르게 표시해뒀습니다 (마름모 시야에서 카메라에 가까워
//    렌더러 없이 콜라이더만 있는 방향입니다).
// 7. 콜라이더 없이 뚫려있어야 하는 골목 폭(Passage Width, 기본 2)은 조립 피벗 아래
//    생성되는 옆쪽 콜라이더/통로 옆 칸막이(14~15번 참고) 사이 빈 틈으로 바로 확인할 수
//    있습니다. 그 틈 안쪽만 비워두고 나머지 공간(빌딩/화분/펜스 등)에 전부 콜라이더를
//    채우면, 플레이어는 그 틈으로만 다음 방에 드나들 수 있습니다. 여러 골목 디자인(벽
//    프리팹 변형)을 만들더라도 이 폭과 위치를 통일해야, 다음 방에 진입했을 때 항상
//    같은 자리(골목)로 나오게 되어 건물에서 튀어나오는 일이 없습니다.
// 8. 방 전환이 실제로 판정되는 정확한 좌표는 GetWallOffset()이 반환하는 위치이며, 이는
//    각 방향 피벗 오브젝트의 정중앙 위치와 정확히 같습니다 (피벗 자체가 곧 그 기준점).
// 9. 열린 벽(골목+바리케이드)과 닫힌 벽(완전히 막힘)은 같은 피벗/슬롯 위치를 공유하니,
//    같은 피벗 밑에서 두 버전을 순서대로 조립해 각각 다른 이름으로 프리팹 저장하면 됩니다.
// 10. 조립이 끝난 피벗 오브젝트를 프로젝트 창으로 드래그해서 프리팹으로 저장하세요. 시각
//    가이드(박스들)만 정리하고 싶으면 "가이드 삭제"를 쓰면 되고, 피벗과 작업물은 그대로
//    남아있습니다 (가이드와 피벗이 서로 다른 오브젝트 계층에 있어 안전합니다).
// 11. 보라색 통행 바닥은 경계선을 넘어 이웃 방 쪽으로 Alley Floor Overlap 값만큼 더
//    뻗어야, 두 방의 보라 바닥이 서로 겹쳐서 이음매(빈틈)가 안 생깁니다. 이 값은
//    조립 피벗 아래 생성되는 통로 옆 칸막이(14~15번 참고)의 깊이와 동일하니, 그 콜라이더
//    끝자락까지 보라 바닥 메시를 덮으면 됩니다. 골목형(openWalls) 프리팹에만 해당합니다.
// 12. 게이트(바리케이드)가 열려 있어도 총알은 항상 막혀야 하는 위치(탄환 전용 경계)가
//    방과 방이 만나는 경계선이 아니라 그보다 이웃 방 쪽으로 alleyFloorOverlap만큼 더
//    들어간 지점인 이유: 입구에서부터 총알이 막히면 "뚫려있는데 왜 막히지" 하는
//    위화감이 생기기 때문에, 일부러 통로 안쪽 깊숙한 지점으로 뺀 것입니다. 실제
//    콜라이더는 조립 피벗 아래 자동으로 생성됩니다(14번 참고, "BulletOnly").
//    이 콜라이더는 Tag가 Wall이지만(Projectile.cs가 이미 Wall 태그를 막고 있어 코드
//    수정 없이 동작), 플레이어 이동까지 막으면 안 되므로 별도 물리 레이어
//    (bulletOnlyLayerName, 기본 BulletOnlyBlocker)로 지정됩니다. Edit > Project
//    Settings > Physics > Layer Collision Matrix에서 그 레이어와 Player 레이어의
//    충돌 체크를 꺼주세요. 그러면 총알(트리거 콜라이더)은 계속 막히고
//    플레이어(CharacterController)는 그대로 통과합니다. (참고: Player 오브젝트가 아직
//    전용 물리 레이어가 아니라 Default 레이어라면, 이 설정을 위해 Player 전용 레이어를
//    새로 만들어 지정해주는 게 안전합니다.) 이 콜라이더 역시 골목형(openWalls) 프리팹에만
//    해당하고, 완전히 막힌 벽(closedWalls)은 전체 폭이 이미 다 막혀있어(오버랩/골목
//    개념 자체가 없으므로) 경계선 위치 그대로 막으면 됩니다.
// 13. 실제 콜라이더 오브젝트를 어떻게 만들지 궁금하다면: 렌더러(Mesh Renderer/Mesh Filter)
//    없이 Collider 컴포넌트(보통 Box Collider)만 있는 빈 오브젝트로 만드는 게 가장
//    간단하고 가볍습니다. Plane 프리미티브는 기본적으로 가로로 눕혀진 바닥용 메시라 벽
//    용도로는 회전까지 시켜야 하고 기본 Mesh Renderer도 지워야 해서 오히려 손이 더 갑니다.
//    또한 이 가이드처럼 "에디터에서는 보이고 게임에서는 안 보이는" 참고용 표시가 필요하면
//    InvisibleColliderGizmo.cs 컴포넌트를 그 콜라이더 오브젝트에 붙이세요. Gizmos는 Scene
//    뷰(및 원하면 에디터의 Game 뷰)에서만 그려지고 실제 빌드에는 절대 나타나지 않으므로,
//    씬에서 눈으로 위치를 맞추면서도 최종 게임 화면에는 아무 것도 안 보이게 할 수 있습니다.
// 14. generateBoundaryColliders를 켜두면(기본 켜짐) "조립 피벗 생성"을 실행할 때 각 방향
//    피벗 밑에 위 13번에서 설명한 투명 콜라이더가 실제로 자동 생성됩니다 (지금까지의
//    가이드 박스들과 달리, 이건 진짜로 작동하는 콜라이더가 달린 실물 오브젝트입니다).
//    - "{방향}_close": 전체 폭을 막는 콜라이더 1개.
//      열린(골목) 프리팹을 조립할 때는 이 오브젝트를 통째로 지우고 저장하세요.
//    - "{방향}_open": 골목 옆쪽을 막는 콜라이더 2개(Side_A/B) + 통로 옆 칸막이 2개
//      (Connector_A/B) + 13번에서 설명한 탄환 전용 경계 콜라이더 1개(BulletOnly)가 한
//      그룹으로 묶여 있습니다. 닫힌 벽 프리팹을 조립할 때는 이 그룹을 통째로 지우고
//      저장하세요.
//    즉 열린 벽을 만들 땐 "_close"만 지우고, 닫힌 벽을 만들 땐 "_open"만 지우면 됩니다.
//    오브젝트 이름 자체에 어느 쪽을 지울지 적어뒀으니 헷갈릴 걱정은 없습니다.
//    이 콜라이더들은 전부 InvisibleColliderGizmo가 붙어 있어 씬 뷰에서는 반투명 박스로
//    보이지만 게임 화면에는 나타나지 않습니다. Tag는 자동으로 Wall로 지정되고(프로젝트에
//    Wall 태그가 없으면 경고가 뜹니다), 탄환 전용 경계만 별도로 bulletOnlyLayerName에
//    지정된 물리 레이어로 설정됩니다(레이어가 없으면 경고와 함께 기본 레이어로 생성되니,
//    Tags and Layers에서 레이어를 만들고 Physics의 Layer Collision Matrix에서 Player와의
//    충돌을 꺼주는 걸 잊지 마세요). cellSize/passageWidth/alleyFloorOverlap 등을 나중에
//    조정했다면, 컴포넌트 우클릭 메뉴의 "경계 콜라이더만 다시 생성"으로 이미 조립해둔
//    아트 오브젝트는 그대로 둔 채 콜라이더 치수만 최신 값으로 갱신할 수 있습니다.
// 15. 골목 하나에는 총 4가지 투명 콜라이더가 있습니다: (1) 입구(경계선, "{방향}_close" -
//    열린 프리팹을 만들 때 지우는 대상, 플레이어를 막음), (2) 통행로 폭 바깥쪽을 입구에서
//    얇게 막는 옆쪽 콜라이더 2개("Side_A/B", 플레이어를 막음), (3) 입구부터
//    출구까지 통행로 폭 경계를 잇는 칸막이 2개("Connector_A/B", 플레이어와 총알을 둘 다
//    막음), (4) 출구("BulletOnly", 총알만 막고 플레이어는 통과). (2)(3)(4)는 전부
//    "{방향}_open" 그룹에 함께 들어있어 닫힌 프리팹을 만들 때 한 번에 지웁니다.
//    (3) 칸막이는 원래 (2) 옆쪽 콜라이더(입구에서만 얇게 막음)와 (4) 탄환 전용 경계
//    (출구에서 총알만 막음) 사이에 아무것도 없어서, 플레이어가 통로 안으로 걸어 들어간 뒤
//    옆으로 빠져나갈 수 있던 빈틈을 메우기 위해 추가된 것입니다.
public class RoomAssemblyGuide : MonoBehaviour
{
    [Header("LevelBuilder와 반드시 동일하게 맞춰야 하는 값")]
    [Tooltip("LevelBuilder 인스펙터의 Cell Size와 동일한 값을 넣어주세요.")]
    public float cellSize = 6f;
    [Tooltip("대략적인 참고용입니다 - 실제로는 벽 프리팹을 어떤 오브젝트로 조립하느냐에 따라 달라집니다.")]
    public float wallHeight = 4f;
    [Tooltip("대략적인 참고용입니다 - 실제로는 벽 프리팹을 어떤 오브젝트로 조립하느냐에 따라 달라집니다.")]
    public float wallThickness = 0.5f;
    [Tooltip("LevelBuilder 인스펙터의 Floor Overlap Margin과 동일한 값을 넣어주세요.")]
    public float floorOverlapMargin = 1f;

    [Header("통행로(골목) 가이드")]
    [Tooltip("플레이어가 실제로 걸어 들어갈 열린 통로의 폭입니다. 이 폭 안쪽만 콜라이더 없이 " +
             "비워두고, 나머지는 빌딩/화분 등으로 막으면 '중앙으로만 이동 가능'이 완성됩니다. " +
             "모든 벽 프리팹 변형(랜덤 배치될 여러 골목 디자인)이 이 폭을 통일해서 지켜야, " +
             "다음 방 진입 시 항상 같은 위치의 골목으로 나오게 됩니다.")]
    public float passageWidth = 2f;

    [Header("골목 바닥 오버랩 (보라 바닥이 경계선 너머로 뻗어야 하는 깊이)")]
    [Tooltip("보라색 통행 바닥이 방 경계선을 넘어 이웃 방 쪽으로 이만큼 더 뻗어야, 두 방의 " +
             "보라 바닥이 서로 겹쳐서 이음매(빈틈)가 생기지 않습니다. LevelBuilder의 기존 " +
             "floorOverlapMargin(메인 핑크 바닥용)과 같은 목적을, 방향별 골목 바닥에 적용한 " +
             "값이라고 보면 됩니다. 골목형(openWalls) 프리팹을 조립할 때 보라 바닥 메시를 " +
             "경계선 기준 이 깊이만큼 바깥쪽으로 더 그려주세요. 이 값은 입구~출구를 잇는 " +
             "통로 옆 칸막이(스크립트 상단 주석 15번 참고)의 깊이에도 그대로 재사용되어, " +
             "칸막이가 정확히 입구부터 출구까지 이어지도록 합니다.")]
    public float alleyFloorOverlap = 1f;

    [Header("탄환 차단 전용 경계 (게이트가 열려 있어도 총알은 항상 막혀야 함)")]
    [Tooltip("탄환 차단 경계 콜라이더의 두께입니다. 자세한 설명은 스크립트 상단 주석 " +
             "12번 항목을 참고하세요.")]
    public float bulletBoundaryThickness = 0.15f;

    [Header("실제로 작동하는 경계 콜라이더 자동 생성 (조립 피벗과 함께 생성됨)")]
    [Tooltip("켜두면 '조립 피벗 생성'(또는 '가이드 생성') 시 각 방향 피벗 밑에 실제로 작동하는 " +
             "투명 경계 콜라이더가 함께 생성됩니다. 닫힌 벽 버전(전체 폭 콜라이더 1개)과 열린 " +
             "벽(골목) 버전(옆쪽 콜라이더 2개 + 탄환 전용 경계 1개)이 동시에 다 만들어지며, " +
             "형빈이 실제로 어느 쪽 프리팹을 조립하느냐에 따라 안 쓰는 쪽 그룹을 통째로 지우고 " +
             "저장하면 됩니다. 이렇게 하면 방 사이 이동 제한이 특정 아트 프리팹에 콜라이더를 " +
             "제대로 심었는지에 의존하지 않고, 모든 방이 항상 동일한 크기/위치의 경계를 갖게 " +
             "됩니다. 자세한 내용은 스크립트 상단 주석 14~15번을 참고하세요.")]
    public bool generateBoundaryColliders = true;
    [Tooltip("탄환 전용 경계 콜라이더에 지정할 물리 레이어 이름입니다. 이 레이어가 프로젝트에 " +
             "아직 없으면 경고 로그와 함께 기본 레이어로 생성되니, Edit > Project Settings > " +
             "Tags and Layers에서 먼저 만들고, Physics 설정의 Layer Collision Matrix에서 이 " +
             "레이어와 Player 레이어의 충돌을 꺼주세요.")]
    public string bulletOnlyLayerName = "BulletOnlyBlocker";

    [Header("표시 설정")]
    [Range(0.1f, 1f)] public float guideAlpha = 0.25f;

    private const string GuideRootName = "AssemblyGuide";
    private const string PivotRootName = "AssemblyPivots";

    [ContextMenu("가이드 생성")]
    public void GenerateGuide()
    {
        ClearGuide();

        var root = new GameObject(GuideRootName);
        root.transform.SetParent(transform, false);
        root.transform.localPosition = Vector3.zero;

        CreateFloorGuide(root.transform);

        CreateWallGuide(root.transform, Direction.North, "North_slot", new Color(0.3f, 0.55f, 1f, guideAlpha));
        CreateWallGuide(root.transform, Direction.West, "West_slot", new Color(0.9f, 0.85f, 0.2f, guideAlpha));
        // 남쪽/동쪽은 마름모 시야에서 카메라에 가까워 렌더러 없이 콜라이더만 필요한
        // 방향이라, 시야를 가리면 안 된다는 걸 잊지 않도록 눈에 띄게 다른 색으로 강조합니다.
        CreateWallGuide(root.transform, Direction.South, "South_slot", new Color(1f, 0.25f, 0.25f, guideAlpha));
        CreateWallGuide(root.transform, Direction.East, "East_slot", new Color(1f, 0.5f, 0.25f, guideAlpha));

        // 통로 위치/폭/오버랩 깊이/탄환 차단 위치는 이제 실제로 작동하는 투명 콜라이더
        // (조립 피벗 아래 생성됨, 14~15번 참고)만 보면 충분히 파악되므로, 별도 시각
        // 전용 가이드(경계중앙선/통행로가이드/골목바닥오버랩가이드/탄환차단경계가이드)는
        // 만들지 않습니다.
        GeneratePivotAnchors();

        Debug.Log("RoomAssemblyGuide: 가이드 생성 완료. South/East 슬롯(빨강/주황)은 카메라 시야를 가리지 않는 " +
                  "구성으로 조립하세요. 실제 조립은 '_pivots'로 끝나는 이름이 붙은 빈 오브젝트 밑에서 진행하세요. " +
                  "통로 폭/위치/깊이는 조립 피벗 아래 실제로 생성된 투명 콜라이더(14~15번 참고)를 보고 맞추면 " +
                  "됩니다. 북<->서, 남<->동은 LevelBuilder가 서로 회전 재사용하므로 두 방향(예: North/South)만 " +
                  "조립해도 충분합니다.");
    }

    // 실제 조립 작업물을 담을 빈 오브젝트(피벗)를 바닥/동서남북 각 위치의 정중앙에 정확히
    // 생성합니다. "가이드 생성"을 다시 눌러도 이미 존재하면 건드리지 않으므로, 조립 중인
    // 작업물이 날아갈 걱정 없이 시각 가이드(박스들)만 다시 만들어볼 수 있습니다.
    //
    // [사용 방법]
    // 예를 들어 북쪽 벽을 조립하려면, "North_pivots" 오브젝트 밑에 빌딩/화분/
    // 바리케이드 등 소품들을 자식으로 배치하세요. 이 피벗 오브젝트의 위치가 곧
    // LevelBuilder가 실제로 프리팹을 꽂아넣는 기준점(wallPos)과 정확히 일치하므로,
    // 다 조립한 뒤 이 피벗 오브젝트를 통째로 프로젝트 창에 드래그해서 프리팹으로 저장하면
    // 위치/피벗을 손으로 맞출 필요 없이 항상 정확하게 재현됩니다. 북<->서, 남<->동은
    // LevelBuilder가 서로 회전 재사용하므로, 예를 들어 North/South 피벗만 조립해도
    // West/East는 자동으로 채워집니다 - West/East 피벗은 방향별로 다른 디자인을 주고
    // 싶을 때만 별도로 조립하세요.
    [ContextMenu("조립 피벗 생성 (이미 있으면 건너뜀)")]
    public void GeneratePivotAnchors()
    {
        var existingPivotRoot = transform.Find(PivotRootName);
        if (existingPivotRoot != null)
        {
            Debug.Log("RoomAssemblyGuide: 조립 피벗이 이미 존재해 다시 만들지 않았습니다 (작업물 보호). " +
                      "다시 만들고 싶으면 '조립 피벗 초기화'를 사용하세요 (기존 작업물이 전부 삭제됩니다).");
            return;
        }

        BuildPivotAnchors();
    }

    // 기존 피벗과 그 안의 모든 작업물을 삭제하고 새로 만듭니다. 되돌릴 수 없으니
    // 안에 조립해둔 게 있다면 먼저 프리팹으로 저장했는지 확인하세요.
    [ContextMenu("조립 피벗 초기화 (주의: 작업물 전부 삭제됨)")]
    public void ResetPivotAnchors()
    {
        var existingPivotRoot = transform.Find(PivotRootName);
        if (existingPivotRoot != null)
        {
            if (Application.isPlaying) Destroy(existingPivotRoot.gameObject);
            else DestroyImmediate(existingPivotRoot.gameObject);
        }

        BuildPivotAnchors();
    }

    private void BuildPivotAnchors()
    {
        var pivotRoot = new GameObject(PivotRootName);
        pivotRoot.transform.SetParent(transform, false);
        pivotRoot.transform.localPosition = Vector3.zero;

        CreatePivotAnchor(pivotRoot.transform, "Floor_pivots", Vector3.zero);

        foreach (var dir in new[] { Direction.North, Direction.East, Direction.South, Direction.West })
        {
            var pivot = CreatePivotAnchor(pivotRoot.transform, $"{dir}_pivots", GetWallOffset(dir));

            if (generateBoundaryColliders)
            {
                CreateClosedBoundaryCollider(pivot, dir);
                CreateOpenBoundaryGroup(pivot, dir);
            }
        }

        Debug.Log("RoomAssemblyGuide: 조립 피벗 5개(바닥/북/동/남/서) 생성 완료. " +
                  "각 피벗 오브젝트 밑에 소품을 배치한 뒤, 피벗 오브젝트를 통째로 드래그해 프리팹으로 저장하세요. " +
                  "북<->서, 남<->동은 LevelBuilder가 서로 회전 재사용하니 두 방향만 조립해도 충분합니다. " +
                  (generateBoundaryColliders
                      ? "실제로 작동하는 경계 콜라이더도 함께 생성했습니다 - 어느 쪽 프리팹을 만드는지에 따라 " +
                        "'_open' 또는 '_close'가 붙은 오브젝트를 지우고 저장하세요 " +
                        "(자세한 내용은 스크립트 상단 주석 14~15번 참고)."
                      : "generateBoundaryColliders가 꺼져 있어 실제 경계 콜라이더는 생성하지 않았습니다."));
    }

    // 이미 조립 피벗이 있는 상태에서 cellSize/passageWidth/alleyFloorOverlap 등 치수를
    // 나중에 조정했을 때, 형빈이 이미 조립해둔 아트 오브젝트는 건드리지 않고 경계
    // 콜라이더(close/open 그룹)만 최신 치수로 다시 만듭니다.
    [ContextMenu("경계 콜라이더만 다시 생성 (조립한 아트는 유지)")]
    public void RegenerateBoundaryColliders()
    {
        var pivotRoot = transform.Find(PivotRootName);
        if (pivotRoot == null)
        {
            Debug.LogWarning("RoomAssemblyGuide: 조립 피벗이 아직 없습니다. 먼저 '조립 피벗 생성'을 실행해주세요.");
            return;
        }

        foreach (var dir in new[] { Direction.North, Direction.East, Direction.South, Direction.West })
        {
            var pivot = pivotRoot.Find($"{dir}_pivots");
            if (pivot == null) continue;

            RemoveExistingBoundaryColliders(pivot);

            if (generateBoundaryColliders)
            {
                CreateClosedBoundaryCollider(pivot, dir);
                CreateOpenBoundaryGroup(pivot, dir);
            }
        }

        Debug.Log("RoomAssemblyGuide: 경계 콜라이더를 최신 치수로 다시 생성했습니다 (조립해둔 아트 오브젝트는 그대로 유지됩니다).");
    }

    private void RemoveExistingBoundaryColliders(Transform pivot)
    {
        for (int i = pivot.childCount - 1; i >= 0; i--)
        {
            var child = pivot.GetChild(i);
            if (child.name.Contains("_close") || child.name.Contains("_open"))
            {
                DestroyImmediate(child.gameObject);
            }
        }
    }

    // 방향 하나를 완전히 막는 콜라이더입니다. 골목이 없는(이웃이 없는) closedWalls 프리팹을
    // 조립할 때 그대로 쓰고, openWalls(골목형) 프리팹을 조립할 때는 통째로 지우면 됩니다.
    private void CreateClosedBoundaryCollider(Transform pivot, Direction dir)
    {
        bool isNorthSouth = (dir == Direction.North || dir == Direction.South);

        var go = new GameObject($"{dir}_close");
        go.transform.SetParent(pivot, false);
        go.transform.localPosition = new Vector3(0f, wallHeight / 2f, 0f);
        go.transform.localRotation = Quaternion.identity;

        var box = go.AddComponent<BoxCollider>();
        box.size = isNorthSouth
            ? new Vector3(cellSize, wallHeight, wallThickness)
            : new Vector3(wallThickness, wallHeight, cellSize);

        SafeSetWallTag(go);

        var gizmo = go.AddComponent<InvisibleColliderGizmo>();
        gizmo.gizmoColor = new Color(0.6f, 0.6f, 0.6f, 0.4f);
    }

    // 골목(통행로)이 있는 방향을 위한 콜라이더 묶음입니다:
    // - 옆쪽 콜라이더 2개(경계선/입구 위치, 통행로 폭 바깥쪽을 얇게 막음)
    // - 통로 옆 칸막이 2개(입구~출구 구간을 잇는, 통행로 폭 경계에 딱 붙는 얇은 벽 -
    //   플레이어와 총알을 둘 다 막습니다. 옆쪽 콜라이더와 탄환 전용 경계 사이에 원래
    //   비어 있던 구간을 메꿔서, 통로 안에서 옆으로 빠져나가는 것을 막기 위한 것입니다)
    // - 게이트 상태와 무관하게 항상 총알만 막는 탄환 전용 경계 1개(출구 위치, 플레이어는 통과)
    // closedWalls(완전히 막힌) 프리팹을 조립할 때는 이 그룹을 통째로 지우면 됩니다.
    private void CreateOpenBoundaryGroup(Transform pivot, Direction dir)
    {
        var group = new GameObject($"{dir}_open");
        group.transform.SetParent(pivot, false);
        group.transform.localPosition = Vector3.zero;
        group.transform.localRotation = Quaternion.identity;

        bool isNorthSouth = (dir == Direction.North || dir == Direction.South);
        float sideWidth = Mathf.Max(0f, (cellSize - passageWidth) / 2f);

        if (sideWidth > 0f)
        {
            float centerOffset = passageWidth / 2f + sideWidth / 2f;
            CreateOpenSidePiece(group.transform, dir, isNorthSouth, +centerOffset, sideWidth, "A");
            CreateOpenSidePiece(group.transform, dir, isNorthSouth, -centerOffset, sideWidth, "B");
        }
        else
        {
            Debug.LogWarning($"RoomAssemblyGuide: {dir} 방향은 통행로 폭(passageWidth)이 Cell Size 이상이라 " +
                              "옆 벽 콜라이더를 만들지 않았습니다. passageWidth를 줄여주세요.");
        }

        CreateSideConnector(group.transform, dir, isNorthSouth, +passageWidth / 2f, "A");
        CreateSideConnector(group.transform, dir, isNorthSouth, -passageWidth / 2f, "B");

        CreateBulletOnlyBoundaryCollider(group.transform, dir);
    }

    // 통로 양옆에서, 통행로 폭(passageWidth) 바깥쪽을 막는 콜라이더입니다. 경계선(입구)
    // 위치에 두께(wallThickness)만큼만 얇게 존재하며, closedWalls의 전체 폭 벽과 같은
    // 성격(입구 지점에서 항상 플레이어를 막는 벽)입니다.
    private void CreateOpenSidePiece(Transform parent, Direction dir, bool isNorthSouth, float lengthOffset, float sideWidth, string suffix)
    {
        var go = new GameObject($"Side_{suffix}");
        go.transform.SetParent(parent, false);

        go.transform.localPosition = isNorthSouth
            ? new Vector3(lengthOffset, wallHeight / 2f, 0f)
            : new Vector3(0f, wallHeight / 2f, lengthOffset);
        go.transform.localRotation = Quaternion.identity;

        var box = go.AddComponent<BoxCollider>();
        box.size = isNorthSouth
            ? new Vector3(sideWidth, wallHeight, wallThickness)
            : new Vector3(wallThickness, wallHeight, sideWidth);

        SafeSetWallTag(go);

        var gizmo = go.AddComponent<InvisibleColliderGizmo>();
        gizmo.gizmoColor = new Color(0.3f, 0.85f, 0.4f, 0.35f);
    }

    // 통로 옆 칸막이입니다. 옆쪽 콜라이더(위, 입구에서만 얇게 막음)와 탄환 전용 경계
    // (출구에서 총알만 막음) 사이에는 원래 아무것도 없어서, 플레이어가 통로 안으로 걸어
    // 들어간 뒤 옆으로 빠져나갈 수 있는 빈틈이 있었습니다. 이 칸막이는 통행로 폭
    // (passageWidth)의 정확한 양 끝 경계에 붙어서, 입구(경계선)부터 출구(탄환 전용 경계
    // 위치)까지 이어지는 얇은 벽으로 그 빈틈을 메웁니다. 탄환 전용 경계와 달리 플레이어와
    // 총알을 둘 다 막는 일반 벽입니다(플레이어가 방 사이에 끼거나, 총알이 옆으로 새는
    // 것을 막기 위한 안전장치).
    private void CreateSideConnector(Transform parent, Direction dir, bool isNorthSouth, float lateralOffset, string suffix)
    {
        var go = new GameObject($"Connector_{suffix}");
        go.transform.SetParent(parent, false);

        // alleyFloorOverlap이 0으로 설정된 경우를 대비해 최소 두께(wallThickness)는 보장합니다.
        float depth = Mathf.Max(alleyFloorOverlap, wallThickness);

        // 경계선(입구)에서 이웃 방 쪽(바깥쪽, 출구)으로 depth/2만큼 나간 위치를 중심으로
        // 잡아서, 박스가 정확히 [입구 ~ 출구] 구간을 덮도록 합니다.
        Vector3 outwardDir = GetWallOffset(dir).normalized;
        Vector3 depthCenterOffset = outwardDir * (depth / 2f);

        go.transform.localPosition = (isNorthSouth
            ? new Vector3(lateralOffset, wallHeight / 2f, 0f)
            : new Vector3(0f, wallHeight / 2f, lateralOffset)) + depthCenterOffset;
        go.transform.localRotation = Quaternion.identity;

        var box = go.AddComponent<BoxCollider>();
        box.size = isNorthSouth
            ? new Vector3(wallThickness, wallHeight, depth)
            : new Vector3(depth, wallHeight, wallThickness);

        SafeSetWallTag(go);

        var gizmo = go.AddComponent<InvisibleColliderGizmo>();
        gizmo.gizmoColor = new Color(1f, 0.6f, 0.2f, 0.4f); // 주황색 계열로 구분 (칸막이)
    }

    // 게이트(바리케이드) 상태와 무관하게 총알을 항상 막는 콜라이더입니다. 경계선이 아니라
    // 골목 바닥 오버랩이 끝나는 바깥쪽(이웃 방 쪽) 끝자락에 위치합니다. Player와는
    // 충돌하지 않도록 별도 물리 레이어를 지정합니다.
    private void CreateBulletOnlyBoundaryCollider(Transform parent, Direction dir)
    {
        bool isNorthSouth = (dir == Direction.North || dir == Direction.South);

        var go = new GameObject("BulletOnly");
        go.transform.SetParent(parent, false);

        Vector3 outwardDir = GetWallOffset(dir).normalized;
        Vector3 farEdgeLocal = outwardDir * alleyFloorOverlap;

        go.transform.localPosition = farEdgeLocal + new Vector3(0f, wallHeight / 2f, 0f);
        go.transform.localRotation = Quaternion.identity;

        var box = go.AddComponent<BoxCollider>();
        box.size = isNorthSouth
            ? new Vector3(passageWidth, wallHeight, bulletBoundaryThickness)
            : new Vector3(bulletBoundaryThickness, wallHeight, passageWidth);

        SafeSetWallTag(go);

        int layerIndex = LayerMask.NameToLayer(bulletOnlyLayerName);
        if (layerIndex >= 0)
        {
            go.layer = layerIndex;
        }
        else
        {
            Debug.LogWarning($"RoomAssemblyGuide: '{bulletOnlyLayerName}' 레이어가 아직 프로젝트에 없습니다. " +
                              "Edit > Project Settings > Tags and Layers에서 먼저 만들고, Physics 설정의 " +
                              "Layer Collision Matrix에서 이 레이어와 Player 레이어의 충돌을 꺼주세요. " +
                              "지금은 기본 레이어로 생성됩니다.");
        }

        var gizmo = go.AddComponent<InvisibleColliderGizmo>();
        gizmo.gizmoColor = new Color(1f, 0.55f, 0.1f, 0.5f);
    }

    // Wall 태그가 프로젝트에 아직 등록되어 있지 않으면 SetTag가 예외를 던지므로, 여기서
    // 잡아서 경고 로그로 안내만 하고 넘어갑니다 (콜라이더 자체는 정상적으로 생성됩니다).
    private void SafeSetWallTag(GameObject go)
    {
        try
        {
            go.tag = "Wall";
        }
        catch (UnityException)
        {
            Debug.LogWarning($"RoomAssemblyGuide: '{go.name}'에 Wall 태그를 지정하지 못했습니다. " +
                              "프로젝트에 Wall 태그가 아직 없다면 Edit > Project Settings > Tags and Layers에서 먼저 추가해주세요.");
        }
    }

    private Transform CreatePivotAnchor(Transform parent, string name, Vector3 localPosition)
    {
        var anchor = new GameObject(name);
        anchor.transform.SetParent(parent, false);
        anchor.transform.localPosition = localPosition;
        anchor.transform.localRotation = Quaternion.identity;
        return anchor.transform;
    }

    [ContextMenu("가이드 삭제")]
    public void ClearGuide()
    {
        var existing = transform.Find(GuideRootName);
        if (existing != null)
        {
            if (Application.isPlaying) Destroy(existing.gameObject);
            else DestroyImmediate(existing.gameObject);
        }
    }

    private void CreateFloorGuide(Transform parent)
    {
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Floor_slot";
        floor.transform.SetParent(parent, false);

        float floorSize = cellSize + floorOverlapMargin;
        floor.transform.localPosition = new Vector3(0f, -0.1f, 0f);
        floor.transform.localScale = new Vector3(floorSize, 0.2f, floorSize);

        ApplyGuideLook(floor, new Color(0.6f, 0.6f, 0.6f, guideAlpha));
    }

    private void CreateWallGuide(Transform parent, Direction dir, string name, Color color)
    {
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = name;
        wall.transform.SetParent(parent, false);

        Vector3 offset = GetWallOffset(dir);
        bool isNorthSouth = (dir == Direction.North || dir == Direction.South);

        wall.transform.localPosition = offset + new Vector3(0f, wallHeight / 2f, 0f);
        wall.transform.localRotation = Quaternion.identity;
        wall.transform.localScale = isNorthSouth
            ? new Vector3(cellSize, wallHeight, wallThickness)
            : new Vector3(wallThickness, wallHeight, cellSize);

        ApplyGuideLook(wall, color);
    }

    // 가이드는 실제 게임 로직과 무관하므로 콜라이더를 제거하고, 조립 중 실수로 총알 판정
    // 등에 걸리지 않도록 Wall 태그도 붙이지 않습니다.
    private void ApplyGuideLook(GameObject go, Color color)
    {
        var collider = go.GetComponent<Collider>();
        if (collider != null) DestroyImmediate(collider);

        var rend = go.GetComponent<Renderer>();
        if (rend != null)
        {
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;

            // URP 프로젝트에서 기본 Standard 셰이더는 핑크색으로 보일 수 있어, URP 셰이더를
            // 우선 찾고 없으면 순서대로 대체 셰이더를 사용합니다.
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Standard")
                ?? Shader.Find("Sprites/Default");

            if (shader != null)
            {
                var mat = new Material(shader) { color = color };
                rend.material = mat;
            }
        }
    }

    private Vector3 GetWallOffset(Direction dir)
    {
        float half = cellSize / 2f;
        switch (dir)
        {
            case Direction.North: return new Vector3(0f, 0f, half);
            case Direction.South: return new Vector3(0f, 0f, -half);
            case Direction.East: return new Vector3(half, 0f, 0f);
            case Direction.West: return new Vector3(-half, 0f, 0f);
            default: return Vector3.zero;
        }
    }
}