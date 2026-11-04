using UnityEngine;

// 장신구 110번 - "보유하고 있으면 좌클릭을 꾹 눌러 총알을 충전할 수 있다. 떼면 충전한 총알에 비례해 강력한 총알 한 발을 발사한다."
//
// [기획 - 형빈 확정]
// - 좌클릭을 누르는 순간 발사될 총알 자체의 탄약 1발을 소모하고, 누르고 있는 동안 0.5초(chargeInterval)마다 탄약 1발씩을 더 소모해
//   1발씩 충전합니다. 충전은 남은 탄약만큼만 됩니다(탄창 5발이면 누르는 순간 1발 + 충전 최대 4발). 탄약을 다 쓰면 더 충전되지 않습니다.
// - 좌클릭을 떼야만 발사됩니다(언제 쏠지는 플레이어 마음).
//   - 충전 0발(0.5초 전에 뗌): 기존 기본 총알과 똑같이 발사됩니다(연사 포함, 누를 때 미리 뺀 1발은 돌려준 뒤 평소 규칙으로 소모).
//   - 충전 1발 이상: 연사를 무시하고 강화 총알 한 발을 쏩니다(19번 양갈래가 있으면 강화 총알 두 발로 갈라짐 - 형빈 확정).
//     충전 1발당 피해 +현재 공격력의 50%, 사거리 +0.1, 총알 크기 +0.01(실제 값, 화면 +1), 총알 속도 +0.1(현재 스탯에 가산).
//     예) 1발 충전 = 공격력 150%.
// - 충전 중 장전(R) / 무기 전환(2·3키) / 아이템 획득 연출 / 사망 / 110번을 잃음: 충전을 완전히 취소합니다. 충전에 쓴 탄약은
//   돌려주지 않습니다(형빈 확정). R이면 그대로 장전에 들어갑니다.
// - 1발 충전될 때마다 플레이어 몸 중심에서 vfx_Implosion_01이 한 번씩 터집니다.
// - 충전 수 표시: 1발 이상 충전되면 장전 R 표시 자리 위쪽에 110번 아이콘 + 아이콘 오른쪽 아래에 숫자(검은 글자 + 흰 테두리)를 띄웁니다.
//   숫자 = 이번 사격에 소모한 탄약 수(누를 때 1발 + 충전 수). 예) 1발 충전 = "2", 탄창 5발을 모두 쓰면 "5".
// - 강화 총알 외형: 1발 이상 충전된 총알은 빛나는 중심부 + 외곽 빛 + 나선 고리 + 번개 + 분홍→파랑 꼬리로 보입니다(코드로 생성,
//   ChargedBulletVisual.cs). 총알 색 아이템(38번 등)이 있으면 중심부는 그 색을 따르고 나머지만 덧씌웁니다. 5번으로 분열한 분열탄도
//   강화 외형을 물려받습니다(형빈 확정).
// - [관통] 4발 이상 충전(머리 위 숫자 5 이상 - pierceChargeThreshold)한 강화 총알은 적과 벽을 관통합니다. 같은 적은 한 번만 맞고,
//   35번(벽 튕김)/69번(적 튕김)으로 튕기지 않습니다(형빈 확정). 4번 폭발/5번 분열 등 닿을 때마다 반응하는 효과는 관통하며 닿을 때마다
//   그대로 반응합니다. 5번 분열탄은 강화 외형만 물려받고 관통하지는 않습니다.
// - 조준선: 충전 중(1발 이상, 우클릭 조준 시)에는 늘어난 사거리/총알 크기로 바로바로 그리고, 관통 기준 이상이면 벽에서 끊기지 않습니다.
// - 강화 총알도 일반 총알과 같이 OnProjectileSpawned를 거치므로 4/5/11/23/35/38/68/69번 등 총알 아이템 효과를 그대로 받습니다.
//
// [구현]
// - ChargeShotOverride 등록소에 설정 묶음 + 아이콘을 등록/해제하고, 플레이어에 PlayerChargeShot을 붙여 둡니다(없으면 추가).
// - 좌클릭 처리: PlayerAimAndAttack(누름/뗌 판단) → PlayerChargeShot(충전 진행/취소/이펙트/표시) → PlayerShooting.FireChargedShot(발사).
//
// [아이템 에셋 설정]
// ItemDefinition: Item Id = 110, Category = Trinket, Trinket Effect에 ChargeShotTrinketEffect. Settings > Implosion Prefab 칸에
// vfx_Implosion_01을 넣습니다.
[System.Serializable]
public class ChargeShotTrinketEffect : ITrinketEffect
{
    [Tooltip("110번 충전 사격 설정입니다(충전/가산 수치/충전 이펙트/충전 수 표시/강화 총알 외형).")]
    public ChargeShotOverride.Settings settings = new ChargeShotOverride.Settings();

    public void Apply(TrinketEffectContext context, string sourceItemId)
    {
        if (context == null || context.PlayerObject == null)
        {
            Debug.LogWarning("ChargeShotTrinketEffect: 플레이어를 찾을 수 없어 110번 효과를 적용하지 못했습니다.");
            return;
        }

        if (settings == null) settings = new ChargeShotOverride.Settings();

        ChargeShotOverride.Register(SourceKey(sourceItemId), context.PlayerObject, settings,
                                    FindIcon(context.PlayerObject, sourceItemId));
        PlayerChargeShot.GetOrAdd(context.PlayerObject);
    }

    // 장착 해제(교체/버리기) 시: 등록만 풉니다. 충전 중이었다면 PlayerChargeShot이 다음 프레임에 충전을 취소합니다.
    public void Remove(TrinketEffectContext context, string sourceItemId)
    {
        ChargeShotOverride.Unregister(SourceKey(sourceItemId));
    }

    private static string SourceKey(string sourceItemId) => "charge_shot_" + sourceItemId;

    // 장착 중인 장신구 목록에서 이 아이템의 아이콘을 찾습니다(PlayerTrinket은 Apply 직전에 목록에 추가함).
    private static Sprite FindIcon(GameObject player, string sourceItemId)
    {
        var trinket = player.GetComponent<PlayerTrinket>();
        if (trinket == null) return null;

        foreach (var item in trinket.EquippedItems)
        {
            if (item != null && item.itemId == sourceItemId) return item.icon;
        }
        return null;
    }
}
