using UnityEngine;
using UnityEngine.InputSystem;

public class StickRotate : MonoBehaviour
{
    [Header("Input")]
    public InputActionAsset actions;
    public string actionMapName = "XRI RightHand Locomotion";
    public string actionName = "Move";

    [Header("Rotation Target")]
    public Transform target;
    public float rotationSpeed = 100f;

    private InputAction moveAction;

    // Флаг разрешения вращения — безопасный!
    public bool allowRotation = false;

    private void Start()
    {
        InitInput();
    }

    private void InitInput()
    {
        var map = actions.FindActionMap(actionMapName, false);
        moveAction = map.FindAction(actionName, false);
        moveAction.Enable();   // << ВКЛЮЧАЕМ ТОЛЬКО ОДИН РАЗ
    }

    private void Update()
    {
        if (!allowRotation) return; // << БЛОКИРУЕМ безопасно

        if (moveAction == null) return;

        Vector2 input = moveAction.ReadValue<Vector2>();
        if (input.sqrMagnitude > 0.001f)
        {
            target.Rotate(Vector3.up, input.x * rotationSpeed * Time.deltaTime);
            target.Rotate(Vector3.right, -input.y * rotationSpeed * Time.deltaTime);
        }
    }

    // Вызывается из Scenary
    public void ActivateRotation()
    {
        allowRotation = true;
        Debug.Log("Rotation activated");
    }

    public void DeactivateRotation()
    {
        allowRotation = false;
        Debug.Log("Rotation deactivated");
    }
}
