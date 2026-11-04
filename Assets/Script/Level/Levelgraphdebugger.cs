using System.Collections.Generic;
using UnityEngine;

// 레벨 생성 결과를 씬 뷰에 색깔 큐브와 연결선으로 그려서 눈으로 확인하기 위한 스크립트입니다.
// 실제 프리팹 배치와는 무관하며, 오직 검증 목적으로만 사용합니다.
public class LevelGraphDebugger : MonoBehaviour
{
    [Header("검증할 레벨 설정")]
    public RoomGenerationConfig config;

    [Header("Gizmo 표시")]
    public float cellSize = 3f;

    private List<RoomInstance> generatedRooms;

    [ContextMenu("Generate Level")]
    public void Generate()
    {
        if (config == null)
        {
            Debug.LogWarning("config가 비어있습니다. RoomGenerationConfig 에셋을 연결해주세요.");
            return;
        }

        var generator = new LevelGraphGenerator(config);
        generatedRooms = generator.Generate();

        if (generatedRooms == null)
        {
            Debug.LogWarning("생성 실패: maxGenerationAttempts를 초과했습니다.");
            return;
        }

        Debug.Log($"[레벨 {config.levelNumber}] 방 {generatedRooms.Count}개 생성 완료");
        foreach (var room in generatedRooms)
        {
            Debug.Log($"  - {room.occupiedCells[0]} : {room.category} {room.variantId} (앵커 {room.anchors.Count}개)");
        }
    }

    void OnDrawGizmos()
    {
        if (generatedRooms == null) return;

        foreach (var room in generatedRooms)
        {
            var coord = room.occupiedCells[0];
            var worldPos = new Vector3(coord.x * cellSize, 0f, coord.y * cellSize);

            Gizmos.color = GetColor(room.category);
            Gizmos.DrawCube(worldPos, Vector3.one * (cellSize * 0.8f));

#if UNITY_EDITOR
            string label = string.IsNullOrEmpty(room.variantId)
                ? room.category.ToString()
                : $"{room.category}\n{room.variantId}";
            UnityEditor.Handles.Label(worldPos + Vector3.up * 1.5f, label);
#endif
        }

        var drawn = new HashSet<AnchorLink>();
        foreach (var room in generatedRooms)
        {
            foreach (var link in room.anchors)
            {
                if (drawn.Contains(link)) continue;
                drawn.Add(link);

                var posA = new Vector3(link.cellA.x * cellSize, 0.2f, link.cellA.y * cellSize);
                var posB = new Vector3(link.cellB.x * cellSize, 0.2f, link.cellB.y * cellSize);
                Gizmos.color = Color.white;
                Gizmos.DrawLine(posA, posB);
            }
        }
    }

    Color GetColor(RoomCategory category)
    {
        switch (category)
        {
            case RoomCategory.Start: return Color.green;
            case RoomCategory.Boss: return Color.red;
            case RoomCategory.Club: return Color.yellow;
            case RoomCategory.Store: return Color.cyan;
            case RoomCategory.Special: return new Color(1f, 0.5f, 0f);
            case RoomCategory.Normal: return Color.gray;
            default: return Color.white;
        }
    }
}