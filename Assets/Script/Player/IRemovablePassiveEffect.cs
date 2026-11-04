// [패시브 아이템 "잃기" - 제거 약속]
// IPassiveItemEffect는 "얻으면 영원히 유지"를 전제로 만들어져 Apply()만 있습니다. 패시브를 잃는 기능(48번 - 보유 패시브
// 전부 랜덤 교체, 나중에 생길 다른 "아이템 잃기" 효과)을 위해, 소지하는 동안만 유지되는 효과를 가진 패시브 효과는 이
// 인터페이스를 함께 구현해 Remove()에서 그 효과를 되돌립니다. PlayerInventory.RemoveItem/ReplaceItems가 호출합니다.
//
// [잃을 때 무엇을 되돌리나 - 형빈 확정]
// 1) 기본 스탯(ItemDefinition.Stat Modifiers): 최대 체력을 뺀 나머지는 사라집니다. 최대 체력은 남습니다(48번 사용만으로
//    체력이 사라져 사망하는 일을 막기 위함). 이건 효과가 아니라 PlayerInventory가 직접 처리하므로 Remove에서 하지 않습니다.
// 2) 특수 효과 자체(폭발/분열/튕김/총알 색/매료/등록소/감시 컴포넌트/이벤트 구독 등): Remove()에서 전부 되돌립니다.
// 3) 특수 효과로 "얻은" 능력치(47번 처치 공격력 등)는 남습니다 - 13/14/34번 음식처럼 영구 귀속입니다.
//    그래서 이런 효과는 PlayerStats.AddModifier에 아이템 ID가 아니라 EffectStatSourceId(아이템 ID + "_effect")를 넣어,
//    1)의 기본 스탯 제거에 휩쓸리지 않게 합니다. (효과 설명에 "잃으면 능력치도 잃는다"고 적힌 경우만 예외로 되돌립니다.)
// 4) 1회성으로 받은 재화(수류탄/해킹툴/크레딧/청휘석)는 남습니다 - Remove에서 빼앗지 않습니다.
// 5) 예외: 24번의 총 코스트 증가는 플레이어 스탯이 아니라 PlayerResources가 관리하는 값이라, 잃으면 총 코스트를 되돌리고
//    현재 코스트도 새 총 코스트를 넘지 않게 깎습니다.
//
// [다시 얻을 때 - 48번]
// 48번은 결과로 나온 아이템을 전부 "새로 얻은 것"으로 취급합니다(원래 가졌던 아이템이 다시 나와도). 그래서 같은 아이템을
// 잃었다가 같은 순간 다시 얻을 수 있습니다 - Remove()가 소지 효과를 확실히 되돌려야 Apply()로 다시 걸어도 효과가 두 번
// 겹치지 않습니다(4번 폭발 2번, 24번 코스트 14 같은 문제 방지).
//
// [감시 컴포넌트를 떼는 효과 주의]
// 플레이어에 붙인 컴포넌트(10/26/54번 Runner, 2번 FootprintTrail)는 Remove에서 DestroyImmediate로 즉시 뗍니다. 일반
// Destroy는 프레임 끝에야 실제로 지워져서, 같은 프레임에 다시 얻으면(Apply의 GetComponent가 곧 지워질 컴포넌트를 재사용)
// 프레임이 끝날 때 효과가 사라져버리기 때문입니다.
//
// [새 패시브 효과를 만들 때]
// 소지하는 동안만 유지되는 효과가 있으면 이 인터페이스를 구현하세요. 구현하지 않은 특수 효과를 가진 패시브를 잃으면
// PlayerInventory가 경고를 남깁니다(효과가 남아버릴 수 있다는 뜻). 되돌릴 게 없는 효과(1회성 지급만 하는 효과 등)도
// 빈 Remove()로 구현해두면 경고가 뜨지 않습니다.
public interface IRemovablePassiveEffect
{
    // 이 효과가 소지하는 동안 걸어둔 것을 되돌립니다. Apply()와 같은 context/sourceItemId로 호출됩니다.
    void Remove(PassiveEffectContext context, string sourceItemId);
}

public static class PassiveEffectIds
{
    // 특수 효과로 얻은(잃어도 남는) 능력치를 PlayerStats에 등록할 때 쓰는 출처 이름입니다. 위 3) 참고.
    public static string EffectStatSourceId(string sourceItemId) => sourceItemId + "_effect";
}
