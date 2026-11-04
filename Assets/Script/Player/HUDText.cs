using TMPro;
using UnityEngine;

// HUD/UI 글자(TextMeshPro)를 같은 기본값으로 만드는 공용 도우미입니다.
//
// [왜 필요한가]
// 옛 방식 Text(UnityEngine.UI.Text)는 글자를 비트맵으로 붙이기 때문에 화면 배율(CanvasScaler)이나 부모 크기
// 조절에 따라 흐릿하고 테두리가 깨져 보입니다. TextMeshPro(TMP)는 글자 모양을 거리 정보(SDF)로 그려서 크기와
// 상관없이 선명합니다. HUD 글자를 파일 단위로 하나씩 TMP로 옮기는 중이며, 옮긴 파일은 이 도우미로 글자를 만듭니다.
//
// [기본값 - 예전 HUD Text 설정과 같은 동작]
// - 글꼴: HUDFont.GetTMP() (경기천년제목_Bold SDF)
// - 줄바꿈 없음(예전 horizontalOverflow = Overflow), 칸을 넘쳐도 잘리지 않고 표시(예전 verticalOverflow = Overflow)
// - 마우스 클릭을 가로채지 않음(raycastTarget = false - HUD 관례)
// - 정렬은 예전 TextAnchor 값을 그대로 넘기면 TMP 정렬로 바꿔 줍니다(ToTMP).
// 줄바꿈이 필요한 글자(설명문 등)는 AddTo(go, 정렬, true)로 만들면 칸 폭에서 줄을 바꿉니다(예전 horizontalOverflow = Wrap).
//
// [외곽선 - SetOutline]
// 예전 Outline 컴포넌트는 TMP 글자에는 제대로 먹지 않아서, TMP 자체 외곽선(글자 재질의 Outline)을 씁니다.
// 인스펙터 값은 예전처럼 "픽셀 두께"로 두고, SetOutline이 글자 크기와 글꼴 에셋 설정에 맞춰 TMP 값으로 바꿔 줍니다.
//
// TMP 외곽선은 글자 테두리선을 중심으로 안쪽/바깥쪽에 반씩 그려져서, 그대로 쓰면 안쪽 절반이 가는 획을 덮어 글자가
// 회색 덩어리처럼 보입니다(첫 TMP 교체 때 F 안내/보스 이름/데미지 숫자에서 생긴 문제). 그래서 외곽선과 같은 값만큼
// 글자를 바깥으로 부풀려(Face Dilate) 외곽선의 안쪽 경계가 원래 글자 테두리에 오게 합니다 - 글자 본체는 원래 굵기
// 그대로, 테두리는 예전 Outline처럼 바깥에만 붙습니다.
//
// 그릴 수 있는 최대 두께는 글꼴 에셋의 Padding에 묶여 있어서(기본 Padding 9 / Sampling Point Size 90 기준, 글자 크기
// 28이면 약 1.5픽셀), 그보다 큰 값을 넣어도 최대 두께에서 멈춥니다. 더 두꺼운 테두리가 필요하면 글꼴 에셋의 Padding을
// 키운 뒤 다시 생성(Update Atlas Texture)하면 됩니다.
public static class HUDText
{
    // go에 TextMeshProUGUI를 붙이고 기본값을 채워 돌려줍니다. go에는 RectTransform이 이미 있거나, 없으면
    // TMP가 자동으로 붙입니다.
    public static TextMeshProUGUI AddTo(GameObject go, TextAnchor alignment)
    {
        return AddTo(go, alignment, false);
    }

    // wrap = true면 칸 폭을 넘을 때 줄을 바꿉니다(설명문/확인창 문구 등).
    public static TextMeshProUGUI AddTo(GameObject go, TextAnchor alignment, bool wrap)
    {
        var text = go.AddComponent<TextMeshProUGUI>();
        text.font = HUDFont.GetTMP();
        text.alignment = ToTMP(alignment);
        text.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        return text;
    }

    // 외곽선 두께 + 부풀림의 합이 1을 넘으면 TMP가 전체를 줄여버리므로, 외곽선 값은 0.5까지만 씁니다(0.5 + 0.5 = 1).
    private const float MaxOutlineWidth = 0.5f;

    // 글꼴 에셋을 못 읽을 때 쓰는 기본값(TMP Font Asset 기본 설정).
    private const float DefaultPointSize = 90f;
    private const float DefaultPadding = 9f;

    // TMP 글자에 외곽선을 겁니다. pixels는 예전 Outline 컴포넌트와 같은 "픽셀 두께"(0 이하면 외곽선 없음)입니다.
    // 글자 크기(fontSize)를 먼저 정한 뒤에 부르세요. 값이 그대로면 재질을 다시 건드리지 않으므로 매 프레임 불러도 됩니다.
    // (처음 외곽선을 걸 때 그 글자 전용 재질 사본이 하나 만들어집니다 - TMP의 정상 동작.)
    public static void SetOutline(TextMeshProUGUI text, float pixels, Color color)
    {
        if (text == null) return;

        float width = PixelsToOutlineWidth(text, pixels);

        if (!Mathf.Approximately(text.outlineWidth, width))
        {
            // 이 글자 전용 재질에 부풀림(Face Dilate)을 외곽선과 같은 값으로 먼저 넣고, 그다음 외곽선 두께를 넣습니다
            // (outlineWidth를 넣을 때 TMP가 여백/비율을 다시 계산하므로 부풀림 값까지 함께 반영됩니다).
            Material mat = text.fontMaterial;
            if (mat != null && mat.HasProperty(ShaderUtilities.ID_FaceDilate))
                mat.SetFloat(ShaderUtilities.ID_FaceDilate, width);

            text.outlineWidth = width;

            if (mat != null) ShaderUtilities.UpdateShaderRatios(mat);
            text.UpdateMeshPadding();
            text.SetVerticesDirty();
            text.SetMaterialDirty();
        }

        if (width > 0f)
        {
            Color32 c = color;
            Color32 current = text.outlineColor;
            if (current.r != c.r || current.g != c.g || current.b != c.b || current.a != c.a) text.outlineColor = c;
        }
    }

    // 픽셀 두께를 TMP 외곽선 값으로 바꿉니다. 외곽선 값 w + 같은 값의 부풀림이면, 바깥에 붙는 테두리 두께는
    // 대략 w x (Padding + 1) x (글자 크기 / Sampling Point Size) 픽셀입니다(TMP SDF 셰이더 계산 기준).
    private static float PixelsToOutlineWidth(TextMeshProUGUI text, float pixels)
    {
        if (pixels <= 0f) return 0f;

        float pointSize = DefaultPointSize;
        float padding = DefaultPadding;
        TMP_FontAsset font = text.font;
        if (font != null)
        {
            if (font.faceInfo.pointSize > 0) pointSize = font.faceInfo.pointSize;
            if (font.atlasPadding > 0) padding = font.atlasPadding;
        }

        float pixelsPerUnit = (padding + 1f) * Mathf.Max(1f, text.fontSize) / pointSize;
        return Mathf.Clamp(pixels / pixelsPerUnit, 0f, MaxOutlineWidth);
    }

    // 예전 TextAnchor 정렬을 같은 위치의 TMP 정렬로 바꿉니다.
    public static TextAlignmentOptions ToTMP(TextAnchor anchor)
    {
        switch (anchor)
        {
            case TextAnchor.UpperLeft: return TextAlignmentOptions.TopLeft;
            case TextAnchor.UpperCenter: return TextAlignmentOptions.Top;
            case TextAnchor.UpperRight: return TextAlignmentOptions.TopRight;
            case TextAnchor.MiddleLeft: return TextAlignmentOptions.Left;
            case TextAnchor.MiddleCenter: return TextAlignmentOptions.Center;
            case TextAnchor.MiddleRight: return TextAlignmentOptions.Right;
            case TextAnchor.LowerLeft: return TextAlignmentOptions.BottomLeft;
            case TextAnchor.LowerCenter: return TextAlignmentOptions.Bottom;
            case TextAnchor.LowerRight: return TextAlignmentOptions.BottomRight;
            default: return TextAlignmentOptions.Center;
        }
    }
}
