// ShapeMover.cs
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ShapeMover : MonoBehaviour
{
    public RectTransform canvasRect;
    public float snapStep = 10f;
    public float snapCenterDistance = 24f;
    public float snapAxisDistance = 16f;

    public Image xAxisImage; // optional
    public Image yAxisImage; // optional
    public Color axisHighlightColor = new Color(0.3f, 0.6f, 1f, 0.85f);

    public event Action OnRectanglePlacedAtCenter;
    public event Action OnCirclePlacedAtRectCenter;

    private ShapeRegistry registry;
    private GraphicRaycaster raycaster;
    private EventSystem eventSystem;

    private bool draggingRect = false;
    private bool draggingCircle = false;

    void Start()
    {
        registry = GetComponent<ShapeRegistry>() ?? GetComponentInParent<ShapeRegistry>();
        raycaster = canvasRect.GetComponentInParent<GraphicRaycaster>();
        eventSystem = EventSystem.current;
        if (xAxisImage != null) xAxisImage.color = new Color(1, 1, 1, 0);
        if (yAxisImage != null) yAxisImage.color = new Color(1, 1, 1, 0);
    }

    Vector2 Snap(Vector2 p)
    {
        p.x = Mathf.Round(p.x / snapStep) * snapStep;
        p.y = Mathf.Round(p.y / snapStep) * snapStep;
        return p;
    }

    bool IsClicking(RectTransform rt)
    {
        if (eventSystem == null || raycaster == null) return false;
        PointerEventData pd = new PointerEventData(eventSystem) { position = Input.mousePosition };
        var results = new System.Collections.Generic.List<RaycastResult>();
        raycaster.Raycast(pd, results);
        foreach (var r in results)
            if (r.gameObject.GetComponent<RectTransform>() == rt) return true;
        return false;
    }

    // вызывается контроллером в Update() чтобы обрабатывать вход
    public void UpdateDraggingForRectangleIfNeeded()
    {
        if (registry == null || registry.Rectangle == null) return;

        if (Input.GetMouseButtonDown(0) && IsClicking(registry.Rectangle))
        {
            draggingRect = true;
        }

        if (draggingRect && Input.GetMouseButton(0))
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, Input.mousePosition, null, out Vector2 pos);
            pos = Snap(pos);

            bool snappedXCenter = false, snappedYCenter = false, snappedXaxis = false, snappedYaxis = false;

            if (Mathf.Abs(pos.x) < snapCenterDistance) { pos.x = 0; snappedXCenter = true; }
            if (Mathf.Abs(pos.y) < snapCenterDistance) { pos.y = 0; snappedYCenter = true; }

            if (!snappedYCenter && Mathf.Abs(pos.y) < snapAxisDistance) { pos.y = 0; snappedYaxis = true; }
            if (!snappedXCenter && Mathf.Abs(pos.x) < snapAxisDistance) { pos.x = 0; snappedXaxis = true; }

            registry.Rectangle.anchoredPosition = pos;

            if (xAxisImage != null) xAxisImage.color = (snappedYCenter || snappedYaxis) ? axisHighlightColor : new Color(1, 1, 1, 0);
            if (yAxisImage != null) yAxisImage.color = (snappedXCenter || snappedXaxis) ? axisHighlightColor : new Color(1, 1, 1, 0);
        }

        if (draggingRect && Input.GetMouseButtonUp(0))
        {
            draggingRect = false;
            if (xAxisImage != null) xAxisImage.color = new Color(1, 1, 1, 0);
            if (yAxisImage != null) yAxisImage.color = new Color(1, 1, 1, 0);

            Vector2 pos = registry.Rectangle.anchoredPosition;
            if (Mathf.Abs(pos.x) < 0.01f && Mathf.Abs(pos.y) < 0.01f)
                OnRectanglePlacedAtCenter?.Invoke();
        }
    }

    public void UpdateDraggingForCircleIfNeeded()
    {
        if (registry == null || registry.Circle == null || registry.Rectangle == null) return;

        if (Input.GetMouseButtonDown(0) && IsClicking(registry.Circle))
            draggingCircle = true;

        if (draggingCircle && Input.GetMouseButton(0))
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, Input.mousePosition, null, out Vector2 pos);
            pos = Snap(pos);

            Vector2 rc = registry.Rectangle.anchoredPosition;
            if (Vector2.Distance(pos, rc) < snapCenterDistance) pos = rc;

            registry.Circle.anchoredPosition = pos;
        }

        if (draggingCircle && Input.GetMouseButtonUp(0))
        {
            draggingCircle = false;
            Vector2 rc = registry.Rectangle.anchoredPosition;
            Vector2 pos = registry.Circle.anchoredPosition;
            if (Vector2.Distance(pos, rc) < 0.01f)
                OnCirclePlacedAtRectCenter?.Invoke();
        }
    }
}
