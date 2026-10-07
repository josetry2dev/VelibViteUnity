using UnityEngine;
using UnityEngine.InputSystem;

public class BikeVisualController : MonoBehaviour
{
    [Header("References")]
    public PlayerInput playerInput;
    public Rigidbody bikeRigidbody;
    private InputAction _moveAction;

    [Header("Tilt Settings")]
    public float maxTiltAngle = 25f;
    public float tiltSpeed = 5f;
    public float returnSpeed = 8f;

    private Quaternion initialRotation;
    private float currentTiltZ = 0f;

    private void Awake()
    {
        playerInput = FindFirstObjectByType<PlayerInput>();
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
        float currentSmoothing = (dir.x != 0) ? tiltSpeed : returnSpeed;
        currentTiltZ = Mathf.Lerp(currentTiltZ, targetTiltX, currentSmoothing * Time.deltaTime);

        transform.localRotation = initialRotation * Quaternion.Euler(0f, 0f,-currentTiltZ);
    }
}