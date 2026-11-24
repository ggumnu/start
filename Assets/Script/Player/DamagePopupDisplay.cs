using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 적이 피해를 받을 때 실제 피해량을 숫자로 띄우는 HUD입니다(EnemyHealth.OnAnyEnemyDamaged 구독 - 모든 피해가 지나는 통로).
// [포물선 연출 - 형빈 확정] 피격 즉시 적 몸통 가운데에서 "뿅" 나타나 오른쪽 위로 포물선을 그리며 튀어나가고, 꼭대기를 지나 내려가는 동안
// 서서히 사라집니다. 98번 섬멸은 숫자 대신 글자(머리 위 고정, 예전 그대로).
// [연속 피격 - 형빈 확정] 정확한 값보다 "많이 때렸다"가 보이는 게 목적입니다.
//   - 피해가 클수록 숫자가 큼(공격력 대비 비율 - 기본 0.5배 이하 0.8배 크기 ~ 3배 이상 1.6배 크기).
//   - 큰 숫자가 작은 숫자 위에 그려짐(같은 크기면 최근 것이 위) - 4번 폭발이 총알 숫자를 덮지 않게.
//   - 같은 적이 짧은 시간 안에 연달아 맞으면 오른쪽 위 부채꼴 안에서 숫자마다 다른 각도로 튀어나감.
// [블루 아카이브식 스타일 - 형빈 확정] "현재 공격력"(PlayerStats.Damage)과 비교해 세 가지로 띄웁니다(기울인 글씨):
//   보통   = 피해 == 공격력          : 빨간 폭발 배경 + 흰 숫자 / 검붉은 테두리
//   Weak   = 피해 > 공격력, 지속 피해 : 빨간 폭발 배경 + 노란 숫자 / 어두운 주황 테두리 + 위에 주황 "Weak"
//   Resist = 피해 < 공격력, 보호막 피해(2레벨 보스, OnAnyEnemyShieldDamaged) : 폭발 없이 하늘색 숫자 / 파란 테두리 + "Resist"
// 공격력과 비교하는 것은 총알 피해뿐입니다. 지속 피해(EnemyHealth.IsDotDamageInProgress)와 아이템 효과 부가 피해(IsForcedWeakDamageInProgress -
// 4번 폭발, 29번, 68번 충돌, 81번 연소, 87번 아군 공격, 94번 후광, 수류탄, 해킹툴, 118번 포격)는 항상 Weak입니다(보호막에 들어간 피해는
// 보호막 규칙 = Resist). 설계 배경은 Docs/설계노트.md [피해 숫자].
// [3레벨 보스] 피해 감소막으로 줄어든 피해는 어떤 피해든 항상 Resist(EnemyHealth.IsReducedDamageInProgress), 감소막 생성 연출(무적) 중에 맞으면
// 숫자 없이 빨간 폭발 배경 + 흰 "MISS"(EnemyHealth.OnAnyEnemyMissed) - 형빈 확정.
// [3레벨 보스 그로기 비콘] 적이 아닌 대상(수류탄/해킹 비콘이 총알을 막음)에도 같은 "MISS"를 띄울 수 있습니다(ShowMissOn - 대상 Transform을 따라감).
// [63번 미사일] 미사일 명중 피해는 "공격력 x 미사일 피해 배율(50%)"을 기준으로 비교합니다(EnemyHealth.DamageReferenceScale, 형빈 확정).
public class DamagePopupDisplay : MonoBehaviour
{
    [Header("글자 모양")]
    [Tooltip("숫자 테두리 두께(캔버스 픽셀)입니다. 너무 키우면(약 5 이상) 테두리가 각져 보일 수 있습니다.")]
    [Min(0f)] public float outlineThickness = 2f;

    [Tooltip("숫자 글자 크기입니다.")]
    public int fontSize = 40;

    [Tooltip("켜면 숫자와 Weak/Resist 글자를 블루 아카이브처럼 기울여 씁니다(TMP 기울임꼴).")]
    public bool italic = true;

    [Header("보통 (피해 = 현재 공격력)")]
    public Color normalTextColor = Color.white;
    public Color normalOutlineColor = new Color(0.42f, 0.05f, 0.06f, 1f);

    [Header("Weak (피해 > 현재 공격력, 지속 피해)")]
    public Color weakTextColor = new Color(1f, 0.89f, 0.24f, 1f);
    public Color weakOutlineColor = new Color(0.62f, 0.26f, 0.04f, 1f);
    public string weakLabel = "Weak";
    public Color weakLabelColor = new Color(1f, 0.6f, 0.12f, 1f);
    public Color weakLabelOutlineColor = new Color(0.55f, 0.21f, 0.03f, 1f);

    [Header("Resist (피해 < 현재 공격력, 보호막 피해)")]
    public Color resistTextColor = new Color(0.6f, 0.88f, 1f, 1f);
    public Color resistOutlineColor = new Color(0.12f, 0.32f, 0.78f, 1f);
    public string resistLabel = "Resist";
    public Color resistLabelColor = new Color(0.55f, 0.84f, 1f, 1f);
    public Color resistLabelOutlineColor = new Color(0.12f, 0.32f, 0.78f, 1f);

    [Header("MISS (3레벨 보스 감소막 생성 중 - 숫자 없이 폭발 배경 + 글자)")]
    public string missText = "MISS";
    public Color missTextColor = Color.white;
    public Color missOutlineColor = new Color(0.42f, 0.05f, 0.06f, 1f);

    [Header("Weak / Resist 글자")]
    [Tooltip("Weak/Resist 글자 크기 = 숫자 글자 크기 x 이 값.")]
    [Min(0.1f)] public float labelFontScale = 0.55f;
    [Tooltip("Weak/Resist 글자 위치: 숫자 가운데 기준(캔버스 픽셀, X = 오른쪽, Y = 위).")]
    public Vector2 labelOffset = new Vector2(-14f, 24f);
    [Tooltip("Weak/Resist 글자 테두리 두께(캔버스 픽셀)입니다.")]
    [Min(0f)] public float labelOutlineThickness = 1.5f;

    [Header("폭발 배경 (보통 / Weak)")]
    [Tooltip("숫자 뒤의 빨간 폭발 이미지입니다. 비우면 Resources/DamageBurst를 씁니다.")]
    public Sprite burstSprite;
    [Tooltip("폭발 이미지 크기 = 숫자 글자 크기 x 이 값(캔버스 픽셀).")]
    [Min(0.1f)] public float burstSizeScale = 2.6f;
    [Tooltip("폭발 이미지 위치: 숫자 가운데 기준(캔버스 픽셀).")]
    public Vector2 burstOffset = new Vector2(0f, -2f);
    [Tooltip("숫자마다 폭발 이미지를 무작위로 돌리는 최대 각도(도)입니다.")]
    [Range(0f, 180f)] public float burstRandomRotation = 25f;

    [Header("공격력 비교")]
    [Tooltip("피해와 현재 공격력의 차이가 이 값 이하이면 같은 것으로 봅니다(소수점 오차 방지).")]
    [Min(0f)] public float attackCompareTolerance = 0.01f;

    [Header("표시 시간과 연출")]
    [Tooltip("숫자가 화면에 보이는 총 시간(초)입니다. 포물선 꼭대기를 지난 뒤 남은 시간 동안 서서히 사라집니다.")]
    [Min(0.05f)] public float popupLifetime = 0.8f;

    [Tooltip("'뿅' 하고 커지며 나타나는 데 걸리는 시간(초)입니다.")]
    [Min(0.01f)] public float popDuration = 0.12f;

    [Tooltip("나타날 때 시작하는 크기 배율입니다(0.3 = 30% 크기에서 시작).")]
    [Min(0.01f)] public float popStartScale = 0.3f;

    [Tooltip("나타날 때 잠깐 커지는 최대 크기 배율입니다(1보다 크면 살짝 튀어나온 뒤 안착).")]
    [Min(0.01f)] public float popOvershoot = 1.3f;

    [Header("포물선")]
    [Tooltip("튀어나가는 처음 속도의 최솟값(캔버스 픽셀/초, X = 오른쪽, Y = 위). 펼치기를 켜면 방향은 펼치기 각도가 정하고, " +
             "이 값과 최댓값은 튀는 힘(속도 크기 = 이 벡터의 길이 ~ 최댓값 벡터의 길이)으로만 씁니다. 끄면 예전처럼 X/Y를 각각 무작위로 고릅니다.")]
    public Vector2 launchVelocityMin = new Vector2(110f, 380f);
    [Tooltip("튀어나가는 처음 속도의 최댓값(캔버스 픽셀/초). 쓰는 방식은 최솟값 설명 참고.")]
    public Vector2 launchVelocityMax = new Vector2(210f, 470f);
    [Tooltip("포물선을 아래로 당기는 힘(캔버스 픽셀/초²)입니다. 클수록 낮고 빠르게 꺾입니다(꼭대기 시간 = 위 속도 / 이 값).")]
    [Min(1f)] public float launchGravity = 1400f;
    [Tooltip("시작 위치: 적 몸통 가운데에서 화면 기준으로 이만큼 옮깁니다(캔버스 픽셀).")]
    public Vector2 launchStartOffset = Vector2.zero;

    [Header("피해 크기별 숫자 크기")]
    [Tooltip("켜면 피해가 클수록 숫자(폭발 배경/Weak 글자 포함)가 커집니다(현재 공격력 대비 비율). 끄면 모두 같은 크기.")]
    public bool scaleByDamage = true;
    [Tooltip("피해가 공격력의 이 배수 이하이면 가장 작은 크기입니다(기본 0.5배).")]
    [Min(0f)] public float smallDamageRatio = 0.5f;
    [Tooltip("가장 작은 크기 배율입니다(기본 0.8). 피해 = 공격력이면 항상 1배.")]
    [Min(0.1f)] public float smallDamageScale = 0.8f;
    [Tooltip("피해가 공격력의 이 배수 이상이면 가장 큰 크기입니다(기본 3배).")]
    [Min(1.01f)] public float bigDamageRatio = 3f;
    [Tooltip("가장 큰 크기 배율입니다(기본 1.6).")]
    [Min(0.1f)] public float bigDamageScale = 1.6f;

    [Header("연속 피격 펼치기 (오른쪽 위 부채꼴)")]
    [Tooltip("켜면 같은 적이 연달아 맞을 때 숫자마다 다른 각도로 튀어나갑니다. 끄면 예전처럼 속도 범위에서 무작위.")]
    public bool spreadConsecutiveHits = true;
    [Tooltip("같은 적이 이 시간(초) 안에 다시 맞으면 '연달아 맞음'으로 보고 다음 각도를 씁니다. 더 늦게 맞으면 첫 각도부터 다시.")]
    [Min(0f)] public float spreadWindow = 0.3f;
    [Tooltip("부채꼴의 한쪽 끝 각도(도) - 바로 위(0)에서 오른쪽으로 기운 정도입니다.")]
    [Range(-80f, 80f)] public float spreadAngleMin = 5f;
    [Tooltip("부채꼴의 다른 쪽 끝 각도(도) - 바로 위(0)에서 오른쪽으로 기운 정도입니다(90 = 수평 오른쪽).")]
    [Range(-80f, 80f)] public float spreadAngleMax = 50f;
    [Tooltip("부채꼴을 몇 개의 방향으로 나눌지입니다. 연달아 맞을 때 바로 앞 숫자와 최대한 먼 방향부터 씁니다.")]
    [Range(2, 8)] public int spreadSteps = 4;
    [Tooltip("각 방향에 더하는 무작위 흔들림(도)입니다. 0이면 정확히 같은 각도.")]
    [Min(0f)] public float spreadAngleJitter = 3f;

    [Header("98번 섬멸 글자 위치")]
    [Tooltip("섬멸 글자: 적 머리 기준점에서 화면 기준 오른쪽 위로 띄울 거리(캔버스 픽셀)입니다.")]
    public Vector2 screenOffset = new Vector2(45f, 25f);
    [Tooltip("섬멸 글자: 적의 자동 계산된 머리 높이(Collider/Renderer 최상단)에 더할 여유 높이(월드 단위)입니다.")]
    public float headroom = 0.15f;

    [Header("개수")]

    [Tooltip("동시에 화면에 띄울 수 있는 숫자 최대 개수입니다. 넘으면 가장 오래된 숫자를 재사용합니다.")]
    [Min(1)] public int maxPopups = 60;

    [Header("캔버스")]
    [Tooltip("다른 HUD 위에 그려지는 정렬 순서입니다. (장전 표시 5, F 안내 10, 툴팁 20이므로 " +
             "그보다 낮게 둬서 UI 안내를 가리지 않게 합니다.)")]
    public int canvasSortingOrder = 8;

    [Tooltip("CanvasScaler의 기준 해상도입니다. 다른 HUD와 동일하게 1920x1080을 씁니다.")]
    public Vector2 referenceResolution = new Vector2(1920f, 1080f);

    private class Popup
    {
        public GameObject go;
        public RectTransform rect;
        public TextMeshProUGUI text;
        public bool active;
        public float age;
        public EnemyHealth enemy;      // 따라다닐 적(파괴되면 null, 적이 아닌 대상이면 비어 있음)
        public Transform follow;       // 따라다닐 대상(적이면 적의 Transform, 비콘 MISS면 비콘)
        public float headHeight;       // 적 pivot 기준 머리 높이(월드 단위)
        public Vector3 lastEnemyPos;   // 적이 사라진 뒤 고정할 마지막 위치
        public float bodyHeight;       // 적 pivot 기준 몸통 가운데 높이(월드 단위) - 포물선 시작점
        public Vector2 velocity;       // 포물선 처음 속도(캔버스 픽셀/초)
        public float peakTime;         // 포물선 꼭대기에 닿는 시간(초) - 이후 서서히 사라짐

        // [98번] "섬멸" 글자(숫자 대신)인지와 그 모양입니다.
        public bool isLabel;
        public Color labelTextColor;
        public Color labelOutlineColor;
        public float labelOutlinePixels;
        public int labelFontSize;
        public float labelFadeOutTime;
        public float labelFadeElapsed;   // 적이 사라진 뒤 흐른 시간(-1 = 아직 적이 남아 있음)

        public bool isShield;            // [2레벨 보스] 보호막이 받은 피해(Resist로 표시)
        public float sizeScale = 1f;     // 피해 크기별 크기 배율(그리는 순서도 이 값 기준)
        public int spawnOrder;           // 같은 크기일 때 최근 것이 위

        public HitStyle style;
        public RectTransform burstRect;
        public Image burstImage;
        public TextMeshProUGUI labelText;
    }

    private enum HitStyle { Normal, Weak, Resist, Miss }

    private PlayerStats playerStats;

    // [98번] 적이 사라지지 않는 예외 상황에서도 "섬멸" 글자가 남지 않도록 하는 최대 표시 시간(초)입니다.
    private const float LabelMaxLifetime = 5f;

    private readonly List<Popup> popups = new List<Popup>();
    private readonly List<Popup> drawOrderBuffer = new List<Popup>();
    private int spawnCounter;

    // 연속 피격 펼치기: 적마다 마지막으로 맞은 시간과 몇 번째 연속 피격인지.
    private struct SpreadState { public float lastTime; public int index; }
    private readonly Dictionary<Transform, SpreadState> spreadStates = new Dictionary<Transform, SpreadState>();
    private readonly List<Transform> spreadPruneBuffer = new List<Transform>();
    private int[] spreadOrder;
    private const int SpreadPruneThreshold = 64;
    private Canvas canvas;
    private CanvasScaler canvasScaler;
    private RectTransform canvasRect;
    private Camera mainCamera;
    private bool built;

    void Awake()
    {
        EnsureBuilt();
    }

    // [3레벨 보스 그로기 비콘] 적이 아닌 대상에 MISS를 띄울 때 쓰는 켜져 있는 HUD입니다.
    private static DamagePopupDisplay activeInstance;

    // 적이 아닌 대상(그로기 비콘 등) 위에 숫자 없이 "MISS"를 띄웁니다. HUD가 없으면 아무것도 하지 않습니다.
    public static void ShowMissOn(Transform target)
    {
        if (target == null || activeInstance == null) return;
        activeInstance.ShowPopupCore(target, null, 1f, false, false, HitStyle.Miss);
    }

    void OnEnable()
    {
        activeInstance = this;
        EnemyHealth.OnAnyEnemyDamaged += HandleEnemyDamaged;
        EnemyHealth.OnAnyEnemyShieldDamaged += HandleShieldDamaged;
        EnemyHealth.OnAnyEnemyMissed += HandleEnemyMissed;
    }

    void OnDisable()
    {
        if (activeInstance == this) activeInstance = null;
        EnemyHealth.OnAnyEnemyDamaged -= HandleEnemyDamaged;
        EnemyHealth.OnAnyEnemyShieldDamaged -= HandleShieldDamaged;
        EnemyHealth.OnAnyEnemyMissed -= HandleEnemyMissed;
        for (int i = 0; i < popups.Count; i++) Deactivate(popups[i]);
        spreadStates.Clear();
    }

    void LateUpdate()
    {
        if (!built) return;

        UpdateCanvasScalerMatch();

        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null) return;

        float dt = Time.deltaTime;
        for (int i = 0; i < popups.Count; i++)
        {
            Popup p = popups[i];
            if (!p.active) continue;

            p.age += dt;

            // [98번] "섬멸" 글자는 적이 완전히 사라질 때까지 유지하고, 사라진 뒤 짧게 옅어집니다.
            if (p.isLabel)
            {
                bool enemyGone = p.follow == null || !p.follow.gameObject.activeInHierarchy;
                if (enemyGone && p.labelFadeElapsed < 0f) p.labelFadeElapsed = 0f;
                if (p.labelFadeElapsed >= 0f) p.labelFadeElapsed += dt;

                if (p.labelFadeElapsed >= p.labelFadeOutTime || p.age >= LabelMaxLifetime)
                {
                    Deactivate(p);
                    continue;
                }

                UpdatePopup(p, dt);
                continue;
            }

            if (p.age >= popupLifetime)
            {
                Deactivate(p);
                continue;
            }

            UpdatePopup(p, dt);
        }
    }

    private void HandleEnemyDamaged(EnemyHealth enemy, float amount)
    {
        // [3레벨 보스] 감소막으로 줄어든 피해는 지속/부가 피해여도 Resist.
        HitStyle? forced = EnemyHealth.IsReducedDamageInProgress ? HitStyle.Resist : (HitStyle?)null;
        ShowPopup(enemy, amount, false, EnemyHealth.IsDotDamageInProgress || EnemyHealth.IsForcedWeakDamageInProgress, forced);
    }

    private void HandleShieldDamaged(EnemyHealth enemy, float amount)
    {
        ShowPopup(enemy, amount, true, false);
    }

    // [3레벨 보스] 감소막 생성 연출(무적) 중 피격 - 숫자 없이 "MISS"(크기는 보통 1배).
    private void HandleEnemyMissed(EnemyHealth enemy)
    {
        ShowPopup(enemy, 1f, false, false, HitStyle.Miss);
    }

    // 현재 공격력(PlayerStats.Damage). 플레이어를 못 찾으면 false.
    private bool TryGetPlayerAttack(out float attack)
    {
        attack = 0f;
        if (playerStats == null)
        {
            var health = FindFirstObjectByType<PlayerHealth>();
            if (health != null) playerStats = health.GetComponent<PlayerStats>();
        }
        if (playerStats == null) return false;
        // [63번 미사일] 미사일 피해는 "공격력 x 미사일 피해 배율"과 비교합니다(EnemyHealth.DamageReferenceScale - 평소 1).
        attack = playerStats.Damage * EnemyHealth.DamageReferenceScale;
        return true;
    }

    // 피해와 현재 공격력을 비교해 스타일을 정합니다. 플레이어를 못 찾으면 보통.
    private HitStyle ResolveStyle(float amount, bool isShield, bool alwaysWeak)
    {
        if (isShield) return HitStyle.Resist;
        if (alwaysWeak) return HitStyle.Weak;
        if (!TryGetPlayerAttack(out float attack)) return HitStyle.Normal;
        if (amount > attack + attackCompareTolerance) return HitStyle.Weak;
        if (amount < attack - attackCompareTolerance) return HitStyle.Resist;
        return HitStyle.Normal;
    }

    // 피해 크기별 숫자 크기 배율입니다(공격력 대비 비율 - 작은 쪽 ~ 1배 ~ 큰 쪽을 직선으로 이음). 공격력을 모르면 1배.
    private float ResolveSizeScale(float amount)
    {
        if (!scaleByDamage) return 1f;
        if (!TryGetPlayerAttack(out float attack) || attack <= 0f) return 1f;

        float ratio = amount / attack;
        float smallRatio = Mathf.Min(smallDamageRatio, 0.999f);
        float bigRatio = Mathf.Max(bigDamageRatio, 1.001f);
        if (ratio <= 1f) return Mathf.Lerp(smallDamageScale, 1f, Mathf.InverseLerp(smallRatio, 1f, ratio));
        return Mathf.Lerp(1f, bigDamageScale, Mathf.InverseLerp(1f, bigRatio, ratio));
    }

    // 이번 숫자의 처음 속도입니다. 펼치기를 켜면 같은 적의 연속 피격마다 부채꼴 안의 다른 각도를 씁니다.
    private Vector2 ResolveLaunchVelocity(Transform enemy)
    {
        if (!spreadConsecutiveHits || spreadSteps < 2)
        {
            return new Vector2(Random.Range(launchVelocityMin.x, launchVelocityMax.x), Random.Range(launchVelocityMin.y, launchVelocityMax.y));
        }

        float now = Time.time;
        int index = 0;
        if (spreadStates.TryGetValue(enemy, out SpreadState state) && now - state.lastTime <= spreadWindow) index = state.index + 1;
        if (spreadStates.Count >= SpreadPruneThreshold) PruneSpreadStates(now);
        spreadStates[enemy] = new SpreadState { lastTime = now, index = index };

        int[] order = GetSpreadOrder(spreadSteps);
        int step = order[index % order.Length];
        float angle = Mathf.Lerp(spreadAngleMin, spreadAngleMax, step / (float)(order.Length - 1));
        angle += Random.Range(-spreadAngleJitter, spreadAngleJitter);

        float speed = Random.Range(launchVelocityMin.magnitude, launchVelocityMax.magnitude);
        float rad = angle * Mathf.Deg2Rad;
        return new Vector2(Mathf.Sin(rad), Mathf.Cos(rad)) * speed;
    }

    // 연속 피격 때 쓸 방향 순서입니다(0 = 부채꼴 한쪽 끝). 바로 앞 숫자와 최대한 먼 방향이 오도록 반씩 쪼개 나가는 순서를 쓰고,
    // 첫 숫자는 예전 느낌(살짝 오른쪽)에 가깝도록 한 칸 옮깁니다. 예) 4칸 = 1, 3, 2, 0.
    private int[] GetSpreadOrder(int steps)
    {
        if (spreadOrder != null && spreadOrder.Length == steps) return spreadOrder;

        var order = new List<int>(steps);
        var used = new bool[steps];
        for (int i = 0; order.Count < steps && i < 1024; i++)
        {
            // i의 비트를 뒤집은 소수(0, 0.5, 0.25, 0.75, ...)로 칸을 고릅니다.
            float f = 0f, b = 0.5f;
            for (int n = i; n > 0; n >>= 1, b *= 0.5f) if ((n & 1) != 0) f += b;
            int pos = Mathf.Min(steps - 1, Mathf.FloorToInt(f * steps));
            if (used[pos]) continue;
            used[pos] = true;
            order.Add((pos + 1) % steps);
        }
        for (int pos = 0; order.Count < steps; pos++) if (!used[pos]) { used[pos] = true; order.Add((pos + 1) % steps); }

        spreadOrder = order.ToArray();
        return spreadOrder;
    }

    // 사라진 적/오래전에 맞은 적의 기록을 지웁니다(기록이 계속 쌓이지 않게).
    private void PruneSpreadStates(float now)
    {
        spreadPruneBuffer.Clear();
        foreach (var pair in spreadStates)
        {
            if (pair.Key == null || now - pair.Value.lastTime > spreadWindow) spreadPruneBuffer.Add(pair.Key);
        }
        for (int i = 0; i < spreadPruneBuffer.Count; i++) spreadStates.Remove(spreadPruneBuffer[i]);
        spreadPruneBuffer.Clear();
    }

    // 그리는 순서: 작은 숫자 -> 큰 숫자(같은 크기면 오래된 것 -> 최근 것), 98번 섬멸 글자는 맨 위.
    private void SortDrawOrder()
    {
        drawOrderBuffer.Clear();
        for (int i = 0; i < popups.Count; i++) if (popups[i].active) drawOrderBuffer.Add(popups[i]);
        drawOrderBuffer.Sort((a, b) =>
        {
            float sa = a.isLabel ? float.MaxValue : a.sizeScale;
            float sb = b.isLabel ? float.MaxValue : b.sizeScale;
            int c = sa.CompareTo(sb);
            return c != 0 ? c : a.spawnOrder.CompareTo(b.spawnOrder);
        });
        for (int i = 0; i < drawOrderBuffer.Count; i++) drawOrderBuffer[i].go.transform.SetAsLastSibling();
        drawOrderBuffer.Clear();
    }

    private void ShowPopup(EnemyHealth enemy, float amount, bool isShield, bool alwaysWeak, HitStyle? forcedStyle = null)
    {
        if (enemy == null || amount <= 0f) return;
        if (AllyFaction.IsAlly(enemy.gameObject)) return; // [87번] 아군이 맞을 때는 피해 숫자를 띄우지 않습니다.
        ShowPopupCore(enemy.transform, enemy, amount, isShield, alwaysWeak, forcedStyle);
    }

    // target = 따라갈 대상, enemy = 그 대상이 적이면 그 적(적이 아니면 null - 98번 섬멸 확인 등 적 전용 처리를 건너뜀).
    private void ShowPopupCore(Transform target, EnemyHealth enemy, float amount, bool isShield, bool alwaysWeak, HitStyle? forcedStyle)
    {
        if (target == null || amount <= 0f) return;
        EnsureBuilt();

        int shown = Mathf.Max(1, Mathf.RoundToInt(amount));

        Popup p = AcquirePopup();
        p.active = true;
        p.age = 0f;
        p.enemy = enemy;
        p.follow = target;
        MeasureHeights(target, out p.headHeight, out p.bodyHeight);
        p.lastEnemyPos = target.position;
        p.velocity = ResolveLaunchVelocity(target);
        p.peakTime = Mathf.Max(0f, p.velocity.y) / Mathf.Max(1f, launchGravity);
        p.isShield = isShield;
        p.style = forcedStyle ?? ResolveStyle(amount, isShield, alwaysWeak);
        bool isMiss = p.style == HitStyle.Miss;
        p.spawnOrder = spawnCounter++;

        // [98번] 즉시 처치(섬멸)면 숫자 대신 설정의 글자를 띄웁니다.
        var execute = !isShield && !isMiss && enemy != null && enemy.WasExecuted ? enemy.ExecuteSettings : null;
        p.isLabel = execute != null;
        p.sizeScale = p.isLabel || isMiss ? 1f : ResolveSizeScale(amount);
        if (p.isLabel)
        {
            p.labelTextColor = execute.popupTextColor;
            p.labelOutlineColor = execute.popupOutlineColor;
            p.labelOutlinePixels = execute.popupOutlinePixels;
            p.labelFontSize = execute.popupFontSize;
            p.labelFadeOutTime = Mathf.Max(execute.popupFadeOutTime, 0f);
            p.labelFadeElapsed = -1f;
            p.text.text = string.IsNullOrEmpty(execute.popupText) ? shown.ToString() : execute.popupText;
        }
        else
        {
            p.text.text = isMiss ? missText : shown.ToString();
        }

        // 폭발 배경(보통/Weak)과 Weak/Resist 글자. 섬멸 글자에는 붙이지 않습니다.
        bool showBurst = !p.isLabel && p.style != HitStyle.Resist && GetBurstSprite() != null;
        p.burstImage.gameObject.SetActive(showBurst);
        if (showBurst)
        {
            p.burstImage.sprite = GetBurstSprite();
            float size = fontSize * burstSizeScale;
            p.burstRect.sizeDelta = new Vector2(size, size);
            p.burstRect.anchoredPosition = burstOffset;
            p.burstRect.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-burstRandomRotation, burstRandomRotation));
        }
        bool showLabel = !p.isLabel && p.style != HitStyle.Normal && p.style != HitStyle.Miss;
        p.labelText.gameObject.SetActive(showLabel);
        if (showLabel)
        {
            p.labelText.text = p.style == HitStyle.Weak ? weakLabel : resistLabel;
            p.labelText.rectTransform.anchoredPosition = labelOffset;
        }
        FontStyles fs = italic ? FontStyles.Italic : FontStyles.Normal;
        p.text.fontStyle = p.isLabel ? FontStyles.Normal : fs;
        p.labelText.fontStyle = fs;
        p.go.SetActive(true);
        SortDrawOrder();

        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera != null) UpdatePopup(p, 0f); // 첫 프레임 깜빡임 방지
    }

    // 적 pivot(transform.position) 기준으로 Collider(없으면 Renderer) 최상단(머리)과 가운데(몸통)가 얼마나 위인지 잽니다.
    private void MeasureHeights(Transform enemy, out float head, out float body)
    {
        float baseY = enemy.position.y;
        float top = float.NegativeInfinity, bottom = float.PositiveInfinity;

        var colliders = enemy.GetComponentsInChildren<Collider>();
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] == null || colliders[i].isTrigger) continue;
            top = Mathf.Max(top, colliders[i].bounds.max.y);
            bottom = Mathf.Min(bottom, colliders[i].bounds.min.y);
        }

        if (float.IsNegativeInfinity(top))
        {
            var renderers = enemy.GetComponentsInChildren<Renderer>();
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                top = Mathf.Max(top, renderers[i].bounds.max.y);
                bottom = Mathf.Min(bottom, renderers[i].bounds.min.y);
            }
        }

        if (float.IsNegativeInfinity(top)) { head = 1f; body = 0.5f; return; } // 재지 못하면 대략적인 기본 높이
        head = Mathf.Max(0f, top - baseY);
        body = Mathf.Max(0f, (top + bottom) * 0.5f - baseY);
    }

    private void UpdatePopup(Popup p, float dt)
    {
        // 적이 살아있는 동안만 위치를 갱신하고, 사라지면 마지막 위치에 고정합니다.
        if (p.follow != null) p.lastEnemyPos = p.follow.position;

        // 섬멸 글자는 머리 위 고정, 숫자는 몸통 가운데에서 포물선 시작.
        Vector3 world = p.isLabel ? p.lastEnemyPos + Vector3.up * (p.headHeight + headroom)
                                  : p.lastEnemyPos + Vector3.up * p.bodyHeight;
        Vector3 screen = mainCamera.WorldToScreenPoint(world);

        if (screen.z < 0f)
        {
            p.go.SetActive(false);
            return;
        }
        if (!p.go.activeSelf) p.go.SetActive(true);

        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out Vector2 local);

        // 크기: 뿅(작게 -> 오버슈트 -> 1)
        float scale = 1f;
        if (p.age < popDuration)
        {
            float a = p.age / popDuration;
            if (a < 0.6f) scale = Mathf.Lerp(popStartScale, popOvershoot, a / 0.6f);
            else scale = Mathf.Lerp(popOvershoot, 1f, (a - 0.6f) / 0.4f);
        }

        // 투명도: 포물선 꼭대기까지는 그대로, 꼭대기를 지나 내려가는 동안 남은 시간에 걸쳐 서서히 사라짐.
        float alpha = 1f;
        Vector2 arc = Vector2.zero;
        if (p.isLabel)
        {
            // [98번] "섬멸" 글자는 떨어지지 않고, 적이 사라진 뒤에만 옅어집니다.
            if (p.labelFadeElapsed >= 0f)
            {
                alpha = p.labelFadeOutTime > 0f ? 1f - Mathf.Clamp01(p.labelFadeElapsed / p.labelFadeOutTime) : 0f;
            }
        }
        else
        {
            float a = p.age;
            arc = new Vector2(p.velocity.x * a, p.velocity.y * a - 0.5f * launchGravity * a * a);
            // 꼭대기가 표시 시간보다 늦으면 마지막 30% 동안 사라지게 합니다.
            float fadeStart = Mathf.Min(p.peakTime, popupLifetime * 0.7f);
            if (a > fadeStart) alpha = 1f - Mathf.Clamp01((a - fadeStart) / Mathf.Max(0.0001f, popupLifetime - fadeStart));
        }

        p.rect.anchoredPosition = p.isLabel ? local + screenOffset : local + launchStartOffset + arc;
        scale *= p.sizeScale;
        p.rect.localScale = new Vector3(scale, scale, 1f);

        p.text.fontSize = p.isLabel ? p.labelFontSize : fontSize;
        GetStyleColors(p.style, out Color styleText, out Color styleOutline, out Color styleLabel, out Color styleLabelOutline);
        Color c = p.isLabel ? p.labelTextColor : styleText; c.a *= alpha;
        p.text.color = c;
        // [TMP] 외곽선은 Outline 컴포넌트 대신 TMP 자체 외곽선을 씁니다(두께는 예전처럼 픽셀 값 - HUDText.SetOutline이
        // TMP 비율로 바꿈). 예전 Outline(useGraphicAlpha)처럼 외곽선도 글자와 함께 투명해집니다.
        Color oc = p.isLabel ? p.labelOutlineColor : styleOutline; oc.a *= alpha;
        HUDText.SetOutline(p.text, p.isLabel ? p.labelOutlinePixels : outlineThickness, oc);

        if (p.burstImage.gameObject.activeSelf)
        {
            p.burstImage.color = new Color(1f, 1f, 1f, alpha);
        }
        if (p.labelText.gameObject.activeSelf)
        {
            p.labelText.fontSize = fontSize * labelFontScale;
            Color lc = styleLabel; lc.a *= alpha;
            p.labelText.color = lc;
            Color loc = styleLabelOutline; loc.a *= alpha;
            HUDText.SetOutline(p.labelText, labelOutlineThickness, loc);
        }
    }

    private void GetStyleColors(HitStyle style, out Color text, out Color outline, out Color label, out Color labelOutline)
    {
        switch (style)
        {
            case HitStyle.Weak:
                text = weakTextColor; outline = weakOutlineColor; label = weakLabelColor; labelOutline = weakLabelOutlineColor;
                break;
            case HitStyle.Resist:
                text = resistTextColor; outline = resistOutlineColor; label = resistLabelColor; labelOutline = resistLabelOutlineColor;
                break;
            case HitStyle.Miss:
                text = missTextColor; outline = missOutlineColor; label = missTextColor; labelOutline = missOutlineColor;
                break;
            default:
                text = normalTextColor; outline = normalOutlineColor; label = normalTextColor; labelOutline = normalOutlineColor;
                break;
        }
    }

    private Sprite cachedBurstSprite;
    private bool burstSpriteLoaded;

    // 인스펙터 칸이 비어 있으면 Resources/DamageBurst를 한 번 읽어 씁니다(프리팹을 고치지 않아도 바로 동작하도록).
    private Sprite GetBurstSprite()
    {
        if (burstSprite != null) return burstSprite;
        if (!burstSpriteLoaded)
        {
            burstSpriteLoaded = true;
            cachedBurstSprite = Resources.Load<Sprite>("DamageBurst");
        }
        return cachedBurstSprite;
    }

    private Popup AcquirePopup()
    {
        for (int i = 0; i < popups.Count; i++)
            if (!popups[i].active) return popups[i];

        if (popups.Count < maxPopups) return CreatePopup();

        // 가득 찼으면 가장 오래된 숫자를 재사용.
        Popup oldest = popups[0];
        for (int i = 1; i < popups.Count; i++)
            if (popups[i].age > oldest.age) oldest = popups[i];
        return oldest;
    }

    private Popup CreatePopup()
    {
        var go = new GameObject("DamagePopup");
        go.transform.SetParent(canvas.transform, false);

        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(240f, 80f);

        // 자식 순서 = 그리는 순서: 폭발 배경(맨 뒤) -> 숫자 -> Weak/Resist 글자(맨 앞).
        var burstGo = new GameObject("Burst");
        burstGo.transform.SetParent(go.transform, false);
        var burstRect = burstGo.AddComponent<RectTransform>();
        burstRect.anchorMin = burstRect.anchorMax = new Vector2(0.5f, 0.5f);
        burstRect.pivot = new Vector2(0.5f, 0.5f);
        var burstImage = burstGo.AddComponent<Image>();
        burstImage.raycastTarget = false;
        burstImage.preserveAspect = true;

        // [TMP] 옛 방식 Text는 화면 배율에 따라 흐릿하게 보여서 TextMeshPro로 교체했습니다(HUDText 참고).
        // TMP SDF 글자라 확대 연출(localScale)을 해도 흐려지지 않습니다.
        var textGo = new GameObject("Number");
        textGo.transform.SetParent(go.transform, false);
        var textRect = textGo.AddComponent<RectTransform>();
        textRect.anchorMin = textRect.anchorMax = new Vector2(0.5f, 0.5f);
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.sizeDelta = new Vector2(240f, 80f);
        var text = HUDText.AddTo(textGo, TextAnchor.MiddleCenter);

        var labelGo = new GameObject("Label");
        labelGo.transform.SetParent(go.transform, false);
        var labelRect = labelGo.AddComponent<RectTransform>();
        labelRect.anchorMin = labelRect.anchorMax = new Vector2(0.5f, 0.5f);
        labelRect.pivot = new Vector2(0.5f, 0.5f);
        labelRect.sizeDelta = new Vector2(200f, 50f);
        var label = HUDText.AddTo(labelGo, TextAnchor.MiddleCenter);

        go.SetActive(false);

        var p = new Popup { go = go, rect = rect, text = text, burstRect = burstRect, burstImage = burstImage, labelText = label };
        popups.Add(p);
        return p;
    }

    private void Deactivate(Popup p)
    {
        p.active = false;
        p.enemy = null;
        p.follow = null;
        p.isLabel = false;
        if (p.go != null) p.go.SetActive(false);
    }

    private void EnsureBuilt()
    {
        if (built) return;

        var canvasGO = new GameObject("DamagePopupCanvas");
        canvasGO.transform.SetParent(transform, false);
        canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = canvasSortingOrder;

        canvasScaler = canvasGO.AddComponent<CanvasScaler>();
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = referenceResolution;

        canvasRect = canvasGO.GetComponent<RectTransform>();
        built = true;
    }

    // PlayerHUDManager / InteractPromptDisplay와 동일한 방식으로 화면 비율에 맞춰 match를 자동 전환합니다.
    private void UpdateCanvasScalerMatch()
    {
        if (canvasScaler == null) return;
        if (referenceResolution.y <= 0f || Screen.height <= 0f) return;

        float screenAspect = (float)Screen.width / Screen.height;
        float referenceAspect = referenceResolution.x / referenceResolution.y;
        canvasScaler.matchWidthOrHeight = screenAspect > referenceAspect ? 1f : 0f;
        canvas.sortingOrder = canvasSortingOrder;
    }
}
