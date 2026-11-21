using UnityEngine;

// Sprites painted in code (glows, shadows, placeholder art), so they need no image file.
public static class ProceduralSprite
{
    // Paints a width x height sprite pixel by pixel. 'pixel' is told where the pixel is:
    // x from -1 (left) to 1 (right), y from -1 (bottom) to 1 (top), so 0, 0 is the middle.
    public static Sprite Create(int width, int height, System.Func<float, float, Color32> pixel,
                                FilterMode filter, float pixelsPerUnit)
    {
        var pixels = new Color32[width * height];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                pixels[y * width + x] = pixel((x + 0.5f) / width * 2f - 1f, (y + 0.5f) / height * 2f - 1f);

        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false) { filterMode = filter, wrapMode = TextureWrapMode.Clamp };
        tex.SetPixels32(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), pixelsPerUnit);
    }

    // A pixel of this colour (0-255), with alpha from 0 (clear) to 1 (solid)
    public static Color32 Pixel(byte brightness, float alpha) =>
        new Color32(brightness, brightness, brightness, (byte)(Mathf.Clamp01(alpha) * 255f));
}
