using UnityEngine;

// [레벨별 배경] 레벨 설정(RoomGenerationConfig / ChoiceLevelConfig)의 Background Material을 화면 배경(Skybox)으로 바꾸고, 그 재질의 초상화 칸
// 색으로 HUD 초상화 하늘(PlayerPortraitCamera)도 맞춥니다. 칸이 비어 있으면 게임 시작 때 씬에 있던 원래 배경과 초상화 원래 색으로 되돌립니다.
// GameFlowManager가 레벨을 만들 때(로딩 화면이 덮고 있는 동안) 부릅니다. 실행 중에만 바꾸므로 씬/Lighting 설정은 그대로입니다.
// 설계 배경은 Docs/설계노트.md [레벨별 배경].
public static class LevelBackground
{
    private const string PortraitTopId = "_PortraitTopColor";
    private const string PortraitBottomId = "_PortraitBottomColor";

    private static bool defaultSkyboxCached;
    private static Material defaultSkybox;
    private static bool defaultPortraitCached;
    private static Color defaultPortraitTop;
    private static Color defaultPortraitBottom;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        defaultSkyboxCached = false;
        defaultSkybox = null;
        defaultPortraitCached = false;
    }

    // levelMaterial이 비어 있으면 원래 배경/초상화 색. 오류가 나도 레벨 진행에는 영향 없음.
    public static void Apply(Material levelMaterial)
    {
        try
        {
            if (!defaultSkyboxCached)
            {
                defaultSkybox = RenderSettings.skybox;
                defaultSkyboxCached = true;
            }

            Material sky = levelMaterial != null ? levelMaterial : defaultSkybox;
            if (RenderSettings.skybox != sky) RenderSettings.skybox = sky;

            ApplyPortrait(levelMaterial);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"LevelBackground: 레벨 배경을 바꾸는 중 오류가 발생해 건너뜁니다: {e}");
        }
    }

    private static void ApplyPortrait(Material levelMaterial)
    {
        foreach (var portrait in Object.FindObjectsByType<PlayerPortraitCamera>(FindObjectsSortMode.None))
        {
            if (portrait == null) continue;

            // 처음 본 초상화의 색(플레이어 프리팹에 설정된 값)을 원래 색으로 기억합니다.
            if (!defaultPortraitCached)
            {
                defaultPortraitTop = portrait.skyTopColor;
                defaultPortraitBottom = portrait.skyBottomColor;
                defaultPortraitCached = true;
            }

            if (levelMaterial != null && levelMaterial.HasProperty(PortraitTopId) && levelMaterial.HasProperty(PortraitBottomId))
            {
                portrait.skyTopColor = levelMaterial.GetColor(PortraitTopId);
                portrait.skyBottomColor = levelMaterial.GetColor(PortraitBottomId);
            }
            else
            {
                portrait.skyTopColor = defaultPortraitTop;
                portrait.skyBottomColor = defaultPortraitBottom;
            }
        }
    }
}
