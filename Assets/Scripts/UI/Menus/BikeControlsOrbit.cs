using UnityEngine;
using UnityEngine.InputSystem;

public class BikeControlsOrbit : MonoBehaviour
{
    [Header(" --- References --- ")]
    [SerializeField] private Transform camOrigin;       // Le pivot principal au centre du vélo
    [SerializeField] private Transform yRotationRig;    // Le sous-pivot pour la rotation verticale
    [SerializeField] private Transform mainCamera;      // La caméra elle-même
    [SerializeField] private PlayerInput playerInput;   // Ton PlayerInput

    [Header(" --- UI Billboarding --- ")]
    // Liste de tous tes éléments d'UI (tu peux les glisser-déposer dans l'inspecteur)
    [SerializeField] private GameObject[] controlsUI; 

    [Header(" --- Rotation Settings --- ")]
    [SerializeField] private float rotationSpeedH = 200f;
    [SerializeField] private float rotationSpeedV = 150f;
    [SerializeField] private float minVerticalAngle = -10f; // Pour pas regarder sous le sol
    [SerializeField] private float maxVerticalAngle = 60f;  // Pour pas regarder le ciel à la verticale

    [Header(" --- Zoom Settings --- ")]
    [SerializeField] private float zoomSpeed = 10f;
    [SerializeField] private float minZoomDistance = 2f; // Plus près c'est moche
    [SerializeField] private float maxZoomDistance = 10f; // Plus loin on voit plus rien
    [SerializeField] private float smoothing = 10f; // Pour un zoom fluide

    // Input Actions
    private InputAction _lookAction;
    private InputAction _zoomAction;

    // Variables internes
    private float _currentX; // Rotation horizontale accumulée
    private float _currentY; // Rotation verticale accumulée
    private float _targetZ;     // Distance de zoom cible
    private Transform _cameraBoom; // Le transform intermédiaire

    private void Awake()
    {
        _cameraBoom = mainCamera.parent;
        
        // Récupération des actions. ATTENTION : Il faut créer l'action "Zoom" dans ton InputSystem asset.
        // Ici je pars du principe que le joystick droit est mappé sur une action nommée "Zoom" (Vector2).
        _lookAction = playerInput.actions["Move"]; // Left Stick
        _zoomAction = playerInput.actions["Look"]; // Right Stick (on utilisera .y)
    }

    private void Update()
    {
        HandleRotation();
        HandleZoom();
    }

    private void LateUpdate()
    {
        HandleBillboards();
    }

    private void HandleRotation()
    {
        Vector2 lookInput = _lookAction.ReadValue<Vector2>();

        if (lookInput.sqrMagnitude > 0.01f)
        {
            // Rotation Horizontale (autour de CamOrigin)
            // On tourne l'objet parent global
            camOrigin.Rotate(Vector3.up, lookInput.x * rotationSpeedH * Time.deltaTime, Space.World);

            // Rotation Verticale (autour de Y_Rotation_Rig)
            // On veut modifier l'angle localement sur X, mais en clampant la valeur
            _currentY -= lookInput.y * rotationSpeedV * Time.deltaTime;
            _currentY = Mathf.Clamp(_currentY, minVerticalAngle, maxVerticalAngle);

            // Applique la rotation verticale sur le rig intermédiaire
            yRotationRig.localEulerAngles = new Vector3(_currentY, 0f, 0f);
        }
    }

    private void HandleZoom()
    {
        Vector2 zoomInput = _zoomAction.ReadValue<Vector2>();
        float scrollY = zoomInput.y;
        
        if (Mathf.Abs(scrollY) < 0.01f)
        {
            return; 
        }

        _targetZ = _cameraBoom.localPosition.z;

        // Calcul du nouveau target
        _targetZ -= scrollY * zoomSpeed * Time.deltaTime;

        // Clamp la distance de zoom (toujours sur les valeurs négatives)
        _targetZ = Mathf.Clamp(_targetZ, -maxZoomDistance, -minZoomDistance);

        // Applique le zoom avec lerp
        Vector3 currentPos = _cameraBoom.localPosition;
        Vector3 targetPos = new Vector3(currentPos.x, currentPos.y, _targetZ);
    
        _cameraBoom.localPosition = Vector3.Lerp(currentPos, targetPos, Time.deltaTime * smoothing);
    }

    private void HandleBillboards()
    {
        // Reste identique : l'UI regarde la caméra
        Vector3 cameraForward = mainCamera.forward;

        foreach (GameObject uiControl in controlsUI)
        {
            if (uiControl && uiControl.activeSelf)
            {
                uiControl.transform.rotation = Quaternion.LookRotation(cameraForward);
            }
        }
    }
}