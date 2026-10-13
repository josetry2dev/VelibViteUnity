using UnityEngine;

public class TrafficLightController : MonoBehaviour
{
    public enum LightState { Green, Yellow, Red }

    [Header("État Actuel")]
    public LightState currentState;

    [Header("Références Visuelles")]
    public GameObject[] greenLightsObject;
    public GameObject[] yellowLightsObject;
    public GameObject[] redLightsObject;

    // Cette fonction sera appelée par le Chef d'Orchestre
    public void SetLightState(LightState newState)
    {
        currentState = newState;
        UpdateVisuals();
    }

    void UpdateVisuals()
    {
        foreach (var greenLight in greenLightsObject) greenLight.SetActive(false);
        foreach (var yellowLight in yellowLightsObject) yellowLight.SetActive(false);
        foreach (var redLight in redLightsObject) redLight.SetActive(false);

        switch (currentState)
        {
            case LightState.Green:
                foreach (var greenLight in greenLightsObject) greenLight.SetActive(true);
                break;
            case LightState.Yellow:
                foreach (var yellowLight in yellowLightsObject) yellowLight.SetActive(true);
                break;
            case LightState.Red:
                foreach (var redLight in redLightsObject) redLight.SetActive(true);
                break;
        }
    }
}