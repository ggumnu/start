using UnityEngine;

// [첫 돌진 충전 이펙트] 돌진 적(일반 돌진 EnemyMeleeAttack / 연속 돌진 EnemyChainDashAttack / 2레벨 보스 벽 돌진)이 첫 돌진 충전을 시작할 때
// 재생하는 이펙트 설정과 재생 도우미입니다(1레벨 보스 전방위 공격 충전 이펙트도 같은 것을 씀). 이펙트는 적을 따라다니고(적의 자식), Lifetime 뒤 또는 충전이 중간에 끊기면 바로 지워집니다.
// 위치 = 모델 중심(메시 렌더러 범위 중심) + Offset(적이 바라보는 방향 기준). 설계 배경은 Docs/설계노트.md [첫 돌진 충전 이펙트].
[System.Serializable]
public class DashChargeVfxSettings
{
    [Tooltip("충전이 시작될 때 재생할 이펙트 프리팹. 비워두면 이펙트 없음.")]
    public GameObject prefab;
    [Tooltip("모델 중심 기준 위치 보정(m, 적이 바라보는 방향 기준 - Z가 앞).")]
    public Vector3 offset = Vector3.zero;
    [Tooltip("이펙트 크기 배율(프리팹 크기 x 이 값).")]
    [Min(0.01f)] public float scale = 1f;
    [Tooltip("이 시간(초) 뒤 이펙트를 지웁니다. 충전이 중간에 끊기면 그때 바로 지웁니다.")]
    [Min(0.1f)] public float lifetime = 2f;

    public bool HasPrefab => prefab != null;

    public DashChargeVfxSettings Clone() => (DashChargeVfxSettings)MemberwiseClone();
}

public static class DashChargeVfx
{
    // 이펙트를 재생하고 그 인스턴스를 돌려줍니다(없으면 null). 연출이라 오류가 나도 공격은 그대로 진행합니다.
    public static GameObject Play(DashChargeVfxSettings settings, Transform owner)
    {
        if (settings == null || settings.prefab == null || owner == null) return null;
        try
        {
            Vector3 center = owner.position;
            bool any = false;
            Bounds b = default;
            foreach (var r in owner.GetComponentsInChildren<Renderer>())
            {
                if (r == null || !r.enabled) continue;
                if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;
                if (any) b.Encapsulate(r.bounds);
                else { b = r.bounds; any = true; }
            }
            if (any) center = b.center;

            GameObject vfx = Object.Instantiate(settings.prefab, center + owner.rotation * settings.offset, Quaternion.identity, owner);
            vfx.transform.localScale = vfx.transform.localScale * settings.scale;
            Object.Destroy(vfx, settings.lifetime);
            return vfx;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"DashChargeVfx ({owner.name}): 첫 돌진 충전 이펙트 생성 중 오류가 발생해 건너뜁니다: {e}");
            return null;
        }
    }

    // 충전이 끊겼을 때 아직 남아 있는 이펙트를 바로 지웁니다.
    public static void Stop(ref GameObject vfx)
    {
        if (vfx != null) Object.Destroy(vfx);
        vfx = null;
    }
}
