using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public enum Act
{
    Drawing,
    Modeling,
    MakingPlan
}
public class Scenary : MonoBehaviour
{
    [SerializeField] Canvas[] apps;
    [SerializeField] GameObject miniAppPrefab;
    public Button button3D;
    public static int CountStage = 0;
    
    private void Start()
    {
        SwitchApp();
    }
    public void SwitchApp()
    {
        foreach (var app in apps) {
            app.GetComponent<Canvas>().enabled = false;
        }
        apps[CountStage].GetComponent<Canvas>().enabled = true;
        
        CountStage++;
    }
    public void SpawnMiniApp()
    {
        miniAppPrefab.SetActive(true);
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
