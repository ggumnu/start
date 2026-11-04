using System;
using UnityEngine;

// 보스방 클리어 시 RoomController가 그 자리에 스폰하는 사다리(포탈)입니다.
// 플레이어가 근접한 상태에서 F키를 누르면 "다음 레벨로 이동" 확인을 요청합니다.
//
// [헬기 퇴각 연출 연결 - 2026-10]
// 같은 오브젝트에 HeliExtraction이 붙어 있으면(헬기 퇴각 프리팹):
// - 사다리가 다 내려오기 전(또는 퇴각 연출 중)에는 F 대상/F 안내에서 빠집니다(PlayerInRange가 false).
// - 확인창 [예](ConfirmAdvance) 뒤 바로 다음 레벨로 가지 않고, 퇴각 연출(사다리에 매달려 올라감)이 끝난 뒤 다음 레벨로 갑니다.
// HeliExtraction이 없는 예전 포탈 프리팹은 예전과 100% 같게 동작합니다.
//
// [확인창 UI (별도 작업 예정)]
// 실제 예/아니오 확인창(Canvas/Button)은 아직 만들지 않았습니다. 대신 OnConfirmationRequested
// 이벤트를 노출해뒀으니, 나중에 확인창 UI를 만들면 이 이벤트를 구독해서 팝업을 띄우고
// - "예" 버튼 OnClick -> ConfirmAdvance()
// - "아니오" 버튼 OnClick -> CancelAdvance()
// 를 각각 연결하면 됩니다.
// UI가 아직 연결되지 않은 지금은, F키를 누르면 경고 로그를 남기고 즉시 다음 페이즈로
// 진행하는 폴백 동작으로 테스트할 수 있습니다 (다른 곳들과 동일한 "연결 안 되어있으면
// 기본 동작으로 진행" 패턴입니다).
[RequireComponent(typeof(Collider))]
public class LevelPortal : MonoBehaviour, IMinimapMarker
{
    [Header("참조 (비워두면 자동으로 찾습니다)")]
    public GameFlowManager gameFlowManager;

    [Header("상호작용 설정")]
    public KeyCode interactKey = KeyCode.F;

    private bool playerInRange = false;
    private HeliExtraction heli; // 헬기 퇴각 프리팹이면 있음(없으면 예전 포탈)

    // 지금 F를 받을 수 있는지: 예전 포탈은 항상, 헬기 퇴각은 사다리가 다 내려온 뒤(그리고 퇴각 연출 중이 아닐 때)만.
    private bool Interactable => heli == null || heli.CanStartExtraction;

    // IMinimapMarker 구현 - 이 포탈이 있는 방을 미니맵에서 "포탈 있음"으로 표시하기 위한 키입니다
    // (MinimapDisplay가 이 키를 방 정중앙 아이콘으로 따로 처리합니다).
    public string MinimapIconKey => MinimapKeys.Portal;

    // InteractPromptDisplay(F 안내 중앙 관리 컴포넌트, v1.35 추가)가 "지금 플레이어가 이
    // 포탈의 상호작용 범위 안에 있는지"를 읽을 때 사용합니다. 읽기 전용이며, 이 스크립트
    // 내부 로직에는 영향을 주지 않습니다.
    public bool PlayerInRange => playerInRange && Interactable;

    // 확인창 UI가 구독할 이벤트입니다. F키를 눌렀을 때 호출됩니다.
    public event Action OnConfirmationRequested;

    // 포탈은 보스방 클리어마다 새로 스폰되는 동적 오브젝트라서, 특정 인스턴스를 미리
    // 알 수 없는 전역 UI가 이 정적 이벤트 하나만 구독해두면 어떤 포탈이 나중에
    // 생기든 전부 받을 수 있습니다. 콜백 인자로 어떤 포탈인지(this)를 전달합니다.
    public static event Action<LevelPortal> OnAnyPortalInteractionRequested;

    void Awake()
    {
        if (gameFlowManager == null)
        {
            gameFlowManager = UnityEngine.Object.FindFirstObjectByType<GameFlowManager>();
        }
        heli = GetComponent<HeliExtraction>();
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        playerInRange = true;
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        playerInRange = false;
    }

    void Update()
    {
        if (!PlayerInRange) return;
        // 슬롯 교체 확인창이 떠 있거나 방금 닫힌 프레임이면 F를 무시합니다 - 보스방처럼 보물상자와 포탈이 같이
        // 있을 때, 확인창에서 누른 F(예)가 포탈의 "다음 레벨로 이동" 창까지 여는 것을 막습니다.
        if (SlotReplaceConfirmUI.BlocksInteract) return;
        // F 대상은 범위 안에서 플레이어와 가장 가까운 하나뿐입니다(InteractionTargetSelector) - 근처 상자를 여는
        // F가 포탈에도 전달되지 않도록, 포탈이 지금 F 대상일 때만 처리합니다.
        if (!InteractionTargetSelector.IsCurrentTarget(this)) return;
        if (Input.GetKeyDown(interactKey))
        {
            RequestConfirmation();
        }
    }

    private void RequestConfirmation()
    {
        bool handled = false;

        if (OnConfirmationRequested != null)
        {
            OnConfirmationRequested.Invoke();
            handled = true;
        }

        if (OnAnyPortalInteractionRequested != null)
        {
            OnAnyPortalInteractionRequested.Invoke(this); // 확인창 UI가 구독 중이면 UI가 팝업을 띄웁니다.
            handled = true;
        }

        if (!handled)
        {
            Debug.LogWarning("LevelPortal: 확인창 UI가 아직 연결되어 있지 않아, 테스트를 위해 즉시 다음 레벨로 진행합니다.");
            ConfirmAdvance();
        }
    }

    // 확인창의 "예" 버튼에 연결할 메서드입니다.
    public void ConfirmAdvance()
    {
        // 헬기 퇴각 프리팹이면 퇴각 연출을 먼저 보여주고, 화면이 검어진 뒤 다음 레벨로 갑니다.
        if (heli != null)
        {
            heli.BeginExtraction(AdvanceNow);
            return;
        }
        AdvanceNow();
    }

    private void AdvanceNow()
    {
        if (gameFlowManager == null)
        {
            Debug.LogWarning("LevelPortal: gameFlowManager를 찾지 못해 다음 레벨로 이동할 수 없습니다.");
            return;
        }
        gameFlowManager.AdvanceToNextPhase();
    }

    // 확인창의 "아니오" 버튼에 연결할 메서드입니다. 지금은 아무 동작도 하지 않습니다.
    public void CancelAdvance()
    {
        // 확인창이 생기면, 여기서 팝업을 닫는 처리를 추가하면 됩니다.
    }
}