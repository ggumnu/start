using System;
using UnityEngine;

// ============================================================================================================
// [보류 - 형빈 확정, 2026-10] 48번의 예전 효과("보물상자 아이템 가중치를 랜덤으로 뒤섞기")입니다.
// 48번은 "사용하면 보유 패시브 전부를 랜덤 교체하는 일회용"(Item48effect.cs - RerollAllPassivesEffect)으로 바뀌었고,
// 이 효과는 나중에 다시 쓸 수도 있어 지우지 않고 이 파일로 옮겨 보관합니다. 지금은 어떤 아이템에도 쓰이지 않습니다.
// - 클래스 이름(WeightScrambleTrinketEffect / WeightScramblePassiveEffect)은 그대로 유지했습니다. 아이템 에셋은 효과를
//   클래스 이름으로 기억하므로, 이름을 바꾸면 이 효과를 쓰던 에셋의 연결이 끊어집니다.
// - 실제 가중치 뒤섞기 코드(ItemPedestal/NormalRoomTreasureSpawner의 scramble 분기, NextItemPickWeightOverride)도 그대로
//   남아 있어, 이 효과를 아이템에 다시 넣기만 하면 예전처럼 동작합니다.
// - 패시브 버전에는 패시브 잃기(IRemovablePassiveEffect) 대응을 추가했습니다(잃으면 상자 열림 구독 해제).
// ============================================================================================================

// 장신구 48번 (v1.39 변경 - 일회용에서 장신구로) - "장착하고 있는 동안 개봉하는 모든
// 보물상자의 아이템 가중치가 매번 새로 랜덤하게 뒤섞인다".
//
// [장신구 -> 패시브 변경 (형빈 확정)]
// 효과는 그대로 두고 분류만 패시브로 바꿨습니다. 패시브는 IPassiveItemEffect(획득 시 Apply 한 번, Remove 없음 - 영구)를
// 쓰므로 장신구용 효과(WeightScrambleTrinketEffect)를 그대로 쓸 수 없어, 이 파일 아래에 패시브용
// WeightScramblePassiveEffect를 추가했습니다(동작은 같음 - 상자가 열릴 때마다 NextItemPickWeightOverride 무장).
// - 패시브라 한 번 얻으면 런이 끝날 때까지 계속 적용됩니다(장신구처럼 교체로 잃는 일 없음).
// - 정적 이벤트 구독은 씬을 다시 불러와도(게임 오버 후 재시작) 남기 때문에, 구독할 때 플레이어 오브젝트를 함께
//   기억해두고, 상자가 열릴 때 그 플레이어가 이미 사라졌으면(새 런) 아무것도 하지 않고 스스로 구독을 해지합니다 -
//   48번 없이 새로 시작한 런에 효과가 새지 않습니다.
// - 장신구 버전(WeightScrambleTrinketEffect)도 기존 에셋 호환을 위해 그대로 남겨둡니다(더 이상 쓰지 않음).
// - 에셋 설정: 48번 ItemDefinition의 Category = Passive, Special Effects에 WeightScramblePassiveEffect 추가.
//   Trinket Effect 칸은 패시브에서는 읽지 않으므로 비워두거나 그대로 둬도 무해합니다.
//
// [기획 - v1.39 변경: 일회용 -> 장신구, 형빈 확정]
// - 원래 48번은 일회용 아이템으로, 사용하면 딱 한 번(바로 다음에 여는 보물상자 하나)만
//   가중치가 뒤섞이고 끝이었습니다. 한 번 쓰고 나면 효과가 사라져서 체감이 잘 안 되고
//   활용도가 낮다는 문제가 있어, 장신구로 바꿨습니다.
// - 이제 48번은 장신구(Trinket) 아이템입니다. 장착하고 있는 동안에는, 그 동안 개봉하는
//   모든 보물상자(패시브/액티브/장신구/일회용 단일 아이템은 물론, 기본 아이템 번들도
//   포함)의 가중치가 열 때마다 매번 새로 랜덤 정수로 뒤섞인 뒤 뽑힙니다. 원래의
//   초급/일반/상급/최상급 가중치와는 완전히 무관합니다.
// - [v1.39 - 랜덤 범위 조정, 형빈 확정] 처음에는 1~5 사이로 굴렸지만, 범위가 너무 넓다는
//   판단 하에 1~3 사이로 좁혔습니다(ItemPedestal.PickWeighted/PickWeightedBundle,
//   NormalRoomTreasureSpawner.PickWeightedItem/PickWeightedBundle의
//   Random.Range(1, 4) 참고 - 실제 랜덤 굴림/디버그 로그는 전부 그쪽 네 메서드에 있고,
//   이 클래스는 "지금 스크램블이 걸려야 한다"는 신호만 담당합니다).
// - 같은 상자를 두 번 열 수는 없지만, 상자를 열 때마다 그 자리에서 새로 굴리므로 서로
//   다른 상자를 여러 개 열면 상자마다 결과가 다를 수 있습니다.
// - 다른 장신구와 교체되거나(Remove) 이 아이템을 잃으면, 그 이후로 여는 보물상자는 다시
//   원래 가중치를 그대로 따릅니다.
//
// [구현 - ContainerOpenEvents.OnAnyContainerOpened 구독, 장착 중 계속 유지]
// TreasureChest.Open()/ItemPedestal.Open()은 실제 내용물을 확정하기(가중치 뽑기를 실행하기)
// 전에 반드시 ContainerOpenEvents.RaiseOpened()를 먼저 호출하도록 되어 있습니다. 예전
// 일회용 버전은 "한 번 발동하면 스스로 구독 해지"했지만, 장신구는 장착하는 동안 계속
// 반응해야 하므로 Apply()에서 구독을 걸고 Remove() 전까지 해지하지 않습니다 - 매번 상자가
// 열릴 때마다 그 즉시 NextItemPickWeightOverride.ArmForThisOpen()을 호출해 무장시키고,
// 각 보물상자의 Open()이 뽑기가 끝난 뒤 항상 ClearAfterThisOpen()을 호출해 스스로 정리하는
// 흐름은 그대로입니다.
//
// [디버그 로그 - v1.39 추가, 형빈 확정]
// 랜덤이 실제로 매번 새로 굴러가는지 눈으로 확인할 수 있도록, 스크램블이 걸린 상태로 뽑기가
// 일어날 때마다 "어떤 아이템/묶음이 어떤 임시 가중치로 등장했는지"를 콘솔에 출력합니다.
// 이 클래스가 직접 로그를 남기는 게 아니라(이 클래스는 무장 신호만 켜고 끄는 시점을 모릅니다),
// 실제 뽑기가 일어나는 ItemPedestal/NormalRoomTreasureSpawner의 네 메서드 쪽에서
// scramble(=NextItemPickWeightOverride.IsArmed)이 true일 때만 출력합니다. 즉 48번을 장착하지
// 않았을 때(스크램블이 꺼져있을 때)는 평소처럼 아무 로그도 뜨지 않습니다.
//
// [재적용 안전장치]
// 다른 장신구들(20번/32번 등)과 동일한 이유로, Apply() 시작 시 Remove()를 먼저 호출해
// 혹시 남아있을 이전 구독을 정리한 뒤 새로 구독합니다.
//
// [아이템 에셋 설정 - 기존 48번 에셋을 직접 수정해야 합니다]
// ItemDefinition: Item Id = 48(그대로), Category를 Disposable -> Trinket으로 변경,
// Trinket Effect = WeightScrambleTrinketEffect. 기존 Disposable Effect 필드(예전
// Item48effect 참조)는 이제 코드에서 보지 않으니 비워두거나 그대로 남겨둬도 무해합니다.
[Serializable]
public class WeightScrambleTrinketEffect : ITrinketEffect
{
    // 보물상자가 열릴 때마다 무장 신호를 켜기 위해 구독해둔 핸들러입니다. null이 아니면
    // "지금 장착 중이고 구독도 이미 되어있다"는 뜻입니다.
    private Action containerOpenedHandler;

    public void Apply(TrinketEffectContext context, string sourceItemId)
    {
        // 재장착 시 이전 구독이 남아있으면 먼저 정리합니다(위 [재적용 안전장치] 참고).
        Remove(context, sourceItemId);

        containerOpenedHandler = () =>
        {
            NextItemPickWeightOverride.ArmForThisOpen();
        };
        ContainerOpenEvents.OnAnyContainerOpened += containerOpenedHandler;
    }

    public void Remove(TrinketEffectContext context, string sourceItemId)
    {
        if (containerOpenedHandler != null)
        {
            ContainerOpenEvents.OnAnyContainerOpened -= containerOpenedHandler;
        }
        containerOpenedHandler = null;
    }
}

// [패시브 버전 - 장신구 -> 패시브 변경, 형빈 확정] 효과는 WeightScrambleTrinketEffect와 같습니다(파일 상단 참고).
// 획득 시 Apply()가 한 번 실행되어 상자 열림 신호를 구독하고, 런이 끝날 때까지 유지됩니다.
[Serializable]
public class WeightScramblePassiveEffect : IPassiveItemEffect, IRemovablePassiveEffect
{
    // 구독해둔 핸들러와, 그 구독을 건 플레이어(사라지면 = 새 런 -> 스스로 해지)
    private Action containerOpenedHandler;
    private GameObject owner;

    public void Apply(PassiveEffectContext context, string sourceItemId)
    {
        // 혹시 남아있을 이전 구독(이전 런 등)을 먼저 정리합니다.
        Unsubscribe();

        owner = context != null ? context.PlayerObject : null;
        if (owner == null)
        {
            Debug.LogWarning("WeightScramblePassiveEffect: 플레이어 오브젝트를 찾을 수 없어 효과를 등록하지 못했습니다.");
            return;
        }

        containerOpenedHandler = () =>
        {
            // 이 구독을 건 플레이어가 사라졌으면(씬 재시작 = 새 런) 효과를 걸지 않고 구독을 해지합니다.
            if (owner == null)
            {
                Unsubscribe();
                return;
            }
            NextItemPickWeightOverride.ArmForThisOpen();
        };
        ContainerOpenEvents.OnAnyContainerOpened += containerOpenedHandler;
    }

    // [패시브 잃기 - 48번 등] 상자 열림 구독을 해제합니다(IRemovablePassiveEffect.cs 참고).
    public void Remove(PassiveEffectContext context, string sourceItemId)
    {
        Unsubscribe();
        owner = null;
    }

    private void Unsubscribe()
    {
        if (containerOpenedHandler != null)
        {
            ContainerOpenEvents.OnAnyContainerOpened -= containerOpenedHandler;
        }
        containerOpenedHandler = null;
    }
}
