// ShapeRegistry.cs
using System;
using UnityEngine;

public class ShapeRegistry : MonoBehaviour
{
    public RectTransform Rectangle { get; private set; }
    public RectTransform Circle { get; private set; }

    public event Action<RectTransform> OnRectangleSet;
    public event Action<RectTransform> OnCircleSet;

    public void SetRectangle(RectTransform rect)
    {
        Rectangle = rect;
        OnRectangleSet?.Invoke(rect);
    }

    public void SetCircle(RectTransform circle)
    {
        Circle = circle;
        OnCircleSet?.Invoke(circle);
    }

    public void ClearRectangle()
    {
        Rectangle = null;
        OnRectangleSet?.Invoke(null);
    }

    public void ClearCircle()
    {
        Circle = null;
        OnCircleSet?.Invoke(null);
    }

    public void ClearAll()
    {
        Rectangle = null;
        Circle = null;
        OnRectangleSet?.Invoke(null);
        OnCircleSet?.Invoke(null);
    }
}
