using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

// 일반방 보물상자(확률 등장)와 보스방 확정 보상 상자를 스폰하고, 상자를 처음 여는 순간 내용물(기본 아이템 묶음 또는 아이템 하나)을 정합니다.
// LevelBuilder의 Treasure Spawner 필드에 연결합니다. 아이템 후보는 방 기준(이 방 variant 전용 + 방 종류 공용)으로 고릅니다.
// 설계 배경(등장 확률/가중치/스폰 위치/연다 개념/48·40·9번 연동/중복 방지)은 Docs/설계노트.md [보물상자 스포너].
public class NormalRoomTreasureSpawner : MonoBehaviour
{
    [Header("등장 확률")]
    [Tooltip("적 한 마리당 더해지는 등장 확률입니다 (0.1 = 10%). 실제로 스폰된 적 수 기준으로 " +
             "합연산되며, TreasureChanceBonusOverride의 보너스 배수가 곱해진 뒤 최대 100%로 " +
             "클램프됩니다.")]
    public float chancePerEnemy = 0.1f;

    [Header("내용물 타입 결정 (가중치 - 둘 중 하나만 나옵니다)")]
    [Tooltip("이 값과 passiveItemContainerWeight의 합 대비 비율로 확률이 정해집니다. " +
             "예: 4 / (4+1) = 80%.")]
    public float basicItemsContainerWeight = 4f;
    [Tooltip("이 값과 basicItemsContainerWeight의 합 대비 비율로 확률이 정해집니다. " +
             "예: 1 / (4+1) = 20%.")]
    public float passiveItemContainerWeight = 1f;

    [Header("기본 아이템 묶음 풀 (가중치 기반 랜덤 선택)")]
    [Tooltip("Element를 추가해서 종류(Type)/개수(Count)/가중치(Weight)를 자유롭게 구성하세요. " +
             "같은 종류라도 개수가 다르면 별도 묶음으로 등록하면 됩니다 (예: 수류탄 1개 묶음과 " +
             "수류탄 3개 묶음을 각각 다른 가중치로 - 원 기획 예시: 수류탄 1개 가중치 1, 수류탄 " +
             "3개 가중치 0.3, 크레딧 1개 가중치 1).")]
    public List<BasicItemBundle> basicItemBundlePool = new List<BasicItemBundle>();

    [Header("패시브 아이템")]
    [Tooltip("Club과 동일한 ItemDatabase를 그대로 연결하세요. 일반맵에서 등장 가능한 아이템은 " +
             "각 ItemDefinition.appearanceContexts에 category=Normal 항목을 추가해서 " +
             "등록하면 됩니다 (variantId는 비워두면 됩니다 - 일반방은 variantId가 없습니다).")]
    public ItemDatabase itemDatabase;

    [Header("프리팹 (비워두면 기본 Cube 플레이스홀더로 자동 생성)")]
    [Tooltip("실제 3D 보물상자 프리팹을 연결하세요. 콜라이더는 이 스크립트가 스폰 시 자동으로 " +
             "트리거로 전환하므로, 프리팹 자체는 물리 콜라이더로 만들어둬도 무방합니다. " +
             "TreasureChest 컴포넌트가 없으면 자동으로 추가됩니다.")]
    public GameObject treasureChestPrefab;

    [Header("스폰 위치 탐색")]
    [Tooltip("마지막 적 사망 지점 기준 스폰 후보 위치를 뽑을 반경입니다.")]
    public float spawnSearchRadius = 2.5f;
    [Tooltip("NavMesh.SamplePosition 허용 오차 거리입니다.")]
    public float navMeshSampleDistance = 1f;
    [Tooltip("이미 배치된 다른 오브젝트(벽/장애물 등)와 겹치는지 확인할 때 쓰는 반경입니다.")]
    public float overlapCheckRadius = 0.5f;
    [Tooltip("겹침 검사에 포함할 레이어입니다.")]
    public LayerMask overlapCheckMask = ~0;
    public int maxPositionAttempts = 10;

    [Header("통과 가능한 레이어 (선택 사항 - 콜라이더 설정과 무관한 이중 안전장치)")]
    [Tooltip("비워두면 레이어를 건드리지 않습니다. 값을 채우면(예: \"Chest\") 상자와 그 모든 " +
             "자식 오브젝트의 레이어를 여기로 바꿉니다. Edit > Project Settings > Physics > " +
             "Layer Collision Matrix에서 이 레이어와 Player 레이어의 충돌을 꺼두면, 콜라이더가 " +
             "트리거로 제대로 동작하지 않는 경우에도(예: 알 수 없는 원인으로 여전히 막히는 " +
             "경우) 물리 엔진 차원에서 플레이어가 절대 막히지 않습니다 - RealEnemySpawner가 " +
             "적을 통과시키는 것과 동일한 방식입니다. 이 레이어를 새로 만든 뒤 이름을 " +
             "여기 적어주세요.")]
    public string walkthroughLayerName = "";

    [Header("보스방 확정 보상 상자 (SpawnBossRewardChest 전용)")]
    [Tooltip("보스방 전용으로 따로 만든 보상 상자 프리팹을 연결하세요. 비워두면 위의 " +
             "treasureChestPrefab(일반방과 공용)을 대신 사용하고, 그것마저 비어있으면 기본 " +
             "Cube 플레이스홀더로 대체됩니다 - 즉 이 필드를 아직 안 채웠다고 보스방 보상 상자가 " +
             "안 나오지는 않습니다.")]
    public GameObject bossTreasureChestPrefab;
    [Tooltip("보스방 클리어 시 나오는 확정 보상 상자를, 방 중앙(포탈이 생기는 지점) 기준으로 이 " +
             "오프셋만큼 떨어뜨려서 스폰합니다. 카메라가 완전한 탑다운이 아니라 비스듬한 시점이라 " +
             "월드 좌표축이 화면상 '위/아래'와 정확히 일치하지 않을 수 있으므로, 코드를 고치지 " +
             "않고 플레이하면서 이 값을 직접 조절해 화면상 원하는 위치(포탈보다 아래쪽 등)를 " +
             "맞추면 됩니다.")]
    public Vector3 bossChestSpawnOffset = new Vector3(0f, 0f, -2f);

    [Header("복제 보물상자 (9번 음식 연동)")]
    [Tooltip("9번 음식으로 상자를 복제할 때, 원본 상자 위치에서 이 거리만큼 떨어뜨려 스폰합니다 " +
             "(방향은 매번 무작위, FindDuplicateSpawnPosition 참고). 너무 가까우면 두 상자 모델이 " +
             "겹쳐 보이거나, interactRadius(상호작용 범위)가 겹쳐 F 한 번에 두 상자가 동시에 " +
             "반응할 수 있습니다 - 그런 경우 이 값을 interactRadius의 2배보다 크게 늘려주세요.")]
    public float duplicateChestOffsetDistance = 1f;
    [Tooltip("특수방의 크레딧 상자(PresetBasicItemChest)를 9번 음식으로 복제할 때, 복제 상자가 방에 있는 다른 상자들과 " +
             "최소한 이만큼은 떨어지도록 자리를 찾습니다(바닥 평면 거리). 상자끼리는 통과 가능한 트리거라 기존 겹침 검사로는 " +
             "걸러지지 않아 따로 확인합니다. 자리를 끝내 못 찾으면 가장 덜 겹치는 자리에 놓습니다.")]
    public float duplicateChestMinSpacing = 1f;

    // RoomController.HandleCleared()가 일반방 클리어 시 호출합니다.
    public void TrySpawnTreasure(RoomInstance room, Transform contentRoot, int enemyCount, Vector3 lastDeathPosition)
    {
        // [40번 음식 연동] 무장되어 있으면 이번 일반방은 등장 확률 굴림 자체를 건너뛰고
        // 반드시 스폰합니다. 무장은 "다음에 클리어되는 일반방 하나"에만 유효하므로, 여기서
        // 곧바로 소모(해제)합니다 - 그래야 다음 일반방에는 영향이 새지 않습니다.
        bool guaranteed = NextNormalRoomTreasureGuaranteeOverride.IsArmed;
        if (guaranteed)
        {
            NextNormalRoomTreasureGuaranteeOverride.ClearAfterThisRoom();
        }

        if (!guaranteed)
        {
            // 18번 아이템 등 "보물상자 등장 확률 증가" 계열 패시브가 있으면, 적 한 마리당 확률을
            // 합산한 결과(baseChance)에 TreasureChanceBonusOverride의 배수를 곱한 뒤 100%로
            // 클램프합니다. 등록된 보너스가 없으면 배수가 1이라 기존 동작과 완전히 동일합니다.
            float baseChance = enemyCount * chancePerEnemy;
            float chance = Mathf.Min(1f, baseChance * TreasureChanceBonusOverride.CurrentMultiplier);
            if (Random.value > chance) return;
        }

        Vector3 spawnPos = FindSpawnPosition(lastDeathPosition, contentRoot.position);
        GameObject chestGO = SpawnChestObject(spawnPos, contentRoot);
        if (chestGO == null)
        {
            Debug.LogError("NormalRoomTreasureSpawner: 보물상자 오브젝트 생성에 실패해 스폰을 건너뜁니다.");
            return;
        }

        var chest = PrepareChestComponents(chestGO);
        if (chest == null) return; // 실패 로그는 PrepareChestComponents 내부에서 이미 남깁니다.

        // v1.30: 내용물을 지금 바로 정하지 않고, "열릴 때(Open()) 실행할 함수"만 맡겨둡니다 -
        // 실제 추첨(가중치 뽑기, 보유 여부 필터링)은 그 함수가 실행되는 시점(=플레이어가 F로
        // 여는 순간)에 일어납니다.
        chest.SetContentResolver(() => ResolveContent(chest, room, enemyCount));
    }

    // 보스방 클리어 시 RoomController.HandleCleared()가 호출합니다. 일반방(TrySpawnTreasure)과
    // 달리 등장 확률 굴림이 없고(항상 확정 스폰), 내용물도 기본 아이템/패시브 아이템 중 가중치로
    // 고르지 않고 항상 패시브 아이템을 우선 시도합니다 - 다만 등록된 패시브 후보가 하나도 없는
    // 극단적인 경우(ItemDatabase에 category=Boss 항목 미설정, 후보가 전부 이미 보유 중인 경우
    // 등)에는 빈 상자를 주지 않기 위해 기본 아이템으로 폴백합니다(TrySpawnTreasure의 폴백과
    // 동일한 안전장치).
    //
    // 스폰 위치는 방 중앙(포탈이 생기는 contentRoot 위치)에서 bossChestSpawnOffset만큼 떨어진
    // 지점을 ResolveExactChestPosition()으로 계산합니다 - 일반방의 FindSpawnPosition()과 달리
    // 랜덤으로 흩뿌리지 않고, 지정한 오프셋 지점 그대로(NavMesh 스냅 정도만) 정확히 스폰됩니다.
    public void SpawnBossRewardChest(RoomInstance room, Transform contentRoot)
    {
        Vector3 targetPosition = contentRoot.position + bossChestSpawnOffset;
        Vector3 spawnPos = ResolveExactChestPosition(targetPosition, contentRoot.position);

        GameObject chestGO = SpawnChestObject(spawnPos, contentRoot, bossTreasureChestPrefab);
        if (chestGO == null)
        {
            Debug.LogError("NormalRoomTreasureSpawner: 보스방 보상 상자 오브젝트 생성에 실패해 스폰을 건너뜁니다.");
            return;
        }

        var chest = PrepareChestComponents(chestGO);
        if (chest == null) return; // 실패 로그는 PrepareChestComponents 내부에서 이미 남깁니다.

        // v1.30: 일반방과 동일하게, 내용물 결정 자체를 "열릴 때" 실행할 함수로 미룹니다.
        chest.SetContentResolver(() => ResolveBossRewardContent(chest, room));
    }

    // [9번 음식 연동] 일반방 보물상자를 복제합니다. TrySpawnTreasure()와 동일한 내용물 결정 로직
    // (ResolveContent - 기본 아이템 vs 패시브 아이템 가중치 추첨)을 그대로 재사용하되, 등장 확률
    // 굴림이 없이 항상 스폰되고, 스폰 위치도 "마지막 사망 지점"이 아니라 기존 상자 위치
    // (nearPosition)에서 duplicateChestOffsetDistance만큼 떨어진 자리를 찾습니다
    // (FindDuplicateSpawnPosition - NavMesh 샘플링 + 겹침 검사, 거리는 고정).
    // enemyCount는 원본 상자와 동일한 슬롯 수 계산을 위해 호출부(RoomController.
    // SpawnedEnemyCount)에서 그대로 넘겨받습니다.
    public void SpawnDuplicateChest(RoomInstance room, Transform contentRoot, int enemyCount, Vector3 nearPosition)
    {
        Vector3 spawnPos = FindDuplicateSpawnPosition(nearPosition, contentRoot.position);
        GameObject chestGO = SpawnChestObject(spawnPos, contentRoot);
        if (chestGO == null)
        {
            Debug.LogError("NormalRoomTreasureSpawner: 복제 보물상자 오브젝트 생성에 실패해 스폰을 건너뜁니다.");
            return;
        }

        var chest = PrepareChestComponents(chestGO);
        if (chest == null) return; // 실패 로그는 PrepareChestComponents 내부에서 이미 남깁니다.

        chest.SetContentResolver(() => ResolveContent(chest, room, enemyCount));
    }

    // [9번 음식 연동] 보스방 보상 상자를 복제합니다. SpawnBossRewardChest()와 동일하게 확률 없이
    // 항상 스폰되고 내용물도 패시브 우선(ResolveBossRewardContent)입니다. 원본 상자와 정확히 같은
    // bossChestSpawnOffset 지점에 겹쳐 나오지 않도록, nearPosition(원본 상자의 현재 위치)에서
    // duplicateChestOffsetDistance만큼 떨어진 자리를 FindDuplicateSpawnPosition()으로 찾습니다 -
    // 보스방 확정 스폰 전용인 ResolveExactChestPosition()은 "정확히 그 지점"이 목적이라
    // 복제(=같은 자리에 두 번째 상자)에는 맞지 않아 여기서는 쓰지 않습니다.
    public void SpawnDuplicateBossChest(RoomInstance room, Transform contentRoot, Vector3 nearPosition)
    {
        Vector3 spawnPos = FindDuplicateSpawnPosition(nearPosition, contentRoot.position);
        GameObject chestGO = SpawnChestObject(spawnPos, contentRoot, bossTreasureChestPrefab);
        if (chestGO == null)
        {
            Debug.LogError("NormalRoomTreasureSpawner: 복제 보스방 보상 상자 오브젝트 생성에 실패해 스폰을 건너뜁니다.");
            return;
        }

        var chest = PrepareChestComponents(chestGO);
        if (chest == null) return; // 실패 로그는 PrepareChestComponents 내부에서 이미 남깁니다.

        chest.SetContentResolver(() => ResolveBossRewardContent(chest, room));
    }

    // [9번 음식 연동 - 특수방 크레딧 상자] 바닥 프리팹에 놓인 PresetBasicItemChest(크레딧 더미의 크레딧 상자 등)를
    // 같은 설정의 상자로 복제합니다. 일반방 상자와 같은 생성 과정(SpawnChestObject - 등장 연출/바닥 맞춤,
    // PrepareChestComponents - 통과 가능)을 거친 뒤 PresetBasicItemChest를 붙이고 원본의 설정(종류/줄당 개수/줄 수
    // 가중치)을 복사합니다 - 그래서 크레딧 상자에서는 크레딧 상자만 나옵니다. 내용물은 원본을 복사하지 않고 열 때 새로 뽑습니다.
    // - 모델: 원본의 Duplicate Prefab 칸 → 비어 있으면 일반방 상자 프리팹(treasureChestPrefab) → 그것도 없으면 금색 큐브.
    // - 방향: 원본 상자를 Y축으로 돌려 놓았으면 복제 상자도 같은 각도로 돌립니다(ChestLidOpener가 경첩을 맞춰 줌).
    // - 위치: 원본에서 duplicateChestOffsetDistance 이상 떨어지고, 방의 다른 상자들과 duplicateChestMinSpacing 이상
    //   떨어진 자리(FindSpacedDuplicatePosition).
    public void SpawnDuplicatePresetChest(PresetBasicItemChest source, Transform contentRoot)
    {
        if (source == null || contentRoot == null) return;

        GameObject prefab = source.duplicatePrefab != null ? source.duplicatePrefab : treasureChestPrefab;
        Vector3 spawnPos = FindSpacedDuplicatePosition(source.transform.position, contentRoot);
        GameObject chestGO = SpawnChestObject(spawnPos, contentRoot, prefab);
        if (chestGO == null)
        {
            Debug.LogError("NormalRoomTreasureSpawner: 복제 크레딧 상자 오브젝트 생성에 실패해 스폰을 건너뜁니다.");
            return;
        }

        // 원본이 돌아가 있는 각도만큼 Y축으로 돌립니다(프리팹 원래 회전에 더함). Y축 회전이라 바닥 맞춤 높이는 그대로입니다.
        float sourceYaw = GetYawDegrees(source.transform);
        if (Mathf.Abs(sourceYaw) > 0.01f)
        {
            chestGO.transform.rotation = Quaternion.Euler(0f, sourceYaw, 0f) * chestGO.transform.rotation;
        }

        var chest = PrepareChestComponents(chestGO);
        if (chest == null) return; // 실패 로그는 PrepareChestComponents 내부에서 이미 남깁니다.

        // PresetBasicItemChest가 붙는 순간 Awake에서 열릴 때의 내용물 결정 함수를 맡겨 두고, 설정은 그 함수가 열릴 때 읽으므로
        // 붙인 뒤에 원본 설정을 복사해도 됩니다.
        var preset = chestGO.GetComponent<PresetBasicItemChest>();
        if (preset == null) preset = chestGO.AddComponent<PresetBasicItemChest>();
        preset.CopySettingsFrom(source);
        chestGO.name = $"{source.name}_Duplicate";
    }

    // 상자가 월드 Y축 기준으로 몇 도 돌아 있는지입니다(ChestLidOpener.GetChestYawDegrees와 같은 방식 - 루트가 X축으로
    // 눕혀진 3ds Max 모델이어도 좌우축 방향으로 재므로 정확합니다).
    private static float GetYawDegrees(Transform t)
    {
        Vector3 right = t.right;
        Vector3 flatRight = new Vector3(right.x, 0f, right.z);
        if (flatRight.sqrMagnitude > 0.25f)
        {
            return Vector3.SignedAngle(Vector3.right, flatRight.normalized, Vector3.up);
        }

        Vector3 forward = t.forward;
        Vector3 flatForward = new Vector3(forward.x, 0f, forward.z);
        if (flatForward.sqrMagnitude > 0.25f)
        {
            return Vector3.SignedAngle(Vector3.forward, flatForward.normalized, Vector3.up);
        }
        return 0f;
    }

    // 원본(nearPosition) 둘레를 duplicateChestOffsetDistance의 1배/1.5배/2배/2.5배 거리 원 위에서 12방향씩 살펴,
    // NavMesh 위이고 벽/장애물과 겹치지 않으며 방의 다른 상자(TreasureChest/ItemPedestal)와 duplicateChestMinSpacing
    // 이상 떨어진 첫 자리를 고릅니다. 그런 자리가 없으면 NavMesh 위 자리 중 다른 상자와 가장 먼 곳, 그것도 없으면
    // 기존 FindDuplicateSpawnPosition 결과를 씁니다(복제 상자는 반드시 나타나야 함).
    private Vector3 FindSpacedDuplicatePosition(Vector3 nearPosition, Transform contentRoot)
    {
        var occupied = new List<Vector3>();
        foreach (var other in contentRoot.GetComponentsInChildren<TreasureChest>())
        {
            if (other != null) occupied.Add(other.transform.position);
        }
        foreach (var other in contentRoot.GetComponentsInChildren<ItemPedestal>())
        {
            if (other != null) occupied.Add(other.transform.position);
        }

        Vector3 searchOrigin = new Vector3(nearPosition.x, contentRoot.position.y, nearPosition.z);
        float baseDistance = Mathf.Max(0.1f, duplicateChestOffsetDistance);
        float[] distanceScales = { 1f, 1.5f, 2f, 2.5f };
        const int directions = 12;

        bool hasBest = false;
        Vector3 best = Vector3.zero;
        float bestClearance = float.MinValue;

        foreach (float scale in distanceScales)
        {
            float startAngle = Random.Range(0f, 360f);
            for (int i = 0; i < directions; i++)
            {
                float angle = (startAngle + i * (360f / directions)) * Mathf.Deg2Rad;
                Vector3 candidate = searchOrigin + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (baseDistance * scale);

                if (!NavMesh.SamplePosition(candidate, out NavMeshHit navHit, navMeshSampleDistance, NavMesh.AllAreas))
                    continue;
                if (Physics.CheckSphere(navHit.position, overlapCheckRadius, overlapCheckMask, QueryTriggerInteraction.Ignore))
                    continue;

                float clearance = MinFlatDistance(navHit.position, occupied);
                if (clearance >= duplicateChestMinSpacing) return navHit.position;

                if (!hasBest || clearance > bestClearance)
                {
                    hasBest = true;
                    best = navHit.position;
                    bestClearance = clearance;
                }
            }
        }

        if (hasBest) return best;
        return FindDuplicateSpawnPosition(nearPosition, contentRoot.position);
    }

    private static float MinFlatDistance(Vector3 point, List<Vector3> others)
    {
        float min = float.MaxValue;
        foreach (var o in others)
        {
            float dx = o.x - point.x;
            float dz = o.z - point.z;
            float d = Mathf.Sqrt(dx * dx + dz * dz);
            if (d < min) min = d;
        }
        return min;
    }

    // SpawnBossRewardChest()가 맡겨두는 내용물 결정 함수의 실제 본문입니다(Open() 시점에
    // 실행됨) - 로직 자체는 기존과 완전히 동일하고, 실행 시점만 스폰 직후에서 열리는
    // 순간으로 옮겨졌습니다.
    //
    // [27번 음식 연동 - 보스방 확장] 뽑기 전에 BossChestOpenEvents를 울려 27번이 최상급 보장을 무장할 기회를 주고,
    // 무장돼 있으면 Tier 3 후보만으로 좁혀 뽑습니다(ResolvePassiveItemCandidate의 applyLegendaryGuarantee). 뽑기가
    // 끝나면(아이템이든 기본 아이템 폴백이든) 항상 무장을 풀어 다음 상자로 새지 않게 합니다 - ItemPedestal.Open()과 같은 규칙.
    private void ResolveBossRewardContent(TreasureChest chest, RoomInstance room)
    {
        BossChestOpenEvents.RaiseOpened();
        try
        {
            ResolveBossRewardContentInner(chest, room);
        }
        finally
        {
            NextClubPedestalLegendaryOverride.ClearAfterThisOpen();
        }
    }

    private void ResolveBossRewardContentInner(TreasureChest chest, RoomInstance room)
    {
        var passiveCandidate = ResolvePassiveItemCandidate(room, true);
        if (passiveCandidate != null)
        {
            chest.SetPassiveItemContent(passiveCandidate);
        }
        else
        {
            // 보스방 전용 패시브 아이템 후보가 하나도 없으면(ItemDefinition.appearanceContexts에
            // category=Boss 항목을 아직 안 채웠거나, 전부 이미 보유 중인 경우) 빈 상자를 주지
            // 않기 위해 기본 아이템 경로로 폴백합니다. enemyCount 인자가 없는 보스방 특성상
            // 슬롯 수는 1개(AssignBasicItems(chest, 0) -> floor(0/2)+1 = 1개)로 계산됩니다.
            Debug.LogWarning("NormalRoomTreasureSpawner: 보스방에 등장 가능한 패시브 아이템이 없어 " +
                              "기본 아이템으로 대체합니다. ItemDatabase의 appearanceContexts 설정을 확인해 주세요.");
            AssignBasicItems(chest, 0);
        }
    }

    // 상자 오브젝트에 콜라이더/TreasureChest 컴포넌트를 붙이고, 모든 콜라이더를 트리거로
    // 전환한 뒤 walkthroughLayer까지 적용하는 공통 준비 과정입니다. TrySpawnTreasure()와
    // SpawnBossRewardChest()가 동일한 상자 뼈대 준비 로직을 공유하도록 뽑아냈습니다 - 두 경로가
    // 각자 따로 복사해두면 나중에 한쪽만 고치고 다른 쪽을 깜빡하는 실수가 생기기 쉽습니다.
    // 실패 시(TreasureChest 컴포넌트를 못 붙인 경우) null을 반환하며, 원인 로그는 이 안에서
    // 남깁니다.
    private TreasureChest PrepareChestComponents(GameObject chestGO)
    {
        // TreasureChest는 [RequireComponent(typeof(Collider))]인데, Collider는 추상 타입이라
        // Unity가 "어떤 구체 콜라이더를 붙여야 할지" 알 수 없어 자동으로 추가해주지
        // 않습니다(조용히 건너뜀). treasureChestPrefab의 루트 오브젝트에 콜라이더가 아예
        // 없는 경우(예: 메쉬/콜라이더가 자식 오브젝트에만 있는 3D 모델 구성)를 대비해,
        // TreasureChest를 붙이기 전에 루트에 콜라이더가 있는지 직접 확인하고 없으면
        // BoxCollider를 추가해 둡니다.
        if (chestGO.GetComponent<Collider>() == null)
        {
            chestGO.AddComponent<BoxCollider>();
        }

        var chest = chestGO.GetComponent<TreasureChest>();
        if (chest == null) chest = chestGO.AddComponent<TreasureChest>();

        if (chest == null)
        {
            // 정상적인 경우라면 절대 발생하지 않아야 하지만(위에서 콜라이더 요구사항도
            // 미리 충족시켜뒀습니다), 혹시라도 실패하면 원인 파악이 가능하도록 명확한
            // 로그를 남기고 여기서 안전하게 중단합니다(이후 코드에서 chest를 널 상태로
            // 계속 쓰다 예기치 못한 곳에서 NullReferenceException이 나는 것을 방지).
            Debug.LogError("NormalRoomTreasureSpawner: TreasureChest 컴포넌트를 추가하지 못해 " +
                            "보물상자 내용물 설정을 건너뜁니다. treasureChestPrefab의 루트 오브젝트 " +
                            "구성(콜라이더/다른 스크립트와의 충돌 등)을 확인해 주세요.");
            return null;
        }

        // TreasureChest는 [RequireComponent(typeof(Collider))]라 프리팹에 콜라이더가 전혀
        // 없었다면 AddComponent<TreasureChest>() 시점에 기본 콜라이더(isTrigger=false)가
        // 자동으로 추가됩니다. 그래서 콜라이더를 전부 트리거로 강제하는 작업은 반드시 이
        // 시점(TreasureChest가 붙은 뒤) 이후에 해야, 그렇게 자동 추가된 콜라이더까지 빠짐없이
        // 트리거로 전환됩니다. 동아리방 보물상자와 달리 플레이어가 자유롭게 통과할 수 있어야 합니다.
        foreach (var col in chestGO.GetComponentsInChildren<Collider>())
        {
            // Non-convex Mesh Collider는 isTrigger를 true로 설정해도 물리적으로는 계속
            // 막는 콜라이더로 동작하는 유니티의 제약이 있습니다(트리거는 convex 형태에서만
            // 정상 동작). 상자는 이제 상호작용 판정을 거리 기반으로 하므로 정확한 충돌
            // 모양은 중요하지 않아, Convex 근사 형태로 바뀌어도 문제가 없습니다.
            if (col is MeshCollider meshCollider && !meshCollider.convex)
            {
                meshCollider.convex = true;
            }
            col.isTrigger = true;
        }

        // 위의 트리거 강제 설정만으로 정상적으로 통과가 안 되는 경우를 대비한 이중
        // 안전장치입니다. walkthroughLayerName이 비어있으면 아무 일도 하지 않습니다.
        ApplyWalkthroughLayer(chestGO);

        return chest;
    }

    // 상자와 그 모든 자식 오브젝트의 레이어를 walkthroughLayerName으로 바꿉니다.
    // RealEnemySpawner.SpawnOne()이 적 레이어를 설정하는 것과 같은 목적이지만, 자식까지
    // 전부 바꾼다는 점이 다릅니다(적 쪽은 루트만 바꿔서 자식 콜라이더가 레이어를 못 받는
    // 문제가 있었던 걸 참고해, 상자는 처음부터 자식까지 전부 바꿉니다).
    private void ApplyWalkthroughLayer(GameObject chestGO)
    {
        if (string.IsNullOrEmpty(walkthroughLayerName)) return;

        int layerIndex = LayerMask.NameToLayer(walkthroughLayerName);
        if (layerIndex < 0)
        {
            Debug.LogWarning($"NormalRoomTreasureSpawner: '{walkthroughLayerName}' 레이어가 프로젝트에 없습니다.");
            return;
        }

        foreach (var t in chestGO.GetComponentsInChildren<Transform>(true))
        {
            t.gameObject.layer = layerIndex;
        }
    }

    private void ResolveContent(TreasureChest chest, RoomInstance room, int enemyCount)
    {
        if (chest == null) return; // 방어적 가드 - 호출부에서 이미 검사하지만 이중 안전장치입니다.

        // [116번 장신구] 일반방 상자 플레이어 아이템 보장 중이면 "재화냐 아이템이냐" 확률을 건너뛰고 바로 아이템을 뽑습니다.
        // 뽑을 아이템이 없으면 아래 폴백대로 재화가 나옵니다(NormalChestItemGuaranteeOverride.cs 참고). 상자를 여는 순간 기준입니다.
        bool guaranteeItem = NormalChestItemGuaranteeOverride.IsActive;

        if (!guaranteeItem && RollBasicItemsContainer())
        {
            AssignBasicItems(chest, enemyCount);
            return;
        }

        var passiveCandidate = ResolvePassiveItemCandidate(room);
        if (passiveCandidate != null)
        {
            chest.SetPassiveItemContent(passiveCandidate);
        }
        else
        {
            // 등장 가능한 패시브 아이템이 하나도 없으면(전부 보유 중이거나 풀이 비어있는 등)
            // 상자를 빈 손으로 만들지 않기 위해 기본 아이템 경로로 폴백합니다.
            AssignBasicItems(chest, enemyCount);
        }
    }

    private bool RollBasicItemsContainer()
    {
        float basicW = Mathf.Max(0f, basicItemsContainerWeight);
        float passiveW = Mathf.Max(0f, passiveItemContainerWeight);
        float total = basicW + passiveW;
        if (total <= 0f) return true; // 둘 다 0이면 안전하게 기본 아이템 쪽으로

        return Random.value * total <= basicW;
    }

    private void AssignBasicItems(TreasureChest chest, int enemyCount)
    {
        int slotCount = Mathf.FloorToInt(enemyCount / 2f) + 1;
        var stacks = new List<ResolvedBasicItemStack>();

        for (int i = 0; i < slotCount; i++)
        {
            var bundle = PickWeightedBundle(basicItemBundlePool);
            if (bundle == null) continue;

            stacks.Add(new ResolvedBasicItemStack
            {
                type = bundle.type,
                count = bundle.count,
                consumed = false,
            });
        }

        chest.SetBasicItemContent(stacks);
    }

    // applyLegendaryGuarantee: 보스방 상자만 true - 27번 음식의 최상급 보장(NextClubPedestalLegendaryOverride)을
    // 이 뽑기에 적용할지 여부입니다. 일반방 상자는 27번 대상이 아니므로 false(기본값)로 호출합니다.
    private ItemDefinition ResolvePassiveItemCandidate(RoomInstance room, bool applyLegendaryGuarantee = false)
    {
        if (itemDatabase == null)
        {
            Debug.LogWarning("NormalRoomTreasureSpawner: itemDatabase가 비어있어 패시브 아이템을 뽑지 못했습니다.");
            return null;
        }

        // 패시브/액티브/장신구/일회용/음식을 합친 풀에서 하나만 뽑습니다(같은 weight 기준 가중치 추첨).
        // [2레벨 보스방] 방 기준으로 고릅니다 - 이 방 variant 전용 아이템 + 방 종류 공용 아이템(variantId 비움). variantId가 없는
        // 방(일반방/1레벨 보스방)은 예전(GetItemsForContext(카테고리 이름))과 같은 결과이고, 보스를 고르려고 variantId가 붙은
        // 보스방(drone_elite_lv2 등)도 'Boss' 공용 아이템이 그대로 나옵니다.
        var pool = itemDatabase.GetAllCategoryItemsForRoom(room, true);

        var playerInventory = Object.FindFirstObjectByType<PlayerInventory>();
        if (playerInventory != null)
        {
            pool = playerInventory.FilterUnowned(pool);
        }

        var playerActiveItem = Object.FindFirstObjectByType<PlayerActiveItem>();
        if (playerActiveItem != null)
        {
            pool = playerActiveItem.FilterOutEquipped(pool);
        }

        var playerTrinket = Object.FindFirstObjectByType<PlayerTrinket>();
        if (playerTrinket != null)
        {
            pool = playerTrinket.FilterOutEquipped(pool);
        }

        var playerDisposableItem = Object.FindFirstObjectByType<PlayerDisposableItem>();
        if (playerDisposableItem != null)
        {
            pool = playerDisposableItem.FilterOutEquipped(pool);
        }

        var playerFoodItem = Object.FindFirstObjectByType<PlayerFoodItem>();
        if (playerFoodItem != null)
        {
            pool = playerFoodItem.FilterOutEquipped(pool);
        }

        // v1.33 - 이 레벨의 다른 보물상자에서 이미 뽑혀 배정된(줍지 않았어도) 아이템은
        // 다시 뽑히지 않도록 걸러냅니다.
        var gameFlow = Object.FindFirstObjectByType<GameFlowManager>();
        if (gameFlow != null)
        {
            pool = gameFlow.FilterOutAlreadyAssignedThisLevel(pool);
        }

        // [27번 음식 연동] 보스방 상자이고 최상급 보장이 무장돼 있으면 Tier 3 후보만으로 좁힙니다
        // (ItemPedestal.PickWeighted()와 같은 규칙 - 최상급 후보가 없으면 경고를 남기고 보장 없이 진행).
        if (applyLegendaryGuarantee && NextClubPedestalLegendaryOverride.IsArmed)
        {
            var legendaryOnly = pool.Where(item => item != null && item.rarity == ItemDefinition.ItemRarity.Tier3).ToList();
            if (legendaryOnly.Count > 0)
            {
                pool = legendaryOnly;
            }
            else
            {
                Debug.LogWarning("NormalRoomTreasureSpawner: 27번 음식으로 보스방 상자에 최상급 아이템을 보장하려 했지만, " +
                                  "뽑을 수 있는 최상급 후보가 없어(전부 이미 보유/배정되었거나 등록되지 않음) 보장 없이 진행합니다.");
            }
        }

        var picked = PickWeightedItem(pool);
        // v1.33 - 이 상자가 방금 이 아이템을 내용물로 확정했으므로, 줍기 전이라도 이
        // 레벨의 다른 보물상자에서 다시 뽑히지 않도록 즉시 기록합니다.
        if (picked != null && gameFlow != null)
        {
            gameFlow.MarkItemAssignedThisLevel(picked.itemId);
        }
        return picked;
    }

    // 보스방 보상 상자 전용 위치 계산입니다. 아래 FindSpawnPosition()과 달리 spawnSearchRadius
    // 범위 안에서 랜덤하게 흩뿌리는 과정이 전혀 없습니다 - "마지막으로 죽은 적 근처에 대충"이
    // 아니라 "형빈이 지정한 정확한 오프셋 지점"이 목적이기 때문입니다. exactPosition을 그대로
    // 쓰되, NavMesh.SamplePosition으로 딱 한 번만 스냅해서 바닥 높이/근소한 좌표 오차만
    // 보정합니다(주로 Y값 보정 목적 - X/Z는 사실상 그대로 유지됩니다). 이 지점 근처에 NavMesh가
    // 아예 없는 경우(예: 오프셋 값이 방 바깥을 가리키는 경우)에만 방 중앙으로 폴백합니다.
    private Vector3 ResolveExactChestPosition(Vector3 exactPosition, Vector3 fallbackPosition)
    {
        if (NavMesh.SamplePosition(exactPosition, out NavMeshHit navHit, navMeshSampleDistance, NavMesh.AllAreas))
        {
            return navHit.position;
        }

        Debug.LogWarning("NormalRoomTreasureSpawner: bossChestSpawnOffset 위치 근처에서 NavMesh를 찾지 " +
                          "못해 방 중앙으로 대체합니다. 오프셋 값이 방 범위를 벗어나지 않았는지 확인해 주세요.");

        if (NavMesh.SamplePosition(fallbackPosition, out NavMeshHit fallbackHit, navMeshSampleDistance * 3f, NavMesh.AllAreas))
        {
            return fallbackHit.position;
        }
        return fallbackPosition;
    }

    // [9번 음식 연동] FindSpawnPosition()은 0~spawnSearchRadius 범위에서 무작위 거리를 뽑기 
    // 때문에(Random.insideUnitCircle) 아주 가까운 거리가 나올 수도 있어, 복제 상자에는 맞지 
    // 않습니다(원본과 겹쳐버림). 이 메서드는 각도만 무작위로 굴리고 거리는 항상 
    // duplicateChestOffsetDistance로 고정해서, 복제 상자가 항상 원본에서 최소한 그만큼은 
    // 떨어지도록 보장합니다. NavMesh 스냅/겹침 검사/폴백 로직은 FindSpawnPosition과 동일합니다.
    private Vector3 FindDuplicateSpawnPosition(Vector3 nearPosition, Vector3 fallbackPosition)
    {
        Vector3 searchOrigin = new Vector3(nearPosition.x, fallbackPosition.y, nearPosition.z);

        for (int attempt = 0; attempt < maxPositionAttempts; attempt++)
        {
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * duplicateChestOffsetDistance;
            Vector3 candidate = searchOrigin + offset;

            if (!NavMesh.SamplePosition(candidate, out NavMeshHit navHit, navMeshSampleDistance, NavMesh.AllAreas))
                continue;

            if (Physics.CheckSphere(navHit.position, overlapCheckRadius, overlapCheckMask, QueryTriggerInteraction.Ignore))
                continue;

            return navHit.position;
        }

        // 유효한 자리를 끝내 못 찾으면(예: 좁은 방) 방 중심으로 폴백합니다 - 복제 상자도 
        // 반드시 나타나야 합니다.
        if (NavMesh.SamplePosition(fallbackPosition, out NavMeshHit fallbackHit, navMeshSampleDistance * 3f, NavMesh.AllAreas))
        {
            return fallbackHit.position;
        }
        return fallbackPosition;
    }

    private Vector3 FindSpawnPosition(Vector3 targetPosition, Vector3 fallbackPosition)
    {
        // 죽은 적의 위치는 X/Z(수평 위치)만 신뢰합니다. Y(높이)는 적 모델의 루트 피벗이
        // 발밑이 아니라 허리/가슴 높이 등으로 잡혀있는 경우가 흔해서, 그 값을 그대로 쓰면
        // NavMesh.SamplePosition이 navMeshSampleDistance 범위 안에서 실제 바닥을 못 찾고
        // 계속 실패할 수 있습니다(그 결과 상자가 부자연스러운 위치/높이로 스폰됨). 대신
        // 방 바닥 기준 높이인 fallbackPosition(contentRoot 위치)의 Y를 사용합니다.
        Vector3 searchOrigin = new Vector3(targetPosition.x, fallbackPosition.y, targetPosition.z);

        for (int attempt = 0; attempt < maxPositionAttempts; attempt++)
        {
            Vector2 offset = Random.insideUnitCircle * spawnSearchRadius;
            Vector3 candidate = searchOrigin + new Vector3(offset.x, 0f, offset.y);

            if (!NavMesh.SamplePosition(candidate, out NavMeshHit navHit, navMeshSampleDistance, NavMesh.AllAreas))
                continue;

            if (Physics.CheckSphere(navHit.position, overlapCheckRadius, overlapCheckMask, QueryTriggerInteraction.Ignore))
                continue;

            return navHit.position;
        }

        // 유효한 자리를 끝내 못 찾으면 방 중심으로 폴백합니다 (형빈 확정 - 확률에 당첨된
        // 이상 상자는 반드시 나타나야 합니다). 폴백 지점도 일단 NavMesh 위로 스냅을
        // 시도해보고, 그마저 안 되면 원래 좌표를 그대로 씁니다.
        if (NavMesh.SamplePosition(fallbackPosition, out NavMeshHit fallbackHit, navMeshSampleDistance * 3f, NavMesh.AllAreas))
        {
            return fallbackHit.position;
        }
        return fallbackPosition;
    }

    // prefabOverride를 넘기면 그 프리팹을 우선 사용하고(보스방 전용 상자 등), 넘기지 않으면
    // (null) 기존과 동일하게 treasureChestPrefab을 사용합니다 - 일반방 호출부(TrySpawnTreasure)는
    // 인자를 안 넘기므로 동작이 그대로 유지됩니다.
    private GameObject SpawnChestObject(Vector3 position, Transform parent, GameObject prefabOverride = null)
    {
        GameObject prefabToUse = prefabOverride != null ? prefabOverride : treasureChestPrefab;

        GameObject chestGO;
        if (prefabToUse != null)
        {
            // Quaternion.identity로 고정하면 프리팹 에셋 자체에 설정해둔 회전값이 완전히
            // 무시되어 항상 같은 방향으로만 스폰됩니다(형빈이 프리팹을 어떻게 회전시켜
            // 놓든 소용없음). 대신 프리팹 에셋에 저장된 회전값을 그대로 사용해서, 씬에
            // 직접 가져다 놨을 때 보이는 방향 그대로 스폰되도록 합니다 - 원하는 방향은
            // 프리팹 에셋의 Transform > Rotation 값을 조정해서 맞추면 됩니다.
            chestGO = Instantiate(prefabToUse, position, prefabToUse.transform.rotation, parent);
        }
        else
        {
            chestGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
            chestGO.name = "TreasureChest_Placeholder";
            chestGO.transform.SetParent(parent);
            chestGO.transform.position = position;
            chestGO.transform.localScale = new Vector3(0.8f, 0.8f, 0.8f);

            var rend = chestGO.GetComponent<Renderer>();
            if (rend != null) rend.material.color = new Color(1f, 0.85f, 0.2f); // 임시 식별용 금색
        }

        // 모델의 피벗(origin)이 바닥이 아니라 중심 등 다른 곳에 있어도(예: 허리 높이),
        // 실제 렌더링되는 메쉬의 가장 아래쪽이 스폰 지점의 바닥 높이에 딱 맞닿도록 오브젝트
        // 전체를 위/아래로 보정합니다. 이걸 안 하면 피벗이 중심에 있는 프리팹은 절반쯤
        // 바닥에 파묻힌 것처럼 보입니다. 플레이스홀더 큐브도 동일한 보정을 거칩니다(기존의
        // "+0.4f" 임시값 대신 실제 크기 기준으로 정확히 계산됩니다).
        SnapToGround(chestGO, position.y);

        // 상자가 갑자기 뿅 나타나지 않고, 투명한 상태에서 흰색으로 떠오른 뒤
        // 서서히 원래 색으로 돌아오도록 합니다(일반방/보스방 상자 모두 이 메서드를
        // 공유하므로 여기 한 곳만 고치면 둘 다 적용됩니다).
        AppearFromWhite.Begin(chestGO);

        return chestGO;
    }

    // renderers의 월드 공간 경계 상자(bounds)를 모두 합친 뒤, 그 아래쪽 끝(min.y)이
    // floorY와 정확히 일치하도록 오브젝트를 위/아래로 이동시킵니다. 피벗 위치가 어디에
    // 있든(모델 중심이든 바닥이든) 결과적으로 항상 바닥에 자연스럽게 놓이게 됩니다.
    private void SnapToGround(GameObject go, float floorY)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers == null || renderers.Length == 0) return;

        Bounds combined = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            combined.Encapsulate(renderers[i].bounds);
        }

        float offset = floorY - combined.min.y;
        if (Mathf.Abs(offset) > 0.0001f)
        {
            go.transform.position += new Vector3(0f, offset, 0f);
        }
    }

    // [48번 아이템 연동 - v1.31] NextItemPickWeightOverride.IsArmed가 켜져 있으면(=ResolveContent
    // /ResolveBossRewardContent가 TreasureChest.Open() 안에서 실행 중이고, 그 상자를 열기
    // 직전에 48번을 사용해 다음 상자에 무장해 둔 경우), 각 후보의 실제 weight 대신 1~5 사이의
    // 랜덤 정수를 그 후보만의 가중치로 새로 매겨서 뽑습니다(후보마다 독립적으로 굴리므로 전부
    // 같은 값이 나올 수도, 전부 다를 수도 있습니다). 같은 굴림 값을 총합 계산과 누적 판정
    // 양쪽에 그대로 재사용하기 위해 effectiveWeights 배열에 한 번만 계산해 둡니다.
    // ItemPedestal.PickWeightedBundle()과 동일한 로직입니다 - 두 클래스가 서로 다른 컴포넌트라
    // 공유 유틸로 뽑지 않고 각자 구현합니다.
    private BasicItemBundle PickWeightedBundle(List<BasicItemBundle> pool)
    {
        var valid = pool?.Where(b => b != null && b.weight > 0f).ToList();
        if (valid == null || valid.Count == 0) return null;
        if (valid.Count == 1) return valid[0];

        bool scramble = NextItemPickWeightOverride.IsArmed;
        float[] effectiveWeights = new float[valid.Count];
        float totalWeight = 0f;
        for (int i = 0; i < valid.Count; i++)
        {
            // [v1.39 - 48번 장신구 조정] 1~5 대신 1~3 사이로 좁혔습니다.
            effectiveWeights[i] = scramble ? Random.Range(1, 4) : valid[i].weight;
            totalWeight += effectiveWeights[i];
        }

        float roll = Random.value * totalWeight;
        float cumulative = 0f;
        for (int i = 0; i < valid.Count; i++)
        {
            cumulative += effectiveWeights[i];
            if (roll <= cumulative)
            {
                // [48번 장신구 디버그 로그 - v1.39 추가] 아래 PickWeightedItem()과 동일한 이유입니다.
                if (scramble)
                {
                    Debug.Log($"[48번 장신구] 기본 아이템 묶음 '{valid[i].type} x{valid[i].count}'가 임시 가중치 {effectiveWeights[i]}로 등장했습니다.");
                }
                return valid[i];
            }
        }
        if (scramble)
        {
            Debug.Log($"[48번 장신구] 기본 아이템 묶음 '{valid[valid.Count - 1].type} x{valid[valid.Count - 1].count}'가 임시 가중치 {effectiveWeights[valid.Count - 1]}로 등장했습니다.");
        }
        return valid[valid.Count - 1];
    }

    // PickWeightedBundle()과 동일한 48번 연동입니다 - ItemPedestal.PickWeighted()와 동일한
    // 로직입니다.
    private ItemDefinition PickWeightedItem(List<ItemDefinition> pool)
    {
        if (pool == null || pool.Count == 0) return null;
        if (pool.Count == 1) return pool[0];

        bool scramble = NextItemPickWeightOverride.IsArmed;
        float[] effectiveWeights = new float[pool.Count];
        float totalWeight = 0f;
        for (int i = 0; i < pool.Count; i++)
        {
            // [v1.39 - 48번 장신구 조정, 형빈 확정] 1~5 대신 1~3 사이로 좁혔습니다.
            // 평소에는 67번 등의 등장 가중치 보정이 적용된 값을 씁니다(ItemWeightModifier).
            effectiveWeights[i] = scramble ? Random.Range(1, 4) : ItemWeightModifier.GetWeight(pool[i]);
            totalWeight += effectiveWeights[i];
        }
        if (totalWeight <= 0f) return pool[0];

        float roll = Random.value * totalWeight;
        float cumulative = 0f;
        for (int i = 0; i < pool.Count; i++)
        {
            cumulative += effectiveWeights[i];
            if (roll <= cumulative)
            {
                // [48번 장신구 디버그 로그 - v1.39 추가] 스크램블이 실제로 걸려 있을 때만,
                // 뽑힌 아이템이 어떤 임시 가중치로 등장했는지 콘솔에 남깁니다.
                if (scramble)
                {
                    Debug.Log($"[48번 장신구] '{pool[i].displayName}' 아이템이 임시 가중치 {effectiveWeights[i]}로 등장했습니다.");
                }
                return pool[i];
            }
        }
        if (scramble)
        {
            Debug.Log($"[48번 장신구] '{pool[pool.Count - 1].displayName}' 아이템이 임시 가중치 {effectiveWeights[pool.Count - 1]}로 등장했습니다.");
        }
        return pool[pool.Count - 1];
    }
}
