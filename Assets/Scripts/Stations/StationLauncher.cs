using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class StationLauncher : MonoBehaviour
{
    [Header(" --- References --- ")]
    public StationManager stationManager;
    public PlayerInput playerInput;
    public Camera activeCamera;

    [Header(" --- UI --- ")]
    public GameObject enterStationPanel;

    [Header(" --- Prefabs --- ")]
    public GameObject fpsPlayerPrefab;

    private InputAction _enterAction;
    private InputAction _dropAction;
    private GameObject _player;
    private bool _isPlayerInZone;
    private bool _hasJustSpawn;

    public void Awake()
    {
        stationManager.enabled = false;
        if (playerInput == null)
        {
            playerInput = FindAnyObjectByType<PlayerInput>();
        }
        _enterAction = playerInput.actions["Interact"];
        _dropAction = playerInput.actions["Carre"];
    }

    public void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !_hasJustSpawn)
        {
            _player = other.gameObject;
            enterStationPanel.SetActive(true);
            _isPlayerInZone = true;
            _enterAction.Enable();
            _enterAction.performed += OnInteractPerformed;
            _dropAction.Enable();
            _dropAction.performed += OnDropPerformed;
        }
    }

    public void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            enterStationPanel.SetActive(false);
            _isPlayerInZone = false;
            _enterAction.performed -= OnInteractPerformed;
            _dropAction.performed -= OnDropPerformed;
            _hasJustSpawn = false;
        }
    }

    private void OnInteractPerformed(InputAction.CallbackContext context)
    {
        if (_isPlayerInZone)
        {
            stationManager.enabled = true;
            enterStationPanel.SetActive(false);
            var pos = _player.GetComponentInChildren<Camera>().gameObject.transform;
            Destroy(_player);
            _enterAction.performed -= OnInteractPerformed;
            _dropAction.performed -= OnDropPerformed;
            stationManager.StartSelection(pos);
        }
    }
    
    private void OnDropPerformed(InputAction.CallbackContext context)
    {
        if (_isPlayerInZone)
        {
            var pos = _player.GetComponentInChildren<Camera>().gameObject.transform;
            Destroy(_player);
            _enterAction.performed -= OnInteractPerformed;
            _dropAction.performed -= OnDropPerformed;
            Instantiate(fpsPlayerPrefab, pos.position, Quaternion.identity);
        }
    }
    
    public void LaunchStation()
    {
        stationManager.enabled = true;
        enterStationPanel.SetActive(false);
        activeCamera = Camera.main;
        var pos = activeCamera.transform;
        Destroy(activeCamera.gameObject);
        _enterAction.performed -= OnInteractPerformed;
        stationManager.StartSelection(pos, StartTutorial);
        _hasJustSpawn = true;
    }

    public void StartTutorial()
    {
        // ScoreManager.Instance.StartRun();
        SceneManager.LoadScene(1);
    }

    // Lance la sélection de vélo depuis un point de caméra donné (ex : intro du tuto)
    public void LaunchStationFrom(Transform cameraStart, Action onComplete)
    {
        stationManager.enabled = true;
        enterStationPanel.SetActive(false);
        stationManager.StartSelection(cameraStart, onComplete);
        _hasJustSpawn = true;
    }

    // Évite d'afficher le panel "entrer dans la station" quand le joueur apparaît directement dedans
    public void IgnoreNextPlayerEnter()
    {
        _hasJustSpawn = true;
    }
}
