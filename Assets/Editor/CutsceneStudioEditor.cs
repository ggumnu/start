using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// [촬영 스튜디오 - 에디터 도구]
// - 메뉴 MobArchive/촬영 스튜디오/씬 만들기: LevelBuilder 씬을 복사해 조명/후처리/하늘 설정이 게임과 같은 촬영 전용 씬을 만듭니다.
//   (씬을 처음부터 새로 만들면 Lighting 창의 환경 설정이 게임과 달라져 화면 색이 어긋나므로 복사 방식을 씁니다.)
//   촬영 씬은 에디터 전용이라 Build Profiles에는 등록하지 않습니다.
// - CutsceneStudio 인스펙터: 재생할 장면 고르기, 새 장면 만들기(대본 파일도 함께 만듦).
// - StudioShot 인스펙터: 세트 짓기, 구도 저장, 위치 저장.
// - 장면 안에 놓은 적 프리팹: NavMeshAgent를 씬에서 자동으로 꺼 둠(아래 QuietPlacedEnemies).
// - ShotMark 인스펙터: 구도를 선택하면 촬영 카메라가 그 자리로 옮겨져 Game 창에 실제 화면이 보입니다.
//
// [Play 중 구도 저장] 캐릭터의 머리카락/옷/총은 Play 중에만 붙으므로, 캐릭터가 들어간 구도는 Play를 일시정지하고 찍습니다.
// Play 중에 만든 오브젝트는 Play를 끝내면 사라지기 때문에, 찍은 값을 따로 적어 두었다가(SessionState) Play가 끝난 직후
// 편집 상태의 씬에 다시 만들어 넣습니다. Play 중에 표시를 손으로 옮긴 것은 남지 않습니다([다시 찍기] 버튼으로 찍은 것만 남음).
[InitializeOnLoad]
public static class CutsceneStudioTools
{
    private const string PendingKey = "MobArchive.CutsceneStudio.PendingMarks";

    [System.Serializable]
    private class PendingMark
    {
        public string shot;
        public string mark;
        public int kind;
        public Vector3 position;
        public Quaternion rotation;
        public float fieldOfView;
    }

    [System.Serializable]
    private class PendingList
    {
        public List<PendingMark> items = new List<PendingMark>();
    }

    static CutsceneStudioTools()
    {
        EditorApplication.playModeStateChanged -= HandlePlayModeChanged;
        EditorApplication.playModeStateChanged += HandlePlayModeChanged;
        EditorApplication.hierarchyChanged -= QuietPlacedEnemies;
        EditorApplication.hierarchyChanged += QuietPlacedEnemies;
    }

    // 장면 안에 적 프리팹을 놓으면 그 적의 NavMeshAgent를 씬에서 꺼 둡니다(프리팹 파일은 그대로).
    // 촬영 씬에는 NavMesh가 없어서, 켜진 채로 Play하면 "Failed to create agent" 경고가 Console에 나옵니다.
    private static void QuietPlacedEnemies()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var studio = Object.FindFirstObjectByType<CutsceneStudio>(FindObjectsInactive.Include);
        if (studio == null) return;

        foreach (EnemyStats stats in studio.GetComponentsInChildren<EnemyStats>(true))
        {
            var agent = stats.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent == null || !agent.enabled) continue;
            agent.enabled = false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(agent);
            EditorSceneManager.MarkSceneDirty(stats.gameObject.scene);
        }
    }

    private static void HandlePlayModeChanged(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.EnteredEditMode) ApplyPendingMarks();
    }

    // Play 중에 찍은(또는 다시 찍은) 표시를 적어 둡니다. 같은 장면의 같은 이름은 마지막 값으로 덮어씁니다.
    private static void RememberMark(ShotMark mark)
    {
        StudioShot shot = mark.GetComponentInParent<StudioShot>(true);
        if (shot == null) return;

        PendingList list = LoadPending();
        list.items.RemoveAll(m => m.shot == shot.name && string.Equals(m.mark, mark.name, System.StringComparison.OrdinalIgnoreCase));
        list.items.Add(new PendingMark
        {
            shot = shot.name,
            mark = mark.name,
            kind = (int)mark.kind,
            position = mark.transform.position,
            rotation = mark.transform.rotation,
            fieldOfView = mark.fieldOfView
        });
        SessionState.SetString(PendingKey, JsonUtility.ToJson(list));
    }

    private static PendingList LoadPending()
    {
        string json = SessionState.GetString(PendingKey, "");
        PendingList list = string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<PendingList>(json);
        return list ?? new PendingList();
    }

    private static void ApplyPendingMarks()
    {
        PendingList list = LoadPending();
        SessionState.EraseString(PendingKey);
        if (list.items.Count == 0) return;

        CutsceneStudio studio = FindStudio();
        if (studio == null)
        {
            Debug.LogWarning("[촬영] Play 중에 찍은 구도 " + list.items.Count + "개를 넣을 촬영 씬을 찾지 못했습니다.");
            return;
        }

        int applied = 0;
        foreach (PendingMark pending in list.items)
        {
            StudioShot shot = studio.GetShots().Find(x => x.name == pending.shot);
            if (shot == null)
            {
                Debug.LogWarning("[촬영] 장면 '" + pending.shot + "'을(를) 찾지 못해 표시 '" + pending.mark + "'을(를) 넣지 못했습니다.");
                continue;
            }

            ShotMark mark = shot.FindMark(pending.mark);
            if (mark == null)
            {
                Transform marks = EnsureChild(shot.transform, StudioShot.MarksRootName);
                var go = new GameObject(pending.mark);
                Undo.RegisterCreatedObjectUndo(go, "Play 중 구도 저장");
                go.transform.SetParent(marks, false);
                mark = go.AddComponent<ShotMark>();
            }
            else
            {
                Undo.RecordObject(mark.transform, "Play 중 구도 저장");
                Undo.RecordObject(mark, "Play 중 구도 저장");
            }
            mark.transform.SetPositionAndRotation(pending.position, pending.rotation);
            mark.kind = (ShotMark.MarkKind)pending.kind;
            mark.fieldOfView = pending.fieldOfView;
            EditorUtility.SetDirty(mark);
            applied++;
        }

        if (applied > 0)
        {
            EditorSceneManager.MarkSceneDirty(studio.gameObject.scene);
            Debug.Log("[촬영] Play 중에 찍은 구도/위치 " + applied + "개를 씬에 넣었습니다. Ctrl+S로 씬을 저장해주세요.");
        }
    }

    // Play 중에는 Undo/씬 변경 표시를 쓸 수 없으므로 갈라서 처리합니다.
    private static void FinishNewMark(StudioShot shot, GameObject go, string undoName)
    {
        if (EditorApplication.isPlaying)
        {
            RememberMark(go.GetComponent<ShotMark>());
        }
        else
        {
            Undo.RegisterCreatedObjectUndo(go, undoName);
            EditorSceneManager.MarkSceneDirty(shot.gameObject.scene);
        }
        Selection.activeGameObject = go; // 선택되면 ShotMark 인스펙터가 촬영 카메라를 이 구도로 옮김
    }

    public static void RecaptureMark(ShotMark mark, SceneView view)
    {
        if (!EditorApplication.isPlaying) Undo.RecordObject(mark.transform, "구도 다시 찍기");
        mark.transform.SetPositionAndRotation(view.camera.transform.position, view.camera.transform.rotation);
        if (EditorApplication.isPlaying) RememberMark(mark);
        else EditorSceneManager.MarkSceneDirty(mark.gameObject.scene);
    }

    private static Transform MarksRootOf(StudioShot shot)
    {
        if (!EditorApplication.isPlaying) return EnsureChild(shot.transform, StudioShot.MarksRootName);
        Transform marks = shot.MarksRoot;
        if (marks != null) return marks;
        var go = new GameObject(StudioShot.MarksRootName);
        go.transform.SetParent(shot.transform, false);
        return go.transform;
    }

    public const string SourceScenePath = "Assets/Scenes/LevelBuilder.unity";
    public const string StudioScenePath = "Assets/Scenes/CutsceneStudio.unity";
    public const string GameCameraPrefabPath = "Assets/Config/Level/GameCamera.prefab";
    public const string ShotScriptFolder = "Assets/Cutscene/Shots";
    public const string PlayerPrefabPath = "Assets/Player/Character/Player/SD_Player.prefab";
    public const string HudPrefabPath = "Assets/Config/Level/PlayerHUD.prefab";

    [MenuItem("MobArchive/촬영 스튜디오/씬 만들기")]
    private static void CreateStudioScene()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("촬영 스튜디오", "Play를 멈춘 뒤에 다시 눌러주세요.", "확인");
            return;
        }

        // 이미 있으면 절대 덮어쓰지 않고 열기만 합니다(그동안 만든 장면을 지키기 위함).
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(StudioScenePath) != null)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(StudioScenePath, OpenSceneMode.Single);
            EditorUtility.DisplayDialog("촬영 스튜디오", "촬영 씬이 이미 있어서 그 씬을 열었습니다.\n(" + StudioScenePath + ")", "확인");
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SourceScenePath) == null)
        {
            EditorUtility.DisplayDialog("촬영 스튜디오", "복사할 게임 씬을 찾지 못했습니다.\n(" + SourceScenePath + ")", "확인");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        if (!AssetDatabase.CopyAsset(SourceScenePath, StudioScenePath))
        {
            EditorUtility.DisplayDialog("촬영 스튜디오", "씬을 복사하지 못했습니다. Console 창을 확인해주세요.", "확인");
            return;
        }

        var scene = EditorSceneManager.OpenScene(StudioScenePath, OpenSceneMode.Single);

        // 조명 / 후처리(Volume) / 카메라만 남기고 게임 진행용 오브젝트는 모두 지웁니다.
        Camera cam = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            var rootCamera = root.GetComponent<Camera>();
            bool keep = root.GetComponent<Light>() != null || root.GetComponent("Volume") != null || rootCamera != null;
            if (!keep)
            {
                Object.DestroyImmediate(root);
                continue;
            }
            if (rootCamera != null && cam == null) cam = rootCamera;
        }

        if (cam == null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GameCameraPrefabPath);
            if (prefab != null)
            {
                var instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
                if (instance != null) cam = instance.GetComponent<Camera>();
            }
        }
        if (cam == null)
        {
            var camObject = new GameObject("StudioCamera");
            camObject.tag = "MainCamera";
            cam = camObject.AddComponent<Camera>();
            cam.fieldOfView = 60f;
            Debug.LogWarning("[촬영] 게임 카메라를 찾지 못해 기본 카메라를 만들었습니다. 후처리 설정이 게임과 다를 수 있습니다.");
        }

        cam.gameObject.name = "StudioCamera";
        var follow = cam.GetComponent<CameraFollow>();
        if (follow != null) follow.enabled = false;

        var studioObject = new GameObject("CutsceneStudio");
        var studio = studioObject.AddComponent<CutsceneStudio>();
        studio.studioCamera = cam;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = studioObject;

        EditorUtility.DisplayDialog("촬영 스튜디오",
            "촬영 씬을 만들었습니다.\n(" + StudioScenePath + ")\n\n" +
            "Hierarchy의 CutsceneStudio를 선택하고, 인스펙터 아래쪽 '새 장면 이름'에 이름을 넣은 뒤 [새 장면 만들기]를 눌러 시작하세요.", "확인");
    }

    // 플레이어/HUD 프리팹 칸이 비어 있으면 기본 프리팹을 넣습니다(편집 중에만).
    public static void FillDefaultPrefabs(CutsceneStudio studio)
    {
        if (EditorApplication.isPlaying || (studio.playerPrefab != null && studio.hudPrefab != null)) return;
        var player = studio.playerPrefab != null ? studio.playerPrefab : AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        var hud = studio.hudPrefab != null ? studio.hudPrefab : AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);
        if (player == studio.playerPrefab && hud == studio.hudPrefab) return;
        Undo.RecordObject(studio, "촬영 스튜디오 기본 프리팹");
        studio.playerPrefab = player;
        studio.hudPrefab = hud;
        EditorUtility.SetDirty(studio);
        EditorSceneManager.MarkSceneDirty(studio.gameObject.scene);
    }

    public static CutsceneStudio FindStudio()
    {
        return Object.FindFirstObjectByType<CutsceneStudio>(FindObjectsInactive.Include);
    }

    // 고른 장면만 켜고 나머지 장면은 끕니다(장면마다 세트가 같은 자리에 겹쳐 있으므로).
    public static void ShowOnly(CutsceneStudio studio, StudioShot shot)
    {
        if (studio == null || shot == null) return;
        foreach (StudioShot other in studio.GetShots())
        {
            bool on = other == shot;
            if (other.gameObject.activeSelf == on) continue;
            Undo.RecordObject(other.gameObject, "장면 보이기");
            other.gameObject.SetActive(on);
        }
        Undo.RecordObject(studio, "장면 고르기");
        studio.currentShot = shot;
        EditorUtility.SetDirty(studio);
        EditorSceneManager.MarkSceneDirty(studio.gameObject.scene);
    }

    public static Transform EnsureChild(Transform parent, string childName)
    {
        Transform child = parent.Find(childName);
        if (child != null) return child;
        var go = new GameObject(childName);
        Undo.RegisterCreatedObjectUndo(go, "촬영 스튜디오");
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    public static StudioShot CreateShot(CutsceneStudio studio, string shotName)
    {
        shotName = (shotName ?? "").Trim();
        if (shotName.Length == 0)
        {
            EditorUtility.DisplayDialog("촬영 스튜디오", "'새 장면 이름' 칸에 이름을 먼저 넣어주세요. 예: 01_골목입장", "확인");
            return null;
        }
        if (shotName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            EditorUtility.DisplayDialog("촬영 스튜디오", "장면 이름에 파일 이름으로 쓸 수 없는 글자( \\ / : * ? \" < > | )가 있습니다.", "확인");
            return null;
        }
        foreach (StudioShot other in studio.GetShots())
        {
            if (string.Equals(other.name, shotName, System.StringComparison.OrdinalIgnoreCase))
            {
                EditorUtility.DisplayDialog("촬영 스튜디오", "같은 이름의 장면이 이미 있습니다: " + shotName, "확인");
                return null;
            }
        }

        string scriptPath = ShotScriptFolder + "/" + shotName + ".txt";
        TextAsset scriptAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(scriptPath);
        if (scriptAsset == null)
        {
            EnsureFolder(ShotScriptFolder);
            File.WriteAllText(scriptPath, ScriptTemplate(shotName).Replace("\n", "\r\n"), new UTF8Encoding(true));
            AssetDatabase.ImportAsset(scriptPath);
            scriptAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(scriptPath);
        }

        var go = new GameObject(shotName);
        Undo.RegisterCreatedObjectUndo(go, "새 장면");
        go.transform.SetParent(studio.transform, false);
        var shot = go.AddComponent<StudioShot>();
        shot.script = scriptAsset;
        EnsureChild(go.transform, StudioShot.SetRootName);
        EnsureChild(go.transform, StudioShot.PropsRootName);
        EnsureChild(go.transform, StudioShot.MarksRootName);

        ShowOnly(studio, shot);
        Selection.activeGameObject = go;
        return shot;
    }

    private static void EnsureFolder(string folder)
    {
        string[] parts = folder.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    private static string ScriptTemplate(string shotName)
    {
        return
            "// 장면 대본: " + shotName + "\n" +
            "// 한 줄 = 시간(초)  대상  명령.   // 뒤는 메모.   +1 처럼 쓰면 '바로 윗줄 시간 + 1초'.\n" +
            "//\n" +
            "//   0    카메라  A                           구도 A로 바로 전환\n" +
            "//   0.5  카메라  A > B  3초  부드럽게         A에서 B로 3초 동안 이동 (A > B > C 도 가능)\n" +
            "//   4    카메라  > C  2초                     지금 위치에서 C로 이동\n" +
            "//   0    카메라  바라보기  P1  높이 0.3  1초    P1을 계속 바라봄 (마지막 숫자 = 시선이 옮겨가는 시간)\n" +
            "//   6    카메라  바라보기  해제  1초\n" +
            "//   2    카메라  돌기  P1  90도  3초  높이 1.5  P1 주위를 돎 (+ = 위에서 볼 때 시계 방향, 높이 = 올라가는 양)\n" +
            "//   2    카메라  줌  40  2초                  화각을 40으로\n" +
            "//   3    카메라  흔들림  0.1  0.5초            세기(m), 시간\n" +
            "//   2    카메라  따라가기  플레이어            거리/방향을 유지하며 따라감 (해제: 카메라 따라가기 해제)\n" +
            "//   0    화면  밝게  1초   /   화면  어둡게  1초\n" +
            "//   -- 플레이어 (바라보기/돌기/따라가기의 대상 이름 자리에 '플레이어'를 쓸 수 있음) --\n" +
            "//   0    플레이어  등장  P1                    P1 자리에, P1이 향한 방향으로 섬\n" +
            "//   1    플레이어  걷기  P1 > P2 > P3          '걷기 P2'처럼 하나만 적으면 지금 자리에서 P2로\n" +
            "//   1    플레이어  뛰기  P1 > P2\n" +
            "//   4    플레이어  보기  P4  0.5초              '보기 카메라'면 카메라 쪽\n" +
            "//   5    플레이어  장전  1.5초\n" +
            "//   7    플레이어  포즈  짜잔                   전신 포즈 / '포즈 획득' = 상체만\n" +
            "//   9    플레이어  대기  0.6초                  포즈/장전을 풀고 대기 자세로\n" +
            "//   3    플레이어  표정  획득  2초              기본 / 피격 / 획득 / 장전 / 사망 / 뛰기\n" +
            "//   -- 적 배우 (장면의 Props 안에 놓은 적 프리팹. 이름 자리에 그 오브젝트 이름, 적이 하나뿐이면 '보스' / '적'도 됨) --\n" +
            "//   0    보스  확산충전  2초                   전방위 공격의 충전만 보여 주고 쏘지 않음\n" +
            "//   0    보스  확산공격                        충전 -> 발사 -> 회전 ('확산공격 충전 0.3초'로 충전 시간 변경)\n" +
            "//   1    보스  공격취소  /  보스  보기  플레이어  0.5초  /  보스  이동  E1 > E2  2초\n" +
            "//   0    보스  보호막생성                      (2레벨 보스) 충전 이펙트 + 보호막 생성 ('보호막생성 충전 1.2초'로 충전 시간 변경)\n" +
            "//   0    보스  보호막두르기 / 포격모션 / 조준표시 P2   (2레벨 보스) 보호막 두른 채 시작 / 포격 발동 모션 / 바닥 조준 표시만\n" +
            "//   0    플레이어  조준  0.1초                  총 조준 자세 ('조준 해제'로 풀기)\n" +
            "//   0.2  시간  0.15배                         세상(플레이어/적/총알)의 빠르기. 여러 줄이면 그 사이를 부드럽게 이음(카메라는 영향 없음)\n" +
            "//   8    끝                                   장면 끝 (녹화도 여기서 멈춤)\n" +
            "//   속도 낱말: 부드럽게(기본) / 일정하게 / 가속 / 감속\n" +
            "\n";
    }

    // ---------------------------------------------------------------- 세트 짓기

    public static void BuildSet(StudioShot shot)
    {
        Transform setRoot = EnsureChild(shot.transform, StudioShot.SetRootName);
        EnsureChild(shot.transform, StudioShot.PropsRootName);
        EnsureChild(shot.transform, StudioShot.MarksRootName);

        for (int i = setRoot.childCount - 1; i >= 0; i--)
        {
            Undo.DestroyObjectImmediate(setRoot.GetChild(i).gameObject);
        }

        float half = shot.cellSize * 0.5f;
        for (int ix = 0; ix < shot.cellsX; ix++)
        {
            for (int iz = 0; iz < shot.cellsZ; iz++)
            {
                Vector3 center = shot.CellLocalCenter(ix, iz);
                BuildFloor(shot, setRoot, center, ix, iz);

                if (iz == shot.cellsZ - 1 && shot.buildNorthWall)
                    BuildWall(shot, setRoot, center + new Vector3(0f, 0f, half), "North", shot.northWallPrefab, shot.westWallPrefab, 0f, 270f, ix, iz);
                if (iz == 0 && shot.buildSouthWall)
                    BuildWall(shot, setRoot, center + new Vector3(0f, 0f, -half), "South", shot.southWallPrefab, shot.eastWallPrefab, 180f, 90f, ix, iz);
                if (ix == shot.cellsX - 1 && shot.buildEastWall)
                    BuildWall(shot, setRoot, center + new Vector3(half, 0f, 0f), "East", shot.eastWallPrefab, shot.southWallPrefab, 90f, 180f, ix, iz);
                if (ix == 0 && shot.buildWestWall)
                    BuildWall(shot, setRoot, center + new Vector3(-half, 0f, 0f), "West", shot.westWallPrefab, shot.northWallPrefab, 270f, 0f, ix, iz);
            }
        }

        EditorSceneManager.MarkSceneDirty(shot.gameObject.scene);
    }

    private static void BuildFloor(StudioShot shot, Transform setRoot, Vector3 localCenter, int ix, int iz)
    {
        GameObject floor;
        if (shot.floorPrefab != null)
        {
            floor = PlacePrefab(shot.floorPrefab, setRoot, localCenter, 0f);
        }
        else
        {
            floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.SetParent(setRoot, false);
            floor.transform.localPosition = localCenter + new Vector3(0f, -0.1f, 0f);
            float size = shot.cellSize + shot.floorOverlapMargin;
            floor.transform.localScale = new Vector3(size, 0.2f, size);
        }
        if (floor == null) return;
        floor.name = "Floor_" + ix + "_" + iz;
        Undo.RegisterCreatedObjectUndo(floor, "세트 짓기");
    }

    // 그 방향 프리팹이 있으면 그대로, 없으면 짝 방향(북<->서, 남<->동) 프리팹을 돌려서 씁니다(LevelBuilder와 같은 규칙).
    private static void BuildWall(StudioShot shot, Transform setRoot, Vector3 localPos, string dirName,
                                  GameObject ownPrefab, GameObject partnerPrefab, float ownYaw, float partnerYaw, int ix, int iz)
    {
        GameObject prefab = ownPrefab;
        float yaw = 0f;
        if (prefab == null && partnerPrefab != null)
        {
            prefab = partnerPrefab;
            yaw = ownYaw - partnerYaw;
        }
        if (prefab == null) return;

        GameObject wall = PlacePrefab(prefab, setRoot, localPos, yaw);
        if (wall == null) return;
        wall.name = "Wall_" + dirName + "_" + ix + "_" + iz;

        // 게임에서는 열린 벽의 바리케이드가 꺼진 채로 시작하므로 여기서도 맞춥니다.
        var side = wall.GetComponent<WallSidePrefab>();
        if (side != null)
        {
            if (side.barricade != null) side.barricade.SetActive(shot.showBarricades);
            if (side.specialLockBarricade != null) side.specialLockBarricade.SetActive(false);
        }
        Undo.RegisterCreatedObjectUndo(wall, "세트 짓기");
    }

    private static GameObject PlacePrefab(GameObject prefab, Transform parent, Vector3 localPos, float yaw)
    {
        GameObject go = null;
        if (PrefabUtility.IsPartOfPrefabAsset(prefab)) go = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
        if (go == null) go = Object.Instantiate(prefab, parent); // 씬 안의 오브젝트를 넣은 경우
        if (go == null) return null;
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        return go;
    }

    // ---------------------------------------------------------------- 구도 / 위치 저장

    public static void CaptureCameraMark(StudioShot shot)
    {
        SceneView view = SceneView.lastActiveSceneView;
        if (view == null || view.camera == null)
        {
            EditorUtility.DisplayDialog("촬영 스튜디오", "Scene 창을 찾지 못했습니다. Scene 창을 한 번 클릭한 뒤 다시 눌러주세요.", "확인");
            return;
        }
        if (view.orthographic)
        {
            EditorUtility.DisplayDialog("촬영 스튜디오", "Scene 창이 평행 투영(Iso) 상태입니다. Scene 창 오른쪽 위 방향 표시 아래의 글자를 눌러 Persp로 바꾼 뒤 다시 눌러주세요.", "확인");
            return;
        }

        Transform marks = MarksRootOf(shot);
        var go = new GameObject(NextMarkName(shot, true));
        go.transform.SetParent(marks, false);
        go.transform.SetPositionAndRotation(view.camera.transform.position, view.camera.transform.rotation);
        var mark = go.AddComponent<ShotMark>();
        mark.kind = ShotMark.MarkKind.Camera;
        mark.fieldOfView = 60f;
        FinishNewMark(shot, go, "구도 저장");
    }

    public static void CapturePositionMark(StudioShot shot)
    {
        SceneView view = SceneView.lastActiveSceneView;
        Vector3 point = shot.transform.position;
        if (view != null && view.camera != null)
        {
            // Scene 창 한가운데가 바닥(장면 높이)과 만나는 곳
            Transform cam = view.camera.transform;
            var ground = new Plane(Vector3.up, shot.transform.position);
            var ray = new Ray(cam.position, cam.forward);
            if (ground.Raycast(ray, out float distance) && distance < 500f) point = ray.GetPoint(distance);
            else point = new Vector3(view.pivot.x, shot.transform.position.y, view.pivot.z);
        }

        Transform marks = MarksRootOf(shot);
        var go = new GameObject(NextMarkName(shot, false));
        go.transform.SetParent(marks, false);
        go.transform.position = point;
        var mark = go.AddComponent<ShotMark>();
        mark.kind = ShotMark.MarkKind.Position;
        FinishNewMark(shot, go, "위치 저장");
    }

    // 구도는 A, B, C ... (Z 다음은 A2, B2 ...), 위치는 P1, P2 ... 중 안 쓰인 첫 이름
    private static string NextMarkName(StudioShot shot, bool cameraMark)
    {
        var used = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        foreach (ShotMark mark in shot.GetComponentsInChildren<ShotMark>(true)) used.Add(mark.name.Trim());

        if (!cameraMark)
        {
            for (int n = 1; ; n++)
            {
                string candidate = "P" + n;
                if (!used.Contains(candidate)) return candidate;
            }
        }
        for (int round = 1; ; round++)
        {
            for (char c = 'A'; c <= 'Z'; c++)
            {
                if (c == 'P') continue; // P1, P2 ... 위치 표시와 헷갈리지 않게 구도 이름에서는 뺌
                string candidate = round == 1 ? c.ToString() : c.ToString() + round;
                if (!used.Contains(candidate)) return candidate;
            }
        }
    }

    // 촬영 카메라를 구도 자리로 옮겨 Game 창에 그 화면을 보여줍니다.
    public static void PreviewMark(ShotMark mark)
    {
        if (mark == null || mark.kind != ShotMark.MarkKind.Camera) return;
        CutsceneStudio studio = FindStudio();
        if (studio == null || studio.studioCamera == null) return;
        // Play 중에는 장면이 흐르는 동안만 건드리지 않습니다(일시정지 / 구도 잡기 / 장면이 끝난 뒤에는 미리보기 가능).
        if (EditorApplication.isPlaying && !EditorApplication.isPaused && studio.IsShotRunning) return;

        Camera cam = studio.studioCamera;
        Transform t = cam.transform;
        bool same = (t.position - mark.transform.position).sqrMagnitude < 0.0000001f &&
                    Quaternion.Angle(t.rotation, mark.transform.rotation) < 0.01f &&
                    Mathf.Abs(cam.fieldOfView - mark.fieldOfView) < 0.001f;
        if (same) return;

        t.SetPositionAndRotation(mark.transform.position, mark.transform.rotation);
        cam.fieldOfView = mark.fieldOfView;
        cam.nearClipPlane = Mathf.Max(0.01f, studio.nearClip); // 미리보기도 촬영 때와 같은 거리에서 잘리게
        UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
    }
}

[CustomEditor(typeof(CutsceneStudio))]
public class CutsceneStudioInspector : Editor
{
    private string newShotName = "";

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var studio = (CutsceneStudio)target;
        CutsceneStudioTools.FillDefaultPrefabs(studio);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("장면", EditorStyles.boldLabel);

        List<StudioShot> shots = studio.GetShots();
        if (shots.Count == 0)
        {
            EditorGUILayout.HelpBox("아직 장면이 없습니다. 아래에 이름을 넣고 [새 장면 만들기]를 눌러주세요.", MessageType.Info);
        }
        else
        {
            var names = new string[shots.Count];
            for (int i = 0; i < shots.Count; i++) names[i] = shots[i].name;
            int index = Mathf.Max(0, shots.IndexOf(studio.currentShot));

            int picked = EditorGUILayout.Popup("재생할 장면", index, names);
            if (EditorApplication.isPlaying)
            {
                if (picked != index) studio.PlayShot(shots[picked]);
            }
            else if (picked != index || studio.currentShot != shots[picked])
            {
                CutsceneStudioTools.ShowOnly(studio, shots[picked]);
            }
        }

        using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
        {
            EditorGUILayout.Space();
            newShotName = EditorGUILayout.TextField("새 장면 이름", newShotName);
            if (GUILayout.Button("새 장면 만들기", GUILayout.Height(26f)))
            {
                if (CutsceneStudioTools.CreateShot(studio, newShotName) != null) newShotName = "";
                GUIUtility.ExitGUI();
            }
        }

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("Play 중 키: Enter = 다시 재생 / PageDown, PageUp = 다음, 이전 장면", MessageType.None);
    }
}

[CustomEditor(typeof(StudioShot))]
public class StudioShotInspector : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var shot = (StudioShot)target;

        using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("세트", EditorStyles.boldLabel);
            if (GUILayout.Button("세트 짓기 (Set 안을 새로 만듦 - Props / Marks는 그대로)", GUILayout.Height(26f)))
            {
                CutsceneStudioTools.BuildSet(shot);
                GUIUtility.ExitGUI();
            }
        }

        // 구도/위치 저장은 Play 중(일시정지 포함)에도 됩니다 - Play가 끝나면 씬에 자동으로 넣어집니다.
        EditorGUILayout.Space();
        EditorGUILayout.LabelField(EditorApplication.isPlaying ? "콘티 (Play 중 - 끝나면 씬에 자동으로 들어감)" : "콘티", EditorStyles.boldLabel);
        if (GUILayout.Button("구도 저장 (지금 Scene 창 화면)", GUILayout.Height(26f)))
        {
            CutsceneStudioTools.CaptureCameraMark(shot);
            GUIUtility.ExitGUI();
        }
        if (GUILayout.Button("위치 저장 (Scene 창 한가운데 바닥)", GUILayout.Height(26f)))
        {
            CutsceneStudioTools.CapturePositionMark(shot);
            GUIUtility.ExitGUI();
        }

        using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
        {
            EditorGUILayout.Space();
            if (GUILayout.Button("이 장면만 보이기 / 재생할 장면으로 고르기"))
            {
                CutsceneStudioTools.ShowOnly(CutsceneStudioTools.FindStudio(), shot);
            }
            if (shot.script != null && GUILayout.Button("대본 열기"))
            {
                AssetDatabase.OpenAsset(shot.script);
            }
        }

        EditorGUILayout.HelpBox("구도는 Game 창(16:9)에 보이는 화면이 실제 화면입니다. 구도 표시를 선택하면 Game 창에 그 화면이 뜹니다.\n" +
                                "대본이 빈 장면을 Play하면 플레이어가 P1에 서 있는 '구도 잡기' 상태가 됩니다. 캐릭터를 보면서 구도와 위치를 한 번에 찍으세요.\n" +
                                "대본이 있는 장면은 원하는 순간에 일시정지(Play 버튼 옆 ❚❚)하고 찍습니다. Play를 끝내면 씬에 자동으로 들어갑니다.\n" +
                                "구도를 찍은 뒤에는 씬을 저장(Ctrl+S)해야 Claude가 값을 읽을 수 있습니다.", MessageType.Info);
    }
}

[CustomEditor(typeof(ShotMark))]
public class ShotMarkInspector : Editor
{
    void OnEnable()
    {
        CutsceneStudioTools.PreviewMark((ShotMark)target);
    }

    // 구도 표시를 Scene 창에서 옮기거나 돌리는 동안에도 Game 창 화면이 따라오게 합니다.
    void OnSceneGUI()
    {
        CutsceneStudioTools.PreviewMark((ShotMark)target);
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var mark = (ShotMark)target;
        CutsceneStudioTools.PreviewMark(mark);

        if (mark.kind != ShotMark.MarkKind.Camera)
        {
            // 구도 잡기 중: 플레이어를 다른 위치 표시로 옮겨 세워 보고 구도를 잡을 수 있게 합니다.
            if (EditorApplication.isPlaying)
            {
                EditorGUILayout.Space();
                if (GUILayout.Button("플레이어를 이 자리에 세우기"))
                {
                    CutsceneStudio studio = CutsceneStudioTools.FindStudio();
                    if (studio == null || !studio.PlacePlayerAt(mark))
                    {
                        Debug.LogWarning("[촬영] 장면이 재생 중일 때는 플레이어를 옮길 수 없습니다. 장면이 끝난 뒤나 대본이 빈 장면에서 눌러주세요.");
                    }
                }
            }
            return;
        }

        EditorGUILayout.Space();
        if (GUILayout.Button("Scene 창을 이 구도로 맞추기"))
        {
            SceneView view = SceneView.lastActiveSceneView;
            if (view != null) view.AlignViewToObject(mark.transform);
        }
        if (GUILayout.Button("지금 Scene 창 화면으로 이 구도 다시 찍기"))
        {
            SceneView view = SceneView.lastActiveSceneView;
            if (view != null && view.camera != null) CutsceneStudioTools.RecaptureMark(mark, view);
        }
        EditorGUILayout.HelpBox("대본에서는 이 오브젝트의 이름으로 부릅니다. 이름을 바꾸면 대본에서도 바꿔야 합니다.\n" +
                                "Play 중에는 [다시 찍기]로 찍은 것만 남고, 손으로 옮기거나 화각을 바꾼 것은 Play를 끝내면 사라집니다.", MessageType.None);
    }
}
