using UnityEngine;

// 패시브 97번 - "보물상자를 열 때마다 보호막을 1개 얻는다."
//
// [기획 - 형빈 확정]
// - 보유 중 보물상자를 "열 때"(F로 처음 여는 순간) 보호막을 shieldPerOpen(기본 1)개 얻습니다. 최대 5개(PlayerHealth.maxShield)이고,
//   꽉 차 있으면 아무 일도 일어나지 않습니다(초과분은 기억하지 않음 - 58번과 같은 규칙).
// - 정당한 보물상자만 해당합니다: 일반방 보물상자, 동아리방/보스방 보물상자, 특수방 크레딧 더미/아이템 받침대, 9번으로 복제된 상자.
//   Ctrl 꾹으로 내려놓은 슬롯 아이템 좌대, 상점 임시 보관 좌대(둘 다 HoldingPedestal)는 보물상자가 아니라서 얻지 않습니다.
// - 이미 열린 상자는 다시 열리지 않으므로 같은 상자에서 두 번 얻을 수 없습니다.
// - 97번을 얻은 뒤에 여는 상자부터 해당합니다(얻기 전에 열어 둔 상자는 해당 없음).
// - 보호막을 얻는 순간 보호막 획득 연출(깎은 정이십면체, 0.5초)이 바로 나타납니다(PlayerHealth.AddShield 기본 동작).
// - (테스트 씬만) ItemTestArenaBuilder의 "테스트 상자를 열린 상태로 시작"이 켜져 있으면, 97번을 가진 채 테스트 상자를 다시 만들 때
//   자동으로 열리면서 보호막이 들어올 수 있습니다. 실제 게임에는 영향이 없어 그대로 둡니다(형빈 확정).
//
// [패시브를 잃을 때 - 48번 등]
// 보호막 얻기만 멈춥니다. 이미 얻은 보호막은 그대로 남습니다.
//
// [구현 - 기존 스크립트는 고치지 않았습니다]
// "보물상자가 처음 열렸다" 알림(ContainerOpenEvents.OnAnyContainerOpened - TreasureChest/ItemPedestal.Open()이 보냄, HoldingPedestal은
// 보내지 않음)을 구독해서 PlayerHealth.AddShield를 부릅니다. 이 알림은 모든 판이 같이 쓰는 정적 알림이라, 새 게임 등으로 플레이어가
// 사라졌으면 스스로 구독을 정리합니다.
//
// [아이템 에셋 설정]
// ItemDefinition: Item Id = 97, Category = Passive, Special Effects에 ChestOpenShieldEffect 하나. Stat Modifiers는 비워둡니다.
[System.Serializable]
public class ChestOpenShieldEffect : IPassiveItemEffect, IRemovablePassiveEffect
{
    [Tooltip("보물상자를 하나 열 때마다 얻는 보호막 개수입니다(최대 보호막 5개를 넘는 만큼은 버려짐).")]
    [Min(1)] public int shieldPerOpen = 1;

    [System.NonSerialized] private PlayerHealth health;
    [System.NonSerialized] private bool subscribed;

    public void Apply(PassiveEffectContext context, string sourceItemId)
    {
        if (context == null || context.PlayerObject == null)
        {
            Debug.LogWarning("ChestOpenShieldEffect: 플레이어를 찾을 수 없어 97번 효과를 적용하지 못했습니다.");
            return;
        }

        Unsubscribe(); // 혹시 남아 있을 이전 구독 정리(중복 방지 - 효과 객체는 아이템 에셋에 하나뿐이라 재사용됨)

        health = context.Health != null ? context.Health : context.PlayerObject.GetComponent<PlayerHealth>();
        if (health == null)
        {
            Debug.LogWarning("ChestOpenShieldEffect: PlayerHealth를 찾을 수 없어 97번 효과를 적용하지 못했습니다.");
            return;
        }

        ContainerOpenEvents.OnAnyContainerOpened += HandleContainerOpened;
        subscribed = true;
    }

    // [패시브 잃기 - 48번 등] 보호막 얻기만 멈춥니다. 이미 얻은 보호막은 그대로 둡니다.
    public void Remove(PassiveEffectContext context, string sourceItemId)
    {
        Unsubscribe();
    }

    private void Unsubscribe()
    {
        if (subscribed) ContainerOpenEvents.OnAnyContainerOpened -= HandleContainerOpened;
        subscribed = false;
    }

    private void HandleContainerOpened()
    {
        // 새 게임(씬 재시작) 등으로 이 효과를 건 플레이어가 사라졌으면 구독을 정리합니다.
        if (health == null)
        {
            Unsubscribe();
            return;
        }

        if (health.IsDead) return;
        health.AddShield(shieldPerOpen); // 최대치면 늘지 않고 아무 일도 없음(획득 연출도 나오지 않음)
    }
}
