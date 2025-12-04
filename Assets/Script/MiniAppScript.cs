using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class MiniAppScript : MonoBehaviour
{
    [SerializeField] private Button applyTransform;
    [SerializeField] private TMP_InputField inputTransform;
    [SerializeField] private Scenary scenary;

    private void Start()
    {
        applyTransform.onClick.AddListener(Switch);
    }
    public void Switch()
    {
        if (inputTransform.text == "15")
        {
            scenary.SwitchApp();
            Destroy(gameObject);
        }
        else
        {
            var placeholderText = inputTransform.placeholder as TextMeshProUGUI;
            if (placeholderText != null)
            {
                placeholderText.text = "¬веден некорректный размер";
            }

            inputTransform.text = ""; // очистить поле
        }
    }
}
