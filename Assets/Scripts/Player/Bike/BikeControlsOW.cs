using UnityEngine;
using UnityEngine.InputSystem;

public class BikeControlsOW : BikeControlsBase
{
    #region Params
    [Header(" --- Speed Settings --- ")]
    [Tooltip("Accélération standard quand on pédale")]
    public float accelerationPower = 12f;
    [Tooltip("Vitesse Max standard (100% du palier)")]
    public float maxSpeed = 20f;
    [Tooltip("Vitesse Max en marche arrière")]
    public float reverseMaxSpeed = 5f;

    [Header(" --- Turn Settings --- ")]
    [Tooltip("Vitesse de virage")]
    public float turnSpeed = 50f;
    public float stopTurnFactor = 3f;
    public float wobbleAmplitude = 0.5f;
    public float wobbleFrequency = 6f;

    [Header(" --- Friction & Braking --- ")]
    [Tooltip("Décélération naturelle (roue libre)")]
    public float coastingDrag = 2f;
    [Tooltip("Force du freinage de base (0 à MaxSpeed)")]
    public float brakingPower = 25f;
    [Tooltip("Force du freinage MAX (quand on est à fond de boost)")]
    public float boostBrakingPower = 50f;

    [Header(" --- Drift Settings --- ")]
    [Tooltip("Freinage appliqué pendant le drift")]
    public float driftDeceleration = 10f; 
    [Tooltip("Multiplicateur de rotation pendant le drift (pour faire des têtes à queue)")]
    public float driftTurnMultiplier = 2.5f; 
    [Tooltip("Adhérence pdt le drift. Plus c'est bas, plus ça glisse. (Ex: 2.0)")]
    public float driftGrip = 2f;

    [Header(" --- Boost Settings --- ")]
    public float boostMaxSpeed = 50f;
    public float boostAcceleration = 30f;
    public float boostDeceleration = 20f;
    [Tooltip("Durée totale du boost en secondes")]
    public float maxBoostDuration = 3f;

    [Header(" --- Jump Settings --- ")]
    public float jumpForce = 500f;
    public float groundCheckDistance = 1.2f;
    public LayerMask groundLayer;

    [Header(" --- Hands Free & Drunk Settings --- ")]
    public bool handsFree;
    [SerializeField] private float instabilityIntensity = 40f;
    [SerializeField] private float instabilitySpeed = 0.5f;

    [Header(" --- Sounds --- ")]
    public AudioSource sonnetteAudio;
    public AudioSource roueLibreAudio;

    [Header(" --- Debug Info (Read Only) --- ")]
    [SerializeField] private float currentSpeed;
    [SerializeField] private int currentTierIndex;
    [SerializeField] private float boostFuel;
    [SerializeField] private bool isBoosting;
    [SerializeField] private bool isBraking;
    [SerializeField] private bool isDrifting;
    [SerializeField] private bool isReversing;
    [SerializeField] private bool isBroken;
    [SerializeField] private bool isGrounded = true;
    [SerializeField] private float targetSpeed;

    // References added to base ones
    private InputAction _accelerateAction;
    private InputAction _brakeAction;
    private InputAction _driftAction;
    private InputAction _boostAction;
    private InputAction _jumpAction;
    private InputAction _sonnetteAction;

    // Internal State
    private const int MaxTiers = 5;
    private float turn;
    private float _boostConsumed;

    // Drunk
    private float _drunkPercentage;
    private float _drunkTransitionSpeed;
    private float _currentInstability;

    // Bonk
    private float _impactTimer;
    private float _bonkCooldown;
    #endregion
    
    #region Awake Enable
    protected override void Awake()
    {
        base.Awake();
        // Setup Inputs
        _accelerateAction = _playerInput.actions["Accelerate"];
        _brakeAction = _playerInput.actions["Brake"];
        _driftAction = _playerInput.actions["Drift"];
        _boostAction = _playerInput.actions["Boost"];
        _jumpAction = _playerInput.actions["Jump"];
        _sonnetteAction = _playerInput.actions["Sonnette"];

        roueLibreAudio.volume = 0.2f;
    }
    
    void OnEnable()
    {
        _accelerateAction.started += OnAcceleratePressed;
        
        // Logique spécifique pour la Marche Arrière
        _brakeAction.started += OnBrakeStarted;
        _brakeAction.canceled += OnBrakeCanceled;
        _jumpAction.canceled += OnJumpPressed;
        _sonnetteAction.started += OnSonnettePressed;
    }
    
    void OnDisable()
    {
        _accelerateAction.started -= OnAcceleratePressed;
        _brakeAction.started -= OnBrakeStarted;
        _brakeAction.canceled -= OnBrakeCanceled;
        _jumpAction.canceled -= OnJumpPressed;
        _sonnetteAction.started -= OnSonnettePressed;
    }
    #endregion
    
    #region Controls Callbacks
    private void OnAcceleratePressed(InputAction.CallbackContext context)
    {
        if (isBraking) return;
        if (roueLibreAudio.isPlaying) roueLibreAudio.Stop();
        if (isBroken)
        {
            currentTierIndex = 1;
            UpdateTargetSpeedFromTier();
            return;
        }
        if (!Mathf.Approximately(currentSpeed, 0f) && Mathf.Approximately(targetSpeed, 0f)) // En roue libre, on reprend le palier juste au dessus de la vitesse actuelle
        {
            float tierSize = maxSpeed / MaxTiers;
            int tierBasedOnSpeed = Mathf.CeilToInt(currentSpeed / tierSize);
            currentTierIndex = Mathf.Clamp(tierBasedOnSpeed, 0, MaxTiers);
        }
        else if (currentTierIndex < MaxTiers)
        {
            currentTierIndex++;
        }
        UpdateTargetSpeedFromTier();
    }
    private void OnBrakeStarted(InputAction.CallbackContext context)
    {
        // LE COEUR DE LA MARCHE ARRIERE
        // Si on appuie sur le frein ALORS qu'on est quasi à l'arrêt, on autorise la marche arrière.
        // Mathf.Abs(_currentSpeed) < 1f : Tolérance de 1 unité pour dire "à l'arrêt"
        if (isGrounded && currentSpeed < 1f)
        {
            isReversing = true;
        }
    }
    private void OnBrakeCanceled(InputAction.CallbackContext context)
    {
        // Dès qu'on relâche le frein, on arrête la marche arrière
        isReversing = false;
    }
    
    private void OnSonnettePressed(InputAction.CallbackContext context)
    {
        sonnetteAudio.Play();
    }
    
    private void OnJumpPressed(InputAction.CallbackContext context)
    {
        // Saut uniquement si au sol
        if (isGrounded)
        {
            // Reset de la vélocité Y pour un saut constant même si on descend une pente
            Vector3 vel = _rb.linearVelocity;
            vel.y = 0;
            _rb.linearVelocity = vel;

            _rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        }
    }
    
    protected override void ReadContinuousInputs()
    {
        base.ReadContinuousInputs();
        isBraking = _brakeAction.IsPressed();
        isDrifting = _driftAction.IsPressed(); 
        if (isDrifting) isReversing = false; 
    }
    
    #endregion
    
    #region Updates
    void Update()
    {
        CheckGroundStatus();
        ReadContinuousInputs();
        
        if (_bonkCooldown > 0) _bonkCooldown -= Time.fixedDeltaTime;
        
        // Audio 
        if ((currentSpeed < 0.5f && roueLibreAudio.isPlaying) 
            || (isBraking && roueLibreAudio.isPlaying)
            || (isBoosting && roueLibreAudio.isPlaying)) 
            roueLibreAudio.Stop();
        else if (Mathf.Approximately(targetSpeed, 0f) && !isDrifting && !roueLibreAudio.isPlaying && roueLibreAudio.isActiveAndEnabled) roueLibreAudio.Play();
        
        // Si on est en l'air, on ne gère pas le moteur ni le boost
        if (isGrounded)
        {
            HandleBoostLogic();
            HandleSpeedLogic();
        }
    }
    
    void FixedUpdate()
    {
        // Bonk scenario
        if (_impactTimer > 0)
        {
            _impactTimer -= Time.fixedDeltaTime;
            return; 
        }
        
        if (isGrounded)
        {
            ApplyPhysicsMovement();
        }
        else
        {
            ApplyGravityMultiplier();
        }
    }
    #endregion
    
    #region Boost
    private void HandleBoostLogic()
    {
        if (isBroken) return;
        bool boostPressed = _boostAction.IsPressed();

        // Activation du boost si on appuie ET qu'il reste du fuel
        if (boostPressed && boostFuel > 0)
        {
            if (!isBoosting)
            {
                // Démarrage du boost
                isBoosting = true;
            }

            // Consommation
            boostFuel -= Time.deltaTime;
            _boostConsumed += Time.deltaTime;
            if (boostFuel < 0) boostFuel = 0;
        }
        else
        {
            // Arrêt du boost
            if (isBoosting)
            {
                isBoosting = false;
                RecalculateTierAfterBoost();
            }
        }
    }
    private void RecalculateTierAfterBoost()
    {
        // "La vitesse revient au palier inférieur à la vitesse actuelle"
        float tierSize = maxSpeed / MaxTiers;
        
        // On prend la vitesse actuelle, on regarde combien de "tranches" de 25% on remplit
        // FloorToInt pour prendre le palier EN DESSOUS
        int tierBelow = Mathf.FloorToInt(currentSpeed / tierSize);

        // On s'assure qu'on reste dans les bornes (0 à 4)
        currentTierIndex = Mathf.Clamp(tierBelow, 0, MaxTiers);
        
        UpdateTargetSpeedFromTier();
    }
    public void RefillBoost(float amount)
    {
        if (isBroken) return;
        if (boostFuel < maxBoostDuration) boostFuel += amount;
    }
    public void RemoveBoost(float amount)
    {
        if (boostFuel >= amount) boostFuel -= amount;
    }
    #endregion

    #region Speed/Turn
    private void UpdateTargetSpeedFromTier()
    {
        float percentage = (float)currentTierIndex / MaxTiers;
        targetSpeed = maxSpeed * percentage;
    }

    private void HandleSpeedLogic()
    {
        float dt = Time.deltaTime;
        // CAS 0 : MARCHE ARRIÈRE
        if (isReversing)
        {
            currentSpeed = Mathf.MoveTowards(currentSpeed, -reverseMaxSpeed, accelerationPower * dt);
            currentTierIndex = 0;
            targetSpeed = 0;
        }
        // CAS 1 : DRIFT
        else if (isDrifting)
        {
            currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, driftDeceleration * dt); // Roue libre
            currentTierIndex = 0;
            UpdateTargetSpeedFromTier();
        }
        // CAS 2 : BOOST
        else if (isBoosting)
        {
            currentSpeed = Mathf.MoveTowards(currentSpeed, boostMaxSpeed, boostAcceleration * dt);
        }
        // CAS 3 : FREINAGE
        else if (isBraking)
        {
            float currentBrakePower = brakingPower;
            if (currentSpeed > maxSpeed) // Frein plus puissant lors du boost
            {
                float t = Mathf.InverseLerp(maxSpeed, boostMaxSpeed, currentSpeed);
                currentBrakePower = Mathf.Lerp(brakingPower, boostBrakingPower, t);
            }
            currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, currentBrakePower * dt);
            currentTierIndex = 0;
            UpdateTargetSpeedFromTier();
        }
        // CAS 4 : RETOUR DE BOOST
        else if (currentSpeed > maxSpeed)
        {
            currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, boostDeceleration * dt);
        }
        // CAS 5 : ROUE LIBRE
        else if (currentSpeed > targetSpeed)
        {
            currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, coastingDrag * dt);
        }
        // CAS 6 : PÉDALAGE
        else
        {
            currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, accelerationPower * dt);
        }
    }
    
    protected override void ApplyPhysicsMovement()
    {
        turn = _steerInput * turnSpeed * Time.fixedDeltaTime;
        
        _currentInstability = Mathf.Lerp(_currentInstability, _drunkPercentage * instabilityIntensity, Time.fixedDeltaTime * _drunkTransitionSpeed);
        if (currentSpeed > 0.5f && _currentInstability > 0.001f)
        {
            float noise = Mathf.PerlinNoise(Time.time * instabilitySpeed, 0f) * 2f - 1f;
            turn += noise * _currentInstability * Time.fixedDeltaTime;
        }
        
        // if (handsFree && currentSpeed > 0.5f)
        // {
        //     float noise = Mathf.PerlinNoise(Time.time * instabilitySpeed, 0f) * 2f - 1f;
        //     turn += noise * instabilityIntensity * Time.fixedDeltaTime;
        // }
        
        if (isBroken && currentSpeed > 0.1f)
        {
            float wobble = Mathf.Sin(Time.time * wobbleFrequency) * wobbleAmplitude;
            turn += wobble;
        }
        
        if (currentSpeed > -0.1f && currentSpeed < 0.1f) turn /= stopTurnFactor; // Arret
        if (currentSpeed < 0) turn = -turn; // Marche arrière
        if (isDrifting && (currentSpeed > 0.5f)) turn *= driftTurnMultiplier; // Drift
        
        Quaternion turnRotation = Quaternion.Euler(0f, turn, 0f);
        _rb.MoveRotation(_rb.rotation * turnRotation);

        // Glissade en Drift
        Vector3 worldVelocity = _rb.linearVelocity;
        Vector3 localVel = transform.InverseTransformVector(worldVelocity); // Vitesse forward devient vitesse laterale (x)
        
        if (isDrifting)
        {
            // En drift : On tue la vitesse latérale doucement selon le DriftGrip.
            // Si DriftGrip = 0, Mathf.Lerp ne change rien -> Glissade infinie
            localVel.x = Mathf.Lerp(localVel.x, 0f, driftGrip * Time.fixedDeltaTime);
        }
        else
        {
            localVel.x = 0;
        }
        localVel.z = currentSpeed;
        Vector3 newWorldVelocity = transform.TransformVector(localVel);
        newWorldVelocity.y = worldVelocity.y;
        _rb.linearVelocity = newWorldVelocity;
    }
    #endregion
    
    #region Collision
    public void ApplyBonk(Vector3 impactForce)
    {
        if (_bonkCooldown > 0) return;
        _impactTimer = 0.5f;
        _bonkCooldown = 1.5f;
        currentSpeed = 0f;
        currentTierIndex = 0;
        targetSpeed = 0f;
        _rb.linearVelocity = Vector3.zero;
        
        transform.position += Vector3.up * 0.15f; // anti clip dans le sol 

        _rb.AddForce(impactForce, ForceMode.Impulse);
        
        RemoveBoost(0.1f);
    }
    
    public void StepUp()
    {
        transform.position += Vector3.up * 0.2f;
    }

    public override void ResetBikePhysics(Vector3 respawnPos, Quaternion respawnRot)
    {
        base.ResetBikePhysics(respawnPos, respawnRot);
        isBoosting = false;
        targetSpeed = 0f;
        currentSpeed = 0;
        currentTierIndex = 0;
    }

    public void SetBrokenMode()
    {
        isBroken = true;
        boostFuel = 0f;
    }
    
    #endregion
    
    #region Ground 
    private void CheckGroundStatus()
    {
        isGrounded = Physics.Raycast(transform.position + Vector3.up * 0.5f, Vector3.down, groundCheckDistance, groundLayer);
    }
    void OnDrawGizmos()
    {
        // Change la couleur selon l'état
        Gizmos.color = isGrounded ? Color.green : Color.red;
        
        // Dessine le rayon exactement comme dans le code
        Vector3 origin = transform.position + Vector3.up * 0.5f;
        Vector3 direction = Vector3.down * groundCheckDistance;
        
        Gizmos.DrawLine(origin, origin + direction);
        
        // Petite boule au bout pour bien voir où ça s'arrête
        Gizmos.DrawSphere(origin + direction, 0.1f);
    }
    #endregion
    
    #region Drunk
    public void ApplyDrunk(float drunkPercentage, float drunkTransitionSpeed)
    {
        _drunkPercentage = drunkPercentage;
        _drunkTransitionSpeed = drunkTransitionSpeed;
    }
    #endregion 
    
    #region Accesseurs
    public override float GetSpeedForUI()
    {
        if (currentSpeed > 0.1f)
        {
            return currentSpeed / maxSpeed * 25;
        }
        return 0;
    }
    
    public int GetTierLevel()
    {
        return currentTierIndex;
    }
    public float GetBoostRatio()
    {
        if (maxBoostDuration <= 0) return 0f;
        return boostFuel / maxBoostDuration;
    }
    public override bool IsBoosting()
    {
        return isBoosting;
    }
    public override bool IsGrounded()
    {
        return isGrounded;
    }

    public float GetConsumedBoost()
    {
        var normalizedBoost = _boostConsumed / maxBoostDuration;
        _boostConsumed = 0;
        return normalizedBoost;
    }
    #endregion
}
