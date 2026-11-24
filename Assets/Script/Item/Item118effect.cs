using UnityEngine;

// 패시브 118번 - "총알이 적중하면 2초 뒤 그 자리에 포격을 해 범위 안 모든 대상에게 수류탄 피해를 입힌다."
//
// [기획 - 형빈 확정]
// - 플레이어 총알이 적에게 명중할 때마다(5번 분열탄, 99번 관통, 69번 튕김, 110번 충전탄, 19/137번 여러 발 모두 각각) 맞은 자리에 포격 하나.
//   맞은 적을 따라가지 않습니다. 그 총알로 적이 죽어도 포격은 합니다. 수류탄 개수는 쓰지 않습니다.
// - 포격 = 수류탄 폭발과 같은 처리입니다: 범위 안 모든 적에게 수류탄 피해, 범위 안이면 플레이어도 수류탄 자해 피해(57번 등 자해 무효 적용),
//   81번 소이탄/66번 해킹/57번 외형/특수방 수류탄 잠금 해제 모두 적용. 77번은 2초 조준 동안 유도, 92번은 폭발 2초 뒤 X자 4곳에 분열 포격.
//   115번이면 크레모어 포격(조준 원만 깔리고 수류탄 모드 R로 떨어짐). 자세한 규칙은 ArtilleryStrike.cs 참고.
// - 피해/범위/자해 = 총알이 맞은 순간의 수류탄 스탯(PlayerStats). 5번 분열탄이 맞혀 생긴 포격은 그 분열탄의 효과 배율(세대마다 50%)만큼
//   피해/범위/자해가 줄어듭니다(자해는 0.5 단위 올림).
// - 조준 표시: 빨간 반투명 원 + 빨간 테두리 + 안쪽 원 + 가운데 십자선, 원 중심에서 하늘로 빠르게 점멸하는 빨간 레이저. 마지막 0.5초 동안
//   수류탄 모델(1.2배)이 하늘에서 떨어져 땅에 닿는 순간 터집니다.
// - 플레이어가 다른 방에 들어가면 조준 중인 일반 포격은 취소됩니다(크레모어 포격은 남음 - 115번 규칙).
// - 118번을 잃으면(48번 등) 그 뒤로 맞히는 총알은 포격을 만들지 않습니다(이미 시작된 포격은 그대로 진행).
//
// [구현] Projectile.OnPlayerBulletHitEnemyDetailed(맞힌 총알 + 맞은 적)를 받아 ArtilleryStrike.Create를 부릅니다.
// [3레벨 보스 그로기 비콘 - 형빈 확정] 비콘(어느 종류든)에 맞은 총알도 그 자리에 포격합니다(Projectile.OnPlayerBulletHitBeacon) -
// 포격은 수류탄 폭발과 같으므로 수류탄 비콘을 해제할 수 있습니다.
//
// [아이템 에셋 설정]
// ItemDefinition: Item Id = 118, Category = Passive, Special Effects에 ArtilleryStrikeEffect 하나. Stat Modifiers는 비워둡니다.
[System.Serializable]
public class ArtilleryStrikeEffect : IPassiveItemEffect, IRemovablePassiveEffect
{
    [Tooltip("포격 시간/포탄/조준 표시/레이저 설정입니다.")]
    public ArtilleryStrike.Settings settings = new ArtilleryStrike.Settings();

    [System.NonSerialized] private bool subscribed;
    [System.NonSerialized] private GameObject owner;
    [System.NonSerialized] private PlayerGrenadeThrower thrower;

    public void Apply(PassiveEffectContext context, string sourceItemId)
    {
        if (context == null || context.PlayerObject == null)
        {
            Debug.LogWarning("ArtilleryStrikeEffect: 플레이어를 찾을 수 없어 118번 효과를 적용하지 못했습니다.");
            return;
        }

        Unsubscribe();
        owner = context.PlayerObject;
        thrower = owner.GetComponent<PlayerGrenadeThrower>();
        if (thrower == null)
        {
            Debug.LogWarning("ArtilleryStrikeEffect: 플레이어에 PlayerGrenadeThrower가 없어 포격(수류탄 스탯/폭발 이펙트)을 쓸 수 없습니다.");
        }
        if (settings == null) settings = new ArtilleryStrike.Settings();

        Projectile.OnPlayerBulletHitEnemyDetailed += OnBulletHit;
        Projectile.OnPlayerBulletHitBeacon += OnBulletHitBeacon;
        subscribed = true;
    }

    // [패시브 잃기 - 48번 등] 더 이상 포격을 만들지 않습니다(이미 시작된 포격은 그대로).
    public void Remove(PassiveEffectContext context, string sourceItemId)
    {
        Unsubscribe();
        owner = null;
        thrower = null;
    }

    private void Unsubscribe()
    {
        Projectile.OnPlayerBulletHitEnemyDetailed -= OnBulletHit;
        Projectile.OnPlayerBulletHitBeacon -= OnBulletHitBeacon;
        subscribed = false;
    }

    private void OnBulletHitBeacon(Projectile projectile, GroggyBeacon beacon) => StrikeAt(projectile);

    private void OnBulletHit(Projectile projectile, EnemyHealth enemy) => StrikeAt(projectile);

    // 맞힌 총알 자리에 포격 하나(적/비콘 공통).
    private void StrikeAt(Projectile projectile)
    {
        if (!subscribed) return;
        // 플레이어가 사라졌는데(이전 플레이 등) 구독이 남아 있으면 정리만 합니다.
        if (owner == null) { Unsubscribe(); return; }
        if (projectile == null) return;

        try
        {
            if (thrower == null) thrower = owner.GetComponent<PlayerGrenadeThrower>();
            if (thrower == null || thrower.stats == null) return;
            var stats = thrower.stats;

            // [5번 분열탄] 효과 배율(원본 1, 1세대 0.5, 2세대 0.25)만큼 피해/범위/자해를 줄입니다.
            float scale = Mathf.Clamp(projectile.EffectScale, 0f, 1f);
            if (scale <= 0f) return;

            float radius = Mathf.Max(stats.GrenadeRadius, 0f) * scale;
            if (radius <= 0f) return;
            float damage = stats.GrenadeDamage * scale;
            float self = stats.GrenadeSelfDamage;
            if (scale < 1f) self = self > 0f ? ArtilleryStrike.RoundUpToHalf(self * scale) : 0f;

            // 맞은 자리(총알 위치)의 바닥 = 플레이어 발 높이
            Vector3 point = projectile.transform.position;
            point.y = owner.transform.position.y;

            // 맞은 순간의 다른 수류탄 아이템 상태로 정합니다(던지는 순간 기준인 수류탄과 같음).
            var incendiary = IncendiaryGrenadeOverride.TryGetActive(out var inc) ? inc : null;
            var split = GrenadeSplitOverride.TryGetActive(out var sp) ? sp : null;
            var claymore = ClaymoreOverride.TryGetActive(out var cm) ? cm : null;
            RoomInstance room = claymore != null ? ClaymoreOverride.CurrentPlayerRoom() : null;

            ArtilleryStrike.Create(thrower, settings, point, projectile.Direction, damage, self, radius,
                                   incendiary, split, claymore, room);
        }
        catch (System.Exception e)
        {
            // 명중 알림 안에서 실행되므로, 오류가 나도 총알 처리가 막히지 않게 건너뜁니다.
            Debug.LogError($"ArtilleryStrikeEffect: 포격을 만드는 중 오류가 발생해 건너뜁니다: {e}");
        }
    }
}
