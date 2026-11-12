using System;
using System.Collections.Generic;
using UnityEngine;

// SD 캐릭터가 손에 든 총을 바꾸는 컴포넌트입니다. 총을 바꾸면 총과 연결된 칸들을 같이 다시 연결합니다:
// - PlayerWeaponMode.gunModel(모드별 숨기기, 헬기 연출 숨기기 - HeliExtraction도 이 칸을 씀)
// - HumanoidAimLookAt.muzzleDirection(조준 시 총구 정렬 기준 - 새 총 안의 MuzzleDirection 오브젝트)
// - PlayerShooting.firePoint 위치(총 정의의 총알 위치 보정 - 기본 자리에서 계산하므로 여러 번 바꿔도 쌓이지 않음)
// - PlayerHitFlash 깜빡임 대상(RefreshRenderers), 레이어(캐릭터와 같게 - 초상화)
//
// [에디터 설정]
// SD_Player 루트(PlayerWeaponMode가 붙은 오브젝트)에 Add Component로 붙입니다. "시작 총"을 비워두면 프리팹에 놓인 총을 그대로 씁니다.
// 총을 붙일 뼈는 비워두면 지금 Gun Model의 부모(오른손 뼈)를, 그것도 없으면 Humanoid 오른손 뼈를 씁니다.
//
// [알아둘 점]
// - 바꿀 때 이전 총은 캐릭터 밖으로 빼낸 뒤 지웁니다(SDPartAttacher와 같은 이유 - 같은 프레임에 렌더러를 다시 모으는 스크립트 대비).
// - 새 총이 숨겨져야 하는 상황(수류탄 모드 등)이면 같은 프레임의 PlayerWeaponMode.LateUpdate가 숨기므로 화면에 번쩍이지 않습니다.
[DefaultExecutionOrder(-90)] // PlayerWeaponMode/HumanoidAimLookAt(-50)와 PlayerHitFlash보다 먼저 시작 총을 끼움
[DisallowMultipleComponent]
public class SDGunController : MonoBehaviour
{
    [Tooltip("플레이를 시작할 때 쓸 총입니다. 비워두면 프리팹에 놓인 총(PlayerWeaponMode의 Gun Model)을 그대로 씁니다.")]
    public SDGunDefinition startingGun;

    [Tooltip("총을 붙일 뼈입니다. 비워두면 지금 Gun Model의 부모(오른손 뼈), 그것도 없으면 Humanoid 오른손 뼈를 씁니다.")]
    public Transform mountBone;

    [Tooltip("[테스트용] Play 중 ⋮ 메뉴의 \"테스트 - 다음 총으로 바꾸기\"가 이 목록을 차례로 돌려 끼웁니다.")]
    public List<SDGunDefinition> testGuns = new List<SDGunDefinition>();

    // 지금 든 총 정의(시작 총을 비워서 프리팹의 총 그대로면 null)
    public SDGunDefinition Current { get; private set; }

    // 지금 든 총 오브젝트
    public GameObject CurrentGunObject => weaponMode != null ? weaponMode.gunModel : null;

    // 총을 바꾼 직후 알립니다.
    public event Action OnGunChanged;

    private PlayerWeaponMode weaponMode;
    private HumanoidAimLookAt aimLookAt;
    private PlayerShooting shooting;
    private PlayerHitFlash hitFlash;
    private Vector3 baseFirePointLocal;
    private bool initialized;
    private int testIndex = -1;

    void Awake()
    {
        Init();
        if (startingGun != null) Equip(startingGun);
    }

    private void Init()
    {
        if (initialized) return;
        initialized = true;

        weaponMode = GetComponent<PlayerWeaponMode>();
        aimLookAt = GetComponentInChildren<HumanoidAimLookAt>(true);
        shooting = GetComponent<PlayerShooting>();
        hitFlash = GetComponent<PlayerHitFlash>();
        if (shooting != null && shooting.firePoint != null) baseFirePointLocal = shooting.firePoint.localPosition;

        if (mountBone == null && weaponMode != null && weaponMode.gunModel != null) mountBone = weaponMode.gunModel.transform.parent;
        if (mountBone == null)
        {
            var animator = GetComponentInChildren<Animator>();
            if (animator != null && animator.isHuman) mountBone = animator.GetBoneTransform(HumanBodyBones.RightHand);
        }
    }

    // 총을 바꿉니다. 같은 정의를 다시 넣으면 새로 만들어 끼웁니다.
    public GameObject Equip(SDGunDefinition def)
    {
        Init();
        if (def == null || def.gunPrefab == null)
        {
            Debug.LogWarning("SDGunController: 총 정의가 비어 있거나 총 프리팹(Gun Prefab)이 없어 바꾸지 않았습니다.", def != null ? (UnityEngine.Object)def : this);
            return null;
        }
        if (mountBone == null)
        {
            Debug.LogError("SDGunController: 총을 붙일 뼈를 찾지 못했습니다. Mount Bone 칸에 오른손 뼈를 넣어주세요.", this);
            return null;
        }
        if (weaponMode == null)
        {
            Debug.LogError("SDGunController: 같은 오브젝트에 PlayerWeaponMode가 없습니다.", this);
            return null;
        }

        // 이전 총 정리(캐릭터 밖으로 빼낸 뒤 지움)
        GameObject old = weaponMode.gunModel;
        if (old != null)
        {
            old.SetActive(false);
            old.transform.SetParent(null, false);
            Destroy(old);
        }

        // 새 총: 프리팹 맨 위 오브젝트의 위치/회전/크기를 손 뼈 기준 자리로 그대로 씀
        GameObject gun = Instantiate(def.gunPrefab, mountBone, false);
        gun.name = def.gunPrefab.name;
        SetLayerRecursive(gun.transform, gameObject.layer);
        weaponMode.gunModel = gun;

        // 총구 정렬 기준
        if (aimLookAt != null)
        {
            Transform muzzle = FindChildByName(gun.transform, def.muzzleObjectName);
            if (muzzle == null)
            {
                Debug.LogWarning("SDGunController: 총 프리팹 '" + def.gunPrefab.name + "' 안에서 '" + def.muzzleObjectName +
                                 "' 오브젝트를 찾지 못해 조준 시 총구 정렬을 끕니다.", def);
            }
            aimLookAt.muzzleDirection = muzzle;
        }

        // 총알 위치
        if (shooting != null && shooting.firePoint != null)
        {
            shooting.firePoint.localPosition = baseFirePointLocal + def.firePointOffset;
        }

        if (hitFlash != null) hitFlash.RefreshRenderers();

        Current = def;
        OnGunChanged?.Invoke();
        return gun;
    }

    private static void SetLayerRecursive(Transform t, int layer)
    {
        t.gameObject.layer = layer;
        for (int i = 0; i < t.childCount; i++) SetLayerRecursive(t.GetChild(i), layer);
    }

    private static Transform FindChildByName(Transform root, string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == name) return t;
        }
        return null;
    }

    [ContextMenu("테스트 - 다음 총으로 바꾸기")]
    private void TestNextGun()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("SDGunController: Play 중에만 쓸 수 있습니다.", this);
            return;
        }
        if (testGuns.Count == 0)
        {
            Debug.LogWarning("SDGunController: Test Guns 목록이 비어 있습니다.", this);
            return;
        }

        testIndex = (testIndex + 1) % testGuns.Count;
        SDGunDefinition def = testGuns[testIndex];
        if (Equip(def) != null) Debug.Log("SDGunController: " + def.name, this);
    }
}
