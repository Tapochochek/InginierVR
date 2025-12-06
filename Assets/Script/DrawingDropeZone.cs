// Имя файла: DrawingDropZone.cs
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using Unity.XR.CoreUtils; // Потребуется для InputHelpers в новых версиях

public class DrawingDropZone : MonoBehaviour
{
    [Header("Объекты Сцены")]
    [Tooltip("Модель, которую нужно сбросить. (Объект с XR Grab Interactable)")]
    public GameObject targetModel;

    [Tooltip("Canvas, который нужно показать после завершения.")]
    public GameObject readyCanvas;

    [Header("Настройки Завершения")]
    [Tooltip("Контроллер, с которого считываем нажатие (например, Right Hand Controller).")]
    public XRController controllerToMonitor;

    [Tooltip("Кнопка на контроллере для подтверждения сброса (например, Primary/Secondary Button).")]
    // Пример: InputHelpers.Button.PrimaryButton или InputHelpers.Button.MenuButton
    public InputHelpers.Button finishButton = InputHelpers.Button.PrimaryButton;

    private bool modelIsInZone = false;
    private IXRSelectInteractable currentInteractable;
    private XRInteractionManager interactionManager;

    void Start()
    {
        // 1. Проверка и скрытие финального канваса
        if (readyCanvas != null)
        {
            readyCanvas.SetActive(false);
        }

        // 2. Получение ссылки на XR Interaction Manager
        if (targetModel != null)
        {
            XRGrabInteractable grabInteractable = targetModel.GetComponent<XRGrabInteractable>();
            if (grabInteractable != null)
            {
                interactionManager = grabInteractable.interactionManager;
            }
        }

        if (interactionManager == null)
        {
            Debug.LogError("Не удалось найти XR Interaction Manager.");
        }
    }

    void Update()
    {
        // Условие: Модель находится в зоне И контроллер существует И модель захвачена
        if (modelIsInZone && controllerToMonitor != null && currentInteractable != null && currentInteractable.isSelected)
        {
            // Проверка, нажата ли кнопка подтверждения (PrimaryButton/SecondaryButton и т.д.)
            InputHelpers.IsPressed(controllerToMonitor.inputDevice, finishButton, out bool isPressed);

            if (isPressed)
            {
                FinishDrawingTask();
            }
        }
    }

    // Событие: Модель вошла в зону триггера (чертеж)
    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject == targetModel)
        {
            currentInteractable = targetModel.GetComponent<IXRSelectInteractable>();
            // Условие "модель захвачена" будет проверяться в Update
            modelIsInZone = true;
            Debug.Log("Модель над чертежом. Нажмите кнопку для подтверждения.");
        }
    }

    // Событие: Модель покинула зону триггера
    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject == targetModel)
        {
            modelIsInZone = false;
            currentInteractable = null;
        }
    }

    private void FinishDrawingTask()
    {
        if (currentInteractable == null || interactionManager == null) return;

        // 1. Принудительно отпускаем модель из руки
        if (currentInteractable.isSelected)
        {
            // ИСПРАВЛЕНИЕ: Используем свойство selectingInteractor, доступное в 2.6.5
            IXRSelectInteractor selectingInteractor = currentInteractable.interactorsSelecting[0];

            if (selectingInteractor != null)
            {
                // Программно отпускаем объект (Select Exit)
                // Мы должны передать интерфейсные типы (IXRSelectInteractor и IXRSelectInteractable)
                interactionManager.SelectExit((IXRSelectInteractor)selectingInteractor, currentInteractable);
            }
        }

        // 2. Уничтожаем 3D-модель (она больше не нужна)
        Destroy(targetModel);

        // 3. Активируем новый Canvas
        if (readyCanvas != null)
        {
            readyCanvas.SetActive(true);
        }

        Debug.Log("Чертеж готов к печати! Задание завершено.");

        // Отключаем этот скрипт
        enabled = false;
    }
}