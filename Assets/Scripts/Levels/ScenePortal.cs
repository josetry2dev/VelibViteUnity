using System.Collections.Generic;
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

    [Header(" --- Dialogue d'entrée (optionnel) --- ")]
    [Tooltip("Dialogue SMS joué avant d'entrer, une seule fois jusqu'au prochain retour au menu")]
    public SmsDialogue entryDialogue;
    public SmsManager smsManager;

    // Portail sur lequel faire apparaître le joueur au prochain chargement de scène
    private static string _pendingArrivalId;
    // Portails dont le dialogue d'entrée a déjà été joué depuis le dernier passage au menu
    private static readonly HashSet<string> _playedEntryDialogues = new HashSet<string>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _pendingArrivalId = null;
        _playedEntryDialogues.Clear();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Retour au menu (build index 0) : les dialogues d'entrée peuvent se rejouer
        if (scene.buildIndex == 0) _playedEntryDialogues.Clear();
    }

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
        if (_interactAction.WasPressedThisFrame()) TryEnter();
    }

    public void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        _isPlayerInZone = true;
        _interactAction?.Enable();
        if (promptPanel && !_isLoading) promptPanel.SetActive(true);
    }

    public void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        _isPlayerInZone = false;
        if (promptPanel) promptPanel.SetActive(false);
    }

    private void TryEnter()
    {
        if (entryDialogue == null || _playedEntryDialogues.Contains(portalId))
        {
            LoadTarget();
            return;
        }

        // Le SmsManager est sur le SMSPanel, désactivé tant qu'aucun dialogue ne tourne
        if (smsManager == null) smsManager = FindAnyObjectByType<SmsManager>(FindObjectsInactive.Include);
        if (smsManager == null)
        {
            Debug.LogWarning($"ScenePortal '{portalId}' : aucun SmsManager trouvé, on entre sans dialogue.", this);
            LoadTarget();
            return;
        }

        _isLoading = true;
        _playedEntryDialogues.Add(portalId);
        if (promptPanel) promptPanel.SetActive(false);
        SetPlayerMovementLocked(true);
        // Pas besoin de déverrouiller : la scène change à la fin du dialogue
        smsManager.StartDialogue(entryDialogue, LoadTarget);
    }

    private void LoadTarget()
    {
        _isLoading = true;
        _pendingArrivalId = targetPortalId;
        SceneManager.LoadScene(targetScene);
    }

    private static void SetPlayerMovementLocked(bool locked)
    {
        var fpsController = FindAnyObjectByType<FpsPlayerController>();
        if (fpsController) fpsController.MovementLocked = locked;
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
