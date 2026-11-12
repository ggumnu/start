using System;
using System.Collections.Generic;
using UnityEngine;

// SD 캐릭터의 헤일로(머리 뼈 아래 스프라이트)를 바꾸는 컴포넌트입니다.
//
// [에디터 설정]
// 헤일로 오브젝트(Sprite Renderer가 붙은 "레이사 헤일로")에 Add Component로 붙입니다. 붙이면 PlayerEffectExclude도 같이 붙어서
// 피격 깜빡임/112번 투명/74번 잔상에 반응하지 않습니다(형빈 확정). "시작 헤일로"를 비워두면 지금 Sprite Renderer의 그림을 그대로 씁니다.
//
// [기본 자리]
// 프리팹에 놓인 위치/회전/크기를 기본값으로 기억하고, 헤일로 정의의 위치/크기 보정은 항상 그 기본값에서 계산합니다
// (헤일로를 여러 번 바꿔도 보정이 쌓이지 않음).
//
// [다른 스크립트에서 쓰는 법]
// 플레이어에서 GetComponentInChildren<SDHaloController>(true)로 찾아 Equip(정의) / Unequip()을 부릅니다. 바뀌면 OnHaloChanged를 알립니다.
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(PlayerEffectExclude))]
public class SDHaloController : MonoBehaviour
{
    [Tooltip("플레이를 시작할 때 쓸 헤일로입니다. 비워두면 지금 Sprite Renderer에 들어 있는 그림을 그대로 씁니다.")]
    public SDHaloDefinition startingHalo;

    [Tooltip("[테스트용] Play 중 ⋮ 메뉴의 \"테스트 - 다음 헤일로로 바꾸기\"가 이 목록을 차례로 돌려 끼웁니다(목록 끝 다음 = 헤일로 없음).")]
    public List<SDHaloDefinition> testHalos = new List<SDHaloDefinition>();

    // 지금 끼워진 헤일로 정의(시작 헤일로를 비워서 프리팹 그림 그대로이거나, 헤일로를 뗐으면 null)
    public SDHaloDefinition Current { get; private set; }

    // 헤일로를 바꾸거나 뗀 직후 알립니다.
    public event Action OnHaloChanged;

    private SpriteRenderer spriteRenderer;
    private Vector3 baseLocalPosition;
    private Quaternion baseLocalRotation;
    private Vector3 baseLocalScale;
    private bool baseCached;
    private int testIndex = -1;

    void Awake()
    {
        CacheBase();
        if (startingHalo != null) Equip(startingHalo);
    }

    // 프리팹에 놓인 자리/크기를 한 번만 기억합니다.
    private void CacheBase()
    {
        if (baseCached) return;
        spriteRenderer = GetComponent<SpriteRenderer>();
        baseLocalPosition = transform.localPosition;
        baseLocalRotation = transform.localRotation;
        baseLocalScale = transform.localScale;
        baseCached = true;
    }

    // 헤일로를 바꿉니다. null이면 헤일로를 뗍니다.
    public void Equip(SDHaloDefinition def)
    {
        CacheBase();
        if (def == null)
        {
            Unequip();
            return;
        }
        if (def.sprite == null)
        {
            Debug.LogWarning("SDHaloController: 헤일로 정의 '" + def.name + "'의 그림(Sprite)이 비어 있어 바꾸지 않았습니다.", def);
            return;
        }

        spriteRenderer.sprite = def.sprite;
        spriteRenderer.color = def.color;
        spriteRenderer.enabled = true;

        transform.localRotation = baseLocalRotation;
        transform.localScale = baseLocalScale * def.scale;
        transform.localPosition = baseLocalPosition + ToParentLocalOffset(def.positionOffset);

        Current = def;
        OnHaloChanged?.Invoke();
    }

    // 헤일로를 뗍니다(그림만 끄고 오브젝트는 남겨서, 다시 끼울 때 같은 자리를 씁니다).
    public void Unequip()
    {
        CacheBase();
        spriteRenderer.enabled = false;
        transform.localPosition = baseLocalPosition;
        transform.localScale = baseLocalScale;
        Current = null;
        OnHaloChanged?.Invoke();
    }

    // 그림 기준 축의 미터 거리를 부모(머리 뼈) 기준 로컬 거리로 바꿉니다. 뼈의 크기 배율이 1이 아니어도 미터 그대로 움직이게 합니다.
    private Vector3 ToParentLocalOffset(Vector3 offsetMeters)
    {
        if (offsetMeters == Vector3.zero) return Vector3.zero;
        Transform parent = transform.parent;
        Vector3 dirInParentAxes = baseLocalRotation * offsetMeters;
        if (parent == null) return dirInParentAxes;
        return parent.InverseTransformVector(parent.rotation * dirInParentAxes);
    }

    [ContextMenu("테스트 - 다음 헤일로로 바꾸기")]
    private void TestNextHalo()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("SDHaloController: Play 중에만 쓸 수 있습니다.", this);
            return;
        }
        if (testHalos.Count == 0)
        {
            Debug.LogWarning("SDHaloController: Test Halos 목록이 비어 있습니다.", this);
            return;
        }

        testIndex++;
        if (testIndex >= testHalos.Count)
        {
            testIndex = -1;
            Unequip();
            Debug.Log("SDHaloController: 헤일로 없음", this);
            return;
        }
        Equip(testHalos[testIndex]);
        Debug.Log("SDHaloController: " + (testHalos[testIndex] != null ? testHalos[testIndex].name : "(빈 칸)"), this);
    }
}
