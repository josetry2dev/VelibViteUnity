using UnityEngine;
using System.Collections;

public class TutoIntroSequence : MonoBehaviour
{
    [Header(" --- References --- ")]
    public GameObject player;
    public StationLauncher startStation;
    public StationManager startStationManager;
    public SmsManager smsManager;

    [Header(" --- Intro Settings --- ")]
    public SmsDialogue introDialogue;
    [Tooltip("Position de la caméra pendant les SMS, et point de départ de la sélection de vélo")]
    public Transform introCameraPoint;

    [Header(" --- Gameplay Activation --- ")]
    [Tooltip("Scripts désactivés pendant l'intro et activés quand on débloque le vélo (ex : TutoController)")]
    public Behaviour[] enableAfterIntro;

    private GameObject _introCamera;

    void Awake()
    {
        // Le joueur n'existe pas tant qu'on n'a pas débloqué de vélo
        player.SetActive(false);
        foreach (var behaviour in enableAfterIntro) behaviour.enabled = false;
    }

    void Start()
    {
        // Restart ou LevelSelect (contre la montre) : on saute direct l'intro
        if (LevelManager.SkipIntro)
        {
            startStation.IgnoreNextPlayerEnter();
            StartLevel();
            return;
        }

        _introCamera = Instantiate(startStationManager.cameraPrefab, introCameraPoint.position, introCameraPoint.rotation);

        if (introDialogue != null) smsManager.StartDialogue(introDialogue, OnDialogueEnd);
        else OnDialogueEnd();
    }

    private void OnDialogueEnd()
    {
        // La sélection crée sa propre caméra au même endroit
        Destroy(_introCamera);
        startStation.LaunchStationFrom(introCameraPoint, OnBikeUnlocked);
    }

    private void OnBikeUnlocked()
    {
        // On attend une frame que la caméra de la station soit détruite,
        // sinon le CameraController du joueur risque de s'accrocher à elle
        StartCoroutine(StartLevelNextFrame());
    }

    private IEnumerator StartLevelNextFrame()
    {
        yield return null;
        StartLevel();
    }

    private void StartLevel()
    {
        player.SetActive(true);
        foreach (var behaviour in enableAfterIntro) behaviour.enabled = true;
        LevelManager.Instance.StartTimer();
    }
}
