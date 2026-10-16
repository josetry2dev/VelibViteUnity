using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Porte entre deux scènes (ex : rue <-> studio).
// Quand le joueur entre dans la zone, le prompt "Triangle + texte" s'affiche, et Interact charge la scène cible.
// Au chargement, le joueur est replacé sur l'arrivalPoint du portail dont l'id correspond.
[RequireComponent(typeof(Collider))]
public class ScenePortal : MonoBehaviour
{
    [Header(" --- Destination --- ")]
    [Tooltip("Nom de la scène à charger (ex : 02_Studio)")]
    public string targetScene;
    [Tooltip("Id du portail d'arrivée dans la scène cible")]
    public string targetPortalId;

    [Header(" --- Ce portail --- ")]
    [Tooltip("Id de ce portail, utilisé quand on arrive depuis une autre scène")]
    public string portalId;
    [Tooltip("Où le joueur réapparaît quand il arrive par ce portail")]
    public Transform arrivalPoint;

    [Header(" --- UI --- ")]
    public GameObject promptPanel;

    // Portail sur lequel faire apparaître le joueur au prochain chargement de scène
    private static string _pendingArrivalId;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => _pendingArrivalId = null;

    private InputAction _interactAction;
    private bool _isPlayerInZone;
    private bool _isLoading;

    private void Awake()
    {
        var playerInput = FindAnyObjectByType<PlayerInput>();
        if (playerInput != null) _interactAction = playerInput.actions["Interact"];
        if (promptPanel) promptPanel.SetActive(false);
    }

    private void Start()
    {
        if (string.IsNullOrEmpty(_pendingArrivalId) || _pendingArrivalId != portalId) return;
        _pendingArrivalId = null;
        PlacePlayerAtArrival();
    }

    private void Update()
    {
        if (!_isPlayerInZone || _isLoading || _interactAction == null) return;
        if (_interactAction.WasPressedThisFrame()) LoadTarget();
    }

    public void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        _isPlayerInZone = true;
        _interactAction?.Enable();
        if (promptPanel) promptPanel.SetActive(true);
    }

    public void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        _isPlayerInZone = false;
        if (promptPanel) promptPanel.SetActive(false);
    }

    private void LoadTarget()
    {
        _isLoading = true;
        _pendingArrivalId = targetPortalId;
        SceneManager.LoadScene(targetScene);
    }

    private void PlacePlayerAtArrival()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null || arrivalPoint == null) return;

        // Le CharacterController écrase la position si on ne le coupe pas pendant le déplacement
        var controller = player.GetComponent<CharacterController>();
        if (controller) controller.enabled = false;
        player.transform.SetPositionAndRotation(arrivalPoint.position, Quaternion.Euler(0f, arrivalPoint.eulerAngles.y, 0f));
        if (controller) controller.enabled = true;
    }
}
