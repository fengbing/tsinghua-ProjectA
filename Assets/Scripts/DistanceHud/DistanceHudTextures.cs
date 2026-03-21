using UnityEngine;

/// <summary>
/// Runtime-generated sprites: horizontal center falloff × vertical bottom→top fade (no asset files).
/// </summary>
public static class DistanceHudTextures
{
    /// <summary>
    /// Alpha = horizontal center glow × vertical mask (bottom opaque, top transparent).
    /// </summary>
    /// <param name="horizontalCenterPower">
    /// &gt; 1 tightens the bright core toward screen center (light reads as spreading from the middle).
    /// </param>
    public static Sprite CreateCompositeStripSprite(int width = 256, int height = 96, float horizontalCenterPower = 2.2f)
    {
        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        float halfW = (width - 1) * 0.5f;
        float hp = Mathf.Max(1f, horizontalCenterPower);
        for (int y = 0; y < height; y++)
        {
            float ty = y / (float)(height - 1);
            float verticalMask = Smooth(1f - ty);
            for (int x = 0; x < width; x++)
            {
                float nx = Mathf.Abs(x - halfW) / halfW;
                float edge = Smooth(nx);
                float horizontalMask = Mathf.Pow(1f - edge, hp);
                float a = horizontalMask * verticalMask;
                var c = new Color(1f, 1f, 1f, a);
                tex.SetPixel(x, y, c);
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0f), 100f);
    }

    public static Sprite CreateHorizontalFalloff(int width = 256, int height = 64)
    {
        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        float half = (width - 1) * 0.5f;
        for (int x = 0; x < width; x++)
        {
            float nx = Mathf.Abs(x - half) / half;
            float a = 1f - Smooth(nx);
            var c = new Color(1f, 1f, 1f, a);
            for (int y = 0; y < height; y++)
                tex.SetPixel(x, y, c);
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
    }

    public static Sprite CreateVerticalBottomFade(int width = 8, int height = 128)
    {
        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < height; y++)
        {
            float t = y / (float)(height - 1);
            // Bottom of strip (y=0) opaque; toward top of rect fades out (game view visible above).
            float a = Smooth(1f - t);
            var c = new Color(1f, 1f, 1f, a);
            for (int x = 0; x < width; x++)
                tex.SetPixel(x, y, c);
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0f), 100f);
    }

    static float Smooth(float x)
    {
        return x * x * (3f - 2f * x);
    }
}
