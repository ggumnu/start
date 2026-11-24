using System.Collections.Generic;
using UnityEngine;

// [패밀리어 공통 부품 - 59번에서 시작, 60번부터 공용]
// 플레이어를 따라다니는 드론(아이작 패밀리어) 하나의 모습/움직임을 맡습니다. 아이템별 능력(59번 해킹, 60번 보호막)은 각 아이템 러너가
// 갖고, 이 클래스는 공통 동작만 합니다: 생성 + 나타남(투명 → 흰빛 → 원래 색), 앞 대상 따라가기, 위아래 떠 있기, 발동 자세(떠오르며 앞이
// 들림), 이펙트 붙이기(서서히 사라짐), 재질 색 바꾸기, 잃을 때 흰빛으로 사라짐(DissolveAndVanish - 형빈 확정).
//
// [줄 서기] 얻은 순서대로 FamiliarChain(플레이어에 붙음)에 등록되고, 따라가기는 FamiliarChain이 앞에서부터 차례로 부릅니다
// (앞 대상이 먼저 움직여야 뒤가 그 자리를 따라가므로). 첫 번째는 플레이어, 나머지는 바로 앞 패밀리어를 따라갑니다.
// [고정 자리 - 63번] 줄을 서지 않는 패밀리어(Spawn의 joinChain = false)는 러너가 정한 자리(예: 플레이어 오른쪽 머리 위)를
// FollowAnchored로 따라갑니다. 줄 서는 패밀리어들은 이 패밀리어와 상관없이 플레이어 뒤에서 줄을 시작합니다.
// [그 밖의 공용 기능 - 63번] 계속 도는 부품(프로펠러 - SetSpinningParts), 발사 반동 흔들림(AddRecoil), 모델 안 부품 찾기(FindModelChild).
//
// [드론 오브젝트] 씬 최상위에 따로 만듭니다 - 플레이어 회전에 휘둘리지 않고, 레벨을 지울 때(LevelRoot 삭제) 함께 지워지지 않게.
// 구조: 루트(위치 + 진행 방향) > 자세 축(떠 있기/기울기/발동 자세) > 모델.
public class FamiliarDrone
{
    // 아이템 에셋의 값을 매 프레임 그대로 옮겨 담는 움직임 설정입니다(Play 중 Inspector 수정도 바로 반영).
    public struct Motion
    {
        public float idleDistance;      // 앞 대상이 멈춰 있을 때 머무는 거리
        public float movingMaxDistance; // 앞 대상이 움직이는 동안 이보다 멀어지지 않음
        public float hoverHeight;
        public float followSmoothTime;
        public float maxFollowSpeed;
        public float snapDistance;
        public float turnSpeed;
        public float leanAngle;
        public float leanFullSpeed;
        public float bobAmplitude;
        public float bobPeriod;
        public float poseLiftHeight;
        public float poseNoseUpAngle;
        public float poseDuration;
    }

    public Motion motion;

    public GameObject Root => root;
    public Transform PosePivot => posePivot;
    public bool Exists => root != null;
    public bool IsAppearing => root != null && root.GetComponent<AppearFromWhite>() != null;
    public bool IsDissolving => root != null && root.GetComponent<DissolveAndVanish>() != null;
    public bool IsPosing => poseTimer >= 0f;

    // 따라가기 상태(FamiliarChain이 읽고 씀)
    public Vector3 MoveDir => moveDir;              // 마지막으로 움직인 수평 방향
    public bool IsMoving { get; private set; }      // 이번 프레임에 스스로 움직였는지(뒤 패밀리어의 거리 묶기 판단용)
    public Vector3 BasePosition => root != null ? root.transform.position : Vector3.zero; // 떠 있기 흔들림을 뺀 위치

    private readonly Transform player;
    private GameObject root;
    private Transform posePivot;
    private GameObject modelInstance;
    private bool inChain;

    // 계속 도는 부품(프로펠러)
    private readonly List<Transform> spinningParts = new List<Transform>();
    private Vector3 spinAxis = Vector3.forward;
    private float spinSpeed;

    // 발사 반동(앞 들림 각도/뒤로 밀림) - 쏠 때마다 더해지고 스스로 0으로 돌아옴
    private float recoilPitch;
    private float recoilBack;
    private float recoilRecoverSpeed = 12f;

    private Vector3 moveDir = Vector3.forward;
    private Vector3 followVelocity;
    private Quaternion yaw = Quaternion.identity;
    private float currentLean;
    private float bobTime;
    private float poseTimer = -1f;

    // 색 바꾸기용 재질 복사본
    private readonly List<Material> tintMaterials = new List<Material>();
    private readonly List<Color> originalColors = new List<Color>();
    private readonly List<bool> usesBaseColor = new List<bool>();
    private float appliedTintWeight = -1f;
    private Color appliedTintColor;

    // 붙여 둔 이펙트들(서서히 사라짐 / 드론 뒤에 고정)
    private readonly List<FamiliarVfx.Fading> fadingVfx = new List<FamiliarVfx.Fading>();
    private readonly List<BehindVfx> behindVfx = new List<BehindVfx>();

    private class BehindVfx
    {
        public GameObject instance;
        public Vector3 worldOffset;
        public float pushBehind;
        public Quaternion worldRotation;
    }

    public FamiliarDrone(Transform playerRoot)
    {
        player = playerRoot;
    }

    // ---------------- 생성 / 해제 ----------------

    // 드론을 만들고 줄 맨 뒤에 섭니다. 나타남 연출(투명 → 흰빛 → 원래 색)이 함께 시작됩니다.
    // joinChain = false면 줄을 서지 않고(63번 고정 자리), initialPosition에서 시작합니다(러너가 FollowAnchored로 움직임).
    public void Spawn(GameObject model, float modelScale, Vector3 modelRotationOffset, float appearDuration, string objectName,
                      bool joinChain = true, Vector3? initialPosition = null)
    {
        if (root != null || player == null) return;

        root = new GameObject(objectName);
        posePivot = new GameObject("PosePivot").transform;
        posePivot.SetParent(root.transform, false);

        if (model != null)
        {
            GameObject m = Object.Instantiate(model, posePivot);
            modelInstance = m;
            m.name = model.name;
            m.transform.localPosition = Vector3.zero;
            m.transform.localRotation = Quaternion.Euler(modelRotationOffset);
            m.transform.localScale = model.transform.localScale * modelScale;

            // 패밀리어는 총알/적/플레이어와 부딪히지 않습니다.
            foreach (var col in m.GetComponentsInChildren<Collider>(true)) Object.Destroy(col);

            CacheTintMaterials(m);
        }
        else
        {
            Debug.LogWarning($"FamiliarDrone: '{objectName}'의 Drone Model 칸이 비어 있어 모델이 보이지 않습니다(능력은 동작함).");
        }

        Vector3 away = FamiliarChain.CameraAwayDirection();
        moveDir = -away;
        yaw = Quaternion.LookRotation(-away, Vector3.up);
        followVelocity = Vector3.zero;
        inChain = joinChain;

        if (joinChain)
        {
            // 줄에 서고, 앞 대상의 카메라 반대쪽 뒤에서 시작합니다(플레이어를 가리지 않게).
            var chain = FamiliarChain.GetOrAdd(player.gameObject);
            chain.Register(this);
            root.transform.SetPositionAndRotation(chain.InitialPositionFor(this, away), yaw);
        }
        else
        {
            root.transform.SetPositionAndRotation(initialPosition ?? player.position, yaw);
        }

        var appear = AppearFromWhite.Begin(root);
        if (appear != null) appear.duration = appearDuration;
    }

    // 드론을 놓아줍니다. dissolve = 흰빛으로 물들며 사라지는 연출과 함께(아이템을 잃을 때), 아니면 바로 지움.
    public void Release(bool dissolve)
    {
        if (player != null && inChain)
        {
            var chain = player.GetComponent<FamiliarChain>();
            if (chain != null) chain.Unregister(this);
        }

        if (root == null) return;
        GameObject go = root;
        root = null;
        posePivot = null;
        modelInstance = null;
        spinningParts.Clear();
        tintMaterials.Clear();
        originalColors.Clear();
        usesBaseColor.Clear();
        fadingVfx.Clear();
        behindVfx.Clear();

        if (dissolve && Application.isPlaying && go.activeInHierarchy)
        {
            // 나타나는 연출이 진행 중이면 두 연출이 같은 색을 동시에 바꾸지 않도록 먼저 끝냅니다.
            var appear = go.GetComponent<AppearFromWhite>();
            if (appear != null) Object.Destroy(appear);
            DissolveAndVanish.Begin(go, DissolveAndVanish.EndAction.Destroy);
        }
        else
        {
            Object.Destroy(go);
        }
    }

    // ---------------- 매 프레임 (러너의 Update에서) ----------------

    public void Tick(float dt)
    {
        if (root == null || dt <= 0f) return;

        if (poseTimer >= 0f)
        {
            poseTimer += dt;
            if (poseTimer >= Mathf.Max(motion.poseDuration, 0.01f)) poseTimer = -1f;
        }

        FamiliarVfx.UpdateFades(fadingVfx, dt);
        UpdateBehindVfx();

        // 프로펠러 등 계속 도는 부품(각 부품의 피벗 기준, 로컬 축)
        if (spinSpeed != 0f)
        {
            Quaternion step = Quaternion.AngleAxis(spinSpeed * dt, spinAxis);
            for (int i = spinningParts.Count - 1; i >= 0; i--)
            {
                if (spinningParts[i] == null) { spinningParts.RemoveAt(i); continue; }
                spinningParts[i].localRotation = spinningParts[i].localRotation * step;
            }
        }

        // 발사 반동이 스스로 제자리로 돌아옴
        float k = Mathf.Exp(-recoilRecoverSpeed * dt);
        recoilPitch *= k;
        recoilBack *= k;
    }

    // ---------------- 계속 도는 부품 / 반동 / 부품 찾기 (63번) ----------------

    // 모델 안에서 이름이 같은 부품들을 찾아 axis(각 부품의 로컬 축) 기준으로 초당 degreesPerSecond도씩 계속 돌립니다.
    public void SetSpinningParts(List<string> partNames, Vector3 axis, float degreesPerSecond)
    {
        spinningParts.Clear();
        spinAxis = axis.sqrMagnitude > 0.0001f ? axis.normalized : Vector3.forward;
        spinSpeed = degreesPerSecond;
        if (partNames == null || modelInstance == null) return;

        foreach (string partName in partNames)
        {
            Transform part = FindModelChild(partName);
            if (part != null) spinningParts.Add(part);
            else if (!string.IsNullOrEmpty(partName))
                Debug.LogWarning($"FamiliarDrone: 모델 안에서 '{partName}' 부품을 찾지 못해 회전시키지 않습니다(이름 확인 - Hierarchy에서 보이는 이름 그대로).");
        }
    }

    public void SetSpinSpeed(Vector3 axis, float degreesPerSecond)
    {
        spinAxis = axis.sqrMagnitude > 0.0001f ? axis.normalized : Vector3.forward;
        spinSpeed = degreesPerSecond;
    }

    // 모델 안(자식 전체)에서 이름이 같은 부품을 찾습니다. 없으면 null.
    public Transform FindModelChild(string childName)
    {
        if (modelInstance == null || string.IsNullOrEmpty(childName)) return null;
        foreach (var t in modelInstance.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == childName) return t;
        }
        return null;
    }

    // 발사 반동: 앞이 pitchDegrees만큼 들리고 backDistance만큼 뒤로 밀렸다가 recoverSpeed 속도로 돌아옵니다(연사 버스트처럼 쏠 때마다 누적).
    public void AddRecoil(float pitchDegrees, float backDistance, float recoverSpeed)
    {
        recoilPitch += pitchDegrees;
        recoilBack += backDistance;
        recoilRecoverSpeed = Mathf.Max(0.1f, recoverSpeed);
    }

    // ---------------- 따라가기 (FamiliarChain이 앞에서부터 차례로 부름) ----------------

    // leaderPos/leaderMoveDir/leaderMoving = 바로 앞 대상(플레이어 또는 앞 패밀리어). snap = 플레이어가 순간이동해 줄 전체를 바로 붙일 때.
    public void Follow(Vector3 leaderPos, Vector3 leaderMoveDir, bool leaderMoving, float playerY, bool snap, float dt)
    {
        if (root == null || posePivot == null || dt <= 0f) return;

        Transform t = root.transform;
        Vector3 before = t.position;
        Vector3 target = TargetBehind(leaderPos, leaderMoveDir, playerY);

        if (snap)
        {
            t.position = target;
            followVelocity = Vector3.zero;
        }
        else
        {
            t.position = Vector3.SmoothDamp(t.position, target, ref followVelocity,
                                            Mathf.Max(motion.followSmoothTime, 0.01f), Mathf.Max(motion.maxFollowSpeed, 0.1f), dt);

            // 앞 대상이 움직이는 동안에는 관성 때문에 벌어지는 간격을 Moving Max Distance로 묶습니다(멈춰 있을 때 거리와 따로 - 형빈 확정).
            if (leaderMoving) t.position = ClampHorizontalDistance(t.position, leaderPos, motion.movingMaxDistance);
        }

        // 내가 움직인 방향 기억(뒤 패밀리어가 이 방향의 뒤쪽에 섬)
        Vector3 moved = t.position - before;
        moved.y = 0f;
        IsMoving = !snap && moved.magnitude / dt > 0.3f;
        if (IsMoving) moveDir = moved.normalized;
        else if (snap) moveDir = leaderMoveDir;

        // 진행 방향 바라보기 + 앞으로 기울기
        Vector3 flyVel = followVelocity;
        flyVel.y = 0f;
        float flySpeed = flyVel.magnitude;
        if (flySpeed > 0.15f)
        {
            yaw = Quaternion.RotateTowards(yaw, Quaternion.LookRotation(flyVel / flySpeed, Vector3.up), motion.turnSpeed * dt);
        }
        t.rotation = yaw;

        ApplyPivot(flySpeed, dt);
    }

    // [고정 자리 - 63번] 러너가 정한 자리(target)를 따라가고, facingDirection(수평)을 바라봅니다. snap = 순간이동 등으로 바로 붙일 때.
    // maxDistance > 0이면 관성 때문에 자리에서 벌어지는 수평 간격을 그 거리까지로 묶습니다(이동 중 너무 뒤처지지 않게 - 형빈 확정).
    public void FollowAnchored(Vector3 target, Vector3 facingDirection, bool snap, float dt, float maxDistance = 0f)
    {
        if (root == null || posePivot == null || dt <= 0f) return;

        Transform t = root.transform;
        if (snap)
        {
            t.position = target;
            followVelocity = Vector3.zero;
        }
        else
        {
            t.position = Vector3.SmoothDamp(t.position, target, ref followVelocity,
                                            Mathf.Max(motion.followSmoothTime, 0.01f), Mathf.Max(motion.maxFollowSpeed, 0.1f), dt);
            if (maxDistance > 0f) t.position = ClampHorizontalDistance(t.position, target, maxDistance);
        }

        facingDirection.y = 0f;
        if (facingDirection.sqrMagnitude > 0.0001f)
        {
            Quaternion want = Quaternion.LookRotation(facingDirection.normalized, Vector3.up);
            yaw = snap ? want : Quaternion.RotateTowards(yaw, want, motion.turnSpeed * dt);
        }
        t.rotation = yaw;

        Vector3 flyVel = followVelocity;
        flyVel.y = 0f;
        ApplyPivot(flyVel.magnitude, dt);
    }

    // 떠 있기 + 앞으로 기울기 + 발동 자세(떠오르며 앞이 들림) + 발사 반동을 자세 축에 넣습니다.
    void ApplyPivot(float flySpeed, float dt)
    {
        float targetLean = motion.leanAngle * Mathf.Clamp01(flySpeed / Mathf.Max(motion.leanFullSpeed, 0.1f));
        currentLean = Mathf.Lerp(currentLean, targetLean, 1f - Mathf.Exp(-8f * dt));

        bobTime += dt;
        float bob = Mathf.Sin(bobTime * Mathf.PI * 2f / Mathf.Max(motion.bobPeriod, 0.1f)) * motion.bobAmplitude;
        float pose = PoseWeight();

        posePivot.localPosition = Vector3.up * (bob + pose * motion.poseLiftHeight) + Vector3.back * recoilBack;
        posePivot.localRotation = Quaternion.Euler(currentLean - pose * motion.poseNoseUpAngle - recoilPitch, 0f, 0f);
    }

    public Vector3 TargetBehind(Vector3 leaderPos, Vector3 leaderMoveDir, float playerY)
    {
        Vector3 p = leaderPos - leaderMoveDir * motion.idleDistance;
        p.y = playerY + motion.hoverHeight; // 높이는 모두 플레이어 기준(헬기 사다리에서도 줄째로 오르내림)
        return p;
    }

    // 앞 대상과의 수평 거리가 maxDistance를 넘으면 그 거리까지 끌어당깁니다(높이는 그대로).
    static Vector3 ClampHorizontalDistance(Vector3 position, Vector3 leaderPos, float maxDistance)
    {
        Vector3 offset = position - leaderPos;
        offset.y = 0f;
        float distance = offset.magnitude;
        if (distance <= maxDistance || distance < 0.0001f) return position;

        Vector3 clamped = leaderPos + offset * (maxDistance / distance);
        clamped.y = position.y;
        return clamped;
    }

    // ---------------- 발동 자세 ----------------

    // 효과가 발동할 때: 살짝 떠올랐다가 앞이 위를 보며 잠깐 머문 뒤 제자리로.
    public void PlayPose()
    {
        poseTimer = 0f;
    }

    float PoseWeight()
    {
        if (poseTimer < 0f) return 0f;
        float t = Mathf.Clamp01(poseTimer / Mathf.Max(motion.poseDuration, 0.01f));
        const float rise = 0.25f;
        const float hold = 0.6f;
        if (t < rise) return Mathf.SmoothStep(0f, 1f, t / rise);
        if (t < hold) return 1f;
        return Mathf.SmoothStep(1f, 0f, (t - hold) / (1f - hold));
    }

    // ---------------- 이펙트 ----------------

    // 드론(자세 축)에 붙어 함께 움직이는 이펙트입니다(59번 쉴드). lifetime 뒤 지우고, 그 전 fadeOut 동안 서서히 투명해집니다.
    public GameObject AttachVfx(GameObject prefab, Vector3 localOffset, float sizeMultiplier, float lifetime, float fadeOut)
    {
        if (prefab == null || posePivot == null) return null;

        GameObject vfx = Object.Instantiate(prefab, posePivot);
        vfx.transform.localPosition = localOffset;
        vfx.transform.localRotation = Quaternion.identity;
        FamiliarVfx.ApplySizeMultiplier(vfx, sizeMultiplier);
        FamiliarVfx.Restart(vfx);
        TrackLifetime(vfx, lifetime, fadeOut);
        return vfx;
    }

    // 드론 "뒤"(카메라 반대쪽)에 겹쳐 띄우는 이펙트들입니다(60번 노란 발광/입자 - 보물상자 아이템 빛과 같은 방식). 드론을 따라다니고,
    // 회전은 월드 기준으로 고정됩니다. color = 덮어쓸 색(레이어의 Recolor가 켜진 경우만).
    public void SpawnBehindVfx(List<FamiliarProcVfxLayer> layers, Color color, float lifetime, float fadeOut)
    {
        if (layers == null || root == null) return;

        foreach (var layer in layers)
        {
            if (layer == null || !layer.enabled || layer.prefab == null) continue;

            Quaternion rot = Quaternion.Euler(layer.worldEulerAngles);
            GameObject obj = Object.Instantiate(layer.prefab, root.transform.position, rot);
            obj.name = layer.prefab.name + " (Familiar)";
            obj.transform.SetParent(root.transform, true); // 루트 크기 1 - 아래 크기가 그대로 월드 배율
            obj.transform.localScale = Vector3.one * layer.scale;

            if (layer.disabledChildNames != null && layer.disabledChildNames.Count > 0)
            {
                foreach (var t in obj.GetComponentsInChildren<Transform>(true))
                {
                    if (t == obj.transform) continue;
                    if (layer.disabledChildNames.Contains(t.name)) t.gameObject.SetActive(false);
                }
            }

            FamiliarVfx.ApplyLayerSettings(obj, layer, color);
            FamiliarVfx.Restart(obj);

            behindVfx.Add(new BehindVfx
            {
                instance = obj,
                worldOffset = layer.worldOffset,
                pushBehind = layer.pushBehindDistance,
                worldRotation = rot,
            });
            TrackLifetime(obj, lifetime, fadeOut);
        }
        UpdateBehindVfx();
    }

    void TrackLifetime(GameObject vfx, float lifetime, float fadeOut)
    {
        if (lifetime <= 0f) return; // 지우지 않는 설정이면 사라짐도 없음
        Object.Destroy(vfx, lifetime);
        var fading = FamiliarVfx.CreateFading(vfx, lifetime, Mathf.Min(Mathf.Max(fadeOut, 0f), lifetime));
        if (fading != null) fadingVfx.Add(fading);
    }

    // 드론 뒤 이펙트를 매 프레임 "드론 위치 + 카메라 반대쪽으로 밀기"에 다시 놓습니다(드론이 돌아도 항상 드론 뒤).
    void UpdateBehindVfx()
    {
        if (behindVfx.Count == 0 || root == null) return;
        Vector3 away = FamiliarChain.CameraAwayDirection3D();
        Vector3 center = posePivot != null ? posePivot.position : root.transform.position;

        for (int i = behindVfx.Count - 1; i >= 0; i--)
        {
            var b = behindVfx[i];
            if (b.instance == null) { behindVfx.RemoveAt(i); continue; }
            b.instance.transform.SetPositionAndRotation(center + b.worldOffset + away * b.pushBehind, b.worldRotation);
        }
    }

    // ---------------- 색 바꾸기 ----------------

    void CacheTintMaterials(GameObject model)
    {
        tintMaterials.Clear();
        originalColors.Clear();
        usesBaseColor.Clear();

        foreach (var rend in model.GetComponentsInChildren<Renderer>(true))
        {
            if (rend is ParticleSystemRenderer || rend is SpriteRenderer) continue;

            // materials = 이 드론만의 복사본 - 같은 재질을 쓰는 다른 모델에는 영향 없음(AppearFromWhite/DissolveAndVanish도 같은 복사본을 씀).
            foreach (var mat in rend.materials)
            {
                if (mat == null) continue;
                bool hasBase = mat.HasProperty("_BaseColor");
                if (!hasBase && !mat.HasProperty("_Color")) continue;

                tintMaterials.Add(mat);
                usesBaseColor.Add(hasBase);
                originalColors.Add(hasBase ? mat.GetColor("_BaseColor") : mat.GetColor("_Color"));
            }
        }
        appliedTintWeight = -1f;
    }

    // 원래 색에 multiplyColor를 weight(0~1)만큼 곱해 칠합니다(38번 매료 색처럼 색만 바뀜, 발광 없음).
    // 나타나는/사라지는 연출이 색을 바꾸는 동안에는 건드리지 않습니다(false를 돌려줌 - 연출이 끝난 뒤 다시 칠해짐).
    public bool SetTint(Color multiplyColor, float weight)
    {
        if (tintMaterials.Count == 0 || root == null) return true;
        if (IsAppearing || IsDissolving) { appliedTintWeight = -1f; return false; }
        if (Mathf.Approximately(weight, appliedTintWeight) && appliedTintColor == multiplyColor) return true;

        appliedTintWeight = weight;
        appliedTintColor = multiplyColor;

        for (int i = 0; i < tintMaterials.Count; i++)
        {
            Material mat = tintMaterials[i];
            if (mat == null) continue;

            Color original = originalColors[i];
            Color target = original * multiplyColor;
            target.a = original.a;
            mat.SetColor(usesBaseColor[i] ? "_BaseColor" : "_Color", Color.Lerp(original, target, weight));
        }
        return true;
    }
}

// 패밀리어 발동 때 드론 뒤에 겹쳐 띄우는 이펙트 한 겹입니다(보물상자 아이템 빛 ItemGlowSpawner의 Extra Layers와 같은 항목).
[System.Serializable]
public class FamiliarProcVfxLayer
{
    [Tooltip("이 겹을 쓸지입니다.")]
    public bool enabled = true;
    [Tooltip("띄울 이펙트 프리팹입니다(예: 발광 = vfx_LootDrop_02, 흩날리는 입자 = vfx_Projectile_01).")]
    public GameObject prefab;
    [Tooltip("월드 기준 크기 배율입니다(보물상자 빛과 같은 방식 - Transform 크기).")]
    [Min(0.001f)] public float scale = 0.5f;
    [Tooltip("드론 중심에서 옮길 거리입니다(월드 기준, m).")]
    public Vector3 worldOffset = Vector3.zero;
    [Tooltip("카메라 반대쪽(드론 뒤)으로 밀어 두는 거리입니다(m). 드론 모델이 이펙트를 가리도록 하는 값입니다.")]
    [Min(0f)] public float pushBehindDistance = 0.15f;
    [Tooltip("이펙트 회전(월드 기준, 도)입니다.")]
    public Vector3 worldEulerAngles = Vector3.zero;
    [Tooltip("켜면 파티클 색(Start Color)과 꼬리 색을 아이템 설정의 발동 색으로 바꿉니다.")]
    public bool recolor = true;
    [Tooltip("켜면 파티클마다 원래 정해진 투명도는 그대로 둡니다(퍼지는 원은 반투명 등).")]
    public bool keepOriginalAlpha = true;
    [Tooltip("입자 수명 배율입니다.")]
    [Min(0.01f)] public float lifetimeMultiplier = 1f;
    [Tooltip("입자 속도 배율입니다.")]
    [Min(0f)] public float speedMultiplier = 1f;
    [Tooltip("입자 생성량(초당) 배율입니다.")]
    [Min(0f)] public float emissionRateMultiplier = 1f;
    [Tooltip("입자 크기 배율입니다.")]
    [Min(0.01f)] public float particleSizeMultiplier = 1f;
    [Tooltip("0이 아니면 입자의 중력 값을 이 값으로 바꿉니다(음수 = 위로 떠오름).")]
    public float gravityOverride = 0f;
    [Tooltip("프리팹 안에서 끌 부품(자식 오브젝트) 이름입니다(예: vfx_Projectile_01의 \"Particle System\", \"Trail\"). 이름이 정확히 같아야 합니다.")]
    public List<string> disabledChildNames = new List<string>();
}

// 패밀리어 이펙트 도우미들입니다(크기/재시작/색/서서히 사라짐).
public static class FamiliarVfx
{
    // 59번과 같은 방식 - localScale이 아니라 파티클 크기/속도 배율을 곱해 Scaling Mode와 상관없이 커지게 합니다.
    public static void ApplySizeMultiplier(GameObject instance, float multiplier)
    {
        if (instance == null || Mathf.Approximately(multiplier, 1f)) return;

        var systems = instance.GetComponentsInChildren<ParticleSystem>(true);
        if (systems.Length == 0)
        {
            instance.transform.localScale *= multiplier;
            return;
        }

        foreach (var ps in systems)
        {
            var main = ps.main;
            main.startSizeMultiplier *= multiplier;
            if (main.startSize3D)
            {
                main.startSizeXMultiplier *= multiplier;
                main.startSizeYMultiplier *= multiplier;
                main.startSizeZMultiplier *= multiplier;
            }
            main.startSpeedMultiplier *= multiplier;
        }
    }

    // Prewarm이 켜진 이펙트(vfx_Shield_01 등)는 생성되는 순간 원래 크기/색으로 파티클을 미리 만들어 둡니다(값을 바꾸기 전).
    // 그 파티클이 거대한 링 등으로 보였으므로, 값을 바꾼 뒤 비우고 다시 재생해 새 값으로 다시 미리 만들게 합니다(꺼진 부품은 제외).
    public static void Restart(GameObject instance)
    {
        var systems = instance.GetComponentsInChildren<ParticleSystem>(true);
        foreach (var ps in systems)
        {
            if (!ps.gameObject.activeInHierarchy) continue;
            ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        foreach (var ps in systems)
        {
            if (!ps.gameObject.activeInHierarchy) continue;
            if (ps.main.playOnAwake) ps.Play(false);
        }
    }

    // 보물상자 아이템 빛(ItemGlowSpawner.ApplyParticleSettings)과 같은 규칙으로 크기/수명/속도/생성량/중력/색을 바꿉니다.
    public static void ApplyLayerSettings(GameObject obj, FamiliarProcVfxLayer layer, Color color)
    {
        foreach (var ps in obj.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = ps.main;

            if (!Mathf.Approximately(layer.particleSizeMultiplier, 1f))
            {
                main.startSizeMultiplier *= layer.particleSizeMultiplier;
                if (main.startSize3D)
                {
                    main.startSizeXMultiplier *= layer.particleSizeMultiplier;
                    main.startSizeYMultiplier *= layer.particleSizeMultiplier;
                    main.startSizeZMultiplier *= layer.particleSizeMultiplier;
                }
            }
            if (!Mathf.Approximately(layer.lifetimeMultiplier, 1f)) main.startLifetimeMultiplier *= layer.lifetimeMultiplier;
            if (!Mathf.Approximately(layer.speedMultiplier, 1f)) main.startSpeedMultiplier *= layer.speedMultiplier;
            if (!Mathf.Approximately(layer.gravityOverride, 0f)) main.gravityModifierMultiplier = layer.gravityOverride;
            if (!Mathf.Approximately(layer.emissionRateMultiplier, 1f))
            {
                var emission = ps.emission;
                emission.rateOverTimeMultiplier *= layer.emissionRateMultiplier;
            }

            if (layer.recolor)
            {
                Color original = main.startColor.mode == ParticleSystemGradientMode.Color
                    ? main.startColor.color
                    : main.startColor.colorMax;
                Color c = color;
                if (layer.keepOriginalAlpha) c.a = original.a;
                main.startColor = new ParticleSystem.MinMaxGradient(c);
            }
        }

        if (!layer.recolor) return;
        foreach (var trail in obj.GetComponentsInChildren<TrailRenderer>(true)) trail.colorGradient = TintGradient(trail.colorGradient, color);
        foreach (var line in obj.GetComponentsInChildren<LineRenderer>(true)) line.colorGradient = TintGradient(line.colorGradient, color);
    }

    static Gradient TintGradient(Gradient source, Color tint)
    {
        var g = new Gradient();
        var alphaKeys = source != null ? source.alphaKeys : new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) };
        var colorKeys = new[] { new GradientColorKey(tint, 0f), new GradientColorKey(tint, 1f) };
        g.SetKeys(colorKeys, alphaKeys);
        return g;
    }

    // ---- 서서히 사라짐 ----
    // 파티클마다 알파를 줄이는 방식이라 이펙트 재질/셰이더와 상관없이 동작합니다(파티클 색은 거의 모든 파티클 셰이더가 곱함).
    // - 이미 떠 있는 파티클: 사라짐을 시작할 때의 알파를 기억해 두고(randomSeed 기준) 그 값 x 남은 비율로 맞춤(반복 곱셈 오차 없음).
    // - 새로 나오는 파티클: Start Color 알파를 남은 비율로 줄여 둠 - 처음 보는 파티클은 직전 프레임 비율로 태어났으므로 역산해 기억.

    public class Fading
    {
        public GameObject instance;
        public ParticleSystem[] systems;
        public ParticleSystem.MinMaxGradient[] originalStartColors;
        public float elapsed;
        public float fadeStart;
        public float fadeDuration;
        public float lastFactor = 1f;
        public bool fading;
        public readonly Dictionary<uint, float> baseAlphaBySeed = new Dictionary<uint, float>();
    }

    private static ParticleSystem.Particle[] particleBuffer;

    public static Fading CreateFading(GameObject vfx, float lifetime, float fadeDuration)
    {
        if (vfx == null || fadeDuration <= 0f) return null;
        var systems = vfx.GetComponentsInChildren<ParticleSystem>(true);
        if (systems.Length == 0) return null;

        var f = new Fading
        {
            instance = vfx,
            systems = systems,
            originalStartColors = new ParticleSystem.MinMaxGradient[systems.Length],
            fadeStart = lifetime - fadeDuration,
            fadeDuration = fadeDuration,
        };
        for (int i = 0; i < systems.Length; i++) f.originalStartColors[i] = systems[i].main.startColor;
        return f;
    }

    public static void UpdateFades(List<Fading> list, float dt)
    {
        for (int e = list.Count - 1; e >= 0; e--)
        {
            Fading entry = list[e];
            if (entry.instance == null) { list.RemoveAt(e); continue; }

            entry.elapsed += dt;
            if (entry.elapsed < entry.fadeStart) continue;

            float factor = Mathf.Clamp01(1f - (entry.elapsed - entry.fadeStart) / Mathf.Max(entry.fadeDuration, 0.0001f));

            for (int i = 0; i < entry.systems.Length; i++)
            {
                ParticleSystem ps = entry.systems[i];
                if (ps == null) continue;

                var main = ps.main;
                main.startColor = ScaleAlpha(entry.originalStartColors[i], factor);

                int max = main.maxParticles;
                if (particleBuffer == null || particleBuffer.Length < max) particleBuffer = new ParticleSystem.Particle[max];
                int count = ps.GetParticles(particleBuffer);
                for (int k = 0; k < count; k++)
                {
                    uint seed = particleBuffer[k].randomSeed;
                    Color32 c = particleBuffer[k].startColor;
                    if (!entry.baseAlphaBySeed.TryGetValue(seed, out float baseAlpha))
                    {
                        float bornFactor = entry.fading ? entry.lastFactor : 1f;
                        baseAlpha = bornFactor > 0.001f ? Mathf.Clamp(c.a / bornFactor, 0f, 255f) : 0f;
                        entry.baseAlphaBySeed[seed] = baseAlpha;
                    }
                    c.a = (byte)Mathf.Clamp(Mathf.RoundToInt(baseAlpha * factor), 0, 255);
                    particleBuffer[k].startColor = c;
                }
                ps.SetParticles(particleBuffer, count);
            }

            entry.fading = true;
            entry.lastFactor = factor;
        }
    }

    static ParticleSystem.MinMaxGradient ScaleAlpha(ParticleSystem.MinMaxGradient source, float factor)
    {
        switch (source.mode)
        {
            case ParticleSystemGradientMode.Color:
                return new ParticleSystem.MinMaxGradient(WithAlpha(source.color, factor));
            case ParticleSystemGradientMode.TwoColors:
                return new ParticleSystem.MinMaxGradient(WithAlpha(source.colorMin, factor), WithAlpha(source.colorMax, factor));
            case ParticleSystemGradientMode.Gradient:
                return new ParticleSystem.MinMaxGradient(ScaleGradient(source.gradient, factor));
            case ParticleSystemGradientMode.TwoGradients:
                return new ParticleSystem.MinMaxGradient(ScaleGradient(source.gradientMin, factor), ScaleGradient(source.gradientMax, factor));
            case ParticleSystemGradientMode.RandomColor:
            {
                var g = new ParticleSystem.MinMaxGradient(ScaleGradient(source.gradient, factor));
                g.mode = ParticleSystemGradientMode.RandomColor;
                return g;
            }
        }
        return source;
    }

    static Color WithAlpha(Color c, float factor)
    {
        c.a *= factor;
        return c;
    }

    static Gradient ScaleGradient(Gradient source, float factor)
    {
        if (source == null) return null;
        var keys = source.alphaKeys;
        for (int i = 0; i < keys.Length; i++) keys[i].alpha *= factor;
        var g = new Gradient { mode = source.mode };
        g.SetKeys(source.colorKeys, keys);
        return g;
    }
}
