using UnityEngine;

// 패시브 아이템 23번 "30색 물감 세트" - "이 아이템을 소지하고 있으면 총알이 공격력의 ±30만큼 랜덤한 피해를 입힌다.
// 최소 피해는 1. 굴린 값에 따라 총알 색이 빨주노초파남보 중 하나가 된다."
//
// [기획 - 형빈 확정]
// - 총알 한 발마다 -30 ~ +30 사이 정수를 같은 확률로 굴려 최종 피해에 더합니다. 최소 피해는 1입니다.
//   예) 공격력 30 -> 1 ~ 60, 공격력 60 -> 30 ~ 90. 실제 피해는 소수점 그대로, 화면 숫자는 반올림되어 보입니다.
// - 23번은 "추가 피해" 개념이라 다른 효과가 전부 계산된 뒤에 덧씌웁니다.
//   최종 피해 = 최대값(11번 거리 보정/35번 튕김 보너스 등이 반영된 피해 + 굴린 값, 1)
//   예) 11번으로 40 -> -30이면 10, +30이면 70 / 11번으로 5 -> -30이면 1(하한선).
//   35번 튕김 +25%는 원래 피해에만 붙고 23번 값에는 붙지 않습니다.
// - 4번 폭발 피해는 총알의 최종 피해를 읽으므로 23번 값이 포함되고, 폭발 불꽃 색도 그 총알의 23번 색을 따라갑니다
//   (흰색/검정 같은 무채색 파티클은 그대로 - Item04effect.cs 참고).
// - 5번 분열탄은 새로 굴리고, 굴린 값에 분열 효과 배율을 곱합니다(원본 ±30, 1세대 ±15, 2세대 ±7.5). 색은 각자의
//   굴린 값으로 정해집니다.
// - 색 구간(범위 30 기준): 빨 -30~-22 / 주 -21~-13 / 노 -12~-5 / 초 -4~+4 / 파 +5~+12 / 남 +13~+21 / 보 +22~+30
// - 23번 색은 다른 어떤 총알 색 아이템(38번 보라색 등)보다도 우선합니다.
// - 행운 연동은 나중에 RandomBulletDamageOverride.RollInteger() 한 곳에 추가할 예정입니다.
//
// [에디터 설정 방법]
// ItemDefinition 에셋 - Item Id = 23, Category = Passive, Special Effects 리스트에 "+"를 눌러
// RandomBulletDamageEffect를 고르면 아래 필드들이 나타납니다. 색은 여기서 바로 바꿀 수 있고, Play 중에 바꾸면 다음
// 총알부터 바로 반영됩니다.
//
// [패시브 잃기 - 48번 등] 이 아이템을 잃으면 Remove()에서 등록을 해제해, 그 뒤로 쏘는 총알부터 원래대로 돌아갑니다.
[System.Serializable]
public class RandomBulletDamageEffect : IPassiveItemEffect, IRemovablePassiveEffect
{
    [Header("랜덤 추가 피해")]
    [Tooltip("총알마다 -이 값 ~ +이 값 사이의 정수를 같은 확률로 굴려 최종 피해에 더합니다(기본 30).")]
    [Min(0)] public int rollRange = 30;

    [Tooltip("랜덤 추가 피해를 더한 뒤의 최소 피해입니다(기본 1).")]
    [Min(0f)] public float minDamage = 1f;

    [Header("총알 색 (굴린 값 낮음 -> 높음)")]
    [Tooltip("가장 낮은 구간(범위 30 기준 -30 ~ -22)의 색입니다. 적 총알 빨강과 구분되도록 분홍빛 빨강이 기본입니다.")]
    public Color redColor = new Color(1f, 0.302f, 0.478f, 1f);       // FF4D7A
    [Tooltip("범위 30 기준 -21 ~ -13 구간의 색입니다.")]
    public Color orangeColor = new Color(1f, 0.604f, 0.180f, 1f);    // FF9A2E
    [Tooltip("범위 30 기준 -12 ~ -5 구간의 색입니다.")]
    public Color yellowColor = new Color(1f, 0.878f, 0f, 1f);        // FFE000
    [Tooltip("범위 30 기준 -4 ~ +4 구간(가운데)의 색입니다.")]
    public Color greenColor = new Color(0.239f, 0.863f, 0.353f, 1f); // 3DDC5A
    [Tooltip("범위 30 기준 +5 ~ +12 구간의 색입니다.")]
    public Color blueColor = new Color(0.180f, 0.608f, 1f, 1f);      // 2E9BFF
    [Tooltip("범위 30 기준 +13 ~ +21 구간의 색입니다.")]
    public Color indigoColor = new Color(0.239f, 0.294f, 0.859f, 1f);// 3D4BDB
    [Tooltip("가장 높은 구간(범위 30 기준 +22 ~ +30)의 색입니다.")]
    public Color violetColor = new Color(0.651f, 0.302f, 1f, 1f);    // A64DFF

    public void Apply(PassiveEffectContext context, string sourceItemId)
    {
        if (context == null || context.PlayerObject == null)
        {
            Debug.LogWarning("RandomBulletDamageEffect: 플레이어를 찾을 수 없어 23번 효과를 적용하지 못했습니다.");
            return;
        }

        RandomBulletDamageOverride.Register("random_bullet_damage_" + sourceItemId, context.PlayerObject, this);
    }

    // [패시브 잃기 - 48번 등] 소지 중에만 유지되던 효과를 되돌립니다(IRemovablePassiveEffect.cs 참고).
    public void Remove(PassiveEffectContext context, string sourceItemId)
    {
        RandomBulletDamageOverride.Unregister("random_bullet_damage_" + sourceItemId);
    }
}
