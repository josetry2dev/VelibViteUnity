using UnityEngine;

public class BikeCameraEffect : MonoBehaviour
{
    [Header(" --- References --- ")]
    public BikeControlsBase bikeControls;
    public Camera targetCamera;

    [Header(" --- FOV Settings --- ")]
    public float normalFOV = 65f;
    public float boostFOV = 85f;
    public float transitionSpeed = 5f;
    public float returnSpeed = 1.5f;

    [Header(" --- Crash Settings --- ")]
    public float crashTiltAngle = 80f;
    public float crashDropAmount = 1.0f;
    public float crashSideOffset = 1.5f;
    public float crashSpeed = 10f;
    
    private bool _isCrashed;
    private Vector3 _initialLocalPos;
    private Quaternion _initialLocalRotation;

    void Start()
    {
        if (targetCamera == null) targetCamera = GetComponent<Camera>();
        if (targetCamera != null)
        {
            targetCamera.fieldOfView = normalFOV;
            _initialLocalPos = targetCamera.transform.localPosition;
            _initialLocalRotation = targetCamera.transform.localRotation;
        }
    }
    public void TriggerCrashEffect()
    {
        _isCrashed = true;
    }
    void Update()
    {
        if (bikeControls == null || targetCamera == null) return;
        if (_isCrashed) HandleCrashAnimation();
        else HandleFOVLogic();
    }
    void HandleFOVLogic()
    {
        if (bikeControls.IsBoosting())
        {
            targetCamera.fieldOfView = Mathf.Lerp(targetCamera.fieldOfView, boostFOV, transitionSpeed * Time.deltaTime);
        }
        else
        {
            targetCamera.fieldOfView = Mathf.Lerp(targetCamera.fieldOfView, normalFOV, returnSpeed * Time.deltaTime);
        }
        
    }
    void HandleCrashAnimation()
    {
        float dt = Time.deltaTime;
        Quaternion currentRot = targetCamera.transform.localRotation;
        Quaternion targetRot = Quaternion.Euler(currentRot.eulerAngles.x, currentRot.eulerAngles.y, crashTiltAngle);
        Vector3 targetPos = _initialLocalPos - (Vector3.up * crashDropAmount) + (Vector3.right * crashSideOffset);
        
        targetCamera.transform.localRotation = Quaternion.Lerp(targetCamera.transform.localRotation, targetRot, crashSpeed * dt);
        targetCamera.transform.localPosition = Vector3.Lerp(targetCamera.transform.localPosition, targetPos, crashSpeed * dt);
    }
    public void ResetCamera()
    {
        _isCrashed = false;
        targetCamera.transform.localPosition = _initialLocalPos;
        targetCamera.transform.localRotation = _initialLocalRotation;
        targetCamera.fieldOfView = normalFOV;
    }
}