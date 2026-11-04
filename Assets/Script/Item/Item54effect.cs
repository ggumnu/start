using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 패시브 54번 - "획득 시 5 크레딧(레벨당 최초 1회). 보유하고 있으면 적을 처치할 때 25% 확률로 2 크레딧을 얻는다."
//
// [기획 - 형빈 확정]
// 1) 획득 시 크레딧 creditOnAcquire(기본 5)를 줍니다. "하나의 레벨" 기준 최초 1회입니다(10번/26번/37번과 같은 규칙 -
//    GameFlowManager.TryConsumeOneTimeGrant). 같은 레벨에서 잃었다가 다시 획득하면 효과는 생기지만 크레딧은 주지 않고,
//    다음 레벨에서 다시 획득하면 다시 줍니다.
// 2) 보유 중에 적이 죽을 때마다 procChance(기본 25%) 확률로 크레딧 creditOnProc(기본 2)를 얻습니다. 일반 적/보스 모두,
//    처치 원인과 상관없이 셉니다(47번과 같은 기준). 행운(Luck) 스탯은 아직 정립 전이라 이번에는 영향을 주지 않는 고정
//    확률입니다(형빈 확정 - 확률은 인스펙터에서 조절).
//    30번(크레딧 획득 시 +1)을 함께 들고 있으면 PlayerResources.Add 규칙대로 각각 1씩 더 얻습니다(5 -> 6, 2 -> 3).
// 3) 2)로 크레딧을 얻으면, 그 적이 하얗게 사라지기 시작하는 순간 적 머리 위에 크레딧 아이콘이 "투명 -> 원래 색으로
//    나타나며 살짝 떠오름 -> 잠깐 머묾 -> 투명해지며 아래로 내려가 사라짐" 연출을 보여줍니다(CreditPopupDisplay).
//    확률이 안 터졌을 때는 아무것도 뜨지 않으므로, 아이콘이 크레딧 획득 여부를 알려 줍니다.
//    아이콘 뒤에는 보물상자 아이템 빛과 비슷한 노란 발광 + 흩날리는 빛 입자가 함께 뜹니다(형빈 확정 3번안 - UI 그림으로
//    흉내 냄, CreditPopupDisplay 참고).
//
// [크레딧 아이콘 - 화면 위에 그림 (모델링에 가려지지 않게)]
// 피해 숫자(DamagePopupDisplay)와 같은 방식으로, 맵 위가 아니라 화면 위 UI 전용 캔버스(Screen Space - Overlay)에
// 그립니다. 적이 죽은 자리의 머리 위 월드 좌표를 매 프레임 화면 좌표로 바꿔서 놓기 때문에, 벽/건물/다른 적 모델에
// 가려지지 않습니다. 머리 높이는 죽은 적의 콜라이더(없으면 렌더러) 맨 위를 재서 정합니다.
// 단, 큰 적(보스 등)일 때 아이콘이 하늘 높이 뜨지 않도록 "발밑에서 화면상 최대 maxScreenHeightAboveFeet 픽셀"로
// 제한합니다(화면 픽셀 기준이라 모델 크기/카메라 거리와 상관없이 일정).
//
// [구현 - 기존 스크립트 수정 1곳]
// EnemyHealth에 "죽은 적 자체"를 넘기는 전역 알림 OnAnyEnemyKilled를 추가했습니다(형빈 확정 - 기존 OnAnyEnemyDied는
// 위치만 넘겨서 머리 높이를 알 수 없었음). 이 알림은 적의 Die() 안에서 바로 실행되므로, 여기서 예외가 나면 그 적의
// 사라짐/파괴가 막힙니다. 그래서 처리 전체를 try/catch로 감쌌습니다.
//
// [보유 판정]
// 획득 시 플레이어에 감시 컴포넌트(CreditOnKillRunner)를 붙이고, 그 컴포넌트가 처치 알림을 받습니다. 컴포넌트가 붙어
// 있는 것 자체가 "54번 보유 중"이라, 나중에 아이템을 잃는 기능이 생기면 이 컴포넌트를 떼면 됩니다. 플레이어와 함께
// 다음 레벨로 넘어가고, 새 게임(씬 재시작)에서는 플레이어와 함께 사라져 처치 알림 구독도 자동으로 풀립니다.
//
// [아이템 에셋 설정]
// ItemDefinition: Item Id = 54, Category = Passive, Special Effects에 CreditOnKillEffect 하나.
[System.Serializable]
public class CreditOnKillEffect : IPassiveItemEffect, IRemovablePassiveEffect
{
    [Tooltip("획득할 때 주는 크레딧입니다. 같은 레벨에서는 한 번만 줍니다(잃어버렸다가 다시 획득해도 다시 주지 않음). 단, 48번으로 얻으면 제한 없이 줍니다.")]
    [Min(0)] public int creditOnAcquire = 5;

    [Tooltip("적을 처치할 때 크레딧을 얻을 확률입니다(0.25 = 25%). 행운 스탯과 무관한 고정 확률입니다.")]
    [Range(0f, 1f)] public float procChance = 0.25f;

    [Tooltip("확률이 터졌을 때 얻는 크레딧입니다.")]
    [Min(0)] public int creditOnProc = 2;

    [Tooltip("확률이 터졌을 때 적 머리 위에 띄우는 크레딧 아이콘 연출 설정입니다.")]
    public CreditPopupSettings popup = new CreditPopupSettings();

    public void Apply(PassiveEffectContext context, string sourceItemId)
    {
        if (context == null || context.PlayerObject == null)
        {
            Debug.LogWarning("CreditOnKillEffect: 플레이어를 찾을 수 없어 효과를 적용하지 못했습니다.");
            return;
        }

        GrantCreditOncePerLevel(context, sourceItemId);

        var runner = context.PlayerObject.GetComponent<CreditOnKillRunner>();
        if (runner == null) runner = context.PlayerObject.AddComponent<CreditOnKillRunner>();
        runner.Configure(context.Resources, procChance, creditOnProc, popup);
    }

    // [패시브 잃기 - 48번 등] 처치 시 크레딧 감시 컴포넌트를 즉시 떼어냅니다(같은 프레임에 다시 얻을 수 있어
    // DestroyImmediate). 1회성으로 받은 크레딧은 빼앗지 않습니다(형빈 확정).
    public void Remove(PassiveEffectContext context, string sourceItemId)
    {
        if (context == null || context.PlayerObject == null) return;

        var runner = context.PlayerObject.GetComponent<CreditOnKillRunner>();
        if (runner != null) Object.DestroyImmediate(runner);
    }

    private void GrantCreditOncePerLevel(PassiveEffectContext context, string sourceItemId)
    {
        if (creditOnAcquire <= 0) return;

        if (context.Resources == null)
        {
            Debug.LogWarning("CreditOnKillEffect: PlayerResources를 찾을 수 없어 크레딧을 지급하지 못했습니다.");
            return;
        }

        // 레벨당 1회 게이트입니다(다음 정수 레벨로 넘어갈 때만 초기화). 아이템 테스트 씬처럼 GameFlowManager가 없는
        // 곳에서는 막을 기준이 없으므로 경고만 남기고 지급합니다(10번/26번/37번과 같은 처리).
        // [48번 연동, 형빈 확정] 48번으로 얻을 때는(PassiveEffectContext.IgnoreOneTimeGrantLimit) 레벨당 1회 제한 없이 지급합니다.
        if (!context.IgnoreOneTimeGrantLimit)
        {
            var gameFlow = Object.FindFirstObjectByType<GameFlowManager>();
            if (gameFlow != null)
            {
                if (!gameFlow.TryConsumeOneTimeGrant(sourceItemId + "_credit_grant")) return;
            }
            else
            {
                Debug.LogWarning("CreditOnKillEffect: GameFlowManager를 찾지 못해 레벨당 1회 제한 없이 크레딧을 지급합니다.");
            }
        }

        context.Resources.Add(BasicItemType.Credit, creditOnAcquire);
    }
}

// 크레딧 아이콘 연출 설정입니다(54번 아이템 에셋의 Special Effects 안에서 조절). 위치/거리는 1920x1080 기준 캔버스 픽셀입니다.
[System.Serializable]
public class CreditPopupSettings
{
    [Tooltip("띄울 크레딧 아이콘입니다. 비워두면 HUD 자원 표시(PlayerResourceDisplay)의 Credit Icon을 자동으로 씁니다.")]
    public Sprite icon;

    [Tooltip("아이콘 크기(캔버스 픽셀)입니다.")]
    [Min(1f)] public float iconSize = 48f;

    [Tooltip("죽은 적의 자동 계산된 머리 높이에 더할 여유 높이(월드 단위)입니다.")]
    public float headroom = 0.15f;

    [Tooltip("아이콘이 뜨는 위치를 발밑에서 화면상 최대 이 픽셀까지만 올립니다(보스처럼 큰 적일 때 하늘 높이 뜨는 것을 막음).")]
    [Min(0f)] public float maxScreenHeightAboveFeet = 140f;

    [Tooltip("나타나면서 떠오르는 거리(캔버스 픽셀)입니다.")]
    public float riseDistance = 30f;

    [Tooltip("사라지면서 시작 위치보다 아래로 내려가는 거리(캔버스 픽셀)입니다.")]
    public float sinkDistance = 25f;

    [Tooltip("투명 -> 원래 색으로 나타나며 떠오르는 시간(초)입니다.")]
    [Min(0.01f)] public float appearDuration = 0.25f;

    [Tooltip("다 떠오른 뒤 머무는 시간(초)입니다.")]
    [Min(0f)] public float holdDuration = 0.35f;

    [Tooltip("투명해지며 아래로 내려가 사라지는 시간(초)입니다.")]
    [Min(0.01f)] public float vanishDuration = 0.35f;

    [Tooltip("다른 HUD와의 앞뒤 순서입니다(피해 숫자와 같은 8 - 숫자가 클수록 위에 그려짐).")]
    public int canvasSortingOrder = 8;

    [Header("아이콘 뒤 발광 (원형 빛)")]
    [Tooltip("끄면 발광 없이 아이콘만 뜹니다.")]
    public bool enableGlow = true;

    [Tooltip("발광 그림입니다. 비워두면 코드가 만든 부드러운 원형 빛을 씁니다. vfx_Projectile_01 등의 빛 텍스처(스프라이트)를 넣어도 됩니다.")]
    public Sprite glowSprite;

    [Tooltip("발광 색입니다(알파로 진하기 조절). 기본은 노랑입니다.")]
    public Color glowColor = new Color(1f, 0.85f, 0.25f, 0.85f);

    [Tooltip("발광 크기 = 아이콘 크기 x 이 배율입니다.")]
    [Min(0.1f)] public float glowSizeMultiplier = 2.2f;

    [Tooltip("발광이 맥박치듯 커졌다 작아지는 정도입니다(0.08 = ±8%). 0이면 맥박 없음.")]
    [Min(0f)] public float glowPulseAmount = 0.08f;

    [Tooltip("맥박 속도입니다(클수록 빠름).")]
    [Min(0f)] public float glowPulseSpeed = 8f;

    [Header("흩날리는 빛 입자")]
    [Tooltip("끄면 빛 입자 없이 발광과 아이콘만 뜹니다.")]
    public bool enableSparkles = true;

    [Tooltip("입자 그림입니다. 비워두면 코드가 만든 작은 빛 점을 씁니다. vfx_Projectile_01의 입자 텍스처를 넣어도 됩니다.")]
    public Sprite sparkleSprite;

    [Tooltip("입자 색입니다(알파로 진하기 조절).")]
    public Color sparkleColor = new Color(1f, 0.92f, 0.45f, 1f);

    [Tooltip("한 번에 튀어 나가는 입자 수입니다.")]
    [Min(0)] public int sparkleCount = 8;

    [Tooltip("입자 크기 범위(캔버스 픽셀)입니다. X=최소, Y=최대.")]
    public Vector2 sparkleSizeRange = new Vector2(6f, 14f);

    [Tooltip("입자가 튀어 나가는 속도 범위(초당 캔버스 픽셀)입니다. X=최소, Y=최대.")]
    public Vector2 sparkleSpeedRange = new Vector2(40f, 90f);

    [Tooltip("입자가 보이는 시간 범위(초)입니다. X=최소, Y=최대.")]
    public Vector2 sparkleLifetimeRange = new Vector2(0.35f, 0.6f);

    [Tooltip("입자들이 연출 시작 후 이 시간(초) 안에 하나씩 나타납니다(0이면 한꺼번에).")]
    [Min(0f)] public float sparkleSpawnWindow = 0.25f;

    [Tooltip("입자가 위로 떠오르는 힘(초당 속도 증가량, 캔버스 픽셀)입니다. 음수면 아래로 떨어집니다.")]
    public float sparkleRiseAcceleration = 80f;
}

// 54번 효과의 처치 감시 컴포넌트입니다. CreditOnKillEffect.Apply()가 플레이어에 자동으로 붙이므로 직접 붙일 필요가 없습니다.
public class CreditOnKillRunner : MonoBehaviour
{
    private PlayerResources resources;
    private float procChance;
    private int creditOnProc;
    private CreditPopupSettings popup;

    public void Configure(PlayerResources playerResources, float chance, int credit, CreditPopupSettings popupSettings)
    {
        resources = playerResources != null ? playerResources : GetComponent<PlayerResources>();
        procChance = chance;
        creditOnProc = credit;
        popup = popupSettings;
    }

    void OnEnable()
    {
        EnemyHealth.OnAnyEnemyKilled += HandleEnemyKilled;
    }

    void OnDisable()
    {
        EnemyHealth.OnAnyEnemyKilled -= HandleEnemyKilled;
    }

    // 적이 죽는 순간(Die() 안)에 호출됩니다. 여기서 예외가 나면 그 적의 사라짐/파괴가 막히므로 전부 try/catch로 감쌉니다.
    private void HandleEnemyKilled(EnemyHealth enemy, Vector3 deathPosition)
    {
        try
        {
            if (creditOnProc <= 0 || procChance <= 0f) return;
            if (Random.value >= procChance) return;

            if (resources == null) resources = GetComponent<PlayerResources>();
            if (resources == null) return;

            resources.Add(BasicItemType.Credit, creditOnProc);
            CreditPopupDisplay.Show(enemy, deathPosition, popup);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"CreditOnKillRunner: 적 처치 크레딧 처리 중 오류가 발생해 건너뜁니다 (적 처치 자체에는 영향 없음): {e}");
        }
    }
}

// 적 머리 위 크레딧 아이콘 연출을 그리는 화면 UI입니다. 처음 필요할 때 씬에 자동으로 만들어지므로 따로 배치할 필요가 없고,
// 씬이 바뀌면 씬과 함께 사라집니다. 피해 숫자(DamagePopupDisplay)와 같은 Screen Space - Overlay 캔버스 방식이라 모델에
// 가려지지 않습니다. 아이콘은 적이 죽은 자리에 고정되어(적은 곧 사라지므로) 카메라가 움직여도 그 자리 위에 보입니다.
//
// [아이콘 뒤 발광 - 형빈 확정 3번안]
// 보물상자 아이템 아이콘 뒤 빛(vfx_LootDrop/vfx_Projectile_01 파티클)과 비슷한 느낌을 UI 그림으로 흉내 냅니다(파티클은
// 화면 UI 캔버스에 그릴 수 없어서). 한 연출은 "발광(원형 빛) -> 흩날리는 빛 입자들 -> 크레딧 아이콘" 순서로 겹쳐 그려서
// 아이콘이 항상 맨 위, 발광과 입자는 그 뒤에 보입니다. 셋 다 아이콘과 같이 나타나고/떠오르고/사라집니다.
// - 발광: glowSprite(비우면 코드가 만든 부드러운 원형 빛)를 glowColor로 물들여 아이콘 뒤에 깔고, 살짝 맥박치듯 커졌다
//   작아집니다.
// - 빛 입자: 나타날 때 sparkleCount개가 아이콘 주변에서 사방으로 튀어 나가며 위로 떠오르다 투명해집니다
//   (sparkleSprite - 비우면 코드가 만든 작은 빛 점). vfx_Projectile_01의 빛 텍스처를 그대로 넣어 써도 됩니다.
//
// [머리 위 알림 줄 세우기 - 형빈 확정]
// - 대상을 따라다니는 연출(followTarget = true - 플레이어 머리 위: 72번 회복/코스트, 103/106/111/39번, 일회용/음식 사용 등)은 같은 대상끼리
//   줄을 서서 하나씩 나옵니다. 앞 연출이 "사라지기 시작할 때"(나타남 + 머무름이 끝났을 때) 다음 연출이 떠오르기 시작합니다(B안).
//   적 머리 위 크레딧(54번)처럼 따라다니지 않는 연출은 줄을 서지 않고 바로 나옵니다(기존 그대로).
// - 아이템 획득 연출(PlayerItemPickupPresenter)과의 순서: 알림이 우선입니다. 알림이 줄 서 있거나 떠오르는 중에 아이템을 얻으면 획득 연출이
//   알림 뒤로 기다립니다(IsBusyFor). 이미 획득 연출이 진행 중일 때 생긴 알림은 그 획득 연출이 끝난 뒤에 나옵니다(동작을 끊지 않음).
// - 다른 방에 들어가면 그 전에 생긴 대상 따라다니는 연출은 줄 서 있던 것까지 모두 건너뜁니다(획득 연출처럼).
// - 연출과 함께 나와야 하는 이펙트(103/106/111/39번 핑크 이펙트 등)는 Show의 onStarted로 넘깁니다 - 아이콘이 실제로 떠오르는 순간 불립니다.
public class CreditPopupDisplay : MonoBehaviour
{
    private static CreditPopupDisplay instance;

    // 코드가 만든 기본 빛 그림(발광용 넓은 원 / 입자용 작은 점)입니다. 한 번만 만들어 재사용합니다.
    private static Sprite defaultGlowSprite;
    private static Sprite defaultSparkleSprite;

    private class Sparkle
    {
        public RectTransform rect;
        public Image image;
        public Vector2 position;   // 연출 묶음 기준 위치(캔버스 픽셀)
        public Vector2 velocity;   // 초당 이동량(캔버스 픽셀)
        public float delay;        // 연출 시작 후 나타나기까지 기다리는 시간(초)
        public float lifetime;     // 나타난 뒤 사라지기까지의 시간(초)
        public float size;         // 처음 크기(캔버스 픽셀)
    }

    private class Popup
    {
        public GameObject go;          // 연출 묶음(발광 + 입자 + 아이콘)의 부모
        public RectTransform rect;
        public Image icon;
        public RectTransform iconRect;
        public Image glow;
        public RectTransform glowRect;
        public readonly List<Sparkle> sparkles = new List<Sparkle>();
        public Vector3 feetWorld;
        public float headHeight;
        public CreditPopupSettings settings;
        public float age;
        public Transform followTarget; // [범용화 - 72번] 있으면 매 프레임 이 대상의 위치(발밑)를 따라다닙니다.
        public int createdFrame;       // 만든 프레임(방 이동 시 건너뛰기 판정용)
    }

    // [줄 세우기] 아직 시작하지 않은 대상 따라다니는 연출입니다(먼저 들어온 것부터).
    private class Pending
    {
        public GameObject target;
        public Vector3 feetPosition;
        public CreditPopupSettings settings;
        public Sprite fallbackIcon;
        public Transform followTarget;
        public System.Action onStarted;
        public int queuedFrame;
    }

    private readonly List<Popup> popups = new List<Popup>();
    private readonly List<Pending> queue = new List<Pending>();
    private Canvas canvas;
    private CanvasScaler canvasScaler;
    private RectTransform canvasRect;
    private Sprite cachedHudCreditIcon;

    private static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

    // enemy의 머리 위(deathPosition 기준)에 크레딧 아이콘 연출을 하나 띄웁니다(54번용 - 기존 그대로).
    public static void Show(EnemyHealth enemy, Vector3 deathPosition, CreditPopupSettings settings)
    {
        EnsureInstance();
        instance.Spawn(enemy != null ? enemy.gameObject : null, deathPosition, settings, instance.GetHudCreditIcon(), null);
    }

    // [범용화 - 72번 때 추가, 형빈 확정] 아무 대상(target)의 머리 위에 아이콘 연출을 하나 띄웁니다. 54번 크레딧 전용이 아니라
    // 어디서든 쓸 수 있습니다(아이콘/발광 색/입자 색은 settings로 지정).
    // - feetPosition: 연출 기준이 되는 발밑 위치입니다. 머리 높이는 target의 콜라이더(트리거 제외, 없으면 렌더러) 맨 위로 잽니다.
    // - fallbackIcon: settings.icon이 비어 있을 때 쓸 아이콘입니다(예: HUD의 하트/코스트 아이콘). 둘 다 없으면 연출을 건너뜁니다.
    // - followTarget: true면 연출이 끝날 때까지 target을 따라다닙니다(예: 플레이어 머리 위). false면 feetPosition에 고정됩니다.
    //   true면 같은 대상의 다른 연출과 줄을 섭니다(클래스 위 [머리 위 알림 줄 세우기] 참고).
    // - onStarted: 아이콘이 실제로 떠오르기 시작하는 순간 한 번 불립니다(줄을 서면 그만큼 늦게). 함께 나올 이펙트를 여기서 시작하세요.
    //   방 이동으로 건너뛴 연출은 불리지 않습니다.
    public static void Show(GameObject target, Vector3 feetPosition, CreditPopupSettings settings, Sprite fallbackIcon, bool followTarget,
                            System.Action onStarted = null)
    {
        EnsureInstance();
        Transform follow = (followTarget && target != null) ? target.transform : null;

        if (follow == null)
        {
            instance.Spawn(target, feetPosition, settings, fallbackIcon, null);
            InvokeStarted(onStarted);
            return;
        }

        instance.queue.Add(new Pending
        {
            target = target,
            feetPosition = feetPosition,
            settings = settings,
            fallbackIcon = fallbackIcon,
            followTarget = follow,
            onStarted = onStarted,
            queuedFrame = Time.frameCount,
        });
        instance.TryStartQueued(); // 비어 있으면 바로 시작(기존과 같은 프레임)
    }

    // target 머리 위 알림이 줄 서 있거나, 지금 떠오르는/머무는 중이면 true입니다(사라지는 중이면 false - 다음 것이 이어 나와도 됨).
    // 아이템 획득 연출이 알림 뒤로 기다릴 때 씁니다.
    public static bool IsBusyFor(GameObject target)
    {
        if (instance == null || target == null) return false;
        Transform t = target.transform;
        foreach (var q in instance.queue)
        {
            if (q.followTarget == t) return true;
        }
        return instance.IsShowingFor(t);
    }

    private static void InvokeStarted(System.Action onStarted)
    {
        if (onStarted == null) return;
        try
        {
            onStarted();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"CreditPopupDisplay: 연출 시작 콜백 중 오류가 발생해 건너뜁니다: {e}");
        }
    }

    // 이 대상을 따라다니는 연출 중 아직 사라지기 전(나타남/머무름) 단계인 것이 있는지입니다.
    private bool IsShowingFor(Transform follow)
    {
        foreach (var p in popups)
        {
            if (p.go == null || p.followTarget != follow) continue;
            if (p.age < p.settings.appearDuration + p.settings.holdDuration) return true;
        }
        return false;
    }

    // 줄 맨 앞부터, 대상마다 하나씩 시작할 수 있으면 시작합니다.
    private void TryStartQueued()
    {
        if (queue.Count == 0) return;

        List<Transform> blocked = null;
        for (int i = 0; i < queue.Count; i++)
        {
            var q = queue[i];
            if (q.followTarget == null) { queue.RemoveAt(i); i--; continue; } // 대상이 사라짐

            if (blocked != null && blocked.Contains(q.followTarget)) continue; // 같은 대상의 앞 순서가 아직 대기 중

            bool free = !IsShowingFor(q.followTarget) && !IsPickupPresenting(q.followTarget);
            if (!free)
            {
                if (blocked == null) blocked = new List<Transform>();
                blocked.Add(q.followTarget);
                continue;
            }

            queue.RemoveAt(i);
            i--;
            Spawn(q.target, q.feetPosition, q.settings, q.fallbackIcon, q.followTarget);
            InvokeStarted(q.onStarted);

            // 방금 시작한 것이 아이콘 없이 건너뛰어졌을 수도 있으니, 같은 대상의 다음 것은 다음 확인 때 판단합니다.
            if (blocked == null) blocked = new List<Transform>();
            blocked.Add(q.followTarget);
        }
    }

    // 이 대상(플레이어)이 아이템 획득 연출 중이면 true - 그 동작을 끊지 않도록 알림은 끝날 때까지 기다립니다.
    private static bool IsPickupPresenting(Transform follow)
    {
        if (follow == null) return false;
        var presenter = follow.GetComponent<PlayerItemPickupPresenter>();
        return presenter != null && presenter.isActiveAndEnabled && presenter.IsPresenting;
    }

    void OnEnable()
    {
        RoomController.OnAnyRoomEntered += HandleRoomEntered;
    }

    void OnDisable()
    {
        RoomController.OnAnyRoomEntered -= HandleRoomEntered;
    }

    // 다른 방에 들어가면, 이 프레임 이전에 생긴 대상 따라다니는 연출을 줄 서 있던 것까지 모두 건너뜁니다(이번 프레임에 방 진입으로
    // 새로 생긴 연출은 남깁니다 - 알림 순서와 상관없이).
    private void HandleRoomEntered(RoomInstance room)
    {
        try
        {
            int frame = Time.frameCount;
            queue.RemoveAll(q => q.queuedFrame < frame);

            for (int i = popups.Count - 1; i >= 0; i--)
            {
                var p = popups[i];
                if (p.followTarget == null || p.createdFrame >= frame) continue;
                if (p.go != null) Destroy(p.go);
                popups.RemoveAt(i);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"CreditPopupDisplay: 방 이동 시 연출 정리 중 오류가 발생해 건너뜁니다(방 이동에는 영향 없음): {e}");
        }
    }

    private static void EnsureInstance()
    {
        if (instance == null)
        {
            var go = new GameObject("CreditPopupDisplay");
            instance = go.AddComponent<CreditPopupDisplay>();
        }
    }

    private void Spawn(GameObject target, Vector3 deathPosition, CreditPopupSettings settings, Sprite fallbackIcon, Transform followTarget)
    {
        if (settings == null) settings = new CreditPopupSettings();

        Sprite iconSprite = settings.icon != null ? settings.icon : fallbackIcon;
        if (iconSprite == null)
        {
            Debug.LogWarning("CreditPopupDisplay: 띄울 아이콘이 없어 연출을 건너뜁니다. 해당 아이템의 Popup > Icon(또는 HUD의 해당 아이콘)을 넣어주세요.");
            return;
        }

        EnsureCanvas(settings.canvasSortingOrder);

        // 연출 묶음의 부모입니다. 아래 가운데가 머리 위 지점에 오고, 크기는 아이콘 크기와 같습니다(자식들은 이 사각형의
        // 가운데 = 아이콘 가운데를 기준으로 놓입니다).
        var root = new GameObject("CreditPopup");
        root.transform.SetParent(canvasRect, false);
        var rootRect = root.AddComponent<RectTransform>();
        rootRect.anchorMin = rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        rootRect.pivot = new Vector2(0.5f, 0f);
        rootRect.sizeDelta = new Vector2(settings.iconSize, settings.iconSize);

        var popup = new Popup
        {
            go = root,
            rect = rootRect,
            feetWorld = deathPosition,
            headHeight = MeasureHeadHeight(target, deathPosition.y),
            settings = settings,
            age = 0f,
            followTarget = followTarget,
            createdFrame = Time.frameCount,
        };

        // 그리는 순서 = 만드는 순서입니다(먼저 만든 것이 뒤에 깔림): 발광 -> 입자 -> 아이콘.
        if (settings.enableGlow)
        {
            Sprite glowSprite = settings.glowSprite != null ? settings.glowSprite : GetDefaultGlowSprite();
            popup.glow = CreateImage(rootRect, "Glow", glowSprite, out popup.glowRect);
        }

        if (settings.enableSparkles && settings.sparkleCount > 0)
        {
            Sprite sparkleSprite = settings.sparkleSprite != null ? settings.sparkleSprite : GetDefaultSparkleSprite();
            for (int i = 0; i < settings.sparkleCount; i++)
            {
                popup.sparkles.Add(CreateSparkle(rootRect, sparkleSprite, settings));
            }
        }

        popup.icon = CreateImage(rootRect, "Icon", iconSprite, out popup.iconRect);

        popups.Add(popup);
        UpdatePopup(popup); // 생성된 프레임부터 올바른 자리에 놓습니다(투명 상태).
    }

    private static Image CreateImage(RectTransform parent, string name, Sprite sprite, out RectTransform rect)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        rect = go.AddComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;

        var image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.color = new Color(1f, 1f, 1f, 0f);
        return image;
    }

    private static Sparkle CreateSparkle(RectTransform parent, Sprite sprite, CreditPopupSettings s)
    {
        var sparkle = new Sparkle();
        sparkle.image = CreateImage(parent, "Sparkle", sprite, out sparkle.rect);

        // 아이콘 가운데 근처에서 사방으로 튀어 나갑니다(위쪽으로 조금 더 치우침).
        float angle = Random.Range(0f, Mathf.PI * 2f);
        Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        float spawnRadius = s.iconSize * 0.25f * Random.value;
        float speed = Random.Range(Mathf.Min(s.sparkleSpeedRange.x, s.sparkleSpeedRange.y), Mathf.Max(s.sparkleSpeedRange.x, s.sparkleSpeedRange.y));

        sparkle.position = dir * spawnRadius;
        sparkle.velocity = dir * speed;
        sparkle.delay = Random.Range(0f, Mathf.Max(0f, s.sparkleSpawnWindow));
        sparkle.lifetime = Random.Range(Mathf.Min(s.sparkleLifetimeRange.x, s.sparkleLifetimeRange.y), Mathf.Max(s.sparkleLifetimeRange.x, s.sparkleLifetimeRange.y));
        sparkle.lifetime = Mathf.Max(0.05f, sparkle.lifetime);
        sparkle.size = Random.Range(Mathf.Min(s.sparkleSizeRange.x, s.sparkleSizeRange.y), Mathf.Max(s.sparkleSizeRange.x, s.sparkleSizeRange.y));
        sparkle.rect.sizeDelta = new Vector2(sparkle.size, sparkle.size);
        return sparkle;
    }

    void Update()
    {
        for (int i = popups.Count - 1; i >= 0; i--)
        {
            var p = popups[i];
            if (p.go == null) { popups.RemoveAt(i); continue; }

            p.age += Time.deltaTime;
            float total = p.settings.appearDuration + p.settings.holdDuration + p.settings.vanishDuration;
            if (p.age >= total)
            {
                Destroy(p.go);
                popups.RemoveAt(i);
                continue;
            }

            UpdatePopup(p);
        }

        TryStartQueued();
    }

    private void UpdatePopup(Popup p)
    {
        Camera cam = Camera.main;
        if (cam == null || canvasRect == null) return;

        var s = p.settings;
        UpdateCanvasScalerMatch(s.canvasSortingOrder);

        // [범용화 - 72번] 따라다니는 대상이 있으면 그 위치를 발밑으로 삼습니다(대상이 사라지면 마지막 위치에 머묾).
        if (p.followTarget != null) p.feetWorld = p.followTarget.position;

        Vector3 feetScreen = cam.WorldToScreenPoint(p.feetWorld);
        Vector3 headScreen = cam.WorldToScreenPoint(p.feetWorld + Vector3.up * (p.headHeight + s.headroom));
        if (feetScreen.z < 0f || headScreen.z < 0f)
        {
            if (p.go.activeSelf) p.go.SetActive(false);
            return;
        }
        if (!p.go.activeSelf) p.go.SetActive(true);

        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, feetScreen, null, out Vector2 feetLocal);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, headScreen, null, out Vector2 headLocal);

        // 큰 적이라도 발밑에서 화면상 maxScreenHeightAboveFeet 픽셀보다 높이 뜨지 않게 제한합니다.
        Vector2 basePos = headLocal;
        if (basePos.y - feetLocal.y > s.maxScreenHeightAboveFeet)
        {
            basePos.y = feetLocal.y + s.maxScreenHeightAboveFeet;
        }

        float alpha;
        float offsetY;
        float appear = Mathf.Max(0.01f, s.appearDuration);
        float hold = Mathf.Max(0f, s.holdDuration);
        float vanish = Mathf.Max(0.01f, s.vanishDuration);

        if (p.age < appear)
        {
            // 나타남: 투명 -> 원래 색, 빠르게 시작해 부드럽게 멈추며 떠오름(ease-out)
            float t = p.age / appear;
            float eased = 1f - (1f - t) * (1f - t);
            alpha = eased;
            offsetY = s.riseDistance * eased;
        }
        else if (p.age < appear + hold)
        {
            alpha = 1f;
            offsetY = s.riseDistance;
        }
        else
        {
            // 사라짐: 원래 색 -> 투명, 천천히 시작해 점점 빠르게 아래로(ease-in)
            float t = Mathf.Clamp01((p.age - appear - hold) / vanish);
            float eased = t * t;
            alpha = 1f - t;
            offsetY = Mathf.Lerp(s.riseDistance, -s.sinkDistance, eased);
        }

        p.rect.anchoredPosition = basePos + new Vector2(0f, offsetY);
        p.rect.sizeDelta = new Vector2(s.iconSize, s.iconSize);

        p.iconRect.sizeDelta = new Vector2(s.iconSize, s.iconSize);
        p.icon.color = new Color(1f, 1f, 1f, alpha);

        UpdateGlow(p, alpha);
        UpdateSparkles(p, alpha);
    }

    // 발광: 아이콘 크기 x glowSizeMultiplier에서 살짝 맥박치듯 커졌다 작아지고, 투명도는 아이콘을 따라갑니다.
    private static void UpdateGlow(Popup p, float alpha)
    {
        if (p.glow == null) return;
        var s = p.settings;

        float pulse = 1f + s.glowPulseAmount * Mathf.Sin(p.age * s.glowPulseSpeed);
        float size = s.iconSize * s.glowSizeMultiplier * pulse;
        p.glowRect.sizeDelta = new Vector2(size, size);

        Color c = s.glowColor;
        c.a *= alpha;
        p.glow.color = c;
    }

    // 빛 입자: 나타날 차례(delay)가 되면 튀어 나가며 위로 떠오르고(sparkleRiseAcceleration), 수명 동안 작아지며 투명해집니다.
    // 연출 전체 투명도(alpha)도 함께 곱해서, 연출이 사라질 때 남은 입자도 같이 사라집니다.
    private static void UpdateSparkles(Popup p, float alpha)
    {
        if (p.sparkles.Count == 0) return;
        var s = p.settings;
        float dt = Time.deltaTime;

        for (int i = 0; i < p.sparkles.Count; i++)
        {
            var sp = p.sparkles[i];
            float localAge = p.age - sp.delay;

            if (localAge <= 0f || localAge >= sp.lifetime)
            {
                sp.image.color = new Color(s.sparkleColor.r, s.sparkleColor.g, s.sparkleColor.b, 0f);
                continue;
            }

            sp.velocity.y += s.sparkleRiseAcceleration * dt;
            sp.position += sp.velocity * dt;

            float life = localAge / sp.lifetime;
            float fade = 1f - life * life;           // 처음엔 또렷하다가 끝으로 갈수록 빠르게 사라짐
            float scale = Mathf.Lerp(1f, 0.4f, life); // 조금씩 작아짐

            sp.rect.anchoredPosition = sp.position;
            sp.rect.sizeDelta = new Vector2(sp.size * scale, sp.size * scale);

            Color c = s.sparkleColor;
            c.a *= fade * alpha;
            sp.image.color = c;
        }
    }

    // 죽은 적의 콜라이더(트리거 제외, 없으면 렌더러) 맨 위가 사망 위치보다 얼마나 높은지 잽니다(피해 숫자와 같은 방식).
    // 이 알림은 사라짐 연출이 시작되기 전에 오므로 콜라이더/렌더러가 아직 그대로입니다.
    // [범용화 - 72번] 적뿐 아니라 아무 대상(플레이어 등)에도 쓰도록 GameObject를 받습니다(재는 방식은 그대로).
    private static float MeasureHeadHeight(GameObject enemy, float baseY)
    {
        if (enemy == null) return 1f;

        float top = float.NegativeInfinity;
        foreach (var col in enemy.GetComponentsInChildren<Collider>())
        {
            if (col == null || col.isTrigger) continue;
            top = Mathf.Max(top, col.bounds.max.y);
        }

        if (float.IsNegativeInfinity(top))
        {
            foreach (var rend in enemy.GetComponentsInChildren<Renderer>())
            {
                if (rend == null) continue;
                top = Mathf.Max(top, rend.bounds.max.y);
            }
        }

        if (float.IsNegativeInfinity(top)) return 1f; // 재지 못하면 대략적인 기본 높이
        return Mathf.Max(0f, top - baseY);
    }

    private Sprite GetHudCreditIcon()
    {
        if (cachedHudCreditIcon != null) return cachedHudCreditIcon;
        var hud = FindFirstObjectByType<PlayerResourceDisplay>();
        if (hud != null) cachedHudCreditIcon = hud.creditIcon;
        return cachedHudCreditIcon;
    }

    // 발광용 기본 그림: 가운데가 가장 밝고 바깥으로 부드럽게 사라지는 넓은 원입니다(흰색 - 색은 glowColor로 입힘).
    private static Sprite GetDefaultGlowSprite()
    {
        if (defaultGlowSprite == null) defaultGlowSprite = CreateRadialSprite("CreditGlow_Default", 128, 2f);
        return defaultGlowSprite;
    }

    // 입자용 기본 그림: 가운데가 또렷하고 가장자리가 빠르게 사라지는 작은 빛 점입니다.
    private static Sprite GetDefaultSparkleSprite()
    {
        if (defaultSparkleSprite == null) defaultSparkleSprite = CreateRadialSprite("CreditSparkle_Default", 32, 3f);
        return defaultSparkleSprite;
    }

    // 가운데에서 바깥으로 투명해지는 흰색 원 그림을 만듭니다. falloff가 클수록 가장자리가 빨리 사라집니다.
    private static Sprite CreateRadialSprite(string name, int size, float falloff)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.name = name;
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        var pixels = new Color32[size * size];
        float center = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - center) / center;
                float dy = (y - center) / center;
                float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                float a = Mathf.Pow(1f - d, falloff);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private void EnsureCanvas(int sortingOrder)
    {
        if (canvasRect != null) return;

        var canvasGO = new GameObject("CreditPopupCanvas");
        canvasGO.transform.SetParent(transform, false);
        canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        canvasScaler = canvasGO.AddComponent<CanvasScaler>();
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = ReferenceResolution;

        canvasRect = canvasGO.GetComponent<RectTransform>();
        UpdateCanvasScalerMatch(sortingOrder);
    }

    // 피해 숫자/다른 HUD와 같은 방식으로 화면 비율에 맞춰 기준 축을 자동 전환합니다.
    private void UpdateCanvasScalerMatch(int sortingOrder)
    {
        if (canvasScaler == null || Screen.height <= 0) return;
        float screenAspect = (float)Screen.width / Screen.height;
        float referenceAspect = ReferenceResolution.x / ReferenceResolution.y;
        canvasScaler.matchWidthOrHeight = screenAspect > referenceAspect ? 1f : 0f;
        canvas.sortingOrder = sortingOrder;
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}
