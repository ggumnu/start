using System.Collections.Generic;
using UnityEngine;

// "총알이 정중앙이 아니라 좌우로 벌어진 두 갈래로 나간다"는 19번 패시브를 위한 정적 등록
// 창구입니다. ReloadSpeedBonusOverride/EnemyDetectRangeOverride 등과 동일한 패턴입니다 -
// 아이템이 자기 등록만 해두면, 실제 사용 지점(PlayerShooting.FireBurst,
// AimTrajectoryIndicator.Update)은 어떤 아이템이 등록했는지 전혀 몰라도 됩니다.
//
// [숫자 보너스가 아니라 on/off인 이유]
// 다른 등록소들(ReloadSpeedBonusOverride 등)은 "얼마나"를 다투는 숫자 보너스라 여러 개가
// 동시에 등록될 수 있지만, 19번은 "벌어지느냐 아니냐"만 있는 하나뿐인 아이템입니다.
// 그래도 등록소 자체는 다른 것들과 동일하게 sourceId 기준 등록/해제 구조로 만들어서, 나중에
// 비슷한 계열의 다른 아이템이 생기더라도 이 파일 하나만 건드리면 되도록 대비해뒀습니다 -
// 지금은 등록된 게 하나라도 있으면 IsActive가 true입니다.
//
// [각도 값을 여기 한 곳에 - 형빈 확정: 60도 벌어짐(좌우 각 30도)]
// HalfAngle(기본 30)을 PlayerShooting과 AimTrajectoryIndicator가 똑같이 참조하므로, 나중에
// 벌어지는 각도를 조절하고 싶어지면 이 파일의 HalfAngle 하나만 고치면 발사/조준선이 항상
// 같이 바뀝니다.
//
// [등록자(플레이어)가 사라졌을 때]
// 등록할 때 주인 오브젝트(보통 플레이어)를 함께 넘기고, 주인이 파괴되면(런 재시작 등) 그
// 등록은 자동으로 무시/정리됩니다 - 다른 정적 등록소들과 동일한 패턴입니다.
//
// [사용 방법 - 코드에서]
// SplitShotOverride.SetActive("19", 주인GameObject);
// SplitShotOverride.RemoveActive("19");
// if (SplitShotOverride.IsActive) { ... SplitShotOverride.HalfAngle ... }
public static class SplitShotOverride
{
    // 좌우로 벌어지는 각도(도, 한쪽 기준)입니다. 총 벌어지는 각도는 이 값의 2배입니다
    // (기본 30 -> 총 60도, 형빈 확정).
    public const float HalfAngle = 30f;

    private static readonly Dictionary<string, UnityEngine.Object> owners = new Dictionary<string, UnityEngine.Object>();
    private static readonly List<string> deadKeys = new List<string>();

    // 에디터에서 도메인 리로드를 끈 상태로 Play를 다시 눌러도 이전 Play의 등록이 남지
    // 않도록, Play 시작 때마다 비웁니다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlayStart()
    {
        owners.Clear();
    }

    // 등록합니다. 같은 sourceId로 다시 불러도 안전합니다(값을 덮어씀).
    public static void SetActive(string sourceId, UnityEngine.Object owner)
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
