using UnityEngine;
using UnityEngine.InputSystem;

public class StickRotate : MonoBehaviour
{
    [Header("Input")]
    public InputActionAsset actions;  // ТВОЙ XRI Input Actions Asset
    public string actionMapName = "XRI RightHand Locomotion";
    public string actionName = "Move";  // Экшен стика (обычно Move)

    [Header("Rotation Target")]
    public Transform target;     // Модель, которую вращаем
    public float rotationSpeed = 100f;

    private InputAction moveAction;

    void Start()
    {
        // Находим action map
        var map = actions.FindActionMap(actionMapName, true);
        moveAction = map.FindAction(actionName, true);

        moveAction.Enable();
    }

    void Update()
    {
        Vector2 input = moveAction.ReadValue<Vector2>();

        if (input.sqrMagnitude > 0.001f)
        {
            // Вращение модели
            target.Rotate(Vector3.up, input.x * rotationSpeed * Time.deltaTime);
            target.Rotate(Vector3.right, -input.y * rotationSpeed * Time.deltaTime);
        }
    }
}
