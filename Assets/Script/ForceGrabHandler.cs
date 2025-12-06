using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.UI;

public class ForceGrabHandler : MonoBehaviour
{
    [Header("Настройки VR-захвата")]
    [Tooltip("Контроллер, который должен взять объект.")]
    public XRBaseInteractor interactorToUse;

    [Tooltip("Модель, которую нужно взять в руку (должна иметь XR Grab Interactable).")]
    public XRGrabInteractable interactableToGrab;

    void Start()
    {
        // ... (Проверка ссылок) ...
        if (interactorToUse == null || interactableToGrab == null)
        {
            Debug.LogError("Не установлены ссылки на Interactor или Interactable.");
            return;
        }

        Button button = GetComponent<Button>();
        if (button != null)
        {
            // Привязываем метод к нажатию кнопки
            button.onClick.AddListener(AttemptForceGrab);
        }
        else
        {
            Debug.LogError("Скрипт ForceGrabHandler должен быть на объекте с компонентом Button.");
        }
    }

    public void AttemptForceGrab()
    {
        if (interactableToGrab == null || interactorToUse == null) return;

        // Если объект уже захвачен, ничего не делаем
        if (interactableToGrab.isSelected) return;

        // Проверяем, может ли интерактор схватить объект
        if (!interactorToUse.CanSelect(interactableToGrab))
        {
            Debug.LogWarning("Интерактор не может захватить объект.");
            return;
        }

        // Получаем менеджер взаимодействий
        XRInteractionManager interactionManager = interactableToGrab.interactionManager;

        if (interactionManager != null)
        {
            // ИСПРАВЛЕННАЯ СТРОКА: Прямой вызов SelectEnter
            interactionManager.SelectEnter((IXRSelectInteractor)interactorToUse, (IXRSelectInteractable)interactableToGrab);

            Debug.Log($"Объект {interactableToGrab.name} принудительно захвачен контроллером {interactorToUse.name}.");
        }
    }
}