using System.Collections.Generic;
using UnityEngine;

// 수류탄/해킹툴 특수방의 "특수방 잠금 바리케이드"에 붙는 컴포넌트입니다. LevelBuilder가 부모 방(일반방/시작방)의
// 특수방 쪽 벽을 지을 때 자동으로 붙이므로, 프리팹에 미리 붙일 필요는 없습니다.
//
// [2단 바리케이드]
// 부모 방의 그 벽에는 바리케이드가 두 개 섭니다.
// - 전투 바리케이드(WallSidePrefab.barricade): RoomController가 관리 - 부모 방 전투 중에만 막음
// - 특수방 잠금 바리케이드(이 컴포넌트가 붙은 오브젝트): 전투와 무관 - Unlock()될 때까지 계속 막음
// 둘은 서로 다른 오브젝트라 전투가 끝나 전투 바리케이드가 사라져도 이 바리케이드는 그대로 남고, 반대로 전투 중에
// 이 바리케이드가 먼저 풀려도 전투 바리케이드는 남아 있습니다. 둘 다 사라져야 특수방에 드나들 수 있고, 그 뒤로는
// 일반 열린 벽처럼 자유롭게 드나듭니다.
//
// [해제 - 3단계]
// - 수류탄 방: 플레이어 수류탄이 터졌을 때(PlayerGrenadeThrower.OnGrenadeExploded) 폭발 반경 안에 이 바리케이드가 걸리면 해제
// - 해킹툴 방: 해킹툴이 발동했을 때(PlayerHackTool.OnHackDeployed) 해킹 반경 안에 이 바리케이드가 걸리면 해제
// 조건이 다른 도구에는 반응하지 않습니다(수류탄 방은 해킹툴로 안 열림). 전투 중이어도 해제됩니다. 반경 판정은 바닥 평면
// 거리(높이 무시)로, 폭발/해킹 중심에서 바리케이드 콜라이더(없으면 렌더러) 경계 상자의 가장 가까운 점까지의 거리가 반경
// 이하이면 걸린 것으로 봅니다(적이 몸 일부만 걸쳐도 맞는 수류탄/해킹툴 판정과 같은 느낌).
// [대상 방 - 방 전환 작업]
// - 수류탄: 수류탄을 "던진 방"의 바리케이드만 대상입니다(PlayerGrenadeThrower.ExplodingRoom / TryUnlockByGrenade로 받은 방).
//   폭발 위치로 방을 정하지 않는 이유: 수류탄은 벽을 넘어 날아가 바리케이드 너머 특수방 칸에 떨어질 수도 있기 때문입니다.
//   수류탄을 던지고 바로 다른 방으로 나가 플레이어가 없을 때 터져도 그 방의 바리케이드는 풀립니다(이때는 연출 없이 조용히 사라짐).
//   꺼져 있는 바리케이드는 Unity가 콜라이더 크기를 0으로 주므로, 켜져 있을 때 미리 기록해 둔 크기(cachedBounds)로 판정합니다.
// - 해킹툴, 방을 넘기지 않는 폭발(포격 등): 예전처럼 플레이어가 지금 있는 방의 바리케이드만 대상입니다(다른 방 바리케이드는
//   그 방 콘텐츠가 꺼져 있어 화면에 없으므로 제외).
// 수류탄/해킹툴 스크립트는 고치지 않았습니다 - 두 스크립트가 이미 열어둔 아이템용 이벤트를 구독만 합니다.
//
// [해제 연출]
// 바리케이드는 방 클리어 때의 전투 바리케이드와 같은 방식으로 흰색이 되었다가 사라지고(DissolveAndVanish - 콜라이더는
// 즉시 꺼져 바로 지나갈 수 있음), 동시에 조건별 해제 이펙트(Special Room Entry Settings - 수류탄 = 연쇄 폭파, 해킹툴 =
// 전용 이펙트)가 재생됩니다(SpecialRoomUnlockVfx).
//
// [테스트용 수동 해제]
// Play 중에 이 오브젝트를 선택하고 Inspector에서 이 컴포넌트 오른쪽 위 ⋮ 메뉴 > "테스트: 지금 잠금 해제"를 누르면 바로
// 풀 수 있습니다(해제 이펙트도 재생).
//
// [잠긴 바리케이드 목록]
// 아직 잠겨 있는 바리케이드를 All로 모아둡니다. 부모 방 콘텐츠는 그 방에 있을 때만 켜지므로 OnEnable/OnDisable이 아니라
// 초기화/해제/파괴 시점으로 관리합니다.
public class SpecialRoomLock : MonoBehaviour
{
    private static readonly List<SpecialRoomLock> lockedInstances = new List<SpecialRoomLock>();

    // 수류탄/해킹툴 이벤트를 구독 중인 플레이어 컴포넌트입니다(플레이어가 바뀌거나 파괴되면 다시 찾음).
    private static PlayerGrenadeThrower boundThrower;
    private static PlayerHackTool boundHackTool;

    // 지금 잠겨 있는 모든 특수방 잠금 바리케이드입니다(읽기 전용).
    public static IReadOnlyList<SpecialRoomLock> All => lockedInstances;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        lockedInstances.Clear();
        boundThrower = null;
        boundHackTool = null;
    }

    // 이 바리케이드를 푸는 방법(수류탄 또는 해킹툴)입니다.
    public SpecialRoomEntryCondition Condition { get; private set; }

    // 이 바리케이드가 막고 있는 특수방 / 이 바리케이드가 서 있는 부모 방입니다.
    public RoomInstance SpecialRoom { get; private set; }
    public RoomInstance ParentRoom { get; private set; }

    public bool IsLocked { get; private set; }

    // 해제 이펙트 설정을 꺼내 올 공용 설정 에셋입니다(LevelBuilder > Special Room Entry Settings). 없으면 이펙트 없이 풀립니다.
    private SpecialRoomEntrySettings entrySettings;

    // [방 전환] 켜져 있을 때 기록해 둔 바리케이드 크기입니다(꺼져 있을 때의 범위 판정용). 부모 방에 처음 들어온 프레임에 기록합니다.
    private readonly List<Bounds> cachedBounds = new List<Bounds>();
    private bool boundsCached;

    // LevelBuilder가 바리케이드를 세운 직후 호출합니다.
    public void Initialize(SpecialRoomEntryCondition condition, RoomInstance specialRoom, RoomInstance parentRoom,
                           SpecialRoomEntrySettings settings)
    {
        Condition = condition;
        SpecialRoom = specialRoom;
        ParentRoom = parentRoom;
        entrySettings = settings;
        IsLocked = true;
        if (!lockedInstances.Contains(this)) lockedInstances.Add(this);
    }

    // 이 바리케이드가 화면에 나타날 때(=플레이어가 부모 방에 들어왔을 때) 수류탄/해킹툴 이벤트 구독을 확인합니다.
    // 플레이어가 이미 있는 시점이라 확실히 찾을 수 있고, 플레이어가 새로 만들어졌으면 여기서 다시 구독합니다.
    void OnEnable()
    {
        EnsureBoundToPlayer();
    }

    // [방 전환] 켜진 뒤 첫 프레임에 바리케이드 크기를 기록합니다(바리케이드는 움직이지 않으므로 한 번이면 충분).
    void Update()
    {
        if (!boundsCached && IsLocked) CacheBounds();
    }

    private void CacheBounds()
    {
        cachedBounds.Clear();
        foreach (var col in GetComponentsInChildren<Collider>())
        {
            if (col == null || !col.enabled) continue;
            cachedBounds.Add(col.bounds);
        }
        if (cachedBounds.Count == 0)
        {
            foreach (var rend in GetComponentsInChildren<Renderer>())
            {
                if (rend == null || !rend.enabled) continue;
                cachedBounds.Add(rend.bounds);
            }
        }
        boundsCached = true;
    }

    // 잠금을 풉니다. 이미 풀렸으면 아무 일도 하지 않습니다.
    public void Unlock()
    {
        if (!IsLocked) return;
        IsLocked = false;
        lockedInstances.Remove(this);

        if (gameObject.activeInHierarchy)
        {
            GameAudio.Play(SfxId.SpecialRoomUnlock); // [사운드] 보이는 잠금이 풀릴 때만(꺼진 방에서 풀리면 소리 없음)

            // 콜라이더는 DissolveAndVanish가 곧바로 끄므로(끈 콜라이더는 경계 상자가 비어 있음) 그 전에 이펙트 위치를 계산합니다.
            var lockSettings = entrySettings != null ? entrySettings.GetLockSettings(Condition) : null;
            if (lockSettings != null && lockSettings.unlockVfxPrefab != null)
            {
                SpecialRoomUnlockVfx.Play(lockSettings, ComputeVisualBounds());
            }

            DissolveAndVanish.Begin(gameObject, DissolveAndVanish.EndAction.Deactivate);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    void OnDestroy()
    {
        lockedInstances.Remove(this);
    }

    [ContextMenu("테스트: 지금 잠금 해제")]
    private void DebugUnlock()
    {
        Unlock();
    }

    // ------------------------------------------------------------------
    // 수류탄/해킹툴 연동
    // ------------------------------------------------------------------

    private static void EnsureBoundToPlayer()
    {
        // Unity 오브젝트는 파괴되면 == null이 되므로, 플레이어가 바뀐 경우에도 아래에서 다시 찾습니다.
        if (boundThrower != null && boundHackTool != null) return;

        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return;

        var thrower = player.GetComponent<PlayerGrenadeThrower>();
        if (thrower != null && thrower != boundThrower)
        {
            if (boundThrower != null) boundThrower.OnGrenadeExploded -= HandleGrenadeExploded;
            boundThrower = thrower;
            boundThrower.OnGrenadeExploded += HandleGrenadeExploded;
        }

        var hackTool = player.GetComponent<PlayerHackTool>();
        if (hackTool != null && hackTool != boundHackTool)
        {
            if (boundHackTool != null) boundHackTool.OnHackDeployed -= HandleHackDeployed;
            boundHackTool = hackTool;
            boundHackTool.OnHackDeployed += HandleHackDeployed;
        }
    }

    private static void HandleGrenadeExploded(Vector3 center, float radius)
    {
        // [방 전환] 수류탄이 넘겨준 "던진 방"이 있으면 그 방 기준, 없으면(포격 등) 예전처럼 지금 플레이어가 있는 방 기준입니다.
        RoomInstance room = boundThrower != null ? boundThrower.ExplodingRoom : null;
        TryUnlockInRange(SpecialRoomEntryCondition.Grenade, center, radius, room);
    }

    private static void HandleHackDeployed(Vector3 center, float radius, IReadOnlyList<EnemyHealth> hacked)
    {
        TryUnlockInRange(SpecialRoomEntryCondition.HackingTool, center, radius, null);
    }

    // [방 전환] 플레이어가 없는 방에서 수류탄이 조용히 터졌을 때 Grenade가 직접 부릅니다(이벤트를 보내지 않는 폭발이라).
    // room(수류탄을 던진 방)의 수류탄 잠금 바리케이드 중 범위에 걸린 것을 풉니다.
    public static void TryUnlockByGrenade(Vector3 center, float radius, RoomInstance room)
    {
        if (room == null) return;
        TryUnlockInRange(SpecialRoomEntryCondition.Grenade, center, radius, room);
    }

    // condition 도구가 center/radius로 터졌을 때, 그 범위에 걸린 같은 조건의 잠긴 바리케이드를 전부 풉니다.
    // room이 있으면 그 방(부모 방)의 바리케이드만, 없으면 플레이어가 지금 있는 방(= 켜져 있는) 바리케이드만 대상입니다.
    private static void TryUnlockInRange(SpecialRoomEntryCondition condition, Vector3 center, float radius, RoomInstance room)
    {
        if (radius <= 0f || lockedInstances.Count == 0) return;

        // Unlock()이 목록에서 자신을 빼므로 복사본을 돌며 확인합니다.
        var snapshot = lockedInstances.ToArray();
        foreach (var candidate in snapshot)
        {
            if (candidate == null || !candidate.IsLocked) continue;
            if (candidate.Condition != condition) continue;
            if (room != null)
            {
                if (!ReferenceEquals(candidate.ParentRoom, room)) continue; // 수류탄을 던진 방의 바리케이드만
            }
            else if (!candidate.gameObject.activeInHierarchy) continue; // 플레이어가 지금 있는 방의 바리케이드만

            if (candidate.IsWithinFlatRange(center, radius))
            {
                candidate.Unlock();
            }
        }
    }

    // 바닥 평면(XZ) 기준으로 center에서 이 바리케이드까지의 가장 가까운 거리가 radius 이하인지 확인합니다.
    private bool IsWithinFlatRange(Vector3 center, float radius)
    {
        var flatCenter = new Vector2(center.x, center.z);

        // [방 전환] 꺼져 있으면(플레이어가 부모 방에 없음) 켜져 있을 때 기록해 둔 크기로 판정합니다.
        if (!gameObject.activeInHierarchy && boundsCached)
        {
            if (cachedBounds.Count == 0)
            {
                var tp = transform.position;
                return Vector2.Distance(flatCenter, new Vector2(tp.x, tp.z)) <= radius;
            }
            foreach (var b in cachedBounds)
            {
                if (FlatDistanceToBounds(b, flatCenter) <= radius) return true;
            }
            return false;
        }

        bool hasShape = false;

        foreach (var col in GetComponentsInChildren<Collider>())
        {
            if (col == null || !col.enabled) continue;
            hasShape = true;
            if (FlatDistanceToBounds(col.bounds, flatCenter) <= radius) return true;
        }

        if (!hasShape)
        {
            foreach (var rend in GetComponentsInChildren<Renderer>())
            {
                if (rend == null || !rend.enabled) continue;
                hasShape = true;
                if (FlatDistanceToBounds(rend.bounds, flatCenter) <= radius) return true;
            }
        }

        if (!hasShape)
        {
            var p = transform.position;
            return Vector2.Distance(flatCenter, new Vector2(p.x, p.z)) <= radius;
        }
        return false;
    }

    private static float FlatDistanceToBounds(Bounds b, Vector2 point)
    {
        float x = Mathf.Clamp(point.x, b.min.x, b.max.x);
        float z = Mathf.Clamp(point.y, b.min.z, b.max.z);
        return Vector2.Distance(point, new Vector2(x, z));
    }

    // 해제 이펙트를 뿌릴 영역입니다. 눈에 보이는 모양(렌더러)을 우선 쓰고, 없으면 콜라이더, 둘 다 없으면 위치 한 점입니다.
    private Bounds ComputeVisualBounds()
    {
        bool found = false;
        var bounds = new Bounds(transform.position, Vector3.zero);

        foreach (var rend in GetComponentsInChildren<Renderer>())
        {
            if (rend == null || !rend.enabled) continue;
            if (!found) { bounds = rend.bounds; found = true; }
            else bounds.Encapsulate(rend.bounds);
        }

        if (!found)
        {
            foreach (var col in GetComponentsInChildren<Collider>())
            {
                if (col == null || !col.enabled) continue;
                if (!found) { bounds = col.bounds; found = true; }
                else bounds.Encapsulate(col.bounds);
            }
        }

        return bounds;
    }
}
