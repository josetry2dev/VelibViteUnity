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

    [Header(" --- Auto Animation --- ")]
    [SerializeField] private bool autoAnimationEnabled = true;
    [Tooltip("Temps sans toucher aux joysticks avant que l'animation reprenne (secondes)")]
    [SerializeField] private float resumeDelay = 3f;
    [Tooltip("Durée du retour en douceur sur le parcours d'animation (secondes)")]
    [SerializeField] private float returnDuration = 2f;
    [SerializeField] private float inputDeadzone = 0.1f;

    [Header(" --- Auto Traveling --- ")]
    [Tooltip("Vitesse de rotation latérale constante (degrés/seconde)")]
    [SerializeField] private float autoRotationSpeed = 10f;
    [Tooltip("Amplitude de la montée/descente autour de l'angle de départ (degrés)")]
    [SerializeField] private float verticalAmplitude = 8f;
    [Tooltip("Durée d'un cycle complet de montée/descente (secondes)")]
    [SerializeField] private float verticalPeriod = 12f;

    [Header(" --- Auto Zoom --- ")]
    [Tooltip("Amplitude du zoom/dézoom autour du zoom de départ")]
    [SerializeField] private float zoomAmplitude = 0.4f;
    [Tooltip("Durée d'un cycle complet de zoom/dézoom (secondes)")]
    [SerializeField] private float zoomPeriod = 9f;

    // Input Actions
    private InputAction _lookAction;
    private InputAction _zoomAction;

    // Variables internes
    private float _currentY; // Rotation verticale accumulée
    private float _targetZ;     // Distance de zoom cible
    private Transform _cameraBoom; // Le transform intermédiaire

    // Auto animation
    private float _basePitch;      // Centre du parcours vertical (angle de départ de la scène)
    private float _baseZoom;       // Centre du parcours de zoom (zoom de départ de la scène)
    private float _autoTime;       // Avancement dans le parcours
    private float _idleTimer;      // Temps écoulé depuis le dernier input
    private bool _isAutoPlaying;
    private float _returnProgress; // 0 -> 1 pendant le retour sur le parcours
    private float _returnStartPitch;
    private float _returnStartZoom;

    private void Awake()
    {
        _cameraBoom = mainCamera.parent;

        // Récupération des actions. ATTENTION : Il faut créer l'action "Zoom" dans ton InputSystem asset.
        // Ici je pars du principe que le joystick droit est mappé sur une action nommée "Zoom" (Vector2).
        _lookAction = playerInput.actions["Move"]; // Left Stick
        _zoomAction = playerInput.actions["Look"]; // Right Stick (on utilisera .y)

        // Le parcours d'animation est centré sur le cadrage de départ de la scène
        _currentY = NormalizeAngle(yRotationRig.localEulerAngles.x);
        _basePitch = _currentY;
        _baseZoom = _cameraBoom.localPosition.z;

        // L'animation démarre direct au lancement
        _idleTimer = resumeDelay;
    }

    private void Update()
    {
        bool playerIsControlling = IsStickActive(_lookAction) || IsStickActive(_zoomAction);

        if (playerIsControlling || !autoAnimationEnabled)
        {
            _idleTimer = 0f;
            _isAutoPlaying = false;
            HandleRotation();
            HandleZoom();
            return;
        }

        _idleTimer += Time.deltaTime;
        if (_idleTimer < resumeDelay) return;

        if (!_isAutoPlaying) StartAutoAnimation();
        HandleAutoAnimation();
    }

    private void LateUpdate()
    {
        HandleBillboards();
    }

    private bool IsStickActive(InputAction action)
    {
        return action.ReadValue<Vector2>().sqrMagnitude > inputDeadzone * inputDeadzone;
    }

    #region Manual Control
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
    #endregion

    #region Auto Animation
    private void StartAutoAnimation()
    {
        // On part de là où le joueur a laissé la caméra, puis on rejoint le parcours en douceur
        _isAutoPlaying = true;
        _returnProgress = 0f;
        _returnStartPitch = _currentY;
        _returnStartZoom = _cameraBoom.localPosition.z;
    }

    private void HandleAutoAnimation()
    {
        float dt = Time.deltaTime;
        _autoTime += dt;

        // Retour progressif sur le parcours (SmoothStep pour un départ et une arrivée en douceur)
        _returnProgress = returnDuration > 0f ? Mathf.Min(_returnProgress + dt / returnDuration, 1f) : 1f;
        float blend = Mathf.SmoothStep(0f, 1f, _returnProgress);

        // Traveling latéral à vitesse constante (accélère en douceur pendant le retour)
        camOrigin.Rotate(Vector3.up, autoRotationSpeed * blend * dt, Space.World);

        // Montée / descente régulière
        float pathPitch = _basePitch + verticalAmplitude * Mathf.Sin(_autoTime * 2f * Mathf.PI / verticalPeriod);
        pathPitch = Mathf.Clamp(pathPitch, minVerticalAngle, maxVerticalAngle);
        _currentY = Mathf.Lerp(_returnStartPitch, pathPitch, blend);
        yRotationRig.localEulerAngles = new Vector3(_currentY, 0f, 0f);

        // Zoom / dézoom périodique
        float pathZoom = _baseZoom + zoomAmplitude * Mathf.Sin(_autoTime * 2f * Mathf.PI / zoomPeriod);
        pathZoom = Mathf.Clamp(pathZoom, -maxZoomDistance, -minZoomDistance);
        Vector3 boomPos = _cameraBoom.localPosition;
        boomPos.z = Mathf.Lerp(_returnStartZoom, pathZoom, blend);
        _cameraBoom.localPosition = boomPos;
    }
    #endregion

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

    private static float NormalizeAngle(float angle)
    {
        if (angle > 180f) return angle - 360f;
        return angle;
    }
}
