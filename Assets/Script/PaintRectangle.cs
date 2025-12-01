using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;

public class PainRectangle : MonoBehaviour
{
    [Header("Canvas / Prefabs")]
    public RectTransform canvasRect;
    public RectTransform rectPrefab;
    public RectTransform circlePrefab;
    public GameObject dimensionInputPrefab;

    [Header("Draw surface")]
    public RawImage drawSurface; // назначьте RawImage — рисование и перемещение ограничены этой областью

    [Header("Dimension Button")]
    public Button dimensionButton;

    [Header("Snap settings")]
    public float snapStep = 10f;
    public float snapCenterDistance = 24f;

    [Header("Axis snap / highlight")]
    public float snapAxisDistance = 16f; // расстояние до оси для "магнита"
    public Image xAxisImage; // optional: подсветка оси X
    public Image yAxisImage; // optional: подсветка оси Y
    public Color axisHighlightColor = new Color(0.3f, 0.6f, 1f, 0.85f);

    [Header("Edge Highlight")]
    public Color edgeHighlightColor = new Color(0.3f, 0.6f, 1f, 0.85f);

    // -----------------------
    // XR / Input
    // -----------------------
    [Header("XR Settings (world-space)")]
    public bool useXR = true;
    public XRRayInteractor xrInteractor;
    public XRNode xrNode = XRNode.RightHand;
    public Camera mainCamera; // used for non-XR screen ray fallback / optionally assign for XR screen conversions

    private InputDevice xrDevice;
    private bool prevPressed = false;
    private bool pressed = false;
    private bool pointerDown = false;
    private bool pointerUp = false;

    // -----------------------
    // REQUIRED DIMENSIONS (×20)
    // -----------------------
    private const float requiredRectA = 1200f; // 60×20
    private const float requiredRectB = 600f;  // 30×20
    private const float requiredCircleD = 200f; // 10×20

    // -----------------------
    // STAGES
    // -----------------------
    public enum Stage
    {
        DrawRectangle,
        CenterRectangle,
        DrawCircle,
        CenterCircle,
        WaitDimensionButton,
        DimensionRectangle,
        DimensionCircle,
        Done
    }

    public Stage stage = Stage.DrawRectangle;

    // INSTANCES
    private RectTransform rectangle;
    private RectTransform circle;
    private RectTransform currentShape = null;
    private Vector2 drawStartPos;

    // UI
    private GraphicRaycaster raycaster;
    private EventSystem eventSystem;
    private GameObject currentWindow = null;
    private bool dimensionUIOpen = false;
    private Image highlightedEdge = null;

    // DRAG
    private bool draggingRect = false;
    private bool draggingCircle = false;

    // derived
    private RectTransform drawSurfaceRect;

    void Start()
    {
        raycaster = canvasRect.GetComponentInParent<GraphicRaycaster>();
        eventSystem = EventSystem.current;

        if (dimensionButton != null)
        {
            dimensionButton.onClick.AddListener(OnDimensionButtonPressed);
            dimensionButton.gameObject.SetActive(false);
        }

        if (useXR)
        {
            xrDevice = InputDevices.GetDeviceAtXRNode(xrNode);
            if (!xrDevice.isValid)
            {
                // try again later if device not available now
                InputDevices.deviceConnected += OnDeviceConnected;
            }
        }

        if (mainCamera == null)
            mainCamera = Camera.main;

        if (drawSurface != null)
            drawSurfaceRect = drawSurface.rectTransform;
    }

    void OnDestroy()
    {
        InputDevices.deviceConnected -= OnDeviceConnected;
    }

    void OnDeviceConnected(InputDevice device)
    {
        // пробуем переполучить устройство
        xrDevice = InputDevices.GetDeviceAtXRNode(xrNode);
    }

    void Update()
    {
        UpdatePointerState();

        // запрет всего взаимодействия когда окно открыто
        if (dimensionUIOpen) return;

        switch (stage)
        {
            case Stage.DrawRectangle:
                HandleDrawRectangle();
                break;

            case Stage.CenterRectangle:
                HandleCenterRectangle();
                break;

            case Stage.DrawCircle:
                HandleDrawCircle();
                break;

            case Stage.CenterCircle:
                HandleCenterCircle();
                break;

            case Stage.DimensionRectangle:
                HandleRectangleDimensionInput();
                break;

            case Stage.DimensionCircle:
                HandleCircleDimensionInput();
                break;

            case Stage.Done:
                break;
        }
    }

    // -----------------------
    // Pointer / XR helpers
    // -----------------------
    void UpdatePointerState()
    {
        prevPressed = pressed;

        if (useXR && xrDevice.isValid)
        {
            // primary trigger button used as pointer "click"
            xrDevice.TryGetFeatureValue(CommonUsages.triggerButton, out pressed);
        }
        else
        {
            pressed = Input.GetMouseButton(0);
        }

        pointerDown = !prevPressed && pressed;
        pointerUp = prevPressed && !pressed;
    }

    bool TryGetWorldRay(out Ray ray)
    {
        if (useXR && xrInteractor != null && xrInteractor.transform != null)
        {
            ray = new Ray(xrInteractor.transform.position, xrInteractor.transform.forward);
            return true;
        }

        // fallback: camera screen ray
        if (mainCamera != null)
        {
            ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            return true;
        }

        ray = default;
        return false;
    }

    bool RaycastRectTransformPlane(RectTransform rect, out Vector3 hitPoint)
    {
        hitPoint = Vector3.zero;
        if (!TryGetWorldRay(out Ray ray)) return false;

        Plane plane = new Plane(rect.transform.forward, rect.transform.position);
        if (plane.Raycast(ray, out float enter))
        {
            hitPoint = ray.GetPoint(enter);
            return true;
        }

        return false;
    }

    // Получить локальную точку относительно canvasRect (canvas local coordinates),
    // но только если указатель пересек drawSurface.
    bool TryGetLocalPointOnCanvasFromPointer(out Vector2 canvasLocal)
    {
        canvasLocal = Vector2.zero;

        // получаем world-hit на плоскость canvas
        if (!TryGetWorldRay(out Ray ray))
            return false;

        Plane plane = new Plane(canvasRect.transform.forward, canvasRect.transform.position);
        if (!plane.Raycast(ray, out float enter))
            return false;

        Vector3 worldHit = ray.GetPoint(enter);
        // проверяем, находится ли worldHit внутри drawSurface (если задан)
        if (drawSurfaceRect != null)
        {
            Vector3 localOnDraw = drawSurfaceRect.InverseTransformPoint(worldHit);
            Rect drawRect = drawSurfaceRect.rect;
            if (localOnDraw.x < drawRect.xMin || localOnDraw.x > drawRect.xMax || localOnDraw.y < drawRect.yMin || localOnDraw.y > drawRect.yMax)
                return false; // pointer outside drawSurface
        }

        // конвертируем в локальные координаты canvas
        Vector3 canvasLocal3 = canvasRect.InverseTransformPoint(worldHit);
        canvasLocal = new Vector2(canvasLocal3.x, canvasLocal3.y);
        return true;
    }

    // Mouse path: screen-space local to canvas, with drawSurface check
    bool TryGetLocalPointOnCanvasFromMouse(out Vector2 canvasLocal)
    {
        canvasLocal = Vector2.zero;
        if (mainCamera == null) return false;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, Input.mousePosition, mainCamera, out Vector2 local))
            return false;

        // check if local point lies inside drawSurface (in canvas local coordinates)
        if (drawSurfaceRect != null)
        {
            // get drawSurface corners in canvas local coords
            Vector3[] worldCorners = new Vector3[4];
            drawSurfaceRect.GetWorldCorners(worldCorners);
            // transform to canvas local
            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            for (int i = 0; i < 4; i++)
            {
                Vector3 c = canvasRect.InverseTransformPoint(worldCorners[i]);
                if (c.x < minX) minX = c.x;
                if (c.x > maxX) maxX = c.x;
                if (c.y < minY) minY = c.y;
                if (c.y > maxY) maxY = c.y;
            }

            if (local.x < minX || local.x > maxX || local.y < minY || local.y > maxY)
                return false;
        }

        canvasLocal = local;
        return true;
    }

    bool TryGetLocalPointOnCanvas(out Vector2 canvasLocal)
    {
        if (useXR)
            return TryGetLocalPointOnCanvasFromPointer(out canvasLocal);
        else
            return TryGetLocalPointOnCanvasFromMouse(out canvasLocal);
    }

    // Ограничить позицию rect так, чтобы элемент полностью оставался внутри drawSurface.
    Vector2 ClampPositionToDrawSurface(RectTransform rt, Vector2 desiredCanvasLocal)
    {
        if (drawSurfaceRect == null) return desiredCanvasLocal;

        // получаем границы drawSurface в координатах canvas local
        Vector3[] worldCorners = new Vector3[4];
        drawSurfaceRect.GetWorldCorners(worldCorners);

        float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
        for (int i = 0; i < 4; i++)
        {
            Vector3 c = canvasRect.InverseTransformPoint(worldCorners[i]);
            if (c.x < minX) minX = c.x;
            if (c.x > maxX) maxX = c.x;
            if (c.y < minY) minY = c.y;
            if (c.y > maxY) maxY = c.y;
        }

        // Размер текущего rect (в локальных единицах canvas)
        float halfW = rt.rect.width * 0.5f;
        float halfH = rt.rect.height * 0.5f;

        float clampedX = Mathf.Clamp(desiredCanvasLocal.x, minX + halfW, maxX - halfW);
        float clampedY = Mathf.Clamp(desiredCanvasLocal.y, minY + halfH, maxY - halfH);

        return new Vector2(clampedX, clampedY);
    }

    // Осевой "магнит" и подсветка осей
    Vector2 ApplyAxisSnapAndHighlight(Vector2 pos)
    {
        bool snapX = Mathf.Abs(pos.x) <= snapAxisDistance;
        bool snapY = Mathf.Abs(pos.y) <= snapAxisDistance;

        if (snapX) pos.x = 0f;
        if (snapY) pos.y = 0f;

        if (xAxisImage != null)
            xAxisImage.color = snapX ? axisHighlightColor : new Color(xAxisImage.color.r, xAxisImage.color.g, xAxisImage.color.b, 0f);

        if (yAxisImage != null)
            yAxisImage.color = snapY ? axisHighlightColor : new Color(yAxisImage.color.r, yAxisImage.color.g, yAxisImage.color.b, 0f);

        return pos;
    }

    bool RayHitXRRect(RectTransform t)
    {
        if (t == null) return false;

        if (useXR)
        {
            if (!RaycastRectTransformPlane(t, out Vector3 worldHit)) return false;

            // проверяем, что hit также внутри drawSurface
            if (drawSurfaceRect != null)
            {
                Vector3 localOnDraw = drawSurfaceRect.InverseTransformPoint(worldHit);
                Rect drawRect = drawSurfaceRect.rect;
                if (localOnDraw.x < drawRect.xMin || localOnDraw.x > drawRect.xMax || localOnDraw.y < drawRect.yMin || localOnDraw.y > drawRect.yMax)
                    return false;
            }

            Vector3 local3 = t.InverseTransformPoint(worldHit);
            float halfX = t.rect.width / 2f;
            float halfY = t.rect.height / 2f;
            if (local3.x >= -halfX && local3.x <= halfX && local3.y >= -halfY && local3.y <= halfY) return true;

            // also check children bounds
            foreach (RectTransform child in t.GetComponentsInChildren<RectTransform>())
            {
                Vector3 cLocal = child.InverseTransformPoint(worldHit);
                float cx = child.rect.width / 2f;
                float cy = child.rect.height / 2f;
                if (cLocal.x >= -cx && cLocal.x <= cx && cLocal.y >= -cy && cLocal.y <= cy) return true;
            }
            return false;
        }
        else
        {
            // existing GraphicRaycaster-based behavior for mouse / screen UI
            PointerEventData pd = new PointerEventData(eventSystem) { position = Input.mousePosition };
            var list = new List<RaycastResult>();
            raycaster.Raycast(pd, list);
            foreach (var hit in list)
            {
                RectTransform rt = hit.gameObject.GetComponent<RectTransform>();
                if (rt == t || rt.IsChildOf(t)) return true;
            }
            return false;
        }
    }

    // ----------------------------------------------------------------------
    // DRAW RECTANGLE
    // ----------------------------------------------------------------------
    void HandleDrawRectangle()
    {
        if (pointerDown)
        {
            if (!TryGetLocalPointOnCanvas(out drawStartPos)) return;

            drawStartPos = Snap(drawStartPos);

            currentShape = Instantiate(rectPrefab, canvasRect);
            currentShape.sizeDelta = Vector2.zero;
            currentShape.anchoredPosition = drawStartPos;
        }

        if (currentShape != null && pressed)
        {
            if (!TryGetLocalPointOnCanvas(out Vector2 pos)) return;

            pos = Snap(pos);

            Vector2 size = pos - drawStartPos;
            float width = Mathf.Abs(size.x);
            float height = Mathf.Abs(size.y);

            // временно расположение центра и размеры, затем ограничим по drawSurface
            Vector2 center = drawStartPos + size / 2f;
            currentShape.sizeDelta = new Vector2(width, height);
            center = ApplyAxisSnapAndHighlight(center);
            center = ClampPositionToDrawSurface(currentShape, center);
            currentShape.anchoredPosition = center;
        }

        if (currentShape != null && pointerUp)
        {
            if (currentShape.sizeDelta.magnitude < 6f)
            {
                Destroy(currentShape.gameObject);
                return;
            }

            rectangle = currentShape;
            currentShape = null;
            stage = Stage.CenterRectangle;
        }
    }

    // ----------------------------------------------------------------------
    // CENTER RECTANGLE
    // ----------------------------------------------------------------------
    void HandleCenterRectangle()
    {
        if (rectangle == null) return;

        if (pointerDown && RayHitXRRect(rectangle))
            draggingRect = true;

        if (draggingRect && pressed)
        {
            if (!TryGetLocalPointOnCanvas(out Vector2 pos)) return;

            pos = Snap(pos);

            // axis snap + highlight
            pos = ApplyAxisSnapAndHighlight(pos);

            // center snap (в центр канвы)
            if (Vector2.Distance(pos, Vector2.zero) < snapCenterDistance) pos = Vector2.zero;

            // ограничиваем по drawSurface
            pos = ClampPositionToDrawSurface(rectangle, pos);

            rectangle.anchoredPosition = pos;
        }

        if (draggingRect && pointerUp)
        {
            draggingRect = false;
            if (Vector2.Distance(rectangle.anchoredPosition, Vector2.zero) < 0.1f)
                stage = Stage.DrawCircle;
            // сброс подсветки осей
            ClearAxisHighlights();
        }
    }

    // ----------------------------------------------------------------------
    // DRAW CIRCLE
    // ----------------------------------------------------------------------
    void HandleDrawCircle()
    {
        if (pointerDown)
        {
            if (!TryGetLocalPointOnCanvas(out drawStartPos)) return;
            drawStartPos = Snap(drawStartPos);

            currentShape = Instantiate(circlePrefab, canvasRect);
            currentShape.sizeDelta = Vector2.zero;
            currentShape.anchoredPosition = drawStartPos;
        }

        if (currentShape != null && pressed)
        {
            if (!TryGetLocalPointOnCanvas(out Vector2 pos)) return;
            pos = Snap(pos);

            Vector2 size = pos - drawStartPos;
            float side = Mathf.Max(Mathf.Abs(size.x), Mathf.Abs(size.y));
            currentShape.sizeDelta = new Vector2(side, side);

            Vector2 center = drawStartPos + size / 2f;
            center = ApplyAxisSnapAndHighlight(center);
            center = ClampPositionToDrawSurface(currentShape, center);
            currentShape.anchoredPosition = center;
        }

        if (currentShape != null && pointerUp)
        {
            if (currentShape.sizeDelta.magnitude < 6f)
            {
                Destroy(currentShape.gameObject);
                return;
            }

            circle = currentShape;
            currentShape = null;
            stage = Stage.CenterCircle;
        }
    }

    // ----------------------------------------------------------------------
    // CENTER CIRCLE
    // ----------------------------------------------------------------------
    void HandleCenterCircle()
    {
        if (circle == null || rectangle == null) return;

        if (pointerDown && RayHitXRRect(circle))
            draggingCircle = true;

        if (draggingCircle && pressed)
        {
            if (!TryGetLocalPointOnCanvas(out Vector2 pos)) return;
            pos = Snap(pos);

            pos = ApplyAxisSnapAndHighlight(pos);

            if (Vector2.Distance(pos, rectangle.anchoredPosition) < snapCenterDistance)
                pos = rectangle.anchoredPosition;

            pos = ClampPositionToDrawSurface(circle, pos);

            circle.anchoredPosition = pos;
        }

        if (draggingCircle && pointerUp)
        {
            draggingCircle = false;
            if (Vector2.Distance(circle.anchoredPosition, rectangle.anchoredPosition) < 0.1f)
            {
                if (dimensionButton != null) dimensionButton.gameObject.SetActive(true);
                stage = Stage.WaitDimensionButton;
            }
            ClearAxisHighlights();
        }
    }

    // ----------------------------------------------------------------------
    // BUTTON → DIMENSION MODE
    // ----------------------------------------------------------------------
    public void OnDimensionButtonPressed()
    {
        if (stage == Stage.WaitDimensionButton)
        {
            stage = Stage.DimensionRectangle;
            if (dimensionButton != null) dimensionButton.gameObject.SetActive(false);
        }
    }

    // ----------------------------------------------------------------------
    // RECTANGLE DIMENSION
    // ----------------------------------------------------------------------
    void HandleRectangleDimensionInput()
    {
        HighlightEdge();
        if (pointerDown) TryOpenDimensionWindow(rectangle, true);
    }

    // ----------------------------------------------------------------------
    // CIRCLE DIMENSION
    // ----------------------------------------------------------------------
    void HandleCircleDimensionInput()
    {
        ClearHighlight();
        if (pointerDown) TryOpenDimensionWindow(circle, false);
    }

    // ----------------------------------------------------------------------
    // OPEN DIMENSION WINDOW
    // ----------------------------------------------------------------------
    void TryOpenDimensionWindow(RectTransform target, bool isRect)
    {
        if (!RayHitXRRect(target)) return;
        OpenDimensionWindow(target, isRect);
    }

    void OpenDimensionWindow(RectTransform target, bool isRect)
    {
        if (currentWindow != null) Destroy(currentWindow);
        dimensionUIOpen = true;

        currentWindow = Instantiate(dimensionInputPrefab, canvasRect);

        // position window at intersection point with canvas (world-space) when using XR
        if (useXR && RaycastRectTransformPlane(canvasRect, out Vector3 worldHit))
        {
            currentWindow.transform.position = worldHit;
            // ensure window faces the same direction as canvas
            currentWindow.transform.rotation = canvasRect.rotation;
        }
        else
        {
            // fallback to screen position placement (works in screen-space)
            currentWindow.transform.position = Input.mousePosition;
        }

        TMP_InputField input = currentWindow.GetComponentInChildren<TMP_InputField>();
        Button ok = currentWindow.GetComponentInChildren<Button>();
        ok.onClick.RemoveAllListeners();

        if (isRect)
        {
            string side = GetEdgeSide(rectangle);
            ok.onClick.AddListener(() => { ApplyRectangleDimension(side, input.text); });
        }
        else
        {
            ok.onClick.AddListener(() => { ApplyCircleDimension(input.text); });
        }
    }

    // ----------------------------------------------------------------------
    // APPLY DIMENSIONS
    // ----------------------------------------------------------------------
    void ApplyRectangleDimension(string side, string value)
    {
        if (!float.TryParse(value, out float v)) return;
        if (rectangle == null) return;

        Vector2 sz = rectangle.sizeDelta;

        // горизонтальная линия → задаём высоту
        if (side == "Top" || side == "Bottom")
            sz.y = v;
        else
            sz.x = v;

        rectangle.sizeDelta = sz;
        CloseWindow();

        if (IsRectangleCorrect()) stage = Stage.DimensionCircle;
    }

    void ApplyCircleDimension(string value)
    {
        if (!float.TryParse(value, out float v)) return;
        if (circle == null) return;

        circle.sizeDelta = new Vector2(v, v);
        CloseWindow();

        if (Mathf.Abs(v - requiredCircleD) < 0.1f) stage = Stage.Done;
    }

    void CloseWindow()
    {
        if (currentWindow != null) Destroy(currentWindow);
        currentWindow = null;
        dimensionUIOpen = false;
    }

    // ----------------------------------------------------------------------
    // CHECKS
    // ----------------------------------------------------------------------
    bool IsRectangleCorrect()
    {
        if (rectangle == null) return false;
        float a = rectangle.sizeDelta.x;
        float b = rectangle.sizeDelta.y;

        return (Mathf.Abs(a - requiredRectA) < 1f && Mathf.Abs(b - requiredRectB) < 1f) ||
               (Mathf.Abs(b - requiredRectA) < 1f && Mathf.Abs(a - requiredRectB) < 1f);
    }

    // ----------------------------------------------------------------------
    // EDGE HIGHLIGHT + DETECT SIDE
    // ----------------------------------------------------------------------
    string GetEdgeSide(RectTransform rect)
    {
        if (rect == null) return "None";

        if (useXR)
        {
            if (!RaycastRectTransformPlane(rect, out Vector3 worldHit)) return "None";
            Vector3 local3 = rect.InverseTransformPoint(worldHit);
            float x = rect.rect.width / 2f;
            float y = rect.rect.height / 2f;

            if (Mathf.Abs(local3.x - x) < 15f) return "Right";
            if (Mathf.Abs(local3.x + x) < 15f) return "Left";
            if (Mathf.Abs(local3.y - y) < 15f) return "Top";
            if (Mathf.Abs(local3.y + y) < 15f) return "Bottom";
            return "None";
        }
        else
        {
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, Input.mousePosition, mainCamera, out local);

            float x = rect.rect.width / 2f;
            float y = rect.rect.height / 2f;

            if (Mathf.Abs(local.x - x) < 15f) return "Right";
            if (Mathf.Abs(local.x + x) < 15f) return "Left";
            if (Mathf.Abs(local.y - y) < 15f) return "Top";
            if (Mathf.Abs(local.y + y) < 15f) return "Bottom";

            return "None";
        }
    }

    void HighlightEdge()
    {
        ClearHighlight();

        string side = GetEdgeSide(rectangle);
        if (side == "None") return;

        Transform edge = rectangle.Find(side + "Edge");
        if (edge == null) return;

        Image img = edge.GetComponent<Image>();
        if (img == null) return;

        highlightedEdge = img;
        img.color = edgeHighlightColor;
    }

    void ClearHighlight()
    {
        if (highlightedEdge != null)
        {
            highlightedEdge.color = new Color(1, 1, 1, 0);
            highlightedEdge = null;
        }
    }

    void ClearAxisHighlights()
    {
        if (xAxisImage != null) xAxisImage.color = new Color(xAxisImage.color.r, xAxisImage.color.g, xAxisImage.color.b, 0f);
        if (yAxisImage != null) yAxisImage.color = new Color(yAxisImage.color.r, yAxisImage.color.g, yAxisImage.color.b, 0f);
    }

    // ----------------------------------------------------------------------
    // UTILS
    // ----------------------------------------------------------------------
    bool RayHit(RectTransform t)
    {
        // wrapper kept for compatibility; prefer XR-aware RayHitXRRect
        return RayHitXRRect(t);
    }

    Vector2 Snap(Vector2 p)
    {
        p.x = Mathf.Round(p.x / snapStep) * snapStep;
        p.y = Mathf.Round(p.y / snapStep) * snapStep;
        return p;
    }
}