using System;
using UnityEngine;

public class BikeControlsRunner : BikeControlsBase
{
    [Header("Settings")]
    public float acceleration = 12f;
    public float maxSpeed = 35f;
    public float turnSpeed = 15f;
    private float currentSpeed = 0f;
    
    [Header("Boost")]
    public float boostDuration = 3f;
    public float boostSpeed = 50f;
    public float boostAcceleration = 20f;
    private float _boostTimer = 0f;
    private bool _isReturning = false;
    private float _originalSpeed;
    private float _originalAcceleration;
    
    protected void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Collectible"))
        {
            Destroy(other.gameObject);
            TriggerBoost();
        }
    }

    void Update()
    {
        ReadContinuousInputs();
        HandleBoost();
    }

    private void FixedUpdate()
    {
        ApplyPhysicsMovement();
        ApplyGravityMultiplier();
    }

    protected override void ApplyPhysicsMovement()
    {
        currentSpeed += acceleration * Time.fixedDeltaTime;
        currentSpeed = Mathf.Clamp(currentSpeed, 0f, maxSpeed);

        Vector3 forwardMove = transform.forward * currentSpeed;
        Vector3 lateralMove = transform.right * _steerInput * turnSpeed;

        Vector3 targetVelocity = forwardMove + lateralMove;

        _rb.linearVelocity = new Vector3(targetVelocity.x, _rb.linearVelocity.y, targetVelocity.z);
    }
    
    private void TriggerBoost()
    {
        _originalSpeed = maxSpeed;
        _originalAcceleration = acceleration;

        maxSpeed = boostSpeed;
        acceleration = boostAcceleration;

        _boostTimer = boostDuration;
        _isReturning = false;
    }

    private void HandleBoost()
    {
        if (_boostTimer > 0f)
        {
            _boostTimer -= Time.deltaTime;
            if (_boostTimer <= 0f)
            {
                _isReturning = true;
            }
        }
        if (_isReturning)
        {
            maxSpeed = Mathf.MoveTowards(maxSpeed, _originalSpeed, boostAcceleration * Time.deltaTime);
            acceleration = Mathf.MoveTowards(acceleration, _originalAcceleration, boostAcceleration/2 * Time.deltaTime);
            if (Mathf.Approximately(maxSpeed, _originalSpeed))
            {
                _isReturning = false;
            }
        }
    }
    
    public override bool IsBoosting()
    {
        return (_boostTimer >0);
    }
}
