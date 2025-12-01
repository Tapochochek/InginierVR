using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.XR.Interaction.Toolkit;

public class VRSketchManager : MonoBehaviour
{
    [Header("Canvas")]
    public RectTransform canvasRect;          // Canvas (World Space)

    [Header("Prefabs")]
    public RectTransform rectPrefab;
    public RectTransform circlePrefab;
    public GameObject dimensionWindowPrefab;

    [Header("VR")]
    public XRRayInteractor rayInteractor;     // Луч контроллера

    [Header("Snap")]
    public float snapStep = 10f;
    public float snapCenterDistance = 30f;

    [Header("Dimensions Required (×20)")]
    public float rectA = 1200f;   // 60×20
    public float rectB = 600f;    // 30×20
    public float circleD = 200f;  // 10×20

    // ========================
    // INTERNAL STATE
    // ========================
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

    RectTransform rectangle;
    RectTransform circle;

    RectTransform drawingShape = null;
    Vector2 drawStartLocal;

    GameObject dimensionWindow = null;
    bool dimensionUIOpen = false;

    RectTransform highlightedEdge = null;

    bool draggingShape = false;

    [Header("UI")]
    public Button dimensionButton;

    // ==========================
    // UNITY
    // ==========================
    void Start()
    {
        dimensionButton.gameObject.SetActive(false);
        dimensionButton.onClick.AddListener(() =>
        {
            stage = Stage.DimensionRectangle;
            dimensionButton.gameObject.SetActive(false);
        });
    }

    void Update()
    {
        if (dimensionUIOpen) return;

        switch (stage)
        {
            case Stage.DrawRectangle: VR_DrawRectangle(); break;
            case Stage.CenterRectangle: VR_CenterRectangle(); break;

            case Stage.DrawCircle: VR_DrawCircle(); break;
            case Stage.CenterCircle: VR_CenterCircle(); break;

            case Stage.DimensionRectangle: VR_HandleRectDimension(); break;
            case Stage.DimensionCircle: VR_HandleCircleDimension(); break;
        }
    }

    // =================================================================
    //  VR INPUT HELPERS
    // =================================================================
    bool RayHitUI(out GameObject obj)
    {
        obj = null;

        rayInteractor.TryGetCurrentUIRaycastResult(out var uiHit);
        if (uiHit.isValid)
        {
            obj = uiHit.gameObject;
            return true;
        }
        return false;
    }

    bool RayHitWorld(out RaycastHit hit)
    {
        return rayInteractor.TryGetCurrent3DRaycastHit(out hit);
    }

    bool TriggerDown()
    {
        return rayInteractor.xrController.activateInteractionState.activatedThisFrame;
    }

    bool TriggerPressed()
    {
        return rayInteractor.xrController.activateInteractionState.active;
    }

    bool TriggerUp()
    {
        return rayInteractor.xrController.activateInteractionState.deactivatedThisFrame;
    }

    // Convert world → local canvas
    Vector2 WorldToCanvas(Vector3 worldPos)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            Camera.main.WorldToScreenPoint(worldPos),
            null,
            out Vector2 local
        );
        return Snap(local);
    }

    Vector2 Snap(Vector2 p)
    {
        p.x = Mathf.Round(p.x / snapStep) * snapStep;
        p.y = Mathf.Round(p.y / snapStep) * snapStep;
        return p;
    }

    // ================================================
    //  DRAW RECTANGLE
    // ================================================
    void VR_DrawRectangle()
    {
        if (!RayHitWorld(out var hit)) return;

        if (TriggerDown())
        {
            drawStartLocal = WorldToCanvas(hit.point);

            drawingShape = Instantiate(rectPrefab, canvasRect);
            drawingShape.anchoredPosition = drawStartLocal;
            drawingShape.sizeDelta = Vector2.zero;
        }

        if (drawingShape != null && TriggerPressed())
        {
            Vector2 now = WorldToCanvas(hit.point);
            Vector2 diff = now - drawStartLocal;

            drawingShape.sizeDelta = new Vector2(Mathf.Abs(diff.x), Mathf.Abs(diff.y));
            drawingShape.anchoredPosition = drawStartLocal + diff / 2f;
        }

        if (drawingShape != null && TriggerUp())
        {
            if (drawingShape.sizeDelta.magnitude < 5)
            {
                Destroy(drawingShape.gameObject);
                drawingShape = null;
                return;
            }

            rectangle = drawingShape;
            drawingShape = null;

            stage = Stage.CenterRectangle;
        }
    }

    // ================================================
    //  CENTER RECTANGLE
    // ================================================
    void VR_CenterRectangle()
    {
        if (!RayHitUI(out var uiObj))
        {
            // hit world?
            if (!RayHitWorld(out var hit)) return;

            // click on rectangle
            if (TriggerDown() && hit.collider && hit.collider.GetComponent<RectTransform>() == rectangle)
                draggingShape = true;

            if (draggingShape && TriggerPressed())
            {
                Vector2 local = WorldToCanvas(hit.point);

                if (Vector2.Distance(local, Vector2.zero) < snapCenterDistance)
                    local = Vector2.zero;

                rectangle.anchoredPosition = local;
            }

            if (draggingShape && TriggerUp())
            {
                draggingShape = false;

                if (Vector2.Distance(rectangle.anchoredPosition, Vector2.zero) < 2f)
                    stage = Stage.DrawCircle;
            }
        }
    }

    // ================================================
    //  DRAW CIRCLE
    // ================================================
    void VR_DrawCircle()
    {
        if (!RayHitWorld(out var hit)) return;

        if (TriggerDown())
        {
            drawStartLocal = WorldToCanvas(hit.point);

            drawingShape = Instantiate(circlePrefab, canvasRect);
            drawingShape.sizeDelta = Vector2.zero;
            drawingShape.anchoredPosition = drawStartLocal;
        }

        if (drawingShape != null && TriggerPressed())
        {
            Vector2 now = WorldToCanvas(hit.point);
            Vector2 diff = now - drawStartLocal;

            float side = Mathf.Max(Mathf.Abs(diff.x), Mathf.Abs(diff.y));
            drawingShape.sizeDelta = new Vector2(side, side);
            drawingShape.anchoredPosition = drawStartLocal + diff / 2f;
        }

        if (drawingShape != null && TriggerUp())
        {
            if (drawingShape.sizeDelta.magnitude < 5)
            {
                Destroy(drawingShape.gameObject);
                drawingShape = null;
                return;
            }

            circle = drawingShape;
            drawingShape = null;

            stage = Stage.CenterCircle;
        }
    }

    // ================================================
    //  CENTER CIRCLE
    // ================================================
    void VR_CenterCircle()
    {
        if (!RayHitWorld(out var hit)) return;

        if (TriggerDown() && hit.collider && hit.collider.GetComponent<RectTransform>() == circle)
            draggingShape = true;

        if (draggingShape && TriggerPressed())
        {
            Vector2 local = WorldToCanvas(hit.point);

            // snap to rectangle center
            if (Vector2.Distance(local, rectangle.anchoredPosition) < snapCenterDistance)
                local = rectangle.anchoredPosition;

            circle.anchoredPosition = local;
        }

        if (draggingShape && TriggerUp())
        {
            draggingShape = false;

            if (Vector2.Distance(circle.anchoredPosition, rectangle.anchoredPosition) < 2f)
            {
                dimensionButton.gameObject.SetActive(true);
                stage = Stage.WaitDimensionButton;
            }
        }
    }

    // ================================================
    //  DIMENSION RECTANGLE
    // ================================================
    void VR_HandleRectDimension()
    {
        HighlightRectEdge(rectangle);

        if (TriggerDown() && RayHitWorld(out var hit))
        {
            if (TrySelectRectEdge(rectangle, hit.point, out var side))
                OpenDimensionWindow(side, true);
        }
    }

    // ================================================
    //  DIMENSION CIRCLE
    // ================================================
    void VR_HandleCircleDimension()
    {
        ClearHighlight();

        if (TriggerDown() && RayHitWorld(out var hit))
        {
            if (hit.collider && hit.collider.GetComponent<RectTransform>() == circle)
                OpenDimensionWindow("Circle", false);
        }
    }

    // =============================================================
    //  DIMENSION WINDOW
    // =============================================================
    void OpenDimensionWindow(string side, bool isRect)
    {
        if (dimensionWindow != null)
            Destroy(dimensionWindow);

        dimensionUIOpen = true;

        dimensionWindow = Instantiate(dimensionWindowPrefab, canvasRect);
        dimensionWindow.transform.position = Camera.main.WorldToScreenPoint(rayInteractor.transform.position + rayInteractor.transform.forward * 0.5f);

        TMP_InputField input = dimensionWindow.GetComponentInChildren<TMP_InputField>();
        Button ok = dimensionWindow.GetComponentInChildren<Button>();

        ok.onClick.AddListener(() =>
        {
            float.TryParse(input.text, out float v);

            if (isRect) ApplyRectDimension(side, v);
            else ApplyCircleDimension(v);

            Destroy(dimensionWindow);
            dimensionUIOpen = false;
        });
    }

    // =============================================================
    // APPLY DIMENSIONS
    // =============================================================
    void ApplyRectDimension(string side, float value)
    {
        Vector2 s = rectangle.sizeDelta;

        if (side == "Top" || side == "Bottom")
            s.y = value;
        else
            s.x = value;

        rectangle.sizeDelta = s;

        if (CheckRectDone())
            stage = Stage.DimensionCircle;
    }

    void ApplyCircleDimension(float value)
    {
        circle.sizeDelta = new Vector2(value, value);

        if (Mathf.Abs(value - circleD) < 1f)
            stage = Stage.Done;
    }

    bool CheckRectDone()
    {
        float a = rectangle.sizeDelta.x;
        float b = rectangle.sizeDelta.y;

        return
            (Mathf.Abs(a - rectA) < 2f && Mathf.Abs(b - rectB) < 2f) ||
            (Mathf.Abs(a - rectB) < 2f && Mathf.Abs(b - rectA) < 2f);
    }

    // =============================================================
    // EDGE DETECTION & HIGHLIGHT
    // =============================================================
    bool TrySelectRectEdge(RectTransform rect, Vector3 worldPoint, out string side)
    {
        Vector2 local;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rect,
            Camera.main.WorldToScreenPoint(worldPoint),
            null,
            out local
        );

        float hx = rect.sizeDelta.x / 2f;
        float hy = rect.sizeDelta.y / 2f;

        if (Mathf.Abs(local.x - hx) < 20f) { side = "Right"; return true; }
        if (Mathf.Abs(local.x + hx) < 20f) { side = "Left"; return true; }
        if (Mathf.Abs(local.y - hy) < 20f) { side = "Top"; return true; }
        if (Mathf.Abs(local.y + hy) < 20f) { side = "Bottom"; return true; }

        side = "";
        return false;
    }

    void HighlightRectEdge(RectTransform rect)
    {
        ClearHighlight();

        if (RayHitWorld(out var hit))
        {
            if (TrySelectRectEdge(rect, hit.point, out var side))
            {
                var edge = rect.Find(side + "Edge") as RectTransform;
                if (edge)
                {
                    edge.GetComponent<Image>().color = new Color(0.3f, 0.6f, 1f, 0.8f);
                    highlightedEdge = edge;
                }
            }
        }
    }

    void ClearHighlight()
    {
        if (highlightedEdge)
            highlightedEdge.GetComponent<Image>().color = new Color(1, 1, 1, 0);

        highlightedEdge = null;
    }
}
