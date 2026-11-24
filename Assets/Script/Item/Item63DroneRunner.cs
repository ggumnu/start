using System.Collections.Generic;
using UnityEngine;

// [63번 Drone_Shiroko] 63번 고유 동작(오른쪽 어깨 자리, 프로펠러, 목표 고르기, 순차 발사, 반동)을 맡는 러너입니다
// (기획/설정 칸은 Item63effect.cs의 MissileDroneFamiliarEffect). 63번을 얻으면 플레이어에 붙고, 잃으면 Remove가 뗍니다.
// 미사일 한 발은 플레이어 총알(Projectile 미사일 모드) + 유도 조종기 HomingMissile, 드론 모습/떠 있기/반동/프로펠러 회전은 FamiliarDrone이 합니다.
// [총알과 같은 취급 - 형빈 확정] 한 번에 쏘는 수 = Missiles Per Volley(4) x 연사(Multishot) - 19번/137번 갈래는 영향 없음.
public class Item63DroneRunner : MonoBehaviour
{
    private MissileDroneFamiliarEffect settings;
    private PlayerHealth health;
    private PlayerStats stats;
    private PlayerShooting shooting;
    private FamiliarDrone drone;

    private Transform firePointLeft;
    private Transform firePointRight;

    private Vector3 lastPlayerPos;
    private bool hasLastPlayerPos;

    // 발사 상태
    private float cooldownRemaining;     // 0이면 준비됨(획득 직후 바로 준비 - 적이 있으면 쏨)
    private float entryDelayRemaining;   // 방 입장 직후 발사 대기
    private readonly List<Component> volleyTargets = new List<Component>(); // 적 또는 3레벨 보스 총알 비콘
    private int volleyShotsLeft;
    private int volleyShotIndex;
    private float shotTimer;

    private readonly List<Component> candidates = new List<Component>();

    public void Init(MissileDroneFamiliarEffect effectSettings)
    {
        settings = effectSettings;
        if (health == null) health = GetComponent<PlayerHealth>();
        if (stats == null) stats = GetComponent<PlayerStats>();
        if (shooting == null) shooting = GetComponent<PlayerShooting>();
        if (shooting == null) Debug.LogWarning("Item63DroneRunner: 플레이어에 PlayerShooting이 없어 63번 미사일을 쏘지 못합니다.");

        RoomController.OnAnyRoomEntered -= HandleRoomEntered;
        RoomController.OnAnyRoomEntered += HandleRoomEntered;

        if (drone == null || !drone.Exists)
        {
            drone = new FamiliarDrone(transform);
            drone.motion = settings.BuildMotion();
            drone.Spawn(settings.droneModel, settings.modelScale, settings.modelRotationOffset, settings.appearDuration,
                        "Item63_ShirokoDrone", false, AnchorPosition());
            drone.SetSpinningParts(settings.propellerNames, settings.propellerAxis, settings.propellerSpeed);

            firePointLeft = drone.FindModelChild(settings.firePointLeftName);
            firePointRight = drone.FindModelChild(settings.firePointRightName);
            if (firePointLeft == null || firePointRight == null)
            {
                Debug.LogWarning($"Item63DroneRunner: 드론 모델에서 '{settings.firePointLeftName}'/'{settings.firePointRightName}'를 찾지 못해 " +
                                 "드론 중심 좌우(Fallback Side Offset)에서 발사합니다.");
            }
        }
        hasLastPlayerPos = false;
    }

    public void ReleaseDrone(bool dissolve)
    {
        volleyShotsLeft = 0;
        volleyTargets.Clear();
        if (drone == null) return;
        drone.Release(dissolve);
        drone = null;
        firePointLeft = null;
        firePointRight = null;
    }

    void OnDestroy()
    {
        RoomController.OnAnyRoomEntered -= HandleRoomEntered; // 정적 알림이라 반드시 해제
        ReleaseDrone(false);
    }

    void HandleRoomEntered(RoomInstance room)
    {
        if (settings == null) return;
        entryDelayRemaining = settings.roomEntryFireDelay;
        // 방을 옮기면 쏘던 중인 발사는 멈춥니다(남은 발은 버림 - 새 방에서 다시 준비되면 새로 쏨).
        volleyShotsLeft = 0;
        volleyTargets.Clear();
    }

    // 플레이어가 바라보는 방향 기준 자리(오른쪽 머리 위, 살짝 뒤).
    Vector3 AnchorPosition()
    {
        Vector3 forward = transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
        forward.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        Vector3 o = settings != null ? settings.anchorOffset : Vector3.zero;
        return transform.position + right * o.x + Vector3.up * o.y + forward * o.z;
    }

    void Update()
    {
        if (settings == null) return;
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        if (drone != null)
        {
            drone.motion = settings.BuildMotion(); // Play 중 Inspector 수정도 바로 반영
            drone.SetSpinSpeed(settings.propellerAxis, settings.propellerSpeed);
            drone.Tick(dt);
        }

        if (cooldownRemaining > 0f) cooldownRemaining = Mathf.Max(0f, cooldownRemaining - dt);
        if (entryDelayRemaining > 0f) entryDelayRemaining = Mathf.Max(0f, entryDelayRemaining - dt);

        if (volleyShotsLeft > 0)
        {
            UpdateVolley(dt);
            return;
        }

        if (cooldownRemaining <= 0f && entryDelayRemaining <= 0f && CanFireNow()) TryStartVolley();
    }

    // 플레이어 이동(Update)이 끝난 뒤 자리를 따라가도록 LateUpdate에서 움직입니다.
    void LateUpdate()
    {
        if (settings == null || drone == null) return;
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        Vector3 playerPos = transform.position;
        bool snap = !hasLastPlayerPos || (playerPos - lastPlayerPos).magnitude >= Mathf.Max(settings.snapDistance, 0.5f);
        lastPlayerPos = playerPos;
        hasLastPlayerPos = true;

        drone.FollowAnchored(AnchorPosition(), transform.forward, snap, dt, settings.EffectiveMovingMaxDistance);
    }

    bool CanFireNow()
    {
        if (drone == null || !drone.Exists || drone.IsAppearing) return false;
        if (health != null && health.IsDead) return false;
        if (PlayerItemPickupPresenter.BlocksActions) return false;
        if (HeliExtraction.BlocksInput || IntroCutscene.BlocksInput) return false;
        if (LoadingScreen.BlocksPlayerInput) return false;
        if (CutsceneStudio.IsActive) return false;
        return true;
    }

    // ---------------- 목표 고르기 ----------------

    // 방 안의 적을 가까운 순(플레이어 기준, 거리가 같으면 체력 많은 순)으로 모읍니다(87번 아군 제외, 보스 포함 - HomingMissile과 같은 기준).
    // [3레벨 보스 그로기 비콘 - 형빈 확정] 노릴 수 있는 총알 비콘도 같은 거리 순으로 함께 후보가 됩니다.
    void CollectCandidates(Vector3 from, List<Component> result)
    {
        HomingMissile.CollectRoomTargets(from, result, null);
    }

    public static bool IsValidTarget(Component target) => HomingMissile.IsValidTargetFor(target, null);

    void TryStartVolley()
    {
        CollectCandidates(transform.position, candidates);
        if (candidates.Count == 0) return; // 적이 없으면 준비된 채로 기다림

        // 서로 다른 적을 최대 발 수만큼 고르고, 발마다 번갈아 배정합니다(골고루 분배 - 형빈 확정).
        // 미사일 수 = 기본 발 수 x 연사(총알과 같은 취급 - 연사 +1이면 발 수만큼 늘어남, 형빈 확정).
        int multishot = stats != null ? Mathf.Max(1, stats.Multishot) : 1;
        int shots = Mathf.Max(1, settings.missilesPerVolley) * multishot;
        int distinct = Mathf.Min(shots, candidates.Count);
        volleyTargets.Clear();
        for (int i = 0; i < shots; i++) volleyTargets.Add(candidates[i % distinct]);

        volleyShotsLeft = shots;
        volleyShotIndex = 0;
        shotTimer = 0f;
        cooldownRemaining = settings.volleyInterval; // 발사를 시작한 순간부터 셈
    }

    void UpdateVolley(float dt)
    {
        shotTimer -= dt;
        while (volleyShotsLeft > 0 && shotTimer <= 0f)
        {
            if (!CanFireNow()) { shotTimer = 0f; return; } // 연출 중에는 잠깐 멈췄다가 이어서 쏨

            FireOne();
            volleyShotsLeft--;
            volleyShotIndex++;
            shotTimer += Mathf.Max(settings.shotInterval, 0f);
            if (settings.shotInterval <= 0f && volleyShotsLeft > 0) continue;
        }
    }

    void FireOne()
    {
        Component target = volleyShotIndex < volleyTargets.Count ? volleyTargets[volleyShotIndex] : null;
        if (!IsValidTarget(target))
        {
            // 배정된 적이 이미 죽었으면 지금 가장 가까운 적에게 쏩니다.
            CollectCandidates(transform.position, candidates);
            target = candidates.Count > 0 ? candidates[0] : null;
            if (target == null) { volleyShotsLeft = 1; return; } // 남은 적이 없으면 이번 발사 끝
        }

        bool left = volleyShotIndex % 2 == 0; // 왼쪽 → 오른쪽 → 왼쪽 → 오른쪽
        Vector3 origin = FirePosition(left);
        Vector3 forward = drone != null && drone.Exists ? drone.Root.transform.forward : transform.forward;

        float damage = (stats != null ? stats.Damage : 1f) * settings.damageMultiplier;
        HomingMissile.Launch(shooting, origin, forward, target, damage, settings);

        if (drone != null) drone.AddRecoil(settings.recoilPitch, settings.recoilBack, settings.recoilRecoverSpeed);
        GameAudio.Play(SfxId.Missile63Launch); // [사운드] 63번 미사일 발사
    }

    // 좌우 포드 위치. 프리팹에서 L/R을 반대로 붙였어도 드론 기준 실제 왼쪽/오른쪽으로 판별합니다.
    Vector3 FirePosition(bool left)
    {
        Transform root = drone != null && drone.Exists ? drone.Root.transform : transform;
        if (firePointLeft != null && firePointRight != null)
        {
            bool swapped = Vector3.Dot(firePointLeft.position - firePointRight.position, root.right) > 0f;
            Transform l = swapped ? firePointRight : firePointLeft;
            Transform r = swapped ? firePointLeft : firePointRight;
            return (left ? l : r).position;
        }
        Vector3 center = drone != null && drone.PosePivot != null ? drone.PosePivot.position : root.position;
        return center + root.right * (left ? -settings.fallbackSideOffset : settings.fallbackSideOffset);
    }
}
