using System.Collections.Generic;

// TreasureChestInfoDisplay.cs가 "지금 플레이어 근처에 아직 남은 기본 아이템(체력/수류탄/
// 해킹툴/크레딧/청휘석/코스트)이 있는 컨테이너가 있는지"를 찾을 때 쓰는 최소 읽기 전용
// 인터페이스입니다.
//
// [왜 필요한가 - v1.30]
// 지금까지 "기본 아이템 여러 개"는 TreasureChest(일반맵/보스방 보물상자)에서만 나올 수
// 있었지만, v1.30부터는 ItemPedestal(동아리방 보물상자 - 열었는데 뽑을 아이템이 하나도 없는
// 경우의 폴백)에서도 나올 수 있게 됐습니다. ItemPedestal과 TreasureChest는 서로 다른 클래스라(상호작용
// 판정 방식 자체가 다릅니다 - ItemPedestal은 물리적 장애물+트리거 Enter/Exit, TreasureChest는 통과 가능+
// 거리 기반), 두 클래스를 하나로 합치는 대신 "기본 아이템 UI 표시에 필요한 최소한의
// 정보"만 이 인터페이스로 뽑아내서 TreasureChest/ItemPedestal 둘 다 구현하게 했습니다.
// TreasureChestInfoDisplay는 이제 이 인터페이스 하나만 보고 두 종류를 동일하게 취급합니다.
public interface IBasicItemContainer
{
    bool PlayerInRange { get; }
    IReadOnlyList<ResolvedBasicItemStack> BasicStacks { get; }
}
