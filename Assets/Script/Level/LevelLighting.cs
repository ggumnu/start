using System;
using UnityEngine;

// [레벨별 조명] 레벨 설정의 Lighting Profile(LevelLightingProfile)을 적용합니다. GameFlowManager가 레벨을 만들기 직전(로딩 화면이 덮고
// 있는 동안) 부릅니다. 비어 있으면(1레벨, n.5 선택방 레벨) 원래 조명으로 되돌립니다.
// - 해 색: 씬의 해(RenderSettings.Sun, 없으면 첫 Directional Light)의 색을 바꿉니다. 세기는 건드리지 않습니다(툰 셰이더는 색만 씀).
//   처음 바꾸기 전의 색을 기억했다가 비어 있는 레벨에서 되돌립니다.
// - 그늘 색 / 밝기: 툰 셰이더 전역 값(_MobLvEnvShadowSub / _MobLvEnvDarken / _MobLvCharShadowSub / _MobLvCharDarken)으로 넣습니다.
//   0이면 변화 없음이라, 이 스크립트가 한 번도 불리지 않는 테스트 씬에서도 원래 모습 그대로입니다.
// - HUD 초상화도 같은 해 색과 캐릭터 값을 받아 함께 물듭니다(형빈 확정).
// - 밤 여부(IsNight)와 바뀜 알림(OnChanged)은 밤 전용 조명 프리팹이 읽습니다.
// 실행 중에만 바꾸며, Play를 끝내면 전역 값을 원래대로 돌려 놓아 에디터 화면에 남지 않게 합니다.
public static class LevelLighting
{
    private static readonly int EnvShadowSubId = Shader.PropertyToID("_MobLvEnvShadowSub");
    private static readonly int EnvDarkenId = Shader.PropertyToID("_MobLvEnvDarken");
    private static readonly int CharShadowSubId = Shader.PropertyToID("_MobLvCharShadowSub");
    private static readonly int CharDarkenId = Shader.PropertyToID("_MobLvCharDarken");

    private static Light cachedSun;
    private static LevelLightingProfile currentProfile; // 지금 적용 중인 설정(Play 중 인스펙터 수정을 바로 반영하기 위함)
    private static Color cachedSunColor = Color.white;
    private static bool quitHooked;

    // 지금 레벨이 밤인지입니다(밤 전용 조명 프리팹용).
    public static bool IsNight { get; private set; }

    // 조명이 바뀔 때마다 알립니다(밤 전용 조명 프리팹이 켜고 끄기를 다시 맞춤).
    public static event Action OnChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        cachedSun = null;
        currentProfile = null;
        cachedSunColor = Color.white;
        IsNight = false;
        OnChanged = null;
        ClearShaderGlobals();
    }

    // profile이 비어 있으면 원래 조명. 오류가 나도 레벨 진행에는 영향 없음.
    public static void Apply(LevelLightingProfile profile)
    {
        currentProfile = profile;
        try
        {
            HookQuit();

            Light sun = FindSun();
            if (sun != null && sun != cachedSun)
            {
                // 이 해를 처음 보면(씬이 새로 열린 경우 포함) 지금 색을 원래 색으로 기억합니다.
                cachedSun = sun;
                cachedSunColor = sun.color;
            }

            if (profile == null)
            {
                if (sun != null) sun.color = cachedSunColor;
                ClearShaderGlobals();
                IsNight = false;
            }
            else
            {
                if (sun != null) sun.color = profile.ResolveSunColor();
                Shader.SetGlobalVector(EnvShadowSubId, SubOf(profile.environmentShadowTint));
                Shader.SetGlobalFloat(EnvDarkenId, Mathf.Clamp01(1f - profile.environmentBrightness));
                Shader.SetGlobalVector(CharShadowSubId, SubOf(profile.characterShadowTint));
                Shader.SetGlobalFloat(CharDarkenId, Mathf.Clamp01(1f - profile.characterBrightness));
                IsNight = profile.isNight;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"LevelLighting: 레벨 조명을 바꾸는 중 오류가 발생해 건너뜁니다: {e}");
        }

        try { OnChanged?.Invoke(); }
        catch (Exception e) { Debug.LogError($"LevelLighting: 조명 바뀜 알림 처리 중 오류가 발생했습니다: {e}"); }
    }

    // [Play 중 바로 반영] profile이 지금 적용 중인 설정이면 다시 적용합니다. LevelLightingProfile의 인스펙터 값을 Play 중에 바꿀 때
    // (OnValidate) 부릅니다. 적용 중인 설정이 아니면 아무것도 하지 않습니다(다음에 그 레벨을 만들 때 반영).
    public static void RefreshIfCurrent(LevelLightingProfile profile)
    {
        if (profile == null || !ReferenceEquals(profile, currentProfile)) return;
        Apply(profile);
    }

    // 곱할 색(흰색 = 그대로)을 셰이더가 받는 "빼는 양"(0 = 그대로)으로 바꿉니다.
    private static Vector4 SubOf(Color tint)
    {
        return new Vector4(Mathf.Clamp01(1f - tint.r), Mathf.Clamp01(1f - tint.g), Mathf.Clamp01(1f - tint.b), 0f);
    }

    private static void ClearShaderGlobals()
    {
        Shader.SetGlobalVector(EnvShadowSubId, Vector4.zero);
        Shader.SetGlobalFloat(EnvDarkenId, 0f);
        Shader.SetGlobalVector(CharShadowSubId, Vector4.zero);
        Shader.SetGlobalFloat(CharDarkenId, 0f);
    }

    private static Light FindSun()
    {
        if (RenderSettings.sun != null) return RenderSettings.sun;
        foreach (var light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (light != null && light.type == LightType.Directional && light.isActiveAndEnabled) return light;
        }
        return null;
    }

    // Play를 끝낼 때(에디터에서는 Play 종료) 전역 값을 원래대로 돌려 에디터 화면/다음 Play에 남지 않게 합니다.
    private static void HookQuit()
    {
        if (quitHooked) return;
        quitHooked = true;
        Application.quitting += () =>
        {
            ClearShaderGlobals();
            IsNight = false;
        };
    }
}
