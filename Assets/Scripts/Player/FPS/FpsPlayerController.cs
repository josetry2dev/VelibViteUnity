using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class FpsPlayerController : MonoBehaviour
{
    public enum Posture
    {
        Standing,
        Crouching,
        Prone
    }

    [Header(" --- References --- ")]
    [SerializeField] private Transform cameraPivot;

    [Header(" --- Movement Speeds --- ")]
    [SerializeField, Min(0f)] private float walkSpeed = 4.5f;
    [SerializeField, Min(0f)] private float sprintSpeed = 7.5f;
    [SerializeField, Min(0f)] private float crouchSpeed = 2.5f;
    [SerializeField, Min(0f)] private float proneSpeed = 1.2f;

    [Header(" --- Movement Response --- ")]
    [SerializeField, Min(0f)] private float groundAcceleration = 25f;
    [SerializeField, Min(0f)] private float airAcceleration = 10f;
    [SerializeField, Min(0f)] private float stoppingDeceleration = 30f;
    [SerializeField, Min(0f)] private float movementDeadzone = 0.15f;

    [Header(" --- Jump & Gravity --- ")]
    [SerializeField, Min(0f)] private float jumpHeight = 1.2f;
    [SerializeField] private float gravity = -25f;
    [SerializeField, Min(0f)] private float groundStickForce = 2f;

    [Header(" --- Posture Heights --- ")]
    [SerializeField, Min(0.5f)] private float standingHeight = 1.8f;
    [SerializeField, Min(0.5f)] private float crouchingHeight = 1.15f;
    [SerializeField, Min(0.3f)] private float proneHeight = 0.55f;

    [Header(" --- Posture Camera Heights --- ")]
    [SerializeField, Min(0f)] private float standingCameraHeight = 1.62f;
    [SerializeField, Min(0f)] private float crouchingCameraHeight = 0.95f;
    [SerializeField, Min(0f)] private float proneCameraHeight = 0.35f;

    [Header(" --- Posture Transition --- ")]
    [SerializeField, Min(0f)] private float postureTransitionSpeed = 10f;

    [Header(" --- Collision --- ")]
    [SerializeField, Min(0.01f)] private float capsuleRadius = 0.3f;

    [Header(" --- Input --- ")]
    [SerializeField, Min(0f)] private float longPressThreshold = 0.45f;

    public Posture CurrentPosture { get; private set; } = Posture.Standing;
    public bool IsSprinting => _isSprinting;
    public bool IsGrounded => _controller != null && _controller.isGrounded;

    private PlayerInput _playerInput;

    private InputAction _moveAction;
    private InputAction _jumpAction;
    private InputAction _sprintAction;
    private InputAction _postureAction;

    private CharacterController _controller;
    private Vector3 _horizontalVelocity;
    private float _verticalVelocity;
    private float _targetHeight;
    private float _targetCameraHeight;
    private bool _isSprinting;

    private bool _posturePressInProgress;
    private float _posturePressStartTime;
    private bool _longPosturePressHandled;

    private readonly Collider[] _overlapBuffer = new Collider[32];

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();

        // Find the shared PlayerInput owned by the GameManager.
        _playerInput = FindAnyObjectByType<PlayerInput>();

        if (_playerInput == null)
        {
            Debug.LogError(
                "FpsPlayerController: No central PlayerInput found.",
                this
            );

            enabled = false;
            return;
        }

        // Retrieve each action independently from the shared asset.
        _moveAction = _playerInput.actions.FindAction("Move", true);
        _jumpAction = _playerInput.actions.FindAction("Accelerate", true);
        _sprintAction = _playerInput.actions.FindAction("Sonnette", true);
        _postureAction = _playerInput.actions.FindAction("Jump", true);

        _controller.radius = capsuleRadius;
        _controller.height = standingHeight;
        _controller.center = Vector3.up * (standingHeight * 0.5f);

        _targetHeight = standingHeight;
        _targetCameraHeight = standingCameraHeight;
        CurrentPosture = Posture.Standing;

        if (cameraPivot != null)
        {
            Vector3 position = cameraPivot.localPosition;
            position.y = standingCameraHeight;
            cameraPivot.localPosition = position;
        }
    }

    private void Update()
    {
        if (_playerInput == null)
            return;

        HandleJumpInput();
        HandleSprintInput();
        HandlePostureInput();

        UpdateSprint();
        UpdatePostureTransition();
        UpdateMovement();
    }

    private void HandleJumpInput()
    {
        // Jump reads only the dedicated Jump action.
        if (_jumpAction.WasPressedThisFrame())
            TryJump();
    }

    private void HandleSprintInput()
    {
        // Sprint reads only the dedicated Sprint action.
        if (_sprintAction.WasPressedThisFrame())
            TryStartSprint();
    }

    private void HandlePostureInput()
    {
        // Posture reads only the dedicated Posture action.
        if (_postureAction.WasPressedThisFrame())
        {
            _posturePressInProgress = true;
            _posturePressStartTime = Time.time;
            _longPosturePressHandled = false;
        }

        if (!_posturePressInProgress)
            return;

        if (!_longPosturePressHandled &&
            _postureAction.IsPressed() &&
            Time.time - _posturePressStartTime >= longPressThreshold)
        {
            SetPosture(Posture.Prone);
            _longPosturePressHandled = true;
        }

        if (_postureAction.WasReleasedThisFrame())
        {
            if (!_longPosturePressHandled)
                TogglePosture();

            _posturePressInProgress = false;
        }
    }

    private void UpdateSprint()
    {
        Vector2 input = _moveAction.ReadValue<Vector2>();

        // Returning the stick to its center cancels sprint.
        if (input.magnitude < movementDeadzone)
            _isSprinting = false;
    }

    private void TryStartSprint()
    {
        if (_moveAction.ReadValue<Vector2>().magnitude >= movementDeadzone &&
            CurrentPosture == Posture.Standing)
        {
            _isSprinting = true;
        }
    }

    private void UpdateMovement()
    {
        Vector2 input = _moveAction.ReadValue<Vector2>();
        float inputMagnitude = Mathf.Clamp01(input.magnitude);

        Vector3 desiredDirection =
            transform.right * input.x +
            transform.forward * input.y;

        desiredDirection = Vector3.ClampMagnitude(desiredDirection, 1f);

        float speed = GetCurrentSpeed();
        Vector3 targetVelocity = desiredDirection * speed * inputMagnitude;

        bool hasMovementInput = inputMagnitude >= movementDeadzone;
        float acceleration;

        if (!_controller.isGrounded)
            acceleration = airAcceleration;
        else if (!hasMovementInput)
            acceleration = stoppingDeceleration;
        else
            acceleration = groundAcceleration;

        _horizontalVelocity = Vector3.MoveTowards(
            _horizontalVelocity,
            targetVelocity,
            acceleration * Time.deltaTime
        );

        if (_controller.isGrounded && _verticalVelocity < 0f)
            _verticalVelocity = -groundStickForce;

        _verticalVelocity += gravity * Time.deltaTime;

        Vector3 finalVelocity =
            _horizontalVelocity + Vector3.up * _verticalVelocity;

        _controller.Move(finalVelocity * Time.deltaTime);
    }

    private float GetCurrentSpeed()
    {
        switch (CurrentPosture)
        {
            case Posture.Crouching:
                return crouchSpeed;

            case Posture.Prone:
                return proneSpeed;

            default:
                return _isSprinting ? sprintSpeed : walkSpeed;
        }
    }

    private void TryJump()
    {
        if (CurrentPosture != Posture.Standing)
            return;

        if (!_controller.isGrounded)
            return;

        _verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
    }

    private void TogglePosture()
    {
        switch (CurrentPosture)
        {
            case Posture.Standing:
                SetPosture(Posture.Crouching);
                break;

            case Posture.Crouching:
                SetPosture(Posture.Standing);
                break;

            case Posture.Prone:
                SetPosture(Posture.Crouching);
                break;
        }
    }

    private void SetPosture(Posture posture)
    {
        float newHeight = GetHeightForPosture(posture);

        // Prevent standing up through a ceiling.
        if (newHeight > _controller.height && !CanFitCapsule(newHeight))
            return;

        CurrentPosture = posture;
        _targetHeight = newHeight;
        _targetCameraHeight = GetCameraHeightForPosture(posture);

        if (posture != Posture.Standing)
            _isSprinting = false;
    }

    private void UpdatePostureTransition()
    {
        float nextHeight = Mathf.MoveTowards(
            _controller.height,
            _targetHeight,
            postureTransitionSpeed * Time.deltaTime
        );

        _controller.height = nextHeight;
        _controller.center = Vector3.up * (nextHeight * 0.5f);

        if (cameraPivot != null)
        {
            Vector3 position = cameraPivot.localPosition;

            position.y = Mathf.MoveTowards(
                position.y,
                _targetCameraHeight,
                postureTransitionSpeed * Time.deltaTime
            );

            cameraPivot.localPosition = position;
        }
    }

    private float GetHeightForPosture(Posture posture)
    {
        switch (posture)
        {
            case Posture.Crouching:
                return crouchingHeight;

            case Posture.Prone:
                return proneHeight;

            default:
                return standingHeight;
        }
    }

    private float GetCameraHeightForPosture(Posture posture)
    {
        switch (posture)
        {
            case Posture.Crouching:
                return crouchingCameraHeight;

            case Posture.Prone:
                return proneCameraHeight;

            default:
                return standingCameraHeight;
        }
    }

    private bool CanFitCapsule(float height)
    {
        float radius = Mathf.Min(capsuleRadius, height * 0.5f);

        Vector3 bottom = transform.position + Vector3.up * radius;
        Vector3 top = transform.position + Vector3.up * (height - radius);

        int count = Physics.OverlapCapsuleNonAlloc(
            bottom,
            top,
            radius * 0.95f,
            _overlapBuffer,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore
        );

        for (int i = 0; i < count; i++)
        {
            Collider hit = _overlapBuffer[i];

            if (hit == null || hit == _controller)
                continue;

            if (hit.transform.IsChildOf(transform))
                continue;

            return false;
        }

        return true;
    }
}
