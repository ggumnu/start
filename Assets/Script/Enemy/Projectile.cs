using System;
using System.Collections.Generic;
using UnityEngine;

// 탄환이 소멸하는 사유입니다. 특수효과 아이템이 OnDespawning을 구독할 때 "어떤 상황에서만
// 반응할지"를 이 값으로 구분할 수 있습니다 (예: 벽에 튕기는 효과는 HitWall일 때만 반응).
public enum ProjectileDespawnReason
{
    RangeExceeded, // 사거리를 다 소진함
    HitWall,       // "Wall" 태그와 충돌
    HitEnemy,      // "Enemy" 태그와 충돌 (데미지는 이미 적용된 뒤)
}

// Projectile.OnDespawning 구독자에게 전달되는 정보입니다. 여러 특수효과 아이템이 동시에
// 구독할 수 있으므로(예: "소멸 시 폭발" + "벽에 튕김"을 동시에 보유), 하나의 인스턴스를
// 순서대로 다 같이 들여다보고 각자 필요한 처리를 하는 방식입니다.
//
// [CancelDespawn - 관통/튕김 같은 효과를 위한 자리]
// 기본값은 false(=평소처럼 그대로 소멸)입니다. "소멸 시 폭발 이펙트만 만들고 싶다" 같은
// 효과는 이 값을 건드릴 필요 없이 원하는 처리만 하면 됩니다("알림형"으로 쓰는 셈). 반대로
// "벽에 튕겨나가야 한다"처럼 실제로 소멸을 막아야 하는 효과는 이 값을 true로 바꾸면
// Destroy가 취소됩니다. 실제로 방향을 바꾸는 건 Projectile.Redirect()를 호출하는 쪽
// (예: WallBounceEffect.cs)의 몫입니다.
//
// [SurfaceNormal - HitWall일 때만 의미 있음]
// 부딪힌 벽 표면의 방향(법선)입니다. Vector3.Reflect(원래 방향, 이 값)으로 반사 방향을
// 계산할 수 있습니다. RangeExceeded/HitEnemy일 때는 Vector3.zero입니다.
public class ProjectileDespawnEvent
{
    public Vector3 Position;
    public ProjectileDespawnReason Reason;
    public GameObject HitObject; // RangeExceeded일 때는 null
    public Vector3 SurfaceNormal; // HitWall일 때만 의미 있음
    public bool CancelDespawn = false;

    // [5번 패시브 연동] 충돌한 그 순간의 진행 방향입니다. 35번(벽 튕김)이 먼저 처리되어 탄환의
    // 방향이 이미 반사된 뒤에도, 이 값은 충돌 당시 방향 그대로라서 아이템 획득 순서와 상관없이
    // 안전하게 읽을 수 있습니다.
    public Vector3 IncomingDirection;

    // [110번 관통 총알] 관통 총알이 적/벽을 지나가는 중이면 true입니다. 이때는 소멸이 처음부터 취소된 상태(CancelDespawn = true)로
    // 들어오고 구독자가 바꿔도 다시 취소됩니다 - 35번/69번 튕김은 이 경우 반응하지 않습니다. 4번 폭발/5번 분열처럼 "닿을 때마다"
    // 반응하는 효과는 그대로 반응합니다.
    public bool IsPierceHit;
}

// 발사된 탄환 하나의 동작을 담당합니다.
// PlayerShooting이 생성 직후 Launch()를 호출해서 필요한 능력치 값을 전달합니다.
//
// [에디터 설정 필요사항]
// - 탄환 프리팹(임시 3D Sphere)에 Rigidbody 추가 → Is Kinematic 체크, Use Gravity 해제
// - Sphere Collider의 Is Trigger 체크
// - 플레이어 오브젝트의 Tag를 "Player"로 설정 (자기 자신 피격 방지용)
// - 벽 오브젝트의 Tag를 "Wall"로 설정 (임시 소멸 처리용, 나중에 Enemy 태그도 추가)
//
// [OnDespawning - 패시브 아이템 특수효과용 훅]
// 예전에는 세 소멸 지점(사거리 소진/벽 충돌/적 충돌)에서 각자 바로 Destroy(gameObject)를
// 불렀습니다. 이제는 RequestDespawn() 하나로 모아서, Destroy를 실제로 부르기 직전에
// OnDespawning을 먼저 흘려보내고, 구독자가 CancelDespawn을 true로 바꾸지 않았을 때만
// 소멸시킵니다 - 위 ProjectileDespawnEvent 주석 참고.
//
// [사거리 계산 - 누적 이동 거리 방식]
// 예전에는 "발사 지점 -> 현재 위치"의 직선 거리로 사거리 소진을 판정했습니다. 방향이 한 번도
// 안 바뀌면 문제없지만, 벽에 튕기는 것처럼 진행 중 방향이 바뀌는 효과가 생기면 직선 거리가
// 실제 이동 경로 길이와 달라집니다(튕겨서 되돌아가는 방향이 섞이면 오히려 줄어들 수도 있음).
// 그래서 매 물리 프레임 실제로 움직인 거리를 distanceTraveled에 계속 더하는 방식으로
// 바꿨습니다 - 직진만 하는 경우 결과는 예전과 완전히 동일합니다.
//
// [방 전환 - 그 방에서 쏜 총알은 그 방에서만]
// 발사하는 순간 플레이어가 있던 방을 기억합니다(originRoom - RoomScope 기준).
// - 그 방을 벗어나는 순간(문 너머 다음 방 칸으로 넘어가는 등) 사거리 소진과 같은 조용한 소멸로 사라집니다. 사거리 소진에는 어떤
//   아이템도 반응하지 않으므로(4번 폭발/5번 분열/35번·69번 튕김 등) 다른 방의 적을 맞히거나 효과를 일으키지 않습니다.
// - 플레이어가 다른 방에 들어가는 순간, 이전 방에서 날아가던 총알은 아무 반응 없이 바로 지워집니다(꼬리 포함 -
//   RoomScope.DestroyWhenPlayerLeaves).
// - 방을 판단할 수 없으면(테스트 씬 등) 예전과 똑같이 동작합니다.
[RequireComponent(typeof(Rigidbody))]
public class Projectile : MonoBehaviour
{
    private float damage;
    private float maxRange;
    private float speed;
    private Vector3 direction;
    private float distanceTraveled;
    private float totalDistanceTraveled; // v1.47: 리셋 안 되는 진짜 누적 거리
    private Vector3 lastStepStart;

    // [v1.42 추가] ComputeWallNormal()이 쓰는 SphereCast 결과 버퍼입니다. 매번 새로
    // 배열을 만들지 않도록 재사용합니다(AimTrajectoryIndicator.cs의 hitBuffer와 동일한
    // 이유).
    private static readonly RaycastHit[] wallNormalHitBuffer = new RaycastHit[16];

    // [v1.44 추가] RequestDespawn이 실제로 Destroy를 부른 뒤에는 더 이상 처리하지 않기
    // 위한 안전장치입니다 - 아래 [사거리/벽 충돌 순서] 주석 참고.
    private bool hasDespawned = false;

    private Rigidbody rb;

    // [5번 패시브 연동 - 분열탄 정보]
    // generation: 0 = 플레이어가 직접 발사한 원본, 1 = 원본이 분열해서 생긴 탄, 2 = 그 탄이 다시
    //   분열해서 생긴 탄 ...
    // effectScale: 이 탄환이 가진 "아이템 효과 배율"입니다. 원본은 1, 분열할 때마다 5번 아이템의
    //   effectRatio(기본 0.5)가 곱해집니다. 4번 폭발/11번 거리 피해 같은 효과가 자기 수치에 이
    //   값을 곱합니다.
    // ignoredEnemies: 이 탄환이 통과해야 하는 적입니다(분열의 원인이 된 부모가 이미 맞힌 적).
    private int generation = 0;
    private float effectScale = 1f;
    private float bulletSize = 1f;
    private bool isSplitProjectile = false;
    private readonly HashSet<EnemyHealth> ignoredEnemies = new HashSet<EnemyHealth>();

    // 지금 살아있는 분열탄(generation >= 1)의 총 개수입니다. 5번 아이템이 연쇄 분열로 탄환이
    // 폭증하는 것을 막는 안전장치로 사용합니다.
    public static int LiveSplitCount { get; private set; }

    public int Generation => generation;
    public float EffectScale => effectScale;
    public bool IsSplitProjectile => isSplitProjectile;

    // 5번 아이템이 분열탄의 사거리/속도/크기를 정할 때 부모 값을 읽는 용도입니다.
    public float MaxRange => maxRange;
    public float Speed => speed;
    public float BulletSize => bulletSize;

    public event Action<ProjectileDespawnEvent> OnDespawning;

    // [38번 패시브 연동] 플레이어 총알이 적에게 명중하는 모든 순간 전역으로 알립니다. 이
    // 클래스는 플레이어 전용 총알이므로(EnemyProjectile과 별도 클래스), 적 총알과 섞일
    // 걱정 없이 그대로 사용할 수 있습니다.
    public static event Action<EnemyHealth> OnPlayerBulletHitEnemy;

    // [118번 포격 등] OnPlayerBulletHitEnemy와 같은 순간(피해를 준 직후)에, 맞힌 총알 자신도 함께 알려줍니다(맞은 위치/진행 방향/5번 분열
    // 배율 EffectScale 등이 필요한 효과용). 그 총알로 적이 죽었어도 알립니다.
    public static event Action<Projectile, EnemyHealth> OnPlayerBulletHitEnemyDetailed;

    // 특수효과(예: 벽 튕김)가 현재 방향을 읽어 반사 방향을 계산할 때 사용합니다.
    public Vector3 Direction => direction;

    // 특수효과(예: 소멸 시 폭발)가 "이 탄환이 원래 가진 데미지"를 기준으로 자기 효과의
    // 피해량을 정할 때 사용합니다. 공격력 스탯/멀티샷 등이 이미 반영된 값입니다.
    // [피해 보너스] 거리 비례 피해 같은 효과(예: 11번 아이템)가 AddDamageBonusProvider로 등록해둔
    // 보너스(음수면 피해 감소)가 있으면, 이 값을 읽는 그 시점의 값으로 더해져서 반환됩니다(그래서
    // 폭발처럼 이 값을 읽어 피해를 정하는 다른 효과에도 자동으로 반영됩니다). 결과는 항상
    // SetMinDamage로 정한 최소 피해 이상입니다(기본 0 - 보너스 때문에 피해가 음수가 되지 않음).
    // 발사 당시의 기본 값이 필요하면 BaseDamage를 쓰세요.
    // [23번 30색 물감 세트] 이 총알에 랜덤 추가 피해가 굴려져 있으면, 위 계산이 전부 끝난 값에 그 값을 마지막으로
    // 더하고 23번의 최소 피해(기본 1)로 한 번 더 막습니다(형빈 확정 - 23번은 다른 효과 뒤에 덧씌우는 추가 피해).
    // [35번/69번 튕김 곱연산 - 형빈 확정] 순서: (기본 피해 + 덧셈 보너스) x 곱셈 배율 -> 최소 피해 하한 -> 23번 추가 피해.
    // 곱셈 배율은 23번 추가 피해에는 곱하지 않습니다(23번은 계속 "마지막에 덧씌우는 추가 피해").
    // [소멸 처리 중 피해 고정] OnDespawning 구독자들이 도는 동안에는, 그 충돌 순간의 피해로 고정된 값을 돌려줍니다 - 아래
    // RequestDespawn의 [피해 고정] 주석 참고.
    public float Damage
    {
        get
        {
            if (isDispatchingDespawn) return frozenDamage;

            float result = Mathf.Max(UnclampedDamage, minDamage);
            if (hasRandomDamageRoll)
            {
                result = Mathf.Max(result + randomDamageRoll.offset, randomDamageRoll.minDamage);
            }
            return result;
        }
    }

    // [23번 30색 물감 세트] 이 총알에 굴려진 랜덤 추가 피해/색입니다. Launch에서 정해지고 이후 바뀌지 않습니다.
    private bool hasRandomDamageRoll = false;
    private BulletDamageRoll randomDamageRoll;

    // 이 총알에 23번 랜덤 추가 피해가 굴려져 있는지, 그 색은 무엇인지입니다(4번 폭발 불꽃 색에 사용).
    public bool HasRandomDamageRoll => hasRandomDamageRoll;
    public Color RandomDamageColor => randomDamageRoll.color;

    // 피해 보너스가 감소 쪽으로 붙더라도 Damage가 이 값 밑으로 내려가지 않게 하는 최소 피해입니다.
    // 보통은 플레이어 스탯의 데미지 하한선을 넣습니다(11번 아이템이 탄환 생성 때 설정).
    private float minDamage = 0f;

    // 최소 피해를 정합니다. 0 미만은 0으로 취급합니다.
    public void SetMinDamage(float value)
    {
        minDamage = Mathf.Max(0f, value);
    }

    // 보너스가 전혀 붙지 않은, 발사 당시의 기본 피해량입니다.
    public float BaseDamage => damage;

    // 발사 지점에서부터 실제로 움직인 누적 거리입니다(튕겨서 되돌아간 거리도 모두 포함).
    // v1.47: distanceTraveled는 튕길 때마다 0으로 리셋되는 '이번 구간' 전용값이라(사거리
    // 판정용), 11번 거리 데미지가 이걸 그대로 읽으면 튕긴 뒤 거리가 초기화된 것처럼
    // 계산됨 - totalDistanceTraveled를 따로 둬서 분리.
    public float DistanceTraveled => totalDistanceTraveled;

    // [v1.46 추가] 지금까지 튕긴 전체 횟수입니다(resetDistanceTraveled: true로 Redirect가 호출된 횟수 - 벽 튕김 + 적 튕김).
    private int bounceCount = 0;
    public int BounceCount => bounceCount;

    // [69번 추가] 튕긴 횟수를 벽/적으로 나눠서 셉니다. 35번(벽 튕김 피해 x1.25)은 WallBounceCount를, 69번(적 튕김 피해
    // x0.25)은 EnemyBounceCount를 읽습니다. 벽 튕김은 Redirect(.., resetDistanceTraveled: true, ..), 적 튕김은
    // RedirectFromEnemy로 셉니다.
    private int wallBounceCount = 0;
    private int enemyBounceCount = 0;
    public int WallBounceCount => wallBounceCount;
    public int EnemyBounceCount => enemyBounceCount;

    // [69번 추가] 이 총알에 35번(벽 튕김) 효과가 붙어 있는지입니다. 35번이 탄환 생성 때 MarkWallBounceEffect로 표시합니다.
    // 69번은 이 표시가 있으면 적 튕김 피해 감소를 적용하지 않습니다(형빈 확정 - 35번+69번이면 적 튕김 x1). "지금 35번을
    // 들고 있는지"가 아니라 총알마다 판단하므로, 날아가는 도중에 35번을 잃어도(48번 등) 쏠 때의 규칙이 그대로 유지됩니다.
    private bool hasWallBounceEffect = false;
    public bool HasWallBounceEffect => hasWallBounceEffect;

    // [110번 관통 총알] 4발 이상 충전한 강화 총알은 적과 벽을 관통합니다(PlayerShooting.FireChargedShot이 SetPiercing(true)).
    // - 적/벽에 닿아도 소멸하지 않고 그대로 직진합니다(사거리를 다 쓰면 평소처럼 사라짐).
    // - 같은 적은 한 번만 맞습니다(콜라이더가 여러 개여도 - 맞힌 적은 통과 목록에 넣음).
    // - 35번(벽 튕김)/69번(적 튕김)은 관통 총알을 튕기지 않습니다(형빈 확정). 4번/5번 등 닿을 때마다 반응하는 효과는 그대로 반응합니다.
    private bool isPiercing = false;
    public bool IsPiercing => isPiercing;
    public void SetPiercing(bool value) { isPiercing = value; }
    public void MarkWallBounceEffect() { hasWallBounceEffect = true; }

    // [99번 적 관통 - 형빈 확정] 적만 관통합니다(벽에는 평소처럼 사라지거나 35번으로 튕김). Item99effect가 생성되는 모든 총알(5번 분열탄 포함)에
    // SetEnemyPiercing(true)를 겁니다. 110번 강화 총알의 관통(isPiercing - 적과 벽 모두)과는 별개 표시입니다.
    // - 적에게 닿으면 피해와 "적 명중" 처리(4번/5번/38번/68번/91번 등)를 평소대로 한 뒤, 소멸만 취소하고 계속 날아갑니다(사거리 끝에서 사라짐).
    // - 같은 적은 한 번만 맞습니다(piercedEnemies - 적 몸 안을 지나가는 동안 계속 피해를 주지 않음. 판정은 OnTriggerEnter라 들어갈 때만).
    // - 벽에 튕기면(35번 - Redirect) 그 기록을 비워서, 튕겨 돌아온 총알은 아까 뚫은 적을 다시 맞힐 수 있습니다.
    // - 소멸 알림에는 관통으로 표시(IsPierceHit)되어 69번은 튕기지 않습니다(애초에 99번과 69번은 같이 가질 수 없음).
    private bool isEnemyPiercing = false;
    public bool IsEnemyPiercing => isEnemyPiercing;
    public void SetEnemyPiercing(bool value) { isEnemyPiercing = value; }

    // [120번 벽 관통 - 형빈 확정] 벽만 관통합니다(적에게는 평소처럼 사라짐 - 99번과 함께면 둘 다 관통). Item120effect가 생성되는 모든 총알
    // (5번 분열탄 포함)에 SetWallPiercing(true)를 겁니다. 아래 "벽을 관통하는 총알 공통 규칙"은 110번 강화 총알(isPiercing)에도 똑같이 적용됩니다.
    // - 벽에 닿으면 "벽 명중" 처리(4번 폭발/5번 분열/벽 피격 이펙트)를 평소대로 한 뒤 소멸만 취소하고 계속 날아갑니다(35번은 튕기지 않음).
    // - 벽 한 덩어리를 지날 때 한 번만 반응합니다: 이미 다른 벽 콜라이더 안을 지나가는 중에 새로 닿은 벽 조각(겹쳐 붙은 조각)은 무시합니다.
    // - 무시할 벽(AddIgnoredWall - 5번 분열탄이 자기를 만든 벽)에는 반응하지 않습니다.
    // - 방 테두리를 넘는 순간 사라집니다(테두리 벽에 닿을 때의 반응은 위와 같이 일어나고, 그 뒤 방을 벗어나면 소멸 - 사거리 소진과 같은 조용한 소멸).
    private bool isWallPiercing = false;
    public bool IsWallPiercing => isWallPiercing;
    public void SetWallPiercing(bool value) { isWallPiercing = value; }
    public bool PiercesWalls => isPiercing || isWallPiercing;

    private readonly HashSet<Collider> insideWalls = new HashSet<Collider>();
    private readonly HashSet<GameObject> ignoredWalls = new HashSet<GameObject>();
    private bool boundaryRoomChecked;
    private RoomInstance boundaryRoom;

    // [방 전환] 발사하는 순간 플레이어가 있던 방입니다(null이면 방을 판단할 수 없음 = 예전처럼 동작).
    private RoomInstance originRoom;

    // [120번 + 5번] 이 벽 오브젝트(의 콜라이더)에 닿아도 아무 반응 없이 지나갑니다(벽을 관통하는 총알만 의미 있음).
    public void AddIgnoredWall(GameObject wall)
    {
        if (wall != null) ignoredWalls.Add(wall);
    }
    private readonly HashSet<EnemyHealth> piercedEnemies = new HashSet<EnemyHealth>();

    // [69번 추가] 마지막으로 튕겨 나온 적입니다. 그 적에게서 튕겨 나오는 동안 다시 맞지 않도록(콜라이더를 빠져나가는 도중
    // 다시 판정되거나, 목표 적이 그 적 뒤에 있어서 다시 관통하는 경우) 다른 적을 맞히거나 벽에 튕기기 전까지 통과합니다.
    private EnemyHealth lastBouncedEnemy = null;

    // [69번 추가] 최종 피해에 곱하는 배율을 계산하는 함수들입니다(35번 x1.25^벽 튕김, 69번 x0.25^적 튕김). 1이면 영향 없음.
    private readonly List<Func<Projectile, float>> damageMultiplierProviders = new List<Func<Projectile, float>>();

    public void AddDamageMultiplierProvider(Func<Projectile, float> provider)
    {
        if (provider == null) return;

        damageMultiplierProviders.Add(provider);
    }

    private float GetDamageMultiplier()
    {
        float multiplier = 1f;
        for (int i = 0; i < damageMultiplierProviders.Count; i++)
        {
            multiplier *= damageMultiplierProviders[i](this);
        }
        return multiplier;
    }

    // [69번 추가] 최소 피해 하한(SetMinDamage)과 23번 추가 피해를 적용하기 "전"의 피해입니다 - (기본 + 덧셈 보너스) x 곱셈 배율.
    // 69번의 "피해가 1 미만이 되면 소멸" 판정용입니다(11번 등의 최소 피해 하한 때문에 Damage로는 1 미만이 나오지 않을 수 있음).
    public float UnclampedDamage => (damage + GetBonusDamage()) * GetDamageMultiplier();

    // [소멸 처리 중 피해 고정] RequestDespawn 참고.
    private bool isDispatchingDespawn = false;
    private float frozenDamage = 0f;

    // Damage에 더해질 보너스를 계산하는 함수들입니다. 각 함수는 이 탄환을 받아 추가 피해량을
    // 돌려줍니다(0 이상이 일반적). Damage를 읽을 때마다 전부 호출해서 합산합니다.
    private readonly List<Func<Projectile, float>> damageBonusProviders = new List<Func<Projectile, float>>();

    // 이 탄환의 피해 보너스 계산 함수를 등록합니다(PlayerShooting.OnProjectileSpawned를 구독한
    // 아이템이 탄환마다 한 번씩 호출).
    public void AddDamageBonusProvider(Func<Projectile, float> provider)
    {
        if (provider == null) return;

        damageBonusProviders.Add(provider);
    }

    private float GetBonusDamage()
    {
        float bonus = 0f;
        for (int i = 0; i < damageBonusProviders.Count; i++)
        {
            bonus += damageBonusProviders[i](this);
        }
        return bonus;
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false; // 탄환은 중력의 영향을 받지 않습니다.
        rb.isKinematic = true; // 물리 힘이 아니라 스크립트로 직접 위치를 이동시킵니다.
    }

    // PlayerShooting에서 발사 직후 호출합니다.
    // damageRoll: [23번 30색 물감 세트] 이 총알에 붙을 랜덤 추가 피해/색입니다. 23번이 없으면 null(기존과 완전히 동일).
    public void Launch(Vector3 fireDirection, float bulletDamage, float bulletRange, float bulletSpeed, float bulletSize,
                       BulletDamageRoll? damageRoll = null)
    {
        direction = fireDirection.normalized;
        damage = bulletDamage;
        hasRandomDamageRoll = damageRoll.HasValue;
        randomDamageRoll = damageRoll.GetValueOrDefault();
        maxRange = bulletRange;
        speed = bulletSpeed;
        distanceTraveled = 0f;
        totalDistanceTraveled = 0f;
        lastStepStart = transform.position;

        this.bulletSize = bulletSize;
        transform.localScale = Vector3.one * bulletSize;
        transform.rotation = Quaternion.LookRotation(direction);

        // [아이템 총알 색] 38번처럼 총알 색을 바꾸는 아이템이 있으면 이 총알에만 색을 입힙니다(우선순위 높은 것, 같으면
        // 나중에 얻은 것 - ProjectileColorOverride 참고). 꼬리가 이 색을 읽어야 하므로 꼬리를 붙이기 전에 합니다.
        // [23번 30색 물감 세트] 23번 색은 다른 모든 총알 색보다 우선합니다(형빈 확정) - 있으면 그 색만 입힙니다.
        if (hasRandomDamageRoll)
        {
            ProjectileColorOverride.ApplyColor(gameObject, randomDamageRoll.color);
        }
        else
        {
            ProjectileColorOverride.ApplyTo(gameObject);
        }

        // [탄환 꼬리] 위치/크기가 정해진 뒤 별똥별 꼬리를 붙입니다(분열탄도 이 Launch를 거침). 꼬리 모양/색은
        // Resources/BulletTrailSettings 설정과 그 템플릿 프리팹에서 정하고, 탄환이 사라지면 꼬리는 스스로 사라집니다.
        BulletTrail.Attach(gameObject, BulletTrailKind.Bullet);

        // [방 전환] 쏜 방을 기억하고, 플레이어가 다른 방에 들어가면 바로 지워지도록 등록합니다(클래스 주석 [방 전환] 참고).
        originRoom = RoomScope.CurrentPlayerRoom();
        RoomScope.DestroyWhenPlayerLeaves(gameObject, originRoom);
    }

    // [5번 패시브] 분열로 생긴 탄환임을 표시합니다. PlayerShooting.SpawnSplitProjectile이
    // Launch() 직후, OnProjectileSpawned를 발생시키기 전에 호출합니다(그래야 4번/11번 같은
    // 효과가 탄환에 붙을 때 이미 세대/효과 배율을 읽을 수 있습니다).
    public void MarkAsSplit(int newGeneration, float newEffectScale, EnemyHealth enemyToIgnore)
    {
        generation = newGeneration;
        effectScale = newEffectScale;

        if (!isSplitProjectile)
        {
            isSplitProjectile = true;
            LiveSplitCount++;
        }

        if (enemyToIgnore != null) ignoredEnemies.Add(enemyToIgnore);
    }

    void OnDestroy()
    {
        if (isSplitProjectile)
        {
            isSplitProjectile = false;
            LiveSplitCount = Mathf.Max(0, LiveSplitCount - 1);
        }
    }

    // 진행 방향을 바꿉니다 (예: 벽 튕김 효과). resetDistanceTraveled가 false(기본값)이면
    // distanceTraveled/maxRange는 전혀 건드리지 않으므로, 여러 번 방향을 바꿔도
    // "지금까지 이동한 거리 + 앞으로 이동 가능한 거리"의 총합은 항상 원래 사거리
    // (maxRange) 안에서만 유지됩니다.
    //
    // [resetDistanceTraveled - v1.41 추가, 35번 벽 튕김 사거리 재계산용, 형빈 확정]
    // true로 넘기면 distanceTraveled를 0으로 되돌립니다 - 방향이 바뀐 이 지점을 새로운
    // 발사 지점처럼 취급해서, 원래 사거리(maxRange)만큼 처음부터 다시 이동할 수 있게
    // 됩니다(WallBounceEffect.cs 참고 - 사거리가 5인데 3만큼 날아가 튕기면 그 지점부터
    // 다시 5를 날아가 총 8만큼 이동합니다). 기본값 false는 이 매개변수가 생기기 전과
    // 100% 동일하게 동작합니다.
    // [rangeMultiplierOnReset - v1.45 추가, 무한 튕김 방지용, 형빈 확정]
    // resetDistanceTraveled가 true일 때만 의미가 있습니다. maxRange 자체에 이 배율을
    // 곱해서 줄여버립니다 - distanceTraveled를 0으로 되돌리는 것과 합치면, "이 지점부터
    // 다시 날아갈 수 있는 거리"가 이전 maxRange의 이 배율만큼으로 줄어드는 효과입니다.
    // maxRange 자체를 줄이므로 튕길 때마다 다시 곱해져 계속 작아집니다(예: 10 -> 7.5 ->
    // 5.625 ...) - 벽 사이에 낀 총알이 사거리 소진 없이 무한히 튕기는 문제를 막기 위한
    // 장치입니다(WallBounceEffect.cs 참고). 기본값 1f는 예전과 100% 동일하게(사거리 안
    // 줄어듦) 동작합니다.
    public void Redirect(Vector3 newDirection, bool resetDistanceTraveled = false, float rangeMultiplierOnReset = 1f)
    {
        if (newDirection.sqrMagnitude < 0.0001f) return;

        direction = newDirection.normalized;
        transform.rotation = Quaternion.LookRotation(direction);

        if (resetDistanceTraveled)
        {
            distanceTraveled = 0f;
            maxRange *= rangeMultiplierOnReset;
            bounceCount++;
            wallBounceCount++;

            // [69번 연동] 벽에 튕긴 뒤에는 아까 튕겨 나온 적을 다시 맞힐 수 있습니다.
            lastBouncedEnemy = null;

            // [99번 적 관통] 벽에 튕긴 뒤에는 아까 뚫고 지나간 적도 다시 맞힐 수 있습니다(형빈 확정).
            piercedEnemies.Clear();
        }

        // 방향을 바꾼 직후 살짝 그 방향으로 밀어내, 부딪혔던 콜라이더와 겹친 상태로 남아
        // 다음 물리 프레임에 같은 벽에 대해 또 판정이 걸리는 것을 방지합니다.
        transform.position += direction * 0.05f;
    }

    // [69번 추가] 적에게 맞고 튕겨 나갑니다. 벽 튕김과 똑같이 그 지점을 새 발사 지점으로 삼고 사거리에 rangeMultiplier를
    // 곱하지만, 튕긴 횟수는 적 튕김(EnemyBounceCount)으로 셉니다. fromEnemy는 방금 맞힌 적이라, 다른 적을 맞히거나 벽에
    // 튕기기 전까지는 다시 맞지 않습니다.
    public void RedirectFromEnemy(Vector3 newDirection, float rangeMultiplier, EnemyHealth fromEnemy)
    {
        if (newDirection.sqrMagnitude < 0.0001f) return;

        direction = newDirection.normalized;
        transform.rotation = Quaternion.LookRotation(direction);

        distanceTraveled = 0f;
        maxRange *= rangeMultiplier;
        bounceCount++;
        enemyBounceCount++;
        lastBouncedEnemy = fromEnemy;

        transform.position += direction * 0.05f;
    }

    void FixedUpdate()
    {
        lastStepStart = transform.position;

        Vector3 moveStep = direction * speed * Time.fixedDeltaTime;
        rb.MovePosition(rb.position + moveStep);

        distanceTraveled += moveStep.magnitude;
        totalDistanceTraveled += moveStep.magnitude;
    }

    // [v1.44 변경 - 벽에 튕긴 직후인데도 사거리 소진으로 먼저 사라지는 버그, 형빈 확인 요청]
    // 예전에는 사거리 소진 체크를 FixedUpdate 안에서 이동 직후 바로 했습니다. 문제는, 벽
    // 충돌(OnTriggerEnter)은 이 FixedUpdate가 "모두" 끝난 뒤 유니티 물리 엔진이 그 다음에
    // 처리한다는 점입니다 - 즉 한 물리 스텝 안에서 "이동 -> 사거리 다 됐으니 소멸" 판정이
    // "벽에 부딪혔으니 튕겨라(사거리 리셋)" 판정보다 먼저 일어나 버립니다. 사거리가 벽까지의
    // 거리와 비슷하게 잡힌 경우(흔함), 총알이 벽에 닿는 바로 그 순간에 사거리도 같이 다
    // 떨어지는 경우가 많아서, WallBounceEffect가 튕기라고 방향을 바꾸기도 전에 이미
    // RangeExceeded로 Destroy가 불려버려 "벽 바로 앞에서 튕기지도 못하고 사라지는" 현상이
    // 생깁니다. 그래서 사거리 체크를 Update()로 옮겼습니다 - Update는 이번 물리 스텝의
    // OnTriggerEnter가 전부 처리되고 난 뒤에 실행되므로, 이번 프레임에 벽에 튕겨
    // distanceTraveled가 이미 0으로 리셋됐다면 여기서는 그 리셋된 값을 보게 되어 잘못
    // 소멸되지 않습니다.
    void Update()
    {
        if (distanceTraveled >= maxRange)
        {
            RequestDespawn(ProjectileDespawnReason.RangeExceeded, null, Vector3.zero);
            return;
        }

        // [방 전환] 쏜 방을 벗어나면 조용히 사라집니다(사거리 소진과 같은 소멸 - 어떤 아이템도 반응하지 않음).
        if (originRoom != null && !hasDespawned &&
            !ReferenceEquals(RoomScope.RoomOf(transform.position), originRoom))
        {
            RequestDespawn(ProjectileDespawnReason.RangeExceeded, null, Vector3.zero);
            return;
        }

        // [120번/110번 벽 관통] 방 테두리를 넘으면 조용히 사라집니다(사거리 소진과 같은 소멸 - 4번/5번은 반응하지 않음).
        // 처음 확인한 순간의 방을 기준으로 합니다(LevelBuilder가 없으면 방이 null이라 경계 없음).
        if (PiercesWalls && !hasDespawned)
        {
            if (!boundaryRoomChecked)
            {
                boundaryRoomChecked = true;
                boundaryRoom = WallPierceOverride.RoomOf(transform.position);
            }
            else if (boundaryRoom != null && !ReferenceEquals(WallPierceOverride.RoomOf(transform.position), boundaryRoom))
            {
                RequestDespawn(ProjectileDespawnReason.RangeExceeded, null, Vector3.zero);
            }
        }
    }

    void OnTriggerEnter(Collider other)
    {
        // [총알 1발 = 적 1명 - 형빈 확정] 서로 겹쳐 있는 적 두 마리의 겹친 부분을 맞히면, 같은 물리 스텝 안에서 두 적의
        // OnTriggerEnter가 연달아 들어옵니다. Destroy는 프레임 끝에 실제로 처리되므로, 예전에는 첫 적에게 피해를 주고
        // 소멸이 확정된 뒤에도 두 번째 적에게까지 피해가 들어갔습니다. 이미 소멸이 확정된 총알은 더 이상 아무것도
        // 맞히지 않습니다. 관통처럼 소멸을 취소한(CancelDespawn) 총알은 hasDespawned가 false로 남아 기존대로 계속 맞힙니다.
        if (hasDespawned) return;

        if (other.CompareTag("Player")) return; // 자기 자신을 쏜 플레이어는 무시

        if (other.CompareTag("Enemy"))
        {
            EnemyHealth enemy = other.GetComponent<EnemyHealth>();

            // [5번 패시브] 분열의 원인이 된 부모가 이미 맞힌 적은 그대로 통과합니다(피해도,
            // 소멸도 없음). 분열탄이 태어날 때 그 적의 콜라이더와 겹쳐 있어도 바로 사라지지
            // 않게 하는 역할도 합니다.
            if (enemy != null && ignoredEnemies.Contains(enemy)) return;

            // [69번] 방금 튕겨 나온 적은 통과합니다(RedirectFromEnemy 참고).
            if (enemy != null && enemy == lastBouncedEnemy) return;

            // [99번 적 관통] 이번에(마지막 벽 튕김 이후) 이미 뚫고 지나간 적은 통과합니다(피해/소멸 판정 없음).
            if (enemy != null && isEnemyPiercing && piercedEnemies.Contains(enemy)) return;

            if (enemy != null)
            {
                // [113번 슈가 중첩 등 - 총알 명중 추가 피해] 총알 피해(모든 총알 보정 반영)에 추가 피해를 더해 한 번에 줍니다. 41번 같은
                // "적이 받는 피해 % 증가"는 TakeDamage 안에서 이 합계에 마지막으로 곱해집니다(PlayerBulletBonusDamage.cs 참고).
                // 중첩은 아래 명중 알림(피해를 준 뒤)에서 쌓이므로, 이번 총알은 "쌓이기 전" 중첩 기준입니다.
                enemy.TakeDamage(Damage + PlayerBulletBonusDamage.Resolve(enemy));
                OnPlayerBulletHitEnemy?.Invoke(enemy); // [38번 패시브 연동]
                OnPlayerBulletHitEnemyDetailed?.Invoke(this, enemy); // [118번 포격 등]

                // [110번 관통 총알] 같은 적을 다시 맞히지 않도록 통과 목록에 넣습니다.
                if (isPiercing) ignoredEnemies.Add(enemy);

                // [99번 적 관통] 같은 적을 다시 맞히지 않도록 기록합니다(벽에 튕기면 비워짐).
                if (isEnemyPiercing) piercedEnemies.Add(enemy);
            }
            RequestDespawn(ProjectileDespawnReason.HitEnemy, other.gameObject, Vector3.zero);
            return;
        }

        if (other.CompareTag("Wall"))
        {
            // [120번/110번 벽 관통] 벽 한 덩어리에 한 번만 반응하고, 무시할 벽에는 반응하지 않습니다.
            if (PiercesWalls)
            {
                insideWalls.RemoveWhere(c => c == null || !c.enabled || !c.gameObject.activeInHierarchy);
                bool alreadyInsideWall = insideWalls.Count > 0;
                insideWalls.Add(other);
                if (alreadyInsideWall) return;
                if (ignoredWalls.Contains(other.gameObject)) return;
            }

            Vector3 normal = ComputeWallNormal();
            RequestDespawn(ProjectileDespawnReason.HitWall, other.gameObject, normal);
        }
    }

    // [120번/110번 벽 관통] 지나가던 벽 콜라이더에서 빠져나오면 "벽 안"에서 뺍니다(그 뒤 새로 닿는 벽에는 다시 반응).
    void OnTriggerExit(Collider other)
    {
        if (insideWalls.Count == 0 || other == null) return;
        insideWalls.Remove(other);
    }

    // [v1.42 변경 - 얇은 Raycast에서 SphereCast로 교체, 형빈 확인 요청]
    // 예전에는 얇은(두께 0) Raycast로만 벽 표면을 찾았는데, 완만한(비스듬한) 각도로
    // 벽을 스치듯 지나갈 때 이 레이가 "어딘가에는" 맞긴 하지만 실제로 트리거를 발생시킨
    // 그 벽이 아니라 근처의 다른 콜라이더(모서리 너머의 다른 벽 조각 등)를 잘못 맞혀서,
    // 튕기는 방향이 실제 부딪힌 면과 무관하게 엉뚱해지는(플레이어 쪽으로 되돌아오는 등)
    // 현상이 있었던 것으로 보입니다(레이 자체가 완전히 빗나가는 경우는 드물어서, 아래
    // 대체값 로그는 거의 뜨지 않았습니다). AimTrajectoryIndicator.cs가 조준선 미리보기에
    // 쓰는 것과 동일하게, 실제 총알 크기(bulletSize)를 반지름으로 하는 SphereCast로
    // 바꿨습니다 - 얇은 선 대신 총알만큼 굵은 구체로 훑기 때문에 좁은 틈이나 모서리에서도
    // 실제로 부딪힌 면을 훨씬 안정적으로 찾습니다. 여러 콜라이더가 걸릴 수 있어
    // SphereCastNonAlloc으로 전부 받은 뒤, "Wall" 태그가 붙은 것 중 가장 가까운 면의
    // 법선만 사용합니다(트리거 콜라이더는 무시 - Wall은 보통 일반 콜라이더입니다).
    private Vector3 ComputeWallNormal()
    {
        float radius = Mathf.Max(bulletSize * 0.5f, 0.001f);

        // [v1.44 변경 - 거의 항상 오던 방향의 정반대로 튕기는 진짜 원인, 형빈 확인 요청]
        // 예전에는 lastStepStart(이번 스텝이 시작된 지점)에서 바로 SphereCast를 쐈습니다.
        // 그런데 이 캐스트 거리(한 물리 스텝의 짧은 이동 거리 + 여유 0.1)가 구체 반지름
        // (bulletSize 절반)보다 작거나 비슷한 경우가 흔해서, 사실상 "스피어가 시작부터 이미
        // 벽에 거의 닿아 있는" 상태로 캐스트가 시작되는 셈이었습니다. 물리 엔진은 캐스트
        // 시작 지점에서 이미 겹쳐 있거나 거의 닿아 있는 콜라이더에 대해서는 법선을 신뢰할 수
        // 없게 반환하는 경우가 많고(대략 캐스트 방향의 정반대에 가까운 값), 이게 바로 "벽
        // 각도와 무관하게 거의 항상 오던 방향으로 되돌아가는" 현상의 진짜 원인으로 보입니다.
        // AimTrajectoryIndicator.cs의 조준선 캐스트는 플레이어 위치에서 벽까지 먼 거리를
        // 미리 쭉 훑기 때문에(스피어가 벽과 멀리 떨어진 채로 캐스트 시작) 이 문제가 없어서,
        // 조준선에 보이는 반사각은 정상이었던 것입니다. 고치는 방법: 캐스트 시작점을 실제
        // 이동 시작 지점보다 반지름만큼 더 뒤로 물려서, 스피어가 벽에 닿기 "전"부터 확실히
        // 떨어진 상태로 캐스트가 시작되게 만듭니다(조준선 쪽도 같은 방식으로 맞췄습니다).
        float backoff = radius + 0.2f;
        Vector3 castOrigin = lastStepStart - direction * backoff;
        float castDistance = Vector3.Distance(lastStepStart, transform.position) + backoff + 0.1f;

        int hitCount = Physics.SphereCastNonAlloc(
            castOrigin, radius, direction, wallNormalHitBuffer, castDistance,
            Physics.AllLayers, QueryTriggerInteraction.Ignore);

        float closestDistance = float.MaxValue;
        Vector3 closestNormal = Vector3.zero;
        bool found = false;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = wallNormalHitBuffer[i];
            if (!hit.collider.CompareTag("Wall")) continue; // Wall이 아니면 무시

            if (hit.distance < closestDistance)
            {
                closestDistance = hit.distance;
                closestNormal = hit.normal;
                found = true;
            }
        }

        if (found) return closestNormal;

        // [진단용 로그 - v1.41 추가, v1.44 기준으로도 유지 - 이 경고가 뜨면 알려주세요]
        // 이 대체값(-direction, 오던 방향으로 정반대 반사)을 쓰면 Vector3.Reflect가 입사
        // 방향을 거의 그대로 뒤집어버립니다. 위 backoff 수정 뒤로는 거의 뜨지 않아야
        // 정상입니다 - 그래도 뜬다면 다른 원인을 다시 찾아야 합니다.
        Debug.LogWarning($"Projectile: ComputeWallNormal SphereCast가 Wall 콜라이더를 못 찾아 " +
                          $"대체값(정반대 반사)을 사용합니다. lastStepStart={lastStepStart}, " +
                          $"position={transform.position}, direction={direction}, castDistance={castDistance}");
        return -direction;
    }

    // 세 소멸 지점이 공통으로 거치는 창구입니다. OnDespawning 구독자에게 먼저 알리고,
    // 아무도 CancelDespawn을 true로 바꾸지 않았을 때만 실제로 Destroy합니다.
    private void RequestDespawn(ProjectileDespawnReason reason, GameObject hitObject, Vector3 surfaceNormal)
    {
        // [v1.44 추가] 이미 실제로 Destroy가 확정된 뒤에 또 다른 소멸 사유(예: 같은 프레임에
        // 벽/적 판정이 겹침)가 뒤늦게 들어오면 무시합니다 - Redirect처럼 살아있는 탄환을
        // 전제로 한 처리가 이미 소멸 확정된 탄환에 대해 실행되는 것을 막기 위한 안전장치입니다.
        if (hasDespawned) return;

        var despawnEvent = new ProjectileDespawnEvent
        {
            Position = transform.position,
            Reason = reason,
            HitObject = hitObject,
            SurfaceNormal = surfaceNormal,
            IncomingDirection = direction,
        };

        // [110번 관통 총알] 적/벽에 닿은 것이면 처음부터 소멸을 취소한 상태로 알립니다(35번/69번은 이걸 보고 튕기지 않음).
        // [99번 적 관통] 적에 닿은 경우에만 같은 방식으로 관통 처리합니다(벽/사거리 소진은 평소대로).
        bool pierceHit = (isPiercing && reason != ProjectileDespawnReason.RangeExceeded) ||
                         (isEnemyPiercing && reason == ProjectileDespawnReason.HitEnemy) ||
                         (isWallPiercing && reason == ProjectileDespawnReason.HitWall); // [120번 벽 관통]
        if (pierceHit)
        {
            despawnEvent.IsPierceHit = true;
            despawnEvent.CancelDespawn = true;
        }

        // [피해 고정 - 69번 때 추가] 구독자(35번/69번 튕김, 4번 폭발 등)는 아이템을 얻은 순서대로 불립니다. 튕김 효과가 먼저
        // 불려 튕긴 횟수가 늘면 그 뒤에 불리는 4번 폭발이 "튕긴 뒤의 피해"(69번이면 x0.25)로 터지는 등, 결과가 획득 순서에
        // 따라 달라졌습니다. 구독자들이 도는 동안은 Damage를 충돌 순간의 값으로 고정해서 순서와 상관없이 같게 만듭니다.
        frozenDamage = Damage;
        isDispatchingDespawn = true;
        try
        {
            OnDespawning?.Invoke(despawnEvent);
        }
        finally
        {
            isDispatchingDespawn = false;
        }

        // [110번 관통 총알] 구독자가 취소를 풀었더라도 관통 중에는 사라지지 않습니다.
        if (pierceHit) despawnEvent.CancelDespawn = true;

        if (!despawnEvent.CancelDespawn)
        {
            hasDespawned = true;
            Destroy(gameObject);
        }
    }
}