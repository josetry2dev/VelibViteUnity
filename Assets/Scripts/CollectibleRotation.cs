using UnityEngine;

public class CollectibleRotation : MonoBehaviour
{
    [Header("Rotation Settings")]
    [Tooltip("Rotation speed in degrees per second")]
    public float rotationSpeed = 90f; // Rotation speed
    public float maxBoostDivider = 10f;
    private float _respawnDelay = 15f;
    private bool _isConsumed;
    private MeshRenderer _meshRenderer;

    void Awake()
    {
        Quaternion initialRotation = transform.rotation;
        _meshRenderer = GetComponent<MeshRenderer>();
        float randomRotation = Random.Range(0f, 360f);
        transform.rotation = Quaternion.Euler(initialRotation.eulerAngles.x, randomRotation, initialRotation.eulerAngles.z);
    }
    void Update()
    {
        if (_isConsumed) _respawnDelay -= Time.deltaTime;

        if (_respawnDelay <= 0f)
        {
            _isConsumed =  false;
            _respawnDelay = 10f;
            _meshRenderer.enabled = true;
        }
        // Rotate around global Y axis
        transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);
    }
    
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") &&  !_isConsumed)
        {
            _meshRenderer.enabled = false;
            _isConsumed =  true;
            var bikeControls = other.gameObject.GetComponent<BikeControlsOW>();
            bikeControls.RefillBoost(bikeControls.maxBoostDuration / maxBoostDivider);
        }
    }
}