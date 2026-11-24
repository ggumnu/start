using System;
using System.Collections.Generic;
using UnityEngine;

// 이미 있는 게임 알림을 구독해 효과음을 냅니다. GameAudio가 자동으로 붙이므로 프리팹/씬 작업이 없고, 기존 스크립트도 고치지 않습니다.
// 플레이어는 FindFirstObjectByType<PlayerHealth>()로 찾고, 씬이 바뀌어 플레이어가 새로 생기면 다시 연결합니다.
// 연결: 사격/장전/무기 전환/수류탄 투척·폭발/해킹툴 전개/피격/보호막(막음·획득)/사망/적 피격·처치(보스 따로)/총알 벽 충돌/
// 전투 시작·방 클리어/보스방 입장 경고(GameAudio.PlayBossAlert - BGM 끊김)/보물상자 열기/플레이어 아이템 획득.
public class SoundEventHooks : MonoBehaviour
{
    // 플레이어가 막 생겼을 때 초기 무기 모드 설정이 "전환" 소리로 들리지 않게 이 시간 동안은 무시합니다.
    private const float WeaponSwitchMuteAfterBind = 0.5f;
    private const float PlayerSearchInterval = 0.5f;

    private PlayerHealth boundHealth;
    private PlayerShooting shooting;
    private PlayerGrenadeThrower thrower;
    private PlayerHackTool hackTool;
    private PlayerWeaponMode weaponMode;
    private float bindTime;
    private float nextSearchTime;

    void OnEnable()
    {
        PlayerHealth.OnAnyPlayerDied += HandlePlayerDied;
        Projectile.OnPlayerBulletHitEnemy += HandleBulletHitEnemy;
        EnemyHealth.OnAnyEnemyKilled += HandleEnemyKilled;
        RoomController.OnAnyRoomCleared += HandleRoomCleared;
        RoomController.OnAnyRoomEntered += HandleRoomEntered;
        ContainerOpenEvents.OnAnyContainerOpened += HandleContainerOpened;
        ItemPickupEvents.OnPlayerItemPickedUp += HandleItemPickedUp;
    }

    void OnDisable()
    {
        PlayerHealth.OnAnyPlayerDied -= HandlePlayerDied;
        Projectile.OnPlayerBulletHitEnemy -= HandleBulletHitEnemy;
        EnemyHealth.OnAnyEnemyKilled -= HandleEnemyKilled;
        RoomController.OnAnyRoomCleared -= HandleRoomCleared;
        RoomController.OnAnyRoomEntered -= HandleRoomEntered;
        ContainerOpenEvents.OnAnyContainerOpened -= HandleContainerOpened;
        ItemPickupEvents.OnPlayerItemPickedUp -= HandleItemPickedUp;
        Unbind();
    }

    void Update()
    {
        // 씬이 바뀌거나 R 재시작으로 플레이어가 사라지면(Unity null) 다시 찾습니다.
        if (boundHealth != null) return;
        if (Time.unscaledTime < nextSearchTime) return;
        nextSearchTime = Time.unscaledTime + PlayerSearchInterval;

        Unbind();
        var health = FindFirstObjectByType<PlayerHealth>();
        if (health != null) Bind(health);
    }

    private static T Find<T>(GameObject root) where T : Component
    {
        var c = root.GetComponent<T>();
        return c != null ? c : root.GetComponentInChildren<T>(true);
    }

    private void Bind(PlayerHealth health)
    {
        boundHealth = health;
        bindTime = Time.unscaledTime;
        GameObject root = health.gameObject;

        health.OnDamageTaken += HandleDamageTaken;
        health.OnShieldBroken += HandleShieldBroken;
        health.OnShieldGained += HandleShieldGained;

        shooting = Find<PlayerShooting>(root);
        if (shooting != null)
        {
            shooting.OnShotFired += HandleShotFired;
            shooting.OnReloadStarted += HandleReloadStarted;
            shooting.OnProjectileSpawned += HandleProjectileSpawned;
        }

        thrower = Find<PlayerGrenadeThrower>(root);
        if (thrower != null)
        {
            thrower.OnGrenadeThrown += HandleGrenadeThrown;
            thrower.OnGrenadeExploded += HandleGrenadeExploded;
        }

        hackTool = Find<PlayerHackTool>(root);
        if (hackTool != null) hackTool.OnHackDeployed += HandleHackDeployed;

        weaponMode = Find<PlayerWeaponMode>(root);
        if (weaponMode != null) weaponMode.OnModeChanged += HandleWeaponModeChanged;
    }

    // 사라진 플레이어의 컴포넌트도 C# 객체로는 남아 있어 구독 해제가 안전합니다.
    private void Unbind()
    {
        if (!ReferenceEquals(boundHealth, null))
        {
            boundHealth.OnDamageTaken -= HandleDamageTaken;
            boundHealth.OnShieldBroken -= HandleShieldBroken;
            boundHealth.OnShieldGained -= HandleShieldGained;
        }
        if (!ReferenceEquals(shooting, null))
        {
            shooting.OnShotFired -= HandleShotFired;
            shooting.OnReloadStarted -= HandleReloadStarted;
            shooting.OnProjectileSpawned -= HandleProjectileSpawned;
        }
        if (!ReferenceEquals(thrower, null))
        {
            thrower.OnGrenadeThrown -= HandleGrenadeThrown;
            thrower.OnGrenadeExploded -= HandleGrenadeExploded;
        }
        if (!ReferenceEquals(hackTool, null)) hackTool.OnHackDeployed -= HandleHackDeployed;
        if (!ReferenceEquals(weaponMode, null)) weaponMode.OnModeChanged -= HandleWeaponModeChanged;

        boundHealth = null;
        shooting = null;
        thrower = null;
        hackTool = null;
        weaponMode = null;
    }

    // ---- 플레이어 ----

    private void HandleDamageTaken(float amount) => GameAudio.Play(SfxId.PlayerHit);

    private void HandleShieldBroken() => GameAudio.Play(SfxId.ShieldBlock);

    private void HandlePlayerDied(PlayerHealth health) => GameAudio.Play(SfxId.PlayerDeath);

    private void HandleShieldGained(int gained, bool playVfx)
    {
        if (gained > 0) GameAudio.Play(SfxId.ShieldGain);
    }

    // 총알마다 소멸 알림을 구독합니다(총알이 사라지면 구독도 함께 사라짐 - CombatVfxSpawner와 같은 방식).
    private void HandleProjectileSpawned(Projectile projectile)
    {
        if (projectile != null) projectile.OnDespawning += HandleProjectileDespawning;
    }

    private void HandleProjectileDespawning(ProjectileDespawnEvent e)
    {
        if (e != null && e.Reason == ProjectileDespawnReason.HitWall) GameAudio.Play(SfxId.BulletHitWall);
    }

    private void HandleShotFired(float nextShotDelay) => GameAudio.Play(SfxId.GunShot);

    private void HandleReloadStarted() => GameAudio.Play(SfxId.ReloadStart);

    private void HandleWeaponModeChanged(PlayerWeapon weapon)
    {
        if (Time.unscaledTime - bindTime < WeaponSwitchMuteAfterBind) return;
        GameAudio.Play(SfxId.WeaponSwitch);
    }

    private void HandleGrenadeThrown(Grenade grenade) => GameAudio.Play(SfxId.GrenadeThrow);

    // 방 범위 규칙: 던지고 다른 방으로 나간 뒤 터진 수류탄은 소리를 내지 않습니다(118번 포격/115번 크레모어도 이 알림을 거침).
    private void HandleGrenadeExploded(Vector3 position, float radius)
    {
        RoomInstance room = thrower != null ? thrower.ExplodingRoom : null;
        if (room == null) room = RoomScope.RoomOf(position);
        if (!RoomScope.IsPlayerInRoom(room)) return;
        // [108번] 폭죽탄 폭발이면 폭죽 소리로 냅니다(PlayerGrenadeThrower.IsExplodingFirework).
        GameAudio.Play(thrower != null && thrower.IsExplodingFirework ? SfxId.Firework108Burst : SfxId.GrenadeExplode);
    }

    private void HandleHackDeployed(Vector3 position, float radius, IReadOnlyList<EnemyHealth> enemies)
    {
        GameAudio.Play(SfxId.HackDeploy);
    }

    // ---- 적 / 방 / 상자 ----

    private void HandleBulletHitEnemy(EnemyHealth enemy)
    {
        if (enemy == null || AllyFaction.IsAlly(enemy)) return;
        GameAudio.Play(SfxId.EnemyHit);
    }

    private void HandleEnemyKilled(EnemyHealth enemy, Vector3 position)
    {
        try
        {
            if (enemy != null && AllyFaction.IsAlly(enemy)) return;
            bool isBoss = enemy != null && enemy.GetComponent<BossAIController>() != null;
            GameAudio.Play(isBoss ? SfxId.BossKill : SfxId.EnemyKill);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }

    private void HandleRoomCleared(RoomInstance room) => GameAudio.Play(SfxId.RoomClear);

    // 아직 클리어하지 않은 전투방에 들어가 바리케이드가 닫히는 순간(RoomController.EnterRoom과 같은 조건).
    // 보스방은 전투 시작음 대신 보스방 입장 경고(BGM이 잠깐 끊겼다 처음부터 서서히 커짐 - 살아 있는 보스가 있을 때만).
    private void HandleRoomEntered(RoomInstance room)
    {
        if (room == null || room.isCleared) return;
        if (room.category == RoomCategory.Boss) GameAudio.PlayBossAlert();
        else if (room.category == RoomCategory.Normal) GameAudio.Play(SfxId.CombatStart);
    }

    private void HandleContainerOpened() => GameAudio.Play(SfxId.ChestOpen);

    private void HandleItemPickedUp(ItemDefinition item) => GameAudio.Play(SfxId.ItemPickup);
}
