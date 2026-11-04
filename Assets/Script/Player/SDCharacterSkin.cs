using UnityEngine;

// SD 캐릭터 하나의 "피부색"을 한 곳에서 관리하는 컴포넌트입니다.
//
// [하는 일]
// 캐릭터 아래의 모든 렌더러 중 "MobArchive/Toon_Character_SD" 셰이더(정확히는 _SkinColor 값을 가진 재질)를
// 쓰는 것을 찾아, 피부색을 한 번에 덮어씁니다. 그래서 머리와 맨손은 항상 같은 피부색이 되고, 나중에
// 팔찌 찬 손처럼 "일부만 피부"인 파츠도 재질에서 "X 영역 = 피부색 사용"만 켜 두면 자동으로 같은 색이 됩니다.
//
// [재질을 직접 바꾸지 않는 이유]
// 재질(.mat) 자체의 피부색을 바꾸면 그 재질을 쓰는 모든 캐릭터(예: 외형 설정 화면의 미리보기 캐릭터)가 한꺼번에
// 바뀝니다. 대신 렌더러마다 MaterialPropertyBlock으로 "이 캐릭터의 값"만 덮어써서 캐릭터끼리 서로 영향을
// 주지 않게 합니다. 재질에 들어 있는 피부색은 이 컴포넌트가 없을 때의 기본값으로만 쓰입니다.
//
// [다른 스크립트와의 관계]
// - PlayerHitFlash는 material.color(_BaseColor)와 renderer.enabled만 바꾸므로 이 컴포넌트와 겹치지 않습니다.
// - SDPartAttacher가 새 파츠를 붙일 때마다 Apply()를 불러 새 파츠에도 피부색을 입힙니다.
//
// [에디터 설정]
// 캐릭터의 최상위 오브젝트(SD_AltBody_v02 등을 담고 있는 오브젝트)에 붙이고 "피부색"만 고르면 됩니다.
// 플레이 중이 아니어도 인스펙터에서 색을 바꾸면 씬 화면에 바로 반영됩니다.
[DisallowMultipleComponent]
public class SDCharacterSkin : MonoBehaviour
{
    [Tooltip("이 캐릭터의 피부색입니다. 머리, 맨손, 그리고 재질에서 '피부색 사용'을 켠 영역에 한 번에 적용됩니다.")]
    public Color skinColor = new Color(0.984f, 0.929f, 0.910f, 1f); // 레이사 얼굴 텍스처의 피부색(#FBEDE8)

    private static readonly int SkinColorId = Shader.PropertyToID("_SkinColor");
    private MaterialPropertyBlock block;

    void OnEnable()
    {
        Apply();
    }

    // 인스펙터에서 값을 바꾸면 바로 반영합니다.
    void OnValidate()
    {
        Apply();
    }

    // 다른 스크립트(외형 설정 화면, 아이템 효과 등)가 피부색을 바꿀 때 씁니다.
    public void SetSkinColor(Color color)
    {
        skinColor = color;
        Apply();
    }

    // 캐릭터 아래의 피부색 대상 렌더러 전부에 현재 피부색을 입힙니다.
    // 파츠를 새로 붙였거나 바꾼 뒤에도 이 함수를 다시 호출하면 됩니다.
    public void Apply()
    {
        if (block == null) block = new MaterialPropertyBlock();

        // 꺼져 있는 파츠(나중에 켜질 수 있는 것)까지 미리 칠해 두기 위해 비활성 오브젝트도 포함합니다.
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null || !UsesSkinColor(r)) continue;

            // 다른 값이 이미 들어 있을 수 있으므로 읽어온 뒤 피부색만 바꿔서 다시 넣습니다.
            r.GetPropertyBlock(block);
            block.SetColor(SkinColorId, skinColor);
            r.SetPropertyBlock(block);
            block.Clear();
        }
    }

    private static bool UsesSkinColor(Renderer r)
    {
        Material[] mats = r.sharedMaterials;
        for (int i = 0; i < mats.Length; i++)
        {
            if (mats[i] != null && mats[i].HasProperty(SkinColorId)) return true;
        }
        return false;
    }
}
