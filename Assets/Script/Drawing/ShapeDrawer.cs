// ShapeDrawer.cs
using System;
using UnityEngine;

public class ShapeDrawer : MonoBehaviour
{
    public RectTransform canvasRect;
    public RectTransform rectPrefab;
    public RectTransform circlePrefab;
    public float snapStep = 10f;

    private RectTransform currentShape;
    private Vector2 startLocal;
    private bool isDrawing = false;
    private ShapeKind currentKind;

    public enum ShapeKind { Rectangle, Circle }

    public event Action<RectTransform, ShapeKind> OnShapeCreated;

    Vector2 Snap(Vector2 p)
    {
        p.x = Mathf.Round(p.x / snapStep) * snapStep;
        p.y = Mathf.Round(p.y / snapStep) * snapStep;
        return p;
    }

    public void BeginDrawRectangle()
    {
        if (isDrawing) return;
        isDrawing = true;
        currentKind = ShapeKind.Rectangle;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, Input.mousePosition, null, out startLocal);
        startLocal = Snap(startLocal);
        currentShape = Instantiate(rectPrefab, canvasRect);
        currentShape.anchoredPosition = startLocal;
        currentShape.sizeDelta = Vector2.zero;
    }

    public void BeginDrawCircle()
    {
        if (isDrawing) return;
        isDrawing = true;
        currentKind = ShapeKind.Circle;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, Input.mousePosition, null, out startLocal);
        startLocal = Snap(startLocal);
        currentShape = Instantiate(circlePrefab, canvasRect);
        currentShape.anchoredPosition = startLocal;
        currentShape.sizeDelta = Vector2.zero;
    }

    void Update()
    {
        if (!isDrawing) return;

        if (Input.GetMouseButton(0) && currentShape != null)
        {
            Vector2 cur;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, Input.mousePosition, null, out cur);
            cur = Snap(cur);
            Vector2 diff = cur - startLocal;

            if (currentKind == ShapeKind.Circle)
            {
                float side = Mathf.Max(Mathf.Abs(diff.x), Mathf.Abs(diff.y));
                currentShape.anchoredPosition = startLocal + diff / 2f;
                currentShape.sizeDelta = new Vector2(side, side);
            }
            else
            {
                currentShape.anchoredPosition = startLocal + diff / 2f;
                currentShape.sizeDelta = new Vector2(Mathf.Abs(diff.x), Mathf.Abs(diff.y));
            }
        }

        if (Input.GetMouseButtonUp(0) && currentShape != null)
        {
            if (currentShape.sizeDelta.magnitude < 6f)
            {
                Destroy(currentShape.gameObject);
            }
            else
            {
                // finalise and notify
                var kind = currentKind;
                OnShapeCreated?.Invoke(currentShape, kind);
            }
            currentShape = null;
            isDrawing = false;
        }
    }
}
