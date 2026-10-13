using UnityEngine;

public class FpsCameraController : MonoBehaviour
{
    [SerializeField] private FpsInputReader inputReader;
    [SerializeField] private Transform playerBody;
    [SerializeField] private float sensitivity = 150f;
    [SerializeField] private float minPitch = -85f;
    [SerializeField] private float maxPitch = 85f;

    private float _pitch;

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

        transform.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
    }
}