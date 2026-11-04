using System;
using System.Collections.Generic;
using UnityEngine;

// 특정 Animator 상태가 재생되는 동안에만 지정한 레이어(기본 UpperBodyTorso)의 Weight를 올려주는 컴포넌트입니다.
//
// [왜 필요한가]
// 상체 레이어(UpperBody) 마스크는 총 조준 때의 "림보 자세"를 막기 위해 몸통(Body)을 빼고 팔 + 머리만 씁니다.
// 그런데 수류탄 투척/해킹/아이템 획득처럼 몸통을 크게 쓰는 동작은 몸통이 빠지면 팔만 허우적거립니다.
// 마스크는 레이어마다 하나라서 상태별로 바꿀 수 없으므로, UpperBody를 그대로 따라가는 Sync 레이어
// (UpperBodyTorso, 마스크 = 몸통만)를 하나 더 두고, 몸통이 필요한 상태에서만 그 레이어의 Weight를 올립니다.
// 총 조준 중에는 Weight가 0이라 지금 자세가 그대로 유지됩니다.
//
// [에디터 설정]
// 1) Animator Controller에 레이어 추가: 이름 UpperBodyTorso, Weight 0, Mask = TorsoAvatarMask,
//    Sync 체크 + Source Layer = UpperBody, Blending = Override. (상태/전이는 UpperBody를 자동으로 따라갑니다)
// 2) 이 컴포넌트를 Animator가 붙은 오브젝트(유니티짱: SD_unitychan_humanoid)에 붙입니다.
//    레이사(Generic) 프리팹에는 붙이지 않습니다 -> 레이어 Weight가 계속 0이라 레이사는 지금과 똑같습니다.
//
// [동작]
// Active States 목록의 상태가 재생되는 정도(전이 중이면 전이 비율만큼)의 최댓값 x Max Weight를 목표로,
// Blend Speed 속도로 부드럽게 레이어 Weight를 바꿉니다. Play 중에 값을 바꾸면 바로 반영됩니다.
[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public class TorsoLayerWeightByState : MonoBehaviour
{
    [Serializable]
    public class StateRef
    {
        [Tooltip("상태가 있는 Animator 레이어 이름입니다(예: UpperBody).")]
        public string layerName = "UpperBody";
        [Tooltip("이 상태가 재생되는 동안 대상 레이어의 Weight를 올립니다.")]
        public string stateName = "Grenade_Throw";

        public StateRef() { }
        public StateRef(string layer, string state) { layerName = layer; stateName = state; }
    }

    [Header("참조 (비워두면 자동으로 찾습니다)")]
    [Tooltip("비워두면 같은 오브젝트의 Animator를 씁니다.")]
    public Animator animator;

    [Header("대상 레이어")]
    [Tooltip("Weight를 조절할 레이어 이름입니다(몸통만 덮어쓰는 Sync 레이어).")]
    public string targetLayerName = "UpperBodyTorso";

    [Header("켜는 상태")]
    [Tooltip("이 상태들이 재생되는 동안 대상 레이어의 Weight를 올립니다.")]
    public List<StateRef> activeStates = new List<StateRef>
    {
        new StateRef("UpperBody", "Grenade_Ready"),
        new StateRef("UpperBody", "Grenade_Throw"),
        new StateRef("UpperBody", "Hack_Ready"),
        new StateRef("UpperBody", "Hack"),
        new StateRef("UpperBody", "ItemPickup"),
    };

    [Header("강도")]
    [Tooltip("켜는 상태일 때의 최대 Weight입니다. 걸으면서 던질 때 하체와 상체가 엇갈려 보이면 0.7 정도로 낮춰 보세요.")]
    [Range(0f, 1f)] public float maxWeight = 1f;
    [Tooltip("Weight가 바뀌는 속도(초당)입니다. 8이면 약 0.125초 만에 완전히 바뀝니다.")]
    [Min(0.1f)] public float blendSpeed = 8f;

    private float currentWeight;
    private bool warnedMissingLayer;

    void Awake()
    {
        if (animator == null) animator = GetComponent<Animator>();
    }

    void OnEnable()
    {
        currentWeight = 0f;
    }

    // Animator는 Update 다음에 자세를 계산하므로, 여기서 정한 Weight가 이번 프레임에 바로 반영됩니다.
    void Update()
    {
        if (animator == null || !animator.isActiveAndEnabled || animator.runtimeAnimatorController == null) return;

        int layer = string.IsNullOrEmpty(targetLayerName) ? -1 : animator.GetLayerIndex(targetLayerName);
        if (layer < 0)
        {
            if (!warnedMissingLayer)
            {
                Debug.LogWarning($"TorsoLayerWeightByState: Animator에 '{targetLayerName}' 레이어가 없어서 아무것도 하지 않습니다.", this);
                warnedMissingLayer = true;
            }
            return;
        }

        float target = GetActiveWeight() * maxWeight;
        currentWeight = Mathf.MoveTowards(currentWeight, target, blendSpeed * Time.deltaTime);
        animator.SetLayerWeight(layer, currentWeight);
    }

    // Active States 중 지금 재생 중인 정도(0~1)의 최댓값입니다.
    private float GetActiveWeight()
    {
        if (activeStates == null) return 0f;

        float max = 0f;
        for (int i = 0; i < activeStates.Count; i++)
        {
            var s = activeStates[i];
            if (s == null) continue;
            float w = GetStateWeight(s.layerName, s.stateName);
            if (w > max) max = w;
        }
        return max;
    }

    // 그 상태가 지금 얼마나 재생되고 있는지(0~1)입니다. 전이 중이면 전이 진행도만큼 섞습니다.
    private float GetStateWeight(string layerName, string stateName)
    {
        if (string.IsNullOrEmpty(layerName) || string.IsNullOrEmpty(stateName)) return 0f;

        int layer = animator.GetLayerIndex(layerName);
        if (layer < 0) return 0f;

        int hash = Animator.StringToHash(layerName + "." + stateName);
        AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(layer);
        float currentMatch = current.fullPathHash == hash ? 1f : 0f;

        if (!animator.IsInTransition(layer)) return currentMatch;

        AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(layer);
        float nextMatch = next.fullPathHash == hash ? 1f : 0f;
        float t = Mathf.Clamp01(animator.GetAnimatorTransitionInfo(layer).normalizedTime);
        return currentMatch * (1f - t) + nextMatch * t;
    }
}
