using UnityEngine;

// [59번 아테나 3호] 59번 고유 동작(해킹툴 전개 판정/쿨타임/회색)을 맡는 러너입니다(기획/설정 칸은 Item59effect.cs의 HackDroneFamiliarEffect).
// 59번을 얻으면 플레이어에 붙고, 잃으면 HackDroneFamiliarEffect.Remove가 뗍니다.
// 드론 모습/따라가기/떠 있기/발동 자세/이펙트 사라짐/잃을 때 사라짐은 패밀리어 공통 부품 FamiliarDrone(줄 서기는 FamiliarChain)이 합니다.
//
// [해킹툴 전개] 판정/전개는 PlayerHackTool의 범위 판정(CollectEnemiesInRange)과 DeployFromItem을 그대로 써서 직접 사용과 기준이 같습니다.
// 플레이어가 직접 해킹툴을 쓰는 동작 중(IsDeploying)에는 전개하지 않습니다 - 같은 순간 겹쳐 쿨타임을 낭비하지 않게 하고,
// 58번 보호막 연출(직접 사용의 발동 순간에 뜸)을 59번 전개가 먼저 가져가지 않게 하기 위함입니다.
public class Item59DroneRunner : MonoBehaviour
{
    private HackDroneFamiliarEffect settings;

    private PlayerHackTool hackTool;
    private PlayerHealth health;
    private FamiliarDrone drone;

    // 해킹 상태
    private float cooldownRemaining; // 0이면 전개 가능(획득 직후 바로 가능 - 형빈 확정)
    private float checkTimer;
    private float tintWeight;

    public bool IsReady => cooldownRemaining <= 0f;

    public void Init(HackDroneFamiliarEffect effectSettings)
    {
        settings = effectSettings;

        if (hackTool == null) hackTool = GetComponent<PlayerHackTool>();
        if (health == null) health = GetComponent<PlayerHealth>();
        if (hackTool == null)
        {
            Debug.LogWarning("Item59DroneRunner: 플레이어에 PlayerHackTool이 없어 59번 드론이 해킹툴을 전개하지 못합니다.");
        }

        // 이미 드론이 있으면(같은 러너에 설정만 다시 들어온 경우) 그대로 둡니다.
        if (drone == null || !drone.Exists)
        {
            drone = new FamiliarDrone(transform);
            drone.motion = settings.BuildMotion();
            drone.Spawn(settings.droneModel, settings.modelScale, settings.modelRotationOffset, settings.appearDuration,
                        "Item59_AthenaDrone");
        }
    }

    // 드론을 놓아줍니다. dissolve = 흰빛으로 물들며 사라지는 연출과 함께(아이템을 잃을 때), 아니면 바로 지움.
    public void ReleaseDrone(bool dissolve)
    {
        if (drone == null) return;
        drone.Release(dissolve);
        drone = null;
    }

    void OnDestroy()
    {
        // 러너가 지워질 때(플레이어 삭제/씬 종료 등) 드론이 홀로 남지 않게 합니다. 잃기(Remove)는 이미 ReleaseDrone을 부른 뒤라 아무 일도 없음.
        ReleaseDrone(false);
    }

    void Update()
    {
        if (settings == null) return;
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        if (drone != null)
        {
            drone.motion = settings.BuildMotion(); // Play 중 Inspector 수정도 바로 반영
            drone.Tick(dt);
        }

        if (cooldownRemaining > 0f) cooldownRemaining = Mathf.Max(0f, cooldownRemaining - dt);

        UpdateTint(dt);

        // 범위 확인은 일정 간격으로만 합니다.
        checkTimer -= dt;
        if (checkTimer <= 0f)
        {
            checkTimer = settings.checkInterval;
            if (IsReady && CanDeployNow() && HasWillfulEnemyInRange()) Deploy();
        }
    }

    // ---------------- 해킹툴 전개 ----------------

    bool CanDeployNow()
    {
        if (hackTool == null || !hackTool.isActiveAndEnabled) return false;
        if (health != null && health.IsDead) return false;
        if (hackTool.IsDeploying) return false;                       // 플레이어가 직접 쓰는 중(위 설명)
        if (PlayerItemPickupPresenter.BlocksActions) return false;    // 아이템 획득 연출 중
        if (HeliExtraction.BlocksInput || IntroCutscene.BlocksInput) return false;
        if (LoadingScreen.BlocksPlayerInput) return false;
        if (CutsceneStudio.IsActive) return false;
        if (drone != null && drone.IsAppearing) return false;         // 나타나는 중
        return true;
    }

    // 플레이어 해킹툴 범위 안에 "자기 뜻으로 움직이는" 적이 있는지 봅니다(기준은 Item59effect.cs [기획]).
    bool HasWillfulEnemyInRange()
    {
        // 59번 범위 = 플레이어 해킹툴 범위 x Range Multiplier(기본 75% - 원래 범위로는 너무 자주 전개됨, 형빈 확정).
        var enemies = hackTool.CollectEnemiesInRange(transform.position, hackTool.CurrentRadius * settings.EffectiveRangeMultiplier,
                                                     Mathf.Max(hackTool.effectHeight, 0.1f));
        foreach (var enemy in enemies)
        {
            if (IsActingOnOwnWill(enemy)) return true;
        }
        return false;
    }

    static bool IsActingOnOwnWill(EnemyHealth enemy)
    {
        if (enemy == null || enemy.IsDead || !enemy.isActiveAndEnabled) return false;
        GameObject go = enemy.gameObject;

        if (AllyFaction.IsAlly(go)) return false;
        if (EnemyControlState.IsStrongControlledFor(go)) return false; // 해킹/수면
        if (ForcedPush.IsBeingPushed(go)) return false;                // 넉백으로 밀리는 중

        // 매료는 강제로 다가오게 만드는 제어라 제외합니다. 보스는 매료돼도 감속만 받으므로(스스로 움직임) 셉니다.
        if (go.GetComponent<BossAIController>() == null)
        {
            var control = go.GetComponent<EnemyControlState>();
            if (control != null && control.NormalType != EnemyControlType.None) return false;
            var charm = go.GetComponent<EnemyCharmState>();
            if (charm != null && charm.IsCharmed) return false;
        }
        return true;
    }

    void Deploy()
    {
        // 중심은 플레이어(형빈 확정) - 조건이 된 적이 확실히 범위에 들어감. 효과음/특수방 해제/보스 보호막 추가 피해는
        // DeployFromItem이 보내는 OnHackDeployed를 받는 기존 코드가 처리합니다.
        hackTool.DeployFromItem(transform.position, settings.rangeOnlyForDetection ? 1f : settings.EffectiveRangeMultiplier);

        cooldownRemaining = settings.cooldown;
        if (drone != null)
        {
            drone.PlayPose();
            drone.AttachVfx(settings.shieldVfxPrefab, settings.shieldVfxOffset, settings.shieldVfxScale,
                            settings.shieldVfxLifetime, settings.EffectiveShieldFadeOut);
        }
    }

    // ---------------- 쿨타임 색 ----------------

    // 쿨타임 중(전개 자세가 끝난 뒤)에는 회색, 쿨타임이 끝나는 순간부터 원래 색으로 돌아옵니다(형빈 확정 - 끝날 때부터 복귀).
    void UpdateTint(float dt)
    {
        if (drone == null || !drone.Exists) return;

        bool wantGray = cooldownRemaining > 0f && !drone.IsPosing;
        float target = wantGray ? 1f : 0f;
        float duration = wantGray ? settings.grayInDuration : settings.restoreDuration;
        tintWeight = duration <= 0f ? target : Mathf.MoveTowards(tintWeight, target, dt / duration);

        drone.SetTint(settings.cooldownColor, tintWeight);
    }
}
