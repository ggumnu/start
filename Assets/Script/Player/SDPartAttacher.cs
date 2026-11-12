using System.Collections.Generic;
using UnityEngine;

// SD 캐릭터에 교체 파츠(머리카락, 옷, 장갑, 신발 등)를 붙이고 떼는 컴포넌트입니다.
//
// [원리]
// 파츠 FBX에는 파츠 메시와 함께 바이패드 본 전체가 들어 있습니다. 파츠를 붙일 때 파츠 메시(SkinnedMeshRenderer)가
// 가리키는 본들을 "같은 이름"의 캐릭터 본으로 바꿔 연결하고, 파츠 FBX에 딸려 온 본 사본은 지웁니다.
// 그러면 파츠가 알바디와 똑같이 캐릭터 애니메이션을 따라 움직입니다.
// - 캐릭터 뼈대에 없는 본(머리카락 흔들림용 본 등)은 파츠 파일 안의 부모 본과 같은 이름의 캐릭터 본 아래로
//   옮겨 붙입니다. 파츠를 떼면 같이 지워집니다.
// - 몸통/손/발 파츠를 붙이면 그 자리의 알바디 메시(Body_Torso 등)를 숨기고, 떼면 다시 보이게 합니다.
//   (렌더러를 끄지 않고 오브젝트를 끄기 때문에 PlayerHitFlash의 깜빡임과 겹치지 않습니다.)
// - 붙인 뒤에는 SDCharacterSkin.Apply()를 불러 새 파츠에도 피부색을 입힙니다.
// - 새 파츠 오브젝트는 캐릭터와 같은 레이어로 맞춥니다(초상화 카메라가 바로 그리도록).
// - 붙이거나 뗄 때마다 OnPartsChanged를 알립니다. PlayerHitFlash가 이걸 듣고 깜빡임 대상 렌더러를 다시 모읍니다
//   (112번 투명/74번 잔상은 원래 그 순간의 렌더러를 매번 찾으므로 따로 할 일 없음).
// - 뗀 파츠는 캐릭터 밖으로 빼낸 뒤 지웁니다(Destroy는 프레임 끝에 지워지므로, 같은 프레임에 렌더러를 다시 모으는 스크립트가
//   곧 사라질 파츠를 잡지 않게 하기 위함).
//
// [에디터 설정]
// 캐릭터 최상위 오브젝트(SD_AltBody_v02, SDCharacterSkin이 붙은 곳)에 붙이고, "시작 파츠"에 파츠 정의
// 에셋을 넣으면 플레이를 누를 때 자동으로 붙습니다. 뼈대 루트는 비워두면 "Bip001"을 자동으로 찾습니다.
//
// [알아둘 점]
// - 시작 파츠는 Awake에서 다른 스크립트보다 먼저 붙입니다. 게임 도중 Equip()으로 붙인 파츠도 OnPartsChanged로
//   피격 깜빡임 대상에 들어갑니다.
// - 플레이 중에만 동작합니다(에디터 화면에서 미리 붙여 보는 기능은 없음).
[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public class SDPartAttacher : MonoBehaviour
{
    [Tooltip("캐릭터 뼈대의 최상위 본입니다. 비워두면 자식 중 'Bip001'을 찾습니다.")]
    public Transform skeletonRoot;

    [Tooltip("플레이를 시작할 때 붙일 파츠들입니다.")]
    public List<SDPartDefinition> startingParts = new List<SDPartDefinition>();

    // 슬롯마다 기본으로 숨길 알바디 오브젝트 이름
    private static readonly Dictionary<SDPartSlot, string[]> DefaultHiddenBodyParts = new Dictionary<SDPartSlot, string[]>
    {
        { SDPartSlot.Torso, new[] { "Body_Torso" } },
        { SDPartSlot.Hands, new[] { "Body_Hand_L", "Body_Hand_R" } },
        { SDPartSlot.Feet,  new[] { "Body_Foot_L", "Body_Foot_R" } },
    };

    // 붙어 있는 파츠 하나의 기록
    private class EquippedPart
    {
        public SDPartDefinition definition;
        public GameObject holder;                                   // 파츠 메시를 담는 오브젝트
        public readonly List<GameObject> extraBones = new List<GameObject>(); // 캐릭터 뼈대에 옮겨 붙인 전용 본
        public readonly List<GameObject> hiddenBody = new List<GameObject>(); // 이 파츠 때문에 숨긴 알바디
    }

    private readonly Dictionary<SDPartSlot, EquippedPart> equipped = new Dictionary<SDPartSlot, EquippedPart>();
    private Dictionary<string, Transform> boneMap;

    // 파츠를 붙이거나 뗀 직후 알립니다(PlayerHitFlash가 구독).
    public event System.Action OnPartsChanged;
    private bool suppressChangeEvent; // Equip 안에서 부르는 Unequip은 알림을 한 번만 보내기 위함

    void Awake()
    {
        foreach (SDPartDefinition def in startingParts)
        {
            if (def != null) Equip(def);
        }
    }

    // 지금 그 슬롯에 붙어 있는 파츠 정의(없으면 null)
    public SDPartDefinition GetEquipped(SDPartSlot slot)
    {
        return equipped.TryGetValue(slot, out EquippedPart part) ? part.definition : null;
    }

    // 파츠를 붙입니다. 같은 슬롯에 붙어 있던 파츠는 먼저 뗍니다. 붙인 파츠의 오브젝트를 돌려줍니다.
    public GameObject Equip(SDPartDefinition def)
    {
        if (def == null) return null;
        if (def.modelPrefab == null)
        {
            Debug.LogWarning("SDPartAttacher: 파츠 정의 '" + def.name + "'에 파츠 모델(Model Prefab)이 비어 있습니다. " +
                             "파츠 정의 에셋에 FBX를 넣고 File > Save Project로 저장해주세요.", def);
            return null;
        }
        if (!EnsureBoneMap()) return null;

        suppressChangeEvent = true;
        Unequip(def.slot);
        suppressChangeEvent = false;

        var part = new EquippedPart { definition = def };
        part.holder = new GameObject("Part_" + def.slot + "_" + def.name);
        part.holder.transform.SetParent(transform, false);
        part.holder.layer = gameObject.layer;

        // 파츠 FBX를 잠깐 꺼낸 상태에서 본을 옮겨 연결한 뒤, 남은 껍데기(본 사본 등)는 지웁니다.
        GameObject instance = Instantiate(def.modelPrefab);
        SkinnedMeshRenderer[] renderers = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        if (renderers.Length == 0)
        {
            Debug.LogWarning("SDPartAttacher: '" + def.modelPrefab.name + "'에 스키닝된 메시(SkinnedMeshRenderer)가 없습니다. " +
                             "파츠 FBX의 Rig > Animation Type이 Generic인지, 3ds Max에서 Skin을 넣고 바이패드 본과 함께 내보냈는지 확인해주세요.", def);
        }

        // 캐릭터 뼈대에 없는 본 -> 캐릭터 뼈대에 옮겨 붙인 본
        var moved = new Dictionary<Transform, Transform>();

        foreach (SkinnedMeshRenderer smr in renderers)
        {
            Transform[] bones = smr.bones;
            for (int i = 0; i < bones.Length; i++)
            {
                bones[i] = ResolveBone(bones[i], part, moved);
            }
            smr.bones = bones;
            smr.rootBone = ResolveBone(smr.rootBone, part, moved);

            // 파츠 메시를 캐릭터 쪽으로 옮깁니다. 스키닝된 메시는 본을 따라 그려지므로 위치값은 영향이 없습니다.
            smr.transform.SetParent(part.holder.transform, false);
            smr.gameObject.layer = gameObject.layer;
        }

        Destroy(instance);

        // 이 슬롯이 대신하는 알바디 부위 숨기기
        foreach (string bodyName in GetHiddenBodyNames(def))
        {
            Transform body = FindChildByName(transform, bodyName);
            if (body == null || !body.gameObject.activeSelf) continue;
            body.gameObject.SetActive(false);
            part.hiddenBody.Add(body.gameObject);
        }

        equipped[def.slot] = part;

        // 새 파츠에도 피부색 입히기
        var skin = GetComponent<SDCharacterSkin>();
        if (skin != null) skin.Apply();

        OnPartsChanged?.Invoke();
        return part.holder;
    }

    // 그 슬롯의 파츠를 뗍니다. 숨겼던 알바디 부위는 (다른 파츠가 숨기고 있지 않으면) 다시 보이게 합니다.
    public void Unequip(SDPartSlot slot)
    {
        if (!equipped.TryGetValue(slot, out EquippedPart part)) return;
        equipped.Remove(slot);

        if (part.holder != null)
        {
            // 캐릭터 밖으로 빼낸 뒤 지웁니다(위 [원리] 마지막 줄 참고).
            part.holder.SetActive(false);
            part.holder.transform.SetParent(null, false);
            Destroy(part.holder);
        }
        foreach (GameObject bone in part.extraBones)
        {
            if (bone != null) Destroy(bone);
        }

        foreach (GameObject body in part.hiddenBody)
        {
            if (body == null || IsHiddenByOtherPart(body)) continue;
            body.SetActive(true);
        }

        if (!suppressChangeEvent) OnPartsChanged?.Invoke();
    }

    // [테스트용] Play 중 이 컴포넌트의 ⋮ 메뉴에서 실행: 시작 파츠를 모두 뗐다가 다시 붙입니다(게임 도중 교체 동작 확인용).
    [ContextMenu("테스트 - 시작 파츠 떼었다 다시 붙이기")]
    private void TestReattachStartingParts()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("SDPartAttacher: Play 중에만 쓸 수 있습니다.", this);
            return;
        }
        foreach (SDPartDefinition def in startingParts)
        {
            if (def != null) Unequip(def.slot);
        }
        foreach (SDPartDefinition def in startingParts)
        {
            if (def != null) Equip(def);
        }
        Debug.Log("SDPartAttacher: 시작 파츠를 다시 붙였습니다(" + equipped.Count + "개).", this);
    }

    // ------------------------------------------------------------------ 도우미

    private bool EnsureBoneMap()
    {
        if (boneMap != null) return true;

        if (skeletonRoot == null) skeletonRoot = FindChildByName(transform, "Bip001");
        if (skeletonRoot == null)
        {
            Debug.LogError("SDPartAttacher: 캐릭터 뼈대(Bip001)를 찾지 못했습니다. '뼈대 루트' 칸에 직접 넣어주세요.", this);
            return false;
        }

        boneMap = new Dictionary<string, Transform>();
        foreach (Transform t in skeletonRoot.GetComponentsInChildren<Transform>(true))
        {
            if (!boneMap.ContainsKey(t.name)) boneMap.Add(t.name, t);
        }
        return true;
    }

    // 파츠의 본을 캐릭터 본으로 바꿉니다. 캐릭터에 없는 본이면 부모를 따라 올라가 캐릭터에 있는 본을 찾고,
    // 그 아래로 옮겨 붙입니다(전용 본 사슬은 맨 위 하나만 옮기면 자식들이 함께 따라옵니다).
    private Transform ResolveBone(Transform partBone, EquippedPart part, Dictionary<Transform, Transform> moved)
    {
        if (partBone == null) return null;
        if (boneMap.TryGetValue(partBone.name, out Transform target)) return target;
        if (moved.ContainsKey(partBone)) return moved[partBone];

        // 이미 옮긴 전용 본 사슬 안에 있는 본이면 그대로 씁니다.
        for (Transform p = partBone.parent; p != null; p = p.parent)
        {
            if (moved.ContainsKey(p))
            {
                moved[partBone] = partBone;
                return partBone;
            }
        }

        // 사슬의 맨 위(부모가 캐릭터 뼈대에 있는 본)를 찾아 그 부모 아래로 옮깁니다.
        Transform top = partBone;
        while (top.parent != null && !boneMap.ContainsKey(top.parent.name))
        {
            top = top.parent;
        }

        if (top.parent == null)
        {
            Debug.LogWarning("SDPartAttacher: 본 '" + partBone.name + "'의 부모를 캐릭터 뼈대에서 찾지 못해 연결하지 못했습니다.");
            return partBone;
        }

        Transform newParent = boneMap[top.parent.name];
        top.SetParent(newParent, false); // 부모 본 기준 위치/회전을 그대로 유지
        part.extraBones.Add(top.gameObject);
        moved[top] = top;
        moved[partBone] = partBone;
        return partBone;
    }

    private IEnumerable<string> GetHiddenBodyNames(SDPartDefinition def)
    {
        if (DefaultHiddenBodyParts.TryGetValue(def.slot, out string[] names))
        {
            foreach (string n in names) yield return n;
        }
        if (def.extraHiddenBodyParts != null)
        {
            foreach (string n in def.extraHiddenBodyParts)
            {
                if (!string.IsNullOrEmpty(n)) yield return n;
            }
        }
    }

    private bool IsHiddenByOtherPart(GameObject body)
    {
        foreach (EquippedPart other in equipped.Values)
        {
            if (other.hiddenBody.Contains(body)) return true;
        }
        return false;
    }

    private static Transform FindChildByName(Transform root, string name)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == name) return t;
        }
        return null;
    }
}
