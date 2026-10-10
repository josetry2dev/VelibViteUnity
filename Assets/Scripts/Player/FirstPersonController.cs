using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class FirstPersonController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform playerCamera;

    [Header("Movement Settings")]
    [SerializeField] private float walkSpeed = 5.0f;
    [SerializeField] private float runSpeed = 9.0f;

    [Header("Look Settings")]
    [SerializeField] private float lookSpeed = 100.0f;
    [SerializeField] private float lookXLimit = 80.0f;

    [Header("Jump Settings")]
    [SerializeField] private float jumpForce = 5.0f;
    [SerializeField] private float groundCheckDistance = 0.2f;
    [SerializeField] private LayerMask groundLayer; // Pense à assigner ton layer "Ground" dans l'inspecteur

    [Header("Head Bobbing Settings")]
    [SerializeField] private float walkBobSpeed = 4.5f;
    [SerializeField] private float walkBobAmount = 0.15f;
    [SerializeField] private float runBobSpeed = 6.5f;
    [SerializeField] private float runBobAmount = 0.25f;
    [SerializeField] private float bobReturnSpeed = 5.0f;

    // Inputs
    private PlayerInput _playerInput;
    private InputAction _moveAction;
    private InputAction _lookAction;
    private InputAction _jumpAction;
    private InputAction _sprintAction;
    private Vector2 _moveInput;
    private Vector2 _lookInput;

    // Etat
    private Rigidbody _rb;
    private float _rotationX = 0;
    private bool _isGrounded;
    private bool _wantsToSprint = false; // Mémorise si le joueur a activé le sprint
    private bool _isSprinting = false;   // Est-ce qu'il remplit les conditions pour sprinter

    // Bobbing
    private float _defaultCamYPos;
    private float _timer = 0f;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.freezeRotation = true; // Sécurité

        _playerInput = GetComponent<PlayerInput>();
        _moveAction = _playerInput.actions["Move"];
        _lookAction = _playerInput.actions["Look"];
        _jumpAction = _playerInput.actions["Jump"];
        _sprintAction = _playerInput.actions["Sprint"];

        if (playerCamera == null) playerCamera = Camera.main.transform;
    }

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        _defaultCamYPos = playerCamera.localPosition.y;

        // Abonnement à l'événement de sprint pour le toggle
        _sprintAction.performed += ctx => ToggleSprint();
    }

    private void OnDestroy()
    {
        // Bonne pratique de se désabonner
        _sprintAction.performed -= ctx => ToggleSprint();
    }

    private void Update()
    {
        _moveInput = _moveAction.ReadValue<Vector2>();
        _lookInput = _lookAction.ReadValue<Vector2>();

        GroundCheck();
        HandleRotation();
        HandleHeadBob();

        // Logique de désactivation du sprint : si le joueur arrête d'avancer
        if (_isSprinting && _moveInput.y <= 0.1f)
        {
            _isSprinting = false;
        }
    }

    private void FixedUpdate()
    {
        // La physique se fait dans FixedUpdate
        HandleMovement();
        HandleJumping();
    }

    // --- Logique Sprint (Toggle) ---
    private void ToggleSprint()
    {
        // Appuyer sur le bouton inverse l'état désiré
        _wantsToSprint = !_wantsToSprint;

        // Si on veut sprinter mais qu'on recule ou est immobile, ça ne s'active pas tout de suite
        // (Ça s'activera dès qu'on repoussera le stick en avant, voir Update)
        if (_moveInput.y > 0.1f)
        {
             _isSprinting = _wantsToSprint;
        }
        else
        {
            _isSprinting = false;
        }
    }

    // --- Mouvements Physiques ---
    private void HandleMovement()
    {
        // Calcule la vitesse actuelle
        float currentSpeed = _isSprinting ? runSpeed : walkSpeed;

        // Calcule la direction locale
        Vector3 targetVelocity = new Vector3(_moveInput.x, 0, _moveInput.y) * currentSpeed;
        
        // Convertit en direction monde
        targetVelocity = transform.TransformDirection(targetVelocity);

        // Applique la force pour atteindre la vélocité cible (plus propre que Velocity Change direct)
        Vector3 velocity = _rb.linearVelocity;
        Vector3 velocityChange = targetVelocity - velocity;
        velocityChange.x = Mathf.Clamp(velocityChange.x, -10f, 10f); // Limite l'accélération pour le feeling
        velocityChange.z = Mathf.Clamp(velocityChange.z, -10f, 10f);
        velocityChange.y = 0; // On ne touche pas à la vélocité verticale ici

        _rb.AddForce(velocityChange, ForceMode.VelocityChange);
    }

    private void HandleJumping()
    {
        if (_jumpAction.triggered && _isGrounded)
        {
            // Impulsion physique vers le haut
            _rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        }
    }

    // --- Détection Sol ---
    private void GroundCheck()
    {
        // Raycast vers le bas depuis le centre du joueur
        _isGrounded = Physics.Raycast(transform.position, Vector3.down, groundCheckDistance, groundLayer);
        
        // Debug pour voir la ligne dans l'éditeur
        Debug.DrawRay(transform.position, Vector3.down * groundCheckDistance, _isGrounded ? Color.green : Color.red);
    }

    // --- Reste (Identique mais propre) ---
    private void HandleRotation()
    {
        float lookX = _lookInput.x * lookSpeed * Time.deltaTime;
        float lookY = _lookInput.y * lookSpeed * Time.deltaTime;

        transform.Rotate(0, lookX, 0);
        _rotationX -= lookY;
        _rotationX = Mathf.Clamp(_rotationX, -lookXLimit, lookXLimit);
        playerCamera.localRotation = Quaternion.Euler(_rotationX, 0, 0);
    }

    private void HandleHeadBob()
    {
        bool isMoving = _moveInput.magnitude > 0.1f;
        float speed = _isSprinting ? runBobSpeed : walkBobSpeed;
        float amount = _isSprinting ? runBobAmount : walkBobAmount;

        if (isMoving && _isGrounded)
        {
            _timer += Time.deltaTime * speed;
            float targetY = _defaultCamYPos + Mathf.Sin(_timer) * amount;
            playerCamera.localPosition = new Vector3(playerCamera.localPosition.x, targetY, playerCamera.localPosition.z);
        }
        else
        {
            _timer = 0f;
            float targetY = Mathf.Lerp(playerCamera.localPosition.y, _defaultCamYPos, Time.deltaTime * bobReturnSpeed);
            playerCamera.localPosition = new Vector3(playerCamera.localPosition.x, targetY, playerCamera.localPosition.z);
        }
    }
}