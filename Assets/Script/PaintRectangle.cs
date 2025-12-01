using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;

public class PainRectangle : MonoBehaviour
{

    // --- CONSTANTS ---
    private const float RequiredTolerance = 1f;
    private const float CenterTolerance = 0.1f;
    private const float MinDrawMagnitude = 6f;

    // --- ENUMS ---
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

    // --- PUBLIC FIELDS (Inspector Assignable) ---
    [Header("Canvas / Prefabs")]
    public RectTransform canvasRect;
    public RectTransform rectPrefab;
    public RectTransform circlePrefab;
    public GameObject dimensionInputPrefab;

    [Header("Draw surface")]
    public RawImage drawSurface; // Рисование и перемещение ограничены этой областью

    [Header("Dimension Button")]
    public Button dimensionButton;

    [Header("Draw Buttons")]
    public Button drawRectangleButton;
    public Button drawCircleButton;

    [Header("Snap settings")]
    public float snapStep = 10f;
    public float snapCenterDistance = 24f;

    [Header("Axis snap / highlight")]
    public float snapAxisDistance = 16f;
    public Image xAxisImage;
    public Image yAxisImage;
    public Color axisHighlightColor = new Color(0.3f, 0.6f, 1f, 0.85f);

    [Header("Edge Highlight")]
    public Color edgeHighlightColor = new Color(0.3f, 0.6f, 1f, 0.85f);

    [Header("XR Settings (world-space)")]
    public bool useXR = true;
    public XRRayInteractor xrInteractor;
    public XRNode xrNode = XRNode.RightHand;
    public Camera mainCamera;

    [Header("Step Trackers")]
    public Image checker;
    public Image step1Check;
    public Image step2Check;
    public Image step3Check;
    public Image step4Check;
    public Image step5Check;
    public Image step6Check;


    // --- REQUIRED DIMENSIONS (×10/20 depending on input scaling) ---
    private const float requiredRectA = 600f; // 60×10 (or 60×20 in other variants)
    private const float requiredRectB = 300f;  // 30×10
    private const float requiredCircleD = 200f; // 10×20

    // --- PRIVATE STATE ---
    public Stage stage = Stage.DrawRectangle;

    private RectTransform rectangle;
    private RectTransform circle;
    private RectTransform currentShape = null;
    private Vector2 drawStartPos;

    private GraphicRaycaster raycaster;
    private EventSystem eventSystem;
    private GameObject currentWindow = null;
    private bool dimensionUIOpen = false;
    private Image highlightedEdge = null;

    private bool draggingRect = false;
    private bool draggingCircle = false;

    // XR / Input state
    private InputDevice xrDevice;
    private bool prevPressed = false;
    private bool pressed = false;
    private bool pointerDown = false;
    private bool pointerUp = false;

    // --- CACHED / OPTIMIZED VARIABLES ---
    private RectTransform drawSurfaceRect;
    private Rect drawSurfaceLocalRect; // Кэшированные границы DrawSurface в локальных координатах Canvas!
    private List<RaycastResult> raycastResults = new List<RaycastResult>(); // Для оптимизации GC Allocations!

    // --- DRAW MODE FLAGS ---
    private bool rectangleDrawMode = false;
    private bool circleDrawMode = false;

    // ======================================================================
    // UNITY LIFECYCLE
    // ======================================================================

    void Start()
    {
        raycaster = canvasRect.GetComponentInParent<GraphicRaycaster>();
        eventSystem = EventSystem.current;

        if (dimensionButton != null)
        {
            dimensionButton.onClick.AddListener(OnDimensionButtonPressed);
            dimensionButton.gameObject.SetActive(false);
        }

        // Setup draw buttons
        if (drawRectangleButton != null)
        {
            drawRectangleButton.onClick.AddListener(() =>
            {
                rectangleDrawMode = true;
                circleDrawMode = false;
                stage = Stage.DrawRectangle;
            });
            drawRectangleButton.gameObject.SetActive(true);
        }

        if (drawCircleButton != null)
        {
            drawCircleButton.onClick.AddListener(() =>
            {
                circleDrawMode = true;
                rectangleDrawMode = false;
                stage = Stage.DrawCircle;
            });
            drawCircleButton.gameObject.SetActive(false); // hidden initially
        }

        if (useXR)
        {
            xrDevice = InputDevices.GetDeviceAtXRNode(xrNode);
            if (!xrDevice.isValid)
            {
                InputDevices.deviceConnected += OnDeviceConnected;
            }
        }

        if (mainCamera == null)
            mainCamera = Camera.main;

        if (drawSurface != null)
        {
            drawSurfaceRect = drawSurface.rectTransform;
            CacheDrawSurfaceLocalRect(); // Кэширование границ
        }
    }

    void OnDestroy()
    {
        InputDevices.deviceConnected -= OnDeviceConnected;
    }

    void OnDeviceConnected(InputDevice device)
    {
        xrDevice = InputDevices.GetDeviceAtXRNode(xrNode);
    }

    void Update()
    {
        UpdatePointerState();

        if (dimensionUIOpen) return;

        // Prevent drawing unless corresponding mode/button is active
        if ((stage == Stage.DrawRectangle && !rectangleDrawMode) ||
            (stage == Stage.DrawCircle && !circleDrawMode))
        {
            // still allow other stages to run (centering, dimensioning etc.)
            if (stage == Stage.DrawRectangle || stage == Stage.DrawCircle)
                return;
        }

        switch (stage)
        {
            case Stage.DrawRectangle:
                HandleDrawShape(rectPrefab, ref rectangle, Stage.CenterRectangle, false);
                

                break;
            case Stage.CenterRectangle:
                HandleCenterRectangle();
                
                break;
            case Stage.DrawCircle:
                HandleDrawShape(circlePrefab, ref circle, Stage.CenterCircle, true);
                
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

    // ======================================================================
    // XR / INPUT HELPERS
    // ======================================================================

    void UpdatePointerState()
    {
        prevPressed = pressed;

        if (useXR && xrDevice.isValid)
        {
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

        // Используем rotation * forward для корректности в world-space canvas
        Plane plane = new Plane(rect.rotation * Vector3.forward, rect.position);

        if (plane.Raycast(ray, out float enter))
        {
            hitPoint = ray.GetPoint(enter);
            return true;
        }

        return false;
    }

    bool TryGetLocalPointOnCanvasFromPointer(out Vector2 canvasLocal)
    {
        canvasLocal = Vector2.zero;

        if (!TryGetWorldRay(out Ray ray))
            return false;

        Plane plane = new Plane(canvasRect.rotation * Vector3.forward, canvasRect.position);
        if (!plane.Raycast(ray, out float enter))
            return false;

        Vector3 worldHit = ray.GetPoint(enter);

        // Проверка, находится ли worldHit внутри drawSurface (с использованием кэша)
        if (drawSurfaceRect != null)
        {
            Vector3 localOnDraw = canvasRect.InverseTransformPoint(worldHit);
            if (!drawSurfaceLocalRect.Contains(localOnDraw))
                return false;
        }

        Vector3 canvasLocal3 = canvasRect.InverseTransformPoint(worldHit);
        canvasLocal = new Vector2(canvasLocal3.x, canvasLocal3.y);
        return true;
    }

    bool TryGetLocalPointOnCanvasFromMouse(out Vector2 canvasLocal)
    {
        canvasLocal = Vector2.zero;
        if (mainCamera == null) return false;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, Input.mousePosition, mainCamera, out Vector2 local))
            return false;

        // Проверка, находится ли local point внутри drawSurface (с использованием кэша)
        if (drawSurfaceRect != null)
        {
            if (!drawSurfaceLocalRect.Contains(local))
                return false;
        }

        canvasLocal = local;
        return true;
    }

    bool TryGetLocalPointOnCanvas(out Vector2 canvasLocal)
    {
        return useXR
            ? TryGetLocalPointOnCanvasFromPointer(out canvasLocal)
            : TryGetLocalPointOnCanvasFromMouse(out canvasLocal);
    }

    // Оптимизированный RayHit для XR/Mouse
    bool RayHitShape(RectTransform t)
    {
        if (t == null) return false;

        if (useXR)
        {
            if (!RaycastRectTransformPlane(t, out Vector3 worldHit)) return false;

            // 1. Проверка попадания в drawSurface (через кэшированный Rect)
            if (drawSurfaceRect != null)
            {
                Vector3 localOnDraw = canvasRect.InverseTransformPoint(worldHit);
                if (!drawSurfaceLocalRect.Contains(localOnDraw)) return false;
            }

            // 2. Проверка попадания в Bounds самого RectTransform или его дочерних элементов
            Vector3 local3 = t.InverseTransformPoint(worldHit);

            // Проверка самого RectTransform
            if (t.rect.Contains(new Vector2(local3.x, local3.y))) return true;

            // Проверка дочерних элементов (без частого GetComponentsInChildren)
            foreach (RectTransform child in t.GetComponentsInChildren<RectTransform>())
            {
                if (child == t) continue;
                Vector3 cLocal = child.InverseTransformPoint(worldHit);
                if (child.rect.Contains(new Vector2(cLocal.x, cLocal.y))) return true;
            }

            return false;
        }
        else
        {
            // Non-XR: Устранение GC Allocations!
            PointerEventData pd = new PointerEventData(eventSystem) { position = Input.mousePosition };
            raycastResults.Clear();
            raycaster.Raycast(pd, raycastResults);

            foreach (var hit in raycastResults)
            {
                // Проверяем, попали ли мы в t или в его дочерний элемент
                var rt = hit.gameObject.GetComponent<RectTransform>();
                if (rt == null) continue;

                if (rt == t || rt.IsChildOf(t))
                {
                    // Дополнительная проверка: находится ли указатель внутри drawSurface?
                    if (drawSurfaceRect != null)
                    {
                        if (!TryGetLocalPointOnCanvas(out Vector2 canvasLocalCheck)) continue; // Попали, но вне DrawSurface
                    }
                    return true;
                }
            }
            return false;
        }
    }


    // ======================================================================
    // CORE LOGIC
    // ======================================================================

    // --- DRAW SHAPE (Unified Method) ---
    void HandleDrawShape(RectTransform prefab, ref RectTransform shapeInstance, Stage nextStage, bool isCircle)
    {
        // Mode gating: do not allow drawing if corresponding button/mode is off
        if (isCircle && !circleDrawMode) return;
        if (!isCircle && !rectangleDrawMode) return;

        if (pointerDown)
        {
            if (!TryGetLocalPointOnCanvas(out drawStartPos)) return;

            drawStartPos = Snap(drawStartPos);

            currentShape = Instantiate(prefab, canvasRect);
            currentShape.sizeDelta = Vector2.zero;
            currentShape.anchoredPosition = drawStartPos;

            // If circle, ensure it appears above rectangle in world-space canvas
            if (isCircle)
            {
                currentShape.SetAsLastSibling();
                Vector3 lp = currentShape.localPosition;
                lp.z -= 0.001f;
                currentShape.localPosition = lp;
            }
        }

        if (currentShape != null && pressed)
        {
            if (!TryGetLocalPointOnCanvas(out Vector2 pos)) return;

            pos = Snap(pos);

            Vector2 size = pos - drawStartPos;

            // Unified size calc
            float width, height;
            if (isCircle)
            {
                float side = Mathf.Max(Mathf.Abs(size.x), Mathf.Abs(size.y));
                width = side;
                height = side;
            }
            else
            {
                width = Mathf.Abs(size.x);
                height = Mathf.Abs(size.y);
            }

            currentShape.sizeDelta = new Vector2(width, height);
            Vector2 center = drawStartPos + size / 2f;

            // Apply axis snap and clamp
            center = ApplyAxisSnapAndHighlight(center);
            center = ClampPositionToDrawSurface(currentShape, center);
            currentShape.anchoredPosition = center;
        }

        if (currentShape != null && pointerUp)
        {
            if (currentShape.sizeDelta.magnitude < MinDrawMagnitude)
            {
                Destroy(currentShape.gameObject);
                currentShape = null;
                return;
            }

            shapeInstance = currentShape;
            currentShape = null;
            stage = nextStage;
            

            // If rectangle finished, disable rectangle draw mode and show circle button
            if (!isCircle)
            {
                rectangleDrawMode = false;
                if (drawRectangleButton != null) drawRectangleButton.gameObject.SetActive(false);
                if (drawCircleButton != null) drawCircleButton.gameObject.SetActive(true);
                MarkStep(1);
            }
            else
            {
                // If circle finished, disable circle draw mode and hide circle button
                circleDrawMode = false;
                if (drawCircleButton != null) drawCircleButton.gameObject.SetActive(false);
                MarkStep(3);
            }
        }
    }

    // --- CENTER RECTANGLE ---
    void HandleCenterRectangle()
    {
        if (rectangle == null) return;

        if (pointerDown && RayHitShape(rectangle))
            draggingRect = true;

        if (draggingRect && pressed)
        {
            if (!TryGetLocalPointOnCanvas(out Vector2 pos)) return;

            pos = Snap(pos);
            pos = ApplyAxisSnapAndHighlight(pos);

            // Центровка к (0,0)
            if (Vector2.Distance(pos, Vector2.zero) < snapCenterDistance)
                pos = Vector2.zero;

            pos = ClampPositionToDrawSurface(rectangle, pos);
            rectangle.anchoredPosition = pos;
        }

        if (draggingRect && pointerUp)
        {
            draggingRect = false;
            ClearAxisHighlights();

            // Переход к следующему этапу, если центровка успешна
            if (Vector2.Distance(rectangle.anchoredPosition, Vector2.zero) < CenterTolerance)
            {
                MarkStep(2);
                stage = Stage.DrawCircle;

                // Make sure circle button is active to allow explicit start
                if (drawCircleButton != null) drawCircleButton.gameObject.SetActive(true);
            }
        }
    }

    // --- CENTER CIRCLE ---
    void HandleCenterCircle()
    {
        if (circle == null || rectangle == null) return;

        if (pointerDown && RayHitShape(circle))
            draggingCircle = true;

        if (draggingCircle && pressed)
        {
            if (!TryGetLocalPointOnCanvas(out Vector2 pos)) return;
            pos = Snap(pos);
            pos = ApplyAxisSnapAndHighlight(pos);

            // Центровка к центру прямоугольника
            Vector2 rectCenter = rectangle.anchoredPosition;
            if (Vector2.Distance(pos, rectCenter) < snapCenterDistance)
                pos = rectCenter;

            pos = ClampPositionToDrawSurface(circle, pos);
            circle.anchoredPosition = pos;
        }

        if (draggingCircle && pointerUp)
        {
            draggingCircle = false;
            ClearAxisHighlights();

            // Переход к следующему этапу, если центровка успешна
            if (Vector2.Distance(circle.anchoredPosition, rectangle.anchoredPosition) < CenterTolerance)
            {
                MarkStep(4);
                // отключаем режим рисования круга
                circleDrawMode = false;

                // скрываем кнопку круга
                if (drawCircleButton != null) drawCircleButton.gameObject.SetActive(false);

                if (dimensionButton != null) dimensionButton.gameObject.SetActive(true);
                stage = Stage.WaitDimensionButton;
            }
        }
    }

    // --- DIMENSION INPUTS ---

    void HandleRectangleDimensionInput()
    {
        HighlightEdge();
        if (pointerDown) TryOpenDimensionWindow(rectangle, true);
    }

    void HandleCircleDimensionInput()
    {
        ClearHighlight();
        if (pointerDown) TryOpenDimensionWindow(circle, false);
    }

    // ======================================================================
    // UI / DIMENSION LOGIC
    // ======================================================================

    public void OnDimensionButtonPressed()
    {
        if (stage == Stage.WaitDimensionButton)
        {
            stage = Stage.DimensionRectangle;
            if (dimensionButton != null) dimensionButton.gameObject.SetActive(false);
        }
    }

    void TryOpenDimensionWindow(RectTransform target, bool isRect)
    {
        if (!RayHitShape(target)) return;
        OpenDimensionWindow(target, isRect);
    }

    void OpenDimensionWindow(RectTransform target, bool isRect)
    {
        if (currentWindow != null) Destroy(currentWindow);
        dimensionUIOpen = true;

        currentWindow = Instantiate(dimensionInputPrefab, canvasRect);

        // Позиционируем окно в World Space, используя Raycast, если возможно
        Vector3 windowPosition = target.position;
        if (TryGetWorldRay(out Ray ray))
        {
            if (RaycastRectTransformPlane(canvasRect, out Vector3 worldHit))
                windowPosition = worldHit;
        }

        currentWindow.transform.position = windowPosition;
        currentWindow.transform.rotation = canvasRect.rotation;

        // Смещаем окно немного вперед, чтобы оно было перед плоскостью
        currentWindow.transform.position -= currentWindow.transform.forward * 0.002f;

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

    void ApplyRectangleDimension(string side, string value)
    {
        if (!float.TryParse(value, out float v)) return;
        if (rectangle == null) return;

        Vector2 sz = rectangle.sizeDelta;

        // NOTE: original code used different multipliers; adapt as needed
        if (side == "Left" || side == "Right")
            sz.y = v * 10f;
        else if (side == "Top" || side == "Bottom")
            sz.x = v * 10f;
        else
            return; // Замер не на ребре

        rectangle.sizeDelta = sz;
        CloseWindow();

        if (IsRectangleCorrect())
        {
            MarkStep(5);
            stage = Stage.DimensionCircle;
        }
    }

    void ApplyCircleDimension(string value)
    {
        if (!float.TryParse(value, out float v)) return;
        if (circle == null) return;
        v *= 20f;
        circle.sizeDelta = new Vector2(v, v);
        CloseWindow();

        if (Mathf.Abs(v - requiredCircleD) < RequiredTolerance) stage = Stage.Done;
        MarkStep(6);
    }

    void CloseWindow()
    {
        if (currentWindow != null) Destroy(currentWindow);
        currentWindow = null;
        dimensionUIOpen = false;
    }

    bool IsRectangleCorrect()
    {
        if (rectangle == null) return false;
        float a = rectangle.sizeDelta.x;
        float b = rectangle.sizeDelta.y;

        // Проверка в любом порядке A x B или B x A
        bool correct1 = Mathf.Abs(a - requiredRectA) < RequiredTolerance && Mathf.Abs(b - requiredRectB) < RequiredTolerance;
        bool correct2 = Mathf.Abs(b - requiredRectA) < RequiredTolerance && Mathf.Abs(a - requiredRectB) < RequiredTolerance;

        return correct1 || correct2;
    }

    // ======================================================================
    // UTILS / CACHING
    // ======================================================================

    void CacheDrawSurfaceLocalRect()
    {
        if (drawSurfaceRect == null) return;

        Vector3[] worldCorners = new Vector3[4];
        drawSurfaceRect.GetWorldCorners(worldCorners);

        float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
        for (int i = 0; i < 4; i++)
        {
            Vector3 c = canvasRect.InverseTransformPoint(worldCorners[i]);
            minX = Mathf.Min(minX, c.x); maxX = Mathf.Max(maxX, c.x);
            minY = Mathf.Min(minY, c.y); maxY = Mathf.Max(maxY, c.y);
        }
        // Храним minX/minY и maxX/maxY в одном Rect для быстрой проверки Contains
        drawSurfaceLocalRect = new Rect(minX, minY, maxX - minX, maxY - minY);
    }

    Vector2 ClampPositionToDrawSurface(RectTransform rt, Vector2 desiredCanvasLocal)
    {
        if (drawSurfaceRect == null) return desiredCanvasLocal;

        float halfW = rt.rect.width * 0.5f;
        float halfH = rt.rect.height * 0.5f;

        float clampedX = Mathf.Clamp(desiredCanvasLocal.x, drawSurfaceLocalRect.xMin + halfW, drawSurfaceLocalRect.xMax - halfW);
        float clampedY = Mathf.Clamp(desiredCanvasLocal.y, drawSurfaceLocalRect.yMin + halfH, drawSurfaceLocalRect.yMax - halfH);

        return new Vector2(clampedX, clampedY);
    }

    Vector2 Snap(Vector2 p)
    {
        p.x = Mathf.Round(p.x / snapStep) * snapStep;
        p.y = Mathf.Round(p.y / snapStep) * snapStep;
        return p;
    }

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
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, Input.mousePosition, mainCamera, out local))
                return "None";

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

    void MarkStep(int step)
    {
        switch (step)
        {
            case 1: if (step1Check) step1Check.sprite = checker.sprite; break;
            case 2: if (step2Check) step2Check.sprite = checker.sprite; break;
            case 3: if (step3Check) step3Check.sprite = checker.sprite; break;
            case 4: if (step4Check) step4Check.sprite = checker.sprite; break;
            case 5: if (step5Check) step5Check.sprite = checker.sprite; break;
            case 6: if (step6Check) step6Check.sprite = checker.sprite; break;
        }
    }


    // ======================================================================
    // END
    // ======================================================================
}
