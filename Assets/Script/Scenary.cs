using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class Scenary : MonoBehaviour
{
    [SerializeField] Canvas[] _apps;
    [SerializeField] GameObject _miniAppPrefab;
    public Button button3D;
    public StickRotate stickRotateScript;
    public static int CountStage = 0;

    private void Start()
    {
        SwitchApp();
    }

    public void SwitchApp()
    {
        // Выключаем все Canvas'ы
        foreach (var app in _apps)
            app.enabled = false;

        // Сначала блокируем вращение (без Disable Input)
        if (stickRotateScript != null)
            stickRotateScript.DeactivateRotation();

        // Включаем текущий Canvas
        _apps[CountStage].enabled = true;

        // Ждём 1 кадр — дождаться переключения UI
        StartCoroutine(EnableRotationSafe());

        CountStage++;
    }

    private IEnumerator EnableRotationSafe()
    {
        yield return null; // подождать до конца кадра

        // Вращение включаем только во втором приложении
        int index = CountStage - 1;

        if (stickRotateScript != null)
        {
            if (index == 1) // Второй Canvas
            {
                stickRotateScript.ActivateRotation();
                Debug.Log("Scenary: Rotation ENABLED for app 2");
            }
            else
            {
                stickRotateScript.DeactivateRotation();
                Debug.Log("Scenary: Rotation DISABLED");
            }
        }
    }

    public void SpawnMiniApp()
    {
        _miniAppPrefab.SetActive(true);
    }

    private void Update()
    {
        if (PaintRectangle.isEnding)
        {
            button3D.onClick.AddListener(() => SpawnMiniApp());
            PaintRectangle.isEnding = false;
        }
    }
}
