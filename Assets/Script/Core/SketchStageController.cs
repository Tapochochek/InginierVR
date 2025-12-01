// SketchStageController.cs
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(ShapeRegistry))]
public class SketchStageController : MonoBehaviour
{
    public ShapeDrawer drawer;
    public ShapeDimensioner dimensioner;
    public ShapeMover mover;
    public ShapeRegistry registry;

    [Header("UI")]
    public Button dimensionButton; // назначить в инспекторе
    public Text statusText; // опционально: показать "Эскиз полностью определён"

    // internal stage machine
    enum Stage { DrawRect, MoveRect, DrawCircle, MoveCircle, AwaitDimensionButton, Dimensioning, Done }
    Stage current = Stage.DrawRect;

    void Reset()
    {
        registry = GetComponent<ShapeRegistry>();
    }

    void OnEnable()
    {
        if (drawer != null) drawer.OnShapeCreated += OnShapeCreated;
        if (dimensioner != null)
        {
            dimensioner.OnRectangleDimensioned += OnRectangleDimensioned;
            dimensioner.OnCircleDimensioned += OnCircleDimensioned;
            dimensioner.OnAllTargetsMatched += OnAllTargetsMatched;
        }
        if (mover != null)
        {
            mover.OnRectanglePlacedAtCenter += OnRectanglePlacedAtCenter;
            mover.OnCirclePlacedAtRectCenter += OnCirclePlacedAtRectCenter;
        }

        if (dimensionButton != null)
        {
            dimensionButton.onClick.RemoveAllListeners();
            dimensionButton.onClick.AddListener(OnDimensionButtonClicked);
            dimensionButton.interactable = false; // активируем только когда оба объекта размещены
        }
    }

    void OnDisable()
    {
        if (drawer != null) drawer.OnShapeCreated -= OnShapeCreated;
        if (dimensioner != null)
        {
            dimensioner.OnRectangleDimensioned -= OnRectangleDimensioned;
            dimensioner.OnCircleDimensioned -= OnCircleDimensioned;
            dimensioner.OnAllTargetsMatched -= OnAllTargetsMatched;
        }
        if (mover != null)
        {
            mover.OnRectanglePlacedAtCenter -= OnRectanglePlacedAtCenter;
            mover.OnCirclePlacedAtRectCenter -= OnCirclePlacedAtRectCenter;
        }
        if (dimensionButton != null) dimensionButton.onClick.RemoveAllListeners();
    }

    void Start()
    {
        if (registry == null) registry = GetComponent<ShapeRegistry>();
        EnterStage_DrawRect();
    }

    void Update()
    {
        if (current == Stage.MoveRect)
            mover?.UpdateDraggingForRectangleIfNeeded();

        else if (current == Stage.MoveCircle)
            mover?.UpdateDraggingForCircleIfNeeded();

        else if (current == Stage.Dimensioning)
        {
            // Клик по элементу — запускает Dimensioner
            if (Input.GetMouseButtonDown(0))
                dimensioner?.TrySelectForDimension();

            // Наведение — подсвечивает грань
            dimensioner?.UpdateHoverHighlight();
        }
    }
    void EnterStage_DrawRect()
    {
        current = Stage.DrawRect;
        drawer?.BeginDrawRectangle();
        Debug.Log("[Stage] DrawRectangle");
        if (statusText != null) statusText.text = "Нарисуйте прямоугольник";
    }

    void OnShapeCreated(RectTransform shape, ShapeDrawer.ShapeKind kind)
    {
        if (kind == ShapeDrawer.ShapeKind.Rectangle)
        {
            registry.SetRectangle(shape);
            EnterStage_MoveRect();
        }
        else if (kind == ShapeDrawer.ShapeKind.Circle)
        {
            registry.SetCircle(shape);
            EnterStage_MoveCircle();
        }
    }

    void EnterStage_MoveRect()
    {
        current = Stage.MoveRect;
        // подготовка: даём mover знать, что прямоугольник готов для перемещения (он читает registry)
        Debug.Log("[Stage] MoveRectangle - перетащите прямоугольник в центр");
        if (statusText != null) statusText.text = "Перетащите прямоугольник в центр";
    }

    void OnRectanglePlacedAtCenter()
    {
        // вызван mover'ом когда rectangle точно в центре
        Debug.Log("Rectangle placed at center");
        // переход на рисование круга
        EnterStage_DrawCircle();
    }

    void EnterStage_DrawCircle()
    {
        current = Stage.DrawCircle;
        drawer?.BeginDrawCircle();
        Debug.Log("[Stage] DrawCircle");
        if (statusText != null) statusText.text = "Нарисуйте круг";
    }

    void EnterStage_MoveCircle()
    {
        current = Stage.MoveCircle;
        Debug.Log("[Stage] MoveCircle - перетащите круг в центр прямоугольника");
        if (statusText != null) statusText.text = "Перетащите круг в центр прямоугольника";
    }

    void OnCirclePlacedAtRectCenter()
    {
        Debug.Log("Circle placed at rect center");
        // теперь ждем нажатия кнопки размеров
        EnterStage_AwaitDimensionButton();
    }

    void EnterStage_AwaitDimensionButton()
    {
        current = Stage.AwaitDimensionButton;
        Debug.Log("[Stage] AwaitDimensionButton - нажмите кнопку 'Размеры' чтобы задать размеры");
        if (dimensionButton != null) dimensionButton.interactable = true;
        if (statusText != null) statusText.text = "Нажмите 'Размеры' чтобы задать размеры";
    }

    void OnDimensionButtonClicked()
    {
        if (current != Stage.AwaitDimensionButton) return;
        // выключаем кнопку и переходим в режим Dimensioning
        if (dimensionButton != null) dimensionButton.interactable = false;
        EnterStage_Dimensioning();
    }

    void EnterStage_Dimensioning()
    {
        current = Stage.Dimensioning;
        Debug.Log("[Stage] Dimensioning - кликните на грань прямоугольника и на круг, чтобы ввести размеры");
        if (statusText != null) statusText.text = "Введите размеры (60, 30 для прямоугольника; 10 для круга)";
        // dimensioner будет обрабатывать клики в этот момент (через TrySelectForDimension — его нужно вызвать на клике)
    }

    // callbacks from dimensioner (per shape)
    void OnRectangleDimensioned()
    {
        Debug.Log("[Event] Rectangle dimensioned");
    }

    void OnCircleDimensioned()
    {
        Debug.Log("[Event] Circle dimensioned");
    }

    void OnAllTargetsMatched()
    {
        Debug.Log("[Event] All targets matched - SKETCH COMPLETE");
        current = Stage.Done;
        if (statusText != null) statusText.text = "Эскиз полностью определён";
    }
}
