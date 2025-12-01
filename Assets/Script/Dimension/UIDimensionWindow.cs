// UI_DimensionWindow.cs
using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIDimensionWindow : MonoBehaviour
{
    [SerializeField] TMP_InputField inputField;
    [SerializeField] Button okButton;
    [SerializeField] Button cancelButton;

    private Action<float> onConfirm;
    private Action onCancel;

    void Reset()
    {
        inputField = GetComponentInChildren<TMP_InputField>();
        okButton = GetComponentInChildren<Button>();
    }

    public void Show(Vector2 screenPosition, Action<float> onConfirm, Action onCancel = null, string placeholder = "")
    {
        this.onConfirm = onConfirm;
        this.onCancel = onCancel;

        transform.SetAsLastSibling();
        transform.position = screenPosition;

        if (inputField != null)
        {
            inputField.text = "";
            if (!string.IsNullOrEmpty(placeholder) && inputField.placeholder != null)
                inputField.placeholder.GetComponent<TMP_Text>().text = placeholder;
            inputField.ActivateInputField();
        }

        if (okButton != null)
        {
            okButton.onClick.RemoveAllListeners();
            okButton.onClick.AddListener(OnOkClicked);
        }

        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveAllListeners();
            cancelButton.onClick.AddListener(OnCancelClicked);
        }

        // ensure window blocks raycasts
        var cg = GetComponent<CanvasGroup>();
        if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = true;
        cg.interactable = true;
    }

    void OnOkClicked()
    {
        if (inputField == null) return;
        if (float.TryParse(inputField.text, out float value))
        {
            onConfirm?.Invoke(value);
            Destroy(gameObject);
        }
        else
        {
            inputField.ActivateInputField();
        }
    }

    void OnCancelClicked()
    {
        onCancel?.Invoke();
        Destroy(gameObject);
    }
}
