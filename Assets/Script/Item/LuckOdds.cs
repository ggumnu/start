using UnityEngine;

// [행운 확률 보정 창구 - 아직 행운 효과 없음]
// 아이템의 "확률" 판정(106번 상자 3배 복제 25% 등)이 거쳐 가는 한 곳입니다. 행운 스탯을 정립하면 여기서 행운에 따라 확률을 바꾸면 되고,
// 그 확률을 쓰는 아이템들은 고칠 필요가 없습니다(형빈 확정 - 지금은 참조만 해두고 행운의 영향은 없음).
// 지금은 기본 확률을 그대로(0~1로 자르기만) 돌려줍니다.
//
// [사용 방법 - 코드에서]
// float chance = LuckOdds.Resolve(0.25f);          // 행운이 좋을수록 커져야 하는 확률(성공 확률)
// bool success = LuckOdds.Roll(0.25f);             // 위 확률로 굴린 결과
public static class LuckOdds
{
    // 성공 확률(행운이 좋으면 커져야 하는 쪽)을 보정합니다. 지금은 그대로입니다.
    public static float Resolve(float baseChance)
    {
        return Mathf.Clamp01(baseChance);
    }

    // 보정된 확률로 한 번 굴립니다.
    public static bool Roll(float baseChance)
    {
        return Random.value < Resolve(baseChance);
    }
}
