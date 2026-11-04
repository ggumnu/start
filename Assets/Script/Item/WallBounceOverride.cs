using System.Collections.Generic;
using UnityEngine;

// "총알이 벽에 튕긴다"는 35번 패시브(WallBounceEffect, Item35effect.cs)를 보유 중인지를
// 다른 스크립트가 몰라도 조회할 수 있게 해주는 정적 등록 창구입니다. SplitShotOverride(19번)와
// 완전히 동일한 패턴입니다.
//
// [왜 필요한가 - AimTrajectoryIndicator 연동]
// WallBounceEffect 자체는 OnProjectileSpawned/OnDespawning 훅만으로 완결되는 효과라 원래는
// 이런 등록소가 필요 없었지만, 조준선(AimTrajectoryIndicator.cs)이 "이 벽에서 튕겨서 어디로
// 갈지" 미리보기를 그리려면 튕기는 아이템을 보유 중인지부터 알아야 합니다. 그래서 35번도
// 19번과 동일하게, Apply() 시점에 여기 한 번 등록해두고 조준선 쪽에서 IsActive만 조회합니다.
//
// [등록자(플레이어)가 사라졌을 때]
// 등록할 때 주인 오브젝트(보통 플레이어)를 함께 넘기고, 주인이 파괴되면(런 재시작 등) 그
// 등록은 자동으로 무시/정리됩니다 - 다른 정적 등록소들과 동일한 패턴입니다.
//
// [사용 방법 - 코드에서]
// WallBounceOverride.SetActive("35", 주인GameObject);
// WallBounceOverride.RemoveActive("35");
// if (WallBounceOverride.IsActive) { ... }
public static class WallBounceOverride
{
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
