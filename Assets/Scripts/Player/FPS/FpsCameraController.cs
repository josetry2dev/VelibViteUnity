using UnityEngine;

public class FpsCameraController : MonoBehaviour
{
    [Header(" --- References --- ")]
    [SerializeField] private FpsInputReader inputReader;
    [SerializeField] private Transform playerBody;

    [Header(" --- Look Settings --- ")]
    [SerializeField] private float sensitivity = 150f;
    [SerializeField] private float minPitch = -85f;
    [SerializeField] private float maxPitch = 85f;

    [Header(" --- Drunk Sway (degrés à 100% bourré) --- ")]
    [SerializeField] private float drunkYawSway = 6f;
    [SerializeField] private float drunkPitchSway = 3f;
    [SerializeField] private float drunkRollSway = 5f;

    private float _pitch;
    private float _drunkPercentage;
    private float _drunkTransitionSpeed;
    private float _currentDrunk;

    private void Awake()
    {
        if (inputReader == null)
            inputReader = GetComponentInParent<FpsInputReader>();

        if (playerBody == null && transform.parent != null)
            playerBody = transform.parent;
    }

    private void Update()
    {
        if (inputReader == null || playerBody == null)
            return;

        Vector2 look = inputReader.Look;

        float mouseX = look.x * sensitivity * Time.deltaTime;
        float mouseY = look.y * sensitivity * Time.deltaTime;

        playerBody.Rotate(Vector3.up * mouseX);

        _pitch -= mouseY;
        _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);

        // Tangage d'ivrogne
        _currentDrunk = Mathf.Lerp(_currentDrunk, _drunkPercentage, Time.deltaTime * _drunkTransitionSpeed);
        float yawSway = Mathf.Sin(Time.time * 0.9f) * drunkYawSway * _currentDrunk;
        float pitchSway = Mathf.Sin(Time.time * 0.6f + 1f) * drunkPitchSway * _currentDrunk;
        float rollSway = Mathf.Sin(Time.time * 0.75f + 2f) * drunkRollSway * _currentDrunk;

        transform.localRotation = Quaternion.Euler(_pitch + pitchSway, yawSway, rollSway);
    }

    public void ApplyDrunk(float drunkPercentage, float drunkTransitionSpeed)
    {
        _drunkPercentage = drunkPercentage;
        _drunkTransitionSpeed = drunkTransitionSpeed;
    }
}