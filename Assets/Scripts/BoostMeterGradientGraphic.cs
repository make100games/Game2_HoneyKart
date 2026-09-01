using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays boost charge with Unity's horizontal filled-image path and a generated cool-to-hot gradient.
/// </summary>
public sealed class BoostMeterGradientGraphic : Image
{
    private static readonly Color CoolColor = new(0.21f, 0.78f, 1f, 1f);
    private static readonly Color CoolMidColor = new(0.2f, 0.9f, 0.69f, 1f);
    private static readonly Color WarmMidColor = new(1f, 0.88f, 0.35f, 1f);
    private static readonly Color HotColor = new(1f, 0.24f, 0.36f, 1f);

    private Texture2D m_GradientTexture;
    private Sprite m_GradientSprite;

    /// <summary>Creates and assigns the runtime gradient used by the filled image, baking rounded corners into its alpha.</summary>
    public void Initialize(Vector2 sizeInPixels, int cornerRadius)
    {
        if (m_GradientSprite != null)
            return;

        int textureWidth = Mathf.RoundToInt(sizeInPixels.x);
        int textureHeight = Mathf.RoundToInt(sizeInPixels.y);
        if (textureWidth <= 0 || textureHeight <= 0)
            return;

        m_GradientTexture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false)
        {
            name = "BoostMeterGradientTexture",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        Color[] pixels = new Color[textureWidth * textureHeight];
        for (int y = 0; y < textureHeight; y++)
        {
            for (int x = 0; x < textureWidth; x++)
            {
                float normalizedPosition = x / (float)(textureWidth - 1);
                Color gradientColor = EvaluateGradient(normalizedPosition);
                float coverage = RoundedRectSpriteFactory.EvaluateCornerCoverage(x + 0.5f, y + 0.5f, textureWidth, textureHeight, cornerRadius);
                gradientColor.a *= coverage;
                pixels[y * textureWidth + x] = gradientColor;
            }
        }

        m_GradientTexture.SetPixels(pixels);
        m_GradientTexture.Apply(false, true);
        m_GradientSprite = Sprite.Create(
            m_GradientTexture,
            new Rect(0f, 0f, textureWidth, textureHeight),
            new Vector2(0.5f, 0.5f),
            1f);
        m_GradientSprite.name = "BoostMeterGradientSprite";

        sprite = m_GradientSprite;
        type = Type.Filled;
        fillMethod = FillMethod.Horizontal;
        fillOrigin = (int)OriginHorizontal.Left;
        fillClockwise = true;
        fillAmount = 0f;
        color = Color.white;
        raycastTarget = false;
    }

    /// <summary>Sets the normalized visible fill.</summary>
    public void SetFillAmount(float value)
    {
        if (m_GradientSprite == null)
            return;

        fillAmount = Mathf.Clamp01(value);
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (m_GradientSprite != null)
            Destroy(m_GradientSprite);
        if (m_GradientTexture != null)
            Destroy(m_GradientTexture);
    }

    private static Color EvaluateGradient(float normalizedPosition)
    {
        if (normalizedPosition < 0.34f)
            return Color.Lerp(CoolColor, CoolMidColor, normalizedPosition / 0.34f);
        if (normalizedPosition < 0.68f)
            return Color.Lerp(CoolMidColor, WarmMidColor, (normalizedPosition - 0.34f) / 0.34f);

        return Color.Lerp(WarmMidColor, HotColor, (normalizedPosition - 0.68f) / 0.32f);
    }
}
