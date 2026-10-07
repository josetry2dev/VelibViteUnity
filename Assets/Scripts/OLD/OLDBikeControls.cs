using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody), typeof(PlayerInput))]
public class BikeControls : MonoBehaviour
{
    [Header(" --- Speed Settings --- ")]
    [Tooltip("Accélération standard quand on pédale")]
    public float accelerationPower = 12f;
    [Tooltip("Vitesse Max standard (100% du palier)")]
    public float maxSpeed = 20f;
    [Tooltip("Vitesse Max en marche arrière")]
    public float reverseMaxSpeed = 5f; // Nouvelle variable
    [Tooltip("Vitesse de rotation")]
    public float turnSpeed = 50f;
    public float stopTurnFactor = 3f;
    
    [Header(" --- Friction & Braking --- ")]
    [Tooltip("Décélération naturelle (roue libre)")]
    public float coastingDrag = 2f;
    [Tooltip("Force du freinage de base (0 à MaxSpeed)")]
    public float brakingPower = 25f;
    [Tooltip("Force du freinage MAX (quand on est à fond de boost)")]
    public float boostBrakingPower = 50f;

    [Header(" --- Drift Settings (New) --- ")]
    [Tooltip("Freinage appliqué pendant le drift")]
    public float driftDeceleration = 10f; 
    [Tooltip("Multiplicateur de rotation pendant le drift (pour faire des têtes à queue)")]
    public float driftTurnMultiplier = 2.5f; 
    [Tooltip("Adhérence pdt le drift. Plus c'est bas, plus ça glisse. (Ex: 2.0)")]
    public float driftGrip = 2f;

    [Header(" --- Boost Settings --- ")]
    public float boostMaxSpeed = 50f;
    public float boostAcceleration = 30f;
    public float boostDecceleration = 20f;
    [Tooltip("Durée totale du boost en secondes")]
    public float maxBoostDuration = 3f;

    [Header(" --- Jump Settings --- ")]
    public float jumpForce = 500f;
    public float groundCheckDistance = 1.2f;
    public LayerMask groundLayer;
    
    [Header(" --- Bonk Settings --- ")]
    private float _impactTimer = 0f; 
    
    [Header(" --- Debug Info (Read Only) --- ")]
    [SerializeField] private float _currentSpeed = 0f;
    [SerializeField] private int _currentTierIndex = 0; 
    [SerializeField] private float _boostFuel = 0f;
    [SerializeField] private bool _isBoosting = false;
    [SerializeField] private bool _isBraking = false;
    [SerializeField] private bool _isDrifting = false;
    [SerializeField] private bool _isReversing = false;
    [SerializeField] private bool _isGrounded = true;
    [SerializeField] private float _targetSpeed;

    // References
    private Rigidbody _rb;
    private PlayerInput _playerInput;
    private InputAction _moveAction;
    private InputAction _accelerateAction;
    private InputAction _brakeAction;
    private InputAction _driftAction;
    private InputAction _boostAction;
    private InputAction _jumpAction;

    // Internal State
    private float _steerInput = 0f;
    private const int MAX_TIERS = 5;

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _playerInput = GetComponent<PlayerInput>();

        // Configuration Rigidbody
        _rb.interpolation = RigidbodyInterpolation.Interpolate;
        _rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        
        // Setup Inputs
        _moveAction = _playerInput.actions["Move"];
        _accelerateAction = _playerInput.actions["Accelerate"];
        _brakeAction = _playerInput.actions["Brake"];
        _driftAction = _playerInput.actions["Drift"];
        _boostAction = _playerInput.actions["Boost"];
        _jumpAction = _playerInput.actions["Jump"];
    }

    void OnEnable()
    {
        _accelerateAction.started += OnAcceleratePressed;
        
        // Logique spécifique pour la Marche Arrière
        _brakeAction.started += OnBrakeStarted;
        _brakeAction.canceled += OnBrakeCanceled;

        // Logique du Saut (au relâchement)
        _jumpAction.canceled += OnJumpReleased;
    }

    void OnDisable()
    {
        _accelerateAction.started -= OnAcceleratePressed;
        _brakeAction.started -= OnBrakeStarted;
        _brakeAction.canceled -= OnBrakeCanceled;
        _jumpAction.canceled -= OnJumpReleased;
    }

    // Fonction appelée quand on tape sur la touche Accelerate
    private void OnAcceleratePressed(InputAction.CallbackContext context)
    {
        // Si on est en train de freiner, l'accélérateur ne fait rien (ou alors faudrait couper le frein)
        if (_isBraking) return;

        // Logique "Reprise" : Si on est en roue libre (vitesse qui chute), 
        // on repart du palier juste au-dessus de la vitesse actuelle.
        if (_currentSpeed != 0 && _targetSpeed == 0)
        {
            // Calcul du tier correspondant à la vitesse actuelle
            float tierSize = maxSpeed / MAX_TIERS; // Ex: 20 / 4 = 5
            int tierBasedOnSpeed = Mathf.CeilToInt(_currentSpeed / tierSize);
            
            // On vise le palier au-dessus
            _currentTierIndex = Mathf.Clamp(tierBasedOnSpeed, 0, MAX_TIERS);
        }
        else
        {
            // Comportement normal : on monte d'un cran
            if (_currentTierIndex < MAX_TIERS)
            {
                _currentTierIndex++;
            }
        }

        UpdateTargetSpeedFromTier();
    }
    
    private void OnBrakeStarted(InputAction.CallbackContext context)
    {
        // LE COEUR DE LA MARCHE ARRIERE
        // Si on appuie sur le frein ALORS qu'on est quasi à l'arrêt, on autorise la marche arrière.
        // Mathf.Abs(_currentSpeed) < 1f : Tolérance de 1 unité pour dire "à l'arrêt"
        if (_isGrounded && Mathf.Abs(_currentSpeed) < 1f)
        {
            _isReversing = true;
        }
    }

    private void OnBrakeCanceled(InputAction.CallbackContext context)
    {
        // Dès qu'on relâche le frein, on arrête la marche arrière
        _isReversing = false;
    }

    private void OnJumpReleased(InputAction.CallbackContext context)
    {
        // Saut uniquement si au sol
        if (_isGrounded)
        {
            // Reset de la vélocité Y pour un saut constant même si on descend une pente
            Vector3 vel = _rb.linearVelocity;
            vel.y = 0;
            _rb.linearVelocity = vel;

            _rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        }
    }
    
    // --- UPDATE LOOPS ---
    
    void Update()
    {
        CheckGroundStatus();
        ReadContinuousInputs();
        
        // Si on est en l'air, on ne gère pas le moteur ni le boost
        if (_isGrounded)
        {
            HandleBoostLogic();
            HandleSpeedLogic();
        }
    }

    void FixedUpdate()
    {
        // 1. GESTION DU TIMER D'IMPACT
        if (_impactTimer > 0)
        {
            _impactTimer -= Time.fixedDeltaTime;
            // TANT QUE le timer tourne, on n'applique PAS ApplyPhysics().
            // On laisse le Rigidbody gérer le rebond tout seul.
            return; 
        }
        
        if (_isGrounded)
        {
            ApplyPhysics();
        }
        else
        {
            _rb.AddForce(Physics.gravity * 1.5f, ForceMode.Acceleration);
        }
    }

    // --- LOGIC ---

    private void CheckGroundStatus()
    {
        // Raycast simple depuis le centre du vélo vers le bas
        _isGrounded = Physics.Raycast(transform.position + Vector3.up * 0.5f, Vector3.down, groundCheckDistance, groundLayer);
    }
    
    private void ReadContinuousInputs()
    {
        // Direction
        Vector2 dir = _moveAction.ReadValue<Vector2>();
        _steerInput = dir.x;

        // Freinage (Maintenu)
        _isBraking = _brakeAction.IsPressed();

        // Lecture du Drift
        _isDrifting = _driftAction.IsPressed(); 
        // Important : Si on drift, on désactive la logique de marche arrière potentielle
        if (_isDrifting) _isReversing = false; 
        
        // Boost (Maintenu) -> La logique est dans HandleBoostLogic
    }

    private void HandleBoostLogic()
    {
        bool boostPressed  = _boostAction.IsPressed();

        // Activation du boost si on appuie ET qu'il reste du fuel
        if (boostPressed  && _boostFuel > 0)
        {
            if (!_isBoosting)
            {
                // Démarrage du boost
                _isBoosting = true;
            }

            // Consommation
            _boostFuel -= Time.deltaTime;
            if (_boostFuel < 0) _boostFuel = 0;
        }
        else
        {
            // Arrêt du boost
            if (_isBoosting)
            {
                _isBoosting = false;
                RecalculateTierAfterBoost();
            }
        }
    }

    private void RecalculateTierAfterBoost()
    {
        // "La vitesse revient au palier inférieur à la vitesse actuelle"
        float tierSize = maxSpeed / MAX_TIERS;
        
        // On prend la vitesse actuelle, on regarde combien de "tranches" de 25% on remplit
        // FloorToInt pour prendre le palier EN DESSOUS
        int tierBelow = Mathf.FloorToInt(_currentSpeed / tierSize);

        // On s'assure qu'on reste dans les bornes (0 à 4)
        _currentTierIndex = Mathf.Clamp(tierBelow, 0, MAX_TIERS);
        
        UpdateTargetSpeedFromTier();
    }

    private void UpdateTargetSpeedFromTier()
    {
        // 0 appuis = 0%, 1 = 25%, 2 = 50%, 3 = 75%, 4 = 100%
        float percentage = (float)_currentTierIndex / MAX_TIERS;
        _targetSpeed = maxSpeed * percentage;
    }

    private void HandleSpeedLogic()
    {
        float dt = Time.deltaTime;
        
        // CAS 0 : MARCHE ARRIÈRE
        if (_isReversing)
        {

            _currentSpeed = Mathf.MoveTowards(_currentSpeed, -reverseMaxSpeed, accelerationPower * dt);


            _currentTierIndex = 0;
            _targetSpeed = 0;
        }
        // CAS 1 : DRIFT (Prioritaire sur frein normal)
        else if (_isDrifting)
        {
            // Le drift agit comme un frein, mais on garde de la vitesse pour glisser
            // On ne reset pas le TargetSpeed à 0 ici, on laisse l'inertie jouer
            _currentSpeed = Mathf.MoveTowards(_currentSpeed, 0f, driftDeceleration * dt);
            
            _currentTierIndex = 0;
            UpdateTargetSpeedFromTier();
        }
        // CAS 2 : BOOST
        else if (_isBoosting)
        {

            _currentSpeed = Mathf.MoveTowards(_currentSpeed, boostMaxSpeed, boostAcceleration * dt);
        }
        // CAS 3 : FREINAGE
        else if (_isBraking)
        {

            float currentBrakePower = brakingPower;


            if (_currentSpeed > maxSpeed)
            {

                float t = Mathf.InverseLerp(maxSpeed, boostMaxSpeed, _currentSpeed);

                currentBrakePower = Mathf.Lerp(brakingPower, boostBrakingPower, t);
            }


            _currentSpeed = Mathf.MoveTowards(_currentSpeed, 0f, currentBrakePower * dt);
            
            _currentTierIndex = 0;
            UpdateTargetSpeedFromTier();
        }

        // CAS 4 : RETOUR DE BOOST
        else if (_currentSpeed > maxSpeed)
        {
            _currentSpeed = Mathf.MoveTowards(_currentSpeed, _targetSpeed, boostDecceleration * dt);
        }
        // CAS 5 : ROUE LIBRE


        else if (_currentSpeed > _targetSpeed)
        {

            _currentSpeed = Mathf.MoveTowards(_currentSpeed, _targetSpeed, coastingDrag * dt);
        }
        // CAS 6 : PÉDALAGE


        else
        {
            _currentSpeed = Mathf.MoveTowards(_currentSpeed, _targetSpeed, accelerationPower * dt);
        }
    }
    
    private void ApplyPhysics()
    {
        // -------------------------------------------------------
        // 1. ROTATION (inchangé)
        // -------------------------------------------------------
        float turn = _steerInput * turnSpeed * Time.fixedDeltaTime;
        
        if (_currentSpeed > -0.1f && _currentSpeed < 0.1f) turn /= stopTurnFactor;
        if (_currentSpeed < 0) turn = -turn;

        if (_isDrifting && (_currentSpeed > 0.5f)) turn *= driftTurnMultiplier;

        Quaternion turnRotation = Quaternion.Euler(0f, turn, 0f);
        _rb.MoveRotation(_rb.rotation * turnRotation);

        // -------------------------------------------------------
        // 2. VÉLOCITÉ INTELLIGENTE (LOCALE VS MONDE)
        // -------------------------------------------------------
        
        // A. On récupère la vitesse actuelle du RB (Inertie réelle)
        Vector3 currentWorldVelocity = _rb.linearVelocity;
        
        // B. On la convertit en Vitesse Locale (Vue du vélo)
        // Si je vais au Nord et que je tourne mon vélo vers l'Est :
        // localVel.z (devant) devient 0, localVel.x (côté) devient énorme (glissade gauche).
        Vector3 localVel = transform.InverseTransformVector(currentWorldVelocity);
        
        // C. Gestion de la vitesse latérale (Axe X = Glissade)
        if (_isDrifting)
        {
            // En drift : On tue la vitesse latérale doucement selon le DriftGrip.
            // Si DriftGrip = 0, Mathf.Lerp ne change rien -> Glissade infinie
            localVel.x = Mathf.Lerp(localVel.x, 0f, driftGrip * Time.fixedDeltaTime);
        }
        else
        {
            // Pas drift : On tue la vitesse latérale direct (ça colle à la route)
            // On peut mettre un petit Lerp très rapide pour éviter les à-coups violents
            localVel.x = Mathf.Lerp(localVel.x, 0f, 15f * Time.fixedDeltaTime);
        }

        // D. Gestion de la vitesse Avant/Arrière (Axe Z = Moteur)
        // Ici on écrase la physique pure par notre logique de moteur (_currentSpeed)
        // C'est ce qui donne le contrôle arcade précis.
        localVel.z = _currentSpeed;

        // E. On repasse en Monde pour l'appliquer
        Vector3 newWorldVelocity = transform.TransformVector(localVel);

        // F. On remet la gravité (Y) qu'on avait au début
        newWorldVelocity.y = currentWorldVelocity.y;

        // G. Application finale
        _rb.linearVelocity = newWorldVelocity;
    }
    
    public void ApplyBonk(Vector3 impactForce)
    {
        Debug.Log("BONK ACTIVÉ !");

        _impactTimer = 0.5f;
        _currentSpeed = 0f;
        _currentTierIndex = 0;
        _targetSpeed = 0f;
        _rb.linearVelocity = Vector3.zero;

        // SECURITÉ 2 : Le Micro-Lift
        // On soulève le vélo de 0.1 unité pour être sûr qu'il ne touche plus le sol/l'objet
        // et qu'il ne puisse pas "clipper" à travers.
        transform.position += Vector3.up * 0.15f;

        _rb.AddForce(impactForce, ForceMode.Impulse);
    }
    
    // --- Interaction Collectible ---

    public void RefillBoost()
    {
        // Remplit la jauge à fond
        _boostFuel = maxBoostDuration;
        Debug.Log("Boost Refilled !");
    }

    public void RemoveBoost(float amount)
    {
        _boostFuel -= amount;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Collectible"))
        {
            Destroy(other.gameObject);
            RefillBoost();
        }
    }
    
    void OnDrawGizmos()
    {
        // Change la couleur selon l'état
        Gizmos.color = _isGrounded ? Color.green : Color.red;
        
        // Dessine le rayon exactement comme dans le code
        Vector3 origin = transform.position + Vector3.up * 0.5f;
        Vector3 direction = Vector3.down * groundCheckDistance;
        
        Gizmos.DrawLine(origin, origin + direction);
        
        // Petite boule au bout pour bien voir où ça s'arrête
        Gizmos.DrawSphere(origin + direction, 0.1f);
    }
    
    
    // Renvoie la vitesse en valeur positive (même en marche arrière) et arrondie si tu veux
    public float GetSpeedForUI()
    {
        return Mathf.Abs(_currentSpeed); 
    }

    // Renvoie le niveau de tier actuel (0 à 5)
    public int GetTierLevel()
    {
        return _currentTierIndex;
    }

    // Renvoie un chiffre entre 0 et 1 pour la barre de boost (FillAmount)
    public float GetBoostRatio()
    {
        if (maxBoostDuration <= 0) return 0f;
        return _boostFuel / maxBoostDuration;
    }
    
    public bool IsBoosting()
    {
        return _isBoosting;
    }
}