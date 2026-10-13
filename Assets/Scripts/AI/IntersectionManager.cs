using UnityEngine;

public class IntersectionManager : MonoBehaviour
{
    [Header(" --- Traffic Lights --- ")]
    public TrafficLightController[] roadALights; // Les feux de la route 1 (ex: Nord/Sud)
    public TrafficLightController[] roadBLights; // Les feux de la route 2 (ex: Est/Ouest)

    [Header(" --- Timers --- ")]
    public float greenDuration = 5f;
    public float yellowDuration = 2f;
    public float allRedSafetyDuration = 1f; // Petit temps où tout est rouge pour vider le carrefour

    private float timer;
    private int stepIndex;

    void Start()
    {
        // On lance le cycle
        NextStep();
    }

    void Update()
    {
        timer -= Time.deltaTime;
        if (timer <= 0)
        {
            NextStep();
        }
    }

    void NextStep()
    {
        stepIndex++;
        if (stepIndex > 4) stepIndex = 1; // On boucle (4 étapes principales ici)

        switch (stepIndex)
        {
            case 1: // Route A passe au VERT, B reste ROUGE
                SetLights(roadALights, TrafficLightController.LightState.Green);
                SetLights(roadBLights, TrafficLightController.LightState.Red);
                timer = greenDuration;
                break;

            case 2: // Route A passe à l'ORANGE, B reste ROUGE
                SetLights(roadALights, TrafficLightController.LightState.Yellow);
                SetLights(roadBLights, TrafficLightController.LightState.Red);
                timer = yellowDuration;
                break;

            // (Optionnel: tu pourrais ajouter une étape "Tout Rouge" ici pour la sécu)

            case 3: // Route A passe au ROUGE, B passe au VERT
                SetLights(roadALights, TrafficLightController.LightState.Red);
                SetLights(roadBLights, TrafficLightController.LightState.Green);
                timer = greenDuration;
                break;

            case 4: // Route A reste ROUGE, B passe à l'ORANGE
                SetLights(roadALights, TrafficLightController.LightState.Red);
                SetLights(roadBLights, TrafficLightController.LightState.Yellow);
                timer = yellowDuration;
                break;
        }
    }

    // Fonction utilitaire pour changer tout un groupe de feux d'un coup
    void SetLights(TrafficLightController[] lights, TrafficLightController.LightState state)
    {
        foreach (var trafficLight in lights)
        {
            trafficLight.SetLightState(state);
        }
    }
}