using UnityEngine;

// [레벨 조명 설정] 레벨마다 다른 조명 분위기(2레벨 저녁 노을 / 3레벨 차가운 밤)를 정하는 설정 에셋입니다.
// 레벨 설정(RoomGenerationConfig)의 Lighting Profile 칸에 넣으면 그 레벨을 만들 때 LevelLighting이 적용합니다.
// 칸이 비어 있으면(1레벨, n.5 선택방 레벨) 원래 조명 그대로입니다.
//
// [왜 해 색만으로는 안 되는지]
// 툰 셰이더들(Toon_Environment / Toon_Character / Toon_Character_SD / Toon_Face_SD)은 해(Directional Light)의 "색"만 반영하고 세기와
// 환경광은 무시하며, 그늘 쪽은 재질마다 고정된 "그림자 색"을 씁니다. 그래서 해 색만 바꾸면 빛 받는 면만 물들고 그늘은 낮처럼 밝게
// 남습니다. 이 에셋의 그림자 색조 / 밝기 값이 셰이더 전역 값으로 들어가 그늘 색과 전체 밝기까지 함께 바꿉니다(재질은 건드리지 않음).
//
// [만드는 법]
// Project 창 우클릭 > Create > MobArchive > Level Lighting Profile. 인스펙터 오른쪽 위 ⋮ 메뉴의 "기본값: 저녁 노을" / "기본값: 밤"으로
// 시작 값을 채운 뒤 플레이하면서 조절하세요. 지금 레벨에 적용 중인 에셋이면 Play 중에 값을 바꾸는 즉시 화면에 반영됩니다
// (Play 중에 바꾼 값도 에셋에 그대로 남습니다 - ScriptableObject라서 Play를 끝내도 되돌아가지 않음).
[CreateAssetMenu(menuName = "MobArchive/Level Lighting Profile", fileName = "LevelLightingProfile")]
public class LevelLightingProfile : ScriptableObject
{
    [Header("해(Directional Light) 색 - 빛 받는 면의 색")]
    [Tooltip("켜면 아래 색온도(K)로 해 색을 정합니다. 낮을수록 주황(노을 약 3000), 높을수록 파랑(밤 약 10000). 6500이 거의 흰색입니다.")]
    public bool useColorTemperature = true;

    [Tooltip("해 색온도(K). 노을 약 3000~3500, 밤 약 9000~12000.")]
    [Range(1500f, 20000f)] public float colorTemperature = 6500f;

    [Tooltip("해 색에 곱하는 색입니다(색온도를 끄면 이 색이 그대로 해 색). 흰색이면 영향 없음.")]
    public Color sunFilterColor = Color.white;

    [Header("배경(벽/바닥/소품) - Toon_Environment")]
    [Tooltip("배경 그늘 색에 곱하는 색입니다. 흰색이면 원래 그림자 색 그대로. 노을은 붉은 보라 쪽, 밤은 짙은 남색 쪽으로 낮추면 그늘이 물듭니다.")]
    public Color environmentShadowTint = Color.white;

    [Tooltip("배경 전체 밝기 배율입니다(1 = 원래 밝기). 밤에도 너무 낮추면 길이 안 보이니 0.6 아래로는 권장하지 않습니다.")]
    [Range(0.3f, 1f)] public float environmentBrightness = 1f;

    [Header("캐릭터(플레이어/적/보스/HUD 초상화) - Toon_Character / Toon_Character_SD / Toon_Face_SD")]
    [Tooltip("캐릭터 그늘 색에 곱하는 색입니다. 흰색이면 원래 그대로. 시야를 위해 배경보다 약하게 물들이는 것을 권장합니다.")]
    public Color characterShadowTint = Color.white;

    [Tooltip("캐릭터 전체 밝기 배율입니다(1 = 원래 밝기). 캐릭터가 배경보다 밝아야 잘 보이므로 배경 밝기보다 높게 두는 것을 권장합니다.")]
    [Range(0.3f, 1f)] public float characterBrightness = 1f;

    [Header("밤")]
    [Tooltip("켜면 밤 레벨입니다. 밤 전용 조명 프리팹(창문/자판기/가로등 빛)이 이 레벨에서만 켜집니다.")]
    public bool isNight = false;

    // 해 색(정규화 전)을 돌려줍니다. 셰이더는 색의 비율만 쓰므로 밝기는 상관없습니다.
    public Color ResolveSunColor()
    {
        Color c = useColorTemperature ? Mathf.CorrelatedColorTemperatureToRGB(colorTemperature) : Color.white;
        c *= sunFilterColor;
        c.a = 1f;
        return c;
    }

    // [Play 중 바로 반영] 인스펙터에서 값을 바꿀 때마다 불립니다. 지금 레벨에 적용 중인 에셋이면 바로 다시 적용합니다.
    private void OnValidate()
    {
        if (Application.isPlaying) LevelLighting.RefreshIfCurrent(this);
    }

    // [연한 노을 - 형빈 확인: 3200K는 "불타는 재앙"처럼 너무 빨갰음] 색온도를 높여(덜 주황) 은은한 노을빛만 남깁니다.
    [ContextMenu("기본값: 저녁 노을")]
    private void PresetEvening()
    {
        useColorTemperature = true;
        colorTemperature = 5000f;
        sunFilterColor = new Color(1f, 0.96f, 0.95f, 1f);
        environmentShadowTint = new Color(0.97f, 0.9f, 0.93f, 1f);
        environmentBrightness = 0.97f;
        characterShadowTint = new Color(0.99f, 0.95f, 0.96f, 1f);
        characterBrightness = 1f;
        isNight = false;
        MarkDirty();
        if (Application.isPlaying) LevelLighting.RefreshIfCurrent(this);
    }

    [ContextMenu("기본값: 밤")]
    private void PresetNight()
    {
        useColorTemperature = true;
        colorTemperature = 11000f;
        sunFilterColor = new Color(0.8f, 0.88f, 1f, 1f);
        environmentShadowTint = new Color(0.5f, 0.56f, 0.78f, 1f);
        environmentBrightness = 0.72f;
        characterShadowTint = new Color(0.72f, 0.76f, 0.9f, 1f);
        characterBrightness = 0.88f;
        isNight = true;
        MarkDirty();
        if (Application.isPlaying) LevelLighting.RefreshIfCurrent(this);
    }

    // 메뉴로 바꾼 값이 에셋에 저장되도록 표시합니다(에디터 전용).
    private void MarkDirty()
    {
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }
}
