using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum Act
{
    Drawing,
    Modeling,
    MakingPlan
}
public class Scenary : MonoBehaviour
{
    [SerializeField] GameObject[] apps;
    public static int CountStage = 0;
    private void Start()
    {
        SwitchApp();
    }
    public void SwitchApp()
    {
        foreach (var app in apps) {
            app.SetActive(false);
        }
        apps[CountStage].SetActive(true);
        CountStage++;
    }
}
