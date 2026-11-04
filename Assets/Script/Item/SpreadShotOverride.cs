using System.Collections.Generic;
using UnityEngine;

// "총알이 세 갈래(정중앙 + 좌우 15도)로 퍼져 나간다"는 137번 패시브를 위한 정적 등록 창구입니다. 19번의 SplitShotOverride와
// 완전히 같은 구조입니다(sourceId 기준 등록/해제, 주인이 파괴되면 자동 정리, Play 시작 때 초기화).
//
// [발사 각도 계산을 한 곳에 - FillShotAngles]
// 실제 발사(PlayerShooting.FireBurst)와 조준선(AimTrajectoryIndicator)이 같은 함수로 각도 목록을 받아 쓰므로, 둘이 항상
// 일치합니다(형빈 확정 규칙).
// - 아무것도 없음          : 0                              (1발)
// - 137번만               : -15, 0, +15                    (3발)
// - 19번만                : -30, +30                       (2발 - 기존과 같음)
// - 19번 + 137번          : -45, -30, -15, +15, +30, +45   (6발 - 19번의 각 총구에서 3발씩, 정중앙으로는 나가지 않음)
// 즉 19번이 "총구 방향"을 먼저 정하고(19번 우선), 137번이 그 각 총구를 ±SpreadAngle로 세 갈래로 퍼뜨립니다.
//
// [조준선은 총구 방향만 - 형빈 확정]
// 137번 확산 줄까지 모두 그리면 화면이 너무 복잡해서, 조준선(AimTrajectoryIndicator)은 FillShotAngles 대신 FillAimAngles를 씁니다.
// FillAimAngles는 137번의 퍼짐을 빼고 "총구 방향"만 돌려줍니다 - 137번만이면 중앙 한 줄, 19번과 함께면 19번 양갈래 두 줄
// (19번만일 때와 같음). 실제 발사(FillShotAngles)는 그대로 3발/6발입니다.
//
// [사용 방법 - 코드에서]
// SpreadShotOverride.SetActive("137", 주인GameObject);
// SpreadShotOverride.RemoveActive("137");
// SpreadShotOverride.FillShotAngles(목록);  // 이번 한 발(탄약 1)로 나갈 총알들의 각도(도, 기준 방향에서 Y축 회전)
public static class SpreadShotOverride
{
    // 한 총구에서 좌우로 퍼지는 각도(도)입니다(형빈 확정 - 15도).
    public const float SpreadAngle = 15f;

    // 동시에 나갈 수 있는 총알(조준선 방향) 최대 수입니다(19번 2총구 x 137번 3갈래). 조준선이 이만큼 미리 만들어 둡니다.
    public const int MaxShotCount = 6;

    private static readonly Dictionary<string, Object> owners = new Dictionary<string, Object>();
    private static readonly List<string> deadKeys = new List<string>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlayStart()
    {
        owners.Clear();
    }

    // 등록합니다. 같은 sourceId로 다시 불러도 안전합니다(값을 덮어씀).
    public static void SetActive(string sourceId, Object owner)
    {
        if (string.IsNullOrEmpty(sourceId)) return;

        PruneDead();
        owners[sourceId] = owner;
    }

    // 등록을 해제합니다. 등록된 적이 없는 sourceId여도 안전합니다.
    public static void RemoveActive(string sourceId)
    {
        if (string.IsNullOrEmpty(sourceId)) return;

        owners.Remove(sourceId);
    }

    // 살아있는 등록이 하나라도 있으면 true입니다.
    public static bool IsActive
    {
        get
        {
            PruneDead();
            return owners.Count > 0;
        }
    }

    // 이번 한 발(탄약 1)로 나갈 총알들의 각도(도)를 왼쪽부터 순서대로 채웁니다. 기존 내용은 지웁니다.
    public static void FillShotAngles(List<float> angles)
    {
        if (angles == null) return;
        angles.Clear();

        bool spread = IsActive;

        // 1) 19번이 총구 방향을 정합니다(없으면 정중앙 하나).
        if (SplitShotOverride.IsActive)
        {
            float half = SplitShotOverride.HalfAngle;
            AddMuzzle(angles, -half, spread);
            AddMuzzle(angles, half, spread);
        }
        else
        {
            AddMuzzle(angles, 0f, spread);
        }
    }

    // 조준선용: 137번의 퍼짐을 뺀 총구 방향만 채웁니다(없으면 0 하나, 19번이면 -HalfAngle/+HalfAngle). 기존 내용은 지웁니다.
    public static void FillAimAngles(List<float> angles)
    {
        if (angles == null) return;
        angles.Clear();

        if (SplitShotOverride.IsActive)
        {
            float half = SplitShotOverride.HalfAngle;
            angles.Add(-half);
            angles.Add(half);
        }
        else
        {
            angles.Add(0f);
        }
    }

    // 2) 137번이 있으면 각 총구를 -SpreadAngle / 0 / +SpreadAngle 세 갈래로 퍼뜨립니다.
    private static void AddMuzzle(List<float> angles, float muzzleAngle, bool spread)
    {
        if (spread)
        {
            angles.Add(muzzleAngle - SpreadAngle);
            angles.Add(muzzleAngle);
            angles.Add(muzzleAngle + SpreadAngle);
        }
        else
        {
            angles.Add(muzzleAngle);
        }
    }

    // 주인이 이미 파괴된 등록을 사전에서 치웁니다.
    private static void PruneDead()
    {
        deadKeys.Clear();

        foreach (var pair in owners)
        {
            if (pair.Value == null) deadKeys.Add(pair.Key);
        }

        for (int i = 0; i < deadKeys.Count; i++)
        {
            owners.Remove(deadKeys[i]);
        }

        deadKeys.Clear();
    }
}
