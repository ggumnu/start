using UnityEngine;

// [비전투 이동 가속] 전투가 없는 상태에서 걸을 때 이동 속도를 서서히 올려 줍니다(설정 = Resources/TravelBoostSettings).
// PlayerMovement가 Awake에서 자동으로 붙이고 매 프레임 Tick을 불러 실제 이동 속도를 받아 갑니다 - 프리팹에 직접 붙일 필요 없음.
// 규칙(형빈 확정): 비전투 상태 1초 뒤부터 걷는 동안 2초에 걸쳐 0 -> 50%(현재 이동 속도 x1.5, 최종 상한 5).
// 멈춰도 가속은 그대로 유지(멈춘 동안 오르지만 않음). 좌클릭 누름/우클릭 조준/피격/장전 중이면 즉시 0%로, 다시 걸으면 1초 대기 없이 2초 재상승.
// 액티브 아이템 사용은 유지.
// 비전투 방끼리 옮겨 다닐 때는 1초 대기를 다시 하지 않음(전투 상태 -> 비전투 상태로 바뀐 순간에만 기다림).
// 뛰기 애니메이션(Animator IsRunning/RunSpeed)은 PlayerMovement가 IsBoosting을 읽어 넘깁니다.
[DisallowMultipleComponent]
public class PlayerTravelBoost : MonoBehaviour
{
    // 0 = 가속 없음, 1 = 최대 가속.
    public float BoostRatio { get; private set; }
    public bool IsBoosting => BoostRatio > 0f;
    // 지금 비전투 상태인지(전투방이 아니거나 클리어된 방).
    public bool IsCombatFree { get; private set; }
    // 마지막 Tick에서 적용한 배율(최종 속도 / 스탯 속도).
    public float CurrentSpeedMultiplier { get; private set; } = 1f;

    private PlayerHealth health;
    private PlayerShooting shooting;
    private float combatFreeTimer;
    private bool hitPending;
    // 공격 입력으로 친 누름만 추적합니다 - HUD 버튼/팝업/중단 화면 위에서 시작한 누름은 떼기 전까지 무시합니다.
    private bool leftPressCounted;
    private bool rightPressCounted;

    private LevelBuilder levelBuilder;
    private float nextBuilderSearchTime;

    void Awake()
    {
        health = GetComponent<PlayerHealth>();
        if (health == null) health = GetComponentInParent<PlayerHealth>();
        shooting = GetComponent<PlayerShooting>();
        if (shooting == null) shooting = GetComponentInChildren<PlayerShooting>();
    }

    void OnEnable()
    {
        if (health != null) health.OnDamageTaken += HandleDamageTaken;
    }

    void OnDisable()
    {
        if (health != null) health.OnDamageTaken -= HandleDamageTaken;
        ResetBoost();
    }

    // 보호막이 막은 공격은 OnDamageTaken이 오지 않으므로 가속이 풀리지 않습니다(보호막 = 피격 아님 규칙).
    private void HandleDamageTaken(float amount)
    {
        hitPending = true;
    }

    // 매 프레임 PlayerMovement가 부릅니다. 반환값 = 이번 프레임 실제 이동 속도.
    // freeze = 돌진/넉백처럼 이동 입력이 잠깐 막히는 동안(가속 상태를 그대로 유지, 오르지도 풀리지도 않음).
    // hardReset = 로딩/입장 컷씬/헬기 연출처럼 조작이 아닌 이동(가속 0 + 비전투 대기도 처음부터).
    public float Tick(float statSpeed, bool isMoving, bool freeze, bool hardReset)
    {
        TravelBoostSettings s = TravelBoostSettings.Get();
        float dt = Time.deltaTime;

        UpdateMouseInput();
        // 장전은 총을 손에 들고 하는 동작이라 장전하는 동안 내내 가속을 풉니다(형빈 확정).
        bool attackInput = leftPressCounted || rightPressCounted || (shooting != null && shooting.IsReloading);
        bool hit = hitPending;
        hitPending = false;

        IsCombatFree = EvaluateCombatFree();

        if (!s.enableBoost || hardReset)
        {
            combatFreeTimer = 0f;
            ResetBoost();
            return Finish(statSpeed, s);
        }

        if (IsCombatFree) combatFreeTimer += dt;
        else combatFreeTimer = 0f;

        if (!IsCombatFree || attackInput || hit)
        {
            ResetBoost();
            return Finish(statSpeed, s);
        }

        if (freeze) return Finish(statSpeed, s);

        // 멈춘 동안은 가속을 유지만 하고 더 올리지 않습니다(형빈 확정 - 멈춤으로는 풀리지 않음).
        if (!isMoving) return Finish(statSpeed, s);

        if (combatFreeTimer >= s.combatFreeDelay)
        {
            BoostRatio = Mathf.MoveTowards(BoostRatio, 1f, dt / Mathf.Max(0.01f, s.rampDuration));
        }

        return Finish(statSpeed, s);
    }

    // [촬영 스튜디오] 대본의 '뛰기' 동안 PlayerMovement가 Tick 대신 부릅니다. 비전투 대기/오르는 시간을 건너뛰고 바로 최대 가속으로 둡니다.
    public float TickForcedMax(float statSpeed)
    {
        BoostRatio = 1f;
        return Finish(statSpeed, TravelBoostSettings.Get());
    }

    private float Finish(float statSpeed, TravelBoostSettings s)
    {
        float speed = statSpeed;
        if (BoostRatio > 0f && statSpeed > 0f)
        {
            float boosted = statSpeed * (1f + s.bonusRatio * BoostRatio);
            if (s.boostedSpeedCap > 0f) boosted = Mathf.Min(boosted, s.boostedSpeedCap);
            // 상한이 스탯보다 낮게 설정돼 있어도 가속 때문에 오히려 느려지지는 않게 합니다.
            speed = Mathf.Max(statSpeed, boosted);
        }
        CurrentSpeedMultiplier = statSpeed > 0f ? speed / statSpeed : 1f;
        return speed;
    }

    private void ResetBoost()
    {
        BoostRatio = 0f;
    }

    private void UpdateMouseInput()
    {
        bool paused = Time.timeScale <= 0f || PauseMenuUI.IsOpen;

        if (!Input.GetMouseButton(0)) leftPressCounted = false;
        else if (Input.GetMouseButtonDown(0) && !paused && !HUDClickBlocker.BlocksLeftClick) leftPressCounted = true;

        if (!Input.GetMouseButton(1)) rightPressCounted = false;
        else if (Input.GetMouseButtonDown(1) && !paused) rightPressCounted = true;
    }

    // 판정 기준은 SlotItemDropper.IsInCombatFreeRoom과 같음(전투방이 아니거나 클리어됨). 매 프레임 씬 검색을 피하려고 LevelBuilder를 캐시합니다.
    // LevelBuilder가 없는 테스트 씬은 항상 비전투, 플레이어가 어느 방인지 아직 모르면 전투로 봅니다.
    private bool EvaluateCombatFree()
    {
        if (levelBuilder == null && Time.unscaledTime >= nextBuilderSearchTime)
        {
            levelBuilder = FindFirstObjectByType<LevelBuilder>();
            nextBuilderSearchTime = Time.unscaledTime + 0.5f;
        }
        if (levelBuilder == null) return true;
        if (!levelBuilder.HasPlayerCell) return false;

        Vector2Int cell = levelBuilder.CurrentPlayerCell;
        var rooms = levelBuilder.GeneratedRooms;
        for (int i = 0; i < rooms.Count; i++)
        {
            RoomInstance r = rooms[i];
            if (r == null || r.occupiedCells == null || !r.occupiedCells.Contains(cell)) continue;

            RoomController controller = levelBuilder.GetRoomController(r.id);
            return controller == null || !controller.RequiresClear || r.isCleared;
        }
        return false;
    }
}
