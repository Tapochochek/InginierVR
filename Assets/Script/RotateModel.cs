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

    private void OnEnable()
    {
        InitInput();
    }

    private void OnDisable()
    {
        if (moveAction != null)
            moveAction.Disable();
    }

    private void InitInput()
    {
        if (actions == null)
        {
            Debug.LogError("StickRotate: actions asset not assigned!");
            return;
        }

        var map = actions.FindActionMap(actionMapName, false);
        if (map == null)
        {
            Debug.LogError("StickRotate: action map not found: " + actionMapName);
            return;
        }

        moveAction = map.FindAction(actionName, false);
        if (moveAction == null)
        {
            Debug.LogError("StickRotate: action not found: " + actionName);
            return;
        }

        moveAction.Enable();

        Debug.Log("StickRotate: Input reinitialized!");
    }

    private void Update()
    {
        if (moveAction == null) return;

        Vector2 input = moveAction.ReadValue<Vector2>();

        if (input.sqrMagnitude > 0.001f)
        {
            target.Rotate(Vector3.up, input.x * rotationSpeed * Time.deltaTime);
            target.Rotate(Vector3.right, -input.y * rotationSpeed * Time.deltaTime);
        }
    }
}
