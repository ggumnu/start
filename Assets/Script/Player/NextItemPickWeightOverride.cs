// 48번 일회용 아이템("사용하면 다음에 여는 상자에서 나타나는 모든 후보의 가중치가 1~5로
// 랜덤하게 뒤섞인다") 전용 플래그입니다.
//
// [스코프 - "그 상자를 여는 한 번의 Open() 호출" 동안만 유효]
// ArmForThisOpen()으로 무장하면, 그 다음 열리는 보물상자 하나의 Open() 안에서 일어나는
// 모든 가중치 뽑기(패시브/액티브/장신구/일회용 단일 아이템 + 기본 아이템 번들, 둘 다)가
// IsArmed == true인 동안 "각 후보의 실제 weight 대신 Random.Range(1, 6)으로 새로 매긴
// 가중치"를 사용합니다. 그 Open() 호출이 끝나면(내용물이 기본 아이템 폴백이든 단일
// 아이템이든 상관없이) ClearAfterThisOpen()이 항상 호출되어 무장이 풀립니다 - 그래야
// 그 다음다음 상자에는 영향이 새지 않습니다.
//
// [무장 시점]
// Item48effect.BeginPending()이 구독한 ContainerOpenEvents.OnAnyContainerOpened 핸들러가
// 발동하는 시점(=보물상자가 RaiseOpened()를 호출한 직후, 아직 실제 뽑기 로직 실행 전)에
// 동기적으로 ArmForThisOpen()을 호출합니다. TreasureChest.Open()/ItemPedestal.Open()이
// RaiseOpened()를 뽑기보다 먼저 호출하도록 되어 있어 순서가 항상 보장됩니다.
public static class NextItemPickWeightOverride
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
