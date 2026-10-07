using UnityEngine;
using UnityEngine.InputSystem;

public abstract class BikeControlsBase : MonoBehaviour
{
    [Header(" --- Shared Settings --- ")]
    public float gravityMultiplier = 10f;
    
    protected Rigidbody _rb;
    protected PlayerInput _playerInput;
    private InputAction _moveAction;
    
    protected float _steerInput;
    
    protected virtual void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.interpolation = RigidbodyInterpolation.Interpolate;
        _rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        
        _playerInput = FindFirstObjectByType<PlayerInput>();
        _moveAction = _playerInput.actions["Move"]; 
    }
    
    // --- MÉTHODES ABSTRAITES ---
    protected abstract void ApplyPhysicsMovement();
    
    // --- MÉTHODES VIRTUELLES ---
    protected virtual void ReadContinuousInputs()
    {
        Vector2 dir = _moveAction.ReadValue<Vector2>();
        _steerInput = dir.x;
    }

    protected void ApplyGravityMultiplier()
    {
        if(_rb.useGravity) {
            _rb.AddForce(Physics.gravity * gravityMultiplier, ForceMode.Acceleration);
        }
    }
    
    public virtual void ResetBikePhysics(Vector3 respawnPos, Quaternion respawnRot)
    {
        _rb.linearVelocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
        transform.position = respawnPos;
        // Vector3 safeRot = respawnRot.eulerAngles;
        transform.rotation = respawnRot;
    }
    
    public virtual bool IsGrounded()
    {
        return true;
    }
    public virtual float GetSpeedForUI() { return _rb.linearVelocity.magnitude; }
    
    public abstract bool IsBoosting();
}