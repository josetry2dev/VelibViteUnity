using UnityEngine;
using UnityEngine.InputSystem;

public class BikeVisualController : MonoBehaviour
{
    [Header(" --- References --- ")]
    public PlayerInput playerInput;
    public Rigidbody bikeRigidbody;

    [Header(" --- Tilt Settings --- ")]
    public float maxTiltAngle = 25f;
    public float tiltSpeed = 5f;
    public float returnSpeed = 8f;

    private InputAction _moveAction;
    private Quaternion initialRotation;
    private float currentTiltZ;

    private void Awake()
    {
        playerInput = FindAnyObjectByType<PlayerInput>();
        _moveAction = playerInput.actions["Move"];
    }

    private void Start()
    {
        initialRotation = transform.localRotation;
        if (bikeRigidbody == null) bikeRigidbody = GetComponentInParent<Rigidbody>();
    }

    private void Update()
    {
        Vector2 dir = _moveAction.ReadValue<Vector2>();
        float targetTiltX = dir.x * maxTiltAngle;
        float currentSmoothing = Mathf.Approximately(dir.x, 0f) ? returnSpeed : tiltSpeed;
        currentTiltZ = Mathf.Lerp(currentTiltZ, targetTiltX, currentSmoothing * Time.deltaTime);

        transform.localRotation = initialRotation * Quaternion.Euler(0f, 0f, -currentTiltZ);
    }
}
