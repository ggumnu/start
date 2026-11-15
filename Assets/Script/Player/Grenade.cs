using System.Collections.Generic;
using UnityEngine;

// 날아가는 수류탄 한 개입니다. PlayerGrenadeThrower가 던질 때 자동으로 붙이므로(수류탄 프리팹에
// 미리 붙여둘 필요 없음), 에디터에서 따로 설정할 것은 없습니다.
//
// [비행]
// 물리 엔진을 쓰지 않고 EvaluateArc(포물선 공식)로 매 프레임 위치를 계산합니다. 조준선
// (GrenadeAimIndicator)도 같은 함수로 그리므로 보이는 궤적과 실제 궤적이 항상 같습니다.
// 비행 시간(flightTime)이 지나 착지점에 닿는 순간 바로 폭발합니다. 벽은 넘어서 날아갑니다.
//
// [폭발 판정]
// - 적: 바닥 원과 같은 반경의 원기둥(높이 explosionHeight) 안에 콜라이더가 걸친 적(enemyTag)에게
//   데미지를 줍니다. 콜라이더가 여러 개인 적도 한 번만 맞도록 EnemyHealth 단위로 중복을 걸러내고,
//   이미 죽은 적은 건너뜁니다. 피해 숫자 팝업/체력바/처치 연출은 EnemyHealth.TakeDamage를 거치므로
//   자동으로 동작합니다.
// - 플레이어: 폭발 중심과 플레이어 발 위치의 수평 거리가 반경 이내면 자해 데미지를 받습니다
//   (적 데미지와 별개 값). PlayerHealth.TakeDamage를 거치므로 무적 시간/피격 점멸이 그대로
//   적용됩니다. 자해 데미지가 0이면 아무 일도 없습니다.
//   단, 수류탄 자해 무효 아이템(57번 등 - GrenadeSelfDamageOverride)을 보유 중이면 피해 처리 자체를 건너뜁니다.
//
// [착탄 지점 표시]
// 던진 순간부터 터지기 직전까지 착지점에 폭발 반경 원(반투명 안쪽 + 흰 테두리)을 띄워서, 플레이어가
// 어디에 떨어질지 보고 피할 수 있게 합니다. 색/두께/표시 여부는 PlayerGrenadeThrower 인스펙터의
// "착탄 지점 표시" 항목에서 조절합니다. 원은 플레이어의 자식이 아닌 별도 오브젝트라 플레이어가
// 움직여도 착지점에 그대로 고정되고, 폭발(또는 수류탄이 어떤 이유로 사라질 때) 함께 지워집니다.
// (날아가는 동안 원이 수류탄을 따라 움직이던 방식은 착시가 생겨 되돌렸습니다 - 형빈 확정. 77번 유도 중에는 지금처럼 따라다닙니다.)
//
// [77번 - 착지 후 유도 단계]
// 착지하는 순간 GrenadeHomingOverride에 등록이 있으면(77번 보유) 바로 터지지 않고 유도 단계로 넘어갑니다.
// - 설정된 시간(기본 1초) 동안 바닥(착지점 높이 + 띄움 높이)에 붙어, 매 순간 가장 가까운 살아 있는 적(보스 포함)에게
//   플레이어의 현재 이동속도로 다가갑니다. 적이 없으면 제자리에 있다가 적이 나타나면 그쪽으로 갑니다. 적에게 닿아도
//   시간이 다 될 때까지 따라붙습니다.
// - "Wall" 태그 콜라이더에 막히고, 막히면 벽을 따라 미끄러지며 계속 쫓습니다. 굴러가는 방향으로 회전합니다.
// - 착탄 지점 원은 유도 중에도 수류탄을 따라다닙니다.
// - 시간이 다 되면 "지금 수류탄이 있는 곳"에서 터집니다. 적 피해/자해 판정/폭발 이펙트/OnGrenadeExploded(57번 토끼,
//   특수방 잠금 해제 등)가 모두 그 위치 기준입니다. 77번이 없으면 터지는 위치는 예전과 같은 착지점입니다.
//
// [81번 - 소이탄]
// 던지는 순간 IncendiaryGrenadeOverride에 등록이 있으면(81번 보유) 이 수류탄은 소이탄입니다. 주변에 빨간 발광을 붙이고,
// 터질 때 적 피해를 비율(기본 50%)로 줄이고 자해 피해를 고정값(기본 0.5)으로 바꾼 뒤, 폭발 처리(NotifyExploded)가 끝나면 그
// 자리에 불타는 구역(IncendiaryFireZone)을 남깁니다. 자세한 규칙은 Item81effect.cs 참고.
//
// [92번 - 분열 수류탄]
// 던지는 순간 GrenadeSplitOverride에 등록이 있으면(92번 보유) 이 수류탄은 터질 때 분열합니다. 폭발 처리(피해/NotifyExploded/81번
// 불 구역)가 모두 끝난 뒤, 터진 위치에서 "던진 방향" 기준 X자 4방향(앞 ±45도, 뒤 ±135도)으로 분열탄 4개를 날립니다
// (PlayerGrenadeThrower.SpawnSplitGrenade). 분열탄은 피해/반경/자해가 원래 값의 비율(기본 50%)이고, 원래 수류탄의 소이탄 여부를
// 그대로 물려받으며, 다시 분열하지 않습니다. 착지 후 77번 유도/57번/66번/특수방 잠금 해제는 일반 수류탄과 같은 규칙으로 적용됩니다.
// 자세한 규칙은 Item92effect.cs 참고.
//
// [115번 - 크레모어]
// 던지는 순간 ClaymoreOverride에 등록이 있으면(115번 보유) 이 수류탄은 크레모어입니다. 날아가는 것/스탯은 그대로이고, 착지하면 터지지 않고
// 설치됩니다(착탄 원은 계속 보이고, 빨간 점멸 ClaymoreGlow를 붙임). 수류탄 모드 R(ClaymoreDetonator)이나 115번을 잃을 때 DetonateClaymore로
// 터집니다. 던질 때 플레이어가 있던 방을 기억하고(ClaymoreRoom), 플레이어가 다른 방에 있으면 숨기며, 다음 레벨로 넘어가 그 방이 사라지면
// 조용히 없어집니다.
// - 날아가는 도중에는 터지지 않습니다(형빈 확정): R을 눌러도 착지해서 설치된 크레모어만 터집니다(DetonateClaymore가 설치 전이면 false).
// - 115번을 잃을 때 아직 날아가던 크레모어는 일반 수류탄으로 돌아가(RevertToNormalGrenade) 착지하면 평소처럼 터집니다(77번/92번도 일반 규칙).
// - 77번: 설치하면 유도를 시작하고, 터뜨릴 때까지 시간 제한 없이 유도합니다. 설치된 방의 적만 쫓습니다.
// - 92번: 설치하는 순간 그 자리에서 분열 크레모어 4개를 날립니다(분열 크레모어는 원래 것의 방/크레모어 설정을 물려받고 다시 분열 X).
//   자세한 규칙은 Item115effect.cs 참고.
//
// [방 전환 - 그 방에서 생긴 일은 그 방에서만]
// 던지는 순간 플레이어가 있던 방을 기억합니다(ownerRoom - RoomScope 기준. 크레모어/분열 크레모어는 크레모어의 방).
// - 플레이어가 다른 방에 있는 동안은 수류탄/착탄 원/소이탄 발광을 숨기고 꼬리는 바로 지웁니다. 날아가기/유도/터지는 시간은 그대로
//   진행되고, 그 방에 돌아오면 다시 보입니다(크레모어는 원래의 크레모어 규칙 그대로 - 보이기/숨기기만 같은 함수를 씀).
// - 플레이어가 그 방에 없을 때 터지면 "조용한 폭발": 폭발 이펙트/적 피해/자해/66번 해킹/57번 토끼/81번 불 구역/92번 분열을 모두
//   건너뛰고, 그 방의 특수방 잠금 바리케이드 해제만 합니다(SpecialRoomLock.TryUnlockByGrenade). 그 방의 적은 꺼져 있어 원래도
//   맞지 않고, 지금 플레이어가 있는 방에는 어떤 영향도 주지 않습니다.
// - 플레이어가 있을 때 터지면 예전과 같고, 특수방 잠금 해제도 이 수류탄의 방 기준으로 판정합니다(NotifyExploded에 방을 넘김).
// - 77번 유도는 이 수류탄의 방에 있는 적만 쫓습니다(숨은 수류탄이 플레이어가 있는 방으로 굴러가지 않도록).
// - 다음 레벨로 넘어가 그 방이 사라졌으면 조용히 없앱니다.
// - 방을 판단할 수 없으면(테스트 씬 등) 예전과 똑같이 동작합니다.
public class Grenade : MonoBehaviour
{
    private const int MarkerSegments = 64;

    // 출발점 start에서 착지점 end까지의 포물선 위 한 점을 돌려줍니다. u는 0(출발)~1(착지)입니다.
    // 두 점을 잇는 직선 위의 점에, 한가운데(u=0.5)에서 높이 arcHeight가 되는 포물선 높이를 더합니다.
    public static Vector3 EvaluateArc(Vector3 start, Vector3 end, float arcHeight, float u)
    {
        u = Mathf.Clamp01(u);
        Vector3 point = Vector3.Lerp(start, end, u);
        point.y += 4f * arcHeight * u * (1f - u);
        return point;
    }

    private PlayerGrenadeThrower owner;
    private Vector3 start;
    private Vector3 end;
    private float flightTime;
    private float arcHeight;
    private float enemyDamage;
    private float selfDamage;
    private float radius;
    private float explosionHeight;
    private string enemyTag;
    private float spinDegreesPerSecond;
    private Vector3 spinAxis;

    private float elapsed;
    private bool launched;
    private bool exploded;

    // [81번 소이탄] 던지는 순간 정해집니다(발광/폭발 피해 감소/불타는 구역이 한 세트). null이면 일반 수류탄입니다.
    private IncendiaryGrenadeOverride.Settings incendiarySettings;

    // [92번 분열탄] 던지는 순간 정해집니다. null이면 터질 때 분열하지 않습니다(분열탄 자신은 항상 null).
    private GrenadeSplitOverride.Settings splitSettings;
    private Vector3 throwDirection;   // 던진 방향(수평 단위 벡터) - 분열 4방향의 기준
    private bool isSplitChild;        // 92번으로 생긴 분열탄이면 true(다시 분열하지 않음)
    private IncendiaryGrenadeOverride.Settings inheritedIncendiarySettings; // 분열탄이 물려받는 소이탄 설정

    // [115번 크레모어] 던지는 순간 정해집니다. null이면 일반 수류탄입니다.
    private const float ClaymoreRoomCheckInterval = 0.5f;
    private ClaymoreOverride.Settings claymoreSettings;
    private ClaymoreOverride.Settings inheritedClaymoreSettings; // 분열 크레모어가 물려받는 크레모어 설정
    private RoomInstance claymoreRoom;     // 던질 때 플레이어가 있던 방(R은 이 방에 있을 때만 이 크레모어를 터뜨림)
    private bool claymoreInstalled;        // 착지해서 설치됨
    private bool claymoreVisible = true;
    private float claymoreRoomCheckTimer;
    private ClaymoreGlow claymoreGlow;

    // [방 전환] 던지는 순간 플레이어가 있던 방(null이면 방을 판단할 수 없음 = 예전처럼 동작)과 지금 보이는지 여부입니다.
    private RoomInstance ownerRoom;
    private bool roomVisible = true;

    // [77번] 착지 후 유도 단계 상태입니다.
    private const float WallSkin = 0.01f;
    private const float TargetSearchInterval = 0.1f; // 가장 가까운 적을 다시 찾는 간격(초)
    private static readonly RaycastHit[] wallHitBuffer = new RaycastHit[16];
    private bool homing;
    private float homingElapsed;
    private GrenadeHomingOverride.Settings homingSettings;
    private EnemyHealth homingTarget;
    private float targetSearchTimer;

    // 착탄 지점 표시용 오브젝트와, 이 수류탄 전용으로 만든 메시/머티리얼(폭발 시 함께 정리)
    private GameObject markerRoot;
    private Mesh markerDiscMesh;
    private Material markerFillMaterial;
    private Material markerOutlineMaterial;

    public Vector3 LandingPoint => end;
    public float Radius => radius;
    public bool IsSplitChild => isSplitChild;
    public bool IsClaymore => claymoreSettings != null;
    public RoomInstance ClaymoreRoom => claymoreRoom;

    // [92번 분열탄] Launch 전에 불러서 이 수류탄을 분열탄으로 표시합니다. 분열탄은 다시 분열하지 않고, 소이탄 여부는 등록소를 다시
    // 확인하지 않고 원래 수류탄의 값(inheritedIncendiary - null이면 일반)을 그대로 씁니다.
    public void PrepareAsSplitChild(IncendiaryGrenadeOverride.Settings inheritedIncendiary)
    {
        isSplitChild = true;
        inheritedIncendiarySettings = inheritedIncendiary;
    }

    // [115번 크레모어] 분열 크레모어로 만들 때 Launch 전에 부릅니다(PrepareAsSplitChild 뒤). 원래 크레모어의 설정과 방을 물려받습니다.
    public void PrepareAsClaymoreChild(ClaymoreOverride.Settings inheritedClaymore, RoomInstance room)
    {
        inheritedClaymoreSettings = inheritedClaymore;
        claymoreRoom = room;
    }

    public void Launch(PlayerGrenadeThrower owner, Vector3 start, Vector3 end, float flightTime, float arcHeight,
                       float enemyDamage, float selfDamage, float radius, float explosionHeight,
                       string enemyTag, float spinDegreesPerSecond)
    {
        this.owner = owner;
        this.start = start;
        this.end = end;
        this.flightTime = Mathf.Max(flightTime, 0.01f);
        this.arcHeight = arcHeight;
        this.enemyDamage = enemyDamage;
        this.selfDamage = selfDamage;
        this.radius = radius;
        this.explosionHeight = explosionHeight;
        this.enemyTag = enemyTag;
        this.spinDegreesPerSecond = spinDegreesPerSecond;

        Vector3 flatDir = end - start;
        flatDir.y = 0f;
        spinAxis = flatDir.sqrMagnitude > 0.0001f ? Vector3.Cross(Vector3.up, flatDir.normalized) : Vector3.right;

        // [92번] 던진 방향을 기억합니다(분열 4방향의 기준). 발밑에 던져 방향이 없으면 플레이어가 바라보는 방향을 씁니다.
        throwDirection = flatDir.sqrMagnitude > 0.0001f ? flatDir.normalized : FallbackForward(owner);

        // [92번] 던지는 순간 92번 보유 중이면 이 수류탄은 터질 때 분열합니다. 분열탄 자신은 확인하지 않습니다(다시 분열 X).
        splitSettings = (!isSplitChild && GrenadeSplitOverride.TryGetActive(out var split)) ? split : null;

        // [115번] 던지는 순간 115번 보유 중이면 크레모어입니다(착지하면 설치). 분열 크레모어는 원래 크레모어의 설정/방을 물려받습니다.
        if (isSplitChild)
        {
            claymoreSettings = inheritedClaymoreSettings;
        }
        else
        {
            claymoreSettings = ClaymoreOverride.TryGetActive(out var claymore) ? claymore : null;
            claymoreRoom = claymoreSettings != null ? ClaymoreOverride.CurrentPlayerRoom() : null;
        }
        if (claymoreSettings != null) ClaymoreOverride.Track(this);

        // [방 전환] 이 수류탄의 방입니다. 크레모어는 크레모어의 방(분열 크레모어는 플레이어가 다른 방에 있을 때 설치되며 생길 수 있어
        // 물려받은 방을 써야 함), 그 밖에는 지금 플레이어가 있는 방입니다.
        ownerRoom = (claymoreSettings != null && claymoreRoom != null) ? claymoreRoom : RoomScope.CurrentPlayerRoom();
        roomVisible = true;

        elapsed = 0f;
        launched = true;
        transform.position = start;

        // [탄환 꼬리] 던지는 위치가 정해진 뒤 꼬리를 붙입니다(기본 흰색 - Resources/BulletTrailSettings의 수류탄 템플릿).
        // 수류탄이 터져 사라지면 꼬리는 스스로 사라집니다(BulletTrail.cs 참고).
        BulletTrail.Attach(gameObject, BulletTrailKind.Grenade);

        // [81번 소이탄] 던지는 순간 보유 중이면 이 수류탄은 소이탄입니다 - 수류탄 주변 빨간 발광을 붙입니다(Item81effect.cs 참고).
        // [92번 분열탄] 분열탄은 원래 수류탄이 던져진 순간 정해진 소이탄 여부를 그대로 물려받습니다(PrepareAsSplitChild).
        if (isSplitChild) incendiarySettings = inheritedIncendiarySettings;
        else incendiarySettings = IncendiaryGrenadeOverride.TryGetActive(out var incendiary) ? incendiary : null;
        if (incendiarySettings != null) IncendiaryGrenadeGlow.Attach(gameObject, incendiarySettings);

        if (owner != null && owner.showLandingMarker && radius > 0f)
        {
            CreateLandingMarker();
        }
    }

    void CreateLandingMarker()
    {
        Vector3 center = end + Vector3.up * owner.landingMarkerGroundOffset;

        markerRoot = new GameObject("GrenadeLandingMarker");
        markerRoot.transform.position = center;
        markerRoot.transform.rotation = Quaternion.identity;

        // 안쪽(꽉 찬 반투명 원) - 조준 때의 폭발 반경 원과 같은 메시/머티리얼 방식
        if (owner.landingMarkerFillColor.a > 0f)
        {
            markerDiscMesh = GrenadeAimIndicator.BuildUnitDiscMesh(MarkerSegments);
            markerFillMaterial = GrenadeAimIndicator.BuildMaterial("GrenadeLandingFill_Material",
                                                                   owner.landingMarkerFillColor);

            GameObject fill = new GameObject("Fill");
            fill.transform.SetParent(markerRoot.transform, worldPositionStays: false);
            fill.transform.localScale = new Vector3(radius, 1f, radius);
            fill.AddComponent<MeshFilter>().sharedMesh = markerDiscMesh;
            MeshRenderer fillRenderer = fill.AddComponent<MeshRenderer>();
            ConfigureMarkerRenderer(fillRenderer, markerFillMaterial);
        }

        // 테두리(흰 원)
        if (owner.landingMarkerOutlineColor.a > 0f)
        {
            markerOutlineMaterial = GrenadeAimIndicator.BuildMaterial("GrenadeLandingOutline_Material",
                                                                      owner.landingMarkerOutlineColor);

            GameObject outline = new GameObject("Outline");
            outline.transform.SetParent(markerRoot.transform, worldPositionStays: false);
            LineRenderer line = outline.AddComponent<LineRenderer>();
            // [77번] 유도 중 원이 수류탄을 따라 움직일 수 있도록 markerRoot 기준(로컬) 좌표로 그립니다. 움직이지 않을 때의
            // 모양/위치는 예전(월드 좌표)과 같습니다.
            line.useWorldSpace = false;
            line.loop = true;
            line.alignment = LineAlignment.View;
            line.numCornerVertices = 2;
            line.startWidth = owner.landingMarkerOutlineWidth;
            line.endWidth = owner.landingMarkerOutlineWidth;
            line.positionCount = MarkerSegments;
            for (int i = 0; i < MarkerSegments; i++)
            {
                float angle = (float)i / MarkerSegments * Mathf.PI * 2f;
                line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
            }
            ConfigureMarkerRenderer(line, markerOutlineMaterial);
        }
    }

    static void ConfigureMarkerRenderer(Renderer rend, Material mat)
    {
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rend.receiveShadows = false;
        rend.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        rend.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        rend.sharedMaterial = mat;
    }

    void DestroyLandingMarker()
    {
        if (markerRoot != null) Destroy(markerRoot);
        if (markerDiscMesh != null) Destroy(markerDiscMesh);
        if (markerFillMaterial != null) Destroy(markerFillMaterial);
        if (markerOutlineMaterial != null) Destroy(markerOutlineMaterial);
        markerRoot = null;
        markerDiscMesh = null;
        markerFillMaterial = null;
        markerOutlineMaterial = null;
    }

    // 폭발 외의 이유(씬 전환 등)로 수류탄이 사라져도 표시가 남지 않도록 합니다.
    void OnDestroy()
    {
        DestroyLandingMarker();
        ClaymoreOverride.Untrack(this); // [115번] 크레모어 목록에서 빠짐(크레모어가 아니면 아무 일도 없음)
    }

    void Update()
    {
        if (!launched || exploded) return;

        // [115번] 플레이어가 다른 방에 있으면 숨기고, 다음 레벨로 넘어가 방이 사라졌으면 조용히 없앱니다(없앴으면 true).
        if (claymoreSettings != null && UpdateClaymoreRoomState()) return;
        // [방 전환] 크레모어가 아닌 수류탄도 같은 방식으로 숨기고, 방이 사라졌으면 조용히 없앱니다(없앴으면 true).
        if (claymoreSettings == null && UpdateOwnerRoomState()) return;

        // [77번] 착지 후 유도 단계
        if (homing)
        {
            UpdateHoming();
            return;
        }

        // [115번] 설치된 크레모어는 터뜨릴 때까지 그 자리에서 기다립니다.
        if (claymoreInstalled) return;

        elapsed += Time.deltaTime;
        float u = elapsed / flightTime;

        transform.position = EvaluateArc(start, end, arcHeight, u);
        if (spinDegreesPerSecond != 0f)
        {
            transform.Rotate(spinAxis, spinDegreesPerSecond * Time.deltaTime, Space.World);
        }

        if (u >= 1f)
        {
            // [115번] 크레모어는 착지하면 터지지 않고 설치됩니다(77번 유도/92번 분열도 설치에서 처리).
            if (claymoreSettings != null)
            {
                InstallClaymore();
                return;
            }

            // [77번] 착지 순간 유도 아이템을 보유 중이면 바로 터지지 않고 유도 단계로 넘어갑니다.
            if (GrenadeHomingOverride.TryGetActive(out var settings) && settings.homingDuration > 0f)
            {
                BeginHoming(settings);
                return;
            }

            Explode();
        }
    }

    // [77번] 유도 단계를 시작합니다. 수류탄을 착지점 바닥 높이(+ 띄움 높이)에 놓습니다.
    void BeginHoming(GrenadeHomingOverride.Settings settings)
    {
        homing = true;
        homingSettings = settings;
        homingElapsed = 0f;
        homingTarget = null;
        targetSearchTimer = 0f;

        Vector3 pos = end;
        pos.y = end.y + settings.groundOffset;
        transform.position = pos;
        MoveLandingMarker();
    }

    void UpdateHoming()
    {
        homingElapsed += Time.deltaTime;

        // 가장 가까운 적을 일정 간격으로 다시 찾고, 쫓던 적이 죽었거나 사라졌으면 바로 다시 찾습니다.
        targetSearchTimer -= Time.deltaTime;
        if (targetSearchTimer <= 0f || homingTarget == null || homingTarget.IsDead || !homingTarget.isActiveAndEnabled)
        {
            homingTarget = FindNearestEnemy();
            targetSearchTimer = TargetSearchInterval;
        }

        if (homingTarget != null)
        {
            float speed = 0f;
            if (owner != null && owner.stats != null) speed = Mathf.Max(0f, owner.stats.MoveSpeed) * homingSettings.speedMultiplier;

            Vector3 toTarget = homingTarget.transform.position - transform.position;
            toTarget.y = 0f;
            float distance = toTarget.magnitude;
            float step = Mathf.Min(speed * Time.deltaTime, distance); // 적 위에 도착하면 그 자리에서 따라붙음

            if (step > 0.0001f)
            {
                Vector3 before = transform.position;
                MoveAlongGround(toTarget / distance, step);
                RollAlong(transform.position - before);
            }
        }

        MoveLandingMarker();

        // [115번] 크레모어는 시간 제한 없이 터뜨릴 때까지 유도합니다.
        if (claymoreSettings == null && homingElapsed >= homingSettings.homingDuration)
        {
            Explode();
        }
    }

    // 살아 있는(활성화된) 적 중 수평 거리가 가장 가까운 적입니다(보스 포함). 없으면 null.
    EnemyHealth FindNearestEnemy()
    {
        EnemyHealth best = null;
        float bestSqr = float.MaxValue;
        Vector3 pos = transform.position;

        var enemies = FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None);
        foreach (var enemy in enemies)
        {
            if (enemy == null || enemy.IsDead || !enemy.isActiveAndEnabled) continue;
            if (!string.IsNullOrEmpty(enemyTag) && !enemy.CompareTag(enemyTag)) continue;
            // [115번] 크레모어는 설치된 방의 적만 쫓습니다(없으면 제자리).
            if (claymoreSettings != null && claymoreRoom != null &&
                !ReferenceEquals(ClaymoreOverride.RoomOf(enemy.transform.position), claymoreRoom)) continue;
            // [방 전환] 일반 수류탄도 이 수류탄의 방에 있는 적만 쫓습니다(플레이어가 다른 방으로 간 뒤 그 방의 적에게 굴러가지 않도록).
            if (claymoreSettings == null && ownerRoom != null &&
                !ReferenceEquals(RoomScope.RoomOf(enemy.transform.position), ownerRoom)) continue;

            Vector3 d = enemy.transform.position - pos;
            d.y = 0f;
            float sqr = d.sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = enemy;
            }
        }
        return best;
    }

    // dir(수평 단위 벡터) 방향으로 distance만큼 바닥을 따라 움직입니다. "Wall" 태그에 막히면 벽 바로 앞까지만 가고,
    // 남은 거리는 벽면을 따라 미끄러지듯 움직입니다(한 번 더 막히면 거기서 멈춤).
    void MoveAlongGround(Vector3 dir, float distance)
    {
        Vector3 remaining = dir * distance;
        for (int iteration = 0; iteration < 2 && remaining.sqrMagnitude > 0.000001f; iteration++)
        {
            float length = remaining.magnitude;
            Vector3 moveDir = remaining / length;

            if (CastWall(moveDir, length, out float hitDistance, out Vector3 hitNormal))
            {
                float allowed = Mathf.Max(0f, hitDistance - WallSkin);
                transform.position += moveDir * allowed;

                // 남은 이동을 벽면 방향으로 돌립니다(벽을 따라 미끄러짐).
                Vector3 normal = new Vector3(hitNormal.x, 0f, hitNormal.z);
                if (normal.sqrMagnitude < 0.0001f) break;
                normal.Normalize();
                Vector3 left = moveDir * (length - allowed);
                remaining = left - Vector3.Dot(left, normal) * normal;
            }
            else
            {
                transform.position += remaining;
                break;
            }
        }
    }

    // 수류탄 크기의 구체로 진행 방향을 훑어 "Wall" 태그 콜라이더를 찾습니다. 시작부터 이미 겹친 콜라이더는 무시합니다.
    bool CastWall(Vector3 dir, float maxDistance, out float hitDistance, out Vector3 hitNormal)
    {
        hitDistance = maxDistance;
        hitNormal = Vector3.zero;

        float r = homingSettings.collisionRadius;
        // 바닥에 걸리지 않도록 바닥 높이보다 살짝 위에서 훑습니다.
        Vector3 origin = new Vector3(transform.position.x, end.y + r + 0.05f, transform.position.z);

        int count = Physics.SphereCastNonAlloc(origin, r, dir, wallHitBuffer, maxDistance + WallSkin,
                                               Physics.AllLayers, QueryTriggerInteraction.Ignore);
        bool found = false;
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = wallHitBuffer[i];
            if (hit.collider == null || !hit.collider.CompareTag("Wall")) continue;
            if (hit.distance <= 0f && hit.point == Vector3.zero) continue; // 시작부터 겹쳐 있던 콜라이더

            if (hit.distance < hitDistance)
            {
                hitDistance = hit.distance;
                hitNormal = hit.normal;
                found = true;
            }
        }
        return found;
    }

    // 움직인 만큼 굴러가는 방향으로 회전시킵니다.
    void RollAlong(Vector3 moved)
    {
        moved.y = 0f;
        float dist = moved.magnitude;
        if (dist < 0.0001f) return;

        Vector3 axis = Vector3.Cross(Vector3.up, moved / dist);
        float degrees = dist / Mathf.Max(homingSettings.collisionRadius, 0.001f) * Mathf.Rad2Deg;
        transform.Rotate(axis, degrees, Space.World);
    }

    // 착탄 지점 원을 지금 수류탄 위치(바닥 높이)로 옮깁니다.
    void MoveLandingMarker()
    {
        if (markerRoot == null || owner == null) return;
        Vector3 center = CurrentGroundPoint();
        center.y += owner.landingMarkerGroundOffset;
        markerRoot.transform.position = center;
    }

    // 지금 수류탄 아래의 바닥 점(착지점 높이)입니다. 유도 중이 아니면 착지점 그대로입니다.
    Vector3 CurrentGroundPoint()
    {
        if (!homing) return end;
        return new Vector3(transform.position.x, end.y, transform.position.z);
    }

    // ---------------- [115번 크레모어] ----------------

    // 착지한 크레모어를 설치합니다: 빨간 점멸을 붙이고, 92번이면 분열 크레모어 4개를 날리고, 77번이면 유도를 시작합니다.
    void InstallClaymore()
    {
        claymoreInstalled = true;
        transform.position = end;
        if (claymoreVisible) GameAudio.Play(SfxId.ClaymoreInstall); // [사운드] 플레이어가 있는 방에서만

        claymoreGlow = ClaymoreGlow.Attach(gameObject, claymoreSettings);
        if (claymoreGlow != null) claymoreGlow.SetVisible(claymoreVisible);

        // [92번] 설치하는 순간 그 자리에서 분열 크레모어를 날립니다. 이 크레모어가 나중에 터질 때는 다시 분열하지 않습니다.
        if (splitSettings != null && owner != null)
        {
            try
            {
                SpawnSplitGrenades(end);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Grenade: 분열 크레모어 생성 중 오류가 발생해 건너뜁니다(설치 자체에는 영향 없음): {e}");
            }
        }
        splitSettings = null;

        // [77번] 보유 중이면 설치한 자리에서 유도를 시작합니다(터뜨릴 때까지 계속 - UpdateHoming 참고).
        if (GrenadeHomingOverride.TryGetActive(out var homingSet)) BeginHoming(homingSet);
    }

    // 설치된 크레모어를 지금 터뜨립니다. 크레모어가 아니거나, 이미 터졌거나, 아직 날아가는 중(설치 전)이면 터지지 않고 false
    // (형빈 확정 - 공중 폭파 없음).
    public bool DetonateClaymore()
    {
        if (!launched || exploded || claymoreSettings == null) return false;
        if (!claymoreInstalled) return false;
        Explode();
        return true;
    }

    // [115번을 잃을 때] 아직 날아가는 중인(설치 전) 크레모어를 일반 수류탄으로 되돌립니다. 착지하면 평소처럼 터지고, 77번/92번도
    // 일반 수류탄 규칙을 따릅니다(분열탄도 일반 수류탄). 되돌렸으면 true.
    public bool RevertToNormalGrenade()
    {
        if (!launched || exploded || claymoreSettings == null || claymoreInstalled) return false;
        claymoreSettings = null;
        ClaymoreOverride.Untrack(this);
        // [방 전환] 이제부터는 일반 수류탄 규칙(UpdateOwnerRoomState)으로 보이기/숨기기를 맞춥니다. 지금 보이는 상태를 그대로 넘겨받습니다.
        roomVisible = claymoreVisible;
        return true;
    }

    // 다음 레벨로 넘어가 방이 사라졌으면 조용히 없애고 true. 아니면 플레이어가 있는 방인지에 따라 보이기/숨기기만 하고 false.
    bool UpdateClaymoreRoomState()
    {
        claymoreRoomCheckTimer -= Time.deltaTime;
        if (claymoreRoomCheckTimer <= 0f)
        {
            claymoreRoomCheckTimer = ClaymoreRoomCheckInterval;
            if (ClaymoreOverride.IsRoomGone(claymoreRoom))
            {
                exploded = true; // 더 이상 아무 처리도 하지 않음(폭발 없음)
                ClaymoreOverride.Untrack(this);
                Destroy(gameObject);
                return true;
            }
        }

        bool visible = claymoreRoom == null || ReferenceEquals(claymoreRoom, ClaymoreOverride.CurrentPlayerRoom());
        if (visible != claymoreVisible) SetClaymoreVisible(visible);
        return false;
    }

    void SetClaymoreVisible(bool visible)
    {
        claymoreVisible = visible;
        ApplyVisible(visible);
    }

    // [방 전환] 크레모어가 아닌 수류탄: 다음 레벨로 넘어가 방이 사라졌으면 조용히 없애고 true. 아니면 플레이어가 이 수류탄의 방에
    // 있는지에 따라 보이기/숨기기만 하고 false. 방을 판단할 수 없으면(ownerRoom이 null) 항상 보입니다.
    bool UpdateOwnerRoomState()
    {
        if (ownerRoom == null) return false;

        if (RoomScope.IsRoomGone(ownerRoom))
        {
            exploded = true; // 더 이상 아무 처리도 하지 않음(폭발 없음)
            Destroy(gameObject);
            return true;
        }

        bool visible = RoomScope.IsPlayerInRoom(ownerRoom);
        if (visible != roomVisible)
        {
            roomVisible = visible;
            ApplyVisible(visible);
        }
        return false;
    }

    // 수류탄 모델/착탄 원/크레모어 점멸/소이탄 발광을 보이거나 숨깁니다. 숨길 때는 꼬리도 바로 지웁니다(다시 보여도 꼬리는 새로
    // 생기지 않음 - 다른 방 위 허공에 꼬리가 그려지지 않도록).
    void ApplyVisible(bool visible)
    {
        if (!visible) BulletTrail.RemoveNow(gameObject);
        foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = visible;
        if (markerRoot != null) markerRoot.SetActive(visible);
        if (claymoreGlow != null) claymoreGlow.SetVisible(visible);
        var incendiaryGlow = GetComponent<IncendiaryGrenadeGlow>();
        if (incendiaryGlow != null) incendiaryGlow.SetVisible(visible);
    }

    void Explode()
    {
        exploded = true;
        ClaymoreOverride.Untrack(this); // [115번]
        // [77번] 유도했으면 지금 수류탄이 있는 바닥 위치에서, 아니면 예전처럼 착지점에서 터집니다.
        Vector3 center = CurrentGroundPoint();

        // 터지는 순간 착탄 지점 표시를 지웁니다(폭발 이펙트로 자연스럽게 넘어가도록).
        DestroyLandingMarker();

        // [방 전환] 플레이어가 이 수류탄의 방에 없으면 조용한 폭발: 그 방의 특수방 잠금 해제만 하고 나머지는 모두 건너뜁니다
        // (클래스 주석 [방 전환] 참고).
        if (ownerRoom != null && !RoomScope.IsPlayerInRoom(ownerRoom))
        {
            SpecialRoomLock.TryUnlockByGrenade(center, radius, ownerRoom);
            Destroy(gameObject);
            return;
        }

        // [81번 소이탄] 폭발 피해는 적 = 수류탄 피해 x 비율(기본 50%), 플레이어 자해 = 고정값(기본 0.5, 자해 스탯이 0이면 없음).
        // 일반 수류탄이면 예전과 같습니다.
        float explosionEnemyDamage = enemyDamage;
        float explosionSelfDamage = selfDamage;
        if (incendiarySettings != null)
        {
            explosionEnemyDamage = enemyDamage * incendiarySettings.explosionDamageRatio;
            explosionSelfDamage = selfDamage > 0f ? incendiarySettings.explosionSelfDamage : 0f;
        }

        DamageEnemies(center, explosionEnemyDamage);
        DamageOwner(center, explosionSelfDamage);

        // [115번] 크레모어는 설치된 뒤에만 터지므로 이펙트 위치 = 지금 크레모어 위치(설치 자리, 77번이면 유도로 옮겨 간 자리)입니다.
        Vector3 vfxPosition = claymoreSettings != null ? transform.position : center;
        // [방 전환] 특수방 잠금 해제가 폭발 위치가 아닌 "이 수류탄의 방" 기준으로 판정하도록 방을 함께 넘깁니다.
        if (owner != null) owner.NotifyExploded(center, radius, vfxPosition, ownerRoom);

        // [81번 소이탄] 폭발 이펙트/66번 해킹/57번 토끼/특수방 잠금 해제(NotifyExploded) 뒤에 그 자리에 불타는 구역을 남깁니다.
        // 불 구역 피해는 비율을 곱하기 전의 수류탄 피해(던진 순간 값)를 기준으로 합니다.
        if (incendiarySettings != null)
        {
            try
            {
                IncendiaryFireZone.Spawn(owner, center, radius, explosionHeight, enemyTag, enemyDamage, incendiarySettings);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Grenade: 소이탄 불타는 구역 생성 중 오류가 발생해 건너뜁니다(폭발 자체에는 영향 없음): {e}");
            }
        }

        // [92번 분열탄] 폭발 처리(피해/이펙트/66번/57번/특수방/81번 불 구역)가 모두 끝난 뒤, 터진 위치에서 X자 4방향으로 분열탄을
        // 날립니다. 분열탄은 splitSettings가 항상 null이라 여기로 오지 않습니다.
        // [115번] 크레모어는 설치할 때 분열하므로 터질 때는 분열하지 않습니다.
        if (splitSettings != null && owner != null && claymoreSettings == null)
        {
            try
            {
                SpawnSplitGrenades(center);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Grenade: 분열 수류탄 생성 중 오류가 발생해 건너뜁니다(폭발 자체에는 영향 없음): {e}");
            }
        }

        Destroy(gameObject);
    }

    // [92번 분열탄] 터진 위치 center에서 던진 방향 기준 X자 4방향(앞 ±splitAngle, 뒤 ±(180 - splitAngle))으로 분열탄을 날립니다.
    // - 날아가는 거리 = 원래 폭발 반경 x distanceRatio(기본 1 = 원래 폭발 원의 가장자리). 방 밖이면 방 안쪽 가장자리로 당깁니다.
    // - 피해/반경 = 원래 값(던진 순간) x 비율. 자해 = 원래 자해 x 비율을 0.5 단위로 올림(원래 0이면 0).
    // - 소이탄 여부는 이 수류탄의 값을 그대로 물려줍니다. 착지 후 77번 유도는 분열탄도 일반 수류탄과 같은 규칙으로 판단합니다.
    void SpawnSplitGrenades(Vector3 center)
    {
        var s = splitSettings;

        float childRadius = radius * s.radiusRatio;
        float childEnemyDamage = enemyDamage * s.damageRatio;
        float childSelfDamage = selfDamage > 0f ? RoundUpToHalf(selfDamage * s.selfDamageRatio) : 0f;
        float distance = radius * s.distanceRatio;

        Vector3 forward = throwDirection.sqrMagnitude > 0.0001f ? throwDirection : FallbackForward(owner);
        Vector3 origin = center + Vector3.up * s.spawnHeight;

        float front = s.splitAngle;
        float back = 180f - s.splitAngle;
        float[] angles = { front, -front, back, -back };

        foreach (float angle in angles)
        {
            Vector3 dir = Quaternion.AngleAxis(angle, Vector3.up) * forward;
            Vector3 target = center + dir * distance;
            target.y = center.y;
            target = owner.ClampToRoomBounds(center, target);

            // 분열탄 비행 시간(착탄 = 폭발 또는 77번 유도 시작까지)은 원래 수류탄과 같습니다(형빈 확정 - 이 수류탄이 던져진 순간의 값).
            owner.SpawnSplitGrenade(origin, target, flightTime, s.arcHeight,
                                    childEnemyDamage, childSelfDamage, childRadius,
                                    explosionHeight, enemyTag, s.modelScale, incendiarySettings,
                                    claymoreSettings, claymoreRoom); // [115번] 크레모어면 분열 크레모어로
        }
    }

    // 플레이어가 바라보는 방향(수평)입니다. 플레이어가 없거나 방향이 없으면 월드 앞(+Z)입니다.
    static Vector3 FallbackForward(PlayerGrenadeThrower thrower)
    {
        if (thrower != null)
        {
            Vector3 f = thrower.transform.forward;
            f.y = 0f;
            if (f.sqrMagnitude > 0.0001f) return f.normalized;
        }
        return Vector3.forward;
    }

    // 플레이어 체력 단위(0.5)에 맞춰 올림합니다. 예) 0.25 → 0.5, 0.5 → 0.5, 0.75 → 1.
    static float RoundUpToHalf(float value)
    {
        if (value <= 0f) return 0f;
        return Mathf.Ceil(value * 2f - 0.0001f) * 0.5f;
    }

    void DamageEnemies(Vector3 center, float enemyDamage)
    {
        if (radius <= 0f || enemyDamage <= 0f) return;

        Vector3 bottom = center;
        Vector3 top = center + Vector3.up * Mathf.Max(explosionHeight, 0.1f);
        Collider[] hits = Physics.OverlapCapsule(bottom, top, radius);

        var damaged = new HashSet<EnemyHealth>();
        foreach (var hit in hits)
        {
            if (hit == null) continue;

            EnemyHealth enemyHealth = hit.GetComponent<EnemyHealth>();
            if (enemyHealth == null) enemyHealth = hit.GetComponentInParent<EnemyHealth>();
            if (enemyHealth == null) continue;

            // 콜라이더나 그 적 본체 중 하나라도 적 태그여야 합니다(4번 폭발과 동일한 기준).
            bool isEnemy = string.IsNullOrEmpty(enemyTag)
                           || hit.CompareTag(enemyTag)
                           || enemyHealth.CompareTag(enemyTag);
            if (!isEnemy) continue;

            if (enemyHealth.IsDead) continue;
            if (!damaged.Add(enemyHealth)) continue;

            enemyHealth.TakeWeakDamage(enemyDamage); // 피해 숫자 항상 Weak(형빈 확정)
        }
    }

    void DamageOwner(Vector3 center, float selfDamage)
    {
        if (owner == null || selfDamage <= 0f) return;

        // [57번 패시브 등 - 수류탄 자해 무효] 막는 아이템을 보유 중이면 피해 처리 자체를 건너뜁니다(피격 점멸/무적 시간/
        // 21번 장신구 판정도 일어나지 않음). 수류탄 계열 폭발이 새로 생기면 그쪽 자해 판정에도 같은 확인을 넣어주세요.
        if (GrenadeSelfDamageOverride.IsBlocked) return;

        // [74번 돌진 회피] 터지는 순간 플레이어가 돌진 중이면 자해 피해를 받지 않습니다.
        if (PlayerDash.IsDodging) return;

        PlayerHealth health = owner.OwnerHealth;
        if (health == null || health.IsDead) return;

        Vector3 playerPos = owner.transform.position;
        Vector2 flatOffset = new Vector2(playerPos.x - center.x, playerPos.z - center.z);
        if (flatOffset.magnitude <= radius)
        {
            health.TakeDamage(selfDamage, PlayerDamageSource.GrenadeSelf); // [플레이 통계] 사망 원인 = 수류탄 자해
        }
    }
}
