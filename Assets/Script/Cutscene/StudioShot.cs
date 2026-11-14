using UnityEngine;

// [촬영 스튜디오 - 장면 하나]
// 장면마다 세트(바닥/벽), 손으로 놓은 소품, 구도/위치 표시, 대본을 따로 가집니다. CutsceneStudio 아래에 장면 수만큼 둡니다.
// - Set: [세트 짓기]가 만드는 바닥/벽. 다시 지으면 통째로 새로 만들어지므로 여기에는 손으로 아무것도 넣지 않습니다.
// - Props: 손으로 놓는 소품 자리. 세트를 다시 지어도 그대로 남습니다.
// - Marks: 구도/위치 표시(ShotMark).
// 바닥/벽 위치는 LevelBuilder의 배치 공식과 같습니다(벽은 칸 중심에서 cellSize/2 떨어진 곳, 북 = +Z, 동 = +X).
public class StudioShot : MonoBehaviour
{
    public const string SetRootName = "Set";
    public const string PropsRootName = "Props";
    public const string MarksRootName = "Marks";

    [Header("대본")]
    [Tooltip("이 장면의 대본(텍스트 파일)입니다. [새 장면 만들기]가 자동으로 만들어 연결합니다.")]
    public TextAsset script;

    [Header("세트 - 크기 (LevelBuilder와 같은 값)")]
    [Tooltip("가로(X, 동서) 칸 수입니다. 방 한 칸 = 1.")]
    [Min(1)] public int cellsX = 1;
    [Tooltip("세로(Z, 남북) 칸 수입니다. 방 한 칸 = 1.")]
    [Min(1)] public int cellsZ = 1;
    [Tooltip("한 칸의 한 변 길이(m)입니다. LevelBuilder의 Cell Size와 같게 둡니다.")]
    [Min(0.1f)] public float cellSize = 6f;
    [Tooltip("바닥 프리팹을 비웠을 때 만드는 임시 바닥이 칸보다 이만큼 큽니다(LevelBuilder의 Floor Overlap Margin).")]
    [Min(0f)] public float floorOverlapMargin = 1f;

    [Header("세트 - 바닥")]
    [Tooltip("칸마다 놓을 바닥 프리팹입니다. 비우면 회색 임시 바닥을 만듭니다.")]
    public GameObject floorPrefab;

    [Header("세트 - 벽 (세트 바깥 둘레에만 세움)")]
    [Tooltip("북쪽(+Z, 화면 오른쪽 위) 벽을 세울지입니다.")]
    public bool buildNorthWall = true;
    [Tooltip("북쪽 벽 프리팹입니다. 비우면 서쪽 프리팹을 돌려서 씁니다(게임과 같은 규칙).")]
    public GameObject northWallPrefab;
    [Tooltip("서쪽(-X, 화면 왼쪽 위) 벽을 세울지입니다.")]
    public bool buildWestWall = true;
    [Tooltip("서쪽 벽 프리팹입니다. 비우면 북쪽 프리팹을 돌려서 씁니다.")]
    public GameObject westWallPrefab;
    [Tooltip("남쪽(-Z, 화면 왼쪽 아래 - 게임 카메라에 가까운 쪽) 벽을 세울지입니다.")]
    public bool buildSouthWall = true;
    [Tooltip("남쪽 벽 프리팹입니다. 비우면 동쪽 프리팹을 돌려서 씁니다.")]
    public GameObject southWallPrefab;
    [Tooltip("동쪽(+X, 화면 오른쪽 아래 - 게임 카메라에 가까운 쪽) 벽을 세울지입니다.")]
    public bool buildEastWall = true;
    [Tooltip("동쪽 벽 프리팹입니다. 비우면 남쪽 프리팹을 돌려서 씁니다.")]
    public GameObject eastWallPrefab;
    [Tooltip("켜면 열린 벽 프리팹의 바리케이드를 보이게 둡니다. 끄면 게임에서 방이 열려 있을 때처럼 숨깁니다.")]
    public bool showBarricades = false;

    [Header("분위기 (Play 중에만 적용)")]
    [Tooltip("레벨 조명 설정입니다. 비우면 1레벨과 같은 낮 조명입니다.")]
    public LevelLightingProfile lightingProfile;
    [Tooltip("화면 배경(하늘) 재질입니다. 비우면 씬의 원래 배경입니다.")]
    public Material backgroundMaterial;

    public Transform SetRoot { get { return transform.Find(SetRootName); } }
    public Transform PropsRoot { get { return transform.Find(PropsRootName); } }
    public Transform MarksRoot { get { return transform.Find(MarksRootName); } }

    // 이름으로 구도/위치 표시를 찾습니다(꺼진 표시 포함, 대소문자 구분 없음).
    public ShotMark FindMark(string markName)
    {
        if (string.IsNullOrEmpty(markName)) return null;
        foreach (var mark in GetComponentsInChildren<ShotMark>(true))
        {
            if (string.Equals(mark.name.Trim(), markName, System.StringComparison.OrdinalIgnoreCase)) return mark;
        }
        return null;
    }

    // 칸 (ix, iz)의 중심(이 장면 기준 로컬 위치)입니다. 세트 전체의 중심이 장면 원점에 오도록 놓습니다.
    public Vector3 CellLocalCenter(int ix, int iz)
    {
        float x = (ix - (cellsX - 1) * 0.5f) * cellSize;
        float z = (iz - (cellsZ - 1) * 0.5f) * cellSize;
        return new Vector3(x, 0f, z);
    }
}
