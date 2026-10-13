using UnityEngine;
using UnityEngine.InputSystem;

public class CameraController : MonoBehaviour
{
    [Header(" --- Gamepad Settings --- ")]
    public float gamepadResponsiveness = 10f;

    [Header(" --- Look Clamps --- ")]
    public float topClamp = -60f;
    public float bottomClamp = 60f;
    public float leftClamp = -90f;
    public float rightClamp = 90f;

    [Header(" --- Tilt Settings --- ")]
    public float maxTiltAngle = 5f;
    public float tiltSpeed = 5f;
    public float returnSpeed = 8f;

    // References
    private Transform _cameraTransform;
    private PlayerInput _playerInput;
    private InputAction _moveAction;
    private InputAction _lookAction;
    private Vector2 _lookInput;
    private Vector2 _moveInput;

    // Drunk
    private const float MaxDrunkXOffset = 0.03f;
    private const float MaxDrunkYOffset = 0.02f;
    private float _drunkPercentage;
    private float _drunkTransitionSpeed;
    private float _currentDrunkXOffset;
    private float _currentDrunkYOffset;

    // Rotation
    private float _xRotation;
    private float _yRotation;
    private float _currentTiltZ;
    private float _defaultXOffset;
    private float _defaultYOffset;

    private void Awake()
    {
        _playerInput = FindAnyObjectByType<PlayerInput>();
        _cameraTransform = FindAnyObjectByType<Camera>().transform;
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
        _currentDrunkXOffset = Mathf.Lerp(_currentDrunkXOffset, _drunkPercentage * MaxDrunkXOffset, Time.deltaTime * _drunkTransitionSpeed);
        _currentDrunkYOffset = Mathf.Lerp(_currentDrunkYOffset, _drunkPercentage * MaxDrunkYOffset, Time.deltaTime * _drunkTransitionSpeed);
        _defaultXOffset += Mathf.Sin(Time.time * 0.7f) * _currentDrunkYOffset;
        _defaultYOffset += Mathf.Sin(Time.time) * _currentDrunkXOffset;
        
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
        float targetY = Mathf.Lerp(leftClamp, rightClamp, (_lookInput.x + 1f) / 2f);
        float tValueVertical = (_lookInput.y + 1f) / 2f; // Transforme -1..1 en 0..1
        float targetX = Mathf.Lerp(bottomClamp, topClamp, tValueVertical);
        targetY += _defaultYOffset;
        targetX += _defaultXOffset;
        _xRotation = Mathf.Lerp(_xRotation, targetX, gamepadResponsiveness * Time.deltaTime);
        _yRotation = Mathf.Lerp(_yRotation, targetY, gamepadResponsiveness * Time.deltaTime);
    }

    private void HandleTilt()
    {
        float targetTiltZ = _moveInput.x * maxTiltAngle;
        float speed = Mathf.Approximately(_moveInput.x, 0f) ? returnSpeed : tiltSpeed;
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