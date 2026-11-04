// "이번 좌클릭은 HUD 버튼이 쓴다"는 신호입니다. HUD 접기 버튼(CollapsibleHUDPanel) 위에서 누른 좌클릭이
// 총 사격(PlayerAimAndAttack)/수류탄 투척(PlayerGrenadeThrower)/해킹툴 전개(PlayerHackTool)로 같이 나가지
// 않도록, 세 공격 스크립트가 좌클릭을 처리하기 전에 이 값을 확인합니다(PlayerItemPickupPresenter.BlocksActions와
// 같은 정적 신호 패턴).
//
// 상태를 저장하지 않고 "지금 마우스가 HUD 버튼 위에 있는가"를 그때그때 계산하므로, 스크립트 실행 순서
// (HUD의 Update가 먼저냐 플레이어의 Update가 먼저냐)와 무관하게 항상 같은 결과가 나옵니다.
// 나중에 다른 클릭형 HUD 버튼이 생기면 여기에 조건만 추가하면 됩니다.
// - 미니맵 구역(MinimapDisplay): 구역 안 좌클릭은 미니맵 드래그용이라 공격하지 않습니다.
// - 슬롯 교체 확인창(SlotReplaceConfirmUI): 창이 떠 있는 동안(+ 닫힌 그 프레임)에는 화면 어디를 눌러도 공격하지 않습니다.
// - 상점 창(ShopUI): 위와 같습니다.
// - 슬롯 아이템 버리기 창(SlotItemDropUI): 위와 같습니다.
// - 다음 레벨 이동 확인창(LevelTransitionConfirmUI): 위와 같습니다.
// - 게임 중단 화면(PauseMenuUI): 위와 같습니다.
// - 패시브 교체 창(PassiveRerollUI - 56번): 위와 같습니다.
public static class HUDClickBlocker
{
    public static bool BlocksLeftClick =>
        CollapsibleHUDPanel.IsPointerOverAnyButton() ||
        MinimapDisplay.IsPointerOverZone() ||
        SlotReplaceConfirmUI.BlocksInteract ||
        ShopUI.BlocksInteract ||
        SlotItemDropUI.BlocksInteract ||
        LevelTransitionConfirmUI.BlocksInteract ||
        PauseMenuUI.BlocksInteract ||
        PassiveRerollUI.BlocksInteract;
}
