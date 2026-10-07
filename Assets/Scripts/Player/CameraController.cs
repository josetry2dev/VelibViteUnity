using UnityEngine;
using UnityEngine.InputSystem;

public class CameraController : MonoBehaviour
{
    [Header("Références")]
    private Transform _cameraTransform;
    private PlayerInput _playerInput;
    
    [Header("Gamepad")]
    public float gamepadResponsiveness = 10f; 

    [Header("Limites & Angles Max")]
    public float topClamp = -60f;
    public float bottomClamp = 60f;
    public float leftClamp = -90f;
    public float rightClamp = 90f;

    [Header("Effet Tilt (Inclinaison au mouvement)")]
    public float maxTiltAngle = 5f;
    public float tiltSpeed = 5f;
    public float returnSpeed = 8f;
    
    [Header("Drunk settings")]
    private float _drunkPercentage;
    private float _drunkTransitionSpeed;
    private float _maxDrunkXOffset = 0.03f;
    private float _maxDrunkYOffset = 0.02f;
    private float _currentDrunkXOffset;
    private float _currentDrunkYOffset;
    
    private Vector2 _lookInput;
    private Vector2 _moveInput;
    private InputAction _moveAction;
    private InputAction _lookAction;
    
    private float _xRotation = 0f;
    private float _yRotation = 0f;
    private float _currentTiltZ = 0f;
    private bool _isGamepad = true;
    private float _defaultXOffset = 0f;
    private float _defaultYOffset = 0f;

    private void Awake()
    {
        _playerInput = FindFirstObjectByType<PlayerInput>();
        _cameraTransform = FindFirstObjectByType<Camera>().transform;
        _moveAction = _playerInput.actions["Move"];
        _lookAction = _playerInput.actions["Look"];
    }
    void Start()
    {
        if (_playerInput == null) _playerInput = GetComponent<PlayerInput>();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        
        Vector3 currentAngles = _cameraTransform.localEulerAngles;
        _defaultXOffset = NormalizeAngle(currentAngles.x);
        _defaultYOffset = NormalizeAngle(currentAngles.y);
        _xRotation = _defaultXOffset;
        _yRotation = _defaultYOffset;
    }

    void Update()
    {
        _currentDrunkXOffset = Mathf.Lerp(_currentDrunkXOffset, _drunkPercentage * _maxDrunkXOffset, Time.deltaTime * _drunkTransitionSpeed);
        _currentDrunkYOffset = Mathf.Lerp(_currentDrunkYOffset, _drunkPercentage * _maxDrunkYOffset, Time.deltaTime * _drunkTransitionSpeed);
        _defaultXOffset += (Mathf.Sin(Time.time*0.7f) * _currentDrunkYOffset);
        _defaultYOffset += (Mathf.Sin(Time.time) * _currentDrunkXOffset);
        
        _lookInput = _lookAction.ReadValue<Vector2>();
        _moveInput = _moveAction.ReadValue<Vector2>();
        HandleLookRotation();
        HandleTilt();
        ApplyRotation();
    }
    
    private float NormalizeAngle(float angle)
    {
        if (angle > 180f) return angle - 360f;
        return angle;
    }

    private void HandleLookRotation()
    {
        if (_isGamepad)
        {
            float targetY = Mathf.Lerp(leftClamp, rightClamp, (_lookInput.x + 1f) / 2f);
            float tValueVertical = (_lookInput.y + 1f) / 2f; // Transforme -1..1 en 0..1
            float targetX = Mathf.Lerp(bottomClamp, topClamp, tValueVertical);
            targetY += _defaultYOffset;
            targetX += _defaultXOffset;
            _xRotation = Mathf.Lerp(_xRotation, targetX, gamepadResponsiveness * Time.deltaTime);
            _yRotation = Mathf.Lerp(_yRotation, targetY, gamepadResponsiveness * Time.deltaTime);
        }
    }

    private void HandleTilt()
    {
        float targetTiltZ = _moveInput.x * maxTiltAngle;
        float speed = (_moveInput.x != 0) ? tiltSpeed : returnSpeed;
        _currentTiltZ = Mathf.Lerp(_currentTiltZ, targetTiltZ, speed * Time.deltaTime);
    }

    private void ApplyRotation()
    {
        if (_cameraTransform != null)
        {
            _cameraTransform.localRotation = Quaternion.Euler(_xRotation, _yRotation, -_currentTiltZ);
        }
    }

    public void ApplyDrunk(float drunkPercentage, float drunkTransitionSpeed)
    {
        _drunkPercentage = drunkPercentage;
        _drunkTransitionSpeed = drunkTransitionSpeed;
    }
}