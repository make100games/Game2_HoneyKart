using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shared static factory that generates anti-aliased rounded-rectangle sprites and rounded
/// alpha coverage values at runtime, for use by UI graphics that need smooth corners.
/// </summary>
public static class RoundedRectSpriteFactory
{
    private const int MinimumCornerRadius = 1;
    private const int CenterPixels = 2;
    private const int SliceGuard = 0;

    // Matches the project's Canvas Scaler "Reference Pixels Per Unit" (100) so that one
    // generated texture pixel maps to exactly one UI unit; using 1f here would make Unity's
    // 9-slice border math inflate 100x, blowing the corners up into a blurry capsule shape.
    private const float ReferencePixelsPerUnit = 100f;

    private static readonly Dictionary<int, Sprite> s_SlicedRoundedRectCache = new();

    /// <summary>
    /// Returns a cached, white, 9-sliced rounded-rect sprite for the given corner radius.
    /// Callers should tint the result via <see cref="UnityEngine.UI.Image.color"/>.
    /// </summary>
    public static Sprite GetSlicedRoundedRect(int cornerRadius)
    {
        int radius = Mathf.Max(MinimumCornerRadius, cornerRadius);

        if (s_SlicedRoundedRectCache.TryGetValue(radius, out Sprite cachedSprite) && cachedSprite != null)
            return cachedSprite;

        int textureSide = 2 * radius + CenterPixels + 2 * SliceGuard;
        Texture2D texture = new(textureSide, textureSide, TextureFormat.RGBA32, false)
        {
            name = $"RoundedRectTexture_{radius}",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave
        };

        Color[] pixels = new Color[textureSide * textureSide];
        for (int y = 0; y < textureSide; y++)
        {
            for (int x = 0; x < textureSide; x++)
            {
                float pixelCenterX = x + 0.5f;
                float pixelCenterY = y + 0.5f;
                float coverage = EvaluateCornerCoverage(pixelCenterX, pixelCenterY, textureSide, textureSide, radius);
                pixels[y * textureSide + x] = new Color(1f, 1f, 1f, coverage);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);

        float border = radius + 1;
        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, textureSide, textureSide),
            new Vector2(0.5f, 0.5f),
            ReferencePixelsPerUnit,
            0,
            SpriteMeshType.FullRect,
            new Vector4(border, border, border, border));
        sprite.name = $"RoundedRectSprite_{radius}";
        sprite.hideFlags = HideFlags.DontSave;

        s_SlicedRoundedRectCache[radius] = sprite;
        return sprite;
    }

    /// <summary>
    /// Returns 0-1 anti-aliased coverage for one pixel of a rounded rect of the given size,
    /// where the pixel position and rect size share the same coordinate space (pixel centers).
    /// </summary>
    public static float EvaluateCornerCoverage(float pixelCenterX, float pixelCenterY, float width, float height, float cornerRadius)
    {
        float radius = Mathf.Clamp(cornerRadius, MinimumCornerRadius, Mathf.Min(width, height) * 0.5f);

        float halfWidth = width * 0.5f;
        float halfHeight = height * 0.5f;
        float offsetX = pixelCenterX - halfWidth;
        float offsetY = pixelCenterY - halfHeight;

        float innerExtentX = halfWidth - radius;
        float innerExtentY = halfHeight - radius;
        float clampedX = Mathf.Clamp(offsetX, -innerExtentX, innerExtentX);
        float clampedY = Mathf.Clamp(offsetY, -innerExtentY, innerExtentY);

        float distanceX = offsetX - clampedX;
        float distanceY = offsetY - clampedY;
        float signedDistance = Mathf.Sqrt(distanceX * distanceX + distanceY * distanceY) - radius;

        return Mathf.Clamp01(0.5f - signedDistance);
    }
}
