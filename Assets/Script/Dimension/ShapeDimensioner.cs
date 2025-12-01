// ShapeDimensioner.cs
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ShapeDimensioner : MonoBehaviour
{
    public EdgeHighlighter highlighter;

    public RectTransform canvasRect;
    public GameObject dimensionInputPrefab;
    public float edgeDetectThickness = 20f;

    // Targets (можно менять в инспекторе)
    public float rectTargetA = 60f; // например 60
    public float rectTargetB = 30f; // например 30
    public float circleTarget = 10f; // диаметр

    private ShapeRegistry registry;
    private GraphicRaycaster raycaster;
    private EventSystem eventSystem;

    // track current assigned sizes
    private float? rectSideValue1 = null;
    private float? rectSideValue2 = null;
    private float? circleValue = null;

    // events
    public event Action OnRectangleDimensioned;
    public event Action OnCircleDimensioned;
    public event Action OnAllTargetsMatched;

    public enum DimensionSide { None, Left, Right, Top, Bottom }

    void Start()
    {
        registry = GetComponent<ShapeRegistry>() ?? GetComponentInParent<ShapeRegistry>();
        raycaster = canvasRect.GetComponentInParent<GraphicRaycaster>();
        eventSystem = EventSystem.current;
    }

    // public: invoked by controller when in dimension mode and user clicked
    public void TrySelectForDimension()
    {
        if (registry == null || eventSystem == null || raycaster == null) return;

        PointerEventData pd = new PointerEventData(eventSystem) { position = Input.mousePosition };
        var results = new System.Collections.Generic.List<RaycastResult>();
        raycaster.Raycast(pd, results);

        foreach (var r in results)
        {
            var rt = r.gameObject.GetComponent<RectTransform>();

            // circle priority
            if (registry.Circle != null && rt == registry.Circle)
            {
                ShowCircleDimensionWindow();
                return;
            }

            if (registry.Rectangle != null && rt == registry.Rectangle)
            {
                var side = DetectSide(registry.Rectangle, Input.mousePosition);
                if (side != DimensionSide.None)
                {
                    highlighter?.HighlightSelected(side);
                    ShowRectangleDimensionWindow(side);
                }
                return;
            }
        }
    }

    DimensionSide DetectSide(RectTransform rect, Vector2 screenPos)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screenPos, null, out Vector2 local);
        float halfX = rect.sizeDelta.x / 2f;
        float halfY = rect.sizeDelta.y / 2f;

        if (Mathf.Abs(local.x + halfX) < edgeDetectThickness) return DimensionSide.Left;
        if (Mathf.Abs(local.x - halfX) < edgeDetectThickness) return DimensionSide.Right;
        if (Mathf.Abs(local.y - halfY) < edgeDetectThickness) return DimensionSide.Top;
        if (Mathf.Abs(local.y + halfY) < edgeDetectThickness) return DimensionSide.Bottom;
        return DimensionSide.None;
    }

    void ShowRectangleDimensionWindow(DimensionSide side)
    {
        var go = Instantiate(dimensionInputPrefab, canvasRect);
        var wnd = go.GetComponent<UIDimensionWindow>();
        if (wnd == null) { Debug.LogError("dimensionInputPrefab must have UI_DimensionWindow"); Destroy(go); return; }

        wnd.Show(Input.mousePosition, (val) =>
        {
            ApplyRectangleDimension(side, val);
        }, null, "мм");
    }

    void ShowCircleDimensionWindow()
    {
        var go = Instantiate(dimensionInputPrefab, canvasRect);
        var wnd = go.GetComponent<UIDimensionWindow>();
        if (wnd == null) { Debug.LogError("dimensionInputPrefab must have UI_DimensionWindow"); Destroy(go); return; }

        wnd.Show(Input.mousePosition, (val) =>
        {
            ApplyCircleDimension(val);
        }, null, "мм");
    }

    void ApplyRectangleDimension(DimensionSide side, float value)
    {
        if (registry == null || registry.Rectangle == null) return;

        // apply correctly: Top/Bottom -> width (x), Left/Right -> height (y)
        var sz = registry.Rectangle.sizeDelta;
        if (side == DimensionSide.Left || side == DimensionSide.Right)
            sz.y = value;
        else
            sz.x = value;
        registry.Rectangle.sizeDelta = sz;

        // store the given dimension in one of the two slots (order doesn't matter)
        if (!rectSideValue1.HasValue) rectSideValue1 = value;
        else if (!rectSideValue2.HasValue) rectSideValue2 = value;
        else
        {
            // replace nearest? we'll just keep latest two: shift
            rectSideValue1 = rectSideValue2;
            rectSideValue2 = value;
        }

        OnRectangleDimensioned?.Invoke();
        CheckAllTargets();
    }

    void ApplyCircleDimension(float value)
    {
        if (registry == null || registry.Circle == null) return;
        registry.Circle.sizeDelta = new Vector2(value, value);
        circleValue = value;
        OnCircleDimensioned?.Invoke();
        CheckAllTargets();
    }

    void CheckAllTargets()
    {
        // need both rect sides and circle
        if (!rectSideValue1.HasValue || !rectSideValue2.HasValue || !circleValue.HasValue) return;

        // We don't require which side is width/height, so check both possibilities:
        bool rectMatch = (AlmostEqual(rectSideValue1.Value, rectTargetA) && AlmostEqual(rectSideValue2.Value, rectTargetB))
                         || (AlmostEqual(rectSideValue1.Value, rectTargetB) && AlmostEqual(rectSideValue2.Value, rectTargetA));

        bool circMatch = AlmostEqual(circleValue.Value, circleTarget);

        if (rectMatch && circMatch)
        {
            Debug.Log("All dimension targets matched!");
            OnAllTargetsMatched?.Invoke();
        }
    }

    bool AlmostEqual(float a, float b, float eps = 0.01f)
    {
        return Mathf.Abs(a - b) <= eps;
    }

    public void UpdateHoverHighlight()
    {
        if (registry == null || registry.Rectangle == null) return;

        PointerEventData pd = new PointerEventData(eventSystem) { position = Input.mousePosition };
        var results = new System.Collections.Generic.List<RaycastResult>();
        raycaster.Raycast(pd, results);

        foreach (var r in results)
        {
            var rt = r.gameObject.GetComponent<RectTransform>();

            if (rt == registry.Rectangle)
            {
                var side = DetectSide(registry.Rectangle, Input.mousePosition);

                if (side != DimensionSide.None)
                    highlighter?.HighlightHover(side);
                else
                    highlighter?.Clear();

                return;
            }
        }

        highlighter?.Clear();
    }

}
