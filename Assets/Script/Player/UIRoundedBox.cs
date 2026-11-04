using UnityEngine;
using UnityEngine.UI;

// [둥근 사각형 공용 도우미]
// 코드로 만드는 UI의 창/버튼을 둥근 모서리로 칠할 때 씁니다. 실행 중에 흰색 둥근 사각형 그림을 한 번만 만들어 두고, 9-Slice로 늘려
// 원하는 크기/색/모서리 둥글기로 그립니다(슬롯 교체 확인창/상점 창과 같은 방식). 게임 오버 창/다음 레벨 이동 확인창이 쓰고, 앞으로
// 새 UI도 이걸 쓰면 됩니다.
public static class UIRoundedBox
{
    private const int TexSize = 96;
    private const int TexRadius = 32;

    private static Sprite sprite;
    private static Texture2D texture;

    // image를 color 색, cornerRadius 둥글기의 둥근 사각형으로 칠합니다. size는 그 Image의 실제 크기(둥글기가 크기의 절반을 넘지 않게).
    public static void Apply(Image image, Color color, float cornerRadius, Vector2 size)
    {
        if (image == null) return;
        image.color = color;

        float radius = Mathf.Min(cornerRadius, Mathf.Min(size.x, size.y) * 0.5f);
        if (radius < 0.5f)
        {
            image.sprite = null;
            image.type = Image.Type.Simple;
            return;
        }

        image.sprite = GetSprite();
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = TexRadius / radius;
    }

    private static Sprite GetSprite()
    {
        if (sprite != null) return sprite;

        float r = TexRadius;
        texture = new Texture2D(TexSize, TexSize, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        var pixels = new Color32[TexSize * TexSize];
        for (int y = 0; y < TexSize; y++)
        {
            for (int x = 0; x < TexSize; x++)
            {
                float cx = Mathf.Clamp(x + 0.5f, r, TexSize - r);
                float cy = Mathf.Clamp(y + 0.5f, r, TexSize - r);
                float dx = x + 0.5f - cx;
                float dy = y + 0.5f - cy;
                float alpha = Mathf.Clamp01(r - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                pixels[y * TexSize + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply();

        sprite = Sprite.Create(texture, new Rect(0f, 0f, TexSize, TexSize), new Vector2(0.5f, 0.5f),
                               100f, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
        return sprite;
    }
}
