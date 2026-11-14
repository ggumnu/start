using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// [촬영 스튜디오 - 적 배우]
// 장면(StudioShot) 안에 손으로 놓아둔 적 프리팹(EnemyStats가 붙은 오브젝트)을 대본대로 움직이는 "꼭두각시"로 씁니다.
// 게임의 공격 부품(EnemyRadialAttack 등)은 그대로 두고, 스스로 판단하는 두뇌(BossAIController / EnemyAIController /
// EnemyRangedAIController)와 NavMeshAgent만 꺼서 대본이 시킨 것만 하게 합니다(촬영 씬에는 NavMesh가 없음).
//
// [원본과 복사본] 씬에 놓아둔 것은 "원본"으로 Play가 시작되면 숨기고, 장면을 재생할 때마다 원본을 복사해 그 복사본을 움직입니다.
// 다시 재생하면 복사본을 지우고 새로 만들기 때문에 충전 색/이펙트/위치가 항상 처음 상태에서 시작합니다.
//
// [물리] 보스 프리팹의 Rigidbody는 게임에서는 NavMeshAgent가 붙잡고 있어 문제가 없지만, 여기서는 그대로 두면 중력으로
// 떨어지거나 넘어지므로 물리 영향을 받지 않게(isKinematic) 바꿉니다. 원본 프리팹 파일은 건드리지 않습니다.
//
// [총알과 플레이어] 촬영 중에는 적 총알이 플레이어에 맞지 않고 그대로 지나갑니다(피격 점멸/표정/이펙트가 장면을 망치지 않도록).
// 새로 생긴 총알마다 플레이어 콜라이더와의 충돌을 끄는 방식이라 게임 쪽 총알/체력 스크립트는 건드리지 않습니다.
//
// [명령이 한 프레임 늦게 적용되는 이유] 대본의 0초 명령은 재생 준비 단계(녹화 시작 전 대기)에서 미리 읽히므로,
// 받은 명령을 모아 두었다가 장면 시간이 실제로 흐르기 시작한 뒤에 적용합니다.
public class StudioEnemyActors
{
    // 충전만 보여 줄 때(확산충전) 발사 직전에 공격 부품을 멈추는 여유 시간의 최솟값(초)
    private const float HoldMargin = 0.02f;
    // 충전만 보여 줄 때 충전 이펙트가 장면 도중에 사라지지 않도록 더하는 시간(초)
    private const float HoldVfxExtraLifetime = 600f;

    private class Puppet
    {
        public string name;
        public GameObject root;
        public EnemyRadialAttack radial;
        public BossShield shield;
        public BossLv2AIController lv2;   // 포격 모션/조준 표시 설정을 읽어 오는 곳(두뇌는 꺼져 있음)

        // 포격 발동 모션(BossLv2AIController와 같은 값/순서를 여기서 재연)
        public int launchStage;           // 0 없음, 1 들기, 2 돌며 발사, 3 내리기
        public float launchWeight, launchSpin, launchSpinAtLower, launchTimer;
        public int launchShellsFired;
        public bool launchBaseSaved;
        public bool launchForever;        // '포격모션 계속': 내리지 않고 계속 돎
        public float launchSpinScale = 1f; // '회전 N배': 도는 빠르기 배율
        public Quaternion launchBaseRotation;

        public bool holdingCharge;     // 확산충전 중(발사 직전에 멈출 예정)
        public float chargeLeft;

        public bool turning;
        public Quaternion turnFrom, turnTo;
        public float turnTime, turnDuration;

        public bool moving;
        public List<Vector3> path;
        public float moveTime, moveDuration;
    }

    private readonly Camera cam;
    private readonly List<Puppet> puppets = new List<Puppet>();
    private readonly List<ShotCue> pending = new List<ShotCue>();
    private StudioShot shot;

    // '보기 플레이어' / 총알 통과용 - 플레이어가 없는 장면이면 null
    public Transform PlayerRoot;

    private Transform colliderOwner;
    private Collider[] playerColliders = new Collider[0];
    private readonly HashSet<int> passedBullets = new HashSet<int>();

    public StudioEnemyActors(Camera cam)
    {
        this.cam = cam;
    }

    public bool HasActors { get { return puppets.Count > 0; } }

    // Play가 시작될 때 한 번: 모든 장면의 원본 적을 꼭두각시 상태로 바꾸고 숨깁니다.
    public static void HideTemplates(IList<StudioShot> shots)
    {
        foreach (StudioShot s in shots)
        {
            foreach (EnemyStats stats in s.GetComponentsInChildren<EnemyStats>(true))
            {
                MakePuppet(stats.gameObject);
                stats.gameObject.SetActive(false);
            }
        }
    }

    // 두뇌와 길찾기를 끄고 물리 영향을 막습니다.
    private static void MakePuppet(GameObject go)
    {
        foreach (BossAIController brain in go.GetComponents<BossAIController>()) brain.enabled = false;
        foreach (EnemyAIController brain in go.GetComponents<EnemyAIController>()) brain.enabled = false;
        foreach (EnemyRangedAIController brain in go.GetComponents<EnemyRangedAIController>()) brain.enabled = false;

        var agent = go.GetComponent<NavMeshAgent>();
        if (agent != null) agent.enabled = false;

        var body = go.GetComponent<Rigidbody>();
        if (body != null) body.isKinematic = true;
    }

    // 장면을 (다시) 재생하기 직전: 앞 재생의 복사본과 날아다니던 적 탄환을 지우고, 이 장면의 원본들을 새로 복사합니다.
    public void ResetForShot(StudioShot newShot)
    {
        Clear();
        shot = newShot;
        if (shot == null) return;

        foreach (EnemyStats template in shot.GetComponentsInChildren<EnemyStats>(true))
        {
            GameObject source = template.gameObject;
            if (source.activeSelf) continue; // 원본은 HideTemplates가 꺼 두었습니다. 켜져 있는 것은 원본이 아님.

            GameObject copy = Object.Instantiate(source, source.transform.parent);
            copy.name = source.name;
            copy.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            MakePuppet(copy);
            copy.SetActive(true);

            // 게임에서는 스포너와 두뇌가 해 주는 준비입니다(안 하면 체력 0, 탄환이 안 나감).
            var stats = copy.GetComponent<EnemyStats>();
            var health = copy.GetComponent<EnemyHealth>();
            if (health != null) health.Initialize(stats);
            var radial = copy.GetComponent<EnemyRadialAttack>();
            if (radial != null) radial.Initialize(stats, copy.transform);

            puppets.Add(new Puppet { name = copy.name, root = copy, radial = radial, shield = copy.GetComponent<BossShield>(), lv2 = copy.GetComponent<BossLv2AIController>() });
        }
    }

    public void Clear()
    {
        pending.Clear();
        passedBullets.Clear();
        foreach (Puppet p in puppets)
        {
            if (p.root != null) Object.Destroy(p.root);
        }
        puppets.Clear();

        // 포격 연출로 생긴 것들(보스 밖에 따로 생김)
        foreach (BossArtilleryStrike strike in Object.FindObjectsByType<BossArtilleryStrike>(FindObjectsSortMode.None))
        {
            if (strike != null) Object.Destroy(strike.gameObject);
        }
        foreach (BossArtilleryLaunchShell shell in Object.FindObjectsByType<BossArtilleryLaunchShell>(FindObjectsSortMode.None))
        {
            if (shell != null) Object.Destroy(shell.gameObject);
        }

        // 지우는 동안 목록이 바뀌므로 복사해 두고 지웁니다.
        foreach (EnemyProjectile bullet in new List<EnemyProjectile>(EnemyProjectile.Active))
        {
            if (bullet != null) Object.Destroy(bullet.gameObject);
        }
    }

    // 카메라의 대상 이름(바라보기/돌기/따라가기/궤도)으로 적 배우를 쓸 때 찾습니다. 없으면 null.
    public Transform FindRoot(string name)
    {
        Puppet p = Find(name);
        return p != null && p.root != null ? p.root.transform : null;
    }

    private Puppet Find(string name)
    {
        foreach (Puppet p in puppets)
        {
            if (p.root != null && string.Equals(p.name, name, System.StringComparison.OrdinalIgnoreCase)) return p;
        }
        // 장면에 적이 하나뿐이면 긴 프리팹 이름 대신 '보스' / '적'이라고만 적어도 됩니다.
        if (puppets.Count == 1 && puppets[0].root != null && (name == "보스" || name == "적")) return puppets[0];
        return null;
    }

    public void Execute(ShotCue cue)
    {
        // 시작 모습을 정하는 명령은 녹화 시작 전 대기 동안 미리 적용해, 첫 프레임부터 그 모습이 되게 합니다.
        bool atStart = cue.type == ShotCueType.ActorShieldOn || (cue.type == ShotCueType.ActorLaunch && cue.hold);
        if (atStart)
        {
            Puppet p = Find(cue.actor);
            if (p == null) Warn(cue, "'" + cue.actor + "'(이)라는 적 배우를 이 장면에서 찾지 못해 이 줄을 건너뜁니다. 지금 이 장면의 적 배우: " + ActorNames());
            else if (cue.type == ShotCueType.ActorShieldOn) ShieldOnAtStart(p, cue);
            else BeginLaunch(p, cue);
            return;
        }
        pending.Add(cue);
    }

    // 매 프레임(공격 부품들의 Update보다 늦게) 불립니다. advance = 장면 시간이 흐르는 중.
    public void Tick(float deltaTime, bool advance)
    {
        LetBulletsPassPlayer();

        if (!advance)
        {
            // 장면이 끝나 시간이 멈췄는데 충전만 보여 주던 적이 뒤늦게 쏘지 않도록 바로 멈춰 둡니다.
            foreach (Puppet p in puppets)
            {
                if (p.root == null || !p.holdingCharge || p.radial == null) continue;
                p.radial.enabled = false;
                p.holdingCharge = false;
            }
            return;
        }

        foreach (Puppet p in puppets)
        {
            if (p.root == null) continue;
            TickHold(p, deltaTime);
            TickTurn(p, deltaTime);
            TickMove(p, deltaTime);
            TickLaunch(p, deltaTime);
        }

        if (pending.Count > 0)
        {
            var cues = new List<ShotCue>(pending);
            pending.Clear();
            foreach (ShotCue cue in cues) Apply(cue);
        }
    }

    // 총알은 공격 부품의 Update에서 생기고 다음 물리 단계에서 처음 부딪히므로, 같은 프레임의 이 시점에 끄면 늦지 않습니다.
    private void LetBulletsPassPlayer()
    {
        if (PlayerRoot == null) return;
        if (colliderOwner != PlayerRoot)
        {
            colliderOwner = PlayerRoot;
            playerColliders = PlayerRoot.GetComponentsInChildren<Collider>(true);
            passedBullets.Clear();
        }

        IReadOnlyList<EnemyProjectile> bullets = EnemyProjectile.Active;
        for (int i = 0; i < bullets.Count; i++)
        {
            EnemyProjectile bullet = bullets[i];
            if (bullet == null || !passedBullets.Add(bullet.GetInstanceID())) continue;
            foreach (Collider bulletCollider in bullet.GetComponentsInChildren<Collider>(true))
            {
                foreach (Collider playerCollider in playerColliders)
                {
                    if (playerCollider != null) Physics.IgnoreCollision(bulletCollider, playerCollider, true);
                }
            }
        }
    }

    private void Apply(ShotCue cue)
    {
        Puppet p = Find(cue.actor);
        if (p == null)
        {
            Warn(cue, "'" + cue.actor + "'(이)라는 적 배우를 이 장면에서 찾지 못해 이 줄을 건너뜁니다. 지금 이 장면의 적 배우: " + ActorNames());
            return;
        }

        switch (cue.type)
        {
            case ShotCueType.ActorRadial: BeginRadial(p, cue); break;
            case ShotCueType.ActorCancel: CancelAttack(p); break;
            case ShotCueType.ActorLook: BeginTurn(p, cue); break;
            case ShotCueType.ActorMove: BeginMove(p, cue); break;
            case ShotCueType.ActorShield: BeginShield(p, cue); break;
            case ShotCueType.ActorLaunch: BeginLaunch(p, cue); break;
            case ShotCueType.ActorStrikeMark: ShowStrikeMark(p, cue); break;
        }
    }

    private string ActorNames()
    {
        if (puppets.Count == 0) return "(없음 - 장면의 Props 안에 적 프리팹을 놓고 씬을 저장했는지 확인해주세요)";
        var names = new List<string>();
        foreach (Puppet p in puppets) names.Add(p.name);
        return string.Join(", ", names.ToArray());
    }

    // ---------- 전방위(확산) 공격 ----------

    private void BeginRadial(Puppet p, ShotCue cue)
    {
        if (p.radial == null)
        {
            Warn(cue, "'" + p.name + "'에는 전방위 공격(EnemyRadialAttack)이 없어 이 줄을 건너뜁니다.");
            return;
        }

        p.radial.enabled = true;
        p.holdingCharge = false;

        bool resume = cue.remain > 0f && cue.duration > 0f;
        if (cue.duration > 0f)
        {
            p.radial.chargeDuration = cue.duration;
            // 충전 이펙트는 정해진 시간 뒤 스스로 사라지므로, 충전보다 먼저 사라지지 않게 늘립니다.
            if (p.radial.chargeVfx != null)
            {
                float need = cue.duration + (cue.hold ? HoldVfxExtraLifetime : 0.5f);
                if (p.radial.chargeVfx.lifetime < need) p.radial.chargeVfx.lifetime = need;
            }
        }

        if (!p.radial.BeginAttack(p.root.transform.position + p.root.transform.forward))
        {
            Warn(cue, "'" + p.name + "'이(가) 앞 공격을 아직 끝내지 못해 새 공격을 시작하지 못했습니다(공격 사이 간격을 늘려주세요).");
            return;
        }

        // 앞 장면의 충전 끝 모습에서 이어 시작: 이미 지난 충전 시간만큼 색/남은 시간/이펙트를 한 번에 맞춥니다.
        if (resume) p.radial.SkipChargeTime(cue.duration - cue.remain);

        if (cue.hold)
        {
            p.holdingCharge = true;
            p.chargeLeft = cue.duration;
        }
    }

    // 확산충전: 공격 부품이 충전을 끝내고 쏘기 직전에 그 부품을 멈춰, 충전이 끝난 모습 그대로 둡니다.
    private void TickHold(Puppet p, float deltaTime)
    {
        if (!p.holdingCharge || p.radial == null) return;

        // 공격 부품이 이번 프레임에 깎은 만큼 똑같이 깎습니다(충전을 시작한 프레임에는 이 함수가 시작보다 먼저 불려 깎지 않음).
        p.chargeLeft -= deltaTime;

        if (p.chargeLeft <= Mathf.Max(HoldMargin, deltaTime * 2f))
        {
            p.radial.enabled = false;
            p.holdingCharge = false;
        }
    }

    // ---------- 보호막 (2레벨 보스) ----------

    // 게임에서는 두뇌(BossLv2AIController)가 보스방에 처음 들어왔을 때 부르는 것과 같은 함수를 부릅니다.
    // 보호막이 다 차면 게임에서는 두뇌가 포격 발동 모션으로 넘어가지만, 여기서는 두뇌가 꺼져 있어 보호막만 생기고 끝납니다.
    private void BeginShield(Puppet p, ShotCue cue)
    {
        if (p.shield == null)
        {
            Warn(cue, "'" + p.name + "'에는 보호막(BossShield)이 없어 이 줄을 건너뜁니다(2레벨 보스만 보호막이 있습니다).");
            return;
        }

        if (cue.duration > 0f)
        {
            // 충전 이펙트는 정해진 시간 뒤 스스로 사라지므로, 게임과 같이 충전보다 조금 더 오래 남게 맞춥니다.
            float extra = Mathf.Max(0f, p.shield.chargeVfxLifetime - p.shield.chargeDuration);
            p.shield.chargeDuration = cue.duration;
            p.shield.chargeVfxLifetime = cue.duration + extra;
        }

        if (!p.shield.BeginCharge())
        {
            Warn(cue, "'" + p.name + "'이(가) 이미 보호막을 충전 중이거나 보호막이 있어 새로 만들지 못했습니다.");
        }
    }

    // 보호막이 이미 다 생긴 모습으로 시작: 충전 이펙트와 다 찼을 때의 번쩍임을 끄고 아주 짧게 충전시켜,
    // 시작 전 대기(0.5초) 동안 보호막이 완성되게 합니다. 복사본의 값만 바꾸므로 프리팹은 그대로입니다.
    private void ShieldOnAtStart(Puppet p, ShotCue cue)
    {
        if (p.shield == null)
        {
            Warn(cue, "'" + p.name + "'에는 보호막(BossShield)이 없어 이 줄을 건너뜁니다(2레벨 보스만 보호막이 있습니다).");
            return;
        }
        p.shield.chargeVfxPrefab = null;
        p.shield.gainFlashIntensity = 0f;
        p.shield.chargeDuration = 0.05f;
        if (!p.shield.BeginCharge())
        {
            Warn(cue, "'" + p.name + "'이(가) 이미 보호막을 충전 중이거나 보호막이 있어 새로 만들지 못했습니다.");
        }
    }

    // ---------- 포격 발동 모션 (2레벨 보스) ----------
    // 게임에서는 보호막이 다 찬 순간 두뇌(BossLv2AIController)가 하는 모션입니다. 두뇌는 꺼 두었으므로 같은 설정 값으로
    // 같은 순서(들기 -> 돌며 수류탄 쏘아 올리기 -> 내리기)를 여기서 재연합니다. 게임처럼 세상 시간으로 진행해 슬로모션이 그대로 먹습니다.
    // 실제 포격(조준 -> 폭발)은 시작하지 않습니다. 조준 표시는 '조준표시' 명령으로 원하는 자리에 따로 띄웁니다.

    private void BeginLaunch(Puppet p, ShotCue cue)
    {
        if (p.lv2 == null)
        {
            Warn(cue, "'" + p.name + "'에는 2레벨 보스 설정(BossLv2AIController)이 없어 이 줄을 건너뜁니다.");
            return;
        }
        if (p.lv2.groggyTiltTarget == null)
        {
            Warn(cue, "'" + p.name + "'의 모델(Groggy Tilt Target)을 찾지 못해 몸을 들지 못합니다. 수류탄만 쏘아 올립니다.");
        }
        p.launchStage = 1;
        p.launchTimer = 0f;
        p.launchShellsFired = 0;
        p.launchForever = cue.hold;
        p.launchSpinScale = cue.value > 0f ? cue.value : 1f;

        // '계속': 첫 프레임부터 하늘로 든 채 도는 모습(들기 단계 없음). 수류탄은 장면 시간이 흐르기 시작하면 쏩니다.
        if (cue.hold)
        {
            p.launchStage = 2;
            p.launchWeight = 1f;
            ApplyLaunchPose(p);
        }
    }

    private void TickLaunch(Puppet p, float dt)
    {
        if (p.launchStage == 0 || p.lv2 == null) return;
        BossLv2AIController cfg = p.lv2;
        float raise = Mathf.Max(cfg.launchRaiseTime, 0.01f);

        switch (p.launchStage)
        {
            case 1:
                p.launchWeight = Mathf.MoveTowards(p.launchWeight, 1f, dt / raise);
                if (p.launchWeight >= 1f)
                {
                    p.launchStage = 2;
                    p.launchTimer = 0f;
                }
                break;

            case 2:
            {
                p.launchTimer += dt;
                p.launchSpin += cfg.launchSpinSpeed * p.launchSpinScale * dt;

                int count = Mathf.Max(cfg.launchShellCount, 0);
                float duration = Mathf.Max(cfg.launchFireDuration, 0f);
                int due = count;
                if (count > 1 && duration > 0f)
                {
                    int byTime = 1 + Mathf.FloorToInt(p.launchTimer / (duration / (count - 1)));
                    // '계속'이면 게임과 같은 간격으로 수류탄을 끝없이 쏘아 올림
                    due = p.launchForever ? byTime : Mathf.Min(count, byTime);
                }
                while (p.launchShellsFired < due)
                {
                    LaunchShell(p);
                    p.launchShellsFired++;
                }

                if (!p.launchForever && p.launchTimer >= duration && p.launchShellsFired >= count)
                {
                    p.launchStage = 3;
                    p.launchSpinAtLower = Mathf.Repeat(p.launchSpin, 360f);
                }
                break;
            }

            case 3:
                p.launchWeight = Mathf.MoveTowards(p.launchWeight, 0f, dt / raise);
                p.launchSpin = Mathf.LerpAngle(p.launchSpinAtLower, 0f, 1f - p.launchWeight);
                break;
        }

        ApplyLaunchPose(p);
        if (p.launchStage == 3 && p.launchWeight <= 0f)
        {
            p.launchStage = 0;
            p.launchSpin = 0f;
        }
    }

    // 모델을 보스 루트의 오른쪽 축으로 들고(앞 -> 위) 위쪽 축으로 돌립니다(게임과 같은 식). 끝나면 원래 자세로.
    private static void ApplyLaunchPose(Puppet p)
    {
        Transform model = p.lv2.groggyTiltTarget;
        if (model == null) return;
        if (!p.launchBaseSaved)
        {
            p.launchBaseRotation = model.localRotation;
            p.launchBaseSaved = true;
        }
        if (p.launchWeight <= 0f && p.launchStage != 2)
        {
            model.localRotation = p.launchBaseRotation;
            p.launchBaseSaved = false;
            return;
        }
        Quaternion pose = Quaternion.AngleAxis(p.launchSpin, Vector3.up) * Quaternion.AngleAxis(-p.lv2.launchPitchAngle * p.launchWeight, Vector3.right);
        model.localRotation = pose * p.launchBaseRotation;
    }

    // 모델 꼭대기(지금 자세 기준)에서 거의 수직으로 수류탄 모델을 쏘아 올립니다(게임과 같은 식).
    private static void LaunchShell(Puppet p)
    {
        BossLv2AIController cfg = p.lv2;
        Vector3 origin = p.root.transform.position + Vector3.up;
        Transform model = cfg.groggyTiltTarget != null ? cfg.groggyTiltTarget : p.root.transform;
        bool any = false;
        Bounds b = default;
        foreach (Renderer r in model.GetComponentsInChildren<Renderer>())
        {
            if (r == null || !r.enabled) continue;
            if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;
            if (any) b.Encapsulate(r.bounds);
            else { b = r.bounds; any = true; }
        }
        if (any) origin = b.center + Vector3.up * b.extents.y;

        Vector3 dir = Quaternion.AngleAxis(Random.Range(0f, 360f), Vector3.up)
                    * Quaternion.AngleAxis(Random.Range(0f, cfg.launchShellSpread), Vector3.right) * Vector3.up;
        BossArtilleryLaunchShell.Launch(cfg.artilleryShellPrefab, cfg.launchShellScale, origin, dir, cfg.launchShellSpeed, cfg.launchShellDespawnHeight);
    }

    // ---------- 포격 조준 표시 (2레벨 보스) ----------
    // 게임의 포격 하나(BossArtilleryStrike)를 피해 0으로 만들되, 조준 시간을 아주 길게 해서 포탄이 떨어지거나 터지기 전에
    // 장면이 끝나게 합니다(바닥 원 + 레이저만 보임). 설정은 복사해서 바꾸므로 보스의 원래 설정은 그대로입니다.
    private const float MarkOnlyTelegraphTime = 1000f;

    private void ShowStrikeMark(Puppet p, ShotCue cue)
    {
        if (p.lv2 == null)
        {
            Warn(cue, "'" + p.name + "'에는 2레벨 보스 설정(BossLv2AIController)이 없어 조준 표시를 띄우지 못합니다.");
            return;
        }
        ShotMark mark = shot != null ? shot.FindMark(cue.target) : null;
        if (mark == null)
        {
            Warn(cue, "'" + cue.target + "' 표시를 이 장면의 Marks에서 찾지 못해 이 줄을 건너뜁니다.");
            return;
        }

        var settings = new BossArtilleryStrike.Settings();
        JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(p.lv2.artillery), settings);
        settings.telegraphTime = MarkOnlyTelegraphTime;

        var health = p.root.GetComponent<EnemyHealth>();
        BossArtilleryStrike.Create(settings, health, mark.transform.position, p.lv2.artilleryRadius, 0f, p.lv2.artilleryShellPrefab);
    }

    private void CancelAttack(Puppet p)
    {
        p.holdingCharge = false;
        if (p.radial == null) return;
        p.radial.enabled = true;
        p.radial.CancelAttack();
    }

    // ---------- 보기 ----------

    private void BeginTurn(Puppet p, ShotCue cue)
    {
        Transform target = ResolveTarget(cue.target, cue);
        if (target == null) return;

        Vector3 dir = target.position - p.root.transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;

        p.turnFrom = p.root.transform.rotation;
        p.turnTo = Quaternion.LookRotation(dir);
        p.turnTime = 0f;
        p.turnDuration = cue.duration;
        p.turning = true;
        if (p.turnDuration <= 0f)
        {
            p.root.transform.rotation = p.turnTo;
            p.turning = false;
        }
    }

    private void TickTurn(Puppet p, float deltaTime)
    {
        if (!p.turning) return;
        p.turnTime += deltaTime;
        float u = Mathf.Clamp01(p.turnTime / p.turnDuration);
        p.root.transform.rotation = Quaternion.Slerp(p.turnFrom, p.turnTo, ShotScript.Ease(ShotEase.Smooth, u));
        if (u >= 1f) p.turning = false;
    }

    private Transform ResolveTarget(string name, ShotCue cue)
    {
        if (name == ShotScript.PlayerName)
        {
            if (PlayerRoot == null) Warn(cue, "플레이어가 없어 이 줄을 건너뜁니다(대본에 '플레이어 등장'이 있는지 확인해주세요).");
            return PlayerRoot;
        }
        if (name == ShotScript.CameraName) return cam != null ? cam.transform : null;

        ShotMark mark = shot != null ? shot.FindMark(name) : null;
        if (mark == null) Warn(cue, "'" + name + "' 표시를 이 장면의 Marks에서 찾지 못해 이 줄을 건너뜁니다.");
        return mark != null ? mark.transform : null;
    }

    // ---------- 이동 ----------

    private void BeginMove(Puppet p, ShotCue cue)
    {
        // 높이는 놓아둔 그대로 두고(공중에 떠 있는 드론) 바닥 위 자리만 옮깁니다.
        float y = p.root.transform.position.y;
        var path = new List<Vector3>();
        if (cue.fromCurrent) path.Add(p.root.transform.position);
        foreach (string markName in cue.marks)
        {
            ShotMark mark = shot != null ? shot.FindMark(markName) : null;
            if (mark == null)
            {
                Warn(cue, "'" + markName + "' 표시를 이 장면의 Marks에서 찾지 못해 이 줄을 건너뜁니다.");
                return;
            }
            Vector3 point = mark.transform.position;
            point.y = y;
            path.Add(point);
        }
        if (path.Count < 2) return;

        p.path = path;
        p.moveTime = 0f;
        p.moveDuration = Mathf.Max(0.01f, cue.duration);
        p.moving = true;
        p.root.transform.position = path[0];
    }

    private void TickMove(Puppet p, float deltaTime)
    {
        if (!p.moving) return;
        p.moveTime += deltaTime;
        float u = Mathf.Clamp01(p.moveTime / p.moveDuration);
        p.root.transform.position = PointOnPath(p.path, ShotScript.Ease(ShotEase.Smooth, u));
        if (u >= 1f) p.moving = false;
    }

    // 꺾인 길 전체 길이의 u(0~1) 지점
    private static Vector3 PointOnPath(List<Vector3> path, float u)
    {
        float total = 0f;
        for (int i = 1; i < path.Count; i++) total += (path[i] - path[i - 1]).magnitude;
        if (total <= 0.0001f) return path[path.Count - 1];

        float want = total * Mathf.Clamp01(u);
        for (int i = 1; i < path.Count; i++)
        {
            float segment = (path[i] - path[i - 1]).magnitude;
            if (want <= segment || i == path.Count - 1)
            {
                return segment > 0.0001f ? Vector3.Lerp(path[i - 1], path[i], Mathf.Clamp01(want / segment)) : path[i];
            }
            want -= segment;
        }
        return path[path.Count - 1];
    }

    private void Warn(ShotCue cue, string message)
    {
        Debug.LogError("[촬영] 장면 '" + (shot != null ? shot.name : "?") + "' 대본 " + cue.line + "번째 줄: " + message, shot);
    }
}
