using UnityEngine;

// [60번 Drone_Cherino] 60번 고유 동작(피격 시 확률로 보호막)을 맡는 러너입니다(기획/설정 칸은 Item60effect.cs의 ShieldDroneFamiliarEffect).
// 60번을 얻으면 플레이어에 붙고, 잃으면 ShieldDroneFamiliarEffect.Remove가 뗍니다.
// 드론 모습/따라가기/줄 서기/떠 있기/발동 자세/이펙트는 패밀리어 공통 부품 FamiliarDrone/FamiliarChain이 합니다.
//
// [피격 판정] PlayerHealth의 두 알림을 받습니다 - 체력이 깎였을 때(OnDamageTaken)와 보호막이 깨졌을 때(OnShieldBroken).
// 둘은 한 번의 피격에 하나만 울리므로(보호막이 있으면 보호막만 깎임) 피격 1번 = 판정 1번입니다.
public class Item60DroneRunner : MonoBehaviour
{
    private ShieldDroneFamiliarEffect settings;
    private PlayerHealth health;
    private FamiliarDrone drone;

    public void Init(ShieldDroneFamiliarEffect effectSettings)
    {
        settings = effectSettings;

        if (health == null)
        {
            health = GetComponent<PlayerHealth>();
            if (health != null)
            {
                health.OnDamageTaken -= HandleDamageTaken;
                health.OnDamageTaken += HandleDamageTaken;
                health.OnShieldBroken -= HandleShieldBroken;
                health.OnShieldBroken += HandleShieldBroken;
            }
            else
            {
                Debug.LogWarning("Item60DroneRunner: 플레이어에 PlayerHealth가 없어 60번 보호막 효과가 동작하지 않습니다.");
            }
        }

        if (drone == null || !drone.Exists)
        {
            drone = new FamiliarDrone(transform);
            drone.motion = settings.BuildMotion();
            drone.Spawn(settings.droneModel, settings.modelScale, settings.modelRotationOffset, settings.appearDuration,
                        "Item60_CherinoDrone");
        }
    }

    public void ReleaseDrone(bool dissolve)
    {
        if (drone == null) return;
        drone.Release(dissolve);
        drone = null;
    }

    void OnDestroy()
    {
        if (health != null)
        {
            health.OnDamageTaken -= HandleDamageTaken;
            health.OnShieldBroken -= HandleShieldBroken;
        }
        // 러너가 지워질 때(플레이어 삭제/씬 종료 등) 드론이 홀로 남지 않게 합니다. 잃기(Remove)는 이미 ReleaseDrone을 부른 뒤라 아무 일도 없음.
        ReleaseDrone(false);
    }

    void Update()
    {
        if (settings == null || drone == null) return;
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        drone.motion = settings.BuildMotion(); // Play 중 Inspector 수정도 바로 반영
        drone.Tick(dt);
    }

    // 체력이 깎였을 때. 이 피해로 사망했으면 굴리지 않습니다(알림이 사망 처리보다 먼저 울리므로 남은 체력으로 판단).
    void HandleDamageTaken(float amount)
    {
        if (health == null || health.IsDead || health.CurrentHealth <= 0f) return;
        TryProc();
    }

    // 보호막이 깨졌을 때(형빈 확정 - 보호막이 깨질 때도 굴림).
    void HandleShieldBroken()
    {
        if (health == null || health.IsDead) return;
        TryProc();
    }

    void TryProc()
    {
        if (settings == null) return;
        try
        {
            if (!LuckOdds.Roll(settings.shieldChance)) return;

            // 기존 보호막 획득 이펙트/효과음과 같은 경로(연출 켬).
            health.AddShield(Mathf.Max(1, settings.shieldAmount), true);

            if (drone != null && drone.Exists)
            {
                drone.PlayPose();
                drone.SpawnBehindVfx(settings.procVfxLayers, settings.procColor,
                                     settings.procVfxLifetime, settings.procVfxFadeOutDuration);
            }
        }
        catch (System.Exception e)
        {
            // 연출/효과 문제가 피격 처리 자체를 막지 않도록 합니다.
            Debug.LogError($"Item60DroneRunner: 60번 보호막 발동 중 오류가 발생해 건너뜁니다: {e}");
        }
    }
}
