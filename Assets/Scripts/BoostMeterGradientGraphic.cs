using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Draws the boost meter background, border, and a cool-to-hot segmented gradient fill.
/// </summary>
public sealed class BoostMeterGradientGraphic : MaskableGraphic
{
    private const int GradientSegmentCount = 48;
    private const float BorderThickness = 3f;

    [SerializeField, Range(0f, 1f)] private float fillAmount;
    [SerializeField] private Color backgroundColor = new(0.15f, 0.17f, 0.21f, 1f);
    [SerializeField] private Color borderColor = new(1f, 1f, 1f, 0.24f);
    [SerializeField] private Color coolColor = new(0.21f, 0.78f, 1f, 1f);
    [SerializeField] private Color coolMidColor = new(0.2f, 0.9f, 0.69f, 1f);
    [SerializeField] private Color warmMidColor = new(1f, 0.88f, 0.35f, 1f);
    [SerializeField] private Color hotColor = new(1f, 0.24f, 0.36f, 1f);

    /// <summary>Sets the normalized visible fill without rebuilding when the value is unchanged.</summary>
    public void SetFillAmount(float value)
    {
        float clampedValue = Mathf.Clamp01(value);
        if (Mathf.Approximately(fillAmount, clampedValue))
            return;

        fillAmount = clampedValue;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();
        Rect rect = GetPixelAdjustedRect();
        AddQuad(vertexHelper, rect.xMin, rect.yMin, rect.xMax, rect.yMax, backgroundColor);

        float innerLeft = rect.xMin + BorderThickness;
        float innerRight = rect.xMax - BorderThickness;
        float innerBottom = rect.yMin + BorderThickness;
        float innerTop = rect.yMax - BorderThickness;
        float availableWidth = Mathf.Max(0f, innerRight - innerLeft);
        float fillWidth = availableWidth * fillAmount;

        if (fillWidth > 0f)
        {
            int visibleSegments = Mathf.Max(1, Mathf.CeilToInt(GradientSegmentCount * fillAmount));
            for (int segmentIndex = 0; segmentIndex < visibleSegments; segmentIndex++)
            {
                float startT = segmentIndex / (float)GradientSegmentCount;
                float endT = Mathf.Min(fillAmount, (segmentIndex + 1f) / GradientSegmentCount);
                float startX = innerLeft + availableWidth * startT;
                float endX = innerLeft + availableWidth * endT;
                AddGradientQuad(vertexHelper, startX, innerBottom, endX, innerTop, EvaluateGradient(startT), EvaluateGradient(endT));
            }
        }

        AddQuad(vertexHelper, rect.xMin, rect.yMax - BorderThickness, rect.xMax, rect.yMax, borderColor);
        AddQuad(vertexHelper, rect.xMin, rect.yMin, rect.xMax, rect.yMin + BorderThickness, borderColor);
        AddQuad(vertexHelper, rect.xMin, rect.yMin, rect.xMin + BorderThickness, rect.yMax, borderColor);
        AddQuad(vertexHelper, rect.xMax - BorderThickness, rect.yMin, rect.xMax, rect.yMax, borderColor);
    }

    private Color EvaluateGradient(float normalizedPosition)
    {
        if (normalizedPosition < 0.34f)
            return Color.Lerp(coolColor, coolMidColor, normalizedPosition / 0.34f);
        if (normalizedPosition < 0.68f)
            return Color.Lerp(coolMidColor, warmMidColor, (normalizedPosition - 0.34f) / 0.34f);

        return Color.Lerp(warmMidColor, hotColor, (normalizedPosition - 0.68f) / 0.32f);
    }

    private static void AddQuad(VertexHelper vertexHelper, float left, float bottom, float right, float top, Color color)
    {
        int startIndex = vertexHelper.currentVertCount;
        vertexHelper.AddVert(new Vector3(left, bottom), color, Vector2.zero);
        vertexHelper.AddVert(new Vector3(left, top), color, Vector2.up);
        vertexHelper.AddVert(new Vector3(right, top), color, Vector2.one);
        vertexHelper.AddVert(new Vector3(right, bottom), color, Vector2.right);
        vertexHelper.AddTriangle(startIndex, startIndex + 1, startIndex + 2);
        vertexHelper.AddTriangle(startIndex + 2, startIndex + 3, startIndex);
    }

    private static void AddGradientQuad(VertexHelper vertexHelper, float left, float bottom, float right, float top, Color leftColor, Color rightColor)
    {
        int startIndex = vertexHelper.currentVertCount;
        vertexHelper.AddVert(new Vector3(left, bottom), leftColor, Vector2.zero);
        vertexHelper.AddVert(new Vector3(left, top), leftColor, Vector2.up);
        vertexHelper.AddVert(new Vector3(right, top), rightColor, Vector2.one);
        vertexHelper.AddVert(new Vector3(right, bottom), rightColor, Vector2.right);
        vertexHelper.AddTriangle(startIndex, startIndex + 1, startIndex + 2);
        vertexHelper.AddTriangle(startIndex + 2, startIndex + 3, startIndex);
    }
}
