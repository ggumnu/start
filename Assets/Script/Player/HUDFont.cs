using TMPro;
using UnityEngine;

// HUD/UI 글자에 쓰는 글꼴을 한 곳에서 관리합니다. 글꼴을 바꾸고 싶으면 Assets/Resources/Fonts
// 폴더의 글꼴 파일만 교체하면 됩니다(아래 FontPath의 이름과 파일 이름이 같아야 합니다).
// 글꼴을 못 찾으면 Fonts 폴더 안의 첫 번째 글꼴, 그것도 없으면 Unity 기본 글꼴로 대체합니다.
// 새 UI 텍스트를 만들 때도 text.font = HUDFont.Get(); 한 줄만 쓰면 같은 글꼴이 적용됩니다.
//
// [TextMeshPro 글꼴 - GetTMP()]
// 옛 방식 Text는 글자를 비트맵으로 붙여서 화면 배율에 따라 흐려지므로, HUD 글자를 TextMeshPro(TMP)로
// 단계적으로 옮기고 있습니다. TMP 글자는 TTF가 아니라 TTF로 만든 TMP 글꼴 에셋을 씁니다:
// Assets/Resources/Fonts/경기천년제목_Bold SDF.asset (TTF를 우클릭 > Create > TextMeshPro > Font Asset으로 만든 것).
// 이 에셋을 못 찾으면 TMP 기본 글꼴(영문 전용 - 한글은 네모로 보임)을 쓰고 경고를 한 번 남깁니다.
// 아직 TMP로 옮기지 않은 스크립트는 그대로 Get()을 쓰므로 영향이 없습니다. 새 TMP 글자는 HUDText.AddTo()를 쓰면
// 이 글꼴이 자동으로 적용됩니다.
public static class HUDFont
{
    // "경기천년제목_Bold" (Assets/Resources/Fonts/경기천년제목_Bold.ttf, 확장자 제외)
    private const string FontPath = "Fonts/\uacbd\uae30\ucc9c\ub144\uc81c\ubaa9_Bold";
    private const string FontFolder = "Fonts";

    // "경기천년제목_Bold SDF" (Assets/Resources/Fonts/경기천년제목_Bold SDF.asset, 확장자 제외)
    private const string TMPFontPath = "Fonts/\uacbd\uae30\ucc9c\ub144\uc81c\ubaa9_Bold SDF";

    private static Font cached;
    private static TMP_FontAsset cachedTMP;
    private static bool warnedTMP;

    public static Font Get()
    {
        if (cached != null) return cached;

        cached = Resources.Load<Font>(FontPath);

        if (cached == null)
        {
            var fonts = Resources.LoadAll<Font>(FontFolder);
            if (fonts != null && fonts.Length > 0) cached = fonts[0];
        }

        if (cached == null)
        {
            Debug.LogWarning("[HUDFont] Assets/Resources/Fonts 에서 글꼴을 찾지 못해 기본 글꼴을 사용합니다.");
            cached = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        return cached;
    }

    public static TMP_FontAsset GetTMP()
    {
        if (cachedTMP != null) return cachedTMP;

        cachedTMP = Resources.Load<TMP_FontAsset>(TMPFontPath);

        if (cachedTMP == null)
        {
            var assets = Resources.LoadAll<TMP_FontAsset>(FontFolder);
            if (assets != null && assets.Length > 0) cachedTMP = assets[0];
        }

        if (cachedTMP == null)
        {
            if (!warnedTMP)
            {
                warnedTMP = true;
                Debug.LogWarning("[HUDFont] Assets/Resources/Fonts 에서 TMP 글꼴 에셋(경기천년제목_Bold SDF)을 찾지 못해 " +
                                 "TMP 기본 글꼴을 사용합니다(한글이 네모로 보일 수 있음).");
            }
            return TMP_Settings.defaultFontAsset; // 다음에 다시 찾아보도록 캐시하지 않습니다.
        }

        return cachedTMP;
    }
}
