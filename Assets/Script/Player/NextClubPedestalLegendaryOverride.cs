// 27번 음식("사용하면 다음에 여는 동아리방 상자에서 반드시 최상급(Tier 3) 아이템 중
// 하나가 나온다") 전용 플래그입니다. NextItemPickWeightOverride(48번 일회용)와 완전히
// 동일한 구조입니다.
//
// [스코프 - "다음에 열리는 동아리방 보물상자 하나의 Open() 호출" 동안만 유효]
// ArmForThisOpen()으로 무장하면, 그 다음 열리는 동아리방 보물상자 하나의 ItemPedestal.
// PickWeighted()가 IsArmed == true인 동안 뽑기 대상을 최상급(Tier 3) 등급 후보만으로
// 좁혀서 뽑습니다(자세한 내용은 ItemPedestal.PickWeighted() 참고). 그 Open() 호출이
// 끝나면(단일 아이템이 뽑혔든, 최상급 후보가 없어 보장 없이 진행됐든, 기본 아이템 폴백으로
// 빠졌든 상관없이) ClearAfterThisOpen()이 항상 호출되어 무장이 풀립니다 - 그래야 그
// 다음다음 동아리방 보물상자에는 영향이 새지 않습니다.
//
// [무장 시점]
// Item27effect(GuaranteedLegendaryFoodEffect).BeginPending()이 구독한 ClubChestOpenEvents.
// OnClubChestOpened 핸들러가 발동하는 시점(=동아리방 보물상자가 RaiseOpened()를 호출한 직후,
// 아직 실제 뽑기 로직 실행 전)에 동기적으로 ArmForThisOpen()을 호출합니다. ItemPedestal.
// Open()이 ClubChestOpenEvents.RaiseOpened()를 뽑기보다 먼저 호출하도록 되어 있어 순서가
// 항상 보장됩니다.
public static class NextClubPedestalLegendaryOverride
{
    public static bool IsArmed { get; private set; }

    public static void ArmForThisOpen()
    {
        IsArmed = true;
    }

    public static void ClearAfterThisOpen()
    {
        IsArmed = false;
    }
}
