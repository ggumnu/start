using UnityEngine;

// [사용 방법]
// 1. Player 오브젝트(PlayerAimAndAttack, PlayerStats, PlayerShooting이 붙어있는 바로 그
//    오브젝트)에 이 스크립트를 추가합니다. 별도로 만들어야 할 프리팹이나 머티리얼은 없습니다 -
//    필요한 시각 오브젝트(선)와 머티리얼을 전부 코드에서 런타임에 만듭니다.
// 2. Inspector에 노출된 필드는 전부 기본값 그대로 두고 써도 되고, 색/두께/장애물 판정 방식만
//    취향껏 조절하면 됩니다. 자세한 항목 설명은 아래 각 필드의 Tooltip 참고.
//
// [동작 방식]
// - 우클릭 조준 중(PlayerAimAndAttack.IsAiming == true)에만 바닥에 반투명 흰색 띠가 보입니다.
//   조준을 풀면 즉시 사라집니다.
// - 방향은 PlayerShooting.TryFire()에 실제로 전달되는 것과 동일한 transform.forward를
//   그대로 사용합니다. 즉 "이 선이 가리키는 방향 = 실제로 총알이 나갈 방향"이 항상 일치합니다.
// - 길이는 PlayerStats.Range, 두께는 PlayerStats.BulletSize(총알 프리팹 기본 지름 1m 기준
//   그대로 m 단위로 매핑)를 매 프레임 다시 읽기 때문에, 사거리/총알크기 아이템을 먹으면
//   조준선도 즉시 그 값에 맞춰 늘어나거나 줄어듭니다.
// - stopAtWalls가 켜져 있으면(기본값), firePoint에서 조준 방향으로 "실제 총알 크기(BulletSize)를
//   반지름으로 하는" SphereCast를 쏴서, wallTag(기본 "Wall")가 붙은 콜라이더를 만나는 지점까지만
//   선을 그립니다. 두께 0짜리 Raycast가 아니라 실제 총알만큼 굵은 구체로 쓸어보기 때문에, 벽 틈이
//   총알 지름보다 좁아서 실제로는 못 지나가는 경우까지 반영됩니다(총알이 커질수록 이 오차가
//   커지는 문제라 SphereCast로 바꿨습니다). Width Multiplier는 화면에 보이는 두께에만 쓰이고
//   이 판정에는 영향을 주지 않습니다 - 항상 "진짜 총알 크기" 기준으로 판정합니다.
//   Projectile.cs가 오직 "Wall"과 "Enemy" 태그만 보고 나머지는 그냥 통과하는 것과 동일하게,
//   이 SphereCast도 Wall 태그가 아닌 것(적, 소품 등)은 전부 무시하고 통과시킵니다 - 그래야
//   "실제로 총알이 멈추는 지점"과 조준선이 항상 일치합니다.
//
// [19번 패시브 - 양갈래 조준선, v1.40 추가]
// 19번(양갈래 발사)을 보유하면 PlayerShooting이 매 발을 정중앙이 아니라 좌우로
// SplitShotOverride.HalfAngle도씩 벌어진 두 발로 쏩니다(PlayerShooting.cs 참고). 조준선도
// 실제 탄도와 항상 일치해야 하므로, 중앙선 한 줄이 아니라 이 벌어진 두 방향 각각으로 선을
// 보여줘야 합니다. 그래서 "방향" 단위로 2개(directionSegments[0]/[1])를 만들어두고,
// SplitShotOverride.IsActive가 꺼져 있으면 방향 0만 각도 0(중앙)으로 켜고 방향 1은 끄며,
// 켜져 있으면 두 방향을 각각 -HalfAngle/+HalfAngle로 켭니다 - 즉 19번을 얻기 전에는 기존과
// 100% 동일하게 동작합니다.
//
// [137번 패시브 - 확산 조준선 추가]
// 137번(확산 - 정중앙 + 좌우 15도)까지 생기면서 한 번에 나가는 방향이 최대 6개(19번 2총구 x 137번 3갈래)가 됐습니다.
// 다만 확산 줄까지 전부 그리면 너무 복잡해서(형빈 확정), 조준선은 SpreadShotOverride.FillAimAngles로 "총구 방향"만 받습니다 -
// 137번만이면 중앙 한 줄, 19번과 함께면 19번 양갈래 두 줄입니다(실제 발사는 3발/6발 그대로). 방향 오브젝트는 나중을 위해
// MaxShotCount(6)개를 미리 만들어 두고, 받은 각도 수만큼 앞에서부터 켜고 남는 방향은 끕니다. 19번/137번이 없으면 방향 0 하나만
// 각도 0으로 켜지므로 기존과 100% 같습니다.
//
// [35번 패시브 - 벽 튕김 조준선 미리보기, v1.41 추가]
// 35번(벽 튕김)을 보유하면 총알이 벽에 닿아도 사라지지 않고 그 지점부터 사거리를 처음부터
// 다시 계산해서 튕겨 나갑니다(Item35effect.cs/Projectile.Redirect 참고). 조준선도 사거리
// 안쪽에서 벽에 닿으면 그 자리에서 끊기지 않고, 튕긴 뒤 날아갈 방향까지 이어서 보여줍니다.
// 그래서 "방향" 하나마다 선을 한 조각(segment)이 아니라 여러 조각(segment)으로 표현할 수
// 있도록 확장했습니다 - segmentsPerDirection = 1 + maxPreviewBounces입니다. 매 방향마다
// 첫 조각을 쏘고, 그 조각이 벽에 막혀서 끝났고 WallBounceOverride.IsActive(35번 보유 중)이면
// 그 지점을 새 시작점, 반사된 방향을 새 방향, maxRange를 다시 꽉 채운 사거리로 삼아 다음
// 조각을 잇습니다 - 실제 Projectile이 튕길 때와 완전히 동일한 계산(Vector3.Reflect +
// 사거리 초기화)이라 조준선과 실제 탄도가 항상 일치합니다. maxPreviewBounces(기본 1)로
// 몇 번 튕기는 지점까지 미리 보여줄지 조절할 수 있습니다 - 이 값은 Awake() 시점에 필요한
// 만큼 선 오브젝트를 미리 만들어두는 데 쓰이므로, 플레이 중에 바꾸면 반영되지 않고 다음
// 플레이 시작(재생 재시작) 때부터 적용됩니다.
//
// [110번 장신구 - 충전 사격 미리보기]
// 110번으로 충전 중(1발 이상)이면 조준선 길이/두께를 충전으로 늘어난 사거리/총알 크기로 바로바로 바꿔 보여줍니다
// (PlayerChargeShot.TryGetAimPreview). 관통 기준(기본 4발) 이상이면 실제 총알처럼 벽에서 끊기지 않고 사거리 끝까지 그리며,
// 35번 벽 튕김 미리보기도 하지 않습니다(관통 총알은 튕기지 않음). 충전하지 않을 때는 기존과 100% 같습니다.
//
// [PlayerHitFlash.cs와의 연동 - 중요]
// 선이 여러 개(방향 x 조각)가 되면서, 캐릭터 무적 점멸(PlayerHitFlash.cs)이 전부를 알아야
// 점멸/틴트 대상에서 제대로 제외할 수 있습니다. 그래서 LineRenderer(단수) 프로퍼티를
// LineRenderers(배열)로 바꿨습니다 - PlayerHitFlash.cs도 이 변경에 맞춰 함께 수정되어
// 있습니다.
//
// [에디터 설정 필요사항 - 중요]
// - Wall 태그가 붙은 콜라이더는 Box/Capsule/Sphere Collider이거나, Mesh Collider라면 반드시
//   Convex 체크박스가 켜져 있어야 합니다. Unity의 SphereCast는 Convex가 꺼진 일반 Mesh
//   Collider를 감지하지 못하고 조용히 통과시켜버리기 때문입니다(에러 없이 그냥 무시됨). 그 외에는
//   Add Component만으로 바로 동작합니다.
[RequireComponent(typeof(PlayerAimAndAttack))]
[RequireComponent(typeof(PlayerStats))]
[RequireComponent(typeof(PlayerShooting))]
public class AimTrajectoryIndicator : MonoBehaviour
{
    [Header("참조 (비워두면 자동으로 찾습니다)")]
    public PlayerAimAndAttack aimAndAttack;
    public PlayerStats stats;
    public PlayerShooting shooting;

    [Tooltip("총 모드(1키)가 아닐 때(수류탄/해킹툴 모드) 총 조준선을 숨기기 위해 참조합니다. 같은 " +
             "오브젝트에 있으면 자동으로 찾고, 없으면 기존과 동일하게 동작합니다.")]
    public PlayerWeaponMode weaponMode;

    [Header("시각 설정")]
    [Tooltip("조준선의 색상과 투명도(알파값)입니다. 기본값은 반투명 흰색입니다.")]
    public Color lineColor = new Color(1f, 1f, 1f, 0.35f);

    [Header("크기 설정")]
    [Tooltip("총알 크기(BulletSize, 지름 m 기준)에 곱해지는 배수입니다. 1이면 실제 총알 " +
             "지름과 동일한 두께로 표시됩니다. 눈에 더 잘 띄게 하고 싶으면 1보다 크게 조절하세요.")]
    public float widthMultiplier = 1f;

    [Tooltip("총알 크기 디버프 등으로 두께가 아주 작아져도 조준선이 안 보이지 않도록 하는 " +
             "최소 두께(m)입니다.")]
    public float minWidth = 0.05f;

    [Tooltip("바닥과 정확히 겹쳐서 깜빡이는(Z-fighting) 현상을 막기 위해, 조준선을 바닥에서 " +
             "살짝 띄우는 높이(m)입니다.")]
    public float groundOffset = 0.03f;

    [Header("장애물 판정")]
    [Tooltip("켜두면(기본값) wallTag가 붙은 콜라이더에 막혔을 때 조준선이 그 지점에서 끊깁니다. " +
             "끄면 장애물과 무관하게 항상 사거리 끝까지 표시합니다.")]
    public bool stopAtWalls = true;

    [Tooltip("조준선을 막는 것으로 판정할 콜라이더의 태그입니다. Projectile.cs가 탄환을 " +
             "멈추는 태그(\"Wall\")와 반드시 동일하게 맞춰야 실제 사격 결과와 일치합니다.")]
    public string wallTag = "Wall";

    [Header("벽 튕김 미리보기 (35번 패시브)")]
    [Tooltip("35번(벽 튕김) 보유 중일 때, 벽에 튕기는 지점을 몇 번까지 이어서 미리 보여줄지" +
             "입니다. 0이면 첫 벽에서 그대로 끊깁니다(35번을 얻기 전과 동일). 플레이 중에 바꾸면" +
             "즉시 반영되지 않고, 다음 재생 시작 때부터 적용됩니다.")]
    [Min(0)] public int maxPreviewBounces = 1;

    // 선 한 조각(오브젝트 + 렌더러)을 표현하는 최소 단위입니다.
    private class LineSegment
    {
        public Transform transform;
        public MeshRenderer renderer;
    }

    // directionSegments[0] = 중앙(또는 양갈래일 때 좌측), [1] = 양갈래일 때 우측(평소엔 꺼둠).
    // 각 방향은 segmentsPerDirection개의 조각으로 구성되고, 조각 0이 발사 지점에서 첫 벽까지,
    // 조각 1이 그 벽에서 튕긴 뒤 다음 벽(또는 사거리 끝)까지... 를 나타냅니다.
    private LineSegment[][] directionSegments;
    private int segmentsPerDirection;
    private Mesh quadMesh;
    private Material runtimeMaterial;

    // PlayerHitFlash.cs가 무적 시간 점멸 연출 대상(플레이어 캐릭터 렌더러)을 모을 때, 같은
    // Player 자식 트리에 있는 이 조준선 렌더러들까지 함께 걸러지는 걸 막기 위해 열어둔 창구입니다.
    // 조준선은 플레이어 캐릭터의 "몸"이 아니라 별도의 UI성 표시 요소라서, 무적 점멸/틴트 대상에서
    // 제외되어야 합니다. 오브젝트 이름("AimTrajectoryLine")으로 걸러내는 방식보다, 이렇게 실제
    // 렌더러 참조를 직접 비교하는 방식이 이름 변경이나 구조 변경에도 안전합니다.
    public Renderer[] LineRenderers { get; private set; }

    // SphereCast는 Raycast보다 한 번에 더 많은 콜라이더를 걸칠 수 있어서 여유 있게 잡아둡니다.
    private readonly RaycastHit[] hitBuffer = new RaycastHit[32];

    // [110번] 충전 미리보기 - 이번 프레임에 쓸 사거리/총알 크기 가산과 관통 여부입니다(Update 시작에 갱신).
    private PlayerChargeShot chargeShot;
    private float chargeRangeBonus;
    private float chargeSizeBonus;
    private bool chargePiercing;

    // 지금 조준선이 쓸 사거리/총알 크기입니다(충전 중이면 충전 가산 포함).
    float CurrentRange => Mathf.Max((stats != null ? stats.Range : 0f) + chargeRangeBonus, 0f);
    float CurrentBulletSize => (stats != null ? stats.BulletSize : 1f) + chargeSizeBonus;

    // [137번] 이번 프레임에 켤 방향들의 각도 목록입니다(SpreadShotOverride.FillShotAngles가 채움).
    private readonly System.Collections.Generic.List<float> shotAngles =
        new System.Collections.Generic.List<float>(SpreadShotOverride.MaxShotCount);

    // [깜빡임 버그 수정 - v1.41] 튕긴 지점에 정확히 붙어서 다음 SphereCast를 쏘면, 그
    // 구체가 방금 튕긴 바로 그 벽과 겹친 상태로 시작해버려 거리 0 근처의 판정이 매 프레임
    // 아주 살짝씩 흔들리면서 깜빡였습니다. Projectile.Redirect()가 튕긴 뒤 실제 위치를
    // direction * 0.05f만큼 밀어서 같은 문제를 피하는 것과 동일하게, 미리보기 선도 다음
    // 조각을 계산하기 전에 새 방향으로 이만큼 밀어서 벽 표면과의 겹침을 피합니다.
    private const float BounceStartNudge = 0.05f;

    void Awake()
    {
        if (aimAndAttack == null) aimAndAttack = GetComponent<PlayerAimAndAttack>();
        if (stats == null) stats = GetComponent<PlayerStats>();
        if (shooting == null) shooting = GetComponent<PlayerShooting>();
        if (weaponMode == null) weaponMode = GetComponent<PlayerWeaponMode>();

        BuildLineObjects();
    }

    // 조준선을 표시할 자식 오브젝트(MeshFilter+MeshRenderer)들을 만듭니다. 방향은 항상 2개
    // (19번 양갈래 지원 - 평소엔 방향 0만 켜져서 기존과 동일하게 보이고, 방향 1은 꺼둔 채로
    // 대기합니다), 방향마다 조각은 1 + maxPreviewBounces개(35번 벽 튕김 미리보기 지원)를
    // 만들어둡니다. 모든 조각이 같은 메쉬/머티리얼을 공유합니다(모양과 색이 항상 같으므로
    // 각자 새로 만들 필요가 없습니다). Player의 자식으로 붙여서, 레벨 전환 시 Player가
    // 유지되면(DontDestroyOnLoad 등) 이 선 오브젝트들도 함께 유지되도록 합니다. 부모(Player)의
    // 스케일이 (1,1,1)이 아니더라도 조준선의 실제 월드 크기가 항상 정확하도록, 매 프레임
    // 부모 스케일을 역산해서 localScale을 보정합니다(ApplySegmentTransform 참고).
    void BuildLineObjects()
    {
        quadMesh = BuildQuadMesh();
        runtimeMaterial = BuildMaterial();

        segmentsPerDirection = 1 + Mathf.Max(maxPreviewBounces, 0);

        // [137번] 19번 2총구 x 137번 3갈래 = 최대 6방향을 미리 만들어 둡니다(평소엔 방향 0만 켜짐).
        int directionCount = SpreadShotOverride.MaxShotCount;
        directionSegments = new LineSegment[directionCount][];

        var rendererList = new System.Collections.Generic.List<Renderer>();

        for (int d = 0; d < directionCount; d++)
        {
            directionSegments[d] = new LineSegment[segmentsPerDirection];

            for (int s = 0; s < segmentsPerDirection; s++)
            {
                GameObject lineObj = new GameObject("AimTrajectoryLine_" + d + "_" + s);
                Transform lineTransform = lineObj.transform;
                lineTransform.SetParent(transform, worldPositionStays: false);

                MeshFilter meshFilter = lineObj.AddComponent<MeshFilter>();
                meshFilter.sharedMesh = quadMesh;

                MeshRenderer meshRenderer = lineObj.AddComponent<MeshRenderer>();
                meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                meshRenderer.receiveShadows = false;
                meshRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                meshRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                meshRenderer.sharedMaterial = runtimeMaterial;
                meshRenderer.enabled = false;

                directionSegments[d][s] = new LineSegment { transform = lineTransform, renderer = meshRenderer };
                rendererList.Add(meshRenderer);
            }
        }

        LineRenderers = rendererList.ToArray();
    }

    void Update()
    {
        bool isAiming = aimAndAttack != null && aimAndAttack.IsAiming;

        // [무기 모드] 수류탄/해킹툴 모드 중에는 총 조준선 대신 각자의 표시(GrenadeAimIndicator/
        // HackRangeIndicator)가 보이므로, 우클릭 중이어도 총 조준선은 숨깁니다.
        if (weaponMode == null) weaponMode = GetComponent<PlayerWeaponMode>();
        if (weaponMode != null && !weaponMode.IsGun) isAiming = false;

        // 아이템 획득 연출(PlayerItemPickupPresenter) 중에는 사격할 수 없으므로 우클릭 중이어도 조준선을 숨깁니다.
        if (PlayerItemPickupPresenter.BlocksActions) isAiming = false;

        if (!isAiming)
        {
            for (int d = 0; d < directionSegments.Length; d++) HideDirection(d);
            return;
        }

        // [110번] 충전 중이면 늘어난 사거리/총알 크기/관통 여부를 이번 프레임 조준선에 반영합니다.
        if (chargeShot == null) chargeShot = GetComponent<PlayerChargeShot>();
        if (chargeShot == null || !chargeShot.TryGetAimPreview(out chargeRangeBonus, out chargeSizeBonus, out chargePiercing))
        {
            chargeRangeBonus = 0f;
            chargeSizeBonus = 0f;
            chargePiercing = false;
        }

        if (runtimeMaterial.color != lineColor)
        {
            runtimeMaterial.color = lineColor;
        }

        // [19번 패시브 - 양갈래 조준선] 보유 중이 아니면 기존과 동일하게 방향 0 하나만 중앙
        // 방향(각도 0)으로 켭니다. 보유 중이면 방향 0/1을 각각 -HalfAngle/+HalfAngle로 켜고,
        // 정중앙으로는 더 이상 총알이 나가지 않으므로 중앙선은 보여주지 않습니다.
        // [137번 확산 추가] 조준선은 137번의 퍼짐 줄은 그리지 않고 총구 방향만 보여줍니다(형빈 확정 - 줄이 너무 많아 복잡함).
        // SpreadShotOverride.FillAimAngles: 137번만이면 중앙 한 줄, 19번이 있으면(137번 유무와 상관없이) 위 설명 그대로 ±HalfAngle 두 줄.
        // 실제 발사(PlayerShooting.FireBurst - FillShotAngles)는 그대로 3발/6발입니다.
        SpreadShotOverride.FillAimAngles(shotAngles);
        for (int d = 0; d < directionSegments.Length; d++)
        {
            if (d < shotAngles.Count) UpdateDirectionChain(d, shotAngles[d]);
            else HideDirection(d);
        }
    }

    void HideDirection(int dirIndex)
    {
        LineSegment[] segments = directionSegments[dirIndex];
        for (int i = 0; i < segments.Length; i++)
        {
            if (segments[i].renderer.enabled) segments[i].renderer.enabled = false;
        }
    }

    // 방향 하나(중앙 또는 좌/우 갈래)를 처음부터 끝까지 계산합니다. angleOffsetDeg는
    // PlayerShooting.TryFire()에 넘어가는 기준 방향(transform.forward)에서 Y축으로 얼마나
    // 더 돌아간 방향에서 이 방향의 첫 조각을 시작할지를 뜻합니다(0 = 중앙, 19번 보유 중이면
    // ±SplitShotOverride.HalfAngle). 첫 조각이 벽에 막히고 35번(WallBounceOverride)을 보유
    // 중이면, 실제 Projectile이 튕길 때와 동일하게 그 지점부터 반사 방향으로, 사거리를 다시
    // 꽉 채워서 다음 조각을 잇습니다 - maxPreviewBounces만큼 이어붙인 뒤 멈춥니다.
    void UpdateDirectionChain(int dirIndex, float angleOffsetDeg)
    {
        LineSegment[] segments = directionSegments[dirIndex];

        Vector3 origin = (shooting != null && shooting.firePoint != null)
            ? shooting.firePoint.position
            : transform.position;

        Vector3 direction = ComputeBaseDirection(angleOffsetDeg);
        float maxRange = CurrentRange;
        // [110번] 관통 총알은 튕기지 않으므로 35번 미리보기도 하지 않습니다. [120번] 벽 관통도 마찬가지입니다.
        bool canBounce = WallBounceOverride.IsActive && !chargePiercing && !WallPierceOverride.IsActive;

        int segmentIndex = 0;
        while (segmentIndex < segments.Length)
        {
            SegmentHit hit = ComputeSegment(origin, direction, maxRange);

            ApplySegmentTransform(segments[segmentIndex], origin, direction, hit.length);
            segments[segmentIndex].renderer.enabled = true;
            segmentIndex++;

            bool canContinue = hit.hitWall && canBounce && segmentIndex < segments.Length;
            if (!canContinue) break;

            // 실제 Projectile.Redirect(newDirection, resetDistanceTraveled: true,
            // rangeMultiplierOnReset: WallBounceEffect.RangeMultiplierPerBounce)와 동일한
            // 계산입니다 - 튕긴 지점을 새 시작점, 반사 방향을 새 방향으로 삼고, 사거리는
            // 튕길 때마다 WallBounceEffect.RangeMultiplierPerBounce(75%)만큼 줄어듭니다
            // (Item35effect.cs 참고 - v1.45 변경, 무한 튕김 방지).
            maxRange *= WallBounceEffect.RangeMultiplierPerBounce;
            origin = origin + direction * hit.length;

            // [v1.43 수정 - 조준선이 Y축으로 비스듬히 꺾이는 버그, 형빈 확인 요청]
            // hit.normal이 항상 완전히 수평이라는 보장이 없어(모서리 근처 등) Y 성분이
            // 섞여 나올 수 있습니다. 실제 총알 쪽(Item35effect.cs)과 똑같이, 법선의 Y를
            // 지우고 정규화한 뒤에 반사시켜서 조준선도 항상 수평으로만 꺾이게 만듭니다.
            Vector3 flatNormal = hit.normal;
            flatNormal.y = 0f;
            if (flatNormal.sqrMagnitude < 0.0001f) flatNormal = -direction;
            else flatNormal.Normalize();

            direction = Vector3.Reflect(direction, flatNormal).normalized;
            origin += direction * BounceStartNudge;
        }

        for (int i = segmentIndex; i < segments.Length; i++)
        {
            if (segments[i].renderer.enabled) segments[i].renderer.enabled = false;
        }
    }

    // transform.forward를 수평면(Y=0)으로 눕힌 뒤, angleOffsetDeg만큼 Y축으로 회전시킨
    // 방향을 돌려줍니다. PlayerShooting.FireBurst가 SplitShotOverride.HalfAngle로 총알을
    // 돌리는 것과 동일한 기준(transform.forward)에서 계산합니다.
    Vector3 ComputeBaseDirection(float angleOffsetDeg)
    {
        Vector3 baseDirection = transform.forward;
        baseDirection.y = 0f;
        if (baseDirection.sqrMagnitude < 0.0001f) baseDirection = transform.forward;
        baseDirection.Normalize();

        if (Mathf.Approximately(angleOffsetDeg, 0f)) return baseDirection;

        return (Quaternion.Euler(0f, angleOffsetDeg, 0f) * baseDirection).normalized;
    }

    // 한 조각의 위치/회전/크기를 갱신합니다. origin/direction/length는 이미 계산되어
    // 넘어옵니다 - 첫 조각이면 발사 지점 기준, 튕긴 뒤 조각이면 그 튕긴 지점 기준입니다.
    void ApplySegmentTransform(LineSegment segment, Vector3 origin, Vector3 direction, float length)
    {
        float width = Mathf.Max(CurrentBulletSize * widthMultiplier, minWidth);

        Vector3 groundPosition = new Vector3(origin.x, transform.position.y + groundOffset, origin.z);

        segment.transform.position = groundPosition;
        segment.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);

        // 부모(Player) 스케일이 1이 아니어도 조준선의 실제 월드 크기가 (width, 1, length)가
        // 되도록, 부모의 lossyScale을 역산해 보정합니다.
        Vector3 parentScale = transform.lossyScale;
        segment.transform.localScale = new Vector3(
            SafeDivide(width, parentScale.x),
            SafeDivide(1f, parentScale.y),
            SafeDivide(length, parentScale.z)
        );
    }

    static float SafeDivide(float value, float divisor)
    {
        return Mathf.Approximately(divisor, 0f) ? value : value / divisor;
    }

    // 한 조각(segment)의 SphereCast 결과입니다. hitWall이 true면 normal이 그 벽의 표면
    // 법선이라 Vector3.Reflect로 다음 조각의 반사 방향을 계산할 수 있습니다.
    private struct SegmentHit
    {
        public float length;
        public bool hitWall;
        public Vector3 normal;
    }

    // origin에서 direction 방향으로, 실제 총알 크기(BulletSize)를 반지름으로 하는
    // SphereCast를 maxSegmentRange까지 쏴서 wallTag가 붙은 콜라이더 중 가장 가까운 지점까지의
    // 거리와 그 표면 법선을 반환합니다. Wall이 아닌 것(플레이어 자신, 적, 소품 등)은 전부
    // 무시하고 통과시켜서, 실제 Projectile.cs가 벽을 멈추는 조건과 일치시킵니다. 화면 표시용
    // Width Multiplier가 아니라 항상 진짜 BulletSize를 기준으로 계산합니다 - 화면에 보이는
    // 두께(연출용)와 실제 판정(정확도용)을 분리하기 위해서입니다.
    SegmentHit ComputeSegment(Vector3 origin, Vector3 direction, float maxSegmentRange)
    {
        // [110번 관통 총알 / 120번 벽 관통] 벽에서 끊기지 않고 그리되, 실제 총알처럼 방 테두리를 넘는 곳에서 끊습니다(형빈 확정).
        if (chargePiercing || WallPierceOverride.IsActive)
        {
            float inside = WallPierceOverride.DistanceInsideRoom(origin, direction, Mathf.Max(maxSegmentRange, 0f));
            return new SegmentHit { length = inside, hitWall = false, normal = Vector3.zero };
        }

        if (!stopAtWalls || maxSegmentRange <= 0f)
        {
            return new SegmentHit { length = maxSegmentRange, hitWall = false, normal = Vector3.zero };
        }

        float bulletRadius = Mathf.Max(CurrentBulletSize * 0.5f, 0.001f);

        // [v1.45 되돌림 - v1.44에서 넣었던 backoff를 여기서는 뺐습니다, 형빈 확인 요청]
        // 튕긴 다음 조각은 시작점이 방금 튕긴 벽 바로 옆(0.05f)뿐이라, 반지름만큼 뒤로
        // 물리면 오히려 캐스트 시작점이 벽 반대쪽(벽 속)으로 넘어가버려서 그 벽을 아예 못
        // 찾는 문제가 있었습니다(그래서 튕긴 뒤의 조준선이 안 보였습니다). 여기(미리보기)는
        // 실제 총알(Projectile.ComputeWallNormal)과 달리 애초에 각도 자체는 정상이었고
        // Y축으로 뜨는 것만 문제였는데 그건 UpdateDirectionChain에서 법선을 평평하게
        // 만드는 걸로 이미 해결했으므로, 여기서는 원래대로 origin에서 바로 캐스트합니다.
        int hitCount = Physics.SphereCastNonAlloc(
            origin, bulletRadius, direction, hitBuffer, maxSegmentRange,
            Physics.AllLayers, QueryTriggerInteraction.Ignore);

        if (hitCount <= 0)
        {
            return new SegmentHit { length = maxSegmentRange, hitWall = false, normal = Vector3.zero };
        }

        float closest = maxSegmentRange;
        Vector3 closestNormal = Vector3.zero;
        bool found = false;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = hitBuffer[i];

            if (hit.transform.IsChildOf(transform)) continue; // 플레이어 자기 자신은 무시
            if (!hit.collider.CompareTag(wallTag)) continue;  // Wall이 아니면 무시

            if (hit.distance < closest)
            {
                closest = hit.distance;
                closestNormal = hit.normal;
                found = true;
            }
        }

        return new SegmentHit { length = closest, hitWall = found, normal = closestNormal };
    }

    // 선의 좌표계 정의: Z=0이 시작 지점(원점), Z=1이 그 조각의 끝. X는 -0.5~0.5로 폭 1인
    // 사각 형태입니다. 실제 크기는 각 조각의 localScale(폭, 1, 길이)로 조절합니다.
    Mesh BuildQuadMesh()
    {
        Mesh mesh = new Mesh();
        mesh.name = "AimTrajectoryQuad";

        Vector3[] vertices =
        {
            new Vector3(-0.5f, 0f, 0f),
            new Vector3(0.5f, 0f, 0f),
            new Vector3(-0.5f, 0f, 1f),
            new Vector3(0.5f, 0f, 1f),
        };

        Vector2[] uvs =
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
        };

        int[] triangles = { 0, 2, 1, 2, 3, 1 };

        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }

    // URP 프로젝트 기준으로 안전한 반투명 머티리얼을 런타임에 만듭니다. 혹시 다른 렌더
    // 파이프라인이어도 안전하도록 셰이더를 순서대로 찾습니다(EnemyRangedAttack.cs의
    // 관례와 동일).
    Material BuildMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
            ?? Shader.Find("Unlit/Transparent")
            ?? Shader.Find("Sprites/Default")
            ?? Shader.Find("Standard");

        if (shader == null)
        {
            Debug.LogWarning("AimTrajectoryIndicator: 사용 가능한 셰이더를 찾지 못했습니다.");
            shader = Shader.Find("Diffuse");
        }

        Material mat = new Material(shader);
        mat.name = "AimTrajectoryIndicator_Material";
        ApplyTransparentSettings(mat);
        mat.color = lineColor;
        return mat;
    }

    void ApplyTransparentSettings(Material mat)
    {
        if (mat.HasProperty("_Surface"))
        {
            mat.SetFloat("_Surface", 1f); // URP Unlit/Lit: 0=Opaque, 1=Transparent
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        }

        if (mat.HasProperty("_Cull"))
        {
            // 위/아래 어느 방향(각도가 심한 카메라 등)에서도 항상 보이도록 양면 렌더링.
            mat.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
        }

        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }

    void OnDestroy()
    {
        if (runtimeMaterial != null) Destroy(runtimeMaterial);
        if (quadMesh != null) Destroy(quadMesh);
    }
}
