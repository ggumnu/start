using UnityEngine;

// 패시브 94번 - "플레이어는 디버프에 걸리지 않는다. 총알 주변에 후광이 생겨, 후광 안의 적에게 매초 총알 피해의 25%만큼
// 피해를 입힌다." ([아이작 리펜던스]의 갓헤드와 비슷한 효과)
//
// [기획 - 형빈 확정]
// - 디버프 면역: 적이 거는 상태 이상(앞으로 생길 감속/독/기절 등)만 막습니다. 트레이드오프 아이템의 능력치 감소는 디버프가
//   아닙니다. 지금은 플레이어 디버프가 없어서 등록만 해 둡니다(PlayerDebuffImmunity.cs 참고).
// - 후광: 플레이어 총알이 생길 때마다(OnProjectileSpawned - 19번/137번 갈래 총알, 5번 분열탄 포함) 그 총알에 후광을 붙입니다.
//   - 반지름: 기본 총알 크기에서 Halo Radius(0.3). 총알이 커지거나 작아지면 같은 비율로 커지고 작아집니다.
//   - 피해: 0.1초마다 그 총알의 지금 피해(11번 거리 보정/23번 랜덤 피해 등이 반영된 Projectile.Damage) x 2.5%를 후광 안의 살아 있는
//     적 전부에게 줍니다(합치면 매초 25%). 직격당하는 적도 맞기 전까지 후광 피해를 받고, 여러 총알의 후광이 겹치면 각각
//     피해를 줍니다. 순수 피해만 주고 38번 매료/68번 넉백/4번 폭발 같은 "총알 명중" 효과는 일으키지 않습니다.
//   - 피해 숫자는 기존 규칙 그대로(반올림, 0보다 크면 최소 1로 보임). 실제 피해는 소수점 그대로 들어갑니다.
//   - 색: 총알에 입혀진 색(23번 무지개색/38번 보라색 등)을 따라가고, 색 아이템이 없으면 Default Color(노란색)입니다.
//   - 총알이 사라지면 후광도 함께 사라집니다(총알의 자식).
//   - 외곽 발광: 후광 바깥 가장자리가 셰이더 외곽선처럼 은은하게 빛납니다(카메라를 바라보는 고리 모양 빛 - BulletHalo.cs 참고).
//     색은 후광 색을 따라가고, 크기/세기는 아래 "후광 외곽 발광" 칸에서 조절합니다. 이펙트 프리팹을 쓸 때도 함께 붙습니다.
// - 48번 등으로 잃으면 디버프 면역은 바로 풀리고, 그 뒤로 쏘는 총알부터 후광이 붙지 않습니다(날아가는 중인 총알의 후광은 그 총알이
//   사라질 때까지 남음 - 4번과 같은 규칙).
//
// [아이템 에셋 설정]
// ItemDefinition: Item Id = 94, Category = Passive, Special Effects에 Item94effect 하나. Stat Modifiers는 비워둡니다.
// Halo Vfx Prefab 칸은 비워두면 노란 반투명 발광 구체를 코드로 만들어 씁니다. 직접 이펙트를 쓰려면 원본 이펙트 프리팹을 복제해서
// 넣으세요(크기 1 = 기본 총알에서 반지름 0.3인 후광이 되도록 복제본 크기를 맞추면 됩니다).
[System.Serializable]
public class Item94effect : IPassiveItemEffect, IRemovablePassiveEffect
{
    [Header("후광 범위 / 피해")]
    [Tooltip("기본 총알 크기일 때 후광 반지름입니다(총알 중심 기준, 월드 단위). 총알 크기가 바뀌면 같은 비율로 바뀝니다.")]
    [Min(0.01f)] public float haloRadius = 0.3f;

    [Tooltip("매초 주는 피해 = 총알 피해 x 이 비율입니다(기본 0.25 = 25%).")]
    [Min(0f)] public float damageRatioPerSecond = 0.25f;

    [Tooltip("피해를 주는 간격(초)입니다(기본 0.1초 - 한 번에 총알 피해 x 비율 x 간격만큼).")]
    [Min(0.02f)] public float tickInterval = 0.1f;

    [Tooltip("후광 피해를 받는 적의 태그입니다(4번 폭발/수류탄과 같은 기준).")]
    public string enemyTag = "Enemy";

    [Header("후광 연출")]
    [Tooltip("총알에 색 아이템이 없을 때의 후광 색입니다(기본 노란색). 알파(A)가 불투명도입니다.")]
    public Color defaultColor = new Color(1f, 0.9f, 0.3f, 0.35f);

    [Tooltip("총알 색을 따라갈 때의 후광 불투명도입니다(총알 색의 알파 대신 씀).")]
    [Range(0f, 1f)] public float tintedAlpha = 0.35f;

    [Tooltip("발광 세기입니다. 1보다 크면 색이 그만큼 밝아져(HDR) Bloom이 켜져 있으면 빛나 보입니다. 코드로 만든 구체에만 적용됩니다.")]
    [Min(0f)] public float glowIntensity = 2f;

    [Tooltip("후광 이펙트 프리팹입니다. 비워두면 노란 반투명 구체를 코드로 만들어 씁니다. 크기 1 = 기본 총알에서 반지름 Halo Radius인 후광입니다.")]
    public GameObject haloVfxPrefab;

    [Header("후광 외곽 발광 (외곽선처럼 바깥쪽이 은은하게 빛남)")]
    [Tooltip("끄면 외곽 발광 없이 후광만 보입니다.")]
    public bool showRimGlow = true;

    [Tooltip("발광이 후광 반지름의 몇 배까지 바깥으로 번지는지입니다(기본 1.5 = 후광 가장자리에서 반지름의 50%만큼 더 번짐).")]
    [Range(1.05f, 3f)] public float rimGlowSize = 1.5f;

    [Tooltip("발광이 후광 가장자리에서 안쪽으로 얼마나 들어오는지입니다(후광 반지름 대비 비율, 기본 0.15).")]
    [Range(0f, 1f)] public float rimGlowInnerWidth = 0.15f;

    [Tooltip("발광 세기입니다. 1보다 크면 색이 그만큼 밝아져(HDR) Bloom이 켜져 있으면 더 빛나 보입니다.")]
    [Min(0f)] public float rimGlowIntensity = 2.5f;

    [Tooltip("발광의 불투명도입니다(더하기 방식이라 값이 클수록 밝음).")]
    [Range(0f, 1f)] public float rimGlowAlpha = 0.8f;

    [Tooltip("비워두면 실행 중에 만듭니다. 빌드에서 분홍색으로 보이면 URP Particles/Unlit(Surface Type = Transparent, Blending Mode = Additive) 재질을 직접 넣으세요(발광 그림은 코드가 넣음).")]
    public Material rimGlowMaterial;

    private PlayerStats stats;

    public void Apply(PassiveEffectContext context, string sourceItemId)
    {
        if (context == null || context.PlayerObject == null)
        {
            Debug.LogWarning("Item94effect: 플레이어를 찾을 수 없어 94번 효과를 적용하지 못했습니다.");
            return;
        }

        PlayerDebuffImmunity.Block(sourceItemId, context.PlayerObject);

        stats = context.Stats != null ? context.Stats : context.PlayerObject.GetComponent<PlayerStats>();

        if (context.Shooting != null)
        {
            context.Shooting.OnProjectileSpawned -= HandleProjectileSpawned; // 혹시 남아 있을 이전 구독 정리(중복 방지)
            context.Shooting.OnProjectileSpawned += HandleProjectileSpawned;
        }
    }

    // [패시브 잃기 - 48번 등] 소지 중에만 유지되던 효과를 되돌립니다(IRemovablePassiveEffect.cs 참고).
    public void Remove(PassiveEffectContext context, string sourceItemId)
    {
        PlayerDebuffImmunity.Unblock(sourceItemId);
        if (context?.Shooting != null) context.Shooting.OnProjectileSpawned -= HandleProjectileSpawned;
    }

    private void HandleProjectileSpawned(Projectile projectile)
    {
        if (projectile == null) return;

        try
        {
            // "기본 총알 크기" = 아이템 보정 전 총알 크기 스탯 값입니다. 이 크기일 때 반지름이 정확히 Halo Radius가 됩니다.
            float baseBulletSize = stats != null && stats.bulletSize > 0f ? stats.bulletSize : 1f;
            BulletHalo.Attach(projectile, this, baseBulletSize);
        }
        catch (System.Exception e)
        {
            // 후광 오류로 총알 발사가 막히지 않도록 이 총알의 후광만 건너뜁니다.
            Debug.LogError($"Item94effect: 후광을 붙이는 중 오류가 발생해 이 총알은 후광 없이 날아갑니다: {e}");
        }
    }
}
