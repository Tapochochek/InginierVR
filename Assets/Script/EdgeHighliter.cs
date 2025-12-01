using UnityEngine;
using UnityEngine.UI;

public class EdgeHighlighter : MonoBehaviour
{
    public Image left;
    public Image right;
    public Image top;
    public Image bottom;

    public Color hoverColor = new Color(1f, 0.6f, 0f, 0.8f);
    public Color selectColor = new Color(0f, 1f, 0.3f, 0.9f);
    public Color transparent = new Color(1, 1, 1, 0);

    public void Clear()
    {
        left.color = transparent;
        right.color = transparent;
        top.color = transparent;
        bottom.color = transparent;
    }

    public void HighlightHover(ShapeDimensioner.DimensionSide side)
    {
        Clear();
        if (side == ShapeDimensioner.DimensionSide.Left) left.color = hoverColor;
        if (side == ShapeDimensioner.DimensionSide.Right) right.color = hoverColor;
        if (side == ShapeDimensioner.DimensionSide.Top) top.color = hoverColor;
        if (side == ShapeDimensioner.DimensionSide.Bottom) bottom.color = hoverColor;
    }

    public void HighlightSelected(ShapeDimensioner.DimensionSide side)
    {
        Clear();
        if (side == ShapeDimensioner.DimensionSide.Left) left.color = selectColor;
        if (side == ShapeDimensioner.DimensionSide.Right) right.color = selectColor;
        if (side == ShapeDimensioner.DimensionSide.Top) top.color = selectColor;
        if (side == ShapeDimensioner.DimensionSide.Bottom) bottom.color = selectColor;
    }
}
